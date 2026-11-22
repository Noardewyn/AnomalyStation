// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/changeling/powers/digitalcamo.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps K1 (D327): the whole file: Digital Camouflage, the digitalcamo element toggled on and off. The var block is generated
// (D111).

// tg: code/modules/antagonists/changeling/powers/digitalcamo.dm:1
public partial class datum_action_changeling_digitalcamo : datum_action_changeling
{
    //Prevents AIs tracking you but makes you easily detectable to the human-eye.
    // tg: code/modules/antagonists/changeling/powers/digitalcamo.dm:11
    [DmParams("user")]
    public override async DmTask<object> sting_action(mob_living user = null, mob_living target = null)
    {
        await base.sting_action(user, target);
        if (Dm.truthy(active))
        {
            to_chat(user, span_notice(I18n.T("We return to normal.")));
            user.RemoveElement(typeof(datum_element_digitalcamo));
        }
        else
        {
            to_chat(user, span_notice(I18n.T("We distort our form to hide from the AI.")));
            await user.AddElement(typeof(datum_element_digitalcamo));
        }
        active = Dm.not(active);
        return TRUE;
    }

    // tg: code/modules/antagonists/changeling/powers/digitalcamo.dm:21
    public override async DmTask Remove(mob user = null)
    {
        user.RemoveElement(typeof(datum_element_digitalcamo));
        await base.Remove(user);
    }
}
