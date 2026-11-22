using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Icons;
using Anomaly.Client.Shared.World;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// M6 slice 1 (blood in the world) through the real server on MetaStation with a headless client, in the corridor east of the
/// wall at (42,155): a player in shoes walks east through a blood pool and leaves tg's footprints on the tiles after it (the bloody
/// soles component, footprints.dmi), with the shoe blood on their worn shoes; a bleeding chest drips on the floor (blood.dm's
/// bleed, the drip decals of blood.dmi); the player hits a person with a toolbox until the blow bloodies it (attack_effects) and
/// the dropped toolbox is drawn with tg's item stain ("itemblood"). The client sees every decal and overlay.
/// </summary>
public class BloodGameplayTests
{
    private const int Second = 20;
    private const int X = 43, Y = 155;
    private const double East = -Math.PI / 2; // Godot's yaw: 0 looks north, +90 degrees west

    private readonly ITestOutputHelper _output;

    public BloodGameplayTests(ITestOutputHelper output) => _output = output;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static turf At(int x, int y) => Builtins.locate(x, y, 1);

    private static List<(string Icon, string State, float Layer)> Looks(ReplicaWorld replica, long id)
        => replica.TryGet(id, out ReplicaEntity e) ? CrewOutfitTests.Icons(replica, e.State.Appearance) : new();

    private static List<T> On<T>(int x, int y) where T : class => Dm.each_cast<atom>(At(x, y).contents).OfType<T>().ToList();

    // An appearance and its overlays as the client gets them: icon:state, alpha, colour, dir, plane, layer (the test's output).
    private static string Describe(ReplicaWorld replica, TableRef reference, int depth = 0)
    {
        if (depth > 4 || !replica.TryGetAppearance(reference, out AppearanceEntry e))
            return "?";
        string self = $"{replica.ResolveResource(e.Icon) ?? replica.ResolveString(e.Icon)}:{replica.ResolveString(e.IconState)} a{e.Alpha} c{e.Rgba:X8} d{e.Dir} p{e.Plane} l{e.Layer}";
        string overlays = string.Join("; ", e.Overlays.Select(o => Describe(replica, o, depth + 1)));
        return overlays.Length == 0 ? self : self + " [" + overlays + "]";
    }

    // The client's ReplicaTables (godot/scripts/Render, not in this test project): the replica's tables for the compositor.
    private sealed class Tables(ReplicaWorld replica) : IAppearanceSource
    {
        public bool TryGetAppearance(TableRef reference, out AppearanceEntry entry) => replica.TryGetAppearance(reference, out entry);
        public string ResolveString(TableRef reference) => replica.ResolveString(reference);
        public string ResolveResource(TableRef reference) => replica.ResolveResource(reference);
    }

    // The opaque pixels of the client's picture of an entity, with the converted icons (-1: no converted icons here).
    private int DrawnPixels(ReplicaWorld replica, long id)
    {
        string root = Anomaly.Server.Tests.M3Maps.I1d.MetaPictureTests.IconRoot();
        if (root == null)
            return -1;
        var compositor = new AppearanceCompositor(new IconLibrary(new DirectoryIconFiles(root)));
        ReplicaEntity seen = replica.Entities.Single(e => e.Id == id);
        if (!compositor.TryCompose(new Tables(replica), seen.State.Appearance, seen.State.Dir, 0, out ComposedRaster picture))
            return 0;
        int drawn = 0;
        for (int i = 3; i < picture.Image.Pixels.Length; i += 4)
            if (picture.Image.Pixels[i] != 0)
                drawn++;
        return drawn;
    }

    [MetaServerFact]
    public void Blood_pools_footprints_drips_and_a_bloodied_toolbox_reach_the_client()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 61, TimeoutSteps = 8000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        MetaServer.ClearAuxBase(w.World); // the test works in the empty construction zone
        RealHeadlessWorld.Player p = w.Join("Alice");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        int bootRuntimes = w.World.RuntimeErrorCount;
        _output.WriteLine("the corridor: " + string.Join(", ", Enumerable.Range(X, 6).Select(x => In(w, () => DmTypes.PathOf(At(x, Y).GetType())))));
        Assert.All(Enumerable.Range(X, 6), x => Assert.True(In(w, () => At(x, Y) is turf_open && !Dm.truthy(At(x, Y).density)), $"({x},{Y}) is open floor"));
        Assert.True(In(w, () => me.shoes != null), "the player wears shoes");

        // A full pool east of the player; the client sees it with tg's floor blood.
        obj_effect_decal_cleanable_blood pool = In(w, () =>
        {
            Waiting.NoSleep(me.forceMove(At(X, Y)));
            var made = Dm.@new<obj_effect_decal_cleanable_blood>(At(X + 1, Y));
            made.bloodiness = BLOOD_POOL_MAX;
            return made;
        });
        w.Run(Second);
        List<(string Icon, string State, float Layer)> poolLook = Looks(replica, pool.dm_id);
        _output.WriteLine("the pool: " + string.Join(", ", poolLook.Select(l => $"{l.Icon}:{l.State}")));
        Assert.Contains(poolLook, l => l.Icon == "icons/effects/blood.dmi");
        _output.WriteLine($"  the pool's picture: {DrawnPixels(replica, pool.dm_id)} drawn pixels");

