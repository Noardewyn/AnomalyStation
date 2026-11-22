// Translated from tgstation (snapshot 1d99bd7f): code/modules/modular_computers/file_system/programs/maintenance/phys_scanner.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps S4c, D327): the Physical Scanner, a maintenance disk's PDA app that health-scans a tapped carbon into its
// NtosPhysScanner window. The var block is generated (D111).

// tg: code/modules/modular_computers/file_system/programs/maintenance/phys_scanner.dm:1
public partial class datum_computer_file_program_maintenance_phys_scanner : datum_computer_file_program_maintenance
{
    // tg: code/modules/modular_computers/file_system/programs/maintenance/phys_scanner.dm:13
    public override async DmTask<object> tap(atom tapped_atom = null, mob_living user = null, DmList modifiers = null)
    {
        object dot = await base.tap(tapped_atom, user, modifiers);

        if (!Dm.truthy(iscarbon(tapped_atom)))
            return dot;
        mob_living_carbon carbon = tapped_atom as mob_living_carbon;
        carbon.visible_message(span_notice(I18n.T("[] analyzes []'s vitals.", user, tapped_atom)));
        last_record = healthscan(user, carbon, SCANNER_VERBOSE, tochat: FALSE) as string;
        datum_tgui active_ui = SStgui.get_open_ui(user, computer) as datum_tgui;
        if (active_ui != null)
            await active_ui.send_full_update(force: TRUE);
        return dot;
    }

    // tg: code/modules/modular_computers/file_system/programs/maintenance/phys_scanner.dm:24
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();
        data["last_record"] = last_record;
        return data;
    }
}
