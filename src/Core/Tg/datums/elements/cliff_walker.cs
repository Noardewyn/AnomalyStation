// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/cliff_walker.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C4).

///Lets a mob walk on cliffs while alive.
// tg: code/datums/elements/cliff_walker.dm:2
public partial class datum_element_cliff_walking : datum_element
{
    // tg: code/datums/elements/cliff_walker.dm:5
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object dot = await base.Attach(target, __extra);
        if (!isliving(target))
            return ELEMENT_INCOMPATIBLE;
        RegisterSignals(target, Dm.list(COMSIG_LIVING_DEATH, COMSIG_LIVING_REVIVE), nameof(update_cliff_walking));
        update_cliff_walking(target as mob_living);
        return dot;
    }

    // tg: code/datums/elements/cliff_walker.dm:13
    public override object Detach(datum source = null, params object[] __extra)
    {
        object dot = base.Detach(source, __extra);
        UnregisterSignal(source, Dm.list(COMSIG_LIVING_DEATH, COMSIG_LIVING_REVIVE));
        return dot;
    }

    ///Updates whether a mob can walk on cliffs by whether it is dead.
    // tg: code/datums/elements/cliff_walker.dm:19
    [SignalHandler]
    public virtual void update_cliff_walking(mob_living climber = null)
    {
        if (climber.stat != DEAD)
        {
            ADD_TRAIT(climber, TRAIT_CLIFF_WALKER, type);
            return;
        }
        REMOVE_TRAIT(climber, TRAIT_CLIFF_WALKER, type);
        // The falling off a cliff (turf/open/cliff.try_fall) waits with its turf: no cliffs are on the port's maps.
        Dm.Deferred("code/datums/elements/cliff_walker.dm:31", "try_fall of a dead climber on a cliff turf: no cliff turf on Meta");
    }
}
