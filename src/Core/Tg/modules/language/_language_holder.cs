// Translated from tgstation (snapshot 1d99bd7f): code/modules/language/_language_holder.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: the holder's vars, New and gain_partial_understanding_from_language (with grant_partial_language and
// get_selected_language skeletons), GLOB.prototype_language_holders with init_language_holder_prototypes, and the presets
// that M2 declares (atom_basic, human_basic, empty) translated (M2 F1: a human's random name reads its species' holder,
// __HELPERS/names.dm:33). The holder's procs (granting, removing, blocking, speaking, copying, the language menu) and the
// other presets are languages (M9); typesof(/datum/language_holder) holds the declared presets, as GLOB.species_prototypes
// holds the declared species (M2 P2).

/**
 * Language holders will either exist in an atom/movable. Creation of language holders happens
 * automatically when they are needed, for example when something tries to speak.
 * Where a mind is available, the mind language holder will be the one "in charge". The mind holder
 * will update its language based on the atom holder, and will get updated as part of
 * transformations and other events that cause new languages to become available.
 * Every language holder has three lists of languages (and sources for each of them):
 * - understood_languages
 * - spoken_languages
 * - blocked_languages
 * Understood languages let you understand them, spoken languages lets you speak them
 * (if your tongue is compatible), and blocked languages will let you do neither no matter
 * what the source of the language is.
 * Language holders are designed to mostly only ever require the use the helpers in atom/movable
 * to achieve your goals, but it is also possible to work on them directly if needed. Any adding
 * and removing of languages and sources should only happen through the procs, as directly
 * changing these will mess something up somewhere down the line.
 * All atom movables have the initial_language_holder var which allows you to set the default
 * language holder to create. For example, /datum/language_holder/alien will give you xenocommon
 * and a block for galactic common. Human species also have a default language holder var that
 * will be updated when the species is changed, initial_species_holder.
 * Key procs
 * * [grant_language](atom/movable.html#proc/grant_language)
 * * [remove_language](atom/movable.html#proc/remove_language)
 * * [add_blocked_language](atom/movable.html#proc/add_blocked_language)
 * * [remove_blocked_language](atom/movable.html#proc/remove_blocked_language)
 * * [grant_all_languages](atom/movable.html#proc/grant_all_languages)
 * * [remove_all_languages](atom/movable.html#proc/remove_all_languages)
 * * [has_language](atom/movable.html#proc/has_language)
 * * [can_speak_language](atom/movable.html#proc/can_speak_language)
 * * [get_selected_language](atom/movable.html#proc/get_selected_language)
 */
// tg: code/modules/language/_language_holder.dm:38
[DmPath("/datum/language_holder")]
public partial class datum_language_holder : datum
{
    /// Lazyassoclist of all understood languages
    // tg: code/modules/language/_language_holder.dm:40
    public DmList understood_languages;
    /// Lazyassoclist of languages that can be spoken.
    /// Tongue organ may also set limits beyond this list.
    // tg: code/modules/language/_language_holder.dm:43
    public DmList spoken_languages;
    /// Lazyassoclist of languages we are blocked from understanding, used for certain mobs, mutations, quirks, etc.
    // tg: code/modules/language/_language_holder.dm:45
    public DmList blocked_understanding;
    /// Lazyassoclist of languages we are blocked from speaking, used for certain mobs, mutations, quirks, etc.
    // tg: code/modules/language/_language_holder.dm:47
    public DmList blocked_speaking;
    /// If true, overrides tongue aforementioned limitations.
    // tg: code/modules/language/_language_holder.dm:49
    public float omnitongue = FALSE;
    /// Handles displaying the language menu UI.
    // tg: code/modules/language/_language_holder.dm:51  VAR_FINAL/datum/language_menu/language_menu
    [DmFinal]
    public datum_language_menu language_menu;
    /// Currently spoken language
    // tg: code/modules/language/_language_holder.dm:53
    public object selected_language;
    /// Tracks the entity that owns the holder.
    // tg: code/modules/language/_language_holder.dm:55
    [DmFinal]
    public atom_movable owner;
    /// Lazyassoclist of all mutual understanding this holder has
    /// You generally don't want to access this, you want [best_mutual_languages] instead
    /// Format: list(language_type = list(source = % of understanding))
    // tg: code/modules/language/_language_holder.dm:59
    [DmProtected]
    public DmList mutual_understanding;
    /// Cached form of the mutual language list which only contains the best understanding available to each language
    // tg: code/modules/language/_language_holder.dm:61
    [DmFinal]
    public DmList best_mutual_languages;

