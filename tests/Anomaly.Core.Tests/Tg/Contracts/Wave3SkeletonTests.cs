using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Contracts;

/// <summary>
/// What the wave-3 contracts (W3.0) promise of the Tg skeletons that is behaviour rather than a signature: the inert stub
/// subsystems (lead decision W3 G2), the Failsafe skeleton (G4, DV10), the Master's statics and the deferred leaves.
/// </summary>
public class Wave3SkeletonTests
{
    public static TheoryData<Type, string, string, float, Type[]> Stubs() => new()
    {
        // SSticker left the stubs in M10 slice F (tg's pregame, setup and round start).
        { typeof(datum_controller_subsystem_blackbox), "/datum/controller/subsystem/blackbox", "Blackbox", INITSTAGE_MAIN, Array.Empty<Type>() },
        { typeof(datum_controller_subsystem_dbcore), "/datum/controller/subsystem/dbcore", "Database", INITSTAGE_FIRST, Array.Empty<Type>() },
        { typeof(datum_controller_subsystem_mapping), "/datum/controller/subsystem/mapping", "Mapping", INITSTAGE_MAIN,
            new[] { typeof(datum_controller_subsystem_job), typeof(datum_controller_subsystem_processing_station), typeof(datum_controller_subsystem_processing_reagents) } },
        // SSjob left the stubs in M4a P5 (tg's flags and its Initialize: the job datums that the ID cards' trims read).
        // SSreagents left the stubs in gaps F1 (the reaction engine: its Initialize builds the reaction lists, it fires the equilibria).
        // SSstation initializes since M9 slice D (its announcer, the announcements' sounds): it still does not fire, see below.
        // M3 P4 translated SSicon_smooth (controllers/subsystem/icon_smooth.dm): no longer a stub.
    };

    [Theory]
    [MemberData(nameof(Stubs))]
    public void StubSubsystemsKeepTgsIdentityAndNeitherInitializeNorFire(Type type, string path, string name, float init_stage, Type[] dependencies)
    {
        var w = new World();
        using (w.Enter())
        {
            Assert.Equal(path, DmTypes.PathOf(type));
            var ss = (datum_controller_subsystem)Dm.@new(type);
            Assert.Equal(name, ss.name);
            Assert.Equal(init_stage, ss.init_stage);
            Assert.Equal(dependencies, Dm.each(ss.dependencies).Cast<object>().ToArray());
            Assert.True(Dm.truthy(Dm.bitand(ss.ss_flags, SS_NO_INIT)));
            Assert.True(Dm.truthy(Dm.bitand(ss.ss_flags, SS_NO_FIRE)));
            Assert.True(Dm.@in(ss, global_vars__subsystems())); // SUBSYSTEM_DEF's New registered its global
        }
    }

    [Fact]
    public void TheStationSubsystemHasTgsFlags()
    {
        // gaps R2: no longer a stub: tg's flags since its station traits (station.dm:2-5), a background subsystem that fires every
        // 5 seconds in the game for the traits that process; its Initialize (the traits, then the announcer) is StationTraitsR2Tests'
        var w = new World();
        using (w.Enter())
        {
            var ss = Dm.@new<datum_controller_subsystem_processing_station>();
            Assert.Equal("Station", ss.name);
            Assert.Equal((float)SS_BACKGROUND, ss.ss_flags);
            Assert.Equal((float)RUNLEVEL_GAME, ss.runlevels);
            Assert.Equal(5f * SECONDS, ss.wait);
            Assert.Equal(typeof(datum_centcom_announcer_default), ss.announcer);
        }
    }

    [Fact]
    public void EveryDependencyOfATgSubsystemIsATgSubsystemType()
    {
        // master.dm:361-363: type_to_subsystem[dependency_type] must find an instance, or Master.Initialize runtimes.
        var w = new World();
        using (w.Enter())
        {
            DmList types = TgState.Current.FilterSubsystemTypes(subtypesof(typeof(datum_controller_subsystem)));
            var created = new List<datum_controller_subsystem>();
            foreach (object t in Dm.each(types)) created.Add((datum_controller_subsystem)Dm.@new((Type)t));
            var present = new HashSet<Type>(created.Select(s => s.GetType()));
            foreach (datum_controller_subsystem ss in created)
            {
                foreach (object dependency in Dm.each(ss.dependencies))
                    Assert.True(present.Contains((Type)dependency), ss.GetType().Name + " depends on " + dependency);
            }
            Assert.Contains(typeof(datum_controller_subsystem_atoms), present);
            Assert.Contains(typeof(datum_controller_subsystem_garbage), present);
        }
    }

