using System.Linq;
using System.Text;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared;

// Owner: M2 P8 (client play). Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).

/// <summary>
/// Who runs a console line (the owner's request, 2026-10-10: the console labels each typed line with it): the client itself
/// (never sent), the server as a dev command (apps.md 1.6a), or the server as one of the server console's commands, which a
/// player may type only when the server runs with <c>--client-console</c> (apps.md 1.6b). The routing is the server's; the
/// label is the client's reading of the verb.
/// </summary>
public enum ConsoleRoute : byte
{
    Client,
    Dev,
    Server,
}

/// <summary>Where a server text goes when the dev console is in use.</summary>
public enum TextDestination : byte
{
    /// <summary>The console's reply log.</summary>
    Console,

    /// <summary>The analyzer readout panel (the reply of an <c>analyze</c> command).</summary>
    Analyzer,
}

/// <summary>
/// The model of the dev console (D73, plan Q9): a text line that sends dev commands (<see cref="ActionKind.DevCommand"/>;
/// the server accepts them only when its <c>DEV_COMMANDS</c> is on) and shows the replies. The replies come back as
/// <see cref="ServerText"/>, to the requester only (D40); the reply of an <c>analyze</c> command (tg's <c>healthscan</c>,
/// the dev analyzer) goes to the analyzer readout panel instead, for <see cref="AnalyzerWindowSeconds"/> after the command.
/// History, line validation and the bounded logs are here; the widgets are the scene's.
/// </summary>
public sealed class DevConsoleModel
{
    /// <summary>The most lines the reply log keeps.</summary>
    public const int MaxLogLines = 200;

    /// <summary>The most commands the history keeps.</summary>
    public const int MaxHistory = 50;

    /// <summary>How long after an analyze command its replies count as the analyzer's.</summary>
    public const double AnalyzerWindowSeconds = 3.0;

    /// <summary>The client's own commands: run here, never sent to the server (<see cref="ConsoleCatalog"/>'s client ones).</summary>
    public static readonly string[] ClientVerbs = VerbsOf(ConsoleRoute.Client);

    /// <summary>The server's dev commands (DevCommandLine's verbs; a server test keeps the two lists equal).</summary>
    public static readonly string[] DevVerbs = VerbsOf(ConsoleRoute.Dev);

    /// <summary>The server console's own commands (ConsoleCommand's verbs other than the dev ones; a server test keeps them equal).</summary>
    public static readonly string[] ServerVerbs = VerbsOf(ConsoleRoute.Server);

    private static string[] VerbsOf(ConsoleRoute route)
        => ConsoleCatalog.Commands.Where(c => c.Route == route).SelectMany(c => c.Names).Distinct().ToArray();

    private readonly List<string> _history = new();
    private readonly List<string> _log = new();
    private readonly List<string> _markup = new();
    private int _historyIndex;
    private double _analyzerUntil = double.NegativeInfinity;

    /// <summary>The commands sent, oldest first.</summary>
    public IReadOnlyList<string> History => _history;

    /// <summary>The reply log, oldest first.</summary>
    public IReadOnlyList<string> Log => _log;

    /// <summary>The reply log as BBCode (the panel's RichTextLabel), line for line with <see cref="Log"/>: the help in colors, an
    /// echoed command's label in its route's color, a refusal in red; a server text as it is, escaped.</summary>
    public IReadOnlyList<string> LogMarkup => _markup;

    /// <summary>The log index of the last help's first line (the panel scrolls it to the top), or -1.</summary>
    public int HelpStart { get; private set; } = -1;

    /// <summary>Changes with every change of the log (the panel redraws only then).</summary>
    public int Version { get; private set; }

    /// <summary>The analyzer panel's text (the last reply of an analyze command).</summary>
    public string AnalyzerText { get; private set; } = string.Empty;

    /// <summary>The analyzer panel has had a reply.</summary>
    public bool HasAnalyzerText => AnalyzerText.Length > 0;

    /// <summary>
    /// Checks a command line and, when it can be sent, returns it trimmed: not empty, at most
    /// <see cref="Limits.MaxDevCommandBytes"/> UTF-8 bytes, no control characters (the server refuses all three).
    /// </summary>
    public static bool TryPrepare(string line, out string command, out string problem)
    {
        command = null;
        problem = null;
        string t = (line ?? string.Empty).Trim();
        if (t.Length == 0)
        {
            problem = "type a command";
            return false;
        }
        foreach (char c in t)
        {
            if (char.IsControl(c))
            {
                problem = "the command has control characters";
                return false;
            }
        }
        if (Encoding.UTF8.GetByteCount(t) > Limits.MaxDevCommandBytes)
        {
            problem = $"the command is longer than {Limits.MaxDevCommandBytes} bytes";
            return false;
        }
        command = t;
        return true;
    }

