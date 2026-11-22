// Translated from tgstation (snapshot 1d99bd7f): code/modules/modular_computers/file_system/computer_file.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M9 slice E: NtOS): a computer file, its unique id, its install into a computer and a program's failure events.
// The var block is generated (D111).

// tg: code/modules/modular_computers/file_system/computer_file.dm:1
public partial class datum_computer_file : datum
{
    // tg: code/modules/modular_computers/file_system/computer_file.dm:13  var/static/file_uid = 0
    public float file_uid
    {
        get => TgState.Current.datum_computer_file__file_uid;
        set => TgState.Current.datum_computer_file__file_uid = value;
    }

    // tg: code/modules/modular_computers/file_system/computer_file.dm:19
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        uid = file_uid++;
        RegisterSignal(this, COMSIG_COMPUTER_FILE_STORE, nameof(on_install));
    }

    // tg: code/modules/modular_computers/file_system/computer_file.dm:24
    public override object Destroy(float? force = null)
    {
        if (computer != null)
            computer = null;
        if (disk_host != null)
            disk_host = null;
        return base.Destroy(force);
    }

    /**
     * Used for special cases where an application
     * Requires special circumstances to install on a PC
     * Args:
     * * potential_host - the ModPC that is attempting to store this file.
     */
    // tg: code/modules/modular_computers/file_system/computer_file.dm:37
    public virtual object can_store_file(obj_item_modular_computer potential_host = null)
    {
        return TRUE;
    }

    // Returns independent copy of this file.
    // tg: code/modules/modular_computers/file_system/computer_file.dm:41
    public virtual datum_computer_file clone(object rename = null)
    {
        datum_computer_file temp = Dm.@new(GetType()) as datum_computer_file;
        temp.undeletable = undeletable;
        temp.size = size;
        if (Dm.truthy(rename))
            temp.filename = filename + "(Copy)";
        else
            temp.filename = filename;
        temp.filetype = filetype;
        return temp;
    }

    ///Called post-installation of an application in a computer, after 'computer' var is set. Remember, the user is optional
    // tg: code/modules/modular_computers/file_system/computer_file.dm:53
    [SignalHandler]
    public virtual void on_install(datum_computer_file source = null, obj_item_modular_computer computer_installing = null, mob user = null)
    {
        computer_installing.stored_files.Add(this);
    }

    /**
     * Called when examining a modular computer
     * Args:
     * Source - The tablet that's being examined
     * User - Person examining the computer
     *
     * note: please replace this with signals when hdd's are removed and program's New() already has the tablet set.
     */
    // tg: code/modules/modular_computers/file_system/computer_file.dm:66
    public virtual object on_examine(obj_item_modular_computer source = null, mob user = null)
    {
        return null;
    }

    /// Called on modular computer item_interaction, checking if any application uses the given item. Uses the item interaction chain flags.
    // tg: code/modules/modular_computers/file_system/computer_file.dm:70
    public virtual async DmTask<object> application_item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null)
    {
        return NONE;
    }

    /**
     * Implement this when your program has an object that the user can eject.
     *
     * Examples include ejecting cells AI intellicards.
     * Arguments:
     * * user - The mob requesting the eject.
     * * forced - Whether we are forced to eject everything (usually by the app being deleted)
     */
    // tg: code/modules/modular_computers/file_system/computer_file.dm:81
    public virtual async DmTask<object> try_eject(mob_living user = null, object forced = null)
    {
        return FALSE;
    }
}

// tg: code/modules/modular_computers/file_system/computer_file.dm:86 (interleaved types, rules 1.9)
public partial class datum_computer_file_program : datum_computer_file
{
    /**
     * Called when a computer program is shut down from the tablet's charge dying
     * Arguments:
     * * background - Whether the app is running in the background.
     */
    // tg: code/modules/modular_computers/file_system/computer_file.dm:89
    public virtual void event_powerfailure()
    {
        kill_program();
    }

    /**
     * Called when a computer program is crashing due to any required connection being shut off.
     * Arguments:
     * * background - Whether the app is running in the background.
     */
    // tg: code/modules/modular_computers/file_system/computer_file.dm:97
    public virtual void event_networkfailure(object background = null)
    {
        kill_program();
        if (Dm.truthy(background))
            computer.visible_message(span_danger(I18n.T("\\The []'s screen displays a \"Process [].[] (PID []) terminated - Network Error\" error", computer, filename, filetype, Dm.rand(100, 999))));
        else
            computer.visible_message(span_danger(I18n.T("\\The []'s screen briefly freezes and then shows \"NETWORK ERROR - NTNet connection lost. Please retry. If problem persists contact your system administrator.\" error.", computer)));
    }
}

public sealed partial class TgState
{
    // tg: code/modules/modular_computers/file_system/computer_file.dm:13  /datum/computer_file's var/static/file_uid = 0
    internal float datum_computer_file__file_uid;
}
