// Translated from tgstation (snapshot 1d99bd7f): code/modules/economy/account.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P4a (D327): the whole file: the bank accounts with their balance, debt, paydays, transfers and transaction history, the card
// talk, the civilian bounty slots, the departmental budgets and the remote accounts. The chat line's card picture (icon2html) is a
// chat icon (not-translated section 76). The var blocks are generated.

// tg: code/modules/economy/account.dm:1  #define DUMPTIME 3000 (unused; #undef at :373)
// tg: code/modules/economy/account.dm:3  #define NO_MY_MONEY 10000 (#undef at :374)
// tg: code/modules/economy/account.dm:5
public partial class datum_bank_account : datum
{
    ///Amount of money you need to lose to get the negative moodlet.
    private const int NO_MY_MONEY = 10000;

    // tg: code/modules/economy/account.dm:47
    [DmParams("newname", "job", "modifier", "player_account")]
    public override async DmTask New(params object[] __extra)
    {
        object newname = Dm.arg(__extra, 0);
        datum_job job = Dm.arg(__extra, 1) as datum_job;
        object modifier = Dm.arg(__extra, 2) ?? 1;
        object player_account = Dm.arg(__extra, 3) ?? TRUE;
        account_holder = Dm.str(newname);
        account_job = job;
        payday_modifier = modifier;
        add_to_accounts = Dm.num(player_account);
        setup_unique_account_id();
        update_account_job_lists(job);
        pay_token = uppertext(Dm.text("[][]-[]-[]", copytext(Dm.str(newname), 1, 2), copytext(Dm.str(newname), -1), random_capital_letter(), rand(1111, 9999)));
    }

    // tg: code/modules/economy/account.dm:56
    public override object Destroy(float? force = null)
    {
        if (Dm.truthy(add_to_accounts))
        {
            SSeconomy.bank_accounts_by_id.Remove(Dm.text("[]", account_id));
            (SSeconomy.bank_accounts_by_job[account_job.type] as DmList)?.Remove(this);
        }
        QDEL_LIST(redeemed_coupons);
        return base.Destroy(force);
    }

    /**
     * Proc guarantees the account_id possesses a unique number.
     * If it doesn't, it tries to find a unique alternative.
     * It then adds it to the `SSeconomy.bank_accounts_by_id` global list.
     */
    // tg: code/modules/economy/account.dm:68
    public virtual void setup_unique_account_id()
    {
        if (!Dm.truthy(add_to_accounts))
            return;
        if (Dm.truthy(account_id) && !Dm.truthy(SSeconomy.bank_accounts_by_id[Dm.text("[]", account_id)]))
        {
            SSeconomy.bank_accounts_by_id[Dm.text("[]", account_id)] = this;
            return; //Already unique
        }
        for (int i = 1; i <= 1000; i++)
        {
            account_id = rand(111111, 999999);
            if (!Dm.truthy(SSeconomy.bank_accounts_by_id[Dm.text("[]", account_id)]))
                break;
        }
        if (Dm.truthy(SSeconomy.bank_accounts_by_id[Dm.text("[]", account_id)]))
            stack_trace(Dm.text("Unable to find a unique account ID, substituting currently existing account of id [].", account_id), "code/modules/economy/account.dm", 80);
        SSeconomy.bank_accounts_by_id[Dm.text("[]", account_id)] = this;
    }

    /**
     * Proc places this account into the right place in the `SSeconomy.bank_accounts_by_job` list, if needed.
     * If an old job is given, it removes it from its previous place first.
     */
    // tg: code/modules/economy/account.dm:86
    public virtual void update_account_job_lists(datum_job new_job = null, datum_job old_job = null)
    {
        if (!Dm.truthy(add_to_accounts))
            return;

        if (old_job != null)
            (SSeconomy.bank_accounts_by_job[old_job.type] as DmList)?.Remove(this);
        if (new_job != null)
        {
            DmList job_accounts = SSeconomy.bank_accounts_by_job[new_job.type] as DmList;
            LAZYADD(ref job_accounts, this);
            SSeconomy.bank_accounts_by_job[new_job.type] = job_accounts;
        }
    }

