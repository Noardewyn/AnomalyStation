// Translated from tgstation (snapshot 1d99bd7f): code/modules/unit_tests/mob_damage.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.UnitTests;

// PARTIAL (M2 P2, scope.md 2g, the ROADMAP's done-when test of M2's damage): the carbon part of tg's mob_damage test,
// sanity_simple, sanity_complex, nobreath, toxintraits, ordered_healing and godmode, in tg's order. test_biotypes needs
// /datum/species/plasmaman (the other species are left out, scope.md 2e "the cut") and /datum/unit_test/mob_damage/animal
// needs basic mobs (a later milestone): both are left out. The stamina baseline of test_apply_damage (amount_after
// defaults to the mob's stamina loss minus the expected value, mob_damage.dm:54) is tg's: every included type is checked
// against it, so stamina must take and heal exactly what the other types do.
// ADAPTED: tg runs it on the unit test map; the room is the toy room (TgToyRoom.Install).

/// Tests to make sure mob damage procs are working correctly
// tg: code/modules/unit_tests/mob_damage.dm:2 (/datum/unit_test/mob_damage; class named by rules 2.3)
public sealed class datum_unit_test_mob_damage : TgUnitTest
{
    // tg: code/modules/unit_tests/_unit_tests.dm:99  #define TRAIT_SOURCE_UNIT_TESTS "unit_tests" (inside #if defined(UNIT_TESTS),
    // so the defines converter does not emit it: a constant of the test)
    private const string TRAIT_SOURCE_UNIT_TESTS = "unit_tests";

    public datum_unit_test_mob_damage()
    {
        // tg: code/modules/unit_tests/mob_damage.dm:3
        priority = TEST_LONGER;
    }

    public override void New()
    {
        base.New();
        TgToyRoom.Install(this);
        // ADAPTED: tg's MC always has SSmobs; the unit test harness makes it here so that Run can pause it (an inert stub
        // until it fires).
        if (SSmobs == null)
            Dm.@new<datum_controller_subsystem_mobs>();
    }

    // tg: code/modules/unit_tests/mob_damage.dm:5
    public override void Destroy()
    {
        SSmobs.ignite();
        base.Destroy();
    }

    // tg: code/modules/unit_tests/mob_damage.dm:9
    public override async DmTask Run()
    {
        SSmobs.pause();
        mob_living_carbon_human dummy = allocate<mob_living_carbon_human_consistent>();
        dummy.maxHealth = 200; // tank mode

        /* The sanity tests: here we make sure that:
        1) That damage procs are returning the expected values. They should be returning the actual amount of damage taken/healed.
            (Negative values mean damage was taken, positive mean healing)
        2) Verifying that the damage has been accurately applied to the mob afterwards. */

        await test_sanity_simple(dummy);
        await test_sanity_complex(dummy);

        // Testing if biotypes are working as intended
        // test_biotypes(dummy): left out (/datum/species/plasmaman, see the header)

        // Testing whether or not TRAIT_NOBREATH is working as intended
        await test_nobreath(dummy);

        // Testing whether or not TRAIT_TOXINLOVER and TRAIT_TOXIMMUNE are working as intended
        await test_toxintraits(dummy);

        // Testing the proc ordered_healing()
        await test_ordered_healing(dummy);

        // testing with godmode enabled
        await test_godmode(dummy);
    }

    /**
     * Test whether the adjust damage procs return the correct values and that the mob's health is the expected value afterwards.
     *
     * By default this calls apply_damage(amount) followed by verify_damage(amount_after) and returns TRUE if both succeeded.
     * amount_after defaults to the mob's current stamina loss but can be overridden as needed.
     */
    // tg: code/modules/unit_tests/mob_damage.dm:53
    private async DmTask<bool> test_apply_damage(mob_living testing_mob, float amount, float? expected = null, object amount_after = null, float? included_types = null, float? biotypes = null, float? bodytypes = null, float? forced = null)
    {
        expected ??= -amount;
        if (amount_after == null)
            amount_after = Dm.sub(testing_mob.get_stamina_loss(), expected); // stamina loss applies to both carbon and basic mobs the same way, so that's why we're using it here
        if (!await apply_damage(testing_mob as mob_living_carbon, amount, expected, included_types, biotypes, bodytypes, forced))
            return false;
        if (!verify_damage(testing_mob as mob_living_carbon, amount_after, included_types))
            return false;
        return true;
    }

