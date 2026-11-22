using System.IO;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M3Doors.T1;

// M3 T1's helpers: independent tests of P3 (machinery, doors, access, lights) written from tg's DM, not from the port's
// code. The booted toy room (the real Master: SStimer, SSmachines, SSatoms run) is the world; one tick is half a decisecond.

/// <summary>A key card stand-in for tests: an item whose GetAccess lists the given accesses (ID cards are M4a, D130).</summary>
[DmPath("/obj/item/t1_keycard")]
public class obj_item_t1_keycard : obj_item
{
    public DmList t1_access;

    public override object GetAccess() => t1_access ?? Dm.list();
}

internal readonly record struct DoorSnap(int Tick, bool Dense, bool Opaque, bool Operating, string Icon, string State, float Layer);

internal static class T1
{
    /// <summary>20 ticks per second: a decisecond is two ticks (world.fps 20, tick_lag 0.5).</summary>
    public const int TicksPerDs = 2;

    /// <summary>Ticks that <c>sleep(ds)</c> takes: BYOND rounds a delay down to whole ticks, one tick at least
    /// (waiting-model.md section 8; tg's config/config.txt:470).</summary>
    public static int Sleep(double ds) => Math.Max(1, (int)Math.Floor(ds * TicksPerDs * (1 + 1e-6)));

    public static TgToyRoom Room(Action<World> mapExtras = null, ulong seed = 41, bool game = true, int width = TgToyRoom.Size, int height = TgToyRoom.Size)
        => TgToyRoom.Boot(width: width, height: height, seed: seed, game: game, mapExtras: mapExtras);

    public static TObj At<TObj>(TgToyRoom room, int x, int y) where TObj : atom => room.In(() => Dm.@new<TObj>(locate(x, y, 1)));

    public static mob_living_carbon_human Human(TgToyRoom room, int x, int y)
        => room.In(() => Dm.@new<mob_living_carbon_human_consistent>(locate(x, y, 1)));

    /// <summary>tg's <c>subject.Bump(door)</c> of the unit tests: the mover's Bump asks the bumped atom's Bumped.</summary>
    public static void Bump(TgToyRoom room, atom_movable who, atom what) => room.In(() => what.Bumped(who));

    // M4a P5: an ID card with the accesses in the mob's hands (M3's dev access list, DV23, is retired).
    public static void Access(TgToyRoom room, mob_living who, params string[] accesses)
        => room.In(() => Anomaly.Core.Tests.Tg.M4aAccess.P5.IdCards.Hold(who, accesses));

    public static void AllAccess(TgToyRoom room, mob_living who) => room.In(() => Anomaly.Core.Tests.Tg.M4aAccess.P5.IdCards.HoldAll(who));

    /// <summary>Fails with the runtimes' text (first lines) when the world reported any.</summary>
    public static void NoRuntimes(TgToyRoom room)
    {
        var text = room.RuntimeText.Select(t => string.Join("\n", t.Split('\n').Take(6))).ToList();
        Assert.True(text.Count == 0, "runtimes:\n" + string.Join("\n---\n", text));
    }

    public static DmList L(params object[] items) => Dm.list(items);

    public static bool Dense(obj_machinery_door d) => Dm.truthy(d.density);

    public static DoorSnap Snap(obj_machinery_door d, int tick) => new(
        tick, Dm.truthy(d.density), Dm.truthy(d.opacity), Dm.truthy(d.operating), d.icon_state as string,
        (d as obj_machinery_door_airlock)?.airlock_state as string, Dm.num(d.layer));

    /// <summary>The door's state after each of <paramref name="ticks"/> ticks, tick 0 being right now.</summary>
    public static List<DoorSnap> Record(TgToyRoom room, obj_machinery_door d, int ticks)
    {
        var list = new List<DoorSnap>(ticks + 1);
        list.Add(room.In(() => Snap(d, 0)));
        for (int i = 1; i <= ticks; i++)
        {
            room.Step(1);
            int t = i;
            list.Add(room.In(() => Snap(d, t)));
        }
        return list;
    }

