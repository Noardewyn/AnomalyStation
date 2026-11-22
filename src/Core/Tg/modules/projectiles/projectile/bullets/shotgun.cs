// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/projectile/bullets/shotgun.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7x slice D: the rubber shot pellet's ricochet that wears off with the distance; gaps GU1 (D333): the frag-12 slug's blast. The slug,
// beanbag, buckshot, stun and other projectiles only set vars (the generated var blocks, D111).

// tg: code/modules/projectiles/projectile/bullets/shotgun.dm:73
public partial class obj_projectile_bullet_shotgun_frag12 : obj_projectile_bullet
{
    // tg: code/modules/projectiles/projectile/bullets/shotgun.dm:79
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        blocked ??= 0;
        await base.on_hit(target, blocked, pierce_hit);
        await explosion(target, devastation_range: -1, light_impact_range: 1, explosion_cause: this);
        return BULLET_ACT_HIT;
    }
}

// tg: code/modules/projectiles/projectile/bullets/shotgun.dm:119
public partial class obj_projectile_bullet_pellet_shotgun_rubbershot : obj_projectile_bullet_pellet
{
    // tg: code/modules/projectiles/projectile/bullets/shotgun.dm:137
    public override async DmTask<object> reduce_range()
    {
        if (ricochet_chance > 0)
            ricochet_chance -= tile_dropoff_ricochet;
        return await base.reduce_range();
    }
}
