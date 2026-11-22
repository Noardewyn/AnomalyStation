using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tests.Gaps.GapsHarness;

namespace Anomaly.Core.Tests.Silicons;

/// <summary>
/// Silicons SI1c (D326): tg's acid. Acid on an item eats its integrity until it melts; the acids' touch splashes a bare head
/// (brute and burn) or eat the hat that covers it; a turf under strong acid crumbles in its stages and collapses; a dead silicon's
/// dust leaves robot remains, which acid dissolves into glowing goo; industrial waste spills a caustic sludge that eats the floor.
/// </summary>
public class AcidTests
{
    [Fact]
    public void TheTypesAreLive()
    {
        foreach (Type type in new[]
        {
            typeof(datum_component_acid), typeof(datum_controller_subsystem_processing_acid), typeof(obj_effect_decal_remains_robot),
            typeof(obj_effect_decal_remains_human), typeof(obj_effect_decal_cleanable_greenglow), typeof(obj_effect_decal_cleanable_greenglow_waste),
            typeof(datum_reagent_toxin_acid), typeof(datum_reagent_toxin_acid_fluacid), typeof(datum_reagent_toxin_acid_nitracid),
            typeof(datum_reagent_toxin_acid_industrial_waste),
        })
            Assert.False(Attribute.IsDefined(type, typeof(DmInertAttribute), inherit: false), type.Name + " is inert");
    }

    [Fact]
    public void AcidEatsAnItemUntilItMelts()
    {
        var r = new AudioRoom(9, 9, seed: 71);
        Feed feed = Listen(r);
        // 100 units at power 100: 31 burn a second while the volume decays by 1 + round(sqrt(volume)) a second (acid.dm:142-148)
        var item = r.In(() => Dm.@new<obj_item_paper>(locate(5, 5, 1)));
        r.Step();
        float before = r.In(() => Dm.num(item.get_integrity()));
        Assert.True(r.In(() => Dm.truthy(Waiting.NoSleep(item.acid_act(100, 100)))));
        var acid = r.In(() => item.GetComponent(typeof(datum_component_acid)) as datum_component_acid);
        Assert.NotNull(acid);
        r.Step(5 * Second);
        Assert.True(r.In(() => QDELETED(item) || Dm.num(item.get_integrity()) < before), "the acid eats the toolbox");
        r.Step(15 * Second);
        Assert.True(r.In(() => QDELETED(item)), "melted");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AcidBurnsABareHeadAndEatsAHat()
    {
        var r = new AudioRoom(9, 9, seed: 72);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(5, 5, "Bob");
        r.Step(Second);
        r.In(() => Waiting.NoSleep(bob.acid_act(10, 50, BODY_ZONE_HEAD)));
        var head = r.In(() => bob.get_bodypart(BODY_ZONE_HEAD) as obj_item_bodypart);
        Assert.True(r.In(() => Dm.num(head.brute_dam) > 0 && Dm.num(head.burn_dam) > 0), "the bare head takes brute and burn");

        var hat = r.In(() =>
        {
            var h = Dm.@new<obj_item_clothing_head_soft>(bob.loc);
            Assert.True(Dm.truthy(Waiting.NoSleep(bob.equip_to_slot_if_possible(h, ITEM_SLOT_HEAD))));
            return h;
        });
        float burn = r.In(() => Dm.num(head.burn_dam));
        r.In(() => Waiting.NoSleep(bob.acid_act(10, 50, BODY_ZONE_HEAD)));
        Assert.Equal(burn, r.In(() => Dm.num(head.burn_dam)));
        Assert.NotNull(r.In(() => hat.GetComponent(typeof(datum_component_acid))));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void StrongAcidCollapsesAFloor()
    {
        var r = new AudioRoom(9, 9, seed: 73);
        Feed feed = Listen(r);
        r.Join(2, 2, "Watcher"); // chat reaches clients in view
        r.In(() => Waiting.NoSleep(((turf)locate(4, 4, 1)).ChangeTurf(typeof(turf_open_floor_iron)))); // the room's floor is indestructible
        turf floor = r.In(() => locate(4, 4, 1));
        Type before = r.In(() => floor.GetType());
        r.In(() => Waiting.NoSleep(floor.acid_act(ACID_POWER_MELT_TURF, 1000)));
        Assert.NotNull(r.In(() => floor.GetComponent(typeof(datum_component_acid))));
        r.Step(40 * Second);
        Assert.True(feed.Any("is holding up against the acid!"));
        Assert.True(feed.Any("collapses under its own weight into a puddle of goop and undigested debris!"));
        Assert.NotEqual(before, r.In(() => locate(4, 4, 1).GetType()));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ADeadSiliconsDustLeavesRemainsThatAcidDissolves()
    {
        var r = new AudioRoom(9, 9, seed: 74);
        Feed feed = Listen(r);
        r.Join(2, 2, "Watcher"); // chat reaches clients in view
        var silicon = r.In(() => Dm.@new<mob_living_silicon>(locate(5, 5, 1)));
        r.Step();
        r.In(() => silicon.spawn_dust());
        var remains = r.In(() => Dm.each<obj_effect_decal_remains_robot>(locate(5, 5, 1).contents).Single());
        Assert.Equal(-6, r.In(() => Dm.num(remains.pixel_z)));
        r.In(() => Waiting.NoSleep(remains.acid_act(10, 10)));
        Assert.True(r.In(() => QDELETED(remains)));
        Assert.Single(r.In(() => Dm.each<obj_effect_decal_cleanable_greenglow>(locate(5, 5, 1).contents).ToList()));
        Assert.True(feed.Any("into a puddle of sizzling goop!"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void IndustrialWasteSpillsASludgeThatEatsTheFloor()
    {
        var r = new AudioRoom(9, 9, seed: 75);
        Feed feed = Listen(r);
        r.Join(2, 2, "Watcher"); // chat reaches clients in view
        // the room's floor is indestructible and lies on space, which would delete the waste with the floor; a station floor lies on plating
        r.In(() => Waiting.NoSleep(((turf)locate(4, 4, 1)).ChangeTurf(typeof(turf_open_floor_iron), Dm.list(typeof(turf_open_floor_plating)))));
        turf floor = r.In(() => locate(4, 4, 1));
        Type before = r.In(() => floor.GetType());
        var waste = r.In(() => Dm.@new<datum_reagent_toxin_acid_industrial_waste>());
        r.In(() => Waiting.NoSleep(waste.expose_turf(floor, 20)));
        var goo = r.In(() => Dm.each<obj_effect_decal_cleanable_greenglow_waste>(floor.contents).Single());
        Assert.Equal(1, r.In(() => Dm.num(goo.active)));
        Assert.True(feed.Any("begins corroding"));
        r.Step(50 * Second);
        Assert.NotEqual(before, r.In(() => locate(4, 4, 1).GetType()));
        Assert.True(feed.Any("The waste eats away at the floor"));
        r.AssertNoRuntimes();
    }
}
