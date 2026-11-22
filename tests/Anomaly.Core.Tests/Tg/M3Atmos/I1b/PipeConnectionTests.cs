using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M3Atmos.I1b;

/// <summary>
/// M3 I1b (D123): tg's node search (atmosmachinery.dm:267-359, SSair.setup_atmos_machinery, air.dm:710) and the pipes'
/// pictures (pipes.dm:124, smart.dm:18) on fixtures. The expected icon states are worked out by hand from tg's code: a full
/// pipe toward each node (CARDINAL_TO_FULLPIPES), and for a smart pipe that would be a stub, short pipes until it is not.
/// </summary>
public class PipeConnectionTests
{
    private sealed class Fixture
    {
        public readonly Dictionary<string, obj_machinery_atmospherics> At = new();
    }

    private static (TgToyRoom room, Fixture f) Boot()
    {
        var f = new Fixture();
        var room = TgToyRoom.Boot(24, 24, game: false, mapExtras: _ =>
        {
            void Pipe<T>(string name, int x, int y) where T : obj_machinery_atmospherics
                => f.At[name] = Dm.@new<T>(locate(x, y, 1));
            // straight, west to east
            Pipe<obj_machinery_atmospherics_pipe_smart_manifold4w_supply_visible>("straight_w", 2, 2);
            Pipe<obj_machinery_atmospherics_pipe_smart_manifold4w_supply_visible>("straight_mid", 3, 2);
            Pipe<obj_machinery_atmospherics_pipe_smart_manifold4w_supply_visible>("straight_e", 4, 2);
            // a bend: west and north of its corner
            Pipe<obj_machinery_atmospherics_pipe_smart_manifold4w_supply_visible>("bend_w", 2, 4);
            Pipe<obj_machinery_atmospherics_pipe_smart_manifold4w_supply_visible>("bend_corner", 3, 4);
            Pipe<obj_machinery_atmospherics_pipe_smart_manifold4w_supply_visible>("bend_n", 3, 5);
            // a manifold: west, east and north
            Pipe<obj_machinery_atmospherics_pipe_smart_manifold4w_supply_visible>("t_w", 2, 7);
            Pipe<obj_machinery_atmospherics_pipe_smart_manifold4w_supply_visible>("t_mid", 3, 7);
            Pipe<obj_machinery_atmospherics_pipe_smart_manifold4w_supply_visible>("t_e", 4, 7);
            Pipe<obj_machinery_atmospherics_pipe_smart_manifold4w_supply_visible>("t_n", 3, 8);
            // a 4-way
            Pipe<obj_machinery_atmospherics_pipe_smart_manifold4w_supply_visible>("x_mid", 3, 11);
            Pipe<obj_machinery_atmospherics_pipe_smart_manifold4w_supply_visible>("x_w", 2, 11);
            Pipe<obj_machinery_atmospherics_pipe_smart_manifold4w_supply_visible>("x_e", 4, 11);
            Pipe<obj_machinery_atmospherics_pipe_smart_manifold4w_supply_visible>("x_n", 3, 12);
            Pipe<obj_machinery_atmospherics_pipe_smart_manifold4w_supply_visible>("x_s", 3, 10);
            // a layer change: neighbours on layers 2 and 4 do not connect; nor do two colours (supply, scrubbers)
            Pipe<obj_machinery_atmospherics_pipe_smart_manifold4w_supply_visible_layer2>("layer2_alone", 2, 14);
            Pipe<obj_machinery_atmospherics_pipe_smart_manifold4w_supply_visible_layer4>("layer4_alone", 3, 14);
            Pipe<obj_machinery_atmospherics_pipe_smart_manifold4w_supply_visible>("supply_alone", 5, 14);
            Pipe<obj_machinery_atmospherics_pipe_smart_manifold4w_scrubbers_visible>("scrubbers_alone", 6, 14);
            // a layer adaptor between layer 2 (west) and layer 4 (east)
            Pipe<obj_machinery_atmospherics_pipe_smart_manifold4w_supply_visible_layer2>("adapted_2", 2, 16);
            f.At["adaptor"] = Dm.@new<obj_machinery_atmospherics_pipe_layer_manifold_supply_visible>(locate(3, 16, 1));
            f.At["adaptor"].dir = EAST; // a map var, set before SSatoms initializes it
            Pipe<obj_machinery_atmospherics_pipe_smart_manifold4w_supply_visible_layer4>("adapted_4", 4, 16);
            // a vent's end: a vent north of a pipe, facing south (its mapped default)
            Pipe<obj_machinery_atmospherics_pipe_smart_manifold4w_supply_visible>("vent_pipe", 2, 18);
            f.At["vent"] = Dm.@new<obj_machinery_atmospherics_components_unary_vent_pump>(locate(2, 19, 1));
        });
        return (room, f);
    }

