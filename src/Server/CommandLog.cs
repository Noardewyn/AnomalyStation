using System.Globalization;
using System.IO;
using System.Text;
using Anomaly.Protocol;

namespace Anomaly.Server;

/// <summary>
/// The replay log (scope 2j): the commands the world applied, in the order it applied them, with what the world needs to
/// be built again (the seed, the room and its layout, the smoke load, the capsule speed scale). It is kept in memory for every run; <c>--record FILE</c>
/// also writes it, one command per line as it applies, and the final tick and state hash at shutdown; <c>--replay FILE</c>
/// reads it and re-runs the commands fast-forward without network, which gives the same final state hash (D31: everything
/// that enters the world is a command applied at a tick; the host's own randomness, the tokens, never does).
/// <para>
/// The text format (one item per line, fields separated by one space):
/// <code>
/// # comment
/// format 2
/// protocol 4
/// seed 1
/// empty 0
/// room 48 48
/// layout rooms
/// smoke 0
/// speedscale 1
/// wallheight 3 (when not 2)
/// spreadscale 0.5 (when not 1, tg's: a log without it was written before the setting)
/// c APPLY_TICK SESSION SEQ KIND EXTRA TEXT
/// end TICK
/// hash HEX
/// </code>
/// KIND is a <see cref="CommandKind"/> name; EXTRA is <c>-</c> for none, <c>d</c> and the <see cref="HeldDirs"/> bits (the
/// version 1 <c>Input</c>), <c>m</c> and <c>moveX,moveY,yaw,flags</c> (a <see cref="CommandKind.Motion"/> frame) or <c>a</c> and
/// <c>kind,target,button,modifiers,targetKind,x,y,z,iconX,iconY,keybinding,keyUp</c> (a <see cref="CommandKind.Action"/>; M3's lines have
/// the first four, which parse as before; then the aim zone, and version 15's radial menu and element, when set; version 17's drop
/// adds <c>aim,menu,element,window,params,overKind,overTarget,overX,overY,overZ,dragTime</c>), or <c>s</c> and <c>type,channel,visible</c>
/// (version 38: a <see cref="ActionKind.Say"/>, its type and channel as hex UTF-8, <c>-</c> for no channel; the entry is the TEXT); TEXT is <c>-</c> for none or <c>:</c> followed by
/// the text with <c>\</c>, newlines and tabs escaped (<c>\\</c>, <c>\n</c>, <c>\r</c>, <c>\t</c>).
/// </para>
/// </summary>
public sealed class CommandLog
{
    /// <summary>The format version this build writes and reads.</summary>
    public const int FormatVersion = 2;

    private readonly List<QueuedCommand> _commands = new();
    private TextWriter _writer;

    /// <summary>The world the commands were applied to.</summary>
    public ulong Seed { get; init; } = 1;

    /// <inheritdoc cref="ServerConfig.Empty"/>
    public bool Empty { get; init; }

    /// <inheritdoc cref="ServerConfig.RoomWidth"/>
    public int RoomWidth { get; init; } = Anomaly.Core.Tg.TestRoom.DefaultSize;

    /// <inheritdoc cref="ServerConfig.RoomHeight"/>
    public int RoomHeight { get; init; } = Anomaly.Core.Tg.TestRoom.DefaultSize;

    /// <inheritdoc cref="ServerConfig.SmokeActors"/>
    public int SmokeActors { get; init; }

    /// <inheritdoc cref="ServerConfig.RoomLayout"/>
    public Anomaly.Core.Tg.TestRoomLayout RoomLayout { get; init; } = Anomaly.Core.Tg.TestRoomLayout.Rooms;

    /// <summary>
    /// <see cref="ServerConfig.RoomSpace"/>. The header line <c>space N</c>; a log without it (written before the space ring,
    /// 2026-10-13) is a room with none, so it replays as it was recorded.
    /// </summary>
    public int RoomSpace { get; init; }

    /// <inheritdoc cref="ServerConfig.MoveSpeedScale"/>
    public float MoveSpeedScale { get; init; } = 1f;

