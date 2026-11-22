// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/login.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E2a (D327): the whole file. A living mob that logs in syncs its mind, refreshes its damage, health and medical HUD and registers on
// its level in SSmobs.clients_by_zlevel (until now a living mob registered only when it changed levels, so the messages sent to a
// level's players, the weather's alerts among them, reached no one who had not moved between levels); the ventcrawler's login notice since gaps X5. Not here:
// the field of view's client (tg's 2D cone, not drawn in first person).

// tg: code/modules/mob/living/login.dm:1
public partial class mob_living
{
    // tg: code/modules/mob/living/login.dm:1
    public override async DmTask<object> Login()
    {
        object dot = await base.Login();
        if (!Dm.truthy(dot) || client == null)
            return FALSE;

        //Mind updates
        await sync_mind();

        update_damage_hud();
        update_health_hud();

        turf T = get_turf(this);
        if (isturf(T))
            await update_z(T.z);

        //Vents
        notify_ventcrawler_on_login(); // gaps X5: ventcrawling

        med_hud_set_status();

        // tg: :21  update_fov_client(): tg's field of view cone (2D) is not drawn in the first-person client
        Dm.Deferred("code/modules/mob/living/login.dm:21", "Login: update_fov_client (the 2D field of view)");
        return dot;
    }
}
