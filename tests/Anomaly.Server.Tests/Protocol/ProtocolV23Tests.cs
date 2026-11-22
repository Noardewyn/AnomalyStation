using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>
/// Protocol version 23 (DV54, docs/ai/specs/protocol.md section 32): the private block's ghost body. Its bytes, round trips in a
/// Delta and a Snapshot, the clearing, the refusals, and the replica's <see cref="ReplicaWorld.OwnBody"/>.
/// </summary>
public class ProtocolV23Tests
{
    private static OwnBlock Decoded(Delta d)
    {
        Assert.True(Codec.TryDecode(Codec.Encode(d), out Delta back, out ProtocolError error), error.ToString());
        return back.Own;
    }

    [Fact]
    public void TheBodyTravelsOnlyWhenItChangedAndANoneClearsIt()
    {
        Assert.True(ProtocolVersion.Current >= 23);
        Delta d = Msg.Delta(9);
        d.Own.BodyChanged = true;
        d.Own.Body = new MapCell(42, 155, 1);
        OwnBlock own = Decoded(d);
        Assert.True(own.BodyChanged);
        Assert.Equal(new MapCell(42, 155, 1), own.Body);

        d.Own.Body = null; // the ghost re-entered: none
        own = Decoded(d);
        Assert.True(own.BodyChanged);
        Assert.Null(own.Body);

        d.Own.BodyChanged = false;
        d.Own.Body = new MapCell(1, 1, 1); // not changed: not written
        int quiet = Codec.Encode(d).Length;
        own = Decoded(d);
        Assert.False(own.BodyChanged);
        Assert.Null(own.Body);
        d.Own.BodyChanged = true;
        Assert.Equal(quiet + 4, Codec.Encode(d).Length); // the flag byte, then x, y and z of one byte each
    }

    [Fact]
    public void AFlagOtherThanZeroOrOneIsRefused()
    {
        Delta d = Msg.Delta(9);
        d.Own.BodyChanged = true;
        byte[] bytes = Codec.Encode(d);
        bytes[^1] = 2; // the body part is the block's last byte when it carries none
        Assert.False(Codec.TryDecode(bytes, out Delta _, out _));
    }

    [Fact]
    public void TheReplicaKeepsTheBodyUntilAChangeOrASnapshotWithoutIt()
    {
        var replica = new ReplicaWorld();
        replica.Apply(Msg.FlatMap(8, 8));
        Snapshot snapshot = Msg.Snapshot(5, 0, 0, Array.Empty<string>());
        snapshot.Own.BodyChanged = true;
        snapshot.Own.Body = new MapCell(3, 4, 1);
        Assert.True(replica.TryApply(Codec.Encode(snapshot), out _, out ProtocolError error), error.ToString());
        Assert.Equal(new MapCell(3, 4, 1), replica.OwnBody);

        Assert.True(replica.TryApply(Codec.Encode(Msg.Delta(6)), out _, out error), error.ToString());
        Assert.Equal(new MapCell(3, 4, 1), replica.OwnBody); // a Delta without the part keeps it

        Delta moved = Msg.Delta(7);
        moved.Own.BodyChanged = true;
        moved.Own.Body = new MapCell(3, 5, 1);
        Assert.True(replica.TryApply(Codec.Encode(moved), out _, out error), error.ToString());
        Assert.Equal(new MapCell(3, 5, 1), replica.OwnBody);

        Assert.True(replica.TryApply(Codec.Encode(Msg.Snapshot(8, 0, 0, Array.Empty<string>())), out _, out error), error.ToString());
        Assert.Null(replica.OwnBody); // a Snapshot without it: none
    }
}
