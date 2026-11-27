// Translated from tgstation (snapshot 1d99bd7f): code/modules/atmospherics/machinery/portable/canister.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M4b slice 2: the canisters): the init (the gas from create_gas or initial_gas_mix, the description, the pressure
// limit's random quality, the heat exposure), interact, create_gas and its air, anesthetic and fusion test
// variants, the pictures (update_icon_state, update_overlays, update_greyscale), the heat damage check, Exited,
// canister_break, the shielding's power draw (process), the pressure light (get_pressure_state), process_atmos (the valve's
// release and the pressure light), toggle_valve, toggle_shielding, eject_tank, toggle_reaction_suppression,
// unregister_holding and take_atmos_damage; M6 slice 7b: the gas window (update_window, its visuals under tg's alpha filter). Deferred leaves: the shielding's power cell (internal_cell is generated as a
// Type, a GAP(types); the shielding is switched only from the tgui window, M9), the leak and the volatile gas storage
// (/datum/component/gas_leaker and /datum/element/volatile_gas_storage: a damaged canister's leak and explosion, M5), the
// interact's access check (allowed, M4a), the emissives (DEVIATION DV12) and the admin and investigate logs (M10).
// Generated live-partial stubs: the map export (M10), the context tips (M9), the deconstruction, tool and item
// acts (M4a, M5), take_damage and atom_break (M5), replace_tank (the hand-held tanks, M4a), the tgui window (ui_state,
// ui_interact, ui_static_data, ui_data, ui_act: M9) and the colour menu (recolor, M9). The var blocks are generated.
// M5 slice 3 (D148, protocol 16): the tgui window (ui_state, ui_interact, ui_static_data, ui_data, ui_act) is translated; its
// relabel (tgui_input_list) is a deferred leaf, and cellCharge reads the missing cell (GAP). The colour menu (recolor) came with the
// MetaStation leftovers.

// tg: code/modules/atmospherics/machinery/portable/canister.dm:2  #define CAN_DEFAULT_RELEASE_PRESSURE (ONE_ATMOSPHERE) (file-local)
// tg: code/modules/atmospherics/machinery/portable/canister.dm:4  #define TEMPERATURE_RESISTANCE (1000 + T0C) (file-local, #undef at :778)

// tg: code/modules/atmospherics/machinery/portable/canister.dm:6
public partial class obj_machinery_portable_atmospherics_canister : obj_machinery_portable_atmospherics
{
    private const float TEMPERATURE_RESISTANCE = 1000 + T0C;

    // tg: code/modules/atmospherics/machinery/portable/canister.dm:2  #define CAN_DEFAULT_RELEASE_PRESSURE (ONE_ATMOSPHERE) (file-local)
    private const float CAN_DEFAULT_RELEASE_PRESSURE = ONE_ATMOSPHERE;

    // tg: code/modules/atmospherics/machinery/portable/canister.dm:59
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        if (Dm.truthy(mapload))
        {
            // GAP(types): internal_cell = new /obj/item/stock_parts/power_store/cell/high(src): the generated var is a Type
            // (types-converter.md 3.5), which the cell instance does not fit; the cell powers the shielding, which only the
            // tgui window switches on (M9)
            Dm.Deferred("code/modules/atmospherics/machinery/portable/canister.dm:63", "Initialize: internal_cell = new /obj/item/stock_parts/power_store/cell/high(src), the shielding's cell (GAP(types); tgui, M10+)");
        }

        if (!Dm.truthy(initial_gas_mix))
            create_gas();

        if (ispath(gas_type, typeof(datum_gas)))
            desc = Dm.text("[]. []", ((DmList)GLOB.meta_gas_info[META_GAS_NAME])[gas_type], ((DmList)GLOB.meta_gas_info[META_GAS_DESC])[gas_type]);

        update_window();

        float random_quality = Dm.num(rand());
        pressure_limit = Dm.num(Dm.initial(this, (obj_machinery_portable_atmospherics p) => p.pressure_limit)) * (1 + 0.2f * random_quality);

