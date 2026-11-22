using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tests.Gaps.GapsHarness;

namespace Anomaly.Core.Tests.Syndicate;

/// <summary>
/// The captain-caster (tg's greenscreen camera, status_display.dm:896-1200; syndicate campaign D316) in a booted toy room: switched on, the
/// camera finds the greenscreen in front of it, marks it, and tells the status displays to show its feed; whoever stands on the greenscreen
/// is copied into the feed (turned as the display faces), follows the mob's turns and leaves with it; switched off, the displays go back to
/// the shuttle timer. With no backdrop within five tiles it refuses, a wall-mounted greenscreen poster is a backdrop, and turning a
/// broadcasting camera stops the feed.
/// </summary>
public class GreenscreenTests
{
    private static (AudioRoom R, mob_living_carbon_human Bob, Feed Feed) Room(ulong seed, int bobX = 3, int bobY = 6)
    {
        var r = new AudioRoom(15, 9, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(bobX, bobY, "Bob");
        r.Step(Second);
        return (r, bob, feed);
    }

    private static obj_machinery_greenscreen_camera Camera(AudioRoom r, int x, int y, float dir) => r.In(() =>
    {
        var camera = Dm.@new<obj_machinery_greenscreen_camera>(locate(x, y, 1));
        Waiting.NoSleep(camera.setDir(dir));
        return camera;
    });

    [Fact]
    public void TheGreenscreenIsLive()
    {
        foreach (Type type in new[] { typeof(obj_machinery_greenscreen_camera), typeof(obj_effect_abstract_greenscreen_display), typeof(obj_effect_abstract_greenscreen_appearance_holder), typeof(obj_effect_abstract_greenscreen_location_indicator) })
            Assert.False(Attribute.IsDefined(type, typeof(DmInertAttribute), inherit: false), type.Name + " is inert");
    }

    [Fact]
    public void TheCameraShowsWhoeverStandsOnTheGreenscreenOnTheStatusDisplays()
    {
        // Bob stands by the display, out of earshot of the greenscreen (eight tiles)
        var (r, bob, feed) = Room(2201, 1, 8);
        r.In(() => Waiting.NoSleep(locate(9, 4, 1).ChangeTurf(typeof(turf_open_floor_greenscreen))));
        var evac = r.In(() => Dm.@new<obj_machinery_status_display_evac>(locate(2, 8, 1)));
        var camera = Camera(r, 4, 4, EAST);
        r.Step(Second);

        r.In(() => camera.toggle_feed());
        r.Step(2);
        obj_effect_abstract_greenscreen_display display = r.In(() => camera.display);
        Assert.NotNull(display);
        Assert.Equal(EAST, r.In(() => display.dir));
        Assert.Single(r.In(() => Dm.each<obj_effect_abstract_greenscreen_location_indicator>(locate(9, 4, 1).contents).ToList()));
        Assert.Equal((float)ACTIVE_POWER_USE, r.In(() => camera.use_power));
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/machines/terminal/terminal_on.ogg");
        // the display shows the feed in its vis_contents
        Assert.Equal((float)SD_GREENSCREEN, r.In(() => evac.current_mode));
        Assert.True(r.In(() => Dm.@in(display, evac.vis_contents)), "the status display shows the feed");

        // a mob steps onto the greenscreen: it is copied into the feed, turned as the east-facing display shows it
        mob_living_carbon_human anchor = r.Dummy(8, 4);
        r.In(() => anchor.real_name = anchor.name = "Anchor");
        r.In(() => anchor.add_filter("anchor_glow", 2, outline_filter(1, "#39ff14") as DmList)); // a glow the copy keeps (BYOND copies an appearance's filters)
        r.In(() => Waiting.NoSleep(anchor.forceMove(locate(9, 4, 1))));
        r.Step(2);
        var holder = r.In(() => display.displaying[anchor] as obj_effect_abstract_greenscreen_appearance_holder);
        Assert.NotNull(holder);
        Assert.True(r.In(() => Dm.@in(holder, display.vis_contents)));
        Assert.Equal(r.In(() => anchor.icon), r.In(() => holder.icon));
        Assert.True(r.In(() => Dm.truthy(Dm.bitand(holder.vis_flags, VIS_INHERIT_ID))));
        Assert.Equal(1, r.In(() => Dm.length(holder.filters)));
        string examined = r.In(() => string.Join("\n", Dm.each<object>(Waiting.NoSleep(evac.examine(bob)) as DmList).Select(Dm.str)));
        Assert.Contains("Anchor", examined);

        // the camera's microphone relays what is said on the greenscreen to the displays' speakers: Bob hears it from the display
        r.In(() => Waiting.Detach(anchor.say("Good evening, station")));
        r.Step(Second);
        Assert.Contains(feed.Chat, c => ReferenceEquals(c.Target, bob) && c.Html != null && c.Html.Contains("Good evening, station", StringComparison.Ordinal));

        // the mob turns north: the feed turns with it (a quarter turn for the east-facing camera)
        r.In(() => Waiting.NoSleep(anchor.setDir(NORTH)));
        r.Step(1);
        Assert.Equal((float)turn(NORTH, 90), r.In(() => holder.dir));

        // the mob walks off the greenscreen: it leaves the feed
        r.In(() => Waiting.NoSleep(anchor.forceMove(locate(8, 4, 1))));
        r.Step(2);
        Assert.Equal(0, r.In(() => Dm.length(display.displaying)));
        Assert.True(r.In(() => Dm.truthy(holder.gc_destroyed)), "the copy is deleted");

        // switched off: no feed, no mark, the displays go back to the shuttle timer
        r.In(() => camera.toggle_feed());
        r.Step(2);
        Assert.Null(r.In(() => camera.display));
        Assert.Empty(r.In(() => Dm.each<obj_effect_abstract_greenscreen_location_indicator>(locate(9, 4, 1).contents).ToList()));
        Assert.Equal((float)SD_EMERGENCY, r.In(() => evac.current_mode));
        Assert.False(r.In(() => Dm.@in(display, evac.vis_contents)), "the deleted feed leaves the display");
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/machines/terminal/terminal_off.ogg");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void WithoutABackdropTheCameraRefusesAndAWallPosterIsOne()
    {
        var (r, _, _) = Room(2202);
        var camera = Camera(r, 2, 4, EAST);
        r.Step(Second);
        r.In(() => camera.toggle_feed());
        r.Step(2);
        Assert.Null(r.In(() => camera.display));
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/machines/terminal/terminal_on.ogg" && h.Frequency == 0.5f);

        // a greenscreen poster mounted on the wall three tiles ahead: that tile is the backdrop
        r.In(() =>
        {
            var poster = Dm.@new<obj_structure_sign_poster_greenscreen>(locate(5, 4, 1));
            poster.pixel_y = 32;
        });
        r.In(() => camera.toggle_feed());
        r.Step(2);
        Assert.NotNull(r.In(() => camera.display));
        Assert.Single(r.In(() => Dm.each<obj_effect_abstract_greenscreen_location_indicator>(locate(5, 4, 1).contents).ToList()));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TurningABroadcastingCameraStopsTheFeed()
    {
        var (r, _, _) = Room(2203);
        r.In(() => Waiting.NoSleep(locate(7, 4, 1).ChangeTurf(typeof(turf_open_floor_greenscreen))));
        var camera = Camera(r, 4, 4, EAST);
        r.Step(Second);
        r.In(() => camera.toggle_feed());
        r.Step(2);
        Assert.NotNull(r.In(() => camera.display));

        r.In(() => Waiting.NoSleep(camera.setDir(NORTH)));
        r.Step(2);
        Assert.Null(r.In(() => camera.display));
        Assert.Empty(r.In(() => Dm.each<obj_effect_abstract_greenscreen_location_indicator>(locate(7, 4, 1).contents).ToList()));
        Assert.Equal(0, r.In(() => Dm.length(GLOB.greenscreen_displays)));
        r.AssertNoRuntimes();
    }
}
