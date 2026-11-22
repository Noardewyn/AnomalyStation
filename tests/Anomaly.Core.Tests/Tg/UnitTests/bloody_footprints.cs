// Translated from tgstation (snapshot 1d99bd7f): code/modules/unit_tests/bloody_footprints.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.UnitTests;

// M6 slice 1: the whole test (the bloody soles component, the footprints decal and the pool's COMSIG_STEP_ON_BLOOD).
// ADAPTED: tg runs it on the unit test map; the room is the toy room (TgToyRoom.Install).

///Tests to make sure bloody footprits work as expected
///So no stacking, they actually apply, and shoe staining thrown in for free
// tg: code/modules/unit_tests/bloody_footprints.dm:3
public sealed class datum_unit_test_bloody_footprints : TgUnitTest
{
    public override void New()
    {
        base.New();
        TgToyRoom.Install(this);
    }

    // tg: code/modules/unit_tests/bloody_footprints.dm:5
    public override async DmTask Run()
    {
        mob_living_carbon_human blood_master = allocate<mob_living_carbon_human_consistent>();
        obj_item_clothing_shoes holds_blood = allocate<obj_item_clothing_shoes>();

        await blood_master.equip_to_slot_if_possible(holds_blood, ITEM_SLOT_FEET);

        turf_open move_to = get_step(blood_master, get_dir(blood_master, run_loc_floor_top_right)) as turf_open;
        //We need to not be "on" the same tile as the pool
        await blood_master.forceMove(move_to);

        obj_effect_decal_cleanable_blood pool = allocate<obj_effect_decal_cleanable_blood>();

        //Max out the pools blood, so each step will make things stained enough to matter
        pool.bloodiness = BLOOD_POOL_MAX;

        await pool.forceMove(run_loc_floor_bottom_left as atom);
        await blood_master.forceMove(run_loc_floor_bottom_left as atom);

        datum_component_bloodysoles soles = holds_blood.GetComponent(typeof(datum_component_bloodysoles)) as datum_component_bloodysoles;

        TEST_ASSERT(Dm.truthy(soles.total_bloodiness), "Shoes didn't become stained after stepping in a pool of blood");

        //The bloody soles component handles the order of stepping on blood/stepping on a bloody tile in a constranating way
        //Which means it needs to check and see if any time has passed between steps, so it can be sure the player is stepping onto a new tile (that should become bloody)
        //I on the other hand need to do all my movements in one tick, so we need to sidestep this check
        //Please kill me slowly
        soles.last_pickup = Dm.num(soles.last_pickup) - 1;

        //Move off the bloody tile, time to do some testing
        await blood_master.forceMove(move_to);

        soles.last_pickup = Dm.num(soles.last_pickup) - 1;
        await blood_master.forceMove(run_loc_floor_bottom_left as atom);

        int footprint_total = 0;
        foreach (obj_effect_decal_cleanable_blood_footprints print_set in Dm.each<obj_effect_decal_cleanable_blood_footprints>(move_to))
            footprint_total += 1;

        TEST_ASSERT(footprint_total != 0, "The floor didn't get covered in blood after being walked over");

        soles.last_pickup = Dm.num(soles.last_pickup) - 1;

        //Another set of movements to try and make some doubled up prints
        await blood_master.forceMove(move_to);

        soles.last_pickup = Dm.num(soles.last_pickup) - 1;
        await blood_master.forceMove(run_loc_floor_bottom_left as atom);

        footprint_total = 0;
        foreach (obj_effect_decal_cleanable_blood_footprints print_set in Dm.each<obj_effect_decal_cleanable_blood_footprints>(move_to))
            footprint_total += 1;

        TEST_ASSERT(footprint_total != 0, "The floor somehow lost its footprints after being walked over");
        TEST_ASSERT_EQUAL(footprint_total, 1, "The floor had more than one set of footprints in it, something is fucked");
    }
}
