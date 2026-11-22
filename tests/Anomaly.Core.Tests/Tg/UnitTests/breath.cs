// Translated from tgstation (snapshot 1d99bd7f): code/modules/unit_tests/breath.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.UnitTests;

// PARTIAL (M8 slice D): the human case (breath_sanity); the plasmaman and ashwalker cases stay out (other species).
// ADAPTED: tg runs it on the unit test map; the room is the toy room (TgToyRoom.Install).

/// Tests to ensure humans, plasmamen, and ashwalkers can breath in normal situations.
/// Ensures algorithmic correctness of the "breathe()" and "toggle_internals()" procs.
// tg: code/modules/unit_tests/breath.dm:17
public sealed class datum_unit_test_breath_breath_sanity : TgUnitTest
{
    public override void New()
    {
        base.New();
        TgToyRoom.Install(this);
    }

    /// Equips the given Human with a new instance of the given tank type and a breathing mask.
    /// Returns the new equipped tank.
    // tg: code/modules/unit_tests/breath.dm:10
    private async DmTask<obj_item_tank_internals> equip_labrat_internals(mob_living_carbon_human lab_rat, Type tank_type)
    {
        var mask = allocate<obj_item_clothing_mask_breath>();
        var source = allocate(tank_type) as obj_item_tank_internals;
        await lab_rat.equip_to_slot_if_possible(mask, ITEM_SLOT_MASK);
        await lab_rat.equip_to_slot_if_possible(source, ITEM_SLOT_HANDS);
        return source;
    }

    // tg: code/modules/unit_tests/breath.dm:17
    public override async DmTask Run()
    {
        // Breathing from turf.
        mob_living_carbon_human lab_rat = allocate<mob_living_carbon_human_consistent>();
        await lab_rat.forceMove(run_loc_floor_bottom_left as atom);
        turf_open to_fill = run_loc_floor_bottom_left as turf_open;
        to_fill.initial_gas_mix = OPENTURF_DEFAULT_ATMOS;
        to_fill.air = to_fill.create_gas_mixture() as datum_gas_mixture;
        await lab_rat.breathe();
        TEST_ASSERT(!Dm.truthy(lab_rat.failed_last_breath) && !Dm.truthy(lab_rat.has_alert(ALERT_NOT_ENOUGH_OXYGEN)), "Humans can't get a full breath from the standard initial_gas_mix on a turf");

        // Breathing from standard internals tank.
        lab_rat = allocate<mob_living_carbon_human_consistent>();
        obj_item_tank_internals source = await equip_labrat_internals(lab_rat, typeof(obj_item_tank_internals_emergency_oxygen));
        await lab_rat.breathe();
        TEST_ASSERT(!Dm.truthy(lab_rat.failed_last_breath) && !Dm.truthy(lab_rat.has_alert(ALERT_NOT_ENOUGH_OXYGEN)), "Humans can't get a full breath from standard o2 tanks");
        if (lab_rat.@internal != null)
            TEST_ASSERT(Dm.truthy(await source.toggle_internals(lab_rat)) && lab_rat.@internal == null, "toggle_internals() failed to close internals");

        // Empty internals suffocation.
        lab_rat = allocate<mob_living_carbon_human_consistent>();
        source = await equip_labrat_internals(lab_rat, typeof(obj_item_tank_internals_emergency_oxygen_empty));
        TEST_ASSERT(Dm.truthy(await source.toggle_internals(lab_rat)) && lab_rat.@internal != null, "Plasmaman toggle_internals() failed to toggle internals");
        await lab_rat.breathe();
        TEST_ASSERT(Dm.truthy(lab_rat.failed_last_breath) && Dm.truthy(lab_rat.has_alert(ALERT_NOT_ENOUGH_OXYGEN)), "Humans should suffocate from empty o2 tanks");

        // Nitrogen internals suffocation.
        lab_rat = allocate<mob_living_carbon_human_consistent>();
        source = await equip_labrat_internals(lab_rat, typeof(obj_item_tank_internals_emergency_oxygen_empty));
        datum_gas_mixture air_contents = source.air_contents as datum_gas_mixture;
        air_contents.assert_gas(typeof(datum_gas_nitrogen));
        air_contents.moles[typeof(datum_gas_nitrogen)] = (10 * ONE_ATMOSPHERE) * Dm.num(source.volume) / (R_IDEAL_GAS_EQUATION * T20C);
        TEST_ASSERT(Dm.truthy(await source.toggle_internals(lab_rat)) && lab_rat.@internal != null, "Plasmaman toggle_internals() failed to toggle internals");
        await lab_rat.breathe();
        TEST_ASSERT(Dm.truthy(lab_rat.failed_last_breath) && Dm.truthy(lab_rat.has_alert(ALERT_NOT_ENOUGH_OXYGEN)), "Humans should suffocate from pure n2 tanks");
    }

    // tg: code/modules/unit_tests/breath.dm:55  Destroy resets the turf's initial_gas_mix: each test here has a world of its own.
}
