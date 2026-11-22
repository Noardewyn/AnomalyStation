namespace Anomaly.Protocol;

// Protocol version 9 (M4v slice 1, D143): BYOND's animate() reaches the clients (docs/ai/specs/protocol.md section 18). Version 26 (D156):
// a track may animate one of the atom's filters (MessagesV26.cs).

/// <summary>The animated vars a step or a track's start names (<see cref="AnimatedVars.Mask"/>), in wire order.</summary>
[Flags]
public enum AnimatedVar : byte
{
    None = 0,
    PixelX = 1,
    PixelY = 2,
    PixelZ = 4,
    PixelW = 8,
    Alpha = 16,
    /// <summary>BYOND <c>color</c> as 0xRRGGBBAA (a colour matrix is not sent: section 18.1).</summary>
    Color = 32,
    /// <summary>BYOND <c>transform</c>: the six numbers a to f of the 2D matrix.</summary>
    Transform = 64,
    /// <summary>Version 74 (gaps W1): BYOND <c>maptext_x</c>, pixels (tg's status display scrolls its long lines with it).</summary>
    MaptextX = 128,
    All = PixelX | PixelY | PixelZ | PixelW | Alpha | Color | Transform | MaptextX,
}

/// <summary>The flags of one step (BYOND's <c>flags</c> argument, the bits a client needs).</summary>
[Flags]
public enum AnimationStepFlags : byte
{
    None = 0,
    /// <summary>BYOND <c>ANIMATION_RELATIVE</c>: the step's numbers are added to the value before it (a transform or a colour multiplied).</summary>
    Relative = 1,
    /// <summary>BYOND <c>ANIMATION_LINEAR_TRANSFORM</c>: the transform's six numbers are interpolated one by one, not decomposed.</summary>
    LinearTransform = 2,
    All = Relative | LinearTransform,
}

/// <summary>BYOND's easing numbers (<c>LINEAR_EASING</c> 0 to <c>JUMP_EASING</c> 8, with <c>EASE_IN</c> 64 and <c>EASE_OUT</c> 128).</summary>
public static class AnimationEasing
{
    public const byte Linear = 0, Sine = 1, Circular = 2, Cubic = 3, Bounce = 4, Elastic = 5, Back = 6, Quad = 7, Jump = 8;
    public const byte EaseIn = 64, EaseOut = 128;

    /// <summary>The curve of an easing byte (its low four bits).</summary>
    public static byte Curve(byte easing) => (byte)(easing & 15);

    /// <summary>Whether the byte is one the codec accepts: a curve 0 to 8 and only the in and out bits beside it.</summary>
    public static bool IsValid(byte easing) => (easing & 0x30) == 0 && Curve(easing) <= Jump;
}

/// <summary>Values of the animated vars; <see cref="Mask"/> says which are set. Numbers are BYOND's (pixels, alpha 0 to 255).</summary>
public record struct AnimatedVars
{
    public AnimatedVar Mask;
    public float PixelX, PixelY, PixelZ, PixelW, Alpha;
    public uint Rgba;
    public float MaptextX;
    public float A, B, C, D, E, F;

    public readonly bool Has(AnimatedVar v) => (Mask & v) != 0;
}

/// <summary>One step of a chain: <c>animate(..., time, easing, flags, delay)</c>.</summary>
/// <param name="Time">BYOND's <c>time</c> in deciseconds (0: the step jumps).</param>
/// <param name="Delay">BYOND's <c>delay</c>: a pause before the step, in deciseconds.</param>
/// <param name="Easing">BYOND's <c>easing</c> (<see cref="AnimationEasing"/>).</param>
/// <param name="Flags">The step's flags.</param>
/// <param name="Vars">The values the step names: end values, or with <see cref="AnimationStepFlags.Relative"/> what it adds.</param>
/// <param name="Filter">Version 26: a filter track's step, the filter's parameters it names (<see cref="AnimationTrack.Filter"/>); then <paramref name="Vars"/> names none.</param>
public readonly record struct AnimationStep(float Time, float Delay, byte Easing, AnimationStepFlags Flags, AnimatedVars Vars, FilterVars Filter = default);

