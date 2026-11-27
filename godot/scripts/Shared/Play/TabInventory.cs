using Anomaly.Client.Shared.Icons;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared;

// ui-overhaul.md section 12, slice 4A: the Tab inventory on the human HUD. Engine-free: no Godot API here (it is compiled into
// tests/Anomaly.Server.Tests too). Under Tab the chat window is docked as the left column, and the middle column holds the character panel: the own
// sprite facing the viewer with tg's worn slots round it by body position. The slots are tg's own screen entries re-placed; the sprite is ours.
// DEVIATION(DV694): the character panel is not tg's (its native inventory panel is gone); the sprite takes clicks as the health doll does.

/// <summary>Where the Tab inventory's parts go this frame, screen pixels (<see cref="TabInventory.Layout"/>).</summary>
/// <param name="Chat">The chat window's docked place (the left column); empty where it has no room.</param>
/// <param name="Sprite">The square the own sprite's 32 x 32 icon square fills; empty where the panel is left out.</param>
/// <param name="SpriteScale">Screen pixels per tg pixel of the sprite: a whole number.</param>
/// <param name="Cells">The worn slots' cells in <see cref="TabInventory.CellOf"/>'s order; null where the panel is left out.</param>
/// <param name="CellUnit">Screen pixels per reference pixel of the cells (the bar's unit, less where they were made smaller to fit).</param>
/// <param name="Column">Slice 4B-2: the right column the storage panels stand in (<see cref="StorageColumn"/>); empty where it has no room.</param>
public readonly record struct TabInventoryLayout(HudRect Chat, HudRect Sprite, int SpriteScale, HudRect[] Cells, float CellUnit, HudRect Column = default)
{
    /// <summary>Whether the character panel is shown.</summary>
    public bool Shown => Cells != null && Sprite.Width > 0f;

    /// <summary>The cell of the worn slot number <paramref name="cell"/> (<see cref="TabInventory.CellOf"/>); empty for none or without the panel.</summary>
    public HudRect CellAt(int cell) => Shown && cell >= 0 && cell < Cells.Length ? Cells[cell] : default;
}

/// <summary>The Tab inventory's layout and rules (ui-overhaul.md section 12, slice 4A).</summary>
public static class TabInventory
{
    /// <summary>The screen entry id the HUD pick reports for the own sprite, which is ours, not a tg entry: below zero, as the bar's belt cells are.</summary>
    public const long SpriteId = -100;

    /// <summary>
    /// tg's worn slots in the panel (__DEFINES/inventory.dm), in the order of <see cref="TabInventoryLayout.Cells"/>: above the sprite a row of the
    /// ears, the head and the eyes, under it the mask, under that the neck; left of the sprite, top to bottom, the suit, the uniform and the gloves;
    /// right of it the back, suit storage and the belt; under it the shoes. The pockets and the ID slot are the bar's cells 8, 9 and 0.
    /// </summary>
    private static readonly uint[] SlotOrder =
    {
        1u << 4, 1u << 6, 1u << 3, // ITEM_SLOT_EARS, ITEM_SLOT_HEAD, ITEM_SLOT_EYES
        1u << 5, 1u << 12, // ITEM_SLOT_MASK, ITEM_SLOT_NECK
        1u << 0, 1u << 1, 1u << 2, // ITEM_SLOT_OCLOTHING, ITEM_SLOT_ICLOTHING, ITEM_SLOT_GLOVES
        1u << 10, 1u << 14, 1u << 9, // ITEM_SLOT_BACK, ITEM_SLOT_SUITSTORE, ITEM_SLOT_BELT
        1u << 7, // ITEM_SLOT_FEET
    };

    /// <summary>How many worn slots the panel has.</summary>
    public const int CellCount = 12;

    /// <summary>The panel's cell (0 to 11) of tg's slot <paramref name="slotId"/>; -1 for a slot the panel does not show (the pockets, the ID slot, a hand).</summary>
    public static int CellOf(uint slotId) => Array.IndexOf(SlotOrder, slotId);

