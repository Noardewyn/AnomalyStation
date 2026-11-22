// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_decorators/check_cooldown.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Check if the specified blackboard key is off cooldown.
// tg: code/datums/ai/generic_decorators/check_cooldown.dm:2
public partial class datum_bt_node_decorator_key_off_cooldown : datum_bt_node_decorator
{
    // tg: code/datums/ai/generic_decorators/check_cooldown.dm:5
    public override async DmTask<object> check_condition(datum_ai_controller controller = null)
    {
        object cooldown_time = controller.blackboard[cooldown_key];
        return Dm.num(cooldown_time == null || Dm.num(cooldown_time) <= world.time);
    }
}

/**
 * Checks if a blackboard cooldown key is off cooldown before ticking the child,
 * then sets it when the child finishes  replacing the key_off_cooldown
 *
 * cooldown_duration is in deciseconds (e.g. 30 SECONDS).
 * lock_on_succeed = TRUE (default): lock after child SUCCESS only.
 * lock_on_succeed = FALSE: lock after any completion (SUCCESS or FAILURE).
 */
// tg: code/datums/ai/generic_decorators/check_cooldown.dm:17
public partial class datum_bt_node_decorator_cooldown : datum_bt_node_decorator
{
    // tg: code/datums/ai/generic_decorators/check_cooldown.dm:22
    public override async DmTask<object> check_condition(datum_ai_controller controller = null)
    {
        object cooldown_time = controller.blackboard[cooldown_key];
        return Dm.num(cooldown_time == null || Dm.num(cooldown_time) <= world.time);
    }

    // tg: code/datums/ai/generic_decorators/check_cooldown.dm:26
    public override void on_child_complete(datum_ai_controller controller = null, object result = null)
    {
        if (!Dm.truthy(lock_on_succeed) || Dm.eq(result, BT_SUCCESS))
            controller.set_blackboard_key(cooldown_key, world.time + Dm.num(cooldown_duration));
    }
}
