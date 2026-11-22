// Translated from tgstation (snapshot 1d99bd7f): code/game/atom/atom_act.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only on_log (M1 P1: mob/Login and mob/Logout call it on the mob's location, login.dm:87,
// logout.dm:10), hitby, hitby_react (M4a P2: throwing) and fire_act, extinguish, handle_slip (M4b slice 3: fire, wet
// floors), handle_fall (D296: the body's fall), container_resist_act (M4c slice 4: the root of the suit storage unit's), ex_act, contents_explosion,
// singularity_act and singularity_pull (M5x slice 1: explosions; the singularity's roots, which its overrides need), emag_act (G1a, D316: the emag's root), emp_act
// (M7 EMP), zap_act (M7 zaps: the tesla zap's root), projectile_hit and bullet_act (the M7 projectiles slice), rcd_act and rcd_vals (E13, the RCD's roots), rust_heretic_act and attack_hulk (silicons SI1, D326: the roots of the
// silicons' overrides) and acid_act (silicons SI1c: the acid) translated; the other acts (do_rust_heretic_act, ...) come with their mechanics.

// The [DmPath] of /atom is on its engine part Builtin/atom.builtin.cs (rules 2.3).
public partial class atom : datum
{
    /**
     * Respond to fire being used on our atom
     *
     * Default behaviour is to send [COMSIG_ATOM_FIRE_ACT] and return
     */
    /**
     * React to being hit by an explosion
     *
     * Should be called through the [EX_ACT] wrapper macro.
     * The wrapper takes care of the [COMSIG_ATOM_EX_ACT] signal.
     * as well as calling [/atom/proc/contents_explosion].
     *
     * Returns TRUE by default, and behavior should be implemented on children procs on a per-atom basis. Should only return FALSE if we resist the explosion for any reason.
     * We assume that the default is TRUE because all atoms should be considered destructible in some manner unless they explicitly opt out (in our current framework).
     * However, the return value itself doesn't have any external consumers, it's only so children procs can listen to the value from their parent procs (due to the nature of the [EX_ACT] macro).
     * Thus, the return value only matters on overrides of this proc, and the only thing that truly matters is the code that is executed (applying damage, calling damage procs, etc.)
     *
     */
    // tg: code/game/atom/atom_act.dm:40  (set waitfor = FALSE: this definition cannot sleep, a plain method, waiting-model.md 5.4)
    public virtual object ex_act(object severity = null, object target = null, params object[] __extra)
    {
        return TRUE;
    }

    /// Handle what happens when your contents are exploded by a bomb
    // tg: code/game/atom/atom_act.dm:45
    public virtual async DmTask contents_explosion(object severity = null, object target = null)
    {
        return; //For handling the effects of explosions on contents that would not normally be effected
    }

    // tg: code/game/atom/atom_act.dm:53
    public virtual async DmTask<object> blob_act(obj_structure_blob attacking_blob = null)
    {
        object blob_act_result = !Dm.truthy(_listen_lookup?[COMSIG_ATOM_BLOB_ACT]) ? NONE : _SendSignal(COMSIG_ATOM_BLOB_ACT, Dm.list(this, attacking_blob));
        if (Dm.truthy(Dm.bitand(blob_act_result, COMPONENT_CANCEL_BLOB_ACT)))
            return FALSE;
        return TRUE;
    }

