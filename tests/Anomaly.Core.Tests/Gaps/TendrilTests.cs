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
/// The necropolis tendrils: a tendril takes root, clears the rock around it and turns the ash about it to necropolis floor; it
/// lashes, sends its spikes after its target, raises its cross of spikes and stabs back at what strikes it, its heartbeat
/// quickening as it is hurt; it dies into its locked chest and the floor goes back to ash. The ice moon's nests spawn their
/// fauna, and the demonic portal collapses into its loot. The tendril's chest opens on a skeleton key.
/// </summary>
public class TendrilTests
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

    // the world's own (range() leaves out the abstract invisible ones, as the spike chasers' trails are)
    private static int Count<T>(AudioRoom r) where T : class =>
        r.In(() => Dm.each<T>().Count(a => a is datum d && !QDELETED(d)));

    /// Lavaland's ash floor over the whole room, the way the cave generator leaves it.
    private static void Ash(AudioRoom r, int width, int height) => r.In(() =>
    {
        for (int x = 1; x <= width; x++)
            for (int y = 1; y <= height; y++)
                Waiting.NoSleep(locate(x, y, 1).ChangeTurf(typeof(turf_open_misc_asteroid_basalt_lava_land_surface)));
    });

    private static T Ability<T>(AudioRoom r, mob_living_basic_mining_tendril tendril) where T : datum_action =>
        r.In(() => Dm.each<T>(tendril.actions).First());

    /// A tendril with its mind off: the test triggers its abilities itself.
    private static mob_living_basic_mining_tendril Mindless(AudioRoom r, int x, int y) => r.In(() =>
    {
        var tendril = Dm.@new<mob_living_basic_mining_tendril>(locate(x, y, 1));
        Waiting.NoSleep((tendril.ai_controller as datum_ai_controller).set_ai_status(AI_STATUS_OFF));
        return tendril;
    });

    [Fact]
    public void A_tendril_takes_root_on_ash_and_dies_into_its_chest_leaving_the_ash_as_it_was()
    {
        var r = new AudioRoom(13, 13, seed: 13401);
        Ash(r, 13, 13);
        // a rock beside the tendril, which its rooting clears
        r.In(() => Waiting.NoSleep(locate(8, 7, 1).ChangeTurf(typeof(turf_closed_mineral_volcanic_lava_land_surface), Dm.list(typeof(turf_open_misc_asteroid_basalt_lava_land_surface)))));
        var tendril = r.In(() => Dm.@new<mob_living_basic_mining_tendril>(locate(7, 7, 1)));
        r.Step(2);
        Assert.False(r.In(() => InertSeam.IsInert(tendril)), "the tendril is inert");
        Assert.True(r.In(() => Dm.@in(tendril, GLOB.tendrils)), "the tendril is on the tendrils' list");
        Assert.NotNull(r.In(() => tendril.GetComponent(typeof(datum_component_gps))));
        r.AssertNoRuntimes();
        Assert.False(r.In(() => ismineralturf(locate(8, 7, 1))), "the rock beside the tendril was cleared");
        int infected = Count<turf_open_indestructible_necropolis>(r);
        Assert.True(infected > 0, "the ash about the tendril turned to necropolis floor");
        Assert.True(r.In(() => tendril.infected_turfs.len) == infected, "the tendril remembers each floor it turned");
        Assert.True(r.In(() => Dm.each<turf>(range(5, tendril)).Where(t => t is turf_open_indestructible_necropolis).All(t => get_dist_euclidean(t, tendril) is var d && Dm.num(d) <= 4.5f)), "only within its reach");

        r.In(() => Waiting.Detach(tendril.adjust_brute_loss(Dm.num(tendril.maxHealth) * 2, forced: TRUE)));
        Assert.True(RunUntil(r, 20, () => QDELETED(tendril)), "the tendril died and went away");
        Assert.True(r.In(() => Dm.each<obj_structure_closet_crate_necropolis_tendril>(locate(7, 7, 1)).Any()), "its chest stands where it stood");
        Assert.False(r.In(() => Dm.@in(tendril, GLOB.tendrils)), "the tendril left the tendrils' list");
        Assert.True(RunUntil(r, 70, () => Count<turf_open_indestructible_necropolis>(r) == 0), "the necropolis floor went back to ash");
        Assert.True(r.In(() => locate(7, 8, 1) is turf_open_misc_asteroid_basalt_lava_land_surface), "the ash is the ash it was");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_floor_changed_since_the_tendril_rooted_stays_as_it_is_when_it_dies()
    {
        var r = new AudioRoom(13, 13, seed: 13402);
        Ash(r, 13, 13);
        var tendril = r.In(() => Dm.@new<mob_living_basic_mining_tendril>(locate(7, 7, 1)));
        r.Step(2);
        turf changed = r.In(() => Dm.each<turf>(range(2, tendril)).First(t => t is turf_open_indestructible_necropolis && t != tendril.loc));
        (float cx, float cy) = r.In(() => (changed.x, changed.y));
        r.In(() => Waiting.NoSleep(changed.ChangeTurf(typeof(turf_open_floor_iron))));
        r.In(() => Waiting.Detach(tendril.adjust_brute_loss(Dm.num(tendril.maxHealth) * 2, forced: TRUE)));
        RunFor(r, 80);
        Assert.True(r.In(() => locate(cx, cy, 1) is turf_open_floor_iron), "the iron floor laid since stays");
        Assert.Equal(0, Count<turf_open_indestructible_necropolis>(r));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_tendril_lashes_in_eight_directions_and_reels_in_what_it_hits()
    {
        var r = new AudioRoom(15, 15, seed: 13403);
        mob_living_carbon_human bob = r.Dummy(11, 8);
        var tendril = Mindless(r, 7, 8);
        r.Step(2);
        var lash = Ability<datum_action_cooldown_mob_cooldown_projectile_attack_tendril_lash>(r, tendril);
        float before = r.In(() => Dm.num(bob.health));
        r.In(() => Waiting.Detach(lash.Trigger(null, null, bob)));
        Assert.True(RunUntil(r, 20, () => Count<obj_effect_temp_visual_telegraphing_line>(r) > 0), "the lash was telegraphed");
        Assert.True(RunUntil(r, 30, () => Count<obj_projectile_tentacle_lash>(r) >= 1), "the tentacles flew");
        Assert.True(RunUntil(r, 60, () => Dm.num(bob.health) < before), "a tentacle hit Bob");
        Assert.True(RunUntil(r, 40, () => get_dist(bob, tendril) <= 1), "Bob was reeled in");
        Assert.True(RunUntil(r, 80, () => lash.next_use_time > world.time), "the lash went on cooldown");
        RunFor(r, 40);
        Assert.Equal(0, Count<obj_projectile_tentacle_lash>(r));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_tendrils_spikes_chase_its_target_through_the_ground()
    {
        var r = new AudioRoom(15, 11, seed: 13404);
        mob_living_carbon_human bob = r.Dummy(11, 5);
        var tendril = Mindless(r, 4, 5);
        r.Step(2);
        var chaser = Ability<datum_action_cooldown_mob_cooldown_tendril_chaser>(r, tendril);
        float before = r.In(() => Dm.num(bob.health));
        r.In(() => Waiting.Detach(chaser.Trigger(null, null, bob)));
        Assert.True(RunUntil(r, 20, () => Count<obj_effect_temp_visual_effect_trail_tendril_chaser>(r) == 1), "a chaser set off");
        Assert.True(RunUntil(r, 80, () => Count<obj_effect_temp_visual_emerging_ground_spike_tendril_chaser>(r) > 0), "spikes rose along its way");
        Assert.True(RunUntil(r, 100, () => Dm.num(bob.health) < before), "the spikes impaled Bob");
        RunFor(r, 120);
        Assert.Equal(0, Count<obj_effect_temp_visual_effect_trail_tendril_chaser>(r));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_hurt_tendril_sends_its_chasers_wide()
    {
        var r = new AudioRoom(15, 11, seed: 13405);
        mob_living_carbon_human bob = r.Dummy(11, 5);
        var tendril = Mindless(r, 4, 5);
        r.Step(2);
        r.In(() => Waiting.NoSleep(tendril.adjust_brute_loss(Dm.num(tendril.maxHealth) * 0.5f, forced: TRUE)));
        var chaser = Ability<datum_action_cooldown_mob_cooldown_tendril_chaser>(r, tendril);
        r.In(() => Waiting.Detach(chaser.Trigger(null, null, bob)));
        Assert.True(RunUntil(r, 20, () => Count<obj_effect_temp_visual_effect_trail_tendril_chaser_wide_area>(r) == 1), "the chaser went wide");
        Assert.True(RunUntil(r, 40, () => Count<obj_effect_temp_visual_emerging_ground_spike_tendril_chaser>(r) > 1), "the wide chaser raised spikes about itself");
        RunFor(r, 120);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_tendrils_cross_spikes_rise_about_it_and_call_its_chasers_back()
    {
        var r = new AudioRoom(15, 15, seed: 13406);
        mob_living_carbon_human bob = r.Dummy(9, 8);
        // three tiles north of the tendril: a spike of the second, turned cross rises there (two tiles east, where Bob stands, is a gap in both)
        mob_living_carbon_human alice = r.Dummy(7, 11);
        var tendril = Mindless(r, 7, 8);
        r.Step(2);
        var chaser = Ability<datum_action_cooldown_mob_cooldown_tendril_chaser>(r, tendril);
        var spikes = Ability<datum_action_cooldown_mob_cooldown_tendril_cross_spikes>(r, tendril);
        r.In(() => Waiting.Detach(chaser.Trigger(null, null, bob)));
        Assert.True(RunUntil(r, 20, () => Count<obj_effect_temp_visual_effect_trail_tendril_chaser>(r) == 1), "a chaser set off");
        // the chaser holds the tendril's other abilities back while it sets off
        RunFor(r, 10);
        float before = r.In(() => Dm.num(alice.health));
        r.In(() => Waiting.Detach(spikes.Trigger(null, null, bob)));
        Assert.True(RunUntil(r, 30, () => Count<obj_effect_temp_visual_telegraphing_circle_short>(r) > 0), "the cross of spikes was telegraphed");
        Assert.True(RunUntil(r, 60, () => Count<obj_effect_temp_visual_emerging_ground_spike_tendril_single>(r) > 0), "the cross of spikes rose");
        Assert.True(RunUntil(r, 120, () => Dm.num(alice.health) < before), "a spike hurt Alice");
        Assert.True(RunUntil(r, 120, () => spikes.next_use_time > world.time), "the spikes went on cooldown");
        Assert.True(r.In(() => chaser.next_use_time <= world.time), "the chaser's cooldown was reset when the spikes finished");
        RunFor(r, 60);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_tendril_stabs_back_at_what_strikes_it()
    {
        var r = new AudioRoom(11, 9, seed: 13407);
        mob_living_carbon_human bob = r.Dummy(6, 4);
        var tendril = Mindless(r, 5, 4);
        r.Step(2);
        float before = r.In(() => Dm.num(bob.health));
        r.In(() => Waiting.Detach(tendril.tendril_melee.Trigger(null, null, bob)));
        bool stabShown = false;
        Assert.True(RunUntil(r, 30, () =>
        {
            stabShown |= Dm.each<atom>(range(2, tendril)).Any(a => a is obj_effect_temp_visual_telegraphing_line_short || a is obj_projectile_tentacle_lash_stab);
            return Dm.num(bob.health) < before;
        }), "the stab hurt Bob");
        Assert.True(stabShown, "the stab was telegraphed and its tentacles flew");
        RunFor(r, 40);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_tendrils_heartbeat_quickens_as_it_is_hurt_in_a_fight()
    {
        var r = new AudioRoom(11, 9, seed: 13408);
        mob_living_carbon_human bob = r.Dummy(9, 4);
        var tendril = Mindless(r, 4, 4);
        r.Step(2);
        Assert.Equal(1.8f * SECONDS, r.In(() => Dm.num(tendril.soundloop.mid_length)));
        r.In(() =>
        {
            (tendril.ai_controller as datum_ai_controller).set_blackboard_key(BB_CURRENT_TARGET, bob);
            tendril.update_heartbeat();
        });
        Assert.Equal(1 * SECONDS, r.In(() => Dm.num(tendril.soundloop.mid_length)));
        r.In(() => Waiting.NoSleep(tendril.adjust_brute_loss(Dm.num(tendril.maxHealth) * 0.75f, forced: TRUE)));
        Assert.True(r.In(() => Dm.num(tendril.soundloop.mid_length)) < 1 * SECONDS, "the heart beat faster");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_tendrils_chest_stays_locked_until_a_skeleton_key_opens_it_onto_its_loot()
    {
        var r = new AudioRoom(11, 9, seed: 13409);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.Step(20);
        var chest = r.In(() => Dm.@new<obj_structure_closet_crate_necropolis_tendril>(locate(5, 4, 1)));
        r.Step(2);
        Assert.Equal(FALSE, r.In(() => Waiting.NoSleep(chest.open(bob))));
        Assert.False(r.In(() => Dm.truthy(chest.opened)), "the chest is locked");
        var key = r.In(() =>
        {
            var k = Dm.@new<obj_item_skeleton_key>(bob.loc);
            Waiting.NoSleep(bob.put_in_active_hand(k));
            return k;
        });
        Assert.Equal(ITEM_INTERACT_SUCCESS, r.In(() => Waiting.NoSleep(chest.item_interaction(bob, key))));
        Assert.True(r.In(() => QDELETED(key)), "the key went into the lock");
        Assert.True(r.In(() => Dm.truthy(chest.spawned_loot)), "the lock gave");
        r.Step(2);
        Assert.True(r.In(() => Dm.truthy(Waiting.NoSleep(chest.open(bob)))), "the chest opens");
        r.Step(2);
        Assert.True(r.In(() => Dm.each<atom>(locate(5, 4, 1)).Any(a => a is obj_item || a is obj_structure_closet_crate_necropolis_puzzle)), "the loot lies in the open chest");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_necropolis_chest_holds_one_piece_of_mining_loot_whichever_the_key_reveals()
    {
        // the loot table, over a run of chests
        var r = new AudioRoom(21, 9, seed: 13410);
        var kinds = new HashSet<string>();
        for (int i = 0; i < 18; i++)
        {
            int x = 2 + i;
            var chest = r.In(() => Dm.@new<obj_structure_closet_crate_necropolis_tendril>(locate(x, 4, 1)));
            r.In(() => Dm.@new(chest.loot_to_spawn, chest));
            r.Step(1);
            var contents = r.In(() => Dm.each<atom_movable>(chest.contents).ToList());
            Assert.NotEmpty(contents);
            foreach (var a in contents)
                kinds.Add(a.GetType().Name);
        }
        Assert.True(kinds.Count > 5, "the chests held different loot: " + string.Join(", ", kinds));
        RunFor(r, 20);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_ice_moons_cave_entrance_clears_its_rock_spawns_its_wolves_and_spills_ore_when_broken()
    {
        var r = new AudioRoom(13, 13, seed: 13411);
        Ash(r, 13, 13);
        r.In(() => Waiting.NoSleep(locate(8, 7, 1).ChangeTurf(typeof(turf_closed_mineral_volcanic_lava_land_surface), Dm.list(typeof(turf_open_misc_asteroid_basalt_lava_land_surface)))));
        var den = r.In(() => Dm.@new<obj_structure_spawner_ice_moon>(locate(7, 7, 1)));
        r.Step(2);
        Assert.False(r.In(() => ismineralturf(locate(8, 7, 1))), "the rock by the den was cleared");
        Assert.True(RunUntil(r, 40 * 10, () => Count<mob_living_basic_mining_wolf>(r) > 0), "a wolf came out of the den");
        r.In(() => Waiting.Detach(den.take_damage(10000)));
        Assert.True(RunUntil(r, 20, () => QDELETED(den)), "the den collapsed");
        Assert.True(Count<obj_item_stack_ore>(r) > 0, "ore fell out of the den");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_demonic_portal_spawns_its_demons_and_collapses_into_its_loot()
    {
        var r = new AudioRoom(13, 13, seed: 13412);
        Ash(r, 13, 13);
        var portal = r.In(() => Dm.@new<obj_structure_spawner_ice_moon_demonic_portal>(locate(7, 7, 1)));
        r.Step(2);
        Assert.NotNull(r.In(() => portal.GetComponent(typeof(datum_component_gps))));
        Assert.True(RunUntil(r, 40 * 10, () => Count<mob_living_basic_mining_ice_demon>(r) > 0), "a demon came through");
        r.In(() =>
        {
            foreach (var demon in Dm.each<mob_living_basic_mining_ice_demon>(range(12, locate(7, 7, 1))).ToList())
                qdel(demon);
        });
        r.In(() => Waiting.Detach(portal.take_damage(10000)));
        Assert.True(RunUntil(r, 20, () => Count<obj_effect_collapsing_demonic_portal>(r) == 1), "the portal began to collapse");
        Assert.True(RunUntil(r, 70, () => Count<obj_effect_collapsing_demonic_portal>(r) == 0), "the portal closed");
        Assert.True(r.In(() => Dm.each<atom>(locate(7, 7, 1)).Any(a => a is obj_structure_closet_crate_necropolis_tendril_demonic
            || a is mob_living_basic_boss_blood_drunk_miner_doom || a is obj_structure_elite_tumor)), "something slipped out of the portal");
        RunFor(r, 20);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_mining_scanner_tags_a_nest_with_its_holotag()
    {
        var r = new AudioRoom(11, 9, seed: 13413);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.Step(20);
        var den = r.In(() => Dm.@new<obj_structure_spawner_ice_moon_polarbear>(locate(5, 4, 1)));
        var scanner = r.In(() =>
        {
            var s = Dm.@new<obj_item_mining_scanner>(bob.loc);
            Waiting.NoSleep(bob.put_in_active_hand(s));
            return s;
        });
        Assert.Equal(ITEM_INTERACT_SUCCESS, r.In(() => Waiting.NoSleep(den.item_interaction(bob, scanner))));
        Assert.True(r.In(() => Dm.truthy(den.gps_tagged)), "the den was tagged");
        Assert.EndsWith("Animal Den", r.In(() => (den.GetComponent(typeof(datum_component_gps)) as datum_component_gps).gpstag as string));
        Assert.StartsWith("[BR-", r.In(() => den.assigned_tag as string));
        r.AssertNoRuntimes();
    }
}
