namespace Anomaly.Protocol;

// Owner: M1 P2 (contract: M1 contracts step). The values are scope 2e's; P2 and P3 may tune a default and record it in
// docs/ai/specs/protocol.md, but never remove a cap.

/// <summary>
/// Every cap of the protocol (scope 2e "Validation and limits", D40, D65). Inbound caps protect the server from clients;
/// outbound caps protect clients (and bots) from a hostile or broken server: a decoder never allocates past them, and a
/// count is also checked against the bytes left in the message before anything is allocated for it. A violation is a
/// <see cref="ProtocolException"/> in the codec, a strike or a refusal in the server host.
/// </summary>
public static class Limits
{
    // ---- client to server (checked by the server host on its own thread, before anything is queued) ----

    /// <summary>The largest message a client may send (scope 2e: 256 B); a bigger one is refused unread and is a strike.</summary>
    public const int MaxInboundMessageBytes = 256;

    /// <summary>
    /// The longest player name, in characters: tg's <c>MAX_NAME_LEN</c> (code/__DEFINES/say.dm:127). Control characters are
    /// refused (<see cref="DisconnectReason.InvalidName"/>).
    /// </summary>
    public const int MaxNameChars = 42;

    /// <summary>The byte cap of the name in <see cref="Hello"/>: <see cref="MaxNameChars"/> UTF-8 characters of 4 bytes at most.</summary>
    public const int MaxNameBytes = MaxNameChars * 4;

    /// <summary>The length of a <see cref="SessionToken"/> on the wire.</summary>
    public const int TokenBytes = 16;

    /// <summary>Version 85 (D335): the byte cap of a <see cref="Hello.LoginToken"/> (the hub's game token for the longest server address is about 650 bytes).</summary>
    public const int MaxLoginTokenBytes = 768;

    /// <summary>Protocol violations (an unknown id, a decode error, a command before Welcome, an oversized message) that disconnect a session ...</summary>
    public const int MaxStrikes = 3;

    /// <summary>... within this many ticks (10 s at 20 TPS).</summary>
    public const int StrikeWindowTicks = 200;

    /// <summary>The host's token bucket for <see cref="InputFrame"/> per session: 60 per second, bursts of 30 (scope 2e). tg's own keysend flood rule runs on top, inside the world.</summary>
    public static readonly RateLimit InputFrameRate = new(60, 30);

    /// <summary>At most this many commands of one session are queued for one tick; the rest are refused (a strike).</summary>
    public const int MaxQueuedCommandsPerSessionPerTick = 16;

    /// <summary>Connection requests waiting for an answer at once; more are refused with <see cref="DisconnectReason.ServerFull"/>.</summary>
    public const int MaxPendingConnectionRequests = 64;

    /// <summary>The default of the server's <c>max_players</c> (150 bots and the clients of the M1 soak fit).</summary>
    public const int DefaultMaxPlayers = 200;

    // ---- server to client (checked by every decoder: the Godot client, the bots, the tests) ----

    /// <summary>The largest message a client accepts (a join Snapshot of a full map is the biggest).</summary>
    public const int MaxOutboundMessageBytes = 16 * 1024 * 1024;

    /// <summary>The byte cap of <see cref="ServerText.Html"/> (tg chat HTML; tg's MAX_MESSAGE_LEN of 1024 is per player message, server text can be longer).</summary>
    public const int MaxServerTextBytes = 16 * 1024;

    /// <summary>The byte cap of <see cref="ServerText.Type"/> (tg's MESSAGE_TYPE_* text).</summary>
    public const int MaxServerTextTypeBytes = 64;

    /// <summary>The byte cap of <see cref="DisconnectInfo.Text"/>.</summary>
    public const int MaxDisconnectTextBytes = 1024;

    /// <summary>The byte cap of an entity's <see cref="NetField.Name"/>.</summary>
    public const int MaxEntityNameBytes = 512;

    /// <summary>The largest <see cref="NetField.GlideSize"/> (tg clamps glide sizes to 1 through <c>ICON_SIZE_ALL</c>, 32; the cap leaves room).</summary>
    public const float MaxGlideSize = 256f;

    /// <summary>The byte cap of a DM type path in a type table.</summary>
    public const int MaxTypePathBytes = 256;

    /// <summary>The most entries of a connection's type table (and of <see cref="MapGrid.TurfTypes"/>).</summary>
    public const int MaxTypeTableEntries = 65536;

    /// <summary>The most entity records (spawned, changed or despawned) one Snapshot or Delta may hold.</summary>
    public const int MaxEntitiesPerMessage = 262144;

    /// <summary>The largest map width and height (tg maps are at most 255 x 255, D23).</summary>
    public const int MaxMapSize = 255;

    /// <summary>
    /// The most z levels of a world: a map's own (at most 64, <c>MapDocument.MaxLevels</c>: the map editor makes no more) and the levels a round adds to
    /// them (CentCom, the mining levels, the reserved ones).
    /// </summary>
    public const int MaxMapLevels = 80;

    /// <summary>Version 87: the most bytes of a map's sky picture (<see cref="MapLook.Image"/>).</summary>
    public const int MaxSkyImageBytes = 4 * 1024 * 1024;

    // ---- protocol version 2 (M2 contracts; P5 and P6 may tune a default and record it in protocol.md, never remove a cap) ----

