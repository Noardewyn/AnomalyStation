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
/// M6 slice 4, dismemberment (surgery/bodyparts/dismemberment.dm, datums/wounds/loss.dm) in a running world: a lost arm drops
/// what it held, leaves a stump, lands on the floor bloodied and cannot hold again; lost legs make a person crawl and drop the
/// shoes; a split chest spills its organs; a lost head takes the brain and drops the hat; a mangled limb is severed by a heavy
/// cut (the loss wound's message) and grows back; the attack message warns of the severing; a limb destroyer's
/// strike tears off the most wrecked limb.
/// </summary>
public class DismembermentTests
{
    private const int Second = 20;

    private static AudioRoom Room(ulong seed)
    {
        var r = new AudioRoom(12, 9, seed: seed);
        // tg's strings/ files (the scars' texts of the regrown limbs and of the wounds), as the server mounts them
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

    private static obj_item_bodypart Part(mob_living_carbon who, string zone, bool stumps = false)
        => who.get_bodypart(zone, include_stumps: stumps ? TRUE : FALSE) as obj_item_bodypart;

    [Fact]
    public void ALostArmDropsWhatItHeldLeavesAStumpAndCannotHoldAgain()
    {
        var r = Room(6701);
        mob_living_carbon_human victim = r.Join(6, 5, "Victim");
        r.Step(Second);
        obj_item_bodypart arm = null;
        obj_item_wrench wrench = r.In(() =>
        {
            var made = Dm.@new<obj_item_wrench>(victim.loc);
            Assert.True(Dm.truthy(Waiting.NoSleep(victim.put_in_l_hand(made))));
            arm = Part(victim, BODY_ZONE_L_ARM);
            return made;
        });
        float blood_before = r.In(() => Dm.num(victim.get_blood_volume()));

        string line = r.In(() => DevCommands.Dismember(victim, BODY_ZONE_L_ARM));
        Assert.Contains("l_arm dismembered", line, StringComparison.Ordinal);
        r.Step(Second);
        r.In(() =>
        {
            Assert.Null(Part(victim, BODY_ZONE_L_ARM));
            Assert.IsType<obj_item_bodypart_arm_left_stump>(Part(victim, BODY_ZONE_L_ARM, stumps: true));
            Assert.Null(arm.owner);
            Assert.True(isturf(arm.loc), "the severed arm lies on the floor");
            Assert.True(get_dist(arm, victim) >= 1, "it was thrown away from its owner");
            Assert.True(Dm.truthy(GET_ATOM_BLOOD_DNA(arm)), "the severed arm is bloody");
            Assert.True(isturf(wrench.loc), "the lost hand dropped the wrench");
            Assert.Equal((1f, 1f), (victim.num_hands, victim.usable_hands));
            Assert.True(Dm.num(Part(victim, BODY_ZONE_CHEST).brute_dam) >= 15f, "the chest takes the shock (15 to 50 brute)");
            float lost = blood_before - Dm.num(victim.get_blood_volume());
            Assert.True(lost >= 20f, $"the stump gushes 20 to 40 units of blood, lost {lost}");
            Assert.False(Dm.truthy(Waiting.NoSleep(victim.put_in_l_hand(wrench))), "a missing hand holds nothing");
        });
        Assert.NotEmpty(r.Sounds.Of(victim, "sound/effects/dismember.ogg"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void LostLegsMakeAPersonCrawlAndDropTheShoes()
    {
        var r = Room(6702);
        mob_living_carbon_human victim = r.Dummy(6, 5);
        r.Step(Second);
        obj_item_clothing_shoes_sneakers_black shoes = r.In(() =>
        {
            var made = Dm.@new<obj_item_clothing_shoes_sneakers_black>(victim.loc);
            Assert.True(Dm.truthy(Waiting.NoSleep(victim.equip_to_slot_if_possible(made, ITEM_SLOT_FEET))));
            return made;
        });
        r.In(() =>
        {
            DevCommands.Dismember(victim, BODY_ZONE_L_LEG);
            DevCommands.Dismember(victim, BODY_ZONE_R_LEG);
        });
        r.Step(Second);
        r.In(() =>
        {
            Assert.Equal((0f, 0f), (victim.num_legs, victim.usable_legs));
            Assert.True(isturf(shoes.loc), "the shoes fell off with the feet");
            Assert.Equal(LYING_DOWN, victim.body_position);
            Assert.NotNull(victim.has_movespeed_modifier(typeof(datum_movespeed_modifier_limbless)));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ASplitChestSpillsItsOrgans()
    {
        var r = Room(6703);
        mob_living_carbon_human victim = r.Join(6, 5, "Victim");
        r.Step(Second);
        obj_item_organ heart = r.In(() => victim.get_organ_slot(ORGAN_SLOT_HEART) as obj_item_organ);
        r.In(() => DevCommands.Dismember(victim, BODY_ZONE_CHEST));
        r.Step(Second);
        r.In(() =>
        {
            Assert.NotNull(Part(victim, BODY_ZONE_CHEST)); // the chest stays: only its organs spill
            Assert.Null(victim.get_organ_slot(ORGAN_SLOT_HEART));
            Assert.Null(heart.owner);
            Assert.Same(victim.loc, heart.loc);
        });
        Assert.NotEmpty(r.Sounds.Of(victim, "sound/misc/splort.ogg"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ALostHeadTakesTheBrainAndDropsTheHat()
    {
        var r = Room(6704);
        mob_living_carbon_human victim = r.Dummy(6, 5);
        r.Step(Second);
        obj_item_clothing_head_helmet helmet = r.In(() =>
        {
            victim.real_name = "Bob Smith";
            var made = Dm.@new<obj_item_clothing_head_helmet>(victim.loc);
            Assert.True(Dm.truthy(Waiting.NoSleep(victim.equip_to_slot_if_possible(made, ITEM_SLOT_HEAD))));
            return made;
        });
        obj_item_bodypart head = r.In(() => Part(victim, BODY_ZONE_HEAD));
        Assert.False(r.In(() => Dm.truthy(head.can_dismember__proc())), "tg's head cannot be cut off a living person by wounds");
        r.In(() => DevCommands.Dismember(victim, BODY_ZONE_HEAD));
        r.Step(Second);
        r.In(() =>
        {
            Assert.Null(Part(victim, BODY_ZONE_HEAD));
            Assert.IsType<obj_item_bodypart_head_stump>(Part(victim, BODY_ZONE_HEAD, stumps: true));
            Assert.Equal("Bob Smith's head", Dm.str(head.name));
            Assert.True(isturf(helmet.loc), "the helmet fell off");
            Assert.NotNull(Dm.locate<obj_item_organ_brain>(head));
            Assert.Null(victim.get_organ_slot(ORGAN_SLOT_BRAIN));
            Assert.Equal(DEAD, Dm.num(victim.stat));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AHeavyCutSeversAMangledArmWhichGrowsBack()
    {
        var r = Room(6705);
        mob_living_carbon_human victim = r.Join(6, 5, "Victim");
        var chat = new ChatCapture();
        PresentationSinks.Of(r.World).Messages = chat;
        r.Step(Second);
        obj_item_bodypart arm = r.In(() => Part(victim, BODY_ZONE_R_ARM));
        r.In(() =>
        {
            DevCommands.Wound(victim, BODY_ZONE_R_ARM, WOUND_SLASH, WOUND_SEVERITY_CRITICAL);
            DevCommands.Wound(victim, BODY_ZONE_R_ARM, WOUND_BLUNT, WOUND_SEVERITY_CRITICAL);
            Assert.Equal(BODYPART_MANGLED_BOTH, Dm.num(arm.mangled_state));
            // the strike's wounding damage alone is above 100 percent (try_dismember)
            Waiting.NoSleep(arm.receive_damage(110, 0, sharpness: SHARP_EDGED));
        });
        r.Step(Second);
        r.In(() =>
        {
            Assert.Null(Part(victim, BODY_ZONE_R_ARM));
            Assert.Null(arm.owner);
            // dismember records the wounding type (dismemberment.dm:25), and the stump that drop_limb attaches takes it off at
            // once (try_attach_limb, dismemberment.dm:272): tg's regrown limb never gets the loss scar of regenerate_limb
            Assert.Null(victim.body_zone_dismembered_by?[BODY_ZONE_R_ARM]);
            // the lost arm's wounds leave the victim (remove_wound(ignore_limb = TRUE)) and stay on the arm, which reapplies them
            // if it is attached again (try_attach_limb, dismemberment.dm:282)
            Assert.Empty(Dm.each_cast<datum_wound>(victim.all_wounds));
            Assert.Equal(2, Dm.each_cast<datum_wound>(arm.wounds).Count());
        });
        Assert.Contains(chat.Html, h => h.Contains("is slashed through the last", StringComparison.Ordinal) && h.Contains("severing it completely!", StringComparison.Ordinal));

        r.In(() => Waiting.NoSleep(victim.fully_heal(HEAL_ALL)));
        r.Step(Second);
        r.In(() =>
        {
            obj_item_bodypart regrown = Part(victim, BODY_ZONE_R_ARM);
            Assert.NotNull(regrown);
            Assert.NotSame(arm, regrown);
            Assert.Equal((2f, 2f), (victim.num_hands, victim.usable_hands));
            Assert.Empty(Dm.each_cast<datum_scar>(regrown.scars)); // see above
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheAttackMessageWarnsOfTheSevering()
    {
        var r = Room(6706);
        mob_living_carbon_human attacker = r.Join(5, 5, "Attacker");
        mob_living_carbon_human victim = r.Join(6, 5, "Victim");
        var chat = new ChatCapture();
        PresentationSinks.Of(r.World).Messages = chat;
        r.Step(Second);
        r.In(() =>
        {
            victim.real_name = victim.name = "Victim";
            attacker.real_name = attacker.name = "Attacker";
            var axe = Dm.@new<obj_item_fireaxe>(attacker.loc);
            DevCommands.Wound(victim, BODY_ZONE_L_LEG, WOUND_SLASH, WOUND_SEVERITY_CRITICAL);
            DevCommands.Wound(victim, BODY_ZONE_L_LEG, WOUND_BLUNT, WOUND_SEVERITY_CRITICAL);
            Assert.True(Dm.truthy(victim.send_item_attack_message(axe, attacker, "left leg", BODY_ZONE_L_LEG)));
        });
        // the attacker is in view, so tg's texts name it (carbon_defense.dm:104)
        Assert.Contains(chat.Html, h => h.Contains("Attacker", StringComparison.Ordinal) && h.Contains("you in the left leg with", StringComparison.Ordinal)
            && h.Contains(", threatening to sever it entirely!", StringComparison.Ordinal));
        Assert.Contains(chat.Html, h => h.Contains("You ", StringComparison.Ordinal) && h.Contains("Victim in the left leg with", StringComparison.Ordinal)
            && h.Contains(", threatening to sever it entirely!", StringComparison.Ordinal));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ALimbDestroyersStrikeTearsOffTheMostWreckedLimb()
    {
        var r = Room(6707);
        mob_living_carbon_human attacker = r.Dummy(5, 5);
        mob_living_carbon_human victim = r.Dummy(6, 5);
        r.Step(Second);
        r.In(() =>
        {
            obj_item_bodypart leg = Part(victim, BODY_ZONE_R_LEG);
            Waiting.NoSleep(leg.receive_damage(leg.max_damage, 0, wound_bonus: CANT_WOUND));
            // not a limb destroyer: the zone comes back and nothing happens
            Assert.Equal(BODY_ZONE_CHEST, Waiting.NoSleep(victim.dismembering_strike(attacker, BODY_ZONE_CHEST)));
            Assert.NotNull(Part(victim, BODY_ZONE_R_LEG));
            attacker.limb_destroyer = 1;
            // an attacker without a client ignores the zone and goes for the most damaged arm or leg; null means it came off
            Assert.Null(Waiting.NoSleep(victim.dismembering_strike(attacker, BODY_ZONE_CHEST)));
            Assert.Null(Part(victim, BODY_ZONE_R_LEG));
            Assert.Null(leg.owner);
        });
        r.AssertNoRuntimes();
    }
}
