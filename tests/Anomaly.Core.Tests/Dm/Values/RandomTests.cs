using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;

namespace Anomaly.Core.Tests.DmValues;

/// <summary>
/// Randomness from the world RNG (docs/ai/translation-rules.md 4.13; D31): BYOND ref <c>/proc/prob</c>,
/// <c>/proc/rand</c>, <c>/proc/pick</c>, <c>/proc/roll</c>; OpenDream tests Builtins/prob and pick_list_empty. Every
/// draw comes from <c>World.Current.Rng</c>, so a world with the same seed at the same tick draws the same values.
/// </summary>
public class RandomTests
{
    private static T InWorld<T>(ulong seed, Func<T> body)
    {
        var w = new World(seed);
        using (w.Enter()) return body();
    }

    [Fact]
    public void ProbIsAPercentage()
    {
        // OpenDream test Builtins/prob: prob(0) never, prob(100) always, prob(0.1) about once in a thousand.
        InWorld(7, () =>
        {
            const int n = 100000;
            int zero = 0, tenth = 0, all = 0, half = 0;
            for (int i = 0; i < n; i++)
            {
                if (Dm.prob(0)) zero++;
                if (Dm.prob(0.1f)) tenth++;
                if (Dm.prob(100)) all++;
                if (Dm.prob(50)) half++;
            }
            Assert.Equal(0, zero);
            Assert.InRange(tenth, 1, n / 100 / 2 - 1);
            Assert.Equal(n, all);
            Assert.InRange(half, n * 45 / 100, n * 55 / 100);
            return 0;
        });
    }

    [Fact]
    public void RandGivesIntegersInclusiveOrAFraction()
    {
        // BYOND ref /proc/rand: "A random integer between L and H inclusive; or a random number from 0 up to 1".
        InWorld(3, () =>
        {
            var seen = new HashSet<float>();
            for (int i = 0; i < 2000; i++)
            {
                float d = Dm.rand(1, 6);
                Assert.Equal(MathF.Truncate(d), d);
                Assert.InRange(d, 1f, 6f);
                seen.Add(d);
                float h = Dm.rand(5);
                Assert.InRange(h, 0f, 5f);
                Assert.InRange(Dm.rand(6, 1), 1f, 6f);   // the order of the bounds does not matter
                float f = Dm.rand();
                Assert.True(f >= 0f && f < 1f);
            }
            Assert.Equal(6, seen.Count);
            Assert.Equal(4f, Dm.rand(4, 4));
            return 0;
        });
    }

    [Fact]
    public void TheSameSeedAndTickDrawTheSameValues()
    {
        // D31: the world RNG is reseeded every tick from (seed, tick).
        float[] Draws(ulong seed) => InWorld(seed, () => Enumerable.Range(0, 20).Select(_ => Dm.rand(1, 1000)).ToArray());
        Assert.Equal(Draws(42), Draws(42));
        Assert.NotEqual(Draws(42), Draws(43));
    }

    [Fact]
    public void PickChoosesAnArgumentOrAnItem()
    {
        // BYOND ref /proc/pick: "If only one argument is included and it is a list, then the item is chosen from that
        // list"; OpenDream test pick_list_empty (a runtime error).
        InWorld(5, () =>
        {
            var seen = new HashSet<object>();
            for (int i = 0; i < 500; i++)
            {
                object p = Dm.pick(1f, 2f, 3f);
                Assert.Contains(p, new object[] { 1f, 2f, 3f });
                seen.Add(p);
                Assert.Contains(Dm.pick(Dm.list("a", "b")), new object[] { "a", "b" });
            }
            Assert.Equal(3, seen.Count);
            Assert.True(Dm.eq(Dm.pick(1, 1d), 1f));     // any numeric box is the DM number
            ulong before = World.Current.Rng.State;
            Assert.Equal(5f, Dm.pick(5f));            // a single non-list argument, no draw
            Assert.Null(Dm.pick(null));
            Assert.Equal(before, World.Current.Rng.State);
            V.Error(() => Dm.pick(Dm.list()));
            Assert.Equal("only", Dm.pick(Dm.arglist(Dm.list("only"))));
            return 0;
        });
    }

    [Fact]
    public void WeightedPickFollowsTheWeights()
    {
        // BYOND ref /proc/pick: "A value for P of 200 makes it twice as likely as the norm"; rules 4.13: an unweighted
        // value weighs 100.
        InWorld(9, () =>
        {
            int brown = 0, green = 0;
            for (int i = 0; i < 20000; i++)
            {
                object p = Dm.pick_weighted((200, "brown"), (0, "never"), (100, "green"));
                Assert.NotEqual("never", p);
                if (Dm.eq(p, "brown")) brown++;
                else green++;
            }
            Assert.InRange(brown / (double)green, 1.8, 2.2);
            Assert.Null(Dm.pick_weighted((0, "a"), (-5, "b")));
            Assert.Equal("only", Dm.pick_weighted((1, "only")));
            return 0;
        });
    }

