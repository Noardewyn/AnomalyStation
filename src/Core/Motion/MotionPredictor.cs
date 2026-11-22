namespace Anomaly.Core.Motion;

// Owner: the contract is the M2 contracts step's (frozen); the bodies are M2 P4's. Users: the Godot client (M2 P8) and the
// bots (M2 P6); the headless prediction tests of the integration step.

/// <summary>
/// <see cref="MotionPredictor.Pushes"/>: whether the press <paramref name="press"/> of frame <paramref name="seq"/> on level <paramref name="z"/>
/// pushes a thing away, the mob's glide after it taking <paramref name="glideTicks"/> ticks.
/// </summary>
public delegate bool PushGuess(in MotionEvent press, int z, uint seq, ushort glideTicks);

/// <summary>What one <see cref="MotionPredictor.Reconcile"/> found.</summary>
/// <param name="AckedSeq">The frame seq the authoritative state follows (<c>lastInputSeq</c>).</param>
/// <param name="Replayed">How many newer frames were replayed on top of it.</param>
/// <param name="ErrorTiles">The distance between the old prediction for the newest frame and the new one, in tiles.</param>
/// <param name="Kind">What the predictor did with the difference.</param>
public readonly record struct Reconciliation(uint AckedSeq, int Replayed, float ErrorTiles, CorrectionKind Kind);

/// <summary>How a prediction error is shown (scope 2d "Reconciliation").</summary>
public enum CorrectionKind : byte
{
    /// <summary>Under <see cref="MotionPredictor.IgnoreBelowTiles"/> (1 cm): nothing to do; the replayed state is taken as is.</summary>
    None = 0,

    /// <summary>Up to <see cref="MotionPredictor.SnapAboveTiles"/> (a tile; <see cref="MotionPredictor.SlideReachTiles"/> while the server's state slides): the replayed state is taken, and the visible error decays over about <see cref="MotionPredictor.DecaySeconds"/>.</summary>
    Smoothed = 1,

    /// <summary>More: the view snaps to the replayed state.</summary>
    Snapped = 2,
}

/// <summary>
/// The counters the prediction tests read ("zero corrections when walking alone", scope 2l). A reconciliation while the body cannot
/// move itself (<see cref="MotionParams.CanMove"/> false: carried in a transit tube pod, buckled, stunned) is no misprediction, the
/// server alone moves it: it is counted in <see cref="Carried"/>, not in <see cref="Corrections"/>, <see cref="Snaps"/> or <see cref="Mismatches"/>.
/// </summary>
public struct PredictionCounters
{
    /// <summary>Authoritative states reconciled.</summary>
    public long Reconciliations;

    /// <summary>Reconciliations whose error was at least <see cref="MotionPredictor.IgnoreBelowTiles"/> (Smoothed plus Snapped).</summary>
    public long Corrections;

    /// <summary>Reconciliations that snapped.</summary>
    public long Snaps;

    /// <summary>Reconciliations whose replayed state differed from the old prediction in any bit (also below 1 cm): the bit-exactness check of scope 2l.</summary>
    public long Mismatches;

    /// <summary>Reconciliations with an error of at least <see cref="MotionPredictor.IgnoreBelowTiles"/> while the body could not move itself.</summary>
    public long Carried;

    /// <summary>The largest error seen, in tiles.</summary>
    public float MaxErrorTiles;

    /// <summary>Frames dropped because the ring was full (the server acknowledged nothing for <see cref="MotionPredictor.Capacity"/> frames).</summary>
    public long Overflows;

    /// <summary>
    /// M4c slice 7: reconciliations of a weightless capsule that the client did not predict (D146). Since DV69 a weightless capsule is
    /// predicted as any other (its push-offs, its jetpack flight and its drift), so none are counted.
    /// </summary>
    public long ServerDriven;
}

/// <summary>
/// Own-capsule prediction (D103, scope 2d): the client steps its own capsule with <see cref="MotionStep.Step"/> on each
/// input frame it sends, keeps a ring of the frames with the state predicted after each, and on every Delta resets to the
/// server's state after the acknowledged frame (<c>lastInputSeq</c>), replays the later frames through the same step and
/// view, and compares. Engine-free and single-threaded: the Godot client calls it from <c>_Process</c>, the bots from their
/// update.
/// <para>
/// The client's tick: once per server tick of its input clock it calls <see cref="Push"/> (which assigns the seq and steps
/// the prediction), sends <see cref="CopyRecentFrames"/> (the newest up to <see cref="Redundancy"/> frames) on the Input
/// channel, and draws <see cref="Predicted"/> plus <see cref="VisualOffset"/> (and a render-only partial step of its own).
/// On each Delta with an own block it calls <see cref="Reconcile"/>; every frame it calls <see cref="Decay"/>.
/// </para>
/// </summary>
public sealed class MotionPredictor
{
    /// <summary>The error below which a reconciliation changes nothing visible: 1 cm at 2 m tiles.</summary>
    public const float IgnoreBelowTiles = 0.005f;

