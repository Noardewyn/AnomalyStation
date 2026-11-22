// Translated from tgstation (snapshot 1d99bd7f): code/datums/mood_events/generic_negative_events.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps MS1 (D333, D339): the whole file. The var blocks are generated (D111); suffocation, vomit and vomitself keep the [DmPath] of
// their M2 skeleton.

// tg: code/datums/mood_events/generic_negative_events.dm:10
public partial class datum_mood_event_on_fire : datum_mood_event
{
    // tg: code/datums/mood_events/generic_negative_events.dm:15
    public override async DmTask add_effects(params object[] __extra)
    {
        if (Dm.truthy(owner.has_quirk(typeof(datum_quirk_pyromania))))
        {
            mood_change *= 0.25f;
            description = I18n.T("This might be a bit too much fire.");
        }
        else if (Dm.truthy(owner.has_quirk(typeof(datum_quirk_pyrophobia))))
            mood_change *= 1.5f;
    }
}

// tg: code/datums/mood_events/generic_negative_events.dm:22
[DmPath("/datum/mood_event/suffocation")]
public partial class datum_mood_event_suffocation : datum_mood_event
{
}

// tg: code/datums/mood_events/generic_negative_events.dm:82
public partial class datum_mood_event_dismembered : datum_mood_event
{
    // tg: code/datums/mood_events/generic_negative_events.dm:87
    [DmParams("limb")]
    public override async DmTask add_effects(params object[] __extra)
    {
        obj_item_bodypart limb = Dm.arg<obj_item_bodypart>(__extra, 0);
        if (Dm.truthy(limb))
            description = I18n.T("AHH! MY []! I WAS USING THAT!", uppertext(limb.plaintext_zone));
    }
}

// tg: code/datums/mood_events/generic_negative_events.dm:91
public partial class datum_mood_event_reattachment : datum_mood_event
{
    // tg: code/datums/mood_events/generic_negative_events.dm:97
    [DmParams("limb")]
    public override async DmTask add_effects(params object[] __extra)
    {
        obj_item_bodypart limb = Dm.arg<obj_item_bodypart>(__extra, 0);
        if (Dm.truthy(limb))
            description = I18n.T("Ouch! My [] feels like I fell asleep on it.", limb.plaintext_zone);
    }
}

// tg: code/datums/mood_events/generic_negative_events.dm:110
public partial class datum_mood_event_table : datum_mood_event
{
    // tg: code/datums/mood_events/generic_negative_events.dm:115
    [DmParams()]
    public override async DmTask add_effects(params object[] __extra)
    {
        if (HAS_TRAIT(owner, TRAIT_CATLIKE_INSTINCT)) //Holy snowflake batman!
        {
            mob_living_carbon_human feline = (mob_living_carbon_human)owner;
            await feline.wag_tail(3 * SECONDS);
            description = I18n.T("They want to play on the table!");
            mood_change = 2;
        }
    }
}

// tg: code/datums/mood_events/generic_negative_events.dm:122
public partial class datum_mood_event_table_limbsmash : datum_mood_event
{
    // tg: code/datums/mood_events/generic_negative_events.dm:128
    [DmParams("banged_limb")]
    public override async DmTask add_effects(params object[] __extra)
    {
        obj_item_bodypart banged_limb = Dm.arg<obj_item_bodypart>(__extra, 0);
        if (Dm.truthy(banged_limb))
            description = I18n.T("My fucking [], man that hurts...", banged_limb.plaintext_zone);
    }
}

// tg: code/datums/mood_events/generic_negative_events.dm:132
public partial class datum_mood_event_brain_damage : datum_mood_event
{
    // tg: code/datums/mood_events/generic_negative_events.dm:135
    [DmParams()]
    public override async DmTask add_effects(params object[] __extra)
    {
        object damage_message = pick_list_replacements(BRAIN_DAMAGE_FILE, "brain_damage");
        description = I18n.T("Hurr durr... []", damage_message);
    }
}

// tg: code/datums/mood_events/generic_negative_events.dm:180
public partial class datum_mood_event_jittery : datum_mood_event
{
    // tg: code/datums/mood_events/generic_negative_events.dm:184
    public override async DmTask add_effects(params object[] __extra)
    {
        if (Dm.truthy(HAS_PERSONALITY(owner, typeof(datum_personality_paranoid))))
            mood_change -= 1;
    }
}

// tg: code/datums/mood_events/generic_negative_events.dm:193
[DmPath("/datum/mood_event/vomit")]
public partial class datum_mood_event_vomit : datum_mood_event
{
}

// tg: code/datums/mood_events/generic_negative_events.dm:198
[DmPath("/datum/mood_event/vomitself")]
public partial class datum_mood_event_vomitself : datum_mood_event
{
}

// tg: code/datums/mood_events/generic_negative_events.dm:233
public partial class datum_mood_event_notcreepingsevere : datum_mood_event
{
    // tg: code/datums/mood_events/generic_negative_events.dm:238
    [DmParams("name")]
    public override async DmTask add_effects(params object[] __extra)
    {
        object name = Dm.arg(__extra, 0);
        DmList unstable = Dm.list(name);
        for (float i = 1, i__end = rand(3, 5); i <= i__end; i++)
            Dm.append(ref unstable, copytext_char(name, -1));
        string unhinged = uppertext(unstable.Join("")); //example Tinea Luxor > TINEA LUXORRRR (with randomness in how long that slur is)
        description = I18n.T("THEY NEEEEEEED []!!", unhinged);
    }
}

