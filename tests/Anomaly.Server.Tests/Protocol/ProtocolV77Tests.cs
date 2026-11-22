using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>Protocol version 77 (DV371): an entity's announced level ride in a spawn and a change, its end on either side of the message's tick.</summary>
public class ProtocolV77Tests
{
    [Fact]
    public void ARideRoundTripsInASpawnAndAChangeAndIsChecked()
    {
        var up = new RideState(1, EndTick: 70, Ticks: 60);
        Snapshot s = Msg.Snapshot(10, 0, 0, new[] { "/obj/structure/transport/linear" }, Msg.State(4, 0, 2, 2) with { Ride = up });
        Assert.True(Codec.TryDecode(Codec.Encode(s), out Snapshot back, out ProtocolError error), error.ToString());
        Assert.Equal(up, back.Entities[0].Ride);

        // a ride whose end the message's tick has passed (a late timer) and one that ends
        foreach (RideState ride in new[] { new RideState(-1, EndTick: 9, Ticks: 20), RideState.None })
        {
            Delta d = Msg.Delta(11, changed: new[] { new EntityChange { Id = 4, Mask = NetField.Ride, Ride = ride } });
            Assert.True(Codec.TryDecode(Codec.Encode(d), out Delta changed, out error), error.ToString());
            Msg.AssertEqual(d, changed);
        }
        foreach (RideState bad in new[] { new RideState(2, 20, 5), new RideState(1, 20, 0) })
            Assert.Throws<ProtocolException>(() => Codec.Encode(Msg.Delta(11, changed: new[] { new EntityChange { Id = 4, Mask = NetField.Ride, Ride = bad } })));
    }
}
