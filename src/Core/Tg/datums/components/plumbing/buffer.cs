// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/plumbing/buffer.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Engineering E15 (plumbing): the whole file. The var block is generated (D111).

// tg: code/datums/components/plumbing/buffer.dm:1
public partial class datum_component_plumbing_buffer : datum_component_plumbing
{
    // tg: code/datums/components/plumbing/buffer.dm:5
    [DmParams("ducting_layer")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        if (!istype(parent, typeof(obj_machinery_plumbing_buffer)))
            return COMPONENT_INCOMPATIBLE;
        return await base.Initialize(__extra);
    }

    // tg: code/datums/components/plumbing/buffer.dm:10
    public override async DmTask send_request(object dir = null)
    {
        obj_machinery_plumbing_buffer buffer = (obj_machinery_plumbing_buffer)parent;
        if (Dm.eq(buffer.mode, AB_UNREADY))
            await base.send_request(dir);
    }

    // tg: code/datums/components/plumbing/buffer.dm:15
    public override object can_give(object amount = null, object reagent = null, datum_ductnet net = null)
    {
        obj_machinery_plumbing_buffer buffer = (obj_machinery_plumbing_buffer)parent;
        return Dm.eq(buffer.mode, AB_READY) ? base.can_give(amount, reagent, net) : FALSE;
    }
}
