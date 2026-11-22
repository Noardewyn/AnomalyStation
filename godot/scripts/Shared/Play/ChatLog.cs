using System.Linq;
using System.Text.RegularExpressions;

namespace Anomaly.Client.Shared;

// Owner: M9 slice C (D259, chat.md 3). Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).
// A port of tgui/packages/tgui-panel/chat (constants.ts, model.ts, renderer.tsx's processBatch, changePage, pruneMessages and clearChat,
// helpers.ts's updateMessageCount, use-chat-pages.ts's changeChatPage): the big chat window's log. A line's type is the one tg's server
// gave it or, as in tg, the first type whose selector matches one of its elements; the pages (tabs) show the types they accept; a line
// repeated within 5 s is counted on the earlier one (tg's badge); unread lines are counted per page. The Godot window (Hud/ChatWindow.cs)
// draws the visible lines.
// Original copyright and license: tgui-panel, Aleksej Komarov 2020, MIT (tgui/packages/tgui-panel). See ../tgstation/LICENSE.

/// <summary>tg's <c>MESSAGE_TYPES</c> entry: a type, its name, and the CSS selector that sorts an untyped line into it.</summary>
public sealed record ChatMessageType(string Type, string Name, string Description, string Selector = null, bool Important = false, bool Admin = false);

/// <summary>tg's chat page (a tab): the types it shows and its unread count.</summary>
public sealed class ChatPage
{
    public string Id { get; init; }

    public string Name { get; set; }

    public bool IsMain { get; init; }

    public Dictionary<string, bool> AcceptedTypes { get; } = new(StringComparer.Ordinal);

    public int UnreadCount { get; set; }

    public bool HideUnreadCount { get; set; }

    // tg: chat/model.ts canPageAcceptType
    public bool Accepts(string type) => type.StartsWith(ChatLog.TypeInternal, StringComparison.Ordinal) || AcceptedTypes.GetValueOrDefault(type);
}

/// <summary>A line of the log: tg's message (its type, its HTML, and the times it was repeated), made plain for the window.</summary>
public sealed class ChatEntry
{
    public string Type { get; set; }

    public string Html { get; init; }

    public string Plain { get; set; }

    public IReadOnlyList<ChatLink> Links { get; set; }

    public IReadOnlyList<ChatSpan> Spans { get; set; }

    /// <summary>How many times the line came (tg's <c>times</c>: the badge "xN" from 2 on).</summary>
    public int Times { get; set; } = 1;

    /// <summary>When it came, in seconds.</summary>
    public double CreatedAt { get; init; }
}

public sealed class ChatLog
{
    // tg: chat/constants.ts
    public const int MaxVisibleMessages = 2500;
    public const int MaxPersistedMessages = 1000;
    public const double MessagePruneInterval = 60.0;
    public const int CombineMaxMessages = 5;
    public const double CombineMaxTimeWindow = 5.0;

    public const string TypeUnknown = "unknown";
    public const string TypeInternal = "internal";
    public const string TypeSystem = "system";
    public const string TypeLocalChat = "localchat";
    public const string TypeRadio = "radio";
    public const string TypeEntertainment = "entertainment";
    public const string TypeInfo = "info";
    public const string TypeWarning = "warning";
    public const string TypeDeadChat = "deadchat";
    public const string TypeOoc = "ooc";
    public const string TypeAdminPm = "adminpm";
    public const string TypeCombat = "combat";
    public const string TypeAdminChat = "adminchat";
    public const string TypeModChat = "modchat";
    public const string TypePrayer = "prayer";
    public const string TypeEventChat = "eventchat";
    public const string TypeAdminLog = "adminlog";
    public const string TypeAttackLog = "attacklog";
    public const string TypeDebug = "debug";

    /// <summary>M9 slice E (DV171): the PDA messenger's lines, the port's own type (tg's are untyped info) for the Messages tab.</summary>
    public const string TypePda = "pda";

