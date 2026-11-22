// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/gloves/color.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// J1 (D298): the black gloves' Initialize (the radio gloves' slapcrafting recipe). The var blocks are generated.

// tg: code/modules/clothing/gloves/color.dm:2
public partial class obj_item_clothing_gloves_color_black : obj_item_clothing_gloves_color
{
    // tg: code/modules/clothing/gloves/color.dm:13
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // tg: code/modules/clothing/gloves/color.dm:15  var/static/list/slapcraft_recipe_list (a proc-level static, rules 10.6)
        Waiting.NoSleep(AddElement(typeof(datum_element_slapcrafting), Dm.named("slapcraft_recipes", TgState.Current.obj_item_clothing_gloves_color_black__Initialize__slapcraft_recipe_list)));
        return dot;
    }
}

// The static lists of the coloured gloves (rules 10.6: type-level statics live in TgState).
public sealed partial class TgState
{
    // tg: code/modules/clothing/gloves/color.dm:15  var/static/list/slapcraft_recipe_list = list(/datum/crafting_recipe/radio_containing/radiogloves)
    internal DmList obj_item_clothing_gloves_color_black__Initialize__slapcraft_recipe_list = Dm.list(typeof(datum_crafting_recipe_radio_containing_radiogloves));
}