    /// <summary>
    /// Who runs <paramref name="command"/>, by its verb: a client verb, a dev verb, a server console verb. A player's
    /// <c>spawn</c> is the dev one (tg's spawn panel, any type; the owner's dev menu, 2026-10-13), the server console's own only
    /// while the server's dev commands are off. An unknown verb goes to the server as a dev command (which answers that it
    /// does not know it).
    /// </summary>
    public static ConsoleRoute Classify(string command)
    {
        string[] words = (command ?? string.Empty).Split((char[])null, 3, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
            return ConsoleRoute.Dev;
        string verb = words[0].ToLowerInvariant();
        if (Array.IndexOf(ClientVerbs, verb) >= 0)
            return ConsoleRoute.Client;
        if (verb == "spawn")
            return ConsoleRoute.Dev;
        if (Array.IndexOf(ServerVerbs, verb) >= 0)
            return ConsoleRoute.Server;
        return ConsoleRoute.Dev;
    }

    /// <summary>The label of a route in the log.</summary>
    public static string Label(ConsoleRoute route) => route switch
    {
        ConsoleRoute.Client => "[client]",
        ConsoleRoute.Server => "[server]",
        _ => "[dev]",
    };

    /// <summary>
    /// Runs a client command (<see cref="ConsoleRoute.Client"/>): records it like a sent one and answers in the log. False
    /// for any other line, which the caller sends to the server (<see cref="OnSent"/>).
    /// </summary>
    public bool TryRunLocal(string command)
    {
        if (Classify(command) != ConsoleRoute.Client)
            return false;
        Remember(command);
        string[] words = command.Split((char[])null, 3, StringSplitOptions.RemoveEmptyEntries);
        if (words[0].ToLowerInvariant() == "clear")
        {
            _log.Clear();
            _markup.Clear();
            HelpStart = -1;
            Version++;
            return true;
        }
        AddLog("> " + Label(ConsoleRoute.Client) + " " + command);
        if (words[0].ToLowerInvariant() == "ui")
        {
            // the owner (2026-11-11): the whole interface hidden or shown (the scene's switch)
            AddLog(Interface?.Invoke(words.Length > 1 ? words[1].ToLowerInvariant() : "toggle") ?? "ui: there is no interface here");
            return true;
        }
        HelpStart = _log.Count - 1;
        foreach ((string plain, string markup) in words.Length > 1 ? ConsoleCatalog.HelpFor(words[1]) : ConsoleCatalog.Help())
            Add(plain, markup);
        return true;
    }

    /// <summary>The scene's interface switch for <c>ui hide|show|toggle</c>: the mode in, the answer for the log out.</summary>
    public Func<string, string> Interface { get; set; }

    /// <summary>Records a command that was sent at local time <paramref name="nowSeconds"/> (history, the echo in the log, the analyzer window).</summary>
    public void OnSent(string command, double nowSeconds)
    {
        Remember(command);
        AddLog("> " + Label(Classify(command)) + " " + command);
        string verb = command.Split(' ', 2)[0];
        if (string.Equals(verb, "analyze", StringComparison.OrdinalIgnoreCase) || string.Equals(verb, "healthscan", StringComparison.OrdinalIgnoreCase))
            _analyzerUntil = nowSeconds + AnalyzerWindowSeconds;
    }

    private void Remember(string command)
    {
        if (_history.Count == 0 || _history[^1] != command)
            _history.Add(command);
        if (_history.Count > MaxHistory)
            _history.RemoveAt(0);
        _historyIndex = _history.Count;
    }

    /// <summary>A line to the log (the client's own notices: a refused command, a closed console).</summary>
    public void AddLog(string line)
    {
        foreach (string part in line.Split('\n'))
            Add(part, MarkupOf(part));
    }

    private void Add(string plain, string markup)
    {
        _log.Add(plain);
        _markup.Add(markup);
        if (_log.Count > MaxLogLines)
        {
            _log.RemoveAt(0);
            _markup.RemoveAt(0);
            HelpStart = Math.Max(-1, HelpStart - 1);
        }
        Version++;
    }

    /// <summary>The BBCode of a plain log line: an echo ("&gt; [dev] ...") with its label colored, a notice ("! ...") in red.</summary>
    private static string MarkupOf(string line)
    {
        if (line.StartsWith("! ", StringComparison.Ordinal))
            return "[color=#ff8080]" + ConsoleCatalog.Escape(line) + "[/color]";
        foreach (ConsoleRoute route in new[] { ConsoleRoute.Client, ConsoleRoute.Dev, ConsoleRoute.Server })
        {
            string head = "> " + Label(route) + " ";
            if (line.StartsWith(head, StringComparison.Ordinal))
                return $"[color={ConsoleCatalog.DescriptionColor}]> [/color][color={ConsoleCatalog.RouteColor(route)}]{ConsoleCatalog.Escape(Label(route))}[/color] "
                    + $"[color={ConsoleCatalog.CurrentColor}]{ConsoleCatalog.Escape(line.Substring(head.Length))}[/color]";
        }
        return ConsoleCatalog.Escape(line);
    }

    /// <summary>Routes a server text: into the analyzer panel when an analyze command is waiting for its reply, else into the log. Returns where it went (and the plain text).</summary>
    public TextDestination OnServerText(ServerText text, double nowSeconds, out string plain)
    {
        plain = HtmlText.ToPlain(text.Html);
        if (plain.Length == 0)
            return TextDestination.Console;
        if (nowSeconds <= _analyzerUntil)
        {
            AnalyzerText = plain;
            _analyzerUntil = double.NegativeInfinity; // one reply per command: the next text is the console's again
            return TextDestination.Analyzer;
        }
        AddLog(plain);
        return TextDestination.Console;
    }

    /// <summary>The previous command in the history (Up), or null at the start.</summary>
    public string HistoryPrevious()
    {
        if (_history.Count == 0)
            return null;
        _historyIndex = Math.Max(0, _historyIndex - 1);
        return _history[_historyIndex];
    }

    /// <summary>The next command in the history (Down); an empty text after the newest.</summary>
    public string HistoryNext()
    {
        if (_history.Count == 0)
            return null;
        _historyIndex = Math.Min(_history.Count, _historyIndex + 1);
        return _historyIndex >= _history.Count ? string.Empty : _history[_historyIndex];
    }
}
