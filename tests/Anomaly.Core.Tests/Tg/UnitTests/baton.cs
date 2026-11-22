// Translated from tgstation (snapshot 1d99bd7f): code/modules/unit_tests/baton.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.UnitTests;

// ADAPTED (M7x slice E): every case of tg's baton test (security batons and stunprods with left and right clicks, active and
// not, with and without combat mode; the detective's police baton). The room is the toy room (TgToyRoom.Install);
// click_wrapper is tg's (unit_test.dm:201); the abstract_type classes are abstract C# classes (rules 2.3). Each concrete
// class is one xunit test in a fresh world.

/// <summary>tg's click_wrapper (unit_test.dm:201), shared by the ported combat tests.</summary>
internal static class TgClicks
{
    // tg: code/modules/unit_tests/unit_test.dm:201
    public static async DmTask click_wrapper(mob_living clicker, atom clicked_on, DmList passed_params = null, float wait = 0)
    {
        passed_params ??= Dm.list(Dm.kv(LEFT_CLICK, 1), Dm.kv(BUTTON, LEFT_CLICK));
        clicker.next_click = -1;
        clicker.next_move = -1;
        await clicker.ClickOn(clicked_on, list2params(passed_params));
        if (wait > 0)
            await sleep(wait); // the detached part of a click (a timer-driven effect) finishes
    }
}

/// Tests a variety of baton interactions with a variety of baton types
// tg: code/modules/unit_tests/baton.dm:2 (/datum/unit_test/baton; abstract_type)
public abstract class datum_unit_test_baton : TgUnitTest
{
    private const string TRAIT_SOURCE_UNIT_TESTS = "unit_tests";

    /// Baton type being tested
    public Type baton_type;
    /// Description of the click type being tested
    public string click_descriptor;
    /// Modifiers for the click being tested
    public DmList click_modifiers;
    /// Whether the attacker is in combat mode
    public bool combat_mode = true;
    /// Whether the baton is active
    public bool use_baton = true;

    public override void New()
    {
        base.New();
        TgToyRoom.Install(this);
    }

    // tg: code/modules/unit_tests/baton.dm:15
    public override async DmTask Run()
    {
        mob_living_carbon_human secoff = allocate<mob_living_carbon_human_consistent>();
        mob_living_carbon_human tider = allocate<mob_living_carbon_human_consistent>();
        ADD_TRAIT(secoff, TRAIT_PERFECT_ATTACKER, TRAIT_SOURCE_UNIT_TESTS);

        obj_item_melee_baton stun_baton = allocate(baton_type) as obj_item_melee_baton;
        await secoff.put_in_active_hand(stun_baton);
        secoff.set_combat_mode(combat_mode ? TRUE : FALSE);
        if (use_baton)
            await stun_baton.attack_self(secoff);

        await test_attack(secoff, tider);
    }

    /// Performs two attacks, tests the resulting damage on the defender
    // tg: code/modules/unit_tests/baton.dm:27
    public virtual async DmTask test_attack(mob_living attacker, mob_living defender)
    {
        string baton_name = Dm.initial(baton_type, (obj_item_melee_baton b) => b.name);
        // Perform an attack, while off baton cooldown
        await TgClicks.click_wrapper(attacker, defender, click_modifiers);
        TEST_ASSERT_EQUAL(defender.get_stamina_loss(), asserted_stamina_damage(),
            Dm.text("[] did an incorrect amount of stamina damage to target ([])", baton_name, get_descriptor()));
        TEST_ASSERT_EQUAL(defender.get_brute_loss(), asserted_brute_damage(),
            Dm.text("[] did an incorrect amount of brute damage to target ([])", baton_name, get_descriptor()));

        // Now perform an attack while on baton cooldown
        await TgClicks.click_wrapper(attacker, defender, click_modifiers);
        TEST_ASSERT_EQUAL(defender.get_stamina_loss(), asserted_stamina_damage(),
            Dm.text("[] did an incorrect amount of stamina damage to target while on cooldown ([])", baton_name, get_descriptor()));
        TEST_ASSERT_EQUAL(defender.get_brute_loss(), asserted_brute_damage() * 2,
            Dm.text("[] did an incorrect amount of brute damage to target while on cooldown ([])", baton_name, get_descriptor()));
    }

    /// How much stamina damage is expected from this test case
    // tg: code/modules/unit_tests/baton.dm:42
    public virtual float asserted_stamina_damage() => Dm.initial(baton_type, (obj_item_melee_baton b) => b.stamina_damage);

    /// How much brute damage is expected from this test case
    // tg: code/modules/unit_tests/baton.dm:46
    public virtual float asserted_brute_damage() => Dm.initial(baton_type, (obj_item_melee_baton b) => b.force);

