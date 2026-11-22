// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/decals/chemspray.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8 slice C: the whole file but blob_act (a live-partial row: blobs): a spray's puff of reagents drifting to its target,
// exposing what it crosses. The var block is generated.

//Used by spraybottles.
// tg: code/game/objects/effects/decals/chemspray.dm:2
public partial class obj_effect_decal_chempuff : obj_effect_decal
{
    // tg: code/game/objects/effects/decals/chemspray.dm:19
    public override object Destroy(float? force = null)
    {
        user = null;
        sprayer = null;
        return base.Destroy(force);
    }

    // tg: code/game/objects/effects/decals/chemspray.dm:27
    public virtual void end_life(object delay = null)
    {
        delay ??= 0.5f * SECONDS;
        float time = Dm.num(delay);
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), time > GC_FILTER_QUEUE ? (object)WEAKREF(this) : this), time, file: "code/game/objects/effects/decals/chemspray.dm", line: 28); //Gotta let it stop drifting // QDEL_IN(src, delay), inline (rules 11.1 (f))
        animate(this, Dm.named("alpha", 0), Dm.named("time", delay));
    }

    // tg: code/game/objects/effects/decals/chemspray.dm:31
    [SignalHandler]
    public virtual void loop_ended(datum_move_loop source = null)
    {
        if (QDELETED(this))
            return;
        end_life(source.delay);
    }

    // tg: code/game/objects/effects/decals/chemspray.dm:37
    [SignalHandler]
    public virtual void check_move(datum_move_loop source = null, object result = null)
    {
        if (QDELETED(this)) //Reasons PLEASE WORK I SWEAR TO GOD
            return;
        if (Dm.eq(result, MOVELOOP_FAILURE)) //If we hit something
        {
            end_life(source.delay);
            return;
        }

        Waiting.NoSleep(spray_down_turf(get_turf(this), travelled_max_distance: (Dm.num(source.lifetime) - Dm.num(source.delay) <= 0) ? TRUE : FALSE));

        if (lifetime < 0) // Did we use up all the puff early?
            end_life(source.delay);
    }

    /**
     * Handles going through every movable on the passed turf and calling [spray_down_atom] on them.
     *
     * [travelled_max_distance] is used to determine if we're at the end of the life, as in some
     * contexts an atom may or may not end up being exposed depending on how far we've travelled.
     */
    // tg: code/game/objects/effects/decals/chemspray.dm:57
    public virtual async DmTask spray_down_turf(turf spraying = null, float? travelled_max_distance = null)
    {
        travelled_max_distance ??= FALSE;
        foreach (atom_movable turf_atom in Dm.each<atom_movable>(spraying))
        {
            if (turf_atom == this || Dm.truthy(turf_atom.invisibility)) //we ignore the puff itself and stuff below the floor
                continue;

            if (lifetime < 0)
                break;

            if (!Dm.truthy(stream))
            {
                await spray_down_atom(turf_atom);
                if (ismob(turf_atom))
                    lifetime -= 1;
                continue;
            }

            if (isliving(turf_atom))
            {
                mob_living turf_mob = (mob_living)turf_atom;

                if (!Dm.truthy(turf_mob.can_inject()))
                    continue;
                if (!Dm.eq(turf_mob.body_position, STANDING_UP) && !Dm.truthy(travelled_max_distance))
                    continue;

                await spray_down_atom(turf_atom);
                lifetime -= 1;
            }
            else if (Dm.truthy(travelled_max_distance))
            {
                await spray_down_atom(turf_atom);
                lifetime -= 1;
            }
        }

        if (lifetime >= 0 && (!Dm.truthy(stream) || Dm.truthy(travelled_max_distance)))
        {
            await spray_down_atom(spraying);
            lifetime -= 1;
        }
    }

    /// Actually handles exposing the passed atom to the reagents and logging
    // tg: code/game/objects/effects/decals/chemspray.dm:92
    public virtual async DmTask spray_down_atom(atom spraying = null)
    {
        if (logging_string == null)
            logging_string = ((datum_reagents)reagents).get_reagent_log_string();
        await ((datum_reagents)reagents).expose(spraying, VAPOR);
        log_combat(user, spraying, "sprayed", sprayer, addition: Dm.text("which had []", logging_string));
    }
}
