using System.Globalization;
using System.Linq;
using System.Text;
using Godot;
using Anomaly.Client.Light;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.World;
using Anomaly.Protocol;

namespace Anomaly.Client.Render;

/// <summary>
/// <c>--bench</c> (scope 2i, docs/ai/specs/apps.md 2.10): the camera flies a fixed path of MetaStation waypoints for a number of seconds per
/// preset, with vsync off, while the frame times, draw calls and primitives are recorded; the numbers go to a file and to the log. The
/// path is routed over the open cells (doors count as open) between <see cref="BenchPath.MetaWaypoints"/>, or, on a map that is not Meta, between
/// a few open cells spread over the map. It runs windowed at the resolution the process was started with (1920x1080 for the M3 numbers).
/// </summary>
public sealed class BenchRunner
{
    private const double WarmupSeconds = 2.0;

    private readonly ClientArgs _args;
    private readonly IWorldFeed _feed;
    private readonly MapRenderer _map;
    private readonly CameraRig _rig;
    private readonly Viewport _viewport;
    private readonly LightLayer _light;
    private readonly double _loadSeconds;
    private readonly RenderPreset _custom; // the options' own settings, for the "custom" preset (D288)
    private readonly double _startupSeconds;
    private readonly BenchTrack _track;
    private readonly List<string> _lines = new();
    private readonly List<double> _frames = new();
    private int _presetIndex = -1;
    private double _presetStart, _lastFrame;
    private long _drawCalls, _primitives, _objects;
    private int _samples;
    private int _maxDrawCalls;
    private string _preset;
    private int _lightsNow;
    private bool _finished;

    public BenchRunner(ClientArgs args, IWorldFeed feed, MapRenderer map, CameraRig rig, Viewport viewport, LightLayer light, double loadSeconds, RenderPreset custom = null)
    {
        _args = args;
        _feed = feed;
        _map = map;
        _rig = rig;
        _viewport = viewport;
        _light = light;
        _loadSeconds = loadSeconds;
        _custom = custom ?? RenderPreset.Medium with { Name = RenderPreset.CustomName };
        _startupSeconds = App.Instance.Uptime;
        _track = BuildTrack(feed, args, args.BenchSeconds);
        DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
        _rig.SetMode(CameraMode.Free);
        GD.Print(string.Create(CultureInfo.InvariantCulture, $"[anomaly] bench: path {_track.Length:0} m at {_track.Speed:0.0} m/s, {args.BenchSeconds:0}s per preset: {string.Join(", ", args.BenchPresets)}; window {_viewport.GetVisibleRect().Size}"));
    }

    /// <summary>True once every preset was flown and the file written.</summary>
    public bool Finished => _finished;

