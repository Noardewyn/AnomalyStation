using Godot;
using Anomaly.Client.Shared;

namespace Anomaly.Client.Editor;

/// <summary>
/// Tiles of the map marked in the 3D view, each by a flat coloured square over it (docs/ai/specs/map-editor.md 5.5): the selected rectangle,
/// the tiles a dragged shape will change, the place a copy goes to, the areas. One draw call whatever the number of tiles, drawn over
/// what stands on the tiles.
/// </summary>
public sealed partial class EditorCells : MultiMeshInstance3D
{
    private readonly MultiMesh _tiles = new() { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true };
    private readonly float _lift;
    private string _shown;

    /// <param name="lift">Metres above the floor the squares lie (the middle of another shape).</param>
    /// <param name="priority">The material's place among the see-through ones: a higher one is drawn over a lower.</param>
    /// <param name="shape">What marks a tile in place of the flat square: a box over it, seen from the side and from afar (the search's finds).</param>
    public EditorCells(float lift, int priority, Mesh shape = null)
    {
        _lift = lift;
        _tiles.Mesh = shape ?? new PlaneMesh { Size = new Vector2(TileSpace.TileSize, TileSpace.TileSize) };
        Multimesh = _tiles;
        CastShadow = ShadowCastingSetting.Off;
        MaterialOverride = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            VertexColorUseAsAlbedo = true,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            NoDepthTest = true, // a mark is seen through the walls and the things of its tile
            DisableReceiveShadows = true,
            RenderPriority = priority,
        };
        Visible = false;
    }

    /// <summary>
    /// Shows tiles, each in its colour. <paramref name="key"/> names what is shown: the squares are made again only when it changes, and
    /// <paramref name="cells"/> is read only then.
    /// </summary>
    public void Show(string key, Func<IEnumerable<(int X, int Y, int Z, Color Colour)>> cells)
    {
        Visible = true;
        if (key == _shown)
            return;
        _shown = key;
        var list = new List<(int X, int Y, int Z, Color Colour)>(cells());
        _tiles.InstanceCount = list.Count;
        for (int i = 0; i < list.Count; i++)
        {
            (int x, int y, int z, Color colour) = list[i];
            _tiles.SetInstanceTransform(i, new Transform3D(Basis.Identity, new Vector3(TileSpace.WorldX(x), TileSpace.WorldY(z) + _lift, TileSpace.WorldZ(y))));
            _tiles.SetInstanceColor(i, colour);
        }
        // the map is far larger than the mesh's own bounds, which the engine would cull the squares by
        CustomAabb = new Aabb(new Vector3(-100000, -100000, -100000), new Vector3(200000, 200000, 200000));
    }

    public void Clear()
    {
        Visible = false;
        _shown = null;
    }
}
