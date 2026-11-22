// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/screentips/contextual_screentip_tools.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4a P1: the whole file (D125, docs/ai/specs/interaction.md 6). The var block is generated.

// tg: code/datums/elements/screentips/contextual_screentip_tools.dm:4
public partial class datum_element_contextual_screentip_tools : datum_element
{
    // tg: code/datums/elements/screentips/contextual_screentip_tools.dm:11
    [DmParams("target", "tool_behaviors")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object tool_behaviors = Dm.arg(__extra, 0);
        object dot = null;
        dot = await base.Attach(target, Dm.forward(__extra, tool_behaviors));
        if (!isatom(target))
            return ELEMENT_INCOMPATIBLE;

        this.tool_behaviors = tool_behaviors as DmList;

        atom atom_target = target as atom;
        Dm.combine(ref atom_target.flags_1, HAS_CONTEXTUAL_SCREENTIPS_1);
        RegisterSignal(atom_target, COMSIG_ATOM_REQUESTING_CONTEXT_FROM_ITEM, nameof(on_requesting_context_from_item));
        return dot;
    }

    // tg: code/datums/elements/screentips/contextual_screentip_tools.dm:22
    public override object Detach(datum source = null, params object[] __extra)
    {
        UnregisterSignal(source, COMSIG_ATOM_REQUESTING_CONTEXT_FROM_ITEM);
        return base.Detach(source, __extra);
    }

    // tg: code/datums/elements/screentips/contextual_screentip_tools.dm:32
    [SignalHandler]
    public virtual object on_requesting_context_from_item(datum source = null, DmList context = null, obj_item held_item = null)
    {
        if (held_item == null)
            return NONE;

        object tool_behavior = held_item.tool_behaviour;
        if (!Dm.@in(tool_behavior, tool_behaviors))
            return NONE;

        Dm.append(ref context, tool_behaviors[tool_behavior]);
        return CONTEXTUAL_SCREENTIP_SET;
    }
}
