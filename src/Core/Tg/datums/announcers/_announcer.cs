// Translated from tgstation (snapshot 1d99bd7f): code/datums/announcers/_announcer.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M9 slice D: the announcements' sounds). The announcers themselves (default, intern, medbot) are var blocks
// (generated, D111).

///Data holder for the announcers that can be used in a game, this can be used to have alternative announcements outside of the default e.g.the intern
// tg: code/datums/announcers/_announcer.dm:2
public partial class datum_centcom_announcer : datum
{
    // tg: code/datums/announcers/_announcer.dm:15
    public virtual object get_rand_welcome_sound()
    {
        return Dm.pick(welcome_sounds);
    }

    // tg: code/datums/announcers/_announcer.dm:19
    public virtual object get_rand_alert_sound()
    {
        return Dm.pick(alert_sounds);
    }

    // tg: code/datums/announcers/_announcer.dm:22
    public virtual object get_rand_report_sound()
    {
        return Dm.pick(command_report_sounds);
    }
}