    /// <inheritdoc cref="ServerConfig.WallHeight"/>
    public float WallHeight { get; init; } = Anomaly.Core.Tg.LevelHeight.DefaultMetres;

    /// <inheritdoc cref="ServerConfig.SpreadScale"/>
    public float SpreadScale { get; init; } = Anomaly.Core.Tg.ShotSpread.TgScale;

    /// <summary>
    /// <see cref="ServerConfig.MoveFeel"/> (DV263). The header line <c>movefeel ACCEL DECEL BACK STRAFE</c>; a log without it (written before
    /// the movement feel, 2026-11-03) had none, so it replays as it was recorded.
    /// </summary>
    public Anomaly.Core.Motion.MoveFeel MoveFeel { get; init; }

    /// <summary>
    /// M3 P2: the converted map of the run (<see cref="ServerConfig.Map"/>, the map json's name), or null for the test room. The
    /// header line <c>map NAME</c> is written only when set, so a log of a room run is unchanged; a replay loads the map of
    /// that name from its own maps folder (<see cref="ServerConfig.MapsFolder"/>).
    /// </summary>
    public string Map { get; init; }

    /// <summary>
    /// Gaps R6 (D332): the first world's <c>world.realtime</c> start (<see cref="ServerConfig.RealtimeStart"/>), world input: the holidays
    /// and the commendations read the date. The header line <c>realtime N</c> is written only when not 0, so a run on the fixed clock
    /// (and every log before it) is unchanged; a replay starts its world at the same date.
    /// </summary>
    public double RealtimeStart { get; set; }

    /// <inheritdoc cref="ServerConfig.RunDelay"/>
    public float RunDelay { get; init; } = ServerConfig.DefaultRunDelay;

    /// <inheritdoc cref="ServerConfig.WalkDelay"/>
    public float WalkDelay { get; init; } = ServerConfig.DefaultWalkDelay;

    /// <summary>The protocol version of the recording run.</summary>
    public int Protocol { get; init; } = ProtocolVersion.Current;

    /// <summary>
    /// M4b audio: the digest of the run's sound length table (<c>SoundLengths.Digest</c>; the trailer line <c>sounds X16</c>, before
    /// <c>end</c>; 0 and no line without a table). The lengths are world input, so a replay with another table would end with another
    /// hash: the replay host refuses to start instead.
    /// </summary>
    public ulong SoundsDigest { get; private set; }

    /// <summary>
    /// M10 slice I: the round id the run's database gave its first round (<c>GLOB.round_id</c>, world input: the round-end report and
    /// tg's colour hashes read it); the trailer line <c>round ID</c>, before <c>end</c>; a replay gives its world the same id.
    /// </summary>
    public string RoundId { get; set; }

    /// <summary>The applied commands, in application order.</summary>
    public IReadOnlyList<QueuedCommand> Commands => _commands;

    /// <summary>The last tick of the recorded run (null until it ended).</summary>
    public long? EndTick { get; private set; }

    /// <summary>The state hash at <see cref="EndTick"/> (null until the run ended).</summary>
    public ulong? FinalHash { get; private set; }

    /// <summary>The log of a run of <paramref name="config"/>.</summary>
    public static CommandLog For(ServerConfig config) => new()
    {
        Seed = config.Seed,
        Empty = config.Empty,
        RoomWidth = config.RoomWidth,
        RoomHeight = config.RoomHeight,
        SmokeActors = config.SmokeActors,
        RoomLayout = config.RoomLayout,
        RoomSpace = config.RoomSpace,
        MoveSpeedScale = config.MoveSpeedScale,
        WallHeight = config.WallHeight,
        SpreadScale = config.SpreadScale,
        MoveFeel = config.MoveFeel,
        RunDelay = config.RunDelay,
        WalkDelay = config.WalkDelay,
        Map = config.Map,
    };

