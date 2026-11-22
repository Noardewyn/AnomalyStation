using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M2Medical.P1;

/// <summary>
/// The living core (M2 P1) on a booted toy room: SSmobs runs Life every 2 s, damage of each type moves health and stat
/// across tg's thresholds (crit 0, full crit -30, dead -100), a knockdown floors the mob and get_up's do_after stands it up
/// one second after the knockdown ends, resting and getting up, the stagger's 0.85 ds per tile, a shove into a wall, and
/// walk and run from the configuration (RUN_DELAY 1.5, WALK_DELAY 4). The world keeps <c>NotTranslatedThrows</c> on,
/// except where a scenario names the other package's skeleton it passes through.
/// </summary>
public class LivingCoreTests
{
    // 20 ticks per second (world.fps, World.Fps): 1 s is 20 ticks, 2 s is 40.
    private const int TicksPerSecond = 20;

    [Fact]
    public void SSmobsRunsLifeEveryTwoSeconds()
    {
        var room = new LivingRoom(seed: 51);
        mob_living_test_p1_carbonlike living = room.Spawn();
        datum_test_m2p1_listener listener = room.In(() =>
        {
            var l = Dm.@new<datum_test_m2p1_listener>();
            l.Watch(living);
            return l;
        });
        room.Step(5 * TicksPerSecond);
        room.AssertNoRuntimes();

        // tg: SSmobs wait = 2 SECONDS, seconds_per_tick = wait / (1 SECONDS) (mobs.dm:5, :36)
        Assert.InRange(listener.Lives.Count, 2, 3);
        Assert.All(listener.Lives, life => Assert.Equal(2f, Dm.num(life.SecondsPerTick)));
        for (int i = 1; i < listener.Lives.Count; i++)
            Assert.Equal(2 * TicksPerSecond, listener.Lives[i].Tick - listener.Lives[i - 1].Tick);
        room.In(() =>
        {
            Assert.Equal(STABLE, living.stat);
            Assert.Equal(100f, living.health);
            Assert.True(Dm.@in(living, GLOB.mob_living_list));
            Assert.True(Dm.@in(living, GLOB.alive_mob_list));
        });
    }

    [Theory]
    [InlineData(BRUTE)]
    [InlineData(BURN)]
    [InlineData(TOX)]
    [InlineData(OXY)]
    public void DamageOfEachTypeCrossesTgsThresholds(string damagetype)
    {
        var room = new LivingRoom(seed: 52);
        mob_living_test_p1_carbonlike living = room.Spawn();
        void Hit(float amount) => room.In(() => Waiting.NoSleep(living.apply_damage(amount, damagetype)));

        Hit(99);
        room.In(() =>
        {
            Assert.Equal(1f, living.health);
            Assert.Equal(STABLE, living.stat);
            Assert.Equal(99f, Dm.num(living.get_current_damage_of_type(damagetype)));
            Assert.False(HAS_TRAIT(living, TRAIT_FLOORED));
        });

        Hit(1); // health 0: HEALTH_THRESHOLD_CRIT (combat.dm:88)
        room.In(() =>
        {
            Assert.Equal(0f, living.health);
            Assert.Equal(SOFT_CRIT, living.stat);
            Assert.True(HAS_TRAIT_FROM(living, TRAIT_FLOORED, STAT_TRAIT));
            Assert.True(HAS_TRAIT_FROM(living, TRAIT_INCAPACITATED, STAT_TRAIT));
            Assert.True(HAS_TRAIT_FROM(living, TRAIT_HANDS_BLOCKED, STAT_TRAIT));
            Assert.False(HAS_TRAIT(living, TRAIT_KNOCKEDOUT));
            Assert.Equal(LYING_DOWN, living.body_position); // floored: on_floored_start lays the mob down
            Assert.False(Dm.truthy(living.density));
        });

        Hit(29);
        room.In(() => Assert.Equal(SOFT_CRIT, living.stat));
        Hit(1); // health -30: HEALTH_THRESHOLD_FULLCRIT (combat.dm:89)
        room.In(() =>
        {
            Assert.Equal(-30f, living.health);
            Assert.Equal(HARD_CRIT, living.stat);
            Assert.True(HAS_TRAIT_FROM(living, TRAIT_KNOCKEDOUT, STAT_TRAIT));
            Assert.True(Dm.truthy(living.has_status_effect(typeof(datum_status_effect_knocked_out))));
            Assert.True(HAS_TRAIT_FROM(living, TRAIT_DEAF, STAT_TRAIT));
        });

        Hit(69);
        room.In(() => Assert.Equal(HARD_CRIT, living.stat));
        Hit(1); // health -100: HEALTH_THRESHOLD_DEAD (combat.dm:90)
        room.In(() =>
        {
            Assert.Equal(-100f, living.health);
            Assert.Equal(DEAD, living.stat);
            Assert.True(Dm.@in(living, GLOB.dead_mob_list));
            Assert.False(Dm.@in(living, GLOB.alive_mob_list));
            Assert.Equal(room.In(() => Dm.world.time), living.timeofdeath);
        });
        room.Step(3 * TicksPerSecond); // SSmobs keeps running Life on the dead mob
        room.AssertNoRuntimes();
    }

