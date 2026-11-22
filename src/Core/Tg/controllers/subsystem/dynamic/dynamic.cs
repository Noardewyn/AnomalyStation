// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/dynamic/dynamic.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Syndicate package A3 (D316): the whole file, with build_dynamic_toml (tg compiles it only with TESTING). DV346: the rulesets whose own
// procs are not translated (inert) weigh 0 (_dynamic_ruleset.cs get_weight), so no pick ever runs one. Deferred leaf: the revolution's result in set_round_result (GLOB.revolution_handler is the revolution's, not ported: always null).

// tg: code/controllers/subsystem/dynamic/dynamic.dm:1 SUBSYSTEM_DEF(dynamic)
public partial class datum_controller_subsystem_dynamic : datum_controller_subsystem
{
    // The New() that SUBSYSTEM_DEF(dynamic) defines (__DEFINES/MC.dm), with NEW_SS_GLOBAL(SSdynamic) (__DEFINES/MC.dm:29) expanded
    // inline (it reads src).
    // tg: code/controllers/subsystem/dynamic/dynamic.dm:1
    public override async DmTask New(params object[] __extra)
    {
        if (SSdynamic != this)
        {
            if (SSdynamic is datum_controller_subsystem_dynamic)
            {
                Recover();
                qdel(SSdynamic);
            }
            SSdynamic = this;
        }
        PreInit();
    }

    // tg: code/controllers/subsystem/dynamic/dynamic.dm:62
    public override async DmTask fire(float? resumed = null, params object[] __extra)
    {
        if (!(midround_cooldown <= world.time) || datum_controller_subsystem_shuttle.EMERGENCY_PAST_POINT_OF_NO_RETURN()) // COOLDOWN_FINISHED(src, midround_cooldown)
            return;

        if (light_ruleset_start <= world.time) // COOLDOWN_FINISHED(src, light_ruleset_start)
        {
            if (Dm.truthy(await try_spawn_midround(LIGHT_MIDROUND)))
                return;
        }

        if (heavy_ruleset_start <= world.time) // COOLDOWN_FINISHED(src, heavy_ruleset_start)
        {
            if (Dm.truthy(await try_spawn_midround(HEAVY_MIDROUND)))
                return;
        }
    }

    // tg: code/controllers/subsystem/dynamic/dynamic.dm:74
    public virtual object get_config()
    {
        if (!Dm.truthy(Dm.length(dynamic_config)))
            load_config();
        return dynamic_config;
    }

    /// Used to get a config entry for some variable on some typepath
    /// Can be passed a default value.
    // tg: code/controllers/subsystem/dynamic/dynamic.dm:81
    public virtual object get_config_value(object some_typepath = null, object var_name = null, object default_value = null)
    {
        object config_tag;
        if (ispath(some_typepath, typeof(datum_dynamic_ruleset)))
            config_tag = Dm.initial(some_typepath, "config_tag"); // ruleset_type::config_tag
        else if (ispath(some_typepath, typeof(datum_dynamic_tier)))
            config_tag = Dm.initial(some_typepath, "config_tag"); // tier_type::config_tag
        else
        {
            stack_trace(Dm.text("Dynamic get_config_value called with invalid typepath: []", some_typepath), "code/controllers/subsystem/dynamic/dynamic.dm", 91);
            return default_value;
        }

        if (config_tag == null) // Technically valid
            return default_value;

        if (!Dm.truthy(Dm.length(dynamic_config)))
            load_config();

        object config_value = (dynamic_config?[config_tag] as DmList)?[var_name];
        return config_value == null ? default_value : config_value;
    }

    /**
     * Selects which rulesets are to run at roundstart, and sets them up
     *
     * Note: This proc can sleep (due to lazyloading of templates)!
     */
    // tg: code/controllers/subsystem/dynamic/dynamic.dm:107
    public virtual async DmTask<object> select_roundstart_antagonists()
    {
        load_config();
        // SEND_SIGNAL(src, COMSIG_DYNAMIC_PRE_ROUNDSTART, dynamic_config) (rules 11.1 (e), a statement)
        if (Dm.truthy(this._listen_lookup?[COMSIG_DYNAMIC_PRE_ROUNDSTART]))
            this._SendSignal(COMSIG_DYNAMIC_PRE_ROUNDSTART, Dm.list(this, dynamic_config));
        // we start by doing a dry run of the job selection process to detect antag rollers
        await SSjob.divide_occupations(pure: TRUE, allow_all: TRUE);

        DmList antag_candidates = Dm.list();
        // anyone who was readied up and NOT unassigned is a potential candidate (even those who have no antag preferences)
        foreach (mob_dead_new_player player in Dm.each_cast<mob_dead_new_player>(Dm.sub(GLOB.new_player_list, SSjob.unassigned)))
        {
            if (Dm.eq(player.ready, PLAYER_READY_TO_PLAY) && player.mind != null)
                Dm.append(ref antag_candidates, player);
        }

        // anyone unassigned are potential antag rollers, so we need to warn them
        foreach (mob_dead_new_player player in Dm.each_cast<mob_dead_new_player>(SSjob.unassigned))
        {
            DmList job_data = Dm.list();
            DmList job_prefs = player.client?.prefs.job_preferences;
            foreach (object job in Dm.each(job_prefs))
            {
                object priority = job_prefs[job];
                Dm.append(ref job_data, Dm.text("[]: []", job, SSjob.job_priority_level_to_string(priority)));
            }
            to_chat(player, span_danger("You were unable to qualify for any roundstart antagonist role this round because your job preferences presented a high chance of all of your selected jobs being unavailable, along with 'return to lobby if job is unavailable' enabled. Increase the number of roles set to medium or low priority to reduce the chances of this happening."));
            log_admin(Dm.text("[] failed to qualify for any roundstart antagonist role because their job preferences presented a high chance of all of their selected jobs being unavailable, along with 'return to lobby if job is unavailable' enabled and has [] antag preferences enabled. They will be unable to qualify for any roundstart antagonist role. These are their job preferences - []", player.ckey, player.client.prefs.be_special.len, job_data.Join(" | ")));
        }

        float num_real_players = Dm.length(antag_candidates);
        // now select a tier (if admins didn't)
        // this also calculates the number of rulesets to spawn
        if (current_tier == null)
            pick_tier(num_real_players);
        // put rulesets in the queue (if admins didn't)
        // this will even handle the case in which the tier wants 0 roundstart rulesets
        if (!Dm.truthy(Dm.length(queued_rulesets)))
        {
            foreach (object ruleset in Dm.each(pick_roundstart_rulesets(antag_candidates)))
                await queue_ruleset(ruleset);
        }
        // we got what we needed, reset so we can do real job selection later
        // reset only happens AFTER roundstart selection so we can verify stuff like "can we get 3 heads of staff for revs?"
        SSjob.reset_occupations();
        // finally, run through the queue and prepare rulesets for execution
        // (actual execution, ie assigning antags, will happen after job assignment)
        foreach (datum_dynamic_ruleset_roundstart ruleset in Dm.each<datum_dynamic_ruleset_roundstart>(queued_rulesets))
        {
            // NOTE: !! THIS CAN SLEEP !!
            if (!Dm.truthy(await ruleset.prepare_execution(num_real_players, antag_candidates)))
            {
                log_dynamic(Dm.text("Roundstart: Selected ruleset [], but preparation failed! []", ruleset.config_tag, ruleset.log_data));
                unqueue_ruleset(ruleset);
                qdel(ruleset);
                continue;
            }

            // Just logs who was selected at roundstart
            foreach (datum_mind selected in Dm.each_cast<datum_mind>(ruleset.selected_minds))
                log_dynamic(Dm.text("Roundstart: [] has been selected for [].", key_name(selected), ruleset.config_tag));

            Dm.remove_at(rulesets_to_spawn, ROUNDSTART, 1);
        }
        // and start ticking
        light_ruleset_start = world.time + Dm.num((current_tier.ruleset_type_settings[LIGHT_MIDROUND] as DmList)[TIME_THRESHOLD]); // COOLDOWN_START
        heavy_ruleset_start = world.time + Dm.num((current_tier.ruleset_type_settings[HEAVY_MIDROUND] as DmList)[TIME_THRESHOLD]); // COOLDOWN_START
        latejoin_ruleset_start = world.time + Dm.num((current_tier.ruleset_type_settings[LATEJOIN] as DmList)[TIME_THRESHOLD]); // COOLDOWN_START

        return TRUE;
    }

