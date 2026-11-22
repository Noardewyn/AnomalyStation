// Translated from tgstation (snapshot 1d99bd7f): code/modules/events/sandstorm.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E2b (D327): the whole file but the admins' side picker (/datum/event_admin_setup, as the other events'): the directional
// sandstorm (sand and dust grinding one side of the station after a warning naming the side) and the admins' classic sandstorm. The
// var blocks are generated (D111).

/**
 * Sandstorm Event: Throws dust/sand at one side of the station. High-intensity and relatively short,
 * however the incoming direction is given along with time to prepare. Damages can be reduced or
 * mitigated with a few people actively working to fix things as the storm hits, but leaving the event to run on its own can lead to widespread breaches.
 *
 * Meant to be encountered mid-round, with enough spare manpower among the crew to properly respond.
 * Anyone with a welder or metal can contribute.
 */
// tg: code/modules/events/sandstorm.dm:23
public partial class datum_round_event_sandstorm : datum_round_event
{
    // tg: code/modules/events/sandstorm.dm:30
    public override async DmTask<object> setup()
    {
        start_when = rand(70, 90);
        end_when = rand(110, 140);
        return null;
    }

    // tg: code/modules/events/sandstorm.dm:34
    public override async DmTask announce__proc(object fake = null)
    {
        if (!Dm.truthy(start_side))
            start_side = pick(GLOB.cardinals);

        string start_side_text = "unknown";
        float side = Dm.num(start_side);
        if (side == NORTH)
            start_side_text = "fore";
        else if (side == SOUTH)
            start_side_text = "aft";
        else if (side == EAST)
            start_side_text = "starboard";
        else if (side == WEST)
            start_side_text = "port";
        else
        {
            stack_trace(Dm.text("Sandstorm event given [] as unrecognized direction. Cancelling event...", start_side), "code/modules/events/sandstorm.dm", 50);
            kill();
            return;
        }

        priority_announce(Dm.text("A large wave of space dust is approaching from the [] side of the station. Impact is expected in the next two minutes. All employees are encouraged to assist in repairs and damage mitigation if possible.", start_side_text),
            "Collision Emergency Alert");
    }

    // tg: code/modules/events/sandstorm.dm:57
    public override async DmTask tick()
    {
        spawn_meteors(15, GLOB.meteors_sandstorm, start_side);
    }
}

/**
 * The original sandstorm event. An admin-only disasterfest that sands down all sides of the station
 * Uses space dust, meaning walls/rwalls are quickly chewed up very quickly.
 *
 * Super dangerous, super funny, preserved for future admin use in case the new event reminds
 * them that this exists. It is unchanged from its original form and is arguably perfect.
 */
// tg: code/modules/events/sandstorm.dm:79
public partial class datum_round_event_sandstorm_classic : datum_round_event
{
    // tg: code/modules/events/sandstorm.dm:85
    public override async DmTask tick()
    {
        spawn_meteors(10, GLOB.meteors_dust);
    }
}
