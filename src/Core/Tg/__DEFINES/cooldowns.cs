// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/cooldowns.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only COMSIG_CD_STOP, COMSIG_CD_RESET, TIMER_COOLDOWN_RUNNING, TIMER_COOLDOWN_END and S_TIMER_COOLDOWN_TIMELEFT translated
// (the timer cooldown macros that end_cooldown and reset_cooldown in datums/datum.cs use); the rest of cooldowns.dm
// (TIMER_COOLDOWN_START and S_TIMER_COOLDOWN_START expand addtimer, so they are inline, rules 11.1 (f)) is completed
// with the timer translation.

public static partial class GlobalProcs
{
    //TIMER COOLDOWN MACROS

    // tg: code/__DEFINES/cooldowns.dm:74
    public static string COMSIG_CD_STOP(object cd_index) => Dm.text("cooldown_[]", cd_index);
    // tg: code/__DEFINES/cooldowns.dm:75
    public static string COMSIG_CD_RESET(object cd_index) => Dm.text("cd_reset_[]", cd_index);

    /// Checks if a timer based cooldown is NOT finished.
    // tg: code/__DEFINES/cooldowns.dm:80
    // tg: code/__DEFINES/cooldowns.dm:34
    public static string COOLDOWN_MECHA_EQUIPMENT(object type) => Dm.text("mecha_equip_[]", type);

    public static object TIMER_COOLDOWN_RUNNING(object cd_source, object cd_index) => LAZYACCESS(((datum)cd_source).cooldowns, cd_index);

    // tg: code/__DEFINES/cooldowns.dm:85
    public static void TIMER_COOLDOWN_END(object cd_source, object cd_index) => LAZYREMOVE(ref ((datum)cd_source).cooldowns, cd_index);

    // tg: code/__DEFINES/cooldowns.dm:98
    public static object S_TIMER_COOLDOWN_TIMELEFT(object cd_source, object cd_index) => timeleft(TIMER_COOLDOWN_RUNNING(cd_source, cd_index));
}
