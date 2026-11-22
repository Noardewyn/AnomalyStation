// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/dynamic/_dynamic_ruleset.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using System.Linq;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Syndicate package A3 (D316): the whole file. get_candidate_mind's parameter is a /mob (Appendix A: tg declares mob/dead, but the
// from-living rulesets pass living mobs). DV346: a ruleset whose own procs are not translated (an inert type: its antagonist is not
// ported) weighs 0, so Dynamic never picks it.

/**
 * ## Dynamic ruleset datum
 *
 * These datums (which are not singletons) are used by dynamic to create antagonists
 */
public partial class datum_dynamic_ruleset : datum
{
    // tg: code/controllers/subsystem/dynamic/_dynamic_ruleset.dm:86
    public override async DmTask New(params object[] __extra)
    {
        DmList dynamic_config = __extra.Length > 0 ? __extra[0] as DmList : null;
        DmList tagged = dynamic_config?[config_tag] as DmList;
        foreach (object new_var in Dm.each(tagged))
            set_config_value(new_var, (dynamic_config[config_tag] as DmList)[new_var]);
    }

    // tg: code/controllers/subsystem/dynamic/_dynamic_ruleset.dm:90
    public override object Destroy(float? force = null)
    {
        selected_minds = null;
        return base.Destroy(force);
    }

    /// Used for parsing config entries to validate them
    // tg: code/controllers/subsystem/dynamic/_dynamic_ruleset.dm:95
    public virtual object set_config_value(object new_var = null, object new_val = null)
    {
        if (!Dm.has_var(this, Dm.str(new_var))) // !(new_var in vars)
        {
            log_dynamic(Dm.text("Erroneous config edit rejected: []", new_var));
            return FALSE;
        }
        DmList locked_config_values = Dm.list(
            nameof(config_tag),
            nameof(jobban_flag),
            nameof(pref_flag),
            nameof(preview_antag_datum),
            nameof(ruleset_flags),
            nameof(ruleset_lazy_templates),
            nameof(selected_minds),
            "vars"
        );

        if (Dm.@in(new_var, locked_config_values))
        {
            log_dynamic(Dm.text("Bad config edit rejected: []", new_var));
            return FALSE;
        }

        if (islist(new_val) && (Dm.eq(new_var, nameof(weight)) || Dm.eq(new_var, nameof(min_pop))))
            new_val = load_tier_list(new_val as DmList);

        Dm.set_var(this, Dm.str(new_var), new_val); // vars[new_var] = new_val
        return TRUE;
    }

    /// Used to create tier alists for weights and min_pop values
    // tg: code/controllers/subsystem/dynamic/_dynamic_ruleset.dm:126
    public virtual object load_tier_list(DmList incoming_list = null)
    {
        // PRIVATE_PROC(TRUE)

        DmList tier_list = Dm.alist();
        // loads a list of list("2" = 1, "3" = 3) into an alist(2 = 1, 3 = 3)
        foreach (object tier in Dm.each(incoming_list))
        {
            float? tier_value = Dm.text2num(tier);
            if (Dm.gt(tier_value, DYNAMIC_TIER_HIGH))
            {
                stack_trace(Dm.text("Dynamic config for [] encountered a tier value exceeding DYNAMIC_TIER_HIGH!", name), "code/controllers/subsystem/dynamic/_dynamic_ruleset.dm", 134);
                message_admins(span_boldannounce(Dm.text("Dynamic config for [] encountered a tier value exceeding DYNAMIC_TIER_HIGH!", name)));
                tier_value = DYNAMIC_TIER_HIGH;
            }
            else if (Dm.lt(tier_value, DYNAMIC_TIER_GREEN))
            {
                stack_trace(Dm.text("Dynamic config for [] encountered a negative tier value, below DYNAMIC_TIER_GREEN!", name), "code/controllers/subsystem/dynamic/_dynamic_ruleset.dm", 138);
                message_admins(span_boldannounce(Dm.text("Dynamic config for [] encountered a negative tier value, below DYNAMIC_TIER_GREEN!", name)));
                tier_value = DYNAMIC_TIER_GREEN;
            }
            tier_list[tier_value] = incoming_list[tier];
        }

        return tier_list;
    }

    /**
     * Any additional checks to see if this ruleset can be selected
     */
    // tg: code/controllers/subsystem/dynamic/_dynamic_ruleset.dm:148
    public virtual object can_be_selected()
    {
        return TRUE;
    }

