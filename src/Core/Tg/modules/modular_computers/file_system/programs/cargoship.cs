// Translated from tgstation (snapshot 1d99bd7f): code/modules/modular_computers/file_system/programs/cargoship.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P4d (D298): the whole file, the GrandArk Exporter app (its NtosShipping window): barcodes printed for an account and its cut.
// The split's BYOND input() is tgui's number prompt (DV419, as DV217). The var block is generated (D111).

// tg: code/modules/modular_computers/file_system/programs/cargoship.dm:1
public partial class datum_computer_file_program_shipping : datum_computer_file_program
{
    // tg: code/modules/modular_computers/file_system/programs/cargoship.dm:19
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();

        data["has_id_slot"] = computer.stored_id != null ? TRUE : FALSE;
        data["paperamt"] = Dm.text("[] / []", computer.stored_paper, computer.max_paper);
        data["card_owner"] = (object)computer.stored_id ?? I18n.T("No Card Inserted.");
        data["current_user"] = payments_acc != null ? payments_acc.account_holder : null;
        data["barcode_split"] = cut_multiplier * 100;
        return data;
    }

    // tg: code/modules/modular_computers/file_system/programs/cargoship.dm:29
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (computer.stored_id == null) //We need an ID to successfully run
            return FALSE;

        switch (Dm.str(action))
        {
            case "ejectid":
                await computer.remove_id(usr);
                break;
            case "selectid":
                if (computer.stored_id.registered_account == null)
                {
                    playsound(get_turf(computer.ui_host() as atom), Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 50, TRUE, -1);
                    return TRUE;
                }
                payments_acc = computer.stored_id.registered_account;
                playsound(get_turf(computer.ui_host() as atom), Dm.resource("sound/machines/ping.ogg"), 50, TRUE, -1);
                break;
            case "resetid":
                payments_acc = null;
                break;
            case "setsplit":
            {
                // DEVIATION(DV419): tg's input() as num|null is tgui's number prompt (as DV217)
                object potential_cut = await tgui_input_number(usr, I18n.T("How much would you like to pay out to the registered card?"), I18n.T("Percentage Profit ([]% - []%)", round(cut_min * 100), round(cut_max * 100)), cut_multiplier * 100, cut_max * 100, cut_min * 100);
                cut_multiplier = Dm.truthy(potential_cut) ? clamp(round(Dm.num(potential_cut) / 100, cut_min), cut_min, cut_max) : Dm.num(Dm.initial(this, nameof(cut_multiplier)));
                break;
            }
            case "print":
            {
                if (computer.stored_paper <= 0)
                {
                    to_chat(usr, span_notice(I18n.T("Printer is out of paper.")));
                    return TRUE;
                }
                if (payments_acc == null)
                {
                    to_chat(usr, span_notice(I18n.T("Software error: Please set a current user first.")));
                    return TRUE;
                }
                var barcode = Dm.@new<obj_item_barcode>(get_turf(computer.ui_host() as atom));
                barcode.payments_acc = payments_acc;
                barcode.cut_multiplier = cut_multiplier;
                computer.stored_paper--;
                to_chat(usr, span_notice(I18n.T("The computer prints out a barcode.")));
                break;
            }
        }
        return dot;
    }
}
