// Translated from tgstation (snapshot 1d99bd7f): code/modules/explorer_drone/scanner_array.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the scanner arrays (the dishes), their controller that sums their scan power under each site's scan conditions, the
// wide, point and deep scans with their timings, and the scanner array control console (the ExoscannerConsole window) with its
// experiment handler for the exploration scans. The var blocks are generated (D111).

public partial class datum_controller_global_vars
{
    // tg: code/modules/explorer_drone/scanner_array.dm:1  GLOBAL_DATUM_INIT(exoscanner_controller, /datum/scanner_controller, new)
    public datum_scanner_controller exoscanner_controller;

    // tg: code/modules/explorer_drone/scanner_array.dm:1  (GLOBAL_MANAGED(exoscanner_controller, new), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalexoscanner_controller()
    {
        exoscanner_controller = Dm.@new<datum_scanner_controller>();
        Dm.append(ref gvars_datum_init_order, "exoscanner_controller");
    }

    /// List of scanned distances
    // tg: code/modules/explorer_drone/scanner_array.dm:3  GLOBAL_LIST_INIT(exoscanner_bands, list(...))
    public DmList exoscanner_bands;

    // tg: code/modules/explorer_drone/scanner_array.dm:3  (GLOBAL_MANAGED(exoscanner_bands, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalexoscanner_bands()
    {
        exoscanner_bands = Dm.list(
            Dm.kv(EXOSCANNER_BAND_PLASMA, 0),
            Dm.kv(EXOSCANNER_BAND_LIFE, 0),
            Dm.kv(EXOSCANNER_BAND_TECH, 0),
            Dm.kv(EXOSCANNER_BAND_RADIATION, 0),
            Dm.kv(EXOSCANNER_BAND_DENSITY, 0));
        Dm.append(ref gvars_datum_init_order, "exoscanner_bands");
    }

    /// Scan condition instances
    // tg: code/modules/explorer_drone/scanner_array.dm:5  GLOBAL_LIST_INIT(scan_conditions, init_scan_conditions())
    public DmList scan_conditions;

    // tg: code/modules/explorer_drone/scanner_array.dm:5  (GLOBAL_MANAGED(scan_conditions, init_scan_conditions()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalscan_conditions()
    {
        scan_conditions = init_scan_conditions() as DmList;
        Dm.append(ref gvars_datum_init_order, "scan_conditions");
    }
}

public static partial class GlobalProcs
{
    // tg: code/modules/explorer_drone/scanner_array.dm:8
    public static object init_scan_conditions()
    {
        DmList dot = Dm.list();
        foreach (object type in Dm.each(subtypesof(typeof(datum_scan_condition))))
            Dm.append(ref dot, Dm.@new((Type)type));
        return dot;
    }
}

/// Represents scan in progress, only one globally for now, todo later split per z or allow partial dish swarm usage
// tg: code/modules/explorer_drone/scanner_array.dm:22
public partial class datum_exoscan : datum
{
    // tg: code/modules/explorer_drone/scanner_array.dm:13  #define MAX_SCAN_DISTANCE 10 (file-local, #undef at :405; the console reads it)
    internal const int MAX_SCAN_DISTANCE = 10;

    // tg: code/modules/explorer_drone/scanner_array.dm:15  #define WIDE_SCAN_COST(BAND, SCAN_POWER) (min(((BAND*BAND)/(SCAN_POWER))*2*60*10, 10 MINUTES))
    // (file-local, #undef at :406; the console reads it)
    internal static float WIDE_SCAN_COST(float BAND, float SCAN_POWER) => Dm.min(Dm.div(BAND * BAND, SCAN_POWER) * 2 * 60 * 10, 10 * MINUTES);

    // tg: code/modules/explorer_drone/scanner_array.dm:16  #define BASE_POINT_SCAN_TIME (2 MINUTES) (file-local, #undef at :407)
    internal const float BASE_POINT_SCAN_TIME = 2 * MINUTES;

