// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/empprotection.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M7 EMP slice). The var block and the element flags are generated (D111).

// tg: code/datums/elements/empprotection.dm:2
public partial class datum_element_empprotection : datum_element
{
    // tg: code/datums/elements/empprotection.dm:6
    [DmParams("target", "_flags")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object _flags = Dm.arg(__extra, 0);
        object dot = null;
        dot = await base.Attach(target, Dm.forward(__extra, _flags));
        if (Dm.eq(dot, ELEMENT_INCOMPATIBLE) || !isatom(target))
            return ELEMENT_INCOMPATIBLE;
        flags = Dm.num(_flags);
        // COMSIG_ATOM_PRE_EMP_ACT and COMSIG_ATOM_EMP_ACT are the same string in tg ("atom_emp_act", signals_atom_x_act.dm:12,14),
        // so this handler also answers the second signal of atom/emp_act, whose result emp_act ignores.
        RegisterSignal(target, COMSIG_ATOM_PRE_EMP_ACT, nameof(getEmpFlags));
        RegisterSignal(target, COMSIG_ATOM_EXAMINE_TAGS, nameof(get_examine_tags));
        return dot;
    }

    // tg: code/datums/elements/empprotection.dm:14
    [DmParams("target")]
    public override object Detach(datum target = null, params object[] __extra)
    {
        UnregisterSignal(target, Dm.list(COMSIG_ATOM_PRE_EMP_ACT, COMSIG_ATOM_EXAMINE_TAGS));
        return base.Detach(target, __extra);
    }

    // tg: code/datums/elements/empprotection.dm:18
    [SignalHandler]
    public virtual object getEmpFlags(datum source = null, object severity = null)
    {
        // SIGNAL_HANDLER

        return Dm.bitand(flags, EMP_PROTECT_ALL);
    }

    // tg: code/datums/elements/empprotection.dm:23
    [SignalHandler]
    public virtual void get_examine_tags(atom source = null, mob user = null, DmList examine_list = null)
    {
        // SIGNAL_HANDLER

        if (Dm.truthy(Dm.bitand(flags, EMP_NO_EXAMINE)))
            return;

        if (Dm.eq(Dm.bitand(flags, EMP_PROTECT_ALL), EMP_PROTECT_ALL))
        {
            examine_list["EMP proof"] = I18n.T("[] [] unaffected by electromagnetic pulses, and shields [] contents and wiring from them.", source.p_They(), source.p_are(), source.p_their());
            return;
        }

        if (Dm.truthy(Dm.bitand(flags, EMP_PROTECT_SELF)))
            examine_list["EMP resilient"] = I18n.T("[] [] unaffected by electromagnetic pulses.", source.p_They(), source.p_are());

        if (Dm.eq(Dm.bitand(flags, Dm.bitor(EMP_PROTECT_CONTENTS, EMP_PROTECT_WIRES)), Dm.bitor(EMP_PROTECT_CONTENTS, EMP_PROTECT_WIRES)))
            examine_list["partially EMP blocking"] = I18n.T("[] protects [] wiring and contents from electromagnetic pulses.", source.p_They(), source.p_their());

        else if (Dm.truthy(Dm.bitand(flags, EMP_PROTECT_CONTENTS)))
            examine_list["partially EMP blocking"] = I18n.T("[] protects [] contents from electromagnetic pulses.", source.p_They(), source.p_their());

        else if (Dm.truthy(Dm.bitand(flags, EMP_PROTECT_WIRES)))
            examine_list["partially EMP blocking"] = I18n.T("[] protects [] wiring from electromagnetic pulses.", source.p_They(), source.p_their());
    }
}
