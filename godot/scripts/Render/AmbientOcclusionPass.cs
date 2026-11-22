using Godot;

namespace Anomaly.Client.Render;

/// <summary>
/// The ambient occlusion (ambient_occlusion.gdshader): a quad under the camera that its shader stretches over the whole view and that
/// darkens the corners and the floor under things in the grid-lit world, which Godot's SSAO cannot reach (the grid's light is the
/// shaders' emission). Its level is the Graphics tab's "Ambient occlusion": off, or low, medium and high by the samples per pixel.
/// </summary>
public partial class AmbientOcclusionPass : MeshInstance3D
{
    private static Shader _shader;
    private ShaderMaterial _material;
    private int _level;

    /// <summary>The samples by level (1 to 3); the shader takes half of them at each of the four points of its 2 x 2 filter.</summary>
    private static readonly int[] Samples = { 0, 8, 12, 16 };

    public override void _Ready()
    {
        _material = new ShaderMaterial { Shader = _shader ??= GD.Load<Shader>("res://shaders/ambient_occlusion.gdshader"), RenderPriority = (int)Material.RenderPriorityMin };
        Mesh = new QuadMesh { Size = new Vector2(2f, 2f) };
        MaterialOverride = _material;
        ExtraCullMargin = 16384f; // the shader places it, not its transform
        Layers = Light.LightLayer.MainViewLayer; // not in a mirror's view
        CastShadow = ShadowCastingSetting.Off;
        GIMode = GIModeEnum.Disabled;
        Configure(_level);
    }

    /// <summary>Sets the level: 0 off, 1 to 3 low to high.</summary>
    public void Configure(int level)
    {
        _level = Mathf.Clamp(level, 0, Samples.Length - 1);
        if (_material == null)
            return;
        Visible = _level > 0;
        _material.SetShaderParameter("samples", Samples[_level]);
    }
}
