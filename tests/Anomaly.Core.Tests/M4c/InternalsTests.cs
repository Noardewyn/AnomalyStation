using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M4c;

/// <summary>
/// M4c slice 1: tg's gas tanks and internals (tanks.dm, tank_types.dm, carbon and human inventory.dm, carbon life.dm's
/// get_breath_from_internal, the breath mask). A human with a breath mask and an oxygen tank breathes from the tank in a
/// vacuum without oxygen damage, each breath takes tg's moles at the tank's release pressure, closing the valve or taking
/// the mask off (or pushing it down) cuts the tank off, alt-click on the tank toggles it, an empty tank gives FALSE, and the
/// valve plays internals_on and internals_off. The tank dispenser hands out tanks by hand.
/// </summary>
public class InternalsTests
{
    private const int Second = 20;
    private const string On = "sound/items/internals/internals_on.ogg";
    private const string Off = "sound/items/internals/internals_off.ogg";

    private static (AudioRoom r, mob_living_carbon_human bob, obj_item_clothing_mask_breath mask, T tank) Suited<T>(ulong seed, bool mask = true) where T : obj_item_tank
    {
        var r = new AudioRoom(9, 9, seed: seed);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.Step(Second);
        obj_item_clothing_mask_breath worn = null;
        T tank = r.In(() =>
        {
            if (mask)
            {
                worn = Dm.@new<obj_item_clothing_mask_breath>(bob.loc);
                Assert.True(Dm.truthy(Waiting.NoSleep(bob.equip_to_slot_if_possible(worn, ITEM_SLOT_MASK))), "the mask goes on");
            }
            T t = Dm.@new<T>(bob.loc);
            Assert.True(Dm.truthy(Waiting.NoSleep(bob.put_in_hands(t))), "the tank is in a hand");
            return t;
        });
        return (r, bob, worn, tank);
    }

    private static void AltClick(AudioRoom r, mob who, atom target)
    {
        r.Command(() => ClientSeam.Click(who.client, target, "alt=1;icon-x=16;icon-y=16"));
        r.Step(Second); // past the click cooldown
    }

    private static float Moles(AudioRoom r, obj_item_tank tank) => r.In(() => Dm.num(tank.air_contents.total_moles()));

    private static float Oxy(AudioRoom r, mob_living who) => r.In(() => Dm.num(who.get_oxy_loss()));

