using Anomaly.Core.Motion;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Server;

/// <summary>
/// The session's jitter buffer (scope 2d "Server jitter buffer", InputJitterBuffer.cs): each frame kept once in seq order,
/// one taken per tick, the last repeats when none is queued, the oldest are dropped above three, a forged seq refuses the
/// whole message.
/// </summary>
public class InputJitterBufferTests
{
    private static MotionInputMessage Message(params uint[] seqs)
    {
        var message = new MotionInputMessage();
        foreach (uint seq in seqs) message.Frames.Add(new MotionInput(seq, (sbyte)(seq % 100), 0, (ushort)seq, MotionInputFlags.None));
        return message;
    }

    [Fact]
    public void NothingToTakeBeforeTheFirstFrame()
    {
        var buffer = new InputJitterBuffer();
        Assert.False(buffer.TryTake(out MotionInput frame, out bool repeated));
        Assert.Equal(default, frame);
        Assert.False(repeated);
        Assert.Equal((0, 0u, 0u, 0L, 0L), (buffer.Depth, buffer.LastTakenSeq, buffer.NewestSeq, buffer.Dropped, buffer.Repeats));
        Assert.Equal(Limits.MaxJitterBufferFrames, buffer.MaxFrames);
    }

    [Fact]
    public void FramesComeOutOneAtATimeInSeqOrderAndTheLastRepeats()
    {
        var buffer = new InputJitterBuffer();
        Assert.True(buffer.Offer(Message(1, 2)));
        Assert.Equal(2, buffer.Depth);
        Assert.True(buffer.TryTake(out MotionInput a, out bool repeatedA));
        Assert.True(buffer.TryTake(out MotionInput b, out bool repeatedB));
        Assert.Equal((1u, false, 2u, false), (a.Seq, repeatedA, b.Seq, repeatedB));
        Assert.Equal(2u, buffer.LastTakenSeq);
        Assert.Equal(0, buffer.Depth);
        // None queued: the last taken frame repeats, with its seq (so lastInputSeq does not advance).
        Assert.True(buffer.TryTake(out MotionInput c, out bool repeatedC));
        Assert.True(buffer.TryTake(out MotionInput d, out bool repeatedD));
        Assert.Equal((2u, true, 2u, true), (c.Seq, repeatedC, d.Seq, repeatedD));
        Assert.Equal(b, c);
        Assert.Equal(2L, buffer.Repeats);
        Assert.Equal(2u, buffer.LastTakenSeq);
    }

    [Fact]
    public void EveryMessageRepeatsTheLastFramesAndEachIsKeptOnce()
    {
        var buffer = new InputJitterBuffer(maxFrames: 4); // the M2 cap (8 since D331): the drop rule at a small cap
        // The client sends frames n-3..n in every message (scope 2d); three messages deliver 1..6 once each.
        buffer.Offer(Message(1));
        buffer.Offer(Message(1, 2));
        buffer.Offer(Message(1, 2, 3));
        buffer.Offer(Message(1, 2, 3, 4));
        buffer.Offer(Message(2, 3, 4, 5));
        Assert.Equal(5u, buffer.NewestSeq);
        Assert.Equal(5, buffer.Depth);
        var taken = new List<uint>();
        while (buffer.Depth > 0 && buffer.TryTake(out MotionInput f, out bool repeated) && !repeated) taken.Add(f.Seq);
        // Five queued, four kept (the cap): the oldest was dropped at the first take.
        Assert.Equal(new uint[] { 2, 3, 4, 5 }, taken);
        Assert.Equal(1L, buffer.Dropped);
    }

    [Fact]
    public void FramesNotAboveTheNewestOfferedAreIgnoredWhateverTheirOrderOfArrival()
    {
        var buffer = new InputJitterBuffer();
        buffer.Offer(Message(5, 6, 7, 8));
        buffer.Offer(Message(2, 3, 4, 5)); // late: all of it at or below the newest
        Assert.Equal(8u, buffer.NewestSeq);
        Assert.Equal(4, buffer.Depth);
        buffer.Offer(Message(7, 8, 9));
        Assert.Equal(9u, buffer.NewestSeq);
        Assert.Equal(5, buffer.Depth);
    }

