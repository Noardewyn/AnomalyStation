using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.Wave4.Integration;
using Anomaly.Core.Tests.Tg.Wave4.Mob;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M0Acceptance;

/// <summary>
/// ROADMAP M0, "Done when", clause 3: "the same scenario run twice gives an identical state hash". The scenario is the whole of
/// clauses 1 and 2 at once on a booted world (<see cref="BootedToyWorld"/>): steps of mobs and objs through the Move chain, a
/// bump into a wall and into a crate, a diagonal step, a forceMove, a new atom at a place the world RNG picks, a qdel, a mob that
/// walks at random (the world RNG picks each direction), a do_after that completes and one that a step cancels, with the real
/// Master and the ticker subsystems firing in every tick. It is run twice in two worlds of one seed, and
/// <see cref="World.ComputeStateHash"/> is compared at many ticks, not only the last one: three of the boot ticks, every tick of
/// the busy first 12 ticks of the scenario and five later ones up to the idle end (<see cref="Sampled"/>); the results the
/// commands saw are compared too.
/// <para>
/// A hash that does not see the scenario would pass this vacuously, so the other tests show that it does: a different seed, and
/// a scenario that differs by one command, give a different hash, from the tick of the difference on and not before.
/// </para>
/// <para>
/// The hash is expensive (it forces two full garbage collections so that two runs hash the same set of objects, about 25 ms), so
/// the ticks are sampled, and the five scenarios the tests only read from are run once and shared by the tests of the class.
/// </para>
/// </summary>
public class Clause3DeterminismTests
{
    /// <summary>Ticks after the first command the scenario is run for: its commands end at T + 41, the rest is idle (SSgarbage, timers).</summary>
    private const int Length = 50;

    /// <summary>The tick of the first command: the boot takes 30 ticks (Master.Initialize sleeps 10 ds), so the scenario starts at 31.</summary>
    private const long FirstCommandTick = 31;

    /// <summary>The ticks whose state hash is taken: boot ticks 10, 20 (the Master initializes every subsystem) and 30, every tick of T to T + 12, then T + 14, 20, 30, 41 and 50.</summary>
    private static bool Sampled(long tick)
    {
        long at = tick - FirstCommandTick;
        return tick is 10 or 20 or 30 || at is >= 0 and <= 12 || at is 14 or 20 or 30 or 41 or 50;
    }

    private static readonly Lazy<Scenario> Seed11 = new(() => new Scenario(11).Run());
    private static readonly Lazy<Scenario> Seed11Again = new(() => new Scenario(11).Run());
    private static readonly Lazy<Scenario> Seed12 = new(() => new Scenario(12).Run());
    private static readonly Lazy<Scenario> Seed11NoCancel = new(() => new Scenario(11, cancel_with_step: false).Run());
    private static readonly Lazy<Scenario> Seed11ExtraStep = new(() => new Scenario(11, extra_step: true).Run());

    internal sealed class Scenario
    {
        public readonly BootedToyWorld W;
        public readonly SortedDictionary<long, ulong> Hashes = new();
        public readonly List<string> Results = new();
        private readonly List<DoAfterCall> _calls = new();

