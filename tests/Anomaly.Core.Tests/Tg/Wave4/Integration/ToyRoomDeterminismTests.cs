using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.Wave4.Mob;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tests.Tg.Wave4.Integration.Toy;

namespace Anomaly.Core.Tests.Tg.Wave4.Integration;

/// <summary>
/// Determinism on the toy room (W4.M item 7): the same scenario twice, in two worlds of one seed, gives equal
/// <c>World.ComputeStateHash()</c> at every sampled tick; a different seed or a different scenario gives another one. The
/// scenarios use everything the wave added: steps (the real Move chain), a bump into a wall and into a crate, a diagonal
/// step, a forceMove, a qdel, a mob's do_after that a step cancels and the do_after of another that completes (the real
/// Master polling SStimed_actions).
/// </summary>
public class ToyRoomDeterminismTests
{
    private sealed class Run
    {
        public readonly TgToyRoom Room;
        public readonly SortedDictionary<long, ulong> Hashes = new();
        public readonly List<string> Results = new();
        private readonly long t;

        public Run(ulong seed, bool cancel_with_step = true, bool extra_move = false)
        {
            Room = TgToyRoom.Boot(seed: seed);
            mob doer = null, bystander = null, walker = null;
            obj target = null;
            DoAfterCall call_cancelled = null, call_completed = null;
            Room.In(() =>
            {
                doer = Dm.@new<mob>(locate(3, 3, 1));
                bystander = Dm.@new<mob>(locate(5, 5, 1));
                walker = Dm.@new<mob>(locate(2, 2, 1));
                target = Dm.@new<obj>(locate(3, 3, 1));
                Dm.@new<obj>(locate(5, 2, 1)).density = TRUE;   // a crate to bump
            });
            t = Room.World.Tick + 1;
            Room.World.AddPhase("commands", w =>
            {
                long at = w.Tick - t;
                switch (at)
                {
                    case 0:
                        call_cancelled = DoAfterCall.Start(() => do_after(doer, 1 * SECONDS, target));
                        call_completed = DoAfterCall.Start(() => do_after(bystander, 5, target));
                        Results.Add("walk " + Dm.str(Waiting.NoSleep(step(walker, EAST))));
                        break;
                    case 2:
                        Results.Add("walk " + Dm.str(Waiting.NoSleep(step(walker, EAST))));
                        break;
                    case 3:
                        Results.Add("bump " + Dm.str(Waiting.NoSleep(step(walker, EAST))));   // the crate at (5, 2)
                        break;
                    case 4:
                        Results.Add("wall " + Dm.str(Waiting.NoSleep(step(walker, SOUTH))));  // the wall at (4, 1)
                        break;
                    case 5:
                        if (cancel_with_step)
                            Results.Add("cancel " + Dm.str(Waiting.NoSleep(step(doer, EAST))));
                        break;
                    case 8:
                        Results.Add("diag " + Dm.str(Waiting.NoSleep(step(walker, NORTHWEST))));
                        break;
                    case 10:
                        Results.Add("force " + Dm.str(Waiting.NoSleep(walker.forceMove(locate(6, 6, 1)))));
                        if (extra_move)
                            qdel(bystander);
                        break;
                }
                // the results of the do_afters, as soon as they are known
                if (call_cancelled?.Done == true && !Results.Contains("cancelled " + Dm.str(call_cancelled.Result)))
                    Results.Add("cancelled " + Dm.str(call_cancelled.Result));
                if (call_completed?.Done == true && !Results.Contains("completed " + Dm.str(call_completed.Result)))
                    Results.Add("completed " + Dm.str(call_completed.Result));
            });
            for (int i = 0; i <= 45; i++)
            {
                if (i % 3 == 0 || i == 5 || i == 6 || i == 11)
                    Hashes[Room.World.Tick] = Room.World.ComputeStateHash();
                Room.World.Step();
            }
        }
    }