    /// Initializes, and copies in the languages from the current atom if available.
    // tg: code/modules/language/_language_holder.dm:64
    [DmParams("new_owner")]
    public override async DmTask New(params object[] __extra)
    {
        atom new_owner = Dm.arg<atom>(__extra, 0);
        if (Dm.truthy(new_owner))
        {
            if (QDELETED(new_owner))
                throw Dm.CRASH(Dm.text("Language holder added to a qdeleting thing, what the fuck []", text_ref(new_owner)), "code/modules/language/_language_holder.dm", 67);
            if (!ismovable(new_owner))
                throw Dm.CRASH(Dm.text("Language holder being added to a non-movable thing, this is invalid (was: [] / [])", new_owner, new_owner.type), "code/modules/language/_language_holder.dm", 69);
        }

        owner = new_owner as atom_movable;

        // If we have an owner, we'll set a default selected language
        if (Dm.truthy(owner))
            get_selected_language();
        // Normally this is applied in grant_language, which we bypass
        foreach (object language in Dm.each(understood_languages))
            gain_partial_understanding_from_language(language);
    }

    // tg: code/modules/language/_language_holder.dm:80  (gaps L2)
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref language_menu);
        owner = null;
        return base.Destroy(force);
    }

    /// Helper to apply all the partial understanding from the passed language
    // tg: code/modules/language/_language_holder.dm:87
    [DmPrivate]
    public virtual void gain_partial_understanding_from_language(object language = null)
    {
        datum_language prototype = GLOB.language_datum_instances[language] as datum_language;
        foreach (object other_language in Dm.each(prototype.mutual_understanding))
            grant_partial_language(other_language, Dm.num_or_null(prototype.mutual_understanding[other_language]), language as string);
    }

    /// Helper to remove all the partial understanding from the passed language
    // tg: code/modules/language/_language_holder.dm:94
    [DmPrivate]
    public virtual void lose_partial_understanding_from_language(object language = null)
    {
        datum_language prototype = GLOB.language_datum_instances[language] as datum_language;
        foreach (object other_language in Dm.each(prototype.mutual_understanding))
            remove_partial_language(other_language, language as string);
    }

    /// Calculates the "best mutual language list"
    // tg: code/modules/language/_language_holder.dm:102
    public virtual void calculate_best_mutual_language()
    {
        best_mutual_languages = Dm.list();
        foreach (object language in Dm.each(mutual_understanding))
        {
            foreach (object source in Dm.each(mutual_understanding[language]))
            {
                // if this mutual understanding comes from a language, and that language is blocked, skip it
                if (Dm.truthy(LAZYACCESS(blocked_understanding, source)))
                    continue;
                if (!Dm.truthy(best_mutual_languages[language]) || Dm.lt(best_mutual_languages[language], (mutual_understanding[language] as DmList)[source]))
                    best_mutual_languages[language] = (mutual_understanding[language] as DmList)[source];
            }
        }
    }

    /// Grants the supplied language.
    // tg: code/modules/language/_language_holder.dm:113
    public virtual object grant_language(object language = null, float? language_flags = null, object source = null) // copy_languages passes a list of sources
    {
        language_flags ??= ALL;
        source ??= LANGUAGE_MIND;
        object dot = null;
        if (Dm.truthy(Dm.bitand(language_flags, UNDERSTOOD_LANGUAGE)))
        {
            LAZYORASSOCLIST(ref understood_languages, language, source);
            gain_partial_understanding_from_language(language);
            dot = TRUE;
        }
        if (Dm.truthy(Dm.bitand(language_flags, SPOKEN_LANGUAGE)))
        {
            LAZYORASSOCLIST(ref spoken_languages, language, source);
            dot = TRUE;
        }

        return dot;
    }

    /// Grants every language to understood and spoken, and gives omnitongue.
    // tg: code/modules/language/_language_holder.dm:125
    public virtual object grant_all_languages(float? language_flags = null, float? grant_omnitongue = null, string source = null)
    {
        language_flags ??= ALL;
        grant_omnitongue ??= TRUE;
        source ??= LANGUAGE_MIND;
        foreach (object language in Dm.each(GLOB.all_languages))
            grant_language(language, language_flags, source);
        if (Dm.truthy(grant_omnitongue)) // Overrides tongue limitations.
            omnitongue = TRUE;
        return TRUE;
    }

    /// Grants partial understanding of the passed language.
    /// Giving 100 understanding is basically equivalent to knowning the language, just with butchered punctuation.
    // tg: code/modules/language/_language_holder.dm:135
    public virtual object grant_partial_language(object language = null, float? amount = null, string source = null)
    {
        amount ??= 50;
        source ??= LANGUAGE_MIND;
        LAZYINITLIST(ref mutual_understanding);
        DmList understanding = mutual_understanding[language] as DmList;
        LAZYSET(ref understanding, source, amount);
        mutual_understanding[language] = understanding;
        calculate_best_mutual_language();
        return TRUE;
    }

    /// Removes a single language or source, removing all sources returns the pre-removal state of the language.
    // tg: code/modules/language/_language_holder.dm:142
    public virtual object remove_language(object language = null, float? language_flags = null, string source = null)
    {
        language_flags ??= ALL;
        source ??= LANGUAGE_ALL;
        object dot = null;
        if (Dm.truthy(Dm.bitand(language_flags, UNDERSTOOD_LANGUAGE)))
        {
            if (Dm.eq(source, LANGUAGE_ALL))
                LAZYREMOVE(ref understood_languages, language);
            else
                LAZYREMOVEASSOC(ref understood_languages, language, source);
            if (!Dm.truthy(LAZYACCESS(understood_languages, language)))
                lose_partial_understanding_from_language(language);
            dot = TRUE;
        }

        if (Dm.truthy(Dm.bitand(language_flags, SPOKEN_LANGUAGE)))
        {
            if (Dm.eq(source, LANGUAGE_ALL))
                LAZYREMOVE(ref spoken_languages, language);
            else
                LAZYREMOVEASSOC(ref spoken_languages, language, source);
            dot = TRUE;
        }

        return dot;
    }

    /// Removes every language and optionally sets omnitongue false. If a non default source is supplied, only removes that source.
    // tg: code/modules/language/_language_holder.dm:163
    public virtual object remove_all_languages(string source = null, float? remove_omnitongue = null)
    {
        source ??= LANGUAGE_ALL;
        remove_omnitongue ??= FALSE;
        foreach (object language in Dm.each(GLOB.all_languages))
            remove_language(language, ALL, source);
        if (Dm.truthy(remove_omnitongue))
            omnitongue = FALSE;
        return TRUE;
    }

    /// Removes partial understanding of the passed language.
    // tg: code/modules/language/_language_holder.dm:171
    public virtual object remove_partial_language(object language = null, string source = null)
    {
        source ??= LANGUAGE_MIND;
        object dot = FALSE;
        if (Dm.eq(source, LANGUAGE_ALL))
        {
            foreach (object other_source in Dm.each(mutual_understanding?[language]))
            {
                if (ispath(other_source, typeof(datum_language)))
                    continue;
                dot = (remove_partial_language(language, other_source as string) is var or__1 && Dm.truthy(or__1) ? or__1 : dot);
            }
        }
        else if (Dm.truthy(LAZYACCESSASSOC(mutual_understanding, language, source)))
        {
            DmList understanding = mutual_understanding[language] as DmList;
            LAZYREMOVE(ref understanding, source);
            mutual_understanding[language] = understanding;
            ASSOC_UNSETEMPTY(ref mutual_understanding, language);
            UNSETEMPTY(ref mutual_understanding);
            dot = TRUE;
        }

        if (Dm.truthy(dot))
            calculate_best_mutual_language();
        return dot;
    }

    /// Removes all partial understandings of all languages.
    // tg: code/modules/language/_language_holder.dm:190
    public virtual object remove_all_partial_languages(string source = null)
    {
        source ??= LANGUAGE_MIND;
        foreach (object language in Dm.each(mutual_understanding))
            remove_partial_language(language, source);
        return TRUE;
    }

    /// Adds a single language or list of languages to the blocked language list.
    // tg: code/modules/language/_language_holder.dm:196
    public virtual object add_blocked_language(object languages = null, float? language_flags = null, object source = null) // copy_languages passes a list of sources
    {
        language_flags ??= ALL;
        source ??= LANGUAGE_MIND;
        if (!islist(languages))
            languages = Dm.list(languages);

        foreach (object language in Dm.each(languages))
        {
            if (Dm.truthy(Dm.bitand(language_flags, SPOKEN_LANGUAGE)))
                LAZYORASSOCLIST(ref blocked_speaking, language, source);
            if (Dm.truthy(Dm.bitand(language_flags, UNDERSTOOD_LANGUAGE)))
                LAZYORASSOCLIST(ref blocked_understanding, language, source);
        }
        calculate_best_mutual_language();
        return TRUE;
    }

    /// Removes a single language or list of languages from the blocked language list.
    // tg: code/modules/language/_language_holder.dm:209
    public virtual object remove_blocked_language(object languages = null, float? language_flags = null, string source = null)
    {
        language_flags ??= ALL;
        source ??= LANGUAGE_MIND;
        if (!islist(languages))
            languages = Dm.list(languages);

        foreach (object language in Dm.each(languages))
        {
            if (Dm.eq(source, LANGUAGE_ALL))
            {
                if (Dm.truthy(Dm.bitand(language_flags, SPOKEN_LANGUAGE)))
                    LAZYREMOVE(ref blocked_speaking, language);
                if (Dm.truthy(Dm.bitand(language_flags, UNDERSTOOD_LANGUAGE)))
                    LAZYREMOVE(ref blocked_understanding, language);
            }
            else
            {
                if (Dm.truthy(Dm.bitand(language_flags, SPOKEN_LANGUAGE)))
                    LAZYREMOVEASSOC(ref blocked_speaking, language, source);
                if (Dm.truthy(Dm.bitand(language_flags, UNDERSTOOD_LANGUAGE)))
                    LAZYREMOVEASSOC(ref blocked_understanding, language, source);
            }
        }
        calculate_best_mutual_language();
        return TRUE;
    }

    /// Checks if you have the language passed.
    // tg: code/modules/language/_language_holder.dm:229
    public virtual object has_language(object language = null, object flag_to_check = null)
    {
        flag_to_check ??= UNDERSTOOD_LANGUAGE;
        DmList langs_to_check = Dm.list();
        if (Dm.truthy(Dm.bitand(flag_to_check, SPOKEN_LANGUAGE)) && !Dm.truthy(LAZYACCESS(blocked_speaking, language)))
            Dm.combine(ref langs_to_check, spoken_languages);
        if (Dm.truthy(Dm.bitand(flag_to_check, UNDERSTOOD_LANGUAGE)) && !Dm.truthy(LAZYACCESS(blocked_understanding, language)))
            Dm.combine(ref langs_to_check, understood_languages);

        return Dm.@in(language, langs_to_check) ? TRUE : FALSE;
    }

    /// Checks if you have partial understanding of the language passed.
    // tg: code/modules/language/_language_holder.dm:239
    public virtual object has_partial_language(object language = null)
    {
        return LAZYACCESS(best_mutual_languages, language);
    }

    /// Checks if you can speak the language. Tongue limitations should be supplied as an argument.
    // tg: code/modules/language/_language_holder.dm:243
    public virtual object can_speak_language(object language = null)
    {
        object can_speak_language_path = Dm.truthy(omnitongue) ? omnitongue : owner.could_speak_language(language as Type);
        return Dm.truthy(can_speak_language_path) && Dm.truthy(has_language(language, SPOKEN_LANGUAGE)) ? TRUE : FALSE;
    }

    /// Returns selected language if it can be spoken, or decides, sets and returns a new selected language if possible.
    // tg: code/modules/language/_language_holder.dm:248
    public virtual object get_selected_language()
    {
        if (Dm.truthy(selected_language) && Dm.truthy(can_speak_language(selected_language)))
            return selected_language;
        selected_language = null;
        object highest_priority = null;
        foreach (object lang in Dm.each(spoken_languages))
        {
            Type language = lang as Type;
            float priority = Dm.initial(language, (datum_language p) => p.default_priority);
            if ((!Dm.truthy(highest_priority) || Dm.gt(priority, highest_priority)) && !Dm.@in(language, blocked_speaking))
            {
                if (Dm.truthy(can_speak_language(language)))
                {
                    selected_language = language;
                    highest_priority = priority;
                }
            }
        }
        return selected_language;
    }

    /// Gets a random understood language, useful for hallucinations and such.
    // tg: code/modules/language/_language_holder.dm:263
    public virtual object get_random_understood_language()
    {
        return Dm.pick(understood_languages);
    }

    /// Gets a random spoken language, useful for forced speech and such.
    // tg: code/modules/language/_language_holder.dm:267
    public virtual object get_random_spoken_language()
    {
        return Dm.pick(spoken_languages);
    }

    /// Gets a random spoken language, trying to get a non-common language.
    // tg: code/modules/language/_language_holder.dm:268  (gaps L2)
    public virtual object get_random_spoken_uncommon_language()
    {
        DmList languages_minus_common = Dm.sub(assoc_to_keys(spoken_languages), typeof(datum_language_common)) as DmList;

        // They have a language other than common
        if (Dm.truthy(length(languages_minus_common)))
            return Dm.pick(languages_minus_common);

        // They can only speak common, oh well.
        return typeof(datum_language_common);
    }

    /// Returns a list of languages we understand
    // tg: code/modules/language/_language_holder.dm:283
    public virtual object get_understood_languages()
    {
        return Dm.sub(assoc_to_keys(understood_languages), assoc_to_keys(blocked_understanding));
    }

    /// Opens a language menu reading from the language holder.
    // tg: code/modules/language/_language_holder.dm:287  (gaps L2)
    public virtual async DmTask open_language_menu(mob user = null)
    {
        language_menu ??= Dm.@new<datum_language_menu>(this);
        await language_menu.ui_interact(user);
    }

    /// Copies all languages from the supplied atom/language holder. Source should be overridden when you
    /// do not want the language overwritten by later atom updates or want to avoid blocked languages.
    // tg: code/modules/language/_language_holder.dm:294
    public virtual object copy_languages(datum_language_holder from_holder = null, string source_override = null)
    {
        if (Dm.truthy(source_override)) //No blocked languages here, for now only used by ling absorb.
        {
            foreach (object language in Dm.each(from_holder.understood_languages))
                grant_language(language, UNDERSTOOD_LANGUAGE, source_override);
            foreach (object language in Dm.each(from_holder.spoken_languages))
                grant_language(language, SPOKEN_LANGUAGE, source_override);
        }
        else
        {
            foreach (object language in Dm.each(from_holder.understood_languages))
                grant_language(language, UNDERSTOOD_LANGUAGE, from_holder.understood_languages[language]);
            foreach (object language in Dm.each(from_holder.spoken_languages))
                grant_language(language, SPOKEN_LANGUAGE, from_holder.spoken_languages[language]);
            foreach (object language in Dm.each(from_holder.blocked_speaking))
                add_blocked_language(language, SPOKEN_LANGUAGE, from_holder.blocked_speaking[language]);
            foreach (object language in Dm.each(from_holder.blocked_understanding))
                add_blocked_language(language, UNDERSTOOD_LANGUAGE, from_holder.blocked_understanding[language]);
        }
        return TRUE;
    }

    /// Transfers all mind languages to the supplied language holder.
    // tg: code/modules/language/_language_holder.dm:309
    public virtual void transfer_mind_languages(datum_language_holder to_holder = null)
    {
        foreach (object language in Dm.each(understood_languages))
        {
            if (Dm.@in(LANGUAGE_MIND, understood_languages[language]))
            {
                remove_language(language, UNDERSTOOD_LANGUAGE, LANGUAGE_MIND);
                to_holder.grant_language(language, UNDERSTOOD_LANGUAGE, LANGUAGE_MIND);
            }
        }
        foreach (object language in Dm.each(spoken_languages))
        {
            if (Dm.@in(LANGUAGE_MIND, spoken_languages[language]))
            {
                remove_language(language, SPOKEN_LANGUAGE, LANGUAGE_MIND);
                to_holder.grant_language(language, SPOKEN_LANGUAGE, LANGUAGE_MIND);
            }
        }
        foreach (object language in Dm.each(blocked_speaking))
        {
            if (Dm.@in(LANGUAGE_MIND, blocked_speaking[language]))
            {
                remove_blocked_language(language, SPOKEN_LANGUAGE, LANGUAGE_MIND);
                to_holder.add_blocked_language(language, SPOKEN_LANGUAGE, LANGUAGE_MIND);
            }
        }
        foreach (object language in Dm.each(blocked_understanding))
        {
            if (Dm.@in(LANGUAGE_MIND, blocked_understanding[language]))
            {
                remove_blocked_language(language, UNDERSTOOD_LANGUAGE, LANGUAGE_MIND);
                to_holder.add_blocked_language(language, UNDERSTOOD_LANGUAGE, LANGUAGE_MIND);
            }
        }
        foreach (object language in Dm.each(mutual_understanding))
        {
            float mind_understanding = Dm.num((mutual_understanding[language] as DmList)?[LANGUAGE_MIND]);
            if (mind_understanding > 0)
            {
                remove_partial_language(language, LANGUAGE_MIND);
                to_holder.grant_partial_language(language, mind_understanding, LANGUAGE_MIND);
            }
        }

        if (owner != null)
            get_selected_language();
        if (to_holder.owner != null)
            to_holder.get_selected_language();
    }
}

