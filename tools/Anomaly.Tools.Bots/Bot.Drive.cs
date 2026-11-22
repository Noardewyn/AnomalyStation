using Anomaly.Core.Motion;
using Anomaly.Net;
using Anomaly.Protocol;

namespace Anomaly.Tools.Bots;

// M2 P6: the bot's acting under protocol version 2. Every update a walking bot sends ONE MotionInput message on the Input
// channel carrying its newest up to Limits.MaxInputFramesPerMessage frames (oldest first, consecutive seqs, the newest
// last), and its discrete actions as Action messages on the Control channel, tagged with the seq of the newest frame sent.
// The decisions are BotDriver's; this file builds its views from the replica and sends what it decides.

public sealed partial class Bot
{
    private readonly MotionInput[] _ring = new MotionInput[Limits.MaxInputFramesPerMessage];
    private readonly MotionInputMessage _message = new();
    private readonly List<PlannedAction> _planned = new();
    private OtherBody[] _others = new OtherBody[64];
    private int _ringCount;
    private uint _frameSeq;
    private MotionPredictor _predictor;
    private ICollisionView _view;
    private InputClock _clock;
    private PredictionCounters _seenCounters;
    private int _actionFloodKind;

    /// <summary>The seq of the last input frame sent on this connection (0: none).</summary>
    public uint InputSeq => _frameSeq;

    /// <summary>The bot's own capsule predictor, null unless <see cref="BotOptions.Predict"/> is on and the first private block arrived on this connection.</summary>
    public MotionPredictor Predictor => _predictor;

    /// <summary>The bot's input clock (a predicting bot's), null otherwise.</summary>
    public InputClock Clock => _clock;

    private void ResetMotion()
    {
        _frameSeq = 0;
        _ringCount = 0;
        _predictor = null;
        _view = null;
        _clock = null;
        _seenCounters = default;
        _actionFloodKind = 0;
        _planned.Clear();
    }

    // ---- acting ----

    private void ActConnecting()
    {
        if (Options.Behaviour != BotBehaviour.PreWelcomeCommand || !_transportConnected || _leave != Leave.None) return;
        for (int i = 0; i < Options.PreWelcomeFramesPerUpdate && _preWelcomeSent < Options.PreWelcomeFramesMax; i++)
        {
            PushFrame(i % 2 == 0 ? new BotWish(MotionInput.MaxMove, 0, 0) : default);
            SendRing();
            _preWelcomeSent++;
        }
    }

    private void ActJoined()
    {
        if (_leave != Leave.None) return;

        if (UpdateCount % RttSamplePeriod == 0) Stats.AddRtt(Transport.RoundTripMs);

        if (!BotBehaviourNames.IsAbuse(Options.Behaviour) && ActInLobby()) return;

        long sinceJoin = UpdateCount - _joinedAt;
        switch (Options.Behaviour)
        {
            case BotBehaviour.Idle:
                break;

            case BotBehaviour.Churn:
                Drive();
                if (UpdateCount >= _churnAt) BeginLeave(Leave.Churn);
                break;

            case BotBehaviour.Flood:
                for (int i = 0; i < Options.FloodFramesPerUpdate; i++)
                {
                    PushFrame((i & 1) == 0 ? new BotWish(MotionInput.MaxMove, 0, 0) : default);
                    SendRing();
                }
                break;

            case BotBehaviour.Malformed:
                SendRaw(AbusePayloads.Garbage(_random));
                break;

            case BotBehaviour.Oversized:
                SendRaw(AbusePayloads.Oversized(_random));
                break;

            case BotBehaviour.ActionFlood:
                PushFrame(default);
                SendRing();
                for (int i = 0; i < Options.ActionFloodPerUpdate; i++)
                {
                    // The cheap actions in turn; every one is legal, only the rate is wrong.
                    ActionKind kind = (_actionFloodKind++ % 3) switch
                    {
                        0 => ActionKind.Rest,
                        1 => ActionKind.ToggleMoveIntent,
                        _ => ActionKind.ToggleCombatMode,
                    };
                    SendAction(new ActionMessage(_frameSeq, kind, 0, ClickButton.None, ClickModifiers.None, null));
                }
                break;

            case BotBehaviour.ForgedSeq:
                if (sinceJoin <= Options.ForgedSeqWarmupUpdates)
                {
                    PushFrame(default);
                    SendRing();
                }
                else
                {
                    SendRaw(AbusePayloads.ForgedSeq(_frameSeq, _random));
                }
                break;

            case BotBehaviour.BadAction:
                PushFrame(default);
                SendRing();
                if (sinceJoin % 10 == 0) SendEncodedAction(AbusePayloads.MissingTargetClick(_frameSeq, _random), ActionKind.Click);
                if (sinceJoin % 7 == 3) SendRaw(AbusePayloads.BadAction(_random));
                break;

            case BotBehaviour.DevAbuse:
                PushFrame(default);
                SendRing();
                if (sinceJoin % 10 == 0) SendEncodedAction(AbusePayloads.DevCommandAtCap(_frameSeq), ActionKind.DevCommand);
                if (sinceJoin % 10 == 5) SendRaw(AbusePayloads.DevCommandOverCap(_frameSeq, _random));
                break;

            case BotBehaviour.Fiddle:
                Drive();
                Fiddle();
                break;

            case BotBehaviour.ForgedTarget:
                PushFrame(default);
                SendRing();
                if (sinceJoin % 4 == 0) ForgeTarget();
                break;

            case BotBehaviour.HoverFlood:
                PushFrame(default);
                SendRing();
                if (sinceJoin % 4 != 3) HoverSomething(); // 15 a second, over the hover rate and under the overall one
                break;

            case BotBehaviour.BadV4:
                PushFrame(default);
                SendRing();
                if (sinceJoin % 25 == 0) SendRaw(AbusePayloads.BadV4Action(_frameSeq, _random));
                break;

            default:
                // Wander, Rest, Shove, Mixed (a Mixed bot runs the behaviour its index resolved to).
                Drive();
                break;
        }
    }

