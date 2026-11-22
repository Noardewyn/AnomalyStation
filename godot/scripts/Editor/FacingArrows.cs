using Godot;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Editor;

namespace Anomaly.Client.Editor;

/// <summary>
/// The map editor's arrows (docs/ai/specs/map-editor.md 5.19): a flat arrow over each thing that has a way to face, pointing that way, drawn
/// through what stands on its tile. One draw call whatever their number.
/// </summary>
public sealed partial class FacingArrows : MultiMeshInstance3D
{
    private const float Lift = 0.14f; // metres over its level's floor

    private readonly MultiMesh _arrows = new() { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D };

    public FacingArrows()
    {
        Name = "FacingArrows";
        _arrows.Mesh = Arrow();
        Multimesh = _arrows;
        CastShadow = ShadowCastingSetting.Off;
        MaterialOverride = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            AlbedoColor = new Color(1f, 0.3f, 0.85f, 0.9f),
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            NoDepthTest = true, // seen through the thing it is the arrow of
            DisableReceiveShadows = true,
            RenderPriority = 14,
        };
        Visible = false;
    }

    // An arrow lying flat that points north (the world's -Z): a shaft and a head, 1.3 m long on a 2 m tile.
    private static ArrayMesh Arrow()
    {
        Vector3 At(float x, float z) => new(x, 0f, z);
        Vector3[] points = { At(-0.12f, 0.65f), At(0.12f, 0.65f), At(0.12f, -0.1f), At(-0.12f, -0.1f), At(-0.38f, -0.1f), At(0.38f, -0.1f), At(0f, -0.65f) };
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = points;
        arrays[(int)Mesh.ArrayType.Index] = new[] { 0, 1, 2, 0, 2, 3, 4, 5, 6 };
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }

    /// <summary>Shows an arrow for each thing: its tile, its level and BYOND's dir it faces.</summary>
    public void ShowThings(IReadOnlyList<(int X, int Y, int Z, int Dir)> things)
    {
        Visible = true;
        _arrows.InstanceCount = things.Count;
        for (int i = 0; i < things.Count; i++)
        {
            (int x, int y, int z, int dir) = things[i];
            _arrows.SetInstanceTransform(i, new Transform3D(new Basis(Vector3.Up, Mathf.DegToRad(FacingMarks.Degrees(dir))), new Vector3(TileSpace.WorldX(x), TileSpace.WorldY(z) + Lift, TileSpace.WorldZ(y))));
        }
        // the map is far larger than the mesh's own bounds, which the engine would cull the arrows by
        CustomAabb = new Aabb(new Vector3(-100000, -100000, -100000), new Vector3(200000, 200000, 200000));
    }

    public void Clear() => Visible = false;
}
