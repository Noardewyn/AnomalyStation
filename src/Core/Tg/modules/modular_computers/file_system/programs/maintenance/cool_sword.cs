// Translated from tgstation (snapshot 1d99bd7f): code/modules/modular_computers/file_system/programs/maintenance/cool_sword.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps S4c, D327): the NtOS Cursor Replacer, a maintenance disk's PDA app whose PDA worn in the ID slot turns its
// wearer's mouse pointer into tg's cool sword, with its NtosCursor window. The var block is generated (D111).

// tg: code/modules/modular_computers/file_system/programs/maintenance/cool_sword.dm:1
public partial class datum_computer_file_program_maintenance_cool_sword : datum_computer_file_program_maintenance
{
    // tg: code/modules/modular_computers/file_system/programs/maintenance/cool_sword.dm:16
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        RegisterSignal(this, COMSIG_COMPUTER_FILE_DELETE, nameof(on_delete));
    }

    // tg: code/modules/modular_computers/file_system/programs/maintenance/cool_sword.dm:20
    public override void on_install(datum_computer_file source = null, obj_item_modular_computer computer_installing = null, mob user = null)
    {
        base.on_install(source, computer_installing, user);
        RegisterSignal(computer_installing, COMSIG_ITEM_EQUIPPED, nameof(host_equipped));
        RegisterSignal(computer_installing, COMSIG_ITEM_DROPPED, nameof(host_dropped));

        if (Dm.truthy(ismob(computer_installing.loc)))
        {
            mob_living computer_guy = computer_installing.loc as mob_living;
            object current_slot = computer_guy.get_slot_by_item(computer_installing);
            host_equipped(computer_installing, computer_guy, current_slot);
        }
    }

    // tg: code/modules/modular_computers/file_system/programs/maintenance/cool_sword.dm:30
    [SignalHandler]
    public virtual void on_delete(datum source = null, obj_item_modular_computer computer_uninstalling = null)
    {
        if (Dm.truthy(ismob(computer_uninstalling.loc)))
            host_dropped(computer_uninstalling, computer_uninstalling.loc as mob);
    }

    // tg: code/modules/modular_computers/file_system/programs/maintenance/cool_sword.dm:36
    [SignalHandler]
    public virtual void host_equipped(datum source = null, mob user = null, object slot = null)
    {
        if (Dm.truthy(Dm.bitand(slot, ITEM_SLOT_ID)))
        {
            if (user.client != null)
                user.client.mouse_override_icon = sword_icon;
            RegisterSignal(user, COMSIG_MOB_LOGIN, nameof(update_mouse), @override: TRUE);
            RegisterSignal(user, COMSIG_MOB_LOGOUT, nameof(stop_mouse), @override: TRUE);
        }
        else
        {
            // Shouldn't be necessary w/ dropped but just to be safe
            if (user.client != null)
                user.client.mouse_override_icon = null;
            UnregisterSignal(user, Dm.list(COMSIG_MOB_LOGIN, COMSIG_MOB_LOGOUT));
        }
        user.update_mouse_pointer();
    }

    // tg: code/modules/modular_computers/file_system/programs/maintenance/cool_sword.dm:49
    [SignalHandler]
    public virtual void host_dropped(datum source = null, mob user = null)
    {
        if (user.client != null)
            user.client.mouse_override_icon = null;
        UnregisterSignal(user, Dm.list(COMSIG_MOB_LOGIN, COMSIG_MOB_LOGOUT));
        user.update_mouse_pointer();
    }

    // tg: code/modules/modular_computers/file_system/programs/maintenance/cool_sword.dm:56
    [SignalHandler]
    public virtual void update_mouse(mob source = null)
    {
        if (source.client != null)
            source.client.mouse_override_icon = sword_icon;
        source.update_mouse_pointer();
    }

    // tg: code/modules/modular_computers/file_system/programs/maintenance/cool_sword.dm:62
    [SignalHandler]
    public virtual void stop_mouse(mob source = null)
    {
        client canon = source.canon_client;
        if (canon != null)
            canon.mouse_override_icon = null;
        canon?.mob?.update_mouse_pointer();
    }

    // tg: code/modules/modular_computers/file_system/programs/maintenance/cool_sword.dm:68
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();
        data["dmi"] = Dm.list(Dm.kv("icon", sword_icon), Dm.kv("icon_state", ""));
        return data;
    }
}
