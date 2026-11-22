// Translated from tgstation (snapshot 1d99bd7f): code/modules/power/turbine/turbine.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Engineering package E11: the gas turbine (MetaStation's incinerator: the inlet compressor, the core rotor and the turbine outlet).
// The three parts with their installed tier parts and the gas mixes they hold, the panel, the rotation, the part swap and the
// deconstruction; the rotor's multitool link of the three parts (with tg's alignment checks) and its cable layer; its process: the
// compressor draws the input turf's gas, the rotor and the outlet expand it, the outlet expels it, the work spins the rotor up to the
// parts' rated RPM and adds energy to the cable; the heat above the parts' rated temperature damages the turbine, the engineering
// radio alert and the alarm sound, the explosion at zero integrity. Not here: the parts' screentip context (live-partial
// rows, D264) and the rotor's emissive glow (DV12). The var blocks are generated (D111).

// tg: code/modules/power/turbine/turbine.dm:7
public partial class obj_machinery_power_turbine : obj_machinery_power
{
    // tg: code/modules/power/turbine/turbine.dm:2  #define MINIMUM_TURBINE_PRESSURE 0.01 (file-local)
    internal const float MINIMUM_TURBINE_PRESSURE = 0.01f;

    ///Returns the minimum pressure if it falls below the value
    // tg: code/modules/power/turbine/turbine.dm:4  #define PRESSURE_MAX(value)(max((value), MINIMUM_TURBINE_PRESSURE)) (file-local)
    internal static float PRESSURE_MAX(object value) => max(Dm.num(value), MINIMUM_TURBINE_PRESSURE);

    // tg: code/modules/power/turbine/turbine.dm:23
    [DmParams("mapload", "gas_theoretical_volume")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object gas_theoretical_volume = Dm.arg(__extra, 0);
        object dot = base.Initialize(mapload, __extra);

        machine_gasmix = Dm.@new<datum_gas_mixture>();
        machine_gasmix.volume = Dm.num_or_null(gas_theoretical_volume);

        if (Dm.truthy(mapload))
        {
            installed_part = Dm.@new(part_path, this) as obj_item_turbine_parts;
            efficiency = Dm.num(installed_part.get_tier_value(TURBINE_MAX_EFFICIENCY));
        }

        air_update_turf(TRUE);

        update_appearance(UPDATE_OVERLAYS);

        register_context();
        return dot;
    }

    // tg: code/modules/power/turbine/turbine.dm:39
    public override async DmTask post_machine_initialize()
    {
        await base.post_machine_initialize();

        await activate_parts();
    }

    // tg: code/modules/power/turbine/turbine.dm:44
    public override object Destroy(float? force = null)
    {
        force ??= FALSE;
        air_update_turf(TRUE);

        if (installed_part != null)
            QDEL_NULL(ref installed_part);

        if (machine_gasmix != null)
            QDEL_NULL(ref machine_gasmix);

        Waiting.NoSleep(deactivate_parts());
        return base.Destroy(force);
    }

