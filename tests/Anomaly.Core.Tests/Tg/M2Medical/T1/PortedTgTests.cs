// Translated from tgstation (snapshot 1d99bd7f): code/modules/unit_tests/stuns.dm, oxyloss_suffocation.dm, full_heal.dm,
// status_effect_validity.dm and spawn_humans.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.UnitTests;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M2Medical.T1;

// M2 T1's ports of tg's medical unit tests. ADAPTED: tg runs them on the unit test map; the room is the toy room
// (TgToyRoom.Install), with NotTranslatedThrows on. Left out: stuns.dm /datum/unit_test/stun_absorb (the stun absorption
// status effect, buffs.dm, is not part of M2) and spawn_humans.dm many_armed_humans (change_number_of_hands, M4a).

/// Shared setup of the ported tests: the toy room, the strict world.
public abstract class MedicalUnitTest : TgUnitTest
{
    public override void New()
    {
        base.New();
        TgToyRoom.Install(this);
        DmWorldState.Of(world_under_test).NotTranslatedThrows = true;
    }
}

/// Tests stun and the canstun flag
// tg: code/modules/unit_tests/stuns.dm:2 (/datum/unit_test/stun; class named by rules 2.3)
public sealed class datum_unit_test_stun : MedicalUnitTest
{
    // tg: code/modules/unit_tests/stuns.dm:4
    public override async DmTask Run()
    {
        mob_living_carbon_human gets_stunned = allocate<mob_living_carbon_human_consistent>();

        await gets_stunned.Stun(1 * SECONDS);
        TEST_ASSERT(Dm.truthy(gets_stunned.IsStun()), "Stun() failed to apply stun");

        await gets_stunned.SetStun(0 * SECONDS);
        TEST_ASSERT(!Dm.truthy(gets_stunned.IsStun()), "SetStun(0) failed to clear stun");

        gets_stunned.status_flags = Dm.bitand(gets_stunned.status_flags, ~CANSTUN);
        await gets_stunned.Stun(1 * SECONDS);
        TEST_ASSERT(!Dm.truthy(gets_stunned.IsStun()), "Stun() stunned despite not having CANSTUN flag");
    }
}

/// Tests knockdown and the canknockdown flag
// tg: code/modules/unit_tests/stuns.dm:17 (/datum/unit_test/knockdown)
public sealed class datum_unit_test_knockdown : MedicalUnitTest
{
    // tg: code/modules/unit_tests/stuns.dm:19
    public override async DmTask Run()
    {
        mob_living_carbon_human gets_knockdown = allocate<mob_living_carbon_human_consistent>();

        await gets_knockdown.Knockdown(1 * SECONDS);
        TEST_ASSERT(Dm.truthy(gets_knockdown.IsKnockdown()), "Knockdown() failed to apply knockdown");

        await gets_knockdown.SetKnockdown(0 * SECONDS);
        TEST_ASSERT(!Dm.truthy(gets_knockdown.IsKnockdown()), "SetKnockdown(0) failed to clear knockdown");

        gets_knockdown.status_flags = Dm.bitand(gets_knockdown.status_flags, ~CANKNOCKDOWN);
        await gets_knockdown.Knockdown(1 * SECONDS);
        TEST_ASSERT(!Dm.truthy(gets_knockdown.IsKnockdown()), "Knockdown() knocked over despite not having CANKNOCKDOWN flag");
    }
}

/// Tests paralyze and stuns that have two flags checked (in this case, canstun and canknockdown)
// tg: code/modules/unit_tests/stuns.dm:32 (/datum/unit_test/paralyze)
public sealed class datum_unit_test_paralyze : MedicalUnitTest
{
    // tg: code/modules/unit_tests/stuns.dm:34
    public override async DmTask Run()
    {
        mob_living_carbon_human gets_paralyzed = allocate<mob_living_carbon_human_consistent>();

        await gets_paralyzed.Paralyze(1 * SECONDS);
        TEST_ASSERT(Dm.truthy(gets_paralyzed.IsParalyzed()), "Paralyze() failed to apply paralyze");

        await gets_paralyzed.SetParalyzed(0 * SECONDS);
        TEST_ASSERT(!Dm.truthy(gets_paralyzed.IsParalyzed()), "SetParalyzed(0) failed to clear paralyze");

        gets_paralyzed.status_flags = Dm.bitand(gets_paralyzed.status_flags, ~CANSTUN); // paralyze needs both CANSTUN and CANKNOCKDOWN to succeed
        await gets_paralyzed.Paralyze(1 * SECONDS);
        TEST_ASSERT(!Dm.truthy(gets_paralyzed.IsParalyzed()), "Paralyze() paralyzed a mob despite not having CANSTUN flag (but still having CANKNOCKDOWN)");
    }
}

