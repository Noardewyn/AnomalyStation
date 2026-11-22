using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Icons;
using Anomaly.Client.Shared.World;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Interaction;

/// <summary>A small world for the pick tests: 10 x 10 tiles, one level, walls, shapes in cells, in the pick's frame (tile (x, y) spans east (x - 1) * 2 .. x * 2).</summary>
internal sealed class PickFixture : IPickWorld
{
    public int Width { get; init; } = 10;

    public int Height { get; init; } = 10;

    public int Levels { get; init; } = 1;

    public double WallHeight => 2.0;

    public HashSet<(int X, int Y, int Z)> Walls { get; } = new();

    public HashSet<(int X, int Y, int Z)> NoCeiling { get; } = new();

    public List<PickShape> Moving { get; } = new();

    public Dictionary<(int X, int Y, int Z), List<PickShape>> Static { get; } = new();

    public bool IsWallCell(int x, int y, int z) => Walls.Contains((x, y, z));

    public bool HasCeiling(int x, int y, int z) => !NoCeiling.Contains((x, y, z));

    /// <summary>D325: open space cells (their level stands on the one under them).</summary>
    public HashSet<(int X, int Y, int Z)> Open { get; } = new();

    public bool OpenBelow(int x, int y, int z) => z > 1 && Open.Contains((x, y, z));

    public bool LevelAbove(int x, int y, int z) => z < Levels;

    public void CollectMoving(in PickRay ray, List<PickShape> into) => into.AddRange(Moving);

    public void CollectStatic(int x, int y, int z, List<PickShape> into)
    {
        if (Static.TryGetValue((x, y, z), out List<PickShape> list))
            into.AddRange(list);
    }

    public static double CentreOf(int tile) => (tile - 0.5) * 2.0;

    /// <summary>A wall line along x = <paramref name="x"/>.</summary>
    public PickFixture WallColumn(int x)
    {
        for (int y = 1; y <= Height; y++)
            Walls.Add((x, y, 1));
        return this;
    }

    /// <summary>A standing picture (a mob at the tile scale: 32 x 32, its square from the feet up) that is fully opaque.</summary>
    public static PickShape Mob(long id, int tx, int ty, Raster image = null)
    {
        image ??= Raster.Solid(32, 32, 0xFFFFFFFF);
        return PickShape.StandingPicture(id, PickSurface.Billboard, tx, ty, 1, CentreOf(tx), CentreOf(ty), 0, SpriteRect.Of(32, 32, 0, 0), image, 0, 0);
    }

    /// <summary>A static item at the item scale (D121) with eight clear rows under its drawn pixels.</summary>
    public static PickShape Item(long id, int tx, int ty)
    {
        SpriteRect rect = SpriteRect.Of(32, 32, 0, 0).AsItem(0.5, 8);
        return PickShape.StandingPicture(id, PickSurface.StaticProp, tx, ty, 1, CentreOf(tx), CentreOf(ty), 0, rect, Raster.Solid(32, 32, 0xFFFFFFFF), 0, 0);
    }

    /// <summary>A piece: the boxes of <see cref="PieceBoxes"/> around the tile centre.</summary>
    public void AddPiece(long id, RenderKind shape, int tx, int ty, byte face, byte joints = 0, bool open = false)
    {
        var placed = new Placed(id, PlacedKind.Piece, shape, tx, ty, 1, 0, face, 0f, 0f, 0f, default, default, null, false, joints);
        if (!Static.TryGetValue((tx, ty, 1), out List<PickShape> list))
            Static[(tx, ty, 1)] = list = new List<PickShape>();
        foreach (PieceBox box in PieceBoxes.For(placed, WallHeight, open))
            list.Add(PickShape.BoxOf(id, PickSurface.Piece, tx, ty, 1, CentreOf(tx) + box.MinEast, CentreOf(ty) + box.MinNorth, box.MinUp, CentreOf(tx) + box.MaxEast, CentreOf(ty) + box.MaxNorth, box.MaxUp, WallHeight));
    }

    public void AddStatic(PickShape shape)
    {
        var key = (shape.TileX, shape.TileY, shape.TileZ);
        if (!Static.TryGetValue(key, out List<PickShape> list))
            Static[key] = list = new List<PickShape>();
        list.Add(shape);
    }

    /// <summary>A ray from tile (<paramref name="ex"/>, <paramref name="ey"/>) at the standing eye (1.2 m) toward the point (east, north, up) metres.</summary>
    public static PickRay RayTo(int ex, int ey, double toEast, double toNorth, double toUp, double max = WorldPicker.DefaultRange, double eye = 1.2)
    {
        double oe = CentreOf(ex), on = CentreOf(ey), ou = eye;
        double de = toEast - oe, dn = toNorth - on, du = toUp - ou;
        double length = Math.Sqrt(de * de + dn * dn + du * du);
        return new PickRay(oe, on, ou, 1, de / length, dn / length, du / length, max);
    }

    /// <summary>A ray along a bearing: dx, dy a ground direction, pitch up positive in degrees.</summary>
    public static PickRay Look(int ex, int ey, double dirEast, double dirNorth, double pitchDegrees = 0, double max = WorldPicker.DefaultRange)
    {
        double h = Math.Sqrt(dirEast * dirEast + dirNorth * dirNorth);
        double p = pitchDegrees * Math.PI / 180.0;
        return new PickRay(CentreOf(ex), CentreOf(ey), 1.2, 1, dirEast / h * Math.Cos(p), dirNorth / h * Math.Cos(p), Math.Sin(p), max);
    }
}
