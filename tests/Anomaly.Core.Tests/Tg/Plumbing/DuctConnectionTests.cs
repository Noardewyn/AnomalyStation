using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Plumbing;

/// <summary>
/// tg's fluid ducts (modules/plumbing/ducts.dm): map-loaded ducts join the ducts beside them in post_machine_initialize
/// (:58) and take the icon state update_icon_state (:228) builds from their connected directions, in GLOB.cardinals order
/// (north, south, east, west). The expected states are worked out by hand from that code.
/// </summary>
public class DuctConnectionTests
{
    private static (TgToyRoom room, Dictionary<string, obj_machinery_duct> at) Boot()
    {
        var at = new Dictionary<string, obj_machinery_duct>();
        var room = TgToyRoom.Boot(16, 16, game: false, mapExtras: _ =>
        {
            void Duct(string name, int x, int y) => at[name] = Dm.@new<obj_machinery_duct>(locate(x, y, 1));
            // a straight run, west to east
            Duct("straight_w", 2, 2);
            Duct("straight_mid", 3, 2);
            Duct("straight_e", 4, 2);
            // an L: west and north of its corner
            Duct("bend_w", 2, 4);
            Duct("bend_corner", 3, 4);
            Duct("bend_n", 3, 5);
            // a T: west, east and north of its middle
            Duct("t_w", 2, 7);
            Duct("t_mid", 3, 7);
            Duct("t_e", 4, 7);
            Duct("t_n", 3, 8);
            // a lone duct
            Duct("lone", 8, 2);
        });
        return (room, at);
    }

    [Fact]
    public void MapLoadedDuctsJoinAndTakeTgsConnectionStates()
    {
        var (room, at) = Boot();
        using (room.World.Enter())
        {
            Assert.True(room.Runtimes.Count == 0, string.Join("\n", room.Runtimes));
            string State(string name) => at[name].icon_state as string;

            Assert.Equal("nduct_e", State("straight_w"));
            Assert.Equal("nduct_e_w", State("straight_mid"));
            Assert.Equal("nduct_w", State("straight_e"));

            Assert.Equal("nduct_e", State("bend_w"));
            Assert.Equal("nduct_n_w", State("bend_corner"));
            Assert.Equal("nduct_s", State("bend_n"));

            Assert.Equal("nduct_n_e_w", State("t_mid"));
            Assert.Equal("nduct_s", State("t_n"));

            Assert.Equal("nduct", State("lone"));

            // one ductnet per run, holding every duct of it
            datum_ductnet net = at["straight_mid"].net;
            Assert.NotNull(net);
            Assert.Same(net, at["straight_w"].net);
            Assert.Same(net, at["straight_e"].net);
            Assert.Equal(3, net.ducts.len);
            Assert.NotSame(net, at["bend_corner"].net);

            // the default layer and colour (ducts.dm:13, :11): no pixel offset
            Assert.Equal(THIRD_DUCT_LAYER, at["lone"].duct_layer);
            Assert.Equal(0f, at["lone"].pixel_x);
            Assert.Equal(ATMOS_COLOR_OMNI, at["lone"].color as string);
            // the toy room's floor tiles hide it (the undertile element, ducts.dm:52): on the floor plane, below the catwalk
            Assert.Equal(BELOW_CATWALK_LAYER, at["lone"].layer, 5);
            Assert.True(HAS_TRAIT(at["lone"], TRAIT_UNDERFLOOR));
        }
    }

    [Fact]
    public void DuctsOnOtherLayersOrColoursDoNotJoin()
    {
        var (room, at) = Boot();
        using (room.World.Enter())
        {
            // ducts.dm:17: new /obj/machinery/duct(loc, color, layer), as the duct stack places them (:340)
            var first = Dm.@new<obj_machinery_duct>(locate(10, 10, 1), null, FIRST_DUCT_LAYER);
            var beside = Dm.@new<obj_machinery_duct>(locate(11, 10, 1));
            var red = Dm.@new<obj_machinery_duct>(locate(10, 12, 1), COLOR_RED, null);
            var blue = Dm.@new<obj_machinery_duct>(locate(11, 12, 1), COLOR_BLUE, null);
            var omni = Dm.@new<obj_machinery_duct>(locate(12, 12, 1));

            Assert.Equal(-10f, first.pixel_x);
            Assert.Equal(-10f, first.pixel_y);
            Assert.Equal("nduct", first.icon_state as string);
            Assert.Equal("nduct", beside.icon_state as string);
            // red and blue do not join; blue joins the omni duct east of it
            Assert.Equal("nduct", red.icon_state as string);
            Assert.Equal("nduct_e", blue.icon_state as string);
            Assert.Equal("nduct_w", omni.icon_state as string);
        }
    }

    [Fact]
    public void ADestroyedDuctLeavesItsNeighboursOnNewNets()
    {
        var (room, at) = Boot();
        using (room.World.Enter())
        {
            datum_ductnet old = at["t_mid"].net;
            qdel(at["t_mid"]);

            Assert.Equal("nduct", at["t_w"].icon_state as string);
            Assert.Equal("nduct", at["t_e"].icon_state as string);
            Assert.Equal("nduct", at["t_n"].icon_state as string);
            Assert.NotNull(at["t_w"].net);
            Assert.NotSame(old, at["t_w"].net);
            Assert.NotSame(at["t_w"].net, at["t_e"].net);
            Assert.Equal(1, at["t_n"].net.ducts.len);
        }
    }
}
