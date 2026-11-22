// Translated from tgstation (snapshot 1d99bd7f): code/modules/reagents/stacked_effects/bevarages.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M8 slice A; the two mood effects since gaps MS1). The var blocks (the requirements lists) are generated (D111).

// tg: code/modules/reagents/stacked_effects/bevarages.dm:1
[DmPath("/datum/stacked_metabolization_effect/sweet_coffee")]
public partial class datum_stacked_metabolization_effect_sweet_coffee : datum_stacked_metabolization_effect
{
    // tg: code/modules/reagents/stacked_effects/bevarages.dm:7
    public override object apply(DmList reagents_metabolized = null, mob_living_carbon owner = null, object seconds_per_tick = null)
    {
        if (owner.mob_mood == null)
            return null; // tg reads mob_mood unguarded: a moodless carbon's runtime ends apply here (kept, without the runtime, DV1)
        if (!Dm.truthy(owner.mob_mood.mood_events["sweet_coffee"]))
            Waiting.NoSleep(owner.add_mood_event("sweet_coffee", typeof(datum_mood_event_sweetcoffee)));
        return null;
    }
}

// tg: code/modules/reagents/stacked_effects/bevarages.dm:11
[DmPath("/datum/stacked_metabolization_effect/sweet_tea")]
public partial class datum_stacked_metabolization_effect_sweet_tea : datum_stacked_metabolization_effect
{
    // tg: code/modules/reagents/stacked_effects/bevarages.dm:17
    public override object apply(DmList reagents_metabolized = null, mob_living_carbon owner = null, object seconds_per_tick = null)
    {
        if (owner.mob_mood == null)
            return null; // tg reads mob_mood unguarded: a moodless carbon's runtime ends apply here (kept, without the runtime, DV1)
        if (!Dm.truthy(owner.mob_mood.mood_events["sweet_tea"]))
            Waiting.NoSleep(owner.add_mood_event("sweet_tea", typeof(datum_mood_event_sweettea)));
        return null;
    }
}

// tg: code/modules/reagents/stacked_effects/bevarages.dm:21
[DmPath("/datum/stacked_metabolization_effect/coffee_oxidise")]
public partial class datum_stacked_metabolization_effect_coffee_oxidise : datum_stacked_metabolization_effect
{
    // tg: code/modules/reagents/stacked_effects/bevarages.dm:27
    public override object apply(DmList reagents_metabolized = null, mob_living_carbon owner = null, object seconds_per_tick = null)
    {
        if (SPT_PROB(30, seconds_per_tick))
            return Waiting.NoSleep(owner.adjust_oxy_loss(-2 * Dm.num(average(reagents_metabolized)), updating_health: FALSE));
        return null;
    }
}

// tg: code/modules/reagents/stacked_effects/bevarages.dm:31
[DmPath("/datum/stacked_metabolization_effect/coffee_oxidise_triple")]
public partial class datum_stacked_metabolization_effect_coffee_oxidise_triple : datum_stacked_metabolization_effect
{
    // tg: code/modules/reagents/stacked_effects/bevarages.dm:38
    public override object apply(DmList reagents_metabolized = null, mob_living_carbon owner = null, object seconds_per_tick = null)
    {
        if (SPT_PROB(70, seconds_per_tick))
            return Waiting.NoSleep(owner.adjust_oxy_loss(-3 * Dm.num(average(reagents_metabolized)), updating_health: FALSE));
        return null;
    }
}

// tg: code/modules/reagents/stacked_effects/bevarages.dm:42
[DmPath("/datum/stacked_metabolization_effect/astrotame_cream")]
public partial class datum_stacked_metabolization_effect_astrotame_cream : datum_stacked_metabolization_effect
{
    // tg: code/modules/reagents/stacked_effects/bevarages.dm:48
    public override object apply(DmList reagents_metabolized = null, mob_living_carbon owner = null, object seconds_per_tick = null)
    {
        owner.adjust_disgust(-1.5f * Dm.num(average(reagents_metabolized)) * Dm.num(seconds_per_tick));
        return null;
    }
}
