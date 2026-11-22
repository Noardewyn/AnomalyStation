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
/// Gaps package C6: the last creatures. Every type spawns, thinks and goes away with no runtime; one behaviour per family that does
/// something particular: the statue stops when watched, the fugu inflates, the gorilla carries, the deer grazes, the hivebot fights,
/// the mushroom eats its kin, the gnome sinks, the creature jaunts, the lightgeist heals, the paper wizard summons.
/// </summary>
public class FaunaTests
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

    public static IEnumerable<object[]> Creatures() => new[]
    {
        typeof(mob_living_basic_hivebot), typeof(mob_living_basic_hivebot_range), typeof(mob_living_basic_hivebot_rapid), typeof(mob_living_basic_hivebot_strong), typeof(mob_living_basic_hivebot_mechanic),
        typeof(mob_living_basic_statue), typeof(mob_living_basic_statue_frosty), typeof(mob_living_basic_statue_mannequin), typeof(mob_living_basic_statue_mannequin_suspicious),
        typeof(mob_living_basic_eyeball), typeof(mob_living_basic_wumborian_fugu), typeof(mob_living_basic_blankbody), typeof(mob_living_basic_creature), typeof(mob_living_basic_creature_tiggles),
        typeof(mob_living_basic_creature_hatchling), typeof(mob_living_basic_migo), typeof(mob_living_basic_migo_hatsune), typeof(mob_living_basic_paper_wizard), typeof(mob_living_basic_paper_wizard_copy),
        typeof(mob_living_basic_garden_gnome), typeof(mob_living_basic_lightgeist), typeof(mob_living_basic_mushroom), typeof(mob_living_basic_ant), typeof(mob_living_basic_killer_tomato),
        typeof(mob_living_basic_faithless), typeof(mob_living_basic_ghost), typeof(mob_living_basic_ghost_swarm), typeof(mob_living_basic_spaceman), typeof(mob_living_basic_roro), typeof(mob_living_basic_cat_butcherer),
        typeof(mob_living_basic_gorilla), typeof(mob_living_basic_gorilla_lesser), typeof(mob_living_basic_gorilla_genetics), typeof(mob_living_basic_gorilla_hostile),
        typeof(mob_living_basic_deer), typeof(mob_living_basic_deer_ice),
        typeof(mob_living_basic_stickman), typeof(mob_living_basic_stickman_lesser), typeof(mob_living_basic_stickman_dog), typeof(mob_living_basic_stickman_ranged),
    }.Select(t => new object[] { t });

    [Theory]
    [MemberData(nameof(Creatures))]
    public void A_creature_spawns_thinks_on_its_own_tree_and_goes_away_cleanly(Type path)
    {
        var r = new AudioRoom(13, 9, seed: 8601);
        r.Join(11, 4, "Bob");
        var mob = (mob_living)r.In(() => Dm.@new(path, locate(4, 4, 1)));
        Assert.False(r.In(() => InertSeam.IsInert(mob)));
        Assert.NotNull(mob.ai_controller);
        RunFor(r, 100);
        if (!r.In(() => QDELETED(mob)))
            r.In(() => qdel(mob));
        RunFor(r, 20);
        r.AssertNoRuntimes();
    }

    [Theory]
    [InlineData(typeof(mob_living_basic_gorilla_cargorilla))]
    [InlineData(typeof(mob_living_basic_gorilla_bar))]
    public void The_stations_gorillas_spawn_and_go_away_cleanly(Type path)
    {
        // the cargorilla has no AI (tg's null controller); Pun Pun runs the monkey controller (live with C5)
        var r = new AudioRoom(13, 9, seed: 8602);
        r.Join(11, 4, "Bob");
        var mob = (mob_living)r.In(() => Dm.@new(path, locate(4, 4, 1)));
        Assert.False(r.In(() => InertSeam.IsInert(mob)));
        RunFor(r, 50);
        r.In(() => qdel(mob));
        RunFor(r, 20);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_statue_stands_still_while_watched_and_comes_for_you_once_you_are_blind()
    {
        var r = new AudioRoom(13, 9, seed: 8603);
        mob_living_carbon_human bob = r.Join(9, 4, "Bob");
        var statue = r.In(() => Dm.@new<mob_living_basic_statue>(locate(4, 4, 1)));
        turf start = r.In(() => get_turf(statue));
        RunFor(r, 80);
        Assert.Same(start, r.In(() => get_turf(statue)));
        Assert.True(r.In(() => Dm.num(bob.health) == Dm.num(bob.maxHealth)));

        r.In(() => Waiting.NoSleep(bob.adjust_temp_blindness(120 * SECONDS)));
        Assert.True(RunUntil(r, 300, () => Dm.num(bob.health) < Dm.num(bob.maxHealth)), r.In(() => "the statue reached the blind Bob and clawed him; it is at " + statue.x + "; " + string.Join(";", r.Room.RuntimeText)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_statues_blindness_blinds_those_in_range()
    {
        var r = new AudioRoom(13, 9, seed: 8604);
        mob_living_carbon_human bob = r.Dummy(9, 4);
        var statue = r.In(() => Dm.@new<mob_living_basic_statue>(locate(4, 4, 1)));
        var spell = r.In(() => Dm.each<datum_action_cooldown_spell_aoe_blindness>(statue.actions).First());
        r.In(() => Waiting.NoSleep(spell.PreActivate(statue)));
        Assert.True(r.In(() => Dm.truthy(bob.is_blind())));
        Assert.True(r.In(() => spell.next_use_time > world.time), "the cooldown runs");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_fugu_inflates_for_ten_seconds_and_its_gland_makes_another_animal_big()
    {
        var r = new AudioRoom(13, 9, seed: 8605);
        r.Join(11, 4, "Bob");
        var fugu = r.In(() => Dm.@new<mob_living_basic_wumborian_fugu>(locate(4, 4, 1)));
        r.In(() => Waiting.NoSleep(fugu.expand.PreActivate(fugu)));
        Assert.Equal("Fugu1", r.In(() => fugu.icon_state));
        Assert.Equal(15f, r.In(() => Dm.num(fugu.melee_damage_lower)));
        Assert.True(r.In(() => HAS_TRAIT(fugu, TRAIT_GODMODE)));
        Assert.True(RunUntil(r, 120, () => fugu.icon_state == "Fugu0"), "it shrank back");
        Assert.Equal(0f, r.In(() => Dm.num(fugu.melee_damage_lower)));
        Assert.False(r.In(() => HAS_TRAIT(fugu, TRAIT_GODMODE)));
        object listeners = r.In(() => fugu._listen_lookup?[COMSIG_LIVING_UNARMED_ATTACK]);
        Assert.False(listeners is datum_element_wall_tearer || (listeners is DmList many && r.In(() => Dm.each<datum_element_wall_tearer>(many).Any())), "the wall tearing went with the size");

        var gland = r.In(() => Dm.@new<obj_item_fugu_gland>(locate(7, 7, 1)));
        var corgi = r.In(() => Dm.@new<mob_living_basic_spaceman>(locate(7, 6, 1)));
        mob_living_carbon_human user = r.Dummy(8, 6);
        float before = r.In(() => Dm.num(corgi.maxHealth));
        r.In(() => Waiting.NoSleep(gland.interact_with_atom(corgi, user)));
        Assert.Equal(before * 1.5f, r.In(() => Dm.num(corgi.maxHealth)));
        Assert.True(r.In(() => HAS_TRAIT(corgi, TRAIT_FUGU_GLANDED)));
        r.Step();
        Assert.True(r.In(() => QDELETED(gland)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_gorilla_stands_up_to_carry_things_and_a_cargorilla_carries_crates()
    {
        var r = new AudioRoom(13, 9, seed: 8606);
        var gorilla = r.In(() => Dm.@new<mob_living_basic_gorilla>(locate(4, 4, 1)));
        var wrench = r.In(() => Dm.@new<obj_item_wrench>(locate(4, 4, 1)));
        Assert.Equal(2f, r.In(() => (float)Dm.length(GLOB.strippable_gorilla_items)));
        Assert.Equal("crawling", r.In(() => gorilla.icon_state));
        Assert.True(r.In(() => Dm.truthy(Waiting.NoSleep(gorilla.put_in_active_hand(wrench)))), "dextrous hands hold it");
        r.Step();
        Assert.Equal("standing", r.In(() => gorilla.icon_state));
        Assert.True(r.In(() => Dm.truthy(gorilla.has_movespeed_modifier(typeof(datum_movespeed_modifier_gorilla_standing)))));
        Assert.True(r.In(() => Dm.length((gorilla.GetComponent(typeof(datum_component_basic_inhands)) as datum_component_basic_inhands).cached_overlays) > 0), "the wrench shows in the hand");

        var cargo = r.In(() => Dm.@new<mob_living_basic_gorilla_cargorilla>(locate(7, 7, 1)));
        var crate = r.In(() => Dm.@new<obj_structure_closet_crate>(locate(8, 7, 1)));
        var carrier = r.In(() => cargo.GetComponent(typeof(datum_component_crate_carrier)) as datum_component_crate_carrier);
        r.In(() => cargo.combat_mode = FALSE);
        r.In(() => carrier.on_unarm_attack(cargo, crate, TRUE));
        Assert.Same(cargo, r.In(() => crate.loc));
        r.In(() => carrier.on_unarm_attack(cargo, locate(8, 6, 1), TRUE));
        Assert.Same(r.In(() => locate(8, 6, 1)), r.In(() => crate.loc));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_gorilla_tears_through_a_wall_in_three_goes_but_not_a_reinforced_one()
    {
        var r = new AudioRoom(13, 9, seed: 8607);
        var gorilla = r.In(() => Dm.@new<mob_living_basic_gorilla_cargorilla>(locate(4, 4, 1))); // no AI to wander off
        r.In(() => Waiting.NoSleep(locate(5, 4, 1).ChangeTurf(typeof(turf_closed_wall))));
        turf wall = r.In(() => locate(5, 4, 1));
        r.In(() => Waiting.Detach(gorilla.UnarmedAttack(wall, TRUE)));
        Assert.True(RunUntil(r, 50, () => locate(5, 4, 1) is not turf_closed_wall), "the wall came down");

        r.In(() => Waiting.NoSleep(locate(4, 5, 1).ChangeTurf(typeof(turf_closed_wall_r_wall))));
        r.In(() => Waiting.Detach(gorilla.UnarmedAttack(locate(4, 5, 1), TRUE)));
        RunFor(r, 30);
        Assert.IsAssignableFrom<turf_closed_wall_r_wall>(r.In(() => locate(4, 5, 1)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_deer_grazes_on_grass_marks_its_tree_and_rests()
    {
        var r = new AudioRoom(13, 9, seed: 8608);
        r.Join(11, 4, "Bob");
        var deer = r.In(() => Dm.@new<mob_living_basic_deer>(locate(4, 4, 1)));
        var controller = r.In(() => deer.ai_controller as datum_ai_controller);
        r.In(() => Waiting.NoSleep(locate(5, 4, 1).ChangeTurf(typeof(turf_open_floor_grass))));
        Assert.True(RunUntil(r, 1200, () => Dm.truthy(controller.blackboard[BB_DEER_GRAZE_COOLDOWN])), "it found the grass and grazed");

        var tree = r.In(() => Dm.@new<obj_effect_decal_cleanable_ants>(locate(7, 7, 1))); // any atom stands for tg's tree in the leaf
        var mark = r.In(() => Dm.@new<datum_bt_node_ai_behavior_hunt_target_deer_mark>());
        r.In(() => Waiting.NoSleep(mark.target_caught(deer, tree)));
        Assert.Same(tree, r.In(() => controller.blackboard[BB_DEER_TREEHOME]));

        var rest = r.In(() => Dm.@new<datum_bt_node_ai_behavior_deer_rest>());
        float before = r.In(() => world.time);
        r.In(() => Waiting.NoSleep(rest.finish_action(controller, TRUE)));
        Assert.True(r.In(() => Dm.num(controller.blackboard[BB_DEER_NEXT_REST_TIMER]) >= before + 2 * MINUTES));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_hivebot_fights_and_a_mechanic_repairs_its_kin_and_machines()
    {
        var r = new AudioRoom(13, 9, seed: 8609);
        mob_living_carbon_human bob = r.Join(9, 4, "Bob");
        var bot = r.In(() => Dm.@new<mob_living_basic_hivebot>(locate(5, 4, 1)));
        Assert.True(RunUntil(r, 300, () => Dm.num(bob.health) < Dm.num(bob.maxHealth)), "the hivebot reached Bob and clawed him");
        Assert.True(r.In(() => (bot.ai_controller as datum_ai_controller).blackboard[BB_CURRENT_TARGET] == bob));
        r.In(() => qdel(bot));

        var mechanic = r.In(() => Dm.@new<mob_living_basic_hivebot_mechanic>(locate(2, 7, 1)));
        var hurt = r.In(() => Dm.@new<mob_living_basic_hivebot_strong>(locate(3, 7, 1)));
        r.In(() => hurt.faction = Dm.list(FACTION_HIVEBOT));
        r.In(() => Waiting.NoSleep(hurt.adjust_brute_loss(30)));
        r.In(() => Waiting.NoSleep(mechanic.repair_hivebot(hurt)));
        Assert.Equal(r.In(() => Dm.num(hurt.maxHealth)), r.In(() => Dm.num(hurt.health)));
        Assert.True(r.In(() => mechanic.repair_cooldown > world.time));

        var light = r.In(() => Dm.@new<obj_machinery_light>(locate(2, 8, 1)));
        r.In(() => { light.update_integrity(5f); mechanic.repair_cooldown = 0; });
        r.In(() => mechanic.repair_machine(light));
        Assert.Equal(r.In(() => Dm.num(light.max_integrity)), r.In(() => Dm.num(light.get_integrity())));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_mechanics_foam_wall_rises_under_it()
    {
        var r = new AudioRoom(13, 9, seed: 8610);
        var mechanic = r.In(() => Dm.@new<mob_living_basic_hivebot_mechanic>(locate(4, 4, 1)));
        var foam = r.In(() => Dm.each<datum_action_cooldown_spell_conjure_foam_wall>(mechanic.actions).First());
        r.In(() => Waiting.NoSleep(foam.PreActivate(mechanic)));
        Assert.NotNull(r.In(() => Dm.locate<obj_structure_foamedmetal>(locate(4, 4, 1))));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_mushroom_chews_its_dead_kin_devours_it_and_recovers_on_a_mushroom()
    {
        var r = new AudioRoom(13, 9, seed: 8611);
        var eater = r.In(() => Dm.@new<mob_living_basic_mushroom>(locate(4, 4, 1)));
        var meal = r.In(() => Dm.@new<mob_living_basic_mushroom>(locate(5, 4, 1)));
        r.In(() => Waiting.NoSleep(meal.death()));
        Assert.Equal("mushroom_dead", r.In(() => meal.icon_state));

        for (int i = 1; i <= 3; i++)
        {
            int chew = i;
            r.In(() => Waiting.NoSleep(eater.melee_attack(meal, ignore_cooldown: TRUE)));
            r.Step(); // the attack's short animation
            Assert.Equal((float)chew, r.In(() => meal.faint_ticker));
        }
        float level = r.In(() => meal.powerlevel);
        r.In(() => Waiting.NoSleep(eater.melee_attack(meal, ignore_cooldown: TRUE)));
        r.Step();
        Assert.True(r.In(() => QDELETED(meal)));
        Assert.True(r.In(() => meal.powerlevel > level), "tg's quirk: the eaten one levels up");

        r.In(() => Waiting.NoSleep(eater.death()));
        var food = r.In(() => Dm.@new<obj_item_food_grown_mushroom_reishi>(locate(4, 4, 1))); // the base is abstract, no seed
        r.In(() => Waiting.NoSleep(eater.recover(food)));
        Assert.NotEqual(DEAD, r.In(() => eater.stat));
        Assert.Equal("mushroom_color", r.In(() => eater.icon_state));

        r.In(() => { eater.powerlevel = 1; Waiting.NoSleep(eater.harvest()); });
        Assert.Equal(2, r.In(() => Dm.each<obj_item_food_hugemushroomslice>(locate(4, 4, 1)).Count()));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_gnome_sinks_into_the_ground_when_still_and_pops_out_when_it_moves()
    {
        var r = new AudioRoom(13, 9, seed: 8612);
        var gnome = r.In(() => Dm.@new<mob_living_basic_garden_gnome>(locate(4, 4, 1)));
        var sinking = r.In(() => gnome.GetComponent(typeof(datum_component_ground_sinking)) as datum_component_ground_sinking);
        r.In(() => sinking.start_sinking(gnome));
        Assert.True(RunUntil(r, 60, () => Dm.truthy(sinking.sinked)), "three goes of a second each");
        Assert.Equal("gnome_burried_3", r.In(() => gnome.icon_state));
        Assert.Equal(0f, r.In(() => Dm.num(gnome.density)));
        Assert.Equal(0.5f, r.In(() => Dm.num((gnome.physiology as DmList)[BRUTE])), 3);

        r.In(() => sinking.on_moved(gnome));
        Assert.Equal("gnome", r.In(() => gnome.icon_state));
        Assert.Equal(1f, r.In(() => Dm.num(gnome.density)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_creature_jaunts_only_when_nobody_sees_it()
    {
        var r = new AudioRoom(13, 9, seed: 8613);
        var creature = r.In(() => Dm.@new<mob_living_basic_creature>(locate(4, 4, 1)));
        r.In(() => ADD_TRAIT(creature, TRAIT_AI_PAUSED, "test")); // it would wander out of its six still seconds
        var jaunt = r.In(() => Dm.each<datum_action_cooldown_spell_jaunt_creature_teleport>(creature.actions).First());
        r.In(() => Waiting.Detach(jaunt.PreActivate(creature)));
        Assert.True(RunUntil(r, 80, () => creature.loc is obj_effect_dummy_phased_mob), r.In(() => "six still seconds and it is gone: loc=" + creature.loc + " busy=" + Dm.length(creature.do_afters) + " next=" + jaunt.next_use_time + " phased=" + Dm.each<obj_effect_dummy_phased_mob>(world).Count() + " rt=" + string.Join(";", r.Room.RuntimeText)));
        Assert.True(r.In(() => HAS_TRAIT(creature, TRAIT_MAGICALLY_PHASED)));
        var holder = r.In(() => creature.loc as obj_effect_dummy_phased_mob);
        r.In(() => Waiting.NoSleep(holder.relaymove(creature, EAST)));
        Assert.Same(r.In(() => locate(5, 4, 1)), r.In(() => holder.loc));

        r.In(() => jaunt.next_use_time = 0);
        r.In(() => Waiting.Detach(jaunt.PreActivate(creature)));
        Assert.True(RunUntil(r, 80, () => creature.loc is turf), "it came back");
        Assert.False(r.In(() => HAS_TRAIT(creature, TRAIT_MAGICALLY_PHASED)));

        // a player in view blocks it (the unobserved actor)
        r.Join(7, 4, "Bob");
        r.In(() => jaunt.next_use_time = 0);
        Assert.False(r.In(() => Dm.truthy(Waiting.NoSleep(jaunt.PreActivate(creature)))));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_lightgeist_mends_the_wounded()
    {
        var r = new AudioRoom(13, 9, seed: 8614);
        mob_living_carbon_human bob = r.Join(5, 4, "Bob");
        r.In(() => Waiting.NoSleep(bob.apply_damage(30, BRUTE, BODY_ZONE_CHEST)));
        float hurt = r.In(() => Dm.num(bob.get_brute_loss()));
        Assert.True(hurt > 0);
        var geist = r.In(() => Dm.@new<mob_living_basic_lightgeist>(locate(4, 4, 1)));
        Assert.True(RunUntil(r, 300, () => Dm.num(bob.get_brute_loss()) < hurt), "it reached Bob and mended him");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_paper_wizard_summons_minions_and_clones_that_vanish_when_he_is_hurt_and_leaves_a_portal_on_death()
    {
        var r = new AudioRoom(13, 9, seed: 8615);
        mob_living_carbon_human bob = r.Dummy(8, 4);
        var wizard = r.In(() => Dm.@new<mob_living_basic_paper_wizard>(locate(4, 4, 1)));
        var summon = r.In(() => Dm.each<datum_action_cooldown_spell_conjure_wizard_summon_minions>(wizard.actions).First());
        r.In(() => Waiting.NoSleep(summon.PreActivate(wizard)));
        Assert.Equal(2f, r.In(() => summon.summoned_minions));

        var mimic = r.In(() => Dm.each<datum_action_cooldown_spell_pointed_wizard_mimic>(wizard.actions).First());
        r.In(() => Waiting.NoSleep(mimic.PreActivate(bob)));
        Assert.Equal(3, r.In(() => Dm.length(mimic.copies)));
        r.In(() => Waiting.NoSleep(wizard.adjust_brute_loss(5)));
        r.Step();
        Assert.Equal(0, r.In(() => Dm.length(mimic.copies)));

        r.In(() => Waiting.NoSleep(wizard.death()));
        Assert.True(RunUntil(r, 40, () => Dm.each<obj_item_clothing_suit_wizrobe_paper>(world).Any()), "the portal closes and leaves the robe");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_faithless_pries_an_unpowered_airlock_open_and_space_ants_bite()
    {
        var r = new AudioRoom(13, 9, seed: 8616);
        var airlock = r.In(() => Dm.@new<obj_machinery_door_airlock>(locate(5, 4, 1)));
        var faithless = r.In(() => Dm.@new<mob_living_basic_faithless>(locate(4, 4, 1)));
        r.In(() => faithless.combat_mode = FALSE);
        Assert.Equal(1f, r.In(() => Dm.num(airlock.density)));
        r.In(() => Waiting.Detach(faithless.UnarmedAttack(airlock, TRUE)));
        Assert.True(RunUntil(r, 40, () => !Dm.truthy(airlock.density)), "it forced the door");

        var ants = r.In(() => Dm.@new<obj_effect_decal_cleanable_ants>(locate(8, 8, 1)));
        var bite = r.In(() => ants.GetComponent(typeof(datum_component_caltrop)) as datum_component_caltrop);
        Assert.NotNull(bite);
        r.In(() => Waiting.NoSleep(ants.update_ant_damage(1f, 8f)));
        Assert.Equal("ants_4", r.In(() => ants.icon_state));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_gorilla_takes_a_limb_off_the_downed_and_a_cat_surgeon_slashes()
    {
        var r = new AudioRoom(13, 9, seed: 8617);
        mob_living_carbon_human bob = r.Dummy(5, 4);
        var gorilla = r.In(() => Dm.@new<mob_living_basic_gorilla>(locate(4, 4, 1)));
        r.In(() => ADD_TRAIT(gorilla, TRAIT_AI_PAUSED, "test"));
        r.In(() => bob.stat = HARD_CRIT);
        float limbs = r.In(() => (float)Dm.length(bob.get_bodyparts()));
        r.In(() => Waiting.NoSleep(gorilla.melee_attack(bob, ignore_cooldown: TRUE)));
        Assert.True(RunUntil(r, 10, () => Dm.length(bob.get_bodyparts()) < limbs), "a limb came off");
        r.AssertNoRuntimes();

        r = new AudioRoom(13, 9, seed: 8620);
        mob_living_carbon_human dan = r.Join(9, 7, "Dan");
        var surgeon = r.In(() => Dm.@new<mob_living_basic_cat_butcherer>(locate(5, 7, 1)));
        float start = r.In(() => Dm.num(dan.health));
        Assert.True(RunUntil(r, 300, () => Dm.num(dan.health) < start), "he reached Dan");
        Assert.Equal(Dm.resource("icons/mob/human/human.dmi"), r.In(() => surgeon.icon));
        r.In(() => Waiting.NoSleep(surgeon.death()));
        r.Step();
        Assert.NotNull(r.In(() => Dm.each<obj_item_circular_saw>(world).FirstOrDefault()));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void An_eyeball_heals_a_hurt_eye_and_its_glare_slows_who_looks_at_it()
    {
        var r = new AudioRoom(13, 9, seed: 8618);
        mob_living_carbon_human bob = r.Dummy(5, 4);
        var eyeball = r.In(() => Dm.@new<mob_living_basic_eyeball>(locate(4, 4, 1)));
        r.In(() => ADD_TRAIT(eyeball, TRAIT_AI_PAUSED, "test"));
        var eyes = r.In(() => bob.get_organ_slot(ORGAN_SLOT_EYES) as obj_item_organ_eyes);
        r.In(() => Waiting.NoSleep(eyes.apply_organ_damage(15)));
        float hurt = r.In(() => Dm.num(eyes.damage));
        r.In(() => Waiting.NoSleep(eyeball.early_melee_attack(bob)));
        Assert.Equal(hurt - 3, r.In(() => Dm.num(eyes.damage)));

        var glare = r.In(() => Dm.each<datum_action_cooldown_spell_pointed_death_glare>(eyeball.actions).First());
        r.In(() => Waiting.NoSleep(bob.setDir(WEST)));
        r.In(() => Waiting.NoSleep(glare.PreActivate(bob)));
        Assert.True(r.In(() => Dm.truthy(bob.has_movespeed_modifier(typeof(datum_movespeed_modifier_glare_slowdown)))));
        Assert.True(RunUntil(r, 60, () => !Dm.truthy(bob.has_movespeed_modifier(typeof(datum_movespeed_modifier_glare_slowdown)))), "the glare wears off");

        r.In(() => Dm.@new<obj_item_stack_sheet_iron>(locate(4, 4, 1)));
        r.In(() => Waiting.NoSleep(eyeball.attackby(r.In(() => Dm.@new<obj_item_wrench>(locate(4, 4, 1))), bob)));
        Assert.True(r.In(() => Dm.truthy(eyeball.crying)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_mi_go_dodges_sideways_and_a_ranged_hivebot_shoots()
    {
        var r = new AudioRoom(13, 9, seed: 8619);
        var migo = r.In(() => Dm.@new<mob_living_basic_migo>(locate(4, 4, 1)));
        r.In(() => { ADD_TRAIT(migo, TRAIT_AI_PAUSED, "test"); migo.dodge_prob = 100; });
        r.In(() => Waiting.NoSleep(migo.Move(locate(5, 4, 1), EAST)));
        Assert.Equal(5f, r.In(() => migo.x));
        Assert.NotEqual(4f, r.In(() => migo.y));

        mob_living_carbon_human bob = r.Join(9, 2, "Bob");
        var gunner = r.In(() => Dm.@new<mob_living_basic_hivebot_range>(locate(3, 2, 1)));
        Assert.True(RunUntil(r, 300, () => Dm.num(bob.health) < Dm.num(bob.maxHealth)), "a bullet found Bob");
        r.AssertNoRuntimes();
    }
}
