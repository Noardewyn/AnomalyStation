using System.Globalization;
using System.Linq;
using System.Text;

namespace Anomaly.Client.Shared;

/// <summary>
/// The dev menu's settings (the owner, 2026-10-14: "save the streams' settings and the other settings in the options file"):
/// the airflow streams (D141), the audio (M4b), the gas haze and the gas overlay, the cell coordinates, the ceiling (F2) and the names overlay
/// (F4), and the spawn and gas tools' choices. In the options file as <c>dev.NAME=VALUE</c> lines (<see cref="PlayOptions"/>);
/// the menu saves them when a control is let go. What a tool aims at (the selected type, the armed tool) is not a setting.
/// </summary>
public sealed class DevMenuOptions
{
    public bool Airflow { get; set; } = true;
    public int AirflowMaxStreams { get; set; } = 880;
    public int AirflowMaxPerHole { get; set; } = 126;
    public int AirflowMaxPerVent { get; set; } = 37;
    public int AirflowSpawnDistance { get; set; } = 20;
    public bool AirflowDebug { get; set; }

    public bool GasHaze { get; set; } = true;

    public bool GasOverlay { get; set; }

    /// <summary>mix, gas, pressure or temperature.</summary>
    public string GasOverlayMode { get; set; } = "mix";

    /// <summary>tg's id of the gas the overlay's "one gas" mode shows.</summary>
    public string GasOverlayGas { get; set; } = "plasma";

    public int GasOverlayOpacityPercent { get; set; } = 50;
    public bool GasOverlayLabels { get; set; } = true;
    public int GasOverlayLabelRadius { get; set; } = 3;

    public bool CellCoordinates { get; set; }
    public int CellCoordinatesRadius { get; set; } = 6;

    public bool Ceiling { get; set; } = true;
    public bool Fullbright { get; set; }
    public bool MinimapMobs { get; set; } // D315: the minimap's trackers
    public bool PlayerMarkers { get; set; } // the markers over the players in the world (the same trackers)
    public bool NamesOverlay { get; set; }

    public bool SpawnMapping { get; set; }
    public bool SpawnAbstract { get; set; }
    public int SpawnAmount { get; set; } = 1;
    public bool SpawnStick { get; set; }
    public bool SpawnFavouritesOnly { get; set; }

    /// <summary>The spawn panel's favourite types of each tab (objects, turfs, mobs), their paths comma-separated and sorted (the owner, 2026-11-10).</summary>
    public string[] SpawnFavourites { get; set; } = { "", "", "" };

    private static readonly string[] FavouriteTabs = { "objects", "turfs", "mobs" };

    public float GasMoles { get; set; } = 50f;
    public float GasKelvin { get; set; } = 293.15f;
    public int GasRadius { get; set; }

    /// <summary>M4b audio (apps.md 2.20): the master volume in percent, the mute, DV39's occlusion factor k, the muffled bus's
    /// low-pass cutoff and the max(muffle, occlusion) from which a sound goes through it.</summary>
    public int AudioVolumePercent { get; set; } = 100;
    public bool AudioMute { get; set; }
    public float AudioOcclusionFactor { get; set; } = 0.65f;
    public float AudioLowPassHz { get; set; } = 900f;
    public float AudioMuffleThreshold { get; set; } = 0.5f;
    public float AudioReverbWet { get; set; } = Anomaly.Client.Shared.Audio.ReverbPresets.DefaultWetScale;

    public static readonly string[] OverlayModes = { "mix", "gas", "pressure", "temperature" };

    /// <summary>A copy.</summary>
    public DevMenuOptions Clone()
    {
        var copy = (DevMenuOptions)MemberwiseClone();
        copy.SpawnFavourites = (string[])SpawnFavourites.Clone();
        return copy;
    }

    /// <summary>A set of paths as a favourites value: sorted, comma-separated.</summary>
    public static string JoinPaths(IEnumerable<string> paths) => string.Join(',', paths.OrderBy(p => p, StringComparer.Ordinal));

