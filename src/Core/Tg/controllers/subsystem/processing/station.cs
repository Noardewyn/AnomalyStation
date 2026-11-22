// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/processing/station.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps R1 (D327): the station goals (goals_by_type, generate_station_goals, get_station_goals, get_station_goal). Gaps R2 (D327): the
// station traits (SetupTraits, pick_traits, setup_trait, Recover) and the processing of the traits that process. Not here: the
// parallax's post-setup (SSparallax: the 3D view has no parallax, Dm.Deferred).
// DEVIATION(DV432): a random event modifier trait whose event is not ported (inert, so not in SSevents.control) is left out of the
// pool as one whose event cannot run on the map, and so is a trait whose whole effect is another system not ported yet
// (TraitsWaitingForTheirSystem). A world booted without tg's configuration file (the toy and test worlds) forbids
// the station traits (TgWorld.Boot), as tg's unit test build skips them (:24).

// tg: code/controllers/subsystem/processing/station.dm:1 PROCESSING_SUBSYSTEM_DEF(station)
[DmPath("/datum/controller/subsystem/processing/station")]
public partial class datum_controller_subsystem_processing_station : datum_controller_subsystem_processing
{
    // tg: code/controllers/subsystem/processing/station.dm:2
    public datum_controller_subsystem_processing_station()
    {
        name = "Station";
        ss_flags = SS_BACKGROUND;
        runlevels = RUNLEVEL_GAME;
        wait = 5 * SECONDS;
    }

    ///Currently active announcer. Starts as a type but gets initialized after traits are selected
    // tg: code/controllers/subsystem/processing/station.dm:12  (a type path until Initialize makes the datum)
    public object announcer = typeof(datum_centcom_announcer_default);

    // tg: code/controllers/subsystem/processing/station.dm:21
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        //If doing unit tests we don't do none of that trait shit ya know?
        // Autowiki also wants consistent outputs, for example making sure the vending machine page always reports the normal products
        // tg: #if !defined(UNIT_TESTS) && !defined(AUTOWIKI) (both undefined in production, rules 1.7)
        await SetupTraits();

        announcer = Dm.@new((Type)announcer); //Initialize the station's announcer datum
        // SSparallax.post_station_setup(): the random parallax layer's global effects (the radioactive nebula's green starlight); the
        // 3D view has no parallax layers (SSparallax is not ported)
        Dm.Deferred("code/controllers/subsystem/processing/station.dm:29", "Initialize: SSparallax.post_station_setup, the parallax layer's global effects (no parallax in the 3D view)");

