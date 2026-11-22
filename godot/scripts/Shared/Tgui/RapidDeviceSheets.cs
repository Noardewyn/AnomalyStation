using System.Linq;
using System.Collections.Concurrent;
using Anomaly.Core.DmShim;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tg.Defines;

namespace Anomaly.Client.Shared.Tgui;

// Translated from tgstation (snapshot 1d99bd7f): code/modules/asset_cache/assets/rcd.dm, rtd.dm and rdd.dm (E13, the RCD family's windows),
// plumbing.dm and chemmaster.dm (E15: the plumbing constructor's and the chemical press's windows), research_designs.dm (gaps P2: the
// biogenerator's designs).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
// The server sends no assets (DV44): the client builds each sheet's classes as tg's create_spritesheets inserts them, from the same design
// lists the server's GLOB holds (GlobalProcs.rcd_designs_list, floor_designs_list, rdd_designs_list) and the design types' initial vars.

/// <summary>The classes of the RCD's, the RTD's and the RDD's sprite sheets: the .dmi file, state and dir each one draws.</summary>
public static class RapidDeviceSheets
{
    private static readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, SpritesheetIcon>> Sheets = new();

    /// <summary>Every class of <paramref name="sheet"/> (<see cref="TguiSpritesheets.Rcd32"/>, <see cref="TguiSpritesheets.Rtd32"/> or <see cref="TguiSpritesheets.Rdd32"/>), built once.</summary>
    public static IReadOnlyDictionary<string, SpritesheetIcon> Classes(string sheet) => Sheets.GetOrAdd(sheet, s => s switch
    {
        TguiSpritesheets.Rcd32 => Rcd(),
        TguiSpritesheets.Rtd32 => Rtd(),
        TguiSpritesheets.Rdd32 => Rdd(),
        TguiSpritesheets.Plumbing32 => Plumbing(),
        TguiSpritesheets.Chemmaster32 => Chemmaster(),
        TguiSpritesheets.Design32 => Designs(),
        _ => new Dictionary<string, SpritesheetIcon>(),
    });

    private static string File(Type path) => Dm.initial(path, (atom a) => a.icon)?.ToString();

    private static string State(Type path) => Dm.initial(path, (atom a) => a.icon_state);

    private static string Css(object name) => Dm.str(GlobalProcs.sanitize_css_class_name(name));

    // tg: code/modules/asset_cache/assets/rcd.dm:4  (a later design of the same name replaces an earlier one, as insert_icon does)
    private static Dictionary<string, SpritesheetIcon> Rcd()
    {
        var classes = new Dictionary<string, SpritesheetIcon>(StringComparer.Ordinal);
        DmList designs = GlobalProcs.rcd_designs_list();
        foreach (object root_category in Dm.each(designs))
        {
            if (designs[root_category] is not DmList category_designs || category_designs.Count == 0)
                continue;
            foreach (object category in Dm.each(category_designs))
            {
                foreach (DmList design in Dm.each<DmList>(category_designs[category]))
                {
                    if (design[RCD_DESIGN_PATH] is not Type path)
                        continue;
                    string sprite_name = Dm.str(Dm.initial(path, (atom_movable a) => a.name));
                    SpritesheetIcon sprite_icon;
                    //icon for windows are blended with grills if required and loaded from radial menu
                    if (Dm.ispath(path, typeof(obj_structure_window)))
                    {
                        if (path == typeof(obj_structure_window))
                            sprite_icon = Of("icons/hud/radial.dmi", "windowsize");
                        else if (path == typeof(obj_structure_window_reinforced))
                            sprite_icon = Of("icons/hud/radial.dmi", "windowtype");
                        else if (path == typeof(obj_structure_window_fulltile) || path == typeof(obj_structure_window_reinforced_fulltile))
                            sprite_icon = Of(File(path), State(path)) with { Underlay = new SpritesheetLayer("icons/obj/structures.dmi", "grille") };
                        else
                            continue; // tg leaves sprite_icon null for any other window (no design is one)
                    }
                    //icons for solid airlocks have an added solid overlay on top of their glass icons
                    else if (Dm.ispath(path, typeof(obj_machinery_door_airlock)))
                    {
                        string airlock_icon = File(path);
                        sprite_icon = Of(airlock_icon, "closed");
                        if (!Dm.truthy(Dm.initial(path, (obj_machinery_door_airlock a) => a.glass)) && Dm.truthy(Dm.initial(path, (obj_machinery_door_airlock a) => a.can_be_glass)))
                            sprite_icon = sprite_icon with { Overlay = new SpritesheetLayer(airlock_icon, "fill_closed") };
                    }
                    //for all other icons we load the paths default icon & icon state
                    else
                        sprite_icon = Of(File(path), State(path));
                    classes[Css(sprite_name)] = sprite_icon;
                }
            }
        }
        return classes;
    }

    // tg: code/modules/asset_cache/assets/rtd.dm:4  "[icon_file]-[icon_state]-[dir2text(dir)]" for each design's turf; tg inserts the dirs of
    // the tile's rotations (or SOUTH), here every dir is listed (a class the window never names is never drawn)
    private static Dictionary<string, SpritesheetIcon> Rtd()
    {
        var classes = new Dictionary<string, SpritesheetIcon>(StringComparer.Ordinal);
        DmList designs = GlobalProcs.floor_designs_list();
        foreach (object main_root in Dm.each(designs))
        {
            foreach (object sub_category in Dm.each(designs[main_root]))
            {
                foreach (DmList design in Dm.each<DmList>(((DmList)designs[main_root])[sub_category]))
                {
                    Type turf_type = Dm.initial(design["type"] as Type, (obj_item_stack_tile t) => t.turf_type);
                    string icon_file = File(turf_type), icon_state = State(turf_type);
                    foreach ((string name, byte dir) in DirNames)
                        classes.TryAdd(Css($"{icon_file}-{icon_state}-{name}"), new SpritesheetIcon(new[] { icon_file }, icon_state, dir));
                }
            }
        }
        return classes;
    }

