// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_decorators/pawn_health_below.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Passes when the pawn's current health is below the configured threshold.
// tg: code/datums/ai/generic_decorators/pawn_health_below.dm:1
public partial class datum_bt_node_decorator_pawn_health_below : datum_bt_node_decorator
{
    // tg: code/datums/ai/generic_decorators/pawn_health_below.dm:8
    public override object register_observe_signals(atom pawn = null)
    {
        RegisterSignal(pawn, COMSIG_LIVING_HEALTH_UPDATE, nameof(on_signal_changed));
        return TRUE;
    }

    // tg: code/datums/ai/generic_decorators/pawn_health_below.dm:12
    public override void unregister_observe_signals(atom pawn = null)
    {
        UnregisterSignal(pawn, COMSIG_LIVING_HEALTH_UPDATE);
    }

    // tg: code/datums/ai/generic_decorators/pawn_health_below.dm:15
    public override async DmTask<object> check_condition(datum_ai_controller controller = null)
    {
        if (controller.pawn is not mob_living living_pawn)
            return FALSE;
        float final_threshold = Dm.truthy(controller.blackboard[health_blackboard_key]) ? Dm.num(controller.blackboard[health_blackboard_key]) : health_threshold;
        return Dm.num(living_pawn.health < final_threshold);
    }
}
