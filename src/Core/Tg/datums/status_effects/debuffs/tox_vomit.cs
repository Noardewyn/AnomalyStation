// Translated from tgstation (snapshot 1d99bd7f): code/datums/status_effects/debuffs/tox_vomit.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Translated in M2 by P2 (the human's adjust_tox_loss applies it; scope.md 2e "human"). In the status effects' area
// (datums/status_effects/**, M2 P1): a new file, so it merges without a conflict.

/// Simple status effect applied when a mob has high toxins and starts to vomit regularly
// tg: code/datums/status_effects/debuffs/tox_vomit.dm:2
[DmPath("/datum/status_effect/tox_vomit")]
public partial class datum_status_effect_tox_vomit : datum_status_effect
{
    // tg: code/datums/status_effects/debuffs/tox_vomit.dm:3-5  the var overrides of the type block (rules 10.2)
    public datum_status_effect_tox_vomit()
    {
        id = "vomitting_from_toxins";
        tick_interval = 2 * SECONDS;
        alert_type = null;
    }

    /// Has a chance to count up every tick, until it reaches a threshold, which causes the mob to vomit and resets
    // tg: code/datums/status_effects/debuffs/tox_vomit.dm:7  VAR_PRIVATE/puke_counter = 0
    private float puke_counter = 0;

    // tg: code/datums/status_effects/debuffs/tox_vomit.dm:9
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        if (!AT_TOXIN_VOMIT_THRESHOLD(owner))
        {
            qdel(this);
            return null;
        }

        if (owner.stat == DEAD || HAS_TRAIT(owner, TRAIT_STASIS))
            return null;

        puke_counter += Dm.num(SPT_PROB(30, seconds_between_ticks));
        if (puke_counter < 50) // This is like 150 seconds apparently according to old comments
            return null;

        mob_living_carbon_human sick_guy = owner as mob_living_carbon_human;
        await sick_guy.vomit(VOMIT_CATEGORY_DEFAULT, lost_nutrition: 20);
        puke_counter = 0;
        return null;
    }
}
