// Translated from tgstation (snapshot 1d99bd7f): code/modules/pai/shell.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI6 (D326): the whole file.

public partial class mob_living_silicon_pai : mob_living_silicon
{
    // tg: code/modules/pai/shell.dm:1
    public override async DmTask<object> mob_try_pickup(mob_living user = null, float? instant = null)
    {
        if (!Dm.truthy(possible_chassis[chassis]))
        {
            to_chat(user, span_warning(I18n.T("[]'s current form isn't able to be carried!", this)));
            return FALSE;
        }
        return await base.mob_try_pickup(user, instant);
    }

    // tg: code/modules/pai/shell.dm:7
    public override async DmTask<object> start_pulling(atom_movable pulled_atom = null, object state = null, object force = null, float? supress_message = null)
    {
        await DmTask.CompletedTask;
        return FALSE;
    }

    // tg: code/modules/pai/shell.dm:10
    public override void update_resting()
    {
        base.update_resting();
        update_appearance(UPDATE_ICON_STATE);
        if (loc != card)
            visible_message(span_notice(I18n.T("[] []", this, Dm.truthy(resting) ? "lays down for a moment..." : "perks up from the ground.")));
    }

    // tg: code/modules/pai/shell.dm:16
    public override async DmTask<object> wabbajack(object what_to_randomize = null, float? change_flags = null)
    {
        if (Dm.length(possible_chassis) < 2)
            return FALSE;
        DmList others = possible_chassis.Copy();
        Dm.remove(ref others, chassis);
        object holochassis = pick(others);
        set_holochassis(holochassis);
        balloon_alert(this, Dm.text("[] composite engaged", holochassis));
        await DmTask.CompletedTask;
        return TRUE;
    }

    /**
     * Checks if we are allowed to interact with a radial menu
     *
     * @param {atom} anchor - The atom that is anchoring the menu.
     *
     * @returns {boolean} - TRUE if we are allowed to interact with the menu,
     * 	FALSE otherwise.
     */
    // tg: code/modules/pai/shell.dm:31
    public virtual object check_menu(atom anchor = null)
    {
        if (Dm.truthy(incapacitated))
            return FALSE;
        if (get_turf(this) != get_turf(anchor))
            return FALSE;
        if (!isturf(loc) && loc != card)
        {
            balloon_alert(this, "can't do that here");
            return FALSE;
        }
        return TRUE;
    }

    /**
     * Sets a new holochassis skin based on a pAI's choice.
     *
     * @returns {boolean} - True if the skin was successfully set.
     * 	FALSE otherwise.
     */
    // tg: code/modules/pai/shell.dm:47
    public virtual async DmTask<object> choose_chassis()
    {
        DmList skins = Dm.list();
        foreach (object holochassis_option in Dm.each(possible_chassis))
        {
            image item_image = image(icon: icon, icon_state: holochassis_option);
            skins[Dm.text("[]", holochassis_option)] = item_image;
        }
        sort_list(skins);
        atom anchor = get_atom_on_turf(this) as atom;
        object choice = await show_radial_menu(this, anchor, skins, custom_check: Dm.@new<datum_callback>(this, nameof(check_menu), anchor), radius: 40, require_near: TRUE);
        if (!Dm.truthy(choice))
            return FALSE;
        set_holochassis(choice);
        balloon_alert(this, Dm.text("[] composite engaged", choice));
        update_resting();
        return TRUE;
    }