/// Tests unconsciousness and the canunconscious flag
// tg: code/modules/unit_tests/stuns.dm:47 (/datum/unit_test/unconsciousness)
public sealed class datum_unit_test_unconsciousness : MedicalUnitTest
{
    // tg: code/modules/unit_tests/stuns.dm:49
    public override async DmTask Run()
    {
        mob_living_carbon_human gets_unconscious = allocate<mob_living_carbon_human_consistent>();

        await gets_unconscious.Unconscious(1 * SECONDS);
        TEST_ASSERT(Dm.truthy(gets_unconscious.IsUnconscious()), "Unconscious() failed to apply unconsciousness");

        await gets_unconscious.SetUnconscious(0 * SECONDS);
        TEST_ASSERT(!Dm.truthy(gets_unconscious.IsUnconscious()), "SetUnconscious(0) failed to clear unconsciousness");

        gets_unconscious.status_flags = Dm.bitand(gets_unconscious.status_flags, ~CANUNCONSCIOUS);
        await gets_unconscious.Unconscious(1 * SECONDS);
        TEST_ASSERT(!Dm.truthy(gets_unconscious.IsUnconscious()), "Unconscious() knocked unconscious despite not having CANUNCONSCIOUS flag");
    }
}

/// Test getting over a certain threshold of oxy damage results in KO
// tg: code/modules/unit_tests/oxyloss_suffocation.dm:2 (/datum/unit_test/oxyloss_suffocation)
public sealed class datum_unit_test_oxyloss_suffocation : MedicalUnitTest
{
    // tg: code/modules/unit_tests/oxyloss_suffocation.dm:4
    public override async DmTask Run()
    {
        mob_living_carbon_human dummy = allocate<mob_living_carbon_human_consistent>();

        await dummy.set_oxy_loss(75);
        TEST_ASSERT(HAS_TRAIT_FROM(dummy, TRAIT_KNOCKEDOUT, OXYLOSS_TRAIT), "Dummy should have been knocked out from taking oxy damage.");
        await dummy.set_oxy_loss(0);
        TEST_ASSERT(!HAS_TRAIT_FROM(dummy, TRAIT_KNOCKEDOUT, OXYLOSS_TRAIT), "Dummy should have woken up from KO when healing to 0 oxy damage.");
    }
}

/// Tests the fully heal flag [HEAL_ORGANS].
// tg: code/modules/unit_tests/full_heal.dm:2 (/datum/unit_test/full_heal_heals_organs)
public sealed class datum_unit_test_full_heal_heals_organs : MedicalUnitTest
{
    // tg: code/modules/unit_tests/full_heal.dm:4
    public override async DmTask Run()
    {
        mob_living_carbon_human dummy = allocate<mob_living_carbon_human_consistent>();

        foreach (obj_item_organ organ in Dm.each_cast<obj_item_organ>(dummy.organs))
            await organ.apply_organ_damage(50);

        await dummy.fully_heal(HEAL_ORGANS);

        foreach (obj_item_organ organ in Dm.each_cast<obj_item_organ>(dummy.organs))
        {
            if (organ.damage <= 0)
                continue;
            TEST_FAIL(Dm.text("Organ [] did not get healed by fullyheal flag HEAL_ORGANS.", organ));
        }
    }
}

