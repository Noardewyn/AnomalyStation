using Godot;
using Anomaly.Client.Hud.Tgui;
using Anomaly.Client.Render;
using Anomaly.Client.Shared;
using Anomaly.Protocol;

namespace Anomaly.Client.Hud;

/// <summary>
/// M4a P7 (D125, interaction.md 6.3): the radial menu around the crosshair, drawn from <see cref="RadialModel"/>: tg's context entries for the target with the held
/// item, then the fixed ones. The mouse is captured, so the model's aiming vector (the movement since it opened) picks the entry; the highlighted entry has a thick frame
/// besides its colour (D70), and every entry is a label with an icon (a Font Awesome glyph, a pile item its own sprite).
/// <para>
/// Protocol version 15 (M4c slice 4, DV40): tg's own radial menus (<c>show_radial_menu</c>: the suit storage unit, the toolbox) are drawn here too, at the crosshair
/// instead of around the anchor: each slice is tg's picture (<c>icons/hud/radial.dmi</c>'s slice, hovered its <c>_focus</c> state) with the choice's image over it, laid
/// out clockwise from the top as tg lays them out, the centre button in the middle; the hovered slice's name shows under the ring (tg's screentip of the hovered
/// slice), and each slice carries its number key.
/// </para>
/// </summary>
public partial class Radial : Control
{
    private RadialModel _model;
    private ReplicaWorld _replica;
    private IAppearanceTextures _textures;

    /// <summary>The target's name over the ring.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>The HUD scale (the options' UI scale).</summary>
    public float UiScale { get; set; } = 1f;

    /// <summary>The owner (2026-10-30): how opaque the backgrounds and buttons are (PlayOptions.RadialOpacity); text and icons stay solid.</summary>
    public float Opacity { get; set; } = 1f;

    /// <summary>DV694: where the client's own wheel is centred (the inventory's right-click wheel: at the pointer); null: the screen's centre, the crosshair.</summary>
    public Vector2? Centre { get; set; }

    // the ring's sizes, the reference being a window 1650 px high (the owner, 2026-10-30): its radius and an entry's height
    private (float Ui, float Radius, float Height) Ring()
    {
        float ui = Mathf.Clamp(UiScale, 0.5f, 2f) * GetViewportRect().Size.Y / 1650f;
        return (ui, 300f * ui, 56f * ui);
    }

    /// <summary>DV694: the centre for a wheel opened at <paramref name="at"/>, moved in from the screen's edges so the ring and its entries stay on it.</summary>
    public Vector2 CentreNear(Vector2 at)
    {
        Vector2 size = GetViewportRect().Size;
        (_, float radius, float height) = Ring();
        float marginX = radius + 3.5f * height, marginY = radius + 1.5f * height; // an entry's label reaches out sideways; the title stands above
        float x = size.X <= 2f * marginX ? size.X / 2f : Mathf.Clamp(at.X, marginX, size.X - marginX);
        float y = size.Y <= 2f * marginY ? size.Y / 2f : Mathf.Clamp(at.Y, marginY, size.Y - marginY);
        return new Vector2(x, y);
    }

    public void Bind(RadialModel model, ReplicaWorld replica = null, IAppearanceTextures textures = null)
    {
        _model = model;
        _replica = replica;
        _textures = textures;
        Name = "Radial";
        MouseFilter = MouseFilterEnum.Ignore;
        TextureFilter = TextureFilterEnum.Nearest; // tg's pixel art
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
    }

    /// <summary>Redraws while open (the aiming vector changes every mouse move).</summary>
    public void Refresh()
    {
        bool open = _model != null && _model.Phase == RadialPhase.Open;
        if (open || Visible)
            QueueRedraw();
        Visible = open;
    }

