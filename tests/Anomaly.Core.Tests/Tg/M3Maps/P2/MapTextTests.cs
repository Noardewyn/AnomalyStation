using System.IO;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Xunit;

namespace Anomaly.Core.Tests.Tg.M3Maps.P2;

/// <summary>M3 P2: the engine map reader (MapText.Parse, MapValue.Parse and ToDm) against map-format.md 4 and tg's parse_constant.</summary>
public class MapTextTests
{
    private const string Small =
        "anomaly-map 1\n"
        + "source _maps/test.dmm tg 1d99bd7f\n"
        + "size 40 3 1\n"
        + "chunk 32\n"
        + "default /turf/open/space/basic | /area/space\n"
        + "\n"
        + "[chunk 1 1 1]\n"
        + "1,3: /obj/item/crowbar{name = \"bar | none\"; pixel_x = -4} | /turf/open/floor/iron | /area/station/hallway\n"
        + "2,3: /turf/open/floor/iron | /area/station/hallway\n"
        + "1,1: /turf/open/floor/iron | /area/station/hallway\n"
        + "\n"
        + "[chunk 33 1 1]\n"
        + "40,2: /obj/machinery/camera{network = list(\"ss13\",\"rd\"); c_tag = \"A; B\"} | /turf/open/floor/iron | /area/station/hallway\n";

    // D347: a map written before the game was named Anomaly Station has the old header
    [Fact]
    public void The_old_header_is_read()
        => Assert.Equal(40, MapText.Parse(Small.Replace("anomaly-map 1", "ss3d-map 1"), "old").Width);

    [Fact]
    public void Parses_the_header_models_and_tiles()
    {
        ParsedMapText map = MapText.Parse(Small, "small");
        Assert.Equal("_maps/test.dmm tg 1d99bd7f", map.Source);
        Assert.Equal((40, 3, 1), (map.Width, map.Height, map.Levels));
        Assert.Equal("/turf/open/space/basic | /area/space", map.Default.Text);
        Assert.Equal(4, map.Models.Count); // the default, then in order of first appearance
        Assert.Same(map.Default, map.Models[0]);
        Assert.Same(map.Default, map.ModelAt(3, 2, 1));
        Assert.Same(map.ModelAt(2, 3, 1), map.ModelAt(1, 1, 1));
        MapModel first = map.ModelAt(1, 3, 1);
        Assert.Equal(3, first.Members.Count);
        Assert.Equal("/obj/item/crowbar", first.Members[0].Path);
        Assert.Equal("name", first.Members[0].Overrides[0].Var);
        Assert.Equal("bar | none", first.Members[0].Overrides[0].Value.Text);
        Assert.Equal(-4f, first.Members[0].Overrides[1].Value.Number);
        Assert.Equal("/area/station/hallway", first.Members[2].Path);
        MapModel camera = map.ModelAt(40, 2, 1);
        Assert.Equal("A; B", camera.Members[0].Overrides[1].Value.Text);
        Assert.Equal(MapValueKind.List, camera.Members[0].Overrides[0].Value.Kind);
        Assert.Equal(new[] { (1, 3), (2, 3), (1, 1), (40, 2) }, map.Lines.Select(l => (l.X, l.Y)).ToArray());
        Assert.Throws<ArgumentOutOfRangeException>(() => map.ModelAt(41, 1, 1));
    }

    [Theory]
    [InlineData("anomaly-map 2\nsize 1 1 1\nchunk 32\ndefault /turf/a | /area/b\n", 1)]
    [InlineData("anomaly-map 1\nsize 1 1 1\nchunk 16\ndefault /turf/a | /area/b\n", 3)]
    [InlineData("anomaly-map 1\nsize 1 1 1\nchunk 32\ndefault /turf/a | /area/b \n", 4)]
    [InlineData("anomaly-map 1\nsize 2 2 1\nchunk 32\ndefault /turf/a | /area/b\n[chunk 1 1 1]\n1,1: /turf/c | /area/b\n1,2: /turf/c | /area/b\n", 7)]
    [InlineData("anomaly-map 1\nsize 2 2 1\nchunk 32\ndefault /turf/a | /area/b\n[chunk 1 1 1]\n3,1: /turf/c | /area/b\n", 6)]
    [InlineData("anomaly-map 1\nsize 2 2 1\nchunk 32\ndefault /turf/a | /area/b\n[chunk 1 1 1]\n1,1: /turf/a | /area/b\n", 6)]
    [InlineData("anomaly-map 1\nsize 2 2 1\nchunk 32\ndefault /turf/a | /area/b\n[chunk 1 1 1]\n1,1: /turf/c{dir = list(1} | /area/b\n", 6)]
    [InlineData("anomaly-map 1\nsize 2 2 1\nchunk 32\ndefault /turf/a | /area/b\n[chunk 1 1 1]\n1,1: turf/c | /area/b\n", 6)]
    [InlineData("anomaly-map 1\nsize 40 2 1\nchunk 32\ndefault /turf/a | /area/b\n[chunk 33 1 1]\n33,1: /turf/c | /area/b\n[chunk 1 1 1]\n1,1: /turf/c | /area/b\n", 7)]
    [InlineData("anomaly-map 1\nsize 2 2 1\nchunk 32\ndefault /turf/a | /area/b\n[chunk 1 1 1]\n1,1: /turf/c\t| /area/b\n", 6)]
    public void Refuses_what_section_4_does_not_allow(string text, int line)
    {
        MapTextException e = Assert.Throws<MapTextException>(() => MapText.Parse(text, "bad"));
        Assert.Equal(line, e.Line);
        Assert.Equal("bad", e.SourceName);
    }

