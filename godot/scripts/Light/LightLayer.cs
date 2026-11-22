using System.Linq;
using Godot;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Light;
using Anomaly.Client.Shared.World;
using Anomaly.Protocol;

namespace Anomaly.Client.Light;

// Owner: the contract (these public members and their meaning) is the M3 contracts step's and frozen
// (docs/ai/specs/m3-ownership.md, docs/ai/specs/light-grid.md section 7); M3 P7 implements it. M3 P6's GameScene creates it,
// attaches it and forwards the preset and the dev flashlight key.

/// <summary>
/// The client's light (D24, D114, D288). D288 (<see cref="Dynamic"/>, the default): every lit source near the camera is a Godot light in tg's
/// colour, range and power, the nearest of them with shadows (the preset's budget), switched off the moment its source is; the sun is a
/// shadowed directional light; the stars and the lava planet light what is open to the sky (<see cref="SkyLight"/>). The light grid computed
/// from the replica's light state (tg's corner math) stays as the dev comparison (<see cref="Dynamic"/> off): its texture and global uniforms,
/// with the few shadowed Godot lights near the camera of D114 on top. Both draw the flashlights' spots and the dev camera flashlight.
/// </summary>
public partial class LightLayer : Node3D
{
    /// <summary>D288: the render layers of the 3D things (models, pieces) and of the characters' pictures, which only the nearest lamps' shadows draw.</summary>
    public const uint ThingLayer = 1u << 10, CharacterLayer = 1u << 11;

    /// <summary>The render layer of the own held things in first person (Hud/ViewModel): the own beam starts beside them and does not light them.</summary>
    public const uint HeldLayer = 1u << 12;

    /// <summary>D288: the render layer of the shut doors' blocks as the lamps' shadows draw them (<see cref="PlaceDoorBlocks"/>); the flashlights' draw theirs on <see cref="BeamBlockLayer"/>, the sun's neither.</summary>
    public const uint BlockLayer = 1u << 13;

    /// <summary>D288: the render layer of the shut doors' blocks as the flashlights' shadows draw them (<see cref="PlaceDoorBlocks"/>).</summary>
    public const uint BeamBlockLayer = 1u << 15;

    /// <summary>D288: the render layer of the own body's shadow-only picture in first person (<c>Billboard.ShadowOnly</c>): the lamps with object shadows hold it as a character, the own flashlight does not (it is held before the body).</summary>
    public const uint OwnBodyLayer = 1u << 14;

    /// <summary>The render layer of what only a mirror's camera draws (Render/MirrorPass.cs): the own body's picture in first person, which the main camera leaves out.</summary>
    public const uint MirrorOnlyLayer = 1u << 16;

    /// <summary>The render layer of what a mirror's camera leaves out: the quads that fill the main view (the outlines, the ambient occlusion, the sight's shade, the sun's flare) and the mirrors' own surfaces.</summary>
    public const uint MainViewLayer = 1u << 17;

    /// <summary>
    /// The render layer of a mob's or an item's own picture (Billboard.Reflects), held with it while it is drawn as itself (not the own
    /// hidden body, which never draws as itself): a mirror's camera leaves this out too, so only its reflection twin (MirrorOnlyLayer, its
    /// own mirrored facing) is seen there, never both at once.
    /// </summary>
    public const uint MirrorHiddenLayer = 1u << 18;

    /// <summary>The dim ambient every lit material gets (<c>anomaly_light_ambient</c>).</summary>
    public static readonly Vector3 DefaultAmbient = new(0.035f, 0.035f, 0.045f);

    private const double SelectEvery = 0.2;
    private const double MoveCheckEvery = 0.2;
    private const float FadeSeconds = 0.35f;
    private const float ShadowEnergyScale = 0.5f;
    private const float DevSpotRange = 20f;
    private const float DevSpotEnergy = 4f;
    // D288: the own beam starts at the hand held low (camera space, metres: right, down, ahead), so its shadows fall beside what it
    // lights, not behind it as from the eye; it is aimed at the crosshair OwnBeamAim metres ahead
    private static readonly Vector3 OwnBeamHand = new(0.35f, -0.4f, -0.15f);
    private const float OwnBeamAim = 8f;
    private const float BeamMaxTiles = 24f;     // another flashlight's spot is drawn this close to the camera (the grid lights farther)
    // Soft edges on surfaces without scattering a visible beam through the air.
    private const float BeamAngleAttenuation = 2f;
    private const float LampFogEnergy = 0.2f;   // a shadowed lamp's faint halo in it
    // D288 (the owner's play tests, 2026-10-29): metres a surface may stand behind what shades it from a lamp and still be lit, Godot's 0.1. A 2 cm
    // bias striped a wall a lamp lights at a slant (shadow acne), even close up with the normal bias moving the lookup three shadow texels off a
    // slanted face; the far side of a shut door's 16 cm leaf, which 0.1 lit, is shaded by the door's block, its face this far toward each light
    private const float LampShadowBias = 0.1f;
    private const float LampShadowNormalBias = 3f;
    private const float DoorBlockSeal = 0.1f;    // metres a shut door's block reaches into the walls beside it, the floor and the ceiling
    // metres a shut door's block stands toward a flashlight: under the spots' shadow bias (Godot's 0.03), so the window insert 2.7 cm before the
    // door's plane stays lit in the beam (the lamps' LampShadowBias left it black)
    private const float BeamBlockFace = 0.01f;
    // D288: the lamps as Godot lights
    private const float LampEnergy = 3f;         // energy per unit of tg's light_power at its peak (the owner, 2026-10-29: 3, was 3.6 tuned on MetaStation's medbay)
    // a fixture's light sits under the fixture (D119's 0.925 of the wall), so the ceiling over it is lit as the grid lit it
    private const float LampLightShare = 0.7f;
    private const float MaxLampOffset = 0.375f;  // tiles: how far tg's visual offset moves a lamp from its tile's centre (a quarter metre short of the wall)
    // Godot's omni falloff (1 - (d/r)^4)^2 * d^-0.25 over tg's range follows tg's linear one (falloff_at_coord) within a few per cent
    private const float LampAttenuation = 0.25f;
    private const float SmallRange = 2.5f;       // tiles: a light this small is drawn only near (SmallLightTiles) and its shadow holds no things
    private const float SmallLightTiles = 10f;   // a small light (a screen, an APC's lamp) is drawn only this near: farther it lights next to nothing
    private const float FloorLightHeight = 0.25f; // metres: a light tg puts on the floor (light_height below 0.5)
    private const float FadeMetres = 6f;         // a lamp fades out over the last metres of the light distance (Godot's distance fade)
    private const float ShadowKeepTiles = 3f;    // a lamp with a shadow keeps it until another is this much nearer, so shadows do not flap (a new one is drawn whole)
    private const float FireLampBoost = 2.5f;    // D302: a group's one lamp stands in for up to nine
    private const int MaxFireShadows = 2;      // D302: a burning room's lamps leave the rest of the budget to the other lights
    private readonly Dictionary<long, long> _fireLamps = new(); // D302: a group of burning tiles to the hotspot that lights it (its lowest id)
    private const int MaxNewShadows = 2;        // lamps that gain a shadow in one selection (each draws its cube anew; a turn gave several at once)
    private const int OutOfView = 3;             // added to a light's priority when its light does not reach the view: it takes a shadow after every one that does
    private const float ViewMargin = 2f;         // metres added to a light's range in that test, so a small turn finds its shadow there
    private const float TurnReselectCos = 0.94f; // the view turned 20 degrees since the last selection: the next is now
    private const float OutOfViewKey = 1e6f;     // added to an order's distance key for a light out of view
    private static readonly Color SunColour = new(1f, 0.96f, 0.88f);
    private const float SunEnergy = 0.7f;        // the owner (2026-10-29): 0.7 (was 1.4)
    private const float DefaultFill = 0.05f;     // the grid's share as the lamps' bounce (the owner, 2026-10-29: 0.05, was 0.3)

    private sealed class Lamp
    {
        public OmniLight3D Light;
        public float Fade, Target, Shadow, ShadowTarget, Energy;
        public bool Carried; // held by a mob: placed at its drawn pose every frame
        public bool Small;   // range below SmallRange: drawn only within SmallLightTiles
    }

    private sealed class Level
    {
        public LightGrid Grid;
        public byte[] Seen;     // the flags (opaque, starlit) last applied, to tell a change from a touch
    }

    private sealed class Slot
    {
        public OmniLight3D Light;
        public long Key = long.MinValue;
        public float Fade;
        public float Target;
        public float Energy;
    }

    private IWorldFeed _feed;
    private ReplicaWorld _replica;
    private Camera3D _camera;
    private ILightCells _cells;
    private int _levels;
    private readonly Dictionary<int, Level> _byZ = new();
    private readonly Dictionary<long, (int X, int Y, int Z, LightState S)> _lit = new();
    private Level _active;
    // D325: the camera's multi-z stack (its lowest and highest level): the textures hold every level of it, a block of rows each
    private int _stackLow, _stackHigh;
    private byte[] _stackTexels, _cellStackTexels, _exteriorStackTexels;
    private ImageTexture _texture;
    private int _textureWidth, _textureHeight;
    private bool _globalsDirty = true;
    private bool _failed;
    private ImageTexture _cellTexture; // DoorCellLight's codes of the active level (anomaly_light_cells)
    private byte[] _cellTexels;
    private bool _cellsDirty = true;
    private readonly Dictionary<(int X, int Y), MultiMeshInstance3D> _doorBlocks = new(); // the shut doors' cells as the lamps' shadows see them, by chunk, each sharing its instances with the flashlights' (PlaceDoorBlocks)

    private int _budget = 4;
    private int _objectShadows = 1; // RenderPreset.ObjectShadows
    private int _objectShadowLamps = 4; // RenderPreset.ObjectShadowLamps: the shadowed lamps nearest the camera whose shadows hold the things and the characters
    private readonly List<(float Distance, long Id)> _thingOrder = new();
    private readonly Plane[] _frustum = new Plane[6]; // the camera's at the last selection (world space, normals out)
    private Vector3 _selectedLook;
    private readonly List<Slot> _slots = new();
    private readonly List<ShadowCandidate> _candidates = new();
    private readonly List<ShadowCandidate> _chosen = new();
    private readonly List<(float, ShadowCandidate)> _scratch = new();
    private double _selectIn, _moveIn;
    private SpotLight3D _flashlight;
    private bool _flashlightOn;
    private bool _ownFlashlight; // the player's own directional overlay light (a flashlight in a hand) is on
    private readonly Dictionary<long, SpotLight3D> _beams = new(); // the other flashlights' spots (DV62), by entity
    private readonly List<long> _beamGone = new();
    private readonly List<(float, long)> _beamOrder = new();
    private int _beamShadows;
    private int _beamsLogged;