    /// <summary>One tick of a walking bot: the driver's decision, one frame (or two, or none, for a predicting bot's clock), the actions.</summary>
    private void Drive()
    {
        if (Options.Predict && _predictor == null && !TryStartPredictor()) return;

        OwnBody own = BuildOwnBody();
        int others = _driver.Behaviour == BotBehaviour.Shove ? GatherOthers(own.Id) : 0;
        _planned.Clear();
        _routeMap?.Refresh(UpdateCount);
        BotWish wish = _driver.Decide(UpdateCount, own, _others.AsSpan(0, others), _solidity, _planned);
        Stats.RestsStarted = _driver.Rest.LieDowns;
        if (_driver.Behaviour == BotBehaviour.Tour)
        {
            Stats.TourReplans = _driver.Tour.Replans;
            Stats.TourStuckEvents = _driver.Tour.StuckEvents;
            Stats.TourStuck = _driver.Tour.IsStuck;
            Stats.TourDoorPressTicks = _driver.Tour.DoorPressTicks;
        }

        if (_predictor == null)
        {
            PushFrame(wish);
            SendRing();
        }
        else
        {
            int due = _clock.Advance(1.0 / UpdatesPerSecond);
            OwnState state = Replica.Own;
            for (int i = 0; i < due; i++)
            {
                MotionInput frame = _predictor.Push(wish.MoveX, wish.MoveY, wish.Yaw, MotionInputFlags.None, state.Params, _view);
                _frameSeq = frame.Seq;
                Stats.FramesSent++;
            }
            if (due > 0) SendPredictorFrames();
        }

        for (int i = 0; i < _planned.Count; i++)
        {
            PlannedAction action = _planned[i];
            if (action.Kind == ActionKind.Click && action.Button == ClickButton.Right) Stats.ShovesSent++;
            SendAction(new ActionMessage(_frameSeq, action.Kind, action.Target, action.Button, ClickModifiers.None, null));
        }
    }

    // ---- what the bot sees ----

    private OwnBody BuildOwnBody()
    {
        long id = Replica.OwnEntity;
        if (id == 0 || !Replica.TryGet(id, out ReplicaEntity entity)) return default;

        EntityState state = entity.State;
        int x, y;
        if (_predictor != null)
        {
            // A predicting bot walks by where it sees itself: its own prediction, a few ticks ahead of the Deltas.
            MotionState predicted = _predictor.Predicted;
            x = predicted.X;
            y = predicted.Y;
        }
        else if (entity.HasPos)
        {
            x = state.PosX;
            y = state.PosY;
        }
        else
        {
            x = state.X * MotionMath.SubunitsPerTile;
            y = state.Y * MotionMath.SubunitsPerTile;
        }
        return new OwnBody(true, id, x, y, state.Z, PostureBits.IsLying(state.Posture));
    }

    /// <summary>Fills <see cref="_others"/> with the other capsule mobs of the replica; returns how many.</summary>
    private int GatherOthers(long ownId)
    {
        int n = 0;
        foreach (ReplicaEntity entity in Replica.Entities)
        {
            if (entity.State.Id == ownId || !entity.HasPos) continue;
            if (n == _others.Length) Array.Resize(ref _others, n * 2);
            EntityState state = entity.State;
            _others[n++] = new OtherBody(state.Id, state.PosX, state.PosY, PostureBits.IsLying(state.Posture));
        }
        return n;
    }

    // ---- prediction ----

    private bool TryStartPredictor()
    {
        OwnState own = Replica.Own;
        if (!own.HasMotion) return false;
        _predictor = new MotionPredictor(own.Motion);
        _view = new SelfExcludingView(Replica.CollisionView, Replica.OwnEntity);
        _clock = new InputClock(Math.Max(0, Options.InputClockDepth));
        _seenCounters = default;
        return true;
    }