    // tg: code/modules/power/turbine/turbine.dm:56
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        if (installed_part != null)
            await installed_part.forceMove(loc);
        return await base.on_deconstruction(disassembled);
    }

    // tg: code/modules/power/turbine/turbine.dm:90
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (installed_part != null)
        {
            Dm.append(ref dot, span_notice(I18n.T("Currently at tier [].", installed_part.current_tier)));
            if (installed_part.current_tier + 1 < TURBINE_PART_TIER_FOUR)
                Dm.append(ref dot, span_notice(I18n.T("Can be upgraded by using a tier [] part.", installed_part.current_tier + 1)));
            Dm.append(ref dot, span_notice(I18n.T("\\The [] can be [] out.", installed_part, EXAMINE_HINT(I18n.T("pried")))));
        }
        else
            Dm.append(ref dot, span_warning(I18n.T("Is missing a [].", Dm.initial(part_path, (obj_item p) => p.name))));
        Dm.append(ref dot, span_notice(I18n.T("Its maintenance panel can be [] [].", EXAMINE_HINT(I18n.T("screwed")), Dm.truthy(panel_open) ? I18n.T("closed") : I18n.T("open"))));
        if (Dm.truthy(panel_open))
        {
            Dm.append(ref dot, span_notice(I18n.T("It can rotated with a []", EXAMINE_HINT(I18n.T("wrench")))));
            Dm.append(ref dot, span_notice(I18n.T("The full machine can be [] apart", EXAMINE_HINT(I18n.T("pried")))));
        }
        return dot;
    }

    ///Is this machine currently running
    // tg: code/modules/power/turbine/turbine.dm:105
    public virtual object is_active()
    {
        // SHOULD_BE_PURE(TRUE), PROTECTED_PROC(TRUE)
        return FALSE;
    }

    /**
     * Adds overlays to this turbines appearance
     * Arguments
     *
     * * list/overlays - the list of overlays to add to
     */
    // tg: code/modules/power/turbine/turbine.dm:117
    public virtual void set_overlays(DmList overlays = null)
    {
        // PROTECTED_PROC(TRUE)
        Dm.append(ref overlays, Dm.text("[]_[]", base_icon_state, Dm.truthy(is_active()) ? "on" : "off"));
    }

    // tg: code/modules/power/turbine/turbine.dm:122
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);

        if (Dm.truthy(panel_open))
            Dm.append(ref dot, Dm.text("[]_open", base_icon_state));

        set_overlays(dot as DmList);
        return dot;
    }

    /**
     * Handles all the calculations needed for the gases, work done, temperature increase/decrease
     *
     * Arguments
     * * datum/gas_mixture/input_mix - the gas from the environment or from another part of the turbine
     * * datum/gas_mixture/output_mix - the gas that got pumped into this part from the input mix.
     * ideally should be same as input mix but varying texmperatur & pressures can cause varying results
     * * work_amount_to_remove - the amount of work to subtract from the actual work done to pump in the input mixture.
     * For e.g. if gas was transfered from the inlet compressor to the rotor we want to subtract the work done
     * by the inlet from the rotor to get the true work done
     * * intake_size - the percentage of gas to be fed into an turbine part, controlled by turbine computer for inlet compressor only
     */
    // tg: code/modules/power/turbine/turbine.dm:142
    public virtual object transfer_gases(datum_gas_mixture input_mix = null, datum_gas_mixture output_mix = null, object work_amount_to_remove = null, float? intake_size = null)
    {
        intake_size ??= 1;
        // PROTECTED_PROC(TRUE)

        //pump gases. if no gases were transferred then no work was done
        float output_pressure = PRESSURE_MAX(output_mix.return_pressure());
        datum_gas_mixture transferred_gases = input_mix.pump_gas_to(output_mix, Dm.num(input_mix.return_pressure()) * intake_size.Value) as datum_gas_mixture;
        if (transferred_gases == null)
            return 0;

        //compute work done
        float work_done = QUANTIZE(transferred_gases.total_moles()) * R_IDEAL_GAS_EQUATION * transferred_gases.temperature * log((Dm.num(transferred_gases.volume) * PRESSURE_MAX(transferred_gases.return_pressure())) / (Dm.num(output_mix.volume) * output_pressure)) * TURBINE_WORK_CONVERSION_MULTIPLIER;
        if (Dm.truthy(work_amount_to_remove))
            work_done = work_done - Dm.num(work_amount_to_remove);

        //compute temperature & work from temperature if that is a lower value
        float output_mix_heat_capacity = Dm.num(output_mix.heat_capacity());
        if (!Dm.truthy(output_mix_heat_capacity))
            return 0;
        work_done = min(work_done, (output_mix_heat_capacity * output_mix.temperature - output_mix_heat_capacity * TCMB) / TURBINE_HEAT_CONVERSION_MULTIPLIER);
        output_mix.temperature = max((output_mix.temperature * output_mix_heat_capacity + work_done * TURBINE_HEAT_CONVERSION_MULTIPLIER) / output_mix_heat_capacity, TCMB);
        return work_done;
    }

    // tg: code/modules/power/turbine/turbine.dm:164
    public override object block_superconductivity()
    {
        return TRUE;
    }

    // tg: code/modules/power/turbine/turbine.dm:167
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        object dot = ITEM_INTERACT_BLOCKING;
        if (Dm.truthy(is_active()))
        {
            balloon_alert(user, I18n.T("turn it off!"));
            return dot;
        }
        if (!Dm.truthy(anchored))
        {
            balloon_alert(user, I18n.T("anchor first!"));
            return dot;
        }

        tool.play_tool_sound(this, 50);
        toggle_panel_open();
        if (Dm.truthy(panel_open))
            await deactivate_parts(user);
        else
            await activate_parts(user);
        update_appearance(UPDATE_OVERLAYS);

        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/power/turbine/turbine.dm:186
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        object dot = ITEM_INTERACT_BLOCKING;
        if (Dm.truthy(await default_change_direction_wrench(user, tool)))
            return ITEM_INTERACT_SUCCESS;
        return dot;
    }

    // tg: code/modules/power/turbine/turbine.dm:191
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        return await default_deconstruction_crowbar(user, tool);
    }

    // tg: code/modules/power/turbine/turbine.dm:194
    public override async DmTask<object> crowbar_act_secondary(mob_living user = null, obj_item tool = null)
    {
        object dot = ITEM_INTERACT_BLOCKING;
        if (!Dm.truthy(panel_open))
        {
            balloon_alert(user, I18n.T("panel is closed!"));
            return dot;
        }
        if (installed_part == null)
        {
            balloon_alert(user, I18n.T("no rotor installed!"));
            return dot;
        }
        if (Dm.truthy(is_active()))
        {
            balloon_alert(user, I18n.T("[] is on!", this));
            return dot;
        }

        await user.put_in_hands(installed_part);
        return ITEM_INTERACT_SUCCESS;
    }

    /**
     * Allow easy enabling of each machine for connection to the main controller
     *
     * Arguments
     * * mob/user - the player who activated the parts
     * * check_only - if TRUE it will not activate the machine but will only check if it can be activated
     */
    // tg: code/modules/power/turbine/turbine.dm:216
    public virtual async DmTask<object> activate_parts(mob user = null, float? check_only = null)
    {
        check_only ??= FALSE;
        // SHOULD_CALL_PARENT(TRUE)

        await set_machine_stat(Dm.bitand(machine_stat, Dm.bitnot(MAINT)));

        return TRUE;
    }

    /**
     * Allow easy disabling of each machine from the main controller
     *
     * Arguments
     * * mob/user - the player who deactivated the parts
     */
    // tg: code/modules/power/turbine/turbine.dm:229
    public virtual async DmTask<object> deactivate_parts(mob user = null)
    {
        // SHOULD_CALL_PARENT(TRUE)

        await set_machine_stat(Dm.bitor(machine_stat, MAINT));

        return TRUE;
    }

    // tg: code/modules/power/turbine/turbine.dm:236
    public override async DmTask<object> Moved(atom old_loc = null, object movement_dir = null, object forced = null, DmList old_locs = null, float? momentum_change = null)
    {
        momentum_change ??= TRUE;
        object dot = await base.Moved(old_loc, movement_dir, forced, old_locs, momentum_change);
        set_panel_open(TRUE);
        update_appearance(UPDATE_OVERLAYS);
        await deactivate_parts();
        old_loc.air_update_turf(TRUE);
        air_update_turf(TRUE);
        return dot;
    }

    // tg: code/modules/power/turbine/turbine.dm:244
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (gone == installed_part)
            installed_part = null;
        return dot;
    }

    // tg: code/modules/power/turbine/turbine.dm:249
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = NONE;
        if (!Dm.istype(tool, part_path))
            return dot;

        //not in a state to accept the part. block so we don't bash the machine and damage it
        if (Dm.truthy(is_active()))
        {
            balloon_alert(user, I18n.T("turn off the machine first!"));
            return ITEM_INTERACT_BLOCKING;
        }
        if (!Dm.truthy(panel_open))
        {
            balloon_alert(user, I18n.T("open the maintenance hatch first!"));
            return ITEM_INTERACT_BLOCKING;
        }

        //install the part
        if (!Dm.truthy(await do_after(user, 2 * SECONDS, this)))
            return ITEM_INTERACT_BLOCKING;
        if (installed_part != null)
        {
            await user.put_in_hands(installed_part);
            balloon_alert(user, I18n.T("replaced part with the one in hand"));
        }
        else
            balloon_alert(user, I18n.T("installed new part"));
        await user.transferItemToLoc(tool, this);
        installed_part = tool as obj_item_turbine_parts;
        efficiency = Dm.num(installed_part.get_tier_value(TURBINE_MAX_EFFICIENCY));

        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/power/turbine/turbine.dm:60
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        if (held_item == null)
            return NONE;

        if (Dm.truthy(panel_open) && istype(held_item, part_path))
        {
            context[SCREENTIP_CONTEXT_CTRL_LMB] = Dm.text("[] part", installed_part != null ? "Replace" : "Install");
            return CONTEXTUAL_SCREENTIP_SET;
        }

        if (Dm.eq(held_item.tool_behaviour, TOOL_SCREWDRIVER))
        {
            context[SCREENTIP_CONTEXT_CTRL_LMB] = Dm.text("[] panel", Dm.truthy(panel_open) ? "Close" : "Open");
            return CONTEXTUAL_SCREENTIP_SET;
        }

        if (Dm.eq(held_item.tool_behaviour, TOOL_WRENCH) && Dm.truthy(panel_open))
        {
            context[SCREENTIP_CONTEXT_CTRL_LMB] = "Rotate";
            return CONTEXTUAL_SCREENTIP_SET;
        }

        if (Dm.eq(held_item.tool_behaviour, TOOL_CROWBAR))
        {
            if (installed_part != null)
                context[SCREENTIP_CONTEXT_CTRL_RMB] = "Remove part";
            if (Dm.truthy(panel_open))
                context[SCREENTIP_CONTEXT_CTRL_LMB] = "Deconstruct";
            return CONTEXTUAL_SCREENTIP_SET;
        }

        if (Dm.eq(held_item.tool_behaviour, TOOL_MULTITOOL))
        {
            if (Dm.truthy(panel_open))
                context[SCREENTIP_CONTEXT_CTRL_LMB] = "Change cable layer";
            else
                context[SCREENTIP_CONTEXT_CTRL_LMB] = "Link parts";
            return CONTEXTUAL_SCREENTIP_SET;
        }
        return null;
    }
}

