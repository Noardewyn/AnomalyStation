using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Icons;
using Anomaly.Client.Shared.World;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// The space play test (2026-10-15), the fire extinguisher through the real server on MetaStation and a headless client:
/// <list type="bullet">
/// <item><description>On the station a spray down the arrivals corridor sends tg's five water puffs
/// (<c>/obj/effect/particle_effect/water/extinguisher</c>, icons/effects/effects.dmi "extinguish") gliding to the clicked turf; the client
/// draws them: the entity layer takes them (never static props), their picture composes and is see-through (alpha 11 to 153: the opaque
/// pass's alpha scissor of 0.5 cut it away whole, which is why "nothing visible showed it works"), and they show once they are out of the
/// own eye's tile (DV46).</description></item>
/// <item><description>In space a weightless player clicks a space turf (the client's far pick, <see cref="WorldPicker.PickInView"/>: the
/// 4-tile pick found nothing over the starfield and no click was sent) and sprays toward it: tg's <c>newtonian_move</c> pushes the user the
/// other way (extinguisher.dm:310), the drift carries the player off and the client's own view follows it.</description></item>
/// </list>
/// </summary>
public class ExtinguisherSprayTests
{
    private const int Second = 20;
    private const string Puff = "/obj/effect/particle_effect/water/extinguisher";
    private const string Spray = "sound/effects/extinguish.ogg";
    private readonly ITestOutputHelper _output;

    public ExtinguisherSprayTests(ITestOutputHelper output) => _output = output;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static obj_item_extinguisher Ready(RealHeadlessWorld w, RealHeadlessWorld.Player p, mob_living_carbon_human me)
    {
        w.Host.EnqueueConsoleLine("give Assistant /obj/item/extinguisher");
        w.Run(5);
        var extinguisher = In(w, () => me.get_active_held_item() as obj_item_extinguisher);
        Assert.NotNull(extinguisher);
        Assert.Equal(1f, In(w, () => extinguisher.safety)); // tg's extinguisher comes with the safety on (extinguisher.dm:13)
        p.Session.Keybinding(KeybindingCode.ActivateInhand); // Z: attack_self takes the safety off (extinguisher.dm:228)
        w.Run(5);
        Assert.Equal(0f, In(w, () => extinguisher.safety));
        return extinguisher;
    }

    private static string IconRoot()
    {
        string root = Anomaly.Server.Tests.M3Maps.I1d.MetaPictureTests.IconRoot();
        if (root != null)
            return root;
        root = Environment.GetEnvironmentVariable("ANOMALY_ICON_ROOT");
        return root != null && System.IO.Directory.Exists(System.IO.Path.Combine(root, "icons")) ? root : null;
    }

    [MetaServerFact]
    public void Down_a_corridor_the_client_draws_the_water_puffs_gliding_to_the_clicked_turf()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 5, TimeoutSteps = 6000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Assistant");
        var me = (mob_living_carbon_human)w.MobOf(p);
        int z = In(w, () => (int)me.z);
        int bootRuntimes = w.World.RuntimeErrorCount;
        In(w, () => Waiting.NoSleep(me.forceMove(locate(46, 122, z)))); // the arrivals corridor, open floor to the east
        w.Run(Second);
        obj_item_extinguisher extinguisher = Ready(w, p, me);
        ReplicaWorld replica = p.Session.Replica;
        var heard = new List<SoundEvent>();
        replica.SoundsReceived += events => heard.AddRange(events);

        Assert.True(p.Session.Click(new ClickTarget(TargetKind.Turf, 0, 49, 122, z, 16, 16), ClickButton.Left, ClickModifiers.None));
        var puffs = new Dictionary<long, (float X0, float Far, float Fade)>();
        long lastSeen = -1;
        for (int t = 0; t < 2 * Second; t++)
        {
            w.Run(1);
            (float ex, float ey, _) = p.Session.Prediction.RenderPosition();
            foreach (ReplicaEntity e in replica.Entities.Where(e => e.TypePath == Puff))
            {
                Assert.False(StaticPropRule.IsCandidate(e.TypePath, e.HasPos)); // drawn by the entity layer from its first tick
                Assert.True(p.Session.Poses.TryGetPose(e, out RenderPose pose));
                float fade = NearEyeFade.Alpha(pose.X - ex, pose.Y - ey);
                (float x0, float far, float shown) = puffs.TryGetValue(e.Id, out var seen) ? seen : (pose.X, pose.X, 0f);
                puffs[e.Id] = (x0, Math.Max(far, pose.X), Math.Max(shown, fade));
                lastSeen = t;
            }
        }
        _output.WriteLine($"puffs seen {puffs.Count}: " + string.Join(", ", puffs.Values.Select(v => $"{v.X0:0.00}->{v.Far:0.00} shown {v.Fade:0.00}")) + $"; the last seen at tick {lastSeen}");
        Assert.Equal(5, puffs.Count); // five puffs a spray (extinguisher.dm:322)
        Assert.All(puffs.Values, v => Assert.True(v.Far - v.X0 >= 1.5f, $"a puff glides east toward the clicked turf: {v.X0} -> {v.Far}"));
        Assert.All(puffs.Values, v => Assert.Equal(1f, v.Fade)); // whole once a tile from the eye
        Assert.True(lastSeen < 2 * Second - 1, "the puffs end (end_life, effects_water.dm:57)");
        Assert.Single(heard, h => h.Kind == SoundKind.Play && h.File != null && (h.File ?? "").StartsWith(Spray, StringComparison.Ordinal));
        Assert.Equal(95f, In(w, () => ((datum_reagents)extinguisher.reagents).total_volume), 0.01f);