    /**
     * Test whether the set damage procs return the correct values and that the mob's health is the expected value afterwards.
     */
    // tg: code/modules/unit_tests/mob_damage.dm:78
    private async DmTask<bool> test_set_damage(mob_living testing_mob, float amount, float? expected = null, object amount_after = null, float? included_types = null, float? biotypes = null, float? bodytypes = null, float? forced = null)
    {
        if (amount_after == null)
            amount_after = Dm.sub(testing_mob.get_stamina_loss(), expected);
        if (!await set_damage(testing_mob as mob_living_carbon, amount, expected, included_types, biotypes, bodytypes, forced))
            return false;
        if (!verify_damage(testing_mob as mob_living_carbon, amount_after, included_types))
            return false;
        return true;
    }

    /**
     * Check that the mob has a specific amount of damage
     *
     * By default this checks that the mob has <amount> of every type of damage.
     */
    // tg: code/modules/unit_tests/mob_damage.dm:96
    private bool verify_damage(mob_living_carbon testing_mob, object amount, float? included_types = null)
    {
        included_types ??= ALL;
        if (Dm.truthy(Dm.bitand(included_types, TOXLOSS)))
            TEST_ASSERT_EQUAL(testing_mob.get_tox_loss(), amount,
                Dm.text("[] should have [] toxin damage, instead they have []!", testing_mob, amount, testing_mob.get_tox_loss()));
        if (Dm.truthy(Dm.bitand(included_types, BRUTELOSS)))
        {
            TEST_ASSERT_EQUAL(round(Dm.num(testing_mob.get_brute_loss()), 1), amount,
                Dm.text("(Testing get_brute_loss()) [] should have [] brute damage, instead they have []!", testing_mob, amount, testing_mob.get_brute_loss()));
            TEST_ASSERT_EQUAL(round(Dm.num(testing_mob.get_brute_loss_for_type(BODYTYPE_ORGANIC)), 1), amount,
                Dm.text("(Testing get_brute_loss_for_type(BODYTYPE_ORGANIC)) [] should have [] brute damage, instead they have []!", testing_mob, amount, testing_mob.get_brute_loss_for_type(BODYTYPE_ORGANIC)));
        }
        if (Dm.truthy(Dm.bitand(included_types, FIRELOSS)))
        {
            TEST_ASSERT_EQUAL(round(Dm.num(testing_mob.get_fire_loss()), 1), amount,
                Dm.text("(Testing get_fire_loss()) [] should have [] burn damage, instead they have []!", testing_mob, amount, testing_mob.get_fire_loss()));
            TEST_ASSERT_EQUAL(round(Dm.num(testing_mob.get_fire_loss_for_type(BODYTYPE_ORGANIC)), 1), amount,
                Dm.text("(Testing get_fire_loss_for_type(BODYTYPE_ORGANIC)) [] should have [] burn damage, instead they have []!", testing_mob, amount, testing_mob.get_fire_loss_for_type(BODYTYPE_ORGANIC)));
        }
        if (Dm.truthy(Dm.bitand(included_types, OXYLOSS)))
            TEST_ASSERT_EQUAL(testing_mob.get_oxy_loss(), amount,
                Dm.text("[] should have [] oxy damage, instead they have []!", testing_mob, amount, testing_mob.get_oxy_loss()));
        if (Dm.truthy(Dm.bitand(included_types, STAMINALOSS)))
            TEST_ASSERT_EQUAL(testing_mob.get_stamina_loss(), amount,
                Dm.text("[] should have [] stamina damage, instead they have []!", testing_mob, amount, testing_mob.get_stamina_loss()));
        return true;
    }

