// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/ventcrawling.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (gaps X5, D333: the xenomorphs crawl the vents and the alien infestation puts its larvae in one): the login notice, the
// entrance checks, the entrance and exit (with the vent indicator's arrows) and move_into_vent. update_pipe_vision (:132) lives in
// living.cs (its pipe images and plane masters stay a Dm.Deferred leaf: the 3D client draws no pipe vision, required-models.md).

// VENTCRAWLING

// tg: code/modules/mob/living/ventcrawling.dm:3
public partial class mob_living : mob
{
    // tg: code/modules/mob/living/ventcrawling.dm:3
    public virtual void notify_ventcrawler_on_login()
    {
        bool ventcrawler = HAS_TRAIT(this, TRAIT_VENTCRAWLER_ALWAYS) || HAS_TRAIT(this, TRAIT_VENTCRAWLER_NUDE);
        if (!ventcrawler)
            return;
        to_chat(this, span_notice(I18n.T("You can ventcrawl! Use alt+click on vents to quickly travel about the station.")));
    }

    /// Checks if the mob is able to enter the vent, and provides feedback if they are unable to.
    // tg: code/modules/mob/living/ventcrawling.dm:17
    public virtual async DmTask<object> can_enter_vent(obj_machinery_atmospherics_components ventcrawl_target = null, float? provide_feedback = null)
    {
        provide_feedback ??= TRUE;
        // Being able to always ventcrawl trumps being only able to ventcrawl when wearing nothing
        bool required_nudity = HAS_TRAIT(this, TRAIT_VENTCRAWLER_NUDE) && !HAS_TRAIT(this, TRAIT_VENTCRAWLER_ALWAYS);
        // Cache the vent_movement bitflag var from atmos machineries
        float vent_movement = ventcrawl_target.vent_movement;

        if (!Dm.truthy(await Adjacent(ventcrawl_target)))
            return null;
        if (!HAS_TRAIT(this, TRAIT_VENTCRAWLER_NUDE) && !HAS_TRAIT(this, TRAIT_VENTCRAWLER_ALWAYS))
            return null;
        if (IS_UNCONSCIOUS_OR_CRIT(this))
        {
            if (Dm.truthy(provide_feedback))
                to_chat(this, span_warning(I18n.T("You must be conscious to do this!")));
            return null;
        }
        if (HAS_TRAIT(this, TRAIT_IMMOBILIZED))
        {
            if (Dm.truthy(provide_feedback))
                to_chat(this, span_warning(I18n.T("You currently can't move into the vent!")));
            return null;
        }
        if (HAS_TRAIT(this, TRAIT_HANDS_BLOCKED))
        {
            if (Dm.truthy(provide_feedback))
                to_chat(this, span_warning(I18n.T("You need to be able to use your hands to ventcrawl!")));
            return null;
        }
        if (Dm.truthy(has_buckled_mobs()))
        {
            if (Dm.truthy(provide_feedback))
                to_chat(this, span_warning(I18n.T("You can't vent crawl with other creatures on you!")));
            return null;
        }
        if (buckled != null)
        {
            if (Dm.truthy(provide_feedback))
                to_chat(this, span_warning(I18n.T("You can't vent crawl while buckled!")));
            return null;
        }
        if (iscarbon(this) && required_nudity)
        {
            if (Dm.truthy(Dm.length(get_equipped_items(Dm.bitor(INCLUDE_POCKETS, INCLUDE_HELD)))))
            {
                if (Dm.truthy(provide_feedback))
                    to_chat(this, span_warning(I18n.T("You can't crawl around in the ventilation ducts with items!")));
                return null;
            }
        }
        if (Dm.truthy(ventcrawl_target.welded))
        {
            if (Dm.truthy(provide_feedback))
            {
                // Add cooldown to prevent welded vent message spam during movement
                if (welded_vent_message_cd <= world.time) // COOLDOWN_FINISHED (__DEFINES/cooldowns.dm:113), inline (rules 11.1 (a))
                {
                    to_chat(this, span_warning(I18n.T("You can't crawl around a welded vent!")));
                    welded_vent_message_cd = world.time + 2 * SECONDS; // COOLDOWN_START (__DEFINES/cooldowns.dm:111)
                }
            }
            return null;
        }

        if (!Dm.truthy(Dm.bitand(vent_movement, VENTCRAWL_ENTRANCE_ALLOWED)))
        {
            if (Dm.truthy(provide_feedback))
                to_chat(this, span_warning(I18n.T("You can't enter this vent!")));
            return null;
        }

        return TRUE;
    }

