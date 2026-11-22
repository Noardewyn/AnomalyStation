// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/changeling/powers/mmi_talk.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps K1 (D327): the whole file: the MMI Talk power the decoy brain's component grants while the changeling's brain sits in an
// MMI: the LingMMITalk window with its map view of the MMI's surroundings (kept current by a movement detector), the MMI made
// hearing sensitive to relay what it hears to the changeling in purple, and the speech said through the MMI's brain mob. The var
// block is generated (D111).

// tg: code/modules/antagonists/changeling/powers/mmi_talk.dm:1
public partial class datum_action_changeling_mmi_talk : datum_action_changeling
{
    // tg: code/modules/antagonists/changeling/powers/mmi_talk.dm:30
    public override object Destroy(float? force = null)
    {
        brain_ref = null;
        QDEL_NULL(ref mmi_view);
        QDEL_NULL(ref mmi_view_background);
        QDEL_NULL(ref update_view_tracker);
        return base.Destroy(force);
    }

    // tg: code/modules/antagonists/changeling/powers/mmi_talk.dm:37
    public override async DmTask Remove(mob remove_from = null)
    {
        await base.Remove(remove_from);
        SStgui.close_uis(this);
    }

    // tg: code/modules/antagonists/changeling/powers/mmi_talk.dm:41
    public override async DmTask<object> can_sting(mob_living user = null, mob_living target = null)
    {
        object dot = null;
        dot = await base.can_sting(user, target);
        if (!Dm.truthy(dot))
            return FALSE;
        // This generally shouldn't happen, but just in case
        if (brain_ref == null)
        {
            stack_trace(Dm.text("[] can_sting was called with a null brain!", type), "code/modules/antagonists/changeling/powers/mmi_talk.dm", 47);
            return FALSE;
        }
        if (brain_ref.loc is not obj_item_brain_processor_organic)
        {
            stack_trace(Dm.text("[] can_sting was called with a brain not located in an MMI!", type), "code/modules/antagonists/changeling/powers/mmi_talk.dm", 50);
            return FALSE;
        }
        return TRUE;
    }

    // tg: code/modules/antagonists/changeling/powers/mmi_talk.dm:54
    public override async DmTask<object> sting_action(mob_living user = null, mob_living target = null)
    {
        await base.sting_action(user, target);
        await ui_interact(user);
        return TRUE;
    }

    // tg: code/modules/antagonists/changeling/powers/mmi_talk.dm:59
    public override object ui_state(mob user = null)
    {
        return GLOB.always_state;
    }

    // tg: code/modules/antagonists/changeling/powers/mmi_talk.dm:62
    public override async DmTask<object> ui_status(mob user = null, datum_ui_state state = null)
    {
        if (user != owner)
            return UI_CLOSE;
        return await base.ui_status(user, state);
    }

    // tg: code/modules/antagonists/changeling/powers/mmi_talk.dm:67
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        await DmTask.CompletedTask;
        DmList data = Dm.list();
        data["mmi_view"] = mmi_view_key;
        return data;
    }

    // tg: code/modules/antagonists/changeling/powers/mmi_talk.dm:72
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        if (mmi_view_key == null)
        {
            // it's worth noting a ling could have multiple of these actions.
            mmi_view_key = Dm.text("ling_mmi_[]_view", REF(this));
            // Generate background
            mmi_view_background = Dm.@new<atom_movable_screen_background>();
            mmi_view_background.assigned_map = mmi_view_key;
            mmi_view_background.del_on_map_removal = FALSE;
            mmi_view_background.fill_rect(1, 1, 5, 5);
            // Generate map view
            mmi_view = Dm.@new<atom_movable_screen_map_view>();
            mmi_view.generate_view(mmi_view_key);
            mmi_view.set_display(brain_ref);
            // Generate movement detector (to update the view on MMI movement)
            update_view_tracker = Dm.@new<datum_movement_detector>(brain_ref, Dm.@new<datum_callback>(this, nameof(update_mmi_view)));
        }

        // Register map objects
        user.client.register_map_obj(mmi_view_background);
        update_mmi_view();
        // Makes the MMI relay heard messages
        if (!HAS_TRAIT_FROM(brain_ref.loc, TRAIT_HEARING_SENSITIVE, REF(this)))
        {
            obj_item_brain_processor_organic mmi = brain_ref.loc as obj_item_brain_processor_organic;
            mmi.become_hearing_sensitive(Dm.str(REF(this)));
            RegisterSignal(mmi, COMSIG_MOVABLE_HEAR, nameof(relay_hearing));
        }
        // Actually open the UI
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "LingMMITalk");
            await ui.open();
            // Open map view
            mmi_view.display_to(user, ui.window);
        }
        return null;
    }

    // tg: code/modules/antagonists/changeling/powers/mmi_talk.dm:104
    public override void ui_close(mob user = null)
    {
        obj_item_brain_processor_organic mmi = brain_ref.loc as obj_item_brain_processor_organic;
        UnregisterSignal(mmi, COMSIG_MOVABLE_HEAR);
        mmi.lose_hearing_sensitivity(Dm.str(REF(this)));
    }

    // tg: code/modules/antagonists/changeling/powers/mmi_talk.dm:109
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = null;
        dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return TRUE;

        if (!Dm.eq(action, "send_mmi_message"))
            return FALSE;

        obj_item_brain_processor_organic mmi = brain_ref.loc as obj_item_brain_processor_organic;
        if (IS_UNCONSCIOUS_OR_CRIT(mmi.brainmob))
            to_chat(usr, span_warning(I18n.T("Our decoy brain is too damaged to speak.")));
        else
            // Say will perform input sanitization and such for us
            await mmi.brainmob.say(@params["message"], sanitize: TRUE);
        return TRUE;
    }

    /// Used in callbacks to update the map view when the MMI moves.
    // tg: code/modules/antagonists/changeling/powers/mmi_talk.dm:126
    public virtual void update_mmi_view()
    {
        mmi_view.vis_contents.Cut();
        foreach (turf visible_turf in Dm.each<turf>(view(2, get_turf(brain_ref))))
            Dm.append(mmi_view.vis_contents, visible_turf);
    }

    /// Signal proc for [COMSIG_MOVABLE_HEAR] to relay stuff the MMI hears to the ling.
    /// Not super good, but it works.
    // tg: code/modules/antagonists/changeling/powers/mmi_talk.dm:133
    [SignalHandler]
    public virtual void relay_hearing(obj_item_brain_processor_organic source = null, DmList hear_args = null)
    {
        // We can likely already hear them, so do not bother
        if (Dm.truthy(can_see(owner, hear_args[HEARING_SPEAKER] as atom, 7)))
            return;

        DmList new_args = hear_args.Copy();
        Dm.combine_at(new_args, HEARING_SPANS, "purple");
        new_args[HEARING_RANGE] = INFINITY; // so we can hear it from any distance away
        Waiting.NoSleep(Dm.Call(owner, nameof(atom_movable.Hear), Dm.arglist(new_args)));
    }
}
