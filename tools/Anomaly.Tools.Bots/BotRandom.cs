namespace Anomaly.Tools.Bots;

/// <summary>
/// The bots' own random numbers (SplitMix64): deterministic for a seed on every platform and runtime, and independent of
/// the world's RNG (the bots never touch the core). One per bot, single-threaded.
/// </summary>
public sealed class BotRandom
{
    private ulong _state;

    public BotRandom(ulong seed)
    {
        _state = seed;
    }

    /// <summary>The next 64 random bits.</summary>
    public ulong NextUInt64()
    {
        _state += 0x9E3779B97F4A7C15UL;
        ulong z = _state;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    /// <summary>An integer in [<paramref name="minInclusive"/>, <paramref name="maxInclusive"/>].</summary>
    public int Next(int minInclusive, int maxInclusive)
    {
        if (maxInclusive < minInclusive) throw new ArgumentOutOfRangeException(nameof(maxInclusive), maxInclusive, "below the minimum");
        ulong span = (ulong)((long)maxInclusive - minInclusive + 1);
        return (int)(minInclusive + (long)(NextUInt64() % span));
    }

    /// <summary>A value in [0, 1).</summary>
    public double NextDouble() => (NextUInt64() >> 11) * (1.0 / (1UL << 53));

    /// <summary>True with probability <paramref name="p"/>.</summary>
    public bool Chance(double p) => NextDouble() < p;

    /// <summary>Fills <paramref name="buffer"/> with random bytes.</summary>
    public void Fill(Span<byte> buffer)
    {
        int i = 0;
        while (i < buffer.Length)
        {
            ulong v = NextUInt64();
            for (int b = 0; b < 8 && i < buffer.Length; b++, i++)
            {
                buffer[i] = (byte)(v >> (b * 8));
            }
        }
    }

    /// <summary>A seed for the bot number <paramref name="index"/> of a run seeded with <paramref name="seed"/> (the streams of different bots must not overlap).</summary>
    public static ulong DeriveSeed(ulong seed, int index)
    {
        var mixer = new BotRandom(seed ^ ((ulong)(uint)index * 0xD6E8FEB86659FD93UL));
        mixer.NextUInt64();
        return mixer.NextUInt64();
    }
}
