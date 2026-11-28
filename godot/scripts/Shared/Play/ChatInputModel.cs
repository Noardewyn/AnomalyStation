using Anomaly.Protocol;

namespace Anomaly.Client.Shared;

// Owner: M9 slice C (D259, chat.md 3). Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).
// The big chat window's input row: tg's say window's keys and messages (SayModel: the radio prefixes, the history, the typing indicator,
// the force-say) on the window's channels, which add tg's whisper and the shout (D253) to the say window's: a whispered line goes out as
// tg's "#" mode key, a shouted one with tg's "!!" yell ending. The text stays while the input is not focused (the player walks and reads).

public sealed class ChatInputModel
{
    public const string Whisper = "Whisper";
    public const string Shout = "Shout";

    /// <summary>tg's say mode key of a whisper (<c>#</c>, say.dm's get_message_mods).</summary>
    public const string WhisperKey = "#";

    /// <summary>The channels Tab cycles, in the action row's order (Admin is not one: no admins yet).</summary>
    public static readonly IReadOnlyList<string> Channels = new[] { SayChannels.Say, Whisper, Shout, SayChannels.Radio, SayChannels.Me, SayChannels.Ooc, SayChannels.Pray };

    /// <summary>
    /// tg's communication keybindings: the channel a speech key opens the say window on, or selects in this input when the chat window is open
    /// (ui-overhaul.md section 5: the radio, OOC and me keys as well as say); null for any other action.
    /// </summary>
    public static string ChannelOfKey(PlayAction? action) => action switch
    {
        PlayAction.Say => SayChannels.Say,
        PlayAction.Radio => SayChannels.Radio,
        PlayAction.Me => SayChannels.Me,
        PlayAction.Ooc => SayChannels.Ooc,
        PlayAction.Pray => SayChannels.Pray,
        PlayAction.Whisper => Whisper, // slice 4E: SS14's FocusWhisperChatWindow
        _ => null,
    };

    // tg's ChannelIterator quiet channels: no typing indicator
    private static bool IsQuiet(string channel) => channel is SayChannels.Ooc or SayChannels.Pray;

    private readonly Action<SayOut> _send;
    private readonly SayChatHistory _history = new();
    private readonly SayModel.Throttle _typingMsg = new(4.0);
    private int _index;

    public ChatInputModel(Action<SayOut> send) => _send = send ?? throw new ArgumentNullException(nameof(send));

    public string Channel => Channels[_index];

    /// <summary>
    /// M10x slice A (D281): the player is in tg's lobby, where only OOC and Pray reach anyone (a new player's speech has no hearers): the
    /// other channels are skipped and the input moves to OOC.
    /// </summary>
    public bool Lobby
    {
        get => _lobby;
        set
        {
            _lobby = value;
            if (!Allowed(Channel))
            {
                _index = IndexOf(SayChannels.Ooc);
                Prefix = null;
            }
        }
    }

    private bool _lobby;

    /// <summary>Whether <paramref name="channel"/> (an action button's) can be picked now.</summary>
    public bool Allowed(string channel) => !_lobby || channel is SayChannels.Ooc or SayChannels.Pray;

    /// <summary>The text typed (without a radio prefix, which <see cref="Prefix"/> holds).</summary>
    public string Value { get; private set; } = "";

    /// <summary>The radio prefix typed first (":e "), or null.</summary>
    public string Prefix { get; private set; }

    /// <summary>The channel button's text: the history's place while it is walked, the prefix's channel, or the channel.</summary>
    public string ButtonContent => !_history.IsAtLatest ? _history.Index.ToString(System.Globalization.CultureInfo.InvariantCulture)
        : Prefix != null ? SayModel.RadioPrefixes[Prefix] : Channel;

    /// <summary>The channel's colour theme (<see cref="SayColours"/>): whisper and shout are said, so Say's.</summary>
    public string Theme => Prefix != null ? SayModel.RadioPrefixes[Prefix] : Channel is Whisper or Shout ? SayChannels.Say : Channel;

    public int MaxLength { get; private set; } = Limits.MaxSayEntryChars;

    /// <summary>The input has the keyboard: the server's say window counts as open (tg's thinking indicator on a loud channel).</summary>
    public bool Focused { get; private set; }

    // The say window's channel a window channel goes out on.
    private string Outgoing => Channel is Whisper or Shout ? SayChannels.Say : Channel;

    /// <summary>The input took the keyboard (tg's window open).</summary>
    public void Focus()
    {
        if (Focused)
            return;
        Focused = true;
    }

    // The owner (2026-10-26): the chat window shows no indicator over the speaker until something is typed in it (tg opens its say
    // window with the thinking indicator); the server's window is opened by the first keystroke.
    private bool _serverOpen;

    private void OpenOnServer()
    {
        if (_serverOpen)
            return;
        _serverOpen = true;
        _send(new SayOut(SayTypes.Open, Channel: Outgoing));
    }

    /// <summary>The input let the keyboard go (tg's window close); the text stays.</summary>
    public void Blur()
    {
        if (!Focused)
            return;
        Focused = false;
        if (_serverOpen)
            _send(new SayOut(SayTypes.Close));
        _serverOpen = false;
        _history.Reset();
    }

