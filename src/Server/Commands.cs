using System.Globalization;
using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Server;

// Owner: M1 P3 (server host). Contract by the M1 contracts step (scope 2e "Where the network meets the tick", 2h, 2j); the
// bodies are P3's.

/// <summary>What a <see cref="QueuedCommand"/> does in the command phase.</summary>
public enum CommandKind : byte
{
    /// <summary>A session enters the world: key the avatar, then <c>ClientSeam.Connect</c> (<see cref="QueuedCommand.Text"/> is the key).</summary>
    Join,

    /// <summary>A session left: <c>ClientSeam.Disconnect</c> of its client.</summary>
    Leave,

    /// <summary>An InputFrame: <c>ClientSeam.SetHeldDirections</c> with <see cref="QueuedCommand.Dirs"/>.</summary>
    Input,

    /// <summary>A console command (<see cref="QueuedCommand.Text"/> is the line), from session 0, always authorized (D73).</summary>
    Console,

    // ---- M2 (contracts; P6 applies them) ----

    /// <summary>M2: the input frame the session's jitter buffer gave this tick (<see cref="QueuedCommand.Motion"/>): the locomotion seam's frame for the tick (<c>LocomotionSeam.SetFrame</c>). <see cref="QueuedCommand.Seq"/> is the frame's seq.</summary>
    Motion,

    /// <summary>M2: an action (<see cref="QueuedCommand.Action"/>, <see cref="QueuedCommand.Target"/>, <see cref="QueuedCommand.Button"/>, <see cref="QueuedCommand.Modifiers"/>, a dev command's line in <see cref="QueuedCommand.Text"/>), applied after its frame's Motion command: <c>ClientSeam</c>'s action entry points or <c>DevCommands</c>.</summary>
    Action,
}

