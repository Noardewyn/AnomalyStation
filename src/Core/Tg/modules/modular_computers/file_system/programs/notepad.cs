// Translated from tgstation (snapshot 1d99bd7f): code/modules/modular_computers/file_system/programs/notepad.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M9 slice E (NtOS's notepad): the Notepad app (notepad.dm:1-120), its text files (an inserted computer disk's too, syndicate G3d)
// and its NtosNotepad window, and its circuit component (:122-156) that reads and writes the note from a circuit plugged into
// the computer. The var block is generated (D111).

// tg: code/modules/modular_computers/file_system/programs/notepad.dm:1
public partial class datum_computer_file_program_notepad : datum_computer_file_program
{
    // tg: code/modules/modular_computers/file_system/programs/notepad.dm:26
    public virtual obj_item_disk_computer get_target_disk(object opened_on_disk = null)
    {
        if (Dm.truthy(opened_on_disk) && computer?.inserted_disk is obj_item_disk_computer disk)
            return disk;
        return null;
    }

    // tg: code/modules/modular_computers/file_system/programs/notepad.dm:31
    public virtual datum_computer_file_data_text find_text_file(object uid = null, object opened_on_disk = null, object name = null)
    {
        obj_item_disk_computer target_disk = get_target_disk(opened_on_disk);
        datum_computer_file_data_text file = null;
        if (Dm.truthy(uid))
            file = computer.find_file_by_uid(uid, target_disk) as datum_computer_file_data_text;
        if (file == null && Dm.length(name) > 0)
            file = computer.find_file_by_full_name(Dm.text("[].TXT", name), target_disk) as datum_computer_file_data_text;
        return file;
    }

    // tg: code/modules/modular_computers/file_system/programs/notepad.dm:40
    public virtual void load_text_file(datum_computer_file_data_text file = null)
    {
        written_note = file.stored_text;
        opened_file_uid = file.uid;
        opened_file_on_disk = file.disk_host != null ? TRUE : FALSE;
        opened_file_name = file.filename;
    }

    // tg: code/modules/modular_computers/file_system/programs/notepad.dm:46
    public virtual void save_text_file(mob user = null, object name = null, object extension = null, object note = null, object uid = null, object opened_on_disk = null)
    {
        obj_item_disk_computer target_disk = get_target_disk(opened_on_disk);
        datum_computer_file_data_text file = find_text_file(uid, opened_on_disk, name);
        if (file != null)
        {
            file.stored_text = Dm.str(note);
            file.calculate_size();
            load_text_file(file);
            return;
        }

        datum_computer_file_data_text new_file = create_text_file(extension);
        new_file.filename = Dm.str(name);
        new_file.stored_text = Dm.str(note);
        new_file.calculate_size();
        object file_stored = target_disk != null ? target_disk.add_file(new_file) : computer.store_file(new_file, user);
        if (!Dm.truthy(file_stored))
        {
            to_chat(user, span_warning(I18n.T("Error occured while saving the file!")));
            qdel(new_file);
            return;
        }

        load_text_file(new_file);
    }

    // tg: code/modules/modular_computers/file_system/programs/notepad.dm:67
    public virtual datum_computer_file_data_text create_text_file(object extension = null)
    {
        if (Dm.eq(extension, "LOG"))
            return Dm.@new<datum_computer_file_data_text_logfile>();
        return Dm.@new<datum_computer_file_data_text>();
    }

