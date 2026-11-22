// Translated from tgstation (snapshot 1d99bd7f): code/_globalvars/lists/objects.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only GLOB.cable_list (M3 P4: the cables' init), the nav beacon lists (2026-10-18: the beacons' init) and the crafting and cooking recipe lists (M5 slice 2:
// init_crafting_recipes), intercoms_list and active_jammers (M9 slice D: the radio), ladders and stairs (D325: multi-z), rcd_list (E13: the RCDs), wire_color_directory and
// wire_name_directory (engineering E14: the wires' shared colours and the blueprints' legend), editable_sign_types (gaps S3), teleportbeacons (gaps E1a), experiment_handlers (gaps RD3), curtains and trophy_cases (gaps ST2) translated; the file's other object lists come with their users.

public partial class datum_controller_global_vars : datum_controller
{
    // The proc-level static of /obj/machinery/camera/exosuit/proc/update_c_tag (presets.dm:141): world state, so a list of the world.
    public DmList exosuit_camera_names;

    // tg: code/modules/vehicles/mecha/working/ripley.dm:246  GLOBAL_DATUM(cargo_ripley, /obj/vehicle/sealed/mecha/ripley/cargo)
    public obj_vehicle_sealed_mecha_ripley_cargo cargo_ripley;

    public virtual void InitGlobalcargo_ripley() => Dm.append(ref gvars_datum_init_order, "cargo_ripley");

    /// List of all portals
    // tg: code/_globalvars/lists/objects.dm:5  GLOBAL_LIST_EMPTY(portals)
    public DmList portals;

    // tg: code/_globalvars/lists/objects.dm:5  (GLOBAL_MANAGED(portals, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalportals()
    {
        portals = Dm.list();
        Dm.append(ref gvars_datum_init_order, "portals");
    }

    /// List of all curtains for button tracking
    // tg: code/_globalvars/lists/objects.dm:8  GLOBAL_LIST_EMPTY(curtains) (gaps ST2)
    public DmList curtains;

    // tg: code/_globalvars/lists/objects.dm:8  (GLOBAL_MANAGED(curtains, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalcurtains()
    {
        curtains = Dm.list();
        Dm.append(ref gvars_datum_init_order, "curtains");
    }

    /// List of all active teleport beacons
    // tg: code/_globalvars/lists/objects.dm:23  GLOBAL_LIST_EMPTY(teleportbeacons) (gaps E1a)
    public DmList teleportbeacons;

    // tg: code/_globalvars/lists/objects.dm:23  (GLOBAL_MANAGED(teleportbeacons, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalteleportbeacons()
    {
        teleportbeacons = Dm.list();
        Dm.append(ref gvars_datum_init_order, "teleportbeacons");
    }

    /// List of area names of roundstart station cyborg rechargers, for the low charge/no charge cyborg screen alert tooltips.
    // tg: code/_globalvars/lists/objects.dm:83  GLOBAL_LIST_EMPTY(roundstart_station_borgcharger_areas) (silicons SI2)
    public DmList roundstart_station_borgcharger_areas;

    public virtual void InitGlobalroundstart_station_borgcharger_areas()
    {
        roundstart_station_borgcharger_areas = Dm.list();
        Dm.append(ref gvars_datum_init_order, "roundstart_station_borgcharger_areas");
    }

    // tg: code/_globalvars/lists/objects.dm:86  GLOBAL_LIST_EMPTY(roundstart_station_mechcharger_areas)
    public DmList roundstart_station_mechcharger_areas;

    public virtual void InitGlobalroundstart_station_mechcharger_areas()
    {
        roundstart_station_mechcharger_areas = Dm.list();
        Dm.append(ref gvars_datum_init_order, "roundstart_station_mechcharger_areas");
    }

    // tg: code/_globalvars/lists/objects.dm:11  GLOBAL_LIST_EMPTY(mechas_list)
    public DmList mechas_list;

    // tg: code/_globalvars/lists/objects.dm:11  (GLOBAL_MANAGED(mechas_list, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalmechas_list()
    {
        mechas_list = Dm.list();
        Dm.append(ref gvars_datum_init_order, "mechas_list");
    }