/// <summary>
/// A command for the world, stamped on the host thread and applied in the command phase of <see cref="ApplyTick"/>
/// (scope 2j "Determinism with network input"): received while tick T is the last stepped, it applies at T + 1. Commands
/// of one tick apply in (<see cref="Session"/>, <see cref="Seq"/>) order, with <c>usr</c> the session's mob. Joins, leaves
/// and console commands are commands too; a rejected command never becomes one, so it never reaches the world or the
/// replay log. Everything a command carries is a plain value, so the replay log can write and read it.
/// <para>
/// The seqs the host gives (P3): an InputFrame keeps its own seq (from 1, growing); a Join is <see cref="JoinSeq"/> (0) and a
/// Leave <see cref="LeaveSeq"/> (the largest value), so that a session's Join comes before and its Leave after every other
/// command of the session in one tick; console commands (session 0) are numbered from 1 by the host.
/// </para>
/// </summary>
/// <param name="ApplyTick">The tick whose command phase applies it.</param>
/// <param name="Session">The session ordinal (0: the console).</param>
/// <param name="Seq">The order within the session: the InputFrame seq for inputs, the host's per-session counter for the others.</param>
/// <param name="Kind">What it does.</param>
/// <param name="Text">The key (Join) or the console line (Console); null otherwise.</param>
/// <param name="Dirs">The held directions (Input); None otherwise.</param>
/// <param name="Motion">M2: the frame of a <see cref="CommandKind.Motion"/> command; default otherwise.</param>
/// <param name="Action">M2: the kind of a <see cref="CommandKind.Action"/> command; <see cref="ActionKind.None"/> otherwise.</param>
/// <param name="Target">M2: a click's target entity id (M4a: an entity or screen id, by <paramref name="TargetKind"/>); 0 otherwise.</param>
/// <param name="Button">M2: a click's button.</param>
/// <param name="Modifiers">M2: a click's modifier keys.</param>
/// <param name="TargetKind">M4a: what a click or hover is on (<see cref="Anomaly.Protocol.TargetKind"/>); None with a <paramref name="Target"/> above 0 is an entity (a version-3 log).</param>
/// <param name="TurfX">M4a: a turf target's x.</param>
/// <param name="TurfY">M4a: a turf target's y.</param>
/// <param name="TurfZ">M4a: a turf target's z.</param>
/// <param name="IconX">M4a: tg's icon-x of a click, 0 for none.</param>
/// <param name="IconY">M4a: tg's icon-y of a click, 0 for none.</param>
/// <param name="Keybinding">M4a: the keybinding of a <see cref="ActionKind.Keybinding"/> command.</param>
/// <param name="KeyUp">M4a: the keybinding is a release.</param>
/// <param name="AimZone">The body zone a click aims at (DV30), None for none.</param>
/// <param name="RadialMenu">Version 15: the radial menu of a <see cref="ActionKind.RadialPick"/>; 0 otherwise.</param>
/// <param name="RadialElement">Version 15: the picked element (0 the centre).</param>
/// <param name="UiWindow">Version 16: the tgui window (pool index) of a <see cref="ActionKind.UiAct"/> or <see cref="ActionKind.UiClose"/>; 0 otherwise (the act's action is the text).</param>
/// <param name="UiParams">Version 16: a <see cref="ActionKind.UiAct"/>'s params as JSON, null for none.</param>
/// <param name="OverKind">Version 17: what a <see cref="ActionKind.Drop"/> was released over (the dragged thing is <paramref name="Target"/> by <paramref name="TargetKind"/>).</param>
/// <param name="OverTarget">Version 17: the id of an entity or screen <paramref name="OverKind"/>.</param>
/// <param name="OverX">Version 17: a turf <paramref name="OverKind"/>'s x.</param>
/// <param name="OverY">Version 17: a turf <paramref name="OverKind"/>'s y.</param>
/// <param name="OverZ">Version 17: a turf <paramref name="OverKind"/>'s z.</param>
/// <param name="DragTime">Version 17: the drag's length in deciseconds (at most 255).</param>
/// <param name="Aim">Version 34: the camera's aim of a click or a mouse action (DV94), null for none.</param>
/// <param name="WaitsForLoad">Version 97 (DV597): a Join of a client that loads the world first (<see cref="Session.AwaitingLoad"/>).</param>
public readonly record struct QueuedCommand(long ApplyTick, int Session, uint Seq, CommandKind Kind, string Text = null, HeldDirs Dirs = HeldDirs.None,
    Anomaly.Core.Motion.MotionInput Motion = default, ActionKind Action = ActionKind.None, long Target = 0, ClickButton Button = ClickButton.None, ClickModifiers Modifiers = ClickModifiers.None,
    TargetKind TargetKind = TargetKind.None, int TurfX = 0, int TurfY = 0, int TurfZ = 0, byte IconX = 0, byte IconY = 0, KeybindingCode Keybinding = KeybindingCode.None, bool KeyUp = false, AimZone AimZone = AimZone.None,
    long RadialMenu = 0, int RadialElement = 0, int UiWindow = 0, string UiParams = null,
    TargetKind OverKind = TargetKind.None, long OverTarget = 0, int OverX = 0, int OverY = 0, int OverZ = 0, byte DragTime = 0, ShotAim? Aim = null,
    string SayType = null, string SayChannel = null, bool SayVisible = false, bool WaitsForLoad = false)
{
    /// <summary>The seq of a Join: first in its session.</summary>
    public const uint JoinSeq = 0;

    /// <summary>The seq of a Leave: last in its session.</summary>
    public const uint LeaveSeq = uint.MaxValue;
}

/// <summary>
/// The commands waiting for their tick. Host thread only (the console's reader thread hands lines to the host, it never
/// touches the queue).
/// </summary>
public sealed class CommandQueue
{
    // Pending commands with their insertion number (the last key of the order, so equal keys keep their arrival order).
    private readonly List<(QueuedCommand Command, long Order)> _pending = new();
    private readonly Dictionary<(int Session, long Tick), int> _perTick = new();
    private readonly List<(QueuedCommand Command, long Order)> _due = new();
    private long _order;

    private static readonly Comparison<(QueuedCommand Command, long Order)> ByApplyOrder = static (a, b) =>
    {
        int c = a.Command.ApplyTick.CompareTo(b.Command.ApplyTick);
        if (c != 0) return c;
        c = a.Command.Session.CompareTo(b.Command.Session);
        if (c != 0) return c;
        c = a.Command.Seq.CompareTo(b.Command.Seq);
        return c != 0 ? c : a.Order.CompareTo(b.Order);
    };

    /// <summary>Commands waiting.</summary>
    public int Count => _pending.Count;

