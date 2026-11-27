// Translated from tgstation (snapshot 1d99bd7f): code/modules/modular_computers/file_system/program.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M9 slice E: NtOS): an executable program, its access checks, its start, kill and background, and its hardware
// check, and the unremovable circuit components a program adds to its computer's circuit shell. The var block is generated (D111).

// /program/ files are executable programs that do things.
// tg: code/modules/modular_computers/file_system/program.dm:2
public partial class datum_computer_file_program : datum_computer_file
{
    // tg: code/modules/modular_computers/file_system/program.dm:52
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        if (Dm.truthy(LAZYLEN(run_access)))
            run_access = string_list(run_access) as DmList;
        if (Dm.truthy(LAZYLEN(download_access)))
            download_access = string_list(download_access) as DmList;
        ///We need to ensure that different programs (subtypes mostly) won't try to load in the same circuit comps into the shell or usb port of the modpc.
        if (circuit_comp_type != null && !Dm.eq(Dm.initial(circuit_comp_type, (obj_item_circuit_component_mod_program c) => c.associated_program), type))
            stack_trace(Dm.text("circuit comp type mismatch: [] has circuit comp type \\[[]\\], while \\[[]\\] has associated program \\[[]\\].", type, circuit_comp_type, circuit_comp_type, Dm.initial(circuit_comp_type, (obj_item_circuit_component_mod_program c) => c.associated_program)), "code/modules/modular_computers/file_system/program.dm", 60);
    }

    /**
     * Here we deal with peculiarity of adding unremovable components to the computer shell.
     * It probably doesn't look badass, but it's a decent way of doing it without taining the component with
     * oddities like this.
     */
    // tg: code/modules/modular_computers/file_system/program.dm:67
    [SignalHandler]
    public override void on_install(datum_computer_file source = null, obj_item_modular_computer computer_installing = null, mob user = null)
    {
        base.on_install(source, computer_installing, user);
        if (circuit_comp_type == null || computer.shell == null)
            return;
        if (Dm.locate_type(circuit_comp_type, computer.shell.unremovable_circuit_components) == null)
        {
            obj_item_circuit_component_mod_program comp = Dm.@new(circuit_comp_type) as obj_item_circuit_component_mod_program;
            computer.shell.add_unremovable_circuit_component(comp);
            if (computer.shell.attached_circuit != null)
            {
                Waiting.NoSleep(comp.forceMove(computer.shell.attached_circuit));
                Waiting.NoSleep(computer.shell.attached_circuit.add_component(comp));
            }
        }
    }

    ///Here we deal with killing the associated components instead.
    // tg: code/modules/modular_computers/file_system/program.dm:79
    public override object Destroy(float? force = null)
    {
        if (circuit_comp_type == null || computer?.shell == null)
            return base.Destroy(force);
        foreach (obj_item_circuit_component_mod_program comp in Dm.each<obj_item_circuit_component_mod_program>(computer.shell.unremovable_circuit_components))
        {
            if (Dm.eq(comp.associated_program, this))
            {
                Dm.remove(computer.shell.unremovable_circuit_components, comp);
                qdel(comp);
            }
        }
        return base.Destroy(force);

    }

    // tg: code/modules/modular_computers/file_system/program.dm:88
    public override datum_computer_file clone(object rename = null)
    {
        datum_computer_file_program temp = base.clone(rename) as datum_computer_file_program;
        temp.run_access = run_access;
        temp.filedesc = filedesc;
        temp.program_open_overlay = program_open_overlay;
        temp.program_flags = program_flags;
        temp.can_run_on_flags = can_run_on_flags;
        if (Dm.truthy(Dm.bitand(program_flags, PROGRAM_UNIQUE_COPY)))
        {
            if (computer != null)
                computer.remove_file(this);
            if (disk_host != null)
                disk_host.remove_file(this);
        }
        return temp;
    }

    /**
     * WARNING: this proc does not work the same as normal `ui_interact`, as the
     * computer takes care of opening the UI. The `datum/tgui/ui` parameter will always exist.
     * This proc only serves as a callback.
     */
    // tg: code/modules/modular_computers/file_system/program.dm:107
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        return null;
    }

    // Relays icon update to the computer.
    // tg: code/modules/modular_computers/file_system/program.dm:111
    public virtual void update_computer_icon()
    {
        if (computer != null)
            computer.update_appearance();
    }

    ///Attempts to generate an Ntnet log, returns the log on success, FALSE otherwise.
    // tg: code/modules/modular_computers/file_system/program.dm:116
    public virtual object generate_network_log(object text = null)
    {
        if (computer == null || Dm.truthy(Dm.bitand(computer.obj_flags, EMAGGED)))
            return FALSE;
        return computer.add_log(text);
    }

    /**
     *Runs when the device is used to attack an atom in non-combat mode using right click (secondary).
     *
     *Simulates using the device to read or scan something. Tap is called by the computer during pre_attack
     *and sends us all of the related info. If we return TRUE, the computer will stop the attack process
     *there. What we do with the info is up to us, but we should only return TRUE if we actually perform
     *an action of some sort.
     *Arguments:
     *A is the atom being tapped
     *user is the person making the attack action
     *modifiers is anything the pre_attack() proc had in the same-named variable.
    */
    // tg: code/modules/modular_computers/file_system/program.dm:133
    public virtual async DmTask<object> tap(atom tapped_atom = null, mob_living user = null, DmList modifiers = null)
    {
        return FALSE;
    }

    ///Makes sure a program can run on this hardware (for apps limited to tablets/computers/laptops)
    // tg: code/modules/modular_computers/file_system/program.dm:137
    public virtual object is_supported_by_hardware(object hardware_flag = null, object loud = null, mob user = null)
    {
        hardware_flag ??= NONE;
        if (!Dm.truthy(Dm.bitand(hardware_flag, can_run_on_flags)))
        {
            if (Dm.truthy(loud) && computer != null && user != null)
                to_chat(user, span_danger(I18n.T("\\The [] flashes a \"Hardware Error - Incompatible software\" warning.", computer)));
            return FALSE;
        }
        return TRUE;
    }

    // Called by Process() on device that runs us, once every tick.
    // tg: code/modules/modular_computers/file_system/program.dm:145
    public virtual object process_tick(object seconds_per_tick = null)
    {
        return TRUE;
    }

    /**
     * Checks if the user can run program. Only humans and silicons can operate computer. Automatically called in on_start()
     * ID must be inserted into a card slot to be read. If the program is not currently installed (as is the case when
     * NT Software Hub is checking available software), a list can be given to be used instead.
     * Args:
     * user is a ref of the mob using the device.
     * loud is a bool deciding if this proc should use to_chats
     * access_to_check is an access level that will be checked against the ID
     * downloading: Boolean on whether it's downloading the app or not. If it is, it will check download_access instead of run_access.
     * access can contain a list of access numbers to check against. If access is not empty, it will be used istead of checking any inserted ID.
     */
    // tg: code/modules/modular_computers/file_system/program.dm:159
    public virtual object can_run(mob user = null, object loud = null, object access_to_check = null, object downloading = null, DmList access = null)
    {
        if (user != null)
        {
            if (issilicon(user) && !ispAI(user))
                return TRUE;
            // if(isAdminGhostAI(user)) return TRUE: GAP(lead): /proc/isAdminGhostAI (mob_helpers.dm:357) needs an admin observer
            // with admin rights (check_rights_for, M10), so no mob is one
        }

        if (computer != null && Dm.truthy(Dm.bitand(computer.obj_flags, EMAGGED)) && (Dm.truthy(Dm.bitand(program_flags, PROGRAM_ON_SYNDINET_STORE)) || !Dm.truthy(downloading))) //emagged can run anything on syndinet, and can bypass execution locks, but not download.
            return TRUE;

        if (!Dm.truthy(access_to_check))
        {
            if (Dm.truthy(downloading) && Dm.length(download_access) > 0)
                access_to_check = download_access;
            else
                access_to_check = run_access;
        }
        if (Dm.length(access_to_check) == 0) // No access requirements, allow it.
            return TRUE;

        if (Dm.length(access) == 0)
        {
            obj_item_card_id accesscard = null;
            if (computer != null)
                accesscard = computer.stored_id?.GetID() as obj_item_card_id;

            if (accesscard == null)
            {
                if (Dm.truthy(loud) && user != null)
                    to_chat(user, span_danger(I18n.T("\\The [] flashes an \"RFID Error - Unable to scan ID\" warning.", computer)));
                return FALSE;
            }
            access = accesscard.GetAccess() as DmList;
        }

        foreach (object singular_access in Dm.each(access_to_check))
        {
            if (Dm.@in(singular_access, access)) //For loop checks every individual access entry in the access list. If the user's ID has access to any entry, then we're good.
                return TRUE;
        }

        if (Dm.truthy(loud) && user != null)
            to_chat(user, span_danger(I18n.T("\\The [] flashes an \"Access Denied\" warning.", computer)));
        return FALSE;
    }

    /**
     * Called on program startup.
     *
     * May be overridden to add extra logic. Remember to include ..() call. Return 1 on success, 0 on failure.
     * When implementing new program based device, use this to run the program.
     * Arguments:
     * * user - The mob that started the program
     **/
    // tg: code/modules/modular_computers/file_system/program.dm:204
    public virtual async DmTask<object> on_start(mob_living user = null)
    {
        if (!Dm.truthy(can_run(user, loud: TRUE)))
            return FALSE;
        if (Dm.truthy(Dm.bitand(program_flags, PROGRAM_REQUIRES_NTNET)))
        {
            obj_item_card_id ID = computer.stored_id?.GetID() as obj_item_card_id;
            generate_network_log(Dm.text("Connection opened -- Program ID:[] User:[]", filename, ID != null ? Dm.text("[]", ID.registered_name) : "None"));
        }
        if (Dm.truthy(this._listen_lookup?[COMSIG_COMPUTER_PROGRAM_START]))
            this._SendSignal(COMSIG_COMPUTER_PROGRAM_START, Dm.list(this, user));
        return TRUE;
    }

    /**
     * Kills the running program
     *
     * Use this proc to kill the program.
     * Designed to be implemented by each program if it requires on-quit logic, such as the NTNRC client.
     * Args:
     * - user - If there's a user, this is the person killing the program.
     **/
    // tg: code/modules/modular_computers/file_system/program.dm:222
    public virtual object kill_program(mob user = null)
    {
        if (this == computer.active_program)
        {
            computer.active_program = null;
            if (!QDELETED(computer) && Dm.truthy(computer.enabled))
                Waiting.Detach(computer.update_tablet_open_uis(user));
        }
        else if (Dm.@in(this, computer.idle_threads))
            computer.idle_threads.Remove(this);
        else //The program wasn't running to begin with.
            return FALSE;

        if (Dm.truthy(Dm.bitand(program_flags, PROGRAM_REQUIRES_NTNET)))
        {
            obj_item_card_id ID = computer.stored_id?.GetID() as obj_item_card_id;
            generate_network_log(Dm.text("Connection closed -- Program ID: [] User:[]", filename, ID != null ? Dm.text("[]", ID.registered_name) : "None"));
        }

        computer.update_appearance(UPDATE_ICON);
        if (Dm.truthy(this._listen_lookup?[COMSIG_COMPUTER_PROGRAM_KILL]))
            this._SendSignal(COMSIG_COMPUTER_PROGRAM_KILL, Dm.list(this, user));
        return TRUE;
    }

    ///Sends the running program to the background/idle threads. Header programs can't be minimized and will kill instead.
    // tg: code/modules/modular_computers/file_system/program.dm:243
    public virtual object background_program(mob user = null)
    {
        if (Dm.truthy(Dm.bitand(program_flags, PROGRAM_HEADER)) || computer.idle_threads.len > computer.max_idle_programs)
            return kill_program();

        computer.idle_threads.Add(this);
        computer.active_program = null;

        if (user != null)
            Waiting.Detach(computer.update_tablet_open_uis(user));
        computer.update_appearance(UPDATE_ICON);
        return TRUE;
    }

    ///Called when the program is made the active program.
    // tg: code/modules/modular_computers/file_system/program.dm:257
    public virtual async DmTask on_made_active_program(mob user = null)
    {
    }
}
