using Godot;
using Anomaly.Client.Shared;

namespace Anomaly.Client.Render;

/// <summary>
/// D298: the edge outlines (outline.gdshader): a quad under the camera that its shader stretches over the whole view, drawn first
/// among the transparent things (the sprites and the held items cover its lines); hidden when off.
/// </summary>
public partial class OutlinePass : MeshInstance3D
{
    private static Shader _shader;
    private ShaderMaterial _material;

    /// <summary>How far the lines reach before they are faded out, in metres.</summary>
    public const float FadeMetres = 16 * TileSpace.TileSize;

    public override void _Ready()
    {
        _material = new ShaderMaterial { Shader = _shader ??= GD.Load<Shader>("res://shaders/outline.gdshader"), RenderPriority = (int)Material.RenderPriorityMin };
        _material.SetShaderParameter("fade", FadeMetres);
        Mesh = new QuadMesh { Size = new Vector2(2f, 2f) };
        MaterialOverride = _material;
        ExtraCullMargin = 16384f; // the shader places it, not its transform
        Layers = Light.LightLayer.MainViewLayer; // not in a mirror's view
        CastShadow = ShadowCastingSetting.Off;
        GIMode = GIModeEnum.Disabled;
        Visible = false;
    }

    /// <summary>Sets the lines' width in pixels at 1080 lines (0: off) and their strength (0 to 1).</summary>
    public void Configure(int width, float strength)
    {
        if (_material == null)
            return;
        Visible = width > 0 && strength > 0f;
        _material.SetShaderParameter("width", (float)width);
        _material.SetShaderParameter("strength", strength);
    }
}