    /// <summary>The error above which the view snaps instead of smoothing: one tile.</summary>
    public const float SnapAboveTiles = 1f;

    /// <summary>
    /// The error up to which a correction is still smoothed while the server's state slides (tg moved the mob: a pull of the bed one lies
    /// on, the wind, a shove; diagonal, or two tiles weightless): the slide is shown as the glide it is, not as a jump.
    /// </summary>
    public const float SlideReachTiles = 3f;

    /// <summary>About how long a smoothed error takes to disappear.</summary>
    public const float DecaySeconds = 0.1f;

    /// <summary>Frames repeated in every input message (scope 2d: "each message repeating the last 4 frames").</summary>
    public const int Redundancy = 4;

    // The pending frames (sent, not yet acknowledged) with the state predicted after each, oldest first, in a ring.
    private readonly MotionInput[] _frames;
    private readonly MotionState[] _after;
    private int _head;   // index of the oldest pending frame
    private int _count;  // pending frames

    // The newest frames pushed, acknowledged or not, for the input message's redundancy (newest last).
    private readonly MotionInput[] _recent = new MotionInput[Redundancy];
    private int _recentCount;

    private MotionState _predicted;
    private uint _nextSeq = 1;
    private uint _ackedSeq;
    private PredictionCounters _counters;
    private float _offsetX;
    private float _offsetY;
    private float _decayRate; // tiles per second of the linear decay of the visible error

    // The walk/run toggles sent and not yet acknowledged: the seq of the frame each takes effect at (ToggleIntentAt).
    private readonly List<uint> _intentToggles = new();

