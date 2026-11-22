using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>
/// Protocol version 26 (D156, docs/ai/specs/protocol.md section 35): every BYOND filter type with its parameters round trips; a filter's
/// animation track (its position, its animated parameters, a screen entry's child) round trips in an entity's field and a screen entry;
/// undefined field bits, a child track that names no filter and a filter position past the limit are refused.
/// </summary>
public class ProtocolV26Tests
{
    [Fact]
    public void TheVersionIsTwentySix() => Assert.True(ProtocolVersion.Current >= 26);

    private static Delta WithAppearance(AppearanceEntry entry)
    {
        Delta d = Msg.Delta(10, 4, 5);
        d.FirstNewString = 1;
        d.NewStrings.Add("*target");
        d.FirstNewResource = 1;
        d.NewResources.Add("icons/effects/effects.dmi");
        d.FirstNewAppearance = 1;
        d.NewAppearances.Add(entry);
        return d;
    }

    [Fact]
    public void Every_filter_type_round_trips_with_its_parameters()
    {
        var matrix = Enumerable.Range(0, 20).Select(i => i / 20f).ToArray();
        var filters = new List<FilterEntry>
        {
            new() { Kind = FilterKind.AngularBlur, X = 2, Y = -3, Size = 15, Offset = 4 },
            new() { Kind = FilterKind.Bloom, ThresholdRgba = 0x808080FF, Size = 2, Offset = 1, Alpha = 128 },
            new() { Kind = FilterKind.Blur, Size = 2.5f },
            new() { Kind = FilterKind.DropShadow, X = 1, Y = -1, Size = 3, Offset = 1, Rgba = 0x00000080 },
            new() { Kind = FilterKind.Layer, Icon = TableRef.Public(1), RenderSource = TableRef.Public(1), Flags = FilterFlags.Underlay, ColorMatrix = matrix, Transform = new[] { 1f, 0, 2, 0, 1, -2 }, BlendMode = 2 },
            new() { Kind = FilterKind.MotionBlur, X = 0, Y = 1 },
            new() { Kind = FilterKind.RadialBlur, Size = 0.01f, Offset = 3 },
            new() { Kind = FilterKind.Rays, Size = 16, Rgba = 0xFFFFFFFF, Offset = 3.5f, Density = 10, Threshold = 0.5f, Factor = 0.2f, Flags = 3 },
            new() { Kind = FilterKind.Ripple, Size = 2, Repeat = 2, Radius = 10, Falloff = 1, Flags = FilterFlags.WaveBounded },
            new() { Kind = FilterKind.Wave, X = 4, Y = 3, Size = 1.5f, Offset = 0.25f, Flags = FilterFlags.WaveSideways },
            new() { Kind = FilterKind.Greyscale },
            new() { Kind = FilterKind.Color, ColorMatrix = matrix, Space = 2 },
            new() { Kind = FilterKind.Outline, Size = 1.2f, Rgba = 0xFF0033FF, Alpha = 0 },
        };
        Delta d = WithAppearance(new AppearanceEntry { Filters = filters });
        Assert.True(Codec.TryDecode(Codec.Encode(d), out Delta back, out ProtocolError error), error.ToString());
        List<FilterEntry> got = back.NewAppearances[0].Filters;
        Assert.Equal(filters.Count, got.Count);
        for (int i = 0; i < filters.Count; i++)
            Assert.Equal(filters[i], got[i]);
    }

    [Fact]
    public void A_bad_colour_space_or_blend_mode_is_refused()
    {
        Assert.Throws<ProtocolException>(() => Codec.Encode(WithAppearance(new AppearanceEntry { Filters = new List<FilterEntry> { new() { Kind = FilterKind.Color, Space = 4 } } })));
        Assert.Throws<ProtocolException>(() => Codec.Encode(WithAppearance(new AppearanceEntry { Filters = new List<FilterEntry> { new() { Kind = FilterKind.Layer, BlendMode = 6 } } })));
        Assert.Throws<ProtocolException>(() => Codec.Encode(WithAppearance(new AppearanceEntry { Filters = new List<FilterEntry> { new() { Kind = (FilterKind)16 } } })));
    }

