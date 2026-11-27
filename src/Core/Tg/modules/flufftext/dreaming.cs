// Translated from tgstation (snapshot 1d99bd7f): code/modules/flufftext/dreaming.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: a sleeping carbon's dreams (the fragments of a dream shown one by one in the chat), the list of dreams and the
// random and sound dreams. The var blocks are generated (D111).

// tg: code/modules/flufftext/dreaming.dm:10
public partial class mob_living_carbon
{
    // tg: code/modules/flufftext/dreaming.dm:1  #define DREAMING_SOURCE "dreaming_source"
    private const string DREAMING_SOURCE = "dreaming_source";

    /**
     * Begins the dreaming process on a sleeping carbon.
     *
     * Checks a 10% chance and whether or not the carbon this is called on is already dreaming. If
     * the prob() passes and there are no dream images left to display, a new dream is constructed.
     */
    // tg: code/modules/flufftext/dreaming.dm:10
    public virtual void handle_dreams()
    {
        if (!HAS_TRAIT(this, TRAIT_DREAMING) && prob(10))
            dream();
    }

    /**
     * Generates a dream sequence to be displayed to the sleeper.
     *
     * Generates the "dream" to display to the sleeper. A dream consists of a subject, a verb, and (most of the time) an object, displayed in sequence to the sleeper.
     * Dreams are generated as a list of strings stored inside dream_fragments, which is passed to and displayed in dream_sequence().
     * Bedsheets on the sleeper will provide a custom subject for the dream, pulled from the dream_messages on each bedsheet.
     */
    // tg: code/modules/flufftext/dreaming.dm:22  (set waitfor = FALSE: the wrapper of waiting-model.md 5.4)
    public virtual void dream()
    {
        Waiting.WaitforFalse(dream__body());
    }

    private async DmTask dream__body()
    {
        DmList dream_pool = Dm.list();

        if (Dm.truthy(this._listen_lookup?[COMSIG_PRE_DREAMING]))
            this._SendSignal(COMSIG_PRE_DREAMING, Dm.list(this, dream_pool));
        if (!Dm.truthy(Dm.length(dream_pool)))
            dream_pool = GLOB.dreams;

        datum_dream chosen_dream = pick_weight(dream_pool) as datum_dream;

        ADD_TRAIT(this, TRAIT_DREAMING, DREAMING_SOURCE);
        if (Dm.truthy(this._listen_lookup?[COMSIG_START_DREAMING]))
            this._SendSignal(COMSIG_START_DREAMING, Dm.list(this, chosen_dream));
        await dream_sequence(chosen_dream.GenerateDream(this) as DmList, chosen_dream);
    }

    /**
     * Displays the passed list of dream fragments to a sleeping carbon.
     *
     * Displays the first string of the passed dream fragments, then either ends the dream sequence
     * or performs a callback on itself depending on if there are any remaining dream fragments to display.
     *
     * Arguments:
     * * dream_fragments - A list of strings, in the order they will be displayed.
     * * current_dream - The dream datum used for the current dream
     */
    // tg: code/modules/flufftext/dreaming.dm:48
    public virtual async DmTask dream_sequence(DmList dream_fragments = null, datum_dream current_dream = null)
    {
        if (!IS_UNCONSCIOUS(this) || stat >= HARD_CRIT)
        {
            REMOVE_TRAIT(this, TRAIT_DREAMING, DREAMING_SOURCE);
            current_dream.OnDreamEnd(this);
            if (Dm.truthy(this._listen_lookup?[COMSIG_END_DREAMING]))
                this._SendSignal(COMSIG_END_DREAMING, Dm.list(this, current_dream));
            return;
        }
        object next_message = dream_fragments[1];
        dream_fragments.Cut(1, 2);

        if (next_message is datum_callback something_happens)
            next_message = something_happens.InvokeAsync(this);

        to_chat(this, span_notice(Dm.text("<i>... [] ...</i>", next_message)));

        if (Dm.truthy(LAZYLEN(dream_fragments)))
        {
            float next_wait = rand(10, 30);
            if (Dm.truthy(current_dream.sleep_until_finished))
                await AdjustSleeping(next_wait);
            _addtimer(Dm.@new<datum_callback>(this, nameof(dream_sequence), dream_fragments, current_dream), next_wait, file: "code/modules/flufftext/dreaming.dm", line: 69);
        }
        else
        {
            REMOVE_TRAIT(this, TRAIT_DREAMING, DREAMING_SOURCE);
            current_dream.OnDreamEnd(this);
            if (Dm.truthy(this._listen_lookup?[COMSIG_END_DREAMING]))
                this._SendSignal(COMSIG_END_DREAMING, Dm.list(this, current_dream));
        }
    }
}

