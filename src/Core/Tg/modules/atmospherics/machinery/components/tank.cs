// Translated from tgstation (snapshot 1d99bd7f): code/modules/atmospherics/machinery/components/tank.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate N1b, D316: the syndicate base's pressure tank): the tank with its merged gas storage, ports, window, cracks,
// leak and burst, its repair and cutting, the gas variants, and the tank frame that builds one. The var blocks are generated (D111); the
// type's static lists are TgState fields (rules 10.6).

// tg: code/modules/atmospherics/machinery/components/tank.dm:3
public partial class obj_machinery_atmospherics_components_tank : obj_machinery_atmospherics_components
{
    // tg: code/modules/atmospherics/machinery/components/tank.dm:1  #define TANK_PLATING_SHEETS 12
    internal const int TANK_PLATING_SHEETS = 12;

    // tg: code/modules/atmospherics/machinery/components/tank.dm:49  var/static/list/breaking_sounds
    private static DmList breaking_sounds => TgState.Current.obj_machinery_atmospherics_components_tank__breaking_sounds ??= Dm.list(
        Dm.resource("sound/effects/structure_stress/pop1.ogg"),
        Dm.resource("sound/effects/structure_stress/pop2.ogg"),
        Dm.resource("sound/effects/structure_stress/pop3.ogg"));
    // tg: code/modules/atmospherics/machinery/components/tank.dm:56  var/static/list/knob_overlays
    private static ref DmList knob_overlays => ref TgState.Current.obj_machinery_atmospherics_components_tank__knob_overlays;
    // tg: code/modules/atmospherics/machinery/components/tank.dm:61  var/static/list/crack_states
    private static ref DmList crack_states => ref TgState.Current.obj_machinery_atmospherics_components_tank__crack_states;
    // tg: code/modules/atmospherics/machinery/components/tank.dm:66  var/static/list/merger_typecache
    private static ref DmList merger_typecache => ref TgState.Current.obj_machinery_atmospherics_components_tank__merger_typecache;

    // tg: code/modules/atmospherics/machinery/components/tank.dm:74
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        if (knob_overlays == null)
        {
            knob_overlays = Dm.list();
            foreach (object dir in Dm.each(GLOB.cardinals))
                knob_overlays[Dm.text("[]", dir)] = image(Dm.resource("icons/obj/pipes_n_cables/stationary_canisters_misc.dmi"), icon_state: "knob", dir: Dm.num(dir), layer: FLOAT_LAYER);
        }

        if (crack_states == null)
        {
            crack_states = Dm.list();
            for (int i = 1; i <= crack_states_count; i++)
                Dm.append(ref crack_states, Dm.text("crack[]", i));
        }

        merger_typecache ??= typecacheof(typeof(obj_machinery_atmospherics_components_tank)) as DmList;

        greyscaled_icon = SSgreyscale.GetColoredIconByType(overlay_greyscale_config, greyscale_colors) as icon;
        gas_holder = Dm.@new<obj_effect_abstract_tank_gas_holder>(this);
        window_glass = Dm.@new<obj_effect_abstract_tank_glass>(this, this);
        vis_contents.Add(window_glass);
        vis_contents.Add(gas_holder);

        Waiting.NoSleep(AddComponent(typeof(datum_component_gas_leaker), Dm.named("leak_rate", 0.05f)));
        Waiting.NoSleep(AddElement(typeof(datum_element_volatile_gas_storage)));
        Waiting.NoSleep(AddElement(typeof(datum_element_crackable), Dm.resource("icons/obj/pipes_n_cables/stationary_canisters_misc.dmi"), crack_states));

        RegisterSignal(this, COMSIG_MERGER_ADDING, nameof(merger_adding));
        RegisterSignal(this, COMSIG_MERGER_REMOVING, nameof(merger_removing));
        RegisterSignal(this, COMSIG_ATOM_SMOOTHED_ICON, nameof(smoothed));

