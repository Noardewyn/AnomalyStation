using Anomaly.Core.Tg;

namespace Anomaly.Tools.Maps.Tests;

/// <summary>The converter against map-format.md 3, 4 and 6, on the hand-written fixtures of tests/fixtures/maps.</summary>
public class ConverterTests
{
    internal static string RepoRoot()
    {
        for (string dir = AppContext.BaseDirectory; dir != null; dir = Path.GetDirectoryName(dir))
        {
            if (File.Exists(Path.Combine(dir, "AnomalyStation.sln"))) return dir;
        }
        throw new InvalidOperationException("no repository root above " + AppContext.BaseDirectory);
    }

    internal static string Fixture(string name) => File.ReadAllText(Path.Combine(RepoRoot(), "tests", "fixtures", "maps", name));

    // The types and vars of the fixtures (a small index with vars, so every rule is checked).
    internal static TypeIndex FixtureTypes()
    {
        var t = new TypeIndex();
        t.Add("/datum", null, new[] { "vars" });
        t.Add("/atom", "/datum", new[] { "name", "dir", "pixel_x", "pixel_y", "icon_state", "density" });
        t.Add("/turf", "/atom");
        t.Add("/turf/open", "/turf");
        t.Add("/turf/open/space", "/turf/open");
        t.Add("/turf/open/space/basic", "/turf/open/space");
        t.Add("/turf/open/floor", "/turf/open");
        t.Add("/turf/open/floor/iron", "/turf/open/floor");
        t.Add("/turf/closed", "/turf");
        t.Add("/turf/closed/wall", "/turf/closed");
        t.Add("/area", "/atom");
        t.Add("/area/space", "/area");
        t.Add("/area/station", "/area");
        t.Add("/area/station/hallway", "/area/station");
        t.Add("/obj", "/atom");
        t.Add("/obj/machinery", "/obj");
        t.Add("/obj/machinery/camera", "/obj/machinery", new[] { "c_tag", "network" });
        t.Add("/obj/machinery/camera/directional", "/obj/machinery/camera");
        t.Add("/obj/machinery/camera/directional/east", "/obj/machinery/camera/directional");
        t.Add("/obj/effect", "/obj");
        t.Add("/obj/effect/turf_decal", "/obj/effect");
        t.Add("/obj/effect/turf_decal/stripes", "/obj/effect/turf_decal");
        t.Add("/obj/effect/turf_decal/stripes/line", "/obj/effect/turf_decal/stripes");
        t.Add("/obj/effect/landmark", "/obj/effect");
        t.Add("/obj/effect/landmark/latejoin", "/obj/effect/landmark");
        t.Add("/obj/item", "/obj");
        t.Add("/obj/item/crowbar", "/obj/item");
        return t;
    }

    private static ConversionResult Convert(string dmm, bool allowBad = false)
        => MapConverter.Convert(dmm, "_maps/test/p2_small.dmm", FixtureTypes(), allowBad, "1d99bd7f");

    [Fact]
    public void Converts_the_tgm_fixture_to_the_checked_in_text()
    {
        ConversionResult result = Convert(Fixture("p2_small.dmm"));
        Assert.Empty(result.Findings);
        Assert.Equal(Fixture("p2_small.as3dmap"), result.Text);
        Assert.Equal((5, 4, 1, 20), (result.Width, result.Height, result.Levels, result.Tiles));
        Assert.Equal(12, result.Lines);
        Assert.Equal(2, result.Areas);
        Assert.Equal(4, result.Movables);
        Assert.Equal(44, result.Atoms);
    }

    [Fact]
    public void The_dmm_syntax_gives_the_same_map()
    {
        Assert.Equal(Convert(Fixture("p2_small.dmm")).Text, Convert(Fixture("p2_small_dmmsyntax.dmm")).Text);
    }

    [Fact]
    public void A_second_run_is_byte_equal_and_the_round_trip_holds()
    {
        string dmm = Fixture("p2_small.dmm");
        Assert.Equal(Convert(dmm).Text, Convert(dmm).Text);
        Assert.Empty(MapConverter.RoundTrip(dmm, Convert(dmm).Text, FixtureTypes()));
        Assert.Empty(MapConverter.RoundTrip(Fixture("p2_small_dmmsyntax.dmm"), Convert(dmm).Text, FixtureTypes()));
    }

    [Fact]
    public void A_one_object_edit_changes_one_line()
    {
        string dmm = Fixture("p2_small.dmm");
        string edited = dmm.Replace("\tpixel_x = -4;", "\tpixel_x = 6;");
        Assert.NotEqual(dmm, edited);
        string[] before = Convert(dmm).Text.Split('\n');
        string[] after = Convert(edited).Text.Split('\n');
        Assert.Equal(before.Length, after.Length);
        Assert.Equal(1, before.Zip(after).Count(p => p.First != p.Second));
    }

    [Fact]
    public void The_engine_reader_reads_the_output()
    {
        ParsedMapText map = MapText.Parse(Convert(Fixture("p2_small.dmm")).Text, "p2_small");
        Assert.Equal("_maps/test/p2_small.dmm tg 1d99bd7f", map.Source);
        MapModel camera = map.ModelAt(2, 3, 1);
        Assert.Equal(new[] { "/obj/machinery/camera/directional/east", "/obj/effect/turf_decal/stripes/line", "/turf/open/floor/iron", "/area/station/hallway" },
            camera.Members.Select(m => m.Path).ToArray());
        Assert.Equal("Hall; North | East", camera.Members[0].Overrides[0].Value.Text);
        MapModel crowbar = map.ModelAt(3, 3, 1);
        Assert.Equal("\\improper Bob's \\\"bar\\\"", crowbar.Members[0].Overrides[0].Value.Text);
        Assert.Equal(200000f, crowbar.Members[0].Overrides[2].Value.Number);
        Assert.Equal("/turf/open/space/basic | /area/space", map.Default.Text);
    }

