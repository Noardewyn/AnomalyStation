// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_decorators/keys_different_gender.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

///Checks for gender difference, I know, not very 2026 of me but until we add m-preg this'll have to do.
// tg: code/datums/ai/generic_decorators/keys_different_gender.dm:1
public partial class datum_bt_node_decorator_keys_different_gender : datum_bt_node_decorator
{
    // tg: code/datums/ai/generic_decorators/keys_different_gender.dm:8
    public override object register_observe_signals(atom pawn = null)
    {
        RegisterSignals(pawn, Dm.list(COMSIG_AI_BLACKBOARD_KEY_SET(key_a), COMSIG_AI_BLACKBOARD_KEY_CLEARED(key_a), COMSIG_AI_BLACKBOARD_KEY_SET(key_b), COMSIG_AI_BLACKBOARD_KEY_CLEARED(key_b)), nameof(on_signal_changed));
        return TRUE;
    }

    // tg: code/datums/ai/generic_decorators/keys_different_gender.dm:12
    public override void unregister_observe_signals(atom pawn = null)
    {
        UnregisterSignal(pawn, Dm.list(COMSIG_AI_BLACKBOARD_KEY_SET(key_a), COMSIG_AI_BLACKBOARD_KEY_CLEARED(key_a), COMSIG_AI_BLACKBOARD_KEY_SET(key_b), COMSIG_AI_BLACKBOARD_KEY_CLEARED(key_b)));
    }

    // tg: code/datums/ai/generic_decorators/keys_different_gender.dm:25
    public override async DmTask<object> check_condition(datum_ai_controller controller = null)
    {
        mob mob_a = controller.blackboard[key_a] as mob;
        mob mob_b = controller.blackboard[key_b] as mob;
        if (mob_a == null || mob_b == null)
            return FALSE;
        return Dm.num(!Dm.eq(mob_a.gender, mob_b.gender));
    }
}
