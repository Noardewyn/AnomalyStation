using Anomaly.Core.Motion;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared;

// Owner: M2 P8 (client play). Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).

/// <summary>
/// The collision view of the own capsule: the replica's view with the own entity taken out of the capsules it gathers (the
/// controller does not collide with itself, and the replica holds the own entity at the position the server last sent it,
/// which is the past).
/// </summary>
public sealed class OwnExcludingView : ICollisionView
{
    private readonly ICollisionView _inner;
    private readonly Func<long> _ownId;

    public OwnExcludingView(ICollisionView inner, Func<long> ownId)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _ownId = ownId ?? throw new ArgumentNullException(nameof(ownId));
    }

    public bool IsSolid(int x, int y, int z) => _inner.IsSolid(x, y, z);

    public bool IsOutsideMap(int x, int y, int z) => _inner.IsOutsideMap(x, y, z);

    public bool IsClimbWalkable(int x, int y, int z) => _inner.IsClimbWalkable(x, y, z);

    public bool IsFlaps(int x, int y, int z) => _inner.IsFlaps(x, y, z);

    public bool IsHandhold(int x, int y, int z) => _inner.IsHandhold(x, y, z);

    public int LevelShift(int x, int y, int z, byte dir) => _inner.LevelShift(x, y, z, dir);

    public bool OnLift(int x, int y, int z) => _inner.OnLift(x, y, z);

    public bool LadderAt(int x, int y, int z) => _inner.LadderAt(x, y, z);

    public (int Dx, int Dy) TramCarry(int x, int y, int z) => _inner.TramCarry(x, y, z);

    public (float Dx, float Dy) TramDrawShift(int x, int y, int z, float phase) => _inner.TramDrawShift(x, y, z, phase);


    public TileEdges EdgesOf(int x, int y, int z) => _inner.EdgesOf(x, y, z);

    public uint SteppingFrame { set => _inner.SteppingFrame = value; }

    public void GatherCapsules(int x, int y, int z, int range, long exclude, List<CapsuleObstacle> into)
    {
        int first = into.Count;
        _inner.GatherCapsules(x, y, z, range, exclude, into);
        long own = _ownId();
        for (int i = into.Count - 1; i >= first; i--)
        {
            if (into[i].Id == own)
                into.RemoveAt(i);
        }
    }
}

// DV535: a view with one cell open: the drawn tick in progress walking on through a cell its press is guessed to push (the guess itself is
// made only when the frame is pushed).
internal sealed class OpenCellView : ICollisionView
{
    private readonly ICollisionView _inner;
    private readonly int _x, _y, _z;

    public OpenCellView(ICollisionView inner, int x, int y, int z) => (_inner, _x, _y, _z) = (inner, x, y, z);

    public bool IsSolid(int x, int y, int z) => (x != _x || y != _y || z != _z) && _inner.IsSolid(x, y, z);

    public bool IsOutsideMap(int x, int y, int z) => _inner.IsOutsideMap(x, y, z);

    public bool IsClimbWalkable(int x, int y, int z) => _inner.IsClimbWalkable(x, y, z);

    public bool IsFlaps(int x, int y, int z) => _inner.IsFlaps(x, y, z);

    public bool IsHandhold(int x, int y, int z) => _inner.IsHandhold(x, y, z);

    public int LevelShift(int x, int y, int z, byte dir) => _inner.LevelShift(x, y, z, dir);

    public bool OnLift(int x, int y, int z) => _inner.OnLift(x, y, z);

    public bool LadderAt(int x, int y, int z) => _inner.LadderAt(x, y, z);

    public (int Dx, int Dy) TramCarry(int x, int y, int z) => _inner.TramCarry(x, y, z);

    public (float Dx, float Dy) TramDrawShift(int x, int y, int z, float phase) => _inner.TramDrawShift(x, y, z, phase);

    public TileEdges EdgesOf(int x, int y, int z) => _inner.EdgesOf(x, y, z);

    public uint SteppingFrame { set => _inner.SteppingFrame = value; }

    public void GatherCapsules(int x, int y, int z, int range, long exclude, List<CapsuleObstacle> into) => _inner.GatherCapsules(x, y, z, range, exclude, into);
}

