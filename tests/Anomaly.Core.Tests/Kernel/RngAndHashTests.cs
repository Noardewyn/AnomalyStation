using Anomaly.Core.Kernel;

namespace Anomaly.Core.Tests.Kernel;

public class RngAndHashTests
{
    private const ulong Golden = 0x9E3779B97F4A7C15UL;

    private static ulong Inverse(ulong odd)
    {
        ulong x = odd; // correct to 3 bits; each Newton step doubles the correct bits
        for (int i = 0; i < 5; i++) x *= 2 - odd * x;
        return x;
    }

    [Fact]
    public void AdjacentTicksNeverShareAShiftedStream()
    {
        // SplitMix64 advances its state by G per draw, so tick T+1 replays tick T shifted by k draws exactly
        // when state(T+1) - state(T) == k * G. Check every small shift for seeds 0..999, tg's UNIT_TESTS seed
        // (code/controllers/master.dm: 29051994) and 2^63, over 1000 ticks each.
        ulong gInverse = Inverse(Golden);
        Assert.Equal(1UL, gInverse * Golden);
        var seeds = Enumerable.Range(0, 1000).Select(i => (ulong)i).Concat(new[] { 29051994UL, 1UL << 63 });
        var rng = new Rng(0);
        var bad = new List<string>();
        foreach (var seed in seeds)
        {
            rng.Reseed(seed, 0);
            ulong previous = rng.State;
            for (long tick = 1; tick <= 1000; tick++)
            {
                rng.Reseed(seed, tick);
                ulong state = rng.State;
                long shift = unchecked((long)((state - previous) * gInverse));
                if (shift >= -64 && shift <= 64) bad.Add($"seed {seed} ticks {tick - 1}/{tick}: shift {shift}");
                previous = state;
            }
        }
        Assert.Empty(bad);
    }

    [Fact]
    public void TheSecondDrawOfATickIsNotTheFirstDrawOfTheNext()
    {
        // The symptom the reviewers measured (seed 0: 1000 of 1000 tick pairs before the fix).
        foreach (var seed in new[] { 0UL, 2UL, 42UL, 200UL, 29051994UL, 1UL << 63 })
        {
            var r = new Rng(0);
            int overlaps = 0;
            for (long t = 0; t < 1000; t++)
            {
                r.Reseed(seed, t);
                r.NextUInt64();
                ulong second = r.NextUInt64();
                r.Reseed(seed, t + 1);
                if (second == r.NextUInt64()) overlaps++;
            }
            Assert.Equal(0, overlaps);
        }
    }

    [Fact]
    public void WorldsWithSmallSeedsDrawDifferentNumbersInDifferentPhases()
    {
        var w = new World(0);
        var a = new List<ulong>();
        var b = new List<ulong>();
        w.AddPhase("a", x => a.Add(x.Rng.NextUInt64()));
        w.AddPhase("b", x => b.Add(x.Rng.NextUInt64()));
        w.Step(20);
        Assert.Empty(b.Take(19).Where((v, i) => v == a[i + 1]));
    }

    [Fact]
    public void NextIntAndNextFloatStayInRange()
    {
        var r = new Rng(123);
        long min = long.MaxValue, max = long.MinValue;
        for (int i = 0; i < 100_000; i++)
        {
            int v = r.NextInt(int.MinValue, int.MaxValue);
            min = Math.Min(min, v);
            max = Math.Max(max, v);
        }
        Assert.True(min < -2_000_000_000 && max > 2_000_000_000);
        for (int i = 0; i < 1000; i++) Assert.Equal(int.MaxValue, r.NextInt(int.MaxValue, int.MaxValue));
        for (int i = 0; i < 1000; i++) Assert.InRange(r.NextInt(5, 1), 1, 5);
        for (int i = 0; i < 100_000; i++) Assert.InRange(r.NextFloat(), 0f, 0.99999994f);
    }

    [Fact]
    public void RngStateIsPartOfTheStateHash()
    {
        // Two worlds that differ only in how many numbers were drawn this tick.
        var w1 = new World(5);
        var w2 = new World(5);
        w1.AddPhase("r", w => w.Rng.NextUInt64());
        w2.AddPhase("r", w => { w.Rng.NextUInt64(); w.Rng.NextUInt64(); });
        w1.Step(1);
        w2.Step(1);
        Assert.NotEqual(w1.Rng.State, w2.Rng.State);
        Assert.NotEqual(w1.ComputeStateHash(), w2.ComputeStateHash());
    }

    [Fact]
    public void NaNAndNegativeZeroHashCanonically()
    {
        // A NaN produced at run time is 0x7FC00000 on arm64, float.NaN is 0xFFC00000.
        float zero = float.Parse("0", System.Globalization.CultureInfo.InvariantCulture);
        float runtimeNaN = zero / zero;
        float payloadNaN = BitConverter.Int32BitsToSingle(0x7FC00001);
        ulong Hash(float v) { var h = StateHasher.Create(); h.Add(v); return h.Value; }
        ulong HashD(double v) { var h = StateHasher.Create(); h.Add(v); return h.Value; }
        Assert.Equal(Hash(float.NaN), Hash(runtimeNaN));
        Assert.Equal(Hash(float.NaN), Hash(payloadNaN));
        Assert.Equal(Hash(float.NaN), Hash(MathF.Sqrt(-1f - zero)));
        Assert.Equal(Hash(0f), Hash(-zero));
        Assert.NotEqual(Hash(0f), Hash(float.NaN));
        Assert.Equal(HashD(double.NaN), HashD(BitConverter.Int64BitsToDouble(0x7FF8000000000001)));
        Assert.Equal(HashD(0d), HashD(-0d));
    }
}
