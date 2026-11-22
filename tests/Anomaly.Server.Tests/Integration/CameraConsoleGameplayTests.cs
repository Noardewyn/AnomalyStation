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
/// M10 slice D (D23, D268; multiplayer.md section 7) through the real server on MetaStation: Alice clicks a security camera console and
/// gets tg's CameraConsole window with the station's cameras (SScameras, sorted by c_tag); she picks one across the station and the
/// window names it and draws its map (the ByondUi from the camera's REF); the camera's view becomes her second eye, so Bob standing in
/// front of that camera, far from her and behind the walls, reaches her client; closing the window takes him away after the hold.
/// </summary>
public class CameraConsoleGameplayTests(ITestOutputHelper output)
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

    private static bool Clear(turf t) => t is turf_open_floor && !Dm.truthy(t.density) && !Dm.each_cast<atom>(t.contents).Any(a => Dm.truthy(a.density));

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

    private static UiNode Shown(TguiWindows windows, ReplicaWorld replica, int index)
    {
        windows.Sync(replica.UiWindows, 1280, 720, 1);
        return windows.Get(index)?.Content;
    }

    [Fact]
    public void The_security_camera_console_shows_a_camera_across_the_station_and_its_view_reaches_the_client()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 105, TimeoutSteps = 40000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        mob aliceMob = w.MobOf(alice), bobMob = w.MobOf(bob);
        ReplicaWorld sees = alice.Session.Replica;
        var windows = new TguiWindows { SendAct = (window, action, parameters) => alice.Session.UiAct(window, action, parameters), SendClose = window => alice.Session.UiClose(window) };
        w.Run(Second);
        int bootRuntimes = w.World.RuntimeErrorCount;

        // tg's cameras registered in SScameras; about 3% start off (camera.dm:130)
        int cameras = (int)In(w, () => SScameras.cameras.len);
        int off = In(w, () => Dm.each<obj_machinery_camera>(SScameras.cameras).Count(c => !Dm.truthy(c.camera_enabled)));
        output.WriteLine($"{cameras} cameras, {off} off at the round start");
        Assert.True(cameras > 250, "Meta's cameras: " + cameras);
        Assert.InRange(off, 0, cameras / 10);

        // Alice at a security camera console
        obj_machinery_computer_security console = In(w, () => Dm.each<obj_machinery_computer_security>(world.contents)
            .First(c => c.GetType() == typeof(obj_machinery_computer_security) && Beside(c) != null));
        Do(w, () => Waiting.NoSleep(aliceMob.forceMove(Beside(console))));
        w.Run(Second);
        Assert.True(alice.Session.Click(new ClickTarget(TargetKind.Entity, console.dm_id, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        UiWindowState state = Assert.Single(sees.UiWindows.Tgui());
        Assert.Equal("CameraConsole", state.Interface);
        UiNode tree = Shown(windows, sees, state.Window);
        List<UiNode> buttons = tree.Walk().Where(n => n.Key != null && n.Key.StartsWith("camera:", StringComparison.Ordinal)).ToList();
        output.WriteLine($"the window lists {buttons.Count} cameras, first {buttons[0].Text}");
        Assert.True(buttons.Count > 200);
        Assert.Contains(tree.Walk(), n => n.Kind == UiKind.ByondUi && n.MapFrom == null && n.MapRef.StartsWith("camera_console_", StringComparison.Ordinal));
        Assert.Contains(tree.Walk(), n => n.Kind == UiKind.NoticeBox && n.Text == "No input signal");

        // A working camera at least 40 tiles away with a clear floor in front of it, where Bob stands
        turf start = In(w, () => aliceMob.loc as turf);
        (obj_machinery_camera camera, turf spot) = In(w, () => Dm.each<obj_machinery_camera>(SScameras.cameras)
            .Where(c => Dm.truthy(c.can_use()) && Dm.truthy(c.c_tag) && c.loc is turf)
            .Select(c => (c, get_step(c, Dm.num(c.dir)) is turf wall ? get_step(c, Dm.num(turn(c.dir, 180))) as turf : null))
            .First(p => p.Item2 != null && Clear(p.Item2) && Math.Max(Math.Abs(p.c.x - start.x), Math.Abs(p.c.y - start.y)) >= 40));
        Do(w, () => Waiting.NoSleep(bobMob.forceMove(spot)));
        w.Run(3 * Second);
        Assert.False(sees.TryGet(bobMob.dm_id, out _), "Bob across the station is not Alice's before she watches him");

        string cameraRef = In(w, () => Dm.str(REF(camera)));
        UiNode pick = buttons.First(b => b.Key == "camera:" + cameraRef);
        output.WriteLine($"Alice watches {pick.Text} at {camera.x},{camera.y}; Bob at {spot.x},{spot.y}");
        pick.OnClick();
        w.Run(2 * Second);
        Assert.Same(camera, In(w, () => console.active_camera));
        Assert.True(In(w, () => console.cam_screen.vis_contents.len) > 5, "the camera's view turfs are in the map view");
        tree = Shown(windows, sees, state.Window);
        Assert.Contains(tree.Walk(), n => n.Kind == UiKind.NoticeBox && n.Text == In(w, () => Dm.str(camera.c_tag)));
        Assert.Contains(tree.Walk(), n => n.Kind == UiKind.ByondUi && n.MapFrom == cameraRef);
        Assert.Equal(camera.dm_id, CameraViewPose.IdOfRef(cameraRef));
        Assert.True(sees.TryGet(camera.dm_id, out _), "the camera itself, drawn from");
        Assert.True(sees.TryGet(bobMob.dm_id, out _), "Bob in front of the camera reaches Alice through it");

        // closing the window: the second eye goes, and Bob with it after the hold
        alice.Session.UiClose(state.Window);
        w.Run(4 * Second);
        Assert.Empty(sees.UiWindows.Tgui());
        Assert.Null(In(w, () => console.active_camera));
        Assert.False(sees.TryGet(bobMob.dm_id, out _), "Bob leaves with the camera's view");
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