    /// Gets the list value for the given tier, otherwise use next highest tier,
    /// or failing that, next lowest
    // tg: code/controllers/subsystem/dynamic/_dynamic_ruleset.dm:153
    public virtual object get_tier_specific_value(DmList values = null, object tier = null)
    {
        // PRIVATE_PROC(TRUE)
        if (Dm.isnum(values[tier]))
            return values[tier];

        // search higher tiers
        for (float i = Dm.num(tier); i <= 4; i++)
        {
            if (Dm.isnum(values[i]))
                return values[i];
        }

        // no dice, lower tiers?
        for (float i = Dm.num(tier); i >= 1; i--)
        {
            if (Dm.isnum(values[i]))
                return values[i];
        }

        return 0;
    }

    /**
     * Calculates the weight of this ruleset for the given tier.
     *
     * * population_size - How many players are alive
     * * tier - The dynamic tier to calculate the weight for
     */
    // tg: code/controllers/subsystem/dynamic/_dynamic_ruleset.dm:176
    public virtual object get_weight(float? population_size = null, float? tier = null)
    {
        // SHOULD_NOT_OVERRIDE(TRUE)
        population_size ??= 0;
        tier ??= DYNAMIC_TIER_LOW;

        // DEVIATION(DV346): a ruleset whose own procs are not translated (its antagonist is not ported) is never picked
        if (Attribute.IsDefined(GetType(), typeof(DmInertAttribute), inherit: false))
            return 0;

        if (Dm.@in(type, SSdynamic.admin_disabled_rulesets))
            return 0;
        if (!Dm.truthy(can_be_selected()))
            return 0;
        object final_minpop = islist(min_pop) ? get_tier_specific_value(min_pop as DmList, tier) : min_pop;
        if (Dm.gt(final_minpop, population_size))
            return 0;

        float final_weight = Dm.num(islist(weight) ? get_tier_specific_value(weight as DmList, tier) : weight);
        foreach (datum_dynamic_ruleset other_ruleset in Dm.each_cast<datum_dynamic_ruleset>(SSdynamic.executed_rulesets))
        {
            if (other_ruleset == this)
                continue;
            if (tier != DYNAMIC_TIER_HIGH && Dm.truthy(Dm.bitand(ruleset_flags, RULESET_HIGH_IMPACT)) && Dm.truthy(Dm.bitand(other_ruleset.ruleset_flags, RULESET_HIGH_IMPACT)))
                return 0;
            if (!istype(other_ruleset, type))
                continue;
            if (!Dm.truthy(repeatable))
                return 0;
            final_weight -= repeatable_weight_decrease;
        }

        return Dm.max(final_weight, 0);
    }

    /// Returns what the antag cap with the given population is.
    // tg: code/controllers/subsystem/dynamic/_dynamic_ruleset.dm:202
    public virtual object get_antag_cap(object population_size = null, object antag_cap = null)
    {
        // SHOULD_NOT_OVERRIDE(TRUE)
        if (Dm.isnum(antag_cap))
            return antag_cap;

        DmList cap = antag_cap as DmList;
        return Dm.ceil(Dm.num(population_size) / Dm.num(cap["denominator"])) + Dm.num(cap["offset"]);
    }

    /**
     * Prepares the ruleset for execution, primarily used for selecting the players who will be assigned to this ruleset
     *
     * * antag_candidates - List of players who are candidates for this ruleset
     * This list is mutated by this proc!
     *
     * Returns TRUE if execution is ready, FALSE if it should be canceled
     */
    // tg: code/controllers/subsystem/dynamic/_dynamic_ruleset.dm:217
    public virtual async DmTask<object> prepare_execution(float? population_size = null, DmList antag_candidates = null)
    {
        // SHOULD_NOT_OVERRIDE(TRUE)
        population_size ??= 0;
        antag_candidates ??= Dm.list();

        // !! THIS SLEEPS !!
        await load_templates();

        // This is (mostly) redundant, buuuut the (potential) sleep above makes it iffy, so let's just be safe
        if (!Dm.truthy(can_be_selected()))
        {
            log_data = "Reason: Ruleset cannot be selected.";
            return FALSE;
        }

        object max_candidates = get_antag_cap(population_size, Dm.truthy(max_antag_cap) ? max_antag_cap : min_antag_cap);
        object min_candidates = get_antag_cap(population_size, min_antag_cap);

        DmList selected_candidates = await select_candidates(antag_candidates, Dm.num(max_candidates)) as DmList;
        if (Dm.lt(Dm.length(selected_candidates), min_candidates))
        {
            log_data = Dm.text("Reason: Not enough eligible candidates. Have: [], Need: []", Dm.length(selected_candidates), min_candidates);
            return FALSE;
        }

        foreach (mob candidate in Dm.each_cast<mob>(selected_candidates))
        {
            datum_mind candidate_mind = get_candidate_mind(candidate) as datum_mind;
            await prepare_for_role(candidate_mind);
            LAZYADDASSOC(ref SSjob.prevented_occupations, candidate_mind, get_blacklisted_roles()); // this is what makes sure you can't roll traitor as a sec-off
            Dm.append(ref selected_minds, candidate_mind);
            Dm.remove(ref antag_candidates, candidate);
        }

        return TRUE;
    }

