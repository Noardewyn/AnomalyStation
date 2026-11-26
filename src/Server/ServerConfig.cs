using System.Globalization;
using System.IO;
using Anomaly.Protocol;

namespace Anomaly.Server;

// Owner: M1 P3 (server host); M2 P6 for the M2 options (dev_commands, move_speed_scale, room_layout). Contract by the M1 and M2
// contracts steps: the options and their defaults.

/// <summary>
/// Every option of the M1 server (scope 2i, 2j; docs/ai/specs/apps.md section 1): read from the config file
/// (<see cref="ConfigFile"/>, tg's <c>KEY value</c> style, one option per line, <c>#</c> comments) and then from the
/// command line (<c>--key value</c> or <c>--key=value</c>), the command line winning. Keys are the lower-case
/// snake_case of the property names (<c>bind_address</c>, <c>max_players</c>, <c>hash_every</c>, ...).
/// <para>
/// The file follows tg's loader (<c>code/controllers/configuration/configuration.dm</c>, <c>LoadEntries</c>): a key is
/// matched without regard to case (the sample file writes them in upper case, as tg's <c>config/*.txt</c> do), the value
/// is the rest of the line after the first space, a line starting with <c>#</c> is a comment, and a key with no value is
/// an enabled flag (a flag also takes <c>0</c>/<c>1</c>). An unknown key is reported as a warning (<see cref="Warnings"/>)
/// and skipped, as tg logs "Unknown setting in configuration". On the command line a key may use <c>-</c> for
/// <c>_</c>, and the short forms of <see cref="Usage"/> (<c>--metrics</c>, <c>--record</c>, <c>--replay</c>, <c>--bind</c>,
/// <c>--room</c>, <c>--smoke</c>, <c>--config</c>, <c>--boot</c>, <c>--no-console</c>) are accepted; flags on the command line
/// take no value.
/// </para>
/// </summary>
public sealed class ServerConfig
{
    /// <summary>The default UDP port.</summary>
    public const int DefaultPort = 7777;

    /// <summary>The default config file (relative to the working directory).</summary>
    public const string DefaultConfigFile = "config/server.txt";

    /// <summary>The config file read before the command line (missing: defaults only).</summary>
    public string ConfigFile { get; init; } = DefaultConfigFile;

    /// <summary>The UDP port to listen on (0: an ephemeral port, for tests).</summary>
    public int Port { get; init; } = DefaultPort;

    /// <summary>The address to bind; loopback by default (scope risk 8: <c>0.0.0.0</c> for LAN play).</summary>
    public string BindAddress { get; init; } = "127.0.0.1";

    /// <summary>tg-style <c>max_players</c>: sessions at once; more connection requests are refused (<see cref="DisconnectReason.ServerFull"/>).</summary>
    public int MaxPlayers { get; init; } = Limits.DefaultMaxPlayers;

    /// <summary>The world seed (D31): fixed for tests and replays; a real server without --seed or SEED draws a random one (Program).</summary>
    public ulong Seed { get; init; } = 1;

    /// <summary>The test room's width and height, rim included (scope 2a: about 48; at most 64 until the boot scaling is fixed, scope risk 3).</summary>
    public int RoomWidth { get; init; } = Anomaly.Core.Tg.TestRoom.DefaultSize;

    /// <inheritdoc cref="RoomWidth"/>
    public int RoomHeight { get; init; } = Anomaly.Core.Tg.TestRoom.DefaultSize;

    /// <summary><c>--empty</c>: run the bare kernel world (no tg boot, no room, no joins), for loop and pacing tests.</summary>
    public bool Empty { get; init; }

    /// <summary>Ticks to run; null runs until a stop request (Ctrl+C, SIGTERM, the console's <c>quit</c>).</summary>
    public long? Ticks { get; init; }

    /// <summary>Pace ticks at 20 per second of wall time (default: fast-forward).</summary>
    public bool Realtime { get; init; }

    /// <summary><c>--metrics FILE</c>: a CSV of per-tick metrics (scope 2i); null: none.</summary>
    public string MetricsFile { get; init; }

    /// <summary>Ticks between status lines in the log (scope 2i: every 10 s).</summary>
    public int StatusEveryTicks { get; init; } = 200;

    /// <summary><c>--hash-every N</c>: a state hash every N ticks (0, the default, is off: a hash costs a full GC and about 200 ms at 1.5k objects, scope risk 1).</summary>
    public int HashEvery { get; init; }

    /// <summary><c>--record FILE</c>: write the command log (joins, leaves, inputs, console commands with their apply ticks) for a replay.</summary>
    public string RecordPath { get; init; }

    /// <summary><c>--replay FILE</c>: run the commands of a recorded log fast-forward, with no network (scope 2j).</summary>
    public string ReplayPath { get; init; }

    /// <summary>Read console commands from standard input (a reader thread only enqueues lines, scope 2e).</summary>
    public bool Console { get; init; } = true;

    // ---- added by P3 ----

    /// <summary><c>--smoke K</c> (M0): K scripted smoke actors in the world (<see cref="SmokeLoad"/>), for loop tests of the empty world.</summary>
    public int SmokeActors { get; init; }

    // ---- M2 (contracts; P6 parses them: keys dev_commands and move_speed_scale, docs/ai/specs/apps.md section 1) ----

    /// <summary>
    /// M2, D73 and the lead's Q9: whether players may send dev commands (<c>ActionKind.DevCommand</c>: damage, heal, kill,
    /// knockdown, gas, analyze, speedscale, ...). Null (the default) means "on when <see cref="BindAddress"/> is a loopback
    /// address, off otherwise" (<see cref="DevCommandsEnabled"/>); the console's commands are always allowed.
    /// </summary>
    public bool? DevCommands { get; init; }

    /// <summary>Whether dev commands from players are accepted (<see cref="DevCommands"/>, or a loopback bind when it is null).</summary>
    public bool DevCommandsEnabled => DevCommands ?? (BindAddress == "127.0.0.1" || BindAddress == "::1" || BindAddress == "localhost");

    /// <summary>
    /// The owner's request (2026-10-10): whether players may also type the server console's own commands (spawn of any type,
    /// teleport, list, kick, status, hash, quit) in their dev console, as the console types them, with the reply sent back to
    /// them (docs/ai/specs/apps.md 1.6b). Off by default, whatever the bind: on a shared server every player becomes an admin.
    /// </summary>
    public bool ClientConsole { get; init; }

