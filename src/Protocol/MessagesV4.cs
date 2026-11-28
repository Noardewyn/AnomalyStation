namespace Anomaly.Protocol;

// Owner: the types and the meaning of every field are the M4a contracts step's (frozen, docs/ai/specs/m4a-ownership.md); their
// encoding is M4a P6's (Codec.V4.cs, docs/ai/specs/protocol.md section "Version 4"). Design basis: the M4a scope 2a, 2b, 2d,
// 2e, 2g, 2i; D124, D125, D127, D128, D131; docs/ai/specs/interaction.md.
//
// Version 4 adds to version 3 (MessagesV3.cs):
// - the target of a click or a hover: an entity, a turf by its coordinates or a screen object (TargetKind), with tg's
//   icon-x and icon-y (ActionMessage's version-4 members, MessagesV2.cs);
// - the actions Keybinding (tg's keybinding datums: drop, quick equip, swap hands, activate in-hand, throw mode, select
//   hand) and Hover (tg's MouseEntered, which feeds the screentips);
// - the server's ContextHints, tg's screentip context of the hovered target with the held item (D125's radial);
// - the HUD group of a screen entry (ScreenEntry.Group: tg's hud_used.screen_groups, D131's windows);
// - the private images of the own client (tg's client.images: the progress bar, D128), attached to an atom;
// - the entity flags byte (NetField.Flags), whose first bit is Thrown (D127's arc and spin).

/// <summary>What a click or a hover is on (<see cref="ActionMessage.TargetKind"/>, interaction.md section 2).</summary>
public enum TargetKind : byte
{
    /// <summary>No target (an action that has none). A version-3 click (kind None with a <see cref="ActionMessage.Target"/> above 0) is an entity click: <see cref="ActionMessage.EffectiveTargetKind"/>.</summary>
    None = 0,

    /// <summary>An entity by its world id (<see cref="ActionMessage.Target"/>): a movable the replicator shows (a billboard, a static prop, a door, a window or a table piece).</summary>
    Entity = 1,

    /// <summary>A turf by its coordinates (<see cref="ActionMessage.TurfX"/>, <see cref="ActionMessage.TurfY"/>, <see cref="ActionMessage.TurfZ"/>): a floor, wall or ceiling cell. The server resolves tg's <c>locate(x, y, z)</c>.</summary>
    Turf = 2,

    /// <summary>The independent upper boundary of a cell, including an empty construction location.</summary>
    Ceiling = 4,

    /// <summary>A screen object of the player's own <c>client.screen</c> by its id (<see cref="ActionMessage.Target"/>, a <see cref="ScreenEntry.Id"/>): a HUD button, a hand or slot, a storage cell or an item shown in an open storage.</summary>
    Screen = 3,
}

/// <summary>
/// The tg keybindings a client sends as <see cref="ActionKind.Keybinding"/> (tg's defaults in brackets; every key is
/// remappable on the client, D53, D70). Each code is one tg <c>/datum/keybinding</c> type, whose <c>can_use</c>, <c>down</c>
/// and <c>up</c> run on the server (<c>Anomaly.Core.Tg.ClientSeam.KeybindingType</c> is the table; a contract test keeps the two
/// equal). Rest, walk or run and combat mode keep their version-2 action kinds.
/// </summary>
public enum KeybindingCode : byte
{
    /// <summary>Never on the wire.</summary>
    None = 0,

    /// <summary><c>/datum/keybinding/mob/swap_hands</c> [X]: <c>swap_hand()</c>.</summary>
    SwapHands = 1,

    /// <summary><c>/datum/keybinding/mob/select_hand/right</c> [unbound].</summary>
    SelectRightHand = 2,

    /// <summary><c>/datum/keybinding/mob/select_hand/left</c> [unbound].</summary>
    SelectLeftHand = 3,

    /// <summary><c>/datum/keybinding/mob/activate_inhand</c> [Z]: <c>mode()</c>, the held item's <c>attack_self</c>.</summary>
    ActivateInhand = 4,

    /// <summary><c>/datum/keybinding/mob/drop_item</c> [Q]: <c>dropItemToGround</c> of the active hand's item.</summary>
    DropItem = 5,

    /// <summary><c>/datum/keybinding/human/quick_equip</c> [E]: <c>quick_equip()</c>.</summary>
    QuickEquip = 6,

    /// <summary><c>/datum/keybinding/living/toggle_throw_mode</c> [R]: <c>toggle_throw_mode()</c>.</summary>
    ToggleThrowMode = 7,

