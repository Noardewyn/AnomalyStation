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

    /// <summary>
    /// One of tg's alerts (<c>/atom/movable/screen/alert</c>: in no HUD group, its icon tg's <c>screen_alert.dmi</c>, or the <c>template</c> state
    /// of the player's HUD style for an alert that takes it, or any picture at one of tg's five alert places): drawn as a chip by the doll panel
    /// (slice D, <see cref="LiveHud.AlertStack"/>).
    /// </summary>
    Alert,

    /// <summary>tg's health indicator (<c>healths</c> at <c>ui_health</c>): in the vitals panel's second cell (<see cref="LiveHud.VitalSquare"/>).</summary>
    Health,

    /// <summary>tg's mood face (<c>ui_mood</c>): in the third cell, centred, or with the hunger bar beside it at tg's offset while that shows.</summary>
    Mood,

    /// <summary>tg's hunger bar (<c>ui_hunger</c>): in the third cell, left of the mood face.</summary>
    Hunger,

    /// <summary>tg's stamina indicator (<c>ui_stamina</c>): a chip after the alerts while the vitals panel is shown and tg draws it.</summary>
    Stamina,

    /// <summary>tg's internals indicator (<c>ui_internal</c>): a chip after the stamina chip while the vitals panel is shown and tg draws it.</summary>
    Internals,

    /// <summary>tg's suit cell indicator (<c>ui_spacesuit</c>): a chip after the internals.</summary>
    SpaceSuit,

    /// <summary>
    /// Any other entry tg has put in its right status column (<see cref="LiveHud.InStatusColumn"/>), in tg's info group and neither a slot nor a hand:
    /// a chip after the suit cell while the vitals panel is shown and tg draws it, in tg's order top to bottom.
    /// </summary>
    StatusOther,
}

/// <summary>Where the live HUD's boxes go, screen pixels (<see cref="LiveHud.Layout"/>).</summary>
/// <param name="Panel">The doll panel's background.</param>
/// <param name="Doll">The square the doll's 32 x 32 icon fills (<see cref="ScreenLoc.FitIcon"/>).</param>
/// <param name="Pill">The combat pill under the panel.</param>
/// <param name="DollScale">Screen pixels per tg pixel of the doll: a whole number.</param>
/// <param name="Vitals">Slice D: the full vitals column at the screen's right edge, its panel holding tg's health indicator and mood face (with
/// the hunger bar) under the doll; false: the reduced column left of tg's right column, which keeps those (the designer, 2026-11-26).</param>
/// <param name="Health">The vitals panel's health cell: the square tg's health indicator's 32 x 32 icon fills.</param>
/// <param name="Mood">Its mood cell: the row the mood face fills alone at <paramref name="CellScale"/>, or the hunger and mood pair (58 by 32 tg
/// pixels) at <paramref name="PairScale"/>, each centred in it (<see cref="LiveHud.VitalSquare"/>).</param>
/// <param name="CellScale">Screen pixels per tg pixel of the health and mood cells: the doll's less one, at least one.</param>
/// <param name="PairScale">The same for the hunger and mood pair: smaller, step by step, where the pair would be wider than the panel at the cells' scale.</param>
public readonly record struct LiveHudLayout(HudRect Panel, HudRect Doll, HudRect Pill, int DollScale, bool Vitals = false,
    HudRect Health = default, HudRect Mood = default, int CellScale = 0, int PairScale = 0)
{
    /// <summary>The left edge of the panel and the pill together: whatever stands left of them (the zone labels, the prompts) ends a gap before it.</summary>
    public float Left => Math.Min(Panel.Left, Pill.Left);
}

/// <summary>Where the hands and the hotbar's gap go, screen pixels (<see cref="LiveHud.Hands"/>).</summary>
/// <param name="Left">The left hand's box, left of the gap.</param>
/// <param name="Right">The right hand's box, right of it.</param>
/// <param name="Hotbar">The gap kept for the nine-slot hotbar; tg's bottom slots stand in it until package 3 (<see cref="LiveHud.SlotCell"/>).</param>
/// <param name="Unit">Screen pixels per reference pixel in the row (<see cref="UiTokens.Unit"/>, less when the row is made smaller to fit).</param>
/// <param name="Hint">Slice D: the held-item hint strip over the row (<see cref="LiveHud.HintBy"/>); empty where it is left out.</param>
/// <param name="HintShare">The share of its natural size the hint strip is drawn at.</param>
public readonly record struct HandsLayout(HudRect Left, HudRect Right, HudRect Hotbar, float Unit, HudRect Hint = default, float HintShare = 0f);

