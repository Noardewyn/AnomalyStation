using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Xunit;

namespace Anomaly.Core.Tests.Tg.M3Maps.P2;

[DmPath("/obj/test_p2_mapvars_camera")]
public class obj_test_p2_mapvars_camera : obj
{
    public string c_tag = null;
    public DmList network = null;
    public float start_active = 0;
    public object anything = null;

    public override DmTask New(params object[] __extra) => DmTask.CompletedTask;
}

/// <summary>M3 P2: the preloader's by-name var set (MapVars.TrySet) and the map seam's per-world state (MapSeam).</summary>
public class MapVarsTests
{
    [Fact]
    public void Sets_fields_and_engine_vars_by_their_dm_names()
    {
        var world = new World(1);
        using (world.Enter())
        {
            var camera = Dm.@new<obj_test_p2_mapvars_camera>();
            Assert.True(MapVars.TrySet(camera, "c_tag", "Hall", out string error), error);
            Assert.Equal("Hall", camera.c_tag);
            Assert.True(MapVars.TrySet(camera, "start_active", 1f, out error), error);
            Assert.Equal(1f, camera.start_active);
            Assert.True(MapVars.TrySet(camera, "dir", 4f, out error), error);
            Assert.Equal(4f, camera.dir);
            Assert.True(MapVars.TrySet(camera, "name", "east camera", out error), error);
            Assert.Equal("east camera", camera.name);
            Assert.True(MapVars.TrySet(camera, "anything", typeof(obj), out error), error);
            Assert.Equal(typeof(obj), camera.anything);
        }
    }

    [Fact]
    public void A_list_is_deep_copied_and_an_unknown_var_is_an_error()
    {
        var world = new World(1);
        using (world.Enter())
        {
            var camera = Dm.@new<obj_test_p2_mapvars_camera>();
            var value = (DmList)MapValue.Parse("list(\"ss13\", \"rd\" = list(1))").ToDm();
            Assert.True(MapVars.TrySet(camera, "network", value, out string error), error);
            Assert.NotSame(value, camera.network);
            Assert.Equal(2f, camera.network.len);
            Assert.NotSame(value["rd"], camera.network["rd"]);
            Assert.False(MapVars.TrySet(camera, "no_such_var", 1f, out error));
            Assert.Contains("no_such_var", error);
        }
    }

    [Fact]
    public void The_selection_is_per_world()
    {
        var a = new World(1);
        var b = new World(1);
        Assert.Null(MapSeam.Selected(a));
        var selection = new MapSelection(System.IO.Path.Combine(MapFixtures.FixtureFolder(), "p2load"), "p2load");
        MapSeam.Select(a, selection);
        Assert.Same(selection, MapSeam.Selected(a));
        Assert.Null(MapSeam.Selected(b));
        Assert.Empty(MapSeam.LatejoinSpawns(a));
        Assert.Equal((7, 6), MapSeam.CompiledMapSize(a));
        Assert.Equal((0, 0), MapSeam.CompiledMapSize(b));
        // the json and the map file are in the world's file seam at tg's paths, the map file under its tg name
        DmWorldState state = DmWorldState.Of(a);
        Assert.Contains("\"map_name\": \"P2 Load\"", state.Files.ReadText("_maps/map_jsons/p2load.json"));
        Assert.StartsWith("anomaly-map 1\n", state.Files.ReadText("_maps/map_files/P2Load/P2Load.dmm"));
        Assert.Throws<ArgumentException>(() => MapSeam.Select(b, new MapSelection("", "x")));
        Assert.Throws<System.IO.FileNotFoundException>(() => MapSeam.Select(b, new MapSelection(MapFixtures.FixtureFolder(), "no_such_map")));
        Assert.Null(MapSeam.Selected(b));
    }
}
