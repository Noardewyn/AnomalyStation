// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/computer/accounting.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P4a (D327): the whole file: the account lookup console with its AccountingConsole window: the crew's accounts, paycheck advances
// and pay modifiers, and the economy's audit log. The var block is generated (D111).

// tg: code/game/machinery/computer/accounting.dm:5
public partial class obj_machinery_computer_accounting : obj_machinery_computer
{
    // tg: code/game/machinery/computer/accounting.dm:1-3  (file-local, #undef at :79-81)
    private const int MAX_ADVANCES = 3;
    private const float MIN_PAY_MOD = 0.5f;
    private const float MAX_PAY_MOD = 1.5f;

    // tg: code/game/machinery/computer/accounting.dm:46  var/static/ian_format (a proc-level static, rules 10.6)
    private static ref object ui_static_data__ian_format => ref TgState.Current.obj_machinery_computer_accounting__ui_static_data__ian_format;

    // tg: code/game/machinery/computer/accounting.dm:14
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        object dot = await base.ui_interact(user, ui, __extra);
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "AccountingConsole", name);
            await ui.open();
        }
        return dot;
    }

    // tg: code/game/machinery/computer/accounting.dm:21
    public override async DmTask<object> ui_data(mob user = null)
    {
        await base.ui_data(user);
        DmList data = Dm.list();
        DmList player_accounts = Dm.list();

        foreach (object id in Dm.each(SSeconomy.bank_accounts_by_id))
        {
            var current_bank_account = SSeconomy.bank_accounts_by_id[id] as datum_bank_account;
            if (!Dm.truthy(Dm.bitand(current_bank_account.account_job?.job_flags, JOB_CREW_MANIFEST)))
                continue;
            Dm.append(ref player_accounts, Dm.list(Dm.list(
                Dm.kv("name", current_bank_account.account_holder),
                Dm.kv("job", current_bank_account.account_job.title),
                Dm.kv("balance", round(current_bank_account.account_balance)),
                Dm.kv("modifier", current_bank_account.payday_modifier),
                Dm.kv("num_advances", current_bank_account.paydays_to_skip),
                Dm.kv("id", id))));
        }
        data["accounts"] = player_accounts;
        data["audit_log"] = SSeconomy.audit_log;
        data["crashing"] = HAS_TRAIT(SSeconomy, TRAIT_MARKET_CRASHING) ? TRUE : FALSE;
        data["station_time"] = round_timestamp("hh:mm");
        return data;
    }

    // tg: code/game/machinery/computer/accounting.dm:44
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();
        ui_static_data__ian_format ??= pick("png", "jpg", "jpeg", "webp", "bmp");
        data["pic_file_format"] = ui_static_data__ian_format;
        data["young_ian"] = check_holidays(IAN_HOLIDAY);
        data["max_advances"] = MAX_ADVANCES;
        data["max_pay_mod"] = MAX_PAY_MOD;
        data["min_pay_mod"] = MIN_PAY_MOD;
        return data;
    }

    // tg: code/game/machinery/computer/accounting.dm:54
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        playsound(this, SFX_TERMINAL_TYPE, 50, FALSE);
        var bank_account = SSeconomy.bank_accounts_by_id[@params["account_id"]] as datum_bank_account;
        if (bank_account == null || !Dm.truthy(Dm.bitand(bank_account.account_job?.job_flags, JOB_CREW_MANIFEST)))
            return dot;

        switch (Dm.str(action))
        {
            case "paycheck_advance":
                if (bank_account.paydays_to_skip < MAX_ADVANCES)
                {
                    await bank_account.payday(1, @event: I18n.T("Paycheck advance"));
                    bank_account.paydays_to_skip += 1;
                }
                return TRUE;
            case "change_pay_mod":
            {
                object old_modifier = bank_account.payday_modifier;
                bank_account.payday_modifier = clamp(round(Dm.num(text2num(@params["pay_mod"])), 0.05f), MIN_PAY_MOD, MAX_PAY_MOD);
                float new_check_total = Dm.num(bank_account.payday_modifier) * Dm.num(bank_account.account_job.paycheck);
                string raise_or_cut = new_check_total > Dm.num(old_modifier) * Dm.num(bank_account.account_job.paycheck) ? I18n.T("raised") : I18n.T("cut");
                await bank_account.bank_card_talk(I18n.T("Paycheck [] to [][].", raise_or_cut, new_check_total, MONEY_SYMBOL), force: TRUE);
                SSeconomy.add_audit_entry(bank_account, new_check_total, I18n.T("Paycheck []", raise_or_cut));
                return TRUE;
            }
        }
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/game/machinery/computer/accounting.dm:46  var/static/ian_format
    internal object obj_machinery_computer_accounting__ui_static_data__ian_format;
}
