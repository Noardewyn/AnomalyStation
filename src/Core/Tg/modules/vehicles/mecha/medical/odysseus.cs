// Translated from tgstation (snapshot 1d99bd7f): code/modules/vehicles/mecha/medical/odysseus.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/modules/vehicles/mecha/medical/odysseus.dm:1
public partial class obj_vehicle_sealed_mecha_odysseus : obj_vehicle_sealed_mecha
{
    // tg: code/modules/vehicles/mecha/medical/odysseus.dm:15
    public override async DmTask<object> moved_inside(mob_living newoccupant = null)
    {
        object dot = await base.moved_inside(newoccupant);
        if (!Dm.truthy(dot))
            return dot;
        ADD_TRAIT(newoccupant, TRAIT_MEDICAL_HUD, VEHICLE_TRAIT);
        return dot;
    }

    // tg: code/modules/vehicles/mecha/medical/odysseus.dm:21
    public override async DmTask<object> remove_occupant(mob M = null)
    {
        REMOVE_TRAIT(M, TRAIT_MEDICAL_HUD, VEHICLE_TRAIT);
        return await base.remove_occupant(M);
    }

    // mmi_moved_inside (odysseus.dm:25) comes with the MMI: see docs/ai/specs/not-translated.md
}
