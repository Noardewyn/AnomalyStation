using System.Linq;
using System.Reflection;
using Anomaly.Core.DmShim;
using Anomaly.Core.Tg;

namespace Anomaly.Client.Shared.Dev;

// The dev menu's spawn panel (the owner, 2026-10-13: "spawn any object, mob or building, by types, with a dropdown of every
// object, with their icons"). Engine-free (compiled into the client and, linked, into tests/Anomaly.Server.Tests/Client/). The
// list is tg's own spawn panel's (code/modules/admin/spawn_panel/assets.dm, /datum/asset/json/spawnpanel/generate): every
// type of /obj, /turf and /mob with its icon (icon_preview first), icon state, name, description and whether it is a mapping
// helper. Read from the generated classes' constructors (rules 10.2: a constructor assigns plain defaults and never touches a
// world), as the fake feed's ReflectionTypeSource reads them. The categories inside a tab are the dev menu's own.

/// <summary>tg's spawn panel's three lists (constants.tsx listTypes).</summary>
public enum SpawnTab : byte
{
    Objects,
    Turfs,
    Mobs,

    /// <summary>tg's areas: not the spawn panel's (an area is not spawned), the map editor's fourth list (docs/ai/specs/map-editor.md 5.2).</summary>
    Areas,
}

/// <summary>One spawnable type.</summary>
/// <param name="Path">The DM path (<c>/obj/item/wrench</c>).</param>
/// <param name="Name">tg's <c>name</c> (the path's last segment when it has none).</param>
/// <param name="Description">tg's <c>desc</c>, or null.</param>
/// <param name="Icon">The icon resource the panel shows (<c>icon_preview</c>, else the GAGS composition when the type is a greyscale one, else <c>icon</c>), or null.</param>
/// <param name="IconState">The icon state (<c>icon_state_preview</c>, else <c>post_init_icon_state</c> for a GAGS type, else <c>icon_state</c>).</param>
/// <param name="Tab">Objects, turfs or mobs.</param>
/// <param name="Category">The dev menu's category (an index into <see cref="SpawnCatalog.CategoriesOf"/> of the tab).</param>
/// <param name="Mapping">tg's <c>mapping</c> flag: a mapping helper, landmark, spawner or docking port (hidden by default).</param>
/// <param name="Abstract">The type is its own <c>abstract_type</c> (tg: a base class not meant to be made).</param>
public sealed record SpawnEntry(string Path, string Name, string Description, string Icon, string IconState, SpawnTab Tab, int Category, bool Mapping, bool Abstract)
{
    internal string SearchText { get; init; } = string.Empty;

    /// <summary>True for an /obj/item (it can be spawned into a hand).</summary>
    public bool IsItem => Path == "/obj/item" || Path.StartsWith("/obj/item/", StringComparison.Ordinal);
}

/// <summary>A category of a tab: a name and the path prefixes it holds (the most specific category that matches wins).</summary>
public sealed record SpawnCategory(string Name, params string[] Prefixes);

/// <summary>Every spawnable type, its tab and category, and the search.</summary>
public sealed class SpawnCatalog
{
    // tg's mapping_objects typecache (assets.dm:7-17).
    private static readonly string[] MappingRoots =
    {
        "/obj/effect/mapping_helpers", "/obj/effect/landmark", "/obj/effect/spawner", "/obj/effect/mob_spawn", "/obj/effect/holodeck_effect",
        "/obj/docking_port", "/obj/modular_map_connector", "/obj/modular_map_root", "/obj/pathfind_guy",
    };

