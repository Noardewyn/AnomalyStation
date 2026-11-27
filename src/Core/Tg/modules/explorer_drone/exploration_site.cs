// Translated from tgstation (snapshot 1d99bd7f): code/modules/explorer_drone/exploration_site.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the exploration sites a wide scan finds (their distance band, coordinates, events with their scan bands, an
// adventure from tg's adventure files, the scan conditions), what a point or deep scan and a drone's arrival reveal, and the sites'
// data for the consoles' windows. The var blocks are generated (D111).

// tg: code/modules/explorer_drone/exploration_site.dm:2
public partial class datum_controller_global_vars
{
    /// All exploration site instances
    // tg: code/modules/explorer_drone/exploration_site.dm:2  GLOBAL_LIST_EMPTY(exploration_sites)
    public DmList exploration_sites;

    // tg: code/modules/explorer_drone/exploration_site.dm:2  (GLOBAL_MANAGED(exploration_sites, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalexploration_sites()
    {
        exploration_sites = Dm.list();
        Dm.append(ref gvars_datum_init_order, "exploration_sites");
    }
}

public static partial class GlobalProcs
{
    // Band is general distance group. Cost of scanning bands increasly exponentialy.
    // tg: code/modules/explorer_drone/exploration_site.dm:5
    public static async DmTask generate_exploration_sites()
    {
        float band = GLOB.exoscanner_controller.wide_scan_band;
        float site_count = 1 + rand(band - 1, band + 1);
        object site_types = subtypesof(typeof(datum_exploration_site)); //cache?
        for (float i = 1, i__end = site_count; i <= i__end; i++)
        {
            object site_type = pick(site_types);
            datum_exploration_site fresh_site = await Dm.new_async((Type)site_type, band) as datum_exploration_site;
            Dm.append(ref GLOB.exploration_sites, fresh_site);
        }
        GLOB.exoscanner_controller.wide_scan_band += 1;
    }

    /// Helper proc for exploration site listings in ui.
    // tg: code/modules/explorer_drone/exploration_site.dm:181
    public static object build_exploration_site_ui_data()
    {
        DmList dot = Dm.list();
        foreach (datum_exploration_site site in Dm.each<datum_exploration_site>(GLOB.exploration_sites))
            Dm.append(ref dot, Dm.list(site.site_data()));
        return dot;
    }
}

/// Exploration site, drone travel destination representing interesting zone for exploration.
// tg: code/modules/explorer_drone/exploration_site.dm:16
public partial class datum_exploration_site : datum
{
    // tg: code/modules/explorer_drone/exploration_site.dm:42
    [DmParams("band")]
    public override async DmTask New(params object[] __extra)
    {
        object band = Dm.arg(__extra, 0);
        await base.New(__extra);
        distance = Dm.max(Dm.num(band) + Dm.num(pick(-1, 0, 1, 2)), 1);
        coordinates = Dm.text("ℓ:[]°,𝑏:[]°", rand(0, 360), rand(0, 90)); // ℓ and 𝑏 are symbols for longitude/inclination in made-up station centric coordinate system.
        await generate_events();
        generate_scan_conditions();
    }

    // tg: code/modules/explorer_drone/exploration_site.dm:49
    public virtual async DmTask generate_events()
    {
        /// Try to find aventure first since they're the meat of the system.
        datum_exploration_event adventure = generate_adventure(site_traits) as datum_exploration_event;
        if (Dm.truthy(adventure))
            add_event(adventure);
        /// Fill other events
        /// Baseline weights for each event root type
        DmList base_weights = generate_events__base_weights ??= Dm.list(
            Dm.kv(typeof(datum_exploration_event_fluff), 20),
            Dm.kv(typeof(datum_exploration_event_simple_danger), 20),
            Dm.kv(typeof(datum_exploration_event_simple_trader), 10),
            Dm.kv(typeof(datum_exploration_event_simple_resource), 10));
        /// Weight mods scaled by distance, resources are more easily found on farther sites
        DmList distance_modifiers = generate_events__distance_modifiers ??= Dm.list(
            Dm.kv(typeof(datum_exploration_event_simple_trader), 3),
            Dm.kv(typeof(datum_exploration_event_simple_resource), 3));
        DmList category_weights = base_weights.Copy();
        foreach (object modifier in Dm.each(distance_modifiers))
            Dm.append_at(category_weights, modifier, distance * Dm.num(distance_modifiers[modifier]));
        float min_events_amount = CEILING(0.4f * distance + 0.2f, 1);
        for (float i = 1, i__end = rand(min_events_amount, min_events_amount + 2); i <= i__end; i++)
        {
            object chosen_category = pick_weight(category_weights);
            datum_exploration_event @event = await generate_event(site_traits, chosen_category) as datum_exploration_event;
            if (Dm.truthy(@event))
                add_event(@event);
        }
    }

