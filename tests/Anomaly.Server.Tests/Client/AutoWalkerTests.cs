using Anomaly.Client.Shared;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client;

/// <summary>M1 P6: the <c>--autowalk</c> behaviour.</summary>
public class AutoWalkerTests
{
    private static List<HeldDirs> Run(int seed, double seconds, Func<double, (int X, int Y)> position)
    {
        var walker = new AutoWalker(seed);
        var result = new List<HeldDirs>();
        for (double t = 0; t < seconds; t += 0.05)
        {
            (int x, int y) = position(t);
            result.Add(walker.Update(t, x, y));
        }
        return result;
    }

    [Fact]
    public void TheSameSeedWalksTheSameWay()
    {
        Func<double, (int, int)> wander = t => ((int)(t * 6) % 40 + 2, 10);
        Assert.Equal(Run(5, 60, wander), Run(5, 60, wander));
        Assert.NotEqual(Run(5, 60, wander), Run(6, 60, wander));
    }

    [Fact]
    public void ItAlwaysHoldsAValidMask()
    {
        foreach (HeldDirs mask in Run(3, 300, t => ((int)(t * 6) % 40 + 2, 10)))
        {
            Assert.Equal(HeldDirs.None, mask & ~HeldDirs.All);
            // never both of an opposite pair
            Assert.False((mask & HeldDirs.North) != 0 && (mask & HeldDirs.South) != 0);
            Assert.False((mask & HeldDirs.East) != 0 && (mask & HeldDirs.West) != 0);
        }
    }

    [Fact]
    public void ItPicksAnotherDirectionWhenTheTileDoesNotChange()
    {
        var walker = new AutoWalker(11) { MinHoldSeconds = 100, MaxHoldSeconds = 101 };
        HeldDirs first = walker.Update(0.0, 5, 5);
        // Make sure it holds something to be blocked in: re-seed until it does not pick "stop" first.
        int seed = 11;
        while (first == HeldDirs.None)
        {
            walker = new AutoWalker(++seed) { MinHoldSeconds = 100, MaxHoldSeconds = 101 };
            first = walker.Update(0.0, 5, 5);
        }
        // The tile never changes: after the blocked time, the direction changes, and keeps changing.
        HeldDirs previous = first;
        int repicks = 0;
        for (double t = 0.05; t < 10; t += 0.05)
        {
            HeldDirs now = walker.Update(t, 5, 5);
            if (now != previous)
            {
                repicks++;
                Assert.True(t > walker.BlockedAfterSeconds, $"re-picked too early at {t}");
            }
            previous = now;
        }
        Assert.True(repicks >= 5, $"only {repicks} re-picks in 10 s of being blocked");
        Assert.Equal(walker.BlockedRepicks, repicks);
    }

    [Fact]
    public void ItDoesNotRepickWhileTheTileKeepsChanging()
    {
        var walker = new AutoWalker(11) { MinHoldSeconds = 100, MaxHoldSeconds = 101 };
        HeldDirs first = walker.Update(0.0, 5, 5);
        int seed = 11;
        while (first == HeldDirs.None)
        {
            walker = new AutoWalker(++seed) { MinHoldSeconds = 100, MaxHoldSeconds = 101 };
            first = walker.Update(0.0, 5, 5);
        }
        for (double t = 0.05; t < 30; t += 0.05)
            Assert.Equal(first, walker.Update(t, 5 + (int)(t / 0.15), 5));
        Assert.Equal(0, walker.BlockedRepicks);
    }

    [Fact]
    public void ItChangesDirectionWhenTheHoldTimeIsOver()
    {
        var masks = Run(2, 120, t => ((int)(t * 6) % 40 + 2, 10));
        int changes = 0;
        for (int i = 1; i < masks.Count; i++)
        {
            if (masks[i] != masks[i - 1])
                changes++;
        }
        // 120 s with holds of 1 to 4 s (and short pauses): dozens of changes, not a handful.
        Assert.InRange(changes, 25, 120);
        Assert.Contains(HeldDirs.None, masks);
        Assert.Contains(masks, m => m == HeldDirs.North || m == HeldDirs.South || m == HeldDirs.East || m == HeldDirs.West);
        Assert.Contains(masks, IsDiagonal);
    }

    private static bool IsDiagonal(HeldDirs m) => ((m & (HeldDirs.North | HeldDirs.South)) != 0) && ((m & (HeldDirs.East | HeldDirs.West)) != 0);
}
