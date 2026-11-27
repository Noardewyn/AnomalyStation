using System.Diagnostics;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.World;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// M5x slice 1 through the real server on MetaStation, with a headless Godot client (<see cref="PlaySession"/>): a player's dev
/// <c>explode 41 155 1 1 2 4</c> (tg's admin Drop Bomb with a custom bomb) in the space by the Aux base construction zone's west
/// wall (x = 42, y 153..157; the zone x 43..51, y 151..159 holds the aux base, which tg loads there at round start: M10 slice G3).
/// SSexplosions' next fire dismantles the wall tiles in the heavy range (girders, iron sheets), damages and throws the
/// items in the light range away from the epicenter, and the zone vents through the hole; the player in the room south of the
/// zone hears the near explosion sound; the client's turfs change with the server's and it sees the debris. A second fact
/// measures a (3, 7, 14) blast on the bridge: the propagation, the server tick that processes it, the ticks after and the
/// client's chunks to rebuild (the numbers go to the test output).
/// </summary>
public class ExplosionGameplayTests
{
    private const int Second = 20; // ticks per second
    private const int EpiX = 41, EpiY = 155; // the space by the zone's west wall (the zone holds the aux base: M10 slice G3)
    private const int LightX = 43, LightY = 152; // the light blast's epicenter: the aux base's floor, away from its fittings
    private const int WallX = 42;            // the zone's west wall (y 153..157), space beyond
    private const int ZoneX = 48, ZoneY = 155; // the middle of the zone
    private const int RoomX = 48, RoomY = 148; // the room south of the zone, through the zone's airlock (47,150)

    private readonly ITestOutputHelper _output;

    public ExplosionGameplayTests(ITestOutputHelper output) => _output = output;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static turf At(int x, int y) => Builtins.locate(x, y, 1);

    private static float Kpa(RealHeadlessWorld w, int x, int y)
        => In(w, () => At(x, y) is turf_open t && t.air != null ? Dm.num(t.air.return_pressure()) : -1f);

    private static string TypeOf(RealHeadlessWorld w, int x, int y) => In(w, () => DmTypes.PathOf(At(x, y).GetType()));

    [MetaServerFact]
    public void A_dev_explosion_by_the_Aux_base_west_wall_breaches_it_and_the_zone_vents()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 19, TimeoutSteps = 8000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Assistant");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        var heard = new List<SoundEvent>();
        replica.SoundsReceived += events => heard.AddRange(events);
        var texts = new List<string>();
        p.Client.ServerTextReceived += t => texts.Add(t.Html);
        var changed = new HashSet<(int, int, int)>();
        replica.CellsChanged += cells => { foreach (var c in cells) changed.Add(c); };

        // The place as the plan read it from the map.
        for (int y = 153; y <= 157; y++)
            Assert.Equal("/turf/closed/wall", TypeOf(w, WallX, y));
        Assert.StartsWith("/turf/open/space", TypeOf(w, WallX - 1, EpiY));

        // Two tools on the zone's plating in the light range (dist 2 and 3), the player in the room south of the zone.
        obj_item crowbar = null, wrench = null;
        using (w.World.Enter())
        {
            Waiting.NoSleep(me.forceMove(At(RoomX, RoomY)));
            crowbar = Dm.@new<obj_item_crowbar>(At(EpiX + 2, EpiY));
            wrench = Dm.@new<obj_item_wrench>(At(EpiX + 3, EpiY));
        }
        w.Run(2 * Second);
        float before = Kpa(w, ZoneX, ZoneY);
        Assert.InRange(before, 90f, 110f);
        heard.Clear();

        p.Session.DevCommand($"explode {EpiX} {EpiY} 1 1 2 4");
        w.Run(2); // the command is applied, then SSexplosions fires
        string reply = texts.LastOrDefault(t => t.Contains("explode", StringComparison.Ordinal));
        _output.WriteLine("reply: " + reply);
        Assert.NotNull(reply);
        Assert.Contains("queued", reply, StringComparison.Ordinal);
        Assert.False(In(w, () => Dm.truthy(SSexplosions.is_exploding())), "one fire processed the blast");

