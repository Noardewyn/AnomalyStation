// Translated from tgstation (snapshot 1d99bd7f): code/_globalvars/lists/rtd.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// E13 (the RCD family): the whole file, the rapid tiling device's designs. The list is built by floor_designs_list, which the
// client's sprite sheet of the RTD's window (TguiSpritesheets) also reads, without a world.

public partial class datum_controller_global_vars
{
    ///all designs supported by the RTD
    // tg: code/_globalvars/lists/rtd.dm:2  GLOBAL_LIST_INIT(floor_designs, list(...))
    public DmList floor_designs;

    // tg: code/_globalvars/lists/rtd.dm:2  (GLOBAL_MANAGED(floor_designs, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalfloor_designs()
    {
        floor_designs = floor_designs_list();
        Dm.append(ref gvars_datum_init_order, "floor_designs");
    }
}

public static partial class GlobalProcs
{
    // The port's own: GLOBAL_LIST_INIT(floor_designs)'s list literal (rtd.dm:2-113), a proc so that the client's sprite sheet can
    // build the same designs without a world. The root categories, their sub categories and the designs in tg's order.
    public static DmList floor_designs_list()
    {
        return Dm.list(
            Dm.kv("Conventional", Dm.list(
                Dm.kv("Standard", Dm.list(
                    Dm.list(Dm.kv("name", "Base"), Dm.kv("type", typeof(obj_item_stack_tile_iron)), Dm.kv("tile_cost", 4)),
                    Dm.list(Dm.kv("name", "Small"), Dm.kv("type", typeof(obj_item_stack_tile_iron_small)), Dm.kv("tile_cost", 3)),
                    Dm.list(Dm.kv("name", "Large"), Dm.kv("type", typeof(obj_item_stack_tile_iron_large)), Dm.kv("tile_cost", 7)),
                    Dm.list(Dm.kv("name", "Diagonal"), Dm.kv("type", typeof(obj_item_stack_tile_iron_diagonal)), Dm.kv("tile_cost", 5)),
                    Dm.list(Dm.kv("name", "Edge"), Dm.kv("type", typeof(obj_item_stack_tile_iron_edge)), Dm.kv("tile_cost", 5)),
                    Dm.list(Dm.kv("name", "Half"), Dm.kv("type", typeof(obj_item_stack_tile_iron_half)), Dm.kv("tile_cost", 5)),
                    Dm.list(Dm.kv("name", "Corner"), Dm.kv("type", typeof(obj_item_stack_tile_iron_corner)), Dm.kv("tile_cost", 5)),
                    Dm.list(Dm.kv("name", "Textured"), Dm.kv("type", typeof(obj_item_stack_tile_iron_textured)), Dm.kv("tile_cost", 5)),
                    Dm.list(Dm.kv("name", "Textured Edge"), Dm.kv("type", typeof(obj_item_stack_tile_iron_textured_edge)), Dm.kv("tile_cost", 6)),
                    Dm.list(Dm.kv("name", "Textured Half"), Dm.kv("type", typeof(obj_item_stack_tile_iron_textured_half)), Dm.kv("tile_cost", 6)),
                    Dm.list(Dm.kv("name", "Textured Corner"), Dm.kv("type", typeof(obj_item_stack_tile_iron_textured_corner)), Dm.kv("tile_cost", 6)),
                    Dm.list(Dm.kv("name", "Textured Large"), Dm.kv("type", typeof(obj_item_stack_tile_iron_textured_large)), Dm.kv("tile_cost", 6)))),
                Dm.kv("Translucent", Dm.list(
                    Dm.list(Dm.kv("name", "Smooth"), Dm.kv("type", typeof(obj_item_stack_tile_iron_smooth)), Dm.kv("tile_cost", 4)),
                    Dm.list(Dm.kv("name", "Smooth Edge"), Dm.kv("type", typeof(obj_item_stack_tile_iron_smooth_edge)), Dm.kv("tile_cost", 4)),
                    Dm.list(Dm.kv("name", "Smooth Half"), Dm.kv("type", typeof(obj_item_stack_tile_iron_smooth_half)), Dm.kv("tile_cost", 4)),
                    Dm.list(Dm.kv("name", "Smooth Corner"), Dm.kv("type", typeof(obj_item_stack_tile_iron_smooth_corner)), Dm.kv("tile_cost", 4)),
                    Dm.list(Dm.kv("name", "Smooth Large"), Dm.kv("type", typeof(obj_item_stack_tile_iron_smooth_large)), Dm.kv("tile_cost", 7)),
                    Dm.list(Dm.kv("name", "Freezer"), Dm.kv("type", typeof(obj_item_stack_tile_iron_freezer)), Dm.kv("tile_cost", 5)),
                    Dm.list(Dm.kv("name", "Showroom"), Dm.kv("type", typeof(obj_item_stack_tile_iron_showroomfloor)), Dm.kv("tile_cost", 5)),
                    Dm.list(Dm.kv("name", "Glass"), Dm.kv("type", typeof(obj_item_stack_tile_glass)), Dm.kv("tile_cost", 5)),
                    Dm.list(Dm.kv("name", "Reinforced Glass"), Dm.kv("type", typeof(obj_item_stack_tile_rglass)), Dm.kv("tile_cost", 10)))),
                Dm.kv("Circuit", Dm.list(
                    Dm.list(Dm.kv("name", "Recharge"), Dm.kv("type", typeof(obj_item_stack_tile_iron_recharge_floor)), Dm.kv("tile_cost", 5)),
                    Dm.list(Dm.kv("name", "Solar Panel"), Dm.kv("type", typeof(obj_item_stack_tile_iron_solarpanel)), Dm.kv("tile_cost", 5)),
                    Dm.list(Dm.kv("name", "Blue Circuit"), Dm.kv("type", typeof(obj_item_stack_tile_circuit)), Dm.kv("tile_cost", 5)),
                    Dm.list(Dm.kv("name", "Green Circuit"), Dm.kv("type", typeof(obj_item_stack_tile_circuit_green)), Dm.kv("tile_cost", 5)),
                    Dm.list(Dm.kv("name", "Green Circuit Anim"), Dm.kv("type", typeof(obj_item_stack_tile_circuit_green_anim)), Dm.kv("tile_cost", 5)),
                    Dm.list(Dm.kv("name", "Red Circuit"), Dm.kv("type", typeof(obj_item_stack_tile_circuit_red)), Dm.kv("tile_cost", 5)),
                    Dm.list(Dm.kv("name", "Red Circuit Anim"), Dm.kv("type", typeof(obj_item_stack_tile_circuit_red_anim)), Dm.kv("tile_cost", 5)))))),
            Dm.kv("Decorated", Dm.list(
                Dm.kv("Dark Colored", Dm.list(
                    Dm.list(Dm.kv("name", "Base"), Dm.kv("type", typeof(obj_item_stack_tile_iron_dark)), Dm.kv("tile_cost", 4)),
                    Dm.list(Dm.kv("name", "Smooth Edge"), Dm.kv("type", typeof(obj_item_stack_tile_iron_dark_smooth_edge)), Dm.kv("tile_cost", 4)),
                    Dm.list(Dm.kv("name", "Smooth Half"), Dm.kv("type", typeof(obj_item_stack_tile_iron_dark_smooth_half)), Dm.kv("tile_cost", 4)),
                    Dm.list(Dm.kv("name", "Smooth Corner"), Dm.kv("type", typeof(obj_item_stack_tile_iron_dark_smooth_corner)), Dm.kv("tile_cost", 4)),
                    Dm.list(Dm.kv("name", "Smooth Large"), Dm.kv("type", typeof(obj_item_stack_tile_iron_dark_smooth_large)), Dm.kv("tile_cost", 7)),
                    Dm.list(Dm.kv("name", "Small"), Dm.kv("type", typeof(obj_item_stack_tile_iron_dark_small)), Dm.kv("tile_cost", 4)),
                    Dm.list(Dm.kv("name", "Diagonal"), Dm.kv("type", typeof(obj_item_stack_tile_iron_dark_diagonal)), Dm.kv("tile_cost", 4)),
                    Dm.list(Dm.kv("name", "Herringbone"), Dm.kv("type", typeof(obj_item_stack_tile_iron_dark_herringbone)), Dm.kv("tile_cost", 4)),
                    Dm.list(Dm.kv("name", "Half Dark"), Dm.kv("type", typeof(obj_item_stack_tile_iron_dark_side)), Dm.kv("tile_cost", 4)),
                    Dm.list(Dm.kv("name", "Dark Corner"), Dm.kv("type", typeof(obj_item_stack_tile_iron_dark_corner)), Dm.kv("tile_cost", 4)))),
                Dm.kv("White Colored", Dm.list(
                    Dm.list(Dm.kv("name", "Base"), Dm.kv("type", typeof(obj_item_stack_tile_iron_white)), Dm.kv("tile_cost", 5)),
                    Dm.list(Dm.kv("name", "Smooth Edge"), Dm.kv("type", typeof(obj_item_stack_tile_iron_white_smooth_edge)), Dm.kv("tile_cost", 5)),
                    Dm.list(Dm.kv("name", "Smooth Half"), Dm.kv("type", typeof(obj_item_stack_tile_iron_white_smooth_half)), Dm.kv("tile_cost", 5)),
                    Dm.list(Dm.kv("name", "Smooth Corner"), Dm.kv("type", typeof(obj_item_stack_tile_iron_white_smooth_corner)), Dm.kv("tile_cost", 5)),
                    Dm.list(Dm.kv("name", "Smooth Large"), Dm.kv("type", typeof(obj_item_stack_tile_iron_white_smooth_large)), Dm.kv("tile_cost", 7)),
                    Dm.list(Dm.kv("name", "Small"), Dm.kv("type", typeof(obj_item_stack_tile_iron_white_small)), Dm.kv("tile_cost", 5)),
                    Dm.list(Dm.kv("name", "Diagonal"), Dm.kv("type", typeof(obj_item_stack_tile_iron_white_diagonal)), Dm.kv("tile_cost", 5)),
                    Dm.list(Dm.kv("name", "Herringbone"), Dm.kv("type", typeof(obj_item_stack_tile_iron_white_herringbone)), Dm.kv("tile_cost", 5)),
                    Dm.list(Dm.kv("name", "Half White"), Dm.kv("type", typeof(obj_item_stack_tile_iron_white_side)), Dm.kv("tile_cost", 5)),
                    Dm.list(Dm.kv("name", "White Corner"), Dm.kv("type", typeof(obj_item_stack_tile_iron_white_corner)), Dm.kv("tile_cost", 5)))),
                Dm.kv("Textured", Dm.list(
                    Dm.list(Dm.kv("name", "Textured White"), Dm.kv("type", typeof(obj_item_stack_tile_iron_white_textured)), Dm.kv("tile_cost", 5)),
                    Dm.list(Dm.kv("name", "Textured White Edge"), Dm.kv("type", typeof(obj_item_stack_tile_iron_white_textured_edge)), Dm.kv("tile_cost", 5)),
                    Dm.list(Dm.kv("name", "Textured White Half"), Dm.kv("type", typeof(obj_item_stack_tile_iron_white_textured_half)), Dm.kv("tile_cost", 5)),
                    Dm.list(Dm.kv("name", "Textured White Corner"), Dm.kv("type", typeof(obj_item_stack_tile_iron_white_textured_corner)), Dm.kv("tile_cost", 5)),
                    Dm.list(Dm.kv("name", "Textured White Large"), Dm.kv("type", typeof(obj_item_stack_tile_iron_white_textured_large)), Dm.kv("tile_cost", 7)),
                    Dm.list(Dm.kv("name", "Textured Dark"), Dm.kv("type", typeof(obj_item_stack_tile_iron_dark_textured)), Dm.kv("tile_cost", 5)),
                    Dm.list(Dm.kv("name", "Textured Dark Edge"), Dm.kv("type", typeof(obj_item_stack_tile_iron_dark_textured_edge)), Dm.kv("tile_cost", 5)),
                    Dm.list(Dm.kv("name", "Textured Dark Half"), Dm.kv("type", typeof(obj_item_stack_tile_iron_dark_textured_half)), Dm.kv("tile_cost", 5)),
                    Dm.list(Dm.kv("name", "Textured Dark Corner"), Dm.kv("type", typeof(obj_item_stack_tile_iron_dark_textured_corner)), Dm.kv("tile_cost", 5)),
                    Dm.list(Dm.kv("name", "Textured Dark Large"), Dm.kv("type", typeof(obj_item_stack_tile_iron_dark_textured_large)), Dm.kv("tile_cost", 7)))))),
            Dm.kv("Interior", Dm.list(
                Dm.kv("Room", Dm.list(
                    Dm.list(Dm.kv("name", "Kitchen"), Dm.kv("type", typeof(obj_item_stack_tile_iron_kitchen)), Dm.kv("tile_cost", 4)),
                    Dm.list(Dm.kv("name", "Kitchen Small"), Dm.kv("type", typeof(obj_item_stack_tile_iron_kitchen_small)), Dm.kv("tile_cost", 4)),
                    Dm.list(Dm.kv("name", "Diagonal Kitchen"), Dm.kv("type", typeof(obj_item_stack_tile_iron_kitchen_diagonal)), Dm.kv("tile_cost", 4)),
                    Dm.list(Dm.kv("name", "Chapel"), Dm.kv("type", typeof(obj_item_stack_tile_iron_chapel)), Dm.kv("tile_cost", 4)),
                    Dm.list(Dm.kv("name", "Cafeteria"), Dm.kv("type", typeof(obj_item_stack_tile_iron_cafeteria)), Dm.kv("tile_cost", 4)),
                    Dm.list(Dm.kv("name", "Grimy"), Dm.kv("type", typeof(obj_item_stack_tile_iron_grimy)), Dm.kv("tile_cost", 5)),
                    Dm.list(Dm.kv("name", "Sepia"), Dm.kv("type", typeof(obj_item_stack_tile_iron_sepia)), Dm.kv("tile_cost", 5)),
                    Dm.list(Dm.kv("name", "Herringbone"), Dm.kv("type", typeof(obj_item_stack_tile_iron_kitchen_herringbone)), Dm.kv("tile_cost", 5)))),
                Dm.kv("Pattern", Dm.list(
                    Dm.list(Dm.kv("name", "Terracotta"), Dm.kv("type", typeof(obj_item_stack_tile_iron_terracotta)), Dm.kv("tile_cost", 5)),
                    Dm.list(Dm.kv("name", "Small"), Dm.kv("type", typeof(obj_item_stack_tile_iron_terracotta_small)), Dm.kv("tile_cost", 5)),
                    Dm.list(Dm.kv("name", "Diagonal"), Dm.kv("type", typeof(obj_item_stack_tile_iron_terracotta_diagonal)), Dm.kv("tile_cost", 5)),
                    Dm.list(Dm.kv("name", "Herrigone"), Dm.kv("type", typeof(obj_item_stack_tile_iron_terracotta_herringbone)), Dm.kv("tile_cost", 5)),
                    Dm.list(Dm.kv("name", "Checkered"), Dm.kv("type", typeof(obj_item_stack_tile_iron_checker)), Dm.kv("tile_cost", 5)),
                    Dm.list(Dm.kv("name", "Herringbone"), Dm.kv("type", typeof(obj_item_stack_tile_iron_herringbone)), Dm.kv("tile_cost", 5)))))));
    }

    /// Lazy-initialize the datum field on all the designs
    // tg: code/_globalvars/lists/rtd.dm:116
    public static void populate_rtd_datums()
    {
        foreach (object main_root in Dm.each(GLOB.floor_designs))
        {
            foreach (object sub_category in Dm.each(GLOB.floor_designs[main_root]))
            {
                foreach (DmList design in Dm.each_cast<DmList>(((DmList)GLOB.floor_designs[main_root])[sub_category]))
                {
                    datum_tile_info tile_data = Dm.@new<datum_tile_info>(design);
                    design["datum"] = tile_data;
                }
            }
        }
    }
}