    /**
     * Apply a specific amount of damage to the mob using adjust_brute_loss(), adjust_tox_loss(), etc.
     *
     * By default this applies <amount> damage of every type to the mob, and checks that the damage procs return the <expected> value
     */
    // tg: code/modules/unit_tests/mob_damage.dm:131
    private async DmTask<bool> apply_damage(mob_living_carbon testing_mob, float amount, float? expected = null, float? included_types = null, float? biotypes = null, float? bodytypes = null, float? forced = null)
    {
        expected ??= -amount;
        included_types ??= ALL;
        biotypes ??= ALL;
        bodytypes ??= ALL;
        forced ??= FALSE;
        object damage_returned = null;
        if (Dm.truthy(Dm.bitand(included_types, TOXLOSS)))
        {
            damage_returned = await testing_mob.adjust_tox_loss(amount, updating_health: FALSE, forced: forced, required_biotype: biotypes);
            TEST_ASSERT_EQUAL(damage_returned, expected,
                Dm.text("adjust_tox_loss() should have returned [], but returned [] instead!", expected, damage_returned));
        }
        if (Dm.truthy(Dm.bitand(included_types, BRUTELOSS)))
        {
            damage_returned = round(Dm.num(await testing_mob.adjust_brute_loss(amount, updating_health: FALSE, forced: forced, required_bodytype: bodytypes)), 1);
            TEST_ASSERT_EQUAL(damage_returned, expected,
                Dm.text("adjust_brute_loss() should have returned [], but returned [] instead!", expected, damage_returned));
        }
        if (Dm.truthy(Dm.bitand(included_types, FIRELOSS)))
        {
            damage_returned = round(Dm.num(await testing_mob.adjust_fire_loss(amount, updating_health: FALSE, forced: forced, required_bodytype: bodytypes)), 1);
            TEST_ASSERT_EQUAL(damage_returned, expected,
                Dm.text("adjust_fire_loss() should have returned [], but returned [] instead!", expected, damage_returned));
        }
        if (Dm.truthy(Dm.bitand(included_types, OXYLOSS)))
        {
            damage_returned = await testing_mob.adjust_oxy_loss(amount, updating_health: FALSE, forced: forced, required_biotype: biotypes);
            TEST_ASSERT_EQUAL(damage_returned, expected,
                Dm.text("adjust_oxy_loss() should have returned [], but returned [] instead!", expected, damage_returned));
        }
        if (Dm.truthy(Dm.bitand(included_types, STAMINALOSS)))
        {
            damage_returned = await testing_mob.adjust_stamina_loss(amount, updating_stamina: FALSE, forced: forced, required_biotype: biotypes);
            TEST_ASSERT_EQUAL(damage_returned, expected,
                Dm.text("adjust_stamina_loss() should have returned [], but returned [] instead!", expected, damage_returned));
        }
        return true;
    }

    /**
     * Set a specific amount of damage for the mob using set_brute_loss(), set_tox_loss(), etc.
     *
     * By default this sets every type of damage to <amount> for the mob, and checks that the damage procs return the <expected> value
     */
    // tg: code/modules/unit_tests/mob_damage.dm:168
    private async DmTask<bool> set_damage(mob_living_carbon testing_mob, float amount, float? expected = null, float? included_types = null, float? biotypes = null, float? bodytypes = null, float? forced = null)
    {
        expected ??= -amount;
        included_types ??= ALL;
        biotypes ??= ALL;
        bodytypes ??= ALL;
        forced ??= FALSE;
        object damage_returned = null;
        if (Dm.truthy(Dm.bitand(included_types, TOXLOSS)))
        {
            damage_returned = await testing_mob.set_tox_loss(amount, updating_health: FALSE, forced: forced, required_biotype: biotypes);
            TEST_ASSERT_EQUAL(damage_returned, expected,
                Dm.text("set_tox_loss() should have returned [], but returned [] instead!", expected, damage_returned));
        }
        if (Dm.truthy(Dm.bitand(included_types, BRUTELOSS)))
        {
            damage_returned = round(Dm.num(await testing_mob.set_brute_loss(amount, updating_health: FALSE, forced: forced)), 1);
            TEST_ASSERT_EQUAL(damage_returned, expected,
                Dm.text("set_brute_loss() should have returned [], but returned [] instead!", expected, damage_returned));
        }
        if (Dm.truthy(Dm.bitand(included_types, FIRELOSS)))
        {
            damage_returned = round(Dm.num(await testing_mob.set_fire_loss(amount, updating_health: FALSE, forced: forced)), 1);
            TEST_ASSERT_EQUAL(damage_returned, expected,
                Dm.text("set_fire_loss() should have returned [], but returned [] instead!", expected, damage_returned));
        }
        if (Dm.truthy(Dm.bitand(included_types, OXYLOSS)))
        {
            damage_returned = await testing_mob.set_oxy_loss(amount, updating_health: FALSE, forced: forced, required_biotype: biotypes);
            TEST_ASSERT_EQUAL(damage_returned, expected,
                Dm.text("set_oxy_loss() should have returned [], but returned [] instead!", expected, damage_returned));
        }
        if (Dm.truthy(Dm.bitand(included_types, STAMINALOSS)))
        {
            damage_returned = await testing_mob.set_stamina_loss(amount, updating_stamina: FALSE, forced: forced, required_biotype: biotypes);
            TEST_ASSERT_EQUAL(damage_returned, expected,
                Dm.text("set_stamina_loss() should have returned [], but returned [] instead!", expected, damage_returned));
        }
        return true;
    }

