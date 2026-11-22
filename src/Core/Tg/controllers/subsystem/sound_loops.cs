// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/sound_loops.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4b audio slice 5 (loops and alarms): the whole file. The var block (name, priority) is generated (D111); the rest is
// SStimer's (controllers/subsystem/timer.dm), which this timer subsystem inherits.

// tg: code/controllers/subsystem/sound_loops.dm:1 TIMER_SUBSYSTEM_DEF(sound_loops)
[DmPath("/datum/controller/subsystem/timer/sound_loops")]
public partial class datum_controller_subsystem_timer_sound_loops : datum_controller_subsystem_timer
{
    // The New() that TIMER_SUBSYSTEM_DEF(sound_loops) defines (__DEFINES/MC.dm:100-103), with NEW_SS_GLOBAL(SSsound_loops)
    // (__DEFINES/MC.dm:29) expanded inline (it reads src). It replaces SStimer's New (no ..()).
    // tg: code/controllers/subsystem/sound_loops.dm:1
    public override async DmTask New(params object[] __extra)
    {
        if (SSsound_loops != this)
        {
            if (SSsound_loops is datum_controller_subsystem_timer_sound_loops)
            {
                Recover();
                qdel(SSsound_loops);
            }
            SSsound_loops = this;
        }
        PreInit();
    }

    // The fire() that TIMER_SUBSYSTEM_DEF(sound_loops) defines (__DEFINES/MC.dm:104): ..() /*just so it shows up on the profiler*/
    // tg: code/controllers/subsystem/sound_loops.dm:1
    [DmParams()]
    public override async DmTask fire(float? resumed = null, params object[] __extra)
    {
        await base.fire(resumed, __extra); /*just so it shows up on the profiler*/
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/sound_loops.dm:1  TIMER_SUBSYSTEM_DEF(sound_loops): GLOBAL_REAL(SSsound_loops, /datum/controller/subsystem/timer/sound_loops)
    internal datum_controller_subsystem_timer_sound_loops SSsound_loops;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_timer_sound_loops SSsound_loops => ref TgState.Current.SSsound_loops;
}
