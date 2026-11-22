using System.Linq;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/// <summary>Independent upper boundaries. Never stored on a replaceable turf or in its contents.</summary>
public sealed partial class CeilingSystem
{
    private readonly World _world;
    private readonly Dictionary<(int X, int Y, int Z), CeilingState> _cells = new();
    private readonly HashSet<(int X, int Y, int Z)> _supportDirty = new();
    private readonly Dictionary<(int X, int Y, int Z), CeilingState> _underShuttle = new();
    // DV507 on a level under another: the floor tile laid under the plating of the turf above (ShownAt), by the map or in a round with a floor
    // tile from below (Interact). The cell has no ceiling of its own there (D325), so this is no part of the support, the air or the damage rules.
    private readonly Dictionary<(int X, int Y, int Z), CeilingState> _laidUnder = new();
    // Those of them whose turf above has plating now: found in the world's own context (a map or a template loaded, a turf changed), for what reads
    // the shown ceilings reads them between the world's steps (the replication).
    private readonly HashSet<(int X, int Y, int Z)> _laidShown = new();
    public event Action<int, int, int> Changed;
    public bool Initialized { get; private set; }
    public IReadOnlyDictionary<(int X, int Y, int Z), CeilingState> Cells => _cells;
    private CeilingSystem(World world)
    {
        _world = world;
        world.AddStateHasher(nameof(CeilingSystem), static (World w, ref StateHasher h) => Of(w).Hash(ref h));
    }
    private void Hash(ref StateHasher h)
    {
        if (!Initialized) return;
        h.Add(Initialized);
        foreach (var entry in _cells.OrderBy(e => e.Key.Z).ThenBy(e => e.Key.Y).ThenBy(e => e.Key.X))
        {
            h.Add(entry.Key.X); h.Add(entry.Key.Y); h.Add(entry.Key.Z);
            h.Add(entry.Value.Turf ?? ""); h.Add((int)entry.Value.Lattice); h.Add((int)entry.Value.Broken);
            h.Add(entry.Value.Supported); h.Add((int)entry.Value.Damage);
        }
        foreach (var entry in _underShuttle.OrderBy(e => e.Key.Z).ThenBy(e => e.Key.Y).ThenBy(e => e.Key.X))
        {
            h.Add(entry.Key.X); h.Add(entry.Key.Y); h.Add(entry.Key.Z); h.Add(entry.Value.Turf ?? "");
        }
        foreach (var entry in _laidUnder.OrderBy(e => e.Key.Z).ThenBy(e => e.Key.Y).ThenBy(e => e.Key.X))
        {
            h.Add(entry.Key.X); h.Add(entry.Key.Y); h.Add(entry.Key.Z); h.Add(entry.Value.Turf ?? "");
        }
    }
    public static CeilingSystem Of(World world) => world.GetExtension(static w => new CeilingSystem(w));
    public CeilingState At(int x, int y, int z) => _cells.GetValueOrDefault((x, y, z));

    /// <summary>
    /// What is drawn over a cell: its ceiling (<see cref="At"/>); or, under a level above, the floor tile its map laid under the plating of the turf
    /// over it (DV507), for as long as that turf has plating to lie under: a floor that is not glass, or a wall.
    /// </summary>
    public CeilingState ShownAt(int x, int y, int z)
        => _cells.TryGetValue((x, y, z), out CeilingState own) ? own : _laidShown.Contains((x, y, z)) ? _laidUnder[(x, y, z)] : default;

    /// <summary>The cells <see cref="ShownAt"/> may show something over.</summary>
    public IEnumerable<(int X, int Y, int Z)> ShownCells => _cells.Keys.Concat(_laidUnder.Keys);

    // The turf of the level above stands or lies on plating: tg's glass floors have none (their base is the open space under them), nor has open space.
    private bool HasPlatingOver(int x, int y, int z)
        => MapView.TurfAt(_world, x, y, z) is turf t && GET_TURF_ABOVE(t) is turf above
            && above is not (turf_open_space or turf_open_openspace or turf_open_floor_glass);

    // The tile a map lays under the plating of the turf above: a floor's type that is neither plating itself nor glass. The var's other words
    // (none, a lattice, a catwalk) say nothing under a level above, where the turf above is the cell's boundary.
    private CeilingState LaidUnderOf(turf t)
        => MapCeiling(t, out CeilingState asked) && asked.Sealed && !asked.IsGlass && Proto(asked.Turf) is not turf_open_floor_plating ? asked : default;

