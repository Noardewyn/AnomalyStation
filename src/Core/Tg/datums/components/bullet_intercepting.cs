// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/bullet_intercepting.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/**
 * Component which allows an equipped item to occasionally absorb a projectile.
 */
// tg: code/datums/components/bullet_intercepting.dm:5
public partial class datum_component_bullet_intercepting : datum_component
{
    // tg: code/datums/components/bullet_intercepting.dm:20
    [DmParams("block_chance", "block_type", "active_slots", "on_intercepted", "block_charges", "is_blocking_check")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object block_chance = Dm.arg(__extra, 0) ?? 2;
        DmList block_type = Dm.arg(__extra, 1) as DmList ?? Dm.list(BULLET);
        object active_slots = Dm.arg(__extra, 2);
        object on_intercepted = Dm.arg(__extra, 3);
        object block_charges = Dm.arg(__extra, 4) ?? INFINITY;
        object is_blocking_check = Dm.arg(__extra, 5);
        object dot = await base.Initialize(__extra);
        if (!isitem(parent))
            return COMPONENT_INCOMPATIBLE;
        this.block_chance = Dm.num(block_chance);
        this.block_type = block_type;
        this.active_slots = active_slots;
        this.on_intercepted = on_intercepted as datum_callback;
        this.block_charges = Dm.num(block_charges);
        this.is_blocking_check = is_blocking_check as datum_callback;

        RegisterSignal(parent, COMSIG_ITEM_EQUIPPED, nameof(on_parent_equipped));
        RegisterSignal(parent, COMSIG_ITEM_PRE_UNEQUIP, nameof(on_unequipped));
        return dot;
    }

    // tg: code/datums/components/bullet_intercepting.dm:34
    public override object Destroy(float? force = null)
    {
        wearer = null;
        on_intercepted = null;
        return base.Destroy(force);
    }

    /// Called when item changes slots, check if we're in a valid location to take bullets
    // tg: code/datums/components/bullet_intercepting.dm:40
    [SignalHandler]
    public virtual void on_parent_equipped(obj_item source = null, mob equipper = null, object slot = null)
    {
        if (wearer != null)
        {
            if (!Dm.truthy(Dm.bitand(active_slots, slot)))
                on_unequipped();
            return;
        }

        if (!Dm.truthy(Dm.bitand(active_slots, slot)))
            return;
        RegisterSignal(equipper, COMSIG_PROJECTILE_PREHIT, nameof(on_wearer_shot));
        RegisterSignal(equipper, COMSIG_QDELETING, nameof(on_wearer_deleted));
        wearer = equipper;
    }

    /// Called when item is unequipped, stop tracking bullets
    // tg: code/datums/components/bullet_intercepting.dm:54
    [SignalHandler]
    public virtual void on_unequipped()
    {
        if (wearer == null)
            return;
        UnregisterSignal(wearer, Dm.list(COMSIG_PROJECTILE_PREHIT, COMSIG_QDELETING));
        wearer = null;
    }

    /// Called when wearer is shot, check if we're going to block the hit
    // tg: code/datums/components/bullet_intercepting.dm:62
    [SignalHandler]
    public virtual object on_wearer_shot(mob_living victim = null, obj_projectile bullet = null)
    {
        if (is_blocking_check != null && !Dm.truthy(Waiting.NoSleep(is_blocking_check.Invoke())))
            return NONE;
        if (victim != wearer || Dm.eq(victim.stat, DEAD) || !prob(Dm.num(block_chance)))
            return NONE;
        foreach (object blocktype in Dm.each(block_type))
        {
            if (Dm.eq(bullet.armor_flag, blocktype))
            {
                if (on_intercepted != null)
                    Waiting.NoSleep(on_intercepted.Invoke(victim, bullet));
                block_charges--;
                if (block_charges <= 0)
                    qdel(this);
                return PROJECTILE_INTERRUPT_HIT;
            }
        }
        return NONE;
    }

    /// Called when wearer is deleted, stop tracking them
    // tg: code/datums/components/bullet_intercepting.dm:78
    [SignalHandler]
    public virtual void on_wearer_deleted()
    {
        wearer = null;
    }
}