    /// <summary>After an applied Delta: resets the prediction to the server's state after the acknowledged frame, replays the rest and compares.</summary>
    private void ReconcilePrediction()
    {
        if (_predictor == null) return;
        OwnState own = Replica.Own;
        if (!own.HasMotion) return;
        _clock.ObserveBufferDepth(own.InputBufferDepth);
        _predictor.Reconcile(own.Motion, own.AckedSeq, own.Params, _view);
        Stats.AddPrediction(ref _seenCounters, _predictor.Counters);
    }

    // ---- sending ----

    /// <summary>Gives the wish the next seq and puts it in the ring of the newest frames (a bot that does not predict).</summary>
    private void PushFrame(BotWish wish)
    {
        var frame = new MotionInput(++_frameSeq, wish.MoveX, wish.MoveY, wish.Yaw, MotionInputFlags.None);
        if (_ringCount == _ring.Length)
        {
            Array.Copy(_ring, 1, _ring, 0, _ring.Length - 1);
            _ring[_ring.Length - 1] = frame;
        }
        else
        {
            _ring[_ringCount++] = frame;
        }
        Stats.FramesSent++;
    }

    /// <summary>Sends the ring: one MotionInput message with the newest frames, oldest first.</summary>
    private void SendRing()
    {
        _message.Frames.Clear();
        for (int i = 0; i < _ringCount; i++) _message.Frames.Add(_ring[i]);
        SendMotionMessage();
    }

    private void SendPredictorFrames()
    {
        Span<MotionInput> recent = stackalloc MotionInput[Limits.MaxInputFramesPerMessage];
        int n = _predictor.CopyRecentFrames(recent, Limits.MaxInputFramesPerMessage);
        _message.Frames.Clear();
        for (int i = 0; i < n; i++) _message.Frames.Add(recent[i]);
        SendMotionMessage();
    }

    private void SendMotionMessage()
    {
        Span<byte> buffer = stackalloc byte[Limits.MaxMotionInputMessageBytes];
        int length = Codec.Encode(_message, buffer);
        Transport.Send(Channel.Input, buffer.Slice(0, length));
        Stats.BytesOut += length;
        Stats.MessagesOut++;
        Stats.InputsSent++;
    }

    private void SendAction(in ActionMessage action)
    {
        Span<byte> buffer = stackalloc byte[Limits.MaxActionMessageBytes];
        int length = Codec.Encode(action, buffer);
        Transport.Send(Channel.Control, buffer.Slice(0, length));
        CountAction(action.Kind, length);
    }

    private void SendEncodedAction(byte[] message, ActionKind kind)
    {
        Transport.Send(Channel.Control, message);
        CountAction(kind, message.Length);
    }

    private void CountAction(ActionKind kind, int length)
    {
        Stats.BytesOut += length;
        Stats.MessagesOut++;
        int index = (int)kind;
        if (index >= 0 && index < Stats.ActionsByKind.Length) Stats.ActionsByKind[index]++;
    }

    private void SendRaw(byte[] message)
    {
        Transport.Send(AbusePayloads.ChannelFor(message), message);
        Stats.BytesOut += message.Length;
        Stats.MessagesOut++;
        Stats.AbuseSent++;
    }
}

/// <summary>
/// The replica's collision view with the bot's own entity left out. The predictor steps the capsule without knowing which
/// entity is its own (<c>MotionStep.Step</c> gets no <c>selfId</c> from it), and the replica's view lists every capsule it
/// holds, the own one at its newest received position, a tick or two behind the prediction: without this wrapper the
/// capsule collides with its own ghost whenever it turns back, and the prediction is wrong by up to a step.
/// </summary>
internal sealed class SelfExcludingView : ICollisionView
{
    private readonly ICollisionView _inner;
    private readonly long _ownId;

    public SelfExcludingView(ICollisionView inner, long ownId)
    {
        _inner = inner;
        _ownId = ownId;
    }

    public bool IsSolid(int x, int y, int z) => _inner.IsSolid(x, y, z);

    public bool IsFlaps(int x, int y, int z) => _inner.IsFlaps(x, y, z);

    public bool LadderAt(int x, int y, int z) => _inner.LadderAt(x, y, z);

    public uint SteppingFrame { set => _inner.SteppingFrame = value; } // DV403: the tram ride is read at the frame's tick

    public (int Dx, int Dy) TramCarry(int x, int y, int z) => _inner.TramCarry(x, y, z);

    public (float Dx, float Dy) TramDrawShift(int x, int y, int z, float phase) => _inner.TramDrawShift(x, y, z, phase);

    public TileEdges EdgesOf(int x, int y, int z) => _inner.EdgesOf(x, y, z);

    public void GatherCapsules(int x, int y, int z, int range, long exclude, List<CapsuleObstacle> into)
        => _inner.GatherCapsules(x, y, z, range, exclude == 0 ? _ownId : exclude, into);
}
