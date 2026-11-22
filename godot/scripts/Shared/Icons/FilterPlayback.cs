using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Icons;

/// <summary>
/// Protocol version 26 (D156): the client's playback of BYOND's <c>animate()</c> of a filter, engine-free. A filter's track
/// (<see cref="AnimationTrack.Filter"/>, the filter's position in the appearance's filters) plays as an atom's does
/// (<see cref="AnimationPlayback"/>: delays, times, easing, loops; a relative step adds, a colour multiplies), and the filter is drawn
/// at the appearance's values (where the chains end, as the server applied them) plus each running track's difference from its end.
/// tg's health doll pulses its wound outline's alpha between 0 and 200 (screen_objects.dm:1023); high gravity moves a motion blur's y
/// (life.dm:129).
/// </summary>
public static class FilterPlayback
{
    private const int Numbers = 11;

    private struct Vals
    {
        public double[] N; // FilterVars.Numbers order
        public double R, G, B, A;
        public double[] M; // 20, or null when no step names the matrix

        public Vals Copy() => new() { N = (double[])N.Clone(), R = R, G = G, B = B, A = A, M = (double[])M?.Clone() };
    }

    /// <summary>
    /// The filters of an appearance as the running filter tracks of <paramref name="animation"/> show them at <paramref name="renderTick"/>:
    /// the root's (its tracks with no <see cref="AnimationTrack.Child"/>) and each animated child's (a screen entry's vis_contents child),
    /// keyed by the appearance entry they replace the filters of; false (and null) when no filter track changes one now.
    /// </summary>
    public static bool TryEvaluate(IAppearanceSource tables, TableRef root, EntityAnimation animation, double renderTick, int ticksPerSecond, out Dictionary<AppearanceEntry, IReadOnlyList<FilterEntry>> looks)
    {
        looks = null;
        if (animation?.Tracks == null || ticksPerSecond <= 0)
            return false;
        foreach (AnimationTrack track in animation.Tracks)
        {
            if (track == null || track.Filter == 0)
                continue;
            if (!tables.TryGetAppearance(track.Child.IsNone ? root : track.Child, out AppearanceEntry entry) || entry.Filters == null)
                continue;
            if (looks != null && looks.ContainsKey(entry))
                continue; // every track of this entry was applied with the first
            if (TryEvaluate(animation, renderTick, ticksPerSecond, entry.Filters, track.Child, out List<FilterEntry> shown))
                (looks ??= new Dictionary<AppearanceEntry, IReadOnlyList<FilterEntry>>(ReferenceEqualityComparer.Instance))[entry] = shown;
        }
        return looks != null;
    }

    /// <summary>
    /// The filters to draw at <paramref name="renderTick"/>: copies of <paramref name="filters"/> with the running filter tracks of
    /// <paramref name="animation"/> for <paramref name="child"/> (none: the root's) applied. False (and null) when no such track changes one now.
    /// </summary>
    public static bool TryEvaluate(EntityAnimation animation, double renderTick, int ticksPerSecond, IReadOnlyList<FilterEntry> filters, TableRef child, out List<FilterEntry> shown)
    {
        shown = null;
        if (animation?.Tracks == null || filters == null || filters.Count == 0 || ticksPerSecond <= 0)
            return false;
        foreach (AnimationTrack track in animation.Tracks)
        {
            if (track == null || track.Filter == 0 || track.Filter > filters.Count || track.Steps == null || track.Steps.Length == 0 || track.Child != child)
                continue;
            double t = (renderTick - track.StartTick) * 10.0 / ticksPerSecond;
            if (!TryTrack(track, t, out Vals now, out Vals end))
                continue;
            shown ??= filters.Select(f => f.Copy()).ToList();
            Apply(shown[track.Filter - 1], track, now, end);
        }
        return shown != null;
    }

    // The filter plus the track's difference from its end, for every parameter the track names.
    private static void Apply(FilterEntry f, AnimationTrack track, in Vals now, in Vals end)
    {
        FilterVar mask = track.FilterStart.Mask;
        foreach (AnimationStep step in track.Steps) mask |= step.Filter.Mask;
        for (int i = 0; i < Numbers; i++)
        {
            FilterVar bit = FilterVars.Numbers[i];
            if ((mask & bit) == 0) continue;
            float value = (float)(Get(f, bit) + now.N[i] - end.N[i]);
            Set(f, bit, float.IsFinite(value) ? value : Get(f, bit));
        }
        if ((mask & FilterVar.Color) != 0)
        {
            uint c = f.Rgba;
            f.Rgba = (Byte(Channel(c, 24) + now.R - end.R) << 24) | (Byte(Channel(c, 16) + now.G - end.G) << 16)
                | (Byte(Channel(c, 8) + now.B - end.B) << 8) | Byte(Channel(c, 0) + now.A - end.A);
        }
        if ((mask & FilterVar.ColorMatrix) != 0 && now.M != null && end.M != null)
        {
            float[] m = f.ColorMatrix ?? Identity();
            var made = new float[20];
            for (int i = 0; i < 20; i++)
                made[i] = (float)(m[i] + now.M[i] - end.M[i]);
            f.ColorMatrix = made;
        }
    }

    private static uint Byte(double v) => (uint)Math.Clamp((int)Math.Round(v), 0, 255);

    private static double Channel(uint rgba, int shift) => (rgba >> shift) & 0xFF;

    private static float[] Identity()
    {
        var m = new float[20];
        m[0] = m[5] = m[10] = m[15] = 1f;
        return m;
    }

