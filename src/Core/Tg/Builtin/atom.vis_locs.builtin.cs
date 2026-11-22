// Engine ("BYOND builtin") part of /atom: vis_locs, the reverse of vis_contents (BYOND ref /atom/var/vis_locs), and the
// appearance var (BYOND ref /atom/var/appearance). Not translated from a tg file. M4v slice 3: tg's flick_overlay_view
// (__HELPERS/game.dm:124) puts a flick visual into an atom's vis_contents and deletes it after its duration; tg's
// /atom/movable/Destroy takes it out of every vis_contents with `vis_locs = null` (atoms_movable.dm:268), and the visual
// takes its look with `visual.appearance = passed_appearance`.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;

namespace Anomaly.Core.Tg;

public partial class atom
{
    // The atoms whose vis_contents held this one when the index last looked (null: none); a holder that no longer holds it
    // is dropped when vis_locs is read. The index is filled lazily: a write to a vis_contents only marks its holder (the list
    // owner is told in the middle of a list operation and must not read the list), and a read of vis_locs first reads the
    // marked holders' lists (VisIndex).
    private List<atom> _vis_holders;

    /// <summary>
    /// BYOND <c>vis_locs</c> (BYOND ref <c>/atom/var/vis_locs</c>): the atoms whose <c>vis_contents</c> hold this one, the
    /// reverse of <see cref="vis_contents"/>; a new list on each read. Assigning null takes the atom out of all of them (tg's
    /// <c>/atom/movable/Destroy</c>, atoms_movable.dm:268); any other value is a runtime error (the var is read-only). Outside a
    /// world (no ambient <c>World</c>) the index is not kept and the list is empty.
    /// </summary>
    public DmList vis_locs
    {
        get
        {
            DmList result = Dm.list();
            List<atom> holders = dm_current_vis_holders();
            if (holders != null)
                foreach (atom holder in holders)
                    result.Add(holder);
            return result;
        }
        set
        {
            if (value != null)
                throw new DmRuntimeException("vis_locs is read-only (only null may be assigned)");
            List<atom> holders = dm_current_vis_holders();
            if (holders == null)
                return;
            foreach (atom holder in holders.ToArray())
                holder._vis_contents.RemoveAll(this);
            _vis_holders = null;
        }
    }

    /// <summary>
    /// BYOND <c>appearance</c> (BYOND ref <c>/atom/var/appearance</c>): assigning an appearance (an image, a mutable appearance,
    /// an atom or an appearance snapshot) sets every appearance var of the atom from it (icon, icon_state, dir, layer, plane,
    /// alpha, color, the pixel offsets, transform, appearance_flags, blend_mode, invisibility, the overlays and underlays, the filters, and
    /// a non-null name, and an appearance snapshot's name and description), each through its own setter so the presentation and replication hints hear of it. A read gives an
    /// immutable snapshot of the atom's look (<see cref="DmAppearance"/>). Null or anything else is a runtime error.
    /// </summary>
    // SHIM-GAP: BYOND's appearance also carries maptext, mouse_opacity, render_source/target and vis_flags; nothing ported
    // assigns an appearance that sets them (tg's flick_overlay_view sets mouse_opacity itself after the assignment, the greenscreen's
    // copy its vis_flags, status_display.dm:942).
    public object appearance
    {
        get => DmAppearance.Of(this);
        set
        {
            if (value is not IDmAppearance source)
                throw new DmRuntimeException("appearance: expected an image, a mutable appearance, an atom or an appearance");
            icon = source.icon;
            icon_state = source.icon_state;
            dir = source.dir;
            layer = source.layer;
            plane = source.plane;
            alpha = source.alpha;
            color = source.color;
            pixel_x = source.pixel_x;
            pixel_y = source.pixel_y;
            pixel_w = source.pixel_w;
            pixel_z = source.pixel_z;
            transform = source.transform;
            appearance_flags = source.appearance_flags;
            blend_mode = source.blend_mode;
            invisibility = source.invisibility;
            overlays = source.overlays?.Copy();
            underlays = source.underlays?.Copy();
            filters = source.filters; // the greenscreen's copy of a mob keeps its filters (status_display.dm:929)
            if (value is image { name: not null } named)
                name = named.name;
            else if (value is DmAppearance { name: not null } snapshot)
            {
                // an atom's look carries its name and description (the chameleon projector's dummy takes the scanned thing's)
                name = snapshot.name;
                desc = snapshot.desc;
            }
        }
    }

    // The holders that hold this atom now, after reading the vis_contents written since the last look (null: none).
    private List<atom> dm_current_vis_holders()
    {
        World world = World.CurrentOrNull;
        if (world != null)
            VisIndex.Of(world).Resolve();
        if (_vis_holders == null)
            return null;
        _vis_holders.RemoveAll(holder => !Holds(holder._vis_contents, this)); // a deleted holder's vis_contents were cut (Destroy)
        if (_vis_holders.Count == 0)
            _vis_holders = null;
        return _vis_holders;
    }

    // A write to this atom's vis_contents (its list owner, atom.overlays.builtin.cs): the holder is read at the next vis_locs.
    private void dm_vis_contents_written()
    {
        World world = World.CurrentOrNull;
        if (world != null)
            VisIndex.Of(world).Dirty.Add(this);
    }

    private static bool Holds(DmList list, atom item)
    {
        int count = list.Count;
        for (int i = 1; i <= count; i++)
            if (ReferenceEquals(list[(float)i], item))
                return true;
        return false;
    }

    // The per-world set of holders whose vis_contents were written since the last read of any vis_locs (World.GetExtension:
    // no static state).
    private sealed class VisIndex
    {
        internal readonly HashSet<atom> Dirty = new(ReferenceEqualityComparer.Instance);

        internal static VisIndex Of(World world) => world.GetExtension(static _ => new VisIndex());

        internal void Resolve()
        {
            if (Dirty.Count == 0)
                return;
            foreach (atom holder in Dirty)
            {
                DmList list = holder._vis_contents;
                int count = list.Count;
                for (int i = 1; i <= count; i++)
                {
                    if (list[(float)i] is not atom child)
                        continue;
                    child._vis_holders ??= new List<atom>(1);
                    if (!child._vis_holders.Exists(known => ReferenceEquals(known, holder)))
                        child._vis_holders.Add(holder);
                }
            }
            Dirty.Clear();
        }
    }
}
