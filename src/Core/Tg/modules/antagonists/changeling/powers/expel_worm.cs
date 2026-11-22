// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/changeling/powers/expel_worm.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps K1 (D327): the whole file: Expel Worm, the changeling host's action that forces a blood worm out of its body. The blood
// worms are not ported: no blood worm host exists, so IsAvailable never passes, and the worm's leave_host is a deferred leaf
// (not-translated.md section 187). The var block is generated (D111).

// tg: code/modules/antagonists/changeling/powers/expel_worm.dm:1
public partial class datum_action_changeling_expel_worm : datum_action
{
    // tg: code/modules/antagonists/changeling/powers/expel_worm.dm:10
    public override async DmTask<object> IsAvailable(float? feedback = null)
    {
        if (!Dm.truthy(IS_CHANGELING(owner)))
            return FALSE;
        if (!(owner is mob_living_blood_worm_host))
            return FALSE;
        if (!HAS_TRAIT(owner.loc, TRAIT_BLOOD_WORM_HOST))
            return FALSE;
        if (!Dm.truthy(Dm.locate<mob_living_basic_blood_worm>(owner.loc)))
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/antagonists/changeling/powers/expel_worm.dm:21
    public override async DmTask<object> Trigger(mob clicker = null, object trigger_flags = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.Trigger(clicker, trigger_flags, __extra);
        if (!Dm.truthy(dot))
            return dot;
        mob_living_basic_blood_worm invader = Dm.locate<mob_living_basic_blood_worm>(owner.loc);
        to_chat(owner, span_danger(I18n.T("You expel \\the [] from your body!", invader)));
        to_chat(invader, span_userdanger(I18n.T("You are forcefully expelled by the body of \\the []!", owner.loc)));
        Dm.Deferred("code/modules/antagonists/changeling/powers/expel_worm.dm:28", "Trigger: invader.leave_host(), the blood worm leaving its host (the blood worms are not ported)"); // hasta la vista, worm
        return TRUE;
    }
}