    /// <summary>A predictor that starts from <paramref name="start"/> with a ring of <paramref name="capacity"/> frames.</summary>
    public MotionPredictor(in MotionState start, int capacity = 64)
    {
        if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "The ring holds at least one frame.");
        _frames = new MotionInput[capacity];
        _after = new MotionState[capacity];
        _predicted = start;
    }

    /// <summary>The ring's size: frames that can wait for an acknowledgement.</summary>
    public int Capacity => _frames.Length;

    /// <summary>The predicted state after the newest pushed frame.</summary>
    public MotionState Predicted => _predicted;

    /// <summary>The drift direction after the newest predicted input, including sub-tile thrust.</summary>
    public MotionParams PredictedParams { get; private set; }

    /// <summary>The seq the next <see cref="Push"/> gives (1 for the first).</summary>
    public uint NextSeq => _nextSeq;

    /// <summary>The seq of the last frame the server acknowledged (0: none).</summary>
    public uint AckedSeq => _ackedSeq;

    /// <summary>Frames sent and not yet acknowledged.</summary>
    public int Pending => _count;

    /// <summary>The counters of every reconciliation so far.</summary>
    public PredictionCounters Counters => _counters;

    /// <summary>The visible error still being smoothed away, in tiles: draw the capsule (and the camera) at <see cref="Predicted"/> plus this.</summary>
    public (float X, float Y) VisualOffset => (_offsetX, _offsetY);

    /// <summary>
    /// A new frame for the next tick: gives it the next seq, steps the prediction with it under <paramref name="parameters"/>
    /// (the newest the server sent) and <paramref name="view"/>, rings the frame with the state after it, and returns the
    /// frame to send. A full ring drops its oldest frame (<see cref="PredictionCounters.Overflows"/>).
    /// </summary>
    public MotionInput Push(sbyte moveX, sbyte moveY, ushort yaw, MotionInputFlags flags, in MotionParams parameters, ICollisionView view, MotionEvents events = null)
    {
        ArgumentNullException.ThrowIfNull(view);
        var frame = new MotionInput(_nextSeq++, moveX, moveY, yaw, flags);
        view.SteppingFrame = frame.Seq;
        ApplyTramCarry(ref _predicted, view);
        MotionParams stepParams = SpaceGateOf(in _predicted, InGravityOf(ref _predicted, UnblockedAt(frame.Seq, IntentAt(frame.Seq, in parameters))), view);
        MotionState before = _predicted;
        int firstEvent = events?.Count ?? 0;
        if (events == null)
        {
            events = _replayEvents;
            events.Clear();
        }
        bool driftedBefore = events.Drifting; // a caller's events may hold earlier frames: this frame's drift alone decides its halt
        events.Drifting = false;
        MotionStep.Step(ref _predicted, in frame, in stepParams, view, events);
        bool drifted = events.Drifting;
        events.Drifting |= driftedBefore;
        ApplyPushes(ref _predicted, in before, in frame, events, firstEvent, in stepParams, view);
        ApplyLevelShift(ref _predicted, events, firstEvent, in stepParams, view);
        PredictedParams = HaltAtHandhold(in _predicted, in stepParams, OffClimbable(before, _predicted, DriftAfterInput(before, _predicted, frame, parameters, view), view), drifted, frame.Seq, view);
        if (_count == _frames.Length)
        {
            _head = (_head + 1) % _frames.Length;
            _count--;
            _counters.Overflows++;
        }
        int slot = (_head + _count) % _frames.Length;
        _frames[slot] = frame;
        _after[slot] = _predicted;
        _count++;
        if (_recentCount < Redundancy)
            _recent[_recentCount++] = frame;
        else
        {
            Array.Copy(_recent, 1, _recent, 0, Redundancy - 1);
            _recent[Redundancy - 1] = frame;
        }
        return frame;
    }

    /// <summary>Copies the newest frames (up to <paramref name="max"/>, newest last) into <paramref name="into"/>; returns how many.</summary>
    public int CopyRecentFrames(Span<MotionInput> into, int max = Redundancy)
    {
        int n = Math.Min(Math.Min(max, _recentCount), into.Length);
        if (n <= 0) return 0;
        for (int i = 0; i < n; i++)
            into[i] = _recent[_recentCount - n + i];
        return n;
    }

    /// <summary>
    /// The server's state after frame <paramref name="ackedSeq"/> (the private block of a Delta): drops the acknowledged
    /// frames, resets to <paramref name="authoritative"/>, replays the frames after it with <paramref name="parameters"/>
    /// and <paramref name="view"/>, compares the result with the old prediction of the newest frame, and smooths or snaps
    /// the difference (<see cref="CorrectionKind"/>). An acknowledgement older than the last one is ignored (a Delta that
    /// arrived out of order); the same acknowledgement again is reconciled (the server may have stepped a repeated frame or
    /// been displaced by tg since).
    /// </summary>
    public Reconciliation Reconcile(in MotionState authoritative, uint ackedSeq, in MotionParams parameters, ICollisionView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        if (ackedSeq < _ackedSeq)
            return new Reconciliation(ackedSeq, 0, 0f, CorrectionKind.None);
        _ackedSeq = ackedSeq;
        // D331: the server stood in for seqs this client has not made yet (its own frames came late): they are taken, the next frame follows them
        if (_nextSeq <= ackedSeq)
        {
            _nextSeq = ackedSeq + 1;
            _recentCount = 0; // the redundant frames sent again are consecutive: none from before the gap
        }
        while (_count > 0 && _frames[_head].Seq <= ackedSeq)
        {
            _head = (_head + 1) % _frames.Length;
            _count--;
        }
        _intentToggles.RemoveAll(seq => seq <= ackedSeq); // the server's parameters after the acknowledged frame have them

        MotionState old = _predicted;
        bool climbed = parameters.OnClimbable && !PredictedParams.OnClimbable; // tg's climb (do_climb's forceMove) finished: not the player's step
        MotionState state = authoritative;
        MotionParams replayParams = parameters;
        for (int i = 0; i < _count; i++)
        {
            int slot = (_head + i) % _frames.Length;
            view.SteppingFrame = _frames[slot].Seq;
            ApplyTramCarry(ref state, view);
            MotionParams stepParams = SpaceGateOf(in state, InGravityOf(ref state, UnblockedAt(_frames[slot].Seq, IntentAt(_frames[slot].Seq, in replayParams))), view);
            MotionState before = state;
            MotionEvents events = _replayEvents;
            events.Clear();
            MotionStep.Step(ref state, in _frames[slot], in stepParams, view, events);
            ApplyPushes(ref state, in before, in _frames[slot], events, 0, in stepParams, view);
            ApplyLevelShift(ref state, events, 0, in stepParams, view);
            replayParams = HaltAtHandhold(in state, in stepParams, OffClimbable(before, state, DriftAfterInput(before, state, _frames[slot], replayParams, view), view), events.Drifting, _frames[slot].Seq, view);
            _after[slot] = state;
        }
        view.SteppingFrame = _nextSeq - 1;
        _predicted = state;
        PredictedParams = replayParams;

        _counters.Reconciliations++;
        // D325: an elevator's car moved its rider a level (tg's group_move on the lift's timer) or a ladder its climber: a carry, as a transit tube's
        bool lifted = old.Z != state.Z && (view.OnLift(state.TileX, state.TileY, state.Z) || view.OnLift(old.TileX, old.TileY, old.Z));
        // DV422: a level up or down the same column (a flyer's or a ghost's level key, a fall through open space) is the server's move too; the
        // predictor changes the level only on stairs, which its replay makes as the server does
        bool column = old.Z != state.Z && Math.Abs(old.TileX - state.TileX) <= 1 && Math.Abs(old.TileY - state.TileY) <= 1;
        bool carried = !parameters.CanMove || climbed || lifted || column; // only the server moves it: nothing the client could have predicted
        if (!SameBits(in old, in state) && !carried)
            _counters.Mismatches++;
        float ex = MotionMath.ToTiles(old.X - state.X);
        float ey = MotionMath.ToTiles(old.Y - state.Y);
        float error = old.Z != state.Z ? float.PositiveInfinity : MathF.Sqrt(ex * ex + ey * ey);
        if (error > _counters.MaxErrorTiles && !float.IsInfinity(error) && !carried)
            _counters.MaxErrorTiles = error;
        CorrectionKind kind;
        if (error < IgnoreBelowTiles)
        {
            kind = CorrectionKind.None; // taken as is: under a centimetre nobody sees it
        }
        // DV357: the wind or a pull that changes its strength moves the server's state by its change times the frames ahead: smoothed as a slide
        else if (error <= (authoritative.Slide.Active || parameters.Wind.Active || parameters.PullSpeed > 0 ? SlideReachTiles : SnapAboveTiles))
        {
            kind = CorrectionKind.Smoothed;
            Count(carried);
            // Draw where the old prediction was drawn, then let the difference decay to the new state.
            _offsetX += ex;
            _offsetY += ey;
            _decayRate = MathF.Sqrt(_offsetX * _offsetX + _offsetY * _offsetY) / DecaySeconds;
        }
        else
        {
            kind = CorrectionKind.Snapped;
            Count(carried);
            if (!carried)
                _counters.Snaps++;
            _offsetX = 0f;
            _offsetY = 0f;
            _decayRate = 0f;
        }
        if (float.IsInfinity(error))
            error = column ? 0f : SnapAboveTiles + 1f; // another level: a snap (no error shown, so no shake, for the server's move up or down a column)
        return new Reconciliation(ackedSeq, _count, error, kind);
    }

    /// <summary>
    /// The client's guess of tg's push (living.dm PushAM): asked for each press of a stepped frame (pushed or replayed), true when the pressed tile
    /// holds a thing tg pushes away; DV535: the capsule then walks on into the pressed tile, where tg's mob steps after it at once.
    /// </summary>
    public PushGuess Pushes { get; set; }

    private readonly MotionEvents _replayEvents = new();

    // D325, DV370: the first crossing of the step that tg's code turns into a level change (the stairs' climb, a fall through open space)
    // moves the capsule as the server's bridge follows the mob (MotionStep.Displace keeps its place and speed); the server stops the
    // step's later events there
    public static void ApplyLevelShift(ref MotionState state, MotionEvents events, int first, in MotionParams parameters, ICollisionView view)
    {
        if (state.Driver != MotionDriver.Grounded || parameters.NoClip)
            return;
        for (int i = first; i < events.Count; i++)
        {
            MotionEvent e = events[i];
            if (e.Kind is not (MotionEventKind.Crossed or MotionEventKind.Pressing))
                continue;
            int shift = view.LevelShift(e.FromX, e.FromY, state.Z, e.Dir);
            // a press against the face the stairs go up by climbs too: the server's bridge Moves the pressing mob, and the stairs' on_exit
            // takes it up before the wall or railing ahead is asked (the owner's report: tg's stairs end against a wall or a railing); a fall
            // needs the crossing
            if (shift == 0 || e.Kind == MotionEventKind.Pressing && shift < 0)
                continue;
            if (e.Kind == MotionEventKind.Pressing)
                MotionStep.Displace(ref state, e.ToX, e.ToY, state.Z + shift, 0); // the stairs' zMove onto the turf over the one ahead, as the capsule follows it
            else
                MotionStep.Displace(ref state, state.TileX, state.TileY, state.Z + shift, 0);
            return;
        }
    }

    // DEVIATION(DV403): the server's tram steps in its world phase, before the capsules: one that steps in the frame's tick carries a capsule
    // standing on it by its tile, as the server's bridge does when tg's group_move moves the mob (MotionStep.Carry)
    private static void ApplyTramCarry(ref MotionState state, ICollisionView view)
    {
        (int dx, int dy) = view.TramCarry(state.TileX, state.TileY, state.Z);
        if (dx != 0 || dy != 0)
            MotionStep.Carry(ref state, dx, dy);
    }

    // DEVIATION(DV535): a press guessed pushing leaves the capsule to walk on into the freed tile itself, as the server's does; its step is
    // taken again through that tile (the guess makes it passable from the next frame on), as the server's bridge takes it again
    private void ApplyPushes(ref MotionState state, in MotionState before, in MotionInput frame, MotionEvents events, int first, in MotionParams parameters, ICollisionView view)
    {
        if (Pushes == null || events == null)
            return;
        ushort glide = PushGlideTicks(parameters.TilesPerTick);
        for (int i = first; i < events.Count; i++)
        {
            MotionEvent e = events[i];
            if (e.Kind != MotionEventKind.Pressing || !Pushes(in e, state.Z, frame.Seq, glide))
                continue;
            if (MotionStep.WalksOnAfterPush(in state, events, first))
            {
                view.SteppingFrame = frame.Seq + 1;
                MotionStep.StepAgain(ref state, in before, in frame, in parameters, view, events);
                view.SteppingFrame = frame.Seq;
            }
            return;
        }
    }

    /// <summary>
    /// The glide of the mob's step after a push in ticks: the bridge's <c>set_glide_size(DELAY_TO_GLIDE_SIZE(slowdown / speed scale))</c>, whose
    /// delay over the tick is one over <paramref name="tilesPerTick"/> (MotionMath.TilesPerTickFor), and LocomotionSeam.GlideTicks of it.
    /// </summary>
    public static ushort PushGlideTicks(float tilesPerTick)
        => tilesPerTick > 0f ? (ushort)Math.Clamp((int)MathF.Ceiling(1f / tilesPerTick - 1e-4f), 1, 32) : (ushort)1;

    /// <summary>
    /// The player toggled walk and run with the action of frame <paramref name="seq"/> (a frame not pushed yet: the server applies the action
    /// before it steps that frame), so that frame and the later ones step at <see cref="MotionParams.IntentTilesPerTick"/> until the server's
    /// parameters after it arrive.
    /// </summary>
    public void ToggleIntentAt(uint seq) => _intentToggles.Add(seq);

    // The parameters frame seq steps with: an odd number of toggles at or before it swaps the speed and the move delay for the other intent's.
    // shortcut: OtherTilesPerTick (the other gravity kind's speed) keeps the old intent's, until the server's parameters arrive.
    private MotionParams IntentAt(uint seq, in MotionParams parameters)
    {
        int toggles = 0;
        foreach (uint at in _intentToggles)
            if (at <= seq)
                toggles++;
        if ((toggles & 1) == 0 || !(parameters.IntentTilesPerTick > 0f))
            return parameters;
        return parameters with
        {
            TilesPerTick = parameters.IntentTilesPerTick,
            IntentTilesPerTick = parameters.TilesPerTick,
            MoveDelayTicks = parameters.IntentMoveDelayTicks,
            IntentMoveDelayTicks = parameters.MoveDelayTicks,
        };
    }

    // Protocol version 50: from the UnblockTicks-th frame after the acknowledged one tg's drift input block no longer holds the capsule.
    private MotionParams UnblockedAt(uint seq, in MotionParams parameters)
        => !parameters.CanMove && parameters.UnblockTicks > 0 && seq - _ackedSeq >= (uint)parameters.UnblockTicks ? parameters with { CanMove = true, UnblockTicks = 0 } : parameters;

    private void Count(bool carried)
    {
        if (carried)
            _counters.Carried++;
        else
            _counters.Corrections++;
    }

    /// <summary>
    /// The parameters a frame steps with where the capsule now is (protocol version 48): on a tile of <see cref="MotionParams.GravityMask"/>
    /// whose kind differs from the mask's centre the speed is <see cref="MotionParams.OtherTilesPerTick"/>, and the driver turns
    /// <see cref="MotionDriver.ZeroG"/> on a weightless tile and back to <see cref="MotionDriver.Grounded"/> off one, as the server's bridge
    /// turns it at the start of the step from the mob's tile (a slide's target while sliding).
    /// </summary>
    public static MotionParams InGravityOf(ref MotionState state, in MotionParams parameters)
    {
        if (parameters.GravityMask == 0 || state.Driver == MotionDriver.Thrown)
            return parameters;
        int dx = (state.Slide.Active ? MotionMath.TileOf(state.Slide.TargetX) : state.TileX) - parameters.GravityX;
        int dy = (state.Slide.Active ? MotionMath.TileOf(state.Slide.TargetY) : state.TileY) - parameters.GravityY;
        if (Math.Abs(dx) > 3 || Math.Abs(dy) > 3)
            return parameters;
        bool weightless = ((parameters.GravityMask >> ((dy + 3) * 7 + dx + 3)) & 1) != 0;
        if (weightless)
            state.Driver = MotionDriver.ZeroG;
        else if (state.Driver == MotionDriver.ZeroG)
            state.Driver = MotionDriver.Grounded;
        bool centre = ((parameters.GravityMask >> 24) & 1) != 0;
        return weightless == centre ? parameters : parameters with { TilesPerTick = parameters.OtherTilesPerTick };
    }

    /// <summary>
    /// Protocol version 50: on a weightless tile reached from a floor (the parameters were made where the mob had gravity) tg's gate refuses
    /// a step with nothing to push off (<c>get_spacemove_backup</c>: a dense tile around), unless the mob may
    /// steer without (<see cref="MotionParams.OtherCanMove"/>).
    /// shortcut: dense movables that are no solid tile (a mob, a pane on a border) are not looked at, until a play test shows them missed.
    /// </summary>
    private static MotionParams SpaceGateOf(in MotionState state, MotionParams parameters, ICollisionView view)
    {
        if (state.Driver != MotionDriver.ZeroG || parameters.NoClip || !parameters.CanMove)
            return parameters;
        // parameters made where the mob had gravity tell OtherCanMove; ones made weightless tell SteersByHandhold (DV534)
        bool fromGround = parameters.GravityMask != 0 && ((parameters.GravityMask >> 24) & 1) == 0;
        if (fromGround ? parameters.OtherCanMove : !parameters.SteersByHandhold)
            return parameters;
        return InReach(in state, view) ? parameters : parameters with { CanMove = false };
    }

    // tg's get_spacemove_backup (mob_movement.dm:306) as the view knows it: DV534's handhold (a solid cell, a lattice) within the hand's reach of the centre.
    private static bool InReach(in MotionState state, ICollisionView view)
    {
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
                if (MotionMath.InHandReach(state.X, state.Y, state.TileX + dx, state.TileY + dy) && view.IsHandhold(state.TileX + dx, state.TileY + dy, state.Z))
                    return true;
        return false;
    }

    /// <summary>
    /// Protocol version 89 (DV534; tg's <c>after_move</c>, drift_handler.dm:153): a drift halts on the tick a handhold comes within the hand's
    /// reach, as the server checks each drifting tick; the input stays blocked until tg's <c>block_inputs_until</c>, the loop's delay and 1 ds
    /// after the halt.
    /// </summary>
    private MotionParams HaltAtHandhold(in MotionState state, in MotionParams stepParams, MotionParams next, bool drifting, uint seq, ICollisionView view)
    {
        if (!drifting || !stepParams.HaltsAtHandhold || (stepParams.DriftX == 0 && stepParams.DriftY == 0) || stepParams.NoClip
            || state.Driver != MotionDriver.ZeroG || state.Slide.Active || !InReach(in state, view))
            return next;
        double speed = Math.Sqrt((double)stepParams.DriftX * stepParams.DriftX + (double)stepParams.DriftY * stepParams.DriftY);
        long block = (long)Math.Ceiling(MotionMath.SubunitsPerTile / speed) + (long)Math.Round(1 / MotionMath.TickDeciseconds);
        return next with
        {
            DriftX = 0, DriftY = 0, HaltsAtHandhold = false, ImpulseSpeed = 0, CanMove = false,
            UnblockTicks = (short)Math.Clamp(seq - _ackedSeq + block, 1, short.MaxValue),
        };
    }

    // Protocol version 50: whether the parameters' mask has tile (x, y) weightless.
    private static bool WeightlessIn(in MotionParams parameters, int x, int y)
    {
        int dx = x - parameters.GravityX, dy = y - parameters.GravityY;
        return parameters.GravityMask != 0 && Math.Abs(dx) <= 3 && Math.Abs(dy) <= 3 && ((parameters.GravityMask >> ((dy + 3) * 7 + dx + 3)) & 1) != 0;
    }

    // climb_walkable's on_climb_exit (climb_walkable.dm:44): a step off a table or crate onto a tile without one takes TRAIT_ON_CLIMBABLE away,
    // so the frames after it are stopped by the table again, as the server's are, before the server's parameters say so.
    private static MotionParams OffClimbable(in MotionState before, in MotionState after, in MotionParams parameters, ICollisionView view)
        => parameters.OnClimbable && (after.TileX != before.TileX || after.TileY != before.TileY) && !view.IsClimbWalkable(after.TileX, after.TileY, after.Z)
            ? parameters with { OnClimbable = false }
            : parameters;

    // DV177: the server updates tg's inertia after accepted continuous input, even without a cell crossing.
    // Predict that direction for subsequent idle frames and replay it in sequence. The server still supplies
    // the drift's speed and whether thrust is possible; a first impulse in open space starts at the server's ImpulseSpeed
    // (protocol version 51), one beside something to push off and environmental stops remain authoritative.
    private static MotionParams DriftAfterInput(in MotionState before, in MotionState after, in MotionInput input, in MotionParams parameters, ICollisionView view)
    {
        // Protocol version 50: a step off a floor onto a weightless tile is tg's Moved and the bridge's newtonian_move: a drift from rest
        // along the step, which carries the capsule on while the gate refuses the keys (SpaceGateOf).
        if (before.Driver == MotionDriver.Grounded && !before.Slide.Active && !after.Slide.Active && parameters.CanMove && !parameters.NoClip
            && !parameters.OtherCanMove && parameters.InertiaTilesPerTick > 0f && parameters.DriftX == 0 && parameters.DriftY == 0
            && ((parameters.GravityMask >> 24) & 1) == 0 && WeightlessIn(in parameters, after.TileX, after.TileY))
        {
            double sx = after.X - before.X, sy = after.Y - before.Y;
            double run = Math.Sqrt(sx * sx + sy * sy);
            if (run > 0)
            {
                double inertia = parameters.InertiaTilesPerTick * MotionMath.SubunitsPerTile;
                return parameters with
                {
                    DriftX = (short)Math.Clamp((int)Math.Round(sx / run * inertia), short.MinValue, short.MaxValue),
                    DriftY = (short)Math.Clamp((int)Math.Round(sy / run * inertia), short.MinValue, short.MaxValue),
                };
            }
        }
        if (before.Driver != MotionDriver.ZeroG || before.Slide.Active || after.Slide.Active || parameters.NoClip
            || !parameters.CanMove || (input.MoveX == 0 && input.MoveY == 0)
            || (parameters.DriftX == 0 && parameters.DriftY == 0 && parameters.ImpulseSpeed == 0))
            return parameters;
        // DV534: a step that keeps a handhold in reach pushes off nothing; one that leaves the last behind drifts on
        if (parameters.DriftX == 0 && parameters.DriftY == 0 && parameters.SteersByHandhold && InReach(in after, view))
            return parameters;
        double dx = after.X - before.X, dy = after.Y - before.Y;
        double length = Math.Sqrt(dx * dx + dy * dy);
        if (length == 0)
            return parameters;
        // protocol version 51: a push from rest starts the drift the server said it would; a push while drifting turns it at its speed
        double speed = parameters.DriftX == 0 && parameters.DriftY == 0 ? parameters.ImpulseSpeed
            : Math.Sqrt((double)parameters.DriftX * parameters.DriftX + (double)parameters.DriftY * parameters.DriftY);
        return parameters with
        {
            DriftX = (short)Math.Clamp((int)Math.Round(dx / length * speed), short.MinValue, short.MaxValue),
            DriftY = (short)Math.Clamp((int)Math.Round(dy / length * speed), short.MinValue, short.MaxValue),
        };
    }

    /// <summary>
    /// Lets <paramref name="seconds"/> of render time pass: the visible error decays toward zero, linearly, so that an
    /// error disappears <see cref="DecaySeconds"/> after the correction that made it.
    /// </summary>
    public void Decay(float seconds)
    {
        if (!(seconds > 0f) || (_offsetX == 0f && _offsetY == 0f))
            return;
        float length = MathF.Sqrt(_offsetX * _offsetX + _offsetY * _offsetY);
        float left = length - _decayRate * seconds;
        if (left <= 0f || length == 0f)
        {
            _offsetX = 0f;
            _offsetY = 0f;
            _decayRate = 0f;
            return;
        }
        float k = left / length;
        _offsetX *= k;
        _offsetY *= k;
    }

    /// <summary>
    /// Shows the prediction from where something else drew the capsule (M4c slice 7: the own body leaving zero gravity, drawn until then by
    /// tg's glide): the visible error becomes (<paramref name="dx"/>, <paramref name="dy"/>) tiles and decays as a correction's does
    /// (<see cref="DecaySeconds"/>); more than <see cref="SnapAboveTiles"/> is not shown (the view snaps). Counts nothing.
    /// </summary>
    public void Blend(float dx, float dy)
    {
        float length = MathF.Sqrt(dx * dx + dy * dy);
        if (!(length >= IgnoreBelowTiles) || length > SnapAboveTiles)
        {
            _offsetX = 0f;
            _offsetY = 0f;
            _decayRate = 0f;
            return;
        }
        _offsetX = dx;
        _offsetY = dy;
        _decayRate = length / DecaySeconds;
    }

    /// <summary>Starts over from <paramref name="state"/> (a join, a resynchronizing Snapshot, a teleport the client cannot predict): no pending frames, no visible error; the seq goes on.</summary>
    public void Reset(in MotionState state)
    {
        _predicted = state;
        PredictedParams = default;
        _head = 0;
        _count = 0;
        _intentToggles.Clear();
        _offsetX = 0f;
        _offsetY = 0f;
        _decayRate = 0f;
    }

    private static bool SameBits(in MotionState a, in MotionState b)
        => a.X == b.X && a.Y == b.Y && a.Z == b.Z && a.Yaw == b.Yaw && a.Posture == b.Posture && a.Driver == b.Driver
           && a.Slide == b.Slide && a.PressTimer == b.PressTimer && a.VelX == b.VelX && a.VelY == b.VelY;
}

