using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>Protocol version 9 (docs/ai/specs/protocol.md section 18): the entity's Animation field on the wire, its round trips and limits.</summary>
public class AnimationCodecTests
{
    // Distinct numbers the tests find in the encoded bytes.
    private const float MarkStartPixel = 4321.25f;
    private const float MarkTime = 1234.5f;

    /// <summary>An animation with a main track (every var, two steps, looping three times) and a parallel relative one, started at <paramref name="tick"/>.</summary>
    public static EntityAnimation Sample(long tick) => new()
    {
        Tracks = new[]
        {
            new AnimationTrack
            {
                StartTick = tick, Loop = 3, Parallel = false,
                Start = new AnimatedVars
                {
                    Mask = AnimatedVar.All, PixelX = MarkStartPixel, PixelY = -6, PixelZ = 2, PixelW = 0.5f, Alpha = 0, Rgba = 0x11223344,
                    A = 0.7f, B = 0.2f, C = 0, D = -0.2f, E = 0.7f, F = 3,
                },
                Steps = new[]
                {
                    new AnimationStep(MarkTime, 0, AnimationEasing.Cubic, AnimationStepFlags.None, new AnimatedVars { Mask = AnimatedVar.Alpha | AnimatedVar.Transform, Alpha = 255, A = 1, E = 1 }),
                    new AnimationStep(2, 1.5f, (byte)(AnimationEasing.Sine | AnimationEasing.EaseOut), AnimationStepFlags.LinearTransform,
                        new AnimatedVars { Mask = AnimatedVar.PixelX | AnimatedVar.PixelY | AnimatedVar.PixelZ | AnimatedVar.PixelW | AnimatedVar.Color, Rgba = 0xFFFFFFFF }),
                },
            },
            new AnimationTrack
            {
                StartTick = tick - 4, Loop = 0, Parallel = true,
                Start = new AnimatedVars { Mask = AnimatedVar.PixelW },
                Steps = new[] { new AnimationStep(2, 0, AnimationEasing.Linear, AnimationStepFlags.Relative, new AnimatedVars { Mask = AnimatedVar.PixelW, PixelW = 3 }) },
            },
        },
    };

    private static Delta DeltaWith(EntityAnimation animation, long tick = 200)
        => Msg.Delta(tick, changed: new[] { new EntityChange { Id = 5, Mask = NetField.Animation, Animation = animation } });

    [Fact]
    public void An_animation_round_trips_in_a_change_and_a_record_and_none_is_a_change_too()
    {
        Delta d = DeltaWith(Sample(198));
        d.Changed.Add(new EntityChange { Id = 6, Mask = NetField.Animation, Animation = null }); // ended or stopped
        Assert.True(Codec.TryDecode(Codec.Encode(d), out Delta decoded, out ProtocolError error), error.ToString());
        Msg.AssertEqual(d, decoded);
        Assert.Equal(Sample(198), decoded.Changed[0].Animation);
        Assert.Null(decoded.Changed[1].Animation);

        EntityState prop = Msg.State(7, 0, 3, 3, 1, 0, null, 0f, 10) with { Animation = Sample(50) };
        Snapshot s = Msg.Snapshot(100, 7, 0, new[] { "/obj" }, prop);
        Assert.True(Codec.TryDecode(Codec.Encode(s), out Snapshot snapshot, out error), error.ToString());
        Assert.Equal(Sample(50), snapshot.Entities[0].Animation);
    }

    [Fact]
    public void A_change_that_only_animates_is_one_mask_bit_and_the_track_bytes()
    {
        var a = new EntityAnimation
        {
            Tracks = new[] { new AnimationTrack { StartTick = 200, Start = new AnimatedVars { Mask = AnimatedVar.PixelZ }, Steps = new[] { new AnimationStep(10, 0, 0, 0, new AnimatedVars { Mask = AnimatedVar.PixelZ, PixelZ = 2 }) } } },
        };
        byte[] with = Codec.EncodeDeltaBody(DeltaWith(a));
        byte[] none = Codec.EncodeDeltaBody(DeltaWith(null));
        // the mask is wire bit 14 (three varuint bytes) either way; the track: 1 count + 1 flags + 1 ago + 1 loop + 5 start + 1 steps + 15 step
        Assert.Equal(1 + 1 + 1 + 1 + 5 + 1 + (4 + 4 + 1 + 1 + 5) - 1, with.Length - none.Length);
    }

