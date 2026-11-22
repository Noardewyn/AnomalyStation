// Engine ("BYOND builtin") procs of the map (docs/ai/translation-rules.md 1.5: Builtins; docs/ai/specs/move-chain.md E2, E3;
// the wave-3 scope's risk R5). Not translated from a tg file: BYOND's builtin procs, over the world's map grid
// (Builtin/MapGrid.cs). Behaviour from the BYOND reference (an offline copy of www.byond.com/docs/ref), then OpenDream
// (OpenDreamRuntime/Procs/Native/DreamProcNativeHelpers.cs GetDir, GetStep; DreamProcNativeRoot.cs block, is*), then
// tg's usage (translation-rules.md 0.2).
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;

namespace Anomaly.Core.Tg;

public static partial class Builtins
{
    /// <summary>
    /// BYOND <c>get_step(Ref, Dir)</c>: the turf one step from Ref's turf in Dir (the outermost turf of an atom inside
    /// containers, move-chain.md E3), null off the map or when Ref is not on it. NORTH, SOUTH, EAST, WEST and their
    /// diagonals move in x and y, UP and DOWN in z (tg's <c>GET_TURF_ABOVE</c> steps with UP); opposite bits cancel, and
    /// Dir 0 gives Ref's own turf (tg's <c>get_turf</c>, __DEFINES/turfs.dm:79).
    /// </summary>
    public static turf get_step(object Ref, float Dir)
    {
        MapGrid grid = MapGrid.Current;
        if (!grid.Position(Ref, out int x, out int y, out int z)) return null;
        if (Dm.truthy(Dm.bitand(Dir, NORTH))) y += 1;
        if (Dm.truthy(Dm.bitand(Dir, SOUTH))) y -= 1;
        if (Dm.truthy(Dm.bitand(Dir, EAST))) x += 1;
        if (Dm.truthy(Dm.bitand(Dir, WEST))) x -= 1;
        if (Dm.truthy(Dm.bitand(Dir, UP))) z += 1;
        if (Dm.truthy(Dm.bitand(Dir, DOWN))) z -= 1;
        return grid.TurfAt(x, y, z);
    }

    /// <summary>
    /// BYOND <c>get_dir(Loc1, Loc2)</c>: the direction from Loc1's turf to Loc2's turf, a cardinal when they share a row
    /// or a column and the diagonal of the quadrant otherwise ("if Loc2 is mostly north but a little to the east of Loc1,
    /// the direction returned will be NORTHEAST"); 0 for the same turf or when either is off the map. Only x and y count
    /// (the reference lists the eight planar directions).
    /// </summary>
    // RULE-GAP: across z levels OpenDream returns 0 while the BYOND reference only lists planar results; tg's
    // get_dir_multiz (modules/mapping/space_management/multiz_helpers.dm:11-29) ORs UP/DOWN with get_dir of turfs on two
    // z levels, which needs the planar direction there, so tg wins (rules 0.2): z is ignored (flagged for the BYOND oracle).
    public static float get_dir(object Loc1, object Loc2)
    {
        MapGrid grid = MapGrid.Current;
        if (!grid.Position(Loc1, out int x1, out int y1, out int _) || !grid.Position(Loc2, out int x2, out int y2, out int _))
            return 0;
        float direction = 0;
        if (x2 < x1) direction = Dm.bitor(direction, WEST);
        else if (x2 > x1) direction = Dm.bitor(direction, EAST);
        if (y2 < y1) direction = Dm.bitor(direction, SOUTH);
        else if (y2 > y1) direction = Dm.bitor(direction, NORTH);
        return direction;
    }

    /// <summary>
    /// BYOND <c>get_step_towards(Ref, Trg)</c>: the turf one step from Ref toward Trg, <c>get_step(Ref, get_dir(Ref, Trg))</c>
    /// (OpenDream's DMStandard/_Standard.dm:200; M4b, the extinguisher's water: tg's move_towards_budget loop): Ref's own turf
    /// when they share a turf, null when either is off the map.
    /// </summary>
    public static turf get_step_towards(object Ref, object Trg)
    {
        float dir = get_dir(Ref, Trg);
        return get_step(Ref, dir);
    }

    /// <summary>
    /// BYOND <c>get_step_away(Ref, Trg, Max)</c>: the turf one step from Ref away from Trg,
    /// <c>get_step(Ref, turn(get_dir(Ref, Trg), 180))</c> (OpenDream's DMStandard/_Standard.dm:205; M5x slice 3, the armoured
    /// human thrown by a devastating blast, human_defense.dm:295): Ref's own turf when they share a turf. Max is ignored, as
    /// OpenDream does.
    /// </summary>
    public static turf get_step_away(object Ref, object Trg, float? Max = null)
    {
        float dir = Dm.turn(get_dir(Ref, Trg), 180);
        return get_step(Ref, dir);
    }

    /// <summary>
    /// BYOND <c>get_dist(Loc1, Loc2)</c>: the number of tile moves from one to the other, diagonal moves and moves across z
    /// levels included, the largest of the x, y and z distances; -1 when Loc1 and Loc2 are the same object; infinite when
    /// either is not on the map (BYOND ref <c>/proc/get_dist</c>, 515 and later).
    /// </summary>
    public static float get_dist(object Loc1, object Loc2)
    {
        if (Loc1 != null && ReferenceEquals(Loc1, Loc2)) return -1;
        MapGrid grid = MapGrid.Current;
        if (!grid.Position(Loc1, out int x1, out int y1, out int z1) || !grid.Position(Loc2, out int x2, out int y2, out int z2))
            return float.PositiveInfinity;
        return Math.Max(Math.Abs(x2 - x1), Math.Max(Math.Abs(y2 - y1), Math.Abs(z2 - z1)));
    }