    /**
     * Respond to an emag being used on our atom
     *
     * Args:
     * * mob/user: The mob that used the emag. Nullable.
     * * obj/item/card/emag/emag_card: The emag that was used. Nullable.
     *
     * Returns:
     * TRUE if the emag had any effect, falsey otherwise.
     */
    // tg: code/game/atom/atom_act.dm:200
    public virtual async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        return !Dm.truthy(_listen_lookup?[COMSIG_ATOM_EMAG_ACT]) ? NONE : _SendSignal(COMSIG_ATOM_EMAG_ACT, Dm.list(this, user, emag_card));
    }

    // tg: code/game/atom/atom_act.dm:208
    public virtual async DmTask<object> narsie_act(params object[] __extra)
    {
        if (Dm.truthy(_listen_lookup?[COMSIG_ATOM_NARSIE_ACT]))
            _SendSignal(COMSIG_ATOM_NARSIE_ACT, Dm.list(this));
        return null;
    }

    ///Respond to the singularity eating this atom
    // tg: code/game/atom/atom_act.dm:170
    public virtual async DmTask<object> singularity_act()
    {
        return null;
    }

    /**
     * Respond to the singularity pulling on us
     *
     * Default behaviour is to send [COMSIG_ATOM_SING_PULL] and return
     */
    // tg: code/game/atom/atom_act.dm:178
    public virtual async DmTask<object> singularity_pull(atom singularity = null, object current_size = null)
    {
        if (Dm.truthy(_listen_lookup?[COMSIG_ATOM_SING_PULL]))
            _SendSignal(COMSIG_ATOM_SING_PULL, Dm.list(this, singularity, current_size));
        return null;
    }

    // tg: code/game/atom/atom_act.dm:14
    public virtual async DmTask<object> fire_act(object exposed_temperature = null, object exposed_volume = null)
    {
        if (Dm.truthy(_listen_lookup?[COMSIG_ATOM_FIRE_ACT]))
            _SendSignal(COMSIG_ATOM_FIRE_ACT, Dm.list(this, exposed_temperature, exposed_volume));
        return FALSE;
    }

    /**
     * Respond to an electromagnetic pulse
     *
     * Default behaviour is to send [COMSIG_ATOM_PRE_EMP_ACT], which an EMP-protected atom answers with its protection flags
     * (the empprotection element), to pulse the atom's wires unless it protects them, and to send [COMSIG_ATOM_EMP_ACT].
     * We then return the protection value
     */
    // tg: code/game/atom/atom_act.dm:69
    public virtual async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        // SHOULD_CALL_PARENT(TRUE)
        object protection = !Dm.truthy(_listen_lookup?[COMSIG_ATOM_PRE_EMP_ACT]) ? (object)NONE : _SendSignal(COMSIG_ATOM_PRE_EMP_ACT, Dm.list(this, severity));
        if (!Dm.truthy(Dm.bitand(protection, EMP_PROTECT_WIRES)) && wires is datum_wires)
            await wires.emp_pulse();

        if (Dm.truthy(Dm.bitand(flags_1, HOLOGRAM_1))) // holograms should ignore EMP_PROTECT flags (since the protection itself is also a hologram)
        {
            if (prob(75 / Dm.num(severity)))
            {
                // avoid spamming messages for anything inside the holodeck directly
                if (!istype(get_area(this), typeof(area_station_holodeck)))
                    visible_message(span_warning(I18n.T("[] fades away!", this)));
                // animate(alpha = 0, time = 1 SECONDS): the Object argument is left out, which animates src
                animate(this, Dm.named("alpha", 0), Dm.named("time", 1 * SECONDS));
                _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), 1 * SECONDS > GC_FILTER_QUEUE ? (object)WEAKREF(this) : this), 1 * SECONDS, file: "code/game/atom/atom_act.dm", line: 81); // QDEL_IN(src, 1 SECONDS), inline (rules 11.1 (f))
            }
        }

        if (Dm.truthy(_listen_lookup?[COMSIG_ATOM_EMP_ACT]))
            _SendSignal(COMSIG_ATOM_EMP_ACT, Dm.list(this, severity, protection));
        return protection; // Pass the protection value collected here upwards
    }

    /**
     * Sends [COMSIG_ATOM_EXTINGUISH] signal, which properly removes burning component if it is present.
     *
     * Default behaviour is to send [COMSIG_ATOM_ACID_ACT] and return
     */
    // tg: code/game/atom/atom_act.dm:23
    public virtual async DmTask<object> extinguish()
    {
        // SHOULD_CALL_PARENT(TRUE)
        return !Dm.truthy(this._listen_lookup?[COMSIG_ATOM_EXTINGUISH]) ? (object)NONE : this._SendSignal(COMSIG_ATOM_EXTINGUISH, Dm.list(this));
    }

    ///Handle the atom being slipped over
    // tg: code/game/atom/atom_act.dm:162  (rules 3.2, typed vars that hold wider values: obj/slipping_object is atom, the slippery
    // component of a wet floor passes the turf; gaps package G4, the slip memory's "wet floor")
    public virtual async DmTask<object> handle_slip(mob_living_carbon slipped_carbon = null, object knockdown_amount = null, atom slipping_object = null, object lube = null, object paralyze = null, object daze = null, object force_drop = null)
    {
        return FALSE;
    }

    ///Used for making a sound when a mob involuntarily falls into the ground.
    // tg: code/game/atom/atom_act.dm:166
    public virtual async DmTask handle_fall(mob faller = null)
    {
    }

    /**
     * Called when the atom log's in or out
     *
     * Default behaviour is to call on_log on the location this atom is in
     */
    // tg: code/game/atom/atom_act.dm:224
    public virtual void on_log(object login = null)
    {
        if (Dm.truthy(loc))
            loc.on_log(login);
    }

    /**
     * Respond to an electric bolt action on our item
     *
     * Default behaviour is to return, we define here to allow for cleaner code later on
     */
    // tg: code/game/atom/atom_act.dm:216  (M7 zaps)
    public virtual async DmTask<object> zap_act(object power = null, object zap_flags = null)
    {
        return null;
    }

    /**
     * Wrapper for bullet_act used for atom-specific calculations, i.e. armor
     *
     * @params
     * * hitting_projectile - projectile
     * * def_zone - zone hit
     * * piercing_hit - is this hit piercing or normal?
     */
    // tg: code/game/atom/atom_act.dm:95  (the M7 projectiles slice)
    public virtual async DmTask<object> projectile_hit(obj_projectile hitting_projectile = null, object def_zone = null, float? piercing_hit = null, object blocked = null)
    {
        piercing_hit ??= FALSE;
        // DEVIATION(DV27): the interaction guard (D132) on an atom whose own tg projectile_hit or bullet_act is not translated (a
        // mirror, a reflector, a target, a mech): nothing of the atom's happens and the projectile stops there, as on a block
        if (InertSeam.MissingOverride(this, nameof(projectile_hit)) || InertSeam.MissingOverride(this, nameof(bullet_act)))
            return BULLET_ACT_BLOCK;
        object sigreturn = (!Dm.truthy(_listen_lookup?[COMSIG_ATOM_PRE_BULLET_ACT]) ? NONE : _SendSignal(COMSIG_ATOM_PRE_BULLET_ACT, Dm.list(this, hitting_projectile, def_zone, piercing_hit)));

        if (Dm.truthy(Dm.bitand(sigreturn, COMPONENT_BULLET_PIERCED)))
            return BULLET_ACT_FORCE_PIERCE;
        if (Dm.truthy(Dm.bitand(sigreturn, COMPONENT_BULLET_BLOCKED)))
            return BULLET_ACT_BLOCK;
        if (Dm.truthy(Dm.bitand(sigreturn, COMPONENT_BULLET_ACTED)))
            return BULLET_ACT_HIT;

        if (blocked == null)
            blocked = check_projectile_armor(def_zone, hitting_projectile);

        return await bullet_act(hitting_projectile, def_zone, piercing_hit, Dm.num_or_null(blocked));
    }

    /**
     * React to a hit by a projectile object
     *
     * @params
     * * hitting_projectile - projectile
     * * def_zone - zone hit
     * * piercing_hit - is this hit piercing or normal?
     * * blocked - total armor value to apply to this hit
     */
    // tg: code/game/atom/atom_act.dm:119  (the M7 projectiles slice)
    [ShouldCallParent]
    public virtual async DmTask<object> bullet_act(obj_projectile hitting_projectile = null, object def_zone = null, float? piercing_hit = null, float? blocked = null)
    {
        piercing_hit ??= FALSE;
        blocked ??= 0;
        if (Dm.truthy(_listen_lookup?[COMSIG_ATOM_BULLET_ACT]))
            _SendSignal(COMSIG_ATOM_BULLET_ACT, Dm.list(this, hitting_projectile, def_zone, piercing_hit, blocked));
        if (QDELETED(hitting_projectile)) // Signal deleted it?
            return BULLET_ACT_BLOCK;

        return await hitting_projectile.on_hit(
            target: this,
            // This armor check only matters for the visuals and messages in on_hit(), it's not actually used to reduce damage since
            // only living mobs use armor to reduce damage, but on_hit() is going to need the value no matter what is shot.
            blocked: blocked,
            pierce_hit: piercing_hit
        );
    }

    /**
     * Causes effects when the atom gets hit by a rust effect from heretics
     *
     * Override this if you want custom behaviour in whatever gets hit by the rust
     * /turf/rust_turf should be used instead for overriding rust on turfs
     * rust_strength (optional) - if you want to vary the effect based on the users' strength
     */
    /**
     * Respond to acid being used on our atom
     *
     * Default behaviour is to send [COMSIG_ATOM_ACID_ACT] and return
     */
    // tg: code/game/atom/atom_act.dm:186
    public virtual async DmTask<object> acid_act(object acidpwr = null, object acid_volume = null, params object[] __extra)
    {
        if (Dm.truthy(_listen_lookup?[COMSIG_ATOM_ACID_ACT]))
            _SendSignal(COMSIG_ATOM_ACID_ACT, Dm.list(this, acidpwr, acid_volume));
        return FALSE;
    }

    // tg: code/game/atom/atom_act.dm:235
    public virtual async DmTask<object> rust_heretic_act(object rust_strength = null)
    {
        return FALSE;
    }

    ///This atom has been hit by a hulkified mob in hulk mode (user)
    // tg: code/game/atom/atom_act.dm:262
    public virtual async DmTask<object> attack_hulk(mob_living_carbon_human user = null, params object[] __extra)
    {
        if (Dm.truthy(_listen_lookup?[COMSIG_ATOM_HULK_ATTACK]))
            _SendSignal(COMSIG_ATOM_HULK_ATTACK, Dm.list(this, user));
        return null;
    }

    ///Called when something resists while this atom is its loc
    // tg: code/game/atom/atom_act.dm:245  (M4c slice 4: the root of the suit storage unit's override)
    public virtual async DmTask<object> container_resist_act(mob_living user = null, params object[] __extra)
    {
        return null;
    }

    /**
     * Respond to an RCD acting on our item
     *
     * Default behaviour is to send [COMSIG_ATOM_RCD_ACT] and return FALSE
     */
    // tg: code/game/atom/atom_act.dm:253  (E13: the RCD's root)
    public virtual async DmTask<object> rcd_act(mob user = null, obj_item_construction_rcd the_rcd = null, DmList rcd_data = null, params object[] __extra)
    {
        if (Dm.truthy(this._listen_lookup?[COMSIG_ATOM_RCD_ACT]))
            this._SendSignal(COMSIG_ATOM_RCD_ACT, Dm.list(this, user, the_rcd, rcd_data[RCD_DESIGN_MODE]));
        return FALSE;
    }

    ///Return the values you get when an RCD eats you?
    // tg: code/game/atom/atom_act.dm:258  (E13)
    public virtual object rcd_vals(mob user = null, obj_item_construction_rcd the_rcd = null, params object[] __extra)
    {
        return FALSE;
    }
}

