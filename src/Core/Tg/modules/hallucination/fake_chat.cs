// Translated from tgstation (snapshot 1d99bd7f): code/modules/hallucination/fake_chat.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8 slice G (D187), the languages and runechat M9 slice A. The fake line goes to the hallucinator's chat alone; runechat reads the
// enable_runechat default (DV161).
// The radio branch needs the crew manifest (get_crewmember_minds, a marked leaf). The var block is generated (D111).

/// Sends a fake chat message to the hallucinator.
// tg: code/modules/hallucination/fake_chat.dm:2
public partial class datum_hallucination_chat : datum_hallucination
{
    // tg: code/modules/hallucination/fake_chat.dm:11
    [DmParams("hallucinator", "force_radio", "specific_message")]
    public override async DmTask New(params object[] __extra)
    {
        object force_radio = Dm.arg(__extra, 1) ?? FALSE;
        object specific_message = Dm.arg(__extra, 2);
        this.force_radio = force_radio;
        this.specific_message = specific_message;
        await base.New(__extra);
    }

    /// When passed a mob, returns a list of languages that mob could theoretically speak IF a blank slate.
    // tg: code/modules/hallucination/fake_chat.dm:17
    public virtual object get_hallucinating_spoken_languages(atom_movable who = null)
    {
        Type override_typepath = null;
        if (iscarbon(who))
        {
            mob_living_carbon human_who = (mob_living_carbon)who;
            override_typepath = (human_who.dna?.species as datum_species)?.species_language_holder;
        }

        datum_language_holder what_they_speak = GLOB.prototype_language_holders[override_typepath ?? who.initial_language_holder] as datum_language_holder;
        return what_they_speak?.spoken_languages?.Copy() ?? Dm.list();
    }

