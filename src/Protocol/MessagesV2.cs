using Anomaly.Core.Motion;

namespace Anomaly.Protocol;

// Owner: the types and the meaning of every field are the M2 contracts step's (frozen, docs/ai/specs/m2-ownership.md);
// their encoding is M2 P5's (Codec, docs/ai/specs/protocol.md section for version 2). Design basis: M2 scope 2d, 2h, 2i; D40,
// D103-D107.
//
// Version 2 adds to M1's protocol (Messages.cs):
// - entity fields Pos, Yaw, Posture and Appearance (NetField), and a change mask that is a varuint;
// - the appearance, resource and string tables: public ones shared by every connection (new entries travel in the Delta
//   body, the whole tables in the Snapshot), and private ones per session for what only its owner may see (its HUD);
// - the per-connection private block of every Delta and Snapshot (OwnBlock): the own capsule's motion state after
//   lastInputSeq with its parameters, the server's input buffer depth, the client.screen entries;
// - MapGrid version 2 (each cell's turf appearance and solidity bit) and the cell changes of a Delta (M3);
// - MotionInput frames on the unreliable Input channel, each message repeating the last 4 frames;
// - actions on the Control channel, tagged with the seq of the frame whose tick applies them.

/// <summary>
/// A reference into one of the appearance, resource or string tables: an index (from 1; 0 is "none") and whether it is
/// in the connection's <b>private</b> table (D40: what only its owner may see, the HUD, never enters the public tables, so
/// no other session's bytes can reveal it) or the public one. On the wire a varuint <c>(Index &lt;&lt; 1) | Private</c>.
/// </summary>
/// <param name="Index">The entry's index in its table, from 1; 0 for none.</param>
/// <param name="Private">True for the session's private table.</param>
public readonly record struct TableRef(uint Index, bool Private)
{
    /// <summary>No entry.</summary>
    public static TableRef None => default;

    /// <summary>True for <see cref="None"/>.</summary>
    public bool IsNone => Index == 0;

    /// <summary>A public table's entry.</summary>
    public static TableRef Public(uint index) => new(index, false);
}

/// <summary>
/// One interned appearance (scope 2h; BYOND's own model: an appearance is immutable and interned, an atom points at one):
/// what the client composes into a billboard texture (M2 P7) or a HUD element (M2 P8). Built by the server's replicator
/// from an atom's builtin appearance vars and its overlays and underlays (Anomaly.Core.DmShim.IDmAppearance), read-only and
/// outside the world. Append-only for the run: an entry never changes; a changed look is a new entry.
/// </summary>
public sealed class AppearanceEntry
{
    /// <summary>The icon: a resource (<c>icons/mob/human/bodyparts_greyscale.dmi</c>, or a GAGS reference <c>gags:&lt;config type path&gt;|&lt;colours&gt;</c> that the client composes, D104), or none (an overlay then uses its parent's icon).</summary>
    public TableRef Icon { get; set; }

    /// <summary>The icon state: a string, or none (BYOND's null).</summary>
    public TableRef IconState { get; set; }

    /// <summary>BYOND <c>dir</c> (0: the parent's, for an overlay; the entity's own <see cref="NetField.Dir"/> otherwise wins for a top-level appearance).</summary>
    public byte Dir { get; set; }

    /// <summary>BYOND <c>color</c> as RGBA (<c>0xRRGGBBAA</c>); white (<c>0xFFFFFFFF</c>) is no tint. Ignored when <see cref="ColorMatrix"/> is set.</summary>
    public uint Rgba { get; set; } = 0xFFFFFFFF;

    /// <summary>BYOND's colour matrix, normalized to 20 floats (4 x 5, BYOND ref "color matrix"), or null.</summary>
    public float[] ColorMatrix { get; set; }

    /// <summary>BYOND <c>alpha</c>, 0 to 255.</summary>
    public byte Alpha { get; set; } = 255;

    /// <summary>BYOND <c>layer</c> (negative: <c>FLOAT_LAYER</c>-style, above the parent and ordered among the floats).</summary>
    public float Layer { get; set; }

    /// <summary>BYOND <c>plane</c>, only to order the few special planes (lighting and emissive planes are not ported, DV12); <c>FLOAT_PLANE</c> is the parent's.</summary>
    public short Plane { get; set; }

    /// <summary>BYOND <c>pixel_x</c> (32 pixels are a tile).</summary>
    public short PixelX { get; set; }

    /// <summary>BYOND <c>pixel_y</c>.</summary>
    public short PixelY { get; set; }

    /// <summary>BYOND <c>pixel_w</c>.</summary>
    public short PixelW { get; set; }

    /// <summary>BYOND <c>pixel_z</c>.</summary>
    public short PixelZ { get; set; }

    /// <summary>BYOND <c>transform</c>: the six components <c>a b c d e f</c> of the <c>/matrix</c> (tg's lying turn, <c>update_transform</c>), or null for none.</summary>
    public float[] Transform { get; set; }

    /// <summary>BYOND <c>appearance_flags</c> (the client reads <c>RESET_COLOR</c>, <c>RESET_ALPHA</c>, <c>RESET_TRANSFORM</c>, <c>KEEP_TOGETHER</c>, <c>KEEP_APART</c>).</summary>
    public ushort AppearanceFlags { get; set; }

    /// <summary>BYOND <c>blend_mode</c> (0: the parent's).</summary>
    public byte BlendMode { get; set; }

