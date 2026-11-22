using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M7;

/// <summary>
/// The M7 delamination slice (G6, D163): the supermatter's delamination (supermatter_delamination/*.dm). Each strategy's
/// delaminate reaches its effects with tg's numbers: the explosive one explodes with calculate_explosion's ranges
/// (delamination_effects.dm:80-93), the singularity one makes an 800-energy singularity that eats the crystal (:96-104), the tesla
/// one an energy ball of 200 (:107-114), the cascade one its explosion, emergency, rift and crystal mass (cascade_delam.dm:52-67);
/// a main engine scatters ten anomalies over the event spawns, four at once (:55-77); a countdown that runs out delaminates
/// (supermatter.dm:637); the crystal mass grows (cascade_delam_objects.dm:43) and a stage six singularity glares and burns
/// (singularity.dm:271, :435). tg has no unit test of the delamination.
/// </summary>
public class DelaminationTests
{
    private const int Second = 20;

    private static AudioRoom Room(ulong seed, int w = 31, int h = 31)
    {
        var r = new AudioRoom(w, h, seed: seed);
        // the station's name, as tg's STATIONNAME config sets it: the rig has no strings/ files for new_station_name (the farewell names it)
        r.In(() => GLOB.station_name = "Test Station");
        r.Step(Second);
        // the room's area counts as the station's, as SSmapping registers the station level's areas (mapping.dm:509): a dimensional
        // anomaly with nothing left to transmute relocates through the anomaly placer, which picks a station area (anomaly_placer.dm:10)
        r.In(() => Dm.append(ref GLOB.the_station_areas, ((area)get_area(locate(2, 2, 1))).type));
        return r;
    }

    private static obj_machinery_power_supermatter_crystal Crystal(AudioRoom r, int x, int y, Type strategy = null) => r.In(() =>
    {
        var sm = Dm.@new<obj_machinery_power_supermatter_crystal_engine>(locate(x, y, 1));
        sm.time_frozen();
        sm.activation_logged = TRUE;
        if (strategy != null)
            sm.set_delam(SM_DELAM_PRIO_IN_GAME, strategy);
        return sm;
    });

    private static void EventSpawns(AudioRoom r, params (int x, int y)[] at) => r.In(() =>
    {
        foreach (var (x, y) in at)
            Dm.@new<obj_effect_landmark_event_spawn>(locate(x, y, 1));
    });

    /// <summary>A main engine's effect_anomaly takes ten event spawns (delamination_effects.dm:64-65; MetaStation maps 107); with
    /// fewer, tg's RANGE_TURFS of a null spawn runtimes.</summary>
    private static void TenSpawns(AudioRoom r) => EventSpawns(r, Enumerable.Range(0, 10).Select(i => (3 + 2 * i, 2)).ToArray());

    private static List<T> All<T>(AudioRoom r) where T : atom => r.In(() =>
    {
        var found = new List<T>();
        r.World.Objects.ForEachLiveInIdOrder((_, o) =>
        {
            if (o is T a && !QDELETED(a))
                found.Add(a);
        });
        return found;
    });