    // What a cell under a level above has for a ceiling, seen from below: the tile laid under the plating of the turf over it, else that plating;
    // nothing where the turf over it has none (open space, glass).
    private CeilingState UnderLevel(int x, int y, int z)
        => !HasPlatingOver(x, y, z) ? default : _laidUnder.TryGetValue((x, y, z), out CeilingState laid) ? laid : CeilingState.Of(CeilingState.Plating);

    private void SetLaidUnder(int x, int y, int z, CeilingState laid)
    {
        var key = (x, y, z);
        bool same = _laidUnder.GetValueOrDefault(key) == laid, was = _laidShown.Contains(key), shown = laid.Exists && HasPlatingOver(x, y, z);
        if (laid.Exists) _laidUnder[key] = laid;
        else _laidUnder.Remove(key);
        if (shown) _laidShown.Add(key);
        else _laidShown.Remove(key);
        if (same && shown == was) return;
        _revisions[key] = _revisions.GetValueOrDefault(key) + 1;
        Changed?.Invoke(x, y, z);
    }

    // The turf over a cell changed: the tile laid under its plating is shown while it has plating (a floor made glass, or taken away, shows it no more).
    private void RefreshLaidUnder(int x, int y, int z)
    {
        var key = (x, y, z);
        if (!_laidUnder.ContainsKey(key) && !_laidShown.Contains(key)) return;
        bool shown = _laidUnder.ContainsKey(key) && HasPlatingOver(x, y, z);
        if (shown ? _laidShown.Add(key) : _laidShown.Remove(key)) Changed?.Invoke(x, y, z);
    }

    public void Initialize()
    {
        if (Initialized || MapView.Width(_world) == 0) return;
        Initialized = true;
        _world.AddPhase("ceiling supports", _ => RefreshSupport());
        for (int z = 1; z <= MapView.Levels(_world); z++)
        for (int y = 1; y <= MapView.Height(_world); y++)
        for (int x = 1; x <= MapView.Width(_world); x++)
        {
            turf t = MapView.TurfAt(_world, x, y, z);
            if (t == null) continue;
            if (!HasLevelAbove(t) && StartOf(t) is { Exists: true } start)
                _cells[(x, y, z)] = start;
            else if (HasLevelAbove(t) && LaidUnderOf(t) is { Exists: true } laid)
            {
                _laidUnder[(x, y, z)] = laid;
                if (HasPlatingOver(x, y, z)) _laidShown.Add((x, y, z));
            }
            UpdateExposure(x, y, z);
            _supportDirty.Add((x, y, z));
            Changed?.Invoke(x, y, z);
        }
    }

    /// <summary>Whether a turf starts under a plating ceiling: not in space, solars or tg's outdoor areas (Lavaland's surface, D304).
    /// D325: open space at the top of a stack in a station area is roofed too (tg's air stays in it: nothing is above it).</summary>
    public static bool DefaultPresent(string turfType, string areaType, bool outdoors = false)
        => !outdoors && turfType != null && (turfType.StartsWith("/turf/open", StringComparison.Ordinal)
            || turfType.StartsWith("/turf/closed", StringComparison.Ordinal))
            && !turfType.StartsWith("/turf/open/space", StringComparison.Ordinal)
            && !(areaType?.StartsWith("/area/space", StringComparison.Ordinal) ?? false)
            && !(areaType?.StartsWith("/area/station/solars", StringComparison.Ordinal) ?? false);

    /// <summary>
    /// D325: a turf of a level with a level above it (tg's <c>GET_TURF_ABOVE</c>) has no ceiling of its own: the turf above (a floor,
    /// open space) is its boundary, built and broken by tg's own code on that level, and the air goes up through open space by LINDA.
    /// </summary>
    public static bool HasLevelAbove(turf t) => t != null && GET_TURF_ABOVE(t) != null;

