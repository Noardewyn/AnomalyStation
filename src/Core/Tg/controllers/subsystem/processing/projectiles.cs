// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/processing/projectiles.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The M7 projectiles slice (D165): the whole file. The var block is generated (D111).

// tg: code/controllers/subsystem/processing/projectiles.dm:1 PROCESSING_SUBSYSTEM_DEF(projectiles) (the var block is generated)
public partial class datum_controller_subsystem_processing_projectiles : datum_controller_subsystem_processing
{
    // The New() that PROCESSING_SUBSYSTEM_DEF(projectiles) defines (__DEFINES/MC.dm:116-119), with NEW_SS_GLOBAL(SSprojectiles)
    // (__DEFINES/MC.dm:29) expanded inline (it reads src).
    // tg: code/controllers/subsystem/processing/projectiles.dm:1
    public override async DmTask New(params object[] __extra)
    {
        if (SSprojectiles != this)
        {
            if (SSprojectiles is datum_controller_subsystem_processing_projectiles)
            {
                Recover();
                qdel(SSprojectiles);
            }
            SSprojectiles = this;
        }
        // DEVIATION(DV211): the port's projectiles fly faster than tg's, through tg's own debug knob (the dev command projspeed changes it live)
        pixels_per_decisecond = ICON_SIZE_ALL * DevCommands.DefaultProjectileSpeedScale;
        PreInit();
    }

    // The fire() that PROCESSING_SUBSYSTEM_DEF(projectiles) defines (__DEFINES/MC.dm:120): ..() /*just so it shows up on the profiler*/
    // tg: code/controllers/subsystem/processing/projectiles.dm:1
    [DmParams()]
    public override async DmTask fire(float? resumed = null, params object[] __extra)
    {
        await base.fire(resumed, __extra); /*just so it shows up on the profiler*/
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/processing/projectiles.dm:1  PROCESSING_SUBSYSTEM_DEF(projectiles): GLOBAL_REAL(SSprojectiles, /datum/controller/subsystem/processing/projectiles)
    internal datum_controller_subsystem_processing_projectiles SSprojectiles;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_processing_projectiles SSprojectiles => ref TgState.Current.SSprojectiles;
}
