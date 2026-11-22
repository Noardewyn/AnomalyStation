// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/material_turf_tracking.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7 (D170): the whole file, reached by the delamination's dimensional anomaly turning floors into a contact material's
// (_material.dm:128; plasma's fire stacks on a bare foot, optional.dm:113). The var block is generated (D111) but for the
// statics interaction_blacklist and elevation_interactions, which live in TgState (rules 10.6). Nothing sets TRAIT_ELEVATED_TURF
// or TRAIT_MOB_ELEVATED yet (the elevation element is not translated): those signals never come and an elevated surface never
// triggers.

/// Sends a signal to the owner material whenever something enters its objects' turf and steps onto said object
// tg: code/datums/components/material_turf_tracking.dm:2
public partial class datum_component_material_turf_tracking : datum_component
{
    /// Typecache of things we should ignore
    // tg: code/datums/components/material_turf_tracking.dm:10  var/static/list/interaction_blacklist (a type-level static, rules 10.6)
    private static ref DmList interaction_blacklist => ref TgState.Current.datum_component_material_turf_tracking__interaction_blacklist;

    /// Typecache of objects which we only consider "touched" when they elevate the mob, or the mob is buckled to them
    /// Easy way to keep track of snowflake behavior like flipped tables
    // tg: code/datums/components/material_turf_tracking.dm:37  var/static/list/elevation_interactions (a type-level static, rules 10.6)
    private static ref DmList elevation_interactions => ref TgState.Current.datum_component_material_turf_tracking__elevation_interactions;

    // tg: code/datums/components/material_turf_tracking.dm:47
    [DmParams("owner_material")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        datum_material owner_material = Dm.arg<datum_material>(__extra, 0);
        if (!isopenturf(parent) && !isobj(parent))
            return COMPONENT_INCOMPATIBLE;
        this.owner_material = owner_material;
        if (Dm.truthy(is_type_in_typecache(parent, elevation_interactions)))
            requires_elevation = TRUE;
        return null;
    }

    // tg: code/datums/components/material_turf_tracking.dm:54
    public override object Destroy(float? force = null)
    {
        owner_material = null;
        return base.Destroy(force);
    }

    // tg: code/datums/components/material_turf_tracking.dm:58
    public override async DmTask RegisterWithParent()
    {
        atom target_turf = parent as atom;
        if (ismovable(parent))
        {
            atom_movable as_movable = (atom_movable)parent;
            RegisterSignal(as_movable, COMSIG_ATOM_ENTERING, nameof(on_source_entering));
            RegisterSignal(as_movable, COMSIG_ATOM_EXITING, nameof(on_source_exiting));
            RegisterSignal(as_movable, COMSIG_MOVABLE_TURF_INITIALIZING, nameof(on_turf_initializing));
            target_turf = as_movable.loc;
        }

        register_turf(target_turf);
    }

    /// Start listening to this turf without triggering effects on anything already on it.
    // tg: code/datums/components/material_turf_tracking.dm:70
    public virtual void register_turf(atom location = null)
    {
        if (!isopenturf(location))
            return;

        if (!Dm.truthy(requires_elevation))
        {
            RegisterSignal(location, SIGNAL_ADDTRAIT(TRAIT_ELEVATED_TURF), nameof(on_turf_lost));
            RegisterSignal(location, SIGNAL_REMOVETRAIT(TRAIT_ELEVATED_TURF), nameof(on_turf_gained));
            if (HAS_TRAIT(location, TRAIT_ELEVATED_TURF))
                return;
        }

        // Not tracking initializations or existing objects as this would allow you to TP someone from plating by placing a tile underneath
        RegisterSignal(location, COMSIG_ATOM_ENTERED, nameof(on_entered));
        RegisterSignal(location, COMSIG_TURF_MOVABLE_THROW_LANDED, nameof(on_entered)); // Need this as shoves are 1 tile throws, and COMSIG_ATOM_ENTERED runs before the throw ends
        RegisterSignal(location, COMSIG_ATOM_EXITED, nameof(on_exited));
    }

    // tg: code/datums/components/material_turf_tracking.dm:85
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        await base.UnregisterFromParent(__extra);
        if (isturf(parent))
        {
            on_source_exiting(parent as atom_movable); // tg passes the turf as the source and no exiting turf: a no-op
            return;
        }

