// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/syndie_spraycan.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate G3b): the seditious spraycan and its rune. The suicide's painted face (the face_decal component) is not
// live, so AddComponent does nothing (not-translated.md section 136). The var blocks are generated (D111).

// Extending the existing spraycan item was more trouble than it was worth, I don't want or need this to be able to draw arbitrary shapes.
// tg: code/game/objects/items/syndie_spraycan.dm:4
public partial class obj_item_traitor_spraycan : obj_item
{
    // tg: code/game/objects/items/syndie_spraycan.dm:1  #define SYNDIE_DRAW_TIME 3 SECONDS
    private const float SYNDIE_DRAW_TIME = 3 * SECONDS;

    // tg: code/game/objects/items/syndie_spraycan.dm:15  var/static/list/no_draw_turfs
    public DmList no_draw_turfs => TgState.Current.obj_item_traitor_spraycan__no_draw_turfs ??= typecacheof(Dm.list(typeof(turf_open_space), typeof(turf_open_openspace), typeof(turf_open_lava), typeof(turf_open_chasm))) as DmList;

    // tg: code/game/objects/items/syndie_spraycan.dm:22
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!Dm.truthy(check_allowed_items(interacting_with)) || !isliving(user))
            return NONE;

        if (Dm.truthy(expended))
        {
            user.balloon_alert(user, I18n.T("all out of paint..."));
            return ITEM_INTERACT_BLOCKING;
        }

        if (Dm.truthy(drawing_rune))
        {
            user.balloon_alert(user, I18n.T("already busy!"));
            return ITEM_INTERACT_BLOCKING;
        }

        if (isturf(interacting_with))
        {
            await try_draw_new_rune(user, (turf)interacting_with);
            return ITEM_INTERACT_SUCCESS;
        }

        if (interacting_with is obj_effect_decal_cleanable_traitor_rune rune)
        {
            await try_complete_rune(user, rune);
            return ITEM_INTERACT_SUCCESS;
        }

        return ITEM_INTERACT_BLOCKING;
    }

    /**
     * Attempt to draw a rune on [target_turf].
     * Shamelessly adapted from the heretic rune drawing process.
     *
     * Arguments
     * * user - the mob drawing the rune
     * * target_turf - the place the rune's being drawn
     */
    // tg: code/game/objects/items/syndie_spraycan.dm:51
    public virtual async DmTask try_draw_new_rune(mob_living user = null, turf target_turf = null)
    {
        foreach (turf nearby_turf in Dm.each_cast<turf>(RANGE_TURFS(1, target_turf)))
        {
            if (!isopenturf(nearby_turf) || Dm.truthy(is_type_in_typecache(nearby_turf, no_draw_turfs)))
            {
                user.balloon_alert(user, I18n.T("you need a clear 3x3 area!"));
                return;
            }
        }

        await draw_rune(user, target_turf);
    }

    /**
     * Draw your stage one rune on the ground and store it.
     *
     * Arguments
     * * user - the mob drawing the rune
     * * target_turf - the place the rune's being drawn
     */
    // tg: code/game/objects/items/syndie_spraycan.dm:66
    public virtual async DmTask draw_rune(mob_living user = null, turf target_turf = null)
    {
        if (!Dm.truthy(await try_draw_step(I18n.T("drawing outline..."), user, target_turf)))
            return;
        await try_complete_rune(user, await Dm.new_async<obj_effect_decal_cleanable_traitor_rune>(target_turf));
    }

    /**
     * Holder for repeated code to do something after a message and a set amount of time.
     *
     * Arguments
     * * output - a string to show when you start the process
     * * user - the mob drawing the rune
     * * target - what they're trying to draw, or the place they are trying to draw on
     */
    // tg: code/game/objects/items/syndie_spraycan.dm:79
    public virtual async DmTask<object> try_draw_step(object start_output = null, mob_living user = null, atom target = null)
    {
        drawing_rune = TRUE;
        user.balloon_alert(user, Dm.text("[]", start_output));
        float wait_time = SYNDIE_DRAW_TIME;

        if (HAS_TRAIT(user, TRAIT_TAGGER))
            wait_time *= 0.5f;

        if (!Dm.truthy(await do_after(user, wait_time, target, cog_icon: null, extra_checks: Dm.@new<datum_callback>(this, nameof(adjacency_check), user, target))))
        {
            user.balloon_alert(user, I18n.T("interrupted!"));
            drawing_rune = FALSE;
            return FALSE;
        }

        playsound(this, Dm.resource("sound/effects/spray.ogg"), 5, TRUE, 5);
        drawing_rune = FALSE;
        return TRUE;
    }

    /**
     * Try to upgrade a floor rune to its next stage.
     *
     * Arguments
     * * user - the mob drawing the rune
     * * target_turf - the place the rune's being drawn
     */
    // tg: code/game/objects/items/syndie_spraycan.dm:109
    public virtual async DmTask try_complete_rune(mob_living user = null, obj_effect_decal_cleanable_traitor_rune rune = null)
    {
        switch (rune.drawn_stage)
        {
            case obj_effect_decal_cleanable_traitor_rune.RUNE_STAGE_OUTLINE:
                if (!Dm.truthy(await try_draw_step(I18n.T("... finalising design..."), user, rune)))
                    return;
                if (QDELETED(rune))
                {
                    user.balloon_alert(user, I18n.T("graffiti was destroyed!"));
                    return;
                }
                rune.set_stage(obj_effect_decal_cleanable_traitor_rune.RUNE_STAGE_COLOURED);
                await try_complete_rune(user, rune);
                break;

            case obj_effect_decal_cleanable_traitor_rune.RUNE_STAGE_COLOURED:
                if (!Dm.truthy(await try_draw_step(I18n.T("... applying final coating..."), user, rune)))
                    return;
                if (QDELETED(rune))
                {
                    user.balloon_alert(user, I18n.T("graffiti was destroyed!"));
                    return;
                }
                user.balloon_alert(user, I18n.T("finished!"));
                rune.set_stage(obj_effect_decal_cleanable_traitor_rune.RUNE_STAGE_COMPLETE);
                expended = TRUE;
                desc = I18n.T("A suspicious looking spraycan, it's all out of paint.");
                if (Dm.truthy(this._listen_lookup?[COMSIG_TRAITOR_GRAFFITI_DRAWN]))
                    this._SendSignal(COMSIG_TRAITOR_GRAFFITI_DRAWN, Dm.list(this, rune));
                break;

            case obj_effect_decal_cleanable_traitor_rune.RUNE_STAGE_COMPLETE:
            case obj_effect_decal_cleanable_traitor_rune.RUNE_STAGE_REMOVABLE:
                user.balloon_alert(user, I18n.T("all done!"));
                break;
        }
    }

    /// Copying the functionality from normal spraycans, but doesn't need all the optional checks
    // tg: code/game/objects/items/syndie_spraycan.dm:139
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        if (Dm.truthy(expended))
        {
            user.visible_message(span_suicide(I18n.T("[] shakes up [] with a rattle and lifts it to [] mouth, but nothing happens!", user, this, user.p_their())));
            await user.say(I18n.T("MEDIOCRE!!"), forced: "spraycan suicide");
            return SHAME;
        }

        mob_living_carbon_human suicider = user as mob_living_carbon_human;
        user.visible_message(span_suicide(I18n.T("[] shakes up [] with a rattle and lifts it to [] mouth, spraying paint across [] teeth!", user, this, user.p_their(), user.p_their())));
        await user.say(I18n.T("WITNESS ME!!"), forced: "spraycan suicide");
        playsound(this, Dm.resource("sound/effects/spray.ogg"), 5, TRUE, 5);
        await suicider.AddComponent(typeof(datum_component_face_decal), "spray", Dm.list(Dm.kv(EXTERNAL_ADJACENT, BODY_ADJ_LAYER)), paint_color);
        return OXYLOSS;
    }

    ///Checks if the user is still adjacent to the target (used for do_after extra_checks)
    // tg: code/game/objects/items/syndie_spraycan.dm:155
    public virtual object adjacency_check(mob user = null, atom target = null)
    {
        if (!Dm.truthy(Waiting.NoSleep(user.Adjacent(target))))
        {
            user.balloon_alert(user, I18n.T("moved too far away!"));
            return FALSE;
        }
        return TRUE;
    }
}