    private static float Get(FilterEntry f, FilterVar v) => v switch
    {
        FilterVar.X => f.X,
        FilterVar.Y => f.Y,
        FilterVar.Size => f.Size,
        FilterVar.Offset => f.Offset,
        FilterVar.Alpha => f.Alpha,
        FilterVar.Radius => f.Radius,
        FilterVar.Falloff => f.Falloff,
        FilterVar.Repeat => f.Repeat,
        FilterVar.Density => f.Density,
        FilterVar.Threshold => f.Threshold,
        FilterVar.Factor => f.Factor,
        _ => 0f,
    };

    private static void Set(FilterEntry f, FilterVar v, float value)
    {
        switch (v)
        {
            case FilterVar.X: f.X = value; break;
            case FilterVar.Y: f.Y = value; break;
            case FilterVar.Size: f.Size = value; break;
            case FilterVar.Offset: f.Offset = value; break;
            case FilterVar.Alpha: f.Alpha = Math.Clamp(value, 0f, 255f); break;
            case FilterVar.Radius: f.Radius = value; break;
            case FilterVar.Falloff: f.Falloff = value; break;
            case FilterVar.Repeat: f.Repeat = value; break;
            case FilterVar.Density: f.Density = value; break;
            case FilterVar.Threshold: f.Threshold = value; break;
            case FilterVar.Factor: f.Factor = value; break;
        }
    }

    // The track's values at t deciseconds after its start and at the end of its first round; false once it has ended.
    private static bool TryTrack(AnimationTrack track, double t, out Vals now, out Vals end)
    {
        Vals start = From(track.FilterStart);
        end = Round(track, start.Copy(), double.PositiveInfinity);
        now = start;
        double cycle = EntityAnimation.CycleOf(track);
        if (t < 0)
            return true;
        if (cycle <= 0)
            return false;
        double rounds = t / cycle;
        if (track.Loop > 0 && rounds >= track.Loop)
            return false;
        long n = (long)Math.Floor(rounds);
        now = Round(track, StartOfRound(track, start, end, n), t - n * cycle);
        return true;
    }

    // Where round n starts: the first at the start values, a later one where the last ended (a parameter some step sets outright ends
    // every round at the same value; one only relative steps change moves by the round's change n times).
    private static Vals StartOfRound(AnimationTrack track, in Vals start, in Vals firstEnd, long n)
    {
        if (n == 0)
            return start.Copy();
        FilterVar absolute = FilterVar.None;
        foreach (AnimationStep step in track.Steps)
            if ((step.Flags & AnimationStepFlags.Relative) == 0) absolute |= step.Filter.Mask;
        Vals v = firstEnd.Copy();
        for (int i = 0; i < Numbers; i++)
            if ((absolute & FilterVars.Numbers[i]) == 0)
                v.N[i] = start.N[i] + n * (firstEnd.N[i] - start.N[i]);
        return v;
    }

    private static Vals Round(AnimationTrack track, Vals v, double local)
    {
        foreach (AnimationStep step in track.Steps)
        {
            if (local < step.Delay)
                return v;
            local -= step.Delay;
            Vals target = Target(v, step);
            if (local < step.Time)
                return Lerp(v, target, AnimationPlayback.Ease(step.Easing, local / step.Time));
            v = target;
            local -= step.Time;
        }
        return v;
    }

    private static Vals Target(in Vals from, in AnimationStep step)
    {
        Vals to = from.Copy();
        FilterVars s = step.Filter;
        bool relative = (step.Flags & AnimationStepFlags.Relative) != 0;
        for (int i = 0; i < Numbers; i++)
        {
            FilterVar bit = FilterVars.Numbers[i];
            if (s.Has(bit)) to.N[i] = relative ? from.N[i] + s.Get(bit) : s.Get(bit);
        }
        if (s.Has(FilterVar.Color))
        {
            double r = Channel(s.Rgba, 24), g = Channel(s.Rgba, 16), b = Channel(s.Rgba, 8), a = Channel(s.Rgba, 0);
            (to.R, to.G, to.B, to.A) = relative ? (from.R * r / 255, from.G * g / 255, from.B * b / 255, from.A * a / 255) : (r, g, b, a);
        }
        if (s.Has(FilterVar.ColorMatrix) && s.Matrix is { Length: 20 })
        {
            to.M ??= Identity().Select(x => (double)x).ToArray();
            for (int i = 0; i < 20; i++)
                to.M[i] = relative ? to.M[i] + s.Matrix[i] : s.Matrix[i];
        }
        return to;
    }

    private static Vals Lerp(in Vals a, in Vals b, double f)
    {
        Vals v = a.Copy();
        for (int i = 0; i < Numbers; i++)
            v.N[i] = a.N[i] + (b.N[i] - a.N[i]) * f;
        v.R = a.R + (b.R - a.R) * f;
        v.G = a.G + (b.G - a.G) * f;
        v.B = a.B + (b.B - a.B) * f;
        v.A = a.A + (b.A - a.A) * f;
        if (a.M != null && b.M != null)
            for (int i = 0; i < 20; i++)
                v.M[i] = a.M[i] + (b.M[i] - a.M[i]) * f;
        else
            v.M = (double[])b.M?.Clone();
        return v;
    }

    private static Vals From(in FilterVars s)
    {
        var v = new Vals { N = new double[Numbers], R = 255, G = 255, B = 255, A = 255 };
        for (int i = 0; i < Numbers; i++)
            if (s.Has(FilterVars.Numbers[i])) v.N[i] = s.Get(FilterVars.Numbers[i]);
        if (s.Has(FilterVar.Color))
            (v.R, v.G, v.B, v.A) = (Channel(s.Rgba, 24), Channel(s.Rgba, 16), Channel(s.Rgba, 8), Channel(s.Rgba, 0));
        if (s.Has(FilterVar.ColorMatrix) && s.Matrix is { Length: 20 })
            v.M = s.Matrix.Select(x => (double)x).ToArray();
        return v;
    }
}