    // tg: code/modules/economy/account.dm:95  just so you don't have to do it manually
    public override async DmTask<object> vv_edit_var(object var_name = null, object var_value = null)
    {
        object old_id = account_id;
        datum_job old_job = account_job;
        float old_balance = account_balance;
        object dot = await base.vv_edit_var(var_name, var_value);
        if (Dm.eq(var_name, nameof(account_id)))
        {
            if (Dm.truthy(add_to_accounts))
            {
                SSeconomy.bank_accounts_by_id.Remove(Dm.text("[]", old_id));
                setup_unique_account_id();
            }
        }
        else if (Dm.eq(var_name, nameof(account_job)))
            update_account_job_lists(account_job, old_job);
        else if (Dm.eq(var_name, nameof(add_to_accounts)))
        {
            if (Dm.truthy(add_to_accounts))
            {
                setup_unique_account_id();
                update_account_job_lists(account_job);
            }
            else
            {
                SSeconomy.bank_accounts_by_id.Remove(Dm.text("[]", account_id));
                (SSeconomy.bank_accounts_by_job[account_job.type] as DmList)?.Remove(this);
            }
        }
        else if (Dm.eq(var_name, nameof(account_balance)))
            add_log_to_history(Dm.num(var_value) - old_balance, I18n.T("Nanotrasen: Moderator Action"));
        return dot;
    }

    /**
     * Sets the bank_account to behave as though a CRAB-17 event is happening.
     */
    // tg: code/modules/economy/account.dm:120
    public virtual void dumpeet(obj_structure_checkoutmachine dump_machine = null)
    {
        LAZYADD(ref being_dumped, dump_machine);
        money_crabbed = 0;
    }

    /**
     * Stops the dumping of the bank account.
     */
    // tg: code/modules/economy/account.dm:127
    public virtual async DmTask stop_dump(obj_structure_checkoutmachine dump_machine = null)
    {
        LAZYREMOVE(ref being_dumped, dump_machine);
        if (Dm.num(money_crabbed) < NO_MY_MONEY)
            return;
        foreach (obj card in Dm.each<obj>(bank_cards))
        {
            var card_holder = recursive_loc_check(card, typeof(mob_living)) as mob_living;
            if (!isliving(card_holder)) //If on a mob
                continue;
            //overwrite the slots event.
            await card_holder.add_mood_event(SLOTS_MOOD_CATEGORY, typeof(datum_mood_event_slots_all_gone));
        }
    }

    /**
     * Returns TRUE if a bank account has more than or equal to the amount, amt.
     * Otherwise returns false.
     * Arguments:
     * * amount - the quantity of credits that will be reconciled with the account balance.
     */
    // tg: code/modules/economy/account.dm:144
    public virtual bool has_money(object amount = null)
    {
        return account_balance >= Dm.num(amount);
    }

    /**
     * Adjusts the balance of a bank_account as well as sanitizes the numerical input.
     * Arguments:
     * * amount - the quantity of credits that will be written off if the value is negative, or added if it is positive.
     * * reason - the reason for the appearance or loss of money
     */
    // tg: code/modules/economy/account.dm:153
    public virtual object adjust_money(object amount = null, object reason = null)
    {
        float value = Dm.num(amount);
        if ((value < 0 && has_money(-value)) || value > 0)
        {
            float debt_collected = 0;
            if (account_debt > 0 && value > 0)
                debt_collected = min(ceil(value * DEBT_COLLECTION_COEFF), account_debt);
            account_balance += value - debt_collected;
            if (Dm.truthy(reason))
                add_log_to_history(value, reason);
            if (Dm.truthy(debt_collected))
                pay_debt(debt_collected, FALSE);
            return TRUE;
        }
        return FALSE;
    }

