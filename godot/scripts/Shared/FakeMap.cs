using System.Linq;
using System.Globalization;
using Anomaly.Client.Shared.World;
using Anomaly.Core.Tg;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared;

// Owner: the contract (these members and their meaning) is the M3 contracts step's and frozen (docs/ai/specs/m3-ownership.md);
// M3 P6 implements it; M3 P7 codes against it too. Engine-free: compiled into the client and, linked, into
// tests/Anomaly.Server.Tests (Client/).

/// <summary>One type whose things draw tg's <c>post_init_icon_state</c> (<see cref="FakeMap.PostInitChanges"/>): the editor's preview state, the state drawn, and the things changed.</summary>
public readonly record struct PostInitChange(string Preview, string Drawn, int Count);

/// <summary>
/// One movable of a <see cref="FakeMap"/> as the fake server replicates it: what the real server would send for it after
/// init, read without booting tg from the type's generated class (its constructor defaults, which never touch a world:
/// rules 10.2) and the map's overrides of the same vars.
/// </summary>
/// <param name="Id">The fake world id, from 1 in the file's member order.</param>
/// <param name="TypePath">The DM type path.</param>
/// <param name="X">The tile x.</param>
/// <param name="Y">The tile y.</param>
/// <param name="Z">The level.</param>
/// <param name="Dir">BYOND <c>dir</c>.</param>
/// <param name="Name">tg's <c>name</c>.</param>
/// <param name="Icon">The icon path (<c>icons/obj/doors/airlocks/station/public.dmi</c>), or null.</param>
/// <param name="IconState">The icon state, or null.</param>
/// <param name="PixelX">BYOND <c>pixel_x</c>.</param>
/// <param name="PixelY">BYOND <c>pixel_y</c>.</param>
/// <param name="Layer">BYOND <c>layer</c>.</param>
/// <param name="Density">tg's <c>density</c>.</param>
/// <param name="Opacity">tg's <c>opacity</c>.</param>
/// <param name="OnBorder">Whether <c>flags_1</c> has <c>ON_BORDER_1</c>.</param>
/// <param name="Light">The light (<see cref="LightState"/> from the light vars, <c>light_on</c> as mapped).</param>
/// <param name="Plane">M3 R5 (D122): BYOND <c>plane</c>, as the real server's appearance carries it (tg's floor plane -6 for what lies in the floor layers, the game plane -4 for the rest; 0 when unknown).</param>
public readonly record struct FakeMapEntity(long Id, string TypePath, int X, int Y, int Z, byte Dir, string Name, string Icon, string IconState,
    short PixelX, short PixelY, float Layer, bool Density, bool Opacity, bool OnBorder, LightState Light, float Plane = 0f, bool DirSet = false);

/// <summary>What <see cref="FakeMap.Bind"/> builds against one set of appearance tables.</summary>
/// <param name="Grid">The version-3 MapGrid (its appearance runs index the tables).</param>
/// <param name="EntityAppearances">The appearance of each entity of <see cref="FakeMap.Entities"/>, in the same order.</param>
public sealed record FakeMapBinding(MapGrid Grid, IReadOnlyList<TableRef> EntityAppearances);

/// <summary>
/// The FakeFeed version-3 source (scope 3, P6 and P7 before M3 P2's loader exists): a converted map
/// (<c>docs/ai/specs/map-format.md</c>; the scout's probe output <c>meta.as3dmap.b</c> is one) read and turned into what the
/// real server's join would carry, without a tg world: no init runs, so spawners, decals and helpers are not resolved
/// (except that the window spawners are replaced by the window and grille they make, so that a window has a picture and a
/// face to draw), and only what D115 shows (<c>invisibility</c> up to 25) becomes an entity.
/// <see cref="FakeServerOptions.Map"/> makes the fake server serve it.
/// <para>
/// The file is read by a private reader (<see cref="FakeMapReader"/>) of the same grammar, because the engine reader
/// (<see cref="MapText"/>) is M3 P2's; <see cref="Source"/> gives its result once it exists. The types' defaults come from the
/// generated classes (<see cref="ReflectionTypeSource"/>), merged with the few post-init facts of <see cref="PathRuleTypeSource"/> (<see cref="MergedTypeSource"/>).
/// Walls, full-tile windows and tables get tg's smoothing junction in their icon state (<see cref="Smoothing"/>), as SSicon_smooth
/// gives them on the real server.
/// </para>
/// </summary>
public sealed class FakeMap
{
    private const float MaxEntityInvisibility = 25f; // ReplicatedFields.MaxEntityInvisibility (D115)

