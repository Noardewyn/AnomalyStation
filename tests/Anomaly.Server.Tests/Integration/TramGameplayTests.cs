using System.IO;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Tgui;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
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
/// The tram port (DV403) through the real server: the fixture tests/fixtures/maps/t2/tram (Tramstation's line cut out by
/// tools/fixtures/cut-tram-line.py: three platforms on one level, the car at the central one). A call button sends the tram; it carries
/// two players and a wrench, a rider walks on it while it moves, the predictor is never corrected, and what the riders draw (the own
/// capsule and the other rider) moves evenly from frame to frame.
/// </summary>
public class TramGameplayTests(ITestOutputHelper output)
{
    private static string Fixture()
    {
        for (string dir = AppContext.BaseDirectory; dir != null; dir = Path.GetDirectoryName(dir))
        {
            string candidate = Path.Combine(dir, "tests", "fixtures", "maps", "t2", "tram");
            if (Directory.Exists(candidate)) return candidate;
        }
        throw new InvalidOperationException("tests/fixtures/maps/t2/tram not found");
    }

    private static datum_transport_controller_linear_tram Tram()
        => Assert.Single(Dm.each<datum_transport_controller_linear_tram>((DmList)SStransport.transports_by_type[TRANSPORT_TYPE_TRAM]));

