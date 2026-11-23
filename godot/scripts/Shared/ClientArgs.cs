using System.Globalization;
using System.Linq;
using Anomaly.Protocol;
using Anomaly.Client.Shared.World;

namespace Anomaly.Client.Shared;

/// <summary>The camera modes (D101, D73): first person is the default; the others are for inspection and screenshots.</summary>
public enum CameraMode : byte
{
    FirstPerson,
    Free,
    Orbit,
}

/// <summary>A <c>--screenshot-after=S,PATH</c> request.</summary>
public readonly record struct ScreenshotRequest(double AfterSeconds, string Path);

/// <summary>A <c>--dev-after=S,TEXT</c> request: send a dev command S seconds after the game started.</summary>
public readonly record struct DevCommandRequest(double AfterSeconds, string Text);

/// <summary>
/// The automation arguments of the client (after <c>--</c> on the Godot command line, docs/ai/specs/apps.md 2):
/// <list type="table">
/// <item><term><c>--connect=host[:port]</c></term><description>connect at start, without the connect screen (port default 7777)</description></item>
/// <item><term><c>--name=X</c></term><description>the player name (default <c>Player</c>)</description></item>
/// <item><term><c>--token-store=PATH</c></term><description>the file of the kept reconnect tokens (default <c>user://reconnect-tokens.txt</c>; <c>--token-store=none</c> keeps none)</description></item>
/// <item><term><c>--autowalk</c></term><description>hold random directions, re-pick when blocked (the soak)</description></item>
/// <item><term><c>--quit-after=S</c></term><description>quit S seconds after the program started</description></item>
/// <item><term><c>--screenshot-after=S,PATH</c></term><description>save the frame S seconds after the game scene started, to the absolute PATH (repeatable); quit after the last one unless <c>--quit-after</c> is given</description></item>
/// <item><term><c>--screenshot=PATH</c></term><description>the M0 form: save the frame after 10 rendered frames and quit</description></item>
/// <item><term><c>--fake-server</c></term><description>play against the scripted world in the client (development; no server needed)</description></item>
/// <item><term><c>--camera=first|free|orbit</c>, <c>--yaw=DEG</c>, <c>--pitch=DEG</c></term><description>the starting camera mode and view (for screenshots)</description></item>
/// <item><term><c>--keys=SCRIPT</c></term><description>scripted key presses and view turns (see <see cref="InputScript"/>), for input tests without a keyboard</description></item>
/// <item><term><c>--names</c></term><description>start with the names overlay (F4) on</description></item>
/// <item><term><c>--fake-bots=N</c></term><description>the number of wandering bots of the fake world (default 8, at most 1000; the room grows with them)</description></item>
/// <item><term><c>--sim-latency=MS</c>, <c>--sim-loss=PERCENT</c></term><description>M2: a network that is not instant: every message is held back MS milliseconds in total (half each way, so the ping grows by MS), and PERCENT of the Input messages sent are lost (the reliable channels lose nothing); for feeling prediction on localhost</description></item>
/// <item><term><c>--bind=ACTION=KEY</c></term><description>M2: bind a key or mouse button (<c>--bind=rest=Z</c>; <c>KeyBindings.IdOf</c> names the actions, <c>Godot.Key</c> the keys), repeatable; on top of the options file</description></item>
/// <item><term><c>--option=NAME=VALUE</c></term><description>M2: set an option of the options file (<c>--option=head_bob=off</c>), repeatable</description></item>
/// <item><term><c>--dev-after=S,TEXT</c></term><description>M2: send the dev command TEXT S seconds after the game scene started (repeatable), for checks without a keyboard</description></item>
/// <item><term><c>--fake-map[=PATH]</c></term><description>M3: with <c>--fake-server</c>, serve a converted map (<c>maps/map_files/MetaStation/MetaStation.as3dmap</c> by default) instead of the generated room (FakeFeed version 3)</description></item>
/// <item><term><c>--eye-height=METRES</c></term><description>M3 R2 (D120, D157): the standing eye's height, 0.8 to 1.9 m (default 1.3, D157, <see cref="WorldScale.EyeHeight"/>; lying it is 0.4); the options file's <c>eye_height</c> otherwise</description></item>
/// <item><term><c>--item-scale=S</c></term><description>M3 R3 (D121): the scale items (<c>/obj/item</c>) are drawn at, 0.1 to 1 of the tile scale (default 0.5, <see cref="WorldScale.ItemScale"/>); the options file's <c>item_scale</c> otherwise</description></item>
/// <item><term><c>--view=X,Y,EYE,YAW,PITCH</c></term><description>M3: place the free camera at tile (X, Y) (BYOND coordinates, fractions allowed) EYE metres above the floor, looking YAW degrees (0 north, 90 west) and PITCH degrees (positive up); for screenshots</description></item>
/// <item><term><c>--ceiling</c></term><description>M3: keep the ceiling in the free and orbit cameras (it is hidden there by default so that the map can be seen from above)</description></item>
/// <item><term><c>--quality=low|medium|high</c></term><description>M3: the render preset (shadowed lights, shadow atlas, MSAA, render scale, view distance); the options file's <c>quality</c> otherwise</description></item>
/// <item><term><c>--bench[=SECONDS]</c>, <c>--bench-out=PATH</c>, <c>--bench-presets=low,medium,high,max</c>, <c>--bench-at=X,Y</c></term><description>M3: fly the fixed MetaStation path for SECONDS (default 60) per preset and write the FPS, draw calls, primitives and load time to PATH (docs/ai/specs/apps.md 2.10)</description></item>
/// <item><term><c>--occlusion=on|off</c></term><description>M3 I1c: Godot's occlusion culling (the merged wall boxes of <c>OccluderPlanner</c>) on (default) or off, to measure what it saves</description></item>
/// <item><term><c>--seed=N</c></term><description>the seed of <c>--autowalk</c> and of the fake world (default: from the name)</description></item>
/// </list>
/// A bad value is recorded in <see cref="Errors"/> (and the option ignored), not thrown: the client prints the errors and
/// still starts, or exits with status 2 when <see cref="Errors"/> is not empty and the run is automated.
/// </summary>
public sealed class ClientArgs
{
    /// <summary>The default server port (the server's <c>ServerConfig.DefaultPort</c>).</summary>
    public const int DefaultPort = 7777;