    // client to server

    /// <summary>Input frames in one <see cref="MotionInputMessage"/>: the newest frame and up to three before it (scope 2d).</summary>
    public const int MaxInputFramesPerMessage = 4;

    /// <summary>The largest <see cref="MotionInputMessage"/> (well under the 256 of <see cref="MaxInboundMessageBytes"/> and LiteNetLib's 1023 unreliable bytes).</summary>
    public const int MaxMotionInputMessageBytes = 64;

    /// <summary>The token bucket for <see cref="MotionInputMessage"/> per session: a client sends one per tick (20 per second), bursts allow a clock nudged 5 % fast and a stalled frame's catch-up.</summary>
    public static readonly RateLimit MotionInputRate = new(30, 15);

    /// <summary>The token bucket for <see cref="ActionMessage"/> per session (clicks, rest, intent toggles, dev commands).</summary>
    public static readonly RateLimit ActionRate = new(100, 100);

    /// <summary>The byte cap of a dev command line (<see cref="ActionMessage.Text"/>).</summary>
    public const int MaxDevCommandBytes = 200;

    /// <summary>
    /// Version 24: the byte cap of a chat link's href (<see cref="ActionKind.Topic"/>'s <see cref="ActionMessage.Text"/>), the dev command's,
    /// so <see cref="MaxActionMessageBytes"/> holds it unchanged. tg's hrefs are a few refs and keys (an embedded object's is under 100 bytes);
    /// a longer link in a chat line is not offered as a link.
    /// </summary>
    public const int MaxTopicHrefBytes = MaxDevCommandBytes;

    /// <summary>The largest <see cref="ActionMessage"/> on the wire (version 4 adds at most <see cref="MaxActionV4Bytes"/>).</summary>
    public const int MaxActionMessageBytes = 1 + 5 + 1 + 10 + 1 + 1 + 2 + MaxDevCommandBytes + MaxActionV4Bytes + MaxActionV15Bytes;

    /// <summary>
    /// Version 17: the largest <see cref="ActionKind.Drop"/> (id, frame seq, kind, two targets of a kind byte and a varuint id of 10, the
    /// button, the modifiers, the icon pixel and the drag time): within <see cref="MaxActionMessageBytes"/>, which a dev command's text sets.
    /// </summary>
    public const int MaxDropActionBytes = 1 + 5 + 1 + 2 * (1 + 10) + 1 + 1 + 2 + 1;

    /// <summary>Version 15: the most bytes a <see cref="ActionKind.RadialPick"/>'s menu id (a varuint of 10) and element (a varuint of 5) add.</summary>
    public const int MaxActionV15Bytes = 10 + 5;

    /// <summary>Version 4: the most bytes the version-4 members of an <see cref="ActionMessage"/> add (target kind 1, turf x and y 1 each, z a varuint of 5, icon-x and icon-y 1 each, the keybinding code 1, the key-up flag 1).</summary>
    public const int MaxActionV4Bytes = 1 + 1 + 1 + 5 + 1 + 1 + 1 + 1;

    /// <summary>
    /// Frames a session's jitter buffer keeps on the server: one more and the oldest is dropped (scope 2d), a step the prediction
    /// took and the server never does. 4 since the M2 integration: with a remote client's target depth of 2, a lost message followed by
    /// the next one's redundant frames and a clock nudge overflowed 3 (measured: 100 ms one way, 2 % loss, 12 x 60 s walks: 5
    /// corrections, 3 of them drops, at 3; 2 corrections and no drop at 4, 5 or 6). 8 since D331 (2026-11-14): with a latency varying
    /// by two ticks the bursts overflowed 4 (one to four drops in 12 s, each a correction; none at 8). The input delay is the depth the
    /// client's clock keeps (its target), not the cap: the cap only bounds a burst.
    /// </summary>
    public const int MaxJitterBufferFrames = 8;

    /// <summary>How far ahead of the last applied frame a frame's seq may be before it is refused as forged (a strike): a minute of frames.</summary>
    public const int MaxInputSeqLead = 1200;

    // server to client

    /// <summary>The most entries of a public resource table or of one session's private resource table.</summary>
    public const int MaxResourceTableEntries = 65536;

    /// <summary>The byte cap of a resource (an icon path or a GAGS reference).</summary>
    public const int MaxResourceBytes = 512;

    /// <summary>The most entries of a public string table or of one session's private string table.</summary>
    public const int MaxStringTableEntries = 1 << 20;

    /// <summary>The byte cap of a table string (an icon state; tg's longest are about 60 bytes). Version 18: 1024, for maptext (tg's <c>MAPTEXT_TINY_UNICODE</c> macro alone is 130 bytes).</summary>
    public const int MaxTableStringBytes = 1024;

    /// <summary>
    /// The most entries of a public appearance table or of one session's private appearance table (262144; a Snapshot carries a
    /// table whole, and a decoded entry is an object of about 100 bytes, so the cap bounds what a message makes a client allocate).
    /// </summary>
    public const int MaxAppearanceTableEntries = 1 << 18;

    /// <summary>Overlays, or underlays, of one appearance entry (tg's human has 23 overlay layers, several entries each).</summary>
    public const int MaxAppearanceOverlays = 256;

