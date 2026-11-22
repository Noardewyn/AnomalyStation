using Anomaly.Protocol;

namespace Anomaly.Client.Shared;

// Owner: M4a P7 (client interaction). Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).
// docs/ai/specs/interaction.md 4.1: one pick function for the highlight, the crosshair, the radial and the click.

/// <summary>
/// What the world pick needs from the renderers (interaction.md 4.1): the map's cells and the shapes standing in them. The Godot adapters
/// (<c>MapRenderer</c>, <c>EntityLayer</c>) make the shapes from what they draw; the tests make them up.
/// </summary>
public interface IPickWorld
{
    /// <summary>The map's size in tiles and levels.</summary>
    int Width { get; }

    int Height { get; }

    int Levels { get; }

    /// <summary>The height of the walls and the ceiling (D119).</summary>
    double WallHeight { get; }

    /// <summary>True for a closed turf (a wall): it stops the ray, and its face is a hit.</summary>
    bool IsWallCell(int x, int y, int z);

    /// <summary>True when the open cell has a ceiling over it (space gets none, D116).</summary>
    bool HasCeiling(int x, int y, int z);

    /// <summary>The shapes of things that move (mobs, thrown and carried items): all of them, or those near <paramref name="ray"/>.</summary>
    void CollectMoving(in PickRay ray, List<PickShape> into);

    /// <summary>The shapes of the static things in cell (<paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>): batched props and pieces.</summary>
    void CollectStatic(int x, int y, int z, List<PickShape> into);

    /// <summary>D325: how far a level's floor is over the floor of the level it stands on.</summary>
    double LevelHeight => WallHeight;

    /// <summary>D325: true when the open cell's floor is open space over a level linked under it: a ray goes on down into that level.</summary>
    bool OpenBelow(int x, int y, int z) => false;

    /// <summary>D325: true when a level stands on <paramref name="z"/>: the turf over the cell is its ceiling.</summary>
    bool LevelAbove(int x, int y, int z) => false;
}

/// <summary>
/// The world pick (interaction.md 4.1): the nearest of a billboard, a static prop, a piece, a floor, a wall or a ceiling cell along the ray
/// from the eye, stopped by the first wall (whose face is itself a hit). The server decides reach (D40); this only chooses what is under the
/// crosshair, and tg's <c>icon-x</c>/<c>icon-y</c> of the point.
/// </summary>
public sealed class WorldPicker : IWorldPicker
{
    /// <summary>The ordinary pick range: 4 tiles (interaction.md 4.1).</summary>
    public const double DefaultRange = 8.0;

    /// <summary>Throw mode's range: the held item's <c>throw_range</c> of 7 tiles.</summary>
    public const double ThrowRange = 14.0;

    private readonly IPickWorld _world;
    private readonly List<PickShape> _shapes = new();
    private readonly List<PickShape> _inWall = new();

    public WorldPicker(IPickWorld world) => _world = world ?? throw new ArgumentNullException(nameof(world));

    /// <summary>The shape the last <see cref="Pick"/> hit (null for a cell or no hit): the highlight draws around it.</summary>
    public PickShape LastShape { get; private set; }
    public bool PickEmptyCeilings { get; set; }

    /// <summary>
    /// DV356: while the T-ray shows what is inside the walls, the flat things in the wall cell the ray hits (the pipes, valves and meters tg draws over
    /// the wall) are picked through the wall's face or top, up to where the ray leaves the cell; the wall itself is the hit when the ray misses them.
    /// </summary>
    public bool SeeIntoWalls { get; set; }

    /// <inheritdoc />
    public PickHit Pick(in PickRay ray, long exclude, bool entitiesOnly = false) => Pick(OnOwnLevel(ray), exclude, entitiesOnly, 0);

    // D325: a ray whose eye is over its level's ceiling (the third-person camera, the free one) starts in the linked level the eye is in, so the walls and floors
    // there are seen: from the level of the own mob it passed under them and hit the top of the lowest wall of a stack (the owner, 2026-11-10)
    private PickRay OnOwnLevel(PickRay ray)
    {
        double level = _world.LevelHeight;
        int x = PickGrid.CellOf(ray.OriginEast), y = PickGrid.CellOf(ray.OriginNorth);
        for (int guard = 0; guard < _world.Levels; guard++)
        {
            if (ray.OriginUp >= level && ray.Z < _world.Levels && _world.LevelAbove(x, y, ray.Z))
                ray = ray with { Z = ray.Z + 1, OriginUp = ray.OriginUp - level };
            else if (ray.OriginUp < 0 && ray.Z > 1 && _world.LevelAbove(x, y, ray.Z - 1))
                ray = ray with { Z = ray.Z - 1, OriginUp = ray.OriginUp + level };
            else
                break;
        }
        return ray;
    }