/// Tests the fully heal flag [HEAL_REFRESH_ORGANS].
// tg: code/modules/unit_tests/full_heal.dm:18 (/datum/unit_test/full_heal_regenerates_organs)
public sealed class datum_unit_test_full_heal_regenerates_organs : MedicalUnitTest
{
    // tg: code/modules/unit_tests/full_heal.dm:20
    public override async DmTask Run()
    {
        mob_living_carbon_human dummy = allocate<mob_living_carbon_human_consistent>();

        DmList we_started_with = Dm.list();

        foreach (obj_item_organ organ in Dm.each_cast<obj_item_organ>(dummy.organs).ToList())
        {
            if (Dm.truthy(Dm.bitand(organ.organ_flags, ORGAN_VITAL))) // leave this for now
                continue;
            we_started_with.Add(organ.GetType());
            qdel(organ);
        }

        TEST_ASSERT(Dm.truthy(length(we_started_with)), "Dummy didn't spawn with any organs to regenerate.");

        await dummy.fully_heal(HEAL_REFRESH_ORGANS);

        foreach (Type organ_type in Dm.each_cast<Type>(we_started_with))
        {
            if (Dm.truthy(dummy.get_organ_by_type(organ_type)))
                continue;
            TEST_FAIL(Dm.text("Organ [] didn't regenerate in the dummy after fullyheal flag HEAL_REFRESH_ORGANS.", organ_type.Name));
        }
    }
}

/// Tests the fully heal combination flag [HEAL_DAMAGE].
// tg: code/modules/unit_tests/full_heal.dm:42 (/datum/unit_test/full_heal_damage_types)
public sealed class datum_unit_test_full_heal_damage_types : MedicalUnitTest
{
    // tg: code/modules/unit_tests/full_heal.dm:44
    public override async DmTask Run()
    {
        mob_living_carbon_human dummy = allocate<mob_living_carbon_human_consistent>();

        await dummy.apply_damages(brute: 10, burn: 10, tox: 10, oxy: 10, stamina: 10);
        await dummy.fully_heal(HEAL_DAMAGE);

        if (Dm.truthy(dummy.get_brute_loss()))
            TEST_FAIL("The dummy still had brute damage after a fully heal!");
        if (Dm.truthy(dummy.get_fire_loss()))
            TEST_FAIL("The dummy still had burn damage after a fully heal!");
        if (Dm.truthy(dummy.get_tox_loss()))
            TEST_FAIL("The dummy still had toxins damage after a fully heal!");
        if (Dm.truthy(dummy.get_oxy_loss()))
            TEST_FAIL("The dummy still had oxy damage after a fully heal!");
        if (Dm.truthy(dummy.get_stamina_loss()))
            TEST_FAIL("The dummy still had stamina damage after a fully heal!");
    }
}

/// Validates status effect tick interval setup
// tg: code/modules/unit_tests/status_effect_validity.dm:2 (/datum/unit_test/status_effect_ticks)
public sealed class datum_unit_test_status_effect_ticks : MedicalUnitTest
{
    // tg: code/modules/unit_tests/status_effect_validity.dm:4
    public override async DmTask Run()
    {
        foreach (Type checking in Dm.each_cast<Type>(subtypesof(typeof(datum_status_effect))))
        {
            if (Dm.eq(Dm.initial(checking, (datum_status_effect p) => p.id), STATUS_EFFECT_ID_ABSTRACT))
                continue;
            float tick_speed = Dm.initial(checking, (datum_status_effect p) => p.tick_interval);
            if (tick_speed == STATUS_EFFECT_NO_TICK)
                continue;
            if (tick_speed < 0)
            {
                TEST_FAIL(Dm.text("Status effect [] has tick_interval set to a negative value other than STATUS_EFFECT_NO_TICK, this is not how you prevent ticks - use tick_interval = STATUS_EFFECT_NO_TICK instead.", checking.Name));
                continue;
            }
            if (tick_speed == INFINITY)
            {
                TEST_FAIL(Dm.text("Status effect [] has tick_interval set to INFINITY, this is not how you prevent ticks - use tick_interval = STATUS_EFFECT_NO_TICK instead.", checking.Name));
                continue;
            }
            float speed = Dm.initial(checking, (datum_status_effect p) => p.processing_speed);
            if (speed == STATUS_EFFECT_FAST_PROCESS)
            {
                if (Dm.num(Dm.mod(tick_speed, SSfastprocess.wait)) != 0 && tick_speed != STATUS_EFFECT_AUTO_TICK)
                    TEST_FAIL(Dm.text("Status effect [] has tick_interval set to [], which is not a multiple of SSfastprocess wait time ([]).", checking.Name, tick_speed, SSfastprocess.wait));
            }
            else if (speed == STATUS_EFFECT_NORMAL_PROCESS)
            {
                if (Dm.num(Dm.mod(tick_speed, SSprocessing.wait)) != 0 && tick_speed != STATUS_EFFECT_AUTO_TICK)
                    TEST_FAIL(Dm.text("Status effect [] has tick_interval set to [], which is not a multiple of SSprocessing wait time ([]).", checking.Name, tick_speed, SSprocessing.wait));
            }
            else if (speed == STATUS_EFFECT_PRIORITY)
            {
                float priority_wait = Dm.num(world.tick_lag) * Dm.num(SSpriority_effects.wait); // SSpriority_effects has the SS_TICKER flag, so its wait is in ticks, so we have to convert it to deciseconds.
                if (Dm.num(Dm.mod(tick_speed, priority_wait)) != 0 && tick_speed != STATUS_EFFECT_AUTO_TICK)
                    TEST_FAIL(Dm.text("Status effect [] has tick_interval set to [], which is not a multiple of SSpriority_effects wait time ([]).", checking.Name, tick_speed, priority_wait));
            }
            else
                TEST_FAIL(Dm.text("Invalid processing speed for status effect [] : []", checking.Name, speed));
        }
    }
}

