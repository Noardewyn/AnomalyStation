// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/modular_shield.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Engineering package E10 (D287): the whole file. The modular shield generator (buildable, not mapped) with its ModularShieldGen
// window: it charges to its strength, then projects a ring of hardlight around itself (the radius set in the window, inside and
// outside or outside only) that turns solid two seconds later and soaks attacks, hot gas, EMPs and explosions out of the stored
// strength; under five it collapses and recovers. The gate projects a flat wall along its facing. The nodes, the cable, the
// chargers, the relays and the wells connect by facing (a multitool syncs them) and add regeneration, radius and strength from
// their parts. The modules' examine is translated (X1). The var blocks are generated (D111).

// tg: code/game/machinery/modular_shield.dm:1
public partial class obj_machinery_modular_shield_generator : obj_machinery
{
    // tg: code/game/machinery/modular_shield.dm:79
    public override object power_change()
    {
        object dot = base.power_change();
        if (!Dm.truthy(Dm.bitand(machine_stat, NOPOWER)))
        {
            begin_processing();
            return dot;
        }

        deactivate_shields();
        end_processing();
        return dot;
    }

    // tg: code/game/machinery/modular_shield.dm:88
    public override async DmTask<object> RefreshParts()
    {
        object dot = await base.RefreshParts();

        innate_regen = Dm.initial(this, (obj_machinery_modular_shield_generator g) => g.innate_regen);
        innate_radius = Dm.initial(this, (obj_machinery_modular_shield_generator g) => g.innate_radius);
        innate_strength = Dm.initial(this, (obj_machinery_modular_shield_generator g) => g.innate_strength);

        foreach (datum_stock_part_capacitor new_capacitor in Dm.each<datum_stock_part_capacitor>(component_parts))
            innate_strength += Dm.num(new_capacitor.tier) * 20;

        foreach (datum_stock_part_servo new_servo in Dm.each<datum_stock_part_servo>(component_parts))
            innate_regen += Dm.num(new_servo.tier) * 2;

        foreach (datum_stock_part_micro_laser new_laser in Dm.each<datum_stock_part_micro_laser>(component_parts))
            innate_radius += Dm.num(new_laser.tier) * 0.25f;

        calculate_regeneration();
        calculate_max_strength();
        calculate_radius();
        return dot;
    }