/// <summary>
/// Own-capsule prediction for the Godot client (D103, scope 2d): the glue between the engine-free predictor of
/// Anomaly.Core.Motion (<see cref="MotionPredictor"/>, <see cref="InputClock"/>) and the input of a frame loop.
/// <list type="bullet">
/// <item><description><b>One frame per tick.</b> The input clock says how many frames are due; each frame carries the yaw and the
/// time-weighted wish of the tick that just passed (<see cref="WishIntegrator"/>), is stepped through the same controller and view
/// as the server's, and is sent with the three before it (the redundancy of the Input channel).</description></item>
/// <item><description><b>The camera draws more than the last tick.</b> <see cref="RenderPosition"/> is the newest predicted tick, plus the
/// error still being smoothed away, plus a render-only partial step of the tick in progress (the same integral so far, never
/// stored): a key shows on the next frame, and at the tick's end the partial step equals the one the frame makes, so nothing
/// jumps.</description></item>
/// <item><description><b>Reconcile on every Delta's private block</b> (<see cref="OnOwnState"/>): reset to the server's state after the
/// acknowledged frame, replay the later ones, smooth or snap the difference.</description></item>
/// <item><description><b>The input clock</b> is nudged by up to 5 per cent by the server's buffer depth in the same block.</description></item>
/// </list>
/// Single-threaded: the frame loop calls everything.
/// </summary>
public sealed class OwnPrediction
{
    private readonly ICollisionView _view;
    private readonly MotionEvents _renderEvents = new();
    private readonly InputClock _clock;
    private readonly WishIntegrator _wish = new();
    private readonly MotionInput[] _recent = new MotionInput[MotionPredictor.Redundancy];
    private readonly MotionInputMessage _message = new();
    private readonly MotionEvents _events = new();
    private MotionPredictor _predictor;
    private MotionParams _params = MotionParams.DefaultRun;
    private double _phase;
    private ushort _yaw;
    private (float East, float North) _wishNow;
    private Reconciliation _last;
    private bool _hasLast;
    private long _framesSent;

    /// <param name="view">The collision view of the own capsule (<see cref="OwnExcludingView"/> over the replica's).</param>
    /// <param name="targetDepth">The frames the server should keep queued (1 on localhost, 2 on a real network).</param>
    /// <param name="bodyId">The own entity now (the replica's): a private block of another body starts the prediction over (DV358: climbing into an exosuit).</param>
    public OwnPrediction(ICollisionView view, int targetDepth = 1, Func<long> bodyId = null)
    {
        _view = view ?? throw new ArgumentNullException(nameof(view));
        _clock = new InputClock(targetDepth);
        _bodyId = bodyId;
    }

    private readonly Func<long> _bodyId;
    private long _body;

    /// <summary>True once the server has told the own state: frames are predicted and sent from then on.</summary>
    public bool Active => _predictor != null;

    /// <summary>The newest predicted state (after the last frame pushed).</summary>
    public MotionState Predicted => _predictor.Predicted;

    /// <summary>The parameters the predictor steps with (the newest the server sent).</summary>
    public MotionParams Params => _params;

    /// <summary>The seq of the newest frame pushed (0: none yet): what an action of this moment is tagged with.</summary>
    public uint NewestSeq => _predictor == null ? 0 : _predictor.NextSeq - 1;

    /// <summary>
    /// A walk/run toggle for the next frame (the server applies the action before it steps that frame): the predictor steps that frame and the
    /// later ones at the other intent's speed. Returns the seq to tag the action with.
    /// </summary>
    public uint ToggleIntent()
    {
        if (_predictor == null)
            return 0;
        _predictor.ToggleIntentAt(_predictor.NextSeq);
        return _predictor.NextSeq;
    }

    /// <summary>The guess of tg's push the predictor asks for each press (<see cref="MotionPredictor.Pushes"/>; <see cref="OwnGuesses.OnPush"/>).</summary>
    public PushGuess Pushes
    {
        get => _pushes;
        set
        {
            _pushes = value;
            if (_predictor != null)
                _predictor.Pushes = value;
        }
    }

    private PushGuess _pushes;

    /// <summary>DV535: whether a press of a frame not pushed yet would be guessed pushing (<see cref="OwnGuesses.WouldPush"/>): the drawn tick in progress walks on through it.</summary>
    public WouldPushGuess WouldPush { get; set; }

    public delegate bool WouldPushGuess(in MotionEvent press, int z, uint frame);

    /// <summary>Frames sent and not yet acknowledged.</summary>
    public int Pending => _predictor?.Pending ?? 0;

    /// <summary>Frames pushed so far.</summary>
    public long FramesSent => _framesSent;

    /// <summary>The counters of the reconciliations (corrections, snaps, the largest error).</summary>
    public PredictionCounters Counters => _predictor?.Counters ?? default;