    private bool _dynamic = true;
    private int _lightDistance = 18;
    private bool _cubeShadows = true;
    private SunShadowPlan _sunPlan = SunShadowPlan.For(2);
    private readonly Dictionary<long, Lamp> _lamps = new();
    private readonly HashSet<long> _instant = new(); // sources switched on since the last selection: their lamps appear at once
    private readonly List<(int Priority, float Distance, long Id)> _lampOrder = new();
    private int _effectShadows; // the effects' shadows (ShadowedLightSelector.Priority 0), which come before the other flashlights' spots
    private readonly List<long> _lampGone = new();
    private DirectionalLight3D _sun;
    private Vector3 _sunShown;
    private float _sunCheckIn, _sunFade;
    private bool _sunWanted = true;
    private const float SunCheckEvery = 0.5f, SunFadeSeconds = 0.5f;
    private byte[] _zeroTexels;
    private ImageTexture _exteriorTexture;
    private byte[] _exteriorTexels;
    // each level's sky mask (SkyLight), kept while nothing on the stack changes: it does not depend on the level one stands on
    private readonly Dictionary<int, byte[]> _skyByZ = new();
    private bool _exteriorDirty = true, _exteriorFull = true;
    private int _exteriorLevel;
    private float _skyGain = 0.2f; // the owner (2026-10-29): 0.2 (was 1)

    /// <summary>The drawn poses of the entities (the session's), so another holder's beam follows its drawn body; null: the replica's tiles.</summary>
    public IPoseSource Poses { get; set; }

    /// <summary>The gases' optics of a cell (DV62, <c>GasFog.At</c>): a beam through gas is tinted and shortened; null: no gas.</summary>
    public Func<int, int, int, CellOptics> GasAt { get; set; }

    /// <summary>The face of the door standing at a cell (<see cref="DoorAxis"/>: NORTH, EAST, 0 for none; the map renderer's), so a closed door's cell is lit by its sides (<see cref="DoorCellLight"/>); null: no doors.</summary>
    public Func<int, int, int, byte> DoorFaceAt { get; set; }

    private LightState _ownLight;

    /// <summary>The number of other flashlights' spots drawn now (DV62).</summary>
    public int BeamCount => _beams.Count;

    /// <summary>The dim ambient (<c>anomaly_light_ambient</c>); set before or after <see cref="Attach"/>.</summary>
    public Vector3 Ambient { get; set; } = DefaultAmbient;

    /// <summary>Dev: every surface is lit as if by full daylight (the ambient light at 1), whatever the light grid says.</summary>
    public bool Fullbright
    {
        get => _fullbright;
        set
        {
            if (_fullbright == value)
                return;
            _fullbright = value;
            // the uniforms are pushed only when the grid changes: a switch would wait for the next light change
            RenderingServer.GlobalShaderParameterSet(LightGridUniforms.Ambient, value ? Vector3.One : Ambient);
        }
    }

    private bool _fullbright;

    /// <summary>The scale of the grid term (<c>anomaly_light_strength</c>; 0 turns the grid off for debugging).</summary>
    public float Strength { get; set; } = 0.75f;

    /// <summary>The most light a surface gets after <see cref="Strength"/> (<c>anomaly_light_cap</c>; 0 for no cap).</summary>
    public float Cap { get; set; } = 1.3f; // the owner (2026-10-28): 1.3 (was 1.5)

    /// <summary>
    /// D288: the sources are drawn as Godot lights, the sun shines and the sky lights what is open to it (true, the default); false: the old
    /// light grid with D114's few shadowed lights on top (a dev comparison until the owner accepts the new light).
    /// </summary>
    public bool Dynamic
    {
        get => _dynamic;
        set
        {
            if (_dynamic == value)
                return;
            _dynamic = value;
            foreach (long id in _lamps.Keys.ToList())
                RemoveLamp(id);
            foreach (Slot slot in _slots)
                slot.Light.QueueFree();
            _slots.Clear();
            _effectShadows = 0;
            _globalsDirty = true; // the grid's texture or none
            _selectIn = 0;
            ApplySky();
            if (_replica != null)
                foreach (long id in _lit.Where(l => FlashlightBeam.IsBeam(l.Value.S)).Select(l => l.Key).ToList())
                    if (_replica.TryGet(id, out ReplicaEntity e))
                        ApplyEntityLight(e); // in the grid again or out of it
        }
    }

    /// <summary>Where the sun is (a unit vector toward it, the world's frame: <c>Starfield.SunDirection</c>); the scene sets it every frame.</summary>
    public Vector3 SunDirection { get; set; } = Vector3.Up;

    /// <summary>D305: the camera's level has tg's ZTRAIT_NOPARALLAX (Lavaland): no sun and no light of space, only <see cref="SkyLight.AshCube"/>.</summary>
    public bool Ashen
    {
        get => _ashen;
        set
        {
            if (_ashen == value) return;
            _ashen = value;
            ApplySky();
        }
    }

    private bool _ashen;

    /// <summary>D288: the strength of the light of space (the stars and the planet, <see cref="SkyLight.Cube"/>), 0.2 by default; a dev slider.</summary>
    public float SkyGain
    {
        get => _skyGain;
        set
        {
            _skyGain = value;
            ApplySky();
        }
    }

    /// <summary>D288: the share of the grid that lights as the Godot lights' bounce (0.05 by default; a dev slider, 0 for none).</summary>
    public float FillShare
    {
        get => _fillShare;
        set
        {
            _fillShare = Math.Max(0f, value);
            _globalsDirty = true;
        }
    }

    private float _fillShare = DefaultFill;

    /// <summary>Protocol version 31 (DV81): tg's lighting cutoff (mesons, night vision), the least light a surface gets (<c>anomaly_light_floor</c>).</summary>
    public Vector3 VisionFloor
    {
        get => _visionFloor;
        set
        {
            _visionFloor = value;
            PushVisionFloor();
        }
    }

    private Vector3 _visionFloor;

    /// <summary>The sun's energy (a dev slider).</summary>
    public float SunStrength { get; set; } = SunEnergy;

    /// <summary>The strength of the sun's light when nothing sets another (a map's own: protocol 87).</summary>
    public const float DefaultSunStrength = SunEnergy;

    /// <summary>The colour of the sun's light: the game's, or a map's own (protocol 87).</summary>
    public Color SunTint
    {
        get => _sunTint;
        set
        {
            _sunTint = value;
            if (_sun != null && IsInstanceValid(_sun))
                _sun.LightColor = value;
        }
    }

    private Color _sunTint = SunColour;

    /// <summary>The game's own colour of the sun's light.</summary>
    public static Color DefaultSunTint => SunColour;

    /// <summary>The lamps' energy per unit of tg's light power (a dev slider).</summary>
    public float LampStrength
    {
        get => _lampStrength;
        set
        {
            _lampStrength = value;
            foreach ((long id, Lamp lamp) in _lamps)
                if (_lit.ContainsKey(id))
                    StyleLamp(id, lamp);
        }
    }

    private float _lampStrength = LampEnergy;

    /// <summary>D288: the Godot lights drawing sources now (on or fading).</summary>
    public int LampCount => _lamps.Count;

    /// <summary>Starts following <paramref name="feed"/>'s replica (its v3 cells and lights) for <paramref name="camera"/>; call once the feed is playing.</summary>
    public void Attach(IWorldFeed feed, Camera3D camera)
    {
        Detach();
        _feed = feed;
        _camera = camera;
        try
        {
            _replica = feed.Replica;
            MapGrid map = feed.Map;
            _levels = map.Levels;
            _cells = new ReplicaLightCells(_replica, map.Width, map.Height);
            _replica.LightChanged += OnLightChanged;
            _replica.StarlightChanged += OnStarlightChanged;
            _replica.CellsChanged += OnCellsChanged;
            feed.Despawned += OnDespawned;
            foreach (ReplicaEntity e in feed.Entities)
                TrackEntity(e);
            _failed = false;
        }
        catch (NotImplementedException ex)
        {
            // The replica's version-3 members are protocol P5's; until they exist the layer stays off and the scene runs without a grid.
            GD.PushWarning("LightLayer: no light grid, " + ex.Message);
            _failed = true;
        }

        EnsureFlashlight();
        EnsureSun();
        _globalsDirty = true;
    }

    /// <summary>
    /// Starts the layer over plain cells and no replica (the dev light room, tests): the caller adds sources with
    /// <see cref="SetSource"/> and reports opacity changes with <see cref="CellOpacityChanged"/>.
    /// </summary>
    public void AttachCells(ILightCells cells, int levels, Camera3D camera)
    {
        Detach();
        _cells = cells;
        _levels = levels;
        _camera = camera;
        _failed = false;
        EnsureFlashlight();
        EnsureSun();
        _globalsDirty = true;
    }

    /// <summary>Adds, changes or removes a source (see <see cref="LightGrid.SetSource"/>) on level <paramref name="z"/>.</summary>
    public void SetSource(long key, int x, int y, int z, in LightState state)
    {
        if (_cells == null)
            return;
        LevelFor(z).Grid.SetSource(key, x, y, GridLight(FlashlightBeam.Presented(state)));
        bool live = !state.IsNone && state.On && state.Power != 0f && TgLightMath.OnGrid(state.System);
        if (live && key > 0)
            _lit[key] = (x, y, z, state);
        else
            _lit.Remove(key);
        LampChanged(key);
    }

    /// <summary>An opaque cell changed on level <paramref name="z"/>: recomputes the sources that see it.</summary>
    public void CellOpacityChanged(int x, int y, int z)
    {
        if (_byZ.TryGetValue(z, out Level level))
            level.Grid.CellOpacityChanged(x, y);
        _cellsDirty = true;
        _exteriorDirty = _exteriorFull = true;
        _skyByZ.Clear();
    }

