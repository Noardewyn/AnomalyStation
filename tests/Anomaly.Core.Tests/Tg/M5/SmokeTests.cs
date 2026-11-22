using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Core.Tests.Tg.M2Locomotion.T2;
using Xunit;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M5;

/// <summary>tg's fluid smoke (effects_smoke.dm, fluids.dm): it spreads to its group's size at SSsmoke's pace, blocks sight until it fades, and bad smoke makes a human cough and drop things.</summary>
public class SmokeTests
{
    private const int Second = 20;

    private static int Count<T>(T2Room r) where T : class => r.In(() =>
    {
        int n = 0;
        foreach (T _ in Dm.each<T>())
            n++;
        return n;
    });

    [Fact]
    public void SmokeSpreadsToItsSizeThenFadesAndGoes()
    {
        var r = new T2Room(4401);
        r.Step(Second);
        r.In(() => Waiting.NoSleep(do_smoke(2, null, locate(8, 8, 1))));
        Assert.Equal(5, Count<obj_effect_particle_effect_fluid_smoke>(r)); // start() spreads the first node at once (effects_smoke.dm, _fluid_spread.dm:135)
        r.In(() => Assert.True(Dm.truthy(locate<obj_effect_particle_effect_fluid_smoke>(locate(8, 8, 1)).opacity)));

        r.Step(Second);
        int spread = Count<obj_effect_particle_effect_fluid_smoke>(r);
        Assert.InRange(spread, 13, 16); // DIAMOND_AREA(2) = 13 wanted: a node's last neighbours may overshoot, as in tg
        r.In(() => Assert.True(Dm.truthy(((turf)locate(8, 8, 1)).opacity_sources) || Dm.truthy(locate<obj_effect_particle_effect_fluid_smoke>(locate(8, 8, 1)).opacity)));

        r.Step(12 * Second); // 10 s of lifetime, in SSsmoke's 2 s steps, and its second of fading
        Assert.Equal(0, Count<obj_effect_particle_effect_fluid_smoke>(r));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void BadSmokeMakesAHumanDropThingsAndChoke()
    {
        var r = new T2Room(4402);
        r.Step(Second);
        mob_living_carbon_human man = r.In(() => Dm.@new<mob_living_carbon_human>(locate(8, 8, 1)));
        obj_item_crowbar crowbar = r.In(() =>
        {
            obj_item_crowbar c = Dm.@new<obj_item_crowbar>(locate(8, 8, 1));
            Waiting.NoSleep(man.put_in_hands(c));
            return c;
        });
        Assert.True(r.In(() => crowbar.loc == man));

        // not do_smoke(smoke_type = bad): tg's do_smoke then sets the factory's effect_type to its own default, plain smoke (effects_smoke.dm:172)
        r.In(() => Waiting.NoSleep(Dm.@new<datum_effect_system_fluid_spread_smoke_bad>(locate(8, 8, 1), 1).start()));
        r.Step(4 * Second);
        Assert.True(r.In(() => crowbar.loc != man), "the smoke made the human drop what they held");
        Assert.True(r.In(() => Dm.num(man.get_oxy_loss()) > 0), "the smoke chokes");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ChemSmokeCarriesItsReagentsToEveryNode()
    {
        var r = new T2Room(4403);
        r.Step(Second);
        r.In(() => Waiting.NoSleep(do_chem_smoke(1, null, locate(8, 8, 1), typeof(datum_reagent_water), 10)));
        r.Step(Second);
        Assert.True(Count<obj_effect_particle_effect_fluid_smoke_chem>(r) > 1);
        r.In(() =>
        {
            foreach (obj_effect_particle_effect_fluid_smoke_chem node in Dm.each<obj_effect_particle_effect_fluid_smoke_chem>())
                Assert.True(((datum_reagents)node.reagents).total_volume > 0);
        });
        r.AssertNoRuntimes();
    }
}
