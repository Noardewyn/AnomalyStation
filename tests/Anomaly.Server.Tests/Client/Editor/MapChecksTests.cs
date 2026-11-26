using Anomaly.Client.Shared.Editor;
using Anomaly.Core.Tg;

namespace Anomaly.Server.Tests.Client.Editor;

/// <summary>The map editor's checks (docs/ai/specs/map-editor.md 5.13): tg's lint rules of one tile, and the editor's own.</summary>
public sealed class MapChecksTests
{
    private const string Space = "/turf/open/space/basic | /area/space";
    private const string Hall = " | /area/station/hallway/primary/central";

    private static List<MapProblem> Check(params (int X, int Y, string Members)[] tiles)
    {
        MapDocument map = MapDocument.Create(8, 8, 1, Space);
        // (a way in, so that the map's own problem is not in every list)
        map.Edit("join", new[] { (8, 8, 1, map.Model("/obj/effect/landmark/latejoin | /turf/open/floor/iron" + Hall)) });
        map.Edit("tiles", tiles.Select(t => (t.X, t.Y, 1, map.Model(t.Members))).ToList());
        return MapChecks.Run(map, 100, out _);
    }

    [Fact]
    public void PlayFindsTheNearestSpawnPointOfTheLevelFirst()
    {
        MapDocument map = MapDocument.Create(8, 8, 2, Space);
        Assert.Null(MapChecks.StartPoint(map, 1, 1, 1));
        map.Edit("starts", new[]
        {
            (1, 1, 1, map.Model("/obj/effect/landmark/start/new_player | /turf/open/floor/iron" + Hall)), // the lobby's: not a spawn point
            (2, 2, 2, map.Model("/obj/effect/landmark/start/assistant | /turf/open/floor/iron" + Hall)),
            (7, 7, 1, map.Model("/obj/effect/landmark/start/captain | /turf/open/floor/iron" + Hall)),
            (5, 5, 1, map.Model("/obj/effect/landmark/start/cook | /turf/open/floor/iron" + Hall)),
        });
        Assert.Equal((5, 5, 1), MapChecks.StartPoint(map, 1, 1, 1));
        Assert.Equal((2, 2, 2), MapChecks.StartPoint(map, 8, 8, 2));
    }

    [Fact]
    public void WhatMustNotShareATileIsFound()
    {
        MapProblem airlocks = Assert.Single(Check((2, 2, "/obj/machinery/door/airlock | /obj/machinery/door/airlock/command | /turf/open/floor/iron" + Hall)));
        Assert.Equal(("multiple_airlocks", 2, 2, 1), (airlocks.Rule, airlocks.X, airlocks.Y, airlocks.Member));

        // identical only: two cables written alike, not a cable and another cable
        Assert.Empty(Check((2, 2, "/obj/structure/cable | /obj/structure/cable/layer1 | /turf/open/floor/iron" + Hall)));
        Assert.Equal("identical_cables", Assert.Single(Check((2, 2, "/obj/structure/cable | /obj/item/wrench | /obj/structure/cable | /turf/open/floor/iron" + Hall))).Rule);

        // exact: two firelocks, not a firelock and one of its kinds
        Assert.Empty(Check((2, 2, "/obj/machinery/door/firedoor | /obj/machinery/door/firedoor/border_only | /turf/open/floor/iron" + Hall)));

        // an airlock in a wall, a grille over a cable, a disposal unit with no trunk (and with one)
        Assert.Equal("wall_stacking", Assert.Single(Check((2, 2, "/obj/machinery/door/airlock | /turf/closed/wall" + Hall))).Rule);
        Assert.Equal("grille_stacking", Assert.Single(Check((2, 2, "/obj/structure/grille | /obj/structure/cable | /turf/open/floor/plating" + Hall))).Rule);
        Assert.Equal("disposals_without_trunks", Assert.Single(Check((2, 2, "/obj/machinery/disposal/bin | /turf/open/floor/iron" + Hall))).Rule);
        Assert.Empty(Check((2, 2, "/obj/machinery/disposal/bin | /obj/structure/disposalpipe/trunk | /turf/open/floor/iron" + Hall)));

        // a clean tile is clean
        Assert.Empty(Check((2, 2, "/obj/structure/table | /obj/item/wrench | /obj/machinery/light/directional/north | /turf/open/floor/iron" + Hall), (2, 3, "/turf/closed/wall" + Hall)));
    }