// tg: code/modules/power/turbine/turbine.dm:277
public partial class obj_machinery_power_turbine_inlet_compressor : obj_machinery_power_turbine
{
    // tg: code/modules/power/turbine/turbine.dm:295
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        //Volume of gas mixture is 1000
        return base.Initialize(mapload, 1000);
    }

    // tg: code/modules/power/turbine/turbine.dm:299
    public override object is_active()
    {
        return QDELETED(rotor) ? FALSE : rotor.is_active();
    }

    // tg: code/modules/power/turbine/turbine.dm:302
    public override async DmTask<object> deactivate_parts(mob user = null)
    {
        object dot = await base.deactivate_parts(user);
        if (!QDELETED(rotor))
            await rotor.deactivate_parts();
        rotor = null;
        input_turf = null;
        return dot;
    }

    /**
     * transfers gases from its input turf to its internal gas mix
     * Returns temperature of the gas mix absorbed only if some work was done
     */
    // tg: code/modules/power/turbine/turbine.dm:313
    public virtual object compress_gases()
    {
        // SHOULD_NOT_OVERRIDE(TRUE)

        compressor_work = 0;
        compressor_pressure = MINIMUM_TURBINE_PRESSURE;
        // tg keeps a closed turf in its /turf/open var; the port's var holds only an open one, the closed turf is read through intake
        turf intake = QDELETED(input_turf) ? get_step(loc, REVERSE_DIR(dir)) : input_turf;
        input_turf = intake as turf_open;

        datum_gas_mixture input_turf_mixture = Waiting.NoSleep(intake.return_air()) as datum_gas_mixture;
        if (input_turf_mixture == null)
            return 0;

        //the compressor compresses down the gases from 2500 L to 1000 L
        //the temperature and pressure rises up, you can regulate this to increase/decrease the amount of gas moved in.
        compressor_work = transfer_gases(input_turf_mixture, machine_gasmix, work_amount_to_remove: 0, intake_size: intake_regulator);
        intake.air_update_turf(TRUE);
        ((turf_open)intake).update_visuals();
        compressor_pressure = PRESSURE_MAX(machine_gasmix.return_pressure());

        return input_turf_mixture.temperature;
    }
}

