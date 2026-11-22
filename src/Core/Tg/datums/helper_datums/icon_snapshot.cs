// Translated from tgstation (snapshot 1d99bd7f): code/datums/helper_datums/icon_snapshot.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps K1 (D327): the whole file (the changeling profile's picture of its source: name, icon, icon state and overlays, which the
// changeling's disguise menu draws). makeImg's flattened icon is getFlatIcon, the presentation's (not-translated.md 187). The var
// block is generated (D111).

// tg: code/datums/helper_datums/icon_snapshot.dm:1
public partial class datum_icon_snapshot : datum
{
    // tg: code/datums/helper_datums/icon_snapshot.dm:7
    public virtual object makeImg()
    {
        if (!Dm.truthy(icon) || !Dm.truthy(icon_state))
            return null;
        obj temp = Dm.@new<obj>();
        temp.icon = icon;
        temp.icon_state = icon_state as string;
        temp.overlays = overlays.Copy();
        // var/icon/tempicon = getFlatIcon(temp) // TODO Actually write something less heavy-handed for this
        Dm.Deferred("code/datums/helper_datums/icon_snapshot.dm:14", "makeImg: getFlatIcon of the snapshot (the flattened 2D picture is the presentation's, chat icons; the 3D client draws the snapshot's icon and overlays itself)");
        return null;
    }
}
