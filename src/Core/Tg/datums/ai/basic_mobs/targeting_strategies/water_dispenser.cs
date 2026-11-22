// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/basic_mobs/targeting_strategies/water_dispenser.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Accepts sinks or reagent dispensers that contain water and are visible to the pawn.
// tg: code/datums/ai/basic_mobs/targeting_strategies/water_dispenser.dm:3
public partial class datum_targeting_strategy_water_dispenser : datum_targeting_strategy
{
    // tg: code/datums/ai/basic_mobs/targeting_strategies/water_dispenser.dm:5
    public override async DmTask<object> is_valid_target(mob_living living_mob = null, atom target = null, object vision_range = null, datum_ai_controller controller = null)
    {
        object dot = await base.is_valid_target(living_mob, target, vision_range, controller);
        if (!Dm.truthy(dot))
            return FALSE;
        if (target is not obj_structure_sink && target is not obj_structure_reagent_dispensers)
            return FALSE;
        if (target.reagents == null)
            return FALSE;
        bool has_water = false;
        foreach (datum_reagent_water water in Dm.each<datum_reagent_water>(((datum_reagents)target.reagents).reagent_list))
        {
            has_water = true;
            break;
        }
        if (!has_water)
            return FALSE;
        return can_see(living_mob, target, vision_range);
    }
}