    /// list of wallmounted intercom radios.
    // tg: code/_globalvars/lists/objects.dm:14  GLOBAL_LIST_EMPTY(shuttle_caller_list) (M10 slice G4)
    public DmList shuttle_caller_list;

    // tg: code/_globalvars/lists/objects.dm:14  (GLOBAL_MANAGED(shuttle_caller_list, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalshuttle_caller_list()
    {
        shuttle_caller_list = Dm.list();
        Dm.append(ref gvars_datum_init_order, "shuttle_caller_list");
    }

    // tg: code/_globalvars/lists/objects.dm:64  GLOBAL_LIST_EMPTY(ladders) (D325: multi-z)
    public DmList ladders;

    // tg: code/_globalvars/lists/objects.dm:64  (GLOBAL_MANAGED(ladders, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGloballadders()
    {
        ladders = Dm.list();
        Dm.append(ref gvars_datum_init_order, "ladders");
    }

    // tg: code/_globalvars/lists/objects.dm:65  GLOBAL_LIST_EMPTY(stairs) (D325: multi-z)
    public DmList stairs;

    // tg: code/_globalvars/lists/objects.dm:65  (GLOBAL_MANAGED(stairs, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalstairs()
    {
        stairs = Dm.list();
        Dm.append(ref gvars_datum_init_order, "stairs");
    }

    // tg: code/_globalvars/lists/objects.dm:66  GLOBAL_LIST_EMPTY(janitor_devices) (gaps J3: the access key; the mop and the janitor's cart join with their package)
    public DmList janitor_devices;

    // tg: code/_globalvars/lists/objects.dm:66  (GLOBAL_MANAGED(janitor_devices, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobaljanitor_devices()
    {
        janitor_devices = Dm.list();
        Dm.append(ref gvars_datum_init_order, "janitor_devices");
    }

    // tg: code/_globalvars/lists/objects.dm:67  GLOBAL_LIST_EMPTY(trophy_cases) (gaps ST2: the trophy display cases)
    public DmList trophy_cases;

    // tg: code/_globalvars/lists/objects.dm:67  (GLOBAL_MANAGED(trophy_cases, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobaltrophy_cases()
    {
        trophy_cases = Dm.list();
        Dm.append(ref gvars_datum_init_order, "trophy_cases");
    }

    // tg: code/_globalvars/lists/objects.dm:68  GLOBAL_LIST_EMPTY(experiment_handlers) (gaps RD3: every experiment handler, told of a completed experiment)
    public DmList experiment_handlers;

    // tg: code/_globalvars/lists/objects.dm:68  (GLOBAL_MANAGED(experiment_handlers, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalexperiment_handlers()
    {
        experiment_handlers = Dm.list();
        Dm.append(ref gvars_datum_init_order, "experiment_handlers");
    }

    /// list of Rapid Construction Devices.
    // tg: code/_globalvars/lists/objects.dm:53  GLOBAL_LIST_EMPTY(rcd_list) (E13)
    public DmList rcd_list;

    // tg: code/_globalvars/lists/objects.dm:53  (GLOBAL_MANAGED(rcd_list, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalrcd_list()
    {
        rcd_list = Dm.list();
        Dm.append(ref gvars_datum_init_order, "rcd_list");
    }

    // tg: code/_globalvars/lists/objects.dm:55  GLOBAL_LIST_EMPTY(intercoms_list) (M9 slice D)
    public DmList intercoms_list;

    // tg: code/_globalvars/lists/objects.dm:55  (GLOBAL_MANAGED(intercoms_list, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalintercoms_list()
    {
        intercoms_list = Dm.list();
        Dm.append(ref gvars_datum_init_order, "intercoms_list");
    }

    /// List of active radio jammers
    // tg: code/_globalvars/lists/objects.dm:63  GLOBAL_LIST_EMPTY(active_jammers) (M9 slice D; the jammer item is an antagonist's, so it stays empty)
    public DmList active_jammers;

    // tg: code/_globalvars/lists/objects.dm:63  (GLOBAL_MANAGED(active_jammers, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalactive_jammers()
    {
        active_jammers = Dm.list();
        Dm.append(ref gvars_datum_init_order, "active_jammers");
    }

    // tg: code/_globalvars/lists/objects.dm:2  GLOBAL_LIST_EMPTY(cable_list)
    public DmList cable_list;