    private static readonly SpawnCategory[] ObjectCategories =
    {
        new("Clothing", "/obj/item/clothing"),
        new("Weapons", "/obj/item/gun", "/obj/item/melee", "/obj/item/grenade", "/obj/item/ammo_box", "/obj/item/ammo_casing", "/obj/item/knife", "/obj/item/spear", "/obj/item/shield", "/obj/item/fireaxe"),
        new("Tools", "/obj/item/wrench", "/obj/item/screwdriver", "/obj/item/crowbar", "/obj/item/weldingtool", "/obj/item/wirecutters", "/obj/item/multitool",
            "/obj/item/analyzer", "/obj/item/t_scanner", "/obj/item/flashlight", "/obj/item/extinguisher", "/obj/item/storage/toolbox", "/obj/item/construction",
            "/obj/item/pipe_dispenser", "/obj/item/inducer", "/obj/item/lightreplacer", "/obj/item/light/", "/obj/item/rcl", "/obj/item/stack/pipe_cleaner_coil", "/obj/item/geiger_counter", "/obj/item/stack/cable_coil", "/obj/item/storage/belt/utility", "/obj/item/holosign_creator"),
        new("Medical", "/obj/item/stack/medical", "/obj/item/healthanalyzer", "/obj/item/reagent_containers/hypospray", "/obj/item/reagent_containers/syringe",
            "/obj/item/reagent_containers/pill", "/obj/item/scalpel", "/obj/item/hemostat", "/obj/item/retractor", "/obj/item/cautery", "/obj/item/surgicaldrill",
            "/obj/item/circular_saw", "/obj/item/bonesetter", "/obj/item/blood_filter", "/obj/item/surgical_drapes", "/obj/item/organ", "/obj/item/bodypart",
            "/obj/item/storage/medkit", "/obj/item/defibrillator", "/obj/item/stack/sticky_tape/surgical"),
        new("Food and drink", "/obj/item/food", "/obj/item/reagent_containers/cup/glass", "/obj/item/reagent_containers/condiment", "/obj/item/reagent_containers/cup/soda_cans", "/obj/item/reagent_containers/cup/bowl"),
        new("Chemistry", "/obj/item/reagent_containers", "/obj/machinery/chem_dispenser", "/obj/machinery/chem_master", "/obj/machinery/chem_heater"),
        new("Materials", "/obj/item/stack"),
        new("Storage", "/obj/item/storage"),
        new("Atmospherics", "/obj/item/tank", "/obj/machinery/portable_atmospherics", "/obj/machinery/atmospherics", "/obj/machinery/airalarm", "/obj/item/pipe", "/obj/item/pipe_meter"),
        new("Electronics", "/obj/item/card", "/obj/item/circuitboard", "/obj/item/stock_parts", "/obj/item/electronics", "/obj/item/assembly", "/obj/item/radio",
            "/obj/item/modular_computer", "/obj/item/computer_disk", "/obj/item/encryptionkey", "/obj/item/stack/circuit_stack"),
        new("Other items", "/obj/item"),
        new("Doors and windows", "/obj/machinery/door", "/obj/structure/window", "/obj/structure/grille", "/obj/machinery/door_buttons", "/obj/structure/windoor_assembly", "/obj/structure/door_assembly"),
        new("Lights and power", "/obj/machinery/light", "/obj/machinery/power", "/obj/structure/cable", "/obj/machinery/light_switch"),
        new("Machinery", "/obj/machinery"),
        new("Furniture", "/obj/structure/table", "/obj/structure/chair", "/obj/structure/bed", "/obj/structure/rack", "/obj/structure/closet", "/obj/structure/dresser",
            "/obj/structure/sink", "/obj/structure/toilet", "/obj/structure/shower", "/obj/structure/mirror", "/obj/structure/displaycase", "/obj/structure/filingcabinet"),
        new("Structures", "/obj/structure"),
        new("Effects", "/obj/effect"),
        new("Other objects", "/obj"),
    };

    private static readonly SpawnCategory[] TurfCategories =
    {
        new("Floors", "/turf/open/floor"),
        new("Other open turfs", "/turf/open"),
        new("Walls", "/turf/closed/wall"),
        new("Other closed turfs", "/turf/closed"),
        new("Full-tile windows", "/obj/structure/window"),
        new("Other turfs", "/turf"),
    };

    private static readonly SpawnCategory[] MobCategories =
    {
        new("Humans", "/mob/living/carbon/human"),
        new("Other carbons", "/mob/living/carbon"),
        new("Animals", "/mob/living/basic", "/mob/living/simple_animal"),
        new("Silicons", "/mob/living/silicon"),
        new("Other mobs", "/mob"),
    };

    private static readonly SpawnCategory[] AreaCategories =
    {
        new("Station", "/area/station"),
        new("Shuttles", "/area/shuttle"),
        new("Space and mining", "/area/space", "/area/mine", "/area/lavaland", "/area/icemoon"),
        new("Ruins and away", "/area/ruin", "/area/awaymission"),
        new("Other areas", "/area"),
    };

    private readonly List<SpawnEntry> _entries;

    private readonly Dictionary<string, List<SpawnEntry>> _byPath = new(StringComparer.Ordinal); // a path's entries in the list's order (Find)

    private SpawnCatalog(List<SpawnEntry> entries)
    {
        _entries = entries;
        foreach (SpawnEntry e in entries)
        {
            if (!_byPath.TryGetValue(e.Path, out List<SpawnEntry> same))
                _byPath[e.Path] = same = new List<SpawnEntry>(1);
            same.Add(e);
        }
    }

