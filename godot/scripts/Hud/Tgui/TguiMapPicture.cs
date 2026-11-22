using Godot;
using Anomaly.Client.Render;
using Anomaly.Protocol;

namespace Anomaly.Client.Hud.Tgui;

/// <summary>
/// M10x slice E2 (D284): tgui-core's ByondUi map with no turfs (tg's CharacterPreview) as one 2D picture: the screen object whose
/// screen_loc names the map (<c>"character_preview_[0x2a]:1,1"</c>, tg's char_preview) composed facing its appearance's dir and drawn
/// with nearest filtering, scaled to fit.
/// shortcut: draws only the first screen object of the map (tg's preview has one); layer several when a map holds more.
/// </summary>
public partial class TguiMapPicture : TextureRect
{
    private readonly Func<ReplicaWorld> _replica;
    private readonly IAppearanceTextures _textures;

    /// <summary>The map's id (tg's <c>assigned_map</c>).</summary>
    public string MapRef { get; set; }

    public TguiMapPicture(Func<ReplicaWorld> replica, IAppearanceTextures textures)
    {
        _replica = replica;
        _textures = textures;
        ExpandMode = ExpandModeEnum.IgnoreSize;
        StretchMode = StretchModeEnum.KeepAspectCentered;
        TextureFilter = TextureFilterEnum.Nearest;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;
        CustomMinimumSize = new Vector2(96, 96);
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Process(double delta)
    {
        ReplicaWorld replica = _replica();
        Texture = null;
        if (replica == null || _textures == null || string.IsNullOrEmpty(MapRef))
            return;
        string prefix = MapRef + ":";
        foreach (ScreenEntry entry in replica.Screen.Values)
        {
            if (entry.ScreenLoc == null || !entry.ScreenLoc.StartsWith(prefix, StringComparison.Ordinal))
                continue;
            byte dir = replica.TryGetAppearance(entry.Appearance, out AppearanceEntry look) && look.Dir != 0 ? look.Dir : (byte)2;
            if (_textures.TryCompose(replica, entry.Appearance, dir, Time.GetTicksMsec() / 1000.0, out ComposedAppearance composed))
                Texture = composed.Texture;
            return;
        }
    }
}
