// Translated from tgstation (snapshot 1d99bd7f): code/datums/status_effects/debuffs/confusion.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/// A status effect used for adding confusion to a mob.
// tg: code/datums/status_effects/debuffs/confusion.dm:9
public partial class datum_status_effect_confusion : datum_status_effect
{
    /// The threshold in which all of our movements are fully randomized, in seconds.
    // tg: code/datums/status_effects/debuffs/confusion.dm:2  #define CONFUSION_FULL_THRESHOLD 40 (file-local, #undef at :49)
    private const float CONFUSION_FULL_THRESHOLD = 40;
    /// A multiplier applied on how much time is left (in seconds) that determines the chance of moving sideways randomly
    // tg: code/datums/status_effects/debuffs/confusion.dm:4
    private const float CONFUSION_SIDEWAYS_MOVE_PROB_PER_SECOND = 1.5f;
    /// A multiplier applied on how much time is left (in seconds) that determines the chance of moving diagonally randomly
    // tg: code/datums/status_effects/debuffs/confusion.dm:6
    private const float CONFUSION_DIAGONAL_MOVE_PROB_PER_SECOND = 3;

    // tg: code/datums/status_effects/debuffs/confusion.dm:14
    [DmParams("new_owner", "duration")]
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        object duration = Dm.arg(__extra, 0);
        duration ??= 10 * SECONDS;
        this.duration = Dm.num_or_null(duration);
        return await base.on_creation(new_owner, Dm.forward(__extra, duration));
    }

    // tg: code/datums/status_effects/debuffs/confusion.dm:18
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        RegisterSignal(owner, COMSIG_MOB_CLIENT_PRE_MOVE, nameof(on_move));
        return TRUE;
    }

    // tg: code/datums/status_effects/debuffs/confusion.dm:22
    public override async DmTask<object> on_remove()
    {
        UnregisterSignal(owner, COMSIG_MOB_CLIENT_PRE_MOVE);
        return null;
    }

    /// Signal proc for [COMSIG_MOB_CLIENT_PRE_MOVE]. We have a chance to mix up our movement pre-move with confusion.
    // tg: code/datums/status_effects/debuffs/confusion.dm:26
    [SignalHandler]
    public virtual void on_move(datum source = null, DmList move_args = null)
    {
        // How much time is left in the duration, in seconds.
        float time_left = Dm.num(duration) / 10;
        object direction = move_args[MOVE_ARG_DIRECTION];
        object new_dir = null;

        if (time_left > CONFUSION_FULL_THRESHOLD && !Dm.truthy(owner.resting))
            new_dir = pick(GLOB.alldirs);

        else if (prob(time_left * CONFUSION_SIDEWAYS_MOVE_PROB_PER_SECOND))
            new_dir = angle2dir(Dm.num(dir2angle(direction)) + Dm.num(pick(90, -90)));

        else if (prob(time_left * CONFUSION_DIAGONAL_MOVE_PROB_PER_SECOND))
            new_dir = angle2dir(Dm.num(dir2angle(direction)) + Dm.num(pick(45, -45)));

        if (!(new_dir == null))
        {
            move_args[MOVE_ARG_NEW_LOC] = get_step(owner, Dm.num(new_dir));
            move_args[MOVE_ARG_DIRECTION] = new_dir;
        }
    }
}
