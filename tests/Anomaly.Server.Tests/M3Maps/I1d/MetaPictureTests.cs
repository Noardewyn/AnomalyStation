using System.IO;
using System.Text.Json;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Server.Replication;
using Anomaly.Server.Tests.Integration;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;

namespace Anomaly.Server.Tests.M3Maps.I1d;

/// <summary>
/// M3 I1d (pictures the server does not send): every entity the real server's Meta replicates, checked against the icons the
/// client has (tools/Anomaly.Tools.Icons' output in <c>godot/assets/tg</c>): an entity shows nothing (no icon, no overlays), names
/// an icon file the client lacks, or a state that file lacks (with BYOND's fallback to the default state), and the random
/// spawners still standing with their map-preview icons (tg deletes every spawner at init). The counts per type are printed.
/// Skipped without the converted Meta; nothing is checked without the converted icons.
/// </summary>
public class MetaPictureTests
{
    private readonly ITestOutputHelper _output;

    public MetaPictureTests(ITestOutputHelper output) => _output = output;

    internal static string IconRoot()
    {
        for (string dir = AppContext.BaseDirectory; dir != null; dir = Path.GetDirectoryName(dir))
        {
            string candidate = Path.Combine(dir, "godot", "assets", "tg");
            if (Directory.Exists(Path.Combine(candidate, "icons"))) return candidate;
        }
        return null;
    }

    private sealed class Icons
    {
        private readonly string _root;
        private readonly Dictionary<string, HashSet<string>> _states = new(StringComparer.Ordinal);

        public Icons(string root) => _root = root;