/// Validates status effect duration setup
// tg: code/modules/unit_tests/status_effect_validity.dm:30 (/datum/unit_test/status_effect_duration)
public sealed class datum_unit_test_status_effect_duration : MedicalUnitTest
{
    // tg: code/modules/unit_tests/status_effect_validity.dm:32
    public override async DmTask Run()
    {
        foreach (Type checking in Dm.each_cast<Type>(subtypesof(typeof(datum_status_effect))))
        {
            if (Dm.eq(Dm.initial(checking, (datum_status_effect p) => p.id), STATUS_EFFECT_ID_ABSTRACT))
                continue;
            float duration = Dm.initial(checking, (datum_status_effect p) => p.duration) ?? 0;
            if (duration == STATUS_EFFECT_PERMANENT)
                continue;
            if (duration == INFINITY) // for some god forsaken reason, this is allowed
                continue;
            if (duration < 0)
            {
                TEST_FAIL(Dm.text("Status effect [] has duration set to a negative value other than STATUS_EFFECT_PERMANENT, this is not how you make effects last forever - use duration = STATUS_EFFECT_PERMANENT instead.", checking.Name));
                continue;
            }
            float speed = Dm.initial(checking, (datum_status_effect p) => p.processing_speed);
            if (speed == STATUS_EFFECT_FAST_PROCESS)
            {
                if (Dm.num(Dm.mod(duration, SSfastprocess.wait)) != 0)
                    TEST_FAIL(Dm.text("Status effect [] has duration set to [], which is not a multiple of SSfastprocess wait time ([]).", checking.Name, duration, SSfastprocess.wait));
            }
            else if (speed == STATUS_EFFECT_NORMAL_PROCESS)
            {
                if (Dm.num(Dm.mod(duration, SSprocessing.wait)) != 0)
                    TEST_FAIL(Dm.text("Status effect [] has duration set to [], which is not a multiple of SSprocessing wait time ([]).", checking.Name, duration, SSprocessing.wait));
            }
            else if (speed == STATUS_EFFECT_PRIORITY)
            {
                float priority_wait = Dm.num(world.tick_lag) * Dm.num(SSpriority_effects.wait);
                if (Dm.num(Dm.mod(duration, priority_wait)) != 0)
                    TEST_FAIL(Dm.text("Status effect [] has duration set to [], which is not a multiple of SSpriority_effects wait time ([]).", checking.Name, duration, priority_wait));
            }
            else
                TEST_FAIL(Dm.text("Invalid processing speed for status effect [] : []", checking.Name, speed));
        }
    }
}