    private readonly string _text;
    private readonly string _sourceName;
    private ParsedMapText _source;
    private readonly List<FakeMapEntity> _entities = new();
    private readonly List<(int X, int Y, int Z)> _spawnCells = new();
    private readonly RawModel[] _cells;
    private readonly Dictionary<RawModel, Info> _infos = new();
    private readonly ITypeSource _types;
    private readonly int _width, _height, _levels;
    private readonly AppearanceTables _ownTables = new();
    private FakeMapBinding _ownBinding;

    // What the grid and the entities need of one cell, derived once per distinct model.
    private sealed class Info
    {
        public RawMember Turf;
        public string Area;
        public TypeDefaults TurfDefaults;
        public TurfKind TurfKind;
        public List<Piece> Movables = new();
        public bool Solid, Opaque;
        public byte Faces;

        /// <summary>The turf's <c>underfloor_accessibility</c> (D122): what shows of the undertile things in the cell.</summary>
        public int Access;
    }

    // One movable of a model after the spawner expansion: its member (for overrides) and defaults.
    private sealed class Piece
    {
        public string Path;
        public RawMember Member;
        public TypeDefaults Defaults;
    }

    private FakeMap(string text, string sourceName, ITypeSource types)
    {
        _text = text;
        _sourceName = sourceName;
        _types = types;
        RawMap raw = FakeMapReader.Parse(text, sourceName);
        Warnings = raw.Warnings;
        _width = raw.Width;
        _height = raw.Height;
        _levels = raw.Levels;
        _cells = new RawModel[checked(_width * _height * _levels)];
        Array.Fill(_cells, raw.Default);
        foreach ((int x, int y, int z, RawModel model) in raw.Lines)
            _cells[Index(x, y, z)] = model;
        Build(raw);
        if (int.TryParse(System.Environment.GetEnvironmentVariable("ANOMALY_FAKE_FIRE"), out int fire))
            AddFire(fire);
    }

    // ANOMALY_FAKE_FIRE=N (scripted shots and frame measurements): tg's hotspots on N by N tiles around the first spawn cell, heavy in the middle and light at the rim.
    private void AddFire(int size)
    {
        if (_spawnCells.Count == 0)
            return;
        (int cx, int cy, int z) = _spawnCells[0];
        // ANOMALY_FAKE_FIRE_AT=X,Y,Z burns around that cell instead (a fire over open space on the multi-z map)
        if (System.Environment.GetEnvironmentVariable("ANOMALY_FAKE_FIRE_AT")?.Split(',') is { Length: 3 } at
            && int.TryParse(at[0], out int ax) && int.TryParse(at[1], out int ay) && int.TryParse(at[2], out int az))
            (cx, cy, z) = (ax, ay, az);
        long id = _entities.Count == 0 ? 1 : _entities[^1].Id + 1;
        var light = new LightState(3f, 1f, 0xFAA019FFu, true, 360f, 2, 1f, 0f, 0f, 2);
        for (int y = cy - size / 2; y < cy - size / 2 + size; y++)
        {
            for (int x = cx - size / 2; x < cx - size / 2 + size; x++)
            {
                int ring = Math.Max(Math.Abs(x - cx), Math.Abs(y - cy));
                string state = ring * 3 < size ? "heavy" : ring * 3 < size * 3 / 2 ? "medium" : "light";
                if (x >= 1 && y >= 1 && x <= _width && y <= _height)
                    _entities.Add(new FakeMapEntity(id++, "/obj/effect/hotspot", x, y, z, 2, "fire", "icons/effects/fire.dmi", state, 0, 0, 5f, false, false, false, light));
            }
        }
    }

