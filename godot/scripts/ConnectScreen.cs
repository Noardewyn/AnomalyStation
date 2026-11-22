using System.Globalization;
using System.Linq;
using Godot;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.World;
using Anomaly.Net;

namespace Anomaly.Client;

/// <summary>
/// The connect screen (scenes/connect.tscn): host, port and name, a Connect button and a status label that says what is
/// happening (connecting, joining) and why a connection ended (rejected with the server's reason, failed, timed out,
/// disconnected). Nothing here blocks the main thread: the transport's connect
/// returns at once (it resolves a host name on a pool thread itself), and <see cref="GameClient.Poll"/> runs in
/// <c>_Process</c> (scope risk 6). With <c>--connect</c> or
/// <c>--fake-server</c> the screen connects at once, without waiting for a click, and a scripted run that fails exits with
/// status 1 after printing the reason. While a connection is made, and while the map editor's world starts, the fields give way to
/// the loading screen (<see cref="ShowLoading"/>): the program's name, and a bar along the bottom under what is being done.
/// </summary>
public partial class ConnectScreen : Control
{
    private LineEdit _host;
    private LineEdit _port;
    private LineEdit _name;
    private Button _button;
    private Label _formStatus; // the status line under the fields
    private Label _status => _loading is { Visible: true } ? _loadingStatus : _formStatus; // ... or the loading screen's, while that is shown

    private JoinAttempt _join;
    private GameClient _client => _join?.Client;
    private double _connectStartedAt;
    private bool _autoStartPending;

    public override void _Ready()
    {
        _host = GetNode<LineEdit>("%Host");
        _port = GetNode<LineEdit>("%Port");
        _name = GetNode<LineEdit>("%Name");
        _button = GetNode<Button>("%ConnectButton");
        _formStatus = GetNode<Label>("%Status");
        _button.Pressed += OnButton;
        _host.TextSubmitted += _ => OnButton();
        _port.TextSubmitted += _ => OnButton();
        _name.TextSubmitted += _ => OnButton();

        ClientArgs args = App.Instance.Args;
#if ANOMALY_EDITOR
        bool editorStarts = App.Instance.EditorStarting != null;
        if (args.Editor || editorStarts)
            ShowLoading(ClientArgs.EditorName, EditorPicture); // (the editor makes no connection: its window never shows the fields)
#else
        const bool editorStarts = false;
#endif
        if (App.Instance.LastTarget is { } last)
        {
            // After a lost connection (or a failed one): the same address and name, ready for another click (M3 I1c).
            _host.Text = last.Host;
            _port.Text = last.Port;
            _name.Text = last.Name;
        }
        else
        {
            if (args.HasConnect)
            {
                _host.Text = args.Host;
                _port.Text = args.Port.ToString(CultureInfo.InvariantCulture);
            }
            _name.Text = args.ServerFromFile && !args.NameGiven ? "" : args.Name; // server.txt only: the player types the name
        }
        Input.MouseMode = Input.MouseModeEnum.Visible;

        if (!string.IsNullOrEmpty(App.Instance.Notice))
        {
            GD.Print($"[anomaly] connect screen: {App.Instance.Notice} (host {_host.Text}, port {_port.Text}, name {_name.Text})");
            ShowStatus(App.Instance.Notice, true);
            if (_loadingBar != null)
                _loadingBar.Visible = false; // (the editor's world ended: nothing is loading)
        }
        App.Instance.Notice = null;

        // A scripted start leaves the screen on the first frame (the scene tree is still being set up in _Ready).
        _autoStartPending = (args.SkipConnectScreen && string.IsNullOrEmpty(_status.Text)) || editorStarts;
    }

