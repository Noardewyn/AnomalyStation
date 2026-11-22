// Translated from tgstation (snapshot 1d99bd7f): code/modules/power/lighting/light.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: the light fixture's M3 subset (M3 P3, scope 2h): Initialize and the wall mount, post_machine_initialize, the
// fixture's light (update: brightness, bulb power and colour, the 170 degree cone towards REVERSE_DIR(dir)), the status
// states (ok, empty, burned, broken), the appearance, the area's power and fire, breaking, burning out and fixing.
// Power until M7 is tg's own: an area's light channel starts powered (areas.dm:85-87) and the lightswitch on, so a
// fixture turns on in post_machine_initialize's power_change. M7 lights slice: the emergency cell and its power (the mock
// cell, get_cell, is_full_charge, the process step's charge_cell, has_emergency_power, use_emergency_power) and the fixture's
// power use (update_current_power_usage). M7 slice 3 (the APC): flicker (the power network's flicker); the nightshift is the
// APC's (set_nightshift); the M7 shocks slice: the sockets' shocks. Rigged bulbs, the grey tide and the AI's and the saboteur's hooks: 2026-11-05. M7 slice T3: inserting a tube or bulb. M7 lighting slice L1: opening the casing into a wired frame (screwdriver_act, on_deconstruction). M4a P4a: a tube removed by hand with tg's burn check, the item stuck into an empty socket.
// The procs of later milestones are generated live-partial stubs (docs/ai/specs/live-partial.txt, ## P3, ## P4). The
// sounds of translated procs are translated (M4b audio slice 6). M5 slice 1: the heat exposure (atmos_sensitive), the sparks
// of a broken tube, take_damage and play_attack_sound. The var block is generated (D111) but for `cell` (below), which makes
// the constructor hand-written.

// tg: code/modules/power/lighting/light.dm:2
public partial class obj_machinery_light : obj_machinery
{
    ///Cell reference
    // tg: code/modules/power/lighting/light.dm:40
    // The var block is generated, but /obj/machinery/light/var/cell is a Type there (rules 3.2 V3: some other type assigns a
    // typepath to a var of that name), which an emergency cell instance does not fit (types-converter.md 3.5, "Not covered"):
    // the hand-written member keeps tg's declared type, and the generator leaves it out. A hand-written var makes the class's
    // var block hand-written (types-converter.md 3.1, "Hand"): the constructor with tg's overrides (light.dm:3-14) is here.
    public obj_item_stock_parts_power_store cell;

    // tg: code/modules/power/lighting/light.dm:3
    public obj_machinery_light()
    {
        name = "light fixture";
        icon = Dm.resource("icons/obj/lighting.dmi");
        icon_state = "tube";
        desc = "A lighting fixture.";
        layer = WALL_OBJ_LAYER;
        max_integrity = 100;
        use_power = ACTIVE_POWER_USE;
        idle_power_usage = BASE_MACHINE_IDLE_CONSUMPTION * 0.02f;
        active_power_usage = BASE_MACHINE_ACTIVE_CONSUMPTION * 0.02f;
        power_channel = AREA_USAGE_LIGHT; //Lights are calc'd via area so they dont need to be in the machine list
        always_area_sensitive = TRUE;
        light_angle = 170;
    }

