// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/computer/records/medical.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps SC2, D333): the medical records console and its laptop in tgui's MedicalRecords window: the crew's medical
// records with their notes, the physical and mental statuses, the cause of death, the blood type, the disabilities and quirks, and
// a new record from a photo. The var blocks are generated (D111).

// tg: code/game/machinery/computer/records/medical.dm:1
public partial class obj_machinery_computer_records_medical : obj_machinery_computer_records
{
    // tg: code/game/machinery/computer/records/medical.dm:25
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!(tool is obj_item_photo))
            return NONE;
        await insert_new_record(user, (obj_item_photo)tool);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/computer/records/medical.dm:31
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        object dot = await base.ui_interact(user, ui, __extra);
        if (Dm.truthy(dot))
            return dot;
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "MedicalRecords");
            ui.set_autoupdate(FALSE);
            await ui.open();
        }
        return dot;
    }

    // tg: code/game/machinery/computer/records/medical.dm:41
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = await base.ui_data(user) as DmList;

        DmList records = Dm.list();
        foreach (datum_record_crew target in Dm.each<datum_record_crew>(GLOB.manifest.general))
        {
            DmList notes = Dm.list();
            foreach (datum_medical_note note in Dm.each<datum_medical_note>(target.medical_notes))
            {
                Dm.append(ref notes, Dm.list(Dm.list(
                    Dm.kv("author", note.author),
                    Dm.kv("content", note.content),
                    Dm.kv("note_ref", REF(note)),
                    Dm.kv("time", note.time))));
            }

            Dm.append(ref records, Dm.list(Dm.list(
                Dm.kv("age", target.age),
                // initial(target.blood_type:name): the blood type's datum or path; the text name a manifest record holds gives null,
                // as BYOND's initial() of a var of a primitive does (OpenDream DMOpcodeHandlers.Initial, InitialVarOnPrimitiveException off)
                Dm.kv("blood_type", target.blood_type is datum or Type ? Dm.initial(target.blood_type, "name") : null),
                Dm.kv("crew_ref", REF(target)),
                Dm.kv("dna", target.dna_string),
                Dm.kv("gender", target.gender),
                Dm.kv("major_disabilities", target.major_disabilities_desc),
                Dm.kv("minor_disabilities", target.minor_disabilities_desc),
                Dm.kv("physical_status", target.physical_status),
                Dm.kv("cause_of_death", target.cause_of_death),
                Dm.kv("mental_status", target.mental_status),
                Dm.kv("name", target.name),
                Dm.kv("notes", notes),
                Dm.kv("quirk_notes", target.quirk_notes),
                Dm.kv("rank", target.rank),
                Dm.kv("species", target.species),
                Dm.kv("trim", target.trim))));
        }

        data["records"] = records;

        return data;
    }

    // tg: code/game/machinery/computer/records/medical.dm:78
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();
        DmList blood_type_strings = Dm.list();
        data["min_age"] = AGE_MIN;
        data["max_age"] = AGE_MAX;
        data["physical_statuses"] = PHYSICAL_STATUSES();
        data["mental_statuses"] = MENTAL_STATUSES();
        foreach (Type blood_path in Dm.each_cast<Type>(get_roundstart_blood_types()))
            Dm.append(ref blood_type_strings, Dm.initial(blood_path, (datum_blood_type p) => p.name));
        data["blood_types"] = blood_type_strings;

        return data;
    }

    // tg: code/game/machinery/computer/records/medical.dm:91
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        datum_record_crew target = null;
        if (Dm.truthy(@params["crew_ref"]))
            target = Dm.locate_ref(@params["crew_ref"]) is datum_record_crew crew__1 && Dm.@in(crew__1, GLOB.manifest.general) ? crew__1 : null; // locate(params["crew_ref"]) in GLOB.manifest.general
        if (target == null)
            return FALSE;

        object sw__1 = action;
        if (Dm.eq(sw__1, "add_note"))
        {
            if (!Dm.truthy(@params["content"]))
                return FALSE;
            object content = reject_bad_name(@params["content"], allow_numbers: TRUE, max_length: MAX_MESSAGE_LEN, strict: TRUE, cap_after_symbols: FALSE);
            if (!Dm.truthy(content))
                return FALSE;

            datum_medical_note new_note = Dm.@new<datum_medical_note>(usr.name, content, round_timestamp());
            while (length(target.medical_notes) > 2)
                target.medical_notes.Cut(1, 2);

            Dm.append(ref target.medical_notes, new_note);

            return TRUE;
        }
        else if (Dm.eq(sw__1, "delete_note"))
        {
            datum_medical_note old_note = Dm.locate_ref(@params["note_ref"]) is datum_medical_note note__1 && Dm.@in(note__1, target.medical_notes) ? note__1 : null; // locate(params["note_ref"]) in target.medical_notes
            if (old_note == null)
                return FALSE;

            Dm.remove(ref target.medical_notes, old_note);
            qdel(old_note);

            return TRUE;
        }
        else if (Dm.eq(sw__1, "set_physical_status"))
        {
            object physical_status = @params["physical_status"];
            if (!Dm.truthy(physical_status) || !Dm.@in(physical_status, PHYSICAL_STATUSES()))
                return FALSE;

            target.physical_status = physical_status;
            if (!Dm.eq(physical_status, PHYSICAL_DECEASED))
                target.cause_of_death = null;

            return TRUE;
        }
        else if (Dm.eq(sw__1, "set_mental_status"))
        {
            object mental_status = @params["mental_status"];
            if (!Dm.truthy(mental_status) || !Dm.@in(mental_status, MENTAL_STATUSES()))
                return FALSE;

            target.mental_status = mental_status;

            return TRUE;
        }
        else if (Dm.eq(sw__1, "set_cause_of_death"))
        {
            object death_text = reject_bad_name(@params["cause"], allow_numbers: TRUE, max_length: MAX_DESC_LEN, strict: TRUE, cap_after_symbols: FALSE);
            if (!Dm.truthy(death_text))
                return FALSE;
            target.cause_of_death = death_text;
            return TRUE;
        }
        else if (Dm.eq(sw__1, "set_blood_type"))
        {
            Type chosen_path = null;
            foreach (Type blood_path in Dm.each_cast<Type>(get_roundstart_blood_types()))
            {
                if (Dm.eq(Dm.initial(blood_path, (datum_blood_type p) => p.name), @params["blood_type"]))
                {
                    chosen_path = blood_path;
                    break;
                }
            }
            if (chosen_path == null)
                return FALSE;
            target.blood_type = chosen_path;
            return TRUE;
        }

        return FALSE;
    }

    /// Deletes medical information from a record.
    // tg: code/game/machinery/computer/records/medical.dm:169
    public override object expunge_record_info(datum_record_crew target = null)
    {
        if (target == null)
            return FALSE;

        target.age = 18;
        target.blood_type = random_human_blood_type();
        target.dna_string = "Unknown";
        target.gender = "Unknown";
        target.major_disabilities = "";
        target.major_disabilities_desc = "";
        target.medical_notes.Cut();
        target.minor_disabilities = "";
        target.minor_disabilities_desc = "";
        target.physical_status = "";
        target.mental_status = "";
        target.name = "Unknown";
        target.quirk_notes = "";
        target.rank = "Unknown";
        target.species = "Unknown";
        target.trim = "Unknown";

        return TRUE;
    }
}
