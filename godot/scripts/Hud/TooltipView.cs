using Godot;
using Anomaly.Client.Hud.Maptext;
using Anomaly.Client.Shared.Maptext;
using Anomaly.Protocol;

namespace Anomaly.Client.Hud;

/// <summary>
/// Protocol version 19 (tg's tooltips, DV47): draws the session's tooltip (<see cref="ReplicaWorld.Tooltip"/>: tg's HTML text and theme) as tg's
/// tooltip page draws it (<see cref="TooltipLayout"/>: the theme's outer border, inner border and background, the bold 12px sans text with its
/// h1 title), scaled with the HUD, under the pointer (<see cref="TooltipLayout.Position"/>). It shows while the server says so and the pointer is
/// free (cursor mode, where the HUD is hovered); it does not take the mouse.
/// </summary>
public partial class TooltipView : Control
{
    // The system pointer's arrow is about this tall: the box starts under it.
    private const float PointerHeight = 22f;

    private readonly MaptextFonts _fonts = MaptextRenderer.Shared.Fonts;
    private TooltipMessage _shown;
    private float _scale = 1f;
    private TooltipBox _box;
    private string _laidText, _laidTheme;
    private float _laidScale;

    public TooltipView()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Visible = false;
    }

    /// <summary>The laid out box drawn now (null when hidden).</summary>
    public TooltipBox Box => Visible ? _box : null;

    /// <summary>
    /// Shows <paramref name="tooltip"/> (null hides) at the pointer <paramref name="pointer"/> on a screen of <paramref name="screen"/> pixels, at the HUD's
    /// <paramref name="scale"/>; <paramref name="allowed"/> is false while the pointer is captured (no HUD is hovered then).
    /// </summary>
    public void Show(TooltipMessage tooltip, Vector2 pointer, Vector2 screen, float scale, bool allowed)
    {
        if (tooltip == null || !allowed)
        {
            Visible = false;
            _shown = null;
            return;
        }

        scale = Mathf.Max(0.5f, scale);
        if (_box == null || tooltip.Text != _laidText || tooltip.Theme != _laidTheme || !Mathf.IsEqualApprox(scale, _laidScale))
        {
            _box = TooltipLayout.Place(tooltip.Text, tooltip.Theme, new ScaledMetrics(_fonts, scale));
            _laidText = tooltip.Text;
            _laidTheme = tooltip.Theme;
            _laidScale = scale;
            QueueRedraw();
        }

        _shown = tooltip;
        _scale = scale;
        var size = new Vector2(Mathf.Ceil(_box.Width * scale), Mathf.Ceil(_box.Height * scale));
        (float x, float y) = TooltipLayout.Position(pointer.X, pointer.Y, size.X, size.Y, screen.X, screen.Y, PointerHeight);
        Position = new Vector2(x, y);
        Size = size;
        Visible = true;
    }

    public override void _Draw()
    {
        if (_box == null || _shown == null)
            return;
        float s = _scale;
        TooltipTheme theme = _box.Theme;
        var whole = new Rect2(Vector2.Zero, Size);
        DrawRect(whole, ToColor(theme.WrapBorder));
        Rect2 content = whole.Grow(-Mathf.Round(TooltipLayout.WrapBorder * s));
        DrawRect(content, ToColor(theme.ContentBorder));
        DrawRect(content.Grow(-Mathf.Round(TooltipLayout.ContentBorder * s)), ToColor(theme.Background));
        var origin = new Vector2(_box.TextX * s, _box.TextY * s);
        foreach (PlacedText placed in _box.Text.Items)
        {
            MaptextStyle style = placed.Style;
            Font font = _fonts.Get(style.Font, style.Bold, style.Italic);
            int fontSize = MaptextFonts.ScreenSize(style.SizePx, s);
            var pos = new Vector2(Mathf.Round(origin.X + placed.X * s), Mathf.Round(origin.Y + placed.Baseline * s));
            Color color = ToColor(style.Rgba);
            DrawString(font, pos, placed.Text, HorizontalAlignment.Left, -1, fontSize, color);
            if (style.Underline)
                DrawLine(pos + new Vector2(0, Mathf.Max(1f, s)), pos + new Vector2(placed.Width * s, Mathf.Max(1f, s)), color, Mathf.Max(1f, s));
        }
    }

    private static Color ToColor(uint rgba)
        => new(((rgba >> 24) & 0xFF) / 255f, ((rgba >> 16) & 0xFF) / 255f, ((rgba >> 8) & 0xFF) / 255f, (rgba & 0xFF) / 255f);

    // The fonts' measurements at the drawn size, in CSS pixels: the box is laid out with the widths the text is drawn with.
    private sealed class ScaledMetrics : IMaptextMetrics
    {
        private readonly MaptextFonts _fonts;
        private readonly float _scale;

        public ScaledMetrics(MaptextFonts fonts, float scale)
        {
            _fonts = fonts;
            _scale = scale;
        }

        private Font Font(in MaptextStyle style) => _fonts.Get(style.Font, style.Bold, style.Italic);

        private int Size(in MaptextStyle style) => MaptextFonts.ScreenSize(style.SizePx, _scale);

        public float Width(in MaptextStyle style, string text) => Font(style).GetStringSize(text, HorizontalAlignment.Left, -1, Size(style)).X / _scale;

        public float LineHeight(in MaptextStyle style) => Font(style).GetHeight(Size(style)) / _scale;

        public float Ascent(in MaptextStyle style) => Font(style).GetAscent(Size(style)) / _scale;
    }
}
