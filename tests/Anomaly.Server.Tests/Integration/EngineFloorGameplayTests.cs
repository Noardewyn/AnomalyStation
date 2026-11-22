using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// Engineering package E4 (reinforced_floor.dm) on MetaStation through the real server: an engineer stands in the supermatter room
/// and uses tools on one of its reinforced floor tiles (369 of them on Meta). A crowbar does nothing (the tile is not pried up);
/// a wrench takes the reinforcement rods off in three seconds with the ratchet's sound, two rods drop and the client sees the floor
/// turn into plating. The tiles around it are the same reinforced floor still.
/// </summary>
public class EngineFloorGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static bool Clear(turf t) => t is turf_open && !Dm.truthy(t.density) && !Dm.each_cast<atom>(t.contents).Any(a => Dm.truthy(a.density));

    private static turf Beside(atom thing)
    {
        foreach (float dir in new float[] { SOUTH, NORTH, EAST, WEST })
        {
            turf t = get_step(thing, dir);
            if (t != null && Clear(t))
                return t;
        }
        return null;
    }

    private static string FloorLook(ReplicaWorld replica, turf t)
        => replica.TryGetAppearance(replica.TurfAppearance((int)t.x, (int)t.y, (int)t.z), out AppearanceEntry look) ? replica.ResolveString(look.IconState) : null;

    private static void Hold(RealHeadlessWorld w, mob_living_carbon_human me, string path)
    {
        using (w.World.Enter())
        {
            foreach (object held in Dm.each(me.held_items.Copy()))
            {
                if (held is obj_item old)
                    GlobalProcs.qdel(old);
            }
            DevCommands.Give(me, path);
        }
    }

    [MetaServerFact]
    public void An_engineer_wrenches_a_reinforced_floor_tile_in_the_engine_room_into_plating_and_rods_and_a_crowbar_does_nothing()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 94, TimeoutSteps = 8000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Engineer");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        int bootRuntimes = w.World.RuntimeErrorCount;
        var heard = new List<SoundEvent>();
        replica.SoundsReceived += events => heard.AddRange(events);
        using (w.World.Enter())
            ADD_TRAIT(me, TRAIT_GODMODE, ADMIN_TRAIT); // the crystal's radiation and the engine room's air are not this test's

        // a plain reinforced floor tile of the supermatter room with a free tile beside it
        int engineTiles = In(w, () => Dm.each_cast<turf>(GLOB.station_turfs).Count(t => t is turf_open_floor_engine));
        turf tile = In(w, () => Dm.each_cast<turf>(GLOB.station_turfs)
            .Where(t => t.GetType() == typeof(turf_open_floor_engine) && get_area(t) is area_station_engineering_supermatter_room && Clear(t) && Beside(t) != null)
            .OrderBy(t => t.x).ThenBy(t => t.y).FirstOrDefault());
        Assert.NotNull(tile);
        output.WriteLine($"{engineTiles} reinforced floor tiles on the station; the test's tile at {tile.x},{tile.y}");
        Assert.True(engineTiles > 300, engineTiles.ToString());
        float x = In(w, () => tile.x), y = In(w, () => tile.y), z = In(w, () => tile.z);
        turf Here() => In(w, () => locate(x, y, z));
        Assert.Equal("engine", FloorLook(replica, tile));
        turf stand = In(w, () => Beside(tile));
        In(w, () => Waiting.NoSleep(me.forceMove(stand)));
        w.Run(Second);
        var target = new ClickTarget(TargetKind.Turf, 0, (int)x, (int)y, (int)z);

        // a crowbar: nothing happens (reinforced_floor.dm:41)
        Hold(w, me, "/obj/item/crowbar");
        Assert.True(p.Session.Click(target, ClickButton.Left, ClickModifiers.None));
        w.Run(3 * Second);
        Assert.IsType<turf_open_floor_engine>(Here());
        Assert.Empty(In(w, () => Dm.each_cast<atom>(Here().contents).OfType<obj_item_stack_rods>().ToList()));
        Assert.DoesNotContain(heard, h => h.File != null && (h.File ?? "").StartsWith("sound/items/tools/crowbar", StringComparison.Ordinal));

        // a wrench: three seconds, the ratchet, two rods and the plating
        Hold(w, me, "/obj/item/wrench");
        Assert.True(p.Session.Click(target, ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        Assert.IsType<turf_open_floor_engine>(Here()); // not yet
        w.Run(4 * Second);
        var plating = Assert.IsType<turf_open_floor_plating>(Here());
        obj_item_stack_rods rods = Assert.Single(In(w, () => Dm.each_cast<atom>(plating.contents).OfType<obj_item_stack_rods>().ToList()));
        Assert.Equal(2f, In(w, () => Dm.num(rods.amount)));
        Assert.Contains(heard, h => h.File != null && (h.File ?? "").StartsWith("sound/items/tools/ratchet", StringComparison.Ordinal));
        w.Run(Second);
        Assert.Equal("plating", FloorLook(replica, plating));
        Assert.Equal(engineTiles - 1, In(w, () => Dm.each_cast<turf>(GLOB.station_turfs).Count(t => t is turf_open_floor_engine)));

        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
