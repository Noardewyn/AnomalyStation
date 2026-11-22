// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/granters/chuuni_granter.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SL1 (D333): the whole file. The var block is generated (D111).

/// Turns the user into a chuunibyou.
// tg: code/game/objects/items/granters/chuuni_granter.dm:2
public partial class obj_item_book_granter_chuunibyou : obj_item_book_granter
{
    // tg: code/game/objects/items/granters/chuuni_granter.dm:17
    public override object can_learn(mob_living user = null)
    {
        if (!isliving(user))
            return null;
        if (Dm.truthy(user.GetComponent(typeof(datum_component_chuunibyou))))
        {
            to_chat(user, span_warning(I18n.T("You're already a chuunibyou!")));
            return null;
        }
        return TRUE;
    }

    // tg: code/game/objects/items/granters/chuuni_granter.dm:25
    public override async DmTask recoil(mob_living user = null)
    {
        to_chat(user, span_warning(I18n.T("You just can't bring yourself to read it... it's just not worth the cringe...")));
        await DmTask.CompletedTask;
    }

    // tg: code/game/objects/items/granters/chuuni_granter.dm:28
    public override async DmTask on_reading_finished(mob_living user = null)
    {
        await base.on_reading_finished(user);
        to_chat(user, span_notice(I18n.T("You've learned how to cast spells in a more chuunibyou-like style!")));
        await user.AddComponent(typeof(datum_component_chuunibyou_no_healing));
    }
}