    // tg: code/game/machinery/modular_shield.dm:108
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        set_wires(Dm.@new<datum_wires_modular_shield_generator>(this));
        if (Dm.truthy(mapload) && Dm.truthy(active) && Dm.truthy(anchored))
            Waiting.NoSleep(activate_shields());
        return dot;
    }

    ///qdels the forcefield and calls calculate regen to update the regen value accordingly
    // tg: code/game/machinery/modular_shield.dm:133
    public virtual void deactivate_shields()
    {
        active = FALSE;
        QDEL_LIST(deployed_shields);
        deployed_shields = null;
        LAZYNULL(ref list_of_turfs);
        LAZYNULL(ref inside_shield);
        calculate_regeneration();
    }

    // tg: code/game/machinery/modular_shield.dm:141
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        return default_deconstruction_screwdriver(user, tool);
    }

    // tg: code/game/machinery/modular_shield.dm:144
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        return await default_deconstruction_crowbar(user, tool);
    }

    // tg: code/game/machinery/modular_shield.dm:147
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(panel_open) || !Dm.truthy(is_wire_tool(tool)))
            return NONE;
        await wires.interact(user);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/modular_shield.dm:153
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        obj_item_multitool multi = (obj_item_multitool)tool;
        multi.set_buffer(this);
        balloon_alert(user, I18n.T("saved to buffer"));
        return ITEM_INTERACT_SUCCESS;
    }

    ///toggles the forcefield on and off
    // tg: code/game/machinery/modular_shield.dm:159
    public virtual async DmTask toggle_shields()
    {
        if (Dm.truthy(initiating))
            return;
        if (Dm.truthy(active))
        {
            deactivate_shields();
            return;
        }
        if (Dm.truthy(recovering))
            return;
        await activate_shields();
    }

    // tg: code/game/machinery/modular_shield.dm:169
    public override async DmTask<object> onShuttleMove(turf newT = null, turf oldT = null, DmList movement_force = null, object move_dir = null, obj_docking_port_stationary old_dock = null, obj_docking_port_mobile moving_dock = null)
    {
        object dot = await base.onShuttleMove(newT, oldT, movement_force, move_dir, old_dock, moving_dock);
        if (Dm.truthy(active))
            deactivate_shields();
        return dot;
    }

    ///generates the forcefield based on the given radius and calls calculate_regen to update the regen value accordingly
    // tg: code/game/machinery/modular_shield.dm:175
    public virtual async DmTask activate_shields()
    {
        if (Dm.truthy(active) || Dm.truthy(Dm.bitand(machine_stat, NOPOWER))) //bug or did admin call proc on already active shield gen?
            return;
        if (Dm.num(radius) < 0) //what the fuck are admins doing
            radius = Dm.initial(this, (obj_machinery_modular_shield_generator g) => g.radius);
        active = TRUE;
        initiating = TRUE;
        object color_shield = Dm.truthy(cached_color_filter) ? cached_color_filter : color;
        if (Dm.num(radius) >= 10) //the shield is large so we are going to use the midpoint formula and clamp it to the lowest full number in order to save processing power
        {
            LAZYADD(ref inside_shield, circle_range_turfs(this, Dm.num(radius) - 1)); //in the future we might want to apply an effect to turfs inside the shield
            LAZYADD(ref list_of_turfs, get_perimeter(this, radius));

            if (Dm.truthy(exterior_only))
            {
                foreach (turf_open target_tile in Dm.each<turf_open>(list_of_turfs))
                {
                    if (isfloorturf(target_tile))
                        continue;
                    if (Dm.truthy(Dm.locate<obj_structure_emergency_shield_modular>(target_tile)))
                        continue;
                    obj_structure_emergency_shield_modular deploying_shield = Dm.@new<obj_structure_emergency_shield_modular>(target_tile, this);
                    LAZYADD(ref deployed_shields, deploying_shield);
                    if (Dm.truthy(color_shield))
                        await deploying_shield.add_atom_colour(color_shield, FIXED_COLOUR_PRIORITY);
                }
                _addtimer(Dm.@new<datum_callback>(this, nameof(finish_field)), 2 * SECONDS, file: "code/game/machinery/modular_shield.dm", line: 199);
                calculate_regeneration();
                return;
            }

            foreach (turf_open target_tile in Dm.each<turf_open>(list_of_turfs))
            {
                if (Dm.truthy(Dm.locate<obj_structure_emergency_shield_modular>(target_tile)))
                    continue;
                obj_structure_emergency_shield_modular deploying_shield = Dm.@new<obj_structure_emergency_shield_modular>(target_tile, this);
                LAZYADD(ref deployed_shields, deploying_shield);
                if (Dm.truthy(color_shield))
                    await deploying_shield.add_atom_colour(color_shield, FIXED_COLOUR_PRIORITY);
            }

            _addtimer(Dm.@new<datum_callback>(this, nameof(finish_field)), 2 * SECONDS, file: "code/game/machinery/modular_shield.dm", line: 211);
            calculate_regeneration();
            return;
        }

        //this code only runs on radius less than 10 and gives us a more accurate circle that is more compatible with decimal values
        LAZYADD(ref inside_shield, circle_range_turfs(this, Dm.num(radius) - 1)); //in the future we might want to apply an effect to the turfs inside the shield
        if (Dm.truthy(exterior_only))
        {
            foreach (turf_open target_tile in Dm.each<turf_open>(circle_range_turfs(this, radius)))
            {
                if (isfloorturf(target_tile))
                    continue;
                if (Dm.@in(target_tile, inside_shield))
                    continue;
                if (Dm.truthy(Dm.locate<obj_structure_emergency_shield_modular>(target_tile)))
                    continue;
                obj_structure_emergency_shield_modular deploying_shield = Dm.@new<obj_structure_emergency_shield_modular>(target_tile, this);
                LAZYADD(ref deployed_shields, deploying_shield);
                if (Dm.truthy(color_shield))
                    await deploying_shield.add_atom_colour(color_shield, FIXED_COLOUR_PRIORITY);
            }

            _addtimer(Dm.@new<datum_callback>(this, nameof(finish_field)), 2 * SECONDS, file: "code/game/machinery/modular_shield.dm", line: 230);
            calculate_regeneration();
            return;
        }

        foreach (turf_open target_tile in Dm.each<turf_open>(circle_range_turfs(this, radius)))
        {
            if (Dm.@in(target_tile, inside_shield))
                continue;
            if (Dm.truthy(Dm.locate<obj_structure_emergency_shield_modular>(target_tile)))
                continue;
            obj_structure_emergency_shield_modular deploying_shield = Dm.@new<obj_structure_emergency_shield_modular>(target_tile, this);
            LAZYADD(ref deployed_shields, deploying_shield);
            if (Dm.truthy(color_shield))
                await deploying_shield.add_atom_colour(color_shield, FIXED_COLOUR_PRIORITY);
        }

        _addtimer(Dm.@new<datum_callback>(this, nameof(finish_field)), 2 * SECONDS, file: "code/game/machinery/modular_shield.dm", line: 243);
        calculate_regeneration();
    }

    ///After giving people a grace period to react to we up the alpha value and make the forcefield dense
    // tg: code/game/machinery/modular_shield.dm:246
    public virtual void finish_field()
    {
        foreach (obj_structure_emergency_shield_modular current_shield in Dm.each<obj_structure_emergency_shield_modular>(deployed_shields))
        {
            current_shield.set_density(TRUE);
            current_shield.alpha = 255;
        }
        initiating = FALSE;
    }

    // tg: code/game/machinery/modular_shield.dm:253
    public override object Destroy(float? force = null)
    {
        QDEL_LIST(deployed_shields);
        foreach (obj_machinery_modular_shield_module disconnecting in Dm.each<obj_machinery_modular_shield_module>(connected_modules))
        {
            disconnecting.shield_generator = null;
            Waiting.NoSleep(disconnecting.update_icon_state());
        }
        return base.Destroy(force);
    }

    // tg: code/game/machinery/modular_shield.dm:260
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("[]_[][]", icon_type,
            !Dm.truthy(Dm.bitand(machine_stat, NOPOWER)) ? (Dm.truthy(recovering) ? "recovering_" : "ready_") : "no_power_",
            Dm.truthy(panel_open) ? "open" : "closed");
        return await base.update_icon_state(__extra);
    }

    //ui stuff
    // tg: code/game/machinery/modular_shield.dm:266
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        object dot = await base.ui_interact(user, ui, __extra);
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "ModularShieldGen");
            await ui.open();
        }
        return dot;
    }

    // tg: code/game/machinery/modular_shield.dm:273
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["max_radius"] = max_radius;
        data["current_radius"] = radius;
        data["max_strength"] = max_strength;
        data["max_regeneration"] = max_regeneration;
        data["current_regeneration"] = current_regeneration;
        data["current_strength"] = stored_strength;
        data["active"] = active;
        data["recovering"] = recovering;
        data["exterior_only"] = exterior_only;
        data["initiating_field"] = initiating;
        return data;
    }

    // tg: code/game/machinery/modular_shield.dm:288
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        object sw__1 = action;
        if (Dm.eq(sw__1, "set_radius"))
        {
            if (Dm.truthy(active))
                return dot;
            float change_radius = clamp(Dm.num(text2num(@params["new_radius"])), 1, max_radius);
            if (change_radius >= 10)
            {
                radius = round(change_radius); //if its over 10 we don't allow decimals
                return dot;
            }
            radius = change_radius;
        }
        else if (Dm.eq(sw__1, "toggle_shields"))
            await toggle_shields();
        else if (Dm.eq(sw__1, "toggle_exterior"))
        {
            exterior_only = Dm.truthy(exterior_only) ? FALSE : TRUE;
            calculate_radius();
        }
        return dot;
    }

    ///calculations for the stats supplied by the network of machines that boost us
    // tg: code/game/machinery/modular_shield.dm:310
    public virtual void calculate_boost()
    {
        regen_boost = Dm.initial(this, (obj_machinery_modular_shield_generator g) => g.regen_boost);
        foreach (obj_machinery_modular_shield_module_charger new_charger in Dm.each<obj_machinery_modular_shield_module_charger>(connected_modules))
            regen_boost += new_charger.charge_boost;

        calculate_regeneration();

        max_strength_boost = Dm.initial(this, (obj_machinery_modular_shield_generator g) => g.max_strength_boost);
        foreach (obj_machinery_modular_shield_module_well new_well in Dm.each<obj_machinery_modular_shield_module_well>(connected_modules))
            max_strength_boost += new_well.strength_boost;

        calculate_max_strength();

        radius_boost = Dm.initial(this, (obj_machinery_modular_shield_generator g) => g.radius_boost);
        foreach (obj_machinery_modular_shield_module_relay new_relay in Dm.each<obj_machinery_modular_shield_module_relay>(connected_modules))
            radius_boost += new_relay.range_boost;

        calculate_radius();
    }

    ///Calculates the max radius the shield generator can support, modifiers go here
    // tg: code/game/machinery/modular_shield.dm:331
    public virtual void calculate_radius()
    {
        max_radius = innate_radius + radius_boost;
        if (!Dm.truthy(exterior_only) && Dm.truthy(internal_penalty))
            max_radius = max(3, max_radius * 0.5f);

        if (Dm.num(radius) > max_radius) //the generator can no longer function at this capacity
        {
            deactivate_shields();
            radius = round(max_radius, 1);
            return;
        }
        calculate_regeneration();
    }

    ///Calculates the max strength or health of the forcefield, modifiers go here
    // tg: code/game/machinery/modular_shield.dm:344
    public virtual void calculate_max_strength()
    {
        max_strength = innate_strength + max_strength_boost;
        begin_processing();
    }

    ///Calculates the regeneration based on the status of the generator and boosts from network, modifiers go here
    // tg: code/game/machinery/modular_shield.dm:350
    public virtual void calculate_regeneration()
    {
        max_regeneration = innate_regen + regen_boost;

        if (!Dm.truthy(active))
        {
            if (Dm.truthy(recovering))
            {
                current_regeneration = max_regeneration * 0.25f;
                return;
            }
            current_regeneration = max_regeneration;
            return;
        }

        //we lose more than half the regeneration rate when generating a shield that is near the max
        //radius that we can handle but if we generate a shield with a very small fraction
        //of the max radius we can support we get a very small bonus multiplier
        current_regeneration = max_regeneration / (0.5f + (Dm.num(radius) * 2) / max_radius);

        if (!Dm.truthy(exterior_only) && Dm.truthy(internal_penalty))
            current_regeneration = Dm.num(current_regeneration) * 0.5f;
    }

    ///Reduces the strength of the shield based on the given integer
    // tg: code/game/machinery/modular_shield.dm:370
    public virtual void shield_drain(object damage_amount = null)
    {
        stored_strength -= Dm.num(damage_amount);
        begin_processing();
        if (stored_strength < 5)
        {
            recovering = TRUE;
            deactivate_shields();
            stored_strength = 0;
            Waiting.NoSleep(update_icon_state());
        }
    }

    // tg: code/game/machinery/modular_shield.dm:379
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        stored_strength = min(stored_strength + (Dm.num(current_regeneration) * Dm.num(seconds_per_tick)), max_strength);
        if (stored_strength == max_strength)
        {
            if (Dm.truthy(recovering))
            {
                recovering = FALSE;
                calculate_regeneration();
                Waiting.NoSleep(update_icon_state());
            }
            end_processing(); //we don't care about continuing to update the alpha, we want to show history of damage to show its unstable
        }
        if (Dm.truthy(active))
        {
            float random_num = rand(1, length(deployed_shields));
            obj_structure_emergency_shield_modular random_shield = deployed_shields[random_num] as obj_structure_emergency_shield_modular;
            random_shield.alpha = max(255 * (stored_strength / max_strength), 40);
        }
        return null;
    }
}