    [Fact]
    public void TheFailsafeSkeletonRegistersItselfAndIsNeverOverdue()
    {
        var w = new World();
        w.Step(1000);
        using (w.Enter())
        {
            Assert.Null(Failsafe);
            var failsafe = Dm.@new<datum_controller_failsafe>();
            Assert.Same(failsafe, Failsafe);
            Assert.Equal("Failsafe", failsafe.name);
            Assert.Equal(0f, failsafe.processing_interval); // DV10
            // master.dm:701-702: Loop creates a new Failsafe when this is true; with the skeleton it never is.
            bool recreate = Failsafe == null || (Failsafe.processing_interval > 0 && (Failsafe.lasttick + (Failsafe.processing_interval * 5)) < Dm.world.time);
            Assert.False(recreate);
        }
    }

    [Fact]
    public void TheMastersStaticsArePerWorldWithTgsDefaults()
    {
        var a = new World();
        var b = new World();
        using (a.Enter())
        {
            Assert.Equal(98f, datum_controller_master.current_ticklimit); // TICK_LIMIT_RUNNING with map_cpu 0 (DV3)
            Assert.Null(datum_controller_master.init_stage_completed);
            datum_controller_master.current_ticklimit = 70;
            datum_controller_master.restart_count++;
            var mc = new datum_controller_master();
            Assert.Equal("Master", mc.name);
            Assert.Equal(50f, mc.rolling_usage_length); // 5 SECONDS
        }
        using (b.Enter())
        {
            Assert.Equal(98f, datum_controller_master.current_ticklimit);
            Assert.Equal(0f, datum_controller_master.restart_count);
        }
    }

    [Fact]
    public void DeferredStubLeavesNeitherThrowNorReport()
    {
        var w = new World();
        using (w.Enter())
        {
            var blackbox = Dm.@new<datum_controller_subsystem_blackbox>();
            blackbox.record_feedback("tally", "subsystem_initialize", 1, "Timer");
            Waiting.NoSleep(Dm.@new<datum_controller_subsystem_dbcore>().InitializeRound());
            Waiting.NoSleep(Dm.world.TgsInitializationComplete());
        }
        Assert.Equal(0, w.RuntimeErrorCount);
        // M3 P4: SSicon_smooth's free_deferred is translated, no longer a deferred leaf; M10 slice I: InitializeRound is translated (no
        // round database in this world: tg's early return)
        Assert.Equal(1, DmWorldState.Of(w).DeferredCount);
    }

    [Fact]
    public void EveryConfigEntryTypeOfGeneralDmExists()
    {
        // CONFIG_GET(number/fps) is config.Get(typeof(datum_config_entry_number_fps)) (rules 11.1 (b)); the boot package
        // completes the types.
        int general = TypeOrder.Paths.Count(p => p.StartsWith("/datum/config_entry/", StringComparison.Ordinal)
                                                 && Dm.text2path(p) != null && Dm.text2path(p).Assembly == typeof(TgState).Assembly);
        Assert.True(general >= 231, general.ToString());
        Assert.Equal("/datum/config_entry/number/fps", DmTypes.PathOf(typeof(datum_config_entry_number_fps)));
        Assert.True(typeof(datum_config_entry_number).IsAssignableFrom(typeof(datum_config_entry_number_mc_tick_rate_base_mc_tick_rate)));
    }

    [Fact]
    public void AtomAppearanceVarsAreStoredSoThatConstructorsCanSetThem()
    {
        var item = new obj_item();
        Assert.Equal(Dm.SOUTH, item.dir);   // BYOND's default dir
        Assert.Equal(255f, item.alpha);
        item.layer = 3;
        item.plane = -2;
        Assert.Equal(3f, item.layer);
        Assert.Equal(-2f, item.plane);
        Assert.Equal("/turf/open/space/basic", DmTypes.PathOf(typeof(turf_open_space_basic)));
        Assert.Equal("/area/space", DmTypes.PathOf(typeof(area_space)));
        Assert.Equal("/mob/dead/new_player", DmTypes.PathOf(typeof(mob_dead_new_player)));
    }
}
