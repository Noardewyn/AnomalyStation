using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Icons;
using Anomaly.Client.Shared.Maptext;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// Gaps W1 (protocol version 74) through the real server with a headless client: tg's evac status display (status_display.dm) shows the
/// station logo, then the two lines a communications console posts on the status display frequency as world maptext the client draws: the short
/// line centred, the long one scrolled through the display's screen (tg's marquee: its text object's maptext_x animates for ever, masked to the
/// "outline" state); a red alert shows the alert picture for 30 seconds and goes back to the logo. (tg's revert from an alert to a message
/// calls set_messages with the lines it already shows, which draws nothing: the alert picture stays. That is tg's and is not tested.)
/// </summary>
public class StatusDisplayGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;

    private sealed class Tables(ReplicaWorld replica) : IAppearanceSource
    {
        public bool TryGetAppearance(TableRef reference, out AppearanceEntry entry) => replica.TryGetAppearance(reference, out entry);
        public string ResolveString(TableRef reference) => replica.ResolveString(reference);
        public string ResolveResource(TableRef reference) => replica.ResolveResource(reference);
    }

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    // The icon states the entity's overlays show, as the client's model rules read them.
    private static List<string> OverlayStates(ReplicaWorld replica, ReplicaEntity e)
    {
        var states = new List<string>();
        if (replica.TryGetAppearance(e.State.Appearance, out AppearanceEntry root))
            foreach (TableRef o in root.Overlays)
                if (replica.TryGetAppearance(o, out AppearanceEntry overlay) && replica.ResolveString(overlay.IconState) is string s)
                    states.Add(s);
        return states;
    }

    private static void Post(RealHeadlessWorld w, DmList data)
    {
        using (w.World.Enter())
            SSradio.return_frequency(FREQ_STATUS_DISPLAYS).post_signal(null, Dm.@new<datum_signal>(data));
    }

    [Fact]
    public void An_evac_display_shows_the_logo_a_commands_message_with_a_scrolling_line_and_the_red_alert()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 21, TimeoutSteps = 8000 }, strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        mob_living me = w.MobOf(alice);
        w.Run(10);
        int bootRuntimes = w.World.RuntimeErrorCount;
        obj_machinery_status_display_evac display;
        using (w.World.Enter())
            display = Dm.@new<obj_machinery_status_display_evac>(get_step(me, 4));
        w.Run(Second);
        ReplicaWorld replica = alice.Session.Replica;
        var tables = new Tables(replica);
        ReplicaEntity Entity() => replica.Entities.Single(x => x.Id == display.dm_id);
        output.WriteLine($"machine_stat {In(w, () => display.machine_stat)}, overlays: {string.Join(",", OverlayStates(replica, Entity()))}");
        Assert.Equal(0f, In(w, () => Dm.num(display.machine_stat)));

        // the round start's logo (tg's evac display starts on its "default" picture)
        Assert.Contains("default", OverlayStates(replica, Entity()));
        Assert.Empty(new MaptextCollector().Collect(tables, Entity().State.Appearance, rootOffset: false));

        // a red alert interrupts for 30 seconds (DISPLAY_PRIORITY_ALERT_TEMP), then the logo is back
        using (w.World.Enter())
            SSsecurity_level.set_level(SEC_LEVEL_RED, 0);
        w.Run(Second);
        Assert.Contains("redalert", OverlayStates(replica, Entity()));
        w.Run(30 * Second);
        Assert.Contains("default", OverlayStates(replica, Entity()));

        // the communications console's message (communications.dm post_status: "message", top_text, bottom_text)
        const string Long = "ALL CREW REPORT TO THE BRIDGE";
        Post(w, Dm.list(Dm.kv("command", "message"), Dm.kv("top_text", "eta"), Dm.kv("bottom_text", Long.ToLowerInvariant())));
        w.Run(Second);
        ReplicaEntity e = Entity();
        Assert.DoesNotContain("default", OverlayStates(replica, e));
        MaptextItem[] items = new MaptextCollector().Collect(tables, e.State.Appearance, rootOffset: false);
        output.WriteLine(string.Join("\n", items.Select(i => $"{i.Text} w{i.Width} at {i.X},{i.Y} x{i.MaptextX} mask {i.Mask}")));
        Assert.Equal(2, items.Length);
        MaptextItem top = Assert.Single(items, i => i.Text.Contains(">ETA</div>", StringComparison.Ordinal)); // tg uppertexts the lines
        MaptextItem bottom = Assert.Single(items, i => i.Text.Contains(Long + "    " + Long, StringComparison.Ordinal)); // the marquee's copies
        Assert.Contains("text-align:center", top.Text);
        Assert.Contains("TinyUnicode", top.Text);
        Assert.Null(top.Mask);
        Assert.Equal(1f, top.X); // LINE1_X, from the display's own icon
        Assert.Equal(-4f, top.Y); // LINE1_Y; the text object's pixel_y -32 and pixel_z 32 cancel
        Assert.Equal(-11f, bottom.Y); // LINE2_Y
        Assert.Equal(new MaptextMask("icons/obj/machines/status_display.dmi", "outline", 0f, 0f), bottom.Mask); // the text object's own icon square
        Assert.Equal(22f, bottom.MaptextX); // MAX_STATIC_WIDTH: where the chain ends

        // the long line scrolls left, for ever, at tg's 0.04 s a pixel
        AnimationTrack scroll = Assert.Single(e.Animation.Tracks, t => t.Child == bottom.Source);
        Assert.Equal(0, scroll.Loop);
        Assert.True(scroll.Steps[0].Vars.Has(AnimatedVar.MaptextX));
        Assert.True(AnimationPlayback.TryMaptextX(e.Animation, bottom.Source, scroll.StartTick + Second / 4, Second, bottom.MaptextX, out double early));
        Assert.True(AnimationPlayback.TryMaptextX(e.Animation, bottom.Source, scroll.StartTick + Second / 2, Second, bottom.MaptextX, out double later));
        output.WriteLine($"maptext_x {early} then {later}");
        Assert.InRange(later - early, -6.5, -6.0); // tg's 0.04 s a pixel: about 6.25 pixels a quarter second
        Assert.False(AnimationPlayback.TryMaptextX(e.Animation, top.Source, scroll.StartTick + Second, Second, top.MaptextX, out _));

        // where the client draws them: the short line in its box, the long one inside the screen's mask
        Assert.True(WorldMaptextPlacement.TryRegion(top, null, out WorldTextRegion topRegion));
        Assert.Equal((1f, -4f, 32f, 32f), (topRegion.Left, topRegion.Bottom, topRegion.Width, topRegion.Height));
        // the outline state's opaque bounds (x 3 to 29, y 8 to 24) inside the box's rows (y -11 to 21); the box's left 19 pixels in
        Assert.True(WorldMaptextPlacement.TryRegion(bottom, new MaskBounds(3, 8, 29, 24), out WorldTextRegion masked));
        Assert.Equal((3f, 8f, 26f, 13f, -19f, 0f), (masked.Left, masked.Bottom, masked.Width, masked.Height, masked.BoxLeft, masked.BoxTop));

        Assert.Contains("The display says:", In(w, () => string.Join("\n", Dm.each<object>(Waiting.NoSleep(display.examine(me)) as DmList).Select(Dm.str))));

        output.WriteLine(w.Err.ToString());
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }

    [Fact]
    public void Tg_tiny_unicode_metrics_measure_a_space_and_two_marks()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 22, TimeoutSteps = 2000 }, strict: false);
        // fonts_datum.dm get_metrics: a space is 0 + 1 + 4 less its trailing 4; "!!" is 1 + 1 twice less the last 1
        Assert.Equal(1f, In(w, () => Dm.num(GLOB.status_font.get_metrics(" "))));
        Assert.Equal(3f, In(w, () => Dm.num(GLOB.status_font.get_metrics("!!"))));
        Assert.Equal(0f, In(w, () => Dm.num(GLOB.status_font.get_metrics(""))));
    }
}
