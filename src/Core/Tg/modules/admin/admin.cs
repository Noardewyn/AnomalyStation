// Translated from tgstation (snapshot 1d99bd7f): code/modules/admin/admin.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only message_admins and relay_msg_admins translated (logging and chat, wave 3: to_chat to GLOB.admins, which
// the Master, the timers and the boot code reach), and the [DmPath] of /datum/admins (the M1 contracts: the type the
// /client var block names, client_defines.dm:43); the admin panels, verbs and tools of admin.dm come with the admin
// tools (M10).

public static partial class GlobalProcs
{
    ////////////////////////////////
    // tg: code/modules/admin/admin.dm:2
    public static void message_admins(object msg = null)
    {
        msg = Dm.text("<span class=\"admin\"><span class=\"prefix\">ADMIN LOG:</span> <span class=\"message\">[]</span></span>", msg);
        to_chat(GLOB.admins,
            type: MESSAGE_TYPE_ADMINLOG,
            html: msg,
            confidential: TRUE);
    }

    // tg: code/modules/admin/admin.dm:9
    public static void relay_msg_admins(object msg = null)
    {
        msg = Dm.text("<span class=\"admin\"><span class=\"prefix\">RELAY:</span> <span class=\"message\">[]</span></span>", msg);
        to_chat(GLOB.admins,
            type: MESSAGE_TYPE_ADMINLOG,
            html: msg,
            confidential: TRUE);
    }
}

// tg: code/modules/admin/admin.dm:18 (the earliest .dme file that declares /datum/admins, rules 2.3)
[DmPath("/datum/admins")]
public partial class datum_admins : datum
{
}
