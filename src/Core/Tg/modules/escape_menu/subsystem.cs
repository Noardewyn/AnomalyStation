// Translated from tgstation (snapshot 1d99bd7f): code/modules/escape_menu/subsystem.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M10x slice G4: the whole file.

/// Subsystem for controlling anything related to the escape menu
// tg: code/modules/escape_menu/subsystem.dm:2 PROCESSING_SUBSYSTEM_DEF(escape_menu)
[DmPath("/datum/controller/subsystem/processing/escape_menu")]
public partial class datum_controller_subsystem_processing_escape_menu : datum_controller_subsystem_processing
{
    // The New() that PROCESSING_SUBSYSTEM_DEF(escape_menu) defines (__DEFINES/MC.dm:116-119), with NEW_SS_GLOBAL(SSescape_menu)
    // (__DEFINES/MC.dm:29) expanded inline (it reads src).
    // tg: code/modules/escape_menu/subsystem.dm:2
    public override async DmTask New(params object[] __extra)
    {
        if (SSescape_menu != this)
        {
            if (SSescape_menu is datum_controller_subsystem_processing_escape_menu)
            {
                Recover();
                qdel(SSescape_menu);
            }
            SSescape_menu = this;
        }
        PreInit();
    }

    // The fire() that PROCESSING_SUBSYSTEM_DEF(escape_menu) defines (__DEFINES/MC.dm:120): ..() /*just so it shows up on the profiler*/
    // tg: code/modules/escape_menu/subsystem.dm:2
    [DmParams()]
    public override async DmTask fire(float? resumed = null, params object[] __extra)
    {
        await base.fire(resumed, __extra); /*just so it shows up on the profiler*/
    }
}

public sealed partial class TgState
{
    // tg: code/modules/escape_menu/subsystem.dm:2  PROCESSING_SUBSYSTEM_DEF(escape_menu): GLOBAL_REAL(SSescape_menu, /datum/controller/subsystem/processing/escape_menu)
    internal datum_controller_subsystem_processing_escape_menu SSescape_menu;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_processing_escape_menu SSescape_menu => ref TgState.Current.SSescape_menu;
}