    /// <summary>Every type, by tab, then by path.</summary>
    public IReadOnlyList<SpawnEntry> Entries => _entries;

    /// <summary>The categories of a tab.</summary>
    public static IReadOnlyList<SpawnCategory> CategoriesOf(SpawnTab tab) => tab switch
    {
        SpawnTab.Turfs => TurfCategories,
        SpawnTab.Mobs => MobCategories,
        SpawnTab.Areas => AreaCategories,
        _ => ObjectCategories,
    };

    /// <summary>The tab of a path (null for one outside /obj, /turf, /mob and /area).</summary>
    public static SpawnTab? TabOf(string path)
    {
        if (Under(path, "/obj")) return SpawnTab.Objects;
        if (Under(path, "/turf")) return SpawnTab.Turfs;
        if (Under(path, "/mob")) return SpawnTab.Mobs;
        if (Under(path, "/area")) return SpawnTab.Areas;
        return null;
    }

    /// <summary>The category of a path in its tab: the category with the longest prefix that holds it.</summary>
    public static int CategoryOf(SpawnTab tab, string path)
    {
        IReadOnlyList<SpawnCategory> categories = CategoriesOf(tab);
        int best = categories.Count - 1, bestLength = -1;
        for (int i = 0; i < categories.Count; i++)
        {
            foreach (string prefix in categories[i].Prefixes)
            {
                if (prefix.Length > bestLength && Under(path, prefix))
                {
                    best = i;
                    bestLength = prefix.Length;
                }
            }
        }
        return best;
    }

    /// <summary>tg's mapping flag (assets.dm's <c>mapping_objects</c>).</summary>
    public static bool IsMapping(string path)
    {
        foreach (string root in MappingRoots)
        {
            if (Under(path, root)) return true;
        }
        return false;
    }

    /// <summary>A full-tile window type (tg's <c>fulltile</c> windows), listed with the turfs as well.</summary>
    public static bool IsBlockWindow(string path) => Under(path, "/obj/structure/window") && path.Contains("/fulltile", StringComparison.Ordinal);

    private static bool Under(string path, string root) => path == root || (path.StartsWith(root, StringComparison.Ordinal) && path.Length > root.Length && path[root.Length] == '/');

    /// <summary>
    /// Reads every [DmPath] class of /obj, /turf and /mob in the translated tg code (a few seconds for tg's types: build it off
    /// the main thread). A class whose constructor throws is left out (it needs a world, so it is no plain-defaults class).
    /// </summary>
    public static SpawnCatalog Build() => Build(areas: false);

    /// <summary>The same; with <paramref name="areas"/>, tg's areas too (the map editor's palette).</summary>
    public static SpawnCatalog Build(bool areas) => Build(new[] { typeof(DmPathAttribute).Assembly, typeof(TgWorld).Assembly }, areas);