        air_contents = Dm.@new<datum_gas_mixture>();
        air_contents.temperature = T20C;
        air_contents.volume = Dm.num(volume);

        if (gas_type != null && starting_pressure_percent > 0)
            fill_to_pressure(gas_type, starting_pressure_percent);

        QUEUE_SMOOTH(this);
        QUEUE_SMOOTH_NEIGHBORS(this);

        // Mapped in tanks should automatically connect to adjacent pipenets in the direction set in dir
        if (Dm.truthy(mapload))
        {
            set_portdir_relative(dir, TRUE);
            set_init_directions();
        }

        return INITIALIZE_HINT_LATELOAD;
    }

    // We late initialize here so all stationary tanks have time to set up their
    // initial gas mixes and signal registrations.
    // tg: code/modules/atmospherics/machinery/components/tank.dm:120
    public override async DmTask post_machine_initialize()
    {
        await base.post_machine_initialize();
        await GetMergeGroup(merger_id, merger_typecache);
    }

    // tg: code/modules/atmospherics/machinery/components/tank.dm:124
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref window_glass);
        QDEL_NULL(ref gas_holder);
        QUEUE_SMOOTH_NEIGHBORS(this);
        return base.Destroy(force);
    }

    // tg: code/modules/atmospherics/machinery/components/tank.dm:130
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        string wrench_hint = EXAMINE_HINT("wrench");
        if (!Dm.truthy(initialize_directions))
            dot.Add(span_notice(I18n.T("A pipe port can be opened with a [].", wrench_hint)));
        else
            dot.Add(span_notice(I18n.T("The pipe port can be moved or closed with a [].", wrench_hint)));
        dot.Add(span_notice(I18n.T("A holographic sticker on it says that its maximum safe pressure is: [].", siunit_pressure(max_pressure, 0))));
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/tank.dm:142
    public override async DmTask finalize_material_effects(DmList materials = null)
    {
        await base.finalize_material_effects(materials);
        refresh_pressure_limit();
    }

    /// Recalculates pressure based on the current max integrity compared to original
    // tg: code/modules/atmospherics/machinery/components/tank.dm:147
    public virtual void refresh_pressure_limit()
    {
        float max_pressure_multiplier = Dm.num(max_integrity) / Dm.num(Dm.initial(this, (obj_machinery_atmospherics_components_tank t) => t.max_integrity));
        max_pressure = max_pressure_multiplier * Dm.num(Dm.initial(this, (obj_machinery_atmospherics_components_tank t) => t.max_pressure));
    }

    /// Fills the tank to the maximum safe pressure.
    /// Safety margin is a multiplier for the cap for the purpose of this proc so it doesn't have to be filled completely.
    // tg: code/modules/atmospherics/machinery/components/tank.dm:153
    public virtual void fill_to_pressure(object gastype = null, float? safety_margin = null)
    {
        safety_margin ??= 0.5f;
        float pressure_limit = max_pressure * safety_margin.Value;

        float moles_to_add = (pressure_limit * Dm.num(air_contents.volume)) / (R_IDEAL_GAS_EQUATION * Dm.num(air_contents.temperature));

        air_contents.adjust_gas(gastype, moles_to_add);
        air_contents.archive();
    }

    // tg: code/modules/atmospherics/machinery/components/tank.dm:162
    public override object process_atmos(object seconds_per_tick = null)
    {
        if (Dm.truthy(Waiting.NoSleep(air_contents.react(this))))
            update_parents();

        if (Dm.num(air_contents.return_pressure()) > max_pressure)
        {
            Waiting.NoSleep(take_damage(0.1f, BRUTE, sound_effect: FALSE));
            if (prob(40))
                playsound(this, pick(breaking_sounds), 30, vary: TRUE);
        }

        refresh_window();
        return null;
    }

    ///////////////////////////////////////////////////////////////////
    // Port stuff

    /**
     * Enables/Disables a port direction in var/open_ports. \
     * Use this, then call set_init_directions() instead of setting initialize_directions directly \
     * This system exists because tanks not having all initialize_directions set correctly breaks shuttle rotations
     */
    // tg: code/modules/atmospherics/machinery/components/tank.dm:180
    public virtual void set_portdir_relative(float relative_port_dir = 0, object enable = null)
    {
        if (enable == null)
        {
            // ASSERT(!isnull(enable), "Did not receive argument enable")
            throw Dm.CRASH("assertion failed: !isnull(enable): Did not receive argument enable", "code/modules/atmospherics/machinery/components/tank.dm", 181);
        }

        // Rotate the given dir so that it's relative to north
        float port_dir;
        if (dir == NORTH) // We're already facing north, no rotation needed
            port_dir = relative_port_dir;
        else
        {
            float offnorth_angle = Dm.num(dir2angle(dir));
            port_dir = turn(relative_port_dir, offnorth_angle);
        }

        if (Dm.truthy(enable))
            open_ports = Dm.bitor(open_ports, port_dir);
        else
            open_ports = Dm.bitand(open_ports, ~(int)port_dir);
    }

    /**
     * Toggles a port direction in var/open_ports \
     * Use this, then call set_init_directions() instead of setting initialize_directions directly \
     * This system exists because tanks not having all initialize_directions set correctly breaks shuttle rotations
     */
    // tg: code/modules/atmospherics/machinery/components/tank.dm:202
    public virtual void toggle_portdir_relative(float relative_port_dir = 0)
    {
        float toggle = Dm.truthy(Dm.bitand(initialize_directions, relative_port_dir)) ? FALSE : TRUE;
        set_portdir_relative(relative_port_dir, toggle);
    }

    // tg: code/modules/atmospherics/machinery/components/tank.dm:206
    public override void set_init_directions(object init_dir = null)
    {
        if (!Dm.truthy(open_ports))
        {
            initialize_directions = NONE;
            return;
        }

        //We're rotating open_ports relative to dir, and
        //setting initialize_directions to that rotated dir
        float relative_port_dirs = NONE;
        float dir_angle = Dm.num(dir2angle(dir));
        foreach (object cardinal in Dm.each(GLOB.cardinals))
        {
            float current_dir = Dm.num(Dm.bitand(cardinal, open_ports));
            if (!Dm.truthy(current_dir))
                continue;

            float rotated_dir = turn(current_dir, -dir_angle);
            relative_port_dirs = Dm.bitor(relative_port_dirs, rotated_dir);
        }

        initialize_directions = relative_port_dirs;
    }

    // tg: code/modules/atmospherics/machinery/components/tank.dm:225
    public virtual void toggle_side_port(float port_dir = 0)
    {
        toggle_portdir_relative(port_dir);
        set_init_directions();

        for (float i = 1, i__end = length(nodes); i <= i__end; i++)
        {
            var node = nodes[i] as obj_machinery_atmospherics_components;
            if (node == null)
                continue;
            if (Dm.@in(this, node.nodes))
                node.disconnect(this);
            nodes[i] = null;
            if (Dm.truthy(parents[i]))
                nullify_pipenet(parents[i] as datum_pipeline);
        }

        atmos_init();

        foreach (obj_machinery_atmospherics_components node in Dm.each<obj_machinery_atmospherics_components>(nodes))
        {
            if (node == null)
                continue;
            node.atmos_init();
            node.add_member(this);
        }
        SSair.add_to_rebuild_queue(this);

        update_parents();
    }

    ///////////////////////////////////////////////////////////////////
    // Pipenet stuff

    // tg: code/modules/atmospherics/machinery/components/tank.dm:252
    public override object return_analyzable_air()
    {
        return air_contents;
    }

    // tg: code/modules/atmospherics/machinery/components/tank.dm:255
    public override object return_airs_for_reconcilation(datum_pipeline requester = null)
    {
        DmList dot = base.return_airs_for_reconcilation(requester) as DmList;
        if (air_contents == null)
            return dot;
        dot.Add(air_contents);
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/tank.dm:261
    public override async DmTask<object> return_pipenets_for_reconcilation(datum_pipeline requester = null)
    {
        DmList dot = await base.return_pipenets_for_reconcilation(requester) as DmList;
        var merge_group = await GetMergeGroup(merger_id, merger_typecache) as datum_merger;
        foreach (obj_machinery_atmospherics_components_tank tank in Dm.each<obj_machinery_atmospherics_components_tank>(merge_group.members))
            Dm.append(ref dot, tank.parents);
        return dot;
    }

    ///////////////////////////////////////////////////////////////////
    // Merger handling

    // tg: code/modules/atmospherics/machinery/components/tank.dm:270
    [SignalHandler]
    public virtual void merger_adding(obj_machinery_atmospherics_components_tank us = null, datum_merger new_merger = null)
    {
        if (!Dm.eq(new_merger.id, merger_id))
            return;
        RegisterSignal(new_merger, COMSIG_MERGER_REFRESH_COMPLETE, nameof(merger_refresh_complete));
    }

    // tg: code/modules/atmospherics/machinery/components/tank.dm:276
    [SignalHandler]
    public virtual void merger_removing(obj_machinery_atmospherics_components_tank us = null, datum_merger old_merger = null)
    {
        if (!Dm.eq(old_merger.id, merger_id))
            return;
        UnregisterSignal(old_merger, COMSIG_MERGER_REFRESH_COMPLETE);
    }

    /// Handles the combined gas tank for the entire merger group, only the origin tank actualy runs this.
    // tg: code/modules/atmospherics/machinery/components/tank.dm:283
    [SignalHandler]
    public virtual void merger_refresh_complete(datum_merger merger = null, DmList leaving_members = null, DmList joining_members = null)
    {
        if (merger.origin != this)
            return;
        float shares = length(merger.members) + length(leaving_members) - length(joining_members);
        foreach (obj_machinery_atmospherics_components_tank leaver in Dm.each<obj_machinery_atmospherics_components_tank>(leaving_members))
        {
            var gas_share = air_contents.remove_ratio(1 / shares--) as datum_gas_mixture;
            air_contents.volume = Dm.num(air_contents.volume) - Dm.num(leaver.volume);
            leaver.air_contents = gas_share;
            leaver.update_appearance(UPDATE_ICON);
        }

        foreach (obj_machinery_atmospherics_components_tank joiner in Dm.each<obj_machinery_atmospherics_components_tank>(joining_members))
        {
            if (joiner == this)
                continue;
            datum_gas_mixture joiner_share = joiner.air_contents;
            if (joiner_share != null)
                air_contents.merge(joiner_share);
            joiner.air_contents = air_contents;
            air_contents.volume = Dm.num(air_contents.volume) + Dm.num(joiner.volume);
            joiner.update_appearance(UPDATE_ICON);
        }

        foreach (object dir in Dm.each(GLOB.cardinals))
        {
            if (Dm.truthy(Dm.bitand(Dm.bitand(dir, initialize_directions), merger.members[this])))
                toggle_side_port(Dm.num(dir));
        }
    }

    ///////////////////////////////////////////////////////////////////
    // Appearance stuff

    // tg: code/modules/atmospherics/machinery/components/tank.dm:310
    [SignalHandler]
    public virtual void smoothed()
    {
        refresh_window();
    }

    // tg: code/modules/atmospherics/machinery/components/tank.dm:314
    public override object update_appearance(float? updates = null)
    {
        object dot = base.update_appearance(updates);
        refresh_window();
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/tank.dm:318
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        dot.Add(mutable_appearance(greyscaled_icon, "window-bg"));
        if (!Dm.truthy(initialize_directions))
            return dot;
        foreach (object dir in Dm.each(GLOB.cardinals))
        {
            if (Dm.truthy(Dm.bitand(initialize_directions, dir)))
                dot.Add(knob_overlays[Dm.text("[]", dir)]);
        }
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/tank.dm:327
    public override async DmTask update_greyscale()
    {
        greyscaled_icon = SSgreyscale.GetColoredIconByType(overlay_greyscale_config, greyscale_colors) as icon;
        if (window_glass != null)
            window_glass.icon = greyscaled_icon;
        await base.update_greyscale();
    }

    // tg: code/modules/atmospherics/machinery/components/tank.dm:332
    public virtual void refresh_window()
    {
        if (gas_holder == null) // Not created in init yet
            return;

        if (air_contents == null)
        {
            gas_holder.vis_contents.Cut();
            return;
        }

        DmList air_visuals = air_contents.return_visuals(get_turf(this) as turf) as DmList;
        DmList gas_visual_overrides = TgState.Current.obj_machinery_atmospherics_components_tank__refresh_window__gas_visual_overrides ??= Dm.list();
        DmList new_visuals = Dm.list();
        // For those who come after, we need to map existing gas visuals to tank-specific versions which are VIS_INHERIT_ID | VIS_INHERIT_PLANE
        // due to BYOND tomfuckery, as INHERIT_ID is not enough on its own and only that is passed down. I hate this too, sorry.
        foreach (obj_effect_overlay_gas gas in Dm.each<obj_effect_overlay_gas>(air_visuals))
        {
            if (Dm.truthy(gas_visual_overrides[gas]))
            {
                new_visuals.Add(gas_visual_overrides[gas]);
                continue;
            }

            var local_gas = Dm.@new<obj_effect_overlay_gas>(gas.icon_state, gas.alpha, gas.plane_offset);
            local_gas.vis_flags = Dm.bitor(local_gas.vis_flags, Dm.bitor(VIS_INHERIT_ID, VIS_INHERIT_PLANE));
            gas_visual_overrides[gas] = local_gas;
            new_visuals.Add(local_gas);
        }

        gas_holder.vis_contents = new_visuals;
    }

    ///////////////////////////////////////////////////////////////////
    // Tool interactions

    // tg: code/modules/atmospherics/machinery/components/tank.dm:376
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item I = null)
    {
        object dot = TRUE;
        float new_dir = get_dir(this, user);

        if (Dm.@in(new_dir, GLOB.diagonals))
            return dot;

        I.play_tool_sound(this, 10);
        if (!Dm.truthy(await I.use_tool(this, user, 3 * SECONDS)))
            return dot;

        toggle_side_port(new_dir);

        I.play_tool_sound(this, 50);
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/tank.dm:390
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        await base.welder_act(user, tool);
        object dot = TRUE;
        if (Dm.num(atom_integrity) >= Dm.num(max_integrity))
            return dot;
        if (!Dm.truthy(tool.tool_start_check(user, amount: 0, heat_required: HIGH_TEMPERATURE_REQUIRED)))
            return dot;
        to_chat(user, span_notice(I18n.T("You begin to repair the cracks in the gas tank...")));
        float repair_amount = Dm.num(max_integrity) / 10;
        do
        {
            if (!Dm.truthy(await tool.use_tool(this, user, 2.5f * SECONDS, volume: 40)))
                return dot;
        }
        while (Dm.truthy(await repair_damage(repair_amount)));
        to_chat(user, span_notice(I18n.T("The gas tank has been fully repaired and all cracks sealed.")));
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/tank.dm:405
    public override async DmTask<object> welder_act_secondary(mob_living user = null, obj_item tool = null)
    {
        await base.welder_act_secondary(user, tool);
        object dot = TRUE;
        to_chat(user, span_notice(I18n.T("You begin cutting open the gas tank...")));
        var current_location = get_turf(this) as turf;
        var airmix = await current_location.return_air() as datum_gas_mixture;

        float time_taken = 4 * SECONDS;
        bool unsafe_release = false;

        float internal_pressure = Dm.num(air_contents.return_pressure()) - Dm.num(airmix.return_pressure());
        if (internal_pressure > 2 * ONE_ATMOSPHERE)
        {
            time_taken *= 2;
            to_chat(user, span_warning(I18n.T("The tank seems to be pressurized, are you sure this is a good idea?")));
            unsafe_release = true;
        }

        if (!Dm.truthy(await tool.use_tool(this, user, time_taken, volume: 60)))
            return dot;

        if (unsafe_release)
            await unsafe_pressure_release(user, internal_pressure);
        await deconstruct(disassembled: TRUE);
        to_chat(user, span_notice(I18n.T("You finish cutting open the sealed gas tank, revealing the innards.")));
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/tank.dm:433
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        var location = drop_location() as turf;
        object dot = await base.on_deconstruction(disassembled);
        await location.assume_air(air_contents);
        if (!Dm.truthy(disassembled))
            return dot;
        var frame = Dm.@new<obj_structure_tank_frame>(location);
        frame.construction_state = TANK_PLATING_UNSECURED;
        foreach (datum_material material in Dm.each<datum_material>(custom_materials))
        {
            if (frame.material_end_product != null)
            {
                // If something looks fishy, you get nothing
                message_admins(Dm.text("\\The [] had multiple materials set. Unless you were messing around with VV, yell at a coder", this));
                frame.material_end_product = null;
                frame.construction_state = TANK_FRAME;
                break;
            }
            else
                frame.material_end_product = material;
        }
        frame.update_appearance(UPDATE_ICON);
        return dot;
    }
}

// tg: code/modules/atmospherics/machinery/components/tank.dm:456
public partial class obj_machinery_atmospherics_components_tank_air : obj_machinery_atmospherics_components_tank
{
    // tg: code/modules/atmospherics/machinery/components/tank.dm:472
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (starting_pressure_percent > 0)
        {
            fill_to_pressure(typeof(datum_gas_oxygen), safety_margin: O2STANDARD * starting_pressure_percent);
            fill_to_pressure(typeof(datum_gas_nitrogen), safety_margin: N2STANDARD * starting_pressure_percent);
        }
        return dot;
    }
}

