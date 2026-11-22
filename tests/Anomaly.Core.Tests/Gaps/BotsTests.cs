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
/// Gaps package C3: the bots (modules/mob/living/basic/bots, datums/ai/bots) in a booted toy room: every type spawns and dies clean; a cleanbot cleans a decal; a medbot heals;
/// a secbot stuns and cuffs its attacker; a MULE delivers a crate between two beacons; a patrol follows beacons; a firebot puts out a burning man; the assemblies; the welder.
/// </summary>
public class BotsTests
{
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

    private static string Tree(AudioRoom r, mob_living_basic_bot bot) => r.In(() =>
    {
        var brain = (datum_ai_controller)bot.ai_controller;
        DmList lines = Dm.list();
        ((datum_bt_node)brain.behavior_nodes[1]).append_full_tree_state(lines, "");
        return $"{bot.name} at {bot.x},{bot.y}, mode {bot.mode}, status {brain.ai_status}\n" + lines.Join("\n") + "\n" + string.Join("\n", r.Room.RuntimeText);
    });

    private static obj_machinery_navbeacon Beacon(AudioRoom r, int x, int y, string location, string codes) => r.In(() =>
    {
        var beacon = Dm.@new<obj_machinery_navbeacon>(locate(x, y, 1));
        beacon.location = location;
        beacon.codes_txt = codes;
        beacon.glob_lists_deregister();
        beacon.set_codes();
        beacon.glob_lists_register();
        return beacon;
    });

    [Fact]
    public void Every_bot_and_assembly_type_spawns_and_is_destroyed_with_no_runtimes()
    {
        var r = new AudioRoom(13, 9, seed: 7801);
        foreach (Type type in new[]
        {
            typeof(mob_living_basic_bot_cleanbot), typeof(mob_living_basic_bot_cleanbot_autopatrol), typeof(mob_living_basic_bot_cleanbot_medbay),
            typeof(mob_living_basic_bot_medbot), typeof(mob_living_basic_bot_medbot_autopatrol), typeof(mob_living_basic_bot_medbot_stationary), typeof(mob_living_basic_bot_medbot_mysterious), typeof(mob_living_basic_bot_medbot_derelict), typeof(mob_living_basic_bot_medbot_nukie),
            typeof(mob_living_basic_bot_mulebot), typeof(mob_living_basic_bot_mulebot_paranormal), typeof(mob_living_basic_bot_repairbot),
            typeof(mob_living_basic_bot_secbot), typeof(mob_living_basic_bot_secbot_beepsky), typeof(mob_living_basic_bot_secbot_beepsky_officer), typeof(mob_living_basic_bot_secbot_beepsky_ofitser), typeof(mob_living_basic_bot_secbot_beepsky_armsky), typeof(mob_living_basic_bot_secbot_beepsky_jr), typeof(mob_living_basic_bot_secbot_pingsky), typeof(mob_living_basic_bot_secbot_genesky),
            typeof(mob_living_basic_bot_secbot_grievous), typeof(mob_living_basic_bot_secbot_grievous_toy), typeof(mob_living_basic_bot_secbot_ed209), typeof(mob_living_basic_bot_secbot_ed209_nukie), typeof(mob_living_basic_bot_secbot_honkbot), typeof(mob_living_basic_bot_secbot_honkbot_nopatrol),
            typeof(mob_living_basic_bot_firebot), typeof(mob_living_basic_bot_hygienebot), typeof(mob_living_basic_bot_vibebot), typeof(mob_living_basic_bot_dedbot),
            typeof(obj_item_bot_assembly_cleanbot), typeof(obj_item_bot_assembly_ed209), typeof(obj_item_bot_assembly_repairbot), typeof(obj_item_bot_assembly_medbot), typeof(obj_item_bot_assembly_honkbot), typeof(obj_item_bot_assembly_secbot), typeof(obj_item_bot_assembly_firebot), typeof(obj_item_bot_assembly_hygienebot), typeof(obj_item_bot_assembly_vim),
            typeof(obj_item_mop), typeof(obj_item_assembly_prox_sensor), typeof(obj_machinery_navbeacon), typeof(obj_item_carried_repairbot),
        })
        {
            atom spawned = r.In(() => Dm.@new(type, locate(5, 4, 1)) as atom);
            Assert.False(InertSeam.IsInert(spawned), type.Name);
            r.Step(10);
            Assert.True(r.Room.Runtimes.Count == 0, type.Name + "\n" + string.Join("\n", r.Room.RuntimeText));
            r.In(() => qdel(spawned));
            r.Step(5);
            Assert.True(r.Room.Runtimes.Count == 0, type.Name + " deleted\n" + string.Join("\n", r.Room.RuntimeText));
        }
    }