    public override void _Draw()
    {
        if (_model == null || _model.Phase != RadialPhase.Open)
            return;
        if (_model.ServerMenu != 0)
        {
            DrawServer();
            return;
        }
        Vector2 centre = Centre ?? GetViewportRect().Size / 2f;
        Font font = ThemeDB.FallbackFont;
        // the owner (2026-10-30): a wider ring of larger buttons, each with an icon (a pile item its own sprite); the ring and its
        // buttons keep their share of the screen at every resolution, the sizes below being those of a window 1650 px high
        float ui = Ring().Ui;
        int size = Mathf.RoundToInt(19 * ui), chordSize = Mathf.RoundToInt(13 * ui);
        float icon = 40f * ui, pad = 8f * ui, height = icon + 2f * pad;
        float radius = 300f * ui, line = Mathf.Max(1f, ui);
        IReadOnlyList<RadialEntry> entries = _model.Entries;
        DrawCircle(centre, radius + height, new Color(0f, 0f, 0f, 0.28f * Opacity));
        DrawArc(centre, radius, 0, (float)Mathf.Tau, 96, new Color(1f, 1f, 1f, 0.25f * Opacity), 2f * line);
        DrawCircle(centre, (float)RadialModel.DeadZone * 0.5f, new Color(1f, 1f, 1f, 0.5f));
        if (Title.Length > 0)
            DrawString(font, centre + new Vector2(-radius, -radius - height), Title, HorizontalAlignment.Center, 2f * radius, Mathf.RoundToInt(22 * ui), new Color(1f, 0.95f, 0.7f));
        (double ax, double ay) = _model.Aim;
        DrawLine(centre, centre + new Vector2((float)ax, (float)ay).LimitLength(radius), new Color(1f, 1f, 0.6f, 0.9f), 3f * line);
        double seconds = Time.GetTicksMsec() / 1000.0;
        for (int i = 0; i < entries.Count; i++)
        {
            RadialEntry entry = entries[i];
            (double px, double py) = RadialModel.PositionOf(i, entries.Count, radius);
            string chord = entry.Chord;
            float width = Mathf.Max(font.GetStringSize(entry.Label, HorizontalAlignment.Left, -1, size).X, font.GetStringSize(chord, HorizontalAlignment.Left, -1, chordSize).X);
            var box = new Rect2(centre + new Vector2((float)px, (float)py) - new Vector2((icon + width) / 2f + 1.5f * pad, height / 2f), new Vector2(icon + width + 3f * pad, height));
            bool lit = i == _model.Highlight;
            bool fixedEntry = entry.Kind != RadialKind.Hint;
            Color fill = lit ? new Color(0.95f, 0.85f, 0.3f) : fixedEntry ? new Color(0.12f, 0.15f, 0.22f) : new Color(0.1f, 0.25f, 0.2f);
            DrawRect(box, fill with { A = 0.95f * Opacity });
            DrawRect(box, lit ? Colors.White : new Color(0.7f, 0.78f, 0.9f, 0.9f * Opacity), false, (lit ? 4f : 1.5f) * line);
            Color ink = lit ? Colors.Black : Colors.White;
            Vector2 iconCentre = box.Position + new Vector2(pad + icon / 2f, height / 2f);
            if (entry.Kind == RadialKind.Item)
                DrawPicture(entry.Icon, iconCentre, icon / 32f, seconds);
            else if (entry.IconName is string name && FontAwesomeFont.Glyph(name) is string glyph)
                DrawString(font, iconCentre + new Vector2(-icon / 2f, icon * 0.3f), glyph, HorizontalAlignment.Center, icon, Mathf.RoundToInt(icon * 0.75f), ink);
            float textX = box.Position.X + icon + 2f * pad;
            if (chord.Length > 0)
            {
                DrawString(font, new Vector2(textX, box.Position.Y + height * 0.48f), entry.Label, HorizontalAlignment.Left, -1, size, ink);
                DrawString(font, new Vector2(textX, box.Position.Y + height * 0.82f), chord, HorizontalAlignment.Left, -1, chordSize, lit ? new Color(0f, 0f, 0f, 0.7f) : new Color(1f, 1f, 1f, 0.6f));
            }
            else
                DrawString(font, new Vector2(textX, box.Position.Y + height / 2f + size * 0.35f), entry.Label, HorizontalAlignment.Left, -1, size, ink);
        }
    }

    // Version 15: tg's radial, its own pictures at twice tg's pixels (times the HUD scale) on a ring around the crosshair.
    private void DrawServer()
    {
        Vector2 centre = GetViewportRect().Size / 2f;
        Font font = ThemeDB.FallbackFont;
        float scale = 2f * Mathf.Max(0.5f, UiScale);
        IReadOnlyList<RadialEntry> entries = _model.Entries;
        // tg's ring: radius 32 pixels for 8 slices of 32; a page of more slices keeps them apart.
        float radius = Mathf.Max(32f, 4.5f * entries.Count) * scale;
        double seconds = Time.GetTicksMsec() / 1000.0;
        bool centreLit = _model.Highlight < 0;
        if (_model.ServerCloseButton)
            DrawPicture(centreLit ? _model.ServerCenterFocus : _model.ServerCenter, centre, scale, seconds);
        else
            DrawCircle(centre, 6f * scale, new Color(0f, 0f, 0f, 0.35f));
        for (int i = 0; i < entries.Count; i++)
        {
            RadialEntry entry = entries[i];
            (double px, double py) = RadialModel.PositionOf(i, entries.Count, radius);
            Vector2 at = centre + new Vector2((float)px, (float)py);
            bool lit = i == _model.Highlight;
            if (!DrawPicture(lit ? entry.BackgroundFocus : entry.Background, at, scale, seconds, Opacity))
                DrawCircle(at, 14f * scale, lit ? new Color(0.95f, 0.85f, 0.3f, 0.8f * Opacity) : new Color(0.1f, 0.15f, 0.25f, 0.8f * Opacity));
            DrawPicture(entry.Icon, at, scale, seconds);
            if (i < 9)
                DrawString(font, at + new Vector2(10f * scale, 15f * scale), (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), HorizontalAlignment.Left, -1, 13, new Color(1f, 1f, 1f, 0.8f));
        }
        string label = _model.Highlight >= 0 && _model.Highlight < entries.Count ? entries[_model.Highlight].Label : (_model.ServerCloseButton ? "Close Menu" : string.Empty);
        if (_model.ServerPage.Length > 0)
            label = (label.Length > 0 ? label + "   " : string.Empty) + "(" + _model.ServerPage + ")";
        if (label.Length > 0)
        {
            var at = centre + new Vector2(-200f, radius + 24f * scale);
            DrawString(font, at + new Vector2(1f, 1f), label, HorizontalAlignment.Center, 400, 18, new Color(0f, 0f, 0f, 0.8f));
            DrawString(font, at, label, HorizontalAlignment.Center, 400, 18, new Color(1f, 0.95f, 0.7f));
        }
    }

    // A table appearance composed (tg's own sprite), its icon square centred at the point; false when it cannot be drawn (yet).
    private bool DrawPicture(TableRef appearance, Vector2 at, float scale, double seconds, float alpha = 1f)
    {
        if (appearance.IsNone || _replica == null || _textures == null || !_textures.TryCompose(_replica, appearance, 2, seconds, out ComposedAppearance composed) || composed.Texture == null)
            return false;
        float left = at.X - 16f * scale - composed.OriginX * scale;
        float bottom = at.Y + 16f * scale + composed.OriginY * scale;
        var rect = new Rect2(left, bottom - composed.PixelHeight * scale, composed.PixelWidth * scale, composed.PixelHeight * scale);
        DrawTextureRect(composed.Texture, rect, false, new Color(1f, 1f, 1f, alpha));
        return true;
    }
}