    /// <summary>Version 32 (protocol.md section 42): a track of tg's 36-segment SpinAnimation, an orbit's chain, round trips.</summary>
    [Fact]
    public void A_track_of_36_steps_round_trips_from_version_32()
    {
        Assert.True(ProtocolVersion.Current >= 32);
        Assert.Equal(36, Limits.MaxAnimationSteps);
        AnimationStep turn = new(20 / 36f, 0, AnimationEasing.Linear, AnimationStepFlags.None,
            new AnimatedVars { Mask = AnimatedVar.Transform, A = 0.98f, B = -0.17f, C = 0, D = 0.17f, E = 0.98f, F = 40 });
        var orbit = new EntityAnimation
        {
            Tracks = new[] { new AnimationTrack { StartTick = 198, Loop = 0, Start = new AnimatedVars { Mask = AnimatedVar.Transform, A = 1, E = 1, F = 40 }, Steps = Enumerable.Repeat(turn, 36).ToArray() } },
        };
        Assert.True(Codec.TryDecode(Codec.Encode(DeltaWith(orbit)), out Delta decoded, out ProtocolError error), error.ToString());
        Assert.Equal(orbit, decoded.Changed[0].Animation);
    }

    [Fact]
    public void The_encoder_refuses_what_the_limits_forbid()
    {
        AnimationTrack good = Sample(100).Tracks[1];
        var steps17 = Enumerable.Repeat(good.Steps[0], Limits.MaxAnimationSteps + 1).ToArray();
        EntityAnimation[] bad =
        {
            new() { Tracks = Array.Empty<AnimationTrack>() },
            new() { Tracks = Enumerable.Repeat(good, Limits.MaxAnimationTracks + 1).ToArray() },
            new() { Tracks = new[] { new AnimationTrack { StartTick = 100, Steps = Array.Empty<AnimationStep>() } } },
            new() { Tracks = new[] { new AnimationTrack { StartTick = 100, Steps = steps17 } } },
            new() { Tracks = new[] { new AnimationTrack { StartTick = 300, Steps = good.Steps } } }, // starts after the message's tick
            new() { Tracks = new[] { new AnimationTrack { StartTick = 100, Loop = -1, Steps = good.Steps } } },
            new() { Tracks = new[] { new AnimationTrack { StartTick = 100, Loop = Limits.MaxAnimationLoop + 1, Steps = good.Steps } } },
            With(good.Steps[0] with { Time = -1 }),
            With(good.Steps[0] with { Delay = Limits.MaxAnimationTime + 1 }),
            With(good.Steps[0] with { Easing = 9 }),
            With(good.Steps[0] with { Easing = 16 + 1 }),
            With(good.Steps[0] with { Flags = (AnimationStepFlags)4 }),
            With(good.Steps[0] with { Vars = new AnimatedVars { Mask = AnimatedVar.PixelW, PixelW = float.NaN } }),
            With(good.Steps[0] with { Vars = new AnimatedVars { Mask = AnimatedVar.Alpha, Alpha = Limits.MaxAnimatedValue * 2 } }),
            With(good.Steps[0] with { Vars = new AnimatedVars { Mask = AnimatedVar.MaptextX, MaptextX = float.NaN } }), // version 74: no undefined var bit is left
        };
        foreach (EntityAnimation animation in bad)
            Assert.Throws<ProtocolException>(() => Codec.Encode(DeltaWith(animation)));
    }

