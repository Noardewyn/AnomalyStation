using Anomaly.Client.Shared.Icons;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Render;

/// <summary>M4v slice 1 (protocol version 9): the client's playback of BYOND's animate() chains (AnimationPlayback), engine-free.</summary>
public class AnimationPlaybackTests
{
    private const int Tps = 20; // 2 ticks a decisecond

    private static AnimatedVars Px(float x, float y) => new() { Mask = AnimatedVar.PixelX | AnimatedVar.PixelY, PixelX = x, PixelY = y };

    private static AnimatedVars Z(float z) => new() { Mask = AnimatedVar.PixelZ, PixelZ = z };

    private static AnimatedVars W(float w) => new() { Mask = AnimatedVar.PixelW, PixelW = w };

    private static AnimatedVars Turn(double degrees)
    {
        double r = degrees * Math.PI / 180;
        return new AnimatedVars { Mask = AnimatedVar.Transform, A = (float)Math.Cos(r), B = (float)Math.Sin(r), D = (float)-Math.Sin(r), E = (float)Math.Cos(r) };
    }

    private static EntityAnimation One(AnimationTrack track) => new() { Tracks = new[] { track } };

    private static AnimatedLook At(EntityAnimation a, double tick, AnimatedBase? appearance = null)
    {
        Assert.True(AnimationPlayback.TryEvaluate(a, tick, Tps, appearance ?? AnimatedBase.Default, out AnimatedLook look));
        return look;
    }

    [Fact]
    public void A_dropped_item_fades_in_and_slides_to_its_place_by_the_cubic_curve()
    {
        // tg's do_drop_animation: from 10 pixels up and transparent to its place over 3 ds, CUBIC_EASING (in and out)
        var start = new AnimatedVars { Mask = AnimatedVar.PixelY | AnimatedVar.Alpha, PixelY = 10, Alpha = 0 };
        var end = new AnimatedVars { Mask = AnimatedVar.PixelY | AnimatedVar.Alpha, PixelY = 0, Alpha = 255 };
        EntityAnimation a = One(new AnimationTrack { StartTick = 100, Start = start, Steps = new[] { new AnimationStep(3, 0, AnimationEasing.Cubic, 0, end) } });
        AnimatedLook first = At(a, 100);
        Assert.Equal(10, first.PixelY, 6);
        Assert.Equal(0, first.Alpha, 6);
        AnimatedLook half = At(a, 103); // 1.5 ds: half way, where in-and-out is one half
        Assert.Equal(5, half.PixelY, 6);
        Assert.Equal(0.5, half.Alpha, 3);
        AnimatedLook early = At(a, 100.6); // 0.3 ds: a tenth of the time, the cubic's start is slow
        Assert.True(early.PixelY > 9.9);
        Assert.Equal(10, At(a, 95).PixelY, 6); // before it starts: the start values
        Assert.False(AnimationPlayback.TryEvaluate(a, 106, Tps, AnimatedBase.Default, out AnimatedLook done)); // ended: the appearance
        Assert.True(done.IsNone);
    }

    [Fact]
    public void The_float_loops_for_ever_by_its_relative_steps()
    {
        // DO_FLOATING_ANIM: pixel_z +2 over 1 s, then -2 over 1 s, ANIMATION_RELATIVE, loop -1 (0 on the wire)
        EntityAnimation a = One(new AnimationTrack
        {
            StartTick = 0, Loop = 0, Start = Z(0),
            Steps = new[] { new AnimationStep(10, 0, 0, AnimationStepFlags.Relative, Z(2)), new AnimationStep(10, 0, 0, AnimationStepFlags.Relative, Z(-2)) },
        });
        Assert.Equal(1, At(a, 10).PixelZ, 6);
        Assert.Equal(2, At(a, 20).PixelZ, 6);
        Assert.Equal(1, At(a, 30).PixelZ, 6);
        Assert.Equal(1, At(a, 40 * 1000 + 10).PixelZ, 6); // a thousand rounds on it does not drift
    }

    [Fact]
    public void A_relative_chain_that_loops_keeps_going_from_where_it_ended()
    {
        EntityAnimation a = One(new AnimationTrack { StartTick = 0, Loop = 3, Start = W(0), Steps = new[] { new AnimationStep(10, 0, 0, AnimationStepFlags.Relative, W(1)) } });
        // the appearance holds one round's end (the server applied the step once): pixel_w 1
        var appearance = AnimatedBase.Default with { PixelW = 1 };
        Assert.Equal(-0.5, At(a, 10, appearance).PixelW, 6); // shown 0.5 over a picture composed at 1
        Assert.Equal(1.5 - 1, At(a, 30, appearance).PixelW, 6); // the second round, half way: 1.5
        Assert.Equal(2.5 - 1, At(a, 50, appearance).PixelW, 6); // the third: 2.5
        Assert.False(AnimationPlayback.TryEvaluate(a, 60, Tps, appearance, out _));
    }

