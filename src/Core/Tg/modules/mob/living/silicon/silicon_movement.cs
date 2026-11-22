// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/silicon/silicon_movement.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI4 (D326): the whole file.

public partial class mob_living_silicon : mob_living
{
    //We only call a camera static update if we have successfully moved and the camera is present and working
    // tg: code/modules/mob/living/silicon/silicon_movement.dm:2
    public override async DmTask<object> Moved(atom old_loc = null, object movement_dir = null, object forced = null, DmList old_locs = null, float? momentum_change = null)
    {
        momentum_change ??= TRUE;
        object dot = await base.Moved(old_loc, movement_dir, forced, old_locs, momentum_change);
        if (!Dm.truthy(builtInCamera?.can_use()))
            return dot;
        // Delay's a bit faster then standard cameras to "avoid running out of the camera's fov" whatever that means
        SScameras.camera_moved(builtInCamera, get_turf(old_loc), get_turf(builtInCamera), 0.5f * SECONDS);
        return dot;
    }
}
