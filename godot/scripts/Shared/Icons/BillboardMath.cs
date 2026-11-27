namespace Anomaly.Client.Shared.Icons;

// Owner: M2 P7. The geometry of the billboards (D104): which frame a viewer sees, where the picture's rectangle is, and where a ray
// hits it. Engine-free, in plain doubles: Godot's vectors are converted at the edge (godot/scripts/Render/).

/// <summary>A point or direction in the client's world space (X east, Y up, Z south: TileSpace's convention).</summary>
public readonly record struct Vec3(double X, double Y, double Z)
{
    public static Vec3 operator +(Vec3 a, Vec3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    public static Vec3 operator -(Vec3 a, Vec3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    public static Vec3 operator *(Vec3 a, double s) => new(a.X * s, a.Y * s, a.Z * s);

    public double Dot(Vec3 b) => X * b.X + Y * b.Y + Z * b.Z;

    public double Length => Math.Sqrt(Dot(this));
}

/// <summary>The rectangle a billboard's picture covers, in metres around the feet: where the atom's own 32 x 32 square sits in the composed picture.</summary>
public readonly record struct SpriteRect(double Left, double Right, double Bottom, double Top, double PixelSize)
{
    /// <summary>Metres per tg pixel: 32 pixels are one 2 m tile.</summary>
    public const double MetresPerPixel = 2.0 / 32.0;

    /// <summary>
    /// The rectangle of a picture of <paramref name="pixelWidth"/> x <paramref name="pixelHeight"/> pixels whose icon square's lower left
    /// corner is at (<paramref name="originX"/>, <paramref name="originY"/> from the bottom): horizontally centred on the feet (the square's
    /// centre), the square's bottom edge on the floor. <paramref name="square"/> is the square's side in pixels (32).
    /// </summary>
    public static SpriteRect Of(int pixelWidth, int pixelHeight, int originX, int originY, int square = 32)
        => new(
            -(originX + square / 2.0) * MetresPerPixel,
            (pixelWidth - originX - square / 2.0) * MetresPerPixel,
            -originY * MetresPerPixel,
            (pixelHeight - originY) * MetresPerPixel,
            MetresPerPixel);

    /// <summary>
    /// The rectangle of the same picture lying flat on the floor, around the square's centre (north is up in the picture, as BYOND draws the
    /// map seen from above): Left and Right are east of the centre, Bottom and Top north of it.
    /// </summary>
    public static SpriteRect OfFlat(int pixelWidth, int pixelHeight, int originX, int originY, int square = 32)
        => new(
            -(originX + square / 2.0) * MetresPerPixel,
            (pixelWidth - originX - square / 2.0) * MetresPerPixel,
            -(originY + square / 2.0) * MetresPerPixel,
            (pixelHeight - originY - square / 2.0) * MetresPerPixel,
            MetresPerPixel);

    /// <summary>
    /// The rectangle of a standing ITEM (D121, M3 R3): the same picture drawn at <paramref name="scale"/> of the tile scale about the bottom centre of its DRAWN pixels, which rest on
    /// the floor (or the table top the node stands on): the picture's own vertical offset in its icon square is dropped (tg draws an item mid-frame for the top-down depth, not for height).
    /// <paramref name="trimBottomRows"/> is the number of fully clear rows under the drawn pixels.
    /// </summary>
    public SpriteRect AsItem(double scale, int trimBottomRows)
    {
        double drop = Bottom + trimBottomRows * PixelSize;
        return new SpriteRect(Left * scale, Right * scale, (Bottom - drop) * scale, (Top - drop) * scale, PixelSize * scale);
    }

    /// <summary>
    /// DV49 (M5x slice 2): whether a standing picture of this rectangle with the root's <paramref name="pixelX"/> and <paramref name="pixelY"/>
    /// is a big icon centred on its tile by its own offsets, as tg centres its explosion (96 x 96 at -32, -32) and the singularity's stages:
    /// wider or taller than the 32-pixel square, the offsets minus half the overhang. Such a picture's <c>pixel_y</c> is read as height (the
    /// picture's middle at half a tile up), not as the ground offset north that small pictures' offsets are (M3 R2).
    /// </summary>
    public bool IsCentredBigIcon(int pixelX, int pixelY)
    {
        double widthPx = Math.Round(Width / PixelSize), heightPx = Math.Round((Top - Bottom) / PixelSize);
        if (widthPx <= 32 && heightPx <= 32)
            return false;
        return pixelX == -(widthPx - 32) / 2 && pixelY == -(heightPx - 32) / 2;
    }

    /// <summary>
    /// DV49: whether a standing picture wider than the 32-pixel square is centred across its tile by the root's <paramref name="pixelX"/> (tg's
    /// trees, 64 pixels at -16; the colossus, 96 at -32 with its pixel_y 0: it stands on the floor). Such a pixel_x moves the picture across the
    /// view, not west on the ground: a ground offset leaves the picture's middle beside the point it turns about, so it circles that point.
    /// </summary>
    public bool IsCentredWide(int pixelX)
    {
        double widthPx = Math.Round(Width / PixelSize);
        return widthPx > 32 && pixelX == -(widthPx - 32) / 2;
    }

    /// <summary>The same rectangle <paramref name="k"/> times as big about its origin (the feet of a standing picture, the tile's centre of a flat one).</summary>
    public SpriteRect Times(double k) => k == 1 ? this : new SpriteRect(Left * k, Right * k, Bottom * k, Top * k, PixelSize * k);

    /// <summary>Width in metres.</summary>
    public double Width => Right - Left;

    /// <summary>Height in metres.</summary>
    public double Height => Top - Bottom;
}

/// <summary>The result of a ray against a billboard.</summary>
/// <param name="Distance">Along the (unit) ray.</param>
/// <param name="Point">The hit.</param>
public readonly record struct BillboardHit(double Distance, Vec3 Point);

/// <summary>Frame choice, placement and picking of billboards.</summary>
public static class BillboardMath
{
    /// <summary>The alpha above which a picture's pixel counts as the thing (a ray through a clear pixel misses).</summary>
    public const int PickAlpha = 40;

    /// <summary>
    /// The compass bearing in degrees, clockwise from north, of the viewer as seen from the mob (BYOND tile coordinates: x east, y north).
    /// </summary>
    public static double BearingToViewer(double mobX, double mobY, double viewerX, double viewerY)
    {
        double bearing = Math.Atan2(viewerX - mobX, viewerY - mobY) * 180.0 / Math.PI;
        return bearing < 0 ? bearing + 360.0 : bearing;
    }

    /// <summary>
    /// The BYOND dir whose frame the viewer sees (D104): the mob facing the viewer shows south, the viewer behind it north, at its right east,
    /// at its left west; an eight-direction icon uses eight 45 degree sectors, a four-direction one four 90 degree sectors, a still one south.
    /// <paramref name="yaw"/> is the mob's facing (256 per turn, clockwise from north); the positions are BYOND tile coordinates.
    /// </summary>
    public static byte ChooseDir(byte yaw, double mobX, double mobY, double viewerX, double viewerY, int directions)
    {
        if (directions <= 1)
            return ByondDir.South;
        double dx = viewerX - mobX, dy = viewerY - mobY;
        if (dx * dx + dy * dy < 1e-9)
            return ByondDir.South;
        double relative = BearingToViewer(mobX, mobY, viewerX, viewerY) - yaw * 360.0 / 256.0;
        relative = ((relative % 360.0) + 360.0) % 360.0;
        if (directions >= 8)
            return ByondDir.FrontClockwise[(int)Math.Floor((relative + 22.5) / 45.0) % 8];
        return ByondDir.FrontClockwise[((int)Math.Floor((relative + 45.0) / 90.0) % 4) * 2];
    }

    /// <summary>The yaw byte (256 per turn, clockwise from north) that faces a BYOND direction (for entities that only have a dir).</summary>
    public static byte YawOfDir(byte dir)
    {
        int dx = ((dir & ByondDir.East) != 0 ? 1 : 0) - ((dir & ByondDir.West) != 0 ? 1 : 0);
        int dy = ((dir & ByondDir.North) != 0 ? 1 : 0) - ((dir & ByondDir.South) != 0 ? 1 : 0);
        if (dx == 0 && dy == 0)
            return 128; // no direction: facing south
        double degrees = Math.Atan2(dx, dy) * 180.0 / Math.PI;
        if (degrees < 0)
            degrees += 360.0;
        return (byte)(int)Math.Round(degrees * 256.0 / 360.0);
    }

    /// <summary>
    /// The BYOND dir whose frame a LYING mob shows (the owner's play test, 2026-10-13). tg has one sprite per direction, turned a quarter for a
    /// lying body, so the frame decides which side of the body the viewer sees: a conscious body (knocked down, crawling) lies face down, so the
    /// viewer sees its back (north); a limp one (unconscious, in crit, dead) lies on its back or front by the way it fell: front (south) when the
    /// viewer is on the side it faces, back (north) when behind it. <paramref name="directions"/> is the icon's direction count.
    /// Slice T4c (DV573, DV686), before that rule: a body someone turned over shows the side it was turned to
    /// (<paramref name="turnedFaceUp"/>, the posture's <c>PostureBits.TurnedFaceUp</c>) from every side, and one lying <paramref name="buckled"/> to
    /// an object (a bed, an operating table) lies on its back, its front (south) seen from every side. The server mirrors it for shots
    /// (<c>ShotGeometry.ChooseLyingDir</c>).
    /// </summary>
    public static byte ChooseLyingDir(byte yaw, double mobX, double mobY, double viewerX, double viewerY, int directions, bool limp, bool buckled = false, bool? turnedFaceUp = null)
    {
        if (directions <= 1)
            return ByondDir.South;
        if (turnedFaceUp is bool up)
            return up ? ByondDir.South : ByondDir.North;
        if (buckled)
            return ByondDir.South;
        if (!limp)
            return ByondDir.North;
        double dx = viewerX - mobX, dy = viewerY - mobY;
        if (dx * dx + dy * dy < 1e-9)
            return ByondDir.South;
        double relative = BearingToViewer(mobX, mobY, viewerX, viewerY) - yaw * 360.0 / 256.0;
        return Math.Cos(relative * Math.PI / 180.0) >= -1e-9 ? ByondDir.South : ByondDir.North;
    }

    /// <summary>
    /// The BYOND dir whose frame an item on the floor shows (the owner's play test, 2026-10-29: a flashlight turned through its four pictures as
    /// one walked round it): its side, east when the way it faces points to the viewer's right, west when to the left, so it changes once as the
    /// viewer crosses its line; a still icon south.
    /// </summary>
    public static byte ChooseSideDir(byte yaw, double itemX, double itemY, double viewerX, double viewerY, int directions)
    {
        if (directions <= 1)
            return ByondDir.South;
        double relative = BearingToViewer(itemX, itemY, viewerX, viewerY) - yaw * 360.0 / 256.0;
        return Math.Sin(relative * Math.PI / 180.0) >= -1e-9 ? ByondDir.East : ByondDir.West;
    }

    /// <summary>
    /// A ray against a standing billboard: the rectangle spanned by <paramref name="right"/> (the horizontal unit vector the picture's x runs
    /// along: the camera's right) and world up, with <paramref name="feet"/> at the bottom of the icon square's centre. A hit needs an opaque
    /// pixel of <paramref name="image"/> (null: any point of the rectangle).
    /// </summary>
    public static bool RayStanding(Vec3 origin, Vec3 direction, double maxDistance, Vec3 feet, Vec3 right, SpriteRect rect, Raster image, out BillboardHit hit)
        => RayPlane(origin, direction, maxDistance, feet, right, new Vec3(0, 1, 0), rect, image, out hit);

    /// <summary>
    /// A ray against a picture spanned by the unit vectors <paramref name="right"/> and <paramref name="up"/> (square to each other) from <paramref name="feet"/>:
    /// a standing billboard's up is world up, an item's that faces the eye is the camera's up (<c>face_eye</c>).
    /// </summary>
    public static bool RayPlane(Vec3 origin, Vec3 direction, double maxDistance, Vec3 feet, Vec3 right, Vec3 up, SpriteRect rect, Raster image, out BillboardHit hit)
    {
        hit = default;
        // The plane's normal is right x up.
        var normal = new Vec3(right.Y * up.Z - right.Z * up.Y, right.Z * up.X - right.X * up.Z, right.X * up.Y - right.Y * up.X);
        double denominator = direction.Dot(normal);
        if (Math.Abs(denominator) < 1e-9)
            return false;
        double t = (feet - origin).Dot(normal) / denominator;
        if (t < 0 || t > maxDistance)
            return false;
        Vec3 point = origin + direction * t;
        Vec3 local = point - feet;
        double u = local.Dot(right), v = local.Dot(up);
        if (!Inside(rect, u, v, image))
            return false;
        hit = new BillboardHit(t, point);
        return true;
    }

    /// <summary>
    /// A ray against a lying picture (<paramref name="rect"/> from <see cref="SpriteRect.OfFlat"/>): the rectangle lies flat at <paramref name="floorY"/> around <paramref name="centre"/>, the picture's up
    /// pointing north (-Z) and its right east (+X), the way BYOND draws the map seen from above.
    /// </summary>
    public static bool RayLying(Vec3 origin, Vec3 direction, double maxDistance, Vec3 centre, double floorY, SpriteRect rect, Raster image, out BillboardHit hit)
    {
        hit = default;
        if (Math.Abs(direction.Y) < 1e-9)
            return false;
        double t = (floorY - origin.Y) / direction.Y;
        if (t < 0 || t > maxDistance)
            return false;
        Vec3 point = origin + direction * t;
        double u = point.X - centre.X, v = -(point.Z - centre.Z);
        if (!Inside(rect, u, v, image))
            return false;
        hit = new BillboardHit(t, point);
        return true;
    }

    private static bool Inside(SpriteRect rect, double u, double v, Raster image)
    {
        if (u < rect.Left || u >= rect.Right || v < rect.Bottom || v >= rect.Top)
            return false;
        if (image == null)
            return true;
        int col = (int)Math.Floor((u - rect.Left) / rect.PixelSize);
        int fromBottom = (int)Math.Floor((v - rect.Bottom) / rect.PixelSize);
        if (col < 0 || col >= image.Width || fromBottom < 0 || fromBottom >= image.Height)
            return false;
        int row = image.Height - 1 - fromBottom;
        return image.Pixels[(row * image.Width + col) * 4 + 3] >= PickAlpha;
    }
}
