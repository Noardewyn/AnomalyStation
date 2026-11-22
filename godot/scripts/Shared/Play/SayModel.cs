using System.Text.RegularExpressions;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared;

// Owner: M9 slice A (D250: tg's say window as is). Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).
// A port of tgui/packages/tgui-say (TguiSay.tsx, ChannelIterator.ts, ChatHistory.ts, helpers.ts, timers.ts, constants.ts): the window's
// state and what it sends to the server's /datum/tgui_say (protocol version 38's Say action). The Godot window (Hud/SayWindow.cs) draws
// it and feeds it the keys; tgui's timers run on the clock the view passes in.

/// <summary>tg's <c>ChannelIterator</c>: cycles Say, Radio, Me, OOC, Pray (Admin is blacklisted) and tells the quiet channels.</summary>
public sealed class SayChannelIterator
{
    private static readonly string[] Channels = { SayChannels.Say, SayChannels.Radio, SayChannels.Me, SayChannels.Ooc, SayChannels.Admin, SayChannels.Pray };
    private static readonly string[] Blacklist = { SayChannels.Admin };
    private static readonly string[] Quiet = { SayChannels.Ooc, SayChannels.Admin, SayChannels.Pray };
    private int _index;
    private bool _lobby;

    /// <summary>
    /// M10x slice A (D281): the player is in tg's lobby, where only OOC and Pray reach anyone (a new player's speech has no hearers):
    /// Say, Radio and Me are blacklisted there too, and the window sits on OOC.
    /// </summary>
    public bool Lobby
    {
        get => _lobby;
        set
        {
            _lobby = value;
            if (Blocked(Channels[_index]))
                _index = Array.IndexOf(Channels, SayChannels.Ooc);
        }
    }

    private bool Blocked(string channel) => _lobby && channel is SayChannels.Say or SayChannels.Radio or SayChannels.Me;

    public string Next()
    {
        if (Array.IndexOf(Blacklist, Channels[_index]) >= 0)
            return Channels[_index];
        for (int index = 1; index <= Channels.Length; index++)
        {
            int nextIndex = (_index + index) % Channels.Length;
            if (Array.IndexOf(Blacklist, Channels[nextIndex]) < 0 && !Blocked(Channels[nextIndex]))
            {
                _index = nextIndex;
                break;
            }
        }
        return Channels[_index];
    }

    /// <summary>tg's <c>set</c>: an unknown channel keeps the first (the window is only opened on the channels above).</summary>
    public void Set(string channel)
    {
        _index = Math.Max(0, Array.IndexOf(Channels, channel));
        Lobby = _lobby;
    }

    public string Current => Channels[_index];

    public bool IsSay => Channels[_index] == SayChannels.Say;

    public bool IsVisible => Array.IndexOf(Quiet, Channels[_index]) < 0;

    public void Reset()
    {
        _index = 0;
        Lobby = _lobby;
    }
}

/// <summary>tg's <c>ChatHistory</c>: the last five sent messages, walked with the arrow keys.</summary>
public sealed class SayChatHistory
{
    private readonly List<string> _messages = new();
    private int _index = -1;
    private string _temp;

    public void Add(string message)
    {
        _messages.Insert(0, message);
        _index = -1;
        if (_messages.Count > 5)
            _messages.RemoveAt(_messages.Count - 1);
    }

    public int Index => _index + 1;

    public string GetOlderMessage()
    {
        if (_messages.Count == 0 || _index >= _messages.Count - 1)
            return null;
        _index++;
        return _messages[_index];
    }

    public string GetNewerMessage()
    {
        if (_index <= 0)
        {
            _index = -1;
            return null;
        }
        _index--;
        return _messages[_index];
    }

    public bool IsAtLatest => _index == -1;

    public void SaveTemp(string message) => _temp = message;

    public string GetTemp()
    {
        string temp = _temp;
        _temp = null;
        return temp;
    }

    public void Reset()
    {
        _index = -1;
        _temp = null;
    }
}

/// <summary>One message of the say window to the server (tg's <c>Byond.sendMessage(type, payload)</c>).</summary>
public readonly record struct SayOut(string Type, string Channel = null, string Entry = null, bool Visible = false);

/// <summary>The say window (tg's <c>TguiSay</c>): open on a channel, typed into, sent with Enter, closed with Esc.</summary>
public sealed class SayModel
{
    /// <summary>tg's <c>WindowSize</c>: the width and the three heights, in tgui pixels.</summary>
    public const int Width = 231, SizeSmall = 30, SizeMedium = 50, SizeLarge = 70;