        update_appearance();
        Waiting.NoSleep(AddElement(typeof(datum_element_atmos_sensitive), mapload));
        // AddElement(/datum/element/volatile_gas_storage) and AddComponent(/datum/component/gas_leaker, leak_rate=0.01): a
        // damaged canister's explosion and leak (integrity and damage, explosions: M5)
        Dm.Deferred("code/modules/atmospherics/machinery/portable/canister.dm:79", "Initialize: the volatile_gas_storage element and the gas_leaker component (a damaged canister's explosion and leak, M5)");
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/portable/canister.dm:81
    public override async DmTask<object> interact(mob user = null, params object[] __extra)
    {
        object dot = await base.interact(user, __extra);
        // if(!allowed(user)) { to_chat(user, span_alert("Error - Unauthorized User.")); playsound(...) }: the canister's access
        // check after the base interact opened its tgui window (/obj/proc/allowed is not declared here: access, M4a)
        Dm.Deferred("code/modules/atmospherics/machinery/portable/canister.dm:83", "interact: the access check (allowed, M4a) after the tgui window opened");
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/portable/canister.dm:108
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.num(atom_integrity) < Dm.num(max_integrity))
            Dm.append(ref dot, span_notice(I18n.T("Integrity compromised, repair hull with a welding tool.")));
        Dm.append(ref dot, span_notice(I18n.T("A sticker on its side says <b>MAX SAFE PRESSURE: []; MAX SAFE TEMPERATURE: []</b>.", siunit_pressure(Dm.initial(this, (obj_machinery_portable_atmospherics_canister p) => p.pressure_limit), 0), siunit(temp_limit, "K", 0))));
        Dm.append(ref dot, span_notice(I18n.T("The hull is <b>welded</b> together and can be cut apart.")));
        if (internal_cell != null)
        {
            // internal_cell.percent(): GAP(types), the generated var is a Type (see Initialize), so no cell is ever inside and this branch is unreachable
            Dm.Deferred("code/modules/atmospherics/machinery/portable/canister.dm:115", "examine: internal_cell.percent(), the shielding's cell (GAP(types): the generated var is a Type, so there is never a cell)");
        }
        else
            Dm.append(ref dot, span_notice(I18n.T("Warning, no cell installed, use a screwdriver to open the hatch and insert one.")));
        if (Dm.truthy(panel_open))
            Dm.append(ref dot, span_notice(I18n.T("Hatch open, close it with a screwdriver.")));
        return dot;
    }

    /**
     * Called on Initialize(), fill the canister with the gas_type specified up to the filled level (half if 0.5, full if 1)
     * Used for canisters spawned in maps and by admins
     */
    // tg: code/modules/atmospherics/machinery/portable/canister.dm:333
    public virtual void create_gas()
    {
        if (gas_type == null)
            return;
        air_contents.adjust_gas(gas_type, (maximum_pressure * filled) * Dm.num(air_contents.volume) / (R_IDEAL_GAS_EQUATION * air_contents.temperature));
        SSair.start_processing_machine(this);
    }

    // tg: code/modules/atmospherics/machinery/portable/canister.dm:344
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
            icon_state = Dm.text("[]-1", base_icon_state);
        return await base.update_icon_state(__extra);
    }

    // tg: code/modules/atmospherics/machinery/portable/canister.dm:349
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);

        // DEVIATION(DV12): the emissive_appearance overlays below (the shielding's and the pressure light's glow) are not
        // created (lighting planes and emissives are not ported).
        if (Dm.truthy(shielding_powered))
            Dm.append(ref dot, mutable_appearance(Dm.resource("icons/obj/pipes_n_cables/canisters.dmi"), "shielding"));

        if (Dm.truthy(panel_open))
            Dm.append(ref dot, mutable_appearance(Dm.resource("icons/obj/pipes_n_cables/canisters.dmi"), "cell_hatch"));

        ///Function is used to actually set the overlays
        if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
            Dm.append(ref dot, mutable_appearance(Dm.resource("icons/obj/pipes_n_cables/canisters.dmi"), "broken"));
        if (holding != null)
        {
            Dm.append(ref dot, mutable_appearance(Dm.resource("icons/obj/pipes_n_cables/canisters.dmi"), "can-open"));
            // DEVIATION(DV73): the held tank's own picture as an invisible overlay (alpha 0, nothing in tg's 2D picture): the 3D model draws
            // its tank in that tank's colour (an oxygen tank blue, a plasma one orange); tg's can-open is one picture for every tank.
            Dm.append(ref dot, mutable_appearance(holding.icon, holding.icon_state as string, alpha: 0));
        }
        if (connected_port != null)
            Dm.append(ref dot, mutable_appearance(Dm.resource("icons/obj/pipes_n_cables/canisters.dmi"), "can-connector"));

        object light_state = get_pressure_state();
        if (Dm.truthy(light_state)) //happens when pressure is below 10kpa which means no light
            Dm.append(ref dot, mutable_appearance(Dm.resource("icons/obj/pipes_n_cables/canisters.dmi"), light_state as string));

        update_window();
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/portable/canister.dm:374
    public override async DmTask update_greyscale()
    {
        await base.update_greyscale();
        update_window();
    }

    ///Updates the overlays of this canister based on its air contents
    // tg: code/modules/atmospherics/machinery/portable/canister.dm:379
    public virtual void update_window()
    {
        if (air_contents == null)
            return;

        // M6 slice 7b (D156): the alpha filter reaches the client (protocol version 25)
        if (update_window__alpha_filter == null) // Gotta do this separate since the icon may not be correct at world init
            update_window__alpha_filter = Dm.filter(Dm.named("type", "alpha"), Dm.named("icon", Dm.icon(Dm.resource("icons/obj/pipes_n_cables/canisters.dmi"), "window-base")));

        cut_overlay(window);
        // image(icon, "state", layer = x): the state passed by name (rules 10.9)
        window = image(Dm.resource("icons/obj/pipes_n_cables/canisters.dmi"), icon_state: "window-base", layer: FLOAT_LAYER);
        DmList window_overlays = Dm.list();
        foreach (object visual in Dm.each(air_contents.return_visuals(get_turf(this))))
        {
            image new_visual = image(visual, layer: FLOAT_LAYER);
            new_visual.filters = update_window__alpha_filter;
            Dm.append(ref window_overlays, new_visual);
        }
        window.overlays = window_overlays;
        add_overlay(window);
    }

    // tg: code/modules/atmospherics/machinery/portable/canister.dm:383  var/static/alpha_filter
    private static ref dm_filter update_window__alpha_filter => ref TgState.Current.obj_machinery_portable_atmospherics_canister__update_window__alpha_filter;

    // Both of these procs handle the external temperature damage.
    // tg: code/modules/atmospherics/machinery/portable/canister.dm:398
    public override object should_atmos_process(datum_gas_mixture air = null, object exposed_temperature = null)
    {
        return (Dm.num(exposed_temperature) > TEMPERATURE_RESISTANCE && !Dm.truthy(shielding_powered)) ? TRUE : FALSE;
    }

    // tg: code/modules/atmospherics/machinery/portable/canister.dm:401
    public override async DmTask<object> atmos_expose(datum_gas_mixture air = null, object exposed_temperature = null)
    {
        await take_damage(5, BURN, 0);
        return null;
    }

    // tg: code/modules/atmospherics/machinery/portable/canister.dm:462
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (Dm.eq(gone, internal_cell))
            internal_cell = null;
        return dot;
    }

    ///Handle canisters disassemble, releases the gas content in the turf
    // tg: code/modules/atmospherics/machinery/portable/canister.dm:480
    public virtual async DmTask canister_break()
    {
        await disconnect();
        datum_gas_mixture expelled_gas = air_contents.remove(air_contents.total_moles()) as datum_gas_mixture;
        turf T = get_turf(this);
        await T.assume_air(expelled_gas);

        await atom_break();

        set_density(FALSE);
        playsound(this.loc, Dm.resource("sound/effects/spray.ogg"), 10, TRUE, -3); // tg: code/modules/atmospherics/machinery/portable/canister.dm:489
        investigate_log("was destroyed.", INVESTIGATE_ATMOS);

        if (holding != null)
        {
            await holding.forceMove(T);
            holding = null;
        }

        animate(this, 0.5f * SECONDS, Dm.named("transform", turn(transform, rand(-179, 180))), Dm.named("easing", BOUNCE_EASING));
    }

    // tg: code/modules/atmospherics/machinery/portable/canister.dm:510
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (!Dm.truthy(shielding_powered))
            return null;

        float our_pressure = Dm.num(air_contents.return_pressure());
        float our_temperature = Dm.num(air_contents.return_temperature());
        float energy_factor = round(Dm.num(log(10, max(our_pressure - pressure_limit, 1))) + Dm.num(log(10, max(our_temperature - temp_limit, 1))));
        float energy_consumed = energy_factor * 250 * Dm.num(seconds_per_tick);

        if (!Dm.truthy(energy_consumed))
            return null;

        if (Dm.truthy(powered__proc(AREA_USAGE_EQUIP, ignore_use_power: TRUE)))
            Waiting.NoSleep(use_energy(energy_consumed, channel: AREA_USAGE_EQUIP));
        else
        {
            // !internal_cell?.use(energy_consumed * 0.025): the shielding's cell (GAP(types), see Initialize): there is none
            shielding_powered = FALSE;
            SSair.start_processing_machine(this);
            // investigate_log("shielding turned off due to power loss"): tg passes no subject, so its investigate_log CRASHes and cuts the
            // proc short before update_appearance; the port leaves the log out and finishes the proc
            update_appearance();
        }
        return null;
    }

    ///return the icon_state component for the canister's indicator light based on its current pressure reading
    // tg: code/modules/atmospherics/machinery/portable/canister.dm:531
    public virtual object get_pressure_state()
    {
        float air_pressure = Dm.num(air_contents.return_pressure());
        if (air_pressure >= 40 * ONE_ATMOSPHERE)
            return "can-3";
        if (air_pressure >= 10 * ONE_ATMOSPHERE)
            return "can-2";
        if (air_pressure >= 5 * ONE_ATMOSPHERE)
            return "can-1";
        if (air_pressure >= 10)
            return "can-0";
        return null;
    }

    // tg: code/modules/atmospherics/machinery/portable/canister.dm:545
    public override object process_atmos(object seconds_per_tick = null)
    {
        DotBox<object> dot = new DotBox<object>();
        return Waiting.WaitforFalse(process_atmos__body(seconds_per_tick, dot), dot); // the slot's set waitfor = FALSE (_machinery.dm:322)
    }

    private async DmTask<object> process_atmos__body(object seconds_per_tick, DotBox<object> dot)
    {
        if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
            return PROCESS_KILL;

        // Handle gas transfer.
        if (Dm.truthy(valve_open))
        {
            turf location = get_turf(this);
            datum_gas_mixture target_air = (holding != null && await holding.return_air() is datum_gas_mixture held && Dm.truthy(held)) ? held : await location.return_air() as datum_gas_mixture;
            excited = TRUE;

            if (Dm.truthy(air_contents.release_gas_to(target_air, release_pressure)))
            {
                if (holding == null)
                    air_update_turf(FALSE, FALSE);
            }
        }

        // A bit different than other atmos devices. Wont stop if currently taking damage.
        if (Dm.truthy(await take_atmos_damage()))
        {
            update_appearance();
            excited = TRUE;
            return base.process_atmos(seconds_per_tick); //we have already updated appearance so dont need to update again below
        }

        object new_pressure_state = get_pressure_state();
        if (!Dm.eq(current_pressure_state, new_pressure_state)) //update apperance only when its pressure changes significantly from its current value
        {
            update_appearance();
            current_pressure_state = new_pressure_state;
        }

        return base.process_atmos(seconds_per_tick);
    }

    // tg: code/modules/atmospherics/machinery/portable/canister.dm:572
    public override object ui_state(mob user = null)
    {
        return GLOB.physical_state;
    }

    // tg: code/modules/atmospherics/machinery/portable/canister.dm:575
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "Canister", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/atmospherics/machinery/portable/canister.dm:581
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        return Dm.list(
            Dm.kv("defaultReleasePressure", round(CAN_DEFAULT_RELEASE_PRESSURE)),
            Dm.kv("minReleasePressure", round(CAN_MIN_RELEASE_PRESSURE)),
            Dm.kv("maxReleasePressure", round(CAN_MAX_RELEASE_PRESSURE)),
            Dm.kv("pressureLimit", round(pressure_limit)),
            Dm.kv("holdingTankLeakPressure", round(TANK_LEAK_PRESSURE)),
            Dm.kv("holdingTankFragPressure", round(TANK_FRAGMENT_PRESSURE))
        );
    }

    // tg: code/modules/atmospherics/machinery/portable/canister.dm:591
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList dot = Dm.list(
            Dm.kv("portConnected", Dm.truthy(connected_port) ? TRUE : FALSE),
            Dm.kv("tankPressure", round(Dm.num(air_contents.return_pressure()))),
            Dm.kv("releasePressure", round(release_pressure)),
            Dm.kv("valveOpen", Dm.truthy(valve_open) ? TRUE : FALSE),
            Dm.kv("hasHoldingTank", Dm.truthy(holding) ? TRUE : FALSE),
            Dm.kv("hasHypernobCrystal", Dm.truthy(nob_crystal_inserted) ? TRUE : FALSE),
            Dm.kv("reactionSuppressionEnabled", Dm.truthy(suppress_reactions) ? TRUE : FALSE)
        );

        if (Dm.truthy(holding))
        {
            datum_gas_mixture holding_mix = await holding.return_air() as datum_gas_mixture;
            dot["holdingTank"] = Dm.list(
                Dm.kv("name", holding.name),
                Dm.kv("tankPressure", round(Dm.num(holding_mix.return_pressure())))
            );
        }
        else
            dot["holdingTank"] = null;

        dot.Add(Dm.list(
            Dm.kv("shielding", shielding_powered),
            // internal_cell ? internal_cell.percent() : 0: the shielding's cell (GAP(types), see Initialize): there is none
            Dm.kv("cellCharge", 0)
        ));
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/portable/canister.dm:616
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        switch (action)
        {
            case "relabel":
            {
                object label = await tgui_input_list(usr, I18n.T("New canister label"), I18n.T("Canister"), GLOB.gas_id_to_canister);
                if (label == null)
                    return dot;
                var newtype = GLOB.gas_id_to_canister[label] as Type;
                if (newtype == null)
                    return dot;
                investigate_log(Dm.text("was relabelled to [] by [].", Dm.initial(newtype, "name"), key_name(usr)), INVESTIGATE_ATMOS);
                name = Dm.initial(newtype, "name") as string;
                desc = Dm.initial(newtype, "desc") as string;
                icon_state = Dm.initial(newtype, "icon_state") as string;
                base_icon_state = icon_state;
                await set_greyscale(Dm.initial(newtype, "greyscale_colors"), Dm.initial(newtype, "greyscale_config"));
                break;
            }

            case "pressure":
            {
                object pressure = @params["pressure"];
                if (Dm.eq(pressure, "reset"))
                {
                    pressure = CAN_DEFAULT_RELEASE_PRESSURE;
                    dot = TRUE;
                }
                else if (Dm.eq(pressure, "min"))
                {
                    pressure = CAN_MIN_RELEASE_PRESSURE;
                    dot = TRUE;
                }
                else if (Dm.eq(pressure, "max"))
                {
                    pressure = CAN_MAX_RELEASE_PRESSURE;
                    dot = TRUE;
                }
                else if (Dm.eq(pressure, "input"))
                {
                    pressure = await tgui_input_number(World.Current.usr, message: "New release pressure", title: "Canister Pressure", @default: release_pressure, max_value: CAN_MAX_RELEASE_PRESSURE, min_value: CAN_MIN_RELEASE_PRESSURE, round_value: FALSE);
                    if (pressure != null)
                        dot = TRUE;
                }
                else if (text2num(pressure) != null)
                {
                    pressure = text2num(pressure);
                    dot = TRUE;
                }
                if (Dm.truthy(dot))
                {
                    release_pressure = Dm.clamp(Dm.num(pressure), CAN_MIN_RELEASE_PRESSURE, CAN_MAX_RELEASE_PRESSURE);
                    investigate_log(Dm.text("was set to [] kPa by [].", release_pressure, key_name(usr)), INVESTIGATE_ATMOS);
                }
                break;
            }

            case "valve":
                toggle_valve(World.Current.usr as mob);
                dot = TRUE;
                break;

            case "eject":
                if (Dm.truthy(await eject_tank(World.Current.usr as mob)))
                    dot = TRUE;
                break;

            case "shielding":
                toggle_shielding(World.Current.usr as mob);
                dot = TRUE;
                break;

            case "reaction_suppression":
                toggle_reaction_suppression(World.Current.usr as mob);
                dot = TRUE;
                break;

            case "recolor":
            {
                object initial_config = Dm.initial(this, nameof(greyscale_config));
                if (initial_config == null)
                    return FALSE;
                object initial_icon_state = Dm.initial(this, nameof(post_init_icon_state));
                var menu = Dm.@new<datum_greyscale_modify_menu>(
                    this, usr, Dm.list(Dm.text("[]", initial_config)), Dm.@new<datum_callback>(this, nameof(recolor)),
                    Dm.truthy(initial_icon_state) ? initial_icon_state : Dm.initial(this, nameof(icon_state)), // starting_icon_state
                    initial_config, // starting_config
                    Dm.initial(this, nameof(greyscale_colors))); // starting_colors
                await menu.ui_interact(usr);
                dot = TRUE;
                break;
            }
        }

        update_appearance();
        return dot;
    }

    /// Opens/closes the canister valve
    // tg: code/modules/atmospherics/machinery/portable/canister.dm:692
    public virtual void toggle_valve(mob user = null, float? wire_pulsed = null)
    {
        wire_pulsed ??= FALSE;
        valve_open = Dm.truthy(valve_open) ? FALSE : TRUE;
        playsound(this, Dm.resource("sound/effects/valve_opening.ogg"), 50, TRUE); // tg: code/modules/atmospherics/machinery/portable/canister.dm:694
        if (!Dm.truthy(valve_open))
        {
            string closed_logmsg = Dm.text("valve was <b>closed</b> by [] [], stopping the transfer into \\the [].<br>", key_name(user), Dm.truthy(wire_pulsed) ? "via wire pulse" : "", (object)holding ?? "air");
            investigate_log(closed_logmsg, INVESTIGATE_ATMOS);
            release_log += closed_logmsg;
            return;
        }

        SSair.start_processing_machine(this);
        if (holding != null)
        {
            string opened_logmsg = Dm.text("Valve was <b>opened</b> by [] [], starting a transfer into \\the [].<br>", key_name(user), Dm.truthy(wire_pulsed) ? "via wire pulse" : "", (object)holding ?? "air");
            investigate_log(opened_logmsg, INVESTIGATE_ATMOS);
            release_log += opened_logmsg;
            return;
        }

        // Go over the gases in canister, pull all their info and mark the spooky ones
        DmList output = Dm.list();
        Dm.append(ref output, Dm.text("[] <b>opened</b> a canister[] that contains the following:", key_name(user), Dm.truthy(wire_pulsed) ? " via wire pulse" : ""));
        DmList admin_output = Dm.list();
        Dm.append(ref admin_output, Dm.text("[] <b>opened</b> a canister[] that contains the following at []:", ADMIN_LOOKUPFLW(user), Dm.truthy(wire_pulsed) ? " via wire pulse" : "", ADMIN_VERBOSEJMP(this)));
        DmList cached_moles = air_contents.moles;
        var cached_gas_name = (DmList)GLOB.meta_gas_info[META_GAS_NAME];
        var cached_gas_danger = (DmList)GLOB.meta_gas_info[META_GAS_DANGER];
        var cached_gas_visible = (DmList)GLOB.meta_gas_info[META_GAS_MOLES_VISIBLE];
        bool danger = false;
        for (int gas_index = 1; gas_index <= Dm.num(length(cached_moles)); gas_index++)
        {
            object gas_id = cached_moles[gas_index];
            object gas_name = cached_gas_name[gas_id];
            float moles = Dm.num(cached_moles[gas_id]);

            Dm.append(ref output, Dm.text("[]: [] moles.", gas_name, moles));
            if (gas_index <= 5) //the first five gases added
                Dm.append(ref admin_output, Dm.text("[]: [] moles.", gas_name, moles));
            else if (gas_index == 6) // anddd the warning
                Dm.append(ref admin_output, "Too many gases to log. Check investigate log.");
            //if moles_visible is undefined, default to default visibility
            if (Dm.truthy(cached_gas_danger[gas_id]) && moles > Dm.num(Dm.truthy(cached_gas_visible[gas_id]) ? cached_gas_visible[gas_id] : MOLES_GAS_VISIBLE))
                danger = true;
        }

        if (danger) //sent to admin's chat if contains dangerous gases
            message_admins(admin_output.Join("\n"));
        string logmsg = Dm.str(output.Join("\n"));
        investigate_log(logmsg, INVESTIGATE_ATMOS);
        release_log += logmsg;
    }

    /// Turns canister shielding on or off
    // tg: code/modules/atmospherics/machinery/portable/canister.dm:739
    public virtual void toggle_shielding(mob user = null, float? wire_pulsed = null)
    {
        wire_pulsed ??= FALSE;
        shielding_powered = Dm.truthy(shielding_powered) ? FALSE : TRUE;
        SSair.start_processing_machine(this);
        message_admins(Dm.text("[] turned [][] \\the [] powered shielding.", ADMIN_LOOKUPFLW(user), Dm.truthy(shielding_powered) ? "on" : "off", Dm.truthy(wire_pulsed) ? " via wire pulse" : "", this));
        user.investigate_log(Dm.text("turned [][] \\the [] powered shielding.", Dm.truthy(shielding_powered) ? "on" : "off", Dm.truthy(wire_pulsed) ? " via wire pulse" : "", this), INVESTIGATE_ATMOS);
        update_appearance();
    }

    /// Ejects tank from canister, if any
    // tg: code/modules/atmospherics/machinery/portable/canister.dm:747
    public virtual async DmTask<object> eject_tank(mob user = null, float? wire_pulsed = null)
    {
        wire_pulsed ??= FALSE;
        if (holding == null)
            return FALSE;
        if (Dm.truthy(valve_open))
        {
            message_admins(Dm.text("[] removed [] from [] with valve still open [] at [] releasing contents into the [].", ADMIN_LOOKUPFLW(user), holding, this, Dm.truthy(wire_pulsed) ? "via wire pulse" : "", ADMIN_VERBOSEJMP(this), span_bolddanger("air")));
            user.investigate_log(Dm.text("removed the [] [], leaving the valve open and transferring into the [].", holding, Dm.truthy(wire_pulsed) ? "via wire pulse" : "", span_bolddanger("air")), INVESTIGATE_ATMOS);
        }
        await replace_tank(user as mob_living, FALSE);
        return TRUE;
    }

    /// Turns hyper-noblium crystal reaction suppression in the canister on or off
    // tg: code/modules/atmospherics/machinery/portable/canister.dm:757
    public virtual void toggle_reaction_suppression(mob user = null, float? wire_pulsed = null)
    {
        wire_pulsed ??= FALSE;
        if (!Dm.truthy(nob_crystal_inserted))
        {
            if (!Dm.truthy(wire_pulsed))
                stack_trace(Dm.text("[] tried to toggle reaction suppression on a canister without a noblium crystal inside and without pulsing wires, possible href exploit attempt.", user), "code/modules/atmospherics/machinery/portable/canister.dm", 760);
            return;
        }
        suppress_reactions = Dm.truthy(suppress_reactions) ? FALSE : TRUE;
        SSair.start_processing_machine(this);
        message_admins(Dm.text("[] turned [][] \\the [] reaction suppression.", ADMIN_LOOKUPFLW(user), Dm.truthy(suppress_reactions) ? "on" : "off", Dm.truthy(wire_pulsed) ? "via wire pulse" : "", this));
        user.investigate_log(Dm.text("turned [][] \\the [] reaction suppression.", Dm.truthy(suppress_reactions) ? "on" : "off", Dm.truthy(wire_pulsed) ? "via wire pulse" : "", this), INVESTIGATE_ATMOS);
    }

    // tg: code/modules/atmospherics/machinery/portable/canister.dm:770
    public override void unregister_holding()
    {
        valve_open = FALSE;
        base.unregister_holding();
    }

    // tg: code/modules/atmospherics/machinery/portable/canister.dm:774
    public override async DmTask<object> take_atmos_damage()
    {
        return Dm.truthy(shielding_powered) ? FALSE : await base.take_atmos_damage();
    }

    // tg: code/modules/atmospherics/machinery/portable/canister.dm:498
    public override async DmTask<object> replace_tank(mob_living user = null, object close_valve = null, obj_item_tank new_tank = null)
    {
        object dot = await base.replace_tank(user, close_valve, new_tank);
        if (!Dm.truthy(dot))
            return dot;

        if (Dm.truthy(close_valve))
        {
            valve_open = FALSE;
            update_appearance();
            investigate_log(Dm.text("valve was <b>closed</b> by [].", key_name(user)), INVESTIGATE_ATMOS);
        }
        else if (Dm.truthy(valve_open) && holding != null)
            user.investigate_log(Dm.text("started a transfer into [].", holding), INVESTIGATE_ATMOS);
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/portable/canister.dm:415
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is not obj_item_stock_parts_power_store_cell)
            return await base.item_interaction(user, tool, modifiers, __extra);

        // the shielding's power cell through the open hatch (:419-433): internal_cell is generated as a Type (GAP(types)), and
        // the shielding is power (M7)
        Dm.Deferred("code/modules/atmospherics/machinery/portable/canister.dm:419", "item_interaction: the shielding's power cell put in through the open hatch (power cells, M7; GAP(types))");
        return ITEM_INTERACT_BLOCKING;
    }
}