    public override void _Process(double delta)
    {
        double now = App.NowSeconds;
        if (_autoStartPending)
        {
            _autoStartPending = false;
            ClientArgs args = App.Instance.Args;
#if ANOMALY_EDITOR
            if (App.Instance.EditorStarting is { } next)
            {
                // the editor opened another map: its session is made, its world is waited for here
                App.Instance.EditorStarting = null;
                WaitForEditor(next, now);
                return;
            }
            if (args.Editor)
            {
                StartEditor(args, now);
                return;
            }
#endif
            if (args.FakeServer)
            {
                int bots = args.FakeBots ?? 8;
                // --sim-latency adds to the fake world's own 50 ms round trip, --sim-loss loses Input messages on the way.
                FakeMap map = null;
                if (args.FakeMapPath != null)
                {
                    string path = args.FakeMapPath.Length > 0 ? args.FakeMapPath : DefaultMapPath();
                    try
                    {
                        map = FakeMap.Load(path);
                        GD.Print($"[anomaly] fake map: {path} {map.Width}x{map.Height}x{map.Levels}, {map.Entities.Count} entities, {map.SpawnCells.Count} spawn cells, {map.PostInitChanges.Count} types ({map.PostInitChanges.Values.Sum(c => c.Count)} things) draw their post-init icon state{(map.Warnings.Count > 0 ? ", " + map.Warnings.Count + " lines repaired" : string.Empty)}");
                    }
                    catch (Exception e) when (e is System.IO.IOException or FakeMapFormatException)
                    {
                        GD.PrintErr($"[anomaly] fake map: {path}: {e.Message}");
                        App.Instance.Quit(2);
                        return;
                    }
                }
                var fake = new FakeFeed(args.Name, args.Seed ?? StableSeed(args.Name), FakeFeed.SizeFor(bots), bots,
                    oneWayLatencyMs: 25 + (int)Math.Round(args.SimLatencyMs / 2), inputLoss: args.SimLoss, map: map, doorSounds: true);
                fake.Poll(now); // the world starts with its first poll
                App.Instance.Feed = fake;
                App.Instance.GoToGame();
                return;
            }
            Begin(args.Host, args.Port, args.Name);
        }

#if ANOMALY_EDITOR
        if (_editorStarting != null)
        {
            PollEditorStart(now);
            return;
        }
#endif
        if (_join == null)
            return;
        switch (_join.Poll(now))
        {
            // (a connection says nothing of how far it is: the bar creeps by the clock through each part and never reaches the part's end)
            case FeedPhase.Connecting:
                ShowStatus(string.Create(CultureInfo.InvariantCulture, $"Connecting to {_client.Description}... {now - _connectStartedAt:0} s"), false);
                _loadingBar.Value = Creep(0f, 0.4f, now - _connectStartedAt);
                break;
            case FeedPhase.Joining:
                _joiningAt ??= now;
                ShowStatus("Connected. Joining the game...", false);
                _loadingBar.Value = Creep(0.4f, 0.9f, now - _joiningAt.Value);
                break;
            case FeedPhase.Playing:
                if (_loadingBar.Value < 1)
                {
                    // (one frame with the bar full, before the making of the game's scene holds the picture)
                    _loadingBar.Value = 1;
                    ShowStatus("Building the scene...", false);
                    break;
                }
                if (_join.Retried)
                    GD.Print($"[anomaly] the kept reconnect token was refused; joined as {_join.Name} without it");
                App.Instance.Feed = _client;
                _join = null;
                App.Instance.GoToGame();
                break;
            case FeedPhase.Closed:
                Failed(_join.Advice);
                break;
        }
    }

    public override void _ExitTree()
    {
        // The client that is handed to the game scene is no longer ours (App.Feed); one that is not is closed.
        if (_client != null && !ReferenceEquals(App.Instance.Feed, _client))
        {
            _client.Disconnect();
            _client.Dispose();
        }
        _join = null;
    }

    private void OnButton()
    {
        if (_client != null)
        {
            Cancel();
            return;
        }
        string name = _name.Text.Trim();
        if (!ClientArgs.IsValidName(name, out string problem))
        {
            ShowStatus(problem.Substring(0, 1).ToUpperInvariant() + problem.Substring(1) + ".", true);
            return;
        }
        if (!ClientArgs.TryParsePort(_port.Text.Trim(), out int port))
        {
            ShowStatus("The port must be a number from 1 to 65535.", true);
            return;
        }
        string host = _host.Text.Trim();
        if (host.Length == 0)
        {
            ShowStatus("Enter the server's host name or address.", true);
            return;
        }
        Begin(host, port, name);
    }

