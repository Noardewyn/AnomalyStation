// Translated from tgstation (snapshot 1d99bd7f): code/modules/economy/holopay.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P4a (D327): the whole file: the holographic pay stand an ID card projects, paid by card or holochip into the card's account,
// with its HoloPay window (name, logo, a forced fee), and gone when the card moves out of range. The var blocks are generated.

// tg: code/modules/economy/holopay.dm:1
public partial class obj_structure_holopay : obj_structure
{
    // tg: code/modules/economy/holopay.dm:27
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(force_fee))
            // MONEY_NAME_AUTOPURAL(force_fee) (__DEFINES/economy.dm:99), inline (rules 11.1)
            Dm.append(ref dot, span_boldnotice(I18n.T("This holopay forces a payment of <b>[]</b> [] per swipe instead of a variable amount.", force_fee, Dm.text("credit[]", force_fee == 1 ? "" : "s"))));
        return dot;
    }

    // tg: code/modules/economy/holopay.dm:32
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_holographic_nature)));
        register_context();
        return dot;
    }

    // tg: code/modules/economy/holopay.dm:37
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        object dot = base.add_context(source, context, held_item, user);

        if (isidcard(held_item))
        {
            context[SCREENTIP_CONTEXT_LMB] = I18n.T("Pay");
            var held_id = held_item as obj_item_card_id;
            if (held_id.my_store != null && held_id.my_store == this)
                context[SCREENTIP_CONTEXT_RMB] = I18n.T("Dissipate pay stand");
            return CONTEXTUAL_SCREENTIP_SET;
        }
        else if (held_item is obj_item_holochip)
        {
            context[SCREENTIP_CONTEXT_LMB] = I18n.T("Pay");
            return CONTEXTUAL_SCREENTIP_SET;
        }
        return dot;
    }

    // tg: code/modules/economy/holopay.dm:51
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        var living_user = user as mob_living;
        if (!Dm.truthy(living_user.combat_mode))
        {
            await ui_interact(user);
            return dot;
        }
        await user.do_attack_animation(this, ATTACK_EFFECT_PUNCH);
        user.changeNext_move(CLICK_CD_MELEE);
        await take_damage(5, BRUTE, MELEE, 1);
        return dot;
    }

    // tg: code/modules/economy/holopay.dm:62
    public override object play_attack_sound(object damage_amount = null, string damage_type = null, object damage_flag = null, params object[] __extra)
    {
        damage_type ??= BRUTE;
        switch (damage_type)
        {
            case BRUTE:
                playsound(loc as atom, "sound/items/weapons/egloves.ogg", 80, TRUE);
                break;
            case BURN:
                playsound(loc as atom, "sound/items/weapons/egloves.ogg", 80, TRUE);
                break;
        }
        return null;
    }

    // tg: code/modules/economy/holopay.dm:69
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        dissipate();
    }

    // tg: code/modules/economy/holopay.dm:72
    public override object Destroy(float? force = null)
    {
        if (linked_card != null)
            linked_card.my_store = null;
        linked_card = null;
        return base.Destroy(force);
    }

    // tg: code/modules/economy/holopay.dm:77
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        /// Users can pay with an ID to skip the UI
        if (isidcard(tool))
        {
            if (tool is obj_item_card_id_departmental_budget)
            {
                balloon_alert(user, I18n.T("invalid payment card"));
                to_chat(user, span_warning(I18n.T("You cannot use a departamental card for this.")));
                return ITEM_INTERACT_BLOCKING;
            }
            if (Dm.truthy(force_fee) && !Dm.eq(await tgui_alert(user, I18n.T("This holopay has a [] [] fee. Confirm?", force_fee, MONEY_SYMBOL), I18n.T("Holopay Fee"), Dm.list(I18n.T("Pay"), I18n.T("Cancel"))), I18n.T("Pay")))
                return ITEM_INTERACT_BLOCKING;
            await process_payment(user);
            return ITEM_INTERACT_SUCCESS;
        }
        /// Users can also pay by holochip
        if (tool is obj_item_holochip chip)
        {
            /// Account checks
            if (!Dm.truthy(chip.credits))
            {
                balloon_alert(user, I18n.T("holochip is empty"));
                to_chat(user, span_warning(I18n.T("There doesn't seem to be any [] here.", MONEY_NAME)));
                return ITEM_INTERACT_BLOCKING;
            }
            /// Charges force fee or uses pay what you want
            object cash_deposit = Dm.truthy(force_fee) ? force_fee : await tgui_input_number(user, I18n.T("How much? (Max: [])", chip.credits), I18n.T("Patronage"), max_value: chip.credits);
            /// Exit sanity checks
            if (!Dm.truthy(cash_deposit))
                return ITEM_INTERACT_BLOCKING;
            if (QDELETED(tool) || QDELETED(user) || QDELETED(this) || !Dm.truthy(await user.can_perform_action(this, FORBID_TELEKINESIS_REACH)))
                return ITEM_INTERACT_BLOCKING;
            if (!Dm.truthy(chip.spend(cash_deposit, FALSE)))
            {
                balloon_alert(user, I18n.T("insufficient []", MONEY_NAME));
                to_chat(user, span_warning(I18n.T("You don't have enough [] to pay with this chip.", MONEY_NAME)));
                return ITEM_INTERACT_BLOCKING;
            }
            /// Success: Alert buyer
            await alert_buyer(user, cash_deposit);
            return ITEM_INTERACT_SUCCESS;
        }
        /// Throws errors if they try to use space cash
        if (tool is obj_item_stack_spacecash)
        {
            to_chat(user, I18n.T("What is this, the 2000s? We only take card here."));
            return ITEM_INTERACT_BLOCKING;
        }
        if (tool is obj_item_coin)
        {
            to_chat(user, I18n.T("What is this, the 1800s? We only take card here."));
            return ITEM_INTERACT_BLOCKING;
        }
        return NONE;
    }

    // tg: code/modules/economy/holopay.dm:119
    public override async DmTask<object> item_interaction_secondary(mob_living user = null, obj_item tool = null, DmList modifiers = null)
    {
        /// Can kill it by right-clicking with ID because it seems useful and intuitive, to me, at least
        if (!isidcard(tool))
            return NONE;
        var attacking_id = tool as obj_item_card_id;
        if (attacking_id.my_store == null || attacking_id.my_store != this)
            return ITEM_INTERACT_BLOCKING;
        dissipate();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/economy/holopay.dm:129
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        object dot = await base.ui_interact(user, ui, __extra);
        if (Dm.truthy(dot))
            return FALSE;
        var interactor = user as mob_living;
        if (isliving(interactor) && Dm.truthy(interactor.combat_mode))
            return FALSE;
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "HoloPay");
            await ui.open();
        }
        return dot;
    }

    // tg: code/modules/economy/holopay.dm:141
    public override async DmTask<object> ui_status(mob user = null, datum_ui_state state = null)
    {
        object dot = await base.ui_status(user, state);
        if (!Dm.truthy(in_range(user, this)) && !isobserver(user))
            return UI_CLOSE;
        return dot;
    }

    // tg: code/modules/economy/holopay.dm:146
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList dot = Dm.list();
        dot["available_logos"] = obj_item_card_id.available_logos;
        dot["description"] = desc;
        dot["max_fee"] = linked_card.holopay_max_fee;
        dot["owner"] = linked_card.registered_account?.account_holder;
        dot["shop_logo"] = shop_logo;
        return dot;
    }

    // tg: code/modules/economy/holopay.dm:154
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList dot = Dm.list();
        dot["force_fee"] = force_fee;
        dot["name"] = name;
        if (!isliving(user))
            return dot;
        var card_holder = (mob_living)user;
        var id_card = card_holder.get_idcard(TRUE) as obj_item_card_id;
        datum_bank_account account = id_card?.registered_account;
        if (account != null)
        {
            dot["user"] = Dm.list();
            ((DmList)dot["user"])["name"] = account.account_holder;
            ((DmList)dot["user"])["balance"] = account.account_balance;
        }
        return dot;
    }

    // tg: code/modules/economy/holopay.dm:168
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return FALSE;
        switch (Dm.str(action))
        {
            case "done":
                await ui.send_full_update();
                return TRUE;
            case "fee":
                linked_card.set_holopay_fee(@params["amount"]);
                force_fee = linked_card.holopay_fee;
                break;
            case "logo":
                linked_card.set_holopay_logo(@params["logo"]);
                shop_logo = linked_card.holopay_logo;
                break;
            case "pay":
                ui.close();
                await process_payment(usr as mob_living);
                return TRUE;
            case "rename":
                linked_card.set_holopay_name(@params["name"]);
                name = linked_card.holopay_name;
                break;
        }
        return FALSE;
    }

    /**
     * Links the source card to the holopay. Begins checking if its in range.
     *
     * Parameters:
     * * turf/target - The tile to project the holopay onto
     * * obj/item/card/id/card - The card to link to the holopay
     * Returns:
     * * TRUE - the card was linked
     */
    // tg: code/modules/economy/holopay.dm:200
    public virtual async DmTask<object> assign_card(turf target = null, obj_item_card_id card = null)
    {
        linked_card = card;
        desc = I18n.T("Pays directly into []'s bank account.", card.registered_account.account_holder);
        force_fee = card.holopay_fee;
        shop_logo = card.holopay_logo;
        name = card.holopay_name;
        await add_atom_colour("#77abff", FIXED_COLOUR_PRIORITY);
        await set_light(2);
        visible_message(span_notice(I18n.T("A holographic pay stand appears.")));
        /// Start checking if the source projection is in range
        track(linked_card);
        return TRUE;
    }

    // tg: code/modules/economy/holopay.dm:213
    public virtual void track(atom_movable thing = null)
    {
        RegisterSignal(thing, COMSIG_MOVABLE_MOVED, nameof(handle_move));
        DmList locations = get_nested_locs(thing, include_turf: FALSE) as DmList;
        foreach (atom_movable location in Dm.each<atom_movable>(locations))
            RegisterSignal(location, COMSIG_MOVABLE_MOVED, nameof(handle_move));
    }

    // tg: code/modules/economy/holopay.dm:219
    public virtual void untrack(atom_movable thing = null)
    {
        UnregisterSignal(thing, COMSIG_MOVABLE_MOVED);
        DmList locations = get_nested_locs(thing, include_turf: FALSE) as DmList;
        foreach (atom_movable location in Dm.each<atom_movable>(locations))
            UnregisterSignal(location, COMSIG_MOVABLE_MOVED);
    }

    /**
     * A periodic check to see if the projecting card is nearby.
     * Deletes the holopay if not.
     */
    // tg: code/modules/economy/holopay.dm:229
    [SignalHandler]
    public virtual void handle_move(atom_movable source = null, atom old_loc = null, object dir = null, object forced = null, DmList old_locs = null)
    {
        if (ismovable(old_loc))
            untrack(old_loc as atom_movable);
        if (!IN_GIVEN_RANGE(this, linked_card, max_holo_range))
        {
            dissipate();
            return;
        }
        if (ismovable(source.loc))
            track(source.loc as atom_movable);
    }

    /**
     * Creates holopay vanishing effects.
     * Deletes the holopay thereafter.
     */
    // tg: code/modules/economy/holopay.dm:242
    public virtual void dissipate()
    {
        playsound(loc as atom, "sound/effects/empulse.ogg", 40, TRUE);
        visible_message(span_notice(I18n.T("The pay stand vanishes.")));
        qdel(this);
    }

    /**
     * Initiates a transaction between accounts.
     *
     * Parameters:
     * * mob/living/user - The user who initiated the transaction.
     * Returns:
     * * TRUE - transaction was successful
     */
    // tg: code/modules/economy/holopay.dm:255
    public virtual async DmTask<object> process_payment(mob_living user = null)
    {
        /// Account checks
        var id_card = user.get_idcard(TRUE) as obj_item_card_id;
        if (id_card == null || !Dm.truthy(id_card.can_be_used_in_payment(user)))
        {
            balloon_alert(user, I18n.T("invalid account"));
            to_chat(user, span_warning(I18n.T("You don't have a valid account.")));
            return FALSE;
        }
        datum_bank_account payee = id_card.registered_account;
        if (payee == linked_card?.registered_account)
        {
            balloon_alert(user, I18n.T("invalid transaction"));
            to_chat(user, span_warning(I18n.T("You can't pay yourself.")));
            return FALSE;
        }
        if (id_card is obj_item_card_id_departmental_budget)
        {
            balloon_alert(user, I18n.T("invalid payment card"));
            to_chat(user, span_warning(I18n.T("You cannot use a departamental card for this.")));
            return FALSE;
        }
        /// If the user has enough money, ask them the amount or charge the force fee
        object amount = Dm.truthy(force_fee) ? force_fee : await tgui_input_number(user, I18n.T("How much? (Max: [])", payee.account_balance), I18n.T("Patronage"), max_value: payee.account_balance);
        /// Exit checks in case the user cancelled or entered an invalid amount
        if (!Dm.truthy(amount) || QDELETED(user) || QDELETED(this) || !Dm.truthy(await user.can_perform_action(this, FORBID_TELEKINESIS_REACH)))
            return FALSE;
        if (!Dm.truthy(payee.adjust_money(-Dm.num(amount), I18n.T("Holopay: []", capitalize(name)))))
        {
            balloon_alert(user, I18n.T("insufficient []", MONEY_NAME));
            to_chat(user, span_warning(I18n.T("You don't have the money to pay for this.")));
            return FALSE;
        }
        /// Success: Alert the buyer
        await alert_buyer(user, amount);
        return TRUE;
    }

    /**
     * Alerts the owner of the transaction.
     *
     * Parameters:
     * * payee - The user who initiated the transaction.
     * * amount - The amount of money that was paid.
     * Returns:
     * * TRUE - alert was successful.
     */
    // tg: code/modules/economy/holopay.dm:294
    public virtual async DmTask<object> alert_buyer(object payee = null, object amount = null)
    {
        /// Pay the owner
        linked_card.registered_account.adjust_money(amount, I18n.T("Holopay: []", name));
        /// Make alerts
        await linked_card.registered_account.bank_card_talk(I18n.T("[] has deposited [] [] at your holographic pay stand.", payee, amount, MONEY_SYMBOL));
        await say(I18n.T("Thank you for your patronage, []!", payee));
        playsound(this, "sound/effects/cashregister.ogg", 20, TRUE);
        /// Log the event
        log_econ(Dm.text("[] [] were transferred from []'s transaction to []", amount, MONEY_NAME, payee, linked_card.registered_account.account_holder));
        SSblackbox.record_feedback("amount", "credits_transferred", amount);
        return TRUE;
    }
}
