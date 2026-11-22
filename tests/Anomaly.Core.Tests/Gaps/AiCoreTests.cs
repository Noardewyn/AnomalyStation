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
/// Gaps package C1: the creatures' AI core (code/datums/ai) in a booted toy room: a hostile proteon plans, walks to a target and hits it at tg's cooldown; it sleeps
/// without a client near (tg's gate off the station) and wakes on one; every compiled behaviour tree loads and builds; the targeting strategies and the blackboard.
/// </summary>
public class AiCoreTests
{
    private static mob_living_basic_construct_proteon_hostile Proteon(AudioRoom r, int x, int y) => r.In(() => Dm.@new<mob_living_basic_construct_proteon_hostile>(locate(x, y, 1)));

    private static datum_ai_controller Brain(mob_living_basic mob) => (datum_ai_controller)mob.ai_controller;

    // steps the world until world.time passed that many deciseconds
    private static void RunFor(AudioRoom r, float deciseconds)
    {
        float end = r.In(() => world.time) + deciseconds;
        while (r.In(() => world.time) < end)
            r.Step();
    }

    [Fact]
    public void A_hostile_mob_with_a_controller_plans_walks_to_a_target_and_hits_it_at_tgs_cooldown()
    {
        var r = new AudioRoom(13, 9, seed: 7711);
        mob_living_carbon_human bob = r.Join(9, 4, "Bob");
        var mob = Proteon(r, 2, 4);
        // a toy: the proteon (melee 8 to 10 at 1.5 s) under tg's plain hostile controller, which replaces the one it was born with (PossessPawn)
        datum_ai_controller brain = r.In(() => Dm.@new<datum_ai_controller_basic_controller_simple_simple_hostile>(mob) as datum_ai_controller);
        Assert.Same(brain, r.In(() => mob.ai_controller));
        Assert.Equal(AI_STATUS_ON, r.In(() => brain.ai_status as string)); // Bob's client is in its cells
        Assert.True(r.In(() => brain.movement is datum_ai_movement_basic_avoidance));
        Assert.Equal(typeof(datum_ai_movement_basic_avoidance), r.In(() => Dm.initial(brain, (datum_ai_controller c) => c.ai_movement))); // what the leaves restore after an override

        // it finds Bob (update_combat_targets over hearers) and walks up to him
        float deadline = r.In(() => world.time) + 150;
        while (r.In(() => get_dist(mob, bob)) > 1 && r.In(() => world.time) < deadline)
            r.Step();
        Assert.True(r.In(() => get_dist(mob, bob)) <= 1, r.In(() =>
        {
            DmList lines = Dm.list();
            ((datum_bt_node)brain.behavior_nodes[1]).append_full_tree_state(lines, "");
            return $"the mob stands at {mob.x},{mob.y}, status {brain.ai_status}, target {brain.blackboard[BB_CURRENT_TARGET]}, tick {world.time}\n" + lines.Join("\n") + "\n" + string.Join("\n", r.Room.RuntimeText);
        }));
        Assert.Same(bob, r.In(() => brain.blackboard[BB_CURRENT_TARGET]));

        // and hits him at its melee_attack_cooldown (1.5 s), with tg's first-swing delay
        float before = r.In(() => Dm.num(bob.get_brute_loss()));
        float start = r.In(() => world.time);
        RunFor(r, 150);
        float damage = r.In(() => Dm.num(bob.get_brute_loss())) - before;
        // in 15 s at most 10 hits of at most 10, and it keeps swinging
        Assert.InRange(damage, 8 * 5, 10 * 11);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_copy_mimic_comes_alive_from_an_object_and_hunts_a_crewman()
    {
        var r = new AudioRoom(13, 9, seed: 7718);
        mob_living_carbon_human bob = r.Join(9, 4, "Bob");
        var toolbox = r.In(() => Dm.@new<obj_item_storage_toolbox>(locate(2, 4, 1)));
        var mimic = r.In(() => Dm.@new<mob_living_basic_mimic_copy_machine>(locate(2, 4, 1), toolbox));
        Assert.Equal(toolbox.name, r.In(() => mimic.name));
        Assert.Same(mimic, r.In(() => toolbox.loc));
        Assert.Equal(15 * r.In(() => Dm.num(toolbox.w_class)), r.In(() => mimic.maxHealth));
        Assert.True(r.In(() => mimic.ai_controller is datum_ai_controller_basic_controller_mimic_copy_machine));
        float deadline = r.In(() => world.time) + 300;
        while (r.In(() => Dm.num(bob.get_brute_loss())) == 0 && r.In(() => world.time) < deadline)
            r.Step();
        Assert.True(r.In(() => Dm.num(bob.get_brute_loss())) > 0, "the mimic reached and hit Bob");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_hostile_proteon_ignores_a_crewman_until_he_hits_it_then_retaliates()
    {
        var r = new AudioRoom(13, 9, seed: 7719);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        var mob = Proteon(r, 3, 4);
        datum_ai_controller brain = Brain(mob);
        RunFor(r, 30);
        Assert.Null(r.In(() => brain.blackboard[BB_CURRENT_TARGET])); // tg's skittish brawler: no aggro on sight
        float health = r.In(() => mob.health);
        r.In(() =>
        {
            bob.set_combat_mode(TRUE);
            Waiting.NoSleep(mob.attack_hand(bob, Dm.list()));
        });
        Assert.True(r.In(() => mob.health) < health);
        Assert.True(r.In(() => Dm.@in(bob, brain.blackboard[BB_BASIC_MOB_RETALIATE_LIST])));
        float deadline = r.In(() => world.time) + 25; // the retaliate search runs every 2 s
        while (r.In(() => brain.blackboard[BB_CURRENT_TARGET]) == null && r.In(() => world.time) < deadline)
            r.Step();
        Assert.Same(bob, r.In(() => brain.blackboard[BB_CURRENT_TARGET]));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void Off_the_station_the_ai_sleeps_without_a_client_near_and_wakes_on_one()
    {
        var r = new AudioRoom(13, 9, seed: 7712);
        var mob = Proteon(r, 2, 4);
        datum_ai_controller brain = Brain(mob);
        RunFor(r, 10);
        // the toy room's area is not /area/station: tg's get_active_ai_status sleeps it
        Assert.Equal(AI_STATUS_OFF, r.In(() => brain.ai_status as string));
        turf start = r.In(() => get_turf(mob));
        RunFor(r, 50);
        Assert.Same(start, r.In(() => get_turf(mob)));

        // a living client entering its cells wakes it (the spatial grid's CLIENTS signal)
        mob_living_carbon_human bob = r.Join(5, 4, "Bob");
        RunFor(r, 10);
        Assert.Equal(AI_STATUS_ON, r.In(() => brain.ai_status as string));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void Every_compiled_behaviour_tree_loads_and_builds_with_known_nodes_and_vars()
    {
        var r = new AudioRoom(5, 5, seed: 7713);
        var names = BehaviorTreeProbe.Names();
        Assert.Equal(343, names.Count);
        r.In(() =>
        {
            var failures = new List<string>();
            foreach (string path in names)
            {
                // tg's own bug: mimic_animator.bt.json sets a "key" var its basic_ranged_attack leaf does not have (a runtime in tg too; the gun mimics stay inert)
                if (path.Contains("mimic_animator"))
                    continue;
                try
                {
                    var controller = Dm.@new<datum_ai_controller>();
                    // a subtree's file carries its bindings; resolve_node_children applies them, as tg's
                    DmList desc = Dm.json_decode(BehaviorTreeFiles.Read(path)) as DmList;
                    if (desc["__bindings"] != null)
                        desc = controller.apply_bindings_to_descriptor(desc, null) as DmList;
                    var root = controller.build_node_from_descriptor(desc) as datum_bt_node;
                    Assert.True(root != null, path);
                    controller.behavior_nodes = Dm.list(root);
                    controller.finalize_tree();
                    qdel(controller);
                }
                catch (Exception e)
                {
                    failures.Add(path + ": " + e.Message);
                }
            }
            Assert.True(failures.Count == 0, string.Join("\n", failures));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_basic_targeting_strategy_rejects_friends_the_dead_the_unseen_and_turfs()
    {
        var r = new AudioRoom(13, 9, seed: 7714);
        mob_living_carbon_human bob = r.Join(5, 4, "Bob");
        var mob = Proteon(r, 2, 4);
        r.In(() =>
        {
            var basic = GET_TARGETING_STRATEGY(typeof(datum_targeting_strategy_basic));
            bool Valid(atom a, object range = null) => Dm.truthy(Waiting.NoSleep(basic.is_valid_target(mob, a, range ?? 9, Brain(mob))));
            Assert.True(Valid(bob));
            Assert.False(Valid(get_turf(bob)));
            Assert.False(Valid(bob, 2)); // out of range
            // a shared faction is a friend (the proteon's cult faction)
            bob.add_faction(FACTION_CULT);
            Assert.False(Valid(bob));
            bob.remove_faction(FACTION_CULT);
            Assert.True(Valid(bob));
            // past BB_TARGET_MINIMUM_STAT (the proteon's HARD_CRIT)
            Waiting.NoSleep(bob.death());
            Assert.False(Valid(bob));
            // "anything" only cares that it exists and the distance
            var anything = GET_TARGETING_STRATEGY(typeof(datum_targeting_strategy_anything));
            Assert.True(Dm.truthy(Waiting.NoSleep(anything.is_valid_target(mob, bob, 9))));
            // the nearest priority strategy picks the closest of a list
            var nearest = GET_TARGET_PRIORITY_STRATEGY(typeof(datum_target_priority_strategy_nearest));
            atom near = locate(3, 4, 1), far = locate(9, 4, 1);
            Assert.Same(near, nearest.select_target(Brain(mob), Dm.list(far, near)));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_deleted_target_clears_out_of_the_blackboard_and_keys_signal_their_observers()
    {
        var r = new AudioRoom(13, 9, seed: 7715);
        var mob = Proteon(r, 2, 4);
        datum_ai_controller brain = Brain(mob);
        r.In(() =>
        {
            mob_living_carbon_human dummy = Dm.@new<mob_living_carbon_human>(locate(5, 4, 1));
            brain.set_blackboard_key(BB_CURRENT_TARGET, dummy);
            brain.set_blackboard_key_assoc_lazylist(BB_BASIC_MOB_RETALIATE_LIST, dummy, 1);
            Assert.Same(dummy, brain.blackboard[BB_CURRENT_TARGET]);
            qdel(dummy);
            Assert.Null(brain.blackboard[BB_CURRENT_TARGET]);
            Assert.Equal(0, length(brain.blackboard[BB_BASIC_MOB_RETALIATE_LIST]));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void Jump_point_search_finds_a_path_around_a_wall_and_the_smart_movement_walks_it()
    {
        var r = new AudioRoom(13, 9, seed: 7716);
        var mob = Proteon(r, 2, 4);
        r.In(() =>
        {
            for (int y = 1; y <= 7; y++)
                Waiting.NoSleep(locate(6, y, 1).ChangeTurf(typeof(turf_closed_wall)));
        });
        r.Step();
        DmList path = null;
        r.In(() => Waiting.Detach(Find()));
        async DmTask Find() => path = await get_path_to(mob, r.In(() => locate(9, 4, 1) as atom)) as DmList;
        RunFor(r, 10);
        Assert.NotNull(path);
        Assert.True(length(path) > 0, "a path exists over the wall's top");
        r.In(() => Assert.DoesNotContain(Dm.each_cast<turf>(path), t => t.x == 6 && t.y <= 7));

        // the smart movement datum walks that path: SSai_movement's has_target/jps loop over SSpathfinder (the tree is off, so only the loop moves it)
        datum_ai_controller brain = Brain(mob);
        r.In(() =>
        {
            Waiting.NoSleep(brain.force_ai_off());
            brain.change_ai_movement_type(typeof(datum_ai_movement_jps));
            brain.movement.start_moving_towards(brain, locate(9, 4, 1), 0);
        });
        float deadline = r.In(() => world.time) + 150;
        while (r.In(() => get_turf(mob) != locate(9, 4, 1)) && r.In(() => world.time) < deadline)
            r.Step();
        Assert.True(r.In(() => get_turf(mob) == locate(9, 4, 1)), r.In(() => $"the mob stands at {mob.x},{mob.y}"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_telegraph_is_a_temporary_visual_with_tgs_picture_and_lifetime()
    {
        var r = new AudioRoom(5, 5, seed: 7717);
        var box = r.In(() => Dm.@new<obj_effect_temp_visual_telegraphing_circle>(locate(2, 2, 1)));
        Assert.Equal("target_circle", r.In(() => box.icon_state));
        RunFor(r, 10);
        Assert.False(r.In(() => QDELETED(box)));
        RunFor(r, 12);
        Assert.True(r.In(() => QDELETED(box)));
        r.AssertNoRuntimes();
    }
}

internal static class BehaviorTreeProbe
{
    internal static List<string> Names() => typeof(datum_ai_controller).Assembly.GetManifestResourceNames().Where(n => n.StartsWith("build/behavior_trees/", StringComparison.Ordinal)).ToList();
}