    /// <summary>tg's <c>MESSAGE_TYPES</c>, in tg's order (the first matching selector wins).</summary>
    public static readonly IReadOnlyList<ChatMessageType> MessageTypes = new ChatMessageType[]
    {
        // Always-on types
        new(TypeSystem, "System Messages", "Messages from your client, always enabled", ".boldannounce", Important: true),
        // Basic types
        new(TypeLocalChat, "Local", "In-character local messages (say, emote, etc)", ".say, .emote"),
        new(TypeRadio, "Radio", "All departments of radio messages",
            ".alert, .minorannounce, .syndradio, .centcomradio, .aiprivradio, .comradio, .secradio, .gangradio, .engradio, .medradio, .sciradio, .suppradio, .servradio, .radio, .deptradio, .binarysay, .resonate, .abductor, .alien, .changeling"),
        new(TypeEntertainment, "Entertainment", "Entertainment and newscaster broadcasts", ".enteradio, .newscaster"),
        new(TypeInfo, "Info", "Non-urgent messages from the game and items",
            ".notice:not(.pm), .adminnotice, .info, .sinister, .cult, .infoplain, .announce, .hear, .smallnotice, .holoparasite, .boldnotice"),
        new(TypeWarning, "Warnings", "Urgent messages from the game and items", ".warning:not(.pm), .critical, .userdanger, .italics, .alertsyndie, .warningplain"),
        new(TypeDeadChat, "Deadchat", "All of deadchat", ".deadsay, .ghostalert"),
        new(TypeOoc, "OOC", "The bluewall of global OOC messages", ".ooc, .adminooc, .adminobserverooc, .oocplain"),
        new(TypeAdminPm, "Admin PMs", "Messages to/from admins (adminhelp)", ".pm, .adminhelp"),
        new(TypeCombat, "Combat Log", "Urist McTraitor has stabbed you with a knife!", ".danger"),
        new(TypePda, "Messages", "PDA messages sent and received (the port's, DV171)"),
        new(TypeUnknown, "Unsorted", "Everything we could not sort, always enabled"),
        // Admin stuff
        new(TypeAdminChat, "Admin Chat", "ASAY messages", ".admin_channel, .adminsay", Admin: true),
        new(TypeModChat, "Mod Chat", "MSAY messages", ".mod_channel", Admin: true),
        new(TypePrayer, "Prayers", "Prayers from players", Admin: true),
        new(TypeAdminLog, "Admin Log", "ADMIN LOG: Urist McAdmin has jumped to coordinates X, Y, Z", ".log_message", Admin: true),
        new(TypeAttackLog, "Attack Log", "Urist McTraitor has shot John Doe", Admin: true),
        new(TypeDebug, "Debug Log", "DEBUG: SSPlanets subsystem Recover().", Admin: true),
    };

    private readonly List<ChatEntry> _messages = new();
    private readonly List<ChatEntry> _visible = new();
    private double _lastPruneAt;

    public ChatLog()
    {
        Pages = DefaultPages();
        Current = Pages[0];
    }

    /// <summary>The tabs, the main one first.</summary>
    public List<ChatPage> Pages { get; }

    public ChatPage Current { get; private set; }

    /// <summary>The lines the current page shows, oldest first.</summary>
    public IReadOnlyList<ChatEntry> Visible => _visible;

    /// <summary>The lines kept (every page's).</summary>
    public int Count => _messages.Count;

    /// <summary>tg's <c>scrollTracking</c>: the log is scrolled to its end (the view reports it).</summary>
    public bool ScrollTracking { get; private set; } = true;

    /// <summary>Counts every change of <see cref="Visible"/> that is not lines added or re-counted at its end (a page change, a prune, a clear): the view redraws all.</summary>
    public int Generation { get; private set; }

    private int _dirtyFrom = int.MaxValue;