    /// <summary>The preset's lamps (D288): how far they are drawn, how many cast shadows (the grid's D114 lights take the same budget), their shadow mode and the sun's shadow.</summary>
    public void SetPreset(RenderPreset preset)
    {
        _budget = Math.Max(0, preset.ShadowedLights);
        _lightDistance = Math.Max(1, preset.LightDistance);
        _cubeShadows = preset.CubeShadows;
        _sunPlan = SunShadowPlan.For(preset.SunShadow);
        _objectShadows = preset.ObjectShadows;
        _objectShadowLamps = Math.Max(0, preset.ObjectShadowLamps);
        for (int i = _slots.Count - 1; i >= _budget; i--)
        {
            _slots[i].Light.QueueFree();
            _slots.RemoveAt(i);
        }

        foreach (Lamp lamp in _lamps.Values)
            ApplyLampPreset(lamp);
        ApplySunPreset();
        _selectIn = 0;
    }

    /// <summary>Turns the dev camera flashlight on or off (a client dev toggle; the flashlight item lights it too, <see cref="OwnFlashlightOn"/>).</summary>
    public void ToggleFlashlight()
    {
        _flashlightOn = !_flashlightOn;
        ShowFlashlight();
    }

    /// <summary>The grid of level <paramref name="z"/> if it was built (for the dev HUD and tests).</summary>
    public LightGrid GridOf(int z) => _byZ.TryGetValue(z, out Level l) ? l.Grid : null;

    /// <summary>Whether the dev flashlight is on.</summary>
    public bool FlashlightOn => _flashlightOn;

    /// <summary>Whether the player's own flashlight (a directional overlay light it carries, DV61) lights the camera's spot.</summary>
    public bool OwnFlashlightOn => _ownFlashlight;

    /// <summary>The number of shadowed lights that are on or fading now.</summary>
    public int ShadowedLightCount => _dynamic ? _lamps.Values.Count(l => l.ShadowTarget > 0f) : _slots.Count(s => s.Key != long.MinValue);

    public override void _ExitTree() => Detach();

    /// <summary>A running average (microseconds) of what <see cref="_Process"/> costs a frame: the flush, the texture upload when something changed, the selection and the fades (for the dev HUD and the measurements).</summary>
    public double ProcessMicros { get; private set; }

