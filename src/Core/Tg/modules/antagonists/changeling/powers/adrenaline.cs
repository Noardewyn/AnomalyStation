// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/changeling/powers/adrenaline.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps K1 (D327): the whole file: Gene Stim, the changeling's adrenaline. The var block is generated (D111).

// tg: code/modules/antagonists/changeling/powers/adrenaline.dm:1
public partial class datum_action_changeling_adrenaline : datum_action_changeling
{
    //Recover from stuns.
    // tg: code/modules/antagonists/changeling/powers/adrenaline.dm:14
    [DmParams("user")]
    public override async DmTask<object> sting_action(mob_living user = null, mob_living target = null)
    {
        await base.sting_action(user, target);

        // Get us standing up.
        await user.SetAllImmobility(0);
        await user.set_stamina_loss(0);
        await user.set_resting(FALSE, instant: TRUE);

        await ((datum_reagents)user.reagents).add_reagent(typeof(datum_reagent_medicine_changelingadrenaline), 4); //Tank 5 consecutive baton hits

        to_chat(user, span_changeling(I18n.T("The staggering rush of a stimulant honed precisely to our biology is INVIGORATING. We will not be subdued.")));

        return TRUE;
    }
}