    ///Called when a portion of a debt is to be paid. It'll return the amount of credits put forwards to extinguish the debt.
    // tg: code/modules/economy/account.dm:167
    public virtual float pay_debt(object amount = null, object is_payment = null)
    {
        is_payment ??= TRUE;
        float amount_to_pay = min(Dm.num(amount), account_debt);
        if (Dm.truthy(is_payment))
        {
            if (!Dm.truthy(adjust_money(-Dm.num(amount), I18n.T("Other: Debt Payment"))))
                return 0;
        }
        else
            add_log_to_history(-Dm.num(amount), I18n.T("Other: Debt Collection"));
        log_econ(Dm.text("[] [] were removed from []'s bank account to pay a debt of []", amount_to_pay, MONEY_NAME, account_holder, account_debt));
        account_debt -= amount_to_pay;
        SEND_SIGNAL(COMSIG_BANK_ACCOUNT_DEBT_PAID, Dm.list(this));
        return amount_to_pay;
    }

    /**
     * Performs a transfer of credits to the bank_account datum from another bank account.
     * Arguments:
     * * datum/bank_account/from - The bank account that is sending the credits to this bank_account datum.
     * * amount - the quantity of credits that are being moved between bank_account datums.
     * * transfer_reason - override for adjust_money reason. Use if no default reason(Transfer to/from Name Surname).
     */
    // tg: code/modules/economy/account.dm:186
    public virtual object transfer_money(datum_bank_account from = null, object amount = null, object transfer_reason = null)
    {
        if (from.has_money(amount))
        {
            string reason_to = I18n.T("Transfer: From []", from.account_holder);
            string reason_from = I18n.T("Transfer: To []", account_holder);

            if (Dm.@in(from, SSeconomy.departmental_accounts)) // IS_DEPARTMENTAL_ACCOUNT (__DEFINES/economy.dm:49), inline (rules 11.1)
            {
                reason_to = I18n.T("Nanotrasen: Salary");
                reason_from = "";
            }

            if (Dm.truthy(transfer_reason))
            {
                reason_to = Dm.@in(this, SSeconomy.departmental_accounts) ? "" : Dm.str(transfer_reason);
                reason_from = Dm.str(transfer_reason);
            }

            adjust_money(amount, reason_to);
            from.adjust_money(-Dm.num(amount), reason_from);
            SSblackbox.record_feedback("amount", "credits_transferred", amount);
            log_econ(Dm.text("[] [] were transferred from []'s account to []", amount, MONEY_NAME, from.account_holder, this.account_holder));
            return TRUE;
        }
        return FALSE;
    }

    /**
     * This proc handles passive income gain for players, using their job's paycheck value.
     * Funds are taken from the parent department account to hand out to players. This can result in payment brown-outs if too many people are in one department.
     * Arguments:
     * * amount_of_paychecks - literally the number of salaries, 1 for issuing one salary, 5 for issuing five salaries.
     * * free - issuance of free funds, if TRUE then takes funds from the void, if FALSE (default) tries to send from the department's account.
     * * skippable - if TRUE, this proc may pay out nothing if the account has paydays_to_skip
     * * event - the name of the event that is being processed, used for bank card messages.
     */
    // tg: code/modules/economy/account.dm:215
    public virtual async DmTask<object> payday(object amount_of_paychecks = null, object free = null, object skippable = null, object @event = null)
    {
        free ??= FALSE;
        skippable ??= FALSE;
        @event ??= I18n.T("Payday");
        if (account_job == null)
            return FALSE;

        float paychecks = Dm.num(amount_of_paychecks);
        if (Dm.truthy(skippable) && !Dm.truthy(free))
        {
            while (paydays_to_skip > 0 && paychecks > 0)
            {
                paychecks -= 1;
                paydays_to_skip -= 1;
            }
        }

        if (paychecks <= 0)
            return FALSE;

        float money_to_transfer = round(Dm.num(account_job.paycheck) * Dm.num(payday_modifier) * paychecks);
        if (paychecks == 1)
            money_to_transfer = clamp(money_to_transfer, 0, PAYCHECK_CREW); //We want to limit single, passive paychecks to regular crew income.
        if (Dm.truthy(free))
        {
            adjust_money(money_to_transfer, I18n.T("Nanotrasen: Shift Payment"));
            SSblackbox.record_feedback("amount", "free_income", money_to_transfer);
            SSeconomy.station_target += money_to_transfer;
            log_econ(Dm.text("[] [] were given to []'s account from income.", money_to_transfer, MONEY_NAME, this.account_holder));
            return TRUE;
        }
        datum_bank_account department_account = SSeconomy.get_dep_account(account_job.paycheck_department);
        if (department_account == null)
        {
            await bank_card_talk(I18n.T("ERROR: [] aborted, unable to contact departmental account.", @event));
            return FALSE;
        }
        if (!Dm.truthy(transfer_money(department_account, money_to_transfer)))
        {
            await bank_card_talk(I18n.T("ERROR: [] aborted, departmental funds insufficient.", @event));
            return FALSE;
        }
        await bank_card_talk(I18n.T("[] processed, account now holds [] [].", @event, account_balance, MONEY_SYMBOL));
        return TRUE;
    }