    /// <summary>The visible error still being smoothed away, in tiles.</summary>
    public (float X, float Y) VisualOffset => _predictor?.VisualOffset ?? default;

    /// <summary>The input clock's rate, 0.95 to 1.05.</summary>
    public double ClockRate => _clock.Rate;

    /// <summary>The server's jitter buffer depth the clock last saw.</summary>
    public int BufferDepth { get; private set; }

    /// <summary>The last reconciliation, when there was one.</summary>
    public bool TryGetLastReconciliation(out Reconciliation reconciliation)
    {
        reconciliation = _last;
        return _hasLast;
    }

    /// <summary>Raised after a reconciliation that corrected something (the camera shakes at a shove).</summary>
    public event Action<Reconciliation> Corrected;

    /// <summary>M10 slice E: the newest frame's capsule pressed into a blocked face (the server's bridge bumps what is there in the same frame): the event and the frame's seq.</summary>
    public event Action<MotionEvent, uint> Pressed;

    /// <summary>
    /// A private block arrived (<c>ReplicaWorld.OwnStateReceived</c>): the first one with a capsule starts the predictor from
    /// the server's state, every later one reconciles.
    /// </summary>
    public void OnOwnState(OwnState own)
    {
        _clock.ObserveBufferDepth(own.InputBufferDepth);
        BufferDepth = own.InputBufferDepth;
        if (!own.HasMotion)
        {
            // D151: the own mob is no capsule (a ghost): tg's keyLoop and client/Move tile steps move it on the server. Frames
            // still go out each tick with the wish (BYOND's held keys); nothing is predicted, the frames step no capsule here
            // (no speed), and the acknowledged ones are dropped. The seq goes on from the body's frames.
            if (_predictor == null)
                _predictor = new MotionPredictor(default) { Pushes = _pushes };
            else
                _predictor.Reset(_predictor.Predicted);
            _params = default;
            TileDriven = true;
            return;
        }
        _params = own.Params;
        long body = _bodyId?.Invoke() ?? 0;
        if (_predictor != null && !TileDriven && body != _body)
        {
            // Another capsule is the body now (a pilot in its exosuit, out of it): nothing of the old one's frames predicts it
            _body = body;
            _predictor.Reset(own.Motion); // the view's yaw stays the player's: the exosuit turns to it
            return;
        }
        _body = body;
        if (TileDriven)
        {
            // Back in a capsule (Re-enter Corpse): the prediction starts again from the server's state, the seq going on.
            TileDriven = false;
            _predictor.Reset(own.Motion);
            _yaw = own.Motion.Yaw;
            return;
        }
        if (_predictor == null)
        {
            _predictor = new MotionPredictor(own.Motion) { Pushes = _pushes };
            _yaw = own.Motion.Yaw;
            return;
        }
        // DV69: a weightless capsule is predicted as any other (its push-offs, its jetpack and its drift are the controller's).
        _last = _predictor.Reconcile(own.Motion, own.AckedSeq, own.Params, _view);
        _params = _predictor.PredictedParams;
        _hasLast = true;
        if (_last.Kind != CorrectionKind.None)
            Corrected?.Invoke(_last);
    }

    /// <summary>
    /// The own capsule is weightless (<see cref="MotionDriver.ZeroG"/>, M4c): the camera's zero-g effects (D70). DV69: it is predicted as any
    /// capsule, its drift included.
    /// </summary>
    public bool Weightless => _predictor != null && !TileDriven && _predictor.Predicted.Driver == MotionDriver.ZeroG;

    /// <summary>
    /// One rendered frame. <paramref name="keys"/> are the held movement keys (view-relative) and <paramref name="godotYaw"/>
    /// the view's yaw (Godot's convention) for this frame; <paramref name="send"/> gets the message of every frame that came
    /// due (usually none or one).
    /// <para>
    /// The frame is cut at the tick's boundary: the part of this frame's time before it belongs to the tick that ends, the
    /// rest to the next one, so each frame carries exactly one tick's worth of the keys' time (the wish is analog). The phase
    /// inside the tick follows the input clock's rate; the clock itself says when a frame is due.
    /// </para>
    /// </summary>
    public void Frame(double dtSeconds, MoveKeys keys, double godotYaw, Action<MotionInputMessage> send)
    {
        if (_predictor == null || dtSeconds < 0)
            return;
        _yaw = MoveWish.CompassYaw(godotYaw);
        _wishNow = MoveWish.FromKeys(keys, godotYaw);
        _predictor.Decay((float)dtSeconds);
        double tick = TickSeconds;
        int due = _clock.Advance(dtSeconds);
        if (due <= 0)
        {
            _wish.Add(_wishNow.East, _wishNow.North, dtSeconds);
            _phase = Math.Min(_phase + dtSeconds / tick, 1.0);
            return;
        }
        // The part of the frame before the boundary completes the tick that ends; each further due tick is a whole one.
        double toBoundary = Math.Clamp((1.0 - _phase) * tick, 0.0, dtSeconds);
        _wish.Add(_wishNow.East, _wishNow.North, toBoundary);
        for (int i = 0; i < due; i++)
        {
            (sbyte east, sbyte north) = i == 0 ? _wish.Take(tick) : MoveWishNow();
            Push(east, north, send);
        }
        double leftover = Math.Max(0.0, dtSeconds - toBoundary - (due - 1) * tick);
        _wish.Reset();
        _wish.Add(_wishNow.East, _wishNow.North, leftover);
        _phase = Math.Min(leftover / tick, 1.0);
    }

