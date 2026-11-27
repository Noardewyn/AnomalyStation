using Anomaly.Client.Shared.Icons;
using Anomaly.Core.DmShim;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared;

// Owner: ui-overhaul.md section 6 (package 2, slices B to E). Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).
// The server still computes tg's screen entries and a click still goes to them (D107 as revised by ui-overhaul.md section 1); the client
// re-places some of them. The doll only shows injuries: targeting is where the player looks, so tg's zone selector
// shows only while the cursor is free. Slice E: on the human HUD the vitals stand bare at the right edge, tg's combat toggle is in the corner
// and tg's buttons that a key does are neither drawn nor clicked.

/// <summary>What a screen entry is for the live HUD.</summary>
public enum LiveHudRole : byte
{
    /// <summary>Drawn at tg's <c>screen_loc</c> as before.</summary>
    Tg,

    /// <summary>tg's health doll (its appearance holds the doll's zone states, <see cref="HudReader"/>): re-placed (<see cref="LiveHud.Layout"/>).</summary>
    Doll,

    /// <summary>
    /// tg's target zone selector (icon state <c>zone_sel</c>): at tg's place in cursor mode only (<see cref="LiveHud.Shown"/>); in the captured view
    /// targeting is where the player looks.
    /// </summary>
    ZoneSelector,

    /// <summary>tg's combat toggle (icon state <c>combat</c> or <c>combat_off</c>): tg's own picture; slice E: on the human HUD at tg's zone selector's place (<see cref="LiveHud.HumanPlace"/>).</summary>
    CombatToggle,

    /// <summary>tg's first hand box (<see cref="ScreenEntry.HandIndex"/> 1, a left hand): drawn as the native left hand box (slice C).</summary>
    LeftHand,

    /// <summary>tg's second hand box (<see cref="ScreenEntry.HandIndex"/> 2, a right hand): drawn as the native right hand box.</summary>
    RightHand,

    /// <summary>One of tg's bottom slots (an inventory box with a <see cref="ScreenEntry.SlotId"/> outside the toggled inventory: belt, back, pockets, ID, suit storage): moved into the hotbar's gap.</summary>
    BottomSlot,

    /// <summary>tg's inventory toggle (icon state <c>toggle</c> or <c>toggle_active</c>), which only the human HUD has: its presence marks the human HUD; slice E: not drawn there (the inventory key, I).</summary>
    InventoryToggle,

    /// <summary>
    /// One of tg's alerts (<c>/atom/movable/screen/alert</c>: in no HUD group, its icon tg's <c>screen_alert.dmi</c>, or the <c>template</c> state
    /// of the player's HUD style for an alert that takes it, or any picture at one of tg's five alert places): drawn as a chip by the vitals
    /// (slice D, <see cref="LiveHud.AlertStack"/>).
    /// </summary>
    Alert,

    /// <summary>tg's health indicator (<c>healths</c> at <c>ui_health</c>): under the doll in the full vitals stack (<see cref="LiveHud.VitalSquare"/>).</summary>
    Health,

    /// <summary>tg's mood face (<c>ui_mood</c>): under the health indicator, alone.</summary>
    Mood,

    /// <summary>tg's hunger bar (<c>ui_hunger</c>): slice E, the first status chip after the alerts while the full stack is shown and tg draws it.</summary>
    Hunger,

    /// <summary>tg's stamina indicator (<c>ui_stamina</c>): a chip after the hunger chip while the full stack is shown and tg draws it.</summary>
    Stamina,

    /// <summary>tg's internals indicator (<c>ui_internal</c>): a chip after the stamina chip while the full stack is shown and tg draws it.</summary>
    Internals,

    /// <summary>tg's suit cell indicator (<c>ui_spacesuit</c>): a chip after the internals.</summary>
    SpaceSuit,

    /// <summary>
    /// Any other entry tg has put in its right status column (<see cref="LiveHud.InStatusColumn"/>), in tg's info group and neither a slot nor a hand:
    /// a chip after the suit cell while the full stack is shown and tg draws it, in tg's order top to bottom.
    /// </summary>
    StatusOther,

    /// <summary>
    /// Slice E: one of tg's buttons that a key does (pull, resist, rest, throw, run/walk, drop, swap, the floor changer; tg's icon states for them):
    /// neither drawn nor clicked on the human HUD (<see cref="LiveHud.Placed"/>), at tg's place on any other.
    /// </summary>
    KeyButton,

    /// <summary>Slice E: tg's crafting button (<c>craft</c>): not drawn on the human HUD; the crafting key sends its click (<see cref="LiveHud.ButtonOf"/>).</summary>
    Crafting,

    /// <summary>Slice E: tg's language menu button (<c>talk_wheel</c>): the same, for the language key.</summary>
    Language,

    /// <summary>Slice E: tg's navigate button (<c>navigate</c>): the same, for the navigate key.</summary>
    Navigate,

    /// <summary>Slice E: tg's memories button (<c>memories</c>): on the human HUD under Tab only, at tg's combat toggle's place (<c>ui_acti</c>).</summary>
    Memories,

