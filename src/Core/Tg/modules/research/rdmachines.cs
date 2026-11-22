// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/rdmachines.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package RD0, D333: the research machines' base): the R&D device every fabricator, analyzer, experimentor and
// server derives from: its wires, the station techweb it links to at round start (or a multitool's buffered one), the panel, the
// deconstruction, and the loaded item's checks. The var block is generated (D111).

//All devices that link into the R&D console fall into thise type for easy identification and some shared procs.
// tg: code/modules/research/rdmachines.dm:4
public partial class obj_machinery_rnd : obj_machinery
{
    // tg: code/modules/research/rdmachines.dm:22
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        set_wires(Dm.@new<datum_wires_rnd>(this));
        register_context();
        return dot;
    }

    // tg: code/modules/research/rdmachines.dm:27
    public override async DmTask post_machine_initialize()
    {
        await base.post_machine_initialize();
        if (!Dm.truthy(config.Get(typeof(datum_config_entry_flag_no_default_techweb_link))) && stored_research == null) /* CONFIG_GET(flag/no_default_techweb_link) */
            CONNECT_TO_RND_SERVER_ROUNDSTART(ref stored_research, this);
        if (stored_research != null)
            await on_connected_techweb();
    }

    // tg: code/modules/research/rdmachines.dm:34
    public override object Destroy(float? force = null)
    {
        if (stored_research != null)
        {
            log_research(Dm.text("[] disconnected from techweb [] (destroyed).", this, stored_research));
            stored_research = null;
        }
        return base.Destroy(force);
    }

    // tg: code/modules/research/rdmachines.dm:40
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (!Dm.truthy(in_range(user, this)) && !isobserver(user))
            return dot;

        Dm.append(ref dot, span_notice(I18n.T("A [] with techweb designs can be uploaded here.", EXAMINE_HINT("multitool"))));
        Dm.append(ref dot, span_notice(I18n.T("Its maintenance panel can be [] [].", EXAMINE_HINT("screwed"), Dm.truthy(panel_open) ? "closed" : "open")));
        if (Dm.truthy(panel_open))
        {
            Dm.append(ref dot, span_notice(I18n.T("Use a [] or [] to interact with wires.", EXAMINE_HINT("multitool"), EXAMINE_HINT("wirecutters"))));
            Dm.append(ref dot, span_notice(I18n.T("The machine can be [] apart.", EXAMINE_HINT("pried"))));
        }
        return dot;
    }

    // tg: code/modules/research/rdmachines.dm:51
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        if (held_item == null)
            return NONE;

        if (Dm.eq(held_item.tool_behaviour, TOOL_SCREWDRIVER))
        {
            context[SCREENTIP_CONTEXT_LMB] = Dm.text("[] Panel", Dm.truthy(panel_open) ? "Close" : "Open");
            context[SCREENTIP_CONTEXT_RMB] = Dm.text("[] Panel", Dm.truthy(panel_open) ? "Close" : "Open");
            return CONTEXTUAL_SCREENTIP_SET;
        }

        if (Dm.truthy(panel_open))
        {
            string msg = null;
            if (Dm.eq(held_item.tool_behaviour, TOOL_CROWBAR))
                msg = "Deconstruct";
            else if (Dm.truthy(is_wire_tool(held_item)))
                msg = "Open Wires";

            if (msg != null)
            {
                context[SCREENTIP_CONTEXT_LMB] = msg;
                context[SCREENTIP_CONTEXT_RMB] = msg;
                return CONTEXTUAL_SCREENTIP_SET;
            }
        }
        else
        {
            if (Dm.eq(held_item.tool_behaviour, TOOL_MULTITOOL))
            {
                var tool = held_item.get_proxy_attacker_for(this, user) as obj_item_multitool;
                if (tool != null && !QDELETED(tool.buffer as datum) && tool.buffer is datum_techweb)
                {
                    context[SCREENTIP_CONTEXT_LMB] = "Upload Techweb";
                    context[SCREENTIP_CONTEXT_RMB] = "Upload Techweb";
                    return CONTEXTUAL_SCREENTIP_SET;
                }
            }
        }
        return NONE;
    }

    ///Called when attempting to connect the machine to a techweb, forgetting the old.
    // tg: code/modules/research/rdmachines.dm:81
    public virtual async DmTask connect_techweb(datum_techweb new_techweb = null)
    {
        if (stored_research != null)
            log_research(Dm.text("[] disconnected from techweb [] when connected to [].", this, stored_research, new_techweb));
        stored_research = new_techweb;
        if (stored_research != null)
            await on_connected_techweb();
    }

    ///Called post-connection to a new techweb.
    // tg: code/modules/research/rdmachines.dm:89
    public virtual DmTask on_connected_techweb() => DmTask.CompletedTask;

    ///Reset the state of this machine
    // tg: code/modules/research/rdmachines.dm:93
    public virtual void reset_busy()
    {
        busy = FALSE;
    }

    // tg: code/modules/research/rdmachines.dm:96
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        string @base = Dm.truthy(base_icon_state) ? Dm.str(base_icon_state) : Dm.initial(GetType(), (obj_machinery_rnd r) => r.icon_state);
        icon_state = Dm.truthy(panel_open) ? Dm.text("[]_t", @base) : @base;
        return dot;
    }

    // tg: code/modules/research/rdmachines.dm:100
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        return await default_deconstruction_crowbar(user, tool);
    }

    // tg: code/modules/research/rdmachines.dm:103
    public override async DmTask<object> crowbar_act_secondary(mob_living user = null, obj_item tool = null)
    {
        return await crowbar_act(user, tool);
    }

    // tg: code/modules/research/rdmachines.dm:106
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        await DmTask.CompletedTask;
        return default_deconstruction_screwdriver(user, tool);
    }

    // tg: code/modules/research/rdmachines.dm:109
    public override async DmTask<object> screwdriver_act_secondary(mob_living user = null, obj_item tool = null)
    {
        return await screwdriver_act(user, tool);
    }

    // tg: code/modules/research/rdmachines.dm:112
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(panel_open))
        {
            await wires.interact(user);
            return ITEM_INTERACT_SUCCESS;
        }
        var multitool = tool as obj_item_multitool;
        if (multitool != null && !QDELETED(multitool.buffer as datum) && multitool.buffer is datum_techweb techweb)
        {
            await connect_techweb(techweb);
            return ITEM_INTERACT_SUCCESS;
        }
        return null;
    }

    // tg: code/modules/research/rdmachines.dm:120
    public override async DmTask<object> multitool_act_secondary(mob_living user = null, obj_item tool = null)
    {
        return await multitool_act(user, tool);
    }

    // tg: code/modules/research/rdmachines.dm:123
    public override async DmTask<object> wirecutter_act(mob_living user = null, obj_item tool = null)
    {
        object dot = ITEM_INTERACT_BLOCKING;
        if (Dm.truthy(panel_open))
        {
            await wires.interact(user);
            return ITEM_INTERACT_SUCCESS;
        }
        return dot;
    }

    // tg: code/modules/research/rdmachines.dm:129
    public override async DmTask<object> wirecutter_act_secondary(mob_living user = null, obj_item tool = null)
    {
        return await wirecutter_act(user, tool);
    }

    //whether the machine can have an item inserted in its current state.
    // tg: code/modules/research/rdmachines.dm:133
    public virtual object is_insertion_ready(mob user = null)
    {
        if (Dm.truthy(panel_open))
        {
            balloon_alert(user, "panel open!");
            return FALSE;
        }
        if (Dm.truthy(disabled))
        {
            balloon_alert(user, "belts disabled!");
            return FALSE;
        }
        if (Dm.truthy(busy))
        {
            balloon_alert(user, "still busy!");
            return FALSE;
        }
        if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
        {
            balloon_alert(user, "machine broken!");
            return FALSE;
        }
        if (Dm.truthy(Dm.bitand(machine_stat, NOPOWER)))
        {
            balloon_alert(user, "no power!");
            return FALSE;
        }
        if (loaded_item != null)
        {
            balloon_alert(user, "item already loaded!");
            return FALSE;
        }
        return TRUE;
    }

    //we eject the loaded item when deconstructing the machine
    // tg: code/modules/research/rdmachines.dm:155
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        if (loaded_item != null)
            await loaded_item.forceMove(drop_location() as atom);
        return await base.on_deconstruction(disassembled);
    }
}