    /// <summary>BYOND <c>isturf(Loc)</c> (tg leaves it to BYOND, is_helpers.dm:28; rules 1.6).</summary>
    public static bool isturf(object Loc) => Loc is turf;

    /// <summary>BYOND <c>isarea(Loc)</c>.</summary>
    public static bool isarea(object Loc) => Loc is area;

    /// <summary>BYOND <c>ismob(Loc)</c>.</summary>
    public static bool ismob(object Loc) => Loc is mob;

    /// <summary>BYOND <c>ismovable(Loc)</c>: an obj or a mob ("Movable atoms are either objs or mobs"), any /atom/movable in the port.</summary>
    public static bool ismovable(object Loc) => Loc is atom_movable;

    /// <summary>
    /// BYOND <c>isloc(Loc)</c>: a mob, obj, turf or area; any atom, as in OpenDream ("You might think this excludes
    /// /atom/movable, but it does not").
    /// </summary>
    public static bool isloc(object Loc) => Loc is atom;

    /// <summary>
    /// BYOND <c>block(Start, End)</c>: the turfs of the box between two turfs, inclusive, x fastest, then y, then z
    /// (BYOND ref <c>/proc/block</c>; OpenDream collects them "in z-y-x order"). The corners may come in any order; cells
    /// outside the map are left out; anything but two turfs gives an empty list (OpenDream).
    /// </summary>
    public static DmList block(atom Start, atom End)
    {
        if (Start is not turf start || End is not turf end) return Dm.list();
        return block_of(start.dm_grid_x, start.dm_grid_y, start.dm_grid_z, end.dm_grid_x, end.dm_grid_y, end.dm_grid_z);
    }

    /// <summary>
    /// BYOND 515 <c>block(StartX, StartY, StartZ, EndX=StartX, EndY=StartY, EndZ=StartZ)</c>: the same by coordinates; a
    /// null end coordinate defaults to the start one (tg's <c>increase_max_x</c>, game/world.dm:466). Coordinates are
    /// floored.
    /// </summary>
    public static DmList block(float StartX, float StartY, float StartZ, float? EndX = null, float? EndY = null, float? EndZ = null)
    {
        EndX ??= StartX;
        EndY ??= StartY;
        EndZ ??= StartZ;
        return block_of(Coord(StartX), Coord(StartY), Coord(StartZ), Coord(EndX.Value), Coord(EndY.Value), Coord(EndZ.Value));
    }

    /// <summary>
    /// BYOND <c>step(Ref, Dir, Speed=0)</c> (BYOND ref <c>/proc/step</c>: "Move Ref in the direction Dir"; move-chain.md
    /// E2): the turf one step from Ref in Dir (<see cref="get_step"/>); when there is none (the map edge, or Ref not on
    /// the map) or it is Ref's own loc (Dir 0), Move is not called and step returns 0; otherwise it returns what Ref's
    /// outermost <c>Move(t, Dir)</c> returns, unconverted (OpenDream DMStandard/_Standard.dm:168-174 and its BYOND-parity
    /// test move.dm). <c>Speed</c> (pixel movement) is ignored: tg never passes it, and the port has no pixel movement.
    /// step itself sets no <c>glide_size</c>; tg's Move does (move-chain.md 3a).
    /// </summary>
    // RULE-GAP: the BYOND reference says "Returns: 1 on success; 0 otherwise", which describes the default Move; tg's Move
    // returns TRUE, FALSE or null (a diagonal step that fails both ways, move-chain.md 3f), and step passes that on as
    // OpenDream does (lead decision, wave 4 plan "step"; flagged for the BYOND oracle).
    /// <summary>
    /// BYOND <c>step_towards(Ref, Trg, Speed)</c>: <c>Ref.Move(get_step_towards(Ref, Trg), get_dir(Ref, Trg))</c> (OpenDream's
    /// DMStandard/_Standard.dm:216; M5x slice 5, the singularity's pull), 0 without a move when the step is off the map or Ref's own
    /// turf (as <see cref="step"/>).
    /// </summary>
    public static async DmTask<object> step_towards(atom_movable Ref, object Trg, float Speed = 0)
    {
        float dir = get_dir(Ref, Trg);
        turf t = get_step_towards(Ref, Trg);
        if (t == null || t == Ref.loc)
            return 0;
        return await Ref.Move(t, dir);
    }

    /// <summary>
    /// BYOND <c>step_away(Ref, Trg, Max, Speed)</c>: <c>Ref.Move(get_step_away(Ref, Trg, Max), get_dir(Trg, Ref))</c> (OpenDream's
    /// DMStandard/_Standard.dm), 0 without a move when the step is off the map or Ref's own turf.
    /// </summary>
    public static async DmTask<object> step_away(atom_movable Ref, object Trg, float Max = 5, float Speed = 0)
    {
        turf t = get_step_away(Ref, Trg, Max);
        if (t == null || t == Ref.loc)
            return 0;
        return await Ref.Move(t, get_dir(Trg, Ref));
    }