    private List<TableRef> _overlays;
    private List<TableRef> _underlays;

    /// <summary>The overlays, appearances drawn above, in BYOND's order (the client sorts floating layers as BYOND does). The list is made on first use, so a table of many plain entries costs no list each.</summary>
    public List<TableRef> Overlays => _overlays ??= new();

    /// <summary>The underlays, drawn below (made on first use, as <see cref="Overlays"/>).</summary>
    public List<TableRef> Underlays => _underlays ??= new();

    /// <summary>A copy of the entry with only the overlays <paramref name="keep"/> takes (a client drawing an overlay another way, as a model); the run's tables are not changed.</summary>
    public AppearanceEntry WithOverlays(Func<TableRef, bool> keep)
    {
        var copy = (AppearanceEntry)MemberwiseClone();
        copy._overlays = _overlays?.FindAll(o => keep(o));
        return copy;
    }

    /// <summary>The number of overlays, without making the list (a P5 addition).</summary>
    public int OverlayCount => _overlays?.Count ?? 0;

    /// <summary>The number of underlays, without making the list (a P5 addition).</summary>
    public int UnderlayCount => _underlays?.Count ?? 0;

    // ---- protocol version 18 (HUD maptext: docs/ai/specs/protocol.md section 27) ----

    /// <summary>
    /// Version 18: BYOND <c>maptext</c>, the text drawn on the thing (a string table entry holding tg's small HTML subset, <c>MAPTEXT(...)</c>),
    /// or none. Only the session's private appearances (its HUD) carry it; the public tables never do (the world's maptext is M9).
    /// </summary>
    public TableRef Maptext { get; set; }

    /// <summary>Version 18: BYOND <c>maptext_width</c> in pixels (default <see cref="DefaultMaptextSize"/>); meaningful with <see cref="Maptext"/>.</summary>
    public short MaptextWidth { get; set; } = DefaultMaptextSize;

    /// <summary>Version 18: BYOND <c>maptext_height</c> in pixels (default <see cref="DefaultMaptextSize"/>).</summary>
    public short MaptextHeight { get; set; } = DefaultMaptextSize;

    /// <summary>Version 18: BYOND <c>maptext_x</c>, the text box's offset from the icon's lower left corner.</summary>
    public short MaptextX { get; set; }

    /// <summary>Version 18: BYOND <c>maptext_y</c>.</summary>
    public short MaptextY { get; set; }

    // ---- protocol version 25 (filters: docs/ai/specs/protocol.md section 34) ----

    /// <summary>Version 25: the filters the client draws, in BYOND's order (1 to <see cref="Limits.MaxAppearanceFilters"/>), or null for none (MessagesV25.cs).</summary>
    public List<FilterEntry> Filters { get; set; }

    /// <summary>Version 25: BYOND <c>render_target</c>, a string (a filter's render source names it), or none. One starting with <c>*</c> is not drawn itself.</summary>
    public TableRef RenderTarget { get; set; }

    /// <summary>
    /// Version 99 (DV687): BYOND <c>particles</c>, the particle set the atom emits (tg's particle holders: a cigarette's smoke), as a string
    /// table entry holding the set's vars as JSON (docs/ai/specs/protocol.md section 95), or none.
    /// </summary>
    public TableRef Particles { get; set; }

    /// <summary>BYOND's default <c>maptext_width</c> and <c>maptext_height</c>: one icon (32 pixels).</summary>
    public const short DefaultMaptextSize = 32;
}

/// <summary>Bits of <see cref="NetField.Posture"/>: tg's <c>body_position</c> and <c>lying_angle</c> (0, 90, 180, 270), one byte; all eight bits are used (version 100).</summary>
public static class PostureBits
{
    /// <summary>Bit 0: lying (tg <c>LYING_DOWN</c>).</summary>
    public const byte Lying = 1;

    /// <summary>Bits 1 and 2: <c>lying_angle / 90</c>.</summary>
    public const int AngleShift = 1;

    /// <summary>
    /// Bit 3: limp (the owner's play test, 2026-10-13): tg's <c>stat</c> is not CONSCIOUS (unconscious, soft crit, dead), so the body lies where it
    /// fell, face up or down by chance of the viewer; a conscious lying mob (knocked down, crawling) is drawn face down.
    /// </summary>
    public const byte Limp = 8;

    /// <summary>
    /// Bit 4: incorporeal (the dev noclip, tg's <c>incorporeal_move</c>): the mob passes through tables and crates, so the client never stands
    /// it on one (the owner, 2026-11-02: a noclip player was seen climbing every table he flew over).
    /// </summary>
    public const byte Incorporeal = 16;

    /// <summary>
    /// Bit 5: buckled to an object (tg's <c>buckled</c>: a bed, a chair, a vehicle; not a mob): the client draws the mob where it draws that object, so
    /// a dragged bed carries its patient (the owner, 2026-11-20: the patient trailed the bed and jumped onto it now and then).
    /// </summary>
    public const byte Buckled = 32;

    /// <summary>
    /// Bit 6 (version 100, slice T4c, DV690): someone turned the lying body over, so its side up is fixed (<see cref="FaceUp"/>) for every viewer.
    /// Set only while lying; the server forgets the side when the body stands up.
    /// </summary>
    public const byte SideFixed = 64;

    /// <summary>Bit 7 (version 100, slice T4c, DV690): with <see cref="SideFixed"/>, the turned body lies face up (its front is drawn); without, back up.</summary>
    public const byte FaceUp = 128;

