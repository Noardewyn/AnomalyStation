// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/silicon/robot/death.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (silicons SI2, D326).

// tg: code/modules/mob/living/silicon/robot/death.dm:2
public partial class mob_living_silicon_robot : mob_living_silicon
{
    // tg: code/modules/mob/living/silicon/robot/death.dm:2
    public override void gib_animation()
    {
        Dm.@new<obj_effect_temp_visual_gib_animation>(loc, "gibbed-r");
    }

    // tg: code/modules/mob/living/silicon/robot/death.dm:5
    public override async DmTask dust(object just_ash = null, object drop_items = null, object give_moodlet = null, object force = null)
    {
        // You do not get MMI'd if you are dusted
        QDEL_NULL(ref mmi);
        await base.dust(just_ash, drop_items, give_moodlet, force);
    }

    // tg: code/modules/mob/living/silicon/robot/death.dm:10
    public override async DmTask<object> death(object gibbed = null, params object[] __extra)
    {
        if (stat == DEAD)
            return null;
        if (Dm.truthy(gibbed))
            await dump_into_mmi();
        else
        {
            await logevent("FATAL -- SYSTEM HALT");
            modularInterface.shutdown_computer();
            eye_flash_timer = _addtimer(Dm.@new<datum_callback>(this, nameof(flash_headlamp)), 2 * SECONDS, Dm.bitor(TIMER_STOPPABLE, TIMER_LOOP), file: "code/modules/mob/living/silicon/robot/death.dm", line: 17);
        }
        object dot = await base.death(gibbed, __extra);

        locked = FALSE; //unlock cover

        if (!QDELETED(builtInCamera) && Dm.truthy(builtInCamera.camera_enabled))
            builtInCamera.toggle_cam(this, 0);

        await toggle_headlamp(TRUE); //So borg lights are disabled when killed.
        await drop_all_held_items(); // particularly to ensure sight modes are cleared
        await update_icons();
        unbuckle_all_mobs(TRUE);
        // SSblackbox.ReportDeath(src): the round statistics (SSblackbox is an inert skeleton, M10; as the human's death.dm:33)
        return dot;
    }

    // tg: code/modules/mob/living/silicon/robot/death.dm:32
    public virtual async DmTask flash_headlamp()
    {
        if (eye_lights != null)
        {
            eye_lights = null;
            await regenerate_icons();
            return;
        }

        eye_lights = Dm.@new<mutable_appearance>();
        eye_lights.icon_state = Dm.text("[]_e_r", Dm.truthy(model.special_light_key) ? Dm.text("[]", model.special_light_key) : Dm.text("[]", model.cyborg_base_icon));
        eye_lights.color = COLOR_WHITE;
        SET_PLANE_EXPLICIT(eye_lights, ABOVE_GAME_PLANE, this);
        eye_lights.icon = icon;
        await regenerate_icons();
        add_overlay(eye_lights);
    }
}