    /// <summary>tg's <c>LineLength</c>: how many characters grow the window to the next height.</summary>
    public const int LineSmall = 20, LineMedium = 39, LineLarge = 59;

    /// <summary>tg's <c>RADIO_PREFIXES</c>: the prefix typed first and the name the channel button shows.</summary>
    public static readonly IReadOnlyDictionary<string, string> RadioPrefixes = new Dictionary<string, string>
    {
        [":a "] = "Hive", [":b "] = "io", [":c "] = "Cmd", [":e "] = "Engi", [":g "] = "Cling", [":m "] = "Med", [":n "] = "Sci",
        [":o "] = "AI", [":p "] = "Ent", [":s "] = "Sec", [":t "] = "Synd", [":u "] = "Supp", [":v "] = "Svc", [":y "] = "CCom",
    };

    private static readonly Regex ChannelRegex = new(@"^[:.][A-Za-z0-9_]\s", RegexOptions.CultureInvariant);

    private readonly Action<SayOut> _send;
    private readonly SayChannelIterator _iterator = new();
    private readonly SayChatHistory _history = new();
    // tg's timers.ts
    private readonly Debounce _channelIncrementMsg = new(0.4, immediate: false);
    private readonly Debounce _forceSayMsg = new(1.0, immediate: true);
    private readonly Throttle _saveText = new(1.0);
    private readonly Throttle _typingMsg = new(4.0);

    public SayModel(Action<SayOut> send) => _send = send ?? throw new ArgumentNullException(nameof(send));

    public bool IsOpen { get; private set; }

    public string Value { get; private set; } = "";

    public string ButtonContent { get; private set; } = "";

    public string CurrentPrefix { get; private set; }

    public int MaxLength { get; private set; } = Limits.MaxSayEntryChars;

    public bool LightMode { get; private set; }

    /// <summary>The window's height (<see cref="SizeSmall"/>, <see cref="SizeMedium"/>, <see cref="SizeLarge"/>) for the value's length.</summary>
    public int Size => Value.Length > LineMedium ? SizeLarge : Value.Length > LineSmall ? SizeMedium : SizeSmall;

    /// <summary>The value is long enough for the textarea to scroll (tg's <c>textarea-large</c>).</summary>
    public bool Scrolls => Value.Length > LineLarge;

    /// <summary>The colour theme: light mode, the radio prefix's channel, or the current channel (<see cref="SayColours"/>).</summary>
    public string Theme => LightMode ? "lightMode" : CurrentPrefix != null ? RadioPrefixes[CurrentPrefix] : _iterator.Current;

    public string Channel => _iterator.Current;

    /// <summary>M10x slice A (D281): the player is in tg's lobby; the window offers OOC and Pray only (<see cref="SayChannelIterator.Lobby"/>).</summary>
    public bool Lobby
    {
        get => _iterator.Lobby;
        set
        {
            if (_iterator.Lobby == value)
                return;
            _iterator.Lobby = value;
            if (CurrentPrefix == null)
                ButtonContent = _iterator.Current;
        }
    }

    /// <summary>Runs tgui's pending timers that are due.</summary>
    public void Advance(double now)
    {
        _channelIncrementMsg.Advance(now);
        _forceSayMsg.Advance(now);
        _saveText.Advance(now);
        _typingMsg.Advance(now);
    }

    /// <summary>tg's key opens the window on <paramref name="channel"/> (handleOpen, windowOpen).</summary>
    public void Open(string channel)
    {
        _iterator.Set(channel);
        CurrentPrefix = null;
        ButtonContent = _iterator.Current;
        IsOpen = true;
        _send(new SayOut(SayTypes.Open, Channel: _iterator.Current));
    }

    /// <summary>tg's handleClose (Esc, Enter, the server's close): hides the window and clears it.</summary>
    public void Close()
    {
        if (!IsOpen)
            return;
        IsOpen = false;
        _send(new SayOut(SayTypes.Close));
        _history.Reset();
        _iterator.Reset();
        CurrentPrefix = null;
        ButtonContent = _iterator.Current;
        Value = "";
    }

    /// <summary>tg's handleEnter: sends a non-empty entry shorter than the cap, then closes.</summary>
    public void Enter()
    {
        string prefix = CurrentPrefix ?? "";
        if (Value.Length > 0 && Value.Length < MaxLength)
        {
            _history.Add(Value);
            _send(new SayOut(SayTypes.Entry, Channel: _iterator.Current, Entry: _iterator.IsSay ? prefix + Value : Value));
        }
        Close();
    }

