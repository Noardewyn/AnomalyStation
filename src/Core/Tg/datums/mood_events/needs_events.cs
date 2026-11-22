// Translated from tgstation (snapshot 1d99bd7f): code/datums/mood_events/needs_events.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps MS1 (D333, D339): the whole file. The var blocks are generated (D111).

// tg: code/datums/mood_events/needs_events.dm:72
public partial class datum_mood_event_disgust_dirty_food : datum_mood_event_disgust
{
    // tg: code/datums/mood_events/needs_events.dm:77
    public override async DmTask add_effects(params object[] __extra)
    {
        if (Dm.truthy(LAZYACCESS(owner.personalities, typeof(datum_personality_ascetic)))) // HAS_PERSONALITY (__DEFINES/mood.dm:109)
        {
            mood_change *= 0.25f;
            description = I18n.T("That food was dirty, but edible.");
        }
        if (Dm.truthy(LAZYACCESS(owner.personalities, typeof(datum_personality_gourmand))))
        {
            mood_change *= 1.5f;
            description = I18n.T("That food was filthy, was it made in a dumpster?!");
        }
    }
}

//Generic needs events
// tg: code/datums/mood_events/needs_events.dm:86
public partial class datum_mood_event_shower : datum_mood_event
{
    // tg: code/datums/mood_events/needs_events.dm:91
    [DmParams("shower_reagent")]
    public override async DmTask add_effects(params object[] __extra)
    {
        object shower_reagent = Dm.arg(__extra, 0);
        if (istype(shower_reagent, typeof(datum_reagent_blood)))
        {
            if (HAS_TRAIT(owner, TRAIT_MORBID) || HAS_TRAIT(owner, TRAIT_EVIL) || Dm.truthy(Dm.bitand(owner.mob_biotypes, MOB_UNDEAD)))
            {
                description = I18n.T("The sensation of a lovely blood shower felt good.");
                mood_change = 6; // you sicko
            }
            else
            {
                description = I18n.T("I have recently had a horrible shower raining blood!");
                mood_change = -4;
                timeout = 3 * MINUTES;
            }
        }
        else if (istype(shower_reagent, typeof(datum_reagent_water)))
        {
            if (HAS_TRAIT(owner, TRAIT_WATER_HATER) && !HAS_TRAIT(owner, TRAIT_WATER_ADAPTATION))
            {
                description = I18n.T("I hate being wet!");
                mood_change = -2;
                timeout = 3 * MINUTES;
            }
            else
                return; // just normal clean shower
        }
        else // it's dirty ass water
        {
            description = I18n.T("I have recently had a dirty shower!");
            mood_change = -3;
            timeout = 3 * MINUTES;
        }
    }
}
