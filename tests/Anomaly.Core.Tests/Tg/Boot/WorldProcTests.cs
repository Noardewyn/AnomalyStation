using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Boot;

/// <summary>
/// tg's /world procs of the boot (code/game/world.dm) and the /world defaults (code/world.dm), written from the DM source.
/// The world/New path is run with the configuration and GLOB of the boot package and a world without the Master's subsystems
/// (no-init), so that it ends before Master.Initialize.
/// </summary>
public class WorldProcTests
{
    [Fact]
    public void TheBootstrapSetsTheWorldVarsOfCodeWorldDm()
    {
        var w = new World();
        using (w.Enter())
        {
            TgWorld.dm_init_vars(Dm.world);
            Assert.Equal(typeof(mob_dead_new_player), Dm.world.mob);
            Assert.Equal(typeof(turf_open_space_basic), Dm.world.turf);
            Assert.Equal(typeof(area_space), Dm.world.area);
            Assert.Equal("15x15", Dm.world.view);
            Assert.Equal("/tg/ Station 13", Dm.world.name);
            Assert.Equal(20f, Dm.world.fps);
        }
    }

    [Fact]
    public void ChangeFpsToTheConfiguredTwentyIsANoOpAndAnythingElseIsARuntime()
    {
        var w = new World();
        using (w.Enter())
        {
            Dm.world.change_fps(20); // Master.Initialize: world.change_fps(CONFIG_GET(number/fps)), master.dm:467
            Dm.world.change_fps();   // the default is 20
            var other = Assert.Throws<DmCrashException>(() => Dm.world.change_fps(30));
            Assert.Contains("DV6", other.Message);
            Assert.Equal(502, other.DmLine);
            var bad = Assert.Throws<DmCrashException>(() => Dm.world.change_fps(0));
            Assert.Equal("change_fps() called with 0 new_value.", bad.Message);

            Dm.world.change_tick_lag(0.5f);
            Dm.world.change_tick_lag();
            Assert.Throws<DmCrashException>(() => Dm.world.change_tick_lag(1));
            Assert.Throws<DmCrashException>(() => Dm.world.change_tick_lag(-1));
            Assert.Equal(20f, Dm.world.fps);
            Assert.Equal(0.5f, Dm.world.tick_lag);

            Dm.world.on_tickrate_change(); // SStimer?.reset_buckets(): no timer subsystem in this world
        }
    }

    [Fact]
    public void IncreaseMaxXOrYWithoutZLevelsOnlyGrowsTheMap()
    {
        var w = new World();
        using (w.Enter())
        {
            var resized = new List<(float, float, float)>();
            DmWorldState.Current.MapResized = (x, y, z) => resized.Add((x, y, z));
            Dm.world.increase_max_x(10, 0);
            Dm.world.increase_max_y(5, 0);
            Dm.world.increase_max_x(3, 0); // never shrinks
            Assert.Equal(10f, Dm.world.maxx);
            Assert.Equal(5f, Dm.world.maxy);
            Assert.Equal(new[] { (0f, 0f, 0f), (10f, 0f, 0f) }, resized);
        }
    }

    [Fact]
    public void IncrementMaxZAddsAZLevelGrowsSSmobsLists()
    {
        var w = new World();
        using (w.Enter())
        {
            Dm.@new<datum_controller_subsystem_mobs>(); // tg's Master always has it (SUBSYSTEM_DEF(mobs))
            Dm.world.incrementMaxZ();
            Dm.world.incrementMaxZ();
            Assert.Equal(2f, Dm.world.maxz);
            // SSmobs.MaxZChanged (world.dm:493, mobs.dm:20): one list per z-level
            Assert.Equal(2, datum_controller_subsystem_mobs.clients_by_zlevel.len);
            Assert.Equal(2, datum_controller_subsystem_mobs.dead_players_by_zlevel.len);
        }
    }

    [Fact]
    public void TickRecordsTheLastMaptickTime()
    {
        using var w = new BootPieces();
        w.World.Step(4);
        using (w.World.Enter())
        {
            Dm.world.Tick();
            Assert.Equal(2f, GLOB.last_maptick_time);
        }
    }

    // world/New with the pieces Genesis creates (config, logger, SSdbcore): it reaches log_world, server_timestamp and
    // init_logging of other packages, reported until the merge.
    private static BootPieces WorldBeforeNew(Action<BootPieces> files = null)
    {
        var w = new BootPieces(notTranslatedThrows: false);
        logger = Dm.@new<datum_log_holder>();
        config = Dm.@new<datum_controller_configuration>();
        Dm.@new<datum_controller_subsystem_dbcore>();
        files?.Invoke(w);
        return w;
    }

    [Fact]
    public void WorldNewLoadsTheConfigurationAndStopsBeforeTheMasterWithNoInit()
    {
        using BootPieces w = WorldBeforeNew(p =>
        {
            p.Files.WriteText("config/config.txt", "$include x.txt\nFPS 25");
            p.Files.WriteText("config/x.txt", "");
            Dm.world.@params["no-init"] = 1;
        });

        DmTask boot = Dm.world.New();

        Assert.True(boot.IsCompleted);
        Assert.False(boot.IsFaulted, boot.Exception?.ToString());
        Assert.True(Dm.truthy(config.loaded));
        Assert.True(Dm.eq(config.Get(typeof(datum_config_entry_number_fps)), 25));
        Assert.StartsWith("data/logs/2000/01/01/round-", (string)GLOB.log_directory); // world.realtime starts at 0: 1 Jan 2000
        Assert.Null(GLOB.round_id); // SSdbcore.InitializeRound: no database (M10 slice I's RoundDatabaseSeam), as tg without one
        Assert.Equal(0, GLOB.admin_datums.len); // load_admins: no admins.txt
    }

    [Fact]
    public void WorldNewLoadsTheConfigurationFromTheDirectoryParameter()
    {
        using BootPieces w = WorldBeforeNew(p =>
        {
            p.Files.WriteText("custom/config.txt", "$include x.txt\nLOBBY_COUNTDOWN 30");
            p.Files.WriteText("custom/x.txt", "");
            Dm.world.@params[OVERRIDE_CONFIG_DIRECTORY_PARAMETER] = "custom";
            Dm.world.@params["no-init"] = 1;
        });

        Assert.False(Dm.world.New().IsFaulted);
        Assert.Equal("custom", config.directory);
        Assert.True(Dm.eq(config.Get(typeof(datum_config_entry_number_lobby_countdown)), 30));
    }

    [Fact]
    public void SetupLogsTakesTheLogDirectoryParameter()
    {
        using BootPieces w = WorldBeforeNew(_ => Dm.world.@params["log-directory"] = "test-run");
        w.LoadConfig(); // world/New loads the configuration before ConfigLoaded runs SetupLogs (game/world.dm:134, :136, :159)
        Dm.world.SetupLogs();
        Assert.Equal("data/logs/test-run", GLOB.log_directory);
        Assert.Equal("O_test-run_", GLOB.picture_logging_prefix);
        Assert.Equal("data/picture_logs/test-run", GLOB.picture_log_directory);
    }

    [Fact]
    public void ConfigLoadedReadsAndDeletesTheRestartCounter()
    {
        using BootPieces w = WorldBeforeNew(p => p.Files.WriteText("data/round_counter.txt", " 7 \n"));
        w.LoadConfig(); // ConfigLoaded runs after the configuration loaded (game/world.dm:134, :136)
        Waiting.NoSleep(Dm.world.ConfigLoaded());
        Assert.True(Dm.eq(GLOB.restart_counter, 7));
        Assert.False(w.Files.Exists("data/round_counter.txt"));
    }
}