    /// <summary>BYOND <c>get_step_rand(Ref)</c>: the turf a step in a random direction (the cardinals and diagonals) away, as step_rand picks it.</summary>
    public static turf get_step_rand(object Ref) => get_step(Ref, Dm.num(pick(GlobalProcs.GLOB.alldirs)));
    /// <summary>BYOND <c>step_rand(Ref, Speed)</c>: a step in a random direction (the cardinals and diagonals), 1 on success.</summary>
    public static DmTask<object> step_rand(atom_movable Ref, float Speed = 0) => step(Ref, Dm.num(pick(GlobalProcs.GLOB.alldirs)), Speed);

    public static async DmTask<object> step(atom_movable Ref, float Dir, float Speed = 0)
    {
        turf t = get_step(Ref, Dir);
        if (t == null || t == Ref.loc)
            return 0;
        return await Ref.Move(t, Dir);
    }

    /// <summary>
    /// BYOND <c>step_to(Ref, Trg, Min=0, Speed=0)</c> as OpenDream has it (DMStandard/_Standard.dm:179; M6 slice 1, the gibs'
    /// streak): nothing when Ref is within <c>Min</c> tiles of Trg, otherwise <see cref="step"/> in <c>get_dir(Ref, Trg)</c>.
    /// BYOND steps around obstacles; OpenDream does not ("TODO: Consider obstacles"), and tg calls it only toward an adjacent
    /// turf (gibs and the meat spike), where the two agree unless a diagonal is blocked (flagged for the BYOND oracle).
    /// </summary>
    public static async DmTask<object> step_to(atom_movable Ref, object Trg, float Min = 0, float Speed = 0)
    {
        if (get_dist(Ref, Trg) <= Min)
            return null;
        return await step(Ref, get_dir(Ref, Trg), Speed);
    }

    /// <summary>
    /// BYOND <c>get_step_to(Ref, Trg, Min=0)</c> as OpenDream has it (DreamProcNativeRoot <c>get_step_to</c>,
    /// DreamMapManager.Pathfinding <c>CalculateSteps</c>; M6 slice 5, tg's move_to loop that streaks the gibs): the first turf
    /// of a breadth-first path from Ref's turf to within <c>Min</c> tiles (the rounded-down straight distance) of Trg's turf on
    /// the same z, around dense turfs (dense movables do not block it), exploring east, northeast, north, northwest, west,
    /// southwest, south and southeast in that order, no further than the larger side of <c>world.view</c> less one steps;
    /// null when Ref is already there, when no path is found or when either is off the map.
    /// </summary>
    public static turf get_step_to(object Ref, object Trg, float Min = 0)
    {
        MapGrid grid = MapGrid.Current;
        if (!grid.Position(Ref, out int x, out int y, out int z) || !grid.Position(Trg, out int dx, out int dy, out int dz) || z != dz)
            return null;
        range_size(Dm.world.view, out int width, out int height);
        int maxSteps = Math.Max(width, height) - 1;
        int min = (int)Min;
        var parent = new Dictionary<(int X, int Y), (int X, int Y)>();
        var steps = new Dictionary<(int X, int Y), int> { [(x, y)] = 0 };
        var explored = new HashSet<(int X, int Y)>();
        var queue = new Queue<(int X, int Y)>();
        var queued = new HashSet<(int X, int Y)> { (x, y) };
        queue.Enqueue((x, y));
        (int X, int Y)[] offsets = { (1, 0), (1, 1), (0, 1), (-1, 1), (-1, 0), (-1, -1), (0, -1), (1, -1) };
        while (queue.Count > 0)
        {
            (int X, int Y) node = queue.Dequeue();
            queued.Remove(node);
            int distX = node.X - dx, distY = node.Y - dy;
            if ((int)Math.Sqrt(distX * distX + distY * distY) <= min)
            {
                if (node == (x, y))
                    return null;
                while (parent[node] != (x, y))
                    node = parent[node];
                return grid.TurfAt(node.X, node.Y, z);
            }
            explored.Add(node);
            foreach ((int ox, int oy) in offsets)
            {
                (int X, int Y) next = (node.X + ox, node.Y + oy);
                if (next.X < 1 || next.X > grid.maxx || next.Y < 1 || next.Y > grid.maxy || steps[node] >= maxSteps || explored.Contains(next))
                    continue;
                turf cell = grid.TurfAt(next.X, next.Y, z);
                if (cell == null || Dm.truthy(cell.density))
                    continue;
                // OpenDream's node for a turf already in the queue is a fresh one, so the queued node keeps its first parent
                if (queued.Contains(next))
                    continue;
                queue.Enqueue(next);
                queued.Add(next);
                steps[next] = steps[node] + 1;
                parent[next] = node;
            }
        }
        return null;
    }