    [Fact]
    public void KnockdownLaysTheMobDownAndGetUpTakesASecond()
    {
        var room = new LivingRoom(seed: 53);
        mob_living_test_p1_carbonlike living = room.Spawn();
        float standing = room.In(() => Dm.num(living.cached_multiplicative_slowdown));
        long knockedAt = room.Tick;
        room.In(() => Waiting.NoSleep(living.Knockdown(2 * SECONDS)));
        room.In(() =>
        {
            Assert.True(Dm.truthy(living.IsKnockdown()));
            Assert.True(HAS_TRAIT_FROM(living, TRAIT_FLOORED, TRAIT_STATUS_EFFECT("knockdown")));
            Assert.Equal(LYING_DOWN, living.body_position);
            Assert.False(Dm.truthy(living.density)); // TRAIT_UNDENSE from LYING_DOWN_TRAIT
            Assert.True(Dm.truthy(living.has_movespeed_modifier(typeof(datum_movespeed_modifier_carbon_crawling))));
            Assert.True(Dm.num(living.cached_multiplicative_slowdown) > standing);
        });

        // The knockdown's duration counts down by SSpriority_effects' wait (0.2 s) on each of its fires (_status_effect.dm:100),
        // so it ends on the tenth fire after it began: 2 s, less up to one wait by the subsystem's phase (M4b: SSair in the
        // Master's queue shifted that phase).
        int toFloorEnd = room.StepUntil(() => !HAS_TRAIT(living, TRAIT_FLOORED), 3 * TicksPerSecond);
        Assert.InRange(toFloorEnd, 2 * TicksPerSecond - 4, 2 * TicksPerSecond + 5);
        room.In(() => Assert.Equal(LYING_DOWN, living.body_position)); // get_up's do_after has started

        // get_up: do_after(src, 1 SECONDS) (living.dm:715)
        int toStand = room.StepUntil(() => living.body_position == STANDING_UP, 2 * TicksPerSecond);
        Assert.InRange(toStand, TicksPerSecond, TicksPerSecond + 2);
        room.In(() =>
        {
            Assert.True(Dm.truthy(living.density));
            Assert.False(Dm.truthy(living.has_movespeed_modifier(typeof(datum_movespeed_modifier_carbon_crawling))));
            Assert.Equal(standing, Dm.num(living.cached_multiplicative_slowdown));
            Assert.Equal(0f, living.lying_angle);
        });
        Assert.True(room.Tick - knockedAt >= 3 * TicksPerSecond - 4); // the knockdown's countdown, less up to one wait (above)
        room.AssertNoRuntimes();
    }

    [Fact]
    public void RestAndGetUp()
    {
        var room = new LivingRoom(seed: 54);
        mob_living_test_p1_carbonlike living = room.Spawn();
        room.In(() => Waiting.NoSleep(living.toggle_resting()));
        room.In(() =>
        {
            Assert.Equal(TRUE, living.resting);
            Assert.Equal(LYING_DOWN, living.body_position); // set_lying_down at once
            Assert.False(Dm.truthy(living.density));
            Assert.False(HAS_TRAIT(living, TRAIT_FLOORED));
            Assert.True(Dm.truthy(living.has_movespeed_modifier(typeof(datum_movespeed_modifier_carbon_crawling))));
        });
        room.Step(TicksPerSecond * 3);
        room.In(() => Assert.Equal(LYING_DOWN, living.body_position)); // resting keeps it down

        room.In(() => Waiting.NoSleep(living.toggle_resting()));
        room.In(() =>
        {
            Assert.Equal(FALSE, living.resting);
            Assert.Equal(LYING_DOWN, living.body_position); // get_up's do_after runs
        });
        int toStand = room.StepUntil(() => living.body_position == STANDING_UP, 2 * TicksPerSecond);
        Assert.InRange(toStand, TicksPerSecond, TicksPerSecond + 2);
        room.In(() => Assert.True(Dm.truthy(living.density)));
        room.AssertNoRuntimes();
    }