    private void Begin(string host, int port, string name)
    {
        App.Instance.LastTarget = (host, port.ToString(CultureInfo.InvariantCulture), name);
        _connectStartedAt = App.NowSeconds;
        _joiningAt = null;
        _button.Text = "Cancel";
        SetFieldsEnabled(false);
        ShowLoading("Anomaly Station", GamePicture);
        _loadingBar.Value = 0;
        ShowStatus($"Connecting to {host}:{port}...", false);
        // Normal mode: the transport's own threads move bytes; its events come only in Poll(), in _Process (D31). A host
        // name is resolved by the transport on a pool thread, so a lookup cannot freeze the main thread either.
        ClientArgs args = App.Instance.Args;
        IClientTransport Transport()
        {
            IClientTransport transport = GameClient.CreateTransport();
            if (args.SimulatesNetwork)
                transport = new SimulatedNetworkTransport(transport, () => App.NowSeconds, args.SimLatencyMs, args.SimLoss, args.Seed ?? StableSeed(name));
            return transport;
        }
        // M3 R4: the token kept for this server and name takes the character back after a crash or a quit (JoinAttempt).
        // D335: the launcher starts the client with a game token for the server it names in --connect; no other server is shown it.
        string login = args.HasConnect && host == args.Host && port == args.Port ? System.Environment.GetEnvironmentVariable(LoginTokenVariable) : null;
        _join = new JoinAttempt(Transport, new ReconnectTokens(TokenStorePath(args)), host, port, name, login);
    }

    /// <summary>D335: the environment variable the launcher passes the hub's game token in (not on the command line, which other programs can read).</summary>
    public const string LoginTokenVariable = "ANOMALY_GAME_TOKEN";

    // The reconnect token file: --token-store, else user://reconnect-tokens.txt (the user data folder); "none": in memory.
    private static string TokenStorePath(ClientArgs args)
    {
        if (args.TokenStore != null)
            return args.TokenStore.Length == 0 ? null : args.TokenStore;
        return ProjectSettings.GlobalizePath("user://reconnect-tokens.txt");
    }

    private void Cancel()
    {
        if (_client != null)
        {
            _client.Disconnect();
            _client.Dispose();
            _join = null;
        }
        Reset();
        ShowStatus("Cancelled.", false);
    }

    private void Failed(string reason)
    {
        // M3 I1c: a scripted run (the soak, the smoke, a screenshot run, a headless one) has no one to click again and exits with 1; an interactive one, with
        // or without --connect, stays here with the reason on the screen.
        ConnectionLoss loss = ConnectionLoss.Of(App.Instance.Args, DisplayServer.GetName() == "headless", reason, duringGame: false);
        GD.PrintErr(loss.LogLine);
        if (_client != null)
        {
            _client.Dispose();
            _join = null;
        }
        Reset();
        ShowStatus(loss.Notice, true);
        if (loss.Quit)
            App.Instance.Quit(loss.ExitCode);
    }

    private void Reset()
    {
        _button.Text = "Connect";
        SetFieldsEnabled(true);
        // the fields are back, with the reason under them
        if (_loading != null)
            _loading.Visible = false;
        GetNode<Control>("Center").Visible = true;
    }

    private void SetFieldsEnabled(bool enabled)
    {
        _host.Editable = enabled;
        _port.Editable = enabled;
        _name.Editable = enabled;
    }

    private void ShowStatus(string text, bool error)
    {
        _status.Text = text;
        _status.AddThemeColorOverride("font_color", error ? new Color(1f, 0.45f, 0.4f) : new Color(0.8f, 0.85f, 0.95f));
    }

    /// <summary>A seed that depends on the name only (the fake world repeats for the same name).</summary>
    // The converted MetaStation of tools/Anomaly.Tools.Maps (docs/ai/specs/map-format.md 2): maps/ at the repository root, found from the project folder upward.
    private static string DefaultMapPath()
    {
        string relative = "maps/map_files/MetaStation/MetaStation.as3dmap";
        string beside = Render.DataPaths.Find(relative); // an exported build's copy, beside its executable
        if (System.IO.File.Exists(beside))
            return beside;
        string dir = ProjectSettings.GlobalizePath("res://").TrimEnd('/');
        for (int depth = 0; depth < 5 && !string.IsNullOrEmpty(dir); depth++)
        {
            string candidate = System.IO.Path.Combine(dir, relative);
            if (System.IO.File.Exists(candidate))
                return candidate;
            dir = System.IO.Path.GetDirectoryName(dir);
        }
        return relative;
    }

    private static int StableSeed(string name)
    {
        int hash = 17;
        foreach (char c in name)
            hash = unchecked(hash * 31 + c);
        return hash & 0x7FFFFFFF;
    }

#if ANOMALY_EDITOR
    private Anomaly.Client.Shared.Editor.EditorSession _editorStarting; // the map editor's world is starting
    private double _editorStartedAt;