    // tg: code/modules/explorer_drone/scanner_array.dm:17  #define BASE_DEEP_SCAN_TIME (3 MINUTES) (file-local, #undef at :408)
    internal const float BASE_DEEP_SCAN_TIME = 3 * MINUTES;

    // tg: code/modules/explorer_drone/scanner_array.dm:32
    [DmParams("scan_type", "target")]
    public override async DmTask New(params object[] __extra)
    {
        object scan_type = Dm.arg(__extra, 0);
        datum_exploration_site target = Dm.arg<datum_exploration_site>(__extra, 1);
        this.scan_type = scan_type;
        this.target = target;
        float scan_time = 0;
        switch (scan_type)
        {
            case EXOSCAN_WIDE:
                scan_power = Dm.num(GLOB.exoscanner_controller.calculate_scan_power());
                scan_time = WIDE_SCAN_COST(GLOB.exoscanner_controller.wide_scan_band, scan_power);
                break;
            case EXOSCAN_POINT:
                scan_power = Dm.num(GLOB.exoscanner_controller.get_scan_power(target));
                scan_time = Dm.div(BASE_POINT_SCAN_TIME, scan_power);
                break;
            case EXOSCAN_DEEP:
                scan_power = Dm.num(GLOB.exoscanner_controller.get_scan_power(target));
                scan_time = Dm.div(BASE_DEEP_SCAN_TIME * target.distance, scan_power);
                break;
        }
        scan_timer = _addtimer(Dm.@new<datum_callback>(this, nameof(resolve_scan)), scan_time, TIMER_STOPPABLE, file: "code/modules/explorer_drone/scanner_array.dm", line: 47);
    }

    /// Short description for in progress scan
    // tg: code/modules/explorer_drone/scanner_array.dm:50
    public virtual object ui_description()
    {
        switch (scan_type)
        {
            case EXOSCAN_WIDE:
                return I18n.T("Wide: Scanning sphere starting 1 AU from the station.");
            case EXOSCAN_POINT:
                return I18n.T("Point scan of []", target.display_name());
            case EXOSCAN_DEEP:
                return I18n.T("Deep scan of []", target.display_name());
        }
        return null;
    }

    // tg: code/modules/explorer_drone/scanner_array.dm:59
    public virtual async DmTask resolve_scan()
    {
        switch (scan_type)
        {
            case EXOSCAN_WIDE:
                await generate_exploration_sites();
                break;
            case EXOSCAN_POINT:
                target.reveal();
                target.point_scan_complete = TRUE;
                break;
            case EXOSCAN_DEEP:
                target.reveal();
                target.deep_scan_complete = TRUE;
                break;
        }
        qdel(this);
    }

    // tg: code/modules/explorer_drone/scanner_array.dm:71
    public virtual void stop()
    {
        SEND_SIGNAL(COMSIG_EXOSCAN_INTERRUPTED, Dm.list(this));
        qdel(this);
    }

    // tg: code/modules/explorer_drone/scanner_array.dm:75
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);
        deltimer(scan_timer);
        return dot;
    }
}

