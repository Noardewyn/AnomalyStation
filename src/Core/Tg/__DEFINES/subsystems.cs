// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/subsystems.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only SSOBJ_DT and DELTA_WORLD_TIME translated (the timers and processing package, wave 3). The constants of
// subsystems.dm are generated into Defines; addtimer (:218) is expanded inline at every use (rules 11.1 (f)) and
// INITIALIZE_IMMEDIATE (:96) inline per type by the atoms package (rules 11.1 (c), (d)); SSMACHINES_DT (:276) waits for
// SSmachines; M2 P1 added SSMOBS_DT (:277), E15 SSFLUIDS_DT (:275).

public static partial class GlobalProcs
{
    // Subsystem delta times or tickrates, in seconds. I.e, how many seconds in between each process() call for objects being processed by that subsystem.
    // Only use these defines if you want to access some other objects processing seconds_per_tick, otherwise use the seconds_per_tick that is sent as a parameter to process()
    // tg: code/__DEFINES/subsystems.dm:278  (an object-like macro: a member, rules 11.1)
    public static float SSOBJ_DT() => (SSobj.wait / 10);

    // The change in the world's time from the subsystem's last fire in seconds.
    // tg: code/__DEFINES/subsystems.dm:281
    public static float DELTA_WORLD_TIME(object ss) => ((world.time - ((datum_controller_subsystem)ss).last_fire) * 0.1f);

    // M2 P1 (living core): SSmobs is translated.
    // tg: code/__DEFINES/subsystems.dm:277  #define SSMOBS_DT (SSmobs.wait/10)  (an object-like macro: a member, rules 11.1)
    public static float SSMOBS_DT() => (SSmobs.wait / 10);

    // E15 (plumbing): SSplumbing is translated.
    // tg: code/__DEFINES/subsystems.dm:275  #define SSFLUIDS_DT (SSplumbing.wait/10)  (an object-like macro: a member, rules 11.1)
    public static float SSFLUIDS_DT() => (SSplumbing.wait / 10);
}