/// Validates that status effect tick counts are directly proportional to duration, and that seconds_between_ticks added up is equal to duration.
// tg: code/modules/unit_tests/status_effect_validity.dm:60 (/datum/unit_test/status_effect_tick_counts)
public sealed class datum_unit_test_status_effect_tick_counts : MedicalUnitTest
{
    // tg: code/modules/unit_tests/status_effect_validity.dm:62
    public override async DmTask Run()
    {
        mob_living_carbon_human user = allocate<mob_living_carbon_human_consistent>(run_loc_floor_bottom_left);

        var counter = (datum_status_effect_unit_test_tick_counter)await user.apply_status_effect(typeof(datum_status_effect_unit_test_tick_counter));

        // The 0.2 here and in the for loop is arbitrary and can be any value that divides evenly into the duration and tick interval of the unit test status effect.
        // I chose 0.2 specifically because it just happens to be the SSfastprocess.wait in seconds, which basically simulates this status effect running on SSfastprocess.
        float ticks_required = (counter.duration ?? 0) / 10 / 0.2f + 1;

        for (int i = 1; i <= ticks_required; i++)
        {
            if (!QDELETED(counter))
                counter.process(0.2f);
        }

        float expected_tick_count = 10 * SECONDS / counter.tick_interval;
        if (MathF.Abs(counter.total_tick_count - expected_tick_count) > 0.01f)
            TEST_FAIL(Dm.text("Status effect tick count is not directly proportional to duration. Expected [] ticks, got [] ticks.", expected_tick_count, counter.total_tick_count));

        float expected_seconds = 10 * SECONDS / 10f;
        if (MathF.Abs(counter.total_seconds - expected_seconds) > 0.01f)
            TEST_FAIL(Dm.text("Status effect seconds_between_ticks accumulated together does not equal duration. Expected [] seconds, got [] seconds.", expected_seconds, counter.total_seconds));

        qdel(counter);
    }
}

// tg: code/modules/unit_tests/status_effect_validity.dm:86 (/datum/status_effect/unit_test_tick_counter; a fixture of the test)
[DmPath("/datum/status_effect/unit_test_tick_counter")]
public class datum_status_effect_unit_test_tick_counter : datum_status_effect
{
    public float total_tick_count = 0;
    public float total_seconds = 0;

    public datum_status_effect_unit_test_tick_counter()
    {
        duration = 10 * SECONDS;
        tick_interval = 0.4f * SECONDS;
        id = "unit_test_tick_counter";
        alert_type = null;
    }

    // tg: code/modules/unit_tests/status_effect_validity.dm:98
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        total_tick_count++;
        total_seconds += Dm.num(seconds_between_ticks);
        return null;
    }
}

/// Validates status effect alert type setup
// tg: code/modules/unit_tests/status_effect_validity.dm:104 (/datum/unit_test/status_effect_alert)
public sealed class datum_unit_test_status_effect_alert : MedicalUnitTest
{
    // tg: code/modules/unit_tests/status_effect_validity.dm:106
    public override async DmTask Run()
    {
        // The base typepath is used to indicate "I didn't set an alert type"
        object bad_alert_type = Dm.initial(typeof(datum_status_effect), (datum_status_effect p) => p.alert_type);
        TEST_ASSERT_NOTNULL(bad_alert_type, "No alert type defined in /datum/status_effect - This test may be redundant now.");

        foreach (Type checking in Dm.each_cast<Type>(subtypesof(typeof(datum_status_effect))))
        {
            if (Dm.eq(Dm.initial(checking, (datum_status_effect p) => p.id), STATUS_EFFECT_ID_ABSTRACT))
                continue;
            if (!Dm.eq(Dm.initial(checking, (datum_status_effect p) => p.alert_type), bad_alert_type))
                continue;
            TEST_FAIL(Dm.text("[] has not set alert_type. If you don't want an alert, set alert_type = null - Otherwise, give it an alert subtype.", checking.Name));
        }
    }
}

