using System.Text.RegularExpressions;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Server.Tests.Integration;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;

namespace Anomaly.Server.Tests.M3Maps.I1b;

/// <summary>
/// M3 I1b (D123): on the real server's Meta, every smart pipe takes the connection picture tg draws (R4 measured 0 of 5691).
/// The test is its own oracle: for each smart pipe it applies tg's connection rules (atmosmachinery.dm:319-359) to the
/// neighbours in the pipe's init directions, with each neighbour's init directions as tg's Initialize leaves them, and
/// compares the pipe's nodes with that. The atmospherics components (vents, scrubbers, pumps, ...) were inert in M3 (D112);
/// since M4b slice 2 most are live, and a component that is still inert is no node (DV31), so the pipes that end open where
/// tg's would join such a component are counted apart.
/// </summary>
public class MetaPipeTests
{
    private readonly ITestOutputHelper _output;

    public MetaPipeTests(ITestOutputHelper output) => _output = output;

    // tg's init directions after Initialize for the component bases (unary_devices.dm:13, binary_devices.dm:13,
    // trinary_devices.dm:18); null where a type has its own set_init_directions (tank, circulator) or is not a component.
    private static float? TgInitDirections(obj_machinery_atmospherics m)
    {
        if (!InertSeam.IsInert(m))
            return m.initialize_directions;
        float dir = m.dir;
        if (m is obj_machinery_atmospherics_components_unary)
            return dir;
        if (m is obj_machinery_atmospherics_components_binary_circulator)
            return null;
        if (m is obj_machinery_atmospherics_components_binary)
            return dir == NORTH || dir == SOUTH ? 1 | 2 : 4 | 8; // NORTH|SOUTH, EAST|WEST
        if (m is obj_machinery_atmospherics_components_trinary)
            return dir switch { NORTH => 4 | 1 | 2, SOUTH => 2 | 8 | 1, EAST => 4 | 8 | 2, _ => 8 | 1 | 4 }; // EAST|NORTH|SOUTH, SOUTH|WEST|NORTH, EAST|WEST|SOUTH, WEST|NORTH|EAST
        return null;
    }

    [MetaServerFact]
    public void Every_smart_pipe_on_Meta_takes_tgs_connection_picture()
    {
        using var world = new RealHeadlessWorld(new LoopbackOptions { Seed = 5, TimeoutSteps = 400 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        world.Run(2);
        int smart = 0, joinedState = 0, asTg = 0, differAtInertComponent = 0, differAtInertVisible = 0, differAtOwnInitDirections = 0, differOther = 0, unknownNeighbour = 0;
        var byState = new SortedDictionary<string, int>();
        var differ = new List<string>();
        using (world.World.Enter())
        {
            for (int y = 1; y <= MapView.Height(world.World); y++)
            for (int x = 1; x <= MapView.Width(world.World); x++)
            foreach (atom a in Dm.each_cast<atom>(MapView.TurfAt(world.World, x, y, 1).contents))
            {
                if (a is not obj_machinery_atmospherics_pipe_smart pipe)
                    continue;
                smart++;
                string state = pipe.icon_state as string ?? "";
                if (Regex.IsMatch(state, @"^\d+_\d+$")) joinedState++;
                byState[state] = byState.GetValueOrDefault(state) + 1;

                float actual = 0;
                for (int i = 1; i <= 4; i++)
                    if (pipe.nodes[i] is obj_machinery_atmospherics n)
                        actual = Dm.bitor(actual, Builtins.get_dir(pipe, n));
                float expected = 0;
                bool inertInvolved = false, unknown = false;
                foreach (float d in new float[] { NORTH, SOUTH, EAST, WEST })
                {
                    if (!Dm.truthy(Dm.bitand(pipe.initialize_directions, d)))
                        continue;
                    foreach (obj_machinery_atmospherics target in Dm.each<obj_machinery_atmospherics>(Builtins.get_step(pipe, d)))
                    {
                        float? tgDirs = TgInitDirections(target);
                        if (tgDirs == null) { unknown = true; continue; }
                        bool inert = InertSeam.IsInert(target);
                        bool tgConnects = Dm.truthy(Dm.bitand(tgDirs.Value, Builtins.get_dir(target, pipe)))
                            && Dm.truthy(pipe.is_connectable(target, pipe.piping_layer)) && Dm.truthy(target.is_connectable(pipe, pipe.piping_layer));
                        if (inert)
                        {
                            // DV31 (M4b slice 2): an inert device is no node, so the pipe ends open where tg's would join it
                            if (tgConnects)
                                inertInvolved = true;
                            continue;
                        }
                        if (tgConnects)
                        {
                            expected = Dm.bitor(expected, d);
                            break;
                        }
                    }
                }
                if (unknown) unknownNeighbour++;
                if (actual == expected) asTg++;
                else if (inertInvolved)
                {
                    differAtInertComponent++;
                    if (Dm.num(pipe.invisibility) <= 25) differAtInertVisible++; // D115: an entity the client draws
                }
                else if (unknown) differAtOwnInitDirections++;
                else
                {
                    differOther++;
                    if (differ.Count < 10) differ.Add($"{pipe.GetType().Name} at {x},{y}: nodes {actual}, tg {expected}, state {state}");
                }
            }
        }
        _output.WriteLine($"smart pipes {smart}; with a connection state {joinedState}; nodes as tg's rules {asTg}; differ at an inert component {differAtInertComponent} (of them visible {differAtInertVisible}); differ at a component with its own init directions {differAtOwnInitDirections} (next to one {unknownNeighbour}); differ otherwise {differOther}");
        _output.WriteLine("by state: " + string.Join(", ", byState.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {kv.Value}")));
        foreach (string d in differ) _output.WriteLine(d);
        Assert.True(smart > 5000, "smart pipes " + smart);
        Assert.Equal(smart, joinedState);
        Assert.Equal(0, differOther);
    }
}
