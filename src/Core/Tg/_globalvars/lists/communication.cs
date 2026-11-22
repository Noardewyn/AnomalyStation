// Translated from tgstation (snapshot 1d99bd7f): code/_globalvars/lists/communication.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M10 slice G4: the whole file (CentCom's recall of the emergency shuttle quotes one).

// tg: code/_globalvars/lists/communication.dm:1  GLOBAL_LIST_INIT(admiral_messages, ...)
public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/_globalvars/lists/communication.dm:1
    public DmList admiral_messages;

    // tg: code/_globalvars/lists/communication.dm:1  (GLOBAL_MANAGED(admiral_messages, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobaladmiral_messages()
    {
        admiral_messages = Dm.list(
            "<i>Error: No comment given.</i>",
            "<i>null</i>",
            "Do you know how expensive these stations are?",
            "I was sleeping, thanks a lot.",
            "It's a good day to die!",
            "No.",
            "Stand and fight you cowards!",
            "Stop being paranoid.",
            "Stop wasting my time.",
            "Whatever's broken just build a new one.",
            "You knew the risks coming in.");
        Dm.append(ref gvars_datum_init_order, "admiral_messages");
    }
}