    [Fact]
    public void Every_bot_works_a_room_full_of_chores_then_emagged_then_dies_with_no_runtimes()
    {
        foreach (Type type in new[]
        {
            typeof(mob_living_basic_bot_cleanbot), typeof(mob_living_basic_bot_medbot), typeof(mob_living_basic_bot_mulebot), typeof(mob_living_basic_bot_repairbot), typeof(mob_living_basic_bot_secbot), typeof(mob_living_basic_bot_secbot_beepsky),
            typeof(mob_living_basic_bot_secbot_grievous), typeof(mob_living_basic_bot_secbot_grievous_toy), typeof(mob_living_basic_bot_secbot_ed209), typeof(mob_living_basic_bot_secbot_honkbot), typeof(mob_living_basic_bot_firebot), typeof(mob_living_basic_bot_hygienebot), typeof(mob_living_basic_bot_vibebot), typeof(mob_living_basic_bot_dedbot),
        })
        {
            var r = new AudioRoom(13, 9, seed: 7812);
            mob_living_basic_bot bot = r.In(() => Dm.@new(type, locate(2, 4, 1)) as mob_living_basic_bot);
            mob_living_carbon_human bob = r.Join(5, 4, "Bob");
            r.In(() =>
            {
                Waiting.NoSleep(bob.adjust_brute_loss(30));
                Waiting.NoSleep(bob.add_blood_DNA(bob.get_blood_dna_list() as DmList));
                Dm.@new<obj_effect_decal_cleanable_dirt>(locate(4, 5, 1));
                Dm.@new<obj_item_stack_sheet_iron>(locate(3, 5, 1), 5);
                Dm.@new<obj_structure_girder>(locate(7, 2, 1));
                Dm.@new<obj_structure_grille>(locate(8, 2, 1));
                bot.bot_access_flags = Dm.bitand(bot.bot_access_flags, Dm.bitnot(BOT_COVER_LOCKED));
                bot.set_mode_flags(Dm.bitor(bot.bot_mode_flags, BOT_MODE_ON));
                bot.turn_on();
            });
            r.Step(400); // 20 s of its tree
            Assert.True(r.Room.Runtimes.Count == 0, type.Name + "\n" + string.Join("\n", r.Room.RuntimeText));
            r.In(() =>
            {
                bot.bot_access_flags = Dm.bitor(bot.bot_access_flags, BOT_COVER_MAINTS_OPEN);
                Waiting.NoSleep(bot.emag_act(bob));
            });
            Assert.True(r.In(() => Dm.truthy(Dm.bitand(bot.bot_access_flags, BOT_COVER_EMAGGED))), type.Name);
            r.Step(400); // 20 s emagged
            Assert.True(r.Room.Runtimes.Count == 0, type.Name + " emagged\n" + string.Join("\n", r.Room.RuntimeText));
            r.In(() =>
            {
                if (!QDELETED(bot))
                    Waiting.NoSleep(bot.death());
            });
            r.Step(20);
            Assert.True(r.In(() => QDELETED(bot)), type.Name + " is deleted on death");
            Assert.True(r.Room.Runtimes.Count == 0, type.Name + " dead\n" + string.Join("\n", r.Room.RuntimeText));
        }
    }

