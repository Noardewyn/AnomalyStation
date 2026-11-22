// Translated from tgstation (snapshot 1d99bd7f): code/modules/modular_computers/file_system/programs/maintenance/modsuit.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps MO1 (D333): the file but its circuit component. MODsuit Control, a maintenance disk's PDA app: a control unit tapped on
// the PDA is synced to it, and the NtosMODsuit window then shows that suit's MODsuit window and works its acts remotely. The
// program's circuit component (/obj/item/circuit_component/mod_program/modsuit_control, :52-71) is wiremod's (section 164): a row
// in not-translated.md 219. The var block is generated (D111).

// tg: code/modules/modular_computers/file_system/programs/maintenance/modsuit.dm:1
public partial class datum_computer_file_program_maintenance_modsuit_control : datum_computer_file_program_maintenance
{
    // tg: code/modules/modular_computers/file_system/programs/maintenance/modsuit.dm:15
    public override object Destroy(float? force = null)
    {
        if (controlled_suit != null)
            unsync_modsuit();
        return base.Destroy(force);
    }

    // tg: code/modules/modular_computers/file_system/programs/maintenance/modsuit.dm:20
    public override async DmTask<object> application_item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null)
    {
        if (istype(tool, typeof(obj_item_mod_control)))
        {
            sync_modsuit(tool as obj_item_mod_control, user);
            return ITEM_INTERACT_SUCCESS;
        }
        return null;
    }

    // tg: code/modules/modular_computers/file_system/programs/maintenance/modsuit.dm:25
    public virtual void sync_modsuit(obj_item_mod_control new_modsuit = null, mob_living user = null)
    {
        if (controlled_suit != null)
            unsync_modsuit();
        controlled_suit = new_modsuit;
        RegisterSignal(controlled_suit, COMSIG_QDELETING, nameof(unsync_modsuit));
        user?.balloon_alert(user, I18n.T("suit updated"));
    }

    // tg: code/modules/modular_computers/file_system/programs/maintenance/modsuit.dm:32
    [SignalHandler]
    public virtual void unsync_modsuit(atom source = null)
    {
        UnregisterSignal(controlled_suit, COMSIG_QDELETING);
        controlled_suit = null;
    }

    // tg: code/modules/modular_computers/file_system/programs/maintenance/modsuit.dm:37
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["has_suit"] = Dm.num(controlled_suit != null);
        if (controlled_suit != null)
            Dm.append(ref data, await controlled_suit.ui_data());
        return data;
    }

    // tg: code/modules/modular_computers/file_system/programs/maintenance/modsuit.dm:44
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        if (controlled_suit == null)
            return null;
        return await controlled_suit.ui_static_data();
    }

    // tg: code/modules/modular_computers/file_system/programs/maintenance/modsuit.dm:47
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        await base.ui_act(action, @params, ui, state);
        if (controlled_suit == null)
            return null;
        return await controlled_suit.ui_act(action, @params, ui, state);
    }
}
