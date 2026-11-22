// Translated from tgstation (snapshot 1d99bd7f): code/modules/modular_computers/computers/item/computer_files.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M9 slice E: NtOS's files): storing and removing a computer's files and finding them by name or uid, on the computer
// or on an inserted computer disk (target_disk, syndicate G3d).

// tg: code/modules/modular_computers/computers/item/computer_files.dm:1 (interleaved types, rules 1.9)
public partial class obj_item_modular_computer : obj_item
{
    /**
     * store_file
     *
     * Adds an already initialized file to the computer, checking if one already exists.
     * Returns TRUE if successfully stored, FALSE otherwise.
     * user is optional: If set, the action was done by a mob/player
     */
    // tg: code/modules/modular_computers/computers/item/computer_files.dm:8
    public virtual object store_file(datum_computer_file file_storing = null, mob user = null)
    {
        if (file_storing == null)
            return FALSE;
        if (!Dm.truthy(can_store_file(file_storing)))
            return FALSE;

        // This file is already stored. Don't store it again.
        if (Dm.@in(file_storing, stored_files))
            return FALSE;

        file_storing.computer = this;
        used_capacity += Dm.num(file_storing.size);
        if (Dm.truthy(file_storing._listen_lookup?[COMSIG_COMPUTER_FILE_STORE]))
            file_storing._SendSignal(COMSIG_COMPUTER_FILE_STORE, Dm.list(file_storing, this, user));
        if (Dm.truthy(this._listen_lookup?[COMSIG_MODULAR_COMPUTER_FILE_STORE]))
            this._SendSignal(COMSIG_MODULAR_COMPUTER_FILE_STORE, Dm.list(this, file_storing, user));
        return TRUE;
    }

    /**
     * remove_file
     *
     * Removes a given file from the computer, if possible.
     * Properly checking if the file even exists and is in the computer.
     * Returns TRUE if successfully completed, FALSE otherwise
     */
    // tg: code/modules/modular_computers/computers/item/computer_files.dm:31
    public virtual object remove_file(datum_computer_file file_removing = null)
    {
        if (file_removing == null)
            return FALSE;
        if (!Dm.@in(file_removing, stored_files))
            return FALSE;
        if (file_removing is datum_computer_file_program program_file)
            program_file.kill_program();

        stored_files.Remove(file_removing);
        used_capacity -= Dm.num(file_removing.size);
        if (Dm.truthy(this._listen_lookup?[COMSIG_MODULAR_COMPUTER_FILE_DELETE]))
            this._SendSignal(COMSIG_MODULAR_COMPUTER_FILE_DELETE, Dm.list(this, file_removing));
        if (Dm.truthy(file_removing._listen_lookup?[COMSIG_COMPUTER_FILE_DELETE]))
            file_removing._SendSignal(COMSIG_COMPUTER_FILE_DELETE, Dm.list(file_removing, this));
        qdel(file_removing);
        return TRUE;
    }

    /**
     * can_store_file
     *
     * Checks if a computer can store a file, as computers can only store unique files.
     * returns TRUE if possible, FALSE otherwise.
     */
    // tg: code/modules/modular_computers/computers/item/computer_files.dm:53
    public virtual object can_store_file(datum_computer_file file = null)
    {
        if (file == null)
            return FALSE;
        if (Dm.@in(file, stored_files))
            return FALSE;
        if (find_file_by_name(file.filename) != null)
            return FALSE;
        // In the unlikely event someone manages to create that many files.
        // BYOND is acting weird with numbers above 999 in loops (infinite loop prevention)
        if (stored_files.len >= 999)
            return FALSE;
        if (used_capacity + Dm.num(file.size) > max_capacity)
            return FALSE;
        if (!Dm.truthy(file.can_store_file(this)))
            return FALSE;

        return TRUE;
    }

    /**
     * find_file_by_name
     *
     * Will check all applications in a tablet for files and, if they have \
     * the same filename (disregarding extension), will return it.
     * If a computer disk is passed instead, it will check the disk over the computer.
     */
    // tg: code/modules/modular_computers/computers/item/computer_files.dm:78
    public virtual datum_computer_file find_file_by_name(object filename = null, obj_item_disk_computer target_disk = null)
    {
        if (!istext(filename))
            return null;
        if (target_disk == null)
        {
            foreach (datum_computer_file file in Dm.each<datum_computer_file>(stored_files))
            {
                if (Dm.eq(file.filename, filename))
                    return file;
            }
        }
        else
        {
            foreach (datum_computer_file file in Dm.each<datum_computer_file>(target_disk.stored_files))
            {
                if (Dm.eq(file.filename, filename))
                    return file;
            }
        }
        return null;
    }

    /**
     * find_file_by_full_name
     *
     * Will check all applications in a tablet for files and, if they have \
     * the same filename AND extension, will return it.
     * If a computer disk is passed instead, it will check the disk over the computer.
     */
    // tg: code/modules/modular_computers/computers/item/computer_files.dm:98
    public virtual datum_computer_file find_file_by_full_name(object full_path = null, obj_item_disk_computer target_disk = null)
    {
        if (!istext(full_path))
            return null;
        if (target_disk == null)
        {
            foreach (datum_computer_file file in Dm.each<datum_computer_file>(stored_files))
            {
                if (Dm.eq(Dm.text("[].[]", file.filename, file.filetype), full_path))
                    return file;
            }
        }
        else
        {
            foreach (datum_computer_file file in Dm.each<datum_computer_file>(target_disk.stored_files))
            {
                if (Dm.eq(Dm.text("[].[]", file.filename, file.filetype), full_path))
                    return file;
            }
        }
        return null;
    }

    /**
     * find_file_by_uid
     *
     * Will check all files in this computer and returns the file with the matching uid.
     * A file's uid is always unique to them, so this proc is sometimes preferable over find_file_by_name.
     * If a computer disk is passed instead, it will check the disk over the computer.
     */
    // tg: code/modules/modular_computers/computers/item/computer_files.dm:118
    public virtual datum_computer_file find_file_by_uid(object uid = null, obj_item_disk_computer target_disk = null)
    {
        if (!isnum(uid))
            return null;
        if (target_disk == null)
        {
            foreach (datum_computer_file file in Dm.each<datum_computer_file>(stored_files))
            {
                if (Dm.eq(file.uid, uid))
                    return file;
            }
        }
        else
        {
            foreach (datum_computer_file file in Dm.each<datum_computer_file>(target_disk.stored_files))
            {
                if (Dm.eq(file.uid, uid))
                    return file;
            }
        }
        return null;
    }
}
