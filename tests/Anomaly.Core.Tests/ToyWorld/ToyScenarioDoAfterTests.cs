using Anomaly.Core.DmShim;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tg.Defines;

namespace Anomaly.Core.Tests.ToyWorld;

/// <summary>
/// The two do_afters of the scripted M0 scenario, against docs/ai/specs/m0-reading/timed-actions-helpers.md sections 1, 2 and 8
/// and docs/ai/specs/waiting-model.md 7.3: one second is 10 ds, which is 20 ticks of 0.5 ds; a do_after started by a player
/// command completes (TRUE) 20 ticks later; a movement of the user cancels it (FALSE) and the awaiting caller learns one tick
/// after the cancelling step (the await wakes on its next poll). The result is the DM number FALSE (0), never null (null is the
/// interaction cap's bare <c>return</c>).
/// </summary>
public class ToyScenarioDoAfterTests
{
    [Fact]
    public void TheFirstDoAfterReturnsTrueTwentyTicksAfterItStarted()
    {
        var s = ToyScenario.Run();

        ToyDoAfter first = s.CompletedDoAfter;
        Assert.True(first.Done);
        Assert.True(first.Succeeded);
        Assert.NotNull(first.Result);
        Assert.True(Dm.eq(first.Result, Dm.TRUE));
        Assert.Equal(1, first.Number);
        Assert.Equal(s.StartTick + ToyScenario.FirstDoAfterAt, first.StartTick);
        Assert.Equal(first.StartTick + 20, first.EndTick);
        Assert.Equal(s.StartTick + ToyScenario.FirstDoAfterAt + ToyScenario.DoAfterTicks, first.EndTick);
    }

    [Fact]
    public void TheSecondDoAfterReturnsFalseOneTickAfterTheStepThatCancelsIt()
    {
        var s = ToyScenario.Run();

        ToyDoAfter second = s.CancelledDoAfter;
        Assert.True(second.Done);
        Assert.False(second.Succeeded);
        Assert.NotNull(second.Result);            // FALSE, not the bare null of the interaction cap
        Assert.True(Dm.eq(second.Result, Dm.FALSE));
        Assert.Equal(2, second.Number);
        Assert.Equal(s.StartTick + ToyScenario.SecondDoAfterAt, second.StartTick);
        Assert.Equal(5, ToyScenario.CancelStepAt - ToyScenario.SecondDoAfterAt);      // the step comes 5 ticks after the start
        Assert.Equal(second.StartTick + 5 + 1, second.EndTick);                       // and the caller learns one tick later
        Assert.Equal(s.StartTick + ToyScenario.CancelStepAt + 1, second.EndTick);
        Assert.True(second.EndTick < second.StartTick + ToyScenario.DoAfterTicks);    // long before its second was up
    }

    [Fact]
    public void TheSecondDoAfterStartsOnlyAfterTheFirstHasEnded()
    {
        var s = ToyScenario.Run();

        Assert.True(s.CancelledDoAfter.StartTick > s.CompletedDoAfter.EndTick);
    }

    [Fact]
    public void TheCancellingStepIsTheOnlyStepAfterTheSecondDoAfterStartedAndItMovedTheMob()
    {
        var s = ToyScenario.Run();

        long second_start = s.CancelledDoAfter.StartTick;
        ToyLogEntry[] after = s.Log.Where(e => e.Tick >= second_start && e.Name == "moved").ToArray();
        Assert.Single(after);
        Assert.Equal(second_start + 5, after[0].Tick);
        Assert.Equal("(4,2) -> (5,2) dir EAST", after[0].Detail);
        // the mob stood still for the first do_after (no movement signal between its start and its end)
        Assert.DoesNotContain(s.Log, e => e.Name is "moved" or "pre_move" or "bump" && e.Tick > s.CompletedDoAfter.StartTick && e.Tick <= s.CompletedDoAfter.EndTick);
    }

    [Fact]
    public void TheWorldHasRunToTheEndOfTheScriptWithNoRuntimeAndTheMasterRunning()
    {
        var s = ToyScenario.Run();

        Assert.Equal(s.StartTick + ToyScenario.EndAt, s.World.Tick);
        Assert.Empty(s.Runtimes);
        using (s.World.Enter())
        {
            Assert.True(Dm.eq(datum_controller_master.init_stage_completed, INITSTAGE_MAX));   // the real Master initialized the world
            // at the game run level, as SSticker would set it: SetRunLevel keeps the level as log2(flag) + 1 (master.dm:561)
            Assert.True(Dm.eq(GlobalProcs.Master.current_runlevel, 3));
            Assert.Equal(4, RUNLEVEL_GAME);
        }
        Assert.True(s.StartTick > 20);   // after Master.Initialize, which sleeps 10 ds before it initializes the subsystems
    }
}
