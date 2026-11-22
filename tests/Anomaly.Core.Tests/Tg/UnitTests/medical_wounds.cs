// Translated from tgstation (snapshot 1d99bd7f): code/modules/unit_tests/medical_wounds.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tools;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.UnitTests;

// M6 slice 3 (the wound types, the ROADMAP's done-when test of M6): both tests of the file.
// ADAPTED: tg runs them on the unit test map; the room is the toy room (TgToyRoom.Install), and tg's strings/ files (the scar
// texts the full heals generate) are mounted as the server mounts them. tg reads the pregen data of iter_test_wound before the
// inner loop sets it (medical_wounds.dm:28, :67), so its threshold would come from no pregen data; each step here reads the
// pregen data of the wound it expects, which is what the comment on the threshold says it is ("just enough to guarantee the
// next tier of wound"). ADAPTED (syndicate G4c): a critical tier's roll may also pass check_wounding's outright dismemberment
// (bodyparts/wounds.dm), a chance that depends on the seed (the boot's mutation shuffle moved it when two mutations went live), so
// the first test's victim keeps its limbs (TRAIT_NODISMEMBER) and the tiers are what it tests.

/// This test is used to make sure a flesh-and-bone base human can suffer all the types of wounds, and that suffering more severe wounds removes and replaces the lesser wound. Also tests that [/mob/living/carbon/proc/fully_heal] removes all wounds
// tg: code/modules/unit_tests/medical_wounds.dm:2
public sealed class datum_unit_test_test_human_base : TgUnitTest
{
    public override void New()
    {
        base.New();
        TgToyRoom.Install(this);
        if (TgClone.Root != null)
            TgStrings.Mount(DmWorldState.Of(world_under_test).Files, TgClone.Root);
    }

    // tg: code/modules/unit_tests/medical_wounds.dm:2
    public override async DmTask Run()
    {
        mob_living_carbon_human victim = allocate<mob_living_carbon_human_consistent>();
        ADD_TRAIT(victim, TRAIT_NODISMEMBER, "unit_test"); // ADAPTED, see the header

        /// the limbs have no wound resistance like the chest and head do, so let's go with the r_arm
        obj_item_bodypart tested_part = (obj_item_bodypart)victim.get_bodypart(BODY_ZONE_R_ARM);
        /// In order of the wound types we're trying to inflict, what sharpness do we need to deal them?
        DmList sharps = Dm.list(NONE, SHARP_EDGED, SHARP_POINTY, NONE);
        /// Since burn wounds need burn damage, duh
        DmList dam_types = Dm.list(BRUTE, BRUTE, BRUTE, BURN);

        int i = 1;

        foreach (DmList iter_test_wound_list in Dm.each_cast<DmList>(Dm.list(
            Dm.list(typeof(datum_wound_blunt_bone_moderate), typeof(datum_wound_blunt_bone_severe), typeof(datum_wound_blunt_bone_critical)),
            Dm.list(typeof(datum_wound_slash_flesh_moderate), typeof(datum_wound_slash_flesh_severe), typeof(datum_wound_slash_flesh_critical)),
            Dm.list(typeof(datum_wound_pierce_bleed_moderate), typeof(datum_wound_pierce_bleed_severe), typeof(datum_wound_pierce_bleed_critical)),
            Dm.list(typeof(datum_wound_burn_flesh_moderate), typeof(datum_wound_burn_flesh_severe), typeof(datum_wound_burn_flesh_critical)))))
        {
            TEST_ASSERT_EQUAL(length(victim.all_wounds), 0, "Patient is somehow wounded before test");
            float threshold_penalty = 0;

            foreach (Type iter_test_wound in Dm.each_cast<Type>(iter_test_wound_list))
            {
                datum_wound_pregen_data iter_pregen_data = (datum_wound_pregen_data)GLOB.all_wound_pregen_data[iter_test_wound]; // ADAPTED, see the header
                float threshold = Dm.num(iter_pregen_data.threshold_minimum) - threshold_penalty; // just enough to guarantee the next tier of wound, given the existing wound threshold penalty
                if (Dm.eq(dam_types[i], BRUTE))
                    await tested_part.receive_damage(WOUND_MINIMUM_DAMAGE, 0, wound_bonus: threshold, sharpness: Dm.num(sharps[i]));
                else if (Dm.eq(dam_types[i], BURN))
                    await tested_part.receive_damage(0, WOUND_MINIMUM_DAMAGE, wound_bonus: threshold, sharpness: Dm.num(sharps[i]));

                TEST_ASSERT(Dm.truthy(length(victim.all_wounds)), Dm.text("Patient has no wounds when one wound is expected. Severity: []", Dm.initial(iter_test_wound, (datum_wound w) => w.severity)));
                TEST_ASSERT_EQUAL(length(victim.all_wounds), 1, Dm.text("Patient has more than one wound when only one is expected. Severity: []", Dm.initial(iter_test_wound, (datum_wound w) => w.severity)));
                datum_wound actual_wound = (datum_wound)victim.all_wounds[1];
                TEST_ASSERT_EQUAL(actual_wound.type, iter_test_wound, Dm.text("Patient has wound of incorrect severity. Expected: [] Got: []", Dm.initial(iter_test_wound, (datum_wound w) => w.name), actual_wound));
                threshold_penalty = Dm.num(actual_wound.threshold_penalty);
            }
            i++;
            await victim.fully_heal(ADMIN_HEAL_ALL); // should clear all wounds between types
        }
    }
}

