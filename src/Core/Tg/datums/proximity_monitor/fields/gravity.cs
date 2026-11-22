// Translated from tgstation (snapshot 1d99bd7f): code/datums/proximity_monitor/fields/gravity.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// /datum/proximity_monitor/advanced/gravity and its warns_on_entrance subtype (M4c slice 8: the gravity generator's graviton field,
// gravitygenerator.dm:345); the subtle_effect subtype and /obj/effect/gravity_fluff_field (:67-125, the portable gravity unit's field,
// engineering E16), whose particles and emissive glow are not drawn (below). The var blocks are generated (D111).

// Proximity monitor applies forced gravity to all turfs in range.
// tg: code/datums/proximity_monitor/fields/gravity.dm:2
public partial class datum_proximity_monitor_advanced_gravity : datum_proximity_monitor_advanced
{
    // tg: code/datums/proximity_monitor/fields/gravity.dm:7
    [DmParams("_host", "range", "_ignore_if_not_on_turf", "gravity")]
    public override async DmTask New(params object[] __extra)
    {
        object gravity = Dm.arg<object>(__extra, 3);
        await base.New(__extra);
        gravity_value = Dm.num(gravity);
        recalculate_field(full_recalc: TRUE);
    }

    // tg: code/datums/proximity_monitor/fields/gravity.dm:12
    public override void setup_field_turf(turf target = null)
    {
        base.setup_field_turf(target);
        if (modified_turfs[target] != null)
            return;
        if (HAS_TRAIT(target, TRAIT_FORCED_GRAVITY))
            return;
        Waiting.NoSleep(target.AddElement(typeof(datum_element_forced_gravity), gravity_value, Dm.named("can_override", TRUE)));
        modified_turfs[target] = gravity_value;
    }

    // tg: code/datums/proximity_monitor/fields/gravity.dm:21
    public override void cleanup_field_turf(turf target = null)
    {
        base.cleanup_field_turf(target);
        if (modified_turfs[target] == null)
            return;
        object grav_value = modified_turfs[target] is var v && Dm.truthy(v) ? v : 0;
        target.RemoveElement(typeof(datum_element_forced_gravity), grav_value, Dm.named("can_override", TRUE));
        Dm.remove(ref modified_turfs, target);
    }
}

// Subtype which pops up a balloon alert when a mob enters the field
// tg: code/datums/proximity_monitor/fields/gravity.dm:30
public partial class datum_proximity_monitor_advanced_gravity_warns_on_entrance : datum_proximity_monitor_advanced_gravity
{
    // tg: code/datums/proximity_monitor/fields/gravity.dm:35
    public override void setup_field_turf(turf target = null)
    {
        base.setup_field_turf(target);
        foreach (mob_living guy in Dm.each_cast<mob_living>(target.contents))
            warn_mob(guy, target);
    }

    // tg: code/datums/proximity_monitor/fields/gravity.dm:40
    public override void cleanup_field_turf(turf target = null)
    {
        base.cleanup_field_turf(target);
        foreach (mob_living guy in Dm.each_cast<mob_living>(target.contents))
            warn_mob(guy, target);
    }

    // tg: code/datums/proximity_monitor/fields/gravity.dm:45
    public override void field_edge_crossed(atom_movable movable = null, turf old_location = null, turf new_location = null)
    {
        base.field_edge_crossed(movable, old_location, new_location);
        if (isliving(movable))
            warn_mob((mob_living)movable, new_location);
    }

    // tg: code/datums/proximity_monitor/fields/gravity.dm:50
    public override void field_edge_uncrossed(atom_movable movable = null, turf old_location = null, turf new_location = null)
    {
        base.field_edge_uncrossed(movable, old_location, new_location);
        if (isliving(movable))
            warn_mob((mob_living)movable, old_location);
    }

    // tg: code/datums/proximity_monitor/fields/gravity.dm:55
    public virtual void warn_mob(mob_living to_warn = null, turf location = null)
    {
        object mob_ref_key = REF(to_warn);
        if (Dm.@in(mob_ref_key, recently_warned))
            return;

        location.balloon_alert(to_warn, I18n.T("gravity []", Dm.@in(location, modified_turfs) ? I18n.T("shifts!") : I18n.T("reverts...")));
        LAZYADD(ref recently_warned, mob_ref_key);
        _addtimer(Dm.@new<datum_callback>(this, nameof(clear_recent_warning), mob_ref_key), 3 * SECONDS, file: "code/datums/proximity_monitor/fields/gravity.dm", line: 62);
    }

