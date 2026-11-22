// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/food/love_food_buff.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package H2, D333). The var block is generated (D111).

/// Changes a food item's food buff to something else when it has "love" reagent within
// tg: code/datums/elements/food/love_food_buff.dm:2
public partial class datum_element_love_food_buff : datum_element
{
    // tg: code/datums/elements/food/love_food_buff.dm:8
    [DmParams("target", "love_buff_type")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object love_buff_type = Dm.arg(__extra, 0);
        object dot = await base.Attach(target, __extra);
        if (!istype(target, typeof(obj_item_food)))
            return ELEMENT_INCOMPATIBLE;
        obj_item_food food = (obj_item_food)target;
        if (food.reagents == null)
            return ELEMENT_INCOMPATIBLE;

        this.love_buff_type = love_buff_type;
        RegisterSignal(food.reagents as datum, COMSIG_REAGENTS_HOLDER_UPDATED, nameof(on_reagents_changed));
        RegisterSignal(food, COMSIG_ATOM_EXAMINE, nameof(on_examine));
        return dot;
    }

    // tg: code/datums/elements/food/love_food_buff.dm:20
    public override object Detach(datum source = null, params object[] __extra)
    {
        obj_item_food food = source as obj_item_food;
        if (food != null && food.reagents != null)
        {
            UnregisterSignal(food.reagents as datum, COMSIG_REAGENTS_HOLDER_UPDATED);
            UnregisterSignal(food, COMSIG_ATOM_EXAMINE);
        }
        return base.Detach(source, __extra);
    }

    // tg: code/datums/elements/food/love_food_buff.dm:27
    [SignalHandler]
    public virtual void on_reagents_changed(datum_reagents source = null)
    {
        obj_item_food food = source.my_atom as obj_item_food;
        if (food == null)
            return;

        food.crafted_food_buff = (Dm.truthy(source.has_reagent(typeof(datum_reagent_love))) ? love_buff_type : Dm.initial(food, (obj_item_food f) => f.crafted_food_buff)) as Type;
    }

    // tg: code/datums/elements/food/love_food_buff.dm:36
    [SignalHandler]
    public virtual void on_examine(datum source = null, mob user = null, DmList examine_list = null)
    {
        examine_list.Add(span_notice(I18n.T("Delivering a chef's kiss to [] will alter [] effects.", source, ((atom)source).p_their())));
    }
}
