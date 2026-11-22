// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/logout.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M6 slice 5 (gibbing: a gibbed player's brain, transfer_identity and the mind's transfer_to): the whole file. A mind whose
// player left its body (a ghost) is no longer active, so a transfer of that mind does not pull the player back.

// tg: code/modules/mob/living/logout.dm:1
public partial class mob_living : mob
{
    // tg: code/modules/mob/living/logout.dm:1
    public override async DmTask<object> Logout()
    {
        await update_z(null);
        object dot = await base.Logout();
        if (!Dm.truthy(key) && mind != null) //key and mind have become separated.
            mind.active = FALSE; //This is to stop say, a mind.transfer_to call on a corpse causing a ghost to re-enter its body.
        med_hud_set_status();
        return dot;
    }
}