    /// <summary>The config a replay of this log runs with (fast-forward, no network, no console).</summary>
    public ServerConfig ReplayConfig(ServerConfig basis = null) => new()
    {
        Seed = Seed,
        Empty = Empty,
        RoomWidth = RoomWidth,
        RoomHeight = RoomHeight,
        SmokeActors = SmokeActors,
        RoomLayout = RoomLayout,
        RoomSpace = RoomSpace,
        MoveSpeedScale = MoveSpeedScale,
        WallHeight = WallHeight,
        SpreadScale = SpreadScale,
        MoveFeel = MoveFeel,
        RunDelay = RunDelay,
        WalkDelay = WalkDelay,
        Map = Map,
        RealtimeStart = RealtimeStart,
        MapsFolder = basis?.MapsFolder ?? ServerConfig.DefaultMapsFolder,
        Ticks = EndTick,
        Console = false,
        StatusEveryTicks = basis?.StatusEveryTicks ?? 200,
        HashEvery = basis?.HashEvery ?? 0,
        MetricsFile = basis?.MetricsFile,
        ReplayPath = basis?.ReplayPath,
    };

    /// <summary>Starts writing to <paramref name="writer"/>: the header and every command added from now on (the log owns the writer).</summary>
    public void WriteTo(TextWriter writer)
    {
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _writer.NewLine = "\n"; // the same file on every OS (WriteLine would write "\r\n" on Windows)
        _writer.WriteLine("# Anomaly.Server command log: the commands the world applied (docs/ai/specs/apps.md section 1)");
        _writer.WriteLine(Inv($"format {FormatVersion}"));
        _writer.WriteLine(Inv($"protocol {Protocol}"));
        _writer.WriteLine(Inv($"seed {Seed}"));
        _writer.WriteLine(Inv($"empty {(Empty ? 1 : 0)}"));
        _writer.WriteLine(Inv($"room {RoomWidth} {RoomHeight}"));
        _writer.WriteLine(Inv($"layout {(RoomLayout == Anomaly.Core.Tg.TestRoomLayout.Classic ? "classic" : "rooms")}"));
        _writer.WriteLine(Inv($"space {RoomSpace}"));
        _writer.WriteLine(Inv($"smoke {SmokeActors}"));
        _writer.WriteLine(Inv($"speedscale {MoveSpeedScale:R}"));
        _writer.WriteLine(Inv($"movedelay {RunDelay:R} {WalkDelay:R}"));
        if (WallHeight != Anomaly.Core.Tg.LevelHeight.DefaultMetres)
            _writer.WriteLine(Inv($"wallheight {WallHeight:R}"));
        if (SpreadScale != Anomaly.Core.Tg.ShotSpread.TgScale)
            _writer.WriteLine(Inv($"spreadscale {SpreadScale:R}"));
        if (MoveFeel != default)
            _writer.WriteLine(Inv($"movefeel {MoveFeel.AccelTicks} {MoveFeel.DecelTicks} {MoveFeel.BackSlow:R} {MoveFeel.StrafeSlow:R}"));
        if (Map != null)
            _writer.WriteLine("map " + Map);
        if (RealtimeStart != 0d)
            _writer.WriteLine(Inv($"realtime {RealtimeStart:R}"));
        foreach (QueuedCommand c in _commands)
            _writer.WriteLine(Line(c));
    }

    /// <summary>Appends an applied command.</summary>
    public void Add(in QueuedCommand command)
    {
        _commands.Add(command);
        _writer?.WriteLine(Line(command));
    }

    /// <summary>Writes buffered lines out (once per status window).</summary>
    public void Flush() => _writer?.Flush();

    /// <summary>The run ended at <paramref name="tick"/> with <paramref name="hash"/>; the file, if any, is completed and closed.</summary>
    public void Finish(long tick, ulong hash, ulong soundsDigest = 0)
    {
        EndTick = tick;
        FinalHash = hash;
        SoundsDigest = soundsDigest;
        if (_writer == null) return;
        if (soundsDigest != 0)
            _writer.WriteLine("sounds " + soundsDigest.ToString("X16", CultureInfo.InvariantCulture));
        if (RoundId != null)
            _writer.WriteLine("round " + RoundId);
        _writer.WriteLine(Inv($"end {tick}"));
        _writer.WriteLine("hash " + hash.ToString("X16", CultureInfo.InvariantCulture));
        _writer.Dispose();
        _writer = null;
    }

