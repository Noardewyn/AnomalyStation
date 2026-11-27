using System.Linq;
using Godot;
using Anomaly.Client.Shared;
using Anomaly.Protocol;

namespace Anomaly.Client.Hud;

/// <summary>
/// The live HUD's native boxes (ui-overhaul.md section 6, wireframes Mockup 1): slice C's two hand boxes with the held items' pictures, the active
/// one larger with an amber border, the swap-hands key on the inactive one, a red border on a cuffed or missing hand; package 3's ten hotbar cells
/// between them (ui-overhaul.md section 10); slice D's alert chips by the vitals and the held-item hint strip over the hands. It only draws: <see cref="HudLayer"/> places it first in its root, so tg's pictures (the
/// alerts in their chips) stand over it and it hides with the interface, and reports the hand boxes' rectangles to the HUD pick under their
/// screen entries.
/// </summary>
public partial class LiveHudView : Control
{
    private bool _handsDrawn;
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
        public string HintName; // package 3: the held item's name at the strip's head, cut to fit (null: none)
        public readonly CellScene[] Cells = Enumerable.Range(0, HotbarMessage.CellCount).Select(_ => new CellScene()).ToArray(); // package 3: the bar
    }

    /// <summary>Package 3: one cell of the bar this frame: where it is, what it shows (<see cref="LiveHud.CellLook"/>) and its item's picture.</summary>
    public sealed class CellScene
    {
        public HudRect Rect;
        public HotbarCellLook Look;
        public readonly List<Picture> Pictures = new();
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
        if (Hands.Shown)
            DrawHands(font);
        foreach (HudRect chip in _alertChips)
        {
            float c = chip.Width / UiTokens.AlertChip; // a chip made smaller to fit has smaller corners too
            _chipStyle.BgColor = ChatLines.Rgba(UiTokens.DollPanelBack);
            _chipStyle.SetCornerRadiusAll(Math.Max(1, (int)Math.Round(UiTokens.DollPanelRadius * c)));
            DrawStyleBox(_chipStyle, R(chip));
        }
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
                DrawCap(this, font, cap, h.Swap, capSize, k, 1f);
        }
        foreach (CellScene cell in h.Cells)
            DrawCell(font, cell, k);
        if (!h.HintShown)
            return;
        // slice D: the hint strip over the row: a dark fill and rounded ends, the held item's picture, then each key cap and its verb
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
        if (!string.IsNullOrEmpty(h.HintName)) // package 3: the held item's name leads the line
        {
            x += UiTokens.HintGap * s;
            DrawString(font, new Vector2(x, baseline), h.HintName, HorizontalAlignment.Left, -1, verbSize, ChatLines.Rgba(UiTokens.LocalText));
            x += font.GetStringSize(h.HintName, HorizontalAlignment.Left, -1, verbSize).X;
        }
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

    // Package 3: a cell of the bar: the chips' fill with the faint rim (red while a refused press blinks), the item's picture as a hand box draws
    // it (fainter while the item is out in a hand), its key cap in the top left corner (dim on an empty cell); the whole cell fainter for a belt
    // cell with no belt worn.
    private void DrawCell(Font font, CellScene cell, float k)
    {
        HotbarCellLook look = cell.Look;
        _boxStyle.BgColor = ChatLines.Rgba(UiTokens.DollPanelBack, look.Alpha);
        _boxStyle.BorderColor = ChatLines.Rgba(look.Rim, look.Alpha);
        _boxStyle.SetBorderWidthAll(Math.Max(1, (int)Math.Round(k)));
        _boxStyle.SetCornerRadiusAll(Math.Max(1, (int)Math.Round(UiTokens.DollPanelRadius * k)));
        DrawStyleBox(_boxStyle, R(cell.Rect));
        var tint = new Color(1f, 1f, 1f, look.Alpha * look.PictureAlpha);
        foreach (Picture p in cell.Pictures)
            DrawTextureRect(p.Texture, p.Rect, false, tint);
        if (look.Cap == null)
            return;
        int capSize = FontPx(UiTokens.SwapCapFont, k);
        HudRect cap = LiveHud.CellCap(cell.Rect, k, font.GetStringSize(look.Cap, HorizontalAlignment.Left, -1, capSize).X + 2 * UiTokens.SwapCapInset * k);
        if (cap.Width > 0f)
            DrawCap(this, font, cap, look.Cap, capSize, k, look.Alpha * look.CapAlpha);
    }

    /// <summary>
    /// A key cap (a cell's, the swap-hands cap, an action button's) in <paramref name="cap"/>: its label at <paramref name="size"/>, or smaller
    /// and then cut where it does not fit the cap's inner width (<see cref="LiveHud.CapLabel"/>), never past the cap.
    /// </summary>
    public static void DrawCap(CanvasItem canvas, Font font, HudRect cap, string label, int size, float k, float alpha)
    {
        (string text, int fitted) = LiveHud.CapLabel(label, cap.Width - 2 * UiTokens.SwapCapInset * k, size,
            (t, s) => font.GetStringSize(t, HorizontalAlignment.Left, -1, s).X);
        ChatLines.DrawChip(canvas, font, R(cap), text, fitted, k, alpha);
    }

    /// <summary>The cap of a tg action button's key (its top left corner, <see cref="LiveHud.CellCap"/>): a child of the button's container, drawn over it.</summary>
    public sealed partial class KeyCap : Control
    {
        private HudRect _cap;
        private string _label;
        private float _unit;

        public KeyCap() => MouseFilter = MouseFilterEnum.Ignore;

        /// <summary>The cap for <paramref name="label"/> in the button <paramref name="button"/> at <paramref name="unit"/> screen pixels per reference pixel.</summary>
        public void Set(HudRect button, string label, float unit)
        {
            int size = FontPx(UiTokens.SwapCapFont, unit);
            HudRect cap = LiveHud.CellCap(button, unit, ThemeDB.FallbackFont.GetStringSize(label, HorizontalAlignment.Left, -1, size).X + 2 * UiTokens.SwapCapInset * unit);
            if (cap == _cap && label == _label && unit == _unit)
                return;
            (_cap, _label, _unit) = (cap, label, unit);
            QueueRedraw();
        }

        public override void _Draw()
        {
            if (_label != null && _cap.Width > 0f)
                DrawCap(this, ThemeDB.FallbackFont, _cap, _label, FontPx(UiTokens.SwapCapFont, _unit), _unit, 1f);
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