    // tg: code/controllers/subsystem/dynamic/dynamic.dm:170
    public virtual void load_config()
    {
        // PRIVATE_PROC(TRUE)

        if (!Dm.truthy(config.Get(typeof(datum_config_entry_flag_dynamic_config_enabled)))) // CONFIG_GET(flag/dynamic_config_enabled)
            return;

        string config_file = Dm.text("[]/dynamic.toml", config.directory);
        DmList result = rustg_raw_read_toml_file(config_file);
        if (!Dm.truthy(result["success"]))
        {
            log_dynamic(Dm.text("Failed to load config file! ([] - [])", config_file, result["content"]));
            return;
        }

        dynamic_config = Dm.json_decode(result["content"]) as DmList;
    }

    /// Sets the tier to the typepath passed in
    // tg: code/controllers/subsystem/dynamic/dynamic.dm:185
    public virtual void set_tier(object picked_tier = null, object population = null)
    {
        population ??= Dm.length(GLOB.player_list);
        current_tier = Dm.@new((Type)picked_tier, dynamic_config) as datum_dynamic_tier;

        foreach (object category in Dm.each(current_tier.ruleset_type_settings))
        {
            DmList range = current_tier.ruleset_type_settings[category] as DmList ?? Dm.list();
            object low_end = range[LOW_END] is var low && Dm.truthy(low) ? low : 0f;
            object high_end = range[HIGH_END] is var high && Dm.truthy(high) ? high : 0f;

            if (Dm.le(population, range[HALF_RANGE_POP_THRESHOLD] is var half && Dm.truthy(half) ? half : 0f))
                high_end = Dm.max(Dm.num(low_end), Dm.ceil(Dm.num(high_end) * 0.25f));
            else if (Dm.le(population, range[FULL_RANGE_POP_THRESHOLD] is var full && Dm.truthy(full) ? full : 0f))
                high_end = Dm.max(Dm.num(low_end), Dm.ceil(Dm.num(high_end) * 0.5f));

            rulesets_to_spawn[category] = rand(Dm.num(low_end), Dm.num(high_end));
            base_rulesets_to_spawn[category] = rulesets_to_spawn[category];
        }
    }

    /// Picks what tier we are going to use for this round and sets up all the corresponding variables and ranges
    // tg: code/controllers/subsystem/dynamic/dynamic.dm:202
    public virtual void pick_tier(float? roundstart_population = null)
    {
        // PRIVATE_PROC(TRUE)
        roundstart_population ??= 0;

        DmList tier_weighted = Dm.list();
        foreach (Type tier_datum in Dm.each_cast<Type>(subtypesof(typeof(datum_dynamic_tier))))
        {
            // GET_DYNAMIC_CONFIG(tier_datum, min_pop)
            object tier_pop = SSdynamic.get_config_value(tier_datum, "min_pop", Dm.initial(tier_datum, "min_pop"));
            if (Dm.lt(roundstart_population, tier_pop))
                continue;

            // GET_DYNAMIC_CONFIG(tier_datum, weight)
            object tier_weight = SSdynamic.get_config_value(tier_datum, "weight", Dm.initial(tier_datum, "weight"));
            if (Dm.le(tier_weight, 0))
                continue;

            tier_weighted[tier_datum] = tier_weight;
        }

        set_tier(pick_weight(tier_weighted), roundstart_population);

        object roundstart_spawn = rulesets_to_spawn[ROUNDSTART];
        object light_midround_spawn = rulesets_to_spawn[LIGHT_MIDROUND];
        object heavy_midround_spawn = rulesets_to_spawn[HEAVY_MIDROUND];
        object latejoin_spawn = rulesets_to_spawn[LATEJOIN];

        log_dynamic(Dm.text("Selected tier: []", current_tier.tier));
        log_dynamic(Dm.text("- Roundstart population: []", roundstart_population));
        log_dynamic(Dm.text("- Roundstart ruleset count: []", roundstart_spawn));
        log_dynamic(Dm.text("- Light midround ruleset count: []", light_midround_spawn));
        log_dynamic(Dm.text("- Heavy midround ruleset count: []", heavy_midround_spawn));
        log_dynamic(Dm.text("- Latejoin ruleset count: []", latejoin_spawn));
        SSblackbox.record_feedback(
            "associative",
            "dynamic_tier",
            1,
            Dm.list(
                Dm.kv("server_name", config.Get(typeof(datum_config_entry_string_serversqlname))),
                Dm.kv("tier", current_tier.tier),
                Dm.kv("player_count", roundstart_population),
                Dm.kv("roundstart_ruleset_count", roundstart_spawn),
                Dm.kv("light_midround_ruleset_count", light_midround_spawn),
                Dm.kv("heavy_midround_ruleset_count", heavy_midround_spawn),
                Dm.kv("latejoin_ruleset_count", latejoin_spawn)
            )
        );
    }

