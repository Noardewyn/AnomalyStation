using System.Linq;

namespace Anomaly.Client.Shared.Icons;

/// <summary>
/// When a composed appearance's picture changes (D142: BYOND's icon state animations in the 3D world): the start times of its
/// distinct pictures over one loop of every animated state in it, so the world draws each picture once in an atlas and steps the
/// instance through them. BYOND's rules: delays in deciseconds, <c>loop</c> times (0: forever, then the last frame stays),
/// <c>rewind</c> back and forth (<see cref="IIconState.FrameAt"/>). A <c>flick</c> plays its states once.
/// </summary>
public sealed class AnimationTimeline
{
    /// <summary>The most pictures one animation keeps; a longer one is thinned evenly.</summary>
    public const int MaxFrames = 32;

    /// <summary>The longest loop searched for the common period of several states; past it the longest state's loop is used.</summary>
    public const double MaxLoopSeconds = 10;

    private const double Epsilon = 1e-4;

    private AnimationTimeline(double[] starts, double length, bool loops)
    {
        Starts = starts;
        Length = length;
        Loops = loops;
    }

    /// <summary>The start of each picture in seconds, the first 0, increasing.</summary>
    public double[] Starts { get; }

    /// <summary>The loop's length (or the one pass's) in seconds.</summary>
    public double Length { get; }

    /// <summary>True when the pictures repeat; false when the animation ends on its last picture (a flick, a state with a loop count).</summary>
    public bool Loops { get; }

    /// <summary>The number of pictures.</summary>
    public int Count => Starts.Length;

    /// <summary>The time to compose picture <paramref name="index"/> at (a hair after its start, inside it).</summary>
    public double SampleAt(int index) => Starts[index] + Epsilon;

    /// <summary>The picture shown <paramref name="seconds"/> after the animation started.</summary>
    public int FrameAt(double seconds)
    {
        if (Starts.Length <= 1 || seconds <= 0)
            return 0;
        if (Loops)
            seconds %= Length;
        else if (seconds >= Length)
            return Starts.Length - 1;
        int index = Array.BinarySearch(Starts, seconds);
        return index >= 0 ? index : ~index - 1;
    }

    /// <summary>
    /// The timeline of the animated <paramref name="states"/> of one composed picture; null when nothing in it animates (and it is no
    /// flick). <paramref name="once"/>: a flick, which plays the states one time (a still state shows for one tick, BYOND's least delay).
    /// </summary>
    public static AnimationTimeline Of(IReadOnlyList<IIconState> states, bool once)
    {
        var animated = new List<(double[] Steps, double Cycle, int Loop)>();
        if (states != null)
            foreach (IIconState state in states)
                if (TryCycle(state, out double[] steps, out double cycle))
                    animated.Add((steps, cycle, once ? 1 : state.Loop));
        if (animated.Count == 0)
            return once ? new AnimationTimeline(new[] { 0.0 }, 0.1, false) : null;

        bool loops = animated.Exists(a => a.Loop == 0);
        double length = loops ? CommonLoop(animated.Where(a => a.Loop == 0).Select(a => a.Cycle).ToList()) : animated.Max(a => a.Cycle * a.Loop);
        var starts = new SortedSet<double> { 0 };
        foreach (var (steps, cycle, loop) in animated)
        {
            double end = loop == 0 ? length : Math.Min(length, cycle * loop);
            for (double from = 0; from < end - Epsilon; from += cycle)
                foreach (double step in steps)
                    if (from + step < end - Epsilon)
                        starts.Add(Math.Round(from + step, 4));
        }
        double[] list = starts.ToArray();
        if (list.Length > MaxFrames)
        {
            var thinned = new double[MaxFrames];
            for (int i = 0; i < MaxFrames; i++)
                thinned[i] = list[(int)((long)i * list.Length / MaxFrames)];
            list = thinned;
        }
        return new AnimationTimeline(list, length, loops);
    }

    // One state's frame starts within its cycle and the cycle's length in seconds (a rewinding state plays 0..n-1 and then n-2..1).
    private static bool TryCycle(IIconState state, out double[] steps, out double cycle)
    {
        steps = null;
        cycle = 0;
        if (state == null || state.Frames <= 1 || state.Delays == null || state.Delays.Length != state.Frames)
            return false;
        var order = new List<int>();
        for (int i = 0; i < state.Frames; i++)
            order.Add(i);
        if (state.Rewind && state.Frames > 2)
            for (int i = state.Frames - 2; i >= 1; i--)
                order.Add(i);
        steps = new double[order.Count];
        double t = 0;
        for (int k = 0; k < order.Count; k++)
        {
            steps[k] = t;
            t += Math.Max(0, state.Delays[order[k]]) / 10.0;
        }
        cycle = t;
        return cycle > 0;
    }

    // The shortest multiple of the longest cycle that every cycle divides, searched up to MaxLoopSeconds; else the longest cycle.
    private static double CommonLoop(List<double> cycles)
    {
        double longest = cycles.Max();
        for (double length = longest; length <= MaxLoopSeconds + Epsilon; length += longest)
            if (cycles.TrueForAll(c => Math.Abs(length / c - Math.Round(length / c)) < 1e-3))
                return length;
        return longest;
    }
}
