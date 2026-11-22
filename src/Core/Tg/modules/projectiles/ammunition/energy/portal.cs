// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/ammunition/energy/portal.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps GU1, D333): the wormhole projector's blue and orange casings, which pass the projector's weak reference on to
// their beams. The var blocks are generated (D111).

// tg: code/modules/projectiles/ammunition/energy/portal.dm:1
public partial class obj_item_ammo_casing_energy_wormhole : obj_item_ammo_casing_energy
{
    // tg: code/modules/projectiles/ammunition/energy/portal.dm:16
    [DmParams("mapload", "wh")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        obj_item_gun_energy_wormhole_projector wh = Dm.arg(__extra, 0) as obj_item_gun_energy_wormhole_projector;
        object dot = base.Initialize(mapload, __extra);
        gun = WEAKREF(wh) as datum_weakref;
        return dot;
    }

    // tg: code/modules/projectiles/ammunition/energy/portal.dm:20
    public override async DmTask<object> throw_proj(atom target = null, turf targloc = null, mob_living user = null, object @params = null, object spread = null, atom fired_from = null)
    {
        object dot = await base.throw_proj(target, targloc, user, @params, spread, fired_from);
        // tg reads loaded_projectile after ..() has cleared it (_firing.dm:93), so this never runs; the beam got the gun from its casing
        // when newshot() made it (wormhole.dm:19) or from the projector's update_ammo_types
        if (istype(loaded_projectile, typeof(obj_projectile_beam_wormhole)))
        {
            obj_projectile_beam_wormhole WH = loaded_projectile as obj_projectile_beam_wormhole;
            WH.gun = gun;
        }
        return dot;
    }
}
