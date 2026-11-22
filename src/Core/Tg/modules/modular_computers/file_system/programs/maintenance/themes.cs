// Translated from tgstation (snapshot 1d99bd7f): code/modules/modular_computers/file_system/programs/maintenance/themes.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps S4c, D327): the PDA themes found on maintenance disks: installed, a theme joins Themeify's list (and makes it
// bigger) and the holder deletes itself. The progress award that keeps the theme for later rounds (`give_award`) waits with the
// achievements (not-translated section 164). The var blocks are generated (D111).

/// PDA themes that you can find in maintenance. Once installed by a player, it'll become available to them on future rounds as well.
// tg: code/modules/modular_computers/file_system/programs/maintenance/themes.dm:2
public partial class datum_computer_file_program_maintenance_theme : datum_computer_file_program_maintenance
{
    // tg: code/modules/modular_computers/file_system/programs/maintenance/themes.dm:18
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        filename = Dm.text("[] Theme", theme_name);
    }

    // tg: code/modules/modular_computers/file_system/programs/maintenance/themes.dm:22
    public override object can_store_file(obj_item_modular_computer potential_host = null)
    {
        object dot = base.can_store_file(potential_host);
        if (!Dm.truthy(dot))
            return FALSE;
        datum_computer_file_program_themeify theme_app = Dm.locate<datum_computer_file_program_themeify>(potential_host.stored_files);
        //no theme app, no themes!
        if (theme_app == null)
            return FALSE;
        //don't get the same one twice
        if (Dm.truthy(LAZYFIND(theme_app.imported_themes, theme_name)))
            return FALSE;
        return TRUE;
    }

    ///Called post-installation of an application in a computer, after 'computer' var is set.
    // tg: code/modules/modular_computers/file_system/programs/maintenance/themes.dm:36
    public override void on_install(datum_computer_file source = null, obj_item_modular_computer computer_installing = null, mob user = null)
    {
        // SHOULD_CALL_PARENT(FALSE)
        //add the theme to the computer and increase its size to match
        datum_computer_file_program_themeify theme_app = Dm.locate<datum_computer_file_program_themeify>(computer.stored_files);
        if (theme_app == null)
            return;
        LAZYADD(ref theme_app.imported_themes, theme_name);
        theme_app.size += size;
        // user?.client?.give_award(/datum/award/score/progress/pda_themes, user, theme_id, theme_name): the achievements (not-translated section 164)
        qdel(this);
    }
}