    ///	Sanity tests damage and healing using adjust_tox_loss, adjust_brute_loss, etc
    // tg: code/modules/unit_tests/mob_damage.dm:193
    private async DmTask test_sanity_simple(mob_living_carbon_human dummy)
    {
        // Apply 5 damage and then heal it
        if (!await test_apply_damage(dummy, amount: 5))
            TEST_FAIL("ABOVE FAILURE: failed test_sanity_simple! damage was not applied correctly");

        if (!await test_apply_damage(dummy, amount: -5))
            TEST_FAIL("ABOVE FAILURE: failed test_sanity_simple! healing was not applied correctly");

        // Apply 15 damage and heal 3
        if (!await test_apply_damage(dummy, amount: 15))
            TEST_FAIL("ABOVE FAILURE: failed test_sanity_simple! damage was not applied correctly");

        if (!await test_apply_damage(dummy, amount: -3))
            TEST_FAIL("ABOVE FAILURE: failed test_sanity_simple! underhealing was not applied correctly");

        // Now overheal by 666. It should heal for 12.

        if (!await test_apply_damage(dummy, amount: -666, expected: 12))
            TEST_FAIL("ABOVE FAILURE: failed test_sanity_simple! overhealing was not applied correctly");

        // Now test the damage setter procs

        // set all types of damage to 5
        if (!await test_set_damage(dummy, amount: 5, expected: -5))
            TEST_FAIL("ABOVE FAILURE: failed test_sanity_simple! failed to set damage to 5");
        // now try healing 5
        if (!await test_set_damage(dummy, amount: 0, expected: 5))
            TEST_FAIL("ABOVE FAILURE: failed test_sanity_simple! failed to set damage to 0");
    }