    /// Handles the entrance and exit on ventcrawling
    // tg: code/modules/mob/living/ventcrawling.dm:68
    public virtual async DmTask handle_ventcrawl(obj_machinery_atmospherics_components ventcrawl_target = null)
    {
        if (!Dm.truthy(await can_enter_vent(ventcrawl_target)))
            return;

        bool has_client = client != null; // clientless mobs can do this too! this is just stored in case the client disconnects while we sleep in do_after.

        //Handle the exit here
        if (HAS_TRAIT(this, TRAIT_MOVE_VENTCRAWLING) && loc is obj_machinery_atmospherics && Dm.truthy(Dm.bitand(movement_type, VENTCRAWLING)))
        {
            to_chat(this, span_notice(I18n.T("You begin climbing out from the ventilation system...")));
            if (has_client && client == null)
                return;
            if (!Dm.truthy(await do_after(this, 1 * SECONDS, target: ventcrawl_target)))
                return;
            if (Dm.truthy(ventcrawl_target.welded)) // in case it got welded during our sleep
            {
                to_chat(this, span_warning(I18n.T("You can't crawl around a welded vent!")));
                return;
            }
            visible_message(span_notice(I18n.T("[] scrambles out from the ventilation ducts!", this)), span_notice(I18n.T("You scramble out from the ventilation ducts.")));
            await forceMove(ventcrawl_target.loc);
            REMOVE_TRAIT(this, TRAIT_MOVE_VENTCRAWLING, VENTCRAWLING_TRAIT);
            update_pipe_vision();
        }

        //Entrance here
        else
        {
            datum_pipeline vent_parent = ventcrawl_target.parents[1] as datum_pipeline;
            if (vent_parent != null && (Dm.truthy(vent_parent.members.len) || Dm.truthy(vent_parent.other_atmos_machines)))
            {
                await ventcrawl_target.flick_overlay_static(Dm.image(Dm.resource("icons/effects/vent_indicator.dmi"), icon_state: "arrow", layer: ABOVE_MOB_LAYER, dir: get_dir(this.loc, ventcrawl_target.loc)), 2 * SECONDS);
                visible_message(span_notice(I18n.T("[] begins climbing into the ventilation system...", this)), span_notice(I18n.T("You begin climbing into the ventilation system...")));
                if (!Dm.truthy(await do_after(this, 2.5f * SECONDS, target: ventcrawl_target, extra_checks: Dm.@new<datum_callback>(this, nameof(can_enter_vent), ventcrawl_target))))
                    return;
                if (has_client && client == null)
                    return;
                if (Dm.truthy(ventcrawl_target.welded)) // in case it got welded during our sleep
                {
                    to_chat(this, span_warning(I18n.T("You can't crawl around a welded vent!")));
                    return;
                }
                await ventcrawl_target.flick_overlay_static(Dm.image(Dm.resource("icons/effects/vent_indicator.dmi"), icon_state: "insert", layer: ABOVE_MOB_LAYER), 1 * SECONDS);
                visible_message(span_notice(I18n.T("[] scrambles into the ventilation ducts!", this)), span_notice(I18n.T("You climb into the ventilation ducts.")));
                await move_into_vent(ventcrawl_target);
            }
            else
                to_chat(this, span_warning(I18n.T("This ventilation duct is not connected to anything!")));
        }
    }

    /**
     * Moves living mob directly into the vent as a ventcrawler
     *
     * Arguments:
     * * ventcrawl_target - The vent into which we are moving the mob
     */
    // tg: code/modules/mob/living/ventcrawling.dm:121
    public virtual async DmTask move_into_vent(obj_machinery_atmospherics_components ventcrawl_target = null)
    {
        await forceMove(ventcrawl_target);
        ADD_TRAIT(this, TRAIT_MOVE_VENTCRAWLING, VENTCRAWLING_TRAIT);
        update_pipe_vision();
    }
}

// tg: code/modules/mob/living/ventcrawling.dm:9
public partial class mob_living_carbon_human : mob_living_carbon
{
    // tg: code/modules/mob/living/ventcrawling.dm:9
    public override void notify_ventcrawler_on_login()
    {
        if (!HAS_TRAIT(this, TRAIT_LESSER_HUMANOID))
        {
            base.notify_ventcrawler_on_login();
            return;
        }
        if (!(head is obj_item_clothing_head_helmet_monkey_sentience)) //don't notify them about ventcrawling if they're wearing the sentience helmet, because they can't ventcrawl with it on, and if they take it off they'll no longer be in control of the mob.
        {
            base.notify_ventcrawler_on_login();
            return;
        }
    }
}

// tg: code/modules/mob/living/ventcrawling.dm:108
public partial class mob_living_basic_slime : mob_living_basic
{
    // tg: code/modules/mob/living/ventcrawling.dm:108
    public override async DmTask<object> can_enter_vent(obj_machinery_atmospherics_components ventcrawl_target = null, float? provide_feedback = null)
    {
        provide_feedback ??= TRUE;
        if (buckled != null)
        {
            if (Dm.truthy(provide_feedback))
                to_chat(this, span_warning(I18n.T("You can't vent crawl while feeding!")));
            return null;
        }
        return await base.can_enter_vent(ventcrawl_target, provide_feedback);
    }
}