    // tg: code/modules/asset_cache/assets/rdd.dm:4  (icon.scale(32, 32): the kit draws every class in its 32 px box)
    private static Dictionary<string, SpritesheetIcon> Rdd()
    {
        var classes = new Dictionary<string, SpritesheetIcon>(StringComparer.Ordinal);
        DmList designs = GlobalProcs.rdd_designs_list();
        foreach (object category in Dm.each(designs))
        {
            foreach (DmList design in Dm.each<DmList>(designs[category]))
            {
                var dec_path = (Type)design["path"];
                classes[Css(design["name"])] = Of(File(dec_path), State(dec_path));
            }
        }
        return classes;
    }

    // tg: code/modules/asset_cache/assets/plumbing.dm:4  only the listed states of each file, each class its state
    private static Dictionary<string, SpritesheetIcon> Plumbing()
    {
        var essentials = new (string File, string[] States)[]
        {
            ("icons/obj/medical/iv_drip.dmi", new[] { "plumb" }),
            ("icons/obj/pipes_n_cables/hydrochem/fluid_ducts.dmi", new[] { "nduct" }),
            ("icons/hud/radial.dmi", new[] { "plumbing_layer1", "plumbing_layer2", "plumbing_layer4", "plumbing_layer8", "plumbing_layer16" }),
            ("icons/obj/pipes_n_cables/hydrochem/plumbers.dmi", new[]
            {
                "synthesizer", "reaction_chamber", "grinder_chemical", "fermenter", "pump", "disposal", "buffer", "manifold", "pipe_input", "filter",
                "splitter", "beacon", "pipe_output", "tank", "acclimator", "bottler", "pill_press", "synthesizer_soda", "synthesizer_booze", "tap_output",
            }),
        };
        var classes = new Dictionary<string, SpritesheetIcon>(StringComparer.Ordinal);
        foreach ((string file, string[] states) in essentials)
        {
            foreach (string state in states)
                classes[state] = Of(file, state);
        }
        return classes;
    }

    // tg: code/modules/asset_cache/assets/chemmaster.dm:5  each container of GLOB.reagent_containers by sanitize_css_class_name("[container]"); here
    // every /obj/item/reagent_containers type is listed (a class the window never names is never drawn), from the type's initial icon and state
    private static Dictionary<string, SpritesheetIcon> Chemmaster()
    {
        var classes = new Dictionary<string, SpritesheetIcon>(StringComparer.Ordinal);
        foreach (Type container in Dm.each_cast<Type>(Dm.typesof(typeof(obj_item_reagent_containers))))
        {
            string file = File(container), state = State(container);
            if (file != null && state != null)
                classes.TryAdd(Css(Dm.text("[]", container)), Of(file, state));
        }
        return classes;
    }

    // tg: code/modules/asset_cache/assets/research_designs.dm:5  each design by its id (get_design_id: the path after "/datum/design/" with
    // "/" as "_"): its research icon, else its build path's (a circuit board's machine), else a reagent design's default container. The GAGS
    // pictures and the colour transforms are not drawn (the kit draws a file's state as it is); the computers' screen overlays neither.
    private static Dictionary<string, SpritesheetIcon> Designs()
    {
        var classes = new Dictionary<string, SpritesheetIcon>(StringComparer.Ordinal);
        // valid_subtypesof(/datum/design), tg's abstract test type by type: GlobalProcs keeps its abstract table in a world, and the client
        // has none (silicons SI3: the window threw "No ambient World")
        foreach (Type path in Dm.each_cast<Type>(GlobalProcs.subtypesof(typeof(datum_design))).Where(t => Dm.initial(t, (datum d) => d.abstract_type) != t))
        {
            string file = Dm.initial(path, (datum_design d) => d.research_icon)?.ToString();
            string state = Dm.initial(path, (datum_design d) => d.research_icon_state)?.ToString();
            if (file == null || state == null)
            {
                Type item = Dm.initial(path, (datum_design d) => d.build_path);
                if (item == null || !Dm.ispath(item, typeof(atom)))
                {
                    Type reagent = Dm.initial(path, (datum_design d) => d.make_reagent);
                    if (reagent == null)
                        continue; // shouldn't happen, but just in case
                    item = Dm.initial(reagent, (datum_reagent r) => r.default_container);
                }
                if (item != null && Dm.ispath(item, typeof(obj_item_circuitboard)) && Dm.initial(item, (obj_item_circuitboard b) => b.build_path) is Type machine)
                    item = machine;
                if (item == null)
                    continue;
                file = File(item);
                state = State(item);
            }
            if (file == null || state == null)
                continue;
            string id = Dm.text("[]", path)["/datum/design/".Length..].Replace('/', '_');
            classes[id] = Of(file, state);
        }
        return classes;
    }

    private static SpritesheetIcon Of(string file, string state) => new(new[] { file }, state, 2); // uni_icon's default dir: SOUTH

    private static readonly (string, byte)[] DirNames =
    {
        ("north", 1), ("south", 2), ("east", 4), ("west", 8), ("northeast", 5), ("southeast", 6), ("northwest", 9), ("southwest", 10),
    };
}
