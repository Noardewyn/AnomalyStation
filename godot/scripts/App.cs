using System.Globalization;
using System.Linq;
using Godot;
using Anomaly.Client.Shared;
using Anomaly.Core.Kernel;

namespace Anomaly.Client;

/// <summary>
/// The client's autoload (project.godot [autoload]): it lives across the scene changes (connect screen, game), parses the
/// arguments after <c>--</c> once, hands the connection from the connect screen to the game, and does the automation:
/// <c>--quit-after</c>, <c>--screenshot-after</c>, the legacy <c>--screenshot</c>, the core smoke line and the summary line
/// (docs/ai/specs/apps.md section 2).
/// </summary>
public partial class App : Node
{
    private const string ConnectScene = "res://scenes/connect.tscn";
    private const string GameScene = "res://scenes/game.tscn";

    /// <summary>Rendered frames the legacy <c>--screenshot</c> waits so that the first frames' shader compilation settles.</summary>
    private const int LegacyScreenshotFrames = 10;

    /// <summary>Frames between the request of a screenshot and the capture (the frame must show the latest state).</summary>
    private const int CaptureSettleFrames = 2;

    private ulong _startUsec;
    private readonly List<PendingShot> _pending = new();
    private int _frames;
    private int _screenshotsTaken;
    private int _captureWait = -1;
    private string _capturePath;
    private bool _quitWhenScreenshotsDone;
    private double? _gameStartedAt;
    private bool _frameHooked;
    private bool _quitting;
    private int _exitCode;

    public static App Instance { get; private set; }

    public ClientArgs Args { get; private set; }

    /// <summary>The connection the connect screen made, for the game scene to take (null when there is none).</summary>
    public IWorldFeed Feed { get; set; }

#if ANOMALY_EDITOR
    /// <summary>The map open in the map editor (<c>--editor</c>, docs/ai/specs/map-editor.md), whose feed <see cref="Feed"/> is; null in the game.</summary>
    public Anomaly.Client.Shared.Editor.EditorSession Editor { get; set; }

    /// <summary>
    /// The round the map editor's Play runs (docs/ai/specs/map-editor.md 5.7) while the game scene shows it, with <see cref="Feed"/> the connection to it; null
    /// in the editor and in the game. The map's own world (<see cref="Editor"/>) stands meanwhile.
    /// </summary>
    public Anomaly.Client.Shared.Editor.PlayWorld Play { get; private set; }

    /// <summary>True when the scene is the map editor's: a map is open and no round of it is played.</summary>
    public bool Editing => Editor != null && Play == null;

    /// <summary>True while the map editor's Play runs a round of its map.</summary>
    public bool Playing => Play != null;

    private bool StartsAsEditor => Args.Editor;

    /// <summary>True when the program is the map editor (a map is open), in a round of its Play too.</summary>
    public bool HasEditor => Editor != null;

    /// <summary>What the editor's scene was at when Play left it (its camera, its tool, its selection), for the scene that Stop brings back.</summary>
    public object EditorResume { get; set; }

    /// <summary>What the editor says when it comes back from a round (why the round ended by itself).</summary>
    public string EditorNotice { get; set; }

    /// <summary>What the last round's world wrote, for the editor's log.</summary>
    public IReadOnlyCollection<string> LastPlayLog { get; set; } = Array.Empty<string>();
#else
    // The game's own build has no map editor (AnomalyEditor, docs/ai/specs/map-editor.md 5.13).
    public bool Editing => false;
    public bool Playing => false;
    private bool StartsAsEditor => false;
    public bool HasEditor => false;
#endif

    /// <summary>
    /// A scripted run of the map editor goes on across Play and Stop, which change the scene: the time its steps count from, and how many of
    /// <c>--keys</c> and <c>--dev-after</c> are done. (In the game every scene runs the script from its own start.)
    /// </summary>
    public double? ScriptStartedAt { get; set; }

    public int ScriptKeysDone { get; set; }

    public int ScriptDevDone { get; set; }

#if ANOMALY_EDITOR
    /// <summary>Play: the game scene on the round's connection.</summary>
    public void StartPlay(Anomaly.Client.Shared.Editor.PlayWorld round, IWorldFeed connection)
    {
        Play = round;
        Feed = connection;
        GoToGame();
    }

