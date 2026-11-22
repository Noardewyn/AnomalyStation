using Anomaly.Core.Tg;

namespace Anomaly.Core.Tests.ToyWorld;

/// <summary>
/// What the scripted M0 scenario (<see cref="ToyScenario"/>) logs, against the order tg sends its signals in. The expected
/// order is written from docs/ai/specs/move-chain.md section 3 (3a: COMSIG_MOVABLE_PRE_MOVE is sent after <c>Enter</c> and before
/// the point of no return, COMSIG_MOVABLE_MOVED at the end of a successful move; 3b and 3c: a blocked <c>Enter</c> bumps; 3g: a
/// failed move sent no PRE_MOVE and no MOVED) and from docs/ai/specs/m0-reading/timed-actions-helpers.md (BEGAN when the action
/// starts, ENDED when the caller gets its result, on failure too), not from the host code. A tick is written as its offset
/// from <see cref="ToyScenario.StartTick"/>, the tick of the first command.
/// </summary>
public class ToyScenarioLogTests
{
    private static string Line(ToyScenario s, ToyLogEntry e) => $"+{e.Tick - s.StartTick} {e.Name}";

    private static string[] Lines(ToyScenario s) => s.Log.Select(e => Line(s, e)).ToArray();

    [Fact]
    public void TheLogIsTgsOrderOfEventsOnTheScriptedTicks()
    {
        var s = ToyScenario.Run();

        Assert.Equal(new[]
        {
            "+0 command", "+0 pre_move", "+0 moved",                       // a step east: PRE_MOVE, then MOVED
            "+2 command", "+2 pre_move", "+2 moved",                       // and another
            "+4 command", "+4 bump",                                       // the step into the wall: Bump, nothing else
            "+6 command", "+6 do_after_began",                             // a do_after starts: BEGAN
            "+26 do_after_ended", "+26 do_after_result",                   // one second (20 ticks) later: ENDED, then the caller's result
            "+30 command", "+30 do_after_began",
            "+35 command", "+35 pre_move", "+35 moved",                    // the step that cancels it
            "+36 do_after_ended", "+36 do_after_result",                   // one tick later the caller learns
        }, Lines(s));
        Assert.Empty(s.Runtimes);
    }

    [Fact]
    public void TheScriptConstantsAreTheTicksOfTheCommands()
    {
        var s = ToyScenario.Run();

        long[] commands = s.Log.Where(e => e.Name == "command").Select(e => e.Tick - s.StartTick).ToArray();
        Assert.Equal(new long[]
        {
            ToyScenario.FirstStepAt, ToyScenario.SecondStepAt, ToyScenario.WallStepAt,
            ToyScenario.FirstDoAfterAt, ToyScenario.SecondDoAfterAt, ToyScenario.CancelStepAt,
        }, commands);
        Assert.Equal(new long[] { 0, 2, 4, 6, 30, 35 }, commands);
        Assert.True(s.World.Tick >= s.StartTick + ToyScenario.EndAt);
    }

    [Fact]
    public void PreMoveComesBeforeMovedForEverySuccessfulStep()
    {
        var s = ToyScenario.Run();

        foreach (long at in new long[] { ToyScenario.FirstStepAt, ToyScenario.SecondStepAt, ToyScenario.CancelStepAt })
        {
            string[] names = s.Log.Where(e => e.Tick == s.StartTick + at).Select(e => e.Name).ToArray();
            int pre = Array.IndexOf(names, "pre_move"), moved = Array.IndexOf(names, "moved");
            Assert.True(pre >= 0 && moved > pre, $"step at +{at}: PRE_MOVE must come before MOVED, got [{string.Join(", ", names)}]");
            Assert.Equal(1, names.Count(n => n == "pre_move"));
            Assert.Equal(1, names.Count(n => n == "moved"));
        }
    }

    [Fact]
    public void TheStepsMoveOneTileEastAndMovedCarriesTheDirection()
    {
        var s = ToyScenario.Run();

        ToyLogEntry[] pre = s.Log.Where(e => e.Name == "pre_move").ToArray();
        ToyLogEntry[] moved = s.Log.Where(e => e.Name == "moved").ToArray();
        // PRE_MOVE: the mob is still on the old turf (source loc) and the argument is the new one.
        Assert.Equal(new[] { "(2,2) -> (3,2)", "(3,2) -> (4,2)", "(4,2) -> (5,2)" }, pre.Select(e => e.Detail));
        // MOVED: the old loc, then (the mob is on the new turf) the direction of the move.
        Assert.Equal(new[] { "(2,2) -> (3,2) dir EAST", "(3,2) -> (4,2) dir EAST", "(4,2) -> (5,2) dir EAST" }, moved.Select(e => e.Detail));
    }