// tg: code/modules/atmospherics/machinery/portable/canister.dm:304
public partial class obj_machinery_portable_atmospherics_canister_fusion_test : obj_machinery_portable_atmospherics_canister
{
    // tg: code/modules/atmospherics/machinery/portable/canister.dm:310
    public override void create_gas()
    {
        air_contents.adjust_gas(typeof(datum_gas_hydrogen), 300);
        air_contents.adjust_gas(typeof(datum_gas_tritium), 300);
        air_contents.temperature = 10000;
        SSair.start_processing_machine(this);
    }
}

// tg: code/modules/atmospherics/machinery/portable/canister.dm:316
public partial class obj_machinery_portable_atmospherics_canister_anesthetic_mix : obj_machinery_portable_atmospherics_canister
{
    // tg: code/modules/atmospherics/machinery/portable/canister.dm:324
    public override void create_gas()
    {
        air_contents.adjust_gas(typeof(datum_gas_oxygen), (O2_ANESTHETIC * maximum_pressure * filled) * Dm.num(air_contents.volume) / (R_IDEAL_GAS_EQUATION * air_contents.temperature));
        air_contents.adjust_gas(typeof(datum_gas_nitrous_oxide), (N2O_ANESTHETIC * maximum_pressure * filled) * Dm.num(air_contents.volume) / (R_IDEAL_GAS_EQUATION * air_contents.temperature));
        SSair.start_processing_machine(this);
    }
}