    // tg: code/datums/proximity_monitor/fields/gravity.dm:64
    public virtual void clear_recent_warning(object mob_ref_key = null)
    {
        LAZYREMOVE(ref recently_warned, mob_ref_key);
    }
}

// Engineering E16 (the portable gravity unit): :67-125, the field's fluff tile and the subtype that lays one on every open turf of the
// field. The tile smooths with its neighbours into tg's grav_field outline and takes the colour of its strength's particles; the
// particles themselves (add_shared_particles, /particles/grav_field_*) and the tile's emissive copy are not drawn (particles are not
// ported; DEVIATION DV12).

// tg: code/datums/proximity_monitor/fields/gravity.dm:67
public partial class obj_effect_gravity_fluff_field : obj_effect
{
    // tg: code/datums/proximity_monitor/fields/gravity.dm:82
    [DmParams("mapload", "strength")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object strength = Dm.arg<object>(__extra, 0);
        object dot = base.Initialize(mapload, __extra);
        if (strength == null)
            return INITIALIZE_HINT_QDEL;
        QUEUE_SMOOTH(this);
        QUEUE_SMOOTH_NEIGHBORS(this);
        float s = Dm.num(strength);
        if (s >= 2)
            particle_type = typeof(particles_grav_field_down_strong);
        else if (s >= 1 && s <= 2)
            particle_type = typeof(particles_grav_field_down);
        else if (s >= 0 && s <= 1)
            particle_type = typeof(particles_grav_field_float);
        else if (s <= -1)
            particle_type = typeof(particles_grav_field_up);
        if (particle_type != null)
        {
            // add_shared_particles(particle_type)
            Dm.Deferred("code/datums/proximity_monitor/fields/gravity.dm:95", "Initialize: add_shared_particles(particle_type): the field's particles (particles are not ported)");
            color = Dm.initial(particle_type, (particles p) => p.color);
        }
        RegisterSignal(this, COMSIG_ATOM_SMOOTHED_ICON, nameof(smoothed));
        return dot;
    }

    // tg: code/datums/proximity_monitor/fields/gravity.dm:102
    public override object Destroy(float? force = null)
    {
        // remove_shared_particles(particle_type)
        Dm.Deferred("code/datums/proximity_monitor/fields/gravity.dm:103", "Destroy: remove_shared_particles(particle_type) (particles are not ported)");
        emissive = null;
        return base.Destroy(force);
    }

    // tg: code/datums/proximity_monitor/fields/gravity.dm:107
    public virtual void smoothed(datum source = null)
    {
        // DEVIATION(DV12): cut_overlay(emissive); emissive = emissive_appearance('icons/obj/smooth_structures/grav_field_emissive.dmi',
        // "grav_field_emissive-[splittext(icon_state, "-")[2]]", src); add_overlay(emissive): the glow on the emissive plane is not created
        Dm.Deferred("code/datums/proximity_monitor/fields/gravity.dm:109", "smoothed: the field tile's emissive_appearance (DEVIATION DV12)");
    }
}

// Subtype which adds a subtle overlay to all turfs
// tg: code/datums/proximity_monitor/fields/gravity.dm:116
public partial class datum_proximity_monitor_advanced_gravity_subtle_effect : datum_proximity_monitor_advanced_gravity
{
    // tg: code/datums/proximity_monitor/fields/gravity.dm:118
    public override void setup_field_turf(turf target = null)
    {
        base.setup_field_turf(target);
        if (!isopenturf(target))
            return;
        Dm.@new<obj_effect_gravity_fluff_field>(target, gravity_value);
    }

    // tg: code/datums/proximity_monitor/fields/gravity.dm:123
    public override void cleanup_field_turf(turf target = null)
    {
        base.cleanup_field_turf(target);
        qdel(Dm.locate<obj_effect_gravity_fluff_field>(target));
    }
}
