// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/payment.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P4a (D327): the whole file: a machine's fixed price, charged to the user's ID card account, or paid in held cash, coins and
// holochips with the change made in a holochip. The var blocks are generated.

/**
 * Handles simple payment operations where the cost of the object in question doesn't change.
 *
 * What this is useful for:
 * Basic forms of vending.
 * Objects that can drain the owner's money linearly.
 * What this is not useful for:
 * Things where the seller may want to fluxuate the price of the object.
 * Improving standardizing every form of payment handing, as some custom handling is specific to that object.
 **/
// tg: code/datums/components/payment.dm:12
public partial class datum_component_payment : datum_component
{
    // tg: code/datums/components/payment.dm:23
    [DmParams("_cost", "_target", "_style")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object _cost = Dm.arg(__extra, 0);
        object _target = Dm.arg(__extra, 1);
        object _style = Dm.arg(__extra, 2);
        target_acc = _target as datum_bank_account;
        if (target_acc == null)
            target_acc = SSeconomy?.get_dep_account(ACCOUNT_CIV); // SSeconomy?: a test harness world that makes machines without Master has none (rules 10.6)
        cost = Dm.num(_cost);
        transaction_style = Dm.str(_style);
        return null;
    }

    // tg: code/datums/components/payment.dm:30
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_OBJ_ATTEMPT_CHARGE, nameof(attempt_charge));
        RegisterSignal(parent, COMSIG_OBJ_ATTEMPT_CHARGE_CHANGE, nameof(change_cost));
    }

    // tg: code/datums/components/payment.dm:34
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, Dm.list(COMSIG_OBJ_ATTEMPT_CHARGE, COMSIG_OBJ_ATTEMPT_CHARGE_CHANGE));
    }

    // tg: code/datums/components/payment.dm:37
    [SignalHandler]
    public virtual object attempt_charge(datum source = null, atom_movable target = null, object extra_fees = null)
    {
        extra_fees ??= 0;
        if (!Dm.truthy(cost) && !Dm.truthy(extra_fees)) //In case a free variant of anything is made it'll skip charging anyone.
            return null;
        float total_cost = cost + Dm.num(extra_fees);
        if (!ismob(target))
            return COMPONENT_OBJ_CANCEL_CHARGE;
        mob user = (mob)target;
        if (HAS_SILICON_ACCESS(user) || isdrone(user)) //They have evolved beyond the need for mere credits
            return null;
        obj_item_card_id card = null;
        if (user is mob_living living_user)
            card = living_user.get_idcard(TRUE) as obj_item_card_id;
        if (card == null && user.pulling is obj_item_card_id pulled_card)
            card = pulled_card;
        if (Dm.truthy(handle_card(user as mob_living, card, total_cost)))
            return null; //Only breaks here if the card can handle the cost of purchasing with someone's ID.
        if (Dm.truthy(handle_cardless(user, total_cost))) //Here we attempt to handle the purchase physically, with held money first. Otherwise we default to below.
            return null;
        return COMPONENT_OBJ_CANCEL_CHARGE;
    }

    /**
     * Proc that changes the base cost of the interaction.
     *
     * * source: Datum source of the thing changing the cost.
     * * new_cost: the int value of the attempted new_cost to replace the cost value.
     */
    // tg: code/datums/components/payment.dm:64
    [SignalHandler]
    public virtual void change_cost(datum source = null, object new_cost = null)
    {
        if (!isnum(new_cost))
            throw Dm.CRASH("change_cost called with variable new_cost as not a number.", "code/datums/components/payment.dm", 68);
        cost = Dm.num(new_cost);
    }

    /**
     * Attempts to charge the mob, user, an integer number of credits, total_cost, without the use of an ID card to directly draw upon.
     */
    // tg: code/datums/components/payment.dm:74
    public virtual object handle_cardless(mob user = null, float total_cost = 0)
    {
        //Here is all the possible non-ID payment methods.
        DmList counted_money = Dm.list();
        float physical_cash_total = 0;
        foreach (obj_item credit in Dm.each<obj_item>(typecache_filter_list(user.get_all_contents() as DmList, GLOB.allowed_money) as DmList)) //Coins, cash, and credits.
        {
            if (physical_cash_total > total_cost)
                break;
            physical_cash_total += Dm.num(credit.get_item_credit_value());
            Dm.append(ref counted_money, credit);
        }

        if (Dm.truthy(is_type_in_typecache(user.pulling, GLOB.allowed_money)) && (physical_cash_total < total_cost)) //Coins(Pulled).
        {
            var counted_credit = user.pulling as obj_item;
            physical_cash_total += Dm.num(counted_credit.get_item_credit_value());
            Dm.append(ref counted_money, counted_credit);
        }

        if (physical_cash_total < total_cost)
        {
            bool armless = false; //Suggestions for those with no arms/simple animals.
            if (!ishuman(user) && !isslime(user))
                armless = true;
            else
            {
                var harmless_armless = user as mob_living_carbon_human;
                if (harmless_armless?.get_bodypart(BODY_ZONE_L_ARM) == null && harmless_armless?.get_bodypart(BODY_ZONE_R_ARM) == null)
                    armless = true;
            }

            if (armless)
            {
                if (user.pulling == null || !iscash(user.pulling) && user.pulling is not obj_item_card_id)
                {
                    to_chat(user, span_notice(I18n.T("Try pulling a valid ID, space cash, holochip or coin while using \\the []!", parent)));
                    return FALSE;
                }
            }
            return FALSE;
        }

        if (physical_cash_total < total_cost)
        {
            to_chat(user, span_warning(I18n.T("Insufficient funds. Aborting.")));
            return FALSE;
        }
        foreach (obj cash_object in Dm.each<obj>(counted_money))
            qdel(cash_object);
        physical_cash_total -= total_cost;

        if (physical_cash_total > 0)
        {
            var holochange = Dm.@new<obj_item_holochip>(user.loc, physical_cash_total); //Change is made in holocredits exclusively.
            holochange.name = I18n.T("[] [] holochip", holochange.credits, MONEY_NAME_SINGULAR);
            if (ishuman(user))
            {
                var paying_customer = (mob_living_carbon_human)user;
                // ASYNC: put_in_hands can sleep, we don't want that to block this proc (the result is read at once, as tg's)
                object successfully_put_in_hands = null;
                Waiting.Detach(put_change_in_hands(paying_customer, holochange, result => successfully_put_in_hands = result));
                if (!Dm.truthy(successfully_put_in_hands))
                    user.pulling = holochange;
            }
            else
                user.pulling = holochange;
        }
        log_econ(Dm.text("[] [] were spent on [] by [].", total_cost, MONEY_NAME, parent, user));
        to_chat(user, span_notice(I18n.T("Purchase completed with held [].", MONEY_NAME)));
        playsound(user, "sound/effects/cashregister.ogg", 20, TRUE);
        return TRUE;
    }

    // The ASYNC block of handle_cardless (payment.dm:117-118): put_in_hands runs detached; its result is stored when it returns, which
    // for a hand that is free is at once.
    private static async DmTask put_change_in_hands(mob_living_carbon_human paying_customer, obj_item_holochip holochange, Action<object> store)
    {
        store(await paying_customer.put_in_hands(holochange));
    }

    /**
     * Attempts to charge a mob, user, an integer number of credits, total_cost, directly from an ID card/bank account.
     */
    // tg: code/datums/components/payment.dm:131
    public virtual object handle_card(mob_living user = null, obj_item_card_id idcard = null, float total_cost = 0)
    {
        var atom_parent = parent as atom_movable;

        if (idcard == null)
        {
            if (Dm.eq(transaction_style, PAYMENT_VENDING))
                to_chat(user, span_warning(I18n.T("No card found.")));
            return FALSE;
        }
        if (idcard?.registered_account == null)
        {
            switch (transaction_style)
            {
                case PAYMENT_FRIENDLY:
                    to_chat(user, span_warning(I18n.T("There's no account detected on your ID, how mysterious!")));
                    break;
                case PAYMENT_ANGRY:
                    to_chat(user, span_warning(I18n.T("ARE YOU JOKING. YOU DON'T HAVE A BANK ACCOUNT ON YOUR ID YOU IDIOT.")));
                    break;
                case PAYMENT_CLINICAL:
                    to_chat(user, span_warning(I18n.T("ID Card lacks a bank account. Advancing.")));
                    break;
                case PAYMENT_VENDING:
                    to_chat(user, span_warning(I18n.T("No account found.")));
                    break;
            }

            return FALSE;
        }

        if (!Dm.truthy(idcard.can_be_used_in_payment(user)))
        {
            Waiting.Detach(atom_parent.say(I18n.T("Departmental accounts have been blacklisted from personal expenses due to embezzlement.")));
            return FALSE;
        }

        if (!idcard.registered_account.has_money(total_cost))
        {
            switch (transaction_style)
            {
                case PAYMENT_FRIENDLY:
                    to_chat(user, span_warning(I18n.T("I'm so sorry... You don't seem to have enough money.")));
                    break;
                case PAYMENT_ANGRY:
                    to_chat(user, span_warning(I18n.T("YOU MORON. YOU ABSOLUTE BAFOON. YOU INSUFFERABLE TOOL. YOU ARE POOR.")));
                    break;
                case PAYMENT_CLINICAL:
                    to_chat(user, span_warning(I18n.T("ID Card lacks funds. Aborting.")));
                    break;
                case PAYMENT_VENDING:
                    to_chat(user, span_warning(I18n.T("You do not possess the funds to purchase that.")));
                    break;
            }
            // MONEY_NAME_AUTOPURAL(total_cost) (__DEFINES/economy.dm:99), inline (rules 11.1)
            atom_parent.balloon_alert(user, I18n.T("needs [] []!", total_cost, Dm.text("credit[]", total_cost == 1 ? "" : "s")));
            return FALSE;
        }
        target_acc.transfer_money(idcard.registered_account, total_cost, I18n.T("Nanotrasen: Usage of Corporate Machinery"));
        log_econ(Dm.text("[] [] were spent on [] by [] via []'s card.", total_cost, MONEY_NAME, parent, user, idcard.registered_account.account_holder));
        Waiting.Detach(idcard.registered_account.bank_card_talk(I18n.T("[] [] deducted from your account.", total_cost, MONEY_NAME)));
        // tg plays it at src, the component, whose get_turf is null, so tg plays nothing; the register rings at the machine here
        playsound(atom_parent, "sound/effects/cashregister.ogg", 20, TRUE);
        SSeconomy.add_audit_entry(idcard.registered_account, total_cost, parent);
        return TRUE;
    }

    /**
     * Attempts to remove the payment component, currently when the crew wins a revolution.
     * * datum/source: source of the signal.
     */
    // tg: code/datums/components/payment.dm:178
    [SignalHandler]
    public virtual void clean_up(datum source = null)
    {
        target_acc = null;
        qdel(this);
    }
}