        public Scenario(ulong seed, bool cancel_with_step = true, bool extra_step = false)
        {
            W = new BootedToyWorld(seed, probe: w =>
            {
                if (Sampled(w.Tick))
                    Hashes[w.Tick] = w.ComputeStateHash();
            });
            Assert.Equal(FirstCommandTick, W.T);
            mob doer = null, bystander = null, walker = null, drunk = null;
            obj target = null, other_target = null;
            W.In(() =>
            {
                doer = BootedToyWorld.Mob("doer", 3, 3);
                bystander = BootedToyWorld.Mob("bystander", 5, 5);
                walker = BootedToyWorld.Mob("walker", 2, 2);
                drunk = BootedToyWorld.Mob("drunk", 4, 4);
                target = BootedToyWorld.Obj("target", 3, 3);
                other_target = BootedToyWorld.Obj("other target", 5, 5);
                BootedToyWorld.Obj("crate", 5, 2, dense: true);   // to bump
            });

            void Say(string what, object value) => Results.Add($"{W.World.Tick - W.T} {what} {Dm.str(value)}");

            W.At(0, () =>
            {
                _calls.Add(DoAfterCall.Start(() => do_after(doer, 1 * SECONDS, target)));              // a step will cancel it
                _calls.Add(DoAfterCall.Start(() => do_after(bystander, 5, other_target)));             // completes
                Say("walk", Toy.Step(walker, EAST));
            });
            W.At(2, () => Say("walk", Toy.Step(walker, EAST)));
            W.At(3, () =>
            {
                Say("bump", Toy.Step(walker, EAST));                                                    // the crate at (5, 2)
                // a new atom at a place the world RNG picks: the draw is part of the state, so it must be the same in both worlds
                obj thing = Dm.@new<obj>(locate(rand(2, 6), rand(2, 6), 1));
                Say("new", thing.x * 10 + thing.y);
            });
            W.At(4, () => Say("wall", Toy.Step(walker, SOUTH)));                                        // the wall at (4, 1)
            W.At(5, () =>
            {
                if (cancel_with_step)
                    Say("cancel", Toy.Step(doer, EAST));
            });
            W.At(8, () => Say("diag", Toy.Step(walker, NORTHWEST)));
            W.At(10, () =>
            {
                Say("force", Toy.Force(walker, locate(6, 6, 1)));
                if (extra_step)
                    Say("extra", Toy.Step(bystander, NORTH));
            });
            W.At(12, () => qdel(other_target));                                                         // SSgarbage takes it from here
            for (int k = 14; k <= 41; k++)
            {
                long at = k;
                W.At(at, () => Say("drunk", Toy.Step(drunk, Dm.num(Dm.pick(NORTH, SOUTH, EAST, WEST, NORTHEAST, NORTHWEST, SOUTHEAST, SOUTHWEST)))));
            }
        }

        public Scenario Run()
        {
            W.RunTo(Length);
            for (int i = 0; i < _calls.Count; i++)
                Results.Add($"do_after {i} {(_calls[i].Done ? Dm.str(_calls[i].Result) + "@" + (_calls[i].DoneTick - W.T) : "pending")}");
            return this;
        }

        public long Last => Hashes.Keys.Max();
    }

    // ---- the same scenario twice ----

    [Fact]
    public void TheSameScenarioTwiceInTwoWorldsOfOneSeedGivesEqualStateHashesAtManyTicksBootIncluded()
    {
        Scenario a = Seed11.Value;
        Scenario b = Seed11Again.Value;

        Assert.NotSame(a.W.World, b.W.World);
        Assert.Equal(a.Hashes.Keys, b.Hashes.Keys);
        Assert.Equal(new long[] { 10, 20, 30 }, a.Hashes.Keys.Where(t => t <= 30));   // the boot, the Master's initialization included
        Assert.Equal(a.W.T + Length, a.Last);                                           // up to the idle end
        Assert.Equal(21, a.Hashes.Count);
        foreach (long tick in a.Hashes.Keys)
            Assert.True(a.Hashes[tick] == b.Hashes[tick], $"the state hashes differ at tick {tick}");
        Assert.Equal(a.Results, b.Results);
        Assert.Equal(a.W.World.ComputeStateHash(), b.W.World.ComputeStateHash());
        Assert.Empty(a.W.Runtimes);
        Assert.Empty(b.W.Runtimes);
    }

    [Fact]
    public void TheScenarioDoesWhatItSaysSoTheTwoRunsAgreeOnSomethingReal()
    {
        Scenario a = Seed11.Value;

        Assert.Equal(new[]
        {
            "0 walk 1",         // (2,2) -> (3,2)
            "2 walk 1",         // (3,2) -> (4,2)
            "3 bump 0",         // the crate at (5,2): a failed Move
            "4 wall 0",         // the wall at (4,1)
            "5 cancel 1",       // the doer's step east: it cancels its do_after
            "8 diag 1",         // NORTHWEST from (4,2): north is the doer's new tile (a dense mob), so west, then north
            "10 force 1",
        }, a.Results.Where(r => !r.Contains(" new ") && !r.Contains(" drunk ") && !r.StartsWith("do_after")));
        Assert.Contains("do_after 0 0@6", a.Results);    // FALSE, one tick after the step
        Assert.Contains("do_after 1 1@10", a.Results);   // TRUE after 5 ds
        Assert.Equal(28, a.Results.Count(r => r.Contains(" drunk ")));
        Assert.Contains(a.Results, r => r.Contains(" drunk 1"));   // the random walk moved at least once
        Assert.Empty(a.W.Runtimes);
    }

