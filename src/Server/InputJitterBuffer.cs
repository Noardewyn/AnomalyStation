using Anomaly.Core.Motion;
using Anomaly.Protocol;

namespace Anomaly.Server;

// Owner: M2 P6 (the bodies); the contract is the M2 contracts step's (frozen, docs/ai/specs/m2-ownership.md).

/// <summary>
/// A session's server-side jitter buffer of input frames (scope 2d "Server jitter buffer"): the unreliable Input channel
/// delivers each frame up to <see cref="Limits.MaxInputFramesPerMessage"/> times and in any order; the buffer keeps each
/// frame once, in seq order, and the host takes exactly one per tick for the world (the taken frame becomes a recorded
/// command of that tick, so a replay needs no buffer). Host thread only, no world access (it holds plain values).
/// <para>
/// Rules: a frame whose seq is not above the newest seq offered so far is a repeat and ignored; a seq more than
/// <see cref="Limits.MaxInputSeqLead"/> ahead of the last taken one is refused (the host counts a strike); the frames
/// between the last taken and a newly arrived later one that never arrive are skipped (their ticks repeat the frame
/// before). One frame is taken per tick; with none queued the last taken frame repeats (its seq, so <c>lastInputSeq</c>
/// does not advance) or, D331, stands in for the next seq (it is acknowledged, and that seq's frame is ignored when it arrives
/// late: a player holding the keys sends the same frame again, so the client's prediction already took that step); with more
/// than <see cref="MaxFrames"/> queued the oldest are dropped first, so the buffer never delays input by more than
/// <see cref="MaxFrames"/> ticks.
/// </para>
/// </summary>
public sealed class InputJitterBuffer
{
    private readonly Queue<MotionInput> _queue = new();
    private MotionInput _last;
    private bool _haveLast;

    /// <summary>A buffer that keeps at most <paramref name="maxFrames"/> frames (default <see cref="Limits.MaxJitterBufferFrames"/>).</summary>
    public InputJitterBuffer(int maxFrames = Limits.MaxJitterBufferFrames)
    {
        if (maxFrames < 1) throw new ArgumentOutOfRangeException(nameof(maxFrames), maxFrames, "a jitter buffer keeps at least one frame");
        MaxFrames = maxFrames;
    }

    /// <summary>The most frames kept.</summary>
    public int MaxFrames { get; }

    /// <summary>Frames queued now (what the private block reports as the input buffer depth after the tick).</summary>
    public int Depth => _queue.Count;

    /// <summary>The seq of the last frame taken for a tick (0 before the first).</summary>
    public uint LastTakenSeq { get; private set; }

    /// <summary>The newest seq offered so far.</summary>
    public uint NewestSeq { get; private set; }

    /// <summary>Frames dropped because the buffer was full.</summary>
    public long Dropped { get; private set; }

    /// <summary>Ticks that repeated the last frame because none was queued.</summary>
    public long Repeats { get; private set; }

    /// <summary>D331: the repeats that stood in for the next seq.</summary>
    public long StandIns { get; private set; }

    /// <summary>
    /// Offers the frames of one decoded <see cref="MotionInputMessage"/>: the new ones are queued in seq order. Returns false
    /// when a frame's seq is out of the allowed lead (a forged seq: the host counts a strike and keeps nothing of the message).
    /// </summary>
    public bool Offer(MotionInputMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        // The lead is checked for the whole message first: a forged frame makes the message worthless, so nothing of it stays.
        // The arithmetic is in long: a seq near uint.MaxValue must not wrap.
        long limit = (long)LastTakenSeq + Limits.MaxInputSeqLead;
        foreach (MotionInput frame in message.Frames)
        {
            if (frame.Seq > limit) return false;
        }
        foreach (MotionInput frame in message.Frames)
        {
            if (frame.Seq <= NewestSeq || frame.Seq <= LastTakenSeq) continue; // a repeat (every message carries the last frames again), an old one or one stood in for
            _queue.Enqueue(frame);
            NewestSeq = frame.Seq;
        }
        return true;
    }

    /// <summary>
    /// The frame for this tick: the oldest queued one (after dropping the oldest above <see cref="MaxFrames"/>), else a repeat
    /// of the last taken frame (<paramref name="repeated"/> true), which with <paramref name="standIn"/> takes the next seq (D331).
    /// False before any frame arrived.
    /// </summary>
    public bool TryTake(out MotionInput frame, out bool repeated, bool standIn = false)
    {
        while (_queue.Count > MaxFrames)
        {
            _queue.Dequeue();
            Dropped++;
        }
        if (_queue.Count > 0)
        {
            frame = _queue.Dequeue();
            _last = frame;
            _haveLast = true;
            LastTakenSeq = frame.Seq;
            repeated = false;
            return true;
        }
        if (_haveLast)
        {
            if (standIn && _last.Seq < uint.MaxValue)
            {
                _last = _last with { Seq = _last.Seq + 1 };
                LastTakenSeq = _last.Seq;
                NewestSeq = Math.Max(NewestSeq, _last.Seq);
                StandIns++;
            }
            frame = _last;
            repeated = true;
            Repeats++;
            return true;
        }
        frame = default;
        repeated = false;
        return false;
    }

    /// <summary>Drops everything (a reconnect starts a new buffer anyway; a resynchronization keeps the seqs).</summary>
    public void Clear()
    {
        _queue.Clear();
        _last = default;
        _haveLast = false;
    }
}
