// Translated from tgstation (snapshot 1d99bd7f): code/modules/photography/camera/other.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (gaps J3, D298): the camera obscura; the detective's camera is var-only. The var blocks are generated.

// tg: code/modules/photography/camera/other.dm:2
public partial class obj_item_camera_spooky : obj_item_camera
{
    // tg: code/modules/photography/camera/other.dm:6
    public override async DmTask steal_souls(DmList victims = null)
    {
        foreach (mob_living target in Dm.each<mob_living>(victims))
        {
            if (!Dm.truthy(Dm.bitand(target.mob_biotypes, MOB_SPIRIT)))
                continue;

            // time to steal your soul
            // tg: istype(target, /mob/living/basic/revenant): the revealed and paralyzed statuses of a revenant, with the antagonist
            if (istype(target, typeof(mob_living_basic_revenant)))
                Dm.Deferred("code/modules/photography/camera/other.dm:12", "steal_souls: the revenant's revealed and paralyzed statuses (the revenant, antagonists)");

            target.visible_message(
                span_warning(I18n.T("[] violently flinches!", target)),
                span_revendanger(I18n.T("You feel your essence draining away from having your picture taken!")));
            await target.apply_damage(rand(10, 15));
        }
    }
}
