// Translated from tgstation (snapshot 1d99bd7f): code/modules/explorer_drone/exodrone.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the exploration drone (its travel on its launcher's fuel, its cargo shared by its tools and its loot, its log,
// its adventures and events at a site, its landing back home), its launcher with the fuel pellet and the pellets. The var blocks are
// generated (D111) but for the drone's location, a site, which the generated typepath field cannot hold.

public partial class datum_controller_global_vars
{
    /// All exodrones.
    // tg: code/modules/explorer_drone/exodrone.dm:17  GLOBAL_LIST_EMPTY(exodrones)
    public DmList exodrones;

    // tg: code/modules/explorer_drone/exodrone.dm:17  (GLOBAL_MANAGED(exodrones, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalexodrones()
    {
        exodrones = Dm.list();
        Dm.append(ref gvars_datum_init_order, "exodrones");
    }

    /// All exodrone launchers.
    // tg: code/modules/explorer_drone/exodrone.dm:19  GLOBAL_LIST_EMPTY(exodrone_launchers)
    public DmList exodrone_launchers;

    // tg: code/modules/explorer_drone/exodrone.dm:19  (GLOBAL_MANAGED(exodrone_launchers, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalexodrone_launchers()
    {
        exodrone_launchers = Dm.list();
        Dm.append(ref gvars_datum_init_order, "exodrone_launchers");
    }
}

/// Exploration drone
// tg: code/modules/explorer_drone/exodrone.dm:22
public partial class obj_item_exodrone : obj_item
{
    /// How many lines of log we keep
    // tg: code/modules/explorer_drone/exodrone.dm:2  #define EXODRONE_LOG_SIZE 15 (file-local, #undef at :489)
    private const int EXODRONE_LOG_SIZE = 15;
    /// Size of drone storage shared between loot and tools.
    // tg: code/modules/explorer_drone/exodrone.dm:4  #define EXODRONE_CARGO_SLOTS 6 (file-local, #undef at :490)
    private const int EXODRONE_CARGO_SLOTS = 6;

    // Fuel types and travel time per unit of distance on that fuel.
    // tg: code/modules/explorer_drone/exodrone.dm:7  #define FUEL_BASIC "basic" (file-local, #undef at :491; the launcher reads these)
    internal const string FUEL_BASIC = "basic";
    // tg: code/modules/explorer_drone/exodrone.dm:8  #define BASIC_FUEL_TIME_COST 250 (file-local, #undef at :492)
    internal const int BASIC_FUEL_TIME_COST = 250;
    // tg: code/modules/explorer_drone/exodrone.dm:10  #define FUEL_ADVANCED "advanced" (file-local, #undef at :493)
    internal const string FUEL_ADVANCED = "advanced";
    // tg: code/modules/explorer_drone/exodrone.dm:11  #define ADVANCED_FUEL_TIME_COST 200 (file-local, #undef at :494)
    internal const int ADVANCED_FUEL_TIME_COST = 200;
    // tg: code/modules/explorer_drone/exodrone.dm:13  #define FUEL_EXOTIC "exotic" (file-local, #undef at :495)
    internal const string FUEL_EXOTIC = "exotic";
    // tg: code/modules/explorer_drone/exodrone.dm:14  #define EXOTIC_FUEL_TIME_COST 100 (file-local, #undef at :496)
    internal const int EXOTIC_FUEL_TIME_COST = 100;

    /// Site we're currently at, null means station. (A site: the generated field would be a typepath, rules 3.2 V3.)
    // tg: code/modules/explorer_drone/exodrone.dm:34
    public datum_exploration_site location;

    // tg: code/modules/explorer_drone/exodrone.dm:22  (the type's var overrides: the hand-written location moves the var block to the hand)
    public obj_item_exodrone()
    {
        name = "exploration drone";
        desc = "A long range, semi-autonomous exploration drone.";
        icon = Dm.resource("icons/obj/exploration.dmi");
        icon_state = "drone";
        w_class = WEIGHT_CLASS_BULKY;
    }

