// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/lube_walking.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps OG1: the snail liver's slime trail). The var block is generated (D111).

/**
 * # lube_walking
 *
 * Makes a mob cause a turf to get wet as they walk, requires lying down.
 * Has configurable args for wet flags, time, and resting requirements.
 */
// tg: code/datums/elements/lube_walking.dm:7
public partial class datum_element_lube_walking : datum_element
{
    // tg: code/datums/elements/lube_walking.dm:18
    [DmParams("target", "wet_flags", "min_time_wet_for", "require_resting")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object wet_flags = Dm.arg(__extra, 0) ?? TURF_WET_LUBE;
        object min_time_wet_for = Dm.arg(__extra, 1) ?? 2 * SECONDS;
        object require_resting = Dm.arg(__extra, 2) ?? FALSE;
        object dot = await base.Attach(target, __extra);
        if (!ismovable(target))
            return ELEMENT_INCOMPATIBLE;
        this.wet_flags = wet_flags;
        this.min_time_wet_for = min_time_wet_for;
        this.require_resting = require_resting;

        if (Dm.truthy(require_resting))
        {
            if (!isliving(target))
            {
                stack_trace(Dm.text("lube_walking Element was added onto [] with require_resting set on, which only works on living mobs.", target), "code/datums/elements/lube_walking.dm", 28);
                return ELEMENT_INCOMPATIBLE;
            }
            mob_living living_target = target as mob_living;
            RegisterSignal(living_target, COMSIG_LIVING_RESTING, nameof(on_resting_changed));
            if (Dm.truthy(living_target.resting)) //theyre resting as the element was added, so let them start lubricating.
                on_resting_changed(living_target, new_resting: TRUE);
        }
        else
            RegisterSignal(target, COMSIG_MOVABLE_MOVED, nameof(lubricate));
        return dot;
    }

    // tg: code/datums/elements/lube_walking.dm:37
    public override object Detach(datum target = null, params object[] __extra)
    {
        object dot = base.Detach(target, __extra);
        UnregisterSignal(target, Dm.list(COMSIG_LIVING_RESTING, COMSIG_MOVABLE_MOVED));
        if (istype(target, typeof(mob_living_carbon)))
            ((mob_living_carbon)target).remove_movespeed_modifier(typeof(datum_movespeed_modifier_snail_crawl));
        return dot;
    }

    ///Called when a living mob changes their resting state with require_resting on, giving them their movement speed and ability.
    // tg: code/datums/elements/lube_walking.dm:44
    [SignalHandler]
    public virtual void on_resting_changed(mob snail = null, object new_resting = null, object silent = null, object instant = null)
    {
        if (Dm.truthy(new_resting) && Dm.truthy(lubricate(snail)))
        {
            Waiting.NoSleep(snail.add_movespeed_modifier(typeof(datum_movespeed_modifier_snail_crawl)));
            RegisterSignal(snail, COMSIG_MOVABLE_MOVED, nameof(lubricate));
        }
        else
        {
            snail.remove_movespeed_modifier(typeof(datum_movespeed_modifier_snail_crawl));
            UnregisterSignal(snail, COMSIG_MOVABLE_MOVED);
        }
    }

    // tg: code/datums/elements/lube_walking.dm:54
    [SignalHandler]
    public virtual object lubricate(atom_movable snail = null)
    {
        turf_open turf_standing_on = get_turf(snail) as turf_open;
        if (!istype(turf_standing_on, typeof(turf_open)))
            return FALSE;
        Waiting.NoSleep(turf_standing_on.MakeSlippery(Dm.num(wet_flags), min_wet_time: Dm.num(min_time_wet_for)));
        return TRUE;
    }
}
