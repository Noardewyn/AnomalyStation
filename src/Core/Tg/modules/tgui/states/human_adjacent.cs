// Translated from tgstation (snapshot 1d99bd7f): code/modules/tgui/states/human_adjacent.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5 slice 3 (D148): the whole file.

public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/modules/tgui/states/human_adjacent.dm:13  GLOBAL_DATUM_INIT(human_adjacent_state, ...) (GLOBAL_RAW, __DEFINES/__globals.dm:32)
    public datum_ui_state_human_adjacent_state human_adjacent_state;

    // tg: code/modules/tgui/states/human_adjacent.dm:13  (GLOBAL_MANAGED(human_adjacent_state, new), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalhuman_adjacent_state()
    {
        human_adjacent_state = Dm.@new<datum_ui_state_human_adjacent_state>();
        Dm.append(ref gvars_datum_init_order, "human_adjacent_state");
    }
}

// tg: code/modules/tgui/states/human_adjacent.dm:15
[DmPath("/datum/ui_state/human_adjacent_state")]
public partial class datum_ui_state_human_adjacent_state : datum_ui_state
{
    // tg: code/modules/tgui/states/human_adjacent.dm:15
    public override async DmTask<object> can_use_topic(object src_object = null, mob user = null)
    {
        object dot = user.default_can_use_topic(src_object);

        float dist = get_dist(src_object, user);
        if ((dist > 1) || (!ishuman(user)))
        {
            // Can't be used unless adjacent and human, even with TK
            dot = Dm.min(dot, UI_UPDATE);
        }
        return dot;
    }
}