    /// Gets a weighted list of roundstart rulesets
    // tg: code/controllers/subsystem/dynamic/dynamic.dm:246
    public virtual object get_roundstart_rulesets(DmList antag_candidates = null)
    {
        // PRIVATE_PROC(TRUE)

        DmList rulesets = Dm.list();
        foreach (object ruleset_type in Dm.each(subtypesof(typeof(datum_dynamic_ruleset_roundstart))))
        {
            datum_dynamic_ruleset_roundstart ruleset = Dm.@new((Type)ruleset_type, dynamic_config) as datum_dynamic_ruleset_roundstart;
            rulesets[ruleset] = ruleset.get_weight(Dm.length(antag_candidates), current_tier.tier);
        }

        return rulesets;
    }

    /// Picks as many roundstart rulesets as we are allowed to spawn, returns them
    // tg: code/controllers/subsystem/dynamic/dynamic.dm:257
    public virtual object pick_roundstart_rulesets(DmList antag_candidates = null)
    {
        // PRIVATE_PROC(TRUE)

        if (Dm.le(rulesets_to_spawn[ROUNDSTART], 0))
            return Dm.list();

        DmList rulesets_weighted = get_roundstart_rulesets(antag_candidates) as DmList;
        float total_weight = 0;
        foreach (object ruleset in Dm.each(rulesets_weighted))
            total_weight += Dm.num(rulesets_weighted[ruleset]);
        if (total_weight <= 0)
        {
            log_dynamic("Roundstart: No rulesets to pick from!");
            return Dm.list();
        }

        DmList picked_rulesets = Dm.list();
        while (Dm.gt(rulesets_to_spawn[ROUNDSTART], 0))
        {
            if (!Dm.truthy(Dm.length(rulesets_weighted)) || total_weight <= 0)
            {
                log_dynamic(Dm.text("Roundstart: No more rulesets to pick from with [] left!", rulesets_to_spawn[ROUNDSTART]));
                break;
            }
            Dm.remove_at(rulesets_to_spawn, ROUNDSTART, 1);
            datum_dynamic_ruleset_roundstart picked_ruleset = pick_weight(rulesets_weighted) as datum_dynamic_ruleset_roundstart;
            log_dynamic(Dm.text("Roundstart: Ruleset [] (Chance: []%)", picked_ruleset.config_tag, round(Dm.num(rulesets_weighted[picked_ruleset]) / total_weight * 100, 0.01f)));
            if (Dm.truthy(picked_ruleset.solo))
            {
                log_dynamic("Roundstart: Ruleset is a solo ruleset. Cancelling other picks.");
                picked_rulesets.Cut();
                Dm.remove(ref rulesets_weighted, picked_ruleset);
                Dm.append(ref picked_rulesets, picked_ruleset.type);
                break;
            }
            if (!Dm.eq(current_tier.tier, DYNAMIC_TIER_HIGH) && Dm.truthy(Dm.bitand(picked_ruleset.ruleset_flags, RULESET_HIGH_IMPACT)))
            {
                foreach (datum_dynamic_ruleset_roundstart high_impact_ruleset in Dm.each_cast<datum_dynamic_ruleset_roundstart>(rulesets_weighted))
                {
                    if (!Dm.truthy(Dm.bitand(high_impact_ruleset.ruleset_flags, RULESET_HIGH_IMPACT)))
                        continue;
                    total_weight -= Dm.num(rulesets_weighted[high_impact_ruleset]);
                    Dm.remove(ref rulesets_weighted, high_impact_ruleset);
                }
            }
            if (!Dm.truthy(picked_ruleset.repeatable))
            {
                Dm.remove(ref rulesets_weighted, picked_ruleset);
                Dm.append(ref picked_rulesets, picked_ruleset.type);
                continue;
            }

            Dm.remove_at(rulesets_weighted, picked_ruleset, picked_ruleset.repeatable_weight_decrease);
            total_weight -= picked_ruleset.repeatable_weight_decrease;
            Dm.append(ref picked_rulesets, picked_ruleset.type);
            // Rulesets are not singletons. Queue_ruleset() will make them one.
        }

        // clean up unused rulesets
        QDEL_LIST(rulesets_weighted);
        return picked_rulesets;
    }

    // tg: code/controllers/subsystem/dynamic/dynamic.dm:305
    public virtual object get_advisory_report()
    {
        object shown_tier = current_tier.tier;
        if (prob(10))
            shown_tier = pick(Dm.sub(Dm.list(DYNAMIC_TIER_LOW, DYNAMIC_TIER_LOWMEDIUM, DYNAMIC_TIER_MEDIUMHIGH, DYNAMIC_TIER_HIGH), current_tier.tier));

        else if (prob(15))
            shown_tier = Dm.clamp(Dm.num(current_tier.tier) + Dm.num(pick(-1, 1)), DYNAMIC_TIER_LOW, DYNAMIC_TIER_HIGH);

        foreach (Type tier_datum in Dm.each_cast<Type>(subtypesof(typeof(datum_dynamic_tier))))
        {
            if (Dm.eq(Dm.initial(tier_datum, "tier"), shown_tier)) // tier_datum::tier
                return SSdynamic.get_config_value(tier_datum, "advisory_report", Dm.initial(tier_datum, "advisory_report")); // GET_DYNAMIC_CONFIG(tier_datum, advisory_report)
        }

        return null;
    }

