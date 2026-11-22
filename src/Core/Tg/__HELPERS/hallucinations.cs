// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/hallucinations.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8 slice G (D187): the whole file but for the admin tooling (the debug verb with its popup, the tgui pickers of select_hallucination_type and
// create_delusion: marked leaves, not-translated.md section 56). The var blocks are generated (D111). [DmPath("/mob/living")] stays on this
// part (rules 2.3): hallucinations.dm is the earliest file that declares something on /mob/living.

public partial class datum_controller_global_vars
{
    /// A global list of all ongoing hallucinations, primarily for easy access to be able to stop (delete) hallucinations.
    // tg: code/__HELPERS/hallucinations.dm:2  GLOBAL_LIST_EMPTY(all_ongoing_hallucinations)
    public DmList all_ongoing_hallucinations;

    // tg: code/__HELPERS/hallucinations.dm:2  (GLOBAL_MANAGED(all_ongoing_hallucinations, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalall_ongoing_hallucinations()
    {
        all_ongoing_hallucinations = Dm.list();
        Dm.append(ref gvars_datum_init_order, "all_ongoing_hallucinations");
    }

    /// Global weighted list of all hallucinations that can show up randomly.
    // tg: code/__HELPERS/hallucinations.dm:127  GLOBAL_LIST_INIT_TYPED(random_hallucination_weighted_list, /list, generate_hallucination_weighted_list())
    public DmList random_hallucination_weighted_list;

    // tg: code/__HELPERS/hallucinations.dm:127  (GLOBAL_MANAGED(random_hallucination_weighted_list, generate_hallucination_weighted_list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalrandom_hallucination_weighted_list()
    {
        random_hallucination_weighted_list = (DmList)generate_hallucination_weighted_list();
        Dm.append(ref gvars_datum_init_order, "random_hallucination_weighted_list");
    }
}

public static partial class GlobalProcs
{
    // Hallucination tiers
    /// Very common hallucinations, minor stuff that'll make you double-take but is otherwise very subtle.
    // tg: code/__HELPERS/hallucinations.dm:6  #define HALLUCINATION_TIER_COMMON 1
    public const int HALLUCINATION_TIER_COMMON = 1;
    /// Uncommon hallucinations, more noticeable and potentially more impactful (causing temporary stuns or stamina damage).
    // tg: code/__HELPERS/hallucinations.dm:8  #define HALLUCINATION_TIER_UNCOMMON 2
    public const int HALLUCINATION_TIER_UNCOMMON = 2;
    /// Rarer hallucinations which are usually pretty obvious, but also pretty impactful.
    // tg: code/__HELPERS/hallucinations.dm:10  #define HALLUCINATION_TIER_RARE 3
    public const int HALLUCINATION_TIER_RARE = 3;
    /// Hallucinations which are generally just for laughs and are obviously fake
    // tg: code/__HELPERS/hallucinations.dm:12  #define HALLUCINATION_TIER_VERYSPECIAL 4
    public const int HALLUCINATION_TIER_VERYSPECIAL = 4;
    /// Hallucinations which are never picked, only forced
    // tg: code/__HELPERS/hallucinations.dm:14  #define HALLUCINATION_TIER_NEVER 5
    public const int HALLUCINATION_TIER_NEVER = 5;

    /// What typepath of the hallucination
    // tg: code/__HELPERS/hallucinations.dm:17  #define HALLUCINATION_ARG_TYPE 1
    public const int HALLUCINATION_ARG_TYPE = 1;
    /// Where the hallucination came from, for logging
    // tg: code/__HELPERS/hallucinations.dm:19  #define HALLUCINATION_ARG_SOURCE 2
    public const int HALLUCINATION_ARG_SOURCE = 2;

    /// Onwards from this index, it's the arglist that gets passed into the hallucination created.
    // tg: code/__HELPERS/hallucinations.dm:22  #define HALLUCINATION_ARGLIST 3
    public const int HALLUCINATION_ARGLIST = 3;