// tg: code/modules/atmospherics/machinery/portable/canister.dm:124
public partial class obj_machinery_portable_atmospherics_canister_air : obj_machinery_portable_atmospherics_canister
{
    // tg: code/modules/atmospherics/machinery/portable/canister.dm:339
    public override void create_gas()
    {
        air_contents.adjust_gas(typeof(datum_gas_oxygen), (O2STANDARD * maximum_pressure * filled) * Dm.num(air_contents.volume) / (R_IDEAL_GAS_EQUATION * air_contents.temperature));
        air_contents.adjust_gas(typeof(datum_gas_nitrogen), (N2STANDARD * maximum_pressure * filled) * Dm.num(air_contents.volume) / (R_IDEAL_GAS_EQUATION * air_contents.temperature));
        SSair.start_processing_machine(this);
    }
}

// M5 slice 7 (machinery integrity, D148): the canister's deconstruction (its iron, its gas let out by canister_break), its
// cell hatch (the screwdriver, the crowbar; the cell itself is GAP(types), see Initialize), the welder's cut, its damage
// (processing again) and its break.
// tg: code/modules/atmospherics/machinery/portable/canister.dm:1
public partial class obj_machinery_portable_atmospherics_canister : obj_machinery_portable_atmospherics
{
    // tg: code/modules/atmospherics/machinery/portable/canister.dm:404
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        disassembled ??= TRUE;
        if (!Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
            await canister_break();
        if (!Dm.truthy(disassembled))
        {
            Dm.@new<obj_item_stack_sheet_iron>(drop_location(), 5);
            qdel(this);
            return null;
        }
        Dm.@new<obj_item_stack_sheet_iron>(drop_location(), 10);
        // if(internal_cell) internal_cell.forceMove(drop_location()): the cell is GAP(types) (see Initialize): there is none
        return null;
    }

