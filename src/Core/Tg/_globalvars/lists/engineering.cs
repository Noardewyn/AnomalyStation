// Translated from tgstation (snapshot 1d99bd7f): code/_globalvars/lists/engineering.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P1: all_mechanical_tools and all_surgical_tools (the cavity implant refuses a tool meant for the operation); gaps OD2: the
// file whole, all_tool_behaviours and tool_to_image (the debug omnitool's tools and radial menu).

public partial class datum_controller_global_vars
{
    // List of all tool behaviours.
    // tg: code/_globalvars/lists/engineering.dm:2  GLOBAL_LIST_INIT(all_tool_behaviours, list(...))
    public DmList all_tool_behaviours;

    // tg: code/_globalvars/lists/engineering.dm:2  (GLOBAL_MANAGED(all_tool_behaviours, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalall_tool_behaviours()
    {
        all_tool_behaviours = Dm.list(
            TOOL_ANALYZER,
            TOOL_BLOODFILTER,
            TOOL_BONESET,
            TOOL_CAUTERY,
            TOOL_CROWBAR,
            TOOL_DRILL,
            TOOL_HEMOSTAT,
            TOOL_KNIFE,
            TOOL_MINING,
            TOOL_MULTITOOL,
            TOOL_RETRACTOR,
            TOOL_ROLLINGPIN,
            TOOL_RUSTSCRAPER,
            TOOL_SAW,
            TOOL_SCALPEL,
            TOOL_SCREWDRIVER,
            TOOL_SHOVEL,
            TOOL_WELDER,
            TOOL_WIRECUTTER,
            TOOL_WRENCH);
        Dm.append(ref gvars_datum_init_order, "all_tool_behaviours");
    }

    // tg: code/_globalvars/lists/engineering.dm:25  GLOBAL_LIST_INIT(all_mechanical_tools, list(...))
    public DmList all_mechanical_tools;

    // tg: code/_globalvars/lists/engineering.dm:25  (GLOBAL_MANAGED(all_mechanical_tools, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalall_mechanical_tools()
    {
        all_mechanical_tools = Dm.list(
            TOOL_ANALYZER,
            TOOL_CROWBAR,
            TOOL_MULTITOOL,
            TOOL_SCREWDRIVER,
            TOOL_WELDER,
            TOOL_WIRECUTTER,
            TOOL_WRENCH);
        Dm.append(ref gvars_datum_init_order, "all_mechanical_tools");
    }

    // tg: code/_globalvars/lists/engineering.dm:35  GLOBAL_LIST_INIT(all_surgical_tools, list(...))
    public DmList all_surgical_tools;

    // tg: code/_globalvars/lists/engineering.dm:35  (GLOBAL_MANAGED(all_surgical_tools, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalall_surgical_tools()
    {
        all_surgical_tools = Dm.list(
            TOOL_BONESET,
            TOOL_CAUTERY,
            TOOL_HEMOSTAT,
            TOOL_RETRACTOR,
            TOOL_SAW,
            TOOL_SCALPEL);
        Dm.append(ref gvars_datum_init_order, "all_surgical_tools");
    }

    /// Mapping of tool types to icons that represent them
    // tg: code/_globalvars/lists/engineering.dm:45  GLOBAL_LIST_INIT(tool_to_image, list(...))
    public DmList tool_to_image;

    // tg: code/_globalvars/lists/engineering.dm:45  (GLOBAL_MANAGED(tool_to_image, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobaltool_to_image()
    {
        tool_to_image = Dm.list(
            Dm.kv(TOOL_CROWBAR, image(typeof(obj_item_crowbar))),
            Dm.kv(TOOL_MULTITOOL, image(typeof(obj_item_multitool))),
            Dm.kv(TOOL_SCREWDRIVER, image(typeof(obj_item_screwdriver))),
            Dm.kv(TOOL_WIRECUTTER, image(typeof(obj_item_wirecutters))),
            Dm.kv(TOOL_WRENCH, image(typeof(obj_item_wrench))),
            Dm.kv(TOOL_WELDER, image(typeof(obj_item_weldingtool_mini))),
            Dm.kv(TOOL_ANALYZER, image(typeof(obj_item_analyzer))),
            Dm.kv(TOOL_MINING, image(typeof(obj_item_pickaxe))),
            Dm.kv(TOOL_SHOVEL, image(typeof(obj_item_shovel))),
            Dm.kv(TOOL_RETRACTOR, image(typeof(obj_item_retractor))),
            Dm.kv(TOOL_HEMOSTAT, image(typeof(obj_item_hemostat))),
            Dm.kv(TOOL_CAUTERY, image(typeof(obj_item_cautery))),
            Dm.kv(TOOL_DRILL, image(typeof(obj_item_surgicaldrill))),
            Dm.kv(TOOL_SCALPEL, image(typeof(obj_item_scalpel))),
            Dm.kv(TOOL_SAW, image(typeof(obj_item_circular_saw))),
            Dm.kv(TOOL_BONESET, image(typeof(obj_item_bonesetter))),
            Dm.kv(TOOL_KNIFE, image(typeof(obj_item_knife_kitchen))),
            Dm.kv(TOOL_BLOODFILTER, image(typeof(obj_item_blood_filter))),
            Dm.kv(TOOL_ROLLINGPIN, image(typeof(obj_item_kitchen_rollingpin))),
            Dm.kv(TOOL_RUSTSCRAPER, image(typeof(obj_item_wirebrush))));
        Dm.append(ref gvars_datum_init_order, "tool_to_image");
    }
}