    /// <summary>
    /// BYOND <c>orange(Dist, Center)</c> (BYOND ref <c>/proc/orange</c>: "the same as range(), except Center is excluded"):
    /// the atoms in the square of <c>Dist</c> tiles around <c>Center</c> without the centre, in OpenDream's order
    /// (DreamProcNativeRoot <c>orange</c>, DreamProcNativeHelpers <c>ResolveViewArguments</c>, <c>HandleRange</c> with
    /// <c>includeCenter</c> false, <c>DantomSpiral</c>). The arguments may come in either order: an atom is the centre (by
    /// default <c>usr</c>), a number is the distance and a <c>"WxH"</c> text a box (by default <c>world.view</c>), null is
    /// skipped. For a turf centre the list has the turfs of the box but the centre, each followed by its contents and, the
    /// first time a turf of an area is added, that area. For a movable on a turf it starts with that turf, its area and the
    /// turf's other contents (the centre's own contents are left out), then the box around the turf. For a movable inside
    /// another atom it is that atom and its other contents only. For an area centre it is the area, then its turfs, each with
    /// its contents. Atoms with an <c>invisibility</c> of 101 or more are left out (the area of an area centre excepted).
    /// </summary>
    public static DmList orange(object Dist = null, object Center = null)
    {
        atom center = GlobalProcs.usr;
        object range = Dm.world.view;
        foreach (object arg in new[] { Dist, Center })
        {
            if (arg is atom given) center = given;
            else if (arg != null) range = arg;
        }
        DmList result = Dm.list();
        if (center == null)
            return result;
        range_size(range, out int width, out int height);
        var seen_areas = new HashSet<area>();

        void add(atom value)
        {
            if (value.invisibility >= 101)
                return;
            result.Add(value);
            if (value is turf t && t.loc is area a && seen_areas.Add(a))
                result.Add(a);
        }

        MapGrid grid = MapGrid.Current;
        grid.Sync();
        if (center is area area_center)
        {
            result.Add(area_center); // dodges the invisibility check (OpenDream)
            seen_areas.Add(area_center);
            foreach (turf t in Dm.each<turf>(area_center))
            {
                add(t);
                foreach (atom content in Dm.each<atom>(t))
                    add(content);
            }
            return result;
        }
        if (center is not turf)
        {
            // the loc's contents include the centre, which orange leaves out
            if (center.loc is atom holder)
            {
                add(holder);
                foreach (atom content in Dm.each<atom>(holder))
                {
                    if (content == center)
                        continue;
                    add(content);
                }
                if (holder is not turf)
                    return result;
            }
        }
        if (!grid.Position(center, out int cx, out int cy, out int cz))
            return result;
        foreach ((int x, int y) in dantom_spiral(cx, cy, width, height))
        {
            turf t = grid.TurfAt(x, y, cz);
            if (t == null)
                continue;
            add(t);
            foreach (atom content in Dm.each<atom>(t))
                add(content);
        }
        return result;
    }

    /// <summary>
    /// BYOND <c>range(Dist, Center)</c> (BYOND ref <c>/proc/range</c>: "a list of objects within Dist tiles of Center"; M3 P3,
    /// the lead's assignment): <see cref="orange"/> with the centre included, in OpenDream's order (DreamProcNativeRoot
    /// <c>range</c>, DreamProcNativeHelpers <c>ResolveViewArguments</c>, <c>HandleRange</c> with <c>includeCenter</c> true,
    /// <c>DantomSpiral</c>). The arguments come as orange's. For a turf centre the list starts with that turf, its area and
    /// its contents, then the box around it. For a movable it starts with the movable's own contents, then its loc and the
    /// loc's contents (the movable among them); when the loc is a turf, the box around it follows, otherwise the list ends
    /// there. For an area centre it is the area, then its turfs, each with its contents. Atoms with an <c>invisibility</c> of
    /// 101 or more are left out (the area of an area centre excepted).
    /// </summary>
    public static DmList range(object Dist = null, object Center = null)
    {
        atom center = GlobalProcs.usr;
        object view_range = Dm.world.view;
        foreach (object arg in new[] { Dist, Center })
        {
            if (arg is atom given) center = given;
            else if (arg != null) view_range = arg;
        }
        DmList result = Dm.list();
        if (center == null)
            return result;
        range_size(view_range, out int width, out int height);
        var seen_areas = new HashSet<area>();

        void add(atom value)
        {
            if (value.invisibility >= 101)
                return;
            result.Add(value);
            if (value is turf t && t.loc is area a && seen_areas.Add(a))
                result.Add(a);
        }

        MapGrid grid = MapGrid.Current;
        grid.Sync();
        if (center is area area_center)
        {
            result.Add(area_center); // dodges the invisibility check (OpenDream)
            seen_areas.Add(area_center);
            foreach (turf t in Dm.each<turf>(area_center))
            {
                add(t);
                foreach (atom content in Dm.each<atom>(t))
                    add(content);
            }
            return result;
        }
        if (center is turf turf_center)
        {
            add(turf_center);
            foreach (atom content in Dm.each<atom>(turf_center))
                add(content);
        }
        else
        {
            // a container: its own contents first
            foreach (atom content in Dm.each<atom>(center))
                add(content);
            // the loc's contents include the centre
            if (center.loc is atom holder)
            {
                add(holder);
                foreach (atom content in Dm.each<atom>(holder))
                    add(content);
                if (holder is not turf)
                    return result;
            }
        }
        if (!grid.Position(center, out int cx, out int cy, out int cz))
            return result;
        foreach ((int x, int y) in dantom_spiral(cx, cy, width, height))
        {
            turf t = grid.TurfAt(x, y, cz);
            if (t == null)
                continue;
            add(t);
            foreach (atom content in Dm.each<atom>(t))
                add(content);
        }
        return result;
    }

