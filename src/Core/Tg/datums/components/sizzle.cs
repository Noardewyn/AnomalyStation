// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/sizzle.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package H2, D333): the grill marks of the grill. The var block is generated (D111).

// tg: code/datums/components/sizzle.dm:1
public partial class datum_component_sizzle : datum_component
{
    // tg: code/datums/components/sizzle.dm:9
    [DmParams("grilled_time")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object grilled_time = Dm.arg(__extra, 0);
        if (!isatom(parent))
            return COMPONENT_INCOMPATIBLE;

        atom food = (atom)parent;
        icon grill_marks = icon(food.icon, food.icon_state);
        grill_marks.Blend("#fff", ICON_ADD); //fills the icon_state with white (except where it's transparent)
        grill_marks.Blend(icon(Dm.resource("icons/obj/machines/kitchen.dmi"), "grillmarks"), ICON_MULTIPLY); //adds grill marks and the remaining white areas become transparent
        sizzling = Dm.@new<mutable_appearance>(grill_marks);
        food.add_overlay(sizzling);

        this.grilled_time = Dm.num(grilled_time);
        return null;
    }

    // tg: code/datums/components/sizzle.dm:22
    [DmParams("C", "i_am_original", "grilled_time")]
    public override async DmTask InheritComponent(datum_component C = null, object i_am_original = null, params object[] __extra)
    {
        object grilled_time = Dm.arg(__extra, 0);
        atom food = (atom)parent;
        sizzling.alpha += 5;
        food.cut_overlay(sizzling);
        food.add_overlay(sizzling);
        this.grilled_time = Dm.num(grilled_time);
    }

    ///Returns how long the food item has been sizzling for
    // tg: code/datums/components/sizzle.dm:30
    public virtual object time_elapsed()
    {
        return this.grilled_time;
    }
}
