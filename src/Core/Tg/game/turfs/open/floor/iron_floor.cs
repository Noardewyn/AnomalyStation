// Translated from tgstation (snapshot 1d99bd7f): code/game/turfs/open/floor/iron_floor.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only /turf/open/floor/iron's procs translated (M3 P4, the floors subset; its examine is a generated live-partial
// stub, M9). The many iron subtypes of the file hold only vars (the generated part); the ones with procs of their own
// (the textured, smooth and edge variants' rcd and tile code) come with their milestones.

// tg: code/game/turfs/open/floor/iron_floor.dm:1
public partial class turf_open_floor_iron : turf_open_floor
{
    // tg: code/game/turfs/open/floor/iron_floor.dm:6
    public override object broken_states()
    {
        return Dm.list("damaged1", "damaged2", "damaged3", "damaged4", "damaged5");
    }

    // tg: code/game/turfs/open/floor/iron_floor.dm:9
    public override object burnt_states()
    {
        return Dm.list("floorscorched1", "floorscorched2");
    }

    // tg: code/game/turfs/open/floor/iron_floor.dm:13
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("There's a <b>small crack</b> on the edge of it.")));
        return dot;
    }

    // tg: code/game/turfs/open/floor/iron_floor.dm:17
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (Dm.truthy(broken) || Dm.truthy(burnt))
            return await base.update_icon_state(__extra);
        icon_state = (string)base_icon_state;
        return await base.update_icon_state(__extra);
    }
}
