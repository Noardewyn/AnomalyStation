using Anomaly.Client.Shared.Icons;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// D142: a wet floor shows tg's water (wet_floor.dm update_overlay: water.dmi "wet_floor_static" on a tiled floor) in the turf picture
/// the client draws, and the picture goes back to the dry floor when the water dries.
/// </summary>
public class WetFloorGameplayTests
{
    private const int Second = 20;
    private const int X = 46, Y = 122; // the arrivals corridor (FlashlightGameplayTests' place)

    private static bool ShowsWater(ReplicaWorld replica)
        => replica.TryGetAppearance(replica.TurfAppearance(X, Y, 1), out AppearanceEntry floor)
           && floor.Overlays.Any(o => replica.TryGetAppearance(o, out AppearanceEntry e) && replica.ResolveString(e.IconState) == "wet_floor_static");

    // The client's ReplicaTables (godot/scripts/Render, not in this test project).
    private sealed class Tables(ReplicaWorld replica) : IAppearanceSource
    {
        public bool TryGetAppearance(TableRef reference, out AppearanceEntry entry) => replica.TryGetAppearance(reference, out entry);
        public string ResolveString(TableRef reference) => replica.ResolveString(reference);
        public string ResolveResource(TableRef reference) => replica.ResolveResource(reference);
    }

    // The client's picture of the floor (TileKit composes a turf's appearance the same way); null without the converted icons.
    private static byte[] Picture(ReplicaWorld replica)
    {
        string root = Anomaly.Server.Tests.M3Maps.I1d.MetaPictureTests.IconRoot();
        if (root == null)
            return null;
        var compositor = new AppearanceCompositor(new IconLibrary(new DirectoryIconFiles(root)));
        Assert.True(compositor.TryCompose(new Tables(replica), replica.TurfAppearance(X, Y, 1), ByondDir.South, 0, out ComposedRaster picture));
        return picture.Image.Pixels;
    }

    [MetaServerFact]
    public void A_wet_floor_shows_its_water_to_the_client_until_it_dries()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 23, TimeoutSteps = 8000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Assistant");
        w.Run(Second);
        ReplicaWorld replica = p.Session.Replica;
        Assert.False(ShowsWater(replica));
        byte[] dry = Picture(replica);

        w.Host.EnqueueConsoleLine($"air add water_vapor 30 {X} {Y} 1 2"); // the vapour condenses on the floor (reactions.dm, the vapour reaction)
        w.Run(3 * Second);
        Assert.True(ShowsWater(replica), "the wet floor's water reached the client");
        byte[] wet = Picture(replica);
        if (dry != null)
            Assert.False(dry.AsSpan().SequenceEqual(wet), "the water is drawn in the floor's picture");

        w.Host.EnqueueConsoleLine($"air remove water_vapor {X} {Y} 1 2"); // or it condenses again
        using (w.World.Enter())
            ((turf_open)Builtins.locate(X, Y, 1)).MakeDry(Defines.TURF_WET_WATER, immediate: 1);
        w.Run(Second);
        Assert.False(ShowsWater(replica), "the dried floor lost its water");
    }
}