    private PickHit Pick(in PickRay ray, long exclude, bool entitiesOnly, int depth)
    {
        double wall = _world.WallHeight;
        double max = ray.MaxMetres;
        LastShape = null;
        if (max <= 0)
            return PickHit.None;

        // The floor and the ceiling end the ray (the eye is between them).
        double tLimit = max;
        PickSurface limitSurface = PickSurface.None;
        if (ray.DirUp < -1e-9)
        {
            double tf = -ray.OriginUp / ray.DirUp;
            if (tf > 0 && tf <= tLimit)
            {
                tLimit = tf;
                limitSurface = PickSurface.Floor;
            }
        }
        else if (ray.DirUp > 1e-9)
        {
            double tc = (wall - ray.OriginUp) / ray.DirUp;
            if (tc > 0 && tc <= tLimit)
            {
                tLimit = tc;
                limitSurface = PickSurface.Ceiling;
            }
        }

        _shapes.Clear();
        _world.CollectMoving(ray, _shapes);

        // Walk the cells along the ground track (Amanatides and Woo) until the first wall or the limit; a cell's static shapes join the candidates.
        double gx = ray.OriginEast / PickGrid.Tile + 1.0, gy = ray.OriginNorth / PickGrid.Tile + 1.0;
        double dx = ray.DirEast / PickGrid.Tile, dy = ray.DirNorth / PickGrid.Tile;
        int cx = (int)Math.Floor(gx), cy = (int)Math.Floor(gy);
        int stepX = dx > 0 ? 1 : -1, stepY = dy > 0 ? 1 : -1;
        double tNextX = Math.Abs(dx) < 1e-12 ? double.MaxValue : (dx > 0 ? cx + 1 - gx : gx - cx) / Math.Abs(dx);
        double tNextY = Math.Abs(dy) < 1e-12 ? double.MaxValue : (dy > 0 ? cy + 1 - gy : gy - cy) / Math.Abs(dy);
        double tDeltaX = Math.Abs(dx) < 1e-12 ? double.MaxValue : 1.0 / Math.Abs(dx);
        double tDeltaY = Math.Abs(dy) < 1e-12 ? double.MaxValue : 1.0 / Math.Abs(dy);
        double tEnter = 0;
        // A ray that starts outside the map (the map editor's camera beside a small map) is walked from where it enters the map's rectangle: walked from its own
        // cell it left the walk at once, and passed through every wall and thing to the floor behind them.
        bool reaches = true;
        if (cx < 0 || cy < 0 || cx > _world.Width + 1 || cy > _world.Height + 1)
        {
            double t0 = 0, t1 = tLimit;
            reaches = ClipToSpan(gx, dx, _world.Width + 2, ref t0, ref t1) && ClipToSpan(gy, dy, _world.Height + 2, ref t0, ref t1);
            if (reaches)
            {
                tEnter = t0;
                double at = t0 + 1e-6;
                cx = Math.Clamp((int)Math.Floor(gx + dx * at), 0, _world.Width + 1);
                cy = Math.Clamp((int)Math.Floor(gy + dy * at), 0, _world.Height + 1);
                if (Math.Abs(dx) >= 1e-12)
                    tNextX = ((dx > 0 ? cx + 1 : cx) - gx) / dx;
                if (Math.Abs(dy) >= 1e-12)
                    tNextY = ((dy > 0 ? cy + 1 : cy) - gy) / dy;
            }
        }
        bool wallHit = false;
        double tWall = 0, tInWall = 0;
        bool wallThroughX = false;
        bool wallTop = false;
        int wallX = 0, wallY = 0;
        int z = ray.Z;
        for (int guard = 0; reaches && guard < _world.Width + _world.Height + 8; guard++)
        {
            bool inMap = cx >= 1 && cy >= 1 && cx <= _world.Width && cy <= _world.Height;
            if (inMap)
            {
                if (_world.IsWallCell(cx, cy, z))
                {
                    // At the eye's height a wall is hit where the ray enters its cell (its face). The third-person camera hangs above
                    // the walls: a ray that enters a wall cell above the wall hits its top if it comes down to it within the cell, or
                    // passes over it.
                    double uIn = ray.OriginUp + ray.DirUp * tEnter;
                    // (a ray come up from the level below passes under the walls of this one until it rises through its open space)
                    if (tEnter > 0 && uIn <= wall && uIn >= -1e-6)
                    {
                        wallHit = true;
                        tWall = tEnter;
                        wallX = cx;
                        wallY = cy;
                        CollectInWall(cx, cy, z, Math.Min(tNextX, tNextY), ref tInWall);
                        break;
                    }
                    if (uIn > wall && ray.DirUp < -1e-9)
                    {
                        double tTop = (wall - ray.OriginUp) / ray.DirUp;
                        if (tTop <= Math.Min(Math.Min(tNextX, tNextY), tLimit))
                        {
                            wallHit = true;
                            wallTop = true;
                            tWall = tTop;
                            wallX = cx;
                            wallY = cy;
                            CollectInWall(cx, cy, z, Math.Min(tNextX, tNextY), ref tInWall);
                            break;
                        }
                    }
                }
                _world.CollectStatic(cx, cy, z, _shapes);
            }
            double next = Math.Min(tNextX, tNextY);
            if (next > tLimit || next == double.MaxValue)
                break;
            tEnter = next;
            if (tNextX < tNextY)
            {
                tNextX += tDeltaX;
                cx += stepX;
                wallThroughX = true;
            }
            else
            {
                tNextY += tDeltaY;
                cy += stepY;
                wallThroughX = false;
            }
            if (cx < 0 || cy < 0 || cx > _world.Width + 1 || cy > _world.Height + 1)
                break;
        }
        double tStop = wallHit ? Math.Min(Math.Max(tWall, tInWall), tLimit) : tLimit;

        // The nearest shape before the stop.
        PickShape best = null;
        double bestT = tStop, bestE = 0, bestN = 0, bestU = 0;
        byte bestIx = 0, bestIy = 0;
        foreach (PickShape shape in _shapes)
        {
            if (shape.EntityId == exclude || shape.TileZ != z)
                continue;
            if (shape.TryHit(ray, bestT, out double t, out double e, out double n, out double u, out byte ix, out byte iy) && t <= bestT)
            {
                best = shape;
                bestT = t;
                bestE = e;
                bestN = n;
                bestU = u;
                bestIx = ix;
                bestIy = iy;
            }
        }
        // A shape drawn over its tile's things (PickShape.OnTop: an item on a rack's shelf) takes the click from a nearer hit on its tile (the rack's picture),
        // as BYOND gives it to the top layer, unless a thing of another tile stands between. A piece (a windoor shut before the rack, a window) keeps it.
        // A model's box (PickShape.Yields) gives it to any other thing of its tile the ray meets, but a piece: that door or window is behind it, or it would be the nearest hit.
        if (best != null && !best.OnTop && (best.Surface != PickSurface.Piece || best.Yields))
        {
            PickShape top = null;
            double topT = tStop, topE = 0, topN = 0, topU = 0;
            byte topIx = 0, topIy = 0;
            foreach (PickShape shape in _shapes)
            {
                if (!(shape.OnTop || best.Yields && !shape.Yields && shape.Surface != PickSurface.Piece && shape.EntityId != best.EntityId) || shape.EntityId == exclude || !SameTile(shape, best))
                    continue;
                if (shape.TryHit(ray, topT, out double t, out double e, out double n, out double u, out byte ix, out byte iy) && t <= topT)
                    (top, topT, topE, topN, topU, topIx, topIy) = (shape, t, e, n, u, ix, iy);
            }
            if (top != null && !OtherTileBefore(ray, best, topT, exclude, z))
                (best, bestT, bestE, bestN, bestU, bestIx, bestIy) = (top, topT, topE, topN, topU, topIx, topIy);
        }
        if (best != null)
        {
            LastShape = best;
            return new PickHit(best.Surface, TargetKind.Entity, best.EntityId, best.TileX, best.TileY, best.TileZ, bestT, bestE, bestN, bestU, bestIx, bestIy);
        }
        // D325: a ray that reaches the floor over open space goes on into the level below, one that reaches a cell with no ceiling under a level into the level
        // above (through its open space) or hits that level's turf, whose underside is the ceiling (the owner: hovering, examining and pointing see the other levels)
        if (!(wallHit && tWall <= tLimit) && limitSurface != PickSurface.None && depth < _world.Levels)
        {
            double e = ray.OriginEast + ray.DirEast * tLimit, n = ray.OriginNorth + ray.DirNorth * tLimit;
            int fx = PickGrid.CellOf(e), fy = PickGrid.CellOf(n);
            if (fx >= 1 && fy >= 1 && fx <= _world.Width && fy <= _world.Height && !_world.IsWallCell(fx, fy, z))
            {
                if (limitSurface == PickSurface.Floor && z > 1 && _world.OpenBelow(fx, fy, z))
                    return Pick(ray with { Z = z - 1, OriginUp = ray.OriginUp + _world.LevelHeight }, exclude, entitiesOnly, depth + 1);
                // (a ceiling tool wants the empty ceiling itself, not what is above it: on a lower level the pick went on up to the next level's turf)
                if (limitSurface == PickSurface.Ceiling && !PickEmptyCeilings && z < _world.Levels && !_world.HasCeiling(fx, fy, z) && _world.LevelAbove(fx, fy, z))
                {
                    if (_world.OpenBelow(fx, fy, z + 1))
                        return Pick(ray with { Z = z + 1, OriginUp = ray.OriginUp - _world.LevelHeight }, exclude, entitiesOnly, depth + 1);
                    if (!entitiesOnly)
                        return new PickHit(_world.IsWallCell(fx, fy, z + 1) ? PickSurface.Wall : PickSurface.Floor, TargetKind.Turf, 0, fx, fy, z + 1, tLimit, e, n, 0,
                            PickGrid.PixelIn(e, fx), PickGrid.PixelIn(n, fy));
                }
            }
        }
        if (entitiesOnly)
            return PickHit.None;

        // Else the cell the ray ended in: a wall face, the floor or the ceiling.
        if (wallHit && tWall <= tLimit)
        {
            double e = ray.OriginEast + ray.DirEast * tWall, n = ray.OriginNorth + ray.DirNorth * tWall, u = ray.OriginUp + ray.DirUp * tWall;
            byte ix = wallTop ? PickGrid.PixelIn(e, wallX) : wallThroughX ? PickGrid.PixelIn(n, wallY) : PickGrid.PixelIn(e, wallX);
            byte iy = wallTop ? PickGrid.PixelIn(n, wallY) : (byte)Math.Clamp((int)Math.Ceiling(u / wall * 32.0), 1, 32);
            return new PickHit(PickSurface.Wall, TargetKind.Turf, 0, wallX, wallY, z, tWall, e, n, u, ix, iy);
        }
        if (limitSurface != PickSurface.None)
        {
            double e = ray.OriginEast + ray.DirEast * tLimit, n = ray.OriginNorth + ray.DirNorth * tLimit;
            int fx = PickGrid.CellOf(e), fy = PickGrid.CellOf(n);
            if (fx < 1 || fy < 1 || fx > _world.Width || fy > _world.Height || _world.IsWallCell(fx, fy, z))
                return PickHit.None;
            if (limitSurface == PickSurface.Ceiling && !_world.HasCeiling(fx, fy, z) && !PickEmptyCeilings)
                return PickHit.None;
            return new PickHit(limitSurface, limitSurface == PickSurface.Ceiling ? TargetKind.Ceiling : TargetKind.Turf, 0, fx, fy, z, tLimit, e, n, limitSurface == PickSurface.Floor ? 0 : wall, PickGrid.PixelIn(e, fx), PickGrid.PixelIn(n, fy));
        }
        return PickHit.None;
    }

