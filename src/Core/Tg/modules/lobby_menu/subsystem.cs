// Translated from tgstation (snapshot 1d99bd7f): code/modules/lobby_menu/subsystem.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M10 slice F: the whole file.

// tg: code/modules/lobby_menu/subsystem.dm:1 PROCESSING_SUBSYSTEM_DEF(lobby_menu)
[DmPath("/datum/controller/subsystem/processing/lobby_menu")]
public partial class datum_controller_subsystem_processing_lobby_menu : datum_controller_subsystem_processing
{
    // The New() that PROCESSING_SUBSYSTEM_DEF(lobby_menu) defines (__DEFINES/MC.dm:116-119), with NEW_SS_GLOBAL(SSlobby_menu)
    // (__DEFINES/MC.dm:29) expanded inline (it reads src).
    // tg: code/modules/lobby_menu/subsystem.dm:1
    public override async DmTask New(params object[] __extra)
    {
        if (SSlobby_menu != this)
        {
            if (SSlobby_menu is datum_controller_subsystem_processing_lobby_menu)
            {
                Recover();
                qdel(SSlobby_menu);
            }
            SSlobby_menu = this;
        }
        PreInit();
    }

    // The fire() that PROCESSING_SUBSYSTEM_DEF(lobby_menu) defines (__DEFINES/MC.dm:120): ..() /*just so it shows up on the profiler*/
    // tg: code/modules/lobby_menu/subsystem.dm:1
    [DmParams()]
    public override async DmTask fire(float? resumed = null, params object[] __extra)
    {
        await base.fire(resumed, __extra); /*just so it shows up on the profiler*/
    }
}

public sealed partial class TgState
{
    // tg: code/modules/lobby_menu/subsystem.dm:1  PROCESSING_SUBSYSTEM_DEF(lobby_menu): GLOBAL_REAL(SSlobby_menu, /datum/controller/subsystem/processing/lobby_menu)
    internal datum_controller_subsystem_processing_lobby_menu SSlobby_menu;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_processing_lobby_menu SSlobby_menu => ref TgState.Current.SSlobby_menu;
}
