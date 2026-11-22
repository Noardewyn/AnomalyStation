// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/changeling/powers/fleshmend.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps K1 (D327): the whole file: Fleshmend, which applies the fleshmend status effect (datums/status_effects/buffs.cs). The var
// block is generated (D111).

// tg: code/modules/antagonists/changeling/powers/fleshmend.dm:1
public partial class datum_action_changeling_fleshmend : datum_action_changeling
{
    //Starts healing you every second for 10 seconds.
    //Can be used whilst unconscious.
    // tg: code/modules/antagonists/changeling/powers/fleshmend.dm:13
    [DmParams("user")]
    public override async DmTask<object> sting_action(mob_living user = null, mob_living target = null)
    {
        if (Dm.truthy(user.has_status_effect(typeof(datum_status_effect_fleshmend))))
        {
            user.balloon_alert(user, I18n.T("already fleshmending!"));
            return null;
        }
        await base.sting_action(user, target);
        to_chat(user, span_notice(I18n.T("We begin to heal rapidly.")));
        await user.apply_status_effect(typeof(datum_status_effect_fleshmend));
        return TRUE;
    }

    //Check buffs.dm for the fleshmend status effect code
}