    // The part of a ray's way (t0 to t1) on which one of its grid coordinates is inside 0..span; false when there is none.
    private static bool ClipToSpan(double origin, double step, double span, ref double t0, ref double t1)
    {
        if (Math.Abs(step) < 1e-12)
            return origin >= 0 && origin < span;
        double a = (0 - origin) / step, b = (span - origin) / step;
        (t0, t1) = (Math.Max(t0, Math.Min(a, b)), Math.Min(t1, Math.Max(a, b)));
        return t0 <= t1;
    }

    // DV356: the wall cell's flat things join the candidates, and the ray may reach them until it leaves the cell (tExit).
    private void CollectInWall(int x, int y, int z, double tExit, ref double tInWall)
    {
        if (!SeeIntoWalls)
            return;
        _inWall.Clear();
        _world.CollectStatic(x, y, z, _inWall);
        foreach (PickShape shape in _inWall)
        {
            if (shape.Kind != PickShapeKind.Flat)
                continue;
            _shapes.Add(shape);
            tInWall = tExit;
        }
    }

    private static bool SameTile(PickShape a, PickShape b) => a.TileX == b.TileX && a.TileY == b.TileY && a.TileZ == b.TileZ;

    // True when a shape of another tile than tile's is hit before t.
    private bool OtherTileBefore(in PickRay ray, PickShape tile, double t, long exclude, int z)
    {
        foreach (PickShape shape in _shapes)
        {
            if (shape.EntityId == exclude || shape.TileZ != z || SameTile(shape, tile))
                continue;
            if (shape.TryHit(ray, t, out double hit, out _, out _, out _, out _, out _) && hit < t)
                return true;
        }
        return false;
    }

