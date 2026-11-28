using Godot;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Icons;
using Anomaly.Client.Shared.World;

namespace Anomaly.Client.Render;

/// <summary>
/// M4a P7 (interaction.md 4.1): the engine-free world pick (<see cref="WorldPicker"/>) over what the renderers draw: the map's walls and the shapes of
/// its batched props and pieces (<see cref="MapRenderer"/>), the mobs and moved things (<see cref="EntityLayer"/>).
/// </summary>
public sealed class WorldPickAdapter : IPickWorld
{
    private readonly MapRenderer _map;
    private readonly EntityLayer _entities;

    public WorldPickAdapter(MapRenderer map, EntityLayer entities)
    {
        _map = map;
        _entities = entities;
        PickExclude = 0;
    }

    /// <summary>The own mob's id: never a hit.</summary>
    public long PickExclude { get; set; }

    public int Width => _map.Size.Width;

    public int Height => _map.Size.Height;

    public int Levels => _map.Size.Levels;

    public double WallHeight => WorldScale.WallHeight;

    public bool IsWallCell(int x, int y, int z) => WallOverride?.Invoke(x, y, z) ?? _map.IsWallCell(x, y, z);

    /// <summary>
    /// The map editor's preview (docs/ai/specs/map-editor.md 5.9) is drawn by the world but is not there for a pick: a thing this names is no hit, and
    /// a cell this answers for is a wall or not by the answer (the document's turf), whatever the previewed turf is. Null: nothing is passed over.
    /// </summary>
    public Func<long, bool> PickSkips { get; set; }

    public Func<int, int, int, bool?> WallOverride { get; set; }

    /// <summary>As <see cref="WallOverride"/>, for whether a cell is open space that a ray goes on down through: the answer for a cell this answers for.</summary>
    public Func<int, int, int, bool?> VoidOverride { get; set; }

    /// <summary>The map editor's picks go through the ceilings: a map is edited from above (docs/ai/specs/map-editor.md 5.2).</summary>
    public bool IgnoreCeilings { get; set; }

    public bool HasCeiling(int x, int y, int z) => !IgnoreCeilings && _map.HasCeilingAt(x, y, z);

    public void CollectMoving(in PickRay ray, List<PickShape> into)
    {
        int from = into.Count;
        _entities.CollectPickShapes(into, PickExclude);
        Skip(into, from);
    }

    public void CollectStatic(int x, int y, int z, List<PickShape> into)
    {
        int from = into.Count;
        _map.CollectStatic(x, y, z, into);
        Skip(into, from);
    }

    private void Skip(List<PickShape> shapes, int from)
    {
        if (PickSkips == null)
            return;
        for (int i = shapes.Count - 1; i >= from; i--)
        {
            if (PickSkips(shapes[i].EntityId))
                shapes.RemoveAt(i);
        }
    }

    public double LevelHeight => TileSpace.LevelHeight;

    /// <summary>
    /// The map editor's picks stay among the levels it shows (docs/ai/specs/map-editor.md 5.8): a ray goes no lower than the edited level through
    /// open tiles (an open tile of it is the hit, not the floor seen through it), and no higher than the top level shown. 0: no limit.
    /// </summary>
    public int PickFloorLevel { get; set; }

    public int PickTopLevel { get; set; }

    public bool OpenBelow(int x, int y, int z)
        => (PickFloorLevel == 0 || z > PickFloorLevel) && (VoidOverride?.Invoke(x, y, z) is { } open ? open && _map.LevelAbove(z - 1) : _map.OpenBelow(x, y, z));

    public bool LevelAbove(int x, int y, int z) => (PickTopLevel == 0 || z < PickTopLevel) && _map.LevelAbove(z);
}

/// <summary>
/// M4a P7 (D49): the highlight of the thing under the crosshair: a translucent additive slab around the shape the pick hit (a picture's rectangle turned
/// to the viewer, a piece's box, a floor or wall cell), and a short pulse when a click is sent (local feedback, no prediction).
/// </summary>
public partial class HoverHighlight : Node3D
{
    private MeshInstance3D _slab;
    private BoxMesh _box;
    private StandardMaterial3D _material;
    private double _pulseUntil = -1;
    private static readonly Color Calm = new(0.45f, 0.85f, 1f, 0.16f), Pulse = new(1f, 1f, 0.7f, 0.42f);

