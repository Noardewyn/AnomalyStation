// Translated from tgstation (snapshot 1d99bd7f): code/modules/hydroponics/beekeeping/honeycomb.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P2 (D298): the whole file. GAP(lead): GLOB.chemical_reagents_list is not built (holder.cs): the reagent's name and colour come
// from its type's prototype, which stands in for the list's entry.

// tg: code/modules/hydroponics/beekeeping/honeycomb.dm:1
public partial class obj_item_food_honeycomb : obj_item_food
{
    // tg: code/modules/hydroponics/beekeeping/honeycomb.dm:13
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        pixel_x = base_pixel_x + rand(-5, 5);
        pixel_y = base_pixel_y + rand(-5, 5);
        return dot;
    }

    // tg: code/modules/hydroponics/beekeeping/honeycomb.dm:18
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        mutable_appearance honey_overlay = mutable_appearance(icon, "honey") as mutable_appearance;
        if (Dm.truthy(honey_color))
        {
            honey_overlay.icon_state = "greyscale_honey";
            honey_overlay.color = honey_color;
            Dm.append(ref dot, honey_overlay);
        }
        return dot;
    }

    // tg: code/modules/hydroponics/beekeeping/honeycomb.dm:26
    public virtual async DmTask set_reagent(object reagent = null)
    {
        // var/datum/reagent/R = GLOB.chemical_reagents_list[reagent]: the type's prototype stands in for the entry (the header's GAP)
        datum_reagent R = ispath(reagent, typeof(datum_reagent)) && reagent as Type != typeof(datum_reagent) ? Dm.initial((Type)reagent, (datum_reagent p) => p) : null;
        if (R != null)
        {
            name = Dm.text("honeycomb ([])", R.name);
            honey_color = R.color as string;
            await ((datum_reagents)reagents).add_reagent(R.type, 5);
        }
        else
        {
            honey_color = "";
        }
        update_appearance();
    }
}
