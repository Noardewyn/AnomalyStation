// Translated from tgstation (snapshot 1d99bd7f): code/modules/shuttle/mobile_port/variants/infiltrator.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate N1b, D316). The var block is generated (D111).

/// The syndicate infiltrator shuttle port.
// tg: code/modules/shuttle/mobile_port/variants/infiltrator.dm:2
public partial class obj_docking_port_mobile_infiltrator : obj_docking_port_mobile
{
    // tg: code/modules/shuttle/mobile_port/variants/infiltrator.dm:10
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(SSpoints_of_interest.make_point_of_interest(this));
        return dot;
    }

    // tg: code/modules/shuttle/mobile_port/variants/infiltrator.dm:14
    public override object Destroy(float? force = null)
    {
        SSpoints_of_interest.remove_point_of_interest(this);
        return base.Destroy(force);
    }
}
