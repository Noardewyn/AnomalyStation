// Translated from tgstation (snapshot 1d99bd7f): code/modules/actionspeed/modifiers/wound.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M6 slice 2: an arm wound's slower interactions). The var block is generated (D111).

// tg: code/modules/actionspeed/modifiers/wound.dm:1
public partial class datum_actionspeed_modifier_wound_interaction_inefficiency : datum_actionspeed_modifier
{
    // tg: code/modules/actionspeed/modifiers/wound.dm:6
    [DmParams("new_id", "parent")]
    public override async DmTask New(params object[] __extra)
    {
        object new_id = Dm.arg(__extra, 0);
        datum_wound parent = Dm.arg(__extra, 1) as datum_wound;

        this.parent = parent;

        await base.New(Dm.forward(__extra, new_id, parent));
    }
}