    /// <summary>The first tick at which <paramref name="pred"/> holds from then on (the value flips there), or -1.</summary>
    public static int FirstTick(List<DoorSnap> snaps, Func<DoorSnap, bool> pred, int from = 0)
    {
        for (int i = from; i < snaps.Count; i++)
            if (pred(snaps[i])) return snaps[i].Tick;
        return -1;
    }
}

/// <summary>Reads tg's own source files (the clone the converter tests use); tests that need it use <c>[TgFact]</c>.</summary>
internal static class TgText
{
    public static string[] Lines(string relative)
        => File.ReadAllLines(Path.Combine(Anomaly.Core.Tests.Tools.TgClone.Root, relative.Replace('/', Path.DirectorySeparatorChar)));

    /// <summary>tg's <c>#define ACCESS_NAME "value"</c> lines of <c>__DEFINES/access.dm</c>, in file order.</summary>
    public static List<(string Name, string Value)> AccessDefines()
    {
        var list = new List<(string, string)>();
        foreach (string line in Lines("code/__DEFINES/access.dm"))
        {
            var m = System.Text.RegularExpressions.Regex.Match(line, "^#define (ACCESS_(?!FLAG_)[A-Z0-9_]+) \"([^\"]*)\"\\s*$");
            if (m.Success) list.Add((m.Groups[1].Value, m.Groups[2].Value));
        }
        return list;
    }

    /// <summary>The ACCESS_ names inside the list macros of <c>__DEFINES/access.dm</c> that make tg's "every access" region
    /// (REGION_ACCESS_ALL_GLOBAL, :429: the common, command, private command and captain lists, centcom, syndicate, away, cult).</summary>
    public static List<string> AllGlobalAccessNames()
    {
        var wanted = new HashSet<string> { "COMMON_ACCESS", "COMMAND_ACCESS", "PRIVATE_COMMAND_ACCESS", "CAPTAIN_ACCESS", "CENTCOM_ACCESS", "SYNDICATE_ACCESS", "AWAY_ACCESS", "CULT_ACCESS" };
        var names = new List<string>();
        bool inside = false;
        foreach (string line in Lines("code/__DEFINES/access.dm"))
        {
            var start = System.Text.RegularExpressions.Regex.Match(line, "^#define ([A-Z_]+_ACCESS) list\\(");
            if (start.Success) inside = wanted.Contains(start.Groups[1].Value);
            else if (inside && line.StartsWith(")")) inside = false;
            if (inside)
                foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(line, "ACCESS_[A-Z0-9_]+"))
                    if (!m.Value.EndsWith("_ACCESS")) names.Add(m.Value);
        }
        return names;
    }
}

internal static class TgAccessHelpers
{
    /// <summary>tg's access helpers (<c>modules/mapping/access_helpers.dm</c>): the path under
    /// <c>/obj/effect/mapping_helpers/airlock/access/</c>, any or all, and the ACCESS_ names its get_access adds, in order.</summary>
    public static List<(string Path, bool Any, List<string> Names)> Parse()
    {
        var result = new List<(string, bool, List<string>)>();
        string[] lines = TgText.Lines("code/modules/mapping/access_helpers.dm");
        for (int i = 0; i < lines.Length; i++)
        {
            var m = System.Text.RegularExpressions.Regex.Match(lines[i], "^/obj/effect/mapping_helpers/airlock/access/(any|all)/(.+)/get_access\\(\\)$");
            if (!m.Success) continue;
            var names = new List<string>();
            for (int j = i + 1; j < lines.Length && lines[j].StartsWith("\t"); j++)
                if (lines[j].Contains("access_list +="))
                    foreach (System.Text.RegularExpressions.Match n in System.Text.RegularExpressions.Regex.Matches(lines[j], "ACCESS_[A-Z0-9_]+"))
                        names.Add(n.Value);
            result.Add((m.Groups[2].Value, m.Groups[1].Value == "any", names));
        }
        // `var/list/access_list = ..()` runs the parent's get_access first: a helper whose path extends another helper's path
        // starts from that helper's list (tg's /any/engineering/maintenance/departmental extends /maintenance)
        var byPath = result.ToDictionary(r => (r.Item2, r.Item1), r => r.Item3);
        var full = new List<(string, bool, List<string>)>();
        foreach (var (path, any, names) in result)
        {
            var chain = new List<string>(names);
            string parent = path;
            while (parent.Contains('/'))
            {
                parent = parent.Substring(0, parent.LastIndexOf('/'));
                if (byPath.TryGetValue((any, parent), out var inherited))
                {
                    chain.InsertRange(0, FullList(byPath, any, parent));
                    break;
                }
            }
            full.Add((path, any, chain));
        }
        return full;
    }

