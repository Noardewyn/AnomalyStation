// Translated from tgstation (snapshot 1d99bd7f): code/modules/events/grey_tide.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E1b (D327): the whole file, the grey tide (one to three departments: their lights flicker off as it builds, then their airlocks
// bolt open, their brig timers release, their secure lockers unlock and their APCs lose the lights). The var blocks are generated (D111).

// tg: code/modules/events/grey_tide.dm:11
public partial class datum_round_event_grey_tide : datum_round_event
{
    // tg: code/modules/events/grey_tide.dm:19
    public override async DmTask<object> setup()
    {
        announce_when = rand(50, 60);
        end_when = rand(20, 30);
        severity = rand(1, 3);

        DmList potential_areas = Dm.list(typeof(area_station_command),
            typeof(area_station_engineering),
            typeof(area_station_medical),
            typeof(area_station_security),
            typeof(area_station_cargo),
            typeof(area_station_science));

        for (float i = 1; i <= severity; i++)
            Dm.append(ref grey_tide_areas, pick_n_take(potential_areas));
        return null;
    }

    // tg: code/modules/events/grey_tide.dm:35
    public override async DmTask announce__proc(object fake = null)
    {
        if (Dm.truthy(fake))
            severity = rand(1, 3);
        priority_announce(Dm.text("Gr3y.T1d3 virus detected in [] secure locking encryption subroutines. Severity level of []. Recommend station AI involvement.", station_name(), severity), "Security Alert");
    }

    // tg: code/modules/events/grey_tide.dm:40
    public override async DmTask<object> start()
    {
        if (!Dm.truthy(length(grey_tide_areas)))
        {
            stack_trace("Could not initiate grey-tide. No areas in the list!", "code/modules/events/grey_tide.dm", 42);
            kill();
        }
        return null;
    }

    // tg: code/modules/events/grey_tide.dm:45
    public override async DmTask tick()
    {
        if (!(activeFor % 12 == 0)) // ISMULTIPLE(activeFor, 12)
            return;

        // SEND_GLOBAL_SIGNAL(COMSIG_GLOB_GREY_TIDE_LIGHT, grey_tide_areas), expanded inline (rules 11.1 (e))
        if (Dm.truthy(SSdcs._listen_lookup?[COMSIG_GLOB_GREY_TIDE_LIGHT]))
            SSdcs._SendSignal(COMSIG_GLOB_GREY_TIDE_LIGHT, Dm.list(SSdcs, grey_tide_areas));
    }

    // Objects currently impacted by the greytide event:
    // /obj/machinery/door/airlock -- Signal bolts open the door
    // /obj/machinery/status_display/door_timer -- Signal instantly ends the timer, releasing the occupant
    // /obj/structure/closet/secure_closet -- Signal unlocks locked lockers
    // /obj/machinery/power/apc -- Signal turns the lighting channel off

    // tg: code/modules/events/grey_tide.dm:57
    public override async DmTask end()
    {
        // SEND_GLOBAL_SIGNAL(COMSIG_GLOB_GREY_TIDE, grey_tide_areas), expanded inline (rules 11.1 (e))
        if (Dm.truthy(SSdcs._listen_lookup?[COMSIG_GLOB_GREY_TIDE]))
            SSdcs._SendSignal(COMSIG_GLOB_GREY_TIDE, Dm.list(SSdcs, grey_tide_areas));
    }
}
