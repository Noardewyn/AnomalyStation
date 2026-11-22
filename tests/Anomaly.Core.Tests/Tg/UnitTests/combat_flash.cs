// Translated from tgstation (snapshot 1d99bd7f): code/modules/unit_tests/combat_flash.dm
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

/// Tests that flashes, well, flash.
// tg: code/modules/unit_tests/combat_flash.dm:2 (/datum/unit_test/flash_click; class named by rules 2.3)
public class datum_unit_test_flash_click : TgUnitTest
{
    public string apply_verb = "while Attacker was not on combat mode";

    public override void New()
    {
        base.New();
        TgToyRoom.Install(this);
    }

    // tg: code/modules/unit_tests/combat_flash.dm:5
    public override async DmTask Run()
    {
        mob_living_carbon_human attacker = allocate<mob_living_carbon_human_consistent>();
        mob_living_carbon_human victim = allocate<mob_living_carbon_human_consistent>();
        obj_item_assembly_flash_handheld flash = allocate<obj_item_assembly_flash_handheld>();

        await attacker.put_in_active_hand(flash, TRUE);
        await ready_subjects(attacker, victim);
        await TgClicks.click_wrapper(attacker, victim);
        check_results(attacker, victim);
    }

    // tg: code/modules/unit_tests/combat_flash.dm:13
    public virtual async DmTask ready_subjects(mob_living_carbon_human attacker, mob_living_carbon_human victim)
    {
        await victim.forceMove(locate(attacker.x + 1, attacker.y, attacker.z));
        attacker.face_atom(victim);
        victim.face_atom(attacker);
    }

    // tg: code/modules/unit_tests/combat_flash.dm:18
    public virtual void check_results(mob_living_carbon_human attacker, mob_living_carbon_human victim)
    {
        TEST_ASSERT_NOTEQUAL(victim.get_stamina_loss(), 0, Dm.text("Victim should have sustained stamina loss from being flashed head-on [].", apply_verb));
    }
}

/// Tests that flashes flash on combat mode.
// tg: code/modules/unit_tests/combat_flash.dm:22 (/datum/unit_test/flash_click/combat_mode)
public sealed class datum_unit_test_flash_click_combat_mode : datum_unit_test_flash_click
{
    public datum_unit_test_flash_click_combat_mode() { apply_verb = "while Attacker was on combat mode"; }

    public override async DmTask ready_subjects(mob_living_carbon_human attacker, mob_living_carbon_human victim)
    {
        await base.ready_subjects(attacker, victim);
        attacker.set_combat_mode(TRUE);
    }
}

/// Tests that flashes do not flash if wearing protection.
// tg: code/modules/unit_tests/combat_flash.dm:30 (/datum/unit_test/flash_click/flash_protection)
public sealed class datum_unit_test_flash_click_flash_protection : datum_unit_test_flash_click
{
    public datum_unit_test_flash_click_flash_protection() { apply_verb = "while wearing flash protection"; }

    public override async DmTask ready_subjects(mob_living_carbon_human attacker, mob_living_carbon_human victim)
    {
        await base.ready_subjects(attacker, victim);
        obj_item_clothing_glasses_sunglasses glasses = allocate<obj_item_clothing_glasses_sunglasses>();
        await victim.equip_to_appropriate_slot(glasses);
    }

    public override void check_results(mob_living_carbon_human attacker, mob_living_carbon_human victim)
    {
        TEST_ASSERT_EQUAL(victim.get_stamina_loss(), 0, Dm.text("Victim should not have sustained stamina loss from being flashed head-on [].", apply_verb));
    }
}
