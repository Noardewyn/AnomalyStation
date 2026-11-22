// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/processing/digital_clock.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps DC1 (D333, the station's wall clocks): the whole file. The var block is generated (D111).

/// The subsystem used to tick digital clocks
// tg: code/controllers/subsystem/processing/digital_clock.dm:2 PROCESSING_SUBSYSTEM_DEF(digital_clock)
public partial class datum_controller_subsystem_processing_digital_clock : datum_controller_subsystem_processing
{
    // The New() that PROCESSING_SUBSYSTEM_DEF(digital_clock) defines (__DEFINES/MC.dm:116-119), with NEW_SS_GLOBAL(SSdigital_clock)
    // (__DEFINES/MC.dm:29) expanded inline (it reads src).
    // tg: code/controllers/subsystem/processing/digital_clock.dm:2
    public override async DmTask New(params object[] __extra)
    {
        if (SSdigital_clock != this)
        {
            if (SSdigital_clock is datum_controller_subsystem_processing_digital_clock)
            {
                Recover();
                qdel(SSdigital_clock);
            }
            SSdigital_clock = this;
        }
        PreInit();
    }

    // The fire() that PROCESSING_SUBSYSTEM_DEF(digital_clock) defines (__DEFINES/MC.dm:120): ..() /*just so it shows up on the profiler*/
    // tg: code/controllers/subsystem/processing/digital_clock.dm:2
    [DmParams()]
    public override async DmTask fire(float? resumed = null, params object[] __extra)
    {
        await base.fire(resumed, __extra); /*just so it shows up on the profiler*/
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/processing/digital_clock.dm:2  PROCESSING_SUBSYSTEM_DEF(digital_clock): GLOBAL_REAL(SSdigital_clock, /datum/controller/subsystem/processing/digital_clock)
    internal datum_controller_subsystem_processing_digital_clock SSdigital_clock;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_processing_digital_clock SSdigital_clock => ref TgState.Current.SSdigital_clock;
}