    /// <summary>
    /// The byte of a body position and lying angle (the angle is taken modulo 360 in quarter turns). <paramref name="turnedFaceUp"/>: the side a
    /// turned lying body lies on (true face up, false back up), null when no one turned it; written only while lying.
    /// </summary>
    public static byte Encode(bool lying, int lyingAngle, bool limp = false, bool incorporeal = false, bool buckled = false, bool? turnedFaceUp = null)
        => (byte)((lying ? Lying : 0) | (((lyingAngle / 90) & 3) << AngleShift) | (lying && limp ? Limp : 0) | (incorporeal ? Incorporeal : 0) | (buckled ? Buckled : 0)
            | (lying && turnedFaceUp is bool up ? SideFixed | (up ? FaceUp : 0) : 0));

    /// <summary>The side a turned lying body lies on (<see cref="SideFixed"/>, <see cref="FaceUp"/>): true face up, false back up, null when not turned.</summary>
    public static bool? TurnedFaceUp(byte posture) => (posture & SideFixed) != 0 ? (posture & FaceUp) != 0 : null;

    /// <summary>True when the byte says buckled to an object (<see cref="Buckled"/>).</summary>
    public static bool IsBuckled(byte posture) => (posture & Buckled) != 0;

    /// <summary>True when the byte says incorporeal (<see cref="Incorporeal"/>).</summary>
    public static bool IsIncorporeal(byte posture) => (posture & Incorporeal) != 0;

    /// <summary>True when the byte says limp (<see cref="Limp"/>).</summary>
    public static bool IsLimp(byte posture) => (posture & Limp) != 0;

    /// <summary>True when the byte says lying.</summary>
    public static bool IsLying(byte posture) => (posture & Lying) != 0;

    /// <summary>The lying angle in degrees (0, 90, 180 or 270).</summary>
    public static int LyingAngle(byte posture) => ((posture >> AngleShift) & 3) * 90;
}

/// <summary>A cell's solidity changed (M3 doors): the client's collision view (MapGrid v2's bits) follows.</summary>
public readonly record struct CellSolidChange(int X, int Y, int Z, bool Solid);

/// <summary>A cell's turf appearance changed (M3 <c>ChangeTurf</c>, icon smoothing): a public appearance.</summary>
public readonly record struct CellAppearanceChange(int X, int Y, int Z, TableRef Appearance);

/// <summary>
/// One <c>client.screen</c> entry of the session's player (D107: tg's own screen objects, the health doll, the health
/// indicator, fullscreens, alerts), as its owner sees it. Private (D40): only in the own block of its owner.
/// </summary>
/// <param name="Id">The screen object's world id (<c>dm_id</c>).</param>
/// <param name="Appearance">Its appearance; private-table references (the doll's per-zone states) are what keep a health level out of the public tables.</param>
/// <param name="ScreenLoc">tg's <c>screen_loc</c> text (<c>"WEST,CENTER-2"</c>, <c>"EAST-1:28,CENTER-1:15"</c>), which the client maps from tg's 15x15 view grid onto its screen (M2 P8).</param>
public readonly record struct ScreenEntry(long Id, TableRef Appearance, string ScreenLoc)
{
    // ---- protocol version 4 (M4a contracts, MessagesV4.cs; the encoding is M4a P6's) ----

    /// <summary>
    /// Version 4: tg's HUD group of the screen object (<see cref="HudGroup"/>: the group of <c>hud_used.screen_groups</c> that
    /// lists it, <see cref="HudGroup.None"/> when none does). D131: the client lays out <see cref="HudGroup.Storage"/> and
    /// <see cref="HudGroup.ToggleableInventory"/> entries in its native windows; a click on any entry is a
    /// <see cref="TargetKind.Screen"/> click on its id.
    /// </summary>
    public byte Group { get; init; }

    // ---- protocol version 11 (M4v slice 4, D143: docs/ai/specs/protocol.md section 20) ----

    /// <summary>
    /// Version 11: the screen object's last <c>flick</c> (tg's resist button, <c>screen_objects.dm:582</c>): the appearance it plays
    /// (the object's own with the flicked icon state in its place; a private or public reference, <see cref="TableRef.None"/> for
    /// none) and the tick it started in (<see cref="FlickTick"/>). The HUD plays the flicked state once from that tick, then the
    /// entry's own picture again, as the world does for an entity (section 17.1).
    /// </summary>
    public TableRef FlickAppearance { get; init; }

    /// <summary>Version 11: the tick <see cref="FlickAppearance"/> started in (0 without a flick).</summary>
    public long FlickTick { get; init; }

    /// <summary>
    /// Version 11: the screen object's running <c>animate()</c> chains (an alert sliding in, <c>alert.dm:66</c>; a fullscreen
    /// fading out, <c>fullscreen.dm:31</c>), as an entity's <see cref="NetField.Animation"/> (section 18.1); null for none.
    /// </summary>
    public EntityAnimation Animation { get; init; }

    // ---- protocol version 12 (M4a: docs/ai/specs/protocol.md section 21) ----

    /// <summary>
    /// Version 12: the screen object's clickable <c>vis_contents</c> children (<see cref="ScreenChildren"/>: the item in a hand or a slot,
    /// <c>inventory_slot.dm:55</c>), bottom to top; null for none. BYOND sends a click on a child's pixel to the child itself, so the client
    /// sends it as a <see cref="TargetKind.Screen"/> click on the child's id (interaction.md section 2). A child with <c>VIS_INHERIT_ID</c> or
    /// <c>mouse_opacity</c> 0 is not listed: its pixels are the holder's.
    /// </summary>
    public ScreenChildren VisChildren { get; init; }

    // ---- protocol version 27 (docs/ai/specs/protocol.md section 36) ----

    /// <summary>
    /// Version 27: for tg's action button (<c>/atom/movable/screen/movable/action_button</c>), the world id of its action's target
    /// when that is an atom (<c>linked_action.target</c>: the flashlight of "Toggle Light", the tank of "Set Internals"); 0 otherwise.
    /// The client draws the progress bars that hang on that item (<see cref="ImageEntry.Holder"/>) under the button.
    /// </summary>
    public long ActionTarget { get; init; }

    // ---- protocol version 43 (M10 slice E: docs/ai/specs/protocol.md section 52) ----

    /// <summary>Version 43: for tg's inventory box (<c>/atom/movable/screen/inventory</c>), its <c>slot_id</c> (an <c>ITEM_SLOT_*</c> bit); 0 otherwise.</summary>
    public uint SlotId { get; init; }

    /// <summary>Version 43: for tg's hand box (<c>/atom/movable/screen/inventory/hand</c>), its <c>held_index</c> (odd: a left hand); 0 otherwise.</summary>
    public byte HandIndex { get; init; }
}

