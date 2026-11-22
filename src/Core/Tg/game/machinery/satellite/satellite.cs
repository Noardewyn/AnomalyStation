// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/satellite/satellite.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps R1 (D327): the whole file: the satellite base (its network id, the on/off toggle that anchors it in space and sets it bobbing,
// its active and inactive icon states, the multitool readout) that the meteor shield satellites are. The var block is generated (D111).

// tg: code/game/machinery/satellite/satellite.dm:1
public partial class obj_machinery_satellite : obj_machinery
{
    /// global counter of IDs
    // tg: code/game/machinery/satellite/satellite.dm:13  var/static/global_id (a type static, rules 10.6)
    private static float global_id
    {
        get => TgState.Current.obj_machinery_satellite__global_id;
        set => TgState.Current.obj_machinery_satellite__global_id = value;
    }

    // tg: code/game/machinery/satellite/satellite.dm:17
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        id = global_id++;
        return dot;
    }

    // tg: code/game/machinery/satellite/satellite.dm:21
    public override object Destroy(float? force = null)
    {
        if (Dm.truthy(active))
            //should not be setting this directly but the satellite won't exist so we don't want the setter's effects
            active = FALSE;
        return base.Destroy(force);
    }

    // tg: code/game/machinery/satellite/satellite.dm:27
    public override async DmTask<object> interact(mob user = null, params object[] __extra)
    {
        await toggle(user);
        return null;
    }

    // tg: code/game/machinery/satellite/satellite.dm:30
    public override async DmTask<object> set_anchored(object anchorvalue = null)
    {
        object dot = await base.set_anchored(anchorvalue);
        if (dot == null)
            return dot; //no need to process if we didn't change anything.
        active = Dm.num(anchorvalue);
        if (Dm.truthy(anchorvalue))
        {
            begin_processing();
            animate(this, Dm.named("pixel_y", 2), Dm.named("time", 10), Dm.named("loop", -1));
        }
        else
        {
            end_processing();
            animate(this, Dm.named("pixel_y", 0), Dm.named("time", 10));
        }
        update_appearance();
        return dot;
    }

    // tg: code/game/machinery/satellite/satellite.dm:43
    public virtual async DmTask<object> toggle(mob user = null)
    {
        if (!Dm.truthy(active) && !Dm.truthy(isinspace()))
        {
            if (user != null)
                to_chat(user, span_warning(I18n.T("You can only activate [] in space.", this)));
            return FALSE;
        }
        if (user != null)
            to_chat(user, span_notice(I18n.T("You [] [].", Dm.truthy(active) ? I18n.T("deactivate") : I18n.T("activate"), this)));
        await set_anchored(Dm.truthy(anchored) ? FALSE : TRUE);
        return TRUE;
    }

    // tg: code/game/machinery/satellite/satellite.dm:53
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("[]_[]", base_icon_state, Dm.truthy(active) ? "active" : "inactive");
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/machinery/satellite/satellite.dm:57
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        await base.multitool_act(user, tool);
        to_chat(user, span_notice(I18n.T("// NTSAT-[] // Mode : [] //[]", id, Dm.truthy(active) ? "PRIMARY" : "STANDBY", Dm.truthy(Dm.bitand(obj_flags, EMAGGED)) ? "DEBUG_MODE //" : "")));
        return TRUE;
    }
}

public sealed partial class TgState
{
    // tg: code/game/machinery/satellite/satellite.dm:13  var/static/global_id = 0
    internal float obj_machinery_satellite__global_id;
}