    [Fact]
    public void A_staggered_mob_sways_over_its_float()
    {
        // do_stagger_animation: a parallel relative chain of pixel_w over the main float chain of pixel_z; each adds its change
        AnimationTrack floating = new()
        {
            StartTick = 0, Loop = 0, Start = Z(0),
            Steps = new[] { new AnimationStep(10, 0, 0, AnimationStepFlags.Relative, Z(2)), new AnimationStep(10, 0, 0, AnimationStepFlags.Relative, Z(-2)) },
        };
        AnimationTrack sway = new()
        {
            StartTick = 20, Parallel = true, Start = W(0),
            Steps = new[]
            {
                new AnimationStep(2, 0, 0, AnimationStepFlags.Relative, W(3)), new AnimationStep(4, 0, 0, AnimationStepFlags.Relative, W(-6)),
                new AnimationStep(2, 0, 0, AnimationStepFlags.Relative, W(3)),
            },
        };
        var a = new EntityAnimation { Tracks = new[] { floating, sway } };
        AnimatedLook look = At(a, 24); // the float at 1.2 s: 1.6 up; the sway at 0.2 s: 3 to the side
        Assert.Equal(1.6, look.PixelZ, 6);
        Assert.Equal(3, look.PixelW, 6);
        Assert.Equal(0, At(a, 28).PixelW, 6); // half the -6
        Assert.Equal(-3, At(a, 32).PixelW, 6);
        Assert.Equal(0, At(a, 40).PixelW, 6); // the sway is over; the float goes on
        Assert.True(AnimationPlayback.TryEvaluate(a, 40, Tps, AnimatedBase.Default, out _));
    }

    [Fact]
    public void A_chain_before_its_start_shows_its_start_values()
    {
        // BYOND draws the vars before the animate() call until the chain starts; the appearance already holds its end (the live check of
        // 2026-10-14: a fullscreen blinked out for a frame before its fade, the HUD's render tick being behind the chain's start).
        EntityAnimation a = One(new AnimationTrack { StartTick = 50, Start = Px(0, 0), Steps = new[] { new AnimationStep(4, 0, 0, 0, Px(8, 4)) } });
        var appearance = AnimatedBase.Default with { PixelX = 8, PixelY = 4 };
        Assert.True(AnimationPlayback.TryEvaluate(a, 47.5, Tps, appearance, out AnimatedLook early));
        Assert.Equal(-8, early.PixelX, 6);
        Assert.Equal(-4, early.PixelY, 6);

        var fade = One(new AnimationTrack
        {
            StartTick = 50,
            Start = new AnimatedVars { Mask = AnimatedVar.Alpha, Alpha = 255 },
            Steps = new[] { new AnimationStep(10, 0, 0, 0, new AnimatedVars { Mask = AnimatedVar.Alpha, Alpha = 0 }) },
        });
        var transparent = AnimatedBase.Default with { Alpha = 0 };
        Assert.True(AnimationPlayback.TryEvaluate(fade, 49, Tps, transparent, out AnimatedLook opaque, composedAlpha: 255));
        Assert.Equal(1.0, opaque.Alpha, 6); // the start's 255 over the opaque picture: drawn, though the look is no change of that picture
        Assert.True(opaque.IsNone);
    }

    [Fact]
    public void A_delay_holds_the_value_before_the_step_moves()
    {
        EntityAnimation a = One(new AnimationTrack { StartTick = 0, Start = Px(0, 0), Steps = new[] { new AnimationStep(2, 3, 0, 0, Px(8, 4)) } });
        var appearance = AnimatedBase.Default with { PixelX = 8, PixelY = 4 }; // the end, as the server applied it
        Assert.Equal(-8, At(a, 5, appearance).PixelX, 6); // 2.5 ds: still in the delay, at the start
        Assert.Equal(-4, At(a, 8, appearance).PixelX, 6); // 4 ds: half the step
        Assert.Equal(-2, At(a, 8, appearance).PixelY, 6);
    }

    [Fact]
    public void A_transform_turns_through_its_angle_and_a_linear_one_squashes()
    {
        EntityAnimation turned = One(new AnimationTrack { StartTick = 0, Start = Turn(0), Steps = new[] { new AnimationStep(10, 0, 0, 0, Turn(90)) } });
        var appearance = AnimatedBase.Default with { Transform = new Affine(0, 1, 0, -1, 0, 0) }; // composed at the end: turned 90
        Affine half = At(turned, 10, appearance).Transform.Then(appearance.Transform); // the look is applied after the picture's own
        Affine expected = new(Math.Cos(Math.PI / 4), Math.Sin(Math.PI / 4), 0, -Math.Sin(Math.PI / 4), Math.Cos(Math.PI / 4), 0);
        Assert.Equal(expected.A, half.A, 4);
        Assert.Equal(expected.B, half.B, 4);
        Assert.Equal(expected.D, half.D, 4);
        Assert.Equal(expected.E, half.E, 4);

        EntityAnimation linear = One(new AnimationTrack { StartTick = 0, Start = Turn(0), Steps = new[] { new AnimationStep(10, 0, 0, AnimationStepFlags.LinearTransform, Turn(90)) } });
        Affine squashed = At(linear, 10, appearance).Transform.Then(appearance.Transform);
        Assert.Equal(0.5, squashed.A, 4);
        Assert.Equal(0.5, squashed.B, 4);
    }