    /// <summary>Version 25: the filters of one appearance (tg stacks a few: a colour, an outline, a mask).</summary>
    public const int MaxAppearanceFilters = 16;

    /// <summary>New table entries (resources, strings, appearances together) in one Delta, Snapshot or private block.</summary>
    public const int MaxNewTableEntriesPerMessage = 1 << 20;

    /// <summary>
    /// Cell changes in one Delta, all lists together: <see cref="Delta.SolidChanged"/>, <see cref="Delta.TurfAppearanceChanged"/>
    /// and, from version 3, <see cref="Delta.CellFlagsChanged"/>, <see cref="Delta.BaseLightChanged"/> and
    /// <see cref="Delta.TurfTypeChanged"/>.
    /// </summary>
    public const int MaxCellChangesPerDelta = 65536;

    /// <summary><c>client.screen</c> entries of one session (added, changed and removed in one block each, and in all).</summary>
    public const int MaxScreenEntries = 512;

    /// <summary>The byte cap of a <see cref="ScreenEntry.ScreenLoc"/>.</summary>
    public const int MaxScreenLocBytes = 128;

    /// <summary>Version 12: the clickable <c>vis_contents</c> children of one screen entry (<see cref="ScreenEntry.VisChildren"/>; tg's slots hold one item).</summary>
    public const int MaxScreenChildren = 16;

    /// <summary>The largest speed the private block may carry, in tiles per tick (<c>LocomotionSeam.SetSpeedScale</c> allows 4 at most, and tg's slowest step is a tick).</summary>
    public const float MaxTilesPerTick = 8f;

    /// <summary>The largest capsule radius the private block may carry, in tiles.</summary>
    public const float MaxCapsuleRadius = 4f;

    /// <summary>The largest coordinate of <see cref="NetField.Pos"/> in 1/256 tile (the map's edge plus a tile: 256 * 256).</summary>
    public const int MaxPosSubunits = (MaxMapSize + 1) * 256;

    // ---- protocol version 3 (M3 contracts; P5 may tune a default and record it in protocol.md, never remove a cap) ----

    // server to client

    /// <summary>Entries of the base-light table (<see cref="MapGrid.BaseLights"/> plus every Delta's new ones; Meta's 171 areas have a handful of distinct values).</summary>
    public const int MaxBaseLightEntries = 4096;

    /// <summary>Turf types of the map's turf type table including the ones the Deltas add (<see cref="Delta.NewTurfTypes"/>); the same cap as a type table.</summary>
    public const int MaxTurfTypeEntries = MaxTypeTableEntries;

    /// <summary>New turf types in one Delta.</summary>
    public const int MaxNewTurfTypesPerDelta = 1024;

    /// <summary>The largest <see cref="LightState.Range"/> in tiles (tg's map lights reach 8; admin and event lights more): a decoder refuses more.</summary>
    public const float MaxLightRange = 32f;

    /// <summary>The largest size of <see cref="LightState.Power"/> (tg's negative powers darken).</summary>
    public const float MaxLightPower = 64f;

    /// <summary>The largest size of <see cref="LightState.Height"/>.</summary>
    public const float MaxLightHeight = 16f;

    /// <summary>The largest size of <see cref="LightState.OffsetX"/> and <see cref="LightState.OffsetY"/> in tiles.</summary>
    public const float MaxLightOffset = 16f;

    // ---- protocol version 4 (M4a contracts; P6 may tune a default and record it in protocol.md, never remove a cap) ----

    // client to server: the ActionMessage's version-4 members (MaxActionV4Bytes above); the per-kind rates are the host's
    // ActionLimits (Keybinding, Hover).

    /// <summary>The largest tg <c>icon-x</c> and <c>icon-y</c> (<see cref="ActionMessage.IconX"/>): tg's <c>ICON_SIZE_X</c> and <c>ICON_SIZE_Y</c>, 32 pixels.</summary>
    public const int MaxIconPixel = 32;

    // server to client

    /// <summary>Entries of one <see cref="ContextHintsMessage"/> (tg has 8 context keys).</summary>
    public const int MaxContextHints = 10;

    /// <summary>The byte cap of a <see cref="ContextHint.Label"/> (tg's labels are a few words: "Weld shut", "Swipe ID").</summary>
    public const int MaxContextLabelBytes = 64;

    /// <summary>The byte cap of <see cref="ContextHintsMessage.Name"/>.</summary>
    public const int MaxContextNameBytes = 256;

    /// <summary>The largest <see cref="ContextHintsMessage"/> on the wire.</summary>
    public const int MaxContextHintsMessageBytes = 1 + 1 + 10 + 1 + 1 + 5 + 3 + MaxContextNameBytes + 1 + MaxContextHints * (1 + 1 + 1 + 2 + MaxContextLabelBytes);

    /// <summary>Private images (<c>client.images</c>, <see cref="ImageEntry"/>) of one session: in all, and in each of the added, changed and removed lists of one private block.</summary>
    public const int MaxPrivateImages = 512; // version 30: 64 before; a T-ray scanner's pulse shows every hidden object within 3 tiles

    /// <summary>Version 27: the byte cap of <see cref="ImageEntry.HolderName"/> (the sender cuts a longer name).</summary>
    public const int MaxImageHolderNameBytes = 128;