    ///	Sanity tests damage and healing using the more complex procs like take_overall_damage(), heal_overall_damage(), etc
    // tg: code/modules/unit_tests/mob_damage.dm:223
    private async DmTask test_sanity_complex(mob_living_carbon_human dummy)
    {
        // Heal up, so that errors from the previous tests we won't cause this one to fail
        await dummy.fully_heal(HEAL_DAMAGE);

        object damage_returned = null;
        // take 5 brute, 2 burn
        damage_returned = round(Dm.num(await dummy.take_bodypart_damage(5, 2, updating_health: FALSE)), 1);
        TEST_ASSERT_EQUAL(damage_returned, -7,
            Dm.text("take_bodypart_damage() should have returned -7, but returned [] instead!", damage_returned));

        TEST_ASSERT_EQUAL(round(Dm.num(dummy.get_brute_loss()), 1), 5,
            Dm.text("Dummy should have 5 brute damage, instead they have []!", dummy.get_brute_loss()));
        TEST_ASSERT_EQUAL(round(Dm.num(dummy.get_fire_loss()), 1), 2,
            Dm.text("Dummy should have 2 burn damage, instead they have []!", dummy.get_fire_loss()));

        // heal 4 brute, 1 burn
        damage_returned = round(Dm.num(await dummy.heal_bodypart_damage(4, 1, updating_health: FALSE)), 1);
        TEST_ASSERT_EQUAL(damage_returned, 5,
            Dm.text("heal_bodypart_damage() should have returned 5, but returned [] instead!", damage_returned));

        if (!verify_damage(dummy, 1, included_types: Dm.bitor(BRUTELOSS, FIRELOSS)))
            TEST_FAIL("heal_bodypart_damage did not apply its healing correctly on the mob!");

        // heal 1 brute, 1 burn
        damage_returned = round(Dm.num(await dummy.heal_overall_damage(1, 1, updating_health: FALSE)), 1);
        TEST_ASSERT_EQUAL(damage_returned, 2,
            Dm.text("heal_overall_damage() should have returned 2, but returned [] instead!", damage_returned));

        if (!verify_damage(dummy, 0, included_types: Dm.bitor(BRUTELOSS, FIRELOSS)))
            TEST_FAIL("heal_overall_damage did not apply its healing correctly on the mob!");

        // take 50 brute, 50 burn
        damage_returned = round(Dm.num(await dummy.take_overall_damage(50, 50, updating_health: FALSE)), 1);
        TEST_ASSERT_EQUAL(damage_returned, -100,
            Dm.text("take_overall_damage() should have returned -100, but returned [] instead!", damage_returned));

        if (!verify_damage(dummy, 50, included_types: Dm.bitor(BRUTELOSS, FIRELOSS)))
            TEST_FAIL("take_overall_damage did not apply its damage correctly on the mob!");

        // testing negative damage amount args with the overall damage procs - the sign should be ignored for these procs

        damage_returned = round(Dm.num(await dummy.take_bodypart_damage(-5, -5, updating_health: FALSE)), 1);
        TEST_ASSERT_EQUAL(damage_returned, -10,
            Dm.text("take_bodypart_damage() should have returned -10, but returned [] instead!", damage_returned));

        damage_returned = round(Dm.num(await dummy.heal_bodypart_damage(-5, -5, updating_health: FALSE)), 1);
        TEST_ASSERT_EQUAL(damage_returned, 10,
            Dm.text("heal_bodypart_damage() should have returned 10, but returned [] instead!", damage_returned));

        damage_returned = round(Dm.num(await dummy.take_overall_damage(-5, -5, updating_health: FALSE)), 1);
        TEST_ASSERT_EQUAL(damage_returned, -10,
            Dm.text("take_overall_damage() should have returned -10, but returned [] instead!", damage_returned));

        damage_returned = round(Dm.num(await dummy.heal_overall_damage(-5, -5, updating_health: FALSE)), 1);
        TEST_ASSERT_EQUAL(damage_returned, 10,
            Dm.text("heal_overall_damage() should have returned 10, but returned [] instead!", damage_returned));

        if (!verify_damage(dummy, 50, included_types: Dm.bitor(BRUTELOSS, FIRELOSS)))
            TEST_FAIL("heal_overall_damage did not apply its healingcorrectly on the mob!");

        // testing overhealing

        damage_returned = round(Dm.num(await dummy.heal_overall_damage(75, 99, updating_health: FALSE)), 1);
        TEST_ASSERT_EQUAL(damage_returned, 100,
            Dm.text("heal_overall_damage() should have returned 100, but returned [] instead!", damage_returned));

        if (!verify_damage(dummy, 0, included_types: Dm.bitor(BRUTELOSS, FIRELOSS)))
            TEST_FAIL("heal_overall_damage did not apply its healing correctly on the mob!");
    }

