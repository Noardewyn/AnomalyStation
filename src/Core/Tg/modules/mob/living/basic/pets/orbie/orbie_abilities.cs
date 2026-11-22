// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/pets/orbie/orbie_abilities.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps S4d, D327): Orbie's two abilities, its lights' toggle and its camera's photo (the camera's picture is the
// photography's). The var blocks are generated (D111).

// tg: code/modules/mob/living/basic/pets/orbie/orbie_abilities.dm:1
public partial class datum_action_cooldown_mob_cooldown_lights : datum_action_cooldown_mob_cooldown
{
    // tg: code/modules/mob/living/basic/pets/orbie/orbie_abilities.dm:9
    public override async DmTask<object> Activate(atom target = null)
    {
        await (owner as atom).set_light_on(!Dm.truthy((owner as atom).light_on) ? TRUE : FALSE);
        return TRUE;
    }
}

// tg: code/modules/mob/living/basic/pets/orbie/orbie_abilities.dm:14
public partial class datum_action_cooldown_mob_cooldown_capture_photo : datum_action_cooldown_mob_cooldown
{
    // tg: code/modules/mob/living/basic/pets/orbie/orbie_abilities.dm:24
    public override async DmTask<object> Grant(mob granted_to = null, params object[] __extra)
    {
        object dot = await base.Grant(granted_to, __extra);
        if (owner == null)
            return dot;
        internal_camera = Dm.@new<obj_item_camera>(owner);
        internal_camera.print_picture_on_snap = FALSE;
        internal_camera.cooldown = 1 * SECONDS;
        RegisterSignal(internal_camera, COMSIG_PREQDELETED, nameof(on_camera_delete));
        return dot;
    }

    // tg: code/modules/mob/living/basic/pets/orbie/orbie_abilities.dm:33
    public override async DmTask<object> Activate(atom target = null)
    {
        if (internal_camera == null)
            return FALSE;
        internal_camera.attempt_picture(target, owner as mob);
        StartCooldown();
        return TRUE;
    }

    // tg: code/modules/mob/living/basic/pets/orbie/orbie_abilities.dm:40
    [SignalHandler]
    public virtual void on_camera_delete(datum source = null)
    {
        UnregisterSignal(internal_camera, COMSIG_PREQDELETED);
        internal_camera = null;
    }

    // tg: code/modules/mob/living/basic/pets/orbie/orbie_abilities.dm:45
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref internal_camera);
        return base.Destroy(force);
    }
}