        atom_movable as_movable = (atom_movable)parent;
        UnregisterSignal(as_movable, Dm.list(COMSIG_ATOM_ENTERING, COMSIG_ATOM_EXITING, COMSIG_MOVABLE_TURF_INITIALIZING));
        on_source_exiting(as_movable, as_movable.loc);
    }

    // tg: code/datums/components/material_turf_tracking.dm:95
    [SignalHandler]
    public virtual void on_turf_initializing(atom_movable source = null, turf initializing_turf = null)
    {
        UnregisterSignal(initializing_turf, Dm.list(
            SIGNAL_ADDTRAIT(TRAIT_ELEVATED_TURF),
            SIGNAL_REMOVETRAIT(TRAIT_ELEVATED_TURF),
            COMSIG_ATOM_ENTERED,
            COMSIG_TURF_MOVABLE_THROW_LANDED,
            COMSIG_ATOM_EXITED
        ));
        // Things already here might still land or become elevated, so keep listening for that.
        // Only clear those callbacks if the new turf means they can no longer touch the material turf.
        if (!isopenturf(initializing_turf) || (!Dm.truthy(requires_elevation) && HAS_TRAIT(initializing_turf, TRAIT_ELEVATED_TURF)))
            on_turf_lost(initializing_turf);
        register_turf(initializing_turf);
    }

    // tg: code/datums/components/material_turf_tracking.dm:110
    [SignalHandler]
    public virtual void on_source_entering(atom_movable source = null, atom entering = null, atom old_loc = null)
    {
        if (!isopenturf(entering))
            return;

        if (!Dm.truthy(requires_elevation))
        {
            RegisterSignal(entering, SIGNAL_ADDTRAIT(TRAIT_ELEVATED_TURF), nameof(on_turf_lost));
            RegisterSignal(entering, SIGNAL_REMOVETRAIT(TRAIT_ELEVATED_TURF), nameof(on_turf_gained));
            if (HAS_TRAIT(entering, TRAIT_ELEVATED_TURF))
                return;
        }
        on_turf_gained((turf)entering);
    }

    // tg: code/datums/components/material_turf_tracking.dm:123
    [SignalHandler]
    public virtual void on_source_exiting(atom_movable source = null, atom exiting = null)
    {
        if (!isturf(exiting))
            return;

        UnregisterSignal(exiting, Dm.list(SIGNAL_ADDTRAIT(TRAIT_ELEVATED_TURF), SIGNAL_REMOVETRAIT(TRAIT_ELEVATED_TURF)));
        on_turf_lost((turf)exiting);
    }

    // tg: code/datums/components/material_turf_tracking.dm:132
    [SignalHandler]
    public virtual void on_turf_gained(turf source = null)
    {
        RegisterSignal(source, COMSIG_ATOM_ENTERED, nameof(on_entered));
        RegisterSignal(source, COMSIG_TURF_MOVABLE_THROW_LANDED, nameof(on_entered));
        RegisterSignal(source, COMSIG_ATOM_EXITED, nameof(on_exited));
        foreach (atom_movable thing in Dm.each<atom_movable>(source))
            on_entered(source, thing);
    }

    // tg: code/datums/components/material_turf_tracking.dm:141
    [SignalHandler]
    public virtual void on_turf_lost(turf source = null)
    {
        UnregisterSignal(source, Dm.list(COMSIG_ATOM_ENTERED, COMSIG_TURF_MOVABLE_THROW_LANDED, COMSIG_ATOM_EXITED));
        foreach (atom_movable thing in Dm.each<atom_movable>(source))
            UnregisterSignal(thing, Dm.list(SIGNAL_ADDTRAIT(TRAIT_MOB_ELEVATED), COMSIG_MOVETYPE_FLAG_DISABLED));
    }

    // tg: code/datums/components/material_turf_tracking.dm:148
    [SignalHandler]
    public virtual void on_entered(datum source = null, atom_movable arrived = null, atom old_loc = null, DmList old_locs = null)
    {
        if (Dm.truthy(arrived.throwing) || Dm.num(arrived.invisibility) >= INVISIBILITY_ABSTRACT || arrived == parent)
            return;

        if (Dm.truthy(is_type_in_typecache(arrived, interaction_blacklist)))
            return;

        if (!isliving(arrived) && Dm.truthy(requires_elevation))
            return;

        // Its floating but it may touch down
        if (Dm.truthy(Dm.bitand(arrived.movement_type, MOVETYPES_NOT_TOUCHING_GROUND)))
        {
            RegisterSignal(arrived, COMSIG_MOVETYPE_FLAG_DISABLED, nameof(on_move_flag_disabled));
            return;
        }

        if (!isliving(arrived))
        {
            trigger_effect(arrived);
            return;
        }

        if (Dm.truthy(requires_elevation))
        {
            // We want to know when they touch down so we can interact with them
            RegisterSignal(arrived, SIGNAL_ADDTRAIT(TRAIT_MOB_ELEVATED), nameof(on_mob_elevated));
            // The trait is kept even if the mob is buckled which is weird but plays into our hand here
            if (!HAS_TRAIT(arrived, TRAIT_MOB_ELEVATED))
                return;
        }

        trigger_effect(arrived);
    }

    // tg: code/datums/components/material_turf_tracking.dm:178
    [SignalHandler]
    public virtual void on_exited(datum source = null, atom_movable gone = null)
    {
        UnregisterSignal(gone, Dm.list(SIGNAL_ADDTRAIT(TRAIT_MOB_ELEVATED), COMSIG_MOVETYPE_FLAG_DISABLED));
    }

    // tg: code/datums/components/material_turf_tracking.dm:183
    public virtual void trigger_effect(atom_movable arrived = null)
    {
        if (!isliving(arrived))
        {
            if (Dm.truthy(owner_material._listen_lookup?[COMSIG_MATERIAL_EFFECT_STEP]))
                owner_material._SendSignal(COMSIG_MATERIAL_EFFECT_STEP, Dm.list(owner_material, parent, arrived, null, null, FALSE));
            return;
        }

        mob_living victim = (mob_living)arrived;
        float skin_contact = FEET;
        if (victim.body_position == LYING_DOWN)
            skin_contact = Dm.bitor(Dm.bitor(Dm.bitor(Dm.bitor(Dm.bitor(CHEST, GROIN), LEGS), FEET), ARMS), HANDS);

        foreach (obj_item worn_item in Dm.each<obj_item>(victim.get_equipped_items(INCLUDE_ABSTRACT)))
        {
            skin_contact = Dm.bitand(skin_contact, Dm.bitnot(worn_item.body_parts_covered));
            if (!Dm.truthy(skin_contact))
                break;
        }

        if (Dm.truthy(owner_material._listen_lookup?[COMSIG_MATERIAL_EFFECT_STEP]))
            owner_material._SendSignal(COMSIG_MATERIAL_EFFECT_STEP, Dm.list(owner_material, parent, arrived, null, pick(BODY_ZONE_L_LEG, BODY_ZONE_R_LEG), Dm.num(Dm.truthy(skin_contact))));
    }

    // tg: code/datums/components/material_turf_tracking.dm:200
    public virtual void on_mob_elevated(mob_living source = null, object trait = null)
    {
        if (Dm.truthy(source.throwing) || Dm.num(source.invisibility) >= INVISIBILITY_ABSTRACT || Dm.truthy(Dm.bitand(source.movement_type, MOVETYPES_NOT_TOUCHING_GROUND)))
            return;

        if (Dm.truthy(is_type_in_typecache(source, interaction_blacklist)))
            return;

        trigger_effect(source);
    }

    // tg: code/datums/components/material_turf_tracking.dm:209
    public virtual void on_move_flag_disabled(atom_movable source = null, object flag = null, object old_state = null)
    {
        if (Dm.truthy(source.throwing) || Dm.num(source.invisibility) >= INVISIBILITY_ABSTRACT || Dm.truthy(Dm.bitand(source.movement_type, MOVETYPES_NOT_TOUCHING_GROUND)))
            return;

        if (Dm.truthy(is_type_in_typecache(source, interaction_blacklist)))
            return;

        if (Dm.truthy(requires_elevation))
        {
            if (!isliving(source))
                return;
            RegisterSignal(source, SIGNAL_ADDTRAIT(TRAIT_MOB_ELEVATED), nameof(on_mob_elevated));
            if (!HAS_TRAIT(source, TRAIT_MOB_ELEVATED))
                return;
        }

        trigger_effect(source);
    }
}

