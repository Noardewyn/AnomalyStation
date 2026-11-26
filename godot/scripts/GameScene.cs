using System.Globalization;
using System.Linq;
using System.Text;
using Godot;
using Anomaly.Client.Hud;
using Anomaly.Client.Light;
using Anomaly.Client.Render;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Icons;
using Anomaly.Client.Shared.Light;
using Anomaly.Client.Shared.World;
using Anomaly.Core.Motion;
using Anomaly.Protocol;

namespace Anomaly.Client;

/// <summary>
/// The game scene (scenes/game.tscn): the map, the entities, the first-person camera, the HUD and the input, fed by an
/// <see cref="IWorldFeed"/> (the real connection or the fake world). The play loop itself is the engine-free
/// <see cref="PlaySession"/> (M2): every frame it polls the feed, moves the render clock (which stops at the data), predicts the
/// own capsule from the keys and the view and sends its frames (D103), places the other mobs from their samples, and keeps the
/// camera layer; this scene reads the keys, draws, and turns the player's actions into the messages the server takes.
/// <para>
/// Controls (all remappable, D53 and D70; the defaults): mouse look (captured; Esc releases, a click captures again), WASD or
/// the arrows move relative to the view, C rest or get up, Shift (tapped alone) walk or run, F combat mode, the left mouse button tg's left click and
/// the right button tg's right click (held: the radial), ` the dev console, F9 the analyzer, F10 the options, F1 camera
/// (first person, free fly, orbit), F2 ceiling, F3 debug HUD, F4 names overlay, \ the dev menu (GameScene.Dev.cs); the rest is
/// docs/running.md's table (ui-overhaul.md section 3). In the free camera WASD (Shift faster), Q and E
/// fly the camera and the avatar stands still. The orbit is the third-person play view (DV53): the keys, the clicks and the crosshair
/// work as in the first person, the mouse wheel zooms, cursor mode's pointer clicks the world, and what the mob cannot see is shaded.
/// </para>
/// </summary>
public partial class GameScene : Node3D
{
    private const string OptionsPath = "user://options.cfg";
    private const string DefaultOptionsPath = "res://default-options.cfg"; // D324: what a new player starts from

    private IWorldFeed _feed;
    private MapRenderer _map;
    private EntityLayer _entities;
    private BuildGhost _buildGhost;
    private IconLibrary _ghostIcons;
    // Third-person play (the owner, 2026-10-16): the own mob's line of sight and its shade (Space Station 14's FOV).
    private int _hideAbove; // the own level in the third person: the levels above it are not drawn (0: all are)
    private Func<float, float, int, bool> _belowView; // the entities' sight while they are hidden (_hideAbove), made once
    private readonly FovMask _fov = new();
    private FovShadow _fovShadow;
    private bool _fovOn;
    private float _scopeZoom = 1f; // the scope's field of view zoom now (ScopeZoom, DV98)
    private bool _handsShown; // D285: the held things are drawn (ViewModel)

    // The free look's facing of the own mob (Godot's yaw: radians counter-clockwise from north), apart from the camera's.
    private double _bodyYaw;
    // the owner (2026-11-20): till then the free look's mob faces the crosshair, after a shot, a hit or a throw at the world (AimedAtWorld)
    private double _aimFacingUntil = double.NegativeInfinity;

    private void AimedAtWorld() => _aimFacingUntil = App.NowSeconds + _options.AimFacingSeconds;

    /// <summary>
    /// The keys and the facing the prediction is given: the camera's yaw and the keys as they are, or in the third person's free look
    /// (<see cref="CameraRig.OrbitFreeLook"/>) the mob turned to the way the keys point from the camera, walking forward that way; standing,
    /// it keeps its last facing. For a while after a shot (<see cref="AimedAtWorld"/>) the free look's mob faces the crosshair as in the plain
    /// third person, the keys strafing.
    /// </summary>
    private (MoveKeys Keys, double Yaw) FreeLookMove(MoveKeys keys, double now)
    {
        if (!(_rig.Mode == CameraMode.Orbit && _rig.OrbitFreeLook) || now <= _aimFacingUntil)
        {
            _bodyYaw = _rig.Yaw;
            return (keys, _rig.Yaw);
        }
        int forward = ((keys & MoveKeys.Forward) != 0 ? 1 : 0) - ((keys & MoveKeys.Back) != 0 ? 1 : 0);
        int right = ((keys & MoveKeys.Right) != 0 ? 1 : 0) - ((keys & MoveKeys.Left) != 0 ? 1 : 0);
        if (forward == 0 && right == 0)
            return (MoveKeys.None, _bodyYaw);
        // the yaw grows to the left (counter-clockwise): a key to the right turns the mob clockwise from the camera's view
        _bodyYaw = Mathf.Wrap(_rig.Yaw + Math.Atan2(-right, forward), -Math.PI, Math.PI);
        return (MoveKeys.Forward, _bodyYaw);
    }
    private int _ownZ = 1;
    private bool _observing; // protocol 51 (DV201): the camera looked through another mob's eyes last frame
    private RenderPose _eyePose; // the drawn pose of the mob the camera looks from, when another (ViewerPosition)
    private bool _cameraEye; // DV382: the mob the camera looked from last frame was a camera eye (seen from a ghost's orbit)
    private CameraMode _modeBeforeObserving;
    private bool _freeLookBeforeObserving;
    private bool _wasGhost; // DV68: the own mob was a ghost last frame (its camera starts in the orbit, and goes back to the eye in the body)

    // The third-person camera stops short of a wall or a closed airlock (the replica's opaque cells, from the floor to the wall's top) on the own level.
    private bool OrbitBlocked(Vector3 p)
    {
        if (_wasGhost || _session?.Prediction.Params.NoClip == true)
            return false; // DV68: a ghost's orbit passes through walls as the ghost does
        float floor = TileSpace.WorldY(_ownZ);
        if (p.Y < floor || p.Y > floor + WorldScale.WallHeight + 0.15f)
            return false;
        int x = FovMask.Cell(p.X / TileSpace.TileSize + 0.5f), y = FovMask.Cell(-p.Z / TileSpace.TileSize + 0.5f);
        return _feed.Replica.IsOpaque(x, y, _ownZ);
    }
    private CameraRig _rig;
    private PlaySession _session;
    private PlayOptions _options;
    private HudLayer _hudLayer;
    private ScreenEffectsOverlay _screenEffects; // D290
    private Hud.BodyMarker _bodyMarker; // DV54: where a ghost's body lies (protocol version 23)
    private Render.MesonLayer _mesonLayer; // protocol version 31: the turfs of SEE_TURFS (the mesons) through the walls
    private (float R, float G, float B) _lightFloor;
    private Render.ViewBlurLayer _viewBlur; // protocol version 29: the game plane's blur (tg's eye blur) over the 3D view
    private Render.LookLayer _look; // D298: the colour grade, the grain and the vignette, under every 2D layer
    private Render.OutlinePass _outline; // D298: the edge outlines, under the camera
    private Render.MirrorPass _mirrors; // the live mirror's reflection
    private DevConsolePanel _console;
    private ReadoutPanel _readoutPanel;
    private MinimapPanel _minimap;
    private Label _cameraHint;
    private Tween _cameraHintTween;
    private readonly ReadoutModel _readout = new(); // D184
    private OptionsPanel _optionsPanel;
    private EscMenu _escMenu; // D282
    private int _escapeSerial = -1;
    private Label _cursorHint; // D282: cursor mode is told on the screen
    private Crosshair _crosshair;
    private int _gunSightHits; // DV96: the feed's hit count the crosshair last marked
    private LightLayer _light;
    private GasFog _gasFog;
    private BenchRunner _bench;
    private string _appliedQuality;
    private RenderPreset _appliedGraphics;
    private float _viewDistance = 100f;
    private readonly DevConsoleModel _consoleModel = new();

    private AutoWalker _walker;
    private HeldDirs _autoWalkHeld;
    private readonly Queue<ScriptedInput> _script = new();
    private readonly Queue<DevCommandRequest> _devScript = new();
    private static readonly Dictionary<string, Key> KeyNames = new();

    private bool _ceiling = true;
    private bool _fullbright;
    private bool _minimapMobs; // D315: the dev trackers on the minimap (the server's minimapmobs)
    private bool _playerMarkersOn; // the dev menu's markers over the players in the world (the same feed)
    private Dev.PlayerMarkers _playerMarkers;
    private bool _names;
    private StatsCorner _statsCorner;
    private bool _ownSeen;
    private string _lastServerText = string.Empty;
    private long _targetUnderCrosshair;

    private double _rateAt;
    private long _rateSent, _rateReceived;
    private double _sentPerSecond, _receivedPerSecond;
    private int _frames;
    private double _startedAt;
    private double _scriptBase; // the time the scripted steps count from: the scene's start, or the map editor's first scene's
    private double _lastFrameAt;
    private long _interactionTicks, _interactionMax;
    private bool _leaving;
    private bool _keepFeed; // D306: the feed goes on to the between rounds screen
    private Action _sceneChange; // the map editor's Play or Stop: the change of the scene, done at the start of the next frame