    /// <summary>The whole log as text (what <see cref="WriteTo"/> and <see cref="Finish"/> write).</summary>
    public string ToText()
    {
        var sw = new StringWriter(CultureInfo.InvariantCulture);
        var copy = new CommandLog
        {
            Seed = Seed, Empty = Empty, RoomWidth = RoomWidth, RoomHeight = RoomHeight, SmokeActors = SmokeActors,
            RoomLayout = RoomLayout, RoomSpace = RoomSpace, MoveSpeedScale = MoveSpeedScale, WallHeight = WallHeight, SpreadScale = SpreadScale, MoveFeel = MoveFeel, RunDelay = RunDelay, WalkDelay = WalkDelay,
            Protocol = Protocol, Map = Map, RoundId = RoundId, RealtimeStart = RealtimeStart,
        };
        copy._commands.AddRange(_commands);
        copy.WriteTo(sw);
        if (EndTick.HasValue)
        {
            if (SoundsDigest != 0)
                sw.WriteLine("sounds " + SoundsDigest.ToString("X16", CultureInfo.InvariantCulture));
            if (RoundId != null)
                sw.WriteLine("round " + RoundId);
            sw.WriteLine(Inv($"end {EndTick.Value}"));
            sw.WriteLine("hash " + FinalHash.GetValueOrDefault().ToString("X16", CultureInfo.InvariantCulture));
        }
        return sw.ToString();
    }