    // create a new lighting fixture
    // tg: code/modules/power/lighting/light.dm:88
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);

        // Detect and scream about double stacked lights
        // tg: #if PERFORM_ALL_TESTS(maptest_log_mapping): FALSE in production (rules 1.7)
        if (Dm.truthy(FALSE))
            return null;

        if (!Dm.truthy(mapload)) //sync up nightshift lighting for player made lights
        {
            area our_area = get_room_area() as area;
            obj_machinery_power_apc temp_apc = our_area.apc;
            nightshift_enabled = Dm.num(temp_apc?.nightshift_lights);
        }

        if (!Dm.truthy(start_with_cell) || Dm.truthy(no_low_power))
            has_mock_cell = FALSE;

        // tg: code/modules/power/lighting/light.dm:110
        if (Dm.truthy(is_station_level(z)))
            RegisterSignal(SSdcs, COMSIG_GLOB_GREY_TIDE_LIGHT, nameof(grey_tide));

        // Light projects out backwards from the dir of the light
        Waiting.NoSleep(set_light(l_dir: REVERSE_DIR(dir)));
        RegisterSignal(this, COMSIG_LIGHT_EATER_ACT, nameof(on_light_eater));
        Waiting.NoSleep(AddElement(typeof(datum_element_atmos_sensitive), mapload));
        Dm.Deferred("code/modules/power/lighting/light.dm:117", "Initialize: AddElement(/datum/element/contextual_screentip_bare_hands, rmb_text = \"Remove bulb\"): the screentips (M10+)");
        if (Dm.truthy(mapload))
            Waiting.NoSleep(find_and_mount_on_atom(mark_for_late_init: TRUE));
        return dot;
    }

    // tg: code/modules/power/lighting/light.dm:121
    public override object get_turfs_to_mount_on()
    {
        return Dm.list(get_step(this, dir));
    }

    // tg: code/modules/power/lighting/light.dm:124
    public override async DmTask<object> find_and_mount_on_atom(float? mark_for_late_init = null, float? late_init = null)
    {
        if (Dm.truthy(break_if_moved))
            return await base.find_and_mount_on_atom(mark_for_late_init, late_init);
        return null;
    }

    // tg: code/modules/power/lighting/light.dm:128
    public override async DmTask post_machine_initialize()
    {
        await base.post_machine_initialize();
        // tg: #ifndef MAP_TEST: MAP_TEST is not defined in production (rules 1.7), so the branch is active
        if (Dm.truthy(allow_break_on_init))
        {
            string sw__1 = fitting;
            if (sw__1 == "tube")
            {
                if (prob(2))
                    await break_light_tube(TRUE);
            }
            else if (sw__1 == "bulb")
            {
                if (prob(5))
                    await break_light_tube(TRUE);
            }
        }
        await update(trigger: FALSE);
    }

    // tg: code/modules/power/lighting/light.dm:142
    public override object Destroy(float? force = null)
    {
        area local_area = get_room_area() as area;
        if (Dm.truthy(local_area))
            on = FALSE;
        QDEL_NULL(ref cell);
        return base.Destroy(force);
    }

    // tg: code/modules/power/lighting/light.dm:149
    [DmParams()]
    public override async DmTask<object> Move(atom newloc = null, object direction = null, float? glide_size_override = null, float? update_dir = null)
    {
        if (status != LIGHT_BROKEN && Dm.truthy(break_if_moved))
            await break_light_tube(TRUE);
        return await base.Move(newloc, direction, glide_size_override, update_dir);
    }

    // tg: code/modules/power/lighting/light.dm:154
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = null;
        dot = await base.Exited(gone, direction);
        if (Dm.eq(gone, cell))
            cell = null;
        return dot;
    }

    // tg: code/modules/power/lighting/light.dm:159
    public override async DmTask setDir(object newdir = null)
    {
        await base.setDir(newdir);
        await set_light(l_dir: REVERSE_DIR(dir));
    }

    // If we're adjacent to the source, we make this sorta indentation for our light to ensure it stays lit (and to make distances look right)
    // By shifting the light position we use forward a bit, towards something that isn't off by 0.5 from being in angle
    // Because angle calculation is kinda harsh it's hard to find a happy point between fulldark and fullbright for the corners behind the light. this is good enough tho
    // tg: code/modules/power/lighting/light.dm:166
    public override object get_light_offset()
    {
        DmList hand_back = base.get_light_offset() as DmList;
        DmList dir_offset = dir2offset(REVERSE_DIR(dir)) as DmList;
        Dm.append_at(hand_back, 1, Dm.mul(dir_offset[1], 0.5f));
        Dm.append_at(hand_back, 2, Dm.mul(dir_offset[2], 0.5f));
        return hand_back;
    }

    // tg: code/modules/power/lighting/light.dm:173
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        float sw__1 = status; // set icon_states
        if (sw__1 == LIGHT_OK)
        {
            area local_area = get_room_area() as area;
            if (Dm.truthy(low_power_mode) || Dm.truthy(major_emergency) || Dm.truthy(local_area?.fire))
                icon_state = Dm.text("[]_emergency", base_state);
            else
                icon_state = Dm.text("[]", base_state);
        }
        else if (sw__1 == LIGHT_EMPTY)
            icon_state = Dm.text("[]-empty", base_state);
        else if (sw__1 == LIGHT_BURNED)
            icon_state = Dm.text("[]-burned", base_state);
        else if (sw__1 == LIGHT_BROKEN)
            icon_state = Dm.text("[]-broken", base_state);
        return await base.update_icon_state(__extra);
    }

    // tg: code/modules/power/lighting/light.dm:189
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = null;
        dot = await base.update_overlays(__extra);
        if (!Dm.truthy(on) || status != LIGHT_OK)
            return dot;

        // DEVIATION(DV12): emissive_appearance (__HELPERS/lighting.dm:2), the fixture's glow on the emissive plane, is not
        // created (lighting planes and emissives are not ported, D24, lead decision D2).
        Dm.Deferred("code/modules/power/lighting/light.dm:194", "update_overlays: emissive_appearance(overlay_icon, base_state, src, alpha), the fixture's emissive glow (DEVIATION DV12)");

        area local_area = get_room_area() as area;

        if (Dm.truthy(flickering))
        {
            Dm.append(ref dot, mutable_appearance(overlay_icon, Dm.text("[]_flickering", base_state)));
            return dot;
        }
        if (Dm.truthy(low_power_mode) || Dm.truthy(major_emergency) || Dm.truthy(local_area?.fire))
        {
            Dm.append(ref dot, mutable_appearance(overlay_icon, Dm.text("[]_emergency", base_state)));
            return dot;
        }
        if (Dm.truthy(nightshift_enabled))
        {
            Dm.append(ref dot, mutable_appearance(overlay_icon, Dm.text("[]_nightshift", base_state)));
            return dot;
        }
        Dm.append(ref dot, mutable_appearance(overlay_icon, base_state));
        return dot;
    }

    // Area sensitivity is traditionally tied directly to power use, as an optimization
    // But since we want it for fire reacting, we disregard that
    // tg: code/modules/power/lighting/light.dm:211
    public override object setup_area_power_relationship()
    {
        object dot = null;
        dot = base.setup_area_power_relationship();
        if (!Dm.truthy(dot))
            return dot;
        area our_area = get_room_area() as area;
        RegisterSignal(our_area, COMSIG_AREA_FIRE_CHANGED, nameof(handle_fire));
        return dot;
    }

    // tg: code/modules/power/lighting/light.dm:218
    public override void on_enter_area(datum source = null, area area_to_register = null)
    {
        base.on_enter_area(source, area_to_register);
        // DV184: override, since setup may already have registered this area as the wall's room area (get_room_area)
        RegisterSignal(area_to_register, COMSIG_AREA_FIRE_CHANGED, nameof(handle_fire), @override: TRUE);
        handle_fire(area_to_register, area_to_register.fire);
    }

    // tg: code/modules/power/lighting/light.dm:223
    public override void on_exit_area(datum source = null, area area_to_unregister = null)
    {
        base.on_exit_area(source, area_to_unregister);
        UnregisterSignal(area_to_unregister, COMSIG_AREA_FIRE_CHANGED);
    }

    // tg: code/modules/power/lighting/light.dm:227
    [SignalHandler]
    public virtual void handle_fire(area source = null, object new_fire = null)
    {
        Waiting.NoSleep(update());
    }

    // update the icon_state and luminosity of the light depending on its state
    // tg: code/modules/power/lighting/light.dm:232
    public virtual async DmTask update(float? trigger = null)
    {
        trigger ??= TRUE;
        float sw__1 = status;
        if (sw__1 == LIGHT_BROKEN || sw__1 == LIGHT_BURNED || sw__1 == LIGHT_EMPTY)
            on = FALSE;
        low_power_mode = FALSE;

        if (Dm.truthy(on))
        {
            float brightness_set = brightness;
            float power_set = bulb_power;
            object color_set = bulb_colour;
            if (Dm.truthy(color))
                color_set = color;
            if (Dm.truthy(reagents) || !Dm.truthy(is_full_charge()))
            {
                START_PROCESSING(SSmachines, this);
                if (reagents != null && Dm.truthy(Dm.bitand(await ((datum_reagents)reagents).spark_act(active_power_usage, SPARK_ACT_ENCLOSED), SPARK_ACT_DESTRUCTIVE)))
                {
                    message_admins(Dm.text("A rigged lightbulb at [] has exploded.", AREACOORD(this)));
                    qdel(this);
                    return;
                }
            }
            area local_area = get_room_area() as area;
            if (Dm.truthy(flickering))
            {
                brightness_set = brightness * bulb_low_power_brightness_mul;
                power_set = bulb_low_power_pow_mul;
                color_set = nightshift_light_color;
            }
            else if (Dm.truthy(local_area?.fire))
            {
                color_set = fire_colour;
                power_set = fire_power;
                brightness_set = fire_brightness;
            }
            else if (Dm.truthy(major_emergency))
            {
                color_set = bulb_emergency_colour;
                brightness_set = brightness * bulb_major_emergency_brightness_mul;
            }
            else if (Dm.truthy(nightshift_enabled))
            {
                brightness_set = nightshift_brightness;
                power_set = nightshift_light_power;
                if (!Dm.truthy(color))
                    color_set = nightshift_light_color;
            }
            if (Dm.truthy(cached_color_filter))
                color_set = apply_matrix_to_color(color_set, cached_color_filter["color"] as DmList, Dm.truthy(cached_color_filter["space"]) ? Dm.num(cached_color_filter["space"]) : COLORSPACE_RGB);
            // ADAPTED (D24): tg compares with the light source's values (light.light_range, light_power, light_color), which the
            // lighting module copies from the atom's light vars (lighting_source.dm:65-67, :390-427). The light source is not
            // ported and the atom's light stays null, so the comparison reads the vars it mirrors: the atom's own light,
            // present while it emits (update_light's test, lighting_atom.dm:50).
            bool light__source = Dm.truthy(light_power) && Dm.truthy(light_range) && Dm.truthy(light_on);
            float matching = Dm.num(light__source && brightness_set == light_range && power_set == light_power && Dm.eq(color_set, light_color));
            if (!Dm.truthy(matching))
            {
                switchcount++;
                if (prob(min(60, Dm.pow(switchcount, 2) * 0.01f)))
                {
                    if (Dm.truthy(trigger))
                        await burn_out();
                }
                else
                {
                    use_power = ACTIVE_POWER_USE;
                    await set_light(
                        l_range: brightness_set,
                        l_power: power_set,
                        l_color: color_set
                        );
                }
            }
        }
        // has_emergency_power(LIGHT_EMERGENCY_POWER_USE * SSMACHINES_DT): SSMACHINES_DT is (SSmachines.wait/10)
        // (__DEFINES/subsystems.dm:276), inline; rules 10.6 for a test harness world without SSmachines
        else if (Dm.truthy(has_emergency_power(LIGHT_EMERGENCY_POWER_USE * (Dm.num(SSmachines?.wait) / 10))) && !Dm.truthy(turned_off()))
        {
            use_power = IDLE_POWER_USE;
            low_power_mode = TRUE;
            if (reagents != null && Dm.truthy(Dm.bitand(await ((datum_reagents)reagents).spark_act(idle_power_usage, SPARK_ACT_ENCLOSED), SPARK_ACT_DESTRUCTIVE)))
            {
                message_admins(Dm.text("A rigged lightbulb at [] has exploded.", AREACOORD(this)));
                qdel(this);
                return;
            }
            START_PROCESSING(SSmachines, this);
        }
        else
        {
            use_power = IDLE_POWER_USE;
            await set_light(l_range: 0);
        }
        update_appearance();
        update_current_power_usage();
        broken_sparks(start_only: TRUE);
    }

    // tg: code/modules/power/lighting/light.dm:297
    public override object update_current_power_usage()
    {
        if (!Dm.truthy(on) && static_power_used > 0) //Light is off but still powered
        {
            removeStaticPower(static_power_used, AREA_USAGE_STATIC_LIGHT);
            static_power_used = 0;
        }
        else if (Dm.truthy(on)) //Light is on, just recalculate usage
        {
            float static_power_used_new = 0;
            area local_area = get_room_area() as area;
            if (Dm.truthy(nightshift_enabled) && !Dm.truthy(local_area?.fire))
                static_power_used_new = ceil(nightshift_brightness * nightshift_light_power * power_consumption_rate);
            else
                static_power_used_new = ceil(brightness * bulb_power * power_consumption_rate);
            if (static_power_used != static_power_used_new) //Consumption changed - update
            {
                removeStaticPower(static_power_used, AREA_USAGE_STATIC_LIGHT);
                static_power_used = static_power_used_new;
                addStaticPower(static_power_used, AREA_USAGE_STATIC_LIGHT);
            }
        }
        return null;
    }

    // tg: code/modules/power/lighting/light.dm:313
    public override async DmTask update_atom_colour()
    {
        await base.update_atom_colour();
        await update();
    }

    // tg: code/modules/power/lighting/light.dm:317
    public virtual void broken_sparks(float? start_only = null)
    {
        start_only ??= FALSE;
        if (!QDELETED(this) && status == LIGHT_BROKEN && Dm.truthy(has_power()) && MC_RUNNING())
        {
            if (!Dm.truthy(start_only))
                do_sparks(3, TRUE, this);
            float delay = rand(BROKEN_SPARKS_MIN, BROKEN_SPARKS_MAX);
            _addtimer(Dm.@new<datum_callback>(this, nameof(broken_sparks)), delay, Dm.bitor(TIMER_UNIQUE, TIMER_NO_HASH_WAIT), file: "code/modules/power/lighting/light.dm", line: 322);
        }
    }

    // tg: code/modules/power/lighting/light.dm:324
    public virtual object is_full_charge()
    {
        if (Dm.truthy(cell))
            return Dm.num(cell.charge == cell.maxcharge);
        return TRUE;
    }

    // tg: code/modules/power/lighting/light.dm:329
    [DmParams("seconds_per_tick")]
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (Dm.truthy(has_power()))
        {
            // If the cell is done mooching station power, and reagents don't need processing, stop processing
            if (Dm.truthy(is_full_charge()) && !Dm.truthy(reagents))
                return PROCESS_KILL;
            if (Dm.truthy(cell))
                Waiting.NoSleep(charge_cell__proc(LIGHT_EMERGENCY_POWER_USE * Dm.num(seconds_per_tick), cell: cell)); //Recharge emergency power automatically while not using it
        }
        if (Dm.truthy(reagents)) //with most reagents coming out at 300, and with most meaningful reactions coming at 370+, this rate gives a few seconds of time to place it in and get out of dodge regardless of input.
        {
            ((datum_reagents)reagents).adjust_thermal_energy(8 * ((datum_reagents)reagents).total_volume * SPECIFIC_HEAT_DEFAULT * Dm.num(seconds_per_tick));
            Waiting.NoSleep(((datum_reagents)reagents).handle_reactions());
        }
        if (Dm.truthy(low_power_mode) && !Dm.truthy(Waiting.NoSleep(use_emergency_power(LIGHT_EMERGENCY_POWER_USE * Dm.num(seconds_per_tick)))))
        {
            Waiting.NoSleep(update(FALSE)); //Disables emergency mode and sets the color to normal
            return PROCESS_KILL;
        }
        return null;
    }

    // tg: code/modules/power/lighting/light.dm:343
    public virtual async DmTask burn_out()
    {
        if (status == LIGHT_OK)
        {
            status = LIGHT_BURNED;
            icon_state = Dm.text("[]-burned", base_state);
            on = FALSE;
            await set_light(l_range: 0);
        }
    }

    // attempt to set the light's on/off status
    // will not switch on if broken/burned/empty
    // tg: code/modules/power/lighting/light.dm:352
    public virtual async DmTask set_on(object turn_on = null)
    {
        float was_on = on;
        on = Dm.num(Dm.truthy(turn_on) && status == LIGHT_OK);
        if (on == was_on)
            return;
        if (Dm.truthy(this._listen_lookup?[COMSIG_LIGHT_FIXTURE_TOGGLED]))
            this._SendSignal(COMSIG_LIGHT_FIXTURE_TOGGLED, Dm.list(this, on));
        await update();
    }

    // tg: code/modules/power/lighting/light.dm:360
    public override object get_cell(atom_movable @interface = null, mob user = null)
    {
        if (Dm.truthy(has_mock_cell))
        {
            cell = Dm.@new<obj_item_stock_parts_power_store_cell_emergency_light>(this);
            has_mock_cell = FALSE;
        }

        return cell;
    }

    // tg: code/modules/power/lighting/light.dm:368
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        float sw__1 = status;
        if (sw__1 == LIGHT_OK)
            Dm.append(ref dot, span_notice(I18n.T("It is turned [].", Dm.truthy(on) ? I18n.T("on") : I18n.T("off"))));
        else if (sw__1 == LIGHT_EMPTY)
            Dm.append(ref dot, span_notice(I18n.T("The [] has been removed.", fitting)));
        else if (sw__1 == LIGHT_BURNED)
            Dm.append(ref dot, span_danger(I18n.T("The [] is burnt out.", fitting)));
        else if (sw__1 == LIGHT_BROKEN)
            Dm.append(ref dot, span_danger(I18n.T("The [] has been smashed.", fitting)));
        if (cell != null || Dm.truthy(has_mock_cell))
            Dm.append(ref dot, span_notice(I18n.T("Its backup power charge meter reads []%.", Dm.truthy(has_mock_cell) ? 100 : round((cell.charge / cell.maxcharge) * 100, 0.1f))));
        return dot;
    }

    // tg: code/modules/power/lighting/light.dm:385
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        // attempt to insert light
        if (istype(tool, typeof(obj_item_light)))
        {
            if (status == LIGHT_OK)
            {
                to_chat(user, span_warning(I18n.T("There is a [] already inserted!", fitting)));
                return ITEM_INTERACT_BLOCKING;
            }
            add_fingerprint(user);
            obj_item_light light_object = tool as obj_item_light;
            if (!istype(light_object, light_type))
            {
                to_chat(user, span_warning(I18n.T("This type of light requires a []!", fitting)));
                return ITEM_INTERACT_BLOCKING;
            }
            if (!Dm.truthy(await user.temporarilyRemoveItemFromInventory(light_object)))
                return ITEM_INTERACT_BLOCKING;

            add_fingerprint(user);
            if (status != LIGHT_EMPTY)
            {
                await drop_light_tube(user);
                to_chat(user, span_notice(I18n.T("You replace [].", light_object)));
            }
            else
                to_chat(user, span_notice(I18n.T("You insert [].", light_object)));
            if (Dm.truthy(Dm.length(((datum_reagents)light_object.reagents)?.reagent_list)))
            {
                create_reagents(LIGHT_REAGENT_CAPACITY, Dm.bitor(SEALED_CONTAINER, TRANSPARENT));
                await ((datum_reagents)light_object.reagents).trans_to(reagents, LIGHT_REAGENT_CAPACITY);
            }
            status = light_object.status;
            switchcount = light_object.switchcount;
            brightness = light_object.brightness;
            on = Dm.num(has_power());
            await update();

            qdel(light_object);

            return ITEM_INTERACT_SUCCESS;
        }

        // attempt to stick weapon into light socket
        if (status != LIGHT_EMPTY || Dm.truthy(user.combat_mode) || istype(tool, typeof(obj_item_lightreplacer)))
            return NONE;

        if (Dm.truthy(Dm.bitand(tool.item_flags, ABSTRACT)))
            return NONE;

        to_chat(user, span_userdanger(I18n.T("You stick \\the [] into the light socket!", tool)));
        if (Dm.truthy(has_power()) && Dm.truthy(Dm.bitand(tool.obj_flags, CONDUCTS_ELECTRICITY)))
        {
            do_sparks(3, TRUE, this);
            if (prob(75))
                await electrocute_mob(user as mob_living_carbon, get_area(this), this, (rand(7, 10) * 0.1f), TRUE);
        }

        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/power/lighting/light.dm:433
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        if (status != LIGHT_EMPTY || Dm.truthy(user.combat_mode))
            return NONE;
        tool.play_tool_sound(this, 75);
        user.visible_message(span_notice(I18n.T("[] opens []'s casing.", user.name, this)),
            span_notice(I18n.T("You open []'s casing.", this)),
            span_hear(I18n.T("You hear unscrewing.")));
        await deconstruct(disassembled: TRUE);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/power/lighting/light.dm:443
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        obj_structure_light_construct frame = null;
        string sw__1 = fitting;
        if (sw__1 == "tube")
            frame = Dm.@new<obj_structure_light_construct>(loc);
        else if (sw__1 == "bulb")
            frame = Dm.@new<obj_structure_light_construct_small>(loc);
        else if (sw__1 == "floor bulb")
            frame = Dm.@new<obj_structure_light_construct_floor>(loc);
        frame.stage = LIGHT_CONSTRUCT_WIRED;
        frame.icon_state = Dm.text("[]-construct-stage2", frame.fixture_type);
        await frame.setDir(dir);
        bool hangs = Dm.truthy(await frame.find_and_mount_on_atom());
        if (!Dm.truthy(disassembled))
        {
            await frame.take_damage(frame.max_integrity * 0.5f, sound_effect: FALSE);
            if (status != LIGHT_BROKEN)
                await break_light_tube();
            if (status != LIGHT_EMPTY)
                await drop_light_tube();
            Dm.@new<obj_item_stack_cable_coil>(loc, 1, TRUE); // tg: 1, "red": the stale colour lands in `merge` (cable.dm:515), truthy
        }
        transfer_fingerprints_to(frame);

        obj_item_stock_parts_power_store real_cell = get_cell() as obj_item_stock_parts_power_store;
        if (!QDELETED(real_cell))
        {
            await real_cell.forceMove(frame);
            frame.cell = real_cell;
        }
        // DEVIATION(DV178): a wall frame with no wall to hang on falls apart (tg leaves it hanging where the wall was)
        if (!hangs && frame is not obj_structure_light_construct_floor)
            await frame.deconstruct(FALSE);
        return null;
    }

    // tg: code/modules/power/lighting/light.dm:471
    public override async DmTask<object> attacked_by(obj_item attacking_object = null, mob_living user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        object dot = await base.attacked_by(attacking_object, user, modifiers, attack_modifiers);
        if (Dm.le(dot, 0))
            return dot;
        if (status != LIGHT_BROKEN && status != LIGHT_EMPTY)
            return dot;
        if (!Dm.truthy(on) || !Dm.truthy(Dm.bitand(attacking_object.obj_flags, CONDUCTS_ELECTRICITY)))
            return dot;
        if (!prob(12))
            return dot;
        await electrocute_mob(user as mob_living_carbon, get_area(this), this, 0.3f, TRUE);
        return dot;
    }

    // tg: code/modules/power/lighting/light.dm:483
    public override async DmTask<object> take_damage(object damage_amount = null, string damage_type = null, object damage_flag = null, float? sound_effect = null, object attack_dir = null, float? armour_penetration = null)
    {
        object dot = await base.take_damage(damage_amount, damage_type, damage_flag, sound_effect, attack_dir, armour_penetration);
        if (Dm.truthy(dot) && !QDELETED(this))
        {
            if (prob(Dm.num(damage_amount) * 5))
                await break_light_tube();
        }
        return dot;
    }

    // tg: code/modules/power/lighting/light.dm:489
    public override object play_attack_sound(object damage_amount = null, string damage_type = null, object damage_flag = null, params object[] __extra)
    {
        damage_type ??= BRUTE;
        object sw__1 = damage_type;
        if (Dm.eq(sw__1, BRUTE))
        {
            if (status == LIGHT_EMPTY)
                playsound(loc, Dm.resource("sound/items/weapons/smash.ogg"), 50, TRUE);
            else if (status == LIGHT_BROKEN)
                playsound(loc, Dm.resource("sound/effects/hit_on_shattered_glass.ogg"), 90, TRUE);
            else
                playsound(loc, Dm.resource("sound/effects/glass/glasshit.ogg"), 90, TRUE);
        }
        else if (Dm.eq(sw__1, BURN))
            playsound(loc, Dm.resource("sound/items/tools/welder.ogg"), 100, TRUE);
        return null;
    }

    // returns if the light has power /but/ is manually turned off
    // if a light is turned off, it won't activate emergency power
    // tg: code/modules/power/lighting/light.dm:504
    public virtual object turned_off()
    {
        area local_area = get_room_area() as area;
        return Dm.num((Dm.truthy(local_area) && !Dm.truthy(local_area.lightswitch) && Dm.truthy(local_area.power_light)) || Dm.truthy(flickering));
    }

    // returns whether this light has power
    // true if area has power and lightswitch is on
    // tg: code/modules/power/lighting/light.dm:510
    public virtual object has_power()
    {
        area local_area = get_room_area() as area;
        // local_area && local_area.lightswitch && local_area.power_light: rules 4.5, the first false operand or the last
        return (!Dm.truthy(local_area) ? (object)local_area : (!Dm.truthy(local_area.lightswitch) ? local_area.lightswitch : local_area.power_light));
    }

    // returns whether this light has emergency power
    // can also return if it has access to a certain amount of that power
    // tg: code/modules/power/lighting/light.dm:516
    public virtual object has_emergency_power(object power_usage_amount = null)
    {
        if (Dm.truthy(no_low_power) || (!Dm.truthy(cell) && !Dm.truthy(has_mock_cell)))
            return FALSE;
        if (Dm.truthy(has_mock_cell))
            return Dm.num(status == LIGHT_OK);
        if (Dm.truthy(power_usage_amount) ? cell.charge >= Dm.num(power_usage_amount) : Dm.truthy(cell.charge))
            return Dm.num(status == LIGHT_OK);
        return FALSE;
    }

    // attempts to use power from the installed emergency cell, returns true if it does and false if it doesn't
    // tg: code/modules/power/lighting/light.dm:526
    public virtual async DmTask<object> use_emergency_power(float? power_usage_amount = null)
    {
        power_usage_amount ??= LIGHT_EMERGENCY_POWER_USE;
        if (!Dm.truthy(has_emergency_power(power_usage_amount)))
            return FALSE;
        obj_item_stock_parts_power_store real_cell = get_cell() as obj_item_stock_parts_power_store;
        if (real_cell.charge > 2.5f * Dm.initial(typeof(obj_item_stock_parts_power_store_cell_emergency_light), (obj_item_stock_parts_power_store_cell_emergency_light p) => p.maxcharge)) //it's meant to handle 120 W, ya doofus
        {
            visible_message(span_warning(I18n.T("[] short-circuits from too powerful of a power cell!", this)));
            await burn_out();
            return FALSE;
        }
        await real_cell.use(power_usage_amount);
        await set_light(
            l_range: brightness * bulb_low_power_brightness_mul,
            l_power: max(bulb_low_power_pow_min, bulb_low_power_pow_mul * (real_cell.charge / real_cell.maxcharge)),
            l_color: bulb_low_power_colour
            );
        return TRUE;
    }

    // tg: code/modules/power/lighting/light.dm:542  (set waitfor = FALSE: the body sleeps in stoplag, so this is the wrapper of
    // waiting-model.md 5.4; M7 slice 3: the power network's flicker, powernet.dm:126)
    public virtual object flicker(float? amount = null)
    {
        DotBox<object> dot = new DotBox<object>();
        return Waiting.WaitforFalse(flicker__body(amount, dot), dot);
    }

    private async DmTask<object> flicker__body(float? amount, DotBox<object> dot)
    {
        amount ??= 1;
        if (Dm.truthy(flickering) || !Dm.truthy(on) || status != LIGHT_OK)
            return dot.Value;

        dot.Value = TRUE; // did we actually flicker? Send this now because we expect immediate response, before sleeping.
        await set_light(
            l_range: brightness * bulb_low_power_brightness_mul,
            l_power: bulb_low_power_pow_mul,
            l_color: nightshift_light_color
        );
        cut_overlays(); // tg passes src, which cut_overlays does not declare (dropped, rules 7.3)
        await stoplag(0.7f * SECONDS);
        if (prob(30))
            do_sparks(number: 2, cardinal_only: TRUE, source: this);

        for (float i = 1, i__end = Dm.num(amount); i <= i__end; i++)
        {
            if (status != LIGHT_OK || !Dm.truthy(has_power()))
                break;
            flickering = Dm.not(flickering);
            await update(FALSE);
            await stoplag(pick(Dm.list(2 * SECONDS, 4 * SECONDS, 6 * SECONDS)));
        }

        if (Dm.truthy(has_power()))
            on = Dm.num(status == LIGHT_OK);
        else
            on = FALSE;

        flickering = FALSE;
        await update(FALSE);
        return dot.Value;
    }

    // tg: code/modules/power/lighting/light.dm:584
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        object dot = await base.attack_hand_secondary(user, modifiers);
        if (Dm.eq(dot, SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN))
            return dot;
        user.changeNext_move(CLICK_CD_MELEE);
        // tg: code/modules/power/lighting/light.dm:589
        add_fingerprint(user);

        if (status == LIGHT_EMPTY)
        {
            to_chat(user, span_warning(I18n.T("There is no [] in this light!", fitting)));
            return dot;
        }

        // make it burn hands unless you're wearing heat insulated gloves or have the RESISTHEAT/RESISTHEATHANDS traits
        if (!Dm.truthy(on))
        {
            to_chat(user, span_notice(I18n.T("You remove the light [].", fitting)));
            // create a light tube/bulb item and put it in the user's hand
            await drop_light_tube(user);
            return dot;
        }

        float @protected = FALSE;

        if (user is mob_living_carbon_human human)
        {
            object maybe_stomach = human.get_organ_slot(ORGAN_SLOT_STOMACH);
            if (istype(maybe_stomach, typeof(obj_item_organ_stomach_ethereal)))
            {
                obj_item_organ_stomach_ethereal stomach = maybe_stomach as obj_item_organ_stomach_ethereal;
                if (stomach.drain_time > Dm.num(world.time))
                    return null;
                to_chat(user, span_notice(I18n.T("You start channeling some power through the [] into your body.", fitting)));
                stomach.drain_time = Dm.num(world.time) + LIGHT_DRAIN_TIME;
                while (Dm.truthy(await do_after(user, LIGHT_DRAIN_TIME, target: this)))
                {
                    stomach.drain_time = Dm.num(world.time) + LIGHT_DRAIN_TIME;
                    if (istype(stomach, typeof(obj_item_organ_stomach_ethereal)))
                    {
                        to_chat(user, span_notice(I18n.T("You receive some charge from the [].", fitting)));
                        await stomach.adjust_charge(LIGHT_POWER_GAIN);
                    }
                    else
                        to_chat(user, span_warning(I18n.T("You can't receive charge from the []!", fitting)));
                }
                return null;
            }

            if (human.gloves != null)
            {
                obj_item_clothing_gloves electrician_gloves = human.gloves as obj_item_clothing_gloves;
                if (Dm.truthy(electrician_gloves.max_heat_protection_temperature) && Dm.gt(electrician_gloves.max_heat_protection_temperature, 360))
                    @protected = TRUE;
            }
        }
        else
            @protected = TRUE;

        if (Dm.truthy(@protected) || HAS_TRAIT(user, TRAIT_RESISTHEAT) || HAS_TRAIT(user, TRAIT_RESISTHEATHANDS))
            to_chat(user, span_notice(I18n.T("You remove the light [].", fitting)));
        else if (user is mob_living_carbon_human telekinetic && Dm.truthy(telekinetic.dna.check_mutation(typeof(datum_mutation_telekinesis))))
            to_chat(user, span_notice(I18n.T("You telekinetically remove the light [].", fitting)));
        else
        {
            mob_living_carbon_human burnt = user as mob_living_carbon_human;
            obj_item_bodypart affecting = burnt.get_active_hand() as obj_item_bodypart;
            await burnt.apply_damage(5, BURN, affecting, wound_bonus: CANT_WOUND);
            if (HAS_TRAIT(user, TRAIT_LIGHTBULB_REMOVER))
            {
                to_chat(user, span_notice(I18n.T("You feel your [] burning, but the light begins to budge...", affecting.plaintext_zone)));
                if (!Dm.truthy(await do_after(user, 5 * SECONDS, target: this)))
                    return dot;
                await burnt.apply_damage(10, BURN, burnt.get_active_hand(), wound_bonus: CANT_WOUND);
                to_chat(user, span_notice(I18n.T("You manage to remove the light [], shattering it in process.", fitting)));
                await break_light_tube();
            }
            else
            {
                to_chat(user, span_warning(I18n.T("You try to remove the light [], but you burn your hand on it!", fitting)));
                return dot;
            }
        }
        // create a light tube/bulb item and put it in the user's hand
        await drop_light_tube(user);
        return dot;
    }

    // tg: code/modules/power/lighting/light.dm:648
    public virtual async DmTask set_major_emergency_light()
    {
        major_emergency = TRUE;
        await update();
    }

    // tg: code/modules/power/lighting/light.dm:652
    public virtual async DmTask unset_major_emergency_light()
    {
        major_emergency = FALSE;
        await update();
    }

    // tg: code/modules/power/lighting/light.dm:656
    public virtual async DmTask<object> drop_light_tube(mob user = null)
    {
        obj_item_light light_object = Dm.@new(light_type) as obj_item_light;
        if (reagents != null)
        {
            await ((datum_reagents)reagents).trans_to(light_object.reagents, LIGHT_REAGENT_CAPACITY);
            qdel(reagents);
            reagents = null;
        }
        light_object.status = status;
        light_object.brightness = brightness;

        // light item inherits the switchcount, then zero it
        light_object.switchcount = switchcount;
        switchcount = 0;

        light_object.update_appearance();
        await light_object.forceMove(loc);

        if (Dm.truthy(user)) //puts it in our active hand
        {
            // tg: code/modules/power/lighting/light.dm:672
            light_object.add_fingerprint(user);
            await user.put_in_active_hand(light_object);
        }

        status = LIGHT_EMPTY;
        await update();
        return light_object;
    }

    // break the light and make sparks if was on, state is mutated BEFORE firing side-effects to prevent re-entrancy loops from synchronous signals.
    // tg: code/modules/power/lighting/light.dm:690
    public virtual async DmTask break_light_tube(float? skip_sound_and_sparks = null)
    {
        skip_sound_and_sparks ??= FALSE;
        if (status == LIGHT_EMPTY || status == LIGHT_BROKEN)
            return;

        float was_ok = Dm.num(status == LIGHT_OK || status == LIGHT_BURNED);
        status = LIGHT_BROKEN;

        if (Dm.truthy(this._listen_lookup?[COMSIG_LIGHT_FIXTURE_BROKEN]))
            this._SendSignal(COMSIG_LIGHT_FIXTURE_BROKEN, Dm.list(this, was_ok));
        if (!Dm.truthy(skip_sound_and_sparks))
        {
            if (Dm.truthy(was_ok))
                playsound(loc, Dm.resource("sound/effects/glass/glasshit.ogg"), 75, TRUE); // tg: code/modules/power/lighting/light.dm:700
            if (Dm.truthy(on))
                do_sparks(3, TRUE, this);
        }
        await update();
    }

    // tg: code/modules/power/lighting/light.dm:705
    public virtual async DmTask fix()
    {
        if (status == LIGHT_OK)
            return;
        status = LIGHT_OK;
        brightness = Dm.initial(this, (obj_machinery_light p) => p.brightness);
        on = TRUE;
        await update();
    }

    // tg: code/modules/power/lighting/light.dm:713  (the M7 zaps slice)
    public override async DmTask<object> zap_act(object power = null, object zap_flags = null)
    {
        object dot = null;
        object explosive = Dm.bitand(zap_flags, ZAP_MACHINE_EXPLOSIVE);
        zap_flags = Dm.bitand(zap_flags, Dm.bitnot(Dm.bitor(ZAP_MACHINE_EXPLOSIVE, ZAP_OBJ_DAMAGE)));
        dot = await base.zap_act(power, zap_flags);
        if (Dm.truthy(explosive))
        {
            await explosion(this, flame_range: 5, adminlog: FALSE);
            qdel(this);
        }
        return dot;
    }

    // called when area power state changes
    // tg: code/modules/power/lighting/light.dm:722
    public override object power_change()
    {
        area local_area = get_room_area() as area;
        // local_area.lightswitch && local_area.power_light: rules 4.5, the first false operand or the last
        Waiting.NoSleep(set_on(!Dm.truthy(local_area.lightswitch) ? local_area.lightswitch : local_area.power_light));
        return null;
    }

    // called when heated

    // tg: code/modules/power/lighting/light.dm:729
    public override object should_atmos_process(datum_gas_mixture air = null, object exposed_temperature = null)
    {
        return Dm.num(Dm.gt(exposed_temperature, 673));
    }

    // tg: code/modules/power/lighting/light.dm:732
    public override async DmTask<object> atmos_expose(datum_gas_mixture air = null, object exposed_temperature = null)
    {
        if (prob(max(0, Dm.num(Dm.sub(exposed_temperature, 673))))) //0% at <400C, 100% at >500C
            await break_light_tube();
        return null;
    }

    // tg: code/modules/power/lighting/light.dm:736
    [SignalHandler]
    public virtual object on_light_eater(obj_machinery_light source = null, datum light_eater = null)
    {
        Waiting.NoSleep(break_light_tube());
        return COMPONENT_BLOCK_LIGHT_EATER;
    }

    // tg: code/modules/power/lighting/light.dm:679
    public override async DmTask<object> attack_tk(mob user = null)
    {
        if (status == LIGHT_EMPTY)
        {
            to_chat(user, span_warning(I18n.T("There is no [] in this light!", fitting)));
            return null;
        }

        to_chat(user, span_notice(I18n.T("You telekinetically remove the light [].", fitting)));
        // create a light tube/bulb item and put it in the user's hand
        obj_item_light light_tube = await drop_light_tube() as obj_item_light;
        return await light_tube.attack_tk(user);
    }

    // tg: code/modules/power/lighting/light.dm:575
    public override async DmTask<object> attack_ai(mob user = null)
    {
        no_low_power = Dm.num(!Dm.truthy(no_low_power));
        to_chat(user, span_notice(I18n.T("Emergency lights for this fixture have been [].", Dm.truthy(no_low_power) ? I18n.T("disabled") : I18n.T("enabled"))));
        await update(FALSE);
        return null;
    }

    // tg: code/modules/power/lighting/light.dm:741
    public override async DmTask<object> on_saboteur(datum source = null, object disrupt_duration = null)
    {
        await base.on_saboteur(source, disrupt_duration);
        await break_light_tube();
        return TRUE;
    }

    // tg: code/modules/power/lighting/light.dm:746
    [SignalHandler]
    public virtual void grey_tide(datum source = null, DmList grey_tide_areas = null)
    {
        foreach (object area_type in Dm.each(grey_tide_areas))
        {
            if (!istype(get_area(this), (Type)area_type))
                continue;
            Waiting.DetachSync(() => flicker());
        }
    }

    // tg: code/modules/power/lighting/light.dm:754  (M4c slice 8; reached only by a z-level change, which the one loaded level, D108, never makes)
    public override async DmTask on_changed_z_level(turf old_turf = null, turf new_turf = null, object same_z_layer = null, float? notify_contents = null)
    {
        await base.on_changed_z_level(old_turf, new_turf, same_z_layer, notify_contents);
        if (!QDELING(this))
            await update(FALSE);
    }
}