// tg: code/game/objects/items/syndie_spraycan.dm:162
public partial class obj_effect_decal_cleanable_traitor_rune : obj_effect_decal_cleanable
{
    // tg: code/game/objects/items/syndie_spraycan.dm:100-103  #define RUNE_STAGE_OUTLINE 0 ... RUNE_STAGE_REMOVABLE 3
    public const float RUNE_STAGE_OUTLINE = 0;
    public const float RUNE_STAGE_COLOURED = 1;
    public const float RUNE_STAGE_COMPLETE = 2;
    public const float RUNE_STAGE_REMOVABLE = 3;

    // tg: code/game/objects/items/syndie_spraycan.dm:187
    public override object Destroy(float? force = null)
    {
        deltimer(protected_timer);
        QDEL_NULL(ref demoraliser);
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/syndie_spraycan.dm:192
    public override async DmTask<object> HasProximity(atom_movable proximity_check_mob = null)
    {
        if (isliving(proximity_check_mob) && Dm.num(get_dist(proximity_check_mob, this)) <= 1)
            await slip((mob_living)proximity_check_mob);
        return await base.HasProximity(proximity_check_mob);
    }

    /**
     * Makes someone fall over. If it's not the traitor, this counts as demoralising the crew.
     *
     * Arguments
     * * victim - whoever just slipped, point and laugh at them
     */
    // tg: code/game/objects/items/syndie_spraycan.dm:203
    public virtual async DmTask slip(mob_living victim = null)
    {
        if (Dm.truthy(Dm.bitand(victim.movement_type, MOVETYPES_NOT_TOUCHING_GROUND)))
            return;
        if (!Dm.truthy(await victim.slip(slip_time, this, slip_flags)))
            return;
        if (Dm.truthy(this._listen_lookup?[COMSIG_TRAITOR_GRAFFITI_SLIPPED]))
            this._SendSignal(COMSIG_TRAITOR_GRAFFITI_SLIPPED, Dm.list(this, victim.mind));
    }

    /**
     * Sets the "drawing stage" of the rune.
     * This affects the appearance, behaviour, and description of the effect.
     *
     * Arguments
     * * stage - new stage to apply
     */
    // tg: code/game/objects/items/syndie_spraycan.dm:217
    public virtual void set_stage(float stage)
    {
        drawn_stage = stage;
        switch (drawn_stage)
        {
            case RUNE_STAGE_OUTLINE:
                icon_state = "traitor_rune_outline";
                desc = I18n.T("It looks like it's going to be... the Syndicate logo?");
                break;

            case RUNE_STAGE_COLOURED:
            case RUNE_STAGE_REMOVABLE:
                icon_state = "traitor_rune_done";
                desc = I18n.T("A large depiction of the Syndicate logo.");
                clean_proof = FALSE;
                break;

            case RUNE_STAGE_COMPLETE:
                icon_state = "traitor_rune_sheen";
                desc = I18n.T("A large depiction of the Syndicate logo. It looks slippery.");
                datum_demoralise_moods_graffiti mood_category = Dm.@new<datum_demoralise_moods_graffiti>();
                demoraliser = Dm.@new<datum_proximity_monitor_advanced_demoraliser>(this, 7, TRUE, mood_category);
                clean_proof = TRUE;
                protected_timer = _addtimer(Dm.@new<datum_callback>(this, nameof(set_stage), RUNE_STAGE_REMOVABLE), 5 * MINUTES, file: "code/game/objects/items/syndie_spraycan.dm", line: 235);
                break;
        }
    }

    // tg: code/game/objects/items/syndie_spraycan.dm:237
    public override async DmTask<object> wash(object clean_types = null, params object[] __extra)
    {
        if (Dm.truthy(clean_proof))
            return NONE;

        return await base.wash(clean_types, __extra);
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/items/syndie_spraycan.dm:15  var/static/list/no_draw_turfs
    internal DmList obj_item_traitor_spraycan__no_draw_turfs;
}