// tg: code/modules/explorer_drone/scanner_array.dm:79
public partial class obj_machinery_computer_exoscanner_control : obj_machinery_computer
{
    // tg: code/modules/explorer_drone/scanner_array.dm:88
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        object dot = await base.ui_interact(user, ui);
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "ExoscannerConsole", name);
            await ui.open();
        }
        return dot;
    }

    // tg: code/modules/explorer_drone/scanner_array.dm:95
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList dot = await base.ui_data(user) as DmList;
        dot["failed"] = failed_popup;
        dot["selected_site"] = Dm.truthy(selected_site) ? REF(selected_site) : selected_site;
        float scan_power = 0;
        if (Dm.truthy(selected_site))
        {
            dot["site_data"] = selected_site.site_data();
            dot["scan_power"] = scan_power = Dm.num(GLOB.exoscanner_controller.get_scan_power(selected_site));
            dot["point_scan_eta"] = scan_power > 0 ? Dm.div(datum_exoscan.BASE_POINT_SCAN_TIME, scan_power) : 0;
            dot["deep_scan_eta"] = scan_power > 0 ? Dm.div(datum_exoscan.BASE_DEEP_SCAN_TIME * selected_site.distance, scan_power) : 0;
            DmList condition_descriptions = Dm.list();
            foreach (datum_scan_condition condition in Dm.each<datum_scan_condition>(selected_site.scan_conditions))
                Dm.append(ref condition_descriptions, condition.description);
            dot["scan_conditions"] = condition_descriptions;
        }
        else
        {
            dot["scan_power"] = scan_power = Dm.num(GLOB.exoscanner_controller.calculate_scan_power());
            dot["wide_scan_eta"] = scan_power > 0 ? datum_exoscan.WIDE_SCAN_COST(GLOB.exoscanner_controller.wide_scan_band, scan_power) : 0;
            dot["possible_sites"] = build_exploration_site_ui_data();
            dot["scan_conditions"] = null;
        }

        dot["scan_in_progress"] = Dm.not(Dm.not(GLOB.exoscanner_controller.current_scan));
        if (Dm.truthy(GLOB.exoscanner_controller.current_scan)) //Display scan in progress info
        {
            dot["scan_time"] = timeleft(GLOB.exoscanner_controller.current_scan.scan_timer);
            dot["current_scan_power"] = GLOB.exoscanner_controller.current_scan.scan_power;
            dot["scan_description"] = GLOB.exoscanner_controller.current_scan.ui_description();
        }
        return dot;
    }

    // tg: code/modules/explorer_drone/scanner_array.dm:124
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList dot = await base.ui_static_data(user) as DmList;
        dot["all_bands"] = GLOB.exoscanner_bands;
        return dot;
    }

    // tg: code/modules/explorer_drone/scanner_array.dm:128
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        switch (action)
        {
            case "select_site":
                if (Dm.truthy(@params["site_ref"]))
                {
                    datum_exploration_site site = Dm.locate_ref(@params["site_ref"]) is datum_exploration_site found && Dm.@in(found, GLOB.exploration_sites) ? found : null; // locate(params["site_ref"]) in GLOB.exploration_sites
                    if (Dm.truthy(site))
                        selected_site = site;
                }
                else
                    selected_site = null;
                return TRUE;
            case "stop_scan":
                stop_current_scan();
                return TRUE;
            case "start_wide_scan":
                start_wide_scan();
                return TRUE;
            case "start_point_scan":
                start_point_scan();
                return TRUE;
            case "start_deep_scan":
                start_deep_scan();
                return TRUE;
            case "confirm_fail":
                failed_popup = FALSE;
                return TRUE;
        }
        return dot;
    }

    // tg: code/modules/explorer_drone/scanner_array.dm:155
    public virtual void stop_current_scan()
    {
        if (Dm.truthy(GLOB.exoscanner_controller.current_scan))
            GLOB.exoscanner_controller.current_scan.stop();
    }

    // tg: code/modules/explorer_drone/scanner_array.dm:159
    public virtual void start_wide_scan(object radius = null)
    {
        if (Dm.truthy(GLOB.exoscanner_controller.current_scan))
            return;
        if (GLOB.exoscanner_controller.wide_scan_band > datum_exoscan.MAX_SCAN_DISTANCE)
            return;
        create_scan(EXOSCAN_WIDE);
    }

    // tg: code/modules/explorer_drone/scanner_array.dm:166
    public virtual void start_point_scan()
    {
        if (Dm.truthy(GLOB.exoscanner_controller.current_scan) || !Dm.truthy(selected_site) || Dm.truthy(selected_site.point_scan_complete))
            return;
        create_scan(EXOSCAN_POINT, selected_site);
    }

    // tg: code/modules/explorer_drone/scanner_array.dm:171
    public virtual void start_deep_scan()
    {
        if (Dm.truthy(GLOB.exoscanner_controller.current_scan) || !Dm.truthy(selected_site) || Dm.truthy(selected_site.deep_scan_complete))
            return;
        create_scan(EXOSCAN_DEEP, selected_site);
    }

    // tg: code/modules/explorer_drone/scanner_array.dm:176
    public virtual void create_scan(object scan_type = null, object target = null)
    {
        datum_exoscan scan = GLOB.exoscanner_controller.create_scan(scan_type, target as datum_exploration_site) as datum_exoscan;
        if (Dm.truthy(scan))
            RegisterSignal(scan, COMSIG_EXOSCAN_INTERRUPTED, nameof(scan_failed));
        playsound(this, Dm.resource("sound/machines/terminal/terminal_processing.ogg"), 20, vary: TRUE);
    }

    // tg: code/modules/explorer_drone/scanner_array.dm:182
    [SignalHandler]
    public virtual void scan_failed()
    {
        failed_popup = TRUE;
        SStgui.update_uis(this);
    }

    // tg: code/modules/explorer_drone/scanner_array.dm:187
    public override async DmTask post_machine_initialize()
    {
        await base.post_machine_initialize();
        await AddComponent(typeof(datum_component_experiment_handler),
            Dm.named("allowed_experiments", Dm.list(typeof(datum_experiment_exploration_scan))),
            Dm.named("config_mode", EXPERIMENT_CONFIG_UI),
            Dm.named("config_flags", EXPERIMENT_CONFIG_ALWAYS_ACTIVE));
    }
}

