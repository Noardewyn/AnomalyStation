using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tests.Tg.M2Locomotion.P4;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tests.Gaps.GapsHarness;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package P6 (D298): the cybernetic implants of augments_arms.dm, augments_chest.dm and augments_internal.dm, put in by the
/// augmentation (organ manipulation on an opened arm) or by an autosurgeon, each doing what tg's does: a surgical toolset extends a tool
/// into the hand from its button's radial and the drop key puts it back; the nutriment pump feeds its starving host; the reviver patches
/// a downed host and brings a dead one back; the thrusters push their host through space on the internals the breathing tube connects; the
/// anti-drop implant keeps the held item; the CNS rebooter clears a stun; the Strong-Arm pair gives the door prying bonus and its punch
/// throws; an EMP does tg's harm to each; and every new live type is made and destroyed with no runtimes.
/// </summary>
public class CyberImplantsTests
{
    private static readonly Type[] AllTypes =
    {
        typeof(obj_item_organ_cyberimp_arm_toolkit_toolset), typeof(obj_item_organ_cyberimp_arm_toolkit_surgery), typeof(obj_item_organ_cyberimp_arm_toolkit_surgery_emagged),
        typeof(obj_item_organ_cyberimp_arm_toolkit_surgery_cruel), typeof(obj_item_organ_cyberimp_arm_toolkit_paperwork), typeof(obj_item_organ_cyberimp_arm_toolkit_gun_laser),
        typeof(obj_item_organ_cyberimp_arm_toolkit_gun_taser), typeof(obj_item_organ_cyberimp_arm_toolkit_esword), typeof(obj_item_organ_cyberimp_arm_toolkit_medibeam),
        typeof(obj_item_organ_cyberimp_arm_toolkit_flash), typeof(obj_item_organ_cyberimp_arm_toolkit_baton), typeof(obj_item_organ_cyberimp_arm_toolkit_combat),
        typeof(obj_item_organ_cyberimp_arm_strongarm), typeof(obj_item_organ_cyberimp_chest_nutriment), typeof(obj_item_organ_cyberimp_chest_nutriment_plus),
        typeof(obj_item_organ_cyberimp_chest_nutriment_black_market), typeof(obj_item_organ_cyberimp_chest_reviver), typeof(obj_item_organ_cyberimp_chest_thrusters),
        typeof(obj_item_organ_cyberimp_chest_spine), typeof(obj_item_organ_cyberimp_chest_spine_atlas), typeof(obj_item_organ_cyberimp_brain_anti_drop),
        typeof(obj_item_organ_cyberimp_brain_anti_stun), typeof(obj_item_organ_cyberimp_brain_connector), typeof(obj_item_organ_cyberimp_mouth_breathing_tube),
        typeof(obj_item_autosurgeon_syndicate_laser_arm), typeof(obj_item_autosurgeon_syndicate_anti_stun_single_use), typeof(obj_item_autosurgeon_syndicate_reviver),
        typeof(obj_item_autosurgeon_syndicate_commsagent), typeof(obj_item_autosurgeon_syndicate_emaggedsurgerytoolset), typeof(obj_item_autosurgeon_syndicate_contraband_sechud),
        typeof(obj_item_autosurgeon_syndicate_xray_eyes), typeof(obj_item_storage_box_cyber_implants), typeof(obj_item_assembly_flash_armimplant), typeof(obj_item_borg_stun),
        typeof(obj_item_gun_energy_laser_mounted_augment), typeof(obj_item_gun_energy_e_gun_advtaser_mounted), typeof(obj_item_stamp_mod_toolkit),
        typeof(obj_item_melee_energy_blade_hardlight), typeof(obj_item_gun_medbeam), typeof(obj_item_gun_medbeam_mech),
    };

    private const string MechMove = "sound/vehicles/mecha/mechmove03.ogg";

    private static string Diag(AudioRoom r, Feed feed) => "chat: " + string.Join(" | ", feed.Chat.TakeLast(8).Select(c => c.Html)) + " runtimes: " + string.Join("\n", r.Room.RuntimeText);

    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(11, 9, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.Step(Second);
        return (r, feed, bob);
    }

