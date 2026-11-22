using System.Linq;
using System.Text;
using Anomaly.Protocol;
using System.Globalization;

namespace Anomaly.Tools.Bots;

/// <summary>The names of the behaviours on the command line: <c>wander</c>, <c>rest</c>, <c>shove</c>, <c>mixed</c>, <c>idle</c>, <c>churn</c>, <c>tour</c>, <c>fiddle</c> and <c>abuse-&lt;mode&gt;</c>.</summary>
public static class BotBehaviourNames
{
    private static readonly (string Name, BotBehaviour Behaviour)[] Names =
    {
        ("wander", BotBehaviour.Wander),
        ("rest", BotBehaviour.Rest),
        ("shove", BotBehaviour.Shove),
        ("mixed", BotBehaviour.Mixed),
        ("idle", BotBehaviour.Idle),
        ("churn", BotBehaviour.Churn),
        ("tour", BotBehaviour.Tour),
        ("abuse-flood", BotBehaviour.Flood),
        ("abuse-malformed", BotBehaviour.Malformed),
        ("abuse-oversized", BotBehaviour.Oversized),
        ("abuse-early-command", BotBehaviour.PreWelcomeCommand),
        ("abuse-wrong-version", BotBehaviour.WrongVersion),
        ("abuse-action-flood", BotBehaviour.ActionFlood),
        ("abuse-forged-seq", BotBehaviour.ForgedSeq),
        ("abuse-bad-action", BotBehaviour.BadAction),
        ("abuse-dev", BotBehaviour.DevAbuse),
        ("fiddle", BotBehaviour.Fiddle),
        ("abuse-forged-target", BotBehaviour.ForgedTarget),
        ("abuse-hover-flood", BotBehaviour.HoverFlood),
        ("abuse-bad-v4", BotBehaviour.BadV4),
    };

    /// <summary>
    /// The behaviour a bot actually runs: <see cref="BotBehaviour.Mixed"/> gives bot <paramref name="index"/> wander, rest,
    /// shove or wander for <c>index % 4</c> = 0, 1, 2, 3; every other behaviour is itself.
    /// </summary>
    public static BotBehaviour Resolve(BotBehaviour behaviour, int index)
    {
        if (behaviour != BotBehaviour.Mixed) return behaviour;
        switch (((index % 4) + 4) % 4)
        {
            case 1:
                return BotBehaviour.Rest;
            case 2:
                return BotBehaviour.Shove;
            default:
                return BotBehaviour.Wander;
        }
    }

    /// <summary>All names, in documentation order.</summary>
    public static IEnumerable<string> All
    {
        get
        {
            foreach ((string name, _) in Names) yield return name;
        }
    }

    public static bool TryParse(string text, out BotBehaviour behaviour)
    {
        behaviour = BotBehaviour.Wander;
        if (text == null) return false;
        string key = text.Trim().ToLowerInvariant();
        if (key == "abuse-garbage") key = "abuse-malformed"; // an alias: the scope says "garbage bytes"
        foreach ((string name, BotBehaviour value) in Names)
        {
            if (name == key)
            {
                behaviour = value;
                return true;
            }
        }
        return false;
    }

    public static string ToName(BotBehaviour behaviour)
    {
        foreach ((string name, BotBehaviour value) in Names)
        {
            if (value == behaviour) return name;
        }
        return behaviour.ToString().ToLowerInvariant();
    }

    public static bool IsAbuse(BotBehaviour behaviour) => ToName(behaviour).StartsWith("abuse-", StringComparison.Ordinal);
}