// tg: code/game/machinery/modular_shield.dm:393
public partial class obj_machinery_modular_shield_generator_gate : obj_machinery_modular_shield_generator
{
    // tg: code/game/machinery/modular_shield.dm:404
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        return null;
    }

    // tg: code/game/machinery/modular_shield.dm:407
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        object dot = await base.wrench_act(user, tool);

        if (!Dm.truthy(await default_change_direction_wrench(user, tool)))
            return FALSE;
        return TRUE;
    }

    ///we shield a tile and step forward until we either run out of max radius or hit a closed turf, every turf we shield is a penalty towards regen
    // tg: code/game/machinery/modular_shield.dm:415
    public override async DmTask activate_shields()
    {
        if (Dm.truthy(active) || Dm.truthy(Dm.bitand(machine_stat, NOPOWER))) //bug or did admin call proc on already active shield gen?
            return;
        if (max_radius < 0) //what the fuck are admins doing
            calculate_radius();
        active = TRUE;
        initiating = TRUE;
        object color_shield = Dm.truthy(cached_color_filter) ? cached_color_filter : color;
        turf target_tile = loc as turf;
        radius = 0;
        for (float i = 1, i__end = round(max_radius, 1); i <= i__end; i++)
        {
            if (!isopenturf(target_tile) || Dm.truthy(Dm.locate<obj_structure_emergency_shield_modular>(target_tile)))
            {
                _addtimer(Dm.@new<datum_callback>(this, nameof(finish_field)), 2 * SECONDS, file: "code/game/machinery/modular_shield.dm", line: 428);
                calculate_regeneration();
                if (Dm.num(radius) == 0) //we couldnt even generate a single tile somehow
                    deactivate_shields();
                return;
            }
            obj_structure_emergency_shield_modular deploying_shield = Dm.@new<obj_structure_emergency_shield_modular>(target_tile, this);
            LAZYADD(ref deployed_shields, deploying_shield);
            if (Dm.truthy(color_shield))
                await deploying_shield.add_atom_colour(color_shield, FIXED_COLOUR_PRIORITY);
            radius = Dm.num(radius) + 1;
            target_tile = get_step(target_tile, dir);
        }
        _addtimer(Dm.@new<datum_callback>(this, nameof(finish_field)), 2 * SECONDS, file: "code/game/machinery/modular_shield.dm", line: 439);
        calculate_regeneration();
    }

    //if we are active we most definitely have something protecting us
    // tg: code/game/machinery/modular_shield.dm:442
    public override async DmTask<object> take_damage(object damage_amount = null, string damage_type = null, object damage_flag = null, float? sound_effect = null, object attack_dir = null, float? armour_penetration = null)
    {
        if (Dm.truthy(active))
            return null;
        return await base.take_damage(damage_amount, damage_type, damage_flag, sound_effect, attack_dir, armour_penetration);
    }
}

