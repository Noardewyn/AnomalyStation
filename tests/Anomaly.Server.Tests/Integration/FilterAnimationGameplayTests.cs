using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Icons;
using Anomaly.Client.Shared.World;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Defines;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// M6 slice 7b (D156, protocol version 26) through the real server with a headless client: tg's animate() of a filter reaches the
/// client. The player's wounded arm pulses on the HUD's health doll (screen_objects.dm:1023: the limb's outline alpha 0 to 200 and back,
/// for ever), a chain of the doll's vis_contents child carried in the doll's screen entry; a mob in high gravity gets tg's motion blur
/// whose y moves 0 to 1 and back (life.dm:129), a chain in its entity's animation. The client's playback draws both at their values.
/// </summary>
public class FilterAnimationGameplayTests
{
    private const int Second = 20;

    private readonly ITestOutputHelper _output;

    public FilterAnimationGameplayTests(ITestOutputHelper output) => _output = output;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private sealed class Tables(ReplicaWorld replica) : IAppearanceSource
    {
        public bool TryGetAppearance(TableRef reference, out AppearanceEntry entry) => replica.TryGetAppearance(reference, out entry);
        public string ResolveString(TableRef reference) => replica.ResolveString(reference);
        public string ResolveResource(TableRef reference) => replica.ResolveResource(reference);
    }

    [Fact]
    public void The_dolls_wound_outline_pulses_and_high_gravity_blurs_a_mob()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 83, TimeoutSteps = 8000 }, strict: false);
        RealHeadlessWorld.Player p = w.Join("Alice");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        var tables = new Tables(replica);
        int bootRuntimes = w.World.RuntimeErrorCount;
        w.Run(Second);

        // the doll: a wounded arm's limb starts its pulse
        In(w, () => Waiting.NoSleep(me.cause_wound_of_type_and_severity(WOUND_SLASH, (obj_item_bodypart)me.get_bodypart(BODY_ZONE_L_ARM), WOUND_SEVERITY_MODERATE)));
        w.Run(Second);
        ScreenEntry doll = replica.Screen.Values.FirstOrDefault(e => e.Animation?.Tracks.Any(t => !t.Child.IsNone) == true);
        Assert.True(doll.Animation != null, "a screen entry carries its child's filter chain");
        AnimationTrack pulse = doll.Animation.Tracks.First(t => !t.Child.IsNone);
        Assert.Equal(0, pulse.Loop); // for ever
        Assert.Equal(1, pulse.Filter);
        Assert.Equal(0f, pulse.FilterStart.Alpha);
        Assert.Equal(new[] { 200f, 0f }, pulse.Steps.Select(s => s.Filter.Alpha));
        Assert.Equal(new[] { 15f, 15f }, pulse.Steps.Select(s => s.Time));
        Assert.True(replica.TryGetAppearance(pulse.Child, out AppearanceEntry limb));
        FilterEntry outline = Assert.Single(limb.Filters);
        Assert.Equal(FilterKind.Outline, outline.Kind);
        Assert.Equal(0f, outline.Alpha); // the appearance holds the chain's last value (BYOND's server does)

        Assert.True(FilterPlayback.TryEvaluate(tables, doll.Appearance, doll.Animation, pulse.StartTick + 30, Second, out var top));
        Assert.Equal(200f, top[limb][0].Alpha, 0.01f);
        Assert.True(FilterPlayback.TryEvaluate(tables, doll.Appearance, doll.Animation, pulse.StartTick + 15, Second, out var half));
        Assert.Equal(100f, half[limb][0].Alpha, 0.01f);
        string root = Anomaly.Server.Tests.M3Maps.I1d.MetaPictureTests.IconRoot();
        if (root != null)
        {
            var compositor = new AppearanceCompositor(new IconLibrary(new DirectoryIconFiles(root)));
            Assert.True(compositor.TryCompose(tables, doll.Appearance, ByondDir.South, 0, out ComposedRaster rest, true, false, null));
            Assert.True(compositor.TryCompose(tables, doll.Appearance, ByondDir.South, 0, out ComposedRaster lit, true, false, top));
            int red = Count(lit.Image, 0xFF0033), before = Count(rest.Image, 0xFF0033);
            _output.WriteLine($"outline pixels at rest {before}, at the pulse's top {red}");
            Assert.True(red > before + 4, "the pulse's top draws the red outline");
        }

        // high gravity: tg's gravity_animate on a mob beside the player
        mob_living_carbon_human bob = In(w, () => Dm.@new<mob_living_carbon_human>(Builtins.get_step(me, Dm.EAST)));
        w.Run(Second);
        In(w, () =>
        {
            bob.gravity_animate();
            return 0;
        });
        w.Run(Second);
        Assert.True(replica.TryGet(bob.dm_id, out ReplicaEntity seen), "the client sees Bob");
        Assert.True(replica.TryGetAppearance(seen.State.Appearance, out AppearanceEntry bobLook));
        int blurAt = bobLook.Filters.FindIndex(f => f.Kind == FilterKind.MotionBlur) + 1;
        Assert.True(blurAt > 0, "the motion blur reaches the client");
        AnimationTrack gravity = Assert.Single(seen.Animation.Tracks, t => t.Filter == blurAt);
        Assert.True(gravity.Child.IsNone);
        Assert.Equal(new[] { 1f, 0f }, gravity.Steps.Select(s => s.Filter.Y));
        Assert.True(FilterPlayback.TryEvaluate(tables, seen.State.Appearance, seen.Animation, gravity.StartTick + Second, Second, out var blurred)); // time = 10 ds
        Assert.Equal(1f, blurred[bobLook][blurAt - 1].Y, 0.01f);
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }

    private static int Count(Raster image, uint rgb)
    {
        int n = 0;
        for (int y = 0; y < image.Height; y++)
            for (int x = 0; x < image.Width; x++)
            {
                uint c = image.Get(x, y);
                if ((c & 255) > 100 && (c >> 8) == rgb) n++;
            }
        return n;
    }
}