    public override void _Ready()
    {
        Name = "Highlight";
        _box = new BoxMesh();
        _material = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = BaseMaterial3D.BlendModeEnum.Add,
            AlbedoColor = Calm,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            NoDepthTest = false,
        };
        _slab = new MeshInstance3D { Mesh = _box, MaterialOverride = _material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Visible = false };
        AddChild(_slab);
    }

    // The drawn pixels' rectangle of a picture (the icon square is mostly clear for a small item), cached per picture.
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Raster, int[]> Trims = new();

    private static SpriteRect TightRect(PickShape shape)
    {
        SpriteRect r = shape.Rect;
        if (shape.Image == null)
            return r;
        int[] trim = Trims.GetValue(shape.Image, image =>
        {
            AtlasBuilder.TrimBounds(image, out int x, out int y, out int w, out int h);
            return new[] { x, y, w, h };
        });
        if (trim[2] <= 0 || trim[3] <= 0)
            return r;
        double left = r.Left + trim[0] * r.PixelSize, top = r.Top - trim[1] * r.PixelSize;
        return new SpriteRect(left, left + trim[2] * r.PixelSize, top - trim[3] * r.PixelSize, top, r.PixelSize);
    }

    /// <summary>A short brighter flash (a click was sent).</summary>
    public void PulseNow(double now) => _pulseUntil = now + 0.18;

    /// <summary>How strong the highlight is, 1 as it is in the game: the map editor's select tool shows it fainter, under the mark of what is selected.</summary>
    public float Strength { get; set; } = 1f;

    /// <summary>Shows the highlight for <paramref name="hit"/> (and the shape it hit, when it was a thing), or hides it. <paramref name="camera"/> (its basis) turns a picture's slab to the viewer.</summary>
    public void Show(in PickHit hit, PickShape shape, Basis camera, double now)
    {
        if (_slab == null)
            return;
        if (!hit.IsHit || hit.Kind == Protocol.TargetKind.Screen)
        {
            _slab.Visible = false;
            return;
        }
        Color colour = now < _pulseUntil ? Pulse : Calm;
        _material.AlbedoColor = colour with { A = colour.A * Strength };
        BoxOf(hit, shape, camera, GetViewport()?.GetCamera3D()?.GlobalPosition.Y ?? float.MaxValue, out Vector3 size, out Vector3 centre, out Basis turn);
        _box.Size = size;
        _slab.Transform = new Transform3D(turn, centre);
        _slab.Visible = true;
    }

    /// <summary>
    /// The box around what a pick hit, in the world: a thing's shape (a picture's drawn pixels, turned to the viewer when it stands; a piece's box),
    /// or with no shape the hit's cell (the tile's floor, the whole wall, the ceiling's slab). <paramref name="turn"/> is its rotation: a standing picture's turned
    /// to the <paramref name="camera"/> (its basis) about the vertical axis, an item's that faces the eye tilted back about its feet with it, the rest none;
    /// <paramref name="eyeHeight"/> is the camera's height in the world (a floor seen from under it is marked on its underside).
    /// </summary>
    public static void BoxOf(in PickHit hit, PickShape shape, Basis camera, float eyeHeight, out Vector3 size, out Vector3 centre, out Basis turn)
    {
        float floor = TileSpace.WorldY(hit.Z);
        turn = Basis.Identity;
        if (shape != null && shape.Kind == PickShapeKind.Standing)
        {
            SpriteRect r = TightRect(shape);
            size = new Vector3((float)r.Width + 0.04f, (float)r.Height + 0.04f, 0.03f);
            var feet = new Vector3((float)shape.East, floor + (float)shape.Up, -(float)shape.North);
            Vector3 right = new Vector3(camera.X.X, 0, camera.X.Z).Normalized();
            // (picture_billboard.gdshader's face_eye: the camera's back x the ground right)
            Vector3 up = shape.FacesEye ? camera.Z.Cross(right) : Vector3.Up;
            if (shape.FacesBack)
                right = -right; // T5b: the own body for its own eye is laid out along the view's left (face_back), so its drawn pixels are there
            up = (up with { Y = Mathf.Max(up.Y, 0f) }).Normalized(); // (flat on the floor past straight down, T5a)
            turn = new Basis(right, up, right.Cross(up));
            centre = feet + right * (float)(r.Left + r.Right) / 2f + up * (float)(r.Bottom + r.Top) / 2f;
        }
        else if (shape != null && shape.Kind == PickShapeKind.Flat)
        {
            // (the drawn pixels, as a standing picture's: a cable or a pipe under the floor is a line across its tile, not the tile)
            SpriteRect r = TightRect(shape);
            size = new Vector3((float)r.Width + 0.04f, 0.03f, (float)r.Height + 0.04f);
            centre = new Vector3((float)(shape.East + (r.Left + r.Right) / 2), floor + (float)shape.Up, -(float)(shape.North + (r.Bottom + r.Top) / 2));
        }
        else if (shape != null)
        {
            size = new Vector3((float)(shape.MaxEast - shape.MinEast) + 0.04f, (float)(shape.MaxUp - shape.MinUp) + 0.04f, (float)(shape.MaxNorth - shape.MinNorth) + 0.04f);
            centre = new Vector3((float)(shape.MinEast + shape.MaxEast) / 2, floor + (float)(shape.MinUp + shape.MaxUp) / 2, -(float)(shape.MinNorth + shape.MaxNorth) / 2);
        }
        else
        {
            // A cell: the tile's floor, the whole wall, or the ceiling slab.
            float cx = TileSpace.WorldX(hit.X), cz = TileSpace.WorldZ(hit.Y);
            float wall = WorldScale.WallHeight;
            switch (hit.Surface)
            {
                case PickSurface.Wall:
                    size = new Vector3(2.02f, wall + 0.02f, 2.02f);
                    centre = new Vector3(cx, floor + wall / 2, cz);
                    break;
                case PickSurface.Ceiling:
                    size = new Vector3(2f, 0.03f, 2f);
                    centre = new Vector3(cx, floor + wall - 0.02f, cz);
                    break;
                default:
                    // a floor seen from under it (the level below looking up at its underside, the ceiling there) is marked on that side
                    bool below = eyeHeight < floor;
                    size = new Vector3(2f, 0.03f, 2f);
                    centre = new Vector3(cx, below ? floor - 0.02f : floor + 0.02f, cz);
                    break;
            }
        }
    }
}
