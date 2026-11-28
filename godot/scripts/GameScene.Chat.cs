using System.Linq;
using Godot;
using Anomaly.Client.Hud;
using Anomaly.Client.Shared;
using Anomaly.Protocol;

namespace Anomaly.Client;

// M9 slice C (D247, D252, D259): the big chat window (Hud/ChatWindow over Shared/Play/ChatLog and ChatInputModel). Shift+T opens and
// closes it; it and the say window close each other; the fading feed is hidden while it is open; the pointer is free while it is open,
// and the game keeps its keys unless the window's input has them. Its place and size are kept in the options, the lobby's apart from
// the round's; it opens on entering either (the owner, 2026-10-28).
public partial class GameScene
{
    private const string ChatRoundName = "chat_round", ChatLobbyName = "chat_lobby";

    // in the round at the bottom left, clear of the bottom row (ChatPlace.RoundDefault); in the lobby the left half up to the lobby's menu at the top middle
    private const float LobbyMenuHalfWidth = 80; // the lobby menu's left edge from the middle, px at the lobby's scale (its shutter: -73)

    private string _chatPlace; // whose place the chat window has: ChatRoundName, ChatLobbyName or null (none yet)

    private readonly ChatLog _chatLog = new();
    private ChatInputModel _chatInput;
    private ChatWindow _chatWindow;

    private bool ChatWindowOpen => _chatWindow?.IsOpen == true;

    /// <summary>The chat window's input has the keyboard.</summary>
    private bool ChatTyping => _chatWindow?.Typing == true;

    /// <summary>A window that frees the pointer is open (D282, MouseOwner): a tgui window, an open storage or the chat window typed in.</summary>
    private bool WindowsOpen => TguiOpen || _storageWindow?.IsOpen == true || ChatTyping || ActionsMenuOpen; // the owner (2026-10-26): an open chat window not typed in leaves the game playable

    private void BuildChatWindow(CanvasLayer ui)
    {
        _chatInput = new ChatInputModel(Say); // D319: through Say, which keeps the line so its echo is not translated
        _chatWindow = new ChatWindow();
        ui.AddChild(_chatWindow);
        _chatWindow.Bind(_chatLog, _chatInput);
        _chatWindow.Now = () => App.NowSeconds;
        _chatWindow.GeometryChanged += (share, windowSize) =>
        {
            if (_chatPlace == null)
                return;
            SaveShare(_chatPlace, share);
            _options.WindowSizes[_chatPlace] = ((int)MathF.Round(windowSize.X), (int)MathF.Round(windowSize.Y));
            SaveOptions();
        };
        _chatWindow.EmotesRequested += () => _session?.Keybinding(KeybindingCode.VerbEmotePanel); // tg's emote panel (slice B)
        _chatWindow.MessengerRequested += () => _session?.Keybinding(KeybindingCode.OpenMessenger); // the PDA's messenger (slice E, D251)
        _chatWindow.LinkClicked += href => _session?.Topic(href);
        _chatWindow.CloseRequested += () => _chatWindow.Close();
        _chatWindow.MicHeld += MicButton; // D309
    }

    // M10x slice A (D281): in tg's lobby the chat is on screen as in the round (tg's chat panel): the chat lines, the say window and the
    // chat window are drawn over the lobby screen while it is open, and offer only the channels that reach anyone from it (OOC, Pray).
    private readonly Dictionary<Control, int> _chatOverLobby = new();

    // the map editor's world stands in the lobby (docs/ai/specs/map-editor.md 3): its connection's lobby is not the editor's to show
    private bool InLobby => !EditorOn && _feed?.Replica?.UiWindows.TryGetValue(Limits.LobbyUiWindow, out UiWindowState lobby) == true && lobby.Interface == Limits.LobbyUiInterface;

