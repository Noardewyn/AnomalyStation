using Godot;

namespace Anomaly.Client.Render;

// M5x slice 5 (D150, DV50): the owner's gravitational lensing around tg's singularity. tg draws the singularity as its stage sprite
// (1 to 11 tiles); the client draws that sprite as a billboard (DV49) and puts this disc behind it: a camera-facing quad that bends
// the scene behind toward the singularity's centre (singularity_lens.gdshader). The disc is twice the sprite's width; it is drawn
// before the sprite (a lower render priority), so the sprite stays on top.

/// <summary>The lensing disc of one singularity, a child of its entity node.</summary>
public partial class SingularityLens : MeshInstance3D
{
    /// <summary>The disc's diameter as a share of the singularity picture's width.</summary>
    public const float DiscPerPicture = 2.0f;

    private static Shader _shader;
    private readonly QuadMesh _quad = new();

    /// <summary>Makes the disc under <paramref name="parent"/>.</summary>
    public static SingularityLens Create(Node3D parent)
    {
        var lens = new SingularityLens
        {
            Name = "Lens",
            CastShadow = ShadowCastingSetting.Off,
            ExtraCullMargin = 4f,
        };
        lens.Mesh = lens._quad;
        lens.MaterialOverride = new ShaderMaterial
        {
            Shader = _shader ??= GD.Load<Shader>("res://shaders/singularity_lens.gdshader"),
            RenderPriority = -1,
        };
        parent.AddChild(lens);
        return lens;
    }

    /// <summary>How bright the glow around the hole is (DV67).</summary>
    public const float HaloStrength = 0.9f;

    /// <summary>DV67: the glow around the hole in <paramref name="colour"/> (the singularity light's: its sprite's hues' brightest).</summary>
    public void SetHalo(Color colour)
    {
        var material = (ShaderMaterial)MaterialOverride;
        material.SetShaderParameter("halo_color", new Vector3(colour.R, colour.G, colour.B));
        material.SetShaderParameter("halo_strength", HaloStrength);
    }

    /// <summary>Places the disc at the picture's middle, <paramref name="pictureWidth"/> metres wide.</summary>
    public void Set(Vector3 pictureMiddle, float pictureWidth)
    {
        float size = Mathf.Max(pictureWidth * DiscPerPicture, 1f);
        _quad.Size = new Vector2(size, size);
        Position = pictureMiddle;
    }
}