    /// <summary>The paths of a favourites value.</summary>
    public static IEnumerable<string> SplitPaths(string value) => (value ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>The <c>dev.</c> lines of the options file.</summary>
    public void AppendTo(StringBuilder sb)
    {
        var c = CultureInfo.InvariantCulture;
        void Line(string name, string value) => sb.Append("dev.").Append(name).Append('=').Append(value).Append('\n');
        string B(bool v) => v ? "true" : "false";
        Line("airflow", B(Airflow));
        Line("airflow_max_streams", AirflowMaxStreams.ToString(c));
        Line("airflow_max_per_hole", AirflowMaxPerHole.ToString(c));
        Line("airflow_max_per_vent", AirflowMaxPerVent.ToString(c));
        Line("airflow_spawn_distance", AirflowSpawnDistance.ToString(c));
        Line("airflow_debug", B(AirflowDebug));
        Line("gas_haze", B(GasHaze));
        Line("gas_overlay", B(GasOverlay));
        Line("gas_overlay_mode", GasOverlayMode);
        Line("gas_overlay_gas", GasOverlayGas);
        Line("gas_overlay_opacity", GasOverlayOpacityPercent.ToString(c));
        Line("gas_overlay_labels", B(GasOverlayLabels));
        Line("gas_overlay_label_radius", GasOverlayLabelRadius.ToString(c));
        Line("cell_coordinates", B(CellCoordinates));
        Line("cell_coordinates_radius", CellCoordinatesRadius.ToString(c));
        Line("ceiling", B(Ceiling));
        Line("fullbright", B(Fullbright));
        Line("minimap_mobs", B(MinimapMobs));
        Line("player_markers", B(PlayerMarkers));
        Line("names_overlay", B(NamesOverlay));
        Line("spawn_mapping", B(SpawnMapping));
        Line("spawn_abstract", B(SpawnAbstract));
        Line("spawn_amount", SpawnAmount.ToString(c));
        Line("spawn_stick", B(SpawnStick));
        Line("spawn_favourites_only", B(SpawnFavouritesOnly));
        for (int i = 0; i < FavouriteTabs.Length; i++)
            Line("spawn_favourites_" + FavouriteTabs[i], SpawnFavourites[i] ?? "");
        Line("gas_moles", GasMoles.ToString("0.###", c));
        Line("gas_kelvin", GasKelvin.ToString("0.###", c));
        Line("gas_radius", GasRadius.ToString(c));
        Line("audio_volume", AudioVolumePercent.ToString(c));
        Line("audio_mute", B(AudioMute));
        Line("audio_occlusion_factor", AudioOcclusionFactor.ToString("0.###", c));
        Line("audio_lowpass_hz", AudioLowPassHz.ToString("0.#", c));
        Line("audio_muffle_threshold", AudioMuffleThreshold.ToString("0.###", c));
        Line("audio_reverb_wet", AudioReverbWet.ToString("0.###", c));
    }

    /// <summary>The text of the <c>dev.</c> lines (to see whether anything changed).</summary>
    public string ToText()
    {
        var sb = new StringBuilder();
        AppendTo(sb);
        return sb.ToString();
    }

    /// <summary>Sets one setting by its name after <c>dev.</c>; false with the problem when the name or value is wrong.</summary>
    public bool TrySet(string name, string value, out string problem)
    {
        problem = null;
        switch (name)
        {
            case "airflow": return Bool(value, v => Airflow = v, out problem);
            case "airflow_max_streams": return Int(value, 0, 2000, v => AirflowMaxStreams = v, out problem);
            case "airflow_max_per_hole": return Int(value, 1, 1000, v => AirflowMaxPerHole = v, out problem);
            case "airflow_max_per_vent": return Int(value, 1, 500, v => AirflowMaxPerVent = v, out problem);
            case "airflow_spawn_distance": return Int(value, 2, 64, v => AirflowSpawnDistance = v, out problem);
            case "airflow_debug": return Bool(value, v => AirflowDebug = v, out problem);
            case "gas_haze": return Bool(value, v => GasHaze = v, out problem);
            case "gas_overlay": return Bool(value, v => GasOverlay = v, out problem);
            case "gas_overlay_mode":
                if (Array.IndexOf(OverlayModes, value) >= 0)
                {
                    GasOverlayMode = value;
                    return true;
                }
                problem = "expected mix, gas, pressure or temperature";
                return false;
            case "gas_overlay_gas":
                if (value.Length > 0 && value.Length <= 48)
                {
                    GasOverlayGas = value;
                    return true;
                }
                problem = "expected a gas id";
                return false;
            case "gas_overlay_opacity": return Int(value, 10, 100, v => GasOverlayOpacityPercent = v, out problem);
            case "gas_overlay_labels": return Bool(value, v => GasOverlayLabels = v, out problem);
            case "gas_overlay_label_radius": return Int(value, 0, 7, v => GasOverlayLabelRadius = v, out problem);
            case "cell_coordinates": return Bool(value, v => CellCoordinates = v, out problem);
            case "cell_coordinates_radius": return Int(value, 0, 15, v => CellCoordinatesRadius = v, out problem);
            case "ceiling": return Bool(value, v => Ceiling = v, out problem);
            case "fullbright": return Bool(value, v => Fullbright = v, out problem);
            case "minimap_mobs": return Bool(value, v => MinimapMobs = v, out problem);
            case "player_markers": return Bool(value, v => PlayerMarkers = v, out problem);
            case "names_overlay": return Bool(value, v => NamesOverlay = v, out problem);
            case "spawn_mapping": return Bool(value, v => SpawnMapping = v, out problem);
            case "spawn_abstract": return Bool(value, v => SpawnAbstract = v, out problem);
            case "spawn_amount": return Int(value, 1, 100, v => SpawnAmount = v, out problem);
            case "spawn_stick": return Bool(value, v => SpawnStick = v, out problem);
            case "spawn_favourites_only": return Bool(value, v => SpawnFavouritesOnly = v, out problem);
            case "spawn_favourites_objects" or "spawn_favourites_turfs" or "spawn_favourites_mobs":
            {
                // DM paths only: a slash, then letters, digits, _ and /
                List<string> paths = SplitPaths(value).ToList();
                if (paths.Count > 1000 || paths.Any(p => p.Length > 200 || p[0] != '/' || !p.All(ch => char.IsAsciiLetterOrDigit(ch) || ch is '_' or '/')))
                {
                    problem = "expected DM type paths separated by commas";
                    return false;
                }
                SpawnFavourites[Array.IndexOf(FavouriteTabs, name.Substring("spawn_favourites_".Length))] = JoinPaths(paths.Distinct());
                return true;
            }
            case "gas_moles": return Float(value, 0.1f, 10000f, v => GasMoles = v, out problem);
            case "gas_kelvin": return Float(value, 2.7f, 10000f, v => GasKelvin = v, out problem);
            case "gas_radius": return Int(value, 0, 7, v => GasRadius = v, out problem);
            case "audio_volume": return Int(value, 0, 100, v => AudioVolumePercent = v, out problem);
            case "audio_mute": return Bool(value, v => AudioMute = v, out problem);
            case "audio_occlusion_factor": return Float(value, 0f, 1f, v => AudioOcclusionFactor = v, out problem);
            case "audio_lowpass_hz": return Float(value, 100f, 20000f, v => AudioLowPassHz = v, out problem);
            case "audio_muffle_threshold": return Float(value, 0f, 1f, v => AudioMuffleThreshold = v, out problem);
            case "audio_reverb_wet": return Float(value, 0f, 2f, v => AudioReverbWet = v, out problem);
        }
        problem = "unknown dev option";
        return false;
    }

    private static bool Bool(string value, Action<bool> set, out string problem)
    {
        problem = null;
        switch (value)
        {
            case "true" or "on" or "1" or "yes":
                set(true);
                return true;
            case "false" or "off" or "0" or "no":
                set(false);
                return true;
        }
        problem = "expected true or false";
        return false;
    }

    private static bool Int(string value, int min, int max, Action<int> set, out string problem)
    {
        problem = null;
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) && v >= min && v <= max)
        {
            set(v);
            return true;
        }
        problem = $"expected {min} to {max}";
        return false;
    }

    private static bool Float(string value, float min, float max, Action<float> set, out string problem)
    {
        problem = null;
        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) && v >= min && v <= max)
        {
            set(v);
            return true;
        }
        problem = $"expected {min.ToString(CultureInfo.InvariantCulture)} to {max.ToString(CultureInfo.InvariantCulture)}";
        return false;
    }
}
