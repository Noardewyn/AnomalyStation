using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;

namespace Anomaly.Server;

// Version 84 (gaps E2a): the weather of the level a session's mob stands on (tg's SSweather.processing), after the Delta, when it changes:
// each running weather's stage, overlay state, colour and alpha, glow, the particle weathers' particles, severity and wind, and the cells of
// its impacted areas on that level. The cells are fixed when a weather is made (tg's setup_weather_areas), so they are gathered once per
// weather and level; the message is encoded again only when what it says changes. Read raw after the tick, as the minimap's trackers are.
public sealed partial class ServerHost
{
    private sealed class WeatherCells
    {
        public int MinX, MinY, Width, Height;
        public List<TurfRun> Runs;
    }

    private readonly ConditionalWeakTable<datum_weather, StrongBox<int>> _weatherIds = new();
    private readonly Dictionary<(datum_weather, int), WeatherCells> _weatherCells = new();
    private int _nextWeatherId;

    private void SendWeather(Session s)
    {
        using (World.Enter())
            SendWeatherInWorld(s);
    }

    private void SendWeatherInWorld(Session s)
    {
        int z = TurfOf(s.Client?.mob) is turf t ? (int)t.z : 0;
        if (z is < 1 or > Limits.MaxMapLevels)
            return;
        var layers = new List<(datum_weather Weather, WeatherLayer Layer)>();
        var key = new System.Text.StringBuilder().Append(z);
        datum_controller_subsystem_weather weather_subsystem = GlobalProcs.SSweather;
        foreach (datum_weather w in Dm.each<datum_weather>(weather_subsystem?.processing))
        {
            if (layers.Count >= Limits.MaxWeatherLayers)
                break;
            if (w.stage is < Defines.STARTUP_STAGE or > Defines.WIND_DOWN_STAGE || !Dm.@in(z, w.impacted_z_levels))
                continue;
            WeatherLayer layer = LayerOf(w);
            layers.Add((w, layer));
            key.Append('|').Append(layer.Id).Append(',').Append(layer.Stage).Append(',').Append(layer.State).Append(',').Append(layer.Colour)
                .Append(',').Append(layer.Alpha).Append(',').Append(layer.Glow).Append(',').Append(layer.Particles).Append(',').Append(layer.Severity)
                .Append(',').Append(layer.Wind);
        }
        string signature = key.ToString();
        if (signature == s.WeatherKey)
            return;
        bool shown = s.WeatherKey != null && s.WeatherKey.Contains('|');
        s.WeatherKey = signature;
        if (layers.Count == 0 && !shown)
            return; // clear skies the client already has
        var message = new WeatherMessage { Z = z };
        foreach ((datum_weather w, WeatherLayer layer) in layers)
        {
            WeatherCells cells = CellsOf(w, z);
            layer.MinX = cells.MinX;
            layer.MinY = cells.MinY;
            layer.Width = cells.Width;
            layer.Height = cells.Height;
            layer.Cells.AddRange(cells.Runs);
            message.Layers.Add(layer);
        }
        Send(s, Channel.State, Codec.Encode(message));
        if (layers.Count == 0)
            ForgetEndedWeather(weather_subsystem);
    }

    private WeatherLayer LayerOf(datum_weather w)
    {
        int stage = (int)w.stage;
        object state = stage switch
        {
            Defines.STARTUP_STAGE => w.telegraph_overlay,
            Defines.MAIN_STAGE => w.weather_overlay,
            _ => w.end_overlay,
        };
        var layer = new WeatherLayer
        {
            Id = _weatherIds.GetValue(w, _ => new StrongBox<int>(++_nextWeatherId)).Value,
            Stage = stage,
            State = CutWeather(state as string ?? ""),
            Colour = w.weather_color is string colour ? WeatherRgba(colour) : 0u,
            Alpha = Math.Clamp((int)MathF.Round(w.weather_alpha), 0, 255),
            Glow = Dm.truthy(w.use_glow),
        };
        if (w is datum_weather_particle particle)
        {
            Type set = particle.particle_type ?? particle.emissive_type;
            string path = set?.GetCustomAttribute<DmPathAttribute>()?.Path ?? "";
            layer.Particles = CutWeather(path[(path.LastIndexOf('/') + 1)..]);
            layer.Severity = Math.Clamp((int)MathF.Round(particle.severity), 0, 100);
            layer.Wind = Math.Sign((int)particle.wind_sign);
        }
        return layer;
    }

    private static string CutWeather(string text) => System.Text.Encoding.UTF8.GetByteCount(text) > Limits.MaxWeatherStateBytes ? text[..(Limits.MaxWeatherStateBytes / 4)] : text; // 4 bytes a char at most

    // The impacted areas' turfs on the level (tg's turfs_by_zlevel), as runs of 0 and 1 over their box
    private WeatherCells CellsOf(datum_weather w, int z)
    {
        if (_weatherCells.TryGetValue((w, z), out WeatherCells known))
            return known;
        var covered = new List<(int X, int Y)>();
        foreach (area a in Dm.each<area>(w.impacted_areas))
        {
            if (a.turfs_by_zlevel == null || a.turfs_by_zlevel.len < z || a.turfs_by_zlevel[z] is not DmList turfs)
                continue;
            foreach (turf t in Dm.each<turf>(turfs))
                covered.Add(((int)t.x, (int)t.y));
        }
        var cells = new WeatherCells { MinX = 1, MinY = 1, Width = 1, Height = 1, Runs = new List<TurfRun>() };
        if (covered.Count > 0)
        {
            cells.MinX = covered.Min(c => c.X);
            cells.MinY = covered.Min(c => c.Y);
            cells.Width = covered.Max(c => c.X) - cells.MinX + 1;
            cells.Height = covered.Max(c => c.Y) - cells.MinY + 1;
            var grid = new int[cells.Width * cells.Height];
            foreach ((int x, int y) in covered)
                grid[(y - cells.MinY) * cells.Width + (x - cells.MinX)] = 1;
            AddMinimapRuns(cells.Runs, grid);
        }
        _weatherCells[(w, z)] = cells;
        return cells;
    }

    // a level with no weather left: the gathered cells of the weathers that ended are dropped
    private void ForgetEndedWeather(datum_controller_subsystem_weather weather_subsystem)
    {
        foreach ((datum_weather, int) entry in _weatherCells.Keys.ToList())
        {
            if (!Dm.@in(entry.Item1, weather_subsystem?.processing))
                _weatherCells.Remove(entry);
        }
    }

    // BYOND colour text ("green", "#rrggbb") as 0xRRGGBBAA; 0 (none) for a text BYOND cannot read
    private static uint WeatherRgba(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;
        try
        {
            DmList parts = Dm.rgb2num(text);
            if (parts.Count < 3)
                return 0;
            uint Part(int i) => (uint)Math.Clamp((int)MathF.Round(Dm.num(parts[(float)i])), 0, 255);
            return (Part(1) << 24) | (Part(2) << 16) | (Part(3) << 8) | (parts.Count >= 4 ? Part(4) : 255u);
        }
        catch (DmRuntimeException)
        {
            return 0;
        }
    }
}
