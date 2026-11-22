using Anomaly.Core.Motion;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>
/// Protocol version 81 (DV416): an entity's free motion (a throw's arc, a fall, a slide, a rest point) in a spawn and a change, checked;
/// the replica draws the segment under way at its render time, the one a newer segment replaced until that newer one starts.
/// </summary>
public class ProtocolV81Tests
{
    private static readonly FlightArc Arc = new(9.25, 3f, 8f, 1, 16f, 0.4f, -0.1f, 0.36f, 0.392f);

    [Fact]
    public void AFlightRoundTripsInASpawnAndAChangeAndIsChecked()
    {
        Snapshot s = Msg.Snapshot(10, 0, 0, new[] { "/obj/item" }, Msg.State(4, 0, 3, 8) with { Flight = Arc });
        Assert.True(Codec.TryDecode(Codec.Encode(s), out Snapshot back, out ProtocolError error), error.ToString());
        Assert.Equal(Arc, back.Entities[0].Flight);

        foreach (FlightArc arc in new[] { FlightArc.Rest(10.5, 5.8f, 8f, 1, 12f), FlightArc.None, Arc with { Friction = 0.006f, Gravity = 0f } })
        {
            Delta d = Msg.Delta(11, changed: new[] { new EntityChange { Id = 4, Mask = NetField.Flight, Flight = arc } });
            Assert.True(Codec.TryDecode(Codec.Encode(d), out Delta changed, out error), error.ToString());
            Msg.AssertEqual(d, changed);
        }
        foreach (FlightArc bad in new[] { Arc with { Gravity = -1f }, Arc with { VX = float.NaN }, Arc with { X0 = 1e6f } })
            Assert.Throws<ProtocolException>(() => Codec.Encode(Msg.Delta(11, changed: new[] { new EntityChange { Id = 4, Mask = NetField.Flight, Flight = bad } })));
    }

    [Fact]
    public void AThrowActionCarriesAClicksTargetZoneAndAimAndIsAPlainLeftClick()
    {
        var aim = new ShotAim(800, 2048, 1300, 16384, -2000);
        var thrown = new ActionMessage(7, ActionKind.Throw, 42, ClickButton.Left, ClickModifiers.None, null) { TargetKind = TargetKind.Entity, IconX = 12, IconY = 20, AimZone = AimZone.Head, Aim = aim };
        Assert.True(Codec.TryDecode(Codec.Encode(thrown), out ActionMessage back, out ProtocolError error), error.ToString());
        Assert.Equal(thrown, back);
        var atFloor = new ActionMessage(8, ActionKind.Throw, 0, ClickButton.Left, ClickModifiers.None, null) { TargetKind = TargetKind.Turf, TurfX = 4, TurfY = 4, TurfZ = 2, Aim = aim };
        Assert.True(Codec.TryDecode(Codec.Encode(atFloor), out back, out error), error.ToString());
        Assert.Equal(atFloor, back);
        Assert.Throws<ProtocolException>(() => Codec.Encode(thrown with { Button = ClickButton.Right }));
        Assert.Throws<ProtocolException>(() => Codec.Encode(thrown with { Modifiers = ClickModifiers.Shift }));
    }

    [Fact]
    public void TheReplicaDrawsTheSegmentUnderWayAtTheRenderTime()
    {
        var world = new ReplicaWorld();
        world.Apply(Msg.FlatMap(16, 16));
        world.Apply(Msg.Snapshot(10, 0, 0, new[] { "/obj/item" }, Msg.State(4, 0, 3, 8) with { Flight = Arc }));
        // a hit at 11.4 in tick 12: it falls from there
        FlightArc fall = new(11.4, 3.9f, 7.85f, 1, 17f, 0f, 0f, 0f, 0.392f);
        world.Apply(Msg.Delta(11));
        world.Apply(Msg.Delta(12, changed: new[] { new EntityChange { Id = 4, Mask = NetField.Flight, Flight = fall } }));
        Assert.True(world.TryGet(4, out ReplicaEntity e));
        Assert.Equal(Arc, e.FlightAt(11.0)); // drawn behind the server: the throw still flies
        Assert.Equal(fall, e.FlightAt(11.4));
        Assert.Equal(fall, e.FlightAt(12.0));
        Assert.Equal(Arc, e.FlightAt(9.0)); // before the first segment it is held at its start (it came into the world with it)
        // it ends: none at once
        world.Apply(Msg.Delta(13, changed: new[] { new EntityChange { Id = 4, Mask = NetField.Flight, Flight = FlightArc.None } }));
        Assert.Equal(FlightArc.None, e.FlightAt(12.5));
    }

    [Fact]
    public void AnArcIsAClosedFormItsFrictionStopsAndItsRestPointStays()
    {
        (double x, double y, double lift) = Arc.At(Arc.StartTick + 10);
        Assert.Equal(3 + 4, x, 4);
        Assert.Equal(8 - 1, y, 4);
        Assert.Equal(16 + 3.6 - 0.5 * 0.392 * 100, lift, 3);
        var slide = new FlightArc(0, 4f, 8f, 1, 0f, 0.1f, 0f, 0f, 0f, 0.01f);
        Assert.Equal(10, slide.StopTicks, 6);
        Assert.Equal(4 + 0.5, slide.At(10).X, 6); // v^2 / 2f
        Assert.Equal(slide.At(10).X, slide.At(50).X, 9); // stopped
        Assert.Equal(0, slide.VelocityAt(20).VX);
        FlightArc rest = FlightArc.Rest(3, 5.5f, 2f, 1, 12f);
        Assert.False(rest.IsMoving);
        Assert.Equal((5.5, 2.0, 12.0), rest.At(100));
    }
}
