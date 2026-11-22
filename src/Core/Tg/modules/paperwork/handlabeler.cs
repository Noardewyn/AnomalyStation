// Translated from tgstation (snapshot 1d99bd7f): code/modules/paperwork/handlabeler.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file but the label's vv_edit_var (admin variable editing: a live-partial row), gaps S3 (D298); the cyborg's labeler is a cyborg module since silicons SI2. The var blocks
// are generated.

/// A mini-tool used to apply label items onto something to modify its name.
// tg: code/modules/paperwork/handlabeler.dm:2
public partial class obj_item_hand_labeler : obj_item
{
    // tg: code/modules/paperwork/handlabeler.dm:22
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] is pointing [] at []self. [] going to label [] as a suicide!", user, this, user.p_them(), user.p_Theyre(), user.p_them())));
        labels_left = max(labels_left - 1, 0);

        object old_real_name = user.real_name;
        user.real_name += " (suicide)";
        // no conflicts with their identification card
        foreach (atom A in Dm.each<atom>(user.get_all_contents()))
        {
            if (isidcard(A))
            {
                obj_item_card_id their_card = (obj_item_card_id)A;

                // only renames their card, as opposed to tagging everyone's
                if (!Dm.eq(their_card.registered_name, old_real_name))
                    continue;

                their_card.registered_name = user.real_name;
                their_card.update_label();
                await their_card.update_icon();
            }
        }

        // NOT EVEN DEATH WILL TAKE AWAY THE STAIN
        user.mind.name += " (suicide)";

        mode = 1;
        icon_state = Dm.text("labeler[]", mode);
        label = "suicide";

        return OXYLOSS;
    }

    // tg: code/modules/paperwork/handlabeler.dm:50
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        object dot = await base.interact_with_atom(interacting_with, user, modifiers);
        if (Dm.truthy(Dm.bitand(dot, ITEM_INTERACT_ANY_BLOCKER)))
            return dot;
        if (!Dm.truthy(mode)) //if it's off, give up.
            return dot;
        if (!Dm.truthy(await apply_label(interacting_with, user, modifiers)))
            return ITEM_INTERACT_BLOCKING;
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/paperwork/handlabeler.dm:60
    public virtual async DmTask<object> apply_label(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!Dm.truthy(labels_left))
        {
            balloon_alert(user, I18n.T("no labels left!"));
            return FALSE;
        }
        if (!Dm.truthy(length(label)))
        {
            balloon_alert(user, I18n.T("no text set!"));
            return FALSE;
        }
        if (length(interacting_with.name) + length(label) > MAX_LABEL_LEN)
        {
            balloon_alert(user, I18n.T("label too long!"));
            return FALSE;
        }
        if (ismob(interacting_with))
        {
            interacting_with.balloon_alert(user, I18n.T("can't label!"));
            return FALSE;
        }

        float? cursor_x = text2num(LAZYACCESS(modifiers, ICON_X));
        float? cursor_y = text2num(LAZYACCESS(modifiers, ICON_Y));
        interacting_with.balloon_alert_to_viewers(I18n.T("labelled"));
        user.visible_message(
            span_notice(I18n.T("[] labels [] with \"[]\".", user, interacting_with, label)),
            span_notice(I18n.T("You label [] with \"[]\".", interacting_with, label)));
        obj_item_label stick_label = Dm.@new<obj_item_label>(null, label);
        await stick_label.stick_to_atom(interacting_with, cursor_x, cursor_y);
        playsound(interacting_with, Dm.resource("sound/items/handling/component_pickup.ogg"), 20, TRUE);
        labels_left--;
        return TRUE;
    }

    // tg: code/modules/paperwork/handlabeler.dm:88
    public override async DmTask<object> interact(mob user = null, params object[] __extra)
    {
        object dot = await base.interact(user, __extra);
        if (Dm.truthy(dot))
            return dot;
        if (!ISADVANCEDTOOLUSER(user))
        {
            to_chat(user, span_warning(I18n.T("You don't have the dexterity to use []!", this)));
            return dot;
        }

        mode = Dm.truthy(mode) ? FALSE : TRUE;
        icon_state = Dm.text("labeler[]", mode);
        if (Dm.truthy(mode))
        {
            to_chat(user, span_notice(I18n.T("You turn on [].", this)));
            //Now let them chose the text.
            object str = reject_bad_text(await tgui_input_text(user, I18n.T("Label text"), I18n.T("Set Label"), label, MAX_NAME_LEN));
            if (!Dm.truthy(str) || QDELETED(this) || !Dm.truthy(user.is_holding(this)))
            {
                to_chat(user, span_warning(I18n.T("Invalid text!")));
                return dot;
            }
            label = str;
            to_chat(user, span_notice(I18n.T("You set the text to '[]'.", str)));
        }
        else
            to_chat(user, span_notice(I18n.T("You turn off [].", this)));
        return TRUE;
    }

    // tg: code/modules/paperwork/handlabeler.dm:110
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!istype(tool, typeof(obj_item_hand_labeler_refill)))
            return NONE;

        balloon_alert(user, I18n.T("refilled"));
        qdel(tool);
        labels_left = Dm.num(Dm.initial(this, nameof(labels_left))); //Yes, it's capped at its initial value
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/paperwork/handlabeler.dm:119
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (labels_left > 0)
            Dm.append(ref dot, span_notice(I18n.T("It looks like it could label [] more thing\\s.", labels_left)));
        else
            Dm.append(ref dot, span_notice(I18n.T("It's out of labels.")));
        return dot;
    }
}

