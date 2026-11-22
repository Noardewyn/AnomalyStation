namespace Anomaly.Client.Shared;

// Owner: M2 P8 (client play). Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).

/// <summary>A mob the camera ray may hit: where it stands (tiles with a fraction, as <see cref="RenderPose"/>) and whether it lies.</summary>
public readonly record struct PickCandidate(long Id, float TileX, float TileY, int Z, bool Lying);

/// <summary>
/// The first mob along the camera ray (scope 2f: "target = the camera ray's first mob"; D49: a ray from the camera against
/// simplified colliders). A standing mob is a vertical cylinder (<see cref="StandingRadius"/> wide, <see cref="StandingHeight"/>
/// tall: the capsule of D33, a little fat so that aiming is forgiving), a lying mob a flat disc on the floor; walls stop the
/// ray (every solid tile is floor to ceiling). This is the logical pick the client sends as the click's target; the server
/// validates the target and its reach itself (D40). Pure geometry on the poses the renderer draws from, so the renderer's own
/// pick (M2 P7's) can replace it without a change to the click.
/// </summary>
public static class RayPicker
{
    /// <summary>Metres per tile (D33).</summary>
    private const double Tile = TileSpace.TileSize;

    public const double StandingRadius = 0.45;
    public const double StandingHeight = 1.8;
    public const double LyingRadius = 0.85;
    public const double LyingHeight = 0.35;

    /// <summary>The direction of a view in metres: east, north, up, for Godot's yaw (counter-clockwise from north) and a pitch (up positive).</summary>
    public static (double East, double North, double Up) Direction(double godotYaw, double pitch)
    {
        double cp = Math.Cos(pitch);
        return (-Math.Sin(godotYaw) * cp, Math.Cos(godotYaw) * cp, Math.Sin(pitch));
    }

    /// <summary>
    /// The nearest candidate the ray from the eye (<paramref name="eyeTileX"/>, <paramref name="eyeTileY"/> in tiles,
    /// <paramref name="eyeHeight"/> metres above the floor of level <paramref name="z"/>) hits within
    /// <paramref name="maxMetres"/>, unless a solid tile is in the way. <paramref name="exclude"/> is the own entity.
    /// </summary>
    public static bool TryPick(float eyeTileX, float eyeTileY, int z, float eyeHeight, double godotYaw, double pitch,
        IReadOnlyList<PickCandidate> candidates, Func<int, int, int, bool> isSolid, double maxMetres, long exclude,
        out long id, out double distance)
    {
        id = 0;
        distance = 0;
        (double dx, double dy, double dz) = Direction(godotYaw, pitch);
        double ox = eyeTileX * Tile, oy = eyeTileY * Tile, oz = eyeHeight;
        double best = double.PositiveInfinity;
        long bestId = 0;
        for (int i = 0; i < candidates.Count; i++)
        {
            PickCandidate c = candidates[i];
            if (c.Id == exclude || c.Z != z)
                continue;
            double radius = c.Lying ? LyingRadius : StandingRadius;
            double height = c.Lying ? LyingHeight : StandingHeight;
            if (HitCylinder(ox, oy, oz, dx, dy, dz, c.TileX * Tile, c.TileY * Tile, radius, height, out double t) && t <= maxMetres && t < best)
            {
                best = t;
                bestId = c.Id;
            }
        }
        if (bestId == 0)
            return false;
        if (isSolid != null && WallBetween(eyeTileX, eyeTileY, z, dx, dy, best * Math.Sqrt(dx * dx + dy * dy) / Tile, isSolid))
            return false;
        id = bestId;
        distance = best;
        return true;
    }

    /// <summary>The distance along the ray (metres) where it enters a vertical cylinder of the given radius and height standing on the floor at (cx, cy); false for a miss.</summary>
    public static bool HitCylinder(double ox, double oy, double oz, double dx, double dy, double dz, double cx, double cy, double radius, double height, out double t)
    {
        t = double.PositiveInfinity;
        double rx = ox - cx, ry = oy - cy;
        double a = dx * dx + dy * dy;
        bool hit = false;
        if (a > 1e-12)
        {
            double b = dx * rx + dy * ry;
            double c = rx * rx + ry * ry - radius * radius;
            double disc = b * b - a * c;
            if (disc >= 0)
            {
                double sqrt = Math.Sqrt(disc);
                for (int root = 0; root < 2; root++)
                {
                    double candidate = root == 0 ? (-b - sqrt) / a : (-b + sqrt) / a;
                    if (candidate <= 0)
                        continue;
                    double zz = oz + dz * candidate;
                    if (zz >= 0 && zz <= height && candidate < t)
                    {
                        t = candidate;
                        hit = true;
                    }
                }
            }
        }
        // The caps: a ray from above or below.
        if (Math.Abs(dz) > 1e-12)
        {
            for (int cap = 0; cap < 2; cap++)
            {
                double candidate = ((cap == 0 ? 0.0 : height) - oz) / dz;
                if (candidate <= 0 || candidate >= t)
                    continue;
                double px = rx + dx * candidate, py = ry + dy * candidate;
                if (px * px + py * py <= radius * radius)
                {
                    t = candidate;
                    hit = true;
                }
            }
        }
        return hit;
    }

    /// <summary>
    /// True when a solid tile (not the one the eye is in) lies on the ray's ground track before it has gone
    /// <paramref name="horizontalTiles"/> tiles: Amanatides and Woo's grid walk, a tile covering [n - 0.5, n + 0.5).
    /// </summary>
    public static bool WallBetween(double eyeTileX, double eyeTileY, int z, double dx, double dy, double horizontalTiles, Func<int, int, int, bool> isSolid)
    {
        double length = Math.Sqrt(dx * dx + dy * dy);
        if (length < 1e-9 || horizontalTiles <= 0)
            return false;
        double ux = dx / length, uy = dy / length; // unit step in tiles
        int tx = (int)Math.Floor(eyeTileX + 0.5), ty = (int)Math.Floor(eyeTileY + 0.5);
        int stepX = ux > 0 ? 1 : -1, stepY = uy > 0 ? 1 : -1;
        double nextX = ux > 0 ? tx + 0.5 : tx - 0.5, nextY = uy > 0 ? ty + 0.5 : ty - 0.5;
        double tMaxX = Math.Abs(ux) < 1e-12 ? double.PositiveInfinity : (nextX - eyeTileX) / ux;
        double tMaxY = Math.Abs(uy) < 1e-12 ? double.PositiveInfinity : (nextY - eyeTileY) / uy;
        double tDeltaX = Math.Abs(ux) < 1e-12 ? double.PositiveInfinity : 1.0 / Math.Abs(ux);
        double tDeltaY = Math.Abs(uy) < 1e-12 ? double.PositiveInfinity : 1.0 / Math.Abs(uy);
        for (int guard = 0; guard < 4096; guard++)
        {
            double enter;
            if (tMaxX < tMaxY)
            {
                enter = tMaxX;
                tx += stepX;
                tMaxX += tDeltaX;
            }
            else
            {
                enter = tMaxY;
                ty += stepY;
                tMaxY += tDeltaY;
            }
            if (enter >= horizontalTiles)
                return false;
            if (isSolid(tx, ty, z))
                return true;
        }
        return false;
    }
}