    public override void _Process(double delta)
    {
        using var frameProbe = Anomaly.Client.Dev.FrameProbe.Scope("LightLayer._Process");
        if (_cells == null || _failed || _camera == null || !IsInstanceValid(_camera))
            return;
        long started = System.Diagnostics.Stopwatch.GetTimestamp();
        ProcessInner(delta);
        double us = (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1e6 / System.Diagnostics.Stopwatch.Frequency;
        ProcessMicros = ProcessMicros == 0 ? us : ProcessMicros * 0.95 + us * 0.05;
    }

    private void ProcessInner(double delta)
    {

        int z = ViewLevel > 0 ? ViewLevel : 1 + (int)MathF.Floor(_camera.GlobalPosition.Y / TileSpace.LevelHeight);
        z = Math.Clamp(z, 1, Math.Max(1, _levels));
        Level level = LevelFor(z);
        (int low, int high) = StackOf(z);
        if (!ReferenceEquals(level, _active) || low != _stackLow || high != _stackHigh)
        {
            bool newStack = low != _stackLow || high != _stackHigh;
            _active = level;
            (_stackLow, _stackHigh) = (low, high);
            _globalsDirty = true;
            _cellsDirty = true;
            _exteriorDirty = true;
            _exteriorLevel = z;
            // another level of the same stack (stairs, a ladder): every level's grid and sky mask is already current (each stack level is
            // flushed every frame), only the uploads follow the new level; a new stack's levels are written whole
            if (newStack)
            {
                _exteriorFull = true;
                for (int s = low; s <= high; s++)
                    LevelFor(s).Grid.MarkAllDirty();     // a full texture upload
            }
        }

        if (_cellsDirty)
            using (Anomaly.Client.Dev.FrameProbe.Scope("light/cells"))
                UploadCells(level, z);
        if (_exteriorDirty)
            using (Anomaly.Client.Dev.FrameProbe.Scope("light/exterior"))
                UploadExterior(z);

        // D288: while the Godot lights draw the sources the grid is their bounce (FillShare of it), or nothing at a share of 0
        bool changed = false;
        using (Anomaly.Client.Dev.FrameProbe.Scope("light/flush"))
            for (int s = low; s <= high; s++)
                changed |= LevelFor(s).Grid.Flush(out _, out _, out _, out _);
        if (_globalsDirty || (changed && (!_dynamic || _fillShare > 0f)))
            using (Anomaly.Client.Dev.FrameProbe.Scope("light/texture"))
                UploadTexture(level, z);

        _moveIn -= delta;
        if (_moveIn <= 0)
        {
            _moveIn = MoveCheckEvery;
            CheckMovedSources();
            UpdateOwnFlashlight();
        }

        // a turn of the view brings the selection forward: the lights it turned to take their shadows at once
        if (_dynamic && (-_camera.GlobalTransform.Basis.Z).Dot(_selectedLook) < TurnReselectCos)
            _selectIn = 0;
        _selectIn -= delta;
        if (_selectIn <= 0)
        {
            _selectIn = SelectEvery;
            using (Anomaly.Client.Dev.FrameProbe.Scope("light/select"))
                if (_dynamic)
                    SelectLamps(z);
                else
                    SelectShadowed(z);
        }

        if (_dynamic)
        {
            FadeLamps((float)delta);
            PlaceCarriedLamps();
        }
        else
        {
            FadeShadowed((float)delta);
        }
        using (Anomaly.Client.Dev.FrameProbe.Scope("light/sun"))
            UpdateSun((float)delta);
        using (Anomaly.Client.Dev.FrameProbe.Scope("light/beams"))
            UpdateBeams(z);
        if (_flashlight != null && _flashlight.Visible)
            SwayOwnBeam((float)delta);
        if (_ownFlashlight && _flashlight != null)
        {
            // the own beam goes where the camera looks
            Vector3 forward = -_camera.GlobalTransform.Basis.Z;
            var flat = new Vector2(forward.X, -forward.Z);
            if (flat.LengthSquared() > 1e-6f)
            {
                flat = flat.Normalized();
                Vector3 at = _camera.GlobalPosition;
                Tint(_flashlight, _ownLight, at.X / TileSpace.TileSize + 0.5f, -at.Z / TileSpace.TileSize + 0.5f, z, flat.X, flat.Y);
            }
        }
    }

    // ---- the grid ----

    /// <summary>D325: the multi-z stack of level <paramref name="z"/> (the replica's links; the level alone off multi-z maps).</summary>
    private (int Low, int High) StackOf(int z)
    {
        int low = z, high = z;
        if (_replica == null)
            return (low, high);
        while (low > 1 && _replica.IsLinkedBelow(low))
            low--;
        while (high < _levels && _replica.IsLinkedBelow(high + 1))
            high++;
        return (low, high);
    }

    // D325: the texels of every level of the stack, one block of rows each, lowest first (the level's own array when the stack is one level)
    private byte[] Stacked(ref byte[] into, byte[] one, Func<int, byte[]> levelTexels)
    {
        int n = _stackHigh - _stackLow + 1;
        if (n <= 1)
            return one;
        if (into == null || into.Length != one.Length * n)
            into = new byte[one.Length * n];
        for (int s = _stackLow; s <= _stackHigh; s++)
            Array.Copy(levelTexels(s), 0, into, (s - _stackLow) * one.Length, one.Length);
        return into;
    }

    private int StackCount => Math.Max(1, _stackHigh - _stackLow + 1);

    private bool InStack(int z) => z >= _stackLow && z <= _stackHigh;

    private Level LevelFor(int z)
    {
        if (_byZ.TryGetValue(z, out Level level))
            return level;
        level = new Level { Grid = new LightGrid(_cells, z), Seen = new byte[(_cells.MaxX + 1) * (_cells.MaxY + 1)] };
        _byZ[z] = level;
        if (_replica != null)
            FillLevel(level, z);
        return level;
    }

    // The first fill of a level: its lit entities and its starlit cells (the replica's flags).
    private void FillLevel(Level level, int z)
    {
        foreach (ReplicaEntity e in _feed.Entities)
        {
            if (e.State.Z == z)
                level.Grid.SetSource(e.Id, e.State.X, e.State.Y, GridLight(Presented(e)));
        }

        LightState star = _replica.Starlight;
        int w = _cells.MaxX + 1;
        for (int y = 1; y <= _cells.MaxY; y++)
        {
            for (int x = 1; x <= _cells.MaxX; x++)
            {
                byte flags = ExteriorFlags(x, y, z);
                level.Seen[y * w + x] = flags;
                if ((flags & CellFlags.Starlight) != 0)
                    level.Grid.SetSource(StarKey(x, y, w), x, y, star);
            }
        }
    }

    private byte ExteriorFlags(int x, int y, int z)
    {
        byte flags = _replica.CellFlagsAt(x, y, z);
        return _replica.ReceivesExteriorLight(x, y, z) ? (byte)(flags | CellFlags.Starlight) : (byte)(flags & ~CellFlags.Starlight);
    }

    private static long StarKey(int x, int y, int width) => -(1L + y * width + x);

    private void TrackEntity(ReplicaEntity e)
    {
        if (!e.HasLight)
            return;
        ApplyEntityLight(e);
    }

    private void ApplyEntityLight(ReplicaEntity e)
    {
        EntityState s = e.State;
        LightState light = Presented(e);
        if (_lit.TryGetValue(e.Id, out var old) && (old.Z != s.Z) && _byZ.TryGetValue(old.Z, out Level oldLevel))
            oldLevel.Grid.RemoveSource(e.Id);
        if (_byZ.TryGetValue(s.Z, out Level level))
            level.Grid.SetSource(e.Id, s.X, s.Y, GridLight(light));
        bool live = !light.IsNone && light.On && light.Power != 0f && TgLightMath.OnGrid(light.System);
        if (live)
            _lit[e.Id] = (s.X, s.Y, s.Z, light);
        else
            _lit.Remove(e.Id);
        LampChanged(e.Id);
    }

    private void OnLightChanged(ReplicaEntity e) => ApplyEntityLight(e);

    // D288: while the Godot lights draw the sources a flashlight is its spot alone: in the grid (the lamps' bounce) its light would fill its own shadows
    private LightState GridLight(in LightState light) => _dynamic && FlashlightBeam.IsBeam(light) ? LightState.None : light;

    // An entity's light as the grid has it: a flashlight's as FlashlightBeam presents it; DV67: none for the singularity, whose shadowed light is its own (SingularityGlow).
    private static LightState Presented(ReplicaEntity e) =>
        SingularityDrift.Applies(e.TypePath) ? LightState.None : FlashlightBeam.Presented(e.Light);

    private void OnDespawned(ReplicaEntity e)
    {
        if (_lit.Remove(e.Id, out var at) && _byZ.TryGetValue(at.Z, out Level level))
            level.Grid.RemoveSource(e.Id);
        LampChanged(e.Id);
    }

    private void OnStarlightChanged(LightState star)
    {
        int w = _cells.MaxX + 1;
        foreach ((int z, Level level) in _byZ)
        {
            for (int y = 1; y <= _cells.MaxY; y++)
                for (int x = 1; x <= _cells.MaxX; x++)
                    if ((level.Seen[y * w + x] & CellFlags.Starlight) != 0)
                        level.Grid.SetSource(StarKey(x, y, w), x, y, star);
        }
    }

    private void OnCellsChanged(IReadOnlyList<(int X, int Y, int Z)> cells)
    {
        int w = _cells.MaxX + 1;
        foreach ((int x, int y, int z) in cells)
        {
            if (!_byZ.TryGetValue(z, out Level level) || x < 1 || y < 1 || x > _cells.MaxX || y > _cells.MaxY)
                continue;
            byte now = ExteriorFlags(x, y, z);
            byte was = level.Seen[y * w + x];
            level.Seen[y * w + x] = now;
            if (((now ^ was) & CellFlags.Starlight) != 0)
                level.Grid.SetSource(StarKey(x, y, w), x, y, (now & CellFlags.Starlight) != 0 ? _replica.Starlight : LightState.None);
            if (((now ^ was) & CellFlags.Opaque) != 0)
            {
                level.Grid.CellOpacityChanged(x, y);
                _cellsDirty = true;
            }
            level.Grid.CellBaseLightChanged(x, y);
            // a door, a wall or a ceiling changed what is open to the sky (D288); D325: on a stack it changes its column on every level under it
            // too, refilled at once (the whole stack is refilled only when it is entered)
            if (z >= _stackLow && z <= _stackHigh)
            {
                if (!_exteriorFull)
                    SkyColumnRefilled(x, y); // (a stack not all kept is filled whole by the upload)
                _exteriorDirty = true;
            }
            else
                _skyByZ.Remove(z); // another stack's: refilled when it is entered
        }
    }

    // The kept sky masks of the stack's levels at column (x, y), refilled; false when the stack's masks are not all kept (a full fill comes).
    private bool SkyColumnRefilled(int x, int y)
    {
        var levels = new byte[_stackHigh - _stackLow + 1][];
        for (int s = _stackLow; s <= _stackHigh; s++)
        {
            if (!_skyByZ.TryGetValue(s, out levels[s - _stackLow]))
                return false;
        }
        _cells.FillSkyStack(levels, _stackLow, x, y, x, y);
        return true;
    }

    // A lit entity that moved (a mob carrying a light): its source follows.
    private void CheckMovedSources()
    {
        if (_replica == null || _lit.Count == 0)
            return;
        List<long> moved = null;
        foreach ((long id, var at) in _lit)
        {
            if (!_replica.TryGet(id, out ReplicaEntity e))
                continue;
            if (e.State.X != at.X || e.State.Y != at.Y || e.State.Z != at.Z)
                (moved ??= new List<long>()).Add(id);
        }

        if (moved == null)
            return;
        foreach (long id in moved)
            if (_replica.TryGet(id, out ReplicaEntity e))
                ApplyEntityLight(e);
    }

    private void UploadTexture(Level level, int z)
    {
        LightGrid grid = level.Grid;
        // D288: the Godot lights draw the sources, and the grid is the light they bounce, FillShare of it (no Godot light reaches round a
        // corner or lights the side turned away, the grid does, and stops at the walls as tg's light). With no fill the texture is dark
        // and only the floor of the vision (mesons, night vision) and the ambient stay.
        bool dark = _dynamic && _fillShare <= 0f;
        byte[] texels = Stacked(ref _stackTexels, grid.Texels, s => LevelFor(s).Grid.Texels);
        int height = grid.Height * StackCount;
        if (dark && (_zeroTexels == null || _zeroTexels.Length != texels.Length))
            _zeroTexels = new byte[texels.Length];
        var image = Image.CreateFromData(grid.Width, height, false, Image.Format.Rgbah, dark ? _zeroTexels : texels);
        if (_texture == null || _textureWidth != grid.Width || _textureHeight != height)
        {
            _texture = ImageTexture.CreateFromImage(image);
            _textureWidth = grid.Width;
            _textureHeight = height;
            _globalsDirty = true;
        }
        else
        {
            _texture.Update(image);
        }

        if (_globalsDirty)
        {
            RenderingServer.GlobalShaderParameterSet(LightGridUniforms.Grid, _texture);
            RenderingServer.GlobalShaderParameterSet(LightGridUniforms.GridSize, new Vector2(grid.Width, height));
            RenderingServer.GlobalShaderParameterSet(LightGridUniforms.GridOrigin, new Vector3(0f, TileSpace.WorldY(_stackLow), 0f));
            RenderingServer.GlobalShaderParameterSet(LightGridUniforms.Levels, (float)StackCount);
            RenderingServer.GlobalShaderParameterSet(LightGridUniforms.LevelHeight, TileSpace.LevelHeight);
            RenderingServer.GlobalShaderParameterSet(LightGridUniforms.TileMetres, TileSpace.TileSize);
            _globalsDirty = false;
        }

        RenderingServer.GlobalShaderParameterSet(LightGridUniforms.Ambient, Fullbright ? Vector3.One : Ambient);
        RenderingServer.GlobalShaderParameterSet(LightGridUniforms.Strength, Strength * GridShare);
        RenderingServer.GlobalShaderParameterSet(LightGridUniforms.Cap, Cap);
        PushVisionFloor();
    }

    // The grid's share of the light: all of it on the old grid, FillShare as the Godot lights' bounce (1 with no fill: its texture is dark).
    private float GridShare => _dynamic && _fillShare > 0f ? _fillShare : 1f;

    // The shader raises the grid to the floor before scaling it, so the floor is divided by the share: the vision sees as far as it did.
    private void PushVisionFloor() => RenderingServer.GlobalShaderParameterSet(LightGridUniforms.Floor, _visionFloor / GridShare);

    // A door opening or closing changes one cell; the whole level is refilled (a byte per tile, well under a millisecond on MetaStation).
    private void UploadCells(Level level, int z)
    {
        _cellsDirty = false;
        LightGrid grid = level.Grid;
        if (_cellTexels == null || _cellTexels.Length != grid.Width * grid.Height)
            _cellTexels = new byte[grid.Width * grid.Height];
        DoorCellLight.Fill(_cellTexels, _cells, z, DoorFaceAt ?? ((_, _, _) => 0));
        PlaceDoorBlocks(_cells.MaxX + 1, z);
        // D325: the other levels of the stack read their own doors' codes (the door blocks, the lamps' shadows, stay the camera level's)
        byte[] texels = Stacked(ref _cellStackTexels, _cellTexels, s =>
        {
            if (s == z)
                return _cellTexels;
            var other = new byte[_cellTexels.Length];
            DoorCellLight.Fill(other, _cells, s, DoorFaceAt ?? ((_, _, _) => 0));
            return other;
        });
        int height = grid.Height * StackCount;
        var image = Image.CreateFromData(grid.Width, height, false, Image.Format.R8, texels);
        if (_cellTexture == null || _cellTexture.GetWidth() != grid.Width || _cellTexture.GetHeight() != height)
        {
            _cellTexture = ImageTexture.CreateFromImage(image);
            RenderingServer.GlobalShaderParameterSet(LightGridUniforms.Cells, _cellTexture);
        }
        else
        {
            _cellTexture.Update(image);
        }
    }

    // D288 (the owner's play tests): a shut door's cell (opaque in tg, DoorCellLight's code) blocks every lamp and flashlight whatever its model's
    // seams and thinness: a box over it drawn only into their shadows, of which door_block.gdshader keeps the half beyond the door from each light,
    // its face as far toward the light as the light's shadow bias allows.
    private void PlaceDoorBlocks(int width, int z)
    {
        _doorBlockMesh ??= new BoxMesh();
        _lampBlockMaterial ??= DoorBlockMaterial(LampShadowBias);
        _beamBlockMaterial ??= DoorBlockMaterial(BeamBlockFace);
        // One MultiMesh a chunk (DoorBlockChunk tiles square), refilled only where a door's cell changed: a level-wide one told every lamp on the
        // level to draw its shadow again whenever any door opened or shut
        bool all = _doorBlockCells == null || _doorBlockCells.Length != _cellTexels.Length || _doorBlockLevel != z;
        if (all)
        {
            foreach (MultiMeshInstance3D node in _doorBlocks.Values)
                node.Multimesh.InstanceCount = 0;
            _doorBlockCells = new byte[_cellTexels.Length];
            _doorBlockLevel = z;
        }
        int height = _cellTexels.Length / width;
        for (int cy = 0; cy * DoorBlockChunk < height; cy++)
            for (int cx = 0; cx * DoorBlockChunk < width; cx++)
                if (all || ChunkChanged(cx, cy))
                    FillDoorBlocks(cx, cy, width, height, z);
        Array.Copy(_cellTexels, _doorBlockCells, _cellTexels.Length);

        bool ChunkChanged(int cx, int cy)
        {
            for (int y = cy * DoorBlockChunk; y < Math.Min(height, (cy + 1) * DoorBlockChunk); y++)
                for (int x = cx * DoorBlockChunk; x < Math.Min(width, (cx + 1) * DoorBlockChunk); x++)
                    if (_cellTexels[y * width + x] != _doorBlockCells[y * width + x])
                        return true;
            return false;
        }
    }

    private const int DoorBlockChunk = 8;
    private BoxMesh _doorBlockMesh;
    private ShaderMaterial _lampBlockMaterial, _beamBlockMaterial;
    private byte[] _doorBlockCells; // the door cells the blocks were last placed for
    private int _doorBlockLevel;

    private void FillDoorBlocks(int cx, int cy, int width, int height, int z)
    {
        int x0 = cx * DoorBlockChunk, y0 = cy * DoorBlockChunk, x1 = Math.Min(width, x0 + DoorBlockChunk), y1 = Math.Min(height, y0 + DoorBlockChunk);
        int count = 0;
        for (int y = y0; y < y1; y++)
            for (int x = x0; x < x1; x++)
                if (_cellTexels[y * width + x] != 0)
                    count++;
        if (!_doorBlocks.TryGetValue((cx, cy), out MultiMeshInstance3D node))
        {
            if (count == 0)
                return;
            var multi = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = _doorBlockMesh };
            node = NewDoorBlocks($"DoorBlocks_{cx}_{cy}", multi, _lampBlockMaterial, BlockLayer);
            NewDoorBlocks($"BeamDoorBlocks_{cx}_{cy}", multi, _beamBlockMaterial, BeamBlockLayer);
            _doorBlocks[(cx, cy)] = node;
        }
        MultiMesh blocks = node.Multimesh;
        blocks.InstanceCount = count;
        var buffer = new float[count * 12];
        Span<float> v = stackalloc float[12];
        float along = TileSpace.TileSize + 2f * DoorBlockSeal, tall = WorldScale.WallHeight + 2f * DoorBlockSeal;
        // the box's x along the door, its z across it: world x and z for a door whose plane runs east-west, turned a quarter for one running north-south
        var acrossY = new Basis(new Vector3(along, 0f, 0f), new Vector3(0f, tall, 0f), new Vector3(0f, 0f, TileSpace.TileSize));
        var acrossX = new Basis(new Vector3(0f, 0f, -along), new Vector3(0f, tall, 0f), new Vector3(TileSpace.TileSize, 0f, 0f));
        int i = 0;
        for (int y = y0; y < y1; y++)
        {
            for (int x = x0; x < x1; x++)
            {
                byte code = _cellTexels[y * width + x];
                if (code == 0)
                    continue;
                var at = new Vector3(TileSpace.WorldX(x), TileSpace.WorldY(z) + WorldScale.WallHeight * 0.5f, TileSpace.WorldZ(y));
                Basis b = code == DoorCellLight.SnapX * 127 ? acrossX : acrossY;
                (v[0], v[1], v[2], v[3], v[4], v[5], v[6], v[7], v[8]) = (b.Row0.X, b.Row0.Y, b.Row0.Z, b.Row1.X, b.Row1.Y, b.Row1.Z, b.Row2.X, b.Row2.Y, b.Row2.Z);
                (v[9], v[10], v[11]) = (at.X, at.Y, at.Z);
                MultiMeshBuffer.Write(buffer, i++, v);
            }
        }
        blocks.Buffer = buffer;
        // the chunk's box (a tile and the wall's height beyond its cells): set after the buffer, it tells the lamps near it to draw their shadows again
        Vector3 a = new(TileSpace.WorldX(x0), TileSpace.WorldY(z), TileSpace.WorldZ(y0)), c = new(TileSpace.WorldX(x1 - 1), TileSpace.WorldY(z) + WorldScale.WallHeight, TileSpace.WorldZ(y1 - 1));
        Vector3 low = a.Min(c) - Vector3.One * TileSpace.TileSize, high = a.Max(c) + Vector3.One * TileSpace.TileSize;
        blocks.CustomAabb = new Aabb(low, high - low);
    }