    // tg's pipe construction rule (construction.dm:277), which tg's lint has not: two atmos machines on one layer of a tile that connect a way in common
    [Fact]
    public void AtmosMachinesThatShareALayerAndAWayAreFound()
    {
        const string Manifold = "/obj/machinery/atmospherics/pipe/smart/manifold4w/supply/hidden/layer4";
        // the Demo map's tile: a vent on the manifold's layer, facing east, where the manifold connects too
        MapProblem vent = Assert.Single(Check((2, 2, "/obj/machinery/atmospherics/components/unary/vent_pump/on/layer4{dir = 4} | " + Manifold + " | /turf/open/floor/iron" + Hall)));
        Assert.Equal(("pipe_conflict", 1), (vent.Rule, vent.Member));
        // on another layer it is no conflict (a scrubber of layer 2 beside the supply of layer 4, as tg's maps lay them)
        Assert.Empty(Check((2, 2, "/obj/machinery/atmospherics/components/unary/vent_scrubber/on/layer2{dir = 4} | " + Manifold + " | /turf/open/floor/iron" + Hall)));
        // a straight pipe and a pump on one layer meet where both run; two pumps turned across each other do not
        Assert.Equal("pipe_conflict", Assert.Single(Check((2, 2, "/obj/machinery/atmospherics/components/binary/pump{dir = 4} | /obj/machinery/atmospherics/components/binary/pump{dir = 8} | /turf/open/floor/iron" + Hall))).Rule);
        Assert.Empty(Check((2, 2, "/obj/machinery/atmospherics/components/binary/pump{dir = 4} | /obj/machinery/atmospherics/components/binary/pump{dir = 1} | /turf/open/floor/iron" + Hall)));
        // a layer manifold is on all layers
        Assert.Equal("pipe_conflict", Assert.Single(Check((2, 2, "/obj/machinery/atmospherics/pipe/layer_manifold{dir = 4} | /obj/machinery/atmospherics/components/binary/pump/layer2{dir = 4} | /turf/open/floor/iron" + Hall))).Rule);
    }

    // tg's own station passes tg's lint: the rules taken from it find nothing on MetaStation, and the editor's own are told for a look.
    [Fact]
    [Trait("Category", "Meta")]
    public void MetaStationPassesTgsRules()
    {
        string meta = Anomaly.Server.Tests.Client.World.FakeMapTests.MetaPath();
        if (meta == null)
            return; // no converted MetaStation: skipped
        MapDocument station = MapDocument.Parse(System.IO.File.ReadAllText(meta), "MetaStation");
        List<MapProblem> found = MapChecks.Run(station, 100000, out int total);
        var byRule = found.GroupBy(p => p.Rule).ToDictionary(g => g.Key, g => g.ToList());
        string told = string.Join("; ", byRule.Select(r => r.Key + " " + r.Value.Count + " (first at " + r.Value[0].X + ", " + r.Value[0].Y + ": " + station[r.Value[0].X, r.Value[0].Y, r.Value[0].Z].Text + ")"));
        // (tg's pipe construction rule is no lint of tg's: its mappers lay a few such tiles where nothing beside them breaks)
        string[] own = { "space_area", "wall_mount", "no_arrivals", "shuttle_ports", "pipe_conflict", "space_open_space" };
        Assert.True(byRule.Keys.All(rule => own.Contains(rule)), told);
        Assert.True(total == found.Count, told);
        Assert.True(!byRule.ContainsKey("no_arrivals") && !byRule.ContainsKey("shuttle_ports"), told);
        // the editor's own are few on a finished station: what they find there is told when they grow
        Assert.True(byRule.GetValueOrDefault("space_area")?.Count is null or < 40, told);
        Assert.True(byRule.GetValueOrDefault("wall_mount")?.Count is null or < 40, told);
        Assert.True(byRule.GetValueOrDefault("pipe_conflict")?.Count is null or < 10, told);
        Assert.False(byRule.ContainsKey("space_open_space"), told);
    }