    /// <summary>The largest <see cref="ScreenEntry.Group"/>: tg's <c>SCREEN_GROUP_AMT</c> (<see cref="HudGroup.Last"/>).</summary>
    public const byte MaxHudGroup = HudGroup.Last;

    // ---- protocol version 5 (M4b slice 3: the gas visuals and the dev gas probe, MessagesV5.cs) ----

    /// <summary>Entries of <see cref="MapGrid.GasKinds"/> (tg has 12 gas overlay icon states).</summary>
    public const int MaxGasKinds = 32;

    /// <summary>The byte cap of a gas kind (an icon state: "water_vapor").</summary>
    public const int MaxGasKindBytes = 32;

    /// <summary>Gases one cell shows at once (<see cref="CellGasChange.Gases"/>; tg has 13 gases with an overlay).</summary>
    public const int MaxGasesPerCell = 16;

    /// <summary>The largest <see cref="GasLevel.Level"/>: tg's <c>TOTAL_VISIBLE_STATES</c>.</summary>
    public const int MaxGasLevel = GasVisual.TotalVisibleStates;

    /// <summary>The half side of a <see cref="DevGasReport"/>: a probe answers the (2r + 1) x (2r + 1) cells around its point (tg's view: 15 x 15).</summary>
    public const int DevGasProbeRadius = 7;

    /// <summary>Entries of <see cref="DevGasReport.Gases"/>, and gases of one of its cells (tg has 22 gases).</summary>
    public const int MaxDevGases = 32;

    /// <summary>The byte cap of a <see cref="DevGasInfo.Id"/> and of its <see cref="DevGasInfo.Name"/>.</summary>
    public const int MaxDevGasTextBytes = 48;

    /// <summary>The largest <see cref="DevGasReport"/> on the wire.</summary>
    public const int MaxDevGasReportBytes = 1 + 1 + 1 + 5 + 1 + 1 + 1 + MaxDevGases * (2 * (5 + MaxDevGasTextBytes) + 4)
        + (2 * DevGasProbeRadius + 1) * (2 * DevGasProbeRadius + 1) * (1 + 4 + 4 + 1 + MaxDevGases * 5);

    // ---- protocol version 6 (the airflow streams, MessagesV6.cs) ----

    /// <summary>Runs of one <see cref="AirflowUpdate"/>.</summary>
    public const int MaxAirflowRuns = 2048;

    /// <summary>Cells of one <see cref="AirflowRun"/> (a run covers part of one row of the map).</summary>
    public const int MaxAirflowRunCells = MaxMapSize;

    /// <summary>Cells of one <see cref="AirflowUpdate"/>, over all its runs (the sender leaves the rest for the next tick).</summary>
    public const int MaxAirflowCells = 4096;

    /// <summary>Version 7: floor devices of one <see cref="AirflowUpdate"/>.</summary>
    public const int MaxAirflowVents = 512;

    /// <summary>The largest <see cref="AirflowUpdate"/> on the wire: the id, the run count, every run's header (z, y, x0, count) and the cells, then the vent count and every vent (z, y, x, byte).</summary>
    public const int MaxAirflowBytes = 1 + 2 + MaxAirflowRuns * (1 + 1 + 1 + 2) + MaxAirflowCells + 2 + MaxAirflowVents * 4;

    // ---- protocol version 9 (animate(), MessagesV9.cs) ----

    /// <summary>
    /// Tracks of one <see cref="EntityAnimation"/> (a main chain and parallel ones; the replicator drops the oldest). Version 26: 8, so a
    /// screen entry carries its vis_contents children's filter chains beside its own (tg's health doll pulses up to six limbs).
    /// </summary>
    public const int MaxAnimationTracks = 8;

    /// <summary>
    /// Steps of one <see cref="AnimationTrack"/> (the replicator does not send a longer chain: the client shows its end). Version 32: 36,
    /// tg's finest SpinAnimation, the orbiter component's default of 36 segments (orbiter.dm:14, energy_ball.dm:153).
    /// </summary>
    public const int MaxAnimationSteps = 36;

    /// <summary>The largest finite <see cref="AnimationTrack.Loop"/> (0 is for ever).</summary>
    public const int MaxAnimationLoop = 65535;

    /// <summary>The longest step time or delay, in deciseconds (an hour).</summary>
    public const float MaxAnimationTime = 36000f;

    /// <summary>The largest magnitude of an animated number (a pixel offset, an alpha, a transform's number).</summary>
    public const float MaxAnimatedValue = 1_000_000f;

    /// <summary>The most bytes one <see cref="AnimatedVars"/> takes: the mask, five floats, the colour and the six transform numbers.</summary>
    public const int MaxAnimatedVarsBytes = 1 + 5 * 4 + 4 + 6 * 4;

    /// <summary>Version 26: the most bytes one <see cref="FilterVars"/> takes: the mask, eleven numbers, the colour and a colour matrix.</summary>
    public const int MaxAnimatedFilterVarsBytes = 2 + 11 * 4 + 4 + 20 * 4;

    // ---- protocol version 13 (tg's sounds, MessagesV13.cs) ----

    /// <summary>Events of one <see cref="Sounds"/> message, and the server's budget of a session's events per tick (the quietest plays are dropped first).</summary>
    public const int MaxSoundEvents = 64;

    /// <summary>New paths of one <see cref="Sounds"/> message (the server's budget of new paths per session per tick).</summary>
    public const int MaxSoundPaths = 32;

