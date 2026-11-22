// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/guns/ballistic/bows/bow_types.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps GU1, D333): the longbow, the shortbow and the ashen bow only set vars; the chaplain's divine bow is a null rod
// that brings its quiver of holy arrows when picked. The var blocks are generated (D111).

///chaplain's divine archer bow
// tg: code/modules/projectiles/guns/ballistic/bows/bow_types.dm:16
public partial class obj_item_gun_ballistic_bow_divine : obj_item_gun_ballistic_bow
{
    // tg: code/modules/projectiles/guns/ballistic/bows/bow_types.dm:33
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_nullrod_core)));
        RegisterSignal(this, COMSIG_ITEM_SUBTYPE_PICKER_SELECTED, nameof(on_selected));
        return dot;
    }

    // tg: code/modules/projectiles/guns/ballistic/bows/bow_types.dm:38
    [SignalHandler]
    public virtual void on_selected(datum source = null, obj_item_nullrod old_weapon = null, mob picker = null)
    {
        Dm.@new<obj_item_storage_bag_quiver_holy>(loc);
    }
}
