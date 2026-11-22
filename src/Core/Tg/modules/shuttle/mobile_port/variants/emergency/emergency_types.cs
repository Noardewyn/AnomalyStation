// Translated from tgstation (snapshot 1d99bd7f): code/modules/shuttle/mobile_port/variants/emergency/emergency_types.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M10 slice G4 (D272): the backup emergency shuttle, which the CentCom stand-in's backup dock loads (DV190). Gaps CM1 (D333): the
// monastery pod and the build-your-own shuttle, which the purchasable monastery and construction kit shuttles carry.

/// Fallback shuttle
// tg: code/modules/shuttle/mobile_port/variants/emergency/emergency_types.dm:2
[DmPath("/obj/docking_port/mobile/emergency/backup")]
public partial class obj_docking_port_mobile_emergency_backup : obj_docking_port_mobile_emergency
{
    // tg: code/modules/shuttle/mobile_port/variants/emergency/emergency_types.dm:7
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        // We want to be a valid emergency shuttle
        // but not be the main one, keep whatever's set
        // valid.
        // backup shuttle ignores `timid` because THERE SHOULD BE NO TOUCHING IT
        obj_docking_port_mobile_emergency current_emergency = SSshuttle.emergency;
        object dot = base.Initialize(mapload, __extra);
        SSshuttle.emergency = current_emergency;
        SSshuttle.backup_shuttle = this;
        return dot;
    }

    // tg: code/modules/shuttle/mobile_port/variants/emergency/emergency_types.dm:17
    public override object Destroy(float? force = null)
    {
        if (SSshuttle.backup_shuttle == this)
            SSshuttle.backup_shuttle = null;
        return base.Destroy(force);
    }
}

/// Monastery shuttle
// tg: code/modules/shuttle/mobile_port/variants/emergency/emergency_types.dm:23
public partial class obj_docking_port_mobile_monastery : obj_docking_port_mobile
{
    // tg: code/modules/shuttle/mobile_port/variants/emergency/emergency_types.dm:28
    public override async DmTask on_emergency_dock()
    {
        if (Dm.eq(launch_status, ENDGAME_LAUNCHED))
        {
            await initiate_docking(SSshuttle.getDock("pod_away") as obj_docking_port_stationary); //docks our shuttle as any pod would
            mode = SHUTTLE_ENDGAME;
        }
    }
}

/// Build Your Own Shuttle (BYOS) kit
// tg: code/modules/shuttle/mobile_port/variants/emergency/emergency_types.dm:34
public partial class obj_docking_port_mobile_emergency_shuttle_build : obj_docking_port_mobile_emergency
{
    // tg: code/modules/shuttle/mobile_port/variants/emergency/emergency_types.dm:36
    public override async DmTask postregister(float? replace = null)
    {
        await base.postregister(replace);
        await initiate_docking(SSshuttle.getDock("emergency_home") as obj_docking_port_stationary);
    }
}