    /// <summary>An action button: the channel is chosen (and the prefix dropped).</summary>
    public void SetChannel(string channel)
    {
        int index = IndexOf(channel);
        if (index < 0 || !Allowed(channel))
            return;
        _index = index;
        Prefix = null;
        Reopen();
    }

    /// <summary>
    /// M9 slice E: a department picked from the Radio list (chat.md 3): the radio prefix (":e ") goes to the button on the say channel, as
    /// tg's handleInput moves a typed prefix there.
    /// </summary>
    public void SetRadioPrefix(string prefix)
    {
        if (!SayModel.RadioPrefixes.ContainsKey(prefix) || !Allowed(SayChannels.Say))
            return;
        _index = IndexOf(SayChannels.Say);
        Prefix = prefix;
        Reopen();
    }

    /// <summary>Tab or a click on the channel button: the next channel.</summary>
    public void IncrementChannel()
    {
        do
            _index = (_index + 1) % Channels.Count;
        while (!Allowed(Channel));
        Prefix = null;
        Reopen();
    }

    private static int IndexOf(string channel)
    {
        for (int i = 0; i < Channels.Count; i++)
        {
            if (Channels[i] == channel)
                return i;
        }
        return -1;
    }

    // tg's thinking indicator follows the channel: the server's window is reopened on it.
    private void Reopen()
    {
        if (!Focused || !_serverOpen)
            return;
        _send(new SayOut(SayTypes.Close));
        _send(new SayOut(SayTypes.Open, Channel: Outgoing));
    }

    /// <summary>tg's handleInput: a radio prefix typed first moves to the button (on a said channel), and the typing indicator goes out on its throttle.</summary>
    public void Input(string newValue, double now)
    {
        newValue ??= "";
        if (newValue.Length > MaxLength)
            newValue = newValue.Substring(0, MaxLength);
        string newPrefix = SayModel.GetPrefix(newValue);
        if (newPrefix != null && newPrefix != Prefix && Channel is not (SayChannels.Me or SayChannels.Ooc or SayChannels.Pray))
        {
            Prefix = newPrefix;
            newValue = newValue.Substring(3);
            if (Channel == SayChannels.Radio)
                _index = 0; // tg: a prefix sets the say channel
        }
        if (Focused && newValue.Length > 0)
            OpenOnServer();
        if (Focused && !IsQuiet(Channel) && Prefix != ":b ")
            _typingMsg.Call(now, () => _send(new SayOut(SayTypes.Typing)));
        Value = newValue;
    }

    /// <summary>tg's handleBackspaceDelete: leaves the history, or drops the prefix of an empty text.</summary>
    public void BackspaceDelete()
    {
        if (!_history.IsAtLatest)
            _history.Reset();
        else if (Prefix != null && Value.Length == 0)
            Prefix = null;
    }

    /// <summary>tg's handleArrowKeys: Up walks back through the sent lines, Down forward.</summary>
    public void Arrow(bool up)
    {
        if (up)
        {
            if (_history.IsAtLatest && Value.Length > 0)
                _history.SaveTemp(Value);
            string older = _history.GetOlderMessage();
            if (older != null)
                Value = older;
        }
        else
            Value = _history.GetNewerMessage() ?? _history.GetTemp() ?? "";
    }

    /// <summary>tg's handleEnter: a non-empty line under the cap goes out, shaped for its channel; the input is emptied and keeps the keyboard.</summary>
    public bool Enter()
    {
        if (Value.Length == 0 || Value.Length >= MaxLength)
            return false;
        _history.Add(Value);
        _send(new SayOut(SayTypes.Entry, Channel: Outgoing, Entry: Shape(Channel, Prefix, Value)));
        Value = "";
        Prefix = null;
        _history.Reset();
        if (_serverOpen)
            _send(new SayOut(SayTypes.Close)); // sent: no indicator until the next line is typed
        _serverOpen = false;
        return true;
    }

    /// <summary>The entry a line sends: said ones carry the radio prefix; a whisper tg's "#" mode key, a shout tg's "!!" yell ending.</summary>
    public static string Shape(string channel, string prefix, string value)
    {
        string said = (prefix ?? "") + value;
        return channel switch
        {
            Whisper => WhisperKey + said,
            Shout => said.EndsWith("!!", StringComparison.Ordinal) ? said : said.TrimEnd('!', '.', ' ') + "!!",
            SayChannels.Say => said,
            _ => value,
        };
    }

    /// <summary>tg's timers that are due.</summary>
    public void Advance(double now) => _typingMsg.Advance(now);

    /// <summary>The server's say window messages: its props, and tg's force-say of a focused, loud, non-empty line.</summary>
    public void OnServer(in SayModalMessage message)
    {
        switch (message.Command)
        {
            case SayModalCommand.Props:
                MaxLength = message.MaxLength;
                break;
            case SayModalCommand.Force:
                if (!Focused || Value.Length == 0 || IsQuiet(Channel))
                    return;
                _send(new SayOut(SayTypes.Force, Channel: Outgoing, Entry: Shape(Channel, Prefix, Value)));
                Value = "";
                Prefix = null;
                break;
        }
    }
}