        // The states of an icon file, or null when the client has no such file ('icons/x.dmi' -> icons/x.json; a GAGS icon
        // "gags:/datum/greyscale_config/a/b|<colours>" -> gags/a/b.json, the states of its config).
        public HashSet<string> StatesOf(string path)
        {
            if (_states.TryGetValue(path, out HashSet<string> states)) return states;
            bool gags = path.StartsWith("gags:", StringComparison.Ordinal);
            string json = gags
                ? Path.Combine(_root, "gags", path.Substring(5).Split('|')[0].Replace("/datum/greyscale_config/", "", StringComparison.Ordinal) + ".json")
                : Path.Combine(_root, Path.ChangeExtension(path, ".json"));
            if (File.Exists(json))
            {
                states = new HashSet<string>(StringComparer.Ordinal);
                using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(json));
                JsonElement list = doc.RootElement.GetProperty("states");
                if (gags)
                    foreach (JsonProperty s in list.EnumerateObject()) states.Add(s.Name);
                else
                    foreach (JsonElement s in list.EnumerateArray()) states.Add(s.GetProperty("name").GetString() ?? "");
            }
            _states[path] = states;
            return states;
        }
    }

    private static string IconPath(object icon) => icon switch
    {
        DmResource r => r.Path,
        string s when s.Length > 0 => s,
        _ => null,
    };

    // What is wrong with an appearance's own picture (null when the client can draw it).
    private static string Problem(Icons icons, object icon, string state, int overlays)
    {
        string path = IconPath(icon);
        if (path == null) return overlays > 0 ? null : "nothing (no icon, no overlays)";
        HashSet<string> states = icons.StatesOf(path);
        if (states == null) return "no icon file " + (path.Contains('|') ? path.Split('|')[0] : path);
        // BYOND shows the default ("") state for a state the icon lacks (the client's IconStates.TryGetShown).
        if (!states.Contains(state ?? "") && !states.Contains("")) return overlays > 0 ? null : $"no state '{state}' (nor a default one) in {(path.Contains('|') ? path.Split('|')[0] : path)}";
        return null;
    }

    private static readonly string[] WaitForBasicMobs =
    {
        // (gaps G3: the deluxe garbage, grime and moisture spawners and the maintenance MOD module spawner are live; their inert
        // mobs and modules are props that can be deleted)
        // M4a (the lead, after P3's live containers shifted the boot draws): an inert type that the maintenance loot can
        // roll. tg's /obj/item/paper/construction keeps the spawner icon until its Initialize (paper.dm:916), which waits
        // for /obj/item/paper (paperwork, M9).
        "/obj/item/paper/construction ",
    };

    // tg's own gap: /obj/effect/spawner/random/trash/crushed_can picks "space_up", "lemon_lime" and "space beer" (trash.dm:391-392),
    // states that tg's icons/obj/service/janitor.dmi does not have, so BYOND draws nothing for those cans either (which can a spawner
    // picks depends on the RNG stream). M5 slice 2: a cut cable piece (/obj/item/stack/cable_coil/cut, base_icon_state "coil2",
    // cable.dm:735) gets "coil21" or "coil22" from the coil's update_icon_state (cable.dm:552), states stack_objects.dmi lacks.
    // The M7 supermatter slice: the crystal's countdown (countdown.dm:1) has no icon state; it shows only its maptext, to ghosts.
    // M8x: a cryo cell's gas connector (machine_connector.dm:13) is a bare unary component on the cell's tile with no icon state
    // (unary_devices.dm:1), which unary_devices.dmi lacks: in BYOND it draws nothing under the cell.
    private static readonly string[] TgMissingStates =
    {
        "/obj/item/trash/can: no state 'lemon_lime'", "/obj/item/trash/can: no state 'space_up'", "/obj/item/trash/can: no state 'space beer'",
        "/obj/item/stack/cable_coil/cut: no state 'coil21'", "/obj/item/stack/cable_coil/cut: no state 'coil22'",
        "/obj/effect/countdown/supermatter: no state ''",
        "/obj/machinery/atmospherics/components/unary: no state ''",
    };

    // The port's own gaps of inert types (D112), each with its milestone: a falsewall is inert (construction and the hidden
    // passages, M5), so its update_icon_state (false_walls.dm:79), which swaps in the fake wall's icon, does not run, and the
    // engine's bitmask smoothing gives it "wall-<junction>" in false_walls.dmi. Meta's abandoned airlock helpers turn an
    // airlock into one by a weighted pick (mapping_helpers.dm:262, 1 in 13), so how many there are depends on the RNG stream.
    private static readonly string[] InertGaps =
    {
        "/obj/structure/falsewall: no state 'wall-",
    };

    [MetaServerFact]
    public void Every_replicated_entity_on_Meta_has_a_picture_the_client_can_draw()
    {
        string root = IconRoot();
        if (root == null)
        {
            _output.WriteLine("no converted icons (tools/convert-tg.sh or tools/Anomaly.Tools.Icons): nothing checked");
            return;
        }
        var icons = new Icons(root);
        using var world = new RealHeadlessWorld(new LoopbackOptions { Seed = 5, TimeoutSteps = 400 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        world.Run(2);
        var byType = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var where = new Dictionary<string, string>(StringComparer.Ordinal);
        int entities = 0;
        var preview = new SortedDictionary<string, int>(StringComparer.Ordinal);
        using (world.World.Enter())
        {
            long cursor = 0;
            while (world.World.Objects.TryGetNextLive(cursor, out long id, out object obj))
            {
                cursor = id;
                if (!ReplicatedFields.IsEntity(obj, out atom_movable m, out turf on)) continue;
                entities++;
                string path = IconPath(m.icon);
                if (path != null && (path.StartsWith("icons/map_icons/", StringComparison.Ordinal) || path.Contains("spawner", StringComparison.Ordinal)))
                {
                    string g = "map-preview icon: " + DmTypes.PathOf(m.GetType()) + " " + path + " '" + m.icon_state + "'";
                    preview[g] = preview.GetValueOrDefault(g) + 1;
                    where.TryAdd(g, $"({on.x},{on.y},{on.z})");
                }
                string problem = Problem(icons, m.icon, m.icon_state, m.overlays?.Count ?? 0);
                if (problem == null) continue;
                string key = DmTypes.PathOf(m.GetType()) + ": " + problem;
                byType[key] = byType.GetValueOrDefault(key) + 1;
                where.TryAdd(key, $"({on.x},{on.y},{on.z})");
            }
        }
        int missing = byType.Where(p => !TgMissingStates.Concat(InertGaps).Any(k => p.Key.StartsWith(k, StringComparison.Ordinal))).Sum(p => p.Value);
        _output.WriteLine($"{entities} replicated entities, {missing} without a picture the client can draw, {byType.Count} kinds");
        foreach (var (key, count) in byType.OrderByDescending(p => p.Value).ThenBy(p => p.Key, StringComparer.Ordinal))
            _output.WriteLine($"{count,5}  {key}  first at {where[key]}");
        int previews = 0;
        foreach (var (key, count) in preview.OrderByDescending(p => p.Value))
        {
            if (!WaitForBasicMobs.Any(k => key.Contains(k, StringComparison.Ordinal)))
                previews += count;
            _output.WriteLine($"{count,5}  {key}  first at {where[key]}");
        }
        // M3 I1d: the cameras (their generated var block), the canisters (BYOND's default state of their GAGS icon), and the
        // random spawners that stayed inert with their map-preview icons (the owner's rainbow box with a question mark near
        // (67,119): icons/effects/random_spawners.dmi, 130 spawners of 15 types) all have their pictures or are gone.
        Assert.Equal(0, previews);
        Assert.Equal(0, missing);
    }
}