    /// Gets the mind of a candidate, can be overridden to return a different mind if necessary
    // tg: code/controllers/subsystem/dynamic/_dynamic_ruleset.dm:246
    public virtual object get_candidate_mind(mob candidate = null)
    {
        return candidate.mind;
    }

    /// Returns a list of roles that cannot be selected for this ruleset
    // tg: code/controllers/subsystem/dynamic/_dynamic_ruleset.dm:250
    public virtual object get_blacklisted_roles()
    {
        return Dm.bitor(get_config_blacklisted_roles(), get_always_blacklisted_roles());
    }

    /// Returns all the jobs the config says this ruleset cannot select
    // tg: code/controllers/subsystem/dynamic/_dynamic_ruleset.dm:254
    public virtual object get_config_blacklisted_roles()
    {
        // SHOULD_NOT_OVERRIDE(TRUE)
        DmList blacklist = blacklisted_roles.Copy();
        foreach (datum_job job in Dm.each_cast<datum_job>(SSjob.all_occupations))
        {
            bool @protected = Dm.truthy(Dm.bitand(job.job_flags, JOB_ANTAG_PROTECTED));
            bool blacklisted = Dm.truthy(Dm.bitand(job.job_flags, JOB_ANTAG_BLACKLISTED));
            if ((Dm.truthy(config.Get(typeof(datum_config_entry_flag_protect_roles_from_antagonist))) && @protected) || blacklisted)
                Dm.combine(ref blacklist, job.title);
        }
        // tg's own slip, kept: the assistant goes into the ruleset's own list, not the returned copy (it shows from the next call on)
        if (Dm.truthy(config.Get(typeof(datum_config_entry_flag_protect_assistant_from_antagonist))))
            Dm.combine(ref blacklisted_roles, JOB_ASSISTANT);
        return blacklist;
    }

    /// Returns a list of roles that are always blacklisted from this ruleset, for mechanical reasons (an AI can't be a changeling)
    // tg: code/controllers/subsystem/dynamic/_dynamic_ruleset.dm:267
    public virtual object get_always_blacklisted_roles()
    {
        return Dm.list(
            JOB_AI,
            JOB_CYBORG
        );
    }

    /// Takes in a list of players and returns a list of players who are valid candidates for this ruleset
    /// Don't touch this proc if you need to trim candidates further - override is_valid_candidate() instead
    // tg: code/controllers/subsystem/dynamic/_dynamic_ruleset.dm:275
    public virtual async DmTask<object> trim_candidates(DmList antag_candidates = null)
    {
        // SHOULD_NOT_OVERRIDE(TRUE)

        DmList valid_candidates = Dm.list();
        foreach (mob candidate in Dm.each_cast<mob>(antag_candidates))
        {
            client candidate_client = GET_CLIENT(candidate) as client;
            if (candidate_client == null)
                continue;
            if (Dm.gt(candidate_client.get_remaining_days(minimum_required_age), 0))
                continue;
            if (Dm.truthy(pref_flag) && !Dm.@in(pref_flag, candidate_client.prefs.be_special))
                continue;
            if (Dm.truthy(is_banned_from(candidate.ckey, Dm.list(ROLE_SYNDICATE, Dm.truthy(jobban_flag) ? jobban_flag : pref_flag))))
                continue;
            if (!Dm.truthy(await is_valid_candidate(candidate, candidate_client)))
                continue;
            Dm.append(ref valid_candidates, candidate);
        }
        return valid_candidates;
    }