//===========================OUTLET==============================================
// tg: code/modules/power/turbine/turbine.dm:336
public partial class obj_machinery_power_turbine_turbine_outlet : obj_machinery_power_turbine
{
    // tg: code/modules/power/turbine/turbine.dm:348
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        //Volume of gas mixture is 6000
        return base.Initialize(mapload, 6000);
    }

    // tg: code/modules/power/turbine/turbine.dm:352
    public override object is_active()
    {
        return QDELETED(rotor) ? FALSE : rotor.is_active();
    }

    // tg: code/modules/power/turbine/turbine.dm:355
    public override async DmTask<object> deactivate_parts(mob user = null)
    {
        object dot = await base.deactivate_parts(user);
        if (!QDELETED(rotor))
            await rotor.deactivate_parts();
        rotor = null;
        output_turf = null;
        return dot;
    }

    /// push gases from its gas mix to output turf
    // tg: code/modules/power/turbine/turbine.dm:363
    public virtual object expel_gases()
    {
        // SHOULD_NOT_OVERRIDE(TRUE)

        // tg keeps a closed turf in its /turf/open var; the port's var holds only an open one, the closed turf is read through exhaust
        turf exhaust = QDELETED(output_turf) ? get_step(loc, dir) : output_turf;
        output_turf = exhaust as turf_open;
        //turf is blocked don't eject gases
        if (!Dm.truthy(TURF_SHARES(exhaust)))
            return FALSE;

        //eject gases and update turf if any was ejected
        datum_gas_mixture ejected_gases = machine_gasmix.pump_gas_to(output_turf.air, machine_gasmix.return_pressure()) as datum_gas_mixture;
        if (ejected_gases != null)
        {
            output_turf.air_update_turf(TRUE);
            output_turf.update_visuals();
        }

        //return ejected gases
        return ejected_gases;
    }
}