    /// <summary>The catalog of the classes of <paramref name="assemblies"/>.</summary>
    public static SpawnCatalog Build(IEnumerable<Assembly> assemblies, bool areas = false)
    {
        var entries = new List<SpawnEntry>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (Assembly assembly in assemblies.Distinct())
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                types = e.Types.Where(t => t != null).ToArray();
            }
            foreach (Type type in types)
            {
                if (type.IsAbstract || type.ContainsGenericParameters) continue;
                string path = type.GetCustomAttribute<DmPathAttribute>(false)?.Path;
                if (path == null || TabOf(path) is not SpawnTab tab || (tab == SpawnTab.Areas && !areas) || !seen.Add(path)) continue;
                SpawnEntry entry = Read(type, path, tab);
                if (entry != null) entries.Add(entry);
            }
        }
        // a full-tile window is a block like a wall here (D152): the turf tab lists it too (the owner, 2026-11-10)
        foreach (SpawnEntry e in entries.ToList())
        {
            if (IsBlockWindow(e.Path))
                entries.Add(e with { Tab = SpawnTab.Turfs, Category = CategoryOf(SpawnTab.Turfs, e.Path) });
        }
        entries.Sort(static (a, b) => a.Tab != b.Tab ? a.Tab.CompareTo(b.Tab) : string.CompareOrdinal(a.Path, b.Path));
        return new SpawnCatalog(entries);
    }

    /// <summary>A catalog of given entries (the tests).</summary>
    public static SpawnCatalog Of(IEnumerable<SpawnEntry> entries)
        => new(entries.Select(e => e with { SearchText = SearchTextOf(e.Name, e.Path) }).ToList());

    private static SpawnEntry Read(Type type, string path, SpawnTab tab)
    {
        object instance;
        try
        {
            instance = Activator.CreateInstance(type);
        }
        catch (Exception)
        {
            return null;
        }
        if (instance == null) return null;
        string name = Member(instance, "name") as string;
        // (tg's \proper and \improper are marks inside the text, an area's name has one: not letters to show)
        if (name != null && name.Any(IsTextMark)) name = new string(name.Where(c => !IsTextMark(c)).ToArray());
        if (string.IsNullOrEmpty(name)) name = path.Substring(path.LastIndexOf('/') + 1);
        string icon = Text(Member(instance, "icon_preview")) ?? Text(Member(instance, "icon"));
        string state = Member(instance, "icon_state_preview") as string ?? Member(instance, "icon_state") as string;
        // A GAGS type: its icon is the template; the panel shows the generated icon, as the game will after init
        // (atoms_initializing_EXPENSIVE.dm:128-129 update_greyscale; the client's IconLibrary composes "gags:" resources).
        if (Member(instance, "icon_preview") == null && Member(instance, "greyscale_config") is Type config
            && Member(instance, "greyscale_colors") is string { Length: > 0 } colors
            && config.GetCustomAttribute<DmPathAttribute>(false)?.Path is string configPath)
        {
            icon = "gags:" + configPath + "|" + colors;
            if (Member(instance, "post_init_icon_state") is string { Length: > 0 } postInit)
                state = postInit;
        }
        bool isAbstract = Member(instance, "abstract_type") is Type abstractType && abstractType == type;
        return new SpawnEntry(path, name, Member(instance, "desc") as string, icon, state ?? string.Empty, tab, CategoryOf(tab, path), IsMapping(path), isAbstract)
        {
            SearchText = SearchTextOf(name, path),
        };
    }

    // The shim's marks of tg's \proper and \improper (DmTextRender: two private-use characters).
    private static bool IsTextMark(char c) => c is '\uE000' or '\uE001';

    private static string SearchTextOf(string name, string path) => (name + " " + path).ToLowerInvariant();

    private static string Text(object resource)
    {
        if (resource == null) return null;
        string text = resource.ToString(); // a DmResource renders as its path (rules 3.7)
        return string.IsNullOrEmpty(text) ? null : text;
    }

    private static object Member(object instance, string name)
    {
        Type type = instance.GetType();
        PropertyInfo property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
        if (property != null && property.CanRead && property.GetIndexParameters().Length == 0)
            return property.GetValue(instance);
        return type.GetField(name, BindingFlags.Public | BindingFlags.Instance)?.GetValue(instance);
    }

    /// <summary>
    /// The entries of <paramref name="tab"/> (null: every tab) in <paramref name="category"/> (-1: all) whose name or path
    /// holds every word of <paramref name="query"/> (case-insensitive), without the mapping helpers and the abstract types
    /// unless asked for, into <paramref name="into"/> (cleared first); <paramref name="only"/>: just those paths (the favourites, which show whatever they are).
    /// </summary>
    public void Filter(SpawnTab? tab, int category, string query, bool mapping, bool abstracts, List<SpawnEntry> into, IReadOnlySet<string> only = null)
    {
        into.Clear();
        string[] words = (query ?? string.Empty).ToLowerInvariant().Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
        foreach (SpawnEntry e in _entries)
        {
            if (tab.HasValue && e.Tab != tab.Value) continue;
            if (category >= 0 && e.Category != category) continue;
            if (only != null && !only.Contains(e.Path)) continue;
            if (e.Mapping && !mapping && only == null) continue;
            if (e.Abstract && !abstracts && only == null) continue;
            bool all = true;
            foreach (string word in words)
            {
                if (!e.SearchText.Contains(word, StringComparison.Ordinal))
                {
                    all = false;
                    break;
                }
            }
            if (all) into.Add(e);
        }
    }

    /// <summary>The entry of a path, the one in <paramref name="tab"/> when it is in two (a full-tile window), or null.</summary>
    public SpawnEntry Find(string path, SpawnTab? tab = null)
    {
        // (by the path's own list: the map editor asks for the name of every member of a map)
        if (path == null || !_byPath.TryGetValue(path, out List<SpawnEntry> same))
            return null;
        foreach (SpawnEntry e in same)
        {
            if (tab == null || e.Tab == tab) return e;
        }
        return same[0];
    }
}
