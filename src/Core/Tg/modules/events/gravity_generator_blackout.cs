// Translated from tgstation (snapshot 1d99bd7f): code/modules/events/gravity_generator_blackout.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E1b (D327): the whole file, the gravity generator blackout. The var blocks are generated (D111).

// tg: code/modules/events/gravity_generator_blackout.dm:1
public partial class datum_round_event_control_gravity_generator_blackout : datum_round_event_control
{
    // tg: code/modules/events/gravity_generator_blackout.dm:10
    public override object can_spawn_event(object players_amt = null, float? allow_magic = null)
    {
        object dot = base.can_spawn_event(players_amt, allow_magic);
        if (!Dm.truthy(dot))
            return dot;

        bool station_generator_exists = false;
        foreach (obj_machinery_gravity_generator_main the_generator in Dm.each<obj_machinery_gravity_generator_main>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_gravity_generator_main))))
        {
            if (Dm.truthy(is_station_level(the_generator.z)))
                station_generator_exists = true;
        }

        if (!station_generator_exists)
            return FALSE;
        return dot;
    }
}

// tg: code/modules/events/gravity_generator_blackout.dm:23
public partial class datum_round_event_gravity_generator_blackout : datum_round_event
{
    // tg: code/modules/events/gravity_generator_blackout.dm:28
    public override async DmTask announce__proc(object fake = null)
    {
        priority_announce(Dm.text("Gravnospheric anomalies detected near []. Manual reset of generators is required.", station_name()), "Anomaly Alert", ANNOUNCER_GRANOMALIES);
    }

    // tg: code/modules/events/gravity_generator_blackout.dm:31
    public override async DmTask<object> start()
    {
        foreach (obj_machinery_gravity_generator_main the_generator in Dm.each<obj_machinery_gravity_generator_main>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_gravity_generator_main))))
        {
            if (Dm.truthy(is_station_level(the_generator.z)))
                await the_generator.blackout();
        }
        return null;
    }
}