public sealed partial class TgState
{
    // tg: code/datums/components/material_turf_tracking.dm:10  var/static/list/interaction_blacklist = typecacheof(list(...))
    internal DmList datum_component_material_turf_tracking__interaction_blacklist = typecacheof(Dm.list(
        typeof(obj_docking_port),
        typeof(obj_effect_abstract),
        typeof(obj_effect_atmos_shield),
        typeof(obj_effect_collapse),
        typeof(obj_effect_constructing_effect),
        typeof(obj_effect_dummy_phased_mob),
        typeof(obj_effect_ebeam),
        typeof(obj_effect_fishing_float),
        typeof(obj_effect_hotspot),
        typeof(obj_effect_landmark),
        typeof(obj_effect_light_emitter_tendril),
        typeof(obj_effect_mapping_helpers),
        typeof(obj_effect_particle_effect_ion_trails),
        typeof(obj_effect_particle_effect_sparks),
        typeof(obj_effect_portal),
        typeof(obj_effect_projectile),
        typeof(obj_effect_spectre_of_resurrection),
        typeof(obj_effect_temp_visual),
        typeof(obj_effect_wisp),
        typeof(obj_energy_ball),
        typeof(obj_narsie),
        typeof(obj_singularity)
    )) as DmList;

    // tg: code/datums/components/material_turf_tracking.dm:37  var/static/list/elevation_interactions = typecacheof(list(...))
    internal DmList datum_component_material_turf_tracking__elevation_interactions = typecacheof(Dm.list(
        typeof(obj_structure_platform),
        typeof(obj_structure_table),
        typeof(obj_structure_rack),
        typeof(obj_structure_bed),
        typeof(obj_structure_closet_crate),
        typeof(obj_structure_reagent_dispensers),
        typeof(obj_structure_altar)
    )) as DmList;
}
