// Translated from tgstation (snapshot 1d99bd7f): code/modules/events/camerafailure.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (silicons SI4, D326): the round event that switches off a random number of the station's cameras. The var blocks are
// generated (D111).

// tg: code/modules/events/camerafailure.dm:10
public partial class datum_round_event_camera_failure : datum_round_event
{
    // tg: code/modules/events/camerafailure.dm:13
    public override async DmTask<object> start()
    {
        float iterations = 1;
        DmList cameras = SScameras.cameras.Copy();
        while (prob(round(100 / iterations)))
        {
            obj_machinery_camera C = pick_n_take(cameras) as obj_machinery_camera;
            if (C == null)
                break;
            if (!Dm.@in(CAMERANET_NETWORK_SS13, C.network))
                continue;
            if (Dm.truthy(C.camera_enabled))
                C.toggle_cam(null, 0);
            iterations *= 2.5f;
        }
        await DmTask.CompletedTask;
        return null;
    }
}
