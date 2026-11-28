using System.Linq;
using Godot;
using Anomaly.Client.Shared;
using Anomaly.Protocol;

namespace Anomaly.Client.Hud;

/// <summary>
/// The live HUD's own boxes (ui-overhaul.md section 6, wireframes Mockup 1): slice C's two hand boxes with the held items' pictures, the active
/// one larger, the swap-hands key on the inactive one, a red border on a cuffed or missing hand; package 3's ten hotbar cells between them
/// (ui-overhaul.md section 10); section 16: the boxes and cells drawn with tg's own slot pictures of the player's HUD style; slice D's alert chips by
/// the vitals and the held-item hint strip over the hands; slice 4A's character panel under Tab (its cells and the own sprite). It only draws: <see cref="HudLayer"/> places it first in its root, so tg's pictures (the
/// alerts in their chips) stand over it and it hides with the interface, and reports the hand boxes' rectangles to the HUD pick under their
/// screen entries.
/// </summary>
public partial class LiveHudView : Control
{
    private bool _handsDrawn;
    private readonly StyleBoxFlat _boxStyle = new();

    /// <summary>A picture, where it is drawn, and its tint (<c>0xRRGGBBAA</c>; section 16: tg's slot picture with its entry's colour and alpha and the cell's opacity, <see cref="LiveHud.SlotTint"/>).</summary>
    public readonly record struct Picture(Texture2D Texture, Rect2 Rect, uint Rgba = 0xFFFFFFFFu);

    /// <summary>Slice C: what the hands and the hint strip show this frame (<see cref="HudLayer"/> fills it, then calls <see cref="ShowHands"/>).</summary>
    public sealed class HandsScene
    {
        public bool Shown;
        public HandsLayout Layout;
        public bool LeftActive, RightActive, HasLeft, HasRight;
        public readonly List<Picture> LeftArt = new(), RightArt = new(); // section 16: tg's hand slot picture and its active mark, filling the box
        public readonly List<Picture> Left = new(), Right = new(), LeftOverlays = new(), RightOverlays = new(); // the items, then tg's handcuffs and blocked mark
        public string Swap; // the swap-hands key cap's label (null: unbound)
        public bool LeftMarked, RightMarked; // tg's handcuff or blocked mark on that hand (LiveHud.HandMarked)
        public uint LeftRim, RightRim; // slice 4B-2: a refused move's red blink, or the light rim of a drag over the box (0: none)
        public bool HintShown; // slice D: the held-item hint strip over the row
        public HudRect Hint;
        public float HintShare, HintUnit;
        public List<PromptLine> HintLines = new();
        public readonly List<Picture> HintPictures = new();
        public string HintName; // package 3: the held item's name at the strip's head, cut to fit (null: none)
        public readonly CellScene[] Cells = Enumerable.Range(0, HotbarMessage.CellCount).Select(_ => new CellScene()).ToArray(); // package 3: the bar
    }

    /// <summary>
    /// Package 3: one cell of the bar this frame: where it is, what it shows (<see cref="LiveHud.CellLook"/>) and its item's picture; section 16: tg's
    /// slot picture under it. Slice 4A: a worn slot's cell in the character panel, with tg's marks over its item.
    /// </summary>
    public sealed class CellScene
    {
        public HudRect Rect;
        public HotbarCellLook Look = new(1f, default, 1f, null, 1f, 0u);
        public readonly List<Picture> Art = new(), Pictures = new(), Overlays = new();
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

    private readonly List<CellScene> _panelCells = new();
    private float _panelUnit;
    private Texture2D _sprite;
    private Rect2 _spriteAt, _spriteFrom;