/// <summary>The command line of the bot runner (<c>--key value</c> or <c>--key=value</c>).</summary>
public static class BotArgs
{
    public static readonly string Usage =
        "Usage: Anomaly.Tools.Bots [--host H] [--port P] [--bots N] [--minutes M] [--behaviour B] [--predict] [--seed S]\n"
        + "                       [--name-prefix X] [--report FILE] [--reconnect] [--join-timeout S] [--ramp N] [--status-every S]\n"
        + "  --host H           server address (default 127.0.0.1)\n"
        + "  --port P           server UDP port (default 7777)\n"
        + "  --bots N           number of bots, 1 to 2000 (default 10)\n"
        + "  --minutes M        run length in minutes, a decimal is fine; 0 runs until Ctrl+C (default 1)\n"
        + "  --behaviour B      " + string.Join(" | ", BotBehaviourNames.All) + " (default wander)\n"
        + "  --predict          every bot predicts its own capsule with the motion core (the headless prediction client)\n"
        + "  --seed S           seed of the bots' random choices (default 1)\n"
        + "  --name-prefix X    the bots are named X001, X002, ... (default Bot; at most " + BotRunnerOptions.MaxNamePrefixChars + " characters)\n"
        + "  --report FILE      write the per-bot and aggregate report there as JSON\n"
        + "  --no-per-bot       leave the per-bot rows out of the report\n"
        + "  --reconnect        connect again (with the token) after an unexpected disconnect\n"
        + "  --join-timeout S   seconds a bot keeps trying while the server is full or booting (default 60)\n"
        + "  --ramp N           bots started per second, 0 for all at once (default 25; the server queues at most 64 requests)\n"
        + "  --status-every S   seconds between progress lines, 0 for none (default 10)\n"
        + "  --dev T:COMMAND    the first joined bot sends the dev command T seconds into the run (repeatable: startnow, shuttle call, reboot)\n"
        + "Ctrl+C or SIGTERM ends the run early; the bots leave and the report is still written.";

