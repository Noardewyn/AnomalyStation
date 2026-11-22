using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Tgui;
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
/// Gaps package P2 (code/modules/hydroponics) on MetaStation through the real server and a client's own stack. A botanist buys a wheat
/// seed pack from Hydroponics' MegaSeed Servitor through its Vending window, plants it in one of the room's trays by clicking it, lets the
/// tray run dry (its low-water light comes on) and waters it with a watering can (the light goes out). The tray's cycles then grow the
/// wheat: the client sees the plant's growth stages on the tray (tg's "wheat-growN" overlays) up to the ripe plant and the harvest light;
/// a click with an empty hand harvests the wheat and the plant is gone from the tray. The test skips the 20 seconds between two of the
/// tray's cycles (its lastcycle is moved back); every cycle itself is tg's process.
/// </summary>
public class HydroponicsGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void Do(RealHeadlessWorld w, Action a)
    {
        using (w.World.Enter())
            a();
    }

    private static UiNode Shown(TguiWindows windows, ReplicaWorld replica, int index)
    {
        windows.Sync(replica.UiWindows, 1280, 720, 1);
        return windows.Get(index)?.Content;
    }

    private static ClickTarget On(atom a) => new(TargetKind.Entity, a.dm_id, 0, 0, 0, 16, 16);

    private static bool Clear(turf t) => t is turf_open && !Dm.truthy(t.density) && !Dm.each_cast<atom>(t.contents).Any(a => Dm.truthy(a.density));

    private static turf ClearNeighbour(atom a)
        => new[] { SOUTH, NORTH, EAST, WEST }.Select(d => get_step(a, d) as turf)
            .FirstOrDefault(t => t != null && Clear(t) && Dm.truthy(Waiting.NoSleep(a.Adjacent(t))));

    // the tray's own picture and its overlays (the plant, the status lights) as the client holds them
    private static List<string> Seen(ReplicaWorld replica, atom a)
    {
        Assert.True(replica.TryGet(a.dm_id, out ReplicaEntity seen));
        return CryoGameplayTests.States(replica, seen.State.Appearance);
    }

    private static void Click(RealHeadlessWorld w, RealHeadlessWorld.Player p, atom a)
    {
        Assert.True(p.Session.Click(On(a), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
    }

    [MetaServerFact]
    public void A_botanist_plants_a_seed_from_the_seed_vendor_in_a_tray_waters_it_and_sees_it_grow_to_the_harvest()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 96, TimeoutSteps = 20000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Botanist");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        var windows = new TguiWindows
        {
            SendAct = (window, action, parameters) => p.Session.UiAct(window, action, parameters),
            SendClose = window => p.Session.UiClose(window),
        };
        w.Run(Second);
        int bootRuntimes = w.World.RuntimeErrorCount; // Meta's boot reaches code of later milestones (strict: false)

        // Hydroponics' seed vendor and an empty tray of the room, each with a clear tile beside it
        obj_machinery_vending_hydroseeds vendor = In(w, () => Dm.each<obj_machinery_vending_hydroseeds>()
            .Where(v => v.z == 1 && get_area(v) is area_station_service_hydroponics && ClearNeighbour(v) != null).OrderBy(v => v.dm_id).FirstOrDefault());
        Assert.NotNull(vendor);
        obj_machinery_hydroponics_constructable tray = In(w, () => Dm.each<obj_machinery_hydroponics_constructable>()
            .Where(t => t.z == 1 && get_area(t) is area_station_service_hydroponics && t.myseed == null && ClearNeighbour(t) != null).OrderBy(t => t.dm_id).FirstOrDefault());
        Assert.NotNull(tray);
        Assert.False(In(w, () => InertSeam.IsInert(tray)));
        output.WriteLine(In(w, () => $"{Dm.str(vendor.name)} at {vendor.x},{vendor.y}; {Dm.str(tray.name)} at {tray.x},{tray.y}: water {Dm.num(tray.waterlevel)}, nutrients {Dm.num(((datum_reagents)tray.reagents).total_volume)}"));

        // 1. the seed vendor's window: the wheat is on its Miscellaneous tab; a click on its card buys a seed pack
        Do(w, () =>
        {
            Waiting.NoSleep(me.drop_all_held_items());
            Waiting.NoSleep(me.forceMove(ClearNeighbour(vendor)));
        });
        w.Run(Second);
        Click(w, p, vendor);
        UiWindowState window = Assert.Single(replica.UiWindows.Tgui());
        Assert.Equal("Vending", window.Interface);
        UiNode content = Shown(windows, replica, window.Window);
        content.Walk().Single(n => n.Key == "category:Miscellaneous").OnClick();
        content = Shown(windows, replica, window.Window);
        string wheatKey = "product:" + In(w, () => obj_machinery_vending.SANITIZED_PATH(typeof(obj_item_seeds_wheat)));
        content.Walk().Single(n => n.Key == wheatKey).OnClick();
        w.Run(Second);
        var seed = In(w, () => me.get_active_held_item() as obj_item_seeds_wheat);
        Assert.NotNull(seed);
        p.Session.UiClose(window.Window);
        w.Run(Second);

        // 2. planting: a click on the tray with the seed pack puts it in; the client sees the seedling on the tray
        Do(w, () => Waiting.NoSleep(me.forceMove(ClearNeighbour(tray))));
        w.Run(Second);
        Click(w, p, tray);
        Assert.Same(seed, In(w, () => tray.myseed));
        Assert.Null(In(w, () => me.get_active_held_item()));
        var (growthstages, maturation, icon_grow, icon_harvest) = In(w, () => ((int)Dm.num(seed.growthstages), Dm.num(seed.maturation), Dm.str(seed.icon_grow), Dm.str(seed.icon_harvest)));
        Assert.Contains(icon_grow + "1", Seen(replica, tray));
        Assert.Contains("Wheat Stalks", In(w, () => Dm.str(tray.name)));

        // 3. the tray runs dry: its low-water light comes on; the watering can's two pours (20 units each) put it out
        Do(w, () => tray.set_waterlevel(5));
        w.Run(Second);
        Assert.Contains("over_lowwater3", Seen(replica, tray));
        Do(w, () =>
        {
            // a watering can full of water, as Hydroponics' own (the room's are on its tables; this one is given to the botanist)
            var can = Dm.@new<obj_item_reagent_containers_cup_watering_can>(me.loc);
            Waiting.NoSleep(((datum_reagents)can.reagents).add_reagent(typeof(datum_reagent_water), 100));
            Waiting.NoSleep(me.put_in_active_hand(can));
        });
        Click(w, p, tray);
        Click(w, p, tray);
        Assert.Equal(45f, In(w, () => Dm.num(tray.waterlevel)));
        Assert.DoesNotContain("over_lowwater3", Seen(replica, tray));
        Do(w, () => Waiting.NoSleep(me.drop_all_held_items()));

        // 4. the tray's cycles: the client sees the wheat's growth stages one after another, then the ripe wheat and the harvest light
        var stages = new List<string>();
        for (int cycle = 0; cycle < 20 && In(w, () => Dm.str(tray.plant_status)) != HYDROTRAY_PLANT_HARVESTABLE; cycle++)
        {
            Do(w, () => tray.lastcycle = Dm.num(world.time) - Dm.num(tray.cycledelay) - 1);
            w.Run(Second * 3); // the machines' subsystem runs every 2 seconds
            List<string> seen = Seen(replica, tray);
            string plant = seen.FirstOrDefault(s => s != null && (s.StartsWith(icon_grow, StringComparison.Ordinal) || s == icon_harvest));
            output.WriteLine(In(w, () => $"cycle {cycle + 1}: age {Dm.num(tray.age)}, health {Dm.num(tray.plant_health)}, water {Dm.num(tray.waterlevel)}, status {Dm.str(tray.plant_status)}; the client sees {string.Join(", ", seen)}"));
            if (plant != null && (stages.Count == 0 || stages[^1] != plant))
                stages.Add(plant);
        }
        Assert.Equal(HYDROTRAY_PLANT_HARVESTABLE, In(w, () => Dm.str(tray.plant_status)));
        output.WriteLine("the stages seen: " + string.Join(" -> ", stages));
        List<int> grown = stages.Where(s => s != icon_harvest).Select(s => int.Parse(s[icon_grow.Length..])).ToList();
        Assert.True(grown.Count >= 3, "at least three growth stages are seen");
        Assert.Equal(grown.OrderBy(n => n), grown); // the stages only go up
        Assert.Equal(growthstages, grown[^1]); // the last before the ripe plant: clamp(round(age / maturation * growthstages)) at maturation
        Assert.Equal(icon_harvest, stages[^1]);
        Assert.Contains("over_harvest3", Seen(replica, tray));
        Assert.Equal(6f, maturation);

        // 5. the harvest: a click with an empty hand picks the wheat; a wheat plant grows once, so the tray is empty again
        Click(w, p, tray);
        int wheat = In(w, () => Dm.each<obj_item_food_grown_wheat>().Count(g => g.z == 1 && Dm.truthy(get_dist(g, tray) <= 1)));
        output.WriteLine($"harvested {wheat} wheat");
        Assert.True(wheat > 0);
        Assert.Null(In(w, () => tray.myseed));
        List<string> after = Seen(replica, tray);
        Assert.DoesNotContain(after, s => s != null && (s.StartsWith(icon_grow, StringComparison.Ordinal) || s == icon_harvest || s == "over_harvest3"));

        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