    [Theory]
    [InlineData("/obj/item/crowbar{", "/obj/item/no_such_crowbar{", 1)]
    [InlineData("/obj/effect/landmark/latejoin,\n/turf/open/floor/iron,\n/area/station/hallway)", "/obj/effect/landmark/latejoin,\n/area/station/hallway,\n/turf/open/floor/iron)", 2)]
    [InlineData("/obj/effect/landmark/latejoin,\n/turf/open/floor/iron,\n/area/station/hallway)", "/obj/effect/landmark/latejoin,\n/area/station/hallway)", 2)]
    [InlineData("/obj/effect/landmark/latejoin,\n/turf/open/floor/iron,\n/area/station/hallway)", "/turf/open/floor/iron,\n/obj/effect/landmark/latejoin,\n/area/station/hallway)", 2)]
    [InlineData("\tpixel_x = -4;", "\tno_such_var = -4;", 3)]
    [InlineData("\tpixel_x = -4;", "\tpixel_x = NORTH;", 4)]
    [InlineData("\tpixel_x = -4;", "\tpixel_x = list(\"a\", \"b\" = 1);", 4)]
    [InlineData("\tpixel_x = -4;", "\tpixel_x = new /obj/item();", 4)]
    [InlineData("\tpixel_x = -4;", "\tpixel_x = /obj/item/no_such_thing;", 5)]
    [InlineData("(5,1,1) = {\"\na\na\na\na\n\"}", "(5,1,1) = {\"\na\na\na\n\"}", 6)]
    [InlineData("(5,1,1) = {\"\na\na\na\na\n\"}", "(5,1,1) = {\"\na\na\na\na\n\"}\n(4,1,1) = {\"\na\n\"}", 6)]
    public void Each_validation_rule_refuses_the_map(string from, string to, int rule)
    {
        string dmm = Fixture("p2_small.dmm");
        Assert.Contains(from, dmm);
        ConversionResult result = Convert(dmm.Replace(from, to));
        Assert.Null(result.Text);
        Assert.Contains(result.Findings, f => f.Rule == rule);
    }

    [Fact]
    public void A_malformed_model_is_a_format_error()
    {
        string dmm = Fixture("p2_small.dmm").Replace("\tpixel_x = -4;", "\tpixel_x = list(\"a\";");
        FormatException e = Assert.Throws<FormatException>(() => Convert(dmm));
        Assert.Contains("p2_small.dmm:", e.Message);
    }

    [Fact]
    public void Allow_bad_paths_leaves_unknown_paths_and_vars_out_and_lists_them()
    {
        string dmm = Fixture("p2_small.dmm").Replace("/obj/item/crowbar{", "/obj/item/no_such_crowbar{").Replace("\tc_tag =", "\tno_tag =");
        ConversionResult refused = Convert(dmm);
        Assert.Null(refused.Text);
        ConversionResult allowed = Convert(dmm, allowBad: true);
        Assert.NotNull(allowed.Text);
        Assert.Equal(new[] { "/obj/item/no_such_crowbar", "/obj/machinery/camera/directional/east.no_tag" }, allowed.BadPaths.Keys.ToArray());
        Assert.Equal("d", allowed.BadPaths["/obj/item/no_such_crowbar"].Single());
        Assert.Contains("3,3: /turf/open/floor/iron | /area/station/hallway\n", allowed.Text);
        Assert.Contains("2,3: /obj/machinery/camera/directional/east{network = list(\"ss13\",\"rd\")} |", allowed.Text);
        Assert.Empty(MapConverter.RoundTrip(dmm, allowed.Text, FixtureTypes()));
    }

    [Fact]
    public void Meta_converts_with_its_counts_and_round_trips()
    {
        string tg = TgClone();
        if (tg == null) return; // skipped without the tg clone
        string dmm = File.ReadAllText(Path.Combine(tg, "_maps", "map_files", "MetaStation", "MetaStation.dmm"));
        ConversionResult result = MapConverter.Convert(dmm, "_maps/map_files/MetaStation/MetaStation.dmm", TypeIndex.Permissive(), false, null);
        Assert.Empty(result.Findings);
        Assert.Equal((65025, 18347, 165414, 2702, 171, 35364), (result.Tiles, result.Lines, result.Atoms, result.DistinctTypes, result.Areas, result.Movables));
        Assert.Empty(MapConverter.RoundTrip(dmm, result.Text, TypeIndex.Permissive()));
        Assert.Equal(result.Text, MapConverter.Convert(dmm, "_maps/map_files/MetaStation/MetaStation.dmm", TypeIndex.Permissive(), false, null).Text);
    }

    internal static string TgClone()
    {
        string env = Environment.GetEnvironmentVariable("ANOMALY_TG");
        if (!string.IsNullOrEmpty(env) && Directory.Exists(Path.Combine(env, "_maps"))) return env;
        for (string dir = RepoRoot(); dir != null; dir = Path.GetDirectoryName(dir))
        {
            string candidate = Path.Combine(dir, "tgstation");
            if (Directory.Exists(Path.Combine(candidate, "_maps"))) return candidate;
        }
        return null;
    }
}
