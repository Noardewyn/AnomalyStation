using System.Linq;
using Anomaly.Core.Motion;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared;

// Owner: M2 P8 (client play). Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).

/// <summary>
/// The client's play loop without an engine (M2 scope 2d, 2f, 1.6): everything the game scene does with the feed each frame
/// that is not drawing. It polls the feed, moves the render clock (which stops at the data), predicts and sends the own
/// capsule's frames, hands the frame's render time to the pose source, collects the per-frame speed statistics of what is
/// drawn, keeps the camera layer, and turns the player's actions into the messages the server takes (rest, walk/run,
/// combat mode, a click on a mob, a dev command), each tagged with the frame it belongs to. The Godot scene feeds it the
/// keys and the view and draws; a headless test feeds it a fake clock and keys and checks what comes out.
/// </summary>
public sealed class PlaySession
{
    private readonly IWorldFeed _feed;
    private readonly ReplicaWorld _replica;
    private readonly List<PickCandidate> _candidates = new();
    private long _lastTick = -1;
    private double _lastNow = double.NaN;
    private bool _wasLying;
    private (float X, float Y, int Z)? _lastOwn;
    private float _ownSpeed;

    /// <param name="feed">The world.</param>
    /// <param name="options">The player's options.</param>
    /// <param name="inputBufferTarget">The frames the server should keep queued for this player after its tick: 1 on localhost, 2 on a real
    /// network (scope 2d), so that one lost Input message never starves the server's buffer (a starved tick repeats the last frame, which
    /// the prediction cannot know).</param>
    public PlaySession(IWorldFeed feed, PlayOptions options, int inputBufferTarget = 1)
    {
        _feed = feed ?? throw new ArgumentNullException(nameof(feed));
        Options = options ?? new PlayOptions();
        _replica = feed.Replica;
        Prediction = new OwnPrediction(new OwnExcludingView(_replica.CollisionView, () => _replica.OwnEntity), targetDepth: inputBufferTarget, bodyId: () => _replica.OwnEntity);
        Poses = new PoseSource(_replica, Prediction);
        Camera = new CameraLayer { HeadBob = Options.HeadBob, Shake = Options.CameraShake };
        _replica.OwnStateReceived += Prediction.OnOwnState;
        Guesses = new OwnGuesses(_replica, OwnTile);
        _replica.OwnStateReceived += Guesses.OnOwnState; // after the reconciliation, which still steps with the guessed doors
        Prediction.Pressed += Guesses.OnPressed;
        Prediction.Pushes = Guesses.OnPush;
        Prediction.WouldPush = Guesses.WouldPush;
        // version 21: tg's shake_camera path moves the eye (D70: the camera shake switch); version 34 (DV96): tg's recoil kick turns the view
        _replica.CameraShakeReceived += shake =>
        {
            if (shake.Kind == CameraShakeKind.Recoil)
                Camera.Recoil(shake.Steps.Select(st => (st.PixelX, st.PixelY, st.Time)).ToList(), Prediction.Yaw / 65536.0 * 360.0);
            else
                Camera.FollowShakePath(shake.Steps.Select(st => (st.PixelX, st.PixelY, st.Time)).ToList());
        };
        feed.Despawned += OnDespawned;
        Prediction.Corrected += OnCorrected;
        // The feed may already hold a private block (a join that finished before the session existed).
        if (_replica.Own.Tick >= 0)
        {
            OwnState own = _replica.Own;
            Prediction.OnOwnState(own);
        }
    }

    public PlayOptions Options { get; }

    public OwnPrediction Prediction { get; }

    /// <summary>M10 slice E: the guesses of the own hands and doors (D38 stage 1).</summary>
    public OwnGuesses Guesses { get; }

    // The own tile: the predicted capsule's, else the replica's own entity.
    private (int X, int Y, int Z) OwnTile()
    {
        if (Predicting)
        {
            MotionState m = Prediction.Predicted;
            return (MotionMath.TileOf(m.X), MotionMath.TileOf(m.Y), m.Z);
        }
        return _replica.TryGet(_replica.OwnEntity, out ReplicaEntity own) ? (own.State.X, own.State.Y, own.State.Z) : (0, 0, 0);
    }

    public PoseSource Poses { get; }

    public CameraLayer Camera { get; }

    public RenderClock Clock { get; } = new();

    public LatencyProbe Probe { get; } = new();

    /// <summary>The per-frame speed of the other mobs as drawn (the run summary's p99_speed and peak_speed).</summary>
    public FrameSpeedStats OthersSpeed { get; } = new();

    /// <summary>The per-frame speed of the own capsule as drawn (prediction and its corrections).</summary>
    public FrameSpeedStats OwnSpeed { get; } = new();

    /// <summary>The feed's replica.</summary>
    public ReplicaWorld Replica => _replica;

    /// <summary>Distinct other mobs seen over the run (the ones that left included), and how many of them were seen to move more than a quarter of a tile.</summary>
    public int OthersSeen => _seen.Count;

    public int OthersMoved => _moved.Count;

    private readonly Dictionary<long, (float X, float Y)> _seen = new();
    private readonly HashSet<long> _moved = new();

    /// <summary>The render time of the frame, in server ticks.</summary>
    public double RenderTick => Clock.Started ? Clock.RenderTick : _lastTick;

    /// <summary>True while the feed is playing and the own capsule is predicted.</summary>
    public bool Predicting => _feed.Phase == FeedPhase.Playing && Prediction.Active && !Prediction.TileDriven;