    /// <summary>Reads a converted map file.</summary>
    public static FakeMap Load(string path) => Parse(System.IO.File.ReadAllText(path), path);

    /// <summary>Reads a converted map's text (tests).</summary>
    public static FakeMap Parse(string text, string sourceName) => Parse(text, sourceName, DefaultTypes());

    /// <summary>Reads a map's text with the given source of type defaults (tests).</summary>
    public static FakeMap Parse(string text, string sourceName, ITypeSource types) => new(text, sourceName, types);

    /// <summary>
    /// The type source <see cref="Load"/> uses: the generated classes (loaded by naming their assembly, so the same in the game and in a test),
    /// the truth for the vars they set, merged field by field with the path rules' post-init facts (<see cref="MergedTypeSource"/>), and the plain
    /// placeholder for a type with neither.
    /// </summary>
    public static ITypeSource DefaultTypes() => new MergedTypeSource(new ReflectionTypeSource(), new PathRuleTypeSource(specificOnly: true), new PathRuleTypeSource());

    /// <summary>The map's name as the server knows it (the file's name in lower case, <c>metastation</c>): its map patches' name.</summary>
    public string MapName => string.IsNullOrEmpty(_sourceName) ? null : System.IO.Path.GetFileNameWithoutExtension(_sourceName).ToLowerInvariant();

    /// <summary>The parsed file as the engine reader (<see cref="MapText"/>) reads it. Throws until M3 P2's reader exists.</summary>
    public ParsedMapText Source => _source ??= MapText.Parse(_text, _sourceName);

    /// <summary>Lines the reader had to repair (a cut-off override list); empty for a file the converter wrote.</summary>
    public IReadOnlyList<string> Warnings { get; private set; } = Array.Empty<string>();

    /// <summary>The map's width (tiles).</summary>
    public int Width => _width;

    /// <summary>The map's height.</summary>
    public int Height => _height;

    /// <summary>The map's levels.</summary>
    public int Levels => _levels;

    /// <summary>
    /// The version-3 MapGrid the fake server sends: the turf types, the turf appearances (by the turf types' icons and
    /// states, interned into the tables the fake server's Snapshot carries), solidity (dense turfs and dense non-border
    /// movables), the flags (faces of dense <c>ON_BORDER_1</c> movables by <c>dir</c>, opacity, starlight on space cells next
    /// to non-space ones), the base lights of the areas' types and tg's default starlight.
    /// </summary>
    public MapGrid MapGrid => Own.Grid;

    /// <summary>The entities, in id order.</summary>
    public IReadOnlyList<FakeMapEntity> Entities => _entities;

    private readonly Dictionary<string, PostInitChange> _postInit = new();

    /// <summary>
    /// The types whose things draw their <c>post_init_icon_state</c> instead of the state the map editor previews (M3 I1c, tg's init: atoms_initializing_EXPENSIVE.dm:138-139), by type path, with the preview
    /// state, the state drawn and how many things of the map it changed.
    /// </summary>
    public IReadOnlyDictionary<string, PostInitChange> PostInitChanges => _postInit;

    private void NotePostInit(string path, string preview, string drawn)
    {
        _postInit.TryGetValue(path, out PostInitChange known);
        _postInit[path] = known.Count == 0 ? new PostInitChange(preview, drawn, 1) : known with { Count = known.Count + 1 };
    }

    /// <summary>The cells a player may spawn on: the turfs of the late-join landmarks (<c>/obj/effect/landmark/latejoin</c>), else every <c>start</c> landmark's.</summary>
    public IReadOnlyList<(int X, int Y, int Z)> SpawnCells => _spawnCells;

