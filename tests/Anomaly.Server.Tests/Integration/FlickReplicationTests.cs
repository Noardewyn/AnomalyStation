using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// Protocol version 8 (D142): BYOND's flick(state, atom) reaches the clients as the entity's Flick field, the atom's appearance with
/// the flicked icon state in its place and the tick it started in.
/// </summary>
public class FlickReplicationTests(ITestOutputHelper output)
{
    [Fact]
    public void A_vent_without_a_pipe_turns_off_and_the_client_sees_its_shutdown_flick()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 7, TimeoutSteps = 400 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        mob_living aliceMob = w.MobOf(alice);
        w.Run(10);
        obj_machinery_atmospherics_components_unary_vent_pump vent;
        using (w.World.Enter())
        {
            // tg's vent_pump/on with no pipe on its tile: update_icon_nopipes (vent_pump.dm:187) sets vent_off and flicks
            // vent_out-shutdown, and its first process_atmos turns it off (vent_pump.dm:245)
            vent = Dm.@new<obj_machinery_atmospherics_components_unary_vent_pump_on>(get_step(aliceMob, 4));
        }
        long flickTick = 0;
        string flicked = null, shown = null;
        w.Run(40, _ =>
        {
            ReplicaEntity e = alice.Session.Replica.Entities.SingleOrDefault(x => x.Id == vent.dm_id);
            if (e == null || e.FlickAppearance.IsNone || e.FlickTick == flickTick) return;
            flickTick = e.FlickTick;
            Assert.True(alice.Session.Replica.TryGetAppearance(e.FlickAppearance, out AppearanceEntry look));
            flicked = alice.Session.Replica.ResolveString(look.IconState);
            Assert.True(alice.Session.Replica.TryGetAppearance(e.State.Appearance, out AppearanceEntry now));
            shown = alice.Session.Replica.ResolveString(now.IconState);
        });
        output.WriteLine($"flick at tick {flickTick}: {flicked} over {shown}");
        using (w.World.Enter())
            Assert.False(Dm.truthy(vent.on));
        Assert.Equal("vent_out-shutdown", flicked);
        Assert.Equal("vent_off", shown);
        Assert.True(flickTick > 0);
    }

    [Fact]
    public void A_flick_on_a_walking_mob_reaches_another_client_while_it_moves()
    {
        // M4v slice 2: the entity layer plays a moving entity's flick (FlickPlayback). tg's flicks on mobs are on mobs not ported yet
        // (secbot.dm:177 "[base_icon_state]-c", mulebot.dm:189), so the test flicks a walking human with BYOND's own proc.
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 7, TimeoutSteps = 400 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        mob_living bobMob = w.MobOf(bob);
        w.Run(10);
        bob.Yaw = -Math.PI / 2;
        bob.Keys = MoveKeys.Forward;
        w.Run(6);
        long serverTick = 0;
        using (w.World.Enter())
        {
            Dm.flick("husk", bobMob);
            serverTick = w.Host.Tick;
        }
        long flickTick = 0;
        string flicked = null;
        var seenAt = new List<(double X, double Y)>();
        w.Run(12, _ =>
        {
            ReplicaEntity e = alice.Session.Replica.Entities.SingleOrDefault(x => x.Id == bobMob.dm_id);
            if (e == null)
                return;
            EntitySample sample = e.Sample(0);
            seenAt.Add((sample.PosX, sample.PosY));
            if (e.FlickAppearance.IsNone || e.FlickTick == flickTick)
                return;
            flickTick = e.FlickTick;
            Assert.True(alice.Session.Replica.TryGetAppearance(e.FlickAppearance, out AppearanceEntry look));
            flicked = alice.Session.Replica.ResolveString(look.IconState);
        });
        bob.Keys = MoveKeys.None;
        output.WriteLine($"flick at tick {flickTick} (server {serverTick}): {flicked}; Bob seen at {seenAt.First()} .. {seenAt.Last()}");
        Assert.Equal("husk", flicked);
        Assert.InRange(flickTick, serverTick, serverTick + 2);
        Assert.NotEqual(seenAt.First(), seenAt.Last()); // Bob walked on while it reached Alice: a moving entity, drawn by the entity layer
        Assert.Equal(0, w.World.RuntimeErrorCount);
    }
}