    // The map editor (docs/ai/specs/map-editor.md 3): the map of the file, or a new one, behind an editor feed: no server to run, no player. The feed starts
    // the game's own world for the map on another thread; the screen waits for it as it waits for a connection.
    private void StartEditor(ClientArgs args, double now)
    {
        string path = args.EditorMapPath.Length > 0 ? args.EditorMapPath : "untitled" + Anomaly.Core.Tg.MapText.Extension;
        try
        {
            // (a build's maps are beside its executable; in a checkout the feed finds the repository's own)
            string maps = Render.DataPaths.Find("maps");
            if (System.IO.Directory.Exists(System.IO.Path.Combine(maps, "map_jsons")))
                Anomaly.Client.Shared.Editor.EditorFeed.GameMaps = maps;
            (int width, int height, int levels) = args.EditorSize;
            var session = Anomaly.Client.Shared.Editor.EditorSession.OpenOrNew(path, width, height, levels);
            // (a new station at its place in a maps folder is that folder's at once, as File, New makes it: GameScene.EditorOpen)
            if (!session.OnDisk && Anomaly.Client.Shared.Editor.EditorFeed.PlaceOf(session.FilePath) != null)
                session.Save();
            WaitForEditor(session, now);
        }
        catch (Exception e) when (e is System.IO.IOException or UnauthorizedAccessException or Anomaly.Core.Tg.MapTextException or ArgumentException)
        {
            GD.PrintErr($"[anomaly] editor: {path}: {e.Message}");
            App.Instance.Quit(2);
        }
    }

    private void WaitForEditor(Anomaly.Client.Shared.Editor.EditorSession session, double now)
    {
        GD.Print($"[anomaly] editor: {session.FilePath} {session.Document.Width}x{session.Document.Height}x{session.Document.Levels}{(session.OnDisk ? string.Empty : " (a new map)")}");
        _editorStarting = session;
        _editorStartedAt = now;
        SetFieldsEnabled(false);
        _button.Disabled = true;
    }

    private void PollEditorStart(double now)
    {
        Anomaly.Client.Shared.Editor.EditorSession session = _editorStarting;
        session.Feed.Poll(now);
        switch (session.Feed.Phase)
        {
            case FeedPhase.Playing:
                if (_loadingBar != null && _loadingBar.Value < 1)
                {
                    // (one frame with the bar full, before the making of the game's scene holds the picture; the small window of the editor's start
                    // stays as it is until that scene is made: GameScene.BuildEditorUi)
                    _loadingBar.Value = 1;
                    ShowStatus(session.Title.TrimEnd(' ', '*') + ": building the scene...", false);
                    break;
                }
                GD.Print(string.Create(CultureInfo.InvariantCulture, $"[anomaly] editor: the map is shown after {now - _editorStartedAt:0.0} s, {session.Feed.Count} things ({session.Feed.StartTimes})"));
                App.Instance.HoldSplash();
                _editorStarting = null;
                App.Instance.Editor = session;
                App.Instance.Feed = session.Feed;
                App.Instance.GoToGame();
                break;
            case FeedPhase.Closed:
                _editorStarting = null;
                GD.PrintErr("[anomaly] editor: " + session.Feed.CloseReason);
                foreach (string line in session.Feed.Log.TakeLast(30))
                    GD.PrintErr("[anomaly] editor:   " + line);
                ShowStatus(session.Feed.CloseReason, true);
                if (_loadingBar != null)
                    _loadingBar.Visible = false;
                App.Instance.EndSplashWindow(); // (a window with its border, to read why and to close)
                session.Dispose();
                break;
            default:
                (float fraction, string doing) = session.Feed.StartProgress;
                if (_loadingBar != null)
                    _loadingBar.Value = fraction;
                ShowStatus(string.Create(CultureInfo.InvariantCulture, $"{session.Title.TrimEnd(' ', '*')}: {doing}... {now - _editorStartedAt:0} s"), false);
                break;
        }
    }
#endif

    private Control _loading; // the loading screen, once it was shown: over the fields
    private Label _loadingTitle; // ... the program's name
    private Label _loadingStatus; // ... what is being done
    private ProgressBar _loadingBar; // ... and how far it is
    private VBoxContainer _loadingBottom;
    private Panel _loadingEdge; // the small window's edge
    private double? _joiningAt; // when the connection was made and the join began

    // A part of the bar that fills by the clock, for a wait that says nothing of how far it is: half of the part after two seconds, never its end.
    private static float Creep(float from, float to, double seconds) => from + (to - from) * (float)(seconds / (seconds + 2.0));