    /// <summary>
    /// M2, D106: the scale of every capsule speed (tg's tiles per second times this), a D74 row; the dev command
    /// <c>speedscale</c> changes it at run time (<c>LocomotionSeam.SetSpeedScale</c>). DEVIATION(DV70): default 0.5, running
    /// 6.7 m/s at 2 m tiles, walking 2.5 m/s (the owner, 2026-10-19: the player ran far too fast in 3D).
    /// </summary>
    public float MoveSpeedScale { get; init; } = DefaultMoveSpeedScale;

    /// <summary>DV70: the default <see cref="MoveSpeedScale"/>, half of tg's tiles per second.</summary>
    public const float DefaultMoveSpeedScale = 0.5f;

    /// <summary>
    /// The owner (2026-11-20, revising D119): the height of every level's walls and ceiling in metres, 2 to 4 (key <c>wall_height</c>); the world's
    /// <c>LevelHeight</c>, which the clients build their world with (protocol version 91), so the server knows where every ceiling is.
    /// </summary>
    public float WallHeight { get; init; } = Anomaly.Core.Tg.LevelHeight.DefaultMetres;

    /// <summary>
    /// DV96 (the owner, 2026-11-20): the share of tg's spread an aimed shot's cone keeps, 0 to 2 (key <c>spread_scale</c>; 1 is tg's); the world's
    /// <c>ShotSpread</c>.
    /// </summary>
    public float SpreadScale { get; init; } = DefaultSpreadScale;

    public const float DefaultSpreadScale = 0.5f;

    /// <summary>
    /// DEVIATION(DV263): the walking capsules' speed ramp and slower backward and sideways speeds (the owner, 2026-11-03); the dev command
    /// <c>movefeel</c> changes it at run time (<c>LocomotionSeam.SetFeel</c>). Default <see cref="Anomaly.Core.Motion.MoveFeel.Default"/>.
    /// </summary>
    public Anomaly.Core.Motion.MoveFeel MoveFeel { get; init; } = Anomaly.Core.Motion.MoveFeel.Default;

    /// <summary>
    /// M2 P6. The map the test room builds (<c>--room-layout</c>, <c>ROOM_LAYOUT</c>): <see cref="TestRoomLayout.Rooms"/> (the
    /// default: four rooms, doorways, two corridors, pillars, a dead end) or <see cref="TestRoomLayout.Classic"/> (M1's room).
    /// </summary>
    public Anomaly.Core.Tg.TestRoomLayout RoomLayout { get; init; } = Anomaly.Core.Tg.TestRoomLayout.Rooms;

    /// <summary>
    /// The cells of tg's space around the test room (<c>--room-space</c>, <c>ROOM_SPACE</c>; the owner, 2026-10-13: a deleted
    /// rim wall must open to space, as on a station): 0 to <see cref="Anomaly.Core.Tg.TestRoom.MaxSpace"/>, default
    /// <see cref="Anomaly.Core.Tg.TestRoom.ServerSpace"/>. The room's cells move in by as many (TestRoom's class comment).
    /// </summary>
    public int RoomSpace { get; init; } = Anomaly.Core.Tg.TestRoom.ServerSpace;

    /// <summary>
    /// D141: the half side, in tiles, of the square around each player whose air flow the server sends for the airflow
    /// streams (<c>--airflow-radius</c>, <c>AIRFLOW_RADIUS</c>), 0 (off: no Airflow messages) to
    /// <see cref="MaxAirflowRadius"/>, default <see cref="DefaultAirflowRadius"/>; levels within one above and below.
    /// </summary>
    public int AirflowRadius { get; init; } = DefaultAirflowRadius;

    /// <summary>The default <see cref="AirflowRadius"/> (a little over tg's 15 x 15 view, which a 3D view sees past).</summary>
    public const int DefaultAirflowRadius = 20;

    /// <summary>The largest <see cref="AirflowRadius"/>.</summary>
    public const int MaxAirflowRadius = 64;

    /// <summary>
    /// M2 integration: tg's <c>RUN_DELAY</c> (<c>config/game_options.txt</c>, the run speed's move delay in deciseconds; tg ships
    /// 1.5). The host hands it to tg's own configuration (the world's <c>config/config.txt</c>, which <c>world/New</c> loads),
    /// so the walk and run movespeed modifiers read it as in tg; a replay log records it.
    /// </summary>
    public float RunDelay { get; init; } = DefaultRunDelay;

    /// <summary>M2 integration: tg's <c>WALK_DELAY</c> (<c>config/game_options.txt</c>; tg ships 4), as <see cref="RunDelay"/>.</summary>
    public float WalkDelay { get; init; } = DefaultWalkDelay;

    /// <summary>
    /// M3 (D108, D109; the contract of the M3 contracts step, M3 P2 builds the world from it): the converted map the server
    /// loads (<c>--map NAME</c>, <c>MAP</c>), the name of its map json without the extension (<c>metastation</c>), or null for
    /// the test room (M2's server). With a map, the room options are ignored and players join at tg's late-join landmarks
    /// (<c>MapSeam.LatejoinSpawns</c>). The name is a plain word (letters, digits, <c>_</c> and <c>-</c>), never a path.
    /// </summary>
    public string Map { get; init; }

    /// <summary>M3: the folder of converted maps (<c>--maps-folder DIR</c>, <c>MAPS_FOLDER</c>; default <see cref="DefaultMapsFolder"/>, D109).</summary>
    public string MapsFolder { get; init; } = DefaultMapsFolder;

    /// <summary>
    /// The 3D model catalog (<c>--models-catalog FILE</c>, <c>MODELS_CATALOG</c>; default the repository's <c>godot/assets/models/models.json</c>, found
    /// upward): the server reads its <c>"dir"</c> rules (DV56, docs/ai/specs/models.md). None found: no rule.
    /// </summary>
    public string ModelsCatalog { get; init; }

    /// <summary>
    /// The world is a map editor's (docs/ai/specs/map-editor.md 3, D336; set by the editor, with <see cref="Lobby"/>): it stands in the lobby with the round's
    /// start held, so the map is shown as a round would start with it and nothing of it is played; a random spawner and a landmark stay where the map has
    /// them (DV504); the editor changes the map's tiles with <see cref="ServerHost.SetMapTiles"/>.
    /// </summary>
    public bool MapEditor { get; init; }