    [Fact]
    public void TheSameScenarioTwiceInTwoWorldsOfOneSeedGivesEqualStateHashesAtEveryTick()
    {
        var a = new Run(11);
        var b = new Run(11);

        Assert.Equal(a.Hashes.Keys, b.Hashes.Keys);
        foreach (long tick in a.Hashes.Keys)
            Assert.Equal(a.Hashes[tick], b.Hashes[tick]);
        Assert.Equal(a.Results, b.Results);
        Assert.Empty(a.Room.Runtimes);
        Assert.Empty(b.Room.Runtimes);
    }

    [Fact]
    public void TheScenarioDoesWhatItSaysTheStepsMoveTheBumpFailsAndTheCancelledDoAfterIsFalse()
    {
        var run = new Run(11);

        Assert.Equal(new[]
        {
            "walk 1",          // (2,2) -> (3,2)
            "walk 1",          // (3,2) -> (4,2)
            "bump 0",          // the crate at (5,2): a failed Move
            "wall 0",          // the wall at (4,1)
            "cancel 1",        // the doer's step cancels its do_after
            "diag 1",          // NORTHWEST from (4,2): north is the doer (a dense mob), so west first, then north: (3,2), (3,3)
            "force 1",
            "cancelled 0",     // FALSE, one tick after the step
            "completed 1",     // the bystander's 5 ds do_after
        }.OrderBy(x => x), run.Results.OrderBy(x => x));
        Assert.Empty(run.Room.Runtimes);
    }

    [Fact]
    public void ADifferentSeedADifferentScenarioOrACancelThatDoesNotHappenGivesAnotherHash()
    {
        var baseline = new Run(11);
        var other_seed = new Run(12);
        var no_cancel = new Run(11, cancel_with_step: false);
        var more = new Run(11, extra_move: true);

        long last = baseline.Hashes.Keys.Max();
        Assert.NotEqual(baseline.Hashes[last], other_seed.Hashes[last]);   // mobs draw their nutrition from the world RNG
        Assert.NotEqual(baseline.Hashes[last], no_cancel.Hashes[last]);    // the doer did not move, its do_after completed
        Assert.NotEqual(baseline.Hashes[last], more.Hashes[last]);         // the bystander is gone
        // and before anything differs, the same seed gives the same hash
        long first = baseline.Hashes.Keys.Min();
        Assert.Equal(baseline.Hashes[first], no_cancel.Hashes[first]);
        Assert.Equal(baseline.Hashes[first], more.Hashes[first]);
    }

    [Fact]
    public void TheHashSeesWhereThingsAreAndTheirContentsOrder()
    {
        // a world that does not boot: two movers swap their arrival order on a turf
        static ulong Hash(bool first_a)
        {
            var room = TgToyRoom.Create(seed: 3);
            using (room.World.Enter())
            {
                var a = Thing("a", locate(3, 3, 1));
                var b = Thing("b", locate(3, 4, 1));
                Force(first_a ? a : b, locate(5, 5, 1));
                Force(first_a ? b : a, locate(5, 5, 1));
            }
            return room.World.ComputeStateHash();
        }

        Assert.Equal(Hash(true), Hash(true));
        Assert.NotEqual(Hash(true), Hash(false));
    }

    [Fact]
    public void ASceneOfStepsABumpAForceMoveAndAQdelInAWorldThatDoesNotBootIsDeterministicToo()
    {
        static ulong Scene(ulong seed)
        {
            var room = TgToyRoom.Create(seed: seed);
            using (room.World.Enter())
            {
                var m = Dm.@new<mob>(locate(2, 2, 1));
                var crate = Thing("crate", locate(4, 2, 1), dense: true);
                Step(m, EAST);
                Step(m, EAST);   // bumps the crate
                Step(m, NORTHEAST);
                Force(crate, m.loc);
                Step(m, EAST);
                qdel(crate);
                Assert.Empty(room.Runtimes);
            }
            room.World.Step(10);
            return room.World.ComputeStateHash();
        }

        Assert.Equal(Scene(4), Scene(4));
        Assert.NotEqual(Scene(4), Scene(5));
    }
}