// tg: code/modules/atmospherics/machinery/components/tank.dm:340
public partial class obj_effect_abstract_tank_glass : obj_effect_abstract
{
    // tg: code/modules/atmospherics/machinery/components/tank.dm:345
    [DmParams("mapload", "owner")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.arg(__extra, 0) is obj_machinery_atmospherics_components_tank owner)
            icon = owner.greyscaled_icon;
        return dot;
    }
}

// tg: code/modules/atmospherics/machinery/components/tank.dm:350
public partial class obj_effect_abstract_tank_gas_holder : obj_effect_abstract
{
    // tg: code/modules/atmospherics/machinery/components/tank.dm:354
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        add_filter("tank_gas_holder", 1, alpha_mask_filter(icon: Dm.icon(Dm.resource("icons/obj/pipes_n_cables/stationary_canisters_misc.dmi"), "window-bg")) as DmList);
        return dot;
    }
}

///////////////////////////////////////////////////////////////////
// Tank Frame Structure

// tg: code/modules/atmospherics/machinery/components/tank.dm:559
public partial class obj_structure_tank_frame : obj_structure
{
    // tg: code/modules/atmospherics/machinery/components/tank.dm:569
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        string wrenched_hint = EXAMINE_HINT("wrenched");

        if (!Dm.truthy(anchored))
            dot.Add(span_notice(I18n.T("[] has not been [] to the floor yet.", this, wrenched_hint)));
        else
            dot.Add(span_notice(I18n.T("[] is [] to the floor.", this, wrenched_hint)));

