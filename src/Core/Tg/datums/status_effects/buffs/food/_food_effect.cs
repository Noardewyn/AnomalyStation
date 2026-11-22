// Translated from tgstation (snapshot 1d99bd7f): code/datums/status_effects/buffs/food/_food_effect.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps S1 (D291): the hand-crafted meal's buff, the whole file; its haste is haste.cs.
// The chilling, trait and speech buffs (chilling.dm, grant_trait.dm, speech.dm) are a food's own crafted_food_buff, none of them
// on a live food yet. The var blocks are generated.

/// Buffs given by eating hand-crafted food. The duration scales with consumable reagents purity.
// tg: code/datums/status_effects/buffs/food/_food_effect.dm:2
public partial class datum_status_effect_food : datum_status_effect
{
    // tg: code/datums/status_effects/buffs/food/_food_effect.dm:11
    [DmParams("new_owner", "timeout_mod", "strength")]
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        object timeout_mod = Dm.arg(__extra, 0) ?? 1;
        object strength = Dm.arg(__extra, 1) ?? 1;
        object dot = await base.on_creation(new_owner, Dm.forward(__extra, timeout_mod, strength));
        this.strength = strength;
        if (Dm.isnum(timeout_mod))
            duration *= Dm.num(timeout_mod);
        if (linked_alert is atom_movable_screen_alert_status_effect_food)
            linked_alert.overlay_state = Dm.text("food_buff_[]", strength);
        return dot;
    }
}