    public override void _Ready()
    {
        App app = App.Instance;
        _feed = app.Feed;
#if ANOMALY_EDITOR
        Anomaly.Client.Editor.PreviewTint.Clear(); // (the map editor's preview is its scene's: a round played from it starts with none)
#endif
        if (_feed == null || _feed.Phase != FeedPhase.Playing)
        {
            GD.PrintErr("[anomaly] game: there is no running connection to play on");
            app.GoToConnect("There is no connection.");
            _leaving = true;
            return;
        }

        ClientArgs args = app.Args;
        _options = LoadOptions(args);
        // D119 (revised by the owner, 2026-11-20): the one wall and ceiling height, the server's (protocol 91), before anything is built from it
        // (the meshes, the mounts, the lamps), so the server's shots meet the ceiling this client draws
        float wallHeight = (_feed.Map?.WallHeightMillimetres ?? MapGrid.MinWallHeightMillimetres) / 1000f;
        if (!WorldScale.SetWallHeight(wallHeight))
            GD.PrintErr($"[anomaly] the server's wall height {wallHeight} m is outside {WorldScale.MinWallHeight} to {WorldScale.MaxWallHeight}: keeping {WorldScale.WallHeight} m");
        // D120: the standing eye (read by the camera layer every frame; set once here like the wall height).
        if (!WorldScale.SetEyeHeight(args.EyeHeight ?? _options.EyeHeight))
            GD.PrintErr($"[anomaly] eye height {args.EyeHeight ?? _options.EyeHeight} m is outside {WorldScale.MinEyeHeight} to {WorldScale.MaxEyeHeight}: keeping {WorldScale.EyeHeight} m");
        // D121: the item scale (the planner and the prop shader read it when the world is built).
        if (!WorldScale.SetItemScale(args.ItemScale ?? _options.ItemScale))
            GD.PrintErr($"[anomaly] item scale {args.ItemScale ?? _options.ItemScale} is outside {WorldScale.MinItemScale} to {WorldScale.MaxItemScale}: keeping {WorldScale.ItemScale}");
        // D152 (block windows): windows, grilles and girders as blocks (the planner, the renderer and the pick boxes read it when the world is built).
        WorldScale.SetBlockPieces(_options.BlockWindows);
        WorldLooks.Active = WorldLooks.Parse(FileAccess.FileExists(WorldLooksPath) ? FileAccess.GetFileAsString(WorldLooksPath) : null); // (before the map is planned)
        _map = GetNode<MapRenderer>("Map");
        _entities = GetNode<EntityLayer>("Entities");
        _rig = GetNode<CameraRig>("Rig");
        // The map draws the tiles and the static props itself (M3 P6, scope 2i); the entity layer keeps the mobs and whatever moves.
        // Both take their pictures from tg's icons through one set of textures.
        var textures = new AppearanceTextures();
        // the map editor looks at a whole station from far above: the model parts too small to be seen from there are not drawn (map-editor.md 5.6)
        Render.ChunkBatch.SmallPartRange = app.Editing ? EditorSmallPartRange : 0f;
        _map.Build(_feed.Map, _feed.Replica, textures);
        _entities.Bind(_feed, textures, _map);
        _entities.Rest = _map.TryRest; // things at rest lie on shelves and table tops (docs/ai/specs/models.md)
        _entities.LieOn = _map.TryLieOn; // DV127: bodies lie on beds, stretchers and tables
        _entities.StandOn = _map.TryStandOn; // D239: climbers stand on tables and crates
        _entities.RampOn = _map.TryRampOn; // D325: things on the stairs are drawn up the flight (DV370)
        _entities.OwnCanStandOn = () => _session.Prediction.Params.OnClimbable && !_session.Prediction.Params.NoClip;
        // The map is built: from now on a sheet first drawn mid-game is decoded on a worker thread, not in the frame (IconLibrary.Background).
        IconPrefetch.Start(textures.Library);
        _feed.ServerTextReceived += OnServerText;
        GD.Print(string.Create(CultureInfo.InvariantCulture,
            $"[anomaly] world: {_map.ChunkCount} chunks, {_map.WallCount} walls, {_map.FloorCount} floors, {_map.PropCount} props, {_map.PieceCount} pieces, atlas {_map.AtlasEntries} pictures {_map.AtlasSize.X}x{_map.AtlasSize.Y}, {_map.TileLayers} tile layers in {_map.TilePages} page(s), built in {_map.LoadSeconds:0.00} s, walls and ceiling {WorldScale.WallHeight:0.##} m high, eye {WorldScale.EyeHeight:0.##} m, items at {WorldScale.ItemScale:0.##} of the tile scale"));

        // On a network that is not localhost the server is asked to keep two frames of this player's input queued (scope 2d).
        bool remote = args.SimulatesNetwork || (args.HasConnect && !IsLoopback(args.Host));
        _session = new PlaySession(_feed, _options, remote ? 2 : 1);
        // The layer asks the pose source once per entity per frame: the predicted own capsule, the others from their samples (IPoseSource).
        _entities.Poses = _session.Poses;
        _map.Poses = _session.Poses; // a model carried along (a pulled closet) glides as the things do (DV67)
        _session.Poses.RampOn = _map.TryRampOn; // DV371: a climb up the flight is no level jump
        _fovShadow = FovShadow.Create(this);
        BuildUi(app);
        BuildAudio(); // before the dev menu applies its saved settings to it
        BuildDevUi(textures.Library);

        // D288: the sky is a starfield; the light is P7's layer: the sources as Godot lights, the sun and the light of space on what is open to it (D114's grid as the dev comparison).
        Starfield.Apply(GetNode<WorldEnvironment>("WorldEnvironment").Environment);
        Starfield.AddFlare(this);
        AddChild(_buildGhost = new BuildGhost { Name = "BuildGhost" }); // D320: the held tool's next build on the aimed tile
        _ghostIcons = textures.Library;
        AddChild(_shuttleFx = new ShuttleFx { Name = "ShuttleFx", OwnZ = () => _session?.Prediction is { } own ? own.RenderPosition().Item3 : 0 });
        _feed.Spawned += _shuttleFx.OnSpawned;
        Quality.Environment = GetNode<WorldEnvironment>("WorldEnvironment").Environment; // the preset sets its fog (DV62)
        Quality.Entities = _entities; // and whether the mobs cast shadows (D288)
        // The grid's strength and ambient, tuned on MetaStation's pictures (P7 tuned its defaults on a dev room); ANOMALY_LIGHT_STRENGTH and ANOMALY_LIGHT_AMBIENT override them for tuning.
        _light = new LightLayer { Name = "Light", Strength = EnvFloat("ANOMALY_LIGHT_STRENGTH", MetaLightStrength), Ambient = Vector3.One * EnvFloat("ANOMALY_LIGHT_AMBIENT", MetaLightAmbient) };
        _light.Poses = _session.Poses;
        _light.Dynamic = System.Environment.GetEnvironmentVariable("ANOMALY_LIGHT_GRID") != "1"; // ANOMALY_LIGHT_GRID=1: start on the old grid, for side-by-side shots
        // D288's energies, tuned on MetaStation's pictures; ANOMALY_LAMP, ANOMALY_SUN and ANOMALY_SKY override them for tuning
        _light.LampStrength = EnvFloat("ANOMALY_LAMP", _light.LampStrength);
        _light.SunStrength = EnvFloat("ANOMALY_SUN", _light.SunStrength);
        _light.SkyGain = EnvFloat("ANOMALY_SKY", _light.SkyGain);
        _light.FillShare = EnvFloat("ANOMALY_FILL", _light.FillShare);
        AddChild(_light);
        // DV62: the gases in the beams' fog, and the beams tinted by them.
        _gasFog = new GasFog { Name = "GasFog" };
        AddChild(_gasFog);
        _gasFog.Bind(_feed.Replica, _gasLayer, _rig.Camera);
        // DV386: the AI camera net's static over the tiles no camera sees, for a camera eye's viewer (protocol version 78)
        var cameraStatic = new CameraStatic { Name = "CameraStatic" };
        AddChild(cameraStatic);
        cameraStatic.Bind(_feed.Replica);
        _light.GasAt = _gasFog.At;
        _light.DoorFaceAt = _map.DoorFaceAt; // a closed door's cell is lit by its sides (DoorCellLight)
        if (ReplicaHasLightState(_feed.Replica))
            _light.Attach(_feed, _rig.Camera);
        else
        {
            // No grid to light the world: a flat ambient so that the geometry can be seen (a dev fallback; the real light is the grid's).
            GD.Print("[anomaly] light: the replica has no version-3 light state yet (protocol P5): the light grid stays off, flat ambient");
            RenderingServer.GlobalShaderParameterSet(LightGridUniforms.Ambient, new Vector3(0.55f, 0.55f, 0.6f));
        }
        Quality.Occlusion = args.Occlusion ?? true;
        ApplyQuality(args.Quality != null && RenderPreset.TryParse(args.Quality, out RenderPreset forced) ? forced : _options.Graphics);

        _names = args.Names || _options.Dev.NamesOverlay;
        bool played = app.Playing; // a round of the map editor's (GameScene.EditorPlay.cs): the camera's arguments are the editor's, not the round's
        if (args.Camera.HasValue && !played)
            _rig.SetMode(args.Camera.Value);
        else if (app.Editing)
            _rig.SetMode(CameraMode.Free); // the map editor has no player to look through
        _rig.OrbitFreeLook = args.CameraFreeLook;
        _rig.OrbitBlocked = OrbitBlocked;
        if (args.YawDegrees.HasValue && !played)
            _rig.Yaw = Mathf.DegToRad((float)args.YawDegrees.Value);
        if (args.PitchDegrees.HasValue && !played)
            _rig.Pitch = Mathf.DegToRad((float)args.PitchDegrees.Value);
        if (args.AutoWalk)
            _walker = new AutoWalker(args.Seed ?? StableSeed(args.Name));
        // (the map editor's Play and Stop change the scene: its script goes on where it was, App.ScriptStartedAt)
        bool scriptGoesOn = app.HasEditor && app.ScriptStartedAt.HasValue;
        foreach (ScriptedInput step in args.Keys.Skip(scriptGoesOn ? app.ScriptKeysDone : 0))
            _script.Enqueue(step);
        foreach (DevCommandRequest request in args.DevCommands.OrderBy(r => r.AfterSeconds).Skip(scriptGoesOn ? app.ScriptDevDone : 0))
            _devScript.Enqueue(request);
        // The free camera starts above the south edge of the map, looking down at it, unless the arguments say otherwise.
        // (the map editor's camera starts over its map: a small map's world is larger than the map, DV509)
#if ANOMALY_EDITOR
        Vector2 extent = app.Editor is { } open && !played ? new Vector2(open.Document.Width, open.Document.Height) * TileSpace.TileSize : _map.Extent;
#else
        Vector2 extent = _map.Extent;
#endif
        _rig.PlaceFree(new Vector3(extent.X / 2f, 0.62f * extent.X, -extent.Y / 2f + 0.38f * extent.Y));
        if (((args.Camera == CameraMode.Free && !played) || (app.Editing && !args.Camera.HasValue)) && !args.PitchDegrees.HasValue)
            _rig.Pitch = Mathf.DegToRad(-55f);
        if (args.View.HasValue && !played)
        {
            var v = args.View.Value;
            _rig.PlaceFree(new Vector3(TileSpace.WorldX((float)v.X), (float)v.Eye, TileSpace.WorldZ((float)v.Y)));
            _rig.Yaw = Mathf.DegToRad((float)v.Yaw);
            _rig.Pitch = Mathf.DegToRad((float)v.Pitch);
        }

        StartEditor(app, args);
        StartPlayed(app);
        if (args.Bench)
            _bench = new BenchRunner(args, _feed, _map, _rig, GetViewport(), _light, _map.LoadSeconds, _options.CustomGraphics);
        _startedAt = App.NowSeconds;
        _scriptBase = scriptGoesOn ? app.ScriptStartedAt.Value : _startedAt;
        if (app.HasEditor)
            app.ScriptStartedAt = _scriptBase;
        _lastFrameAt = _startedAt;
        _rateAt = _startedAt;
        app.SummaryProvider = Summary;
        app.GameStarted();
        GD.Print($"[anomaly] game: {_feed.Description}: map {_feed.Map.Width}x{_feed.Map.Height}x{_feed.Map.Levels}, {_feed.Count} entities, own #{_feed.OwnEntity}, camera {_rig.Mode}, input buffer target {(remote ? 2 : 1)}");
    }