    /// <summary>
    /// D151: the own mob is a ghost (tg's observer). DV68: it flies as a capsule through walls, predicted like the living body; the
    /// view is the third-person orbit by default (the first person on the view key), and it clicks nothing (the minimal scope).
    /// </summary>
    public bool Ghosting => _feed.Phase == FeedPhase.Playing && Prediction.Active
        && _replica.TryGet(_feed.OwnEntity, out ReplicaEntity own) && own.TypePath != null && own.TypePath.StartsWith(GhostType, StringComparison.Ordinal);

    /// <summary>tg's observer, the ghost (D151).</summary>
    public const string GhostType = "/mob/dead/observer";

    /// <summary>How many shakes the camera layer was given (the prediction corrected a shove).</summary>
    public int CorrectionShakes { get; private set; }

    /// <summary>
    /// One frame. <paramref name="controlling"/> says the keys drive the avatar (not in the free camera, not while a console
    /// takes the keyboard); <paramref name="godotYaw"/> is the view's yaw, the avatar's facing.
    /// </summary>
    public void Frame(double now, MoveKeys keys, double godotYaw, bool controlling)
    {
        double dt = double.IsNaN(_lastNow) ? 0 : Math.Max(0, now - _lastNow);
        _lastNow = now;
        _feed.Poll(now);
        if (_feed.Phase != FeedPhase.Playing)
            return;
        Hotbar.Apply(_feed.Hotbar, _feed.HotbarRefusals, _feed.HotbarRefusedCell, now); // version 98 also (DV603)
        long newest = Math.Max(_feed.Tick, _replica.MotionTick); // version 42: a Motion runs ahead of a Delta the reliable channel holds back
        if (newest != _lastTick)
        {
            _lastTick = newest;
            Clock.OnServerTick(_lastTick, now);
            Probe.OnAck(_feed.LastAppliedSeq, now);
        }
        Clock.Update(now, _lastTick);

        MoveKeys driving = controlling ? keys : MoveKeys.None;
        if (Prediction.Active)
        {
            Prediction.Frame(dt, driving, godotYaw, _feed.SendMotion);
            // The key press that starts a walk is timed to the first drawn motion (M1's probe, now without the round trip).
            HeldDirs mask = driving == MoveKeys.None ? HeldDirs.None : HeldDirs.North;
            if (mask != _lastMask && !Prediction.TileDriven)
            {
                _lastMask = mask;
                (float px, float py, _) = Prediction.RenderPosition();
                Probe.OnInputSent(Prediction.NewestSeq + 1, mask, now, px, py);
            }
        }
        Poses.BeginFrame(RenderTick, dt);
        CollectStats(now);
    }

    private HeldDirs _lastMask;

    private void CollectStats(double now)
    {
        long own = _feed.OwnEntity;
        foreach (ReplicaEntity entity in _replica.Entities)
        {
            if (!entity.IsMob() || !Poses.TryGetPose(entity, out RenderPose pose))
                continue;
            if (entity.Id == own)
            {
                OwnSpeed.Observe(entity.Id, pose.X, pose.Y, pose.Z, now);
                Probe.OnOwnPose(pose.X, pose.Y, now);
                continue;
            }
            OthersSpeed.Observe(entity.Id, pose.X, pose.Y, pose.Z, now);
            if (!_seen.TryGetValue(entity.Id, out var start))
                _seen[entity.Id] = (pose.X, pose.Y);
            else if (Math.Abs(pose.X - start.X) > 0.25f || Math.Abs(pose.Y - start.Y) > 0.25f)
                _moved.Add(entity.Id);
        }
    }

    /// <summary>
    /// The camera layer for this frame (call after <see cref="Frame"/>): the eye height by the own posture, the bob by the
    /// capsule's speed over the ground, a shake after a correction. Metres, camera-relative.
    /// </summary>
    public CameraOffsets CameraFrame(double dtSeconds)
    {
        Camera.HeadBob = Options.HeadBob;
        Camera.Shake = Options.CameraShake;
        Camera.FloatBob = Options.ZeroGBob;
        Camera.Roll = Options.ZeroGRoll;
        Camera.InExosuit = _replica.TryGet(_replica.OwnEntity, out ReplicaEntity body) && body.IsExosuit();
        if (!Prediction.Active || Prediction.TileDriven)
            return Camera.Update(dtSeconds, false, 0f, false);
        (float x, float y, int z) = Prediction.RenderPosition();
        if (_lastOwn.HasValue && dtSeconds > 0.0005 && _lastOwn.Value.Z == z)
        {
            float ex = x - _lastOwn.Value.X, ny = y - _lastOwn.Value.Y;
            float tiles = MathF.Sqrt(ex * ex + ny * ny);
            float speed = tiles * TileSpace.TileSize / (float)dtSeconds;
            float k = (float)Math.Min(1.0, dtSeconds / 0.08);
            _ownSpeed += (Math.Min(speed, 30f) - _ownSpeed) * k;
            // The glide to the view's right (the compass yaw's right is (cos, -sin) in east and north).
            float bearing = Prediction.Yaw / 65536f * 2f * MathF.PI;
            float sideways = (ex * MathF.Cos(bearing) - ny * MathF.Sin(bearing)) * TileSpace.TileSize / (float)dtSeconds;
            _ownSideways += (Math.Clamp(sideways, -30f, 30f) - _ownSideways) * k;
        }
        _lastOwn = (x, y, z);
        bool lying = Prediction.Predicted.Posture == Posture.Lying;
        if (lying != _wasLying)
        {
            if (lying && !_restPending)
                Camera.AddShake(0.06f, 0.35f); // knocked down, not lying down by choice
            _wasLying = lying;
        }
        _restPending = false;
        bool floating = Prediction.Weightless;
        return Camera.Update(dtSeconds, lying, _ownSpeed, !lying && !floating && Prediction.Params.CanMove, floating, floating ? _ownSideways : 0f);
    }