    [Fact]
    public void RollSumsDice()
    {
        // BYOND ref /proc/roll: "3d4+5 ... adds 5 to the sum of 3 dice having 4 sides each"; roll(ndice, sides).
        InWorld(11, () =>
        {
            var totals = new HashSet<float>();
            for (int i = 0; i < 3000; i++)
            {
                float r = Dm.roll("3d6");
                Assert.InRange(r, 3f, 18f);
                totals.Add(r);
                Assert.InRange(Dm.roll("3d4+5"), 8f, 17f);
                Assert.InRange(Dm.roll("2d6-1"), 1f, 11f);
                Assert.InRange(Dm.roll("d6"), 1f, 6f);
                Assert.InRange(Dm.roll(2, 10), 2f, 20f);
            }
            Assert.Equal(16, totals.Count);
            Assert.Equal(3f, Dm.roll(3, 1));
            Assert.Equal(1f, Dm.roll(null));
            Assert.Equal(0f, Dm.roll(0, 6));
            V.Error(() => Dm.roll("bad dice"));
            return 0;
        });
    }

    [Fact]
    public void PickFromAnAlistChoosesOneOfItsKeys()
    {
        // BYOND ref /proc/pick: "If only one argument is included and it is a list, then the item is chosen from that
        // list"; the items of an alist are its keys, and a number there is a key, never a position (BYOND ref
        // /proc/alist). Picking reads the list only.
        InWorld(11, () =>
        {
            DmList AL = Dm.alist(Dm.kv(5f, "five"), Dm.kv("k", 7f));
            var seen = new HashSet<object>();
            for (int i = 0; i < 200; i++)
            {
                object picked = Dm.pick(AL);
                Assert.True(Dm.eq(picked, 5f) || Dm.eq(picked, "k"), "picked " + (picked ?? "null"));
                seen.Add(picked);
                object spread = Dm.pick(Dm.arglist(AL));
                Assert.True(Dm.eq(spread, 5f) || Dm.eq(spread, "k"), "picked " + (spread ?? "null") + " through arglist");
            }
            Assert.Equal(2, seen.Count);
            Assert.Equal(2f, AL.len);
            V.Eq("five", AL[5]);
            return 0;
        });
    }

    [Fact]
    public void EveryDrawDependsOnlyOnTheSeedAndTheTick()
    {
        // D31 (rules 4.13): the world RNG is reseeded from (seed, tick) at every tick, so the same calls in two worlds
        // with the same seed at the same tick give the same values, whatever ran in earlier ticks; prob() draws once
        // whatever its percentage, so prob(0) and prob(100) leave the stream at the same place.
        static object[] Draws()
        {
            var L = Dm.list("a", "b", "c", "d", "e");
            return new object[]
            {
                Dm.prob(50), Dm.rand(), Dm.rand(1, 100), Dm.pick(L), Dm.pick("x", "y", "z"),
                Dm.pick_weighted((20, "brown"), (5, "blue"), (100, "green")), Dm.roll("3d6"), Dm.roll(2, 20),
            };
        }
        var first = new World(42);
        var second = new World(42);
        first.Step(3);
        using (second.Enter())
        {
            for (int i = 0; i < 50; i++) Dm.rand();   // draws in tick 0 do not leak into tick 3
        }
        second.Step(3);
        object[] a, b;
        using (first.Enter()) a = Draws();
        using (second.Enter()) b = Draws();
        Assert.Equal(a.Length, b.Length);
        for (int i = 0; i < a.Length; i++) Assert.True(Dm.eq(a[i], b[i]) || a[i].Equals(b[i]), "draw " + i);
        first.Step();
        object[] c;
        using (first.Enter()) c = Draws();
        Assert.False(a.Zip(c).All(p => Dm.eq(p.First, p.Second) || p.First.Equals(p.Second)), "the next tick draws other values");

        float afterNever, afterAlways;
        var w1 = new World(7);
        var w2 = new World(7);
        using (w1.Enter())
        {
            Assert.False(Dm.prob(0));
            afterNever = Dm.rand();
        }
        using (w2.Enter())
        {
            Assert.True(Dm.prob(100));
            afterAlways = Dm.rand();
        }
        Assert.Equal(afterNever, afterAlways);
    }
}
