// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/basic_mobs/targeting_strategies/accessible_cable.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Accepts visible cables sitting on accessible open floor tiles.
// tg: code/datums/ai/basic_mobs/targeting_strategies/accessible_cable.dm:3
public partial class datum_targeting_strategy_accessible_cable : datum_targeting_strategy
{
    // tg: code/datums/ai/basic_mobs/targeting_strategies/accessible_cable.dm:5
    public override async DmTask<object> is_valid_target(mob_living living_mob = null, atom target = null, object vision_range = null, datum_ai_controller controller = null)
    {
        object dot = await base.is_valid_target(living_mob, target, vision_range, controller);
        if (!Dm.truthy(dot))
            return FALSE;
        if (target is not obj_structure_cable candidate || !Dm.truthy(can_see(living_mob, candidate, vision_range)))
            return FALSE;
        if (get_turf(candidate) is not turf_open_floor below_the_cable)
            return FALSE;
        return Dm.num(Dm.num(below_the_cable.underfloor_accessibility) >= UNDERFLOOR_INTERACTABLE);
    }
}