    /**
     * Invoked by SSdynamic to try to spawn a random midround ruleset
     * Respects ranges and thresholds
     *
     * Prioritizes light midround rulesets first, then heavy midround rulesets
     *
     * Returns TRUE if a ruleset was spawned, FALSE otherwise
     */
    // tg: code/controllers/subsystem/dynamic/dynamic.dm:327
    public virtual async DmTask<object> try_spawn_midround(object range = null)
    {
        if (Dm.le(rulesets_to_spawn[range], 0))
            return FALSE;
        object midround_chance = get_midround_chance(range);
        if (!prob(Dm.num(midround_chance)))
        {
            log_dynamic(Dm.text("Midround ([]): Ruleset chance failed ([]% chance)", range, midround_chance));
            return FALSE;
        }

        midround_admin_cancel = FALSE;
        midround_admin_reroll = FALSE;
        midround_admin_cancel_period = 0; // COOLDOWN_RESET(src, midround_admin_cancel_period)

        object player_count = get_active_player_count(afk_check: TRUE);
        DmList rulesets_weighted = get_midround_rulesets(player_count, range) as DmList;
        datum_dynamic_ruleset_midround picked_ruleset = pick_weight(rulesets_weighted) as datum_dynamic_ruleset_midround;
        if (picked_ruleset == null)
        {
            log_dynamic(Dm.text("Midround ([]): No rulesets to pick from!", range));
            return FALSE;
        }
        message_admins(Dm.text("Midround ([]): Executing [] [] []", range, picked_ruleset.config_tag, MIDROUND_CANCEL_HREF(picked_ruleset), MIDROUND_REROLL_HREF(picked_ruleset, rulesets_weighted)));
        // if we have admins online, we have a waiting period before execution to allow them to cancel or reroll
        if (Dm.truthy(Dm.length(GLOB.admins)))
        {
            midround_admin_cancel_period = world.time + 15 * SECONDS; // COOLDOWN_START(src, midround_admin_cancel_period, 15 SECONDS)
            while (!(midround_admin_cancel_period <= world.time)) // COOLDOWN_FINISHED(src, midround_admin_cancel_period)
            {
                if (Dm.truthy(midround_admin_cancel))
                {
                    QDEL_LIST(rulesets_weighted);
                    midround_cooldown = world.time + Dm.num(get_ruleset_cooldown(range)); // COOLDOWN_START(src, midround_cooldown, ...)
                    return FALSE;
                }
                if (Dm.truthy(midround_admin_reroll) && Dm.length(rulesets_weighted) >= 2)
                {
                    midround_admin_reroll = FALSE;
                    midround_admin_cancel_period = world.time + 15 * SECONDS; // COOLDOWN_START(src, midround_admin_cancel_period, 15 SECONDS)
                    Dm.remove(ref rulesets_weighted, picked_ruleset);
                    qdel(picked_ruleset);
                    picked_ruleset = pick_weight(rulesets_weighted) as datum_dynamic_ruleset_midround;
                    if (picked_ruleset == null)
                    {
                        log_dynamic(Dm.text("Midround ([]): No rulesets to pick from!", range));
                        message_admins(Dm.text("Rerolling Midround ([]): Failed to pick a new ruleset, cancelling instead!", range));
                        midround_admin_cancel = TRUE;
                        continue;
                    }
                    message_admins(Dm.text("Rerolling Midround ([]): Executing [] - [] remaining rulesets in pool. [] []", range, picked_ruleset.config_tag, Dm.length(rulesets_weighted) - 1, MIDROUND_CANCEL_HREF(picked_ruleset), MIDROUND_REROLL_HREF(picked_ruleset, rulesets_weighted)));
                }
                await stoplag();
            }
        }

        // NOTE: !! THIS CAN SLEEP !!
        if (!Dm.truthy(await picked_ruleset.prepare_execution(Dm.num(player_count), await picked_ruleset.collect_candidates() as DmList)))
        {
            log_dynamic(Dm.text("Midround ([]): Selected ruleset [], but preparation failed! []", range, picked_ruleset.config_tag, picked_ruleset.log_data));
            QDEL_LIST(rulesets_weighted);
            return FALSE;
        }
        // Run the thing
        Dm.append(ref executed_rulesets, picked_ruleset);
        Dm.remove(ref rulesets_weighted, picked_ruleset);
        await picked_ruleset.execute();
        // Post execute logging
        foreach (datum_mind selected in Dm.each_cast<datum_mind>(picked_ruleset.selected_minds))
        {
            message_admins(Dm.text("Midround ([]): [] has been selected for [].", range, ADMIN_LOOKUPFLW(selected.current), picked_ruleset.config_tag));
            log_dynamic(Dm.text("Midround ([]): [] has been selected for [].", range, key_name(selected.current), picked_ruleset.config_tag));
            notify_ghosts(I18n.T("[] has been picked for []!", selected.name, picked_ruleset.config_tag), source: selected.current);
        }
        // Clean up unused rulesets
        QDEL_LIST(rulesets_weighted);
        Dm.remove_at(rulesets_to_spawn, range, 1);
        if (Dm.eq(range, LIGHT_MIDROUND))
            admin_forcing_next_light = FALSE;
        if (Dm.eq(range, HEAVY_MIDROUND))
            admin_forcing_next_heavy = FALSE;
        midround_cooldown = world.time + Dm.num(get_ruleset_cooldown(range)); // COOLDOWN_START(src, midround_cooldown, get_ruleset_cooldown(range))
        return TRUE;
    }

    // #define MIDROUND_CANCEL_HREF(...) (__dynamic_defines.dm:53): reads src and the caller's picked_ruleset (rules 11.1 (d))
    private string MIDROUND_CANCEL_HREF(datum_dynamic_ruleset picked_ruleset)
        => Dm.text("(<a href='byond://?src=[];admin_cancel_midround=[]'>CANCEL</a>)", REF(this), REF(picked_ruleset));

    // #define MIDROUND_REROLL_HREF(rulesets) (__dynamic_defines.dm:55): reads src and the caller's picked_ruleset (rules 11.1 (d))
    private string MIDROUND_REROLL_HREF(datum_dynamic_ruleset picked_ruleset, DmList rulesets)
        => Dm.text("[]", Dm.truthy(Dm.length(rulesets))
            ? Dm.text("(<a href='byond://?src=[];admin_reroll=[]'>SOMETHING ELSE</a>)", REF(this), REF(picked_ruleset))
            : Dm.text("([])", span_tooltip("There are no more rulesets to pick from!", "NOTHING ELSE")));

    /// Gets a weighted list of midround rulesets
    // tg: code/controllers/subsystem/dynamic/dynamic.dm:395
    public virtual object get_midround_rulesets(object player_count = null, object midround_type = null)
    {
        // PRIVATE_PROC(TRUE)

        DmList rulesets = Dm.list();
        foreach (Type ruleset_type in Dm.each_cast<Type>(subtypesof(typeof(datum_dynamic_ruleset_midround))))
        {
            if (!Dm.eq(Dm.initial(ruleset_type, "midround_type"), midround_type))
                continue;
            datum_dynamic_ruleset_midround ruleset = Dm.@new(ruleset_type, dynamic_config) as datum_dynamic_ruleset_midround;
            rulesets[ruleset] = ruleset.get_weight(Dm.num(player_count), current_tier.tier);
        }

        return rulesets;
    }

