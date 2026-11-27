// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/gizmo/gizmodes/gizcopier.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the copier gizmo mode: it scans a loose thing beside it, makes copies that look like it (an item's copy can be
// picked up, a structure's or a mob's not; at most fifty at once) and erases them; a copy fizzles out under an EMP. The var blocks
// are generated (D111).

/// Scan the nearest mob/object
// tg: code/modules/research/gizmo/gizmodes/gizcopier.dm:17
public partial class datum_gizpulse_scan : datum_gizpulse
{
    // tg: code/modules/research/gizmo/gizmodes/gizcopier.dm:17
    public override async DmTask activate(atom_movable holder = null, datum_gizmodes master = null, datum_gizmo_interface @interface = null)
    {
        await DmTask.CompletedTask;
        if (!istype(master, typeof(datum_gizmodes_copier)))
            return;

        datum_gizmodes_copier copier = master as datum_gizmodes_copier;

        foreach (atom_movable candidate in Dm.each<atom_movable>(oview(1, holder)))
        {
            if (Dm.truthy(candidate.anchored) || HAS_TRAIT(candidate, TRAIT_UNDERFLOOR)) // skips most undertile and hidden objects
                continue;

            copier.marked = WEAKREF(candidate) as datum_weakref;
            playsound(null, "sound/items/weapons/flash.ogg", 80); //give some feedback that *something* happened (tg plays it on the pulse datum, which has no turf: no sound)
            return;
        }
    }
}

/// Make a copy of whatever you previously scanned
// tg: code/modules/research/gizmo/gizmodes/gizcopier.dm:32
public partial class datum_gizpulse_copy : datum_gizpulse
{
    // tg: code/modules/research/gizmo/gizmodes/gizcopier.dm:36
    public override async DmTask activate(atom_movable holder = null, datum_gizmodes master = null, datum_gizmo_interface @interface = null)
    {
        await DmTask.CompletedTask;
        if (!istype(master, typeof(datum_gizmodes_copier)))
            return;

        datum_gizmodes_copier copier = master as datum_gizmodes_copier;
        copies = copier.copies;

        atom_movable object_to_copy = copier.marked?.resolve() as atom_movable;

        if (object_to_copy == null)
            return;

        // So you can pick up copied items but not copied structures and mobs
        obj_item copy = Dm.@new<obj_item_gizmo_copy>(get_turf(holder));
        if (!isitem(object_to_copy))
            copy.interaction_flags_item = NONE; //so you cant pick it up anymore

        copy.appearance = object_to_copy.appearance;
        copy.density = object_to_copy.density;

        copies.Add(copy);

        if (Dm.num(copies.len) > copier.max_copies)
        {
            obj_item_gizmo_copy copy_to_delete = copies[1] as obj_item_gizmo_copy;
            qdel(copy_to_delete); //it gets removed from the list on del
        }

        RegisterSignal(copy, COMSIG_QDELETING, nameof(remove_from_list));
    }

    /// Remove a copy from a list if they're deleted
    // tg: code/modules/research/gizmo/gizmodes/gizcopier.dm:65
    [SignalHandler]
    public virtual void remove_from_list(datum source = null)
    {
        copies.Remove(source);
    }
}

/// Wipe all current copies
// tg: code/modules/research/gizmo/gizmodes/gizcopier.dm:71
public partial class datum_gizpulse_erase : datum_gizpulse
{
    // tg: code/modules/research/gizmo/gizmodes/gizcopier.dm:71
    public override async DmTask activate(atom_movable holder = null, datum_gizmodes master = null, datum_gizmo_interface @interface = null)
    {
        await DmTask.CompletedTask;
        if (!istype(master, typeof(datum_gizmodes_copier)))
            return;

        datum_gizmodes_copier copier = master as datum_gizmodes_copier;

        foreach (object copy in Dm.each(copier.copies))
            qdel(copy);

        copier.copies.Cut();
    }
}

// tg: code/modules/research/gizmo/gizmodes/gizcopier.dm:82
public partial class obj_item_gizmo_copy : obj_item
{
    // tg: code/modules/research/gizmo/gizmodes/gizcopier.dm:82
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);

        do_sparks(2, FALSE, null); // tg gives the text "gizmo copy" as the source, which has no turf
        qdel(this);
        return dot;
    }
}