    private static List<string> FullList(Dictionary<(bool, string), List<string>> byPath, bool any, string path)
    {
        var chain = new List<string>(byPath[(any, path)]);
        string parent = path;
        while (parent.Contains('/'))
        {
            parent = parent.Substring(0, parent.LastIndexOf('/'));
            if (byPath.ContainsKey((any, parent)))
            {
                chain.InsertRange(0, FullList(byPath, any, parent));
                break;
            }
        }
        return chain;
    }

    public static Type TypeOf(string dmPath)
        => typeof(obj_machinery_door_airlock).Assembly.GetType("Anomaly.Core.Tg." + dmPath.Trim('/').Replace('/', '_'));
}

/// <summary>A tiny reader of tg's DM for the light fixture types: the var block of <c>/obj/machinery/light</c> and the
/// per-type overrides of <c>light_mapping_helpers.dm</c> and the floor lights of <c>light.dm</c>, with the defines they name.</summary>
internal static class TgLightData
{
    private static Dictionary<string, string> _defines;

    private static Dictionary<string, string> Defines()
    {
        if (_defines != null) return _defines;
        var d = new Dictionary<string, string>();
        string dir = Path.Combine(Anomaly.Core.Tests.Tools.TgClone.Root, "code", "__DEFINES");
        foreach (string file in Directory.GetFiles(dir, "*.dm"))
            foreach (string line in File.ReadAllLines(file))
            {
                var m = System.Text.RegularExpressions.Regex.Match(line, "^#define ([A-Z][A-Z0-9_]+) (.+?)\\s*(//.*)?$");
                if (m.Success && !d.ContainsKey(m.Groups[1].Value)) d[m.Groups[1].Value] = m.Groups[2].Value.Trim();
            }
        return _defines = d;
    }