    [Fact]
    public void TheEditorsOwnChecks()
    {
        // a floor left in the area of space
        MapProblem floor = Assert.Single(Check((2, 2, "/turf/open/floor/iron | /area/space")));
        Assert.Equal(("space_area", 1), (floor.Rule, floor.Member));

        // open space of space in a room's area lets the room's air out; the room's own open space, and space's in the area of space, do not
        Assert.Equal("space_open_space", Assert.Single(Check((2, 2, "/turf/open/space/openspace" + Hall))).Rule);
        Assert.Empty(Check((2, 2, "/turf/open/openspace" + Hall), (3, 2, "/turf/open/space/openspace | /area/space")));

        // a lamp hung on a side with no wall there; with a wall, and with a window that fills its tile, it holds
        Assert.Equal("wall_mount", Assert.Single(Check((2, 2, "/obj/machinery/light/directional/north | /turf/open/floor/iron" + Hall))).Rule);
        Assert.Empty(Check((2, 2, "/obj/machinery/light/directional/east | /turf/open/floor/iron" + Hall), (3, 2, "/obj/effect/spawner/structure/window/reinforced | /turf/open/floor/plating" + Hall)));

        // a map with no arrival point says so once; a shuttle's map does not, but has one port
        MapDocument empty = MapDocument.Create(4, 4, 1, Space);
        Assert.Equal("no_arrivals", Assert.Single(MapChecks.Run(empty, 100, out _)).Rule);
        MapDocument shuttle = MapDocument.Create(4, 4, 1, "/turf/template_noop | /area/template_noop");
        shuttle.Edit("ports", new[]
        {
            (1, 1, 1, shuttle.Model("/obj/docking_port/mobile | /turf/open/floor/mineral/titanium | /area/shuttle/supply")),
            (2, 1, 1, shuttle.Model("/obj/docking_port/mobile{dir = 4} | /turf/open/floor/mineral/titanium | /area/shuttle/supply")),
        });
        Assert.Equal("shuttle_ports", Assert.Single(MapChecks.Run(shuttle, 100, out _)).Rule);

        // under another level a tile's ceiling is the floor of the level above, and its map's word lays a floor tile under that floor's plating: a
        // word that is no such tile, and a tile with no plating over it, are named. On the top level the ceiling is the map's to set
        MapDocument decks = MapDocument.Create(3, 1, 2, Space);
        decks.Edit("ceilings", new[]
        {
            (1, 1, 1, decks.Model("/obj/effect/landmark/latejoin | /turf/open/floor/iron{ceiling = \"/turf/open/floor/glass\"}" + Hall)), // glass is no tile to lay
            (2, 1, 1, decks.Model("/turf/open/floor/iron{ceiling = \"/turf/open/floor/carpet\"}" + Hall)), // laid under the iron floor above: fine
            (3, 1, 1, decks.Model("/turf/open/floor/iron{ceiling = \"/turf/open/floor/carpet\"}" + Hall)), // nothing above to lie under
            (1, 1, 2, decks.Model("/turf/open/floor/iron{ceiling = \"none\"}" + Hall)),
            (2, 1, 2, decks.Model("/turf/open/floor/iron" + Hall)),
        });
        List<MapProblem> under = MapChecks.Run(decks, 100, out _);
        Assert.Equal(new[] { ("ceiling_under_level", 1, 1), ("ceiling_under_level", 3, 1) }, under.Select(p => (p.Rule, p.X, p.Z)));
        Assert.Contains("no floor tile", under[0].Text);
        Assert.Contains("no plating over it", under[1].Text);

        // the list is cut at its limit and the count is whole
        MapDocument many = MapDocument.Create(8, 8, 1, "/turf/open/floor/iron | /area/space");
        Assert.Equal(5, MapChecks.Run(many, 5, out int total).Count);
        Assert.Equal(65, total); // 64 floors in the area of space, and no arrival point
    }
}