    /// <summary>The byte cap of a sound path (tg's resource path, <c>sound/machines/airlock/airlock.ogg</c>).</summary>
    public const int MaxSoundPathBytes = 256;

    /// <summary>Entries of a session's path table (tg has about 3800 sound files).</summary>
    public const int MaxSoundTableEntries = 65536;

    /// <summary>The highest channel: BYOND's 1024 (tg's <c>CHANNEL_LOBBYMUSIC</c>).</summary>
    public const int MaxSoundChannel = 1024;

    /// <summary>The largest <see cref="SoundEvent.Volume"/>: 100 (BYOND's most) in half units.</summary>
    public const int MaxSoundVolume = 200;

    /// <summary>The highest <see cref="SoundEvent.Environment"/>: BYOND's last preset (tg's <c>SOUND_ENVIRONMENT_PSYCHOTIC</c>, 25).</summary>
    public const int MaxSoundEnvironment = 25;

    /// <summary>The largest magnitude of a <see cref="SoundEvent.Frequency"/> (a rate in Hz; tg's random ones reach 55000).</summary>
    public const float MaxSoundFrequency = 1_000_000f;

    /// <summary>The largest <see cref="SoundEvent.Offset"/> (an hour in deciseconds).</summary>
    public const float MaxSoundOffset = 36000f;

    /// <summary>The largest <see cref="Sounds"/> message: the id, the paths (count, first, each string) and the events (head, channel, path, volume, muffle, occlusion, environment, x, y, z, frequency, offset).</summary>
    public const int MaxSoundsBytes = 1 + 1 + 5 + MaxSoundPaths * (2 + MaxSoundPathBytes) + 1 + MaxSoundEvents * (1 + 2 + 5 + 4 + 1 + 1 + 5 + 4 + 4);

    // ---- protocol version 15 (tg's radial menu, MessagesV15.cs) ----

    /// <summary>Slices of one radial page: tg's <c>max_elements</c> is <c>zone / min_angle</c>, 8 for a whole ring at 45 degrees; 16 leaves room for a denser menu.</summary>
    public const int MaxRadialElements = 16;

    /// <summary>The byte cap of a slice's name (tg's <c>E.name</c>: a choice's text or an atom's name).</summary>
    public const int MaxRadialNameBytes = 128;

    /// <summary>The largest <see cref="RadialMenuMessage"/>: the id, kind, menu id, flags, page, pages, the centre's two refs, then each slice's name, flags and three refs.</summary>
    public const int MaxRadialMenuBytes = 1 + 1 + 10 + 1 + 1 + 1 + 2 * 5 + 1 + MaxRadialElements * (2 + MaxRadialNameBytes + 1 + 3 * 5);

    // ---- protocol version 16 (tg's tgui windows, MessagesV16.cs) ----

    /// <summary>tgui windows per client: tg's pool (<c>TGUI_WINDOW_UNLIMITED_LIMIT</c>, 200; the hard limit without the preference is 9).</summary>
    public const int MaxUiWindows = 200;

    /// <summary>
    /// M10 slice F (DV44): the window index of tg's lobby menu (<c>/datum/lobby_menu</c>'s browser window <c>"lobby_menu"</c>, not one of
    /// tgui's pooled windows): the last index, which tg's pool never reaches (its hard limit is 9, its unlimited preference 200 counts
    /// from 1 and the lobby's window comes first). Its interface is <see cref="LobbyUiInterface"/>.
    /// </summary>
    public const int LobbyUiWindow = MaxUiWindows;

    /// <summary>M10 slice F: the interface name of the lobby menu's window (tg's tgui-lobby package, <c>LobbyMenu.tsx</c>).</summary>
    public const string LobbyUiInterface = "LobbyMenu";

    /// <summary>
    /// M10x slice G4 (protocol version 52, D282): the window index of tg's escape menu (<c>/datum/escape_menu</c>'s browser window
    /// <c>"escape_menu"</c>, not one of tgui's pooled windows): the one below the lobby's, which tg's pool never reaches either. Its
    /// interface is <see cref="EscapeMenuUiInterface"/>; the client draws it as its own Esc menu, never as a window.
    /// </summary>
    public const int EscapeMenuUiWindow = MaxUiWindows - 1;

    /// <summary>M10x slice G4: the interface name of the escape menu's window (tg's tgui-escape-menu package).</summary>
    public const string EscapeMenuUiInterface = "EscapeMenu";

    /// <summary>
    /// M10x slice G6 (DV216): the first window index of tg's other browse() pages (a ticket panel, the ticket lists: a window name each,
    /// <see cref="BrowseUiWindows"/> of them a client, up to <see cref="EscapeMenuUiWindow"/>); tgui's pooled windows stop below it.
    /// Their interface is <see cref="BrowseUiInterface"/>.
    /// </summary>
    public const int BrowseUiWindowFirst = EscapeMenuUiWindow - BrowseUiWindows;

    /// <summary>M10x slice G6: how many browse() windows a client has open at once (a new one past them replaces the oldest).</summary>
    public const int BrowseUiWindows = 16;

    /// <summary>
    /// M10x slice G6: the interface of a browse() window: its data is <c>{"html": [the page in pieces of at most
    /// <see cref="MaxUiStringBytes"/>], "width", "height"}</c>, its title the page's.
    /// </summary>
    public const string BrowseUiInterface = "Browser";

