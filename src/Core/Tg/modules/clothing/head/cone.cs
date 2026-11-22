// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/head/cone.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps CL1 (D333): the whole file (the traffic cone set down upright on the floor; its reflective glow is DV12). The var block is
// generated (D111).

// tg: code/modules/clothing/head/cone.dm:1
public partial class obj_item_clothing_head_cone : obj_item_clothing_head
{
    // tg: code/modules/clothing/head/cone.dm:21
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_floor_placeable)));
        return dot;
    }

    // tg: code/modules/clothing/head/cone.dm:25
    public override object worn_overlays(mutable_appearance standing = null, float? isinhands = null, object icon_file = null, float? bodyshape = null)
    {
        bodyshape ??= NONE;
        object dot = base.worn_overlays(standing, isinhands, icon_file, bodyshape);
        // DEVIATION(DV12): if(!isinhands) . += emissive_appearance(icon_file, "[icon_state]-emissive", src, alpha = src.alpha,
        // effect_type = EMISSIVE_SPECULAR): the reflective stripe's glow on the emissive plane is not created
        return dot;
    }
}