    /**
     * Attempt to run a midround ruleset of the given type
     *
     * * midround_type - The type of midround ruleset to force
     * * forced_max_cap - Rather than using the ruleset's max antag cap, use this value
     * As an example, this allows you to only spawn 1 traitor rather than the ruleset's default of 3
     * Can't be set to 0 (why are you forcing a ruleset that spawns 0 antags?)
     * * alert_admins_on_fail - If TRUE, alert admins if the ruleset fails to prepare/execute
     * * mob/admin - The admin who is forcing the ruleset, used for configuring the ruleset if possible
     */
    // tg: code/controllers/subsystem/dynamic/dynamic.dm:417
    public virtual async DmTask<object> force_run_midround(object midround_typepath = null, object forced_max_cap = null, float? alert_admins_on_fail = null, mob admin = null)
    {
        alert_admins_on_fail ??= FALSE;
        if (!ispath(midround_typepath, typeof(datum_dynamic_ruleset_midround)))
            throw Dm.CRASH(Dm.text("force_run_midround() was called with an invalid midround type: []", midround_typepath), "code/controllers/subsystem/dynamic/dynamic.dm", 419);

        datum_dynamic_ruleset_midround running = Dm.@new((Type)midround_typepath, dynamic_config) as datum_dynamic_ruleset_midround;
        if (Dm.isnum(forced_max_cap) && Dm.gt(forced_max_cap, 0))
        {
            running.min_antag_cap = Dm.min(Dm.num(forced_max_cap), Dm.num(running.min_antag_cap));
            running.max_antag_cap = forced_max_cap;
        }

        if (admin != null && Dm.truthy(Dm.bitand(running.ruleset_flags, RULESET_ADMIN_CONFIGURABLE)))
        {
            if (Dm.eq(await running.configure_ruleset(admin), RULESET_CONFIG_CANCEL))
            {
                qdel(running);
                return FALSE;
            }
        }

        // NOTE: !! THIS CAN SLEEP !!
        DmList candidates = await running.collect_candidates() as DmList;
        // DEVIATION(DV437): the admin who forces the ruleset picks its antagonist from its valid candidates, or Random as tg
        if (admin != null && (candidates = await pick_forced_candidate(running, admin, candidates)) == null)
        {
            qdel(running);
            return FALSE;
        }
        if (!Dm.truthy(await running.prepare_execution(Dm.num(get_active_player_count(afk_check: TRUE)), candidates)))
        {
            if (Dm.truthy(alert_admins_on_fail))
                message_admins(Dm.text("Midround (forced): Forced ruleset [], but preparation failed! []", running.config_tag, running.log_data));
            log_dynamic(Dm.text("Midround (forced): Forced ruleset [], but preparation failed! []", running.config_tag, running.log_data));
            qdel(running);
            return FALSE;
        }

        Dm.append(ref executed_rulesets, running);
        await running.execute();
        // Post execute logging
        foreach (datum_mind selected in Dm.each_cast<datum_mind>(running.selected_minds))
        {
            message_admins(Dm.text("Midround (forced): [] has been selected for [].", ADMIN_LOOKUPFLW(selected.current), running.config_tag));
            log_dynamic(Dm.text("Midround (forced): [] has been selected for [].", key_name(selected.current), running.config_tag));
            notify_ghosts(I18n.T("[] has been picked for []!", selected.name, running.config_tag), source: selected.current);
        }
        return TRUE;
    }

    /// <summary>
    /// DEVIATION(DV437), the port's own: the forcing admin picks the antagonist among the ruleset's valid candidates (its
    /// <c>trim_candidates</c>, so the antag preference and the job checks stand), or Random, tg's pick. The candidates to prepare with:
    /// the picked one alone, all of them for Random or when none is valid (prepare_execution says why), null when the admin cancels.
    /// </summary>
    private static async DmTask<DmList> pick_forced_candidate(datum_dynamic_ruleset_midround running, mob admin, DmList candidates)
    {
        DmList valid = await running.trim_candidates(candidates) as DmList;
        if (Dm.length(valid) == 0)
            return candidates;
        const string random = "Random";
        DmList choices = Dm.list(random);
        foreach (mob candidate in Dm.each_cast<mob>(valid))
            choices[Dm.text("[] ([])", candidate.name, candidate.ckey)] = candidate;
        object picked = await tgui_input_list(admin, "Who becomes the antagonist?", Dm.text("Execute []", running.config_tag), choices, random, ui_state: ADMIN_STATE(R_ADMIN));
        if (!Dm.truthy(picked))
            return null;
        if (Dm.eq(picked, random))
            return candidates;
        return choices[picked] is mob chosen && !QDELETED(chosen) ? Dm.list(chosen) : null;
    }

    /**
     * Called when someone latejoins
     * (This could be a signal in the future)
     */
    // tg: code/controllers/subsystem/dynamic/dynamic.dm:452
    public virtual async DmTask on_latejoin(mob_living_carbon_human latejoiner = null)
    {
        // First check queued rulesets - queued rulesets by pass cooldowns and probability checks,
        // because they're generally forced by events or admins (and thus have higher priority)
        foreach (datum_dynamic_ruleset_latejoin queued in Dm.each<datum_dynamic_ruleset_latejoin>(queued_rulesets))
        {
            // NOTE: !! THIS CAN SLEEP !!
            if (!Dm.truthy(await queued.prepare_execution(Dm.num(get_active_player_count(afk_check: TRUE)), Dm.list(latejoiner))))
            {
                message_admins(Dm.text("Latejoin (forced): Queued ruleset [] failed to prepare! It remains queued for next latejoin. (<a href='byond://?src=[];admin_dequeue=[]'>REMOVE FROM QUEUE</a>)", queued.config_tag, REF(this), REF(queued)));
                log_dynamic(Dm.text("Latejoin (forced): Queued ruleset [] failed to prepare! It remains queued for next latejoin.", queued.config_tag));
                continue;
            }
            message_admins(Dm.text("Latejoin (forced): [] has been selected for [].", ADMIN_LOOKUPFLW(latejoiner), queued.config_tag));
            log_dynamic(Dm.text("Latejoin (forced): [] has been selected for [].", key_name(latejoiner), queued.config_tag));
            unqueue_ruleset(queued);
            Dm.append(ref executed_rulesets, queued);
            await queued.execute();
            return;
        }

        if (latejoin_ruleset_start <= world.time && latejoin_cooldown <= world.time) // COOLDOWN_FINISHED of both
        {
            if (Dm.truthy(await try_spawn_latejoin(latejoiner)))
                return;
        }
    }