    /// <summary>The byte cap of an interface name (tg's interface: <c>"Canister"</c>, <c>"NumberInputModal"</c>).</summary>
    public const int MaxUiInterfaceBytes = 64;

    /// <summary>The byte cap of a window's title (tg's <c>title</c>: usually the object's name).</summary>
    public const int MaxUiTitleBytes = 256;

    /// <summary>The byte cap of one string in a payload (a name, a message, a description).</summary>
    public const int MaxUiStringBytes = 4096;

    /// <summary>The byte cap of an object's field name.</summary>
    public const int MaxUiKeyBytes = 128;

    /// <summary>How deep a payload's arrays and objects may nest (tg's deepest interfaces nest about 6).</summary>
    public const int MaxUiDepth = 16;

    /// <summary>The most values one payload may hold (each null, number, string, array and object counts once).</summary>
    public const int MaxUiValues = 32768;

    /// <summary>The largest <see cref="UiWindowMessage"/> (tg's payloads are a few kB; the stack recipes of a large stack about 20).</summary>
    public const int MaxUiWindowBytes = 256 * 1024;

    /// <summary>The byte cap of a <see cref="ActionKind.UiAct"/>'s action (tg's <c>act()</c> names are short words).</summary>
    public const int MaxUiActionBytes = 48;

    /// <summary>
    /// The byte cap of a <see cref="ActionKind.UiAct"/>'s encoded params. With the window (a varuint of 5) and the action (a varuint of 1
    /// and its bytes) a UiAct body is at most 5 + 1 + 48 + 140 = 194 bytes, under the dev command's 200, so <see cref="MaxActionMessageBytes"/>
    /// (and <see cref="MaxInboundMessageBytes"/>) hold it unchanged.
    /// </summary>
    public const int MaxUiParamsBytes = 140;

    // ---- protocol version 19 (tg's tooltips, MessagesV19.cs) ----

    /// <summary>The byte cap of a tooltip's text (tg's <c>"&lt;h1&gt;name&lt;/h1&gt;&lt;p&gt;desc&lt;/p&gt;"</c>: a name and a description, a few hundred bytes).</summary>
    public const int MaxTooltipTextBytes = 4096;

    /// <summary>The byte cap of a tooltip's theme (a class of tg's <c>tooltip.html</c>: <c>"trasen-knox"</c> is the longest, 11).</summary>
    public const int MaxTooltipThemeBytes = 32;

    /// <summary>The largest <see cref="TooltipMessage"/>: the id, the kind, the text and the theme with their varuint lengths.</summary>
    public const int MaxTooltipBytes = 1 + 1 + 3 + MaxTooltipTextBytes + 1 + MaxTooltipThemeBytes;

    // ---- protocol version 21 (the camera shake, MessagesV21.cs) ----

    /// <summary>The most steps of a <see cref="CameraShakeMessage"/> (tg's shake: about one a decisecond of its duration, 1.5 to 6 s).</summary>
    public const int MaxCameraShakeSteps = 256;

    /// <summary>The largest view offset of a shake step, pixels either way (a strength of 128 tiles: tg caps the shakes at a few).</summary>
    public const int MaxCameraShakePixels = 4096;

    /// <summary>The longest shake step, deciseconds (a minute).</summary>
    public const int MaxCameraShakeStepTime = 600;

    /// <summary>The largest <see cref="CameraShakeMessage"/>: the id, the count and each step's two varints of 2 bytes and its varuint of 2.</summary>
    public const int MaxCameraShakeBytes = 1 + 1 + 2 + MaxCameraShakeSteps * 6;

    // ---- protocol version 34 (M7x slice A, MessagesV34.cs) ----

    /// <summary>Version 34: the largest east or north of a <see cref="ShotAim"/>, 1/256 tile: one tile past the map's edge.</summary>
    public const int MaxAimCoordinate = (MaxMapSize + 1) * 256;

    /// <summary>Version 34: the largest <see cref="ShotState.OriginX"/> or <see cref="ShotState.OriginY"/>, tile pixels: one tile past the map's edge.</summary>
    public const int MaxShotCoordinate = (MaxMapSize + 1) * 32;

    /// <summary>
    /// Version 34: <see cref="ActionKind.MouseDrag"/>, the aim while a button is held (the client sends at most one per <c>MouseDragInterval</c>,
    /// 0.1 s). The server keeps only the newest pending drag; one over this rate is dropped silently, and drags take no token of the overall
    /// action bucket (<see cref="ActionRate"/>), so holding the trigger never crowds out clicks.
    /// </summary>
    public static readonly RateLimit MouseDragRate = new(10, 10);

    // ---- protocol version 38 (M9 slice A, speech, MessagesV38.cs) ----

    /// <summary>Version 38: the most characters (UTF-16 code units, as tg's window counts them) of a say entry: tg's <c>MAX_MESSAGE_LEN</c>.</summary>
    public const int MaxSayEntryChars = 1024;

    /// <summary>Version 38: the most UTF-8 bytes of a say entry: <see cref="MaxSayEntryChars"/> code units take at most 3 bytes each.</summary>
    public const int MaxSayEntryBytes = 3 * MaxSayEntryChars;