//===========================================CORE ROTOR=========================================
// tg: code/modules/power/turbine/turbine.dm:383
public partial class obj_machinery_power_turbine_core_rotor : obj_machinery_power_turbine
{
    // tg: code/modules/power/turbine/turbine.dm:416
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        //Volume of gas mixture is 3000
        object dot = base.Initialize(mapload, 3000);

        Dm.@new<obj_item_paper_guides_jobs_atmos_turbine>(loc);
        return dot;
    }

    // tg: code/modules/power/turbine/turbine.dm:434
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (!Dm.truthy(panel_open))
            Dm.append(ref dot, span_notice(I18n.T("[] open its panel to change cable layer.", EXAMINE_HINT(I18n.T("screw")))));
        if (!Dm.truthy(all_parts_connected))
            Dm.append(ref dot, span_warning(I18n.T("The parts need to be linked via a []", EXAMINE_HINT(I18n.T("multitool")))));
        return dot;
    }

    ///Adds overlays to this turbines appearance
    // tg: code/modules/power/turbine/turbine.dm:442
    public override void set_overlays(DmList overlays = null)
    {
        if (Dm.truthy(active))
        {
            Dm.append(ref overlays, Dm.text("[]_on", base_icon_state));
            // DEVIATION(DV12): overlays += emissive_appearance(icon, "[base_icon_state]_on", src), the glow on the emissive plane, is not created
        }
    }

    // tg: code/modules/power/turbine/turbine.dm:447
    public override object is_active()
    {
        return active;
    }

    // tg: code/modules/power/turbine/turbine.dm:450
    public override async DmTask<object> cable_layer_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(panel_open))
        {
            balloon_alert(user, I18n.T("open panel first!"));
            return ITEM_INTERACT_BLOCKING;
        }
        return await base.cable_layer_act(user, tool);
    }

    // tg: code/modules/power/turbine/turbine.dm:456
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        //allow cable layer changing
        if (Dm.truthy(panel_open))
            return await base.multitool_act(user, tool);

        //failed checks
        if (!Dm.truthy(await activate_parts(user)))
            return ITEM_INTERACT_SUCCESS;

        //log rotor to link later to computer
        balloon_alert(user, I18n.T("all parts linked"));
        obj_item_multitool multitool = (obj_item_multitool)tool;
        multitool.set_buffer(this);
        to_chat(user, span_notice(I18n.T("You store linkage information in []'s buffer.", tool)));

        //success
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/power/turbine/turbine.dm:474
    public override async DmTask<object> multitool_act_secondary(mob_living user = null, obj_item tool = null)
    {
        //allow cable layer changing
        if (Dm.truthy(panel_open))
            return await base.multitool_act_secondary(user, tool);

        //works same as regular left click
        return await multitool_act(user, tool);
    }

    /**
     * convinience proc for balloon alert which returns if viewer is null
     * Arguments
     *
     * * mob/viewer - the player receiving the message
     * * text - the message
     */
    // tg: code/modules/power/turbine/turbine.dm:489
    public virtual void feedback(mob viewer = null, object text = null)
    {
        // PRIVATE_PROC(TRUE)

        if (viewer == null)
            return;
        balloon_alert(viewer, text);
    }

    ///Called to activate the complete machine, checks for part presence, correct orientation and installed parts
    // tg: code/modules/power/turbine/turbine.dm:497
    public override async DmTask<object> activate_parts(mob user = null, float? check_only = null)
    {
        check_only ??= FALSE;
        //if this is not a checkup and all parts are connected then we have nothing to do
        if (!Dm.truthy(check_only) && Dm.truthy(all_parts_connected))
            return TRUE;

        //are we broken
        if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
        {
            feedback(user, I18n.T("rotor is broken!"));
            return all_parts_connected = FALSE;
        }

        //locate compressor & turbine, when checking we simply check to see if they are still there
        if (!Dm.truthy(check_only))
        {
            compressor = Dm.locate<obj_machinery_power_turbine_inlet_compressor>(get_step(this, REVERSE_DIR(dir)));
            turbine = Dm.locate<obj_machinery_power_turbine_turbine_outlet>(get_step(this, dir));

            //maybe look for them the other way around. this means the rotor is facing the wrong way
            if (QDELETED(compressor) && QDELETED(turbine))
            {
                compressor = Dm.locate<obj_machinery_power_turbine_inlet_compressor>(get_step(this, dir));
                turbine = Dm.locate<obj_machinery_power_turbine_turbine_outlet>(get_step(this, REVERSE_DIR(dir)));

                //show corrective actions
                if (!QDELETED(compressor) || !QDELETED(turbine))
                {
                    feedback(user, I18n.T("rotor is facing the wrong way!"));
                    return all_parts_connected = FALSE;
                }
            }
        }

        //sanity checks for compressor
        if (QDELETED(compressor))
        {
            feedback(user, I18n.T("missing compressor!"));
            return all_parts_connected = FALSE;
        }
        if (compressor.dir != dir && compressor.dir != REVERSE_DIR(dir)) //make sure it's not perpendicular to the rotor
        {
            feedback(user, I18n.T("compressor not aligned with rotor!"));
            return all_parts_connected = FALSE;
        }
        if (Dm.truthy(Dm.bitand(compressor.machine_stat, MAINT)))
        {
            feedback(user, I18n.T("close compressor panel!"));
            return all_parts_connected = FALSE;
        }
        if (Dm.truthy(Dm.bitand(compressor.machine_stat, BROKEN)))
        {
            feedback(user, I18n.T("compressor is broken!"));
            return all_parts_connected = FALSE;
        }
        if (compressor.installed_part == null)
        {
            feedback(user, I18n.T("compressor has a missing part!"));
            return all_parts_connected = FALSE;
        }

        //sanity checks for turbine
        if (QDELETED(turbine))
        {
            feedback(user, I18n.T("missing turbine!"));
            return all_parts_connected = FALSE;
        }
        if (turbine.dir != dir && turbine.dir != REVERSE_DIR(dir)) //make sure it's not perpendicular to the rotor
        {
            feedback(user, I18n.T("turbine not aligned with rotor!"));
            return all_parts_connected = FALSE;
        }
        if (Dm.truthy(Dm.bitand(turbine.machine_stat, MAINT)))
        {
            feedback(user, I18n.T("close turbine panel!"));
            return all_parts_connected = FALSE;
        }
        if (Dm.truthy(Dm.bitand(turbine.machine_stat, BROKEN)))
        {
            feedback(user, I18n.T("turbine is broken!"));
            return all_parts_connected = FALSE;
        }
        if (turbine.installed_part == null)
        {
            feedback(user, I18n.T("turbine is missing stator part!"));
            return all_parts_connected = FALSE;
        }

        //sanity check to make sure turbine & compressor are facing the same direction. From an visual perspective they will appear facing away from each other actually. I know blame spriter's
        if (compressor.dir != turbine.dir)
        {
            feedback(user, I18n.T("turbine & compressor are not facing away from each other!"));
            return all_parts_connected = FALSE;
        }

        //all checks successfull remember result
        all_parts_connected = TRUE;
        if (Dm.truthy(check_only))
            return TRUE;

        compressor.rotor = this;
        turbine.rotor = this;
        max_allowed_rpm = (Dm.num(compressor.installed_part.get_tier_value(TURBINE_MAX_RPM)) + Dm.num(turbine.installed_part.get_tier_value(TURBINE_MAX_RPM)) + Dm.num(installed_part.get_tier_value(TURBINE_MAX_RPM))) / 3;
        max_allowed_temperature = (Dm.num(compressor.installed_part.get_tier_value(TURBINE_MAX_TEMP)) + Dm.num(turbine.installed_part.get_tier_value(TURBINE_MAX_TEMP)) + Dm.num(installed_part.get_tier_value(TURBINE_MAX_TEMP))) / 3;
        connect_to_network();

        return await base.activate_parts(user, check_only);
    }

    ///Allows to null the various machines and references from the main core
    // tg: code/modules/power/turbine/turbine.dm:575
    [DmParams()]
    public override async DmTask<object> deactivate_parts(mob user = null)
    {
        toggle_power(force_off: TRUE);
        if (compressor != null)
            compressor.rotor = null;
        compressor = null;
        if (turbine != null)
            turbine.rotor = null;
        turbine = null;
        all_parts_connected = FALSE;
        rpm = 0;
        produced_energy = 0;
        disconnect_from_network();

        return await base.deactivate_parts();
    }

    // tg: code/modules/power/turbine/turbine.dm:588
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        await deactivate_parts();
        return await base.on_deconstruction(disassembled);
    }

    /// Toggle power on and off, not safe
    // tg: code/modules/power/turbine/turbine.dm:593
    public virtual void toggle_power(object force_off = null)
    {
        // SHOULD_NOT_OVERRIDE(TRUE)

        //toggle status
        if (Dm.truthy(force_off))
        {
            if (!Dm.truthy(active)) //was already off
                return;
            active = FALSE;
        }
        else
            active = Dm.truthy(active) ? FALSE : TRUE;

        //update operation status of parts
        update_appearance(UPDATE_OVERLAYS);
        if (!QDELETED(compressor))
            compressor.update_appearance(UPDATE_OVERLAYS);
        if (!QDELETED(turbine))
            turbine.update_appearance(UPDATE_OVERLAYS);

        //start or stop processing
        if (Dm.truthy(active))
        {
            update_mode_power_usage(ACTIVE_POWER_USE, active_power_usage);
            begin_processing();
        }
        else
        {
            unset_static_power();
            end_processing();
        }
    }

    /// Getter for turbine integrity, return the amount in %
    // tg: code/modules/power/turbine/turbine.dm:620
    public virtual object get_turbine_integrity()
    {
        // SHOULD_NOT_OVERRIDE(TRUE)

        return max(round(100 - (Dm.num(damage) / 500) * 100, 0.01f), 0);
    }

    // tg: code/modules/power/turbine/turbine.dm:625
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (!Dm.truthy(active) || !Dm.truthy(Waiting.NoSleep(activate_parts(check_only: TRUE))) || !Dm.truthy(powered__proc(ignore_use_power: TRUE)))
        {
            Waiting.NoSleep(deactivate_parts());
            return PROCESS_KILL;
        }

        //===============COMPRESSOR WORKING========//
        //Transfer gases from turf to compressor
        float temperature = Dm.num(compressor.compress_gases());
        //Compute damage taken based on temperature
        damage_archived = Dm.num(damage);
        float temperature_difference = temperature - max_allowed_temperature;
        float damage_done = round(log(90, max(temperature_difference, 1)), 0.5f);
        damage = max(Dm.num(damage) + damage_done * 0.5f, 0);
        damage = min(damage_archived + TURBINE_MAX_TAKEN_DAMAGE, Dm.num(damage));
        if (temperature_difference < 0)
            damage = max(Dm.num(damage) - TURBINE_DAMAGE_HEALING, 0);
        //Apply damage if it passes threshold limits
        if ((Dm.num(damage) - damage_archived >= 2 || Dm.num(damage) > TURBINE_DAMAGE_ALARM_START) && turbine_damage_alert <= world.time) // COOLDOWN_FINISHED (__DEFINES/cooldowns.dm:113), inline (rules 11.1 (a))
        {
            turbine_damage_alert = world.time + max(round(TURBINE_DAMAGE_ALARM_START - damage_done), 5) * SECONDS; // COOLDOWN_START (__DEFINES/cooldowns.dm:111), inline
            //Boom!
            float integrity = Dm.num(get_turbine_integrity());
            if (integrity <= 0)
            {
                Waiting.NoSleep(deactivate_parts());
                if (rpm < TURBINE_MAX_BASE_RPM)
                {
                    Waiting.NoSleep(explosion(this, 0, 1, 4));
                    return PROCESS_KILL;
                }
                if (rpm < TURBINE_MAX_BASE_RPM * 2.5f)
                {
                    Waiting.NoSleep(explosion(this, 0, 2, 6));
                    return PROCESS_KILL;
                }
                if (rpm < TURBINE_MAX_BASE_RPM * 2.5f * 2.5f)
                {
                    Waiting.NoSleep(explosion(this, 1, 3, 7));
                    return PROCESS_KILL;
                }
                if (rpm < TURBINE_MAX_BASE_RPM * 2.5f * 2.5f * 2.5f)
                    Waiting.NoSleep(explosion(this, 2, 5, 7));
                return PROCESS_KILL;
            }

            Waiting.NoSleep(aas_config_announce(typeof(datum_aas_config_entry_engineering_turbine_failure), Dm.list(Dm.kv("INTEGRITY", integrity), Dm.kv("LOCATION", get_area_name(this))), this, Dm.list(RADIO_CHANNEL_ENGINEERING)));
            playsound(this, Dm.resource("sound/machines/engine_alert/engine_alert1.ogg"), 100, FALSE, 30, 30, falloff_distance: 10);
        }

        //================ROTOR WORKING============//
        //The Rotor moves the gases that expands from 1000 L to 3000 L, they cool down and both temperature and pressure lowers
        float rotor_work = Dm.num(transfer_gases(compressor.machine_gasmix, machine_gasmix, compressor.compressor_work));
        //the turbine expands the gases more from 3000 L to 6000 L, cooling them down further.
        float turbine_work = Dm.num(transfer_gases(machine_gasmix, turbine.machine_gasmix, abs(rotor_work)));

        //================TURBINE WORKING============//
        //Calculate final power generated based on how much gas was ejected from the turbine
        datum_gas_mixture ejected_gases = turbine.expel_gases() as datum_gas_mixture;
        if (ejected_gases == null) //output turf was blocked with high pressure/temperature gases or by some structure so no power generated
        {
            rpm = 0;
            produced_energy = 0;
            return null;
        }
        float work_done = QUANTIZE(ejected_gases.total_moles()) * R_IDEAL_GAS_EQUATION * ejected_gases.temperature * log(Dm.num(compressor.compressor_pressure) / PRESSURE_MAX(ejected_gases.return_pressure()));
        //removing the work needed to move the compressor but adding back the turbine work that is the one generating most of the power.
        work_done = max(work_done - Dm.num(compressor.compressor_work) * TURBINE_COMPRESSOR_STATOR_INTERACTION_MULTIPLIER - turbine_work, 0);
        //calculate final acheived rpm
        rpm = Dm.pow(work_done * compressor.efficiency, turbine.efficiency) * efficiency / TURBINE_RPM_CONVERSION;
        rpm = min(ROUND_UP(rpm), max_allowed_rpm);
        //add energy into the grid, also use part of it for turbine operation
        produced_energy = rpm * TURBINE_ENERGY_RECTIFICATION_MULTIPLIER * TURBINE_RPM_CONVERSION * Dm.num(seconds_per_tick);
        add_avail(produced_energy);
        return null;
    }

    // tg: code/modules/power/turbine/turbine.dm:422
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        object dot = base.add_context(source, context, held_item, user);
        if (Dm.eq(dot, NONE))
            return dot;

        if (Dm.eq(held_item.tool_behaviour, TOOL_MULTITOOL))
        {
            if (Dm.truthy(panel_open))
                context[SCREENTIP_CONTEXT_CTRL_LMB] = "Change cable layer";
            else
                context[SCREENTIP_CONTEXT_CTRL_LMB] = "Link/Log parts";
            return CONTEXTUAL_SCREENTIP_SET;
        }
        return dot;
    }
}
