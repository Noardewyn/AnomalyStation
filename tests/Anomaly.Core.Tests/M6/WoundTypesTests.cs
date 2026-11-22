using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tests.Tools;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M6;

/// <summary>
/// M6 slice 3, the wound types' own behaviour in a running world: a cut's blood flow, its clotting and its demotion to a
/// lighter cut (slash.dm), a compound fracture that disables the arm and drops what it held (bones.dm), a burn that breeds
/// infection or heals on a fed patient (burns.dm), a puncture's internal bleeding when hit again (pierce.dm), the cranial
/// fissure that spills the brain on a slip (cranial_fissure.dm), extreme heat burning a limb (_species.dm apply_burn_wounds)
/// and the heavy extinguisher's head bash (extinguisher.dm attack_secondary).
/// </summary>
public class WoundTypesTests
{
    private const int Second = 20;

    private static AudioRoom Room(ulong seed)
    {
        var r = new AudioRoom(12, 9, seed: seed);
        // tg's strings/ files (the scars' texts), as the server mounts them
        if (TgClone.Root != null)
            TgStrings.Mount(DmWorldState.Of(r.World).Files, TgClone.Root);
        r.In(() =>
        {
            for (int x = 2; x <= 11; x++)
            {
                for (int y = 2; y <= 8; y++)
                    Waiting.NoSleep(locate(x, y, 1).ChangeTurf(typeof(turf_open_floor_iron), Dm.list(typeof(turf_open_space), typeof(turf_open_floor_plating))));
            }
        });
        r.Step(Second);
        return r;
    }

    private static obj_item_bodypart Part(mob_living_carbon_human h, string zone) => (obj_item_bodypart)h.get_bodypart(zone);

    private static datum_wound Wound(AudioRoom r, mob_living_carbon_human h, string type, string zone, float severity) =>
        r.In(() => (datum_wound)Waiting.NoSleep(h.cause_wound_of_type_and_severity(type, Part(h, zone), severity)));

