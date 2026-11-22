using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Icons;

/// <summary>The animated vars' values an entity's appearance holds (its root entry): where BYOND's animations end.</summary>
public readonly record struct AnimatedBase(double PixelX, double PixelY, double PixelZ, double PixelW, double Alpha, uint Rgba, Affine Transform)
{
    /// <summary>The root entry's values (a colour matrix counts as white: protocol.md 18.1).</summary>
    public static AnimatedBase Of(AppearanceEntry e)
        => new(e.PixelX, e.PixelY, e.PixelZ, e.PixelW, e.Alpha, e.ColorMatrix != null ? 0xFFFFFFFFu : e.Rgba, Affine.FromMatrix(e.Transform));

    /// <summary>BYOND's defaults: no offset, opaque, white, identity.</summary>
    public static AnimatedBase Default => new(0, 0, 0, 0, 255, 0xFFFFFFFFu, Affine.Identity);
}

/// <summary>
/// What an animation changes on the picture now, relative to the appearance it is composed from: the pixel offsets to add, the factors
/// to multiply its colour and alpha by, and the transform to apply after the picture's own (BYOND pixels, about the icon's centre).
/// </summary>
public readonly record struct AnimatedLook(double PixelX, double PixelY, double PixelZ, double PixelW, double Red, double Green, double Blue, double Alpha, Affine Transform)
{
    public static AnimatedLook None => new(0, 0, 0, 0, 1, 1, 1, 1, Affine.Identity);

    /// <summary>Whether the picture is drawn as composed (the offsets zero, the factors one, the transform identity).</summary>
    public bool IsNone => PixelX == 0 && PixelY == 0 && PixelZ == 0 && PixelW == 0 && Red == 1 && Green == 1 && Blue == 1 && Alpha == 1 && Transform.IsIdentity;
}

/// <summary>
/// The client's playback of BYOND's <c>animate()</c> (protocol version 9, <see cref="EntityAnimation"/>), engine-free: each track's steps
/// in order (a step's <c>delay</c> holds the value, then it moves over <c>time</c> by its easing curve to its value, or by its value with
/// <c>ANIMATION_RELATIVE</c>: added, a colour or transform multiplied), the track repeated <c>loop</c> times (0: for ever; a later round
/// starts where the last ended, so a relative chain keeps going). The tracks are composed as BYOND does with parallel animations: the
/// appearance (which holds every chain's end, as the server applied it) plus each track's difference from its own end. A transform is
/// interpolated as scale, shear, rotation (the shortest way) and move, as BYOND does unless <c>ANIMATION_LINEAR_TRANSFORM</c>.
/// <para>
/// Easing (BYOND reference /proc/animate): a curve's ease-in shape f; <c>EASE_IN</c> plays f, <c>EASE_OUT</c> its mirror 1 - f(1 - t),
/// neither (or both) the two halves in turn, except BOUNCE and ELASTIC whose default is <c>EASE_OUT</c>; LINEAR has no shape; JUMP
/// holds the start and jumps at the end of the time (at its start with <c>EASE_IN</c> only). The curves are Penner's (OpenDream's).
/// </para>
/// </summary>
public static class AnimationPlayback
{
    private struct Vals
    {
        public double PX, PY, PZ, PW, Alpha, R, G, B, A, MX;
        public Affine T;
    }

    /// <summary>
    /// Version 74 (gaps W1): the maptext_x of the entry <paramref name="child"/> names (<see cref="AnimationTrack.Child"/>) at <paramref name="renderTick"/>:
    /// its appearance's value <paramref name="appearance"/> (where the chains end) plus each of its tracks' difference from its end; false when none plays now.
    /// </summary>
    public static bool TryMaptextX(EntityAnimation animation, TableRef child, double renderTick, int ticksPerSecond, double appearance, out double maptextX)
    {
        maptextX = appearance;
        if (animation?.Tracks == null || child.IsNone || ticksPerSecond <= 0)
            return false;
        bool any = false;
        foreach (AnimationTrack track in animation.Tracks)
        {
            if (track?.Steps == null || track.Steps.Length == 0 || track.Filter != 0 || track.Child != child || (Named(track) & AnimatedVar.MaptextX) == 0)
                continue;
            if (!TryTrack(track, (renderTick - track.StartTick) * 10.0 / ticksPerSecond, out Vals now, out Vals end))
                continue;
            any = true;
            maptextX += now.MX - end.MX;
        }
        return any;
    }

