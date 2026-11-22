using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tests.Gaps.GapsHarness;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package DE1 (D333): the household odds in a booted toy room with Bob as a player: the pillow fight (the bonk and its
/// stamina, the brick, the tag; the smothering; the pillow suit's charge; the mattress shield's stance), the dice (rolled, shown,
/// rigged by a microwave, the special faces, the dice bags), the shooting targets (the marks a hit leaves, the welder, the clown's
/// honk), the banners (the department's flourish once a minute, Meditopia's extra, the mundane banner) and the godstaff, and the
/// inert leaves of the melee file made live (the curator's whip, the bee sword, the roasting stick, the bamboo staff, the weak
/// claymore) with skub in a tank holder.
/// </summary>
public class HouseholdDE1Tests
{
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(13, 9, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        r.Step(Second);
        return (r, feed, bob);
    }

    private static T Hold<T>(AudioRoom r, mob_living_carbon_human who) where T : obj_item
    {
        T item = r.In(() => Dm.@new<T>(who.loc));
        r.Step(2);
        r.In(() => Waiting.NoSleep(who.put_in_hands(item)));
        r.In(() => Assert.Same(item, who.get_active_held_item()));
        return item;
    }

    private static void Drop(AudioRoom r, mob_living_carbon_human who) => r.In(() =>
    {
        if (who.get_active_held_item() is obj_item held)
            qdel(held);
    });

    // a left click after tg's click cooldown of the one before
    private static void Use(AudioRoom r, mob who, atom target, string modifiers = "")
    {
        r.Step(Second);
        Click(r, who, target, modifiers);
    }

    private static void RightClick(AudioRoom r, mob who, atom target)
    {
        r.Step(Second);
        r.Command(() => ClientSeam.Click(who.client, target, "icon-x=16;icon-y=16;right=1;button=right"));
        r.Step(2);
    }

    // the use-in-hand key (Z, tg's activate_inhand: the held item's attack_self), pressed and released
    private static void UseInHand(AudioRoom r, mob who)
    {
        r.Step(Second / 2);
        r.Command(() => ClientSeam.Keybinding(who.client, 4, true));
        r.Step(2);
        r.Command(() => ClientSeam.Keybinding(who.client, 4, false));
        r.Step(2);
    }

    private static List<string> Overlays(AudioRoom r, atom a) => r.In(() => Dm.each(a.overlays).Select(o => o is IDmAppearance x ? x.icon_state : Dm.str(o)).ToList());

    private static bool Heard(AudioRoom r, string file) => r.Sounds.Heard.Any(h => (h.File ?? "").Contains(file, StringComparison.Ordinal));

    private static float Stamina(AudioRoom r, mob_living who) => r.In(() => Dm.num(who.get_stamina_loss()));

    // a projectile of that type fired east along the row from x at the target it counts as aimed at (its original)
    private static void Shoot<T>(AudioRoom r, int x, int y, atom target) where T : obj_projectile
    {
        r.In(() =>
        {
            T p = Dm.@new<T>(locate(x, y, 1));
            p.original = target;
            Waiting.NoSleep(p.fire(90));
        });
        r.Step(Second);
    }

    [Fact]
    public void ThePillowBonksForStaminaTakesABrickAndGivesUpItsTagToTheLastFighter()
    {
        var (r, feed, bob) = Room(8611);
        var dummy = r.Dummy(5, 4);
        var pillow = Hold<obj_item_pillow>(r, bob);
        r.In(() => Assert.Equal("pillow_1_t", pillow.icon_state));

        // the bonk: the victim's stamina, the swinger's own, tg's soft hit and its puff
        r.In(() => bob.set_combat_mode(TRUE));
        Use(r, bob, dummy);
        r.In(() => bob.set_combat_mode(FALSE));
        Assert.True(Stamina(r, dummy) > 0, "the pillow did no stamina damage");
        Assert.Equal(5f, Stamina(r, bob));
        Assert.True(Heard(r, "pillow/pillow_hit.ogg"));
        r.In(() =>
        {
            Assert.Same(bob, pillow.last_fighter);
            Assert.NotEmpty(Dm.each<obj_effect_temp_visual_pillow_hit>(locate(5, 4, 1).contents));
        });

        // a sandstone brick goes in: heavier
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(pillow)));
        Hold<obj_item_stack_sheet_mineral_sandstone>(r, bob);
        Use(r, bob, pillow);
        Assert.True(feed.Any("balloon: inserting brick..."));
        r.Step(2 * Second + 5);
        Assert.True(feed.Any("balloon: bricked!"));
        r.In(() =>
        {
            Assert.True(Dm.truthy(pillow.bricked));
            Assert.Equal(10f, pillow.force);
            Assert.Null(bob.get_active_held_item());
        });
        string examined = r.In(() => string.Join("\n", Dm.each(Waiting.NoSleep(pillow.examine(bob))).Select(Dm.str)));
        Assert.Contains("unnaturally heavy", examined, StringComparison.Ordinal);

        // the alt-click takes its tag (two seconds), naming the last fighter; the tag goes back on
        Use(r, bob, pillow, "alt=1");
        Assert.True(feed.Any("balloon: removing tag..."));
        r.Step(2 * Second + 5);
        var tag = r.In(() => Assert.IsType<obj_item_clothing_neck_pillow_tag>(bob.get_active_held_item()));
        Assert.True(feed.Any("balloon: tag removed"));
        Assert.True(Heard(r, "poster_ripped"));
        r.In(() =>
        {
            Assert.Null(pillow.pillow_trophy);
            Assert.Equal("pillow_1", pillow.icon_state);
            Assert.Equal("pillow_no_t", Dm.str(pillow.inhand_icon_state));
            Assert.Contains("tag removed", pillow.desc, StringComparison.Ordinal);
            Assert.Contains(bob.name, tag.desc, StringComparison.Ordinal);
        });
        Use(r, bob, pillow);
        Assert.True(feed.Any("balloon: honor reclaimed!"));
        r.In(() =>
        {
            Assert.Same(tag, pillow.pillow_trophy);
            Assert.Equal("pillow_1_t", pillow.icon_state);
        });

        // the bricked pillow hits hard
        r.In(() => Waiting.NoSleep(bob.put_in_hands(pillow)));
        r.In(() => bob.set_combat_mode(TRUE));
        Use(r, bob, dummy);
        Assert.True(Heard(r, "pillow/pillow_hit2.ogg"));

        // the random pillows pick a pattern
        for (int i = 0; i < 4; i++)
        {
            var random = r.In(() => Dm.@new<obj_item_pillow_random>(locate(6 + i, 6, 1)));
            r.In(() =>
            {
                Assert.InRange(random.variation, 1f, 4f);
                Assert.Equal(Dm.text("pillow_[]_t", random.variation), random.icon_state);
            });
        }
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ThePillowSmothersTheLyingTheSuitChargesWithItsPillowAndTheMattressHunkersDown()
    {
        var (r, feed, bob) = Room(8612);
        var lying = r.Dummy(5, 4);
        r.In(() => Waiting.NoSleep(lying.Paralyze(100 * SECONDS)));
        r.Step(2);
        r.In(() => Assert.Equal((float)LYING_DOWN, Dm.num(lying.body_position)));
        Hold<obj_item_pillow>(r, bob);

        // the right click on a lying man: smothered while Bob keeps at it, a breath lost each second
        RightClick(r, bob, lying);
        Assert.True(feed.Any("You begin smothering"));
        r.Step(3 * Second + 5);
        r.In(() => Assert.True(lying.losebreath >= 2, "losebreath " + lying.losebreath));
        r.In(() => Waiting.NoSleep(bob.forceMove(locate(3, 4, 1)))); // Bob steps back: the do-after fails
        r.Step(2 * Second);
        Assert.True(feed.Any("manages to escape being smothered!"));
        r.In(() => Waiting.NoSleep(bob.forceMove(locate(4, 4, 1))));
        Drop(r, bob);

        // the pillow suit worn: a bump into a man attacks him with its pillow
        var suit = r.In(() => Dm.@new<obj_item_clothing_suit_pillow_suit>(bob.loc));
        r.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(bob.equip_to_slot_if_possible(suit, ITEM_SLOT_OCLOTHING)))));
        var standing = r.Dummy(4, 5);
        r.Step(Second);
        float before = Stamina(r, standing);
        r.In(() => Waiting.NoSleep(bob.Move(locate(4, 5, 1), NORTH)));
        r.Step(4);
        Assert.True(feed.Any("You charge into"));
        Assert.True(Stamina(r, standing) > before, "the suit's pillow did no stamina damage");

        // the mattress shield's stance: harder, slower, the red outline; dropping it ends it
        var mattress = Hold<obj_item_shield_mattress>(r, bob);
        float force = r.In(() => mattress.force);
        float block = r.In(() => Dm.num(mattress.block_chance));
        r.In(() => Waiting.NoSleep(mattress.ui_action_click(bob, null)));
        Assert.True(feed.Any("hunkers down into a defensive stance!"));
        r.In(() =>
        {
            Assert.True(Dm.truthy(mattress.hunkered));
            Assert.Equal(force + 5, mattress.force);
            Assert.Equal(block + 20, Dm.num(mattress.block_chance));
            Assert.True(HAS_TRAIT(bob, TRAIT_BRAWLING_KNOCKDOWN_BLOCKED));
            Assert.NotNull(bob.get_filter("ANGRY_GLOW"));
        });
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(mattress)));
        Assert.True(feed.Any("loosens up and relaxes a bit."));
        r.In(() =>
        {
            Assert.False(Dm.truthy(mattress.hunkered));
            Assert.Equal(force, mattress.force);
            Assert.False(HAS_TRAIT(bob, TRAIT_BRAWLING_KNOCKDOWN_BLOCKED));
            Assert.Null(bob.get_filter("ANGRY_GLOW"));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheDiceRollShowTheirFaceAreRiggedByAMicrowaveAndTheBagsHoldASet()
    {
        var (r, feed, bob) = Room(8613);
        var d20 = Hold<obj_item_dice_d20>(r, bob);
        r.In(() =>
        {
            Assert.InRange(Dm.num(d20.result), 1f, 20f);
            Assert.Contains(Dm.text("d20-[]", d20.result), Dm.each(d20.overlays).Select(o => o is IDmAppearance x ? x.icon_state : Dm.str(o)));
        });

        // rolled in the hand: tg's message and sound, the new face on top
        UseInHand(r, bob);
        Assert.True(feed.Any("It lands on"));
        Assert.True(Heard(r, "dice_roll"));
        float rolled = r.In(() => Dm.num(d20.result));
        Assert.Contains(Dm.text("d20-[]", rolled), Overlays(r, d20));

        // a microwave rigs it to that face; a totally rigged die always shows it
        r.In(() =>
        {
            d20.microwave_act(null, null);
            Assert.Equal((float)DICE_BASICALLY_RIGGED, d20.rigged);
            Assert.Equal(rolled, Dm.num(d20.rigged_value));
            d20.rigged = DICE_TOTALLY_RIGGED;
        });
        for (int i = 0; i < 4; i++)
        {
            UseInHand(r, bob);
            r.In(() => Assert.Equal(rolled, Dm.num(d20.result)));
        }

        // the special dice: the d00's tens, the fudge die's faces
        var d00 = r.In(() => Dm.@new<obj_item_dice_d00>(bob.loc));
        var fudge = r.In(() => Dm.@new<obj_item_dice_fudge>(bob.loc));
        for (int i = 0; i < 5; i++)
        {
            r.In(() =>
            {
                d00.diceroll(bob);
                float tens = Dm.num(d00.result);
                Assert.True(tens % 10 == 0 && tens >= 0 && tens <= 90, "d00 " + tens);
                fudge.diceroll(bob);
                Assert.Contains(Dm.str(fudge.result), new[] { "minus", "blank", "plus" });
            });
        }
        Assert.True(feed.Any("rolls to a stop, landing on"));

        // the d4 is a caltrop
        var d4 = r.In(() => Dm.@new<obj_item_dice_d4>(bob.loc));
        r.In(() => Assert.NotNull(d4.GetComponent(typeof(datum_component_caltrop))));

        // the dice bag: a set and one odd die; the hazard bag: five d6
        var bag = r.In(() => Dm.@new<obj_item_storage_dice>(locate(6, 6, 1)));
        var hazard = r.In(() => Dm.@new<obj_item_storage_dice_hazard>(locate(7, 6, 1)));
        r.Step(2);
        r.In(() =>
        {
            List<obj_item_dice> dice = Dm.each<obj_item_dice>(bag.contents).ToList();
            Assert.Equal(7, dice.Count);
            Assert.Equal(new[] { typeof(obj_item_dice_d4), typeof(obj_item_dice_d6), typeof(obj_item_dice_d8), typeof(obj_item_dice_d10), typeof(obj_item_dice_d12), typeof(obj_item_dice_d20) },
                dice.Take(6).Select(d => d.GetType()));
            Assert.All(Dm.each<obj_item>(hazard.contents), d => Assert.IsAssignableFrom<obj_item_dice_d6>(d));
            Assert.Equal(5, Dm.each<obj_item>(hazard.contents).Count());
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheShootingTargetTakesMarksWhereItIsHitIsWeldedCleanAndTheClownOneHonks()
    {
        var (r, feed, bob) = Room(8614);
        var target = Place<obj_item_target>(r, 8, 6);

        // a laser leaves its scorch, a bullet its dent; a quarter of the hits leave nothing
        for (int i = 0; i < 6; i++)
            Shoot<obj_projectile_beam_laser>(r, 5, 6, target);
        for (int i = 0; i < 4; i++)
            Shoot<obj_projectile_bullet>(r, 5, 6, target);
        List<string> marks = r.In(() => Dm.each<image>(target.bullethole_overlays).Select(i => i.icon_state).ToList());
        Assert.Contains("scorch", marks);
        Assert.Contains("dent", marks);
        Assert.True(marks.Count < 10, "every hit left a mark");
        List<string> shown = Overlays(r, target);
        Assert.Contains("scorch", shown);
        Assert.Contains("dent", shown);

        // a practice beam leaves none, as in tg: the object's bullet_act answers a hit that did no damage as a block before the
        // target's marks (its always_leave_marks list is never reached)
        int count = marks.Count;
        for (int i = 0; i < 4; i++)
            Shoot<obj_projectile_beam_practice>(r, 5, 6, target);
        r.In(() => Assert.Equal(count, Dm.each(target.bullethole_overlays).Count()));

        // a projectile not aimed at the target flies on (DV522: the pixel test of tg's sprite is not asked)
        List<object> answers = r.In(() => Enumerable.Range(0, 5).Select(_ =>
        {
            var stray = Dm.@new<obj_projectile_bullet>(locate(5, 6, 1));
            object answer = Waiting.NoSleep(target.bullet_act(stray));
            qdel(stray);
            return answer;
        }).ToList());
        Assert.Contains(answers, a => Dm.eq(a, BULLET_ACT_FORCE_PIERCE));
        r.In(() => Assert.Equal(count, Dm.each(target.bullethole_overlays).Count()));

        // a lit welder clears the marks
        r.In(() => Waiting.NoSleep(target.forceMove(locate(5, 4, 1))));
        var welder = Hold<obj_item_weldingtool_largetank>(r, bob);
        r.In(() => welder.switched_on(bob));
        Use(r, bob, target);
        r.Step(Second);
        Assert.True(feed.Any("balloon: target repaired"));
        r.In(() => Assert.Null(target.bullethole_overlays));
        Assert.DoesNotContain("scorch", Overlays(r, target));

        // the clown target honks at every shot
        var clown = Place<obj_item_target_clown>(r, 8, 7);
        Shoot<obj_projectile_beam_practice>(r, 5, 7, clown);
        Shoot<obj_projectile_beam_practice>(r, 5, 7, clown);
        Assert.True(Heard(r, "items/bikehorn"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheBannersInspireTheirOwnOnceAMinuteAndTheGodstaffRecoloursWhatItWavesAt()
    {
        var (r, feed, bob) = Room(8615);
        var hurt = r.Dummy(5, 5);
        var fine = r.Dummy(6, 5);
        r.In(() =>
        {
            Waiting.NoSleep(hurt.adjust_brute_loss(120)); // past the soft crit threshold: stat SOFT_CRIT
            Waiting.NoSleep(hurt.adjust_tox_loss(20));
            Waiting.NoSleep(fine.adjust_brute_loss(30));
        });
        r.Step(2);
        r.In(() => Assert.True(Dm.truthy(hurt.stat), "the hurt man is conscious")); // stat != CONSCIOUS
        float hurtBrute = r.In(() => Dm.num(hurt.get_brute_loss()));
        float fineBrute = r.In(() => Dm.num(fine.get_brute_loss()));

        // Meditopia's flourish: the warcry, the rustle, the healing chime; the man down within four tiles healed and dosed, the man
        // standing (no mind, no department) passed over
        var banner = Hold<obj_item_banner_medical>(r, bob);
        UseInHand(r, bob);
        r.Step(Second);
        Assert.True(feed.Any("skywards, inspiring your allies!"));
        Assert.True(feed.Any("No wounds cannot be healed!"));
        Assert.True(Heard(r, "staff_healing"));
        r.In(() =>
        {
            Assert.Equal(hurtBrute - 15, Dm.num(hurt.get_brute_loss()), 1);
            Assert.True(Dm.num(hurt.get_tox_loss()) <= 5.01f, "toxin " + hurt.get_tox_loss());
            Assert.True(Dm.truthy(((datum_reagents)hurt.reagents).has_reagent(typeof(datum_reagent_medicine_inaprovaline))));
            Assert.Equal(fineBrute, Dm.num(fine.get_brute_loss()), 1);
            Assert.True(banner.morale_time > world.time);
        });

        // not again within the minute
        feed.Chat.Clear();
        UseInHand(r, bob);
        Assert.True(feed.Any("You aren't feeling inspired enough"));
        Assert.False(feed.Any("skywards"));

        // a mundane banner only shows its colours
        Drop(r, bob);
        Hold<obj_item_banner_medical_mundane>(r, bob);
        feed.Chat.Clear();
        UseInHand(r, bob);
        Assert.False(feed.Any("skywards"));
        Drop(r, bob);

        // the godstaff chants for two seconds and turns what it waves at its colour
        var wrench = Place<obj_item_wrench>(r, 5, 4);
        Hold<obj_item_godstaff_red>(r, bob);
        Use(r, bob, wrench);
        Assert.True(feed.Any("chants deeply"));
        r.Step(2 * Second + 5);
        r.In(() => Assert.Equal("#ff0000", Dm.str(wrench.color)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheMeleeOddsWhipStingRoastAndWieldAndSkubSitsInATankHolder()
    {
        var (r, feed, bob) = Room(8616);

        // the curator's whip disarms
        var victim = r.Dummy(5, 4);
        var wrench = r.In(() => Dm.@new<obj_item_wrench>(victim.loc));
        r.In(() => Waiting.NoSleep(victim.put_in_hands(wrench)));
        Hold<obj_item_melee_curator_whip>(r, bob);
        r.In(() => bob.set_combat_mode(TRUE));
        Use(r, bob, victim);
        Assert.True(feed.Any("disarms"));
        r.In(() => Assert.Empty(Dm.each<obj_item>(victim.held_items)));
        Drop(r, bob);

        // the bee sword stings with toxin
        Hold<obj_item_melee_beesword>(r, bob);
        Use(r, bob, victim);
        r.In(() => Assert.True(Dm.num(((datum_reagents)victim.reagents).get_reagent_amount(typeof(datum_reagent_toxin))) > 0, "no toxin"));
        r.In(() => bob.set_combat_mode(FALSE));
        Drop(r, bob);

        // the roasting stick: extended, a sausage on it, roasted at the supermatter for ten seconds (tg's crystal lets the stick
        // through; the bonfire is still inert)
        var stick = Hold<obj_item_melee_roastingstick>(r, bob);
        UseInHand(r, bob);
        Assert.True(feed.Any("balloon: extended"));
        Assert.True(Heard(r, "batonextend"));
        r.In(() => Assert.True(HAS_TRAIT(stick, TRAIT_TRANSFORM_ACTIVE)));
        r.In(() => bob.swap_hand());
        var sausage = Hold<obj_item_food_sausage>(r, bob);
        Use(r, bob, stick);
        r.In(() => Assert.Same(sausage, stick.held_sausage));
        Assert.Contains("roastingstick_sausage", Overlays(r, stick));
        r.In(() => bob.swap_hand());
        UseInHand(r, bob);
        Assert.True(feed.Any("You can't retract"));
        r.In(() => Assert.True(HAS_TRAIT(stick, TRAIT_TRANSFORM_ACTIVE)));
        var crystal = Place<obj_machinery_power_supermatter_crystal>(r, 5, 5);
        Use(r, bob, crystal);
        Assert.True(feed.Any("You extend"));
        r.Step(10 * Second + 5);
        Assert.True(feed.Any("You finish roasting"));
        r.In(() => Assert.Contains("-roasted sausage", sausage.name, StringComparison.Ordinal));
        r.In(() => Assert.False(QDELETED(bob)));
        Drop(r, bob);

        // the bamboo staff's punny name; wielded, its state
        var staff = Hold<obj_item_bambostaff>(r, bob);
        r.In(() => Assert.Contains("b", staff.name.ToLowerInvariant(), StringComparison.Ordinal));
        r.In(() => Assert.Equal("bambostaff0", staff.icon_state));
        r.In(() => bob.swap_hand());
        Drop(r, bob);
        r.In(() => bob.swap_hand());
        UseInHand(r, bob);
        r.In(() =>
        {
            Assert.True(HAS_TRAIT(staff, TRAIT_WIELDED));
            Assert.Equal("bambostaff1", staff.icon_state);
        });
        r.In(() => qdel(staff));

        // the weak claymore stabs less deeply
        var claymore = r.In(() => Dm.@new<obj_item_claymore_weak>(bob.loc));
        r.In(() => Assert.NotNull(claymore.GetComponent(typeof(datum_component_alternative_sharpness))));

        // skub in an empty tank holder
        var holder = Place<obj_structure_tank_holder>(r, 6, 4);
        r.In(() => Waiting.NoSleep(bob.forceMove(locate(5, 3, 1))));
        var skub = Hold<obj_item_skub>(r, bob);
        Use(r, bob, holder);
        r.In(() =>
        {
            Assert.Same(skub, holder.tank);
            Assert.Equal("holder_skub", holder.icon_state);
        });
        r.AssertNoRuntimes();
    }
}
