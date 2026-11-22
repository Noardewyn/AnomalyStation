// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/granters/magic/smoke.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SL1 (D333): the whole file (the smoke spellbook and the chaplain's lesser one; the smoke spell is live). The var blocks are
// generated (D111).

// tg: code/game/objects/items/granters/magic/smoke.dm:1
public partial class obj_item_book_granter_action_spell_smoke : obj_item_book_granter_action_spell
{
    // tg: code/game/objects/items/granters/magic/smoke.dm:16
    public override async DmTask recoil(mob_living user = null)
    {
        await base.recoil(user);
        to_chat(user, span_warning(I18n.T("Your stomach rumbles...")));
        if (Dm.truthy(user.nutrition))
        {
            await user.set_nutrition(200);
            if (user.nutrition <= 0)
                await user.set_nutrition(0);
        }
    }
}