    /// <summary>Stop: the round ends, and the editor's scene comes back on the map's own world, saying <paramref name="notice"/>.</summary>
    public void StopPlay(string notice)
    {
        if (Play == null)
            return;
        LastPlayLog = Play.Log;
        Play.Dispose();
        Play = null;
        EditorNotice = notice;
        Feed = Editor.Feed;
        TakeSplashWindow();
        HoldEditorLoading();
        GoToGame();
    }
#endif

    /// <summary>
    /// The client's view of tg's icons (M2 P7's atlas over <c>godot/assets/tg</c>): the HUD draws tg's screen objects with it
    /// (D107). Plug the atlas in here once at start-up; null means the flat stand-in shapes of <c>Hud/PlaceholderTextures</c>.
    /// </summary>
    public Anomaly.Client.Render.IAppearanceTextures Textures { get; set; }

    /// <summary>A message for the connect screen (why the game ended).</summary>
    public string Notice { get; set; }

    /// <summary>
    /// The host, the port text and the name of the last connection attempt (M3 I1c): the connect screen shows them again after a lost connection, so
    /// that one click on Connect (with the reconnect token the client kept) takes the character back. Null before the first attempt.
    /// </summary>
    public (string Host, string Port, string Name)? LastTarget { get; set; }

    /// <summary>The game scene fills this so that the summary line can describe the run.</summary>
    public Func<string> SummaryProvider { get; set; }

    /// <summary>True once the world was drawn at least once in this run (the exit code of a connect run depends on it).</summary>
    public bool EverPlayed { get; set; }

    /// <summary>The monotonic time in seconds (never goes back, unlike the wall clock).</summary>
    public static double NowSeconds => Time.GetTicksUsec() / 1_000_000.0;

    /// <summary>Seconds since the program started.</summary>
    public double Uptime => (Time.GetTicksUsec() - _startUsec) / 1_000_000.0;

    public override void _EnterTree()
    {
        Instance = this;
        _startUsec = Time.GetTicksUsec();
        try
        {
            int moved = UserFolderMove.Run(UserFolderMove.OldFolder(OS.GetDataDir(), OS.GetName() == "Linux"), OS.GetUserDataDir());
            if (moved > 0)
                GD.Print($"[anomaly] the user folder moved to {OS.GetUserDataDir()} ({moved} entries, D347)");
        }
        catch (Exception e) when (e is System.IO.IOException or UnauthorizedAccessException)
        {
            GD.PrintErr("[anomaly] the old user folder was not moved: " + e.Message); // the game starts with what the new folder has
        }
    }

    private Label _versionTag;
    private float _versionTagScale = -1;

    /// <summary>The HUD's text scale (<see cref="HudMetrics.TextScale"/>) while the game scene runs, which sets it every
    /// frame and clears it on leaving; null on the other screens, where the version tag takes the screen's own size.</summary>
    public float? HudTextScale { get; set; }

    /// <summary>The bottom edge of the version tag in viewport pixels, 0 before it is laid out: the HUD's top-right text
    /// (<see cref="Hud.StatsCorner"/>) starts below it.</summary>
    public float VersionTagBottom => _versionTag != null && _versionTag.IsInsideTree() ? _versionTag.GetGlobalRect().End.Y : 0;