/// <summary>
/// The per-connection private block (D40, scope 2h): after the shared body of every <see cref="Delta"/>, and in every
/// <see cref="Snapshot"/>, for that connection only. Another session's private block never appears in a session's bytes,
/// and no health number ever leaves the server (only tg's own screen states, as appearances).
/// </summary>
public sealed partial class OwnBlock
{
    /// <summary>True when the connection's mob is a capsule mob (the locomotion bridge drives it); then <see cref="Motion"/> and <see cref="Params"/> are meaningful.</summary>
    public bool HasMotion { get; set; }

    /// <summary>The authoritative state of the own capsule after the frame <c>lastInputSeq</c> was applied (the header's): what the predictor reconciles against (D103).</summary>
    public MotionState Motion { get; set; }

    /// <summary>The motion parameters the server will use for the next tick (speed, radius, the gate, the move delay): the predictor's parameters.</summary>
    public MotionParams Params { get; set; }

    /// <summary>Frames waiting in the server's jitter buffer for this connection after the tick: the client's input clock aims for about one (scope 2d).</summary>
    public byte InputBufferDepth { get; set; }

    /// <summary>The private table index of <c>NewPrivateStrings[0]</c>, equal to the table's length before this block.</summary>
    public uint FirstNewPrivateString { get; set; }

    /// <summary>New entries of the connection's private string table (icon states of its HUD), in index order.</summary>
    public List<string> NewPrivateStrings { get; } = new();

    /// <summary>The private table index of <c>NewPrivateResources[0]</c>.</summary>
    public uint FirstNewPrivateResource { get; set; }

    /// <summary>New entries of the connection's private resource table, in index order.</summary>
    public List<string> NewPrivateResources { get; } = new();

    /// <summary>The private table index of <c>NewPrivateAppearances[0]</c>.</summary>
    public uint FirstNewPrivateAppearance { get; set; }

    /// <summary>New entries of the connection's private appearance table, in index order (they may reference public entries and earlier private ones).</summary>
    public List<AppearanceEntry> NewPrivateAppearances { get; } = new();

    /// <summary><c>client.screen</c> entries added since the last block (all of them in a Snapshot).</summary>
    public List<ScreenEntry> ScreenAdded { get; } = new();

    /// <summary>Entries whose appearance or <c>screen_loc</c> changed.</summary>
    public List<ScreenEntry> ScreenChanged { get; } = new();

    /// <summary>Ids of the entries removed from <c>client.screen</c>.</summary>
    public List<long> ScreenRemoved { get; } = new();
}

/// <summary>
/// Client to server, Input channel (unreliable): the newest input frames, oldest first, the last one the newest (scope 2d:
/// each message repeats the last <see cref="Limits.MaxInputFramesPerMessage"/> frames, so a lost message costs nothing
/// while the next arrives). The seqs are consecutive. The server's jitter buffer takes each new frame once (by seq) and
/// applies one per tick.
/// </summary>
public sealed class MotionInputMessage
{
    /// <summary>The frames, in increasing consecutive seq, 1 to <see cref="Limits.MaxInputFramesPerMessage"/> of them.</summary>
    public List<MotionInput> Frames { get; } = new();
}

/// <summary>What an <see cref="ActionMessage"/> asks for (scope 2b, 2f, 2k; each runs tg's own path).</summary>
public enum ActionKind : byte
{
    /// <summary>Never on the wire.</summary>
    None = 0,

    /// <summary>Rest or get up: tg's <c>/datum/keybinding/living/rest</c> (<c>U</c>), <c>toggle_resting</c>.</summary>
    Rest = 1,

    /// <summary>Walk or run: tg's <c>/datum/keybinding/living/toggle_move_intent_alternative</c> (press to cycle, it does not cycle back on release), <c>toggle_move_intent</c>.</summary>
    ToggleMoveIntent = 2,

    /// <summary>Combat mode: tg's <c>/datum/keybinding/living/toggle_combat_mode</c> (<c>F</c>).</summary>
    ToggleCombatMode = 3,