    // tg: code/modules/hallucination/fake_chat.dm:25
    public override async DmTask<object> start()
    {
        if (IS_UNCONSCIOUS(hallucinator))
            return FALSE;

        mob speaker = null;
        DmList understood_languages = hallucinator.get_language_holder().understood_languages;
        object understood_language = null;

        if (!Dm.truthy(force_radio))
        {
            DmList valid_humans = Dm.list();
            DmList valid_corpses = Dm.list();
            foreach (mob_living_carbon nearby_human in Dm.each<mob_living_carbon>(view(hallucinator)))
            {
                if (nearby_human == hallucinator)
                    continue;
                if (nearby_human.stat == DEAD)
                {
                    Dm.append(valid_corpses, nearby_human);
                    continue;
                }
                Dm.append(valid_humans, nearby_human);
            }

            // pick a nearby human which can speak a language the hallucinator understands
            foreach (mob_living_carbon nearby_human in Dm.each<mob_living_carbon>(shuffle(valid_humans)))
            {
                DmList shared_languages = Dm.bitand(get_hallucinating_spoken_languages(nearby_human), understood_languages) as DmList;
                if (length(shared_languages) == 0) // future idea : have people hallucinating off their minds ignore this check
                    continue;
                speaker = nearby_human;
                understood_language = pick(shared_languages);
                break;
            }

            // corpses disrespect language because they're... dead
            if (speaker == null && length(valid_corpses) != 0)
                speaker = pick(valid_corpses) as mob;
        }

        // Get person to affect if radio hallucination
        bool is_radio = Dm.truthy(force_radio) || speaker == null;
        if (is_radio)
        {
            foreach (datum_mind crew_mind in Dm.each_cast<datum_mind>(shuffle(get_crewmember_minds() as DmList)))
            {
                if (crew_mind == hallucinator.mind || crew_mind.current == null)
                    continue;
                DmList shared_languages = Dm.bitand(get_hallucinating_spoken_languages(crew_mind.current), understood_languages) as DmList;
                if (length(shared_languages) == 0)
                    continue;
                speaker = crew_mind.current;
                understood_language = pick(shared_languages);
                break;
            }
        }

        if (speaker == null)
            return null;

        // Time to generate a message.
        // Spans of our message
        DmList spans = Dm.list(speaker.speech_span);

        // Contents of our message
        object chosen = specific_message;
        // If we didn't have a preset one, let's make one up.
        if (!Dm.truthy(chosen))
        {
            if (is_radio)
            {
                chosen = pick(Dm.list("Help!",
                    Dm.text("Help [][]", pick_list_replacements(HALLUCINATION_FILE, "location"), prob(50) ? "!" : "!!"),
                    Dm.text("[] is []!", pick_list_replacements(HALLUCINATION_FILE, "people"), pick_list_replacements(HALLUCINATION_FILE, "accusations")),
                    Dm.text("[] has []!", pick_list_replacements(HALLUCINATION_FILE, "people"), pick_list_replacements(HALLUCINATION_FILE, "contraband")),
                    Dm.text("[] in [][]", pick_list_replacements(HALLUCINATION_FILE, "threat"), pick_list_replacements(HALLUCINATION_FILE, "location"), prob(50) ? "!" : "!!"),
                    Dm.text("[]", pick(Dm.text("Where's []?", first_name(hallucinator.name)), Dm.text("Set [] to arrest!", first_name(hallucinator.name)))),
                    Dm.text("[]all the shuttle!", pick("C", "Ai, c", "Someone c", "Rec")),
                    Dm.text("AI []!!", pick("rogue", "is dead")),
                    "Borgs rogue!"));
            }
            else
            {
                chosen = pick(Dm.list(Dm.text("[]", pick_list_replacements(HALLUCINATION_FILE, "suspicion")),
                    Dm.text("[]", pick_list_replacements(HALLUCINATION_FILE, "conversation")),
                    Dm.text("[][]!", pick_list_replacements(HALLUCINATION_FILE, "greetings"), first_name(hallucinator.name)),
                    Dm.text("[]", pick_list_replacements(HALLUCINATION_FILE, "getout")),
                    Dm.text("[]", pick_list_replacements(HALLUCINATION_FILE, "weird")),
                    Dm.text("[]", pick_list_replacements(HALLUCINATION_FILE, "didyouhearthat")),
                    Dm.text("[]", pick_list_replacements(HALLUCINATION_FILE, "doubt")),
                    Dm.text("[]", pick_list_replacements(HALLUCINATION_FILE, "aggressive")),
                    Dm.text("[]!!", pick_list_replacements(HALLUCINATION_FILE, "help")),
                    Dm.text("[]", pick_list_replacements(HALLUCINATION_FILE, "escape")),
                    Dm.text("I'm infected, []!", pick_list_replacements(HALLUCINATION_FILE, "infection_advice"))));
            }

            chosen = capitalize(chosen);
        }

        chosen = replacetext(chosen, "%TARGETNAME%", first_name(hallucinator.name));

        // Log the message
        feedback_details += Dm.text("Type: [], Source: [], Message: []", is_radio ? "Radio" : "Talk", speaker.real_name, chosen);

        object plus_runechat = safe_read_pref(hallucinator.client, typeof(datum_preference_toggle_enable_runechat)); // hallucinator.client?.prefs: DV161

        // Display the message
        if (!is_radio && !Dm.truthy(plus_runechat))
        {
            image speech_overlay = image(Dm.resource("icons/mob/effects/talk.dmi"), speaker, "default0", ABOVE_MOB_LAYER);
            Waiting.DetachSync(() => flick_overlay_global(speech_overlay, Dm.list(hallucinator.client), 30));
        }

        if (Dm.truthy(plus_runechat))
            hallucinator.create_chat_message(speaker, understood_language, chosen, spans);

        // And actually show them the message, for real.
        object message = await hallucinator.compose_message(speaker, understood_language, chosen, is_radio ? Dm.text("[]", FREQ_COMMON) : null, is_radio ? RADIO_CHANNEL_COMMON : null, is_radio ? RADIO_COLOR_COMMON : null, spans, null, TRUE);
        to_chat(hallucinator, message);

        // Then clean up.
        qdel(this);
        return TRUE;
    }
}
