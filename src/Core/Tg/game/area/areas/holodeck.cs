// Translated from tgstation (snapshot 1d99bd7f): code/game/area/areas/holodeck.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the holodeck's area, which draws its power through its control console's area. The var blocks are generated (D111).

/*
	Power tracking: Use the holodeck computer's power grid
	Asserts are to avoid the inevitable infinite loops
*/
// tg: code/game/area/areas/holodeck.dm:1
public partial class area_station_holodeck : area_station
{
    // tg: code/game/area/areas/holodeck.dm:18
    public override object powered(object chan = null)
    {
        if (!Dm.truthy(requires_power))
            return TRUE;
        if (Dm.truthy(always_unpowered))
            return FALSE;
        if (linked == null)
            return FALSE;
        var A = get_area(linked) as area;
        if (istype(A, typeof(area_station_holodeck)))
            throw CRASH(assertion_message("code/game/area/areas/holodeck.dm", 25, "!istype(A, /area/station/holodeck)"), "code/game/area/areas/holodeck.dm", 25); // ASSERT, expanded inline (rules 11.1)
        return A.powered(chan);
    }

    // tg: code/game/area/areas/holodeck.dm:29
    public override void addStaticPower(object value = null, object powerchannel = null)
    {
        if (linked == null)
            return;
        var A = get_area(linked) as area;
        if (istype(A, typeof(area_station_holodeck)))
            throw CRASH(assertion_message("code/game/area/areas/holodeck.dm", 33, "!istype(A, /area/station/holodeck)"), "code/game/area/areas/holodeck.dm", 33); // ASSERT, expanded inline (rules 11.1)
        base.addStaticPower(value, powerchannel);
    }

    // tg: code/game/area/areas/holodeck.dm:36
    public override object use_energy(object amount = null, object chan = null)
    {
        if (linked == null)
            return FALSE;
        var A = get_area(linked) as area;
        if (istype(A, typeof(area_station_holodeck)))
            throw CRASH(assertion_message("code/game/area/areas/holodeck.dm", 41, "!istype(A, /area/station/holodeck)"), "code/game/area/areas/holodeck.dm", 41); // ASSERT, expanded inline (rules 11.1)
        return base.use_energy(amount, chan);
    }
}