    /// <summary>Version 38: the most bytes of a say message's type or channel (tg's are short words: "thinking", "Radio").</summary>
    public const int MaxSayWordBytes = 16;

    /// <summary>
    /// Version 38: the largest <see cref="ActionKind.Say"/> message: the action's head, the type, the channel, the entry and the flag. A say
    /// action may be this long; every other action keeps <see cref="MaxActionMessageBytes"/>.
    /// </summary>
    public const int MaxSayActionBytes = 1 + 5 + 1 + 2 * (1 + MaxSayWordBytes) + 2 + MaxSayEntryBytes + 1;

    /// <summary>
    /// Version 38: <see cref="ActionKind.Say"/> messages, outside the overall action bucket (the window sends a typing message as keys are
    /// typed): one over this rate is a rate limit like any action's.
    /// </summary>
    public static readonly RateLimit SayRate = new(10, 20);

    /// <summary>Version 39: the most bytes of an <see cref="ActionKind.Emote"/>'s key (tg's keys are short words: "scratchhead").</summary>
    public const int MaxEmoteKeyBytes = 32;

    /// <summary>Version 38: the largest <see cref="SayModalMessage"/>.</summary>
    public const int MaxSayModalBytes = 1 + 1 + 3 + 1;

    /// <summary>Version 40: the most channels a <see cref="RadioChannelsMessage"/> lists (tg's department radio keys are fewer).</summary>
    public const int MaxRadioChannels = 32;

    /// <summary>Version 40: the largest <see cref="RadioChannelsMessage"/>: the id, the count and three bytes a prefix.</summary>
    public const int MaxRadioChannelsBytes = 1 + 1 + MaxRadioChannels * 4;

    /// <summary>Version 38: the most bytes of a <see cref="RunechatMessage.Maptext"/> (tg clips the text at the max_chat_length preference, then adds its spans).</summary>
    public const int MaxRunechatBytes = 4096;

    /// <summary>Version 38: the longest <see cref="RunechatMessage.Lifespan"/>, deciseconds.</summary>
    public const int MaxRunechatLifespan = 600;

    /// <summary>Version 38: the largest <see cref="RunechatMessage"/>.</summary>
    public const int MaxRunechatMessageBytes = 1 + 10 + 3 + MaxRunechatBytes + 3;

    /// <summary>Version 63: the most UTF-8 bytes of a <see cref="TtsMessage.Text"/> (a say entry's).</summary>
    public const int MaxTtsTextBytes = MaxSayEntryBytes;

    /// <summary>Version 63: the most UTF-8 bytes of a <see cref="TtsMessage.Voice"/>.</summary>
    public const int MaxTtsVoiceBytes = 64;

    /// <summary>Version 63: tg's pitch preference's bound, semitones either way.</summary>
    public const int MaxTtsPitch = 12;

    /// <summary>Version 63: the loudest <see cref="TtsMessage.Volume"/> (tg's 85 plus a line's offset).</summary>
    public const int MaxTtsVolume = 200;

    /// <summary>Version 64: the most UTF-8 bytes of a <see cref="TtsMessage.Filter"/> (tg's longest filter graphs are under 400).</summary>
    public const int MaxTtsFilterBytes = 1024;

    /// <summary>Version 63 (64: the filter and the flags): the largest <see cref="TtsMessage"/>.</summary>
    public const int MaxTtsMessageBytes = 1 + 10 + 3 + MaxTtsTextBytes + 1 + MaxTtsVoiceBytes + 2 + 2 + 2 + MaxTtsFilterBytes + 1;

    /// <summary>Version 42: the largest <see cref="MotionMessage"/>, under the unreliable channel's single packet (Anomaly.Net's 1023 bytes).</summary>
    public const int MaxMotionBytes = 1000;

    /// <summary>Version 42: the most bytes one <see cref="MotionEntry"/> takes (an id step of up to 10 bytes, the position's 4, the yaw's 1).</summary>
    public const int MaxMotionEntryBytes = 15;

    /// <summary>
    /// Version 42: how many ticks the server resends a capsule mob's position after it last changed, so that a lost Motion is covered by
    /// the next ones (a second: at 2% loss twenty in a row are never all lost; a longer stall of the reliable channel is the case it covers).
    /// </summary>
    public const int MotionResendTicks = 20;

    /// <summary>Version 43: the most accesses in the own access list (tg's every access is about 90; an ID with them all fits).</summary>
    public const int MaxAccessEntries = 256;

    /// <summary>Version 43: the most accesses in one list of a <see cref="DoorState"/> (tg's mapping helpers set a handful).</summary>
    public const int MaxDoorAccessEntries = 32;

    /// <summary>Version 43: the most UTF-8 bytes of one access (tg's are short identifiers: <c>maint_tunnels</c>).</summary>
    public const int MaxAccessBytes = 64;

    /// <summary>Version 53: the most UTF-8 bytes of a <see cref="BugReportMessage.Title"/>.</summary>
    public const int MaxBugReportTitleBytes = 300;

    /// <summary>Version 53: the most UTF-8 bytes of a <see cref="BugReportMessage.WhatHappened"/> and of its <see cref="BugReportMessage.Steps"/>.</summary>
    public const int MaxBugReportTextBytes = 8000;