/// A global assoc list containing prototypes of all language holders
/// [Language holder typepath] to [language holder instance]
/// Used for easy reference of what can speak what without needing to constantly recreate language holders.
// tg: code/modules/language/_language_holder.dm:340  GLOBAL_LIST_INIT(prototype_language_holders, init_language_holder_prototypes())
public partial class datum_controller_global_vars
{
    // tg: code/modules/language/_language_holder.dm:340  GLOBAL_LIST_INIT(prototype_language_holders, init_language_holder_prototypes())
    public DmList prototype_language_holders;

    // tg: code/modules/language/_language_holder.dm:340  (GLOBAL_MANAGED(prototype_language_holders, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalprototype_language_holders()
    {
        prototype_language_holders = init_language_holder_prototypes() as DmList;
        Dm.append(ref gvars_datum_init_order, "prototype_language_holders");
    }
}

public static partial class GlobalProcs
{
    /// Inits the global list of language holder prototypes.
    // tg: code/modules/language/_language_holder.dm:343
    public static object init_language_holder_prototypes()
    {
        DmList prototypes = Dm.list();
        // every holder, as tg (DV25 narrowed, gaps E2c: the holders are data, all live; a corpse spawner's skeleton names itself from
        // its species' holder at map load)
        foreach (object holdertype in Dm.each(typesof(typeof(datum_language_holder))))
            prototypes[holdertype] = Dm.@new((Type)holdertype);