    private float _ownSideways;

    private bool _restPending;

    private void OnCorrected(Reconciliation r)
    {
        // A displacement the player did not make (a shove) shows as an error of a good part of a tile. DV69: weightless, an error is a drift
        // tg started on the server (a push off into space, a spray), not a blow: no shake. Nor is it one for a body that cannot move (carried
        // on the bed it is buckled to), or when such errors keep coming (the wind, being pulled): only the first of them shakes. Nor is climbing
        // onto a table or a crate (the server's parameters say it stands on it now; the owner's play test of 2026-10-27).
        if (r.ErrorTiles < 0.15f)
            return;
        long tick = _replica.Own.Tick;
        bool steady = tick - _lastPushTick <= SteadyPushTicks;
        _lastPushTick = tick;
        // DV357: nor while the wind or a gravity pull carries it (a change of its strength is felt as the glide's change, not a blow)
        if (!steady && !Prediction.Weightless && Prediction.Params.CanMove && !Prediction.Params.OnClimbable && !Prediction.Params.Wind.Active && Prediction.Params.PullSpeed == 0)
        {
            Camera.AddShake(0.05f, 0.3f);
            CorrectionShakes++;
        }
    }

    // The server tick of the last correction big enough to shake, and how soon another counts as the same push going on.
    private long _lastPushTick = long.MinValue / 2;
    private const long SteadyPushTicks = 2 * MotionMath.TicksPerSecond;

    private void OnDespawned(ReplicaEntity entity)
    {
        Poses.Forget(entity.Id);
        OthersSpeed.Forget(entity.Id);
    }

    // ---- actions ----

    private ActionMessage Make(ActionKind kind, long target = 0, ClickButton button = ClickButton.None, ClickModifiers modifiers = ClickModifiers.None, string text = null)
        => new(Prediction.NewestSeq, kind, target, button, modifiers, text);

    /// <summary>Rest or get up (tg's <c>rest</c> keybinding).</summary>
    public void Rest()
    {
        _restPending = true;
        Send(Make(ActionKind.Rest));
    }

    /// <summary>Walk or run (press to cycle).</summary>
    public void ToggleMoveIntent() => Send(Make(ActionKind.ToggleMoveIntent) with { FrameSeq = Prediction.ToggleIntent() });

    /// <summary>Combat mode.</summary>
    public void ToggleCombatMode() => Send(Make(ActionKind.ToggleCombatMode));

    /// <summary>A click on <paramref name="target"/> (0: nothing under the crosshair, no message).</summary>
    public bool Click(long target, ClickButton button, ClickModifiers modifiers)
    {
        if (target == 0 || button == ClickButton.None)
            return false;
        NotePull(target, button, modifiers);
        Send(Make(ActionKind.Click, target, button, modifiers));
        return true;
    }

    /// <summary>
    /// M4a (interaction.md 2): a click on <paramref name="target"/> (an entity, a turf by its cell or a screen entry) with tg's pixel inside the clicked icon.
    /// Version 34: <paramref name="aim"/> is the camera's ray (<see cref="AimOf"/>) of a click on the world, so a shot flies along the crosshair (DV94).
    /// False, and nothing sent, when there is no target or no button. Package 3 (ui-overhaul.md section 10): a belt cell of the bar
    /// (<see cref="LiveHud.BeltCellOf"/>) is ours, not a tg entry: a plain left click on it is that cell's <see cref="HotbarPress"/>, as its key
    /// is, and no click goes out (false).
    /// </summary>
    public bool Click(in ClickTarget target, ClickButton button, ClickModifiers modifiers, ShotAim? aim = null)
    {
        if (LiveHud.BeltCellOf(target) is int cell and > 0)
        {
            if (button == ClickButton.Left && modifiers == ClickModifiers.None)
                HotbarPress(cell);
            return false;
        }
        if (!target.IsSet || button == ClickButton.None || LiveHud.IsOurs(target)) // slice 4A: any other of ours (the own sprite) is never sent
            return false;
        if (target.Kind == TargetKind.Entity)
            NotePull(target.Id, button, modifiers);
        Send(MakeTargeted(ActionKind.Click, target, button, modifiers, aim));
        return true;
    }

    // DV533: tg's ctrl-click on a movable pulls it (base_click_ctrl); the poses draw it trailing the own body while it follows
    private void NotePull(long target, ClickButton button, ClickModifiers modifiers)
    {
        if (button == ClickButton.Left && modifiers == ClickModifiers.Ctrl && target != _feed.OwnEntity)
            Poses.PullTarget = target;
    }

    /// <summary>
    /// Version 81 (D328): throw what the hand holds at <paramref name="target"/> (an entity or a turf), with the crosshair's body zone on a mob and the
    /// camera's <paramref name="aim"/> (the throw goes where the crosshair points): one action, whatever the throw mode is. False, and nothing sent,
    /// when there is no target in the world.
    /// </summary>
    public bool Throw(in ClickTarget target, ShotAim? aim)
    {
        if (!target.IsSet || target.Kind is not (TargetKind.Entity or TargetKind.Turf or TargetKind.Ceiling))
            return false;
        Send(MakeTargeted(ActionKind.Throw, target, ClickButton.Left, ClickModifiers.None, aim));
        return true;
    }

