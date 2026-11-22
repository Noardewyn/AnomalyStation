// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/silicon/ai/death.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (silicons SI4, D326).

// tg: code/modules/mob/living/silicon/ai/death.dm:1
public partial class mob_living_silicon_ai : mob_living_silicon
{
    // tg: code/modules/mob/living/silicon/ai/death.dm:1
    [DmParams("gibbed", "drop_mmi")]
    public override async DmTask<object> death(object gibbed = null, params object[] __extra)
    {
        object drop_mmi = __extra.Length > 0 ? __extra[0] : TRUE;
        if (stat == DEAD)
            return null;

        if (!Dm.truthy(gibbed))
            Waiting.Detach(emote("dead")); // INVOKE_ASYNC

        if (deployed_shell != null)
            disconnect_shell();

        object dot = await base.death(gibbed, __extra);

        cut_overlays();

        string @base = Dm.str(display_icon_override ?? "ai");
        string dead_state = @base + "_dead";
        string screen_state;
        string lights_state = "lights_dead";

        if (Dm.truthy(icon_exists(icon, dead_state)))
            screen_state = dead_state;
        else
            screen_state = "ai_dead";

        if (!Dm.truthy(icon_exists(icon, lights_state)))
            lights_state = "lights_active";

        await set_light(0.2f, 0.2f, LIGHT_COLOR_FAINT_CYAN);

        if (Dm.truthy(icon_exists(icon, lights_state)))
        {
            mutable_appearance lights_overlay = mutable_appearance(icon, lights_state) as mutable_appearance;
            lights_overlay.layer = FLOAT_LAYER;
            lights_overlay.appearance_flags = Dm.bitor(RESET_COLOR, KEEP_APART);
            add_overlay(lights_overlay);
            // DEVIATION(DV12): add_overlay(emissive_appearance(icon, lights_state, src))
        }

        if (Dm.truthy(icon_exists(icon, screen_state)))
        {
            mutable_appearance screen_overlay = mutable_appearance(icon, screen_state) as mutable_appearance;
            screen_overlay.layer = FLOAT_LAYER + 0.1f;
            screen_overlay.appearance_flags = Dm.bitor(RESET_COLOR, KEEP_APART);
            add_overlay(screen_overlay);
            // DEVIATION(DV12): add_overlay(emissive_appearance(icon, screen_state, src))
        }

        if (Dm.truthy(is_anchored))
            flip_anchored();

        if (eyeobj != null)
        {
            eyeobj.setLoc(get_turf(this));
            set_eyeobj_visible(FALSE);
        }

        Dm.remove(ref GLOB.shuttle_caller_list, this);
        SSshuttle?.autoEvac(); // a host-built room without the Master has no SSshuttle (rules 10.6)

        ShutOffDoomsdayDevice();

        if (Dm.truthy(gibbed) && Dm.truthy(drop_mmi))
        {
            obj_item_brain_processor loose_cpu = make_mmi(get_turf(this));
            if (mind != null)
                await mind.transfer_to(loose_cpu.brainmob);
            loose_cpu.update_appearance();
        }

        if (Dm.truthy(explodes_on_death))
            _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.explosion), loc, 3, 6, 12, null, 15), 1 * SECONDS, file: "code/modules/mob/living/silicon/ai/death.dm", line: 66);

        // SSblackbox.ReportDeath(src): the round statistics (SSblackbox is an inert skeleton, M10; as the human's death.dm:33)
        return dot;
    }

    // tg: code/modules/mob/living/silicon/ai/death.dm:69
    public virtual void ShutOffDoomsdayDevice()
    {
        nuking = FALSE;
        QDEL_NULL(ref doomsday_device);
    }
}