    // tg: code/modules/modular_computers/file_system/programs/notepad.dm:72
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        await base.ui_act(action, @params, ui, state);
        mob user = ui.user;
        if (Dm.eq(action, "UpdateNote"))
        {
            written_note = Dm.str(@params["newnote"]);
            return TRUE;
        }
        if (Dm.eq(action, "SaveNote"))
        {
            written_note = Dm.truthy(@params["newnote"]) ? Dm.str(@params["newnote"]) : written_note;
            save_text_file(user, opened_file_name, null, written_note, opened_file_uid, opened_file_on_disk);
            return TRUE;
        }
        if (Dm.eq(action, "Save"))
        {
            written_note = Dm.truthy(@params["note"]) ? Dm.str(@params["note"]) : written_note;
            save_text_file(user, opened_file_name, null, written_note, opened_file_uid, opened_file_on_disk);
            return TRUE;
        }
        if (Dm.eq(action, "SaveAs"))
        {
            object new_name = trim(@params["name"], MAX_MESSAGE_LEN);
            object extension = @params["extension"];
            if (Dm.length(new_name) == 0 || !Dm.truthy(filter_filename_pda(new_name)))
                return null;
            written_note = Dm.truthy(@params["note"]) ? Dm.str(@params["note"]) : written_note;
            save_text_file(user, new_name, extension, written_note, @params["uid"], opened_file_on_disk);
            return TRUE;
        }
        if (Dm.eq(action, "Open"))
        {
            datum_computer_file_data_text file = find_text_file(@params["uid"], @params["onDisk"], @params["name"]);
            if (file == null)
                return null;
            load_text_file(file);
            return TRUE;
        }
        return null;
    }

    // tg: code/modules/modular_computers/file_system/programs/notepad.dm:102
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();

        data["note"] = written_note;
        data["documentName"] = opened_file_name;
        data["files"] = Dm.list();
        if (computer != null)
        {
            foreach (datum_computer_file file in Dm.each<datum_computer_file>(computer.get_files(TRUE)))
            {
                if (!(file is datum_computer_file_data_text))
                    continue;
                data["files"] = Dm.add(data["files"], Dm.list(Dm.list(
                    Dm.kv("uid", file.uid),
                    Dm.kv("name", file.filename),
                    Dm.kv("filetype", file.filetype),
                    Dm.kv("onDisk", file.disk_host != null ? TRUE : FALSE),
                    Dm.kv("displayName", Dm.text("[].[]", file.filename, file.filetype)))));
            }
        }

        return data;
    }
}

// tg: code/modules/modular_computers/file_system/programs/notepad.dm:122
public partial class obj_item_circuit_component_mod_program_notepad : obj_item_circuit_component_mod_program
{
    // tg: code/modules/modular_computers/file_system/programs/notepad.dm:131
    public override async DmTask populate_ports()
    {
        await base.populate_ports();
        set_text = add_input_port("Set Notes", PORT_TYPE_STRING) as datum_port_input;
        updated_text = add_output_port("Notes", PORT_TYPE_STRING) as datum_port_output;
        updated = add_output_port("Updated", PORT_TYPE_SIGNAL) as datum_port_output;
    }

    // tg: code/modules/modular_computers/file_system/programs/notepad.dm:137
    public override async DmTask register_shell(atom_movable shell = null)
    {
        await base.register_shell(shell);
        RegisterSignal(associated_program as datum, COMSIG_UI_ACT, nameof(on_note_updated));
    }

    // tg: code/modules/modular_computers/file_system/programs/notepad.dm:141
    public override async DmTask unregister_shell(atom_movable shell = null)
    {
        UnregisterSignal(associated_program as datum, COMSIG_UI_ACT);
        await base.unregister_shell(shell);
    }

    // tg: code/modules/modular_computers/file_system/programs/notepad.dm:145
    [SignalHandler]
    public virtual void on_note_updated(datum source = null, mob user = null, object action = null, DmList @params = null)
    {
        if (Dm.eq(action, "UpdateNote"))
        {
            updated_text.set_output(@params["newnote"]);
            updated.set_output(COMPONENT_SIGNAL);
        }
    }

    // tg: code/modules/modular_computers/file_system/programs/notepad.dm:151
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        datum_computer_file_program_notepad pad = associated_program as datum_computer_file_program_notepad;
        pad.written_note = Dm.str(set_text.value);
        SStgui.update_uis(pad.computer);
        updated_text.set_output(pad.written_note);
        updated.set_output(COMPONENT_SIGNAL);
        return null;
    }
}
