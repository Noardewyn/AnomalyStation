// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/devices/sensor_device.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/game/objects/items/devices/sensor_device.dm:1
public partial class obj_item_sensor_device : obj_item
{
    // tg: code/game/objects/items/devices/sensor_device.dm:18
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        await GLOB.crewmonitor.show(user, this); //Proc already exists, just had to call it
        return null;
    }

    // tg: code/game/objects/items/devices/sensor_device.dm:21
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_drag_to_activate)));
        return dot;
    }
}