//-------------------------
// DREAM DATUMS

// tg: code/modules/flufftext/dreaming.dm:79
public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/modules/flufftext/dreaming.dm:79  GLOBAL_LIST_INIT(dreams, populate_dream_list())
    public DmList dreams;

    // tg: code/modules/flufftext/dreaming.dm:79  (GLOBAL_MANAGED(dreams, populate_dream_list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobaldreams()
    {
        dreams = populate_dream_list() as DmList;
        Dm.append(ref gvars_datum_init_order, "dreams");
    }
}

public static partial class GlobalProcs
{
    // tg: code/modules/flufftext/dreaming.dm:78
    public static object populate_dream_list()
    {
        DmList output = Dm.list();
        foreach (System.Type dream_type in Dm.each_cast<System.Type>(subtypesof(typeof(datum_dream))))
            output[Dm.@new(dream_type)] = Dm.initial(dream_type, (datum_dream p) => p.weight);
        return output;
    }
}

/**
 * Contains all the behavior needed to play a kind of dream.
 * All dream types get randomly selected from based on weight when an appropriate mobs dreams.
 */
// tg: code/modules/flufftext/dreaming.dm:88
public partial class datum_dream : datum
{
    /**
     * Called when beginning a new dream for the dreamer.
     * Gives back a list of dream events. Events can be text or callbacks that return text.
     */
    // tg: code/modules/flufftext/dreaming.dm:99
    public virtual object GenerateDream(mob_living_carbon dreamer = null)
    {
        return Dm.list();
    }

    /**
     * Called when the dream ends or is interrupted.
     */
    // tg: code/modules/flufftext/dreaming.dm:105
    public virtual void OnDreamEnd(mob_living_carbon dreamer = null)
    {
        return;
    }
}

/// The classic random dream of various words that might form a cohesive narrative, but usually wont
// tg: code/modules/flufftext/dreaming.dm:109
public partial class datum_dream_random : datum_dream
{
    // tg: code/modules/flufftext/dreaming.dm:112
    public override object GenerateDream(mob_living_carbon dreamer = null)
    {
        DmList custom_dream_nouns = (get_dream_nouns(dreamer) as DmList) ?? Dm.list();
        string fragment = "";

        DmList dot = Dm.list();
        Dm.append(ref dot, I18n.T("you see"));

        //Subject
        if (Dm.truthy(custom_dream_nouns.len) && prob(90))
            fragment += Dm.text("[]", pick(custom_dream_nouns));
        else
            fragment += Dm.text("[]", pick(GLOB.dream_strings));

        if (prob(50)) //Replace the adjective space with an adjective, or just get rid of it
            fragment = replacetext(fragment, "%ADJECTIVE%", pick(GLOB.adjectives)) as string;
        else
            fragment = replacetext(fragment, "%ADJECTIVE% ", "") as string;
        if (Dm.truthy(findtext(fragment, "%A% ")))
            fragment = Dm.text("\\a []", replacetext(fragment, "%A% ", ""));
        Dm.append(ref dot, fragment);

        //Verb
        fragment = "";
        if (prob(50))
        {
            if (prob(35))
                fragment += Dm.text("[] ", pick(GLOB.adverbs));
            fragment += Dm.text("[]", pick(GLOB.ing_verbs));
        }
        else
        {
            fragment += I18n.T("will ");
            fragment += Dm.text("[]", pick(GLOB.verbs));
        }
        Dm.append(ref dot, fragment);

        if (prob(25))
            return dot;

        //Object
        fragment = "";
        fragment += Dm.text("[]", pick(GLOB.dream_strings));
        if (prob(50))
            fragment = replacetext(fragment, "%ADJECTIVE%", pick(GLOB.adjectives)) as string;
        else
            fragment = replacetext(fragment, "%ADJECTIVE% ", "") as string;
        if (Dm.truthy(findtext(fragment, "%A% ")))
            fragment = Dm.text("\\a []", replacetext(fragment, "%A% ", ""));
        Dm.append(ref dot, fragment);
        return dot;
    }

