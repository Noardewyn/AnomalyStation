// Translated from tgstation (snapshot 1d99bd7f): code/modules/lootpanel/contents.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps package G1 (D291): the whole file. Besides its slot, add_new_searchable takes an atom (tg types it atom/movable, and passes the
// source turf to it at :18).

public partial class datum_lootpanel
{
    /// Adds the item to contents and to_image (if needed)
    // tg: code/modules/lootpanel/contents.dm:2
    public virtual void add_to_index(datum_search_object index = null)
    {
        RegisterSignal(index, COMSIG_QDELETING, nameof(on_searchable_deleted));
        if (index.icon == null)
            Dm.append(to_image, index);

        Dm.append(contents, index);
    }

    /// Used to populate contents and start generating if needed
    // tg: code/modules/lootpanel/contents.dm:11
    public virtual async DmTask populate_contents()
    {
        if (Dm.truthy(length(contents)))
            reset_contents();

        // Add source turf first
        await add_new_searchable(source_turf, FALSE);

        foreach (atom thing in Dm.each<atom>(source_turf))
            await add_new_searchable(thing, FALSE);

        await queue_update();
    }

    // tg: code/modules/lootpanel/contents.dm:23
    [SignalHandler]
    public virtual void on_source_turf_entered(datum source = null, atom_movable arrived = null, atom old_loc = null, DmList old_locs = null)
    {
        // async because the same move handler we register with this can trigger on the /datum/search_object later in this event
        // deleting it just as we added it
        _addtimer(Dm.@new<datum_callback>(this, nameof(add_new_searchable), arrived, TRUE), 0, file: "code/modules/lootpanel/contents.dm", line: 27);
    }

    // tg: code/modules/lootpanel/contents.dm:30
    public virtual async DmTask add_new_searchable(atom thing = null, object from_signal = null)
    {
        if (QDELETED(thing))
            return;
        if (Dm.eq(thing.mouse_opacity, MOUSE_OPACITY_TRANSPARENT))
            return;
        if (Dm.truthy(thing.IsObscured()))
            return;
        if (Dm.num(thing.invisibility) > Dm.num(owner.mob.see_invisible))
            return;
        if (Dm.truthy(from_signal) && (source_turf == null || !Dm.@in(thing, source_turf.contents)))
            return;

        // convert
        datum_search_object index = Dm.@new<datum_search_object>(owner, thing);
        add_to_index(index);

        if (Dm.truthy(from_signal))
            await queue_update();
    }

    // tg: code/modules/lootpanel/contents.dm:49
    public virtual async DmTask queue_update()
    {
        datum_tgui window = SStgui.get_open_ui(owner.mob, this) as datum_tgui;
        if (window != null)
            await window.send_update();

        if (Dm.truthy(length(to_image)))
            Dm.append(SSlooting.backlog, this);
    }

    /// For: Resetting to empty. Ignores the searchable qdel event
    // tg: code/modules/lootpanel/contents.dm:58
    public virtual void reset_contents()
    {
        foreach (datum_search_object index in Dm.each_cast<datum_search_object>(contents))
        {
            Dm.remove(ref contents, index);
            Dm.remove(ref to_image, index);

            if (QDELETED(index))
                continue;

            UnregisterSignal(index, COMSIG_QDELETING);
            qdel(index);
        }
    }
}