    /// <summary>tg's handleIncrementChannel (Tab, a click on the channel button).</summary>
    public void IncrementChannel(double now)
    {
        _iterator.Next();
        ButtonContent = _iterator.Current;
        CurrentPrefix = null;
        bool visible = _iterator.IsVisible;
        _channelIncrementMsg.Call(now, () => _send(new SayOut(SayTypes.Thinking, Visible: visible)));
    }

    /// <summary>tg's handleArrowKeys: Up walks back through the history, Down forward.</summary>
    public void Arrow(bool up)
    {
        if (up)
        {
            if (_history.IsAtLatest && Value.Length > 0)
                _history.SaveTemp(Value); // Save current message to temp history if at the most recent message
            string prevMessage = _history.GetOlderMessage();
            if (prevMessage != null)
            {
                ButtonContent = _history.Index.ToString(System.Globalization.CultureInfo.InvariantCulture);
                Value = prevMessage;
            }
        }
        else
        {
            string nextMessage = _history.GetNewerMessage() ?? _history.GetTemp() ?? "";
            ButtonContent = _history.IsAtLatest ? _iterator.Current : _history.Index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            Value = nextMessage;
        }
    }

    /// <summary>tg's handleBackspaceDelete, at the key's press before the text changes.</summary>
    public void BackspaceDelete()
    {
        if (!_history.IsAtLatest)
        {
            _history.Reset(); // User is on a chat history message
            ButtonContent = CurrentPrefix != null ? CurrentPrefix : _iterator.Current;
        }
        else if (CurrentPrefix != null && _iterator.IsSay && Value.Length == 0)
        {
            CurrentPrefix = null; // Empty input, resets the channel
            ButtonContent = _iterator.Current;
        }
    }

    /// <summary>
    /// tg's handleInput: the text after a change. A radio prefix typed first is taken off the text and shown on the button; the
    /// typing indicator and the saved text go to the server on their throttles. The view shows <see cref="Value"/> afterwards.
    /// </summary>
    public void Input(string newValue, double now)
    {
        newValue ??= "";
        if (newValue.Length > MaxLength)
            newValue = newValue.Substring(0, MaxLength); // the textarea's maxLength
        string newPrefix = GetPrefix(newValue) ?? CurrentPrefix;
        // Handles switching prefixes
        if (newPrefix != null && newPrefix != CurrentPrefix)
        {
            ButtonContent = RadioPrefixes[newPrefix];
            CurrentPrefix = newPrefix;
            newValue = newValue.Substring(3);
            _iterator.Set(SayChannels.Say);
            if (newPrefix == ":b ")
                _send(new SayOut(SayTypes.Thinking, Visible: false));
        }

        // Handles typing indicators
        if (_iterator.IsVisible && newPrefix != ":b ")
        {
            _typingMsg.Call(now, () => _send(new SayOut(SayTypes.Typing)));
            string channel = _iterator.Current, entry = newValue;
            _saveText.Call(now, () => _send(new SayOut(SayTypes.Save, Channel: channel, Entry: entry)));
        }

        Value = newValue;
    }

    /// <summary>A message from the server's datum (tg's Byond.subscribeTo: props, force, save, close).</summary>
    public void OnServer(in SayModalMessage message, double now)
    {
        switch (message.Command)
        {
            case SayModalCommand.Props:
                MaxLength = message.MaxLength;
                LightMode = message.LightMode;
                break;
            case SayModalCommand.Force:
                // handleForceSay: only on a visible channel with something typed
                if (!IsOpen || Value.Length == 0 || !_iterator.IsVisible)
                    return;
                string grunt = _iterator.IsSay ? (CurrentPrefix ?? "") + Value : Value;
                string channel = _iterator.Current;
                _forceSayMsg.Call(now, () => _send(new SayOut(SayTypes.Force, Channel: channel, Entry: grunt)));
                Close();
                break;
            case SayModalCommand.Save:
                // handleSaveText
                if (!IsOpen || Value.Length == 0 || !_iterator.IsVisible)
                    return;
                string saveChannel = _iterator.Current, value = Value;
                _saveText.Call(now, () => _send(new SayOut(SayTypes.Save, Channel: saveChannel, Entry: value)));
                break;
            case SayModalCommand.Close:
                Close();
                break;
        }
    }

