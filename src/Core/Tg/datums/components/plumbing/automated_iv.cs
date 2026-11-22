// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/plumbing/automated_iv.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Engineering E15 (plumbing): the whole file. The var block is generated (D111).

// tg: code/datums/components/plumbing/automated_iv.dm:1
public partial class datum_component_plumbing_automated_iv : datum_component_plumbing
{
    // tg: code/datums/components/plumbing/automated_iv.dm:8
    [DmParams("ducting_layer")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object dot = null;
        if (!istype(parent, typeof(obj_machinery_iv_drip_plumbing)))
            return COMPONENT_INCOMPATIBLE;
        dot = await base.Initialize(__extra);
        obj_machinery_iv_drip_plumbing drip = (obj_machinery_iv_drip_plumbing)parent;
        datum_reagents drip_reagents = (datum_reagents)drip.reagents;
        holder = Dm.@new<datum_reagents_plumbing>(drip_reagents.maximum_volume, drip_reagents.flags);
        holder.my_atom = drip;
        return dot;
    }

    // tg: code/datums/components/plumbing/automated_iv.dm:16
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref holder);
        return base.Destroy(force);
    }

    // tg: code/datums/components/plumbing/automated_iv.dm:20
    public override object can_give(object amount = null, object reagent = null, datum_ductnet net = null)
    {
        obj_machinery_iv_drip_plumbing drip = (obj_machinery_iv_drip_plumbing)parent;
        return Dm.truthy(base.can_give(amount, reagent, net)) && Dm.eq(drip.mode, IV_TAKING) ? TRUE : FALSE;
    }

    // tg: code/datums/components/plumbing/automated_iv.dm:24
    public override async DmTask send_request(object dir = null)
    {
        obj_machinery_iv_drip_plumbing drip = (obj_machinery_iv_drip_plumbing)parent;
        if (Dm.eq(drip.mode, IV_INJECTING))
            await base.send_request(dir);
    }

    // tg: code/datums/components/plumbing/automated_iv.dm:29
    public override async DmTask transfer_to(datum_component_plumbing target = null, object amount = null, object reagent = null, datum_ductnet net = null, float? round_robin = null)
    {
        round_robin ??= TRUE;
        await reagents.trans_to(holder, reagents.total_volume);
        reagents = holder;
        await base.transfer_to(target, amount, reagent, net, round_robin);
        obj_machinery_iv_drip_plumbing drip = (obj_machinery_iv_drip_plumbing)parent;
        reagents = (datum_reagents)drip.reagents;
    }
}