    [Fact]
    public void MoreThanFourQueuedDropTheOldestAndNeverDelayInputByMoreThanFourTicks()
    {
        var buffer = new InputJitterBuffer(maxFrames: 4); // the M2 cap (8 since D331): the drop rule at a small cap
        buffer.Offer(Message(1, 2, 3, 4));
        buffer.Offer(Message(5, 6));
        Assert.Equal(6, buffer.Depth);
        Assert.True(buffer.TryTake(out MotionInput frame, out bool repeated));
        Assert.Equal((3u, false), (frame.Seq, repeated)); // 1 and 2 were dropped: four frames stay (3, 4, 5, 6)
        Assert.Equal(2L, buffer.Dropped);
        Assert.Equal(3, buffer.Depth);
        Assert.Equal(3u, buffer.LastTakenSeq);
    }

    [Fact]
    public void ASeqMoreThanAMinuteAheadOfTheLastTakenRefusesTheWholeMessage()
    {
        var buffer = new InputJitterBuffer();
        buffer.Offer(Message(1));
        Assert.True(buffer.TryTake(out _, out _));
        uint limit = 1 + (uint)Limits.MaxInputSeqLead;
        Assert.True(buffer.Offer(Message(limit)));           // exactly at the lead: allowed
        Assert.False(buffer.Offer(Message(limit + 1)));      // one past: refused
        Assert.False(buffer.Offer(Message(2, 3, limit + 5))); // one forged frame refuses the message, and nothing of it stays
        Assert.Equal(limit, buffer.NewestSeq);
        Assert.Equal(1, buffer.Depth);
        // A seq near uint.MaxValue must not wrap into the allowed range.
        Assert.False(new InputJitterBuffer().Offer(Message(uint.MaxValue)));
    }

    [Fact]
    public void ClearDropsTheFramesAndTheRepeatButKeepsTheSeqs()
    {
        var buffer = new InputJitterBuffer();
        buffer.Offer(Message(1, 2, 3));
        buffer.TryTake(out _, out _);
        buffer.Clear();
        Assert.Equal(0, buffer.Depth);
        Assert.False(buffer.TryTake(out _, out _)); // nothing to repeat either
        Assert.Equal((1u, 3u), (buffer.LastTakenSeq, buffer.NewestSeq));
        buffer.Offer(Message(3, 4)); // 3 was offered before: ignored; 4 is new
        Assert.Equal(1, buffer.Depth);
        Assert.True(buffer.TryTake(out MotionInput f, out _));
        Assert.Equal(4u, f.Seq);
    }

    [Fact]
    public void ABufferKeepsAtLeastOneFrame()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new InputJitterBuffer(0));
        var one = new InputJitterBuffer(1);
        one.Offer(Message(1, 2, 3));
        Assert.True(one.TryTake(out MotionInput f, out _));
        Assert.Equal(3u, f.Seq);
    }

    // D331: a repeat that stands in for the next seq takes it (acknowledged), and that seq's frame is ignored when it arrives late
    [Fact]
    public void AStandInTakesTheNextSeqAndItsLateFrameIsIgnored()
    {
        var buffer = new InputJitterBuffer();
        Assert.True(buffer.Offer(Message(1, 2)));
        Assert.True(buffer.TryTake(out MotionInput a, out _));
        Assert.True(buffer.TryTake(out MotionInput b, out _));
        Assert.True(buffer.TryTake(out MotionInput c, out bool repeated, standIn: true));
        Assert.Equal((1u, 2u, 3u, true), (a.Seq, b.Seq, c.Seq, repeated));
        Assert.Equal((b.MoveX, b.MoveY, b.Yaw), (c.MoveX, c.MoveY, c.Yaw));
        Assert.Equal((3u, 1L), (buffer.LastTakenSeq, buffer.StandIns));
        Assert.True(buffer.Offer(Message(3, 4)));
        Assert.True(buffer.TryTake(out MotionInput d, out bool repeatedD));
        Assert.Equal((4u, false), (d.Seq, repeatedD)); // 3 came late: already stood in for
        Assert.True(buffer.TryTake(out MotionInput e, out _)); // a plain repeat keeps its seq
        Assert.Equal((4u, 4u), (e.Seq, buffer.LastTakenSeq));
    }
}
