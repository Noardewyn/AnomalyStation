using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M0Acceptance;

/// <summary>
/// The precondition of every M0 acceptance clause (ROADMAP M0, "Done when": "a headless test world"): the world the clauses
/// run on is the real booted one, so that none of them passes on a fixture. Genesis ran (the Master, the logger, GLOB),
/// <c>world/New</c> ran and handed the Master its initialization, the Master initialized every subsystem, the toy room stands
/// in tg's own types, and the ticker subsystems fire every tick once the run level is the game's.
/// </summary>
public class BootedWorldTests
{
    [Fact]
    public void TheAcceptanceWorldIsBootedByGenesisAndWorldNewWithTheRealMasterInitialized()
    {
        var w = new BootedToyWorld();

        w.In(() =>
        {
            Assert.Equal(30, w.World.Tick);
            Assert.NotNull(logger);          // Genesis (code/genesis_call.dme) made the logger, the Master and GLOB
            Assert.NotNull(Master);
            Assert.NotNull(GLOB__var);
            Assert.True(Dm.truthy(config.loaded));   // world/New loaded the configuration
            Assert.True(Dm.eq(datum_controller_master.init_stage_completed, INITSTAGE_MAX));   // master.dm:446
            Assert.All(Dm.each_cast<datum_controller_subsystem>(Master.subsystems), s => Assert.True(Dm.truthy(s.initialized) || Dm.truthy(Dm.bitand(s.ss_flags, SS_NO_INIT)), s.GetType().Name));
            Assert.Contains(w.Room.Log.Lines, l => l.Contains("World loaded at", StringComparison.Ordinal));
            Assert.Contains(w.Room.Log.Lines, l => l.Contains("Initializations complete within", StringComparison.Ordinal));
        });
        Assert.Empty(w.Runtimes);
    }

    [Fact]
    public void TheToyRoomIsTgsOwnTurfsAndAreaAndEverySubsystemOfTheRoomIsInitialized()
    {
        var w = new BootedToyWorld();

        w.In(() =>
        {
            Assert.IsType<area_misc_testroom>(w.Room.Room);
            Assert.IsType<turf_closed_indestructible>(locate(1, 1, 1));    // the rim
            Assert.IsType<turf_open_indestructible>(locate(3, 3, 1));      // the floor
            Assert.Same(w.Room.Room, locate(3, 3, 1).loc);
            Assert.True(Dm.truthy(SSatoms.initialized));                   // the room's atoms were initialized by SSatoms, not by a fixture
            Assert.True(Dm.truthy(Dm.bitand(locate(3, 3, 1).flags_1, INITIALIZED_1)));
            Assert.True(Dm.truthy(Dm.bitand(w.Room.Room.flags_1, INITIALIZED_1)));
        });
    }

    [Fact]
    public void TheTickerSubsystemsFireEveryTickAtTheGameRunLevel()
    {
        var w = new BootedToyWorld();
        float timed = w.In(() => SStimed_actions.times_fired);
        float timer = w.In(() => SStimer.times_fired);

        w.World.Step(10);

        w.In(() =>
        {
            Assert.True(Dm.eq(Master.current_runlevel, 3));   // SetRunLevel(RUNLEVEL_GAME = 4) stores log2(4) + 1 (master.dm:566)
        });
        Assert.True(w.MasterIsRunning());
        Assert.InRange(w.In(() => SStimed_actions.times_fired) - timed, 9, 10);   // a SS_TICKER with wait = 1: once per tick
        Assert.InRange(w.In(() => SStimer.times_fired) - timer, 8, 10);
        Assert.Empty(w.Runtimes);
    }
}