    [Fact]
    public void SmartPipesTakeTgsConnectionStatesAfterSSairInitializes()
    {
        var (room, f) = Boot();
        using (room.World.Enter())
        {
            Assert.True(room.Runtimes.Count == 0, string.Join("\n", room.Runtimes));
            string State(string name) => f.At[name].icon_state as string;

            // straight: the middle has both ends (EAST|WEST = 12); each end one full pipe plus the short opposite one
            Assert.Equal("132_3", State("straight_w")); // EAST | WEST << 4
            Assert.Equal("12_3", State("straight_mid"));
            Assert.Equal("72_3", State("straight_e")); // WEST | EAST << 4
            Assert.Equal((float)EAST, f.At["straight_mid"].dir); // smart.dm:38: EAST|WEST faces EAST
            // bend
            Assert.Equal("9_3", State("bend_corner")); // WEST | NORTH
            Assert.Equal("18_3", State("bend_n")); // SOUTH | NORTH << 4
            // manifold and 4-way
            Assert.Equal("13_3", State("t_mid"));
            Assert.Equal("15_3", State("x_mid"));
            Assert.Equal(4 * UNARY_PIPE_VOLUME, Dm.num(((obj_machinery_atmospherics_pipe)f.At["x_mid"]).volume));
            Assert.Equal(2 * UNARY_PIPE_VOLUME, Dm.num(((obj_machinery_atmospherics_pipe)f.At["straight_e"]).volume));
            // a lone pipe: no node, the first two of its init directions as short pipes (NORTH|SOUTH << 4)
            Assert.Equal("48_2", State("layer2_alone"));
            Assert.Equal("48_4", State("layer4_alone"));
            Assert.Equal("48_3", State("supply_alone"));
            Assert.Equal("48_3", State("scrubbers_alone"));
            // the layer adaptor joins layers 2 and 4
            var adaptor = (obj_machinery_atmospherics_pipe_layer_manifold)f.At["adaptor"];
            Assert.Equal("132_2", State("adapted_2"));
            Assert.Equal("72_4", State("adapted_4"));
            Assert.True(Dm.@in(f.At["adapted_2"], adaptor.nodes));
            Assert.True(Dm.@in(f.At["adapted_4"], adaptor.nodes));
            Assert.Same(f.At["adapted_4"], adaptor.front_nodes[4]); // find_connecting(dir, 4)
            Assert.Same(f.At["adapted_2"], adaptor.back_nodes[2]);
            Assert.Equal("manifoldlayer_center", State("adaptor"));
            // a vent's end
            Assert.Equal("33_3", State("vent_pipe")); // NORTH | SOUTH << 4
            Assert.Same(f.At["vent"], f.At["vent_pipe"].nodes[1]);
            // the pipes are in SSair's machine list in their init order (air.dm:824)
            Assert.True(Dm.@in(f.At["x_mid"], SSair.atmos_machinery));
        }
    }

    [Fact]
    public void ADestroyedPipeLeavesItsNeighboursNodesAndTheyRedrawWhenReinitialized()
    {
        var (room, f) = Boot();
        using (room.World.Enter())
        {
            var east = f.At["straight_e"];
            var mid = f.At["straight_mid"];
            Assert.Same(east, mid.nodes[3]); // get_node_connects: NORTH, SOUTH, EAST, WEST
            qdel(east);
            // nullify_node -> the node's disconnect (atmosmachinery.dm:213, :407) clears the middle's node and redraws it
            Assert.Null(mid.nodes[3]);
            Assert.Equal("72_3", mid.icon_state as string); // WEST | EAST << 4: now a stub, so a short pipe east (smart.dm:52)
        }
    }
}
