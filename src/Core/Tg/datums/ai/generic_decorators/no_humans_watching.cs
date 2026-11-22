// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_decorators/no_humans_watching.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Passes only when no living, conscious human can see or hear the pawn within range.
/// Used by cowardly mobs that only act when nobody is looking.
// tg: code/datums/ai/generic_decorators/no_humans_watching.dm:1
public partial class datum_bt_node_decorator_no_humans_watching : datum_bt_node_decorator
{
    // tg: code/datums/ai/generic_decorators/no_humans_watching.dm:8
    public override async DmTask<object> check_condition(datum_ai_controller controller = null)
    {
        foreach (mob_living_carbon_human watcher in Dm.each<mob_living_carbon_human>(hearers(range, controller.pawn)))
        {
            if (watcher.stat != DEAD)
                return FALSE;
        }
        return TRUE;
    }
}