        // The client's picture of a puff, with the converted icons: tg's "extinguish" state composes and is see-through, so it is blended.
        string root = IconRoot();
        if (root == null)
        {
            _output.WriteLine("no converted icons: the picture checks are skipped");
        }
        else
        {
            Assert.True(p.Session.Click(new ClickTarget(TargetKind.Turf, 0, 49, 122, z, 16, 16), ClickButton.Left, ClickModifiers.None));
            w.Run(Second / 2);
            ReplicaEntity puff = replica.Entities.First(e => e.TypePath == Puff);
            var compositor = new AppearanceCompositor(new IconLibrary(new DirectoryIconFiles(root)));
            var tables = new Tables(replica);
            Assert.True(replica.TryGetAppearance(puff.State.Appearance, out AppearanceEntry look));
            Assert.Equal("extinguish", replica.ResolveString(look.IconState));
            Assert.True(compositor.TryCompose(tables, puff.State.Appearance, puff.State.Dir, 0, out ComposedRaster picture));
            Assert.True(picture.Image.IsTranslucent(), "the puff's picture is see-through");
            int drawn = 0, cut = 0;
            for (int i = 3; i < picture.Image.Pixels.Length; i += 4)
            {
                if (picture.Image.Pixels[i] == 0)
                    continue;
                drawn++;
                if (picture.Image.Pixels[i] < 128)
                    cut++;
            }
            _output.WriteLine($"the puff's picture: {drawn} drawn pixels, {cut} of them under the opaque pass's scissor (alpha 128)");
            Assert.True(cut > drawn * 0.9, "the scissor would have cut nearly all of it");
        }
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }

    [MetaServerFact]
    public void In_space_a_weightless_player_clicks_a_space_turf_sprays_and_drifts_the_other_way()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 7, TimeoutSteps = 6000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Assistant");
        var me = (mob_living_carbon_human)w.MobOf(p);
        int z = In(w, () => (int)me.z);
        int bootRuntimes = w.World.RuntimeErrorCount;
        w.Host.EnqueueConsoleLine("godmode Assistant"); // no suit: space's cold and vacuum are not this test's
        w.Run(2);
        obj_item_extinguisher extinguisher = Ready(w, p, me);
        In(w, () => Waiting.NoSleep(me.forceMove(locate(39, 116, z)))); // outside the Auxiliary Airlock (the jetpack tests' spot)
        w.Run(Second);
        Assert.Equal(MotionDriver.ZeroG, w.ServerState(p).Driver);
        Assert.Null(In(w, () => me.drift_handler));
        Assert.True(In(w, () => locate(42, 116, z) is turf_open_space), "the clicked turf is space");
        ReplicaWorld replica = p.Session.Replica;
        var heard = new List<SoundEvent>();
        replica.SoundsReceived += events => heard.AddRange(events);
        (float startX, _, _) = p.Session.Prediction.RenderPosition();
        long correctionsBefore = p.Counters.Corrections, snapsBefore = p.Counters.Snaps; // the test's own teleport into space snapped the view

        // The click the client's far pick makes over the starfield: the space turf three tiles east.
        Assert.True(p.Session.Click(new ClickTarget(TargetKind.Turf, 0, 42, 116, z, 16, 16), ClickButton.Left, ClickModifiers.None));
        w.Run(5);
        Assert.Single(heard, h => h.Kind == SoundKind.Play && h.File != null && (h.File ?? "").StartsWith(Spray, StringComparison.Ordinal));
        Assert.NotNull(In(w, () => me.drift_handler)); // newtonian_move(dir2angle(WEST)): the push the other way
        Assert.Equal(270f, In(w, () => Dm.num(me.drift_handler.drifting_loop.angle)));
        w.Run(2 * Second);
        (int X, int Y) at = In(w, () => ((int)me.x, (int)me.y));
        (float drawnX, float drawnY, _) = p.Session.Prediction.RenderPosition();
        _output.WriteLine($"sprayed east from (39,116): drifted to {at}, drawn at ({drawnX:0.00},{drawnY:0.00}), {In(w, () => ((datum_reagents)extinguisher.reagents).total_volume)} units left");
        Assert.True(at.X <= 39 - 2, $"the player drifted west: {at}");
        Assert.Equal(116, at.Y);
        Assert.True(drawnX < startX - 1.5f, "the own view follows the drift");
        Assert.Equal(116f, drawnY, 1);
        Assert.Equal(95f, In(w, () => ((datum_reagents)extinguisher.reagents).total_volume), 0.01f);
        Assert.True(p.Counters.Corrections - correctionsBefore <= 2, $"{p.Counters.Corrections - correctionsBefore} corrections"); // DV69: the push the client could not foresee
        Assert.Equal(snapsBefore, p.Counters.Snaps);
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }

    // The client's ReplicaTables (godot/scripts/Render, not in this test project): the replica's tables for the compositor.
    private sealed class Tables(ReplicaWorld replica) : IAppearanceSource
    {
        public bool TryGetAppearance(TableRef reference, out AppearanceEntry entry) => replica.TryGetAppearance(reference, out entry);
        public string ResolveString(TableRef reference) => replica.ResolveString(reference);
        public string ResolveResource(TableRef reference) => replica.ResolveResource(reference);
    }
}
