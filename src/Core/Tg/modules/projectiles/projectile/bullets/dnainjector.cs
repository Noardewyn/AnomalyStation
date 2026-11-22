// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/projectile/bullets/dnainjector.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate G4a, D316: the DNA injector gun's dart). The var block is generated (D111).

// tg: code/modules/projectiles/projectile/bullets/dnainjector.dm:1
public partial class obj_projectile_bullet_dnainjector : obj_projectile_bullet
{
    // tg: code/modules/projectiles/projectile/bullets/dnainjector.dm:10
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        blocked ??= 0;
        if (iscarbon(target))
        {
            mob_living_carbon M = (mob_living_carbon)target;
            if (blocked != 100)
            {
                if (Dm.truthy(M.can_inject(target_zone: def_zone)))
                {
                    if (Dm.truthy(await injector.inject(M, firer as mob)))
                    {
                        QDEL_NULL(ref injector);
                        return BULLET_ACT_HIT;
                    }
                }
                else
                {
                    blocked = 100;
                    target.visible_message(span_danger(I18n.T("\\The [] is deflected!", this)),
                        span_userdanger(I18n.T("You are protected against \\the []!", this)));
                }
            }
        }
        return await base.on_hit(target, blocked, pierce_hit);
    }

    // tg: code/modules/projectiles/projectile/bullets/dnainjector.dm:24
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref injector);
        return base.Destroy(force);
    }
}