    public bool Set(int x, int y, int z, CeilingState state)
    {
        turf under = MapView.TurfAt(_world, x, y, z);
        if (under == null) return false;
        if (state.Exists && HasLevelAbove(under)) return false;
        if (!state.IsValid || state.Sealed && Proto(state.Turf) is not turf_open_floor) return false;
        CeilingState previous = At(x, y, z);
        state = state.Exists ? state with { Turf = state.Turf ?? "", Supported = previous.Exists ? previous.Supported : state.Supported } : default;
        if (previous == state) return true;
        if (state.Exists) _cells[(x, y, z)] = state;
        else _cells.Remove((x, y, z));
        _revisions[(x, y, z)] = _revisions.GetValueOrDefault((x, y, z)) + 1;
        UpdateExposure(x, y, z);
        if (previous.Exists != state.Exists)
        {
            _supportDirty.Add((x, y, z));
            foreach (var p in Neighbors(x, y, z)) _supportDirty.Add(p);
        }
        // DV577: the shots' dents went with the ceiling's tile (a crack keeps them, as a floor's break_tile keeps its dents)
        if (previous.Exists != state.Exists || previous.Sealed != state.Sealed || previous.Turf != state.Turf)
            ShotGeometry.ClearTopDents(under);
        Changed?.Invoke(x, y, z);
        return true;
    }

    /// <summary>
    /// D295: an explosion's severity on the ceiling, with tg's floor odds (<c>floor.dm:48</c>, <c>reinforced_floor.dm:55</c>);
    /// a lattice or catwalk alone is an obj (<c>obj_defense.dm</c> ex_act on its 50 integrity). The ceiling has no contents, so
    /// nothing shields it; the floor's own fire (hotspot_expose) stays with the floor below.
    /// </summary>
    public void ExAct(int x, int y, int z, float severity, object target)
    {
        CeilingState old = At(x, y, z);
        if (!old.Exists || severity <= 0) return;
        turf below = MapView.TurfAt(_world, x, y, z);
        if (!old.Sealed)
        {
            // tg: code/game/objects/obj_defense.dm:10  /obj/ex_act (target == src never holds: a blast is never aimed at the ceiling's lattice)
            float amount = severity switch
            {
                EXPLODE_DEVASTATE => float.PositiveInfinity,
                EXPLODE_HEAVY => Dm.num(rand(100, 250)),
                EXPLODE_LIGHT => Dm.num(rand(10, 90)),
                _ => 0,
            };
            if (amount > 0) DamageLattice(x, y, z, amount, BRUTE, BOMB, 0, false);
            return;
        }
        if (Dm.truthy(target)) severity = EXPLODE_LIGHT;
        if (Proto(old.Turf) is turf_open_floor_engine)
        {
            switch (severity)
            {
                case EXPLODE_DEVASTATE:
                    if (prob(80)) LatticeReplacement(x, y, z);
                    else if (prob(50)) Set(x, y, z, default);
                    else ScrapeAway(x, y, z);
                    break;
                case EXPLODE_HEAVY:
                    if (prob(50)) ScrapeAway(x, y, z);
                    break;
            }
            return;
        }
        switch (severity)
        {
            case EXPLODE_DEVASTATE:
                Set(x, y, z, default);
                break;
            case EXPLODE_HEAVY:
                switch (Dm.num(rand(1, 3)))
                {
                    case 1:
                        // a ceiling's second layer down is never a floor: tg's attempt_lattice_replacement
                        LatticeReplacement(x, y, z);
                        if (prob(33)) Dm.@new<obj_item_stack_sheet_iron>(below);
                        break;
                    case 2:
                        Set(x, y, z, default);
                        break;
                    case 3:
                        if (prob(80)) ScrapeAway(x, y, z);
                        else BreakTile(x, y, z);
                        if (prob(33)) Dm.@new<obj_item_stack_sheet_iron>(below);
                        break;
                }
                break;
            case EXPLODE_LIGHT:
                if (prob(50)) BreakTile(x, y, z);
                break;
        }
    }

