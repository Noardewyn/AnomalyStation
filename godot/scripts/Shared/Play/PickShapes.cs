using Anomaly.Client.Shared.Icons;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared;

// Owner: M4a P7 (client interaction). Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).
// docs/ai/specs/interaction.md 4.1-4.2: the shapes the world pick tests a ray against, in the pick's own frame: metres east, north
// and up from the floor of the level (a tile (x, y) spans east (x - 1) * 2 .. x * 2, north (y - 1) * 2 .. y * 2).

/// <summary>What a <see cref="PickShape"/> is.</summary>
public enum PickShapeKind : byte
{
    /// <summary>A picture on a vertical quad that is turned to the viewer about the vertical axis (a mob, an item, a static prop): the opaque pixel counts.</summary>
    Standing = 0,

    /// <summary>A picture lying flat at a height, its up pointing north (a lying mob).</summary>
    Flat = 1,

    /// <summary>An axis-aligned box (a D113 piece: a door's panels, a window's slab, a table top, a decal's slab).</summary>
    Box = 2,
}

/// <summary>The tile (x, y) a point of the pick's frame is in.</summary>
public static class PickGrid
{
    /// <summary>Metres per tile.</summary>
    public const double Tile = 2.0;

    public static int CellOf(double metres) => (int)Math.Floor(metres / Tile) + 1;

    /// <summary>tg's icon pixel (1 to 32) of a position within a tile: <paramref name="metres"/> from the tile's west or south edge, rounded up.</summary>
    public static byte Pixel(double metresFromEdge) => (byte)Math.Clamp((int)Math.Ceiling(metresFromEdge / Tile * 32.0), 1, 32);

    /// <summary>tg's icon pixel of an absolute coordinate within tile <paramref name="cell"/>.</summary>
    public static byte PixelIn(double metres, int cell) => Pixel(metres - (cell - 1) * Tile);

    /// <summary>
    /// The cell just in front of the face <paramref name="hit"/> struck, a step back along the ray (east, north, up): the cell over a top, the one beside a
    /// side, the one under a ceiling (or under the floor of the level above), a floor's own. Its level may be past the map's (over the top level): the caller checks.
    /// </summary>
    public static (int X, int Y, int Z) InFront(in PickHit hit, double dirEast, double dirNorth, double dirUp, double levelHeight)
    {
        const double back = 0.05;
        double up = hit.HitUp - dirUp * back;
        return (CellOf(hit.HitEast - dirEast * back), CellOf(hit.HitNorth - dirNorth * back), hit.Z + (int)Math.Floor(up / levelHeight));
    }
}

/// <summary>
/// A thing the world pick can hit, made by the renderers' adapters from what they draw (the pick itself stays engine-free):
/// a billboard's picture, a static prop's quad, a piece's box. Mutable so that an adapter keeps one per thing.
/// </summary>
public sealed class PickShape
{
    /// <summary>The entity a hit names.</summary>
    public long EntityId;

    /// <summary>What a hit is (<see cref="PickSurface.Billboard"/>, <see cref="PickSurface.StaticProp"/> or <see cref="PickSurface.Piece"/>).</summary>
    public PickSurface Surface;

    public PickShapeKind Kind;

    /// <summary>The entity's tile (what a hit reports as its cell).</summary>
    public int TileX, TileY, TileZ;

    /// <summary>Standing: the foot centre of the picture's icon square; Flat: the square's centre (east, north) at height <see cref="Up"/>.</summary>
    public double East, North, Up;

    /// <summary>The picture's rectangle in metres around <see cref="East"/>/<see cref="North"/> (<see cref="SpriteRect.Of"/>, <see cref="SpriteRect.OfFlat"/>, <see cref="SpriteRect.AsItem"/>).</summary>
    public SpriteRect Rect;

    /// <summary>The picture (its alpha decides a hit); null: the whole rectangle counts.</summary>
    public Raster Image;

    /// <summary>
    /// Package T, slice T1: a raster of <see cref="Image"/>'s size whose alpha decides a hit instead of the picture's (a mob built from body parts: its
    /// body alone, <see cref="BodyZoneAim.BodyMask"/>); null: the picture's. The picture still is what is drawn and highlighted.
    /// </summary>
    public Raster HitMask;