    /// <summary>An autosurgeon loaded with <paramref name="implant"/> in the host's hand, used on himself with the use key (Z).</summary>
    private static T ByAutosurgeon<T>(AudioRoom r, mob_living_carbon_human who) where T : obj_item_organ
    {
        T implant = r.In(() => Dm.@new<T>(who.loc));
        obj_item_autosurgeon surgeon = r.In(() => Dm.@new<obj_item_autosurgeon>(who.loc));
        r.In(() =>
        {
            Waiting.NoSleep(surgeon.load_organ(implant));
            Assert.True(Dm.truthy(Waiting.NoSleep(who.put_in_active_hand(surgeon))));
        });
        r.Command(() => ClientSeam.Keybinding(who.client, 4, true)); // activate_inhand: attack_self
        r.Step(4);
        Assert.Same(who, r.In(() => implant.owner));
        r.In(() =>
        {
            if (Dm.@in(surgeon, who.held_items))
                Waiting.NoSleep(who.dropItemToGround(surgeon));
        });
        return implant;
    }

    private static void Button(AudioRoom r, mob who, obj_item_organ organ)
    {
        atom button = r.In(() => Assert.Single(Dm.each_cast<datum_action>(organ.actions)).viewers[who.hud_used] as atom);
        Assert.NotNull(button);
        r.Command(() => ClientSeam.Click(who.client, button, "icon-x=16;icon-y=16;left=1;button=left"));
        r.Step(4);
    }