    /// <summary>The default name.</summary>
    public const string DefaultName = "Player";

    public string Host { get; private set; }

    public int Port { get; private set; } = DefaultPort;

    /// <summary>True when <c>--connect</c> was given.</summary>
    public bool HasConnect => Host != null;

    public string Name { get; private set; } = DefaultName;

    public bool AutoWalk { get; private set; }

    /// <summary>M3 R4: <c>--token-store=PATH</c>, the reconnect token file (null: the default; empty: none).</summary>
    public string TokenStore { get; private set; }

    /// <summary>Seconds after the program start; null: run until closed.</summary>
    public double? QuitAfter { get; private set; }

    public List<ScreenshotRequest> Screenshots { get; } = new();

    /// <summary>The M0 <c>--screenshot=PATH</c>; null when absent.</summary>
    public string LegacyScreenshot { get; private set; }

    public bool FakeServer { get; private set; }

    public CameraMode? Camera { get; private set; }


    /// <summary><c>--camera=freelook</c>: the third-person free look (F5): the mob walks and faces apart from the camera.</summary>
    public bool CameraFreeLook { get; private set; }

    /// <summary>The starting yaw in degrees (Godot's convention, 0 faces north, +90 west); null: the default.</summary>
    public double? YawDegrees { get; private set; }

    public double? PitchDegrees { get; private set; }

    public bool Names { get; private set; }

    /// <summary>The <c>--keys</c> script; empty when absent.</summary>
    public List<ScriptedInput> Keys { get; } = new();

