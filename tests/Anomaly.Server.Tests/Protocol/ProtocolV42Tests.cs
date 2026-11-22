using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>
/// Protocol version 42 (M10 slice B, docs/ai/specs/protocol.md section 51): the Motion message (the capsule mobs' positions on the unreliable
/// channel). Bytes, round trips, the id order rule, and the replica: newer entries apply, older or unknown ones are skipped, and the
/// samples stay in tick order when a Motion runs ahead of a Delta.
/// </summary>
public class ProtocolV42Tests
{
    [Fact]
    public void TheIdFollowsVersion40()
    {
        Assert.True(ProtocolVersion.Current >= 42);
        Assert.Equal(22, (int)MessageId.Motion);
        Assert.True(WireReader.IsKnownMessageId(22));
    }

    [Fact]
    public void MotionHasItsBytesAndRoundTrips()
    {
        var message = new MotionMessage { Tick = 300 };
        message.Entries.Add(new MotionEntry(5, 2 * 256 + 1, 3 * 256, 64));
        message.Entries.Add(new MotionEntry(130, 4 * 256, 4 * 256 + 255, 0));
        byte[] bytes = Codec.Encode(message);
        // id 22, tick 300 (varuint), count 2, then id steps 5 and 125, each with x, y (u16 little endian) and the yaw
        Assert.Equal(new byte[] { 22, 0xAC, 0x02, 2, 5, 0x01, 0x02, 0x00, 0x03, 64, 125, 0x00, 0x04, 0xFF, 0x04, 0 }, bytes);
        Assert.True(Codec.TryDecode(bytes, out MotionMessage back, out ProtocolError error), error.ToString());
        Assert.Equal(300, back.Tick);
        Assert.Equal(message.Entries, back.Entries);
        Assert.True(Codec.TryDecode(Codec.Encode(new MotionMessage { Tick = 1 }), out back, out _));
        Assert.Empty(back.Entries);
    }

    [Fact]
    public void IdsIncreaseAndPositionsAreOnTheMap()
    {
        var unordered = new MotionMessage { Tick = 1 };
        unordered.Entries.Add(new MotionEntry(9, 256, 256, 0));
        unordered.Entries.Add(new MotionEntry(9, 256, 256, 0));
        Assert.Throws<ProtocolException>(() => Codec.Encode(unordered));
        // a zero id step on the wire, a position off the map, a truncated entry
        Assert.False(Codec.TryDecode(new byte[] { 22, 1, 1, 0, 0, 1, 0, 1, 0 }, out MotionMessage _, out _));
        Assert.False(Codec.TryDecode(new byte[] { 22, 1, 1, 3, 0, 0, 0, 1, 0 }, out MotionMessage _, out _));
        Assert.False(Codec.TryDecode(new byte[] { 22, 1, 1, 3, 0, 1, 0 }, out MotionMessage _, out _));
    }

    private static ReplicaWorld ReplicaWithMob(out long id)
    {
        id = 7;
        var replica = new ReplicaWorld();
        replica.Apply(Msg.FlatMap(10, 10));
        replica.Apply(Msg.Snapshot(10, 0, 0, new[] { "/mob/living" }, Msg.State(id, 0, 2, 2) with { HasPos = true }));
        return replica;
    }

    private static MotionMessage Motion(long tick, long id, int x, int y, byte yaw = 0)
    {
        var message = new MotionMessage { Tick = tick };
        message.Entries.Add(new MotionEntry(id, x, y, yaw));
        return message;
    }

    [Fact]
    public void TheReplicaTakesNewerEntriesAndSkipsOlderAndUnknownOnes()
    {
        ReplicaWorld replica = ReplicaWithMob(out long id);
        Assert.True(replica.TryGet(id, out ReplicaEntity e));
        replica.Apply(Motion(12, id, 2 * 256 + 50, 2 * 256, 10));
        Assert.Equal((2 * 256 + 50, (byte)10), (e.State.PosX, e.State.Yaw));
        Assert.Equal(12, replica.MotionTick);
        Assert.Equal(12, replica.NewestTick); // ahead of the Delta's tick
        Assert.Equal(10, replica.Tick);
        Assert.Equal(12, e.Sample(0).Tick);
        Assert.Equal(2 * 256 + 50, e.Sample(0).PosX);

        replica.Apply(Motion(11, id, 2 * 256 + 20, 2 * 256)); // reordered: older than the last entry
        Assert.Equal(2 * 256 + 50, e.State.PosX);
        replica.Apply(Motion(13, 99, 3 * 256, 3 * 256)); // an id the replica does not hold (its spawn is still on the way)
        Assert.False(replica.TryGet(99, out _));

        // The Delta of tick 11 arrives after the Motion of 12: its change lands on the newest sample, which keeps its position.
        replica.Apply(Msg.Delta(11, changed: new[] { new EntityChange { Id = id, Mask = NetField.Dir, Dir = 4 } }));
        Assert.Equal(12, e.Sample(0).Tick);
        Assert.Equal((byte)4, e.Sample(0).Dir);
        Assert.Equal(2 * 256 + 50, e.Sample(0).PosX);
        for (int age = 1; age < e.SampleCount; age++)
            Assert.True(e.Sample(age).Tick < e.Sample(age - 1).Tick, "samples in tick order");
    }
}
