using Anomaly.Client.Shared.Icons;
using Anomaly.Client.Shared.World;

namespace Anomaly.Server.Tests.Client.Render;

/// <summary>M4v slice 2: a moving entity's flick at the entity layer's render tick, and the animated turf pictures' layers.</summary>
public class FlickPlaybackTests
{
    private const int Tps = 20;

    [Fact]
    public void A_flick_plays_from_the_tick_it_started_in_for_its_length()
    {
        // vent_out-shutdown's 1.45 s (AnimationTimelineTests) from tick 100: the render tick lags the latest one
        Assert.False(FlickPlayback.TryAge(100, 99.5, Tps, 1.45, out _)); // not yet
        Assert.True(FlickPlayback.TryAge(100, 100, Tps, 1.45, out double start));
        Assert.Equal(0, start, 6);
        Assert.True(FlickPlayback.TryAge(100, 110.5, Tps, 1.45, out double half));
        Assert.Equal(0.525, half, 6);
        Assert.False(FlickPlayback.TryAge(100, 129, Tps, 1.45, out _)); // over: the own picture is back
        Assert.False(FlickPlayback.TryAge(100, 4000, Tps, 1.45, out _)); // an old flick seen with the entity is not played
        Assert.False(FlickPlayback.TryAge(100, 101, Tps, 0, out _)); // the length is not known yet (an icon still loading)
        Assert.False(FlickPlayback.TryAge(100, double.NaN, Tps, 1.45, out _));
    }

    [Fact]
    public void A_flicked_still_state_shows_for_one_tick_of_byond()
    {
        Assert.Equal(0.1, FlickPlayback.LengthOf(null), 6);
        Assert.Equal(0.1, FlickPlayback.LengthOf(new[] { AnimationTimelineTests.State(1, null) }), 6);
        // a looping state flicked plays one round: secbot-c's kind, four frames of 0.2 s
        Assert.Equal(0.8, FlickPlayback.LengthOf(new[] { AnimationTimelineTests.State(4, new[] { 2.0, 2, 2, 2 }) }), 6);
    }

    [Fact]
    public void Turf_frames_move_only_the_pictures_whose_frame_changed()
    {
        var frames = new TurfFrames();
        // the recharge floor: two frames of 0.6 s; the water: 1, 2, 3, 2, 1, 2, 3, 2 ds
        int recharge = frames.Add(AnimationTimeline.Of(new[] { AnimationTimelineTests.State(2, new[] { 6.0, 6 }) }, once: false), new[] { 4, 5 });
        int water = frames.Add(AnimationTimeline.Of(new[] { AnimationTimelineTests.State(8, new[] { 1.0, 2, 3, 2, 1, 2, 3, 2 }) }, once: false), Enumerable.Range(6, 8).ToArray());
        Assert.Equal((4, 6), (frames.LayerNow(recharge), frames.LayerNow(water)));
        var changed = new List<int>();
        frames.Step(0.05, changed);
        Assert.Empty(changed);
        frames.Step(0.15, changed);
        Assert.Equal(new[] { water }, changed);
        Assert.Equal(7, frames.LayerNow(water));
        changed.Clear();
        frames.Step(0.65, changed);
        Assert.Equal(new[] { recharge, water }, changed);
        Assert.Equal((5, 9), (frames.LayerNow(recharge), frames.LayerNow(water))); // 0.65 s: the water's fourth frame (0.6 to 0.8 s)
        changed.Clear();
        frames.Step(1.65, changed); // both loop: the recharge floor's second round (1.2 to 1.8 s: frame 0), the water's (1.6 s: frame 0)
        Assert.Equal((4, 6), (frames.LayerNow(recharge), frames.LayerNow(water)));
        Assert.Throws<ArgumentException>(() => frames.Add(AnimationTimeline.Of(new[] { AnimationTimelineTests.State(2, new[] { 6.0, 6 }) }, once: false), new[] { 1 }));
    }
}