    /// <summary><c>/datum/keybinding/living/hold_throw_mode</c> [Space]: throw mode on while held (<c>down</c>), off on release (<c>up</c>, <see cref="ActionMessage.KeyUp"/>).</summary>
    HoldThrowMode = 8,

    /// <summary>Version 20 (pulling): <c>/datum/keybinding/mob/stop_pulling</c> [H, Delete]: <c>stop_pulling()</c>, or "You are not pulling anything."</summary>
    StopPulling = 9,

    /// <summary>Version 37 (M8z): <c>/datum/keybinding/living/resist</c> [B]: <c>resist()</c> on the press, the resist button lit while held.</summary>
    Resist = 10,

    /// <summary>
    /// Version 37 (M8z, D244): not a tg keybinding: the hidden verb <c>/mob/living/proc/mob_sleep</c> ("Sleep"), which tg's player runs from
    /// the verb panel or the command bar (M9's); the client sends it from its radial menu.
    /// </summary>
    VerbSleep = 11,

    /// <summary>Version 37 (M8z, D244): not a tg keybinding: the hidden verb <c>/mob/living/verb/succumb</c>, as <see cref="VerbSleep"/>.</summary>
    VerbSuccumb = 12,

    /// <summary>
    /// Version 39 (M9 slice B, D256): not a tg keybinding: the hidden verb <c>/client/verb/emote_panel</c> ("Emote Panel",
    /// <c>interface/interface.dm</c>), which opens tg's emote panel; the client sends it from the emote wheel's "More...".
    /// </summary>
    VerbEmotePanel = 13,

    /// <summary>
    /// Version 40 (M9 slice E, D251): not a tg keybinding: the big chat window's Message button, which opens the messenger of the PDA the
    /// player carries, as tg's chat link of a PDA message does (the messenger's Topic, <c>choice=open</c>).
    /// </summary>
    OpenMessenger = 14,

    /// <summary>Version 41 (M9 slice F): tg's <c>/datum/keybinding/living/give</c> [G]: offer the held item to the mobs beside.</summary>
    GiveItem = 15,

    /// <summary>Version 59 (gaps C2): <c>/datum/keybinding/living/view_pet_data</c> [Shift]: held, hovering a friendly pet opens its commands' radial; released, it closes.</summary>
    ViewPetCommands = 16,

    /// <summary>
    /// Version 83 (DV422): tg's <c>/datum/keybinding/movement/zlevel_upwards</c> [Space]: <c>mob.up()</c> for a mob that can change level
    /// (a ghost, an eye, the AI, a flyer, one on a ladder); for any other the key is tg's <see cref="HoldThrowMode"/>, press and release.
    /// </summary>
    ZLevelUp = 17,

    /// <summary>Version 83 (DV422): tg's <c>zlevel_downwards</c> [Ctrl]: <c>mob.down()</c> for a mob that can change level; nothing for any other.</summary>
    ZLevelDown = 18,

    /// <summary>Version 93 (DV585): the deathmatch scoreboard [K]: held, the kills and deaths table shows on the screen; released, it goes.</summary>
    Scoreboard = 19,

    /// <summary>Version 100 also (ui-overhaul.md section 12, slice 4E): <c>/datum/keybinding/human/quick_equip_belt/quick_equip_bag</c> [Shift+B]: <c>smart_equip_targeted</c> of the back slot.</summary>
    QuickEquipBag = 20,

    /// <summary>Version 100 also: <c>/datum/keybinding/human/quick_equip_belt</c> [Shift+E]: <c>smart_equip_targeted</c> of the belt slot.</summary>
    QuickEquipBelt = 21,

    /// <summary>Version 100 also: <c>quick_equip_belt/quick_equip_lpocket</c> [Shift+G]: <c>smart_equip_targeted</c> of the left pocket.</summary>
    QuickEquipLeftPocket = 22,

    /// <summary>Version 100 also: <c>quick_equip_belt/quick_equip_rpocket</c> [Shift+H]: <c>smart_equip_targeted</c> of the right pocket.</summary>
    QuickEquipRightPocket = 23,

    /// <summary>Version 100 also: <c>quick_equip_belt/quick_equip_suit_storage</c> [Shift+F]: <c>smart_equip_targeted</c> of the suit storage slot.</summary>
    QuickEquipSuitStorage = 24,

    /// <summary>The largest valid code; a decoder refuses any other.</summary>
    Last = QuickEquipSuitStorage,
}