    [Fact]
    public void ATankIsFilledByItsTypeAtInitialize()
    {
        var r = new AudioRoom(5, 5, seed: 7301);
        r.In(() =>
        {
            var o2 = Dm.@new<obj_item_tank_internals_oxygen>(locate(2, 2, 1));
            var emergency = Dm.@new<obj_item_tank_internals_emergency_oxygen>(locate(2, 2, 1));
            var empty = Dm.@new<obj_item_tank_internals_emergency_oxygen_empty>(locate(2, 2, 1));
            // tank_types.dm:38 and :183: (n * ONE_ATMOSPHERE) * volume / (R_IDEAL_GAS_EQUATION * T20C)
            Assert.Equal(6 * ONE_ATMOSPHERE * 70 / (R_IDEAL_GAS_EQUATION * T20C), Dm.num(o2.air_contents.moles[typeof(datum_gas_oxygen)]), 3);
            Assert.Equal(10 * ONE_ATMOSPHERE * 3 / (R_IDEAL_GAS_EQUATION * T20C), Dm.num(emergency.air_contents.moles[typeof(datum_gas_oxygen)]), 4);
            Assert.Equal(0f, Dm.num(empty.air_contents.total_moles()));
            Assert.Equal(T20C, o2.air_contents.temperature);
            Assert.InRange(Dm.num(o2.air_contents.return_pressure()), 6 * ONE_ATMOSPHERE - 1, 6 * ONE_ATMOSPHERE + 1);
            Assert.NotNull(o2.GetComponent(typeof(datum_component_atmos_reaction_recorder)));
            Assert.NotNull(o2.GetComponent(typeof(datum_component_container_item_tank_holder)));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AltClickOnTheTankOpensTheValveWithTheSoundAndAgainClosesIt()
    {
        var (r, bob, _, tank) = Suited<obj_item_tank_internals_oxygen>(7302);
        r.Sounds.Heard.Clear();

        AltClick(r, bob, tank);
        Assert.Same(tank, r.In(() => bob.@internal));
        Assert.Same(bob, r.In(() => tank.breathing_mob));
        HeardSound on = Assert.Single(r.Sounds.Of(bob, On));
        Assert.True(on.Volume > 0, "the valve is heard");

        AltClick(r, bob, tank);
        Assert.Null(r.In(() => bob.@internal));
        Assert.Null(r.In(() => tank.breathing_mob));
        Assert.Single(r.Sounds.Of(bob, Off));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void WithoutAMaskTheValveDoesNotOpen()
    {
        var (r, bob, _, tank) = Suited<obj_item_tank_internals_oxygen>(7303, mask: false);
        r.Sounds.Heard.Clear();
        AltClick(r, bob, tank);
        Assert.Null(r.In(() => bob.@internal));
        Assert.Empty(r.Sounds.Of(bob, On));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AHumanOnInternalsBreathesInAVacuumAndTheTankEmptiesByTgsBreath()
    {
        var (r, bob, _, tank) = Suited<obj_item_tank_internals_emergency_oxygen>(7304);
        AltClick(r, bob, tank);
        Assert.Same(tank, r.In(() => bob.@internal));

        // one breath by hand: tg's moles at the release pressure (tanks.dm:320), BREATH_VOLUME litres at the tank's temperature
        float before = Moles(r, tank);
        float expected = TANK_DEFAULT_RELEASE_PRESSURE * BREATH_VOLUME / (R_IDEAL_GAS_EQUATION * T20C);
        var breath = r.In(() => Waiting.NoSleep(bob.get_breath_from_internal(BREATH_VOLUME))) as datum_gas_mixture;
        Assert.NotNull(breath);
        Assert.Equal(expected, r.In(() => Dm.num(breath.moles[typeof(datum_gas_oxygen)])), 4);
        Assert.Equal(before - expected, Moles(r, tank), 4);

        // then the vacuum: Life breathes from the tank and no oxygen damage comes
        r.In(() => DevCommands.Gas(AIRLESS_ATMOS));
        float atBreach = Moles(r, tank);
        r.Step(40 * Second);
        Assert.Equal(0f, Oxy(r, bob));
        Assert.Same(tank, r.In(() => bob.@internal));
        float used = atBreach - Moles(r, tank);
        Assert.True(used >= 3 * expected, $"the tank gave several breaths: {used} moles");
        float breaths = used / expected;
        Assert.InRange(breaths - MathF.Round(breaths), -0.05f, 0.05f); // whole breaths of tg's size
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ClosingTheValveInAVacuumSuffocates()
    {
        var (r, bob, _, tank) = Suited<obj_item_tank_internals_oxygen>(7305);
        AltClick(r, bob, tank);
        r.In(() => DevCommands.Gas(AIRLESS_ATMOS));
        r.Step(20 * Second);
        Assert.Equal(0f, Oxy(r, bob));
        AltClick(r, bob, tank);
        Assert.Null(r.In(() => bob.@internal));
        r.Step(20 * Second);
        Assert.True(Oxy(r, bob) > 0f, "no tank, no air");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TakingTheMaskOffCutsTheTankOffAtTheNextBreath()
    {
        var (r, bob, mask, tank) = Suited<obj_item_tank_internals_oxygen>(7306);
        AltClick(r, bob, tank);
        r.Sounds.Heard.Clear();
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(mask)));
        Assert.True(r.In(() => Dm.truthy(bob.invalid_internals())));
        r.Step(10 * Second); // a breath comes within four SSmobs fires
        Assert.Null(r.In(() => bob.@internal));
        Assert.Null(r.In(() => tank.breathing_mob));
        Assert.Single(r.Sounds.Of(bob, Off));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void PushingTheMaskDownCutsTheTankOffAndTogglingAgainPullsItUp()
    {
        var (r, bob, mask, tank) = Suited<obj_item_tank_internals_oxygen>(7307);
        AltClick(r, bob, tank);
        Assert.Same(tank, r.In(() => bob.@internal));

        // alt-click on the worn mask (breath.dm:30 -> adjust_visor): up, the internals flag off, the tank cut off (clothing.dm:587)
        AltClick(r, bob, mask);
        Assert.True(r.In(() => Dm.truthy(mask.up)));
        Assert.Equal("breath_up", r.In(() => mask.icon_state));
        Assert.Null(r.In(() => bob.@internal));

        // the tank again: human toggle_internals pushes the mask back into place and opens the valve (human/inventory.dm:316)
        AltClick(r, bob, tank);
        Assert.False(r.In(() => Dm.truthy(mask.up)));
        Assert.Equal("breath", r.In(() => mask.icon_state));
        Assert.Same(tank, r.In(() => bob.@internal));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AnEmptyTankGivesFalseNotNoBreath()
    {
        var (r, bob, _, tank) = Suited<obj_item_tank_internals_emergency_oxygen_empty>(7308);
        AltClick(r, bob, tank);
        Assert.Same(tank, r.In(() => bob.@internal));
        object breath = r.In(() => Waiting.NoSleep(bob.get_breath_from_internal(BREATH_VOLUME)));
        Assert.NotNull(breath);
        Assert.Equal(0f, Dm.num(breath));
        // without a tank there is no internal breath at all
        AltClick(r, bob, tank);
        Assert.Null(r.In(() => Waiting.NoSleep(bob.get_breath_from_internal(BREATH_VOLUME))));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void GivingTheOpenTankToSomeoneElseClosesIt()
    {
        var (r, bob, _, tank) = Suited<obj_item_tank_internals_oxygen>(7309);
        AltClick(r, bob, tank);
        mob_living_carbon_human alice = r.Dummy(5, 4);
        r.In(() =>
        {
            Waiting.NoSleep(bob.dropItemToGround(tank));
            Assert.Null(bob.@internal); // tanks.dm:80: dropped away from its breather
            Assert.True(Dm.truthy(Waiting.NoSleep(alice.put_in_hands(tank))));
            Assert.Null(tank.breathing_mob);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheTankDispenserHandsOutTanksByHand()
    {
        var r = new AudioRoom(9, 9, seed: 7310);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        var dispenser = r.In(() => Dm.@new<obj_structure_tank_dispenser>(locate(5, 4, 1)));
        r.Step(Second);
        Assert.Contains("oxygen-4", r.In(() => Dm.each(dispenser.overlays).Cast<IDmAppearance>().Select(a => a.icon_state).ToList()));

        // right click: an oxygen tank (tank_dispenser.dm:47)
        r.Command(() => ClientSeam.Click(bob.client, dispenser, "right=1;icon-x=16;icon-y=16"));
        r.Step(3);
        Assert.IsType<obj_item_tank_internals_oxygen>(r.In(() => bob.get_active_held_item()));
        Assert.Equal(9f, r.In(() => dispenser.oxygentanks));

        // put it back with a click (item_interaction): the count goes up again
        r.Command(() => ClientSeam.Click(bob.client, dispenser, "icon-x=16;icon-y=16"));
        r.Step(3);
        Assert.Null(r.In(() => bob.get_active_held_item()));
        Assert.Equal(10f, r.In(() => dispenser.oxygentanks));

        // left click with an empty hand: a plasma tank, the one put back stays inside
        r.Command(() => ClientSeam.Click(bob.client, dispenser, "icon-x=16;icon-y=16"));
        r.Step(3);
        Assert.IsType<obj_item_tank_internals_plasma>(r.In(() => bob.get_active_held_item()));
        Assert.Equal(9f, r.In(() => dispenser.plasmatanks));
        r.AssertNoRuntimes();
    }
}
