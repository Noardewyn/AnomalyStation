// Translated from tgstation (snapshot 1d99bd7f): code/modules/modular_computers/file_system/programs/antagonist/contractor_program.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign X1a (D316): the Syndicate Contractor Uplink app of the contract kit's tablet, whole (its SyndicateContractor
// window is the client's). The var block is generated (D111).

// tg: code/modules/modular_computers/file_system/programs/antagonist/contractor_program.dm:1
public partial class datum_computer_file_program_contract_uplink : datum_computer_file_program
{
    // tg: code/modules/modular_computers/file_system/programs/antagonist/contractor_program.dm:23
    public override datum_computer_file clone(object rename = null)
    {
        var temp = base.clone(rename) as datum_computer_file_program_contract_uplink;
        temp.traitor_data = traitor_data;
        return temp;
    }

    // tg: code/modules/modular_computers/file_system/programs/antagonist/contractor_program.dm:28
    public override object Destroy(float? force = null)
    {
        traitor_data = null;
        return base.Destroy(force);
    }

    // tg: code/modules/modular_computers/file_system/programs/antagonist/contractor_program.dm:32
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        var user = ui.user as mob_living; // usr

        switch (Dm.str(action))
        {
            case "PRG_contract-accept":
            {
                int contract_id = (int)Dm.num(text2num(@params["contract_id"]));
                datum_contractor_hub hub = traitor_data.uplink_handler.contractor_hub;
                ((datum_syndicate_contract)hub.assigned_contracts[contract_id]).status = CONTRACT_STATUS_ACTIVE;
                hub.current_contract = hub.assigned_contracts[contract_id] as datum_syndicate_contract;
                program_open_overlay = "contractor-contract";
                return TRUE;
            }

            case "PRG_login":
            {
                var traitor_user = user.mind.has_antag_datum(typeof(datum_antagonist_traitor)) as datum_antagonist_traitor;
                if (traitor_user == null)
                {
                    error = "UNAUTHORIZED USER";
                    return TRUE;
                }

                traitor_data = traitor_user;
                if (traitor_data.uplink_handler.contractor_hub == null)
                {
                    traitor_data.uplink_handler.contractor_hub = await Dm.new_async(typeof(datum_contractor_hub)) as datum_contractor_hub;
                    await traitor_data.uplink_handler.contractor_hub.create_contracts(traitor_user.owner);
                    user.playsound_local(user, "sound/music/antag/contractstartup.ogg", 100, FALSE);
                    program_open_overlay = "contractor-contractlist";
                }
                return TRUE;
            }

            case "PRG_call_extraction":
            {
                datum_syndicate_contract current = traitor_data.uplink_handler.contractor_hub.current_contract;
                if (!Dm.eq(current.status, CONTRACT_STATUS_EXTRACTING))
                {
                    if (Dm.truthy(await current.handle_extraction(user)))
                    {
                        user.playsound_local(user, "sound/effects/confirmdropoff.ogg", 100, TRUE);
                        current.status = CONTRACT_STATUS_EXTRACTING;

                        program_open_overlay = "contractor-extracted";
                    }
                    else
                    {
                        user.playsound_local(user, "sound/machines/uplink/uplinkerror.ogg", 50);
                        error = "Either both you or your target aren't at the dropoff location, or the pod hasn't got a valid place to land. Clear space, or make sure you're both inside.";
                    }
                }
                else
                {
                    user.playsound_local(user, "sound/machines/uplink/uplinkerror.ogg", 50);
                    error = "Already extracting... Place the target into the pod. If the pod was destroyed, this contract is no longer possible.";
                }

                return TRUE;
            }

            case "PRG_contract_abort":
            {
                datum_contractor_hub hub = traitor_data.uplink_handler.contractor_hub;
                int contract_id = (int)Dm.num(hub.current_contract.id);

                hub.current_contract = null;
                ((datum_syndicate_contract)hub.assigned_contracts[contract_id]).status = CONTRACT_STATUS_ABORTED;

                program_open_overlay = "contractor-contractlist";

                return TRUE;
            }

            case "PRG_redeem_TC":
            {
                datum_contractor_hub hub = traitor_data.uplink_handler.contractor_hub;
                if (Dm.truthy(hub.contract_TC_to_redeem))
                {
                    var crystals = Dm.@new<obj_item_stack_telecrystal>(get_turf(user), hub.contract_TC_to_redeem);
                    if (user is mob_living_carbon_human H)
                    {
                        if (Dm.truthy(await H.put_in_hands(crystals)))
                            to_chat(H, span_notice(I18n.T("Your payment materializes into your hands!")));
                        else
                            to_chat(user, span_notice(I18n.T("Your payment materializes onto the floor.")));
                    }

                    hub.contract_TC_payed_out += hub.contract_TC_to_redeem;
                    hub.contract_TC_to_redeem = 0;
                    return TRUE;
                }
                else
                {
                    user.playsound_local(user, "sound/machines/uplink/uplinkerror.ogg", 50);
                }
                return TRUE;
            }

            case "PRG_clear_error":
                error = "";
                return TRUE;

            case "PRG_set_first_load_finished":
                first_load = FALSE;
                return TRUE;

            case "PRG_toggle_info":
                info_screen = Dm.truthy(info_screen) ? FALSE : TRUE;
                return TRUE;
        }
        return null;
    }

    // tg: code/modules/modular_computers/file_system/programs/antagonist/contractor_program.dm:111
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();

        data["first_load"] = first_load;
        data["logged_in"] = traitor_data != null ? TRUE : FALSE;
        data["station_name"] = GLOB.station_name;
        data["info_screen"] = info_screen;
        data["error"] = error;

        if (traitor_data == null)
        {
            data["ongoing_contract"] = FALSE;
            data["extraction_enroute"] = FALSE;
            update_computer_icon();
            return data;
        }

        datum_contractor_hub hub = traitor_data.uplink_handler.contractor_hub;
        data["ongoing_contract"] = hub.current_contract != null ? TRUE : FALSE;
        if (hub.current_contract != null)
        {
            program_open_overlay = "contractor-contract";
            if (Dm.eq(hub.current_contract.status, CONTRACT_STATUS_EXTRACTING))
            {
                data["extraction_enroute"] = TRUE;
                program_open_overlay = "contractor-extracted";
            }
            else
            {
                data["extraction_enroute"] = FALSE;
            }
            var curr = get_turf(user) as turf;
            turf dropoff_turf = null;
            data["current_location"] = Dm.text("[]", get_area_name(curr, TRUE));
            foreach (object content in Dm.each(hub.current_contract.contract.dropoff.contents))
            {
                if (content is turf found)
                {
                    dropoff_turf = found;
                    break;
                }
            }
            object direction;
            if (Dm.eq(curr.z, dropoff_turf.z)) //Direction calculations for same z-level only
            {
                direction = uppertext(dir2text(get_dir(curr, dropoff_turf))); //Direction text (East, etc). Not as precise, but still helpful.
                if (get_area(user) == hub.current_contract.contract.dropoff)
                    direction = "LOCATION CONFIRMED";
            }
            else
            {
                direction = "???";
            }
            data["dropoff_direction"] = direction;
        }
        data["redeemable_tc"] = hub.contract_TC_to_redeem;
        data["earned_tc"] = hub.contract_TC_payed_out;
        data["contracts_completed"] = hub.contracts_completed;
        foreach (datum_syndicate_contract contract in Dm.each_cast<datum_syndicate_contract>(hub.assigned_contracts))
        {
            if (contract.contract == null)
            {
                stack_trace(Dm.text("Syndiate contract with null contract objective found in []'s contractor hub!", traitor_data.owner), "code/modules/modular_computers/file_system/programs/antagonist/contractor_program.dm", 154);
                contract.status = CONTRACT_STATUS_ABORTED;
                continue;
            }
            data["contracts"] = Dm.add(data["contracts"], Dm.list(Dm.list(
                Dm.kv("target", contract.contract.target),
                Dm.kv("target_rank", contract.target_rank),
                Dm.kv("payout", contract.contract.payout),
                Dm.kv("payout_bonus", contract.contract.payout_bonus),
                Dm.kv("dropoff", contract.contract.dropoff),
                Dm.kv("id", contract.id),
                Dm.kv("status", contract.status),
                Dm.kv("message", contract.wanted_message))));
        }

        update_computer_icon();
        return data;
    }
}