    [Fact]
    public void TheExplosiveDelaminationExplodesWithCalculateExplosionsRanges()
    {
        AudioRoom r = Room(7901);
        TenSpawns(r);
        obj_machinery_power_supermatter_crystal sm = Crystal(r, 16, 16);
        mob_living_carbon_human bob = r.Join(18, 16, "Bob");
        r.Step(2);
        Assert.IsType<datum_sm_delam_explosive>(r.In(() => sm.delamination_strategy));
        // explosion_power 35 times max(gas_heat_power_generation, 0.205): a crystal without gas (delamination_effects.dm:93)
        Assert.Equal(35 * 0.205f, r.In(() => Dm.num(sm.delamination_strategy.calculate_explosion(sm))), 4);
        r.In(() => sm.force_delam());
        r.Step(2 * Second);
        Assert.True(r.In(() => QDELETED(sm)), "the crystal is gone (_sm_delam.dm:32)");
        Assert.True(r.In(() => QDELETED(bob) || bob.stat == DEAD), "Bob, two tiles away, is inside the devastation range of 3.6");
        Assert.Contains(r.Sounds.Heard, s => s.File == "sound/effects/magic/charge.ogg"); // the players on the level hear reality distort (:41)
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheSingularityDelaminationMakesASingularityThatEatsTheCrystal()
    {
        AudioRoom r = Room(7902);
        TenSpawns(r);
        obj_machinery_power_supermatter_crystal sm = Crystal(r, 16, 16, typeof(datum_sm_delam_singularity));
        turf sm_turf = r.In(() => get_turf(sm));
        r.In(() => sm.force_delam());
        obj_singularity singulo = All<obj_singularity>(r).Single();
        Assert.True(r.In(() => QDELETED(sm)), "the singularity eats the crystal (:104)");
        Assert.Same(sm_turf, r.In(() => get_turf(singulo)));
        // 800 (:103) and the crystal's singularity_act gain of 100 (supermatter_hit_procs.dm:58): a stage six to be
        Assert.Equal(900f, r.In(() => singulo.energy));
        Assert.True(r.In(() => Dm.truthy(singulo.consumed_supermatter)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheTeslaDelaminationMakesAnEnergyBallAndExplodes()
    {
        AudioRoom r = Room(7903);
        TenSpawns(r);
        obj_machinery_power_supermatter_crystal sm = Crystal(r, 16, 16, typeof(datum_sm_delam_tesla));
        r.In(() => sm.force_delam());
        r.Step(Second);
        obj_energy_ball ball = All<obj_energy_ball>(r).Single();
        Assert.Equal(200f, r.In(() => Dm.num(ball.energy))); //Gets us about 9 balls (:113)
        Assert.True(r.In(() => QDELETED(sm)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AMainEngineScattersTenAnomaliesOverTheEventSpawnsFourAtOnce()
    {
        AudioRoom r = Room(7904, 40, 40);
        var spots = Enumerable.Range(0, 10).Select(i => (3 + 3 * i, 30)).ToArray();
        EventSpawns(r, spots);
        obj_machinery_power_supermatter_crystal sm = Crystal(r, 20, 10);
        Assert.Equal(1f, r.In(() => sm.is_main_engine));
        r.In(() => sm.delamination_strategy.effect_anomaly(sm));
        // i < round(10 * 0.5) spawn now (:66-68); the anomalies take the spawns out of tg's own list (:58, no Copy)
        Assert.Equal(4, All<obj_effect_anomaly>(r).Count);
        Assert.Equal(0f, r.In(() => GLOB.generic_event_spawns.len));
        r.Step(10 * Second + 1);
        Assert.True(All<obj_effect_anomaly>(r).Count > 4, "the rest come 5 to 10 seconds later, or 5 to 15 minutes more (:70-76)");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ACountdownThatRunsOutDelaminates()
    {
        AudioRoom r = Room(7905);
        TenSpawns(r);
        obj_machinery_power_supermatter_crystal sm = Crystal(r, 16, 16);
        r.In(() =>
        {
            sm.damage = sm.explosion_point + 1;
            sm.count_down();
        });
        Assert.True(r.In(() => Dm.truthy(sm.final_countdown)));
        r.Step(10 * Second);
        Assert.False(r.In(() => QDELETED(sm)), "15 seconds of countdown (SUPERMATTER_COUNTDOWN_TIME)");
        r.Step(8 * Second);
        Assert.True(r.In(() => QDELETED(sm)), "the countdown's end delaminates (supermatter.dm:637)");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ACountdownThatRunsOutDelaminatesWithALivingMobInNullspace()
    {
        // the owner's round: a character preview's dummy (preferences.dm, a living mob in nullspace) in alive_mob_list made
        // effect_demoralize's is_valid_z_level read null.z, so the delamination stopped before qdel and the crystal hung at 0 %
        AudioRoom r = Room(7910);
        TenSpawns(r);
        r.In(() => Dm.@new<mob_living_carbon_human_dummy>());
        obj_machinery_power_supermatter_crystal sm = Crystal(r, 16, 16, typeof(datum_sm_delam_singularity));
        r.In(() =>
        {
            sm.damage = sm.explosion_point + 1;
            sm.count_down();
        });
        r.Step(18 * Second);
        Assert.True(r.In(() => QDELETED(sm)), "the countdown's end delaminates (supermatter.dm:637)");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ADevDelaminationDuringACountdownIsRefused()
    {
        // the M7 visual check: smdelam forced a tesla delamination while the crystal counted down, and the countdown's end ran
        // delaminate again on the deleted crystal (supermatter.dm:637), whose null turf effect_demoralize reads (levels.dm:15)
        AudioRoom r = Room(7908);
        TenSpawns(r);
        r.Join(29, 29, "Bob"); // effect_demoralize reads the turf of each living mob
        obj_machinery_power_supermatter_crystal sm = Crystal(r, 16, 16);
        r.In(() =>
        {
            sm.damage = sm.explosion_point + 1;
            sm.count_down();
        });
        r.Step(Second);
        string reply = r.In(() => DevCommands.SmDelam(locate(16, 16, 1), "tesla"));
        Assert.Contains("counting down", reply, StringComparison.Ordinal);
        Assert.False(r.In(() => QDELETED(sm)));
        r.Step(17 * Second);
        Assert.True(r.In(() => QDELETED(sm)), "the countdown's end delaminates (supermatter.dm:637)");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void APersonSickFromTheRadiationVomitsThroughTheStomachsOnVomit()
    {
        // the M7 visual check: a person near the hot crystal vomited (tox_vomit) and the stomach's on_vomit (_stomach.dm:414) was not
        // translated; an empty stomach spews nothing out
        AudioRoom r = Room(7909);
        mob_living_carbon_human bob = r.Join(16, 16, "Bob");
        object vomited = r.In(() => Waiting.NoSleep(bob.vomit(Dm.bitor(MOB_VOMIT_MESSAGE, MOB_VOMIT_FORCE))));
        Assert.True(Dm.truthy(vomited));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheCascadeExplodesOpensTheRiftAndGrowsTheCrystalMass()
    {
        AudioRoom r = Room(7906, 70, 16);
        // six spawns at each end: the rift takes one, the crystal mass the other end's, 30 tiles or more away (:229-232)
        EventSpawns(r, Enumerable.Range(0, 6).Select(i => (2 + i, 8)).Concat(Enumerable.Range(0, 6).Select(i => (62 + i, 8))).ToArray());
        // the cascade strands the emergency shuttle (delamination_effects.dm:117): the room has no map, so SSshuttle has none; one idle in a corner
        var emergency = r.In(() => SSshuttle.emergency = Dm.@new<obj_docking_port_mobile_emergency>(locate(69, 15, 1)));
        obj_machinery_power_supermatter_crystal sm = Crystal(r, 35, 8, typeof(datum_sm_delam_cascade));
        Assert.IsType<datum_sm_delam_cascade>(r.In(() => sm.delamination_strategy));
        turf sm_turf = r.In(() => get_turf(sm));
        r.In(() => sm.force_delam());
        r.Step(Second);
        Assert.Equal(TRUE, r.In(() => Dm.num(GLOB.emergency_access))); // effect_emergency_state's make_maint_all_access (:174)
        Assert.Empty(All<obj_cascade_portal>(r));
        r.Step(7 * Second); // sleep(2 SECONDS), the stranded shuttle, sleep(5 SECONDS) (cascade_delam.dm:59-61)
        obj_cascade_portal rift = All<obj_cascade_portal>(r).Single();
        Assert.Equal(SHUTTLE_STRANDED, r.In(() => Dm.str(emergency.mode)));
        Assert.True(r.In(() => QDELETED(sm)));
        Assert.Equal(1f, r.In(() => Dm.num(SSsupermatter_cascade.cascade_initiated)));
        Assert.Equal(1f, r.In(() => Dm.num(SSsupermatter_cascade.can_fire)));
        List<obj_crystal_mass> seeds = All<obj_crystal_mass>(r);
        Assert.Contains(seeds, m => r.In(() => get_turf(m)) == sm_turf);
        // the masses grow a tile a turn, every 0 to 3 seconds, on SSsupermatter_cascade (cascade_delam_objects.dm:43-76)
        r.Step(5 * Second);
        Assert.True(All<obj_crystal_mass>(r).Count > seeds.Count, "the crystal mass grows");
        Assert.All(All<obj_crystal_mass>(r).Where(m => r.In(() => m.loc) != null), m => Assert.Matches("^crystal_cascade_[1-6]$", r.In(() => m.icon_state)));
        // the rift destroyed, the round ends a minute later (delamination_effects.dm:193-220)
        r.In(() => qdel(rift));
        r.Step(61 * Second);
        Assert.Equal((float)FORCE_END_ROUND, r.In(() => SSticker.force_ending));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AStageSixSingularityGlaresAndSetsPeopleOnFire()
    {
        AudioRoom r = Room(7907);
        mob_living_carbon_human bob = r.Join(22, 16, "Bob");
        obj_singularity singulo = r.In(() =>
        {
            var s = Dm.@new(typeof(obj_singularity), locate(10, 16, 1), STAGE_SIX_ENERGY_REQUIREMENT + 100) as obj_singularity;
            s.consumed_supermatter = TRUE;
            Waiting.NoSleep(s.expand(STAGE_SIX));
            return s;
        });
        Assert.Equal((float)STAGE_SIX, r.In(() => singulo.current_size));
        Assert.NotNull(r.In(() => singulo.GetComponent(typeof(datum_component_vision_hurting))));
        r.In(() => Waiting.NoSleep(singulo.combust_mobs()));
        Assert.True(r.In(() => bob.fire_stacks) >= 5, "urange(20) reaches Bob 12 tiles away: five fire stacks (singularity.dm:440)");
        Assert.True(r.In(() => Dm.truthy(bob.on_fire)), "and ignites him (:441)");
        r.AssertNoRuntimes();
    }
}