    /// <summary>The tables <see cref="MapGrid"/> was interned into (an addition: the server uses <see cref="Bind"/> with its own).</summary>
    public AppearanceTables Tables
    {
        get
        {
            _ = Own;
            return _ownTables;
        }
    }

    /// <summary>The appearance of each entity in the tables <see cref="Tables"/> (an addition).</summary>
    public IReadOnlyList<TableRef> EntityAppearances => Own.EntityAppearances;

    private FakeMapBinding Own => _ownBinding ??= Bind(_ownTables);

    /// <summary>Builds the MapGrid and the entities' appearances against <paramref name="tables"/> (an addition: the fake server owns its tables).</summary>
    public FakeMapBinding Bind(AppearanceTables tables)
    {
        var grid = new MapGrid { Tick = 0, Width = _width, Height = _height, Levels = _levels };
        int cells = _cells.Length;

        // The turf types in order of first appearance, and the cells as runs.
        var typeIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        var typeOfCell = new int[cells];
        var solid = new byte[(cells + 7) / 8];
        var flagsOfCell = new byte[cells];
        var appearanceOfCell = new uint[cells];
        var appearanceCache = new Dictionary<(string, string), uint>();

        // Which cells smooth with which (tg's smoothing groups, reduced to what the scripted map needs).
        var wallish = new bool[cells];
        var windowish = new bool[cells];
        var tableFamily = new int[cells];
        var families = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < cells; i++)
        {
            Info info = InfoOf(_cells[i]);
            bool wall = info.TurfKind == TurfKind.Closed;
            bool door = false, window = false;
            foreach (Piece piece in info.Movables)
            {
                if (PropKinds.IsDoor(piece.Path))
                    door = true;
                if (PropKinds.IsFullTileWindow(piece.Path))
                    window = true;
                if (PropKinds.IsTable(piece.Path))
                {
                    string family = PropKinds.TableFamily(piece.Path);
                    if (!families.TryGetValue(family, out int id))
                        families[family] = id = families.Count + 1;
                    tableFamily[i] = id;
                }
            }
            wallish[i] = wall || door || window;
            windowish[i] = window || wall;
        }

        bool Joins(bool[] set, int x, int y, int z) => x >= 1 && y >= 1 && x <= _width && y <= _height && set[Index(x, y, z)];

        for (int z = 1; z <= _levels; z++)
        {
            for (int y = 1; y <= _height; y++)
            {
                for (int x = 1; x <= _width; x++)
                {
                    int i = Index(x, y, z);
                    Info info = InfoOf(_cells[i]);
                    string turfPath = info.Turf.Path;
                    if (CeilingSystem.DefaultPresent(turfPath, info.Area))
                        grid.Ceilings.Add(new CeilingChange(x, y, z, Anomaly.Core.Motion.CeilingState.Of(Anomaly.Core.Motion.CeilingState.Plating)));
                    if (!typeIndex.TryGetValue(turfPath, out int type))
                    {
                        type = typeIndex.Count;
                        typeIndex[turfPath] = type;
                        grid.TurfTypes.Add(turfPath);
                    }
                    typeOfCell[i] = type;
                    if (info.Solid)
                        solid[i >> 3] |= (byte)(1 << (i & 7));
                    byte flags = info.Faces;
                    if (info.Opaque)
                        flags |= CellFlags.Opaque;
                    if (info.TurfKind == TurfKind.Void && NextToNonSpace(x, y, z))
                        flags |= CellFlags.Starlight;
                    flagsOfCell[i] = flags;

                    // The turf's picture: smoothed walls get their junction state.
                    TypeDefaults d = info.TurfDefaults;
                    string state = d?.IconState;
                    if (d?.BaseIconState != null && info.TurfKind == TurfKind.Closed)
                        state = Smoothing.StateOf(d.BaseIconState, Smoothing.Junction(x, y, (nx, ny) => Joins(wallish, nx, ny, z)));
                    if (d != null && (d.Icon != null || state != null) && info.TurfKind != TurfKind.Void)
                    {
                        var key = (d.Icon ?? string.Empty, state ?? string.Empty);
                        if (!appearanceCache.TryGetValue(key, out uint reference))
                        {
                            reference = tables.Appearance(d.Icon, state, d.Layer).Index;
                            appearanceCache[key] = reference;
                        }
                        appearanceOfCell[i] = reference;
                    }
                }
            }
        }
        AddRuns(grid.Runs, typeOfCell);
        AddRuns(grid.AppearanceRuns, appearanceOfCell);
        AddRuns(grid.FlagRuns, flagsOfCell);
        grid.BaseLightRuns.Add(new TurfRun(cells, 0)); // DV22: no area base light is sent for the scripted map (space stays dark)
        grid.SolidBits = solid;
        grid.Starlight = new LightState(2f, 1f, 0x8589FAFF, true, 360f, 0, -0.5f, 0f, 0f, 1);

