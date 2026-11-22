// Translated from tgstation (snapshot 1d99bd7f): code/modules/events/mice_migration.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E4 (D327): the whole file: five to fifteen mice (and perhaps the Rat King) come out of the maintenance (SSminor_mapping's
// migration). The var blocks are generated (D111).

// tg: code/modules/events/mice_migration.dm:8
public partial class datum_round_event_mice_migration : datum_round_event
{
    // tg: code/modules/events/mice_migration.dm:12
    public override async DmTask announce__proc(object fake = null)
    {
        object cause = pick("space-winter", "budget-cuts", "Ragnarok",
            "space being cold", "[REDACTED]", "climate change",
            "bad luck");
        object plural = pick("a number of", "a horde of", "a pack of", "a swarm of",
            "a whoop of", Dm.text("not more than []", maximum_mice));
        object name = pick("rodents", "mice", "squeaking things",
            "wire eating mammals", "[REDACTED]", "energy draining parasites");
        object movement = pick("migrated", "swarmed", "stampeded", "descended");
        object location = pick("maintenance tunnels", "maintenance areas",
            "[REDACTED]", "place with all those juicy wires");

        priority_announce(Dm.text("Due to [], [] [] have [] into the [].", cause, plural, name, movement, location), "Migration Alert",
            Dm.resource("sound/mobs/non-humanoids/mouse/mousesqueek.ogg"));
    }

    // tg: code/modules/events/mice_migration.dm:28
    public override async DmTask<object> start()
    {
        await SSminor_mapping.trigger_migration(rand(minimum_mice, maximum_mice));
        return null;
    }
}
