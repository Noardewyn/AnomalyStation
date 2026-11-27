// Translated from tgstation (snapshot 1d99bd7f): code/modules/photography/camera/camera_image_capturing.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// DEVIATION(DV685): the picture's image is the photographer's client's 3D render of the shot (PhotoSeam), not this composite of every seen atom's
// getFlatIcon on one icon; capture_image no longer calls it.

// tg: code/modules/photography/camera/camera_image_capturing.dm:18
public partial class obj_item_camera : obj_item
{
    // tg: code/modules/photography/camera/camera_image_capturing.dm:18
    public virtual async DmTask<object> camera_get_icon(DmList turfs = null, turf center = null, datum_turf_reservation clone_area = null)
    {
        Dm.Deferred("code/modules/photography/camera/camera_image_capturing.dm:18", "camera_get_icon: the seen atoms' sprites composed on one icon (DV685: the client renders the picture in 3D, PhotoSeam)");
        return null;
    }
}