    /// Biotypes which cannot hallucinate for balance and logic reasons (not code)
    // tg: code/__HELPERS/hallucinations.dm:25  #define NO_HALLUCINATION_BIOTYPES (MOB_ROBOTIC|MOB_SPIRIT|MOB_SPECIAL)
    public const int NO_HALLUCINATION_BIOTYPES = MOB_ROBOTIC | MOB_SPIRIT | MOB_SPECIAL;

    /**
     * Emits a hallucinating pulse around the passed atom.
     * Affects everyone in the passed radius who can view the center,
     * except for those with TRAIT_MADNESS_IMMUNE, or those who are blind.
     *
     * center - required, the center of the pulse
     * radius - the radius around that the pulse reaches
     * hallucination_duration - how much hallucination is added by the pulse. reduced based on distance to the center.
     * hallucination_max_duration - a cap on how much hallucination can be added
     * optional_messages - optional list of messages passed. Those affected by pulses will be given one of the messages in said list.
     */
    // tg: code/__HELPERS/hallucinations.dm:60
    public static async DmTask visible_hallucination_pulse(atom center = null, float? radius = null, float? hallucination_duration = null, object hallucination_max_duration = null, DmList optional_messages = null)
    {
        radius ??= 7;
        hallucination_duration ??= 50 * SECONDS;
        foreach (mob_living nearby_living in Dm.each<mob_living>(view(center, radius)))
        {
            if (HAS_MIND_TRAIT(nearby_living, TRAIT_MADNESS_IMMUNE))
                continue;

            if (Dm.truthy(Dm.bitand(nearby_living.mob_biotypes, NO_HALLUCINATION_BIOTYPES)))
                continue;

            if (Dm.truthy(nearby_living.is_blind()))
                continue;

            // Everyone else gets hallucinations.
            float dist = sqrt(1 / max(1, get_dist(nearby_living, center)));
            await nearby_living.adjust_hallucinations_up_to(hallucination_duration.Value * dist, hallucination_max_duration);
            if (length(optional_messages) != 0)
                to_chat(nearby_living, pick(optional_messages));
        }
    }

    /**
     * Emits a hallucinating pulse around the passed atom.
     * Affects everyone in the passed radius except for those with TRAIT_MADNESS_IMMUNE. This affects blind players.
     *
     * center - required, the center of the pulse
     * radius - the radius around that the pulse reaches
     * hallucination_duration - how much hallucination is added by the pulse. reduced based on distance to the center.
     * hallucination_max_duration - a cap on how much hallucination can be added
     * optional_messages - optional list of messages passed. Those affected by pulses will be given one of the messages in said list.
     */
    // tg: code/__HELPERS/hallucinations.dm:85
    public static async DmTask hallucination_pulse(atom center = null, float? radius = null, float? hallucination_duration = null, object hallucination_max_duration = null, DmList optional_messages = null)
    {
        radius ??= 7;
        hallucination_duration ??= 50 * SECONDS;
        foreach (mob_living nearby_living in Dm.each<mob_living>(range(radius, center)))
        {
            if (HAS_MIND_TRAIT(nearby_living, TRAIT_MADNESS_IMMUNE))
                continue;

            if (Dm.truthy(Dm.bitand(nearby_living.mob_biotypes, NO_HALLUCINATION_BIOTYPES)))
                continue;

            // Everyone else gets hallucinations.
            float dist = sqrt(1 / max(1, get_dist(nearby_living, center)));
            await nearby_living.adjust_hallucinations_up_to(hallucination_duration.Value * dist, hallucination_max_duration);
            if (length(optional_messages) != 0)
                to_chat(nearby_living, pick(optional_messages));
        }
    }

    /// Generates the global weighted list of random hallucinations.
    // tg: code/__HELPERS/hallucinations.dm:130
    public static object generate_hallucination_weighted_list()
    {
        DmList weighted_list = Dm.list();