    /// <summary>
    /// BYOND <c>view(Dist, Center)</c> (BYOND ref <c>/proc/view</c>): the centre's contents, then the visible turfs of the box
    /// of <c>Dist</c> around the centre's turf, centre first then the spiral, each followed by its movables, in OpenDream's
    /// order (DreamProcNativeRoot <c>view</c>; <c>CollectViewData</c>: a tile is opaque when its turf or a movable on it is;
    /// <c>ViewAlgorithm.CalculateVisibility</c>, BYOND's documented algorithm without the light and infrared steps, which
    /// OpenDream leaves as TODO). The arguments may come in either order, as for <see cref="orange"/>. M3 P4 (the random
    /// spawners' scatter, spawners/random/_random.dm:131).
    /// </summary>
    public static DmList view(object Dist = null, object Center = null)
    {
        atom center = GlobalProcs.usr;
        object view_range = Dm.world.view;
        foreach (object arg in new[] { Dist, Center })
        {
            if (arg is atom given) center = given;
            else if (arg != null) view_range = arg;
        }
        DmList result = Dm.list();
        if (center == null)
            return result;
        foreach (atom item in Dm.each<atom>(center))
            result.Add(item);
        MapGrid grid = MapGrid.Current;
        grid.Sync();
        if (!grid.Position(center, out int eye_x, out int eye_y, out int eye_z))
            return result;
        range_size(view_range, out int width, out int height);
        if (width <= 0 || height <= 0) // a negative Dist: an empty box (DV524; OpenDream would throw on the negative array)
            return result;
        view_tile[,] tiles = view_box(grid, eye_x, eye_y, eye_z, width, height);
        void add(view_tile tile)
        {
            if (tile == null || tile.Vis == 0)
                return;
            result.Add(tile.Turf);
            foreach (atom_movable movable in Dm.each<atom_movable>(tile.Turf))
                result.Add(movable);
        }
        add(tiles[width / 2, height / 2]);
        foreach ((int x, int y) in dantom_spiral(width / 2, height / 2, width, height))
            add(tiles[x, y]);
        return result;
    }

    /// <summary>
    /// The turfs of <see cref="view"/>(<paramref name="dist"/>, <paramref name="center"/>) with a turf centre, in its order,
    /// without the movables and the result list: the light source's walk (tg's <c>for(var/turf/T in view(...))</c>,
    /// lighting_source.dm:491), an optimization without behaviour change (M7 lumcount).
    /// </summary>
    internal static IEnumerable<turf> view_turfs(float dist, turf center)
    {
        MapGrid grid = MapGrid.Current;
        grid.Sync();
        if (!grid.Position(center, out int eye_x, out int eye_y, out int eye_z))
            yield break;
        range_size(dist, out int width, out int height);
        if (width <= 0 || height <= 0) // a negative Dist: an empty box (DV524; OpenDream would throw on the negative array)
            yield break;
        view_tile[,] tiles = view_box(grid, eye_x, eye_y, eye_z, width, height);
        view_tile middle = tiles[width / 2, height / 2];
        if (middle != null && middle.Vis != 0)
            yield return middle.Turf;
        foreach ((int x, int y) in dantom_spiral(width / 2, height / 2, width, height))
        {
            view_tile tile = tiles[x, y];
            if (tile != null && tile.Vis != 0)
                yield return tile.Turf;
        }
    }

    /// <summary>
    /// BYOND <c>oview(Dist, Center)</c> (BYOND ref <c>/proc/oview</c>): <see cref="view"/> without the centre's contents and without
    /// the centre's own tile (OpenDream's DreamProcNativeRoot <c>oview</c>: the spiral without its centre, each visible turf followed by
    /// its movables). The arguments may come in either order. The M7 zaps slice (tg's tesla_zap, energy_ball.dm:250).
    /// </summary>
    public static DmList oview(object Dist = null, object Center = null)
    {
        atom center = GlobalProcs.usr;
        object view_range = Dm.world.view;
        foreach (object arg in new[] { Dist, Center })
        {
            if (arg is atom given) center = given;
            else if (arg != null) view_range = arg;
        }
        DmList result = Dm.list();
        if (center == null)
            return result;
        MapGrid grid = MapGrid.Current;
        grid.Sync();
        if (!grid.Position(center, out int eye_x, out int eye_y, out int eye_z))
            return result;
        range_size(view_range, out int width, out int height);
        if (width <= 0 || height <= 0) // a negative Dist: an empty box (DV524; OpenDream would throw on the negative array)
            return result;
        view_tile[,] tiles = view_box(grid, eye_x, eye_y, eye_z, width, height);
        foreach ((int x, int y) in dantom_spiral(width / 2, height / 2, width, height))
        {
            view_tile tile = tiles[x, y];
            if (tile == null || tile.Vis == 0)
                continue;
            result.Add(tile.Turf);
            foreach (atom_movable movable in Dm.each<atom_movable>(tile.Turf))
                result.Add(movable);
        }
        return result;
    }

