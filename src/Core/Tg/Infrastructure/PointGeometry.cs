using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/// <summary>
/// DEVIATION(DV232): the point a pointer's aim met on what it pointed at (the click's <see cref="ClientSeam.AIM_PARAM"/>, kept by ClickOn), in
/// absolute tile pixels (a tile's centre at 32 times its x; the height above the level's floor): a wall's face (the tile's box, a tile high),
/// the floor of an open turf, or a movable's picture (the upright plane through its centre across the aim). Every client draws the arrow there.
/// </summary>
public static class PointGeometry
{
    private const double Half = ShotGeometry.PixelsPerTile / 2.0;

    public static bool TryAimedPoint(mob pointer, atom target, out double x, out double y, out double h)
    {
        x = y = h = 0;
        if (pointer == null || target == null || pointer.dm_attack_target != target
            || !ClientSeam.TryReadAim(pointer.dm_attack_modifiers, out double ex, out double ey, out double eh, out double yaw, out double pitch)
            || get_turf(target) is not turf tile)
            return false;
        double flat = Math.Cos(pitch * Math.PI / 180);
        double dx = Math.Sin(yaw * Math.PI / 180) * flat, dy = Math.Cos(yaw * Math.PI / 180) * flat, dh = Math.Sin(pitch * Math.PI / 180);
        double cx = Dm.num(tile.x) * ShotGeometry.PixelsPerTile, cy = Dm.num(tile.y) * ShotGeometry.PixelsPerTile;
        // DEVIATION(DV393): a target on another level of the stack (seen through open space, or the floor over the pointer): its floor's height over the
        // pointer's, whose eye the aim starts at; the arrow, on the pointer's tile as in tg, is drawn that far up or down
        double floor = get_turf(pointer) is turf own ? (Dm.num(tile.z) - Dm.num(own.z)) * ShotGeometry.CeilingPixels : 0;
        double t;
        switch (target)
        {
            case turf_closed:
                if (!EnterBox(ex - cx, ey - cy, eh - floor, dx, dy, dh, out t))
                    return false;
                break;
            case turf:
                if (Math.Abs(dh) < 1e-6)
                    return false;
                t = (floor - eh) / dh;
                break;
            case atom_movable thing:
                if (LocomotionSeam.TryGetState(World.Current, thing.dm_id, out Anomaly.Core.Motion.MotionState position, out _) && thing.loc is turf)
                    (cx, cy) = (position.X / 8.0, position.Y / 8.0);
                else
                    (cx, cy) = (cx + thing.pixel_x, cy + thing.pixel_y);
                double across = dx * dx + dy * dy;
                if (across < 1e-9)
                    return false;
                t = ((cx - ex) * dx + (cy - ey) * dy) / across;
                break;
            default:
                return false;
        }
        if (t <= 0)
            return false;
        // the point stays on the target's tile, between the floor and the ceiling
        x = Math.Clamp(ex + dx * t, cx - Half, cx + Half);
        y = Math.Clamp(ey + dy * t, cy - Half, cy + Half);
        h = Math.Clamp(eh + dh * t, floor, floor + ShotGeometry.CeilingPixels);
        return true;
    }

    // The distance along the ray (from a point relative to the tile's centre) at which it enters the tile's box, a tile high (the slab method).
    /// <summary>DV255: the side of <paramref name="target"/>'s tile a click's aim went in by, as a BYOND dir: the last side plane the ray crossed before the tile (behind the eye when the eye is on the tile); 0 with no aim.</summary>
    public static int FaceAimedAt(DmList modifiers, atom target)
    {
        if (!ClientSeam.TryReadAim(modifiers, out double ex, out double ey, out _, out double yaw, out _) || get_turf(target) is not turf tile)
            return 0;
        double dx = Math.Sin(yaw * Math.PI / 180), dy = Math.Cos(yaw * Math.PI / 180);
        double ox = ex - Dm.num(tile.x) * ShotGeometry.PixelsPerTile, oy = ey - Dm.num(tile.y) * ShotGeometry.PixelsPerTile;
        double tx = Math.Abs(dx) < 1e-9 ? double.NegativeInfinity : ((dx > 0 ? -Half : Half) - ox) / dx;
        double ty = Math.Abs(dy) < 1e-9 ? double.NegativeInfinity : ((dy > 0 ? -Half : Half) - oy) / dy;
        return (int)(tx > ty ? (dx > 0 ? WEST : EAST) : (dy > 0 ? SOUTH : NORTH));
    }

    private static bool EnterBox(double ox, double oy, double oh, double dx, double dy, double dh, out double t)
    {
        double near = double.NegativeInfinity, far = double.PositiveInfinity;
        foreach ((double o, double d, double lo, double hi) in new[] { (ox, dx, -Half, Half), (oy, dy, -Half, Half), (oh, dh, 0.0, (double)ShotGeometry.CeilingPixels) })
        {
            if (Math.Abs(d) < 1e-9)
            {
                if (o < lo || o > hi)
                {
                    t = 0;
                    return false;
                }
                continue;
            }
            double a = (lo - o) / d, b = (hi - o) / d;
            near = Math.Max(near, Math.Min(a, b));
            far = Math.Min(far, Math.Max(a, b));
        }
        t = near;
        return near <= far && near > 0;
    }

