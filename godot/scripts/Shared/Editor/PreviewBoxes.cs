namespace Anomaly.Client.Shared.Editor;

// Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).

/// <summary>
/// A box of the view's world (metres: x east, y up, z south) whose surfaces are drawn in the preview's blue (docs/ai/specs/map-editor.md 5.9):
/// a turf's (the floors' and the walls' materials take it) or a thing's (every other material).
/// </summary>
public readonly record struct PreviewBox(float MinX, float MinY, float MinZ, float MaxX, float MaxY, float MaxZ, bool Turf)
{
    /// <summary>The least box that holds this one and <paramref name="other"/>.</summary>
    public PreviewBox With(PreviewBox other) => new(Math.Min(MinX, other.MinX), Math.Min(MinY, other.MinY), Math.Min(MinZ, other.MinZ),
        Math.Max(MaxX, other.MaxX), Math.Max(MaxY, other.MaxY), Math.Max(MaxZ, other.MaxZ), Turf);
}

/// <summary>
/// The boxes of what the map editor previews, from what the view already knows of each thing: the shapes a click is tested against. The shaders ask
/// with the point a surface is lit at (light_grid.gdshaderinc, anomaly_previewed): its own place for a solid, and the place it stands at for a picture
/// turned to the eye, whose box is therefore a small one around that place.
/// </summary>
public static class PreviewBoxes
{
    /// <summary>The most boxes the view takes (the shaders' list); a preview is at most 48 tiles (GameScene.Editor).</summary>
    public const int Most = 128;

    private const float Skin = 0.02f; // metres a solid's box is grown by: a face on the box's own side is inside
    private const float Stand = 0.02f; // half the box around the place a picture stands at
    private const float Layer = 0.01f; // half the height of a lying picture's box: its own layer of the floor's stack, not the things that stand on the tile
    private const float Edge = 0.001f; // metres a turf's box reaches past its tile: its own faces, and next to nothing of the tiles beside it
    private const float OverTop = 0.03f; // a wall's top with nothing over it stands 2 cm over the wall's height (turf_kit.gdshader)

    /// <summary>The box of one shape of a thing.</summary>
    public static PreviewBox Of(PickShape shape)
    {
        ArgumentNullException.ThrowIfNull(shape);
        float floor = TileSpace.WorldY(shape.TileZ);
        float east = (float)shape.East, north = (float)shape.North, up = floor + (float)shape.Up;
        return shape.Kind switch
        {
            PickShapeKind.Standing => new PreviewBox(east - Stand, up - Stand, -north - Stand, east + Stand, up + Stand, -north + Stand, false),
            PickShapeKind.Flat => new PreviewBox(east + (float)shape.Rect.Left, up - Layer, -(north + (float)shape.Rect.Top), east + (float)shape.Rect.Right, up + Layer, -(north + (float)shape.Rect.Bottom), false),
            _ => new PreviewBox((float)shape.MinEast - Skin, floor + (float)shape.MinUp - Skin, -(float)shape.MaxNorth - Skin,
                (float)shape.MaxEast + Skin, floor + (float)shape.MaxUp + Skin, -(float)shape.MinNorth + Skin, false),
        };
    }

    /// <summary>
    /// Adds one box per previewed thing: the box that holds every shape of it (a door's panels, a model's pieces) among <paramref name="shapes"/>.
    /// Stops at <see cref="Most"/> boxes in <paramref name="into"/>.
    /// </summary>
    public static void AddThings(IReadOnlyList<PickShape> shapes, Func<long, bool> previewed, List<PreviewBox> into)
    {
        ArgumentNullException.ThrowIfNull(shapes);
        ArgumentNullException.ThrowIfNull(previewed);
        ArgumentNullException.ThrowIfNull(into);
        var places = new Dictionary<long, int>();
        foreach (PickShape shape in shapes)
        {
            if (!previewed(shape.EntityId))
                continue;
            PreviewBox box = Of(shape);
            if (places.TryGetValue(shape.EntityId, out int at))
                into[at] = into[at].With(box);
            else if (into.Count < Most)
            {
                places[shape.EntityId] = into.Count;
                into.Add(box);
            }
        }
    }

    /// <summary>The box of a previewed turf of tile (x, y) of level z: the tile's whole block for a wall (<paramref name="closed"/>), the floor's plane for a floor.</summary>
    public static PreviewBox OfTurf(int x, int y, int z, bool closed)
    {
        float floor = TileSpace.WorldY(z), west = (x - 1) * TileSpace.TileSize, south = -(y - 1) * TileSpace.TileSize;
        return new PreviewBox(west - Edge, floor - (closed ? Edge : Layer), south - TileSpace.TileSize - Edge,
            west + TileSpace.TileSize + Edge, floor + (closed ? TileSpace.LevelHeight + OverTop : Layer), south + Edge, true);
    }
}