        return prototypes;
    }
}

// tg: code/modules/language/_language_holder.dm:382
[DmPath("/datum/language_holder/human_basic")]
public partial class datum_language_holder_human_basic : datum_language_holder
{
    // tg: code/modules/language/_language_holder.dm:383-384  the object-creating defaults (rules 10.2)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        understood_languages = Dm.list(Dm.kv(typeof(datum_language_common), Dm.list(LANGUAGE_ATOM)));
        spoken_languages = Dm.list(Dm.kv(typeof(datum_language_common), Dm.list(LANGUAGE_ATOM)));
    }
}

// Given to atoms by default
// tg: code/modules/language/_language_holder.dm:698
[DmPath("/datum/language_holder/atom_basic")]
public partial class datum_language_holder_atom_basic : datum_language_holder
{
    // tg: code/modules/language/_language_holder.dm:699-700  the object-creating defaults (rules 10.2)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        understood_languages = Dm.list(Dm.kv(typeof(datum_language_common), Dm.list(LANGUAGE_ATOM)));
        spoken_languages = Dm.list(Dm.kv(typeof(datum_language_common), Dm.list(LANGUAGE_ATOM)));
    }
}

// Explicitly empty one for readability
// tg: code/modules/language/_language_holder.dm:703
[DmPath("/datum/language_holder/empty")]
public partial class datum_language_holder_empty : datum_language_holder
{
}