    [Fact]
    public void ACutBleedsAndClotsUntilItStopsBleeding()
    {
        var r = Room(6501);
        mob_living_carbon_human player = r.Join(6, 5, "Cut");
        var chat = new ChatCapture();
        PresentationSinks.Of(r.World).Messages = chat;
        var cut = (datum_wound_slash_flesh)Wound(r, player, WOUND_SLASH, BODY_ZONE_L_ARM, WOUND_SEVERITY_MODERATE);
        r.Step(1);
        float blood = r.In(() =>
        {
            Assert.IsType<datum_wound_slash_flesh_moderate>(cut);
            Assert.Equal(1.75f, Dm.num(cut.blood_flow));
            Assert.True(Dm.num(Part(player, BODY_ZONE_L_ARM).cached_bleed_rate) > 0, "the cut makes the arm bleed");
            return Dm.num(player.get_blood_volume());
        });
        r.Step(6 * Second);
        r.In(() =>
        {
            Assert.True(Dm.num(player.get_blood_volume()) < blood, "the patient loses blood");
            Assert.True(Dm.num(cut.blood_flow) < 1.75f, "the cut clots");
            Waiting.NoSleep(cut.adjust_blood_flow(0.55f - Dm.num(cut.blood_flow))); // just above its minimum flow of 0.5
        });
        r.Step(6 * Second);
        r.Step(2);
        r.In(() =>
        {
            Assert.True(QDELETED(cut), "a cut under its minimum flow with nothing to demote to is healed");
            Assert.Equal(0f, LAZYLEN(player.all_wounds));
        });
        Assert.Contains("The cut on your left arm has stopped bleeding!", string.Join("\n", chat.Html));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ASevereCutThatClotsDemotesToAModerateCutAndKeepsItsScar()
    {
        var r = Room(6502);
        var victim = r.Dummy(6, 5);
        r.Step(Second);
        var cut = (datum_wound_slash_flesh)Wound(r, victim, WOUND_SLASH, BODY_ZONE_R_LEG, WOUND_SEVERITY_SEVERE);
        r.Step(1);
        // the demotion is synchronous (replace_wound, slash.dm:242), so the lighter cut is read in the same call: a later tick may hold a Life
        // run (SSmobs' first fire is staggered by the Master's random 0 to 2 s, master.dm:621) that clots it by its clot_rate * 2 s
        r.In(() =>
        {
            Assert.IsType<datum_wound_slash_flesh_severe>(cut);
            Assert.NotNull(cut.highest_scar);
            datum_scar scar = cut.highest_scar;
            Waiting.NoSleep(cut.adjust_blood_flow(-(Dm.num(cut.blood_flow) - 1.9f))); // under its minimum flow of 2
            Assert.True(QDELETED(cut));
            var lighter = Dm.each_cast<datum_wound>(victim.all_wounds).Single();
            Assert.IsType<datum_wound_slash_flesh_moderate>(lighter);
            Assert.Same(scar, ((datum_wound_slash_flesh)lighter).highest_scar);
            Assert.Equal(1.9f, Dm.num(lighter.blood_flow), 3); // max(the old flow, its initial 1.75)
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ACompoundFractureDisablesTheArmAndDropsWhatItHeld()
    {
        var r = Room(6503);
        var victim = r.Dummy(6, 5);
        r.Step(Second);
        obj_item_wrench wrench = r.In(() =>
        {
            var made = Dm.@new<obj_item_wrench>(locate(6, 5, 1));
            Assert.True(Dm.truthy(Waiting.NoSleep(victim.put_in_r_hand(made))));
            return made;
        });
        r.Step(1);
        datum_wound fracture = Wound(r, victim, WOUND_BLUNT, BODY_ZONE_R_ARM, WOUND_SEVERITY_CRITICAL);
        r.Step(1);
        r.In(() =>
        {
            Assert.IsType<datum_wound_blunt_bone_critical>(fracture);
            Assert.True(Dm.truthy(fracture.disabling));
            Assert.True(HAS_TRAIT(Part(victim, BODY_ZONE_R_ARM), TRAIT_PARALYSIS), "the arm is paralysed");
            Assert.False(Dm.@in(wrench, victim.held_items), "the arm dropped the wrench");
            Assert.Same(victim.loc, wrench.loc);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AnUntreatedThirdDegreeBurnBreedsInfection()
    {
        var r = Room(6504);
        var victim = r.Dummy(6, 5);
        r.Step(Second);
        var burn = (datum_wound_burn_flesh)Wound(r, victim, WOUND_BURN, BODY_ZONE_CHEST, WOUND_SEVERITY_SEVERE);
        r.Step(20 * Second);
        r.In(() =>
        {
            Assert.IsType<datum_wound_burn_flesh_severe>(burn);
            Assert.False(QDELETED(burn));
            Assert.True(Dm.num(burn.infection) > 0, "the burn's infection grows: " + Dm.num(burn.infection));
            Assert.Contains(" Burn / ", (string)burn.severity_text());
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ASecondDegreeBurnOnAFedPatientClearsUp()
    {
        var r = Room(6505);
        mob_living_carbon_human player = r.Join(6, 5, "Burnt");
        var chat = new ChatCapture();
        PresentationSinks.Of(r.World).Messages = chat;
        r.In(() => Waiting.NoSleep(player.set_nutrition(NUTRITION_LEVEL_FED + 50)));
        var burn = (datum_wound_burn_flesh)Wound(r, player, WOUND_BURN, BODY_ZONE_L_LEG, WOUND_SEVERITY_MODERATE);
        r.Step(60 * Second);
        r.In(() =>
        {
            Assert.True(QDELETED(burn), "the burn heals: flesh damage " + burn.flesh_damage + ", healing " + burn.flesh_healing);
            Assert.Equal(0f, LAZYLEN(player.all_wounds));
        });
        Assert.Contains("The burns on your left leg have cleared up!", string.Join("\n", chat.Html));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AnOpenPunctureBleedsInsideWhenTheLimbIsHitAgain()
    {
        var r = Room(6506);
        var victim = r.Dummy(6, 5);
        r.Step(Second);
        datum_wound puncture = Wound(r, victim, WOUND_PIERCE, BODY_ZONE_CHEST, WOUND_SEVERITY_SEVERE);
        // read in the tick of the wound: a Life run clots it (SSmobs' first fire is staggered by the Master's random 0 to 2 s, master.dm:621)
        r.In(() =>
        {
            Assert.IsType<datum_wound_pierce_bleed_severe>(puncture);
            Assert.Equal(2f, Dm.num(puncture.blood_flow));
            float blood = Dm.num(victim.get_blood_volume());
            for (int i = 0; i < 6; i++)
                Waiting.NoSleep(Part(victim, BODY_ZONE_CHEST).receive_damage(brute: 15, wound_bonus: CANT_WOUND));
            Assert.True(Dm.num(victim.get_blood_volume()) < blood, "the hits bleed the patient inside");
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ACranialFissureSpillsTheBrainWhenThePatientSlips()
    {
        var r = Room(6507);
        var victim = r.Dummy(6, 5);
        r.Step(Second);
        datum_wound_cranial_fissure fissure = r.In(() =>
        {
            var made = (datum_wound_cranial_fissure)Waiting.NoSleep(Dm.new_async(typeof(datum_wound_cranial_fissure)));
            Waiting.NoSleep(made.apply_wound(Part(victim, BODY_ZONE_HEAD)));
            return made;
        });
        r.Step(1);
        obj_item_organ_brain brain = r.In(() =>
        {
            Assert.True(HAS_TRAIT(victim, TRAIT_HAS_CRANIAL_FISSURE));
            Assert.True(HAS_TRAIT(Part(victim, BODY_ZONE_HEAD), TRAIT_IMMUNE_TO_CRANIAL_FISSURE));
            var held = (obj_item_organ_brain)victim.get_organ_by_type(typeof(obj_item_organ_brain));
            Assert.NotNull(held);
            Waiting.NoSleep(victim.slip(1 * SECONDS));
            return held;
        });
        r.Step(Second);
        r.In(() =>
        {
            Assert.Null(victim.get_organ_by_type(typeof(obj_item_organ_brain)));
            Assert.IsAssignableFrom<turf>(brain.loc);
            Assert.True(get_dist(brain, victim) <= 1, "the brain falls next to the patient");
            Waiting.NoSleep(fissure.remove_wound());
            Assert.False(HAS_TRAIT(victim, TRAIT_HAS_CRANIAL_FISSURE));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ExtremeHeatBurnsALimb()
    {
        var r = Room(6508);
        var victim = r.Dummy(6, 5);
        r.Step(Second);
        r.In(() =>
        {
            victim.bodytemperature = BODYTEMP_HEAT_WOUND_LIMIT + 10;
            Waiting.NoSleep(((datum_species)victim.dna.species).apply_burn_wounds(victim, 2));
            datum_wound burn = Dm.each_cast<datum_wound>(victim.all_wounds).Single();
            Assert.IsType<datum_wound_burn_flesh_moderate>(burn);
            Assert.Equal("hot temperatures", burn.wound_source);

            // a hotter body worsens that limb's burn, not a second one
            victim.bodytemperature = BODYTEMP_HEAT_WOUND_LIMIT + 500;
            obj_item_bodypart burnt = burn.limb;
            for (int i = 0; i < 40 && burnt.wounds != null && !Dm.each_cast<datum_wound>(burnt.wounds).Any(w => w is datum_wound_burn_flesh_severe); i++)
                Waiting.NoSleep(((datum_species)victim.dna.species).apply_burn_wounds(victim, 2));
            Assert.Contains(Dm.each_cast<datum_wound>(burnt.wounds), w => w is datum_wound_burn_flesh_severe);
            Assert.Single(Dm.each_cast<datum_wound>(burnt.wounds), w => w is datum_wound_burn_flesh);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AHeavyExtinguisherBashesTheHead()
    {
        var r = Room(6509);
        mob_living_carbon_human attacker = r.Join(5, 5, "Basher");
        var victim = r.Dummy(6, 5);
        r.Step(Second);
        obj_item_extinguisher extinguisher = r.In(() =>
        {
            var made = Dm.@new<obj_item_extinguisher>(locate(5, 5, 1));
            Assert.True(Dm.truthy(Waiting.NoSleep(attacker.put_in_active_hand(made))));
            return made;
        });
        r.Step(1);
        float brute_before = r.In(() => Dm.num(Part(victim, BODY_ZONE_HEAD).brute_dam));
        object result = null;
        r.In(() => Waiting.Detach(Bash()));
        async DmTask Bash() => result = await extinguisher.attack_secondary(victim, attacker);
        r.Step(3 * Second);
        r.In(() =>
        {
            Assert.Equal((object)SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN, result);
            Assert.True(Dm.num(Part(victim, BODY_ZONE_HEAD).brute_dam) > brute_before, "the head takes the blow");
        });
        r.AssertNoRuntimes();
    }
}
