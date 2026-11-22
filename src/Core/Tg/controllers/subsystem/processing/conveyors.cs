// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/processing/conveyors.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The conveyors (the owner, 2026-11-04): the whole file. The var block is generated.

///The conveyor belts' move loops (the convey component)
// tg: code/controllers/subsystem/processing/conveyors.dm:1 MOVEMENT_SUBSYSTEM_DEF(conveyors)
public partial class datum_controller_subsystem_movement_conveyors : datum_controller_subsystem_movement
{
    // The New() that MOVEMENT_SUBSYSTEM_DEF(conveyors) defines (__DEFINES/MC.dm:107-111), with NEW_SS_GLOBAL (__DEFINES/MC.dm:29) expanded inline (it reads src).
    // tg: code/controllers/subsystem/processing/conveyors.dm:1
    public override async DmTask New(params object[] __extra)
    {
        if (SSconveyors != this)
        {
            if (SSconveyors is datum_controller_subsystem_movement_conveyors)
            {
                Recover();
                qdel(SSconveyors);
            }
            SSconveyors = this;
        }
        PreInit();
    }

    // tg: code/controllers/subsystem/processing/conveyors.dm:1 (the macro's fire(), ..() for the profiler)
    public override async DmTask fire(float? resumed = null, params object[] __extra)
    {
        await base.fire(resumed, __extra);
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/processing/conveyors.dm:1
    internal datum_controller_subsystem_movement_conveyors SSconveyors;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_movement_conveyors SSconveyors => ref TgState.Current.SSconveyors;
}