    public int? Seed { get; private set; }

    /// <summary>M3: serve a converted map from the fake server (the path; empty: the default Meta path); null: the generated room.</summary>
    public string FakeMapPath { get; private set; }

#if ANOMALY_EDITOR
    /// <summary>
    /// The map editor (docs/ai/specs/map-editor.md): <c>--editor[=PATH]</c> starts it on the map of the file; a file that does not exist yet is a new map of
    /// <see cref="EditorSize"/>. Empty: <c>untitled.as3dmap</c> in the working folder. Null: the game.
    /// </summary>
    public string EditorMapPath { get; private set; }

    /// <summary><c>--editor-size=W,H,Z</c>: the size of the editor's new map (default 64,64,1).</summary>
    public (int Width, int Height, int Levels) EditorSize { get; private set; } = (64, 64, 1);

    /// <summary>
    /// <c>--editor-settings=PATH</c>: the file of the editor's own settings (the docks' layout, the interface's size, the favourites) in place of the
    /// user's own file; a scripted check names its own, so that it leaves the mapper's alone. Null: the user's.
    /// </summary>
    public string EditorSettingsPath { get; private set; }

    /// <summary>True when the program runs as the map editor.</summary>
    public bool Editor => EditorMapPath != null;
#endif

    /// <summary>M3 R2: <c>--eye-height</c>: the standing eye's height in metres (D120); null: the options file's, else <see cref="WorldScale.DefaultEyeHeight"/>.</summary>
    public float? EyeHeight { get; private set; }

    /// <summary>M3 R3: <c>--item-scale</c>: the scale items are drawn at (D121); null: the options file's, else <see cref="WorldScale.DefaultItemScale"/>.</summary>
    public float? ItemScale { get; private set; }

    /// <summary>M3: <c>--ceiling</c>: the ceiling stays in the free and orbit cameras.</summary>
    public bool ShowCeiling { get; private set; }

    /// <summary>M3: <c>--view</c>: the free camera's tile x, y, eye height in metres, yaw and pitch in degrees; null when absent.</summary>
    public (double X, double Y, double Eye, double Yaw, double Pitch)? View { get; private set; }

    /// <summary>M3: the render preset named on the command line (low, medium, high); null: the options file's.</summary>
    public string Quality { get; private set; }

    /// <summary>M3 I1c: <c>--occlusion=on|off</c>: Godot's occlusion culling by the merged wall boxes (on by default); off is for measuring what it saves. Null: on.</summary>
    public bool? Occlusion { get; private set; }

    /// <summary>M3: <c>--bench</c>: fly the bench path and write the numbers.</summary>
    public bool Bench { get; private set; }

    /// <summary>M3: seconds per preset of the bench flight (default 60).</summary>
    public double BenchSeconds { get; private set; } = 60;

    /// <summary>M3: where the bench writes its numbers; null: <c>user://bench.txt</c>.</summary>
    public string BenchOut { get; private set; }

    /// <summary>M3: the presets the bench flies, in order.</summary>
    public List<string> BenchPresets { get; } = new() { "low", "medium", "high", "max" };

    /// <summary>D168: <c>--bench-at=X,Y</c>: the bench flies a small square around this tile (BYOND coordinates) instead of the Meta path, to measure one spot; null: the path.</summary>
    public (int X, int Y)? BenchAt { get; private set; }

    /// <summary>How many bots the fake world has; null: its default.</summary>
    public int? FakeBots { get; private set; }

    /// <summary>The extra round trip of <c>--sim-latency</c>, milliseconds (0: none).</summary>
    public double SimLatencyMs { get; private set; }

    /// <summary>The share of lost Input messages of <c>--sim-loss</c>, 0 to 1.</summary>
    public double SimLoss { get; private set; }

    /// <summary>The <c>--bind</c> overrides, in order: an action id and a key name.</summary>
    public List<(string Action, string Key)> Binds { get; } = new();

