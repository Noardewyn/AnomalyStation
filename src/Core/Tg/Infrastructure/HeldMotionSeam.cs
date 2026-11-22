using Anomaly.Core.Kernel;

namespace Anomaly.Core.Tg;

// D285 (the owner, 2026-10-28): a held gun's reload as an event, not a guess from its sound. A host seam, not tg code: it stands where tg's
// ballistic gun loads, unloads and racks (ballistic.dm: insert_magazine, eject_magazine, load_gun, unload, rack, drop_bolt), which tg shows
// only by its sound and a balloon; the server sends it to the user's client, whose first-person gun moves (DEVIATION(DV213)).

/// <summary>What a gun in the hands did.</summary>
public enum HeldMotion : byte
{
    /// <summary>A magazine or rounds went in.</summary>
    Load = 0,

    /// <summary>A magazine or rounds came out.</summary>
    Unload = 1,

    /// <summary>The bolt, slide or pump was worked.</summary>
    Rack = 2,
}

/// <summary>Where a world's held motions go (the server's: the user's session's HeldMotion message). Write-only, like the presentation sinks.</summary>
public interface IHeldMotionSink
{
    /// <summary><paramref name="user"/> did <paramref name="motion"/> with a gun this tick.</summary>
    void Moved(mob user, HeldMotion motion);
}

/// <summary>The held motion seam: tg's gun reloads report here; the world's sink (none by default) receives it.</summary>
public static class HeldMotionSeam
{
    /// <summary>Installs the world's sink (the host, before the world runs; null removes it).</summary>
    public static void Install(World world, IHeldMotionSink sink)
    {
        ArgumentNullException.ThrowIfNull(world);
        Holder.Of(world).Sink = sink;
    }

    /// <summary>The user worked a gun: nothing without a user, a sink or a world.</summary>
    public static void Report(mob user, HeldMotion motion)
    {
        if (user == null || user.gc_destroyed != null)
            return;
        World world = World.CurrentOrNull;
        if (world == null)
            return;
        Holder.Of(world).Sink?.Moved(user, motion);
    }

    // The per-world sink (World.GetExtension: no static state).
    private sealed class Holder
    {
        internal IHeldMotionSink Sink;

        internal static Holder Of(World world) => world.GetExtension(static _ => new Holder());
    }
}
