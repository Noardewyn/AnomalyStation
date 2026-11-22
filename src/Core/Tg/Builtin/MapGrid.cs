// The per-world map grid and engine state of the atom engine parts (docs/ai/specs/object-model.md sections 2 and 4;
// docs/ai/specs/move-chain.md E1, E3; the wave-3 scope's risks R4, R5 and R16). Not translated from a tg file: BYOND's map is
// engine state. Port machinery, not a DM type.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;

namespace Anomaly.Core.Tg;

/// <summary>
/// The map of one <see cref="World"/>: the turf of every cell from (1, 1, 1) to (maxx, maxy, maxz), behind
/// <c>locate(x, y, z)</c>, <c>block</c>, <c>get_step</c> and the turfs' coordinates. One instance per world (a Kernel
/// extension, so no static state). Creating it registers <see cref="OnMapResized"/> on the shim's map hook
/// (<see cref="DmWorldState.MapResized"/>, W3.0 S7): "New territory created by increasing the map boundaries is filled in
/// with the default turf and area (world.turf, and world.area)" (BYOND ref <c>/world/var/maxx</c>), so growing
/// <c>world.maxx</c>, <c>maxy</c> or <c>maxz</c> creates a <c>world.turf</c> in every new cell, x fastest, then y, then z,
/// inside the <c>world.area</c> instance (tg relies on it: <c>increase_max_x</c>, game/world.dm:458-490). Shrinking is a
/// runtime (tg never shrinks the map; scope R4). A world's grid exists once anything asks for it: the bootstrap attaches it
/// first (<see cref="Attach"/>); otherwise the first map builtin call (<c>locate</c>, <c>block</c>, <c>get_step</c>, ...)
/// attaches it and creates the turfs of a size that was set before.
/// <para>
/// It also holds the atoms whose contents list DM code wrote since the last <see cref="Sync"/> (Builtin/atom.builtin.cs:
/// IDmListOwner), whose edits Sync applies.
/// </para>
/// </summary>
internal sealed class MapGrid
{
    private readonly World _world;
    private readonly DmWorldState _state;

    // The size the grid has built and its cells, index (z - 1) * maxx * maxy + (y - 1) * maxx + (x - 1).
    private int _maxx;
    private int _maxy;
    private int _maxz;
    private turf[] _cells = Array.Empty<turf>();

    // The world.area instance that holds the turfs of a grown map.
    private area _default_area;

    // Atoms whose contents list was handed out since the last Sync, in hand-out order.
    private readonly List<atom> _handed_out = new List<atom>();
    private bool _syncing;

    // While the grid creates turfs (nested growth is allowed; the catch-up of Grown is not).
    private int _growing;

    // The cell of the turf the grid is creating, taken by the turf's placement (TakeSlot).
    private bool _slot_set;
    private int _slot_x;
    private int _slot_y;
    private int _slot_z;
    private area _slot_area;

    private MapGrid(World world)
    {
        _world = world;
        _state = DmWorldState.Of(world);
        _state.MapResized += OnMapResized;
    }

    /// <summary>The grid of <paramref name="world"/>, created (and hooked to the map size) on first use; creates no turfs.</summary>
    internal static MapGrid Of(World world) => world.GetExtension(static w => new MapGrid(w));

    /// <summary>The grid of the ambient world, with the turfs of the current map size (see <see cref="Grown"/>).</summary>
    internal static MapGrid Current => Of(World.Current).Grown();

    /// <summary>
    /// Attaches the grid to <paramref name="world"/> (hooks it to <c>world.maxx</c>, <c>maxy</c> and <c>maxz</c>) and creates
    /// the turfs of a size set before. The Tg bootstrap calls it right after creating the world, before anything sets the
    /// map size, so that every turf is created at the assignment that makes its cell, as in BYOND.
    /// </summary>
    internal static MapGrid Attach(World world)
    {
        MapGrid grid = Of(world);
        if (ReferenceEquals(World.CurrentOrNull, world))
            return grid.Grown();
        using (world.Enter())
            return grid.Grown();
    }

