using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Godot;

namespace Anomaly.Client.Dev;

/// <summary>
/// A hitch probe (env <c>ANOMALY_FRAMEPROBE=1</c>, threshold in ms by <c>ANOMALY_FRAMEPROBE_MS</c>, default 40): the per-frame time of named
/// script sections, and for every frame longer than the threshold one log line with the sections, the engine's process time and the
/// rest (rendering, uploads, pipeline compiles). Off, every call is one branch.
/// </summary>
public static class FrameProbe
{
    public static readonly bool On = System.Environment.GetEnvironmentVariable("ANOMALY_FRAMEPROBE") == "1";
    private static readonly double ThresholdMs = double.TryParse(System.Environment.GetEnvironmentVariable("ANOMALY_FRAMEPROBE_MS"), out double t) ? t : 40;
    private static readonly Dictionary<string, (double Ms, int Count)> Sections = new();
    private static long _frameStart;
    private static ulong _frame;
    private static (int Gen0, int Gen1, int Gen2) _gc;
    private static double _lastFrameMs;

    public readonly struct Section : IDisposable
    {
        private readonly string _name;
        private readonly long _start;

        public Section(string name)
        {
            _name = name;
            _start = Stopwatch.GetTimestamp();
        }

        public void Dispose()
        {
            if (_name == null)
                return;
            double ms = Stopwatch.GetElapsedTime(_start).TotalMilliseconds;
            Sections.TryGetValue(_name, out var v);
            Sections[_name] = (v.Ms + ms, v.Count + 1);
        }
    }

    static FrameProbe()
    {
        if (On)
            Anomaly.Client.Shared.Icons.IconLibrary.ProbeLoaded = (resource, ms) =>
            {
                if (!resource.EndsWith("(background)", StringComparison.Ordinal)) // a worker thread's: not this frame's time
                    Add("icon load", ms);
                if (ms > 3)
                    GD.Print($"[frameprobe] icon {resource} loaded in {ms:0.0} ms");
            };
    }

    public static Section Scope(string name) => On ? new Section(name) : default;

    /// <summary>Adds <paramref name="ms"/> to a section measured elsewhere.</summary>
    public static void Add(string name, double ms, int count = 1)
    {
        if (!On)
            return;
        Sections.TryGetValue(name, out var v);
        Sections[name] = (v.Ms + ms, v.Count + count);
    }

