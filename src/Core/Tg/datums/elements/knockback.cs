// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/knockback.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var block is generated.

/**
 * Mobs and items with this element will knock movable targets they hit away from them.
 * Guns and turrets will instead fire projectiles with similar effects.
 */
// tg: code/datums/elements/knockback.dm:5
public partial class datum_element_knockback : datum_element
{
    // tg: code/datums/elements/knockback.dm:13
    [DmParams("target", "throw_distance", "throw_anchored", "throw_gentle")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object throw_distance = Dm.arg(__extra, 0) ?? 1;
        object throw_anchored = Dm.arg(__extra, 1) ?? FALSE;
        object throw_gentle = Dm.arg(__extra, 2) ?? FALSE;
        object dot = await base.Attach(target, Dm.forward(__extra, throw_distance, throw_anchored, throw_gentle));
        if (ismachinery(target) || isstructure(target) || isgun(target) || isprojectilespell(target)) // turrets, etc
            RegisterSignal(target, COMSIG_PROJECTILE_ON_HIT, nameof(projectile_hit));
        else if (isitem(target))
            RegisterSignal(target, COMSIG_ITEM_AFTERATTACK, nameof(item_afterattack));
        else if (ishostile(target))
            RegisterSignal(target, COMSIG_HOSTILE_POST_ATTACKINGTARGET, nameof(hostile_attackingtarget));
        else
            return ELEMENT_INCOMPATIBLE;

        this.throw_distance = throw_distance;
        this.throw_anchored = throw_anchored;
        this.throw_gentle = throw_gentle;
        return dot;
    }

    // tg: code/datums/elements/knockback.dm:28
    public override object Detach(datum source = null, params object[] __extra)
    {
        UnregisterSignal(source, Dm.list(COMSIG_ITEM_AFTERATTACK, COMSIG_HOSTILE_POST_ATTACKINGTARGET, COMSIG_PROJECTILE_ON_HIT));
        return base.Detach(source, __extra);
    }

    /// triggered after an item attacks something
    // tg: code/datums/elements/knockback.dm:33
    [SignalHandler]
    public virtual void item_afterattack(obj_item source = null, atom target = null, mob user = null, DmList modifiers = null)
    {
        Waiting.NoSleep(do_knockback(target, user, get_dir(source, target)));
    }

    /// triggered after a hostile simplemob attacks something
    // tg: code/datums/elements/knockback.dm:39
    [SignalHandler]
    public virtual void hostile_attackingtarget(mob_living_simple_animal_hostile attacker = null, atom target = null, object success = null)
    {
        if (!Dm.truthy(success))
            return;
        Waiting.NoSleep(do_knockback(target, attacker, get_dir(attacker, target)));
    }

    /// triggered after a projectile hits something
    // tg: code/datums/elements/knockback.dm:46
    [SignalHandler]
    public virtual void projectile_hit(datum fired_from = null, atom_movable firer = null, atom target = null, object Angle = null)
    {
        Waiting.NoSleep(do_knockback(target, null, angle2dir(Dm.num(Angle))));
    }

    /**
     * Throw a target in a direction
     *
     * Arguments:
     * * target - Target atom to throw
     * * thrower - Thing that caused this atom to be thrown
     * * throw_dir - Direction to throw the atom
     */
    // tg: code/datums/elements/knockback.dm:60
    public virtual async DmTask do_knockback(atom target = null, mob thrower = null, object throw_dir = null)
    {
        if (!ismovable(target) || throw_dir == null)
            return;
        atom_movable throwee = (atom_movable)target;
        if (Dm.truthy(throwee.anchored) && !Dm.truthy(throw_anchored))
            return;
        // tg flips the element's own distance here, so every later knockback of this shared element throws the other way; kept.
        if (Dm.num(throw_distance) < 0)
        {
            throw_dir = REVERSE_DIR(Dm.num(throw_dir));
            throw_distance = Dm.num(throw_distance) * -1;
        }
        atom throw_target = get_edge_target_turf(throwee, throw_dir) as atom;
        await throwee.safe_throw_at(throw_target, throw_distance, 1, thrower, gentle: Dm.num(throw_gentle));
    }
}
