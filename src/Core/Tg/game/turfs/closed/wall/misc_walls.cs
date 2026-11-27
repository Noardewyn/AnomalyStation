// Translated from tgstation (snapshot 1d99bd7f): code/game/turfs/closed/wall/misc_walls.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The runed metal wall and the rusted wall (Lavaland's cult altar, greed and pride ruins, the fountain hall, the envy ruin
// and the shoe factory). The var blocks are generated.

// tg: code/game/turfs/closed/wall/misc_walls.dm:2
public partial class turf_closed_wall_mineral_cult : turf_closed_wall_mineral
{
    // tg: code/game/turfs/closed/wall/misc_walls.dm:14
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        Dm.@new<obj_effect_temp_visual_cult_turf>(this);
        return base.Initialize(mapload, __extra);
    }

    // tg: code/game/turfs/closed/wall/misc_walls.dm:18
    public override async DmTask devastate_wall()
    {
        Dm.@new(sheet_type, get_turf(this), sheet_amount);
        await DmTask.CompletedTask;
    }
}

// tg: code/game/turfs/closed/wall/misc_walls.dm:45
public partial class turf_closed_wall_rust : turf_closed_wall
{
    // tg: code/game/turfs/closed/wall/misc_walls.dm:49
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_rust)));
        return dot;
    }
}
