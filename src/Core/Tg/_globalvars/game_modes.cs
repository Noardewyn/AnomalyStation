// Translated from tgstation (snapshot 1d99bd7f): code/_globalvars/game_modes.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M10 slice H): the round-end report's globals. The heretics' reality smash tracker and the deathmatch controller are the
// antagonists' and the minigames' (not ported). M10 slice H also declares _nuclear_bomb.dm's station_was_nuked here (the nuke is not
// ported; nothing sets it).

// tg: code/_globalvars/game_modes.dm:1
public partial class datum_controller_global_vars : datum_controller
{
    //Contains common part of roundend report
    // tg: code/_globalvars/game_modes.dm:1  GLOBAL_VAR(common_report)
    public object common_report;

    // tg: code/_globalvars/game_modes.dm:1  (GLOBAL_UNMANAGED(common_report), __DEFINES/__globals.dm:11)
    public virtual void InitGlobalcommon_report()
    {
        return;
    }

    //Contains shared survivor report for roundend report (part of personal report)
    // tg: code/_globalvars/game_modes.dm:2  GLOBAL_VAR(survivor_report)
    public object survivor_report;

    // tg: code/_globalvars/game_modes.dm:2  (GLOBAL_UNMANAGED(survivor_report), __DEFINES/__globals.dm:11)
    public virtual void InitGlobalsurvivor_report()
    {
        return;
    }

    // Used in round-end report
    // tg: code/_globalvars/game_modes.dm:4  GLOBAL_DATUM(start_state, /datum/station_state)
    public datum_station_state start_state;

    // tg: code/_globalvars/game_modes.dm:4  (GLOBAL_UNMANAGED(start_state), __DEFINES/__globals.dm:11)
    public virtual void InitGlobalstart_state()
    {
        return;
    }

    // tg: code/modules/antagonists/nukeop/equipment/nuclear_bomb/_nuclear_bomb.dm:2  GLOBAL_VAR_INIT(station_was_nuked, FALSE)
    public object station_was_nuked;

    // tg: code/modules/antagonists/nukeop/equipment/nuclear_bomb/_nuclear_bomb.dm:2  (GLOBAL_MANAGED(station_was_nuked, FALSE), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalstation_was_nuked()
    {
        station_was_nuked = Dm.FALSE;
        Dm.append(ref gvars_datum_init_order, "station_was_nuked");
    }
}