    [Fact]
    public void TwoPlayersRideTheTramWestAndOneWalksOnItWithNoCorrectionAndEvenDrawing()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 5, TimeoutSteps = 400 }, MapJoinTests.MapConfig("tram", Fixture()), strict: false);
        RealHeadlessWorld.Player rider = w.Join("Rider");
        RealHeadlessWorld.Player watcher = w.Join("Watcher");
        mob_living riderMob = w.MobOf(rider), watcherMob = w.MobOf(watcher);
        datum_transport_controller_linear_tram tram;
        obj_item_wrench wrench;
        obj_item_assembly_control_transport_call_button west;
        using (w.World.Enter())
        {
            tram = Tram();
            Assert.NotNull(tram.paired_cabinet);
            Assert.True(Dm.truthy(tram.controller_operational));
            Waiting.NoSleep(riderMob.forceMove(locate(64, 9, 2)));
            Waiting.NoSleep(watcherMob.forceMove(locate(69, 7, 2)));
            wrench = Dm.@new<obj_item_wrench>(locate(66, 8, 2));
            west = Dm.each<obj_item_assembly_control_transport_call_button>().First(b => Dm.eq(b.id, 1));
        }
        w.Run(40);
        long Misses(RealHeadlessWorld.Player p) => p.Counters.Corrections + p.Counters.Snaps + p.Counters.Mismatches;
        long riderBefore = Misses(rider), watcherBefore = Misses(watcher), riderSnapsBefore = rider.Counters.Snaps;
        float startX = wrench.x;

        // the frames as the rider and the watcher draw them: the rider's own capsule, the rider from the watcher's seat
        Assert.True(rider.Client.Replica.TryGet(rider.Client.OwnEntity, out ReplicaEntity riderSelf));
        Assert.True(watcher.Client.Replica.TryGet(rider.Client.OwnEntity, out ReplicaEntity riderSeen));
        Assert.True(rider.Client.Replica.TryGet(wrench.dm_id, out ReplicaEntity wrenchSeen));
        var own = new List<float>();
        var seen = new List<float>();
        var item = new List<float>();
        w.AfterFrame = () =>
        {
            if (rider.Session.Poses.TryGetPose(riderSelf, out RenderPose a))
                own.Add(a.X);
            if (watcher.Session.Poses.TryGetPose(riderSeen, out RenderPose b))
                seen.Add(b.X);
            if (rider.Session.Poses.TryGetPose(wrenchSeen, out RenderPose c))
                item.Add(c.X);
        };
        using (w.World.Enter())
            Waiting.Detach(west.activate());
        // the door chime, the doors' closing and the trip's announcement lead (DV403) come first; once it moves the rider walks east on
        // the car for half a second (against its way), then stands
        rider.Yaw = -Math.PI / 2;
        int movingTicks = 0;
        for (int i = 0; i < 400; i++)
        {
            bool moving = wrench.x != startX;
            if (moving)
                movingTicks++;
            rider.Keys = movingTicks >= 5 && movingTicks < 11 ? MoveKeys.Forward : MoveKeys.None;
            w.Run(1);
            if (moving && !Dm.truthy(tram.controller_active))
                break;
        }
        w.AfterFrame = null;
        rider.Keys = MoveKeys.None;
        w.Run(20);
        output.WriteLine($"{own.Count} frames drawn; the rider from {own[0]} to {own[^1]}");

        // even drawing: westward at most a tile a tick (a third of a tile a frame) plus the walk's start, never back east, never a jump
        AssertEven(own, "the rider's own capsule");
        AssertEven(seen, "the rider as the watcher draws it");
        AssertEven(item, "the wrench on the car as the rider draws it");
        Assert.True(own[0] - own[^1] > 50, "the rider rode the line: " + own[0] + " to " + own[^1]);

        using (w.World.Enter())
        {
            Assert.False(Dm.truthy(tram.controller_active), "the tram stopped");
            Assert.Equal(1, Dm.num(tram.idle_platform.platform_code));
            Assert.Equal(startX - 53, wrench.x); // carried the whole line
            Assert.Equal(2f, riderMob.z); // the car held them over its open space (DV404: zPassOut sees the tram on every tile it covers)
            Assert.Equal(2f, watcherMob.z);
        }
        Assert.Equal(riderBefore, Misses(rider));
        Assert.Equal(watcherBefore, Misses(watcher));

        // off the car at the west platform: along it to the door it opened there, then out through it onto the platform. tg's platform edge is a
        // noslip floor (slowdown -0.3, misc_floor.dm:143), whose speed reaches the client's parameters a tick after the server's: the small smoothed
        // corrections of the steps onto it and off it are the only ones allowed (every floor with a slowdown does that, tram or none)
        long correctionsBefore = rider.Counters.Corrections;
        rider.Yaw = Math.PI / 2; // west
        rider.Keys = MoveKeys.Forward;
        for (int i = 0; i < 80 && w.ServerState(rider).TileX > 10; i++)
            w.Run(1);
        Assert.Equal(riderBefore, Misses(rider)); // along the car: none
        rider.Yaw = Math.PI; // south
        for (int i = 0; i < 80 && w.ServerState(rider).TileY > 4; i++)
            w.Run(1);
        rider.Keys = MoveKeys.None;
        w.Run(20);
        Assert.Equal((10, 4), (w.ServerState(rider).TileX, w.ServerState(rider).TileY));
        Assert.Equal(0, rider.Counters.Snaps - riderSnapsBefore);
        Assert.True(rider.Counters.Corrections - correctionsBefore <= 2, "out through the door: " + (rider.Counters.Corrections - correctionsBefore) + " corrections");
    }

    private static void AssertEven(List<float> xs, string what)
    {
        Assert.True(xs.Count > 300, what + ": " + xs.Count + " frames");
        for (int i = 1; i < xs.Count; i++)
        {
            float d = xs[i] - xs[i - 1];
            Assert.True(d <= 0.02f && d >= -0.42f, what + " moves " + d + " tiles at frame " + i + " (" + xs[i - 1] + " to " + xs[i] + ")");
        }
    }

    private static UiNode Shown(TguiWindows windows, ReplicaWorld replica, int index)
    {
        windows.Sync(replica.UiWindows, 1280, 720, 1);
        return windows.Get(index)?.Content;
    }

    [Fact]
    public void APlayerReadsThePlateSendsTheTramFromItsConsoleAndCallsItBackWithAPlatformButton()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 7, TimeoutSteps = 400 }, MapJoinTests.MapConfig("tram", Fixture()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Rider");
        mob_living me = w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        var windows = new TguiWindows
        {
            SendAct = (window, action, parameters) => p.Session.UiAct(window, action, parameters),
            SendClose = window => p.Session.UiClose(window),
        };
        datum_transport_controller_linear_tram tram;
        obj_machinery_computer_tram_controls console;
        obj_structure_sign_tram_plate plate;
        using (w.World.Enter())
        {
            tram = Tram();
            Waiting.NoSleep(me.forceMove(locate(69, 8, 2))); // in the car, beside its plate
            console = Dm.each<obj_machinery_computer_tram_controls>().First(c => c.x == 65);
            plate = Dm.each<obj_structure_sign_tram_plate>().Single();
        }
        w.Run(20);

        // tg's information plate: the tram's serial and its history
        Assert.True(p.Session.Click(plate.dm_id, ClickButton.Left, ClickModifiers.None));
        w.Run(4);
        UiWindowState plaque = Assert.Single(replica.UiWindows.Tgui(), u => u.Interface == "TramPlaque");
        UiNode content = Shown(windows, replica, plaque.Window);
        Assert.Contains(content.Walk(), n => n.Kind == UiKind.Section && n.Title == "Tram History");
        p.Session.UiClose(plaque.Window);
        using (w.World.Enter())
            Waiting.NoSleep(me.forceMove(locate(66, 8, 2))); // beside its console
        w.Run(4);

        // the car's console: a platform's circle, then Send Tram
        Assert.True(p.Session.Click(console.dm_id, ClickButton.Left, ClickModifiers.None));
        w.Run(4);
        UiWindowState control = Assert.Single(replica.UiWindows.Tgui(), u => u.Interface == "TramControl");
        UiValue west = control.Merged.Arr("destinations").First(d => d.Num("id") == 1);
        windows = new TguiWindows { SendAct = (window, action, parameters) => p.Session.UiAct(window, action, parameters), SendClose = window => p.Session.UiClose(window) };
        content = Shown(windows, replica, control.Window);
        content.Walk().First(n => n.Key == west.Str("name")).Walk().First(n => n.Kind == UiKind.Button && n.Key == "circle").OnClick();
        content = Shown(windows, replica, control.Window);
        UiNode send = content.Walk().First(n => n.Kind == UiKind.Button && n.Text == "Send Tram");
        Assert.False(send.Disabled);
        send.OnClick();
        w.Run(4);
        using (w.World.Enter())
            Assert.True(Dm.truthy(tram.controller_active), "the console sent the tram");
        for (int i = 0; i < 400; i++)
        {
            w.Run(1);
            using (w.World.Enter())
                if (!Dm.truthy(tram.controller_active))
                    break;
        }
        using (w.World.Enter())
        {
            Assert.Equal(1, Dm.num(tram.idle_platform.platform_code));
            Assert.Equal(2f, me.z);
        }

        // a call button on the east platform calls it back: the rider walks out at the west platform's door and a caller presses the button
        RealHeadlessWorld.Player caller = w.Join("Caller");
        obj_machinery_button_transport_tram east;
        using (w.World.Enter())
        {
            east = Dm.each<obj_machinery_button_transport_tram>().First(b => b.x == 119 && b.y == 12);
            Waiting.NoSleep(w.MobOf(caller).forceMove(locate(119, 12, 2)));
        }
        w.Run(20);
        Assert.True(caller.Session.Click(east.dm_id, ClickButton.Left, ClickModifiers.None));
        w.Run(4);
        using (w.World.Enter())
            Assert.True(Dm.truthy(tram.controller_active), "the call button called the tram");
        for (int i = 0; i < 600; i++)
        {
            w.Run(1);
            using (w.World.Enter())
                if (!Dm.truthy(tram.controller_active))
                    break;
        }
        using (w.World.Enter())
            Assert.Equal(3, Dm.num(tram.idle_platform.platform_code));
    }

    [Fact]
    public void TheTramHitsAMobOnTheRailsItsCrossingsWarnAndItsDoorsOpenAtThePlatform()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 5, TimeoutSteps = 400 }, MapJoinTests.MapConfig("tram", Fixture()), strict: false);
        datum_transport_controller_linear_tram tram;
        mob_living_carbon_human walker;
        List<obj_machinery_transport_crossing_signal> crossings;
        List<obj_machinery_door_airlock_tram> doors;
        using (w.World.Enter())
        {
            tram = Tram();
            walker = Dm.@new<mob_living_carbon_human>(locate(40, 8, 2)); // on the rails, in the way west
            crossings = Dm.each<obj_machinery_transport_crossing_signal>().ToList();
            doors = Dm.each<obj_machinery_door_airlock_tram>().ToList();
            Assert.NotEmpty(crossings);
            Assert.NotEmpty(doors);
            Waiting.Detach(Dm.each<obj_item_assembly_control_transport_call_button>().First(b => Dm.eq(b.id, 1)).activate());
        }
        w.Run(20);
        var states = new HashSet<float>();
        bool closedOnTheWay = false;
        for (int i = 0; i < 400; i++)
        {
            w.Run(1);
            using (w.World.Enter())
            {
                foreach (obj_machinery_transport_crossing_signal x in crossings)
                    states.Add(x.signal_state);
                if (Dm.num(tram.travel_remaining) > 0 && doors.All(d => Dm.truthy(d.density)))
                    closedOnTheWay = true;
                if (!Dm.truthy(tram.controller_active))
                    break;
            }
        }
        w.Run(40);
        using (w.World.Enter())
        {
            Assert.Equal(1, Dm.num(tram.idle_platform.platform_code));
            Assert.True(walker.get_brute_loss() is float brute && brute > 0, "the tram hit the walker on its rails");
            Assert.NotEqual(8f, walker.y); // thrown off the line
            Assert.Contains(XING_STATE_RED, states); // a crossing the tram nears goes red
            Assert.All(crossings, x => Assert.Equal(XING_STATE_GREEN, x.signal_state)); // and clears once it stops
            Assert.True(closedOnTheWay, "the doors are shut while it moves");
            Assert.Contains(doors, d => !Dm.truthy(d.density)); // open at the platform
        }
    }
}
