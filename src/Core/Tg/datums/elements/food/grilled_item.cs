// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/food/grilled_item.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package H2, D333): what the barbeque grill does to what it grills.

/// Items grilled through the grill.
// tg: code/datums/elements/food/grilled_item.dm:2
public partial class datum_element_grilled_item : datum_element
{
    // tg: code/datums/elements/food/grilled_item.dm:4
    [DmParams("target", "grill_time")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object grill_time = Dm.arg(__extra, 0);
        object dot = await base.Attach(target, __extra);
        if (!isatom(target))
            return ELEMENT_INCOMPATIBLE;

        atom this_food = (atom)target;

        object sw__1 = grill_time; //no 0-20 to prevent spam
        if (Dm.in_to(sw__1, 20 * SECONDS, 30 * SECONDS))
        {
            this_food.name = I18n.T("lightly-grilled []", this_food.name);
            this_food.desc += I18n.T(" It's been lightly grilled.");
        }
        else if (Dm.in_to(sw__1, 30 * SECONDS, 80 * SECONDS))
        {
            this_food.name = I18n.T("grilled []", this_food.name);
            this_food.desc += I18n.T(" It's been grilled.");
        }
        else if (Dm.in_to(sw__1, 80 * SECONDS, 100 * SECONDS))
        {
            this_food.name = I18n.T("heavily grilled []", this_food.name);
            this_food.desc += I18n.T(" It's been heavily grilled.");
        }
        else if (Dm.in_to(sw__1, 100 * SECONDS, INFINITY)) //grill marks reach max alpha
        {
            this_food.name = I18n.T("Powerfully Grilled []", this_food.name);
            this_food.desc = I18n.T("A []. Reminds you of your wife, wait, no, it's prettier!", this_food.name);
        }

        if (Dm.num(grill_time) > 30 * SECONDS && this_food.GetComponent(typeof(datum_component_edible)) == null)
            await this_food.AddComponentFrom(SOURCE_EDIBLE_GRILLED, typeof(datum_component_edible), Dm.named("foodtypes", FRIED));

        if (Dm.truthy(this_food._listen_lookup?[COMSIG_ITEM_BARBEQUE_GRILLED]))
            this_food._SendSignal(COMSIG_ITEM_BARBEQUE_GRILLED, Dm.list(this_food, grill_time));
        ADD_TRAIT(this_food, TRAIT_FOOD_BBQ_GRILLED, ELEMENT_TRAIT(type));
        return dot;
    }

    // tg: code/datums/elements/food/grilled_item.dm:34
    public override object Detach(datum source = null, params object[] __extra)
    {
        atom source__atom = (atom)source;
        source__atom.name = Dm.initial(source__atom, (atom a) => a.name);
        source__atom.desc = Dm.initial(source__atom, (atom a) => a.desc);
        source.RemoveComponentSource(SOURCE_EDIBLE_GRILLED, typeof(datum_component_edible));
        // tg removes the trait from the element itself (src), not from the source; the source keeps it
        REMOVE_TRAIT(this, TRAIT_FOOD_BBQ_GRILLED, ELEMENT_TRAIT(type));
        return base.Detach(source, __extra);
    }
}
