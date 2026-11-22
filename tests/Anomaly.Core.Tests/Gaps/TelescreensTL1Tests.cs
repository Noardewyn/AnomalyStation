using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tests.Gaps.GapsHarness;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package TL1 (D333): the telescreens in a booted toy room with players. A wall telescreen opens the camera console window
/// on its network and shows its broken picture; the broadcast camera goes live in Bob's hands, so the entertainment monitor
/// lights its screen, announces the show and lets a viewer across the room watch the live camera, its speakers mute on a
/// ctrl-click, and dropping the camera ends the show; the showtime button starts and ends its show on every monitor; the
/// round's first curator packs the broadcast camera, the second does not.
/// </summary>
public class TelescreensTL1Tests
{
    private static string Diag(Feed feed, AudioRoom r) => "chat: " + string.Join(" | ", feed.Chat.Select(c => c.Html)) + " runtimes: " + string.Join("\n", r.Room.RuntimeText);

    private static List<string> OverlayStates(AudioRoom r, atom thing) =>
        r.In(() => Dm.each(thing.overlays).Select(o => o is IDmAppearance x ? x.icon_state : Dm.str(o)).ToList());

    [Fact]
    public void A_wall_telescreen_shows_its_networks_cameras_and_its_broken_picture()
    {
        obj_machinery_computer_security_telescreen_normal screen = null;
        var r = new AudioRoom(13, 9, seed: 9951, mapExtras: w =>
        {
            screen = Dm.@new<obj_machinery_computer_security_telescreen_normal_directional_south>(locate(5, 2, 1));
            var camera = Dm.@new<obj_machinery_camera>(locate(10, 6, 1));
            camera.c_tag = "Test Hall";
        });
        var bob = r.Join(5, 3, "Bob");
        r.Step(Second);
        Assert.Equal("telescreen", r.In(() => Dm.str(screen.icon_state)));
        Assert.True(r.In(() => Dm.@in(CAMERANET_NETWORK_SS13, screen.network)));

        Click(r, bob, screen);
        var outputs = Take(bob);
        var payload = LastUpdate(outputs, out _);
        Assert.Equal("CameraConsole", InterfaceOf(payload));
        var cameras = payload.GetProperty("static_data").GetProperty("cameras");
        Assert.Contains(cameras.EnumerateArray(), c => c.GetProperty("name").GetString() == "Test Hall");

        // tg's computer breaks only with a circuit (_computer.dm:113): a telescreen has none, so a hit leaves it whole
        r.In(() => Waiting.NoSleep(screen.atom_break()));
        r.Step(2);
        Assert.Equal("telescreen", r.In(() => Dm.str(screen.icon_state)));
        // broken by its stat, it shows the broken picture
        r.In(() =>
        {
            Waiting.NoSleep(screen.set_machine_stat(Dm.bitor(screen.machine_stat, BROKEN)));
            screen.update_appearance();
        });
        r.Step(2);
        Assert.Equal("telescreenb", r.In(() => Dm.str(screen.icon_state)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_broadcast_camera_puts_its_live_feed_on_the_entertainment_monitor_until_it_is_dropped()
    {
        obj_machinery_computer_security_telescreen_entertainment tv = null;
        var r = new AudioRoom(13, 9, seed: 9952, mapExtras: w =>
        {
            tv = Dm.@new<obj_machinery_computer_security_telescreen_entertainment_directional_south>(locate(6, 2, 1));
        });
        Feed feed = Listen(r);
        var bob = r.Join(10, 6, "Bob");
        var viewer = r.Join(6, 6, "Viewer"); // four tiles from the screen: watches, cannot reach it
        r.Step(Second);
        r.In(() =>
        {
            Assert.Equal(0, Dm.length(tv.network));
            Assert.Equal("entertainment_blank", Dm.str(tv.icon_screen));
            Assert.NotNull(tv.speakers);
        });
        Assert.Contains("entertainment_blank", OverlayStates(r, tv));

        // nothing on TV: a click from across the room opens nothing
        Take(viewer); // the lobby's outputs from the join
        Click(r, viewer, tv);
        Assert.Empty(Take(viewer));

        // Bob goes live
        var camera = r.In(() =>
        {
            var cam = Dm.@new<obj_item_broadcast_camera>(bob.loc);
            Waiting.NoSleep(bob.put_in_active_hand(cam));
            Waiting.NoSleep(cam.attack_self(bob));
            return cam;
        });
        r.Step(Second);
        r.In(() =>
        {
            Assert.Equal(1f, camera.active);
            Assert.Equal("broadcast_cam1", Dm.str(camera.icon_state));
            Assert.NotNull(camera.internal_camera);
            Assert.Same(bob, camera.internal_camera.loc);
            Assert.Equal("LIVE: Curator News", Dm.str(camera.internal_camera.c_tag));
            Assert.NotNull(camera.internal_radio);
            Assert.True(Dm.@in(CAMERANET_NETWORK_CURATOR, tv.network));
            Assert.Equal("entertainment", Dm.str(tv.icon_screen));
        });
        Assert.Contains("entertainment", OverlayStates(r, tv));
        Assert.True(feed.Any("Curator News is now LIVE!"), Diag(feed, r));
        Assert.Contains(r.Sounds.Heard, s => (s.File ?? "").EndsWith("terminal_processing.ogg", StringComparison.Ordinal));

        // the viewer watches from across the room: the camera window with the live camera
        Click(r, viewer, tv);
        var payload = LastUpdate(Take(viewer), out _);
        Assert.Equal("CameraConsole", InterfaceOf(payload));
        Assert.Contains(payload.GetProperty("static_data").GetProperty("cameras").EnumerateArray(), c => c.GetProperty("name").GetString() == "LIVE: Curator News");

        // the examine and the mute
        var examine = r.In(() => Dm.each(Waiting.NoSleep(tv.examine(viewer)) as DmList).Select(Dm.str).ToList());
        Assert.Contains(examine, l => l.Contains("The TV is broadcasting something!", StringComparison.Ordinal));
        Assert.Contains(examine, l => l.Contains("The volume is currently on.", StringComparison.Ordinal));
        r.In(() => Waiting.NoSleep(tv.click_ctrl(viewer)));
        Assert.Equal(0f, r.In(() => tv.speakers.should_be_listening));

        // dropping the camera ends the show
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(camera)));
        r.Step(Second);
        r.In(() =>
        {
            Assert.Equal(0f, camera.active);
            Assert.Null(camera.internal_camera);
            Assert.Null(camera.internal_radio);
            Assert.Equal(0, Dm.length(tv.network));
            Assert.Equal("entertainment_blank", Dm.str(tv.icon_screen));
        });
        Assert.Contains(r.Sounds.Heard, s => (s.File ?? "").EndsWith("terminal_prompt_deny.ogg", StringComparison.Ordinal));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_showtime_controller_starts_and_ends_its_show_and_the_first_curator_packs_the_camera()
    {
        obj_machinery_computer_security_telescreen_entertainment tv = null;
        var r = new AudioRoom(13, 9, seed: 9953, mapExtras: w =>
        {
            tv = Dm.@new<obj_machinery_computer_security_telescreen_entertainment_directional_south>(locate(6, 2, 1));
        });
        Feed feed = Listen(r);
        var bob = r.Join(6, 4, "Bob");
        r.Step(Second);

        var controller = r.In(() => Dm.@new<obj_item_assembly_control_showtime>(locate(6, 4, 1)));
        r.In(() => Waiting.NoSleep(controller.activate(bob)));
        r.Step(Second);
        r.In(() =>
        {
            Assert.Equal(1f, controller.is_show_active);
            Assert.True(Dm.@in("thunder", tv.network));
            Assert.Equal("entertainment", Dm.str(tv.icon_screen));
        });
        Assert.True(feed.Any("The Thunderdome show has begun"), Diag(feed, r));
        r.In(() => Waiting.NoSleep(controller.activate(bob)));
        r.Step(Second);
        Assert.False(r.In(() => Dm.@in("thunder", tv.network)));
        Assert.True(feed.Any("The Thunderdome show has ended"), Diag(feed, r));

        // the round's first curator packs the broadcast camera; the next does not
        var first = r.Dummy(3, 6);
        var second = r.Dummy(4, 6);
        r.In(() =>
        {
            Waiting.NoSleep(first.equipOutfit(typeof(datum_outfit_job_curator)));
            Waiting.NoSleep(second.equipOutfit(typeof(datum_outfit_job_curator)));
        });
        r.Step(2);
        r.In(() =>
        {
            Assert.Single(Dm.each<obj_item_broadcast_camera>(first.get_all_contents()));
            Assert.Empty(Dm.each<obj_item_broadcast_camera>(second.get_all_contents()));
        });
        r.AssertNoRuntimes();
    }
}