    /// Description of the current test case
    // tg: code/modules/unit_tests/baton.dm:50
    public virtual string get_descriptor()
        => Dm.text("[] / [] / []", click_descriptor, combat_mode ? "in combat mode" : "not in combat mode", use_baton ? "active baton" : "inactive baton");

    // tg: code/modules/unit_tests/baton.dm:55,59 (the click type classes)
    protected static DmList left_click() => Dm.list(Dm.kv(LEFT_CLICK, TRUE), Dm.kv(BUTTON, LEFT_CLICK));
    protected static DmList right_click() => Dm.list(Dm.kv(RIGHT_CLICK, TRUE), Dm.kv(BUTTON, RIGHT_CLICK));
}

// tg: code/modules/unit_tests/baton.dm:55 (/datum/unit_test/baton/left_click; abstract_type)
public abstract class datum_unit_test_baton_left_click : datum_unit_test_baton
{
    protected datum_unit_test_baton_left_click() { click_descriptor = "left click"; click_modifiers = left_click(); }
}

// tg: code/modules/unit_tests/baton.dm:60 (/datum/unit_test/baton/right_click; abstract_type)
public abstract class datum_unit_test_baton_right_click : datum_unit_test_baton
{
    protected datum_unit_test_baton_right_click() { click_descriptor = "right click"; click_modifiers = right_click(); }
}

// Left click sec baton
// tg: code/modules/unit_tests/baton.dm:66 (/datum/unit_test/baton/left_click/sec; abstract_type)
public abstract class datum_unit_test_baton_left_click_sec : datum_unit_test_baton_left_click
{
    protected datum_unit_test_baton_left_click_sec() { baton_type = typeof(obj_item_melee_baton_security_loaded); }
}

// - Active + combat mode = stuns, no damage
public sealed class datum_unit_test_baton_left_click_sec_on_with_combat : datum_unit_test_baton_left_click_sec
{
    public datum_unit_test_baton_left_click_sec_on_with_combat() { combat_mode = true; use_baton = true; }
    public override float asserted_brute_damage() => 0;
}

// - Active + no combat mode = stuns, no damage
public sealed class datum_unit_test_baton_left_click_sec_on_no_combat : datum_unit_test_baton_left_click_sec
{
    public datum_unit_test_baton_left_click_sec_on_no_combat() { combat_mode = false; use_baton = true; }
    public override float asserted_brute_damage() => 0;
}

// - Inactive + combat mode = no stun, deals damage
public sealed class datum_unit_test_baton_left_click_sec_off_with_combat : datum_unit_test_baton_left_click_sec
{
    public datum_unit_test_baton_left_click_sec_off_with_combat() { combat_mode = true; use_baton = false; }
    public override float asserted_stamina_damage() => 0;
}

// - Inactive + no combat mode = no sun, no damage
public sealed class datum_unit_test_baton_left_click_sec_off_no_combat : datum_unit_test_baton_left_click_sec
{
    public datum_unit_test_baton_left_click_sec_off_no_combat() { combat_mode = false; use_baton = false; }
    public override float asserted_stamina_damage() => 0;
    public override float asserted_brute_damage() => 0;
}

// Right click sec baton
// tg: code/modules/unit_tests/baton.dm:108 (/datum/unit_test/baton/right_click/sec)
public abstract class datum_unit_test_baton_right_click_sec : datum_unit_test_baton_right_click
{
    protected datum_unit_test_baton_right_click_sec() { baton_type = typeof(obj_item_melee_baton_security_loaded); }
}

// - Active + combat mode = stuns, deals damage
public sealed class datum_unit_test_baton_right_click_sec_on_with_combat : datum_unit_test_baton_right_click_sec
{
    public datum_unit_test_baton_right_click_sec_on_with_combat() { combat_mode = true; use_baton = true; }
}

// - Active + no combat mode = stuns, deals damage
public sealed class datum_unit_test_baton_right_click_sec_on_no_combat : datum_unit_test_baton_right_click_sec
{
    public datum_unit_test_baton_right_click_sec_on_no_combat() { combat_mode = false; use_baton = true; }
}

// - Inactive + combat mode = no stun, deals damage
public class datum_unit_test_baton_right_click_sec_attack_when_off : datum_unit_test_baton_right_click_sec
{
    public datum_unit_test_baton_right_click_sec_attack_when_off() { use_baton = false; }
    public override float asserted_stamina_damage() => 0;
}

// - Inactive + no combat mode = no stun, deals damage
public sealed class datum_unit_test_baton_right_click_sec_attack_when_off_no_combat : datum_unit_test_baton_right_click_sec_attack_when_off
{
    public datum_unit_test_baton_right_click_sec_attack_when_off_no_combat() { combat_mode = false; }
    public override float asserted_stamina_damage() => 0;
}