    // tg: code/modules/explorer_drone/exploration_site.dm:76
    public virtual void generate_scan_conditions()
    {
        float condition_count = Dm.num(Dm.pick_weighted((3, 0), (2, 1), (1, 2))); //scale this with distance maybe ?
        DmList possible_conditions = GLOB.scan_conditions.Copy();
        for (float i = 1, i__end = condition_count; i <= i__end; i++)
            LAZYADD(ref scan_conditions, pick_n_take(possible_conditions));
    }

    // tg: code/modules/explorer_drone/exploration_site.dm:82
    public virtual object generate_adventure(object site_traits = null)
    {
        DmList possible_adventures = Dm.list();
        foreach (datum_adventure_db_entry entry in Dm.each<datum_adventure_db_entry>(GLOB.explorer_drone_adventure_db_entries))
        {
            if (Dm.truthy(entry.valid_for_use(site_traits as DmList)))
                Dm.append(ref possible_adventures, entry);
        }
        if (!Dm.truthy(length(possible_adventures)))
            return null;
        datum_adventure_db_entry chosen_db_entry = pick(possible_adventures) as datum_adventure_db_entry;
        datum_adventure chosen_adventure = chosen_db_entry.create_adventure() as datum_adventure;
        chosen_db_entry.placed = TRUE;
        datum_exploration_event_adventure adventure_event = Dm.@new<datum_exploration_event_adventure>();
        adventure_event.adventure = chosen_adventure;
        adventure_event.band_values = chosen_adventure.band_modifiers;
        return adventure_event;
    }

    // tg: code/modules/explorer_drone/exploration_site.dm:98
    public virtual async DmTask<object> generate_event(object site_traits = null, object event_root_type = null)
    {
        /// List of exploration event requirements indexed by type, .[/datum/exploration_site/a] = list("required"=list(trait),"blacklisted"=list(other_trait))
        if (!Dm.truthy(length(generate_event__exploration_event_requirements_cache ??= Dm.list())))
        {
            DmList built = await build_exploration_event_requirements_cache() as DmList;
            generate_event__exploration_event_requirements_cache = built;
        }
        DmList exploration_event_requirements_cache = generate_event__exploration_event_requirements_cache;
        DmList viable_events = Dm.list();
        foreach (object event_type in Dm.each(exploration_event_requirements_cache))
        {
            DmList required_traits = ((DmList)exploration_event_requirements_cache[event_type])["required"] as DmList;
            DmList blacklisted_traits = ((DmList)exploration_event_requirements_cache[event_type])["blacklisted"] as DmList;
            if (!ispath(event_type, event_root_type as Type))
                continue;
            if (Dm.truthy(required_traits) && length(Dm.sub(required_traits, site_traits)) != 0)
                continue;
            if (Dm.truthy(blacklisted_traits) && length(Dm.bitand(required_traits, blacklisted_traits)) != 0) // tg compares the required traits, not the site's
                continue;
            Dm.append(ref viable_events, event_type);
        }
        if (!Dm.truthy(length(viable_events)))
            return null;
        object chosen_type = pick(viable_events);
        return await Dm.new_async((Type)chosen_type);
    }

    // tg: code/modules/explorer_drone/exploration_site.dm:119
    public virtual async DmTask<object> build_exploration_event_requirements_cache()
    {
        DmList dot = Dm.list();
        foreach (object event_type in Dm.each(valid_subtypesof(typeof(datum_exploration_event))))
        {
            datum_exploration_event @event = await Dm.new_async((Type)event_type) as datum_exploration_event;
            dot[event_type] = Dm.list(Dm.kv("required", @event.required_site_traits), Dm.kv("blacklisted", @event.blacklisted_site_traits));
            //Should be no event refs,GC'd naturally
        }
        return dot;
    }

    // tg: code/modules/explorer_drone/exploration_site.dm:127
    public virtual void add_event(datum_exploration_event @event = null)
    {
        Dm.append(ref events, @event);
        /// Add up event band values to ours
        foreach (object band in Dm.each(@event.band_values))
        {
            if (Dm.truthy(band_info[band]))
                Dm.append_at(band_info, band, ((DmList)@event.band_values)[band]);
            else
                band_info[band] = ((DmList)@event.band_values)[band];
        }
        return;
    }

    // tg: code/modules/explorer_drone/exploration_site.dm:137
    public virtual void on_drone_arrival(obj_item_exodrone drone = null)
    {
        float was_known_before = revealed;
        reveal();
        if (!Dm.truthy(was_known_before))
            drone.drone_log__proc(I18n.T("Discovered [] at [].", name, coordinates));
        else
            drone.drone_log__proc(I18n.T("Arrived at [].", display_name()));
    }

    // tg: code/modules/explorer_drone/exploration_site.dm:145
    public virtual void reveal()
    {
        revealed = TRUE;
    }

