// Translated from tgstation (snapshot 1d99bd7f): code/modules/bitrunning/virtual_domain/modular_mob_segment.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps BR1 (D339, D341): the whole file, the modular domains' mob segments: a landmark spawns a segment's mobs around itself
// and tells the domain. The segments' mob lists are generated (D111); a segment whose mobs are not live yet spawns them inert.

// tg: code/modules/bitrunning/virtual_domain/modular_mob_segment.dm:1  #define SPAWN_ALWAYS 100; :2 SPAWN_LIKELY 85;
// :3 SPAWN_UNLIKELY 35; :4 SPAWN_RARE 10 (the segments' probability, read by the generated var block)

// tg: code/modules/bitrunning/objects/landmarks.dm:77 (the type), the proc here
public partial class obj_effect_landmark_bitrunning_mob_segment : obj_effect_landmark_bitrunning
{
    /// Handles spawning mobs for this landmark. Sends a signal when done.
    // tg: code/modules/bitrunning/virtual_domain/modular_mob_segment.dm:8
    public virtual async DmTask<object> spawn_mobs(turf origin = null, datum_modular_mob_segment segment = null)
    {
        DmList spawned_mobs = Dm.list();

        Dm.append(ref spawned_mobs, await segment.spawn_mobs(origin));

        SEND_SIGNAL(COMSIG_BITRUNNING_MOB_SEGMENT_SPAWNED, Dm.list(this, spawned_mobs));

        DmList mob_refs = Dm.list();
        foreach (mob_living spawned in Dm.each_cast<mob_living>(spawned_mobs))
        {
            if (QDELETED(spawned))
                continue;

            Dm.append(ref mob_refs, WEAKREF(spawned));
        }

        return mob_refs;
    }
}

/**
 * A list for mob spawning landmarks to use.
 */
// tg: code/modules/bitrunning/virtual_domain/modular_mob_segment.dm:28
public partial class datum_modular_mob_segment : datum
{
    /// Spawns mobs in a circle around the location
    // tg: code/modules/bitrunning/virtual_domain/modular_mob_segment.dm:40
    public virtual async DmTask<object> spawn_mobs(turf origin = null)
    {
        if (!prob(probability))
            return null;

        DmList spawned_mobs = Dm.list();

        float total_amount = Dm.truthy(exact) ? length(mobs) : rand(1, max);

        shuffle_inplace(mobs);

        DmList nearby = Dm.list();
        foreach (turf tile in Dm.each_cast<turf>(RANGE_TURFS(2, origin)))
        {
            if (!Dm.truthy(await tile.is_blocked_turf()))
                Dm.append(ref nearby, tile);
        }

        if (length(nearby) == 0)
        {
            stack_trace("Couldn't find any valid turfs to spawn on", "code/modules/bitrunning/virtual_domain/modular_mob_segment.dm", 56);
            return null;
        }

        for (int index = 1; index <= total_amount; index++)
        {
            // For each of those, we need to find an open space
            turf destination = pick(nearby) as turf;

            Type path; // Either a random mob or the next mob in the list
            if (Dm.truthy(exact))
                path = mobs[index] as Type;
            else
                path = pick(mobs) as Type;

            mob_living mob = await Dm.new_async(path, destination) as mob_living;
            Dm.remove(ref nearby, destination);
            Dm.append(ref spawned_mobs, mob);
        }

        return spawned_mobs;
    }
}