    /// <summary>
    /// Version 51 (M10x slice C): BYOND's double click on <paramref name="target"/>, sent after the second press's own <see cref="Click(in ClickTarget, ClickButton, ClickModifiers, ShotAim?)"/>
    /// (<see cref="DoubleClick"/>); no aim. False, and nothing sent, when there is no target or no button.
    /// </summary>
    public bool DblClick(in ClickTarget target, ClickButton button, ClickModifiers modifiers)
    {
        if (!target.IsSet || button == ClickButton.None)
            return false;
        Send(MakeTargeted(ActionKind.DblClick, target with { Aim = AimZone.None }, button, modifiers));
        return true;
    }

    /// <summary>
    /// Version 82 (D329): put what the active hand holds down at the crosshair's point on <paramref name="target"/> (a table's top or the floor
    /// in reach; the server drops it at the feet otherwise), with the click's pixel and the camera's <paramref name="aim"/>. False, and nothing
    /// sent, when there is no target in the world.
    /// </summary>
    public bool PutDown(in ClickTarget target, ShotAim? aim)
    {
        if (!target.IsSet || target.Kind is not (TargetKind.Entity or TargetKind.Turf or TargetKind.Ceiling))
            return false;
        Send(MakeTargeted(ActionKind.PutDown, target with { Aim = AimZone.None }, ClickButton.Left, ClickModifiers.None, aim));
        return true;
    }

    private ActionMessage MakeTargeted(ActionKind kind, in ClickTarget target, ClickButton button, ClickModifiers modifiers, ShotAim? aim = null)
    {
        bool pixel = kind is ActionKind.Click or ActionKind.Throw or ActionKind.PutDown or ActionKind.DblClick || IsMouse(kind); // the codec takes a pixel on a click, a throw, a put-down, a double click and a mouse action only
        return new(Prediction.NewestSeq, kind, target.Kind is TargetKind.Turf or TargetKind.Ceiling ? 0 : target.Id, button, modifiers, null)
        {
            TargetKind = target.Kind,
            TurfX = target.Kind is TargetKind.Turf or TargetKind.Ceiling ? target.X : 0,
            TurfY = target.Kind is TargetKind.Turf or TargetKind.Ceiling ? target.Y : 0,
            TurfZ = target.Kind is TargetKind.Turf or TargetKind.Ceiling ? target.Z : 0,
            IconX = pixel ? target.IconX : (byte)0,
            IconY = pixel ? target.IconY : (byte)0,
            AimZone = kind is ActionKind.Click or ActionKind.Throw or ActionKind.Hover && target.Kind == TargetKind.Entity ? target.Aim : AimZone.None, // version 98 also: a hover's zone
            // version 88 (DV527): the tick the world was drawn at, for the server's lag compensation
            Aim = pixel && kind != ActionKind.DblClick && aim is ShotAim a ? a with { Seen = (long)Math.Round(Math.Max(0, RenderTick) * 16) } : null,
        };
    }

    private static bool IsMouse(ActionKind kind) => kind is ActionKind.MouseDown or ActionKind.MouseDrag or ActionKind.MouseUp or ActionKind.MouseWheel;

    /// <summary>
    /// Version 70: BYOND's MouseWheel over <paramref name="target"/> (a thing or a turf; anything else is sent as nothing under the mouse): one
    /// notch <paramref name="up"/> or down, with the held modifiers (Shift turns the RPD's next build, D320).
    /// </summary>
    public void MouseWheel(in ClickTarget target, bool up, ClickModifiers modifiers)
    {
        ClickTarget over = target.Kind is TargetKind.Entity or TargetKind.Turf ? target with { Aim = AimZone.None } : default;
        Send(MakeTargeted(ActionKind.MouseWheel, over, up ? ClickButton.Left : ClickButton.Right, modifiers));
    }

    /// <summary>
    /// Version 34 (DV94): the aim of a camera ray: its eye in 1/256 tile (a tile's centre at 256 times its x) and millimetres above the level's
    /// floor, its direction as a compass yaw and a pitch, 65536 per turn.
    /// </summary>
    public static ShotAim AimOf(in PickRay ray)
    {
        static int Coordinate(double metres) => (int)Math.Clamp(Math.Round((metres / TileSpace.TileSize + 0.5) * 256.0), 0, Limits.MaxAimCoordinate);
        double length = Math.Sqrt(ray.DirEast * ray.DirEast + ray.DirNorth * ray.DirNorth + ray.DirUp * ray.DirUp);
        double up = length > 1e-9 ? ray.DirUp / length : 0;
        double yaw = Math.Atan2(ray.DirEast, ray.DirNorth) / (2 * Math.PI);
        if (yaw < 0)
            yaw += 1;
        double pitch = Math.Asin(Math.Clamp(up, -1, 1)) / (2 * Math.PI);
        return new ShotAim(Coordinate(ray.OriginEast), Coordinate(ray.OriginNorth), (ushort)Math.Clamp(Math.Round(ray.OriginUp * 1000.0), 0, ushort.MaxValue),
            (ushort)((int)Math.Round(yaw * 65536.0) & 0xFFFF), (short)Math.Clamp(Math.Round(pitch * 65536.0), -ShotAim.MaxPitch, ShotAim.MaxPitch));
    }

    /// <summary>Version 34: the shortest time between two drags while a button is held (the server takes <see cref="Limits.MouseDragRate"/>).</summary>
    public const double MouseDragInterval = 0.1;

    private ClickButton _mouseHeld;
    private double _lastDragAt = double.NegativeInfinity;

    /// <summary>Version 34: the button held since <see cref="MouseDown"/> (None when none is).</summary>
    public ClickButton MouseHeld => _mouseHeld;

    /// <summary>Version 34: mouse actions sent so far (down, drag and up).</summary>
    public int MouseActionsSent { get; private set; }