    [Fact]
    public void RestingAgainInterruptsGettingUp()
    {
        var room = new LivingRoom(seed: 55);
        mob_living_test_p1_carbonlike living = room.Spawn();
        room.In(() => Waiting.NoSleep(living.toggle_resting()));
        room.In(() => Waiting.NoSleep(living.toggle_resting())); // get up: the do_after starts
        room.Step(TicksPerSecond / 2);
        room.In(() => Waiting.NoSleep(living.toggle_resting())); // rest again: rest_checks_callback fails the do_after
        room.Step(TicksPerSecond * 2);
        room.In(() => Assert.Equal(LYING_DOWN, living.body_position));
        room.AssertNoRuntimes();
    }

    [Fact]
    public void StaggerAddsPointEightFiveDsPerTile()
    {
        var room = new LivingRoom(seed: 56);
        mob_living_test_p1_carbonlike living = room.Spawn();
        float before = room.In(() => Dm.num(living.cached_multiplicative_slowdown));
        room.In(() => Waiting.NoSleep(living.adjust_staggered(STAGGERED_SLOWDOWN_LENGTH)));
        room.In(() =>
        {
            Assert.True(Dm.truthy(living.has_status_effect(typeof(datum_status_effect_staggered))));
            Assert.Equal(before + STAGGERED_SLOWDOWN_STRENGTH, Dm.num(living.cached_multiplicative_slowdown), 4);
        });
        // STAGGERED_SLOWDOWN_LENGTH is 3 s; each SSfastprocess fire (wait 0.2 s, 4 ticks) takes 0.2 s off the duration, so the
        // effect leaves on its 15th fire: 3 s give or take one wait, by where the first fire falls (the Master staggers the
        // subsystems' first fires at random, so another subsystem in the list moves it; M4b slice 3's SSwet_floors did).
        int toEnd = room.StepUntil(() => !Dm.truthy(living.has_status_effect(typeof(datum_status_effect_staggered))), 5 * TicksPerSecond);
        Assert.InRange(toEnd, 3 * TicksPerSecond - 4, 3 * TicksPerSecond + 5);
        room.In(() => Assert.Equal(before, Dm.num(living.cached_multiplicative_slowdown), 4));
        room.AssertNoRuntimes();
    }

    // The knockdown of a shove dazes (Knockdown's daze_amount, status_procs.dm:96): /datum/status_effect/dazed draws its
    // overlay with mutable_appearance, an M2 P3 skeleton in this branch, so the scenario runs with NotTranslatedThrows off
    // and admits that report only.
    private const string DazedOverlaySkeleton = "code/datums/mutable_appearance.dm:26";

    [Fact]
    public void AShoveIntoAWallKnocksDown()
    {
        var room = new LivingRoom(seed: 57, strict: false);
        (int x, int y) = room.In(() => { turf t = TgToyRoom.TopRight(); return ((int)t.x, (int)t.y); });
        mob_living_test_p1_carbonlike target = room.Spawn(x, y);
        mob_living_test_p1_carbonlike shover = room.Spawn(x - 1, y);
        turf targetTurf = room.In(() => (turf)target.loc);
        room.In(() => Waiting.NoSleep(shover.disarm(target)));
        room.In(() =>
        {
            Assert.Same(targetTurf, target.loc); // the wall stopped the shove (SHOVE_BLOCKED)
            Assert.True(Dm.truthy(target.IsKnockdown())); // SHOVE_KNOCKDOWN_SOLID
            Assert.True(Dm.truthy(target.has_status_effect(typeof(datum_status_effect_dazed)))); // daze_amount 3 s (living_defense.dm:851)
            Assert.True(HAS_TRAIT(target, TRAIT_FLOORED));
            Assert.Equal(LYING_DOWN, target.body_position);
            Assert.Equal(STANDING_UP, shover.body_position);
            Assert.Equal(GRAB_PASSIVE, target.grab_state);
        });
        room.AssertOnlySkeletons(DazedOverlaySkeleton);
    }

