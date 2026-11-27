using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
using Anomaly.Core.Tg;
using Anomaly.Protocol;
using Anomaly.Server.Replication;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server;

// The world half of the host: the command phase (a World phase: BYOND's step 6, client verbs after the MC,
// waiting-model.md 7.1), the avatar and its capsule, the input and action commands, the client host seam (IClientHost) and
// the logins polled after each Step.
//
// Determinism (D31, scope 2j): what this half does in the world depends only on the applied commands and on the world
// itself, never on the network side of a session (its State, strikes, buckets, jitter buffer or connection), so a replay of
// the recorded commands gives the world of the live run. The fields it uses are the session's world half: Client, Login,
// LastFrameSeq, LastYaw, OwnEntity, and the host's avatar table and spawn cursor.
public sealed partial class ServerHost
{
    private readonly List<QueuedCommand> _due = new();
    private readonly Dictionary<string, long> _avatarIds = new(StringComparer.Ordinal); // ckey -> the avatar mob's world id
    private int _spawnCursor;
    private bool _shutdownApplied;
    private Session _connecting;           // the session whose ClientSeam.Connect is running (its client is not known yet)
    private bool _connectingClientDeleted; // ... and whether the world deleted that client meanwhile

    /// <summary>The world id of the avatar of a key (compared as ckeys), 0 when none was made.</summary>
    public long AvatarOf(string key) => _avatarIds.TryGetValue(Dm.ckey(key), out long id) ? id : 0;

    // ---- the command phase ----