/// Validates status effect id setup
// tg: code/modules/unit_tests/status_effect_validity.dm:118 (/datum/unit_test/status_effect_ids)
public sealed class datum_unit_test_status_effect_ids : MedicalUnitTest
{
    // tg: code/modules/unit_tests/status_effect_validity.dm:120
    public override async DmTask Run()
    {
        // The base id is used to indicate "I didn't set an id"
        object bad_id = Dm.initial(typeof(datum_status_effect), (datum_status_effect p) => p.id);
        TEST_ASSERT_NOTNULL(bad_id, "No id defined in /datum/status_effect - This test may be redundant now.");

        foreach (Type checking in Dm.each_cast<Type>(subtypesof(typeof(datum_status_effect))))
        {
            if (Dm.eq(Dm.initial(checking, (datum_status_effect p) => p.id), STATUS_EFFECT_ID_ABSTRACT))
            {
                // we are just assuming that a child of an abstract should not be abstract.
                // of course in practice, this may not always be the case - but if you're
                // structuring a status effect like this, you can just change the parent id to anything else
                Type checking_parent = checking.BaseType;
                if (!Dm.eq(Dm.initial(checking_parent, (datum_status_effect p) => p.id), STATUS_EFFECT_ID_ABSTRACT))
                    continue;
            }
            if (!Dm.eq(Dm.initial(checking, (datum_status_effect p) => p.id), bad_id))
                continue;
            TEST_FAIL(Dm.text("[] has not set an id. This is required for status effects.", checking.Name));
        }
    }
}

// tg: code/modules/unit_tests/spawn_humans.dm:1 (/datum/unit_test/spawn_humans)
public sealed class datum_unit_test_spawn_humans : MedicalUnitTest
{
    // tg: code/modules/unit_tests/spawn_humans.dm:3
    public override async DmTask Run()
    {
        DmList locs = Builtins.block((atom)run_loc_floor_bottom_left, (atom)run_loc_floor_top_right);

        for (int I = 1; I <= 5; I++)
            allocate<mob_living_carbon_human_consistent>(pick(locs));

        await sleep(5 * SECONDS);
    }
}

/// Tests [/mob/living/carbon/human/proc/setup_organless_effects], specifically that they aren't applied when init is done
// tg: code/modules/unit_tests/spawn_humans.dm:10 (/datum/unit_test/human_default_traits)
public sealed class datum_unit_test_human_default_traits : MedicalUnitTest
{
    // tg: code/modules/unit_tests/spawn_humans.dm:12
    public override async DmTask Run()
    {
        mob_living_carbon_human_consistent dummy = allocate<mob_living_carbon_human_consistent>();
        TEST_ASSERT(!HAS_TRAIT_FROM(dummy, TRAIT_AGEUSIA, NO_TONGUE_TRAIT), "Dummy has ageusia on init, when it should've been removed by its default tongue.");
        TEST_ASSERT(!Dm.truthy(dummy.is_blind_from(NO_EYES)), "Dummy is blind on init,  when it should've been removed by its default eyes.");
        TEST_ASSERT(!HAS_TRAIT_FROM(dummy, TRAIT_DEAF, NO_EARS), "Dummy is deaf on init, when it should've been removed by its default ears.");
    }
}

/// Tests spawned humans have the correct bodypart order
// tg: code/modules/unit_tests/spawn_humans.dm:26 (/datum/unit_test/human_bodypart_order)
public sealed class datum_unit_test_human_bodypart_order : MedicalUnitTest
{
    // tg: code/modules/unit_tests/spawn_humans.dm:28
    public override async DmTask Run()
    {
        mob_living_carbon_human_consistent dummy = allocate<mob_living_carbon_human_consistent>();
        DmList bodyparts = (DmList)dummy.get_bodyparts();
        TEST_ASSERT(Dm.eq(((obj_item_bodypart)bodyparts[1]).body_zone, BODY_ZONE_CHEST), "First bodypart in bodyparts list is not the chest, this is important for human rendering");
        TEST_ASSERT(Dm.eq(((obj_item_bodypart)bodyparts[2]).body_zone, BODY_ZONE_HEAD), "Second bodypart in bodyparts list is not the head, this is important for human rendering");

        DmList bodyparts_by_zone = (DmList)dummy.get_bodyparts_by_zones();
        TEST_ASSERT(Dm.eq(bodyparts_by_zone[1], BODY_ZONE_CHEST), "First bodypart in bodyparts_by_zone list is not the chest, this is important for human rendering");
        TEST_ASSERT(Dm.eq(bodyparts_by_zone[2], BODY_ZONE_HEAD), "Second bodypart in bodyparts_by_zone list is not the head, this is important for human rendering");
    }
}