    private static ShaderMaterial DoorBlockMaterial(float towardLight)
    {
        var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/door_block.gdshader") };
        material.SetShaderParameter("toward_light", towardLight);
        return material;
    }

    // a chunk's blocks as one kind of light's shadows see them; the lamps' and the flashlights' share the chunk's MultiMesh
    private MultiMeshInstance3D NewDoorBlocks(string name, MultiMesh multi, ShaderMaterial material, uint layer)
    {
        var blocks = new MultiMeshInstance3D
        {
            Name = name,
            Multimesh = multi,
            MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.ShadowsOnly,
            Layers = layer,
        };
        AddChild(blocks);
        return blocks;
    }

    // ---- the shadowed Godot lights (D114) ----

    private void SelectShadowed(int z)
    {
        _candidates.Clear();
        float camX = _camera.GlobalPosition.X / TileSpace.TileSize + 0.5f;
        float camY = -_camera.GlobalPosition.Z / TileSpace.TileSize + 0.5f;
        if (_budget > 0)
        {
            foreach ((long id, var at) in _lit)
            {
                if (at.Z != z)
                    continue;
                LightState l = at.S;
                if (l.Power <= 0f || l.System != TgLightMath.ComplexLight) // an overlay light (a flashlight) is the grid's and the camera's spot
                    continue;
                float dx = at.X - camX, dy = at.Y - camY;
                if (dx * dx + dy * dy > (l.Range + 2f) * (l.Range + 2f))
                    continue;
                _candidates.Add(new ShadowCandidate(id, at.X, at.Y, l.Range, l.Power));
            }
        }

        ShadowedLightSelector.Select(_cells, z, camX, camY, _candidates, Math.Max(0, _budget - _beamShadows), _chosen, _scratch);

        // Slots whose source is no longer chosen fade out; chosen sources without a slot take a free one.
        foreach (Slot slot in _slots)
            if (slot.Key != long.MinValue)
                slot.Target = _chosen.Any(c => c.Key == slot.Key) ? 1f : 0f;
        foreach (ShadowCandidate c in _chosen)
        {
            if (_slots.Any(s => s.Key == c.Key))
                continue;
            Slot free = _slots.FirstOrDefault(s => s.Key == long.MinValue);
            if (free == null && _slots.Count < _budget)
            {
                var omni = new OmniLight3D { ShadowEnabled = true, ShadowBias = LampShadowBias, ShadowNormalBias = LampShadowNormalBias, ShadowCasterMask = ~BeamBlockLayer, LightSpecular = 0f, OmniAttenuation = 1.4f, LightVolumetricFogEnergy = LampFogEnergy, Visible = false };
                AddChild(omni);
                free = new Slot { Light = omni };
                _slots.Add(free);
            }

            if (free == null)
                continue;
            if (!_lit.TryGetValue(c.Key, out var src))
                continue;
            LightState l = src.S;
            free.Key = c.Key;
            free.Fade = 0f;
            free.Target = 1f;
            free.Energy = l.Power * ShadowEnergyScale;
            free.Light.LightColor = new Color(((l.Rgba >> 24) & 255) / 255f, ((l.Rgba >> 16) & 255) / 255f, ((l.Rgba >> 8) & 255) / 255f);
            free.Light.OmniRange = (l.Range + 1f) * TileSpace.TileSize;
            // tg's offset is added to the corner's offset from the source (generate_sheet), so the source sits at the tile minus it.
            free.Light.Position = new Vector3(TileSpace.WorldX(c.X - l.OffsetX), TileSpace.WorldY(z) + WorldScale.LampHeight, TileSpace.WorldZ(c.Y - l.OffsetY));
            free.Light.LightEnergy = 0f;
            free.Light.Visible = true;
        }
    }

    private void FadeShadowed(float delta)
    {
        float step = delta / FadeSeconds;
        foreach (Slot slot in _slots)
        {
            if (slot.Key == long.MinValue)
                continue;
            slot.Fade = Mathf.MoveToward(slot.Fade, slot.Target, step);
            slot.Light.LightEnergy = slot.Fade * slot.Energy;
            if (slot.Fade <= 0f && slot.Target <= 0f)
            {
                slot.Key = long.MinValue;
                slot.Light.Visible = false;
            }
        }
    }

    // ---- D288: the sources as Godot lights ----

    // DEVIATION(DV221): tg's sources are drawn as Godot lights (tg's range, power and colour, Godot's falloff, no per-corner normalisation),
    // not as tg's corner light; the sun and the light of space below are the client's own.

    // Every 0.2 s: the lit sources within the light distance, nearest first; the first of them get the shadow budget (a shadowed lamp keeps
    // its shadow until another is ShadowKeepTiles nearer), and one beyond the budget is drawn only while the camera sees it (without a shadow
    // its light would cross the walls into the next room). The others fade out.
    private void SelectLamps(int z)
    {
        float camX = _camera.GlobalPosition.X / TileSpace.TileSize + 0.5f;
        float camY = -_camera.GlobalPosition.Z / TileSpace.TileSize + 0.5f;
        float reach = _lightDistance + 2f;
        LoadFrustum();
        _lampOrder.Clear();
        // D302: a burning room's many hotspots light it as one lamp per group of tiles (FireGroup), a Godot light for each would cost far more than the flames
        _fireLamps.Clear();
        foreach ((long id, var at) in _lit)
        {
            if (InStack(at.Z) && IsFire(id)) // D325: every level of the camera's stack
            {
                long group = ShadowedLightSelector.FireGroup(at.X, at.Y);
                _fireLamps[group] = _fireLamps.TryGetValue(group, out long first) ? Math.Min(first, id) : id;
            }
        }
        foreach ((long id, var at) in _lit)
        {
            if (!InStack(at.Z) || FlashlightBeam.IsBeam(at.S)) // D325: the lamps of every level of the camera's stack
                continue;
            if (IsFire(id) && _fireLamps[ShadowedLightSelector.FireGroup(at.X, at.Y)] != id)
                continue;
            float dx = at.X - camX, dy = at.Y - camY, d2 = dx * dx + dy * dy;
            float near = at.S.Range < SmallRange ? Math.Min(reach, SmallLightTiles + 2f) : reach;
            if (d2 > near * near)
                continue;
            float keep = _lamps.TryGetValue(id, out Lamp had) && had.ShadowTarget > 0f ? ShadowKeepTiles : 0f;
            ReplicaEntity e = null;
            bool onBody = _replica != null && _replica.TryGet(id, out e) && e.IsMob();
            // the owner (2026-10-29): the lights that reach the view before the others, whatever their kind and distance
            var centre = new Vector3(TileSpace.WorldX(at.X), TileSpace.WorldY(at.Z) + WorldScale.WallHeight * 0.5f, TileSpace.WorldZ(at.Y));
            bool seen = InView(centre, Math.Max(at.S.Range, 1f) * TileSpace.TileSize + ViewMargin);
            _lampOrder.Add((ShadowedLightSelector.Priority(e?.TypePath, onBody) + (seen ? 0 : OutOfView), MathF.Sqrt(d2) - keep, id));
        }

        _lampOrder.Sort((a, b) => a.Priority != b.Priority ? a.Priority.CompareTo(b.Priority) : a.Distance != b.Distance ? a.Distance.CompareTo(b.Distance) : a.Id.CompareTo(b.Id));
        foreach (Lamp lamp in _lamps.Values)
            lamp.Target = 0f;
        // the effects take the shadows first, then the other flashlights' spots (UpdateBeams) and the lights on bodies, then the lamps
        int shadows = _budget, effects = 0, newShadows = 0, fireShadows = 0;
        bool beamsTaken = false;
        _thingOrder.Clear();
        foreach ((int priority, float distance, long id) in _lampOrder)
        {
            if (priority > 0 && !beamsTaken)
            {
                shadows = Math.Max(0, shadows - _beamShadows);
                beamsTaken = true;
            }
            var at = _lit[id];
            bool small = at.S.Range < SmallRange;
            bool had = _lamps.TryGetValue(id, out Lamp known) && known.ShadowTarget > 0f;
            // an effect (fire, sparks) is short-lived: its shadow comes at once; a lamp past this selection's new shadows waits for the next
            bool fire = IsFire(id);
            bool shadowed = shadows > 0 && (had || priority == 0 || newShadows < MaxNewShadows) && !(fire && fireShadows >= MaxFireShadows);
            if (shadowed)
            {
                if (fire)
                    fireShadows++;
                shadows--;
                if (priority == 0)
                    effects++;
                else if (!had)
                    newShadows++;
            }
            // the owner's play test (2026-10-29): a light without a shadow crosses walls and shut doors, a small one too (maintenance's dim lamps,
            // 2.4 tiles, lit the tunnel behind a shut door), so it is drawn only while the camera sees its tile
            bool shown = shadowed || ShadowedLightSelector.HasLineOfSight(_cells, z, camX, camY, at.X, at.Y);
            if (!_lamps.TryGetValue(id, out Lamp lamp))
            {
                if (!shown)
                    continue;
                lamp = NewLamp(id, _instant.Contains(id));
            }

            lamp.Target = shown ? 1f : 0f;
            lamp.ShadowTarget = shadowed ? 1f : 0f;
            if (shadowed && !small)
                _thingOrder.Add((priority >= OutOfView ? distance + OutOfViewKey : distance, id));
            else
                HoldThings(lamp, false);
            if (_instant.Contains(id))
                lamp.Fade = lamp.Target; // switched on: at once, as tg's light
        }

        // every shadow pass draws whole chunks of things, so only the shadows of the shadowed lamps nearest the camera hold them and the
        // characters, whatever their priority (the own body's shadow is among them); the rest shade the walls
        _thingOrder.Sort();
        for (int i = 0; i < _thingOrder.Count; i++)
            HoldThings(_lamps[_thingOrder[i].Id], i < _objectShadowLamps);
        _effectShadows = effects;
        _instant.Clear();
    }

