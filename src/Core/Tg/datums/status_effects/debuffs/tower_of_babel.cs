// Translated from tgstation (snapshot 1d99bd7f): code/datums/status_effects/debuffs/tower_of_babel.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps RG3 (D339): the whole file (the mushroom hallucinogen's overdose babbles in one random language; the wizard's and the
// event's magical curse). The var blocks and the alert are generated.

// tg: code/datums/status_effects/debuffs/tower_of_babel.dm:1
public partial class datum_status_effect_tower_of_babel : datum_status_effect
{
    // tg: code/datums/status_effects/debuffs/tower_of_babel.dm:7
    [DmParams("new_owner", "duration")]
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        object duration = Dm.arg(__extra, 0);
        duration ??= 15 * SECONDS;
        this.duration = Dm.num_or_null(duration);
        if (trait_source == null)
            trait_source = TRAIT_STATUS_EFFECT(id);
        return await base.on_creation(new_owner, Dm.forward(__extra, duration));
    }

    // tg: code/datums/status_effects/debuffs/tower_of_babel.dm:13
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        object random_language = pick(GLOB.all_languages);
        owner.grant_language(random_language, source: LANGUAGE_BABEL);
        // block every language except the randomized one
        owner.add_blocked_language(Dm.sub(GLOB.all_languages, random_language), source: LANGUAGE_BABEL);
        // this lets us bypass tongue language restrictions except for people who have stuff like mute,
        // no tongue, tongue tied, etc. curse of babel shouldn't let people who have a tongue disability speak
        if (owner.mind != null)
            ADD_TRAIT(owner.mind, TRAIT_TOWER_OF_BABEL, trait_source);
        await owner.add_mood_event(id, typeof(datum_mood_event_tower_of_babel));
        return await base.on_apply(__extra);
    }

    // tg: code/datums/status_effects/debuffs/tower_of_babel.dm:25
    public override async DmTask<object> on_remove()
    {
        owner.clear_mood_event(id);
        // if user is affected by tower of babel, we remove the blocked languages
        owner.remove_blocked_language(GLOB.all_languages, source: LANGUAGE_BABEL);
        owner.remove_all_languages(source: LANGUAGE_BABEL);
        if (owner.mind != null)
            REMOVE_TRAIT(owner.mind, TRAIT_TOWER_OF_BABEL, trait_source);
        return await base.on_remove();
    }
}

// Used by wizard magic and tower of babel event
// tg: code/datums/status_effects/debuffs/tower_of_babel.dm:35
public partial class datum_status_effect_tower_of_babel_magical : datum_status_effect_tower_of_babel
{
    // tg: code/datums/status_effects/debuffs/tower_of_babel.dm:40
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        object dot = await base.on_apply(__extra);
        if (!Dm.truthy(dot))
            return dot;

        await owner.emote("mumble");
        owner.playsound_local(get_turf(owner), Dm.resource("sound/effects/magic/magic_block_mind.ogg"), 75, vary: TRUE); // sound of creepy whispers
        to_chat(owner, span_reallybig(span_hypnophrase(I18n.T("You feel a magical force affecting your speech patterns!"))));
        return dot;
    }

    // tg: code/datums/status_effects/debuffs/tower_of_babel.dm:49
    public override async DmTask<object> on_remove()
    {
        object dot = await base.on_remove();
        if (!Dm.truthy(dot))
            return dot;

        to_chat(owner, span_reallybig(span_hypnophrase(I18n.T("You feel the magical force affecting your speech patterns fade away..."))));
        return dot;
    }
}
