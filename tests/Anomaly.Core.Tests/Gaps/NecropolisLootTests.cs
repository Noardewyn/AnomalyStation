using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Core.Tests.M4bAudio;
using Xunit;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// The necropolis chests' loot, each doing what tg's does: the berserker armour's rage, the memento mori, the concussive gauntlets,
/// the godslayer armour, the spooky lantern's wisp, Jacob's ladder, the book of Babel, the potion of flight, the rod of Asclepius
/// with its Hippocratic oath, the warp cubes, the immortality talisman, the paradox bag, the eye of god, the cursed katana, Cain and
/// Abel, the lava boat and the ship in a bottle, the alien welder, the instant summons spellbook, and the elites' crusher trophies.
/// </summary>
public class NecropolisLootTests
{
    private static void RunFor(AudioRoom r, float deciseconds)
    {
        float end = r.In(() => world.time) + deciseconds;
        while (r.In(() => world.time) < end)
            r.Step();
    }

    private static bool RunUntil(AudioRoom r, float deciseconds, Func<bool> done)
    {
        float end = r.In(() => world.time) + deciseconds;
        while (r.In(() => world.time) < end)
        {
            if (r.In(done))
                return true;
            r.Step();
        }
        return r.In(done);
    }

    private static T Hold<T>(AudioRoom r, mob_living_carbon_human who) where T : obj_item => r.In(() =>
    {
        var item = Dm.@new<T>(who.loc);
        Waiting.NoSleep(who.put_in_active_hand(item));
        return item;
    });

    private static T Wear<T>(AudioRoom r, mob_living_carbon_human who, float slot) where T : obj_item => r.In(() =>
    {
        var item = Dm.@new<T>(who.loc);
        Assert.True(Dm.truthy(Waiting.NoSleep(who.equip_to_slot_if_possible(item, slot, disable_warning: TRUE, bypass_equip_delay_self: TRUE))), typeof(T).Name + " was put on");
        return item;
    });

    private static mob_living_carbon_human Joined(AudioRoom r, int x, int y, string key)
    {
        mob_living_carbon_human who = r.Join(x, y, key);
        r.Step(20);
        return who;
    }

