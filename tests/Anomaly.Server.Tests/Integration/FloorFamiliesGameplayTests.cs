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
/// Gaps package G2 (fancy_floor.dm) on MetaStation through the real server: a player unscrews a wood floor tile of one of Meta's
/// wooden rooms (480 wood tiles) with a screwdriver and pries a carpet tile (223 carpet tiles) with a crowbar. Each leaves the plating,
/// which the client sees, and its own floor tile on the plating, which the client sees as an entity of the tile's type; the
/// screwdriver's sound plays.
/// </summary>
public class FloorFamiliesGameplayTests(ITestOutputHelper output)
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

    // A plain tile of the floor type in a station room, free of things and with a free tile beside it.
    private static turf Pick(RealHeadlessWorld w, Type type) => In(w, () => Dm.each_cast<turf>(GLOB.station_turfs)
        .Where(t => t.GetType() == type && Clear(t) && !Dm.each_cast<atom>(t.contents).OfType<obj_item>().Any() && Beside(t) != null)
        .OrderBy(t => t.x).ThenBy(t => t.y).FirstOrDefault());

    // Stand beside the tile, hold the tool and click the tile; return the turf at that cell afterwards and the tile stacks on it.
    private static (turf After, List<obj_item_stack_tile> Tiles) UseOn(RealHeadlessWorld w, RealHeadlessWorld.Player p, mob_living_carbon_human me, turf tile, string tool)
    {
        float x = In(w, () => tile.x), y = In(w, () => tile.y), z = In(w, () => tile.z);
        In(w, () => Waiting.NoSleep(me.forceMove(Beside(tile))));
        w.Run(Second);
        Hold(w, me, tool);
        Assert.True(p.Session.Click(new ClickTarget(TargetKind.Turf, 0, (int)x, (int)y, (int)z), ClickButton.Left, ClickModifiers.None));
        w.Run(2 * Second);
        turf after = In(w, () => locate(x, y, z));
        return (after, In(w, () => Dm.each_cast<atom>(after.contents).OfType<obj_item_stack_tile>().ToList()));
    }

    [MetaServerFact]
    public void A_player_unscrews_a_wood_floor_tile_and_pries_a_carpet_tile_and_sees_the_plating_and_the_tiles()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 95, TimeoutSteps = 8000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Carpenter");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        int bootRuntimes = w.World.RuntimeErrorCount;
        var heard = new List<SoundEvent>();
        replica.SoundsReceived += events => heard.AddRange(events);
        using (w.World.Enter())
            ADD_TRAIT(me, TRAIT_GODMODE, ADMIN_TRAIT);

        int woodTiles = In(w, () => Dm.each_cast<turf>(GLOB.station_turfs).Count(t => t.GetType() == typeof(turf_open_floor_wood)));
        int carpetTiles = In(w, () => Dm.each_cast<turf>(GLOB.station_turfs).Count(t => t.GetType() == typeof(turf_open_floor_carpet)));
        output.WriteLine($"{woodTiles} wood and {carpetTiles} carpet tiles on the station");
        Assert.True(woodTiles > 400, woodTiles.ToString());
        Assert.True(carpetTiles > 150, carpetTiles.ToString());
        Assert.False(In(w, () => InertSeam.IsInert(Pick(w, typeof(turf_open_floor_wood)))), "Meta's wood floors are live");

        // the wood: a screwdriver unscrews the planks (fancy_floor.dm:29), its own tile drops, the plating shows
        turf wood = Pick(w, typeof(turf_open_floor_wood));
        Assert.NotNull(wood);
        output.WriteLine($"the wood tile at {In(w, () => wood.x)},{In(w, () => wood.y)} in {In(w, () => get_area(wood).GetType().Name)}");
        Assert.Equal("wood", FloorLook(replica, wood));
        var (afterWood, woodStacks) = UseOn(w, p, me, wood, "/obj/item/screwdriver");
        Assert.IsType<turf_open_floor_plating>(afterWood);
        obj_item_stack_tile plank = Assert.Single(woodStacks);
        Assert.IsType<obj_item_stack_tile_wood>(plank);
        Assert.Contains(heard, h => h.File != null && (h.File ?? "").StartsWith("sound/items/tools/screwdriver", StringComparison.Ordinal));
        w.Run(Second);
        Assert.Equal("plating", FloorLook(replica, afterWood));
        Assert.Contains(replica.Entities, e => e.Id == plank.dm_id && e.TypePath == "/obj/item/stack/tile/wood");

        // the carpet: a crowbar pries it (floor.dm:144), the carpet tile drops, the plating shows
        turf carpet = Pick(w, typeof(turf_open_floor_carpet));
        Assert.NotNull(carpet);
        output.WriteLine($"the carpet tile at {In(w, () => carpet.x)},{In(w, () => carpet.y)} in {In(w, () => get_area(carpet).GetType().Name)}");
        Assert.StartsWith("carpet-", FloorLook(replica, carpet));
        var (afterCarpet, carpetStacks) = UseOn(w, p, me, carpet, "/obj/item/crowbar");
        Assert.IsType<turf_open_floor_plating>(afterCarpet);
        obj_item_stack_tile rug = Assert.Single(carpetStacks);
        Assert.IsType<obj_item_stack_tile_carpet>(rug);
        w.Run(Second);
        Assert.Equal("plating", FloorLook(replica, afterCarpet));
        Assert.Contains(replica.Entities, e => e.Id == rug.dm_id && e.TypePath == "/obj/item/stack/tile/carpet");

        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
