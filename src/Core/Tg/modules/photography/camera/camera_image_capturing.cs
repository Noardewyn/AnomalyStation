// Translated from tgstation (snapshot 1d99bd7f): code/modules/photography/camera/camera_image_capturing.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The picture's image has no counterpart in the 3D client (gaps J3, D298; not-translated section 107): getFlatIcon of every seen atom composed on one icon. The rest of the camera works without it.

// tg: code/modules/photography/camera/camera_image_capturing.dm:18
public partial class obj_item_camera : obj_item
{
    // tg: code/modules/photography/camera/camera_image_capturing.dm:18
    public virtual async DmTask<object> camera_get_icon(DmList turfs = null, turf center = null, datum_turf_reservation clone_area = null)
    {
        Dm.Deferred("code/modules/photography/camera/camera_image_capturing.dm:18", "camera_get_icon: the picture's image, the seen atoms' sprites composed on one icon (no icon compositing of the 3D view; the photo shows its description)");
        return null;
    }
}
