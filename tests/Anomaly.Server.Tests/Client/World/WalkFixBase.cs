using System.Reflection;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.World;
using Anomaly.Core.DmShim;
using Anomaly.Core.Tg;
using Anomaly.Protocol;
using Xunit.Abstractions;

namespace Anomaly.Server.Tests.Client.World;

/// <summary>
/// The shared part of the M3 R2 walk-fix tests (the fixes from the owner's first walk of MetaStation on the fake feed, 2026-10-08: chairs in the
/// air, tables in walls, wall things floating or on doors, the firelock frame drawn as a billboard): Meta planned once per test run, skipped
/// without the converted map as the other Meta tests are.
/// </summary>
public abstract class WalkFixBase
{
    protected readonly ITestOutputHelper _output;

    protected WalkFixBase(ITestOutputHelper output) => _output = output;

    // ---- Meta, planned once per test run ----

    protected sealed class MetaWorld
    {
        public FakeMap Map;
        public FakeMapBinding Binding;
        public WorldPlan Plan;
        public Dictionary<long, Placed> Placed;
        public Dictionary<long, FakeMapEntity> Entities;
        public bool[] Closed;
        public HashSet<(int, int)> Windows = new();

        public bool IsWallOrWindow(int x, int y)
            => x >= 1 && y >= 1 && x <= Map.Width && y <= Map.Height && (Closed[(x - 1) + (y - 1) * Map.Width] || Windows.Contains((x, y)));
    }

    private static readonly Lazy<MetaWorld> Meta = new(BuildMeta);

    private static MetaWorld BuildMeta()
    {
        string path = FakeMapTests.MetaPath();
        if (path == null)
            return null;
        var world = new MetaWorld { Map = FakeMap.Load(path) };
        var tables = new AppearanceTables();
        world.Binding = world.Map.Bind(tables);
        // The planner gets the icon state the client sees, the bound appearance's (MapRenderer.ToPlan): the fake feed's smoothing junction
        // of a window (FakeMap.Bind), which M5 slice 5's WindowJunction reads.
        string SeenState(TableRef look, string raw)
        {
            if (look.IsNone || look.Index < 1 || look.Index > tables.Appearances.Count)
                return raw;
            TableRef state = tables.Appearances[(int)look.Index - 1].IconState;
            return state.IsNone || state.Index < 1 || state.Index > tables.Strings.Count ? raw : tables.Strings[(int)state.Index - 1];
        }
        var entities = world.Map.Entities.Select((e, i) => new PlanEntity(e.Id, e.TypePath, e.X, e.Y, e.Z, e.Dir, world.Binding.EntityAppearances[i], e.PixelX, e.PixelY, e.Light, SeenState(world.Binding.EntityAppearances[i], e.IconState), e.Plane, e.Layer)).ToList();
        world.Plan = ChunkPlanner.Plan(world.Binding.Grid, null, entities);
        world.Placed = world.Plan.Chunks.SelectMany(c => c.Placed).ToDictionary(p => p.EntityId);
        world.Entities = world.Map.Entities.ToDictionary(e => e.Id);
        MapGrid grid = world.Binding.Grid;
        ushort[] cells = grid.ExpandCells();
        world.Closed = new bool[cells.Length];
        var kinds = grid.TurfTypes.Select(TurfStyle.Classify).ToArray();
        for (int i = 0; i < cells.Length; i++)
            world.Closed[i] = kinds[cells[i]] == TurfKind.Closed;
        foreach (FakeMapEntity e in world.Map.Entities)
        {
            if (PropKinds.IsFullTileWindow(e.TypePath))
                world.Windows.Add((e.X, e.Y));
        }
        return world;
    }

    /// <summary>The planned Meta, or null (and a note in the test output) without the converted map.</summary>
    protected MetaWorld RequireMeta()
    {
        MetaWorld world = Meta.Value;
        if (world == null)
            _output.WriteLine("no converted MetaStation (maps/map_files/MetaStation/MetaStation.as3dmap or ANOMALY_META_MAP): skipped");
        return world;
    }

    /// <summary>The paths of every generated class of tg.</summary>
    protected static List<string> AllClassPaths()
    {
        var paths = new List<string>();
        foreach (Type type in ReflectionTypeSource.TgAssembly.GetTypes())
        {
            DmPathAttribute path = type.GetCustomAttribute<DmPathAttribute>(false);
            if (path != null && !type.IsAbstract)
                paths.Add(path.Path);
        }
        return paths;
    }

    protected static PlanEntity Entity(long id, string type, int x, int y, byte dir = 2, short px = 0, short py = 0)
        => new(id, type, x, y, 1, dir, TableRef.Public((uint)id), px, py, LightState.None, null);

    /// <summary>A grid of open floor.</summary>
    protected static MapGrid Grid(int width, int height)
    {
        var grid = new MapGrid { Width = width, Height = height, Levels = 1 };
        grid.TurfTypes.Add("/turf/open/floor/iron");
        for (int i = 0; i < width * height; i++)
            grid.Runs.Add(new TurfRun(1, 0));
        return grid;
    }
}