    /// Repeated drone name counter
    // tg: code/modules/explorer_drone/exodrone.dm:60  var/static/name_counter = list()
    public static ref DmList name_counter => ref TgState.Current.obj_item_exodrone__name_counter;

    /// Used to provide source to the regex replacement function. DO NOT MODIFY DIRECTLY
    // tg: code/modules/explorer_drone/exodrone.dm:62  var/static/obj/item/exodrone/_regex_context
    public static ref obj_item_exodrone _regex_context => ref TgState.Current.obj_item_exodrone___regex_context;

    // tg: code/modules/explorer_drone/exodrone.dm:64
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        name_counter ??= Dm.list();
        if (name == Dm.initial(typeof(obj_item_exodrone), (obj_item_exodrone p) => p.name))
        {
            name = (string)pick(strings(EXODRONE_FILE, "probe_names"));
            if (Dm.truthy(name_counter[name]))
            {
                name_counter[name] = Dm.num(name_counter[name]) + 1;
                name = Dm.text("[] \\Roman[]", name, name_counter[name]);
            }
            else
                name_counter[name] = 1;
        }
        else
            name = name;
        Dm.append(ref GLOB.exodrones, this);
        // Cargo storage
        Waiting.NoSleep(create_storage(max_slots: EXODRONE_CARGO_SLOTS, canthold: GLOB.blacklisted_cargo_types));
        return dot;
    }

    // tg: code/modules/explorer_drone/exodrone.dm:79
    public override async DmTask handle_deconstruct(float? disassembled = null)
    {
        await base.handle_deconstruct(disassembled);
        await explosion(this, 0, 0, 1, 1);
        do_sparks(5, FALSE, this);
    }

    // tg: code/modules/explorer_drone/exodrone.dm:84
    public override object Destroy(float? force = null)
    {
        Dm.remove(ref GLOB.exodrones, this);
        return base.Destroy(force);
    }

    /// Description for drone listing, describes location and current status
    // tg: code/modules/explorer_drone/exodrone.dm:89
    public virtual object ui_description()
    {
        switch (drone_status)
        {
            case EXODRONE_TRAVEL:
                return Dm.truthy(travel_target) ? I18n.T("Traveling to [].", travel_target.display_name()) : I18n.T("Traveling back to station.");
            case EXODRONE_EXPLORATION:
            case EXODRONE_ADVENTURE:
            case EXODRONE_BUSY:
                return I18n.T("Exploring [].", Dm.truthy(location?.display_name()) ? location.display_name() : "ERROR"); // better safe than sorry.
            case EXODRONE_IDLE:
                return I18n.T("Idle.");
        }
        return null;
    }

    // Searches for blacklisted items hidden inside containers
    // tg: code/modules/explorer_drone/exodrone.dm:99
    public virtual object check_blacklist()
    {
        foreach (obj_item contained_item in Dm.each<obj_item>(contents))
        {
            if (Dm.truthy(contained_item.contents))
            {
                foreach (object subcontained_object in Dm.each(contained_item.get_all_contents()))
                {
                    if (Dm.truthy(is_type_in_typecache(subcontained_object, GLOB.blacklisted_cargo_types)))
                        return FALSE;
                }
            }
        }
        return TRUE;
    }

    /// Starts travel for site, does not validate if it's possible
    // tg: code/modules/explorer_drone/exodrone.dm:108
    public virtual async DmTask launch_for(datum_exploration_site target_site = null)
    {
        if (!Dm.truthy(location)) //We're launching from station, fuel up
        {
            obj_machinery_exodrone_launcher pad = Dm.locate<obj_machinery_exodrone_launcher>(loc);
            await pad.fuel_up(this);
            await pad.launch_effect();
            last_pad = WEAKREF(pad) as datum_weakref;
            drone_log__proc(I18n.T("Launched from [] and set course for []", pad.name, target_site.display_name()));
        }
        else
            drone_log__proc(I18n.T("Launched from [] and set course for []", location.display_name(), Dm.truthy(target_site) ? target_site.display_name() : station_name()));
        set_status(EXODRONE_TRAVEL);
        await moveToNullspace();
        float distance_to_travel = Dm.truthy(target_site) ? target_site.distance : location.distance; //If we're going home distance is distance of our current location
        if (Dm.truthy(location) && Dm.truthy(target_site)) //Traveling site to site is faster (don't think too hard on 3d space logistics here)
            distance_to_travel = Dm.max(Dm.abs(target_site.distance - location.distance), 1);
        travel_target = target_site;
        travel_time = travel_cost_coeff * distance_to_travel;
        travel_timer_id = _addtimer(Dm.@new<datum_callback>(this, nameof(finish_travel)), Dm.num(travel_time), TIMER_STOPPABLE, file: "code/modules/explorer_drone/exodrone.dm", line: 124);
    }

    /// Travel cleanup
    // tg: code/modules/explorer_drone/exodrone.dm:127
    public virtual async DmTask finish_travel()
    {
        location = travel_target;
        travel_timer_id = null;
        travel_time = null;
        if (Dm.truthy(location)) //We're arriving at exploration site
        {
            location.on_drone_arrival(this);
            set_status(EXODRONE_EXPLORATION);
        }
        else
        {
            obj_machinery exodrone_launcher = find_landing_pad() as obj_machinery;
            if (Dm.truthy(exodrone_launcher))
            {
                await forceMove(get_turf(exodrone_launcher) as atom);
                drone_log__proc(I18n.T("Arrived at []. Landing at [].", station_name(), exodrone_launcher));
            }
            else
            {
                turf drop_zone = await drop_somewhere_on_station() as turf;
                drone_log__proc(I18n.T("Arrived at []. Emergency landing at [].", station_name(), drop_zone.loc.name));
            }
            set_status(EXODRONE_IDLE);
        }
    }

    // tg: code/modules/explorer_drone/exodrone.dm:144
    public virtual void set_status(object new_status = null)
    {
        SEND_SIGNAL(COMSIG_EXODRONE_STATUS_CHANGED, Dm.list(this));
        drone_status = (string)new_status;
    }

    /// Cargo space left
    // tg: code/modules/explorer_drone/exodrone.dm:149
    public virtual object space_left()
    {
        return EXODRONE_CARGO_SLOTS - length(contents) - length(tools);
    }

    /// Adds drone tool and resizes storage.
    // tg: code/modules/explorer_drone/exodrone.dm:153
    public virtual void add_tool(object tool_type = null)
    {
        if (Dm.num(space_left()) > 0 && Dm.@in(tool_type, GLOB.exodrone_tool_metadata))
        {
            Dm.append(ref tools, tool_type);
            update_storage_size();
        }
    }

    /// Removes drone tool and resizes storage.
    // tg: code/modules/explorer_drone/exodrone.dm:159
    public virtual void remove_tool(object tool_type = null)
    {
        Dm.remove(ref tools, tool_type);
        update_storage_size();
    }

    /// Resizes storage component depending on slots used by tools.
    // tg: code/modules/explorer_drone/exodrone.dm:164
    public virtual void update_storage_size()
    {
        atom_storage.max_slots = EXODRONE_CARGO_SLOTS - length(tools);
    }

    /// Builds ui data for drone storage.
    // tg: code/modules/explorer_drone/exodrone.dm:168
    public virtual object get_cargo_data()
    {
        DmList dot = Dm.list();
        foreach (object tool in Dm.each(tools))
            Dm.append(ref dot, Dm.list(Dm.list(Dm.kv("type", "tool"), Dm.kv("name", tool))));
        foreach (obj cargo in Dm.each<obj>(contents))
            Dm.append(ref dot, Dm.list(Dm.list(Dm.kv("type", "cargo"), Dm.kv("name", cargo.name), Dm.kv("ref", REF(cargo)))));
        for (float __ = 1, __end = Dm.num(space_left()); __ <= __end; __++)
            Dm.append(ref dot, Dm.list(Dm.list(Dm.kv("type", "empty"), Dm.kv("name", I18n.T("Free space")))));
        return dot;
    }

    /// Tries to add loot to drone cargo while respecting space left
    // tg: code/modules/explorer_drone/exodrone.dm:178
    public virtual async DmTask try_transfer(obj loot = null, float? delete_on_failure = null)
    {
        delete_on_failure ??= TRUE;
        if (Dm.truthy(space_left()))
        {
            await loot.forceMove(this);
            drone_log__proc(I18n.T("Acquired [].", loot.name));
        }
        else
        {
            drone_log__proc(I18n.T("Abandoned [] due to lack of space.", loot.name));
            if (Dm.truthy(delete_on_failure))
                qdel(loot);
        }
    }

    /// Crashes the drone somewhere random if there's no launchpad to be found.
    // tg: code/modules/explorer_drone/exodrone.dm:188
    public virtual async DmTask<object> drop_somewhere_on_station()
    {
        turf random_spot = get_safe_random_station_turf_equal_weight();

        obj_structure_closet_supplypod pod = await podspawn(Dm.list(
            Dm.kv("target", random_spot))) as obj_structure_closet_supplypod;
        await forceMove(pod);
        return random_spot;
    }

    /// Tries to find landing pad, starting with the one we launched from.
    // tg: code/modules/explorer_drone/exodrone.dm:198
    public virtual object find_landing_pad()
    {
        obj_machinery_exodrone_launcher landing_pad = last_pad?.resolve() as obj_machinery_exodrone_launcher;
        if (Dm.truthy(landing_pad))
            return landing_pad;
        foreach (obj_machinery_exodrone_launcher other_pad in Dm.each<obj_machinery_exodrone_launcher>(GLOB.exodrone_launchers))
            return other_pad;
        return null;
    }

    /// encounters random or specificed event for the current site.
    // tg: code/modules/explorer_drone/exodrone.dm:206
    public virtual void explore_site(datum_exploration_event specific_event = null)
    {
        if (!Dm.truthy(specific_event)) //encounter random event
        {
            DmList events_to_encounter = Dm.list();
            foreach (datum_exploration_event @event in Dm.each<datum_exploration_event>(location.events))
            {
                if (Dm.truthy(@event.visited))
                    continue;
                Dm.append(ref events_to_encounter, @event);
            }
            if (!Dm.truthy(length(events_to_encounter)))
            {
                drone_log__proc(I18n.T("It seems there's nothing interesting left around [].", location.name));
                return;
            }
            datum_exploration_event encountered_event = pick(events_to_encounter) as datum_exploration_event;
            encountered_event.encounter(this);
        }
        else if (Dm.truthy(specific_event.is_targetable()))
            specific_event.encounter(this);
    }

    // tg: code/modules/explorer_drone/exodrone.dm:221
    public virtual object get_adventure_data()
    {
        DmList data = Waiting.NoSleep(current_adventure.ui_data()) as DmList;
        data["description"] = updateKeywords(data["description"]);
        DmList choices = data["choices"] as DmList;
        foreach (DmList choice in Dm.each<DmList>(choices))
            choice["text"] = updateKeywords(choice["text"]);
        return data;
    }

    ///Replaces $$SITE_NAME with site name and $$QualityName with quality values
    // tg: code/modules/explorer_drone/exodrone.dm:230
    public virtual object updateKeywords(object text = null)
    {
        _regex_context = this;
        // var/static/regex/keywordRegex = regex(@"\$\$(\S*)","g") and keywordRegex.Replace(text, /obj/item/exodrone/proc/replace_keyword):
        // BYOND calls the replacement proc with the regex as src, which the shim's Replace does only for global procs and the
        // regex's own; each match is replaced here by the proc's text, every match in order as the "g" flag does.
        object dot = text == null ? null : System.Text.RegularExpressions.Regex.Replace(Dm.str(text), @"\$\$(\S*)",
            m => Dm.str(replace_keyword(m.Value, m.Groups[1].Value)));
        _regex_context = null;
        return dot;
    }

    /// This is called with src = regex datum, so don't try to access any instance variables directly here.
    // tg: code/modules/explorer_drone/exodrone.dm:237
    public virtual object replace_keyword(object match = null, object g1 = null)
    {
        switch (g1)
        {
            case "SITE_NAME":
                return _regex_context.location.display_name();
            default:
                if (Dm.truthy(_regex_context.current_adventure.qualities[g1]))
                    return Dm.text("[]", _regex_context.current_adventure.qualities[g1]);
                else
                    return "";
        }
    }

    // tg: code/modules/explorer_drone/exodrone.dm:248
    public virtual void start_adventure(datum_adventure adventure = null)
    {
        current_adventure = adventure;
        RegisterSignal(current_adventure, COMSIG_ADVENTURE_FINISHED, nameof(resolve_adventure));
        RegisterSignal(current_adventure, COMSIG_ADVENTURE_QUALITY_INIT, nameof(add_tool_qualities));
        RegisterSignal(current_adventure, COMSIG_ADVENTURE_DELAY_START, nameof(adventure_delay_start));
        RegisterSignal(current_adventure, COMSIG_ADVENTURE_DELAY_END, nameof(adventure_delay_end));
        set_status(EXODRONE_ADVENTURE);
        current_adventure.start_adventure();
    }

    /// Handles finishing adventure
    // tg: code/modules/explorer_drone/exodrone.dm:258
    [SignalHandler]
    public virtual void resolve_adventure(datum source = null, object result = null)
    {
        switch (result)
        {
            case ADVENTURE_RESULT_SUCCESS:
                Waiting.NoSleep(award_adventure_loot());
                UnregisterSignal(current_adventure, Dm.list(COMSIG_ADVENTURE_FINISHED, COMSIG_ADVENTURE_QUALITY_INIT, COMSIG_ADVENTURE_DELAY_START, COMSIG_ADVENTURE_DELAY_END));
                current_adventure = null;
                set_status(EXODRONE_EXPLORATION);
                return;
            case ADVENTURE_RESULT_DAMAGE:
                Waiting.NoSleep(damage(max_integrity * 0.5f)); //Half health lost
                if (!QDELETED(this)) // Don't bother if we just blown up from the damage
                {
                    UnregisterSignal(current_adventure, Dm.list(COMSIG_ADVENTURE_FINISHED, COMSIG_ADVENTURE_QUALITY_INIT, COMSIG_ADVENTURE_DELAY_START, COMSIG_ADVENTURE_DELAY_END));
                    current_adventure = null;
                    set_status(EXODRONE_EXPLORATION);
                }
                return;
            case ADVENTURE_RESULT_DEATH:
                qdel(this);
                break;
        }
    }

    /// Adds loot from current adventure to the drone
    // tg: code/modules/explorer_drone/exodrone.dm:278
    public virtual async DmTask award_adventure_loot()
    {
        if (Dm.truthy(length(current_adventure.loot_categories)))
        {
            Type generator_type = GLOB.adventure_loot_generator_index[pick(current_adventure.loot_categories)] as Type;
            if (!Dm.truthy(generator_type))
                return; //Could probably warn but i suppose this is up to adventure creator.
            datum_adventure_loot_generator generator = await Dm.new_async(generator_type) as datum_adventure_loot_generator;
            await generator.transfer_loot(this);
        }
    }

    /// Applies adventure qualities based on our tools
    // tg: code/modules/explorer_drone/exodrone.dm:287
    [SignalHandler]
    public virtual void add_tool_qualities(datum source = null, DmList quality_list = null)
    {
        foreach (object tool in Dm.each(tools))
            quality_list[tool] = 1;
    }

    // tg: code/modules/explorer_drone/exodrone.dm:292
    [SignalHandler]
    public virtual void adventure_delay_start(datum source = null, object delay_time = null, object delay_message = null)
    {
        set_busy(delay_message, delay_time);
    }

    // tg: code/modules/explorer_drone/exodrone.dm:296
    [SignalHandler]
    public virtual void adventure_delay_end(datum source = null)
    {
        unset_busy(EXODRONE_ADVENTURE);
    }

    /// Enters busy mode for a given duration.
    // tg: code/modules/explorer_drone/exodrone.dm:301
    public virtual void set_busy(object message = null, object duration = null)
    {
        if (Dm.truthy(message))
            busy_message = (string)message;
        busy_start_time = world.time;
        busy_duration = duration;
        set_status(EXODRONE_BUSY);
    }

    /// Resets busy status
    // tg: code/modules/explorer_drone/exodrone.dm:309
    public virtual void unset_busy(object new_status = null)
    {
        busy_message = Dm.initial(this, (obj_item_exodrone p) => p.busy_message);
        busy_start_time = null;
        busy_duration = null;
        set_status(new_status);
    }

    // tg: code/modules/explorer_drone/exodrone.dm:315
    public virtual object busy_time_left()
    {
        return Dm.num(busy_duration) - (world.time - Dm.num(busy_start_time));
    }

    /// Returns failure message or FALSE if we're ready to travel
    // tg: code/modules/explorer_drone/exodrone.dm:319
    public virtual object travel_error()
    {
        /// We're home and on ready pad or exploring and out of any events/adventures
        switch (drone_status)
        {
            case EXODRONE_IDLE:
            {
                obj_machinery_exodrone_launcher pad = Dm.locate<obj_machinery_exodrone_launcher>(loc);
                if (!Dm.truthy(pad))
                    return I18n.T("No launcher");
                if (!Dm.truthy(pad.fuel_canister))
                    return I18n.T("No fuel in launcher");
                if (pad.fuel_canister.uses <= 0)
                    return I18n.T("Launcher fuel used up");
                return FALSE;
            }
            case EXODRONE_EXPLORATION:
                if (Dm.truthy(current_event_ui_data))
                    return I18n.T("Busy");
                return FALSE;
            default:
                return "";
        }
    }

    /// Deals damage in adventures/events.
    // tg: code/modules/explorer_drone/exodrone.dm:339
    public virtual async DmTask damage(object amount = null)
    {
        await take_damage(amount);
        drone_log__proc(I18n.T("Sustained [] damage.", amount));
    }

    // tg: code/modules/explorer_drone/exodrone.dm:343
    public virtual void drone_log__proc(object message = null)
    {
        if (length(drone_log) > EXODRONE_LOG_SIZE)
            drone_log.Remove(); // tg's Remove() with no argument removes nothing: the log keeps every line
        drone_log.Insert(1, message);
    }

    // tg: code/modules/explorer_drone/exodrone.dm:348
    public virtual object has_tool(object tool_type = null)
    {
        return tools.Find(tool_type);
    }

    // tg: code/modules/explorer_drone/exodrone.dm:453
    public virtual object get_travel_coeff()
    {
        switch (drone_status)
        {
            case EXODRONE_IDLE:
            {
                obj_machinery_exodrone_launcher pad = Dm.locate<obj_machinery_exodrone_launcher>(loc);
                if (Dm.truthy(pad) && Dm.truthy(pad.fuel_canister))
                    return pad.get_fuel_coefficent();
                else
                    return travel_cost_coeff;
            }
            default:
                return travel_cost_coeff;
        }
    }
}

