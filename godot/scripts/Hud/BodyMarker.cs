using System;
using System.Globalization;
using Godot;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Icons;
using Anomaly.Protocol;

namespace Anomaly.Client.Hud;

/// <summary>
/// DEVIATION(DV54): where a ghost's body lies (protocol version 23, <see cref="ReplicaWorld.OwnBody"/>). tg's own icon of the
/// ghost HUD's Re-enter Corpse button (<c>icons/hud/screen_ghost.dmi</c>, <c>reenter_corpse</c>) is drawn over the world at the
/// body's turf with the distance in tiles under it, on top of the walls; a body behind the camera or off the screen is shown
/// at the screen's edge in its direction. Nothing is drawn when the own mob is no ghost with a body to re-enter.
/// </summary>
public sealed partial class BodyMarker : Control
{
    private const float IconPixels = 48;
    private const float Margin = 40;
    private const float BottomMargin = 130; // above the HUD's bottom row (the ghost's buttons) and room for the label
    private const float RaiseMetres = 1.2f; // above the body lying on its tile

    private TextureRect _icon;
    private Label _distance;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.FullRect);
        _icon = new TextureRect
        {
            MouseFilter = MouseFilterEnum.Ignore,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            TextureFilter = TextureFilterEnum.Nearest,
            Size = new Vector2(IconPixels, IconPixels),
            Modulate = new Color(1, 1, 1, 0.9f),
        };
        AddChild(_icon);
        _distance = new Label { MouseFilter = MouseFilterEnum.Ignore, HorizontalAlignment = HorizontalAlignment.Center, Size = new Vector2(120, 20) };
        _distance.AddThemeColorOverride("font_outline_color", Colors.Black);
        _distance.AddThemeConstantOverride("outline_size", 4);
        AddChild(_distance);
        Visible = false;
    }

    /// <summary>Loads tg's picture of the button once (nothing is drawn without the converted icons).</summary>
    public void Bind(IconLibrary icons)
    {
        if (icons?.GetSheet("icons/hud/screen_ghost.dmi") is IconSheet sheet && sheet.TryGetState("reenter_corpse", out IIconState state))
            _icon.Texture = ImageTexture.CreateFromImage(Render.AppearanceTextures.ToImage(state.Get(0, 0)));
    }

    /// <summary>Places the marker for this frame: the body's turf seen from <paramref name="camera"/>, <paramref name="from"/> the own position in tiles.</summary>
    public void Update(Camera3D camera, MapCell? body, Vector2 from)
    {
        if (body is not MapCell cell || camera == null || _icon.Texture == null)
        {
            Visible = false;
            return;
        }

        Visible = true;
        var world = new Vector3(TileSpace.WorldX(cell.X), TileSpace.WorldY(cell.Z) + RaiseMetres, TileSpace.WorldZ(cell.Y));
        Vector2 screen = GetViewportRect().Size;
        bool behind = camera.IsPositionBehind(world);
        Vector2 at = camera.UnprojectPosition(world);
        if (behind)
            at = screen - at; // the projection of a point behind the eye is mirrored through the screen's centre
        var low = new Vector2(Margin, Margin);
        Vector2 high = screen - new Vector2(Margin, BottomMargin);
        if (behind || at.X < low.X || at.Y < low.Y || at.X > high.X || at.Y > high.Y)
        {
            // Off the screen: on its edge, along the line from the centre towards the body.
            Vector2 centre = (low + high) / 2;
            Vector2 dir = at - centre;
            if (dir.LengthSquared() < 1e-4f)
                dir = Vector2.Down;
            float scale = Math.Min((high.X - centre.X) / Math.Max(Math.Abs(dir.X), 1e-4f), (high.Y - centre.Y) / Math.Max(Math.Abs(dir.Y), 1e-4f));
            at = centre + dir * scale;
        }

        _icon.Position = at - new Vector2(IconPixels / 2, IconPixels / 2);
        float tiles = new Vector2(cell.X, cell.Y).DistanceTo(from);
        _distance.Text = string.Create(CultureInfo.InvariantCulture, $"{tiles:0} tiles");
        _distance.Position = at + new Vector2(-_distance.Size.X / 2, IconPixels / 2);
    }
}