    /// <summary>
    /// Version 34: BYOND's MouseDown: <paramref name="button"/> went down over <paramref name="target"/> (none: nothing under the crosshair) with the
    /// camera's <paramref name="aim"/>. The client sends it just before the press's click (tg's full auto then drops that click).
    /// </summary>
    public void MouseDown(in ClickTarget target, ClickButton button, ClickModifiers modifiers, ShotAim? aim, double now)
    {
        if (button == ClickButton.None)
            return;
        _mouseHeld = button;
        _lastDragAt = now;
        MouseActionsSent++;
        Send(MakeTargeted(ActionKind.MouseDown, target, button, modifiers, aim));
    }

    /// <summary>Version 34: BYOND's MouseDrag while the button is held: the target under the crosshair and the aim, at most once per <see cref="MouseDragInterval"/>. True when one was sent.</summary>
    public bool MouseDrag(in ClickTarget target, ClickModifiers modifiers, ShotAim? aim, double now)
    {
        if (_mouseHeld == ClickButton.None || now - _lastDragAt < MouseDragInterval)
            return false;
        _lastDragAt = now;
        MouseActionsSent++;
        Send(MakeTargeted(ActionKind.MouseDrag, target, _mouseHeld, modifiers, aim));
        return true;
    }

    /// <summary>Version 34: BYOND's MouseUp: the held button went up over <paramref name="target"/> (it stops tg's full auto). Nothing when no button is held.</summary>
    public void MouseUp(in ClickTarget target, ClickModifiers modifiers, ShotAim? aim)
    {
        if (_mouseHeld == ClickButton.None)
            return;
        ClickButton button = _mouseHeld;
        _mouseHeld = ClickButton.None;
        MouseActionsSent++;
        Send(MakeTargeted(ActionKind.MouseUp, target, button, modifiers, aim));
    }

    /// <summary>M4a (D124): a tg keybinding pressed, or released (<paramref name="up"/>: only throw-hold has an effect on release).</summary>
    public void Keybinding(KeybindingCode code, bool up = false)
    {
        if (code == KeybindingCode.None || code > KeybindingCode.Last)
            return;
        if (code == KeybindingCode.StopPulling)
            Poses.PullTarget = 0;
        if (up)
            _heldKeybindings.Remove(code);
        else if (HasRelease(code))
            _heldKeybindings.Add(code);
        Send(new ActionMessage(Prediction.NewestSeq, ActionKind.Keybinding, 0, ClickButton.None, ClickModifiers.None, null) { Keybinding = code, KeyUp = up });
    }

    // The keybindings the scene also sends on the key's release (GameScene.OnKeyUp): tg acts on both, and keeps a state while one is held.
    private static bool HasRelease(KeybindingCode code) => code is KeybindingCode.HoldThrowMode or KeybindingCode.ZLevelUp or KeybindingCode.Resist
        or KeybindingCode.ViewPetCommands or KeybindingCode.Scoreboard;

    private readonly HashSet<KeybindingCode> _heldKeybindings = new(); // sent down, their release not yet sent

    /// <summary>
    /// Each frame: a keybinding sent down whose key is no longer down (<paramref name="keyDown"/>) gets its release now. Its own release can be
    /// lost: the window lost the focus (Godot sends no key release then), or a panel or the free camera took the key as it was let go.
    /// </summary>
    public void ReleaseLetGo(Func<KeybindingCode, bool> keyDown)
    {
        if (_heldKeybindings.Count == 0)
            return;
        foreach (KeybindingCode code in _heldKeybindings.ToArray())
        {
            if (!keyDown(code))
                Keybinding(code, up: true);
        }
    }

    /// <summary>Protocol version 38: a message of the say window to the server's /datum/tgui_say (<see cref="SayModel"/>).</summary>
    public void Say(in SayOut message)
    {
        if (!SayTypes.IsKnown(message.Type))
            return;
        Send(new ActionMessage(Prediction.NewestSeq, ActionKind.Say, 0, ClickButton.None, ClickModifiers.None, message.Entry)
        {
            SayType = message.Type,
            SayChannel = message.Channel,
            SayVisible = message.Visible,
        });
    }

    /// <summary>Protocol version 78: run the command verb <paramref name="path"/> of the own mob (a <see cref="VerbEntry.Path"/> the server listed).</summary>
    public void RunVerb(string path)
    {
        if (!string.IsNullOrEmpty(path))
            Send(Make(ActionKind.Verb, text: path));
    }

    /// <summary>Protocol version 78 (DV387): the player closed the popup window <paramref name="name"/> (tg's on-close command).</summary>
    public void ClosePopup(string name)
    {
        if (!string.IsNullOrEmpty(name))
            Send(Make(ActionKind.PopupClose, text: name));
    }

    /// <summary>Protocol version 39 (D256): the emote wheel's pick, tg's emote keybinding of <paramref name="key"/>; false (nothing sent) for a key no emote could have.</summary>
    public bool Emote(string key)
    {
        if (!EmoteKeys.IsValid(key))
            return false;
        Send(Make(ActionKind.Emote, text: key));
        return true;
    }

    /// <summary>The shortest time between two hovers (the server's bucket lets 10 a second through, ActionLimits.Hover).</summary>
    public const double HoverInterval = 0.1;

    /// <summary>
    /// Version 98 also (slice T4b): the shortest time between two hovers of the same thing whose zone changed: 8 a second. It mirrors the server's
    /// <c>ActionLimits.Hover</c> (a bucket of 5 hovers refilled at 10 a second, <see cref="HoverInterval"/>): the zone's resends share
    /// <see cref="HoverInterval"/>'s clock, so a sweep, however long, never sends faster than the refill and the server never drops its newest zone;
    /// the quarter below the refill leaves the bucket's burst for the arrival jitter.
    /// </summary>
    public const double ZoneHoverInterval = HoverInterval * 1.25;