    private void CommandPhase(World world)
    {
        if (world.Tick % 20 == 0)
            UpdateStatus(); // D335: once a second, here where the world can be read
        // M10 slice F: tg's ticker drives the runlevels from the Master's RUNLEVEL_LOBBY (its pregame countdown, the setup, the
        // round). Without the lobby (D270) the round starts at once, as tg's start_immediately (the lobby's Start Now), and joins open
        // once it is in progress, so every join late-joins before its Welcome.
        if (!_config.Empty && !_config.Lobby && MasterInitialized() && !Dm.truthy(SSticker.start_immediately))
            SSticker.start_immediately = 1;
        if (!_config.Empty && !JoinsOpen && MasterInitialized() && (_config.Lobby || Dm.truthy(SSticker.IsRoundInProgress())))
        {
            CeilingSystem.Of(world).Initialize();
            JoinsOpen = true;
            JoinsOpenedAt = world.Tick;
            if (_config.MapEditor)
                SSticker.SetTimeLeft(-1); // tg's delayed round start (ticker.dm: the countdown returns while timeLeft is under 0): the editor's map is never played
            _replicator.RereadMap(); // M3 R4: the map as the init left it, for the first join (replication.md 8)
            JoinWaiting(world); // D306
            _out.WriteLine(string.Create(CultureInfo.InvariantCulture, $"joins open at tick {world.Tick} (the Master initialized; {(_config.Lobby ? "the lobby" : "no lobby: the round is in progress")})"));
            _out.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"status boot: heap {GC.GetTotalMemory(false) / (1024 * 1024)} MB, working set {Environment.WorkingSet / (1024 * 1024)} MB, gc gen0 {GC.CollectionCount(0)} gen1 {GC.CollectionCount(1)} gen2 {GC.CollectionCount(2)} pause {GC.GetTotalPauseDuration().TotalMilliseconds:0} ms, server gc {System.Runtime.GCSettings.IsServerGC}, latency {System.Runtime.GCSettings.LatencyMode} | inits (ms): ")
                + string.Join(", ", Profiler.Inits.OrderByDescending(i => i.Ms).Take(12).Select(i => string.Create(CultureInfo.InvariantCulture, $"{i.Name} {i.Ms:0}"))));
        }

        FindSubsystems();
        _due.Clear();
        _queue.TakeDue(world.Tick, _due);
        foreach (QueuedCommand command in _due)
        {
            Log.Add(command);
            try
            {
                Apply(world, command);
            }
            catch (Exception e)
            {
                world.ReportRuntime(e); // each command is an isolation boundary, as a verb is its own thread in BYOND
            }
            finally
            {
                world.usr = null;
                _connecting = null;
            }
        }
        _due.Clear();
        AutoRespawns(world); // DV580: before the direct joins, which late-join the respawned players
        DirectJoins(world);
    }

    // ---- the input of the tick (M2, scope 2d "Server jitter buffer") ----

    // Network side, before World.Step: each joined session's jitter buffer gives the frame of the tick (one per tick; the last
    // repeats when none is queued, standing in for the next seq (D331), and goes idle after half a second of silence). The frame becomes a recorded Motion
    // command when it differs from the one before: the locomotion seam keeps a mob's last frame, so an unchanged repeat is
    // no command (the log holds changes, as M1's held keys did), but the frame's seq is acknowledged all the same
    // (lastInputSeq). The actions whose frame has been taken follow as commands of the same tick, after the Motion.
    private void FormInputs(long applyTick)
    {
        foreach (Session s in _sessions)
        {
            if (s.State != SessionState.Joined) continue;
            // D331: for half a second of silence a repeat stands in for the next seq, which is acknowledged, so the client's prediction (which
            // stepped that seq with the same held keys) agrees; after it the idle frame is no stand-in
            if (s.Jitter.TryTake(out MotionInput frame, out bool repeated, standIn: s.RepeatedTicks < ActionLimits.MaxRepeatedFrameTicks))
            {
                if (repeated)
                {
                    if (++s.RepeatedTicks > ActionLimits.MaxRepeatedFrameTicks)
                        frame = MotionInput.Idle(frame.Seq, frame.Yaw);
                }
                else
                {
                    s.RepeatedTicks = 0;
                }
                s.LastAppliedInputSeq = s.Jitter.LastTakenSeq;
                if (!s.HaveQueuedFrame || !SameWish(s.LastQueuedFrame, frame))
                {
                    s.LastQueuedFrame = frame;
                    s.HaveQueuedFrame = true;
                    _queue.Enqueue(new QueuedCommand(applyTick, s.Ordinal, frame.Seq, CommandKind.Motion, Motion: frame));
                }
            }
            if (s.PendingActions.Count == 0) continue;
            uint taken = s.Jitter.LastTakenSeq;
            int kept = 0;
            for (int i = 0; i < s.PendingActions.Count; i++)
            {
                ActionMessage a = s.PendingActions[i];
                long stamp = s.PendingStamps[i];
                if (a.FrameSeq > taken)
                {
                    s.PendingActions[kept] = a;
                    s.PendingStamps[kept++] = stamp;
                    continue;
                }
                _queue.Enqueue(new QueuedCommand(applyTick, s.Ordinal, taken, CommandKind.Action, a.Text, Action: a.Kind, Target: a.Target, Button: a.Button, Modifiers: a.Modifiers,
                    TargetKind: a.EffectiveTargetKind, TurfX: a.TurfX, TurfY: a.TurfY, TurfZ: a.TurfZ, IconX: a.IconX, IconY: a.IconY, Keybinding: a.Keybinding, KeyUp: a.KeyUp, AimZone: a.AimZone,
                    RadialMenu: a.RadialMenu, RadialElement: a.RadialElement,
                    UiWindow: a.UiWindow, UiParams: a.UiParams?.ToJson(), OverKind: a.OverKind, OverTarget: a.OverTarget, OverX: a.OverX, OverY: a.OverY, OverZ: a.OverZ,
                    DragTime: a.DragTime, Aim: a.Aim, SayType: a.SayType, SayChannel: a.SayChannel, SayVisible: a.SayVisible) with { Text = a.Kind == ActionKind.UiAct ? a.UiAction : a.Text });
                if (a.Kind is ActionKind.Click or ActionKind.Throw or ActionKind.PutDown) _clickStamps.Add(stamp);
            }
            s.PendingActions.RemoveRange(kept, s.PendingActions.Count - kept);
            s.PendingStamps.RemoveRange(kept, s.PendingStamps.Count - kept);
        }
    }

    private static bool SameWish(in MotionInput a, in MotionInput b)
        => a.MoveX == b.MoveX && a.MoveY == b.MoveY && a.Yaw == b.Yaw && a.Flags == b.Flags;

    // ---- SSmobs' peak tick (metrics) ----

    private datum_controller_subsystem_mobs _ssMobs;
    private float _ssMobsFired;

    // Whether SSmobs fired in the tick that was just stepped (its times_fired grew): the tick that runs every living mob's
    // Life, the heaviest one (DV3: it never yields). The subsystem is looked up once, inside the world.
    private bool MobsFired()
    {
        if (_ssMobs == null) return false;
        float fired = _ssMobs.times_fired;
        bool grew = fired != _ssMobsFired;
        _ssMobsFired = fired;
        return grew;
    }

    private void FindSubsystems()
    {
        if (_ssMobs != null || Master == null) return;
        foreach (datum_controller_subsystem_mobs ss in Dm.each<datum_controller_subsystem_mobs>(Master.subsystems))
        {
            _ssMobs = ss;
            _ssMobsFired = ss.times_fired;
            break;
        }
    }

    private static bool MasterInitialized()
        => Master != null && Dm.eq(datum_controller_master.init_stage_completed, INITSTAGE_MAX);

    private void Apply(World world, in QueuedCommand command)
    {
        switch (command.Kind)
        {
            case CommandKind.Join:
                ApplyJoin(world, command);
                break;
            case CommandKind.Leave:
                ApplyLeave(world, SessionOf(command.Session));
                break;
            case CommandKind.Input:
                break; // protocol version 1's InputFrame: a version-2 server never queues one (an old log is refused by its format)
            case CommandKind.Motion:
                ApplyMotion(world, SessionOf(command.Session), command);
                break;
            case CommandKind.Action:
                ApplyAction(world, SessionOf(command.Session), command);
                break;
            case CommandKind.Console:
                ApplyConsole(world, command.Text);
                break;
        }
    }

    // M10 slice F: a key no mob holds connects and BYOND's client/New makes it tg's world.mob, the lobby's /mob/dead/new_player;
    // its mob is registered with the locomotion seam when tg moves the key into a living body (ClientMobChanged). Without the lobby
    // (D270) that new player late-joins at once as the overflow job at the host's next spawn point (DirectJoins). A reconnect finds
    // the mob that kept its key (tg's SSD) and makes nothing: it is still registered and stands where it was.
    private void ApplyJoin(World world, in QueuedCommand command)
    {
        Session s = SessionOf(command.Session);
        if (s == null)
        {
            // A replay: the session has no connection, only its world half.
            s = new Session(command.Session, command.Text, SessionToken.None, null) { State = SessionState.Joining };
            AddSession(s);
            _lastOrdinal = Math.Max(_lastOrdinal, s.Ordinal);
        }
        // From here a failure (a runtime in KeyMob or Connect) leaves the session LoggingIn without a login, which
        // PollLogins refuses after the tick.
        if (s.State == SessionState.Joining) s.State = SessionState.LoggingIn;
        s.AwaitingLoad = command.WaitsForLoad; // DV597: until its client's Loaded, a new player stays in the lobby
        if (Room == null && !MapLoaded)
            throw new InvalidOperationException("a join needs the tg world (the server runs --empty)");

        // BYOND lets a key connect once: a session of this key that still has a client in the world (only a replaced one:
        // its Leave has a lower ordinal and applies first in this tick, scope 2j) leaves before the new client is made, so
        // tg sees the old client's Logout before the new one's Login.
        foreach (Session other in _sessions)
        {
            if (other != s && other.Ckey == s.Ckey && (other.Client != null || other.Login != null))
                ApplyLeave(world, other);
        }

        InstallScreentips(world);
        long joinStart = Stopwatch.GetTimestamp();
        mob avatar = FindAvatar(s.Ckey);
        bool newPlayer = avatar == null; // BYOND's client/New makes the key a world.mob, tg's /mob/dead/new_player

        world.usr = avatar;
        _connecting = s;
        _connectingClientDeleted = false;
        Profiler.Begin(JoinConnectPiece);
        // an in-process loopback client has no address: it is the host's own, as BYOND's local connection is
        ILoginHandle login = _clients.Connect(s.Key, s.Address ?? "127.0.0.1");
        Profiler.End(JoinConnectPiece);
        s.Login = login;
        s.Client = _connectingClientDeleted ? null : login.Client;
        AfterClientNew(s); // M10 slice I: the key's unread notes, the watchlist notice to the admins
        avatar ??= s.Client?.mob;
        if (!_config.Lobby && avatar is mob_dead_new_player && !_directJoins.Contains(s))
            _directJoins.Add(s);
        if (avatar is mob_living living && !_locomotion.IsRegistered(avatar))
            _locomotion.Register(living);
        else if (avatar is mob_dead_observer joinedGhost && !_locomotion.IsRegistered(avatar))
            _locomotion.RegisterGhost(joinedGhost); // DV68: back into one's ghost
        // M3 I1d: each join's step time (wall clock, a status line outside the deterministic output).
        double joinMs = (Stopwatch.GetTimestamp() - joinStart) * 1000.0 / Stopwatch.Frequency;
        _out.WriteLine(string.Create(CultureInfo.InvariantCulture, $"status join step #{s.Ordinal} {s.Key}: {joinMs:0.0} ms{(newPlayer ? "" : " (back to its mob)")}"));
        DirectJoins(world);
    }

    // M3 I1d: the pieces of a join in the step profiler's slow-tick lines (the join stall).
    private const string JoinConnectPiece = "join: client/New";
    private const string JoinLateSpawnPiece = "join: late spawn";

    // D270: without the lobby, the new players that wait for the round to start (it starts at the ticker's first fire), and DV597's that wait for
    // their client to load the world (Session.AwaitingLoad).
    private readonly List<Session> _directJoins = new();

    // D270: without the lobby, each waiting new player late-joins as tg's overflow job once the round is in progress, as tg's
    // AttemptLateSpawn after the Join button and the job menu, at the host's next spawn point: the host's spawn point is the job's
    // spawn override for that one late join (GLOB.jobspawn_overrides, tg's "something special today", job.dm:513).
    private void DirectJoins(World world)
    {
        if (_directJoins.Count == 0 || !Dm.truthy(SSticker?.IsRoundInProgress()))
            return;
        foreach (Session s in _directJoins.ToArray())
        {
            // DEVIATION(DV597): a client still loading the world (its loading screen up) is late-joined once it says it is loaded, as BYOND's
            // PRELOAD_RSC sends the resources before the login; until then its key stays tg's new player, with no body
            if (s.AwaitingLoad && s.State != SessionState.Closed)
                continue;
            _directJoins.Remove(s);
            // (the map editor's Play as a new arrival: tg's own late-join point, the arrivals shuttle's chair, and no spawn point of the host's)
            bool arrive = _config.JoinAsArrival;
            if (s.Client?.mob is not mob_dead_new_player player || QDELETED(player) || (!arrive && SpawnPoints.Count == 0))
                continue;
            object rank = (SSjob.get_job_type(SSjob.overflow_role) as datum_job)?.title;
            object kept = GLOB.jobspawn_overrides[rank];
            if (!arrive)
            {
                SpawnPoint at = SpawnPoints[_spawnCursor++ % SpawnPoints.Count];
                GLOB.jobspawn_overrides[rank] = Dm.list(locate(at.X, at.Y, at.Z));
            }
            world.usr = player;
            Profiler.Begin(JoinLateSpawnPiece);
            try
            {
                Waiting.Detach(player.AttemptLateSpawn(rank));
            }
            finally
            {
                Profiler.End(JoinLateSpawnPiece);
                if (!arrive && kept == null)
                    Dm.remove(ref GLOB.jobspawn_overrides, rank);
                else if (!arrive)
                    GLOB.jobspawn_overrides[rank] = kept;
                world.usr = null;
            }
        }
    }

    // The avatar of a ckey: the mob the host made for it, while it lives and still has the key.
    private mob FindAvatar(string ckey)
    {
        if (!_avatarIds.TryGetValue(ckey, out long id)) return null;
        if (!World.Objects.TryGet(id, out object o) || o is not mob m || QDELETED(m) || m.ckey != ckey) return null;
        return m;
    }

    /// <summary>
    /// Version 95: the cooldown a melee blow with the active hand would set now, milliseconds: the held thing's <c>attack_speed</c> (an empty
    /// hand's <c>CLICK_CD_MELEE</c>) through the mob's modifiers and its status effects', as mob_living's changeNext_move takes it (click.dm:23).
    /// </summary>
    internal static int MeleeCooldownMs(mob own)
    {
        if (own is not mob_living living)
            return OwnBlock.DefaultMeleeCooldownMs;
        float speed = living.get_active_held_item() is obj_item held ? held.attack_speed : CLICK_CD_MELEE;
        float mod = living.next_move_modifier, adj = living.next_move_adjust;
        foreach (datum_status_effect effect in Dm.each_cast<datum_status_effect>(living.status_effects))
        {
            mod *= Dm.num(effect.nextmove_modifier());
            adj += Dm.num(effect.nextmove_adjust());
        }
        return (int)Math.Clamp(Math.Round((speed + adj) * mod * 100), 0, OwnBlock.MaxMeleeCooldownMs);
    }

    // Version 43 (M10 slice E): each joined player's access, tg's get_access() (the IDs held and worn answer COMSIG_MOB_RETRIEVE_ACCESS),
    // read inside the world once a tick after the step, for the private block: the client guesses which airlocks open for it. A read
    // only (the replicator calls no proc; this getter sends a signal its listeners answer by adding to the list).
    private void ReadAccess()
    {
        using (World.Enter())
        {
            foreach (Session s in _sessions)
            {
                if (s.State != SessionState.Joined) continue;
                mob own = MobOf(s);
                // version 89: click.dm ClickOn's first refusals, before the click reaches what was clicked
                s.HandsBlocked = own != null && (Dm.truthy(INCAPACITATED_IGNORING(own, INCAPABLE_RESTRAINTS | INCAPABLE_STASIS)) || HAS_TRAIT(own, TRAIT_HANDS_BLOCKED));
                s.MeleeCooldownMs = MeleeCooldownMs(own); // version 95
                List<string> now = (own as mob_living)?.get_access() is DmList list
                    ? Dm.each(list).Select(a => Dm.str(a)).Where(a => !string.IsNullOrEmpty(a) && Encoding.UTF8.GetByteCount(a) <= Limits.MaxAccessBytes).Distinct().Take(Limits.MaxAccessEntries).ToList()
                    : new List<string>();
                if (s.Access == null || !s.Access.SequenceEqual(now))
                    s.Access = now;
            }
        }
    }

    private mob MobOf(Session s)
    {
        if (s.OwnEntity == 0 || !World.Objects.TryGet(s.OwnEntity, out object o)) return null;
        return o as mob;
    }

    // tg's client/Del through the seam (the connection ended); the mob keeps its key. Nothing if the world already
    // deleted the client (a kick, tg's flood autokick).
    private void ApplyLeave(World world, Session s)
    {
        if (s == null) return;
        client player = s.Client ?? s.Login?.Client;
        s.Login = null;
        if (player != null)
        {
            world.usr = MobOf(s);
            if (world.usr is mob_living leaving && _locomotion.IsRegistered(leaving))
                _locomotion.SetFrame(leaving, MotionInput.Idle(s.LastFrameSeq, s.LastYaw)); // the mob stays, standing (tg's SSD)
            _clients.Disconnect(player);
        }
        s.Client = null;
        s.Outbox.Clear();
        s.ControlOutbox.Clear();
        s.SpeechOutbox.Clear();
        s.PendingActions.Clear();
        s.PendingStamps.Clear();
        if (s.State != SessionState.Closed)
        {
            s.State = SessionState.Closed;
            _out.WriteLine(string.Create(CultureInfo.InvariantCulture, $"left #{s.Ordinal} {s.Key} at tick {world.Tick}"));
        }
        _forget.Add(s.Ordinal);
    }

    // A MotionInput frame (the one the jitter buffer gave this tick, when it differs from the one before): the locomotion seam's
    // frame for the tick. The seam keeps a mob's last frame when none comes, so an unchanged repeat is no command.
    private void ApplyMotion(World world, Session s, in QueuedCommand command)
    {
        if (s == null || s.Client == null) return;
        s.LastFrameSeq = command.Seq; // the world half: the network half's acknowledgement (LastAppliedInputSeq) follows frames that are no command
        s.LastYaw = command.Motion.Yaw;
        mob avatar = MobOf(s);
        if (avatar == null || !_locomotion.IsRegistered(avatar))
        {
            // DEVIATION(DV15): a mob the locomotion bridge does not drive moves by tg's own keyLoop and client/Move tile steps
            // (bindings_atom.dm): the frame's wish is its held movement keys, as BYOND's keyDown and keyUp verbs give them; the wish
            // is in the world's frame (the client turns its keys by the camera's yaw), so the client's dir stays NORTH and keyLoop
            // turns nothing. (A ghost flies by the bridge since DV68.)
            if (avatar != null)
            {
                world.usr = avatar;
                _clients.SetHeldDirections(s.Client, LocomotionSeam.WishDir(command.Motion.MoveX, command.Motion.MoveY));
            }
            return;
        }
        world.usr = avatar;
        if (s.Client.keys_held.Count > 0)
            _clients.SetHeldDirections(s.Client, 0); // the keys a ghost held are let go when the player is back in a capsule mob
        _locomotion.SetFrame(avatar, command.Motion);
    }

    // An action (scope 2b, 2f, 2k): each is tg's own path with usr the player's mob, as a verb call is in BYOND.
    private void ApplyAction(World world, Session s, in QueuedCommand command)
    {
        if (s == null || s.Client == null) return;
        world.usr = MobOf(s);
        switch (command.Action)
        {
            case ActionKind.Rest:
                _clients.Rest(s.Client);
                break;
            case ActionKind.ToggleMoveIntent:
                _clients.ToggleMoveIntent(s.Client);
                break;
            case ActionKind.ToggleCombatMode:
                _clients.ToggleCombatMode(s.Client);
                break;
            case ActionKind.Click:
                ApplyClick(s, command);
                break;
            case ActionKind.Throw:
                ApplyThrow(s, command);
                break;
            case ActionKind.PutDown:
                ApplyPutDown(s, command);
                break;
            case ActionKind.DblClick:
                ApplyDblClick(s, command);
                break;
            case ActionKind.Hover:
                ApplyHover(s, command);
                break;
            case ActionKind.Drop:
                ApplyDrop(s, command);
                break;
            case ActionKind.Unhover:
                _clients.Unhover(s.Client);
                s.HoversApplied++;
                break;
            case ActionKind.RadialPick:
                // version 15 (DV40): the slice's or the centre's Click of a radial menu shown to the player
                _clients.RadialPick(s.Client, command.RadialMenu, command.RadialElement);
                s.RadialPicksApplied++;
                break;
            case ActionKind.UiAct:
            case ActionKind.UiClose:
                ApplyUi(s, command);
                break;
            case ActionKind.Keybinding:
                _clients.Keybinding(s.Client, (byte)command.Keybinding, !command.KeyUp);
                s.KeybindingsApplied++;
                break;
            case ActionKind.DevCommand:
                ApplyDevAction(s, command.Text);
                break;
            case ActionKind.DevGasProbe:
                ApplyDevGasProbe(s, command);
                break;
            case ActionKind.Topic:
                // version 24 (D154): a chat link the session was sent (the intake's check); tg's /client/Topic and the src's Topic
                _clients.Topic(s.Client, command.Text);
                s.TopicsApplied++;
                break;
            case ActionKind.MouseDown:
            case ActionKind.MouseDrag:
            case ActionKind.MouseUp:
            case ActionKind.MouseWheel: // version 70
                ApplyMouse(s, command);
                break;
            case ActionKind.Say:
                // version 38: the say window's message, as tg's window sends it to the client's /datum/tgui_say
                _clients.Say(s.Client, command.SayType, command.SayChannel, command.Text, command.SayVisible);
                break;
            case ActionKind.Emote:
                // version 39 (D256): the emote wheel's pick, tg's emote keybinding
                _clients.Emote(s.Client, command.Text);
                break;
            case ActionKind.Verb:
                // version 78: a command verb of the stat panel
                _clients.Verb(s.Client, command.Text);
                break;
            case ActionKind.Loaded:
                // version 97 (DV597): the client has loaded the world; a new player waiting for that joins the round now (no lobby)
                s.AwaitingLoad = false;
                DirectJoins(world);
                break;
            case ActionKind.ContextPick:
                // version 86 (DV503): the radial's entry that is no click, on a target checked as a click's
                if (TryResolveTarget(s, command, out atom picked))
                    _clients.ContextPick(s.Client, picked, command.Text);
                else
                    s.ClicksRejected++;
                break;
            case ActionKind.PopupClose:
                // version 78 (DV387): a popup window's on-close command
                _clients.ClosePopup(s.Client, command.Text);
                break;
            default:
                return;
        }
        s.ActionsApplied++;
    }

    // tg's view range in tiles (a 15 x 15 view): a click or hover target farther than this from the player's mob, or on another
    // level, is forged or stale (interaction.md section 2). Reach is tg's own (Adjacent, IsReachableBy) and stricter.
    private const int InteractionViewRange = 7;

    // The target of a click or a hover (D40, interaction.md section 2), resolved in the command phase where the world is at the
    // tick of the player's frame: an entity must be one the replicator shows (a registered movable on a turf, not invisible)
    // on the mob's level within view range; a turf must be inside the map, on the mob's level and within view range; a screen
    // object must be in the client's screen list now, or (protocol version 12) be a clickable vis_contents child of an object
    // there: BYOND sends a click on the item drawn in a hand or slot (inventory_slot.dm:55) to the item itself
    // (ClientSeam.ScreenHolderOf). A forged or stale id is dropped and counted (ClicksRejected).
    private bool TryResolveTarget(Session s, in QueuedCommand command, out atom target)
    {
        TargetKind kind = command.TargetKind != TargetKind.None ? command.TargetKind : command.Target > 0 ? TargetKind.Entity : TargetKind.None;
        return TryResolveTarget(s, kind, command.Target, command.TurfX, command.TurfY, command.TurfZ, out target);
    }

    // Version 17: the same rules for any target of a command (a drop names two: the dragged thing and what it was released over).
    private bool TryResolveTarget(Session s, TargetKind kind, long id, int x, int y, int z, out atom target)
    {
        target = null;
        mob mob = MobOf(s);
        // BYOND: a client sees, and so clicks, what is in view of its eye (client.eye: the mob, or a camera eye such as the AI's, silicons SI4)
        atom viewer = s.Client?.eye is atom eye && !QDELETED(eye) ? eye : mob;
        turf here = mob == null || QDELETED(mob) ? null : get_turf(viewer);
        switch (kind)
        {
            case TargetKind.Entity:
                if (!World.Objects.TryGet(id, out object found) || !ReplicatedFields.IsEntity(found, out atom_movable entity, out turf on) || QDELETED(entity))
                    return false;
                // Version 22: BYOND shows no atom above the viewer's see_invisible, so a client cannot target one (a living player
                // never has a ghost's id; one that guesses it is refused like an unknown id).
                if (mob == null || entity.invisibility > mob.see_invisible)
                    return false;
                if (!InViewOf(here, on))
                    return false;
                target = entity;
                return true;
            case TargetKind.Ceiling:
            case TargetKind.Turf:
                if (x < 1 || x > Limits.MaxMapSize || y < 1 || y > Limits.MaxMapSize || z < 1 || z > Limits.MaxMapLevels)
                    return false;
                turf cell = locate(x, y, z);
                if (cell == null || !InViewOf(here, cell))
                    return false;
                target = cell;
                return true;
            case TargetKind.Screen:
                if (s.Client == null || !World.Objects.TryGet(id, out object screen) || screen is not atom_movable shown || QDELETED(shown))
                    return false;
                if (!Dm.@in(shown, s.Client.screen) && ClientSeam.ScreenHolderOf(s.Client, shown) == null)
                    return false;
                target = shown;
                return true;
            default:
                return false;
        }
    }

    private static bool InViewOf(turf here, turf there)
        => here != null && there != null && (Dm.eq(here.z, there.z)
            ? Dm.le(get_dist(here, there), InteractionViewRange)
            : OnOneStack(here, there) && Math.Max(Math.Abs(Dm.num(here.x) - Dm.num(there.x)), Math.Abs(Dm.num(here.y) - Dm.num(there.y))) <= InteractionViewRange);

    // D325 (the owner, 2026-11-10: hovering, pointing and shooting at another level were refused here): the 3D client shows the levels of
    // the eye's stack through open space and the floor over it, so a target on a linked level is in view as one on the own level would be.
    // tg's own checks still run on it (reach is Adjacent, one level; pointing's view, DV393).
    private static bool OnOneStack(turf here, turf there)
    {
        float target = Dm.num(there.z);
        bool up = target > Dm.num(here.z);
        for (turf t = here; t != null; t = (up ? GET_TURF_ABOVE(t) : GET_TURF_BELOW(t)) as turf)
        {
            if (Dm.num(t.z) == target)
                return true;
        }
        return false;
    }

    // A click (BYOND's Click, tg's ClickOn): the target is validated above; BYOND hands Click the params text of
    // ClientSeam.ClickParams with tg's icon-x and icon-y and, for a screen object, its own screen_loc; the click is a verb call,
    // detached from the phase by the seam. The screen_loc is read from the object, never from the client.
    private void ApplyClick(Session s, in QueuedCommand command)
    {
        string button = command.Button switch
        {
            ClickButton.Left => LEFT_CLICK,
            ClickButton.Right => RIGHT_CLICK,
            ClickButton.Middle => MIDDLE_CLICK,
            _ => null,
        };
        if (button == null || !TryResolveTarget(s, command, out atom target))
        {
            s.ClicksRejected++;
            return;
        }
        // Version 12: a vis child of a screen object has the holder's place on the screen (the item's own screen_loc is stale: tg
        // sets it only while a storage shows the item); BYOND's screen-loc is where the mouse was, which is over the holder.
        string screenLoc = target is atom_movable shown && command.TargetKind == TargetKind.Screen
            ? (Dm.@in(shown, s.Client.screen) ? shown : ClientSeam.ScreenHolderOf(s.Client, shown))?.screen_loc
            : null;
        string clickParams = ClientSeam.ClickParams(button, (command.Modifiers & ClickModifiers.Shift) != 0, (command.Modifiers & ClickModifiers.Ctrl) != 0,
            (command.Modifiers & ClickModifiers.Alt) != 0, command.IconX, command.IconY, screenLoc) + AimParamOf(s, command.Aim);
        if (command.AimZone != AimZone.None && command.TargetKind == TargetKind.Entity && target is mob_living_carbon && s.Client.mob is mob aimer)
            AimAt(aimer, command.AimZone);
        // the dev hook of `shoot WHO TYPE` (M7x slice A): the click fires a projectile with its params, as a gun's would
        if (s.Client.dm_dev_shot is Type shot && (_config.DevCommandsEnabled || s.Client.holder != null) && s.Client.mob is mob shooter)
        {
            DevCommands.FireAimed(shooter, target, shot, clickParams);
            return;
        }
        if (command.TargetKind == TargetKind.Ceiling && target is turf ceilingCell && s.Client.mob is mob_living builder
            && builder.get_active_held_item() is not obj_item_gun)
        {
            if (command.Button == ClickButton.Left && (command.Modifiers & ~ClickModifiers.Alt) == 0)
                Waiting.Detach(CeilingSystem.Of(World).Interact(builder, ceilingCell));
            return;
        }
        _clients.Click(s.Client, target, clickParams);
    }

    // Version 81 (D328): a throw at the target, tg's left click in throw mode with the click's params and the camera's aim (ClientSeam.Throw)
    private void ApplyThrow(Session s, in QueuedCommand command)
    {
        if (!TryResolveTarget(s, command, out atom target))
        {
            s.ClicksRejected++;
            return;
        }
        string clickParams = ClientSeam.ClickParams(LEFT_CLICK, false, false, false, command.IconX, command.IconY, null) + AimParamOf(s, command.Aim);
        if (command.AimZone != AimZone.None && command.TargetKind == TargetKind.Entity && target is mob_living_carbon && s.Client.mob is mob aimer)
            AimAt(aimer, command.AimZone);
        _clients.Throw(s.Client, target, clickParams);
    }

    // Version 82 (D329): the drop key at the crosshair: the target checked as a click's, then ClientSeam.PutDown with the click's pixel and the camera's aim
    private void ApplyPutDown(Session s, in QueuedCommand command)
    {
        if (!TryResolveTarget(s, command, out atom target))
        {
            s.ClicksRejected++;
            return;
        }
        _clients.PutDown(s.Client, target, ClientSeam.ClickParams(LEFT_CLICK, false, false, false, command.IconX, command.IconY, null) + AimParamOf(s, command.Aim));
    }

    // Version 51 (M10x slice C): BYOND's DblClick, the second press of a double click (its Click came first): the target checked as a
    // click's, then /atom/DblClick with a click's params (tg's usr.DblClickOn: a ghost follows a movable or jumps to a turf).
    private void ApplyDblClick(Session s, in QueuedCommand command)
    {
        string button = command.Button switch
        {
            ClickButton.Left => LEFT_CLICK,
            ClickButton.Right => RIGHT_CLICK,
            ClickButton.Middle => MIDDLE_CLICK,
            _ => null,
        };
        if (button == null || !TryResolveTarget(s, command, out atom target))
        {
            s.ClicksRejected++;
            return;
        }
        string screenLoc = target is atom_movable shown && command.TargetKind == TargetKind.Screen
            ? (Dm.@in(shown, s.Client.screen) ? shown : ClientSeam.ScreenHolderOf(s.Client, shown))?.screen_loc
            : null;
        _clients.DblClick(s.Client, target, ClientSeam.ClickParams(button, (command.Modifiers & ClickModifiers.Shift) != 0, (command.Modifiers & ClickModifiers.Ctrl) != 0,
            (command.Modifiers & ClickModifiers.Alt) != 0, command.IconX, command.IconY, screenLoc));
    }

    // Version 34: BYOND's MouseDown, MouseDrag and MouseUp (tg's /client procs, which its full auto listens to). The target is checked as
    // a click's; one that is none, forged or stale is nothing under the mouse (a release must still reach tg: it stops the autofire). The
    // params are a click's with the aim.
    private void ApplyMouse(Session s, in QueuedCommand command)
    {
        string button = command.Button switch
        {
            ClickButton.Left => LEFT_CLICK,
            ClickButton.Right => RIGHT_CLICK,
            ClickButton.Middle => MIDDLE_CLICK,
            _ => null,
        };
        if (button == null)
            return;
        atom target = null;
        if (command.TargetKind != TargetKind.None && !TryResolveTarget(s, command, out target))
            target = null;
        string mouseParams = ClientSeam.ClickParams(button, (command.Modifiers & ClickModifiers.Shift) != 0, (command.Modifiers & ClickModifiers.Ctrl) != 0,
            (command.Modifiers & ClickModifiers.Alt) != 0, target == null ? 0 : command.IconX, target == null ? 0 : command.IconY, ScreenLocOf(s, target, command.TargetKind)) + AimParamOf(s, command.Aim);
        switch (command.Action)
        {
            case ActionKind.MouseDown:
                _clients.MouseDown(s.Client, target, mouseParams);
                break;
            case ActionKind.MouseDrag:
                _clients.MouseDrag(s.Client, target, mouseParams);
                break;
            case ActionKind.MouseWheel:
                // version 70: BYOND's wheel notch is 120; Left is a turn up (delta_y above 0)
                _clients.MouseWheel(s.Client, target, command.Button == ClickButton.Left ? 120 : -120, mouseParams);
                break;
            default:
                _clients.MouseUp(s.Client, target, mouseParams);
                break;
        }
        s.MouseActionsApplied++;
    }

    // DEVIATION(DV94): the click params' aim key (";anomaly-aim=..."), or nothing. The direction is the player's; the eye is trusted only near the
    // mob: within ShotGeometry.MaxEyeOffsetPixels of its own position (the capsule's, else its tile's centre) and between the floor and the
    // highest eye (standing 1.9 m, lying 0.8 m); otherwise the server's own position and eye height (1.3 m, lying 0.4 m) replace it.
    private string AimParamOf(Session s, ShotAim? aim)
    {
        if (aim is not ShotAim a || MobOf(s) is not mob m || QDELETED(m) || get_turf(m) is not turf here)
            return "";
        double east = a.East / 8.0, north = a.North / 8.0, up = a.Up / 1000.0 * 16.0; // 1/256 tile and millimetres to tile pixels
        double ownX = Dm.num(here.x) * ShotGeometry.PixelsPerTile, ownY = Dm.num(here.y) * ShotGeometry.PixelsPerTile;
        if (LocomotionSeam.TryGetState(World, m.dm_id, out Anomaly.Core.Motion.MotionState state, out _) && m.loc is turf)
            (ownX, ownY) = (state.X / 8.0, state.Y / 8.0);
        bool lying = m is mob_living living && living.body_position == LYING_DOWN;
        double dx = east - ownX, dy = north - ownY;
        // DV526: on the stairs' flight the client lifts the eye with the body
        double lift = ShotGeometry.TryFlight(here, ownX, ownY, out double flight, out _) ? flight : 0;
        if (dx * dx + dy * dy > ShotGeometry.MaxEyeOffsetPixels * ShotGeometry.MaxEyeOffsetPixels || up < 0 || up > lift + (lying ? ShotGeometry.MaxLyingEyePixels : ShotGeometry.MaxStandingEyePixels))
        {
            (east, north, up) = (ownX, ownY, lift + (lying ? ShotGeometry.LyingEyePixels : ShotGeometry.StandingEyePixels));
            s.AimsCorrected++;
        }
        string aimed = ";" + ClientSeam.AimParam(east, north, up, a.Yaw * 360.0 / 65536.0, a.Pitch * 360.0 / 65536.0);
        // DV527: how far behind the world the client drew the others (the command is applied on this tick), at most ShotRewind.MaxTicks
        if (a.Seen > 0 && World.Tick - a.SeenTicks > 0)
            aimed += ";" + ClientSeam.RewindParam(Math.Min(World.Tick - a.SeenTicks, ShotRewind.MaxTicks));
        return aimed;
    }

    // Version 17: a drop (BYOND's MouseDrag and MouseDrop, tg's /client/MouseDrop): both the dragged thing and what it was released over
    // are validated as a click's target is (the player's view, its client.screen and the vis children of its screen objects); a forged or
    // stale one is dropped and counted (DropsRejected). The params are BYOND's: the button, the modifiers and drag=<the button>, tg's
    // icon pixel of the thing under the pointer and, for a screen object, its own screen_loc (the press's for the drag, the release's
    // for the drop).
    private void ApplyDrop(Session s, in QueuedCommand command)
    {
        string button = command.Button switch
        {
            ClickButton.Left => LEFT_CLICK,
            ClickButton.Right => RIGHT_CLICK,
            ClickButton.Middle => MIDDLE_CLICK,
            _ => null,
        };
        if (button == null || !TryResolveTarget(s, command.TargetKind, command.Target, command.TurfX, command.TurfY, command.TurfZ, out atom dragged)
            || !TryResolveTarget(s, command.OverKind, command.OverTarget, command.OverX, command.OverY, command.OverZ, out atom over))
        {
            s.DropsRejected++;
            return;
        }
        bool shift = (command.Modifiers & ClickModifiers.Shift) != 0, ctrl = (command.Modifiers & ClickModifiers.Ctrl) != 0, alt = (command.Modifiers & ClickModifiers.Alt) != 0;
        string dragParams = ClientSeam.DropParams(button, shift, ctrl, alt, 0, 0, ScreenLocOf(s, dragged, command.TargetKind));
        string dropParams = ClientSeam.DropParams(button, shift, ctrl, alt, command.IconX, command.IconY, ScreenLocOf(s, over, command.OverKind));
        _clients.Drop(s.Client, dragged, over, dragParams, dropParams, command.DragTime);
        s.DropsApplied++;
    }

    // The screen_loc BYOND gives for a screen target (version 12: a vis child has its holder's place), null for a map target.
    private static string ScreenLocOf(Session s, atom target, TargetKind kind)
        => target is atom_movable shown && kind == TargetKind.Screen
            ? (Dm.@in(shown, s.Client.screen) ? shown : ClientSeam.ScreenHolderOf(s.Client, shown))?.screen_loc
            : null;

    // DEVIATION(DV30): a click on a carbon's picture selects the body zone under the crosshair before tg's Click, through tg's
    // own zone selector (set_selected_zone, so the HUD's doll shows it), where tg selects it on the zone selector alone.
    private static void AimAt(mob aimer, AimZone aim)
    {
        string zone = aim switch
        {
            AimZone.Head => BODY_ZONE_HEAD,
            AimZone.Chest => BODY_ZONE_CHEST,
            AimZone.LeftArm => BODY_ZONE_L_ARM,
            AimZone.RightArm => BODY_ZONE_R_ARM,
            AimZone.LeftLeg => BODY_ZONE_L_LEG,
            AimZone.RightLeg => BODY_ZONE_R_LEG,
            _ => null,
        };
        if (zone == null)
            return;
        if (aimer.hud_used?.screen_objects?[HUD_MOB_ZONE_SELECTOR] is atom_movable_screen_zone_sel selector)
            selector.set_selected_zone(zone, aimer);
        else
            aimer.zone_selected = zone;
    }

    // A hover (BYOND's MouseEntered, tg's /atom/MouseEntered): the crosshair's target changed. At most one a tick per session
    // (the receiving side keeps the newest pending hover). tg's SSmouse_entered then runs on_mouse_enter, whose screentip reaches
    // ScreentipRouter.
    // Version 16 (M5 slice 3, DV44): tg's tgui messages from the player's window N ("tgui-window-N"): an act ("act/<action>" with the
    // params JSON, json_decode'd as tgui_Topic does) or the close button ("close"). Whether the window is the player's is tgui_Topic's
    // check (ClientSeam.TguiMessage force-closes an unknown one).
    private void ApplyUi(Session s, in QueuedCommand command)
    {
        if (command.UiWindow is Limits.LobbyUiWindow or Limits.EscapeMenuUiWindow)
        {
            ApplyPageAct(s, command);
            return;
        }
        if (command.UiWindow is >= Limits.BrowseUiWindowFirst and < Limits.EscapeMenuUiWindow)
        {
            // M10x slice G6 (DV216): a browse() page has no acts (its links are Topic links); its close button closes BYOND's window
            if (command.Action == ActionKind.UiClose)
                _replicator.ForgetBrowse(s.Ordinal, command.UiWindow);
            return;
        }
        string window = datum_tgui_window.TGUI_WINDOW_ID(command.UiWindow);
        if (command.Action == ActionKind.UiClose)
        {
            _clients.TguiMessage(s.Client, window, "close", null);
            s.UiClosesApplied++;
            return;
        }
        // an act without parameters gets an empty list: tgui's act(action, payload = {}) always sends an object, which tgui_window's
        // Topic decodes to a list (tg's ui_act reads params["ref"] of every act: the air alarm's "alarm")
        DmList payload = command.UiParams == null ? Dm.list() : Dm.json_decode(command.UiParams) as DmList;
        _clients.TguiMessage(s.Client, window, "act/" + command.Text, payload);
        s.UiActsApplied++;
    }

    // M10 slice F (DV44): the lobby page's Byond.sendMessage('action', { action, ...payload }) (tgui-lobby LobbyMenu.tsx sendAction):
    // the client's act on the lobby's window is tg's "action" message to the lobby_menu window, its params with the action's name
    // added; the lobby has no close button (tg's page is the skin's pane), so a close does nothing. M10x slice G4: the escape menu's
    // page (tgui-escape-menu) sends its actions the same way, and has no close button either (its "closed" is an action).
    private void ApplyPageAct(Session s, in QueuedCommand command)
    {
        if (command.Action == ActionKind.UiClose)
            return;
        DmList payload = (command.UiParams == null ? null : Dm.json_decode(command.UiParams) as DmList) ?? Dm.list();
        payload["action"] = command.Text;
        string window = command.UiWindow == Limits.LobbyUiWindow ? Replication.Replicator.LobbyWindowId : Replication.Replicator.EscapeMenuWindowId;
        _clients.TguiMessage(s.Client, window, "action", payload);
        s.UiActsApplied++;
    }

    private void ApplyHover(Session s, in QueuedCommand command)
    {
        if (!TryResolveTarget(s, command, out atom target))
        {
            s.ClicksRejected++;
            return;
        }
        if (command.TargetKind == TargetKind.Ceiling && target is turf ceilingCell)
        {
            var state = CeilingSystem.Of(World).At((int)ceilingCell.x, (int)ceilingCell.y, (int)ceilingCell.z);
            var hint = new ContextHintsMessage { TargetKind = TargetKind.Ceiling, TurfX = (int)ceilingCell.x, TurfY = (int)ceilingCell.y, TurfZ = (int)ceilingCell.z,
                Name = $"Ceiling: {CeilingSystem.Of(World).NameAt((int)ceilingCell.x, (int)ceilingCell.y, (int)ceilingCell.z)}"
                    + (state.Broken != 0 ? " (broken)" : "") + (state.Damage != 0 ? $" ({CeilingState.LatticeIntegrity - state.Damage / 10f:0.#}/{CeilingState.LatticeIntegrity})" : "") + (state.Exists && !state.Supported ? " (NO SUPPORT)" : "") };
            while (s.ControlOutbox.Count >= Session.ControlOutboxCap) s.ControlOutbox.Dequeue();
            s.ControlOutbox.Enqueue(Codec.Encode(hint));
            return;
        }
        _clients.Hover(s.Client, target);
        s.HoversApplied++;
    }

    // ---- screentips (D125): tg's on_mouse_enter publishes the hovered target's name and context; the router turns it into a
    // ContextHints message for the hovering session (sent after the tick, with the chat). It only records: a sink never changes
    // the world (presentation-seam.md). A replay has no connections, so nothing is queued there.
    private bool _screentipsInstalled;

    private void InstallScreentips(World world)
    {
        if (_screentipsInstalled || _replay) return;
        _screentipsInstalled = true;
        ScreentipSeam.Install(world, new ScreentipRouter(this));
    }

    private sealed class ScreentipRouter : IScreentipSink
    {
        private readonly ServerHost _host;

        public ScreentipRouter(ServerHost host) => _host = host;

        public void Screentip(client viewer, atom target, string name, IReadOnlyList<ScreentipHint> hints)
        {
            Session s = _host.SessionOfClient(viewer);
            if (s == null || s.Connection == null || s.State != SessionState.Joined) return;
            var message = new ContextHintsMessage { Name = name == null ? null : Truncate(name, Limits.MaxContextNameBytes) };
            if (ClientSeam.IsScreenTarget(viewer, target)) // version 12: an item in a hand or slot is a screen target too
            {
                message.TargetKind = TargetKind.Screen;
                message.Target = target.dm_id;
            }
            else if (target is turf cell)
            {
                message.TargetKind = TargetKind.Turf;
                message.TurfX = (int)cell.x;
                message.TurfY = (int)cell.y;
                message.TurfZ = (int)cell.z;
            }
            else
            {
                message.TargetKind = TargetKind.Entity;
                message.Target = target.dm_id;
            }

            foreach (ScreentipHint hint in hints)
            {
                if (message.Hints.Count >= Limits.MaxContextHints) break;
                ClickModifiers modifiers = (hint.Shift ? ClickModifiers.Shift : 0) | (hint.Ctrl ? ClickModifiers.Ctrl : 0) | (hint.Alt ? ClickModifiers.Alt : 0);
                ClickButton button = hint.Button == RIGHT_CLICK ? ClickButton.Right : hint.Button == LEFT_CLICK ? ClickButton.Left : ClickButton.None;
                if (button == ClickButton.None) continue;
                message.Hints.Add(new ContextHint(button, modifiers, Truncate(PlainLabel(hint.Label), Limits.MaxContextLabelBytes)));
            }

            // D230 (version 36), D238: the radial's Buckle, Unbuckle, Climb in, Climb, Pull and Stop pulling after tg's entries, a label tg gives not repeated.
            foreach ((RadialExtra kind, string label) in RadialExtras.Of(viewer.mob, target))
            {
                if (message.Hints.Count >= Limits.MaxContextHints) break;
                if (message.Hints.Exists(h => string.Equals(h.Label, label, StringComparison.OrdinalIgnoreCase))) continue;
                message.Hints.Add(kind switch
                {
                    RadialExtra.Buckle or RadialExtra.Climb or RadialExtra.Enter => new ContextHint(ClickButton.Left, ClickModifiers.None, label, ContextHintAction.DropSelf),
                    RadialExtra.Pull => new ContextHint(ClickButton.Left, ClickModifiers.Ctrl, label),
                    RadialExtra.StopPulling => new ContextHint(ClickButton.Left, ClickModifiers.None, label, ContextHintAction.StopPulling),
                    RadialExtra.Strip => new ContextHint(ClickButton.Left, ClickModifiers.None, label, ContextHintAction.DropOntoSelf),
                    RadialExtra.Flip or RadialExtra.StepIn => new ContextHint(ClickButton.Left, ClickModifiers.None, label, ContextHintAction.Extra), // version 86 (DV503, DV640)
                    _ => new ContextHint(ClickButton.Left, ClickModifiers.None, label), // Unbuckle: the empty hand's click
                });
            }

            if (message.TargetKind == TargetKind.Entity && message.Target < 1) return; // an object the replica cannot name
            while (s.ControlOutbox.Count >= Session.ControlOutboxCap) s.ControlOutbox.Dequeue();
            s.ControlOutbox.Enqueue(Codec.Encode(message));
        }

        // tg's labels are plain words; a stray tag or entity from a context builder is removed.
        private static string PlainLabel(string label)
        {
            if (string.IsNullOrEmpty(label)) return "";
            var sb = new StringBuilder(label.Length);
            bool tag = false;
            foreach (char ch in label)
            {
                if (ch == '<') tag = true;
                else if (ch == '>' && tag) tag = false;
                else if (!tag && !char.IsControl(ch)) sb.Append(ch);
            }
            return sb.ToString().Trim();
        }
    }

    // After each Step (live and replay alike): the logins that finished. The handle is dropped once done, whatever the
    // session's network state, so a replay holds the same references at the same ticks.
    private void PollLogins()
    {
        foreach (Session s in _sessions)
        {
            ILoginHandle login = s.Login;
            if (login == null && s.State == SessionState.LoggingIn)
            {
                // The Join failed before client/New ran (a runtime in the command, reported by its boundary).
                s.CloseAfterTick = true;
                if (s.CloseReason == DisconnectReason.None)
                {
                    s.CloseReason = DisconnectReason.Refused;
                    s.CloseText = "The world could not take the player in.";
                }
                continue;
            }
            if (login == null || !login.Done) continue;
            s.Login = null;
            if (login.Accepted)
            {
                long mobId = login.Mob.dm_id;
                if (s.OwnEntity == 0) s.OwnEntity = mobId;
                if (s.State == SessionState.LoggingIn)
                {
                    s.State = SessionState.Joined;
                    s.JoinedTick = World.Tick;
                    _joinedThisTick.Add(s);
                }
            }
            else if (s.State == SessionState.LoggingIn)
            {
                s.CloseAfterTick = true;
                if (s.CloseReason == DisconnectReason.None)
                {
                    s.CloseReason = DisconnectReason.Refused;
                    s.CloseText = login.Error != null ? "client/New failed: " + login.Error.Message : "client/New refused the connection.";
                }
            }
        }
    }

    // ---- IClientHost: the /client engine part tells the host (called inside the world, in simulation order) ----

    void IClientHost.ClientDeleted(client player)
    {
        Session s = SessionOfClient(player);
        if (s == null) return;
        if (ReferenceEquals(s, _connecting) && s.Client == null) _connectingClientDeleted = true;
        s.Client = null;
        if (s.State is SessionState.LoggingIn or SessionState.Joined)
            s.CloseAfterTick = true; // the reason is set already for a kick; ClientDeleted otherwise
    }

    void IClientHost.ClientMobChanged(client player, mob old_mob, mob new_mob)
    {
        Session s = SessionOfClient(player);
        if (s == null) return;
        s.OwnEntity = new_mob?.dm_id ?? 0;
        if (new_mob != null) _avatarIds[s.Ckey] = new_mob.dm_id;
        // D151: a player who becomes a ghost or goes back into the body keeps holding the same keys; the next tick's frame is
        // queued again (FormInputs queues only changed frames) so the new mob gets it (ApplyMotion), and the mob left behind
        // stands still (as a disconnect leaves it, tg's SSD).
        s.HaveQueuedFrame = false;
        if (old_mob is mob_living left && _locomotion.IsRegistered(left))
            _locomotion.SetFrame(left, MotionInput.Idle(s.LastFrameSeq, s.LastYaw));
        else if (old_mob is mob_dead_observer leftGhost && _locomotion.IsRegistered(leftGhost))
            _locomotion.Unregister(leftGhost); // back in the body: the ghost is deleted, or left behind without a player
        // DV68: a player who becomes a ghost flies it by the bridge (its capsule starts on the ghost's turf)
        if (new_mob is mob_dead_observer ghost && !_locomotion.IsRegistered(ghost) && World.CurrentOrNull != null)
            _locomotion.RegisterGhost(ghost);
        // M10 slice F: tg's create_character and transfer_character move the key from the lobby's new player into the new crew
        // member: it becomes the player's capsule (D105)
        else if (new_mob is mob_living body && !_locomotion.IsRegistered(body) && World.CurrentOrNull != null)
            _locomotion.Register(body);
    }

    private Session SessionOfClient(client player)
    {
        if (player == null) return null;
        foreach (Session s in _sessions)
        {
            if (ReferenceEquals(s.Client, player) || ReferenceEquals(s.Login?.Client, player)) return s;
        }
        return _connecting; // inside ClientSeam.Connect, before the host knows the new client
    }

    // ---- chat to the players (tg's to_chat through the message sink) ----

    // tg chat for sessions, queued as ServerText and sent after the tick (on Control, once the session is Joined). It only
    // records: a sink never changes the world (presentation-seam.md).
    private sealed class ChatRouter : IMessageSink
    {
        private readonly ServerHost _host;

        public ChatRouter(ServerHost host) => _host = host;

        public void Chat(object target, DmList message)
        {
            if (message == null) return;
            string html = message["html"] as string ?? message["text"] as string;
            if (html == null) return;
            string type = message["type"] as string;
            Route(target, new ServerText(Truncate(type, Limits.MaxServerTextTypeBytes), Truncate(html, Limits.MaxServerTextBytes)), 0);
        }

        public void Output(object target, string text)
        {
            if (text != null) Route(target, new ServerText(null, Truncate(text, Limits.MaxServerTextBytes)), 0);
        }

        // tg's balloon_alert (D128): a line of type "balloon" for the viewer, which the client shows under the crosshair.
        public void Balloon(object viewer, object atom, string text)
        {
            if (!string.IsNullOrEmpty(text))
                Route(viewer, new ServerText("balloon", Truncate(System.Net.WebUtility.HtmlEncode(text), Limits.MaxServerTextBytes)), 0);
        }

        // Version 38 (DV160): tg's runechat image as the finished line; the client measures, stacks and fades it over the speaker.
        public void Runechat(object viewer, object atom, string maptext, float lifespan)
        {
            Session s = viewer is client c ? _host.SessionOfClient(c) : null;
            // shortcut: a line over a turf (a turf speaker) or over what the replica does not show is dropped; no translated speaker is one yet
            if (s == null || string.IsNullOrEmpty(maptext) || Encoding.UTF8.GetByteCount(maptext) > Limits.MaxRunechatBytes
                || !ReplicatedFields.IsEntity(atom, out atom_movable on, out _))
                return;
            int life = (int)Math.Clamp(MathF.Round(lifespan), 1, Limits.MaxRunechatLifespan);
            s.SpeechOutbox.Enqueue(Codec.Encode(new RunechatMessage(on.dm_id, maptext, life)));
        }

        // Version 63 (D307, DV262): a line the listener's client voices itself, from an entity of the replica or, with no atom, to the
        // listener only (the preferences' voice preview). Version 64 (D308): the filter chain, the special filters and the radio as flags; a
        // radio in a pocket or an ear speaks from what holds it (the first holder the replica shows).
        public void Tts(object listener, object atom, string text, string voice, float pitch, float volume, string filter, string special_filters, float radio_frequency, bool own)
        {
            Session s = listener is client c ? _host.SessionOfClient(c) : null;
            if (s == null || string.IsNullOrWhiteSpace(text) || string.IsNullOrEmpty(voice) || Encoding.UTF8.GetByteCount(text) > Limits.MaxTtsTextBytes
                || Encoding.UTF8.GetByteCount(voice) > Limits.MaxTtsVoiceBytes)
                return;
            long speaker = 0;
            if (atom != null)
            {
                atom_movable on = null;
                for (object at = atom; at is atom_movable && !ReplicatedFields.IsEntity(at, out on, out _); at = ((atom_movable)at).loc)
                    on = null;
                if (on == null)
                    return; // shortcut: a turf speaker is not voiced, as its runechat is dropped
                speaker = on.dm_id;
            }
            if (string.IsNullOrEmpty(filter) || Encoding.UTF8.GetByteCount(filter) > Limits.MaxTtsFilterBytes)
                filter = null;
            TtsFlags flags = TtsFlags.None;
            if (radio_frequency > 0)
                flags |= TtsFlags.Radio | (radio_frequency == Defines.FREQ_COMMON ? TtsFlags.Common : TtsFlags.None);
            if (own)
                flags |= TtsFlags.Own;
            string[] specials = (special_filters ?? "").Split('|');
            if (specials.Contains(Defines.TTS_FILTER_SILICON))
                flags |= TtsFlags.Silicon;
            if (specials.Contains(Defines.TTS_FILTER_RADIO))
                flags |= TtsFlags.Clicks;
            if (specials.Contains(datum_controller_subsystem_tts.TTS_FILTER_WHISPER))
                flags |= TtsFlags.Whisper; // version 65 (D312)
            if (specials.Contains(datum_controller_subsystem_tts.TTS_FILTER_YELL))
                flags |= TtsFlags.Yell;
            int semitones = (int)Math.Clamp(MathF.Round(pitch), -Limits.MaxTtsPitch, Limits.MaxTtsPitch);
            int loudness = (int)Math.Clamp(MathF.Round(volume), 0, Limits.MaxTtsVolume);
            s.SpeechOutbox.Enqueue(Codec.Encode(new TtsMessage(speaker, text, voice, semitones, loudness, filter, flags)));
        }

        // Version 38: tg's window.send_message from the say window's datum to the client's say window.
        public void SayModal(object player, string type, int maxLength, bool lightMode)
        {
            Session s = player is client c ? _host.SessionOfClient(c) : null;
            if (s == null) return;
            SayModalMessage message = type switch
            {
                "props" => new SayModalMessage(SayModalCommand.Props, Math.Clamp(maxLength, 1, Limits.MaxSayEntryChars), lightMode),
                "force" => new SayModalMessage(SayModalCommand.Force, 0, false),
                "save" => new SayModalMessage(SayModalCommand.Save, 0, false),
                "close" => new SayModalMessage(SayModalCommand.Close, 0, false),
                _ => default,
            };
            if (message.Command != SayModalCommand.None)
                s.SpeechOutbox.Enqueue(Codec.Encode(message));
        }

        private void Route(object target, ServerText text, int depth)
        {
            switch (target)
            {
                case null:
                    return;
                case client c:
                    Send(_host.SessionOfClient(c), text);
                    return;
                case mob m when m.client != null:
                    Send(_host.SessionOfClient(m.client), text);
                    return;
                case DmList list when depth < 4:
                    foreach (object item in Dm.each(list))
                        Route(item, text, depth + 1);
                    return;
                case DmWorld:
                    foreach (Session s in _host._sessions)
                    {
                        if (s.Client != null) Send(s, text);
                    }
                    return;
            }
        }

        // Version 24 (D154): the session keeps the hrefs of the chat links it is sent, the only ones it may click back.
        private static void Send(Session s, ServerText text)
        {
            if (s == null) return;
            s.Outbox.Enqueue(text);
            s.ChatLinks.Record(text.Html);
        }
    }
}