        switch (construction_state)
        {
            case TANK_FRAME:
            {
                string screwed_hint = EXAMINE_HINT("screwed");
                string plating_hint = EXAMINE_HINT("metal plating");
                dot.Add(span_notice(I18n.T("[] is [] together and now just needs some [].", this, screwed_hint, plating_hint)));
                break;
            }
            case TANK_PLATING_UNSECURED:
            {
                string crowbar_hint = EXAMINE_HINT("crowbar");
                string welder_hint = EXAMINE_HINT("welder");
                dot.Add(span_notice(I18n.T("The plating has been firmly attached and would need a [] to detach, but still needs to be sealed by a [].", crowbar_hint, welder_hint)));
                break;
            }
        }
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/tank.dm:588
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        if (Dm.truthy(disassembled))
        {
            foreach (datum_material mat in Dm.each<datum_material>(custom_materials))
                await Dm.new_async(mat.sheet_type, drop_location(), Dm.num(custom_materials[mat]) / SHEET_MATERIAL_AMOUNT);
        }
    }

    // tg: code/modules/atmospherics/machinery/components/tank.dm:593
    public override async DmTask<object> update_icon(float? updates = null, params object[] __extra)
    {
        object dot = await base.update_icon(updates, __extra);
        switch (construction_state)
        {
            case TANK_FRAME:
                icon_state = "frame";
                break;
            case TANK_PLATING_UNSECURED:
                icon_state = "plated_frame";
                break;
        }
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/tank.dm:601
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (construction_state != TANK_FRAME || !isstack(tool))
            return await base.item_interaction(user, tool, modifiers, __extra);

        if (!Dm.truthy(await add_plating(user, tool as obj_item_stack)))
            return ITEM_INTERACT_BLOCKING;

        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/atmospherics/machinery/components/tank.dm:610
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        await base.wrench_act(user, tool);
        await default_unfasten_wrench(user, tool, time: 0.5f * SECONDS);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/atmospherics/machinery/components/tank.dm:615
    public override async DmTask<object> screwdriver_act_secondary(mob_living user = null, obj_item tool = null)
    {
        object dot = await base.screwdriver_act_secondary(user, tool);
        if (construction_state != TANK_FRAME)
            return dot;
        dot = TRUE;
        to_chat(user, span_notice(I18n.T("You begin taking apart [].", this)));
        if (!Dm.truthy(await tool.use_tool(this, user, 1 * SECONDS)))
            return dot;
        await deconstruct(TRUE);
        to_chat(user, span_notice(I18n.T("[] has been taken apart.", this)));
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/tank.dm:626
    public virtual async DmTask<object> add_plating(mob_living user = null, obj_item_stack stack = null)
    {
        object dot = FALSE;
        if (stack.material_type == null)
            balloon_alert(user, I18n.T("invalid material!"));
        var stack_mat = await SSmaterials.get_material(stack.material_type) as datum_material;
        if (!Dm.truthy(Dm.bitand(stack_mat.mat_flags, MATERIAL_CLASS_RIGID)))
        {
            to_chat(user, span_notice(I18n.T("This material doesn't seem rigid enough to hold the shape of a tank...")));
            return dot;
        }

        dot = TRUE;
        to_chat(user, span_notice(I18n.T("You begin adding [] to []...", stack, this)));
        if (!Dm.truthy(await stack.use_tool(this, user, 3 * SECONDS)))
            return dot;
        if (!Dm.truthy(await stack.use(obj_machinery_atmospherics_components_tank.TANK_PLATING_SHEETS)))
        {
            string amount_more;
            float share = 100 * Dm.num(stack.amount) / obj_machinery_atmospherics_components_tank.TANK_PLATING_SHEETS;
            if (share == 0) // Wat?
                amount_more = "any at all";
            else if (share >= 1 && share <= 25)
                amount_more = "a lot more";
            else if (share >= 26 && share <= 50)
                amount_more = "about four times as much";
            else if (share >= 51 && share <= 75)
                amount_more = "about twice as much";
            else if (share >= 76 && share <= 100)
                amount_more = "just a bit more";
            else
                amount_more = "an indeterminate amount more";
            to_chat(user, span_notice(I18n.T("You don't have enough [] to add all the plating. Maybe [].", stack, amount_more)));
            return dot;
        }

        material_end_product = stack_mat;
        construction_state = TANK_PLATING_UNSECURED;
        update_appearance(UPDATE_ICON);
        to_chat(user, span_notice(I18n.T("You finish attaching [] to [].", stack, this)));
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/tank.dm:661
    public override async DmTask<object> crowbar_act_secondary(mob_living user = null, obj_item tool = null)
    {
        object dot = await base.crowbar_act_secondary(user, tool);
        if (construction_state != TANK_PLATING_UNSECURED)
            return dot;
        dot = TRUE;
        to_chat(user, span_notice(I18n.T("You start prying off the outer plating...")));
        if (!Dm.truthy(await tool.use_tool(this, user, 2 * SECONDS)))
            return dot;
        construction_state = TANK_FRAME;
        await Dm.new_async(material_end_product.sheet_type, drop_location(), obj_machinery_atmospherics_components_tank.TANK_PLATING_SHEETS);
        material_end_product = null;
        update_appearance(UPDATE_ICON);
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/tank.dm:674
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        object dot = await base.welder_act(user, tool);
        if (construction_state != TANK_PLATING_UNSECURED)
            return dot;
        dot = TRUE;
        if (!Dm.truthy(anchored))
        {
            to_chat(user, span_notice(I18n.T("You need to <b>wrench</b> [] to the floor before finishing.", this)));
            return dot;
        }
        if (!Dm.truthy(tool.tool_start_check(user, amount: 0, heat_required: HIGH_TEMPERATURE_REQUIRED)))
            return dot;
        to_chat(user, span_notice(I18n.T("You begin sealing the outer plating with the welder...")));
        if (!Dm.truthy(await tool.use_tool(this, user, 2 * SECONDS, volume: 60)))
            return dot;

        var build_location = drop_location();
        if (!isturf(build_location))
            return dot;
        var new_tank = Dm.@new<obj_machinery_atmospherics_components_tank>(build_location);
        DmList new_custom_materials = Dm.list(Dm.kv(material_end_product, obj_machinery_atmospherics_components_tank.TANK_PLATING_SHEETS * SHEET_MATERIAL_AMOUNT));
        await new_tank.set_custom_materials(new_custom_materials);
        await new_tank.on_construction(user, new_tank.pipe_color, new_tank.piping_layer);
        to_chat(user, span_notice(I18n.T("[] has been sealed and is ready to accept gases.", new_tank)));
        qdel(this);
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/atmospherics/machinery/components/tank.dm:49  /obj/machinery/atmospherics/components/tank/var/static/list/breaking_sounds
    internal DmList obj_machinery_atmospherics_components_tank__breaking_sounds;
    // tg: code/modules/atmospherics/machinery/components/tank.dm:56  var/static/list/knob_overlays
    internal DmList obj_machinery_atmospherics_components_tank__knob_overlays;
    // tg: code/modules/atmospherics/machinery/components/tank.dm:61  var/static/list/crack_states
    internal DmList obj_machinery_atmospherics_components_tank__crack_states;
    // tg: code/modules/atmospherics/machinery/components/tank.dm:66  var/static/list/merger_typecache
    internal DmList obj_machinery_atmospherics_components_tank__merger_typecache;
    // tg: code/modules/atmospherics/machinery/components/tank.dm:341  var/static/list/gas_visual_overrides
    internal DmList obj_machinery_atmospherics_components_tank__refresh_window__gas_visual_overrides;
}