        // The entities' appearances, with the smoothing junction of windows and tables.
        var appearances = new TableRef[_entities.Count];
        string[] fireExtras = System.Environment.GetEnvironmentVariable("ANOMALY_FAKE_FIRE_EXTRAS")?.Split(',');
        for (int i = 0; i < _entities.Count; i++)
        {
            FakeMapEntity e = _entities[i];
            string state = e.IconState;
            int cell = Index(e.X, e.Y, e.Z);
            if (_smoothBase.TryGetValue(e.Id, out string baseState))
            {
                int junction = PropKinds.IsTable(e.TypePath)
                    ? Smoothing.Junction(e.X, e.Y, (nx, ny) => nx >= 1 && ny >= 1 && nx <= _width && ny <= _height && tableFamily[Index(nx, ny, e.Z)] == tableFamily[cell] && tableFamily[cell] != 0)
                    : Smoothing.Junction(e.X, e.Y, (nx, ny) => Joins(windowish, nx, ny, e.Z));
                state = Smoothing.StateOf(baseState, junction);
            }
            appearances[i] = tables.Appearance(e.Icon, state, e.Layer, e.PixelX, e.PixelY, 0, (short)e.Plane);
            if (fireExtras != null && e.TypePath == StaticPropRule.HotspotType)
            {
                // ANOMALY_FAKE_FIRE_EXTRAS=sparkle,lightning,fusion: tg's overlays of a very hot fire (update_color), for shots
                var hot = new AppearanceEntry { Icon = tables.Resource(e.Icon), IconState = tables.String(state), Layer = e.Layer };
                foreach ((string name, string icon, string overlay) in new[] { ("sparkle", "icons/effects/effects.dmi", "shieldsparkles"), ("lightning", "icons/effects/fire.dmi", "overcharged"), ("fusion", "icons/effects/atmospherics.dmi", "fusion_gas") })
                {
                    if (fireExtras.Contains(name))
                        hot.Overlays.Add(tables.Appearance(icon, overlay, e.Layer));
                }
                appearances[i] = tables.Add(hot);
            }
        }
        return new FakeMapBinding(grid, appearances);
    }

    /// <summary>
    /// tg's undertile element on a turf (D122): the thing is invisible (so the real server does not replicate it) while the turf's floor covers it, which is when its
    /// <c>underfloor_accessibility</c> is under <c>UNDERFLOOR_VISIBLE</c>: under every floor tile and in every wall. Plating, space, glass and catwalk floors show it.
    /// </summary>
    internal static bool IsUnderTile(string path, int accessibility) => PropKinds.HasUndertile(path) && !PropKinds.TileAccess.Shows(accessibility);

    private bool NextToNonSpace(int x, int y, int z)
    {
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                int nx = x + dx, ny = y + dy;
                if ((dx != 0 || dy != 0) && nx >= 1 && ny >= 1 && nx <= _width && ny <= _height && InfoOf(_cells[Index(nx, ny, z)]).TurfKind != TurfKind.Void)
                    return true;
            }
        }
        return false;
    }

    private readonly Dictionary<long, string> _smoothBase = new();

    private static void AddRuns<T>(List<TurfRun> runs, T[] values) where T : struct, IConvertible
    {
        int runStart = 0;
        for (int i = 1; i <= values.Length; i++)
        {
            if (i == values.Length || !values[i].Equals(values[runStart]))
            {
                runs.Add(new TurfRun(i - runStart, Convert.ToInt32(values[runStart], CultureInfo.InvariantCulture)));
                runStart = i;
            }
        }
    }

    private int Index(int x, int y, int z) => (x - 1) + (y - 1) * _width + (z - 1) * _width * _height;

    private Info InfoOf(RawModel model)
    {
        if (_infos.TryGetValue(model, out Info info))
            return info;
        info = new Info();
        foreach (RawMember member in model.Members)
        {
            if (member.Path.StartsWith("/area", StringComparison.Ordinal))
            {
                info.Area = member.Path;
                continue;
            }
            if (member.Path.StartsWith("/turf", StringComparison.Ordinal))
            {
                info.Turf ??= member; // the first turf is the real one, the rest are piled beneath it
                continue;
            }
            foreach (string path in PropKinds.ExpandSpawner(member.Path))
            {
                _types.TryGet(path, out TypeDefaults defaults);
                info.Movables.Add(new Piece { Path = path, Member = member, Defaults = defaults });
            }
        }
        info.Turf ??= new RawMember { Path = "/turf/open/space/basic", Overrides = Array.Empty<(string, string)>() };
        _types.TryGet(info.Turf.Path, out TypeDefaults turf);
        info.TurfDefaults = turf;
        info.Access = turf?.UnderfloorAccessibility ?? PropKinds.TileAccess.Hidden;
        info.TurfKind = TurfStyle.Classify(info.Turf.Path);
        info.Solid = info.TurfKind == TurfKind.Closed || (turf?.Density ?? false);
        info.Opaque = info.TurfKind == TurfKind.Closed || (turf?.Opacity ?? false);
        foreach (Piece piece in info.Movables)
        {
            TypeDefaults d = piece.Defaults;
            if (d == null || d.Invisibility > MaxEntityInvisibility || IsUnderTile(piece.Path, info.Access))
                continue;
            bool density = (piece.Member.Number("density") ?? (d.Density ? 1f : 0f)) != 0f;
            bool opacity = (piece.Member.Number("opacity") ?? (d.Opacity ? 1f : 0f)) != 0f;
            if (density && !d.OnBorder)
                info.Solid = true;
            if (opacity)
                info.Opaque = true;
            if (density && d.OnBorder)
                info.Faces |= (byte)(DirOf(piece, d) & CellFlags.Faces);
        }
        _infos[model] = info;
        return info;
    }

    private static byte DirOf(Piece piece, TypeDefaults d)
    {
        float? dir = piece.Member.Number("dir");
        return dir.HasValue ? (byte)dir.Value : d.Dir;
    }

    private void Build(RawMap raw)
    {
        var latejoin = new List<(int, int, int)>();
        var starts = new List<(int, int, int)>();
        long id = 0;
        // tg's cables link to the cables of the same layer beside them in their Initialize (connect_cable): the fake feed does it here, hidden cables included.
        var cableCells = new HashSet<(int X, int Y, int Z, int Layer)>();
        foreach ((int cx, int cy, int cz, RawModel cableModel) in raw.Lines)
        {
            foreach (Piece cable in InfoOf(cableModel).Movables)
            {
                if (CableLayerOf(cable.Path) != 0)
                    cableCells.Add((cx, cy, cz, CableLayerOf(cable.Path)));
            }
        }
        foreach ((int x, int y, int z, RawModel model) in raw.Lines)
        {
            Info info = InfoOf(model);
            foreach (RawMember member in model.Members)
            {
                if (member.Path.StartsWith("/obj/effect/landmark/latejoin", StringComparison.Ordinal))
                    latejoin.Add((x, y, z));
                else if (member.Path.StartsWith("/obj/effect/landmark/start", StringComparison.Ordinal))
                    starts.Add((x, y, z));
            }
            foreach (Piece piece in info.Movables)
            {
                TypeDefaults d = piece.Defaults;
                if (d == null || d.Invisibility > MaxEntityInvisibility || IsUnderTile(piece.Path, info.Access))
                    continue;
                RawMember m = piece.Member;
                byte dir = DirOf(piece, d);
                string name = m.Text("name") ?? d.Name ?? NameOf(piece.Path);
                // tg's init sets icon_state to post_init_icon_state when the type has one (atoms_initializing_EXPENSIVE.dm:138-139), over the map editor's preview state and a map's own
                // override: a meter is "meter" in the game and "meterX" in the editor (M3 I1c).
                string previewState = m.Text("icon_state") ?? d.IconState;
                string state = d.StateAfterInit(m.Text("icon_state"));
                if (state != previewState)
                    NotePostInit(piece.Path, previewState, state);
                // A greyscale type draws the generated icon after init (the resource gags:<config>|<colours> the real server replicates), the map's own greyscale_colors over the class's.
                string icon = d.IconAfterInit(m.Text("greyscale_colors"));
                if (m.Text("icon_state") == null && CableLayerOf(piece.Path) is int cableLayer and not 0)
                    state = CableState(cableCells, x, y, z, cableLayer, info);
                // tg's random posters pick a poster in their Initialize (poster.dm:187); the fake feed does the same, by a hash of the place, instead of showing the
                // placeholder state random_anything (a magenta checker). A map's own icon_state override keeps its state.
                if (d.RandomBaseType != null && m.Text("icon_state") == null && _types is IRandomTypeSource random)
                {
                    IReadOnlyList<TypeDefaults> choices = random.RandomChoices(d.RandomBaseType, RandomExcluded);
                    if (choices.Count > 0)
                    {
                        TypeDefaults pick = choices[(int)(((uint)x * 73856093u ^ (uint)y * 19349663u ^ (uint)z * 83492791u ^ (uint)id * 2654435761u) % (uint)choices.Count)];
                        state = pick.StateAfterInit(null);
                        icon = pick.Icon ?? icon;
                        name = m.Text("name") ?? pick.Name ?? name;
                    }
                }
                short px = (short)(m.Number("pixel_x") ?? d.PixelX);
                short py = (short)(m.Number("pixel_y") ?? d.PixelY);
                bool density = (m.Number("density") ?? (d.Density ? 1f : 0f)) != 0f;
                bool opacity = (m.Number("opacity") ?? (d.Opacity ? 1f : 0f)) != 0f;
                id++;
                // Only the entities that have a joiner below smooth: the tables and the full-tile windows (the rest, a grille or a girder, keep their state).
                if (d.BaseIconState != null && m.Text("icon_state") == null && (PropKinds.IsTable(piece.Path) || PropKinds.IsFullTileWindow(piece.Path)))
                    _smoothBase[id] = d.BaseIconState;
                // tg's undertile element (D122): a thing under a glass floor or a covered catwalk (accessibility "visible") moves to the floor plane at BELOW_CATWALK_LAYER, if it was not on it
                // already (undertile.dm:hide); on plating and in space it keeps its own plane and layer.
                float plane = d.Plane, layer = d.Layer;
                // tg's atmospherics machines shift their layer by their piping layer (pipes.dm:151: (piping_layer - PIPING_LAYER_DEFAULT) * PIPING_LAYER_LCHANGE, 0.001 each) when they
                // update it in Initialize, and by their colour's place in the list (0.0001 each): two pipes of different layers on one tile cross in a fixed order.
                float shift = PropKinds.Is(piece.Path, "/obj/machinery/atmospherics") ? (d.PipingLayer - 3) * 0.001f + PipeColorIndex(m.Text("pipe_color") ?? d.PipeColor) * 0.0001f : 0f;
                layer += shift;
                // tg's undertile element (D122): a thing under a glass floor or a covered catwalk (accessibility "visible") moves to the floor plane at BELOW_CATWALK_LAYER, if it was not on it
                // already (undertile.dm:hide); on plating and in space it keeps its own plane and layer.
                if (PropKinds.HasUndertile(piece.Path) && info.Access == PropKinds.TileAccess.Visible && FloorLayers.TruePlane(plane) != FloorLayers.FloorPlane)
                {
                    plane = FloorLayers.FloorPlane;
                    layer = FloorLayers.TopdownLayer + FloorLayers.BelowCatwalkOffset + shift;
                }
                // (DirSet: the map says the way it faces, which the models' own facing rule then leaves alone, DV508)
                _entities.Add(new FakeMapEntity(id, piece.Path, x, y, z, dir, name, icon, state, px, py, layer, density, opacity, d.OnBorder, d.Light, plane, m.Number("dir") != null));
            }
        }
        // Spawn on the late-join turfs, else on the start landmarks (the order of the file).
        List<(int, int, int)> chosen = latejoin.Count > 0 ? latejoin : starts;
        foreach ((int x, int y, int z) in chosen)
        {
            if (!InfoOf(_cells[Index(x, y, z)]).Solid)
                _spawnCells.Add((x, y, z));
        }
    }

    // The poster types a random poster never becomes: tg's own blacklist (traitor, abductor) and the contraband family, which the path rules hide.
    private static readonly string[] RandomExcluded = { "/obj/structure/sign/poster/traitor", "/obj/structure/sign/poster/abductor", "/obj/structure/sign/poster/contraband" };

    // The place of a pipe colour in GLOB.pipe_colors_ordered (the real list's order is tg's; the fake feed only needs two colours to differ): a stable number from 0 to 31 of the colour text.
    private static int PipeColorIndex(string color)
    {
        if (string.IsNullOrEmpty(color))
            return 0;
        uint hash = 2166136261;
        foreach (char c in color)
            hash = (hash ^ c) * 16777619;
        return (int)(hash % 32);
    }

    /// <summary>tg's cable layers (cable.dm: <c>CABLE_LAYER_1</c> 1, <c>_2</c> 2, <c>_3</c> 4) of a type in the cable family that links by neighbours; 0 for any other type (the multilayer hubs keep their own picture).</summary>
    internal static int CableLayerOf(string path)
    {
        if (!PropKinds.Is(path, "/obj/structure/cable") || PropKinds.Is(path, "/obj/structure/cable/multilayer"))
            return 0;
        return path.Contains("/layer1", StringComparison.Ordinal) ? 1 : path.Contains("/layer3", StringComparison.Ordinal) ? 4 : 2;
    }

    // tg: /obj/structure/cable/get_dir_string (cable.dm:172) over the dirs of the neighbours that carry a cable of the same layer (the cardinals in N, S, E, W order), with the node
    // dot where the cell has something that wants one (a power machine, a grille, a reinforced table: update_icon_state, cable.dm:186).
    private static string CableState(HashSet<(int X, int Y, int Z, int Layer)> cables, int x, int y, int z, int layer, Info info)
    {
        var links = new List<string>();
        if (cables.Contains((x, y + 1, z, layer)))
            links.Add("1");
        if (cables.Contains((x, y - 1, z, layer)))
            links.Add("2");
        if (cables.Contains((x + 1, y, z, layer)))
            links.Add("4");
        if (cables.Contains((x - 1, y, z, layer)))
            links.Add("8");
        if (links.Count == 0)
            return $"l{layer}-noconnection";
        bool node = info.Movables.Any(p => PropKinds.Is(p.Path, "/obj/machinery/power") || PropKinds.Is(p.Path, "/obj/structure/grille") || PropKinds.Is(p.Path, "/obj/structure/table/reinforced"));
        return links.Count == 1 || !node ? $"l{layer}-{string.Join("-", links)}" : $"l{layer}-{string.Join("-", links)}-node";
    }

    private static string NameOf(string path) => path.Substring(path.LastIndexOf('/') + 1).Replace('_', ' ');
}