        // Walking east through it: the shoes pick the blood up (COMSIG_STEP_ON_BLOOD) and print it on the tiles after the pool.
        p.Yaw = East;
        p.Keys = MoveKeys.Forward;
        for (int i = 0; i < 4 * Second && In(w, () => me.x) < X + 4; i++)
            w.Run(1);
        p.Keys = MoveKeys.None;
        w.Run(Second);
        var soles = In(w, () => ((atom)me.shoes).GetComponent(typeof(datum_component_bloodysoles)) as datum_component_bloodysoles);
        Assert.NotNull(soles);
        _output.WriteLine($"at ({In(w, () => me.x)},{In(w, () => me.y)}); the soles' bloodiness {In(w, () => soles.total_bloodiness)}");
        var prints = In(w, () => Enumerable.Range(X + 1, 4).SelectMany(x => On<obj_effect_decal_cleanable_blood_footprints>(x, Y)).ToList());
        Assert.NotEmpty(prints);
        foreach (var print in prints)
        {
            List<(string Icon, string State, float Layer)> look = Looks(replica, print.dm_id);
            ReplicaEntity printSeen = replica.Entities.Single(e => e.Id == print.dm_id);
            _output.WriteLine($"footprints at ({In(w, () => print.x)},{Y}): " + Describe(replica, printSeen.State.Appearance) + $"; animation tracks {printSeen.Animation?.Tracks.Length ?? 0}; the walker's dir {In(w, () => me.dir)}");
            Assert.Contains(look, l => l.Icon == "icons/effects/footprints.dmi");
            int drawn = DrawnPixels(replica, print.dm_id);
            _output.WriteLine($"  the client's picture: {drawn} drawn pixels");
            Assert.True(drawn != 0, "the client draws the footprints");
            // each step is an overlay facing the way the walker went (entered_dirs, exited_dirs: EAST)
            Assert.True(replica.TryGetAppearance(printSeen.State.Appearance, out AppearanceEntry printLook));
            Assert.NotEmpty(printLook.Overlays);
            Assert.All(printLook.Overlays, o => Assert.True(replica.TryGetAppearance(o, out AppearanceEntry step) && step.Dir == 4, "a step faces east"));
        }
        List<(string Icon, string State, float Layer)> meLook = Looks(replica, me.dm_id);
        Assert.Contains(meLook, l => l.State == "shoeblood"); // the worn shoes' blood overlay (clothing.dm:656, shoes "shoe")

        // Washing the shoes cleans their soles (on_clean) and their overlay.
        using (w.World.Enter())
            Waiting.NoSleep(((atom)me.shoes).wash(CLEAN_TYPE_BLOOD));
        w.Run(Second);
        Assert.Equal(0f, In(w, () => soles.total_bloodiness));
        Assert.DoesNotContain(Looks(replica, me.dm_id), l => l.State == "shoeblood");

        // A bleeding chest drips on the floor (bleed, add_splatter_floor, the drip decals) of a clean tile (on a bloody one the
        // blood goes into the decal there, blood_types.dm:110-125).
        using (w.World.Enter())
        {
            Waiting.NoSleep(me.forceMove(At(X + 5, Y)));
            Assert.Empty(On<obj_effect_decal_cleanable>(X + 5, Y));
            ((obj_item_bodypart)me.get_bodypart(BODY_ZONE_CHEST)).adjustBleedStacks(10);
        }
        w.Run(10 * Second);
        var drips = In(w, () => On<obj_effect_decal_cleanable_blood>(X + 5, Y));
        _output.WriteLine($"blood volume {In(w, () => me.blood_volume)}; {drips.Count} new blood decals: " + string.Join(", ", drips.Select(d => In(w, () => DmTypes.PathOf(d.GetType())))));
        Assert.NotEmpty(drips);
        Assert.True(In(w, () => me.blood_volume) < BLOOD_VOLUME_NORMAL, "the bleeding lost blood");
        Assert.Contains(drips, d => Looks(replica, d.dm_id).Any(l => l.Icon == "icons/effects/blood.dmi"));

        // The player picks a toolbox up (its fingerprints: attempt_pickup) and hits a person east of them until a blow bloodies
        // it (attack_effects: prob(25 + damage * 2)).
        (obj_item toolbox, mob_living_carbon_human victim) = In(w, () =>
        {
            var tb = Dm.@new<obj_item_storage_toolbox_mechanical>(me.loc);
            var target = Dm.@new<mob_living_carbon_human>(Builtins.get_step(me, Dm.EAST));
            return ((obj_item)tb, target);
        });
        w.Run(Second);
        Assert.True(p.Session.Click(toolbox.dm_id, ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        Assert.True(In(w, () => toolbox.loc == me), "the toolbox is in hand");
        Assert.True(In(w, () => Dm.length(GET_ATOM_FINGERPRINTS(toolbox))) > 0, "the pickup left the player's fingerprints");
        using (w.World.Enter())
            me.set_combat_mode(Dm.TRUE);
        int blows = 0;
        while (blows < 30 && In(w, () => GET_ATOM_BLOOD_DNA_LENGTH(toolbox)) == 0)
        {
            Assert.True(p.Session.Click(victim.dm_id, ClickButton.Left, ClickModifiers.None));
            w.Run(Second); // CLICK_CD_MELEE is 0.8 s
            blows++;
        }
        _output.WriteLine($"{blows} blows; the victim's brute {In(w, () => victim.get_brute_loss())}");
        Assert.True(In(w, () => GET_ATOM_BLOOD_DNA_LENGTH(toolbox)) > 0, "a blow bloodied the toolbox");
        using (w.World.Enter())
            Waiting.NoSleep(me.dropItemToGround(toolbox));
        w.Run(Second);
        List<(string Icon, string State, float Layer)> toolboxLook = Looks(replica, toolbox.dm_id);
        _output.WriteLine("the dropped toolbox: " + string.Join(", ", toolboxLook.Select(l => $"{l.Icon}:{l.State}")));
        Assert.Contains(toolboxLook, l => l.State == "itemblood");
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