/// <summary>
/// One animation sequence of an atom: BYOND's chain of steps from one <c>animate(atom, ...)</c> call and the calls without an object
/// that follow it. A main track (not <see cref="Parallel"/>) replaced every animation before it; a parallel one adds its change to them.
/// </summary>
public sealed class AnimationTrack : IEquatable<AnimationTrack>
{
    /// <summary>The server tick the chain started in.</summary>
    public long StartTick { get; init; }

    /// <summary>BYOND's <c>loop</c>: how many times the chain plays (1 or more), 0 for ever.</summary>
    public int Loop { get; init; } = 1;

    /// <summary>BYOND <c>ANIMATION_PARALLEL</c> on the first step.</summary>
    public bool Parallel { get; init; }

    /// <summary>The values the chain starts from (the vars before the call, for every var a step names).</summary>
    public AnimatedVars Start { get; init; }

    /// <summary>The steps, 1 to <see cref="Limits.MaxAnimationSteps"/>.</summary>
    public AnimationStep[] Steps { get; init; } = Array.Empty<AnimationStep>();

    /// <summary>
    /// Version 26: 0 for the atom's own vars; 1 to <see cref="Limits.MaxAppearanceFilters"/> for a chain of <c>animate(filter, ...)</c>,
    /// the filter at that position of the appearance's filters (its steps name <see cref="AnimationStep.Filter"/>).
    /// </summary>
    public byte Filter { get; init; }

    /// <summary>Version 26: a filter track's start values (the filter's parameters before the call, for every one a step names).</summary>
    public FilterVars FilterStart { get; init; }

    /// <summary>
    /// Version 26: a screen entry's filter track of one of its <c>vis_contents</c> children, flattened into its picture: that child's
    /// appearance (the row of its overlay there); none for the entry's own (tg's health doll animates its limbs' outlines).
    /// Version 74 (gaps W1): also a track of a child's own vars, of a screen entry's or a world entity's child (tg's status display
    /// scrolls its text overlays' <c>maptext_x</c>); a client applies such a track to that child's maptext only.
    /// </summary>
    public TableRef Child { get; init; }

    public bool Equals(AnimationTrack other)
        => other != null && StartTick == other.StartTick && Loop == other.Loop && Parallel == other.Parallel && Start == other.Start
           && Filter == other.Filter && FilterStart == other.FilterStart && Child == other.Child && Steps.AsSpan().SequenceEqual(other.Steps);

    public override bool Equals(object obj) => Equals(obj as AnimationTrack);

    public override int GetHashCode() => HashCode.Combine(StartTick, Loop, Parallel, Steps.Length);
}

/// <summary>
/// <see cref="NetField.Animation"/> (version 9): the atom's running animations, 1 to <see cref="Limits.MaxAnimationTracks"/> tracks,
/// the main one (if any) first. A client plays them over the entity's appearance, which already holds their end values.
/// </summary>
public sealed class EntityAnimation : IEquatable<EntityAnimation>
{
    public AnimationTrack[] Tracks { get; init; } = Array.Empty<AnimationTrack>();

    public bool Equals(EntityAnimation other)
    {
        if (other == null || other.Tracks.Length != Tracks.Length) return false;
        for (int i = 0; i < Tracks.Length; i++)
            if (!Tracks[i].Equals(other.Tracks[i])) return false;
        return true;
    }

    public override bool Equals(object obj) => Equals(obj as EntityAnimation);

    public override int GetHashCode() => Tracks.Length;

    /// <summary>The length of one play of a track in deciseconds (its steps' delays and times).</summary>
    public static double CycleOf(AnimationTrack track)
    {
        double total = 0;
        foreach (AnimationStep step in track.Steps) total += step.Delay + step.Time;
        return total;
    }

    /// <summary>The last server tick any track still changes the picture in, or <see cref="long.MaxValue"/> while one loops for ever.</summary>
    public long EndTick(int ticksPerSecond)
    {
        long end = 0;
        foreach (AnimationTrack t in Tracks)
        {
            if (t.Loop == 0) return long.MaxValue;
            double ds = CycleOf(t) * t.Loop;
            end = Math.Max(end, t.StartTick + (long)Math.Ceiling(ds * ticksPerSecond / 10.0));
        }
        return end;
    }
}
