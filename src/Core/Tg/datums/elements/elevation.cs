// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/elevation.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8y slice B (D217): the whole file, for the stasis bed (pixel_shift 6). Tables, racks and the safe still skip their AddElement
// (their own presentation rows, M4a); the mob's lifted position is the offsets of living_update_icons.cs (add_offsets, z), which
// the client draws as it draws a lying body's. Buckling (slice A) sends COMSIG_LIVING_SET_BUCKLED. The var blocks are generated.

/**
 * Manages the elevation of the turf the source is on
 * The atom with the highest pixel_shift gets to set the elevation of the turf to that value.
 */
// tg: code/datums/elements/elevation.dm:5
public partial class datum_element_elevation : datum_element
{
    // tg: code/datums/elements/elevation.dm:11
    [DmParams("target", "pixel_shift")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object pixel_shift = Dm.arg(__extra, 0);
        object dot = await base.Attach(target, Dm.forward(__extra, pixel_shift));
        if (!ismovable(target))
            return ELEMENT_INCOMPATIBLE;

        ADD_TRAIT(target, TRAIT_ELEVATING_OBJECT, REF(this));

        this.pixel_shift = pixel_shift;

        RegisterSignals(target, Dm.list(COMSIG_ATOM_ENTERING, COMSIG_MOVABLE_TURF_INITIALIZING), nameof(on_source_entering));
        RegisterSignal(target, COMSIG_ATOM_EXITING, nameof(on_source_exiting));

        atom atom_target = (atom)target;
        await register_turf(atom_target as atom_movable, atom_target.loc);
        return dot;
    }

    // tg: code/datums/elements/elevation.dm:26
    [DmParams("source")]
    public override object Detach(datum source = null, params object[] __extra)
    {
        atom_movable movable = (atom_movable)source;
        UnregisterSignal(movable, Dm.list(COMSIG_ATOM_ENTERING, COMSIG_MOVABLE_TURF_INITIALIZING, COMSIG_ATOM_EXITING));
        Waiting.NoSleep(unregister_turf(movable, movable.loc));
        REMOVE_TRAIT(movable, TRAIT_ELEVATING_OBJECT, REF(this));
        return base.Detach(source, __extra);
    }

    // tg: code/datums/elements/elevation.dm:32
    public virtual async DmTask reset_elevation(turf target = null)
    {
        DmList current_values = Dm.list(null, null); // list/current_values[2]
        if (Dm.truthy(target._listen_lookup?[COMSIG_TURF_RESET_ELEVATION]))
            target._SendSignal(COMSIG_TURF_RESET_ELEVATION, Dm.list(target, current_values));
        object current_pixel_shift = current_values[ELEVATION_CURRENT_PIXEL_SHIFT];
        object new_pixel_shift = current_values[ELEVATION_MAX_PIXEL_SHIFT];
        if (Dm.eq(new_pixel_shift, current_pixel_shift))
            return;
        if (Dm.truthy(current_pixel_shift))
            target.RemoveElement(typeof(datum_element_elevation_core), current_pixel_shift);
        if (Dm.truthy(new_pixel_shift))
            await target.AddElement(typeof(datum_element_elevation_core), new_pixel_shift);
    }

    // tg: code/datums/elements/elevation.dm:44
    [SignalHandler]
    public virtual void check_elevation(turf source = null, DmList current_values = null)
    {
        current_values[ELEVATION_MAX_PIXEL_SHIFT] = max(current_values[ELEVATION_MAX_PIXEL_SHIFT], pixel_shift);
    }

    // tg: code/datums/elements/elevation.dm:48
    [SignalHandler]
    public virtual void on_source_entering(atom_movable source = null, atom entering = null, atom old_loc = null, params object[] __extra)
    {
        Waiting.NoSleep(register_turf(source, entering));
    }

    // tg: code/datums/elements/elevation.dm:52
    [SignalHandler]
    public virtual void on_source_exiting(atom_movable source = null, atom exiting = null, params object[] __extra)
    {
        Waiting.NoSleep(unregister_turf(source, exiting));
    }

    // tg: code/datums/elements/elevation.dm:56
    public virtual async DmTask register_turf(atom_movable source = null, atom location = null)
    {
        if (!isturf(location))
            return;
        if (!HAS_TRAIT(location, TRAIT_TURF_HAS_ELEVATED_OBJ(pixel_shift)))
        {
            RegisterSignal(location, COMSIG_TURF_RESET_ELEVATION, nameof(check_elevation));
            await reset_elevation((turf)location); // This needs to go in the before COMSIG_TURF_CHANGE, or we can end up bouncing back into this and getting a runtime
            RegisterSignal(location, COMSIG_TURF_CHANGE, nameof(pre_change_turf));
        }
        ADD_TRAIT(location, TRAIT_TURF_HAS_ELEVATED_OBJ(pixel_shift), REF(source));
    }

