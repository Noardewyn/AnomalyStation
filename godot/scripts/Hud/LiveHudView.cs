using Godot;
using Anomaly.Client.Shared;

namespace Anomaly.Client.Hud;

/// <summary>
/// The live HUD's native boxes (ui-overhaul.md section 6, wireframes Mockup 1): the doll panel's background, and the combat pill under it (a
/// key chip with the key bound to combat mode and "Combat" or "Peaceful", red when on); slice C: the two hand boxes with the held items' pictures,
/// the active one larger with an amber border, the swap-hand chip over the right one, and the held-item card. It only draws: <see cref="HudLayer"/>
/// places it first in its root, so tg's pictures (the doll) stand over it and it hides with the interface, and reports the pill's and the hand
/// boxes' rectangles to the HUD pick under their screen entries.
/// </summary>
public partial class LiveHudView : Control
{
    private LiveHudLayout _layout;
    private float _unit = 1f;
    private bool _panel;
    private bool? _combat;
    private string _key;
    private bool _handsDrawn;
    private readonly StyleBoxFlat _panelStyle = new();
    private readonly StyleBoxFlat _pillStyle = new();
    private readonly StyleBoxFlat _boxStyle = new();

    /// <summary>A held item's picture and where it is drawn.</summary>
    public readonly record struct Picture(Texture2D Texture, Rect2 Rect);

    /// <summary>Slice C: what the hands and the card show this frame (<see cref="HudLayer"/> fills it, then calls <see cref="ShowHands"/>).</summary>
    public sealed class HandsScene
    {
        public bool Shown;
        public HandsLayout Layout;
        public bool LeftActive, RightActive, HasLeft, HasRight;
        public readonly List<Picture> Left = new(), Right = new(), LeftOverlays = new(), RightOverlays = new(); // the items, then tg's handcuffs and blocked mark
        public PromptLine? Swap;
        public bool CardShown;
        public HudRect Card;
        public float CardUnit;
        public string CardHand;
        public List<PromptLine> CardLines = new();
        public readonly List<Picture> CardPictures = new();
    }

    /// <summary>Slice C: the hands and the card.</summary>
    public HandsScene Hands { get; } = new();