        foreach (Type hallucination_type in Dm.each_cast<Type>(typesof(typeof(datum_hallucination))))
        {
            if (hallucination_type == Dm.initial(hallucination_type, (datum_hallucination p) => p.abstract_hallucination_parent))
                continue;
            // weights need to be integers for pick_weight(), multiple by ten preserving their relative probabilities.
            float weight = Dm.initial(hallucination_type, (datum_hallucination p) => p.random_hallucination_weight) * 10;
            if (weight <= 0)
                continue;

            string tier_key = Dm.text("[]", Dm.initial(hallucination_type, (datum_hallucination p) => p.hallucination_tier));
            DmList tier_list = weighted_list[tier_key] as DmList;
            LAZYSET(ref tier_list, hallucination_type, weight);
            weighted_list[tier_key] = tier_list;
        }

        return weighted_list;
    }

    /// Select a random hallucination from the hallucination pool
    ///
    /// * tier - the tier of hallucination to select from
    /// * strict - if true, only select from the passed tier. If false, select from the passed tier and all tiers below it.
    // tg: code/__HELPERS/hallucinations.dm:147  (the tier is a number, which indexes the list by position, as in tg)
    public static async DmTask<object> get_random_hallucination(float? tier = null, float? strict = null)
    {
        tier ??= HALLUCINATION_TIER_COMMON;
        strict ??= FALSE;
        if (!Dm.truthy(GLOB.random_hallucination_weighted_list[tier.Value]))
            throw Dm.CRASH(Dm.text("get_random_hallucination - No hallucinations in tier \\[[]\\].", tier), "code/__HELPERS/hallucinations.dm", 149);

        DmList pool = ((DmList)((DmList)GLOB.random_hallucination_weighted_list)[Dm.text("[]", tier)]).Copy();
        if (!Dm.truthy(strict))
        {
            tier -= 1;
            while (tier >= HALLUCINATION_TIER_COMMON)
            {
                Dm.append(pool, ((DmList)GLOB.random_hallucination_weighted_list)[Dm.text("[]", tier)]);
                tier -= 1;
            }
        }

        return pick_weight(pool);
    }

    /// Debug proc for getting the total weight of the random_hallucination_weighted_list
    // tg: code/__HELPERS/hallucinations.dm:160
    public static object debug_hallucination_weighted_list()
    {
        float total_weight = 0;
        foreach (object tier in Dm.each(GLOB.random_hallucination_weighted_list))
        {
            foreach (Type hallucination_type in Dm.each_cast<Type>(((DmList)GLOB.random_hallucination_weighted_list)[tier]))
                total_weight += Dm.num(((DmList)((DmList)GLOB.random_hallucination_weighted_list)[tier])[hallucination_type]);
        }

        to_chat(usr, span_boldnotice(Dm.text("The total weight of the hallucination weighted list is [].", total_weight)));
        return total_weight;
    }

    /// Gets a random subtype of the passed hallucination type that has a random_hallucination_weight > 0.
    /// If no subtype is passed, it will get any random hallucination subtype that is not abstract and has weight > 0.
    /// This can be used instead of picking from the global weighted list to just get a random valid hallucination.
    // tg: code/__HELPERS/hallucinations.dm:210
    public static object get_random_valid_hallucination_subtype(Type passed_type = null)
    {
        passed_type ??= typeof(datum_hallucination);
        if (!ispath(passed_type, typeof(datum_hallucination)))
            throw Dm.CRASH("get_random_valid_hallucination_subtype - get_random_valid_hallucination_subtype passed not a hallucination subtype.", "code/__HELPERS/hallucinations.dm", 212);

        foreach (Type hallucination_type in Dm.each_cast<Type>(shuffle(subtypesof(passed_type))))
        {
            if (Dm.initial(hallucination_type, (datum_hallucination p) => p.abstract_hallucination_parent) == hallucination_type)
                continue;
            if (Dm.initial(hallucination_type, (datum_hallucination p) => p.random_hallucination_weight) <= 0)
                continue;

            return hallucination_type;
        }

        return null;
    }

    /// Helper to give the passed mob the ability to select a hallucination from the list of all hallucination subtypes.
    // tg: code/__HELPERS/hallucinations.dm:225
    public static async DmTask<object> select_hallucination_type(mob user = null, string message = null, string title = null)
    {
        Dm.NotTranslated("code/__HELPERS/hallucinations.dm:225", "select_hallucination_type: the tgui list picker (tgui_input_list), admin tooling");
        return default;
    }