    /// <summary>
    /// The text of map files loaded in place of what the files hold, by a file's full path (<see cref="Anomaly.Core.Tg.MapSelection.Texts"/>); null: none. Set by
    /// the map editor's Play (docs/ai/specs/map-editor.md 5.7), which runs a map with the changes that are not saved.
    /// </summary>
    public IReadOnlyDictionary<string, string> MapTexts { get; init; }

    /// <summary>
    /// Without <see cref="Lobby"/>: the tile every join spawns at, in place of the map's late-join landmarks; null: the landmarks. Set by the map editor's
    /// Play (docs/ai/specs/map-editor.md 5.7): the player starts where the mapper looks, and a map with no landmark of its own (a new map) can be joined.
    /// </summary>
    public (int X, int Y, int Z)? JoinAt { get; init; }

    /// <summary>
    /// Without <see cref="Lobby"/>: a join is tg's own late join, with no spawn point of the host's: a chair of the arrivals shuttle, else a late-join
    /// landmark (tg's <c>AttemptLateSpawn</c> with no spawn override: the player is buckled and announced, and a shuttle that is away comes in).
    /// <see cref="JoinAt"/> is not used. Set by the map editor's Play when the mapper starts as a new arrival (docs/ai/specs/map-editor.md 5.7).
    /// </summary>
    public bool JoinAsArrival { get; init; }

    /// <summary>
    /// A client that connects from this machine holds tg's <c>!localhost!</c> admin rank, with every right (tg's <c>ENABLE_LOCALHOST_RANK</c>, config.txt:55), as
    /// it does with <see cref="Lobby"/>. Set by the map editor's Play (docs/ai/specs/map-editor.md 5.7), whose one player is the mapper.
    /// </summary>
    public bool LocalAdmin { get; init; }

    /// <summary>M10 slice C: no interest management (<c>--no-interest</c>, <c>NO_INTEREST</c>): every client receives every entity, as before M10.</summary>
    public bool NoInterest { get; init; }

    /// <summary>
    /// M10 slice F (D270): tg's lobby (<c>--lobby</c>, <c>LOBBY</c>; the shipped config/server.txt turns it on): a join is tg's new
    /// player in the lobby, the round starts after tg's countdown, and the station engineer is the only open job (the overflow; the
    /// host writes tg's legacy <c>jobs.txt</c> with the others at 0). Without it (the tests, the test room) tg's round starts at
    /// once (<c>start_immediately</c>), joins open when it is in progress, and every join late-joins as tg's overflow job, the
    /// assistant, at the host's next spawn point (tg's <c>AttemptLateSpawn</c>, the spawn point as the job's spawn override).
    /// </summary>
    public bool Lobby { get; init; }

    /// <summary>
    /// With <see cref="Lobby"/> (<c>--open-jobs</c>, <c>OPEN_JOBS</c>): tg's jobs open with tg's own slots, and the cyborg also
    /// to late joins (DV426). The round start gives the ready
    /// players jobs by their job preferences (the character setup's Occupations page), and tg's overflow role is the assistant (D278).
    /// </summary>
    public bool OpenJobs { get; init; }

    /// <summary>
    /// Gaps R2 (D327): tg's station traits (<c>--station-traits</c>, <c>STATION_TRAITS</c>; the shipped config/server.txt turns them
    /// on, as tgstation ships them on): SSstation rolls the round's positive, neutral and negative traits. Without it (the tests, the
    /// test room) the host writes tg's <c>FORBID_STATION_TRAITS</c> (game_options.txt:158), as tg's unit test build skips them.
    /// </summary>
    public bool StationTraits { get; init; }

    /// <summary>
    /// D349: every ported species can be picked in Character Setup (<c>--all-species-roundstart</c>, <c>ALL_SPECIES_ROUNDSTART</c>), for
    /// testing; without it the host writes tg's own <c>ROUNDSTART_RACES</c> (game_options.txt:365-390).
    /// </summary>
    public bool AllSpeciesRoundstart { get; init; }

    /// <summary>
    /// Gaps R6 (D332): the round's clock at the real date (<c>--real-date</c>, <c>REAL_DATE</c>; the shipped config/server.txt turns it
    /// on): the first world's <c>world.realtime</c> starts at the host's wall clock (UTC, DV3), each next round's where the last one ended,
    /// and the host writes tg's <c>ALLOW_HOLIDAYS</c> (config.txt:260, as tgstation ships it), so the holidays come by the calendar.
    /// Without it (the tests, the test room) the clock starts at 2000-01-01 and there are no holidays.
    /// </summary>
    public bool RealDate { get; init; }

    /// <summary>
    /// Gaps R6: the first world's <c>world.realtime</c> start in deciseconds since 2000-01-01 (<c>DmWorldState.RealtimeStart</c>): a
    /// replay's, from its log's <c>realtime</c> line; 0 for a run, whose host takes it from the wall clock with <see cref="RealDate"/>.
    /// </summary>
    public double RealtimeStart { get; init; }

    /// <summary>The default <see cref="MapsFolder"/>: the git-ignored <c>maps/</c> at the repository root (docs/ai/specs/map-format.md 2).</summary>
    public const string DefaultMapsFolder = "maps";

    /// <summary>
    /// Version 13: the sound tool's index (<c>--sound-index FILE</c>, <c>SOUND_INDEX</c>; default <see cref="DefaultSoundIndex"/>), whose
    /// lengths (deciseconds) the world reads as tg reads rust-g's <c>sound_len</c>. Missing: one line on the output, every length 0.
    /// </summary>
    public string SoundIndex { get; init; } = DefaultSoundIndex;

    /// <summary>The default <see cref="SoundIndex"/>: the git-ignored index tools/Anomaly.Tools.Sounds writes beside the client's copies.</summary>
    public const string DefaultSoundIndex = "godot/assets/tg/sound/index.json";

    /// <summary>
    /// M10 slice I (D76, D275): the SQLite file of the keys, bans, notes and round ids (<c>--database FILE</c>, <c>DATABASE</c>; the
    /// shipped config/server.txt names data/anomaly.sqlite); null: a database in memory, gone with the server (the tests).
    /// </summary>
    public string DatabasePath { get; init; }

    /// <summary>
    /// M10x slice G5: the folder the Esc menu's bug reports are saved in, <c>DATE/ROUND-N/</c> each (<c>--bug-reports DIR</c>,
    /// <c>BUG_REPORTS</c>; the shipped config/server.txt names data/bug_reports); null: not saved (the tests that do not set it).
    /// </summary>
    public string BugReportsFolder { get; init; }