    private void SyncLobbyChat()
    {
        bool lobby = InLobby;
        PlaceChat(lobby ? ChatLobbyName : _feed?.OwnEntity > 0 ? ChatRoundName : _chatPlace);
        if (_sayModel != null)
            _sayModel.Lobby = lobby;
        if (_chatInput != null)
            _chatInput.Lobby = lobby;
        if (_tguiHost == null || lobby == _chatOverLobby.Count > 0)
            return;
        // moving the chat window between the lobby screen and the game takes the keyboard from its input: a line being typed keeps it, and its
        // rows stay shown (ui-overhaul.md section 13)
        bool typing = ChatTyping;
        if (lobby)
        {
            foreach (Control node in new Control[] { _chatLines, _sayWindow, _chatWindow })
            {
                if (node != null)
                    _chatOverLobby[node] = node.GetIndex(); // all before any moves: Release puts them back in this order
            }
            foreach (Control node in _chatOverLobby.Keys)
                _tguiHost.Adopt(node);
        }
        else
        {
            Node ui = GetNode("Ui");
            foreach ((Control node, int index) in _chatOverLobby.OrderBy(p => p.Value))
                _tguiHost.Release(node, ui, index);
            _chatOverLobby.Clear();
        }
        if (typing && !ChatTyping)
            _chatWindow.FocusInput();
    }

    // Entering the lobby or the round: the chat window opens at that one's place and size.
    private void PlaceChat(string place)
    {
        if (place == _chatPlace || place == null || _chatWindow == null)
            return;
        _chatPlace = place;
        _chatWindow.Round = place == ChatRoundName; // ui-overhaul.md section 13: see-through, its rows shown only while typing
        if (place == ChatLobbyName)
        {
            Vector2 screen = GetViewport().GetVisibleRect().Size;
            float lobbyScale = Math.Min(screen.X / 608f, screen.Y / 480f); // LobbyScreen's scale (tgui-lobby's scaling.ts)
            float right = Math.Max(0.2f, 0.5f - LobbyMenuHalfWidth * lobbyScale / screen.X - 0.01f);
            _chatWindow.DefaultShare = new Rect2(0.005f, 0.01f, right - 0.005f, 0.98f);
            _chatWindow.DefaultPlace = null;
        }
        else
            _chatWindow.DefaultPlace = (screen, scale) =>
            {
                HudRect r = ChatPlace.RoundDefault(new HudMetrics(screen.X, screen.Y, _options.UiScale), scale);
                return new Rect2(r.Left, r.Top, r.Width, r.Height);
            };
        Vector2? size = _options.WindowSizes.TryGetValue(place, out (int Width, int Height) s) ? new Vector2(s.Width, s.Height) : null;
        _chatWindow.SetGeometry(SavedShare(place), size);
        _chatWindow.Open();
    }

    /// <summary>A window's saved top left (the options' <c>window.NAME</c>) as its share of the screen, or null.</summary>
    private Vector2? SavedShare(string name)
        => _options.WindowPositions.TryGetValue(name, out (int X, int Y) p) ? new Vector2(p.X, p.Y) / SavedScreen : null;

    private void SaveShare(string name, Vector2 share)
    {
        Vector2 at = share * SavedScreen;
        _options.WindowPositions[name] = ((int)MathF.Round(at.X), (int)MathF.Round(at.Y));
    }

    private Vector2 SavedScreen
    {
        get
        {
            Vector2 view = GetViewport().GetVisibleRect().Size;
            (int w, int h) = _options.WindowScreenFor(view.X, view.Y);
            return new Vector2(w, h);
        }
    }

    /// <summary>Shift+T: the chat window opens (closing the say window) or closes.</summary>
    private bool OnChatWindowKey(PlayAction? action)
    {
        if (action != PlayAction.ChatWindow || _chatWindow == null)
            return false;
        if (ChatWindowOpen)
            _chatWindow.Close();
        else
        {
            _sayModel?.Close();
            _chatWindow.Open();
            _chatWindow.FocusInput();
        }
        GetViewport().SetInputAsHandled();
        return true;
    }

    /// <summary>Esc with the window open and its input not focused: the window closes.</summary>
    private bool CloseChatWindow()
    {
        if (!ChatWindowOpen)
            return false;
        _chatWindow.Close();
        return true;
    }
}
