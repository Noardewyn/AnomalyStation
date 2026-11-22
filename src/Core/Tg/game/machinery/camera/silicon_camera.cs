// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/camera/silicon_camera.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI7 (D326): the whole file (a drone's built-in camera). The var blocks are generated (D111).

///Special type of camera installed into silicons (drones included) that gives an alert when someone is watching them through cameras.
// tg: code/game/machinery/camera/silicon_camera.dm:2
public partial class obj_machinery_camera_silicon : obj_machinery_camera
{
    // tg: code/game/machinery/camera/silicon_camera.dm:11
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (!isliving(loc))
            return INITIALIZE_HINT_QDEL;
        living_host = (mob_living)loc;
        return dot;
    }

    // tg: code/game/machinery/camera/silicon_camera.dm:17
    public override object Destroy(float? force = null)
    {
        if (living_host != null)
        {
            if (Dm.truthy(living_host.has_alert(ALERT_SILICON_RECORDING)))
                living_host.clear_alert(ALERT_SILICON_RECORDING);
            living_host = null;
        }
        sources_watching = null;
        return base.Destroy(force);
    }

    // tg: code/game/machinery/camera/silicon_camera.dm:25
    public override void on_start_watching(datum source = null)
    {
        (sources_watching ??= Dm.list()).Add(source);
        living_host.throw_alert(ALERT_SILICON_RECORDING, typeof(atom_movable_screen_alert_being_recorded));
    }

    // tg: code/game/machinery/camera/silicon_camera.dm:29
    public override void on_stop_watching(datum source = null)
    {
        sources_watching?.Remove(source); // no_longer_watching
        if (Dm.length(sources_watching) == 0 && Dm.truthy(living_host.has_alert(ALERT_SILICON_RECORDING)))
            living_host.clear_alert(ALERT_SILICON_RECORDING);
    }
}