    /// <summary>
    /// The folder of tg's character savefiles, the world's <c>data/player_saves/</c> (<c>--player-saves DIR</c>, <c>PLAYER_SAVES</c>;
    /// the shipped config/server.txt names data/player_saves); null: in memory, gone with the server (the tests).
    /// </summary>
    public string PlayerSavesFolder { get; init; }

    /// <summary>
    /// Gaps C2: the folder of tg's pets' memories, the world's <c>data/npc_saves/</c> (Ian's age, Poly's phrases, Runtime's kittens; <c>--npc-saves DIR</c>, <c>NPC_SAVES</c>;
    /// the shipped config/server.txt names data/npc_saves); null: in memory, gone with the server (the tests).
    /// </summary>
    public string NpcSavesFolder { get; init; }

    /// <summary>
    /// The folder of tg's round counters, the world's <c>data/persistence/</c> (the shifts since the last delamination and their record,
    /// DV220; <c>--persistence-saves DIR</c>, <c>PERSISTENCE_SAVES</c>; the shipped config/server.txt names data/persistence); null: in
    /// memory, every round starts at 0 (the tests).
    /// </summary>
    public string PersistenceSavesFolder { get; init; }

    /// <summary>M10x slice G5: a Discord webhook URL each bug report is posted to with its screenshot (<c>--bug-report-webhook URL</c>, <c>BUG_REPORT_WEBHOOK_URL</c>); null: none.</summary>
    public string BugReportWebhook { get; init; }

    /// <summary>
    /// M10x slice G6: tg's <c>REGULAR_ADMINHELP_WEBHOOK_URL</c>, the Discord webhook an admin help is posted to when no admin is on
    /// (<c>--adminhelp-webhook URL</c>, <c>ADMINHELP_WEBHOOK_URL</c>); null: none.
    /// </summary>
    public string AdminhelpWebhook { get; init; }

    /// <summary>D335: the name the launcher's server list shows (<c>--server-name NAME</c>, <c>SERVER_NAME</c>).</summary>
    public string ServerName { get; init; } = "Anomaly Station";

    /// <summary>D335: the text the launcher shows for the selected server (<c>--server-description TEXT</c>, <c>SERVER_DESCRIPTION</c>).</summary>
    public string ServerDescription { get; init; } = "";

    /// <summary>The mode the launcher shows for the selected server, free text (<c>--server-mode TEXT</c>, <c>SERVER_MODE</c>); empty: not shown.</summary>
    public string ServerMode { get; init; } = "";

    /// <summary>D335: answer the launcher's <c>GET /status</c> over TCP on the game's port number (<c>--status</c>, <c>STATUS</c>); off by default.</summary>
    public bool Status { get; init; }

    /// <summary>D335: the TCP port of the status answer; null (always, outside tests): the game's port number, where the launcher looks for it.</summary>
    public int? StatusPort { get; init; }

    /// <summary>D335: the JSON file that names the client build this server's players run (<c>--client-build FILE</c>, <c>CLIENT_BUILD</c>; <c>tools/make-client-manifest.sh</c> writes it); null: the status names none.</summary>
    public string ClientBuildFile { get; init; }

    /// <summary>D335: the hub's public key, a PEM file (<c>--hub-key FILE</c>, <c>HUB_KEY</c>): players join through the launcher's login only; null: a player is the name typed.</summary>
    public string HubKeyFile { get; init; }

    /// <summary>D335: the addresses players reach this server at, "host:port" in lower case (<c>--public-address A[,B]</c>, <c>PUBLIC_ADDRESS</c>): a login is for one address, and must be for one of these.</summary>
    public IReadOnlyList<string> PublicAddresses { get; init; } = Array.Empty<string>();

    /// <summary>The folder of tg's admin files (<c>admins.txt</c>, <c>admin_ranks.txt</c>): the config file's (D275).</summary>
    public string ConfigFolder => Path.GetDirectoryName(ConfigFile) is { Length: > 0 } folder ? folder : ".";

    /// <summary>tg's shipped <c>RUN_DELAY</c> (tgstation <c>config/game_options.txt</c>).</summary>
    public const float DefaultRunDelay = 1.5f;

    /// <summary>tg's shipped <c>WALK_DELAY</c> (tgstation <c>config/game_options.txt</c>).</summary>
    public const float DefaultWalkDelay = 4f;

    /// <summary>The largest <see cref="MoveSpeedScale"/> (the locomotion seam's: <c>LocomotionSeam.SetSpeedScale</c> takes a positive number up to 4).</summary>
    public const float MaxMoveSpeedScale = 4f;

    /// <summary>Unknown keys of the config file, skipped (tg logs them and carries on).</summary>
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