// Androids (the species' holder; written by hand so the prototype list, which skips generated-only types, has it)
// tg: code/modules/language/_language_holder.dm:485
public partial class datum_language_holder_synthetic : datum_language_holder
{
    // tg: code/modules/language/_language_holder.dm:485
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        understood_languages = Dm.list(Dm.kv(typeof(datum_language_common), Dm.list(LANGUAGE_ATOM)), Dm.kv(typeof(datum_language_uncommon), Dm.list(LANGUAGE_ATOM)), Dm.kv(typeof(datum_language_machine), Dm.list(LANGUAGE_ATOM)));
        spoken_languages = Dm.list(Dm.kv(typeof(datum_language_common), Dm.list(LANGUAGE_ATOM)), Dm.kv(typeof(datum_language_uncommon), Dm.list(LANGUAGE_ATOM)), Dm.kv(typeof(datum_language_machine), Dm.list(LANGUAGE_ATOM)));
    }
}

// Has all the languages known (via "mind")
// tg: code/modules/language/_language_holder.dm:706
public partial class datum_language_holder_universal : datum_language_holder
{
    // tg: code/modules/language/_language_holder.dm:708
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        grant_all_languages(source: LANGUAGE_MIND);
    }
}

/// For intelligent synthetics like cyborgs and the AI, not for basic baby bots ran on a raspberry pi
// tg: code/modules/language/_language_holder.dm:497
public partial class datum_language_holder_synthetic_silicon : datum_language_holder_synthetic
{
    // tg: code/modules/language/_language_holder.dm:499
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        foreach (object lang in Dm.each(GLOB.uncommon_roundstart_languages))
            grant_partial_language(lang, 66, LANGUAGE_ATOM);
    }
}