    /// <summary>
    /// D259 (chat.md 3): tg's main page (all types) and pages made as tg's <c>createPage</c> makes them (the important types on) for Local,
    /// Radio, Combat and OOC, and (M9 slice E) Messages, the PDA's.
    /// </summary>
    public static List<ChatPage> DefaultPages()
    {
        var main = CreatePage("main", "All", true);
        foreach (ChatMessageType type in MessageTypes)
            main.AcceptedTypes[type.Type] = true;
        ChatPage With(string id, string name, string type)
        {
            ChatPage page = CreatePage(id, name, false);
            page.AcceptedTypes[type] = true;
            return page;
        }
        return new List<ChatPage> { main, With("local", "Local", TypeLocalChat), With("radio", "Radio", TypeRadio), With("combat", "Combat", TypeCombat), With("ooc", "OOC", TypeOoc), With("messages", "Messages", TypePda), With("dead", "Dead", TypeDeadChat) }; // M10x slice B: dead chat
    }

    // tg: chat/model.ts createPage
    private static ChatPage CreatePage(string id, string name, bool isMain)
    {
        var page = new ChatPage { Id = id, Name = name, IsMain = isMain };
        foreach (ChatMessageType type in MessageTypes)
            page.AcceptedTypes[type.Type] = type.Important;
        return page;
    }

    /// <summary>
    /// A line from the server (tg's processBatch of one): a repeat of one of the last 5 visible lines within 5 s counts on it; otherwise the
    /// line is typed (the server's type or tg's selectors), kept, shown if the page takes it, and counted unread on the pages that take it.
    /// Returns the new line, or null when there is none (an empty line, a repeat).
    /// </summary>
    public ChatEntry Add(string type, string html, double now)
    {
        if (string.IsNullOrEmpty(html))
            return null;
        // Combine messages
        ChatEntry combinable = GetCombinableMessage(html, now);
        if (combinable != null)
        {
            combinable.Times++;
            MarkDirty(_visible.LastIndexOf(combinable));
            return null;
        }
        var links = new List<ChatLink>();
        var spans = new List<ChatSpan>();
        string plain = HtmlText.ToPlain(html, links, spans);
        var message = new ChatEntry
        {
            Type = string.IsNullOrEmpty(type) ? TypeOf(html) : type,
            Html = html,
            Plain = plain,
            Links = links.Count > 0 ? links : null,
            Spans = spans.Count > 0 ? spans : null,
            CreatedAt = now,
        };
        _messages.Add(message);
        if (Current.Accepts(message.Type))
        {
            _visible.Add(message);
            MarkDirty(_visible.Count - 1);
        }
        UpdateMessageCount(message.Type);
        return message;
    }

    /// <summary>D319: a kept line's text changes (its translation came): the window draws it again.</summary>
    public void Retext(ChatEntry entry, string plain, IReadOnlyList<ChatSpan> spans, IReadOnlyList<ChatLink> links)
    {
        entry.Plain = plain;
        entry.Spans = spans is { Count: > 0 } ? spans : null;
        entry.Links = links is { Count: > 0 } ? links : null;
        MarkDirty(_visible.IndexOf(entry));
    }

    // tg: chat/renderer.tsx getCombinableMessage
    private ChatEntry GetCombinableMessage(string html, double now)
    {
        int from = _visible.Count - 1;
        int to = Math.Max(0, _visible.Count - CombineMaxMessages);
        for (int i = from; i >= to; i--)
        {
            ChatEntry message = _visible[i];
            if (!message.Type.StartsWith(TypeInternal, StringComparison.Ordinal) && message.Html == html && now < message.CreatedAt + CombineMaxTimeWindow)
                return message;
        }
        return null;
    }

    // tg: chat/helpers.ts updateMessageCount
    private void UpdateMessageCount(string type)
    {
        foreach (ChatPage page in Pages)
        {
            // Message does not belong here
            if (!page.Accepts(type))
                continue;
            // Current page scroll tracked
            if (page == Current && ScrollTracking)
                continue;
            // This page received the same message which we can read on the current page
            if (page != Current && Current.Accepts(type))
                continue;
            page.UnreadCount++;
        }
    }

    /// <summary>tg's changeChatPage: the page becomes current, its unread count is cleared, and it shows the kept lines it takes.</summary>
    public void ChangePage(ChatPage page)
    {
        if (page == null || !Pages.Contains(page))
            return;
        Current = page;
        page.UnreadCount = 0;
        _visible.Clear();
        foreach (ChatEntry message in _messages)
        {
            if (page.Accepts(message.Type))
                _visible.Add(message);
        }
        Changed();
    }

