// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/doors/shutters.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M3 P3 (D117: shutters keep their mapped state). The var blocks are generated (D111).

// tg: code/game/machinery/doors/shutters.dm:1
public partial class obj_machinery_door_poddoor_shutters : obj_machinery_door_poddoor
{
    // tg: code/game/machinery/doors/shutters.dm:16
    public override object animation_length(object animation = null)
    {
        object sw__1 = animation;
        if (Dm.eq(sw__1, DOOR_OPENING_ANIMATION))
            return 1.388f * SECONDS;
        else if (Dm.eq(sw__1, DOOR_CLOSING_ANIMATION))
            return 1.388f * SECONDS;
        return null;
    }

    // tg: code/game/machinery/doors/shutters.dm:23
    public override object animation_segment_delay(object animation = null)
    {
        object sw__1 = animation;
        if (Dm.eq(sw__1, DOOR_OPENING_PASSABLE))
            return 0.76f * SECONDS;
        else if (Dm.eq(sw__1, DOOR_OPENING_FINISHED))
            return 1.388f * SECONDS;
        else if (Dm.eq(sw__1, DOOR_CLOSING_UNPASSABLE))
            return 0.152f * SECONDS;
        else if (Dm.eq(sw__1, DOOR_CLOSING_FINISHED))
            return 1.388f * SECONDS;
        return null;
    }
}
