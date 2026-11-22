// Translated from tgstation (snapshot 1d99bd7f): code/modules/events/grid_check.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E1b (D327): the whole file, the grid check (power_fail: the station's APCs go dark for one to four minutes, or until rebooted).
// The var blocks are generated (D111).

// tg: code/modules/events/grid_check.dm:14
public partial class datum_round_event_grid_check : datum_round_event
{
    // tg: code/modules/events/grid_check.dm:18
    public override async DmTask announce__proc(object fake = null)
    {
        var controller = control as datum_round_event_control_grid_check;
        if (!Dm.truthy(fake))
        {
            if (controller == null)
                throw Dm.CRASH("event started without controller!", "code/modules/events/grid_check.dm", 22);
            if (!(controller.announcement_spam_protection <= world.time)) // COOLDOWN_FINISHED
                return;
        }
        priority_announce(Dm.text("Abnormal activity detected in []'s powernet. As a precautionary measure, the station's power will be shut off for an indeterminate duration.", station_name()), "Critical Power Failure", ANNOUNCER_POWEROFF);
        if (!Dm.truthy(fake)) // Only start the CD if we're real
            controller.announcement_spam_protection = world.time + 30 * SECONDS; // COOLDOWN_START
    }

    // tg: code/modules/events/grid_check.dm:29
    public override async DmTask<object> start()
    {
        power_fail(60, 240); // 1 to 4 minutes
        return null;
    }
}