    private bool IsFire(long id) => _replica != null && _replica.TryGet(id, out ReplicaEntity e) && e.TypePath == Anomaly.Client.Shared.World.StaticPropRule.HotspotType;

    private void LoadFrustum()
    {
        Godot.Collections.Array<Plane> planes = _camera.GetFrustum();
        for (int i = 0; i < _frustum.Length; i++)
            _frustum[i] = i < planes.Count ? planes[i] : default;
        _selectedLook = -_camera.GlobalTransform.Basis.Z;
    }

    // Whether a light's sphere reaches into the camera's frustum (as Godot culls: outside one plane is outside).
    private bool InView(Vector3 centre, float radius)
    {
        foreach (Plane plane in _frustum)
            if (plane.Normal != Vector3.Zero && plane.DistanceTo(centre) > radius)
                return false;
        return true;
    }

    // A light's shadow switched only when it flips: Godot draws a light's shadow again on every set of it, of its range or of its shadow's
    // kind, the same value or not (each fading lamp and every beam set it every frame).
    private static void SetShadow(Light3D light, bool on)
    {
        if (light.ShadowEnabled != on)
            light.ShadowEnabled = on;
    }

    private void HoldThings(Lamp lamp, bool things)
    {
        uint casters = (!things ? ~(ThingLayer | CharacterLayer | OwnBodyLayer) : _objectShadows > 0 ? uint.MaxValue : ~ThingLayer) & ~BeamBlockLayer;
        if (lamp.Light.ShadowCasterMask != casters)
            lamp.Light.ShadowCasterMask = casters;
        // the own body's shadow-only picture (first person) moves with every step, and Godot draws again the shadow of every lamp it is
        // paired with, whatever the caster mask: it is paired only with the lamps that cast it (it is never drawn lit, so no light misses it)
        uint cull = (casters & OwnBodyLayer) != 0 ? uint.MaxValue : ~OwnBodyLayer;
        if (lamp.Light.LightCullMask != cull)
            lamp.Light.LightCullMask = cull;
    }

    // Fades the lamps toward their targets (in and out over FadeSeconds, their shadows too) and frees the ones faded out.
    private void FadeLamps(float delta)
    {
        float step = delta / FadeSeconds;
        _lampGone.Clear();
        foreach ((long id, Lamp lamp) in _lamps)
        {
            if (lamp.Fade == lamp.Target && lamp.Shadow == lamp.ShadowTarget)
                continue;
            lamp.Fade = Mathf.MoveToward(lamp.Fade, lamp.Target, step);
            lamp.Shadow = Mathf.MoveToward(lamp.Shadow, lamp.ShadowTarget, step);
            lamp.Light.LightEnergy = lamp.Fade * lamp.Energy;
            SetShadow(lamp.Light, lamp.Shadow > 0f);
            lamp.Light.ShadowOpacity = lamp.Shadow;
            if (lamp.Fade <= 0f && lamp.Target <= 0f)
                _lampGone.Add(id);
        }

        foreach (long id in _lampGone)
            RemoveLamp(id);
    }

    // A source's light changed, went out or went away: its lamp follows at once (tg's flicker and switches are instant); one switched on
    // appears with the next selection, which is brought forward.
    private void LampChanged(long id)
    {
        if (!_dynamic)
            return;
        if (!_lit.ContainsKey(id))
        {
            RemoveLamp(id);
            return;
        }

        if (_lamps.TryGetValue(id, out Lamp lamp))
        {
            StyleLamp(id, lamp);
            return;
        }

        _instant.Add(id);
        _selectIn = 0;
    }

    private Lamp NewLamp(long id, bool instant)
    {
        var omni = new OmniLight3D { ShadowBias = LampShadowBias, ShadowNormalBias = LampShadowNormalBias, LightSpecular = 0f, OmniAttenuation = LampAttenuation, LightVolumetricFogEnergy = LampFogEnergy, DistanceFadeEnabled = true, ShadowEnabled = false, ShadowCasterMask = ~(ThingLayer | CharacterLayer | OwnBodyLayer | BeamBlockLayer), LightCullMask = ~OwnBodyLayer };
        AddChild(omni);
        var lamp = new Lamp { Light = omni, Fade = instant ? 1f : 0f };
        _lamps[id] = lamp;
        StyleLamp(id, lamp);
        return lamp;
    }

    private void RemoveLamp(long id)
    {
        if (_lamps.Remove(id, out Lamp lamp))
            lamp.Light.QueueFree();
    }

    // The preset's part of a lamp: its shadow's kind and the distance it fades out over (Godot's distance fade, the shadow's with it).
    private void ApplyLampPreset(Lamp lamp)
    {
        OmniLight3D omni = lamp.Light;
        OmniLight3D.ShadowMode mode = _cubeShadows ? OmniLight3D.ShadowMode.Cube : OmniLight3D.ShadowMode.DualParaboloid;
        if (omni.OmniShadowMode != mode)
            omni.OmniShadowMode = mode;
        float end = (lamp.Small ? Math.Min(_lightDistance, SmallLightTiles) : _lightDistance) * TileSpace.TileSize;
        omni.DistanceFadeBegin = Math.Max(0f, end - FadeMetres);
        omni.DistanceFadeLength = FadeMetres;
        omni.DistanceFadeShadow = Math.Max(0f, end - FadeMetres);
    }

    // tg's light as a Godot light: its colour, its range in metres, its power as energy; where it is (StyleLamp's PlaceLamp).
    private void StyleLamp(long id, Lamp lamp)
    {
        var at = _lit[id];
        LightState l = at.S;
        // tg's falloff at the source itself (its height under the root): a small light is dim even where it stands
        lamp.Energy = l.Power * LampStrength * TgLightMath.FalloffAtCoord(0f, 0f, 0f, l.Range, 0, 360f, Math.Max(l.Height, 0f)) * (IsFire(id) ? FireLampBoost : 1f);
        lamp.Light.LightColor = new Color(((l.Rgba >> 24) & 255) / 255f, ((l.Rgba >> 16) & 255) / 255f, ((l.Rgba >> 8) & 255) / 255f);
        float range = Math.Max(l.Range, 1f) * TileSpace.TileSize;
        if (lamp.Light.OmniRange != range)
            lamp.Light.OmniRange = range; // a flicker changes the power only: the shadow stays
        lamp.Light.LightEnergy = lamp.Fade * lamp.Energy;
        lamp.Small = l.Range < SmallRange;
        ApplyLampPreset(lamp);
        ReplicaEntity e = null;
        lamp.Carried = _replica != null && _replica.TryGet(id, out e) && e.IsMob();
        if (lamp.Carried)
        {
            PlaceCarried(lamp, e);
            return;
        }
        // tg's offset is added to the corner's offset from the source (generate_sheet), so the source sits at the tile minus it; a wall
        // lamp's half tile would put it on the wall's face, so it stays MaxLampOffset off (a quarter metre): the wall shades it cleanly
        float ox = Math.Clamp(l.OffsetX, -MaxLampOffset, MaxLampOffset), oy = Math.Clamp(l.OffsetY, -MaxLampOffset, MaxLampOffset);
        float height = l.Height < 0.5f ? FloorLightHeight : WorldScale.WallHeight * LampLightShare;
        lamp.Light.Position = new Vector3(TileSpace.WorldX(at.X - ox), TileSpace.WorldY(at.Z) + height, TileSpace.WorldZ(at.Y - oy));
    }

    // A light a mob carries (a glowstick, a flare in a hand) is at its hand, with its drawn body.
    private void PlaceCarriedLamps()
    {
        if (_replica == null)
            return;
        foreach ((long id, Lamp lamp) in _lamps)
            if (lamp.Carried && _replica.TryGet(id, out ReplicaEntity e))
                PlaceCarried(lamp, e);
    }

    private void PlaceCarried(Lamp lamp, ReplicaEntity e)
    {
        float x = e.State.X, y = e.State.Y;
        int z = e.State.Z;
        float rise = 0f;
        bool lying = (e.State.Posture & PostureBits.Lying) != 0;
        if (Poses != null && Poses.TryGetPose(e, out RenderPose pose))
        {
            x = pose.X;
            y = pose.Y;
            z = pose.Z;
            lying = (pose.Posture & PostureBits.Lying) != 0;
            rise = pose.Rise; // DV371
        }
        lamp.Light.Position = new Vector3(TileSpace.WorldX(x), TileSpace.WorldY(z) + rise + (lying ? FlashlightBeam.LyingHeight : FlashlightBeam.HandHeight), TileSpace.WorldZ(y));
    }

