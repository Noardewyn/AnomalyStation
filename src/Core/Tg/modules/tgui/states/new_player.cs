// Translated from tgstation (snapshot 1d99bd7f): code/modules/tgui/states/new_player.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M10 slice F: the whole file.

/**
 * tgui state: new_player_state
 *
 * Checks that the user is a /mob/dead/new_player
 */
public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/modules/tgui/states/new_player.dm:7  GLOBAL_DATUM_INIT(new_player_state, ...) (GLOBAL_RAW, __DEFINES/__globals.dm:32)
    public datum_ui_state_new_player_state new_player_state;

    // tg: code/modules/tgui/states/new_player.dm:7  (GLOBAL_MANAGED(new_player_state, new), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalnew_player_state()
    {
        new_player_state = Dm.@new<datum_ui_state_new_player_state>();
        Dm.append(ref gvars_datum_init_order, "new_player_state");
    }
}

// tg: code/modules/tgui/states/new_player.dm:9
[DmPath("/datum/ui_state/new_player_state")]
public partial class datum_ui_state_new_player_state : datum_ui_state
{
    // tg: code/modules/tgui/states/new_player.dm:9
    public override async DmTask<object> can_use_topic(object src_object = null, mob user = null)
    {
        return isnewplayer(user) ? UI_INTERACTIVE : UI_CLOSE;
    }
}
