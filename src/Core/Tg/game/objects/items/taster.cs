// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/taster.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package OD1, D333): the taster tells what a container's reagents taste of. The var block is generated (D111).

// tg: code/game/objects/items/taster.dm:2
public partial class obj_item_taster : obj_item
{
    // tg: code/game/objects/items/taster.dm:11
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        datum_reagents holder = interacting_with.reagents as datum_reagents;
        if (holder == null)
            to_chat(user, span_notice(I18n.T("[] cannot taste [], since [] [] have no reagents.", this, interacting_with, interacting_with.p_they(), interacting_with.p_have())));
        else if (holder.total_volume == 0)
            to_chat(user, span_notice(I18n.T("[] cannot taste [], since [] [] empty.", this, interacting_with, interacting_with.p_they(), interacting_with.p_are())));
        else
        {
            object message = holder.generate_taste_message(user, taste_sensitivity);
            to_chat(user, span_notice(I18n.T("[] tastes <i>[]</i> in [].", this, message, interacting_with)));
        }
        return Dm.truthy(user.combat_mode) ? NONE : ITEM_INTERACT_SUCCESS;
    }
}
