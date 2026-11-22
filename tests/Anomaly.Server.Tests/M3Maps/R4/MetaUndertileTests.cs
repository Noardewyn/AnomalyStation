using System.Text.RegularExpressions;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Protocol;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Server.Tests.Integration;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Defines;

namespace Anomaly.Server.Tests.M3Maps.R4;

/// <summary>
/// M3 R4 (the owner saw pipes and cables everywhere): tg's undertile element hides the station's pipes, cables and disposal
/// pipes under an intact floor tile (invisibility INVISIBILITY_OBSERVER), so the replicator, which sends nothing above
/// SEE_INVISIBLE_LIVING, does not send them; on plating they show. tg's "visible" pipes (hide = FALSE: the atmos and engine
/// pipework, the heat exchangers) stay on top of the floor. The owner's empty map (MapJoinTests) made the plating's and
/// the visible pipework stand out.
/// </summary>
public class MetaUndertileTests
{
    private readonly ITestOutputHelper _output;

    public MetaUndertileTests(ITestOutputHelper output) => _output = output;

    [MetaServerFact]
    public void Metas_pipes_cables_and_disposals_under_floor_tiles_are_hidden_and_not_replicated_and_show_on_plating()
    {
        using var world = new RealHeadlessWorld(new LoopbackOptions { Seed = 5, TimeoutSteps = 400 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        world.Run(2);
        int underTiles = 0, hidden = 0, replicatedUnderTiles = 0, onPlating = 0, replicatedOnPlating = 0, visiblePipes = 0;
        using (world.World.Enter())
        {
            for (int y = 1; y <= MapView.Height(world.World); y++)
            {
                for (int x = 1; x <= MapView.Width(world.World); x++)
                {
                    turf t = MapView.TurfAt(world.World, x, y, 1);
                    bool tiled = Dm.num(t.underfloor_accessibility) < UNDERFLOOR_VISIBLE;
                    bool plating = t is turf_open_floor_plating;
                    foreach (atom a in Dm.each_cast<atom>(t.contents))
                    {
                        bool hides = a is obj_structure_cable || a is obj_structure_disposalpipe
                            || (a is obj_machinery_atmospherics_pipe pipe && Dm.truthy(pipe.hide));
                        if (a is obj_machinery_atmospherics_pipe p2 && !Dm.truthy(p2.hide)) visiblePipes++;
                        if (!hides) continue;
                        bool replicated = world.Replicator.TryGetEntity(a.dm_id, out _);
                        if (tiled)
                        {
                            underTiles++;
                            if (Dm.num(a.invisibility) > SEE_INVISIBLE_LIVING) hidden++;
                            if (replicated) replicatedUnderTiles++;
                        }
                        else if (plating)
                        {
                            onPlating++;
                            if (replicated) replicatedOnPlating++;
                        }
                    }
                }
            }
        }
        _output.WriteLine($"under floor tiles {underTiles}: hidden {hidden}, replicated {replicatedUnderTiles}; on plating {onPlating}: replicated {replicatedOnPlating}; visible pipes (hide = FALSE) {visiblePipes}");
        Assert.True(underTiles > 7000, "under tiles " + underTiles);
        Assert.Equal(underTiles, hidden);
        Assert.Equal(0, replicatedUnderTiles);
        Assert.True(onPlating > 2000, "on plating " + onPlating);
        Assert.Equal(onPlating, replicatedOnPlating);
    }

    /// <summary>
    /// The lead's D122 checks (the client draws an entity flat or upright from its replicated plane and layer): the replicated
    /// plane and layer of every pipe, cable and disposal pipe are the atom's current ones (undertile's SET_PLANE_IMPLICIT on
    /// a see-through floor included); and a cable under a tile that is pried up becomes an entity once undertile shows it.
    /// </summary>
    [MetaServerFact]
    public void Replicated_infrastructure_has_its_current_plane_and_layer_and_a_cable_under_a_pried_tile_spawns()
    {
        using var world = new RealHeadlessWorld(new LoopbackOptions { Seed = 5, TimeoutSteps = 400 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        world.Run(2);
        Snapshot snapshot = world.Replicator.BuildSnapshot(0, 0);
        int compared = 0, mismatched = 0, smartDefault = 0, smartJoined = 0;
        obj_structure_cable buried = null;
        turf_open_floor buriedTurf = null;
        using (world.World.Enter())
        {
            for (int y = 1; y <= MapView.Height(world.World); y++)
            {
                for (int x = 1; x <= MapView.Width(world.World); x++)
                {
                    turf t = MapView.TurfAt(world.World, x, y, 1);
                    foreach (atom a in Dm.each_cast<atom>(t.contents))
                    {
                        if (a is obj_machinery_atmospherics_pipe_smart smart)
                        {
                            if (Regex.IsMatch(smart.icon_state ?? "", @"^\d+_\d+$")) smartJoined++;
                            else smartDefault++;
                        }
                        if (!(a is obj_structure_cable || a is obj_structure_disposalpipe || a is obj_machinery_atmospherics_pipe)) continue;
                        if (world.Replicator.TryGetEntity(a.dm_id, out EntityState state))
                        {
                            AppearanceEntry entry = snapshot.Appearances[(int)state.Appearance.Index - 1];
                            compared++;
                            if (entry.Plane != (short)Dm.num(a.plane) || entry.Layer != Dm.num(a.layer)) mismatched++;
                        }
                        else if (buried == null && a is obj_structure_cable cable && t.GetType() == typeof(turf_open_floor_iron) && t is turf_open_floor floor
                            && Dm.num(t.underfloor_accessibility) < UNDERFLOOR_VISIBLE)
                        {
                            buried = cable;
                            buriedTurf = floor;
                        }
                    }
                }
            }
        }
        _output.WriteLine($"replicated pipes, cables and disposals compared {compared}, plane or layer differs {mismatched}; smart pipes with connection icon states {smartJoined}, with their mapped icon state {smartDefault}");
        Assert.True(compared > 2000, "compared " + compared);
        Assert.Equal(0, mismatched);

        Assert.NotNull(buried);
        using (world.World.Enter())
            Waiting.NoSleep(buriedTurf.ScrapeAway(null, CHANGETURF_INHERIT_AIR)); // tg's make_plating (floor.dm:110; M5 here)
        int ticks = 0;
        bool spawned = false;
        for (; ticks < 40 && !spawned; ticks++)
        {
            world.Run(1);
            spawned = world.Replicator.TryGetEntity(buried.dm_id, out _);
        }
        using (world.World.Enter())
        {
            _output.WriteLine($"the tile at {buried.x},{buried.y} pried up: {DmTypes.PathOf(MapView.TurfAt(world.World, (int)buried.x, (int)buried.y, 1).GetType())}, the cable's invisibility {buried.invisibility}, an entity after {ticks} ticks: {spawned}");
            Assert.True(Dm.num(buried.invisibility) <= SEE_INVISIBLE_LIVING);
        }
        Assert.True(spawned, "the cable under the pried tile was not replicated within 40 ticks");
    }
}