    /// <summary>
    /// Version 98 also (slice T4b): whether a hover on <paramref name="target"/> carries the body zone looked at: a picked entity pixel seen through
    /// the crosshair. In cursor mode (Tab) it carries none: the free pointer passing over a person must not overwrite the zone chosen on tg's zone
    /// selector, which shows there; a click there still carries its zone.
    /// </summary>
    public static bool HoverCarriesZone(in ClickTarget target, bool cursorMode) => !cursorMode && target.Kind == TargetKind.Entity && target.IconX != 0;

    /// <summary>
    /// DV601: while the same world thing stays hovered, the hover goes out again this long after the last hover or unhover, so the server's answer
    /// follows reach (the prompt shows only in reach: the player walks up, steps back, or the thing moves), the held item and combat mode. Two a
    /// second, on <see cref="HoverInterval"/>'s clock, well inside the server's bucket (10 a second, 5 at once), so the zone's resends are not starved.
    /// None while the pet-commands key is held: tg's pets listen to MouseEntered then (obeys_commands.dm), and each repeat would be a new entering
    /// that opens a befriended pet's command radial again; the prompt does not follow reach meanwhile.
    /// </summary>
    public const double RepeatHoverInterval = 0.5;


    /// <summary>
    /// DV601: the server's newest hints (<see cref="HintsArrived"/>), kept only while <see cref="Hover"/> is given the same thing (any zone):
    /// a look at another thing, or at nothing, drops them, so an answer from an earlier look at a thing never stands for a new one (it may have
    /// been in reach then). Match them to a target with <see cref="KeyChips.AnswerFor"/>.
    /// </summary>
    public ContextHintsMessage Hints { get; private set; }

    /// <summary>A ContextHints message from the server: the newest answer to this look's hovers.</summary>
    public void HintsArrived(ContextHintsMessage hints) => Hints = hints;

    private ClickTarget _hintsLook;

    private ClickTarget _hovered;
    private ClickTarget _pendingHover;
    private bool _hasPendingHover;
    private double _lastHoverAt = double.NegativeInfinity;

    /// <summary>The target the last hover named (what the hints belong to); none after the pointer left it.</summary>
    public ClickTarget Hovered => _hovered;

    /// <summary>Hovers sent so far.</summary>
    public int HoversSent { get; private set; }

    /// <summary>Version 14: unhovers sent so far (the pointer left the hovered thing over nothing).</summary>
    public int UnhoversSent { get; private set; }

    // What the server was last told the pointer is over (a hover sent, none after an unhover): BYOND's client keeps it to call
    // MouseExited on it when the mouse leaves.
    private ClickTarget _serverHovered;

    /// <summary>
    /// Version 36 (D230): the next <see cref="Hover"/> of the same thing goes out again, so the server answers with fresh hints (the radial's
    /// Buckle, Unbuckle, Pull and Stop pulling follow what changed since: a pull started, a buckle made).
    /// </summary>
    public void Rehover() => _serverHovered = default;

    /// <summary>
    /// M4a (D125): the crosshair's target (or, in cursor mode, the HUD entry under the pointer) is <paramref name="target"/> now. A hover goes
    /// out when the thing changed, at most once per <see cref="HoverInterval"/> (the newest change waits for the interval). Version 14: nothing
    /// under it clears the hints at once and, when the server was told of a thing, sends an <see cref="ActionKind.Unhover"/> (BYOND's
    /// <c>MouseExited</c>), on the same interval. Version 98 also (slice T4b): the hover carries the target's <see cref="ClickTarget.Aim"/>, the zone looked
    /// at, and goes out again when only that zone changed, at most once per <see cref="ZoneHoverInterval"/>. Returns true when a hover or an unhover was sent.
    /// A belt cell of the bar (package 3) or the Tab inventory's own sprite (slice 4A) is no tg entry: over it the pointer hovers nothing.
    /// </summary>
    public bool Hover(in ClickTarget target, double now)
    {
        if (LiveHud.IsOurs(target)) // a belt cell, the own sprite (slice 4A)
            return Hover(ClickTarget.None, now);
        if (!target.SameThing(_hintsLook))
            (_hintsLook, Hints) = (target, null); // DV601: a new look, or none: the answer in hand was for an earlier one
        bool repeat = target.IsSet && target.Kind != TargetKind.Screen && !_heldKeybindings.Contains(KeybindingCode.ViewPetCommands) && now - _lastHoverAt >= RepeatHoverInterval; // DV601
        if (!target.IsSet)
        {
            _hovered = default;
            if (!_serverHovered.IsSet)
            {
                _hasPendingHover = false;
                return false;
            }
        }
        else if (!_hasPendingHover && target.SameThing(_hovered) && target.SameThing(_serverHovered) && target.Aim == _serverHovered.Aim && !repeat)
            return false;
        _pendingHover = target;
        _hasPendingHover = true;
        // version 98 also: a new zone on the same thing goes out too, at most ZoneHoverInterval apart (the newest waits for it)
        if (now - _lastHoverAt < (_pendingHover.IsSet && _pendingHover.SameThing(_serverHovered) ? ZoneHoverInterval : HoverInterval))
            return false;
        _lastHoverAt = now;
        _hasPendingHover = false;
        if (!_pendingHover.IsSet)
        {
            _serverHovered = default;
            UnhoversSent++;
            Send(Make(ActionKind.Unhover));
            return true;
        }
        if (_pendingHover.SameThing(_serverHovered) && _pendingHover.Aim == _serverHovered.Aim && !repeat)
        {
            _hovered = _pendingHover; // back on what the server still has: nothing to send
            return false;
        }
        _hovered = _pendingHover;
        _serverHovered = _pendingHover;
        HoversSent++;
        Send(MakeTargeted(ActionKind.Hover, _pendingHover, ClickButton.None, ClickModifiers.None));
        return true;
    }

