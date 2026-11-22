// Translated from tgstation (snapshot 1d99bd7f): code/modules/reagents/chemistry/machinery/chem_heater.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps P5): the reaction chamber with its acid and base buffers and the ChemHeater window, the debug chamber and
// the mapped one with buffers. Its screentips (add_context) are a live-partial row (D264). The var block is generated.

// tg: code/modules/reagents/chemistry/machinery/chem_heater.dm:1
public partial class obj_machinery_chem_heater : obj_machinery
{
    // tg: code/modules/reagents/chemistry/machinery/chem_heater.dm:23
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        create_reagents(200, NO_REACT);
        register_context();
        return dot;
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_heater.dm:28
    public override object Destroy(float? force = null)
    {
        if (beaker != null)
        {
            UnregisterSignal(beaker.reagents as datum, COMSIG_REAGENTS_REACTION_STEP);
            QDEL_NULL(ref beaker);
        }
        return base.Destroy(force);
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_heater.dm:34
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (gone == beaker)
        {
            UnregisterSignal(beaker.reagents as datum, COMSIG_REAGENTS_REACTION_STEP);
            beaker = null;
            update_appearance();
        }
        return dot;
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_heater.dm:65
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (Dm.truthy(in_range(user, this)) || isobserver(user))
        {
            Dm.append(dot, span_notice(I18n.T("The status display reads: Heating reagents at <b>[]%</b> speed.", heater_coefficient * 1000)));
            if (!QDELETED(beaker))
            {
                datum_reagents beaker_reagents = beaker.reagents as datum_reagents;
                Dm.append(dot, span_notice(I18n.T("It has a beaker of [] units capacity.", beaker_reagents.total_volume)));
                if (Dm.truthy(beaker_reagents.is_reacting))
                    Dm.append(dot, span_notice(I18n.T("Its contents are currently reacting.")));
            }
            else
                Dm.append(dot, span_warning(I18n.T("There is no beaker inserted.")));
            Dm.append(dot, span_notice(I18n.T("Its heating is turned [].", Dm.truthy(on) ? "On" : "Off")));
            Dm.append(dot, span_notice(I18n.T("The status display reads: Heating reagents at <b>[]%</b> speed.", heater_coefficient * 1000)));
            if (Dm.truthy(panel_open))
                Dm.append(dot, span_notice(I18n.T("Its panel is open and can now be [] apart.", EXAMINE_HINT("pried"))));
            else
                Dm.append(dot, span_notice(I18n.T("Its panel can be [] open", EXAMINE_HINT("pried"))));
        }
        return dot;
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_heater.dm:82
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("[][]b", base_icon_state, beaker != null && !Dm.truthy(panel_open) ? 1 : 0);
        return await base.update_icon_state(__extra);
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_heater.dm:86
    public override async DmTask<object> RefreshParts()
    {
        object dot = await base.RefreshParts();
        heater_coefficient = 0.1f;
        foreach (datum_stock_part_micro_laser micro_laser in Dm.each<datum_stock_part_micro_laser>(component_parts))
            heater_coefficient *= Dm.num(micro_laser.tier);
        return dot;
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_heater.dm:92
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        obj_item held_item = tool;
        if (!QDELETED(beaker))
        {
            if (istype(held_item, typeof(obj_item_reagent_containers_dropper)) || istype(held_item, typeof(obj_item_reagent_containers_syringe)))
            {
                obj_item_reagent_containers injector = held_item as obj_item_reagent_containers;
                await injector.interact_with_atom(beaker, user, modifiers);
                return ITEM_INTERACT_SUCCESS;
            }
        }

        if (!Dm.truthy(await held_item.can_insert_container(user, this)))
            return NONE;

        if (!Dm.truthy(await replace_beaker(user, held_item as obj_item_reagent_containers)))
            return ITEM_INTERACT_BLOCKING;

        await ui_interact(user);
        balloon_alert(user, "beaker added");

        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_heater.dm:110
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.eq(await default_unfasten_wrench(user, tool), SUCCESSFUL_UNFASTEN))
            return ITEM_INTERACT_SUCCESS;
        return ITEM_INTERACT_BLOCKING;
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_heater.dm:115
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        await DmTask.CompletedTask;
        return default_deconstruction_screwdriver(user, tool);
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_heater.dm:118
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        return await default_deconstruction_crowbar(user, tool);
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_heater.dm:121
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        object dot = await base.attack_hand_secondary(user, modifiers);
        if (Dm.eq(dot, SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN))
            return dot;
        if (!Dm.truthy(await user.can_perform_action(this, Dm.bitor(ALLOW_SILICON_REACH, FORBID_TELEKINESIS_REACH))))
            return dot;
        await replace_beaker(user as mob_living);
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_heater.dm:130  (a new slot: the /atom root, _onclick/cyborg.dm, is not declared)
    public override async DmTask<object> attack_robot_secondary(mob user = null, DmList modifiers = null)
    {
        return await attack_hand_secondary(user, modifiers);
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_heater.dm:133  (a new slot: the /atom root, _onclick/ai.dm, is not declared)
    public override async DmTask<object> attack_ai_secondary(mob user = null, DmList modifiers = null)
    {
        return await attack_hand_secondary(user, modifiers);
    }

    /**
     * Replace or eject the beaker inside this machine. Returns TRUE on success.
     * Arguments
     * * mob/living/user - the player operating this machine
     * * obj/item/reagent_containers/new_beaker - the new beaker to replace the current one if not null else it will just eject
     */
    // tg: code/modules/reagents/chemistry/machinery/chem_heater.dm:142
    public virtual async DmTask<object> replace_beaker(mob_living user = null, obj_item_reagent_containers new_beaker = null)
    {
        if (!QDELETED(beaker))
            await try_put_in_hand(beaker, user);

        if (!QDELETED(new_beaker))
        {
            if (!Dm.truthy(await user.transferItemToLoc(new_beaker, this)))
            {
                update_appearance(UPDATE_ICON_STATE);
                return FALSE;
            }
            beaker = new_beaker;
            RegisterSignal(beaker.reagents as datum, COMSIG_REAGENTS_REACTION_STEP, nameof(on_reaction_step));
        }

        update_appearance(UPDATE_ICON_STATE);

        return TRUE;
    }

    /**
     * Heats the reagents of the currently inserted beaker only if machine is on & beaker has some reagents inside
     * Arguments
     * * seconds_per_tick - passed from process() or from reaction_step()
     */
    // tg: code/modules/reagents/chemistry/machinery/chem_heater.dm:164
    public virtual async DmTask<object> heat_reagents(object seconds_per_tick = null)
    {
        //must be on and beaker must have something inside to heat
        if (!Dm.truthy(on) || !Dm.truthy(is_operational) || QDELETED(beaker) || !Dm.truthy((beaker.reagents as datum_reagents).total_volume))
            return FALSE;

        //heat the beaker and use some power. we want to use only a small amount of power since this proc gets called frequently
        datum_reagents beaker_reagents = beaker.reagents as datum_reagents;
        float energy = (Dm.num(target_temperature) - beaker_reagents.chem_temp) * heater_coefficient * Dm.num(seconds_per_tick) * Dm.num(beaker_reagents.heat_capacity());
        beaker_reagents.adjust_thermal_energy(energy);
        await use_energy(active_power_usage + abs(ROUND_UP(energy) / 120));
        return TRUE;
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_heater.dm:177
    public virtual void on_reaction_step(datum_reagents holder = null, object num_reactions = null, object seconds_per_tick = null)
    {
        //SIGNAL_HANDLER
        //adjust temp
        Waiting.NoSleep(heat_reagents(seconds_per_tick));

        //send updates to ui. faster than SStgui.update_uis
        foreach (datum_tgui ui in Dm.each<datum_tgui>(open_uis))
            Waiting.NoSleep(ui.send_update());
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_heater.dm:187
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        //is_reacting is handled in reaction_step()
        if (QDELETED(beaker) || Dm.truthy((beaker.reagents as datum_reagents).is_reacting))
            return null;

        if (Dm.truthy(Waiting.NoSleep(heat_reagents(seconds_per_tick))))
        {
            //create new reactions after temperature adjust
            Waiting.NoSleep((beaker.reagents as datum_reagents).handle_reactions());
        }

        //send updates to ui. faster than SStgui.update_uis
        foreach (datum_tgui ui in Dm.each<datum_tgui>(open_uis))
            Waiting.NoSleep(ui.send_update());
        return null;
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_heater.dm:200
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "ChemHeater", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_heater.dm:206
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList dot = Dm.list();
        dot["targetTemp"] = target_temperature;
        dot["isActive"] = on;
        dot["upgradeLevel"] = heater_coefficient * 10;
        obj_item held_item = user.get_active_held_item() as obj_item;
        dot["hasBeakerInHand"] = held_item?.is_chem_container() is var held && Dm.truthy(held) ? held : FALSE;

        DmList beaker_data = null;
        float chem_temp = 0;
        if (!QDELETED(beaker))
        {
            datum_reagents r = beaker.reagents as datum_reagents;
            beaker_data = Dm.list();
            beaker_data["maxVolume"] = beaker.volume;
            beaker_data["pH"] = round(r.ph, 0.01f);
            beaker_data["currentVolume"] = r.total_volume;
            DmList beakerContents = Dm.list();
            if (Dm.truthy(length(r.reagent_list)))
            {
                foreach (datum_reagent reagent in Dm.each<datum_reagent>(r.reagent_list))
                    Dm.append(beakerContents, Dm.list(Dm.list(Dm.kv("name", reagent.name), Dm.kv("volume", round(Dm.num(reagent.volume), CHEMICAL_VOLUME_ROUNDING))))); // list in a list because Byond merges the first list...
            }
            beaker_data["contents"] = beakerContents;
            chem_temp = r.chem_temp;
        }
        dot["beaker"] = beaker_data;
        dot["currentTemp"] = chem_temp;

        DmList active_reactions = Dm.list();
        float flashing = DISABLE_FLASHING; //for use with alertAfter - since there is no alertBefore, I set the after to 0 if true, or to the max value if false
        foreach (datum_equilibrium equilibrium in Dm.each_cast<datum_equilibrium>((beaker?.reagents as datum_reagents)?.reaction_list))
        {
            if (!Dm.truthy(equilibrium.reaction.results)) //Incase of no result reactions
                continue;
            datum_reagents beaker_reagents = beaker.reagents as datum_reagents;
            datum_reagent reagent = beaker_reagents.has_reagent(equilibrium.reaction.results[1]) as datum_reagent; //Reactions are named after their primary products
            if (reagent == null)
                continue;

            //check for danger levels primirarly overheating
            float overheat = FALSE;
            float danger = FALSE;
            float purity_alert = 2; //same as flashing
            if (reagent.purity < Dm.num(equilibrium.reaction.purity_min))
            {
                purity_alert = ENABLE_FLASHING; //Because 0 is seen as null
                danger = TRUE;
            }
            if (flashing != ENABLE_FLASHING) //So that the pH meter flashes for ANY reactions out of optimal
            {
                if (Dm.num(equilibrium.reaction.optimal_ph_min) > beaker_reagents.ph || Dm.num(equilibrium.reaction.optimal_ph_max) < beaker_reagents.ph)
                    flashing = ENABLE_FLASHING;
            }
            if (Dm.truthy(equilibrium.reaction.is_cold_recipe))
            {
                if (Dm.num(equilibrium.reaction.overheat_temp) > beaker_reagents.chem_temp && Dm.num(equilibrium.reaction.overheat_temp) != NO_OVERHEAT)
                {
                    danger = TRUE;
                    overheat = TRUE;
                }
            }
            else
            {
                if (Dm.num(equilibrium.reaction.overheat_temp) < beaker_reagents.chem_temp)
                {
                    danger = TRUE;
                    overheat = TRUE;
                }
            }

            //create ui data
            Dm.append(active_reactions, Dm.list(Dm.list(
                Dm.kv("name", reagent.name),
                Dm.kv("danger", danger),
                Dm.kv("overheat", overheat),
                Dm.kv("purityAlert", purity_alert),
                Dm.kv("quality", equilibrium.reaction_quality),
                Dm.kv("inverse", reagent.inverse_chem_val),
                Dm.kv("minPure", equilibrium.reaction.purity_min),
                Dm.kv("reactedVol", equilibrium.reacted_vol),
                Dm.kv("targetVol", round(Dm.num(equilibrium.target_vol), 1)))));

            //additional data for competitive reactions
            if (Dm.truthy(Dm.bitand(equilibrium.reaction.reaction_flags, REACTION_COMPETITIVE))) //We have a compeitive reaction - concatenate the results for the different reactions
            {
                foreach (DmList entry in Dm.each_cast<DmList>(active_reactions))
                {
                    if (Dm.eq(entry["name"], reagent.name)) //If we have multiple reaction methods for the same result - combine them
                    {
                        entry["reactedVol"] = equilibrium.reacted_vol;
                        entry["targetVol"] = round(Dm.num(equilibrium.target_vol), 1); //Use the first result reagent to name the reaction detected
                        entry["quality"] = (Dm.num(entry["quality"]) + Dm.num(equilibrium.reaction_quality)) / 2;
                        continue;
                    }
                }
            }
        }
        dot["activeReactions"] = active_reactions;

        dot["isFlashing"] = flashing;
        datum_reagents buffer = reagents as datum_reagents;
        dot["acidicBufferVol"] = buffer.get_reagent_amount(typeof(datum_reagent_reaction_agent_acidic_buffer));
        dot["basicBufferVol"] = buffer.get_reagent_amount(typeof(datum_reagent_reaction_agent_basic_buffer));
        dot["dispenseVolume"] = dispense_volume;
        await DmTask.CompletedTask;
        return dot;
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_heater.dm:288
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        switch (action)
        {
            case "power":
                on = Dm.truthy(on) ? FALSE : TRUE;
                return TRUE;

            case "temperature":
            {
                object target = @params["target"];
                if (target == null)
                    return FALSE;

                float? number = text2num(target);
                if (number == null)
                    return FALSE;

                target_temperature = Dm.clamp(number.Value, 0, 1000);
                return TRUE;
            }

            case "eject":
                //Eject doesn't turn it off, so you can preheat for beaker swapping
                return await replace_beaker(ui.user as mob_living);

            case "insert":
            {
                obj_item_reagent_containers container = ui.user.get_active_held_item() as obj_item_reagent_containers;
                if (container != null && Dm.truthy(await container.can_insert_container(ui.user as mob_living, this)))
                    await replace_beaker(ui.user as mob_living, container);

                return TRUE;
            }

            case "acidBuffer":
            {
                object target = @params["target"];
                if (!Dm.truthy(target))
                    return FALSE;

                float? number = text2num(target);
                if (number == null)
                    return FALSE;

                return await move_buffer(typeof(datum_reagent_reaction_agent_acidic_buffer), number);
            }
            case "basicBuffer":
            {
                object target = @params["target"];
                if (!Dm.truthy(target))
                    return FALSE;

                float? number = text2num(target);
                if (number == null)
                    return FALSE;

                return await move_buffer(typeof(datum_reagent_reaction_agent_basic_buffer), number);
            }
            case "disp_vol":
            {
                object target = @params["target"];
                if (!Dm.truthy(target))
                    return FALSE;

                float? number = text2num(target);
                if (number == null)
                    return FALSE;

                dispense_volume = number.Value;
                return TRUE;
            }
        }
        return dot;
    }

    /**
     * Injects either acid/base buffer into the beaker
     * Arguments
     * * datum/reagent/buffer_type - the type of buffer[acid, base] to inject/withdraw
     * * volume - how much to volume to inject -ve values means withdraw
     */
    // tg: code/modules/reagents/chemistry/machinery/chem_heater.dm:359
    // buffer_type is a Type (Appendix A, gaps P5): both callers pass the buffer's typepath and the body reads initial(buffer_type.name)
    public virtual async DmTask<object> move_buffer(Type buffer_type = null, object volume = null)
    {
        //no beaker
        if (QDELETED(beaker))
        {
            await say("No beaker found!");
            return FALSE;
        }

        datum_reagents buffer = reagents as datum_reagents;
        datum_reagents beaker_reagents = beaker.reagents as datum_reagents;
        //trying to absorb buffer from currently inserted beaker
        if (Dm.num(volume) < 0)
        {
            if (!Dm.truthy(beaker_reagents.has_reagent(buffer_type)))
            {
                object name = Dm.initial(buffer_type, (datum_reagent r) => r.name);
                await say(Dm.text("Unable to find [] in beaker to draw from! Please insert a beaker containing [].", name, name));
                return FALSE;
            }
            await beaker_reagents.trans_to(this, (buffer.maximum_volume / 2) - Dm.num(buffer.get_reagent_amount(buffer_type)), target_id: buffer_type);
            return TRUE;
        }

        //trying to inject buffer into currently inserted beaker
        await buffer.trans_to(beaker, dispense_volume, target_id: buffer_type);
        return TRUE;
    }
}

//Has a lot of buffer and is upgraded
// tg: code/modules/reagents/chemistry/machinery/chem_heater.dm:381
public partial class obj_machinery_chem_heater_debug : obj_machinery_chem_heater
{
    // tg: code/modules/reagents/chemistry/machinery/chem_heater.dm:385
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        datum_reagents buffer = reagents as datum_reagents;
        buffer.maximum_volume = 2000;
        Waiting.NoSleep(buffer.add_reagent(typeof(datum_reagent_reaction_agent_basic_buffer), 1000));
        Waiting.NoSleep(buffer.add_reagent(typeof(datum_reagent_reaction_agent_acidic_buffer), 1000));
        heater_coefficient = 0.4f; //hack way to upgrade
        return dot;
    }
}

//map load types
// tg: code/modules/reagents/chemistry/machinery/chem_heater.dm:393
public partial class obj_machinery_chem_heater_withbuffer : obj_machinery_chem_heater
{
    // tg: code/modules/reagents/chemistry/machinery/chem_heater.dm:396
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep((reagents as datum_reagents).add_reagent(typeof(datum_reagent_reaction_agent_basic_buffer), 20));
        Waiting.NoSleep((reagents as datum_reagents).add_reagent(typeof(datum_reagent_reaction_agent_acidic_buffer), 20));
        return dot;
    }
}
