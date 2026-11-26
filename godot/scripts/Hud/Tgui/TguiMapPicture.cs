using Godot;
using Anomaly.Client.Render;
using Anomaly.Protocol;

namespace Anomaly.Client.Hud.Tgui;

/// <summary>
/// M10x slice E2 (D284): tgui-core's ByondUi map with no turfs (tg's CharacterPreview) as one 2D picture: the screen object whose
/// screen_loc names the map (<c>"character_preview_[0x2a]:1,1"</c>, tg's char_preview) composed facing its appearance's dir and drawn
/// with nearest filtering. The atom's own 32x32 square fills the box and is centred in it; overlays that reach out of the square (a
/// cerulean's tail, wings) reach out of it as on BYOND's map, cut at the box's edges, so a species with large overlays is drawn at the
/// same scale as a human.
/// shortcut: draws only the first screen object of the map (tg's preview has one); layer several when a map holds more.
/// </summary>
public partial class TguiMapPicture : Control
{
    private readonly Func<ReplicaWorld> _replica;
    private readonly IAppearanceTextures _textures;
    private ComposedAppearance? _composed;

    /// <summary>The map's id (tg's <c>assigned_map</c>).</summary>
    public string MapRef { get; set; }

    public TguiMapPicture(Func<ReplicaWorld> replica, IAppearanceTextures textures)
    {
        _replica = replica;
        _textures = textures;
        TextureFilter = TextureFilterEnum.Nearest;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;
        CustomMinimumSize = new Vector2(96, 96);
        MouseFilter = MouseFilterEnum.Ignore;
        ClipContents = true;
    }

    public override void _Process(double delta)
    {
        _composed = null;
        ReplicaWorld replica = _replica();
        if (replica != null && _textures != null && !string.IsNullOrEmpty(MapRef))
        {
            string prefix = MapRef + ":";
            foreach (ScreenEntry entry in replica.Screen.Values)
            {
                if (entry.ScreenLoc == null || !entry.ScreenLoc.StartsWith(prefix, StringComparison.Ordinal))
                    continue;
                byte dir = replica.TryGetAppearance(entry.Appearance, out AppearanceEntry look) && look.Dir != 0 ? look.Dir : (byte)2;
                if (_textures.TryCompose(replica, entry.Appearance, dir, Time.GetTicksMsec() / 1000.0, out ComposedAppearance composed))
                    _composed = composed;
                break;
            }
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_composed is not ComposedAppearance c || c.Texture == null || c.PixelWidth <= 0 || c.PixelHeight <= 0)
            return;
        float scale = Mathf.Floor(Mathf.Min(Size.X, Size.Y) / 32f * 4f) / 4f; // the 32 px square fills the box (quarter steps keep pixels even)
        if (scale <= 0)
            return;
        // the square's lower left corner sits OriginX, OriginY tg pixels into the texture (y from the bottom)
        Vector2 square = (Size - new Vector2(32, 32) * scale) / 2f;
        var topLeft = new Vector2(square.X - c.OriginX * scale, square.Y + 32 * scale - (c.PixelHeight - c.OriginY) * scale);
        DrawTextureRect(c.Texture, new Rect2(topLeft.Round(), new Vector2(c.PixelWidth, c.PixelHeight) * scale), false);
    }
}
