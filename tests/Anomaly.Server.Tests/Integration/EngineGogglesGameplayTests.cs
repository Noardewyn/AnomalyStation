using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// M7 (D170): the engineering scanner goggles on MetaStation through the real server. An engineer wears the goggles and cycles them with
/// their action button: his client gets the meson sight in the meson mode (and, as in tg, in the off mode until the T-ray mode takes the
/// trait away), the hidden cables as private images in the T-ray mode. An atmospheric technician's goggles also show an arrow for
/// every direction a smart pipe connects to and a coloured overlay on every open turf in view, tg's thermal view.
/// </summary>
public class EngineGogglesGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;

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

    private static RealHeadlessWorld Meta(int seed) => new(new LoopbackOptions { Seed = (ulong)seed, TimeoutSteps = 12000 },
        MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);

    private static void ClickScreen(RealHeadlessWorld.Player p, long id) => Assert.True(p.Session.Click(new ClickTarget(TargetKind.Screen, id, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));

    private static T GiveAndWear<T>(RealHeadlessWorld w, RealHeadlessWorld.Player p, mob_living_carbon_human me, string key, string path) where T : obj_item_clothing_glasses_meson_engine
    {
        w.Host.EnqueueConsoleLine($"give {key} {path}");
        w.Run(5);
        var goggles = In(w, () => me.get_active_held_item()) as T;
        Assert.NotNull(goggles);
        p.Session.Keybinding(KeybindingCode.QuickEquip); // E: the goggles to the eyes
        w.Run(Second);
        Assert.Same(goggles, In(w, () => me.glasses));
        return goggles;
    }

    private static void Cycle(RealHeadlessWorld w, RealHeadlessWorld.Player p, mob_living_carbon_human me, obj_item_clothing_glasses_meson_engine goggles, string expectedMode)
    {
        long button = In(w, () => Assert.Single(Dm.each_cast<datum_action>(goggles.actions)).viewers[me.hud_used] is atom_movable_screen_movable_action_button b ? b.dm_id : 0);
        Assert.NotEqual(0, button);
        ClickScreen(p, button);
        w.Run(Second / 2);
        Assert.Equal(expectedMode, In(w, () => goggles.mode));
    }

    private static List<ImageEntry> OnTiles(ReplicaWorld replica) => replica.Images.Values.Where(i => i.TurfX > 0).ToList();

    private static string State(ReplicaWorld replica, ImageEntry i) => replica.TryGetAppearance(i.Appearance, out AppearanceEntry look) ? replica.ResolveString(look.IconState) : null;

    private static bool WaitForImages(RealHeadlessWorld w, ReplicaWorld replica, Func<ImageEntry, bool> which, int seconds = 5)
    {
        for (int t = 0; t < Second * seconds; t++)
        {
            w.Run(1);
            if (OnTiles(replica).Any(which))
                return true;
        }
        return false;
    }

    // An open floor tile of the first level with a hidden cable and no dense thing, with the most hidden things around.
    private static (int X, int Y) CableSpot(RealHeadlessWorld w) => In(w, () => Dm.each<obj_structure_cable>(Dm.world)
        .Where(c => c.loc is turf_open_floor_iron t && Dm.eq(t.z, 1) && !Dm.each_cast<atom>(t.contents).Any(a => Dm.truthy(a.density)) && HAS_TRAIT(c, TRAIT_T_RAY_VISIBLE))
        .Select(c => ((int)c.x, (int)c.y)).Distinct()
        .OrderByDescending(c => Dm.each<obj>(orange(2, locate(c.Item1, c.Item2, 1))).Count(o => HAS_TRAIT(o, TRAIT_T_RAY_VISIBLE))).ThenBy(c => c.Item1).ThenBy(c => c.Item2).First());

    [MetaServerFact]
    public void An_engineer_cycles_his_goggles_and_his_client_gets_the_meson_sight_in_the_meson_mode_and_the_hidden_cables_in_the_t_ray_mode()
    {
        using RealHeadlessWorld w = Meta(73);
        RealHeadlessWorld.Player p = w.Join("Engineer");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        (int X, int Y) spot = CableSpot(w);
        In(w, () => Waiting.NoSleep(me.forceMove(locate(spot.X, spot.Y, 1))));
        w.Run(2 * Second);
        int bootRuntimes = w.World.RuntimeErrorCount;
        Assert.Equal(VisionState.Normal, replica.OwnVision);

        var goggles = GiveAndWear<obj_item_clothing_glasses_meson_engine>(w, p, me, "Engineer", "/obj/item/clothing/glasses/meson/engine");
        Assert.Equal("trayson-", In(w, () => goggles.icon_state));
        // tg keeps the meson trait of the optical mesons in the engine goggles' off mode until the T-ray mode takes it from the list
        Assert.True(replica.OwnVision.SeesTurfs);
        Assert.Empty(OnTiles(replica));

        // meson: the cutoffs list(15, 12, 0) of the goggles (toggle_mode :55) reach the client with the sight
        Cycle(w, p, me, goggles, "meson");
        Assert.Equal("trayson-meson", In(w, () => goggles.icon_state));
        VisionState vision = replica.OwnVision;
        output.WriteLine(vision.ToString());
        Assert.True(vision.SeesTurfs);
        (float r, float g, float b) = vision.LightFloor;
        Assert.Equal((0.15f, 0.12f, 0f), (r, g, b), new Tolerance());

        // t-ray: the sight goes, the pulse puts an image of every hidden thing within one tile on its tile for the wearer alone
        Cycle(w, p, me, goggles, "t-ray");
        Assert.Equal("trayson-t-ray", In(w, () => goggles.icon_state));
        Assert.Equal(VisionState.Normal, replica.OwnVision);
        Assert.True(WaitForImages(w, replica, _ => true), "the T-ray pulse shows images");
        List<ImageEntry> pulse = OnTiles(replica);
        List<obj> expected = In(w, () => Dm.each<obj>(orange(1, me)).Where(o => HAS_TRAIT(o, TRAIT_T_RAY_VISIBLE)).ToList());
        output.WriteLine($"{pulse.Count} images, {expected.Count} hidden things within 1 tile of ({spot.X}, {spot.Y})");
        Assert.Equal(expected.Count, pulse.Count);
        Assert.All(pulse, i =>
        {
            Assert.True(replica.TryGetAppearance(i.Appearance, out AppearanceEntry look));
            Assert.Equal(128, look.Alpha);
            Assert.InRange(Math.Max(Math.Abs(i.TurfX - spot.X), Math.Abs(i.TurfY - spot.Y)), 0, 1);
        });

        // off: the scans stop (DV91: the last set goes by its own timer within 2.5 s), the cycle wraps
        Cycle(w, p, me, goggles, "");
        w.Run(Second * 4);
        Assert.Empty(OnTiles(replica));
        Assert.False(replica.OwnVision.SeesTurfs);
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }

    [MetaServerFact]
    public void An_atmospheric_technician_sees_the_pipe_connections_and_the_thermal_air_through_his_goggles()
    {
        using RealHeadlessWorld w = Meta(74);
        RealHeadlessWorld.Player p = w.Join("Technician");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        // a smart pipe on an open floor tile with no dense thing
        (int X, int Y) spot = In(w, () => Dm.each<obj_machinery_atmospherics_pipe_smart>(Dm.world)
            .Where(s => s.loc is turf_open t && Dm.eq(t.z, 1) && !Dm.each_cast<atom>(t.contents).Any(a => Dm.truthy(a.density)))
            .Select(s => ((int)s.x, (int)s.y)).Distinct().OrderBy(c => c.Item1).ThenBy(c => c.Item2).First());
        In(w, () => Waiting.NoSleep(me.forceMove(locate(spot.X, spot.Y, 1))));
        w.Run(2 * Second);
        int bootRuntimes = w.World.RuntimeErrorCount;

        var goggles = GiveAndWear<obj_item_clothing_glasses_meson_engine_tray>(w, p, me, "Technician", "/obj/item/clothing/glasses/meson/engine/tray");
        Assert.Empty(OnTiles(replica));

        // connectable (after t-ray): an arrow for every init direction of each smart pipe within two tiles, in the pipe's colour and layer
        Cycle(w, p, me, goggles, "t-ray");
        Cycle(w, p, me, goggles, "connectable");
        Assert.Equal(VisionState.Normal, replica.OwnVision);
        Assert.True(WaitForImages(w, replica, i => State(replica, i) == "connection_overlay"), "the arrows");
        var arrows = OnTiles(replica).Where(i => State(replica, i) == "connection_overlay").ToList();
        var pipes = In(w, () => Dm.each<obj_machinery_atmospherics_pipe_smart>(orange(2, me)).ToList());
        int expectedArrows = In(w, () => pipes.Sum(s => new[] { NORTH, SOUTH, EAST, WEST }.Count(d => Dm.truthy(Dm.bitand(s.get_init_directions(), d)))));
        output.WriteLine($"{arrows.Count} arrows for {pipes.Count} pipes within 2 tiles of ({spot.X}, {spot.Y}), expected {expectedArrows}");
        Assert.True(pipes.Count >= 1);
        Assert.Equal(expectedArrows, arrows.Count);
        foreach (ImageEntry arrow in arrows)
        {
            Assert.True(replica.TryGetAppearance(arrow.Appearance, out AppearanceEntry look));
            Assert.Contains(look.Dir, new byte[] { 1, 2, 4, 8 });
            Assert.Contains("simple.dmi", replica.ResolveResource(look.Icon), StringComparison.Ordinal);
            // several pipes can share a tile (supply and scrubbers on their own piping layers): the arrow has the layer of one of them
            var here = pipes.Where(s => In(w, () => (int)s.x == arrow.TurfX && (int)s.y == arrow.TurfY)).ToList();
            Assert.Contains(here, s => Math.Abs(In(w, () => (double)s.layer) - look.Layer) < 0.001);
            Assert.NotEqual(0xFFFFFFFFu, look.Rgba); // the pipe's own colour (supply blue, scrubbers red...)
        }

        // atmospheric-thermal: an overlay on every open turf in view(5), at alpha 200 on tg's layer, coloured by the air's temperature
        Cycle(w, p, me, goggles, "atmospheric-thermal");
        Assert.True(WaitForImages(w, replica, i => State(replica, i) == "greyOverlay"), "the thermal overlays");
        var overlays = OnTiles(replica).Where(i => State(replica, i) == "greyOverlay").ToList();
        int open = In(w, () => Dm.each<turf>(view(5, me)).Count(t => !Dm.truthy(t.blocks_air)));
        output.WriteLine($"{overlays.Count} thermal overlays for {open} open turfs in view");
        Assert.Equal(open, overlays.Count);
        Assert.All(overlays, i =>
        {
            Assert.True(replica.TryGetAppearance(i.Appearance, out AppearanceEntry look));
            Assert.Equal(200, look.Alpha);
            Assert.Equal((double)ABOVE_OPEN_TURF_LAYER, look.Layer, 1);
            Assert.NotEqual(0xFFFFFFFFu, look.Rgba);
        });
        // a turf of room-temperature air (293 K) is between green and yellow: more green than blue and more red than blue
        var room = overlays.First(i => i.TurfX == spot.X && i.TurfY == spot.Y);
        replica.TryGetAppearance(room.Appearance, out AppearanceEntry roomLook);
        uint rgba = roomLook.Rgba;
        float temperature = In(w, () => Dm.num(((turf_open)locate(spot.X, spot.Y, 1)).air.temperature));
        output.WriteLine($"({spot.X}, {spot.Y}) is {temperature} K: {rgba:X8}");
        if (temperature is > 283.15f and < 300f)
            Assert.True(((rgba >> 24) & 255) > ((rgba >> 8) & 255) && ((rgba >> 16) & 255) > ((rgba >> 8) & 255));

        // off: nothing is shown any more
        Cycle(w, p, me, goggles, "");
        w.Run(Second * 4);
        Assert.Empty(OnTiles(replica));
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }

    private sealed class Tolerance : IEqualityComparer<(float, float, float)>
    {
        public bool Equals((float, float, float) a, (float, float, float) b) => Math.Abs(a.Item1 - b.Item1) < 0.01f && Math.Abs(a.Item2 - b.Item2) < 0.01f && Math.Abs(a.Item3 - b.Item3) < 0.01f;

        public int GetHashCode((float, float, float) t) => 0;
    }
}