    /// <summary>The <c>--option</c> overrides, in order.</summary>
    public List<(string Name, string Value)> Options { get; } = new();

    /// <summary>The <c>--dev-after</c> commands.</summary>
    public List<DevCommandRequest> DevCommands { get; } = new();

    /// <summary>What was wrong with the arguments, in text.</summary>
    public List<string> Errors { get; } = new();

    /// <summary>True when something is scripted: no human is expected to use the mouse (the mouse is then not captured).</summary>
    public bool Automated => Bench || AutoWalk || QuitAfter.HasValue || Screenshots.Count > 0 || LegacyScreenshot != null || Keys.Count > 0 || DevCommands.Count > 0;

    /// <summary>True when the network is simulated (the input buffer then aims for two frames, scope 2d).</summary>
    public bool SimulatesNetwork => SimLatencyMs > 0 || SimLoss > 0;

    /// <summary>True when the game scene should start without the connect screen.</summary>
#if ANOMALY_EDITOR
    public bool SkipConnectScreen => Editor || FakeServer || HasConnect && (!ServerFromFile || NameGiven);
#else
    public bool SkipConnectScreen => FakeServer || HasConnect && (!ServerFromFile || NameGiven);
#endif

    /// <summary>True when the server came from server.txt beside the exe (<c>--server-file</c>, set by the app): the player still types a name.</summary>
    public bool ServerFromFile { get; private set; }

    /// <summary>True when <c>--name</c> was given.</summary>
    public bool NameGiven { get; private set; }

    /// <summary>The map editor's name, as its window, its loading screen and the game's hints write it (docs/ai/specs/map-editor.md 5.13).</summary>
    public const string EditorName = "Anomaly Editor";

#if ANOMALY_EDITOR
    /// <summary>
    /// True when the program's own file says it is the map editor (docs/ai/specs/map-editor.md 5.13): its name holds "AnomalyEditor" or "MapEditor",
    /// with or without a hyphen, an underscore or a space between the words, whatever the case (a copy or a link of the game's executable under such a
    /// name). It then starts as <c>--editor</c> does.
    /// </summary>
    public static bool EditorByName(string executablePath)
    {
        string name = System.IO.Path.GetFileNameWithoutExtension(executablePath ?? string.Empty).Replace("-", string.Empty).Replace("_", string.Empty).Replace(" ", string.Empty);
        return name.Contains("anomalyeditor", StringComparison.OrdinalIgnoreCase) || name.Contains("mapeditor", StringComparison.OrdinalIgnoreCase);
    }
#endif

    public static ClientArgs Parse(IEnumerable<string> args)
    {
        var result = new ClientArgs();
        string[] all = args.ToArray();
        for (int i = 0; i < all.Length; i++)
        {
            string arg = all[i];
            // "--sim-latency 100" as well as "--sim-latency=100" (Godot hands the user arguments over as separate words).
            if (arg is "--sim-latency" or "--sim-loss" or "--bind" or "--option" or "--dev-after" or "--eye-height" or "--item-scale" && i + 1 < all.Length && all[i + 1] != null && !all[i + 1].StartsWith("--", StringComparison.Ordinal))
                arg = arg + "=" + all[++i];
            if (arg == null || !arg.StartsWith("--", StringComparison.Ordinal))
            {
                result.Errors.Add($"unexpected argument '{arg}'");
                continue;
            }
            string key = arg;
            string value = null;
            int equals = arg.IndexOf('=');
            if (equals >= 0)
            {
                key = arg.Substring(0, equals);
                value = arg.Substring(equals + 1);
            }
            result.Apply(key, value, arg);
        }
        return result;
    }

