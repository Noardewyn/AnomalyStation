using Anomaly.Client.Shared.Icons;
using Anomaly.Client.Shared.World;
using Anomaly.Protocol;
using Xunit.Abstractions;

namespace Anomaly.Server.Tests.Client.Render;

/// <summary>
/// M4v slice 2 (D143): the frames tg plays on the ported doors and turfs, read from the converted icons as the client composes them
/// (the door pieces' pictures, MapRenderer.BuildPiece; the turf layers, TileKit), and the rule of the door panels' picture.
/// </summary>
public class PieceAnimationTests(ITestOutputHelper output)
{
    private const string Airlock = "icons/obj/doors/airlocks/station/public.dmi";
    private const string AirlockOverlays = "icons/obj/doors/airlocks/station/overlays.dmi";
    private const string Firelock = "icons/obj/doors/doorfireglass.dmi";
    private const string BlastDoor = "icons/obj/doors/blastdoor.dmi";
    private const string Floors = "icons/turf/floors.dmi";

    // An airlock's appearance as tg's update_icon_state and update_overlays make it (airlock.dm:555-599): the icon state, the frame,
    // the fill and the lights of one frame state.
    private static TableRef AirlockLook(TestTables tables, string iconState, string frame, string lights)
    {
        TableRef frameLook = tables.Add(Airlock, frame);
        TableRef fill = tables.Add(Airlock, "fill_" + frame);
        TableRef light = lights == null ? TableRef.None : tables.Add(AirlockOverlays, "lights_" + lights);
        return tables.Add(Airlock, iconState, e =>
        {
            e.Overlays.Add(frameLook);
            e.Overlays.Add(fill);
            if (!light.IsNone)
                e.Overlays.Add(light);
        });
    }

    [RealIconsFact]
    public void An_airlock_that_denies_blinks_its_red_lights_once_in_tgs_six_frames()
    {
        // airlock.dm: AIRLOCK_DENY shows the closed frame with lights_denied (six frames of 0.1 s, once); its icon state "deny" is not in the icon
        var tables = new TestTables();
        TableRef deny = AirlockLook(tables, "deny", "closed", "denied");
        var compositor = new AppearanceCompositor(RealIcons.NewLibrary());
        Assert.True(compositor.TryCompose(tables, deny, ByondDirConst.South, 0, out ComposedRaster first));
        Assert.Equal((32, 32), (first.Image.Width, first.Image.Height));
        AnimationTimeline timeline = AnimationTimeline.Of(compositor.AnimatedStates(tables, deny, ByondDirConst.South), once: false);
        Assert.NotNull(timeline);
        Assert.Equal((6, 0.6, false), (timeline.Count, Math.Round(timeline.Length, 4), timeline.Loops));
        Assert.True(compositor.TryCompose(tables, deny, ByondDirConst.South, timeline.SampleAt(1), out ComposedRaster second));
        Assert.NotEqual(first.Image.Pixels, second.Image.Pixels);
    }

    [RealIconsFact]
    public void A_closed_airlock_on_emergency_access_blinks_its_yellow_lights_for_ever()
    {
        var tables = new TestTables();
        TableRef emergency = AirlockLook(tables, "closed", "closed", "emergency");
        TableRef plain = AirlockLook(tables, "closed", "closed", null);
        var compositor = new AppearanceCompositor(RealIcons.NewLibrary());
        AnimationTimeline timeline = AnimationTimeline.Of(compositor.AnimatedStates(tables, emergency, ByondDirConst.South), once: false);
        Assert.Equal((2, 2.4, true), (timeline.Count, Math.Round(timeline.Length, 4), timeline.Loops));
        Assert.Null(AnimationTimeline.Of(compositor.AnimatedStates(tables, plain, ByondDirConst.South), once: false)); // a plain closed airlock is still
    }

    [RealIconsFact]
    public void An_opening_airlocks_first_picture_and_a_closing_ones_last_are_the_closed_door()
    {
        // The 3D panels slide by themselves: they show the closed position of every layer (DoorPicture.OwnFirst and OwnLast)
        var tables = new TestTables();
        TableRef closed = AirlockLook(tables, "closed", "closed", null);
        TableRef opening = AirlockLook(tables, "opening", "opening", null);
        TableRef closing = AirlockLook(tables, "closing", "closing", null);
        var compositor = new AppearanceCompositor(RealIcons.NewLibrary());
        Assert.True(compositor.TryCompose(tables, closed, ByondDirConst.South, 0, out ComposedRaster shut));
        byte[] shutPixels = shut.Image.Pixels.ToArray();
        Assert.True(compositor.TryCompose(tables, opening, ByondDirConst.South, 0, out ComposedRaster start));
        Assert.Equal(shutPixels, start.Image.Pixels);
        AnimationTimeline once = AnimationTimeline.Of(compositor.AnimatedStates(tables, closing, ByondDirConst.South), once: true);
        Assert.Equal(0.6, once.Length, 4);
        Assert.True(compositor.TryCompose(tables, closing, ByondDirConst.South, once.SampleAt(once.Count - 1), out ComposedRaster end));
        Assert.Equal(shutPixels, end.Image.Pixels);
    }