    /// <summary>The view's scroll state (tg's handleScroll); back at the end, the current page's unread count is cleared (ChatPanel).</summary>
    public void SetScrollTracking(bool tracking)
    {
        if (tracking == ScrollTracking)
            return;
        ScrollTracking = tracking;
        if (tracking)
            Current.UnreadCount = 0;
    }

    /// <summary>tg's periodic pruning (every <see cref="MessagePruneInterval"/>), on the clock the view passes in.</summary>
    public void Advance(double now)
    {
        if (now - _lastPruneAt < MessagePruneInterval)
            return;
        _lastPruneAt = now;
        PruneMessages();
    }

    // tg: chat/renderer.tsx pruneMessages
    public void PruneMessages()
    {
        // Delay pruning because user is currently interacting with chat history
        if (!ScrollTracking)
            return;
        // Visible messages
        int fromIndex = Math.Max(0, _visible.Count - MaxVisibleMessages);
        if (fromIndex > 0)
        {
            var pruned = new HashSet<ChatEntry>(_visible.GetRange(0, fromIndex));
            _visible.RemoveRange(0, fromIndex);
            _messages.RemoveAll(pruned.Contains);
            Changed();
        }
        // All messages
        fromIndex = Math.Max(0, _messages.Count - MaxPersistedMessages);
        if (fromIndex > 0)
            _messages.RemoveRange(0, fromIndex);
    }

    /// <summary>tg's clearChat: the current page's lines are dropped from the log.</summary>
    public void Clear()
    {
        var cleared = new HashSet<ChatEntry>(_visible);
        _visible.Clear();
        _messages.RemoveAll(cleared.Contains);
        Changed();
    }

    /// <summary>The first index of <see cref="Visible"/> added or re-counted since the last call (<see cref="int.MaxValue"/>: none); the view redraws from there (all when <see cref="Generation"/> moved on).</summary>
    public int TakeChanges()
    {
        int from = _dirtyFrom;
        _dirtyFrom = int.MaxValue;
        return from;
    }

    private void MarkDirty(int index)
    {
        if (index >= 0)
            _dirtyFrom = Math.Min(_dirtyFrom, index);
    }

    private void Changed()
    {
        Generation++;
        _dirtyFrom = int.MaxValue;
    }

    private static readonly Regex ClassAttribute = new(@"<[A-Za-z][^>]*?\sclass\s*=\s*(?:""([^""]*)""|'([^']*)'|([^\s>]+))", RegexOptions.CultureInvariant);

    /// <summary>tg's renderer: the first of <see cref="MessageTypes"/> whose selector matches an element of the line, else unknown.</summary>
    public static string TypeOf(string html)
    {
        var elements = new List<string[]>();
        foreach (Match m in ClassAttribute.Matches(html ?? ""))
        {
            string classes = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Success ? m.Groups[2].Value : m.Groups[3].Value;
            elements.Add(classes.Split((char[])null, StringSplitOptions.RemoveEmptyEntries));
        }
        foreach (ChatMessageType typeDef in MessageTypes)
        {
            if (typeDef.Selector != null && elements.Any(classes => Matches(typeDef.Selector, classes)))
                return typeDef.Type;
        }
        return TypeUnknown;
    }

    // A selector list of tg's forms ".a" and ".a:not(.b)" against one element's classes.
    private static bool Matches(string selector, string[] classes)
    {
        foreach (string part in selector.Split(','))
        {
            string s = part.Trim();
            string not = null;
            int at = s.IndexOf(":not(", StringComparison.Ordinal);
            if (at >= 0)
            {
                not = s.Substring(at + 6, s.Length - at - 7);
                s = s.Substring(0, at);
            }
            if (classes.Contains(s.Substring(1), StringComparer.Ordinal) && (not == null || !classes.Contains(not, StringComparer.Ordinal)))
                return true;
        }
        return false;
    }
}
