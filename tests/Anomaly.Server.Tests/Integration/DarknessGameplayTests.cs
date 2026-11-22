using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// M7 lumcount (D170) on MetaStation through the real server: reading, writing and scanners need light, as in tg. A room's APC
/// breaker is switched off (<c>toggle_breaker</c>), its fixtures go dark, and a player standing on a tile whose measured lumcount
/// fell below LIGHTING_TILE_IS_DARK (0.2) alt-clicks the gas analyzer in his hand: NEED_LIGHT (gas_analyzer.dm:19, living.dm:1426)
/// refuses it with tg's "You need more light to do this!". With the breaker back on the same click gives the analyzer's own answer
/// (the barometer's "won't work indoors"); in the dark again, a flashlight lit in his other hand (lum_power 0.5) lets it through.
/// </summary>
public class DarknessGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;
    private const string TooDark = "You need more light to do this!";
    private const string Indoors = "barometer function won't work indoors!";

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void In(RealHeadlessWorld w, Action a)
    {
        using (w.World.Enter())
            a();
    }

    private static float Lum(turf t) => Dm.num(t.get_lumcount());

    [MetaServerFact]
    public void A_gas_analyzer_is_refused_in_a_room_gone_dark_works_when_the_power_returns_and_works_in_the_dark_with_a_flashlight()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 91, TimeoutSteps = 12000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        In(w, () => SSshuttle.arrivals.perma_docked = 1); // the spot is chosen by its light, which the arrivals shuttle may be
        RealHeadlessWorld.Player p = w.Join("Alice");
        var me = (mob_living_carbon_human)w.MobOf(p);
        var texts = new List<string>();
        p.Client.ServerTextReceived += t => texts.Add(t.Html);

        // a station room with an APC, several lit fixtures and the open floor round them
        (obj_machinery_power_apc apc, area room, List<obj_machinery_light> fixtures) = In(w, () =>
        {
            foreach (obj_machinery_power_apc a in Dm.each<obj_machinery_power_apc>(Dm.world).OrderBy(x => x.dm_id))
            {
                area ar = a.area;
                if (ar == null || !Dm.eq(a.z, 1) || !Dm.@in(ar.type, GLOB.the_station_areas) || !Dm.truthy(ar.requires_power) || a.cell == null || !Dm.truthy(a.operating))
                    continue;
                var lit = Dm.each<obj_machinery_light>(Dm.world)
                    .Where(l => ReferenceEquals(get_area(l), ar) && Dm.truthy(l.on) && Dm.eq(l.status, LIGHT_OK) && l.loc is turf).ToList();
                if (lit.Count >= 3)
                    return (a, ar, lit);
            }
            return (null, null, null);
        });
        Assert.NotNull(apc);
        output.WriteLine($"{In(w, () => Dm.str(room.name))}: the APC at {In(w, () => apc.x)},{In(w, () => apc.y)}, {fixtures.Count} lit fixtures");
        List<turf> candidates = In(w, () => fixtures.SelectMany(f => Dm.each_cast<turf>(RANGE_TURFS(1, (turf)f.loc)))
            .Where(t => t is turf_open && ReferenceEquals(get_area(t), room) && !Dm.truthy(t.density)).Distinct().ToList());
        In(w, () => Waiting.NoSleep(me.forceMove(candidates[0])));
        w.Run(3 * Second);
        int bootRuntimes = w.World.RuntimeErrorCount; // Meta's boot reaches code of later milestones (strict: false)
        Dictionary<turf, float> lit = In(w, () => candidates.ToDictionary(t => t, Lum));
        Assert.Contains(lit.Values, v => v > LIGHTING_TILE_IS_DARK);

        // the breaker off (apc_power_proc.dm:43): the fixtures take their emergency cells, which run out
        In(w, () => Waiting.NoSleep(apc.toggle_breaker()));
        w.Run(3 * Second);
        Assert.False(In(w, () => Dm.truthy(apc.operating)));
        In(w, () =>
        {
            foreach (obj_machinery_light f in fixtures)
                ((obj_item_stock_parts_power_store)f.get_cell()).charge = 1;
        });
        w.Run(6 * Second);
        Dictionary<turf, float> dark = In(w, () => candidates.ToDictionary(t => t, Lum));

        // the tile: lit before, dark now (emergency lights may keep other tiles of the room above the limit)
        turf spot = candidates.Where(t => lit[t] > LIGHTING_TILE_IS_DARK && dark[t] < LIGHTING_TILE_IS_DARK).OrderBy(t => dark[t]).FirstOrDefault();
        output.WriteLine($"lumcount before {string.Join(" ", candidates.Select(t => lit[t].ToString("0.00")))}; after {string.Join(" ", candidates.Select(t => dark[t].ToString("0.00")))}");
        Assert.NotNull(spot);
        In(w, () => Waiting.NoSleep(me.forceMove(spot)));
        w.Run(Second);
        Assert.False(In(w, () => Dm.truthy(me.has_light_nearby())));

        w.Host.EnqueueConsoleLine("give Alice /obj/item/analyzer");
        w.Run(5);
        var analyzer = In(w, () => me.get_active_held_item() as obj_item_analyzer);
        Assert.NotNull(analyzer);
        void AltClick() => Assert.True(p.Session.Click(new ClickTarget(TargetKind.Screen, analyzer.dm_id, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.Alt));

        // refused in the dark
        texts.Clear();
        AltClick();
        w.Run(Second);
        Assert.Contains(texts, t => t.Contains(TooDark, StringComparison.Ordinal));
        Assert.DoesNotContain(texts, t => t.Contains(Indoors, StringComparison.Ordinal));

        // the power returns: the same tile is lit and the click reaches the analyzer
        In(w, () => Waiting.NoSleep(apc.toggle_breaker()));
        w.Run(8 * Second);
        Assert.True(In(w, () => Dm.truthy(apc.operating)));
        Assert.True(In(w, () => Dm.truthy(me.has_light_nearby())), $"the tile's lumcount {In(w, () => Lum(spot))}");
        texts.Clear();
        AltClick();
        w.Run(Second);
        Assert.DoesNotContain(texts, t => t.Contains(TooDark, StringComparison.Ordinal));
        Assert.Contains(texts, t => t.Contains(Indoors, StringComparison.Ordinal));

        // dark again: a flashlight lit in the other hand lets the click through
        In(w, () => Waiting.NoSleep(apc.toggle_breaker()));
        w.Run(2 * Second);
        In(w, () =>
        {
            foreach (obj_machinery_light f in fixtures)
                ((obj_item_stock_parts_power_store)f.get_cell()).charge = 1;
        });
        w.Run(6 * Second);
        Assert.False(In(w, () => Dm.truthy(me.has_light_nearby())));
        texts.Clear();
        AltClick();
        w.Run(Second);
        Assert.Contains(texts, t => t.Contains(TooDark, StringComparison.Ordinal));

        w.Host.EnqueueConsoleLine("give Alice /obj/item/flashlight");
        w.Run(5);
        var flashlight = In(w, () => Dm.each<object>(me.held_items).OfType<obj_item_flashlight>().FirstOrDefault()); // the free hand takes it
        Assert.NotNull(flashlight);
        In(w, () => Waiting.NoSleep(flashlight.toggle_light(me)));
        w.Run(Second);
        Assert.True(In(w, () => Dm.truthy(me.has_light_nearby())));
        texts.Clear();
        AltClick();
        w.Run(Second);
        Assert.DoesNotContain(texts, t => t.Contains(TooDark, StringComparison.Ordinal));
        Assert.Contains(texts, t => t.Contains(Indoors, StringComparison.Ordinal));
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount); // nothing the dark ran into a runtime
    }
}