    /// <summary>A click on <see cref="ActionMessage.Target"/> with <see cref="ActionMessage.Button"/> and <see cref="ActionMessage.Modifiers"/>: BYOND's <c>client/Click</c>, tg's <c>ClickOn</c> (a right click with an empty hand on a mob is the shove, scope 2f).</summary>
    Click = 4,

    /// <summary>A dev command line (<see cref="ActionMessage.Text"/>, D73, Q9): accepted only when the server's <c>DEV_COMMANDS</c> is on.</summary>
    DevCommand = 5,

    // ---- protocol version 4 (M4a contracts, MessagesV4.cs) ----

    /// <summary>Version 4: a tg keybinding (<see cref="ActionMessage.Keybinding"/>) pressed (<c>down</c>) or released (<see cref="ActionMessage.KeyUp"/>: <c>up</c>), tg's <c>keyDown</c>/<c>keyUp</c> path.</summary>
    Keybinding = 6,

    /// <summary>Version 4: the crosshair's target changed to the action's target (tg's <c>MouseEntered</c>; the server answers with <see cref="ContextHintsMessage"/>, at most once per tick, tg's <c>SSmouse_entered</c>).</summary>
    Hover = 7,

    // ---- protocol version 5 (MessagesV5.cs) ----

    /// <summary>Version 5, a dev tool: the air of the cells around the action's turf target (<see cref="ActionMessage.TurfX"/>, <see cref="ActionMessage.TurfY"/>, <see cref="ActionMessage.TurfZ"/>); the server answers with a <see cref="DevGasReport"/> when its <c>DEV_COMMANDS</c> is on.</summary>
    DevGasProbe = 8,

    // ---- protocol version 14 (MessagesV14.cs) ----

    /// <summary>Version 14: the pointer left the last hovered thing over nothing (BYOND's <c>MouseExited</c> on it with no <c>MouseEntered</c> after); untargeted. A <see cref="Hover"/> on another thing exits the last one on the server by itself.</summary>
    Unhover = 9,

    // ---- protocol version 15 (MessagesV15.cs) ----

    /// <summary>Version 15: the player picked element <see cref="ActionMessage.RadialElement"/> (1 based in the page's order; 0 the centre button) of the radial menu <see cref="ActionMessage.RadialMenu"/> (a <see cref="RadialMenuMessage"/>); untargeted.</summary>
    RadialPick = 10,

    // ---- protocol version 16 (MessagesV16.cs) ----

    /// <summary>Version 16: tg's <c>act/&lt;action&gt;</c> message from the tgui window <see cref="ActionMessage.UiWindow"/>: the action <see cref="ActionMessage.UiAction"/> with its params <see cref="ActionMessage.UiParams"/> (an object or none); untargeted.</summary>
    UiAct = 11,

    /// <summary>Version 16: tg's <c>close</c> message: the player closed the tgui window <see cref="ActionMessage.UiWindow"/> (its close button or Esc); untargeted.</summary>
    UiClose = 12,

    // ---- protocol version 17 (MessagesV17.cs) ----

    /// <summary>
    /// Version 17: BYOND's mouse drop. The player dragged <see cref="ActionMessage.Target"/> (by <see cref="ActionMessage.TargetKind"/>) with
    /// <see cref="ActionMessage.Button"/> held and released it over <see cref="ActionMessage.OverTarget"/> (by <see cref="ActionMessage.OverKind"/>);
    /// the server runs tg's <c>/client/MouseDrag</c> and <c>/client/MouseDrop</c> (drag_drop.dm), which call the dragged atom's <c>MouseDrop</c>.
    /// </summary>
    Drop = 13,

    // ---- protocol version 24 (MessagesV24.cs) ----

    /// <summary>
    /// Version 24 (D154): the player clicked a link of tg's chat, <c>&lt;a href='byond://?...'&gt;</c>; <see cref="ActionMessage.Text"/>
    /// is the href, at most <see cref="Limits.MaxTopicHrefBytes"/> bytes. The server takes only a link it sent to that player's chat and
    /// runs BYOND's <c>/client/Topic</c>: the <c>src</c> datum's <c>Topic(href, href_list)</c>; untargeted.
    /// </summary>
    Topic = 14,

    // ---- protocol version 34 (MessagesV34.cs) ----

    /// <summary>
    /// Version 34: BYOND's <c>/client/MouseDown</c>: a button went down over <see cref="ActionMessage.Target"/> (by <see cref="ActionMessage.TargetKind"/>,
    /// None for nothing) with <see cref="ActionMessage.Button"/>, <see cref="ActionMessage.Modifiers"/>, the icon pixel and the aim. The client sends
    /// it just before the press's <see cref="Click"/> (tg's full auto sets <c>click_intercept_time</c> here, which drops that click).
    /// </summary>
    MouseDown = 15,

    /// <summary>Version 34: BYOND's <c>/client/MouseDrag</c> while a button is held: what is under the crosshair now and the aim (at most every 0.1 s).</summary>
    MouseDrag = 16,

    /// <summary>Version 34: BYOND's <c>/client/MouseUp</c>: the button went up over the target, with the aim.</summary>
    MouseUp = 17,

    // ---- protocol version 38 (MessagesV38.cs) ----