    /// <summary>The width, height and depth the grid has built (the world's maxx, maxy and maxz once attached).</summary>
    internal int maxx => _maxx;

    internal int maxy => _maxy;

    internal int maxz => _maxz;

    /// <summary>
    /// Creates the turfs of cells that the world's size has and the grid has not built yet (a size set before the grid
    /// was attached); a no-op once attached, and while the grid is creating turfs.
    /// </summary>
    internal MapGrid Grown()
    {
        if (_growing > 0) return this;
        int nx = Dim(_state.maxx);
        int ny = Dim(_state.maxy);
        int nz = Dim(_state.maxz);
        if (nx > _maxx || ny > _maxy || nz > _maxz)
            Grow(Math.Max(nx, _maxx), Math.Max(ny, _maxy), Math.Max(nz, _maxz));
        return this;
    }

    // The shim's map hook (DmWorldState.MapResized): called after world.maxx, maxy or maxz changed through the facade,
    // with the size before the change.
    private void OnMapResized(float old_maxx, float old_maxy, float old_maxz)
    {
        int nx = Dim(_state.maxx);
        int ny = Dim(_state.maxy);
        int nz = Dim(_state.maxz);
        if (nx < _maxx || ny < _maxy || nz < _maxz)
        {
            // PORT: BYOND drops the turfs of a shrunk map; tg never shrinks it (scope R4), so the port refuses: the size
            // goes back (a direct write of the fields calls nothing) and the assignment is a runtime error.
            _state.maxx = old_maxx;
            _state.maxy = old_maxy;
            _state.maxz = old_maxz;
            throw new DmRuntimeException(Dm.text("the map cannot shrink: world.maxx, maxy, maxz [], [], [] cannot become [], [], []",
                _maxx, _maxy, _maxz, nx, ny, nz));
        }
        Grow(nx, ny, nz);
    }

    // BYOND map sizes are whole numbers of turfs.
    private static int Dim(float size) => size >= 1 && float.IsFinite(size) ? (int)MathF.Floor(size) : 0;

    private int Index(int x, int y, int z) => (z - 1) * _maxx * _maxy + (y - 1) * _maxx + (x - 1);

    // Grows the grid to (nx, ny, nz) and fills every new cell with a world.turf in world.area, x fastest, then y, then z.
    private void Grow(int nx, int ny, int nz)
    {
        int ox = _maxx;
        int oy = _maxy;
        int oz = _maxz;
        if (nx == ox && ny == oy && nz == oz) return;
        var cells = new turf[checked(nx * ny * nz)];
        for (int z = 1; z <= oz; z++)
        {
            for (int y = 1; y <= oy; y++)
            {
                for (int x = 1; x <= ox; x++)
                    cells[(z - 1) * nx * ny + (y - 1) * nx + (x - 1)] = _cells[Index(x, y, z)];
            }
        }
        _cells = cells;
        _maxx = nx;
        _maxy = ny;
        _maxz = nz;

        _growing++;
        try
        {
            area into = null;
            Type turf_type = null;
            for (int z = 1; z <= nz; z++)
            {
                for (int y = 1; y <= ny; y++)
                {
                    for (int x = 1; x <= nx; x++)
                    {
                        if (x <= ox && y <= oy && z <= oz) continue;
                        into ??= DefaultArea();
                        turf_type ??= TurfType();
                        CreateTurf(turf_type, x, y, z, into);
                    }
                }
            }
        }
        finally
        {
            _growing--;
        }
    }

    // world.turf: the type of the turfs of a grown map (BYOND's default /turf while the bootstrap has not set tg's).
    private Type TurfType()
    {
        Type type = _state.turf ?? typeof(turf);
        if (!typeof(turf).IsAssignableFrom(type))
            throw new DmRuntimeException(Dm.text("world.turf is not a turf type: []", type));
        return type;
    }