// tg: code/game/machinery/modular_shield.dm:114
public partial class datum_wires_modular_shield_generator : datum_wires
{
    // tg: code/game/machinery/modular_shield.dm:119
    [DmParams("holder")]
    public override async DmTask New(params object[] __extra)
    {
        atom holder = Dm.arg<atom>(__extra, 0);
        wires = Dm.list(WIRE_HACK);
        await base.New(Dm.forward(__extra, holder));
    }

    // tg: code/game/machinery/modular_shield.dm:123
    public override async DmTask on_pulse(object wire = null, mob_living user = null)
    {
        obj_machinery_modular_shield_generator shield_gen = (obj_machinery_modular_shield_generator)holder;
        object sw__1 = wire;
        if (Dm.eq(sw__1, WIRE_HACK))
            await shield_gen.toggle_shields();

        await base.on_pulse(wire, user);
    }
}

//Start of other machines
///The general code used for machines that want to connect to the network
// tg: code/game/machinery/modular_shield.dm:449
public partial class obj_machinery_modular_shield_module : obj_machinery_modular_shield
{
    // tg: code/game/machinery/modular_shield.dm:471
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        connected_turf = get_step(this, dir);
        try_connect();
        return dot;
    }

    // tg: code/game/machinery/modular_shield.dm:477
    public override object Destroy(float? force = null)
    {
        if (Dm.truthy(shield_generator))
        {
            LAZYREMOVE(ref shield_generator.connected_modules, this);
            shield_generator.calculate_boost();
        }
        if (Dm.truthy(connected_node))
        {
            LAZYREMOVE(ref connected_node.connected_through_us, this);
            Waiting.NoSleep(connected_node.update_icon_state());
        }
        return base.Destroy(force);
    }

    // tg: code/game/machinery/modular_shield.dm:487
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);

        if (shield_generator == null && connected_node == null)
        {
            Dm.append(ref dot, I18n.T("It can be loosened and rotated with a screwdriver and wrench. It can be connected to a node or generator with a multitool."));
            return dot;
        }
        Dm.append(ref dot, I18n.T("It can be loosed and rotated with a screwdriver and wrench, rotating it will sever its connection."));
        return dot;
    }

    // tg: code/game/machinery/modular_shield.dm:495
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        object dot = await base.screwdriver_act(user, tool);

        panel_open = Dm.truthy(panel_open) ? FALSE : TRUE;
        tool.play_tool_sound(this, 50);
        await update_icon_state();
        if (Dm.truthy(panel_open))
        {
            balloon_alert(user, I18n.T("hatch opened"));
            return TRUE;
        }
        balloon_alert(user, I18n.T("hatch closed"));
        return TRUE;
    }

    // tg: code/game/machinery/modular_shield.dm:507
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        object dot = await base.multitool_act(user, tool);

        //rather than automatically checking for connections its probably alot less
        //expensive to just make the players manually multi tool sync each part
        try_connect(user);
        return TRUE;
    }

    // tg: code/game/machinery/modular_shield.dm:515
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        object dot = await base.wrench_act(user, tool);

        if (!Dm.truthy(await default_change_direction_wrench(user, tool)))
            return FALSE;

        if (Dm.truthy(shield_generator))
        {
            LAZYREMOVE(ref shield_generator.connected_modules, this);
            shield_generator.calculate_boost();
            shield_generator = null;
            await update_icon_state();
        }

        if (Dm.truthy(connected_node))
        {
            LAZYREMOVE(ref connected_node.connected_through_us, this);
            await connected_node.update_icon_state();
            connected_node = null;
        }

        connected_turf = get_step(this, dir);
        try_connect();
        return TRUE;
    }

    // tg: code/game/machinery/modular_shield.dm:536
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        return await default_deconstruction_crowbar(user, tool);
    }

    // tg: code/game/machinery/modular_shield.dm:539
    public override async DmTask setDir(object newdir = null)
    {
        await base.setDir(newdir);
        connected_turf = get_step(this, dir);
    }

    ///checks for a valid machine in front of us and connects to it
    // tg: code/game/machinery/modular_shield.dm:544
    public virtual void try_connect(object user = null)
    {
        mob viewer = user as mob;
        if (Dm.truthy(shield_generator) || Dm.truthy(connected_node))
        {
            balloon_alert(viewer, I18n.T("already connected to something!"));
            Waiting.NoSleep(update_icon_state());
            return;
        }

        shield_generator = Dm.locate<obj_machinery_modular_shield_generator>(connected_turf);

        if (Dm.truthy(shield_generator))
        {
            LAZYOR(ref shield_generator.connected_modules, this);
            balloon_alert(viewer, I18n.T("connected to generator"));
            Waiting.NoSleep(update_icon_state());
            shield_generator.calculate_boost();
            return;
        }

        connected_node = Dm.locate<obj_machinery_modular_shield_module_node>(connected_turf);

        if (Dm.truthy(connected_node))
        {
            //checks if the node allows boosters and if we are a booster
            if (!Dm.truthy(connected_node.allow_boosters) && Dm.truthy(is_booster))
            {
                connected_node = null;
                Waiting.NoSleep(update_icon_state());
                balloon_alert(viewer, I18n.T("cant connect"));
                return;
            }

            LAZYOR(ref connected_node.connected_through_us, this);
            Waiting.NoSleep(connected_node.update_icon_state());
            shield_generator = connected_node.shield_generator;
            if (Dm.truthy(shield_generator))
            {
                LAZYOR(ref shield_generator.connected_modules, this);
                balloon_alert(viewer, I18n.T("connected to generator"));
                Waiting.NoSleep(update_icon_state());
                shield_generator.calculate_boost();
                return;
            }
            Waiting.NoSleep(update_icon_state());
            balloon_alert(viewer, I18n.T("connected to node"));
            return;
        }
        Waiting.NoSleep(update_icon_state());
        balloon_alert(viewer, I18n.T("no connection!"));
    }
}

