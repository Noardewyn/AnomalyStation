// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/screentips/contextual_screentip_bare_hands.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4a P1: the whole file (D125, docs/ai/specs/interaction.md 6). The var block is generated.

/// Apply basic contextual screentips when the user hovers over this item with an empty hand.
/// A "Type B" interaction.
/// This stacks with other contextual screentip elements, though you may want to register the signal/flag manually at that point for performance.
// tg: code/datums/elements/screentips/contextual_screentip_bare_hands.dm:4
public partial class datum_element_contextual_screentip_bare_hands : datum_element
{
    // tg: code/datums/elements/screentips/contextual_screentip_bare_hands.dm:22
    [DmParams("target", "use_named_parameters", "lmb_text", "rmb_text", "lmb_text_combat_mode", "rmb_text_combat_mode")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object use_named_parameters = Dm.arg(__extra, 0);
        object lmb_text = Dm.arg(__extra, 1);
        object rmb_text = Dm.arg(__extra, 2);
        object lmb_text_combat_mode = Dm.arg(__extra, 3);
        object rmb_text_combat_mode = Dm.arg(__extra, 4);
        object dot = null;
        dot = await base.Attach(target, Dm.forward(__extra, use_named_parameters, lmb_text, rmb_text, lmb_text_combat_mode, rmb_text_combat_mode));
        if (!isatom(target))
            return ELEMENT_INCOMPATIBLE;

        if (use_named_parameters != null)
            throw Dm.CRASH("Use named parameters instead of positional ones.", "code/datums/elements/screentips/contextual_screentip_bare_hands.dm", 36);

        this.lmb_text = lmb_text;
        this.rmb_text = rmb_text;
        this.lmb_text_combat_mode = (lmb_text_combat_mode is var or__1 && Dm.truthy(or__1) ? or__1 : lmb_text);
        this.rmb_text_combat_mode = (rmb_text_combat_mode is var or__2 && Dm.truthy(or__2) ? or__2 : rmb_text);

        atom atom_target = target as atom;
        Dm.combine(ref atom_target.flags_1, HAS_CONTEXTUAL_SCREENTIPS_1);
        RegisterSignal(atom_target, COMSIG_ATOM_REQUESTING_CONTEXT_FROM_ITEM, nameof(on_requesting_context_from_item));
        return dot;
    }

    // tg: code/datums/elements/screentips/contextual_screentip_bare_hands.dm:46
    public override object Detach(datum source = null, params object[] __extra)
    {
        UnregisterSignal(source, COMSIG_ATOM_REQUESTING_CONTEXT_FROM_ITEM);
        return base.Detach(source, __extra);
    }

    // tg: code/datums/elements/screentips/contextual_screentip_bare_hands.dm:56
    [SignalHandler]
    public virtual object on_requesting_context_from_item(datum source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        object dot = null;
        if (!isliving(user))
            return dot;

        if (held_item != null)
            return NONE;

        mob_living living_user = user as mob_living;

        if (lmb_text != null)
            context[SCREENTIP_CONTEXT_LMB] = Dm.truthy(living_user.combat_mode) ? lmb_text_combat_mode : lmb_text;

        if (rmb_text != null)
            context[SCREENTIP_CONTEXT_RMB] = Dm.truthy(living_user.combat_mode) ? rmb_text_combat_mode : rmb_text;

        return CONTEXTUAL_SCREENTIP_SET;
    }
}
