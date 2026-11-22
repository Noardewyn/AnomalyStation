using System.Linq;
using System.Globalization;
using System.Text;

namespace Anomaly.Client.Shared.World;

// Owner: M3 P6. Engine-free. The --bench flight (scope 2i): a fixed path of waypoints on MetaStation flown at a constant pace for a
// fixed time, and the statistics of the frames drawn on it.

/// <summary>A point of the flight: metres in the client's space and the camera yaw (Godot's: 0 faces north, -Z).</summary>
public readonly record struct BenchPose(float X, float Z, float Yaw);

/// <summary>The flown track.</summary>
public sealed class BenchTrack
{
    private readonly (float X, float Z)[] _points;
    private readonly float[] _cumulative;

    /// <summary>The track through <paramref name="points"/> (metres), smoothed, flown in <paramref name="seconds"/>.</summary>
    public BenchTrack(IReadOnlyList<(float X, float Z)> points, double seconds)
    {
        if (points.Count < 2)
            throw new ArgumentException("a track needs two points", nameof(points));
        Seconds = seconds;
        _points = Smooth(points, 3);
        _cumulative = new float[_points.Length];
        for (int i = 1; i < _points.Length; i++)
            _cumulative[i] = _cumulative[i - 1] + MathF.Sqrt(Sq(_points[i].X - _points[i - 1].X) + Sq(_points[i].Z - _points[i - 1].Z));
        Length = _cumulative[^1];
    }

    /// <summary>The flight's length in seconds.</summary>
    public double Seconds { get; }

    /// <summary>The track's length in metres.</summary>
    public float Length { get; }

    /// <summary>The pace in metres per second.</summary>
    public float Speed => (float)(Length / Seconds);

    /// <summary>Where the camera is <paramref name="t"/> seconds into the flight (clamped to the ends).</summary>
    public BenchPose At(double t)
    {
        float distance = (float)(Math.Clamp(t / Seconds, 0, 1) * Length);
        int i = Array.BinarySearch(_cumulative, distance);
        if (i < 0)
            i = ~i - 1;
        i = Math.Clamp(i, 0, _points.Length - 2);
        float span = _cumulative[i + 1] - _cumulative[i];
        float f = span <= 1e-6f ? 0f : (distance - _cumulative[i]) / span;
        float x = _points[i].X + (_points[i + 1].X - _points[i].X) * f, z = _points[i].Z + (_points[i + 1].Z - _points[i].Z) * f;
        // The heading looks a few metres ahead so that it turns smoothly.
        int j = i + 1;
        while (j < _points.Length - 1 && _cumulative[j] - distance < 3f)
            j++;
        float dx = _points[j].X - x, dz = _points[j].Z - z;
        float yaw = MathF.Abs(dx) + MathF.Abs(dz) < 1e-4f ? 0f : MathF.Atan2(-dx, -dz);
        return new BenchPose(x, z, yaw);
    }

    private static float Sq(float v) => v * v;

    private static (float, float)[] Smooth(IReadOnlyList<(float X, float Z)> points, int radius)
    {
        var result = new (float, float)[points.Count];
        for (int i = 0; i < points.Count; i++)
        {
            float sx = 0, sz = 0;
            int n = 0;
            for (int k = Math.Max(0, i - radius); k <= Math.Min(points.Count - 1, i + radius); k++)
            {
                sx += points[k].X;
                sz += points[k].Z;
                n++;
            }
            result[i] = (sx / n, sz / n);
        }
        return result;
    }
}

/// <summary>Routes between waypoints over the map's open cells.</summary>
public static class BenchPath
{
    /// <summary>
    /// MetaStation's bench waypoints, BYOND (x, y): the arrivals area, the bridge, medbay, cargo, engineering and maintenance and the
    /// halls between (read off the converted map's areas; a test checks that each is an open cell of Meta when the map is at hand).
    /// </summary>
    public static readonly (int X, int Y)[] MetaWaypoints =
    {
        (45, 133), (65, 133), (66, 119), (71, 151), (81, 146), (82, 99), (88, 93), (129, 93), (122, 105), (99, 121),
        (107, 134), (107, 116), (131, 127), (140, 125), (145, 137), (142, 163), (157, 154), (172, 156), (175, 114), (103, 169),
    };