    [Fact]
    public void Values_follow_parse_constant()
    {
        Assert.Equal(MapValueKind.Empty, MapValue.Parse("").Kind);
        Assert.Equal(-1f, MapValue.Parse("-1").Number);
        Assert.Equal(0.5f, MapValue.Parse("0.5").Number);
        Assert.Equal(1000000f, MapValue.Parse("1e+006").Number);
        Assert.Equal(200000f, MapValue.Parse("2e+005").Number);
        Assert.Equal(16f, MapValue.Parse("0x10").Number);
        Assert.Equal(4f, MapValue.Parse("4 2").Number); // text2num reads the leading number
        MapValue text = MapValue.Parse("\"\\improper Bob\\'s \\\"x\\\"\"");
        Assert.Equal(MapValueKind.Text, text.Kind);
        Assert.Equal("\\improper Bob\\'s \\\"x\\\"", text.Text); // not unescaped, as tg's copytext(text, 2, -1)
        Assert.Equal(MapValueKind.Path, MapValue.Parse("/obj/item/crowbar").Kind);
        MapValue file = MapValue.Parse("'icons/obj/x.dmi'");
        Assert.Equal((MapValueKind.Resource, "icons/obj/x.dmi"), (file.Kind, file.Text));
        Assert.Equal(MapValueKind.Null, MapValue.Parse("null").Kind);
        Assert.Equal(MapValueKind.Fallback, MapValue.Parse("NORTH").Kind);
        Assert.Equal(MapValueKind.Fallback, MapValue.Parse("new /obj()").Kind);

        MapValue list = MapValue.Parse("list(\"a\" = 1, b = list(2, \"x,y\"), /obj/x, 'f.ogg',)");
        Assert.Equal(MapValueKind.List, list.Kind);
        Assert.Equal(4, list.Items.Count);
        Assert.Equal("a", list.Items[0].Key.Text);
        Assert.Equal(1f, list.Items[0].Value.Number);
        Assert.Equal((MapValueKind.Fallback, "b"), (list.Items[1].Key.Kind, list.Items[1].Key.Text));
        Assert.Equal("x,y", list.Items[1].Value.Items[1].Value.Text);
        Assert.Null(list.Items[2].Key);
        Assert.Equal(MapValueKind.Path, list.Items[2].Value.Kind);
        Assert.Equal(MapValueKind.Resource, list.Items[3].Value.Kind);
        Assert.Empty(MapValue.Parse("list()").Items);
        Assert.Throws<MapTextException>(() => MapValue.Parse("list(\"a)"));
    }

    [Fact]
    public void ToDm_makes_new_dm_values_inside_a_world()
    {
        var world = new World(1);
        using (world.Enter())
        {
            MapValue value = MapValue.Parse("list(\"ss13\", \"rd\" = list(1, 2), /datum, null)");
            var a = (DmList)value.ToDm();
            var b = (DmList)value.ToDm();
            Assert.NotSame(a, b);
            Assert.Equal(4f, a.len);
            Assert.Equal("ss13", a[1f]);
            var inner = (DmList)a["rd"];
            Assert.Equal(2f, inner.len);
            Assert.Equal(typeof(datum), a[3f]);
            Assert.Null(a[4f]);
            Assert.Equal("", MapValue.Parse("").ToDm());
            Assert.Equal(3f, MapValue.Parse("3").ToDm());
            Assert.Null(MapValue.Parse("/no/such/type").ToDm());
            Assert.Equal("x", MapValue.Parse("'x'").ToDm().ToString());
        }
    }

    [Fact]
    public void Reads_the_converted_meta_with_its_counts()
    {
        string meta = MapFixtures.ConvertedMeta();
        if (meta == null) return; // skipped without the map
        ParsedMapText map = MapText.Parse(File.ReadAllText(meta), "meta");
        Assert.Equal((255, 255, 1), (map.Width, map.Height, map.Levels));
        // The map is checked in and edited in the map editor (D337): its counts are the mapper's now, so this holds that the station reads whole,
        // not what it holds to a thing. As converted from tg it had 18,347 tile lines, 171 areas and 35,364 things; the owner's map patches,
        // folded in on 2026-11-17, took three things away.
        Assert.InRange(map.Lines.Count(), 15000, 30000);
        var areas = new HashSet<string>();
        int movables = 0;
        for (int y = 1; y <= 255; y++)
        {
            for (int x = 1; x <= 255; x++)
            {
                MapModel model = map.ModelAt(x, y, 1);
                areas.Add(model.Members[^1].Path);
                movables += model.Members.Count(m => !m.Path.StartsWith("/turf/", StringComparison.Ordinal) && !m.Path.StartsWith("/area/", StringComparison.Ordinal));
            }
        }
        Assert.InRange(areas.Count, 150, 300);
        Assert.InRange(movables, 30000, 60000);
    }
}
