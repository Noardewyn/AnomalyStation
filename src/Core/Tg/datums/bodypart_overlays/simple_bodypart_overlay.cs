// Translated from tgstation (snapshot 1d99bd7f): code/datums/bodypart_overlays/simple_bodypart_overlay.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M9 slice B, the emotes' overlays): /datum/bodypart_overlay/simple's own procs; the file's subtypes (the sixpack, the bags,
// the golem and species overlays) come with what adds them.

///For simple overlays that really dont need to be complicated. Sometimes icon_state and icon is enough
///Remember to set the layers or shit wont work
// tg: code/datums/bodypart_overlays/simple_bodypart_overlay.dm:3
public partial class datum_bodypart_overlay_simple : datum_bodypart_overlay
{
    // tg: code/datums/bodypart_overlays/simple_bodypart_overlay.dm:11
    public override object get_image(obj_item_bodypart limb = null, object layer_index = null, object layer_real = null)
    {
        object iconstate_to_use = Dm.text("[][]", icon_state, Dm.truthy(layer_index) ? Dm.text("_[]", layer_index) : "");
        return mutable_appearance(icon, Dm.text("[]", iconstate_to_use), layer: Dm.num(layer_real));
    }

    // tg: code/datums/bodypart_overlays/simple_bodypart_overlay.dm:15
    public override async DmTask color_image(image overlay = null, obj_item_bodypart limb = null, object layer_index = null)
    {
        overlay.color = draw_color;
    }

    // tg: code/datums/bodypart_overlays/simple_bodypart_overlay.dm:18
    public override object icon_render_key(obj_item_bodypart limb = null)
    {
        object dot = base.icon_render_key(limb);
        Dm.append(ref dot, icon_state);
        return dot;
    }
}