    /// <summary>
    /// tg's <c>take_damage</c> (<c>atom_defense.dm:17</c>) on the ceiling's lattice or catwalk: its attack sound, tg's
    /// <c>run_atom_armor</c> with <c>/datum/armor/structure_lattice</c> (melee 50, nothing against a bomb), the damage kept in the
    /// state, and at no integrity tg's <c>deconstruct(FALSE)</c>, whose rods fall into the room below; the damage dealt.
    /// </summary>
    public float DamageLattice(int x, int y, int z, float amount, object damageType, string flag, float penetration, bool sound)
    {
        CeilingState old = At(x, y, z);
        if (old.Sealed || old.Lattice == CeilingLattice.None) return 0;
        turf below = MapView.TurfAt(_world, x, y, z);
        // tg: code/game/atom/atom_defense.dm:107  play_attack_sound
        if (sound && Dm.eq(damageType, BRUTE))
            playsound(below, Dm.resource(amount > 0 ? "sound/items/weapons/smash.ogg" : "sound/items/weapons/tap.ogg"), 50, TRUE);
        else if (sound && Dm.eq(damageType, BURN))
            playsound(below, Dm.resource("sound/items/tools/welder.ogg"), 100, TRUE);
        // tg: code/game/atom/atom_defense.dm:92  run_atom_armor (a lattice has no damage_deflection)
        if (!Dm.eq(damageType, BRUTE) && !Dm.eq(damageType, BURN)) return 0;
        float armor = flag == MELEE ? 50 : 0;
        if (armor != 0) armor = Math.Clamp(PENETRATE_ARMOUR(armor, penetration), Math.Min(armor, 0), 100);
        float dealt = float.IsPositiveInfinity(amount) ? amount : Dm.num(round(amount * (100 - armor) * 0.01f, DAMAGE_PRECISION));
        if (dealt < DAMAGE_PRECISION) return 0;
        float left = CeilingState.LatticeIntegrity - old.Damage / 10f - dealt;
        if (left > 0)
        {
            Set(x, y, z, old with { Damage = (ushort)Math.Round((CeilingState.LatticeIntegrity - left) * 10) });
            return dealt;
        }
        // tg: code/game/objects/obj_defense.dm:208  atom_destruction -> deconstruct(FALSE) -> lattice.dm:80 atom_deconstruct
        Set(x, y, z, default);
        Type lattice = old.Lattice == CeilingLattice.Catwalk ? typeof(obj_structure_lattice_catwalk) : typeof(obj_structure_lattice);
        Dm.@new(typeof(obj_item_stack_rods), below, Dm.num(Dm.initial(lattice, "number_of_mats")));
        return dealt;
    }

    /// <summary>tg's <c>ScrapeAway</c> on a ceiling's stack: a floor tile's turf leaves plating, plating or a lattice leaves nothing.</summary>
    public void ScrapeAway(int x, int y, int z)
    {
        CeilingState old = At(x, y, z);
        Set(x, y, z, old.Sealed && old.Turf != CeilingState.Plating ? CeilingState.Of(CeilingState.Plating) : default);
    }

    /// <summary>
    /// DV363: a singularity eats the ceiling's block as tg's <c>/turf/singularity_act</c> (<c>turf.dm:554</c>) eats a floor: one
    /// <see cref="ScrapeAway"/> a pass; a lattice or catwalk goes whole, as an <c>/obj</c>'s singularity_act deletes it.
    /// </summary>
    public void SingularityAct(int x, int y, int z)
    {
        if (Initialized && At(x, y, z).Exists) ScrapeAway(x, y, z);
    }

    // tg: code/game/turfs/change_turf.dm:290  attempt_lattice_replacement (shortcut: lattice_underneath is always TRUE, a map's own value)
    private void LatticeReplacement(int x, int y, int z) => Set(x, y, z, CeilingState.Of(CeilingLattice.Lattice));

    /// <summary>tg's <c>break_tile</c> (<c>_open.dm:52</c>): a pick from the turf's <c>broken_states()</c>; engine and mineral floors do not break.</summary>
    public void BreakTile(int x, int y, int z)
    {
        CeilingState old = At(x, y, z);
        if (!old.Sealed || old.Broken != 0 || Proto(old.Turf) is not turf_open floor || floor.damaged_dmi == null
            || floor is turf_open_floor_engine or turf_open_floor_mineral || floor.broken_states() is not DmList states || states.len == 0) return;
        Set(x, y, z, old with { Broken = (byte)Dm.num(rand(1, states.len)) });
    }

    public bool CanExtend(int x, int y, int z)
    {
        RefreshSupport();
        return Neighbors(x, y, z).Any(p => IsAnchor(p) || At(p.X, p.Y, p.Z) is { Exists: true, Supported: true });
    }

