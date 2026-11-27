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
// transaction history, and its NtosPay window; and its circuit component (:113-186), which pays from a circuit plugged into the
// computer and reports the payments the inserted ID receives. The var block is generated (D111).

// tg: code/modules/modular_computers/file_system/programs/nt_pay.dm:8
public partial class datum_computer_file_program_nt_pay : datum_computer_file_program
{
    // tg: code/modules/modular_computers/file_system/programs/nt_pay.dm:1-6  the payment statuses (file-local, #undef at :188-193)
    internal const int NT_PAY_STATUS_NO_ACCOUNT = 0;
    internal const int NT_PAY_STATUS_DEPT_ACCOUNT = 1;
    internal const int NT_PAY_STATUS_INVALID_TOKEN = 2;
    internal const int NT_PAY_SATUS_SENDER_IS_RECEIVER = 3;
    internal const int NT_PAY_STATUS_INVALID_MONEY = 4;
    internal const int NT_PAY_STATUS_SUCCESS = 5;

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

// tg: code/modules/modular_computers/file_system/programs/nt_pay.dm:113
public partial class obj_item_circuit_component_mod_program_nt_pay : obj_item_circuit_component_mod_program
{
    // tg: code/modules/modular_computers/file_system/programs/nt_pay.dm:130
    public override async DmTask register_shell(atom_movable shell = null)
    {
        await base.register_shell(shell);
        obj_item_modular_computer modpc = (associated_program as datum_computer_file_program).computer;
        RegisterSignal(modpc, COMSIG_MODULAR_COMPUTER_NT_PAY_RESULT, nameof(on_payment_done));
        RegisterSignal(modpc, COMSIG_MODULAR_COMPUTER_INSERTED_ID, nameof(register_id));
        if (modpc.stored_id != null)
            register_id(inserted_id: modpc.stored_id);
    }

    // tg: code/modules/modular_computers/file_system/programs/nt_pay.dm:138
    public override async DmTask unregister_shell(atom_movable shell = null)
    {
        obj_item_modular_computer modpc = (associated_program as datum_computer_file_program).computer;
        UnregisterSignal(modpc, Dm.list(COMSIG_MODULAR_COMPUTER_NT_PAY_RESULT, COMSIG_MODULAR_COMPUTER_INSERTED_ID));
        if (modpc.stored_id != null)
            UnregisterSignal(modpc.stored_id, Dm.list(COMSIG_ID_CARD_NTPAY_MONEY_RECEIVED, COMSIG_MOVABLE_MOVED));
        await base.unregister_shell(shell);
    }

    // tg: code/modules/modular_computers/file_system/programs/nt_pay.dm:145
    [SignalHandler]
    public virtual void register_id(datum source = null, obj_item_card inserted_id = null, mob user = null)
    {
        RegisterSignal(inserted_id, COMSIG_ID_CARD_NTPAY_MONEY_RECEIVED, nameof(on_payment_received));
        RegisterSignal(inserted_id, COMSIG_MOVABLE_MOVED, nameof(unregister_id));
    }

    // tg: code/modules/modular_computers/file_system/programs/nt_pay.dm:150
    [SignalHandler]
    public virtual void unregister_id(obj_item_card gone = null)
    {
        UnregisterSignal(gone, Dm.list(COMSIG_ID_CARD_NTPAY_MONEY_RECEIVED, COMSIG_MOVABLE_MOVED));
    }

    // tg: code/modules/modular_computers/file_system/programs/nt_pay.dm:154
    public override async DmTask populate_ports()
    {
        await base.populate_ports();
        token_port = add_input_port("Token", PORT_TYPE_STRING) as datum_port_input;
        money_port = add_input_port("Amount", PORT_TYPE_NUMBER) as datum_port_input;
        payment_status = add_output_port("Status", PORT_TYPE_NUMBER) as datum_port_output;
        payment_device = add_output_port("Payment Sender", PORT_TYPE_ATOM) as datum_port_output;
        payment_amount = add_output_port("Received Amount", PORT_TYPE_NUMBER) as datum_port_output;
        payment_received = add_output_port("Received Payment", PORT_TYPE_SIGNAL) as datum_port_output;
    }

    // tg: code/modules/modular_computers/file_system/programs/nt_pay.dm:163
    public override object get_ui_notices()
    {
        object dot = base.get_ui_notices();
        Dm.append(ref dot, create_ui_notice("Outputs require inserted ID", "orange"));
        Dm.append(ref dot, create_ui_notice("NT-Pay Statuses:"));
        Dm.append(ref dot, create_ui_notice(Dm.text("Success - []", datum_computer_file_program_nt_pay.NT_PAY_STATUS_SUCCESS), "green"));
        Dm.append(ref dot, create_ui_notice(Dm.text("Fail (No Account) - []", datum_computer_file_program_nt_pay.NT_PAY_STATUS_NO_ACCOUNT), "red"));
        Dm.append(ref dot, create_ui_notice(Dm.text("Fail (Dept Account) - []", datum_computer_file_program_nt_pay.NT_PAY_STATUS_DEPT_ACCOUNT), "red"));
        Dm.append(ref dot, create_ui_notice(Dm.text("Fail (Invalid Token) - []", datum_computer_file_program_nt_pay.NT_PAY_STATUS_INVALID_TOKEN), "red"));
        Dm.append(ref dot, create_ui_notice(Dm.text("Fail (Sender = Receiver) - []", datum_computer_file_program_nt_pay.NT_PAY_SATUS_SENDER_IS_RECEIVER), "red"));
        Dm.append(ref dot, create_ui_notice(Dm.text("Fail (Invalid Amount) - []", datum_computer_file_program_nt_pay.NT_PAY_STATUS_INVALID_MONEY), "red"));
        return dot;
    }

    // tg: code/modules/modular_computers/file_system/programs/nt_pay.dm:174
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        datum_computer_file_program_nt_pay program = associated_program as datum_computer_file_program_nt_pay;
        Waiting.NoSleep(program.make_payment(token_port.value, money_port.value));
        return null;
    }

    // tg: code/modules/modular_computers/file_system/programs/nt_pay.dm:178
    [SignalHandler]
    public virtual void on_payment_done(datum source = null, object payment_result = null)
    {
        payment_status.set_output(payment_result);
    }

    // tg: code/modules/modular_computers/file_system/programs/nt_pay.dm:182
    [SignalHandler]
    public virtual void on_payment_received(datum source = null, obj_item_modular_computer computer = null, object money_received = null)
    {
        payment_device.set_output(computer);
        payment_amount.set_output(money_received);
        payment_received.set_output(COMPONENT_SIGNAL);
    }
}
