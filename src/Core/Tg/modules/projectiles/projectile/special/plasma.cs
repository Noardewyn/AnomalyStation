// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/projectile/special/plasma.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps GU1, D333): the plasma cutters' blast drills the rock it hits and flies on through more of it (less through
// harder rock or rock without ore); the advanced, the mech's and the turret's blasts declare no procs. The var blocks are
// generated (D111).

// tg: code/modules/projectiles/projectile/special/plasma.dm:1
public partial class obj_projectile_plasma : obj_projectile
{
    // tg: code/modules/projectiles/projectile/special/plasma.dm:16
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        blocked ??= 0;
        object dot = await base.on_hit(target, blocked, pierce_hit);
        if (!ismineralturf(target))
            return dot;
        turf_closed_mineral rock = target as turf_closed_mineral;
        if (Dm.truthy(mine_range))
        {
            mine_range -= 1;
            // Harder rocks give less extra range
            range = Dm.num(range) + Dm.initial(typeof(turf_closed_mineral), (turf_closed_mineral p) => p.tool_mine_speed) / rock.tool_mine_speed * (rock.mineral_type == null ? 0.5f : 1);
        }
        await rock.gets_drilled(firer as mob);
        if (Dm.num(range) > 0)
            return BULLET_ACT_FORCE_PIERCE;
        return dot;
    }
}
