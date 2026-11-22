using Anomaly.Client.Shared.Icons;

namespace Anomaly.Server.Tests.Client.Render;

/// <summary>D142: the pictures of an icon state animation in the 3D world (a vent's fan, its flicked shutdown).</summary>
public class AnimationTimelineTests
{
    [Fact]
    public void NothingAnimatedHasNoTimeline()
    {
        Assert.Null(AnimationTimeline.Of(null, once: false));
        Assert.Null(AnimationTimeline.Of(new[] { State(1, null) }, once: false));
    }

    [Fact]
    public void AVentsFanLoopsThroughItsFourFrames()
    {
        // tg's vent_out (icons/obj/machines/atmospherics/unary_devices.dmi): four frames of 0.8 ds
        AnimationTimeline t = AnimationTimeline.Of(new[] { State(4, new[] { 0.8, 0.8, 0.8, 0.8 }) }, once: false);
        Assert.True(t.Loops);
        Assert.Equal(new[] { 0, 0.08, 0.16, 0.24 }, t.Starts);
        Assert.Equal(0.32, t.Length, 6);
        Assert.Equal(0, t.FrameAt(0.01));
        Assert.Equal(2, t.FrameAt(0.17));
        Assert.Equal(1, t.FrameAt(0.32 + 0.09)); // the second loop
    }

    [Fact]
    public void AFlickPlaysOnceAndEndsOnItsLastPicture()
    {
        // vent_out-shutdown: sixteen frames, 14.5 ds in all
        double[] delays = { 1, 1, 1, 1, 0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.8, 1, 1.3, 1.5, 1.8, 2 };
        AnimationTimeline t = AnimationTimeline.Of(new[] { State(16, delays) }, once: true);
        Assert.False(t.Loops);
        Assert.Equal(16, t.Count);
        Assert.Equal(1.45, t.Length, 6);
        Assert.Equal(15, t.FrameAt(1.44));
        Assert.Equal(15, t.FrameAt(9));
        // a still state flicked shows for one tick
        AnimationTimeline still = AnimationTimeline.Of(new[] { State(1, null) }, once: true);
        Assert.Equal(1, still.Count);
        Assert.Equal(0.1, still.Length, 6);
    }

    [Fact]
    public void TwoStatesShareTheirCommonLoop()
    {
        // 2 frames of 1 ds and 3 frames of 1 ds: the pictures change at every decisecond and repeat after 6
        AnimationTimeline t = AnimationTimeline.Of(new[] { State(2, new[] { 1.0, 1 }), State(3, new[] { 1.0, 1, 1 }) }, once: false);
        Assert.Equal(0.6, t.Length, 6);
        Assert.Equal(6, t.Count);
    }

    [Fact]
    public void ALoopCountEndsOnTheLastFrameAndALongAnimationIsThinned()
    {
        AnimationTimeline t = AnimationTimeline.Of(new[] { State(3, new[] { 1.0, 1, 1 }, loop: 2) }, once: false);
        Assert.False(t.Loops);
        Assert.Equal(0.6, t.Length, 6);
        Assert.Equal(5, t.FrameAt(5));
        AnimationTimeline many = AnimationTimeline.Of(new[] { State(100, Enumerable.Repeat(1.0, 100).ToArray()) }, once: false);
        Assert.Equal(AnimationTimeline.MaxFrames, many.Count);
    }

    internal static IIconState State(int frames, double[] delays, int loop = 0) => new Fake(frames, delays, loop);

    internal sealed class Fake(int frames, double[] delays, int loop) : IIconState
    {
        public int Dirs => 1;
        public int Frames => frames;
        public int Width => 32;
        public int Height => 32;
        public bool IsAnimated => frames > 1;
        public double[] Delays => delays;
        public int Loop => loop;
        public bool Rewind => false;
        public int FrameAt(double seconds) => 0;
        public Raster Get(int dirIndex, int frame) => null;
    }
}