    /// <summary>
    /// The <c>world.area</c> instance that holds the turfs of a grown map (BYOND's default /area while the bootstrap has
    /// not set tg's): the one the grid used before, else tg's own registry of unique areas (<c>GLOB.areas_by_type</c>,
    /// _globalvars/lists/mapping.dm:159, which tg's <c>/area/New</c> fills, game/area/areas.dm:177, and which
    /// <c>increase_max_x</c> reads, game/world.dm:465), else a new one ("For each area type defined, one area object is
    /// created at runtime", BYOND ref <c>/area</c>), created with a null location.
    /// </summary>
    internal area DefaultArea()
    {
        Type type = _state.area ?? typeof(area);
        if (!typeof(area).IsAssignableFrom(type))
            throw new DmRuntimeException(Dm.text("world.area is not an area type: []", type));
        if (_default_area != null && _default_area.GetType() == type && _default_area.gc_destroyed == null)
            return _default_area;
        DmList by_type = TgState.Of(_world).GLOB?.areas_by_type;
        if (by_type?[type] is area unique && unique.GetType() == type && unique.gc_destroyed == null)
            return _default_area = unique;
        return _default_area = (area)Dm.@new(type);
    }

    // Creates the turf of one new cell. The turf takes the cell in its placement, before its New (TakeSlot).
    private void CreateTurf(Type turf_type, int x, int y, int z, area into)
    {
        _slot_set = true;
        _slot_x = x;
        _slot_y = y;
        _slot_z = z;
        _slot_area = into;
        try
        {
            Dm.@new(turf_type);
        }
        catch (Exception e)
        {
            // Isolation boundary (engine): a runtime in a turf's New ends that New; the turf is already in its cell
            // (placed before New) and BYOND goes on filling the map (translation-rules.md 9.2 lists the translated ones).
            _world.ReportRuntime(e);
        }
        finally
        {
            _slot_set = false;
            _slot_area = null;
        }
    }

    /// <summary>The cell of the turf the grid is creating, once (the turf's placement takes it before its New).</summary>
    internal bool TakeSlot(out int x, out int y, out int z, out area into)
    {
        x = _slot_x;
        y = _slot_y;
        z = _slot_z;
        into = _slot_area;
        if (!_slot_set) return false;
        _slot_set = false;
        _slot_area = null;
        return true;
    }

    /// <summary>Puts a new turf into its cell and its area (appended to the area's contents).</summary>
    internal void Place(turf t, int x, int y, int z, area into)
    {
        t.dm_set_grid_coords(x, y, z);
        _cells[Index(x, y, z)] = t;
        if (into != null) t.dm_set_loc(into);
    }

    /// <summary>
    /// Replaces the turf <paramref name="old"/> of the map by the new turf <paramref name="replacement"/> (scope R16): the new
    /// turf takes the cell and the old turf's place in its area's contents, the movables on the old turf move onto the new
    /// one in their order (raw, nothing is called), and the old turf leaves the map (x = y = z = 0, no area, no contents)
    /// and the world registry, marked deleted as qdel would (<c>gc_destroyed = world.time</c>, unless tg's ChangeTurf has
    /// qdel'd it already; object-model.md section 8: every port path that unregisters a datum sets <c>gc_destroyed</c>).
    /// A reference to the old turf does not name the new one, as it would in BYOND: translated code takes the new turf at
    /// each site where tg relies on it (TURF-IDENTITY, docs/ai/specs/object-model.md 7.1, the M3 decision).
    /// </summary>
    internal void Replace(turf old, turf replacement)
    {
        int x = old.dm_grid_x;
        int y = old.dm_grid_y;
        int z = old.dm_grid_z;
        replacement.dm_set_grid_coords(x, y, z);
        _cells[Index(x, y, z)] = replacement;
        old.dm_set_grid_coords(0, 0, 0);
        // TURF-IDENTITY: the old reference names the cell's turf from now on (turf.dm_identity), whatever replaced it: ChangeTurf,
        // its CHANGETURF_SKIP's bare new (spawn_rivers), a map generator's or the map reader's new
        old.dm_replaced_by = replacement;
        if (old.dm_loc_raw is area holder)
            holder.dm_swap_held(old, replacement);
        old.dm_move_contents_to(replacement);
        if (old.dm_id != 0 && _world.Objects.TryGet(old.dm_id, out object found) && ReferenceEquals(found, old))
        {
            old.gc_destroyed ??= _world.Time;
            _world.Objects.Unregister(old.dm_id);
        }
    }

