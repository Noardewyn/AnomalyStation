using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>
/// Protocol version 29 (the eye blur, docs/ai/specs/protocol.md section 38): the private block's game plane, the appearance of tg's game plane
/// master controller with its filters. Its bytes, the round trip, a none, the refusal of a reference outside its table, and the replica's
/// <see cref="ReplicaWorld.GamePlane"/> and <see cref="ReplicaWorld.GamePlaneFilters"/>.
/// </summary>
public class ProtocolV29Tests
{
    private static AppearanceEntry Blurred(float size) => new() { Filters = new List<FilterEntry> { new() { Kind = FilterKind.Blur, Size = size } } };

    private static OwnBlock Decoded(Delta d)
    {
        Assert.True(Codec.TryDecode(Codec.Encode(d), out Delta back, out ProtocolError error), error.ToString());
        return back.Own;
    }

    [Fact]
    public void TheVersionIsTwentyNine() => Assert.True(ProtocolVersion.Current >= 29);

    [Fact]
    public void TheGamePlaneTravelsOnlyWhenItChangedAndANoneClearsIt()
    {
        Delta d = Msg.Delta(9);
        d.Own.GamePlaneChanged = true;
        d.Own.GamePlane = new TableRef(3, true);
        OwnBlock own = Decoded(d);
        Assert.True(own.GamePlaneChanged);
        Assert.Equal(new TableRef(3, true), own.GamePlane);

        d.Own.GamePlane = TableRef.None; // the mob lost its hud
        own = Decoded(d);
        Assert.True(own.GamePlaneChanged);
        Assert.True(own.GamePlane.IsNone);

        d.Own.GamePlaneChanged = false;
        d.Own.GamePlane = new TableRef(5, true); // not changed: not written
        int quiet = Codec.Encode(d).Length;
        own = Decoded(d);
        Assert.False(own.GamePlaneChanged);
        d.Own.GamePlaneChanged = true;
        byte[] bytes = Codec.Encode(d);
        Assert.Equal(quiet + 1, bytes.Length); // (5 << 1) | 1 = 11, one varuint byte
        Assert.Equal(11, bytes[^1]);
    }

    [Fact]
    public void APrivateReferenceToNothingIsRefused()
    {
        Delta d = Msg.Delta(9);
        d.Own.GamePlaneChanged = true;
        d.Own.GamePlane = new TableRef(0, true);
        Assert.Throws<ProtocolException>(() => Codec.Encode(d));
    }

    [Fact]
    public void TheReplicaReadsThePlanesFiltersAndRefusesAnAppearanceItDoesNotHave()
    {
        var replica = new ReplicaWorld();
        replica.Apply(Msg.FlatMap(8, 8));
        Snapshot snapshot = Msg.Snapshot(5, 0, 0, Array.Empty<string>());
        snapshot.Own = new OwnBlock { FirstNewPrivateAppearance = 1, GamePlaneChanged = true, GamePlane = new TableRef(1, true) };
        snapshot.Own.NewPrivateAppearances.Add(Blurred(1f));
        Assert.True(replica.TryApply(Codec.Encode(snapshot), out _, out ProtocolError error), error.ToString());
        Assert.Equal(new TableRef(1, true), replica.GamePlane);
        Assert.Equal(FilterKind.Blur, Assert.Single(replica.GamePlaneFilters).Kind);
        Assert.Equal(1f, replica.GamePlaneFilters[0].Size);

        Assert.True(replica.TryApply(Codec.Encode(Msg.Delta(6)), out _, out error), error.ToString());
        Assert.Equal(new TableRef(1, true), replica.GamePlane); // a Delta without the part keeps it

        Delta cleared = Msg.Delta(7);
        cleared.Own.FirstNewPrivateAppearance = 2;
        cleared.Own.NewPrivateAppearances.Add(new AppearanceEntry()); // the blur ended: the controller without filters
        cleared.Own.GamePlaneChanged = true;
        cleared.Own.GamePlane = new TableRef(2, true);
        Assert.True(replica.TryApply(Codec.Encode(cleared), out _, out error), error.ToString());
        Assert.Empty(replica.GamePlaneFilters);

        Delta outside = Msg.Delta(8);
        outside.Own.GamePlaneChanged = true;
        outside.Own.GamePlane = new TableRef(40, true);
        Assert.ThrowsAny<Exception>(() => replica.TryApply(Codec.Encode(outside), out _, out _));
    }

    [Fact]
    public void ASnapshotWithoutTheGamePlaneClearsIt()
    {
        var replica = new ReplicaWorld();
        replica.Apply(Msg.FlatMap(8, 8));
        Snapshot first = Msg.Snapshot(5, 0, 0, Array.Empty<string>());
        first.Own = new OwnBlock { FirstNewPrivateAppearance = 1, GamePlaneChanged = true, GamePlane = new TableRef(1, true) };
        first.Own.NewPrivateAppearances.Add(Blurred(3f));
        replica.Apply(first);
        Assert.NotEmpty(replica.GamePlaneFilters);
        replica.Apply(Msg.Snapshot(9, 0, 0, Array.Empty<string>()));
        Assert.True(replica.GamePlane.IsNone);
        Assert.Empty(replica.GamePlaneFilters);
    }
}
