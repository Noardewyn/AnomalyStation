using System.Globalization;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Protocol;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server;

// The server console (D73's core subset, scope 2h). A reader thread only hands lines over (EnqueueConsoleLine); the host
// parses them between ticks. Commands that act in the world (spawn, teleport, list, kick, quit) are typed commands of an
// always-authorized source, session 0: queued for the next tick, applied in the command phase in order, recorded in the
// replay log. Their bodies call tg's own primitives, as tg's admin verbs do. status, hash and help only read, on the host
// thread between ticks, and are not recorded.
public sealed partial class ServerHost
{
    /// <summary>The name the console's actions carry in tg's logs (an always-authorized source, D73).</summary>
    public const string ConsoleName = "the server console";

    private void DrainConsole()
    {
        while (_consoleLines.TryDequeue(out string line))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            _out.WriteLine("console> " + line);
            if (!ConsoleCommand.TryParse(line, out ConsoleCommand command, out string error))
            {
                _out.WriteLine("console: " + error);
                continue;
            }
            switch (command.Verb)
            {
                case ConsoleVerb.Help:
                    _out.WriteLine(ConsoleCommand.Help);
                    break;
                case ConsoleVerb.DevHelp:
                    _out.WriteLine(DevCommandLine.Help);
                    break;
                case ConsoleVerb.Status:
                    _out.WriteLine(StatusText());
                    break;
                case ConsoleVerb.Hash:
                    _out.WriteLine("hash " + StatusLine(World, World.ComputeStateHash()));
                    break;
                case ConsoleVerb.Quit:
                    RequestStop();
                    break;
                default:
                    if (_config.Empty && command.Verb != ConsoleVerb.List)
                    {
                        _out.WriteLine("console: " + command.Verb.ToString().ToLowerInvariant() + " needs the tg world (the server runs --empty)");
                        break;
                    }
                    if (_replay)
                    {
                        _out.WriteLine("console: a replay takes no world commands");
                        break;
                    }
                    if (_quitQueued)
                    {
                        _out.WriteLine("console: the server is shutting down");
                        break;
                    }
                    _queue.Enqueue(new QueuedCommand(World.Tick + 1, 0, ++_consoleSeq, CommandKind.Console, line));
                    break;
            }
        }
    }

    // ---- 1.6b: the console's commands typed by a player (CLIENT_CONSOLE) ----

    // The player whose console command is being applied (null: the server console), for Say and ConsoleActor.
    private Session _consoleRequester;

    // Who the console's logs and messages name: the server console, or the player who typed the command (tg's key_name).
    private string ConsoleActor => _consoleRequester == null ? ConsoleName
        : MobOf(_consoleRequester) is mob m ? Dm.str(key_name(m)) : _consoleRequester.Key;

    // A line of a console command's answer: the server's output always (the log of what was done), and the typing player.
    private void Say(string line)
    {
        _out.WriteLine(line);
        if (_consoleRequester != null) ReplyTo(_consoleRequester, line);
    }

    private static bool IsConsoleOnlyVerb(string word) => word.ToLowerInvariant()
        is "spawn" or "teleport" or "tp" or "list" or "who" or "kick" or "status" or "hash" or "quit" or "exit" or "shutdown" or "dev-help"
        or "ban" or "ban-ip" or "unban" or "bans" or "note" or "watch" or "notes" or "note-delete" or "tickets" or "pm";

    // The network side of a player's console command: false when the line is no console command (the caller answers with the
    // dev command's reason); otherwise answered here, and queue says whether it is a world command to queue as the player's
    // DevCommand action. status, hash and the help texts only read, between ticks, as the server console's do.
    // M10 slice I: an admin's command needs the right of tg's matching verb (RightsFor) unless CLIENT_CONSOLE opens them to everyone.
    private bool TakeClientConsoleLine(Session s, string text, out bool queue)
    {
        queue = false;
        string[] words = (text ?? "").Split((char[])null, 2, StringSplitOptions.RemoveEmptyEntries);
        // the owner (2026-10-26): on a server with the dev commands on, a player may teleport (no more than noclip gives)
        bool devTeleport = _config.DevCommandsEnabled && words.Length > 0 && words[0].ToLowerInvariant() is "teleport" or "tp";
        bool admin = !_config.ClientConsole && !devTeleport && IsAdmin(s);
        if (!_config.ClientConsole && !admin && !devTeleport) return false;
        if (!ConsoleCommand.TryParse(text, out ConsoleCommand command, out string error))
        {
            if (words.Length == 0 || !IsConsoleOnlyVerb(words[0])) return false;
            ReplyTo(s, error);
            return true;
        }
        if (admin && command.Verb != ConsoleVerb.Dev && !CheckRights(s, RightsFor(command.Verb, text)))
            return true;
        switch (command.Verb)
        {
            case ConsoleVerb.Dev:
                return false; // a dev verb while DEV_COMMANDS is off
            case ConsoleVerb.Help:
                ReplyLines(s, ConsoleCommand.Help);
                return true;
            case ConsoleVerb.DevHelp:
                ReplyLines(s, DevCommandLine.Help);
                return true;
            case ConsoleVerb.Status:
                ReplyLines(s, StatusText());
                return true;
            case ConsoleVerb.Hash:
                ReplyTo(s, "hash " + StatusLine(World, World.ComputeStateHash()));
                return true;
            case ConsoleVerb.Quit:
                if (_replay || _quitQueued) { ReplyTo(s, "the server is already shutting down"); return true; }
                _out.WriteLine($"console ({s.Key})> {text}");
                QueueQuit(World.Tick + 1);
                ReplyTo(s, "the server is shutting down");
                return true;
            default: // spawn, teleport, list, kick: world commands
                if (_config.Empty && command.Verb != ConsoleVerb.List)
                {
                    ReplyTo(s, command.Verb.ToString().ToLowerInvariant() + " needs the tg world (the server runs --empty)");
                    return true;
                }
                queue = true;
                return true;
        }
    }

    // The world side: a player's spawn, teleport, list or kick, applied as the console applies it, answered to the player.
    private void ApplyConsoleFrom(Session s, in ConsoleCommand command)
    {
        _out.WriteLine($"console ({s.Key})> {command.Line}");
        _consoleRequester = s;
        try
        {
            switch (command.Verb)
            {
                case ConsoleVerb.List: ApplyList(); break;
                case ConsoleVerb.Spawn: ApplySpawn(command); break;
                case ConsoleVerb.Teleport: ApplyTeleport(command); break;
                case ConsoleVerb.Kick: ApplyKick(command); break;
                default: ApplyAdminConsole(command); break;
            }
        }
        catch (Exception e)
        {
            World.ReportRuntime(e); // a failing proc is a runtime of the command; the player hears of it
            Say("console command failed: " + e.GetType().Name + ": " + e.Message);
        }
        finally
        {
            _consoleRequester = null;
        }
    }

    // The shutdown is a console command too, so the replay applies it (kick everyone, world/Del) at the same tick.
    private void QueueQuit(long applyTick)
    {
        _quitQueued = true;
        _queue.Enqueue(new QueuedCommand(applyTick, 0, ++_consoleSeq, CommandKind.Console, "quit"));
    }

    private string StatusText()
    {
        int live = LiveSessionCount();
        return string.Create(CultureInfo.InvariantCulture,
            $"status tick={World.Tick} time={World.Time:0.0#} joins={(JoinsOpen ? "open" : "closed")} sessions={live} served={_sessionsServed} "
            + $"queued={_queue.Count} runtimes={World.RuntimeErrorCount} rejected={_rejectedRequests}")
            + (Metrics.LastWindow != null ? "\n" + Metrics.LastWindow : "");
    }

    private void ApplyConsole(World world, string line)
    {
        if (!ConsoleCommand.TryParse(line, out ConsoleCommand command, out string error))
        {
            _out.WriteLine("console: " + error);
            return;
        }
        switch (command.Verb)
        {
            case ConsoleVerb.Quit:
                ApplyQuit(world);
                break;
            case ConsoleVerb.List:
                ApplyList();
                break;
            case ConsoleVerb.Spawn:
                ApplySpawn(command);
                break;
            case ConsoleVerb.Teleport:
                ApplyTeleport(command);
                break;
            case ConsoleVerb.Kick:
                ApplyKick(command);
                break;
            case ConsoleVerb.Dev:
                ApplyDevConsole(command);
                break;
            default:
                ApplyAdminConsole(command);
                break;
        }
    }

    // M10 slice I: the bans and notes (ServerHost.Admin.cs).
    private void ApplyAdminConsole(in ConsoleCommand command)
    {
        switch (command.Verb)
        {
            case ConsoleVerb.Ban: ApplyBan(command, ipOnly: false); break;
            case ConsoleVerb.BanIp: ApplyBan(command, ipOnly: true); break;
            case ConsoleVerb.Unban: ApplyUnban(command); break;
            case ConsoleVerb.Bans: ApplyBans(command); break;
            case ConsoleVerb.Note: ApplyNote(command, "note"); break;
            case ConsoleVerb.Watch: ApplyNote(command, "watchlist entry"); break;
            case ConsoleVerb.Notes: ApplyNotes(command); break;
            case ConsoleVerb.NoteDelete: ApplyNoteDelete(command); break;
            case ConsoleVerb.Tickets or ConsoleVerb.Pm: ApplyTicketConsole(command); break;
            case ConsoleVerb.CheckAntagonists or ConsoleVerb.TraitorPanel or ConsoleVerb.CheckTeams or ConsoleVerb.ComboHud
                or ConsoleVerb.DynamicPanel or ConsoleVerb.DynamicTester or ConsoleVerb.Secrets or ConsoleVerb.StationTraits
                or ConsoleVerb.SkillPanel or ConsoleVerb.Circuits or ConsoleVerb.LoadCircuit or ConsoleVerb.Adventures: ApplyAntagonistConsole(command); break;
        }
    }

    // M10x slice G6: tg's ticket list and Admin PM, as the admin who typed them (usr: their mob); they open windows, so the server
    // console has none
    private void ApplyTicketConsole(in ConsoleCommand command)
    {
        client admin = _consoleRequester?.Client;
        if (admin == null)
        {
            Say(command.Verb.ToString().ToLowerInvariant() + ": an admin's command in the game (it opens a window)");
            return;
        }
        World world = World;
        object callerUsr = world.usr;
        try
        {
            world.usr = admin.mob;
            if (command.Verb == ConsoleVerb.Tickets)
            {
                string state = command.Args.Count == 0 ? "active" : command.Args[0].ToLowerInvariant();
                GlobalProcs.GLOB.ahelp_tickets.BrowseTickets(state == "closed" ? Defines.AHELP_CLOSED : state == "resolved" ? Defines.AHELP_RESOLVED : Defines.AHELP_ACTIVE);
            }
            else
            {
                string message = command.Args.Count > 1 ? command.Rest(1) : null;
                Waiting.Detach(admin.cmd_admin_pm(Dm.ckey(command.Args[0]), message));
            }
        }
        finally
        {
            world.usr = callerUsr;
        }
    }

    // Syndicate A1 (D316): tg's antagonist admin verbs, as the admin who typed them (usr: their mob): Check Antagonists
    // (admin_verbs.dm:121), Show Traitor Panel (admingame.dm:450), the team listing (team_panel.dm:25) and Toggle Combo HUD
    // (admingame.dm:404); syndicate A3: Dynamic Panel and Dynamic Tester; they open windows or change the admin's own HUD, so the
    // server console has none
    private void ApplyAntagonistConsole(in ConsoleCommand command)
    {
        client admin = _consoleRequester?.Client;
        if (admin == null)
        {
            Say(command.Verb.ToString().ToLowerInvariant() + ": an admin's command in the game (it opens a window)");
            return;
        }
        World world = World;
        object callerUsr = world.usr;
        try
        {
            world.usr = admin.mob;
            switch (command.Verb)
            {
                case ConsoleVerb.CheckAntagonists:
                    Waiting.Detach(admin.holder.check_antagonists());
                    break;
                case ConsoleVerb.CheckTeams:
                    Waiting.Detach(admin.holder.check_teams());
                    break;
                case ConsoleVerb.ComboHud:
                    admin.toggle_combo_hud();
                    break;
                case ConsoleVerb.DynamicPanel:
                    // ADMIN_VERB(dynamic_panel, R_ADMIN, ...) (dynamic_admin.dm:1): dynamic_panel(user.mob)
                    Waiting.Detach(dynamic_panel(admin.mob));
                    break;
                case ConsoleVerb.Secrets:
                    // ADMIN_VERB(secrets, R_NONE, ...) (secrets.dm:3)
                    Waiting.Detach(secrets(admin));
                    break;
                case ConsoleVerb.StationTraits:
                    // ADMIN_VERB(station_traits_panel, R_FUN, ...) (station_traits/admin_panel.dm:1)
                    Waiting.Detach(station_traits_panel(admin));
                    break;
                case ConsoleVerb.DynamicTester:
                    // ADMIN_VERB(dynamic_tester, R_DEBUG, ...) (dynamic_testing.dm:2)
                    Waiting.Detach(open_dynamic_tester(admin.mob));
                    break;
                case ConsoleVerb.TraitorPanel:
                {
                    // ADMIN_VERB(show_traitor_panel, ...) (admingame.dm:450), its target typed by the admin
                    mob target_mob = FindMovable(command.Args[0], out _) as mob;
                    if (target_mob == null)
                    {
                        Say($"traitor-panel: no mob '{command.Args[0]}'");
                        return;
                    }
                    datum_mind target_mind = target_mob.mind;
                    if (target_mind == null)
                    {
                        to_chat(admin, "This mob has no mind!", confidential: Dm.TRUE);
                        return;
                    }
                    Waiting.Detach(target_mind.traitor_panel());
                    break;
                }
                case ConsoleVerb.SkillPanel:
                {
                    // ADMIN_VERB(show_skill_panel, R_ADMIN, ...) (admingame.dm:462), its target typed by the admin (gaps SL1)
                    mob target_mob = FindMovable(command.Args[0], out _) as mob;
                    if (target_mob == null)
                    {
                        Say($"skill-panel: no mob '{command.Args[0]}'");
                        return;
                    }
                    Waiting.Detach(admin.show_skill_panel(target_mob));
                    break;
                }
                case ConsoleVerb.Circuits:
                    // ADMIN_VERB(view_all_circuits, R_ADMIN, ...) (wiremod/core/admin_panel.dm:2, gaps CI1)
                    Waiting.Detach(admin.view_all_circuits());
                    break;
                case ConsoleVerb.Adventures:
                    // ADMIN_VERB(adventure_manager, R_DEBUG, ...) (explorer_drone/manager.dm:86)
                    Waiting.Detach(admin.adventure_manager());
                    break;
                case ConsoleVerb.LoadCircuit:
                {
                    // ADMIN_VERB(load_circuit, R_VAREDIT, ...) (wiremod/core/duplicator.dm:223, gaps CI1): the line after the verb is the
                    // direct input tg's input box takes
                    string line = command.Line.TrimStart();
                    string json = line.Substring(line.IndexOfAny(new[] { ' ', '\t' }) + 1).Trim();
                    Waiting.Detach(admin.load_circuit(json));
                    break;
                }
            }
        }
        finally
        {
            world.usr = callerUsr;
        }
    }

    // A clean shutdown, in the world: every client's connection ends (tg's client/Del through the seam, Logout; the
    // network side tells each player "The server is shutting down" after the tick), then tg's world/Del.
    private void ApplyQuit(World world)
    {
        foreach (Session s in _sessions)
        {
            if (s.Client == null && s.Login == null) continue;
            if (s.CloseReason == DisconnectReason.None)
            {
                s.CloseReason = DisconnectReason.ServerShutdown;
                s.CloseText = "The server is shutting down.";
            }
            ApplyLeave(world, s);
        }
        _shutdownApplied = true;
        if (!_config.Empty)
            Dm.world.Del();
        _out.WriteLine(string.Create(CultureInfo.InvariantCulture, $"shutdown at tick {world.Tick}"));
    }

    // tg's who verb, for the console: every session with its world state and tg's key_name of its mob.
    private void ApplyList()
    {
        int shown = 0;
        foreach (Session s in _sessions)
        {
            if (s.State == SessionState.Closed) continue;
            mob m = MobOf(s);
            string name = m != null ? Dm.str(key_name(m)) : "-";
            Say(string.Create(CultureInfo.InvariantCulture,
                $"  #{s.Ordinal} {s.Key} {s.State} mob #{s.OwnEntity} {name}{(m != null ? $" at ({Dm.str(m.x)},{Dm.str(m.y)},{Dm.str(m.z)})" : "")}"));
            shown++;
        }
        Say(string.Create(CultureInfo.InvariantCulture, $"list: {shown} session(s)"));
    }

    // tg's spawn_atom core (code/modules/admin/admin.dm:66-74): new chosen_path(target_turf), ADMIN_SPAWNED_1, log_admin.
    private void ApplySpawn(in ConsoleCommand command)
    {
        Type type = Dm.text2path(command.Args[0]);
        if (type == null || !typeof(atom_movable).IsAssignableFrom(type))
        {
            Say($"spawn: '{command.Args[0]}' is not an /obj or /mob type (turfs change with ChangeTurf, M3)");
            return;
        }
        turf target_turf;
        if (command.Args.Count > 1)
        {
            var (x, y, z) = command.CoordinatesAt(1);
            target_turf = locate(x, y, z);
        }
        else if (_consoleRequester != null && MobOf(_consoleRequester) is mob requester)
        {
            target_turf = get_turf(requester) as turf; // 1.6b: a player's spawn without coordinates lands at the player's feet
        }
        else
        {
            SpawnPoint p = SpawnPoints[0];
            target_turf = locate(p.X, p.Y, p.Z);
        }
        if (target_turf == null)
        {
            Say("spawn: invalid coordinates");
            return;
        }
        var spawned = (atom)Dm.@new(type, target_turf);
        Dm.combine(ref spawned.flags_1, ADMIN_SPAWNED_1);
        // M2 (F1): stands in for the job system (M9/M10): a spawned human is named and dressed as a joining crew member
        // (CrewStandIn: tg's random name and the assistant outfit's random-colour jumpsuit), as the dev command's spawn does.
        if (spawned is mob_living_carbon_human crew)
            CrewStandIn.Join(crew);
        // A living mob is a capsule like a player's (a dummy stands still and stays solid, D105).
        if (spawned is mob_living living && !_locomotion.IsRegistered(living))
            _locomotion.Register(living);
        log_admin(Dm.text("[] spawned 1 x [] at ([],[],[])", ConsoleActor, DmTypes.PathOf(type), target_turf.x, target_turf.y, target_turf.z));
        Say(string.Create(CultureInfo.InvariantCulture,
            $"spawned {DmTypes.PathOf(type)} #{spawned.dm_id} at ({Dm.str(target_turf.x)},{Dm.str(target_turf.y)},{Dm.str(target_turf.z)})"));
    }

    // tg's Jump To Coordinate (code/modules/admin/verbs/adminjump.dm:35-46) applied to a target: abstract_move(locate(x, y, z)).
    private void ApplyTeleport(in ConsoleCommand command)
    {
        atom_movable target = FindMovable(command.Args[0], out string who);
        if (target == null)
        {
            Say($"teleport: no player or movable '{command.Args[0]}'");
            return;
        }
        if (command.Args.Count == 2)
        {
            ApplyTeleportOnto(target, who, command.Args[1]);
            return;
        }
        var (x, y, z) = command.CoordinatesAt(1);
        turf where_we_droppin = locate(x, y, z);
        if (where_we_droppin == null)
        {
            Say("teleport: Invalid coordinates.");
            return;
        }
        // A verb is its own thread in BYOND: an abstract_move that sleeps finishes later, the console goes on.
        Waiting.Detach(target.abstract_move(where_we_droppin));
        log_admin(Dm.text("[] teleported [] to [], [], []", ConsoleActor, key_name(target), x, y, z));
        Say(string.Create(CultureInfo.InvariantCulture, $"teleported {who} to ({x},{y},{z})"));
    }

    // tg's Jump To Mob and Get Mob (code/modules/admin/verbs/adminjump.dm:28-30, :80-119): the target is moved to the other one's turf.
    private void ApplyTeleportOnto(atom_movable target, string who, string destination)
    {
        atom_movable other = FindMovable(destination, out string otherWho);
        if (other == null)
        {
            Say($"teleport: no player or movable '{destination}'");
            return;
        }
        if (get_turf(other) is not turf where_we_droppin)
        {
            Say($"teleport: {otherWho} is nowhere");
            return;
        }
        Waiting.Detach(target.abstract_move(where_we_droppin));
        log_admin(Dm.text("[] teleported [] to []", ConsoleActor, key_name(target), key_name(other)));
        Say($"teleported {who} to {otherWho}");
    }

    // tg's boot2 (code/modules/admin/topic.dm:216-234): to_chat the player, log_admin, qdel(M.client). Deleting the client
    // disconnects the player (BYOND; the /client engine part tells the host through IClientHost.ClientDeleted).
    private void ApplyKick(in ConsoleCommand command)
    {
        mob M = FindMovable(command.Args[0], out string who) as mob;
        if (M == null)
        {
            Say($"kick: no player '{command.Args[0]}'");
            return;
        }
        if (M.client == null)
        {
            Say($"kick: Error: {who} no longer has a client!");
            return;
        }
        Session s = SessionOfClient(M.client);
        string reason = command.KickReason;
        if (s != null && s.CloseReason == DisconnectReason.None)
        {
            s.CloseReason = DisconnectReason.Kicked;
            s.CloseText = reason != null ? "Kicked by " + ConsoleActor + ": " + reason : "Kicked by " + ConsoleActor + ".";
        }
        to_chat(M, span_danger(Dm.text("You have been kicked from the server by []." + (reason != null ? " Reason: []" : ""), ConsoleActor, reason)), confidential: Dm.TRUE);
        log_admin(Dm.text("[] kicked [].", ConsoleActor, key_name(M)));
        qdel(M.client);
        Say($"kicked {who}");
    }

    // A player's mob by key (ckey match, the latest live session first) or any movable by its world id.
    private atom_movable FindMovable(string nameOrId, out string who)
    {
        who = nameOrId;
        if (_consoleRequester != null && string.Equals(nameOrId, "me", StringComparison.OrdinalIgnoreCase) && MobOf(_consoleRequester) is mob self)
        {
            who = _consoleRequester.Key; // 1.6b: "me" is the typing player's mob
            return self;
        }
        string ckey = Dm.ckey(nameOrId);
        for (int i = _sessions.Count - 1; i >= 0 && !string.IsNullOrEmpty(ckey); i--)
        {
            Session s = _sessions[i];
            if (s.Ckey != ckey) continue;
            mob m = MobOf(s);
            if (m != null)
            {
                who = s.Key;
                return m;
            }
        }
        if (_avatarIds.TryGetValue(ckey ?? "", out long avatarId) && World.Objects.TryGet(avatarId, out object avatar) && avatar is mob am)
        {
            who = nameOrId;
            return am;
        }
        if (long.TryParse(nameOrId.TrimStart('#'), NumberStyles.None, CultureInfo.InvariantCulture, out long id)
            && World.Objects.TryGet(id, out object o) && o is atom_movable movable && !QDELETED(movable))
        {
            who = "#" + id.ToString(CultureInfo.InvariantCulture);
            return movable;
        }
        return null;
    }
}