    public LiveHudView()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        TextureFilter = TextureFilterEnum.Nearest; // tg's item pictures, pixel for pixel
        SetAnchorsPreset(LayoutPreset.FullRect);
    }

    /// <summary>What to draw this frame: the panel when a doll is shown, the pill when a combat toggle is shown (null: none) and the key chip's label (null: unbound).</summary>
    public void Show(in LiveHudLayout layout, float unit, bool panel, bool? combat, string key)
    {
        if (layout == _layout && unit == _unit && panel == _panel && combat == _combat && key == _key)
            return;
        (_layout, _unit, _panel, _combat, _key) = (layout, unit, panel, combat, key);
        QueueRedraw();
    }

    /// <summary>Slice C: <see cref="Hands"/> was filled for this frame (the held items' pictures animate, so it is drawn afresh while shown).</summary>
    public void ShowHands()
    {
        if (Hands.Shown || _handsDrawn)
            QueueRedraw();
        _handsDrawn = Hands.Shown;
    }

    private static Rect2 R(HudRect r) => new(r.Left, r.Top, r.Width, r.Height);

    private static int FontPx(float referencePixels, float u) => Math.Max(1, (int)Math.Round(referencePixels * u));

    public override void _Draw()
    {
        Font font = ThemeDB.FallbackFont;
        float u = _unit;
        if (_panel)
        {
            _panelStyle.BgColor = ChatLines.Rgba(UiTokens.DollPanelBack);
            _panelStyle.SetCornerRadiusAll(Math.Max(1, (int)Math.Round(UiTokens.DollPanelRadius * u)));
            DrawStyleBox(_panelStyle, R(_layout.Panel));
        }
        if (Hands.Shown)
            DrawHands(font);
        if (_combat is not bool on)
            return;
        HudRect pill = _layout.Pill;
        _pillStyle.BgColor = ChatLines.Rgba(on ? UiTokens.PillCombat : UiTokens.ChipBack);
        _pillStyle.BorderColor = ChatLines.Rgba(UiTokens.ChipRim);
        _pillStyle.SetBorderWidthAll(Math.Max(1, (int)Math.Round(UiTokens.ChipRimWidth * u)));
        _pillStyle.SetCornerRadiusAll(Math.Max(1, (int)(pill.Height / 2f)));
        DrawStyleBox(_pillStyle, R(pill));
        string text = LiveHud.CombatText(on);
        int textSize = FontPx(UiTokens.PillFont, u), chipSize = FontPx(UiTokens.ChipFont, u);
        float textWidth = font.GetStringSize(text, HorizontalAlignment.Left, -1, textSize).X;
        float chipWidth = _key == null ? 0f : font.GetStringSize(_key, HorizontalAlignment.Left, -1, chipSize).X + 2 * UiTokens.ChipPadX * u;
        float gap = _key == null ? 0f : UiTokens.PromptChipGap * u;
        float left = pill.Left + Math.Max(0f, (pill.Width - chipWidth - gap - textWidth) / 2f);
        if (_key != null)
        {
            float chipHeight = UiTokens.ChipHeight * u;
            ChatLines.DrawChip(this, font, new Rect2(left, pill.Top + (pill.Height - chipHeight) / 2f, chipWidth, chipHeight), _key, chipSize, u, 1f);
        }
        float textLeft = left + chipWidth + gap;
        float textBaseline = pill.Top + (pill.Height + font.GetAscent(textSize) - font.GetDescent(textSize)) / 2f;
        DrawString(font, new Vector2(textLeft, textBaseline), text, HorizontalAlignment.Left, Math.Max(1f, pill.Right - textLeft), textSize, ChatLines.Rgba(UiTokens.LocalText));
    }

    // Slice C: the hand boxes (the active one with its border), the swap chip over the right one, their items, and the held-item card.
    private void DrawHands(Font font)
    {
        HandsScene h = Hands;
        float k = h.Layout.Unit;
        if (h.HasLeft)
            DrawHand(h.Layout.Left, h.LeftActive, h.Left, h.LeftOverlays, k);
        if (h.HasRight)
            DrawHand(h.Layout.Right, h.RightActive, h.Right, h.RightOverlays, k);
        if (h.Swap is PromptLine swap && h.Layout.SwapChip.Width > 0) // no clear place for it at this size: left out
            DrawLine(font, h.Layout.SwapChip, swap, k, alignRight: true);
        if (!h.CardShown)
            return;
        float c = h.CardUnit;
        _boxStyle.BgColor = ChatLines.Rgba(UiTokens.CardBack);
        _boxStyle.SetBorderWidthAll(0);
        _boxStyle.SetCornerRadiusAll(Math.Max(1, (int)Math.Round(UiTokens.DollPanelRadius * c)));
        DrawStyleBox(_boxStyle, R(h.Card));
        HudRect picture = LiveHud.CardPicture(h.Card, c);
        foreach (Picture p in h.CardPictures)
            DrawTextureRect(p.Texture, p.Rect, false);
        float pad = MathF.Round(UiTokens.CardPad * c);
        int nameSize = FontPx(UiTokens.PromptFont, c);
        float nameLeft = picture.Right + pad, nameWidth = Math.Max(1f, h.Card.Right - pad - nameLeft);
        string name = KeyChips.Ellipsize(h.CardHand, nameWidth, s => font.GetStringSize(s, HorizontalAlignment.Left, -1, nameSize).X);
        float nameBaseline = picture.Top + (picture.Height + font.GetAscent(nameSize) - font.GetDescent(nameSize)) / 2f;
        DrawString(font, new Vector2(nameLeft, nameBaseline), name, HorizontalAlignment.Left, nameWidth, nameSize, ChatLines.Rgba(UiTokens.LocalText));
        float y = picture.Bottom + MathF.Round(UiTokens.CardHeaderGap * c);
        foreach (PromptLine line in h.CardLines)
        {
            DrawLine(font, new HudRect(h.Card.Left + pad, y, h.Card.Width - 2 * pad, UiTokens.ChipHeight * c), line, c, alignRight: false);
            y += UiTokens.PromptLine * c;
        }
    }

    private void DrawHand(HudRect box, bool active, List<Picture> pictures, List<Picture> overlays, float k)
    {
        _boxStyle.BgColor = ChatLines.Rgba(UiTokens.HandBack);
        _boxStyle.BorderColor = ChatLines.Rgba(active ? UiTokens.HandActiveBorder : UiTokens.HandRim);
        _boxStyle.SetBorderWidthAll(active ? Math.Max(1, (int)Math.Round(UiTokens.HandBorder * k)) : Math.Max(1, (int)Math.Round(k)));
        _boxStyle.SetCornerRadiusAll(Math.Max(1, (int)Math.Round(UiTokens.ChipRadius * k)));
        DrawStyleBox(_boxStyle, R(box));
        foreach (Picture p in pictures)
            DrawTextureRect(p.Texture, p.Rect, false);
        foreach (Picture p in overlays)
            DrawTextureRect(p.Texture, p.Rect, false);
    }

    // A key chip and its words in `room` (a chip high), from its left, or from its right; the words cut to fit.
    private void DrawLine(Font font, HudRect room, PromptLine line, float k, bool alignRight)
    {
        int chipSize = FontPx(UiTokens.ChipFont, k), textSize = FontPx(UiTokens.PromptFont, k);
        float chipWidth = Math.Min(room.Width, font.GetStringSize(line.Key, HorizontalAlignment.Left, -1, chipSize).X + 2 * UiTokens.ChipPadX * k);
        float gap = UiTokens.PromptChipGap * k;
        float textRoom = Math.Max(0f, room.Width - chipWidth - gap);
        string text = KeyChips.Ellipsize(line.Text, textRoom, s => font.GetStringSize(s, HorizontalAlignment.Left, -1, textSize).X);
        float textWidth = string.IsNullOrEmpty(text) ? 0f : font.GetStringSize(text, HorizontalAlignment.Left, -1, textSize).X;
        float left = alignRight ? room.Right - chipWidth - gap - textWidth : room.Left;
        ChatLines.DrawChip(this, font, new Rect2(left, room.Top, chipWidth, room.Height), line.Key, chipSize, k, 1f);
        if (string.IsNullOrEmpty(text))
            return;
        float textLeft = left + chipWidth + gap;
        float baseline = room.Top + (room.Height + font.GetAscent(textSize) - font.GetDescent(textSize)) / 2f;
        // outlined, as the prompts: it stands over the world
        DrawStringOutline(font, new Vector2(textLeft, baseline), text, HorizontalAlignment.Left, textWidth + 1f, textSize,
            Math.Max(1, (int)Math.Round(UiTokens.TextOutlineWidth * k)), ChatLines.Rgba(UiTokens.TextOutline));
        DrawString(font, new Vector2(textLeft, baseline), text, HorizontalAlignment.Left, textWidth + 1f, textSize, ChatLines.Rgba(UiTokens.LocalText));
    }
}
