using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M5;

/// <summary>
/// M5 slice 1: tg's /obj/proc/handle_internal_lifeform (objs.dm:141), the breath a mob inside an object takes: a breath's
/// share (BREATH_VOLUME of the volume) of the air the object returns, its turf's; a human shut in a locker keeps breathing.
/// </summary>
public class ObjBreathingTests
{
    private const int Second = 20;

    [Fact]
    public void AnObjectGivesABreathOfItsTurfsAir()
    {
        var r = new AudioRoom(8, 8, seed: 5131);
        var closet = r.In(() => Dm.@new<obj_structure_closet>(locate(4, 4, 1)));
        r.Step();
        r.In(() =>
        {
            var air = (datum_gas_mixture)Waiting.NoSleep(((turf)closet.loc).return_air());
            float total = Dm.num(air.total_moles());
            float expected = total * BREATH_VOLUME / Dm.num(air.return_volume());
            var breath = Waiting.NoSleep(closet.handle_internal_lifeform(null, BREATH_VOLUME)) as datum_gas_mixture;
            Assert.NotNull(breath);
            Assert.Equal(expected, Dm.num(breath.total_moles()), 3);
            Assert.Equal(total - expected, Dm.num(air.total_moles()), 2);
            Assert.Null(Waiting.NoSleep(closet.handle_internal_lifeform(null, 0)));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AHumanShutInALockerKeepsBreathing()
    {
        var r = new AudioRoom(8, 8, seed: 5132);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        var closet = r.In(() => Dm.@new<obj_structure_closet>(locate(4, 4, 1)));
        r.Step(Second);
        Assert.False(r.In(() => Dm.truthy(closet.opened)));
        r.In(() => Waiting.NoSleep(bob.forceMove(closet)));
        r.Step(30 * Second);
        Assert.Same(closet, r.In(() => bob.loc));
        Assert.Equal(0f, r.In(() => Dm.num(bob.get_oxy_loss())));
        r.AssertNoRuntimes();
    }
}
