// Translated from tgstation (snapshot 1d99bd7f): code/modules/reagents/chemistry/machinery/chem_mass_spec.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps P5): the high-performance liquid chromatography machine (the mass spectrometer) with its MassSpec window.
// Its screentips (add_context) are a live-partial row (D264). An inverted reagent's mass is read from GLOB.chemical_reagents_list,
// built at its first read (DV309); the var block is generated.

// tg: code/modules/reagents/chemistry/machinery/chem_mass_spec.dm:1
public partial class obj_machinery_chem_mass_spec : obj_machinery
{
    /// GLOB.chemical_reagents_list[path] (DV309: built at its first read)
    private static datum_reagent glob_entry(object path) => chemical_reagents_list_built()[path] as datum_reagent;

    // tg: code/modules/reagents/chemistry/machinery/chem_mass_spec.dm:33
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        if (Dm.truthy(mapload))
            beaker2 = Dm.@new<obj_item_reagent_containers_cup_beaker_large>(this);

        register_context();
        return dot;
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_mass_spec.dm:41
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref beaker1);
        QDEL_NULL(ref beaker2);
        return base.Destroy(force);
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_mass_spec.dm:46
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        atom location = drop_location() as atom;
        if (beaker1 != null)
            await beaker1.forceMove(location);
        if (beaker2 != null)
            await beaker2.forceMove(location);
        return null;
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_mass_spec.dm:87
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;

        if (!QDELETED(beaker1))
        {
            Dm.append(dot, span_notice(I18n.T("Input beaker of []u capacity is inserted.", (beaker1.reagents as datum_reagents).maximum_volume)));
            Dm.append(dot, span_notice(I18n.T("Its Input beaker Can be ejected with [] click.", EXAMINE_HINT("LMB Alt"))));
        }
        else
            Dm.append(dot, span_warning(I18n.T("Its missing an input beaker. insert with [].", EXAMINE_HINT("Left Click"))));
        if (!QDELETED(beaker2))
        {
            Dm.append(dot, span_notice(I18n.T("Output beaker of []u capacity is inserted.", (beaker2.reagents as datum_reagents).maximum_volume)));
            Dm.append(dot, span_notice(I18n.T("Its Output beaker can be ejected with [] click.", EXAMINE_HINT("RMB Alt"))));
        }
        else
            Dm.append(dot, span_warning(I18n.T("Its missing an output beaker, insert with [].", EXAMINE_HINT("Right Click"))));

        if (Dm.truthy(anchored))
            Dm.append(dot, span_notice(I18n.T("Its [] in place.", EXAMINE_HINT("anchored"))));
        else
            Dm.append(dot, span_warning(I18n.T("Needs to be [] to use.", EXAMINE_HINT("wrenched"))));
        Dm.append(dot, span_notice(I18n.T("Its maintenance panel can be [] [].", EXAMINE_HINT("screwed"), Dm.truthy(panel_open) ? "closed" : "open")));
        if (Dm.truthy(panel_open))
            Dm.append(dot, span_notice(I18n.T("It can be [] apart.", EXAMINE_HINT("pried"))));
        return dot;
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_mass_spec.dm:109
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);

        if (Dm.truthy(panel_open))
        {
            Dm.append(ref dot, mutable_appearance(icon, Dm.text("[]_panel-o", base_icon_state)));
            return dot;
        }

        if (!QDELETED(beaker1))
            Dm.append(ref dot, "HPLC_beaker1");
        if (!QDELETED(beaker2))
            Dm.append(ref dot, "HPLC_beaker2");

        if (Dm.truthy(is_operational) && !Dm.truthy(panel_open) && Dm.truthy(anchored) && !Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(BROKEN, NOPOWER))))
        {
            if (Dm.truthy(processing_reagents))
                Dm.append(ref dot, "HPLC_graph_active");
            else if (Dm.truthy(length((beaker1?.reagents as datum_reagents)?.reagent_list)))
                Dm.append(ref dot, "HPLC_graph_idle");
        }
        return dot;
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_mass_spec.dm:127
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (Dm.truthy(is_operational) && !Dm.truthy(panel_open) && Dm.truthy(anchored) && !Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(BROKEN, NOPOWER))))
            icon_state = "HPLC_on";
        else
            icon_state = "HPLC";
        return await base.update_icon_state(__extra);
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_mass_spec.dm:134
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (gone == beaker1)
            beaker1 = null;
        if (gone == beaker2)
            beaker2 = null;
        return dot;
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_mass_spec.dm:141
    public override async DmTask<object> RefreshParts()
    {
        object dot = await base.RefreshParts();

        cms_coefficient = 1;
        foreach (datum_stock_part_micro_laser laser in Dm.each<datum_stock_part_micro_laser>(component_parts))
            cms_coefficient /= Dm.num(laser.tier);
        return dot;
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_mass_spec.dm:148
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        obj_item item = tool;
        if (Dm.truthy(processing_reagents))
        {
            balloon_alert(user, "still processing!");
            return ITEM_INTERACT_BLOCKING;
        }

        if (!Dm.truthy(await item.can_insert_container(user, this)))
            return NONE;

        object is_right_clicking = LAZYACCESS(modifiers, RIGHT_CLICK);
        if (!Dm.truthy(await replace_beaker(user, !Dm.truthy(is_right_clicking) ? TRUE : FALSE, item as obj_item_reagent_containers)))
            return ITEM_INTERACT_BLOCKING;

        to_chat(user, span_notice(I18n.T("You add [] to [] slot.", item, Dm.truthy(is_right_clicking) ? "output" : "input")));
        update_appearance();
        await ui_interact(user);

        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_mass_spec.dm:166
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        object dot = ITEM_INTERACT_BLOCKING;
        if (Dm.truthy(processing_reagents))
        {
            balloon_alert(user, "still processing!");
            return dot;
        }

        if (Dm.eq(await default_unfasten_wrench(user, tool), SUCCESSFUL_UNFASTEN))
            return ITEM_INTERACT_SUCCESS;
        return dot;
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_mass_spec.dm:175
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(processing_reagents))
        {
            balloon_alert(user, "still processing!");
            return ITEM_INTERACT_BLOCKING;
        }

        await DmTask.CompletedTask;
        return default_deconstruction_screwdriver(user, tool);
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_mass_spec.dm:182
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(processing_reagents))
        {
            balloon_alert(user, "still processing!");
            return ITEM_INTERACT_BLOCKING;
        }

        return await default_deconstruction_crowbar(user, tool);
    }

    /**
     * Computes either the lightest or heaviest reagent in the input beaker
     * Arguments
     *
     * * smallest - TRUE to find lightest reagent, FALSE to find heaviest reagent
     */
    // tg: code/modules/reagents/chemistry/machinery/chem_mass_spec.dm:196
    public virtual object calculate_mass(float? smallest = null)
    {
        smallest ??= TRUE;
        if (QDELETED(beaker1))
            return 0;

        float result = 0;
        foreach (datum_reagent reagent in Dm.each_cast<datum_reagent>((beaker1?.reagents as datum_reagents)?.reagent_list))
        {
            datum_reagent target = reagent;
            if (reagent.inverse_chem != null && Dm.num(reagent.inverse_chem_val) > reagent.purity)
                target = glob_entry(reagent.inverse_chem);

            if (!Dm.truthy(result))
                result = Dm.num(target.mass);
            else
                result = Dm.truthy(smallest) ? Dm.min(result, Dm.num(target.mass)) : Dm.max(result, Dm.num(target.mass));
        }
        return Dm.truthy(smallest) ? FLOOR(result, 50) : CEILING(result, 50);
    }

    /*
     * Replaces a beaker in the machine, either input or output. Returns TRUE on success.
     * Arguments
     *
     * * user - The one bonking the machine
     * * target beaker - the target beaker we are trying to replace
     * * new beaker - the new beaker to add/replace the slot with
     */
    // tg: code/modules/reagents/chemistry/machinery/chem_mass_spec.dm:223
    public virtual async DmTask<object> replace_beaker(mob_living user = null, object is_input = null, obj_item_reagent_containers new_beaker = null)
    {
        if (Dm.truthy(is_input)) //replace input beaker
        {
            if (!QDELETED(beaker1))
                await try_put_in_hand(beaker1, user);
            if (!QDELETED(new_beaker))
            {
                if (!Dm.truthy(await user.transferItemToLoc(new_beaker, this)))
                {
                    update_appearance(UPDATE_OVERLAYS);
                    return FALSE;
                }

                beaker1 = new_beaker;
                lower_mass_range = Dm.num(calculate_mass(smallest: TRUE));
                upper_mass_range = Dm.num(calculate_mass(smallest: FALSE));
                estimate_time();
            }
        }
        else //replace output beaker
        {
            if (!QDELETED(beaker2))
                await try_put_in_hand(beaker2, user);
            if (!QDELETED(new_beaker))
            {
                if (!Dm.truthy(await user.transferItemToLoc(new_beaker, this)))
                {
                    update_appearance(UPDATE_OVERLAYS);
                    return FALSE;
                }

                beaker2 = new_beaker;
                log.Cut();
            }
        }

        update_appearance(UPDATE_OVERLAYS);

        return TRUE;
    }

    ///Computes time to purity reagents
    // tg: code/modules/reagents/chemistry/machinery/chem_mass_spec.dm:254
    public virtual void estimate_time()
    {
        delay_time = 0;
        if (QDELETED(beaker1))
            return;

        foreach (datum_reagent reagent in Dm.each_cast<datum_reagent>((beaker1.reagents as datum_reagents).reagent_list))
        {
            datum_reagent target = reagent;
            float inverse = FALSE;

            //inverted chems are dealt with diffrently
            if (reagent.inverse_chem != null && Dm.num(reagent.inverse_chem_val) > reagent.purity)
            {
                target = glob_entry(reagent.inverse_chem);
                inverse = TRUE;
            }
            //out of our selected range
            if (Dm.num(target.mass) < lower_mass_range || Dm.num(target.mass) > upper_mass_range)
                continue;
            //already at max purity
            if (!Dm.truthy(inverse) && (Dm.initial(reagent, (datum_reagent r) => r.purity) - reagent.purity) <= 0)
                continue;
            ///Roughly 10 - 30s?
            delay_time += (((Dm.num(target.mass) * Dm.num(reagent.volume)) + (Dm.num(target.mass) * Dm.num(reagent.get_inverse_purity()) * 0.1f)) * 0.0035f) + 10;
        }

        delay_time *= cms_coefficient;
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_mass_spec.dm:280
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "MassSpec", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_mass_spec.dm:286
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList dot = Dm.list();
        dot["lowerRange"] = lower_mass_range;
        dot["upperRange"] = upper_mass_range;
        dot["processing"] = processing_reagents;
        dot["eta"] = delay_time - progress_time;
        dot["peakHeight"] = 0;
        obj_item held_item = user.get_active_held_item() as obj_item;
        dot["hasBeakerInHand"] = held_item?.is_chem_container() is var held && Dm.truthy(held) ? held : FALSE;

        //input reagents
        DmList beaker1Data = null;
        if (!QDELETED(beaker1))
        {
            beaker1Data = Dm.list();
            datum_reagents beaker_1_reagents = beaker1.reagents as datum_reagents;
            beaker1Data["currentVolume"] = beaker_1_reagents.total_volume;
            beaker1Data["maxVolume"] = beaker_1_reagents.maximum_volume;
            DmList beakerContents = Dm.list();
            foreach (datum_reagent reagent in Dm.each_cast<datum_reagent>(beaker_1_reagents.reagent_list))
            {
                object log_text = "Ready";
                datum_reagent target = reagent;
                float purity = target.purity;
                float is_inverse = FALSE;

                if (Dm.num(reagent.inverse_chem_val) > reagent.purity && reagent.inverse_chem != null)
                {
                    purity = Dm.num(target.get_inverse_purity());
                    target = glob_entry(reagent.inverse_chem);
                    is_inverse = TRUE;
                }
                else
                {
                    float initial_purity = Dm.initial(reagent, (datum_reagent r) => r.purity);
                    if ((initial_purity - reagent.purity) <= 0) //already at max purity
                        log_text = Dm.text("Cannot purify above []%", round(initial_purity * 100));
                }

                Dm.append(beakerContents, Dm.list(Dm.list(
                    Dm.kv("name", target.name),
                    Dm.kv("volume", round(Dm.num(reagent.volume), CHEMICAL_VOLUME_ROUNDING)),
                    Dm.kv("mass", target.mass),
                    Dm.kv("purity", round(purity * 100)),
                    Dm.kv("type", Dm.truthy(is_inverse) ? "Inverted" : "Clean"),
                    Dm.kv("log", log_text))));
                dot["peakHeight"] = Dm.max(Dm.num(dot["peakHeight"]), Dm.num(reagent.volume));
            }
            beaker1Data["contents"] = beakerContents;
        }
        dot["beaker1"] = beaker1Data;

        //+10 because of the range on the peak
        dot["graphUpperRange"] = calculate_mass(smallest: FALSE);

        //output reagents
        DmList beaker2Data = null;
        if (!QDELETED(beaker2))
        {
            beaker2Data = Dm.list();
            datum_reagents beaker_2_reagents = beaker2.reagents as datum_reagents;
            beaker2Data["currentVolume"] = beaker_2_reagents.total_volume;
            beaker2Data["maxVolume"] = beaker_2_reagents.maximum_volume;
            DmList beakerContents = Dm.list();
            foreach (datum_reagent reagent in Dm.each_cast<datum_reagent>(beaker_2_reagents.reagent_list))
            {
                Dm.append(beakerContents, Dm.list(Dm.list(
                    Dm.kv("name", reagent.name),
                    Dm.kv("volume", round(Dm.num(reagent.volume), CHEMICAL_VOLUME_ROUNDING)),
                    Dm.kv("mass", reagent.mass),
                    Dm.kv("purity", round(reagent.purity * 100)),
                    Dm.kv("type", "Clean"),
                    Dm.kv("log", log[reagent.type]))));
            }
            beaker2Data["contents"] = beakerContents;
        }
        dot["beaker2"] = beaker2Data;
        await DmTask.CompletedTask;
        return dot;
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_mass_spec.dm:354
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot) || Dm.truthy(processing_reagents))
            return dot;

        switch (action)
        {
            case "activate":
                if (QDELETED(beaker1))
                {
                    await say("Missing input beaker!");
                    return null;
                }
                if (QDELETED(beaker2))
                {
                    await say("Missing output beaker!");
                    return null;
                }

                //adjust timer for purification
                progress_time = 0;
                estimate_time();
                if (delay_time <= 0)
                {
                    await say("No work to be done!");
                    return null;
                }

                //start the purification process
                processing_reagents = TRUE;
                begin_processing();
                update_appearance();

                return TRUE;

            case "leftSlider":
            {
                object value = @params["value"];
                if (value == null)
                    return null;

                float? number = text2num(value);
                if (number == null)
                    return null;

                lower_mass_range = Dm.clamp(number.Value, Dm.num(calculate_mass(smallest: TRUE)), (lower_mass_range + upper_mass_range) / 2);
                estimate_time();
                return TRUE;
            }

            case "rightSlider":
            {
                object value = @params["value"];
                if (value == null)
                    return null;

                float? number = text2num(value);
                if (number == null)
                    return null;

                upper_mass_range = Dm.clamp(number.Value, (lower_mass_range + upper_mass_range) / 2, Dm.num(calculate_mass(smallest: FALSE)));
                estimate_time();
                return TRUE;
            }

            case "centerSlider":
            {
                object value = @params["value"];
                if (value == null)
                    return null;

                float? number = text2num(value);
                if (number == null)
                    return null;

                float delta_center = ((lower_mass_range + upper_mass_range) / 2) - Dm.num(@params["value"]);
                float lowest = Dm.num(calculate_mass(smallest: TRUE));
                float highest = Dm.num(calculate_mass(smallest: FALSE));
                lower_mass_range = Dm.clamp(lower_mass_range - delta_center, lowest, highest);
                upper_mass_range = Dm.clamp(upper_mass_range - delta_center, lowest, highest);
                estimate_time();

                return TRUE;
            }

            case "eject1":
                await replace_beaker(ui.user as mob_living, TRUE);
                return TRUE;

            case "eject2":
                await replace_beaker(ui.user as mob_living, FALSE);
                return TRUE;

            case "insert1":
            {
                obj_item_reagent_containers container = ui.user.get_active_held_item() as obj_item_reagent_containers;
                if (container != null && Dm.truthy(await container.can_insert_container(ui.user as mob_living, this)))
                    await replace_beaker(ui.user as mob_living, TRUE, container);

                return TRUE;
            }

            case "insert2":
            {
                obj_item_reagent_containers container = ui.user.get_active_held_item() as obj_item_reagent_containers;
                if (container != null && Dm.truthy(await container.can_insert_container(ui.user as mob_living, this)))
                    await replace_beaker(ui.user as mob_living, FALSE, container);

                return TRUE;
            }
        }
        return dot;
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_mass_spec.dm:448
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        if (Dm.truthy(processing_reagents))
        {
            balloon_alert(user, "still processing!");
            return CLICK_ACTION_BLOCKING;
        }
        await replace_beaker(user as mob_living, TRUE);
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_mass_spec.dm:455
    public override async DmTask<object> click_alt_secondary(mob user = null)
    {
        if (Dm.truthy(processing_reagents))
        {
            balloon_alert(user, "still processing!");
            return null;
        }
        await replace_beaker(user as mob_living, FALSE);
        return null;
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_mass_spec.dm:461
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (!Dm.truthy(processing_reagents))
            return PROCESS_KILL;

        if (!Dm.truthy(is_operational) || Dm.truthy(panel_open) || !Dm.truthy(anchored))
            return null;

        progress_time += Dm.num(seconds_per_tick);
        if (progress_time >= delay_time)
        {
            processing_reagents = FALSE;
            progress_time = 0;

            log.Cut();
            datum_reagents input_reagents = beaker1.reagents as datum_reagents;
            datum_reagents output_reagents = beaker2.reagents as datum_reagents;
            foreach (datum_reagent reagent in Dm.each_cast<datum_reagent>(input_reagents.reagent_list))
            {
                object product_vol = reagent.volume;

                //purify inverted chems to their inverted purity
                if (reagent.inverse_chem != null && Dm.num(reagent.inverse_chem_val) > reagent.purity)
                {
                    datum_reagent inverse_reagent = glob_entry(reagent.inverse_chem);
                    if (Dm.num(inverse_reagent.mass) < lower_mass_range || Dm.num(inverse_reagent.mass) > upper_mass_range)
                        continue;
                    Waiting.NoSleep(input_reagents.remove_reagent(reagent.type, product_vol));
                    Waiting.NoSleep(output_reagents.add_reagent(reagent.inverse_chem, product_vol, reagtemp: input_reagents.chem_temp, added_purity: reagent.get_inverse_purity(), added_ph: reagent.ph));
                    log[reagent.type] = Dm.text("Purified to []%", Dm.num(reagent.get_inverse_purity()) * 100);
                    continue;
                }

                //out of our selected range
                if (Dm.num(reagent.mass) < lower_mass_range || Dm.num(reagent.mass) > upper_mass_range)
                    continue;

                //already at max purity
                float delta_purity = Dm.initial(reagent, (datum_reagent r) => r.purity) - reagent.purity;
                if (delta_purity <= 0)
                    continue;

                //add the purified reagent. More impure reagents will yield smaller amounts
                Waiting.NoSleep(input_reagents.remove_reagent(reagent.type, product_vol));
                Waiting.NoSleep(output_reagents.add_reagent(reagent.type, Dm.num(product_vol) * (1 - delta_purity), reagtemp: input_reagents.chem_temp, added_purity: Dm.initial(reagent, (datum_reagent r) => r.purity), added_ph: reagent.ph));
                log[reagent.type] = Dm.text("Purified to []%", Dm.initial(reagent, (datum_reagent r) => r.purity) * 100);
            }

            //recompute everything
            lower_mass_range = Dm.num(calculate_mass(smallest: TRUE));
            upper_mass_range = Dm.num(calculate_mass(smallest: FALSE));
            estimate_time();
            update_appearance();
            return PROCESS_KILL;
        }

        Waiting.NoSleep(use_energy(active_power_usage * Dm.num(seconds_per_tick)));
        return null;
    }
}