// tg: code/game/machinery/modular_shield.dm:588
public partial class obj_machinery_modular_shield_module_node : obj_machinery_modular_shield_module
{
    // tg: code/game/machinery/modular_shield.dm:597
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        if (shield_generator == null || Dm.truthy(Dm.bitand(machine_stat, NOPOWER)))
        {
            icon_state = Dm.text("node_off_[]", Dm.truthy(panel_open) ? "open" : "closed");
            return dot;
        }
        icon_state = Dm.text("node_on_[]", Dm.truthy(panel_open) ? "open" : "closed");
        return dot;
    }

    // tg: code/game/machinery/modular_shield.dm:604
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(await default_change_direction_wrench(user, tool)))
            return FALSE;

        disconnect_connected_through_us();

        if (Dm.truthy(shield_generator))
        {
            LAZYREMOVE(ref shield_generator.connected_modules, this);
            shield_generator.calculate_boost();
            shield_generator = null;
            await update_icon_state();
        }

        if (Dm.truthy(connected_node))
        {
            LAZYREMOVE(ref connected_node.connected_through_us, this);
            await connected_node.update_icon_state();
            connected_node = null;
        }

        connected_turf = get_step(this, dir);
        try_connect();
        return TRUE;
    }

    //after trying to connect to a machine infront of us, we will try to link anything connected to us to a generator
    // tg: code/game/machinery/modular_shield.dm:627
    public override void try_connect(object user = null)
    {
        base.try_connect(user);

        if (shield_generator == null)
            return;
        connect_connected_through_us();
        shield_generator.calculate_boost();
    }

    // tg: code/game/machinery/modular_shield.dm:635
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);

        disconnect_connected_through_us();
        foreach (obj_machinery_modular_shield_module connected in Dm.each<obj_machinery_modular_shield_module>(connected_through_us))
            connected.connected_node = null;
        if (Dm.truthy(shield_generator))
            shield_generator.calculate_boost();
        return dot;
    }

    ///If we are connected to a shield generator this proc will connect anything connected to us to that generator
    // tg: code/game/machinery/modular_shield.dm:645
    public virtual void connect_connected_through_us()
    {
        if (Dm.truthy(shield_generator))
        {
            foreach (obj_machinery_modular_shield_module connected in Dm.each<obj_machinery_modular_shield_module>(connected_through_us))
            {
                LAZYOR(ref shield_generator.connected_modules, connected);
                connected.shield_generator = shield_generator;
                if (connected is obj_machinery_modular_shield_module_node)
                {
                    obj_machinery_modular_shield_module_node connected_node = (obj_machinery_modular_shield_module_node)connected;
                    connected_node.connect_connected_through_us();
                }
                Waiting.NoSleep(connected.update_icon_state());
            }
        }
    }

    ///This proc disconnects modules connected through us from the shield generator in the event that we lose connection
    // tg: code/game/machinery/modular_shield.dm:658
    public virtual void disconnect_connected_through_us()
    {
        foreach (obj_machinery_modular_shield_module connected in Dm.each<obj_machinery_modular_shield_module>(connected_through_us))
        {
            LAZYREMOVE(ref shield_generator.connected_modules, connected);
            if (connected is obj_machinery_modular_shield_module_node)
            {
                obj_machinery_modular_shield_module_node connected_node = (obj_machinery_modular_shield_module_node)connected;
                connected_node.disconnect_connected_through_us();
            }
            connected.shield_generator = null;
            Waiting.NoSleep(connected.update_icon_state());
        }
    }
}

