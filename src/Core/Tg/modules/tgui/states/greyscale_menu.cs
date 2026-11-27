// Translated from tgstation (snapshot 1d99bd7f): code/modules/tgui/states/greyscale_menu.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (the MetaStation leftovers: the colour menu).

/**
 * tgui state: greyscale menu
 *
 * Checks that the target var of the greyscale menu meets the default can_use_topic criteria
 */
public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/modules/tgui/states/greyscale_menu.dm:7  GLOBAL_DATUM_INIT(greyscale_menu_state, ...) (GLOBAL_RAW, __DEFINES/__globals.dm:32)
    public datum_ui_state_greyscale_menu_state greyscale_menu_state;

    // tg: code/modules/tgui/states/greyscale_menu.dm:7  (GLOBAL_MANAGED(greyscale_menu_state, new), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalgreyscale_menu_state()
    {
        greyscale_menu_state = Dm.@new<datum_ui_state_greyscale_menu_state>();
        Dm.append(ref gvars_datum_init_order, "greyscale_menu_state");
    }
}

// tg: code/modules/tgui/states/greyscale_menu.dm:9
public partial class datum_ui_state_greyscale_menu_state : datum_ui_state
{
    // tg: code/modules/tgui/states/greyscale_menu.dm:9
    public override async DmTask<object> can_use_topic(object src_object = null, mob user = null)
    {
        datum_greyscale_modify_menu menu = (datum_greyscale_modify_menu)src_object;
        if (!isatom(menu.target))
            return UI_INTERACTIVE;

        return await GLOB.default_state.can_use_topic(menu.target, user);
    }
}
