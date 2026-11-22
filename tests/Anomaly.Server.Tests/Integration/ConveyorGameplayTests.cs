using System.IO;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Models;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
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
/// tg's conveyors (the owner, 2026-11-04) on MetaStation through the real server and a predicting client: a click on cargo's "Sort and
/// Deliver" switch (packageSort2, one way) throws its lever (tg's lever sound reaches the client) and runs its belts, which the client
/// draws as the running belt (tg's conveyor1, the catalog's conveyor_forward model) with the switch at switch-fwd; a wrench on the belts
/// rides them north, and the player stepping onto them rides along as tg moves him, glided without a snap of the view; a second click
/// stops them.
/// </summary>
public class ConveyorGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static ClickTarget On(atom a) => new ClickTarget(TargetKind.Entity, a.dm_id, 0, 0, 0, 16, 16);

    private static ModelCatalog Catalog()
    {
        for (DirectoryInfo dir = new(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            string candidate = Path.Combine(dir.FullName, "godot", "assets", "models", ModelCatalog.FileName);
            if (File.Exists(candidate))
                return ModelCatalog.Parse(File.ReadAllText(candidate));
        }
        throw new FileNotFoundException(ModelCatalog.FileName);
    }

    private static void Run(RealHeadlessWorld w, RealHeadlessWorld.Player p, int ticks)
        => w.Run(ticks, _ => p.Session.CameraFrame(RealHeadlessWorld.TickSeconds));

    [MetaServerFact]
    public void Cargos_switch_runs_its_belts_that_carry_an_item_and_the_player()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 317, TimeoutSteps = 40000 }, MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice", inputBufferTarget: 2);
        var me = (mob_living_carbon_human)w.MobOf(alice);
        Run(w, alice, Second);
        var heard = new List<SoundEvent>();
        alice.Session.Replica.SoundsReceived += events => heard.AddRange(events);

        // the switch, its belts (south to north), and Alice on a free tile beside the switch
        (obj_machinery_conveyor_switch sw, List<obj_machinery_conveyor> belts) = In(w, () =>
        {
            DmList members = (DmList)GLOB.conveyors_by_id["packageSort2"];
            var lever = Dm.each<obj_machinery_conveyor_switch>(members).Single();
            var row = Dm.each<obj_machinery_conveyor>(members).OrderBy(b => b.y).ToList();
            turf_open beside = Dm.each<turf>(orange(1, lever)).OfType<turf_open>()
                .First(t => !Dm.each_cast<atom>(t.contents).Any(a => Dm.truthy(a.density) || a is obj_machinery_conveyor));
            Waiting.NoSleep(me.forceMove(beside));
            return (lever, row);
        });
        Assert.True(belts.Count >= 3, "Meta's sorting belts");
        Assert.All(belts, b => Assert.Equal(NORTH, In(w, () => Dm.num(b.dir))));
        var wrench = In(w, () => Dm.@new<obj_item_wrench>(belts[0].loc));
        Run(w, alice, Second);
        int runtimes = w.World.RuntimeErrorCount;

        ReplicaWorld replica = alice.Session.Replica;
        ModelCatalog catalog = Catalog();
        AppearanceEntry Look(atom a)
        {
            ReplicaEntity seen = replica.Entities.FirstOrDefault(e => e.Id == a.dm_id);
            return seen != null && replica.TryGetAppearance(seen.State.Appearance, out AppearanceEntry look) ? look : null;
        }
        string State(atom a) => Look(a) is AppearanceEntry look ? replica.ResolveString(look.IconState) : null;
        string ModelOf(atom a) => Look(a) is AppearanceEntry look ? catalog.Find(replica.ResolveResource(look.Icon), replica.ResolveString(look.IconState), new List<string>())?.Id : null;
        Assert.Equal("conveyor0", State(belts[0]));
        Assert.Equal("conveyor_stopped", ModelOf(belts[0]));
        Assert.Equal("switch-off", State(sw));
        (int X, int Y) wrenchFrom = In(w, () => ((int)wrench.x, (int)wrench.y));
        Run(w, alice, Second);
        Assert.Equal(wrenchFrom, In(w, () => ((int)wrench.x, (int)wrench.y))); // stopped belts carry nothing

        Assert.True(alice.Session.Click(On(sw), ClickButton.Left, ClickModifiers.None));
        Run(w, alice, Second / 4);
        Assert.Equal(1f, In(w, () => Dm.num(sw.position)));
        Assert.Equal("switch-fwd", State(sw));
        Assert.All(belts, b => Assert.Equal("conveyor1", State(b)));
        Assert.Equal("conveyor_forward", ModelOf(belts[0]));
        Assert.Contains(heard, e => e.File == "sound/machines/lever/lever_start.ogg");

        // the wrench rides north; the client sees it where the server has it
        Run(w, alice, Second / 2);
        (int X, int Y) wrenchAt = In(w, () => ((int)wrench.x, (int)wrench.y));
        Assert.Equal(wrenchFrom.X, wrenchAt.X);
        Assert.True(wrenchAt.Y > wrenchFrom.Y, $"the wrench rode north from {wrenchFrom} (now {wrenchAt})");
        Run(w, alice, 2);
        ReplicaEntity seenWrench = replica.Entities.First(e => e.Id == wrench.dm_id);
        (int X, int Y) seenAt = In(w, () => ((int)wrench.x, (int)wrench.y));
        Assert.Equal(seenAt, (seenWrench.State.X, seenWrench.State.Y));

        // Alice steps onto the first belt and rides it as tg moves her: glided, no snap of the view
        PredictionCounters before = alice.Counters;
        In(w, () => Waiting.NoSleep(me.forceMove(belts[0].loc)));
        Run(w, alice, Second / 2);
        PredictionCounters after = alice.Counters;
        output.WriteLine($"belts {belts.Count} from y {In(w, () => belts[0].y)}; Alice at y {In(w, () => me.y)}; corrections {after.Corrections - before.Corrections}, snaps {after.Snaps - before.Snaps}");
        Assert.True(In(w, () => me.y) > In(w, () => belts[0].y), "Alice rode north");
        Assert.Equal(before.Snaps, after.Snaps);

        // a second click stops them
        In(w, () => Waiting.NoSleep(me.forceMove(sw.loc)));
        Run(w, alice, Second / 2);
        Assert.True(alice.Session.Click(On(sw), ClickButton.Left, ClickModifiers.None));
        Run(w, alice, Second / 4);
        Assert.Equal(0f, In(w, () => Dm.num(sw.position)));
        Assert.All(belts, b => Assert.Equal("conveyor0", State(b)));
        Assert.Contains(heard, e => e.File == "sound/machines/lever/lever_stop.ogg");
        Assert.Equal(runtimes, w.World.RuntimeErrorCount);
    }

    // A free powered floor row of five tiles near (x, y) on z with a free row south of it and a free tile west of it: its first tile.
    private static (int X, int Y) FreeRow(RealHeadlessWorld w, int z, int nearX, int nearY) => In(w, () =>
    {
        bool Free(int x, int y) => locate(x, y, z) is turf_open_floor t && !Dm.each_cast<atom>(t.contents).Any(a => Dm.truthy(a.density) || a is obj_machinery || a is obj_structure)
            && Dm.truthy(((area)t.loc).powered(AREA_USAGE_EQUIP)) && Dm.truthy(t.has_gravity());
        for (int r = 0; r < 40; r++)
            for (int y = nearY - r; y <= nearY + r; y++)
                for (int x = nearX - r; x <= nearX + r; x++)
                    if (Free(x - 1, y) && Enumerable.Range(0, 5).All(i => Free(x + i, y) && Free(x + i, y - 1)))
                        return (x, y);
        return (0, 0);
    });

    // Alice walks onto a running belt from its end, rides it with her keys up and walks back against it, 100 ms each way: her own view is
    // predicted throughout (the belts around ride in her motion parameters, DV347), no correction at all.
    [MetaServerFact]
    public void Riding_and_walking_on_a_running_belt_is_predicted_without_a_correction()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 347, TimeoutSteps = 40000, MinLatencySteps = 2, MaxLatencySteps = 2 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice", inputBufferTarget: 2);
        var me = (mob_living_carbon_human)w.MobOf(alice);
        Run(w, alice, Second);
        int z = In(w, () => (int)me.z);
        (int x0, int y) = FreeRow(w, z, In(w, () => (int)me.x), In(w, () => (int)me.y));
        Assert.NotEqual(0, x0);
        // five belts running east from x0, on; Alice west of the first
        List<obj_machinery_conveyor> belts = In(w, () =>
        {
            var row = Enumerable.Range(0, 5).Select(i => Dm.@new<obj_machinery_conveyor>(locate(x0 + i, y, z), (float)EAST, "prediction")).ToList();
            var lever = Dm.@new<obj_machinery_conveyor_switch>(locate(x0, y - 1, z), "prediction");
            Waiting.NoSleep(lever.on_user_activation(me, 1)); // forward
            Waiting.NoSleep(me.forceMove(locate(x0 - 1, y, z)));
            return row;
        });
        Run(w, alice, 2 * Second);
        PredictionCounters before = alice.Counters;
        int runtimes = w.World.RuntimeErrorCount;

        alice.Yaw = -Math.PI / 2; // east, onto the belt
        alice.Keys = MoveKeys.Forward;
        for (int i = 0; i < 2 * Second && !In(w, () => belts.Any(b => ReferenceEquals(b.loc, me.loc))); i++)
            Run(w, alice, 1);
        alice.Keys = MoveKeys.None;
        Assert.True(In(w, () => belts.Any(b => ReferenceEquals(b.loc, me.loc))), "Alice walked onto the belt");
        int onX = In(w, () => (int)me.x);
        Run(w, alice, Second / 2);
        int rodeTo = In(w, () => (int)me.x);
        alice.Yaw = Math.PI / 2; // west, against the belt
        alice.Keys = MoveKeys.Forward;
        Run(w, alice, Second / 2);
        alice.Keys = MoveKeys.None;
        Run(w, alice, 2 * Second);
        PredictionCounters after = alice.Counters;
        output.WriteLine($"on the belt at x {onX}, rode to x {rodeTo}, now ({In(w, () => me.x)}, {In(w, () => me.y)}); corrections {after.Corrections - before.Corrections}, snaps {after.Snaps - before.Snaps}, mismatches {after.Mismatches - before.Mismatches}, max error {after.MaxErrorTiles:0.000}");
        Assert.True(rodeTo > onX, "the belt carried her east");
        Assert.Equal((x0 + 5, y), In(w, () => ((int)me.x, (int)me.y))); // off the last belt, where tg's loop leaves what it carries
        Assert.Equal(before.Corrections, after.Corrections);
        Assert.Equal(before.Snaps, after.Snaps);
        Assert.Equal(runtimes, w.World.RuntimeErrorCount);
    }

    // At the speed scale of the shipped config a belt keeps tg's ratio to a runner (the owner, 2026-11-05: it outran the player and held them
    // against a wall for good): a belt ending in a grille pins Alice there, and running against it takes her back off its far end, predicted.
    [MetaServerFact]
    public void At_the_configs_speed_scale_a_runner_beats_the_belt_that_pins_them()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 348, TimeoutSteps = 40000, MinLatencySteps = 2, MaxLatencySteps = 2 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice", inputBufferTarget: 2);
        var me = (mob_living_carbon_human)w.MobOf(alice);
        Run(w, alice, Second);
        int z = In(w, () => (int)me.z);
        (int x0, int y) = FreeRow(w, z, In(w, () => (int)me.x), In(w, () => (int)me.y));
        Assert.NotEqual(0, x0);
        In(w, () =>
        {
            LocomotionSeam.SetSpeedScale(w.World, 0.5f);
            foreach (int i in Enumerable.Range(0, 4))
                Dm.@new<obj_machinery_conveyor>(locate(x0 + i, y, z), (float)EAST, "pinning");
            Dm.@new<obj_structure_grille>(locate(x0 + 4, y, z));
            var lever = Dm.@new<obj_machinery_conveyor_switch>(locate(x0, y - 1, z), "pinning");
            Waiting.NoSleep(lever.on_user_activation(me, 1)); // forward
            Waiting.NoSleep(me.forceMove(locate(x0 + 1, y, z)));
            return 0;
        });
        Run(w, alice, 2 * Second);
        Assert.Equal(x0 + 3, In(w, () => (int)me.x)); // pinned against the grille
        PredictionCounters before = alice.Counters;
        int runtimes = w.World.RuntimeErrorCount;

        alice.Yaw = Math.PI / 2; // west, against the belt
        alice.Keys = MoveKeys.Forward;
        for (int i = 0; i < 8 * Second && In(w, () => (int)me.x) >= x0; i++)
            Run(w, alice, 1);
        alice.Keys = MoveKeys.None;
        Run(w, alice, Second);
        PredictionCounters after = alice.Counters;
        output.WriteLine($"now at x {In(w, () => me.x)} (belts {x0} to {x0 + 3}); corrections {after.Corrections - before.Corrections}, snaps {after.Snaps - before.Snaps}, mismatches {after.Mismatches - before.Mismatches}");
        Assert.True(In(w, () => (int)me.x) < x0, "Alice ran off the belt's far end");
        Assert.Equal(before.Corrections, after.Corrections);
        Assert.Equal(before.Snaps, after.Snaps);
        Assert.Equal(runtimes, w.World.RuntimeErrorCount);
    }
}