/// <summary>
/// tg's HUD groups (<c>code/__DEFINES/hud.dm:26-38</c>, <c>hud_used.screen_groups</c>): the group of a screen entry
/// (<see cref="ScreenEntry.Group"/>). The client draws <see cref="Storage"/> entries in its native container window and
/// <see cref="ToggleableInventory"/> entries in its inventory panel (D131), every other entry at its <c>screen_loc</c>.
/// </summary>
public static class HudGroup
{
    /// <summary>In no group of the player's HUD (a screen object the HUD does not list in <c>screen_groups</c>).</summary>
    public const byte None = 0;

    /// <summary><c>HUD_GROUP_STATIC</c>: always present in the standard HUD (the hands, swap, drop, throw, the intents).</summary>
    public const byte Static = 1;

    /// <summary><c>HUD_GROUP_TOGGLEABLE_INVENTORY</c>: the slots the inventory toggle shows and hides.</summary>
    public const byte ToggleableInventory = 2;

    /// <summary><c>HUD_GROUP_INFO</c>: the info displays (health, the doll).</summary>
    public const byte Info = 3;

    /// <summary><c>HUD_GROUP_SCREEN_OVERLAYS</c>: the on-screen overlays shown whatever the HUD state (fullscreens).</summary>
    public const byte ScreenOverlays = 4;

    /// <summary><c>HUD_GROUP_HOTKEYS</c>: the hotkey buttons.</summary>
    public const byte Hotkeys = 5;

    /// <summary><c>HUD_GROUP_STORAGE</c>: an open storage's cells, its close button and the items it shows (tg's <c>show_contents</c>).</summary>
    public const byte Storage = 6;

    /// <summary>The largest valid group (tg's <c>SCREEN_GROUP_AMT</c>); a decoder refuses any other.</summary>
    public const byte Last = Storage;
}

/// <summary>
/// The body zone a click aims at (the owner's play test 2026-10-09, DV30): the body part of a carbon's picture under the
/// crosshair, which the server selects as tg's <c>zone_selected</c> before the click. Bits 4-6 of a click's modifiers byte, and since
/// version 100 bit 3 as the zone's high bit (<c>((mods &gt;&gt; 4) &amp; 7) | (mods &amp; 8)</c>), so every byte of version 97 keeps its meaning.
/// </summary>
public enum AimZone : byte
{
    None = 0,
    Head = 1,
    Chest = 2,
    LeftArm = 3,
    RightArm = 4,
    LeftLeg = 5,
    RightLeg = 6,

    /// <summary>Version 100 (package T, slice T4a): tg's <c>BODY_ZONE_PRECISE_EYES</c>, the eyes of the head looked at.</summary>
    Eyes = 7,

    /// <summary>Version 100: tg's <c>BODY_ZONE_PRECISE_MOUTH</c>, the mouth of the head looked at.</summary>
    Mouth = 8,

    /// <summary>Version 100: tg's <c>BODY_ZONE_PRECISE_GROIN</c>, the lowest rows of the chest looked at.</summary>
    Groin = 9,

    /// <summary>The largest valid zone; a decoder refuses any other.</summary>
    Last = Groin,
}

/// <summary>The bits of an entity's flags byte (<see cref="NetField.Flags"/>, <see cref="EntityState.Flags"/>).</summary>
[Flags]
public enum EntityFlags : byte
{
    None = 0,

    /// <summary>The entity is in flight: tg's <c>throwing</c> is set (a <c>/datum/thrownthing</c> moves it tile by tile). The client draws D127's arc and spin; the tiles are the server's.</summary>
    Thrown = 1,

    /// <summary>
    /// The entity lunged this tick (tg's <c>do_attack_animation</c>: 8 pixels toward the target over 1 ds and back over 2 ds,
    /// turned 15 degrees; the owner's play test 2026-10-09): set for the one tick of the attack, with the direction in
    /// <see cref="LungeDir"/>. The client plays tg's lunge when the bit rises.
    /// </summary>
    Lunge = 2,

    /// <summary>
    /// tg showed its attack visual on the entity this tick (a punch, a kick or an item's swing at it; the owner's play test
    /// 2026-10-09): set for the one tick. The client draws a burst of pixel particles when the bit rises.
    /// </summary>
    Hit = 4,

    /// <summary>
    /// Version 56: a dense obj a walking mob pushes (tg's living.dm PushAM: not anchored, move_resist within the default move_force), so the
    /// client predicts the push and the mob's step after it (OwnGuesses.OnPush). Set for as long as it holds, not polled.
    /// </summary>
    Pushable = 8,