    /// <summary>Called first in the game scene's frame: closes the last frame (logged when it was long) and opens this one.</summary>
    public static void BeginFrame()
    {
        if (!On)
            return;
        long now = Stopwatch.GetTimestamp();
        if (_frameStart != 0)
        {
            double wall = _lastFrameMs = Stopwatch.GetElapsedTime(_frameStart, now).TotalMilliseconds;
            if (wall >= ThresholdMs)
            {
                var sb = new StringBuilder();
                double scripts = 0;
                sb.Append($"[frameprobe] frame {_frame}: {wall:0.0} ms;");
                var list = new List<KeyValuePair<string, (double Ms, int Count)>>(Sections);
                list.Sort((a, b) => b.Value.Ms.CompareTo(a.Value.Ms));
                foreach (var kv in list)
                {
                    if (!kv.Key.Contains('/'))
                        scripts += kv.Value.Ms;
                    if (kv.Value.Ms >= 0.5)
                        sb.Append($" {kv.Key} {kv.Value.Ms:0.0}" + (kv.Value.Count > 1 ? $"x{kv.Value.Count}" : ""));
                }
                double process = Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000;
                sb.Append($"; top-level sections {scripts:0.0}, engine process {process:0.0}, rest {wall - scripts:0.0}");
                sb.Append($"; objects {Performance.GetMonitor(Performance.Monitor.ObjectNodeCount)} nodes, draws {Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame)}");
                sb.Append($"; gc {GC.CollectionCount(0) - _gc.Gen0}/{GC.CollectionCount(1) - _gc.Gen1}/{GC.CollectionCount(2) - _gc.Gen2}");
                GD.Print(sb.ToString());
            }
        }
        _gc = (GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2)); // the collections of each generation in the frame
        if (_frame > 60) // the join's frames left out, as in the summary
            foreach (var kv in Sections)
            {
                RunSections.TryGetValue(kv.Key, out double ms);
                RunSections[kv.Key] = ms + kv.Value.Ms;
            }
        Sections.Clear();
        _frameStart = now;
        _frame++;
    }

    private static readonly Dictionary<string, double> RunSections = new();

    private static readonly List<float> FrameMs = new();
    private static readonly List<int> FrameDraws = new();

    /// <summary>Records the last frame's wall time for <see cref="Summary"/> (not <c>_Process</c>'s delta, which Godot clamps at about 140 ms) and the draw calls.</summary>
    public static void Record()
    {
        if (!On)
            return;
        FrameMs.Add((float)_lastFrameMs);
        FrameDraws.Add((int)Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame));
    }

    /// <summary>
    /// The whole run's frames (tools/perf/walk.sh reads it): the frame time percentiles and maximum, the frames over 33, 50 and 100 ms,
    /// the draw calls' median and maximum; the first 60 frames (the join) are left out.
    /// </summary>
    public static string Summary()
    {
        if (!On || FrameMs.Count <= 60)
            return null;
        var ms = FrameMs.GetRange(60, FrameMs.Count - 60);
        var draws = FrameDraws.GetRange(60, FrameDraws.Count - 60);
        double seconds = 0;
        foreach (float f in ms)
            seconds += f / 1000.0;
        ms.Sort();
        draws.Sort();
        float At(double q) => ms[Math.Min(ms.Count - 1, (int)(q * ms.Count))];
        int Over(float limit) => ms.Count - ms.FindLastIndex(f => f <= limit) - 1;
        var c = System.Globalization.CultureInfo.InvariantCulture;
        // each section's time a frame on average (ms), the top ones: where a steady frame's script time goes
        string sections = string.Join(" ", RunSections.Where(kv => !kv.Key.Contains(' ')).OrderByDescending(kv => kv.Value).Take(14)
            .Select(kv => string.Create(c, $"{kv.Key}={kv.Value / ms.Count:0.00}")));
        return string.Create(c,
            $"frames={ms.Count} seconds={seconds:0.0} avg_fps={ms.Count / seconds:0.0} p50_ms={At(0.5):0.0} p95_ms={At(0.95):0.0} p99_ms={At(0.99):0.0} max_ms={ms[^1]:0.0} over33={Over(33)} over50={Over(50)} over100={Over(100)} draws_p50={draws[draws.Count / 2]} draws_max={draws[^1]} | per frame: {sections}");
    }

    /// <summary>
    /// A step of a long synchronous job (the world's build) with the time since its start, as one log line, with the queues' items since the last
    /// step (<see cref="Item"/>: each kind's total and count, and the slowest named ones).
    /// </summary>
    public static void Mark(string name, Stopwatch since)
    {
        if (!On)
            return;
        var sb = new StringBuilder(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"[frameprobe] {name} at {since.Elapsed.TotalMilliseconds:0} ms;"));
        var list = new List<KeyValuePair<string, (double Ms, int Count)>>(Sections);
        list.Sort((a, b) => b.Value.Ms.CompareTo(a.Value.Ms));
        foreach (var kv in list.Take(12))
            sb.Append(string.Create(System.Globalization.CultureInfo.InvariantCulture, $" {kv.Key} {kv.Value.Ms:0}x{kv.Value.Count}"));
        GD.Print(sb.ToString());
        Sections.Clear();
    }

    /// <summary>One item of a budgeted queue that took <paramref name="ms"/> (over 2 ms: named in the long frame's line, so a thing that alone blows the budget shows).</summary>
    public static void Item(string queue, string what, double ms)
    {
        if (!On)
            return;
        Add($"{queue}/all", ms);
        if (ms > 2)
            Add($"{queue} {what}", ms);
    }
}
