// Translated from tgstation (snapshot 1d99bd7f): code/modules/hydroponics/grown/cotton.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P2 (D298): the file's procs (the juicing and grinding values, the plants' own uses) The cooking procs (slicing, baking, microwaving, drying) are live-partial rows for the kitchen.

// tg: code/modules/hydroponics/grown/cotton.dm:22
public partial class obj_item_grown_cotton : obj_item_grown
{
    // tg: code/modules/hydroponics/grown/cotton.dm:36
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        float cotton_count = 1;
        if (seed != null)
            cotton_count += Dm.round(Dm.num(((obj_item_seeds)seed).potency) / 25);

        user.balloon_alert(user, I18n.T("pulled [] piece[]", cotton_count, cotton_count == 1 ? "" : "s"));
        Dm.@new(cotton_type, user.drop_location(), cotton_count);
        qdel(this);
        return null;
    }
}