    // tg's action buttons at the top left: the first row of listed buttons and, under it whenever the mob has any action, the palette button
    // (action_group.dm palette/refresh_actions: ui_action_palette_offset(0)); its place is the least the Tab inventory keeps clear of
    private static readonly ScreenLocSpec ActionPalette = ScreenLoc.TryParse("WEST+0:23,NORTH-1:5", out ScreenLocSpec spec) ? spec : default;

    /// <summary>
    /// The Tab inventory's places on the human HUD under Tab, depending on the screen and the scale, the bottom row (<paramref name="hands"/>), the
    /// vitals (<paramref name="vitals"/>, their pictures and every place a chip can take) and tg's entries still drawn (<paramref name="tgDrawn"/>:
    /// the action buttons at the top left, the combat toggle and the Tab row at the bottom right, tg's status column with the reduced vitals):
    /// <list type="bullet">
    /// <item><description>the chat column: from the screen's left edge to <see cref="UiTokens.OverlayLeftShare"/> of its width, from
    /// <see cref="UiTokens.ChatRowGap"/> under tg's action buttons (the first row and the palette button under it, whether shown or not, or the lowest
    /// of tg's entries, the vitals' pictures or the chips' places over the column where there are more) down to that gap over the bottom row's top
    /// line (the active hand box's) or over the highest of them standing in the column's lower half; empty (the chat stays undocked) where nothing
    /// is left between;</description></item>
    /// <item><description>the character panel, see-through, centred in the middle column (<see cref="UiTokens.OverlayLeftShare"/> to
    /// <see cref="UiTokens.OverlayRightShare"/> of the width) between the same top and bottom, and clear of everything above: the sprite at
    /// <see cref="UiTokens.TabSpritePixels"/> screen pixels per tg pixel at the reference size, a whole number, the slots round it as cells of the
    /// bar's size, <see cref="UiTokens.TabCellGap"/> apart and <see cref="UiTokens.TabSpriteGap"/> from the sprite. Where it does not fit the sprite
    /// steps down a whole number at a time, then the cells (and their gaps) are made smaller, down to <see cref="UiTokens.TabCellMinPixels"/>; below
    /// that the panel is left out.</description></item>
    /// </list>
    /// Everything at whole pixels.
    /// </summary>
    public static TabInventoryLayout Layout(in HudMetrics metrics, in HandsLayout hands, in LiveHudLayout vitals, IReadOnlyList<HudRect> tgDrawn)
    {
        float u = UiTokens.Unit(metrics), gap = MathF.Round(UiTokens.ChatRowGap * u);
        float split = MathF.Round(UiTokens.OverlayLeftShare * metrics.Width), end = MathF.Round(UiTokens.OverlayRightShare * metrics.Width);
        float firstRow = MathF.Ceiling(ScreenLoc.Place(ActionPalette, metrics, 32, 32, 0, 0, out _).Bottom);
        // the bottom row's top line: the active hand's box, whichever hand is active, so nothing moves with a swap
        float rowTop = MathF.Floor(hands.Hotbar.Bottom - MathF.Round(UiTokens.HandActive * hands.Unit));
        float half = metrics.Height / 2f;

        // whatever else stands on the screen: tg's entries still drawn, the vitals and every place a chip can take (a portrait screen's chips left of
        // the vitals reach into the left columns)
        var obstacles = new List<HudRect>(tgDrawn);
        obstacles.Add(vitals.Doll);
        if (vitals.Vitals)
        {
            obstacles.Add(vitals.Health);
            obstacles.Add(vitals.Mood);
        }
        for (int n = 0; n <= LiveHud.MaxChips; n++)
            obstacles.AddRange(LiveHud.AlertStack(metrics, n, vitals));

        // the chat column, under what stands over it in the top half and over what stands in it in the bottom half; left undocked (empty) where
        // nothing is left between
        float chatTop = firstRow, chatBottom = rowTop;
        foreach (HudRect r in obstacles)
        {
            if (r.Width <= 0f || r.Height <= 0f || r.Right <= 0f || r.Left >= split)
                continue;
            if (r.Top < half)
                chatTop = Math.Max(chatTop, MathF.Ceiling(r.Bottom));
            else
                chatBottom = Math.Min(chatBottom, MathF.Floor(r.Top));
        }
        chatTop += gap;
        chatBottom -= gap;
        HudRect chat = chatBottom > chatTop ? new HudRect(0f, chatTop, split, chatBottom - chatTop) : default;

        // slice 4B-2: the right column, from the middle column's end (and a gap) to the vitals' left edge, from the chat column's top line to the gap
        // over the bottom row, cut clear of the same
        float columnRight = MathF.Floor(Math.Min(metrics.Width, LiveHud.LeftOfVitals(vitals, metrics)));
        HudRect column = Clear(Span(end + gap, chat.Height > 0f ? chat.Top : firstRow + gap, columnRight, rowTop - gap), obstacles, gap);

        // the middle column, cut clear of the same
        var box = new HudRect(split, firstRow + gap, end - split, rowTop - gap - (firstRow + gap));
        box = Clear(box, obstacles, gap);
        if (box.Width <= 0f || box.Height <= 0f)
            return new TabInventoryLayout(chat, default, 0, null, 0f, column);

        float full = LiveHud.BarCell(hands, 1).Width;
        int first = Math.Max(1, (int)Math.Round(UiTokens.TabSpritePixels * u, MidpointRounding.AwayFromZero));
        for (int s = first; s >= 1; s--)
        {
            if (TryPanel(box, s, full, out HudRect sprite, out HudRect[] cells))
                return new TabInventoryLayout(chat, sprite, s, cells, full / UiTokens.HotbarSlot, column);
        }
        for (float side = full - 1f; side >= UiTokens.TabCellMinPixels; side--)
        {
            if (TryPanel(box, 1, side, out HudRect sprite, out HudRect[] cells))
                return new TabInventoryLayout(chat, sprite, 1, cells, side / UiTokens.HotbarSlot, column);
        }
        return new TabInventoryLayout(chat, default, 0, null, 0f, column);
    }

