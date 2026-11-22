// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/movement/hyperspace_drift.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

///This subsystem handles the hyperspace shuttle pull movement loops
// tg: code/controllers/subsystem/movement/hyperspace_drift.dm:2 MOVEMENT_SUBSYSTEM_DEF(hyperspace_drift)
public partial class datum_controller_subsystem_movement_hyperspace_drift : datum_controller_subsystem_movement
{
    public override async DmTask New(params object[] __extra)
    {
        if (SShyperspace_drift != this)
        {
            if (SShyperspace_drift is datum_controller_subsystem_movement_hyperspace_drift)
            {
                Recover();
                qdel(SShyperspace_drift);
            }
            SShyperspace_drift = this;
        }
        PreInit();
    }
}

public sealed partial class TgState
{
    internal datum_controller_subsystem_movement_hyperspace_drift SShyperspace_drift;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_movement_hyperspace_drift SShyperspace_drift => ref TgState.Current.SShyperspace_drift;
}
