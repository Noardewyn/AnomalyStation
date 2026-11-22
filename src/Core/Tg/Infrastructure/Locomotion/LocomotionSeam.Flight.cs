using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;

namespace Anomaly.Core.Tg;

// DEVIATION(DV416) stage 2 (D328): a capsule mob that is thrown (a grab's throw, a blast, the singularity's pull) flies the flight seam's arc:
// while it flies the capsule takes no wish (tg's gate is not asked: no steering in the air) and stands where the arc puts it; when it lands
// the capsule stands at the landing point and walks on from there.

public static partial class LocomotionSeam
{
    /// <summary>DV416: whether <paramref name="mob"/> has a capsule (the flight seam's arc then moves it, <see cref="PlaceInFlight"/>).</summary>
    internal static bool HasCapsule(World world, mob_living mob) => mob != null && Seam.Of(world).Capsules.ContainsKey(mob.dm_id);

    /// <summary>
    /// DV416: the flight seam put <paramref name="mob"/> at (<paramref name="x"/>, <paramref name="y"/>) tiles on its level (on its loc's tile):
    /// its capsule stands there, with no slide or speed of its own. False when it has no capsule.
    /// </summary>
    internal static bool PlaceInFlight(World world, mob_living mob, double x, double y)
    {
        if (mob == null || !Seam.Of(world).Capsules.TryGetValue(mob.dm_id, out Capsule c) || mob.loc is not turf here)
            return false;
        int tx = (int)Dm.num(here.x), ty = (int)Dm.num(here.y);
        // kept inside the loc's tile, so Follow never takes it for a move of tg's
        const double edge = 0.5 - 1.0 / MotionMath.SubunitsPerTile;
        x = Math.Clamp(x, tx - edge, tx + edge);
        y = Math.Clamp(y, ty - edge, ty + edge);
        c.State.X = (int)Math.Round(x * MotionMath.SubunitsPerTile);
        c.State.Y = (int)Math.Round(y * MotionMath.SubunitsPerTile);
        c.State.Z = (int)Dm.num(here.z);
        c.State.Slide = default;
        c.State.VelX = 0;
        c.State.VelY = 0;
        return true;
    }
}