/// This test is used for making sure species with bones but no flesh (skeletons, plasmamen) can only suffer BONE_WOUNDS, and nothing tagged with FLESH_WOUND (it's possible to require both)
// tg: code/modules/unit_tests/medical_wounds.dm:42
public sealed class datum_unit_test_test_human_bone : TgUnitTest
{
    public override void New()
    {
        base.New();
        TgToyRoom.Install(this);
        if (TgClone.Root != null)
            TgStrings.Mount(DmWorldState.Of(world_under_test).Files, TgClone.Root);
    }

    // tg: code/modules/unit_tests/medical_wounds.dm:42
    public override async DmTask Run()
    {
        mob_living_carbon_human victim = allocate<mob_living_carbon_human_consistent>();

        /// the limbs have no wound resistance like the chest and head do, so let's go with the r_arm
        obj_item_bodypart tested_part = (obj_item_bodypart)victim.get_bodypart(BODY_ZONE_R_ARM);
        /// In order of the wound types we're trying to inflict, what sharpness do we need to deal them?
        DmList sharps = Dm.list(NONE, SHARP_EDGED, SHARP_POINTY, NONE);
        /// Since burn wounds need burn damage, duh
        DmList dam_types = Dm.list(BRUTE, BRUTE, BRUTE, BURN);

        int i = 1;
        tested_part.biological_state = Dm.num(Dm.bitand(tested_part.biological_state, Dm.bitnot(BIO_FLESH))); // take away the base limb's flesh (ouchie!) ((not actually ouchie, this just affects their wounds and dismemberment handling))

        foreach (DmList iter_test_wound_list in Dm.each_cast<DmList>(Dm.list(
            Dm.list(typeof(datum_wound_blunt_bone_moderate), typeof(datum_wound_blunt_bone_severe), typeof(datum_wound_blunt_bone_critical)),
            Dm.list(typeof(datum_wound_slash_flesh_moderate), typeof(datum_wound_slash_flesh_severe), typeof(datum_wound_slash_flesh_critical)),
            Dm.list(typeof(datum_wound_pierce_bleed_moderate), typeof(datum_wound_pierce_bleed_severe), typeof(datum_wound_pierce_bleed_critical)),
            Dm.list(typeof(datum_wound_burn_flesh_moderate), typeof(datum_wound_burn_flesh_severe), typeof(datum_wound_burn_flesh_critical)))))
        {
            TEST_ASSERT_EQUAL(length(victim.all_wounds), 0, "Patient is somehow wounded before test");
            float threshold_penalty = 0;

            foreach (Type iter_test_wound in Dm.each_cast<Type>(iter_test_wound_list))
            {
                datum_wound_pregen_data iter_pregen_data = (datum_wound_pregen_data)GLOB.all_wound_pregen_data[iter_test_wound]; // ADAPTED, see the header
                float threshold = Dm.num(iter_pregen_data.threshold_minimum) - threshold_penalty; // just enough to guarantee the next tier of wound, given the existing wound threshold penalty
                if (Dm.eq(dam_types[i], BRUTE))
                    await tested_part.receive_damage(WOUND_MINIMUM_DAMAGE, 0, wound_bonus: threshold, sharpness: Dm.num(sharps[i]));
                else if (Dm.eq(dam_types[i], BURN))
                    await tested_part.receive_damage(0, WOUND_MINIMUM_DAMAGE, wound_bonus: threshold, sharpness: Dm.num(sharps[i]));

                // so if we just tried to deal a flesh wound, make sure we didn't actually suffer it. We may have suffered a bone wound instead, but we just want to make sure we don't have a flesh wound
                datum_wound_pregen_data pregen_data = (datum_wound_pregen_data)GLOB.all_wound_pregen_data[iter_test_wound];
                if (Dm.truthy(Dm.bitand(pregen_data.required_limb_biostate, BIO_FLESH)))
                {
                    if (!Dm.truthy(length(victim.all_wounds))) // not having a wound is good news
                        continue;
                    else // we have to check that it's actually a bone wound and not the intended wound type
                    {
                        TEST_ASSERT_EQUAL(length(victim.all_wounds), 1, Dm.text("Patient has more than one wound when only one is expected. Severity: []", Dm.initial(iter_test_wound, (datum_wound w) => w.severity)));
                        datum_wound actual_wound = (datum_wound)victim.all_wounds[1];
                        datum_wound_pregen_data actual_pregen_data = (datum_wound_pregen_data)GLOB.all_wound_pregen_data[actual_wound.type];
                        TEST_ASSERT(Dm.truthy(Dm.bitand(actual_pregen_data.required_limb_biostate, Dm.bitnot(BIO_FLESH))), Dm.text("Limb has flesh wound despite no BIO_FLESH biological_state, expected either no wound or bone wound. Offending wound: []", actual_wound));
                        threshold_penalty = Dm.num(actual_wound.threshold_penalty);
                    }
                }
                else // otherwise if it's a bone wound, check that we have it per usual
                {
                    TEST_ASSERT(Dm.truthy(length(victim.all_wounds)), Dm.text("Patient has no wounds when one wound is expected. Severity: []", Dm.initial(iter_test_wound, (datum_wound w) => w.severity)));
                    TEST_ASSERT_EQUAL(length(victim.all_wounds), 1, Dm.text("Patient has more than one wound when only one is expected. Severity: []", Dm.initial(iter_test_wound, (datum_wound w) => w.severity)));
                    datum_wound actual_wound = (datum_wound)victim.all_wounds[1];
                    TEST_ASSERT_EQUAL(actual_wound.type, iter_test_wound, Dm.text("Patient has wound of incorrect severity. Expected: [] Got: []", Dm.initial(iter_test_wound, (datum_wound w) => w.name), actual_wound));
                    threshold_penalty = Dm.num(actual_wound.threshold_penalty);
                }
            }
            i++;
            await victim.fully_heal(ADMIN_HEAL_ALL); // should clear all wounds between types
        }
    }
}
