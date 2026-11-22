// Translated from tgstation (snapshot 1d99bd7f): code/modules/vending/sustenance.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps package S2: the sustenance vendor's interact (prisoners and the access holders only). The labor camp's vendor (proceed_payment and
// fetch_balance_to_use, paid with the labor camp's points: the gulag) stays inert, off Meta. The var blocks are generated.

// tg: code/modules/vending/sustenance.dm:2
public partial class obj_machinery_vending_sustenance : obj_machinery_vending
{
    // tg: code/modules/vending/sustenance.dm:29
    public override async DmTask<object> interact(mob user = null, params object[] __extra)
    {
        mob_living living_user = user as mob_living;
        if (!isliving(living_user))
            return null;
        if (!istype(living_user.get_idcard(TRUE), typeof(obj_item_card_id_advanced_prisoner)))
        {
            if (!Dm.truthy(req_access))
            {
                await speak("No valid prisoner account found. Vending is not permitted.");
                return null;
            }
            if (!Dm.truthy(await allowed__proc(living_user)))
            {
                await speak("No valid permissions. Vending is not permitted.");
                return null;
            }
        }
        return await base.interact(user, __extra);
    }
}
