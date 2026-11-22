// Translated from tgstation (snapshot 1d99bd7f): code/modules/plumbing/plumbers/iv_drip.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Engineering E15 (plumbing): the whole file but add_context (live-partial, screentips). The var block is generated (D111).

///modified IV that can be anchored and takes plumbing in- and output
// tg: code/modules/plumbing/plumbers/iv_drip.dm:2
public partial class obj_machinery_iv_drip_plumbing : obj_machinery_iv_drip
{
    // tg: code/modules/plumbing/plumbers/iv_drip.dm:12
    [DmParams("mapload", "layer")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object layer = Dm.arg(__extra, 0);
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(mapload))
            begin_processing();
        Waiting.NoSleep(AddComponent(typeof(datum_component_plumbing_automated_iv), layer));
        Waiting.NoSleep(AddElement(typeof(datum_element_simple_rotation)));
        return dot;
    }

    // tg: code/modules/plumbing/plumbers/iv_drip.dm:19
    public override object quick_toggle(mob user = null)
    {
        return FALSE;
    }

    // tg: code/modules/plumbing/plumbers/iv_drip.dm:22
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        return NONE;
    }

    // tg: code/modules/plumbing/plumbers/iv_drip.dm:25  add_context: live-partial (screentips).

    // tg: code/modules/plumbing/plumbers/iv_drip.dm:32
    public override async DmTask<object> plunger_act(obj_item_plunger attacking_plunger = null, mob_living user = null, object reinforced = null)
    {
        user.balloon_alert_to_viewers("furiously plunging...", "plunging iv drip...");
        if (!Dm.truthy(await do_after(user, 3 * SECONDS, target: this)))
            return TRUE;
        user.balloon_alert_to_viewers("finished plunging");
        datum_reagents holder = (datum_reagents)reagents;
        await holder.expose(get_turf(this), TOUCH); //splash on the floor
        await holder.clear_reagents();
        return TRUE;
    }

    // tg: code/modules/plumbing/plumbers/iv_drip.dm:41
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.eq(await default_unfasten_wrench(user, tool), SUCCESSFUL_UNFASTEN))
            return ITEM_INTERACT_BLOCKING;
        if (Dm.truthy(anchored))
            begin_processing();
        else
            end_processing();
        return ITEM_INTERACT_SUCCESS;
    }
}
