// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/effect_system/effects_water.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4b (the fire extinguisher): the whole file: the water particle, the extinguisher's water and the stomach acid that its
// move loop carries, and the steam particle. The steam spreader (/datum/effect_system/basic/steam_spread) is a var-only
// subtype of the effect system, which comes with its users. The var blocks are generated (D111).

//WATER EFFECTS

// tg: code/game/objects/effects/effect_system/effects_water.dm:3
[DmPath("/obj/effect/particle_effect/water")]
public partial class obj_effect_particle_effect_water : obj_effect_particle_effect
{
    // tg: code/game/objects/effects/effect_system/effects_water.dm:11
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), 7 * SECONDS > GC_FILTER_QUEUE ? (object)WEAKREF(this) : this), 7 * SECONDS, file: "code/game/objects/effects/effect_system/effects_water.dm", line: 13); // QDEL_IN(src, 7 SECONDS), inline (rules 11.1 (f))
        return dot;
    }

    // tg: code/game/objects/effects/effect_system/effects_water.dm:15
    public override async DmTask<object> Move(atom newloc = null, object direct = null, float? glide_size_override = null, float? update_dir = null)
    {
        life -= 1;
        if (life <= 0)
        {
            qdel(this);
            return FALSE;
        }
        return await base.Move(newloc, direct, glide_size_override, update_dir);
    }

    // tg: code/game/objects/effects/effect_system/effects_water.dm:22
    public override async DmTask<object> Bump(atom bumped_atom = null)
    {
        atom A = bumped_atom;
        if (reagents is datum_reagents holder)
        {
            await holder.expose(A);
            if (A.reagents is datum_reagents A_reagents)
                await A_reagents.expose_temperature(holder.chem_temp);
        }
        return await base.Bump(bumped_atom);
    }
}

///Extinguisher snowflake
// tg: code/game/objects/effects/effect_system/effects_water.dm:29
[DmPath("/obj/effect/particle_effect/water/extinguisher")]
public partial class obj_effect_particle_effect_water_extinguisher : obj_effect_particle_effect_water
{
    // tg: code/game/objects/effects/effect_system/effects_water.dm:31
    public override async DmTask<object> Move(atom newloc = null, object direct = null, float? glide_size_override = null, float? update_dir = null)
    {
        object dot = await base.Move(newloc, direct, glide_size_override, update_dir);
        if (reagents is not datum_reagents holder)
            return dot;
        await holder.expose(get_turf(this));
        foreach (atom thing in Dm.each_cast<atom>(get_turf(this)))
            await holder.expose(thing);
        return dot;
    }

    /// Starts the effect moving at a target with a delay in deciseconds, and a lifetime in moves
    /// Returns the created loop
    // tg: code/game/objects/effects/effect_system/effects_water.dm:41
    public virtual object move_at(atom target = null, object delay = null, object lifetime = null)
    {
        datum_move_loop loop = GLOB.move_manager.move_towards_legacy(this, target, delay, timeout: Dm.num(delay) * Dm.num(lifetime), priority: MOVEMENT_ABOVE_SPACE_PRIORITY, flags: MOVEMENT_LOOP_START_FAST) as datum_move_loop;
        RegisterSignal(loop, COMSIG_MOVELOOP_POSTPROCESS, nameof(post_forcemove));
        RegisterSignal(loop, COMSIG_QDELETING, nameof(movement_stopped));
        return loop;
    }

    // tg: code/game/objects/effects/effect_system/effects_water.dm:47
    [SignalHandler]
    public virtual void post_forcemove(datum_move_loop source = null, object success = null)
    {
        // SIGNAL_HANDLER
        if (!Dm.truthy(success))
            end_life(source);
    }

    // tg: code/game/objects/effects/effect_system/effects_water.dm:52
    [SignalHandler]
    public virtual void movement_stopped(datum_move_loop source = null)
    {
        // SIGNAL_HANDLER
        if (!QDELETED(this))
            end_life(source);
    }

    // tg: code/game/objects/effects/effect_system/effects_water.dm:57
    public virtual void end_life(datum_move_loop engine = null)
    {
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), engine.delay > GC_FILTER_QUEUE ? (object)WEAKREF(this) : this), engine.delay, file: "code/game/objects/effects/effect_system/effects_water.dm", line: 58); //Gotta let it stop drifting // QDEL_IN(src, engine.delay), inline (rules 11.1 (f))
        animate(this, Dm.named("alpha", 0), Dm.named("time", engine.delay));
    }
}

// tg: code/game/objects/effects/effect_system/effects_water.dm:61  (the vars are generated, D111)
[DmPath("/obj/effect/particle_effect/water/extinguisher/stomach_acid")]
public partial class obj_effect_particle_effect_water_extinguisher_stomach_acid : obj_effect_particle_effect_water_extinguisher
{
    // Stomach acid doesn't use legacy because it's not "targeted", and we instead want the circular sorta look
    // tg: code/game/objects/effects/effect_system/effects_water.dm:66
    public override object move_at(atom target = null, object delay = null, object lifetime = null)
    {
        datum_move_loop loop = GLOB.move_manager.move_towards(this, target, delay, timeout: Dm.num(delay) * Dm.num(lifetime), flags: MOVEMENT_LOOP_START_FAST, priority: MOVEMENT_ABOVE_SPACE_PRIORITY) as datum_move_loop;
        RegisterSignal(loop, COMSIG_MOVELOOP_POSTPROCESS, nameof(post_forcemove));
        RegisterSignal(loop, COMSIG_QDELETING, nameof(movement_stopped));
        return loop;
    }
}

/////////////////////////////////////////////
// GENERIC STEAM SPREAD SYSTEM

// Usage: set_up(number of bits of steam, use North/South/East/West only, spawn location)
// The attach(atom/atom) proc is optional, and can be called to attach the effect
// to something, like a smoking beaker, so then you can just call start() and the steam
// will always spawn at the items location, even if it's moved.

/////////////////////////////////////////////
// tg: code/game/objects/effects/effect_system/effects_water.dm:81  (the vars are generated, D111)
[DmPath("/obj/effect/particle_effect/steam")]
public partial class obj_effect_particle_effect_steam : obj_effect_particle_effect
{
    // tg: code/game/objects/effects/effect_system/effects_water.dm:86
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), 2 * SECONDS > GC_FILTER_QUEUE ? (object)WEAKREF(this) : this), 2 * SECONDS, file: "code/game/objects/effects/effect_system/effects_water.dm", line: 88); // QDEL_IN(src, 2 SECONDS), inline (rules 11.1 (f))
        return dot;
    }
}
