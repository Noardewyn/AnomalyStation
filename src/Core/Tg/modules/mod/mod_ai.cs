// Translated from tgstation (snapshot 1d99bd7f): code/modules/mod/mod_ai.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps MO1 (D333): the file whole. A MODsuit's AI assistant: an AI carried on an intellicard is uploaded into an open control
// unit (and downloaded back), a pAI card is slotted into it (and taken out), and the assistant gets the suit's AI action buttons;
// an assistant walks the suit (with its wearer in it, or alone) one powered step at a time, the wearer held standing while it does;
// an AI mini-card holds a dead AI ejected from a wrecked suit until an intellicard recovers it.

public partial class obj_item_mod_control
{
    // tg: code/modules/mod/mod_ai.dm:131-135 (file-local, #undef at :159-162 and :216)
    private const float MOVE_DELAY = 2;
    private const float WEARER_DELAY = 1;
    private const float LONE_DELAY = 5;
    private const float CHARGE_PER_STEP = DEFAULT_CHARGE_DRAIN * 2.5f;
    private const float AI_FALL_TIME = 1 * SECONDS;

    // tg: code/modules/mod/mod_ai.dm:1
    public override async DmTask<object> transfer_ai(object interaction = null, mob user = null, mob_living_silicon_ai AI = null, obj_item_aicard card = null)
    {
        mob_living_silicon_ai intAI = AI;
        object dot = await base.transfer_ai(interaction, user, AI, card);
        if (!Dm.truthy(dot))
            return dot;
        if (!Dm.truthy(open)) //mod must be open
        {
            balloon_alert(user, I18n.T("panel closed!"));
            return dot;
        }
        if (Dm.eq(interaction, AI_TRANS_TO_CARD))
        {
            if (ai_assistant == null)
            {
                balloon_alert(user, I18n.T("no ai in unit!"));
                return dot;
            }
            balloon_alert(user, I18n.T("transferring to card..."));
            if (!Dm.truthy(await do_after(user, 5 * SECONDS, target: this)))
            {
                balloon_alert(user, I18n.T("interrupted!"));
                return dot;
            }
            if (ai_assistant == null)
            {
                balloon_alert(user, I18n.T("no ai in unit!"));
                return dot;
            }
            balloon_alert(user, I18n.T("ai transferred to card"));
            await ai_exit_mod(card);
        }
        else if (Dm.eq(interaction, AI_TRANS_FROM_CARD)) //Using an AI card to upload to the suit.
        {
            intAI = card.AI;
            if (intAI == null)
            {
                balloon_alert(user, I18n.T("no ai in card!"));
                return dot;
            }
            if (ai_assistant != null)
            {
                balloon_alert(user, I18n.T("already has ai!"));
                return dot;
            }
            if (intAI.deployed_shell != null) //Recall AI if shelled so it can be checked for a client
                intAI.disconnect_shell();
            if (IS_UNCONSCIOUS_OR_CRIT(intAI) || intAI.client == null)
            {
                balloon_alert(user, I18n.T("ai unresponsive!"));
                return dot;
            }
            balloon_alert(user, I18n.T("transferring to unit..."));
            if (!Dm.truthy(await do_after(user, 5 * SECONDS, target: this)))
            {
                balloon_alert(user, I18n.T("interrupted!"));
                return dot;
            }
            if (ai_assistant != null)
                return dot;
            balloon_alert(user, I18n.T("ai transferred to unit"));
            await ai_enter_mod(intAI);
            card.AI = null;
        }
        return dot;
    }

    /// Place an AI in control of your suit functions
    // tg: code/modules/mod/mod_ai.dm:47
    public virtual async DmTask ai_enter_mod(mob_living_silicon_ai new_ai = null)
    {
        new_ai.set_control_disabled(FALSE);
        new_ai.radio_enabled = TRUE;
        new_ai.ai_restore_power();
        await new_ai.cancel_camera();
        new_ai.controlled_equipment = this;
        new_ai.remote_control = this;
        await new_ai.forceMove(this);
        await on_gained_assistant(new_ai);
    }

