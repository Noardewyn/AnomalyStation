// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/dead/observer/login.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (the ghosts, 2026-10-16, D151).

// tg: code/modules/mob/dead/observer/login.dm:1
public partial class mob_dead_observer : mob_dead
{
    // tg: code/modules/mob/dead/observer/login.dm:1
    public override async DmTask<object> Login()
    {
        object dot = null;
        dot = await base.Login();
        if (!Dm.truthy(dot) || !Dm.truthy(client))
            return FALSE;

        ghost_accs = client.prefs.read_preference(typeof(datum_preference_choiced_ghost_accessories)) as string;
        ghost_others = client.prefs.read_preference(typeof(datum_preference_choiced_ghost_others)) as string;
        object preferred_form = null;

        // tg: :10-12  if(client.prefs.unlock_content) the ghost_form and ghost_orbit preferences of a BYOND member: a client
        // without prefs has none (preferences, M9).

        await update_icon(ALL, preferred_form);
        updateghostimages();
        client.set_right_click_menu_mode(FALSE);
        lighting_cutoff = Dm.num_or_null(default_lighting_cutoff());
        await GhostNightVision.Ensure(this);
        update_sight();
        return dot;
    }
}
