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
/// Lavaland's and the ice moon's megafauna: the ash drake, Bubblegum, the colossus, the hierophant, the legion, the demonic
/// frost miner, the wendigo, the clockwork defender, the blood-drunk miners and the Thing. Each spawns, fights and goes away
/// cleanly; each one's attacks do what tg's do; each dies into its loot, and into its crusher trophy when a crusher did the work.
/// </summary>
public class MegafaunaTests
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

    private static bool Near<T>(AudioRoom r, int dist, Func<atom> center) where T : class =>
        r.In(() => Dm.each<atom>(range(dist, center())).Any(a => a is T));

    private static int Count<T>(AudioRoom r) where T : class =>
        r.In(() => Dm.each<atom>(range(30, locate(1, 1, 1))).Count(a => a is T));

    /// Hurts a mob past its health, the crusher's share of the damage set first when it is to count.
    private static void Kill(AudioRoom r, mob_living mob, bool byCrusher)
    {
        r.In(() =>
        {
            if (byCrusher)
            {
                Waiting.NoSleep(mob.apply_status_effect(typeof(datum_status_effect_crusher_damage)));
                var share = mob.has_status_effect(typeof(datum_status_effect_crusher_damage)) as datum_status_effect_crusher_damage;
                share.total_damage = Dm.num(mob.maxHealth);
            }
            Waiting.Detach(mob.adjust_brute_loss(Dm.num(mob.maxHealth) * 2, forced: TRUE));
        });
    }

    public static IEnumerable<object[]> Megafauna() => new[]
    {
        typeof(mob_living_simple_animal_hostile_megafauna_dragon), typeof(mob_living_simple_animal_hostile_megafauna_dragon_lesser),
        typeof(mob_living_simple_animal_hostile_megafauna_bubblegum), typeof(mob_living_simple_animal_hostile_megafauna_colossus),
        typeof(mob_living_simple_animal_hostile_megafauna_hierophant), typeof(mob_living_simple_animal_hostile_megafauna_legion),
        typeof(mob_living_simple_animal_hostile_megafauna_legion_medium), typeof(mob_living_simple_animal_hostile_megafauna_legion_small),
        typeof(mob_living_simple_animal_hostile_megafauna_demonic_frost_miner), typeof(mob_living_simple_animal_hostile_megafauna_wendigo),
        typeof(mob_living_simple_animal_hostile_megafauna_wendigo_noportal), typeof(mob_living_simple_animal_hostile_megafauna_clockwork_defender),
        typeof(mob_living_basic_boss_blood_drunk_miner), typeof(mob_living_basic_boss_blood_drunk_miner_guidance),
        typeof(mob_living_basic_boss_blood_drunk_miner_hunter), typeof(mob_living_basic_boss_blood_drunk_miner_doom),
        typeof(mob_living_basic_boss_thing), typeof(mob_living_basic_boss_thing_with_ruin_loot),
    }.Select(t => new object[] { t });

    [Theory]
    [MemberData(nameof(Megafauna))]
    public void A_megafauna_spawns_fights_and_goes_away_cleanly(Type path)
    {
        var r = new AudioRoom(17, 13, seed: 13301);
        mob_living_carbon_human bob = r.Dummy(13, 6);
        var mob = (mob_living)r.In(() => Dm.@new(path, locate(4, 6, 1)));
        Assert.False(r.In(() => InertSeam.IsInert(mob)), path.Name + " is inert");
        RunFor(r, 150);
        if (!r.In(() => QDELETED(bob)))
            r.In(() => qdel(bob));
        RunFor(r, 100);
        if (!r.In(() => QDELETED(mob)))
            r.In(() => qdel(mob));
        RunFor(r, 60);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_megafauna_hunts_a_man_down_and_devours_his_body()
    {
        var r = new AudioRoom(13, 9, seed: 13302);
        mob_living_carbon_human bob = r.Dummy(7, 4);
        var drake = r.In(() => Dm.@new<mob_living_simple_animal_hostile_megafauna_dragon>(locate(4, 4, 1)));
        // its flames and swoops would make the hunt a matter of luck: the bite alone
        r.In(() => drake.ranged = FALSE);
        Assert.True(RunUntil(r, 60 * 10, () => bob.stat == DEAD), "the drake killed Bob");
        Assert.True(RunUntil(r, 30 * 10, () => bob.has_status_effect(typeof(datum_status_effect_gutted)) != null || QDELETED(bob)), "the drake devoured Bob");
        r.AssertNoRuntimes();
    }

    // ---- the ash drake ----

    [Fact]
    public void The_drakes_fire_breath_burns_along_its_cone()
    {
        var r = new AudioRoom(13, 9, seed: 13303);
        mob_living_carbon_human bob = r.Dummy(8, 4);
        var drake = r.In(() => Dm.@new<mob_living_simple_animal_hostile_megafauna_dragon>(locate(4, 4, 1)));
        r.In(() => drake.ranged = FALSE);
        r.Step(2);
        float before = r.In(() => Dm.num(bob.health));
        r.In(() => Waiting.Detach(drake.fire_cone.Trigger(null, null, bob)));
        Assert.True(RunUntil(r, 60, () => Dm.num(bob.health) < before), "the flames burnt Bob");
        Assert.True(Near<obj_effect_hotspot>(r, 6, () => drake) || r.In(() => Dm.num(bob.fire_stacks) > 0 || Dm.truthy(bob.on_fire)), "the flames set the floor or Bob alight");
        RunFor(r, 60);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_drake_swoops_lands_on_its_target_and_rains_meteors()
    {
        var r = new AudioRoom(15, 11, seed: 13304);
        mob_living_carbon_human bob = r.Dummy(10, 5);
        var drake = r.In(() => Dm.@new<mob_living_simple_animal_hostile_megafauna_dragon>(locate(4, 5, 1)));
        r.In(() => drake.ranged = FALSE);
        r.Step(2);
        r.In(() => Waiting.Detach(drake.lava_swoop.Trigger(null, null, bob)));
        Assert.True(RunUntil(r, 40, () => drake.swooping != 0), "the drake took off");
        Assert.True(RunUntil(r, 120, () => drake.swooping == 0), "the drake landed");
        Assert.True(r.In(() => get_dist(drake, bob)) <= 3, "the drake landed by Bob");
        r.In(() => drake.meteors.next_use_time = 0);
        r.In(() => Waiting.Detach(drake.meteors.Trigger(null, null, bob)));
        Assert.True(RunUntil(r, 40, () => Dm.each<atom>(range(8, bob)).Any(a => a is obj_effect_temp_visual_target || a is obj_effect_temp_visual_fireball)), "meteors fell around Bob");
        RunFor(r, 80);
        r.AssertNoRuntimes();
    }

    // ---- Bubblegum ----

    [Fact]
    public void Bubblegum_charges_thrice_and_its_hallucinations_charge_with_it()
    {
        var r = new AudioRoom(17, 11, seed: 13305);
        mob_living_carbon_human bob = r.Dummy(12, 5);
        var bubblegum = r.In(() => Dm.@new<mob_living_simple_animal_hostile_megafauna_bubblegum>(locate(4, 5, 1)));
        r.Step(2);
        r.In(() => Waiting.Detach(bubblegum.triple_charge.Trigger(null, null, bob)));
        Assert.True(RunUntil(r, 60, () => get_dist(bubblegum, bob) <= 2), "Bubblegum charged at Bob");
        RunFor(r, 60);
        r.In(() => Waiting.NoSleep(bubblegum.forceMove(locate(4, 5, 1))));
        r.In(() => bubblegum.hallucination_charge.next_use_time = 0);
        r.In(() => Waiting.Detach(bubblegum.hallucination_charge.Trigger(null, null, bob)));
        Assert.True(RunUntil(r, 30, () => Count<mob_living_simple_animal_hostile_megafauna_bubblegum_hallucination>(r) > 0), "hallucinations came");
        Assert.True(RunUntil(r, 120, () => Count<mob_living_simple_animal_hostile_megafauna_bubblegum_hallucination>(r) == 0), "the hallucinations vanished");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void Bubblegum_warps_through_blood_and_its_hands_rise_from_it()
    {
        var r = new AudioRoom(15, 11, seed: 13306);
        mob_living_carbon_human bob = r.Dummy(11, 5);
        var bubblegum = r.In(() => Dm.@new<mob_living_simple_animal_hostile_megafauna_bubblegum>(locate(3, 5, 1)));
        // a pool under Bubblegum and one at the rim of the warp's pick range around Bob (the inner pools are left out)
        r.In(() =>
        {
            Dm.@new<obj_effect_decal_cleanable_blood>(locate(3, 5, 1));
            Dm.@new<obj_effect_decal_cleanable_blood>(locate(6, 5, 1));
        });
        r.Step(2);
        r.In(() => Waiting.Detach(bubblegum.blood_warp.Trigger(null, null, bob)));
        Assert.True(RunUntil(r, 40, () => bubblegum.loc == locate(6, 5, 1)), "Bubblegum came up through the blood by Bob");
        // Bob stands in blood, so the hands come for him
        r.In(() => Dm.@new<obj_effect_decal_cleanable_blood>(bob.loc));
        float before = r.In(() => Dm.num(bob.health));
        r.In(() => Waiting.Detach(bubblegum.bloodattack(Dm.list(bob), 0)));
        Assert.True(RunUntil(r, 40, () => Dm.num(bob.health) < before || Near<obj_effect_temp_visual_bubblegum_hands>(r, 2, () => bob)), "a hand rose at Bob");
        RunFor(r, 40);
        r.AssertNoRuntimes();
    }

    // ---- the colossus ----

    [Fact]
    public void The_colossus_fires_its_bolts_and_they_hurt()
    {
        var r = new AudioRoom(15, 11, seed: 13307);
        mob_living_carbon_human bob = r.Dummy(10, 5);
        var colossus = r.In(() => Dm.@new<mob_living_simple_animal_hostile_megafauna_colossus>(locate(4, 5, 1)));
        r.Step(2);
        float before = r.In(() => Dm.num(bob.health));
        r.In(() => Waiting.Detach(colossus.shotgun_blast.Trigger(null, null, bob)));
        Assert.True(RunUntil(r, 20, () => Count<obj_projectile_colossus>(r) > 0), "the bolts flew");
        Assert.True(RunUntil(r, 60, () => Dm.num(bob.health) < before), "a bolt hit Bob");
        r.In(() => colossus.spiral_shots.next_use_time = 0);
        r.In(() => Waiting.Detach(colossus.spiral_shots.Trigger(null, null, bob)));
        Assert.True(RunUntil(r, 40, () => Count<obj_projectile_colossus>(r) > 3), "the spiral fired");
        RunFor(r, 100);
        // two bolts on one wall in a tick: the first blast brings it down, the second strikes what stands there now
        turf wall = r.In(() => (turf)Waiting.NoSleep(locate(13, 9, 1).ChangeTurf(typeof(turf_closed_wall))));
        r.In(() =>
        {
            for (int i = 0; i < 2; i++)
            {
                var bolt = Dm.@new<obj_projectile_colossus>(locate(12, 9, 1));
                bolt.damage = 0; // an unfired bolt: no wall dent (the base on_hit's dent needs a flight line), only the blast
                Waiting.NoSleep(bolt.on_hit(wall));
                qdel(bolt);
            }
        });
        r.Step(4);
        Assert.False(r.In(() => locate(13, 9, 1) is turf_closed_wall), "the bolts did not blast the wall");
        r.AssertNoRuntimes();
    }

    // ---- the hierophant ----

    [Fact]
    public void The_hierophants_blasts_and_chasers_hurt_what_they_cross()
    {
        var r = new AudioRoom(17, 13, seed: 13308);
        mob_living_carbon_human bob = r.Dummy(11, 6);
        var hiero = r.In(() => Dm.@new<mob_living_simple_animal_hostile_megafauna_hierophant>(locate(4, 6, 1)));
        r.Step(2);
        r.In(() => hiero.GiveTarget(bob));
        float before = r.In(() => Dm.num(bob.health));
        r.In(() => Waiting.Detach(hiero.blasts(bob)));
        Assert.True(RunUntil(r, 20, () => Count<obj_effect_temp_visual_hierophant_blast_damaging>(r) > 0), "the cross blasts rose");
        Assert.True(RunUntil(r, 40, () => Dm.num(bob.health) < before), "a blast hit Bob");
        RunFor(r, 20);
        r.In(() => Dm.@new<obj_effect_temp_visual_hierophant_chaser>(hiero.loc, hiero, bob, hiero.chaser_speed, FALSE));
        Assert.True(RunUntil(r, 20, () => Count<obj_effect_temp_visual_hierophant_chaser>(r) > 0), "a chaser set out");
        RunFor(r, 100);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_hierophant_traps_its_target_in_an_arena_and_blinks_to_it()
    {
        var r = new AudioRoom(19, 15, seed: 13309);
        mob_living_carbon_human bob = r.Dummy(12, 7);
        var hiero = r.In(() => Dm.@new<mob_living_simple_animal_hostile_megafauna_hierophant>(locate(4, 7, 1)));
        r.Step(2);
        r.In(() => hiero.GiveTarget(bob));
        r.In(() => hiero.arena_trap(bob));
        Assert.True(RunUntil(r, 20, () => Count<obj_effect_temp_visual_hierophant_wall>(r) > 8), "the arena's walls rose");
        RunFor(r, 40);
        r.In(() => Waiting.Detach(hiero.blink(bob)));
        Assert.True(RunUntil(r, 40, () => get_dist(hiero, bob) <= 2), "the hierophant blinked to Bob");
        RunFor(r, 140);
        Assert.Equal(0, Count<obj_effect_temp_visual_hierophant_wall>(r));
        r.AssertNoRuntimes();
    }

    // ---- the legion ----

    [Fact]
    public void The_legion_sends_its_skulls_and_its_sentinels_and_splits_in_three_when_it_dies()
    {
        var r = new AudioRoom(17, 13, seed: 13310);
        mob_living_carbon_human bob = r.Dummy(12, 6);
        var legion = r.In(() => Dm.@new<mob_living_simple_animal_hostile_megafauna_legion>(locate(4, 6, 1)));
        r.Step(2);
        r.In(() => Waiting.Detach(legion.create_legion_skull.Trigger(null, null, bob)));
        Assert.True(RunUntil(r, 20, () => Count<mob_living_basic_mining_legion_brood>(r) > 0), "a skull flew");
        r.In(() => legion.create_legion_turrets.next_use_time = 0);
        r.In(() => Waiting.Detach(legion.create_legion_turrets.Trigger(null, null, bob)));
        Assert.True(RunUntil(r, 20, () => Count<obj_structure_legionturret>(r) > 0), "the sentinels rose");
        Kill(r, legion, byCrusher: false);
        Assert.True(RunUntil(r, 20, () => QDELETED(legion)), "the legion fell apart");
        Assert.True(RunUntil(r, 20, () => Count<mob_living_simple_animal_hostile_megafauna_legion_medium>(r) == 3), "into three legions");
        RunFor(r, 40);
        r.AssertNoRuntimes();
    }

    // ---- the demonic frost miner ----

    [Fact]
    public void The_frost_miner_fires_frost_orbs_and_rages_when_hurt()
    {
        var r = new AudioRoom(15, 11, seed: 13311);
        mob_living_carbon_human bob = r.Dummy(10, 5);
        var miner = r.In(() => Dm.@new<mob_living_simple_animal_hostile_megafauna_demonic_frost_miner>(locate(4, 5, 1)));
        r.Step(2);
        float before = r.In(() => Dm.num(bob.health));
        r.In(() => Waiting.Detach(miner.ice_shotgun.Trigger(null, null, bob)));
        Assert.True(RunUntil(r, 60, () => Dm.num(bob.health) < before), "the ice hit Bob");
        r.In(() => Waiting.Detach(miner.adjust_brute_loss(Dm.num(miner.maxHealth) * 0.8f)));
        r.In(() => Waiting.Detach(miner.check_enraged()));
        Assert.True(RunUntil(r, 80, () => miner.enraged != 0), "the frost miner raged");
        RunFor(r, 60);
        r.AssertNoRuntimes();
    }

    // ---- the wendigo ----

    [Fact]
    public void The_wendigo_slams_the_ground_and_throws_what_stands_by_it()
    {
        var r = new AudioRoom(15, 11, seed: 13312);
        mob_living_carbon_human bob = r.Dummy(6, 5);
        var wendigo = r.In(() => Dm.@new<mob_living_simple_animal_hostile_megafauna_wendigo>(locate(4, 5, 1)));
        r.Step(2);
        float before = r.In(() => Dm.num(bob.health));
        r.In(() => Waiting.Detach(wendigo.ground_slam.Trigger(null, null, bob)));
        Assert.True(RunUntil(r, 60, () => Dm.num(bob.health) < before), "the slam hurt Bob");
        Assert.True(RunUntil(r, 40, () => get_dist(wendigo, bob) > 2), "the slam threw Bob");
        RunFor(r, 40);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_dead_wendigo_is_butchered_for_its_blood_and_opens_a_portal_home()
    {
        var r = new AudioRoom(15, 11, seed: 13313);
        var wendigo = r.In(() => Dm.@new<mob_living_simple_animal_hostile_megafauna_wendigo>(locate(6, 5, 1)));
        r.Step(2);
        Kill(r, wendigo, byCrusher: true);
        Assert.True(RunUntil(r, 40, () => wendigo.stat == DEAD), "the wendigo died");
        RunFor(r, 10);
        Assert.False(r.In(() => QDELETED(wendigo)), "the body stays");
        Assert.Equal(1f, r.In(() => Dm.num(wendigo.guaranteed_butcher_results[typeof(obj_item_wendigo_blood)])));
        Assert.Equal(1f, r.In(() => Dm.num(wendigo.guaranteed_butcher_results[typeof(obj_item_crusher_trophy_wendigo_horn)])));
        Assert.True(Near<obj_effect_portal_permanent_one_way>(r, 3, () => locate(6, 5, 1)), "a portal home opened");
        r.AssertNoRuntimes();
    }

    // ---- the blood-drunk miner ----

    [Fact]
    public void The_blood_drunk_miner_dashes_and_fires_its_accelerator()
    {
        var r = new AudioRoom(15, 11, seed: 13314);
        mob_living_carbon_human bob = r.Dummy(10, 5);
        var miner = r.In(() => Dm.@new<mob_living_basic_boss_blood_drunk_miner>(locate(4, 5, 1)));
        r.Step(2);
        var dash = r.In(() => Dm.each<datum_action_cooldown_mob_cooldown_dash_attack>(miner.actions).First());
        r.In(() => Waiting.Detach(dash.Trigger(null, null, bob)));
        Assert.True(RunUntil(r, 40, () => get_dist(miner, bob) <= 3), "the miner dashed at Bob");
        var shot = r.In(() => Dm.each<datum_action_cooldown_mob_cooldown_projectile_attack_rapid_fire_kinetic_accelerator>(miner.actions).First());
        // the miner's blasts carry four tiles
        r.In(() => Waiting.NoSleep(miner.forceMove(locate(7, 5, 1))));
        r.In(() => shot.next_use_time = 0);
        float before = r.In(() => Dm.num(bob.health));
        r.In(() => Waiting.Detach(shot.Trigger(null, null, bob)));
        Assert.True(RunUntil(r, 60, () => Dm.num(bob.health) < before), "a kinetic blast hit Bob");
        RunFor(r, 60);
        r.AssertNoRuntimes();
    }

    // ---- the Thing ----

    [Fact]
    public void The_thing_loses_a_third_of_its_health_per_phase_and_dies_after_the_third()
    {
        var r = new AudioRoom(15, 11, seed: 13315);
        var thing = r.In(() => Dm.@new<mob_living_basic_boss_thing>(locate(7, 5, 1)));
        r.Step(2);
        Assert.Equal(1f, r.In(() => thing.phase));
        float third = r.In(() => Dm.num(thing.maxHealth)) / 3;
        r.In(() => Waiting.Detach(thing.adjust_brute_loss(third * 0.5f)));
        RunFor(r, 5);
        Assert.Equal(1f, r.In(() => thing.phase));
        r.In(() => Waiting.Detach(thing.adjust_brute_loss(third)));
        Assert.True(RunUntil(r, 20, () => thing.phase == 2), "the first phase ended");
        Assert.Equal("p2", r.In(() => thing.icon_state));
        r.In(() => Waiting.Detach(thing.adjust_brute_loss(third)));
        Assert.True(RunUntil(r, 20, () => thing.phase == 3), "the second phase ended");
        r.In(() => Waiting.Detach(thing.adjust_brute_loss(third)));
        Assert.True(RunUntil(r, 40, () => thing.stat == DEAD), "the Thing died");
        Assert.True(RunUntil(r, 20, () => Near<obj_item_keycard_thing_boss>(r, 2, () => locate(7, 5, 1))), "its keycard dropped");
        r.AssertNoRuntimes();
    }

    // ---- the deaths, the loot and the trophies ----

    public static IEnumerable<object[]> Deaths() => new[]
    {
        new object[] { typeof(mob_living_simple_animal_hostile_megafauna_dragon), typeof(obj_structure_closet_crate_necropolis_dragon), typeof(obj_structure_closet_crate_necropolis_dragon_crusher) },
        new object[] { typeof(mob_living_simple_animal_hostile_megafauna_bubblegum), typeof(obj_structure_closet_crate_necropolis_bubblegum), typeof(obj_structure_closet_crate_necropolis_bubblegum_crusher) },
        new object[] { typeof(mob_living_simple_animal_hostile_megafauna_colossus), typeof(obj_structure_closet_crate_necropolis_colossus), typeof(obj_structure_closet_crate_necropolis_colossus_crusher) },
        new object[] { typeof(mob_living_simple_animal_hostile_megafauna_hierophant), typeof(obj_item_hierophant_club), typeof(obj_item_crusher_trophy_vortex_talisman) },
        new object[] { typeof(mob_living_simple_animal_hostile_megafauna_demonic_frost_miner), typeof(obj_item_ice_energy_crystal), typeof(obj_item_crusher_trophy_ice_block_talisman) },
        new object[] { typeof(mob_living_simple_animal_hostile_megafauna_clockwork_defender), typeof(obj_item_clockwork_alloy), null },
        new object[] { typeof(mob_living_basic_boss_blood_drunk_miner), typeof(obj_item_melee_cleaving_saw), typeof(obj_item_crusher_trophy_miner_eye) },
        new object[] { typeof(mob_living_basic_boss_thing_with_ruin_loot), typeof(obj_item_organ_brain_cybernetic_ai), typeof(obj_item_crusher_trophy_flesh_glob) },
    };

    [Theory]
    [MemberData(nameof(Deaths))]
    public void A_megafauna_dies_into_its_loot_and_a_crushers_kill_earns_its_trophy(Type path, Type loot, Type trophy)
    {
        foreach (bool byCrusher in new[] { false, true })
        {
            var r = new AudioRoom(15, 11, seed: byCrusher ? 13317UL : 13316UL);
            var mob = (mob_living)r.In(() => Dm.@new(path, locate(7, 5, 1)));
            r.Step(2);
            Kill(r, mob, byCrusher);
            Assert.True(RunUntil(r, 60, () => mob.stat == DEAD || QDELETED(mob)), path.Name + " died");
            RunFor(r, 20);
            bool Found(Type t) => r.In(() => Dm.each<atom>(range(3, locate(7, 5, 1))).Any(a => t.IsInstanceOfType(a)));
            // a body that stays gives its trophy to the butcher
            bool Butchered(Type t) => r.In(() => !QDELETED(mob) && mob.guaranteed_butcher_results != null && Dm.truthy(mob.guaranteed_butcher_results[t]));
            // a crusher's kill of the drake, Bubblegum or the colossus swaps their chest for the one holding the trophy
            Assert.True(Found(loot), path.Name + " dropped " + loot.Name + (byCrusher ? " (a crusher's kill)" : ""));
            if (trophy != null)
                Assert.True(byCrusher == (Found(trophy) || Butchered(trophy)), path.Name + (byCrusher ? " left no " : " left a ") + trophy.Name);
            r.AssertNoRuntimes();
        }
    }

    [Fact]
    public void The_last_of_the_legion_leaves_the_storm_staff()
    {
        var r = new AudioRoom(19, 15, seed: 13318);
        var legion = r.In(() => Dm.@new<mob_living_simple_animal_hostile_megafauna_legion>(locate(9, 7, 1)));
        r.Step(2);
        for (int round = 0; round < 8; round++)
        {
            var alive = r.In(() => Dm.each<mob_living_simple_animal_hostile_megafauna_legion>(GLOB.mob_living_list).Where(l => l.stat != DEAD).ToList());
            if (alive.Count == 0)
                break;
            foreach (var part in alive)
            {
                Kill(r, part, byCrusher: false);
                RunFor(r, 10);
            }
            RunFor(r, 20);
        }
        Assert.Equal(0, r.In(() => Dm.each<mob_living_simple_animal_hostile_megafauna_legion>(GLOB.mob_living_list).Count(l => l.stat != DEAD)));
        Assert.True(Count<obj_item_storm_staff>(r) == 1, "one storm staff");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_virtual_megafauna_drops_an_encrypted_cache_and_no_trophy()
    {
        var r = new AudioRoom(15, 11, seed: 13319);
        var drake = r.In(() => Dm.@new<mob_living_simple_animal_hostile_megafauna_dragon>(locate(7, 5, 1)));
        var miner = r.In(() => Dm.@new<mob_living_basic_boss_blood_drunk_miner>(locate(3, 5, 1)));
        r.Step(2);
        r.In(() =>
        {
            drake.make_virtual_megafauna();
            miner.make_virtual_megafauna();
        });
        Assert.Equal(1250f, r.In(() => Dm.num(drake.maxHealth)));
        Assert.Equal(650f, r.In(() => Dm.num(miner.maxHealth)));
        Kill(r, drake, byCrusher: true);
        Kill(r, miner, byCrusher: true);
        Assert.True(RunUntil(r, 60, () => (drake.stat == DEAD || QDELETED(drake)) && (miner.stat == DEAD || QDELETED(miner))), "both died");
        RunFor(r, 20);
        Assert.Equal(2, Count<obj_structure_closet_crate_secure_bitrunning_encrypted>(r));
        Assert.Equal(0, Count<obj_item_crusher_trophy>(r));
        Assert.Equal(0, Count<obj_structure_closet_crate_necropolis>(r));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_menacing_vent_calls_up_its_megafauna()
    {
        var r = new AudioRoom(15, 11, seed: 13320);
        var vent = r.In(() => Dm.@new<obj_structure_ore_vent_boss>(locate(7, 5, 1)));
        r.Step(2);
        r.In(() => Waiting.Detach(vent.start_wave_defense()));
        RunFor(r, 10);
        Assert.True(r.In(() => Dm.each<atom>(range(2, vent)).Any(a => a is mob_living_simple_animal_hostile_megafauna m && m.GetType() == (Type)vent.summoned_boss)), "the vent's boss came");
        r.AssertNoRuntimes();
    }
}
