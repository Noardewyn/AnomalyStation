using System.Linq;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Xunit;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// tg's fluid smoke through the real server (effects_smoke.dm): a smoke grenade's factory spreads a cloud the client sees and that blocks its sight (tg's opacity on the
/// cell's flag), bad smoke chokes a human and takes what they hold, and the cloud goes.
/// </summary>
public class SmokeGameplayTests
{
    private static int Seen(RealHeadlessWorld.Player p) => p.Session.Replica.Entities.Count(e => e.TypePath != null && e.TypePath.StartsWith("/obj/effect/particle_effect/fluid/smoke", StringComparison.Ordinal));

    [Fact]
    public void A_cloud_of_bad_smoke_spreads_blinds_chokes_and_goes()
    {
        using var w = new RealHeadlessWorld(strict: false);
        RealHeadlessWorld.Player p = w.Join("Assistant");
        var me = (mob_living_carbon_human)w.MobOf(p);
        w.Run(10);
        int x, y, z;
        obj_item_crowbar crowbar;
        using (w.World.Enter())
        {
            turf at = (turf)me.loc;
            (x, y, z) = ((int)at.x, (int)at.y, (int)at.z);
            crowbar = Dm.@new<obj_item_crowbar>(at);
            Assert.True(Dm.truthy(Waiting.NoSleep(me.put_in_hands(crowbar))));
            Waiting.NoSleep(Dm.@new<datum_effect_system_fluid_spread_smoke_bad>(at, 3).start());
        }

        w.Run(20);
        Assert.True(Seen(p) >= 13, "the client sees the cloud: " + Seen(p));
        Assert.True(p.Session.Replica.IsOpaque(x, y, z), "the smoke blocks sight, as tg's opacity does");

        w.Run(5 * 20);
        using (w.World.Enter())
        {
            Assert.True(crowbar.loc != me, "the smoke took what the human held");
            Assert.True(Dm.num(me.get_oxy_loss()) > 0, "the smoke chokes");
        }

        w.Run(16 * 20);
        Assert.Equal(0, Seen(p));
        Assert.False(p.Session.Replica.IsOpaque(x, y, z), "the view opens when the smoke is gone");
    }
}