    // tg: code/modules/flufftext/dreaming.dm:158
    public virtual object get_dream_nouns(mob_living_carbon dreamer = null)
    {
        DmList custom_dream_nouns = Dm.list();
        foreach (obj_item_bedsheet sheet in Dm.each<obj_item_bedsheet>(dreamer.loc))
            Dm.append(ref custom_dream_nouns, sheet.dream_messages);
        return custom_dream_nouns;
    }
}

/// Dream plays a random sound at you, chosen from all sounds in the folder
// tg: code/modules/flufftext/dreaming.dm:165
public partial class datum_dream_hear_something : datum_dream
{
    // tg: code/modules/flufftext/dreaming.dm:170
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        RegisterSignal(SSsounds, COMSIG_SUBSYSTEM_POST_INITIALIZE, nameof(ReserveSoundChannel));
    }

    // tg: code/modules/flufftext/dreaming.dm:174
    public override object GenerateDream(mob_living_carbon dreamer = null)
    {
        DmList dot = base.GenerateDream(dreamer) as DmList;
        Dm.append(ref dot, pick(I18n.T("you wind up a toy"), I18n.T("you hear something strange"), I18n.T("you pick out a record to play"), I18n.T("you hit shuffle on your music player")));
        Dm.append(ref dot, Dm.@new<datum_callback>(this, nameof(PlayRandomSound)));
        Dm.append(ref dot, I18n.T("it reminds you of something"));
        return dot;
    }

    // tg: code/modules/flufftext/dreaming.dm:180
    public override void OnDreamEnd(mob_living_carbon dreamer = null)
    {
        base.OnDreamEnd(dreamer);
        // In case we play some long ass music track
        _addtimer(Dm.@new<datum_callback>(this, nameof(StopSound), dreamer), 5 * SECONDS, file: "code/modules/flufftext/dreaming.dm", line: 183);
    }

    // tg: code/modules/flufftext/dreaming.dm:185
    [SignalHandler]
    public virtual void ReserveSoundChannel()
    {
        reserved_sound_channel = SSsounds.reserve_sound_channel_for_datum(this);
        UnregisterSignal(SSsounds, COMSIG_SUBSYSTEM_POST_INITIALIZE);
    }

    // tg: code/modules/flufftext/dreaming.dm:189
    public virtual object PlayRandomSound(mob_living_carbon dreamer = null)
    {
        sound random_sound = sound(pick(SSsounds.all_sounds), channel: Dm.num(reserved_sound_channel));
        random_sound.status = SOUND_STREAM;
        Dm.output_to(dreamer, random_sound); // SEND_SOUND(dreamer, random_sound) (DIRECT_OUTPUT, rules 12)
        return I18n.T("you hear something you weren't expecting!");
    }

    // tg: code/modules/flufftext/dreaming.dm:195
    public virtual void StopSound(mob_living_carbon dreamer = null)
    {
        Dm.output_to(dreamer, sound(channel: Dm.num(reserved_sound_channel))); // SEND_SOUND(dreamer, sound(channel=reserved_sound_channel)) (DIRECT_OUTPUT, rules 12)
    }
}