    private static void PickRadial(AudioRoom r, mob who, string name)
    {
        (long id, int index) = r.In(() =>
        {
            datum_radial_menu menu = Dm.each_cast<datum_radial_menu>(who.client.dm_radial_menus).Single();
            int i = menu.dm_elements.FindIndex(e => e.Name != null && e.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
            Assert.True(i >= 0, "no slice " + name + " in " + string.Join(", ", menu.dm_elements.Select(e => e.Name)));
            return (menu.dm_id, i + 1);
        });
        r.Command(() => ClientSeam.RadialPick(who.client, id, index));
        r.Step(4);
    }

    /// <summary>The surgeon uses what he holds on the patient's zone (picking <paramref name="pick"/> from tg's radial of operations
    /// when the tool offers more than one); the operation's do-after runs its seconds.</summary>
    private static void Operate(AudioRoom r, mob_living_carbon_human surgeon, mob target, string zone, float seconds, string pick = null)
    {
        r.In(() => surgeon.zone_selected = zone);
        r.Command(() => ClientSeam.Click(surgeon.client, target, "icon-x=16;icon-y=16;left=1;button=left"));
        r.Step(4);
        if (pick != null && r.In(() => Dm.each_cast<datum_radial_menu>(surgeon.client.dm_radial_menus).Any()))
            PickRadial(r, surgeon, pick);
        r.Step((int)(seconds * Second) + Second);
    }

    private static T Hold<T>(AudioRoom r, mob_living_carbon_human who, T thing = null) where T : obj_item
    {
        r.In(() =>
        {
            if (who.get_active_held_item() is obj_item old)
                Waiting.NoSleep(who.dropItemToGround(old));
        });
        thing ??= r.In(() => Dm.@new<T>(who.loc));
        r.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(who.put_in_hands(thing)))));
        return thing;
    }

    [Fact]
    public void A_surgical_toolset_augmented_into_the_arm_extends_a_tool_into_the_hand_and_the_drop_key_retracts_it()
    {
        var (r, feed, bob) = Room(9601);
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        obj_structure_table_optable table = r.In(() => Dm.@new<obj_structure_table_optable>(locate(5, 4, 1)));
        mob_living_carbon_human pat = r.Dummy(5, 4);
        r.Step(Second);
        r.In(() => Waiting.NoSleep(table.buckle_mob(pat, force: TRUE)));
        r.Step(2);

        // the augmentation: the arm cut, opened and its organs cut, then the implant put in by hand (organ manipulation)
        Hold<obj_item_scalpel>(r, bob);
        Operate(r, bob, pat, BODY_ZONE_R_ARM, 1.6f);
        Hold<obj_item_retractor>(r, bob);
        Operate(r, bob, pat, BODY_ZONE_R_ARM, 2.4f);
        Hold<obj_item_scalpel>(r, bob);
        Operate(r, bob, pat, BODY_ZONE_R_ARM, 2.4f, pick: "incise organs");
        var kit = Hold(r, bob, r.In(() => Dm.@new<obj_item_organ_cyberimp_arm_toolkit_surgery>(bob.loc)));
        Operate(r, bob, pat, BODY_ZONE_R_ARM, 3.2f, pick: "surgical toolset");
        Assert.True(r.In(() => kit.owner == pat), "the toolset is in the patient's arm; " + Diag(r, feed));
        Assert.Equal(BODY_ZONE_R_ARM, r.In(() => kit.zone));
        Assert.Equal(7, r.In(() => kit.items_list.len)); // tg's seven surgical tools, made inside the implant
        obj_item_bodypart arm = r.In(() => pat.get_bodypart(BODY_ZONE_R_ARM) as obj_item_bodypart);
        Assert.Same(arm, r.In(() => kit.hand));
        // the body overlay of the augment on the right arm (get_overlay_state), drawn on two layers (the "_hand" one)
        Assert.Equal("toolkit_med_right", r.In(() => kit.bodypart_aug.icon_state as string));
        Assert.Contains(r.In(() => kit.bodypart_aug), r.In(() => Dm.each(arm.bodypart_overlays).ToList()));

        // the surgeon's own, put in by an autosurgeon: his button "Toggle surgical toolset implant", the radial of the tools, the scalpel
        // into the hand of the implant's side
        mob_living_carbon_human host = bob;
        var own = ByAutosurgeon<obj_item_organ_cyberimp_arm_toolkit_surgery>(r, host);
        // the autosurgeon's hand chose the arm (use_autosurgeon's checked_zones): the tools come out of that side
        string zone = r.In(() => own.zone as string);
        int side = zone == BODY_ZONE_R_ARM ? RIGHT_HANDS : LEFT_HANDS;
        Assert.Contains(zone, new[] { BODY_ZONE_R_ARM, BODY_ZONE_L_ARM });
        Assert.Equal("Toggle surgical toolset implant", r.In(() => Assert.Single(Dm.each_cast<datum_action>(own.actions)).name));
        r.Sounds.Heard.Clear();
        Button(r, host, own);
        PickRadial(r, host, "scalpel");
        obj_item scalpel = r.In(() => host.held_items[side] as obj_item);
        Assert.IsType<obj_item_scalpel_augment>(scalpel);
        Assert.Same(scalpel, r.In(() => own.active_item));
        Assert.True(r.In(() => HAS_TRAIT_FROM(scalpel, TRAIT_NODROP, HAND_REPLACEMENT_TRAIT)), "the extended tool cannot be dropped");
        Assert.True(feed.Any("You extend"), Diag(r, feed));
        Assert.Contains(r.Sounds.Heard, h => h.File == MechMove);
        // it cannot be dropped as an item; the drop key (Q) with that hand active stows it (dropkey)
        r.In(() => Waiting.NoSleep(host.dropItemToGround(scalpel)));
        Assert.Same(host, r.In(() => scalpel.loc));
        r.In(() => host.active_hand_index = side);
        r.Command(() => ClientSeam.Keybinding(host.client, 5, true));
        r.Step(4);
        Assert.Same(own, r.In(() => scalpel.loc));
        Assert.Null(r.In(() => own.active_item));
        Assert.True(feed.Any("snaps back into your"), Diag(r, feed));

        // the empty hand's use (Z) opens the radial too (the arm's attack_self); the saw comes out, and its own use swaps the tools
        r.Command(() => ClientSeam.Keybinding(host.client, 4, true));
        r.Step(4);
        PickRadial(r, host, "saw");
        Assert.IsType<obj_item_circular_saw_augment>(r.In(() => own.active_item));
        r.Command(() => ClientSeam.Keybinding(host.client, 4, true));
        r.Step(4);
        Assert.Null(r.In(() => own.active_item)); // swap_tools: retracted, then the radial again
        PickRadial(r, host, "hemostat");
        Assert.IsType<obj_item_hemostat_augment>(r.In(() => own.active_item));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_nutriment_pump_feeds_its_starving_host_and_an_emp_poisons_him()
    {
        var (r, feed, bob) = Room(9602);
        var pump = ByAutosurgeon<obj_item_organ_cyberimp_chest_nutriment>(r, bob);
        r.In(() => Waiting.NoSleep(bob.set_nutrition(120))); // below tg's NUTRITION_LEVEL_STARVING, 150 (one feed lifts it above)
        float before = r.In(() => bob.nutrition);
        r.Step(3 * Second);
        Assert.True(feed.Any("You feel less hungry..."), Diag(r, feed));
        Assert.True(r.In(() => bob.nutrition) > before + 20, "fed " + before + " -> " + r.In(() => bob.nutrition));
        Assert.Equal(TRUE, r.In(() => pump.synthesizing));
        r.Step(6 * Second);
        Assert.Equal(FALSE, r.In(() => pump.synthesizing)); // synth_cool after five seconds

        r.In(() => Waiting.NoSleep(bob.emp_act(EMP_HEAVY)));
        Assert.True(r.In(() => Dm.num(((datum_reagents)bob.reagents).get_reagent_amount(typeof(datum_reagent_toxin_bad_food)))) > 0, "the EMP's bad food");
        Assert.True(feed.Any("You feel like your insides are burning."), Diag(r, feed));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_reviver_patches_a_downed_host_and_brings_a_dead_one_back()
    {
        var (r, feed, bob) = Room(9603);
        var reviver = ByAutosurgeon<obj_item_organ_cyberimp_chest_reviver>(r, bob);
        r.In(() => Waiting.NoSleep(bob.adjust_brute_loss(30)));
        r.In(() => Waiting.NoSleep(bob.death()));
        r.Step(2);
        Assert.Equal(DEAD, r.In(() => bob.stat));
        r.Sounds.Heard.Clear();
        r.Step(20 * Second);
        Assert.True(feed.Any("You feel a faint buzzing as your reviver implant starts patching your wounds..."), Diag(r, feed));
        Assert.True(r.In(() => bob.stat) != DEAD, "revived; " + Diag(r, feed));
        Assert.True(feed.Any("gasps!"), Diag(r, feed)); // revive_dead's emote (the convulsing is shown to the onlookers, the dead see nothing)
        Assert.Contains(r.Sounds.Heard, h => h.File != null && h.File.StartsWith("sound/mobs/humanoids/human/gasp/", StringComparison.Ordinal)); // the zap played while he was dead
        Assert.True(r.In(() => Dm.num(bob.get_brute_loss())) < 30, "patched brute " + r.In(() => bob.get_brute_loss()));
        Assert.True(r.In(() => reviver.revive_cost) >= 10 * MINUTES, "the revival's ten minutes' cost");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_anti_drop_implant_keeps_the_held_item_and_the_rebooter_clears_a_stun()
    {
        var (r, feed, bob) = Room(9604);
        var antidrop = ByAutosurgeon<obj_item_organ_cyberimp_brain_anti_drop>(r, bob);
        var rebooter = ByAutosurgeon<obj_item_organ_cyberimp_brain_anti_stun>(r, bob);
        var wrench = Hold<obj_item_wrench>(r, bob);
        Button(r, bob, antidrop);
        Assert.True(feed.Any("grip tightens."), Diag(r, feed));
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(wrench)));
        Assert.Same(bob, r.In(() => wrench.loc));
        Button(r, bob, antidrop);
        Assert.True(feed.Any("Your hands relax..."), Diag(r, feed));
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(wrench)));
        Assert.NotSame(bob, r.In(() => wrench.loc));

        // a ten-second stun: the rebooter clears it after tg's second, with sparks and six seconds of stun immunity
        r.In(() => Waiting.NoSleep(bob.Stun(10 * SECONDS)));
        Assert.NotNull(r.In(() => bob.IsStun()));
        r.Step(Second + Second / 2);
        Assert.Null(r.In(() => bob.IsStun()));
        Assert.True(r.In(() => HAS_TRAIT(bob, TRAIT_STUNIMMUNE)), "stun immune for a while");
        Assert.True(r.In(() => rebooter.implant_cooldown) > r.In(() => world.time), "the minute's cooldown");
        r.Step(7 * Second);
        Assert.False(r.In(() => HAS_TRAIT(bob, TRAIT_STUNIMMUNE)), "the buffs go after six seconds");

        // an EMP: the rebooter fails for nine seconds (its brain implant's seizure stuns), the anti-drop releases
        r.In(() => Waiting.NoSleep(bob.emp_act(EMP_HEAVY)));
        Assert.True(r.In(() => Dm.truthy(Dm.bitand(rebooter.organ_flags, ORGAN_FAILING))), "the rebooter's EMP failure");
        Assert.True(feed.Any("Your body seizes up!"), Diag(r, feed));
        r.Step(10 * Second);
        Assert.False(r.In(() => Dm.truthy(Dm.bitand(rebooter.organ_flags, ORGAN_FAILING))), "rebooted");
        Assert.True(feed.Any("Your rebooter implant is ready."), Diag(r, feed));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_breathing_tube_connects_internals_without_a_mask_and_the_strong_arms_pry_and_punch()
    {
        var (r, feed, bob) = Room(9605);
        ByAutosurgeon<obj_item_organ_cyberimp_mouth_breathing_tube>(r, bob);
        var tank = Hold<obj_item_tank_internals_oxygen>(r, bob);
        r.In(() => Assert.NotNull(bob.can_breathe_internals()));
        r.In(() => Waiting.NoSleep(bob.toggle_internals(tank)));
        Assert.Same(tank, r.In(() => bob.@internal));
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(tank)));

        // the Strong-Arm pair: the set bonus's door prying, the punch's extra damage and throw (in combat mode)
        var right = r.In(() => Dm.@new<obj_item_organ_cyberimp_arm_strongarm>(bob.loc));
        var left = r.In(() => Dm.@new<obj_item_organ_cyberimp_arm_strongarm>(bob.loc));
        r.In(() =>
        {
            Waiting.NoSleep(left.swap_zone(BODY_ZONE_L_ARM));
            Assert.True(Dm.truthy(Waiting.NoSleep(right.Insert(bob))));
            Assert.True(Dm.truthy(Waiting.NoSleep(left.Insert(bob))));
        });
        r.Step(4);
        Assert.True(feed.Any("Your improved arms allow you to open airlocks by force with your bare hands!"), Diag(r, feed));
        Assert.NotNull(r.In(() => bob.has_status_effect(typeof(datum_status_effect_organ_set_bonus_strongarm))));
        Assert.Equal(2 + 10, r.In(() => ((obj_item_bodypart)bob.get_bodypart(BODY_ZONE_R_ARM)).unarmed_damage_high), 3); // tg's +2 on the human arm's 10
        mob_living_carbon_human dummy = r.Dummy(5, 4);
        r.Step(Second);
        r.In(() => { bob.combat_mode = TRUE; bob.active_hand_index = 2; bob.zone_selected = BODY_ZONE_CHEST; });
        float health = r.In(() => dummy.health);
        r.Command(() => ClientSeam.Click(bob.client, dummy, "icon-x=16;icon-y=16;left=1;button=left"));
        r.Step(Second);
        Assert.True(r.In(() => dummy.health) < health, "the punch hurts; " + Diag(r, feed));
        Assert.True(feed.Any("ed Dummy") || feed.Any("You punch") || feed.Any("You smash") || feed.Any("You pummel") || feed.Any("You bash") || feed.Any("You slam"), Diag(r, feed));
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/items/weapons/punch1.ogg");
        r.In(() => Waiting.NoSleep(left.Remove(bob)));
        r.Step(4);
        Assert.True(feed.Any("You can no longer force open airlocks with your bare hands."), Diag(r, feed));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_skillchip_connector_takes_a_held_chip_in_and_gives_it_back_from_its_radial()
    {
        var (r, feed, bob) = Room(9608);
        var connector = ByAutosurgeon<obj_item_organ_cyberimp_brain_connector>(r, bob);
        Assert.Equal("Use CNS skillchip connector implant", r.In(() => Assert.Single(Dm.each_cast<datum_action>(connector.actions)).name));
        var chip = Hold<obj_item_skillchip_wine_taster>(r, bob);
        Button(r, bob, connector);
        Assert.True(feed.Any("You start fiddling around with"), Diag(r, feed));
        r.Step(2 * Second); // tg's do-after of a second and a half
        obj_item_organ_brain brain = r.In(() => bob.get_organ_by_type(typeof(obj_item_organ_brain)) as obj_item_organ_brain);
        Assert.Contains(chip, r.In(() => Dm.each(brain.skillchips).ToList()));
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/machines/chime.ogg");

        // the empty hand: the radial of the brain's chips, the chip back into the hand
        Button(r, bob, connector);
        r.Step(2 * Second);
        if (r.In(() => Dm.each_cast<datum_radial_menu>(bob.client.dm_radial_menus).Any()))
            PickRadial(r, bob, "skillchip"); // (the radial of a single chip answers at once)
        Assert.DoesNotContain(chip, r.In(() => Dm.each(brain.skillchips).ToList()));
        Assert.Same(bob, r.In(() => chip.loc));
        Assert.True(feed.Any("You take"), Diag(r, feed));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_combat_implant_cuts_with_its_blade_heals_with_its_beam_and_shocks_with_its_arm()
    {
        var (r, feed, bob) = Room(9609);
        var kit = ByAutosurgeon<obj_item_organ_cyberimp_arm_toolkit_combat>(r, bob);
        int side = r.In(() => Dm.eq(kit.zone, BODY_ZONE_R_ARM) ? RIGHT_HANDS : LEFT_HANDS);
        r.In(() => { bob.active_hand_index = side; bob.combat_mode = TRUE; bob.zone_selected = BODY_ZONE_CHEST; });
        Assert.Equal(4, r.In(() => kit.items_list.len));
        Assert.Same(kit, r.In(() => Dm.each_cast<object>(kit.contents).OfType<obj_item_assembly_flash_armimplant>().Single().arm.resolve()));
        mob_living_carbon_human dummy = r.Dummy(5, 4);
        r.Step(Second);

        // the hardlight blade: an extended energy blade (TRAIT_TRANSFORM_ACTIVE from its Initialize), tg's 30 force
        Button(r, bob, kit);
        PickRadial(r, bob, "hardlight blade");
        var blade = r.In(() => kit.active_item as obj_item_melee_energy_blade_hardlight);
        Assert.NotNull(blade);
        Assert.True(r.In(() => HAS_TRAIT(blade, TRAIT_TRANSFORM_ACTIVE)), "the blade is lit");
        float brute = r.In(() => Dm.num(dummy.get_brute_loss()));
        r.Command(() => ClientSeam.Click(bob.client, dummy, "icon-x=16;icon-y=16;left=1;button=left"));
        r.Step(Second);
        Assert.True(r.In(() => Dm.num(dummy.get_brute_loss())) >= brute + 20, "the blade cuts; " + Diag(r, feed));
        r.Command(() => ClientSeam.Keybinding(bob.client, 5, true)); // stowed with Q
        r.Step(4);

        // the medical beamgun: its beam on the wounded dummy two tiles away heals him each second
        r.In(() => Waiting.NoSleep(dummy.forceMove(locate(6, 4, 1))));
        r.In(() => bob.combat_mode = FALSE);
        Button(r, bob, kit);
        PickRadial(r, bob, "beamgun");
        var beamgun = r.In(() => kit.active_item as obj_item_gun_medbeam);
        Assert.NotNull(beamgun);
        float wounded = r.In(() => Dm.num(dummy.get_brute_loss()));
        r.Command(() => ClientSeam.Click(bob.client, dummy, "icon-x=16;icon-y=16;left=1;button=left"));
        r.Step(4 * Second);
        Assert.Same(dummy, r.In(() => beamgun.current_target));
        Assert.NotNull(r.In(() => beamgun.current_beam));
        Assert.True(r.In(() => Dm.num(dummy.get_brute_loss())) < wounded, "the beam heals; " + wounded + " -> " + r.In(() => dummy.get_brute_loss()));
        r.Command(() => ClientSeam.Keybinding(bob.client, 5, true));
        r.Step(4);
        Assert.Null(r.In(() => beamgun.current_beam)); // stowing it drops the beam (dropped, LoseTarget)

        // the electrically-charged arm: tg's stamina damage, confusion and stutter on the dummy beside him
        r.In(() => Waiting.NoSleep(dummy.forceMove(locate(5, 4, 1))));
        Button(r, bob, kit);
        PickRadial(r, bob, "electrically-charged arm");
        Assert.IsType<obj_item_borg_stun>(r.In(() => kit.active_item));
        r.Command(() => ClientSeam.Click(bob.client, dummy, "icon-x=16;icon-y=16;left=1;button=left"));
        r.Step(Second);
        Assert.True(r.In(() => Dm.num(dummy.get_stamina_loss())) >= 50, "the shock's stamina damage; " + Diag(r, feed));
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/items/weapons/egloves.ogg");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void An_emp_does_tgs_harm_to_the_arm_chest_and_brain_implants()
    {
        var (r, feed, bob) = Room(9606);
        var strong = ByAutosurgeon<obj_item_organ_cyberimp_arm_strongarm>(r, bob);
        var spine = ByAutosurgeon<obj_item_organ_cyberimp_chest_spine>(r, bob);
        var connector = ByAutosurgeon<obj_item_organ_cyberimp_brain_connector>(r, bob);
        Assert.True(r.In(() => HAS_TRAIT(bob, TRAIT_BOULDER_BREAKER)), "the spine's boulder breaking");
        Assert.NotNull(r.In(() => spine.stone_overlay));
        float brute = r.In(() => Dm.num(bob.get_brute_loss()));
        r.In(() => Waiting.NoSleep(bob.emp_act(EMP_HEAVY)));
        r.Step(2);
        Assert.True(r.In(() => Dm.truthy(Dm.bitand(strong.organ_flags, ORGAN_FAILING))), "the arm spasms");
        Assert.True(feed.Any("balloon: your arm spasms wildly!"), Diag(r, feed));
        Assert.True(r.In(() => Dm.num(bob.get_brute_loss())) >= brute + 20, "the spine crushes for 20 brute");
        Assert.True(feed.Any("You feel shearing pain as your body is crushed like a soda can!"), Diag(r, feed));
        Assert.True(r.In(() => Dm.truthy(Dm.bitand(connector.organ_flags, ORGAN_FAILING))), "the connector fails");
        Assert.True(feed.Any("You feel something fall off from behind your head."), Diag(r, feed));
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/effects/meatslap.ogg");
        r.Step(10 * Second);
        Assert.False(r.In(() => Dm.truthy(Dm.bitand(strong.organ_flags, ORGAN_FAILING))), "the arm reboots after nine seconds");
        Assert.True(feed.Any("balloon: your arm stops spasming!"), Diag(r, feed));
        r.In(() => Waiting.NoSleep(spine.Remove(bob)));
        Assert.False(r.In(() => HAS_TRAIT(bob, TRAIT_BOULDER_BREAKER)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void Every_type_is_made_and_destroyed_with_no_runtimes()
    {
        var (r, _, _) = Room(9607);
        foreach (Type type in AllTypes)
        {
            atom made = r.In(() => (atom)Dm.@new(type, locate(3, 3, 1)));
            r.Step(2);
            r.In(() => qdel(made));
            r.Step(2);
        }
        r.Step(Second);
        r.AssertNoRuntimes();
    }
}

/// <summary>
/// The implantable thrusters in the first-person locomotion's zero gravity (as M4c's JetpackTests): a host with the thrusters and a
/// breathing tube, an oxygen tank in hand on internals, turns the thrusters on with their button and the held key moves him through space
/// on the tank's gas (tg's priority 3), with the ion trail behind him.
/// </summary>
public class CyberThrustersTests
{
    private const int Second = 20;

    [Fact]
    public void The_thrusters_push_their_host_through_space_on_his_internals()
    {
        var r = new CapsuleRoom(9611);
        r.In(() =>
        {
            for (int tx = 3; tx <= 14; tx++)
                for (int ty = 3; ty <= 13; ty++)
                    Waiting.NoSleep(locate(tx, ty, 1).ChangeTurf(typeof(turf_open_space)));
        });
        mob_living_carbon_human bob = r.Join(4, 8, "Bob");
        var (thrusters, tank) = r.In(() =>
        {
            ADD_TRAIT(bob, TRAIT_GODMODE, "test");
            var t = Dm.@new<obj_item_organ_cyberimp_chest_thrusters>(bob.loc);
            var tube = Dm.@new<obj_item_organ_cyberimp_mouth_breathing_tube>(bob.loc);
            Assert.True(Dm.truthy(Waiting.NoSleep(t.Insert(bob))));
            Assert.True(Dm.truthy(Waiting.NoSleep(tube.Insert(bob))));
            var o2 = Dm.@new<obj_item_tank_internals_oxygen>(bob.loc);
            Assert.True(Dm.truthy(Waiting.NoSleep(bob.put_in_hands(o2))));
            Waiting.NoSleep(bob.toggle_internals(o2));
            return (t, o2);
        });
        r.Step(Second);
        Assert.Same(tank, r.In(() => bob.@internal));
        Assert.False(r.Params(bob).CanMove); // nothing to push off from, the thrusters off

        atom button = r.In(() => Assert.Single(Dm.each_cast<datum_action>(thrusters.actions)).viewers[bob.hud_used] as atom);
        r.Command(() => ClientSeam.Click(bob.client, button, "icon-x=16;icon-y=16;left=1;button=left"));
        r.Step(Second / 2);
        r.In(() =>
        {
            Assert.Equal(TRUE, thrusters.on);
            Assert.Equal("imp_jetpack-on", thrusters.icon_state);
            Assert.Equal("imp_jetpack_on", thrusters.bodypart_aug.icon_state as string); // the body overlay's lit ports
            Assert.True(Dm.truthy(bob.has_movespeed_modifier(typeof(datum_movespeed_modifier_jetpack_cybernetic))));
            var component = thrusters.GetComponent(typeof(datum_component_jetpack)) as datum_component_jetpack;
            Assert.Equal(TRUE, component.active);
            Assert.NotNull(component.trail);
        });
        Assert.True(r.Params(bob).CanMove); // the locomotion's preview asks the thrusters (preview_thrust)

        float moles = r.In(() => Dm.num(tank.air_contents.total_moles()));
        (int X, int Y) start = r.Tile(bob);
        bool trail = false;
        for (int i = 0; i < 2 * Second; i++)
        {
            r.Walk(bob, 127, 0, 1, yaw: 16384);
            trail |= r.In(() => Dm.each_cast<object>(world.contents).Any(o => o is obj_effect_particle_effect_ion_trails));
        }
        Assert.True(r.Tile(bob).X > start.X + 2, "moved from " + start + " to " + r.Tile(bob));
        Assert.True(r.In(() => Dm.num(tank.air_contents.total_moles())) < moles, "the thrust used the tank's gas");
        Assert.True(trail, "the ion trail");

        r.Command(() => ClientSeam.Click(bob.client, button, "icon-x=16;icon-y=16;left=1;button=left"));
        r.Step(Second / 2);
        r.In(() =>
        {
            Assert.Equal(FALSE, thrusters.on);
            Assert.Equal("imp_jetpack", thrusters.icon_state);
            Assert.False(Dm.truthy(bob.has_movespeed_modifier(typeof(datum_movespeed_modifier_jetpack_cybernetic))));
        });
        r.AssertNoRuntimes();
    }
}