    /// <summary>Slice E: tg's area creator (<c>area_edit</c>): on the human HUD under Tab only, at tg's floor changer's place (<c>ui_human_floor_changer</c>).</summary>
    AreaCreator,

    /// <summary>Slice E: tg's sleep button (<c>act_sleep</c>), which no key does: the last status chip while the full stack is shown and tg draws it.</summary>
    Sleep,

    /// <summary>Slice E: what <see cref="LiveHud.Placed"/> makes of an entry the human HUD neither draws nor lets be clicked.</summary>
    Hidden,
}

/// <summary>Where the live HUD's vitals go, screen pixels (<see cref="LiveHud.Layout"/>).</summary>
/// <param name="Doll">The square the doll's 32 x 32 icon fills (<see cref="ScreenLoc.FitIcon"/>).</param>
/// <param name="DollScale">Screen pixels per tg pixel of the doll: a whole number.</param>
/// <param name="Vitals">The full stack at the screen's right edge: the doll, tg's health indicator and tg's mood face, no box (slice E); false:
/// the reduced arrangement, the doll alone left of tg's right column, which keeps tg's status icons.</param>
/// <param name="Health">The square tg's health indicator's 32 x 32 icon fills, under the doll.</param>
/// <param name="Mood">The square tg's mood face fills, under the health indicator.</param>
/// <param name="CellScale">Screen pixels per tg pixel of the health indicator and the mood face: the doll's less one, at least one.</param>
public readonly record struct LiveHudLayout(HudRect Doll, int DollScale, bool Vitals = false, HudRect Health = default, HudRect Mood = default, int CellScale = 0)
{
    /// <summary>The pictures together: the doll, and with the full stack the health indicator and the mood face under it.</summary>
    public HudRect Stack => Vitals ? new HudRect(Doll.Left, Doll.Top, Math.Max(Doll.Right, Mood.Right) - Doll.Left, Mood.Bottom - Doll.Top) : Doll;

    /// <summary>The stack's left edge (the doll's, the widest picture): whatever stands left of it (the zone labels, the prompts) ends a gap before it.</summary>
    public float Left => Doll.Left;
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
        // slice E: tg's buttons of the human HUD (hud/human.dm, screen_objects.dm), by tg's icon states for them, only in one of its HUD groups (an
        // alert, in none, keeps its role whatever its picture)
        if (entry.Group != HudGroup.None && ButtonRole(state) is LiveHudRole button)
            return button;
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

    // tg's icon states of the human HUD's buttons (screen_objects.dm: pull, resist, rest, throw_catch, mov_intent, drop, swap_hand, floor_changer,
    // craft, language_menu, navigate, memories, area_creator, sleep; their update_icon_state's states too)
    private static LiveHudRole? ButtonRole(string state) => state switch
    {
        "pull" or "pull0" or "act_resist" or "act_rest" or "act_rest_on" or "act_throw" or "act_throw_on" or "running" or "walking" or "act_drop" or "act_swap"
            or "floor_change" or "floor_change_v" => LiveHudRole.KeyButton,
        "craft" => LiveHudRole.Crafting,
        "talk_wheel" => LiveHudRole.Language,
        "navigate" => LiveHudRole.Navigate,
        "memories" => LiveHudRole.Memories,
        "area_edit" => LiveHudRole.AreaCreator,
        "act_sleep" or "act_sleep_on" => LiveHudRole.Sleep,
        _ => null,
    };

    /// <summary>
    /// Whether an entry drawn in the role <paramref name="placed"/> (<see cref="Placed"/>) is drawn and takes clicks now: tg's zone selector only while
    /// the cursor is free (cursor mode, Tab), where any zone, the eyes, mouth and groin included (tg's surgeries, a ctrl-click on the doll), is still
    /// chosen on it (looking aims at them too since package T, slice T4a, and the selector shows the zone the server selected); slice E: on the human
    /// HUD tg's memories button and area creator only then too, and a hidden entry never (no picture, no pick, no tooltip); every other entry always.
    /// </summary>
    public static bool Shown(LiveHudRole placed, bool cursorMode) => placed switch
    {
        LiveHudRole.ZoneSelector or LiveHudRole.Memories or LiveHudRole.AreaCreator => cursorMode,
        LiveHudRole.Hidden => false,
        _ => true,
    };

    /// <summary>
    /// The role an entry is drawn in: the hands, the bottom slots and the alerts only on the human HUD (<paramref name="humanHud"/>: its inventory
    /// toggle is on the screen); the alien's, a basic mob's and the other HUDs keep tg's places, the alerts tg's column (ui-overhaul.md section 6).
    /// Slice D: tg's health and mood indicators are re-placed into the vitals stack, and slice E's status chips (hunger, stamina, internals, suit
    /// cell, another status display, sleep) are chips, only with the full stack (<paramref name="vitals"/>, <see cref="LiveHudLayout.Vitals"/>);
    /// with the reduced one they stay at tg's places. Slice E: on the human HUD tg's buttons that a key does and its inventory toggle are hidden.
    /// </summary>
    public static LiveHudRole Placed(LiveHudRole role, bool humanHud, bool vitals = false) => role switch
    {
        LiveHudRole.LeftHand or LiveHudRole.RightHand or LiveHudRole.BottomSlot or LiveHudRole.Alert when !humanHud => LiveHudRole.Tg,
        LiveHudRole.Health or LiveHudRole.Mood when !humanHud || !vitals => LiveHudRole.Tg,
        LiveHudRole.Hunger or LiveHudRole.Stamina or LiveHudRole.Internals or LiveHudRole.SpaceSuit or LiveHudRole.StatusOther or LiveHudRole.Sleep
            => humanHud && vitals ? LiveHudRole.Alert : LiveHudRole.Tg,
        LiveHudRole.KeyButton or LiveHudRole.Crafting or LiveHudRole.Language or LiveHudRole.Navigate => humanHud ? LiveHudRole.Hidden : LiveHudRole.Tg,
        LiveHudRole.InventoryToggle when humanHud => LiveHudRole.Hidden,
        LiveHudRole.Memories or LiveHudRole.AreaCreator when !humanHud => LiveHudRole.Tg,
        _ => role,
    };

    // tg's bottom-right places (__DEFINES/hud.dm): the zone selector's corner, the move intent's, the combat toggle's and the floor changer's
    private static readonly ScreenLocSpec ZoneselLoc = Loc("EAST-1:28,SOUTH:5"), MoviLoc = Loc("EAST-2:26,SOUTH:5"), ActiLoc = Loc("EAST-3:24,SOUTH:5"),
        FloorChangerLoc = Loc("EAST-4:22,SOUTH:5");

    /// <summary>
    /// Slice E: the place on the human HUD of an entry drawn in the role <paramref name="placed"/>, where it is not tg's own (null: tg's): tg's combat
    /// toggle in the corner tile (<c>ui_zonesel</c>), and under Tab in one row left of it tg's zone selector at <c>ui_movi</c>, the memories button at
    /// <c>ui_acti</c> and the area creator at <c>ui_human_floor_changer</c>.
    /// </summary>
    public static ScreenLocSpec? HumanPlace(LiveHudRole placed) => placed switch
    {
        LiveHudRole.CombatToggle => ZoneselLoc,
        LiveHudRole.ZoneSelector => MoviLoc,
        LiveHudRole.Memories => ActiLoc,
        LiveHudRole.AreaCreator => FloorChangerLoc,
        _ => null,
    };

    /// <summary>The role of tg's button whose click the key <paramref name="action"/> sends (slice E: crafting, the language menu, navigate); null for any other action.</summary>
    public static LiveHudRole? ButtonOf(PlayAction action) => action switch
    {
        PlayAction.CraftingMenu => LiveHudRole.Crafting,
        PlayAction.LanguageMenu => LiveHudRole.Language,
        PlayAction.Navigate => LiveHudRole.Navigate,
        _ => null,
    };

    /// <summary>
    /// The screen entry the key <paramref name="action"/> clicks (<see cref="ButtonOf"/>): the first of the player's entries in that role, by
    /// <see cref="RoleOf"/>, whether drawn or not; 0 when the HUD has none (another mob's) or the action clicks no button.
    /// </summary>
    public static long ButtonEntry(ReplicaWorld replica, PlayAction action)
    {
        if (ButtonOf(action) is not LiveHudRole role)
            return 0;
        long found = 0;
        foreach (ScreenEntry entry in replica.Screen.Values)
        {
            if ((found == 0 || entry.Id < found) && RoleOf(replica, entry) == role)
                found = entry.Id;
        }
        return found;
    }

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
    /// The order of the chips (slices D and E): tg's alerts in tg's order (<see cref="AlertOrder"/>), then the hunger, stamina, internals and suit
    /// cell indicators, then any other entry of tg's right status column, tg's top to bottom, then the sleep button.
    /// </summary>
    public static int ChipOrder(LiveHudRole role, in ScreenLocSpec spec) => role switch
    {
        LiveHudRole.Hunger => 1000,
        LiveHudRole.Stamina => 1001,
        LiveHudRole.Internals => 1002,
        LiveHudRole.SpaceSuit => 1003,
        LiveHudRole.StatusOther => 3000 - (spec.Y.Tile * (int)HudMetrics.IconPixels + spec.Y.Pixels), // higher on the screen first
        LiveHudRole.Sleep => 10000,
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
    /// draws something visible now (a chip): tg hides one by clearing its icon state (screen_objects.dm update_spacesuit_hud_icon:
    /// SPACESUIT_NO_ICON), by a state its icon file does not have ("spacesuit_0", which BYOND draws as nothing) or by making it transparent (the
    /// hunger bar keeps "hungerbar" at alpha 0 while fed). <paramref name="stateExists"/> says whether an icon file (its resource path) has a state.
    /// </summary>
    public static bool IndicatorShown(ReplicaWorld replica, ScreenEntry entry, Func<string, string, bool> stateExists)
    {
        if (!replica.TryGetAppearance(entry.Appearance, out AppearanceEntry app) || app.Alpha == 0 || replica.ResolveString(app.IconState) is not { Length: > 0 } state)
            return false;
        return (replica.ResolveResource(app.Icon) ?? replica.ResolveString(app.Icon)) is not string icon || stateExists(icon, state);
    }

    /// <summary>
    /// Whether an entry of <paramref name="role"/> drawn as a chip (<see cref="Placed"/> gave <see cref="LiveHudRole.Alert"/>) gets one now: tg's
    /// alerts always; the status indicators while tg draws something (<see cref="IndicatorShown"/>), and the stamina indicator only while stamina
    /// is down (slice E): not at tg's <c>stamina_full</c> (living.dm:1458-1470, which tg sets after its first update with
    /// nothing lost), only at <c>stamina_1</c> to <c>stamina_5</c>, <c>stamina_crit</c> and <c>stamina_dead</c>. At tg's places (another HUD, the
    /// reduced arrangement) tg's entry is drawn in every state, as tg draws it.
    /// </summary>
    public static bool ChipShown(LiveHudRole role, ReplicaWorld replica, ScreenEntry entry, Func<string, string, bool> stateExists)
    {
        if (role == LiveHudRole.Alert)
            return true;
        if (!IndicatorShown(replica, entry, stateExists))
            return false;
        return role != LiveHudRole.Stamina || !(replica.TryGetAppearance(entry.Appearance, out AppearanceEntry app) && replica.ResolveString(app.IconState) == "stamina_full");
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
    /// fits there: wholly right of the window and on the screen, and its smaller hand box at <see cref="UiTokens.HandMinPixels"/> or more (slice E:
    /// tg's drop and swap buttons are no longer in its way); otherwise it is exactly where it is with the chat window closed, and a window over it
    /// covers it as a window covers tg's HUD (D282). The gap and the boxes keep their places when the active hand changes.
    /// Slice D: with the human HUD's vitals <paramref name="column"/> (<see cref="Layout"/>) the held-item hint strip's place over the row for a
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
            if (beside.Left.Left >= chatRight && UiTokens.HandInactive * beside.Unit >= UiTokens.HandMinPixels)
                (row, besideChat) = (Row(metrics, leftActive, rightActive, chatRight), chatRight);
        }
        // slice D: the held-item hint strip over the row, with the vitals given (the human HUD)
        if (column.Doll.Width <= 0f)
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

    /// <summary>
    /// What the HUD pick gets for the doll entry <paramref name="id"/>: the doll's own 32 x 32 icon square in its box, with no picture, so a point
    /// on it is tg's icon pixel whatever size its zones' pictures are (a wound outline grows them).
    /// </summary>
    public static HudDrawn DollDrawn(long id, in LiveHudLayout layout) => new(id, ScreenLoc.FitIcon(layout.Doll, 32, 32, 0, 0), null, 0, 0);

    /// <summary>Where a column of text left of the vitals (the zone labels, the prompts) ends, screen pixels from the left.</summary>
    public static float LeftOfVitals(in LiveHudLayout layout, in HudMetrics metrics) => layout.Left - UiTokens.DollPanelGap * UiTokens.Unit(metrics);

    // tg's lowest row of the bottom-right buttons that stand under the reduced doll's column (ui_above_movement_top, ui_human_area): it stays above it
    private static readonly ScreenLocSpec BottomRow = ScreenLoc.TryParse("EAST-2:26,SOUTH+1:24", out ScreenLocSpec spec) ? spec : default;

    /// <summary>
    /// The vitals. On the human HUD (<paramref name="human"/>; slice E): the full stack (<see cref="VitalsColumn"/>: the
    /// doll, tg's health indicator and mood face, bare against the screen's right edge over tg's combat toggle) wherever it fits at this screen and
    /// scale; else the reduced arrangement, the doll alone, its right edge <see cref="UiTokens.DollPanelGap"/> left of tg's right-hand column (the
    /// tiles at <c>EAST-1</c>, where tg's status indicators then stay), centred vertically and kept above tg's bottom-right buttons and under the
    /// version tag and the ping chip (<see cref="UiTokens.CardTopRoom"/>), the doll made smaller as far as needed. On the other HUDs the same
    /// reduced arrangement, free of the top room. The doll is a whole multiple of tg's pixels (<see cref="UiTokens.DollPixels"/> at the reference
    /// size); everything else scales with <see cref="UiTokens.Unit"/> (the options' HUD scale).
    /// </summary>
    public static LiveHudLayout Layout(in HudMetrics metrics, bool human = false)
    {
        if (human && VitalsColumn(metrics) is LiveHudLayout vitals)
            return vitals;
        float u = UiTokens.Unit(metrics);
        float right = metrics.Width - 2 * metrics.Tile - UiTokens.DollPanelGap * u; // EAST-1's tile starts two tiles from the right edge
        float floor = ScreenLoc.Place(BottomRow, metrics, 32, 32, 0, 0, out _).Top - UiTokens.DollPanelGap * u;
        float ceiling = human ? UiTokens.CardTopRoom * u : 0f; // where the doll may start from the top
        int k = DollScaleAt(u);
        while (k > 1 && HudMetrics.IconPixels * k > floor - ceiling)
            k--;
        // never past the screen's left edge (a portrait screen at a large scale), on the human HUD with room for a column of the smallest chips left of it
        float room = MathF.Max(1f, MathF.Floor(right - (human ? MathF.Round(UiTokens.AlertStackGap * u) + Math.Min(MathF.Round(UiTokens.AlertChip * u), UiTokens.AlertChipMinPixels) : 0f)));
        while (k > 1 && HudMetrics.IconPixels * k > room)
            k--;
        float side = Math.Min(HudMetrics.IconPixels * k, room);
        float top = MathF.Round(Math.Max(Math.Min(ceiling, Math.Max(0f, floor - side)), Math.Min((metrics.Height - side) / 2f, floor - side)));
        return new LiveHudLayout(new HudRect(MathF.Floor(right - side), top, side, side), k);
    }

    // the doll's whole tg pixels per screen pixel at the reference unit's size (3 at 1080 lines and 100 %)
    private static int DollScaleAt(float u) => Math.Max(1, (int)Math.Round(UiTokens.DollPixels * u, MidpointRounding.AwayFromZero));

    // tg's sleep button (ui_above_throw), at tg's place with the reduced arrangement
    private static readonly ScreenLocSpec SleepLoc = Loc("EAST-1:28,SOUTH+1:41");

    /// <summary>
    /// The full vitals stack (slice E), or null where it does not fit: three pictures, no box, centred on one vertical axis
    /// (<see cref="Axis"/>) placed so that the widest, the doll's square, touches the screen's right edge (tg's pictures carry their own transparent
    /// margin), the others centred on it at whole pixels, <see cref="UiTokens.VitalsMargin"/> apart, top to bottom the doll at
    /// its whole-pixel scale S and tg's health indicator and mood face at S - 1 (at least 1), anchored at the bottom: the mood face's bottom
    /// <see cref="UiTokens.VitalsMargin"/> over the top of tg's combat toggle in the corner (<c>ui_zonesel</c>). It keeps under the version tag and the
    /// ping and FPS chip (<see cref="UiTokens.CardTopRoom"/>): S steps down while it does not fit, never for the chips; null when even S = 1 does not.
    /// It depends on the screen and the scale alone.
    /// </summary>
    public static LiveHudLayout? VitalsColumn(in HudMetrics metrics)
    {
        float u = UiTokens.Unit(metrics), gap = MathF.Round(UiTokens.VitalsMargin * u);
        float width = metrics.Width;
        // whole pixels (tg's combat toggle can stand at a fraction of one), so every square of the stack is whole
        float floor = MathF.Floor(ScreenLoc.Place(ZoneselLoc, metrics, 32, 32, 0, 0, out _).Top - gap);
        float ceiling = MathF.Ceiling(UiTokens.CardTopRoom * u);
        const float icon = HudMetrics.IconPixels;
        for (int s = DollScaleAt(u); s >= 1; s--)
        {
            int cell = Math.Max(1, s - 1);
            if (floor - icon * (s + 2 * cell) - 2 * gap < ceiling)
                continue;
            // the doll's square (the widest) against the edge, the axis its middle; the others centred on it (a square of 32 tg pixels at a whole
            // scale is an even number of screen pixels, so their halves are whole)
            float axis = MathF.Floor(width) - icon * s / 2f;
            var mood = new HudRect(axis - icon * cell / 2f, floor - icon * cell, icon * cell, icon * cell);
            var health = mood with { Top = mood.Top - gap - icon * cell };
            var doll = new HudRect(axis - icon * s / 2f, health.Top - gap - icon * s, icon * s, icon * s);
            return new LiveHudLayout(doll, s, true, health, mood, cell);
        }
        return null;
    }

    /// <summary>The vertical axis the full stack's pictures and the chips above them are centred on: the doll's middle, screen pixels from the left.</summary>
    public static float Axis(in LiveHudLayout layout) => layout.Doll.Left + layout.Doll.Width / 2f;

    /// <summary>
    /// The square that the 32 x 32 icon of the re-placed vitals entry of <paramref name="role"/> fills (null for any other role or without the full
    /// stack): tg's health indicator's or tg's mood face's.
    /// </summary>
    public static HudRect? VitalSquare(LiveHudRole role, in LiveHudLayout layout) => !layout.Vitals ? null : role switch
    {
        LiveHudRole.Health => layout.Health,
        LiveHudRole.Mood => layout.Mood,
        _ => null,
    };

    // ---- the held-item hint strip above the hands ----

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
        float width = 2 * UiTokens.HintPad * unit + HintPicture(MathF.Round(UiTokens.HintHeight * unit));
        foreach ((float cap, float verb) in entries)
            width += UiTokens.HintGap * unit + cap + UiTokens.HintCapGap * unit + verb;
        return width;
    }

    // tg's human HUD entries still drawn at the bottom right (slice E: the combat toggle in the corner; under Tab the zone selector, the memories
    // button and the area creator in the row left of it) and the right column's status icons, which stay at tg's places with the reduced
    // arrangement, the sleep button among them: the hint strip keeps clear of all of them, under Tab or not
    private static readonly ScreenLocSpec[] HintClear =
    {
        ZoneselLoc, MoviLoc, ActiLoc, FloorChangerLoc, SleepLoc,
        HealthLoc, MoodLoc, HungerLoc, Loc("EAST-1:28,CENTER-3:14"), Loc("EAST-1:28,CENTER+1:21"), Loc("EAST-1:28,CENTER-4:14"),
    };

    /// <summary>
    /// Where the held-item hint strip goes over the hands' row <paramref name="row"/>, and the share of its size it is drawn at (<paramref name="share"/>;
    /// an empty rectangle where it is left out): its natural width <paramref name="natural"/> (<see cref="HintWidth"/>; 0: none), centred on the
    /// hotbar's gap, its bottom <see cref="UiTokens.HintRise"/> over the row's top line (the taller, active box; slice E). One wider than the row
    /// (both boxes at the active size) is made smaller as one to the row's width, its text to no less than
    /// <see cref="UiTokens.HintLeastShare"/> of the prompts' size; below that it is left out. It is shown only where it is wholly on the screen and
    /// clear of the chat window when the row stands right of it (<paramref name="chatRight"/>), tg's entries still drawn, the vitals
    /// (<paramref name="column"/>), every chip it can show (<see cref="MaxChips"/>) and the widest prompt block beside the crosshair. It never moves
    /// the row and does not change with a swap: it depends on the screen, the scale and the row's place alone (and the bound keys it names).
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
        float width = MathF.Floor(natural * fit), height = MathF.Round(UiTokens.HintHeight * u * fit);
        // at the whole pixel above the active box (the row's place can end at a fraction of one), so the strip and its picture stand at whole pixels
        float bottom = MathF.Floor(row.Hotbar.Bottom - active) - MathF.Round(UiTokens.HintRise * u);
        var hint = new HudRect(MathF.Round(row.Hotbar.Left + row.Hotbar.Width / 2f - width / 2f), bottom - height, width, height);
        if (hint.Left < 0f || hint.Top < 0f || hint.Right > metrics.Width || hint.Bottom > metrics.Height || hint.Left < chatRight)
            return default;
        foreach (ScreenLocSpec spec in HintClear)
        {
            if (Over(hint, ScreenLoc.Place(spec, metrics, 32, 32, 0, 0, out _)))
                return default;
        }
        List<HudRect> chips = AlertStack(metrics, MaxChips, column);
        foreach (HudRect ours in new List<HudRect>(chips) { column.Doll, column.Health, column.Mood })
        {
            if (Over(hint, ours))
                return default;
        }
        float edge = Math.Min(LeftOfVitals(column, metrics), LeftOfAlerts(chips, metrics));
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

    /// <summary>
    /// The most chips the layouts make room for: tg's five alerts and slice E's status chips (hunger, stamina, internals, suit cell, sleep) with two
    /// more status displays of another body.
    /// </summary>
    public const int MaxChips = 12;

    // ---- slice D: tg's alerts as chips by the vitals, above them or left of them ----

    /// <summary>
    /// Where an alert stands in tg's order: its index in tg's column (alert.dm get_ui_alert_placement, read from the screen_loc tg gave it; alert 1
    /// first), the alerts without one last.
    /// </summary>
    public static int AlertOrder(in ScreenLocSpec spec) => ScreenLoc.AlertSlot(spec) is int slot and > 0 ? slot : int.MaxValue;

    /// <summary>
    /// Whether the alert chips stand above the doll at this screen and scale: when five chips at full size (<see cref="UiTokens.AlertChip"/>,
    /// <see cref="UiTokens.AlertChipGap"/> apart, <see cref="UiTokens.AlertStackGap"/> over the doll) fit between the doll's top and the version
    /// tag and the ping and FPS chip (<see cref="UiTokens.CardTopRoom"/>); else they stand left of the vitals. It never depends on how
    /// many alerts show, so no chip moves to the other place when one comes or goes.
    /// </summary>
    public static bool AlertsAbove(in HudMetrics metrics, in LiveHudLayout layout)
    {
        float u = UiTokens.Unit(metrics), side = MathF.Round(UiTokens.AlertChip * u), gap = MathF.Round(UiTokens.AlertChipGap * u);
        return layout.Stack.Top - MathF.Round(UiTokens.AlertStackGap * u) - (MaxAlerts * side + (MaxAlerts - 1) * gap) >= MathF.Ceiling(UiTokens.CardTopRoom * u);
    }

    // tg shows five alerts at most (alert.dm get_ui_alert_placement)
    private const int MaxAlerts = 5;

    /// <summary>
    /// The chips of <paramref name="count"/> alerts, square, in tg's order from top to bottom (alert 1 at the top), by the vitals
    /// (<paramref name="layout"/>). Above the doll (<see cref="AlertsAbove"/>): one column at full size, centred on the full stack's <see cref="Axis"/> (right-aligned with the reduced doll), the lowest
    /// <see cref="UiTokens.AlertStackGap"/> over the doll's top, holding the first chips (as many as fit), and the chips after them (the status
    /// chips, say) in further columns to the left, each read top down and standing on the doll. Else left of the pictures: a column whose right edge
    /// is that gap left of the doll (the widest picture), its first chip level with the doll's top (or under the top room), running down beside
    /// them, above tg's bottom-right row and the hands' row, and above the widest prompt block where that stands under it; when five full-size
    /// chips do not fit there they are all made smaller, down to <see cref="UiTokens.AlertChipMinPixels"/> (or their full size when that is
    /// smaller), and when they still do not fit the column goes on in further columns to the left, top down again. The size and the places there
    /// are those of five chips whatever the count, so a chip never moves when another comes or goes. Empty without the vitals.
    /// </summary>
    public static List<HudRect> AlertStack(in HudMetrics metrics, int count, in LiveHudLayout layout)
    {
        var chips = new List<HudRect>(Math.Max(0, count));
        if (count <= 0 || layout.Doll.Width <= 0f)
            return chips;
        float u = UiTokens.Unit(metrics), stackGap = MathF.Round(UiTokens.AlertStackGap * u);
        float full = MathF.Round(UiTokens.AlertChip * u), fullGap = MathF.Round(UiTokens.AlertChipGap * u);
        HudRect stack = layout.Stack;
        if (AlertsAbove(metrics, layout))
        {
            float bottom = stack.Top - stackGap;
            // the first chips in the column at the edge, as many as fit (five at least); the chips after them go on in the next column to the left
            int above = Math.Max(MaxAlerts, (int)MathF.Floor((bottom - MathF.Ceiling(UiTokens.CardTopRoom * u) + fullGap) / (full + fullGap)));
            for (int i = 0; i < count; i++)
            {
                int column = i / above, row = i % above, inColumn = Math.Min(above, count - column * above);
                float columnTop = bottom - inColumn * full - (inColumn - 1) * fullGap; // each column stands on the doll
                // slice E: the first column centred on the full stack's axis (at a whole pixel); with the reduced doll right-aligned with it
                float first = layout.Vitals ? MathF.Round(Axis(layout) - full / 2f) : stack.Right - full;
                chips.Add(new HudRect(first - column * (full + fullGap), columnTop + row * (full + fullGap), full, full));
            }
            return chips;
        }
        float top = Math.Max(stack.Top, MathF.Ceiling(UiTokens.CardTopRoom * u));
        float right = stack.Left - stackGap; // the doll is the widest picture
        float handsRow = metrics.Height - (UiTokens.HotbarMargin + UiTokens.HandActive) * u; // the hands' row at its full size: it is only ever smaller
        // above tg's bottom-right row still drawn (slice E: the combat toggle and, under Tab, the three entries left of it); with the reduced
        // arrangement above tg's rows of buttons at their places as before
        float buttons = ScreenLoc.Place(layout.Vitals ? MoviLoc : BottomRow, metrics, 32, 32, 0, 0, out _).Top;
        float floor = Math.Min(buttons, handsRow) - stackGap;
        HudRect sleep = ScreenLoc.Place(SleepLoc, metrics, 32, 32, 0, 0, out _);
        if (!layout.Vitals && sleep.Left < right) // tg's sleep button at its place, higher than that row, reaches under the column where tg's tiles are wide (a 4K screen at a large scale)
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
    /// Section 11: how a chip shows the visible art <paramref name="art"/> (tg pixels, its left
    /// and top from the top left corner of tg's 32 x 32 icon square): scaled uniformly, by any factor, so that its larger side fills the chip
    /// less <see cref="UiTokens.AlertChipArtMargin"/> on each side exactly, and centred in the chip both ways. <c>Picture</c> is where the art
    /// lands on the screen: at whole pixels, its smaller side rounded (the aspect kept within a pixel), always inside the chip.
    /// </summary>
    public static (float Scale, HudRect Picture) ChipArt(HudRect chip, HudRect art)
    {
        float margin = MathF.Round(UiTokens.AlertChipArtMargin * chip.Width / UiTokens.AlertChip), room = Math.Max(1f, chip.Width - 2 * margin);
        float scale = room / Math.Max(art.Width, art.Height);
        float width = Math.Max(1f, MathF.Round(art.Width * scale)), height = Math.Max(1f, MathF.Round(art.Height * scale));
        return (scale, new HudRect(chip.Left + MathF.Floor((chip.Width - width) / 2f), chip.Top + MathF.Floor((chip.Height - height) / 2f), width, height));
    }

    /// <summary>
    /// Where a chip's composed picture (<paramref name="pixelWidth"/> x <paramref name="pixelHeight"/> tg pixels, tg's icon square's lower left
    /// corner at (<paramref name="originX"/>, <paramref name="originY"/>), as for <see cref="ScreenLoc.FitIcon"/>) is drawn so that its visible
    /// art <paramref name="art"/> lands exactly on <see cref="ChipArt"/>'s picture; without art (nothing opaque, or the icon could not be read)
    /// tg's whole square fitted in the chip as before (<see cref="AlertPicture"/>).
    /// </summary>
    public static HudRect ChipPicture(HudRect chip, HudRect? art, int pixelWidth, int pixelHeight, int originX, int originY)
    {
        if (art is not HudRect a || a.Width <= 0f || a.Height <= 0f)
            return ScreenLoc.FitIcon(AlertPicture(chip), pixelWidth, pixelHeight, originX, originY);
        HudRect picture = ChipArt(chip, a).Picture;
        float kx = picture.Width / a.Width, ky = picture.Height / a.Height;
        float artX = a.Left + originX, artY = a.Top + (pixelHeight - originY - HudMetrics.IconPixels); // the art's corner in the composed picture
        return new HudRect(picture.Left - artX * kx, picture.Top - artY * ky, pixelWidth * kx, pixelHeight * ky);
    }

    /// <summary>How far tg's outline filters on <paramref name="look"/> reach out of its art (the starving hunger bar's pulsing outline), 0 without one.</summary>
    public static float OutlineReach(AppearanceEntry look)
    {
        float reach = 0f;
        if (look?.Filters != null)
            foreach (FilterEntry f in look.Filters)
                if (f.Kind == FilterKind.Outline)
                    reach = Math.Max(reach, f.Size);
        return reach;
    }

    /// <summary>
    /// The visible art of the screen appearance <paramref name="appearance"/> as the HUD composes it (facing south, its own pixel offset kept):
    /// the smallest rectangle holding every pixel with alpha above zero over every picture of its animation, its icon state's frames with its
    /// overlays' and underlays', in tg pixels from the top left corner of its 32 x 32 icon square (<see cref="ChipArt"/>). The entry is composed
    /// opaque (a hunger bar fading in counts whole), and tg's outline filters on it at their full strength (the starving hunger bar's outline
    /// pulses from alpha 0 to 200: counted at its full reach, so the picture does not move as it pulses). Null when nothing is drawn or nothing
    /// could be composed. False while an icon is still loading: nothing is known yet, ask again on a later frame.
    /// </summary>
    public static bool TryChipArtBounds(AppearanceCompositor compositor, IAppearanceSource tables, TableRef appearance, out HudRect? art)
    {
        art = null;
        int misses = compositor.Library.PendingMisses;
        Dictionary<AppearanceEntry, IReadOnlyList<FilterEntry>> outlines = null;
        if (tables.TryGetAppearance(appearance, out AppearanceEntry root) && OutlineReach(root) > 0f)
        {
            var full = root.Filters.ConvertAll(f =>
            {
                if (f.Kind != FilterKind.Outline)
                    return f;
                FilterEntry strong = f.Copy();
                strong.Alpha = 255f;
                return strong;
            });
            outlines = new Dictionary<AppearanceEntry, IReadOnlyList<FilterEntry>>(ReferenceEqualityComparer.Instance) { [root] = full };
        }
        AnimationTimeline timeline = AnimationTimeline.Of(compositor.AnimatedStates(tables, appearance, ByondDir.South, rootOffset: true), once: false);
        int left = int.MaxValue, top = int.MaxValue, right = int.MinValue, bottom = int.MinValue;
        for (int i = 0; i < (timeline?.Count ?? 1); i++)
        {
            if (!compositor.TryCompose(tables, appearance, ByondDir.South, timeline?.SampleAt(i) ?? 0, out ComposedRaster picture, rootOffset: true, rootOpaque: true, outlines))
                continue;
            Raster image = picture.Image;
            int squareTop = image.Height - picture.OriginY - AppearanceCompositor.DefaultIconSize;
            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    if (image.Pixels[(y * image.Width + x) * 4 + 3] == 0)
                        continue;
                    left = Math.Min(left, x - picture.OriginX);
                    right = Math.Max(right, x - picture.OriginX);
                    top = Math.Min(top, y - squareTop);
                    bottom = Math.Max(bottom, y - squareTop);
                }
            }
        }
        if (compositor.Library.PendingMisses != misses)
            return false;
        if (left <= right)
            art = new HudRect(left, top, right - left + 1, bottom - top + 1);
        return true;
    }

    /// <summary>
    /// The look a screen entry drawn in the role <paramref name="placed"/> (<see cref="Placed"/>) shows of its animate() chain: in a chip
    /// (<see cref="LiveHudRole.Alert"/>: tg's alerts and the status indicators on the human HUD) without moving, so its picture always stays in
    /// <see cref="AlertPicture"/> (tg throws a new alert in 32 pixels to the right, alert.dm:65-66, which would draw it over the doll beside the
    /// empty chip); nothing replaces that slide. Slice E: the hunger bar's chip (<paramref name="role"/>
    /// <see cref="LiveHudRole.Hunger"/>) is drawn at full opacity, not with tg's fade (screen_objects.dm:1237-1247, a second): the chip comes and
    /// goes with the fade's target, which is the entry's own alpha (animate() sets it at once, as BYOND does; <see cref="ChipShown"/>). At tg's
    /// places the look is tg's.
    /// </summary>
    public static HudLook ChipLook(LiveHudRole placed, in HudLook look, LiveHudRole role = LiveHudRole.Alert) => placed != LiveHudRole.Alert ? look
        : role == LiveHudRole.Hunger ? look with { MoveX = 0f, MoveY = 0f, Alpha = 1f } : look with { MoveX = 0f, MoveY = 0f };

    /// <summary>What the HUD pick gets for the alert <paramref name="id"/> drawn in <paramref name="chip"/>: the whole chip, with no picture, so a click anywhere on it is tg's alert.</summary>
    public static HudDrawn AlertDrawn(long id, HudRect chip) => new(id, chip, null, 0, 0);
}