/// <summary>
/// The client's input clock (scope 2d "Server jitter buffer"): the client sends one frame per server tick of its own
/// clock, and nudges that clock by up to <see cref="MaxNudge"/> (5 %) so that about <see cref="TargetDepth"/> frames wait in
/// the server's jitter buffer (the depth comes back in every Delta's private block). Engine-free; the client and the bots
/// call <see cref="Advance"/> with their frame time and send a frame for every tick it returns.
/// <para>
/// The rate follows a running average of the reported depth (each report moves it a tenth of the way): one frame of
/// average depth above or below the target is half the largest nudge, two frames the whole of it. <see cref="Advance"/>
/// returns at most <see cref="MaxFramesPerAdvance"/> frames (a long hitch drops the rest of the time instead of flooding
/// the server, whose buffer keeps at most 3).
/// </para>
/// </summary>
public sealed class InputClock
{
    /// <summary>The largest nudge of the clock's rate (0.05: 5 % faster or slower).</summary>
    public const double MaxNudge = 0.05;

    /// <summary>The most frames one <see cref="Advance"/> returns.</summary>
    public const int MaxFramesPerAdvance = 4;

    private double _phase;      // ticks accumulated toward the next frame
    private double _averageDepth;
    private double _rate = 1.0;

    /// <summary>A clock that keeps <paramref name="targetDepth"/> frames queued on the server (1 on localhost, 2 on a real network).</summary>
    public InputClock(int targetDepth = 1)
    {
        if (targetDepth < 0) throw new ArgumentOutOfRangeException(nameof(targetDepth), targetDepth, "A queue depth is not negative.");
        TargetDepth = targetDepth;
        _averageDepth = targetDepth;
    }

    /// <summary>The queue depth the clock aims for.</summary>
    public int TargetDepth { get; }

    /// <summary>The current rate: 1 plus or minus at most <see cref="MaxNudge"/>.</summary>
    public double Rate => _rate;

    /// <summary>The server's jitter buffer depth after the tick of the last Delta (the private block).</summary>
    public void ObserveBufferDepth(int depth)
    {
        if (depth < 0) depth = 0;
        _averageDepth += (depth - _averageDepth) * 0.1;
        double nudge = (TargetDepth - _averageDepth) * (MaxNudge / 2);
        _rate = 1.0 + Math.Clamp(nudge, -MaxNudge, MaxNudge);
    }

    /// <summary>Lets <paramref name="seconds"/> of wall time pass; returns how many frames are due now (0, 1, rarely 2).</summary>
    public int Advance(double seconds)
    {
        if (!(seconds > 0)) return 0;
        _phase += seconds * MotionMath.TicksPerSecond * _rate;
        int due = (int)Math.Floor(_phase);
        if (due <= 0) return 0;
        _phase -= due;
        return Math.Min(due, MaxFramesPerAdvance);
    }
}