    /// <summary>tg's view radius, 7 tiles (the server's interaction view range): the farthest turf a BYOND player can click on the screen.</summary>
    public const double ViewRange = 14.0;

    /// <summary>
    /// The pick for throw mode (interaction.md 4.1): the nearest hit within <see cref="ThrowRange"/>; with none, the floor cell where the ray
    /// meets the floor, or the cell at that range along the view. The result is a turf or an entity; <see cref="PickHit.None"/> only outside the map.
    /// </summary>
    public PickHit PickThrow(in PickRay ray, long exclude) => PickToRange(ray, exclude, ThrowRange);

    /// <summary>
    /// The click's pick when the ordinary <see cref="Pick"/> finds nothing within its range (the space play test, 2026-10-15): BYOND's screen
    /// shows every turf in view and a click always lands on one, so tg's ranged items (the extinguisher sprays toward a clicked space or
    /// distant floor turf and pushes its user the other way in zero gravity) get a target. The nearest hit within <see cref="ViewRange"/>, else
    /// the floor cell the ray meets, else the cell at that range along the view (a space turf over the starfield). The server decides reach.
    /// </summary>
    public PickHit PickInView(in PickRay ray, long exclude) => PickToRange(ray, exclude, ViewRange);

    /// <summary>Point at the first visible surface over the full view range, independently of interaction reach.</summary>
    public PickHit PickPoint(in PickRay ray, long exclude, double cameraLead = 0) => PickToRange(ray, exclude, cameraLead + ViewRange);

