// Translated from tgstation (snapshot 1d99bd7f): code/datums/screentips/item_context.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4a P1: the whole file (D125, docs/ai/specs/interaction.md 6).

// tg: code/datums/screentips/item_context.dm:5
public partial class obj_item : obj
{
    /// Create a "Type-A" contextual screentip interaction, registering to `add_item_context()`.
    /// This will run `add_item_context()` when the item hovers over another object for context.
    /// `add_item_context()` will *not* be called unless this is run.
    /// This is not necessary for Type-A interactions, as you can just apply the flag and register to the signal yourself.
    // tg: code/datums/screentips/item_context.dm:5
    public virtual void register_item_context()
    {
        Dm.combine(ref item_flags, ITEM_HAS_CONTEXTUAL_SCREENTIPS);
        RegisterSignal(
            this,
            COMSIG_ITEM_REQUESTING_CONTEXT_FOR_TARGET,
            nameof(add_item_context)
        );
    }

    /// Creates a "Type-A" contextual screentip interaction.
    /// When a user hovers over something with this item in hand, this proc will be called in order
    /// to provide context for contextual screentips.
    /// You must call `register_item_context()` in order for this to be registered.
    /// A screentip context list is a list that has context keys (SCREENTIP_CONTEXT_*, from __DEFINES/screentips.dm)
    /// that map to the action as text.
    /// If you mutate the list in this signal, you must return CONTEXTUAL_SCREENTIP_SET.
    /// `source` can, in all cases, be replaced with `src`, and only exists because this proc directly connects to a signal.
    // tg: code/datums/screentips/item_context.dm:21
    [SignalHandler]
    public virtual object add_item_context(obj_item source = null, DmList context = null, atom target = null, mob_living user = null)
    {
        return NONE;
    }
}