    /// <summary>
    /// DEVIATION(DV393): tg's pointing needs its target in the pointer's <c>view()</c>, one level; the 3D client shows the levels of the stack through open space
    /// and the floor over the pointer as its ceiling (D325). A target on another level is seen along the click's aim (DV94) when every level's floor the aim
    /// passes through on the way is see-through (open space, glass floor; up: no ceiling under it) and the target lies along the aim on its level; the floor over
    /// the pointer is seen where the aim meets it. Without an aim, the column straight over or under the target must be see-through. The cell of the pointer's
    /// level the target is seen through is in its view.
    /// </summary>
    public static bool SeenThroughLevels(mob viewer, atom target, object range)
    {
        if (viewer == null || get_turf(viewer) is not turf own || get_turf(target) is not turf tile || Dm.num(tile.z) == Dm.num(own.z))
            return false;
        if (ClientSeam.TryReadAim(viewer.dm_attack_modifiers, out double ex, out double ey, out double eh, out double yaw, out double pitch))
            return SeenAlongAim(viewer, target, tile, own, range, ex, ey, eh, yaw, pitch);
        float ownZ = Dm.num(own.z);
        turf column = tile;
        if (Dm.num(tile.z) < ownZ)
        {
            while (Dm.num(column.z) < ownZ)
            {
                column = GET_TURF_ABOVE(column) as turf;
                if (column == null || !istransparentturf(column))
                    return false;
            }
        }
        else
        {
            if (target is not turf && !istransparentturf(tile))
                return false;
            while (Dm.num(column.z) > ownZ)
            {
                column = GET_TURF_BELOW(column) as turf;
                if (column == null || Dm.num(column.z) > ownZ && !istransparentturf(column))
                    return false;
            }
        }
        return Dm.@in(column, view(range, viewer));
    }

    // The aim from the eye (absolute tile pixels, its height over the pointer's floor) crosses each level's floor between the pointer's and the target's:
    // there the turf is see-through, and past the last crossing the target's tile lies along the aim (within a tile of it).
    private static bool SeenAlongAim(mob viewer, atom target, turf tile, turf own, object range, double ex, double ey, double eh, double yaw, double pitch)
    {
        int ownZ = (int)Dm.num(own.z), targetZ = (int)Dm.num(tile.z), step = targetZ > ownZ ? 1 : -1;
        double flat = Math.Cos(pitch * Math.PI / 180);
        double dx = Math.Sin(yaw * Math.PI / 180) * flat, dy = Math.Cos(yaw * Math.PI / 180) * flat, dh = Math.Sin(pitch * Math.PI / 180);
        if (step < 0 ? dh > -1e-6 : dh < 1e-6)
            return false;
        turf first = null;
        double t = 0;
        for (int level = ownZ; level != targetZ; level += step)
        {
            // the floor of the higher of the two levels, over the pointer's floor
            double plane = (step > 0 ? level + 1 - ownZ : level - ownZ) * ShotGeometry.CeilingPixels;
            t = (plane - eh) / dh;
            int cx = (int)Math.Floor((ex + dx * t) / ShotGeometry.PixelsPerTile + 0.5), cy = (int)Math.Floor((ey + dy * t) / ShotGeometry.PixelsPerTile + 0.5);
            turf lower = locate(cx, cy, step > 0 ? level : level - 1) as turf, upper = locate(cx, cy, step > 0 ? level + 1 : level) as turf;
            if (lower == null || upper == null)
                return false;
            first ??= step > 0 ? lower : upper;
            if (step > 0 && CeilingSystem.Of(World.Current).At(cx, cy, (int)Dm.num(lower.z)).Exists)
                return false;
            // up to the level over the pointer, the target may be that floor itself, seen from under it
            if (step > 0 && level + 1 == targetZ && upper == tile && target is turf)
                break;
            if (!istransparentturf(upper))
                return false;
        }
        // the target along the aim past the last crossing: its tile's middle within a tile of the aim's ground track from that crossing on
        // (an aim straight up or down has no track: the crossing's point)
        double px = ex + dx * t, py = ey + dy * t;
        double tx = Dm.num(tile.x) * ShotGeometry.PixelsPerTile - px, ty = Dm.num(tile.y) * ShotGeometry.PixelsPerTile - py;
        double track = dx * dx + dy * dy;
        double along = track < 1e-9 ? 0 : Math.Max(0, (tx * dx + ty * dy) / track);
        double off = Math.Sqrt(Math.Pow(tx - dx * along, 2) + Math.Pow(ty - dy * along, 2));
        return off <= ShotGeometry.PixelsPerTile && Dm.@in(first, view(range, viewer));
    }

    /// <summary>Marks tg's point arrow with the aimed point (the replicated shot line of an effect, level there); nothing without one.</summary>
    public static void Mark(mob pointer, atom target, object arrow)
    {
        if (arrow is not obj_effect fx || !TryAimedPoint(pointer, target, out double x, out double y, out double h))
            return;
        fx.dm_shot = true;
        fx.dm_origin_x = (float)x;
        fx.dm_origin_y = (float)y;
        fx.dm_origin_h = (float)h;
        fx.dm_slope = 0f;
    }
}
