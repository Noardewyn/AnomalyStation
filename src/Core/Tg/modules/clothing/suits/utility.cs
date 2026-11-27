// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/suits/utility.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7 radiation slice: the radiation hood's and suit's Initialize (their radiation_protected_clothing); the other types of the
// file (fire, bomb and the other utility suits) are generated. The var blocks are generated (D111). The hood's hat stabilizer (a hat worn over the hood, M10+), its clothing dirt (pepper spray and spray-can
// tint, chemistry, M8) and the mesh texture the suits put on the bodypart under them (bodypart textures, M10+) are deferred.

// Radiation
// tg: code/modules/clothing/suits/utility.dm:189
public partial class obj_item_clothing_head_utility_radiation : obj_item_clothing_head_utility
{
    // tg: code/modules/clothing/suits/utility.dm:201
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_adjust_fishing_difficulty), 7));
        Waiting.NoSleep(AddComponent(typeof(datum_component_hat_stabilizer), Dm.named("loose_hat", TRUE))); // gaps MO1: the component is live
        Waiting.NoSleep(AddElement(typeof(datum_element_radiation_protected_clothing)));
        Dm.Deferred("code/modules/clothing/suits/utility.dm:206", "Initialize: AddComponent(/datum/component/clothing_dirt, \"rad_dirt\"), the visor's pepper spray tint (chemistry, M8)");
        Dm.Deferred("code/modules/clothing/suits/utility.dm:207", "Initialize: AddElement(/datum/element/equipment_bodypart_texture, BODY_ZONE_HEAD, ...), the mesh texture on the head (bodypart textures, M10+)");
        return dot;
    }
}

// tg: code/modules/clothing/suits/utility.dm:214
public partial class obj_item_clothing_suit_utility_radiation : obj_item_clothing_suit_utility
{
    // tg: code/modules/clothing/suits/utility.dm:235
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_radiation_protected_clothing)));
        Waiting.NoSleep(AddElement(typeof(datum_element_adjust_fishing_difficulty), 7));
        Dm.Deferred("code/modules/clothing/suits/utility.dm:239", "Initialize: AddElement(/datum/element/equipment_bodypart_texture, BODY_ZONE_CHEST, ...), the mesh texture on the chest (bodypart textures, M10+)");
        return dot;
    }
}
