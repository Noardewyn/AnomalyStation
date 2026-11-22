// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/projectile_drop.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate G4b, D316: the rebar crossbow's rods fall to the floor). The var block is generated (D111).

/**
 * A simple element that spawns an atom when the bullet hits an object or reaches the end of its range
 * If the projectile has embedding and it can embed into the target, then it won't spawn the drop,
 * since embedding the embed element already handles that.
 */
// tg: code/datums/elements/projectile_drop.dm:6
public partial class datum_element_projectile_drop : datum_element
{
    // tg: code/datums/elements/projectile_drop.dm:11
    [DmParams("target", "drop_type")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object drop_type = Dm.arg(__extra, 0);
        object dot = await base.Attach(target, __extra);
        if (!isprojectile(target))
            return ELEMENT_INCOMPATIBLE;
        this.drop_type = drop_type as System.Type;
        RegisterSignal(target, COMSIG_PROJECTILE_RANGE_OUT, nameof(spawn_drop));
        RegisterSignal(target, COMSIG_PROJECTILE_SELF_ON_HIT, nameof(spawn_drop_if_not_embeddable));
        return dot;
    }

    // tg: code/datums/elements/projectile_drop.dm:19
    public override object Detach(datum source = null, params object[] __extra)
    {
        UnregisterSignal(source, Dm.list(COMSIG_PROJECTILE_RANGE_OUT, COMSIG_PROJECTILE_SELF_ON_HIT));
        return base.Detach(source, __extra);
    }

    // tg: code/datums/elements/projectile_drop.dm:23
    [SignalHandler]
    public virtual void spawn_drop(obj_projectile source = null)
    {
        turf turf = get_turf(source);
        atom new_drop = Dm.@new(drop_type, turf) as atom;
        if (Dm.truthy(source._listen_lookup?[COMSIG_PROJECTILE_ON_SPAWN_DROP]))
            source._SendSignal(COMSIG_PROJECTILE_ON_SPAWN_DROP, Dm.list(source, new_drop));
        //Just to be safe, knowing it won't be spawned multiple times.
        Detach(source);
    }

    // tg: code/datums/elements/projectile_drop.dm:31
    [SignalHandler]
    public virtual void spawn_drop_if_not_embeddable(obj_projectile source = null, atom_movable firer = null, atom hit = null, object angle = null, object hit_zone = null, object blocked = null, object pierce_hit = null)
    {
        if (Dm.truthy(pierce_hit))
            return;
        if (Dm.truthy(source.can_embed_into(hit)))
        {
            Detach(source);
            return;
        }
        spawn_drop(source);
    }
}
