using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Icons;

namespace Anomaly.Server.Tests.Client.Play;

/// <summary>M4v slice 4 (protocol version 11): how a HUD picture shows a screen object's animate() look (HudLook).</summary>
public class HudLookTests
{
    [Fact]
    public void No_look_changes_nothing()
    {
        Assert.Equal(HudLook.None, HudLook.Of(AnimatedLook.None, 3f));
    }

    [Fact]
    public void An_alert_sliding_in_moves_right_by_its_translation_in_screen_pixels()
    {
        // alert.dm:66: transform = matrix(32, 0, MATRIX_TRANSLATE), animated to matrix(): halfway, 16 pixels to the right
        HudLook look = HudLook.Of(AnimatedLook.None with { Transform = Affine.Translate(16, 0) }, 2f);
        Assert.Equal(32f, look.MoveX);
        Assert.Equal(0f, look.MoveY);
        Assert.Equal(0f, look.Rotation);
        Assert.Equal(1f, look.ScaleX, 5);
        Assert.Equal(1f, look.ScaleY, 5);
    }

    [Fact]
    public void Byond_up_is_screen_up_and_a_turn_is_clockwise()
    {
        HudLook moved = HudLook.Of(AnimatedLook.None with { PixelY = 3, PixelZ = 1 }, 2f);
        Assert.Equal(-8f, moved.MoveY); // y down on the screen
        // matrix().Turn(90): a = cos, b = sin, d = -sin, e = cos (matrix.cs)
        HudLook turned = HudLook.Of(AnimatedLook.None with { Transform = new Affine(0, 1, 0, -1, 0, 0) }, 1f);
        Assert.Equal((float)(Math.PI / 2), turned.Rotation, 5);
        Assert.Equal(1f, turned.ScaleX, 5);
        Assert.Equal(1f, turned.ScaleY, 5);
        HudLook mirrored = HudLook.Of(AnimatedLook.None with { Transform = new Affine(1, 0, 0, 0, -2, 0) }, 1f);
        Assert.Equal(-2f, mirrored.ScaleY, 5);
    }

    [Fact]
    public void A_fade_to_transparent_plays_over_the_opaque_picture()
    {
        // The live check (2026-10-14): a cleared fullscreen vanished at once. Its appearance holds the chain's end (alpha 0) and the HUD composes
        // it opaque; the chain's alpha is taken over 255. A base of 255 had added the chain's way down to 255: clamped, no change, no fade.
        var fade = new Anomaly.Protocol.EntityAnimation
        {
            Tracks = new[]
            {
                new Anomaly.Protocol.AnimationTrack
                {
                    StartTick = 100, Loop = 1,
                    Start = new Anomaly.Protocol.AnimatedVars { Mask = Anomaly.Protocol.AnimatedVar.Alpha, Alpha = 255 },
                    Steps = new[] { new Anomaly.Protocol.AnimationStep(10, 0, Anomaly.Protocol.AnimationEasing.Linear, Anomaly.Protocol.AnimationStepFlags.None, new Anomaly.Protocol.AnimatedVars { Mask = Anomaly.Protocol.AnimatedVar.Alpha, Alpha = 0 }) },
                },
            },
        };
        AnimatedBase ended = AnimatedBase.Default with { Alpha = 0 };
        Assert.True(AnimationPlayback.TryEvaluate(fade, 110, 20, ended, out AnimatedLook half, composedAlpha: 255));
        Assert.Equal(0.5, half.Alpha, 2);
        Assert.True(AnimationPlayback.TryEvaluate(fade, 99, 20, ended, out AnimatedLook early, composedAlpha: 255));
        Assert.Equal(1.0, early.Alpha, 3);
        Assert.False(AnimationPlayback.TryEvaluate(fade, 121, 20, ended, out _, composedAlpha: 255)); // ended: the appearance (alpha 0) shows
    }

    [Fact]
    public void A_fade_is_the_modulates_alpha()
    {
        HudLook look = HudLook.Of(AnimatedLook.None with { Alpha = 0.25, Red = 0.5 }, 1f);
        Assert.Equal(0.25f, look.Alpha);
        Assert.Equal(0.5f, look.Red);
        Assert.Equal(1f, HudLook.Of(AnimatedLook.None with { Alpha = 1.5 }, 1f).Alpha);
    }
}
