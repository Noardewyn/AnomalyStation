// Translated from tgstation (snapshot 1d99bd7f): code/modules/client/preferences/tgui.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M10 slice F): the layout preference (a player preference, read when the preferences load). The other tgui preferences
// keep DV161 (their defaults).

/// Changes layout in some UI's, like Vending, Smartfridge etc. Making it list or grid
// tg: code/modules/client/preferences/tgui.dm:31
[DmPath("/datum/preference/choiced/tgui_layout")]
public partial class datum_preference_choiced_tgui_layout : datum_preference_choiced
{
    // tg: code/modules/client/preferences/tgui.dm:35
    public override DmList init_possible_values()
    {
        return Dm.list(
            TGUI_LAYOUT_GRID,
            TGUI_LAYOUT_LIST);
    }

    // tg: code/modules/client/preferences/tgui.dm:41 and :52: tg defines create_default_value twice on this type; the second
    // definition (:52, written for the smartfridge subtype but on the parent's path) replaces the first, so the default is the grid.
    public override object create_default_value()
    {
        return TGUI_LAYOUT_GRID;
    }

    // tg: code/modules/client/preferences/tgui.dm:44
    public override void apply_to_client(client client = null, object value = null)
    {
        foreach (datum_tgui tgui in Dm.each<datum_tgui>(client.mob?.tgui_open_uis))
        {
            // Force it to reload either way
            Waiting.NoSleep(tgui.update_static_data(client.mob));
        }
    }
}