    /// <summary>Where the atom's own 32 x 32 icon square sits in <see cref="Image"/>, from the lower left (pixels): tg's icon-x and icon-y count from it.</summary>
    public int OriginX, OriginY;

    /// <summary>The box, for <see cref="PickShapeKind.Box"/>.</summary>
    public double MinEast, MinNorth, MinUp, MaxEast, MaxNorth, MaxUp;

    /// <summary>
    /// Drawn over its cell's other things (an item on a rack's shelf, tg's item layer over the rack's): a hit on it wins against a nearer hit on a picture or
    /// a model of its own tile that is not so (never a piece: a door, a window), as BYOND gives a click to the top layer's opaque pixel.
    /// </summary>
    public bool OnTop;

    /// <summary>
    /// A 3D model's box (a chair, a table, a machine): it holds more than its model, so any other thing of its tile but a piece that the ray meets (a mob sitting on the
    /// chair, an item on the table) takes the click from it, as BYOND's layers give it to what is drawn over the structure.
    /// </summary>
    public bool Yields;

    /// <summary>The height of the level's walls (a box's vertical icon pixels are its share of it).</summary>
    public double WallHeight = 2.0;

    /// <summary>A standing picture that faces the eye fully (an item: tilted back about its feet, its up the camera's up), not turned about the vertical axis only.</summary>
    public bool FacesEye;

    /// <summary>How much nearer the eye a picture that faces it is picked (metres along the view): an item's draw-order lift (DepthRank.LayerStep at its layer).</summary>
    public const double FacingLift = 0.05;

    /// <summary>A standing picture.</summary>
    public static PickShape StandingPicture(long id, PickSurface surface, int tileX, int tileY, int z, double east, double north, double up, SpriteRect rect, Raster image, int originX, int originY)
        => new() { EntityId = id, Surface = surface, Kind = PickShapeKind.Standing, TileX = tileX, TileY = tileY, TileZ = z, East = east, North = north, Up = up, Rect = rect, Image = image, OriginX = originX, OriginY = originY };

    /// <summary>A picture lying flat at height <paramref name="up"/>.</summary>
    public static PickShape FlatPicture(long id, PickSurface surface, int tileX, int tileY, int z, double east, double north, double up, SpriteRect rect, Raster image, int originX, int originY)
        => new() { EntityId = id, Surface = surface, Kind = PickShapeKind.Flat, TileX = tileX, TileY = tileY, TileZ = z, East = east, North = north, Up = up, Rect = rect, Image = image, OriginX = originX, OriginY = originY };

    /// <summary>A box.</summary>
    public static PickShape BoxOf(long id, PickSurface surface, int tileX, int tileY, int z, double minEast, double minNorth, double minUp, double maxEast, double maxNorth, double maxUp, double wallHeight)
        => new() { EntityId = id, Surface = surface, Kind = PickShapeKind.Box, TileX = tileX, TileY = tileY, TileZ = z, MinEast = minEast, MinNorth = minNorth, MinUp = minUp, MaxEast = maxEast, MaxNorth = maxNorth, MaxUp = maxUp, WallHeight = wallHeight };

    /// <summary>
    /// Where <paramref name="ray"/> (a unit direction) first hits the shape within <paramref name="maxT"/>: the distance, the point and tg's icon-x and
    /// icon-y of it. A standing picture is turned to the viewer about the vertical axis (its plane is vertical and square to the ray's ground track).
    /// </summary>
    public bool TryHit(in PickRay ray, double maxT, out double t, out double east, out double north, out double up, out byte iconX, out byte iconY)
        => TryHit(ray, maxT, out t, out east, out north, out up, out iconX, out iconY, out _, out _);