    /// <summary>Bits 4-7: the BYOND direction of a <see cref="Lunge"/> (NORTH 1, SOUTH 2, EAST 4, WEST 8, or a diagonal).</summary>
    LungeDir = 0xF0,

    /// <summary>Every valid bit; a decoder refuses any other.</summary>
    All = Thrown | Lunge | Hit | Pushable | LungeDir,
}

/// <summary>
/// One private image of the own client (tg's <c>client.images</c>, D128: the <c>do_after</c> progress bar, later the cogbar
/// and balloon images), in the private block (<see cref="OwnBlock.ImagesAdded"/>). Only its owner receives it (D40).
/// </summary>
/// <param name="Id">The image's world id (an <c>/image</c> datum's <c>dm_id</c>), unique while it exists.</param>
/// <param name="Appearance">The image's appearance in the session's private (or the public) appearance table: tg's icon, icon_state (<c>prog_bar_NN</c>), layer, plane, pixel offsets and alpha.</param>
/// <param name="AttachedTo">The world id of the atom the image is attached to (its <c>loc</c>): an entity, a turf's cell is not used; 0 when the image hangs on nothing the client knows (then the client draws it under the crosshair).</param>
public readonly record struct ImageEntry(long Id, TableRef Appearance, long AttachedTo)
{
    // ---- protocol version 27 (docs/ai/specs/protocol.md section 36) ----

    /// <summary>
    /// Version 27: the world id of the atom the image hangs on (its <c>loc</c>) wherever that atom is, not only on the map: an
    /// air tank's volume bar hangs on the tank on the player's back (tg's <c>tanks.dm:96</c>), which <see cref="AttachedTo"/> leaves at 0;
    /// the client draws such a bar under the action button of that item (<see cref="ScreenEntry.ActionTarget"/>). 0: none.
    /// </summary>
    public long Holder { get; init; }

    /// <summary>Version 27: the name of the atom the image hangs on (tg's <c>name</c>, at most <see cref="Limits.MaxImageHolderNameBytes"/>
    /// bytes): the label of a progress bar ("airlock", "oxygen tank"). Null for none.</summary>
    public string HolderName { get; init; }

    /// <summary>
    /// Version 30 (DV75): the tile an image on a turf is drawn on (tg's <c>image(loc = get_turf(O))</c>, the T-ray scanner's pulse,
    /// t_scanner.dm:57): its x, y and z, all 0 for an image that hangs on an entity or nothing. The client draws such an image like a
    /// short-lived visual on that tile (<see cref="ReplicaWorld"/> makes it one).
    /// </summary>
    public int TurfX { get; init; }

    /// <summary>Version 30: the y of <see cref="TurfX"/>.</summary>
    public int TurfY { get; init; }

    /// <summary>Version 30: the z of <see cref="TurfX"/>.</summary>
    public int TurfZ { get; init; }

    /// <summary>
    /// Version 35 (M8 slice G, D187, DV114): BYOND's <c>image.override</c>. The client draws this image in place of the picture of the entity
    /// it hangs on (<see cref="AttachedTo"/>), for its holder alone: tg's delusions look like something else, an unseen effect is empty for all
    /// but one. Without it the image is drawn over the entity (the data HUD images).
    /// </summary>
    public bool Override { get; init; }
}

/// <summary>
/// One entry of tg's screentip context (<c>add_context</c>, <c>COMSIG_ATOM_REQUESTING_CONTEXT_FROM_ITEM</c>,
/// <c>COMSIG_ITEM_REQUESTING_CONTEXT_FOR_TARGET</c>): what the click of <see cref="Button"/> with <see cref="Modifiers"/>
/// does on the hovered target with the held item ("Open", "Weld shut", "Swipe ID"). tg's keys map as
/// <c>SCREENTIP_CONTEXT_LMB</c> = (Left, None), <c>RMB</c> = (Right, None), <c>SHIFT_LMB</c> = (Left, Shift), <c>CTRL_LMB</c>
/// = (Left, Ctrl), <c>CTRL_RMB</c> = (Right, Ctrl), <c>ALT_LMB</c> = (Left, Alt), <c>ALT_RMB</c> = (Right, Alt) and
/// <c>CTRL_SHIFT_LMB</c> = (Left, Ctrl | Shift) (<c>code/__DEFINES/screentips.dm</c>).
/// </summary>
/// <param name="Button">The mouse button.</param>
/// <param name="Modifiers">The held modifier keys.</param>
/// <param name="Label">tg's label, plain text (tg's <c>build_context</c> markup removed), at most <see cref="Limits.MaxContextLabelBytes"/> bytes.</param>
/// <param name="Action">Version 36: what the radial entry sends (a click for every tg entry; the port's Buckle and Stop pulling, D230).</param>
public readonly record struct ContextHint(ClickButton Button, ClickModifiers Modifiers, string Label, ContextHintAction Action = ContextHintAction.Click);