    ///	Tests damage procs with godmode on
    // tg: code/modules/unit_tests/mob_damage.dm:293
    private async DmTask test_godmode(mob_living_carbon_human dummy)
    {
        // Heal up, so that errors from the previous tests we won't cause this one to fail
        await dummy.fully_heal(HEAL_DAMAGE);
        // add godmode
        ADD_TRAIT(dummy, TRAIT_GODMODE, TRAIT_GENERIC);

        // Apply 9 damage and then heal it
        if (!await test_apply_damage(dummy, amount: 9, expected: 0))
            TEST_FAIL("ABOVE FAILURE: failed test_godmode! mob took damage despite having godmode enabled.");

        if (!await test_apply_damage(dummy, amount: -9, expected: 0))
            TEST_FAIL("ABOVE FAILURE: failed test_godmode! mob healed when they should've been at full health.");

        // Apply 11 damage and then heal it, this time with forced enabled. The damage should go through regardless of godmode.
        if (!await test_apply_damage(dummy, amount: 11, forced: TRUE))
            TEST_FAIL("ABOVE FAILURE: failed test_godmode! godmode did not respect forced = TRUE");

        if (!await test_apply_damage(dummy, amount: -11, forced: TRUE))
            TEST_FAIL("ABOVE FAILURE: failed test_godmode! godmode did not respect forced = TRUE");

        // remove godmode
        REMOVE_TRAIT(dummy, TRAIT_GODMODE, TRAIT_GENERIC);
    }

    /// Testing oxyloss with the TRAIT_NOBREATH
    // tg: code/modules/unit_tests/mob_damage.dm:352
    private async DmTask test_nobreath(mob_living_carbon_human dummy)
    {
        // Heal up, so that errors from the previous tests we won't cause this one to fail
        await dummy.fully_heal(HEAL_DAMAGE);

        // TRAIT_NOBREATH is supposed to prevent oxyloss damage (but not healing). Let's make sure that's the case.
        ADD_TRAIT(dummy, TRAIT_NOBREATH, TRAIT_SOURCE_UNIT_TESTS);
        // force some oxyloss here
        await dummy.set_oxy_loss(2, updating_health: FALSE, forced: TRUE);

        // Try to take more oxyloss damage with TRAIT_NOBREATH. It should not work.
        if (!await test_apply_damage(dummy, 2, expected: 0, amount_after: dummy.get_oxy_loss(), included_types: OXYLOSS))
            TEST_FAIL("ABOVE FAILURE: failed test_nobreath! mob took oxyloss damage while having TRAIT_NOBREATH");

        // Make sure we are still be able to heal the oxyloss. This should work.
        if (!await test_apply_damage(dummy, -2, amount_after: Dm.sub(dummy.get_oxy_loss(), 2), included_types: OXYLOSS))
            TEST_FAIL("ABOVE FAILURE: failed test_nobreath! mob could not heal oxyloss damage while having TRAIT_NOBREATH");

        REMOVE_TRAIT(dummy, TRAIT_NOBREATH, TRAIT_SOURCE_UNIT_TESTS);
    }

    /// Testing toxloss with TRAIT_TOXINLOVER and TRAIT_TOXIMMUNE
    // tg: code/modules/unit_tests/mob_damage.dm:372
    private async DmTask test_toxintraits(mob_living_carbon_human dummy)
    {
        // Heal up, so that errors from the previous tests we won't cause this one to fail
        await dummy.fully_heal(HEAL_DAMAGE);

        // TRAIT_TOXINLOVER is supposed to invert toxin damage and healing. Things that would normally cause toxloss now heal it, and vice versa.
        ADD_TRAIT(dummy, TRAIT_TOXINLOVER, TRAIT_SOURCE_UNIT_TESTS);
        // force some toxloss here
        await dummy.set_tox_loss(2, updating_health: FALSE, forced: TRUE);

        // Try to take more toxloss damage with TRAIT_TOXINLOVER. It should heal instead.
        if (!await test_apply_damage(dummy, 2, expected: 2, amount_after: Dm.sub(dummy.get_tox_loss(), 2), included_types: TOXLOSS))
            TEST_FAIL("ABOVE FAILURE: failed test_toxintraits! mob did not heal from toxin damage with TRAIT_TOXINLOVER");

        // If we try to heal the toxloss we should take damage instead
        if (!await test_apply_damage(dummy, -2, expected: -2, amount_after: Dm.add(dummy.get_tox_loss(), 2), included_types: TOXLOSS))
            TEST_FAIL("ABOVE FAILURE: failed test_toxintraits! mob did not take damage from toxin healing with TRAIT_TOXINLOVER");

        // TOXIMMUNE trait should prevent the damage you get from being healed by toxins medicines while having TRAIT_TOXINLOVER
        ADD_TRAIT(dummy, TRAIT_TOXIMMUNE, TRAIT_SOURCE_UNIT_TESTS);

        // need to force apply some toxin damage since the TOXIMUNNE trait sets toxloss to 0 upon being added
        await dummy.set_tox_loss(2, updating_health: FALSE, forced: TRUE);

        // try to 'heal' again - this time it should just do nothing because we should be immune to any sort of toxin damage - including from inverted healing
        if (!await test_apply_damage(dummy, -2, expected: 0, amount_after: dummy.get_tox_loss(), included_types: TOXLOSS))
            TEST_FAIL("ABOVE FAILURE: failed test_toxintraits! mob should not have taken any damage or healing with TRAIT_TOXINLOVER + TRAIT_TOXIMMUNE");

        // ok, let's try taking 'damage'. The inverted damage should still heal mobs with the TOXIMMUNE trait.
        if (!await test_apply_damage(dummy, 2, expected: 2, amount_after: Dm.sub(dummy.get_tox_loss(), 2), included_types: TOXLOSS))
            TEST_FAIL("ABOVE FAILURE: failed test_toxintraits! mob did not heal from taking toxin damage with TRAIT_TOXINLOVER + TRAIT_TOXIMMUNE");

        REMOVE_TRAIT(dummy, TRAIT_TOXINLOVER, TRAIT_SOURCE_UNIT_TESTS);
        REMOVE_TRAIT(dummy, TRAIT_TOXIMMUNE, TRAIT_SOURCE_UNIT_TESTS);
    }