    /**
     * Invoked by SSdynamic to try to spawn a latejoin ruleset
     * Respects ranges and thresholds
     *
     * Returns TRUE if a ruleset was spawned, FALSE otherwise
     */
    // tg: code/controllers/subsystem/dynamic/dynamic.dm:478
    public virtual async DmTask<object> try_spawn_latejoin(mob_living_carbon_human latejoiner = null)
    {
        if (Dm.le(rulesets_to_spawn[LATEJOIN], 0))
            return FALSE;
        object latejoin_chance = get_latejoin_chance();
        if (!prob(Dm.num(latejoin_chance)))
        {
            log_dynamic(Dm.text("Latejoin: Ruleset chance failed ([]% chance)", latejoin_chance));
            return FALSE;
        }

        object player_count = get_active_player_count(afk_check: TRUE);
        DmList rulesets_weighted = get_latejoin_rulesets(player_count) as DmList;
        // Note, we make no effort to actually pick a valid ruleset here
        // We pick a ruleset, and they player might not even have that antag selected. And that's fine
        datum_dynamic_ruleset_latejoin picked_ruleset = pick_weight(rulesets_weighted) as datum_dynamic_ruleset_latejoin;
        if (picked_ruleset == null)
        {
            log_dynamic("Latejoin: No rulesets to pick from!");
            return FALSE;
        }
        // NOTE: !! THIS CAN SLEEP !!
        if (!Dm.truthy(await picked_ruleset.prepare_execution(Dm.num(player_count), Dm.list(latejoiner))))
        {
            log_dynamic(Dm.text("Latejoin: Selected ruleset [] for [], but preparation failed! Latejoin chance has increased. []", picked_ruleset.name, key_name(latejoiner), picked_ruleset.log_data));
            QDEL_LIST(rulesets_weighted);
            failed_latejoins++;
            return FALSE;
        }
        // Run the thing
        Dm.append(ref executed_rulesets, picked_ruleset);
        Dm.remove(ref rulesets_weighted, picked_ruleset);
        await picked_ruleset.execute();
        // Post execute logging
        if (!Dm.@in(latejoiner.mind, picked_ruleset.selected_minds))
            stack_trace(Dm.text("Dynamic: Latejoin [] executed, but the latejoiner was not in its selected minds list!", picked_ruleset.type), "code/controllers/subsystem/dynamic/dynamic.dm", 507);
        message_admins(Dm.text("Latejoin: [] has been selected for [].", ADMIN_LOOKUPFLW(latejoiner), picked_ruleset.config_tag));
        log_dynamic(Dm.text("Latejoin: [] has been selected for [].", key_name(latejoiner), picked_ruleset.config_tag));
        // Clean up unused rulesets
        QDEL_LIST(rulesets_weighted);
        Dm.remove_at(rulesets_to_spawn, LATEJOIN, 1);
        failed_latejoins = 0;
        admin_forcing_next_latejoin = FALSE;
        latejoin_cooldown = world.time + Dm.num(get_ruleset_cooldown(LATEJOIN)); // COOLDOWN_START(src, latejoin_cooldown, get_ruleset_cooldown(LATEJOIN))
        return TRUE;
    }

    /// Gets a weighted list of latejoin rulesets
    // tg: code/controllers/subsystem/dynamic/dynamic.dm:519
    public virtual object get_latejoin_rulesets(object player_count = null)
    {
        // PRIVATE_PROC(TRUE)

        DmList rulesets = Dm.list();
        foreach (object ruleset_type in Dm.each(subtypesof(typeof(datum_dynamic_ruleset_latejoin))))
        {
            datum_dynamic_ruleset_latejoin ruleset = Dm.@new((Type)ruleset_type, dynamic_config) as datum_dynamic_ruleset_latejoin;
            rulesets[ruleset] = ruleset.get_weight(Dm.num(player_count), current_tier.tier);
        }

        return rulesets;
    }

    /**
     * Queues a ruleset to run on roundstart or next latejoin which fulfills all requirements
     *
     * For example, if you queue a latejoin revolutionary, it'll only run when population gets large enough and there are enough heads of staff
     * For all latejoins until then, it will simply do nothing
     *
     * * latejoin_type - The type of latejoin ruleset to force
     */
    // tg: code/controllers/subsystem/dynamic/dynamic.dm:537
    public virtual async DmTask queue_ruleset(object ruleset_typepath = null)
    {
        if (!ispath(ruleset_typepath, typeof(datum_dynamic_ruleset_latejoin)) && !ispath(ruleset_typepath, typeof(datum_dynamic_ruleset_roundstart)))
            throw Dm.CRASH(Dm.text("queue_ruleset() was called with an invalid type: []", ruleset_typepath), "code/controllers/subsystem/dynamic/dynamic.dm", 539);

        Dm.append(ref queued_rulesets, await Dm.new_async((Type)ruleset_typepath, dynamic_config));
    }

    /**
     * Unqueues a ruleset because it has executed
     */
    // tg: code/controllers/subsystem/dynamic/dynamic.dm:546
    public virtual void unqueue_ruleset(datum_dynamic_ruleset ruleset = null)
    {
        if (!istype(ruleset, typeof(datum_dynamic_ruleset_latejoin)) && !istype(ruleset, typeof(datum_dynamic_ruleset_roundstart)))
            throw Dm.CRASH(Dm.text("queue_ruleset() was called with an invalid type: []", ruleset.type), "code/controllers/subsystem/dynamic/dynamic.dm", 548);

        Dm.remove(ref queued_rulesets, ruleset);
    }

    /**
     * Get the cooldown between attempts to spawn a ruleset of the given type
     */
    // tg: code/controllers/subsystem/dynamic/dynamic.dm:555
    public virtual object get_ruleset_cooldown(object range = null)
    {
        if (Dm.eq(range, ROUNDSTART))
        {
            stack_trace("Attempting to get cooldown for roundstart rulesets - this is redundant and is likely an error", "code/controllers/subsystem/dynamic/dynamic.dm", 557);
            return 0;
        }

        DmList settings = current_tier.ruleset_type_settings[range] as DmList;
        object low = settings[EXECUTION_COOLDOWN_LOW] is var l && Dm.truthy(l) ? l : 0f;
        object high = settings[EXECUTION_COOLDOWN_HIGH] is var h && Dm.truthy(h) ? h : 0f;
        return rand(Dm.num(low), Dm.num(high));
    }