    // ---- D288: the sun and the light of space ----

    private void EnsureSun()
    {
        if (_sun != null && IsInstanceValid(_sun))
            return;
        // (it lights the map and is no part of a sky: the sky draws its own sun)
        _sun = new DirectionalLight3D { Name = "Sun", LightColor = _sunTint, LightSpecular = 0f, LightVolumetricFogEnergy = 0f, Visible = false, SkyMode = DirectionalLight3D.SkyModeEnum.LightOnly };
        AddChild(_sun);
        ApplySunPreset();
        ApplySky();
    }

    private void ApplySunPreset()
    {
        if (_sun == null)
            return;
        _sun.ShadowEnabled = _sunPlan.Splits > 0;
        _sun.DirectionalShadowMode = _sunPlan.Splits >= 4 ? DirectionalLight3D.ShadowMode.Parallel4Splits : DirectionalLight3D.ShadowMode.Parallel2Splits;
        _sun.DirectionalShadowMaxDistance = Math.Max(1f, _sunPlan.Distance);
        _sun.ShadowCasterMask = (_objectShadows >= 2 ? uint.MaxValue : ~ThingLayer) & ~(BlockLayer | BeamBlockLayer);
        // the shadow fades out from 0.8 of its distance (Godot's default): the materials hand the sun to the open sky before that.
        // With the levels above hidden (third person) they cast no shadow, so the sun goes only where the grid's open sky lets it.
        RenderingServer.GlobalShaderParameterSet(LightGridUniforms.SunReach, _levelsAboveHidden ? 0f : _sunPlan.Distance * 0.75f);
    }

    // The sun shines along -SunDirection; it is turned only when the sky's sun moved.
    // The sun is drawn only while it has something to light near the camera: a cell open to the sky within its shadow's reach (a window
    // borders one, the hull stands on them). Deep inside the station it fades out, and with it the whole pass of its shadow.
    private void UpdateSun(float delta)
    {
        if (_sun == null)
            return;
        _sunCheckIn -= delta;
        if (_sunCheckIn <= 0f)
        {
            _sunCheckIn = SunCheckEvery;
            _sunWanted = _sunPlan.Splits == 0 || OpenSkyNear(_sunPlan.Distance / TileSpace.TileSize);
        }
        _sunFade = Mathf.MoveToward(_sunFade, _dynamic && _sunWanted && !_ashen ? 1f : 0f, delta / SunFadeSeconds);
        _sun.Visible = _sunFade > 0f;
        _sun.LightEnergy = SunStrength * _sunFade;
        if (!_sun.Visible || SunDirection.IsEqualApprox(_sunShown) || SunDirection.LengthSquared() < 1e-6f)
            return;
        _sunShown = SunDirection;
        Vector3 up = Mathf.Abs(SunDirection.Y) > 0.99f ? Vector3.Forward : Vector3.Up;
        _sun.GlobalBasis = Basis.LookingAt(-SunDirection, up);
    }

    // Whether a cell of the camera level's open-sky mask lies within reachTiles of the camera (a square, scanned twice a second).
    private bool OpenSkyNear(float reachTiles)
    {
        if (_exteriorTexels == null || _cells == null || _camera == null)
            return true;
        int w = _cells.MaxX + 1;
        int cx = (int)MathF.Round(_camera.GlobalPosition.X / TileSpace.TileSize + 0.5f), cy = (int)MathF.Round(-_camera.GlobalPosition.Z / TileSpace.TileSize + 0.5f);
        int r = (int)MathF.Ceiling(reachTiles);
        int x0 = Math.Max(1, cx - r), x1 = Math.Min(_cells.MaxX, cx + r), y0 = Math.Max(1, cy - r), y1 = Math.Min(_cells.MaxY, cy + r);
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
                if (_exteriorTexels[y * w + x] != 0)
                    return true;
        return false;
    }

    // The ambient cube of the stars and the planet (SkyLight), or Lavaland's ash glow; none while the grid draws its own starlight.
    private void ApplySky()
    {
        float gain = _dynamic ? _skyGain : 0f;
        var (up, side, down) = _ashen ? SkyLight.AshCube(gain) : SkyLight.Cube(gain);
        RenderingServer.GlobalShaderParameterSet(LightGridUniforms.SkyUp, new Vector3(up.R, up.G, up.B));
        RenderingServer.GlobalShaderParameterSet(LightGridUniforms.SkySide, new Vector3(side.R, side.G, side.B));
        RenderingServer.GlobalShaderParameterSet(LightGridUniforms.SkyDown, new Vector3(down.R, down.G, down.B));
    }

    // The camera level's open-sky mask (anomaly_exterior): filled when the level is first shown or the plain cells changed, cell by cell from the
    // replica's changes (OnCellsChanged), uploaded when touched.
    private void UploadExterior(int z)
    {
        _exteriorDirty = false;
        int w = _cells.MaxX + 1, h = _cells.MaxY + 1;
        for (int s = _stackLow; s <= _stackHigh && !_exteriorFull; s++)
            _exteriorFull = !_skyByZ.TryGetValue(s, out byte[] kept) || kept.Length != w * h;
        if (_exteriorFull)
        {
            // the whole stack in one pass down each column (ILightCells.FillSkyStack)
            var levels = new byte[_stackHigh - _stackLow + 1][];
            for (int s = _stackLow; s <= _stackHigh; s++)
            {
                if (!_skyByZ.TryGetValue(s, out byte[] mask) || mask.Length != w * h)
                    _skyByZ[s] = mask = new byte[w * h];
                levels[s - _stackLow] = mask;
            }
            _cells.FillSkyStack(levels, _stackLow);
            _exteriorFull = false;
        }
        _exteriorTexels = _skyByZ[z];
        // D325: every level of the stack, its own block of rows (the camera level's texels are also the sun's reach)
        byte[] texels = Stacked(ref _exteriorStackTexels, _exteriorTexels, s => _skyByZ[s]);
        h *= StackCount;
        var image = Image.CreateFromData(w, h, false, Image.Format.R8, texels);
        if (_exteriorTexture == null || _exteriorTexture.GetWidth() != w || _exteriorTexture.GetHeight() != h)
        {
            _exteriorTexture = ImageTexture.CreateFromImage(image);
            RenderingServer.GlobalShaderParameterSet(LightGridUniforms.Exterior, _exteriorTexture);
        }
        else
        {
            _exteriorTexture.Update(image);
        }
    }

    // ---- the dev flashlight ----

    // The player's own flashlight: tg draws a directional overlay light (OVERLAY_LIGHT_DIRECTIONAL, OVERLAY_LIGHT_BEAM) ahead
    // of its holder, which the grid has at the server's cast; in first person the camera's spot is that light's beam too, in
    // its colour, as far as tg's light reaches (FlashlightBeam: its range past the cast, its energy per unit of power).
    private void UpdateOwnFlashlight()
    {
        LightState own = LightState.None;
        if (_feed != null && _replica != null && _replica.TryGet(_feed.OwnEntity, out ReplicaEntity me))
            own = FlashlightBeam.Presented(me.Light);
        bool on = FlashlightBeam.IsBeam(own);
        _ownLight = own;
        if (on && _flashlight != null)
        {
            Style(_flashlight, own);
            HeldHandsView hands = HeldHands.Find(_replica);
            bool left = HeldHands.IsLit(_replica, hands.Left) && !HeldHands.IsLit(_replica, hands.Right); // else the right hand (a worn light too)
            _ownHand = left ? OwnBeamHand with { X = -OwnBeamHand.X } : OwnBeamHand;
        }

        if (on == _ownFlashlight)
            return;
        _ownFlashlight = on;
        if (!on && _flashlight != null)
        {
            _flashlight.LightColor = Colors.White;
            _flashlight.SpotRange = DevSpotRange;
            _flashlight.LightEnergy = DevSpotEnergy;
            _ownHand = OwnBeamHand; // the dev flashlight's: the right hand
        }
        ShowFlashlight();
    }

    /// <summary>D288: the own walk for the flashlight's sway, set every frame by the scene: the stride's phase (radians, a step every pi) and how hard one walks (<c>CameraLayer.Stride</c>).</summary>
    public float StridePhase { get; set; }

    public float Stride { get; set; }

    private Vector3 _ownHand = OwnBeamHand;
    private readonly FlashlightSway _sway = new();
    private float _viewYaw, _viewPitch;
    private bool _viewSeen;

    // The own beam at its hand, aimed at the crosshair and swayed by the hand (FlashlightSway: the view's turn, the walk, the breath).
    private void SwayOwnBeam(float dt)
    {
        Vector3 f = -_camera.GlobalBasis.Z;
        float yaw = Mathf.RadToDeg(MathF.Atan2(f.X, -f.Z)), pitch = Mathf.RadToDeg(MathF.Asin(Math.Clamp(f.Y, -1f, 1f)));
        float yawRate = 0f, pitchRate = 0f;
        if (_viewSeen && dt > 0f)
        {
            yawRate = Mathf.Wrap(yaw - _viewYaw, -180f, 180f) / dt;
            pitchRate = (pitch - _viewPitch) / dt;
        }
        (_viewYaw, _viewPitch, _viewSeen) = (yaw, pitch, true);
        (float swayYaw, float swayPitch) = _sway.Update(Time.GetTicksMsec() / 1000.0, dt, yawRate, pitchRate, StridePhase, Stride);
        Basis aim = Basis.LookingAt(new Vector3(0f, 0f, -OwnBeamAim) - _ownHand, Vector3.Up);
        var sway = new Basis(Vector3.Up, -Mathf.DegToRad(swayYaw)) * new Basis(Vector3.Right, Mathf.DegToRad(swayPitch));
        _flashlight.Transform = new Transform3D(sway * aim, _ownHand);
    }

    /// <summary>A new spot light drawn as another holder's flashlight beam (DV62; the dev light room's beam too).</summary>
    private static ShaderMaterial _glareMaterial;
    private static QuadMesh _glareQuad;

    // D288 (the owner, 2026-10-29): the glow at another holder's flashlight, which dazzles the eye inside its beam (flashlight_glare.gdshader);
    // the spot's child, so it is where the beam starts and turns with it.
    private static MeshInstance3D NewGlare() => new()
    {
        Name = "Glare",
        Mesh = _glareQuad ??= new QuadMesh { Size = Vector2.One },
        MaterialOverride = _glareMaterial ??= new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/flashlight_glare.gdshader") },
        CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        ExtraCullMargin = 2f, // the shader draws it up to 1.6 m across about the beam's start
    };