/// <summary>The live HUD's reading of tg's screen entries and its layout (ui-overhaul.md section 6, wireframes Mockup 1).</summary>
public static class LiveHud
{
    /// <summary>
    /// What <paramref name="entry"/> is, by its hand index, slot id, icon state and group, never by where it is drawn (only tg's own alert places
    /// say an entry is an alert): an entry of a native window (a
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
        // tg's own place for an alert (ui_alert1 to ui_alert5), whatever its picture: an alert from another icon file (the tracker, the blood
        // worm's) is not left behind in tg's column
        bool placed = ScreenLoc.TryParse(entry.ScreenLoc, out ScreenLocSpec at);
        if (entry.Group == HudGroup.None && entry.HandIndex == 0 && placed && ScreenLoc.AlertSlot(at) > 0)
            return LiveHudRole.Alert;
        // tg's right-column indicators, by tg's own place for each (hud.dm), which no other screen object of the human HUD takes
        if (placed && entry.HandIndex == 0)
        {
            foreach ((ScreenLocSpec spec, LiveHudRole role) in VitalLocs)
            {
                if (at == spec)
                    return role;
            }
        }
        if (!replica.TryGetAppearance(entry.Appearance, out AppearanceEntry app))
            return LiveHudRole.Tg;
        string state = replica.ResolveString(app.IconState);
        if (state == "zone_sel")
            return LiveHudRole.ZoneSelector;
        if (state is "combat" or "combat_off")
            return LiveHudRole.CombatToggle;
        if (state is "toggle" or "toggle_active")
            return LiveHudRole.InventoryToggle;
        // tg's alert (alert.dm): its own icon file, or with USER_HUD_STYLE_INHERIT (weightless, the gravity alerts, succumb) the HUD style's
        // "template" state with the alert's picture as an overlay; a full inventory slot also shows "template" but has a slot id (above)
        if (entry.Group == HudGroup.None && ((replica.ResolveResource(app.Icon) ?? replica.ResolveString(app.Icon)) == AlertIcon || state == "template"))
            return LiveHudRole.Alert;
        var zones = new List<FoundZone>();
        HudReader.Collect(replica, entry.Appearance, zones);
        if (zones.Count > 0)
            return LiveHudRole.Doll;
        // anything else of tg's info group in its right status column (a status display of another body: the alien plasma display on a human)
        return entry.Group == HudGroup.Info && entry.HandIndex == 0 && placed && InStatusColumn(at) ? LiveHudRole.StatusOther : LiveHudRole.Tg;
    }

    /// <summary>
    /// Whether an entry of <paramref name="role"/> is drawn and takes clicks now: tg's zone selector only while the cursor is free (cursor mode, Tab),
    /// where any zone, the eyes, mouth and groin included (tg's surgeries, a ctrl-click on the doll), is still chosen on it (looking aims at them too since
    /// package T, slice T4a, and the selector shows the zone the server selected); every other
    /// entry always.
    /// </summary>
    public static bool Shown(LiveHudRole role, bool cursorMode) => role != LiveHudRole.ZoneSelector || cursorMode;

    /// <summary>
    /// The role an entry is drawn in: the hands, the bottom slots and the alerts only on the human HUD (<paramref name="humanHud"/>: its inventory
    /// toggle is on the screen); the alien's, a basic mob's and the other HUDs keep tg's places, the alerts tg's column (ui-overhaul.md section 6).
    /// Slice D: tg's health, mood and hunger indicators are re-placed into the vitals panel and the stamina, internals and suit cell indicators
    /// become alert chips only with the full vitals column (<paramref name="vitals"/>, <see cref="LiveHudLayout.Vitals"/>); with the reduced one they stay at
    /// tg's places.
    /// </summary>
    public static LiveHudRole Placed(LiveHudRole role, bool humanHud, bool vitals = false) => role switch
    {
        LiveHudRole.LeftHand or LiveHudRole.RightHand or LiveHudRole.BottomSlot or LiveHudRole.Alert when !humanHud => LiveHudRole.Tg,
        LiveHudRole.Health or LiveHudRole.Mood or LiveHudRole.Hunger when !humanHud || !vitals => LiveHudRole.Tg,
        LiveHudRole.Stamina or LiveHudRole.Internals or LiveHudRole.SpaceSuit or LiveHudRole.StatusOther => humanHud && vitals ? LiveHudRole.Alert : LiveHudRole.Tg,
        _ => role,
    };

    // tg's places of its right-column indicators (__DEFINES/hud.dm)
    private static ScreenLocSpec Loc(string text) => ScreenLoc.TryParse(text, out ScreenLocSpec spec) ? spec : default;
    private static readonly ScreenLocSpec HealthLoc = Loc("EAST-1:28,CENTER-1:19"), MoodLoc = Loc("EAST-1:28,CENTER:21"), HungerLoc = Loc("EAST-1:2,CENTER:21");
    private static readonly (ScreenLocSpec Spec, LiveHudRole Role)[] VitalLocs =
    {
        (HealthLoc, LiveHudRole.Health), (MoodLoc, LiveHudRole.Mood), (HungerLoc, LiveHudRole.Hunger), (Loc("EAST-1:28,CENTER-3:14"), LiveHudRole.Stamina),
        (Loc("EAST-1:28,CENTER+1:21"), LiveHudRole.Internals), (Loc("EAST-1:28,CENTER-4:14"), LiveHudRole.SpaceSuit),
    };

    /// <summary>
    /// Whether <paramref name="spec"/> is in tg's right status column: the screen's last column or the one before it (<c>EAST</c> or <c>EAST-1</c>, any
    /// pixel offset), counted from the screen's middle (<c>CENTER</c>), as all of hud.dm's places there are (ui_healthdoll, ui_health, ui_mood,
    /// ui_hunger, ui_stamina, ui_internal, ui_spacesuit, ui_alienplasmadisplay_human and the others); none of its bottom or left places is.
    /// </summary>
    public static bool InStatusColumn(in ScreenLocSpec spec)
        => !spec.HasRange && spec.X.Anchor == ScreenAnchor.High && spec.X.Tile is 0 or -1 && spec.Y.Anchor == ScreenAnchor.Center;

    /// <summary>
    /// The order of the chips (slice D): tg's alerts in tg's order (<see cref="AlertOrder"/>), then the stamina, internals and suit cell indicators,
    /// then any other entry of tg's right status column, tg's top to bottom.
    /// </summary>
    public static int ChipOrder(LiveHudRole role, in ScreenLocSpec spec) => role switch
    {
        LiveHudRole.Stamina => 1000,
        LiveHudRole.Internals => 1001,
        LiveHudRole.SpaceSuit => 1002,
        LiveHudRole.StatusOther => 3000 - (spec.Y.Tile * (int)HudMetrics.IconPixels + spec.Y.Pixels), // higher on the screen first
        _ => AlertOrder(spec),
    };

    // tg's alert icon file (alert.dm, /atom/movable/screen/alert)
    private const string AlertIcon = "icons/hud/screen_alert.dmi";

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

    /// <summary>
    /// Whether tg marks the hand box <paramref name="hand"/> as unusable: one of <see cref="HandOverlays"/> is tg's <c>handcuff_overlay</c>
    /// (screen_gen.dmi <c>markus</c> on a right hand, <c>gabrielle</c> on a left one) or its <c>blocked_overlay</c> (<c>blocked</c>: the arm is
    /// missing or disabled). The native box then has a red border and a lighter fill.
    /// </summary>
    public static bool HandMarked(ReplicaWorld replica, ScreenEntry hand)
        => HandOverlays(replica, hand).Exists(o => replica.TryGetAppearance(o, out AppearanceEntry e) && replica.ResolveString(e.IconState) is "markus" or "gabrielle" or "blocked");

    /// <summary>
    /// Whether one of tg's status indicators <paramref name="entry"/> (stamina, internals, suit cell, another of its right column, or the hunger bar)
    /// draws something visible now (a chip; the mood pair): tg hides one by clearing its icon state (screen_objects.dm update_spacesuit_hud_icon:
    /// SPACESUIT_NO_ICON), by a state its icon file does not have ("spacesuit_0", which BYOND draws as nothing) or by making it transparent (the
    /// hunger bar keeps "hungerbar" at alpha 0 while fed). <paramref name="stateExists"/> says whether an icon file (its resource path) has a state.
    /// </summary>
    public static bool IndicatorShown(ReplicaWorld replica, ScreenEntry entry, Func<string, string, bool> stateExists)
    {
        if (!replica.TryGetAppearance(entry.Appearance, out AppearanceEntry app) || app.Alpha == 0 || replica.ResolveString(app.IconState) is not { Length: > 0 } state)
            return false;
        return (replica.ResolveResource(app.Icon) ?? replica.ResolveString(app.Icon)) is not string icon || stateExists(icon, state);
    }

    /// <summary>The swap-hands key cap's label: the first key bound to swapping hands; null while it is unbound.</summary>
    public static string SwapKey(KeyBindings bindings) => KeyChips.KeyOf(bindings, PlayAction.SwapHand);

    /// <summary>
    /// The swap-hands key cap, <paramref name="width"/> screen pixels wide at most (its label's measure), in the top outer corner of the inactive
    /// hand's box (the right box's top right when the left hand is active, the left box's top left when the right one is), wholly inside it and
    /// sized with it; empty when neither of the two is active (a third or fourth hand is). Drawn only: a click there is on the hand's box.
    /// </summary>
    public static HudRect SwapCap(in HandsLayout hands, bool leftActive, bool rightActive, float width)
    {
        if (leftActive == rightActive)
            return default;
        HudRect box = leftActive ? hands.Right : hands.Left;
        float k = hands.Unit, inset = Math.Max(1f, MathF.Round(UiTokens.SwapCapInset * k));
        float height = Math.Min(MathF.Round(UiTokens.SwapCapHeight * k), box.Height - 2 * inset), w = Math.Min(MathF.Ceiling(width), box.Width - 2 * inset);
        if (height <= 0f || w <= 0f)
            return default;
        return new HudRect(leftActive ? box.Right - inset - w : box.Left + inset, box.Top + inset, w, height);
    }

    // what the player can do with a held item using keys that exist today (ui-overhaul.md section 6); swapping hands is the cap on the other hand
    private static readonly (PlayAction Action, string Text)[] HeldActions =
    {
        (PlayAction.ActivateInHand, "Use"), (PlayAction.DropItem, "Drop"), (PlayAction.ThrowMode, "Throw"),
    };

    /// <summary>The hint strip's entries: use, drop and throw, each with the first key bound to it; an unbound one is left out.</summary>
    public static List<PromptLine> HintLines(KeyBindings bindings)
    {
        var lines = new List<PromptLine>(HeldActions.Length);
        foreach ((PlayAction action, string text) in HeldActions)
        {
            if (KeyChips.KeyOf(bindings, action) is string key)
                lines.Add(new PromptLine(key, I18n.T(text)));
        }
        return lines;
    }

    // tg's inventory toggle at the bottom left (ui_inventory) and the left column of its bottom-right menu (ui_human_floor_changer,
    // ui_human_memories_menu): the hands' row stands between them
    private static readonly ScreenLocSpec ToggleLoc = ScreenLoc.TryParse("WEST:6,SOUTH:5", out ScreenLocSpec toggle) ? toggle : default;
    private static readonly ScreenLocSpec MenuLoc = ScreenLoc.TryParse("EAST-4:22,SOUTH:5", out ScreenLocSpec menu) ? menu : default;

    /// <summary>
    /// The hands' row at the bottom centre: the hotbar's gap, the left hand's box left of it and the right hand's right of it, each
    /// <see cref="UiTokens.HandActive"/> when it is the active hand (tg's overlay on it: <paramref name="leftActive"/>, <paramref name="rightActive"/>;
    /// neither when a third or fourth hand is active) and <see cref="UiTokens.HandInactive"/> otherwise, all on one bottom line. The row keeps a gap from tg's inventory toggle and from tg's bottom-right menu, and stays under the chat
    /// lines' lowest line; it is moved off the middle as little as that takes, and made smaller when it still does not fit. With the chat window
    /// open at the bottom left (its right edge <paramref name="chatRight"/>, screen pixels; 0: none) the row moves right of it only while it then
    /// fits there: wholly right of the window and on the screen, its smaller hand box at <see cref="UiTokens.HandMinPixels"/> or more, and clear of
    /// tg's drop and swap buttons; otherwise it is exactly where it is with the chat window closed, and a window over it covers it as a window
    /// covers tg's HUD (D282). The gap and the boxes keep their places when the active hand changes.
    /// Slice D: with the human HUD's column <paramref name="column"/> (<see cref="Layout"/>) the held-item hint strip's place over the row for a
    /// strip of natural width <paramref name="hint"/> (<see cref="HintBy"/>), which never moves the row.
    /// </summary>
    public static HandsLayout Hands(in HudMetrics metrics, bool leftActive, bool rightActive, float chatRight = 0f, LiveHudLayout column = default, float hint = 0f)
    {
        float toggle = ScreenLoc.Place(ToggleLoc, metrics, 32, 32, 0, 0, out _).Right;
        HandsLayout row = Row(metrics, leftActive, rightActive, toggle);
        float besideChat = 0f;
        if (chatRight > toggle)
        {
            // judged with both boxes at the active size, so a swap never moves the row between the two places
            HandsLayout beside = Row(metrics, true, true, chatRight);
            if (beside.Left.Left >= chatRight && UiTokens.HandInactive * beside.Unit >= UiTokens.HandMinPixels && !OverDropOrSwap(beside, metrics))
                (row, besideChat) = (Row(metrics, leftActive, rightActive, chatRight), chatRight);
        }
        // slice D: the held-item hint strip over the row, with the vitals column given (the human HUD)
        if (column.Panel.Width <= 0f)
            return row;
        HudRect strip = HintBy(metrics, row, column, besideChat, hint, out float share);
        return row with { Hint = strip, HintShare = share };
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

    // tg's drop and swap buttons over the gap's middle (ui_swaphand_position), which stay at tg's places: a row moved right of the chat window must
    // not reach under them
    private static readonly ScreenLocSpec DropLoc = ScreenLoc.TryParse("CENTER-1:16,SOUTH+1:5", out ScreenLocSpec drop) ? drop : default;
    private static readonly ScreenLocSpec SwapLoc = ScreenLoc.TryParse("CENTER:16,SOUTH+1:5", out ScreenLocSpec swap) ? swap : default;

    private static bool OverDropOrSwap(in HandsLayout row, in HudMetrics metrics)
    {
        HudRect drop = ScreenLoc.Place(DropLoc, metrics, 32, 32, 0, 0, out _), swap = ScreenLoc.Place(SwapLoc, metrics, 32, 32, 0, 0, out _);
        foreach (HudRect part in new[] { row.Left, row.Right, row.Hotbar })
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
        const float height = UiTokens.HotbarMargin + UiTokens.HandActive;
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
        return new HandsLayout(leftBox, rightBox, hotbar, k);
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
    /// The doll panel and the combat pill under it. On the human HUD (<paramref name="human"/>; slice D, the designer, 2026-11-26): the full vitals
    /// column (<see cref="VitalsColumn"/>: the doll, tg's health indicator and mood face in one panel at the screen's right edge, over tg's sleep
    /// button) wherever it fits at this screen and scale; else the reduced column, the panel holding the doll alone with the vitals panel's padding,
    /// its right edge <see cref="UiTokens.DollPanelGap"/> left of tg's right-hand column (the tiles at <c>EAST-1</c>, where tg's health, mood,
    /// stamina and internals indicators then stay), the panel and the pill <see cref="UiTokens.VitalsWidth"/> wide, centred vertically and kept
    /// above tg's bottom-right buttons and under the version tag and the ping chip (<see cref="UiTokens.CardTopRoom"/>): the doll made smaller,
    /// then the pill as the full column's, and only then the panel climbing into that room. On the other HUDs the same reduced column at the
    /// doll's size with the slice B padding, the pill at least <see cref="UiTokens.PillWidth"/> wide. The doll is a whole multiple of tg's pixels
    /// (<see cref="UiTokens.DollPixels"/> at the reference size); everything else scales with <see cref="UiTokens.Unit"/> (the options' HUD scale).
    /// </summary>
    public static LiveHudLayout Layout(in HudMetrics metrics, bool human = false)
    {
        if (human && VitalsColumn(metrics) is LiveHudLayout vitals)
            return vitals;
        float u = UiTokens.Unit(metrics);
        float pad = (human ? UiTokens.VitalsPad : UiTokens.DollPanelPad) * u, gap = UiTokens.PillGap * u, pill = MathF.Round(UiTokens.PillHeight * u);
        float right = metrics.Width - 2 * metrics.Tile - UiTokens.DollPanelGap * u; // EAST-1's tile starts two tiles from the right edge
        float floor = ScreenLoc.Place(BottomRow, metrics, 32, 32, 0, 0, out _).Top - UiTokens.DollPanelGap * u;
        int k = DollScaleAt(u);
        float PanelHeight(int scale) => MathF.Round(2 * pad + HudMetrics.IconPixels * scale);
        float ceiling = human ? UiTokens.CardTopRoom * u : 0f; // where the column may start from the top
        while (k > 1 && PanelHeight(k) + gap + pill > floor - ceiling)
            k--;
        // slice D: the reduced column's pill is made smaller as the full column's is, down to a quarter, before the panel climbs into the top room
        for (int step = 1; human && step <= 15 && PanelHeight(k) + gap + pill > floor - ceiling; step++)
            pill = MathF.Round(UiTokens.PillHeight * u * (1f - 0.05f * step));
        float side = HudMetrics.IconPixels * k, panelHeight = PanelHeight(k), block = panelHeight + gap + pill;
        // the panel and the pill one column, the doll centred
        float width = MathF.Round(human ? UiTokens.VitalsWidth * u : Math.Max(side + 2 * pad, UiTokens.PillWidth * u));
        // never past the screen's left edge (a portrait screen at a large scale), on the human HUD with room for a column of the smallest chips
        // left of it: the doll and then the padding smaller as far as needed
        float room = MathF.Max(1f, MathF.Floor(right - (human ? MathF.Round(UiTokens.AlertStackGap * u) + Math.Min(MathF.Round(UiTokens.AlertChip * u), UiTokens.AlertChipMinPixels) : 0f)));
        if (width > room)
        {
            width = room;
            while (k > 1 && HudMetrics.IconPixels * k + 2 * pad > width)
                k--;
            side = Math.Min(HudMetrics.IconPixels * k, width);
            pad = Math.Min(pad, MathF.Floor((width - side) / 2f));
            panelHeight = PanelHeight(k) - HudMetrics.IconPixels * k + side;
            block = panelHeight + gap + pill;
        }
        float top =MathF.Round(Math.Max(Math.Min(ceiling, Math.Max(0f, floor - block)), Math.Min((metrics.Height - block) / 2f, floor - block)));
        var panel = new HudRect(MathF.Floor(right - width), top, width, panelHeight);
        var doll = new HudRect(panel.Left + MathF.Floor((width - side) / 2f), top + MathF.Round(pad), side, side);
        var pillRect = new HudRect(panel.Left, MathF.Round(top + panelHeight + gap), width, pill);
        return new LiveHudLayout(panel, doll, pillRect, k);
    }

    // the doll's whole tg pixels per screen pixel at the reference unit's size (3 at 1080 lines and 100 %)
    private static int DollScaleAt(float u) => Math.Max(1, (int)Math.Round(UiTokens.DollPixels * u, MidpointRounding.AwayFromZero));

    // tg's highest button under the vitals column at the bottom right: the sleep button (ui_above_throw); the pill stands over it
    private static readonly ScreenLocSpec SleepLoc = Loc("EAST-1:28,SOUTH+1:41");

    /// <summary>
    /// The full vitals column (the designer, 2026-11-26), or null where it does not fit: the vitals panel and the combat pill
    /// <see cref="UiTokens.PillGap"/> under it, <see cref="UiTokens.VitalsWidth"/> wide, <see cref="UiTokens.VitalsMargin"/> from the screen's right
    /// edge, anchored at the bottom with the pill's bottom that margin over tg's highest button under the column (<c>ui_above_throw</c>). The panel
    /// holds three cells top to bottom, centred, <see cref="UiTokens.VitalsPad"/> inside its top and bottom and <see cref="UiTokens.VitalsCellGap"/>
    /// apart: the doll at its whole-pixel scale S, tg's health indicator and tg's mood face at S - 1 (at least 1), the mood cell wide enough for tg's
    /// hunger and mood pair (58 by 32 tg pixels: tg's two squares at tg's offset; smaller, step by step, where it would be wider than the panel). The column
    /// is never narrower than the pair at one tg pixel per pixel and the panel's side padding, the pill as wide. The cells keep their room whether
    /// their entries are shown or not, so the panel's size never changes in play. The column keeps under the version tag and the ping and FPS chip
    /// (<see cref="UiTokens.CardTopRoom"/>): S steps down while it does not fit, and at S = 1 the pill is made smaller, down to a quarter; null when
    /// even that does not fit. It depends on the screen and the scale alone.
    /// </summary>
    public static LiveHudLayout? VitalsColumn(in HudMetrics metrics)
    {
        float u = UiTokens.Unit(metrics);
        float pad = MathF.Round(UiTokens.VitalsPad * u), cellGap = MathF.Round(UiTokens.VitalsCellGap * u);
        // never narrower than its widest cell at one tg pixel per pixel (the 58-pixel hunger and mood pair) and the padding at its sides
        float width = Math.Max(MathF.Round(UiTokens.VitalsWidth * u), PairWidth + 2 * pad);
        float right = MathF.Round(metrics.Width - UiTokens.VitalsMargin * u), left = right - width;
        // whole pixels (tg's sleep button can stand at a fraction of one), so every rectangle of the column is whole
        float floor = MathF.Floor(ScreenLoc.Place(SleepLoc, metrics, 32, 32, 0, 0, out _).Top - MathF.Round(UiTokens.VitalsMargin * u));
        float ceiling = MathF.Ceiling(UiTokens.CardTopRoom * u), gap = MathF.Round(UiTokens.PillGap * u);
        const float icon = HudMetrics.IconPixels;
        for (int s = DollScaleAt(u); s >= 1; s--)
        {
            int cell = Math.Max(1, s - 1);
            int pair = cell;
            while (pair > 1 && PairWidth * pair > width) // smaller until it fits the panel's width
                pair--;
            float moodHeight = icon * Math.Max(cell, pair), panelHeight = 2 * pad + icon * (s + cell) + moodHeight + 2 * cellGap;
            for (int step = 0; step <= 15 && (s == 1 || step == 0); step++)
            {
                float pill = MathF.Round(UiTokens.PillHeight * u * (1f - 0.05f * step));
                if (floor - pill - gap - panelHeight < ceiling)
                    continue;
                var pillRect = new HudRect(left, floor - pill, width, pill);
                var panel = new HudRect(left, pillRect.Top - gap - panelHeight, width, panelHeight);
                float y = panel.Top + pad;
                HudRect Centred(float w, float h)
                {
                    var r = new HudRect(left + MathF.Floor((width - w) / 2f), y, w, h);
                    y += h + cellGap;
                    return r;
                }
                HudRect doll = Centred(icon * s, icon * s), health = Centred(icon * cell, icon * cell);
                HudRect mood = Centred(Math.Max(PairWidth * pair, icon * cell), moodHeight);
                return new LiveHudLayout(panel, doll, pillRect, s, true, health, mood, cell, pair);
            }
        }
        return null;
    }

    // tg's hunger bar (ui_hunger, EAST-1:2) stands this many tg pixels left of its mood face (ui_mood, EAST-1:28), on the same row: the pair is one
    // box of that offset and a square
    private static readonly int MoodOffset = MoodLoc.X.Pixels - HungerLoc.X.Pixels;
    private static readonly int PairWidth = MoodOffset + (int)HudMetrics.IconPixels;

    /// <summary>
    /// The square that the 32 x 32 icon of the re-placed vitals entry of <paramref name="role"/> fills in the vitals panel (null for any other role or
    /// without the full column): the health cell; in the mood cell, while tg's hunger bar shows (<paramref name="hungerShown"/>,
    /// <see cref="IndicatorShown"/>), the hunger and mood pair centred, the mood face at tg's offset right of the hunger bar; else the mood face alone,
    /// centred at the cells' scale.
    /// </summary>
    public static HudRect? VitalSquare(LiveHudRole role, in LiveHudLayout layout, bool hungerShown = true)
    {
        if (!layout.Vitals)
            return null;
        HudRect row = layout.Mood;
        float k = layout.PairScale, side = HudMetrics.IconPixels * k, cell = HudMetrics.IconPixels * layout.CellScale;
        float pairLeft = row.Left + MathF.Floor((row.Width - PairWidth * k) / 2f), pairTop = row.Top + MathF.Floor((row.Height - side) / 2f);
        return role switch
        {
            LiveHudRole.Health => layout.Health,
            LiveHudRole.Hunger => new HudRect(pairLeft, pairTop, side, side),
            LiveHudRole.Mood when hungerShown => new HudRect(pairLeft + MoodOffset * k, pairTop, side, side),
            LiveHudRole.Mood => new HudRect(row.Left + MathF.Floor((row.Width - cell) / 2f), row.Top + MathF.Floor((row.Height - cell) / 2f), cell, cell),
            _ => null,
        };
    }

    // ---- the held-item hint strip above the hands (the designer, 2026-11-26) ----

    /// <summary>
    /// The side of the hint strip's item picture at a strip <paramref name="height"/> screen pixels high: the held item's 32 x 32 icon at the
    /// largest whole number of screen pixels per tg pixel that fits; a strip lower than 32 fits it smaller.
    /// </summary>
    public static float HintPicture(float height) => height >= HudMetrics.IconPixels ? HudMetrics.IconPixels * MathF.Floor(height / HudMetrics.IconPixels) : Math.Max(1f, height);

    /// <summary>
    /// The hint strip's natural width at <paramref name="unit"/> screen pixels per reference pixel: <see cref="UiTokens.HintPad"/> at each end, the
    /// item's picture, then each entry (its key cap's and its verb's widths at the compact size, <paramref name="entries"/>, measured by the caller)
    /// <see cref="UiTokens.HintGap"/> after the one before, its cap <see cref="UiTokens.HintCapGap"/> before its verb.
    /// </summary>
    public static float HintWidth(float unit, IReadOnlyList<(float Cap, float Verb)> entries)
    {
        float width = 2 * UiTokens.HintPad * unit + HintPicture(MathF.Round(UiTokens.PillHeight * unit));
        foreach ((float cap, float verb) in entries)
            width += UiTokens.HintGap * unit + cap + UiTokens.HintCapGap * unit + verb;
        return width;
    }

    // tg's human HUD entries that stay at tg's places at the bottom (the inventory toggle, drop and swap, the bottom-right buttons and menu) and the
    // right column's status icons, which stay at tg's places with the reduced column: the hint strip keeps clear of all of them
    private static readonly ScreenLocSpec[] HintClear =
    {
        ToggleLoc,
        Loc("EAST-1:28,SOUTH:5"), Loc("EAST-1:28,SOUTH+1:24"), Loc("EAST-1:28,SOUTH+1:41"), Loc("EAST-2:26,SOUTH:5"), Loc("EAST-2:26,SOUTH+1:7"),
        Loc("EAST-2:26, SOUTH+1:24"), Loc("EAST-3:24, SOUTH+1:7"), Loc("EAST-3:24,SOUTH+1:24"), Loc("EAST-3:7,SOUTH+1:7"), Loc("EAST-3:7,SOUTH+1:24"),
        Loc("EAST-4:22,SOUTH:5"), Loc("EAST-4:22,SOUTH+1:7"), HealthLoc, MoodLoc, HungerLoc, Loc("EAST-1:28,CENTER-3:14"), Loc("EAST-1:28,CENTER+1:21"),
        Loc("EAST-1:28,CENTER-4:14"),
    };

    /// <summary>
    /// Where the held-item hint strip goes over the hands' row <paramref name="row"/>, and the share of its size it is drawn at (<paramref name="share"/>;
    /// an empty rectangle where it is left out): its natural width <paramref name="natural"/> (<see cref="HintWidth"/>; 0: none), centred on the
    /// hotbar's gap, its bottom <see cref="UiTokens.PillGap"/> over the higher of the row's top line (a box at the active size) and the visible top of
    /// tg's drop and swap buttons (<see cref="VisibleButton"/>). One wider than the row (both boxes at the active size) is made smaller as one to the row's width, its text to no less than
    /// <see cref="UiTokens.HintLeastShare"/> of the prompts' size; below that it is left out. It is shown only where it is wholly on the screen and
    /// clear of the chat window when the row stands right of it (<paramref name="chatRight"/>), tg's entries at tg's places, the vitals column
    /// (<paramref name="column"/>), every chip it can show (eight) and the widest prompt block beside the crosshair. It never moves the row and
    /// does not change with a swap: it depends on the screen, the scale and the row's place alone (and the bound keys it names).
    /// </summary>
    public static HudRect HintBy(in HudMetrics metrics, in HandsLayout row, in LiveHudLayout column, float chatRight, float natural, out float share)
    {
        share = 0f;
        if (natural <= 0f)
            return default;
        float u = UiTokens.Unit(metrics), k = row.Unit;
        float active = MathF.Round(UiTokens.HandActive * k), handGap = MathF.Round(UiTokens.HandGap * k);
        float rowWidth = row.Hotbar.Width + 2 * (handGap + active);
        float fit = natural > rowWidth ? rowWidth / natural : 1f;
        if (fit * UiTokens.HintShare < UiTokens.HintLeastShare - 0.0001f)
            return default;
        float width = MathF.Floor(natural * fit), height = MathF.Round(UiTokens.PillHeight * u * fit);
        // tg's drop and swap buttons drawn only in the lower part of their squares (UiTokens.DropSwapVisible): the strip sits on what shows
        HudRect drop = VisibleButton(DropLoc, metrics), swap = VisibleButton(SwapLoc, metrics);
        // at the whole pixel above what shows (it can end at a fraction of one), so the strip and its picture stand at whole pixels
        float bottom = MathF.Floor(Math.Min(row.Hotbar.Bottom - active, Math.Min(drop.Top, swap.Top))) - MathF.Round(UiTokens.PillGap * u);
        var hint = new HudRect(MathF.Round(row.Hotbar.Left + row.Hotbar.Width / 2f - width / 2f), bottom - height, width, height);
        if (hint.Left < 0f || hint.Top < 0f || hint.Right > metrics.Width || hint.Bottom > metrics.Height || hint.Left < chatRight)
            return default;
        foreach (ScreenLocSpec spec in HintClear)
        {
            if (Over(hint, ScreenLoc.Place(spec, metrics, 32, 32, 0, 0, out _)))
                return default;
        }
        if (Over(hint, drop) || Over(hint, swap))
            return default;
        List<HudRect> chips = AlertStack(metrics, MaxChips, column);
        foreach (HudRect ours in new List<HudRect>(chips) { column.Panel, column.Pill })
        {
            if (Over(hint, ours))
                return default;
        }
        float edge = Math.Min(LeftOfPanel(column, metrics), LeftOfAlerts(chips, metrics));
        HudRect prompt = KeyChips.PromptBlock(metrics, KeyChips.MaxPromptLines, UiTokens.PromptMaxWidth * u, metrics.Width / 2f, metrics.Height / 2f,
            KeyChips.SightReach(false, 0f, 70f, metrics.Height, 1f), edge);
        if (Over(hint, prompt))
            return default;
        share = fit;
        return hint;
    }

    /// <summary>
    /// The square the held item's 32 x 32 icon fills at the hint strip's left: <see cref="HintPicture"/> of the strip's height, after its end padding
    /// (at <paramref name="share"/> of its size), centred in its height; empty without a strip.
    /// </summary>
    public static HudRect HintPictureSquare(HudRect hint, float unit, float share)
    {
        if (hint.Width <= 0f)
            return default;
        float side = HintPicture(hint.Height);
        return new HudRect(hint.Left + MathF.Round(UiTokens.HintPad * unit * share), hint.Top + MathF.Floor((hint.Height - side) / 2f), side, side);
    }

    // tg's five alerts and the stamina, internals and suit cell chips
    private const int MaxChips = 8;

    /// <summary>
    /// The visible part of tg's drop or swap button at <paramref name="spec"/>: the lower <see cref="UiTokens.DropSwapVisible"/> tg pixels of its 32 x 32
    /// square, where its picture is opaque. The hint strip sits on it.
    /// </summary>
    public static HudRect VisibleButton(in ScreenLocSpec spec, in HudMetrics metrics)
    {
        HudRect square = ScreenLoc.Place(spec, metrics, 32, 32, 0, 0, out _);
        float height = square.Height * UiTokens.DropSwapVisible / HudMetrics.IconPixels;
        return new HudRect(square.Left, square.Bottom - height, square.Width, height);
    }

    /// <summary>The share of its normal size the pill's key chip and word are drawn at: smaller together when they would not fit, never cut.</summary>
    public static float PillFit(float needed, float room) => needed > room && needed > 0f ? room / needed : 1f;

    // ---- slice D: tg's alerts as chips by the doll panel, above it or left of it (the designer, 2026-11-26) ----

    /// <summary>
    /// Where an alert stands in tg's order: its index in tg's column (alert.dm get_ui_alert_placement, read from the screen_loc tg gave it; alert 1
    /// first), the alerts without one last.
    /// </summary>
    public static int AlertOrder(in ScreenLocSpec spec) => ScreenLoc.AlertSlot(spec) is int slot and > 0 ? slot : int.MaxValue;

    /// <summary>
    /// Whether the alert chips stand above the doll panel at this screen and scale: when five chips at full size (<see cref="UiTokens.AlertChip"/>,
    /// <see cref="UiTokens.AlertChipGap"/> apart, <see cref="UiTokens.AlertStackGap"/> over the panel) fit between the panel's top and the version
    /// tag and the ping and FPS chip (<see cref="UiTokens.CardTopRoom"/>); else they stand left of the doll's column. It never depends on how
    /// many alerts show, so no chip moves to the other place when one comes or goes (the designer, 2026-11-26).
    /// </summary>
    public static bool AlertsAbove(in HudMetrics metrics, in LiveHudLayout layout)
    {
        float u = UiTokens.Unit(metrics), side = MathF.Round(UiTokens.AlertChip * u), gap = MathF.Round(UiTokens.AlertChipGap * u);
        return layout.Panel.Top - MathF.Round(UiTokens.AlertStackGap * u) - (MaxAlerts * side + (MaxAlerts - 1) * gap) >= MathF.Ceiling(UiTokens.CardTopRoom * u);
    }

    // tg shows five alerts at most (alert.dm get_ui_alert_placement)
    private const int MaxAlerts = 5;

    /// <summary>
    /// The chips of <paramref name="count"/> alerts, square, in tg's order from top to bottom (alert 1 at the top), by the doll panel
    /// (<paramref name="layout"/>). Above it (<see cref="AlertsAbove"/>): one column at full size, right-aligned with the panel, the lowest
    /// <see cref="UiTokens.AlertStackGap"/> over its top, holding the first chips (as many as fit), and the chips after them (the internals and
    /// suit cell chips, say) in further columns to the left, each read top down and standing on the panel. Else left of it: a column whose right edge is that gap left of the
    /// panel and the pill, its first chip level with the panel's top (or under the top room), running down beside them, above tg's bottom-right rows and the hands' row, and above the widest prompt block
    /// where that stands under it; when five full-size chips do not fit there they are all made smaller, down to
    /// <see cref="UiTokens.AlertChipMinPixels"/> (or their full size when that is smaller), and when they still do not fit the column goes on in
    /// further columns to the left, top down again. The size and the places there are those of five chips whatever the count, so a chip never
    /// moves when another comes or goes. Empty without a doll panel.
    /// </summary>
    public static List<HudRect> AlertStack(in HudMetrics metrics, int count, in LiveHudLayout layout)
    {
        var chips = new List<HudRect>(Math.Max(0, count));
        if (count <= 0 || layout.Panel.Width <= 0f)
            return chips;
        float u = UiTokens.Unit(metrics), stackGap = MathF.Round(UiTokens.AlertStackGap * u);
        float full = MathF.Round(UiTokens.AlertChip * u), fullGap = MathF.Round(UiTokens.AlertChipGap * u);
        if (AlertsAbove(metrics, layout))
        {
            float bottom = layout.Panel.Top - stackGap;
            // the first chips in the column at the edge, as many as fit (five at least); the chips after them go on in the next column to the left
            int above = Math.Max(MaxAlerts, (int)MathF.Floor((bottom - MathF.Ceiling(UiTokens.CardTopRoom * u) + fullGap) / (full + fullGap)));
            for (int i = 0; i < count; i++)
            {
                int column = i / above, row = i % above, inColumn = Math.Min(above, count - column * above);
                float columnTop = bottom - inColumn * full - (inColumn - 1) * fullGap; // each column stands on the panel
                chips.Add(new HudRect(layout.Panel.Right - (column + 1) * full - column * fullGap, columnTop + row * (full + fullGap), full, full));
            }
            return chips;
        }
        float top = Math.Max(layout.Panel.Top, MathF.Ceiling(UiTokens.CardTopRoom * u));
        // beside the panel and the pill, left of whichever reaches further left
        float right = (layout.Pill.Width > 0f ? Math.Min(layout.Panel.Left, layout.Pill.Left) : layout.Panel.Left) - stackGap;
        float handsRow = metrics.Height - (UiTokens.HotbarMargin + UiTokens.HandActive) * u; // the hands' row at its full size: it is only ever smaller
        float floor = Math.Min(ScreenLoc.Place(BottomRow, metrics, 32, 32, 0, 0, out _).Top, handsRow) - stackGap;
        HudRect sleep = ScreenLoc.Place(SleepLoc, metrics, 32, 32, 0, 0, out _);
        if (sleep.Left < right) // tg's sleep button, higher than that row, reaches under the column where tg's tiles are wide (a 4K screen at a large scale)
            floor = Math.Min(floor, sleep.Top - stackGap);
        // the widest prompt block beside the crosshair, which ends before the column: where there is no room for it right of the crosshair it
        // stands left of it, under the column on a narrow screen at a large scale, and the column then ends above it
        HudRect prompt = KeyChips.PromptBlock(metrics, KeyChips.MaxPromptLines, UiTokens.PromptMaxWidth * u, metrics.Width / 2f, metrics.Height / 2f,
            KeyChips.SightReach(false, 0f, 70f, metrics.Height, 1f), right - full - UiTokens.DollPanelGap * u);
        if (prompt.Right > right - full && prompt.Left < right)
            floor = Math.Min(floor, prompt.Top - stackGap);
        float room = floor - top;
        float side = MathF.Floor(Math.Min(full, room / (MaxAlerts + (MaxAlerts - 1) * UiTokens.AlertChipGap / UiTokens.AlertChip)));
        float least = Math.Min(full, UiTokens.AlertChipMinPixels);
        int rows = MaxAlerts;
        if (side < least)
        {
            side = least;
            float smallGap = MathF.Round(UiTokens.AlertChipGap * side / UiTokens.AlertChip);
            rows = Math.Max(1, (int)MathF.Floor((room + smallGap) / (side + smallGap)));
        }
        float gap = side == full ? fullGap : MathF.Round(UiTokens.AlertChipGap * side / UiTokens.AlertChip);
        // never past the screen's left edge (a portrait screen at a large scale): smaller still while the columns of the most chips would reach it
        int Columns() => (Math.Max(count, MaxChips) + rows - 1) / rows;
        while (side > 1f && right - Columns() * (side + gap) + gap < 0f)
        {
            side--;
            gap = MathF.Round(UiTokens.AlertChipGap * side / UiTokens.AlertChip);
            rows = Math.Max(1, (int)MathF.Floor((room + gap) / (side + gap)));
        }
        for (int i = 0; i < count; i++)
        {
            int column = i / rows, row = i % rows;
            chips.Add(new HudRect(right - (column + 1) * side - column * gap, top + row * (side + gap), side, side));
        }
        return chips;
    }

    /// <summary>Where what stands left of the alert chips (the prompts, the zone labels) ends, a gap before the leftmost chip; infinity without chips.</summary>
    public static float LeftOfAlerts(List<HudRect> chips, in HudMetrics metrics)
    {
        float left = float.PositiveInfinity;
        foreach (HudRect chip in chips)
            left = Math.Min(left, chip.Left - UiTokens.DollPanelGap * UiTokens.Unit(metrics));
        return left;
    }

    /// <summary>The square inside an alert chip that the alert's 32 x 32 icon square fills (<see cref="ScreenLoc.FitIcon"/>).</summary>
    public static HudRect AlertPicture(HudRect chip)
    {
        float inset = MathF.Round(UiTokens.AlertChipInset * chip.Width / UiTokens.AlertChip);
        return new HudRect(chip.Left + inset, chip.Top + inset, Math.Max(1f, chip.Width - 2 * inset), Math.Max(1f, chip.Height - 2 * inset));
    }

    /// <summary>
    /// The look a screen entry drawn in the role <paramref name="placed"/> (<see cref="Placed"/>) shows of its animate() chain: in a chip
    /// (<see cref="LiveHudRole.Alert"/>: tg's alerts and the status indicators on the human HUD) without moving, so its picture always stays in
    /// <see cref="AlertPicture"/> (tg throws a new alert in 32 pixels to the right, alert.dm:65-66, which would draw it over the panel beside the
    /// empty chip); nothing replaces that slide. At tg's places the look is tg's.
    /// </summary>
    public static HudLook ChipLook(LiveHudRole placed, in HudLook look) => placed == LiveHudRole.Alert ? look with { MoveX = 0f, MoveY = 0f } : look;

    /// <summary>What the HUD pick gets for the alert <paramref name="id"/> drawn in <paramref name="chip"/>: the whole chip, with no picture, so a click anywhere on it is tg's alert.</summary>
    public static HudDrawn AlertDrawn(long id, HudRect chip) => new(id, chip, null, 0, 0);
}
