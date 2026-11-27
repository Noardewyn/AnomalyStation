// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/lavaland/basilisk/basilisk.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/// Watchers' ground-dwelling cousins, they shoot at you until they get into melee and absorb laser fire to power up.
// tg: code/modules/mob/living/basic/lavaland/basilisk/basilisk.dm:2
public partial class mob_living_basic_mining_basilisk : mob_living_basic_mining
{
    // tg: code/modules/mob/living/basic/lavaland/basilisk/basilisk.dm:30
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ranged_attacks = Waiting.NoSleep(AddComponent(typeof(datum_component_ranged_attacks), Dm.named("projectile_type", typeof(obj_projectile_temp_watcher)), Dm.named("projectile_sound", Dm.resource("sound/items/weapons/pierce.ogg")))) as datum_component_ranged_attacks;
        RegisterSignal(this, COMSIG_MOVABLE_MOVED, nameof(check_lava));
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/basilisk/basilisk.dm:35
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref ranged_attacks);
        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/basic/lavaland/basilisk/basilisk.dm:39
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        object dot = await base.welder_act(user, tool);
        await heat_up(); // Who would do this?
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/basilisk/basilisk.dm:43
    public override async DmTask<object> bullet_act(obj_projectile bullet = null, object def_zone = null, float? piercing_hit = null, float? blocked = null)
    {
        object dot = await base.bullet_act(bullet, def_zone, piercing_hit, blocked);
        if (!Dm.eq(dot, BULLET_ACT_HIT))
            return dot;

        if (bullet is obj_projectile_temp)
        {
            obj_projectile_temp heat_bullet = bullet as obj_projectile_temp;
            if (Dm.num(heat_bullet.temperature) < 0)
                return dot;
            await heat_up();
            return dot;
        }

        if (Dm.num(bullet.damage) == 0 || !Dm.eq(bullet.damage_type, BURN))
            return dot;
        await heat_up();
        return dot;
    }

    /// Are we standing in lava?
    // tg: code/modules/mob/living/basic/lavaland/basilisk/basilisk.dm:60
    [SignalHandler]
    public virtual void check_lava()
    {
        turf_open_lava entered_lava = loc as turf_open_lava;
        if (!islava(loc) || !Dm.eq(entered_lava.immunity_trait, TRAIT_LAVA_IMMUNE))
            return;
        Waiting.NoSleep(heat_up());
    }

    /// We got hit by something hot, go into heat mode
    // tg: code/modules/mob/living/basic/lavaland/basilisk/basilisk.dm:68
    public virtual async DmTask heat_up()
    {
        if (IS_UNCONSCIOUS_OR_CRIT(this) || Dm.truthy(has_status_effect(typeof(datum_status_effect_basilisk_overheat))))
            return;
        await apply_status_effect(typeof(datum_status_effect_basilisk_overheat));
    }

    /// Change what kind of beam we fire
    // tg: code/modules/mob/living/basic/lavaland/basilisk/basilisk.dm:74
    public virtual void set_projectile_type(object projectile_type = null)
    {
        ranged_attacks.projectile_type = projectile_type as Type;
    }
}
