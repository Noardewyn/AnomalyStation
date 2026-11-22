// Translated from tgstation (snapshot 1d99bd7f): code/datums/screentips/atom_context.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4a P1: the whole file (D125, docs/ai/specs/interaction.md 6).

// tg: code/datums/screentips/atom_context.dm:5
public partial class atom : datum
{
    /// Create a "Type-B" contextual screentip interaction, registering to `add_context()`.
    /// This will run `add_context()` when the atom is hovered over by an item for context.
    /// `add_context()` will *not* be called unless this is run.
    /// This is not necessary for Type-B interactions, as you can just apply the flag and register to the signal yourself.
    // tg: code/datums/screentips/atom_context.dm:5
    public virtual void register_context()
    {
        Dm.combine(ref flags_1, HAS_CONTEXTUAL_SCREENTIPS_1);
        RegisterSignal(this, COMSIG_ATOM_REQUESTING_CONTEXT_FROM_ITEM, nameof(add_context));
    }

    /// Creates a "Type-B" contextual screentip interaction.
    /// When a user hovers over this, this proc will be called in order
    /// to provide context for contextual screentips.
    /// You must call `register_context()` in order for this to be registered.
    /// A screentip context list is a list that has context keys (SCREENTIP_CONTEXT_*, from __DEFINES/screentips.dm)
    /// that map to the action as text.
    /// If you mutate the list in this signal, you must return CONTEXTUAL_SCREENTIP_SET.
    /// `source` can, in all cases, be replaced with `src`, and only exists because this proc directly connects to a signal.
    // tg: code/datums/screentips/atom_context.dm:17
    [SignalHandler]
    public virtual object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        return NONE;
    }
}