    [Fact]
    public void The_berserker_armour_charges_on_hits_taken_and_its_rage_halves_brute_until_it_drains()
    {
        var r = new AudioRoom(11, 9, seed: 13420);
        mob_living_carbon_human bob = Joined(r, 4, 4, "Bob");
        var suit = Wear<obj_item_clothing_suit_hooded_berserker>(r, bob, ITEM_SLOT_OCLOTHING);
        r.In(() => (suit.GetComponent(typeof(datum_component_toggle_attached_clothing)) as datum_component_toggle_attached_clothing).toggle_deployable());
        r.Step(2);
        var helmet = r.In(() => bob.head as obj_item_clothing_head_hooded_berserker);
        Assert.NotNull(helmet);
        Assert.True(r.In(() => HAS_TRAIT(helmet, TRAIT_NODROP)), "the helmet locks on");

        // hits taken charge it: 3/4 of the damage, half again for a projectile
        r.In(() => Waiting.NoSleep(helmet.hit_reaction(bob, null, "the attack", 0, 40, MELEE_ATTACK, BRUTE)));
        Assert.Equal(30f, r.In(() => Dm.num(helmet.berserk_charge)));
        r.In(() => Waiting.NoSleep(helmet.hit_reaction(bob, null, "the shot", 0, 40, PROJECTILE_ATTACK, BRUTE)));
        Assert.Equal(75f, r.In(() => Dm.num(helmet.berserk_charge)));
        r.In(() => Waiting.NoSleep(helmet.hit_reaction(bob, null, "the attack", 0, 100, MELEE_ATTACK, BRUTE)));
        Assert.Equal(100f, r.In(() => Dm.num(helmet.berserk_charge)));

        var rage = r.In(() => Dm.each<datum_action_item_action_berserk_mode>(helmet.actions).First());
        float brute = r.In(() => Dm.num(GET_PHYSIOLOGY(bob, BRUTE)));
        r.In(() => Waiting.NoSleep(rage.Trigger(bob)));
        Assert.True(r.In(() => Dm.truthy(helmet.berserk_active)), "berserk mode is on");
        Assert.Equal(brute * 0.5f, r.In(() => Dm.num(GET_PHYSIOLOGY(bob, BRUTE))), 3);
        Assert.True(r.In(() => HAS_TRAIT(bob, TRAIT_NOGUNS)), "no guns in a rage");
        Assert.True(r.In(() => Dm.truthy(Waiting.NoSleep(helmet.IsReflect()))), "the rage reflects");
        // charge drains 5 a second
        Assert.True(RunUntil(r, 25 * 10, () => !Dm.truthy(helmet.berserk_active)), "the rage ran out");
        Assert.Equal(brute, r.In(() => Dm.num(GET_PHYSIOLOGY(bob, BRUTE))), 3);
        Assert.False(r.In(() => HAS_TRAIT(bob, TRAIT_NOGUNS)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_memento_mori_keeps_its_wearer_from_dying_and_dusts_him_when_taken_off()
    {
        var r = new AudioRoom(11, 9, seed: 13421);
        mob_living_carbon_human bob = Joined(r, 4, 4, "Bob");
        var pendant = Wear<obj_item_clothing_neck_necklace_memento_mori>(r, bob, ITEM_SLOT_NECK);
        var link = r.In(() => Dm.each<datum_action_item_action_hands_free_memento_mori>(pendant.actions).First());
        r.Command(() => Waiting.Detach(link.Trigger(bob)));
        Assert.True(RunUntil(r, 60, () => HAS_TRAIT(bob, TRAIT_NODEATH)), "the pendant linked to Bob's life");
        Assert.Equal(bob, r.In(() => pendant.active_owner));
        r.In(() => Waiting.NoSleep(bob.adjust_brute_loss(400, forced: TRUE)));
        r.Step(4);
        Assert.NotEqual(DEAD, r.In(() => bob.stat));
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(pendant, force: TRUE)));
        Assert.True(RunUntil(r, 40, () => QDELETED(bob) || bob.stat == DEAD), "taking it off dusted Bob");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_concussive_gauntlets_teach_boxing_and_mine_the_rock_they_punch()
    {
        var r = new AudioRoom(11, 9, seed: 13422);
        mob_living_carbon_human bob = Joined(r, 4, 4, "Bob");
        r.In(() => Waiting.NoSleep(locate(5, 4, 1).ChangeTurf(typeof(turf_closed_mineral_volcanic_lava_land_surface), Dm.list(typeof(turf_open_misc_asteroid_basalt_lava_land_surface)))));
        var gauntlets = Wear<obj_item_clothing_gloves_gauntlets>(r, bob, ITEM_SLOT_GLOVES);
        Assert.Equal(TOOL_MINING, r.In(() => gauntlets.tool_behaviour as string));
        Assert.True(r.In(() => Dm.each<object>(bob.martial_arts).Any(m => m is datum_martial_art_boxing_hunter)), "the gauntlets taught hunter boxing");
        r.In(() => ClientSeam.MouseDown(bob.client, locate(5, 4, 1), "left=1;button=left")); // DV348: the dig runs while the button is held
        Assert.Equal(COMPONENT_CANCEL_ATTACK_CHAIN, r.In(() => Dm.num(gauntlets.rocksmash(bob, locate(5, 4, 1), TRUE))));
        Assert.True(RunUntil(r, 20 * 10, () => !ismineralturf(locate(5, 4, 1))), "the punch mined the rock");
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(gauntlets, force: TRUE)));
        Assert.Null(r.In(() => gauntlets.tool_behaviour as string));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_godslayer_armour_heals_its_wearer_back_from_critical_once_a_while()
    {
        var r = new AudioRoom(11, 9, seed: 13423);
        mob_living_carbon_human bob = Joined(r, 4, 4, "Bob");
        Wear<obj_item_clothing_suit_hooded_cloak_godslayer>(r, bob, ITEM_SLOT_OCLOTHING);
        r.In(() => Waiting.NoSleep(bob.adjust_brute_loss(150, forced: TRUE)));
        r.Step(4);
        Assert.Equal(STABLE, r.In(() => bob.stat));
        Assert.True(r.In(() => Dm.num(bob.get_brute_loss())) < 150, "the armour healed Bob back");
        // the second time within its cooldown it does not
        r.In(() => Waiting.NoSleep(bob.adjust_brute_loss(150, forced: TRUE)));
        r.Step(4);
        Assert.NotEqual(STABLE, r.In(() => bob.stat));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_spooky_lanterns_wisp_orbits_its_holder_and_goes_back_in()
    {
        var r = new AudioRoom(11, 9, seed: 13424);
        mob_living_carbon_human bob = Joined(r, 4, 4, "Bob");
        var lantern = Hold<obj_item_wisp_lantern>(r, bob);
        var wisp = r.In(() => lantern.wisp);
        Assert.Equal(lantern, r.In(() => wisp.loc));
        r.In(() => Waiting.NoSleep(lantern.attack_self(bob)));
        r.Step(4);
        Assert.Equal(bob, r.In(() => wisp.orbiting?.parent as atom));
        Assert.Equal("lantern-blue", r.In(() => lantern.icon_state));
        r.In(() => Waiting.NoSleep(lantern.attack_self(bob)));
        r.Step(2);
        Assert.Equal(lantern, r.In(() => wisp.loc));
        Assert.Equal("lantern-blue-on", r.In(() => lantern.icon_state));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void Jacobs_ladder_unfolds_through_every_level()
    {
        var r = new AudioRoom(11, 9, seed: 13425, levels: 2);
        mob_living_carbon_human bob = Joined(r, 4, 4, "Bob");
        var ladder = Hold<obj_item_jacobs_ladder>(r, bob);
        r.In(() => Waiting.NoSleep(ladder.attack_self(bob)));
        r.Step(2);
        Assert.True(r.In(() => QDELETED(ladder)), "the ladder unfolded");
        var below = r.In(() => Dm.each<obj_structure_ladder_unbreakable_jacob>(locate(4, 4, 1)).FirstOrDefault());
        var above = r.In(() => Dm.each<obj_structure_ladder_unbreakable_jacob>(locate(4, 4, 2)).FirstOrDefault());
        Assert.NotNull(below);
        Assert.NotNull(above);
        Assert.Equal(above, r.In(() => below.up));
        Assert.Equal(below, r.In(() => above.down));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_book_of_babel_teaches_every_language_and_crumbles()
    {
        var r = new AudioRoom(11, 9, seed: 13426);
        mob_living_carbon_human bob = Joined(r, 4, 4, "Bob");
        var book = Hold<obj_item_book_of_babel>(r, bob);
        Assert.False(r.In(() => Dm.truthy(bob.has_language(typeof(datum_language_draconic), SPOKEN_LANGUAGE))));
        r.In(() => Waiting.NoSleep(book.attack_self(bob)));
        r.Step(2);
        Assert.True(r.In(() => QDELETED(book)), "the book crumbled");
        Assert.True(r.In(() => Dm.truthy(bob.has_language(typeof(datum_language_draconic), SPOKEN_LANGUAGE))), "Bob speaks Draconic");
        Assert.True(r.In(() => HAS_TRAIT(bob.mind, TRAIT_TOWER_OF_BABEL)), "Bob is safe from Babel");
        Assert.True(r.In(() => Dm.each<obj_effect_decal_cleanable_ash>(bob.loc).Any()), "it left ash");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_potion_of_flight_grows_wings_on_a_human()
    {
        var r = new AudioRoom(11, 9, seed: 13427);
        mob_living_carbon_human bob = r.Dummy(4, 4);
        r.Step(2);
        Assert.Null(r.In(() => bob.get_organ_slot(ORGAN_SLOT_EXTERNAL_WINGS)));
        var potion = r.In(() => Dm.@new<obj_item_reagent_containers_cup_bottle_potion_flight>(bob.loc));
        r.Command(() => Waiting.Detach((potion.reagents as datum_reagents).expose(bob, TOUCH)));
        Assert.True(RunUntil(r, 20, () => bob.get_organ_slot(ORGAN_SLOT_EXTERNAL_WINGS) != null), "Bob grew wings");
        Assert.True(r.In(() => Dm.num(bob.get_brute_loss())) > 0, "the wings tore out of his back");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_rod_of_asclepius_takes_the_oath_and_its_bearer_heals_those_around_him()
    {
        var r = new AudioRoom(11, 9, seed: 13428);
        mob_living_carbon_human bob = Joined(r, 4, 4, "Bob");
        mob_living_carbon_human patient = r.Dummy(5, 4);
        var rod = Hold<obj_item_rod_of_asclepius>(r, bob);
        r.Command(() => Waiting.Detach(rod.attack_self(bob)));
        Assert.True(RunUntil(r, 30 * 10, () => bob.has_status_effect(typeof(datum_status_effect_hippocratic_oath)) != null), "Bob took the oath");
        Assert.True(r.In(() => Dm.truthy(rod.activated)), "the rod came alive");
        Assert.True(r.In(() => HAS_TRAIT(rod, TRAIT_NODROP)), "the rod grips Bob's arm");
        Assert.True(r.In(() => HAS_TRAIT(bob, TRAIT_PACIFISM)), "harm is a distant memory");
        r.In(() => Waiting.NoSleep(patient.adjust_brute_loss(40, forced: TRUE)));
        float hurt = r.In(() => Dm.num(patient.get_brute_loss()));
        Assert.True(RunUntil(r, 10 * 10, () => Dm.num(patient.get_brute_loss()) < hurt), "the aura healed the patient");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_warp_cubes_take_their_holder_to_each_other()
    {
        var r = new AudioRoom(13, 9, seed: 13429);
        mob_living_carbon_human bob = Joined(r, 3, 4, "Bob");
        var red = Hold<obj_item_warp_cube_red>(r, bob);
        var blue = r.In(() => red.linked);
        Assert.NotNull(blue);
        Assert.Equal(red, r.In(() => blue.linked));
        r.In(() => Waiting.NoSleep(blue.forceMove(locate(10, 4, 1))));
        r.Command(() => Waiting.Detach(red.attack_self(bob)));
        Assert.True(RunUntil(r, 20, () => bob.loc == locate(10, 4, 1)), "the cube took Bob to its twin");
        Assert.False(r.In(() => Dm.truthy(red.teleporting)));
        RunFor(r, 20);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_immortality_talisman_hides_its_holder_out_of_the_world_for_a_while()
    {
        var r = new AudioRoom(11, 9, seed: 13430);
        mob_living_carbon_human bob = Joined(r, 4, 4, "Bob");
        var talisman = Hold<obj_item_immortality_talisman>(r, bob);
        r.In(() => Waiting.NoSleep(talisman.attack_self(bob)));
        Assert.True(RunUntil(r, 20, () => bob.loc is obj_effect_immortality_talisman), "Bob vanished");
        Assert.True(r.In(() => HAS_TRAIT(bob, TRAIT_GODMODE)), "nothing touches him there");
        Assert.True(RunUntil(r, 15 * 10, () => bob.loc == locate(4, 4, 1)), "Bob came back");
        r.In(() => Waiting.NoSleep(talisman.attack_self(bob)));
        r.Step(2);
        Assert.True(r.In(() => bob.loc == locate(4, 4, 1)), "the talisman is not ready again yet");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_paradox_bags_share_one_inside()
    {
        var r = new AudioRoom(11, 9, seed: 13431);
        var red = r.In(() => Dm.@new<obj_item_shared_storage_red>(locate(4, 4, 1)));
        r.Step(2);
        var blue = r.In(() => Dm.each<obj_item_shared_storage_blue>(locate(4, 4, 1)).FirstOrDefault());
        Assert.NotNull(blue);
        Assert.Equal(red, r.In(() => blue.atom_storage.real_location as atom));
        var pen = r.In(() => Dm.@new<obj_item_pen>(locate(4, 4, 1)));
        Assert.True(r.In(() => Dm.truthy(blue.atom_storage.attempt_insert(pen, null, TRUE))), "the pen went into the blue bag");
        Assert.Equal(red, r.In(() => pen.loc));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_eye_of_god_grants_its_scan_which_pins_and_rebukes_its_target()
    {
        var r = new AudioRoom(13, 9, seed: 13432);
        mob_living_carbon_human bob = Joined(r, 3, 4, "Bob");
        mob_living_carbon_human target = r.Dummy(8, 4);
        var eye = Wear<obj_item_clothing_glasses_godeye>(r, bob, ITEM_SLOT_EYES);
        Assert.True(r.In(() => HAS_TRAIT(eye, TRAIT_NODROP)), "the eye locks itself in");
        var scan = r.In(() => eye.scan_ability);
        Assert.Equal(bob, r.In(() => scan.owner));
        r.Command(() => Waiting.Detach(scan.InterceptClickOn(bob, null, target)));
        Assert.True(RunUntil(r, 20, () => bob.has_status_effect(typeof(datum_status_effect_agent_pinpointer_scan)) != null), "the scan pins the target");
        Assert.Equal(target, r.In(() => (bob.has_status_effect(typeof(datum_status_effect_agent_pinpointer_scan)) as datum_status_effect_agent_pinpointer).scan_target));
        Assert.NotNull(r.In(() => target.has_status_effect(typeof(datum_status_effect_rebuked))));
        Assert.True(r.In(() => scan.next_use_time > world.time), "the scan went on cooldown");
        RunFor(r, 40);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_cursed_katana_strikes_its_combos()
    {
        var r = new AudioRoom(13, 9, seed: 13433);
        mob_living_carbon_human bob = Joined(r, 4, 4, "Bob");
        mob_living_carbon_human dummy = r.Dummy(5, 4);
        var katana = Hold<obj_item_cursed_katana>(r, bob);
        float before = r.In(() => Dm.num(dummy.health));
        // the cloak combo: the slice then the cloak (harm, harm, disarm): the wielder turns hard to see
        r.In(() => katana.cloak(dummy, bob));
        Assert.True(r.In(() => Dm.num(bob.alpha) < 255), "the cloak hid Bob");
        Assert.True(RunUntil(r, 10 * 10, () => Dm.num(bob.alpha) == 255), "the cloak wore off");
        r.In(() => Waiting.NoSleep(katana.cut(dummy, bob)));
        Assert.True(r.In(() => Dm.num(dummy.health)) < before, "the cut hurt");
        r.In(() => Waiting.NoSleep(katana.strike(dummy, bob)));
        RunFor(r, 20);
        r.In(() => Waiting.NoSleep(katana.forceMove(bob.loc)));
        r.In(() => Waiting.NoSleep(bob.put_in_active_hand(katana)));
        r.In(() => Waiting.NoSleep(katana.dash(dummy, bob)));
        RunFor(r, 20);
        Assert.True(r.In(() => Dm.num(dummy.health)) < before - 10, "the combos hurt the dummy");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void Cain_and_abels_combo_calls_wisps_and_they_fire_at_a_target()
    {
        var r = new AudioRoom(13, 9, seed: 13434);
        mob_living_carbon_human bob = Joined(r, 4, 4, "Bob");
        mob_living_carbon_human dummy = r.Dummy(9, 4);
        var daggers = Hold<obj_item_cain_and_abel>(r, bob);
        r.Step(4);
        Assert.Equal(0f, r.In(() => daggers.current_wisps.len));
        // each hit of a combo on a large mob calls a wisp up around the wielder
        r.In(() => daggers.set_combo(3, bob));
        float wisps = r.In(() => daggers.current_wisps.len);
        Assert.Equal(3f, wisps);
        float before = r.In(() => Dm.num(dummy.health));
        r.In(() => Waiting.NoSleep(daggers.fire_wisp(bob, dummy)));
        Assert.True(RunUntil(r, 40, () => Dm.num(dummy.health) < before), "a wisp hit the dummy");
        Assert.True(r.In(() => daggers.current_wisps.len) < wisps, "a wisp was spent");
        RunFor(r, 40);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_ship_in_a_bottle_breaks_into_a_dragon_boat_which_bottles_back_up()
    {
        var r = new AudioRoom(11, 9, seed: 13435);
        mob_living_carbon_human bob = Joined(r, 4, 4, "Bob");
        var bottle = Hold<obj_item_ship_in_a_bottle>(r, bob);
        r.In(() => Waiting.NoSleep(bottle.attack_self(bob)));
        r.Step(2);
        r.AssertNoRuntimes();
        Assert.True(r.In(() => QDELETED(bottle)), "the bottle broke");
        var boat = r.In(() => Dm.each<obj_vehicle_ridden_lavaboat_dragon>(bob.loc).FirstOrDefault());
        Assert.NotNull(boat);
        r.Command(() => Waiting.Detach(boat.click_alt(bob)));
        Assert.True(RunUntil(r, 40, () => QDELETED(boat)), "the boat was bottled");
        Assert.True(r.In(() => bob.get_active_held_item() is obj_item_ship_in_a_bottle), "the bottle is back in Bob's hand");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_lava_boat_rides_on_lava_with_its_oar()
    {
        var r = new AudioRoom(11, 9, seed: 13436);
        mob_living_carbon_human bob = Joined(r, 4, 4, "Bob");
        var boat = r.In(() => Dm.@new<obj_vehicle_ridden_lavaboat>(locate(4, 4, 1)));
        var oar = Hold<obj_item_oar>(r, bob);
        Assert.Equal(ITEM_INTERACT_SUCCESS, r.In(() => Waiting.NoSleep(boat.item_interaction(bob, oar))));
        Assert.Equal(oar, r.In(() => boat.inserted_key));
        // the ridable element gives the boat its riding when someone gets in
        r.In(() => Waiting.NoSleep(boat.buckle_mob(bob, TRUE)));
        r.Step(2);
        Assert.True(r.In(() => Dm.@in(bob, boat.buckled_mobs)), "Bob sits in the boat");
        var riding = r.In(() => boat.GetComponent(typeof(datum_component_riding_vehicle_lavaboat)) as datum_component_riding_vehicle_lavaboat);
        Assert.NotNull(riding);
        Assert.True(r.In(() => Dm.truthy(riding.allowed_turf_typecache[typeof(turf_open_lava)])), "the boat floats on lava");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_alien_welder_never_runs_out_of_fuel()
    {
        var r = new AudioRoom(11, 9, seed: 13437);
        mob_living_carbon_human bob = Joined(r, 4, 4, "Bob");
        var welder = Hold<obj_item_weldingtool_abductor>(r, bob);
        r.In(() => Waiting.NoSleep(welder.attack_self(bob)));
        Assert.True(r.In(() => Dm.truthy(welder.welding)), "the welder lit");
        r.In(() => Waiting.NoSleep((welder.reagents as datum_reagents).remove_reagent(typeof(datum_reagent_fuel), Dm.num(welder.get_fuel()) - 1)));
        float low = r.In(() => Dm.num(welder.get_fuel()));
        Assert.True(RunUntil(r, 40, () => Dm.num(welder.get_fuel()) > low), "the welder refuelled itself");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_instant_summons_spellbook_teaches_the_recall_of_a_marked_item()
    {
        var r = new AudioRoom(13, 9, seed: 13438);
        mob_living_carbon_human bob = Joined(r, 3, 4, "Bob");
        var book = Hold<obj_item_book_granter_action_spell_summonitem>(r, bob);
        r.In(() => Waiting.NoSleep(book.on_reading_finished(bob)));
        var spell = r.In(() => Dm.each<datum_action_cooldown_spell_summonitem>(bob.actions).FirstOrDefault());
        Assert.NotNull(spell);
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(book, force: TRUE)));
        var pen = Hold<obj_item_pen>(r, bob);
        r.In(() => Waiting.NoSleep(spell.cast(bob)));
        Assert.Equal(pen, r.In(() => spell.marked_item));
        Assert.StartsWith("Recall", r.In(() => spell.name));
        r.In(() => Waiting.NoSleep(pen.forceMove(locate(10, 4, 1))));
        r.In(() => spell.next_use_time = 0);
        r.In(() => Waiting.NoSleep(spell.cast(bob)));
        Assert.True(r.In(() => Dm.truthy(bob.is_holding(pen))), "the pen came back to Bob's hand");
        r.In(() => qdel(pen));
        Assert.Null(r.In(() => spell.marked_item));
        Assert.Equal("Instant Summons", r.In(() => spell.name));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_elites_crusher_trophies_do_their_work_on_a_detonation()
    {
        var r = new AudioRoom(13, 9, seed: 13439);
        mob_living_carbon_human bob = Joined(r, 4, 4, "Bob");
        var tongue = r.In(() => Dm.@new<obj_item_crusher_trophy_broodmother_tongue>(bob.loc));
        var spine = r.In(() => Dm.@new<obj_item_crusher_trophy_legionnaire_spine>(bob.loc));
        Assert.Contains("tentacle", r.In(() => tongue.effect_desc() as string));
        Assert.Contains("skull", r.In(() => spine.effect_desc() as string));
        var goliath = r.In(() => Dm.@new<mob_living_basic_mining_goliath>(locate(8, 4, 1)));
        r.In(() => Waiting.NoSleep((goliath.ai_controller as datum_ai_controller).set_ai_status(AI_STATUS_OFF)));
        r.In(() => tongue.bonus_value = 100);
        r.In(() => Waiting.NoSleep(tongue.on_mark_detonation(goliath, bob)));
        Assert.True(r.In(() => Dm.each<atom>(range(1, goliath)).Any(a => a is obj_effect_goliath_tentacle)), "the tongue raised a tentacle under the goliath");
        r.In(() => spine.bonus_value = 100);
        r.In(() => Waiting.NoSleep(spine.on_mark_detonation(goliath, bob)));
        Assert.True(RunUntil(r, 10, () => Dm.each<atom>(range(3, bob)).Any(a => a is mob_living_basic_mining_legion_brood)), "the spine let a skull out");
        RunFor(r, 30);
        r.AssertNoRuntimes();
    }
}