    /**
     * Returns the pAI to card mode.
     *
     * @param {boolean} force - If TRUE, the pAI will be forced to card mode.
     *
     * @returns {boolean} - TRUE if the pAI was forced to card mode.
     * 	FALSE otherwise.
     */
    // tg: code/modules/pai/shell.dm:71
    public virtual async DmTask<object> fold_in(object force = null)
    {
        holochassis_ready = FALSE;
        if (!Dm.truthy(force))
            _addtimer(Dm.@new<datum_callback>(this, nameof(set_holochassis_ready)), HOLOCHASSIS_COOLDOWN, file: "code/modules/pai/shell.dm", line: 74);
        else
            _addtimer(Dm.@new<datum_callback>(this, nameof(set_holochassis_ready)), HOLOCHASSIS_OVERLOAD_COOLDOWN, file: "code/modules/pai/shell.dm", line: 76);
        await set_resting(FALSE, silent: TRUE, instant: TRUE);
        if (!Dm.truthy(holoform))
        {
            await fold_out(force);
            return FALSE;
        }
        visible_message(span_notice(I18n.T("[] dematerialises!", this)));
        await stop_pulling();
        if (ispickedupmob(loc))
        {
            var mob_head = (obj_item_mob_holder)loc;
            await mob_head.release(display_messages: FALSE);
        }
        if (client != null)
        {
            client.perspective = EYE_PERSPECTIVE;
            await client.set_eye(card);
        }
        if (isturf(loc))
            Dm.@new<obj_effect_temp_visual_guardian_phase_out>(loc);
        await forceMove(card);
        add_traits(Dm.list(TRAIT_IMMOBILIZED, TRAIT_HANDS_BLOCKED), PAI_FOLDED);
        ADD_TRAIT(this, TRAIT_UNDENSE, PAI_FOLDED);
        await set_light_on(FALSE);
        holoform = FALSE;
        await set_resting(resting);
        return TRUE;
    }

    /**
     * Engage holochassis form.
     *
     * @param {boolean} force - Force the form to engage.
     *
     * @returns {boolean} - TRUE if the form was successfully engaged.
     * 	FALSE otherwise.
     */
    // tg: code/modules/pai/shell.dm:108
    public virtual async DmTask<object> fold_out(object force = null)
    {
        if (holochassis_health < 0)
        {
            balloon_alert(this, "emitter repair incomplete");
            return FALSE;
        }
        if (!Dm.truthy(can_holo) && !Dm.truthy(force))
        {
            balloon_alert(this, "emitters are disabled");
            return FALSE;
        }
        if (Dm.truthy(holoform))
            return await fold_in(force);
        if (!Dm.truthy(holochassis_ready))
        {
            balloon_alert(this, "emitters recycling...");
            return FALSE;
        }
        holochassis_ready = FALSE;
        _addtimer(Dm.@new<datum_callback>(this, nameof(set_holochassis_ready)), HOLOCHASSIS_COOLDOWN, file: "code/modules/pai/shell.dm", line: 122);
        REMOVE_TRAIT(this, TRAIT_IMMOBILIZED, PAI_FOLDED);
        REMOVE_TRAIT(this, TRAIT_HANDS_BLOCKED, PAI_FOLDED);
        REMOVE_TRAIT(this, TRAIT_UNDENSE, PAI_FOLDED);
        await forceMove(get_turf(card));
        if (client != null)
        {
            client.perspective = EYE_PERSPECTIVE;
            await client.set_eye(this);
        }
        await set_light_on(FALSE);
        update_appearance(UPDATE_ICON_STATE);
        visible_message(span_boldnotice(I18n.T("[] appears in a flash of light!", this)));
        holoform = TRUE;
        return TRUE;
    }

    // tg: code/modules/pai/shell.dm:74, :76 and :122  VARSET_CALLBACK(src, holochassis_ready, TRUE)
    public virtual void set_holochassis_ready() => holochassis_ready = TRUE;

    /**
     * Sets the holochassis skin and updates the icons
     *
     * @param {string} choice - The skin that will be used for the pAI holoform
     *
     * @returns {boolean} - TRUE if the skin was successfully set. FALSE otherwise.
     */
    // tg: code/modules/pai/shell.dm:142
    public virtual object set_holochassis(object choice = null)
    {
        if (!Dm.truthy(choice))
            return FALSE;
        chassis = Dm.str(choice);
        update_appearance(Dm.bitor(UPDATE_DESC, UPDATE_ICON_STATE));
        return TRUE;
    }

    /**
     * Toggles the onboard light
     *
     * @returns {boolean} - TRUE if the light was toggled.
     */
    // tg: code/modules/pai/shell.dm:153
    public virtual object toggle_integrated_light()
    {
        Waiting.NoSleep(set_light_on(!Dm.truthy(light_on) ? TRUE : FALSE));
        return TRUE;
    }
}