    /// <summary>Parses the command line. Returns null with <paramref name="error"/> set (null for <c>--help</c>) when nothing should run.</summary>
    public static BotRunnerOptions Parse(IReadOnlyList<string> args, out string error)
    {
        error = null;
        string host = "127.0.0.1";
        int port = 7777;
        int bots = 10;
        double minutes = 1;
        BotBehaviour behaviour = BotBehaviour.Wander;
        ulong seed = 1;
        string prefix = "Bot";
        string report = null;
        bool reconnect = false;
        bool noPerBot = false;
        bool predict = false;
        double joinTimeout = 60;
        int ramp = 25;
        int statusEvery = 10;
        var dev = new List<(double Seconds, string Command)>();

        for (int i = 0; i < args.Count; i++)
        {
            string arg = args[i];
            string name = arg;
            string inline = null;
            int eq = arg.IndexOf('=');
            if (arg.StartsWith("--", StringComparison.Ordinal) && eq > 2)
            {
                name = arg.Substring(0, eq);
                inline = arg.Substring(eq + 1);
            }

            string value;
            switch (name)
            {
                case "-h":
                case "--help":
                    return null;
                case "--reconnect":
                    if (inline != null) return Fail("--reconnect takes no value", out error);
                    reconnect = true;
                    break;
                case "--predict":
                    if (inline != null) return Fail("--predict takes no value", out error);
                    predict = true;
                    break;
                case "--no-per-bot":
                    if (inline != null) return Fail("--no-per-bot takes no value", out error);
                    noPerBot = true;
                    break;
                case "--host":
                    if (!Take(args, ref i, name, inline, out host, out error)) return null;
                    if (host.Length == 0) return Fail("--host is empty", out error);
                    break;
                case "--port":
                    if (!Take(args, ref i, name, inline, out value, out error)) return null;
                    if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out port) || port < 1 || port > 65535) return Fail("--port must be 1 to 65535, got '" + value + "'", out error);
                    break;
                case "--bots":
                    if (!Take(args, ref i, name, inline, out value, out error)) return null;
                    if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out bots) || bots < 1 || bots > 2000) return Fail("--bots must be 1 to 2000, got '" + value + "'", out error);
                    break;
                case "--minutes":
                    if (!Take(args, ref i, name, inline, out value, out error)) return null;
                    if (!double.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out minutes) || minutes < 0 || double.IsInfinity(minutes)) return Fail("--minutes must be 0 or more, got '" + value + "'", out error);
                    break;
                case "--behaviour":
                case "--behavior":
                    if (!Take(args, ref i, name, inline, out value, out error)) return null;
                    if (!BotBehaviourNames.TryParse(value, out behaviour)) return Fail("--behaviour must be one of " + string.Join(", ", BotBehaviourNames.All) + ", got '" + value + "'", out error);
                    break;
                case "--seed":
                    if (!Take(args, ref i, name, inline, out value, out error)) return null;
                    if (!ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out seed)) return Fail("--seed must be an unsigned 64-bit integer, got '" + value + "'", out error);
                    break;
                case "--name-prefix":
                    if (!Take(args, ref i, name, inline, out prefix, out error)) return null;
                    break;
                case "--report":
                    if (!Take(args, ref i, name, inline, out report, out error)) return null;
                    break;
                case "--join-timeout":
                    if (!Take(args, ref i, name, inline, out value, out error)) return null;
                    if (!double.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out joinTimeout) || joinTimeout < 0 || double.IsInfinity(joinTimeout)) return Fail("--join-timeout must be 0 or more seconds, got '" + value + "'", out error);
                    break;
                case "--ramp":
                    if (!Take(args, ref i, name, inline, out value, out error)) return null;
                    if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out ramp)) return Fail("--ramp must be 0 or more, got '" + value + "'", out error);
                    break;
                case "--status-every":
                    if (!Take(args, ref i, name, inline, out value, out error)) return null;
                    if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out statusEvery)) return Fail("--status-every must be 0 or more, got '" + value + "'", out error);
                    break;
                case "--dev":
                {
                    if (!Take(args, ref i, name, inline, out value, out error)) return null;
                    int colon = value.IndexOf(':');
                    if (colon < 1 || !double.TryParse(value.AsSpan(0, colon), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out double at)
                        || at < 0 || colon == value.Length - 1 || Encoding.UTF8.GetByteCount(value, colon + 1, value.Length - colon - 1) > Limits.MaxDevCommandBytes)
                        return Fail("--dev must be SECONDS:COMMAND, got '" + value + "'", out error);
                    dev.Add((at, value.Substring(colon + 1)));
                    break;
                }
                default:
                    return Fail("unknown argument '" + arg + "'", out error);
            }
        }

        if (prefix.Length == 0 || prefix.Length > BotRunnerOptions.MaxNamePrefixChars) return Fail("--name-prefix must be 1 to " + BotRunnerOptions.MaxNamePrefixChars + " characters", out error);
        foreach (char c in prefix)
        {
            if (char.IsControl(c)) return Fail("--name-prefix has a control character", out error);
        }
        if (report != null && report.Length == 0) return Fail("--report is empty", out error);

        return new BotRunnerOptions
        {
            Host = host,
            Port = port,
            Bots = bots,
            Minutes = minutes,
            Behaviour = behaviour,
            Seed = seed,
            NamePrefix = prefix,
            ReportPath = report,
            JoinTimeoutSeconds = joinTimeout,
            Reconnect = reconnect,
            RampPerSecond = ramp,
            StatusEverySeconds = statusEvery,
            NoPerBot = noPerBot,
            Predict = predict,
            DevSchedule = dev.OrderBy(d => d.Seconds).ToList(),
        };
    }

    private static BotRunnerOptions Fail(string message, out string error)
    {
        error = message;
        return null;
    }

    private static bool Take(IReadOnlyList<string> args, ref int i, string name, string inline, out string value, out string error)
    {
        error = null;
        if (inline != null)
        {
            value = inline;
            return true;
        }
        if (i + 1 >= args.Count)
        {
            value = null;
            error = name + " needs a value";
            return false;
        }
        value = args[++i];
        return true;
    }
}