// M4a contracts: the roots of this file's procs that no part declared yet, with the signatures of
// docs/ai/specs/generated/slots.tsv (DmTask exactly where suspends = yes), Dm.NotTranslated until M4a P2 translates
// them or leaves them for their milestone (docs/ai/specs/m4a-ownership.md). Overrides are not declared, so the inherited
// code keeps running until then.

// tg: code/game/atom/atom_act.dm:146
public partial class atom : datum
{
    /**
     * React to being hit by a thrown object
     *
     * Default behaviour is to call [hitby_react][/atom/proc/hitby_react] on ourselves after 2 seconds if we are dense
     * and under normal gravity.
     *
     * Im not sure why this the case, maybe to prevent lots of hitby's if the thrown object is
     * deleted shortly after hitting something (during explosions or other massive events that
     * throw lots of items around - singularity being a notable example)
     *
     * Worth of note: If hitby returns TRUE, it means the object has been blocked or catched by src.
     * So far, this is only possible for living mobs and carbons, who can hold shields and catch thrown items.
     */
    // tg: code/game/atom/atom_act.dm:146
    public virtual async DmTask<object> hitby(atom_movable hitting_atom = null, object skipcatch = null, object hitpush = null, object blocked = null, datum_thrownthing throwingdatum = null)
    {
        // DEVIATION(DV27): the interaction guard (D132, interaction.md 7, P2's site) on the hit atom's own hitby: nothing of the
        // hit atom's happens (FALSE: not blocked or caught).
        if (InertSeam.MissingOverride(this, "hitby", throwingdatum?.get_thrower() as mob))
            return FALSE;
        if (Dm.truthy(this._listen_lookup?[COMSIG_ATOM_HITBY]))
            this._SendSignal(COMSIG_ATOM_HITBY, Dm.list(this, hitting_atom, skipcatch, hitpush, blocked, throwingdatum));
        if (Dm.truthy(density) && !Dm.truthy(has_gravity((object)hitting_atom as turf))) //thrown stuff bounces off dense stuff in no grav, (has_gravity reads a non-turf argument as src's turf, _atom.dm:923) unless the thrown stuff ends up inside what it hit(embedding, bola, etc...).
            _addtimer(Dm.@new<datum_callback>(this, nameof(hitby_react), hitting_atom), 0.2f * SECONDS, file: "code/game/atom/atom_act.dm", line: 149);
        return FALSE;
    }

    /**
     * We have have actually hit the passed in atom
     *
     * Default behaviour is to move back from the item that hit us
     */
    // tg: code/game/atom/atom_act.dm:157
    public virtual async DmTask hitby_react(atom_movable harmed_atom = null)
    {
        if (Dm.truthy(harmed_atom) && isturf(harmed_atom.loc))
            await step(harmed_atom, REVERSE_DIR(harmed_atom.dir));
    }
}
