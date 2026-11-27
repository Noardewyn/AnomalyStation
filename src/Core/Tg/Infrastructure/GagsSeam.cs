using System.IO;
using System.Text.Json;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// DEVIATION(DV686): a GAGS configuration's icon states and colour groups, read from the client's copy of its JSON. tg's SSgreyscale
// reads every configuration's JSON at its init (ReadIconStateConfiguration, ReadMetadata: _greyscale_config.dm:162, :193) and builds the
// icons on the server; the port composes GAGS icons on the client (D104, DV19), so SSgreyscale loads nothing. The colour menu
// (greyscale_modify_menu.dm) needs a configuration's icon states and how many colours it takes: this seam reads them from the
// configuration JSON the icons tool writes for the client (godot/assets/tg/gags/<type>.json, tools/Anomaly.Tools.Icons), which holds
// tg's states and layers as they are. Not tg code: a host seam.

/// <summary>The GAGS configurations' metadata (DV686): the icon states and the expected colours of a configuration type.</summary>
public static class GagsSeam
{
    /// <summary>The folder of the GAGS configuration files, relative to the repository (the client's assets).</summary>
    public const string RelativeFolder = "godot/assets/tg/gags";

    /// <summary>
    /// Sets the folder of <paramref name="world"/>'s GAGS configuration files (a host or a test); without it the folder is looked for from
    /// the working folder and the program's own folder upward, as the server's sound index is.
    /// </summary>
    public static void SetFolder(World world, string folder) => Holder.Of(world).Folder = folder;

    /// <summary>
    /// SSgreyscale's configuration of <paramref name="type"/> (tg's <c>SSgreyscale.configurations["[type]"]</c>): made on first use with
    /// its icon states and expected colours from its JSON, or null when the type is no configuration or its file is missing.
    /// </summary>
    public static datum_greyscale_config Configuration(object type)
    {
        Type config_type = type as Type ?? text2path(Dm.text("[]", type));
        if (config_type == null || !ispath(config_type, typeof(datum_greyscale_config)))
            return null;
        datum_controller_subsystem_processing_greyscale ss = SSgreyscale;
        string key = Dm.text("[]", config_type);
        if (ss?.configurations?[key] is datum_greyscale_config known)
            return known;
        World world = World.CurrentOrNull;
        if (world == null || !TryRead(world, key, out JsonElement states))
            return null;
        var config = Dm.@new(config_type) as datum_greyscale_config;
        config.icon_states = Dm.list();
        var color_groups = new HashSet<int>();
        foreach (JsonProperty state in states.EnumerateObject())
        {
            config.icon_states[state.Name] = Dm.TRUE; // tg keeps the state's layers; the menu reads only the keys
            CollectColorIds(state.Value, color_groups);
        }
        config.expected_colors = color_groups.Count; // ReadMetadata: the distinct numeric colour ids of every layer
        if (ss?.configurations != null)
            ss.configurations[key] = config;
        return config;
    }

    // ReadMetadata's walk over a state's layers (nested layer groups flattened): the numeric colour ids of each layer.
    private static void CollectColorIds(JsonElement layers, HashSet<int> ids)
    {
        if (layers.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in layers.EnumerateArray())
                CollectColorIds(item, ids);
            return;
        }
        if (layers.ValueKind != JsonValueKind.Object || !layers.TryGetProperty("color_ids", out JsonElement color_ids)
            || color_ids.ValueKind != JsonValueKind.Array)
            return;
        foreach (JsonElement id in color_ids.EnumerateArray())
            if (id.ValueKind == JsonValueKind.Number && id.TryGetInt32(out int n))
                ids.Add(n);
    }

    private static bool TryRead(World world, string type_path, out JsonElement states)
    {
        states = default;
        Holder holder = Holder.Of(world);
        if (holder.States.TryGetValue(type_path, out JsonElement cached))
        {
            states = cached;
            return cached.ValueKind == JsonValueKind.Object;
        }
        string folder = holder.Folder ??= FindFolder();
        const string root = "/datum/greyscale_config/";
        string relative = type_path.StartsWith(root, StringComparison.Ordinal) ? type_path.Substring(root.Length) : type_path.TrimStart('/');
        string file = folder == null ? null : Path.Combine(folder, relative + ".json");
        try
        {
            if (file != null && File.Exists(file))
            {
                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(file));
                if (document.RootElement.TryGetProperty("states", out JsonElement found) && found.ValueKind == JsonValueKind.Object)
                    states = found.Clone();
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            states = default;
        }
        holder.States[type_path] = states;
        return states.ValueKind == JsonValueKind.Object;
    }

    private static string FindFolder()
    {
        foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (string dir = Path.GetFullPath(start); dir != null; dir = Path.GetDirectoryName(dir))
            {
                string candidate = Path.Combine(dir, RelativeFolder);
                if (Directory.Exists(candidate))
                    return candidate;
            }
        }
        return null;
    }

    // The per-world folder and the files read (World.GetExtension: no static state).
    private sealed class Holder
    {
        internal string Folder;
        internal readonly Dictionary<string, JsonElement> States = new();

        internal static Holder Of(World world) => world.GetExtension(static _ => new Holder());
    }
}
