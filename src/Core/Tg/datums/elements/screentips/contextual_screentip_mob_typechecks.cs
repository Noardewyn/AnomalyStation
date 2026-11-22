// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/screentips/contextual_screentip_mob_typechecks.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4a P1: the whole file (D125, docs/ai/specs/interaction.md 6). The var block is generated.

// tg: code/datums/elements/screentips/contextual_screentip_mob_typechecks.dm:4
public partial class datum_element_contextual_screentip_mob_typechecks : datum_element
{
    // tg: code/datums/elements/screentips/contextual_screentip_mob_typechecks.dm:11
    [DmParams("target", "mob_paths_to_contexts")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object mob_paths_to_contexts = Dm.arg(__extra, 0);
        object dot = null;
        dot = await base.Attach(target, Dm.forward(__extra, mob_paths_to_contexts));
        if (!isatom(target))
            return ELEMENT_INCOMPATIBLE;

        this.mob_paths_to_contexts = mob_paths_to_contexts as DmList;

        atom atom_target = target as atom;
        Dm.combine(ref atom_target.flags_1, HAS_CONTEXTUAL_SCREENTIPS_1);
        RegisterSignal(atom_target, COMSIG_ATOM_REQUESTING_CONTEXT_FROM_ITEM, nameof(on_requesting_context_from_mob));
        return dot;
    }

    // tg: code/datums/elements/screentips/contextual_screentip_mob_typechecks.dm:22
    public override object Detach(datum source = null, params object[] __extra)
    {
        UnregisterSignal(source, COMSIG_ATOM_REQUESTING_CONTEXT_FROM_ITEM);
        return base.Detach(source, __extra);
    }

    // tg: code/datums/elements/screentips/contextual_screentip_mob_typechecks.dm:32
    [SignalHandler]
    public virtual object on_requesting_context_from_mob(datum source = null, DmList context = null, obj_item held_item = null, mob context_mob = null)
    {
        if (context_mob == null)
            return NONE;

        foreach (object mob_path in Dm.each(mob_paths_to_contexts))
        {
            if (istype(context_mob, mob_path as Type))
            {
                Dm.append(ref context, mob_paths_to_contexts[mob_path]);
                return CONTEXTUAL_SCREENTIP_SET;
            }
        }

        return NONE;
    }
}