    /// <summary>M10 slice H: drops every waiting command (the world they were queued for is replaced by a new round's).</summary>
    public void Clear()
    {
        _pending.Clear();
        _perTick.Clear();
        _due.Clear();
    }

    /// <summary>Adds a command (its <c>ApplyTick</c> after the last stepped tick).</summary>
    public void Enqueue(in QueuedCommand command)
    {
        _pending.Add((command, _order++));
        var key = (command.Session, command.ApplyTick);
        _perTick[key] = _perTick.TryGetValue(key, out int n) ? n + 1 : 1;
    }

    /// <summary>Commands of <paramref name="session"/> waiting for <paramref name="applyTick"/> (the per-tick cap, <see cref="Limits.MaxQueuedCommandsPerSessionPerTick"/>).</summary>
    public int CountFor(int session, long applyTick) => _perTick.TryGetValue((session, applyTick), out int n) ? n : 0;

    /// <summary>Moves every command with <c>ApplyTick &lt;= tick</c> into <paramref name="into"/>, in (ApplyTick, Session, Seq) order; returns how many.</summary>
    public int TakeDue(long tick, List<QueuedCommand> into)
    {
        ArgumentNullException.ThrowIfNull(into);
        _due.Clear();
        int kept = 0;
        for (int i = 0; i < _pending.Count; i++)
        {
            var entry = _pending[i];
            if (entry.Command.ApplyTick <= tick)
            {
                _due.Add(entry);
                Uncount(entry.Command);
            }
            else
            {
                _pending[kept++] = entry;
            }
        }
        _pending.RemoveRange(kept, _pending.Count - kept);
        _due.Sort(ByApplyOrder);
        foreach (var entry in _due)
            into.Add(entry.Command);
        int taken = _due.Count;
        _due.Clear();
        return taken;
    }

    /// <summary>Drops every command of <paramref name="session"/> (it left before they applied); a queued Leave stays.</summary>
    public void DropSession(int session)
    {
        int kept = 0;
        for (int i = 0; i < _pending.Count; i++)
        {
            var entry = _pending[i];
            if (entry.Command.Session == session && entry.Command.Kind != CommandKind.Leave)
                Uncount(entry.Command);
            else
                _pending[kept++] = entry;
        }
        _pending.RemoveRange(kept, _pending.Count - kept);
    }

    private void Uncount(in QueuedCommand command)
    {
        var key = (command.Session, command.ApplyTick);
        if (!_perTick.TryGetValue(key, out int n)) return;
        if (n <= 1) _perTick.Remove(key);
        else _perTick[key] = n - 1;
    }
}

/// <summary>The console's verbs (scope 2h: D73's core subset and the cheap extras).</summary>
public enum ConsoleVerb : byte
{
    Unknown,

    /// <summary><c>spawn TYPE [X Y [Z]]</c>: tg's spawn_atom core, <c>new path(turf)</c> with <c>ADMIN_SPAWNED_1</c> and <c>log_admin</c>.</summary>
    Spawn,

    /// <summary><c>teleport NAME|ID X Y [Z]</c>: tg's Jump To Coordinate, <c>abstract_move(locate(x, y, z))</c>; <c>teleport NAME|ID TO</c>: tg's Jump To Mob and Get Mob.</summary>
    Teleport,

    /// <summary><c>list</c>: the sessions with tg's <c>key_name</c>, like the <c>who</c> verb.</summary>
    List,

    /// <summary><c>kick NAME|ID [reason]</c>: tg's boot2, <c>to_chat</c>, <c>log_admin</c>, <c>qdel(client)</c>.</summary>
    Kick,

    /// <summary><c>status</c>: tick and metrics.</summary>
    Status,

    /// <summary><c>hash</c>: the state hash now.</summary>
    Hash,

    /// <summary><c>quit</c>: clean shutdown through tg's <c>world/Del</c>.</summary>
    Quit,

    /// <summary><c>help</c>.</summary>
    Help,

    /// <summary>M2 P6: <c>dev-help</c>, the dev commands' syntax.</summary>
    DevHelp,

