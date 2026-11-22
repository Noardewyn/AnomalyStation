// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/dead/observer/logout.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (the ghosts, 2026-10-16, D151).

// tg: code/modules/mob/dead/observer/logout.dm:1
public partial class mob_dead_observer : mob_dead
{
    // tg: code/modules/mob/dead/observer/logout.dm:1
    public override async DmTask<object> Logout()
    {
        if (Dm.truthy(client))
            Dm.remove(client.images, Dm.add(GLOB.ghost_images_default, GLOB.ghost_images_simple));

        if (Dm.truthy(observetarget) && ismob(observetarget))
            cleanup_observe();
        await base.Logout();
        // This is one of very few spawn()s left in our codebase
        // Yes it needs to be like this
        // No, you're not allowed to use spawn() yourself, unless you're making something that needs to work outside timers
        // Don't be a dumbass, I will always be watching
        spawn(0, async () =>
        {
            if (Dm.truthy(this) && !Dm.truthy(key)) //we've transferred to another mob. This ghost should be deleted.
                qdel(this);
            await DmTask.CompletedTask;
        });
        return null;
    }
}
