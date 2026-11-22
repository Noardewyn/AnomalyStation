// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/basic_mobs/targeting_strategies/trough_with_ore.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Accepts containers (e.g. troughs) that hold at least one ore item.
// tg: code/datums/ai/basic_mobs/targeting_strategies/trough_with_ore.dm:3
public partial class datum_targeting_strategy_trough_with_ore : datum_targeting_strategy
{
    // tg: code/datums/ai/basic_mobs/targeting_strategies/trough_with_ore.dm:5
    public override async DmTask<object> is_valid_target(mob_living living_mob = null, atom target = null, object vision_range = null, datum_ai_controller controller = null)
    {
        object dot = await base.is_valid_target(living_mob, target, vision_range, controller);
        if (!Dm.truthy(dot))
            return FALSE;
        if (!ismovable(target))
            return FALSE;
        foreach (obj_item_stack_ore ore in Dm.each<obj_item_stack_ore>(target.contents))
            return TRUE;
        return FALSE;
    }
}
