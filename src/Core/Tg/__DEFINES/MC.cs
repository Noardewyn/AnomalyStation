// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/MC.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The constants of MC.dm are generated into Defines. Macros expanded inline at each use (rules 11.1) and therefore
// without a member here:
// tg: code/__DEFINES/MC.dm:1  MC_TICK_CHECK (reads src.state, (d)):
//     ( ( TICK_USAGE > Master.current_ticklimit || src.state != SS_RUNNING ) ? pause() : 0 )
// tg: code/__DEFINES/MC.dm:3  MC_SPLIT_TICK_INIT(phase_count) (declares the caller's locals original_tick_limit and
//     split_tick_phases, (c))
// tg: code/__DEFINES/MC.dm:4-10  MC_SPLIT_TICK (reads and writes those locals, (d))
// tg: code/__DEFINES/MC.dm:29  NEW_SS_GLOBAL(varname) (reads src, (d); assigns its argument)
// tg: code/__DEFINES/MC.dm:92-145  SUBSYSTEM_DEF(X) and its variants (token pasting, (b)): each subsystem's mirror file
//     holds its TgState field, accessor and New (rules 10.6).

public static partial class GlobalProcs
{
    // Used to smooth out costs to try and avoid oscillation.
    // tg: code/__DEFINES/MC.dm:13
    public static float MC_AVERAGE_FAST(float average, float current) => (0.7f * (average) + 0.3f * (current));
    // tg: code/__DEFINES/MC.dm:14
    public static float MC_AVERAGE(float average, float current) => (0.8f * (average) + 0.2f * (current));
    // tg: code/__DEFINES/MC.dm:15
    public static float MC_AVERAGE_SLOW(float average, float current) => (0.9f * (average) + 0.1f * (current));

    // tg: code/__DEFINES/MC.dm:17
    public static float MC_AVG_FAST_UP_SLOW_DOWN(float average, float current) => (average > current ? MC_AVERAGE_SLOW(average, current) : MC_AVERAGE_FAST(average, current));
    // tg: code/__DEFINES/MC.dm:18
    public static float MC_AVG_SLOW_UP_FAST_DOWN(float average, float current) => (average < current ? MC_AVERAGE_SLOW(average, current) : MC_AVERAGE_FAST(average, current));

    ///creates a running average of "things elapsed" per time period when you need to count via a smaller time period.
    ///eg you want an average number of things happening per second but you measure the event every tick (50 milliseconds).
    ///make sure both time intervals are in the same units. doesn't work if current_duration > total_duration or if total_duration == 0
    // tg: code/__DEFINES/MC.dm:23
    public static float MC_AVG_OVER_TIME(float average, float current, float total_duration, float current_duration) => ((Dm.div((total_duration) - (current_duration), (total_duration))) * (average) + (current));

    // tg: code/__DEFINES/MC.dm:25
    public static float MC_AVG_MINUTES(float average, float current, float current_duration) => (MC_AVG_OVER_TIME(average, current, 1 * MINUTES, current_duration));

    // tg: code/__DEFINES/MC.dm:27
    public static float MC_AVG_SECONDS(float average, float current, float current_duration) => (MC_AVG_OVER_TIME(average, current, 1 * SECONDS, current_duration));

    // tg: code/__DEFINES/MC.dm:31  (a statement; Processor is any datum with a processing list: the processing
    // subsystems, SSmachines and others declare their own, so Processor.processing is read and written by name, rules 4.8)
    // RULE-GAP: rules 11.1 casts an object macro parameter to the type that declares the member; processing is declared by
    // several unrelated types, so the member access is the by-name form of 4.8.
    public static void START_PROCESSING(object Processor, object Datum)
    {
        if (!Dm.truthy(Dm.bitand(((datum)Datum).datum_flags, DF_ISPROCESSING)))
        {
            Dm.combine(ref ((datum)Datum).datum_flags, DF_ISPROCESSING);
            object processing = Dm.get_var(Processor, "processing");
            Dm.append(ref processing, Datum);
            Dm.set_var(Processor, "processing", processing);
        }
    }

    // tg: code/__DEFINES/MC.dm:32  (a statement; Processor.processing and Processor.currentrun by name, as in START_PROCESSING)
    public static void STOP_PROCESSING(object Processor, object Datum)
    {
        Dm.mask(ref ((datum)Datum).datum_flags, Dm.bitnot(DF_ISPROCESSING));
        object processing = Dm.get_var(Processor, "processing");
        Dm.remove(ref processing, Datum);
        Dm.set_var(Processor, "processing", processing);
        object currentrun = Dm.get_var(Processor, "currentrun");
        Dm.remove(ref currentrun, Datum);
        Dm.set_var(Processor, "currentrun", currentrun);
    }

    /// Returns true if the MC is initialized and running.
    /// Optional argument init_stage controls what stage the mc must have initialized to count as initialized. Defaults to INITSTAGE_MAX if not specified.
    // tg: code/__DEFINES/MC.dm:36  (INIT_STAGE... is forwarded into min(INITSTAGE_MAX, ##INIT_STAGE): with no argument the
    // call is min(INITSTAGE_MAX))
    public static bool MC_RUNNING(params object[] INIT_STAGE)
    {
        object[] t__1 = new object[INIT_STAGE.Length + 1];
        t__1[0] = INITSTAGE_MAX;
        INIT_STAGE.CopyTo(t__1, 1);
        return (Dm.truthy(Master) && Master.processing > 0 && Dm.truthy(Master.current_runlevel) && Dm.ge(datum_controller_master.init_stage_completed, max(min(t__1), 1)));
    }
}