    /// <summary>BYOND <c>locate(x, y, z)</c>: the turf of that cell; null outside the map or at z 0 (coordinates are floored, scope R5).</summary>
    internal turf TurfAt(float x, float y, float z)
    {
        if (!float.IsFinite(x) || !float.IsFinite(y) || !float.IsFinite(z)) return null;
        return TurfAt((int)MathF.Floor(x), (int)MathF.Floor(y), (int)MathF.Floor(z));
    }

    internal turf TurfAt(int x, int y, int z)
    {
        if (x < 1 || y < 1 || z < 1 || x > _maxx || y > _maxy || z > _maxz) return null;
        return _cells[Index(x, y, z)];
    }

    /// <summary>
    /// Where an atom is on the map (move-chain.md E3: through the loc chain to the outermost turf): a turf's own cell, a
    /// movable's outermost turf, an area's turf with the lowest z, y, x. False for anything off the map (nullspace, a
    /// container chain that ends without a turf, a non-atom).
    /// </summary>
    internal bool Position(object thing, out int x, out int y, out int z)
    {
        Sync();
        turf on = thing switch
        {
            turf t => t,
            area a => a.dm_lowest_turf(),
            atom m => OutermostTurf(m),
            _ => null,
        };
        if (on == null || !on.dm_on_grid)
        {
            x = y = z = 0;
            return false;
        }
        x = on.dm_grid_x;
        y = on.dm_grid_y;
        z = on.dm_grid_z;
        return true;
    }

    private static turf OutermostTurf(atom thing)
    {
        for (atom container = thing.dm_loc_raw; container != null; container = container.dm_loc_raw)
        {
            if (container is turf t) return t;
        }
        return null;
    }

    // ---- replication hints (M1; presentation-seam.md rule 5, object-model.md section 6) ----

    /// <summary>
    /// Tells the world's replication hint sink, if any, that <paramref name="thing"/>'s container changed by an engine move
    /// (atom.dm_set_loc and the other raw moves of Builtin/atom.builtin.cs). One null check without a sink.
    /// </summary>
    internal void HintMoved(atom thing)
    {
        IReplicationHintSink hints = _state.Sinks.Replication;
        if (hints != null && thing.dm_id != 0) hints.Moved(thing);
    }

    // ---- contents-list edits by DM code (Builtin/atom.builtin.cs) ----

    /// <summary>Records that DM code wrote <paramref name="holder"/>'s contents list (IDmListOwner.ListChanged); the next Sync applies it.</summary>
    internal void MarkHandedOut(atom holder)
    {
        if (holder.dm_contents_handed_out) return;
        holder.dm_contents_handed_out = true;
        _handed_out.Add(holder);
    }

    /// <summary>
    /// Applies the edits DM code made to contents lists (atom.dm_reconcile_contents), in the order of the first edit. Every
    /// engine access calls it first, so an edit takes effect before the engine can observe it.
    /// </summary>
    internal void Sync()
    {
        if (_handed_out.Count == 0 || _syncing) return;
        _syncing = true;
        try
        {
            for (int i = 0; i < _handed_out.Count; i++)
            {
                atom holder = _handed_out[i];
                holder.dm_contents_handed_out = false;
                holder.dm_reconcile_contents();
            }
            _handed_out.Clear();
        }
        finally
        {
            _syncing = false;
        }
    }
}