    /// <summary>
    /// BYOND <c>hearers(Depth = world.view, Center = usr)</c> (BYOND ref <c>/proc/hearers</c>: "just like viewers(), but it is a
    /// list of mobs that can hear the center object. Currently, this is computed on the assumption that opaque objects block
    /// sound, just like they block light"; <c>viewers</c>: "the mobs which can see the Center by checking view() from each mob").
    /// M4b audio slice 1 (tg's get_hearers_in_view, __HELPERS/spatial_info.dm:122). The arguments may come in either order, as for
    /// <see cref="view"/>. A mob is in the list when it stands on a turf within the box of <c>Depth</c> around the centre's turf
    /// and the centre's turf is visible in its own view of that box (the algorithm of <see cref="view"/>, without light: OpenDream
    /// <c>HandleViewersHearers</c> with <c>ignoreLight</c>); a mob inside another atom sees only that atom's contents, so it does
    /// not hear a centre outside it. The order is the centre's turf, then the spiral of <see cref="view"/>, each turf's mobs in
    /// contents order (OpenDream iterates an entity lookup instead; the box is the BYOND reference's view() box, where OpenDream
    /// looks the mobs up in a circle of radius Depth).
    /// </summary>
    public static DmList hearers(object Depth = null, object Center = null)
    {
        atom center = GlobalProcs.usr;
        object view_range = Dm.world.view;
        foreach (object arg in new[] { Depth, Center })
        {
            if (arg is atom given) center = given;
            else if (arg != null) view_range = arg;
        }
        DmList result = Dm.list();
        if (center == null)
            return result;
        MapGrid grid = MapGrid.Current;
        grid.Sync();
        if (!grid.Position(center, out int center_x, out int center_y, out int center_z))
            return result;
        range_size(view_range, out int width, out int height);
        if (width <= 0 || height <= 0) // a negative Dist: an empty box (DV524; OpenDream would throw on the negative array)
            return result;
        var seen_from = new Dictionary<turf, bool>();
        void consider(int x, int y)
        {
            turf eye_turf = grid.TurfAt(x, y, center_z);
            if (eye_turf == null)
                return;
            bool hears = false;
            bool computed = false;
            foreach (mob listener in Dm.each<mob>(eye_turf))
            {
                if (!computed)
                {
                    if (!seen_from.TryGetValue(eye_turf, out hears))
                    {
                        view_tile[,] tiles = view_box(grid, x, y, center_z, width, height);
                        view_tile tile = tiles[center_x - x + width / 2, center_y - y + height / 2];
                        hears = tile != null && tile.Vis != 0;
                        seen_from[eye_turf] = hears;
                    }
                    computed = true;
                }
                if (hears)
                    result.Add(listener);
            }
        }
        consider(center_x, center_y);
        foreach ((int x, int y) in dantom_spiral(center_x, center_y, width, height))
            consider(x, y);
        return result;
    }

    /// <summary>
    /// BYOND <c>viewers(Depth = world.view, Center = usr)</c> (BYOND ref <c>/proc/viewers</c>: "the mobs which can see the
    /// Center by checking view() from each mob"). M5x slice 1 (an explosion's flash, explosions.dm:393). BYOND's viewers differs
    /// from <see cref="hearers"/> only by light (a mob in the dark sees nothing); the port's <see cref="view"/> has no light step
    /// (OpenDream leaves it as TODO), so the two are one algorithm here: the list of <see cref="hearers"/>, in its order.
    /// </summary>
    public static DmList viewers(object Depth = null, object Center = null) => hearers(Depth, Center);

    /// <summary>BYOND <c>ohearers(Depth, Center)</c>: <see cref="hearers"/> without the mobs on the centre's tile, as <see cref="oviewers"/>
    /// (silicons SI8: the malf AI's intercom HONK).</summary>
    public static DmList ohearers(object Depth = null, object Center = null) => oviewers(Depth, Center);

    /// <summary>
    /// BYOND <c>oviewers(Depth, Center)</c>: <see cref="viewers"/> without the mobs that stand on the centre's tile (OpenDream's
    /// DreamProcNativeHelpers.HandleOviewersOhearers skips a mob at the centre's x and y). M5x slice 5 (the singularity's mezzer).
    /// </summary>
    public static DmList oviewers(object Depth = null, object Center = null)
    {
        DmList all = viewers(Depth, Center);
        turf centre = Center is atom c ? get_turf__builtin(c) : null;
        var result = Dm.list();
        foreach (object m in Dm.each<object>(all))
        {
            if (centre != null && m is atom a && get_turf__builtin(a) == centre)
                continue;
            result.Add(m);
        }
        return result;
    }

    // The atom's outermost turf (tg's get_turf, the builtin's own copy: Builtins does not see GlobalProcs).
    private static turf get_turf__builtin(atom a) => MapGrid.Current.Position(a, out int x, out int y, out int z) ? MapGrid.Current.TurfAt(x, y, z) : null;

    /// <summary>BYOND <c>locate(X, Y, Z)</c>: the turf at those coordinates, null outside the map or at z 0 (move-chain.md E3, scope R5; coordinates are floored).</summary>
    public static turf locate(float X, float Y, float Z) => MapGrid.Current.TurfAt(X, Y, Z);

    // A map coordinate from a DM number (floored; a non-finite number is off the map).
    private static int Coord(float value) => float.IsFinite(value) ? (int)MathF.Floor(value) : 0;