    // The largest part of `box` clear of every obstacle (each kept `gap` away): for each obstacle in it, the cut (above, below, left or right of
    // it) that keeps the most room. Empty where nothing is left.
    private static HudRect Clear(HudRect box, List<HudRect> obstacles, float gap)
    {
        for (int pass = 0; pass <= obstacles.Count; pass++)
        {
            bool cut = false;
            foreach (HudRect o in obstacles)
            {
                if (box.Width <= 0f || box.Height <= 0f)
                    return default;
                if (!Over(box, o))
                    continue;
                HudRect best = default;
                foreach (HudRect option in new[]
                {
                    Span(MathF.Ceiling(o.Right + gap), box.Top, box.Right, box.Bottom), Span(box.Left, box.Top, MathF.Floor(o.Left - gap), box.Bottom),
                    Span(box.Left, MathF.Ceiling(o.Bottom + gap), box.Right, box.Bottom), Span(box.Left, box.Top, box.Right, MathF.Floor(o.Top - gap)),
                })
                {
                    if (option.Width * option.Height > best.Width * best.Height)
                        best = option;
                }
                box = best;
                cut = true;
            }
            if (!cut)
                return box;
        }
        return box;
    }

    private static HudRect Span(float left, float top, float right, float bottom)
        => right > left && bottom > top ? new HudRect(left, top, right - left, bottom - top) : default;