    /**
     * This sends a local chat message to the owner of a bank account, on all ID cards registered to the bank_account.
     * If not held, sends out a message to all nearby players.
     * Arguments:
     * * message - text that will be sent to listeners after the id card icon
     * * force - if TRUE ignore checks on client and client prefernces.
     */
    // tg: code/modules/economy/account.dm:253
    public virtual async DmTask bank_card_talk(object message = null, object force = null)
    {
        if (!Dm.truthy(message) || !(LAZYLEN(bank_cards) > 0))
            return;
        foreach (obj card in Dm.each<obj>(bank_cards.Copy()))
        {
            // var/icon_source = card, or the ID card's get_cached_flat_icon(): the card's picture in the chat line, [icon2html(icon_source,
            // listener)] before the message (chat icons, not-translated section 76)
            var card_holder = recursive_loc_check(card, typeof(mob)) as mob;
            if (ismob(card_holder)) //If on a mob
            {
                if (card_holder.client == null || (!Dm.truthy(Dm.bitand(get_chat_toggles(card_holder.client), CHAT_BANKCARD)) && !Dm.truthy(force)))
                    return;

                if (!HAS_TRAIT(card_holder, TRAIT_DEAF))
                {
                    card_holder.playsound_local(get_turf(card_holder), "sound/machines/beep/twobeep_high.ogg", 50, TRUE);
                    to_chat(card_holder, span_notice(Dm.str(message)));
                }
            }
            else if (isturf(card.loc)) //If on the ground
            {
                var card_location = card.loc as turf;
                foreach (mob potential_hearer in Dm.each<mob>(hearers(1, card_location)))
                {
                    if (potential_hearer.client == null || (!Dm.truthy(Dm.bitand(get_chat_toggles(potential_hearer.client), CHAT_BANKCARD)) && !Dm.truthy(force)))
                        continue;
                    if (!HAS_TRAIT(potential_hearer, TRAIT_DEAF))
                    {
                        potential_hearer.playsound_local(card_location, "sound/machines/beep/twobeep_high.ogg", 50, TRUE);
                        to_chat(potential_hearer, span_notice(Dm.str(message)));
                    }
                }
            }
            else
            {
                atom sound_atom = null;
                foreach (mob potential_hearer in Dm.each<mob>((card.loc as atom)?.contents)) //If inside a container with other mobs (e.g. locker)
                {
                    if (potential_hearer.client == null || (!Dm.truthy(Dm.bitand(get_chat_toggles(potential_hearer.client), CHAT_BANKCARD)) && !Dm.truthy(force)))
                        continue;
                    if (sound_atom == null)
                        sound_atom = card.drop_location() as atom; //in case we're inside a bodybag in a crate or something. doing this here to only process it if there's a valid mob who can hear the sound.
                    if (!HAS_TRAIT(potential_hearer, TRAIT_DEAF))
                    {
                        potential_hearer.playsound_local(get_turf(sound_atom), "sound/machines/beep/twobeep_high.ogg", 50, TRUE);
                        to_chat(potential_hearer, span_notice(Dm.str(message)));
                    }
                }
            }
        }
    }

    /**
     * Returns a string with the civilian bounty's description on it.
     */
    // tg: code/modules/economy/account.dm:291
    public virtual object bounty_text()
    {
        if (civilian_bounty == null)
            return FALSE;
        return civilian_bounty.description;
    }