    /// Remove an AI's control of your suit functions
    // tg: code/modules/mod/mod_ai.dm:58
    public virtual async DmTask ai_exit_mod(obj_item_aicard card = null)
    {
        mob_living_silicon_ai old_ai = ai_assistant as mob_living_silicon_ai;
        old_ai.ai_restore_power(); //So the AI initially has power.
        old_ai.set_control_disabled(TRUE);
        old_ai.radio_enabled = FALSE;
        old_ai.disconnect_shell();
        await old_ai.forceMove(card);
        card.AI = old_ai;
        old_ai.controlled_equipment = null;
        await on_removed_assistant();
    }

    /// Place a pAI in control of your suit functions
    // tg: code/modules/mod/mod_ai.dm:70
    public virtual async DmTask<object> insert_pai(mob user = null, obj_item_pai_card card = null)
    {
        if ((ai_assistant != null))
        {
            balloon_alert(user, I18n.T("slot occupied!"));
            return FALSE;
        }
        if ((card.pai?.mind == null))
        {
            balloon_alert(user, I18n.T("pAI unresponsive!"));
            return FALSE;
        }
        balloon_alert(user, I18n.T("transferring to unit..."));
        if (!Dm.truthy(await do_after(user, 5 * SECONDS, target: this)))
        {
            balloon_alert(user, I18n.T("interrupted!"));
            return FALSE;
        }
        if (!Dm.truthy(await user.transferItemToLoc(card, this)))
        {
            balloon_alert(user, I18n.T("transfer failed!"));
            return FALSE;
        }
        balloon_alert(user, I18n.T("pAI transferred to unit"));
        mob_living_silicon_pai pai_assistant = card.pai;
        pai_assistant.can_transmit = TRUE;
        pai_assistant.can_receive = TRUE;
        pai_assistant.can_holo = FALSE;
        if (Dm.truthy(pai_assistant.holoform))
            await pai_assistant.fold_in();
        SStgui.close_uis(card);
        await on_gained_assistant(card.pai);
        return TRUE;
    }

    /// Removes pAI control from a modsuit
    // tg: code/modules/mod/mod_ai.dm:96
    public virtual async DmTask<object> remove_pai(mob user = null, float? forced = null)
    {
        forced ??= FALSE;
        if ((ai_assistant == null))
        {
            balloon_alert(user, I18n.T("no pAI!"));
            return FALSE;
        }
        if (!Dm.truthy(forced))
        {
            if (!Dm.truthy(open))
            {
                balloon_alert(user, I18n.T("panel closed!"));
                return FALSE;
            }
            balloon_alert(user, I18n.T("uninstalling card..."));
            if (!Dm.truthy(await do_after(user, 5 * SECONDS, target: this)))
            {
                balloon_alert(user, I18n.T("interrupted!"));
                return FALSE;
            }
        }

        balloon_alert(user, I18n.T("pAI removed"));
        mob_living_silicon_pai pai_helper = ai_assistant as mob_living_silicon_pai;
        pai_helper.can_holo = TRUE;
        await pai_helper.card.forceMove(get_turf(this));
        await on_removed_assistant();
        return null;
    }

    /// Called when a new ai assistant is inserted
    // tg: code/modules/mod/mod_ai.dm:116
    public virtual async DmTask on_gained_assistant(mob_living_silicon new_helper = null)
    {
        ai_assistant = new_helper;
        balloon_alert(new_helper, I18n.T("transferred to a mod unit"));
        foreach (datum_action action in Dm.each_cast<datum_action>(actions))
            await action.Grant(new_helper);
    }

    /// Called when an existing ai assistant is removed
    // tg: code/modules/mod/mod_ai.dm:123
    public virtual async DmTask on_removed_assistant()
    {
        foreach (datum_action action in Dm.each_cast<datum_action>(actions))
            await action.Remove(ai_assistant);
        ai_assistant.remote_control = null;
        balloon_alert(ai_assistant, I18n.T("transferred to a card"));
        ai_assistant = null;
    }