    /// <summary>
    /// The build's version in the window's top-right corner (docs/versioning.md), on every screen and over the HUD, so a
    /// screenshot from a play test names its build: white text with the HUD text's dark outline, no plate (the owner,
    /// 2026-10-18), sized like the HUD's text (<see cref="UpdateVersionTag"/>). It takes no mouse input; only the dev menu
    /// draws above it.
    /// </summary>
    private void AddVersionTag()
    {
        var layer = new CanvasLayer { Name = "VersionTag", Layer = 99 };
        _versionTag = new Label
        {
            Name = "Version",
            Text = "Anomaly Station, Development Build, v" + Anomaly.Protocol.BuildInfo.Display, // the phase is in the version (1.0.0-pre-alpha, D280)
            HorizontalAlignment = HorizontalAlignment.Right,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Modulate = new Color(1, 1, 1, Hud.StatsCorner.TextAlpha),
        };
        _versionTag.AddThemeColorOverride("font_color", Colors.White);
        _versionTag.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.85f));
        _versionTag.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight, Control.LayoutPresetMode.Minsize);
        _versionTag.GrowHorizontal = Control.GrowDirection.Begin;
        layer.AddChild(_versionTag);
        AddChild(layer);
        UpdateVersionTag();
    }

    /// <summary>Whether the version tag is shown: the map editor, whose bars are in that corner, names the build in its status bar instead.</summary>
    public bool VersionTagShown
    {
        set => _versionTag.Visible = value;
    }

    /// <summary>Sizes the version tag like F4's corner text (11 at the HUD's text scale, outline 4), when the scale changed.</summary>
    private void UpdateVersionTag()
    {
        if (_versionTag == null)
            return;
        _versionTag.Modulate = new Color(1, 1, 1, Hud.StatsCorner.TextAlpha);
        Vector2 view = GetViewport().GetVisibleRect().Size;
        float scale = HudTextScale ?? new HudMetrics(view.X, view.Y, 1f).TextScale;
        if (Math.Abs(scale - _versionTagScale) < 0.001f)
            return;
        _versionTagScale = scale;
        int Px(float px) => Math.Max(1, (int)Math.Round(px * scale));
        _versionTag.AddThemeFontSizeOverride("font_size", Px(11));
        _versionTag.AddThemeConstantOverride("outline_size", Px(4));
        _versionTag.Size = Vector2.Zero; // shrink to the new text before the anchors place it
        _versionTag.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight, Control.LayoutPresetMode.Minsize, Px(4));
    }

    public override void _Ready()
    {
        string[] userArgs = OS.GetCmdlineUserArgs();
        // A shipped build names its server in server.txt beside the exe ("host" or "host:port"); an explicit --connect wins. The player types a name unless --name is given.
        string serverFile = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(OS.GetExecutablePath()) ?? "", "server.txt");
        if (!userArgs.Any(a => a.StartsWith("--connect", StringComparison.Ordinal)) && System.IO.File.Exists(serverFile))
        {
            string address = System.IO.File.ReadAllText(serverFile).Trim();
            if (address.Length > 0)
                userArgs = userArgs.Append("--server-file=" + address).ToArray();
        }
#if ANOMALY_EDITOR
        // the program under the map editor's name is the map editor (a copy or a link of the game's executable: map-editor.md 5.13)
        if (ClientArgs.EditorByName(OS.GetExecutablePath()) && !userArgs.Any(a => a.StartsWith("--editor", StringComparison.Ordinal)))
            userArgs = userArgs.Append("--editor").ToArray();