    // tg: code/datums/elements/elevation.dm:65
    public virtual async DmTask unregister_turf(atom_movable source = null, atom location = null)
    {
        if (!isturf(location))
            return;
        REMOVE_TRAIT(location, TRAIT_TURF_HAS_ELEVATED_OBJ(pixel_shift), REF(source));
        if (!HAS_TRAIT(location, TRAIT_TURF_HAS_ELEVATED_OBJ(pixel_shift)))
        {
            UnregisterSignal(location, Dm.list(COMSIG_TURF_RESET_ELEVATION, COMSIG_TURF_CHANGE));
            await reset_elevation((turf)location);
        }
    }

    /// When a turf with elevated objects changes, we need to unregister all the elevating objects on it. When a turf Initializes(),
    /// it calls Entered() on all of its moveable contents, which will invoke on_source_entering(), which will register each elevating
    /// object with the new turf. We need to do this because turfs do not keep their traits when changed, and so the check for
    /// TRAIT_TURF_HAS_ELEVATED_OBJ above will fail and cause override runtimes when we attempt to register the signals again.
    // tg: code/datums/elements/elevation.dm:77
    [SignalHandler]
    public virtual void pre_change_turf(turf changed = null, object path = null, DmList new_baseturfs = null, object flags = null, DmList post_change_callbacks = null)
    {
        foreach (atom_movable content in Dm.each<atom_movable>(changed))
        {
            if (HAS_TRAIT_FROM(content, TRAIT_ELEVATING_OBJECT, REF(this)))
                Waiting.NoSleep(unregister_turf(content, changed));
        }
    }
}

/**
 * The core element attached to the turf itself. Do not use this directly!
 *
 * Causes mobs walking over a turf with this element to be pixel shifted vertically by the pixel_shift amount.
 * Because of the way it's structured, it should only be added through the elevation element (without the core suffix).
 *
 * To explain: in the case of multiple objects with (different instances of) the element being stacked on one turf somehow,
 * we only want that with the highest pixel shift value to apply it to the turf, so that the mobs standing on top of it all
 * doesn't look like it's floating off the pile.
 */
// tg: code/datums/elements/elevation.dm:96
public partial class datum_element_elevation_core : datum_element
{
    // tg: code/datums/elements/elevation.dm:83  #define ELEVATE_TIME 0.2 SECONDS (file-local)
    private const float ELEVATE_TIME = 0.2f * SECONDS;

    // tg: code/datums/elements/elevation.dm:84  #define ELEVATION_SOURCE(datum) "elevation_[REF(datum)]"
    private static string ELEVATION_SOURCE(datum d) => Dm.text("elevation_[]", REF(d));

    // tg: code/datums/elements/elevation.dm:102
    [DmParams("target", "pixel_shift")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object pixel_shift = Dm.arg(__extra, 0);
        object dot = await base.Attach(target, Dm.forward(__extra, pixel_shift));
        if (!isturf(target))
            return ELEMENT_INCOMPATIBLE;
        if (!Dm.truthy(pixel_shift))
            throw Dm.CRASH(Dm.text("attempted attaching /datum/element/elevation_core with a pixel_shift value of []", pixel_shift == null ? "null" : 0), "code/datums/elements/elevation.dm", 107);

        RegisterSignal(target, COMSIG_ATOM_ABSTRACT_ENTERED, nameof(on_entered));
        RegisterSignal(target, COMSIG_ATOM_AFTER_SUCCESSFUL_INITIALIZED_ON, nameof(on_initialized_on));
        RegisterSignal(target, COMSIG_ATOM_ABSTRACT_EXITED, nameof(on_exited));
        RegisterSignal(target, COMSIG_TURF_RESET_ELEVATION, nameof(on_reset_elevation));

        this.pixel_shift = pixel_shift;

        ADD_TRAIT(target, TRAIT_ELEVATED_TURF, ELEVATION_SOURCE(this));

        foreach (mob_living living in Dm.each<mob_living>(target))
            register_new_mob(living);
        return dot;
    }

