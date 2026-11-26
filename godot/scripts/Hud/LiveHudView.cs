using System.Linq;
using Godot;
using Anomaly.Client.Shared;

namespace Anomaly.Client.Hud;

/// <summary>
/// The live HUD's native boxes (ui-overhaul.md section 6, wireframes Mockup 1): the doll panel's background, and the combat pill under it (a
/// key chip with the key bound to combat mode and "Combat" or "Peaceful", red when on); slice C: the two hand boxes with the held items' pictures,
/// the active one larger with an amber border, the swap-hands key on the inactive one, a red border on a cuffed or missing hand;
/// slice D: the vitals panel's cell lines, the alert chips by the panel and the held-item hint strip over the hands. It only draws: <see cref="HudLayer"/>
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

    /// <summary>Slice C: what the hands and the hint strip show this frame (<see cref="HudLayer"/> fills it, then calls <see cref="ShowHands"/>).</summary>
    public sealed class HandsScene
    {
        public bool Shown;
        public HandsLayout Layout;
        public bool LeftActive, RightActive, HasLeft, HasRight;
        public readonly List<Picture> Left = new(), Right = new(), LeftOverlays = new(), RightOverlays = new(); // the items, then tg's handcuffs and blocked mark
        public string Swap; // the swap-hands key cap's label (null: unbound)
        public bool LeftMarked, RightMarked; // tg's handcuff or blocked mark on that hand (LiveHud.HandMarked)
        public bool HintShown; // slice D: the held-item hint strip over the row
        public HudRect Hint;
        public float HintShare, HintUnit;
        public List<PromptLine> HintLines = new();
        public readonly List<Picture> HintPictures = new();
    }

    /// <summary>Slice C: the hands; slice D: the hint strip.</summary>
    public HandsScene Hands { get; } = new();

    private readonly List<HudRect> _alertChips = new();
    private readonly StyleBoxFlat _chipStyle = new();

    /// <summary>Slice D: the alert chips' boxes this frame (tg's alert pictures are drawn over them).</summary>
    public void ShowAlerts(List<HudRect> chips)
    {
        if (chips.SequenceEqual(_alertChips))
            return;
        _alertChips.Clear();
        _alertChips.AddRange(chips);
        QueueRedraw();
    }

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
            if (_layout.Vitals) // slice D: a line between two cells of the vitals panel, in the middle of their gap
            {
                HudRect[] cells = { _layout.Doll, _layout.Health, _layout.Mood };
                for (int i = 1; i < cells.Length; i++)
                {
                    float y = MathF.Round((cells[i - 1].Bottom + cells[i].Top) / 2f);
                    DrawRect(new Rect2(_layout.Panel.Left, y, _layout.Panel.Width, 1f), ChatLines.Rgba(UiTokens.VitalsLine));
                }
            }
        }
        if (Hands.Shown)
            DrawHands(font);
        foreach (HudRect chip in _alertChips)
        {
            float c = chip.Width / UiTokens.AlertChip; // a chip made smaller to fit has smaller corners too
            _chipStyle.BgColor = ChatLines.Rgba(UiTokens.DollPanelBack); // the doll panel's fill; the panel has no rim
            _chipStyle.SetCornerRadiusAll(Math.Max(1, (int)Math.Round(UiTokens.DollPanelRadius * c)));
            DrawStyleBox(_chipStyle, R(chip));
        }
        if (_combat is not bool on)
            return;
        HudRect pill = _layout.Pill;
        _pillStyle.BgColor = ChatLines.Rgba(on ? UiTokens.PillCombat : UiTokens.ChipBack);
        _pillStyle.BorderColor = ChatLines.Rgba(UiTokens.ChipRim);
        _pillStyle.SetBorderWidthAll(Math.Max(1, (int)Math.Round(UiTokens.ChipRimWidth * u)));
        _pillStyle.SetCornerRadiusAll(Math.Max(1, (int)(pill.Height / 2f)));
        DrawStyleBox(_pillStyle, R(pill));
        string text = LiveHud.CombatText(on);
        // slice D: the key chip and the whole word, made smaller together where they would not fit the pill (or a pill made smaller), never cut
        float p = u * Math.Min(1f, pill.Height / (UiTokens.PillHeight * u));
        float Needed(float k) => font.GetStringSize(text, HorizontalAlignment.Left, -1, FontPx(UiTokens.PillFont, k)).X
            + (_key == null ? 0f : font.GetStringSize(_key, HorizontalAlignment.Left, -1, FontPx(UiTokens.ChipFont, k)).X + 2 * UiTokens.ChipPadX * k + UiTokens.PromptChipGap * k);
        u = p * LiveHud.PillFit(Needed(p), pill.Width - 2 * UiTokens.ChipPadX * p);
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
        DrawString(font, new Vector2(textLeft, textBaseline), text, HorizontalAlignment.Left, -1, textSize, ChatLines.Rgba(UiTokens.LocalText));
    }

    // Slice C: the hand boxes (the active one with its border, a cuffed or missing one red), the swap key on the inactive one, their items, and
    // slice D's hint strip over them.
    private void DrawHands(Font font)
    {
        HandsScene h = Hands;
        float k = h.Layout.Unit;
        if (h.HasLeft)
            DrawHand(h.Layout.Left, h.LeftActive, h.LeftMarked, h.Left, h.LeftOverlays, k);
        if (h.HasRight)
            DrawHand(h.Layout.Right, h.RightActive, h.RightMarked, h.Right, h.RightOverlays, k);
        if (h.Swap != null && (h.LeftActive ? h.HasRight : h.HasLeft))
        {
            int capSize = FontPx(UiTokens.SwapCapFont, k);
            float capWidth = font.GetStringSize(h.Swap, HorizontalAlignment.Left, -1, capSize).X + 2 * UiTokens.SwapCapInset * k;
            HudRect cap = LiveHud.SwapCap(h.Layout, h.LeftActive, h.RightActive, capWidth);
            if (cap.Width > 0f) // over the corner of the item's picture
                ChatLines.DrawChip(this, font, R(cap), h.Swap, capSize, k, 1f);
        }
        if (!h.HintShown)
            return;
        // slice D: the hint strip over the row: the pill's dark fill and rounded ends, the held item's picture, then each key cap and its verb
        float s = h.HintUnit * h.HintShare, t = s * UiTokens.HintShare;
        _boxStyle.BgColor = ChatLines.Rgba(UiTokens.ChipBack);
        _boxStyle.SetBorderWidthAll(0);
        _boxStyle.SetCornerRadiusAll(Math.Max(1, (int)(h.Hint.Height / 2f)));
        DrawStyleBox(_boxStyle, R(h.Hint));
        foreach (Picture p in h.HintPictures)
            DrawTextureRect(p.Texture, p.Rect, false);
        int hintCap = FontPx(UiTokens.ChipFont, t), verbSize = FontPx(UiTokens.PromptFont, t);
        float x = LiveHud.HintPictureSquare(h.Hint, h.HintUnit, h.HintShare).Right;
        float chipHeight = Math.Min(h.Hint.Height, UiTokens.ChipHeight * t);
        float baseline = h.Hint.Top + (h.Hint.Height + font.GetAscent(verbSize) - font.GetDescent(verbSize)) / 2f;
        foreach (PromptLine line in h.HintLines)
        {
            x += UiTokens.HintGap * s;
            float capWidth = font.GetStringSize(line.Key, HorizontalAlignment.Left, -1, hintCap).X + 2 * UiTokens.ChipPadX * t;
            ChatLines.DrawChip(this, font, new Rect2(x, h.Hint.Top + (h.Hint.Height - chipHeight) / 2f, capWidth, chipHeight), line.Key, hintCap, t, 1f);
            x += capWidth + UiTokens.HintCapGap * s;
            DrawString(font, new Vector2(x, baseline), line.Text, HorizontalAlignment.Left, -1, verbSize, ChatLines.Rgba(UiTokens.LocalText));
            x += font.GetStringSize(line.Text, HorizontalAlignment.Left, -1, verbSize).X;
        }
    }

    private void DrawHand(HudRect box, bool active, bool marked, List<Picture> pictures, List<Picture> overlays, float k)
    {
        // a cuffed or missing hand: a red border and a lighter fill under tg's mark, active or not
        _boxStyle.BgColor = ChatLines.Rgba(marked ? UiTokens.HandMarkedBack : UiTokens.HandBack);
        _boxStyle.BorderColor = ChatLines.Rgba(marked ? UiTokens.HandMarkedBorder : active ? UiTokens.HandActiveBorder : UiTokens.HandRim);
        _boxStyle.SetBorderWidthAll(active || marked ? Math.Max(1, (int)Math.Round(UiTokens.HandBorder * k)) : Math.Max(1, (int)Math.Round(k)));
        _boxStyle.SetCornerRadiusAll(Math.Max(1, (int)Math.Round(UiTokens.ChipRadius * k)));
        DrawStyleBox(_boxStyle, R(box));
        foreach (Picture p in pictures)
            DrawTextureRect(p.Texture, p.Rect, false);
        foreach (Picture p in overlays)
            DrawTextureRect(p.Texture, p.Rect, false);
    }
}
