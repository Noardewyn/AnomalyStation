using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Wave3.Mc;

/// <summary>
/// Run levels (master.dm:554-562 SetRunLevel, Loop's runlevel lists :596-642 and its rescheduling on a change :704-722):
/// controllers.md section 11, golden test f, and the quirks of Loop's first next_fire.
/// </summary>
public class McRunlevelTests
{
    private static Action<McFixtureSubsystem> Fires(float flags, float wait, float runlevels, float init_stage = INITSTAGE_LAST)
        => ss =>
        {
            ss.ss_flags = Dm.bitor(flags, SS_NO_INIT);
            ss.wait = wait;
            ss.init_stage = init_stage;
            ss.runlevels = runlevels;
        };

    [Fact]
    public void GoldenF_ANonBackgroundTickerIgnoresRunlevelsAndTheOthersJoinWhenTheirRunlevelStarts()
    {
        var w = new McWorld();
        var ticker = w.Add<datum_controller_subsystem_test_mc_a>("Game Ticker", Fires(SS_TICKER, 1, RUNLEVEL_GAME));
        var bg_ticker = w.Add<datum_controller_subsystem_test_mc_b>("Game BG Ticker", Fires(Dm.bitor(SS_TICKER, SS_BACKGROUND), 1, RUNLEVEL_GAME));
        var game = w.Add<datum_controller_subsystem_test_mc_c>("Game Only", Fires(NONE, 10, RUNLEVEL_GAME));
        var lobby = w.Add<datum_controller_subsystem_test_mc_d>("Lobby Only", Fires(NONE, 10, RUNLEVEL_LOBBY));
        var everywhere = w.Add<datum_controller_subsystem_test_mc_e>("Everywhere", Fires(NONE, 10, Dm.bitor(RUNLEVEL_LOBBY, RUNLEVELS_DEFAULT)));
        w.Boot(); // Initialize sets runlevel 1, the lobby (master.dm:448-449)
        w.Step(100);
        using (w.World.Enter())
        {
            Assert.True(Dm.eq(w.Mc.current_runlevel, 1));
            w.Mc.SetRunLevel(RUNLEVEL_GAME); // what SSticker does (scope R6)
        }
        w.Step(100); // to tick 200

        // the ticker is added to every runlevel list (master.dm:607-612, :639)
        Assert.InRange(ticker.fires[0], 1, 2);
        Assert.Equal(McCadenceTests.Every(ticker.fires[0], 1, 200), ticker.fires);
        // a background ticker goes through the runlevel lists; on the change it gets a random offset of
        // TICKS2DS(rand(0, DS2TICKS(min(TICKS2DS(wait), 2 SECONDS)))) = 0 or 1 tick (master.dm:716-722)
        Assert.InRange(bg_ticker.fires[0], 101, 102);
        Assert.Equal(McCadenceTests.Every(bg_ticker.fires[0], 1, 200), bg_ticker.fires);
        // a new subsystem behind its next_fire gets 0 to 20 ticks (min(wait, 2 SECONDS) = 10 ds)
        Assert.InRange(game.fires[0], 101, 121);
        Assert.Equal(McCadenceTests.Every(game.fires[0], 20, 200), game.fires);
        Assert.Equal(new long[] { 1, 21, 41, 61, 81 }, lobby.fires);
        // in the old list too: not rescheduled (:714)
        Assert.Equal(McCadenceTests.Every(1, 20, 200), everywhere.fires);
        Assert.Empty(w.DmRuntimes);
    }

    [Fact]
    public void TgQuirk_LoopTestsTheRunlevelIndexAsAMaskWhenItOffsetsTheFirstFireOfThePreviousStage()
    {
        // master.dm:616 `SS.runlevels & current_runlevel`: current_runlevel is an index (log(2, flag) + 1, :558), 3 for
        // RUNLEVEL_GAME, so the test reads the LOBBY and SETUP bits. A GAME-only main-stage subsystem gets no random offset
        // and fires at once in Loop(4); a SETUP|GAME one gets 0 to 40 ticks.
        var game_first = new List<long>();
        var setup_game_first = new List<long>();
        for (ulong seed = 1; seed <= 10; seed++)
        {
            var w = new McWorld(seed);
            var game = w.Add<datum_controller_subsystem_test_mc_a>("Game Main", Fires(NONE, 20, RUNLEVEL_GAME, INITSTAGE_MAIN));
            var setup_game = w.Add<datum_controller_subsystem_test_mc_b>("Setup Game Main", Fires(NONE, 20, Dm.bitor(RUNLEVEL_SETUP, RUNLEVEL_GAME), INITSTAGE_MAIN));
            w.StartAllStagesInitialized(RUNLEVEL_GAME); // Loop(4)'s first iteration, at tick 0
            w.Step(50);
            game_first.Add(game.fires[0]);
            setup_game_first.Add(setup_game.fires[0]);
            Assert.Empty(w.DmRuntimes);
        }
        Assert.All(game_first, t => Assert.Equal(0, t));
        Assert.All(setup_game_first, t => Assert.InRange(t, 0, 40));
        Assert.Contains(setup_game_first, t => t != 0);
    }

    [Fact]
    public void ASubsystemWithoutRunlevelsIsInNoListAndNeverFires()
    {
        var w = new McWorld();
        var none = w.Add<datum_controller_subsystem_test_mc_a>("No Runlevels", Fires(NONE, 10, 0));
        var plain = w.Add<datum_controller_subsystem_test_mc_b>("Plain", Fires(NONE, 10, RUNLEVEL_LOBBY));
        w.StartAllStagesInitialized();
        w.Step(60);
        Assert.Empty(none.fires); // master.dm:631-632 warns, Loop adds it nowhere
        Assert.Equal(new long[] { 0, 20, 40, 60 }, plain.fires);
        Assert.Empty(w.DmRuntimes);
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(0.5f)]
    public void SetRunLevelBelowTheLobbyFlagCrashesAndKeepsTheRunlevel(float flag)
    {
        var w = new McWorld();
        using (w.World.Enter())
        {
            w.Mc.SetRunLevel(RUNLEVEL_POSTGAME);
            if (flag == 1f)
            {
                w.Mc.SetRunLevel(flag); // log(2, 1) + 1 == 1: the lobby, valid
                Assert.True(Dm.eq(w.Mc.current_runlevel, 1));
                return;
            }
            DmCrashException e = Assert.Throws<DmCrashException>(() => w.Mc.SetRunLevel(flag)); // log(2, 0.5) + 1 == 0
            Assert.Equal("Attempted to set invalid runlevel: 0.5", e.Message);
            Assert.True(Dm.eq(w.Mc.current_runlevel, 4));
        }
    }
}
