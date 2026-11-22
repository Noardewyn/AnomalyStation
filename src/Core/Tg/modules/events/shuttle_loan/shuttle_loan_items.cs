// Translated from tgstation (snapshot 1d99bd7f): code/modules/events/shuttle_loan/shuttle_loan_items.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E3 (D327): the whole file (the papers are var-only): the ticking bomb of the VIP shuttle's loan. The var blocks are generated (D111).

// tg: code/modules/events/shuttle_loan/shuttle_loan_items.dm:5
public partial class obj_machinery_syndicatebomb_shuttle_loan : obj_machinery_syndicatebomb
{
    // tg: code/modules/events/shuttle_loan/shuttle_loan_items.dm:8
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(set_anchored(TRUE));
        timer_set = rand(480, 600); //once the supply shuttle docks (after 5 minutes travel time), players have between 3-5 minutes to defuse the bomb
        activate();
        update_appearance();
        return dot;
    }
}
