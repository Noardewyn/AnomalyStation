// Engine ("BYOND builtin") part of /atom: the overlay snapshot (M2 P3; docs/ai/specs/m2-ownership.md "the overlay
// snapshot"; docs/ai/specs/dm-conformance.md "M2 P3"). Not translated from a tg file: BYOND keeps an immutable snapshot of
// what is added to an atom's overlays and underlays (BYOND ref /atom/var/overlays: "When another object is used as an
// overlay, a static "snapshot" of the object is taken at the time when the overlay is created"), and the engine lists of
// atom.builtin.cs hold DmAppearance snapshots through DmList.dm_convert.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;

namespace Anomaly.Core.Tg;

public partial class atom : datum
{
    /// <summary>
    /// The pre-New hook's var initializers of /atom (rules 10.2): tg's /atom has no object-creating var default, so this
    /// override only installs the engine's overlay conversion on the atom's <c>overlays</c> and <c>underlays</c>
    /// (<see cref="DmAppearance.OverlayItem"/>: an image, a mutable appearance, an atom or an atom type becomes its interned
    /// snapshot; text and icons stay as they are) and their owner (an in-place edit is an appearance change), before any
    /// DM code of the atom runs. A prototype (built by its constructor alone, for <c>initial()</c>) keeps plain lists, which
    /// nothing adds to.
    /// </summary>
    // rules 10.2 (settled at the M2 integration): rules 10.2 reserve dm_init_vars for var defaults that create objects; the engine's own per-instance setup
    // of a builtin list has no other hook in a partial engine file (atom.builtin.cs's constructor and field initializers
    // are the lead's), so it is done here, first, before the tg overrides' initializers (they call base first).
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        _overlays.dm_convert = DmAppearance.OverlayConverter;
        _underlays.dm_convert = DmAppearance.OverlayConverter;
        _overlays.dm_owner ??= new OverlayListOwner(this, AppearanceAspect.Overlays);
        _underlays.dm_owner ??= new OverlayListOwner(this, AppearanceAspect.Underlays);
        // M2 integration: an in-place edit of vis_contents (tg's health doll Cuts and adds its limbs) is an appearance change
        // too; the replicator draws an atom's vis_contents as part of it.
        vis_contents.dm_owner ??= new OverlayListOwner(this, AppearanceAspect.VisContents);
    }

    /// <summary>
    /// The engine owner of an atom's <c>overlays</c> or <c>underlays</c>: an in-place edit by DM code (<c>overlays += x</c>,
    /// <c>Add</c>, <c>Cut</c>, <c>-=</c>: rules 4.3 change the list in place) is an appearance change of the atom, as an
    /// assignment of the list is (atom.builtin.cs), so the presentation sink and the replication hint hear of it in the
    /// tick tg makes it (docs/ai/specs/replication.md 5.3, the request to M2 P3). <see cref="IDmListOwner.ListChanged"/> runs
    /// after every write, possibly in the middle of a list operation, and only raises the dirty hint (presentation-seam.md
    /// section 1 rule 5): it never reads the list.
    /// </summary>
    private sealed class OverlayListOwner : IDmListOwner
    {
        private readonly atom _atom;
        private readonly AppearanceAspect _aspect;

        internal OverlayListOwner(atom owner, AppearanceAspect aspect)
        {
            _atom = owner;
            _aspect = aspect;
        }

        void IDmListOwner.ListChanged(DmList list)
        {
            if (_aspect == AppearanceAspect.VisContents)
                _atom.dm_vis_contents_written(); // M4v slice 3: the vis_locs index (atom.vis_locs.builtin.cs)
            _atom.dm_appearance_changed(_aspect);
        }
    }
}