// Left click stunprod
// tg: code/modules/unit_tests/baton.dm:147 (/datum/unit_test/baton/left_click/prod; abstract_type)
public abstract class datum_unit_test_baton_left_click_prod : datum_unit_test_baton_left_click
{
    protected datum_unit_test_baton_left_click_prod() { baton_type = typeof(obj_item_melee_baton_security_cattleprod_loaded); }
}

// - Active + combat mode = stuns, no damage
public sealed class datum_unit_test_baton_left_click_prod_on_with_combat : datum_unit_test_baton_left_click_prod
{
    public datum_unit_test_baton_left_click_prod_on_with_combat() { combat_mode = true; use_baton = true; }
    public override float asserted_brute_damage() => 0;
}

// - Active + no combat mode = stuns, no damage
public sealed class datum_unit_test_baton_left_click_prod_on_no_combat : datum_unit_test_baton_left_click_prod
{
    public datum_unit_test_baton_left_click_prod_on_no_combat() { combat_mode = false; use_baton = true; }
    public override float asserted_brute_damage() => 0;
}

// - Inactive + combat mode = no stun, damages
public sealed class datum_unit_test_baton_left_click_prod_off_with_combat : datum_unit_test_baton_left_click_prod
{
    public datum_unit_test_baton_left_click_prod_off_with_combat() { combat_mode = true; use_baton = false; }
    public override float asserted_stamina_damage() => 0;
}

// - Inactive + no combat mode = no stun, no damage
public sealed class datum_unit_test_baton_left_click_prod_off_no_combat : datum_unit_test_baton_left_click_prod
{
    public datum_unit_test_baton_left_click_prod_off_no_combat() { combat_mode = false; use_baton = false; }
    public override float asserted_stamina_damage() => 0;
    public override float asserted_brute_damage() => 0;
}

// Right click stunprod
// tg: code/modules/unit_tests/baton.dm:189 (/datum/unit_test/baton/right_click/prod; abstract_type)
public abstract class datum_unit_test_baton_right_click_prod : datum_unit_test_baton_right_click
{
    protected datum_unit_test_baton_right_click_prod() { baton_type = typeof(obj_item_melee_baton_security_cattleprod_loaded); }
}

// - Active + combat mode = stuns, deals damage
public sealed class datum_unit_test_baton_right_click_prod_on_with_combat : datum_unit_test_baton_right_click_prod
{
    public datum_unit_test_baton_right_click_prod_on_with_combat() { combat_mode = true; use_baton = true; }
}

// - Active + no combat mode = stuns, deals damage
public sealed class datum_unit_test_baton_right_click_prod_on_no_combat : datum_unit_test_baton_right_click_prod
{
    public datum_unit_test_baton_right_click_prod_on_no_combat() { combat_mode = false; use_baton = true; }
}

// - Inactive + combat mode = no stun, deals damage
public class datum_unit_test_baton_right_click_prod_attack_when_off : datum_unit_test_baton_right_click_prod
{
    public datum_unit_test_baton_right_click_prod_attack_when_off() { combat_mode = true; use_baton = false; }
    public override float asserted_stamina_damage() => 0;
}

// - Inactive + no combat mode = no stun, deals damage
public sealed class datum_unit_test_baton_right_click_prod_attack_when_off_no_combat : datum_unit_test_baton_right_click_prod_attack_when_off
{
    public datum_unit_test_baton_right_click_prod_attack_when_off_no_combat() { combat_mode = false; use_baton = false; }
    public override float asserted_stamina_damage() => 0;
}

// Left click det baton
// - Always stuns never damages, regardless of combat mode
// tg: code/modules/unit_tests/baton.dm:228 (/datum/unit_test/baton/left_click/det)
public class datum_unit_test_baton_left_click_det : datum_unit_test_baton_left_click
{
    public datum_unit_test_baton_left_click_det() { baton_type = typeof(obj_item_melee_baton); use_baton = false; combat_mode = false; }
    public override float asserted_brute_damage() => 0;
}

public sealed class datum_unit_test_baton_left_click_det_combat_mode : datum_unit_test_baton_left_click_det
{
    public datum_unit_test_baton_left_click_det_combat_mode() { combat_mode = true; }
}

// Right click det baton
// - Never stuns, always damages, regardless of combat mode (see: stun_on_harmbaton var)
// tg: code/modules/unit_tests/baton.dm:241 (/datum/unit_test/baton/right_click/det)
public class datum_unit_test_baton_right_click_det : datum_unit_test_baton_right_click
{
    public datum_unit_test_baton_right_click_det() { baton_type = typeof(obj_item_melee_baton); use_baton = false; }
    public override float asserted_stamina_damage() => 0;
}

public sealed class datum_unit_test_baton_right_click_det_combat_mode : datum_unit_test_baton_right_click_det
{
    public datum_unit_test_baton_right_click_det_combat_mode() { combat_mode = true; }
}
