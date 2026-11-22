// Translated from tgstation (snapshot 1d99bd7f): code/modules/modular_computers/file_system/programs/crewmanifest.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M9 slice E: NtOS's crew list): the Plexagon Crew List app and its NtosCrewManifest window. Its print reaches the
// computer's print_text (paper, a marked leaf). The var block is generated (D111).

// tg: code/modules/modular_computers/file_system/programs/crewmanifest.dm:1
public partial class datum_computer_file_program_crew_manifest : datum_computer_file_program
{
    // tg: code/modules/modular_computers/file_system/programs/crewmanifest.dm:12
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();
        data["manifest"] = GLOB.manifest.get_manifest();
        return data;
    }

    // tg: code/modules/modular_computers/file_system/programs/crewmanifest.dm:17
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        await base.ui_act(action, @params, ui, state);
        if (Dm.eq(action, "PRG_print"))
        {
            if (computer != null) //This option should never be called if there is no printer
            {
                string contents = Dm.text("<h4>Crew Manifest</h4>\n\t\t\t\t\t\t\t\t<br>\n\t\t\t\t\t\t\t\t[]\n\t\t\t\t\t\t\t\t", GLOB.manifest != null ? GLOB.manifest.get_html(0) : "");
                if (!Dm.truthy(computer.print_text(contents, Dm.text("crew manifest ([])", round_timestamp()))))
                {
                    to_chat(usr, span_notice(I18n.T("Printer is out of paper.")));
                    return null;
                }
                else
                    computer.visible_message(span_notice(I18n.T("\\The [] prints out a paper.", computer)));
            }
        }
        return null;
    }
}
