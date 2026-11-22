using System.IO;
using Anomaly.Client.Shared.Icons;
using Anomaly.Client.Shared.World;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Replication;
using Anomaly.Server.Tests.Client.Render;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// The captain-caster (tg's greenscreen camera, status_display.dm:896-1200; syndicate campaign D316) through the real server with a headless
/// client: the feed is an object in nullspace that every status display holds in its vis_contents, and the mob on the greenscreen is a copy
/// in the feed's vis_contents. The client sees the copy in the evac display's picture when the mob steps on, sees it turn when the mob
/// turns (the replicator re-reads an atom's vis_locs holders when its look changes, as BYOND redraws them), and sees it go when it leaves.
/// The client's compositor draws the copy in the display's screen: the feed inherits the display's layer (VIS_INHERIT_LAYER), so it is over
/// the screen, not under it.
/// </summary>
public class GreenscreenGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;

    private sealed class Tables(ReplicaWorld replica) : IAppearanceSource
    {
        public bool TryGetAppearance(TableRef reference, out AppearanceEntry entry) => replica.TryGetAppearance(reference, out entry);
        public string ResolveString(TableRef reference) => replica.ResolveString(reference);
        public string ResolveResource(TableRef reference) => replica.ResolveResource(reference);
    }

    // The display as the client draws it (its picture facing south); written to ANOMALY_GS_PICTURES when set.
    private static Raster Picture(AppearanceCompositor compositor, ReplicaWorld replica, ReplicaEntity display, string name)
    {
        var insets = new VisInsets(new Tables(replica));
        Assert.True(compositor.TryCompose(insets, display.State.Appearance, ByondDir.South, 0, out ComposedRaster picture), string.Join("; ", compositor.Library.Problems));
        string folder = Environment.GetEnvironmentVariable("ANOMALY_GS_PICTURES");
        if (folder != null)
            File.WriteAllBytes(Path.Combine(folder, name + ".png"), PngEncoder.Encode(picture.Image, 6, 8, 0, false));
        return picture.Image;
    }

    private static int Differing(Raster a, Raster b)
    {
        if (a.Width != b.Width || a.Height != b.Height)
            return int.MaxValue;
        int n = 0;
        for (int i = 0; i < a.Pixels.Length; i += 4)
            if (a.Pixels[i] != b.Pixels[i] || a.Pixels[i + 1] != b.Pixels[i + 1] || a.Pixels[i + 2] != b.Pixels[i + 2] || a.Pixels[i + 3] != b.Pixels[i + 3])
                n++;
        return n;
    }

    // The feed's entry in the display's picture (its underlay is the static), then the copies drawn in it (every overlay but the scanlines).
    private static List<AppearanceEntry> FeedCopies(ReplicaWorld replica, ReplicaEntity display)
    {
        Assert.True(replica.TryGetAppearance(display.State.Appearance, out AppearanceEntry root));
        foreach (TableRef o in root.Overlays)
        {
            if (!replica.TryGetAppearance(o, out AppearanceEntry feed)) continue;
            bool isFeed = feed.Underlays.Any(u => replica.TryGetAppearance(u, out AppearanceEntry under) && replica.ResolveString(under.IconState) == "static_base");
            if (!isFeed) continue;
            var copies = new List<AppearanceEntry>();
            foreach (TableRef c in feed.Overlays)
                if (replica.TryGetAppearance(c, out AppearanceEntry copy) && replica.ResolveString(copy.IconState) != "scanline")
                    copies.Add(copy);
            return copies;
        }
        return null;
    }

    [Fact]
    public void The_evac_display_shows_the_mob_on_the_greenscreen_turning_with_it_and_drops_it_when_it_leaves()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 23, TimeoutSteps = 8000 }, strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        mob_living me = w.MobOf(alice);
        w.Run(10);
        int bootRuntimes = w.World.RuntimeErrorCount;
        obj_machinery_status_display_evac display;
        obj_machinery_greenscreen_camera camera;
        mob_living_carbon_human anchor;
        turf screen;
        using (w.World.Enter())
        {
            int x = (int)me.x, y = (int)me.y;
            display = Dm.@new<obj_machinery_status_display_evac>(locate(x, y + 2, me.z));
            screen = locate(x + 4, y, me.z);
            Waiting.NoSleep(screen.ChangeTurf(typeof(turf_open_floor_greenscreen)));
            screen = locate(x + 4, y, me.z);
            camera = Dm.@new<obj_machinery_greenscreen_camera>(locate(x + 1, y, me.z));
            Waiting.NoSleep(camera.setDir(EAST));
            anchor = Dm.@new<mob_living_carbon_human>(locate(x + 3, y, me.z));
        }
        w.Run(Second);
        using (w.World.Enter())
            camera.toggle_feed();
        w.Run(Second);
        ReplicaWorld replica = alice.Session.Replica;
        ReplicaEntity Display() => replica.Entities.Single(e => e.Id == display.dm_id);
        List<AppearanceEntry> copies = FeedCopies(replica, Display());
        Assert.NotNull(copies); // the display draws the feed
        Assert.Empty(copies);
        string iconRoot = Anomaly.Server.Tests.M3Maps.I1d.MetaPictureTests.IconRoot();
        AppearanceCompositor compositor = iconRoot != null ? new AppearanceCompositor(new IconLibrary(new DirectoryIconFiles(iconRoot))) : null;
        Raster empty = compositor != null ? Picture(compositor, replica, Display(), "feed-empty") : null;

        // the greenscreen tile is marked, and the client lays the mark on it (DV383)
        ReplicaEntity mark = replica.Entities.Single(e => e.TypePath == "/obj/effect/abstract/greenscreen_location_indicator");
        Assert.Equal((screen.x, screen.y), (mark.State.X, mark.State.Y));
        Assert.True(replica.TryGetAppearance(mark.State.Appearance, out AppearanceEntry markLook));
        Assert.Contains(markLook.Filters, f => f.Kind == FilterKind.Outline && f.Rgba == 0xFFFF00FFu); // tg's yellow outline
        Assert.True(ChunkPlanner.TryPlace(new PlanEntity(mark.Id, mark.TypePath, mark.State.X, mark.State.Y, 1, mark.State.Dir, mark.State.Appearance, markLook.PixelX, markLook.PixelY,
            default, replica.ResolveString(markLook.IconState), markLook.Plane, markLook.Layer), (x, y, z) => false, out Placed placed));
        Assert.Equal(PlacedKind.FloorDecal, placed.Kind);

        // the anchor steps onto the greenscreen: the display draws a copy of it, a quarter turn round for the east-facing camera
        using (w.World.Enter())
        {
            Waiting.NoSleep(anchor.forceMove(screen));
            Waiting.NoSleep(anchor.setDir(SOUTH));
        }
        w.Run(Second);
        AppearanceEntry copy = Assert.Single(FeedCopies(replica, Display()));
        output.WriteLine($"copy icon {replica.ResolveResource(copy.Icon)} dir {copy.Dir}, {copy.Overlays.Count} overlays");
        Assert.Equal(ReplicatedFields.WireDir(turn(SOUTH, 90)), copy.Dir);
        Assert.NotEmpty(copy.Overlays); // the anchor's body
        if (compositor != null)
        {
            // the client draws the anchor in the display's screen: dozens of its pixels differ from the empty feed's
            int differing = Differing(empty, Picture(compositor, replica, Display(), "feed-anchor"));
            output.WriteLine($"{differing} pixels differ from the empty feed");
            Assert.InRange(differing, 40, 32 * 32);
        }

        // the anchor turns: the copy turns in the client's picture (the feed and the copy are in nullspace: the display is re-read)
        using (w.World.Enter())
            Waiting.NoSleep(anchor.setDir(NORTH));
        w.Run(Second);
        copy = Assert.Single(FeedCopies(replica, Display()));
        Assert.Equal(ReplicatedFields.WireDir(turn(NORTH, 90)), copy.Dir);

        // the anchor walks off: the copy leaves the picture
        using (w.World.Enter())
            Waiting.NoSleep(anchor.forceMove(locate(screen.x - 1, screen.y, screen.z)));
        w.Run(Second);
        Assert.Empty(FeedCopies(replica, Display()));

        output.WriteLine(w.Err.ToString());
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