// tg: code/datums/mood_events/generic_negative_events.dm:261
public partial class datum_mood_event_artbad : datum_mood_event
{
    // tg: code/datums/mood_events/generic_negative_events.dm:267
    [DmParams()]
    public override async DmTask add_effects(params object[] __extra)
    {
        if (Dm.truthy(HAS_PERSONALITY(owner, typeof(datum_personality_creative))))
        {
            mood_change = 0;
            description = I18n.T("Everyone has to start their art journey somewhere!");
        }
    }
}

// tg: code/datums/mood_events/generic_negative_events.dm:307
public partial class datum_mood_event_too_slow : datum_mood_event
{
    // tg: code/datums/mood_events/generic_negative_events.dm:312
    [DmParams("param")]
    public override async DmTask add_effects(params object[] __extra)
    {
        float people_laughing_at_you = 1; // start with 1 in case they're on the same tile or something
        foreach (mob_living_carbon iter_carbon in Dm.each<mob_living_carbon>(oview(owner, 7)))
        {
            if (!IS_UNCONSCIOUS_OR_CRIT(iter_carbon))
            {
                people_laughing_at_you++;
                if (people_laughing_at_you > 7)
                    break;
            }
        }

        mood_change *= people_laughing_at_you;
        await base.add_effects(__extra);
    }
}

// tg: code/datums/mood_events/generic_negative_events.dm:449
public partial class datum_mood_event_russian_roulette_lose : datum_mood_event
{
    // tg: code/datums/mood_events/generic_negative_events.dm:454
    [DmParams()]
    public override async DmTask add_effects(params object[] __extra)
    {
        if (Dm.truthy(HAS_PERSONALITY(owner, typeof(datum_personality_gambler))))
        {
            mood_change *= 0.5f;
            description = I18n.T("I gambled my life and lost! Truth is, the game was rigged from the start...");
            return;
        }
    }
}

// tg: code/datums/mood_events/generic_negative_events.dm:475
public partial class datum_mood_event_body_purist : datum_mood_event
{
    // tg: code/datums/mood_events/generic_negative_events.dm:478
    [DmParams("power")]
    public override async DmTask add_effects(params object[] __extra)
    {
        object power = Dm.arg(__extra, 0);
        mood_change = Dm.num(power);
    }
}

// tg: code/datums/mood_events/generic_negative_events.dm:485
public partial class datum_mood_event_moon_insanity : datum_mood_event
{
    // tg: code/datums/mood_events/generic_negative_events.dm:491
    [DmParams()]
    public override async DmTask add_effects(params object[] __extra)
    {
        if (Dm.truthy(HAS_PERSONALITY(owner, typeof(datum_personality_spiritual))))
            mood_change *= 2;
    }
}

// tg: code/datums/mood_events/generic_negative_events.dm:529
public partial class datum_mood_event_bapped : datum_mood_event
{
    // tg: code/datums/mood_events/generic_negative_events.dm:534
    [DmParams()]
    public override async DmTask add_effects(params object[] __extra)
    {
        // Felinids apparently hate being hit over the head with cardboard
        if (HAS_TRAIT(owner, TRAIT_CATLIKE_INSTINCT))
            mood_change = -2;
    }
}

// tg: code/datums/mood_events/generic_negative_events.dm:544
public partial class datum_mood_event_smoke_in_face : datum_mood_event
{
    // tg: code/datums/mood_events/generic_negative_events.dm:549
    [DmParams("param")]
    public override async DmTask add_effects(params object[] __extra)
    {
        if (HAS_TRAIT(owner, TRAIT_ANOSMIA))
        {
            description = I18n.T("Cigarette smoke is unpleasant.");
            mood_change = -1;
        }
        if (HAS_TRAIT(owner, TRAIT_SMOKER))
        {
            description = I18n.T("Blowing smoke in my face, really?");
            mood_change = 0;
        }
    }
}

// tg: code/datums/mood_events/generic_negative_events.dm:557
public partial class datum_mood_event_slots_loss : datum_mood_event_slots
{
    // tg: code/datums/mood_events/generic_negative_events.dm:563
    [DmParams()]
    public override async DmTask add_effects(params object[] __extra)
    {
        if (Dm.truthy(HAS_PERSONALITY(owner, typeof(datum_personality_gambler))))
        {
            mood_change = 0;
            description = I18n.T("Aww dang it.");
        }
        if (Dm.truthy(HAS_PERSONALITY(owner, typeof(datum_personality_industrious))) || Dm.truthy(HAS_PERSONALITY(owner, typeof(datum_personality_slacking_diligent))))
            mood_change *= 1.5f;
    }
}

// tg: code/datums/mood_events/generic_negative_events.dm:628
public partial class datum_mood_event_splattered_with_blood : datum_mood_event
{
    // tg: code/datums/mood_events/generic_negative_events.dm:633
    public override async DmTask add_effects(params object[] __extra)
    {
        if (HAS_TRAIT(owner, TRAIT_CULT_HALO))
        {
            mood_change = 2;
            description = I18n.T("Blood, blood! The Geometer will be pleased.");
            return;
        }
        if (HAS_TRAIT(owner, TRAIT_MORBID) || HAS_TRAIT(owner, TRAIT_EVIL) || HAS_TRAIT(owner, TRAIT_DRINKS_BLOOD))
        {
            mood_change = 0;
            description = I18n.T("I just got coated in blood. Fascinating!");
            return;
        }
        if (IS_DESENSITIZED(owner))
            mood_change *= 0.5f;
    }
}
