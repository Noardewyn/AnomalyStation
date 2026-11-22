// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/suits/ghostsheet.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (gaps CL1, D333): the ghost sheet's procs; its Halloween glow is DV12 and its fishing difficulty waits for fishing
// (not-translated.md 209). The spooky ghost sheet is var-only. The var blocks are generated (D111).

// tg: code/modules/clothing/suits/ghostsheet.dm:1
public partial class obj_item_clothing_suit_costume_ghost_sheet : obj_item_clothing_suit_costume
{
    // tg: code/modules/clothing/suits/ghostsheet.dm:15
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(check_holidays(HALLOWEEN)))
            Waiting.NoSleep(update_icon(UPDATE_OVERLAYS));
        Dm.Deferred("code/modules/clothing/suits/ghostsheet.dm:19", "Initialize: AddElement(/datum/element/adjust_fishing_difficulty, 8): fishing (M10+)");
        return dot;
    }

    // tg: code/modules/clothing/suits/ghostsheet.dm:21
    public override object worn_overlays(mutable_appearance standing = null, float? isinhands = null, object icon_file = null, float? bodyshape = null)
    {
        bodyshape ??= NONE;
        object dot = base.worn_overlays(standing, isinhands, icon_file, bodyshape);
        // DEVIATION(DV12): if(!isinhands && check_holidays(HALLOWEEN)) . += emissive_appearance('icons/mob/simple/mob.dmi', "ghost",
        // offset_spokesman = src, alpha = src.alpha): the Halloween ghost glow on the emissive plane is not created
        return dot;
    }
}