    // tg: code/_globalvars/lists/objects.dm:2  (GLOBAL_MANAGED(cable_list, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalcable_list()
    {
        cable_list = Dm.list();
        Dm.append(ref gvars_datum_init_order, "cable_list");
    }
    // tg: code/_globalvars/lists/objects.dm:80  GLOBAL_LIST_EMPTY(joinable_mobs) (gaps package C3: the bots' offer to ghosts)
    public DmList joinable_mobs;

    public virtual void InitGlobaljoinable_mobs()
    {
        joinable_mobs = Dm.list();
        Dm.append(ref gvars_datum_init_order, "joinable_mobs");
    }

    /// List of all nav beacons indexed by stringified z level
    // tg: code/_globalvars/lists/objects.dm:20  GLOBAL_LIST_EMPTY(navbeacons) (2026-10-18, the nav beacons' init)
    public DmList navbeacons;

    // tg: code/_globalvars/lists/objects.dm:20  (GLOBAL_MANAGED(navbeacons, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalnavbeacons()
    {
        navbeacons = Dm.list();
        Dm.append(ref gvars_datum_init_order, "navbeacons");
    }

    /// List of all active delivery beacons
    // tg: code/_globalvars/lists/objects.dm:26  GLOBAL_LIST_EMPTY(deliverybeacons)
    public DmList deliverybeacons;

    // tg: code/_globalvars/lists/objects.dm:26  (GLOBAL_MANAGED(deliverybeacons, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobaldeliverybeacons()
    {
        deliverybeacons = Dm.list();
        Dm.append(ref gvars_datum_init_order, "deliverybeacons");
    }

    /// List of all active delivery beacon locations
    // tg: code/_globalvars/lists/objects.dm:29  GLOBAL_LIST_EMPTY(deliverybeacontags)
    public DmList deliverybeacontags;

    // tg: code/_globalvars/lists/objects.dm:29  (GLOBAL_MANAGED(deliverybeacontags, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobaldeliverybeacontags()
    {
        deliverybeacontags = Dm.list();
        Dm.append(ref gvars_datum_init_order, "deliverybeacontags");
    }

    // tg: code/_globalvars/lists/objects.dm:32  GLOBAL_LIST_EMPTY_TYPED(singularities, /datum/component/singularity) (M5x slice 5)
    public DmList singularities;

    // tg: code/_globalvars/lists/objects.dm:32  (GLOBAL_MANAGED(singularities, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalsingularities()
    {
        singularities = Dm.list();
        Dm.append(ref gvars_datum_init_order, "singularities");
    }
    // tg: code/_globalvars/lists/objects.dm:34  GLOBAL_LIST_EMPTY(crafting_recipes)
    public DmList crafting_recipes;

    // tg: code/_globalvars/lists/objects.dm:34  (GLOBAL_MANAGED(crafting_recipes, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalcrafting_recipes()
    {
        crafting_recipes = Dm.list();
        Dm.append(ref gvars_datum_init_order, "crafting_recipes");
    }

    // tg: code/_globalvars/lists/objects.dm:36  GLOBAL_LIST_EMPTY(crafting_recipes_default)
    public DmList crafting_recipes_default;

    // tg: code/_globalvars/lists/objects.dm:36  (GLOBAL_MANAGED(crafting_recipes_default, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalcrafting_recipes_default()
    {
        crafting_recipes_default = Dm.list();
        Dm.append(ref gvars_datum_init_order, "crafting_recipes_default");
    }

    // tg: code/_globalvars/lists/objects.dm:38  GLOBAL_LIST_EMPTY(crafting_recipes_by_typepath)
    public DmList crafting_recipes_by_typepath;

    // tg: code/_globalvars/lists/objects.dm:38  (GLOBAL_MANAGED(crafting_recipes_by_typepath, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalcrafting_recipes_by_typepath()
    {
        crafting_recipes_by_typepath = Dm.list();
        Dm.append(ref gvars_datum_init_order, "crafting_recipes_by_typepath");
    }

    // tg: code/_globalvars/lists/objects.dm:41  GLOBAL_LIST_EMPTY(crafting_recipes_datums)
    public DmList crafting_recipes_datums;