    /// <summary>Moves the camera and records the frame; call once per frame before the rig updates.</summary>
    public void Frame(double now)
    {
        if (_finished)
            return;
        if (_presetIndex < 0 || now - _presetStart >= _args.BenchSeconds + WarmupSeconds)
        {
            if (_presetIndex >= 0)
                EndPreset();
            _presetIndex++;
            if (_presetIndex >= _args.BenchPresets.Count)
            {
                Finish();
                return;
            }
            BeginPreset(now);
        }
        double t = now - _presetStart;
        BenchPose pose = _track.At(t % _args.BenchSeconds);
        _rig.PlaceFree(new Vector3(pose.X, TileSpace.EyeHeight, pose.Z));
        _rig.Yaw = pose.Yaw;
        _rig.Pitch = 0f;
        if (t >= WarmupSeconds)
        {
            _frames.Add(now - _lastFrame);
            long calls = (long)RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.TotalDrawCallsInFrame);
            _drawCalls += calls;
            _maxDrawCalls = Math.Max(_maxDrawCalls, (int)calls);
            _primitives += (long)RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.TotalPrimitivesInFrame);
            _objects += (long)RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.TotalObjectsInFrame);
            _samples++;
        }
        _lastFrame = now;
    }

    private void BeginPreset(double now)
    {
        _preset = _args.BenchPresets[_presetIndex];
        if (!RenderPreset.TryParse(_preset, out RenderPreset preset))
            preset = _custom;
        Quality.Apply(preset, _viewport, _rig.Camera, _light);
        _presetStart = now;
        _lastFrame = now;
        _frames.Clear();
        _drawCalls = _primitives = _objects = 0;
        _samples = 0;
        _maxDrawCalls = 0;
        _lightsNow = preset.ShadowedLights;
        GD.Print($"[anomaly] bench: preset {_preset} ({preset})");
    }

    private void EndPreset()
    {
        BenchStats stats = BenchStats.FromFrameTimes(_frames);
        int calls = _samples == 0 ? 0 : (int)(_drawCalls / _samples);
        long primitives = _samples == 0 ? 0 : _primitives / _samples;
        long objects = _samples == 0 ? 0 : _objects / _samples;
        string line = stats.Line(_preset, calls, primitives, _loadSeconds, objects) + $" max_draw_calls={_maxDrawCalls} shadowed_lights={_lightsNow} shadowed_now={_light?.ShadowedLightCount} lamps_now={_light?.LampCount}";
        _lines.Add(line);
        GD.Print("[anomaly] bench: " + line);
    }

    private void Finish()
    {
        _finished = true;
        var sb = new StringBuilder();
        sb.Append("# Anomaly Station --bench (docs/ai/specs/apps.md 2.10)\n");
        sb.Append(string.Create(CultureInfo.InvariantCulture, $"# renderer={RenderingServer.GetCurrentRenderingDriverName()} method={RenderingServer.GetCurrentRenderingMethod()} adapter={RenderingServer.GetVideoAdapterName()} window={_viewport.GetVisibleRect().Size} map={_feed.Map.Width}x{_feed.Map.Height} chunks={_map.ChunkCount} tiles_floor={_map.FloorCount} walls={_map.WallCount} props={_map.PropCount} pieces={_map.PieceCount} atlas={_map.AtlasEntries}@{_map.AtlasSize.X}x{_map.AtlasSize.Y} tile_layers={_map.TileLayers} load_s={_loadSeconds:0.00} startup_s={_startupSeconds:0.00}\n"));
        foreach (string line in _lines)
            sb.Append(line).Append('\n');
        string path = _args.BenchOut ?? "user://bench.txt";
        using FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        if (file == null)
            GD.PrintErr($"[anomaly] bench: could not write {path}: {FileAccess.GetOpenError()}");
        else
        {
            file.StoreString(sb.ToString());
            GD.Print($"[anomaly] bench: wrote {ProjectSettings.GlobalizePath(path)}");
        }
    }

    // The flight: Meta's waypoints routed through its corridors; anything else between open cells spread over the map.
    private static BenchTrack BuildTrack(IWorldFeed feed, ClientArgs args, double seconds)
    {
        ReplicaWorld replica = feed.Replica;
        MapGrid map = feed.Map;
        var doors = new HashSet<(int, int)>();
        foreach (ReplicaEntity e in feed.Entities)
        {
            if (PropKinds.IsDoor(e.TypePath ?? string.Empty))
                doors.Add((e.State.X, e.State.Y));
        }
        bool Passable(int x, int y) => !replica.IsSolid(x, y, 1) || doors.Contains((x, y));
        var waypoints = new List<(int X, int Y)>();
        if (args.BenchAt is { } spot)
        {
            // D168: a small square round one tile, to measure a light-dense spot.
            (int X, int Y)[] square = { (spot.X, spot.Y), (spot.X + 3, spot.Y), (spot.X + 3, spot.Y + 3), (spot.X, spot.Y + 3), (spot.X, spot.Y) };
            waypoints.AddRange(square.Where(w => Passable(w.X, w.Y)));
        }
        else if (map.Width == 255 && map.Height == 255 && BenchPath.MetaWaypoints.All(w => Passable(w.X, w.Y)))
            waypoints.AddRange(BenchPath.MetaWaypoints);
        else
        {
            var rng = new Random(7);
            for (int attempt = 0; attempt < 5000 && waypoints.Count < 12; attempt++)
            {
                int x = rng.Next(2, map.Width), y = rng.Next(2, map.Height);
                if (Passable(x, y) && !replica.IsSolid(x, y, 1))
                    waypoints.Add((x, y));
            }
        }
        List<(int X, int Y)> route = BenchPath.Route(map.Width, map.Height, Passable, waypoints);
        if (route.Count < 2)
            route = new List<(int X, int Y)> { (Math.Max(1, map.Width / 2), Math.Max(1, map.Height / 2)), (Math.Max(1, map.Width / 2) + 1, Math.Max(1, map.Height / 2)) };
        var points = route.Select(c => (TileSpace.WorldX(c.X), TileSpace.WorldZ(c.Y))).ToList();
        return new BenchTrack(points, seconds);
    }
}