    // The panel with the sprite at `scale` and cells of `side`, centred in `box`: false where it is larger than the box or its parts would overlap.
    private static bool TryPanel(HudRect box, int scale, float side, out HudRect sprite, out HudRect[] cells)
    {
        float k = side / UiTokens.HotbarSlot, p = HudMetrics.IconPixels * scale, c = side;
        float g = Math.Max(1f, MathF.Round(UiTokens.TabCellGap * k)), big = Math.Max(1f, MathF.Round(UiTokens.TabSpriteGap * k));
        // relative to the sprite's top left: the row of ears, head and eyes, the mask and the neck in a column above it, the shoes under it
        float headLeft = MathF.Floor((p - c) / 2f);
        float neckTop = -big - c, maskTop = neckTop - g - c, rowTop = maskTop - g - c;
        float columnTop = MathF.Floor((p - (3 * c + 2 * g)) / 2f);
        var at = new HudRect[CellCount];
        at[0] = new HudRect(headLeft - g - c, rowTop, c, c);
        at[1] = new HudRect(headLeft, rowTop, c, c);
        at[2] = new HudRect(headLeft + c + g, rowTop, c, c);
        at[3] = new HudRect(headLeft, maskTop, c, c);
        at[4] = new HudRect(headLeft, neckTop, c, c);
        for (int i = 0; i < 3; i++)
        {
            at[5 + i] = new HudRect(-big - c, columnTop + i * (c + g), c, c);
            at[8 + i] = new HudRect(p + big, columnTop + i * (c + g), c, c);
        }
        at[11] = new HudRect(headLeft, p + big, c, c);
        var own = new HudRect(0f, 0f, p, p);
        float left = own.Left, top = own.Top, right = own.Right, bottom = own.Bottom;
        for (int i = 0; i < CellCount; i++)
        {
            (left, top, right, bottom) = (Math.Min(left, at[i].Left), Math.Min(top, at[i].Top), Math.Max(right, at[i].Right), Math.Max(bottom, at[i].Bottom));
            if (Over(at[i], own))
            {
                (sprite, cells) = (default, null);
                return false;
            }
            for (int j = 0; j < i; j++)
            {
                if (Over(at[i], at[j]))
                {
                    (sprite, cells) = (default, null);
                    return false;
                }
            }
        }
        float width = right - left, height = bottom - top;
        if (width > box.Width || height > box.Height)
        {
            (sprite, cells) = (default, null);
            return false;
        }
        // the sprite's top left on the screen, so the whole panel is centred in the box at whole pixels
        float x = MathF.Floor(box.Left + (box.Width - width) / 2f) - left, y = MathF.Floor(box.Top + (box.Height - height) / 2f) - top;
        sprite = new HudRect(x, y, p, p);
        cells = new HudRect[CellCount];
        for (int i = 0; i < CellCount; i++)
            cells[i] = new HudRect(x + at[i].Left, y + at[i].Top, c, c);
        return true;
    }

    private static bool Over(HudRect a, HudRect b)
        => a.Width > 0 && a.Height > 0 && b.Width > 0 && b.Height > 0 && a.Left < b.Right && b.Left < a.Right && a.Top < b.Bottom && b.Top < a.Bottom;

    /// <summary>
    /// Where the chat window is docked: the layout's chat column under Tab (<paramref name="cursorMode"/>) on the human HUD
    /// (<paramref name="humanHud"/>) where the column has room; null otherwise (the window stands at its own place).
    /// </summary>
    public static HudRect? ChatDock(bool cursorMode, bool humanHud, in TabInventoryLayout layout)
        => cursorMode && humanHud && layout.Chat.Width > 0f && layout.Chat.Height > 0f ? layout.Chat : null;

    /// <summary>
    /// What a click on the own sprite (<paramref name="clicked"/>, the HUD pick's <see cref="SpriteId"/>) is, as a click on the health doll is
    /// (D183, DV72; <paramref name="zone"/> the part under the point, <see cref="BodyZoneAim"/>'s nine zones): Ctrl+click is a ctrl-click on the own
    /// mob aimed at the limb clicked (tg grasps it); with an item in the active hand a plain click is a click on the own mob aimed at the part
    /// clicked (the item used on oneself there); any other click is a click on tg's health doll <paramref name="dollEntry"/> (an empty hand's is tg's
    /// self-check), or nothing without one. Any other target is kept.
    /// </summary>
    public static ClickTarget SelfClick(in ClickTarget clicked, ClickModifiers modifiers, bool holdsItem, long ownEntity, long dollEntry, AimZone zone)
    {
        if (clicked.Kind != TargetKind.Screen || clicked.Id != SpriteId)
            return clicked;
        if (ownEntity != 0 && (modifiers & ClickModifiers.Ctrl) != 0)
            return new ClickTarget(TargetKind.Entity, ownEntity, 0, 0, 0, 0, 0, BodyZoneMap.LimbOf(zone));
        if (ownEntity != 0 && holdsItem && modifiers == ClickModifiers.None && zone != AimZone.None)
            return new ClickTarget(TargetKind.Entity, ownEntity, 0, 0, 0, 0, 0, zone);
        return dollEntry != 0 ? new ClickTarget(TargetKind.Screen, dollEntry, 0, 0, 0, 16, 16) : ClickTarget.None;
    }