    /// <summary>
    /// Version 38: a message of tg's say window (tgui-say) to the server, <c>/datum/tgui_say/proc/on_message(type, payload)</c>: its type
    /// <see cref="ActionMessage.SayType"/>, the channel <see cref="ActionMessage.SayChannel"/>, the text <see cref="ActionMessage.Text"/> (at
    /// most <see cref="Limits.MaxSayEntryBytes"/>) and the thinking flag <see cref="ActionMessage.SayVisible"/>; untargeted.
    /// </summary>
    Say = 18,

    // ---- protocol version 39 (MessagesV39.cs) ----

    /// <summary>
    /// Version 39 (M9 slice B, D256): the emote wheel's pick, tg's emote keybinding (<c>/datum/keybinding/emote/down</c>:
    /// <c>user.mob.emote(emote_key, intentional = TRUE)</c>) for the emote key <see cref="ActionMessage.Text"/>
    /// (<see cref="EmoteKeys.IsValid"/>); untargeted.
    /// </summary>
    Emote = 19,

    // ---- protocol version 51 (MessagesV51.cs) ----

    /// <summary>
    /// Version 51 (M10x slice C): BYOND's <c>DblClick</c> on <see cref="ActionMessage.Target"/> (tg's <c>/atom/DblClick</c>,
    /// <c>usr.DblClickOn</c>): the second press of a double click, sent after that press's own <see cref="Click"/>, with a mouse
    /// action's fields (a target, the button, the modifiers, the icon pixel; no aim).
    /// </summary>
    DblClick = 20,

    // ---- protocol version 70 (MessagesV70.cs) ----

    /// <summary>
    /// Version 70: BYOND's <c>MouseWheel</c> over <see cref="ActionMessage.Target"/> (tg's <c>/atom/MouseWheel</c>, <c>usr.MouseWheelOn</c>), with a
    /// mouse action's fields: the button byte carries the wheel's turn, <see cref="ClickButton.Left"/> up (tg's <c>delta_y</c> above 0) and
    /// <see cref="ClickButton.Right"/> down.
    /// </summary>
    MouseWheel = 21,
    /// <summary>
    /// Version 78 (silicons SI4): run the command verb <see cref="ActionMessage.Text"/> (a <see cref="VerbEntry.Path"/>, at most
    /// <see cref="Limits.MaxVerbPathBytes"/>) of the client's mob, as BYOND runs a verb clicked in the stat panel; the server takes only a
    /// verb it listed to the client. Untargeted.
    /// </summary>
    Verb = 22,
    /// <summary>
    /// Version 78 (DV387): the player closed the popup window <see cref="ActionMessage.Text"/> (a <see cref="PopupWindow.Name"/>, at most
    /// <see cref="Limits.MaxPopupNameBytes"/>): tg's on-close command, <c>handle-popup-close</c>. Untargeted.
    /// </summary>
    PopupClose = 23,
    /// <summary>
    /// Version 81 (D328): throw what the hand holds (or a mob held in an aggressive grab) at <see cref="ActionMessage.Target"/>, with a click's
    /// fields (the left button, no modifiers, the icon pixel, the aim zone on a mob, the camera's <see cref="ActionMessage.Aim"/>): tg's click in
    /// throw mode, whatever the throw mode is (<c>ClientSeam.Throw</c>). The client's one-key throw and the radial's Throw at.
    /// </summary>
    Throw = 24,
    /// <summary>
    /// Version 82 (D329): put what the active hand holds down at the crosshair's point on <see cref="ActionMessage.Target"/>, with a click's
    /// fields (the left button, no modifiers, the icon pixel, the camera's <see cref="ActionMessage.Aim"/>): on a table in reach tg's own
    /// placing at that point, on a free floor in reach the item put there, else tg's drop at the feet (<c>ClientSeam.PutDown</c>). The drop key.
    /// </summary>
    PutDown = 25,
    /// <summary>
    /// Version 86 (DV503): the radial's entry <see cref="ActionMessage.Text"/> (a <see cref="ContextHint.Label"/> the server sent with
    /// <see cref="ContextHintAction.Extra"/>, at most <see cref="Limits.MaxContextLabelBytes"/>) picked on <see cref="ActionMessage.Target"/>: a port
    /// action that is no click (the table's Flip). The server runs it only when it would offer that entry for the target now.
    /// </summary>
    ContextPick = 26,
    /// <summary>
    /// Version 97 (DV597): the client that said <see cref="Hello.WaitsForLoad"/> has built and loaded the world (its loading screen is done); no
    /// fields. Without tg's lobby the server late-joins its new player now.
    /// </summary>
    Loaded = 27,
    /// <summary>
    /// Version 100 (DV693, ui-overhaul.md section 10): a press of hotbar cell <see cref="ActionMessage.HotbarCell"/> (1 to 10: 1 to 7 the belt,
    /// 8 and 9 the pockets, 10 the ID slot), untargeted. The server does what the cell's rules say with tg's procs, or tells the client it refused.
    /// </summary>
    HotbarPress = 28,
    /// <summary>
    /// Version 100 also (DV696, ui-overhaul.md section 12, slice 4B): move <see cref="ActionMessage.MoveItem"/>, an item of the own inventory, to
    /// <see cref="ActionMessage.MoveTo"/> (a carried storage <see cref="ActionMessage.MoveStorage"/> at a wished cell, a worn slot, a hand or the floor;
    /// <see cref="ActionMessage.MoveIndex"/>), untargeted. The server checks every field and does the move with tg's procs, or refuses it.
    /// </summary>
    InventoryMove = 29,
    /// <summary>Version 100 also (DV696): the Tab inventory was opened; the server plays tg's opening of the back storage (its rustle), no fields.</summary>
    InventoryOpen = 30,
}