// tg: code/modules/paperwork/handlabeler.dm:126
public partial class obj_item_hand_labeler_borg : obj_item_hand_labeler
{
    // tg: code/modules/paperwork/handlabeler.dm:129
    public override async DmTask<object> apply_label(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        mob_living_silicon_robot robot = user as mob_living_silicon_robot;
        if (robot == null)
            return FALSE;

        object dot = await base.apply_label(interacting_with, user, modifiers);
        if (!Dm.truthy(dot))
            return dot;

        float starting_labels = Dm.num(Dm.initial(this, nameof(labels_left)));
        float diff = starting_labels - labels_left;
        if (diff != 0)
        {
            labels_left = starting_labels;
            // 50 per label. Magical cyborg paper doesn't come cheap.
            float cost = diff * 50;

            // If the cyborg manages to use a module without a cell, they get the paper
            // for free.
            if ((object)robot.cell is obj_item_stock_parts_power_store cell)
                await cell.use(cost);
        }

        return dot;
    }
}

/// The label item applied when labelling something
// tg: code/modules/paperwork/handlabeler.dm:168
public partial class obj_item_label : obj_item
{
    // tg: code/modules/paperwork/handlabeler.dm:188
    [DmParams("mapload", "new_label_name")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object new_label_name = Dm.arg(__extra, 0);
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(new_label_name))
            update_label_name(new_label_name);
        return dot;
    }

    // tg: code/modules/paperwork/handlabeler.dm:193
    public override object Destroy(float? force = null)
    {
        clear_stick_to();
        return base.Destroy(force);
    }

    // tg: code/modules/paperwork/handlabeler.dm:197
    public override object update_name(float? updates = null)
    {
        object dot = base.update_name(updates);
        if (Dm.truthy(label_name))
            name = I18n.T("label ([])", label_name);
        return dot;
    }

    /// Sets the lable_name var and performs any necessary updates to the label's appearance
    // tg: code/modules/paperwork/handlabeler.dm:203
    public virtual void update_label_name(object new_label_name = null)
    {
        if (Dm.eq(label_name, new_label_name))
            return;

        if (sticking_to != null)
            remove_label();
        label_name = new_label_name;
        if (sticking_to != null)
            apply_label();
        update_appearance(UPDATE_NAME);
    }

    // tg: code/modules/paperwork/handlabeler.dm:222
    public virtual async DmTask stick_to_atom(atom applying_to = null, float? stick_px = null, float? stick_py = null)
    {
        stick_px ??= ICON_SIZE_X / 2;
        stick_py ??= ICON_SIZE_Y / 2;
        // AddComponent(/datum/component/sticker, stickering_atom = src, dir = applying_to.dir, px = stick_px, py = stick_py,
        // stick_callback = ..., peel_callback = ...): by the component's parameter order
        await applying_to.AddComponent(typeof(datum_component_sticker),
            this,
            applying_to.dir,
            stick_px,
            stick_py,
            Dm.@new<datum_callback>(this, nameof(on_stick)),
            Dm.@new<datum_callback>(this, nameof(on_peel)));
    }

    /// Callback invoked when the label is attached to something
    // tg: code/modules/paperwork/handlabeler.dm:234
    public virtual void on_stick(atom applying_to = null)
    {
        sticking_to = applying_to;
        RegisterSignal(sticking_to, COMSIG_ATOM_ITEM_INTERACTION, nameof(interacted_with));
        RegisterSignal(sticking_to, COMSIG_ATOM_EXAMINE, nameof(on_examine));
        RegisterSignals(sticking_to, Dm.list(SIGNAL_ADDTRAIT(TRAIT_WAS_RENAMED), SIGNAL_REMOVETRAIT(TRAIT_WAS_RENAMED)), nameof(reapply));
        RegisterSignal(sticking_to, COMSIG_QDELETING, nameof(clear_stick_to));
        apply_label();
    }

    /// Callback invoked when the label is removed from something
    // tg: code/modules/paperwork/handlabeler.dm:243
    public virtual void on_peel(atom peeled_from = null)
    {
        qdel(this);
    }

    /// General purpose / signal proc used to clear references and clean up when removed
    // tg: code/modules/paperwork/handlabeler.dm:247
    public virtual void clear_stick_to(params object[] __extra)
    {
        if (sticking_to == null)
            return;
        if (!QDELING(sticking_to))
            remove_label();
        UnregisterSignal(sticking_to, Dm.list(
            COMSIG_ATOM_ITEM_INTERACTION,
            COMSIG_ATOM_EXAMINE,
            SIGNAL_ADDTRAIT(TRAIT_WAS_RENAMED),
            SIGNAL_REMOVETRAIT(TRAIT_WAS_RENAMED),
            COMSIG_QDELETING));
        sticking_to = null;
    }

    /**
     * This proc will trigger when any object is used to attack the thing we're stuck to. .
     *
     * If the attacking object is not a hand labeler, it will return.
     * If the attacking object is a hand labeler, it will either update the label or remove the label entirely.
     *
     * Arguments:
     * * source: The parent.
     * * attacker: The object that is hitting the parent.
     * * user: The mob who is wielding the attacking object.
    */
    // tg: code/modules/paperwork/handlabeler.dm:273
    public virtual object interacted_with(atom source = null, mob_living user = null, obj_item tool = null)
    {
        // If the attacking object is not a hand labeler or its mode is 1 (has a label ready to apply), return.
        // The hand labeler should be off (mode is 0), in order to remove a label.
        obj_item_hand_labeler labeler = tool as obj_item_hand_labeler;
        if (labeler == null)
            return NONE;

        if (Dm.truthy(labeler.mode))
        {
            if (!Dm.truthy(length(labeler.label)))
            {
                labeler.balloon_alert(user, I18n.T("no text set!"));
                return ITEM_INTERACT_BLOCKING;
            }
            if (Dm.eq(labeler.label, label_name))
            {
                sticking_to.balloon_alert(user, I18n.T("already labelled!"));
                return ITEM_INTERACT_BLOCKING;
            }
            if (length(Dm.initial(sticking_to, nameof(sticking_to.name))) + length(labeler.label) > MAX_LABEL_LEN)
            {
                sticking_to.balloon_alert(user, I18n.T("label too long!"));
                return ITEM_INTERACT_BLOCKING;
            }

            update_label_name(labeler.label);
            playsound(sticking_to, Dm.resource("sound/items/handling/component_pickup.ogg"), 20, TRUE);
            sticking_to.balloon_alert(user, I18n.T("label renamed"));
        }
        else
        {
            playsound(sticking_to, Dm.resource("sound/items/poster/poster_ripped.ogg"), 20, TRUE);
            sticking_to.balloon_alert(user, I18n.T("label removed"));
            qdel(this);
        }
        return ITEM_INTERACT_SUCCESS;
    }

    /**
     * This proc will trigger when someone examines the thing we're stuck to.
     * It will attach the text found in the body of the proc to the `examine_list` and display it to the player examining the parent.
     *
     * Arguments:
     * * source: The parent.
     * * user: The mob exmaining the parent.
     * * examine_list: The current list of text getting passed from the parent's normal examine() proc.
    */
    // tg: code/modules/paperwork/handlabeler.dm:312
    public virtual void on_examine(datum source = null, mob user = null, DmList examine_list = null)
    {
        Dm.append(examine_list, span_notice(I18n.T("It has a label with some words written on it. Use a hand labeler to remove it.")));
    }

    /// Applies a label to the name of what we're stuck to in the format of: "parent_name (label)"
    // tg: code/modules/paperwork/handlabeler.dm:318
    public virtual void apply_label()
    {
        sticking_to.name += Dm.text(" ([])", label_name);
        ADD_TRAIT(sticking_to, TRAIT_HAS_LABEL, REF(this));
    }

    /// Removes the label from the name of what we're stuck to
    // tg: code/modules/paperwork/handlabeler.dm:323
    public virtual void remove_label()
    {
        sticking_to.name = replacetext(sticking_to.name, Dm.text("([])", label_name), "") as string; // Remove the label text from the parent's name, wherever it's located.
        sticking_to.name = trim(sticking_to.name) as string; // Shave off any white space from the beginning or end of the parent's name.
        REMOVE_TRAIT(sticking_to, TRAIT_HAS_LABEL, REF(this));
    }

    /// Used to re-apply the label when the thing we're stuck to is renamed.
    // tg: code/modules/paperwork/handlabeler.dm:329
    public virtual void reapply(params object[] __extra)
    {
        remove_label();
        apply_label();
    }
}
