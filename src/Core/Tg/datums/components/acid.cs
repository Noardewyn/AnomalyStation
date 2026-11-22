// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/acid.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (silicons SI1c, D326: the acid the robot remains need). The acid's particles are deferred leaves, as the burning
// component's (particles are a client visual not drawn yet); the overlay shows the acid. The var block is generated (D111).

public partial class datum_controller_global_vars
{
    // tg: code/datums/components/acid.dm:1  GLOBAL_DATUM_INIT(acid_overlay, /mutable_appearance, mutable_appearance('icons/effects/acid.dmi', "default", appearance_flags = RESET_COLOR|KEEP_APART))
    public object acid_overlay;

    // tg: code/datums/components/acid.dm:1  (GLOBAL_MANAGED(acid_overlay, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalacid_overlay()
    {
        acid_overlay = mutable_appearance(Dm.resource("icons/effects/acid.dmi"), "default", appearance_flags: Dm.bitor(RESET_COLOR, KEEP_APART));
        Dm.append(ref gvars_datum_init_order, "acid_overlay");
    }
}

/**
 * Component representing acid applied to an object.
 * Must be attached to an atom.
 * Processes, repeatedly damaging whatever it is attached to.
 * If the parent atom is a turf it applies acid to the contents of the turf.
 * If not being applied to a mob or turf, the atom must use the integrity system.
 */
// tg: code/datums/components/acid.dm:10
public partial class datum_component_acid : datum_component
{
    // tg: code/datums/components/acid.dm:35
    [DmParams("acid_power", "acid_volume", "acid_overlay", "acid_particles", "turf_acid_ignores_mobs")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object acid_power = Dm.arg(__extra, 0) ?? ACID_POWER_MELT_TURF;
        object acid_volume = Dm.arg(__extra, 1) ?? 50;
        object acid_overlay = Dm.arg(__extra, 2) ?? GLOB.acid_overlay;
        object acid_particles = Dm.arg(__extra, 3) ?? typeof(particles_acid);
        object turf_acid_ignores_mobs = Dm.arg(__extra, 4) ?? FALSE;
        if (!isatom(parent))
            return COMPONENT_INCOMPATIBLE;

        // The parent object cannot have acid. Not incompatible, but should not really happen.
        atom atom_parent = parent as atom;
        if (Dm.truthy(Dm.bitand(atom_parent.resistance_flags, UNACIDABLE)))
        {
            qdel(this);
            return null;
        }

        //Not incompatible, but pointless
        if (Dm.le(acid_power, 0) || Dm.le(acid_volume, 0))
        {
            qdel(this);
            stack_trace(Dm.text("Tried to add /datum/component/acid to an atom ([]) with insufficient acid power ([]) or acid volume ([]).", atom_parent.type, acid_power, acid_volume), "code/datums/components/acid.dm", 48);
            return null;
        }

        if (isliving(parent))
        {
            this.max_volume = MOB_ACID_VOLUME_MAX;
            this.process_effect = Dm.@new<datum_callback>(this, nameof(process_mob), parent);
        }
        else if (isturf(parent))
        {
            this.turf_acid_ignores_mobs = Dm.num(turf_acid_ignores_mobs);
            this.max_volume = TURF_ACID_VOLUME_MAX;
            this.process_effect = Dm.@new<datum_callback>(this, nameof(process_turf), parent);
        }
        //if we failed all other checks, we must be an /atom/movable that uses integrity
        else if (Dm.truthy(atom_parent.uses_integrity))
        {
            this.max_volume = MOVABLE_ACID_VOLUME_MAX;
            this.process_effect = Dm.@new<datum_callback>(this, nameof(process_movable), parent);
        }
        //or not...
        else
        {
            stack_trace(Dm.text("Tried to add /datum/component/acid to an atom ([]) which does not use atom_integrity!", atom_parent.type), "code/datums/components/acid.dm", 65);
            return COMPONENT_INCOMPATIBLE;
        }

        this.acid_power = acid_power;
        set_volume(acid_volume);
        this.acid_overlay = acid_overlay;

        sizzle = Dm.@new<datum_looping_sound_acid>(atom_parent, TRUE);
        if (Dm.truthy(acid_particles))
        {
            if (ismovable(parent))
            {
                // movable_parent.add_shared_particles(acid_particles, "[acid_particles]_[isitem(parent)]", isitem(parent) ? NONE :
                // PARTICLE_ATTACH_MOB): particles (a client visual) are not drawn yet; the overlay shows the acid.
                Dm.Deferred("code/datums/components/acid.dm:77", "Initialize: the acid particles (add_shared_particles, a client visual) (M10+)");
                particle_type = acid_particles as Type;
            }
            else
            {
                // acid particles look pretty bad when they stack on mobs, so that behavior is not wanted for items
                // particle_effect = new(atom_parent, acid_particles, ...): the particle holder (game/objects/effects/particle_holder.dm)
                // is a skeleton (particles, a client visual).
                Dm.Deferred("code/datums/components/acid.dm:82", "Initialize: the acid particle holder of a turf (particles, a client visual) (M10+)");
            }
        }
        START_PROCESSING(SSacid, this);
        return null;
    }