    /// Helper to give the passed mob the ability to create a delusion hallucination (even a custom one).
    /// Returns a list of arguments - pass these to _cause_hallucination to cause the desired hallucination
    // tg: code/__HELPERS/hallucinations.dm:238
    public static async DmTask<object> create_delusion(mob user = null)
    {
        Dm.NotTranslated("code/__HELPERS/hallucinations.dm:238", "create_delusion: the tgui pickers (tgui_input_list, tgui_alert, tgui_input_number), admin tooling");
        return default;
    }
}

// tg: code/__HELPERS/hallucinations.dm:39
[DmPath("/mob/living")]
public partial class mob_living : mob
{
    // Macro wrapper for _cause_hallucination so we can cheat in named arguments, like AddComponent.
    /**
     * Causes a hallucination of a certain type to the mob.
     *
     * First argument is always the type of halllucination, a /datum/hallucination, required.
     * second argument is always the key source of the hallucination, used for admin logging, required.
     *
     * Additionally, named arguments are supported for passing them forward to the created hallucination's new().
     */
    // tg: code/__HELPERS/hallucinations.dm:36  #define cause_hallucination(arguments...) _cause_hallucination(list(##arguments))
    public DmTask<object> cause_hallucination(params object[] arguments)
    {
        arguments ??= new object[] { null };
        var items = new object[arguments.Length];
        for (int i = 0; i < arguments.Length; i++)
            items[i] = arguments[i] is DmNamedArg named ? Dm.kv(named.Name, named.Value) : arguments[i];
        return _cause_hallucination(Dm.list(items));
    }

    /// Unless you need this for an explicit reason, use the cause_hallucination wrapper.
    // tg: code/__HELPERS/hallucinations.dm:39
    public virtual async DmTask<object> _cause_hallucination(DmList raw_args = null)
    {
        if (length(raw_args) == 0)
            throw Dm.CRASH("cause_hallucination called with no arguments.", "code/__HELPERS/hallucinations.dm", 41);

        Type hallucination_type = raw_args[HALLUCINATION_ARG_TYPE] as Type; // first arg is the type always
        if (!ispath(hallucination_type, typeof(datum_hallucination)))
            throw Dm.CRASH(Dm.text("cause_hallucination was given a non-hallucination type. (Got: [])", raw_args[HALLUCINATION_ARG_TYPE] ?? "null"), "code/__HELPERS/hallucinations.dm", 45);

        object hallucination_source = raw_args[HALLUCINATION_ARG_SOURCE]; // and second arg, the source
        datum_hallucination new_hallucination;

        if (length(raw_args) >= HALLUCINATION_ARGLIST)
        {
            DmList passed_args = raw_args.Copy(HALLUCINATION_ARGLIST);
            passed_args.Insert(HALLUCINATION_ARG_TYPE, this);

            new_hallucination = (datum_hallucination)Dm.@new(hallucination_type, Dm.arglist(passed_args));
        }
        else
            new_hallucination = (datum_hallucination)Dm.@new(hallucination_type, this);

        // For some reason, we qdel'd in New, maybe something went wrong.
        if (QDELETED(new_hallucination))
            return null;
        // It's not guaranteed that the hallucination passed can successfully be initiated.
        // This means there may be cases where someone should have a hallucination but nothing happens,
        // notably if you pass a randomly picked hallucination type into this.
        // Maybe there should be a separate proc to reroll on failure?
        if (!Dm.truthy(await new_hallucination.start()))
        {
            qdel(new_hallucination);
            return null;
        }

        if (Dm.truthy(_listen_lookup?[COMSIG_LIVING_HALLUCINATING]))
            _SendSignal(COMSIG_LIVING_HALLUCINATING, Dm.list(this, new_hallucination));
        investigate_log(Dm.text("was afflicted with a hallucination of type [] by: []. ([])", hallucination_type, hallucination_source, new_hallucination.feedback_details), INVESTIGATE_HALLUCINATIONS);
        return new_hallucination;
    }
}