    /**
     * Gets the chance of a midround ruleset being selected
     */
    // tg: code/controllers/subsystem/dynamic/dynamic.dm:567
    public virtual object get_midround_chance(object range = null)
    {
        if (Dm.truthy(admin_forcing_next_light) && Dm.eq(range, LIGHT_MIDROUND))
            return 100;
        if (Dm.truthy(admin_forcing_next_heavy) && Dm.eq(range, HEAVY_MIDROUND))
            return 100;

        float chance = 0;
        float num_antags = Dm.length(GLOB.current_living_antags);
        float num_dead = Dm.length(GLOB.dead_player_list);
        float num_alive = Dm.num(get_active_player_count(afk_check: TRUE));
        if (num_dead + num_alive <= 0)
            return 0;

        chance += 100 - (200 * (num_dead / (num_alive + num_dead)));
        if (num_antags < 0)
            chance += 50;

        return chance;
    }

    /**
     * Gets the chance of a latejoin ruleset being selected
     */
    // tg: code/controllers/subsystem/dynamic/dynamic.dm:589
    public virtual object get_latejoin_chance()
    {
        if (Dm.truthy(admin_forcing_next_latejoin))
            return 100;

        float chance = 0;
        float num_antags = Dm.length(GLOB.current_living_antags);
        float num_dead = Dm.length(GLOB.dead_player_list);
        float num_alive = Dm.num(get_active_player_count(afk_check: TRUE));
        if (num_dead + num_alive <= 0)
            return 0;

        chance += 100 - (200 * (num_dead / (num_alive + num_dead)));
        if (num_antags < 0)
            chance += 50;
        chance += failed_latejoins * 15;
        // Reduced chance before lights start
        if (!(light_ruleset_start <= world.time)) // COOLDOWN_FINISHED(src, light_ruleset_start)
            chance *= 0.2f;

        return chance;
    }

    // tg: code/controllers/subsystem/dynamic/dynamic.dm:610
    public virtual void set_round_result()
    {
        // If it got to this part, just pick one high impact ruleset if it exists
        foreach (datum_dynamic_ruleset rule in Dm.each_cast<datum_dynamic_ruleset>(executed_rulesets))
        {
            if (Dm.truthy(rule.round_result()))
                return;
        }

        SSticker.mode_result = "undefined";

        object revolution_result = GLOB.revolution_handler?.result; // gaps V1
        if (Dm.eq(revolution_result, STATION_VICTORY))
        {
            SSticker.mode_result = "loss - rev heads killed";
            SSticker.news_report = REVS_LOSE;
        }
        else if (Dm.eq(revolution_result, REVOLUTION_VICTORY))
        {
            SSticker.mode_result = "win - heads killed";
            SSticker.news_report = REVS_WIN;
        }

        // Something nuked the station - it wasn't nuke ops (they set their own via their rulset)
        if (Dm.truthy(GLOB.station_was_nuked))
            SSticker.news_report = STATION_NUKED;

        if (Dm.truthy(SSsupermatter_cascade.cascade_initiated))
            SSticker.news_report = SUPERMATTER_CASCADE;

        // Only show this one if we have nothing better to show
        if (datum_controller_subsystem_shuttle.EMERGENCY_ESCAPED_OR_ENDGAMED() && !Dm.truthy(SSticker.news_report))
            SSticker.news_report = Dm.truthy(SSshuttle.emergency?.is_hijacked()) ? SHUTTLE_HIJACK : STATION_EVACUATED;
    }

    /// Helper to clear all queued rulesets and stop any other rulesets from naturally spawning
    // tg: code/controllers/subsystem/dynamic/dynamic.dm:638
    public virtual void force_extended()
    {
        foreach (object category in Dm.each(rulesets_to_spawn))
            rulesets_to_spawn[category] = 0;
        QDEL_LIST(queued_rulesets);
    }

    // tg: code/controllers/subsystem/dynamic/dynamic.dm:643
    public override async DmTask<object> Topic(object href = null, DmList href_list = null)
    {
        object dot = await base.Topic(href, href_list);
        if (Dm.truthy(href_list["admin_dequeue"]))
        {
            if (!Dm.truthy(check_rights(R_ADMIN)))
                return dot;
            // locate(href_list["admin_dequeue"]) in queued_rulesets
            object located = Dm.locate_ref(href_list["admin_dequeue"]);
            datum_dynamic_ruleset to_remove = Dm.@in(located, queued_rulesets) ? located as datum_dynamic_ruleset : null;
            if (!istype(to_remove, typeof(datum_dynamic_ruleset)))
                return dot;
            unqueue_ruleset(to_remove);
            qdel(to_remove);
            message_admins(span_adminnotice(Dm.text("[] [] from the latejoin queue.", key_name_admin(usr), to_remove.config_tag)));
            log_admin(Dm.text("[] removed [] from the latejoin queue.", key_name(usr), to_remove.config_tag));
            return dot;
        }

        if (Dm.truthy(href_list["admin_reroll"]))
        {
            if (!Dm.truthy(check_rights(R_ADMIN)) || Dm.truthy(midround_admin_reroll))
                return dot;
            if (midround_admin_cancel_period <= world.time) // COOLDOWN_FINISHED(src, midround_admin_cancel_period)
            {
                to_chat(usr, span_alert("Too late!"));
                return dot;
            }
            midround_admin_reroll = TRUE;
            message_admins(span_adminnotice(Dm.text("[] rerolled the queued midround ruleset.", key_name_admin(usr))));
            log_admin(Dm.text("[] rerolled the queued midround ruleset.", key_name(usr)));
            return dot;
        }

        if (Dm.truthy(href_list["admin_cancel_midround"]))
        {
            if (!Dm.truthy(check_rights(R_ADMIN)) || Dm.truthy(midround_admin_cancel))
                return dot;
            if (midround_admin_cancel_period <= world.time) // COOLDOWN_FINISHED(src, midround_admin_cancel_period)
            {
                to_chat(usr, span_alert("Too late!"));
                return dot;
            }
            midround_admin_cancel = TRUE;
            message_admins(span_adminnotice(Dm.text("[] cancelled the queued midround ruleset.", key_name_admin(usr))));
            log_admin(Dm.text("[] cancelled the queued midround ruleset.", key_name(usr)));
            return dot;
        }
        return dot;
    }

