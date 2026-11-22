using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/// <summary>
/// DEVIATION(DV578, the owner 2026-11-21: a rocket or a grenade fired at the ceiling went off over it, and the ceiling took nothing): a blast set
/// off by a shot that hit the top of its cell (the cell's own ceiling, or the floor of the level over it hit from under it) goes off under that
/// top, in the cell: its epicentre is the cell (not the turf over it), the level's turfs and their ceilings take tg's blast as any blast's, the
/// floor over the epicentre takes the epicentre's severity, and its blasts are drawn at the ceiling (the effects carry that height on the shot's
/// line, <see cref="ShotGeometry"/>'s fields).
/// </summary>
public static class TopBlast
{
    /// <summary>How far under the ceiling a top blast is drawn, tile pixels (its middle).</summary>
    public const float BelowCeilingPixels = 6f;

    /// <summary>
    /// Runs <paramref name="hit"/> (the shot's hit on the top of <paramref name="cell"/>, <paramref name="above"/> the turf over it or null) with that
    /// hit noted, so that a blast it sets off there goes off under the top.
    /// </summary>
    public static async DmTask Hitting(turf cell, turf above, Func<DmTask> hit)
    {
        State s = State.Of(World.Current);
        (turf, turf) before = (s.Cell, s.Above);
        (s.Cell, s.Above) = (cell, above);
        try
        {
            await hit();
        }
        finally
        {
            (s.Cell, s.Above) = before;
        }
    }

    /// <summary>
    /// tg's explosion(): the epicentre of a blast at the top being hit (its cell or the turf over it) is that cell, and the cell is noted for
    /// <see cref="Take"/>; anything else is left as it is.
    /// </summary>
    public static atom Remap(atom origin)
    {
        World world = World.CurrentOrNull;
        if (world == null || origin == null)
            return origin;
        State s = State.Of(world);
        if (s.Cell == null || s.Cell.gc_destroyed != null)
            return origin;
        turf at = get_turf(origin);
        if (at != s.Cell && (s.Above == null || at != s.Above))
            return origin;
        s.Pending.Add(s.Cell);
        return s.Cell;
    }

    /// <summary>Whether the blast at <paramref name="epicenter"/> goes off at the top (noted by <see cref="Remap"/>); the note is used up.</summary>
    public static bool Take(turf epicenter)
    {
        World world = World.CurrentOrNull;
        return world != null && epicenter != null && State.Of(world).Pending.Remove(epicenter);
    }

    /// <summary>A top blast's effect (<c>/obj/effect/explosion</c>) is drawn under the ceiling: a level line at that height on its own point.</summary>
    public static void Mark(obj_effect effect)
    {
        if (effect == null || effect.loc is not turf)
            return;
        effect.dm_shot = true;
        effect.dm_origin_x = Dm.num(effect.x) * ShotGeometry.PixelsPerTile + effect.pixel_x;
        effect.dm_origin_y = Dm.num(effect.y) * ShotGeometry.PixelsPerTile + effect.pixel_y;
        effect.dm_origin_h = ShotGeometry.CeilingPixels - BelowCeilingPixels;
        effect.dm_slope = 0f;
        effect.dm_shot_angle = 0f;
    }

    /// <summary>The turf over a top blast's epicentre that takes its severity: the floor of the level over the cell, null for none.</summary>
    public static turf FloorOver(turf epicenter)
        => ShotGeometry.Overhead(epicenter, out turf above) == ShotGeometry.Top.FloorAbove ? above : null;

    private sealed class State
    {
        internal turf Cell, Above;
        internal readonly HashSet<turf> Pending = new();

        internal static State Of(World world) => world.GetExtension(static _ => new State());
    }
}
