// Translated from tgstation (snapshot 1d99bd7f): code/modules/fishing/fish/types/anadromous.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the fish that live in both waters (the sockeye salmon, the arctic char, the pike and the armored pike). The var
// blocks are generated (D111).

// tg: code/modules/fishing/fish/types/anadromous.dm:1
public partial class obj_item_fish_sockeye_salmon : obj_item_fish
{
    // tg: code/modules/fishing/fish/types/anadromous.dm:15
    public override object get_base_edible_reagents_to_add()
    {
        DmList return_list = base.get_base_edible_reagents_to_add() as DmList;
        return_list[typeof(datum_reagent_consumable_nutriment_fat)] = 1;
        return return_list;
    }
}

// tg: code/modules/fishing/fish/types/anadromous.dm:35
public partial class obj_item_fish_pike : obj_item_fish
{
    // tg: code/modules/fishing/fish/types/anadromous.dm:69
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_FISH_SHOULD_TWOHANDED, INNATE_TRAIT);
        return dot;
    }
}
