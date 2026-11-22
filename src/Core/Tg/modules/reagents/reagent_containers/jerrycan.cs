// Translated from tgstation (snapshot 1d99bd7f): code/modules/reagents/reagent_containers/jerrycan.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package OD1, D333): the plastic jerrycans with their highlight, label and cap overlays, and the reagent filling
// and cap of the one in hand. The var blocks are generated (D111).

// tg: code/modules/reagents/reagent_containers/jerrycan.dm:43
public partial class obj_item_reagent_containers_cup_jerrycan : obj_item_reagent_containers_cup
{
    // tg: code/modules/reagents/reagent_containers/jerrycan.dm:72
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.Deferred("code/modules/reagents/reagent_containers/jerrycan.dm:74", "Initialize: AddElement(/datum/element/cuffable_item) (cuffing an item to a wrist: restraints, M10+)");
        update_appearance();
        return dot;
    }

    // tg: code/modules/reagents/reagent_containers/jerrycan.dm:77
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);

        mutable_appearance highlight = mutable_appearance(icon, Dm.text("[]_highlight", base_icon_state)) as mutable_appearance;
        // highlight.opacity = highlight.opacity * highlight_strenght: an appearance's opacity starts at 0 and an overlay's blocks no
        // sight, so tg's line changes nothing (the shim's image has no opacity var)
        Dm.append(ref dot, highlight);

        if (Dm.truthy(label_type))
            Dm.append(ref dot, mutable_appearance(icon, Dm.text("[]_label_[]", base_icon_state, label_type)));

        if (Dm.truthy(cap_type))
            Dm.append(ref dot, mutable_appearance(icon, Dm.text("[]_cap_[]", base_icon_state, cap_type)));
        return dot;
    }

    // tg: code/modules/reagents/reagent_containers/jerrycan.dm:90
    public override object worn_overlays(mutable_appearance standing = null, float? isinhands = null, object icon_file = null, float? bodyshape = null)
    {
        isinhands ??= FALSE;
        bodyshape ??= NONE;
        object dot = base.worn_overlays(standing, isinhands, icon_file, bodyshape);
        if (!Dm.truthy(isinhands))
            return dot;
        if (Dm.truthy(fill_icon_thresholds) && Dm.truthy(((datum_reagents)reagents).total_volume))
        {
            mutable_appearance inhand_reagent_filling = mutable_appearance(icon_file, Dm.text("[]_reagent_filling", inhand_icon_state)) as mutable_appearance;
            inhand_reagent_filling.color = mix_color_from_reagents(((datum_reagents)reagents).reagent_list);
            Dm.append(ref dot, inhand_reagent_filling);
        }
        if (Dm.truthy(cap_type))
            Dm.append(ref dot, mutable_appearance(icon_file, Dm.text("[]_cap_[]", base_icon_state, cap_type)));
        return dot;
    }
}