    // tg: code/datums/elements/elevation.dm:121
    [DmParams("source")]
    public override object Detach(datum source = null, params object[] __extra)
    {
        /**
         * Since the element can be removed outside of Destroy(),
         * and even then, signals are passed down to the new turf,
         * it's necessary to clear them here.
         */
        UnregisterSignal(source, Dm.list(
            COMSIG_ATOM_ABSTRACT_ENTERED,
            COMSIG_ATOM_ABSTRACT_EXITED,
            COMSIG_ATOM_AFTER_SUCCESSFUL_INITIALIZED_ON,
            COMSIG_TURF_RESET_ELEVATION));
        foreach (mob_living living in Dm.each<mob_living>(source))
        {
            deelevate_mob(living);
            UnregisterSignal(living, Dm.list(COMSIG_LIVING_SET_BUCKLED, SIGNAL_ADDTRAIT(TRAIT_IGNORE_ELEVATION), SIGNAL_REMOVETRAIT(TRAIT_IGNORE_ELEVATION)));
        }
        REMOVE_TRAIT(source, TRAIT_ELEVATED_TURF, ELEVATION_SOURCE(this));
        return base.Detach(source, __extra);
    }

    // tg: code/datums/elements/elevation.dm:139
    [SignalHandler]
    public virtual void on_entered(turf source = null, atom_movable entered = null, atom old_loc = null, params object[] __extra)
    {
        // If the movement has been aborted by something else within the chain we need to abort
        if (!isliving(entered) || entered.loc != source)
            return;

        if (old_loc == null || !HAS_TRAIT_FROM(old_loc, TRAIT_ELEVATED_TURF, ELEVATION_SOURCE(this)))
            register_new_mob((mob_living)entered, elevate_time: isturf(old_loc) && Dm.truthy(Waiting.NoSleep(source.Adjacent(old_loc))) ? ELEVATE_TIME : 0);
    }

    // tg: code/datums/elements/elevation.dm:148
    [SignalHandler]
    public virtual void on_initialized_on(turf source = null, atom_movable spawned = null, params object[] __extra)
    {
        if (isliving(spawned))
            register_new_mob((mob_living)spawned, elevate_time: 0);
    }

    // tg: code/datums/elements/elevation.dm:153
    [SignalHandler]
    public virtual void on_exited(turf source = null, atom_movable gone = null, params object[] __extra)
    {
        if ((gone.loc == null || !HAS_TRAIT_FROM(gone.loc, TRAIT_ELEVATED_TURF, ELEVATION_SOURCE(this))) && isliving(gone))
        {
            // Always unregister the signals, we're still leaving even if not affected by elevation.
            UnregisterSignal(gone, Dm.list(COMSIG_LIVING_SET_BUCKLED, SIGNAL_ADDTRAIT(TRAIT_IGNORE_ELEVATION), SIGNAL_REMOVETRAIT(TRAIT_IGNORE_ELEVATION)));
            deelevate_mob((mob_living)gone, isturf(gone.loc) && Dm.truthy(Waiting.NoSleep(source.Adjacent(gone.loc))) ? ELEVATE_TIME : 0);
        }
    }

    /// Registers a new mob to be elevated, and elevates it.
    // tg: code/datums/elements/elevation.dm:161
    public virtual void register_new_mob(mob_living new_mob = null, float? elevate_time = null)
    {
        elevate_time ??= ELEVATE_TIME;
        elevate_mob(new_mob, elevate_time: elevate_time);
        // mobs can reasonably be reigstered twice if the element is attached and then their init finishes
        RegisterSignal(new_mob, COMSIG_LIVING_SET_BUCKLED, nameof(on_set_buckled), @override: TRUE);
        RegisterSignal(new_mob, SIGNAL_ADDTRAIT(TRAIT_IGNORE_ELEVATION), nameof(on_ignore_elevation_add), @override: TRUE);
        RegisterSignal(new_mob, SIGNAL_REMOVETRAIT(TRAIT_IGNORE_ELEVATION), nameof(on_ignore_elevation_remove), @override: TRUE);
    }

