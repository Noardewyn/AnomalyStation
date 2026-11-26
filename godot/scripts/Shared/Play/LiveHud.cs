using Anomaly.Core.DmShim;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared;

// Owner: ui-overhaul.md section 6 (package 2, slice B). Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).
// The server still computes tg's screen entries and a click still goes to them (D107 as revised by ui-overhaul.md section 1); the client
// re-places the health doll into a panel at the middle right and draws tg's combat toggle as a native pill under it. The doll only shows
// injuries: targeting is where the player looks (the owner, 2026-11-24), so tg's zone selector shows only while the cursor is free.

/// <summary>What a screen entry is for the live HUD.</summary>
public enum LiveHudRole : byte
{
    /// <summary>Drawn at tg's <c>screen_loc</c> as before.</summary>
    Tg,

    /// <summary>tg's health doll (its appearance holds the doll's zone states, <see cref="HudReader"/>): re-placed into the doll panel.</summary>
    Doll,

    /// <summary>
    /// tg's target zone selector (icon state <c>zone_sel</c>): at tg's place in cursor mode only (<see cref="LiveHud.Shown"/>); in the captured view
    /// targeting is where the player looks.
    /// </summary>
    ZoneSelector,

    /// <summary>tg's combat toggle (icon state <c>combat</c> or <c>combat_off</c>): drawn as the native combat pill.</summary>
    CombatToggle,

    /// <summary>tg's first hand box (<see cref="ScreenEntry.HandIndex"/> 1, a left hand): drawn as the native left hand box (slice C).</summary>
    LeftHand,

    /// <summary>tg's second hand box (<see cref="ScreenEntry.HandIndex"/> 2, a right hand): drawn as the native right hand box.</summary>
    RightHand,

    /// <summary>One of tg's bottom slots (an inventory box with a <see cref="ScreenEntry.SlotId"/> outside the toggled inventory: belt, back, pockets, ID, suit storage): moved into the hotbar's gap.</summary>
    BottomSlot,

    /// <summary>tg's inventory toggle (icon state <c>toggle</c> or <c>toggle_active</c>), which only the human HUD has: drawn at tg's place; its presence marks the human HUD.</summary>
    InventoryToggle,
}

/// <summary>Where the live HUD's boxes go, screen pixels (<see cref="LiveHud.Layout"/>).</summary>
/// <param name="Panel">The doll panel's background.</param>
/// <param name="Doll">The square the doll's 32 x 32 icon fills (<see cref="ScreenLoc.FitIcon"/>).</param>
/// <param name="Pill">The combat pill under the panel.</param>
/// <param name="DollScale">Screen pixels per tg pixel of the doll: a whole number.</param>
/// <param name="Card">The held-item card's room under the pill (empty when none is kept).</param>
/// <param name="CardUnit">Screen pixels per reference pixel inside the card (<see cref="UiTokens.Unit"/>, less when the card is made smaller to fit).</param>
public readonly record struct LiveHudLayout(HudRect Panel, HudRect Doll, HudRect Pill, int DollScale, HudRect Card = default, float CardUnit = 0f)
{
    /// <summary>The left edge of the panel and the pill together: whatever stands left of them (the zone labels, the prompts) ends a gap before it.</summary>
    public float Left => Math.Min(Panel.Left, Pill.Left);
}

/// <summary>Where the hands and the hotbar's gap go, screen pixels (<see cref="LiveHud.Hands"/>).</summary>
/// <param name="Left">The left hand's box, left of the gap.</param>
/// <param name="Right">The right hand's box, right of it.</param>
/// <param name="Hotbar">The gap kept for the nine-slot hotbar; tg's bottom slots stand in it until package 3 (<see cref="LiveHud.SlotCell"/>).</param>
/// <param name="SwapChip">The room for the swap-hand chip and its text by the right hand, clear of tg's drop and swap buttons (it does not move with a swap); empty when no clear place exists at this size.</param>
/// <param name="Unit">Screen pixels per reference pixel in the row (<see cref="UiTokens.Unit"/>, less when the row is made smaller to fit).</param>
public readonly record struct HandsLayout(HudRect Left, HudRect Right, HudRect Hotbar, HudRect SwapChip, float Unit);

