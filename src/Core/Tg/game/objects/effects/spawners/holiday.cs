// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/spawners/holiday.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M3 I1d, the map-preview spawners): only the procs that kept Meta's spawners inert, so they spawn their loot and
// delete themselves as tg's do (tg: "If you see this while in a shift, please create a bug report"). The var blocks are generated.

// tg: code/game/objects/effects/spawners/holiday.dm:2
public partial class obj_effect_spawner_holiday : obj_effect_spawner
{
    // tg: code/game/objects/effects/spawners/holiday.dm:11
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        float found_holiday = FALSE;
        foreach (object holiday in Dm.each(holidays_to_spawn))
        {
            if (Dm.truthy(check_holidays(holiday)))
            {
                found_holiday = TRUE;
                DmList spawn_list = holidays_to_spawn[holiday] as DmList;
                foreach (object path in Dm.each(spawn_list))
                    Dm.@new((Type)path, loc);
            }
        }
        if (!Dm.truthy(found_holiday) && Dm.truthy(non_holiday_spawn))
            Dm.@new((Type)non_holiday_spawn, loc);
        return dot;
    }
}
