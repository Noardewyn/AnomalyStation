using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;

namespace Anomaly.Client.Dev;

/// <summary>
/// The Tracy profiler's client (https://github.com/wolfpld/tracy), loaded only when the environment variable <c>ANOMALY_TRACY</c> names its
/// library (<c>tools/perf/tracy-client.sh</c> builds it; docs/testing.md, "Profiling"), or is <c>engine</c>: a Godot built with its own
/// Tracy (<c>profiler=tracy</c>) and the C API exported, whose client is the engine's (one capture holds both the engine's zones and the
/// game's; the engine marks the frames and owns the client's lifetime). Through its C API: zones (every <see cref="FrameProbe.Scope"/> is
/// one), the frame mark, plots and messages. Off, every call is one branch.
/// </summary>
public static unsafe class Tracy
{
    private static bool _on;

    public static bool On => _on;

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct Zone
    {
        public readonly uint Id;
        public readonly int Active;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SourceLocation
    {
        public nint Name;
        public nint Function;
        public nint File;
        public uint Line;
        public uint Color;
    }

    private static readonly delegate* unmanaged[Cdecl]<void> Startup;
    private static readonly delegate* unmanaged[Cdecl]<void> Shutdown;
    private static readonly delegate* unmanaged[Cdecl]<SourceLocation*, int, Zone> ZoneBegin;
    private static readonly delegate* unmanaged[Cdecl]<Zone, void> ZoneEnd;
    private static readonly delegate* unmanaged[Cdecl]<Zone, byte*, nuint, void> ZoneText;
    private static readonly delegate* unmanaged[Cdecl]<Zone, ulong, void> ZoneValue;
    private static readonly delegate* unmanaged[Cdecl]<byte*, void> FrameMarkFn;
    private static readonly delegate* unmanaged[Cdecl]<byte*, double, void> PlotFn;
    private static readonly delegate* unmanaged[Cdecl]<byte*, int, int, int, uint, void> PlotConfig;
    private static readonly delegate* unmanaged[Cdecl]<byte*, nuint, int, void> MessageFn;
    private static readonly delegate* unmanaged[Cdecl]<byte*, void> ThreadName;
    private static readonly bool OwnFrames; // false with the engine's client: Godot marks its frames itself

    /// <summary>The engine's own client (<c>ANOMALY_TRACY=engine</c>): Godot zones its own passes and marks its frames.</summary>
    public static bool InEngine => _on && !OwnFrames;

    // Tracy keeps the pointers it is given (a zone's source location, a plot's name), so each lives in native memory for the run
    private static readonly ConcurrentDictionary<string, nint> Locations = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, nint> Names = new(StringComparer.Ordinal);
    private static readonly nint File = Utf8("godot/scripts");

    static Tracy()
    {
        string path = Environment.GetEnvironmentVariable("ANOMALY_TRACY");
        if (string.IsNullOrEmpty(path))
            return;
        try
        {
            bool engine = path == "engine";
            nint lib = engine ? NativeLibrary.GetMainProgramHandle() : NativeLibrary.Load(path);
            nint F(string name) => NativeLibrary.GetExport(lib, name);
            // the start and the stop exist only in a client built for a manual lifetime (tracy-client.sh's), not in the engine's
            Startup = NativeLibrary.TryGetExport(lib, "___tracy_startup_profiler", out nint startup) ? (delegate* unmanaged[Cdecl]<void>)startup : null;
            Shutdown = NativeLibrary.TryGetExport(lib, "___tracy_shutdown_profiler", out nint shutdown) ? (delegate* unmanaged[Cdecl]<void>)shutdown : null;
            OwnFrames = !engine;
            ZoneBegin = (delegate* unmanaged[Cdecl]<SourceLocation*, int, Zone>)F("___tracy_emit_zone_begin");
            ZoneEnd = (delegate* unmanaged[Cdecl]<Zone, void>)F("___tracy_emit_zone_end");
            ZoneText = (delegate* unmanaged[Cdecl]<Zone, byte*, nuint, void>)F("___tracy_emit_zone_text");
            ZoneValue = (delegate* unmanaged[Cdecl]<Zone, ulong, void>)F("___tracy_emit_zone_value");
            FrameMarkFn = (delegate* unmanaged[Cdecl]<byte*, void>)F("___tracy_emit_frame_mark");
            PlotFn = (delegate* unmanaged[Cdecl]<byte*, double, void>)F("___tracy_emit_plot");
            PlotConfig = (delegate* unmanaged[Cdecl]<byte*, int, int, int, uint, void>)F("___tracy_emit_plot_config");
            MessageFn = (delegate* unmanaged[Cdecl]<byte*, nuint, int, void>)F("___tracy_emit_message");
            ThreadName = (delegate* unmanaged[Cdecl]<byte*, void>)F("___tracy_set_thread_name");
            if (Startup != null)
                Startup();
            _on = true;
            ThreadName((byte*)Utf8("main"));
            Godot.GD.Print($"[anomaly] tracy: the profiler's client is on ({path}); connect tracy-profiler or tracy-capture to localhost");
        }
        catch (Exception e)
        {
            Godot.GD.PushWarning($"[anomaly] tracy: cannot load {path}: {e.Message}");
        }
    }

    private static nint Utf8(string s)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(s + "\0");
        nint p = Marshal.AllocHGlobal(bytes.Length);
        Marshal.Copy(bytes, 0, p, bytes.Length);
        return p;
    }

    private static nint Location(string name) => Locations.GetOrAdd(name, static n =>
    {
        var loc = (SourceLocation*)NativeMemory.AllocZeroed((nuint)sizeof(SourceLocation));
        loc->Name = Utf8(n);
        loc->Function = loc->Name;
        loc->File = File;
        return (nint)loc;
    });

    private static byte* Name(string name) => (byte*)Names.GetOrAdd(name, static n => Utf8(n));

    public static Zone Begin(string name) => On ? ZoneBegin((SourceLocation*)Location(name), 1) : default;

    public static void End(Zone zone)
    {
        if (On)
            ZoneEnd(zone);
    }

    /// <summary>A text on the zone (what it worked on: a type path, a file).</summary>
    public static void Text(Zone zone, string text)
    {
        if (!On || text == null)
            return;
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        fixed (byte* p = bytes)
            ZoneText(zone, p, (nuint)bytes.Length);
    }

    public static void Value(Zone zone, ulong value)
    {
        if (On)
            ZoneValue(zone, value);
    }

    public static void FrameMark()
    {
        if (On && OwnFrames)
            FrameMarkFn(null);
    }

    public enum PlotFormat
    {
        Number = 0,
        Memory = 1,
        Percentage = 2,
    }

    /// <summary>How a plot is drawn: <paramref name="step"/> holds each value until the next instead of a line between them.</summary>
    public static void Configure(string plot, PlotFormat format, bool step = true)
    {
        if (On)
            PlotConfig(Name(plot), (int)format, step ? 1 : 0, 1, 0);
    }

    public static void Plot(string plot, double value)
    {
        if (On)
            PlotFn(Name(plot), value);
    }

    public static void Message(string text)
    {
        if (!On || text == null)
            return;
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        fixed (byte* p = bytes)
            MessageFn(p, (nuint)bytes.Length, 0);
    }

    /// <summary>At the program's end: sends what is left to a connected profiler.</summary>
    public static void Stop()
    {
        if (!_on)
            return;
        _on = false;
        if (Shutdown != null)
            Shutdown();
    }
}