    public void TurfChanged(int x, int y, int z)
    {
        if (!Initialized) return;
        _revisions[(x, y, z)] = _revisions.GetValueOrDefault((x, y, z)) + 1;
        UpdateExposure(x, y, z);
        _supportDirty.Add((x, y, z));
        foreach (var p in Neighbors(x, y, z)) _supportDirty.Add(p);
        if (z < 2) return;
        _revisions[(x, y, z - 1)] = _revisions.GetValueOrDefault((x, y, z - 1)) + 1; // (a recipe under way on the tile under this turf starts again)
        RefreshLaidUnder(x, y, z - 1);
        ShotGeometry.ClearTopDents(MapView.TurfAt(_world, x, y, z - 1)); // DV577: the dents under this turf's floor went with it
    }

    /// <summary>
    /// A shuttle's roof goes with its turfs (DV183): the moved turf takes the ceiling of the turf it left. What stood at the dock
    /// waits under the shuttle (<see cref="_underShuttle"/>) and comes back when it leaves (<see cref="ShuttleLeft"/>).
    /// </summary>
    public void ShuttleArrived(turf oldT, turf newT)
    {
        if (!Initialized) return;
        (int X, int Y, int Z) cell = ((int)newT.x, (int)newT.y, (int)newT.z);
        CeilingState under = At(cell.X, cell.Y, cell.Z);
        if (under.Exists) _underShuttle[cell] = under;
        Set(cell.X, cell.Y, cell.Z, At((int)oldT.x, (int)oldT.y, (int)oldT.z));
    }

    /// <summary>The cell a shuttle left: the ceiling it covered, or none.</summary>
    public void ShuttleLeft(int x, int y, int z)
    {
        if (!Initialized) return;
        _underShuttle.Remove((x, y, z), out CeilingState under);
        Set(x, y, z, under);
    }

    /// <summary>A template loaded after the map's ceilings were made (a shuttle, a ruin) gets the same default ceilings as the map.</summary>
    public void TemplateLoaded(turf t)
    {
        if (!Initialized) return;
        RefreshLaidUnder((int)t.x, (int)t.y, (int)t.z - 1); // (the turf itself may be the plating a tile of the level below lies under)
        if (HasLevelAbove(t))
        {
            SetLaidUnder((int)t.x, (int)t.y, (int)t.z, LaidUnderOf(t)); // (under a level above: the tile its map lays under that level's plating)
            return;
        }
        // (the map's own ceiling, DV507: none where it says so, as an edit of the map editor's world takes one away)
        CeilingState start = StartOf(t);
        if (start.Exists || MapCeiling(t, out _))
            Set((int)t.x, (int)t.y, (int)t.z, start);
    }

    public void RefreshSupport()
    {
        var visited = new HashSet<(int X, int Y, int Z)>();
        foreach (var start in _supportDirty.OrderBy(p => p.Z).ThenBy(p => p.Y).ThenBy(p => p.X).ToArray())
        {
            if (!_cells.ContainsKey(start) || !visited.Add(start)) continue;
            var component = new List<(int X, int Y, int Z)> { start };
            bool supported = false;
            for (int i = 0; i < component.Count; i++)
            {
                var p = component[i];
                supported |= IsAnchor(p);
                foreach (var n in Neighbors(p.X, p.Y, p.Z))
                {
                    supported |= IsAnchor(n);
                    if (_cells.ContainsKey(n) && visited.Add(n)) component.Add(n);
                }
            }
            foreach (var p in component)
            {
                CeilingState old = _cells[p];
                if (old.Supported == supported) continue;
                _cells[p] = old with { Supported = supported };
                Changed?.Invoke(p.X, p.Y, p.Z);
            }
        }
        _supportDirty.Clear();
    }

    private bool IsAnchor((int X, int Y, int Z) p)
        => MapView.TurfAt(_world, p.X, p.Y, p.Z) is turf_closed_wall;

    private static IEnumerable<(int X, int Y, int Z)> Neighbors(int x, int y, int z)
    {
        yield return (x - 1, y, z); yield return (x + 1, y, z);
        yield return (x, y - 1, z); yield return (x, y + 1, z);
    }

    // A ceiling's block is never on the map: its type's vars and procs are read off one unplaced instance per type (constructors
    // never touch the world, object-model.md section 3).
    private readonly Dictionary<string, atom> _protos = new();
    private atom _lattice, _catwalk;
    private atom Proto(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        if (!_protos.TryGetValue(path, out atom proto))
            _protos[path] = proto = DmTypes.FromPath(path) is Type t && typeof(turf_open_floor).IsAssignableFrom(t) && !t.IsAbstract
                ? (atom)Activator.CreateInstance(t) : null;
        return proto;
    }