    [Fact]
    public void TheStepIntoTheWallFailsBumpsTheWallAndSendsNoPreMoveAndNoMoved()
    {
        var s = ToyScenario.Run();

        ToyLogEntry[] atWall = s.Log.Where(e => e.Tick == s.StartTick + ToyScenario.WallStepAt).ToArray();
        Assert.Equal(new[] { "command", "bump" }, atWall.Select(e => e.Name));
        Assert.Equal("step SOUTH -> FALSE", atWall[0].Detail);
        Assert.Equal("wall(4,1) from (4,2)", atWall[1].Detail);   // the rim wall south of the mob, which stays where it was
        Assert.Single(s.Log, e => e.Name == "bump");              // and nothing else bumped
    }

    [Fact]
    public void TheCommandsReportWhatStepReturned()
    {
        var s = ToyScenario.Run();

        Assert.Equal(new[]
        {
            "step EAST -> TRUE", "step EAST -> TRUE", "step SOUTH -> FALSE",
            "do_after #1 of 1 second on the obj", "do_after #2 of 1 second on the obj", "step EAST -> TRUE",
        }, s.Log.Where(e => e.Name == "command").Select(e => e.Detail));
    }

    [Fact]
    public void ABeganAndAnEndedFrameEveryDoAfterInOrder()
    {
        var s = ToyScenario.Run();

        Assert.Equal(new[] { "#1", "#2" }, s.Log.Where(e => e.Name == "do_after_began").Select(e => e.Detail));
        Assert.Equal(new[] { "#1", "#2" }, s.Log.Where(e => e.Name == "do_after_ended").Select(e => e.Detail));
        // an action ends before its caller has the result, and the second starts after the first has ended
        string[] order = s.Log.Where(e => e.Name.StartsWith("do_after_", StringComparison.Ordinal)).Select(e => e.Name + e.Detail.Substring(0, 2)).ToArray();
        Assert.Equal(new[]
        {
            "do_after_began#1", "do_after_ended#1", "do_after_result#1",
            "do_after_began#2", "do_after_ended#2", "do_after_result#2",
        }, order);
    }

    [Fact]
    public void ALogLineReadsTheTickTheNameAndTheDetail()
    {
        var entry = new ToyLogEntry(31, "moved", "(2,2) -> (3,2) dir EAST");

        Assert.Equal("t31 moved (2,2) -> (3,2) dir EAST", entry.ToString());
    }

    // ---- the frames ----

    [Fact]
    public void ThereIsAFrameBeforeTheFirstCommandAndOneAfterEachCommand()
    {
        var s = ToyScenario.Run();

        Assert.Equal(new[] { "start", "step EAST", "step EAST", "step SOUTH", "do_after 1", "do_after 2", "step EAST" }, s.Frames.Select(f => f.Caption));
        Assert.Equal(s.StartTick - 1, s.Frames[0].Tick);
        Assert.Equal(new long[] { 0, 2, 4, 6, 30, 35 }, s.Frames.Skip(1).Select(f => f.Tick - s.StartTick));
    }

    [Fact]
    public void TheFramesDrawTheRoomWithWallsFloorTheObjAndTheMob()
    {
        var s = ToyScenario.Run();

        // the toy room is 7 x 7 with a wall rim, the floor from (2,2) to (6,6); the top row is the northernmost
        Assert.Equal(new[]
        {
            "#######",
            "#.....#",
            "#.....#",
            "#.....#",
            "#..o..#",   // the obj at (4,3)
            "#@....#",   // the mob at (2,2), its start
            "#######",
        }, s.Frames[0].Rows);
        // after the two steps east: at (4,2), under the obj
        Assert.Equal("#..@..#", s.Frames[2].Rows[5]);
        Assert.Equal("#..o..#", s.Frames[2].Rows[4]);
        // the failed step leaves the mob where it was
        Assert.Equal(s.Frames[2].Rows, s.Frames[3].Rows);
        // and the step that cancels the do_after moves it east once more
        Assert.Equal("#...@.#", s.Frames[6].Rows[5]);
        Assert.All(s.Frames, f => Assert.Equal(7, f.Rows.Count));
        Assert.All(s.Frames, f => Assert.All(f.Rows, r => Assert.Equal(7, r.Length)));
        Assert.All(s.Frames, f => Assert.Equal(1, f.Rows.Sum(r => r.Count(c => c == '@'))));
        Assert.All(s.Frames, f => Assert.Equal("#######", f.Rows[0]));
    }

    [Fact]
    public void TheFramesKeepTheMobsDirectionAFailedStepStillTurnsIt()
    {
        var s = ToyScenario.Run();

        // move-chain.md 3a step 5: the dir changes before Exit and Enter, so a failed move still turns the mover
        float[] dirs = s.Frames.Select(f => f.MobDir).ToArray();
        Assert.Equal(new float[] { 2, 4, 4, 2, 2, 2, 4 }, dirs);   // a new mob faces south; east, east; south into the wall; ...
    }
}