    /// <summary>Resolves a DM literal: number, text, TRUE/FALSE, a define (recursively) or a typepath (returned as the string "/path").</summary>
    public static object Resolve(string raw)
    {
        raw = raw.Trim();
        if (raw == "TRUE") return 1d;
        if (raw == "FALSE") return 0d;
        if (raw.StartsWith("\"")) return raw.Trim('"');
        if (raw.StartsWith("/")) return raw;
        if (double.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double n)) return n;
        if (Defines().TryGetValue(raw, out string next)) return Resolve(next);
        throw new InvalidOperationException("cannot resolve " + raw);
    }

    /// <summary>The var assignments per type path (relative to /obj/machinery/light, "" the base), from tg's files.</summary>
    public static Dictionary<string, Dictionary<string, string>> Parse()
    {
        var types = new Dictionary<string, Dictionary<string, string>>();
        void Scan(string[] lines, int from, int to)
        {
            Dictionary<string, string> current = null;
            for (int i = from; i < Math.Min(to, lines.Length); i++)
            {
                string line = lines[i];
                var head = System.Text.RegularExpressions.Regex.Match(line, "^/obj/machinery/light(/[a-z_/]*)?$");
                if (head.Success)
                {
                    string rel = head.Groups[1].Success ? head.Groups[1].Value : "";
                    if (!types.TryGetValue(rel, out current)) types[rel] = current = new();
                    continue;
                }
                if (line.Length > 0 && !line.StartsWith("\t") && !line.StartsWith("//")) current = null;
                if (current == null) continue;
                var v = System.Text.RegularExpressions.Regex.Match(line, "^\t(?:var/(?:[a-z_/]+ )?)?([a-z_]+) = (.+?)\\s*(//.*)?$");
                if (v.Success && !line.TrimStart().StartsWith("///")) current[v.Groups[1].Value] = v.Groups[2].Value.Trim();
            }
        }
        string[] light = TgText.Lines("code/modules/power/lighting/light.dm");
        int floorStart = Array.FindIndex(light, l => l == "/obj/machinery/light/floor");
        Scan(light, 0, floorStart < 0 ? 140 : floorStart + 60);
        Scan(TgText.Lines("code/modules/power/lighting/light_mapping_helpers.dm"), 0, 200);
        return types;
    }

    /// <summary>The value of <paramref name="var"/> for the type: its own override, else the nearest ancestor's, else the base's.</summary>
    public static string Lookup(Dictionary<string, Dictionary<string, string>> types, string rel, string var)
    {
        for (string p = rel; ; p = p.Substring(0, p.LastIndexOf('/')))
        {
            if (types.TryGetValue(p, out var vars) && vars.TryGetValue(var, out string raw)) return raw;
            if (p == "") return null;
        }
    }
}

/// <summary>An independent reader of tg's own TGM file (the dictionary and the column blocks), for the Meta checks: what the
/// map says, to be compared with what the port's loader and the init made of it. Only paths and raw var text are kept.</summary>
internal sealed class TgDmm
{
    public sealed record Entry(string Path, string Vars);

    public Dictionary<(int X, int Y), List<Entry>> Cells { get; } = new();

    public static TgDmm Read(string file)
    {
        var dmm = new TgDmm();
        var dict = new Dictionary<string, List<Entry>>();
        string[] lines = File.ReadAllLines(file);
        int i = 0;
        for (; i < lines.Length; i++)
        {
            string line = lines[i];
            if (line.StartsWith("(")) break;
            if (!line.StartsWith("\"") || !line.EndsWith("= (")) continue;
            string key = line.Substring(1, line.IndexOf('"', 1) - 1);
            var entries = new List<Entry>();
            for (i++; i < lines.Length; i++)
            {
                string l = lines[i];
                if (!l.StartsWith("/")) continue;
                int cut = l.IndexOfAny(new[] { '{', ',', ')' });
                string path = cut < 0 ? l : l.Substring(0, cut);
                var vars = new System.Text.StringBuilder();
                if (l.Contains('{') && !l.Contains('}'))
                    for (i++; i < lines.Length; i++)
                    {
                        vars.AppendLine(lines[i]);
                        if (lines[i].TrimStart().StartsWith("}")) break;
                    }
                else if (l.Contains('{'))
                    vars.Append(l.Substring(l.IndexOf('{')));
                entries.Add(new Entry(path, vars.ToString()));
                if (l.EndsWith(")") ) break;
            }
            dict[key] = entries;
        }
        for (; i < lines.Length; i++)
        {
            var m = System.Text.RegularExpressions.Regex.Match(lines[i], "^\\((\\d+),1,1\\) = \\{\"$");
            if (!m.Success) continue;
            int x = int.Parse(m.Groups[1].Value);
            var keys = new List<string>();
            for (i++; i < lines.Length && lines[i] != "\"}"; i++) keys.Add(lines[i].Trim());
            for (int row = 0; row < keys.Count; row++)
                dmm.Cells[(x, keys.Count - row)] = dict[keys[row]];
        }
        return dmm;
    }
}