    /// <summary>M2 P6: a dev command (<see cref="DevVerb"/>: <c>damage</c>, <c>heal</c>, <c>kill</c>, <c>gas</c>, <c>analyze</c>, ...), tg's own procs on a target (D73, <see cref="DevCommandLine"/>).</summary>
    Dev,

    /// <summary>M10 slice I: <c>ban KEY DURATION REASON</c>, tg's create_ban of a server ban for the key and its last address.</summary>
    Ban,

    /// <summary>M10 slice I: <c>ban-ip IP DURATION REASON</c>, tg's create_ban of a server ban for an address.</summary>
    BanIp,

    /// <summary>M10 slice I: <c>unban ID</c>, tg's unban.</summary>
    Unban,

    /// <summary>M10 slice I: <c>bans [KEY|IP]</c>, the active server bans (tg's unban panel's list).</summary>
    Bans,

    /// <summary>M10 slice I: <c>note KEY [secret] TEXT</c>, tg's create_message of a note.</summary>
    Note,

    /// <summary>M10 slice I: <c>watch KEY TEXT</c>, tg's create_message of a watchlist entry.</summary>
    Watch,

    /// <summary>M10 slice I: <c>notes KEY</c>, a key's notes and watchlist entries (tg's browse_messages).</summary>
    Notes,

    /// <summary>M10 slice I: <c>note-delete ID</c>, tg's delete_message.</summary>
    NoteDelete,

    /// <summary>M10x slice G6: <c>tickets [active|closed|resolved]</c>, tg's ticket list window (the stat panel's ticket lines, BrowseTickets).</summary>
    Tickets,

    /// <summary>M10x slice G6: <c>pm KEY [MESSAGE]</c>, tg's Admin PM (cmd_admin_pm; without a message it asks for one).</summary>
    Pm,

    /// <summary>Syndicate A1 (D316): <c>check-antagonists</c>, tg's Check Antagonists (the Round Status page).</summary>
    CheckAntagonists,

    /// <summary>Syndicate A1 (D316): <c>traitor-panel NAME|ID</c>, tg's Show Traitor Panel of a mob's mind.</summary>
    TraitorPanel,

    /// <summary>Syndicate A1 (D316): <c>check-teams</c>, tg's team listing (the Show Teams link).</summary>
    CheckTeams,

    /// <summary>Gaps SL1 (D333): <c>skill-panel NAME|ID</c>, tg's Show Skill Panel of a mob's mind (the SkillPanel window).</summary>
    SkillPanel,

    /// <summary>Syndicate A1 (D316): <c>combo-hud</c>, tg's Toggle Combo HUD (the data HUDs and every mind's antagonist icons).</summary>
    ComboHud,

    /// <summary>Syndicate A3 (D316): <c>dynamic-panel</c>, tg's Dynamic Panel (the DynamicAdmin window).</summary>
    DynamicPanel,

    /// <summary>Syndicate A3 (D316): <c>dynamic-tester</c>, tg's Dynamic Tester (the DynamicTester window).</summary>
    DynamicTester,

    /// <summary><c>secrets</c>, tg's Secrets panel (the Secrets window: the station's power and lights, DV351).</summary>
    Secrets,

    /// <summary>Gaps R2: <c>station-traits</c>, tg's Modify Station Traits (the StationTraitsPanel window).</summary>
    StationTraits,

    /// <summary>Gaps CI1 (D339): <c>circuits</c>, tg's View All Circuits (the CircuitAdminPanel window).</summary>
    Circuits,

    /// <summary>Gaps CI1 (D339): <c>load-circuit JSON</c>, tg's Load Circuit with the circuit's JSON typed as its direct input.</summary>
    LoadCircuit,
}

