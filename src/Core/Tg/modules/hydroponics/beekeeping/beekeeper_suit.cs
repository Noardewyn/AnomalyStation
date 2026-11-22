// Translated from tgstation (snapshot 1d99bd7f): code/modules/hydroponics/beekeeping/beekeeper_suit.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P2 (D298): the whole file. The mesh body texture element is inert (the bodypart textures are not ported): AddElement is kept.

// tg: code/modules/hydroponics/beekeeping/beekeeper_suit.dm:1
public partial class obj_item_clothing_head_utility_beekeeper_head : obj_item_clothing_head_utility
{
    // tg: code/modules/hydroponics/beekeeping/beekeeper_suit.dm:12
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_equipment_bodypart_texture), BODY_ZONE_HEAD, typeof(datum_bodypart_texture_mesh_white)));
        return dot;
    }
}

// tg: code/modules/hydroponics/beekeeping/beekeeper_suit.dm:16
public partial class obj_item_clothing_suit_utility_beekeeper_suit : obj_item_clothing_suit_utility
{
    // tg: code/modules/hydroponics/beekeeping/beekeeper_suit.dm:28
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_equipment_bodypart_texture), BODY_ZONE_CHEST, typeof(datum_bodypart_texture_mesh_white)));
        return dot;
    }
}