    /// <summary>
    /// Slice 4A: the character panel this frame: the cells of tg's worn slots at <paramref name="unit"/> (section 16: tg's slot picture filling each,
    /// the worn item in its picture square), and the own sprite's picture (<paramref name="sprite"/>; null: none), its part <paramref name="from"/>
    /// drawn at <paramref name="at"/>. No backing.
    /// </summary>
    public void ShowPanel(List<CellScene> cells, float unit, Texture2D sprite, Rect2 at, Rect2 from)
    {
        if (sprite == null && _sprite == null && cells.Count == 0 && _panelCells.Count == 0)
            return;
        _panelCells.Clear();
        _panelCells.AddRange(cells);
        (_panelUnit, _sprite, _spriteAt, _spriteFrom) = (unit, sprite, at, from);
        QueueRedraw(); // the sprite's and the items' pictures animate and change with what is worn: drawn afresh while shown
    }

    public LiveHudView()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        TextureFilter = TextureFilterEnum.Nearest; // tg's item pictures, pixel for pixel
        SetAnchorsPreset(LayoutPreset.FullRect);
        Column = new ColumnView { Name = "StorageColumn", Visible = false };
        AddChild(Column);
        // slice 4C-2: the pinned panel (its backing and header, then its rows cut under the header), the Nearby strip (likewise) and the line over them
        foreach (ColumnView view in new[]
        {
            Pinned = new ColumnView { Name = "PinnedPanel", Visible = false }, PinnedCells = new ColumnView { Name = "PinnedCells", Visible = false },
            Strip = new ColumnView { Name = "NearbyStrip", Visible = false }, StripCells = new ColumnView { Name = "NearbyCells", Visible = false },
        })
            AddChild(view);
        Line = new ColorRect { Name = "ColumnLine", Color = ChatLines.Rgba(UiTokens.StorageLine), MouseFilter = MouseFilterEnum.Ignore, Visible = false };
        AddChild(Line);
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
        // slice 4A: the panel's cells as the bar's (section 16: tg's slot picture, the worn item, tg's marks), and the own sprite
        foreach (CellScene cell in _panelCells)
            DrawCell(font, cell, _panelUnit);
        if (_sprite != null)
            DrawTextureRectRegion(_sprite, _spriteAt, _spriteFrom);
    }