    /// <summary>
    /// <see cref="TryHit(in PickRay, double, out double, out double, out double, out double, out byte, out byte)"/>, with the picture's pixel in tg's
    /// icon coordinates before they are clamped into the icon square (<paramref name="pictureX"/>, <paramref name="pictureY"/>: a giant's or a lying
    /// body's picture reaches past it, package T slice T1); a box gives its icon pixel.
    /// </summary>
    public bool TryHit(in PickRay ray, double maxT, out double t, out double east, out double north, out double up, out byte iconX, out byte iconY, out int pictureX, out int pictureY)
    {
        if (Kind != PickShapeKind.Standing && Kind != PickShapeKind.Flat)
        {
            bool box = HitBox(ray, maxT, out t, out east, out north, out up, out iconX, out iconY);
            (pictureX, pictureY) = (iconX, iconY);
            return box;
        }
        bool hit = Kind == PickShapeKind.Standing
            ? HitStanding(ray, maxT, out t, out east, out north, out up, out pictureX, out pictureY)
            : HitFlat(ray, maxT, out t, out east, out north, out up, out pictureX, out pictureY);
        iconX = hit ? (byte)Math.Clamp(pictureX, 1, 32) : (byte)0;
        iconY = hit ? (byte)Math.Clamp(pictureY, 1, 32) : (byte)0;
        return hit;
    }

    private bool HitStanding(in PickRay ray, double maxT, out double t, out double east, out double north, out double up, out int pictureX, out int pictureY)
    {
        t = 0;
        east = north = up = 0;
        pictureX = pictureY = 0;
        if (FacesEye)
        {
            // An item faces the eye: its plane is square to the camera's forward f (the ray's view; none: the ray itself, through the screen's middle),
            // its x along the camera's right r on the ground (the ray's; none: square to f's ground track), its up the camera's: back x right, as the shader's.
            bool view = ray.ViewEast != 0 || ray.ViewNorth != 0 || ray.ViewUp != 0;
            double fe = view ? ray.ViewEast : ray.DirEast, fn = view ? ray.ViewNorth : ray.DirNorth, fu = view ? ray.ViewUp : ray.DirUp;
            bool given = ray.RightEast != 0 || ray.RightNorth != 0;
            double re = given ? ray.RightEast : fn, rn = given ? ray.RightNorth : -fe, hr = Math.Sqrt(re * re + rn * rn);
            double facing = ray.DirEast * fe + ray.DirNorth * fn + ray.DirUp * fu;
            if (hr < 1e-9 || facing < 1e-9)
                return false;
            (re, rn) = (re / hr, rn / hr);
            // back x right in Godot's frame (x east, y up, z south), back = -f, right = (re, 0, -rn), read back as east, north, up
            double ue = fu * rn, un = -fu * re, uu = fn * re - fe * rn, hu = Math.Sqrt(ue * ue + un * un + uu * uu);
            if (hu < 1e-9)
                return false;
            // the plane FacingLift nearer the eye, as the shader draws it (depth_order.gdshaderinc's rank): seen straight down (the editor's top view) the
            // picture lies in the floor's plane, and a pick that stops at the floor lost it there
            t = ((East - ray.OriginEast) * fe + (North - ray.OriginNorth) * fn + (Up - ray.OriginUp) * fu - FacingLift) / facing;
            if (t <= 0 || t > maxT)
                return false;
            east = ray.OriginEast + ray.DirEast * t;
            north = ray.OriginNorth + ray.DirNorth * t;
            up = ray.OriginUp + ray.DirUp * t;
            double de = east - East + fe * FacingLift, dn = north - North + fn * FacingLift, du = up - Up + fu * FacingLift;
            return Inside(de * re + dn * rn, (de * ue + dn * un + du * uu) / hu, out pictureX, out pictureY);
        }
        double h = Math.Sqrt(ray.DirEast * ray.DirEast + ray.DirNorth * ray.DirNorth);
        if (h < 1e-9)
            return false;
        // The plane's normal is the ray's ground track; the picture's x runs along the viewer's right, (north, -east) of the track.
        double ne = ray.DirEast / h, nn = ray.DirNorth / h;
        t =((East - ray.OriginEast) * ne + (North - ray.OriginNorth) * nn) / h;
        if (t <= 0 || t > maxT)
            return false;
        east = ray.OriginEast + ray.DirEast * t;
        north = ray.OriginNorth + ray.DirNorth * t;
        up = ray.OriginUp + ray.DirUp * t;
        double u = (east - East) * nn + (north - North) * -ne;
        double v = up - Up;
        return Inside(u, v, out pictureX, out pictureY);
    }

