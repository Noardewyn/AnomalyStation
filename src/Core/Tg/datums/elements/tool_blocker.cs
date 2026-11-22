// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/tool_blocker.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps G2 (D291): the whole file (the metal foam plating blocks welders).
//
// GAP(types): tg's `var/action_type` (:8) holds a bitflag (TOOL_ACT_PRIMARY, TOOL_ACT_SECONDARY); the generator types it `Type`,
// so it is declared here as the DM value it is, with the type's constructor.

/// Blocks using the passed tool type on this atom
// tg: code/datums/elements/tool_blocker.dm:2
public partial class datum_element_tool_blocker : datum_element
{
    /// Bitflag representing which tool_acts to block
    // tg: code/datums/elements/tool_blocker.dm:8  (hand-written: see the GAP(types) above)
    public object action_type;

    // tg: code/datums/elements/tool_blocker.dm:2  (the type's overrides, hand-written with the var above)
    public datum_element_tool_blocker()
    {
        element_flags = ELEMENT_BESPOKE;
        argument_hash_start_idx = 2;
    }

    // tg: code/datums/elements/tool_blocker.dm:10
    [DmParams("target", "tool_type", "action_type")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object tool_type = Dm.arg(__extra, 0);
        object action_type = Dm.arg(__extra, 1) ?? TOOL_ACT_ALL;
        object dot = await base.Attach(target, Dm.forward(__extra, tool_type, action_type));
        if (tool_type == null || !Dm.truthy(Dm.bitand(action_type, TOOL_ACT_ALL)))
            return ELEMENT_INCOMPATIBLE;

        this.tool_type = tool_type;
        this.action_type = action_type;

        DmList signals_to_register = Dm.list();

        if (Dm.truthy(Dm.bitand(action_type, TOOL_ACT_PRIMARY)))
            Dm.append(ref signals_to_register, COMSIG_ATOM_TOOL_ACT(tool_type));
        if (Dm.truthy(Dm.bitand(action_type, TOOL_ACT_SECONDARY)))
            Dm.append(ref signals_to_register, COMSIG_ATOM_SECONDARY_TOOL_ACT(tool_type));
        if (isturf(target))
            RegisterSignal(target, COMSIG_QDELETING, nameof(on_turf_deleted), @override: TRUE);

        RegisterSignals(target, signals_to_register, nameof(block_tool));
        return dot;
    }

    // tg: code/datums/elements/tool_blocker.dm:28
    [DmParams("source")]
    public override object Detach(datum source = null, params object[] __extra)
    {
        DmList signals_to_unregister = Dm.list();

        if (Dm.truthy(Dm.bitand(action_type, TOOL_ACT_PRIMARY)))
            Dm.append(ref signals_to_unregister, COMSIG_ATOM_TOOL_ACT(tool_type));
        if (Dm.truthy(Dm.bitand(action_type, TOOL_ACT_SECONDARY)))
            Dm.append(ref signals_to_unregister, COMSIG_ATOM_SECONDARY_TOOL_ACT(tool_type));
        if (isturf(source))
            Dm.append(ref signals_to_unregister, COMSIG_QDELETING);
        UnregisterSignal(source, signals_to_unregister);

        return base.Detach(source, __extra);
    }

    // tg: code/datums/elements/tool_blocker.dm:41  (SIGNAL_HANDLER is dropped, rules 7.9)
    [SignalHandler]
    public virtual object block_tool(params object[] __extra)
    {
        return ITEM_INTERACT_SKIP_TO_ATTACK;
    }

    // tg: code/datums/elements/tool_blocker.dm:45  (SIGNAL_HANDLER is dropped, rules 7.9)
    [SignalHandler]
    public virtual void on_turf_deleted(datum source = null)
    {
        Detach(source);
    }
}
