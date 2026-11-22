using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tests.M5;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tests.Gaps.GapsHarness;

namespace Anomaly.Core.Tests.Syndicate;

/// <summary>
/// Syndicate stage N1b (D316): the pressure tank of the syndicate base (atmospherics/machinery/components/tank.dm with the gas leaker, the
/// volatile gas storage, crackable and cracked) in a booted toy room. A nitrogen tank fills to half its safe pressure; two side by side
/// merge into one storage of twice the volume; a wrench opens the port on the user's side; damage cracks it (one crack per tenth of the
/// integrity lost) and it leaks its gas onto its tile.
/// </summary>
public class PressureTankTests
{
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(11, 9, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(4, 5, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        r.Step(Second);
        return (r, feed, bob);
    }

    private static float Moles(AudioRoom r, datum_gas_mixture mix, Type gas) => r.In(() => Dm.num(mix.moles[gas]));

    [Fact]
    public void TheTankAndItsPiecesAreLive()
    {
        foreach (Type type in new[]
        {
            typeof(obj_machinery_atmospherics_components_tank), typeof(obj_machinery_atmospherics_components_tank_nitrogen),
            typeof(obj_machinery_atmospherics_components_tank_air), typeof(obj_structure_tank_frame), typeof(obj_effect_abstract_tank_glass),
            typeof(obj_effect_abstract_tank_gas_holder), typeof(datum_component_gas_leaker), typeof(datum_element_volatile_gas_storage),
            typeof(datum_element_crackable), typeof(datum_component_cracked),
        })
            Assert.False(Attribute.IsDefined(type, typeof(DmInertAttribute), inherit: false), type.Name + " is inert");
    }

    [Fact]
    public void ANitrogenTankFillsMergesOpensAPortCracksAndLeaks()
    {
        var (r, feed, bob) = Room(1491);
        var tank = r.In(() => Dm.@new<obj_machinery_atmospherics_components_tank_nitrogen>(locate(5, 5, 1)));
        r.Step(Second);

        // half its 46 MPa of nitrogen at room temperature in its 2500 litres; its window and glass in its vis_contents
        float pressure = r.In(() => Dm.num(tank.air_contents.return_pressure()));
        Assert.InRange(pressure, 22999f, 23001f);
        Assert.True(Moles(r, tank.air_contents, typeof(datum_gas_nitrogen)) > 0);
        Assert.Contains(tank.window_glass, Dm.each<object>(tank.vis_contents));
        Assert.Contains(tank.gas_holder, Dm.each<object>(tank.vis_contents));
        string examined = r.In(() => string.Join(" ", Dm.each(Waiting.NoSleep(tank.examine(bob)) as DmList).Select(Dm.str)));
        Assert.Contains("A pipe port can be opened with a <b>wrench</b>.", examined);
        Assert.Contains("maximum safe pressure", examined);

        // a second tank beside it: one storage of both volumes
        var second = r.In(() => Dm.@new<obj_machinery_atmospherics_components_tank_nitrogen>(locate(6, 5, 1)));
        r.Step(Second);
        Assert.Same(tank.air_contents, second.air_contents);
        Assert.Equal(5000f, r.In(() => Dm.num(tank.air_contents.volume)));
        Assert.InRange(r.In(() => Dm.num(tank.air_contents.return_pressure())), 22999f, 23001f);

        // Bob west of it wrenches the west port open
        r.In(() =>
        {
            var wrench = Dm.@new<obj_item_wrench>(bob.loc);
            Waiting.NoSleep(bob.put_in_active_hand(wrench));
        });
        r.Step(2);
        FurnitureTests.Click(r, bob, tank, FurnitureTests.Left, 4);
        Assert.Equal((float)WEST, r.In(() => Dm.num(Dm.bitand(tank.initialize_directions, WEST))));
        examined = r.In(() => string.Join(" ", Dm.each(Waiting.NoSleep(tank.examine(bob)) as DmList).Select(Dm.str)));
        Assert.Contains("The pipe port can be moved or closed with a <b>wrench</b>.", examined);

        // half its integrity gone: five cracks, and the nitrogen leaks onto its tile
        var tile = r.In(() => (turf)tank.loc);
        float before = Moles(r, r.In(() => (datum_gas_mixture)Waiting.NoSleep(tile.return_air())), typeof(datum_gas_nitrogen));
        r.In(() => Waiting.NoSleep(tank.take_damage(400, BRUTE, sound_effect: FALSE)));
        r.Step(4 * Second);
        var cracked = r.In(() => tank.GetComponent(typeof(datum_component_cracked)) as datum_component_cracked);
        Assert.NotNull(cracked);
        Assert.Equal(5, r.In(() => cracked.applied_cracks.len));
        Assert.Equal(5, r.In(() => Dm.each<DmList>(tank.filter_data).Count(filter => Dm.str(filter["name"]).StartsWith("*cracked_overlay_", StringComparison.Ordinal))));
        float after = Moles(r, r.In(() => (datum_gas_mixture)Waiting.NoSleep(tile.return_air())), typeof(datum_gas_nitrogen));
        Assert.True(after > before + 1, $"the tile's nitrogen {before} -> {after}");
        Assert.True(r.In(() => Dm.num(tank.air_contents.return_pressure())) < pressure);
        r.AssertNoRuntimes();
    }
}