    [Fact]
    public void A_cleanbot_goes_to_a_decal_scrubs_it_with_tgs_picture_and_it_is_gone()
    {
        var r = new AudioRoom(13, 9, seed: 7802);
        mob_living_basic_bot_cleanbot bot = r.In(() => Dm.@new<mob_living_basic_bot_cleanbot>(locate(2, 4, 1)));
        obj_effect_decal_cleanable_dirt dirt = r.In(() => Dm.@new<obj_effect_decal_cleanable_dirt>(locate(6, 4, 1)));
        Assert.Equal("cleanbot1", r.In(() => bot.icon_state));
        Assert.False(r.In(() => QDELETED(dirt)), "the decal lives");
        bool scrubbing = false;
        Assert.True(RunUntil(r, 400, () =>
        {
            scrubbing |= bot.icon_state == "cleanbot-c";
            return QDELETED(dirt);
        }), Tree(r, bot));
        Assert.True(scrubbing, "the scrubbing picture\n" + string.Join("\n", r.Room.RuntimeText));
        Assert.True(RunUntil(r, 40, () => bot.icon_state == "cleanbot1"), Tree(r, bot));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_clean_command_takes_a_mess_and_refuses_anything_else()
    {
        var r = new AudioRoom(13, 9, seed: 7812);
        mob_living_basic_bot_cleanbot bot = r.In(() => Dm.@new<mob_living_basic_bot_cleanbot>(locate(2, 4, 1)));
        obj_effect_decal_cleanable_dirt dirt = r.In(() => Dm.@new<obj_effect_decal_cleanable_dirt>(locate(3, 4, 1)));
        r.In(() =>
        {
            Assert.NotNull(bot.GetComponent(typeof(datum_component_obeys_commands)));
            var clean = Dm.@new<datum_pet_command_clean>(bot);
            Assert.False(Dm.truthy(Waiting.NoSleep(clean.set_command_target(bot, bot))));
            Waiting.NoSleep(clean.set_command_target(bot, dirt));
            Assert.Same(dirt, ((datum_ai_controller)bot.ai_controller).blackboard[BB_CURRENT_PET_TARGET]);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_medbot_walks_to_a_hurt_crewman_tends_his_brute_with_tgs_lines_and_stops_at_the_threshold()
    {
        var r = new AudioRoom(13, 9, seed: 7803);
        mob_living_basic_bot_medbot bot = r.In(() => Dm.@new<mob_living_basic_bot_medbot>(locate(2, 4, 1)));
        mob_living_carbon_human bob = r.Join(7, 4, "Bob");
        r.In(() => Waiting.NoSleep(bob.adjust_brute_loss(30)));
        float before = r.In(() => Dm.num(bob.get_brute_loss()));
        Assert.Equal("medbot_generic_idle", r.In(() => bot.icon_state));
        bool active = false;
        Assert.True(RunUntil(r, 900, () =>
        {
            active |= bot.icon_state == "medbot_generic_active";
            return Dm.num(bob.get_brute_loss()) <= bot.heal_threshold;
        }), $"brute {r.In(() => Dm.num(bob.get_brute_loss()))} of {before}\n" + Tree(r, bot));
        Assert.True(active, "the healing picture");
        Assert.True(r.In(() => get_dist(bot, bob)) <= 1);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_secbot_chases_its_attacker_stuns_and_cuffs_him()
    {
        var r = new AudioRoom(13, 9, seed: 7804);
        mob_living_basic_bot_secbot bot = r.In(() => Dm.@new<mob_living_basic_bot_secbot>(locate(2, 4, 1)));
        mob_living_carbon_human bob = r.Join(3, 4, "Bob");
        r.In(() =>
        {
            bob.set_combat_mode(TRUE);
            Waiting.NoSleep(bot.attack_hand(bob, Dm.list()));
        });
        Assert.True(r.In(() => Dm.@in(bob, ((datum_ai_controller)bot.ai_controller).blackboard[BB_BASIC_MOB_RETALIATE_LIST])));
        Assert.True(RunUntil(r, 300, () => Dm.truthy(bob.IsParalyzed())), Tree(r, bot));
        Assert.Equal(BOT_PREP_ARREST, r.In(() => bot.mode));
        Assert.True(RunUntil(r, 400, () => bob.handcuffed != null), Tree(r, bot));
        Assert.IsType<obj_item_restraints_handcuffs_cable_zipties_used>(r.In(() => bob.handcuffed));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_secbot_with_a_check_on_weapons_and_ids_assesses_a_crewman_without_an_id_as_a_threat()
    {
        var r = new AudioRoom(13, 9, seed: 7805);
        mob_living_basic_bot_secbot_beepsky_armsky bot = r.In(() => Dm.@new<mob_living_basic_bot_secbot_beepsky_armsky>(locate(2, 4, 1)));
        Assert.Equal("Sergeant-At-Armsky", r.In(() => bot.name));
        Assert.True(r.In(() => HAS_TRAIT(bot, TRAIT_COMMISSIONED)));
        float flags = r.In(() => Dm.num(bot.judgement_criteria()));
        Assert.Equal(Dm.bitor(JUDGE_IDCHECK, Dm.bitor(JUDGE_RECORDCHECK, JUDGE_WEAPONCHECK)), flags);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_mule_loads_a_crate_at_one_beacon_and_delivers_it_to_another_then_goes_home()
    {
        var r = new AudioRoom(13, 9, seed: 7806);
        obj_machinery_navbeacon home = Beacon(r, 2, 4, "QM", "delivery;dir=1");
        obj_machinery_navbeacon cargo = Beacon(r, 6, 4, "Cargo", "delivery;dir=1");
        obj_machinery_navbeacon medbay = Beacon(r, 10, 4, "Medbay", "delivery;dir=1");
        obj_structure_closet_crate crate = r.In(() => Dm.@new<obj_structure_closet_crate>(locate(6, 5, 1)));
        mob_living_basic_bot_mulebot mule = r.In(() => Dm.@new<mob_living_basic_bot_mulebot>(locate(2, 4, 1)));
        datum_ai_controller brain = r.In(() => (datum_ai_controller)mule.ai_controller);
        Assert.Equal("QM", r.In(() => brain.blackboard[BB_MULEBOT_HOME_BEACON]));

        // tg's window: pick the destination and go
        r.In(() =>
        {
            mule.bot_access_flags = Dm.bitand(mule.bot_access_flags, Dm.bitnot(BOT_COVER_LOCKED));
            mule.set_destination("Cargo");
            mule.start();
        });
        Assert.True(RunUntil(r, 400, () => mule.load == crate), Tree(r, mule));
        Assert.Same(mule, r.In(() => crate.loc));
        Assert.Contains("closed", r.In(() => crate.opened == 0 ? "closed" : "open"));

        // it heads home with the crate (auto-return), then deliver it by hand to Medbay
        Assert.True(RunUntil(r, 400, () => get_turf(mule) == home.loc), Tree(r, mule));
        r.In(() => Waiting.NoSleep(mule.bot_reset()));
        r.Step(2);
        r.In(() =>
        {
            mule.set_destination("Medbay");
            mule.start();
        });
        Assert.True(RunUntil(r, 500, () => mule.load == null), Tree(r, mule));
        Assert.Equal((10f, 5f), r.In(() => (crate.x, crate.y))); // tg's load_direction: the beacon's dir, north of it
        r.AssertNoRuntimes();
    }

    [Fact]
    public void An_emagged_mule_runs_over_a_man_lying_down_and_a_safe_one_slows_down()
    {
        var r = new AudioRoom(13, 9, seed: 7813);
        mob_living_basic_bot_mulebot mule = r.In(() => Dm.@new<mob_living_basic_bot_mulebot>(locate(2, 4, 1)));
        mob_living_carbon_human bob = r.Dummy(3, 4);
        r.In(() =>
        {
            Waiting.NoSleep(bob.set_body_position(LYING_DOWN));
            mule.density = FALSE; // onto his tile for the test (tg drives over a body with a free path)
            Waiting.NoSleep(mule.Move(locate(3, 4, 1), EAST));
        });
        r.Step(2);
        Assert.Same(bob.loc, r.In(() => mule.loc));
        Assert.True(r.In(() => Dm.truthy(mule.has_status_effect(typeof(datum_status_effect_careful_driving)))));
        Assert.Equal(0f, r.In(() => Dm.num(bob.get_brute_loss())));
        r.In(() =>
        {
            mule.bot_access_flags = Dm.bitor(Dm.bitand(mule.bot_access_flags, Dm.bitnot(BOT_COVER_LOCKED)), BOT_COVER_MAINTS_OPEN);
            Waiting.NoSleep(mule.emag_act(null));
            Waiting.NoSleep(mule.run_over(bob));
        });
        Assert.True(r.In(() => Dm.num(bob.get_brute_loss())) > 0);
        Assert.NotNull(r.In(() => mule.GetComponent(typeof(datum_component_blood_walk))));
        r.In(() => Waiting.NoSleep(mule.Move(locate(4, 4, 1), EAST)));
        r.Step(2);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_patrolling_bot_follows_the_beacons_in_their_order()
    {
        var r = new AudioRoom(13, 9, seed: 7807);
        obj_machinery_navbeacon a = Beacon(r, 2, 2, "A", "patrol;next_patrol=B");
        obj_machinery_navbeacon b = Beacon(r, 10, 2, "B", "patrol;next_patrol=C");
        obj_machinery_navbeacon c = Beacon(r, 10, 6, "C", "patrol;next_patrol=A");
        mob_living_basic_bot_cleanbot_autopatrol bot = r.In(() => Dm.@new<mob_living_basic_bot_cleanbot_autopatrol>(locate(3, 2, 1)));
        var visited = new List<string>();
        Assert.True(RunUntil(r, 1500, () =>
        {
            foreach (obj_machinery_navbeacon beacon in new[] { a, b, c })
            {
                if (get_turf(bot) == beacon.loc && (visited.Count == 0 || visited[^1] != beacon.location))
                    visited.Add(beacon.location);
            }
            return visited.Count >= 4;
        }), string.Join(",", visited) + "\n" + Tree(r, bot));
        // once on the route, the next beacon's order holds
        for (int i = 1; i < visited.Count; i++)
            Assert.Equal(visited[i - 1] switch { "A" => "B", "B" => "C", _ => "A" }, visited[i]);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_firebot_sprays_a_burning_man_and_puts_him_out()
    {
        var r = new AudioRoom(13, 9, seed: 7808);
        mob_living_basic_bot_firebot bot = r.In(() => Dm.@new<mob_living_basic_bot_firebot>(locate(2, 4, 1)));
        mob_living_carbon_human bob = r.Join(6, 4, "Bob");
        r.In(() =>
        {
            Waiting.NoSleep(bob.adjust_fire_stacks(5));
            bob.ignite_mob();
        });
        Assert.True(r.In(() => Dm.truthy(bob.on_fire)));
        // the bot's search and patrol run on seeded random timers: with some seeds it takes most of a minute to turn to him (silicons
        // SI3: one more subsystem in the Master shifted the stream)
        Assert.True(RunUntil(r, 2000, () => !Dm.truthy(bob.on_fire)), Tree(r, bot));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_cleanbot_is_built_from_a_bucket_a_sensor_and_a_robot_arm()
    {
        var r = new AudioRoom(13, 9, seed: 7809);
        mob_living_carbon_human bob = r.Join(5, 4, "Bob");
        obj_item_reagent_containers_cup_bucket bucket = r.In(() => Dm.@new<obj_item_reagent_containers_cup_bucket>(locate(5, 4, 1)));
        obj_item_assembly_prox_sensor sensor = r.In(() => Dm.@new<obj_item_assembly_prox_sensor>(locate(5, 4, 1)));
        obj_item_bodypart_arm_right_robot arm = r.In(() => Dm.@new<obj_item_bodypart_arm_right_robot>(locate(5, 4, 1)));
        r.In(() =>
        {
            Waiting.NoSleep(bob.put_in_active_hand(sensor));
            Waiting.NoSleep(bucket.item_interaction(bob, sensor, Dm.list()));
        });
        r.Step(2);
        obj_item_bot_assembly_cleanbot assembly = r.In(() => Dm.locate<obj_item_bot_assembly_cleanbot>(bob));
        Assert.NotNull(assembly);
        Assert.True(r.In(() => QDELETED(sensor)));
        Assert.Same(bucket, r.In(() => assembly.bucket_obj));
        r.In(() =>
        {
            bob.swap_hand();
            Waiting.NoSleep(bob.put_in_active_hand(arm));
            Waiting.NoSleep(assembly.item_interaction(bob, arm, Dm.list()));
        });
        r.Step(2);
        mob_living_basic_bot_cleanbot bot = r.In(() => Dm.each<mob_living_basic_bot_cleanbot>(locate(5, 4, 1)).FirstOrDefault());
        Assert.NotNull(bot);
        Assert.Equal("Cleanbot", r.In(() => bot.name));
        Assert.Same(bucket, r.In(() => bot.build_bucket));
        Assert.Equal(typeof(obj_item_bodypart_arm_right_robot), r.In(() => bot.robot_arm));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_welder_repairs_a_broken_bot_only_with_its_panel_open()
    {
        var r = new AudioRoom(13, 9, seed: 7810);
        mob_living_carbon_human bob = r.Join(5, 4, "Bob");
        mob_living_basic_bot_cleanbot bot = r.In(() => Dm.@new<mob_living_basic_bot_cleanbot>(locate(6, 4, 1)));
        r.In(() => bot.set_mode_flags(Dm.bitand(bot.bot_mode_flags, Dm.bitnot(BOT_MODE_ON))));
        r.In(() => Waiting.NoSleep(bot.adjust_brute_loss(15)));
        float hurt = r.In(() => Dm.num(bot.health));
        Assert.Equal(10f, hurt);
        obj_item_weldingtool welder = r.In(() => Dm.@new<obj_item_weldingtool>(locate(5, 4, 1)));
        r.In(() =>
        {
            Waiting.NoSleep(bob.put_in_active_hand(welder));
            Waiting.NoSleep(welder.attack_self(bob));
        });
        Assert.True(r.In(() => Dm.truthy(welder.welding)));
        // the panel is closed: no repair; the screwdriver is locked out until an ID opens the cover
        r.In(() => Waiting.NoSleep(bot.welder_act(bob, welder)));
        Assert.Equal(hurt, r.In(() => Dm.num(bot.health)));
        r.In(() => bot.bot_access_flags = Dm.bitor(Dm.bitand(bot.bot_access_flags, Dm.bitnot(BOT_COVER_LOCKED)), BOT_COVER_MAINTS_OPEN));
        r.In(() => Waiting.NoSleep(bot.welder_act(bob, welder)));
        Assert.Equal(hurt + 10, r.In(() => Dm.num(bot.health)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_navbeacon_reads_its_codes_and_a_bot_is_turned_off_and_on_with_tgs_pictures()
    {
        var r = new AudioRoom(13, 9, seed: 7811);
        obj_machinery_navbeacon beacon = Beacon(r, 2, 2, "Bar", "patrol;next_patrol=Kitchen;delivery;dir=4");
        Assert.Equal("Kitchen", r.In(() => beacon.codes[NAVBEACON_PATROL_NEXT]));
        Assert.True(r.In(() => Dm.@in("Bar", GLOB.deliverybeacontags)));
        Assert.True(r.In(() => Dm.@in(beacon, GLOB.navbeacons[Dm.text("[]", beacon.z)])));
        r.In(() => beacon.toggle_code(NAVBEACON_DELIVERY_MODE));
        Assert.False(r.In(() => Dm.@in("Bar", GLOB.deliverybeacontags)));

        mob_living_basic_bot_cleanbot bot = r.In(() => Dm.@new<mob_living_basic_bot_cleanbot>(locate(5, 4, 1)));
        r.In(() => Waiting.NoSleep(bot.turn_off()));
        Assert.Equal("cleanbot0", r.In(() => bot.icon_state));
        Assert.True(r.In(() => HAS_TRAIT(bot, TRAIT_IMMOBILIZED)));
        Assert.Equal(AI_STATUS_OFF, r.In(() => ((datum_ai_controller)bot.ai_controller).ai_status as string));
        r.In(() => bot.turn_on());
        Assert.Equal("cleanbot1", r.In(() => bot.icon_state));
        Assert.False(r.In(() => HAS_TRAIT(bot, TRAIT_IMMOBILIZED)));
        r.AssertNoRuntimes();
    }
}