    [RealIconsFact]
    public void A_blast_doors_opening_frames_are_its_slab_going_up()
    {
        // blastdoor.dmi "opening": eight frames (0.1 s, the last 0.4 s), each four rows more of the slab gone; the 3D slab drops instead (DoorAnimation)
        IIcon icon = Icon(BlastDoor);
        Assert.True(icon.TryGetState("opening", out IIconState opening));
        Assert.True(icon.TryGetState("closed", out IIconState closed));
        Assert.Equal(8, opening.Frames);
        Assert.Equal(Drawn(closed.Get(0, 0)), Drawn(opening.Get(0, 0))); // the first frame is the whole slab (a shade lighter than "closed")
        int previous = int.MaxValue;
        for (int f = 0; f < 7; f++)
        {
            int drawn = Drawn(opening.Get(0, f));
            output.WriteLine($"opening frame {f}: {drawn} pixels");
            Assert.True(drawn < previous);
            previous = drawn;
        }
    }

    [RealIconsFact]
    public void A_firelock_plays_its_alarm_lights_and_its_opening_frames()
    {
        // firedoor.dm: the hazard lights overlay (firelock_alarm_type_*: 0.9 s and three of 0.1 s, for ever); door_opening, six of 0.2 s once
        var tables = new TestTables();
        TableRef alarm = tables.Add(Firelock, "firelock_alarm_type_generic");
        TableRef closedAlarm = tables.Add(Firelock, "door_closed", e => e.Overlays.Add(alarm));
        TableRef opening = tables.Add(Firelock, "door_opening");
        var compositor = new AppearanceCompositor(RealIcons.NewLibrary());
        AnimationTimeline lights = AnimationTimeline.Of(compositor.AnimatedStates(tables, closedAlarm, ByondDirConst.South), once: false);
        Assert.Equal((4, 1.2, true), (lights.Count, Math.Round(lights.Length, 4), lights.Loops));
        AnimationTimeline open = AnimationTimeline.Of(compositor.AnimatedStates(tables, opening, ByondDirConst.South), once: false);
        Assert.Equal((6, 1.2, false), (open.Count, Math.Round(open.Length, 4), open.Loops));
    }

    [RealIconsFact]
    public void MetaStations_animated_turfs_play_tgs_frames()
    {
        // The mech bay's recharge floor (iron_floor.dm:308), the park's water (water.dm: riverwater_motion and deep_riverwater_motion, eight
        // frames of 1, 2, 3, 2, 1, 2, 3, 2 ds played back and forth: 14 pictures, 2.9 s), the asteroid's glowing basalt (basalt.dm: four of 2 s)
        var library = RealIcons.NewLibrary();
        var compositor = new AppearanceCompositor(library);
        var tables = new TestTables();
        foreach ((string state, int pictures, double length) in new[] { ("recharge_floor", 2, 1.2), ("riverwater_motion", 14, 2.9), ("deep_riverwater_motion", 14, 2.9), ("basalt1", 4, 8.0) })
        {
            TableRef turf = tables.Add(Floors, state);
            Assert.True(compositor.TryCompose(tables, turf, ByondDirConst.South, 0, out ComposedRaster raster));
            Assert.True(raster.IsAnimated, state);
            AnimationTimeline timeline = AnimationTimeline.Of(compositor.AnimatedStates(tables, turf, ByondDirConst.South), once: false);
            Assert.Equal((pictures, length, true), (timeline.Count, Math.Round(timeline.Length, 4), timeline.Loops));
            var frames = new TurfFrames();
            int group = frames.Add(timeline, Enumerable.Range(10, timeline.Count).ToArray());
            Assert.Equal(10 + timeline.Count - 1, frames.LayerAt(group, timeline.Length - 0.001));
        }
        foreach (string still in new[] { "bcircuit", "gcircuit", "rcircuit", "floor", "plating" })
        {
            TableRef turf = tables.Add(Floors, still);
            Assert.True(compositor.TryCompose(tables, turf, ByondDirConst.South, 0, out ComposedRaster raster));
            Assert.False(raster.IsAnimated, still);
        }
    }

    [Theory]
    [InlineData("closed", false, DoorPicture.Own)]
    [InlineData("closed", true, DoorPicture.Own)]
    [InlineData("deny", true, DoorPicture.Own)]
    [InlineData("opening", true, DoorPicture.OwnFirst)]
    [InlineData("closing", true, DoorPicture.OwnLast)]
    [InlineData("open", true, DoorPicture.Keep)]
    [InlineData("open", false, DoorPicture.ClosedFallback)]
    [InlineData("leftsecureopening", true, DoorPicture.OwnFirst)]
    [InlineData("rightdeny", false, DoorPicture.Own)]
    [InlineData("leftopen", true, DoorPicture.Keep)]
    [InlineData("left", true, DoorPicture.Own)]
    [InlineData(null, false, DoorPicture.Own)]
    public void A_doors_panels_show_the_picture_of_its_state(string state, bool built, DoorPicture expected)
        => Assert.Equal(expected, DoorPictures.Of(state, built));

    private static IIcon Icon(string path)
    {
        Assert.True(RealIcons.NewLibrary().TryGetIcon(path, out IIcon icon));
        return icon;
    }

    private static int Drawn(Raster image)
    {
        int n = 0;
        for (int i = 3; i < image.Pixels.Length; i += 4)
            if (image.Pixels[i] > 0)
                n++;
        return n;
    }
}
