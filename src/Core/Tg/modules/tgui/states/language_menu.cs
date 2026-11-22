// Translated from tgstation (snapshot 1d99bd7f): code/modules/tgui/states/language_menu.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps L2 (D327): the whole file, the language menu's ui state (its owner or an admin).

public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/modules/tgui/states/language_menu.dm:10  GLOBAL_DATUM_INIT(language_menu_state, /datum/ui_state/language_menu, new)
    public datum_ui_state_language_menu language_menu_state;

    // tg: code/modules/tgui/states/language_menu.dm:10  (GLOBAL_MANAGED(language_menu_state, new), __DEFINES/__globals.dm:5)
    public virtual void InitGloballanguage_menu_state()
    {
        language_menu_state = Dm.@new<datum_ui_state_language_menu>();
        Dm.append(ref gvars_datum_init_order, "language_menu_state");
    }
}

// tg: code/modules/tgui/states/language_menu.dm:12
public partial class datum_ui_state_language_menu : datum_ui_state
{
    // tg: code/modules/tgui/states/language_menu.dm:12
    public override async DmTask<object> can_use_topic(object src_object = null, mob user = null)
    {
        object dot = UI_CLOSE;
        if (Dm.truthy(check_rights_for(user.client, R_ADMIN)))
            dot = UI_INTERACTIVE;
        else if (src_object is datum_language_menu my_languages)
        {
            if (my_languages.language_holder.owner == user)
                dot = UI_INTERACTIVE;
        }
        return dot;
    }
}