        // The epicenter (dev 1) stays space; the wall tiles in the heavy range (dist 1, 1.41) are dismantled.
        string epicenter = TypeOf(w, EpiX, EpiY);
        string wall = TypeOf(w, WallX, EpiY);
        _output.WriteLine($"after: epicenter {epicenter}, wall {wall}, {TypeOf(w, WallX, EpiY - 1)}, {TypeOf(w, WallX, EpiY + 1)}, {TypeOf(w, WallX, EpiY - 2)}, {TypeOf(w, WallX, EpiY + 2)}");
        Assert.StartsWith("/turf/open/space", epicenter);
        for (int y = EpiY - 1; y <= EpiY + 1; y++)
            Assert.False(TypeOf(w, WallX, y).StartsWith("/turf/closed", StringComparison.Ordinal), $"the wall at ({WallX},{y}) is down");

        // The client's turfs are the server's, and the changed cells reached it.
        w.Run(2);
        for (int y = EpiY - 2; y <= EpiY + 2; y++)
        {
            for (int x = WallX; x <= EpiX + 1; x++)
                Assert.Equal(TypeOf(w, x, y), replica.TurfTypeAt(x, y, 1));
        }
        Assert.Contains((WallX, EpiY, 1), changed);

        // The debris: the dismantled walls' sheets and girders (or what is left of them after the blast's own throws and the
        // wind), each an entity the client sees.
        var debris = In(w, () => Enumerable.Range(WallX - 6, 14).SelectMany(x => Enumerable.Range(EpiY - 4, 9).SelectMany(y => Dm.each_cast<atom>(At(x, y).contents)))
            .Where(a => a is obj_item_stack_sheet_iron || a is obj_structure_girder).Select(a => a.dm_id).ToList());
        _output.WriteLine($"debris near the hole: {debris.Count}");
        Assert.NotEmpty(debris);
        Assert.All(debris, id => Assert.Contains(replica.Entities, e => e.Id == id));

        // The player in the room (7 tiles off, near_distance 4 + view 7 - 2 = 9) heard the near explosion.
        Assert.Contains(heard, e => e.Kind == SoundKind.Play && e.File != null && e.File.StartsWith("sound/effects/explosion/explosion", StringComparison.Ordinal));

        // The tools in the light range: damaged or destroyed, and the survivors thrown east, away from the epicenter.
        _output.WriteLine($"crowbar: {(In(w, () => QDELETED(crowbar)) ? "destroyed" : In(w, () => $"({crowbar.x},{crowbar.y}) {Dm.num(crowbar.get_integrity())}"))}, wrench: {(In(w, () => QDELETED(wrench)) ? "destroyed" : In(w, () => $"({wrench.x},{wrench.y}) {Dm.num(wrench.get_integrity())}"))}");