/// <summary>A parsed console line.</summary>
/// <param name="Verb">The verb.</param>
/// <param name="Args">The words after it.</param>
/// <param name="Line">The line as typed.</param>
public readonly record struct ConsoleCommand(ConsoleVerb Verb, IReadOnlyList<string> Args, string Line)
{
    /// <summary>The longest console line accepted (a typed line; the replay log writes it whole).</summary>
    public const int MaxLineLength = 1024;

    /// <summary>The console's help text.</summary>
    public const string Help =
        "Console commands (docs/ai/specs/apps.md 1.6):\n"
        + "  spawn TYPE [X Y [Z]]        new TYPE (an /obj or /mob path) at the turf, or at the room's first spawn point\n"
        + "  teleport NAME|ID X Y [Z]    move a player's mob (by key) or any movable (by world id) to the turf\n"
        + "  list                        the sessions, with tg's key_name of their mobs\n"
        + "  kick NAME|ID [REASON]       tg's kick: a message to the player, log_admin, qdel(client)\n"
        + "  status                      tick, sessions and the last metrics window\n"
        + "  hash                        the world's state hash now (slow: a full GC)\n"
        + "  quit                        clean shutdown (kick everyone, world/Del, logs)\n"
        + "  damage|heal|fullheal|kill|revive|knockdown|stun|bleed|blood|gas|analyze|speedscale|godmode ...   the dev commands (dev-help lists them)\n"
        + "  ban KEY DURATION REASON     a server ban of the key and its last address (DURATION: perm, or minutes: 30, 2h, 7d, 1w)\n"
        + "  ban-ip IP DURATION REASON   a server ban of an address\n"
        + "  unban ID                    lift a ban\n"
        + "  bans [KEY|IP]               the active bans\n"
        + "  note KEY [secret] TEXT      a note on a key (shown to the player at connection unless secret)\n"
        + "  watch KEY TEXT              a watchlist entry (the admins hear when the key connects)\n"
        + "  notes KEY                   a key's notes and watchlist entries\n"
        + "  note-delete ID              delete a note or watchlist entry\n"
        + "  tickets [active|closed|resolved]  the admin help tickets (a window; default active)\n"
        + "  pm KEY [MESSAGE]            an admin PM to a player (asks for the message when none is given)\n"
        + "  check-antagonists           the round status and its antagonists (a window)\n"
        + "  traitor-panel NAME|ID       a mob's traitor panel: its antagonists and objectives (a window)\n"
        + "  check-teams                 the antagonist teams (a window)\n"
        + "  skill-panel NAME|ID         a mob's skills: their levels and experience (a window)\n"
        + "  combo-hud                   toggle the admin combo HUD (the data HUDs and the antagonist icons)\n"
        + "  dynamic-panel               Dynamic, the round's mode: its tier, rulesets and config (a window)\n"
        + "  dynamic-tester              Dynamic's ruleset weights by tier and population (a window)\n"
        + "  secrets                     the station's power and lights (a window)\n"
        + "  station-traits              the round's station traits and the next round's (a window)\n"
        + "  circuits                    every integrated circuit in the game (a window)\n"
        + "  load-circuit JSON           a circuit loaded from its saved JSON at your feet\n"
        + "  dev-help                    the dev commands' syntax\n"
        + "  help                        this text";

    /// <summary>True for the verbs that act in the world (applied in the command phase and recorded); the others run on the host between ticks.</summary>
    public bool IsWorldCommand => Verb is ConsoleVerb.Spawn or ConsoleVerb.Teleport or ConsoleVerb.List or ConsoleVerb.Kick or ConsoleVerb.Quit or ConsoleVerb.Dev
        or ConsoleVerb.Ban or ConsoleVerb.BanIp or ConsoleVerb.Unban or ConsoleVerb.Bans or ConsoleVerb.Note or ConsoleVerb.Watch or ConsoleVerb.Notes or ConsoleVerb.NoteDelete
        or ConsoleVerb.Tickets or ConsoleVerb.Pm
        or ConsoleVerb.CheckAntagonists or ConsoleVerb.TraitorPanel or ConsoleVerb.CheckTeams or ConsoleVerb.ComboHud
        or ConsoleVerb.DynamicPanel or ConsoleVerb.DynamicTester or ConsoleVerb.Secrets or ConsoleVerb.StationTraits or ConsoleVerb.SkillPanel
        or ConsoleVerb.Circuits or ConsoleVerb.LoadCircuit;

    /// <summary>
    /// M10 slice I: a ban's DURATION, <c>perm</c> (null) or minutes (<c>30</c>, <c>30m</c>, <c>2h</c>, <c>7d</c>, <c>1w</c>); false for anything else.
    /// </summary>
    public static bool TryDuration(string text, out long? minutes)
    {
        minutes = null;
        if (text is "perm" or "permanent") return true;
        if (string.IsNullOrEmpty(text)) return false;
        long unit = char.ToLowerInvariant(text[^1]) switch { 'm' => 1, 'h' => 60, 'd' => 1440, 'w' => 10080, _ => 0 };
        string number = unit == 0 ? text : text[..^1];
        if (unit == 0) unit = 1;
        if (!long.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out long n) || n < 1 || n > 52560000L / unit) return false;
        minutes = n * unit;
        return true;
    }

    /// <summary>The words from <paramref name="first"/> on, joined by spaces (a reason, a note's text).</summary>
    public string Rest(int first) => string.Join(' ', Args.Skip(first));

    /// <summary>The <c>X Y [Z]</c> at <paramref name="first"/> (Z defaults to 1); valid after a successful parse.</summary>
    public (int X, int Y, int Z) CoordinatesAt(int first)
        => (Int(Args[first]), Int(Args[first + 1]), Args.Count > first + 2 ? Int(Args[first + 2]) : 1);

    /// <summary>The kick reason: the words after the target, or null.</summary>
    public string KickReason => Args.Count > 1 ? string.Join(' ', Args.Skip(1)) : null;

    /// <summary>Parses a line; false with <paramref name="error"/> for an unknown verb or wrong arguments.</summary>
    public static bool TryParse(string line, out ConsoleCommand command, out string error)
    {
        command = default;
        error = null;
        if (line == null) { error = "no line"; return false; }
        if (line.Length > MaxLineLength) { error = $"the line is longer than {MaxLineLength} characters"; return false; }
        foreach (char c in line)
        {
            if (char.IsControl(c) && c != '\t') { error = "the line has control characters"; return false; }
        }
        string[] words = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) { error = "empty line"; return false; }

        ConsoleVerb verb = words[0].ToLowerInvariant() switch
        {
            "spawn" => ConsoleVerb.Spawn,
            "teleport" or "tp" => ConsoleVerb.Teleport,
            "list" or "who" => ConsoleVerb.List,
            "kick" => ConsoleVerb.Kick,
            "status" => ConsoleVerb.Status,
            "hash" => ConsoleVerb.Hash,
            "quit" or "exit" or "shutdown" => ConsoleVerb.Quit,
            "help" or "?" => ConsoleVerb.Help,
            "dev-help" => ConsoleVerb.DevHelp,
            "ban" => ConsoleVerb.Ban,
            "ban-ip" => ConsoleVerb.BanIp,
            "unban" => ConsoleVerb.Unban,
            "bans" => ConsoleVerb.Bans,
            "note" => ConsoleVerb.Note,
            "watch" => ConsoleVerb.Watch,
            "notes" => ConsoleVerb.Notes,
            "note-delete" => ConsoleVerb.NoteDelete,
            "tickets" => ConsoleVerb.Tickets,
            "pm" => ConsoleVerb.Pm,
            "check-antagonists" => ConsoleVerb.CheckAntagonists,
            "traitor-panel" => ConsoleVerb.TraitorPanel,
            "skill-panel" => ConsoleVerb.SkillPanel,
            "check-teams" => ConsoleVerb.CheckTeams,
            "combo-hud" => ConsoleVerb.ComboHud,
            "dynamic-panel" => ConsoleVerb.DynamicPanel,
            "dynamic-tester" => ConsoleVerb.DynamicTester,
            "secrets" => ConsoleVerb.Secrets,
            "station-traits" => ConsoleVerb.StationTraits,
            "circuits" => ConsoleVerb.Circuits,
            "load-circuit" => ConsoleVerb.LoadCircuit,
            _ when DevCommandLine.IsDevVerb(words[0]) => ConsoleVerb.Dev,
            _ => ConsoleVerb.Unknown,
        };
        if (verb == ConsoleVerb.Unknown) { error = $"unknown command '{words[0]}' (help lists the commands)"; return false; }
        string[] args = words.Skip(1).ToArray();

        switch (verb)
        {
            case ConsoleVerb.Spawn:
                if (args.Length != 1 && args.Length != 3 && args.Length != 4) { error = "usage: spawn TYPE [X Y [Z]]"; return false; }
                if (!args[0].StartsWith('/')) { error = $"spawn: '{args[0]}' is not a type path (for example /obj/item)"; return false; }
                if (args.Length > 1 && !Coordinates(args, 1, out error)) return false;
                break;
            case ConsoleVerb.Teleport:
                // two words: onto a player or a #id; a bare number is half a coordinate, not an id
                if (args.Length == 2 && !args[1].All(char.IsAsciiDigit)) break;
                if (args.Length != 3 && args.Length != 4) { error = "usage: teleport NAME|ID X Y [Z], or teleport NAME|ID TO_NAME|#ID"; return false; }
                if (!Coordinates(args, 1, out error)) return false;
                break;
            case ConsoleVerb.Kick:
                if (args.Length < 1) { error = "usage: kick NAME|ID [REASON]"; return false; }
                break;
            case ConsoleVerb.Ban or ConsoleVerb.BanIp:
                if (args.Length < 3) { error = $"usage: {words[0].ToLowerInvariant()} {(verb == ConsoleVerb.Ban ? "KEY" : "IP")} DURATION REASON"; return false; }
                if (verb == ConsoleVerb.BanIp && !System.Net.IPAddress.TryParse(args[0], out _)) { error = $"ban-ip: '{args[0]}' is not an IP address"; return false; }
                if (!TryDuration(args[1], out _)) { error = $"{words[0].ToLowerInvariant()}: '{args[1]}' is not a duration (perm, or minutes: 30, 2h, 7d, 1w)"; return false; }
                break;
            case ConsoleVerb.Unban or ConsoleVerb.NoteDelete:
                if (args.Length != 1 || !long.TryParse(args[0].TrimStart('#'), NumberStyles.None, CultureInfo.InvariantCulture, out _)) { error = $"usage: {words[0].ToLowerInvariant()} ID"; return false; }
                break;
            case ConsoleVerb.Bans:
                if (args.Length > 1) { error = "usage: bans [KEY|IP]"; return false; }
                break;
            case ConsoleVerb.Note or ConsoleVerb.Watch:
                if (args.Length < 2 || (verb == ConsoleVerb.Note && args[1] == "secret" && args.Length < 3)) { error = $"usage: {(verb == ConsoleVerb.Note ? "note KEY [secret] TEXT" : "watch KEY TEXT")}"; return false; }
                break;
            case ConsoleVerb.Notes:
                if (args.Length != 1) { error = "usage: notes KEY"; return false; }
                break;
            case ConsoleVerb.Tickets:
                if (args.Length > 1 || (args.Length == 1 && args[0].ToLowerInvariant() is not ("active" or "closed" or "resolved"))) { error = "usage: tickets [active|closed|resolved]"; return false; }
                break;
            case ConsoleVerb.Pm:
                if (args.Length < 1) { error = "usage: pm KEY [MESSAGE]"; return false; }
                break;
            case ConsoleVerb.TraitorPanel:
                if (args.Length != 1) { error = "usage: traitor-panel NAME|ID"; return false; }
                break;
            case ConsoleVerb.SkillPanel:
                if (args.Length != 1) { error = "usage: skill-panel NAME|ID"; return false; }
                break;
            case ConsoleVerb.LoadCircuit:
                if (args.Length < 1) { error = "usage: load-circuit JSON"; return false; }
                break;
            case ConsoleVerb.Dev:
                // The console's "spawn" stays the general spawn above; the dev verbs parse in full here, so a bad line is
                // refused before it is queued.
                if (!DevCommandLine.TryParse(line, out _, out error)) return false;
                break;
            default:
                if (args.Length != 0) { error = $"{words[0].ToLowerInvariant()} takes no arguments"; return false; }
                break;
        }
        command = new ConsoleCommand(verb, args, line);
        return true;
    }

    private static bool Coordinates(string[] args, int first, out string error)
    {
        error = null;
        for (int i = first; i < args.Length; i++)
        {
            if (!int.TryParse(args[i], NumberStyles.None, CultureInfo.InvariantCulture, out int v) || v < 1 || v > Limits.MaxMapSize)
            {
                error = $"'{args[i]}' is not a map coordinate (1 to {Limits.MaxMapSize})";
                return false;
            }
        }
        return true;
    }

    private static int Int(string text) => int.Parse(text, NumberStyles.None, CultureInfo.InvariantCulture);
}