    /**
     * Elevates the mob by pixel_shift amount.
     *
     * If the mob has the TRAIT_IGNORE_ELEVATION trait, it will not be elevated.
     *
     * If the mob is buckled to something...
     * ...And that something is a vehicle, it will also be elevated.
     * ...And that something is an object, neither the mob nor the object will be elevated.
     * ...And that something is a mob, we will be elevated (but not the other mob).
     */
    // tg: code/datums/elements/elevation.dm:178
    public virtual void elevate_mob(mob_living target = null, float? elevate_time = null, float? force = null)
    {
        elevate_time ??= ELEVATE_TIME;
        force ??= FALSE;
        if (HAS_TRAIT(target, TRAIT_IGNORE_ELEVATION) && !Dm.truthy(force))
            return;
        // while the offset system can natively handle this,
        // we want to avoid accidentally double-elevating anything they're buckled to (namely vehicles)
        if (Dm.truthy(target.has_offset(source: ELEVATION_SOURCE(this))))
            return;
        // We are buckled to something
        if (target.buckled != null)
        {
            // We are buckled to a vehicle, so it also must be elevated
            if (isvehicle(target.buckled))
                animate(target.buckled, Dm.named("pixel_z", pixel_shift), Dm.named("time", elevate_time), Dm.named("flags", Dm.bitor(ANIMATION_RELATIVE, ANIMATION_PARALLEL)));
            // We are buckled to a mob - they're elevated so we're elevated
            else if (isliving(target.buckled))
            {
                // pass()
            }
            // We are buckled to some other object - perhaps the object itself - so skip
            else
            {
                ADD_TRAIT(target, TRAIT_MOB_ELEVATED, ELEVATION_SOURCE(this));
                return;
            }
        }

        target.add_offsets(ELEVATION_SOURCE(this), z_add: pixel_shift, animate: elevate_time > 0 ? TRUE : FALSE);
        ADD_TRAIT(target, TRAIT_MOB_ELEVATED, ELEVATION_SOURCE(this));
    }

    /// Reverts elevation of the mob.
    // tg: code/datums/elements/elevation.dm:202
    public virtual void deelevate_mob(mob_living target = null, float? elevate_time = null)
    {
        elevate_time ??= ELEVATE_TIME;
        target.remove_offsets(ELEVATION_SOURCE(this), animate: elevate_time > 0 ? TRUE : FALSE);
        if (isvehicle(target.buckled))
            animate(target.buckled, Dm.named("pixel_z", -Dm.num(pixel_shift)), Dm.named("time", elevate_time), Dm.named("flags", Dm.bitor(ANIMATION_RELATIVE, ANIMATION_PARALLEL)));
        REMOVE_TRAIT(target, TRAIT_MOB_ELEVATED, ELEVATION_SOURCE(this));
    }

    /**
     * If the mob is buckled or unbuckled to/from a vehicle, shift it up/down
     *.
     * Null the pixel shift if the mob is buckled to something different that's not a mob or vehicle
     *
     * The reason is that it's more important for a mob to look like they're actually buckled to a bed
     * or something anchored to the floor than atop of whatever else is on the same turf.
     */
    // tg: code/datums/elements/elevation.dm:216
    [SignalHandler]
    public virtual void on_set_buckled(mob_living source = null, atom_movable new_buckled = null, params object[] __extra)
    {
        if (HAS_TRAIT(source, TRAIT_IGNORE_ELEVATION))
            return;
        // We were buckled to something
        if (source.buckled != null)
        {
            // It was a vehicle, so reset its pixel_z
            if (isvehicle(source.buckled))
                animate(source.buckled, Dm.named("pixel_z", -Dm.num(pixel_shift)), Dm.named("time", ELEVATE_TIME), Dm.named("flags", Dm.bitor(ANIMATION_RELATIVE, ANIMATION_PARALLEL)));
            // It was a mob, so revert our pixel_z
            else if (isliving(source.buckled))
                deelevate_mob(source);
            // It was some object, maybe the object itself, elevate us
            else
                source.add_offsets(ELEVATION_SOURCE(this), z_add: pixel_shift);
        }
        // We are now buckled to something
        if (new_buckled != null)
        {
            // It's a vehicle, so elevate it
            if (isvehicle(new_buckled))
                animate(new_buckled, Dm.named("pixel_z", pixel_shift), Dm.named("time", ELEVATE_TIME), Dm.named("flags", Dm.bitor(ANIMATION_RELATIVE, ANIMATION_PARALLEL)));
            // It's a mob, so elevate us
            else if (isliving(new_buckled))
                elevate_mob(source);
            // It's some object, maybe the object itself, so clear elevation
            else
                source.remove_offsets(ELEVATION_SOURCE(this));
        }
    }

    // tg: code/datums/elements/elevation.dm:243
    [SignalHandler]
    public virtual void on_ignore_elevation_add(mob_living source = null, object trait = null)
    {
        deelevate_mob(source);
    }

    // tg: code/datums/elements/elevation.dm:247
    [SignalHandler]
    public virtual void on_ignore_elevation_remove(mob_living source = null, object trait = null)
    {
        elevate_mob(source);
    }

    // tg: code/datums/elements/elevation.dm:251
    [SignalHandler]
    public virtual void on_reset_elevation(turf source = null, DmList current_values = null)
    {
        current_values[ELEVATION_CURRENT_PIXEL_SHIFT] = pixel_shift;
    }
}