    // tg: code/datums/components/acid.dm:85
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSacid, this);
        if (sizzle != null)
            QDEL_NULL(ref sizzle);
        if (particle_effect != null)
            QDEL_NULL(ref particle_effect);
        if (ismovable(parent) && particle_type != null)
        {
            // movable_parent.remove_shared_particles("[particle_type]_[isitem(parent)]"): see Initialize's deferred leaf.
        }
        process_effect = null;
        return base.Destroy(force);
    }

    // tg: code/datums/components/acid.dm:97
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_ATOM_ATTACK_HAND, nameof(on_attack_hand));
        RegisterSignal(parent, COMSIG_ATOM_EXAMINE, nameof(on_examine));
        RegisterSignal(parent, COMSIG_ATOM_EXPOSE_REAGENT, nameof(on_expose_reagent));
        RegisterSignal(parent, COMSIG_ATOM_UPDATE_OVERLAYS, nameof(on_update_overlays));
        RegisterSignal(parent, COMSIG_COMPONENT_CLEAN_ACT, nameof(on_clean));
        if (isturf(parent))
            RegisterSignal(parent, COMSIG_ATOM_ENTERED, nameof(on_entered));
        atom atom_parent = parent as atom;
        atom_parent.update_appearance();
    }

    // tg: code/datums/components/acid.dm:108
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, Dm.list(
            COMSIG_ATOM_ATTACK_HAND,
            COMSIG_ATOM_EXAMINE,
            COMSIG_ATOM_EXPOSE_REAGENT,
            COMSIG_ATOM_UPDATE_OVERLAYS,
            COMSIG_COMPONENT_CLEAN_ACT
        ));
        if (isturf(parent))
            UnregisterSignal(parent, COMSIG_ATOM_ENTERED);
        atom atom_parent = parent as atom;
        if (!QDELETED(atom_parent))
            atom_parent.update_appearance();
    }

    /// Averages corrosive power and sums volume.
    // tg: code/datums/components/acid.dm:123
    [DmParams("new_comp", "i_am_original", "acid_power", "acid_volume")]
    public override async DmTask InheritComponent(datum_component new_comp = null, object i_am_original = null, params object[] __extra)
    {
        object acid_power = Dm.arg(__extra, 0);
        object acid_volume = Dm.arg(__extra, 1);
        if (!Dm.truthy(i_am_original))
            return;
        this.acid_power = Dm.div(Dm.add(Dm.mul(this.acid_power, this.acid_volume), Dm.mul(acid_power, acid_volume)), Dm.add(this.acid_volume, acid_volume));
        set_volume(Dm.add(this.acid_volume, acid_volume));
    }

    /// Sets the acid volume to a new value. Limits the acid volume by the amount allowed to exist on the parent atom.
    // tg: code/datums/components/acid.dm:130
    public virtual void set_volume(object new_volume = null)
    {
        acid_volume = clamp(new_volume, 0, max_volume);
        if (!Dm.truthy(acid_volume))
            qdel(this);
    }

    /// Handles the slow corrosion of the parent [/atom].
    // tg: code/datums/components/acid.dm:136
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        return Waiting.WaitforFalse(process__body(seconds_per_tick), null); // the slot's set waitfor = FALSE (processing.dm:48)
    }

    private async DmTask<object> process__body(object seconds_per_tick)
    {
        // If we somehow got unacidable, we need to bail out
        atom parent_atom = parent as atom;
        if (Dm.truthy(Dm.bitand(parent_atom.resistance_flags, UNACIDABLE)))
        {
            qdel(this);
            return null;
        }
        process_effect?.InvokeAsync(seconds_per_tick);
        if (QDELING(this)) //The process effect deals damage, and on turfs diminishes the acid volume, potentially destroying the component. Let's not destroy it twice.
            return null;
        set_volume(Dm.num(acid_volume) - (ACID_DECAY_BASE + (ACID_DECAY_SCALING * round(sqrt(Dm.num(acid_volume))))) * Dm.num(seconds_per_tick));
        return null;
    }

    /// Handles processing on an [/atom/movable] (that uses atom_integrity).
    // tg: code/datums/components/acid.dm:147
    public virtual async DmTask process_movable(atom_movable target = null, object seconds_per_tick = null)
    {
        if (Dm.truthy(Dm.bitand(target.resistance_flags, ACID_PROOF)))
            return;
        await target.take_damage(min(1 + round(sqrt(Dm.num(acid_power) * Dm.num(acid_volume)) * 0.3f), MOVABLE_ACID_DAMAGE_MAX) * Dm.num(seconds_per_tick), BURN, ACID, 0);
    }

    /// Handles processing on a [/mob/living].
    // tg: code/datums/components/acid.dm:153
    public virtual async DmTask process_mob(mob_living target = null, object seconds_per_tick = null)
    {
        if (Dm.truthy(Dm.bitand(target.resistance_flags, ACID_PROOF)))
            return;
        await target.acid_act(acid_power, Dm.mul(acid_volume, seconds_per_tick));
    }

    /// Handles processing on a [/turf].
    // tg: code/datums/components/acid.dm:159
    public virtual async DmTask process_turf(turf target_turf = null, object seconds_per_tick = null)
    {
        float acid_used = min(Dm.num(acid_volume) * 0.05f, 20) * Dm.num(seconds_per_tick);
        float applied_targets = 0;
        foreach (atom_movable target_movable in Dm.each(target_turf))
        {
            // Don't apply acid to things under the turf
            if (HAS_TRAIT(target_movable, TRAIT_UNDERFLOOR))
                continue;
            // Ignore mobs if turf_acid_ignores_mobs is TRUE
            if (Dm.truthy(turf_acid_ignores_mobs) && ismob(target_movable))
                continue;
            // Apply the acid
            if (Dm.truthy(await target_movable.acid_act(acid_power, acid_used)))
                applied_targets++;
        }

        if (Dm.truthy(applied_targets))
            set_volume(Dm.num(acid_volume) - (acid_used * applied_targets));

        if (Dm.truthy(Dm.bitand(target_turf.resistance_flags, ACID_PROOF)))
            return;

        // Snowflake code for handling acid melting walls.
        // We really should consider making turfs use atom_integrity, but for now this is just for acids.

        //Strong walls will never get melted
        if (Dm.ge(target_turf.get_explosive_block(), 2))
            return;
        //Reinforced floors never get melted
        if (target_turf is turf_open_floor_engine)
            return;
        if (Dm.lt(acid_power, ACID_POWER_MELT_TURF))
            return;

        parent_integrity -= Dm.num(seconds_per_tick);
        if (parent_integrity <= 0)
        {
            target_turf.visible_message(span_warning(I18n.T("[] collapses under its own weight into a puddle of goop and undigested debris!", target_turf)));
            await target_turf.acid_melt();
        }
        else if (parent_integrity <= 4 && stage <= 3)
        {
            target_turf.visible_message(span_warning(I18n.T("[] begins to crumble under the acid!", target_turf)));
            stage = 4;
        }
        else if (parent_integrity <= 8 && stage <= 2)
        {
            target_turf.visible_message(span_warning(I18n.T("[] is struggling to withstand the acid!", target_turf)));
            stage = 3;
        }
        else if (parent_integrity <= 16 && stage <= 1)
        {
            target_turf.visible_message(span_warning(I18n.T("[] is being melted by the acid!", target_turf)));
            stage = 2;
        }
        else if (parent_integrity <= 24 && stage == 0)
        {
            target_turf.visible_message(span_warning(I18n.T("[] is holding up against the acid!", target_turf)));
            stage = 1;
        }
    }

    /// Used to maintain the acid overlay on the parent [/atom].
    // tg: code/datums/components/acid.dm:213
    public virtual void on_update_overlays(atom parent_atom = null, DmList overlays = null)
    {
        if (Dm.truthy(acid_overlay))
            Dm.append(overlays, acid_overlay);
    }

    /// Alerts any examiners to the acid on the parent atom.
    // tg: code/datums/components/acid.dm:220
    public virtual void on_examine(atom source = null, mob user = null, DmList examine_list = null)
    {
        Dm.append(examine_list, span_danger(I18n.T("[] covered in a corrosive liquid!", source.p_Theyre())));
    }

    /// Makes it possible to clean acid off of objects.
    // tg: code/datums/components/acid.dm:226
    public virtual object on_clean(atom source = null, object clean_types = null)
    {
        if (!Dm.truthy(Dm.bitand(clean_types, CLEAN_TYPE_ACID)))
            return NONE;
        qdel(this);
        return Dm.bitor(COMPONENT_CLEANED, COMPONENT_CLEANED_GAIN_XP);
    }

    /// Handles water diluting the acid on the object.
    // tg: code/datums/components/acid.dm:235
    public virtual object on_expose_reagent(atom parent_atom = null, datum_reagent exposing_reagent = null, object reac_volume = null, object methods = null)
    {
        if (!(exposing_reagent is datum_reagent_water))
            return NONE;

        acid_power = Dm.div(acid_power, Dm.div(acid_volume, Dm.add(acid_volume, reac_volume)));
        set_volume(Dm.add(acid_volume, reac_volume));
        return NONE;
    }

    /// Handles searing the hand of anyone who tries to touch parent without protection.
    // tg: code/datums/components/acid.dm:246
    public virtual object on_attack_hand(atom source = null, mob_living_carbon user = null)
    {
        if (!iscarbon(user) || Dm.truthy(user.can_touch_acid(source, acid_power, acid_volume)))
            return NONE;

        Waiting.NoSleep(user.apply_damage(5, BURN, user.get_active_hand()));
        to_chat(user, span_userdanger(I18n.T("The acid on \\the [] burns your hand!", source)));
        Waiting.Detach(user.emote("scream"));
        playsound(source, SFX_SEAR, 50, TRUE);
        return COMPONENT_CANCEL_ATTACK_CHAIN;
    }

    /// Handles searing the feet of whoever walks over this without protection. Only active if the parent is a turf.
    // tg: code/datums/components/acid.dm:259
    public virtual async DmTask on_entered(datum source = null, atom_movable arrived = null, atom old_loc = null, DmList old_locs = null)
    {
        if (Dm.truthy(turf_acid_ignores_mobs))
            return;
        if (!isliving(arrived))
            return;
        mob_living crosser = arrived as mob_living;
        if (Dm.truthy(Dm.bitand(crosser.movement_type, MOVETYPES_NOT_TOUCHING_GROUND)))
            return;
        if (crosser.move_intent == MOVE_INTENT_WALK)
            return;
        if (prob(60))
            return;

        float acid_used = min(Dm.num(acid_volume) * 0.05f, 20);
        if (!Dm.truthy(await crosser.acid_act(acid_power, acid_used, FEET)))
            return;
        playsound(crosser, SFX_SEAR, 50, TRUE);
        to_chat(crosser, span_userdanger(I18n.T("The acid on \\the [] burns you!", parent)));
        set_volume(max(Dm.num(acid_volume) - acid_used, 10));
    }
}