    /// <summary>Version 53: the most UTF-8 bytes of a <see cref="BugReportMessage.Client"/>.</summary>
    public const int MaxBugReportClientBytes = 1024;

    /// <summary>Version 53: the most bytes of a <see cref="BugReportMessage.Image"/> (a WebP screenshot).</summary>
    public const int MaxBugReportImageBytes = 2 * 1024 * 1024;

    /// <summary>Version 53: the largest encoded body of a <see cref="BugReportMessage"/> (five fields with a length of up to 4 bytes each).</summary>
    public const int MaxBugReportBodyBytes = 5 * 4 + MaxBugReportTitleBytes + 2 * MaxBugReportTextBytes + MaxBugReportClientBytes + MaxBugReportImageBytes;

    /// <summary>Version 53: the most body bytes of one <see cref="BugReportPart"/>.</summary>
    public const int MaxBugReportPartBytes = 4096;

    /// <summary>
    /// Version 53: the largest <see cref="BugReportPart"/> message (the id, the total and the offset of up to 4 bytes each, the part's length
    /// and bytes): under the 8 fragments of about 993 bytes a server takes from a client (LiteNetServerTransport.MaxInboundFragments).
    /// </summary>
    public const int MaxBugReportPartMessageBytes = 1 + 4 + 4 + 2 + MaxBugReportPartBytes;

    /// <summary>Version 53: the ticks a key waits between two bug reports (2 minutes).</summary>
    public const int BugReportCooldownTicks = 2 * 60 * 20;

    /// <summary>Version 54: a <see cref="HeldMotionMessage"/>: the id and the kind.</summary>
    public const int MaxHeldMotionBytes = 2;

    /// <summary>Version 67: entries of <see cref="MinimapMessage.Colours"/> (tg's tacmap colours and their blends with the areas': Meta has a few dozen).</summary>
    public const int MaxMinimapColours = 4096;

    /// <summary>Version 67: entries of <see cref="MinimapMessage.AreaNames"/> (Meta has 171 areas).</summary>
    public const int MaxMinimapAreas = 4096;

    /// <summary>Version 67: the most UTF-8 bytes of an area's name in a <see cref="MinimapMessage"/>.</summary>
    public const int MaxMinimapAreaNameBytes = 256;

    /// <summary>Version 67: the most UTF-8 bytes of an area's department code in a <see cref="MinimapMessage"/>.</summary>
    public const int MaxMinimapAreaCodeBytes = 8;

    /// <summary>Version 68: the mobs of a <see cref="MinimapMobsMessage"/> (Meta starts with a few hundred living mobs).</summary>
    public const int MaxMinimapMobs = 4096;

    /// <summary>Version 68: the most UTF-8 bytes of a mob's name in a <see cref="MinimapMobsMessage"/> (the server cuts a longer one).</summary>
    public const int MaxMinimapMobNameBytes = 128;

    /// <summary>Version 70: the most UTF-8 bytes of a <see cref="BuildPreviewMessage"/>'s icon file and of its icon state.</summary>
    public const int MaxBuildPreviewTextBytes = 256;

    /// <summary>Version 78 (DV386): the camera chunks of a private block (an AI's eye sees its view's chunks, a few dozen).</summary>
    public const int MaxStaticChunks = 1024;

    /// <summary>Version 78 (DV387): the popup windows open on one client.</summary>
    public const int MaxPopups = 16;

    /// <summary>Version 78 (DV387): a popup window's name (tg's "camera-[REF]") and an action closing it.</summary>
    public const int MaxPopupNameBytes = 64;

    /// <summary>Version 78 (DV387): a popup window's title.</summary>
    public const int MaxPopupTitleBytes = 128;

    /// <summary>Version 78 (DV387): a popup window's side in pixels.</summary>
    public const int MaxPopupSide = 4096;

    /// <summary>Version 78 (DV387): the second views' areas of a private block (the popups and the AI's six multicamera windows).</summary>
    public const int MaxViewAreas = 32;

    /// <summary>Version 78: the command verbs of one mob.</summary>
    public const int MaxVerbs = 128;

    /// <summary>Version 78: a verb's category or name.</summary>
    public const int MaxVerbNameBytes = 64;

    /// <summary>Version 78: a verb's path (<c>/mob/living/silicon/ai/verb/pick_icon</c>) and an action running it.</summary>
    public const int MaxVerbPathBytes = 128;

    /// <summary>Version 79 (silicons SI4d): the largest runtime resource, a PNG the world made (tg's biggest canvas, 45 x 27, is a few KiB).</summary>
    public const int MaxRuntimeResourceBytes = 64 * 1024;

    /// <summary>Version 84: the weathers of a <see cref="WeatherMessage"/> (tg runs one or two on a level at once).</summary>
    public const int MaxWeatherLayers = 16;

    /// <summary>Version 84: the most UTF-8 bytes of a weather's overlay state or particle set name.</summary>
    public const int MaxWeatherStateBytes = 64;

    /// <summary>Version 92: a <see cref="GunSightMessage"/>: the id, the gun flag, the cone and the hits.</summary>
    public const int MaxGunSightBytes = 1 + 1 + 4 + 1;
}

/// <summary>A token bucket: <see cref="PerSecond"/> tokens refill per second of game time (20 ticks), at most <see cref="Burst"/> are kept.</summary>
public readonly record struct RateLimit(int PerSecond, int Burst);
