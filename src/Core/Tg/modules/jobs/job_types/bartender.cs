// Translated from tgstation (snapshot 1d99bd7f): code/modules/jobs/job_types/bartender.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps J2 (D298): the outfit's post_equip. award_service is a live-partial row (the medals).

// tg: code/modules/jobs/job_types/bartender.dm:49
public partial class datum_outfit_job_bartender : datum_outfit_job
{
    // tg: code/modules/jobs/job_types/bartender.dm:67
    public override async DmTask post_equip(mob_living_carbon_human user = null, float? visuals_only = null)
    {
        await base.post_equip(user, visuals_only);

        obj_item_card_id W = user.wear_id as obj_item_card_id;
        if (Dm.num(user.age) < AGE_MINOR)
        {
            W.registered_age = AGE_MINOR;
            to_chat(user, span_notice(I18n.T("You're not technically old enough to access or serve alcohol, but your ID has been discreetly modified to display your age as []. Try to keep that a secret!", AGE_MINOR)));
        }
    }
}
