// Translated from tgstation (snapshot 1d99bd7f): code/modules/reagents/chemistry/machinery/chem_separator.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps P5): the distillation apparatus with its burner, condenser, boiling sound and the ChemSeparator window. Its
// screentips (add_context) are a live-partial row (D264); the burner's glow is DV12's (emissive_appearance). The boiling points
// read the reagents' masses from GLOB.chemical_reagents_list, built at its first read (DV309).

// tg: code/modules/reagents/chemistry/machinery/chem_separator.dm:4
public partial class obj_structure_chem_separator : obj_structure
{
    ///The maximum number of settings on a burner knob
    // tg: code/modules/reagents/chemistry/machinery/chem_separator.dm:2  #define MAX_BURNER_KNOB_SETTINGS 10 (file-local)
    private const float MAX_BURNER_KNOB_SETTINGS = 10;

    // tg: code/modules/reagents/chemistry/machinery/chem_separator.dm:31
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        create_reagents(100, Dm.bitor(TRANSPARENT, INJECTABLE));
        soundloop = Dm.@new<datum_looping_sound_boiling>(this);
        register_context();
        return dot;
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_separator.dm:37
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref distilled_container);
        QDEL_NULL(ref fuel_container);
        QDEL_NULL(ref soundloop);
        return base.Destroy(force);
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_separator.dm:43
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        atom drop = drop_location() as atom;

        Dm.@new<obj_item_stack_sheet_mineral_wood>(drop);

        Dm.@new<obj_item_thermometer>(drop);

        Dm.@new<obj_item_burner>(drop);

        if (Dm.truthy(condenser_installed))
            Dm.@new<obj_item_assembly_igniter_condenser>(drop);

        if (!QDELETED(distilled_container))
            await distilled_container.forceMove(drop);

        if (!QDELETED(fuel_container))
            await fuel_container.forceMove(drop);
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_separator.dm:61
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (distilled_container == gone)
        {
            distilled_container = null;
            update_appearance(UPDATE_OVERLAYS);
        }
        if (fuel_container == gone)
        {
            await toggle_burner(FALSE);
            fuel_container = null;
            update_appearance(UPDATE_OVERLAYS);
        }
        return dot;
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_separator.dm:101
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        datum_reagents flask = reagents as datum_reagents;

        if (Dm.truthy(flask.total_volume))
            Dm.append(dot, span_notice(I18n.T("The distilation flask reads <b>[]/[]u</b>.", flask.total_volume, flask.maximum_volume)));
        if (!QDELETED(distilled_container))
        {
            datum_reagents distilled = distilled_container.reagents as datum_reagents;
            Dm.append(dot, span_notice(I18n.T("The distilation beaker reads <b>[]/[]u</b>.", distilled.total_volume, distilled.maximum_volume)));
            Dm.append(dot, span_notice(I18n.T("Remove beaker with [].", EXAMINE_HINT("LMB"))));
        }
        else
            Dm.append(dot, span_warning(I18n.T("Its missing a distilation container, insert with []", EXAMINE_HINT("LMB"))));
        if (!QDELETED(fuel_container))
        {
            datum_reagents fuel = fuel_container.reagents as datum_reagents;
            Dm.append(dot, span_notice(I18n.T("The burner fuel container reads <b>[]/[]u</b>.", fuel.total_volume, fuel.maximum_volume)));
            Dm.append(dot, span_notice(I18n.T("Remove fuel with [].", EXAMINE_HINT("RMB"))));
        }
        else
            Dm.append(dot, span_warning(I18n.T("Its missing a beaker containing fuel for the burner, insert with []", EXAMINE_HINT("RMB"))));
        if (Dm.truthy(burner_on))
            Dm.append(dot, span_notice(I18n.T("Off burner with [].", EXAMINE_HINT("ALT LMB"))));
        else
            Dm.append(dot, span_notice(I18n.T("You can start a flame with a combustible device.")));

        if (Dm.truthy(condenser_installed))
            Dm.append(dot, span_notice(I18n.T("The in-built condenser can facilitate faster cooling but consumes fuel.")));
        else
            Dm.append(dot, span_notice(I18n.T("You could install a [] for faster cooling.", EXAMINE_HINT("condenser"))));

        Dm.append(dot, span_notice(I18n.T("You can [] to see reagent boiling points & fuel properties.", EXAMINE_HINT("examine more"))));
        Dm.append(dot, span_notice(I18n.T("The whole aparatus can be [] apart.", EXAMINE_HINT("pried"))));
        return dot;
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_separator.dm:129
    public override async DmTask<object> examine_more(mob user = null)
    {
        object dot = await base.examine_more(user);

        Dm.append(ref dot, span_notice(I18n.T("For burner fuel Plasma > Oil > Welding Fuel = Oxygen > Ethanol > Monkey Energy")));

        Dm.append(ref dot, span_notice(I18n.T("Upon cross examining the flasks reagents contents with its chart you see the boiling points of each reagent present.")));
        foreach (datum_reagent reg in Dm.each_cast<datum_reagent>((reagents as datum_reagents).reagent_list))
            Dm.append(ref dot, span_notice(I18n.T("[] []K", reg.name, get_boiling_point(reg))));
        return dot;
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_separator.dm:138
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        datum_reagents flask = reagents as datum_reagents;

        //burner overlays
        if (Dm.truthy(burner_on))
        {
            Dm.append(ref dot, mutable_appearance(Dm.resource("icons/obj/medical/chemical.dmi"), "separator_burn"));
            // DEVIATION(DV12): . += emissive_appearance('icons/obj/medical/chemical.dmi', "separator_burn", src): the flame's glow on the
            // emissive plane (the burner's own light, set_light, still lights the room)
        }

        // var/static/list/fill_icon_thresholds (rules 10.6: an immutable list, rebuilt per call)
        DmList fill_icon_thresholds = Dm.list(1, 30, 80);

        //distilation flask overlays
        if (Dm.truthy(flask.total_volume))
        {
            mutable_appearance overlay = reagent_threshold_overlay(flask, Dm.resource("icons/obj/medical/reagent_fillings.dmi"), "separator_m_", fill_icon_thresholds) as mutable_appearance;
            if (overlay != null)
                Dm.append(ref dot, overlay);
        }

        //dripping overlay
        if (Dm.truthy(boiling))
        {
            mutable_appearance filling = mutable_appearance(Dm.resource("icons/obj/medical/reagent_fillings.dmi"), "separator_dripping") as mutable_appearance;
            filling.color = mix_color_from_reagents(flask.reagent_list) as string;
            Dm.append(ref dot, filling);
        }

        //distilation beaker overlays
        if (!QDELETED(distilled_container))
        {
            Dm.append(ref dot, "separator_beaker");
            mutable_appearance overlay = reagent_threshold_overlay(distilled_container.reagents as datum_reagents, Dm.resource("icons/obj/medical/reagent_fillings.dmi"), "separator_b_", fill_icon_thresholds) as mutable_appearance;
            if (overlay != null)
                Dm.append(ref dot, overlay);
        }

        //thermometer overlay
        // var/static/list/temperature_icon_thresholds (rules 10.6: an immutable list, rebuilt per call)
        DmList temperature_icon_thresholds = Dm.list(0, 50, 100);
        int threshold = 0;
        for (int i = 1; i <= temperature_icon_thresholds.len; i++)
        {
            if (ROUND_UP(flask.chem_temp - T0C) >= Dm.num(temperature_icon_thresholds[i]))
                threshold = i;
        }
        if (threshold != 0)
        {
            string fill_name = Dm.text("separator_temp_[]", temperature_icon_thresholds[threshold]);
            mutable_appearance filling = mutable_appearance(Dm.resource("icons/obj/medical/chemical.dmi"), fill_name) as mutable_appearance;
            Dm.append(ref dot, filling);
        }
        return dot;
    }

    /**
     * Computes the boiling point of the reagent based on its mass. heaiver reagents obviously needs higher temps
     * Arguments
     *
     * * datum/reagent/reg - the reagent whos boiling point we are trying to compute
     */
    // tg: code/modules/reagents/chemistry/machinery/chem_separator.dm:184
    public virtual object get_boiling_point(datum_reagent reg = null)
    {
        //use the constant mass set on init
        reg = chemical_reagents_list_built()[reg.type] as datum_reagent; // DEVIATION(DV309): GLOB.chemical_reagents_list, built on its first read

        //reagents masses can vary between 10->800
        float normalized_coord = (Dm.num(reg.mass) - 10) / 790;

        //return a boiling point anywhere between 400->900 k. Change if you want more varity
        return 400 + (500 * normalized_coord);
    }

    /**
     * Computes the coefficient of burning(the ability of the reagent mixture to burn) of the burner fuel container
     * reagents can affect the intensity of the flame in different ways. A -ve value the mixture can not combust
     * whereas a +ve value(<= 1) means the flame can burn at maximum efficiency
     */
    // tg: code/modules/reagents/chemistry/machinery/chem_separator.dm:201
    public virtual object get_ignition_coefficient()
    {
        if (QDELETED(fuel_container))
            return 0;

        //map of reagents & how much burning potential they all have
        // var/static/list/reagent_coefficients (rules 10.6: an immutable map, rebuilt per call)
        DmList reagent_coefficients = Dm.list(
            Dm.kv(typeof(datum_reagent_toxin_plasma), 1),
            Dm.kv(typeof(datum_reagent_fuel_oil), 0.9f),
            Dm.kv(typeof(datum_reagent_fuel), 0.8f),
            Dm.kv(typeof(datum_reagent_oxygen), 0.8f),
            Dm.kv(typeof(datum_reagent_consumable_ethanol), 0.7f),
            Dm.kv(typeof(datum_reagent_consumable_monkey_energy), 0.6f),
            Dm.kv(typeof(datum_reagent_water), -0.7f));

        float total_coefficient = 0;
        foreach (datum_reagent reg in Dm.each_cast<datum_reagent>((fuel_container.reagents as datum_reagents).reagent_list))
        {
            float coefficient = -1; //any fuel that is not on the list acts as an inhibitor
            foreach (Type fuel in Dm.each_cast<Type>(reagent_coefficients))
            {
                if (istype(reg, fuel))
                {
                    coefficient = Dm.num(reagent_coefficients[fuel]);
                    break;
                }
            }
            total_coefficient += coefficient;
        }

        return Dm.clamp(total_coefficient, 0, 1);
    }

    /**
     * Toggles the burner on(only for a good fuel composition) or off
     * Arguments
     *
     * * state - on or off
     */
    // tg: code/modules/reagents/chemistry/machinery/chem_separator.dm:235
    public virtual async DmTask toggle_burner(object state = null)
    {
        if (!Dm.truthy(state))
        {
            burner_on = FALSE;
            await set_light(0);
        }
        else
        {
            if (!Dm.truthy(get_ignition_coefficient())) //no proper fuel
                return;
            if (!Dm.truthy((reagents as datum_reagents).total_volume)) //no reagents to distill
                return;
            if (QDELETED(distilled_container)) //no beaker to receive distilled reagents
                return;
            burner_on = TRUE;
            await set_light(ROUND_UP(2 * (burner_knob / 5)));
        }
        update_appearance(UPDATE_OVERLAYS);
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_separator.dm:252
    public override async DmTask<object> fire_act(object exposed_temperature = null, object exposed_volume = null)
    {
        await toggle_burner(TRUE);
        return null;
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_separator.dm:255
    public override async DmTask<object> extinguish()
    {
        object dot = await base.extinguish();
        await toggle_burner(FALSE);
        return dot;
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_separator.dm:259
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(user.combat_mode) || Dm.truthy(Dm.bitand(tool.item_flags, ABSTRACT)) || Dm.truthy(Dm.bitand(tool.flags_1, HOLOGRAM_1)) || !Dm.truthy(await user.can_perform_action(this, ALLOW_SILICON_REACH)))
            return ITEM_INTERACT_SKIP_TO_ATTACK;

        ///Add the distilation flask
        if (is_reagent_container(tool) && Dm.truthy(tool.is_open_container()))
        {
            //transfer old container
            if (!QDELETED(distilled_container))
                await user.put_in_hands(distilled_container);

            //add new container
            if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
            {
                to_chat(user, span_warning(I18n.T("[] is stuck in your hand.", tool)));
                return ITEM_INTERACT_BLOCKING;
            }
            distilled_container = tool as obj_item_reagent_containers;

            START_PROCESSING(SSobj, this);
            balloon_alert(user, "distillation container added.");

            await ui_interact(user);
            update_appearance(UPDATE_OVERLAYS);
            return ITEM_INTERACT_SUCCESS;
        }
        else if (istype(tool, typeof(obj_item_assembly_igniter_condenser)))
        {
            if (!Dm.truthy(await user.temporarilyRemoveItemFromInventory(tool)))
            {
                to_chat(user, span_warning(I18n.T("[] is stuck in your hand.", tool)));
                return ITEM_INTERACT_BLOCKING;
            }
            condenser_installed = TRUE;
            await update_static_data_for_all_viewers();
            qdel(tool);
            balloon_alert(user, "condenser installed.");
            return ITEM_INTERACT_SUCCESS;
        }

        ///Try & ignite the bunset burner with this item
        object ignition_message = await tool.ignition_effect(this, user);
        if (!Dm.truthy(ignition_message))
            return NONE;
        user.visible_message(ignition_message);
        await toggle_burner(TRUE);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_separator.dm:300
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        await deconstruct(TRUE);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_separator.dm:304
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        if (!QDELETED(distilled_container))
        {
            if (!Dm.truthy(SStgui.get_open_ui(user, this))) //for convinience open ui first then interact with beakers if you still want to
            {
                await ui_interact(user);
                return TRUE;
            }

            if (Dm.truthy(await (user as mob_living).put_in_hands(distilled_container)))
            {
                to_chat(user, span_notice(I18n.T("you take out the output flask.")));
                update_appearance(UPDATE_OVERLAYS);
            }
            return TRUE;
        }

        return await base.attack_hand(user, modifiers, __extra);
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_separator.dm:317
    public override async DmTask<object> item_interaction_secondary(mob_living user = null, obj_item tool = null, DmList modifiers = null)
    {
        object dot = NONE;
        if (Dm.truthy(user.combat_mode) || Dm.truthy(Dm.bitand(tool.item_flags, ABSTRACT)) || Dm.truthy(Dm.bitand(tool.flags_1, HOLOGRAM_1)) || !Dm.truthy(await user.can_perform_action(this, ALLOW_SILICON_REACH)))
            return ITEM_INTERACT_SKIP_TO_ATTACK;

        if (is_reagent_container(tool) && Dm.truthy(tool.is_open_container()))
        {
            //transfer old container
            if (!QDELETED(fuel_container))
                await user.put_in_hands(fuel_container);

            //add new container
            if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
            {
                to_chat(user, span_warning(I18n.T("[] is stuck in your hand.", tool)));
                return ITEM_INTERACT_BLOCKING;
            }
            fuel_container = tool as obj_item_reagent_containers;
            balloon_alert(user, "fuel container added.");

            await ui_interact(user);
            return ITEM_INTERACT_SUCCESS;
        }
        return dot;
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_separator.dm:337
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        if (!QDELETED(fuel_container))
        {
            if (!Dm.truthy(SStgui.get_open_ui(user, this))) //for convinience open ui first then interact with beakers if you still want to
            {
                await ui_interact(user);
                return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
            }

            if (Dm.truthy(await (user as mob_living).put_in_hands(fuel_container)))
            {
                to_chat(user, span_notice(I18n.T("you take out the burner fuel container")));
                await toggle_burner(FALSE);
            }
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        }

        return await base.attack_hand_secondary(user, modifiers);
    }

    ///Returns the coefficient of cooling of reagents, taking into consideration the condenser
    // tg: code/modules/reagents/chemistry/machinery/chem_separator.dm:351
    public virtual async DmTask<object> get_cool_coefficient()
    {
        float coefficient = 0.2f;

        float fuel_coefficient = Dm.num(get_ignition_coefficient());
        if (Dm.truthy(condenser_installed) && Dm.truthy(condenser_on) && fuel_coefficient > 0)
        {
            datum_reagents fuel = fuel_container.reagents as datum_reagents;
            if (Dm.truthy(await fuel.remove_all(0.15f)))
                coefficient += fuel_coefficient;
            else
                condenser_on = FALSE;
        }

        return coefficient;
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_separator.dm:366
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        return Waiting.NoSleep(process_separator(Dm.num(seconds_per_tick)));
    }

    /// process()'s body: tg's process is synchronous, the holder's procs it calls are DmTasks (rules 8)
    private async DmTask<object> process_separator(float seconds_per_tick)
    {
        datum_reagents flask = reagents as datum_reagents;
        if (!Dm.truthy(flask.total_volume))
        {
            if (QDELETED(distilled_container) || !Dm.truthy((distilled_container.reagents as datum_reagents).total_volume))
            {
                boiling = FALSE;
                soundloop.stop();
                await toggle_burner(FALSE);
                return PROCESS_KILL;
            }
        }

        //if burner in on attempt to heat the reagents
        if (Dm.truthy(burner_on))
        {
            float can_process = TRUE;

            //do we have good quality fuel to burn
            float fuel_coefficient = Dm.num(get_ignition_coefficient());
            if (!Dm.truthy(fuel_coefficient))
            {
                can_process = FALSE;
                await toggle_burner(FALSE);
            }

            float knob_ratio = burner_knob / MAX_BURNER_KNOB_SETTINGS;
            datum_reagents fuel = fuel_container.reagents as datum_reagents;

            //consume some air after we have validated we have some good fuel. Only if we don't already use O2 as a fuel
            if (Dm.truthy(can_process) && !Dm.truthy(fuel.has_reagent(typeof(datum_reagent_oxygen))))
            {
                datum_gas_mixture air = await return_air() as datum_gas_mixture;
                if (!Dm.truthy(air.remove_specific(typeof(datum_gas_oxygen), 0.01f + (0.04f * knob_ratio)))) //can burn anywhere between 0.01 & 0.05 moles of air based on the knob settings
                {
                    can_process = FALSE;
                    await toggle_burner(FALSE);
                }
            }

            //burn some fuel if we combusted some air
            if (Dm.truthy(can_process))
            {
                if (!Dm.truthy(await fuel.remove_all(0.01f + (0.19f * knob_ratio)))) //can burn anywhere between 0.01 & 0.2 units of fuel based on the knob settings
                {
                    can_process = FALSE;
                    await toggle_burner(FALSE);
                }
            }

            //finally heat the mixture
            if (Dm.truthy(can_process))
            {
                flask.adjust_thermal_energy((1000 - flask.chem_temp) * seconds_per_tick * SPECIFIC_HEAT_DEFAULT * (0.05f + (0.45f * knob_ratio)) * fuel_coefficient);
                await flask.handle_reactions();
            }
        }
        else if (flask.chem_temp > DEFAULT_REAGENT_TEMPERATURE) //the container cools down if there is no flame heating it till it reaches room temps
        {
            flask.adjust_thermal_energy((DEFAULT_REAGENT_TEMPERATURE - flask.chem_temp) * seconds_per_tick * SPECIFIC_HEAT_DEFAULT * Dm.num(await get_cool_coefficient()));
            await flask.handle_reactions();
        }

        //the target distilation beaker also cools down
        if (!QDELETED(distilled_container) && (distilled_container.reagents as datum_reagents).chem_temp > DEFAULT_REAGENT_TEMPERATURE)
        {
            datum_reagents distiled_reagents = distilled_container.reagents as datum_reagents;
            distiled_reagents.adjust_thermal_energy((DEFAULT_REAGENT_TEMPERATURE - distiled_reagents.chem_temp) * seconds_per_tick * SPECIFIC_HEAT_DEFAULT * Dm.num(await get_cool_coefficient()));
            await distiled_reagents.handle_reactions();
        }

        //the distilation process checks the individual boiling point of each reagent based on their mass for seperation
        boiling = FALSE;
        foreach (datum_reagent reg in Dm.each<datum_reagent>(flask.reagent_list))
        {
            float bp = Dm.num(get_boiling_point(reg));
            //we can now distil this
            if (flask.chem_temp > bp)
            {
                //distilation rate increases as temps go up peaking at 12 units per tick(at 200k above boiling point)
                float amount = 2 + ((flask.chem_temp - bp) / 200) * 10;
                if (!QDELETED(distilled_container))
                    await flask.trans_to(distilled_container.reagents, amount, target_id: reg.type);
                else //no target container means reagents vanish into thin air i.e leak out
                    await flask.remove_reagent(reg.type, amount);
                boiling = TRUE;
            }
        }

        //boiling sound effect
        if (Dm.truthy(boiling))
        {
            if (!Dm.truthy(loop_started))
            {
                soundloop.start();
                loop_started = TRUE;
                update_appearance(UPDATE_OVERLAYS);
            }
        }
        else
        {
            soundloop.stop();
            loop_started = FALSE;
            update_appearance(UPDATE_OVERLAYS);
        }
        return null;
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_separator.dm:439
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "ChemSeparator", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_separator.dm:445
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        await DmTask.CompletedTask;
        return Dm.list(
            Dm.kv("condenser_installed", condenser_installed),
            Dm.kv("max_burner_knob_settings", MAX_BURNER_KNOB_SETTINGS));
    }

    /// One holder's ui entry (tg writes it out three times, chem_separator.dm:455-483)
    private static DmList holder_data(datum_reagents holder) => Dm.list(
        Dm.kv("total_volume", holder.total_volume),
        Dm.kv("maximum_volume", holder.maximum_volume),
        Dm.kv("temp", holder.chem_temp),
        Dm.kv("color", mix_color_from_reagents(holder.reagent_list)));

    // tg: code/modules/reagents/chemistry/machinery/chem_separator.dm:451
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList dot = Dm.list();

        //distilation flask data
        dot["flask"] = holder_data(reagents as datum_reagents);

        //distilled beaker data
        DmList distilled_data = null;
        if (!QDELETED(distilled_container))
            distilled_data = holder_data(distilled_container.reagents as datum_reagents);
        dot["beaker"] = distilled_data;

        //burner fuel data
        DmList fuel_data = null;
        if (!QDELETED(fuel_container))
            fuel_data = holder_data(fuel_container.reagents as datum_reagents);
        dot["fuel"] = fuel_data;

        //Knob setting
        dot["burner_on"] = burner_on;
        dot["knob"] = burner_knob;

        //Condenser setting
        dot["condenser_on"] = condenser_on;
        await DmTask.CompletedTask;
        return dot;
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_separator.dm:493
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        datum_reagents flask = reagents as datum_reagents;
        switch (action)
        {
            case "drain":
                if (QDELETED(distilled_container) || !Dm.truthy(flask.total_volume))
                    return FALSE;

                if (Dm.truthy(await flask.trans_to(distilled_container.reagents, flask.maximum_volume)))
                {
                    await toggle_burner(FALSE);
                    return TRUE;
                }
                break;

            case "filter":
                if (QDELETED(distilled_container) || !Dm.truthy((distilled_container.reagents as datum_reagents).total_volume))
                    return FALSE;

                if (Dm.truthy(await (distilled_container.reagents as datum_reagents).trans_to(flask, flask.maximum_volume)))
                {
                    update_appearance(UPDATE_OVERLAYS);
                    return TRUE;
                }
                break;

            case "knob":
            {
                object setting = @params["amount"];
                if (setting == null)
                    return FALSE;

                float? number = text2num(setting);
                if (!Dm.truthy(number))
                    return FALSE;

                burner_knob = Dm.clamp(number.Value, 1, MAX_BURNER_KNOB_SETTINGS);
                if (Dm.truthy(burner_on))
                    await set_light(ROUND_UP(2 * (burner_knob / MAX_BURNER_KNOB_SETTINGS)));
                return TRUE;
            }

            case "cool":
                if (!Dm.truthy(condenser_installed))
                    return FALSE;

                condenser_on = Dm.truthy(condenser_on) ? FALSE : TRUE;
                return TRUE;
        }
        return dot;
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_separator.dm:536
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        if (!Dm.truthy(burner_on))
            return CLICK_ACTION_BLOCKING;

        await toggle_burner(FALSE);
        return CLICK_ACTION_SUCCESS;
    }
}