// tg: code/modules/explorer_drone/scanner_array.dm:194
public partial class obj_machinery_exoscanner : obj_machinery
{
    // tg: code/modules/explorer_drone/scanner_array.dm:204
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        RegisterSignals(GLOB.exoscanner_controller, Dm.list(COMSIG_EXOSCAN_STARTED, COMSIG_EXOSCAN_FINISHED), nameof(scan_change));
        update_readiness();
        return dot;
    }

    // tg: code/modules/explorer_drone/scanner_array.dm:209
    public override async DmTask<object> RefreshParts()
    {
        object dot = await base.RefreshParts();
        float power = 1;

        foreach (datum_stock_part_scanning_module scanning_module in Dm.each<datum_stock_part_scanning_module>(component_parts))
            power += Dm.div(Dm.num(scanning_module.tier) - 1, 12);
        scan_power = power;
        GLOB.exoscanner_controller.update_scan_power();

        // More generous power draw scaling. Sum the total energy ratings of all parts involved, divide by how many parts we have, use that as the multiplier.
        // Otherwise it'd be hitting 81x power draw at T4 parts, which seems... unintended.
        float energy_rating = 0;
        foreach (datum_stock_part part in Dm.each<datum_stock_part>(component_parts))
            energy_rating += Dm.num(part.energy_rating());

        foreach (obj_item_stock_parts part in Dm.each<obj_item_stock_parts>(component_parts))
            energy_rating += part.energy_rating;

        idle_power_usage = Dm.initial(this, (obj_machinery_exoscanner m) => m.idle_power_usage) * Dm.div(energy_rating, 8);
        active_power_usage = Dm.initial(this, (obj_machinery_exoscanner m) => m.active_power_usage) * Dm.div(energy_rating, 8);
        update_current_power_usage();
        return dot;
    }

    // tg: code/modules/explorer_drone/scanner_array.dm:231
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        return default_deconstruction_screwdriver(user, tool);
    }

    // tg: code/modules/explorer_drone/scanner_array.dm:234
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        return await default_deconstruction_crowbar(user, tool);
    }

    // tg: code/modules/explorer_drone/scanner_array.dm:237
    [SignalHandler]
    public virtual void scan_change()
    {
        if (Dm.truthy(GLOB.exoscanner_controller.current_scan))
            update_use_power(ACTIVE_POWER_USE);
        else
            update_use_power(IDLE_POWER_USE);
        Waiting.NoSleep(update_icon_state());
    }

    // tg: code/modules/explorer_drone/scanner_array.dm:245
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);
        GLOB.exoscanner_controller.deactivate_scanner(this);
        return dot;
    }

    // tg: code/modules/explorer_drone/scanner_array.dm:249
    public virtual object is_ready()
    {
        return Dm.num(Dm.truthy(anchored) && Dm.truthy(is_operational) && !Dm.truthy(panel_open));
    }

    // tg: code/modules/explorer_drone/scanner_array.dm:252
    public virtual void update_readiness()
    {
        if (Dm.truthy(is_ready()))
            GLOB.exoscanner_controller.activate_scanner(this);
        else
            GLOB.exoscanner_controller.deactivate_scanner(this);
        Waiting.NoSleep(update_icon_state());
    }

    // tg: code/modules/explorer_drone/scanner_array.dm:259
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        if (Dm.truthy(panel_open))
            icon_state = Dm.text("[]_open", base_icon_state);
        else if (Dm.truthy(is_ready()))
        {
            if (Dm.truthy(GLOB.exoscanner_controller.current_scan))
                icon_state = Dm.text("[]_on", base_icon_state);
            else
                icon_state = Dm.text("[]_ready", base_icon_state);
        }
        else
            icon_state = Dm.text("[]_off", base_icon_state);
        return dot;
    }

    // tg: code/modules/explorer_drone/scanner_array.dm:270
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        object dot = await base.wrench_act(user, tool);
        await default_unfasten_wrench(user, tool, time: 1 * SECONDS);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/explorer_drone/scanner_array.dm:275
    public override async DmTask<object> set_anchored(object anchorvalue = null)
    {
        object dot = await base.set_anchored(anchorvalue);
        update_readiness();
        return dot;
    }

    // tg: code/modules/explorer_drone/scanner_array.dm:279
    public override async DmTask on_set_is_operational(object old_value = null)
    {
        await base.on_set_is_operational(old_value);
        update_readiness();
    }

    // tg: code/modules/explorer_drone/scanner_array.dm:283
    public override void on_set_panel_open(object old_value = null)
    {
        update_readiness();
    }
}

