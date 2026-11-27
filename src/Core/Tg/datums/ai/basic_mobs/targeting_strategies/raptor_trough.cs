// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/basic_mobs/targeting_strategies/raptor_trough.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;


/// Looks for filled troughs raptors can eat from
// tg: code/datums/ai/basic_mobs/targeting_strategies/raptor_trough.dm:2
public partial class datum_targeting_strategy_raptor_trough : datum_targeting_strategy
{
    // tg: code/datums/ai/basic_mobs/targeting_strategies/raptor_trough.dm:4
    public override async DmTask<object> is_valid_target(mob_living living_mob = null, atom target = null, object vision_range = null, datum_ai_controller controller = null)
    {
        object dot = await base.is_valid_target(living_mob, target, vision_range, controller);
        if (!Dm.truthy(dot))
            return FALSE;
        atom trough = target;
        if (!Dm.truthy(can_see(living_mob, trough, vision_range)))
            return FALSE;
        return Dm.locate<obj_item_stack_ore>(trough.contents) != null ? TRUE : FALSE;
    }
}