    [Fact]
    public void AShoveOnOpenFloorPushesOneTile()
    {
        var room = new LivingRoom(seed: 58);
        mob_living_test_p1_carbonlike target = room.Spawn(4, 4);
        mob_living_test_p1_carbonlike shover = room.Spawn(3, 4);
        room.In(() => Waiting.NoSleep(shover.disarm(target)));
        room.In(() =>
        {
            Assert.Same(locate(5, 4, 1), target.loc);
            Assert.False(Dm.truthy(target.IsKnockdown()));
            Assert.Equal(STANDING_UP, target.body_position);
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void ShovingOnTheSameTurfIsRefused()
    {
        var room = new LivingRoom(seed: 59);
        mob_living_test_p1_carbonlike target = room.Spawn(4, 4);
        mob_living_test_p1_carbonlike shover = room.Spawn(4, 4);
        room.In(() =>
        {
            Assert.False(Dm.truthy(shover.can_disarm(target))); // not capsule mobs: tg's same-turf refusal (living_defense.dm:889)
            Waiting.NoSleep(shover.disarm(target));
            Assert.Same(locate(4, 4, 1), target.loc);
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void WalkAndRunTakeTheirDelaysFromTheConfig()
    {
        var room = new LivingRoom(seed: 60);
        room.In(() =>
        {
            Waiting.NoSleep(config.Set(typeof(datum_config_entry_number_movedelay_run_delay), 1.5f));
            Waiting.NoSleep(config.Set(typeof(datum_config_entry_number_movedelay_walk_delay), 4));
        });
        mob_living_test_p1_carbonlike living = room.Spawn();
        room.In(() =>
        {
            Assert.Equal(MOVE_INTENT_RUN, living.move_intent);
            Assert.Equal(1.5f, Dm.num(living.cached_multiplicative_slowdown), 4);
            Waiting.NoSleep(living.toggle_move_intent());
            Assert.Equal(MOVE_INTENT_WALK, living.move_intent);
            Assert.Equal(4f, Dm.num(living.cached_multiplicative_slowdown), 4);
            Waiting.NoSleep(living.toggle_move_intent());
            Assert.Equal(1.5f, Dm.num(living.cached_multiplicative_slowdown), 4);
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void ReviveAfterDeathRestoresTheMob()
    {
        var room = new LivingRoom(seed: 61);
        mob_living_test_p1_carbonlike living = room.Spawn();
        room.In(() => Waiting.NoSleep(living.apply_damage(200, BRUTE)));
        room.In(() => Assert.Equal(DEAD, living.stat));
        room.In(() => Waiting.NoSleep(living.revive(full_heal_flags: HEAL_ALL)));
        room.In(() =>
        {
            Assert.Equal(STABLE, living.stat);
            Assert.Equal(100f, living.health);
            Assert.False(HAS_TRAIT(living, TRAIT_FLOORED));
            Assert.True(Dm.@in(living, GLOB.alive_mob_list));
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void TheScenariosAreDeterministic()
    {
        static string Run()
        {
            var room = new LivingRoom(seed: 62);
            mob_living_test_p1_carbonlike living = room.Spawn();
            mob_living_test_p1_carbonlike other = room.Spawn(4, 3);
            var trace = new List<string>();
            room.In(() => Waiting.NoSleep(living.apply_damage(40, BURN)));
            room.In(() => Waiting.NoSleep(living.Knockdown(2 * SECONDS)));
            room.In(() => Waiting.NoSleep(other.adjust_staggered(STAGGERED_SLOWDOWN_LENGTH)));
            for (int i = 0; i < 6 * TicksPerSecond; i++)
            {
                room.Step();
                trace.Add(room.In(() => $"{room.Tick} {living.health} {living.stat} {living.body_position} {living.cached_multiplicative_slowdown} {other.cached_multiplicative_slowdown}"));
            }
            room.AssertNoRuntimes();
            return string.Join("\n", trace);
        }
        Assert.Equal(Run(), Run());
    }
}