    // tg: code/modules/mod/mod_ai.dm:137
    public override async DmTask<object> relaymove(mob_living user = null, object direction = null)
    {
        if ((!Dm.truthy(active) && wearer != null) || Dm.num(get_charge()) < CHARGE_PER_STEP || user != ai_assistant || !(cooldown_mod_move <= world.time) || (Dm.num(wearer?.pulledby?.grab_state) > GRAB_PASSIVE))
            return FALSE;
        datum_mod_part legs_to_move = get_part_datum_from_slot(ITEM_SLOT_FEET) as datum_mod_part;
        if (wearer != null && (legs_to_move == null || !Dm.truthy(legs_to_move.@sealed)))
            return FALSE;
        float timemodifier = MOVE_DELAY * (Dm.truthy(ISDIAGONALDIR(Dm.num(direction))) ? sqrt(2) : 1) * (wearer != null ? WEARER_DELAY : LONE_DELAY);
        if (wearer != null && !Dm.truthy(await wearer.Process_Spacemove(Dm.num(direction))))
            return FALSE;
        else if (wearer == null && (!Dm.truthy(has_gravity()) || !isturf(loc)))
            return FALSE;
        cooldown_mod_move = world.time + (movedelay * timemodifier + slowdown_deployed); // COOLDOWN_START(src, cooldown_mod_move, ...)
        await subtract_charge(CHARGE_PER_STEP);
        playsound(this, Dm.resource("sound/vehicles/mecha/mechmove01.ogg"), 25, TRUE);
        if (ismovable(wearer?.loc))
            return await wearer.loc.relaymove(wearer, direction);
        else if (wearer != null)
        {
            ADD_TRAIT(wearer, TRAIT_FORCED_STANDING, REF(this));
            _addtimer(Dm.@new<datum_callback>(this, nameof(ai_fall)), AI_FALL_TIME, Dm.num(Dm.bitor(TIMER_UNIQUE, TIMER_OVERRIDE)), file: "code/modules/mod/mod_ai.dm", line: 155);
        }
        atom_movable mover = (atom_movable)wearer ?? this;
        return await mover.try_step_multiz(direction);
    }

    // tg: code/modules/mod/mod_ai.dm:163
    public virtual void ai_fall()
    {
        if (wearer == null)
            return;
        REMOVE_TRAIT(wearer, TRAIT_FORCED_STANDING, REF(this));
    }
}

// tg: code/modules/mod/mod_ai.dm:168
public partial class obj_item_mod_ai_minicard : obj_item_mod
{
    // tg: code/modules/mod/mod_ai.dm:175
    [DmParams("mapload", "ai")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        mob_living_silicon_ai ai = Dm.arg<mob_living_silicon_ai>(__extra, 0);
        object dot = base.Initialize(mapload, __extra);
        if ((ai == null))
            return dot;
        ai.controlled_equipment = null;
        ai.remote_control = null;
        Waiting.NoSleep(ai.apply_damage(150, BURN));
        Waiting.Detach(ai.death()); // INVOKE_ASYNC(ai, TYPE_PROC_REF(/mob/living/silicon/ai, death))
        Waiting.NoSleep(ai.forceMove(this));
        stored_ai = WEAKREF(ai) as datum_weakref;
        icon_state = "minicard-filled";
        return dot;
    }

    // tg: code/modules/mod/mod_ai.dm:187
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref stored_ai);
        return base.Destroy(force);
    }

    // tg: code/modules/mod/mod_ai.dm:191
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("You see [] stored inside.", stored_ai.resolve() is var or__1 && Dm.truthy(or__1) ? or__1 : I18n.T("no AI"))));
        return dot;
    }

    // tg: code/modules/mod/mod_ai.dm:195
    public override async DmTask<object> transfer_ai(object interaction = null, mob user = null, mob_living_silicon_ai AI = null, obj_item_aicard card = null)
    {
        object dot = await base.transfer_ai(interaction, user, AI, card);
        if (!Dm.truthy(dot))
            return dot;
        if (!Dm.eq(interaction, AI_TRANS_TO_CARD))
            return dot;
        mob_living_silicon_ai ai = stored_ai.resolve() as mob_living_silicon_ai;
        if (ai == null)
        {
            balloon_alert(user, I18n.T("no ai!"));
            return dot;
        }
        balloon_alert(user, I18n.T("transferring to card..."));
        if (!Dm.truthy(await do_after(user, 5 * SECONDS, target: this)) || ai == null)
        {
            balloon_alert(user, I18n.T("interrupted!"));
            return dot;
        }
        icon_state = "minicard";
        await ai.forceMove(card);
        card.AI = ai;
        ai.notify_revival(I18n.T("You have been recovered from the wreckage!"), source: card);
        balloon_alert(user, I18n.T("ai transferred to card"));
        stored_ai = null;
        return dot;
    }
}