    /**
     * Returns the required item count, or required chemical units required to submit a bounty.
     */
    // tg: code/modules/economy/account.dm:300
    public virtual object bounty_num()
    {
        object required = civilian_bounty?.print_required();
        return Dm.truthy(required) ? required : "N/A";
    }

    /**
     * Produces the value of the account's civilian bounty reward, if able.
     */
    // tg: code/modules/economy/account.dm:306
    public virtual object bounty_value()
    {
        object value = civilian_bounty?.get_bounty_reward();
        return Dm.truthy(value) ? value : 0;
    }

    // tg: code/modules/economy/account.dm:309
    public virtual void set_bounty(datum_bounty new_bounty = null, obj_item id_card = null)
    {
        if (civilian_bounty != null)
            reset_bounty(id_card);

        civilian_bounty = new_bounty;
        civilian_bounty.on_selected(id_card as obj_item_card_id);
    }

    /**
     * Performs house-cleaning on variables when a civilian bounty is replaced, or, when a bounty is claimed.
     */
    // tg: code/modules/economy/account.dm:319
    public virtual void reset_bounty(obj_item id_card = null)
    {
        if (civilian_bounty != null)
        {
            civilian_bounty.on_reset(id_card as obj_item_card_id);
            civilian_bounty = null;
        }

        bounty_timer = 0; // COOLDOWN_RESET (__DEFINES/cooldowns.dm), inline (rules 11.1)
    }

    /**
     * Add log to transactions history. Deletes the oldest log when the history has more than 20 entries.
     * Main format: Category: Reason in Reason. Example: Vending: Machinery Using
     * Arguments:
     * * adjusted_money - How much was added, negative values removing cash.
     * * reason - The reason of interact with balance, for example, "Bought chips" or "Payday".
     */
    // tg: code/modules/economy/account.dm:364
    public virtual void add_log_to_history(object adjusted_money = null, object reason = null)
    {
        if (LAZYLEN(transaction_history) >= 20)
            transaction_history.Cut(1, 2);

        LAZYADD(ref transaction_history, Dm.list(Dm.list(
            Dm.kv("adjusted_money", adjusted_money),
            Dm.kv("reason", reason))));
    }
}

// tg: code/modules/economy/account.dm:326
public partial class datum_bank_account_department : datum_bank_account
{
    // tg: code/modules/economy/account.dm:331
    [DmParams("dep_id", "budget", "player_account")]
    public override async DmTask New(params object[] __extra)
    {
        object dep_id = Dm.arg(__extra, 0);
        object budget = Dm.arg(__extra, 1);
        department_id = Dm.str(dep_id);
        account_balance = Dm.num(budget);
        account_holder = Dm.str(SSeconomy.department_accounts[dep_id]);
        Dm.append(ref SSeconomy.departmental_accounts, this);
    }

    // tg: code/modules/economy/account.dm:337
    public override object adjust_money(object amount = null, object reason = null)
    {
        object dot = base.adjust_money(amount, reason);

        SSblackbox.record_feedback("amount", Dm.text("[]_balance", department_id), account_balance, world.time); //Provides the cargo balance alongside a timestamp for comparison afterwards.
        if (!Dm.eq(department_id, ACCOUNT_CAR))
            return dot;

        // If we're under (or equal) 3 crates woth of money (600?) in the cargo department, we unlock the scrapheap, which gives us a buncha money. Useful in an emergency?
        if (account_balance >= CARGO_CRATE_VALUE * 3)
            return dot;
        // We only allow people to actually buy the shuttle once the round gets going - otherwise you'd just be able to do it roundstart (Not really intended)
        float minimum_allowed_purchase_time = Dm.num(config.Get(typeof(datum_config_entry_number_shuttle_refuel_delay))) * 0.6f;
        if ((world.time - Dm.num(SSticker.round_start_time)) > minimum_allowed_purchase_time)
            SSshuttle.shuttle_purchase_requirements_met[SHUTTLE_UNLOCK_SCRAPHEAP] = TRUE;
        else
            SSshuttle.shuttle_purchase_requirements_met[SHUTTLE_UNLOCK_SCRAPHEAP] = FALSE;
        return dot;
    }
}
