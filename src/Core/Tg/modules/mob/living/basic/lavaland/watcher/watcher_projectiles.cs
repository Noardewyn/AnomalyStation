// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/lavaland/watcher/watcher_projectiles.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/// Chilling projectile, hurts and slows you down
// tg: code/modules/mob/living/basic/lavaland/watcher/watcher_projectiles.dm:2
public partial class obj_projectile_temp_watcher : obj_projectile_temp
{
    // tg: code/modules/mob/living/basic/lavaland/watcher/watcher_projectiles.dm:10
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_parriable_projectile)));
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/watcher/watcher_projectiles.dm:14
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        blocked ??= 0;
        object dot = await base.on_hit(target, blocked, pierce_hit);
        if (!isliving(target))
            return dot;
        await apply_status(target as mob_living);
        return dot;
    }

    /// Apply an additional on-hit effect
    // tg: code/modules/mob/living/basic/lavaland/watcher/watcher_projectiles.dm:21
    public virtual async DmTask apply_status(mob_living target = null)
    {
        await target.apply_status_effect(typeof(datum_status_effect_freezing_blast));
    }
}

/// Lava projectile, ignites you
// tg: code/modules/mob/living/basic/lavaland/watcher/watcher_projectiles.dm:25
public partial class obj_projectile_temp_watcher_magma_wing : obj_projectile_temp_watcher
{
    // tg: code/modules/mob/living/basic/lavaland/watcher/watcher_projectiles.dm:31
    public override async DmTask apply_status(mob_living target = null)
    {
        await target.adjust_fire_stacks(0.1f);
        target.ignite_mob();
    }
}

/// Freezing projectile, freezes you
// tg: code/modules/mob/living/basic/lavaland/watcher/watcher_projectiles.dm:36
public partial class obj_projectile_temp_watcher_ice_wing : obj_projectile_temp_watcher
{
    // tg: code/modules/mob/living/basic/lavaland/watcher/watcher_projectiles.dm:40 (tg freezes only the cold-resistant: kept as tg has it)
    public override async DmTask apply_status(mob_living target = null)
    {
        if (!HAS_TRAIT(target, TRAIT_RESISTCOLD))
            return;
        await target.apply_status_effect(typeof(datum_status_effect_freon_watcher));
    }
}
