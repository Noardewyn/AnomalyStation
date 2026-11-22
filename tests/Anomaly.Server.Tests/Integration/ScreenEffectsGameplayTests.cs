using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// D290 (DV222) through the real server: the client reads the own mob's screen effects from what tg sends. Set on fire, tg's
/// <c>fire</c> alert reaches the HUD and the client sees the mob burning; electrocuted, the mob's <c>electrocuted_*</c> overlay shows
/// the shock until tg's timer takes it away.
/// </summary>
public class ScreenEffectsGameplayTests
{
    [Fact]
    public void The_client_sees_its_mob_burning_and_shocked()
    {
        using var w = new RealHeadlessWorld(strict: false);
        RealHeadlessWorld.Player p = w.Join("Assistant");
        var me = (mob_living_carbon_human)w.MobOf(p);
        w.Run(10);
        Assert.Equal(default, ScreenEffects.Read(p.Session.Replica));

        using (w.World.Enter())
        {
            Waiting.NoSleep(me.adjust_fire_stacks(10));
            Assert.True(Dm.truthy(me.ignite_mob()));
        }
        w.Run(10);
        Assert.Equal(1f, ScreenEffects.Read(p.Session.Replica).Burning);

        using (w.World.Enter())
            Waiting.NoSleep(me.electrocution_animation(40));
        w.Run(4);
        Assert.Equal(1f, ScreenEffects.Read(p.Session.Replica).Shock);
        w.Run(120); // tg's 4 s animation (80 ticks) is over
        Assert.Equal(0f, ScreenEffects.Read(p.Session.Replica).Shock);
    }
}
