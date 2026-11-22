// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/alien/larva/life.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps X5 (D333): the whole file, the larva's growth.

// tg: code/modules/mob/living/carbon/alien/larva/life.dm:3
public partial class mob_living_carbon_alien_larva : mob_living_carbon_alien
{
    // tg: code/modules/mob/living/carbon/alien/larva/life.dm:3
    public override object Life(object seconds_per_tick = null, params object[] __extra)
    {
        seconds_per_tick ??= SSMOBS_DT();
        if (HAS_TRAIT(this, TRAIT_NO_TRANSFORM))
            return null;
        if (!Dm.truthy(base.Life(seconds_per_tick, __extra)) || HAS_TRAIT(this, TRAIT_STASIS) || (amount_grown >= XENOMORPH_MAX_GROWTH))
            return null; // We're dead, in stasis, or already grown.
        // GROW!
        amount_grown = min(amount_grown + (0.5f * Dm.num(seconds_per_tick)), XENOMORPH_MAX_GROWTH);
        Waiting.NoSleep(update_icons());
        return null;
    }

    // tg: code/modules/mob/living/carbon/alien/larva/life.dm:12
    [SignalHandler]
    public override void on_knockedout_trait_loss(datum source = null)
    {
        base.on_knockedout_trait_loss(source);
        Waiting.NoSleep(set_resting(FALSE));
    }

    // tg: code/modules/mob/living/carbon/alien/larva/life.dm:16
    public override void update_stat()
    {
        if (HAS_TRAIT(this, TRAIT_GODMODE))
            return;
        if (stat != DEAD)
        {
            if (health <= -maxHealth || get_organ_by_type(typeof(obj_item_organ_brain)) == null)
            {
                Waiting.NoSleep(death());
                return;
            }
            Waiting.NoSleep(set_stat(STABLE));
        }
        update_damage_hud();
        update_health_hud();
    }
}
