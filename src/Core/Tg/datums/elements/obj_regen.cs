// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/obj_regen.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SL1 (D333): the whole file (the Spinesthetics chip's integrity regeneration; the alien alloy's too). The var block is
// generated (D111).

/** Object integrity regeneration element added by alien alloy.
 */
// tg: code/datums/elements/obj_regen.dm:3
public partial class datum_element_obj_regen : datum_element
{
    // tg: code/datums/elements/obj_regen.dm:16
    [DmParams("target", "_rate")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object _rate = Dm.arg(__extra, 0) ?? 0;
        object dot = await base.Attach(target, __extra);
        if (!isobj(target))
            return ELEMENT_INCOMPATIBLE;
        if (Dm.le(_rate, 0))
            return ELEMENT_INCOMPATIBLE;

        obj target_obj = target as obj;
        rate = _rate;
        RegisterSignal(target_obj, COMSIG_ATOM_TAKE_DAMAGE, nameof(on_take_damage));
        if (Dm.lt(target_obj.get_integrity(), target_obj.max_integrity))
        {
            if (!Dm.truthy(length(processing)))
                START_PROCESSING(SSobj, this);
            Dm.combine(ref processing, target_obj);
        }
        return dot;
    }

    // tg: code/datums/elements/obj_regen.dm:30
    public override object Detach(datum target = null, params object[] __extra)
    {
        object dot = base.Detach(target, __extra);
        UnregisterSignal(target, COMSIG_ATOM_TAKE_DAMAGE);
        Dm.remove(ref processing, target);
        if (!Dm.truthy(length(processing)))
            STOP_PROCESSING(SSobj, this);
        return dot;
    }

    /// Handles beginning processing objects.
    // tg: code/datums/elements/obj_regen.dm:38
    [SignalHandler]
    public virtual void on_take_damage(obj target = null, object damage_amt = null)
    {
        if (!Dm.truthy(damage_amt))
            return;
        if (!Dm.truthy(length(processing)))
            START_PROCESSING(SSobj, this);
        Dm.combine(ref processing, target);
    }

    /// Handle regenerating attached objects.
    // tg: code/datums/elements/obj_regen.dm:48  (set waitfor = FALSE: repair_damage cannot sleep here, Waiting.NoSleep)
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (!Dm.truthy(resumed))
            currentrun = processing.Copy();

        resumed = FALSE;
        DmList cached_run = currentrun;
        if (!Dm.truthy(length(cached_run)))
        {
            if (!Dm.truthy(length(processing)))
            {
                STOP_PROCESSING(SSobj, this);
                return null;
            }
            return null;
        }

        object cached_rate = rate;
        while (Dm.truthy(length(cached_run)))
        {
            obj regen_obj = cached_run[cached_run.len] as obj;
            cached_run.len--;

            if (QDELETED(regen_obj))
            {
                Dm.remove(ref processing, regen_obj);
                if (!Dm.truthy(length(processing)))
                {
                    STOP_PROCESSING(SSobj, this);
                    return PROCESS_KILL;
                }
                if (TICK_CHECK()) // CHECK_TICK: TICK_CHECK ? stoplag() : 0 (stoplag's value is truthy)
                {
                    resumed = TRUE;
                    return null;
                }
                continue;
            }

            if (!Dm.truthy(Waiting.NoSleep(regen_obj.repair_damage(Dm.mul(regen_obj.max_integrity, cached_rate)))))
            {
                Dm.remove(ref processing, regen_obj);
                if (!Dm.truthy(length(processing)))
                {
                    STOP_PROCESSING(SSobj, this);
                    return PROCESS_KILL;
                }
            }

            if (TICK_CHECK()) // CHECK_TICK, as above
            {
                resumed = TRUE;
                return null;
            }
        }
        return null;
    }
}