/// <summary>The live HUD's reading of tg's screen entries and its layout (ui-overhaul.md section 6, wireframes Mockup 1).</summary>
public static class LiveHud
{
    /// <summary>
    /// What <paramref name="entry"/> is, by its hand index, slot id, icon state and group, never by its place: an entry of a native window (a
    /// storage, the inventory) or an action button is always <see cref="LiveHudRole.Tg"/>. The hands and the bottom slots are re-placed only on
    /// the human HUD (<see cref="Placed"/>).
    /// </summary>
    public static LiveHudRole RoleOf(ReplicaWorld replica, ScreenEntry entry)
    {
        if (entry.Group is HudGroup.Storage or HudGroup.ToggleableInventory || entry.ActionTarget > 0)
            return LiveHudRole.Tg;
        if (entry.HandIndex is 1 or 2) // hands beyond the second keep tg's place
            return entry.HandIndex == 1 ? LiveHudRole.LeftHand : LiveHudRole.RightHand;
        if (entry.SlotId != 0 && entry.HandIndex == 0)
            return LiveHudRole.BottomSlot;
        if (!replica.TryGetAppearance(entry.Appearance, out AppearanceEntry app))
            return LiveHudRole.Tg;
        string state = replica.ResolveString(app.IconState);
        if (state == "zone_sel")
            return LiveHudRole.ZoneSelector;
        if (state is "combat" or "combat_off")
            return LiveHudRole.CombatToggle;
        if (state is "toggle" or "toggle_active")
            return LiveHudRole.InventoryToggle;
        var zones = new List<FoundZone>();
        HudReader.Collect(replica, entry.Appearance, zones);
        return zones.Count > 0 ? LiveHudRole.Doll : LiveHudRole.Tg;
    }

    /// <summary>
    /// Whether an entry of <paramref name="role"/> is drawn and takes clicks now: tg's zone selector only while the cursor is free (cursor mode, Tab),
    /// where any zone, the eyes, mouth and groin included (tg's surgeries, a ctrl-click on the doll), is still chosen on it (looking aims at them too since
    /// package T, slice T4a, and the selector shows the zone the server selected); every other
    /// entry always.
    /// </summary>
    public static bool Shown(LiveHudRole role, bool cursorMode) => role != LiveHudRole.ZoneSelector || cursorMode;

    /// <summary>
    /// The role an entry is drawn in: the hands and the bottom slots only on the human HUD (<paramref name="humanHud"/>: its inventory toggle is
    /// on the screen); the alien's, a basic mob's and the other HUDs keep tg's places (ui-overhaul.md section 6).
    /// </summary>
    public static LiveHudRole Placed(LiveHudRole role, bool humanHud)
        => !humanHud && role is LiveHudRole.LeftHand or LiveHudRole.RightHand or LiveHudRole.BottomSlot ? LiveHudRole.Tg : role;

