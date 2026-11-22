// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/bots/bot_decorators.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C3).

/// Gates child on pawn being emagged. Use invert = TRUE for the opposite. Checked each tick.
// tg: code/datums/ai/bots/bot_decorators.dm:2
public partial class datum_bt_node_decorator_bot_is_emagged : datum_bt_node_decorator
{
    // tg: code/datums/ai/bots/bot_decorators.dm:4
    public override async DmTask<object> check_condition(datum_ai_controller controller = null)
    {
        mob_living_basic_bot bot_pawn = (mob_living_basic_bot)controller.pawn;
        return Dm.num(Dm.truthy(Dm.bitand(bot_pawn.bot_access_flags, BOT_COVER_EMAGGED)));
    }
}

/// Gates child on pawn having the specified bot_mode_flag. Observes COMSIG_BOT_MODE_FLAGS_SET.
// tg: code/datums/ai/bots/bot_decorators.dm:9
public partial class datum_bt_node_decorator_bot_mode_flag : datum_bt_node_decorator
{
    // tg: code/datums/ai/bots/bot_decorators.dm:12
    public override object register_observe_signals(atom pawn = null)
    {
        RegisterSignal(pawn, COMSIG_BOT_MODE_FLAGS_SET, nameof(on_signal_changed));
        return TRUE;
    }

    // tg: code/datums/ai/bots/bot_decorators.dm:16
    public override void unregister_observe_signals(atom pawn = null)
    {
        UnregisterSignal(pawn, COMSIG_BOT_MODE_FLAGS_SET);
    }

    // tg: code/datums/ai/bots/bot_decorators.dm:19
    public override async DmTask<object> check_condition(datum_ai_controller controller = null)
    {
        mob_living_basic_bot bot_pawn = (mob_living_basic_bot)controller.pawn;
        return Dm.num(Dm.truthy(Dm.bitand(bot_pawn.bot_mode_flags, flag)));
    }
}

/// Gates child on the pawn's current mode matching `mode` (e.g. BOT_DELIVER). Use invert = TRUE for the opposite. Checked each tick.
// tg: code/datums/ai/bots/bot_decorators.dm:24
public partial class datum_bt_node_decorator_bot_mode : datum_bt_node_decorator
{
    // tg: code/datums/ai/bots/bot_decorators.dm:27
    public override async DmTask<object> check_condition(datum_ai_controller controller = null)
    {
        mob_living_basic_bot bot_pawn = (mob_living_basic_bot)controller.pawn;
        return Dm.num(Dm.eq(bot_pawn.mode, mode));
    }
}

/// Gates child on the pawn's `wire` being cut. Use invert = TRUE to gate on the wire being intact. Checked each tick.
// tg: code/datums/ai/bots/bot_decorators.dm:32
public partial class datum_bt_node_decorator_bot_wire_cut : datum_bt_node_decorator
{
    // tg: code/datums/ai/bots/bot_decorators.dm:35
    public override async DmTask<object> check_condition(datum_ai_controller controller = null)
    {
        mob_living_basic_bot bot_pawn = (mob_living_basic_bot)controller.pawn;
        return Dm.num(Dm.truthy((bot_pawn.wires as datum_wires)?.is_cut(wire)));
    }
}

/// Gates child when pawn has the specified medical mode flag. Use invert = TRUE for the opposite. Checked each tick.
// tg: code/datums/ai/bots/bot_decorators.dm:40
public partial class datum_bt_node_decorator_bot_medical_flag : datum_bt_node_decorator
{
    // tg: code/datums/ai/bots/bot_decorators.dm:43
    public override async DmTask<object> check_condition(datum_ai_controller controller = null)
    {
        mob_living_basic_bot_medbot bot_pawn = (mob_living_basic_bot_medbot)controller.pawn;
        return Dm.num(Dm.truthy(Dm.bitand(bot_pawn.medical_mode_flags, flag)));
    }
}
