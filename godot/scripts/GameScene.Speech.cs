using Godot;
using Anomaly.Client.Hud;
using Anomaly.Client.Hud.Maptext;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Icons;
using Anomaly.Protocol;

namespace Anomaly.Client;

// M9 slice A (protocol version 38): speech. tg's say window (D250, Hud/SayWindow over Shared/Play/SayModel) opens on tg's keys
// (T say, Y radio, M me, O OOC, P pray) and talks to the server's /datum/tgui_say through Say actions; the runechat lines the server sends
// are drawn over the speakers (Hud/RunechatView, DV160).
public partial class GameScene
{
    private const string SayWindowName = "tgui_say";

    private SayModel _sayModel;
    private SayWindow _sayWindow;
    private RunechatView _runechat;

    /// <summary>The say window is open: its text field holds the keys.</summary>
    private bool SayOpen => _sayWindow?.IsOpen == true;

    private void BuildSpeechUi(CanvasLayer ui, IconLibrary library)
    {
        _runechat = new RunechatView();
        ui.AddChild(_runechat);
        _runechat.Bind(MaptextRenderer.Shared, library);
        ui.MoveChild(_runechat, 0); // under the HUD's windows and panels

        _sayModel = new SayModel(Say); // D319: see GameScene.ChatTranslation
        _sayWindow = new SayWindow();
        ui.AddChild(_sayWindow);
        _sayWindow.Bind(_sayModel, SavedShare(SayWindowName));
        _sayWindow.Now = () => App.NowSeconds;
        _sayWindow.Moved += share =>
        {
            SaveShare(SayWindowName, share);
            SaveOptions();
        };

        _feed.SayModalReceived += OnSayModal;
        _feed.RunechatReceived += OnRunechat;
        _feed.RadioChannelsReceived += OnRadioChannels;
    }

    private void TeardownSpeech()
    {
        if (_feed == null)
            return;
        _feed.SayModalReceived -= OnSayModal;
        _feed.RunechatReceived -= OnRunechat;
        _feed.RadioChannelsReceived -= OnRadioChannels;
    }

    // Protocol 40 (M9 slice E): the worn headset's channels, the chat window's Radio list
    private void OnRadioChannels(IReadOnlyList<string> prefixes) => _chatWindow?.SetRadioPrefixes(prefixes);

    private void OnSayModal(SayModalMessage message)
    {
        _sayModel?.OnServer(message, App.NowSeconds);
        _chatInput?.OnServer(message); // M9 slice C: the chat window's input talks to the same /datum/tgui_say
    }

    // OnRunechat: GameScene.ChatTranslation (D319: the text over the head shows the translation)

    private void UpdateSpeech(double now, float windowScale, float hudScale)
    {
        _sayWindow?.Update(windowScale);
        if (_runechat != null)
        {
            _runechat.HudScale = hudScale;
            _runechat.Update(_rig.Camera, _entities, now);
        }
    }

    /// <summary>tg's communication keybindings: the say window opens on the key's channel.</summary>
    private bool OnSpeechKey(PlayAction? action)
    {
        string channel = ChatInputModel.ChannelOfKey(action);
        if (channel == null || _sayWindow == null)
            return false;
        if (ChatWindowOpen)
        {
            // the chat window stays open while playing; the speech keys type into it, on their channel (ui-overhaul.md 5)
            _chatWindow.FocusInput(channel);
            GetViewport().SetInputAsHandled();
            return true;
        }
        _sayWindow.Open(channel);
        GetViewport().SetInputAsHandled();
        return true;
    }
}
