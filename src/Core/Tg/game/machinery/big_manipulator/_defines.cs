// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/big_manipulator/_defines.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;

namespace Anomaly.Core.Tg;

// Engineering E16: the big manipulator's defines, whole (tg never #undefs them; outside code/__DEFINES, so the defines converter does
// not emit them: GlobalProcs constants, as areas.dm's, which the generated Defines manifest does not count).

public static partial class GlobalProcs
{
    // How should the manipulator interact with the point
    // tg: code/game/machinery/big_manipulator/_defines.dm:2
    public const string INTERACT_DROP = "DROP";
    public const string INTERACT_USE = "USE";
    public const string INTERACT_THROW = "THROW";

    // tg: code/game/machinery/big_manipulator/_defines.dm:6
    public const float MIN_SPEED_MULTIPLIER_TIER_1 = 0.5f;
    public const float MIN_SPEED_MULTIPLIER_TIER_2 = 0.4f;
    public const float MIN_SPEED_MULTIPLIER_TIER_3 = 0.3f;
    public const float MIN_SPEED_MULTIPLIER_TIER_4 = 0.1f;

    // tg: code/game/machinery/big_manipulator/_defines.dm:11
    public const float MAX_SPEED_MULTIPLIER_TIER_1 = 2;
    public const float MAX_SPEED_MULTIPLIER_TIER_2 = 3;
    public const float MAX_SPEED_MULTIPLIER_TIER_3 = 5;
    public const float MAX_SPEED_MULTIPLIER_TIER_4 = 6;

    // tg: code/game/machinery/big_manipulator/_defines.dm:16
    public const float MAX_TASKS_TIER_1 = 8;
    public const float MAX_TASKS_TIER_2 = 16;
    public const float MAX_TASKS_TIER_3 = 32;
    public const float MAX_TASKS_TIER_4 = 48;

    // tg: code/game/machinery/big_manipulator/_defines.dm:22
    public const float BASE_POWER_USAGE = 0.2f;
    public const float BASE_INTERACTION_TIME = 0.3f * SECONDS;
    public const float BASE_TASK_DELAY = 0.3f * SECONDS;

    /// How long will the manipulator wait if there's nothing to do
    // tg: code/game/machinery/big_manipulator/_defines.dm:27
    public const float CYCLE_SKIP_TIMEOUT = 1 * SECONDS;

    // How should overflow should be handled
    // tg: code/game/machinery/big_manipulator/_defines.dm:30
    public const string POINT_OVERFLOW_ALLOWED = "ALLOW";
    public const string POINT_OVERFLOW_FILTERS = "TO FILTERS";
    public const string POINT_OVERFLOW_HELD = "TO HELD";
    public const string POINT_OVERFLOW_FORBIDDEN = "FORBID";

    // tg: code/game/machinery/big_manipulator/_defines.dm:35
    public const string PICKUP_EAGER = "Always Pick Up";
    public const string PICKUP_CAN_WAIT = "Wait For Suiting";

    // tg: code/game/machinery/big_manipulator/_defines.dm:38
    public const string TASK_TYPE_PICKUP = "pickup";
    public const string TASK_TYPE_DROP = "drop";
    public const string TASK_TYPE_THROW = "throw";
    public const string TASK_TYPE_USE = "use";
    public const string TASK_TYPE_INTERACT = "interact";
    public const string TASK_TYPE_MOVE = "move";
    public const string TASK_TYPE_WAIT = "wait";
    public const string TASK_TYPE_STOP = "stop";

    // tg: code/game/machinery/big_manipulator/_defines.dm:47
    public const string TASKING_SEQUENTIAL = "Sequential";
    public const string TASKING_STRICT = "Strict order";
}