    private static EntityAnimation With(AnimationStep step)
        => new() { Tracks = new[] { new AnimationTrack { StartTick = 100, Steps = new[] { step } } } };

    [Fact]
    public void The_decoder_refuses_what_the_limits_forbid()
    {
        byte[] good = Codec.Encode(DeltaWith(Sample(198)));
        Assert.True(Codec.TryDecode(good, out Delta _, out _));
        int start = IndexOf(good, MarkStartPixel); // the first track: [count] [flags] [ago] [loop] [vars mask] [pixel_x] ...
        int time = IndexOf(good, MarkTime); // its first step: [time] [delay] [easing] [flags] [vars mask] ...
        Assert.Equal(2, good[start - 5]); // two tracks
        foreach ((int at, byte value) in new[]
        {
            (start - 5, (byte)(Limits.MaxAnimationTracks + 1)), // tracks over the cap
            (start - 4, (byte)8), // an undefined track flag (2 and 4 are a filter's and a child's, versions 26 and 74)
            (start - 3, (byte)0x7F), // starts 127 ticks before a message of tick 198 (fine) ...
            (time + 8, (byte)9), // an easing BYOND does not define
            (time + 9, (byte)8), // an undefined step flag
        })
        {
            byte[] bytes = (byte[])good.Clone();
            bytes[at] = value;
            bool ok = Codec.TryDecode(bytes, out Delta _, out ProtocolError error);
            if (at == start - 3)
                Assert.True(ok, error.ToString());
            else
            {
                Assert.False(ok);
                Assert.Equal(ProtocolError.BadValue, error);
            }
        }

        Assert.Throws<ProtocolException>(() => Codec.Encode(DeltaWith(Sample(3), tick: 3))); // its parallel track would start at tick -1
    }

    [Fact]
    public void An_entity_record_never_carries_an_empty_animation()
    {
        EntityState prop = Msg.State(7, 0, 3, 3, 1, 0, null, 0f, 10) with { Animation = new EntityAnimation() };
        Assert.Throws<ProtocolException>(() => Codec.Encode(Msg.Snapshot(100, 7, 0, new[] { "/obj" }, prop)));
    }

    private static int IndexOf(byte[] bytes, float value)
    {
        byte[] pattern = BitConverter.GetBytes(value);
        if (!BitConverter.IsLittleEndian) Array.Reverse(pattern);
        for (int i = 0; i + 4 <= bytes.Length; i++)
            if (bytes.AsSpan(i, 4).SequenceEqual(pattern)) return i;
        throw new InvalidOperationException("the mark is not in the message");
    }

    [Fact]
    public void Version_74_a_childs_maptext_x_track_round_trips()
    {
        // gaps W1: tg's status display scrolls its text object's maptext_x for ever; the track names the object's row in the entity's picture
        var scroll = new EntityAnimation
        {
            Tracks = new[]
            {
                new AnimationTrack
                {
                    StartTick = 190, Loop = 0, Child = TableRef.Public(42),
                    Start = new AnimatedVars { Mask = AnimatedVar.MaptextX, MaptextX = 0 },
                    Steps = new[]
                    {
                        new AnimationStep(MarkTime, 0, AnimationEasing.Linear, AnimationStepFlags.None, new AnimatedVars { Mask = AnimatedVar.MaptextX, MaptextX = -97.5f }),
                        new AnimationStep(0, 0, AnimationEasing.Linear, AnimationStepFlags.None, new AnimatedVars { Mask = AnimatedVar.MaptextX, MaptextX = 22 }),
                    },
                },
            },
        };
        Delta d = DeltaWith(scroll);
        Assert.True(Codec.TryDecode(Codec.Encode(d), out Delta decoded, out ProtocolError error), error.ToString());
        Assert.Equal(scroll, decoded.Changed[0].Animation);
        Assert.Equal(-97.5f, decoded.Changed[0].Animation.Tracks[0].Steps[0].Vars.MaptextX);
    }
}