    public static SpotLight3D NewBeamSpot() =>
        new() { SpotAngle = FlashlightBeam.Angle, SpotAngleAttenuation = BeamAngleAttenuation, SpotAttenuation = 0.8f, LightSpecular = 0f, LightVolumetricFogEnergy = 0f, ShadowCasterMask = ~BlockLayer };

    /// <summary>Gives <paramref name="spot"/> the colour, range and energy of the flashlight light <paramref name="light"/>.</summary>
    public static void Style(SpotLight3D spot, in LightState light)
    {
        (float r, float g, float b) = FlashlightBeam.Color(light);
        spot.LightColor = new Color(r, g, b);
        spot.SpotRange = FlashlightBeam.RangeTiles(light) * TileSpace.TileSize;
        spot.LightEnergy = FlashlightBeam.Energy(light);
    }

    // DV62: a beam through gas: its hue is what the gases let through (GasOptics.Transmittance from the beam's origin along
    // its heading), and it reaches less far by the mean of that (down to 40 per cent in a gas that stops everything).
    private void Tint(SpotLight3D spot, in LightState light, float x, float y, int z, float hx, float hy) => Tint(spot, light, GasAt, x, y, z, hx, hy);

    /// <summary>
    /// Styles <paramref name="spot"/> as the flashlight light <paramref name="light"/> from (<paramref name="x"/>,
    /// <paramref name="y"/>) along the unit heading, through the gases <paramref name="gas"/> gives (null: none; DV62).
    /// </summary>
    public static void Tint(SpotLight3D spot, in LightState light, Func<int, int, int, CellOptics> gas, float x, float y, int z, float hx, float hy)
    {
        Style(spot, light);
        if (gas == null)
            return;
        float range = FlashlightBeam.RangeTiles(light);
        (float r, float g, float b) = GasOptics.Transmittance((cx, cy) => gas(cx, cy, z), x, y, hx, hy, range, TileSpace.TileSize);
        if (r >= 0.999f && g >= 0.999f && b >= 0.999f)
            return;
        // The hue is what gets through; the brightness falls by half of that at most, since the beam near the holder (the
        // glow in the gas) is not yet dimmed, and the beam reaching less far is the rest of the loss.
        float top = Math.Max(r, Math.Max(g, b));
        float bright = 0.5f + 0.5f * top;
        (float lr, float lg, float lb) = FlashlightBeam.Color(light);
        spot.LightColor = top > 0f ? new Color(lr * r / top * bright, lg * g / top * bright, lb * b / top * bright) : Colors.Black;
        spot.SpotRange = range * (0.4f + 0.6f * (r + g + b) / 3f) * TileSpace.TileSize;
    }

    // ---- the other flashlights' spots (DV62) ----

    // Every other lit directional overlay light on the camera's level near enough is a spot at its holder's hand (or on the
    // floor) along the way it faces, moved every frame with the drawn body; the nearest of them cast shadows, before every lamp
    // but the effects (D288: the budget less the effects' shadows), the rest light without.
    private void UpdateBeams(int z)
    {
        if (_replica == null)
            return;
        Vector3 cam = _camera.GlobalPosition;
        float camX = cam.X / TileSpace.TileSize + 0.5f, camY = -cam.Z / TileSpace.TileSize + 0.5f;
        long own = _feed?.OwnEntity ?? 0;
        _beamOrder.Clear();
        foreach ((long id, var at) in _lit)
        {
            if ((id == own && !_ownBeamFromBody) || at.Z != z || !FlashlightBeam.IsBeam(at.S))
                continue;
            float dx = at.X - camX, dy = at.Y - camY;
            float reach = _dynamic ? _lightDistance + 2f : BeamMaxTiles; // the grid has none of them then (GridLight)
            if (dx * dx + dy * dy > reach * reach || !_replica.TryGet(id, out ReplicaEntity e))
                continue;
            if (!_beams.TryGetValue(id, out SpotLight3D spot))
            {
                spot = NewBeamSpot();
                spot.AddChild(NewGlare());
                AddChild(spot);
                _beams[id] = spot;
            }

            PlaceBeam(spot, e, at.S);
            _beamOrder.Add((dx * dx + dy * dy + (InView(spot.Position, spot.SpotRange + ViewMargin) ? 0f : OutOfViewKey), id));
        }

        _beamGone.Clear();
        foreach (long id in _beams.Keys)
            if (!_beamOrder.Exists(b => b.Item2 == id))
                _beamGone.Add(id);
        foreach (long id in _beamGone)
        {
            _beams[id].QueueFree();
            _beams.Remove(id);
        }

        if (_beamOrder.Count != _beamsLogged)
        {
            _beamsLogged = _beamOrder.Count;
            GD.Print($"[anomaly] light: {_beamsLogged} other flashlight beam(s) drawn");
        }

        _beamOrder.Sort((a, b) => a.Item1.CompareTo(b.Item1));
        int budget = Math.Max(0, _budget - _effectShadows), shadows = 0;
        foreach ((float key, long id) in _beamOrder)
        {
            // D288: the own beam (third person) is always drawn with its shadow, outside the budget, as the camera's spot is in first
            // person (the atlas keeps it a slot); the others take the budget the effects leave, those whose light reaches the view
            bool mine = id == own, shadowed = mine || (key < OutOfViewKey && shadows < budget);
            if (shadowed && !mine)
                shadows++;
            SetShadow(_beams[id], shadowed);
        }
        if (shadows != _beamShadows)
        {
            _beamShadows = shadows;
            _selectIn = 0; // the lamps' share of the budget changed
        }
    }

    private void PlaceBeam(SpotLight3D spot, ReplicaEntity e, in LightState light)
    {
        float x = e.State.X, y = e.State.Y;
        int z = e.State.Z;
        bool hasYaw = e.HasPos, lying = (e.State.Posture & PostureBits.Lying) != 0;
        float yaw = e.State.Yaw, rise = 0f;
        if (Poses != null && Poses.TryGetPose(e, out RenderPose pose))
        {
            x = pose.X;
            y = pose.Y;
            z = pose.Z;
            yaw = pose.Yaw;
            lying = (pose.Posture & PostureBits.Lying) != 0;
            rise = pose.Rise; // DV371
        }

        float heading = FlashlightBeam.YawByte(hasYaw, yaw, light, e.State.Dir);
        bool carried = e.IsMob();
        (float ox, float oy, float h) = FlashlightBeam.Origin(x, y, heading, carried, lying);
        (float hx, float hy) = FlashlightBeam.Heading(heading);
        Tint(spot, light, ox, oy, z, hx, hy);
        if (spot.GetChildCount() > 0 && spot.GetChild(0) is MeshInstance3D glare)
        {
            glare.SetInstanceShaderParameter("tint", spot.LightColor);
            glare.SetInstanceShaderParameter("strength", Math.Clamp(light.Power, 0.5f, 2f));
        }
        spot.Position = new Vector3(TileSpace.WorldX(ox), TileSpace.WorldY(z) + rise + h, TileSpace.WorldZ(oy));
        // A spot shines along its -Z; the yaw byte is clockwise from north (-Z), Godot's yaw counter-clockwise.
        spot.Rotation = new Vector3(carried ? -FlashlightBeam.Tilt : 0f, -heading * (2f * MathF.PI / 256f), 0f);
    }

    private bool _ownBeamFromBody;

    /// <summary>The level the view is on when it is not the camera's height (third person hangs above the walls, which is the next level's height); 0: from the camera.</summary>
    public int ViewLevel { get; set; }

    private bool _levelsAboveHidden;

    /// <summary>The third person's view with the levels above the own one not drawn (MapRenderer.SetSight's hideAbove): they cast no sun shadow then.</summary>
    public bool LevelsAboveHidden
    {
        get => _levelsAboveHidden;
        set
        {
            if (_levelsAboveHidden == value)
                return;
            _levelsAboveHidden = value;
            ApplySunPreset();
        }
    }

    /// <summary>
    /// Third person: the own flashlight is a spot at the drawn body's hand like another holder's, not the camera's spot (which sits behind the player and blows its model out).
    /// </summary>
    public bool OwnBeamFromBody
    {
        get => _ownBeamFromBody;
        set
        {
            if (_ownBeamFromBody == value)
                return;
            _ownBeamFromBody = value;
            ShowFlashlight();
        }
    }

    private void ShowFlashlight()
    {
        if (_flashlight != null)
            _flashlight.Visible = _flashlightOn || (_ownFlashlight && !_ownBeamFromBody);
    }

    private void EnsureFlashlight()
    {
        if (_camera == null)
            return;
        if (_flashlight == null || !IsInstanceValid(_flashlight))
        {
            _flashlight = new SpotLight3D
            {
                Name = "DevFlashlight",
                SpotRange = DevSpotRange,
                SpotAngle = 32f,
                SpotAngleAttenuation = BeamAngleAttenuation,
                SpotAttenuation = 0.8f,
                LightEnergy = DevSpotEnergy,
                ShadowEnabled = true,
                ShadowCasterMask = ~(OwnBodyLayer | BlockLayer), // the owner (2026-10-29): shone down, it drew the own body's shadow before it
                LightCullMask = ~HeldLayer,
                LightSpecular = 0f,
                LightVolumetricFogEnergy = 0f,
                Visible = _flashlightOn || _ownFlashlight,
            };
        }

        if (_flashlight.GetParent() != _camera)
        {
            _flashlight.GetParent()?.RemoveChild(_flashlight);
            _camera.AddChild(_flashlight);
        }
    }

    private void Detach()
    {
        if (_replica != null)
        {
            _replica.LightChanged -= OnLightChanged;
            _replica.StarlightChanged -= OnStarlightChanged;
            _replica.CellsChanged -= OnCellsChanged;
        }

        if (_feed != null)
            _feed.Despawned -= OnDespawned;
        _feed = null;
        _replica = null;
        _byZ.Clear();
        _lit.Clear();
        _active = null;
        foreach (SpotLight3D spot in _beams.Values)
            spot.QueueFree();
        _beams.Clear();
        _beamShadows = 0;
        _effectShadows = 0;
        foreach (Lamp lamp in _lamps.Values)
            lamp.Light.QueueFree();
        _lamps.Clear();
        _instant.Clear();
        _exteriorDirty = _exteriorFull = true;
        _skyByZ.Clear();
        foreach (MultiMeshInstance3D node in _doorBlocks.Values)
            node.Multimesh.InstanceCount = 0;
        _doorBlockCells = null;
    }
}