// tg: code/modules/power/lighting/light.dm:759
public partial class obj_machinery_light_floor : obj_machinery_light
{
    // tg: code/modules/power/lighting/light.dm:774
    public override object get_turfs_to_mount_on()
    {
        return Dm.list(get_turf(this));
    }

    // tg: code/modules/power/lighting/light.dm:777
    public override object is_mountable_turf(turf target = null)
    {
        return Dm.num(!isgroundlessturf(target));
    }

    // tg: code/modules/power/lighting/light.dm:780
    public override object get_mountable_objects()
    {
        return get_mountable_objects__attachables__floor;
    }

    // tg: code/modules/power/lighting/light.dm:781  var/static/list/attachables (a proc-level static, rules 10.6)
    private static ref DmList get_mountable_objects__attachables__floor => ref TgState.Current.obj_machinery_light_floor__get_mountable_objects__attachables;

    // tg: code/modules/power/lighting/light.dm:788
    public override object get_light_offset()
    {
        return Dm.list(0, 0);
    }
}

public sealed partial class TgState
{
    // tg: code/modules/power/lighting/light.dm:781  /obj/machinery/light/floor/get_mountable_objects: var/static/list/attachables
    internal DmList obj_machinery_light_floor__get_mountable_objects__attachables = Dm.list(typeof(obj_structure_thermoplastic), typeof(obj_structure_lattice_catwalk));
}