    private void Push(sbyte east, sbyte north, Action<MotionInputMessage> send)
    {
        _events.Clear();
        MotionInput frame = _predictor.Push(east, north, _yaw, MotionInputFlags.None, _params, _view, _events);
        _params = _predictor.PredictedParams;
        for (int i = 0; i < _events.Count; i++)
            if (_events[i].Kind == MotionEventKind.Pressing)
                Pressed?.Invoke(_events[i], frame.Seq);
        _framesSent++;
        int count = _predictor.CopyRecentFrames(_recent, MotionPredictor.Redundancy);
        _message.Frames.Clear();
        for (int f = 0; f < count; f++)
            _message.Frames.Add(_recent[f]);
        send?.Invoke(_message);
    }

    private (sbyte East, sbyte North) MoveWishNow() => (MoveWish.ToWire(_wishNow.East), MoveWish.ToWire(_wishNow.North));

    /// <summary>One tick at the input clock's rate, in seconds.</summary>
    private double TickSeconds => 1.0 / (MotionMath.TicksPerSecond * Math.Max(_clock.Rate, 0.5));

    /// <summary>
    /// Where the own capsule is drawn, in tiles (BYOND coordinates with a fraction), and its level: the newest predicted
    /// state, plus the visible error still decaying, plus the partial step of the tick in progress.
    /// </summary>
    public (float X, float Y, int Z) RenderPosition()
    {
        (float x, float y, int z) = RenderPositionOffTram();
        // DEVIATION(DV403): on a moving tram, the share of its next step the tick in progress has reached (the step lands with the next frame)
        MotionState predicted = _predictor.Predicted;
        (float tx, float ty) = _view.TramDrawShift(predicted.TileX, predicted.TileY, predicted.Z, (float)Math.Clamp(_phase, 0.0, 1.0));
        return (x + tx, y + ty, z);
    }

    /// <summary>The newest tick in progress as the own capsule is drawn (<see cref="NewestSeq"/>'s tick plus this share of the next one).</summary>
    public double Phase => Math.Clamp(_phase, 0.0, 1.0);

    // DEVIATION(DV535): the tick in progress is drawn as the next frame will step it: a push guessed in the newest frame makes the pushed cell
    // passable from the next one, which the newest frame's view did not show, so the pusher was drawn standing at the crate for a tick; and a
    // press of the next frame that will push walks on through the pushed cell, as the predictor will step it
    private void StepAhead(ref MotionState state, MotionInput input, in MotionParams here)
    {
        MotionState before = state;
        uint frame = NewestSeq + 1;
        _renderEvents.Clear();
        _view.SteppingFrame = frame;
        MotionStep.Step(ref state, input, here, _view, _renderEvents);
        for (int i = 0; WouldPush != null && i < _renderEvents.Count; i++)
        {
            MotionEvent press = _renderEvents[i];
            if (press.Kind != MotionEventKind.Pressing || !WouldPush(in press, state.Z, frame))
                continue;
            if (MotionStep.WalksOnAfterPush(in state, _renderEvents, 0))
            {
                PushingAhead = (press.ToX, press.ToY, state.Z, press.ToX - press.FromX, press.ToY - press.FromY);
                MotionStep.StepAgain(ref state, in before, in input, in here, new OpenCellView(_view, press.ToX, press.ToY, state.Z), _renderEvents);
            }
            break;
        }
        _view.SteppingFrame = NewestSeq;
    }