    [Fact]
    public void TwoWorldsOfOneSeedSteppedInLockstepAgreeAtEveryTickSoNoStateIsSharedBetweenWorlds()
    {
        // interleaved, one tick of A then one tick of B, with the commands of both running between: a static or a shared cache
        // that leaked from one world to the other would split them
        Scenario a = new(23);
        Scenario b = new(23);
        while (a.W.World.Tick < a.W.T + Length)
        {
            a.W.World.Step();
            b.W.World.Step();
        }
        a.Run();
        b.Run();

        Assert.Equal(21, a.Hashes.Count);
        foreach (long tick in a.Hashes.Keys)
            Assert.True(a.Hashes[tick] == b.Hashes[tick], $"the worlds diverge at tick {tick}");
        Assert.Equal(a.Results, b.Results);
        Assert.Empty(a.W.Runtimes);
        Assert.Empty(b.W.Runtimes);
    }

    [Fact]
    public void ARunSteppedInChunksOfSevenTicksGivesTheSameHashesAsOneSteppedATickAtATime()
    {
        Scenario chunked = new(11);
        long end = chunked.W.T + Length;
        while (chunked.W.World.Tick < end)
            chunked.W.World.Step((int)Math.Min(7, end - chunked.W.World.Tick));
        chunked.Run();

        // the probe runs inside every tick however the host steps, so the hashes are those of the same states
        Assert.Equal(Seed11.Value.Hashes, chunked.Hashes);
        Assert.Equal(Seed11.Value.Results, chunked.Results);
    }

    // ---- the hash sees the scenario ----

    [Fact]
    public void ADifferentSeedGivesADifferentHashAndADifferentRandomWalk()
    {
        Scenario a = Seed11.Value;
        Scenario b = Seed12.Value;

        Assert.NotEqual(a.Hashes[a.Last], b.Hashes[b.Last]);
        Assert.NotEqual(a.Results.Where(r => r.Contains(" drunk ")).ToArray(), b.Results.Where(r => r.Contains(" drunk ")).ToArray());   // the world RNG picks the walk
        Assert.NotEqual(a.Results.First(r => r.Contains(" new ")), b.Results.First(r => r.Contains(" new ")));                        // and the place of the new atom
        Assert.Empty(b.W.Runtimes);
    }

    [Fact]
    public void AScenarioThatLacksOneCommandHasTheSameHashUntilThatTickAndAnotherFromThenOn()
    {
        Scenario with_cancel = Seed11.Value;
        Scenario without = Seed11NoCancel.Value;
        long cancel_tick = with_cancel.W.T + 5;

        Assert.Contains(with_cancel.Hashes.Keys, t => t < cancel_tick);
        foreach (long tick in with_cancel.Hashes.Keys.Where(t => t < cancel_tick))
            Assert.True(with_cancel.Hashes[tick] == without.Hashes[tick], $"the hashes differ at tick {tick}, before the first different command");
        foreach (long tick in with_cancel.Hashes.Keys.Where(t => t >= cancel_tick))
            Assert.True(with_cancel.Hashes[tick] != without.Hashes[tick], $"the hashes are equal at tick {tick}, after the step");   // the doer is somewhere else for good
        Assert.Contains("do_after 0 1@20", without.Results);   // the doer did not move and its do_after completed
        Assert.Empty(without.W.Runtimes);
    }

    [Fact]
    public void ADifferentAtomMovedInOneTickOfTheSameSeedChangesTheHashFromThatTickOn()
    {
        Scenario baseline = Seed11.Value;
        Scenario extra = Seed11ExtraStep.Value;   // the bystander also steps north in tick T + 10
        long tick10 = baseline.W.T + 10;

        Assert.Equal(baseline.Hashes[tick10 - 1], extra.Hashes[tick10 - 1]);
        Assert.NotEqual(baseline.Hashes[tick10], extra.Hashes[tick10]);
        Assert.NotEqual(baseline.Hashes[baseline.Last], extra.Hashes[extra.Last]);
    }
}
