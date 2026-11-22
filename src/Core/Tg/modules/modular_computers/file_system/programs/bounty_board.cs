// Translated from tgstation (snapshot 1d99bd7f): code/modules/modular_computers/file_system/programs/bounty_board.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI6b (D326): the whole file, the Bounty Board Request Network app (its NtosBountyBoard window): the station requests the
// newscaster's bounty board shares, posted, applied for and paid between bank accounts. The var block is generated (D111).

// tg: code/modules/modular_computers/file_system/programs/bounty_board.dm:1
public partial class datum_computer_file_program_bounty_board : datum_computer_file_program
{
    // tg: code/modules/modular_computers/file_system/programs/bounty_board.dm:21
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        DmList formatted_requests = Dm.list();
        DmList formatted_applicants = Dm.list();
        var user_data = Dm.list();
        data["user"] = user_data;
        if (current_user != null)
        {
            user_data["name"] = current_user.account_holder;
            if (current_user.account_job != null)
            {
                user_data["job"] = current_user.account_job.title;
                user_data["department"] = current_user.account_job.paycheck_department;
            }
            else
            {
                user_data["job"] = "No Job";
                user_data["department"] = DEPARTMENT_UNASSIGNED;
            }
        }
        else
        {
            user_data["name"] = user.name;
            user_data["job"] = "N/A";
            user_data["department"] = "N/A";
        }
        if (!Dm.truthy(networked))
        {
            Dm.append(ref GLOB.allbountyboards, computer);
            networked = TRUE;
        }
        if (computer.stored_id != null)
            current_user = computer.stored_id?.registered_account;
        foreach (object i in Dm.each(GLOB.request_list))
        {
            if (!Dm.truthy(i))
                continue;
            var request = (datum_station_request)i;
            Dm.append(ref formatted_requests, Dm.list(Dm.list(Dm.kv("owner", request.owner), Dm.kv("value", request.value), Dm.kv("description", request.description), Dm.kv("acc_number", request.req_number))));
            if (request.applicants != null)
            {
                foreach (datum_bank_account j in Dm.each<datum_bank_account>(request.applicants))
                    Dm.append(ref formatted_applicants, Dm.list(Dm.list(Dm.kv("name", j.account_holder), Dm.kv("request_id", request.owner_account.account_id), Dm.kv("requestee_id", j.account_id))));
            }
        }
        if (current_user != null)
            data["accountName"] = current_user.account_holder;
        data["requests"] = formatted_requests;
        data["applicants"] = formatted_applicants;
        data["bountyValue"] = bounty_value;
        data["bountyText"] = bounty_text;
        return data;
    }

    // tg: code/modules/modular_computers/file_system/programs/bounty_board.dm:60
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        await base.ui_act(action, @params, ui, state);
        object current_ref_num = @params["request"];
        object current_app_num = @params["applicant"];
        datum_bank_account request_target = null;
        if (Dm.truthy(current_ref_num))
        {
            foreach (datum_station_request i in Dm.each<datum_station_request>(GLOB.request_list))
            {
                if (Dm.text("[]", i.req_number) == Dm.text("[]", current_ref_num))
                {
                    active_request = i;
                    break;
                }
            }
        }
        if (active_request != null)
        {
            foreach (datum_bank_account j in Dm.each<datum_bank_account>(active_request.applicants))
            {
                if (Dm.text("[]", j.account_id) == Dm.text("[]", current_app_num))
                {
                    request_target = j;
                    break;
                }
            }
        }
        switch (Dm.str(action))
        {
            case "createBounty":
            {
                if (current_user == null || !Dm.truthy(bounty_text))
                {
                    // playsound(src, 'sound/machines/buzz/buzz-sigh.ogg', 20, TRUE): src is the program, which has no turf: tg plays nothing
                    return TRUE;
                }
                foreach (datum_station_request i in Dm.each<datum_station_request>(GLOB.request_list))
                {
                    if (Dm.text("[]", i.req_number) == Dm.text("[]", current_user.account_id))
                    {
                        await computer.say(I18n.T("Account already has active bounty."));
                        return TRUE;
                    }
                }
                var curr_request = Dm.@new<datum_station_request>(current_user.account_holder, bounty_value, bounty_text, current_user.account_id, current_user);
                Dm.append(ref GLOB.request_list, Dm.list(curr_request));
                foreach (obj i in Dm.each<obj>(GLOB.allbountyboards))
                {
                    await i.say(I18n.T("New bounty has been added!"));
                    playsound(i.loc, Dm.resource("sound/effects/cashregister.ogg"), 30, TRUE);
                }
                return TRUE;
            }
            case "apply":
                if (current_user == null)
                {
                    await computer.say(I18n.T("Please swipe a valid ID first."));
                    return TRUE;
                }
                if (Dm.eq(current_user.account_holder, active_request.owner))
                {
                    playsound(computer, Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 20, TRUE);
                    return TRUE;
                }
                Dm.append(ref active_request.applicants, Dm.list(current_user));
                break;
            case "payApplicant":
                if (current_user == null)
                    return null;
                if (!current_user.has_money(active_request.value) || !Dm.eq(current_user.account_holder, active_request.owner))
                {
                    playsound(computer, Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 30, TRUE);
                    return null;
                }
                request_target.transfer_money(current_user, active_request.value, I18n.T("Bounties: Request Completed"));
                await computer.say(I18n.T("Paid out [] [].", active_request.value, MONEY_NAME));
                GLOB.request_list.Remove(active_request);
                return TRUE;
            case "clear":
                if (current_user != null)
                {
                    current_user = null;
                    await computer.say(I18n.T("Account Reset."));
                    return TRUE;
                }
                break;
            case "deleteRequest":
                if (current_user == null)
                {
                    playsound(computer, Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 20, TRUE);
                    return TRUE;
                }
                if (!Dm.eq(active_request.owner, current_user.account_holder))
                {
                    playsound(computer, Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 20, TRUE);
                    return TRUE;
                }
                await computer.say(I18n.T("Deleted current request."));
                GLOB.request_list.Remove(active_request);
                return TRUE;
            case "bountyVal":
                bounty_value = Dm.num(text2num(@params["bountyval"]));
                if (!Dm.truthy(bounty_value))
                    bounty_value = 1;
                return TRUE;
            case "bountyText":
                bounty_text = Dm.str(@params["bountytext"]);
                break;
        }
        return TRUE;
    }
}
