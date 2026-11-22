// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/changeling/powers/spiders.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps K1 (D327): the whole file: Spread Infestation. Its bloody egg cluster (the flesh spider's ghost role) belongs to the spider
// ghost roles and nests, not ported (not-translated.md section 187): the cluster's creation is a deferred leaf. The var block is
// generated (D111).

// tg: code/modules/antagonists/changeling/powers/spiders.dm:1
public partial class datum_action_changeling_spiders : datum_action_changeling
{
    // Ensures that you cannot horrifically cheese the game by spawning spiders while in the vents
    // tg: code/modules/antagonists/changeling/powers/spiders.dm:12
    public override object can_be_used_by(mob_living user = null)
    {
        if (!isopenturf(user.loc))
        {
            turf user_turf = get_turf(user);
            user_turf.balloon_alert(user, I18n.T("not enough space!"));
            return FALSE;
        }
        return base.can_be_used_by(user);
    }

    //Makes a spider egg cluster. Allows you enable further general havok by introducing spiders to the station.
    // tg: code/modules/antagonists/changeling/powers/spiders.dm:20
    [DmParams("user")]
    public override async DmTask<object> sting_action(mob_living user = null, mob_living target = null)
    {
        await base.sting_action(user, target);
        Dm.Deferred("code/modules/antagonists/changeling/powers/spiders.dm:22", "sting_action: new /obj/effect/mob_spawn/ghost_role/spider/bloody(user.loc), the flesh spider's egg cluster (the spider ghost roles, egg clusters and flesh spiders are not ported)");
        return TRUE;
    }
}