    /// <summary>
    /// What the HUD pick gets for the own sprite: only its 32 x 32 icon square of the composed <paramref name="picture"/> (its icon square's lower left
    /// corner at <paramref name="originX"/>, <paramref name="originY"/>), as a raster of that square alone, since the panel draws the sprite cut to
    /// that square. Reported over the sprite's square with origin (0, 0), a point outside the square is no click on the sprite, and inside it the icon
    /// pixel is that of the pixel drawn there (a picture larger than the square, a big hat or a long held item, is never clamped to its edge). Pixels
    /// of the square the picture does not cover are transparent. Null without a picture.
    /// </summary>
    public static Raster SpritePick(Raster picture, int originX, int originY)
    {
        if (picture == null)
            return null;
        const int side = AppearanceCompositor.DefaultIconSize;
        var square = new Raster(side, side);
        for (int iy = 1; iy <= side; iy++)
        {
            int fromBottom = originY + iy - 1;
            if (fromBottom < 0 || fromBottom >= picture.Height)
                continue;
            int from = (picture.Height - 1 - fromBottom) * picture.Width, to = (side - iy) * side;
            for (int ix = 1; ix <= side; ix++)
            {
                int col = originX + ix - 1;
                if (col >= 0 && col < picture.Width)
                    Array.Copy(picture.Pixels, (from + col) * 4, square.Pixels, (to + ix - 1) * 4, 4);
            }
        }
        return square;
    }

    /// <summary>
    /// The action a key press runs (slice 4A): the inventory key's is cursor mode's, so it goes through Tab's own handler in every state (the dev
    /// menu's focus, the panels, the windows); every other action is itself.
    /// </summary>
    public static PlayAction? KeyAction(PlayAction? action) => action == PlayAction.Inventory ? PlayAction.CursorMode : action;

    /// <summary>The words the pointer's prompt names a part of the sprite by; null for none.</summary>
    public static string PartName(AimZone zone) => zone switch
    {
        AimZone.Head => "Head",
        AimZone.Eyes => "Eyes",
        AimZone.Mouth => "Mouth",
        AimZone.Chest => "Chest",
        AimZone.Groin => "Groin",
        AimZone.LeftArm => "Left arm",
        AimZone.RightArm => "Right arm",
        AimZone.LeftLeg => "Left leg",
        AimZone.RightLeg => "Right leg",
        _ => null,
    };

    /// <summary>
    /// Whether the player taking damage (<paramref name="damaged"/>: the health doll shows more damage than before) turns cursor mode off: only while it
    /// is on, and not while the player types in a text field (<paramref name="typing"/>) or a window that needs the pointer is open
    /// (<paramref name="pointerWindow"/>).
    /// </summary>
    public static bool CloseOnDamage(bool damaged, bool cursorMode, bool typing, bool pointerWindow) => damaged && cursorMode && !typing && !pointerWindow;
}

/// <summary>
/// Keeps tg's inventory expanded on the human HUD (slice 4A), so tg's worn slots exist as screen entries under Tab: the client clicks tg's inventory
/// toggle once when it shows collapsed (tg's <c>toggle</c> state), and again only after it has shown expanded since (<c>toggle_active</c>: the server
/// collapsed it again, a new HUD).
/// </summary>
public sealed class InventoryExpander
{
    private long _clicked;

    /// <summary>
    /// The toggle entry to click this frame (0: none), given the toggle entry showing collapsed (<paramref name="collapsed"/>) or expanded
    /// (<paramref name="expanded"/>) now (0 where there is none).
    /// </summary>
    public long Next(long collapsed, long expanded)
    {
        if (expanded != 0)
        {
            _clicked = 0;
            return 0;
        }
        if (collapsed == 0 || collapsed == _clicked)
            return 0;
        _clicked = collapsed;
        return collapsed;
    }
}