        return SS_INIT_SUCCESS;
    }

    /// This gets called by SSdynamic during initial gamemode setup.
    /// This is done because for a greenshift we want all goals to be generated
    // tg: code/controllers/subsystem/processing/station.dm:44
    public virtual async DmTask generate_station_goals(object goal_budget = null)
    {
        DmList possible = subtypesof(typeof(datum_station_goal));

        float goal_weights = 0;
        DmList chosen_goals = Dm.list();
        object is_planetary = SSmapping.is_planetary();
        while (Dm.truthy(possible.len) && goal_weights < Dm.num(goal_budget))
        {
            Type picked = pick_n_take(possible) as Type;
            if (Dm.truthy(Dm.initial(picked, (datum_station_goal p) => p.requires_space)) && Dm.truthy(is_planetary))
                continue;

            goal_weights += Dm.num(Dm.initial(picked, (datum_station_goal p) => p.weight));
            Dm.append(ref chosen_goals, picked);
        }

        foreach (object chosen in Dm.each(chosen_goals))
            Dm.@new((Type)chosen);
    }

    /// Returns all station goals that are currently active
    // tg: code/controllers/subsystem/processing/station.dm:62
    public virtual object get_station_goals()
    {
        DmList goals = Dm.list();
        foreach (object goal_type in Dm.each(goals_by_type))
            Dm.append(ref goals, goals_by_type[goal_type]);
        return goals;
    }

    /// Returns a specific station goal by type
    // tg: code/controllers/subsystem/processing/station.dm:69
    public virtual object get_station_goal(object goal_type = null)
    {
        return goals_by_type[goal_type];
    }

    // tg: code/controllers/subsystem/processing/station.dm:33
    public override void Recover()
    {
        station_traits = SSstation.station_traits;
        selectable_traits_by_types = SSstation.selectable_traits_by_types;
        announcer = SSstation.announcer;
        antag_protected_roles = SSstation.antag_protected_roles;
        antag_restricted_roles = SSstation.antag_restricted_roles;
        goals_by_type = SSstation.goals_by_type;
        base.Recover();
    }

    ///Rolls for the amount of traits and adds them to the traits list
    // tg: code/controllers/subsystem/processing/station.dm:73
    public virtual async DmTask SetupTraits()
    {
        if (Dm.truthy(config.Get(typeof(datum_config_entry_flag_forbid_station_traits)))) /* CONFIG_GET(flag/forbid_station_traits) */
            return;

        if (Dm.truthy(fexists(FUTURE_STATION_TRAITS_FILE)))
        {
            string forced_traits_contents = file2text(FUTURE_STATION_TRAITS_FILE);
            fdel(FUTURE_STATION_TRAITS_FILE);

            object forced_traits_text_paths = json_decode(forced_traits_contents);
            forced_traits_text_paths = SANITIZE_LIST(forced_traits_text_paths);

            foreach (object trait_text_path in Dm.each(forced_traits_text_paths))
            {
                Type station_trait_path = trait_text_path is string text ? text2path(text) : trait_text_path as Type;
                if (!ispath(station_trait_path, typeof(datum_station_trait)) || station_trait_path == typeof(datum_station_trait))
                {
                    object message = Dm.text("Invalid station trait path [] was requested in the future station traits!", station_trait_path);
                    log_game(message);
                    message_admins(message);
                    continue;
                }

                setup_trait(station_trait_path);
            }

            return;
        }

        foreach (Type trait_typepath in Dm.each_cast<Type>(valid_subtypesof(typeof(datum_station_trait))))
        {
            // If forced, (probably debugging), just set it up now, keep it out of the pool.
            if (Dm.truthy(Dm.initial(trait_typepath, (datum_station_trait p) => p.force)))
            {
                setup_trait(trait_typepath);
                continue;
            }

            float trait_flags = Dm.initial(trait_typepath, (datum_station_trait p) => p.trait_flags);
            if (!Dm.truthy(Dm.bitand(trait_flags, STATION_TRAIT_PLANETARY)) && Dm.truthy(SSmapping.is_planetary())) // we're on a planet but we can't do planet ;_;
                continue;

            if (!Dm.truthy(Dm.bitand(trait_flags, STATION_TRAIT_SPACE_BOUND)) && !Dm.truthy(SSmapping.is_planetary())) //we're in space but we can't do space ;_;
                continue;

            if (!Dm.truthy(Dm.bitand(trait_flags, STATION_TRAIT_REQUIRES_AI)) && !Dm.truthy(config.Get(typeof(datum_config_entry_flag_allow_ai)))) //can't have AI traits without AI
                continue;

            if (ispath(trait_typepath, typeof(datum_station_trait_random_event_weight_modifier))) //Don't add event modifiers for events that can't occur on our map.
            {
                Type event_control_type = Dm.initial(trait_typepath, (datum_station_trait_random_event_weight_modifier p) => p.event_control_path);
                if (event_control_type != null)
                {
                    var event_to_check = await Dm.new_async(event_control_type) as datum_round_event_control;
                    if (!Dm.truthy(event_to_check.valid_for_map()))
                        continue;
                    // DEVIATION(DV432): an event not ported (its control or its event inert) runs nowhere, as an event of another map
                    if (InertSeam.IsInertType(event_control_type) || (event_to_check.typepath != null && InertSeam.IsInertType(event_to_check.typepath)))
                        continue;
                }
            }

            // DEVIATION(DV432): a trait whose whole effect is a system not ported yet (inert) stays out of the pool until that system lands
            if (WaitsForItsSystem(trait_typepath))
                continue;

            DmList bucket = selectable_traits_by_types[Dm.initial(trait_typepath, (datum_station_trait p) => p.trait_type)] as DmList;
            bucket[trait_typepath] = Dm.initial(trait_typepath, (datum_station_trait p) => p.weight);
        }

        object positive_trait_budget = text2num(pick_weight(config.Get(typeof(datum_config_entry_keyed_list_positive_station_traits)) as DmList)); /* CONFIG_GET(keyed_list/positive_station_traits) */
        object neutral_trait_budget = text2num(pick_weight(config.Get(typeof(datum_config_entry_keyed_list_neutral_station_traits)) as DmList)); /* CONFIG_GET(keyed_list/neutral_station_traits) */
        object negative_trait_budget = text2num(pick_weight(config.Get(typeof(datum_config_entry_keyed_list_negative_station_traits)) as DmList)); /* CONFIG_GET(keyed_list/negative_station_traits) */

        // tg: #ifdef MAP_TEST ... #endif not translated (inactive in production)

        pick_traits(STATION_TRAIT_POSITIVE, positive_trait_budget);
        pick_traits(STATION_TRAIT_NEUTRAL, neutral_trait_budget);
        pick_traits(STATION_TRAIT_NEGATIVE, negative_trait_budget);
    }

    /// DEVIATION(DV432), gaps R2: the station traits whose whole effect is a system the port has not ported yet, each with the type of
    /// that system. While the type is inert the trait is left out of the random pool (an admin may still force it, as the admin panel
    /// or the future traits file do); it enters the pool by itself when the type turns live. Not tg code: tg has every system.
    internal static readonly System.Collections.ObjectModel.ReadOnlyCollection<(Type Trait, Type System)> TraitsWaitingForTheirSystem = Array.AsReadOnly(new (Type, Type)[]
    {
        (typeof(datum_station_trait_nebula_hostile_radiation), typeof(datum_controller_subsystem_radioactive_nebula)), // the radioactive nebula and its shielders
        (typeof(datum_station_trait_pet_day), typeof(datum_pet_customization)), // the pet customization
        (typeof(datum_station_trait_job_veteran_advisor), typeof(datum_job_veteran_advisor)), // the station trait jobs not ported yet
        (typeof(datum_station_trait_job_human_ai), typeof(datum_job_human_ai)),
    });

    /// Whether <paramref name="trait_typepath"/> is one of <see cref="TraitsWaitingForTheirSystem"/> whose system is still inert.
    internal static bool WaitsForItsSystem(Type trait_typepath)
    {
        foreach ((Type trait, Type system) in TraitsWaitingForTheirSystem)
        {
            if (trait == trait_typepath)
                return InertSeam.IsInertType(system);
        }
        return false;
    }

    /**
     * Picks traits of a specific category (e.g. bad or good), initializes them, adds them to the list of traits,
     * then removes them from possible traits as to not roll twice and subtracts their cost from the budget.
     * All until the whole budget is spent or no more traits can be picked with it.
     */
    // tg: code/controllers/subsystem/processing/station.dm:141
    public virtual void pick_traits(object trait_sign = null, object budget = null)
    {
        if (!Dm.truthy(budget))
            return;
        ///A list of traits of the same trait sign
        DmList selectable_traits = selectable_traits_by_types[trait_sign] as DmList;
        while (Dm.truthy(budget))
        {
            ///Remove any station trait with a cost bigger than the budget
            foreach (Type proto_trait in Dm.each_cast<Type>(selectable_traits))
            {
                if (Dm.initial(proto_trait, (datum_station_trait p) => p.cost) > Dm.num(budget))
                    Dm.remove(ref selectable_traits, proto_trait);
            }
            ///We have spare budget but no trait that can be bought with what's left of it
            if (!Dm.truthy(length(selectable_traits)))
                return;
            //Rolls from the table for the specific trait type
            Type trait_type = pick_weight(selectable_traits) as Type;
            Dm.remove(ref selectable_traits, trait_type);
            budget = Dm.num(budget) - Dm.initial(trait_type, (datum_station_trait p) => p.cost);
            setup_trait(trait_type);
        }
    }

    ///Creates a given trait of a specific type, while also removing any blacklisted ones from the future pool.
    // tg: code/controllers/subsystem/processing/station.dm:161
    public virtual void setup_trait(Type trait_type = null)
    {
        if (Dm.locate_type(trait_type, station_traits) != null)
            return;
        var trait_instance = Dm.@new(trait_type) as datum_station_trait;
        Dm.append(ref station_traits, trait_instance);
        log_game(Dm.text("Station Trait: [] chosen for this round.", trait_instance.name));
        if (!Dm.truthy(trait_instance.blacklist))
            return;
        foreach (Type trait_to_remove in Dm.each_cast<Type>(trait_instance.blacklist))
        {
            DmList bucket = selectable_traits_by_types[Dm.initial(trait_to_remove, (datum_station_trait p) => p.trait_type)] as DmList;
            Dm.remove(ref bucket, trait_to_remove);
        }
    }

    // The New() that PROCESSING_SUBSYSTEM_DEF(station) defines (__DEFINES/MC.dm), with NEW_SS_GLOBAL(SSstation) (__DEFINES/MC.dm:29)
    // expanded inline (it reads src).
    // tg: code/controllers/subsystem/processing/station.dm:1
    public override async DmTask New(params object[] __extra)
    {
        if (SSstation != this)
        {
            if (SSstation is datum_controller_subsystem_processing_station)
            {
                Recover();
                qdel(SSstation);
            }
            SSstation = this;
        }
        PreInit();
    }

    // The fire() that PROCESSING_SUBSYSTEM_DEF(station) defines (__DEFINES/MC.dm:120): ..() /*just so it shows up on the profiler*/
    // tg: code/controllers/subsystem/processing/station.dm:1
    [DmParams()]
    public override async DmTask fire(float? resumed = null, params object[] __extra)
    {
        await base.fire(resumed, __extra); /*just so it shows up on the profiler*/
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/processing/station.dm:1  PROCESSING_SUBSYSTEM_DEF(station): GLOBAL_REAL(SSstation, /datum/controller/subsystem/processing/station)
    internal datum_controller_subsystem_processing_station SSstation;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_processing_station SSstation => ref TgState.Current.SSstation;
}