    /// <summary>tg's getPrefix: the radio prefix a text starts with (":e ", or ".e " read as ":e "), or null.</summary>
    public static string GetPrefix(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length < 3 || !ChannelRegex.IsMatch(value))
            return null;
        string adjusted = value.Substring(0, 3).ToLowerInvariant();
        int dot = adjusted.IndexOf('.');
        if (dot >= 0)
            adjusted = adjusted.Substring(0, dot) + ":" + adjusted.Substring(dot + 1);
        return RadioPrefixes.ContainsKey(adjusted) ? adjusted : null;
    }

    // tgui's debounce: a call waits for `time` of quiet (immediate: runs at once unless one ran within `time`).
    private sealed class Debounce
    {
        private readonly double _time;
        private readonly bool _immediate;
        private double _due = double.NaN;
        private Action _pending;

        public Debounce(double time, bool immediate)
        {
            _time = time;
            _immediate = immediate;
        }

        public void Call(double now, Action fn)
        {
            bool callNow = _immediate && double.IsNaN(_due);
            _due = now + _time;
            _pending = _immediate ? null : fn;
            if (callNow)
                fn();
        }

        public void Advance(double now)
        {
            if (double.IsNaN(_due) || now < _due)
                return;
            _due = double.NaN;
            Action fn = _pending;
            _pending = null;
            fn?.Invoke();
        }
    }

    // tgui's throttle: runs at once at most every `time`; a call within it runs (with its own arguments) when the time is up.
    internal sealed class Throttle
    {
        private readonly double _time;
        private double _last = double.NaN;
        private double _due = double.NaN;
        private Action _queued;

        public Throttle(double time) => _time = time;

        public void Call(double now, Action fn)
        {
            _queued = null;
            _due = double.NaN;
            if (double.IsNaN(_last) || now - _last >= _time)
            {
                fn();
                _last = now;
            }
            else
            {
                _queued = fn;
                _due = _last + _time;
            }
        }

        public void Advance(double now)
        {
            if (_queued == null || now < _due)
                return;
            Action fn = _queued;
            _queued = null;
            _due = double.NaN;
            fn();
            _last = now;
        }
    }
}

/// <summary>tg's say window colours (tgui-say/styles/colors.scss), as HSL: the theme's colour, its button and its window background.</summary>
public static class SayColours
{
    private static readonly Dictionary<string, (float H, float S, float L)> Map = new()
    {
        ["Admin"] = (300f, 100f, 86.7f), ["AI"] = (332.2f, 59.6f, 60.2f), ["CCom"] = (197f, 62.6f, 39.8f), ["Cling"] = (86.7f, 56.6f, 28f),
        ["Cmd"] = (53f, 97.6f, 50f), ["Engi"] = (17f, 87.8f, 61.4f), ["Hive"] = (300f, 17.7f, 44.3f), ["io"] = (209.6f, 100f, 55.9f),
        ["Me"] = (227f, 63.5f, 60.2f), ["Med"] = (202f, 83.6f, 64.1f), ["OOC"] = (47.9f, 100f, 40f), ["Ent"] = (176.1f, 20f, 45.1f),
        ["Pray"] = (214f, 86f, 30f), ["Radio"] = (132.8f, 74.4f, 45.9f), ["Say"] = (213.6f, 37.9f, 74.1f), ["Sci"] = (271.6f, 91.7f, 76.5f),
        ["Sec"] = (0f, 71.2f, 53.7f), ["Supp"] = (33.7f, 44.9f, 49.8f), ["Svc"] = (88.1f, 60.6f, 40.8f), ["Synd"] = (359.1f, 31.8f, 42.5f),
    };

    /// <summary>The theme's colour as RGB 0..1, lightened or darkened by <paramref name="lightness"/> percentage points (sass color.adjust).</summary>
    public static (float R, float G, float B) Of(string theme, float lightness = 0f)
    {
        (float h, float s, float l) = Map.TryGetValue(theme ?? "", out var hsl) ? hsl : Map["Say"];
        return HslToRgb(h, s / 100f, Math.Clamp((l + lightness) / 100f, 0f, 1f));
    }

    private static (float R, float G, float B) HslToRgb(float h, float s, float l)
    {
        float c = (1 - Math.Abs(2 * l - 1)) * s;
        float hp = (h % 360f) / 60f;
        float x = c * (1 - Math.Abs(hp % 2 - 1));
        (float r, float g, float b) = hp switch
        {
            < 1 => (c, x, 0f),
            < 2 => (x, c, 0f),
            < 3 => (0f, c, x),
            < 4 => (0f, x, c),
            < 5 => (x, 0f, c),
            _ => (c, 0f, x),
        };
        float m = l - c / 2;
        return (r + m, g + m, b + m);
    }
}