// tg: code/game/machinery/modular_shield.dm:669
public partial class obj_machinery_modular_shield_module_node_cable : obj_machinery_modular_shield_module_node
{
    // tg: code/game/machinery/modular_shield.dm:678
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        turf right_turf = get_step(this, turn(dir, 270));
        obj_machinery_modular_shield_module_node connected_right = Dm.locate<obj_machinery_modular_shield_module_node>(right_turf);
        if (!Dm.@in(connected_right, connected_through_us))
            connected_right = null;
        turf back_turf = get_step(this, turn(dir, 180));
        obj_machinery_modular_shield_module_node connected_back = Dm.locate<obj_machinery_modular_shield_module_node>(back_turf);
        if (!Dm.@in(connected_back, connected_through_us))
            connected_back = null;
        turf left_turf = get_step(this, turn(dir, 90));
        obj_machinery_modular_shield_module_node connected_left = Dm.locate<obj_machinery_modular_shield_module_node>(left_turf);
        if (!Dm.@in(connected_left, connected_through_us))
            connected_left = null;
        icon_state = Dm.text("cable_node_[]_[]_[]_[]", Dm.truthy(panel_open) ? "open" : "closed", connected_right != null ? "r" : "nr", connected_back != null ? "b" : "nb", connected_left != null ? "l" : "nl");
        return dot;
    }
}