    /// <summary>The usage text.</summary>
    public static string Usage =>
        "Usage: Anomaly.Server [--ticks N] [--seed S] [--realtime] [options]\n"
        + "  --ticks N             run N ticks (20 ticks = 1 second of game time), then shut down cleanly; may be left out\n"
        + "                        with --realtime (run until Ctrl+C, SIGTERM or the console's quit) or --replay\n"
        + "  --seed S              world seed, an unsigned 64-bit integer (default: a random one, printed at start)\n"
        + "  --realtime            pace ticks at 20 per second of wall time (default: fast-forward)\n"
        + "  --port P              UDP port (default 7777; 0: an ephemeral port)\n"
        + "  --bind ADDRESS        address to bind (default 127.0.0.1; 0.0.0.0 for LAN play)\n"
        + "  --max-players N       sessions at once (default " + Limits.DefaultMaxPlayers.ToString(CultureInfo.InvariantCulture) + ")\n"
        + "  --room W[xH]          the test room's size, rim included (default 48; 3 to 64)\n"
        + "  --empty               run the bare kernel world: no tg boot, no room, no joins\n"
        + "  --boot                boot the translated tg world (the default; kept from M0)\n"
        + "  --smoke K             add K scripted smoke actors (M0 loop tests)\n"
        + "  --map NAME            M3: load the converted map NAME (a map json name, such as metastation) instead of the test room\n"
        + "  --no-interest         M10: send every entity to every client (no interest management)\n"
        + "  --lobby               M10: tg's lobby, its countdown and the station engineer's job (else: the round starts at once, joins are assistants)\n"
        + "  --no-lobby            no lobby whatever the config file says (the shipped config/server.txt turns it on)\n"
        + "  --open-jobs           M10, with --lobby: the late join menu opens tg's jobs but the heads of staff and the silicons\n"
        + "  --station-traits      tg's station traits: the round's positive, neutral and negative traits (the shipped config/server.txt turns them on)\n"
        + "  --no-station-traits   no station traits whatever the config file says\n"
        + "  --real-date           the round's clock at the real date, with tg's holidays (the shipped config/server.txt turns it on)\n"
        + "  --no-real-date        the fixed 2000-01-01 clock and no holidays whatever the config file says\n"
        + "  --maps-folder DIR     M3: the folder of converted maps (default maps; tools/Anomaly.Tools.Maps writes it)\n"
        + "  --sound-index FILE    the sound lengths (default godot/assets/tg/sound/index.json; tools/Anomaly.Tools.Sounds writes it)\n"
        + "  --database FILE       the SQLite file of the keys, bans, notes and round ids (default: in memory)\n"
        + "  --player-saves DIR    the folder of the players' characters and preferences (default: in memory)\n"
        + "  --npc-saves DIR       the folder of the pets' memories between rounds (default: in memory)\n"
        + "  --persistence-saves DIR  the folder of the round counters, the shifts since the last delamination (default: in memory)\n"
        + "  --bug-reports DIR     the folder the Esc menu's bug reports are saved in (default: not saved)\n"
        + "  --bug-report-webhook URL  a Discord webhook each bug report is posted to (default: none)\n"
        + "  --adminhelp-webhook URL   a Discord webhook an admin help goes to when no admin is on (default: none)\n"
        + "  --server-name NAME    the name the launcher's server list shows (default Anomaly Station)\n"
        + "  --server-description TEXT  the text the launcher shows for the selected server\n"
        + "  --server-mode TEXT    the mode the launcher shows for the selected server (default: none shown)\n"
        + "  --status              answer the launcher's GET /status over TCP on the game's port number\n"
        + "  --client-build FILE   the JSON file naming the client build this server's players run (tools/make-client-manifest.sh)\n"
        + "  --hub-key FILE        the hub's public key (PEM): players join through the launcher's login only\n"
        + "  --public-address A[,B]  the addresses players reach this server at (host:port): a login must be for one of them\n"
        + "  --room-layout L       the test room: rooms (default: four rooms, doorways, a 1-tile and a 2-tile corridor, a dead end, pillars) or classic (M1's)\n"
        + "  --room-space N        the cells of space around the test room (default 3; 0 to 16)\n"
        + "  --airflow-radius N    tiles around each player whose air flow is sent for the streams (default 20; 0 off, at most 64)\n"
        + "  --move-speed-scale F  D106: the scale of every capsule's speed, tg's tiles per second times F (default 1; 0 to 4, above 0)\n"
        + "  --wall-height M       the height of every level's walls and ceiling in metres, 2 to 4 (default 2); the clients build their world with it\n"
        + "  --spread-scale F      the share of tg's gun spread an aimed shot keeps, 0 to 2 (default 0.5; 1 is tg's)\n"
        + "  --run-delay D        tg's RUN_DELAY: the run speed's move delay in deciseconds (default 1.5, tg's game_options.txt)\n"
        + "  --walk-delay D        tg's WALK_DELAY: the walk speed's move delay in deciseconds (default 4)\n"
        + "  --dev-commands        let players send dev commands (damage, heal, kill, gas, ...); the default is on only for a loopback bind\n"
        + "  --no-dev-commands     refuse dev commands from players whatever the bind (the console's are always allowed)\n"
        + "  --client-console      let players also type the console's commands (spawn, teleport, list, kick, status, hash, quit); off by default\n"
        + "  --metrics FILE        write per-tick metrics as CSV\n"
        + "  --status-every N      ticks between status lines (default 200 = 10 s)\n"
        + "  --hash-every N        print the state hash every N ticks (default 0 = off; a hash costs a full GC)\n"
        + "  --record FILE         write the applied command stream (with the seed and config) for a replay\n"
        + "  --replay FILE         re-run a recorded command stream fast-forward, without network, and print the final hash\n"
        + "  --config FILE         the config file (default config/server.txt; tg's KEY value style)\n"
        + "  --no-console          do not read console commands from standard input\n"
        + "Ctrl+C or SIGTERM shuts down cleanly after the current tick; a second Ctrl+C kills the process.";

    // The keys and how each is read. A flag is a bool; the others take a value.
    private enum Kind { Flag, Int, Long, ULong, Text, Room }

