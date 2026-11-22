using Godot;

namespace Anomaly.Client.Editor;

/// <summary>
/// A frame around a box of the map editor's view (docs/ai/specs/map-editor.md 5.20), drawn through the walls, its lines as thick on the screen at
/// any distance. Two are shown: the mark of what is selected, a thick frame with a faint fill in a colour of its own, so that what is selected
/// is seen from anywhere; and a thin, fainter frame around what the mouse is over, so that a thing as thin as a cable is seen to be under it.
/// </summary>
public sealed partial class SelectionMark : Node3D
{
    /// <summary>The colours of what is selected, and of what the mouse is over.</summary>
    public static readonly Color Selected = new(1f, 0.6f, 0.08f), Hovered = new(0.5f, 0.88f, 1f);

    private readonly float _linePixels;
    private readonly Color _colour;
    private readonly StandardMaterial3D _line;
    private readonly MultiMesh _edges = new() { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = new BoxMesh { Size = Vector3.One }, InstanceCount = 12 };
    private readonly MeshInstance3D _fill;

    /// <param name="colour">The frame's colour.</param>
    /// <param name="linePixels">How thick its lines are on the screen.</param>
    /// <param name="fill">How strong the fill of the box is (0: none).</param>
    public SelectionMark(Color colour, float linePixels, float fill)
    {
        Name = "SelectionMark";
        Visible = false;
        (_colour, _linePixels) = (colour, linePixels);
        if (fill > 0f)
            AddChild(_fill = new MeshInstance3D { Mesh = new BoxMesh { Size = Vector3.One }, MaterialOverride = Material(colour with { A = fill }, 19), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        AddChild(new MultiMeshInstance3D
        {
            Multimesh = _edges, MaterialOverride = _line = Material(colour with { A = 0.96f }, 20), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            // (the frame is far larger than a line's own bounds, which the engine would cull it by)
            CustomAabb = new Aabb(new Vector3(-1000, -1000, -1000), new Vector3(2000, 2000, 2000)),
        });
    }

    /// <summary>How strong the frame's lines are, 0 to 1.</summary>
    public float Strength
    {
        set => _line.AlbedoColor = _colour with { A = 0.96f * value };
    }

    private static StandardMaterial3D Material(Color colour, int priority) => new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        AlbedoColor = colour,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        NoDepthTest = true, // seen through the walls and through the thing itself
        DisableReceiveShadows = true,
        RenderPriority = priority,
    };

    /// <summary>Shows the frame around a box of the world, turned by <paramref name="turn"/> (a standing picture's about the vertical axis, an item's tilted to the eye), as <paramref name="camera"/> sees it.</summary>
    public void ShowBox(Vector3 centre, Vector3 size, Basis turn, Camera3D camera)
    {
        Visible = true;
        Transform = new Transform3D(turn, centre);
        if (_fill != null)
            _fill.Scale = size;
        // a line's thickness in the world: so many pixels where the box is
        float height = camera.GetViewport().GetVisibleRect().Size.Y;
        float perPixel = camera.Projection == Camera3D.ProjectionType.Orthogonal ? camera.Size / height
            : 2f * Mathf.Max(0.5f, camera.GlobalPosition.DistanceTo(centre)) * Mathf.Tan(Mathf.DegToRad(camera.Fov) / 2f) / height;
        float thick = Mathf.Max(0.006f, perPixel * _linePixels);
        int at = 0;
        for (int axis = 0; axis < 3; axis++)
        {
            int u = (axis + 1) % 3, v = (axis + 2) % 3;
            for (int corner = 0; corner < 4; corner++)
            {
                Vector3 scale = new(thick, thick, thick), offset = Vector3.Zero;
                scale[axis] = size[axis] + thick;
                offset[u] = (corner & 1) == 0 ? -size[u] / 2f : size[u] / 2f;
                offset[v] = (corner & 2) == 0 ? -size[v] / 2f : size[v] / 2f;
                _edges.SetInstanceTransform(at++, new Transform3D(Basis.FromScale(scale), offset));
            }
        }
    }

    public void HideBox() => Visible = false;
}