    /// <summary>A dev command line (the server takes it only when its <c>DEV_COMMANDS</c> is on).</summary>
    public void DevCommand(string line) => Send(Make(ActionKind.DevCommand, text: line));

    /// <summary>
    /// Protocol version 24 (D154): a click on a link of tg's chat, its href (<c>byond://?...</c>, <see cref="HtmlText.ToPlain(string, List{ChatLink})"/>).
    /// The server takes only a link it sent to this player (DV59). False (nothing sent) for an href that is no BYOND link or is over
    /// <see cref="Limits.MaxTopicHrefBytes"/>.
    /// </summary>
    public bool Topic(string href)
    {
        if (string.IsNullOrEmpty(href) || !HtmlText.IsLink(href) || System.Text.Encoding.UTF8.GetByteCount(href) > Limits.MaxTopicHrefBytes)
            return false;
        Send(Make(ActionKind.Topic, text: href));
        TopicsSent++;
        return true;
    }

    /// <summary>Version 24: chat link clicks sent.</summary>
    public int TopicsSent { get; private set; }

    /// <summary>M4b slice 3 (the dev menu's gas overlay): asks the server for the air around the cell (<see cref="DevGasReport"/>).</summary>
    public void DevGasProbe(int x, int y, int z) =>
        Send(MakeTargeted(ActionKind.DevGasProbe, new ClickTarget(TargetKind.Turf, 0, x, y, z), ClickButton.None, ClickModifiers.None));

    /// <summary>
    /// Protocol version 15 (DV40): the player's pick of a radial menu the server showed (<see cref="ReplicaWorld.Radials"/>): element
    /// <paramref name="element"/> of its page (1 based, the message's order), or 0 for its centre (close). True when it was sent.
    /// </summary>
    public bool RadialPick(long menu, int element)
    {
        if (menu < 1 || element < 0 || element > Limits.MaxRadialElements)
            return false;
        Send(Make(ActionKind.RadialPick) with { RadialMenu = menu, RadialElement = element });
        RadialPicksSent++;
        return true;
    }

    /// <summary>Version 15: radial picks sent.</summary>
    public int RadialPicksSent { get; private set; }

    /// <summary>
    /// Protocol version 16 (DV44): tg's <c>act(action, params)</c> from the tgui window <paramref name="window"/> (<see cref="ReplicaWorld.UiWindows"/>);
    /// <paramref name="parameters"/> is an object or null. False (nothing sent) for a window out of range, an empty or over-long action, or
    /// params that are no object or over <see cref="Limits.MaxUiParamsBytes"/> on the wire.
    /// </summary>
    public bool UiAct(int window, string action, UiValue parameters = null)
    {
        if (window < 1 || window > Limits.MaxUiWindows || string.IsNullOrEmpty(action) || System.Text.Encoding.UTF8.GetByteCount(action) > Limits.MaxUiActionBytes)
            return false;
        if (parameters != null && (parameters.Kind != UiValueKind.Object || Codec.UiValueBytes(parameters) > Limits.MaxUiParamsBytes))
            return false;
        Send(Make(ActionKind.UiAct) with { UiWindow = window, UiAction = action, UiParams = parameters });
        UiActsSent++;
        return true;
    }

    /// <summary>Version 16: tg's <c>close</c> message: the player closed the tgui window <paramref name="window"/>.</summary>
    public bool UiClose(int window)
    {
        if (window < 1 || window > Limits.MaxUiWindows)
            return false;
        Send(Make(ActionKind.UiClose) with { UiWindow = window });
        UiClosesSent++;
        return true;
    }

    /// <summary>
    /// Protocol version 53: the Esc menu's bug report. False (nothing sent) without a title or what happened, a field over its
    /// cap (<see cref="Limits.MaxBugReportTitleBytes"/> and the others), or an image over <see cref="Limits.MaxBugReportImageBytes"/>.
    /// </summary>
    public bool BugReport(BugReportMessage report)
    {
        static bool Fits(string text, int max) => text == null || System.Text.Encoding.UTF8.GetByteCount(text) <= max;
        if (string.IsNullOrEmpty(report.Title) || string.IsNullOrEmpty(report.WhatHappened) || !Fits(report.Title, Limits.MaxBugReportTitleBytes)
            || !Fits(report.WhatHappened, Limits.MaxBugReportTextBytes) || !Fits(report.Steps, Limits.MaxBugReportTextBytes)
            || !Fits(report.Client, Limits.MaxBugReportClientBytes) || report.Image is { Length: 0 or > Limits.MaxBugReportImageBytes })
            return false;
        if (_feed.Phase != FeedPhase.Playing)
            return false;
        _feed.SendBugReport(report);
        return true;
    }

    /// <summary>Version 16: tgui acts sent.</summary>
    public int UiActsSent { get; private set; }

    /// <summary>Version 16: tgui window closes sent.</summary>
    public int UiClosesSent { get; private set; }

    /// <summary>Version 98 also (DV603): the own hotbar as the server last sent it (the cells, the held names, the last refused press).</summary>
    public HotbarModel Hotbar { get; } = new();