/// <summary>
/// Server to client, Control channel: tg's screentip for the target the player hovers (D125, interaction.md section 6):
/// the answer to a <see cref="ActionKind.Hover"/>, at most one per hover, sent when tg's <c>on_mouse_enter</c> runs for the
/// player (tg's <c>SSmouse_entered</c>, once per tick). It replaces tg's maptext composition of the screentip line
/// (presentation: a DV row): the client shows <see cref="Name"/> and the left and right labels at the crosshair and the
/// whole list in its radial (D49). An empty <see cref="Hints"/> with a name is tg's screentip without context.
/// </summary>
public sealed class ContextHintsMessage
{
    /// <summary>The hovered target, as the hover named it (<see cref="TargetKind.Entity"/>, <see cref="TargetKind.Turf"/> or <see cref="TargetKind.Screen"/>).</summary>
    public TargetKind TargetKind { get; set; }

    /// <summary>The entity or screen entry id (<see cref="TargetKind.Entity"/>, <see cref="TargetKind.Screen"/>), else 0.</summary>
    public long Target { get; set; }

    /// <summary>The turf's x (<see cref="TargetKind.Turf"/>), else 0.</summary>
    public int TurfX { get; set; }

    /// <summary>The turf's y.</summary>
    public int TurfY { get; set; }

    /// <summary>The turf's z.</summary>
    public int TurfZ { get; set; }

    /// <summary>tg's screentip name (<c>used_name</c>: the atom's <c>name</c>, or a mob's override), at most <see cref="Limits.MaxContextNameBytes"/> bytes; null when tg shows no screentip (screentips off, <c>NO_SCREENTIPS_1</c>).</summary>
    public string Name { get; set; }

    /// <summary>tg's context entries in tg's order of lines (LMB, RMB, Ctrl-LMB, Ctrl-RMB, Alt-LMB, Alt-RMB, Shift-LMB, Ctrl-Shift-LMB), at most <see cref="Limits.MaxContextHints"/>.</summary>
    public List<ContextHint> Hints { get; } = new();

    /// <summary>
    /// Version 100 also (ui-overhaul.md section 12, "The hints, improved"; DV697): tg's left-click label for this target with an EMPTY hand (what the
    /// world interact key does), sent only while the active hand holds an item and the key would act on the target; null for none.
    /// </summary>
    public string EmptyHand { get; set; }

    /// <summary>Version 100 also: the label of tg's activate in hand of the held item (tg's own screentip for it in hand, else "Use" where it has an <c>attack_self</c>); null for none.</summary>
    public string HeldUse { get; set; }

    /// <summary>Version 100 also: the label of tg's alt-click on the held item (tg's own screentip for it in hand, else "Alt use" where it has one); null for none.</summary>
    public string HeldAlt { get; set; }

    /// <summary>Version 100 also: tg's quick equip would put the held item somewhere (a slot or a storage).</summary>
    public bool HeldEquips { get; set; }

    /// <summary>Version 100 also: the target is a turf the empty hand's click moves the pulled thing onto (tg's <c>turf/attack_hand</c>, <c>Move_Pulled</c>).</summary>
    public bool MoveHere { get; set; }
}

/// <summary>Version 4 of <see cref="OwnBlock"/>: the private images (<c>client.images</c>).</summary>
public sealed partial class OwnBlock
{
    /// <summary>Version 4: images added to the client's <c>client.images</c> this tick (a Snapshot: every image), at most <see cref="Limits.MaxPrivateImages"/>.</summary>
    public List<ImageEntry> ImagesAdded { get; } = new();

    /// <summary>Version 4: images whose appearance or attachment changed this tick (the progress bar's <c>icon_state</c> each tick), at most <see cref="Limits.MaxPrivateImages"/>.</summary>
    public List<ImageEntry> ImagesChanged { get; } = new();

    /// <summary>Version 4: ids of images removed this tick, at most <see cref="Limits.MaxPrivateImages"/>.</summary>
    public List<long> ImagesRemoved { get; } = new();
}
