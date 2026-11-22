using System.Runtime.CompilerServices;

namespace Anomaly.Core.Kernel;

/// <summary>
/// Deterministic random numbers (D31): no hidden global state. The generator is reseeded from
/// (world seed, tick) at the start of every tick, so replaying a tick reproduces the same numbers
/// regardless of the history before it. SplitMix64 core.
/// </summary>
public sealed class Rng
{
    private ulong _state;

    public Rng(ulong seed) => _state = seed;

    /// <summary>Current internal state; part of the world state hash.</summary>
    public ulong State => _state;

    private const ulong Golden = 0x9E3779B97F4A7C15UL;

    /// <summary>
    /// Start the stream of tick <paramref name="tick"/>. Both inputs go through the SplitMix64 finalizer, so
    /// the state of tick T+1 is not the state of tick T advanced by a few steps: a plain
    /// <c>seed ^ G*(tick+1)</c> made adjacent ticks share a stream shifted by one draw for small seeds.
    /// </summary>
    public void Reseed(ulong worldSeed, long tick)
    {
        _state = Mix64(worldSeed ^ Mix64(unchecked((ulong)tick) + Golden));
    }

    /// <summary>The SplitMix64 output function (a bijective 64-bit mixer).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong Mix64(ulong z)
    {
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong NextUInt64()
    {
        return Mix64(_state += Golden);
    }

    /// <summary>Uniform float in [0, 1), 24 bits of precision like a DM number.</summary>
    public float NextFloat() => (NextUInt64() >> 40) * (1f / (1 << 24));

    /// <summary>Uniform integer in [min, max] inclusive (DM <c>rand(min, max)</c>).</summary>
    public int NextInt(int min, int max)
    {
        if (max < min) (min, max) = (max, min);
        ulong span = (ulong)((long)max - min + 1);
        return (int)(min + (long)(NextUInt64() % span));
    }
}