public sealed partial class TgState
{
    // tg: code/modules/explorer_drone/exodrone.dm:60  var/static/name_counter = list()
    internal DmList obj_item_exodrone__name_counter;
    // tg: code/modules/explorer_drone/exodrone.dm:62  var/static/obj/item/exodrone/_regex_context
    internal obj_item_exodrone obj_item_exodrone___regex_context;
}

/// Exploration drone launcher
// tg: code/modules/explorer_drone/exodrone.dm:352
public partial class obj_machinery_exodrone_launcher : obj_machinery
{
    // tg: code/modules/explorer_drone/exodrone.dm:360
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.append(ref GLOB.exodrone_launchers, this);
        return dot;
    }

    // tg: code/modules/explorer_drone/exodrone.dm:364
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(fuel_canister))
            Dm.append(ref dot, span_notice(I18n.T("You can remove the [] with a <b>prying tool</b>.", fuel_canister)));
        return dot;
    }

    // tg: code/modules/explorer_drone/exodrone.dm:369
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is obj_item_fuel_pellet)
        {
            if (Dm.truthy(fuel_canister))
            {
                to_chat(user, span_warning(I18n.T("There's already fuel loaded inside []!", this)));
                return ITEM_INTERACT_BLOCKING;
            }

            if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
                return ITEM_INTERACT_BLOCKING;

            fuel_canister = tool as obj_item_fuel_pellet;
            await update_icon();
            return ITEM_INTERACT_SUCCESS;
        }

        if (tool is obj_item_exodrone)
        {
            if (!Dm.truthy(await user.transferItemToLoc(tool, drop_location())))
                return ITEM_INTERACT_BLOCKING;

            return ITEM_INTERACT_SUCCESS;
        }

        return await base.item_interaction(user, tool, modifiers, __extra);
    }

    // tg: code/modules/explorer_drone/exodrone.dm:390
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item crowbar = null)
    {
        if (!Dm.truthy(fuel_canister))
            return null;

        to_chat(user, span_notice(I18n.T("You remove [] from [].", fuel_canister, this)));
        await fuel_canister.forceMove(drop_location() as atom);
        fuel_canister = null;
        await update_icon();
        return TRUE;
    }

    // tg: code/modules/explorer_drone/exodrone.dm:400
    public override object Destroy(float? force = null)
    {
        Dm.remove(ref GLOB.exodrone_launchers, this);
        QDEL_NULL(ref fuel_canister);
        return base.Destroy(force);
    }

    // tg: code/modules/explorer_drone/exodrone.dm:405
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (Dm.truthy(fuel_canister) && fuel_canister.uses > 0)
        {
            switch (fuel_canister.fuel_type)
            {
                case obj_item_exodrone.FUEL_BASIC:
                    Dm.append(ref dot, "launchpad_fuel_basic");
                    break;
                case obj_item_exodrone.FUEL_ADVANCED:
                    Dm.append(ref dot, "launchpad_fuel_advanced");
                    break;
                case obj_item_exodrone.FUEL_EXOTIC:
                    Dm.append(ref dot, "launchpad_fuel_exotic");
                    break;
            }
        }
        return dot;
    }

    /*
     * Gets the fuel travel coefficient for what type of fuel is within the launcher.
     */
    // tg: code/modules/explorer_drone/exodrone.dm:419
    public virtual object get_fuel_coefficent()
    {
        if (!Dm.truthy(fuel_canister))
            return null;
        switch (fuel_canister.fuel_type)
        {
            case obj_item_exodrone.FUEL_BASIC:
                return obj_item_exodrone.BASIC_FUEL_TIME_COST;
            case obj_item_exodrone.FUEL_ADVANCED:
                return obj_item_exodrone.ADVANCED_FUEL_TIME_COST;
            case obj_item_exodrone.FUEL_EXOTIC:
                return obj_item_exodrone.EXOTIC_FUEL_TIME_COST;
        }
        return null;
    }

    /*
     * Use up some of the fuel within the launcher to power the drone.
     *
     * drone - the drone that's being fuelled by our launcher.
     */
    // tg: code/modules/explorer_drone/exodrone.dm:435
    public virtual async DmTask fuel_up(obj_item_exodrone drone = null)
    {
        drone.travel_cost_coeff = Dm.num(get_fuel_coefficent());
        await fuel_canister.use();
        await update_icon();
    }

    /*
     * Plays an effect on the pad, with a sound effect to boot.
     */
    // tg: code/modules/explorer_drone/exodrone.dm:443
    public virtual async DmTask launch_effect()
    {
        playsound(this, Dm.resource("sound/effects/podwoosh.ogg"), 50, FALSE);
        await do_smoke(1, this, get_turf(this));
    }

    // tg: code/modules/explorer_drone/exodrone.dm:447
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (gone == fuel_canister)
        {
            fuel_canister = null;
            await update_icon();
        }
        return dot;
    }
}

// tg: code/modules/explorer_drone/exodrone.dm:464
public partial class obj_item_fuel_pellet : obj_item
{
    // tg: code/modules/explorer_drone/exodrone.dm:474
    public override async DmTask<object> use(object used = null, params object[] __extra)
    {
        uses--;
        if (uses <= 0)
            qdel(this);
        return null;
    }
}