///Helper datum to calculate and store scanning power and track in progress scans
// tg: code/modules/explorer_drone/scanner_array.dm:287
public partial class datum_scanner_controller : datum
{
    // tg: code/modules/explorer_drone/scanner_array.dm:298
    public virtual object create_scan(object scan_type = null, datum_exploration_site target = null)
    {
        if (Dm.truthy(current_scan))
            return null;
        if (length(GLOB.exoscanner_controller.tracked_dishes) <= 0 || (Dm.truthy(target) && Dm.num(GLOB.exoscanner_controller.get_scan_power(target)) <= 0))
            return null;
        current_scan = Dm.@new<datum_exoscan>(scan_type, target);
        RegisterSignal(current_scan, COMSIG_QDELETING, nameof(cleanup_current_scan));
        SEND_SIGNAL(COMSIG_EXOSCAN_STARTED, Dm.list(this, current_scan));
        return current_scan;
    }

    // tg: code/modules/explorer_drone/scanner_array.dm:308
    [SignalHandler]
    public virtual void cleanup_current_scan()
    {
        current_scan = null;
        SEND_SIGNAL(COMSIG_EXOSCAN_FINISHED, Dm.list(this, current_scan));
    }

    // tg: code/modules/explorer_drone/scanner_array.dm:313
    public virtual void activate_scanner(obj_machinery_exoscanner scanner = null)
    {
        if (Dm.@in(scanner, tracked_dishes))
            return;
        Dm.append(ref tracked_dishes, scanner);
        update_scan_power();
    }

    // tg: code/modules/explorer_drone/scanner_array.dm:319
    public virtual void deactivate_scanner(obj_machinery_exoscanner scanner = null)
    {
        if (!Dm.@in(scanner, tracked_dishes))
            return;
        Dm.remove(ref tracked_dishes, scanner);
        update_scan_power();
    }

    // tg: code/modules/explorer_drone/scanner_array.dm:325
    public virtual void update_scan_power()
    {
        scan_power_cache = Dm.list();
        if (Dm.truthy(current_scan)) //Check if we need to interrupt current scan.
        {
            float current_power = length(tracked_dishes);
            if (Dm.truthy(current_scan.target))
                current_power = Dm.num(get_scan_power(current_scan.target));
            if (current_scan.scan_power > current_power)
                current_scan.stop(); // tg passes "Scan swarm power reduced", which stop() takes no parameter for
        }
    }