    private bool HitFlat(in PickRay ray, double maxT, out double t, out double east, out double north, out double up, out int pictureX, out int pictureY)
    {
        t = 0;
        east = north = up = 0;
        pictureX = pictureY = 0;
        if (Math.Abs(ray.DirUp) < 1e-9)
            return false;
        t = (Up - ray.OriginUp) / ray.DirUp;
        if (t <= 0 || t > maxT)
            return false;
        east = ray.OriginEast + ray.DirEast * t;
        north = ray.OriginNorth + ray.DirNorth * t;
        up = Up;
        return Inside(east - East, north - North, out pictureX, out pictureY);
    }

    // (u, v) in metres in the picture's own frame: the rectangle and the picture's opaque pixel decide, and tg's pixel within the icon square follows.
    private bool Inside(double u, double v, out int pictureX, out int pictureY)
    {
        pictureX = pictureY = 0;
        if (u < Rect.Left || u >= Rect.Right || v < Rect.Bottom || v >= Rect.Top)
            return false;
        double pixel = Rect.PixelSize;
        Raster test = HitMask ?? Image;
        if (test != null)
        {
            int col = (int)Math.Floor((u - Rect.Left) / pixel);
            int fromBottom = (int)Math.Floor((v - Rect.Bottom) / pixel);
            if (col < 0 || col >= test.Width || fromBottom < 0 || fromBottom >= test.Height)
                return false;
            int row = test.Height - 1 - fromBottom;
            if (test.Pixels[(row * test.Width + col) * 4 + 3] < BillboardMath.PickAlpha)
                return false;
        }
        // The icon square's lower left corner is OriginX, OriginY pixels in from the rectangle's.
        double squareLeft = Rect.Left + OriginX * pixel, squareBottom = Rect.Bottom + OriginY * pixel;
        pictureX = (int)Math.Floor((u - squareLeft) / pixel) + 1;
        pictureY = (int)Math.Floor((v - squareBottom) / pixel) + 1;
        return true;
    }

    private bool HitBox(in PickRay ray, double maxT, out double t, out double east, out double north, out double up, out byte iconX, out byte iconY)
    {
        t = 0;
        east = north = up = 0;
        iconX = iconY = 0;
        double tMin = 0, tMax = maxT;
        int axis = -1;
        double sign = 0;
        for (int a = 0; a < 3; a++)
        {
            double o = a == 0 ? ray.OriginEast : a == 1 ? ray.OriginNorth : ray.OriginUp;
            double d = a == 0 ? ray.DirEast : a == 1 ? ray.DirNorth : ray.DirUp;
            double lo = a == 0 ? MinEast : a == 1 ? MinNorth : MinUp;
            double hi = a == 0 ? MaxEast : a == 1 ? MaxNorth : MaxUp;
            if (Math.Abs(d) < 1e-12)
            {
                if (o < lo || o > hi)
                    return false;
                continue;
            }
            double t1 = (lo - o) / d, t2 = (hi - o) / d;
            double s = d > 0 ? -1 : 1; // the face the ray enters through: -axis when moving up the axis
            if (t1 > t2)
                (t1, t2) = (t2, t1);
            if (t1 > tMin)
            {
                tMin = t1;
                axis = a;
                sign = s;
            }
            tMax = Math.Min(tMax, t2);
            if (tMin > tMax)
                return false;
        }
        if (axis < 0 || tMin <= 1e-6)
            return false; // the eye is inside the box
        t = tMin;
        east = ray.OriginEast + ray.DirEast * t;
        north = ray.OriginNorth + ray.DirNorth * t;
        up = ray.OriginUp + ray.DirUp * t;
        _ = sign;
        // tg's pixel of the face hit: a top or bottom face is the tile-relative point (east is x, north is y); a side face is the position along it and the height.
        byte height = (byte)Math.Clamp((int)Math.Ceiling(up / WallHeight * 32.0), 1, 32);
        if (axis == 2)
        {
            iconX = PickGrid.PixelIn(east, TileX);
            iconY = PickGrid.PixelIn(north, TileY);
        }
        else if (axis == 1)
        {
            iconX = PickGrid.PixelIn(east, TileX);
            iconY = height;
        }
        else
        {
            iconX = PickGrid.PixelIn(north, TileY);
            iconY = height;
        }
        return true;
    }
}
