// Translated from tgstation (snapshot 1d99bd7f): code/modules/unit_tests/move_pulled.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.UnitTests;

// ADAPTED (wave 4, package A's test-writer; the room and the types switched to real ones by W4.M):
// - the room is the toy room (see movement_order_sanity.cs);
// - the puller is a real /mob/living and the crate a real /obj made dense (tg: /mob/living/carbon/human/consistent and
//   /obj/structure/closet/crate, which are later packages: a closet is a dense structure), the decal a real
//   /obj/effect/decal/cleanable (tg: its /blood subtype);
// - tg's `click_wrapper(puller, target)` (a click on a distant turf or decal, which ends in /mob/Move_Pulled when the
//   puller pulls something, _onclick/click.dm) is the direct call of Move_Pulled;
// - `start_pulling(crate)` passes the state GRAB_PASSIVE: the base proc is what a /mob/living without its override runs.

/// Test that people can move pulled objects by clicking distant turfs or decals
// tg: code/modules/unit_tests/move_pulled.dm:1 (/datum/unit_test/move_pulled; abstract)
public abstract class datum_unit_test_move_pulled : TgUnitTest
{
    public override void New()
    {
        base.New();
        TgToyRoom.Install(this);
    }

    // tg: code/modules/unit_tests/move_pulled.dm:5
    public override async DmTask Run()
    {
        mob_living puller = allocate<mob_living>();
        obj crate = allocate<obj>();
        crate.density = TRUE; // ADAPTED: a closet is dense (/obj/structure/closet), a bare /obj is not

        await puller.start_pulling(crate, GRAB_PASSIVE);

        TEST_ASSERT(puller.pulling == crate, "The puller is not pulling the crate.");

        atom first_target = get_first_target(puller);
        await puller.Move_Pulled(first_target); // click_wrapper(puller, first_target)

        TEST_ASSERT(crate.loc == run_loc_floor_bottom_left, "The crate should not have moved from clicking on the crate's turf.");

        atom second_target = get_second_target(puller);
        await puller.Move_Pulled(second_target); // click_wrapper(puller, second_target)

        TEST_ASSERT(crate.loc != run_loc_floor_bottom_left, "The crate should have moved in the direction of the top right turf.");
        TEST_ASSERT(crate.loc == get_turf(second_target), "The crate should be located at the northeast of the puller.");
    }

    // tg: code/modules/unit_tests/move_pulled.dm:24
    public abstract atom get_first_target(mob_living puller);

    // tg: code/modules/unit_tests/move_pulled.dm:27
    public abstract atom get_second_target(mob_living puller);
}

/// Try to move a pulled object to the turf below us, then to the opposite corner
// tg: code/modules/unit_tests/move_pulled.dm:30 (/datum/unit_test/move_pulled/to_turf)
public sealed class datum_unit_test_move_pulled_to_turf : datum_unit_test_move_pulled
{
    // tg: code/modules/unit_tests/move_pulled.dm:33
    public override atom get_first_target(mob_living puller)
    {
        return run_loc_floor_bottom_left as atom;
    }

    // tg: code/modules/unit_tests/move_pulled.dm:36
    public override atom get_second_target(mob_living puller)
    {
        return get_step(puller, NORTHEAST);
    }
}

/// Try to move a pulled object to a decal below us, then to a decal in the opposite corner
// tg: code/modules/unit_tests/move_pulled.dm:39 (/datum/unit_test/move_pulled/to_decal)
public sealed class datum_unit_test_move_pulled_to_decal : datum_unit_test_move_pulled
{
    // tg: code/modules/unit_tests/move_pulled.dm:42
    public override atom get_first_target(mob_living puller)
    {
        return allocate<obj_effect_decal_cleanable>(run_loc_floor_bottom_left);
    }

    // tg: code/modules/unit_tests/move_pulled.dm:45
    public override atom get_second_target(mob_living puller)
    {
        return allocate<obj_effect_decal_cleanable>(get_step(puller, NORTHEAST));
    }
}
