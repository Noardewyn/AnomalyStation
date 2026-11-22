// Translated from tgstation (snapshot 1d99bd7f): code/modules/mapfluff/ruins/spaceruin_code/TheDerelict.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M7, the power monitor): only the examine the derelict ruin gives /obj/machinery/computer/monitor (:63). The rest of the
// file (the derelict's vault controller and its doors) is the ruin's (space ruins, after the core version) and stays inert.

// tg: code/modules/power/monitor.dm:4
public partial class obj_machinery_computer_monitor
{
    // tg: code/modules/mapfluff/ruins/spaceruin_code/TheDerelict.dm:63
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("It appears to be powered via a cable connector.")));
        return dot;
    }
}
