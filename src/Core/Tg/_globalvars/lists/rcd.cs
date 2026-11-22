// Translated from tgstation (snapshot 1d99bd7f): code/_globalvars/lists/rcd.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// E13 (the RCD family): the whole file. The list is built by rcd_designs_list, which the client's sprite sheet of the RCD's window
// (TguiSpritesheets) also reads, without a world.

public partial class datum_controller_global_vars
{
    ///all stuff used by RCD for construction
    // tg: code/_globalvars/lists/rcd.dm:2  GLOBAL_LIST_INIT(rcd_designs, list(...))
    public DmList rcd_designs;

    // tg: code/_globalvars/lists/rcd.dm:2  (GLOBAL_MANAGED(rcd_designs, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalrcd_designs()
    {
        rcd_designs = rcd_designs_list();
        Dm.append(ref gvars_datum_init_order, "rcd_designs");
    }
}

public static partial class GlobalProcs
{
    // The port's own: GLOBAL_LIST_INIT(rcd_designs)'s list literal (rcd.dm:2-91), a proc so that the client's sprite sheet can build the
    // same designs without a world. The root categories, their sub categories and the designs in tg's order.
    public static DmList rcd_designs_list()
    {
        return Dm.list(
            Dm.kv("Construction", Dm.list(
                Dm.kv("Structures", Dm.list(
                    Dm.list(Dm.kv(RCD_DESIGN_MODE, RCD_TURF), Dm.kv(RCD_DESIGN_PATH, typeof(turf_open_floor_plating_rcd))),
                    Dm.list(Dm.kv(RCD_DESIGN_MODE, RCD_WINDOWGRILLE), Dm.kv(RCD_DESIGN_PATH, typeof(obj_structure_window))),
                    Dm.list(Dm.kv(RCD_DESIGN_MODE, RCD_WINDOWGRILLE), Dm.kv(RCD_DESIGN_PATH, typeof(obj_structure_window_reinforced))),
                    Dm.list(Dm.kv(RCD_DESIGN_MODE, RCD_WINDOWGRILLE), Dm.kv(RCD_DESIGN_PATH, typeof(obj_structure_window_fulltile))),
                    Dm.list(Dm.kv(RCD_DESIGN_MODE, RCD_WINDOWGRILLE), Dm.kv(RCD_DESIGN_PATH, typeof(obj_structure_window_reinforced_fulltile))),
                    Dm.list(Dm.kv(RCD_DESIGN_MODE, RCD_STRUCTURE), Dm.kv(RCD_DESIGN_PATH, typeof(obj_structure_reflector_wrenched))),
                    Dm.list(Dm.kv(RCD_DESIGN_MODE, RCD_TURF), Dm.kv(RCD_DESIGN_PATH, typeof(obj_structure_lattice_catwalk))),
                    Dm.list(Dm.kv(RCD_DESIGN_MODE, RCD_STRUCTURE), Dm.kv(RCD_DESIGN_PATH, typeof(obj_structure_girder))))),
                Dm.kv("Machines", Dm.list(
                    Dm.list(Dm.kv(RCD_DESIGN_MODE, RCD_STRUCTURE), Dm.kv(RCD_DESIGN_PATH, typeof(obj_structure_frame_machine_secured))),
                    Dm.list(Dm.kv(RCD_DESIGN_MODE, RCD_STRUCTURE), Dm.kv(RCD_DESIGN_PATH, typeof(obj_structure_frame_computer_rcd_north))),
                    Dm.list(Dm.kv(RCD_DESIGN_MODE, RCD_STRUCTURE), Dm.kv(RCD_DESIGN_PATH, typeof(obj_structure_frame_computer_rcd_south))),
                    Dm.list(Dm.kv(RCD_DESIGN_MODE, RCD_STRUCTURE), Dm.kv(RCD_DESIGN_PATH, typeof(obj_structure_frame_computer_rcd_east))),
                    Dm.list(Dm.kv(RCD_DESIGN_MODE, RCD_STRUCTURE), Dm.kv(RCD_DESIGN_PATH, typeof(obj_structure_frame_computer_rcd_west))),
                    Dm.list(Dm.kv(RCD_DESIGN_MODE, RCD_STRUCTURE), Dm.kv(RCD_DESIGN_PATH, typeof(obj_structure_floodlight_frame_completed))),
                    Dm.list(Dm.kv(RCD_DESIGN_MODE, RCD_WALLFRAME), Dm.kv(RCD_DESIGN_PATH, typeof(obj_item_wallframe_apc))),
                    Dm.list(Dm.kv(RCD_DESIGN_MODE, RCD_WALLFRAME), Dm.kv(RCD_DESIGN_PATH, typeof(obj_item_wallframe_airalarm))),
                    Dm.list(Dm.kv(RCD_DESIGN_MODE, RCD_WALLFRAME), Dm.kv(RCD_DESIGN_PATH, typeof(obj_item_wallframe_firealarm))))),
                Dm.kv("Furniture", Dm.list(
                    Dm.list(Dm.kv(RCD_DESIGN_MODE, RCD_STRUCTURE), Dm.kv(RCD_DESIGN_PATH, typeof(obj_structure_chair))),
                    Dm.list(Dm.kv(RCD_DESIGN_MODE, RCD_STRUCTURE), Dm.kv(RCD_DESIGN_PATH, typeof(obj_structure_chair_stool))),
                    Dm.list(Dm.kv(RCD_DESIGN_MODE, RCD_STRUCTURE), Dm.kv(RCD_DESIGN_PATH, typeof(obj_structure_chair_stool_bar))),
                    Dm.list(Dm.kv(RCD_DESIGN_MODE, RCD_STRUCTURE), Dm.kv(RCD_DESIGN_PATH, typeof(obj_structure_table))),
                    Dm.list(Dm.kv(RCD_DESIGN_MODE, RCD_STRUCTURE), Dm.kv(RCD_DESIGN_PATH, typeof(obj_structure_table_glass))),
                    Dm.list(Dm.kv(RCD_DESIGN_MODE, RCD_STRUCTURE), Dm.kv(RCD_DESIGN_PATH, typeof(obj_structure_rack))),
                    Dm.list(Dm.kv(RCD_DESIGN_MODE, RCD_STRUCTURE), Dm.kv(RCD_DESIGN_PATH, typeof(obj_structure_bed))))))),
            Dm.kv("Airlocks", Dm.list(
                Dm.kv("Windoors", Dm.list(
                    Dm.list(Dm.kv(RCD_DESIGN_MODE, RCD_AIRLOCK), Dm.kv(RCD_DESIGN_PATH, typeof(obj_machinery_door_window))),
                    Dm.list(Dm.kv(RCD_DESIGN_MODE, RCD_AIRLOCK), Dm.kv(RCD_DESIGN_PATH, typeof(obj_machinery_door_window_brigdoor))))),
                Dm.kv("Glass Airlocks", Dm.list(
                    Dm.list(Dm.kv(RCD_DESIGN_MODE, RCD_AIRLOCK), Dm.kv(RCD_DESIGN_PATH, typeof(obj_machinery_door_airlock_glass))),
                    Dm.list(Dm.kv(RCD_DESIGN_MODE, RCD_AIRLOCK), Dm.kv(RCD_DESIGN_PATH, typeof(obj_machinery_door_airlock_public_glass))),
                    Dm.list(Dm.kv(RCD_DESIGN_MODE, RCD_AIRLOCK), Dm.kv(RCD_DESIGN_PATH, typeof(obj_machinery_door_airlock_engineering_glass))),
                    Dm.list(Dm.kv(RCD_DESIGN_MODE, RCD_AIRLOCK), Dm.kv(RCD_DESIGN_PATH, typeof(obj_machinery_door_airlock_atmos_glass))),
                    Dm.list(Dm.kv(RCD_DESIGN_MODE, RCD_AIRLOCK), Dm.kv(RCD_DESIGN_PATH, typeof(obj_machinery_door_airlock_security_glass))),
                    Dm.list(Dm.kv(RCD_DESIGN_MODE, RCD_AIRLOCK), Dm.kv(RCD_DESIGN_PATH, typeof(obj_machinery_door_airlock_command_glass))),
                    Dm.list(Dm.kv(RCD_DESIGN_MODE, RCD_AIRLOCK), Dm.kv(RCD_DESIGN_PATH, typeof(obj_machinery_door_airlock_medical_glass))),
                    Dm.list(Dm.kv(RCD_DESIGN_MODE, RCD_AIRLOCK), Dm.kv(RCD_DESIGN_PATH, typeof(obj_machinery_door_airlock_research_glass))),
                    Dm.list(Dm.kv(RCD_DESIGN_MODE, RCD_AIRLOCK), Dm.kv(RCD_DESIGN_PATH, typeof(obj_machinery_door_airlock_hydroponics_glass))),
                    Dm.list(Dm.kv(RCD_DESIGN_MODE, RCD_AIRLOCK), Dm.kv(RCD_DESIGN_PATH, typeof(obj_machinery_door_airlock_virology_glass))),
                    Dm.list(Dm.kv(RCD_DESIGN_MODE, RCD_AIRLOCK), Dm.kv(RCD_DESIGN_PATH, typeof(obj_machinery_door_airlock_mining_glass))),
                    Dm.list(Dm.kv(RCD_DESIGN_MODE, RCD_AIRLOCK), Dm.kv(RCD_DESIGN_PATH, typeof(obj_machinery_door_airlock_maintenance_glass))),
                    Dm.list(Dm.kv(RCD_DESIGN_MODE, RCD_AIRLOCK), Dm.kv(RCD_DESIGN_PATH, typeof(obj_machinery_door_airlock_external_glass))),
                    Dm.list(Dm.kv(RCD_DESIGN_MODE, RCD_AIRLOCK), Dm.kv(RCD_DESIGN_PATH, typeof(obj_machinery_door_airlock_maintenance_external_glass))))),
                Dm.kv("Solid Airlocks", Dm.list(
                    Dm.list(Dm.kv(RCD_DESIGN_MODE, RCD_AIRLOCK), Dm.kv(RCD_DESIGN_PATH, typeof(obj_machinery_door_airlock))),
                    Dm.list(Dm.kv(RCD_DESIGN_MODE, RCD_AIRLOCK), Dm.kv(RCD_DESIGN_PATH, typeof(obj_machinery_door_airlock_public))),
                    Dm.list(Dm.kv(RCD_DESIGN_MODE, RCD_AIRLOCK), Dm.kv(RCD_DESIGN_PATH, typeof(obj_machinery_door_airlock_engineering))),
                    Dm.list(Dm.kv(RCD_DESIGN_MODE, RCD_AIRLOCK), Dm.kv(RCD_DESIGN_PATH, typeof(obj_machinery_door_airlock_atmos))),
                    Dm.list(Dm.kv(RCD_DESIGN_MODE, RCD_AIRLOCK), Dm.kv(RCD_DESIGN_PATH, typeof(obj_machinery_door_airlock_security))),
                    Dm.list(Dm.kv(RCD_DESIGN_MODE, RCD_AIRLOCK), Dm.kv(RCD_DESIGN_PATH, typeof(obj_machinery_door_airlock_command))),
                    Dm.list(Dm.kv(RCD_DESIGN_MODE, RCD_AIRLOCK), Dm.kv(RCD_DESIGN_PATH, typeof(obj_machinery_door_airlock_medical))),
                    Dm.list(Dm.kv(RCD_DESIGN_MODE, RCD_AIRLOCK), Dm.kv(RCD_DESIGN_PATH, typeof(obj_machinery_door_airlock_research))),
                    Dm.list(Dm.kv(RCD_DESIGN_MODE, RCD_AIRLOCK), Dm.kv(RCD_DESIGN_PATH, typeof(obj_machinery_door_airlock_freezer))),
                    Dm.list(Dm.kv(RCD_DESIGN_MODE, RCD_AIRLOCK), Dm.kv(RCD_DESIGN_PATH, typeof(obj_machinery_door_airlock_hydroponics))),
                    Dm.list(Dm.kv(RCD_DESIGN_MODE, RCD_AIRLOCK), Dm.kv(RCD_DESIGN_PATH, typeof(obj_machinery_door_airlock_virology))),
                    Dm.list(Dm.kv(RCD_DESIGN_MODE, RCD_AIRLOCK), Dm.kv(RCD_DESIGN_PATH, typeof(obj_machinery_door_airlock_mining))),
                    Dm.list(Dm.kv(RCD_DESIGN_MODE, RCD_AIRLOCK), Dm.kv(RCD_DESIGN_PATH, typeof(obj_machinery_door_airlock_maintenance))),
                    Dm.list(Dm.kv(RCD_DESIGN_MODE, RCD_AIRLOCK), Dm.kv(RCD_DESIGN_PATH, typeof(obj_machinery_door_airlock_external))),
                    Dm.list(Dm.kv(RCD_DESIGN_MODE, RCD_AIRLOCK), Dm.kv(RCD_DESIGN_PATH, typeof(obj_machinery_door_airlock_maintenance_external))),
                    Dm.list(Dm.kv(RCD_DESIGN_MODE, RCD_AIRLOCK), Dm.kv(RCD_DESIGN_PATH, typeof(obj_machinery_door_airlock_hatch))),
                    Dm.list(Dm.kv(RCD_DESIGN_MODE, RCD_AIRLOCK), Dm.kv(RCD_DESIGN_PATH, typeof(obj_machinery_door_airlock_maintenance_hatch))))))),
            Dm.kv("Airlock Access", Dm.list()));
    }
}
