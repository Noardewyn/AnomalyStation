// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/suits/labcoat.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// J1 (D298): the Initialize of the job labcoats (the allowed lists). The labcoat's buttons are the toggle suit's component
// (suits/toggles.cs, datums/components/toggle_suit.cs); the var blocks are generated.

// tg: code/modules/clothing/suits/labcoat.dm:38
public partial class obj_item_clothing_suit_toggle_labcoat_cmo : obj_item_clothing_suit_toggle_labcoat
{
    // tg: code/modules/clothing/suits/labcoat.dm:52
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.append(ref allowed, Dm.list(
            typeof(obj_item_melee_baton_telescopic)
        ));
        return dot;
    }
}

// tg: code/modules/clothing/suits/labcoat.dm:59
public partial class obj_item_clothing_suit_toggle_labcoat_paramedic : obj_item_clothing_suit_toggle_labcoat
{
    // tg: code/modules/clothing/suits/labcoat.dm:64
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // AddElement(/datum/element/adjust_fishing_difficulty, -3) //FISH DOCTOR?!
        Dm.Deferred("code/modules/clothing/suits/labcoat.dm:65", "Initialize: AddElement(/datum/element/adjust_fishing_difficulty, -3): fishing (M10+)");
        Dm.append(ref allowed, typeof(obj_item_crowbar_power_paramedic));
        return dot;
    }
}

// tg: code/modules/clothing/suits/labcoat.dm:76
public partial class obj_item_clothing_suit_toggle_labcoat_genetics : obj_item_clothing_suit_toggle_labcoat
{
    // tg: code/modules/clothing/suits/labcoat.dm:85
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.append(ref allowed, typeof(obj_item_sequence_scanner));
        return dot;
    }
}

// tg: code/modules/clothing/suits/labcoat.dm:90
public partial class obj_item_clothing_suit_toggle_labcoat_chemist : obj_item_clothing_suit_toggle_labcoat
{
    // tg: code/modules/clothing/suits/labcoat.dm:99
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.append(ref allowed, typeof(obj_item_storage_bag_chemistry));
        return dot;
    }
}

// tg: code/modules/clothing/suits/labcoat.dm:118
public partial class obj_item_clothing_suit_toggle_labcoat_coroner : obj_item_clothing_suit_toggle_labcoat
{
    // tg: code/modules/clothing/suits/labcoat.dm:127
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.append(ref allowed, Dm.list(
            typeof(obj_item_autopsy_scanner),
            typeof(obj_item_scythe),
            typeof(obj_item_shovel),
            typeof(obj_item_shovel_serrated),
            typeof(obj_item_trench_tool)
        ));
        return dot;
    }
}

// tg: code/modules/clothing/suits/labcoat.dm:138
public partial class obj_item_clothing_suit_toggle_labcoat_science : obj_item_clothing_suit_toggle_labcoat
{
    // tg: code/modules/clothing/suits/labcoat.dm:147
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.append(ref allowed, typeof(obj_item_storage_bag_xeno));
        return dot;
    }
}

// tg: code/modules/clothing/suits/labcoat.dm:174
public partial class obj_item_clothing_suit_toggle_labcoat_research_director : obj_item_clothing_suit_toggle_labcoat
{
    // tg: code/modules/clothing/suits/labcoat.dm:185
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.append(ref allowed, Dm.list(
            typeof(obj_item_storage_bag_xeno),
            typeof(obj_item_melee_baton_telescopic)
        ));
        return dot;
    }
}