    // Slice C: the hand boxes (the active one with its border, a cuffed or missing one red), the swap key on the inactive one, their items, and
    // slice D's hint strip over them.
    private void DrawHands(Font font)
    {
        HandsScene h = Hands;
        float k = h.Layout.Unit;
        if (h.HasLeft)
            DrawHand(h.Layout.Left, h.LeftMarked, h.LeftArt, h.Left, h.LeftOverlays, k, h.LeftRim);
        if (h.HasRight)
            DrawHand(h.Layout.Right, h.RightMarked, h.RightArt, h.Right, h.RightOverlays, k, h.RightRim);
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

    // Package 3: a cell of the bar: section 16's tg slot picture filling it, the item's picture as a hand box draws it (fainter while the item is
    // out in a hand), tg's marks over it (a worn slot's), a red rim while a refused press blinks, its key cap in the top left corner (dim on an
    // empty cell); the whole cell fainter for a belt cell with no belt worn.
    private void DrawCell(Font font, CellScene cell, float k) => DrawCellOn(this, font, cell, k, _frameStyle);

    // the same on any canvas (slice 4B-2: the storage panels' clipped view), with its own frame style
    private static void DrawCellOn(CanvasItem canvas, Font font, CellScene cell, float k, StyleBoxFlat frame)
    {
        HotbarCellLook look = cell.Look;
        var whole = new Color(1f, 1f, 1f, look.Alpha);
        foreach (Picture p in cell.Art)
            canvas.DrawTextureRect(p.Texture, p.Rect, false, ChatLines.Rgba(p.Rgba)); // the cell's opacity is in the tint (LiveHud.SlotTint)
        var tint = new Color(1f, 1f, 1f, look.Alpha * look.PictureAlpha);
        foreach (Picture p in cell.Pictures)
            canvas.DrawTextureRect(p.Texture, p.Rect, false, tint);
        foreach (Picture p in cell.Overlays)
            canvas.DrawTextureRect(p.Texture, p.Rect, false, whole);
        if (look.Rim != 0u)
            FrameOn(canvas, frame, cell.Rect, look.Rim, look.Alpha, Math.Max(1, (int)Math.Round(k)), k);
        if (look.Cap == null)
            return;
        int capSize = FontPx(UiTokens.SwapCapFont, k);
        HudRect cap = LiveHud.CellCap(cell.Rect, k, font.GetStringSize(look.Cap, HorizontalAlignment.Left, -1, capSize).X + 2 * UiTokens.SwapCapInset * k);
        if (cap.Width > 0f)
            DrawCap(canvas, font, cap, look.Cap, capSize, k, look.Alpha * look.CapAlpha);
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

    // Section 16: tg's hand slot picture with its active mark filling the box, the held items, tg's handcuffs or blocked mark over them, and on a
    // cuffed or missing hand a red border over all (LiveHud.HandFrame), active or not.
    private void DrawHand(HudRect box, bool marked, List<Picture> art, List<Picture> pictures, List<Picture> overlays, float k, uint rim)
    {
        foreach (Picture p in art)
            DrawTextureRect(p.Texture, p.Rect, false, ChatLines.Rgba(p.Rgba));
        foreach (Picture p in pictures)
            DrawTextureRect(p.Texture, p.Rect, false);
        foreach (Picture p in overlays)
            DrawTextureRect(p.Texture, p.Rect, false);
        if (LiveHud.HandFrame(marked) is uint frame and not 0u)
            DrawFrame(box, frame, 1f, Math.Max(1, (int)Math.Round(UiTokens.HandBorder * k)), k);
        if (rim != 0u)
            DrawFrame(box, rim, 1f, Math.Max(1, (int)Math.Round(k)), k);
    }

    // A border alone (no fill) round `rect`, with the chips' rounded corners.
    private void DrawFrame(HudRect rect, uint rgba, float alpha, int width, float k) => FrameOn(this, _frameStyle, rect, rgba, alpha, width, k);

    private static void FrameOn(CanvasItem canvas, StyleBoxFlat style, HudRect rect, uint rgba, float alpha, int width, float k)
    {
        style.BorderColor = ChatLines.Rgba(rgba, alpha);
        style.SetBorderWidthAll(width);
        style.SetCornerRadiusAll(Math.Max(1, (int)Math.Round(UiTokens.ChipRadius * k)));
        canvas.DrawStyleBox(style, R(rect));
    }

    private readonly StyleBoxFlat _frameStyle = new() { DrawCenter = false };

    /// <summary>Slice 4B-2: one storage panel this frame: its rectangles, the storage's picture in its header, its title and room, and its cells.</summary>
    public sealed class PanelScene
    {
        public StoragePanelRects Rects;
        public Picture? Header;
        public string Title, Room;
        public bool Locked;
        public readonly List<CellScene> Cells = new();
    }

    /// <summary>
    /// Slice 4B-2: the right column's storage panels, drawn in a view of their own cut at the column's edges (a cell scrolled half out shows its
    /// part inside), each a dark backing, a header and the cells drawn as the bar's, and the scroll bar's thumb when they overflow.
    /// </summary>
    public sealed partial class ColumnView : Control
    {
        private readonly StyleBoxFlat _back = new(), _frame = new() { DrawCenter = false };
        private readonly List<PanelScene> _panels = new();
        private float _unit;

        public ColumnView()
        {
            MouseFilter = MouseFilterEnum.Ignore;
            ClipContents = true;
            TextureFilter = TextureFilterEnum.Nearest;
        }

        /// <summary>
        /// The panels <paramref name="panels"/> cut at <paramref name="clip"/>, with a scroll bar's <paramref name="thumb"/> (empty for none), at
        /// <paramref name="unit"/> screen pixels per reference pixel; none hides the view. Slice 4C-2: a panel with an empty backing is drawn without
        /// one, and one with no title without its header's text.
        /// </summary>
        public void Show(HudRect clip, List<PanelScene> panels, HudRect thumb, float unit)
        {
            bool shown = panels.Count > 0 && clip.Width > 0f && clip.Height > 0f;
            if (!shown && _panels.Count == 0)
            {
                Visible = false;
                return;
            }
            Visible = shown;
            (_thumb, _unit) = (thumb, unit);
            _panels.Clear();
            if (shown)
                _panels.AddRange(panels);
            Position = new Vector2(clip.Left, clip.Top);
            Size = new Vector2(clip.Width, clip.Height);
            QueueRedraw();
        }

        public override void _Draw()
        {
            DrawSetTransform(-Position); // drawn in the screen's pixels, cut at this view's rectangle
            Font font = ThemeDB.FallbackFont;
            float k = _unit;
            int size = FontPx(UiTokens.StorageHeaderFont, k);
            _back.BgColor = ChatLines.Rgba(UiTokens.StoragePanelBack);
            _back.SetCornerRadiusAll(Math.Max(1, (int)Math.Round(UiTokens.DollPanelRadius * k)));
            foreach (PanelScene p in _panels)
            {
                if (p.Rects.Backing.Width > 0f)
                    DrawStyleBox(_back, R(p.Rects.Backing));
                HudRect header = p.Rects.Header;
                if (p.Header is Picture picture)
                    DrawTextureRect(picture.Texture, picture.Rect, false);
                float baseline = header.Top + (header.Height + font.GetAscent(size) - font.GetDescent(size)) / 2f;
                float room = p.Room == null ? 0f : font.GetStringSize(p.Room, HorizontalAlignment.Left, -1, size).X;
                if (p.Room != null)
                    DrawString(font, new Vector2(header.Right - room, baseline), p.Room, HorizontalAlignment.Left, -1, size, ChatLines.Rgba(UiTokens.LocalText));
                float left = p.Rects.Picture.Width > 0f ? p.Rects.Picture.Right + MathF.Round(UiTokens.StoragePanelPad * k) : header.Left;
                float width = header.Right - room - MathF.Round(UiTokens.StoragePanelPad * 2 * k) - left;
                string title = p.Title == null ? null : LiveHud.HintName(p.Title, width, t => font.GetStringSize(t, HorizontalAlignment.Left, -1, size).X);
                if (!string.IsNullOrEmpty(title) && width > 0f)
                    DrawString(font, new Vector2(left, baseline), title, HorizontalAlignment.Left, -1, size, ChatLines.Rgba(UiTokens.LocalText));
                foreach (CellScene cell in p.Cells)
                    DrawCellOn(this, font, cell, k, _frame);
            }
            if (_thumb.Height > 0f)
                DrawRect(R(_thumb), ChatLines.Rgba(UiTokens.StorageScrollThumb));
        }

        private HudRect _thumb;
    }

    /// <summary>Slice 4B-2: the right column's view of the own storages (<see cref="HudLayer"/> fills it each frame); its children are the cells' stack counts.</summary>
    public ColumnView Column { get; private set; }

    /// <summary>Slice 4C-2: the pinned panel's backing and header, and its rows (cut under the header; their children the stack counts).</summary>
    public ColumnView Pinned { get; private set; }

    /// <inheritdoc cref="Pinned"/>
    public ColumnView PinnedCells { get; private set; }

    /// <summary>Slice 4C-2: the Nearby strip's backing and title, and its row of cells (cut at the row; their children the stack counts).</summary>
    public ColumnView Strip { get; private set; }

    /// <inheritdoc cref="Strip"/>
    public ColumnView StripCells { get; private set; }

    /// <summary>Slice 4C-2: the line between the own storages and what is pinned under them.</summary>
    public ColorRect Line { get; private set; }
}