    /// Returns a list of players picked for this ruleset
    // tg: code/controllers/subsystem/dynamic/_dynamic_ruleset.dm:295
    public virtual async DmTask<object> select_candidates(DmList antag_candidates = null, float? num_candidates = null)
    {
        // SHOULD_NOT_OVERRIDE(TRUE)
        // PRIVATE_PROC(TRUE)
        num_candidates ??= 0;

        if (num_candidates <= 0)
            return Dm.list();

        // technically not pure
        DmList resulting_candidates = shuffle(await trim_candidates(antag_candidates) as DmList) as DmList ?? Dm.list();
        if (Dm.length(resulting_candidates) <= num_candidates)
            return resulting_candidates;

        resulting_candidates.Cut(num_candidates.Value + 1);
        return resulting_candidates;
    }

    /// Handles loading map templates that this ruleset requires
    // tg: code/controllers/subsystem/dynamic/_dynamic_ruleset.dm:311
    public virtual async DmTask load_templates()
    {
        // SHOULD_NOT_OVERRIDE(TRUE)
        // PRIVATE_PROC(TRUE)

        foreach (object template in Dm.each(ruleset_lazy_templates))
            await SSmapping.lazy_load_template(template);
    }

    /**
     * Any additional checks to see if this player is a valid candidate for this ruleset
     */
    // tg: code/controllers/subsystem/dynamic/_dynamic_ruleset.dm:321
    public virtual async DmTask<object> is_valid_candidate(mob candidate = null, client candidate_client = null)
    {
        // SHOULD_CALL_PARENT(TRUE)
        return TRUE;
    }

    /**
     * Handles any special logic that needs to be done for a player before they are assigned to this ruleset
     * This is ran before the player is in their job position, and before they even have a player character
     *
     * Override this proc to do things like set forced jobs, DON'T assign roles or give out equipments here!
     */
    // tg: code/controllers/subsystem/dynamic/_dynamic_ruleset.dm:331
    public virtual async DmTask prepare_for_role(datum_mind candidate = null)
    {
        // PROTECTED_PROC(TRUE)
        return;
    }

    /**
     * Executes the ruleset, assigning the selected players to their roles.
     * No backing out now, at this point it's guaranteed to run.
     *
     * Prefer to override assign_role() instead of this proc
     */
    // tg: code/controllers/subsystem/dynamic/_dynamic_ruleset.dm:341
    public virtual async DmTask execute()
    {
        DmList execute_args = await create_execute_args() as DmList;
        foreach (datum_mind mind in Dm.each_cast<datum_mind>(selected_minds))
            await assign_role(mind, Dm.each(execute_args).ToArray()); // assign_role(arglist(list(mind) + execute_args))
    }

    /// Allows you to supply extra arguments to assign_role() if needed
    // tg: code/controllers/subsystem/dynamic/_dynamic_ruleset.dm:347
    public virtual async DmTask<object> create_execute_args()
    {
        return Dm.list();
    }

    /**
     * Used by the ruleset to actually assign the role to the player
     * This is ran after they have a player character spawned, and after they're in their job (with all their job equipment)
     *
     * Override this proc to give out antag datums or special items or whatever
     */
    // tg: code/controllers/subsystem/dynamic/_dynamic_ruleset.dm:356
    public virtual async DmTask assign_role(datum_mind candidate = null, params object[] __extra)
    {
        // PROTECTED_PROC(TRUE)
        stack_trace(Dm.text("Ruleset [] does not implement assign_role()", this), "code/controllers/subsystem/dynamic/_dynamic_ruleset.dm", 358);
        return;
    }

    /**
     * Handles setting SSticker news report / mode result for more impactful rulsets
     *
     * Return TRUE if any result was set
     */
    // tg: code/controllers/subsystem/dynamic/_dynamic_ruleset.dm:366
    public virtual object round_result()
    {
        return FALSE;
    }

    /**
     * Allows admins to configure rulesets before prepare_execution() is called.
     *
     * Only called if RULESET_ADMIN_CONFIGURABLE is set in ruleset_flags.
     * Also only called by midrounds currently.
     */
    // tg: code/controllers/subsystem/dynamic/_dynamic_ruleset.dm:375
    public virtual async DmTask<object> configure_ruleset(mob admin = null)
    {
        stack_trace(Dm.text("Ruleset [] sets flag RULESET_ADMIN_CONFIGURABLE but does not implement configure_ruleset!", type), "code/controllers/subsystem/dynamic/_dynamic_ruleset.dm", 376);
        return null;
    }
}