    /// Puts all repo defaults into a dynamic.toml file
    // tg: code/controllers/subsystem/dynamic/dynamic.dm:686 (#ifdef TESTING: tg's debug build only; the port translates it for the config's
    // regeneration, called by no game code)
    public virtual object build_dynamic_toml()
    {
        string data = "";
        foreach (object tier_type in Dm.each(subtypesof(typeof(datum_dynamic_tier))))
        {
            datum_dynamic_tier tier = Dm.@new((Type)tier_type) as datum_dynamic_tier;
            if (!Dm.truthy(tier.config_tag))
            {
                qdel(tier);
                continue;
            }

            data += Dm.text("[\"[]\"]\n", tier.config_tag);
            data += Dm.text("name = \"[]\"\n", tier.name);
            data += Dm.text("min_pop = []\n", tier.min_pop);
            data += Dm.text("weight = []\n", tier.weight);
            data += Dm.text("advisory_report = \"[]\"\n", tier.advisory_report);
            foreach (object range in Dm.each(tier.ruleset_type_settings))
            {
                DmList settings = tier.ruleset_type_settings[range] as DmList;
                data += Dm.text("ruleset_type_settings.[].[] = []\n", range, LOW_END, settings?[LOW_END] is var low && Dm.truthy(low) ? low : 0f);
                data += Dm.text("ruleset_type_settings.[].[] = []\n", range, HIGH_END, settings?[HIGH_END] is var high && Dm.truthy(high) ? high : 0f);
                data += Dm.text("ruleset_type_settings.[].[] = []\n", range, HALF_RANGE_POP_THRESHOLD, settings?[HALF_RANGE_POP_THRESHOLD] is var half && Dm.truthy(half) ? half : 0f);
                data += Dm.text("ruleset_type_settings.[].[] = []\n", range, FULL_RANGE_POP_THRESHOLD, settings?[FULL_RANGE_POP_THRESHOLD] is var full && Dm.truthy(full) ? full : 0f);
                if (!Dm.eq(range, ROUNDSTART))
                {
                    data += Dm.text("ruleset_type_settings.[].[] = []\n", range, TIME_THRESHOLD, Dm.num(settings?[TIME_THRESHOLD] is var t && Dm.truthy(t) ? t : 0f) / 60 / 10);
                    data += Dm.text("ruleset_type_settings.[].[] = []\n", range, EXECUTION_COOLDOWN_LOW, Dm.num(settings?[EXECUTION_COOLDOWN_LOW] is var cl && Dm.truthy(cl) ? cl : 0f) / 60 / 10);
                    data += Dm.text("ruleset_type_settings.[].[] = []\n", range, EXECUTION_COOLDOWN_HIGH, Dm.num(settings?[EXECUTION_COOLDOWN_HIGH] is var ch && Dm.truthy(ch) ? ch : 0f) / 60 / 10);
                }
            }

            data += "\n";
            qdel(tier);
        }

        foreach (object ruleset_type in Dm.each(subtypesof(typeof(datum_dynamic_ruleset))))
        {
            datum_dynamic_ruleset ruleset = Dm.@new((Type)ruleset_type) as datum_dynamic_ruleset;
            if (!Dm.truthy(ruleset.config_tag))
            {
                qdel(ruleset);
                continue;
            }

            data += Dm.text("[\"[]\"]\n", ruleset.config_tag);
            if (ruleset.weight is DmList weights)
            {
                for (int i = 1; i <= Dm.length(weights); i++)
                    data += Dm.text("weight.[] = []\n", i, weights[i]);
            }
            else
                data += Dm.text("weight = []\n", Dm.truthy(ruleset.weight) ? ruleset.weight : 0f);
            if (ruleset.min_pop is DmList pops)
            {
                for (int i = 1; i <= Dm.length(pops); i++)
                    data += Dm.text("min_pop.[] = []\n", i, pops[i]);
            }
            else
                data += Dm.text("min_pop = []\n", Dm.truthy(ruleset.min_pop) ? ruleset.min_pop : 0f);
            if (Dm.truthy(Dm.length(ruleset.blacklisted_roles)))
            {
                data += "blacklisted_roles = [\n";
                foreach (object i in Dm.each(ruleset.blacklisted_roles))
                    data += Dm.text("\t\"[]\",\n", i);
                data += "]\n";
            }
            else
                data += "blacklisted_roles = []\n";
            if (!istype(ruleset, typeof(datum_dynamic_ruleset_latejoin)))
            {
                if (ruleset.min_antag_cap is DmList min_caps)
                {
                    foreach (object ruleset_min_antag_cap in Dm.each(min_caps))
                        data += Dm.text("min_antag_cap.[] = []\n", ruleset_min_antag_cap, min_caps[ruleset_min_antag_cap]);
                }
                else
                    data += Dm.text("min_antag_cap = []\n", Dm.truthy(ruleset.min_antag_cap) ? ruleset.min_antag_cap : 0f);
                if (ruleset.max_antag_cap is DmList max_caps)
                {
                    foreach (object ruleset_max_antag_cap in Dm.each(max_caps))
                        data += Dm.text("max_antag_cap.[] = []\n", ruleset_max_antag_cap, max_caps[ruleset_max_antag_cap]);
                }
                else if (ruleset.max_antag_cap != null)
                    data += Dm.text("max_antag_cap = []\n", ruleset.max_antag_cap);
                else
                    data += "# max_antag_cap = min_antag_cap\n";
            }
            data += Dm.text("repeatable_weight_decrease = []\n", ruleset.repeatable_weight_decrease);
            data += Dm.text("repeatable = []\n", ruleset.repeatable);
            data += Dm.text("minimum_required_age = []\n", ruleset.minimum_required_age);
            data += "\n";
            qdel(ruleset);
        }

        string filepath = Dm.text("[]/dynamic.toml", config.directory);
        fdel(Dm.file(filepath));
        text2file(data, filepath);
        return TRUE;
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/dynamic/dynamic.dm:1  SUBSYSTEM_DEF(dynamic): GLOBAL_REAL(SSdynamic, /datum/controller/subsystem/dynamic)
    internal datum_controller_subsystem_dynamic SSdynamic;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_dynamic SSdynamic => ref TgState.Current.SSdynamic;
}