    // tg's health doll: an outline's alpha from 0 to 200 and back, for ever (screen_objects.dm:1023).
    public static AnimationTrack Pulse(long start, TableRef child = default) => new()
    {
        StartTick = start,
        Loop = 0,
        Filter = 1,
        Child = child,
        FilterStart = Vars(FilterVar.Alpha, 0f),
        Steps = new[]
        {
            new AnimationStep(15, 0, AnimationEasing.Linear, AnimationStepFlags.None, default, Vars(FilterVar.Alpha, 200f)),
            new AnimationStep(15, 0, AnimationEasing.Linear, AnimationStepFlags.None, default, Vars(FilterVar.Alpha, 0f)),
        },
    };

    private static FilterVars Vars(FilterVar bit, float value)
    {
        var v = new FilterVars();
        v.Set(bit, value);
        return v;
    }

    [Fact]
    public void A_filter_track_round_trips_in_an_entity_and_with_a_child_in_a_screen_entry()
    {
        var matrixStep = new FilterVars { Mask = FilterVar.ColorMatrix | FilterVar.Color, Rgba = 0x112233FF, Matrix = Enumerable.Range(0, 20).Select(i => (float)i).ToArray() };
        matrixStep.Set(FilterVar.Y, 1f);
        var animation = new EntityAnimation
        {
            Tracks = new[]
            {
                AnimationCodecTests.Sample(190).Tracks[0],
                Pulse(195),
                new AnimationTrack
                {
                    StartTick = 199, Loop = 2, Parallel = true, Filter = 3, FilterStart = Vars(FilterVar.Y, 0f),
                    Steps = new[] { new AnimationStep(10, 1, AnimationEasing.Sine, AnimationStepFlags.Relative, default, matrixStep) },
                },
            },
        };
        Delta d = Msg.Delta(200, changed: new[] { new EntityChange { Id = 5, Mask = NetField.Animation, Animation = animation } });
        Assert.True(Codec.TryDecode(Codec.Encode(d), out Delta back, out ProtocolError error), error.ToString());
        Assert.Equal(animation, back.Changed[0].Animation);

        Delta screen = Msg.Delta(300);
        var doll = new EntityAnimation { Tracks = new[] { Pulse(290, new TableRef(7, true)), Pulse(295, new TableRef(8, true)) } };
        screen.Own.ScreenChanged.Add(new ScreenEntry(12, new TableRef(9, true), "EAST,7") { Animation = doll });
        Assert.True(Codec.TryDecode(Codec.Encode(screen), out Delta decoded, out error), error.ToString());
        Assert.Equal(doll, decoded.Own.ScreenChanged[0].Animation);
        Assert.Equal(new TableRef(8, true), decoded.Own.ScreenChanged[0].Animation.Tracks[1].Child);
    }

    [Fact]
    public void A_childs_own_vars_track_round_trips_and_a_filter_past_the_limit_is_refused()
    {
        var childOfAtom = new AnimationTrack
        {
            StartTick = 5, Child = new TableRef(2, true), Start = new AnimatedVars { Mask = AnimatedVar.Alpha, Alpha = 255 },
            Steps = new[] { new AnimationStep(5, 0, 0, AnimationStepFlags.None, new AnimatedVars { Mask = AnimatedVar.Alpha, Alpha = 0 }) },
        };
        Delta d = Msg.Delta(10, changed: new[] { new EntityChange { Id = 5, Mask = NetField.Animation, Animation = new EntityAnimation { Tracks = new[] { childOfAtom } } } });
        // version 74 (gaps W1): a child's track of its own vars is allowed (tg's status display scrolls its text objects' maptext_x)
        Assert.True(Codec.TryDecode(Codec.Encode(d), out Delta decoded, out ProtocolError error), error.ToString());
        Assert.Equal(new TableRef(2, true), decoded.Changed[0].Animation.Tracks[0].Child);
        AnimationTrack far = Pulse(5);
        far = new AnimationTrack { StartTick = far.StartTick, Loop = far.Loop, Filter = (byte)(Limits.MaxAppearanceFilters + 1), FilterStart = far.FilterStart, Steps = far.Steps };
        d = Msg.Delta(10, changed: new[] { new EntityChange { Id = 5, Mask = NetField.Animation, Animation = new EntityAnimation { Tracks = new[] { far } } } });
        Assert.Throws<ProtocolException>(() => Codec.Encode(d));
    }
}
