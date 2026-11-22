// Translated from tgstation (snapshot 1d99bd7f): code/datums/mood_events/food_events.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps MS1 (D333, D339): the whole file. The var blocks are generated (D111).

// tg: code/datums/mood_events/food_events.dm:31
public partial class datum_mood_event_food : datum_mood_event
{
    // tg: code/datums/mood_events/food_events.dm:35
    [DmParams("quality", "timeout_mod")]
    public override async DmTask add_effects(params object[] __extra)
    {
        object quality = Dm.arg(__extra, 0);
        object timeout_mod = Dm.arg(__extra, 1);
        quality ??= (float)FOOD_QUALITY_NORMAL;
        timeout_mod ??= 1f;
        mood_change = Dm.num(calculate_mood_change(quality));
        timeout = Dm.num(Dm.mul(timeout, timeout_mod));
        description = I18n.T("That food was [].", GLOB.food_quality_description[quality]);
    }

    // tg: code/datums/mood_events/food_events.dm:40
    [DmParams("home", "quality", "timeout_mod")]
    public override object be_refreshed(datum_mood home = null, params object[] __extra)
    {
        object quality = Dm.arg(__extra, 0);
        object timeout_mod = Dm.arg(__extra, 1);
        float old_mood = mood_change;
        // updates timeout (which is handled in parent call) and mood
        timeout = max(Dm.num(timeout), Dm.num(Dm.mul(Dm.initial(this, (datum_mood_event p) => p.timeout), timeout_mod)));
        mood_change = max(mood_change, Dm.num(calculate_mood_change(quality)));
        // if mood_change is the same, we don't need to update the description
        if (old_mood != mood_change)
            description = I18n.T("That food was [].", GLOB.food_quality_description[quality]);
        return base.be_refreshed(home, Dm.forward(__extra, quality, timeout_mod));
    }

    // tg: code/datums/mood_events/food_events.dm:50
    public virtual object calculate_mood_change(object base_quality = null)
    {
        float quality = 1 + 1.5f * Dm.num(base_quality);
        if (Dm.truthy(LAZYACCESS(owner.personalities, typeof(datum_personality_ascetic)))) // HAS_PERSONALITY (__DEFINES/mood.dm:109)
            quality *= 0.5f;
        if (Dm.truthy(LAZYACCESS(owner.personalities, typeof(datum_personality_gourmand))))
        {
            if (quality <= FOOD_QUALITY_GOOD)
                quality = FOOD_QUALITY_NORMAL;
        }
        return ceil(quality);
    }
}