/// <summary>The mouse button of a click (tg's <c>params</c>: <c>button=left</c>, <c>right</c>, <c>middle</c>).</summary>
public enum ClickButton : byte
{
    None = 0,
    Left = 1,
    Right = 2,
    Middle = 3,
}

/// <summary>Modifier keys held during a click (tg's <c>params</c>: <c>shift=1</c>, <c>ctrl=1</c>, <c>alt=1</c>).</summary>
[Flags]
public enum ClickModifiers : byte
{
    None = 0,
    Shift = 1,
    Ctrl = 2,
    Alt = 4,

    /// <summary>Every valid bit.</summary>
    All = Shift | Ctrl | Alt,
}

/// <summary>
/// Client to server, Control channel (reliable ordered): one discrete action (scope 2d "Actions ... go reliable on Control,
/// tagged with their frame's seq, and apply in that frame's tick"). The server applies it in the command phase of the tick
/// that applies the frame <see cref="FrameSeq"/> (so a click sees the capsule where the player saw it), after that frame,
/// or at once when the frame was already applied.
/// </summary>
/// <param name="FrameSeq">The <see cref="MotionInput.Seq"/> of the frame the action belongs to.</param>
/// <param name="Kind">What to do.</param>
/// <param name="Target">The entity clicked (<see cref="ActionKind.Click"/>), else 0. The server checks it exists and is visible to the player; reach is tg's own <c>Adjacent</c>.</param>
/// <param name="Button">The button of a click.</param>
/// <param name="Modifiers">The modifier keys of a click.</param>
/// <param name="Text">The command line of <see cref="ActionKind.DevCommand"/>, at most <see cref="Limits.MaxDevCommandBytes"/> bytes, or the href of a <see cref="ActionKind.Topic"/> (version 24), at most <see cref="Limits.MaxTopicHrefBytes"/>; null otherwise.</param>
public readonly record struct ActionMessage(uint FrameSeq, ActionKind Kind, long Target, ClickButton Button, ClickModifiers Modifiers, string Text)
{
    // ---- protocol version 4 (M4a contracts, MessagesV4.cs; the encoding is M4a P6's; docs/ai/specs/interaction.md 2-3) ----

    /// <summary>
    /// Version 4: what a <see cref="ActionKind.Click"/> or <see cref="ActionKind.Hover"/> is on (<see cref="Protocol.TargetKind"/>):
    /// an entity or a screen entry by id (<see cref="Target"/>), or a turf by <see cref="TurfX"/>, <see cref="TurfY"/>,
    /// <see cref="TurfZ"/>. <see cref="TargetKind.None"/> for every other kind. The server checks it (D40): an entity exists, is
    /// replicated and is within tg's view range of the mob; a turf is in the map, on the mob's level and within view range; a
    /// screen id is in the session's <c>client.screen</c> now. Reach is tg's own (<c>Adjacent</c>, <c>IsReachableBy</c>).
    /// </summary>
    public TargetKind TargetKind { get; init; }

    /// <summary>The target's kind as the server reads it: <see cref="TargetKind"/>, or <see cref="TargetKind.Entity"/> for a version-3 click (kind None, <see cref="Target"/> above 0).</summary>
    public TargetKind EffectiveTargetKind => TargetKind != TargetKind.None ? TargetKind : (Kind is ActionKind.Click or ActionKind.Hover or ActionKind.DblClick or ActionKind.Throw or ActionKind.PutDown or ActionKind.ContextPick && Target > 0 ? TargetKind.Entity : TargetKind.None);

    /// <summary>Whether the kind names a target (a click, a hover; version 5: a dev gas probe, always a turf).</summary>
    public bool IsTargeted => Kind is ActionKind.Click or ActionKind.Hover or ActionKind.DevGasProbe or ActionKind.Drop or ActionKind.DblClick or ActionKind.Throw or ActionKind.PutDown or ActionKind.ContextPick || IsMouse;

    /// <summary>Version 34: a <see cref="ActionKind.MouseDown"/>, <see cref="ActionKind.MouseDrag"/> or <see cref="ActionKind.MouseUp"/> (their target may be none).</summary>
    public bool IsMouse => Kind is ActionKind.MouseDown or ActionKind.MouseDrag or ActionKind.MouseUp or ActionKind.MouseWheel; // version 70: the wheel

    /// <summary>Version 34: the camera's aim of a click or a mouse action (<see cref="ShotAim"/>, DV94), null when the client sent none.</summary>
    public ShotAim? Aim { get; init; }

    /// <summary>Version 4: a turf target's x (1 to <see cref="Limits.MaxMapSize"/>), else 0.</summary>
    public int TurfX { get; init; }

    /// <summary>Version 4: a turf target's y.</summary>
    public int TurfY { get; init; }

    /// <summary>Version 4: a turf target's z (1 to <see cref="Limits.MaxMapLevels"/>).</summary>
    public int TurfZ { get; init; }

    /// <summary>
    /// Version 4: tg's <c>icon-x</c> of a click, 1 to 32 (0: absent): the hit point within the sprite (a billboard's picture
    /// pixel) or the tile (a floor or table top: east is x), in tg pixels. The server writes it into tg's <c>params</c>.
    /// </summary>
    public byte IconX { get; init; }

    /// <summary>Version 4: tg's <c>icon-y</c>, 1 to 32 (0: absent): north (or up on a billboard) is y.</summary>
    public byte IconY { get; init; }

    /// <summary>The body zone a click aims at (<see cref="Protocol.AimZone"/>, DV30), bits 4-6 of the modifiers byte (version 100: bit 3 its high bit); version 100 also: the zone a hover on an entity looks at, a byte after its target; None otherwise.</summary>
    public AimZone AimZone { get; init; }

    /// <summary>Version 4: the keybinding of a <see cref="ActionKind.Keybinding"/> (<see cref="KeybindingCode"/>), else None.</summary>
    public KeybindingCode Keybinding { get; init; }

    /// <summary>Version 4: a <see cref="ActionKind.Keybinding"/> is the key's release (tg's <c>up</c>) instead of its press (<c>down</c>).</summary>
    public bool KeyUp { get; init; }

    /// <summary>Version 15: the radial menu of a <see cref="ActionKind.RadialPick"/> (<see cref="RadialMenuMessage.MenuId"/>, above 0), else 0.</summary>
    public long RadialMenu { get; init; }

    /// <summary>Version 15: the picked element of a <see cref="ActionKind.RadialPick"/>, 1 to <see cref="Limits.MaxRadialElements"/> in the page's order, 0 for the centre button; else 0.</summary>
    public int RadialElement { get; init; }

    /// <summary>Version 16: the tgui window of a <see cref="ActionKind.UiAct"/> or <see cref="ActionKind.UiClose"/> (its pool index, 1 to <see cref="Limits.MaxUiWindows"/>), else 0.</summary>
    public int UiWindow { get; init; }

    /// <summary>Version 16: the action of a <see cref="ActionKind.UiAct"/> (tg's <c>act(action)</c>, 1 to <see cref="Limits.MaxUiActionBytes"/> bytes), else null.</summary>
    public string UiAction { get; init; }

    /// <summary>Version 16: the params of a <see cref="ActionKind.UiAct"/> (tg's <c>act(action, params)</c>: an object, or null for none), else null.</summary>
    public UiValue UiParams { get; init; }

    // ---- protocol version 17 (drag and drop, MessagesV17.cs) ----

    /// <summary>Version 17: what a <see cref="ActionKind.Drop"/> was released over (BYOND's <c>over_object</c>): an entity, a turf or a screen entry; None for every other kind.</summary>
    public TargetKind OverKind { get; init; }

    /// <summary>Version 17: the id of an entity or screen <see cref="OverKind"/>, else 0.</summary>
    public long OverTarget { get; init; }

    /// <summary>Version 17: a turf <see cref="OverKind"/>'s x (1 to <see cref="Limits.MaxMapSize"/>), else 0.</summary>
    public int OverX { get; init; }

    /// <summary>Version 17: a turf <see cref="OverKind"/>'s y.</summary>
    public int OverY { get; init; }

    /// <summary>Version 17: a turf <see cref="OverKind"/>'s z (1 to <see cref="Limits.MaxMapLevels"/>).</summary>
    public int OverZ { get; init; }

    /// <summary>
    /// Version 17: how long the drag lasted, from the press that started it to the release, in deciseconds (BYOND's <c>world.time</c> unit),
    /// at most 255 (a longer drag is 255). tg's drag counts as a click when it is shorter than 0.1 s (client/is_drag_clickable); the server
    /// dates tg's <c>drag_start</c> back by it.
    /// </summary>
    public byte DragTime { get; init; }

    // ---- protocol version 38 (speech, MessagesV38.cs) ----

    /// <summary>Version 38: the type of a <see cref="ActionKind.Say"/> (<see cref="SayTypes"/>), else null.</summary>
    public string SayType { get; init; }

    /// <summary>Version 38: the channel of a <see cref="ActionKind.Say"/> (tg's <c>SAY_CHANNEL</c> and the rest, <see cref="SayChannels"/>), or null; null for every other kind.</summary>
    public string SayChannel { get; init; }

    /// <summary>Version 38: a <see cref="ActionKind.Say"/> of type "thinking": tg's <c>payload["visible"]</c>; false otherwise.</summary>
    public bool SayVisible { get; init; }

    /// <summary>Version 100 also (DV693): the cell of a <see cref="ActionKind.HotbarPress"/>, 1 to <see cref="HotbarMessage.CellCount"/>; 0 for every other kind.</summary>
    public int HotbarCell { get; init; }

    /// <summary>Version 100 also (DV696): the item an <see cref="ActionKind.InventoryMove"/> moves (an entity id, above 0); 0 for every other kind.</summary>
    public long MoveItem { get; init; }

    /// <summary>Version 100 also: where an <see cref="ActionKind.InventoryMove"/> puts its item; <see cref="InventoryTarget.None"/> for every other kind.</summary>
    public InventoryTarget MoveTo { get; init; }

    /// <summary>Version 100 also: the storage (an entity id, above 0) of a move to <see cref="InventoryTarget.Storage"/>, else 0.</summary>
    public long MoveStorage { get; init; }

    /// <summary>
    /// Version 100 also: a move's index: the wished cell of a storage (1 to <see cref="Limits.MaxStorageItems"/>, 0 for none), tg's slot id of a worn slot
    /// (<see cref="InventorySlots.Worn"/>), tg's held index of a hand (1 to <see cref="Limits.MaxStorageHands"/>); 0 for the floor and every other kind.
    /// </summary>
    public int MoveIndex { get; init; }
}
