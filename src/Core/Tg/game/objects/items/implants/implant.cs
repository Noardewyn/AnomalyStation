// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/implants/implant.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M8 slice F, D186): the console procs are here as tg has them; handle_management_console_action, which the
// prisoner management console alone calls, came with the console (gaps LC1). The implant pad is not ported
// (docs/ai/specs/not-translated.md section 55).

/**
 * Parent class for all implants
 */
// tg: code/game/objects/items/implants/implant.dm:4
public partial class obj_item_implant : obj_item
{
    // tg: code/game/objects/items/implants/implant.dm:33
    public virtual async DmTask<object> activate(params object[] __extra)
    {
        if (Dm.truthy(this._listen_lookup?[COMSIG_IMPLANT_ACTIVATED]))
            this._SendSignal(COMSIG_IMPLANT_ACTIVATED, Dm.list(this));
        return null;
    }

    // tg: code/game/objects/items/implants/implant.dm:36
    public override async DmTask<object> ui_action_click(mob user = null, object actiontype = null)
    {
        Waiting.Detach(activate("action_button"));
        return null;
    }

    // tg: code/game/objects/items/implants/implant.dm:39
    public override object item_action_slot_check(object slot = null, mob user = null, datum_action action = null)
    {
        return user == imp_in;
    }

    // tg: code/game/objects/items/implants/implant.dm:42
    public virtual object can_be_implanted_in(mob_living target = null)
    {
        if (issilicon(target))
            return FALSE;

        if (isslime(target))
            return TRUE;

        if (!isanimal_or_basicmob(target))
            return TRUE;

        return !Dm.truthy(Dm.bitand(target.mob_biotypes, Dm.bitor(Dm.bitor(MOB_ROBOTIC, MOB_MINERAL), MOB_SPIRIT)));
    }

    /**
     * What does the implant do upon injection?
     *
     * return true if the implant injects
     * return false if there is no room for implant / it fails
     * Arguments:
     * * mob/living/target - mob being implanted
     * * mob/user - mob doing the implanting
     * * silent - unused here
     * * force - if true, implantation will not fail if can_be_implanted_in returns false
     */
    // tg: code/game/objects/items/implants/implant.dm:63
    [DmParams("target", "user", "silent", "force")]
    public virtual async DmTask<object> implant(mob_living target = null, mob user = null, float? silent = null, float? force = null, params object[] __extra)
    {
        silent ??= FALSE;
        force ??= FALSE;
        DmList args = Dm.args(target, user, silent, force, __extra);
        if (Dm.truthy(Dm.bitand((!Dm.truthy(this._listen_lookup?[COMSIG_IMPLANT_IMPLANTING]) ? NONE : this._SendSignal(COMSIG_IMPLANT_IMPLANTING, Dm.list(this, args))), COMPONENT_STOP_IMPLANTING)))
            return null;
        LAZYINITLIST(ref target.implants);
        if (!Dm.truthy(force) && !Dm.truthy(can_be_implanted_in(target)))
            return FALSE;

        float security_implants = 0; //Used to track how many implants with the "security" flag are in the user.
        foreach (obj_item_implant other_implant in Dm.each_cast<obj_item_implant>(target.implants))
        {
            object flags = !Dm.truthy(other_implant._listen_lookup?[COMSIG_IMPLANT_OTHER]) ? NONE : other_implant._SendSignal(COMSIG_IMPLANT_OTHER, Dm.list(other_implant, args, this));
            if (Dm.truthy(Dm.bitand(flags, COMPONENT_STOP_IMPLANTING)))
            {
                UNSETEMPTY(ref target.implants);
                return FALSE;
            }
            if (!Dm.truthy(force) && Dm.truthy(Dm.bitand(other_implant.implant_flags, IMPLANT_TYPE_SECURITY)))
            {
                security_implants++;
                if (security_implants >= SECURITY_IMPLANT_CAP) //We've found too many security implants in this mob, and will reject implantation by normal means
                {
                    balloon_alert(user, I18n.T("too many security implants!"));
                    return FALSE;
                }
            }
            if (Dm.truthy(Dm.bitand(flags, COMPONENT_DELETE_NEW_IMPLANT)))
            {
                UNSETEMPTY(ref target.implants);
                qdel(this);
                return TRUE;
            }
            if (Dm.truthy(Dm.bitand(flags, COMPONENT_DELETE_OLD_IMPLANT)))
            {
                qdel(other_implant);
                continue;
            }

            if (!istype(other_implant, type) || Dm.truthy(allow_multiple))
                continue;

            if (other_implant.uses < Dm.initial(other_implant, (obj_item_implant p) => p.uses) * 2)
            {
                if (uses == -1)
                    other_implant.uses = -1;
                else
                    other_implant.uses = Dm.min(other_implant.uses + uses, Dm.initial(other_implant, (obj_item_implant p) => p.uses) * 2);
                qdel(this);
                return TRUE;
            }
            else
                return FALSE;
        }

        await forceMove(target);
        imp_in = target;
        Dm.append(ref target.implants, this);
        foreach (datum_action implant_action in Dm.each_cast<datum_action>(actions))
            await implant_action.Grant(target);
        if (ishuman(target))
        {
            mob_living_carbon_human target_human = (mob_living_carbon_human)target;
            target_human.sec_hud_set_implants();
        }

        if (Dm.truthy(user))
            log_combat(user, target, "implanted", Dm.text("\a []", name));

        if (Dm.truthy(this._listen_lookup?[COMSIG_IMPLANT_IMPLANTED]))
            this._SendSignal(COMSIG_IMPLANT_IMPLANTED, Dm.list(this, target, user, silent, force));
        Dm.append(ref GLOB.tracked_implants, this);
        return TRUE;
    }

