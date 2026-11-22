// Translated from tgstation (snapshot 1d99bd7f): code/modules/modular_computers/file_system/programs/theme_selector.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M9 slice E: NtOS's themes): the Themeify app and its NtosThemeConfigure window. The var block is generated (D111).

// tg: code/modules/modular_computers/file_system/programs/theme_selector.dm:1
public partial class datum_computer_file_program_themeify : datum_computer_file_program
{
    // tg: code/modules/modular_computers/file_system/programs/theme_selector.dm:15
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();

        if (Dm.truthy(Dm.bitand(computer.obj_flags, EMAGGED)))
            data["themes"] = Dm.add(data["themes"], Dm.list(Dm.list(Dm.kv("theme_name", PDA_THEME_SYNDICATE_NAME), Dm.kv("theme_ref", GLOB.pda_name_to_theme[PDA_THEME_SYNDICATE_NAME]))));
        DmList available_themes = GLOB.default_pda_themes.Copy();
        if (imported_themes != null)
            Dm.append(ref available_themes, imported_themes);
        foreach (object theme_key in Dm.each(available_themes))
            data["themes"] = Dm.add(data["themes"], Dm.list(Dm.list(Dm.kv("theme_name", theme_key), Dm.kv("theme_ref", GLOB.pda_name_to_theme[theme_key]))));

        return data;
    }

    // tg: code/modules/modular_computers/file_system/programs/theme_selector.dm:28
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        await base.ui_act(action, @params, ui, state);
        if (Dm.eq(action, "PRG_change_theme"))
        {
            object selected_theme = @params["selected_theme"];
            if (!Dm.truthy(GLOB.default_pda_themes.Find(selected_theme)) &&
                !Dm.truthy(LAZYFIND(imported_themes, selected_theme)) &&
                !Dm.truthy(Dm.bitand(computer.obj_flags, EMAGGED)))
                return FALSE;
            computer.device_theme = Dm.str(GLOB.pda_name_to_theme[selected_theme]);
            return TRUE;
        }
        return null;
    }
}
