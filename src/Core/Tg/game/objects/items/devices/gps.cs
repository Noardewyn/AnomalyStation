// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/devices/gps.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI2 (D326): the GPS, whole (the mining cyborg's, the miners', the departments' and the pAI's). The survival pod's computer
// GPS (survival_pod.dm) comes with its pod. The var block is generated (D111).

// tg: code/game/objects/items/devices/gps.dm:1
public partial class obj_item_gps : obj_item
{
    // tg: code/game/objects/items/devices/gps.dm:23
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        add_gps_component();
        return dot;
    }

    /// Adds the GPS component to this item.
    // tg: code/game/objects/items/devices/gps.dm:28
    public virtual void add_gps_component()
    {
        Waiting.NoSleep(AddComponent(typeof(datum_component_gps_item), gpstag, tracking_on, Dm.named("overlay_state", overlay_icon), Dm.named("debug", debug_mode)));
    }
}

/*
 * GPS for pAIS, which only allows access if it's contained within the user.
 */
// tg: code/game/objects/items/devices/gps.dm:60
public partial class obj_item_gps_pai : obj_item_gps
{
    // tg: code/game/objects/items/devices/gps.dm:63
    public override void add_gps_component()
    {
        Waiting.NoSleep(AddComponent(typeof(datum_component_gps_item), gpstag, Dm.named("state", GLOB.inventory_state)));
    }
}