    /// <summary>Splits <c>host</c>, <c>host:port</c>, <c>[v6]</c> and <c>[v6]:port</c>.</summary>
    public static bool TryParseEndPoint(string text, out string host, out int port)
    {
        host = null;
        port = DefaultPort;
        if (string.IsNullOrWhiteSpace(text))
            return false;
        text = text.Trim();
        string portText = null;
        if (text.StartsWith('['))
        {
            int close = text.IndexOf(']');
            if (close < 2)
                return false;
            host = text.Substring(1, close - 1);
            string rest = text.Substring(close + 1);
            if (rest.Length > 0)
            {
                if (rest[0] != ':')
                    return false;
                portText = rest.Substring(1);
            }
        }
        else
        {
            int colon = text.LastIndexOf(':');
            if (colon >= 0 && text.IndexOf(':') == colon)
            {
                host = text.Substring(0, colon);
                portText = text.Substring(colon + 1);
            }
            else
            {
                host = text; // a name, or a bare IPv6 address without a port
            }
        }
        if (host.Length == 0)
            return false;
        if (portText != null && !TryParsePort(portText, out port))
            return false;
        return true;
    }

    /// <summary>A port number, 1 to 65535.</summary>
    public static bool TryParsePort(string text, out int port)
    {
        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out port) && port >= 1 && port <= 65535;
    }

    /// <summary>A player name the server will accept: 1 to <see cref="Limits.MaxNameChars"/> characters, no control characters.</summary>
    public static bool IsValidName(string name, out string problem)
    {
        problem = null;
        if (string.IsNullOrWhiteSpace(name))
            problem = "the name is empty";
        else if (name.Length > Limits.MaxNameChars)
            problem = $"the name is longer than {Limits.MaxNameChars} characters";
        else
        {
            foreach (char c in name)
            {
                if (char.IsControl(c))
                {
                    problem = "the name has control characters";
                    break;
                }
            }
        }
        return problem == null;
    }

    private void Apply(string key, string value, string original)
    {
        switch (key)
        {
            case "--connect":
            case "--server-file":
                if (TryParseEndPoint(value, out string host, out int port))
                {
                    Host = host;
                    Port = port;
                    ServerFromFile = key == "--server-file";
                }
                else
                    Errors.Add($"{original}: expected host or host:port");
                break;
            case "--name":
                if (IsValidName(value, out string problem))
                {
                    Name = value;
                    NameGiven = true;
                }
                else
                    Errors.Add($"{original}: {problem}");
                break;
            case "--autowalk":
                AutoWalk = true;
                break;
            case "--token-store":
                if (string.IsNullOrWhiteSpace(value))
                    Errors.Add($"{original}: expected a file path or none");
                else
                    TokenStore = value == "none" ? string.Empty : value;
                break;
            case "--quit-after":
                if (TryParseSeconds(value, out double quit))
                    QuitAfter = quit;
                else
                    Errors.Add($"{original}: expected seconds");
                break;
            case "--screenshot-after":
                ParseScreenshotAfter(value, original);
                break;
            case "--screenshot":
                if (IsAcceptablePath(value))
                    LegacyScreenshot = value;
                else
                    Errors.Add($"{original}: the path must be absolute (or res:// or user://)");
                break;
            case "--fake-server":
                FakeServer = true;
                break;
            case "--camera":
                switch (value)
                {
                    case "first": Camera = CameraMode.FirstPerson; break;
                    case "free": Camera = CameraMode.Free; break;
                    case "orbit": Camera = CameraMode.Orbit; break;
                    case "freelook": Camera = CameraMode.Orbit; CameraFreeLook = true; break;
                    default: Errors.Add($"{original}: expected first, free, orbit or freelook"); break;
                }
                break;
            case "--yaw":
                if (TryParseDouble(value, out double yaw))
                    YawDegrees = yaw;
                else
                    Errors.Add($"{original}: expected degrees");
                break;
            case "--pitch":
                if (TryParseDouble(value, out double pitch))
                    PitchDegrees = pitch;
                else
                    Errors.Add($"{original}: expected degrees");
                break;
            case "--names":
                Names = true;
                break;
            case "--keys":
                if (InputScript.TryParse(value, out List<ScriptedInput> steps, out string scriptError))
                    Keys.AddRange(steps);
                else
                    Errors.Add($"{original}: {scriptError}");
                break;
            case "--fake-bots":
                if (int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int bots) && bots <= 1000)
                    FakeBots = bots;
                else
                    Errors.Add($"{original}: expected a number from 0 to 1000");
                break;
            case "--sim-latency":
                if (TryParseDouble(value, out double latency) && latency >= 0 && latency <= 2000)
                    SimLatencyMs = latency;
                else
                    Errors.Add($"{original}: expected milliseconds from 0 to 2000");
                break;
            case "--sim-loss":
                if (TryParseDouble(value, out double loss) && loss >= 0 && loss <= 50)
                    SimLoss = loss / 100.0;
                else
                    Errors.Add($"{original}: expected a percentage from 0 to 50");
                break;
            case "--bind":
                int bindEquals = value == null ? -1 : value.IndexOf('=');
                if (bindEquals > 0 && KeyBindings.ActionOfId(value.Substring(0, bindEquals)) != null && KeyBindings.IsValidKeyName(value.Substring(bindEquals + 1)))
                    Binds.Add((value.Substring(0, bindEquals), value.Substring(bindEquals + 1)));
                else
                    Errors.Add($"{original}: expected ACTION=KEY (an action id such as rest, a key name such as Z)");
                break;
            case "--option":
                int optionEquals = value == null ? -1 : value.IndexOf('=');
                string optionProblem = "expected NAME=VALUE";
                if (optionEquals > 0 && new PlayOptions().TrySet(value.Substring(0, optionEquals), value.Substring(optionEquals + 1), out optionProblem))
                    Options.Add((value.Substring(0, optionEquals), value.Substring(optionEquals + 1)));
                else
                    Errors.Add($"{original}: {optionProblem}");
                break;
            case "--dev-after":
                int comma = value == null ? -1 : value.IndexOf(',');
                if (comma > 0 && TryParseSeconds(value.Substring(0, comma), out double devAt) && value.Length > comma + 1)
                    DevCommands.Add(new DevCommandRequest(devAt, value.Substring(comma + 1)));
                else
                    Errors.Add($"{original}: expected S,COMMAND");
                break;
            case "--fake-map":
                FakeMapPath = value ?? string.Empty;
                break;
#if ANOMALY_EDITOR
            case "--editor":
                EditorMapPath = value ?? string.Empty;
                break;
            case "--editor-settings":
                if (string.IsNullOrWhiteSpace(value))
                    Errors.Add($"{original}: expected a file's path");
                else
                    EditorSettingsPath = value;
                break;
            case "--editor-size":
                string[] size = (value ?? string.Empty).Split(',');
                if (size.Length == 3 && int.TryParse(size[0], NumberStyles.None, CultureInfo.InvariantCulture, out int editorWidth)
                    && int.TryParse(size[1], NumberStyles.None, CultureInfo.InvariantCulture, out int editorHeight)
                    && int.TryParse(size[2], NumberStyles.None, CultureInfo.InvariantCulture, out int editorLevels)
                    && editorWidth is >= 1 and <= 255 && editorHeight is >= 1 and <= 255 && editorLevels is >= 1 and <= 64)
                    EditorSize = (editorWidth, editorHeight, editorLevels);
                else
                    Errors.Add($"{original}: expected W,H,Z (1 to 255, 1 to 255, 1 to 64)");
                break;
#endif
            case "--ceiling":
                ShowCeiling = true;
                break;
            case "--eye-height":
                if (WorldScale.TryParseEyeHeight(value, out float eyeHeight))
                    EyeHeight = eyeHeight;
                else
                    Errors.Add($"{original}: expected metres from {WorldScale.MinEyeHeight} to {WorldScale.MaxEyeHeight}");
                break;
            case "--item-scale":
                if (WorldScale.TryParseItemScale(value, out float itemScale))
                    ItemScale = itemScale;
                else
                    Errors.Add($"{original}: expected a scale from {WorldScale.MinItemScale} to {WorldScale.MaxItemScale}");
                break;
            case "--view":
                string[] parts = (value ?? string.Empty).Split(',');
                if (parts.Length == 5 && parts.All(part => TryParseDouble(part, out _)))
                {
                    double[] v = parts.Select(part => double.Parse(part, CultureInfo.InvariantCulture)).ToArray();
                    View = (v[0], v[1], v[2], v[3], v[4]);
                    Camera = CameraMode.Free;
                }
                else
                    Errors.Add($"{original}: expected X,Y,EYE,YAW,PITCH");
                break;
            case "--quality":
                if (RenderPreset.TryParse(value, out _))
                    Quality = value.ToLowerInvariant();
                else
                    Errors.Add($"{original}: expected low, medium, high or max");
                break;
            case "--occlusion":
                if (value is "on" or "off")
                    Occlusion = value == "on";
                else
                    Errors.Add($"{original}: expected on or off");
                break;
            case "--bench":
                Bench = true;
                if (value != null)
                {
                    if (TryParseSeconds(value, out double benchSeconds) && benchSeconds >= 1)
                        BenchSeconds = benchSeconds;
                    else
                        Errors.Add($"{original}: expected seconds (at least 1)");
                }
                break;
            case "--bench-out":
                if (IsAcceptablePath(value))
                    BenchOut = value;
                else
                    Errors.Add($"{original}: the path must be absolute (or res:// or user://)");
                break;
            case "--bench-presets":
                BenchPresets.Clear();
                foreach (string name in (value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (RenderPreset.TryParse(name, out _) || string.Equals(name, RenderPreset.CustomName, StringComparison.OrdinalIgnoreCase))
                        BenchPresets.Add(name.ToLowerInvariant());
                    else
                        Errors.Add($"{original}: '{name}' is not a preset (low, medium, high, max, custom)");
                }
                if (BenchPresets.Count == 0 && Errors.Count == 0)
                    Errors.Add($"{original}: expected a list of presets");
                break;
            case "--bench-at":
                string[] at = (value ?? string.Empty).Split(',');
                if (at.Length == 2 && int.TryParse(at[0], NumberStyles.None, CultureInfo.InvariantCulture, out int atX) && int.TryParse(at[1], NumberStyles.None, CultureInfo.InvariantCulture, out int atY) && atX > 0 && atY > 0)
                    BenchAt = (atX, atY);
                else
                    Errors.Add($"{original}: expected X,Y (a tile)");
                break;
            case "--seed":
                if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int seed))
                    Seed = seed;
                else
                    Errors.Add($"{original}: expected an integer");
                break;
            default:
                Errors.Add($"unknown option '{original}'");
                break;
        }
    }

    private void ParseScreenshotAfter(string value, string original)
    {
        int comma = value == null ? -1 : value.IndexOf(',');
        if (comma <= 0 || !TryParseSeconds(value.Substring(0, comma), out double after))
        {
            Errors.Add($"{original}: expected S,PATH");
            return;
        }
        string path = value.Substring(comma + 1);
        if (!IsAcceptablePath(path))
        {
            Errors.Add($"{original}: the path must be absolute (or res:// or user://)");
            return;
        }
        Screenshots.Add(new ScreenshotRequest(after, path));
    }

    private static bool IsAcceptablePath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;
        return path.StartsWith("res://", StringComparison.Ordinal)
            || path.StartsWith("user://", StringComparison.Ordinal)
            || System.IO.Path.IsPathRooted(path);
    }

    private static bool TryParseSeconds(string text, out double seconds)
    {
        return TryParseDouble(text, out seconds) && seconds >= 0;
    }

    private static bool TryParseDouble(string text, out double value)
    {
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            && !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
