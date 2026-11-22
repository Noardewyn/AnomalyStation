// Translated from tgstation (snapshot 1d99bd7f): code/modules/modular_computers/file_system/programs/nt_pay.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P4a (D327): the NT Pay app (nt_pay.dm:1-117): a transfer to another account by its pay token, the token lookup by name and the
// transaction history, and its NtosPay window. Left out with a comment: the app's circuit component
// (/obj/item/circuit_component/mod_program/nt_pay, :113-186: wiremod, not-translated section 164). The var block is generated (D111).

// tg: code/modules/modular_computers/file_system/programs/nt_pay.dm:8
public partial class datum_computer_file_program_nt_pay : datum_computer_file_program
{
    // tg: code/modules/modular_computers/file_system/programs/nt_pay.dm:1-6  the payment statuses (file-local, #undef at :188-193)
    private const int NT_PAY_STATUS_NO_ACCOUNT = 0;
    private const int NT_PAY_STATUS_DEPT_ACCOUNT = 1;
    private const int NT_PAY_STATUS_INVALID_TOKEN = 2;
    private const int NT_PAY_SATUS_SENDER_IS_RECEIVER = 3;
    private const int NT_PAY_STATUS_INVALID_MONEY = 4;
    private const int NT_PAY_STATUS_SUCCESS = 5;

    // tg: code/modules/modular_computers/file_system/programs/nt_pay.dm:24
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        switch (Dm.str(action))
        {
            case "Transaction":
            {
                object token = @params["token"];
                object money_to_send = @params["amount"];
                await make_payment(token, money_to_send, usr);
                break;
            }
            case "GetPayToken":
            {
                wanted_token = null;
                foreach (object account in Dm.each(SSeconomy.bank_accounts_by_id))
                {
                    var acc = SSeconomy.bank_accounts_by_id[account] as datum_bank_account;
                    if (Dm.eq(acc.account_holder, @params["wanted_name"]))
                    {
                        wanted_token = I18n.T("Token: []", acc.pay_token);
                        break;
                    }
                }
                if (!Dm.truthy(wanted_token))
                    return wanted_token = I18n.T("Account \"[]\" not found.", @params["wanted_name"]);
                break;
            }
        }
        return dot;
    }

    // tg: code/modules/modular_computers/file_system/programs/nt_pay.dm:42
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();

        current_user = (computer.stored_id as obj_item_card_id)?.registered_account;
        if (current_user == null)
            data["name"] = null;
        else
        {
            data["name"] = current_user.account_holder;
            data["owner_token"] = current_user.pay_token;
            data["money"] = current_user.account_balance;
            data["wanted_token"] = wanted_token;
            data["transaction_list"] = current_user.transaction_history ?? Dm.list();
        }

        return data;
    }

    ///Wrapper and signal for the main payment function of this program
    // tg: code/modules/modular_computers/file_system/programs/nt_pay.dm:58
    public virtual async DmTask make_payment(object token = null, object money_to_send = null, mob user = null)
    {
        object payment_result = await _pay(token, money_to_send, user);
        computer.SEND_SIGNAL(COMSIG_MODULAR_COMPUTER_NT_PAY_RESULT, Dm.list(computer, payment_result));
    }

    // tg: code/modules/modular_computers/file_system/programs/nt_pay.dm:62
    public virtual async DmTask<object> _pay(object token = null, object money_to_send = null, mob user = null)
    {
        var user_area = get_area(user) as area;
        if (user_area != null && is_area_virtual(user_area))
        {
            to_chat(user, span_notice(I18n.T("You cannot send virtual money to real accounts.")));
            return NT_PAY_STATUS_NO_ACCOUNT;
        }

        float amount = round(Dm.num(money_to_send));

        if (Dm.@in(current_user, SSeconomy.departmental_accounts)) // IS_DEPARTMENTAL_ACCOUNT (__DEFINES/economy.dm:49), inline (rules 11.1)
        {
            if (user != null)
                to_chat(user, span_notice(I18n.T("The app is unable to withdraw from that card.")));
            return NT_PAY_STATUS_DEPT_ACCOUNT;
        }

        datum_bank_account recipient = null;
        if (!Dm.truthy(token))
        {
            if (user != null)
                to_chat(user, span_notice(I18n.T("You need to enter your transfer target's pay token.")));
            return NT_PAY_STATUS_INVALID_TOKEN;
        }
        if (amount <= 0)
        {
            if (user != null)
                to_chat(user, span_notice(I18n.T("You need to specify how much you're sending.")));
            return NT_PAY_STATUS_INVALID_MONEY;
        }
        if (Dm.eq(token, current_user.pay_token))
        {
            if (user != null)
                to_chat(user, span_notice(I18n.T("You can't send [] to yourself.", MONEY_NAME)));
            return NT_PAY_SATUS_SENDER_IS_RECEIVER;
        }

        foreach (object account in Dm.each(SSeconomy.bank_accounts_by_id))
        {
            var acc = SSeconomy.bank_accounts_by_id[account] as datum_bank_account;
            if (Dm.eq(acc.pay_token, token))
            {
                recipient = acc;
                break;
            }
        }

        if (recipient == null)
        {
            if (user != null)
                to_chat(user, span_notice(I18n.T("The app can't find who you're trying to pay. Did you enter the pay token right?")));
            return NT_PAY_STATUS_INVALID_TOKEN;
        }
        if (!current_user.has_money(amount) || amount < 1)
        {
            await current_user.bank_card_talk(I18n.T("You cannot afford it."));
            return NT_PAY_STATUS_INVALID_MONEY;
        }

        await recipient.bank_card_talk(I18n.T("You received [] [](s). Reason: transfer from []", amount, MONEY_NAME, current_user.account_holder));
        recipient.transfer_money(current_user, amount);
        foreach (obj_item_card_id id_card in Dm.each_cast<obj_item_card_id>(recipient.bank_cards))
            id_card.SEND_SIGNAL(COMSIG_ID_CARD_NTPAY_MONEY_RECEIVED, Dm.list(id_card, computer, amount));

        await current_user.bank_card_talk(I18n.T("You send [] [](s) to []. Now you have [] [](s)", amount, MONEY_NAME, recipient.account_holder, current_user.account_balance, MONEY_NAME));

        return NT_PAY_STATUS_SUCCESS;
    }
}