    private static readonly Dictionary<string, (string Key, Kind Kind)> Keys = new(StringComparer.Ordinal)
    {
        ["port"] = ("port", Kind.Int),
        ["bind_address"] = ("bind_address", Kind.Text),
        ["bind"] = ("bind_address", Kind.Text),
        ["max_players"] = ("max_players", Kind.Int),
        ["seed"] = ("seed", Kind.ULong),
        ["room_width"] = ("room_width", Kind.Int),
        ["room_height"] = ("room_height", Kind.Int),
        ["room"] = ("room", Kind.Room),
        ["empty"] = ("empty", Kind.Flag),
        ["boot"] = ("boot", Kind.Flag),
        ["ticks"] = ("ticks", Kind.Long),
        ["realtime"] = ("realtime", Kind.Flag),
        ["metrics_file"] = ("metrics_file", Kind.Text),
        ["metrics"] = ("metrics_file", Kind.Text),
        ["status_every_ticks"] = ("status_every_ticks", Kind.Int),
        ["status_every"] = ("status_every_ticks", Kind.Int),
        ["hash_every"] = ("hash_every", Kind.Int),
        ["record_path"] = ("record_path", Kind.Text),
        ["record"] = ("record_path", Kind.Text),
        ["replay_path"] = ("replay_path", Kind.Text),
        ["replay"] = ("replay_path", Kind.Text),
        ["console"] = ("console", Kind.Flag),
        ["no_console"] = ("no_console", Kind.Flag),
        ["smoke_actors"] = ("smoke_actors", Kind.Int),
        ["smoke"] = ("smoke_actors", Kind.Int),
        ["room_layout"] = ("room_layout", Kind.Text),
        ["room_space"] = ("room_space", Kind.Int),
        ["airflow_radius"] = ("airflow_radius", Kind.Int),
        ["move_speed_scale"] = ("move_speed_scale", Kind.Text),
        ["wall_height"] = ("wall_height", Kind.Text),
        ["spread_scale"] = ("spread_scale", Kind.Text),
        ["dev_commands"] = ("dev_commands", Kind.Flag),
        ["run_delay"] = ("run_delay", Kind.Text),
        ["walk_delay"] = ("walk_delay", Kind.Text),
        ["no_dev_commands"] = ("no_dev_commands", Kind.Flag),
        ["client_console"] = ("client_console", Kind.Flag),
        ["map"] = ("map", Kind.Text),
        ["maps_folder"] = ("maps_folder", Kind.Text),
        ["no_interest"] = ("no_interest", Kind.Flag),
        ["lobby"] = ("lobby", Kind.Flag),
        ["no_lobby"] = ("no_lobby", Kind.Flag),
        ["open_jobs"] = ("open_jobs", Kind.Flag),
        ["station_traits"] = ("station_traits", Kind.Flag),
        ["no_station_traits"] = ("no_station_traits", Kind.Flag),
        ["all_species_roundstart"] = ("all_species_roundstart", Kind.Flag),
        ["real_date"] = ("real_date", Kind.Flag),
        ["no_real_date"] = ("no_real_date", Kind.Flag),
        ["models_catalog"] = ("models_catalog", Kind.Text),
        ["sound_index"] = ("sound_index", Kind.Text),
        ["database"] = ("database", Kind.Text),
        ["bug_reports"] = ("bug_reports", Kind.Text),
        ["player_saves"] = ("player_saves", Kind.Text),
        ["npc_saves"] = ("npc_saves", Kind.Text),
        ["persistence_saves"] = ("persistence_saves", Kind.Text),
        ["bug_report_webhook_url"] = ("bug_report_webhook_url", Kind.Text),
        ["bug_report_webhook"] = ("bug_report_webhook_url", Kind.Text),
        ["adminhelp_webhook_url"] = ("adminhelp_webhook_url", Kind.Text),
        ["adminhelp_webhook"] = ("adminhelp_webhook_url", Kind.Text),
        ["server_name"] = ("server_name", Kind.Text),
        ["server_description"] = ("server_description", Kind.Text),
        ["server_mode"] = ("server_mode", Kind.Text),
        ["status"] = ("status", Kind.Flag),
        ["client_build"] = ("client_build", Kind.Text),
        ["hub_key"] = ("hub_key", Kind.Text),
        ["public_address"] = ("public_address", Kind.Text),
    };

    /// <summary>
    /// Reads the config file through <paramref name="readFile"/> (the file's text, or null when it does not exist; so tests
    /// need no disk) and then <paramref name="args"/>. Returns null and sets <paramref name="error"/> (null for
    /// <c>--help</c>) when the server should not run.
    /// </summary>
    public static ServerConfig Parse(IReadOnlyList<string> args, Func<string, string> readFile, out string error, Func<ulong> randomSeed = null)
    {
        ArgumentNullException.ThrowIfNull(args);
        error = null;
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var warnings = new List<string>();

        // --config first: it names the file the rest of the command line overrides.
        string configFile = DefaultConfigFile;
        bool explicitConfig = false;
        for (int i = 0; i < args.Count; i++)
        {
            if (!SplitArg(args[i], out string name, out string inline) || name != "config") continue;
            if (inline == null)
            {
                if (i + 1 >= args.Count) { error = "--config needs a value"; return null; }
                inline = args[i + 1];
            }
            configFile = inline;
            explicitConfig = true;
        }

        string text = readFile?.Invoke(configFile);
        if (text == null && explicitConfig) { error = $"config file '{configFile}' not found"; return null; }
        if (text != null && !ReadFile(text, configFile, values, warnings, out error)) return null;

        for (int i = 0; i < args.Count; i++)
        {
            string arg = args[i];
            if (arg == "-h" || arg == "--help") return null;
            if (!SplitArg(arg, out string name, out string inline)) { error = $"unknown argument '{arg}'"; return null; }
            if (name == "config") { if (inline == null) i++; continue; }
            if (!Keys.TryGetValue(name, out var entry)) { error = $"unknown argument '{arg}'"; return null; }
            string shown = "--" + arg.Substring(2).Split('=')[0];
            if (entry.Kind == Kind.Flag)
            {
                if (inline != null) { error = $"{shown} takes no value"; return null; }
                values[entry.Key] = "1";
                continue;
            }
            if (inline == null)
            {
                if (i + 1 >= args.Count) { error = $"{shown} needs a value"; return null; }
                inline = args[++i];
            }
            values[entry.Key] = inline;
        }

        return Build(values, configFile, warnings, randomSeed, out error);
    }

    // "--name value" or "--name=value" (name normalized: lower case, '-' as '_').
    private static bool SplitArg(string arg, out string name, out string inline)
    {
        name = null;
        inline = null;
        if (arg == null || !arg.StartsWith("--", StringComparison.Ordinal) || arg.Length < 3) return false;
        string body = arg.Substring(2);
        int eq = body.IndexOf('=');
        if (eq == 0) return false;
        if (eq > 0)
        {
            inline = body.Substring(eq + 1);
            body = body.Substring(0, eq);
        }
        name = body.ToLowerInvariant().Replace('-', '_');
        return true;
    }

    // tg's LoadEntries: "#" comments, KEY value (the key case-insensitive, the value after the first space), a bare KEY a flag.
    private static bool ReadFile(string text, string file, Dictionary<string, string> values, List<string> warnings, out string error)
    {
        error = null;
        string[] lines = text.Split('\n');
        for (int n = 0; n < lines.Length; n++)
        {
            string line = lines[n].Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            int space = line.IndexOfAny(new[] { ' ', '\t' });
            string key = (space < 0 ? line : line.Substring(0, space)).ToLowerInvariant();
            string value = space < 0 ? null : line.Substring(space + 1).Trim();
            if (!Keys.TryGetValue(key, out var entry) || key == "no_console" || key == "no_dev_commands" || key == "no_lobby" || key == "no_station_traits" || key == "no_real_date" || key == "boot")
            {
                warnings.Add($"{file}:{n + 1}: unknown setting '{key}' (skipped)");
                continue;
            }
            if (entry.Kind == Kind.Flag)
            {
                if (value == null || value.Length == 0) value = "1";
                else if (!ParseBool(value, out bool on)) { error = $"{file}:{n + 1}: {key} expects 0 or 1, got '{value}'"; return false; }
                else value = on ? "1" : "0";
            }
            else if (value == null || value.Length == 0)
            {
                error = $"{file}:{n + 1}: {key} needs a value";
                return false;
            }
            values[entry.Key] = value;
        }
        return true;
    }

