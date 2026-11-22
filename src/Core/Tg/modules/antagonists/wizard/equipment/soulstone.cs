// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/wizard/equipment/soulstone.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Only the ectoplasm a ghost drops (gaps package C6); the soulstones wait for the wizard and the cult.

// tg: code/modules/antagonists/wizard/equipment/soulstone.dm:587
public partial class obj_item_ectoplasm : obj_item
{
    // tg: code/modules/antagonists/wizard/equipment/soulstone.dm:599
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_hauntium), 25f));
    }

    // tg: code/modules/antagonists/wizard/equipment/soulstone.dm:602
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(Dm.text("[] is inhaling []! It looks like [] trying to visit the astral plane!", user, this, user.p_theyre())));
        return OXYLOSS;
    }
}
