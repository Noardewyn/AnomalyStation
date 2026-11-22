// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/shield.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Engineering E7 (singularity containment): the whole file. The shield floor a closed loop of field generators lays inside its
// fields (field_generator.dm place_floor): its turf takes no heat while it lies there. The var block is generated (D111).

// tg: code/game/objects/effects/shield.dm:1
public partial class obj_effect_shield : obj_effect
{
    // tg: code/game/objects/effects/shield.dm:11
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        turf location = get_turf(this);
        old_heat_capacity = location.heat_capacity;
        location.heat_capacity = INFINITY;
        return dot;
    }

    // tg: code/game/objects/effects/shield.dm:17
    public override object Destroy(float? force = null)
    {
        turf location = get_turf(this);
        location.heat_capacity = Dm.num(old_heat_capacity);
        return base.Destroy(force);
    }

    // tg: code/game/objects/effects/shield.dm:22
    public override async DmTask<object> singularity_act()
    {
        return null;
    }

    // tg: code/game/objects/effects/shield.dm:25
    public override async DmTask<object> singularity_pull(atom singularity = null, object current_size = null)
    {
        return null;
    }
}
