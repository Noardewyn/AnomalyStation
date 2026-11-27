namespace Anomaly.Protocol;

// Owner: M1 P2 (protocol and transport). Contract written by the M1 contracts step (docs/ai/specs/m1-ownership.md); the
// wire format itself is P2's and is documented in docs/ai/specs/protocol.md.

/// <summary>
/// The protocol version and the magic bytes of <see cref="Hello"/> (scope 2e "Handshake"). A server and a client talk only
/// when their <see cref="Current"/> versions are equal: the server answers any other version with
/// <see cref="DisconnectReason.WrongVersion"/> before a connection exists (D60: a protocol version check on connect).
/// </summary>
public static class ProtocolVersion
{
    /// <summary>
    /// The version this build speaks. Every change of the wire format bumps it. Version 2 (M2): the capsule fields, the
    /// appearance, resource and string tables, the private block, MapGrid version 2, MotionInput frames on the Input channel
    /// and actions (MessagesV2.cs). The M2 contracts step set the number; until M2 P5's codec lands the bytes are still
    /// version 1's layout (both ends of this build agree, so nothing breaks). M10 moves deltas to an unreliable channel with
    /// acks (PLAN 2.9). Version 3 (M3): the cell flags (faces, opacity, starlight), the base lights and the turf type changes
    /// of MapGrid and Delta, and the entity's Light field (MessagesV3.cs). The M3 contracts step set the number; until M3 P5's
    /// codec lands the bytes are still version 2's layout (both ends of this build agree). Version 4 (M4a): the click and
    /// hover targets (entity, turf, screen) with tg's icon-x and icon-y, the Keybinding and Hover actions, the ContextHints
    /// message, the screen entries' HUD group, the private images and the entity flags (MessagesV4.cs). The M4a contracts
    /// step set the number; until M4a P6's codec lands the bytes are still version 3's layout (both ends of this build agree).
    /// Version 5 (M4b slice 3): the gas a cell shows (MapGrid's gas kinds and gas cells, the Delta's gas changes), the
    /// DevGasProbe action and the DevGasReport message (MessagesV5.cs). Version 6 (D141): the Airflow message, the air flow
    /// around each player for the client's airflow streams (MessagesV6.cs). Version 7: the Airflow message's floor devices
    /// (vents, scrubbers, injectors moving air in or out of a cell). Version 8 (D142): the entity's last <c>flick</c>
    /// (<see cref="NetField.Flick"/>). Version 9 (M4v, D143): the atom's running <c>animate()</c> chains
    /// (<see cref="NetField.Animation"/>, MessagesV9.cs). Version 10 (M4v slice 3): the short-lived visuals drawn on another atom
    /// (tg's <c>flick_overlay_view</c>, <see cref="NetField.VisHost"/>). Version 11 (M4v slice 4): a screen entry's flick and
    /// <c>animate()</c> chains (<see cref="ScreenEntry.FlickAppearance"/>, <see cref="ScreenEntry.Animation"/>, MessagesV2.cs). Version 12 (M4a):
    /// a screen entry's clickable <c>vis_contents</c> children, the item in a hand or a slot (<see cref="ScreenEntry.VisChildren"/>, MessagesV12.cs).
    /// Version 13 (M4b audio): the Sounds message, tg's sounds for one session with their muffle and occlusion (MessagesV13.cs).
    /// Version 14 (M4c slice 3): the Unhover action, BYOND's <c>MouseExited</c> when the pointer leaves the hovered thing over nothing
    /// (a hover of another thing exits the last one on the server), and the client's hover of its HUD entries (MessagesV14.cs).
    /// Version 15 (M4c slice 4, DV40): tg's radial menu, the RadialMenu message (a menu's page to draw, or its close) and the RadialPick
    /// action (the client's answer: an element or the centre) (MessagesV15.cs).
    /// Version 16 (M5 slice 3, D148, DV44): tg's tgui windows drawn natively, the UiWindow message (a window's update with tg's config,
    /// data and static data, or its close) and the UiAct and UiClose actions (tg's act and close messages) (MessagesV16.cs).
    /// Version 17 (drag and drop, DV45): the Drop action, BYOND's <c>MouseDrop</c> of a dragged thing over another (MessagesV17.cs).
    /// Version 18 (HUD maptext, D142): the private appearances carry BYOND's <c>maptext</c> with its box (<see cref="AppearanceEntry.Maptext"/>,
    /// MessagesV2.cs), and a table string may be 1024 bytes (MessagesV18.cs).
    /// Version 19 (tg's tooltips, DV47): the Tooltip message, what tg's <c>/datum/tooltip</c> shows (its HTML text and theme) or its hide
    /// (MessagesV19.cs).
    /// Version 20 (pulling): the keybinding code 9, tg's <c>/datum/keybinding/mob/stop_pulling</c> (<see cref="KeybindingCode.StopPulling"/>,
    /// MessagesV4.cs).
    /// Version 21 (M5x slice 2): the CameraShake message, tg's <c>shake_camera</c> path of the player's view (MessagesV21.cs).
    /// Version 22 (the ghosts, D151): the private block's observer layer, the entities above <c>SEE_INVISIBLE_LIVING</c> that the
    /// connection's mob sees (a ghost's <c>INVISIBILITY_OBSERVER</c>), sent to that connection only (MessagesV22.cs).
    /// Version 23 (DV54): the private block's ghost body, the turf of the body a ghost may re-enter (MessagesV23.cs).
    /// Version 24 (D154): the Topic action, a click on a link of tg's chat (<c>byond://?</c> hrefs), which the server takes only when it
    /// sent that link to the player (MessagesV24.cs).
    /// Version 25 (D155): an appearance's filters (alpha masks, outlines, displacement maps, colour matrices) and its render target
    /// (<see cref="AppearanceEntry.Filters"/>, MessagesV25.cs).
    /// Version 26 (D156): every BYOND filter type with its parameters, each filter at its list position (MessagesV25.cs), and the
    /// animations of a filter, an animation track that names the filter by that position (<see cref="AnimationTrack.Filter"/>, MessagesV26.cs).
    /// Version 27 (progress bars, D159): a private image's holder and the holder's name, wherever the holder is (an air tank's volume
    /// bar on the tank on the player's back), and an action button's target item (<see cref="ImageEntry.Holder"/>,
    /// <see cref="ScreenEntry.ActionTarget"/>): the client labels each bar and draws an item's bars under its action button.
    /// Version 28 (DV68, DV69): the private block's motion carries a weightless capsule's drift (<see cref="Anomaly.Core.Motion.MotionParams.DriftX"/>,
    /// subunits a tick), so the client predicts the own body in zero gravity; a ghost's capsule is sent as any capsule.
    /// Version 29 (the eye blur): the private block's game plane, the appearance of tg's game plane master controller with its filters
    /// (<see cref="OwnBlock.GamePlane"/>, MessagesV29.cs): the client draws the eye blur's blur over its 3D view.
    /// Version 30 (DV75, the T-ray scanner): a private image on a turf names its tile (<see cref="ImageEntry.TurfX"/>, section 39), the
    /// client draws it there for its holder alone, and a session holds up to 512 private images (<see cref="Limits.MaxPrivateImages"/>).
    /// Version 31 (M7 slice T3, DV81, the mesons): the private block's vision, the own mob's sight flags and lighting cutoffs
    /// (<see cref="OwnBlock.Vision"/>, MessagesV31.cs).
    /// Version 32 (D169, the energy ball's orbiting mini-balls): an animation track has up to 36 steps (<see cref="Limits.MaxAnimationSteps"/>),
    /// so tg's 36-segment SpinAnimation of an orbit reaches the client.
    /// Version 33 (M7 husks): the icon recipe resource (protocol.md 34.3) gains MapColors (<c>101,1,1,m,N1..Nk</c>, tg's ColorTone of a
    /// husk's limbs), a whole-file icon (an empty state, composed per state) and an operand icon matched by state name, and escapes only
    /// what its syntax needs; the client's ICON_ADD and ICON_SUBTRACT multiply the alphas as BYOND does. A client of 32 would draw husks
    /// and blended icons wrong.
    /// Version 34 (M7x slice A, D177, MessagesV34.cs): a click's and the new mouse actions' aim (the camera's eye point and direction, DV94),
    /// the MouseDown, MouseDrag and MouseUp actions (BYOND's, which tg's full auto listens to), the entity's Shot field (a shot's 3D line:
    /// projectiles and their effects drawn at their height) and the CameraShake message's kind (tg's recoil kick, DV96).
    /// </summary>
    /// Version 35 (M8 slice G, D187, DV114, MessagesV4.cs): a private image carries BYOND's override flag (<see cref="ImageEntry.Override"/>); the client draws
    /// such an image in place of the picture of the entity it hangs on, for its holder alone (tg's delusions, the invisible effects of the hallucinations).
    /// Version 36 (M8y slice D, D230, MessagesV36.cs): a context hint carries its action (<see cref="ContextHint.Action"/>): a click, a drop of
    /// the own mob onto the target (the radial's Buckle) or the stop-pulling keybinding (its Stop pulling).
    /// Version 37 (M8z, D243, MessagesV37.cs): a MapGrid cell's climb-walkable bit (<see cref="CellFlags.ClimbWalkable"/>) and the own
    /// capsule's <see cref="Anomaly.Core.Motion.MotionParams.OnClimbable"/> (D239): the client predicts a climber walking over tables.
    /// Also (M8z slice B, D238): a context hint's action may be a drop of the hinted mob onto the own mob
    /// (<see cref="ContextHintAction.DropOntoSelf"/>, the radial's Strip, tg's drag of a mob onto oneself).
    /// Version 38 (M9 slice A, MessagesV38.cs): speech. The Say action (tg's say window to the server, up to
    /// <see cref="Limits.MaxSayActionBytes"/>), the SayModal message (the server to the window) and the Runechat message (a runechat line, DV160).
    /// Version 39 (M9 slice B, MessagesV39.cs): emotes. The Emote action (the emote wheel's pick, tg's emote keybinding) and the
    /// keybinding code <see cref="KeybindingCode.VerbEmotePanel"/> (tg's hidden verb that opens the emote panel).
    /// Version 40 (M9 slice E, MessagesV40.cs): the PDA. The RadioChannels message (the prefixes of the worn headset's channels, the big
    /// chat window's Radio list) and the keybinding code <see cref="KeybindingCode.OpenMessenger"/> (its Message button).
    /// Version 41 (M9 slice F): the keybinding code <see cref="KeybindingCode.GiveItem"/> (tg's give keybinding, the offering).
    /// Version 42 (M10 slice B, MessagesV42.cs): the Motion message: the capsule mobs' positions and yaws on the unreliable channel, every
    /// tick, each resent for <see cref="Limits.MotionResendTicks"/> ticks after it last changed; the Delta's change records no longer carry
    /// them once the entity is a capsule (a lost Delta holds the reliable channel back, a lost Motion is covered by the next).
    /// Version 44 (MessagesV44.cs): independent ceilings. MapGrid and Delta carry ceiling cells (material, integrity, cover, support),
    /// and a target may name a cell's ceiling (<see cref="TargetKind.Ceiling"/>). Version 45 (M10 slice H): the disconnect reason
    /// <see cref="DisconnectReason.RoundRestart"/>: tg's <c>world/Reboot</c> ended the round and the client joins the next one again.
    /// Version 46 (M10 slice I): the disconnect reason <see cref="DisconnectReason.Banned"/>: a ban refused the connection or closed the session.
    /// Version 47 (MessagesV47.cs): the Delta's sun, tg's <c>SSsun.azimuth</c>, for the sun of the client's sky.
    /// Version 48: the private block's motion parameters end with the tiles around the own mob where it would be weightless
    /// (<see cref="Anomaly.Core.Motion.MotionParams.GravityMask"/>) and its speed on the other kind, so the client predicts stepping off a
    /// floor into space and back.
    /// Version 49: the private block's motion parameters end with the speed and the move delay after a walk/run toggle
    /// (<see cref="Anomaly.Core.Motion.MotionParams.IntentTilesPerTick"/>), so the client predicts the frames from its toggle on.
    /// Version 50: with a gravity mask the motion parameters also say whether the mob steers on a weightless tile with nothing to push
    /// off and the speed of a drift from rest, so the client predicts stepping off a floor into space.
    /// Version 51 (M10x slice C, MessagesV51.cs): the private block's eye, tg's <c>client.eye</c> when it is another mob (a ghost
    /// observing through a player's eyes), and the <see cref="ActionKind.DblClick"/> action, BYOND's <c>DblClick</c>; the motion parameters'
    /// <see cref="Anomaly.Core.Motion.MotionParams.ImpulseSpeed"/>, so the client predicts the drift of a push from rest in open space (DV177).
    /// Version 52 (M10x slice G): the escape menu's window, <see cref="Limits.EscapeMenuUiWindow"/> with the interface
    /// <see cref="Limits.EscapeMenuUiInterface"/> (tg's escape menu page, which the client draws as its Esc menu; an older client would
    /// show it as an unknown window).
    /// Version 53 (M10x slice G5, MessagesV53.cs): the <see cref="MessageId.BugReport"/> message, the Esc menu's bug report with an
    /// optional WebP screenshot, client to server on the State channel.
    /// Version 54 (D285, MessagesV54.cs): the <see cref="MessageId.HeldMotion"/> message, the player's own gun loaded, unloaded or racked
    /// (DV213), so the first-person gun moves as it does.
    /// Version 55 (M10x slice G6, DV216): tg's browse() pages, windows <see cref="Limits.BrowseUiWindowFirst"/> and up with the interface
    /// <see cref="Limits.BrowseUiInterface"/> (the page's HTML in pieces, drawn as tg's chat lines; tgui's pooled windows stop below them),
    /// and tg's Adminhelp window (an older client would show both as unknown windows).
    /// Version 56: <see cref="EntityFlags.Pushable"/>, a dense obj a walking mob pushes, so the client predicts the push (an older client
    /// would refuse the bit).
    /// Version 57 (D295): a ceiling cell carries one of tg's floor blocks (its turf type, a lattice or catwalk, the broken pick and
    /// the support) and the public-table picture tg would draw for it, in place of version 44's material, integrity and cover.
    /// Version 58 (D295): a ceiling cell also carries its lattice's damage (tg's integrity kept between hits).
    /// Version 59 (gaps C2): the keybinding code <see cref="KeybindingCode.ViewPetCommands"/> (tg's view pet commands, the pets' radial).
    /// Version 60 (D305, MessagesV60.cs): MapGrid ends with the levels that have tg's <c>ZTRAIT_NOPARALLAX</c> (Lavaland), where the client
    /// draws its own sky and no light of space.
    /// Version 61: the posture byte's <see cref="PostureBits.Incorporeal"/> (the dev noclip), so no client stands the mob on a table (an
    /// older client would refuse the bit).
    /// Version 62 (D306, MessagesV62.cs): the host's lobby between rounds: a round's end keeps the connection (no
    /// <see cref="DisconnectReason.RoundRestart"/>), the <see cref="BetweenRounds"/> server texts while the next world builds, then a second join sequence (Welcome, MapGrid, Snapshot) on the same connection.
    /// Version 63 (D307, MessagesV63.cs): <see cref="TtsMessage"/>, a line the client voices itself (text to speech on the client).
    /// Version 64 (D308): <see cref="TtsMessage"/> ends with the line's filter chain and its <see cref="TtsFlags"/> (the radio's lines, silicon).
    /// Version 65 (D312): <see cref="TtsFlags.Whisper"/> and <see cref="TtsFlags.Yell"/>.
    /// Version 66 (DV263): the own motion carries the walking capsule's velocity, its parameters the movement feel (<c>MoveFeel</c>).
    /// Version 67 (D315, MessagesV67.cs): <see cref="MinimapMessage"/>, a z-level's minimap (tg's load_z picture, the areas and their
    /// departments), sent once per level and connection when the player's mob first stands on that level.
    /// Version 68 (D315, MessagesV68.cs): <see cref="MinimapMobsMessage"/>, the minimap's dev trackers (the living mobs of the own level,
    /// twice a second while the dev command <c>minimapmobs</c> is on).
    /// Version 69 (DV347): the own motion's parameters end with the running conveyor belts around the mob (<c>BeltField</c>), which its
    /// capsule rides on the server and in the prediction.
    /// Version 70 (D320, MessagesV70.cs): the <see cref="ActionKind.MouseWheel"/> action (BYOND's MouseWheel, tg's MouseWheelOn) and the
    /// <see cref="BuildPreviewMessage"/>, what the held tool would build for the client's ghost on the aimed tile.
    /// Version 71 (D320): the <see cref="BuildPreviewMessage"/> names the tile the build would go to (the server decides it from the hover) in
    /// place of a reach.
    /// Version 72 (DV357): the own motion's parameters end with the space wind around the mob (a <c>BeltField</c> of wind directions and
    /// its push speed) and a gravity pull's tile and speed, which carry its capsule continuously on the server and in the prediction.
    /// Version 73 (DV360, MessagesV73.cs): a MapGrid cell's <see cref="CellFlags.Flaps"/> bit and the own motion parameters' closing
    /// <c>PassesFlaps</c> byte: the client predicts tg's plastic flaps stopping a standing mob.
    /// Version 74 (gaps W1): the world's maptext: the public tables carry an appearance's maptext (the client draws it on the atom: tg's
    /// status displays), an animated vars mask's <see cref="AnimatedVar.MaptextX"/> bit, and a track of a child's own vars
    /// (<see cref="AnimationTrack.Child"/> without a filter: the status display's scrolling lines).
    /// Version 75 (D322, MessagesV75.cs): a <see cref="ServerText"/> of <see cref="DevAccess.Type"/> says whether the player may open the dev menu.
    /// Version 76 (D325, MessagesV76.cs): the MapGrid ends with the levels linked to the level below them (tg's multi-z stacks).
    /// Version 77 (DV371, MessagesV77.cs): an entity's announced level ride (<see cref="NetField.Ride"/>): the client draws an elevator's car and
    /// riders, and a ladder's climber, crossing the level over the wait instead of jumping at its end.
    /// Version 78 (silicons SI4, MessagesV78.cs): the private block's camera chunks with their static (DV386), its popup windows (DV387) and
    /// its mob's command verbs; the actions <see cref="ActionKind.Verb"/> and <see cref="ActionKind.PopupClose"/>.
    /// Version 79 (silicons SI4d, MessagesV79.cs): the runtime resources, images the world made (a painting's PNG), sent to each client once
    /// before the messages that name them.
    /// Version 80 (DV403, Codec.V80.cs): a tram platform's announced trip (<see cref="NetField.Tram"/>, <c>TramRide</c>): the client draws the tram and
    /// what rides it moving evenly, and the own predictor carries a walker on it on the ticks the server's tram steps.
    /// Version 81 (DV416, Codec.V81.cs): an entity's free motion (<see cref="NetField.Flight"/>, <c>FlightArc</c>): a thrown thing's ballistic
    /// arc, a fall, a slide the air pushes, and the point it rests at; the client draws the segment under way at its render time. The
    /// <see cref="ActionKind.Throw"/> action (D328): a throw at a target with a click's fields, whatever the throw mode is.
    /// Version 82 (D329): the <see cref="ActionKind.PutDown"/> action, the drop key putting the held item down at the crosshair's point.
    /// Version 83 (DV422): the keybinding codes <see cref="KeybindingCode.ZLevelUp"/> and <see cref="KeybindingCode.ZLevelDown"/> (tg's Move Upwards
    /// and Move Downwards, on Space and Ctrl), which replace the dev command the client sent for them.
    /// Version 84 (gaps E2a, MessagesV84.cs): the <see cref="WeatherMessage"/>, the weather of the own mob's level (tg's SSweather): each
    /// running weather's stage, overlay state, colour, particles with their severity and wind, and the cells it covers.
    /// Version 85 (D335): the <see cref="Hello"/> may end with the hub's login token (flags bit 1), by which a server that holds the hub's key
    /// names the player; such a server refuses a Hello without one with <see cref="DisconnectReason.LoginRequired"/>. A Hello has its own
    /// size cap (<c>Codec.MaxHelloBytes</c>).
    /// Version 86 (DV503): the context hint action <see cref="ContextHintAction.Extra"/> and the <see cref="ActionKind.ContextPick"/> it sends, the
    /// radial's own entries that are no click (the table's Flip, which the right click no longer does).
    /// Version 87 (D336, the map editor's ME7; MessagesV87.cs): MapGrid ends with the map's own sky and sun (<see cref="MapLook"/>), when its
    /// map json sets them; and a world has up to 80 levels (<see cref="Limits.MaxMapLevels"/>, 16 before), a map's own 64 and the levels a round adds.
    /// Version 88 (DV527): a <see cref="ShotAim"/> ends with the render tick the client drew the world at (<see cref="ShotAim.Seen"/>), so the
    /// server tests an aimed shot against the others where the shooter saw them (lag compensation).
    /// Version 89 (DV534): the own motion parameters end with a handhold flags byte (<c>HaltsAtHandhold</c>, <c>SteersByHandhold</c>): the client
    /// predicts that a weightless mob steers only with a handhold within a metre and that its drift halts there.
    /// Version 89 also (DV179, MessagesV89.cs): the private block's flags say when the own mob's hands are blocked (tg's ClickOn refuses its
    /// clicks: incapacitated, or <c>TRAIT_HANDS_BLOCKED</c>), so the client guesses no click's outcome then.
    /// Version 90 (DV573): the posture byte's <see cref="PostureBits.Buckled"/>, a mob buckled to an object (a bed, a chair, a vehicle), which the
    /// client draws where it draws that object.
    /// Version 91 (the owner, 2026-11-20, revising D119; MessagesV91.cs): MapGrid ends with the height of every level's walls and ceiling, the server's
    /// setting (<see cref="MapGrid.WallHeightMillimetres"/>), which the client builds its world with.
    /// Version 92 (DV96, the owner 2026-11-21; MessagesV92.cs): the GunSight message, the own gun's cone and the own shots' hits, for the shooter's
    /// crosshair.
    /// Version 93 (DV585): the keybinding code <see cref="KeybindingCode.Scoreboard"/>, the deathmatch scoreboard held on a key.
    /// Version 94 (D347): the magic of <see cref="Hello"/> is <c>"ANOM"</c>, no longer <c>"SS3D"</c>.
    /// Version 95 (DV586, MessagesV95.cs): the private block names the own mob's melee click cooldown when it is not tg's <c>CLICK_CD_MELEE</c>
    /// (the held thing's <c>attack_speed</c> with the mob's modifiers), so the client swings and whooshes only on a click the server takes as a blow.
    /// Version 96 (MessagesV96.cs): MapGrid and the Delta body end with the turfs' own lights (<see cref="TurfLightChange"/>): a light floor,
    /// a circuit floor or lava lights the room, and a ceiling's floor block lights the room under it.
    /// Version 97 (DV597): the Hello's flag <see cref="Hello.WaitsForLoad"/> and the action <see cref="ActionKind.Loaded"/>: the game's client
    /// loads the world behind its loading screen before its new player is late-joined.
    /// Version 98 (package T, slice T4a, DV30; Codec.V98.cs): a click's and a throw's aim zone gains <see cref="AimZone.Eyes"/>,
    /// <see cref="AimZone.Mouth"/> and <see cref="AimZone.Groin"/>, the modifiers byte's spare bit 3 its high bit.
    /// Version 98 also (package T, slice T4b, DV30): a hover on an entity may end with the zone looked at (one byte, 1 to 9), which the server
    /// selects without logging before tg's hover, so the hints name what a click there does.
    /// Version 98 also (package T, slice T4c, DV600): the posture byte's bits 6 and 7 (<see cref="PostureBits.SideFixed"/>,
    /// <see cref="PostureBits.FaceUp"/>), the side a lying body was turned to from the radial's Turn over (a ContextPick of that label).
    /// Version 98 also (ui-overhaul.md section 10, slice 3A, DV603; MessagesV98Hotbar.cs): the Hotbar message, the own human's ten hotbar cells
    /// and the held items' names, and the <see cref="ActionKind.HotbarPress"/> action, a press of one cell.
    public const ushort Current = 98;

    /// <summary>The length of <see cref="Magic"/>.</summary>
    public const int MagicLength = 4;

    /// <summary>The four bytes <c>"ANOM"</c> that start every <see cref="Hello"/>; anything else is refused unread.</summary>
    public static ReadOnlySpan<byte> Magic => "ANOM"u8;
}