    /// <summary>Whether the hand box <paramref name="hand"/> is the active hand: tg's <c>lhandactive</c> or <c>rhandactive</c> overlay on it.</summary>
    public static bool IsActiveHand(ReplicaWorld replica, ScreenEntry hand)
    {
        if (!replica.TryGetAppearance(hand.Appearance, out AppearanceEntry app) || app.OverlayCount == 0)
            return false;
        foreach (TableRef overlay in app.Overlays)
        {
            if (replica.TryGetAppearance(overlay, out AppearanceEntry o) && (replica.ResolveString(o.IconState) ?? string.Empty).EndsWith("handactive", StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    /// <summary>
    /// What a native hand box draws of tg's hand box <paramref name="hand"/> over the held items it draws on their own (its vis_contents children):
    /// the overlays the server put on the box's appearance but the active hand's mark and those items, i.e. tg's <c>handcuff_overlay</c> and
    /// <c>blocked_overlay</c> (screen_objects.dm, inventory/hand update_overlays) and a held item the box does not list as a child. The box's own
    /// picture (tg's empty hand slot) is the native box. Bottom to top, as the server sends them.
    /// </summary>
    public static List<TableRef> HandOverlays(ReplicaWorld replica, ScreenEntry hand)
    {
        var shown = new List<TableRef>();
        if (!replica.TryGetAppearance(hand.Appearance, out AppearanceEntry app) || app.OverlayCount == 0)
            return shown;
        ScreenChild[] items = hand.VisChildren?.Items;
        foreach (TableRef overlay in app.Overlays)
        {
            if (items != null && Array.Exists(items, c => c.Appearance.Equals(overlay)))
                continue;
            if (replica.TryGetAppearance(overlay, out AppearanceEntry o) && (replica.ResolveString(o.IconState) ?? string.Empty).EndsWith("handactive", StringComparison.Ordinal))
                continue;
            shown.Add(overlay);
        }
        return shown;
    }

    /// <summary>The swap-hand chip by the hands: the key bound to swapping hands and its words; null while it is unbound.</summary>
    public static PromptLine? SwapLine(KeyBindings bindings)
        => KeyChips.KeyOf(bindings, PlayAction.SwapHand) is string key ? new PromptLine(key, I18n.T("Swap hand")) : null;

    // what the player can do with a held item using keys that exist today (ui-overhaul.md section 6); swapping hands is the chip by the hands
    private static readonly (PlayAction Action, string Text)[] HeldActions =
    {
        (PlayAction.ActivateInHand, "Use"), (PlayAction.DropItem, "Drop"), (PlayAction.ThrowMode, "Throw"),
    };

    /// <summary>The held-item card's key lines: use, drop and throw, each with the first key bound to it; an unbound one is left out.</summary>
    public static List<PromptLine> CardLines(KeyBindings bindings)
    {
        var lines = new List<PromptLine>(HeldActions.Length);
        foreach ((PlayAction action, string text) in HeldActions)
        {
            if (KeyChips.KeyOf(bindings, action) is string key)
                lines.Add(new PromptLine(key, I18n.T(text)));
        }
        return lines;
    }

    /// <summary>The card's words for the hand that holds the item.</summary>
    public static string HandName(bool left) => left ? I18n.T("Left hand") : I18n.T("Right hand");

    // tg's inventory toggle at the bottom left (ui_inventory) and the left column of its bottom-right menu (ui_human_floor_changer,
    // ui_human_memories_menu): the hands' row stands between them
    private static readonly ScreenLocSpec ToggleLoc = ScreenLoc.TryParse("WEST:6,SOUTH:5", out ScreenLocSpec toggle) ? toggle : default;
    private static readonly ScreenLocSpec MenuLoc = ScreenLoc.TryParse("EAST-4:22,SOUTH:5", out ScreenLocSpec menu) ? menu : default;

    /// <summary>
    /// The hands' row at the bottom centre: the hotbar's gap, the left hand's box left of it and the right hand's right of it, each
    /// <see cref="UiTokens.HandActive"/> when it is the active hand (tg's overlay on it: <paramref name="leftActive"/>, <paramref name="rightActive"/>;
    /// neither when a third or fourth hand is active) and <see cref="UiTokens.HandInactive"/> otherwise, all on one bottom line, and the swap-hand
    /// chip's room over the right hand. The row keeps a gap from tg's inventory toggle and from tg's bottom-right menu, and stays under the chat
    /// lines' lowest line; it is moved off the middle as little as that takes, and made smaller when it still does not fit. With the chat window
    /// open at the bottom left (its right edge <paramref name="chatRight"/>, screen pixels; 0: none) the row moves right of it only while it then
    /// fits there: wholly right of the window and on the screen, its smaller hand box at <see cref="UiTokens.HandMinPixels"/> or more, and clear of
    /// tg's drop and swap buttons; otherwise it is exactly where it is with the chat window closed, and a window over it covers it as a window
    /// covers tg's HUD (D282). The gap and the boxes keep their places when the active hand changes.
    /// </summary>
    public static HandsLayout Hands(in HudMetrics metrics, bool leftActive, bool rightActive, float chatRight = 0f)
    {
        float toggle = ScreenLoc.Place(ToggleLoc, metrics, 32, 32, 0, 0, out _).Right;
        if (chatRight > toggle)
        {
            // judged with both boxes at the active size, so a swap never moves the row between the two places
            HandsLayout beside = Row(metrics, true, true, chatRight);
            if (beside.Left.Left >= chatRight && UiTokens.HandInactive * beside.Unit >= UiTokens.HandMinPixels && !OverDropOrSwap(beside, metrics))
                return Row(metrics, leftActive, rightActive, chatRight);
        }
        return Row(metrics, leftActive, rightActive, toggle);
    }

    /// <summary>
    /// The icon square the maptexts of an entry drawn in <paramref name="placed"/> stand on, which they scale with: a hand box's picture square, a
    /// moved bottom slot's cell (number <paramref name="cell"/> of <paramref name="cells"/>); null for an entry at its own place (its screen_loc's
    /// square at the HUD's pixel scale).
    /// </summary>
    public static HudRect? MaptextSquare(LiveHudRole placed, in HandsLayout hands, int cell, int cells) => placed switch
    {
        LiveHudRole.LeftHand => HandPicture(hands.Left, hands.Unit),
        LiveHudRole.RightHand => HandPicture(hands.Right, hands.Unit),
        LiveHudRole.BottomSlot => SlotCell(hands, cell, cells),
        _ => null,
    };

    // tg's drop and swap buttons over the gap's middle (ui_swaphand_position), which stay at tg's places: the swap chip keeps clear of them, and a
    // row moved right of the chat window must not reach under them
    private static readonly ScreenLocSpec DropLoc = ScreenLoc.TryParse("CENTER-1:16,SOUTH+1:5", out ScreenLocSpec drop) ? drop : default;
    private static readonly ScreenLocSpec SwapLoc = ScreenLoc.TryParse("CENTER:16,SOUTH+1:5", out ScreenLocSpec swap) ? swap : default;

    private static bool OverDropOrSwap(in HandsLayout row, in HudMetrics metrics)
    {
        HudRect drop = ScreenLoc.Place(DropLoc, metrics, 32, 32, 0, 0, out _), swap = ScreenLoc.Place(SwapLoc, metrics, 32, 32, 0, 0, out _);
        foreach (HudRect part in new[] { row.Left, row.Right, row.SwapChip, row.Hotbar })
        {
            if (Over(part, drop) || Over(part, swap))
                return true;
        }
        return false;
    }

    // The row between `leftEdge` (and a gap) and tg's bottom-right menu.
    private static HandsLayout Row(in HudMetrics metrics, bool leftActive, bool rightActive, float leftEdge)
    {
        float u = UiTokens.Unit(metrics), gap = UiTokens.DollPanelGap * u;
        float left = leftEdge + gap;
        float right = ScreenLoc.Place(MenuLoc, metrics, 32, 32, 0, 0, out _).Left - gap;
        const float half = UiTokens.HotbarWidth / 2f + UiTokens.HandGap + UiTokens.HandActive;
        const float height = UiTokens.HotbarMargin + UiTokens.HandActive + UiTokens.SwapChipGap + UiTokens.ChipHeight;
        // ChatLines: the newest line's block ends 20 of its units below 0.8 of the height (its default scale is the HUD's unit)
        float room = metrics.Height * 0.2f - 20f * u - gap;
        float s = Math.Min(Math.Clamp(Math.Min((right - left) / (2 * half * u), room / (height * u)), 0.1f, 1f), metrics.Width / (2 * half * u));
        float k = u * s;
        float centre = Math.Max(left + half * k, Math.Min(metrics.Width / 2f, right - half * k));
        centre = Math.Clamp(centre, half * k, Math.Max(half * k, metrics.Width - half * k)); // never off the screen
        float bottom = MathF.Round(metrics.Height - UiTokens.HotbarMargin * k);
        float slot = MathF.Round(UiTokens.HotbarSlot * k), width = MathF.Round(UiTokens.HotbarWidth * k);
        var hotbar = new HudRect(MathF.Round(centre - width / 2f), bottom - slot, width, slot);
        float active = MathF.Round(UiTokens.HandActive * k), other = MathF.Round(UiTokens.HandInactive * k), handGap = MathF.Round(UiTokens.HandGap * k);
        float leftSide = leftActive ? active : other, rightSide = rightActive ? active : other;
        var leftBox = new HudRect(hotbar.Left - handGap - leftSide, bottom - leftSide, leftSide, leftSide);
        var rightBox = new HudRect(hotbar.Right + handGap, bottom - rightSide, rightSide, rightSide);
        // the swap chip by the right hand (the wireframes' "[X] Swap Hand" is in that box), over the larger box's height so it stays put through a
        // swap: over the right box from the row's right edge; else from the box's left edge along to the right, up to tg's bottom-right menu;
        // else the same raised over tg's drop and swap buttons (which stay at tg's places over the gap); else, with no clear place, left out
        float chipWidth = MathF.Round(UiTokens.SwapChipWidth * k), chipHeight = MathF.Round(UiTokens.ChipHeight * k), chipGap = MathF.Round(UiTokens.SwapChipGap * k);
        HudRect drop = ScreenLoc.Place(DropLoc, metrics, 32, 32, 0, 0, out _), swap = ScreenLoc.Place(SwapLoc, metrics, 32, 32, 0, 0, out _);
        float low = bottom - active - chipGap - chipHeight, high = Math.Min(drop.Top, swap.Top) - chipGap - chipHeight;
        float byRow = hotbar.Right + handGap + active - chipWidth, byBox = hotbar.Right + handGap;
        HudRect chip = default;
        foreach (HudRect place in new[]
        {
            new HudRect(byRow, low, chipWidth, chipHeight), new HudRect(byBox, low, chipWidth, chipHeight),
            new HudRect(byRow, Math.Min(low, high), chipWidth, chipHeight), new HudRect(byBox, Math.Min(low, high), chipWidth, chipHeight),
        })
        {
            // within the row (its right edge may pass the gap before tg's menu by its rounding, never the menu), or up to that gap
            if (place.Left >= 0f && place.Top >= 0f && place.Right <= Math.Min(Math.Max(right, Math.Min(byRow + chipWidth, right + gap)), metrics.Width) && !Over(place, drop) && !Over(place, swap))
            {
                chip = place;
                break;
            }
        }
        return new HandsLayout(leftBox, rightBox, hotbar, chip, k);
    }

    // Two rectangles share some area (an empty one shares none).
    private static bool Over(HudRect a, HudRect b)
        => a.Width > 0 && a.Height > 0 && b.Width > 0 && b.Height > 0 && a.Left < b.Right && b.Left < a.Right && a.Top < b.Bottom && b.Top < a.Bottom;

    /// <summary>
    /// Where tg's bottom slot number <paramref name="index"/> (from 0) of <paramref name="count"/> goes in the hotbar's gap: a hotbar slot's square,
    /// the slots side by side with the hotbar's gaps between them, centred in the gap; narrower when more than nine would not fit.
    /// </summary>
    public static HudRect SlotCell(in HandsLayout hands, int index, int count)
    {
        float k = hands.Unit, gap = UiTokens.HotbarSlotGap * k;
        float cell = Math.Min(hands.Hotbar.Height, (hands.Hotbar.Width - (count - 1) * gap) / Math.Max(1, count));
        float block = count * cell + (count - 1) * gap;
        float left = hands.Hotbar.Left + (hands.Hotbar.Width - block) / 2f + index * (cell + gap);
        // both edges rounded, so neighbours never overlap; no taller than the gap (its height was rounded on its own)
        float l = MathF.Round(left), side = Math.Min(MathF.Round(left + cell) - l, hands.Hotbar.Height);
        return new HudRect(l, hands.Hotbar.Bottom - side, side, side);
    }

    /// <summary>The held-item card's picture square at its top left (the held item's 32 x 32 icon square fills it; the hand's name beside it).</summary>
    public static HudRect CardPicture(HudRect card, float unit)
    {
        float pad = MathF.Round(UiTokens.CardPad * unit), side = MathF.Round(UiTokens.CardPicture * unit);
        return new HudRect(card.Left + pad, card.Top + pad, side, side);
    }

    /// <summary>The square inside a hand box that the held item's 32 x 32 icon square fills.</summary>
    public static HudRect HandPicture(HudRect box, float unit)
    {
        float inset = MathF.Round(UiTokens.HandInset * unit);
        return new HudRect(box.Left + inset, box.Top + inset, Math.Max(1f, box.Width - 2 * inset), Math.Max(1f, box.Height - 2 * inset));
    }

    /// <summary>
    /// Where a held item's own picture (<paramref name="width"/> x <paramref name="height"/> pixels, its icon square at <paramref name="originX"/>,
    /// <paramref name="originY"/> from the lower left) is drawn when its holder's 32 x 32 icon square is <paramref name="square"/> and the holder's
    /// pixel offset is (<paramref name="shiftX"/>, <paramref name="shiftY"/>): the same pixels <see cref="HudPicker"/> hits for that child
    /// (<see cref="HudChildDrawn"/>), a picture too big for the square fitted into it (DV575).
    /// </summary>
    public static HudRect ChildPicture(HudRect square, int width, int height, int originX, int originY, int shiftX = 0, int shiftY = 0)
    {
        float k = square.Width / 32f, fit = HudChildDrawn.FitOf(width, height);
        if (fit > 0f)
        {
            float w = width * fit, h = height * fit;
            return new HudRect(square.Left + (shiftX + (32 - w) / 2f) * k, square.Bottom - (shiftY + (32 - h) / 2f + h) * k, w * k, h * k);
        }
        return new HudRect(square.Left + (shiftX - originX) * k, square.Bottom - (shiftY - originY + height) * k, width * k, height * k);
    }

    /// <summary>Whether the combat toggle <paramref name="toggle"/> shows combat mode on (tg's icon state <c>combat</c>).</summary>
    public static bool CombatOn(ReplicaWorld replica, ScreenEntry toggle)
        => replica.TryGetAppearance(toggle.Appearance, out AppearanceEntry app) && replica.ResolveString(app.IconState) == "combat";

    /// <summary>The combat pill's word.</summary>
    public static string CombatText(bool on) => on ? I18n.T("Combat") : I18n.T("Peaceful");

    /// <summary>
    /// What the HUD pick gets for the doll entry <paramref name="id"/>: the doll's own 32 x 32 icon square in its box, with no picture, so a point
    /// on it is tg's icon pixel whatever size its zones' pictures are (a wound outline grows them).
    /// </summary>
    public static HudDrawn DollDrawn(long id, in LiveHudLayout layout) => new(id, ScreenLoc.FitIcon(layout.Doll, 32, 32, 0, 0), null, 0, 0);

    /// <summary>What the HUD pick gets for the combat toggle <paramref name="id"/>: the pill, with no picture (all of it takes the click).</summary>
    public static HudDrawn PillDrawn(long id, in LiveHudLayout layout) => new(id, layout.Pill, null, 0, 0);

    /// <summary>Where a column of text left of the panel and the pill (the zone labels) ends, screen pixels from the left.</summary>
    public static float LeftOfPanel(in LiveHudLayout layout, in HudMetrics metrics) => layout.Left - UiTokens.DollPanelGap * UiTokens.Unit(metrics);

    // tg's lowest row of the bottom-right buttons that stand under the panel's column (ui_above_movement_top, ui_human_area): the pill stays above it
    private static readonly ScreenLocSpec BottomRow = ScreenLoc.TryParse("EAST-2:26,SOUTH+1:24", out ScreenLocSpec spec) ? spec : default;

    /// <summary>
    /// The doll panel at the middle right, its right edge <see cref="UiTokens.DollPanelGap"/> left of tg's right-hand column (the tiles at
    /// <c>EAST-1</c>: the health, mood, stamina and internals icons and the alerts), and the combat pill under it; the two centred vertically,
    /// raised when that would reach tg's bottom-right buttons. The doll is a whole multiple of tg's pixels (<see cref="UiTokens.DollPixels"/>
    /// at the reference size), made smaller, down to one, while the panel and the pill do not fit above those buttons. Everything else scales
    /// with <see cref="UiTokens.Unit"/> (the options' HUD scale).
    /// <para>
    /// With <paramref name="card"/> (the human HUD, slice C) the held-item card's room is kept at the column's bottom, right above those buttons,
    /// whether an item is held or not (the doll does not move when one is taken), and the panel and the pill stand above it; the card is made
    /// smaller when the doll at one tg pixel per pixel would not fit over it under <see cref="UiTokens.CardTopRoom"/>.
    /// </para>
    /// </summary>
    public static LiveHudLayout Layout(in HudMetrics metrics, bool card = false)
    {
        float u = UiTokens.Unit(metrics);
        float pad = UiTokens.DollPanelPad * u, gap = UiTokens.PillGap * u, pill = MathF.Round(UiTokens.PillHeight * u);
        float right = metrics.Width - 2 * metrics.Tile - UiTokens.DollPanelGap * u; // EAST-1's tile starts two tiles from the right edge
        float floor = ScreenLoc.Place(BottomRow, metrics, 32, 32, 0, 0, out _).Top - UiTokens.DollPanelGap * u;
        int k = Math.Max(1, (int)Math.Round(UiTokens.DollPixels * u, MidpointRounding.AwayFromZero));
        float PanelHeight(int scale) => MathF.Round(2 * pad + HudMetrics.IconPixels * scale);
        HudRect cardRect = default;
        float cardUnit = 0f, ceiling = 0f; // ceiling: where the column may start from the top (the card makes it climb)
        if (card)
        {
            float smallest = PanelHeight(1) + gap + pill;
            float room = floor - UiTokens.CardTopRoom * u - UiTokens.DollPanelGap * u - smallest;
            cardUnit = u * Math.Clamp(room / (UiTokens.CardHeight * u), 0.25f, 1f);
            float cardWidth = MathF.Round(UiTokens.CardWidth * cardUnit), cardHeight = MathF.Round(UiTokens.CardHeight * cardUnit);
            cardRect = new HudRect(MathF.Floor(right - cardWidth), MathF.Round(floor - cardHeight), cardWidth, cardHeight);
            floor = cardRect.Top - UiTokens.DollPanelGap * u;
            ceiling = UiTokens.CardTopRoom * u;
        }
        while (k > 1 && PanelHeight(k) + gap + pill > floor - ceiling)
            k--;
        float side = HudMetrics.IconPixels * k, panelHeight = PanelHeight(k), block = panelHeight + gap + pill;
        float width = MathF.Round(Math.Max(side + 2 * pad, UiTokens.PillWidth * u)); // the panel and the pill one column, the doll centred
        float top = MathF.Round(Math.Max(Math.Min(ceiling, Math.Max(0f, floor - block)), Math.Min((metrics.Height - block) / 2f, floor - block)));
        var panel = new HudRect(MathF.Floor(right - width), top, width, panelHeight);
        var doll = new HudRect(panel.Left + MathF.Floor((width - side) / 2f), top + MathF.Round(pad), side, side);
        var pillRect = new HudRect(panel.Left, MathF.Round(top + panelHeight + gap), width, pill);
        return new LiveHudLayout(panel, doll, pillRect, k, cardRect, cardUnit);
    }
}
