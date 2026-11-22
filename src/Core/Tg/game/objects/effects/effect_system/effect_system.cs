// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/effect_system/effect_system.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M4c slice 6: the jetpack's ion trail is an effect system, effects_trail.dm). The var blocks are generated
// (D111) but for /datum/effect_system's: its `location` is declared here as tg's /turf (the generator's V3 grep took it for
// a typepath var: another type's `location` is given typepaths), so its constructor is here as well (types-converter.md 3.2,
// the Hand mode).

/*
 * This is an attempt to make some easily reusable "particle" type effect, to stop the code
 * constantly having to be rewritten. An item like the jetpack that uses the ion_trail_follow system, just has one
 * defined, then set up when it is created with New(). Then this same system can just be reused each time
 * it needs to create more trails.A beaker could have a steam_trail_follow system set up, then the steam
 * would spawn and follow the beaker, even if it is carried or thrown.
*/

// tg: code/game/objects/effects/effect_system/effect_system.dm:11
public partial class obj_effect_particle_effect : obj_effect
{
    // tg: code/game/objects/effects/effect_system/effect_system.dm:9  #define PER_SYSTEM_PARTICLE_CAP 20 (file-local, #undef at :131)
    internal const float PER_SYSTEM_PARTICLE_CAP = 20;

    // Prevents effects from getting registered for SSnewtonian_movement
    // tg: code/game/objects/effects/effect_system/effect_system.dm:18
    public override async DmTask<object> newtonian_move(object inertia_angle = null, float? instant = null, float? start_delay = null, float? drift_force = null, object controlled_cap = null, float? force_loop = null)
    {
        return TRUE;
    }
}

// tg: code/game/objects/effects/effect_system/effect_system.dm:21
public partial class datum_effect_system : datum
{
    /// Turf on which to spawn the effects
    // tg: code/game/objects/effects/effect_system/effect_system.dm:25
    public turf location = null;

    // tg: code/game/objects/effects/effect_system/effect_system.dm:21
    public datum_effect_system()
    {
        // Does not contain any behaviors and should not be used by itself
        abstract_type = typeof(datum_effect_system);
    }

    // tg: code/game/objects/effects/effect_system/effect_system.dm:29
    [DmParams("location")]
    public override async DmTask New(params object[] __extra)
    {
        // turf/location: the trails pass the mob they follow (jetpack.dm:94); get_turf takes either
        object location = Dm.arg<object>(__extra, 0);
        await base.New(__extra);
        this.location = get_turf(location);
    }

    // tg: code/game/objects/effects/effect_system/effect_system.dm:33
    public override object Destroy(float? force = null)
    {
        holder = null;
        location = null;
        return base.Destroy(force);
    }

    /// Instruct the effect system to start following an atom. Can be chained into .start()
    // tg: code/game/objects/effects/effect_system/effect_system.dm:39
    public virtual datum_effect_system attach(atom new_holder = null)
    {
        holder = new_holder;
        return this;
    }

    /// Start the effect system
    // tg: code/game/objects/effects/effect_system/effect_system.dm:45
    public virtual async DmTask<object> start(params object[] __extra)
    {
        return null;
    }
}

/// Basic effect system which spawns a certain number of moving effects
// tg: code/game/objects/effects/effect_system/effect_system.dm:49
public partial class datum_effect_system_basic : datum_effect_system
{
    // tg: code/game/objects/effects/effect_system/effect_system.dm:71
    [DmParams("location", "amount", "cardinals_only")]
    public override async DmTask New(params object[] __extra)
    {
        object amount = Dm.arg<object>(__extra, 1);
        object cardinals_only = Dm.arg<object>(__extra, 2);
        await base.New(__extra);
        if (amount != null)
            this.amount = Dm.num(amount);
        if (cardinals_only != null)
            this.cardinals_only = Dm.num(cardinals_only);
    }

    // tg: code/game/objects/effects/effect_system/effect_system.dm:78
    public override async DmTask<object> start(params object[] __extra)
    {
        if (QDELETED(this))
            return null;
        for (float i = 1; i <= Dm.num(amount); i++)
        {
            if (total_effects > obj_effect_particle_effect.PER_SYSTEM_PARTICLE_CAP)
                return null;
            generate_effect();
        }
        return null;
    }

    /// Returns how many steps to attempt to move a generated effect
    // tg: code/game/objects/effects/effect_system/effect_system.dm:87
    public virtual object get_step_count()
    {
        return rand(1, 3);
    }

    /// Generates a effect for our system to control, returns the generated effect
    // tg: code/game/objects/effects/effect_system/effect_system.dm:91
    public virtual object generate_effect()
    {
        if (Dm.truthy(holder))
            location = get_turf(holder);
        obj_effect effect = Dm.@new(effect_type, location) as obj_effect;
        total_effects++;

        if (!Dm.truthy(length(pickable_dirs)))
        {
            if (Dm.truthy(cardinals_only))
                pickable_dirs = GLOB.cardinals.Copy();
            else
                pickable_dirs = GLOB.alldirs.Copy();
        }
        // Try not to reuse dirs if possible to avoid weird stacking
        object direction = pick_n_take(pickable_dirs);

        object step_count = get_step_count();
        datum_move_loop loop = GLOB.move_manager.move(effect, direction, step_delay, timeout: step_delay * Dm.num(step_count), priority: MOVEMENT_ABOVE_SPACE_PRIORITY, flags: MOVEMENT_LOOP_START_FAST) as datum_move_loop;
        RegisterSignal(loop, COMSIG_MOVELOOP_POSTPROCESS, nameof(post_move));
        RegisterSignal(loop, COMSIG_QDELETING, nameof(loop_end));
        last_loop_length = loop.lifetime;
        return effect;
    }

    // tg: code/game/objects/effects/effect_system/effect_system.dm:112
    [SignalHandler]
    public virtual void post_move(datum_move_loop source = null, object result = null, object visual_delay = null)
    {
        if (Dm.eq(result, MOVELOOP_FAILURE))
            move_failed(source, source.moving as obj_effect);
    }

    /// Allows us to hook into being unable to automatically move
    // tg: code/game/objects/effects/effect_system/effect_system.dm:118
    public virtual void move_failed(datum_move_loop loop = null, obj_effect failed = null)
    {
        if (QDELETED(failed) || !Dm.truthy(delete_on_stop))
            return;
        qdel(failed);
    }

    // tg: code/game/objects/effects/effect_system/effect_system.dm:123
    [SignalHandler]
    public virtual void loop_end(datum_move_loop source = null)
    {
        total_effects--;
        if (Dm.truthy(delete_on_stop) && !QDELETED(source.moving))
            qdel(source.moving);
        if (Dm.truthy(autocleanup) && total_effects == 0)
        {
            // QDEL_IN(src, 2 SECONDS) (__DEFINES/qdel.dm:59), expanded inline (rules 11.1 (f))
            _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), (2 * SECONDS > GC_FILTER_QUEUE ? (object)WEAKREF(this) : this)), 2 * SECONDS, file: "code/game/objects/effects/effect_system/effect_system.dm", line: 129);
        }
    }
}