    /// <summary>DV535: the cell the drawn tick in progress walks on through, guessed pushed one cell along (Dx, Dy) by the frame to come; null when none.</summary>
    public (int X, int Y, int Z, int Dx, int Dy)? PushingAhead { get; private set; }

    private (float X, float Y, int Z) RenderPositionOffTram()
    {
        PushingAhead = null;
        MotionState state = _predictor.Predicted;
        (sbyte east, sbyte north) = _wish.Peek(TickSeconds);
        if (state.Slide.Active)
        {
            // A slide (a shove) plays the part of its next share that the tick in progress has reached, whatever the keys (M4c slice 7: a
            // held key stepped it a whole share ahead, a jump of a tick each time a key went down or up).
            float share = (float)Math.Clamp(_phase, 0.0, 1.0) / state.Slide.TicksLeft;
            (float vx, float vy) = _predictor.VisualOffset;
            return (MotionMath.ToTiles(state.X) + MotionMath.ToTiles(state.Slide.TargetX - state.X) * share + vx,
                MotionMath.ToTiles(state.Y) + MotionMath.ToTiles(state.Slide.TargetY - state.Y) * share + vy, state.Z);
        }
        MotionParams here = MotionPredictor.InGravityOf(ref state, in _params);
        if (here.Feel.Ramps && here.CanMove && state.Driver is MotionDriver.Grounded or MotionDriver.Prone)
        {
            // DV263: the ramped velocity is no partial wish, so draw the share of the tick's whole step that has passed, the step taken with
            // the wish the frame will carry if the keys stay as they are; at the tick's end it is the step the frame makes.
            double phase = Math.Clamp(_phase, 0.0, 1.0);
            (float soFarE, float soFarN) = _wish.Average(TickSeconds);
            float wholeE = soFarE + _wishNow.East * (float)(1.0 - phase), wholeN = soFarN + _wishNow.North * (float)(1.0 - phase);
            MotionState next = state;
            _renderEvents.Clear();
            StepAhead(ref next, new MotionInput(0, MoveWish.ToWire(wholeE), MoveWish.ToWire(wholeN), _yaw, MotionInputFlags.None), in here);
            MotionPredictor.ApplyLevelShift(ref next, _renderEvents, 0, here, _view);
            (float rx, float ry) = _predictor.VisualOffset;
            float x = MotionMath.ToTiles(state.X) + MotionMath.ToTiles(next.X - state.X) * (float)phase;
            float y = MotionMath.ToTiles(state.Y) + MotionMath.ToTiles(next.Y - state.Y) * (float)phase;
            // D325, DV370: the drawn point past the top stair is on the level above, as the tick's end will put it (no drop under the flight)
            bool crossed = (int)MathF.Round(x) != state.TileX || (int)MathF.Round(y) != state.TileY;
            return (x + rx, y + ry, crossed ? next.Z : state.Z);
        }
        if (here.CanMove && (east != 0 || north != 0))
        {
            var input = new MotionInput(0, east, north, _yaw, MotionInputFlags.None);
            _renderEvents.Clear();
            StepAhead(ref state, input, in here);
            MotionPredictor.ApplyLevelShift(ref state, _renderEvents, 0, here, _view);
        }
        else if (state.Driver == MotionDriver.ZeroG && (here.DriftX != 0 || here.DriftY != 0))
        {
            // DV69: the drift's share of the tick in progress
            float share = (float)Math.Clamp(_phase, 0.0, 1.0);
            MotionParams part = here with { DriftX = (short)MathF.Round(here.DriftX * share), DriftY = (short)MathF.Round(here.DriftY * share) };
            MotionStep.Step(ref state, new MotionInput(0, 0, 0, _yaw, MotionInputFlags.None), part, _view, null);
        }
        (float ox, float oy) = _predictor.VisualOffset;
        return (MotionMath.ToTiles(state.X) + ox, MotionMath.ToTiles(state.Y) + oy, state.Z);
    }

    /// <summary>The view's yaw as the last frame carried it (a compass bearing).</summary>
    public ushort Yaw => _yaw;

    /// <summary>
    /// D151: the own mob has no capsule (a ghost): frames carry the wish to tg's keyLoop on the server, nothing is predicted, and
    /// the mob is drawn from the replica (<see cref="RenderPosition"/> means nothing then).
    /// </summary>
    public bool TileDriven { get; private set; }

    /// <summary>Starts over (a new connection).</summary>
    public void Reset()
    {
        _predictor = null;
        _phase = 0;
        _wish.Reset();
        _hasLast = false;
        _framesSent = 0;
        TileDriven = false;
    }
}
