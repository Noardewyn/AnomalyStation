// Translated from tgstation (snapshot 1d99bd7f): code/modules/unit_tests/combat_cuffs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.UnitTests;

// ADAPTED (M7x slice E): the whole file. The room is the toy room (TgToyRoom.Install); click_wrapper is tg's (unit_test.dm:201).

/// Tests that handcuffs can be applied.
// tg: code/modules/unit_tests/combat_cuffs.dm:2 (/datum/unit_test/apply_cuffs; class named by rules 2.3)
public sealed class datum_unit_test_apply_cuffs : TgUnitTest
{
    public datum_unit_test_apply_cuffs()
    {
        // tg: code/modules/unit_tests/combat_cuffs.dm:3
        priority = TEST_LONGER;
    }

    public override void New()
    {
        base.New();
        TgToyRoom.Install(this);
    }

    // tg: code/modules/unit_tests/combat_cuffs.dm:5
    public override async DmTask Run()
    {
        mob_living_carbon_human attacker = allocate<mob_living_carbon_human_consistent>();
        mob_living_carbon_human victim = allocate<mob_living_carbon_human_consistent>();
        obj_item_restraints_handcuffs cuffs = allocate<obj_item_restraints_handcuffs>();
        cuffs.handcuff_time = 0.2f * SECONDS;
        await attacker.put_in_active_hand(cuffs, TRUE);
        await TgClicks.click_wrapper(attacker, victim);
        TEST_ASSERT_EQUAL(victim.handcuffed, cuffs, "Handcuff attempt (non-combat-mode) failed in an otherwise valid setup.");

        await victim.clear_cuffs(cuffs);
        await attacker.put_in_active_hand(cuffs, TRUE);
        attacker.set_combat_mode(TRUE);
        await TgClicks.click_wrapper(attacker, victim);
        TEST_ASSERT_EQUAL(victim.handcuffed, cuffs, "Handcuff attempt (combat-mode) failed in an otherwise valid setup.");
    }
}
