// Translated from tgstation (snapshot 1d99bd7f): code/modules/modular_computers/file_system/programs/raptordex.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;


// tg: code/modules/modular_computers/file_system/programs/raptordex.dm:1
public partial class datum_computer_file_program_raptordex : datum_computer_file_program
{
    // tg: code/modules/modular_computers/file_system/programs/raptordex.dm:14
    public override async DmTask<object> tap(atom tapped_atom = null, mob_living user = null, DmList modifiers = null)
    {
        if (!(tapped_atom is mob_living_basic_raptor my_raptor))
            return FALSE;

        // tg's lines 19-58 build the same scan as the RaptorDex (raptor_dex.dm:27-66; DEVIATION DV619: the pictures)
        scan_data = obj_item_raptor_dex.raptor_scan_data(my_raptor);
        playsound(computer, Dm.resource("sound/mobs/non-humanoids/orbie/orbie_send_out.ogg"), 20);
        my_raptor.balloon_alert(user, "scanned");
        return TRUE;
    }

    // tg: code/modules/modular_computers/file_system/programs/raptordex.dm:62
    public override async DmTask<object> ui_data(mob user = null)
    {
        return scan_data;
    }

    // tg: code/modules/modular_computers/file_system/programs/raptordex.dm:65
    public override async DmTask on_made_active_program(mob user = null)
    {
        RegisterSignal(computer, COMSIG_ITEM_REQUESTING_CONTEXT_FOR_TARGET, nameof(add_item_context));
        computer.item_flags = Dm.num(Dm.bitor(computer.item_flags, ITEM_HAS_CONTEXTUAL_SCREENTIPS));
    }

    // tg: code/modules/modular_computers/file_system/programs/raptordex.dm:69
    public override object background_program(mob user = null)
    {
        object dot = base.background_program(user);
        UnregisterSignal(computer, COMSIG_ITEM_REQUESTING_CONTEXT_FOR_TARGET);
        return dot;
    }

    // tg: code/modules/modular_computers/file_system/programs/raptordex.dm:73
    public override object kill_program(mob user = null)
    {
        object dot = base.kill_program(user);
        UnregisterSignal(computer, COMSIG_ITEM_REQUESTING_CONTEXT_FOR_TARGET);
        return dot;
    }

    // tg: code/modules/modular_computers/file_system/programs/raptordex.dm:77
    [SignalHandler]
    public virtual object add_item_context(obj_item source = null, DmList context = null, atom target = null, mob_living user = null)
    {
        if (!(target is mob_living_basic_raptor))
            return NONE;
        context[SCREENTIP_CONTEXT_RMB] = I18n.T("Scan Raptor");
        return CONTEXTUAL_SCREENTIP_SET;
    }
}