// tg: code/game/machinery/modular_shield.dm:695
public partial class obj_machinery_modular_shield_module_charger : obj_machinery_modular_shield_module
{
    // tg: code/game/machinery/modular_shield.dm:706
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        if (shield_generator == null || Dm.truthy(Dm.bitand(machine_stat, NOPOWER)))
        {
            icon_state = Dm.text("charger_off_[]", Dm.truthy(panel_open) ? "open" : "closed");
            return dot;
        }
        icon_state = Dm.text("charger_on_[]", Dm.truthy(panel_open) ? "open" : "closed");
        return dot;
    }

    // tg: code/game/machinery/modular_shield.dm:713
    public override async DmTask<object> RefreshParts()
    {
        object dot = await base.RefreshParts();
        charge_boost = Dm.initial(this, (obj_machinery_modular_shield_module_charger c) => c.charge_boost);
        foreach (datum_stock_part_servo new_servo in Dm.each<datum_stock_part_servo>(component_parts))
            charge_boost += Dm.num(new_servo.tier) * 2;

        if (Dm.truthy(shield_generator))
            shield_generator.calculate_boost();
        return dot;
    }
}

// tg: code/game/machinery/modular_shield.dm:723
public partial class obj_machinery_modular_shield_module_relay : obj_machinery_modular_shield_module
{
    // tg: code/game/machinery/modular_shield.dm:734
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        if (shield_generator == null || Dm.truthy(Dm.bitand(machine_stat, NOPOWER)))
        {
            icon_state = Dm.text("relay_off_[]", Dm.truthy(panel_open) ? "open" : "closed");
            return dot;
        }
        icon_state = Dm.text("relay_on_[]", Dm.truthy(panel_open) ? "open" : "closed");
        return dot;
    }

    // tg: code/game/machinery/modular_shield.dm:741
    public override async DmTask<object> RefreshParts()
    {
        object dot = await base.RefreshParts();
        range_boost = Dm.initial(this, (obj_machinery_modular_shield_module_relay r) => r.range_boost);
        foreach (datum_stock_part_micro_laser new_laser in Dm.each<datum_stock_part_micro_laser>(component_parts))
            range_boost += Dm.num(new_laser.tier) * 0.25f;

        if (Dm.truthy(shield_generator))
            shield_generator.calculate_boost();
        return dot;
    }
}

// tg: code/game/machinery/modular_shield.dm:751
public partial class obj_machinery_modular_shield_module_well : obj_machinery_modular_shield_module
{
    // tg: code/game/machinery/modular_shield.dm:762
    public override async DmTask<object> RefreshParts()
    {
        object dot = await base.RefreshParts();
        strength_boost = Dm.initial(this, (obj_machinery_modular_shield_module_well w) => w.strength_boost);
        foreach (datum_stock_part_capacitor new_capacitor in Dm.each<datum_stock_part_capacitor>(component_parts))
            strength_boost += Dm.num(new_capacitor.tier) * 20;

        if (Dm.truthy(shield_generator))
            shield_generator.calculate_boost();
        return dot;
    }

