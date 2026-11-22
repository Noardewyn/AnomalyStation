// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/projectile/special/wormhole.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps GU1, D333): the wormhole projector's blue and orange beams, which open a portal where they hit. The var
// blocks are generated (D111).

// tg: code/modules/projectiles/projectile/special/wormhole.dm:1
public partial class obj_projectile_beam_wormhole : obj_projectile_beam
{
    // tg: code/modules/projectiles/projectile/special/wormhole.dm:19
    [DmParams("mapload", "casing")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        obj_item_ammo_casing_energy_wormhole casing = Dm.arg(__extra, 0) as obj_item_ammo_casing_energy_wormhole;
        object dot = base.Initialize(mapload, __extra);
        if (casing != null)
            gun = casing.gun;
        return dot;
    }

    // tg: code/modules/projectiles/projectile/special/wormhole.dm:25
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        blocked ??= 0;
        obj_item_gun_energy_wormhole_projector projector = gun.resolve() as obj_item_gun_energy_wormhole_projector;
        if (projector == null)
        {
            qdel(this);
            return BULLET_ACT_BLOCK;
        }

        object dot = await base.on_hit(target, blocked, pierce_hit);
        projector.create_portal(this, get_turf(this));
        return dot;
    }
}