    private void BuildUi(App app)
    {
        _viewBlur = new Render.ViewBlurLayer { Name = "ViewBlur" };
        AddChild(_viewBlur);
        _look = new Render.LookLayer { Name = "Look" };
        AddChild(_look);
        _mirrors = new Render.MirrorPass { Name = "Mirrors" };
        AddChild(_mirrors);
        _outline = new Render.OutlinePass { Name = "Outline" };
        _rig.Camera.AddChild(_outline);
        Quality.AmbientOcclusion = new Render.AmbientOcclusionPass { Name = "AmbientOcclusion" };
        _rig.Camera.AddChild(Quality.AmbientOcclusion);
        // gaps E2a: the weather of the own level (protocol 84), a haze under the camera and its precipitation around it
        var weather = new Render.WeatherView { Name = "Weather" };
        AddChild(weather);
        weather.Bind(_feed, _rig.Camera);
        _mesonLayer = new Render.MesonLayer { Name = "MesonView" };
        AddChild(_mesonLayer);
        // D290: the own mob's burning, toxins, radiation, shock and hits over the 3D view, under every HUD layer (as tg's fullscreens)
        var effects = new CanvasLayer { Name = "ScreenEffectsLayer", Layer = 0 };
        AddChild(effects);
        _screenEffects = new ScreenEffectsOverlay();
        effects.AddChild(_screenEffects);
        _hudLayer = new HudLayer { Name = "HealthHud", Layer = 2 };
        AddChild(_hudLayer);
        // tg's sprites through the same atlas that draws the pictures; without converted icons the flat stand-in shapes.
        IAppearanceTextures textures = app.Textures ?? (IconPaths.Exists && _entities.Textures != null ? _entities.Textures : new PlaceholderTextures());
        _hudLayer.Bind(_feed.Replica, textures, _options);
        _hudLayer.DamageTaken += () =>
        {
            _session.Camera.AddShake(0.035f, 0.25f);
            _screenEffects.Hit();
        };

        var ui = new CanvasLayer { Name = "Ui", Layer = 3 };
        AddChild(ui);
        _interfaceLayers.Add(ui);
        _bodyMarker = new Hud.BodyMarker { Name = "BodyMarker" };
        ui.AddChild(_bodyMarker);
        _bodyMarker.Bind((textures as AppearanceTextures)?.Library);
        _crosshair = new Crosshair();
        _crosshair.SetScale(_options.CrosshairScalePercent / 100f);
        ui.AddChild(_crosshair);
        _cameraHint = new Label { Name = "CameraHint", Modulate = new Color(1, 1, 1, 0), MouseFilter = Control.MouseFilterEnum.Ignore, HorizontalAlignment = HorizontalAlignment.Center };
        _cameraHint.AddThemeColorOverride("font_outline_color", Colors.Black);
        _cameraHint.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterTop);
        _cameraHint.OffsetTop = 40;
        _cameraHint.GrowHorizontal = Control.GrowDirection.Both;
        ui.AddChild(_cameraHint);
        // D315: the minimap
        _minimap = new MinimapPanel();
        ui.AddChild(_minimap);
        var minimapChrome = new WindowChrome(_minimap, "minimap", _options, SaveOptions);
        _minimap.Bind(_options, minimapChrome, SaveOptions);
        // under the HUD's top left button (a HUD tile from the top, which grows with the screen and the HUD scale) until the player drags it;
        // its place keeps its share of the screen, the panel sizes and scales itself
        Vector2 screen = GetViewport().GetVisibleRect().Size;
        float tile = new HudMetrics(screen.X, screen.Y, _options.UiScale).Tile;
        minimapChrome.Place(new Vector2(0.25f, 1.4f) * tile, Vector2.One * MinimapPanel.Side);
        _readoutPanel = new ReadoutPanel();
        ui.AddChild(_readoutPanel);
        _readoutPanel.Build(_readout, new WindowChrome(_readoutPanel, "readout", _options, SaveOptions));
        _console = new DevConsolePanel();
        ui.AddChild(_console);
        _console.Bind(_consoleModel, _options.Bindings, new WindowChrome(_console, "dev_console", _options, SaveOptions));
        _console.Submitted += OnConsoleSubmitted;
        _consoleModel.Interface = InterfaceCommand;
        _optionsPanel = new OptionsPanel();
        ui.AddChild(_optionsPanel);
        _optionsPanel.Bind(_options, new WindowChrome(_optionsPanel, "settings", _options, SaveOptions));
        _optionsPanel.Changed += OnOptionsChanged;
        ApplyDevUiScale();
        BuildInteractionUi(ui, textures);
        BuildSpeechUi(ui, (textures as AppearanceTextures)?.Library);
        BuildTextToSpeech();
        BuildChatWindow(ui);
        BuildEmoteWheel(ui);
        BuildEscMenu();
    }

    // D282: Esc's menu and the Settings window, over every other layer.
    private void BuildEscMenu()
    {
        var layer = new CanvasLayer { Name = "Menu", Layer = 10 };
        AddChild(layer);
        _optionsPanel.Reparent(layer); // the Settings window opens over the lobby and the tgui windows, under the menu
        _console.Reparent(layer); // the owner (2026-11-02): the dev console over every window, the chat too
        _escMenu = new EscMenu();
        layer.AddChild(_escMenu);
        _escMenu.Build((path, loop) => _sound?.Library?.Get(path, loop));
        _escMenu.Act = (action, parameters) => _session?.UiAct(Limits.EscapeMenuUiWindow, action, parameters);
        _escMenu.Settings = () => _optionsPanel.Visible = true;
        _escMenu.QuitText = App.Instance.Playing ? "Back to the editor" : null;
        _escMenu.Quit = () =>
        {
            if (App.Instance.Playing)
                StopPlay(null); // the map editor's Play: the game is left for the editor, not the program
            else
                App.Instance.Quit(0);
        };
        _escMenu.Vote = () => _session?.Topic(HtmlText.VoteCommand); // DV364
        _escMenu.WindowScale = () => _options.WindowScaleFor(GetViewport().GetVisibleRect().Size.Y);
        _escMenu.SendBugReport = report => _session?.BugReport(report) == true;
        _escMenu.ClientInfo = () => $"Version: {BuildInfo.Display}\nOS: {OS.GetName()} {OS.GetVersion()}\nGPU: {RenderingServer.GetVideoAdapterName()} ({RenderingServer.GetVideoAdapterVendor()}, {RenderingServer.GetVideoAdapterApiVersion()})"
            + $"\nPreset: {_appliedQuality}\nWindow: {DisplayServer.WindowGetSize()}";
        _cursorHint = new Label { MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false, HorizontalAlignment = HorizontalAlignment.Center };
        _cursorHint.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterTop);
        _cursorHint.GrowHorizontal = Control.GrowDirection.Both; // centred on the screen as its text grows
        _cursorHint.OffsetTop = 8;
        _cursorHint.AddThemeColorOverride("font_outline_color", Colors.Black);
        _cursorHint.AddThemeConstantOverride("outline_size", 4);
        layer.AddChild(_cursorHint);
        _lobbyCorner = _escMenu.LobbyCorner();
        _lobbyCorner.Visible = false;
        layer.AddChild(_lobbyCorner);
    }

    private Control _lobbyCorner; // D283: the lobby's Change Log, Bug Report and Exit

    private void ToggleEscMenu()
    {
        if (InLobby && !_escMenu.IsOpen)
            return; // D283: the lobby has no Esc menu, its gear button opens Settings
        if (_escMenu.IsOpen)
            _escMenu.Close();
        else
            _escMenu.Open();
    }

    /// <summary>D282: a panel over the game holds the keys and the pointer (the menu, the options, the dev console).</summary>
    private bool PanelsOpen => _escMenu.IsOpen || _optionsPanel.IsOpen || _console.IsOpen;

    // D282: the one place the pointer is freed and captured (MouseOwner); a scripted run leaves the mouse alone.
    private void SyncMouse()
    {
        if (App.Instance.Args.Automated || DisplayServer.GetName() == "headless" || !GetWindow().HasFocus())
            return;
        // the map editor: the pointer is free but while the right mouse button turns the view
        Input.MouseModeEnum want = (EditorOn ? !_editorLooking : _mouse.Free(PanelsOpen || DevMenuHasMouse)) ? Input.MouseModeEnum.Visible : Input.MouseModeEnum.Captured;
        if (Input.MouseMode != want)
            Input.MouseMode = want;
    }

    public override void _ExitTree()
    {
        App.Instance.HudTextScale = null; // the version tag goes back to the screen's own size
        _playJoining?.Dispose(); // a round the map editor was starting
        _playStarting?.Dispose();
        TeardownInteraction();
        TeardownSpeech();
        TeardownSpeechToText();
        TeardownTextToSpeech();
        TeardownChatTranslation();
        TeardownDev();
        if (_feed != null)
        {
            _feed.ServerTextReceived -= OnServerText;
            if (_shuttleFx != null)
                _feed.Spawned -= _shuttleFx.OnSpawned; // the between rounds screen keeps the feed, and its next round spawns
        }
        // The feed belongs to the game from the hand-over on: leaving the scene (or the program) leaves the server, unless the
        // between rounds screen takes it on (D306).
        if (_feed != null && !_keepFeed)
        {
            _feed.Disconnect();
            (_feed as IDisposable)?.Dispose();
        }
    }

    private ShuttleFx _shuttleFx;
    private double _hyperspaceCheck;
    private Vector3? _hyperspaceFlow;
    private (int MinX, int MinY, int MaxX, int MaxY, int Z)? _hyperspaceShip;

    // Tiles past the own ship's hull the view still reaches in hyperspace: tg keeps SHUTTLE_TRANSIT_BORDER (16) of transit around each
    // ship, so another ship on the reserved level is 32 tiles or more away
    private const int HyperspaceViewMargin = 6;

    // tg's parallax streams while the own area is in transit; here the hyperspace tunnel (D300) shows when the own ship has tg's transit tiles around it
    private void StepHyperspaceSky(double delta)
    {
        if ((_hyperspaceCheck -= delta) <= 0 && _feed.Replica is { } replica && _session?.Prediction is { } own)
        {
            _hyperspaceCheck = 0.5;
            (float x, float y, int z) = own.RenderPosition();
            Func<int, int, string> turfAt = (cx, cy) => replica.TurfTypes.ElementAtOrDefault(replica.TurfTypeIndexAt(cx, cy, z));
            System.Numerics.Vector3? flow = HyperspaceSky.Flow((int)x, (int)y, turfAt);
            _hyperspaceFlow = flow is { } f ? new Vector3(f.X, f.Y, f.Z) : null;
            _hyperspaceShip = flow.HasValue && HyperspaceSky.ShipBounds((int)x, (int)y, turfAt) is var b ? (b.MinX, b.MinY, b.MaxX, b.MaxY, z) : null;
        }
        Starfield.SetHyperspace(_hyperspaceFlow, delta);
        if (_feed.Replica is { } world && _session?.Prediction is { } me)
        {
            int z = me.RenderPosition().Item3;
            _shuttleFx.StepJets(_hyperspaceFlow, world, z, (cx, cy) => world.TurfTypes.ElementAtOrDefault(world.TurfTypeIndexAt(cx, cy, z)), delta);
        }
    }

    public override void _Process(double delta)
    {
        UpdateInterfaceVisibility();
        Dev.FrameProbe.BeginFrame();
        Dev.FrameProbe.Record();
        if (_sceneChange is { } change && !_leaving)
        {
            // the map editor's Play or Stop, asked for in the middle of the last frame or by a key: the scene is left here, where nothing of it is half done
            _sceneChange = null;
            _leaving = true;
            change();
            return;
        }
        if (_leaving || _feed == null)
            return;
        using var probeAll = Dev.FrameProbe.Scope("GameScene");
        double now = App.NowSeconds;
        double dt = Math.Max(0, now - _lastFrameAt);
        _lastFrameAt = now;
        _frames++;

        RunScript(now);
        // a mouse press another part of the interface took (the dev menu) cancels a lone modifier too; one whose release went elsewhere is dropped
        _modifierTap.Frame((long)Input.GetMouseButtonMask(), _modifierTap.Waiting is string waiting && Enum.TryParse(waiting, out Key held) && Input.IsPhysicalKeyPressed(held));
        // the keys are polled, so a field of the dev menu being typed in (ImGui's) is a UI that holds them too
        bool uiOpen = PanelsOpen || _imgui?.WantsText == true || _tguiHost?.Typing == true || SayOpen || ChatTyping;
        MoveKeys keys = _rig.Mode == CameraMode.Free || uiOpen ? MoveKeys.None : (_walker != null ? WalkerKeys(now) : ReadMoveKeys());
        bool controlling = _rig.Mode != CameraMode.Free && !uiOpen;
        (MoveKeys moveKeys, double facing) = FreeLookMove(keys, now);
        using (Dev.FrameProbe.Scope("GameScene/session.Frame"))
            _session.Frame(now, moveKeys, facing, controlling);
        ApplyMapLook();
        Starfield.SetSun(EditorOn ? EditorSunAzimuth(_feed.Replica?.SunAzimuth ?? float.NaN) : _feed.Replica?.SunAzimuth ?? float.NaN, dt); // tg's SSsun, moved once a minute
        bool ashen = _feed.Replica?.IsNoParallax(_ownZ) == true; // D305: Lavaland's own sky (the own or the watched mob's level, last frame's)
        Starfield.SetAshen(ashen);
        if (_light != null)
        {
            _light.SunDirection = Starfield.SunDirection; // D288: the sun's light comes from the sun the sky draws
            _light.Ashen = ashen;
        }
        Starfield.SetMoon((_feed.Replica?.Tick ?? 0) / 20.0); // 20 ticks a second
        StepHyperspaceSky(delta);
        Starfield.UpdateFlare(_rig.Mode);
#if ANOMALY_EDITOR
        if (App.Instance.Play is { } round && (_feed.Phase is FeedPhase.BetweenRounds or FeedPhase.Closed || round.Over))
        {
            // the map editor's Play: a round that ended by itself goes back to the editor, which says why
            StopPlay(round.Failure ?? (_feed.Phase == FeedPhase.Closed ? "The round's connection ended: " + _feed.CloseReason : "The round ended."));
            return;
        }
#endif
        if (_feed.Phase == FeedPhase.BetweenRounds)
        {
            // D306: tg's world/Reboot ended the round: the connection waits on the host's lobby for the next one
            GD.Print("[anomaly] the round has ended: waiting for the next one");
            _leaving = true;
            _keepFeed = true;
            App.Instance.GoToBetweenRounds();
            return;
        }
        if (_feed.Phase == FeedPhase.Closed)
        {
            // M3 I1c: the server closed the session or the connection dropped. A scripted run keeps its exit code (1); an interactive one goes back to
            // the connect screen with the server's reason on it and the name and the address filled in (App.LastTarget), where one click takes the
            // character back with the kept reconnect token. The reason is logged either way.
            ConnectionLoss loss = ConnectionLoss.Of(App.Instance.Args, DisplayServer.GetName() == "headless", _feed.CloseReason, duringGame: true);
            GD.PrintErr(loss.LogLine);
            _leaving = true;
            if (loss.Quit)
            {
                App.Instance.Quit(loss.ExitCode);
                return;
            }
            App.Instance.GoToConnect(loss.Notice);
            return;
        }

        // D160 (the owner, 2026-10-19): a ghost is seen in the third person's free look by default (the camera turns around the ghost, the
        // ghost flies the way the keys point from the camera), the view key switching it to the first person and back; back in the body the
        // view is the eye again. A ghost's orbit looks down through the station, its ceilings off.
        bool ghost = _session.Ghosting;
        if (ghost != _wasGhost)
        {
            _wasGhost = ghost;
            if (_rig.Mode != CameraMode.Free)
            {
                _rig.OrbitFreeLook = ghost;
                _bodyYaw = _rig.Yaw;
                _rig.SetMode(ghost ? CameraMode.Orbit : CameraMode.FirstPerson);
            }
        }
        // The third-person view looks down through the station from above the walls; turned level, its camera is under them and sees the
        // ceilings as the first person does (last frame's camera: this frame's is placed below).
        // DV382: a camera eye is seen as a ghost is, from its orbit through the station, its ceilings off
        bool ghostView = ghost || _cameraEye;
        bool orbitUnder = _rig.Mode == CameraMode.Orbit && !ghostView && _rig.GlobalPosition.Y < TileSpace.WorldY(_ownZ) + WorldScale.WallHeight - 0.02f;
        // (the map editor's camera, flown under the walls into a room, sees the room as a player does: with its ceiling)
        _map.SetCeilingVisible(_ceiling && (_rig.Mode == CameraMode.FirstPerson || orbitUnder || EditorOn && EditorUnder || App.Instance.Args.ShowCeiling));
        _map.RenderTick = _session.RenderTick; // M4v: the static props' animate() chains play at the entities' render tick
        using (Dev.FrameProbe.Scope("GameScene/map.Update"))
            _map.Update(dt, _session?.RenderTick ?? double.NaN);
        // The preset's view distance is for walking about, the third-person view's too (it hangs a few metres back); the free camera
        // (inspection, screenshots) and a ghost's orbit see the whole map.
        if (_bench == null && _rig.Camera != null)
            _rig.Camera.Far = _rig.Mode == CameraMode.FirstPerson ? _viewDistance
                : _rig.Mode == CameraMode.Orbit && !ghostView ? _viewDistance + _rig.OrbitDistance + CameraRig.OrbitAimLead
                : Math.Max(_viewDistance, 700f);
        // the owner (2026-11-02): in hyperspace the view ends a little past the own ship, so the other ships and their gas on the
        // reserved level do not show through the tunnel (the sky is not clipped)
        if (_bench == null && _rig.Camera != null && _hyperspaceShip is { } ship && _rig.Mode != CameraMode.Free)
        {
            Vector3 lens = _rig.Camera.GlobalPosition;
            float reach = 0f;
            foreach (int cx in new[] { ship.MinX - HyperspaceViewMargin, ship.MaxX + HyperspaceViewMargin })
                foreach (int cy in new[] { ship.MinY - HyperspaceViewMargin, ship.MaxY + HyperspaceViewMargin })
                    reach = Math.Max(reach, lens.DistanceTo(new Vector3(TileSpace.WorldX(cx), TileSpace.WorldY(ship.Z), TileSpace.WorldZ(cy))));
            _rig.Camera.Far = Math.Min(_rig.Camera.Far, reach);
        }
        if (_bench != null)
        {
            _bench.Frame(now);
            if (_bench.Finished)
            {
                App.Instance.Quit(0);
                return;
            }
        }
        long own = _feed.OwnEntity;
        RenderPose pose = default;
        bool hasOwn = false;
        float standHeight = 0f;
        if (own != 0 && _feed.Replica.TryGet(own, out ReplicaEntity ownEntity) && _session.Poses.TryGetPose(ownEntity, out pose))
        {
            hasOwn = true;
            _ownSeen = true;
            // D239: on a table or a crate he climbed the eye rises with the body (EntityLayer's StandOn), standing only, and stays under the
            // ceiling by the margin the highest eye setting keeps (DV154)
            // DEVIATION(DV370): on the stairs the eye rises with the flight, to the level above at its far edge
            if (_map.TryRampOn(pose.X, pose.Y, (int)MathF.Round(pose.Z), out float ramp))
                standHeight = ramp;
            else if (_session.Prediction.Params.OnClimbable && !_session.Prediction.Params.NoClip && !PostureBits.IsLying(pose.Posture) && _map.TryStandOn((int)MathF.Round(pose.X), (int)MathF.Round(pose.Y), (int)MathF.Round(pose.Z), out float h))
                standHeight = Math.Clamp(h, 0f, WorldScale.WallHeight - (WorldScale.MinWallHeight - WorldScale.MaxEyeHeight) - WorldScale.EyeHeight);
            // DV127: lying on a bed or a table the eye rises with the body drawn on its top (EntityLayer's LieOn)
            else if (PostureBits.IsLying(pose.Posture) && _map.TryLieOn((int)MathF.Round(pose.X), (int)MathF.Round(pose.Y), (int)MathF.Round(pose.Z), out float lie))
                standHeight = lie;
        }
        else if (own != 0 && _session.Predicting)
        {
            // In a locker (or any container) the own mob is no entity (ReplicatedFields.IsEntity: on a turf only), but the
            // private block still carries its capsule, kept on the container's turf (LocomotionSeam.Follow): the eye stays
            // there, as tg's view centres on the mob's turf, instead of falling to the map's corner.
            (float x, float y, int z) = _session.Prediction.RenderPosition();
            pose = new RenderPose(x, y, z, 0, MoveWish.YawByte(_session.Prediction.Yaw), 0);
            hasOwn = true;
        }

        // M10x slice C (protocol 51): tg's do_observe made client.eye another mob: the camera is that mob's first person view
        // DEVIATION(DV201): at its drawn position and eye height, turned to its replicated yaw, level; the observer's mouse does not turn it
        long eye = _feed.Replica.EyeEntity;
        RenderPose eyePose = default;
        ReplicaEntity eyeEntity = null;
        bool observing = ObserverVision.Observing(own, eye, eye != 0 && _feed.Replica.TryGet(eye, out eyeEntity) && _session.Poses.TryGetPose(eyeEntity, out eyePose));
        // DEVIATION(DV382): a camera eye (a navigation computer's) is seen from a ghost's orbit, turned by the mouse, the keys moving it the
        // way they point from the camera
        bool cameraEye = observing && ObserverVision.IsCameraEye(eyeEntity.TypePath);
        if (observing != _observing || cameraEye != _cameraEye)
        {
            if (observing && !_observing)
            {
                _modeBeforeObserving = _rig.Mode;
                _freeLookBeforeObserving = _rig.OrbitFreeLook;
            }
            _observing = observing;
            _cameraEye = cameraEye;
            if (cameraEye)
            {
                _rig.OrbitFreeLook = true;
                _bodyYaw = _rig.Yaw;
                _rig.SetMode(CameraMode.Orbit);
            }
            else if (observing)
                _rig.SetMode(CameraMode.FirstPerson);
            else
            {
                _rig.OrbitFreeLook = _freeLookBeforeObserving;
                _rig.SetMode(_modeBeforeObserving);
            }
        }
        _eyePose = eyePose;
        if (observing)
        {
            pose = eyePose;
            hasOwn = true;
            standHeight = 0f;
        }

        // Third-person play: what the own mob cannot see is shaded and not drawn (not for a ghost, nor in the free camera).
        _ownZ = pose.Z;
        _minimap.Follow(_feed, hasOwn, pose.Z, pose.X, pose.Y, _rig.Yaw, _minimapMobs ? _feed.MinimapMobs : null); // D315
        _fovOn = _rig.Mode == CameraMode.Orbit && hasOwn && !ghost && !cameraEye && _options.ThirdPersonFov;
        // the light and the gas fog are the own level's, whatever height the camera hangs at (third person and the free camera rise above the walls, a level's height);
        // they are built at the end of _Ready, and a frame can come before
        if (_light != null)
        {
            _light.OwnBeamFromBody = _rig.Mode == CameraMode.Orbit && hasOwn && !ghost && !cameraEye;
            // the free camera's own level (its height); the map editor's camera hangs far above the level it edits
            _light.ViewLevel = EditorOn ? _editorLevel : hasOwn && _rig.Mode != CameraMode.Free ? pose.Z : 0;
            _light.Fullbright = _fullbright;
            if (_gasFog != null)
                _gasFog.ViewLevel = _light.ViewLevel;
        }
        if (_gasLayer != null)
            _gasLayer.Sight = _fovOn ? _fov : null;
        if (_fovOn)
            _fov.Update(_feed.Replica.IsOpaque, pose.X, pose.Y, pose.Z);
        _fovShadow.Apply(_fovOn ? _fov : null, 1f);
        // the owner (2026-11-13): in the third person the levels above the own one are not drawn, so they do not cover it (its ceilings are already off)
        // (the map editor: the levels above the edited one, unless it shows them, and the levels that are no part of its stack)
        _hideAbove = EditorOn ? EditorHideAbove : _rig.Mode == CameraMode.Orbit && hasOwn && !cameraEye ? (int)pose.Z : 0;
        _map.SetSight(_fovOn ? _fov : null, EditorOn ? _editorLevel : hasOwn ? pose.Z : 0, _hideAbove);
        if (_light != null)
            _light.LevelsAboveHidden = _hideAbove != 0;
        _entities.InSight = EditorOn ? _editorInSight ??= (_, _, z) => EditorLevelShown(z) : _hideAbove != 0 ? _belowView ??= (x, y, z) => z <= _hideAbove && (!_fovOn || _fov.PointVisible(x, y, z))
            : _fovOn ? _fov.PointVisible : null;
        _entities.SeeMobs = hasOwn && !observing && ObserverVision.SeesMobs(_feed.Replica?.OwnVision ?? VisionState.Normal, ghost,
            _feed.Replica.TryGet(own, out ReplicaEntity seer) ? seer.State.Posture : (byte)0);
        _entities.PlayerNames = _options.PlayerNames && !_interfaceHiddenNow; // the hidden interface hides the names over heads too
        _entities.MirrorViewer = _mirrors.Update(_rig.Camera, _map, delta); // the camera's place is last frame's: a frame late, unseen
        // T5a: looking down far enough, the own body is drawn for its own eye (its pose's posture: lying and limp, as the entity layer draws it)
        ReplicaEntity body = null;
        bool onTurf = own != 0 && _feed.Replica.TryGet(own, out body);
        _entities.OwnBodyInView = _rig.Camera != null && LookDown.ShowsOwnBody(_rig.Pitch, _rig.FovDegrees, _rig.Camera.Near, _rig.EyeUp, _rig.Mode == CameraMode.FirstPerson,
            onTurf, ghost, observing, PostureBits.IsLying(pose.Posture), PostureBits.IsLimp(pose.Posture), onTurf && body.IsExosuit(), _scopeZoom >= 1.01f);
        using (Dev.FrameProbe.Scope("GameScene/entities.Update"))
            _entities.Update(_session.RenderTick, now, observing ? eye : own, observing || (_rig.Mode == CameraMode.FirstPerson && !ghost), _names);

        // M7x slice D, DV98: tg's scope narrows the first-person field of view by its range modifier (and slows the mouse by as much)
        _scopeZoom = ScopeZoom.Approach(_scopeZoom, _rig.Mode == CameraMode.FirstPerson ? ScopeZoom.Of(_feed.Replica) : 1f, (float)delta);
        _rig.FovDegrees = ScopeZoom.FovDegrees(_options.FovDegrees, _scopeZoom);
        _rig.MouseSensitivity = _options.MouseSensitivity / _scopeZoom;
        _session.Camera.BodyEye = OwnBodyEye(own, ghost || observing, pose); // D346: a possessed corgi, a mouse or a cyborg sees from its own height
        _session.Camera.Kick.Scale = _viewModel?.RecoilScale ?? 1f; // DV96: the held gun's own recoil tuning
        CameraOffsets offsets = _session.CameraFrame(dt);
        (float kickUp, float kickRight) = _session.Camera.Kick.TakeAim(); // DV96: the gun kick's aim part turns the view itself
        if (kickUp != 0f || kickRight != 0f)
            _rig.Turn(kickUp, kickRight);
        if (offsets.FovDegrees != 0f)
            _rig.FovDegrees += offsets.FovDegrees;
        if (_light != null)
        {
            _light.StridePhase = _session.Camera.BobPhase; // D288: the own flashlight sways with the walk
            _light.Stride = _session.Camera.Stride;
        }
        if (observing && !cameraEye) // DV382: the mouse turns the orbit round a camera eye
        {
            offsets = new CameraOffsets(0f, ObserverVision.EyeHeight(pose.Posture), 0f, 0f);
            _rig.Yaw = ObserverVision.Yaw(pose.Yaw);
            _rig.Pitch = 0f;
        }
        Vector3 feet = hasOwn ? new Vector3(TileSpace.WorldX(pose.X), TileSpace.WorldY(pose.Z) + standHeight + pose.Rise, TileSpace.WorldZ(pose.Y)) : Vector3.Zero; // DV371: a level change's rise
        bool typing = _imgui?.WantsText == true || _tguiHost?.Typing == true || SayOpen;
        _rig.UpdateRig(delta, feet, hasOwn, typing ? Vector3.Zero : FlyWish(), !typing && (EditorOn ? EditorHeld("cam.fast") : Input.IsPhysicalKeyPressed(Key.Shift)), offsets);
        _bodyMarker.Update(_rig.Camera, _feed.Replica?.OwnBody, new Vector2(pose.X, pose.Y));
        VisionState vision = _feed.Replica?.OwnVision ?? VisionState.Normal;
        byte posture = _feed.Replica.TryGet(own, out ReplicaEntity viewer) ? viewer.State.Posture : (byte)0;
        bool corpse = ObserverVision.CorpseSight(vision, ghost, posture);
        // a body that cannot move (limp: unconscious, dead) looks round freely; its body keeps the way it fell (the server, LyingFacing)
        bool limp = hasOwn && !ghost && PostureBits.IsLimp(posture);
        // D285: the held things are the own first person's: not a ghost's, nor while seeing through other eyes, lying limp or looking down a scope
        _handsShown = _rig.Mode == CameraMode.FirstPerson && hasOwn && !ghost && !observing && !limp && _scopeZoom < 1.01f;
        _viewBlur.Update(corpse ? null : _feed.Replica);
        _mesonLayer.Update(_feed.Replica, pose.X, pose.Y, (int)pose.Z, hasOwn && ObserverVision.Mesons(vision, ghost, posture), delta);
        // protocol version 31 (DV81): tg's lighting cutoff lifts the dark (the light grid's floor, light_grid.gdshaderinc)
        (float R, float G, float B) floor = EditorOn ? EditorLightFloor : vision.LightFloor; // (the map editor's own: its dark areas shown lit)
        if (floor != _lightFloor)
        {
            _lightFloor = floor;
            _light.VisionFloor = new Vector3(floor.R, floor.G, floor.B); // the layer scales it with the grid's share (D288)
        }

        // M5 slice 3: tg's windows; while one is open the mouse is free for it (as for the dev menu), and captured again after the last.
        // The owner (2026-10-16): the windows and the tooltips follow the screen's height and their own scale, not the HUD's.
        float windowScale = _options.WindowScaleFor(GetViewport().GetVisibleRect().Size.Y);
        // Godot rasterizes a scaled Control's text at its unscaled size; the viewport's oversampling draws the glyphs at the windows' scale, so
        // their text stays sharp (the text drawn at its own size is not blurred by it).
        float chatScale = _options.ChatWindowScaleFor(GetViewport().GetVisibleRect().Size.Y);
        float oversampling = Math.Max(1f, Math.Max(windowScale, chatScale));
        if (!Mathf.IsEqualApprox(GetViewport().OversamplingOverride, oversampling))
            GetViewport().OversamplingOverride = oversampling;
        SyncLobbyChat();
        _screenEffects?.Update(InLobby ? null : _feed.Replica, _options.FullscreenStrength, (float)delta);
        if (_tguiHost != null)
            _tguiHost.Opacity = _options.WindowOpacity;
        if (_chatWindow != null)
            _chatWindow.Opacity = _options.WindowOpacity;
        using (Dev.FrameProbe.Scope("GameScene/tgui.Update"))
            _tguiHost?.Update(_feed.Replica, windowScale);
        UpdateSpeech(now, windowScale, _chatLines?.TextScale ?? 1f);
        UpdateMic();
        UpdateTextToSpeech();
        _chatWindow?.Update(chatScale);
        UpdateEmoteWheel(now);
        // Protocol version 19: the tooltip under the pointer, while the pointer is free (cursor mode hovers the HUD).
        _tooltipView?.Show(_feed.Replica?.Tooltip ?? HudTooltip(), GetViewport().GetMousePosition(), GetViewport().GetVisibleRect().Size, windowScale, _mouse.CursorMode || WindowsOpen);
        // version 52 (G4): tg's escape menu window is the menu's data
        if (_feed?.Replica?.UiWindows.TryGetValue(Limits.EscapeMenuUiWindow, out UiWindowState escape) == true && escape.Serial != _escapeSerial)
        {
            _escapeSerial = escape.Serial;
            _escMenu.Show(escape.Data);
        }
        if (_mouse.Windows(WindowsOpen))
            CursorModeChanged();
        SyncMouse();
        _cursorHint.Visible = _mouse.CursorMode && !PanelsOpen && !InLobby;
        _lobbyCorner.Visible = InLobby && !PanelsOpen;
        // The owner (2026-11-02): the corner's buttons and margin keep their share of the screen, scaled about the screen's corner.
        _lobbyCorner.PivotOffset = _lobbyCorner.Size + new Vector2(16, 16);
        _lobbyCorner.Scale = Vector2.One * (GetViewport().GetVisibleRect().Size.Y / 1080f);
        if (_cursorHint.Visible)
            _cursorHint.Text = "Cursor mode: " + KeyLabel(PlayAction.CursorMode) + " returns to the view";
        long interactionStart = System.Diagnostics.Stopwatch.GetTimestamp();
        UpdateInteraction(now);
        _interactionTicks += System.Diagnostics.Stopwatch.GetTimestamp() - interactionStart;
        _interactionMax = Math.Max(_interactionMax, System.Diagnostics.Stopwatch.GetTimestamp() - interactionStart);
        UpdateDev(now, hasOwn, pose);
        UpdateEditor();
        _hudLayer.RenderTick = _session.RenderTick; // M4v slice 4: the screen objects' flicks and animate() chains play at the entities' render tick
        _hudLayer.CursorMode = _mouse.CursorMode; // ui-overhaul.md 6: tg's zone selector shows only while the cursor is free
        // slice T5c: the doll's part under the free pointer, so its eyes and mouth show tg's hover pictures
        _hudLayer.DollHover = _hudHover.Kind == TargetKind.Screen ? BodyZoneAim.DollFaceAt(_hudLayer.DollZoneAt(_hudHover.Id, _hudHover.IconX, _hudHover.IconY), _hudHover.IconX, _hudHover.IconY) : AimZone.None;
        using (Dev.FrameProbe.Scope("GameScene/hud.Update"))
            _hudLayer.Update(now);
        UpdateRates(now);
        _statsPose = pose;
        _statsHasOwn = hasOwn;
        UpdateStatsCorner();
        if (_console.IsOpen)
            _console.Refresh();
    }

    private Anomaly.Protocol.MapLook _mapLook; // the map's own sky and sun that the sky and the light were given
    private bool _mapLookGiven;
    private float _sunStrengthOwn = float.NaN; // the sun's strength as the game, the environment or the dev menu has it, kept while a map sets its own

    // The map's own sky and sun (protocol 87): the sky's kind and the sun's disc and height are the sky's, the light's strength and colour the light layer's.
    // The map editor shows the settings that are being edited, saved or not.
    private void ApplyMapLook()
    {
        Anomaly.Protocol.MapLook look = EditorOn ? EditorMapLook() : _feed.Map?.Look;
        if (_mapLookGiven && Equals(look, _mapLook))
            return;
        (_mapLook, _mapLookGiven) = (look, true);
        Starfield.SetLook(look);
        if (_light == null)
            return;
        if (float.IsNaN(_sunStrengthOwn))
            _sunStrengthOwn = _light.SunStrength;
        _light.SunStrength = look == null ? _sunStrengthOwn : look.Sun ? look.SunEnergy : 0f;
        _light.SunTint = look == null ? Light.LightLayer.DefaultSunTint : new Color((look.SunColour >> 16 & 255) / 255f, (look.SunColour >> 8 & 255) / 255f, (look.SunColour & 255) / 255f);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_leaving)
            return;
        if (EditorOn && EditorInput(@event))
            return;
        switch (@event)
        {
            // the screen's pixels, not the frame's: the fullscreen resolution does not change the mouse's sensitivity
            case InputEventMouseMotion motion when Input.MouseMode == Input.MouseModeEnum.Captured:
                if (_radialModel.Phase == RadialPhase.Open)
                    _radialModel.Motion(motion.ScreenRelative.X, motion.ScreenRelative.Y); // the radial is aimed with the mouse, the view stays
                else if (_emoteWheel.IsOpen)
                    _emoteWheel.Model.Motion(motion.ScreenRelative.X, motion.ScreenRelative.Y); // M9 slice B: so is the emote wheel
                else
                {
                    (float up, float right) = _rig.Look(motion.ScreenRelative);
                    _session?.Camera.Kick.Compensate(up, right); // DV96: pulling the gun's climb back down
                }
                break;
            case InputEventMouseMotion pointer when _drag.Pressed:
                _drag.Move(pointer.Position.X, pointer.Position.Y); // version 17: the cursor mode's drag
                break;
            case InputEventMouseButton { Pressed: true } when TextFieldTyping:
                BackToGame(); // D292: a click outside the chat's or a window's text field gives the game the keys and the mouse back
                GetViewport().SetInputAsHandled();
                break;
            case InputEventMouseButton button:
                OnMouseButton(button);
                break;
            case InputEventPanGesture or InputEventMagnifyGesture:
                OnTrackpadZoom(@event);
                break;
            case InputEventKey { Pressed: true, Echo: false } key:
                OnKey(key);
                break;
            case InputEventKey { Pressed: false } released:
                OnKeyUp(released);
                break;
        }
    }

    private void OnKey(InputEventKey key)
    {
        if (SayOpen)
        {
            // the say window's field lost the focus (a click elsewhere): the keys are not the game's while it is open
            if (key.PhysicalKeycode == Key.Escape)
                _sayModel.Close();
            return;
        }
        if (key.PhysicalKeycode == Key.Escape)
        {
            // D282, D292: a focused text field lets the keyboard go, else the top window closes, else Esc opens and closes the menu
            if (_options.Bindings.ActionOfPress(key.PhysicalKeycode.ToString(), key.ShiftPressed, key.CtrlPressed) == PlayAction.CloseWindows)
                CloseAllWindows();
            else if (GetViewport().GuiGetFocusOwner() is LineEdit or TextEdit)
                GetViewport().GuiReleaseFocus();
            else if (_escMenu.IsOpen || !CloseTopWindow())
                ToggleEscMenu();
            return;
        }
        if (_escMenu.IsOpen)
            return; // the game behind the menu gets no keys
        if (key.PhysicalKeycode is Key.Enter or Key.KpEnter && !_console.IsOpen && _tguiHost?.Typing != true && _tguiWindows?.Enter() == true)
            return; // version 16: Enter submits the top input modal
        if (key.PhysicalKeycode is Key.Up or Key.Down && !_console.IsOpen && _tguiWindows?.Arrow(key.PhysicalKeycode == Key.Up ? -1 : 1) == true)
            return; // the list input's selection moves with the arrow keys
        if (key.CtrlPressed && !_console.IsOpen && _tguiHost?.Typing != true && WindowScaleKey(key.PhysicalKeycode))
            return;
        if (!_console.IsOpen && !_optionsPanel.IsOpen && OnServerRadialKey(key))
            return;
        if (_optionsPanel.IsOpen || _console.IsOpen)
        {
            if (_optionsPanel.IsOpen && _options.Bindings.Has(PlayAction.Options, key.PhysicalKeycode.ToString()))
                _optionsPanel.Visible = false;
            return;
        }
        // ui-overhaul.md section 5, a press does one thing: Ctrl+Q throws and does not also drop (the pair's action first, else the key's), and
        // Ctrl alone (down a level) waits for its release with nothing else pressed, and does nothing while a text field has the keyboard (ModifierTap)
        string name = key.PhysicalKeycode.ToString();
        (PlayAction? action, bool ownShift, bool ownCtrl) = _options.Bindings.MatchPress(name, key.ShiftPressed, key.CtrlPressed);
        // a key bound to Pull or Examine: only a modifier held on top of its binding goes with tg's click (KeyBindings.ModifiedClick)
        (_pressExtraShift, _pressExtraCtrl) = (key.ShiftPressed && !ownShift, key.CtrlPressed && !ownCtrl);
        if (!_modifierTap.Wait(name, action, KeyboardTaken || _rig.Mode == CameraMode.Free, ModifierTap.OtherModifiers(name, key.ShiftPressed, key.CtrlPressed, key.AltPressed, key.MetaPressed), (long)Input.GetMouseButtonMask()))
            RunKeyAction(action, name);
    }

    private readonly ModifierTap _modifierTap = new();
    private bool _pressExtraShift, _pressExtraCtrl;

    // The action of a key press (key: the physical key's name), or of a lone Ctrl or Shift let go (ModifierTap).
    private void RunKeyAction(PlayAction? action, string key)
    {
        switch (action)
        {
            case PlayAction.Rest:
                _session.Rest();
                break;
            case PlayAction.ToggleMoveIntent:
                _session.ToggleMoveIntent();
                break;
            case PlayAction.ToggleCombatMode:
                _session.ToggleCombatMode();
                break;
            case PlayAction.MoveUpLevel:
            case PlayAction.MoveDownLevel:
                // DV422 (version 83): tg's Move Upwards and Move Downwards; the server decides whether the mob can change level (else Space holds throw mode)
                _session.Keybinding(action == PlayAction.MoveUpLevel ? KeybindingCode.ZLevelUp : KeybindingCode.ZLevelDown);
                break;
            case PlayAction.Console:
                _console.Open();
                GetViewport().SetInputAsHandled();
                break;
            case PlayAction.DevMenu:
                ToggleDevMenu();
                break;
            case PlayAction.CloseWindows:
                CloseAllWindows();
                break;
            case PlayAction.Options:
                _optionsPanel.Visible = true;
                break;
            case PlayAction.Analyzer:
                _readout.Toggle();
                break;
            case PlayAction.CameraMode:
                _rig.CycleMode();
                AnnounceCamera();
                break;
            case PlayAction.ThirdPersonView:
                // F5: first person, the third person behind the mob, the free look, and back to the first person; a ghost's free look
                // and first person (D160); a camera eye is seen from its orbit only (DV382)
                if (_cameraEye)
                    break;
                if (_wasGhost)
                {
                    if (_rig.Mode == CameraMode.Orbit)
                    {
                        _rig.OrbitFreeLook = false;
                        _rig.Yaw = (float)_bodyYaw; // the first person looks where the ghost faces
                        _rig.SetMode(CameraMode.FirstPerson);
                    }
                    else
                    {
                        _rig.OrbitFreeLook = true;
                        _bodyYaw = _rig.Yaw;
                        _rig.SetMode(CameraMode.Orbit);
                    }
                }
                else if (_rig.Mode == CameraMode.Orbit && !_rig.OrbitFreeLook)
                {
                    _rig.OrbitFreeLook = true;
                    _bodyYaw = _rig.Yaw; // the mob keeps facing where it faced
                }
                else if (_rig.Mode == CameraMode.Orbit)
                {
                    _rig.OrbitFreeLook = false;
                    _rig.Yaw = (float)_bodyYaw; // the first person looks where the mob faces
                    _rig.SetMode(CameraMode.FirstPerson);
                }
                else
                {
                    _rig.OrbitFreeLook = false;
                    _rig.SetMode(CameraMode.Orbit);
                }
                AnnounceCamera();
                break;
            case PlayAction.Ceiling:
                _ceiling = !_ceiling;
                SyncDevOptions(); // saved with the dev menu's settings (DevMenuOptions)
                SaveDevOptionsIfDone(false);
                break;
            case PlayAction.Names:
                _names = !_names;
                SyncDevOptions();
                SaveDevOptionsIfDone(false);
                break;
            case PlayAction.DebugHud:
                _options.ShowStats = !_options.ShowStats;
                SaveOptions();
                break;
            case PlayAction.HideInterface:
                InterfaceCommand("toggle");
                break;
            case PlayAction.Flashlight:
                _light.ToggleFlashlight();
                break;
            default:
                if (!OnSpeechKey(action) && !OnEmoteWheelKey(action) && !OnChatWindowKey(action))
                    OnInteractionKey(action);
                break;
        }
    }

    private void OnConsoleSubmitted(string command)
    {
        if (_consoleModel.TryRunLocal(command))
            return; // help and clear: the client's own, never sent
        if (EditorOn && EditorCommand(command))
            return;
        if (App.Instance.Playing && command.Trim() == "editor stop")
        {
            StopPlay(null);
            return;
        }
        // D298: "option KEY=VALUE" sets one of the options file's lines for this run (--dev-after compares the looks in one run)
        if (command.StartsWith("option ", StringComparison.Ordinal))
        {
            string[] pair = command.Substring(7).Split('=', 2);
            string problem = null;
            if (pair.Length == 2 && _options.TrySet(pair[0].Trim(), pair[1].Trim(), out problem))
                OnOptionsChanged();
            else
                GD.PrintErr($"[anomaly] option: {command}: {(pair.Length == 2 ? problem : "expected KEY=VALUE")}");
            return;
        }
        _consoleModel.OnSent(command, App.NowSeconds);
        _session.DevCommand(command);
    }

    // The owner (2026-10-24): 2.2 (was 2.5).
    private const float MetaLightStrength = 2.2f;
    // The owner (2026-10-17): a place without light (a maintenance tunnel) is almost black, as tg's unlit turf is.
    // The owner (2026-10-24): darker still, 0.001 (was 0.008).
    private const float MetaLightAmbient = 0.001f;

    private static float EnvFloat(string name, float fallback)
        => float.TryParse(System.Environment.GetEnvironmentVariable(name), NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : fallback;

    // The replica's version-3 members are protocol P5's; until they exist the light layer is not attached (its grid reads them every frame).
    private static bool ReplicaHasLightState(ReplicaWorld replica)
    {
        try
        {
            _ = replica.BaseLightIndexAt(1, 1, 1);
            _ = replica.IsOpaque(1, 1, 1);
            return true;
        }
        catch (NotImplementedException)
        {
            return false;
        }
    }

    private void ApplyQuality(RenderPreset preset)
    {
        if (preset == _appliedGraphics)
            return;
        _appliedGraphics = preset;
        _appliedQuality = preset.Name;
        _viewDistance = preset.ViewDistance;
        Quality.Apply(preset, GetViewport(), _rig.Camera, _light);
        ApplyLook();
    }

    // D298: after the preset
    private void ApplyLook()
    {
        _look?.Configure(_options);
        _outline?.Configure(_options.OutlineWidth, _options.OutlinePercent / 100f);
        Render.LookLayer.ApplyEnvironment(Quality.Environment, _options);
        if (EditorOn && Quality.Environment != null)
            Quality.Environment.FogEnabled = false; // the map editor looks at a map from far above: the game's distance fog would hide it
    }

    // The owner (2026-10-16): Ctrl+= (or the keypad's +) and Ctrl+- grow and shrink tg's windows and the tooltips by 10 per cent, Ctrl+0 resets them.
    private bool WindowScaleKey(Key key)
    {
        int now = _options.WindowScalePercent;
        int next = PlayOptions.WindowScaleAfterKey(key.ToString(), now);
        if (next < 0)
            return false;
        _options.WindowScalePercent = Math.Clamp(next, PlayOptions.MinWindowScale, PlayOptions.MaxWindowScale);
        if (_options.WindowScalePercent != now)
            SaveOptions();
        return true;
    }

    private void OnOptionsChanged()
    {
        ApplyQuality(_options.Graphics);
        ApplyLook();
        ApplyFullscreenResolution();
        ApplyDevUiScale();
        _crosshair.SetScale(_options.CrosshairScalePercent / 100f);
        _session.Options.HeadBob = _options.HeadBob;
        ApplyPlayAudio();
        SaveOptions();
    }

    /// <summary>The console and the analyzer follow the options' dev windows scale (<see cref="PlayOptions.DevUiScale"/>).</summary>
    private void ApplyDevUiScale()
    {
        float scale = _options.DevUiScale;
        _console.ApplyScale(scale);
        _readoutPanel.ApplyScale(scale);
    }

    // D137: the fullscreen key (F11) switches the window between fullscreen and windowed.
    private void ToggleFullscreen()
    {
        DisplayServer.WindowSetMode(IsFullscreen() ? DisplayServer.WindowMode.Windowed : DisplayServer.WindowMode.Fullscreen);
        ApplyFullscreenResolution();
    }

    private static bool IsFullscreen() => DisplayServer.WindowGetMode() is DisplayServer.WindowMode.Fullscreen or DisplayServer.WindowMode.ExclusiveFullscreen;

    // The owner (2026-10-30): in fullscreen the whole frame is drawn at the options' resolution and scaled to the monitor (bars
    // keep its shape), so the world and every HUD piece scale together; windowed, the frame is the window's own size.
    private void ApplyFullscreenResolution()
    {
        Window root = GetTree().Root;
        (int width, int height) = _options.FullscreenResolution;
        bool scaled = width > 0 && height > 0 && IsFullscreen();
        root.ContentScaleAspect = Window.ContentScaleAspectEnum.Keep;
        root.ContentScaleSize = scaled ? new Vector2I(width, height) : Vector2I.Zero;
        root.ContentScaleMode = scaled ? Window.ContentScaleModeEnum.Viewport : Window.ContentScaleModeEnum.Disabled;
    }

    /// <summary>The <c>--keys</c> script and the <c>--dev-after</c> commands: key steps go through the engine's input (as a keyboard would), view steps set the yaw or pitch.</summary>
    private void RunScript(double now)
    {
        while (_script.Count > 0 && now - _scriptBase >= _script.Peek().AtSeconds)
        {
            ScriptedInput step = _script.Dequeue();
            App.Instance.ScriptKeysDone++;
            switch (step.Action)
            {
                case ScriptAction.Press:
                case ScriptAction.Release:
                    Key key = ScriptKey(step.Key);
                    // a held SHIFT, CTRL, META or ALT counts, as a keyboard's does (Shift+T, a menu's Ctrl+S)
                    Input.ParseInputEvent(new InputEventKey
                    {
                        PhysicalKeycode = key, Keycode = key, Pressed = step.Action == ScriptAction.Press,
                        ShiftPressed = key != Key.Shift && Input.IsPhysicalKeyPressed(Key.Shift),
                        CtrlPressed = key != Key.Ctrl && Input.IsPhysicalKeyPressed(Key.Ctrl),
                        MetaPressed = key != Key.Meta && Input.IsPhysicalKeyPressed(Key.Meta),
                        AltPressed = key != Key.Alt && Input.IsPhysicalKeyPressed(Key.Alt),
                    });
                    break;
                case ScriptAction.Yaw:
                    _rig.Yaw = Mathf.DegToRad((float)step.Value);
                    break;
                case ScriptAction.Pitch:
                    _rig.Pitch = Mathf.DegToRad((float)step.Value);
                    break;
                case ScriptAction.Click:
                    MouseButton index = ScriptButton(step.Key);
                    Vector2 point = step.At is { } at ? new Vector2(at.X, at.Y) : GetViewport().GetVisibleRect().Size / 2f;
                    if (step.At != null)
                    {
                        // a point of the screen (the dev menu's windows): the mouse goes there first, as a hand would move it
                        Input.WarpMouse(point);
                        Input.ParseInputEvent(new InputEventMouseMotion { Position = point, GlobalPosition = point });
                    }
                    Input.ParseInputEvent(WithHeldKeys(new InputEventMouseButton { ButtonIndex = index, Pressed = true, Position = point, GlobalPosition = point }));
                    _pendingReleases.Enqueue((now + 0.06, index, point)); // M4a: a scripted click is a tap, so the right one is still the shove
                    break;
                case ScriptAction.Gesture when step.At is { } where && step.To is { } moved:
                {
                    // a trackpad's two fingers: the pointer is there, then the gesture as the system sends it
                    var spot = new Vector2(where.X, where.Y);
                    Input.WarpMouse(spot);
                    Input.ParseInputEvent(new InputEventMouseMotion { Position = spot, GlobalPosition = spot });
                    Input.ParseInputEvent(step.Value > 0 ? new InputEventMagnifyGesture { Position = spot, Factor = (float)step.Value }
                        : new InputEventPanGesture { Position = spot, Delta = new Vector2(moved.X, moved.Y) });
                    break;
                }
                case ScriptAction.Drag when step.At is { } from && step.To is { } to:
                {
                    // version 17: the press at the start, the pointer moved there in five steps over 0.25 s, the release 0.1 s later
                    var start = new Vector2(from.X, from.Y);
                    var end = new Vector2(to.X, to.Y);
                    MouseButton held = ScriptButton(step.Key);
                    Input.WarpMouse(start);
                    Input.ParseInputEvent(new InputEventMouseMotion { Position = start, GlobalPosition = start });
                    Input.ParseInputEvent(WithHeldKeys(new InputEventMouseButton { ButtonIndex = held, Pressed = true, Position = start, GlobalPosition = start }));
                    (_scriptedMouse, _scriptedDrag) = (start, held);
                    for (int i = 1; i <= 5; i++)
                        _pendingMoves.Enqueue((now + 0.05 * i, start.Lerp(end, i / 5f)));
                    _pendingReleases.Enqueue((now + 0.35, held, end));
                    break;
                }
                case ScriptAction.Radial:
                {
                    // slice T4c: the right button held at the crosshair (the radial opens after RadialModel.HoldSeconds), the entry aimed by its label
                    // at 0.5 s (fresh hints have come by then), then let go: the release chooses it (OnRightReleased)
                    Vector2 centre = GetViewport().GetVisibleRect().Size / 2f;
                    Input.ParseInputEvent(WithHeldKeys(new InputEventMouseButton { ButtonIndex = MouseButton.Right, Pressed = true, Position = centre, GlobalPosition = centre }));
                    _pendingRadial = (now + 0.5, step.Text);
                    _pendingReleases.Enqueue((now + 0.5, MouseButton.Right, centre));
                    break;
                }
                case ScriptAction.Type:
                    foreach (char c in step.Text)
                    {
                        Key letter = char.IsAsciiLetter(c) ? Key.A + (char.ToUpperInvariant(c) - 'A') : char.IsAsciiDigit(c) ? Key.Key0 + (c - '0') : Key.None;
                        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = letter, Keycode = letter, Unicode = c, Pressed = true });
                        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = letter, Keycode = letter, Unicode = c, Pressed = false });
                    }
                    break;
            }
            GD.Print(string.Create(CultureInfo.InvariantCulture, $"[anomaly] script: t={now - _scriptBase:0.00} {step.Action} {step.Key}{(step.Action is ScriptAction.Yaw or ScriptAction.Pitch ? step.Value.ToString(CultureInfo.InvariantCulture) : string.Empty)}"));
        }
        while (_devScript.Count > 0 && now - _scriptBase >= _devScript.Peek().AfterSeconds)
        {
            DevCommandRequest request = _devScript.Dequeue();
            App.Instance.ScriptDevDone++;
            GD.Print(string.Create(CultureInfo.InvariantCulture, $"[anomaly] dev: t={now - _scriptBase:0.00} {request.Text}"));
            OnConsoleSubmitted(request.Text);
            if (_sceneChange != null)
                return; // the line leaves the scene (the map editor's Stop): the script's next steps are the next scene's
        }
    }

    private static MouseButton ScriptButton(string name) => name switch
    {
        "RIGHT" => MouseButton.Right,
        "MIDDLE" => MouseButton.Middle,
        "WHEELUP" => MouseButton.WheelUp,
        "WHEELDOWN" => MouseButton.WheelDown,
        _ => MouseButton.Left,
    };

    // A scripted press of a mouse button with SHIFT, CTRL, META or ALT held counts as a mouse's does (the map editor's Shift + drag).
    private static InputEventMouseButton WithHeldKeys(InputEventMouseButton press)
    {
        press.ShiftPressed = Input.IsPhysicalKeyPressed(Key.Shift);
        press.CtrlPressed = Input.IsPhysicalKeyPressed(Key.Ctrl);
        press.MetaPressed = Input.IsPhysicalKeyPressed(Key.Meta);
        press.AltPressed = Input.IsPhysicalKeyPressed(Key.Alt);
        return press;
    }

    private static Key ScriptKey(string name) => name switch
    {
        "UP" => Key.Up,
        "DOWN" => Key.Down,
        "LEFT" => Key.Left,
        "RIGHT" => Key.Right,
        _ => Enum.TryParse(name, true, out Key parsed) ? parsed : Key.None,
    };

    /// <summary>--autowalk: random view-relative keys, re-picked when blocked (the soak).</summary>
    private MoveKeys WalkerKeys(double now)
    {
        // The walker speaks absolute directions; the avatar's wish is world-space too, so it is turned into keys relative to the view.
        Vector3 pos = _session.Predicting ? new Vector3(_session.Prediction.Predicted.X / 256f, _session.Prediction.Predicted.Y / 256f, 0) : Vector3.Zero;
        _autoWalkHeld = _walker.Update(now, (int)Math.Round(pos.X), (int)Math.Round(pos.Y));
        return AbsoluteToKeys(_autoWalkHeld, _rig.Yaw);
    }

    // The keys that give the wish of an absolute mask at the view's yaw (the inverse of MoveWish for the four cardinal directions).
    private static MoveKeys AbsoluteToKeys(HeldDirs held, double yaw)
    {
        double east = ((held & HeldDirs.East) != 0 ? 1 : 0) - ((held & HeldDirs.West) != 0 ? 1 : 0);
        double north = ((held & HeldDirs.North) != 0 ? 1 : 0) - ((held & HeldDirs.South) != 0 ? 1 : 0);
        if (east == 0 && north == 0)
            return MoveKeys.None;
        // Facing (-sin, cos), right (cos, sin) in (east, north).
        double forward = -Math.Sin(yaw) * east + Math.Cos(yaw) * north;
        double right = Math.Cos(yaw) * east + Math.Sin(yaw) * north;
        MoveKeys keys = MoveKeys.None;
        if (forward > 0.35) keys |= MoveKeys.Forward;
        if (forward < -0.35) keys |= MoveKeys.Back;
        if (right > 0.35) keys |= MoveKeys.Right;
        if (right < -0.35) keys |= MoveKeys.Left;
        return keys;
    }

    private MoveKeys ReadMoveKeys()
    {
        MoveKeys keys = MoveKeys.None;
        // Physical keys: the same keys on every keyboard layout.
        if (IsDown(PlayAction.MoveForward)) keys |= MoveKeys.Forward;
        if (IsDown(PlayAction.MoveBack)) keys |= MoveKeys.Back;
        if (IsDown(PlayAction.MoveLeft)) keys |= MoveKeys.Left;
        if (IsDown(PlayAction.MoveRight)) keys |= MoveKeys.Right;
        return keys;
    }

    private bool IsDown(PlayAction action)
    {
        foreach (string name in _options.Bindings.KeysOf(action))
        {
            if (KeyBindings.IsMouseButton(name))
            {
                MouseButton button = name == "MouseLeft" ? MouseButton.Left : name == "MouseRight" ? MouseButton.Right : MouseButton.Middle;
                if (Input.IsMouseButtonPressed(button))
                    return true;
                continue;
            }
            if (!KeyNames.TryGetValue(name, out Key key))
            {
                key = Enum.TryParse(name, true, out Key parsed) ? parsed : Key.None;
                KeyNames[name] = key;
            }
            if (key != Key.None && Input.IsPhysicalKeyPressed(key))
                return true;
        }
        return false;
    }

    private Vector3 FlyWish()
    {
        if (_rig.Mode != CameraMode.Free)
            return Vector3.Zero;
        if (EditorOn && !(_editorLooking && (_editorCamera == EditorCamera.Fly || _editorTop)) && !(_editorTop && EditorKeysFree()))
            return Vector3.Zero; // the map editor flies while the right mouse button is held; in its top view the keys move the map at any time (but while a field is typed in)
        if (EditorOn)
            return EditorFlyKeys(); // (the editor's own keys: Edit, Hotkeys)
        MoveKeys keys = ReadMoveKeys();
        float x = ((keys & MoveKeys.Right) != 0 ? 1 : 0) - ((keys & MoveKeys.Left) != 0 ? 1 : 0);
        float z = ((keys & MoveKeys.Forward) != 0 ? 1 : 0) - ((keys & MoveKeys.Back) != 0 ? 1 : 0);
        float y = (Input.IsPhysicalKeyPressed(Key.E) ? 1 : 0) - (Input.IsPhysicalKeyPressed(Key.Q) ? 1 : 0);
        return new Vector3(x, y, z);
    }

    private void UpdateRates(double now)
    {
        if (now - _rateAt < 1.0)
            return;
        double span = now - _rateAt;
        long sent = _feed.BytesSent, received = _feed.BytesReceived;
        _sentPerSecond = (sent - _rateSent) / span;
        _receivedPerSecond = (received - _rateReceived) / span;
        _rateSent = sent;
        _rateReceived = received;
        _rateAt = now;
    }

    // D137: the debug HUD's numbers, which the dev menu's Stats tab shows (and the debug HUD key's (F3) pinned window): the connection, the
    // prediction, the player and the server's last text.
    private RenderPose _statsPose;
    private bool _statsHasOwn;

    // D138: the debug HUD key's (F3) corner, drawn by the game's UI: the FPS, then the server's numbers as this client sees them.
    private void UpdateStatsCorner()
    {
        // the HUD's text size (HudMetrics.TextScale), for the version tag as well, which the App draws on every screen
        Vector2 view = GetViewport().GetVisibleRect().Size;
        var metrics = new HudMetrics(view.X, view.Y, _options.UiScale);
        App.Instance.HudTextScale = metrics.TextScale;
        StatsCorner.TextAlpha = _options.HudTextOpacity;
        _statsCorner.Visible = _options.ShowStats;
        if (!_statsCorner.Visible) return;
        CultureInfo c = CultureInfo.InvariantCulture;
        _statsCorner.TextScale = metrics.TextScale;
        _statsCorner.Unit = UiTokens.Unit(metrics);
        // ui-overhaul.md section 6: the ping and the FPS in one chip, the numbers as the template's arguments
        _statsCorner.Chip = Anomaly.Core.DmShim.I18n.T("Ping [] ms | [] FPS", _feed.RoundTripMs, (int)Math.Round(Engine.GetFramesPerSecond()));
        _statsCorner.Lines = new[]
        {
            string.Create(c, $"tick {_feed.Tick} (lag {_session.Clock.LagTicks:0.0})"),
            PredictionLine(c),
            string.Create(c, $"in {Bytes(_receivedPerSecond)}/s   out {Bytes(_sentPerSecond)}/s"),
            WhereLine(c),
        };
    }

    // The owner (2026-10-15): where the player stands (tg's tile x, y, z) and where the camera looks (a compass bearing, clockwise from north, and
    // the pitch), so a screenshot tells the place and the view.
    private string WhereLine(CultureInfo c)
    {
        float bearing = Mathf.PosMod(-Mathf.RadToDeg(_rig.Yaw), 360f); // the rig's yaw is counter-clockwise from north
        string[] names = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
        string facing = names[Mathf.RoundToInt(bearing / 45f) % 8];
        string where = _statsHasOwn ? string.Create(c, $"{Mathf.RoundToInt(_statsPose.X)},{Mathf.RoundToInt(_statsPose.Y)},{_statsPose.Z}") : "-";
        return string.Create(c, $"pos {where}   look {facing} {bearing:0}   pitch {Mathf.RadToDeg(_rig.Pitch):0}");
    }

    private IEnumerable<string> ConnectionLines()
    {
        CultureInfo c = CultureInfo.InvariantCulture;
        yield return string.Create(c, $"{_feed.Description}   tick {_feed.Tick}   render {_session.Clock.RenderTick:0.0} (lag {_session.Clock.LagTicks:0.0})");
        yield return string.Create(c, $"ping {_feed.RoundTripMs} ms   fps {Engine.GetFramesPerSecond():0}   entities {_feed.Count} (mobs {_entities.MobCount})");
        yield return string.Create(c, $"net in {Bytes(_receivedPerSecond)}/s   out {Bytes(_sentPerSecond)}/s");
        (double ackLast, double ackMean, int ackN) = _session.Probe.Ack;
        (double motionLast, double motionMean, int motionN) = _session.Probe.Motion;
        yield return string.Create(c, $"input->ack {ackLast:0} ms (mean {ackMean:0}, n={ackN})   key->motion {motionLast:0} ms (mean {motionMean:0}, n={motionN}){(_session.Probe.Measuring ? " ..." : string.Empty)}");
        yield return string.Create(c, $"seq sent {_feed.LastSentSeq} acked {_feed.LastAppliedSeq}");
    }

    // the owner (2026-10-28): the prediction in brief in the debug HUD's (F3) corner (the dev menu's Stats tab has it all, PredictionLines)
    private string PredictionLine(CultureInfo c)
    {
        OwnPrediction p = _session.Prediction;
        if (!p.Active)
            return "pred -";
        (float vx, float vy) = p.VisualOffset;
        return string.Create(c, $"pred p: {p.Pending} c: {p.Counters.Corrections} o: {Math.Sqrt(vx * vx + vy * vy) * 2:0.00}m");
    }

    private IEnumerable<string> PredictionLines()
    {
        CultureInfo c = CultureInfo.InvariantCulture;
        OwnPrediction p = _session.Prediction;
        if (p.Active)
        {
            PredictionCounters pc = p.Counters;
            (float vx, float vy) = p.VisualOffset;
            yield return string.Create(c, $"reconciled {pc.Reconciliations}  corrections {pc.Corrections}  carried {pc.Carried}  snaps {pc.Snaps}  mismatches {pc.Mismatches}  max err {pc.MaxErrorTiles * 2f:0.00} m  offset {Math.Sqrt(vx * vx + vy * vy) * 2:0.00} m");
            yield return string.Create(c, $"frames sent {p.FramesSent}  pending {p.Pending}  server buffer {p.BufferDepth}  clock x{p.ClockRate:0.000}  speed {p.Params.TilesPerTick * 40f:0.0} m/s{(p.Params.CanMove ? string.Empty : "  (cannot move)")}  {p.Predicted.Posture}");
        }
        else
            yield return "waiting for the server's state";
        yield return string.Create(c, $"others drawn: p99 {_session.OthersSpeed.P99:0.0} m/s  peak {_session.OthersSpeed.Max:0.0} m/s");
    }

    private IEnumerable<string> PlayerLines()
    {
        CultureInfo c = CultureInfo.InvariantCulture;
        RenderPose pose = _statsPose;
        if (_statsHasOwn)
            yield return string.Create(c, $"you #{_feed.OwnEntity} at {pose.X:0.00},{pose.Y:0.00} z{pose.Z}{(_targetUnderCrosshair != 0 ? $"   crosshair: #{_targetUnderCrosshair}" : string.Empty)}");
        else
            yield return "you: no body yet";
        yield return string.Create(c, $"camera {_rig.Mode}   yaw {Mathf.RadToDeg(_rig.Yaw):0}   eye {WorldScale.EyeHeight:0.##} m");
    }

    /// <summary>Names the camera mode just switched to at the top of the screen for a moment.</summary>
    private void AnnounceCamera()
    {
        _cameraHint.Text = _rig.Mode switch
        {
            CameraMode.Free => "Free camera",
            CameraMode.Orbit => _rig.OrbitFreeLook ? "Third person, free look" : "Third person",
            _ => "First person",
        };
        // the owner (2026-11-01): sized with the HUD's text, and never small on a small screen
        int size = Math.Max(30, Mathf.RoundToInt(18 * (App.Instance.HudTextScale ?? 1f)));
        _cameraHint.AddThemeFontSizeOverride("font_size", size);
        _cameraHint.AddThemeConstantOverride("outline_size", size / 4);
        _cameraHintTween?.Kill();
        _cameraHint.Modulate = new Color(1, 1, 1, _options.HudTextOpacity);
        _cameraHintTween = CreateTween();
        _cameraHintTween.TweenInterval(1.0);
        _cameraHintTween.TweenProperty(_cameraHint, "modulate:a", 0f, 0.5);
    }

    private static string Bytes(double value) => value >= 1024 ? string.Create(CultureInfo.InvariantCulture, $"{value / 1024:0.0} KB") : string.Create(CultureInfo.InvariantCulture, $"{value:0} B");

    private void OnServerText(ServerText text)
    {
        if (TakeMobInfo(text))
            return; // D140: the dev menu's Player tab
        if (text.Type == BalloonModel.TextType)
        {
            // tg's balloon_alert: under the crosshair (D128), not in the chat.
            _balloon.Add(HtmlText.ToPlain(text.Html), App.NowSeconds);
            return;
        }
        ChatEntry entry = _chatLog.Add(text.Type, text.Html, App.NowSeconds); // M9 slice C: the chat window's log holds every line, as tg's chat does
        ChatFeed.Line feedLine = null;
        TextDestination where = _consoleModel.OnServerText(text, App.NowSeconds, out string plain);
        if (where == TextDestination.Analyzer)
            _readout.Show(plain);
        else if (_readout.Offer(text.Html))
            _chat.Add(ReadoutModel.Headline(plain), App.NowSeconds); // D184: tg's boxed message (a scan) opens whole in the readout panel
        else
        {
            // version 24 (D154): tg's links (the self-check's embedded objects) stay clickable in the chat; M9: in tg's chat colours
            var links = new List<ChatLink>();
            var spans = new List<ChatSpan>();
            string styled = HtmlText.ToPlain(text.Html, links, spans);
            feedLine = _chat.Add(styled, App.NowSeconds, links, spans);
        }
        TranslateChat(entry, feedLine); // D319
        _lastServerText = plain.Replace('\n', ' ');
        if (_lastServerText.Length > 120)
            _lastServerText = _lastServerText.Substring(0, 120);
        GD.Print($"[anomaly] server: {_lastServerText}");
    }

    private string Summary()
    {
        double seconds = App.NowSeconds - _startedAt;
        CultureInfo c = CultureInfo.InvariantCulture;
        (double ackLast, double ackMean, int ackN) = _session.Probe.Ack;
        (double motionLast, double motionMean, int motionN) = _session.Probe.Motion;
        PredictionCounters pc = _session.Prediction.Counters;
        // Summary runs once, when the program quits: the audio line goes with it (the smoke reads it).
        if (_sound != null)
            GD.Print(_sound.SmokeLine());
        return string.Create(c, $"{_feed?.Description} phase={_feed?.Phase} seconds={seconds:0.0} frames={_frames} tick={_feed?.Tick} deltas={_feed?.DeltaCount} entities={_feed?.Count} others_seen={_session?.OthersSeen} others_moved={_session?.OthersMoved} peak_speed={_session?.OthersSpeed.Max:0.0}m/s p99_speed={_session?.OthersSpeed.P99:0.0}m/s own_p99_speed={_session?.OwnSpeed.P99:0.0}m/s own_seen={_ownSeen} inputs={_feed?.LastSentSeq} acked={_feed?.LastAppliedSeq} ack_ms={ackMean:0}/{ackN} motion_ms={motionMean:0}/{motionN} reconciled={pc.Reconciliations} corrections={pc.Corrections} snaps={pc.Snaps} mismatches={pc.Mismatches} max_err_m={pc.MaxErrorTiles * 2:0.00} hud_items={_hudLayer?.ItemCount} chunks={_map?.ChunkCount} props={_map?.PropCount} pieces={_map?.PieceCount} handed_over={_map?.HandedOver} waited_textures={_map?.WaitedForTextures} atlas={_map?.AtlasEntries} map_load_s={_map?.LoadSeconds:0.00} quality={_appliedQuality} interaction_ms={(_frames > 0 ? _interactionTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency / _frames : 0):0.000}/{_interactionMax * 1000.0 / System.Diagnostics.Stopwatch.Frequency:0.00}");
    }

    // ---- options ----

    internal static PlayOptions LoadOptions(ClientArgs args)
    {
        PlayOptions options;
        if (args.Automated)
        {
            // A scripted run does not read or change the player's own file: the same run on every machine.
            options = new PlayOptions();
        }
        else
        {
            var errors = new List<string>();
            string path = FileAccess.FileExists(OptionsPath) ? OptionsPath : DefaultOptionsPath;
            if (path == DefaultOptionsPath)
                GD.Print("[anomaly] options: a new player, starting from the shipped settings");
            string text = FileAccess.FileExists(path) ? FileAccess.GetFileAsString(path) : null;
            options = PlayOptions.Parse(text, errors);
            foreach (string error in errors)
                GD.PrintErr($"[anomaly] options file: {error}");
        }
        foreach ((string name, string value) in args.Options)
            options.TrySet(name, value, out _);
        foreach ((string action, string key) in args.Binds)
            options.TrySetBinding(action, key, out _);
        return options;
    }

    private void SaveOptions()
    {
        if (App.Instance.Args.Automated || EditorOn) // the map editor's look is its own: it never writes the game's options
            return;
        using FileAccess file = FileAccess.Open(OptionsPath, FileAccess.ModeFlags.Write);
        if (file == null)
        {
            GD.PrintErr($"[anomaly] options: could not save {OptionsPath}: {FileAccess.GetOpenError()}");
            return;
        }
        file.StoreString(_options.ToText());
    }

    private static bool IsLoopback(string host)
        => host == "localhost" || host == "::1" || host == "127.0.0.1" || (host != null && host.StartsWith("127.", StringComparison.Ordinal));

    private static int StableSeed(string name)
    {
        int hash = 17;
        foreach (char ch in name)
            hash = unchecked(hash * 31 + ch);
        return hash & 0x7FFFFFFF;
    }

    // D346: the own body's eye by its standing picture (BodyEyes), measured while it stands (a lying picture is turned), and what is over
    // its cell (a body taller than the walls stays under a ceiling); null for a human.
    private long _bodyTopOf;
    private float _bodyTop;

    private float? OwnBodyEye(long own, bool notBody, RenderPose pose)
    {
        if (notBody || own == 0 || _feed.Replica == null || !_feed.Replica.TryGet(own, out ReplicaEntity body) || body.IsExosuit())
            return null;
        if (own != _bodyTopOf)
        {
            _bodyTopOf = own;
            _bodyTop = 0f;
        }
        if ((body.State.Posture & PostureBits.Lying) == 0 && _entities.TryGetPictureTop(own, out float top))
            _bodyTop = top;
        bool covered = BodyEyes.Covered(_feed.Replica, (int)Math.Round(pose.X), (int)Math.Round(pose.Y), pose.Z);
        return BodyEyes.For(body.TypePath, _bodyTop, covered);
    }
}
