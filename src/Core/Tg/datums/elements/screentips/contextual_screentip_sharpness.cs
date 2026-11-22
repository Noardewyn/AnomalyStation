// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/screentips/contextual_screentip_sharpness.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4a P1: the whole file (D125, docs/ai/specs/interaction.md 6). The var block is generated.

// tg: code/datums/elements/screentips/contextual_screentip_sharpness.dm:4
public partial class datum_element_contextual_screentip_sharpness : datum_element
{
    // tg: code/datums/elements/screentips/contextual_screentip_sharpness.dm:14
    [DmParams("target", "lmb_text", "rmb_text")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object lmb_text = Dm.arg(__extra, 0);
        object rmb_text = Dm.arg(__extra, 1);
        object dot = null;
        dot = await base.Attach(target, Dm.forward(__extra, lmb_text, rmb_text));
        if (!isatom(target))
            return ELEMENT_INCOMPATIBLE;

        this.lmb_text = lmb_text;
        this.rmb_text = rmb_text;

        atom atom_target = target as atom;
        Dm.combine(ref atom_target.flags_1, HAS_CONTEXTUAL_SCREENTIPS_1);
        RegisterSignal(atom_target, COMSIG_ATOM_REQUESTING_CONTEXT_FROM_ITEM, nameof(on_requesting_context_from_item));
        return dot;
    }

    // tg: code/datums/elements/screentips/contextual_screentip_sharpness.dm:26
    public override object Detach(datum source = null, params object[] __extra)
    {
        UnregisterSignal(source, COMSIG_ATOM_REQUESTING_CONTEXT_FROM_ITEM);
        return base.Detach(source, __extra);
    }

    // tg: code/datums/elements/screentips/contextual_screentip_sharpness.dm:36
    [SignalHandler]
    public virtual object on_requesting_context_from_item(datum source = null, DmList context = null, obj_item held_item = null)
    {
        if (held_item == null)
            return NONE;

        object sharpness = held_item.get_sharpness();
        if (!Dm.truthy(sharpness))
            return NONE;

        if (lmb_text != null)
            context[SCREENTIP_CONTEXT_LMB] = lmb_text;

        if (rmb_text != null)
            context[SCREENTIP_CONTEXT_RMB] = rmb_text;

        return CONTEXTUAL_SCREENTIP_SET;
    }
}
