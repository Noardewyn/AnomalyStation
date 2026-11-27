// Translated from tgstation (snapshot 1d99bd7f): code/modules/explorer_drone/exploration_events/fluff.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: a line in the drone's log from the site's fluff list (tg's strings/exodrone.json). The var block is generated (D111).

/// Just a message in the log nothing more
// tg: code/modules/explorer_drone/exploration_events/fluff.dm:2
public partial class datum_exploration_event_fluff : datum_exploration_event
{
    // tg: code/modules/explorer_drone/exploration_events/fluff.dm:5
    public override object get_discovery_message(obj_item_exodrone drone = null)
    {
        return pick_list(EXODRONE_FILE, drone.location.fluff_type);
    }
}