    /// <summary>
    /// The loading screen (the owner, 2026-11-18): the program's name in the window's top left corner and a bar along the bottom that fills as the wait
    /// goes on, with what is being done written over it, in place of the fields. The game shows it from Connect until its scene is made
    /// and goes back to the fields when the connection fails. The map editor, which makes no connection, shows nothing else while the
    /// game's world starts for its map, in a small window in the middle of the screen: at the program's start (App.TakeFirstWindow), and for another
    /// map opened later (App.TakeSplashWindow). Each has its picture behind (<see cref="LoadingPicture"/>): the editor's own, the game's lobby picture.
    /// </summary>
    private void ShowLoading(string title, string picture)
    {
        GetNode<Control>("Center").Visible = false;
        if (_loading != null)
        {
            _loading.Visible = true;
            return;
        }
        _loading = new Control { MouseFilter = MouseFilterEnum.Ignore };
        _loading.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_loading);
        _loading.AddChild(LoadingPicture(picture));
        _loadingTitle = new Label { Text = title, MouseFilter = MouseFilterEnum.Ignore };
        _loadingTitle.AddThemeColorOverride("font_color", new Color(0.92f, 0.95f, 1f));
        _loading.AddChild(_loadingTitle);
        // (a window with no border is lost on a dark desktop without an edge of its own)
        var edge = new StyleBoxFlat { DrawCenter = false, BorderColor = new Color(1f, 1f, 1f, 0.16f) };
        edge.SetBorderWidthAll(2);
        _loadingEdge = new Panel { MouseFilter = MouseFilterEnum.Ignore };
        _loadingEdge.AddThemeStyleboxOverride("panel", edge);
        _loadingEdge.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _loading.AddChild(_loadingEdge);
        _loadingStatus = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, MouseFilter = MouseFilterEnum.Ignore, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _loadingBar = new ProgressBar { MinValue = 0, MaxValue = 1, Step = 0, ShowPercentage = false, MouseFilter = MouseFilterEnum.Ignore };
        _loadingBar.AddThemeStyleboxOverride("background", new StyleBoxFlat { BgColor = new Color(1f, 1f, 1f, 0.08f) });
        _loadingBar.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = new Color(0.45f, 0.68f, 1f) });
        var line = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        line.AddChild(_loadingStatus);
        _loadingBottom = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, GrowVertical = GrowDirection.Begin };
        _loadingBottom.AddChild(line);
        _loadingBottom.AddChild(_loadingBar);
        _loading.AddChild(_loadingBottom);
        Resized += SizeLoading;
        SizeLoading();
    }

    public const string EditorPicture = "res://assets/ui/editor_loading.webp";
    private const string GamePicture = "res://assets/ui/lobby_title.webp";

    /// <summary>A loading picture (the editor's: the owner, 2026-11-18; the game's: 2026-11-20): over the whole window, cut to fill it. App.StopPlay shows the editor's too.</summary>
    public static TextureRect LoadingPicture(string path)
    {
        var picture = new TextureRect
        {
            Texture = GD.Load<Texture2D>(path),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        picture.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        return picture;
    }

    // Sized by the window's height, so the screen is the same picture on a small display and on a large one; the small window of the editor's start has
    // larger letters for its size than a whole window.
    private void SizeLoading()
    {
        bool small = App.Instance.SplashWindow;
        float unit = GetViewportRect().Size.Y / 100f;
        int margin = Math.Max(8, (int)(unit * (small ? 6f : 3f)));
        _loadingEdge.Visible = small;
        _loadingTitle.AddThemeFontSizeOverride("font_size", Math.Max(16, (int)(unit * (small ? 11f : 8f))));
        _loadingTitle.Size = Vector2.Zero; // shrink to the new sizes before the anchors place it
        _loadingTitle.SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft, LayoutPresetMode.Minsize, margin);
        int text = Math.Max(10, (int)(unit * (small ? 4.2f : 2f)));
        _loadingStatus.AddThemeFontSizeOverride("font_size", text);
        _loadingBar.CustomMinimumSize = new Vector2(0, Math.Max(4f, unit * (small ? 1.6f : 1.2f)));
        _loadingBottom.AddThemeConstantOverride("separation", Math.Max(2, (int)(unit * (small ? 2f : 1f))));
        _loadingBottom.Size = Vector2.Zero;
        _loadingBottom.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide, LayoutPresetMode.Minsize, margin);
    }
}
