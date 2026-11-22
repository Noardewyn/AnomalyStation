// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/gigabeacon.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (2026-10-18, the owner: things tg hides under the floor showed on the station's floors): the gigabeacon's
// invisible tracking beacon and the undertile element. The var block is generated.

// tg: code/game/machinery/gigabeacon.dm:1
public partial class obj_machinery_bluespace_beacon : obj_machinery
{
    // tg: code/game/machinery/gigabeacon.dm:12
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        turf T = loc as turf;
        Beacon = Dm.@new<obj_item_beacon>(T);
        Beacon.SetInvisibility(INVISIBILITY_MAXIMUM);

        Waiting.NoSleep(AddElement(typeof(datum_element_undertile), TRAIT_T_RAY_VISIBLE));
        return dot;
    }

    // tg: code/game/machinery/gigabeacon.dm:20
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref Beacon);
        return base.Destroy(force);
    }

    // tg: code/game/machinery/gigabeacon.dm:24
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (QDELETED(Beacon)) //Don't move it out of nullspace BACK INTO THE GAME for the love of god
        {
            turf T = loc as turf;
            Beacon = Dm.@new<obj_item_beacon>(T);
            Beacon.SetInvisibility(INVISIBILITY_MAXIMUM);
        }
        else if (Beacon.loc != loc)
            Waiting.NoSleep(Beacon.forceMove(loc));
        return null;
    }
}
