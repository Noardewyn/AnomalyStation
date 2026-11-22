using Godot;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Icons;
using Anomaly.Protocol;

namespace Anomaly.Client.Render;

// D320 (DV352): the build ghost. What the held tool would build (BuildPreviewMessage: the RPD's next pipe, later the other construction and
// crafting tools) is drawn on the aimed tile (BuildGhostPlacement) as tg's picture lying flat on the floor, tinted and pulsing so it reads
// as a preview and not a placed thing. It turns with the build's direction (Shift and the wheel on the RPD).

/// <summary>A translucent, pulsing picture of the next build on one tile; hidden when there is nothing to show.</summary>
public partial class BuildGhost : MeshInstance3D
{
    private const float Lift = 0.05f; // metres over the floor: above the floor's decals (pipes, wires)

    private const string ShaderCode = """
        shader_type spatial;
        render_mode unshaded, cull_disabled, depth_draw_never, depth_test_disabled, blend_mix; // seen over what stands on the tile (a chair, a table)
        uniform sampler2D picture : source_color, filter_nearest;
        uniform vec4 tint : source_color = vec4(1.0);
        uniform vec4 glow : source_color = vec4(0.45, 0.85, 1.0, 1.0);
        uniform vec2 shift; // tg's pixel shift in UV (east +x, north -y)
        void fragment() {
            vec2 uv = UV - shift;
            bool inside = uv.x >= 0.0 && uv.x <= 1.0 && uv.y >= 0.0 && uv.y <= 1.0;
            vec4 c = inside ? texture(picture, uv) * tint : vec4(0.0);
            float pulse = 0.5 + 0.5 * sin(TIME * 6.0);
            // the tile's outline: where it goes even when the picture is thin
            float edge = min(min(UV.x, 1.0 - UV.x), min(UV.y, 1.0 - UV.y)) < 0.035 ? 1.0 : 0.0;
            float a = max(c.a * (0.6 + 0.35 * pulse), edge * (0.35 + 0.45 * pulse));
            ALBEDO = c.a > 0.0 ? mix(c.rgb, glow.rgb, 0.2 + 0.25 * pulse) : glow.rgb;
            ALPHA = a;
        }
        """;

    private ShaderMaterial _material;
    private BuildPreviewMessage _shown;
    private bool _pictured;

    public override void _Ready()
    {
        Mesh = new PlaneMesh { Size = new Vector2(TileSpace.TileSize, TileSpace.TileSize) };
        _material = new ShaderMaterial { Shader = new Shader { Code = ShaderCode }, RenderPriority = 1 };
        MaterialOverride = _material;
        CastShadow = ShadowCastingSetting.Off;
        Visible = false;
    }

    /// <summary>Shows <paramref name="preview"/> on tile (<paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>), or hides the ghost when <paramref name="show"/> is false.</summary>
    public void Show(BuildPreviewMessage preview, bool show, int x, int y, int z, IconLibrary icons)
    {
        if (!show || preview is not { IsSet: true } || icons == null)
        {
            Visible = false;
            return;
        }
        if (!Equals(preview, _shown) || !_pictured)
        {
            _shown = preview;
            _pictured = TryPicture(preview, icons); // an icon still loading is tried again next frame
        }
        if (!_pictured)
        {
            Visible = false;
            return;
        }
        // tg's picture lies with its top to the north (the plane's -Z, its V = 0) and its right to the east; the shader shifts it by pixel_x
        // east and pixel_y north inside the tile's outline
        Position = new Vector3(TileSpace.WorldX(x), TileSpace.WorldY(z) + Lift, TileSpace.WorldZ(y));
        Visible = true;
    }

    private bool TryPicture(BuildPreviewMessage preview, IconLibrary icons)
    {
        if (!icons.TryGetIcon(preview.Icon, out IIcon icon) || !icon.TryGetState(preview.State, out IIconState state))
            return false;
        Image image = AppearanceTextures.ToImage(state.Get(ByondDir.Index(preview.Dir, state.Dirs), 0));
        _material.SetShaderParameter("picture", ImageTexture.CreateFromImage(image));
        _material.SetShaderParameter("shift", new Vector2(preview.PixelX / 32f, -preview.PixelY / 32f));
        uint c = preview.Color;
        _material.SetShaderParameter("tint", new Color((c >> 24 & 0xFF) / 255f, (c >> 16 & 0xFF) / 255f, (c >> 8 & 0xFF) / 255f, (c & 0xFF) / 255f));
        return true;
    }
}
