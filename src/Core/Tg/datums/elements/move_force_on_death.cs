// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/move_force_on_death.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/**
 * Element to change a mob's move forces on death and reset them on living
 */
// tg: code/datums/elements/move_force_on_death.dm:4
public partial class datum_element_change_force_on_death : datum_element
{
    // tg: code/datums/elements/move_force_on_death.dm:14
    [DmParams("target", "move_force", "move_resist", "pull_force")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object move_force = Dm.arg(__extra, 0);
        object move_resist = Dm.arg(__extra, 1);
        object pull_force = Dm.arg(__extra, 2);
        object dot = await base.Attach(target, __extra);

        if (!isliving(target))
            return ELEMENT_INCOMPATIBLE;

        RegisterSignal(target, COMSIG_LIVING_DEATH, nameof(on_death));
        RegisterSignal(target, COMSIG_LIVING_REVIVE, nameof(on_revive));

        if (move_force != null)
            this.move_force = move_force;
        if (move_resist != null)
            this.move_resist = move_resist;
        if (pull_force != null)
            this.pull_force = pull_force;
        return dot;
    }

    // tg: code/datums/elements/move_force_on_death.dm:30
    public override object Detach(datum target = null, params object[] __extra)
    {
        object dot = base.Detach(target, __extra);
        UnregisterSignal(target, Dm.list(COMSIG_LIVING_DEATH, COMSIG_LIVING_REVIVE));
        return dot;
    }

    // tg: code/datums/elements/move_force_on_death.dm:34
    [SignalHandler]
    public virtual void on_death(mob_living source = null)
    {
        if (move_force != null)
            source.move_force = Dm.num(move_force);
        if (move_resist != null)
            source.move_resist = Dm.num(move_resist);
        if (pull_force != null)
            source.set_pull_force(pull_force);
    }

    // tg: code/datums/elements/move_force_on_death.dm:44
    [SignalHandler]
    public virtual void on_revive(mob_living source = null)
    {
        source.move_force = Dm.initial(source, (mob_living p) => p.move_force);
        source.move_resist = Dm.initial(source, (mob_living p) => p.move_resist);
        source.set_pull_force(Dm.initial(source, (mob_living p) => p.pull_force));
    }
}