        // The zone vents into space through the hole.
        float lowest = before;
        for (int t = 0; t < 30; t++)
        {
            w.Run(Second);
            lowest = Math.Min(lowest, Kpa(w, ZoneX, ZoneY));
        }
        _output.WriteLine($"zone pressure: {before:0.0} kPa before, {lowest:0.0} kPa 30 s after");
        Assert.True(lowest < before * 0.8f, $"the zone vents: {lowest} of {before}");
    }

    [MetaServerFact]
    public void A_3_7_14_blast_on_the_bridge_is_measured_on_the_server_tick_and_the_clients_chunks()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 23, TimeoutSteps = 8000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Assistant");
        ReplicaWorld replica = p.Session.Replica;
        var changed = new HashSet<(int, int, int)>();
        replica.CellsChanged += cells => { foreach (var c in cells) changed.Add(c); };
        using (w.World.Enter())
            Waiting.NoSleep(w.MobOf(p).forceMove(At(100, 134))); // 13 tiles west of the epicenter: outside the light range
        w.Run(2 * Second);

        Dictionary<string, int> Census() => In(w, () => Enumerable.Range(99, 29).SelectMany(x => Enumerable.Range(120, 29).Select(y => At(x, y)))
            .GroupBy(t => DmTypes.PathOf(t.GetType()) + (t is turf_open o && Dm.truthy(o.broken) ? " (broken)" : "")).ToDictionary(g => g.Key, g => g.Count()));
        var censusBefore = Census();
        int movablesBefore = In(w, () => Enumerable.Range(99, 29).SelectMany(x => Enumerable.Range(120, 29).SelectMany(y => Dm.each_cast<atom>(At(x, y).contents))).Count());
        var totalsBefore = w.Host.Profiler.Totals().ToDictionary(t => t.Name, t => t.TotalMs);
        long entitiesBefore = replica.Entities.Count();
        var sw = Stopwatch.StartNew();
        string reply;
        using (w.World.Enter())
            reply = DevCommands.Explode(At(113, 134), 3, 7, 14);
        double propagateMs = sw.Elapsed.TotalMilliseconds;
        _output.WriteLine(reply);

        sw.Restart();
        w.Run(1); // SSexplosions' fire: the ex_acts, the movables, the throws; then the Delta and the client's apply
        double fireTickMs = sw.Elapsed.TotalMilliseconds;
        var pieces = w.Host.Profiler.Totals().Select(t => (t.Name, Ms: t.TotalMs - (totalsBefore.TryGetValue(t.Name, out double b) ? b : 0)))
            .Where(t => t.Ms > 0.5).OrderByDescending(t => t.Ms).Take(8).ToList();
        int cellsAfterFire = changed.Count;
        var chunks = changed.Select(c => (ChunkPlanner.ChunkIndex(c.Item1), ChunkPlanner.ChunkIndex(c.Item2), c.Item3)).Distinct().ToList();

        // The client's part of a chunk rebuild that is engine-free (MapRenderer.RebuildTurfs reads the replica's 16 x 16 turfs
        // of each dirty chunk into tile cells before the MultiMeshes are made; the meshes themselves are Godot's, not here).
        sw.Restart();
        int tiles = 0;
        foreach ((int cx, int cy, int cz) in chunks)
        {
            for (int y = 1 + cy * ChunkPlanner.ChunkSize; y < 1 + (cy + 1) * ChunkPlanner.ChunkSize; y++)
            {
                for (int x = 1 + cx * ChunkPlanner.ChunkSize; x < 1 + (cx + 1) * ChunkPlanner.ChunkSize; x++)
                {
                    if (replica.TurfTypeAt(x, y, cz) != null && !replica.TurfAppearance(x, y, cz).IsNone)
                        tiles++;
                }
            }
        }
        double planMs = sw.Elapsed.TotalMilliseconds;

        double worst = 0;
        int lastArrival = 0, lastBehind = 0;
        for (int t = 1; t <= 10 * Second; t++)
        {
            int had = changed.Count;
            sw.Restart();
            w.Run(1);
            worst = Math.Max(worst, sw.Elapsed.TotalMilliseconds);
            if (changed.Count > had)
                lastArrival = t;
            // the client behind the server: a turf type of the square that the replica does not have yet after this tick's Delta
            if (In(w, () => Enumerable.Range(99, 29).SelectMany(x => Enumerable.Range(120, 29).Select(y => (x, y))).Any(c => DmTypes.PathOf(At(c.x, c.y).GetType()) != replica.TurfTypeAt(c.x, c.y, 1))))
                lastBehind = t;
        }
        // M5x slice 2: the later cell changes are the server's own (fires, the walls' smoothing), not a replication lag
        _output.WriteLine($"the client's turf types last differed from the server's {lastBehind} ticks after the fire tick");
        Assert.Equal(0, lastBehind);
        // the blast's square only: about 1000 cells far south of it (y 16-38, x 128-239) change in the same seconds
        var allChunks = changed.Where(c => c.Item1 is >= 99 and <= 127 && c.Item2 is >= 120 and <= 148).Select(c => (ChunkPlanner.ChunkIndex(c.Item1), ChunkPlanner.ChunkIndex(c.Item2), c.Item3)).Distinct().Count();
        _output.WriteLine($"(3, 7, 14) at the bridge (113,134): propagation {propagateMs:0.0} ms; the fire tick {fireTickMs:0.0} ms (server + client); " +
            $"its pieces: {string.Join(", ", pieces.Select(x => $"{x.Name} {x.Ms:0.0}"))}");
        _output.WriteLine($"client: {cellsAfterFire} cells changed in the fire tick's Delta, {changed.Count} in all in {allChunks} chunks, the last {lastArrival} ticks after; " +
            $"({tiles} turfs of the first chunks read in {planMs:0.00} ms); entities {entitiesBefore} -> {replica.Entities.Count()}; the worst of the next 10 s of ticks {worst:0.0} ms");
        var censusAfter = Census();
        int movablesAfter = In(w, () => Enumerable.Range(99, 29).SelectMany(x => Enumerable.Range(120, 29).SelectMany(y => Dm.each_cast<atom>(At(x, y).contents))).Count());
        _output.WriteLine("turfs before: " + string.Join(", ", censusBefore.OrderByDescending(k => k.Value).Select(k => $"{k.Key} {k.Value}")));
        _output.WriteLine("turfs after: " + string.Join(", ", censusAfter.OrderByDescending(k => k.Value).Select(k => $"{k.Key} {k.Value}")));
        _output.WriteLine($"movables in the 29 x 29 square: {movablesBefore} -> {movablesAfter}");
        int mismatches = In(w, () => Enumerable.Range(99, 29).SelectMany(x => Enumerable.Range(120, 29).Select(y => (x, y)))
            .Count(c => DmTypes.PathOf(At(c.x, c.y).GetType()) != replica.TurfTypeAt(c.x, c.y, 1)));
        _output.WriteLine($"client after 10 s: {mismatches} turf types differ from the server's");

        // The same blast again elsewhere (the Recreation Area), the code warm: the steady cost.
        totalsBefore = w.Host.Profiler.Totals().ToDictionary(t => t.Name, t => t.TotalMs);
        sw.Restart();
        using (w.World.Enter())
            reply = DevCommands.Explode(At(146, 178), 3, 7, 14);
        double propagate2 = sw.Elapsed.TotalMilliseconds;
        sw.Restart();
        w.Run(1);
        double fire2 = sw.Elapsed.TotalMilliseconds;
        var pieces2 = w.Host.Profiler.Totals().Select(t => (t.Name, Ms: t.TotalMs - (totalsBefore.TryGetValue(t.Name, out double b) ? b : 0)))
            .Where(t => t.Ms > 0.5).OrderByDescending(t => t.Ms).Take(6).ToList();
        _output.WriteLine($"again at (146,178), warm: {reply}; propagation {propagate2:0.0} ms, the fire tick {fire2:0.0} ms: {string.Join(", ", pieces2.Select(x => $"{x.Name} {x.Ms:0.0}"))}");

        Assert.Equal(0, mismatches);
        Assert.True(changed.Count > 100, "a (3, 7, 14) blast changes many turfs: " + changed.Count);
        Assert.InRange(allChunks, 1, 16);
    }

    /// <summary>
    /// M5x slice 3: a person in a blast. A light <c>explode 43 152 1 0 0 3 5 0</c> (flash range 5) on the aux base's floor in the
    /// construction zone (M10 slice G3: tg loads the base there at round start), away from its fittings, with a second player two tiles east: the victim takes the light blast's brute, is knocked down and deafened, its
    /// client gets the fullscreen flash (cleared 2.5 s later, faded out over 2.5 s more) and, deaf, hears nothing of a sound played beside it afterwards;
    /// the first player in the room south of the zone hears both.
    /// </summary>
    [MetaServerFact]
    public void A_light_blast_beside_a_second_player_hurts_knocks_down_flashes_and_deafens_them()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 29, TimeoutSteps = 8000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder(), seed: 202), strict: false);
        RealHeadlessWorld.Player engineer = w.Join("Engineer");
        RealHeadlessWorld.Player victimPlayer = w.Join("Victim");
        var victim = (mob_living_carbon_human)w.MobOf(victimPlayer);
        var heardByEngineer = new List<SoundEvent>();
        var heardByVictim = new List<SoundEvent>();
        engineer.Session.Replica.SoundsReceived += events => heardByEngineer.AddRange(events);
        victimPlayer.Session.Replica.SoundsReceived += events => heardByVictim.AddRange(events);
        using (w.World.Enter())
        {
            Waiting.NoSleep(w.MobOf(engineer).forceMove(At(RoomX, RoomY)));
            Waiting.NoSleep(victim.forceMove(At(LightX + 2, LightY)));
        }
        w.Run(2 * Second);
        Assert.Equal(0f, In(w, () => Dm.num(victim.get_brute_loss())));

        var texts = new List<string>();
        engineer.Client.ServerTextReceived += t => texts.Add(t.Html);
        engineer.Session.DevCommand($"explode {LightX} {LightY} 1 0 0 3 5 0"); // Z 1, light 3, flash 5, no flame
        for (int i = 0; i < 20 && texts.All(t => t.Contains("Payday", StringComparison.Ordinal)); i++) // a payday line may come first; the input buffer: the command is applied a few ticks later
            w.Run(1);
        w.Run(1); // SSexplosions fires
        _output.WriteLine("reply: " + string.Join(" | ", texts));
        float brute = In(w, () => Dm.num(victim.get_brute_loss()));
        float ears = In(w, () => Dm.num(((obj_item_organ_ears)victim.get_organ_slot(Defines.ORGAN_SLOT_EARS)).damage));
        _output.WriteLine($"victim: brute {brute}, knocked down {In(w, () => Dm.truthy(victim.IsKnockdown()))}, ears {ears}, deaf {In(w, () => HAS_TRAIT(victim, Defines.TRAIT_DEAF))}, at ({victim.x},{victim.y})");
        Assert.InRange(brute, 5f, 30f); // 5 to a limb, the loop ending at the second wound roll at the earliest
        Assert.True(In(w, () => Dm.truthy(victim.IsKnockdown())), "16 s of knockdown");
        Assert.True(ears >= 15, "15 ear damage");
        Assert.True(In(w, () => HAS_TRAIT(victim, Defines.TRAIT_DEAF)), "deafened for 120 s");

        // Version 21: tg's shake_camera of the blast reached both clients as a view path.
        w.Run(1);
        _output.WriteLine($"camera shakes: victim {victimPlayer.Session.Replica.CameraShakes}, engineer {engineer.Session.Replica.CameraShakes}");
        Assert.True(victimPlayer.Session.Replica.CameraShakes > 0, "the victim's view shook");
        Assert.True(engineer.Session.Replica.CameraShakes > 0, "the engineer's view shook");

        // The flash reached the victim's client as a screen object, and it is cleared after 2.5 s.
        atom_movable_screen_fullscreen flash = In(w, () => victim.screens?["flash"] as atom_movable_screen_fullscreen);
        Assert.NotNull(flash);
        w.Run(2);
        Assert.Contains(victimPlayer.Session.Replica.Screen.Values, e => e.Id == flash.dm_id);
        // flash_act's timer calls clear_fullscreen("flash", length): the 2.5 s flash, then a fade of the same 2.5 s
        w.Run(3 * Second);
        Assert.Null(In(w, () => victim.screens?["flash"]));
        Assert.Contains(victimPlayer.Session.Replica.Screen.Values, e => e.Id == flash.dm_id);
        w.Run(3 * Second);
        Assert.DoesNotContain(victimPlayer.Session.Replica.Screen.Values, e => e.Id == flash.dm_id);

        // Deaf: a sound beside the victim reaches the engineer's client (in hearing range) but not the victim's.
        heardByEngineer.Clear();
        heardByVictim.Clear();
        using (w.World.Enter())
            playsound(At(LightX + 3, LightY), Dm.resource("sound/items/tools/ratchet.ogg"), 100, Dm.FALSE);
        w.Run(2);
        Assert.DoesNotContain(heardByVictim, e => e.File == "sound/items/tools/ratchet.ogg");
        Assert.Contains(heardByEngineer, e => e.File == "sound/items/tools/ratchet.ogg");
    }
}
