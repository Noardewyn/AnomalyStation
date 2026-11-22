using Anomaly.Core.Kernel;

namespace Anomaly.Server;

/// <summary>
/// Scripted load for the headless server while no tg content is translated (<c>--smoke K</c>): K actors, each a
/// detached proc that sleeps 1-10 ticks and adds a die roll to its counter, forever. It makes the state hash
/// depend on the scheduler queue, the per-tick RNG streams and the waiting model, so the determinism check
/// ("the same seed prints the same final hash") is not trivial. Not tg code and not gameplay: it goes away when
/// the server boots the translated Master Controller (M1). All its state hangs off the world it is installed in.
/// </summary>
public sealed class SmokeLoad
{
    private readonly long[] _counters;

    private SmokeLoad(int actors) => _counters = new long[actors];

    /// <summary>Sum of all counters (for logs and tests).</summary>
    public long Total
    {
        get
        {
            long total = 0;
            foreach (long c in _counters) total += c;
            return total;
        }
    }

    /// <summary>Starts <paramref name="actors"/> actors in <paramref name="world"/> and registers their state hash.</summary>
    public static SmokeLoad Install(World world, int actors)
    {
        if (actors < 0) throw new ArgumentOutOfRangeException(nameof(actors), actors, "The number of smoke actors cannot be negative.");
        var load = new SmokeLoad(actors);
        world.AddStateHasher("smoke", load.HashInto);
        using (world.Enter())
        {
            for (int i = 0; i < actors; i++)
                Waiting.Detach(load.Actor(i));
        }
        return load;
    }

    private async DmTask Actor(int index)
    {
        while (true)
        {
            await Sleeping.SleepTicks(World.Current.Rng.NextInt(1, 10));
            _counters[index] += World.Current.Rng.NextInt(1, 6);
        }
    }

    private void HashInto(World world, ref StateHasher hasher)
    {
        hasher.Add(_counters.Length);
        foreach (long c in _counters) hasher.Add(c);
    }
}