    /// <summary>
    /// The look of <paramref name="animation"/> at <paramref name="renderTick"/> (server ticks, fractional) over an appearance whose values
    /// are <paramref name="appearance"/>. False (and <see cref="AnimatedLook.None"/>) when no track changes the picture now: every one has
    /// ended. A track that has not started yet shows its start values.
    /// </summary>
    /// <param name="composedAlpha">The alpha the picture was composed with, when not the appearance's own: 255 for a root composed opaque
    /// (M4v slice 4: a visual's or a fullscreen's chain that ends at alpha 0; the look's alpha is then the shown alpha over 255).</param>
    public static bool TryEvaluate(EntityAnimation animation, double renderTick, int ticksPerSecond, in AnimatedBase appearance, out AnimatedLook look, double composedAlpha = double.NaN)
    {
        look = AnimatedLook.None;
        if (animation?.Tracks == null || animation.Tracks.Length == 0 || ticksPerSecond <= 0)
            return false;
        Vals shown = FromBase(appearance);
        bool any = false;
        foreach (AnimationTrack track in animation.Tracks)
        {
            // a filter's track: FilterPlayback (version 26); a child's own vars: its maptext (TryMaptextX, version 74)
            if (track?.Steps == null || track.Steps.Length == 0 || track.Filter != 0 || !track.Child.IsNone)
                continue;
            double t = (renderTick - track.StartTick) * 10.0 / ticksPerSecond;
            if (!TryTrack(track, t, out Vals now, out Vals end))
                continue;
            any = true;
            AnimatedVar mask = Named(track);
            if ((mask & AnimatedVar.PixelX) != 0) shown.PX += now.PX - end.PX;
            if ((mask & AnimatedVar.PixelY) != 0) shown.PY += now.PY - end.PY;
            if ((mask & AnimatedVar.PixelZ) != 0) shown.PZ += now.PZ - end.PZ;
            if ((mask & AnimatedVar.PixelW) != 0) shown.PW += now.PW - end.PW;
            if ((mask & AnimatedVar.Alpha) != 0) shown.Alpha += now.Alpha - end.Alpha;
            if ((mask & AnimatedVar.Color) != 0)
            {
                shown.R += now.R - end.R;
                shown.G += now.G - end.G;
                shown.B += now.B - end.B;
                shown.A += now.A - end.A;
            }
            if ((mask & AnimatedVar.Transform) != 0)
                shown.T = new Affine(shown.T.A + now.T.A - end.T.A, shown.T.B + now.T.B - end.T.B, shown.T.C + now.T.C - end.T.C,
                    shown.T.D + now.T.D - end.T.D, shown.T.E + now.T.E - end.T.E, shown.T.F + now.T.F - end.T.F);
        }
        if (!any)
            return false;
        Vals base_ = FromBase(appearance);
        Affine delta = appearance.Transform.TryInvert(out Affine inverse) ? inverse.Then(shown.T) : Affine.Identity;
        look = new AnimatedLook(shown.PX - base_.PX, shown.PY - base_.PY, shown.PZ - base_.PZ, shown.PW - base_.PW,
            Factor(shown.R, base_.R), Factor(shown.G, base_.G), Factor(shown.B, base_.B),
            Factor(Math.Clamp(shown.Alpha, 0, 255), double.IsNaN(composedAlpha) ? base_.Alpha : composedAlpha) * Factor(shown.A, base_.A), delta);
        return true;
    }

    // A colour or alpha channel shown over one the picture was composed with (0 there: the picture has nothing to scale).
    private static double Factor(double shown, double composed)
        => composed <= 0 ? 1 : Math.Clamp(Math.Clamp(shown, 0, 255) / composed, 0, 4);

    private static AnimatedVar Named(AnimationTrack track)
    {
        AnimatedVar mask = track.Start.Mask;
        foreach (AnimationStep step in track.Steps) mask |= step.Vars.Mask;
        return mask;
    }

