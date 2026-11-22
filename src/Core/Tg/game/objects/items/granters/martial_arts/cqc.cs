// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/granters/martial_arts/cqc.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign X3b (D316): the CQC manual, whole. The var block is generated (D111).

// tg: code/game/objects/items/granters/martial_arts/cqc.dm:1
public partial class obj_item_book_granter_martial_cqc : obj_item_book_granter_martial
{
    // tg: code/game/objects/items/granters/martial_arts/cqc.dm:18
    public override async DmTask on_reading_finished(mob_living user = null)
    {
        await base.on_reading_finished(user);
        if (uses <= 0)
            to_chat(user, span_warning(I18n.T("[] beeps ominously...", this)));
    }

    // tg: code/game/objects/items/granters/martial_arts/cqc.dm:23
    public override async DmTask recoil(mob_living user = null)
    {
        to_chat(user, span_warning(I18n.T("[] explodes!", this)));
        playsound(this, "sound/effects/explosion/explosion1.ogg", 40, TRUE);
        await user.flash_act(1, 1);
        await user.adjust_brute_loss(6);
        await user.adjust_fire_loss(6);
        qdel(this);
    }
}