    /// <summary>Distance along an orbit camera's ray to the plane through the character's eye.</summary>
    public static double CameraLead(in PickRay ray, double eyeEast, double eyeNorth, double eyeUp)
        => Math.Max(0, (eyeEast - ray.OriginEast) * ray.DirEast + (eyeNorth - ray.OriginNorth) * ray.DirNorth + (eyeUp - ray.OriginUp) * ray.DirUp);

    /// <summary>
    /// <see cref="Pick"/> within <paramref name="range"/> metres, with the throw pick's fallback (the floor cell the ray meets, else the cell at
    /// the range): the third-person view's pick, whose ray starts at the camera some metres behind the own mob.
    /// </summary>
    public PickHit PickToRangeFrom(in PickRay ray, long exclude, double range) => PickToRange(ray, exclude, range);

    private PickHit PickToRange(in PickRay ray, long exclude, double range)
    {
        var longRay = ray with { MaxMetres = range };
        PickHit hit = Pick(longRay, exclude);
        if (hit.IsHit)
            return hit;
        double t = range;
        if (ray.DirUp < -1e-9)
        {
            double tf = -ray.OriginUp / ray.DirUp;
            if (tf > 0 && tf < range)
                t = tf;
        }
        double e = ray.OriginEast + ray.DirEast * t, n = ray.OriginNorth + ray.DirNorth * t;
        int x = Math.Clamp(PickGrid.CellOf(e), 1, _world.Width), y = Math.Clamp(PickGrid.CellOf(n), 1, _world.Height);
        // a wall cell the ray passed over (it rose out of the level before it) is still the click's turf: a gun fires along the aim whatever the target
        return new PickHit(_world.IsWallCell(x, y, ray.Z) ? PickSurface.Wall : PickSurface.Floor, TargetKind.Turf, 0, x, y, ray.Z, t, e, n, 0, PickGrid.PixelIn(e, x), PickGrid.PixelIn(n, y));
    }

    /// <summary>
    /// The reach mirror of the crosshair (interaction.md 4.3): tg's <c>Adjacent</c> on tiles, without the border details: the target's tile is the
    /// own tile or a neighbour on the same level, or (DV425, <paramref name="opensUp"/>: the own tile opens into the level above) on the level
    /// above. It never stops a click; the server decides.
    /// </summary>
    public static bool InReach(int ownTileX, int ownTileY, int ownZ, in PickHit hit, bool opensUp = false)
        => hit.IsHit && hit.Kind != TargetKind.Screen && (hit.Z == ownZ || opensUp && hit.Z == ownZ + 1) && Math.Abs(hit.X - ownTileX) <= 1 && Math.Abs(hit.Y - ownTileY) <= 1;

    /// <summary>DV425: whether the cell opens into the level above (no ceiling, a level there whose turf over it is open space): the reach goes up through it.</summary>
    public bool OpensUp(int x, int y, int z)
        => z >= 1 && z < _world.Levels && x >= 1 && y >= 1 && x <= _world.Width && y <= _world.Height && !_world.HasCeiling(x, y, z) && _world.LevelAbove(x, y, z) && _world.OpenBelow(x, y, z + 1);
}