    // tg: code/modules/explorer_drone/scanner_array.dm:334
    public virtual object get_scan_power(datum_exploration_site target = null)
    {
        if (!Dm.truthy(scan_power_cache[target]))
            scan_power_cache[target] = calculate_scan_power(target.scan_conditions);
        return scan_power_cache[target];
    }

    // tg: code/modules/explorer_drone/scanner_array.dm:339
    public virtual object calculate_scan_power(object conditions = null)
    {
        float dot = 0;
        foreach (obj_machinery_exoscanner dish in Dm.each<obj_machinery_exoscanner>(tracked_dishes))
        {
            float effective_power = dish.scan_power;
            foreach (datum_scan_condition condition in Dm.each<datum_scan_condition>(conditions))
            {
                effective_power *= Dm.num(condition.check_dish(dish));
                if (!Dm.truthy(effective_power)) //Don't bother continuing if it's zero
                    break;
            }
            dot += effective_power;
        }
        return dot;
    }
}

/// Scan condition, these require some specific setup for the dish to count for the scan power for the given site
// tg: code/modules/explorer_drone/scanner_array.dm:350
public partial class datum_scan_condition : datum
{
    /// Returns power multiplier of the dish depending on condition.
    // tg: code/modules/explorer_drone/scanner_array.dm:355
    public virtual object check_dish(obj_machinery_exoscanner dish = null)
    {
        return 1;
    }
}

// tg: code/modules/explorer_drone/scanner_array.dm:358
public partial class datum_scan_condition_nebula : datum_scan_condition
{
    // tg: code/modules/explorer_drone/scanner_array.dm:363
    public override object check_dish(obj_machinery_exoscanner dish = null)
    {
        foreach (obj_machinery_exoscanner other_dish in Dm.each<obj_machinery_exoscanner>(GLOB.exoscanner_controller.tracked_dishes))
        {
            if (dish != other_dish && dish.z == other_dish.z && get_dist(dish, other_dish) < distance)
                return 0;
        }
        return 1;
    }
}

// tg: code/modules/explorer_drone/scanner_array.dm:369
public partial class datum_scan_condition_pulsar : datum_scan_condition
{
    // tg: code/modules/explorer_drone/scanner_array.dm:374
    public override object check_dish(obj_machinery_exoscanner dish = null)
    {
        foreach (obj_machinery some_machine in Dm.each<obj_machinery>(Builtins.range(distance, dish)))
        {
            if (some_machine != dish && Dm.truthy(some_machine.is_operational))
                return 0;
        }
        return 1;
    }
}

// tg: code/modules/explorer_drone/scanner_array.dm:380
public partial class datum_scan_condition_asteroid_belt : datum_scan_condition
{
    // tg: code/modules/explorer_drone/scanner_array.dm:384
    public override object check_dish(obj_machinery_exoscanner dish = null)
    {
        turf dish_turf = get_turf(dish) as turf;
        return Dm.truthy(is_station_level(dish_turf.z)) ? 0 : 1;
    }
}

// tg: code/modules/explorer_drone/scanner_array.dm:388
public partial class datum_scan_condition_black_hole : datum_scan_condition
{
    // tg: code/modules/explorer_drone/scanner_array.dm:392
    public override object check_dish(obj_machinery_exoscanner dish = null)
    {
        float wall_count = 0;
        foreach (turf turf_in_dish_range in Dm.each<turf>(Builtins.range(1, get_turf(dish))))
        {
            if (Dm.truthy(turf_in_dish_range.density))
                wall_count += 1;
        }
        return wall_count > 6 ? 1 : 0;
    }
}

// tg: code/modules/explorer_drone/scanner_array.dm:399
public partial class datum_scan_condition_easy : datum_scan_condition
{
    // tg: code/modules/explorer_drone/scanner_array.dm:403
    public override object check_dish(obj_machinery_exoscanner dish = null)
    {
        return 2;
    }
}