#endif
        Args = ClientArgs.Parse(userArgs);
        foreach (string error in Args.Errors)
            GD.PrintErr($"[anomaly] argument error: {error}");

        // The build this run is (docs/versioning.md): in the log and in the window title, so a report names its build.
        GD.Print("[anomaly] Anomaly Station Development Build, client " + Anomaly.Protocol.BuildInfo.Display + ", protocol " + Anomaly.Protocol.ProtocolVersion.Current);
        DisplayServer.WindowSetTitle((StartsAsEditor ? ClientArgs.EditorName : "Anomaly Station") + " " + Anomaly.Protocol.BuildInfo.Display);
        TakeFirstWindow(splash: StartsAsEditor && Args.Errors.Count == 0);
        AddVersionTag();
        Render.AssetPreload.Start(); // the sounds, the models and the sheets' bytes, on worker threads while the connect screen is up
        Dev.TracyPhases.Install(this);

        // M0's proof that the core assemblies load and run inside Godot, kept as the first line of every run.
        var world = new World(1);
        world.Step(20);
        GD.Print(string.Create(CultureInfo.InvariantCulture,
            $"[anomaly] core smoke: seed={world.Seed} tick={world.Tick} time={world.Time:0.0#} hash={world.ComputeStateHash():X16}"));

        if (Args.Errors.Count > 0 && Args.Automated)
        {
            Quit(2);
            return;
        }

        foreach (ScreenshotRequest request in Args.Screenshots.OrderBy(r => r.AfterSeconds))
            _pending.Add(new PendingShot(request.AfterSeconds, request.Path, false));
        if (Args.LegacyScreenshot != null)
            _pending.Insert(0, new PendingShot(0, Args.LegacyScreenshot, true));
        _quitWhenScreenshotsDone = _pending.Count > 0 && !Args.QuitAfter.HasValue;
        if (_pending.Count > 0 && DisplayServer.GetName() == "headless")
        {
            // --headless runs the dummy renderer, which draws nothing and never emits frame_post_draw.
            GD.PrintErr("[anomaly] screenshot: a --headless run has no renderer; run with a window to save a screenshot");
            _pending.Clear();
            _quitWhenScreenshotsDone = false;
        }
        if (_pending.Count > 0)
        {
            RenderingServer.FramePostDraw += OnFramePostDraw;
            _frameHooked = true;
        }
    }

    private (DisplayServer.WindowMode Mode, Vector2I Size, Vector2I Position)? _windowBeforeSplash;

    /// <summary>True while the window is the small one of the map editor's start.</summary>
    public bool SplashWindow => _windowBeforeSplash.HasValue;

    /// <summary>The note a start of the map editor leaves for the engine's next start: project.godot names this file as its file of overrides.</summary>
    private const string FirstWindowNote = "user://first_window.cfg";

    // The map editor starts in a small window with no border in the middle of the screen, as programs of its kind show their start (the owner,
    // 2026-11-18); the game starts in the project's window. The engine makes the window about 0.7 s before any of this code runs, so what it makes
    // is told to it by the start before: a start of the map editor leaves a note of its small window, which the engine reads as project settings
    // (application/config/project_settings_override), and a start of the game takes the note away. The first start after the other program shows
    // the other one's window for that moment, and this gives it its own.
    private void TakeFirstWindow(bool splash)
    {
        if (DisplayServer.GetName() == "headless")
            return;
        int screen = DisplayServer.WindowGetCurrentScreen();
        Vector2I size = SplashSize(screen);
        try
        {
            string file = ProjectSettings.GlobalizePath(FirstWindowNote);
            if (splash)
                System.IO.File.WriteAllText(file, string.Create(CultureInfo.InvariantCulture, $"[display]\n\nwindow/size/borderless=true\nwindow/size/window_width_override={size.X}\nwindow/size/window_height_override={size.Y}\n"));
            else
                System.IO.File.Delete(file);
        }
        catch (Exception e) when (e is System.IO.IOException or UnauthorizedAccessException)
        {
            GD.PrintErr("[anomaly] the note of the first window is not kept: " + e.Message); // (the next start shows the window of this one first, no more)
        }

        var now = (Mode: DisplayServer.WindowGetMode(), Size: DisplayServer.WindowGetSize(), Position: DisplayServer.WindowGetPosition());
        var note = new Vector2I(ProjectSettings.GetSetting("display/window/size/window_width_override").AsInt32(), ProjectSettings.GetSetting("display/window/size/window_height_override").AsInt32());
        bool noted = note.X > 0; // this start read a note: the window has no border
        // The note's size is the window's too, unless the command line gave the engine another (--resolution, --fullscreen, --maximized: the
        // engine keeps those arguments to itself, so the window's size is what tells).
        bool made = noted && now.Mode == DisplayServer.WindowMode.Windowed && Math.Abs(now.Size.X - note.X) <= 16 && Math.Abs(now.Size.Y - note.Y) <= 16;
        if (noted)
            _windowBeforeSplash = made ? ProjectWindow(screen) : now;
        if (!splash)
        {
            EndSplashWindow(); // (the game after the map editor: the note's window becomes the project's)
            return;
        }
        _windowBeforeSplash ??= now;
        if (made && now.Size == size)
            return; // (the engine has made the small window already, and nothing moves)
        ShrinkWindow(screen, size);
    }

    /// <summary>
    /// The map editor opens another map (a file, or a new one), or comes back from Play: it loads in the small window of the program's start, as the
    /// first map did (the owner, 2026-11-18), and the editor's window is as it was once the map's scene has drawn (<see cref="EndSplashWindow"/>).
    /// </summary>
    private void TakeSplashWindow()
    {
        if (DisplayServer.GetName() == "headless" || SplashWindow)
            return;
        _windowBeforeSplash = (DisplayServer.WindowGetMode(), DisplayServer.WindowGetSize(), DisplayServer.WindowGetPosition());
        int screen = DisplayServer.WindowGetCurrentScreen();
        ShrinkWindow(screen, SplashSize(screen));
    }

    private static Vector2I SplashSize(int screen)
    {
        int width = Math.Clamp(DisplayServer.ScreenGetSize(screen).X / 3, 480, 1280);
        return new Vector2I(width, width * 9 / 16);
    }

    private static void ShrinkWindow(int screen, Vector2I size)
    {
        DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
        DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless, true);
        DisplayServer.WindowSetSize(size);
        Rect2I room = DisplayServer.ScreenGetUsableRect(screen); // (where the engine stands the window it makes of the note)
        DisplayServer.WindowSetPosition(room.Position + (room.Size - size) / 2);
    }

    // The window as the engine makes it of the project's settings, when it reads no note: in the middle of the screen, and filling a screen that
    // has no room for it.
    private static (DisplayServer.WindowMode Mode, Vector2I Size, Vector2I Position) ProjectWindow(int screen)
    {
        var size = new Vector2I(ProjectSettings.GetSetting("display/window/size/viewport_width").AsInt32(), ProjectSettings.GetSetting("display/window/size/viewport_height").AsInt32());
        Rect2I room = DisplayServer.ScreenGetUsableRect(screen);
        bool fits = size.X <= room.Size.X && size.Y < room.Size.Y;
        return (fits ? DisplayServer.WindowMode.Windowed : DisplayServer.WindowMode.Maximized, size, room.Position + (room.Size - size) / 2);
    }

    private CanvasLayer _splashHeld; // the loading screen's last picture, over the scene that is made under it
    private int _splashFrames; // the frames the game's scene draws under that picture before the window is the program's own again

    /// <summary>
    /// The small window's picture stays as it is while the game's scene is made and draws its first frames, which take seconds: the picture of the
    /// loading screen is kept over them, and the window gets its own size once they are drawn (the owner, 2026-11-18: given its size before the
    /// scene was made, the window stood large with the loading screen in it for those seconds).
    /// </summary>
    public void HoldSplash()
    {
        if (_splashHeld != null) // (also for a map opened later in the editor's own window: its scene is made under the loading screen as well)
            return;
        Image picture = GetViewport().GetTexture().GetImage();
        if (picture == null || picture.IsEmpty())
            return;
        var held = new TextureRect { Texture = ImageTexture.CreateFromImage(picture), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale };
        held.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        HoldOver(held);
    }

    // Stop: the editor's scene is made again, which takes seconds; the editor's loading picture and its name are over it until it has drawn.
    private void HoldEditorLoading()
    {
        if (_splashHeld != null)
            return;
        // (it lays itself out again when the viewport takes the small window's size, on a later frame after TakeSplashWindow)
        HoldOver(new Hud.LoadingCover(ClientArgs.EditorName, Hud.LoadingCover.EditorPicture) { Progress = Hud.LoadingCover.ConnectShare });
    }

    private void HoldOver(Control picture)
    {
        _splashHeld = new CanvasLayer { Layer = 128 }; // (over every layer of the scene)
        _splashHeld.AddChild(picture);
        AddChild(_splashHeld);
    }

    /// <summary>The window is the program's own again, as it was before <see cref="TakeFirstWindow"/>: the map's scene has drawn, or its start failed.</summary>
    public void EndSplashWindow()
    {
        _splashFrames = 0;
        _splashHeld?.QueueFree();
        _splashHeld = null;
        if (_windowBeforeSplash is not { } before)
            return;
        _windowBeforeSplash = null;
        DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless, false);
        DisplayServer.WindowSetSize(before.Size);
        DisplayServer.WindowSetPosition(before.Position);
        DisplayServer.WindowSetMode(before.Mode);
    }

    public override void _ExitTree()
    {
        if (_frameHooked)
            RenderingServer.FramePostDraw -= OnFramePostDraw;
#if ANOMALY_EDITOR
        Play?.Dispose();
        Editor?.Dispose(); // the map editor's world, when the window was closed
#endif
    }

    /// <summary>The game scene calls this when its first frame starts: <c>--screenshot-after</c> counts from here.</summary>
    public void GameStarted()
    {
        if (!HasEditor || !_gameStartedAt.HasValue) // (the map editor's Play and Stop change the scene: its screenshots keep counting from its first)
            _gameStartedAt = NowSeconds;
        EverPlayed = true;
        if (SplashWindow || _splashHeld != null)
            _splashFrames = 3; // (the scene's first frames are drawn under the loading screen's picture: HoldSplash, HoldEditorLoading)
    }

    public void GoToGame() => GetTree().ChangeSceneToFile(GameScene);

    /// <summary>D306: the round ended; <see cref="Feed"/> waits on the host's lobby for the next world (<see cref="BetweenRoundsScreen"/>).</summary>
    public void GoToBetweenRounds() => GetTree().ChangeSceneToNode(new BetweenRoundsScreen());

