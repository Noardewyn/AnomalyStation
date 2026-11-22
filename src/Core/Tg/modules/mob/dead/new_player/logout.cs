// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/dead/new_player/logout.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

public partial class mob_dead_new_player : mob_dead
{
    // tg: code/modules/mob/dead/new_player/logout.dm:1
    public override async DmTask<object> Logout()
    {
        ready = PLAYER_NOT_READY;
        await base.Logout();
        if (!Dm.truthy(spawning)) //Here so that if they are spawning and log out, the other procs can play out and they will have a mob to come back to.
        {
            key = null; //We null their key before deleting the mob, so they are properly kicked out.
            QDEL_NULL(ref mind); //Clean out mind, yes this fucking sucks
            qdel(this);
        }
        return null;
    }
}
