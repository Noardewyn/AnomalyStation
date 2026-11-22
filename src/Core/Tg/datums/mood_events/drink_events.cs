// Translated from tgstation (snapshot 1d99bd7f): code/datums/mood_events/drink_events.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps MS1 (D333, D339): the whole file. The var blocks are generated (D111).

// tg: code/datums/mood_events/drink_events.dm:1
public partial class datum_mood_event_drunk : datum_mood_event
{
    // tg: code/datums/mood_events/drink_events.dm:7
    [DmParams("drunkness")]
    public override async DmTask add_effects(params object[] __extra)
    {
        object drunkness = Dm.arg(__extra, 0);
        update_change(Dm.num_or_null(drunkness));
        if (!ishuman(owner))
            return;
        mob_living_carbon_human human_owner = owner as mob_living_carbon_human;
        blush_overlay = await human_owner.give_emote_overlay(typeof(datum_bodypart_overlay_simple_emote_blush)) as datum_bodypart_overlay_simple_emote;
    }

    /// Updates the description and value of the moodlet according to the passed drunkness value
    /// (Does not add to or remove from the current level - it will sets it directly to the new value)
    // tg: code/datums/mood_events/drink_events.dm:16
    public virtual void update_change(float? drunkness = null)
    {
        drunkness ??= 0;
        float old_mood = mood_change;
        float sw__1 = Dm.num(drunkness);
        if (sw__1 >= 0 && sw__1 <= 30)
        {
            mood_change = 3;
            description = I18n.T("Everything just feels better after a drink or two.");
        }
        else if (sw__1 >= 30 && sw__1 <= 45)
        {
            mood_change = 4;
            description = I18n.T("Is it getting hotter, or is it just me? I need another drink to cool down.");
        }
        else if (sw__1 >= 45 && sw__1 <= 60)
        {
            mood_change = 5;
            description = I18n.T("Who keeps moving the floor? I'm going to talk to them... after this drink.");
        }
        else if (sw__1 >= 60 && sw__1 <= 90)
        {
            mood_change = 6;
            description = I18n.T("I'm noooot drunk, you're drunk! In fact... I need another drink!");
        }
        else if (sw__1 >= 90)
        {
            mood_change = 3; // crash out
            description = I18n.T("You're my BESSST frien'... You and me agains' th' world, buddy. Le's get another drink.");
        }
        if (Dm.truthy(LAZYACCESS(owner.personalities, typeof(datum_personality_teetotal)))) // HAS_PERSONALITY (__DEFINES/mood.dm:109)
        {
            mood_change *= -1.5f;
            description = I18n.T("I don't like drinking... It makes me feel horrible.");
        }
        if (Dm.truthy(LAZYACCESS(owner.personalities, typeof(datum_personality_bibulous))))
            mood_change *= 1.5f;
        if (old_mood != mood_change)
            owner.mob_mood.update_mood();
    }

    // tg: code/datums/mood_events/drink_events.dm:42
    public override void remove_effects()
    {
        QDEL_NULL(ref blush_overlay);
    }
}
