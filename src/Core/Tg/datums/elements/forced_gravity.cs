// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/forced_gravity.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M4c slice 8: the turfs of the gravity generator's graviton field). The var block and the element flags are
// generated (D111).

// tg: code/datums/elements/forced_gravity.dm:1
public partial class datum_element_forced_gravity : datum_element
{
    // tg: code/datums/elements/forced_gravity.dm:9
    [DmParams("target", "gravity", "ignore_turf_gravity", "can_override")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object gravity = Dm.arg(__extra, 0) ?? 1;
        object ignore_turf_gravity = Dm.arg(__extra, 1) ?? FALSE;
        object can_override = Dm.arg(__extra, 2) ?? FALSE;
        object dot = null;
        dot = await base.Attach(target, Dm.forward(__extra, gravity, ignore_turf_gravity, can_override));
        if (!isatom(target))
            return ELEMENT_INCOMPATIBLE;

        object our_ref = REF(this);
        if (HAS_TRAIT_FROM(target, TRAIT_FORCED_GRAVITY, our_ref))
            return dot;

        this.gravity = gravity;
        this.ignore_turf_gravity = ignore_turf_gravity;

        RegisterSignal(target, COMSIG_ATOM_HAS_GRAVITY, nameof(gravity_check), @override: Dm.num(can_override));
        if (isturf(target))
            RegisterSignal(target, COMSIG_TURF_HAS_GRAVITY, nameof(turf_gravity_check), @override: Dm.num(can_override));

        ADD_TRAIT(target, TRAIT_FORCED_GRAVITY, our_ref);
        return dot;
    }

    // tg: code/datums/elements/forced_gravity.dm:27
    [DmParams("source")]
    public override object Detach(datum source = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Detach(source, __extra);
        UnregisterSignal(source, Detach__signals_b_gone);
        REMOVE_TRAIT(source, TRAIT_FORCED_GRAVITY, REF(this));
        return dot;
    }

    // tg: code/datums/elements/forced_gravity.dm:29  var/static/list/signals_b_gone (a proc-level static, rules 10.6)
    private static ref DmList Detach__signals_b_gone => ref TgState.Current.datum_element_forced_gravity__Detach__signals_b_gone;

    // tg: code/datums/elements/forced_gravity.dm:33
    [SignalHandler]
    public virtual object gravity_check(datum source = null, turf location = null, DmList gravs = null)
    {
        // SIGNAL_HANDLER

        if (!Dm.truthy(ignore_turf_gravity) && Dm.truthy(location.force_no_gravity))
            return FALSE;
        Dm.append(gravs, gravity);

        return TRUE;
    }

    // tg: code/datums/elements/forced_gravity.dm:42
    [SignalHandler]
    public virtual void turf_gravity_check(datum source = null, atom checker = null, DmList gravs = null)
    {
        // SIGNAL_HANDLER
        gravity_check(null, source as turf, gravs);
    }
}

public sealed partial class TgState
{
    // tg: code/datums/elements/forced_gravity.dm:29  var/static/list/signals_b_gone = list(COMSIG_ATOM_HAS_GRAVITY, COMSIG_TURF_HAS_GRAVITY)
    internal DmList datum_element_forced_gravity__Detach__signals_b_gone = Dm.list(COMSIG_ATOM_HAS_GRAVITY, COMSIG_TURF_HAS_GRAVITY);
}
