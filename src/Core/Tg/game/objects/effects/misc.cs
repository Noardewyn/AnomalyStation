// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/misc.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only the spawner base /obj/effect/spawner translated (M3 P4, the map's spawners); the file's other effects (the
// beams, the list container, the shared effects of misc.dm) come with their milestones.

// tg: code/game/objects/effects/misc.dm:21
public partial class obj_effect_spawner : obj_effect
{
    // Brief explanation:
    // Rather then setting up and then deleting spawners, we block all atomlike setup
    // and do the absolute bare minimum
    // This is with the intent of optimizing mapload
    // tg: code/game/objects/effects/misc.dm:28  (SHOULD_CALL_PARENT(FALSE) is dropped, rules 7.9)
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(flags_1, INITIALIZED_1)))
            stack_trace(Dm.text("Warning: []([]) initialized multiple times!", this, type), "code/game/objects/effects/misc.dm", 31);
        Dm.combine(ref flags_1, INITIALIZED_1);

        return INITIALIZE_HINT_QDEL;
    }

    // tg: code/game/objects/effects/misc.dm:36  (SHOULD_CALL_PARENT(FALSE) is dropped, rules 7.9)
    public override object Destroy(float? force = null)
    {
        Waiting.NoSleep(moveToNullspace());
        return QDEL_HINT_QUEUE;
    }

    // tg: code/game/objects/effects/misc.dm:41
    public override async DmTask<object> forceMove(atom destination = null, params object[] __extra)
    {
        if (Dm.truthy(destination) && QDELETED(this)) // throw a warning if we try to forceMove a qdeleted spawner to somewhere other than nullspace
            stack_trace(Dm.text("Warning: something tried to forceMove() a qdeleted []([]) to non-null destination []([])!", this, type, destination, destination.type), "code/game/objects/effects/misc.dm", 43);
        return await base.forceMove(destination, __extra);
    }

    /// Override to define loot blacklist behavior
    // tg: code/game/objects/effects/misc.dm:47
    public virtual object can_spawn(object loot = null)
    {
        if (!ispath(loot))
            // Means its something evil like /obj/item/stack/sheet/mineral/diamond{amount = 15}
            // (modified instances?) which is not a path and cannot be checked as one
            return TRUE;
        if (Dm.initial((Type)loot, (atom p) => p.abstract_type) == (Type)loot)
            return FALSE;
        if (Dm.truthy(Dm.initial((Type)loot, (atom p) => p.spawn_blacklisted)))
            return FALSE;
        return TRUE;
    }
}