    /// <summary>The cells of a 4-connected shortest route through <paramref name="waypoints"/> over <paramref name="passable"/> cells; a leg with no route is skipped.</summary>
    public static List<(int X, int Y)> Route(int width, int height, Func<int, int, bool> passable, IReadOnlyList<(int X, int Y)> waypoints)
    {
        var route = new List<(int, int)>();
        for (int i = 0; i + 1 < waypoints.Count; i++)
        {
            List<(int, int)> leg = Bfs(width, height, passable, waypoints[i], waypoints[i + 1]);
            if (leg == null)
                continue;
            if (route.Count > 0)
                leg.RemoveAt(0);
            route.AddRange(leg);
        }
        return route;
    }

    private static List<(int, int)> Bfs(int width, int height, Func<int, int, bool> passable, (int X, int Y) from, (int X, int Y) to)
    {
        if (!passable(from.X, from.Y) || !passable(to.X, to.Y))
            return null;
        var previous = new int[width * height];
        Array.Fill(previous, -2);
        int Index(int x, int y) => (y - 1) * width + (x - 1);
        var queue = new Queue<(int, int)>();
        queue.Enqueue(from);
        previous[Index(from.X, from.Y)] = -1;
        ReadOnlySpan<(int, int)> steps = stackalloc (int, int)[] { (1, 0), (-1, 0), (0, 1), (0, -1) };
        while (queue.Count > 0)
        {
            (int x, int y) = queue.Dequeue();
            if ((x, y) == to)
            {
                var path = new List<(int, int)>();
                int at = Index(x, y);
                while (at >= 0)
                {
                    path.Add((at % width + 1, at / width + 1));
                    at = previous[at];
                }
                path.Reverse();
                return path;
            }
            foreach ((int dx, int dy) in steps)
            {
                int nx = x + dx, ny = y + dy;
                if (nx < 1 || ny < 1 || nx > width || ny > height || previous[Index(nx, ny)] != -2 || !passable(nx, ny))
                    continue;
                previous[Index(nx, ny)] = Index(x, y);
                queue.Enqueue((nx, ny));
            }
        }
        return null;
    }
}

/// <summary>Frame statistics of a bench run.</summary>
public sealed class BenchStats
{
    /// <summary>Average frames per second over the run.</summary>
    public double AvgFps { get; init; }

    /// <summary>The median frame's rate.</summary>
    public double P50Fps { get; init; }

    /// <summary>The rate of the 99th percentile frame time (the "1% low").</summary>
    public double P99Fps { get; init; }

    /// <summary>The worst frame's rate.</summary>
    public double MinFps { get; init; }

    public int Frames { get; init; }

    public double Seconds { get; init; }

    /// <summary>Statistics of frame durations in seconds.</summary>
    public static BenchStats FromFrameTimes(IReadOnlyList<double> frameSeconds)
    {
        if (frameSeconds.Count == 0)
            return new BenchStats();
        double[] sorted = frameSeconds.OrderBy(v => v).ToArray();
        double total = sorted.Sum();
        double At(double q) => sorted[Math.Min(sorted.Length - 1, (int)Math.Floor(q * (sorted.Length - 1) + 0.5))];
        return new BenchStats
        {
            Frames = sorted.Length,
            Seconds = total,
            AvgFps = sorted.Length / total,
            P50Fps = 1.0 / At(0.5),
            P99Fps = 1.0 / At(0.99),
            MinFps = 1.0 / sorted[^1],
        };
    }

    /// <summary>One line of the bench file for a preset.</summary>
    public string Line(string preset, int drawCalls, long primitives, double loadSeconds, long objects)
        => string.Create(CultureInfo.InvariantCulture,
            $"preset={preset} avg_fps={AvgFps:0.0} p50_fps={P50Fps:0.0} p99_fps={P99Fps:0.0} min_fps={MinFps:0.0} frames={Frames} seconds={Seconds:0.0} draw_calls={drawCalls} primitives={primitives} objects={objects} load_s={loadSeconds:0.00}");
}
