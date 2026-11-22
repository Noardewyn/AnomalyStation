// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/silicon/robot/login.dm, robot_movement.dm and robot_say.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole files (silicons SI2, D326).

// tg: code/modules/mob/living/silicon/robot/login.dm:2
public partial class mob_living_silicon_robot : mob_living_silicon
{
    // tg: code/modules/mob/living/silicon/robot/login.dm:2
    public override async DmTask<object> Login()
    {
        object dot = await base.Login();
        if (!Dm.truthy(dot) || client == null)
            return FALSE;
        await regenerate_icons();
        await show_laws(null); // show_laws(0): tg passes 0 for the unused show_to
        return dot;
    }

    // tg: code/modules/mob/living/silicon/robot/robot_movement.dm:1
    public override async DmTask<object> Process_Spacemove(float? movement_dir = null, float? continuous_move = null)
    {
        movement_dir ??= 0;
        continuous_move ??= FALSE;
        object dot = await base.Process_Spacemove(movement_dir, continuous_move);
        if (Dm.truthy(dot))
            return TRUE;
        if (Dm.truthy(await ionpulse__proc()))
            return TRUE;
        return FALSE;
    }

    // tg: code/modules/mob/living/silicon/robot/robot_say.dm:1
    public override async DmTask<object> try_speak(object message = null, float? ignore_spam = null, object forced = null, float? filterproof = null)
    {
        ignore_spam ??= FALSE;
        filterproof ??= FALSE;
        // Cyborgs cannot speak if silent borg is on.
        // Unless forced is set, as that's probably stating laws or something.
        if (!Dm.truthy(forced) && Dm.truthy(config.Get(typeof(datum_config_entry_flag_silent_borg))))
        {
            to_chat(this, span_danger(I18n.T("The ability for cyborgs to speak is currently disabled via server config.")));
            return FALSE;
        }

        return await base.try_speak(message, ignore_spam, forced, filterproof);
    }
}
