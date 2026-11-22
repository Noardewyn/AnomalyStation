using Godot;
using Anomaly.Client.Audio;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Audio;

namespace Anomaly.Client;

/// <summary>
/// D306: the host's lobby between two rounds. The round ended (tg's world/Reboot) and the server builds the next world while the
/// connection stays: the lobby's title picture, the server's status line in the middle and the ended round's title track (the
/// owner, 2026-11-02: no chat and no log). When the next world's join sequence is in (<see cref="FeedPhase.Playing"/>) the game scene
/// takes the same connection on, into the new round's lobby.
/// </summary>
public partial class BetweenRoundsScreen : Control
{
    private const string TitlePicture = "res://assets/ui/lobby_title.webp";

    private GameClient _feed;
    private Label _status;
    private AudioStreamPlayer _music;
    private string _musicPath;
    private bool _handedOn;

    public override void _Ready()
    {
        _feed = App.Instance.Feed as GameClient;
        if (_feed == null)
        {
            App.Instance.GoToConnect("There is no connection.");
            return;
        }
        Name = "BetweenRounds";
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var back = new ColorRect { Color = Colors.Black };
        back.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(back);
        var title = new TextureRect
        {
            Texture = ResourceLoader.Exists(TitlePicture) ? GD.Load<Texture2D>(TitlePicture) : null,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            TextureFilter = TextureFilterEnum.Linear, // a painted picture, as the lobby draws it
        };
        title.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(title);

        _status = new Label
        {
            Text = _feed.BetweenRoundsStatus,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _status.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _status.AddThemeFontSizeOverride("font_size", 32);
        _status.AddThemeConstantOverride("outline_size", 8);
        _status.AddThemeColorOverride("font_outline_color", Colors.Black);
        AddChild(_status);

        _music = new AudioStreamPlayer();
        AddChild(_music);
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    public override void _Process(double delta)
    {
        if (_feed == null || _handedOn)
            return;
        _feed.Poll(App.NowSeconds);
        PlayMusic();
        switch (_feed.Phase)
        {
            case FeedPhase.Playing:
                GD.Print("[anomaly] the next round is ready: joining its lobby");
                _handedOn = true;
                App.Instance.GoToGame();
                break;
            case FeedPhase.Closed:
                GD.PrintErr("[anomaly] the connection ended between rounds: " + _feed.CloseReason);
                App.Instance.GoToConnect(_feed.CloseReason);
                break;
        }
    }

    public override void _ExitTree()
    {
        if (_feed != null && !_handedOn)
        {
            // leaving the screen otherwise (the program, a closed connection) leaves the server, as the game scene does
            _feed.Disconnect();
            _feed.Dispose();
        }
    }

    // the ended round's title track (the server names it), at the Settings' music volume
    private void PlayMusic()
    {
        string path = _feed.LobbyMusic;
        if (path == null || path == _musicPath)
            return;
        _musicPath = path;
        AudioStream stream = new SoundLibrary().Get(path, repeat: true);
        if (stream == null)
            return;
        PlayOptions options = GameScene.LoadOptions(App.Instance.Args);
        _music.VolumeDb = SoundMixer.LinearToDb(options.MasterVolumePercent / 100f * (options.MusicVolumePercent / 100f));
        _music.Stream = stream;
        _music.Play();
    }
}
