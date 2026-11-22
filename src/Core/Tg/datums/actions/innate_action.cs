// Translated from tgstation (snapshot 1d99bd7f): code/datums/actions/innate_action.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C4).

//Preset for general and toggled actions
// tg: code/datums/actions/innate_action.dm:2
public partial class datum_action_innate : datum_action
{
    // tg: code/datums/actions/innate_action.dm:15
    public override async DmTask<object> Trigger(mob clicker = null, object trigger_flags = null, params object[] __extra)
    {
        if (!Dm.truthy(await base.Trigger(clicker, trigger_flags, __extra)))
            return FALSE;

        if (Dm.truthy(click_action))
        {
            if (owner.click_intercept == this)
                unset_ranged_ability(owner as mob_living, disable_text);
            else
                set_ranged_ability(owner as mob_living, enable_text);
            await build_all_button_icons(UPDATE_BUTTON_STATUS);
            return TRUE;
        }
        else
        {
            float? active_status = active;
            if (Dm.truthy(active_status))
                await Deactivate();
            else
                await Activate();

            if (active != active_status)
                await build_all_button_icons(UPDATE_BUTTON_STATUS);
        }

        return TRUE;
    }

    // tg: code/datums/actions/innate_action.dm:40
    public override object is_action_active(atom_movable_screen_movable_action_button current_button = null)
    {
        if (Dm.truthy(click_action))
            return current_button.our_hud?.mymob?.click_intercept == this ? TRUE : FALSE;
        else
            return active;
    }

    // tg: code/datums/actions/innate_action.dm:46
    public virtual async DmTask<object> Activate()
    {
        return null;
    }

    // tg: code/datums/actions/innate_action.dm:49
    public virtual async DmTask<object> Deactivate()
    {
        return null;
    }

    /**
     * This is largely a copy paste of the cooldown action's: the ranged ability's cursor and click capture.
     */
    // tg: code/datums/actions/innate_action.dm:61
    public virtual void set_ranged_ability(mob_living on_who = null, object text_to_show = null)
    {
        if (ranged_mousepointer != null)
        {
            if (on_who.client != null)
                on_who.client.mouse_override_icon = ranged_mousepointer;
            on_who.update_mouse_pointer();
        }
        if (Dm.truthy(text_to_show))
            to_chat(on_who, text_to_show);
        on_who.click_intercept = this;
    }

    // tg: code/datums/actions/innate_action.dm:70
    public virtual void unset_ranged_ability(mob_living on_who = null, object text_to_show = null)
    {
        if (ranged_mousepointer != null)
        {
            if (on_who.client != null)
                on_who.client.mouse_override_icon = on_who.client.mouse_pointer_icon;
            on_who.update_mouse_pointer();
        }
        if (Dm.truthy(text_to_show))
            to_chat(on_who, text_to_show);
        on_who.click_intercept = null;
    }

    // tg: code/datums/actions/innate_action.dm:79
    public virtual async DmTask<object> InterceptClickOn(mob_living clicker = null, object @params = null, atom clicked_on = null)
    {
        if (!Dm.truthy(await IsAvailable(feedback: TRUE)))
        {
            unset_ranged_ability(clicker);
            return FALSE;
        }
        if (clicked_on == null)
            return FALSE;

        return await do_ability(clicker, clicked_on);
    }

    /// Actually goes through and does the click ability
    // tg: code/datums/actions/innate_action.dm:89
    public virtual async DmTask<object> do_ability(mob_living clicker = null, atom clicked_on = null)
    {
        return FALSE;
    }

    // tg: code/datums/actions/innate_action.dm:92
    public override async DmTask Remove(mob removed_from = null)
    {
        if (removed_from.click_intercept == this)
            unset_ranged_ability(removed_from as mob_living);
        await base.Remove(removed_from);
    }
}
