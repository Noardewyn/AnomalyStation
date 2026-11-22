using Anomaly.Core.Kernel;

namespace Anomaly.Core.Tg;

// The owner's play test (2026-10-09): a hit effect. A host seam, not tg code: it stands where tg puts its attack visual on the
// attacked atom (items.dm:1592-1602, do_item_attack_animation's effect image, and :1604-1660, animate_attack's item image, both
// shown with flick_overlay_view to the viewers), which the 3D client draws as a burst of pixel particles at the attacked atom.

/// <summary>Where a world's attack visuals go (the server's: a one-tick <c>EntityFlags.Hit</c> on the attacked entity). Write-only, like the presentation sinks.</summary>
public interface IHitEffectSink
{
    /// <summary>tg showed its attack visual on <paramref name="attacked"/> this tick (a punch, a kick or an item's swing).</summary>
    void Hit(atom attacked);
}

/// <summary>The hit effect seam: tg's attack visual reports here; the world's sink (none by default) receives it.</summary>
public static class HitEffectSeam
{
    /// <summary>Installs the world's sink (the host, before the world runs; null removes it).</summary>
    public static void Install(World world, IHitEffectSink sink)
    {
        ArgumentNullException.ThrowIfNull(world);
        Holder.Of(world).Sink = sink;
    }

    /// <summary>tg's attack visual on <paramref name="attacked"/>: nothing without a sink or for a deleted or unregistered atom.</summary>
    public static void Report(atom attacked)
    {
        if (attacked == null || attacked.gc_destroyed != null || attacked.dm_id == 0)
            return;
        World world = World.CurrentOrNull;
        if (world == null)
            return;
        Holder.Of(world).Sink?.Hit(attacked);
    }

    // The per-world sink (World.GetExtension: no static state).
    private sealed class Holder
    {
        internal IHitEffectSink Sink;

        internal static Holder Of(World world) => world.GetExtension(static _ => new Holder());
    }
}
