// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/evolutionary_leap.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/**
 * ### Evolutionary Leap Component; set a time in the round for a mob to evolve into a more dangerous form!
 *
 * Used for bileworms, to turn into vileworms!
 */
// tg: code/datums/components/evolutionary_leap.dm:6
public partial class datum_component_evolutionary_leap : datum_component
{
    // tg: code/datums/components/evolutionary_leap.dm:14
    [DmParams("evolve_mark", "evolve_path")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object evolve_mark = Dm.arg(__extra, 0);
        object evolve_path = Dm.arg(__extra, 1);
        if (!isliving(parent))
            return COMPONENT_INCOMPATIBLE;

        this.evolve_mark = evolve_mark;
        this.evolve_path = evolve_path as Type;

        //don't setup timer yet, timer calc requires the round to have started
        if (!Dm.truthy(SSticker.HasRoundStarted()))
        {
            RegisterSignal(SSticker, COMSIG_TICKER_ROUND_STARTING, nameof(comp_on_round_start));
            return null;
        }

        //if the round has already taken long enough, evolve in three seconds.
        if ((world.time - Dm.num(SSticker.round_start_time)) > Dm.num(evolve_mark))
        {
            _addtimer(Dm.@new<datum_callback>(this, nameof(leap)), 3 * SECONDS, TIMER_DELETE_ME, file: "code/datums/components/evolutionary_leap.dm", line: 28);
            return null;
        }

        setup_timer();
        return null;
    }

    // tg: code/datums/components/evolutionary_leap.dm:33
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);
        deltimer(timer_id);
        return dot;
    }

    // tg: code/datums/components/evolutionary_leap.dm:37
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(SSticker, COMSIG_TICKER_ROUND_STARTING);
    }

    /// Proc ran when round starts.
    // tg: code/datums/components/evolutionary_leap.dm:41
    [SignalHandler]
    public virtual void comp_on_round_start()
    {
        UnregisterSignal(SSticker, COMSIG_TICKER_ROUND_STARTING);
        setup_timer();
    }

    // tg: code/datums/components/evolutionary_leap.dm:46
    public virtual void setup_timer()
    {
        //in cases where this is calculating roundstart, world.time - SSticker.round_start_time should equal 0
        float sum = (world.time - Dm.num(SSticker.round_start_time));
        float mark = Dm.num(evolve_mark) - sum;
        timer_id = _addtimer(Dm.@new<datum_callback>(this, nameof(leap), FALSE), mark, TIMER_STOPPABLE, file: "code/datums/components/evolutionary_leap.dm", line: 50);
    }

    // tg: code/datums/components/evolutionary_leap.dm:52
    public virtual async DmTask leap()
    {
        mob_living old_mob = parent as mob_living;
        if (old_mob.stat == DEAD)
            return;
        Type new_mob = evolve_path;
        object new_mob_name = Dm.initial(new_mob, (mob_living p) => p.name);
        old_mob.visible_message(span_warning(I18n.T("[] evolves into \\a []!", old_mob, new_mob_name)));
        await old_mob.change_mob_type(evolve_path, old_mob.loc, new_name: new_mob_name, delete_old_mob: TRUE);
    }
}