    // The turfs of the inclusive box, x fastest, then y, then z; cells outside the map are left out.
    private static DmList block_of(int x1, int y1, int z1, int x2, int y2, int z2)
    {
        MapGrid grid = MapGrid.Current;
        grid.Sync();
        var turfs = Dm.list();
        int start_x = Math.Min(x1, x2), end_x = Math.Max(x1, x2);
        int start_y = Math.Min(y1, y2), end_y = Math.Max(y1, y2);
        int start_z = Math.Min(z1, z2), end_z = Math.Max(z1, z2);
        for (int z = Math.Max(start_z, 1); z <= Math.Min(end_z, grid.maxz); z++)
        {
            for (int y = Math.Max(start_y, 1); y <= Math.Min(end_y, grid.maxy); y++)
            {
                for (int x = Math.Max(start_x, 1); x <= Math.Min(end_x, grid.maxx); x++)
                {
                    turf cell = grid.TurfAt(x, y, z);
                    if (cell != null) turfs.Add(cell);
                }
            }
        }
        return turfs;
    }
    /// <summary>
    /// SHIM-GAP (M5x slice 1): what reading BYOND's <c>world.view</c> gives: a number when the view is a square of an odd side
    /// (<c>"15x15"</c> reads as 7), the text otherwise (OpenDream <c>DreamObjectWorld</c> "view": "Number if square &amp;
    /// centerable, string representation otherwise"). The shim's <c>world.view</c> returns what was assigned, so tg code that does
    /// arithmetic on it (<c>shake_the_room</c>'s <c>near_distance + world.view - 2</c>) reads it through this.
    /// </summary>
    public static object dm_view_read(object view)
    {
        if (view is string text)
        {
            range_size(text, out int width, out int height);
            if (width == height && width % 2 == 1)
                return (float)((width - 1) / 2);
        }
        return view;
    }

    // M3 P4 (the lead's assignment, a P4 row in m3-ownership.md): the box of a view or range argument, a number Dist
    // (2 * Dist + 1 on each side) or a "WxH" text (OpenDream ViewRange); anything else is world.view's default, 5.
    private static void range_size(object range, out int width, out int height)
    {
        if (range is string text)
        {
            string[] split = text.Split('x');
            if (split.Length == 2 && int.TryParse(split[0], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out width)
                && int.TryParse(split[1], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out height))
                return;
            throw new DmRuntimeException(Dm.text("Invalid view range string \"[]\"", text));
        }
        int dist = Dm.isnum(range) ? (int)MathF.Truncate(Dm.num(range)) : 5;
        width = height = dist * 2 + 1;
    }

    // OpenDream's DantomSpiral (DreamProcNativeHelpers.cs): the cells of the box around (x, y) without the centre, ring
    // by ring outward; in each ring the left column bottom to top, then the bottom and top rows left to right, then the
    // right column bottom to top.
    private static IEnumerable<(int X, int Y)> dantom_spiral(int x, int y, int width, int height)
    {
        int bottom_range = height / 2;
        int top_range = (height - 1) / 2;
        int left_range = width / 2;
        int right_range = (width - 1) / 2;
        int donut_count = Math.Max(bottom_range, left_range);
        for (int donut = 1; donut <= donut_count; donut++)
        {
            int column_bottom = y - Math.Min(donut, bottom_range);
            int column_top = y + Math.Min(donut, top_range);
            if (donut <= left_range)
            {
                for (int pos_y = column_bottom; pos_y <= column_top; pos_y++)
                    yield return (x - donut, pos_y);
            }
            if (donut <= bottom_range)
            {
                int starting_x = x - Math.Min(donut, left_range + 1) + 1;
                int ending_x = x + Math.Min(donut, right_range + 1) - 1;
                for (int pos_x = starting_x; pos_x <= ending_x; pos_x++)
                {
                    yield return (pos_x, y - donut);
                    if (donut > top_range)
                        continue;
                    yield return (pos_x, y + donut);
                }
            }
            if (donut <= right_range)
            {
                for (int pos_y = column_bottom; pos_y <= column_top; pos_y++)
                    yield return (x + donut, pos_y);
            }
        }
    }

    // OpenDream's CollectViewData and ViewAlgorithm.CalculateVisibility (DreamProcNativeHelpers.cs): the view box of width by
    // height around the eye (eye_x, eye_y, eye_z), a tile opaque when its turf or a movable on it is, with each tile's visibility.
    private static view_tile[,] view_box(MapGrid grid, int eye_x, int eye_y, int eye_z, int width, int height)
    {
        var tiles = new view_tile[width, height];
        for (int vx = 0; vx < width; vx++)
        {
            for (int vy = 0; vy < height; vy++)
            {
                int dx = -(width / 2) + vx, dy = -(height / 2) + vy;
                turf t = grid.TurfAt(eye_x + dx, eye_y + dy, eye_z);
                if (t == null)
                    continue;
                var tile = new view_tile { Opaque = Dm.truthy(t.opacity), DeltaX = dx, DeltaY = dy, Turf = t };
                // the turf's contents as the engine holds them (every caller synced the grid), read without a snapshot and
                // only until one is opaque: an optimization of the light sources' walk (M7 lumcount), same result
                IReadOnlyList<atom> held = t.dm_held;
                for (int i = 0; i < held.Count && !tile.Opaque; i++)
                    tile.Opaque = held[i] is atom_movable movable && Dm.truthy(movable.opacity);
                tiles[vx, vy] = tile;
            }
        }
        calculate_visibility(tiles);
        return tiles;
    }

    // OpenDream's ViewAlgorithm.Tile (OpenDreamShared/Dream/ViewAlgorithm.cs): one cell of a view box.
    private sealed class view_tile
    {
        public bool Opaque;
        public int DeltaX, DeltaY;
        public int Vis, Vis2;
        public turf Turf;
    }