    /// <summary>
    /// Version 98 also (DV603, ui-overhaul.md section 10): a press of hotbar cell <paramref name="cell"/> (1 to 10), sent as it is; the server decides
    /// what it does (nothing is predicted). False, and nothing sent, for a cell out of range.
    /// </summary>
    public bool HotbarPress(int cell)
    {
        if (cell is < 1 or > HotbarMessage.CellCount)
            return false;
        Send(new ActionMessage(Prediction.NewestSeq, ActionKind.HotbarPress, 0, ClickButton.None, ClickModifiers.None, null) { HotbarCell = cell });
        return true;
    }

    /// <summary>
    /// Protocol version 17 (drag and drop): the player dragged <see cref="DragRelease.Source"/> with its button held and let go over
    /// <see cref="DragRelease.Over"/> (BYOND's MouseDrop; the server runs tg's /client/MouseDrop). The icon pixel is the one under the
    /// pointer at the release (the over thing's). False, and nothing sent, without both things or a button.
    /// </summary>
    /// <summary>Version 97 (DV597): the world is built and loaded (the loading screen is done): the server may late-join the new player now.</summary>
    public void Loaded() => Send(new ActionMessage(Prediction.NewestSeq, ActionKind.Loaded, 0, ClickButton.None, ClickModifiers.None, null));

    /// <summary>Version 86 (DV503): the radial's entry <paramref name="label"/> that is no click (the table's Flip) picked on an entity <paramref name="target"/>.</summary>
    public bool ContextPick(in ClickTarget target, string label)
    {
        if (!target.IsSet || target.Kind != TargetKind.Entity || string.IsNullOrEmpty(label))
            return false;
        Send(new ActionMessage(Prediction.NewestSeq, ActionKind.ContextPick, target.Id, ClickButton.None, ClickModifiers.None, label) { TargetKind = TargetKind.Entity });
        return true;
    }

    /// <summary>
    /// Protocol version 17 (drag and drop): the player dragged <see cref="DragRelease.Source"/> with its button held and let go over
    /// <see cref="DragRelease.Over"/> (BYOND's MouseDrop; the server runs tg's /client/MouseDrop). The icon pixel is the one under the
    /// pointer at the release (the over thing's). False, and nothing sent, without both things or a button.
    /// </summary>
    public bool Drop(in DragRelease drop)
    {
        // package 3: a belt cell of the bar (slice 4A: or the own sprite) is no tg entry the server knows; dragging to or from it does nothing yet
        if (!drop.Source.IsSet || !drop.Over.IsSet || drop.Button == ClickButton.None || LiveHud.IsOurs(drop.Source) || LiveHud.IsOurs(drop.Over))
            return false;
        bool sourceCell = drop.Source.Kind is TargetKind.Turf or TargetKind.Ceiling, overCell = drop.Over.Kind is TargetKind.Turf or TargetKind.Ceiling;
        Send(new ActionMessage(Prediction.NewestSeq, ActionKind.Drop, sourceCell ? 0 : drop.Source.Id, drop.Button, drop.Modifiers, null)
        {
            TargetKind = drop.Source.Kind,
            TurfX = sourceCell ? drop.Source.X : 0,
            TurfY = sourceCell ? drop.Source.Y : 0,
            TurfZ = sourceCell ? drop.Source.Z : 0,
            OverKind = drop.Over.Kind,
            OverTarget = overCell ? 0 : drop.Over.Id,
            OverX = overCell ? drop.Over.X : 0,
            OverY = overCell ? drop.Over.Y : 0,
            OverZ = overCell ? drop.Over.Z : 0,
            IconX = drop.Over.IconX,
            IconY = drop.Over.IconY,
            DragTime = drop.DragTime,
        });
        DropsSent++;
        return true;
    }

    /// <summary>Version 17: drops sent.</summary>
    public int DropsSent { get; private set; }

    private void Send(in ActionMessage message)
    {
        if (_feed.Phase != FeedPhase.Playing)
            return;
        Guesses.OnAction(message); // every sent action counts: the server's ActionsDone numbers them the same way
        _feed.SendAction(message);
    }

    // ---- the crosshair ----

    /// <summary>
    /// The mob under the crosshair: the first mob along the view's ray from the eye (<see cref="RayPicker"/>), walls stopping
    /// the ray. False when there is none. <paramref name="eyeHeight"/> is the eye above the floor, <paramref name="pitch"/> up
    /// positive; <paramref name="inReach"/> says the mob is on the own tile or a neighbouring one (tg's <c>Adjacent</c>, which the
    /// server checks for itself).
    /// </summary>
    public bool TryPick(double godotYaw, double pitch, float eyeHeight, out long id, out bool inReach, double maxMetres = 8.0)
    {
        id = 0;
        inReach = false;
        if (!Prediction.Active || Prediction.TileDriven || Ghosting)
            return false; // D151: a ghost's clicks are out of the minimal scope
        Poses.GatherMobs(_candidates);
        (float x, float y, int z) = Prediction.RenderPosition();
        if (!RayPicker.TryPick(x, y, z, eyeHeight, godotYaw, pitch, _candidates, _replica.IsSolid, maxMetres, _feed.OwnEntity, out id, out _))
            return false;
        foreach (PickCandidate c in _candidates)
        {
            if (c.Id == id)
            {
                inReach = c.Z == z && Math.Abs((int)Math.Round(c.TileX) - (int)Math.Round(x)) <= 1 && Math.Abs((int)Math.Round(c.TileY) - (int)Math.Round(y)) <= 1;
                break;
            }
        }
        return true;
    }

    /// <summary>The id of the mob under the crosshair, 0 when there is none (<see cref="TryPick"/>).</summary>
    public long PickTarget(double godotYaw, double pitch, float eyeHeight, double maxMetres = 8.0)
        => TryPick(godotYaw, pitch, eyeHeight, out long id, out _, maxMetres) ? id : 0;
}