    private static bool ParseBool(string text, out bool value)
    {
        switch (text.ToLowerInvariant())
        {
            case "1": case "true": case "yes": case "on": value = true; return true;
            case "0": case "false": case "no": case "off": value = false; return true;
            default: value = false; return false;
        }
    }

    private static ServerConfig Build(Dictionary<string, string> values, string configFile, List<string> warnings, Func<ulong> randomSeed, out string error)
    {
        error = null;
        var d = new ServerConfig();
        int port = d.Port, maxPlayers = d.MaxPlayers, width = d.RoomWidth, height = d.RoomHeight, statusEvery = d.StatusEveryTicks;
        int hashEvery = d.HashEvery, smoke = d.SmokeActors, roomSpace = d.RoomSpace, airflowRadius = d.AirflowRadius;
        ulong seed = d.Seed;
        long? ticks = null;

        if (!Int(values, "port", 0, 65535, ref port, out error)) return null;
        if (!Int(values, "max_players", 1, 100000, ref maxPlayers, out error)) return null;
        if (values.TryGetValue("room", out string room))
        {
            string[] parts = room.ToLowerInvariant().Split('x');
            if (parts.Length > 2 || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out width)
                || !int.TryParse(parts[^1], NumberStyles.None, CultureInfo.InvariantCulture, out height))
            {
                error = $"--room expects N or WxH, got '{room}'";
                return null;
            }
        }
        if (!Int(values, "room_width", 0, int.MaxValue, ref width, out error)) return null;
        if (!Int(values, "room_space", 0, Anomaly.Core.Tg.TestRoom.MaxSpace, ref roomSpace, out error)) return null;
        if (!Int(values, "airflow_radius", 0, MaxAirflowRadius, ref airflowRadius, out error)) return null;
        if (!Int(values, "room_height", 0, int.MaxValue, ref height, out error)) return null;
        if (width < Anomaly.Core.Tg.TestRoom.MinSize || height < Anomaly.Core.Tg.TestRoom.MinSize
            || width > Anomaly.Core.Tg.TestRoom.MaxSize || height > Anomaly.Core.Tg.TestRoom.MaxSize)
        {
            error = $"the room is {Anomaly.Core.Tg.TestRoom.MinSize} to {Anomaly.Core.Tg.TestRoom.MaxSize} turfs on a side, got {width}x{height}";
            return null;
        }
        if (!Int(values, "status_every_ticks", 1, int.MaxValue, ref statusEvery, out error)) return null;
        if (!Int(values, "hash_every", 0, int.MaxValue, ref hashEvery, out error)) return null;
        if (!Int(values, "smoke_actors", 0, 1_000_000, ref smoke, out error)) return null;
        float scale = d.MoveSpeedScale;
        if (values.TryGetValue("move_speed_scale", out string scaleText)
            && (!float.TryParse(scaleText, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out scale)
                || !(scale > 0f) || scale > MaxMoveSpeedScale))
        {
            error = string.Create(CultureInfo.InvariantCulture, $"--move-speed-scale expects a number above 0 and up to {MaxMoveSpeedScale}, got '{scaleText}'");
            return null;
        }
        float wallHeight = d.WallHeight;
        if (values.TryGetValue("wall_height", out string wallText)
            && (!float.TryParse(wallText, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out wallHeight) || !Anomaly.Core.Tg.LevelHeight.IsValid(wallHeight)))
        {
            error = string.Create(CultureInfo.InvariantCulture, $"--wall-height expects metres from {Anomaly.Core.Tg.LevelHeight.MinMetres} to {Anomaly.Core.Tg.LevelHeight.MaxMetres}, got '{wallText}'");
            return null;
        }
        float spreadScale = d.SpreadScale;
        if (values.TryGetValue("spread_scale", out string spreadText)
            && (!float.TryParse(spreadText, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out spreadScale) || !Anomaly.Core.Tg.ShotSpread.IsValid(spreadScale)))
        {
            error = string.Create(CultureInfo.InvariantCulture, $"--spread-scale expects a number from {Anomaly.Core.Tg.ShotSpread.MinScale} to {Anomaly.Core.Tg.ShotSpread.MaxScale}, got '{spreadText}'");
            return null;
        }
        float runDelay = d.RunDelay, walkDelay = d.WalkDelay;
        if (!Delay(values, "run_delay", ref runDelay, out error)) return null;
        if (!Delay(values, "walk_delay", ref walkDelay, out error)) return null;
        Anomaly.Core.Tg.TestRoomLayout layout = d.RoomLayout;
        if (values.TryGetValue("room_layout", out string layoutText))
        {
            switch (layoutText.ToLowerInvariant())
            {
                case "rooms": layout = Anomaly.Core.Tg.TestRoomLayout.Rooms; break;
                case "classic": layout = Anomaly.Core.Tg.TestRoomLayout.Classic; break;
                default:
                    error = $"--room-layout expects rooms or classic, got '{layoutText}'";
                    return null;
            }
        }
        bool? devCommands = null;
        if (values.TryGetValue("dev_commands", out string devText)) devCommands = devText == "1";
        if (Flag(values, "no_dev_commands")) devCommands = false; // the command line's off wins over the file's on
        if (values.TryGetValue("seed", out string seedText)
            && !ulong.TryParse(seedText, NumberStyles.None, CultureInfo.InvariantCulture, out seed))
        {
            error = $"--seed expects an unsigned 64-bit integer, got '{seedText}'";
            return null;
        }
        else if (seedText == null && randomSeed != null)
            seed = randomSeed(); // a real round rolls differently each time; tests leave randomSeed out and keep the default
        if (values.TryGetValue("ticks", out string ticksText))
        {
            if (!long.TryParse(ticksText, NumberStyles.None, CultureInfo.InvariantCulture, out long t))
            {
                error = $"--ticks expects a non-negative integer, got '{ticksText}'";
                return null;
            }
            ticks = t;
        }