    // OpenDream's ViewAlgorithm.CalculateVisibility, "as described by https://www.byond.com/forum/post/2130277#comment20659267",
    // steps 2-5 and 8-10 (6 and 7, light and infrared, are OpenDream's TODO); a tile is visible when its Vis is not 0.
    private static void calculate_visibility(view_tile[,] tiles)
    {
        int width = tiles.GetLength(0), height = tiles.GetLength(1);
        int eye_x = width / 2, eye_y = height / 2;
        view_tile eye = tiles[eye_x, eye_y];
        int highest_max_delta = Math.Max(width - eye_x, height - eye_y);
        int highest_sum_delta = (width - eye_x) + (height - eye_y);

        // Step 3, diagonal shadow loop (Vis2)
        for (int d = 0; d < highest_max_delta; d++)
        {
            int lower_x = eye_x - d - 1, upper_x = eye_x + d + 1;
            for (int x = Math.Max(lower_x, 0); x <= Math.Min(upper_x, width - 1); x++)
            {
                int y = eye_y - d - 1, upper_y = eye_y + d + 1;
                bool skip_middle = x != lower_x && x != upper_x;
                if (y < 0)
                    y = skip_middle ? upper_y : 0;
                while (y <= upper_y && y < height)
                {
                    view_tile tile = tiles[x, y];
                    if (tile != null && check_neighbors_vis(tiles, true, x, y, d))
                        tile.Vis2 = tile.Opaque ? -1 : d + 1;
                    if (skip_middle && y != upper_y)
                        y = upper_y;
                    else
                        y++;
                }
            }
        }

        // Step 4, straight shadow loop (Vis)
        for (int d = 0; d < highest_sum_delta; d++)
        {
            int lower_x = Math.Max(eye_x - d - 1, 0), upper_x = Math.Min(eye_x + d + 1, width - 1);
            for (int x = lower_x; x <= upper_x; x++)
            {
                int offset_y = (d + 1) - Math.Abs(eye_x - x);
                update_tile(x, eye_y - offset_y, d);
                if (offset_y != 0)
                    update_tile(x, eye_y + offset_y, d);
            }
        }
        void update_tile(int x, int y, int d)
        {
            if (y < 0 || y >= height || tiles[x, y] is not view_tile tile)
                return;
            if (!check_neighbors_vis(tiles, false, x, y, d))
                return;
            if (tile.Opaque)
                tile.Vis = -1;
            else if (tile.Vis2 != 0)
                tile.Vis = d + 1;
        }

        // Step 5
        if (eye != null)
            eye.Vis = 1;

        // Step 8
        foreach (view_tile tile in tiles)
            if (tile != null)
                tile.Vis2 = tile.Vis;

        // Step 9: the walls and corners at the edge of what is seen become visible
        var boundary = new List<view_tile>();
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                view_tile tile = tiles[x, y];
                if (tile == null || !tile.Opaque || tile.Vis != 0)
                    continue;
                if (x != 0 && x != width - 1 && tiles[x - 1, y] is view_tile east && tiles[x + 1, y] is view_tile west && east.Vis != 0 && west.Vis != 0)
                {
                    boundary.Add(tile);
                    continue;
                }
                if (y != 0 && y != height - 1 && tiles[x, y - 1] is view_tile north && tiles[x, y + 1] is view_tile south && north.Vis != 0 && south.Vis != 0)
                {
                    boundary.Add(tile);
                    continue;
                }
                if (is_corner(tiles, x, y, 1, 1) || is_corner(tiles, x, y, 1, -1) || is_corner(tiles, x, y, -1, -1) || is_corner(tiles, x, y, -1, 1))
                    boundary.Add(tile);
            }
        }
        foreach (view_tile tile in boundary)
            tile.Vis = -1;
    }

    private static bool check_neighbors_vis(view_tile[,] tiles, bool checking_vis2, int x, int y, int d)
    {
        int width = tiles.GetLength(0), height = tiles.GetLength(1);
        for (int nx = Math.Max(x - 1, 0); nx < Math.Min(x + 2, width); nx++)
        {
            for (int ny = Math.Max(y - 1, 0); ny < Math.Min(y + 2, height); ny++)
            {
                if (nx == x && ny == y)
                    continue;
                view_tile tile = tiles[nx, ny];
                if (tile == null)
                    continue;
                if (checking_vis2 ? tile.Vis2 == d : tile.Vis == d)
                    return true;
            }
        }
        return false;
    }

    private static bool is_corner(view_tile[,] tiles, int x, int y, int delta_x, int delta_y)
    {
        int diagonal_x = x + delta_x, diagonal_y = y + delta_y;
        if (diagonal_x < 0 || diagonal_y < 0 || diagonal_x >= tiles.GetLength(0) || diagonal_y >= tiles.GetLength(1))
            return false;
        view_tile diagonal = tiles[diagonal_x, diagonal_y];
        view_tile cardinal1 = tiles[x, diagonal_y];
        view_tile cardinal2 = tiles[diagonal_x, y];
        if (diagonal == null || cardinal1 == null || cardinal2 == null)
            return false;
        return diagonal.Vis != 0 && cardinal1.Vis != 0 && cardinal2.Vis != 0 && cardinal1.Opaque && cardinal2.Opaque && !diagonal.Opaque;
    }
}
