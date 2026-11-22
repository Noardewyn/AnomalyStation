using Godot;
using Anomaly.Client.Shared;

namespace Anomaly.Client.Render;

// Third-person play (the owner, 2026-10-16): the line-of-sight shade of the third-person view (Space Station 14's FOV). A quad
// that fills the screen whatever the camera (fov_shadow.gdshader sets its clip position), drawn last of the transparent pass so
// the gas haze and the translucent pictures behind a wall are shaded too. FovMask is the rule; this node only uploads its
// opaque cells and the eye.

/// <summary>The screen-filling shade of what the own mob cannot see.</summary>
public partial class FovShadow : MeshInstance3D
{
    private ShaderMaterial _material;
    private Image _image;
    private ImageTexture _texture;

    public static FovShadow Create(Node parent)
    {
        var shadow = new FovShadow
        {
            Name = "FovShadow",
            Layers = Light.LightLayer.MainViewLayer,
            CastShadow = ShadowCastingSetting.Off,
            Visible = false,
            Mesh = new QuadMesh { Size = new Vector2(2f, 2f) },
            // the quad is placed by its shader: never culled
            CustomAabb = new Aabb(new Vector3(-1e5f, -1e5f, -1e5f), new Vector3(2e5f, 2e5f, 2e5f)),
        };
        shadow._material = new ShaderMaterial
        {
            Shader = GD.Load<Shader>("res://shaders/fov_shadow.gdshader"),
            RenderPriority = MesonLayer.Priority - 1, // the meson outline (D289) is drawn over the shade
        };
        shadow.MaterialOverride = shadow._material;
        parent.AddChild(shadow);
        return shadow;
    }

    /// <summary>Shows the shade for <paramref name="mask"/> (null hides it).</summary>
    public void Apply(FovMask mask, float darkness)
    {
        Visible = mask != null;
        if (mask == null)
            return;
        if (_image == null || _image.GetWidth() != mask.Side)
        {
            _image = Image.CreateFromData(mask.Side, mask.Side, false, Image.Format.R8, mask.Texels);
            _texture = ImageTexture.CreateFromImage(_image);
            _material.SetShaderParameter("fov_opaque", _texture);
            mask.TextureDirty = false;
        }
        else if (mask.TextureDirty)
        {
            _image.SetData(mask.Side, mask.Side, false, Image.Format.R8, mask.Texels);
            _texture.Update(_image);
            mask.TextureDirty = false;
        }
        _material.SetShaderParameter("fov_origin", new Vector2(mask.OriginX, mask.OriginY));
        _material.SetShaderParameter("fov_side", (float)mask.Side);
        _material.SetShaderParameter("fov_eye", new Vector2(mask.EyeX, mask.EyeY));
        _material.SetShaderParameter("fov_darkness", darkness);
    }
}
