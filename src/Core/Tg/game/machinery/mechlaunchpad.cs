// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/mechlaunchpad.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the orbital mech pad (its id saved to a multitool with the panel open, the lifeform restriction wire cut and
// mended with wirecutters, its open state) and its launch: a see-through supply pod that lands on it, takes the mech up and drops
// it on the other pad, gibbing what stands there. The var blocks are generated (D111).

// tg: code/game/machinery/mechlaunchpad.dm:1
public partial class obj_machinery_mechpad : obj_machinery
{
    // tg: code/game/machinery/mechlaunchpad.dm:15
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        display_name = Dm.text("Orbital Pad - []", get_area_name(this));
        return dot;
    }

    // tg: code/game/machinery/mechlaunchpad.dm:19
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("Use a multitool with the panel open to save id to buffer.")));
        Dm.append(ref dot, span_notice(I18n.T("Use wirecutters with the panel open to [] the lifeform restriction wire.", Dm.truthy(mech_only) ? "cut" : "mend")));
        return dot;
    }

    // tg: code/game/machinery/mechlaunchpad.dm:24
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.str(Dm.truthy(panel_open) ? Dm.text("[]-open", base_icon_state) : base_icon_state);
        return dot;
    }

    // tg: code/game/machinery/mechlaunchpad.dm:28
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        await DmTask.CompletedTask;
        return default_deconstruction_screwdriver(user, tool);
    }

    // tg: code/game/machinery/mechlaunchpad.dm:31
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        return await default_deconstruction_crowbar(user, tool);
    }

    // tg: code/game/machinery/mechlaunchpad.dm:34
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        await DmTask.CompletedTask;
        obj_item_multitool multitool = tool as obj_item_multitool;
        if (!Dm.truthy(panel_open))
            return NONE;

        multitool.set_buffer(this);
        balloon_alert(user, I18n.T("saved to multitool buffer"));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/mechlaunchpad.dm:42
    public override async DmTask<object> wirecutter_act(mob_living user = null, obj_item tool = null)
    {
        await DmTask.CompletedTask;
        if (!Dm.truthy(panel_open))
            return NONE;
        mech_only = Dm.not(mech_only);
        to_chat(user, span_notice(I18n.T("You [] the lifeform restriction wire.", Dm.truthy(mech_only) ? "mend" : "cut")));
        return ITEM_INTERACT_SUCCESS;
    }

    /**
     * Spawns a special supply pod whitelisted to only accept mechs and have its drop off location be another mechpad
     * Arguments:
     * * where - where the supply pod will land after grabbing the mech
     */
    // tg: code/game/machinery/mechlaunchpad.dm:54
    public virtual async DmTask launch(obj_machinery_mechpad where = null)
    {
        turf reverse_turf = get_turf(where);
        await podspawn(Dm.list(
            Dm.kv("target", get_turf(this)),
            Dm.kv("path", typeof(obj_structure_closet_supplypod_mechpod)),
            Dm.kv("style", typeof(datum_pod_style_seethrough)),
            Dm.kv("reverse_dropoff_coords", Dm.list(reverse_turf.x, reverse_turf.y, reverse_turf.z))
        ));
        await use_energy(active_power_usage);
    }
}

// tg: code/game/machinery/mechlaunchpad.dm:64
public partial class obj_structure_closet_supplypod_mechpod : obj_structure_closet_supplypod
{
    // tg: code/game/machinery/mechlaunchpad.dm:78
    public override async DmTask handleReturnAfterDeparting(atom_movable holder = null)
    {
        holder ??= this;
        effectGib = TRUE;
        await base.handleReturnAfterDeparting(holder);
    }
}