    // tg: code/_globalvars/lists/objects.dm:41  (GLOBAL_MANAGED(crafting_recipes_datums, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalcrafting_recipes_datums()
    {
        crafting_recipes_datums = Dm.list();
        Dm.append(ref gvars_datum_init_order, "crafting_recipes_datums");
    }

    // tg: code/_globalvars/lists/objects.dm:43  GLOBAL_LIST_EMPTY(cooking_recipes)
    public DmList cooking_recipes;

    // tg: code/_globalvars/lists/objects.dm:43  (GLOBAL_MANAGED(cooking_recipes, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalcooking_recipes()
    {
        cooking_recipes = Dm.list();
        Dm.append(ref gvars_datum_init_order, "cooking_recipes");
    }

    // tg: code/_globalvars/lists/objects.dm:45  GLOBAL_LIST_EMPTY(cooking_recipes_default)
    public DmList cooking_recipes_default;

    // tg: code/_globalvars/lists/objects.dm:45  (GLOBAL_MANAGED(cooking_recipes_default, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalcooking_recipes_default()
    {
        cooking_recipes_default = Dm.list();
        Dm.append(ref gvars_datum_init_order, "cooking_recipes_default");
    }

    // tg: code/_globalvars/lists/objects.dm:47  GLOBAL_LIST_EMPTY(cooking_recipes_by_typepath)
    public DmList cooking_recipes_by_typepath;

    // tg: code/_globalvars/lists/objects.dm:47  (GLOBAL_MANAGED(cooking_recipes_by_typepath, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalcooking_recipes_by_typepath()
    {
        cooking_recipes_by_typepath = Dm.list();
        Dm.append(ref gvars_datum_init_order, "cooking_recipes_by_typepath");
    }

    // tg: code/_globalvars/lists/objects.dm:50  GLOBAL_LIST_EMPTY(cooking_recipes_datums)
    public DmList cooking_recipes_datums;

    // tg: code/_globalvars/lists/objects.dm:50  (GLOBAL_MANAGED(cooking_recipes_datums, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalcooking_recipes_datums()
    {
        cooking_recipes_datums = Dm.list();
        Dm.append(ref gvars_datum_init_order, "cooking_recipes_datums");
    }

    // tg: code/_globalvars/lists/objects.dm:73  GLOBAL_LIST_EMPTY(wire_color_directory) (engineering E14: the blueprints' wire legend)
    public DmList wire_color_directory;

    // tg: code/_globalvars/lists/objects.dm:73  (GLOBAL_MANAGED(wire_color_directory, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalwire_color_directory()
    {
        wire_color_directory = Dm.list();
        Dm.append(ref gvars_datum_init_order, "wire_color_directory");
    }

    // tg: code/_globalvars/lists/objects.dm:74  GLOBAL_LIST_EMPTY(wire_name_directory) (engineering E14)
    public DmList wire_name_directory;

    // tg: code/_globalvars/lists/objects.dm:74  (GLOBAL_MANAGED(wire_name_directory, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalwire_name_directory()
    {
        wire_name_directory = Dm.list();
        Dm.append(ref gvars_datum_init_order, "wire_name_directory");
    }

    // tg: code/_globalvars/lists/objects.dm:71  GLOBAL_LIST_INIT(editable_sign_types, populate_editable_sign_types()) (gaps S3: a pen changes a sign)
    public DmList editable_sign_types;

    // tg: code/_globalvars/lists/objects.dm:71  (GLOBAL_MANAGED(editable_sign_types, populate_editable_sign_types()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobaleditable_sign_types()
    {
        editable_sign_types = populate_editable_sign_types() as DmList;
        Dm.append(ref gvars_datum_init_order, "editable_sign_types");
    }

    // silicons SI7 (D326)
    // tg: code/_globalvars/lists/objects.dm:77  GLOBAL_LIST_EMPTY(mob_spawners): names of the ghost role spawners and the spawners of each
    public DmList mob_spawners;

    // tg: code/_globalvars/lists/objects.dm:77  (GLOBAL_MANAGED(mob_spawners, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalmob_spawners()
    {
        mob_spawners = Dm.list();
        Dm.append(ref gvars_datum_init_order, "mob_spawners");
    }
}