    // tg: code/modules/atmospherics/machinery/portable/canister.dm:433
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item screwdriver = null)
    {
        return default_deconstruction_screwdriver(user, screwdriver);
    }

    // tg: code/modules/atmospherics/machinery/portable/canister.dm:436
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        // internal_cell is GAP(types) (see Initialize): there is never a cell to remove
        if (!Dm.truthy(panel_open) || internal_cell == null)
            return ITEM_INTERACT_BLOCKING;
        Dm.NotTranslated("code/modules/atmospherics/machinery/portable/canister.dm:440", "crowbar_act: internal_cell.forceMove(drop_location()), the shielding's cell (GAP(types))");
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/atmospherics/machinery/portable/canister.dm:444
    public override async DmTask<object> welder_act_secondary(mob_living user = null, obj_item I = null)
    {
        if (!Dm.truthy(I.tool_start_check(user, amount: 1, heat_required: HIGH_TEMPERATURE_REQUIRED)))
            return ITEM_INTERACT_BLOCKING;

        float pressure = Dm.num(air_contents.return_pressure());
        if (pressure > 300)
        {
            to_chat(user, span_alert(I18n.T("The pressure gauge on [] indicates a high pressure inside... maybe you want to reconsider?", this)));
            message_admins(Dm.text("[] deconstructed by []", this, ADMIN_LOOKUPFLW(user)));
            user.log_message(Dm.text("deconstructed [] with a welder.", this), LOG_GAME);
        }
        to_chat(user, span_notice(I18n.T("You begin cutting [] apart...", this)));
        if (Dm.truthy(await I.use_tool(this, user, 3 * SECONDS, volume: 50)))
        {
            to_chat(user, span_notice(I18n.T("You cut [] apart.", this)));
            await deconstruct(TRUE);
        }

        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/atmospherics/machinery/portable/canister.dm:467
    public override async DmTask<object> take_damage(object damage_amount = null, string damage_type = null, object damage_flag = null, float? sound_effect = null, object attack_dir = null, float? armour_penetration = null)
    {
        damage_type ??= BRUTE;
        damage_flag ??= "";
        sound_effect ??= TRUE;
        armour_penetration ??= 0;
        object dot = await base.take_damage(damage_amount, damage_type, damage_flag, sound_effect, attack_dir, armour_penetration);
        if (!Dm.truthy(dot) || QDELETED(this))
            return dot;
        SSair.start_processing_machine(this);
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/portable/canister.dm:473
    public override async DmTask<object> atom_break(object damage_flag = null)
    {
        object dot = await base.atom_break(damage_flag);
        if (!Dm.truthy(dot))
            return dot;
        await canister_break();
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/atmospherics/machinery/portable/canister.dm:383  /obj/machinery/portable_atmospherics/canister/update_window var/static/alpha_filter
    internal dm_filter obj_machinery_portable_atmospherics_canister__update_window__alpha_filter;
}

// tg: code/modules/atmospherics/machinery/portable/canister.dm:767  (interleaved, rules 1.9; the MetaStation leftovers: the colour menu)
public partial class obj_machinery_portable_atmospherics_canister : obj_machinery_portable_atmospherics
{
    // tg: code/modules/atmospherics/machinery/portable/canister.dm:767
    public virtual async DmTask recolor(datum_greyscale_modify_menu menu = null)
    {
        await set_greyscale(menu.split_colors, menu.config.type);
    }
}
