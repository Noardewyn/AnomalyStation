// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/door_seal.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package OD1, D333): the pneumatic airlock seal's suicide. Sealing an airlock with it is the airlock's own
// item interaction (airlock.dm's seal code, translated with the airlock). The var block is generated (D111).

// tg: code/game/objects/items/door_seal.dm:2
public partial class obj_item_door_seal : obj_item
{
    // tg: code/game/objects/items/door_seal.dm:22
    public override DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] is sealing []self off from the world with []! It looks like [] trying to commit suicide!", user, user.p_them(), this, user.p_theyre())));
        playsound(this, Dm.resource("sound/items/tools/jaws_pry.ogg"), 30, TRUE);
        return DmTask<object>.FromResult(BRUTELOSS);
    }
}