    [Fact]
    public void The_storage_wobble_squashes_and_comes_back()
    {
        // tg's animate_parent: transform Scale(1.07, 0.9) over 1.5 ds, then back over 2 ds
        var squash = new AnimatedVars { Mask = AnimatedVar.Transform, A = 1.07f, E = 0.9f };
        var rest = new AnimatedVars { Mask = AnimatedVar.Transform, A = 1, E = 1 };
        EntityAnimation a = One(new AnimationTrack { StartTick = 0, Start = rest, Steps = new[] { new AnimationStep(1.5f, 0, 0, 0, squash), new AnimationStep(2, 0, 0, 0, rest) } });
        Affine top = At(a, 3).Transform;
        Assert.Equal(1.07, top.A, 4);
        Assert.Equal(0.9, top.E, 4);
        Assert.Equal(1.035, At(a, 5).Transform.A, 4);
    }

    [Fact]
    public void Colour_and_alpha_scale_the_picture_composed_at_their_end()
    {
        var red = new AnimatedVars { Mask = AnimatedVar.Color, Rgba = 0xFF0000FF };
        var white = new AnimatedVars { Mask = AnimatedVar.Color, Rgba = 0xFFFFFFFF };
        EntityAnimation a = One(new AnimationTrack { StartTick = 0, Start = white, Steps = new[] { new AnimationStep(10, 0, 0, 0, red) } });
        var appearance = AnimatedBase.Default with { Rgba = 0xFF0000FF };
        AnimatedLook look = At(a, 10, appearance);
        Assert.Equal(1, look.Red, 6);
        Assert.Equal(1, look.Green, 6); // the picture's green is 0 (composed red): nothing to scale
        Assert.Equal(1, look.Alpha, 6);
        var fade = new AnimatedVars { Mask = AnimatedVar.Alpha, Alpha = 255 };
        EntityAnimation fadeIn = One(new AnimationTrack { StartTick = 0, Start = fade with { Alpha = 0 }, Steps = new[] { new AnimationStep(10, 0, 0, 0, fade) } });
        Assert.Equal(0.25, At(fadeIn, 5).Alpha, 6);
    }

    [Theory]
    [InlineData(AnimationEasing.Sine)]
    [InlineData(AnimationEasing.Circular)]
    [InlineData(AnimationEasing.Cubic)]
    [InlineData(AnimationEasing.Bounce)]
    [InlineData(AnimationEasing.Elastic)]
    [InlineData(AnimationEasing.Back)]
    [InlineData(AnimationEasing.Quad)]
    public void Every_curve_starts_at_0_and_ends_at_1_in_every_mode(byte curve)
    {
        foreach (byte mode in new byte[] { 0, AnimationEasing.EaseIn, AnimationEasing.EaseOut, AnimationEasing.EaseIn | AnimationEasing.EaseOut })
        {
            Assert.Equal(0, AnimationPlayback.Ease((byte)(curve | mode), 0), 6);
            Assert.Equal(1, AnimationPlayback.Ease((byte)(curve | mode), 1), 6);
        }
        Assert.True(AnimationPlayback.Ease((byte)(curve | AnimationEasing.EaseIn), 0.25) < AnimationPlayback.Ease((byte)(curve | AnimationEasing.EaseOut), 0.25));
    }

    [Fact]
    public void Bounce_and_elastic_ease_out_by_default_and_jump_jumps_at_the_end()
    {
        Assert.Equal(AnimationPlayback.Ease(AnimationEasing.Bounce | AnimationEasing.EaseOut, 0.3), AnimationPlayback.Ease(AnimationEasing.Bounce, 0.3), 9);
        Assert.Equal(AnimationPlayback.Ease(AnimationEasing.Elastic | AnimationEasing.EaseOut, 0.3), AnimationPlayback.Ease(AnimationEasing.Elastic, 0.3), 9);
        Assert.Equal(0.5, AnimationPlayback.Ease(AnimationEasing.Sine, 0.5), 9); // in and out: the middle is half
        Assert.Equal(0.3, AnimationPlayback.Ease(AnimationEasing.Linear | AnimationEasing.EaseIn, 0.3), 9);
        Assert.Equal(0, AnimationPlayback.Ease(AnimationEasing.Jump, 0.99), 9);
        Assert.Equal(1, AnimationPlayback.Ease(AnimationEasing.Jump, 1), 9);
        Assert.Equal(1, AnimationPlayback.Ease(AnimationEasing.Jump | AnimationEasing.EaseIn, 0.01), 9);
    }

    [Fact]
    public void Nothing_to_play_is_no_look()
    {
        Assert.False(AnimationPlayback.TryEvaluate(null, 5, Tps, AnimatedBase.Default, out AnimatedLook look));
        Assert.True(look.IsNone);
        EntityAnimation instant = One(new AnimationTrack { StartTick = 0, Start = Z(0), Steps = new[] { new AnimationStep(0, 0, 0, 0, Z(5)) } });
        Assert.False(AnimationPlayback.TryEvaluate(instant, 0, Tps, AnimatedBase.Default, out _)); // a step of no time jumps
    }
}
