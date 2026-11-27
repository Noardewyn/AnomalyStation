// Translated from tgstation (snapshot 1d99bd7f): code/modules/explorer_drone/exploration_events/adventure.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the event that starts a site's adventure. The var block is generated (D111).

/// Adventure wrapper event
// tg: code/modules/explorer_drone/exploration_events/adventure.dm:2
public partial class datum_exploration_event_adventure : datum_exploration_event
{
    // tg: code/modules/explorer_drone/exploration_events/adventure.dm:7
    public override void encounter(obj_item_exodrone drone = null)
    {
        base.encounter(drone);
        drone.start_adventure(adventure);
    }
}