    /// <summary>
    /// The picture tg would draw for the ceiling's block (D295): its type's appearance with tg's bitmask smoothing among the ceiling
    /// neighbours (<c>icon_smoothing.dm:175</c>) and the broken overlay of <c>_open.dm:70</c>; null for no ceiling.
    /// shortcut: a lattice smooths with ceilings only, not with the walls it would join on tg's map.
    /// </summary>
    public mutable_appearance Appearance(int x, int y, int z)
    {
        CeilingState state = ShownAt(x, y, z);
        atom block = BlockOf(state);
        if (block == null) return null;
        var look = new mutable_appearance();
        look.New(block);
        if (Dm.truthy(Dm.bitand(block.smoothing_flags, SMOOTH_BITMASK)))
            look.icon_state = Dm.text("[]-[]", block.base_icon_state, Junction(x, y, z, block));
        if (state.Broken != 0 && block is turf_open floor && floor.broken_states() is DmList states && state.Broken <= states.len)
        {
            var broken = (mutable_appearance)mutable_appearance(floor.damaged_dmi, (string)states[state.Broken]);
            if (Dm.truthy(floor.smoothing_flags) && !Dm.truthy(floor.smooth_broken))
            {
                matrix translation = Dm.@new<matrix>();
                translation.Translate(-LARGE_TURF_SMOOTHING_X_OFFSET, -LARGE_TURF_SMOOTHING_Y_OFFSET);
                broken.transform = translation;
            }
            look.overlays.Add(broken);
        }
        return look;
    }

    /// <summary>
    /// tg's name of the ceiling's block ("plating", "glass floor", "lattice"); "none" without one. Under a level above it is what is seen from below:
    /// the tile under that level's plating, the plating, or its glass floor.
    /// </summary>
    public string NameAt(int x, int y, int z)
    {
        turf over = MapView.TurfAt(_world, x, y, z) is turf t ? GET_TURF_ABOVE(t) as turf : null;
        atom block = over is turf_open_floor_glass ? over : BlockOf(over != null ? UnderLevel(x, y, z) : At(x, y, z));
        return block != null ? Dm.text("[]", block.name) : "none";
    }

    private atom BlockOf(CeilingState state)
        => state.Sealed ? Proto(state.Turf) : state.Lattice switch
        {
            CeilingLattice.Lattice => _lattice ??= new obj_structure_lattice(),
            CeilingLattice.Catwalk => _catwalk ??= new obj_structure_lattice_catwalk(),
            _ => null,
        };

    private int Junction(int x, int y, int z, atom block)
    {
        var with = Groups(block.canSmoothWith);
        bool Joins(int dx, int dy) => BlockOf(ShownAt(x + dx, y + dy, z)) is atom n && Groups(n.smoothing_groups).Overlaps(with);
        int junction = 0;
        if (Joins(0, 1)) junction |= NORTH_JUNCTION;
        if (Joins(0, -1)) junction |= SOUTH_JUNCTION;
        if (Joins(1, 0)) junction |= EAST_JUNCTION;
        if (Joins(-1, 0)) junction |= WEST_JUNCTION;
        if (Dm.truthy(Dm.bitand(block.smoothing_flags, SMOOTH_BITMASK_CARDINALS))) return junction;
        if ((junction & NORTH_JUNCTION) != 0 && (junction & WEST_JUNCTION) != 0 && Joins(-1, 1)) junction |= NORTHWEST_JUNCTION;
        if ((junction & NORTH_JUNCTION) != 0 && (junction & EAST_JUNCTION) != 0 && Joins(1, 1)) junction |= NORTHEAST_JUNCTION;
        if ((junction & SOUTH_JUNCTION) != 0 && (junction & WEST_JUNCTION) != 0 && Joins(-1, -1)) junction |= SOUTHWEST_JUNCTION;
        if ((junction & SOUTH_JUNCTION) != 0 && (junction & EAST_JUNCTION) != 0 && Joins(1, -1)) junction |= SOUTHEAST_JUNCTION;
        return junction;
    }

    // An unplaced type's smoothing groups are still tg's define text ("0,3,"), not the bitfield list init makes of them.
    private static HashSet<string> Groups(object text)
        => new((text as string ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries));
}
