// Translated from tgstation (snapshot 1d99bd7f): code/modules/power/apc/apc_contextual_tips.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7 slice 3 (the APC): the whole file.

// tg: code/modules/power/apc/apc_contextual_tips.dm:1
public partial class obj_machinery_power_apc : obj_machinery_power
{
    // tg: code/modules/power/apc/apc_contextual_tips.dm:1
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        object dot = null;
        dot = base.add_context(source, context, held_item, user);

        if (isAI(user) || iscyborg(user))
        {
            context[SCREENTIP_CONTEXT_LMB] = "Open UI";
            context[SCREENTIP_CONTEXT_RMB] = Dm.truthy(locked) ? "Unlock" : "Lock";
            context[SCREENTIP_CONTEXT_CTRL_LMB] = Dm.truthy(operating) ? "Disable power" : "Enable power";
            context[SCREENTIP_CONTEXT_SHIFT_LMB] = Dm.truthy(lighting) ? "Disable lights" : "Enable lights";
            context[SCREENTIP_CONTEXT_ALT_LMB] = Dm.truthy(equipment) ? "Disable equipment" : "Enable equipment";
            context[SCREENTIP_CONTEXT_CTRL_SHIFT_LMB] = Dm.truthy(environ) ? "Disable environment" : "Enable environment";
        }

        else if (held_item == null)
        {
            if (opened == APC_COVER_CLOSED)
                context[SCREENTIP_CONTEXT_RMB] = Dm.truthy(locked) ? "Unlock" : "Lock";
            else if (opened == APC_COVER_OPENED && cell != null)
                context[SCREENTIP_CONTEXT_LMB] = "Remove cell";
        }

        else if (Dm.eq(held_item.tool_behaviour, TOOL_CROWBAR))
        {
            if (opened == APC_COVER_CLOSED)
                context[SCREENTIP_CONTEXT_LMB] = "Open the cover";
            else if ((opened == APC_COVER_OPENED && has_electronics == APC_ELECTRONICS_SECURED) && !Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
                context[SCREENTIP_CONTEXT_LMB] = "Close and lock";
            else if (Dm.truthy(malfhack) || Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(BROKEN, EMAGGED))))
                context[SCREENTIP_CONTEXT_LMB] = "Remove damaged board";
            else
                context[SCREENTIP_CONTEXT_LMB] = "Remove board";
        }

        else if (Dm.eq(held_item.tool_behaviour, TOOL_SCREWDRIVER))
        {
            if (opened == APC_COVER_CLOSED)
                context[SCREENTIP_CONTEXT_LMB] = Dm.truthy(panel_open) ? "Unexpose wires" : "Expose wires";
            else if (cell != null && opened == APC_COVER_OPENED)
                context[SCREENTIP_CONTEXT_LMB] = "Remove cell";
            else if (has_electronics == APC_ELECTRONICS_INSTALLED)
                context[SCREENTIP_CONTEXT_LMB] = "Fasten the board";
            else if (has_electronics == APC_ELECTRONICS_SECURED)
                context[SCREENTIP_CONTEXT_LMB] = "Unfasten the board";
        }

        else if (Dm.eq(held_item.tool_behaviour, TOOL_WIRECUTTER))
        {
            if (terminal != null && opened == APC_COVER_OPENED)
                context[SCREENTIP_CONTEXT_LMB] = "Dismantle wire terminal";
        }

        else if (Dm.eq(held_item.tool_behaviour, TOOL_WELDER))
        {
            if (opened == APC_COVER_OPENED && !Dm.truthy(has_electronics))
                context[SCREENTIP_CONTEXT_LMB] = "Disassemble the APC";
        }

        else if (held_item is obj_item_stock_parts_power_store_battery && opened == APC_COVER_OPENED)
            context[SCREENTIP_CONTEXT_LMB] = "Insert Battery";

        else if (held_item is obj_item_stack_cable_coil && opened == APC_COVER_OPENED)
            context[SCREENTIP_CONTEXT_LMB] = "Create wire terminal";

        else if (held_item is obj_item_electronics_apc && opened == APC_COVER_OPENED)
            context[SCREENTIP_CONTEXT_LMB] = "Insert board";

        else if (held_item is obj_item_electroadaptive_pseudocircuit && opened == APC_COVER_OPENED)
        {
            if (!Dm.truthy(has_electronics))
                context[SCREENTIP_CONTEXT_LMB] = "Insert an APC board";
            else if (cell == null)
                context[SCREENTIP_CONTEXT_LMB] = "Insert a battery";
        }

        else if (held_item is obj_item_wallframe_apc)
            context[SCREENTIP_CONTEXT_LMB] = "Replace damaged frame";

        return CONTEXTUAL_SCREENTIP_SET;
    }
}