#if ANOMALY_EDITOR
    /// <summary>The map editor opens another map (docs/ai/specs/map-editor.md 5.2): the session whose world the connect screen waits for.</summary>
    public Anomaly.Client.Shared.Editor.EditorSession EditorStarting { get; set; }

    /// <summary>Leaves the map that is open for <paramref name="next"/>: the open one's world is stopped, the connect screen starts the next one's.</summary>
    public void OpenEditor(Anomaly.Client.Shared.Editor.EditorSession next)
    {
        Editor?.Dispose();
        Editor = null;
        EditorStarting = next;
        TakeSplashWindow();
        // (a scripted run goes on across the maps it opens, as across Play and Stop: its steps and its screenshots keep counting, each step done once)
        var script = (ScriptStartedAt, ScriptKeysDone, ScriptDevDone, _gameStartedAt);
        GoToConnect(null);
        (ScriptStartedAt, ScriptKeysDone, ScriptDevDone, _gameStartedAt) = script;
    }
#endif

    public void GoToConnect(string notice)
    {
        Notice = notice;
        Feed = null;
        _gameStartedAt = null;
        _splashHeld?.QueueFree(); // (a scene that left before its first frame: the held picture would cover the connect screen)
        (_splashHeld, _splashFrames) = (null, 0);
        (ScriptStartedAt, ScriptKeysDone, ScriptDevDone) = (null, 0, 0);
        GetTree().ChangeSceneToFile(ConnectScene);
    }

    public override void _Process(double delta)
    {
        if (_quitting)
            return;
        if (_splashFrames > 0 && --_splashFrames == 0)
            EndSplashWindow();
        UpdateVersionTag();
        _frames++;
        if (Args.QuitAfter.HasValue && Uptime >= Args.QuitAfter.Value)
        {
            Quit(ExitCodeOfARun());
            return;
        }
        if (_capturePath != null || _pending.Count == 0)
            return;
        // The M0 form counts rendered frames from the start; the new form counts seconds from the game's first frame.
        PendingShot next = _pending[0];
        bool due = next.Legacy
            ? _frames >= LegacyScreenshotFrames
            : _gameStartedAt.HasValue && NowSeconds - _gameStartedAt.Value >= next.AfterSeconds;
        if (!due)
            return;
        _pending.RemoveAt(0);
        _capturePath = ResolvePath(next.Path);
        _captureWait = CaptureSettleFrames;
    }

    private void OnFramePostDraw()
    {
        if (_capturePath == null || _quitting)
            return;
        if (_captureWait-- > 0)
            return;
        string path = _capturePath;
        _capturePath = null;

        Image image = GetViewport().GetTexture().GetImage();
        if (image == null || image.IsEmpty())
        {
            GD.PrintErr("[anomaly] screenshot: the renderer produced no image");
            Quit(1);
            return;
        }
        Error error = image.SavePng(path);
        if (error != Error.Ok)
        {
            GD.PrintErr($"[anomaly] screenshot: saving {path} failed: {error}");
            Quit(1);
            return;
        }
        _screenshotsTaken++;
        GD.Print(string.Create(CultureInfo.InvariantCulture, $"[anomaly] screenshot: {image.GetWidth()}x{image.GetHeight()} saved to {path}"));
        if (_pending.Count == 0 && _quitWhenScreenshotsDone)
            Quit(ExitCodeOfARun());
    }

    /// <summary>Ends the program: prints the summary line, then quits with <paramref name="exitCode"/>.</summary>
    public void Quit(int exitCode)
    {
        if (_quitting)
            return;
        _quitting = true;
        _exitCode = exitCode;
        string summary = SummaryProvider?.Invoke();
        if (Dev.FrameProbe.Summary() is { } frames)
            GD.Print($"[frameprobe] summary {frames}");
        if (DisplayServer.GetName() != "headless")
            GD.Print("[anomaly] pipeline compilations at the end: " + Render.ShaderWarmUp.Pipelines());
        GD.Print($"[anomaly] client summary: {summary ?? "no game was started"} preload_misses={Render.AssetPreload.Misses} exit={exitCode}");
#if ANOMALY_EDITOR
        Play?.Dispose();
        Editor?.Dispose(); // the map editor's world
#endif
        GetTree().Quit(exitCode);
    }

    /// <summary>A scripted connect run that never got into the game is a failure (the soak and the smoke look at the exit code).</summary>
    public int ExitCodeOfARun() => Args.HasConnect && !EverPlayed ? 1 : 0;

    private static string ResolvePath(string path) => ProjectSettings.GlobalizePath(path);

    private readonly record struct PendingShot(double AfterSeconds, string Path, bool Legacy);
}
