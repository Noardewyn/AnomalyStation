// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/sun.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7 slice G1 (Meta's solars, D163): the whole file but vv_edit_var (admin variable editing, M10: docs/ai/specs/live-partial.txt). The var
// block is generated (D111). The sun moves once a minute and tells the solar panels, trackers and control computers by COMSIG_SUN_MOVED.

// tg: code/controllers/subsystem/sun.dm:1 SUBSYSTEM_DEF(sun)
public partial class datum_controller_subsystem_sun : datum_controller_subsystem
{
    // The New() that SUBSYSTEM_DEF(sun) defines (__DEFINES/MC.dm:93-96), with NEW_SS_GLOBAL(SSsun)
    // (__DEFINES/MC.dm:29) expanded inline (it reads src).
    // tg: code/controllers/subsystem/sun.dm:1
    public override async DmTask New(params object[] __extra)
    {
        if (SSsun != this)
        {
            if (SSsun is datum_controller_subsystem_sun)
            {
                Recover();
                qdel(SSsun);
            }
            SSsun = this;
        }
        PreInit();
    }

    // tg: code/controllers/subsystem/sun.dm:9
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        azimuth = rand(0, 359);
        azimuth_mod = round(rand(50, 200) / 100, 0.01f); // 50% - 200% of standard rotation
        if (prob(50))
            azimuth_mod *= -1;
        // DEVIATION(DV505): a map may fix its sun (the map json's anomaly.sun.azimuth, MapSettings): it stands there and does not go round
        if (MapSettingsSeam.Current.SunAzimuth is { } fixed_azimuth)
            (azimuth, azimuth_mod) = (fixed_azimuth, 0);
        return SS_INIT_SUCCESS;
    }

    // tg: code/controllers/subsystem/sun.dm:15
    public override async DmTask fire(float? resumed = null, params object[] __extra)
    {
        resumed ??= FALSE;
        azimuth += azimuth_mod * base_rotation;
        azimuth = round(azimuth, 0.01f);
        if (azimuth >= 360)
            azimuth -= 360;
        if (azimuth < 0)
            azimuth += 360;
        complete_movement();
    }

    // tg: code/controllers/subsystem/sun.dm:24
    public virtual void complete_movement()
    {
        if (Dm.truthy(_listen_lookup?[COMSIG_SUN_MOVED])) // SEND_SIGNAL (__DEFINES/dcs/helpers.dm:5)
            _SendSignal(COMSIG_SUN_MOVED, Dm.list(this, azimuth));
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/sun.dm:1  SUBSYSTEM_DEF(sun): GLOBAL_REAL(SSsun, /datum/controller/subsystem/sun)
    internal datum_controller_subsystem_sun SSsun;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_sun SSsun => ref TgState.Current.SSsun;
}