    // tg: code/modules/explorer_drone/exploration_site.dm:148
    public virtual object display_name()
    {
        return Dm.truthy(revealed) ? name : I18n.T("Anomaly");
    }

    // tg: code/modules/explorer_drone/exploration_site.dm:151
    public virtual object display_description()
    {
        if (!Dm.truthy(revealed))
            return I18n.T("No Data");
        DmList descriptions = Dm.list(description);
        foreach (datum_exploration_event @event in Dm.each<datum_exploration_event>(events))
        {
            if (Dm.truthy(deep_scan_complete) && Dm.truthy(@event.deep_scan_description))
                Dm.append(ref descriptions, @event.deep_scan_description);
            else if (Dm.truthy(point_scan_complete) && Dm.truthy(@event.point_scan_description))
                Dm.append(ref descriptions, @event.point_scan_description);
        }
        return descriptions.Join("\n");
    }

    /// Data for ui_data, exploration
    // tg: code/modules/explorer_drone/exploration_site.dm:163
    public virtual object site_data(float? exploration = null)
    {
        exploration ??= FALSE;
        DmList dot = Dm.list();
        dot["ref"] = REF(this);
        dot["name"] = display_name();
        dot["coordinates"] = coordinates;
        dot["description"] = display_description();
        dot["distance"] = distance;
        dot["revealed"] = revealed;
        dot["point_scan_complete"] = point_scan_complete;
        dot["deep_scan_complete"] = deep_scan_complete;
        dot["band_info"] = Dm.truthy(point_scan_complete) ? band_info : Dm.list(); //This loses order so when you iterate bands ui side use all_bands
        if (Dm.truthy(exploration))
        {
            DmList event_data = Dm.list();
            foreach (datum_exploration_event @event in Dm.each<datum_exploration_event>(events))
            {
                if (Dm.truthy(@event.visited) && Dm.truthy(@event.is_targetable()))
                    Dm.append(ref event_data, Dm.list(Dm.list(Dm.kv("name", @event.name), Dm.kv("ref", REF(@event)))));
            }
            dot["events"] = event_data;
        }
        return dot;
    }

    // tg: code/modules/explorer_drone/exploration_site.dm:55  var/static/list/base_weights (generate_events)
    private static ref DmList generate_events__base_weights => ref TgState.Current.datum_exploration_site__generate_events__base_weights;

    // tg: code/modules/explorer_drone/exploration_site.dm:62  var/static/list/distance_modifiers (generate_events)
    private static ref DmList generate_events__distance_modifiers => ref TgState.Current.datum_exploration_site__generate_events__distance_modifiers;

    // tg: code/modules/explorer_drone/exploration_site.dm:100  var/static/exploration_event_requirements_cache (generate_event)
    private static ref DmList generate_event__exploration_event_requirements_cache => ref TgState.Current.datum_exploration_site__generate_event__exploration_event_requirements_cache;
}

public sealed partial class TgState
{
    // tg: code/modules/explorer_drone/exploration_site.dm:55  var/static/list/base_weights (generate_events)
    internal DmList datum_exploration_site__generate_events__base_weights;
    // tg: code/modules/explorer_drone/exploration_site.dm:62  var/static/list/distance_modifiers (generate_events)
    internal DmList datum_exploration_site__generate_events__distance_modifiers;
    // tg: code/modules/explorer_drone/exploration_site.dm:100  var/static/exploration_event_requirements_cache (generate_event)
    internal DmList datum_exploration_site__generate_event__exploration_event_requirements_cache;
}

/// Sites

// tg: code/modules/explorer_drone/exploration_site.dm:196
public partial class datum_exploration_site_trader_post : datum_exploration_site
{
    // tg: code/modules/explorer_drone/exploration_site.dm:203
    [DmParams("band")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        object chosen_name = pick_list(EXODRONE_FILE, "trading_station_names");
        name = Dm.text("\"[]\" trading station", chosen_name);
        description = replacetext(description, "RANDOMIZED_NAME", chosen_name);
    }
}

// tg: code/modules/explorer_drone/exploration_site.dm:221
public partial class datum_exploration_site_uncharted_planet : datum_exploration_site
{
    // tg: code/modules/explorer_drone/exploration_site.dm:227
    [DmParams("band")]
    public override async DmTask New(params object[] __extra)
    {
        /// Planet Type, Atmosphere
        DmList planet_info = pick_list(EXODRONE_FILE, "planet_types") as DmList;
        name = planet_info["name"];
        description = planet_info["description"];
        if (Dm.truthy(planet_info["habitable"]))
            Dm.append(ref site_traits, EXPLORATION_SITE_HABITABLE);
        if (Dm.truthy(planet_info["civilized"]))
            Dm.append(ref site_traits, EXPLORATION_SITE_CIVILIZED);
        if (Dm.truthy(planet_info["tech"]))
            Dm.append(ref site_traits, EXPLORATION_SITE_TECHNOLOGY);
        await base.New(__extra);
    }
}
