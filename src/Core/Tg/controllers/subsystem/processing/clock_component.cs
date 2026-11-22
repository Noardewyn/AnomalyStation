// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/processing/clock_component.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps CI1, D339, D341): the subsystem that ticks the circuits' clock components. The var block is generated (D111).

/// The subsystem used to tick [/datum/component/acid] instances.
// tg: code/controllers/subsystem/processing/clock_component.dm:2 PROCESSING_SUBSYSTEM_DEF(clock_component) (the var block is generated)
public partial class datum_controller_subsystem_processing_clock_component : datum_controller_subsystem_processing
{
    // The New() that PROCESSING_SUBSYSTEM_DEF(clock_component) defines (__DEFINES/MC.dm:116-119), with NEW_SS_GLOBAL(SSclock_component)
    // (__DEFINES/MC.dm:29) expanded inline (it reads src).
    // tg: code/controllers/subsystem/processing/clock_component.dm:2
    public override async DmTask New(params object[] __extra)
    {
        if (SSclock_component != this)
        {
            if (SSclock_component is datum_controller_subsystem_processing_clock_component)
            {
                Recover();
                qdel(SSclock_component);
            }
            SSclock_component = this;
        }
        PreInit();
    }

    // The fire() that PROCESSING_SUBSYSTEM_DEF(clock_component) defines (__DEFINES/MC.dm:120): ..() /*just so it shows up on the profiler*/
    // tg: code/controllers/subsystem/processing/clock_component.dm:2
    [DmParams()]
    public override async DmTask fire(float? resumed = null, params object[] __extra)
    {
        await base.fire(resumed, __extra); /*just so it shows up on the profiler*/
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/processing/clock_component.dm:2  PROCESSING_SUBSYSTEM_DEF(clock_component): GLOBAL_REAL(SSclock_component, /datum/controller/subsystem/processing/clock_component)
    internal datum_controller_subsystem_processing_clock_component SSclock_component;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_processing_clock_component SSclock_component => ref TgState.Current.SSclock_component;
}