    /**
     * Remove implant from mob.
     *
     * This removes the effects of the implant and moves it out of the mob and into nullspace.
     * Arguments:
     * * mob/living/source - What the implant is being removed from
     * * silent - unused here
     * * special - Set to true if removed by admin panel, should bypass any side effects
     */
    // tg: code/game/objects/items/implants/implant.dm:121
    public virtual async DmTask<object> removed(mob_living source = null, float? silent = null, float? special = null)
    {
        silent ??= FALSE;
        special ??= FALSE;
        await moveToNullspace();
        imp_in = null;
        Dm.remove(ref source.implants, this);
        foreach (datum_action implant_action in Dm.each_cast<datum_action>(actions))
            await implant_action.Remove(source);
        if (ishuman(source))
        {
            mob_living_carbon_human human_source = (mob_living_carbon_human)source;
            human_source.sec_hud_set_implants();
        }

        if (Dm.truthy(this._listen_lookup?[COMSIG_IMPLANT_REMOVED]))
            this._SendSignal(COMSIG_IMPLANT_REMOVED, Dm.list(this, source, silent, special));
        Dm.remove(ref GLOB.tracked_implants, this);
        return TRUE;
    }

    // tg: code/game/objects/items/implants/implant.dm:136
    public override object Destroy(float? force = null)
    {
        if (Dm.truthy(imp_in))
            Waiting.NoSleep(removed(imp_in, silent: TRUE, special: TRUE));
        return base.Destroy(force);
    }

    /**
     * Gets the implant's info, for the implant pad.
     */
    // tg: code/game/objects/items/implants/implant.dm:144
    public virtual object get_data()
    {
        return implant_info;
    }

    /**
     * Gets the implant's lore info, also for the implant pad.
     */
    // tg: code/game/objects/items/implants/implant.dm:150
    public virtual object get_lore()
    {
        return implant_lore;
    }

    // tg: code/game/objects/items/implants/implant.dm:153
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = TRUE;
        await base.dropped(user, silent, __extra);
        return dot;
    }

    /// Determines if the implant is visible on the implant management console.
    /// Note that this would only ever be called on implants currently inserted into a mob.
    // tg: code/game/objects/items/implants/implant.dm:159
    public virtual object is_shown_on_console(obj_machinery_computer_prisoner_management console = null)
    {
        return FALSE;
    }

    /**
     * Returns a list of information to show on the implant management console for this implant
     *
     * Unlike normal UI data, the keys of the list are shown on the UI itself, so they should be human readable.
     */
    // tg: code/game/objects/items/implants/implant.dm:167
    public virtual object get_management_console_data()
    {
        DmList info_shown = Dm.list();
        info_shown["ID"] = imp_in.name;
        return info_shown;
    }

    /**
     * Returns a list of "structs" that translate into buttons displayed on the implant management console
     *
     * The struct should have the following keys:
     * * name - the name of the button, optional if button_icon is set
     * * icon - the icon of the button, optional if button_name is set
     * * color - the color of the button, optional
     * * tooltip - the tooltip of the button, optional
     * * action_key - the key that will be passed to handle_management_console_action when the button is clicked
     * * action_params - optional, additional params passed when the button is clicked
     */
    // tg: code/game/objects/items/implants/implant.dm:187
    [ShouldCallParent]
    public virtual object get_management_console_buttons()
    {
        DmList buttons = Dm.list();
        // UNTYPED_LIST_ADD(buttons, list(...)): the button is one element (gaps LC1: was merged key by key)
        Dm.append(buttons, Dm.list(Dm.list(
            Dm.kv("name", "Self Destruct"),
            Dm.kv("color", "bad"),
            Dm.kv("tooltip", "Destoys the implant from within the user harmlessly."),
            Dm.kv("action_key", "self_destruct"))));
        return buttons;
    }

    /**
     * Handles a button click on the implant management console
     *
     * * user - the mob clicking the button
     * * params - the params passed to the button, as if this were a ui_act handler.
     * See params["implant_action"] for the action key passed to the button
     * (which should correspond to a button returned by get_management_console_buttons)
     * * console - the console the button was clicked on
     */
    // tg: code/game/objects/items/implants/implant.dm:208
    [ShouldCallParent]
    public virtual async DmTask<object> handle_management_console_action(mob user = null, DmList @params = null, obj_machinery_computer_prisoner_management console = null)
    {
        if (Dm.eq(@params["implant_action"], "self_destruct"))
        {
            object warning = await tgui_alert(user, I18n.T("Activation will harmlessly self-destruct this implant. Proceed?"), I18n.T("You sure?"), Dm.list("Yes", "No"));
            if (!Dm.eq(warning, "Yes") || QDELETED(this) || QDELETED(user) || QDELETED(console) || imp_in == null)
                return TRUE;
            if (!Dm.truthy(console.is_operational) || !Dm.truthy(await user.can_perform_action(console, NEED_DEXTERITY | ALLOW_SILICON_REACH)))
                return TRUE;

            to_chat(imp_in, span_hear(I18n.T("You feel a tiny jolt from inside of you as one of your implants fizzles out.")));
            do_sparks(number: 2, cardinal_only: FALSE, source: imp_in);
            await deconstruct();
            return TRUE;
        }
        return null;
    }
}
