using Anomaly.Tools.Bots;

namespace Anomaly.Server.Tests.Bots;

public class BotRandomTests
{
    [Fact]
    public void AKnownSeedGivesAKnownStream()
    {
        // SplitMix64 reference values for seed 0 (the generator is fixed, so a run can be repeated from its seed anywhere).
        var random = new BotRandom(0);
        Assert.Equal(0xE220A8397B1DCDAFUL, random.NextUInt64());
        Assert.Equal(0x6E789E6AA1B965F4UL, random.NextUInt64());
        Assert.Equal(0x06C45D188009454FUL, random.NextUInt64());
    }

    [Fact]
    public void NextStaysInItsRangeAndReachesBothEnds()
    {
        var random = new BotRandom(9);
        var seen = new HashSet<int>();
        for (int i = 0; i < 2000; i++)
        {
            int v = random.Next(-2, 3);
            Assert.InRange(v, -2, 3);
            seen.Add(v);
        }
        Assert.Equal(6, seen.Count);
        Assert.Equal(4, random.Next(4, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => random.Next(3, 2));
    }

    [Fact]
    public void NextDoubleIsBelowOne()
    {
        var random = new BotRandom(5);
        for (int i = 0; i < 2000; i++)
        {
            double d = random.NextDouble();
            Assert.True(d >= 0 && d < 1);
        }
    }

    [Fact]
    public void FillWritesEveryByteDeterministically()
    {
        var a = new byte[37];
        var b = new byte[37];
        new BotRandom(3).Fill(a);
        new BotRandom(3).Fill(b);
        Assert.Equal(a, b);
        Assert.Contains(a, x => x != 0);
    }

    [Fact]
    public void BotsOfOneRunGetDistinctSeeds()
    {
        var seeds = new HashSet<ulong>();
        for (int i = 0; i < 500; i++) seeds.Add(BotRandom.DeriveSeed(1, i));
        Assert.Equal(500, seeds.Count);
        Assert.NotEqual(BotRandom.DeriveSeed(1, 0), BotRandom.DeriveSeed(2, 0));
        Assert.Equal(BotRandom.DeriveSeed(7, 3), BotRandom.DeriveSeed(7, 3));
    }
}