        bool realtime = Flag(values, "realtime");
        bool empty = Flag(values, "empty");
        if (empty && values.TryGetValue("boot", out string boot) && boot == "1")
        {
            error = "--empty and --boot exclude each other";
            return null;
        }
        string replay = Text(values, "replay_path");
        string record = Text(values, "record_path");
        if (replay != null && record != null) { error = "--replay and --record exclude each other"; return null; }
        if (replay != null && realtime) { error = "--replay runs fast-forward; it takes no --realtime"; return null; }
        if (ticks == null && !realtime && replay == null)
        {
            error = "--ticks N is required (it may be left out only with --realtime, to run until Ctrl+C, or with --replay)";
            return null;
        }
        string map = Text(values, "map");
        if (map != null && !IsMapName(map))
        {
            error = $"--map expects a map json name (letters, digits, '_' and '-'), got '{map}'";
            return null;
        }
        if (map != null && empty) { error = "--empty and --map exclude each other"; return null; }
        string mapsFolder = Text(values, "maps_folder") ?? d.MapsFolder;
        string bind = Text(values, "bind_address") ?? d.BindAddress;
        bool console = !Flag(values, "no_console") && (!values.TryGetValue("console", out string c) || c == "1");

        // D335: a login is for one address, so a server that takes logins has to know its own.
        var publicAddresses = new List<string>();
        foreach (string one in (Text(values, "public_address") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int colon = one.LastIndexOf(':');
            if (colon <= 0 || !int.TryParse(one.AsSpan(colon + 1), NumberStyles.None, CultureInfo.InvariantCulture, out int publicPort) || publicPort is < 1 or > 65535
                || one.IndexOfAny(new[] { ' ', '/', '@' }) >= 0)
            {
                error = $"--public-address expects host:port (several with commas), got '{one}'";
                return null;
            }
            publicAddresses.Add(one.ToLowerInvariant());
        }
        if (Text(values, "hub_key") != null && publicAddresses.Count == 0)
        {
            error = "--hub-key needs --public-address: the launcher's login is for one address, the one players reach this server at";
            return null;
        }

        return new ServerConfig
        {
            ConfigFile = configFile,
            Port = port,
            BindAddress = bind,
            MaxPlayers = maxPlayers,
            Seed = seed,
            RoomWidth = width,
            RoomHeight = height,
            Empty = empty,
            Ticks = ticks,
            Realtime = realtime,
            MetricsFile = Text(values, "metrics_file"),
            StatusEveryTicks = statusEvery,
            HashEvery = hashEvery,
            RecordPath = record,
            ReplayPath = replay,
            Console = console,
            SmokeActors = smoke,
            RoomLayout = layout,
            RoomSpace = roomSpace,
            AirflowRadius = airflowRadius,
            MoveSpeedScale = scale,
            WallHeight = wallHeight,
            SpreadScale = spreadScale,
            RunDelay = runDelay,
            WalkDelay = walkDelay,
            DevCommands = devCommands,
            ClientConsole = Flag(values, "client_console"),
            Map = map,
            MapsFolder = mapsFolder,
            NoInterest = Flag(values, "no_interest"),
            Lobby = Flag(values, "lobby") && !Flag(values, "no_lobby"), // the command line's off wins over the file's on
            OpenJobs = Flag(values, "open_jobs"),
            StationTraits = Flag(values, "station_traits") && !Flag(values, "no_station_traits"), // the command line's off wins over the file's on
            AllSpeciesRoundstart = Flag(values, "all_species_roundstart"),
            RealDate = Flag(values, "real_date") && !Flag(values, "no_real_date"), // the command line's off wins over the file's on
            ModelsCatalog = Text(values, "models_catalog"),
            SoundIndex = Text(values, "sound_index") ?? d.SoundIndex,
            DatabasePath = Text(values, "database"),
            BugReportsFolder = Text(values, "bug_reports"),
            PlayerSavesFolder = Text(values, "player_saves"),
            NpcSavesFolder = Text(values, "npc_saves"),
            PersistenceSavesFolder = Text(values, "persistence_saves"),
            BugReportWebhook = Text(values, "bug_report_webhook_url"),
            AdminhelpWebhook = Text(values, "adminhelp_webhook_url"),
            ServerName = Text(values, "server_name") ?? d.ServerName,
            ServerDescription = Text(values, "server_description") ?? d.ServerDescription,
            ServerMode = Text(values, "server_mode") ?? d.ServerMode,
            Status = Flag(values, "status"),
            ClientBuildFile = Text(values, "client_build"),
            HubKeyFile = Text(values, "hub_key"),
            PublicAddresses = publicAddresses,
            Warnings = warnings,
        };
    }

    /// <summary>Whether <paramref name="name"/> is a map json name: a plain word (letters, digits, <c>_</c>, <c>-</c>), never a path (the server opens <c>&lt;MapsFolder&gt;/map_jsons/&lt;name&gt;.json</c>).</summary>
    public static bool IsMapName(string name)
    {
        if (name.Length == 0 || name.Length > 64) return false;
        foreach (char c in name)
        {
            if (!(c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '_' or '-')) return false;
        }
        return true;
    }

    // A move delay in deciseconds: above 0 and at most 100 (tg's config number entries take any number; a delay of 0 or less
    // would make a capsule's speed infinite).
    private static bool Delay(Dictionary<string, string> values, string key, ref float value, out string error)
    {
        error = null;
        if (!values.TryGetValue(key, out string text)) return true;
        if (!float.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out float v) || !(v > 0f) || v > 100f)
        {
            error = $"--{key.Replace('_', '-')} expects a number of deciseconds above 0 and up to 100, got '{text}'";
            return false;
        }
        value = v;
        return true;
    }

    private static bool Flag(Dictionary<string, string> values, string key) => values.TryGetValue(key, out string v) && v == "1";

    private static string Text(Dictionary<string, string> values, string key)
        => values.TryGetValue(key, out string v) && v.Length > 0 ? v : null;

    private static bool Int(Dictionary<string, string> values, string key, int min, int max, ref int value, out string error)
    {
        error = null;
        if (!values.TryGetValue(key, out string text)) return true;
        string shown = "--" + key.Replace('_', '-');
        if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int v) || v < min || v > max)
        {
            error = max == int.MaxValue
                ? $"{shown} expects an integer of at least {min}, got '{text}'"
                : $"{shown} expects an integer from {min} to {max}, got '{text}'";
            return false;
        }
        value = v;
        return true;
    }
}