    // tg: code/game/machinery/modular_shield.dm:771
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        if (shield_generator == null || Dm.truthy(Dm.bitand(machine_stat, NOPOWER)))
        {
            icon_state = Dm.text("well_off_[]", Dm.truthy(panel_open) ? "open" : "closed");
            return dot;
        }
        icon_state = Dm.text("well_on_[]", Dm.truthy(panel_open) ? "open" : "closed");
        return dot;
    }
}

//The shield itself
// tg: code/game/machinery/modular_shield.dm:780
public partial class obj_structure_emergency_shield_modular : obj_structure_emergency_shield
{
    // tg: code/game/machinery/modular_shield.dm:795
    [DmParams("mapload", "connected_to_generator")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        obj_machinery_modular_shield_generator connected_to_generator = Dm.arg<obj_machinery_modular_shield_generator>(__extra, 0);
        Waiting.NoSleep(AddElement(typeof(datum_element_blocks_explosives)));
        object dot = base.Initialize(mapload, Dm.forward(__extra, connected_to_generator));
        if (!Dm.truthy(connected_to_generator))
            return INITIALIZE_HINT_QDEL;
        shield_generator = connected_to_generator;
        Waiting.NoSleep(AddElement(typeof(datum_element_atmos_sensitive), mapload));
        return dot;
    }

    // tg: code/game/machinery/modular_shield.dm:803
    public override object should_atmos_process(datum_gas_mixture air = null, object exposed_temperature = null)
    {
        return Dm.num(exposed_temperature) > (T0C + 200) ? TRUE : FALSE;
    }

    //Damage from atmos
    // tg: code/game/machinery/modular_shield.dm:807
    public override async DmTask<object> atmos_expose(datum_gas_mixture air = null, object exposed_temperature = null)
    {
        if (shield_generator == null)
        {
            qdel(this);
            return null;
        }

        shield_generator.shield_drain(round(Dm.num(air.return_volume()) / 400)); //400 integer determines how much damage the shield takes from hot atmos (higher value = less damage)
        return null;
    }

    //Damage from direct attacks
    // tg: code/game/machinery/modular_shield.dm:816
    public override async DmTask<object> take_damage(object damage_amount = null, string damage_type = null, object damage_flag = null, float? sound_effect = null, object attack_dir = null, float? armour_penetration = null)
    {
        damage_type ??= BRUTE;
        damage_flag ??= 0;
        sound_effect ??= 1;
        object dot = await base.take_damage(damage_amount, damage_type, damage_flag, sound_effect, attack_dir, armour_penetration);
        if (Dm.eq(damage_type, BRUTE) || Dm.eq(damage_type, BURN))
        {
            if (shield_generator == null)
            {
                qdel(this);
                return dot;
            }

            shield_generator.shield_drain(damage_amount); //can add or subtract a flat value to buff or nerf crowd damage
        }
        return dot;
    }

    //Damage from emp
    // tg: code/game/machinery/modular_shield.dm:826
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (shield_generator == null)
        {
            qdel(this);
            return dot;
        }

        shield_generator.shield_drain(Dm.div(15, severity)); //Light is 2 heavy is 1, note emp is usually a large aoe, tweak the number if not enough damage
        return dot;
    }

    //Damage from explosions, remember we dont use armor
    // tg: code/game/machinery/modular_shield.dm:835
    public override object ex_act(object severity = null, object target = null, params object[] __extra)
    {
        if (shield_generator == null)
        {
            qdel(this);
            return null;
        }

        object sw__1 = severity;
        if (Dm.eq(sw__1, EXPLODE_LIGHT))
        {
            shield_generator.shield_drain(20);
            return null;
        }
        else if (Dm.eq(sw__1, EXPLODE_HEAVY))
        {
            shield_generator.shield_drain(50);
            return null;
        }
        else if (Dm.eq(sw__1, EXPLODE_DEVASTATE))
            shield_generator.shield_drain(100);
        return null;
    }

    // tg: code/game/machinery/modular_shield.dm:853
    public override object hulk_damage()
    {
        if (shield_generator == null)
        {
            qdel(this);
            return 0;
        }
        shield_generator.shield_drain(30 + Dm.num(shield_generator.current_regeneration)); //shield will eventually go down no matter the regen, flat number is how fast the hulk eats at reserves
        return 0;
    }
}