    /// Testing heal_ordered_damage()
    // tg: code/modules/unit_tests/mob_damage.dm:407
    private async DmTask test_ordered_healing(mob_living_carbon_human dummy)
    {
        // Heal up, so that errors from the previous tests we won't cause this one to fail
        await dummy.fully_heal(HEAL_DAMAGE);
        object damage_returned = null;

        // We apply 20 brute, 20 burn, and 20 toxin damage. 60 damage total
        await apply_damage(dummy, 20, included_types: Dm.bitor(Dm.bitor(TOXLOSS, BRUTELOSS), FIRELOSS));

        // Heal 30 damage of that, starting from brute
        damage_returned = round(Dm.num(await dummy.heal_ordered_damage(30, Dm.list(BRUTE, BURN, TOX))), 1);
        TEST_ASSERT_EQUAL(damage_returned, 30,
            Dm.text("heal_ordered_damage() should have returned 30, but returned [] instead!", damage_returned));

        // Should have 10 burn damage and 20 toxins damage remaining, let's check
        TEST_ASSERT_EQUAL(dummy.get_brute_loss(), 0,
            Dm.text("[] should have 0 brute damage, but has [] instead!", this, dummy.get_brute_loss()));
        TEST_ASSERT_EQUAL(dummy.get_fire_loss(), 10,
            Dm.text("[] should have 10 burn damage, but has [] instead!", this, dummy.get_fire_loss()));
        TEST_ASSERT_EQUAL(dummy.get_tox_loss(), 20,
            Dm.text("[] should have 20 toxin damage, but has [] instead!", this, dummy.get_tox_loss()));

        // Now heal the remaining 30, overhealing by 5.
        damage_returned = round(Dm.num(await dummy.heal_ordered_damage(35, Dm.list(BRUTE, BURN, TOX))), 1);
        TEST_ASSERT_EQUAL(damage_returned, 30,
            Dm.text("heal_ordered_damage() should have returned 30, but returned [] instead!", damage_returned));

        // Should have no damage remaining
        TEST_ASSERT_EQUAL(dummy.get_brute_loss(), 0,
            Dm.text("[] should have 0 brute damage, but has [] instead!", this, dummy.get_brute_loss()));
        TEST_ASSERT_EQUAL(dummy.get_fire_loss(), 0,
            Dm.text("[] should have 0 burn damage, but has [] instead!", this, dummy.get_fire_loss()));
        TEST_ASSERT_EQUAL(dummy.get_tox_loss(), 0,
            Dm.text("[] should have 0 toxin damage, but has [] instead!", this, dummy.get_tox_loss()));
    }
}
