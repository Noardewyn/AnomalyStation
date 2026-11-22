// Translated from tgstation (snapshot 1d99bd7f): code/modules/unit_tests/tail_wag.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.UnitTests;

// Gaps SP1 (D338): the whole file.
// ADAPTED: tg runs it on the unit test map; the room is the toy room (TgToyRoom.Install), a world without a Master. tg's stop_after
// check waits for a timer that sets the test's var (VARSET_CALLBACK on the /datum/unit_test) and UNTIL it is set; the harness's test is
// no datum, so it sleeps the timer's 0.2 seconds with SStimer firing every tick by hand (CombatStamina.sleep, as combat_stamina does).

/// Tests to make sure tail wagging behaves as expected
// tg: code/modules/unit_tests/tail_wag.dm:2 (/datum/unit_test/tail_wag; class named by rules 2.3)
public sealed class datum_unit_test_tail_wag : TgUnitTest
{
    public override void New()
    {
        base.New();
        TgToyRoom.Install(this);
    }

    // tg: code/modules/unit_tests/tail_wag.dm:6
    public override async DmTask Run()
    {
        mob_living_carbon_human dummy = allocate<mob_living_carbon_human_consistent>();
        obj_item_organ_tail_cat dummy_tail = allocate<obj_item_organ_tail_cat>();
        await dummy_tail.Insert(dummy, special: TRUE, movement_flags: DELETE_IF_REPLACED);

        // SANITY TEST

        // start wagging
        await dummy.wag_tail();
        if (!Dm.truthy(Dm.bitand(dummy_tail.wag_flags, WAG_WAGGING)))
            TEST_FAIL("Tail did not start wagging when it should have!");

        // stop wagging
        await dummy.unwag_tail();
        if (Dm.truthy(Dm.bitand(dummy_tail.wag_flags, WAG_WAGGING)))
            TEST_FAIL("Tail did not stop wagging when it should have!");

        // TESTING WAG EMOTE

        // start wagging
        await dummy.emote("wag");
        if (!Dm.truthy(Dm.bitand(dummy_tail.wag_flags, WAG_WAGGING)))
            TEST_FAIL("Tail did not start wagging after using the *wag emote!");

        // stop wagging
        await dummy.emote("wag");
        if (Dm.truthy(Dm.bitand(dummy_tail.wag_flags, WAG_WAGGING)))
            TEST_FAIL("Tail did not stop wagging after using the *wag emote!");

        // TESTING WAG_ABLE FLAG

        // flip the wag flag to unwaggable
        dummy_tail.wag_flags = Dm.bitand(dummy_tail.wag_flags, Dm.bitnot(WAG_ABLE));

        // try to wag it again
        await dummy.wag_tail();
        if (Dm.truthy(Dm.bitand(dummy_tail.wag_flags, WAG_WAGGING)))
            TEST_FAIL("Tail should not have the ability to wag, yet it did!");

        // flip the wag flag to waggable again
        dummy_tail.wag_flags = Dm.bitor(dummy_tail.wag_flags, WAG_ABLE);

        // start wagging again
        await dummy.wag_tail();
        if (!Dm.truthy(Dm.bitand(dummy_tail.wag_flags, WAG_WAGGING)))
            TEST_FAIL("Tail did not start wagging when it should have!");

        // TESTING STOP_AFTER

        // stop wagging
        await dummy.unwag_tail();
        if (Dm.truthy(Dm.bitand(dummy_tail.wag_flags, WAG_WAGGING)))
            TEST_FAIL("Tail did not stop wagging when it should have!");

        // start wagging, stop after 0.1 seconds
        await dummy.wag_tail(0.1f * SECONDS);
        // because timers are a pain
        if (!Dm.truthy(Dm.bitand(dummy_tail.wag_flags, WAG_WAGGING)))
            TEST_FAIL("Tail did not start wagging when it should have!");

        // wait a little bit (tg: addtimer(VARSET_CALLBACK(src, timer_finished, TRUE), 0.2 SECONDS) and UNTIL(timer_finished))
        await CombatStamina.sleep(0.2f * SECONDS);

        if (Dm.truthy(Dm.bitand(dummy_tail.wag_flags, WAG_WAGGING)))
            TEST_FAIL("Tail was supposed to stop wagging on its own after 0.1 seconds but it did not!");

        // TESTING TAIL REMOVAL

        // remove the tail
        await dummy_tail.Remove(dummy, special: TRUE);

        // check if tail is still wagging after being removed
        if (Dm.truthy(Dm.bitand(dummy_tail.wag_flags, WAG_WAGGING)))
            TEST_FAIL("Tail was still wagging after being removed!");

        // try to wag the removed tail
        await dummy.wag_tail();
        if (Dm.truthy(Dm.bitand(dummy_tail.wag_flags, WAG_WAGGING)))
            TEST_FAIL("A disembodied tail was able to start wagging!");

        // TESTING MOB DEATH

        // put it back and start wagging again
        await dummy_tail.Insert(dummy, special: TRUE, movement_flags: DELETE_IF_REPLACED);
        await dummy.wag_tail();
        if (!Dm.truthy(Dm.bitand(dummy_tail.wag_flags, WAG_WAGGING)))
            TEST_FAIL("Tail did not start wagging when it should have!");

        // kill the mob, see if it stops wagging
        await dummy.adjust_brute_loss(9001);
        if (Dm.truthy(Dm.bitand(dummy_tail.wag_flags, WAG_WAGGING)))
            TEST_FAIL("A mob's tail was still wagging after being killed!");

        // check if we are still able to wag the tail after death
        await dummy.wag_tail();
        if (Dm.truthy(Dm.bitand(dummy_tail.wag_flags, WAG_WAGGING)))
            TEST_FAIL("A dead mob was able to wag their tail!");
    }
}
