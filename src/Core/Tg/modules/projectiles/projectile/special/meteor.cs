// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/projectile/special/meteor.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps GU1, D333): the meteor gun's meteor, which blasts a living mob it hits, smashes what it bumps with the
// explosions' medium queue and shakes the onlookers' screens; punched or hit back, it turns away from its deflector
// (component/meteor_combat). The var block is generated (D111).

// tg: code/modules/projectiles/projectile/special/meteor.dm:1
public partial class obj_projectile_meteor : obj_projectile
{
    // tg: code/modules/projectiles/projectile/special/meteor.dm:14
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_meteor_combat), Dm.@new<datum_callback>(this, nameof(deflect))));
        return dot;
    }

    // tg: code/modules/projectiles/projectile/special/meteor.dm:18
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        blocked ??= 0;
        object dot = await base.on_hit(target, blocked, pierce_hit);
        if (Dm.eq(dot, BULLET_ACT_HIT) && isliving(target))
        {
            await explosion(target, devastation_range: -1, light_impact_range: 2, flame_range: 0, flash_range: 1, adminlog: FALSE);
            playsound(target.loc, Dm.resource("sound/effects/meteorimpact.ogg"), 40, TRUE);
        }
        return dot;
    }

    // tg: code/modules/projectiles/projectile/special/meteor.dm:24
    public override async DmTask<object> Bump(atom bumped_atom = null)
    {
        atom hit_target = bumped_atom;
        if (hit_target == firer)
        {
            await forceMove(hit_target.loc);
            return null;
        }
        if (isobj(hit_target))
            Dm.append(ref SSexplosions.med_mov_atom, hit_target);
        if (isturf(hit_target))
            Dm.append(ref SSexplosions.medturf, hit_target);
        playsound(loc, Dm.resource("sound/effects/meteorimpact.ogg"), 40, TRUE);
        foreach (mob onlookers_in_range in Dm.each<mob>(urange(10, this)))
        {
            if (!IS_UNCONSCIOUS_OR_CRIT(onlookers_in_range))
                shake_camera(onlookers_in_range, 3, 1);
        }
        qdel(this);
        return null;
    }

    ///Called by component/meteor_combat to direct us away from whatever has punched us.
    // tg: code/modules/projectiles/projectile/special/meteor.dm:39
    public virtual void deflect(mob user = null)
    {
        firer = user;
        set_angle(Dm.num(get_angle(user, this)) + rand(-45, 45));
    }
}
