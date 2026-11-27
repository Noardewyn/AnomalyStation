// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/gizmo/gizmodes/giz_filler.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the filler gizmo modes: the sputter (a shake that spills oil around, or a throw of the gizmo itself), the tool
// dispenser and the goop food dispenser (at most fifty of their things at once). The var blocks are generated (D111).

/// Shake around some and spill oil
// tg: code/modules/research/gizmo/gizmodes/giz_filler.dm:12
public partial class datum_gizpulse_sputter : datum_gizpulse
{
    // tg: code/modules/research/gizmo/gizmodes/giz_filler.dm:18
    public override async DmTask activate(atom_movable holder = null, datum_gizmodes master = null, datum_gizmo_interface @interface = null)
    {
        await DmTask.CompletedTask;
        playsound(holder, "sound/effects/splat.ogg", 30);
        foreach (turf_open tile in Dm.each<turf_open>(oview(oil_range, holder)))
        {
            if (prob(oil_chance))
                Dm.@new<obj_effect_decal_cleanable_blood_oil>(tile);
        }

        holder.Shake();
    }
}

// tg: code/modules/research/gizmo/gizmodes/giz_filler.dm:26
public partial class datum_gizpulse_throw_self : datum_gizpulse
{
    // tg: code/modules/research/gizmo/gizmodes/giz_filler.dm:26
    public override async DmTask activate(atom_movable holder = null, datum_gizmodes master = null, datum_gizmo_interface @interface = null)
    {
        await holder.throw_at(get_edge_target_turf(holder, Dm.num(pick(GLOB.alldirs))) as atom, 50, 1);
    }
}

// tg: code/modules/research/gizmo/gizmodes/giz_filler.dm:42
public partial class datum_gizpulse_dispense : datum_gizpulse
{
    // tg: code/modules/research/gizmo/gizmodes/giz_filler.dm:62
    public override async DmTask activate(atom_movable holder = null, datum_gizmodes master = null, datum_gizmo_interface @interface = null)
    {
        if (!Dm.truthy(object_to_spawn))
            object_to_spawn = pick_weight(possible_objects);

        // We don't technically track to make sure we dont make more than [max_objects] objects, we just delete whatever was made [max_objects] ago
        if (Dm.num(spawned_objects_weakrefs.len) > (next_object_position % max_objects))
            qdel(spawned_objects_weakrefs[Dm.text("[]", (next_object_position % max_objects) + 1)]);

        object new_object = Dm.@new(object_to_spawn as Type, get_turf(holder));
        await modify(new_object as atom_movable);
        spawned_objects_weakrefs[Dm.text("[]", (next_object_position % max_objects) + 1)] = WEAKREF(new_object as datum);
        next_object_position++;
    }

    // tg: code/modules/research/gizmo/gizmodes/giz_filler.dm:75
    public virtual async DmTask modify(atom_movable new_object = null)
    {
        await DmTask.CompletedTask;
        return;
    }
}

/// Food made with goop
// tg: code/modules/research/gizmo/gizmodes/giz_filler.dm:92
public partial class datum_gizpulse_dispense_food : datum_gizpulse_dispense
{
    // tg: code/modules/research/gizmo/gizmodes/giz_filler.dm:98
    public override async DmTask modify(atom_movable new_object = null)
    {
        await (new_object.reagents as datum_reagents).clear_reagents();
        // its goop all the way down
        await (new_object.reagents as datum_reagents).add_reagent(typeof(datum_reagent_consumable_gizmo_goop), 5);
    }
}