    /// <summary>Reads a log; null with <paramref name="error"/> when the text is not one.</summary>
    public static CommandLog Parse(string text, out string error)
    {
        error = null;
        ulong seed = 1;
        bool empty = false;
        int width = Anomaly.Core.Tg.TestRoom.DefaultSize, height = Anomaly.Core.Tg.TestRoom.DefaultSize, smoke = 0, space = 0;
        int protocol = ProtocolVersion.Current, format = 0;
        var layout = Anomaly.Core.Tg.TestRoomLayout.Rooms;
        float speedScale = 1f, wallHeight = Anomaly.Core.Tg.LevelHeight.DefaultMetres, spreadScale = Anomaly.Core.Tg.ShotSpread.TgScale;
        Anomaly.Core.Motion.MoveFeel feel = default;
        string map = null;
        double realtime = 0d;
        float runDelay = ServerConfig.DefaultRunDelay, walkDelay = ServerConfig.DefaultWalkDelay;
        long? end = null;
        ulong? hash = null;
        ulong sounds = 0;
        string round = null;
        var commands = new List<QueuedCommand>();
        string[] lines = (text ?? "").Split('\n');
        for (int n = 0; n < lines.Length; n++)
        {
            string line = lines[n].TrimEnd('\r');
            if (line.Length == 0 || line[0] == '#') continue;
            string[] f = line.Split(' ', 7);
            string where = Inv($"line {n + 1}");
            try
            {
                switch (f[0])
                {
                    case "format": format = int.Parse(f[1], CultureInfo.InvariantCulture); break;
                    case "protocol": protocol = int.Parse(f[1], CultureInfo.InvariantCulture); break;
                    case "seed": seed = ulong.Parse(f[1], CultureInfo.InvariantCulture); break;
                    case "empty": empty = f[1] == "1"; break;
                    case "room": width = int.Parse(f[1], CultureInfo.InvariantCulture); height = int.Parse(f[2], CultureInfo.InvariantCulture); break;
                    case "space": space = int.Parse(f[1], CultureInfo.InvariantCulture); break;
                    case "smoke": smoke = int.Parse(f[1], CultureInfo.InvariantCulture); break;
                    case "layout":
                        layout = f[1] switch
                        {
                            "rooms" => Anomaly.Core.Tg.TestRoomLayout.Rooms,
                            "classic" => Anomaly.Core.Tg.TestRoomLayout.Classic,
                            _ => throw new FormatException("unknown room layout '" + f[1] + "'"),
                        };
                        break;
                    case "speedscale": speedScale = float.Parse(f[1], NumberStyles.Float, CultureInfo.InvariantCulture); break;
                    case "wallheight":
                        wallHeight = float.Parse(f[1], NumberStyles.Float, CultureInfo.InvariantCulture);
                        if (!Anomaly.Core.Tg.LevelHeight.IsValid(wallHeight)) throw new FormatException("a wall height outside 2 to 4 m");
                        break;
                    case "spreadscale":
                        spreadScale = float.Parse(f[1], NumberStyles.Float, CultureInfo.InvariantCulture);
                        if (!Anomaly.Core.Tg.ShotSpread.IsValid(spreadScale)) throw new FormatException("a spread scale outside 0 to 2");
                        break;
                    case "movefeel":
                        feel = new Anomaly.Core.Motion.MoveFeel(short.Parse(f[1], CultureInfo.InvariantCulture), short.Parse(f[2], CultureInfo.InvariantCulture),
                            float.Parse(f[3], NumberStyles.Float, CultureInfo.InvariantCulture), float.Parse(f[4], NumberStyles.Float, CultureInfo.InvariantCulture));
                        if (!feel.IsValid) throw new FormatException("a movement feel outside its ranges");
                        break;
                    case "movedelay":
                        runDelay = float.Parse(f[1], NumberStyles.Float, CultureInfo.InvariantCulture);
                        walkDelay = float.Parse(f[2], NumberStyles.Float, CultureInfo.InvariantCulture);
                        break;
                    case "map":
                        if (!ServerConfig.IsMapName(f[1])) throw new FormatException("not a map json name '" + f[1] + "'");
                        map = f[1];
                        break;
                    case "realtime": realtime = double.Parse(f[1], NumberStyles.Float, CultureInfo.InvariantCulture); break;
                    case "end": end = long.Parse(f[1], CultureInfo.InvariantCulture); break;
                    case "sounds": sounds = ulong.Parse(f[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture); break;
                    case "round": round = f[1]; break;
                    case "hash": hash = ulong.Parse(f[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture); break;
                    case "c":
                        if (f.Length != 7) { error = where + ": a command has 6 fields"; return null; }
                        if (!Enum.TryParse(f[4], out CommandKind kind) || !Enum.IsDefined(kind)) { error = where + $": unknown command kind '{f[4]}'"; return null; }
                        commands.Add(ParseCommand(f, kind));
                        break;
                    default:
                        error = where + $": unknown item '{f[0]}'";
                        return null;
                }
            }
            catch (Exception e) when (e is FormatException or OverflowException or IndexOutOfRangeException)
            {
                error = where + ": " + e.Message;
                return null;
            }
        }
        if (format != FormatVersion) { error = Inv($"not a command log of format {FormatVersion} (format {format})"); return null; }
        var log = new CommandLog
        {
            Seed = seed, Empty = empty, RoomWidth = width, RoomHeight = height, SmokeActors = smoke, RoomLayout = layout, RoomSpace = space,
            MoveSpeedScale = speedScale, WallHeight = wallHeight, SpreadScale = spreadScale, MoveFeel = feel, RunDelay = runDelay, WalkDelay = walkDelay, Protocol = protocol, EndTick = end, FinalHash = hash,
            Map = map, SoundsDigest = sounds, RoundId = round, RealtimeStart = realtime,
        };
        log._commands.AddRange(commands);
        return log;
    }

    private static string Line(in QueuedCommand c)
        => Inv($"c {c.ApplyTick} {c.Session} {c.Seq} {c.Kind} {Extra(c)} ") + EscapeText(c.Text);

    // The EXTRA field: what a command carries besides its text (the file comment).
    private static string Extra(in QueuedCommand c) => c.Kind switch
    {
        CommandKind.Motion => Inv($"m{c.Motion.MoveX},{c.Motion.MoveY},{c.Motion.Yaw},{(byte)c.Motion.Flags}"),
        CommandKind.Action when c.Action == ActionKind.Say => "s" + Hex(c.SayType) + "," + Hex(c.SayChannel) + "," + (c.SayVisible ? "1" : "0"),
        CommandKind.Action => Inv($"a{(byte)c.Action},{c.Target},{(byte)c.Button},{(byte)c.Modifiers},{(byte)c.TargetKind},{c.TurfX},{c.TurfY},{c.TurfZ},{c.IconX},{c.IconY},{(byte)c.Keybinding},{(c.KeyUp ? 1 : 0)}{(c.AimZone != AimZone.None || c.RadialMenu != 0 || c.UiWindow != 0 || IsDrop(c) || c.Aim.HasValue ? "," + (byte)c.AimZone : "")}{(c.RadialMenu != 0 || c.UiWindow != 0 || IsDrop(c) || c.Aim.HasValue ? "," + c.RadialMenu + "," + c.RadialElement : "")}{(c.UiWindow != 0 || IsDrop(c) || c.Aim.HasValue ? "," + c.UiWindow + "," + (c.UiParams == null ? "-" : Convert.ToHexString(Encoding.UTF8.GetBytes(c.UiParams))) : "")}{(IsDrop(c) || c.Aim.HasValue ? "," + (byte)c.OverKind + "," + c.OverTarget + "," + c.OverX + "," + c.OverY + "," + c.OverZ + "," + c.DragTime : "")}{(c.Aim is ShotAim aim ? "," + aim.East + "," + aim.North + "," + aim.Up + "," + aim.Yaw + "," + aim.Pitch + (aim.Seen != 0 ? "," + aim.Seen : "") : "")}"),
        _ => c.Dirs != HeldDirs.None ? Inv($"d{(byte)c.Dirs}") : "-",
    };

    private static string Hex(string word) => word == null ? "-" : Convert.ToHexString(Encoding.UTF8.GetBytes(word));

    private static string Unhex(string field) => field == "-" ? null : Encoding.UTF8.GetString(Convert.FromHexString(field));

    private static QueuedCommand ParseCommand(string[] f, CommandKind kind)
    {
        long tick = long.Parse(f[1], CultureInfo.InvariantCulture);
        int session = int.Parse(f[2], CultureInfo.InvariantCulture);
        uint seq = uint.Parse(f[3], CultureInfo.InvariantCulture);
        string text = UnescapeText(f[6]);
        string extra = f[5];
        if (extra == "-") return new QueuedCommand(tick, session, seq, kind, text);
        string[] v = extra.Substring(1).Split(',');
        switch (extra[0])
        {
            case 'd':
                return new QueuedCommand(tick, session, seq, kind, text, (HeldDirs)byte.Parse(v[0], CultureInfo.InvariantCulture));
            case 'm':
                var frame = new Anomaly.Core.Motion.MotionInput(seq, sbyte.Parse(v[0], CultureInfo.InvariantCulture), sbyte.Parse(v[1], CultureInfo.InvariantCulture),
                    ushort.Parse(v[2], CultureInfo.InvariantCulture), (Anomaly.Core.Motion.MotionInputFlags)byte.Parse(v[3], CultureInfo.InvariantCulture));
                return new QueuedCommand(tick, session, seq, kind, text, Motion: frame);
            case 's':
                return new QueuedCommand(tick, session, seq, kind, text, Action: ActionKind.Say, SayType: Unhex(v[0]), SayChannel: Unhex(v[1]), SayVisible: v[2] == "1");
            case 'a':
                return new QueuedCommand(tick, session, seq, kind, text, Action: (ActionKind)byte.Parse(v[0], CultureInfo.InvariantCulture),
                    Target: long.Parse(v[1], CultureInfo.InvariantCulture), Button: (ClickButton)byte.Parse(v[2], CultureInfo.InvariantCulture),
                    Modifiers: (ClickModifiers)byte.Parse(v[3], CultureInfo.InvariantCulture),
                    TargetKind: v.Length > 4 ? (TargetKind)byte.Parse(v[4], CultureInfo.InvariantCulture) : TargetKind.None,
                    TurfX: v.Length > 5 ? int.Parse(v[5], CultureInfo.InvariantCulture) : 0,
                    TurfY: v.Length > 6 ? int.Parse(v[6], CultureInfo.InvariantCulture) : 0,
                    TurfZ: v.Length > 7 ? int.Parse(v[7], CultureInfo.InvariantCulture) : 0,
                    IconX: v.Length > 8 ? byte.Parse(v[8], CultureInfo.InvariantCulture) : (byte)0,
                    IconY: v.Length > 9 ? byte.Parse(v[9], CultureInfo.InvariantCulture) : (byte)0,
                    Keybinding: v.Length > 10 ? (KeybindingCode)byte.Parse(v[10], CultureInfo.InvariantCulture) : KeybindingCode.None,
                    KeyUp: v.Length > 11 && v[11] == "1",
                    AimZone: v.Length > 12 ? (AimZone)byte.Parse(v[12], CultureInfo.InvariantCulture) : AimZone.None,
                    RadialMenu: v.Length > 14 ? long.Parse(v[13], CultureInfo.InvariantCulture) : 0,
                    RadialElement: v.Length > 14 ? int.Parse(v[14], CultureInfo.InvariantCulture) : 0,
                    UiWindow: v.Length > 16 ? int.Parse(v[15], CultureInfo.InvariantCulture) : 0,
                    UiParams: v.Length > 16 && v[16] != "-" ? Encoding.UTF8.GetString(Convert.FromHexString(v[16])) : null,
                    OverKind: v.Length > 22 ? (TargetKind)byte.Parse(v[17], CultureInfo.InvariantCulture) : TargetKind.None,
                    OverTarget: v.Length > 22 ? long.Parse(v[18], CultureInfo.InvariantCulture) : 0,
                    OverX: v.Length > 22 ? int.Parse(v[19], CultureInfo.InvariantCulture) : 0,
                    OverY: v.Length > 22 ? int.Parse(v[20], CultureInfo.InvariantCulture) : 0,
                    OverZ: v.Length > 22 ? int.Parse(v[21], CultureInfo.InvariantCulture) : 0,
                    DragTime: v.Length > 22 ? byte.Parse(v[22], CultureInfo.InvariantCulture) : (byte)0,
                    Aim: v.Length > 27 ? new ShotAim(int.Parse(v[23], CultureInfo.InvariantCulture), int.Parse(v[24], CultureInfo.InvariantCulture),
                        ushort.Parse(v[25], CultureInfo.InvariantCulture), ushort.Parse(v[26], CultureInfo.InvariantCulture), short.Parse(v[27], CultureInfo.InvariantCulture),
                        v.Length > 28 ? long.Parse(v[28], CultureInfo.InvariantCulture) : 0) : null);
            default:
                throw new FormatException("unknown command extra '" + extra + "'");
        }
    }

    private static string EscapeText(string text)
    {
        if (text == null) return "-";
        var sb = new StringBuilder(text.Length + 1).Append(':');
        foreach (char ch in text)
        {
            switch (ch)
            {
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default: sb.Append(ch); break;
            }
        }
        return sb.ToString();
    }

    private static string UnescapeText(string field)
    {
        if (field == "-") return null;
        if (field.Length == 0 || field[0] != ':') throw new FormatException("a text field is '-' or starts with ':'");
        var sb = new StringBuilder(field.Length);
        for (int i = 1; i < field.Length; i++)
        {
            char ch = field[i];
            if (ch != '\\') { sb.Append(ch); continue; }
            if (++i >= field.Length) throw new FormatException("a text ends inside an escape");
            sb.Append(field[i] switch
            {
                '\\' => '\\',
                'n' => '\n',
                'r' => '\r',
                't' => '\t',
                _ => throw new FormatException("unknown escape \\" + field[i]),
            });
        }
        return sb.ToString();
    }

    private static string Inv(FormattableString s) => s.ToString(CultureInfo.InvariantCulture);

    // Version 17: a drop's line carries the aim zone, the radial fields and version 16's window fields (zeros, '-') before its own six values.
    private static bool IsDrop(in QueuedCommand c) => c.Action == ActionKind.Drop;
}