    // The track's values at t deciseconds after its start and at the end of its first round (what the server applied); false once it has ended.
    private static bool TryTrack(AnimationTrack track, double t, out Vals now, out Vals end)
    {
        Vals start = From(track.Start, default);
        end = Round(track, start, double.PositiveInfinity);
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

    // Where round n starts: the first round at the start values; a later one where the last ended. A var some step sets outright ends
    // every round at the same value; one only relative steps change moves by the round's change n times (a transform's by its power).
    private static Vals StartOfRound(AnimationTrack track, in Vals start, in Vals firstEnd, long n)
    {
        if (n == 0)
            return start;
        Vals delta = RelativeOfRound(track);
        Vals v = start;
        AnimatedVar absolute = AnimatedVar.None;
        foreach (AnimationStep step in track.Steps)
            if ((step.Flags & AnimationStepFlags.Relative) == 0) absolute |= step.Vars.Mask;
        v.PX = (absolute & AnimatedVar.PixelX) != 0 ? firstEnd.PX : start.PX + n * delta.PX;
        v.PY = (absolute & AnimatedVar.PixelY) != 0 ? firstEnd.PY : start.PY + n * delta.PY;
        v.PZ = (absolute & AnimatedVar.PixelZ) != 0 ? firstEnd.PZ : start.PZ + n * delta.PZ;
        v.PW = (absolute & AnimatedVar.PixelW) != 0 ? firstEnd.PW : start.PW + n * delta.PW;
        v.Alpha = (absolute & AnimatedVar.Alpha) != 0 ? firstEnd.Alpha : start.Alpha + n * delta.Alpha;
        v.MX = (absolute & AnimatedVar.MaptextX) != 0 ? firstEnd.MX : start.MX + n * delta.MX;
        if ((absolute & AnimatedVar.Color) != 0)
            (v.R, v.G, v.B, v.A) = (firstEnd.R, firstEnd.G, firstEnd.B, firstEnd.A);
        else
            (v.R, v.G, v.B, v.A) = (Scale(start.R, delta.R, n), Scale(start.G, delta.G, n), Scale(start.B, delta.B, n), Scale(start.A, delta.A, n));
        v.T = (absolute & AnimatedVar.Transform) != 0 ? firstEnd.T : start.T.Then(Power(delta.T, n));
        return v;
    }

    private static double Scale(double start, double factor, long n) => Math.Clamp(start * Math.Pow(factor, n), 0, 255);

    private static Affine Power(Affine m, long n)
    {
        Affine result = Affine.Identity;
        while (n > 0)
        {
            if ((n & 1) != 0) result = result.Then(m);
            m = m.Then(m);
            n >>= 1;
        }
        return result;
    }

    // The change one round of the relative steps makes (numbers added, colour factors 0 to 1 per channel, transforms in order).
    private static Vals RelativeOfRound(AnimationTrack track)
    {
        var d = new Vals { R = 1, G = 1, B = 1, A = 1, T = Affine.Identity };
        foreach (AnimationStep step in track.Steps)
        {
            if ((step.Flags & AnimationStepFlags.Relative) == 0)
                continue;
            AnimatedVars s = step.Vars;
            if (s.Has(AnimatedVar.PixelX)) d.PX += s.PixelX;
            if (s.Has(AnimatedVar.PixelY)) d.PY += s.PixelY;
            if (s.Has(AnimatedVar.PixelZ)) d.PZ += s.PixelZ;
            if (s.Has(AnimatedVar.PixelW)) d.PW += s.PixelW;
            if (s.Has(AnimatedVar.Alpha)) d.Alpha += s.Alpha;
            if (s.Has(AnimatedVar.MaptextX)) d.MX += s.MaptextX;
            if (s.Has(AnimatedVar.Color))
            {
                d.R *= Channel(s.Rgba, 24) / 255.0;
                d.G *= Channel(s.Rgba, 16) / 255.0;
                d.B *= Channel(s.Rgba, 8) / 255.0;
                d.A *= Channel(s.Rgba, 0) / 255.0;
            }
            if (s.Has(AnimatedVar.Transform)) d.T = d.T.Then(TransformOf(s));
        }
        return d;
    }

    // One round of the steps from v, stopped at local deciseconds into it.
    private static Vals Round(AnimationTrack track, Vals v, double local)
    {
        foreach (AnimationStep step in track.Steps)
        {
            if (local < step.Delay)
                return v;
            local -= step.Delay;
            Vals target = Target(v, step);
            if (local < step.Time)
                return Lerp(v, target, step, Ease(step.Easing, local / step.Time));
            v = target;
            local -= step.Time;
        }
        return v;
    }

    private static Vals Target(in Vals from, in AnimationStep step)
    {
        Vals to = from;
        AnimatedVars s = step.Vars;
        bool relative = (step.Flags & AnimationStepFlags.Relative) != 0;
        if (s.Has(AnimatedVar.PixelX)) to.PX = relative ? from.PX + s.PixelX : s.PixelX;
        if (s.Has(AnimatedVar.PixelY)) to.PY = relative ? from.PY + s.PixelY : s.PixelY;
        if (s.Has(AnimatedVar.PixelZ)) to.PZ = relative ? from.PZ + s.PixelZ : s.PixelZ;
        if (s.Has(AnimatedVar.PixelW)) to.PW = relative ? from.PW + s.PixelW : s.PixelW;
        if (s.Has(AnimatedVar.Alpha)) to.Alpha = relative ? from.Alpha + s.Alpha : s.Alpha;
        if (s.Has(AnimatedVar.MaptextX)) to.MX = relative ? from.MX + s.MaptextX : s.MaptextX;
        if (s.Has(AnimatedVar.Color))
        {
            double r = Channel(s.Rgba, 24), g = Channel(s.Rgba, 16), b = Channel(s.Rgba, 8), a = Channel(s.Rgba, 0);
            (to.R, to.G, to.B, to.A) = relative ? (from.R * r / 255, from.G * g / 255, from.B * b / 255, from.A * a / 255) : (r, g, b, a);
        }
        if (s.Has(AnimatedVar.Transform)) to.T = relative ? from.T.Then(TransformOf(s)) : TransformOf(s);
        return to;
    }

    private static Vals Lerp(in Vals a, in Vals b, in AnimationStep step, double f)
    {
        Vals v = a;
        v.PX = a.PX + (b.PX - a.PX) * f;
        v.PY = a.PY + (b.PY - a.PY) * f;
        v.PZ = a.PZ + (b.PZ - a.PZ) * f;
        v.PW = a.PW + (b.PW - a.PW) * f;
        v.Alpha = a.Alpha + (b.Alpha - a.Alpha) * f;
        v.MX = a.MX + (b.MX - a.MX) * f;
        v.R = a.R + (b.R - a.R) * f;
        v.G = a.G + (b.G - a.G) * f;
        v.B = a.B + (b.B - a.B) * f;
        v.A = a.A + (b.A - a.A) * f;
        v.T = (step.Flags & AnimationStepFlags.LinearTransform) != 0 ? LerpLinear(a.T, b.T, f) : LerpDecomposed(a.T, b.T, f);
        return v;
    }

    private static Affine LerpLinear(Affine a, Affine b, double f)
        => new(a.A + (b.A - a.A) * f, a.B + (b.B - a.B) * f, a.C + (b.C - a.C) * f, a.D + (b.D - a.D) * f, a.E + (b.E - a.E) * f, a.F + (b.F - a.F) * f);

    /// <summary>
    /// A transform between <paramref name="a"/> and <paramref name="b"/>: each split into a rotation and an upper triangle (scale x, shear,
    /// scale y; the linear part is the rotation after it) and a move; the angle goes the shorter way.
    /// </summary>
    public static Affine LerpDecomposed(Affine a, Affine b, double f)
    {
        if (a.A == b.A && a.B == b.B && a.C == b.C && a.D == b.D && a.E == b.E && a.F == b.F)
            return a;
        (double sx0, double k0, double sy0, double phi0) = Decompose(a);
        (double sx1, double k1, double sy1, double phi1) = Decompose(b);
        double turn = phi1 - phi0;
        turn = Math.IEEERemainder(turn, 2 * Math.PI);
        double phi = phi0 + turn * f;
        double sx = sx0 + (sx1 - sx0) * f, k = k0 + (k1 - k0) * f, sy = sy0 + (sy1 - sy0) * f;
        double cos = Math.Cos(phi), sin = Math.Sin(phi);
        // linear = R(phi) * [[sx, k], [0, sy]], columns (a, d) and (b, e)
        return new Affine(cos * sx, cos * k - sin * sy, a.C + (b.C - a.C) * f, sin * sx, sin * k + cos * sy, a.F + (b.F - a.F) * f);
    }

    private static (double Sx, double K, double Sy, double Phi) Decompose(Affine m)
    {
        double sx = Math.Sqrt(m.A * m.A + m.D * m.D);
        double phi = sx > 1e-9 ? Math.Atan2(m.D, m.A) : 0;
        double cos = Math.Cos(phi), sin = Math.Sin(phi);
        return (sx, cos * m.B + sin * m.E, -sin * m.B + cos * m.E, phi);
    }

    /// <summary>BYOND's easing of a fraction <paramref name="t"/> (0 to 1) of a step's time.</summary>
    public static double Ease(byte easing, double t)
    {
        t = Math.Clamp(t, 0, 1);
        byte curve = AnimationEasing.Curve(easing);
        bool easeIn = (easing & AnimationEasing.EaseIn) != 0, easeOut = (easing & AnimationEasing.EaseOut) != 0;
        if (curve == AnimationEasing.Linear)
            return t;
        if (curve == AnimationEasing.Jump)
            return easeIn && !easeOut ? (t > 0 ? 1 : 0) : (t >= 1 ? 1 : 0);
        if (!easeIn && !easeOut && curve is AnimationEasing.Bounce or AnimationEasing.Elastic)
            easeOut = true;
        if (easeIn && !easeOut)
            return In(curve, t);
        if (easeOut && !easeIn)
            return 1 - In(curve, 1 - t);
        return t < 0.5 ? In(curve, 2 * t) / 2 : 1 - In(curve, 2 - 2 * t) / 2;
    }

    // The ease-in shape of a curve (0 at 0, 1 at 1).
    private static double In(byte curve, double t) => curve switch
    {
        AnimationEasing.Sine => 1 - Math.Cos(t * Math.PI / 2),
        AnimationEasing.Circular => 1 - Math.Sqrt(Math.Max(0, 1 - t * t)),
        AnimationEasing.Cubic => t * t * t,
        AnimationEasing.Quad => t * t,
        AnimationEasing.Bounce => 1 - BounceOut(1 - t),
        AnimationEasing.Elastic => t <= 0 ? 0 : t >= 1 ? 1 : -Math.Pow(2, 10 * (t - 1)) * Math.Sin((t - 1.075) * 2 * Math.PI / 0.3),
        AnimationEasing.Back => t * t * t - t * Math.Sin(t * Math.PI),
        _ => t,
    };

    private static double BounceOut(double t)
    {
        const double n = 7.5625, d = 2.75;
        if (t < 1 / d) return n * t * t;
        if (t < 2 / d) return n * (t -= 1.5 / d) * t + 0.75;
        if (t < 2.5 / d) return n * (t -= 2.25 / d) * t + 0.9375;
        return n * (t -= 2.625 / d) * t + 0.984375;
    }

    private static Vals FromBase(in AnimatedBase b)
        => new()
        {
            PX = b.PixelX, PY = b.PixelY, PZ = b.PixelZ, PW = b.PixelW, Alpha = b.Alpha,
            R = Channel(b.Rgba, 24), G = Channel(b.Rgba, 16), B = Channel(b.Rgba, 8), A = Channel(b.Rgba, 0), T = b.Transform,
        };

    // A track's start values (vars it does not name keep BYOND's defaults: they do not matter, the track does not change them).
    private static Vals From(in AnimatedVars s, Vals v)
    {
        v.R = v.G = v.B = v.A = 255;
        v.Alpha = 255;
        v.T = Affine.Identity;
        if (s.Has(AnimatedVar.PixelX)) v.PX = s.PixelX;
        if (s.Has(AnimatedVar.PixelY)) v.PY = s.PixelY;
        if (s.Has(AnimatedVar.PixelZ)) v.PZ = s.PixelZ;
        if (s.Has(AnimatedVar.PixelW)) v.PW = s.PixelW;
        if (s.Has(AnimatedVar.Alpha)) v.Alpha = s.Alpha;
        if (s.Has(AnimatedVar.MaptextX)) v.MX = s.MaptextX;
        if (s.Has(AnimatedVar.Color)) (v.R, v.G, v.B, v.A) = (Channel(s.Rgba, 24), Channel(s.Rgba, 16), Channel(s.Rgba, 8), Channel(s.Rgba, 0));
        if (s.Has(AnimatedVar.Transform)) v.T = TransformOf(s);
        return v;
    }

    private static Affine TransformOf(in AnimatedVars s) => new(s.A, s.B, s.C, s.D, s.E, s.F);

    private static double Channel(uint rgba, int shift) => (rgba >> shift) & 0xFF;
}
