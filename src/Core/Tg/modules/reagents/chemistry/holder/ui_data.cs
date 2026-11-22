// Translated from tgstation (snapshot 1d99bd7f): code/modules/reagents/chemistry/holder/ui_data.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps P5): the holder's Reagents window, the reaction search the chem dispenser opens on its beaker. GLOB's
// chemical_reagents_list is not built (holder.cs's GAP): an entry is its type's prototype, read only for vars tg never changes on
// it (names, colours, descriptions, pH). GLOB.addictions is built since gaps RG2 (D339).

// tg: code/modules/reagents/chemistry/holder/ui_data.dm:1
public partial class datum_reagents : datum
{
    /// The reagent type's prototype for GLOB.chemical_reagents_list[path] (the holder's GAP: "no entry" is the list's own test)
    private static datum_reagent glob_reagent_entry(object path)
    {
        Type reagent_path = path as Type;
        return ispath(reagent_path, typeof(datum_reagent)) && reagent_path != typeof(datum_reagent) && !Dm.@in(reagent_path, get_abstract_types())
            ? Dm.initial(reagent_path, (datum_reagent p) => p) : null;
    }

    // tg: code/modules/reagents/chemistry/holder/ui_data.dm:1
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "Reagents", "Reaction search");
            ui.status = UI_INTERACTIVE; //How do I prevent a UI from autoclosing if not in LoS
            ui_tags_selected = NONE; //Resync with gui on open (gui expects no flags)
            ui_reagent_id = null;
            ui_reaction_id = null;
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/reagents/chemistry/holder/ui_data.dm:12
    public override async DmTask<object> ui_status(mob user = null, datum_ui_state state = null)
    {
        await DmTask.CompletedTask;
        return UI_INTERACTIVE; //please advise
    }

    // tg: code/modules/reagents/chemistry/holder/ui_data.dm:15
    public override object ui_state(mob user = null)
    {
        return GLOB.physical_state;
    }

    ///Generates a (rough) rate vs temperature graph profile
    // tg: code/modules/reagents/chemistry/holder/ui_data.dm:19
    public virtual object generate_thermodynamic_profile(datum_chemical_reaction reaction = null)
    {
        DmList coords = Dm.list();
        float x_temp;
        float increment;
        float required_temp = Dm.num(reaction.required_temp), optimal_temp = Dm.num(reaction.optimal_temp), temp_exponent_factor = Dm.num(reaction.temp_exponent_factor);
        if (Dm.truthy(reaction.is_cold_recipe))
        {
            Dm.append(coords, Dm.list(Dm.list(0, 0)));
            Dm.append(coords, Dm.list(Dm.list(reaction.required_temp, 0)));
            x_temp = required_temp;
            increment = (optimal_temp - required_temp) / 10;
            while (x_temp < optimal_temp)
            {
                float y = Dm.pow(x_temp - required_temp, temp_exponent_factor) / Dm.pow(optimal_temp - required_temp, temp_exponent_factor);
                Dm.append(coords, Dm.list(Dm.list(x_temp, y)));
                x_temp += increment;
            }
        }
        else
        {
            Dm.append(coords, Dm.list(Dm.list(reaction.required_temp, 0)));
            x_temp = required_temp;
            increment = (required_temp - optimal_temp) / 10;
            while (x_temp > optimal_temp)
            {
                float y = Dm.pow(x_temp - required_temp, temp_exponent_factor) / Dm.pow(optimal_temp - required_temp, temp_exponent_factor);
                Dm.append(coords, Dm.list(Dm.list(x_temp, y)));
                x_temp -= increment;
            }
        }

        Dm.append(coords, Dm.list(Dm.list(reaction.optimal_temp, 1)));
        if (Dm.num(reaction.overheat_temp) == NO_OVERHEAT)
        {
            if (Dm.truthy(reaction.is_cold_recipe))
                Dm.append(coords, Dm.list(Dm.list(optimal_temp + 10, 1)));
            else
                Dm.append(coords, Dm.list(Dm.list(optimal_temp - 10, 1)));
            return coords;
        }
        Dm.append(coords, Dm.list(Dm.list(reaction.overheat_temp, 1)));
        Dm.append(coords, Dm.list(Dm.list(reaction.overheat_temp, 0)));
        return coords;
    }

    // tg: code/modules/reagents/chemistry/holder/ui_data.dm:52
    public virtual object generate_explosive_profile(datum_chemical_reaction reaction = null)
    {
        if (Dm.num(reaction.overheat_temp) == NO_OVERHEAT)
            return null;
        float overheat_temp = Dm.num(reaction.overheat_temp);
        DmList coords = Dm.list();
        Dm.append(coords, Dm.list(Dm.list(reaction.overheat_temp, 0)));
        Dm.append(coords, Dm.list(Dm.list(reaction.overheat_temp, 1)));
        if (Dm.truthy(reaction.is_cold_recipe))
        {
            Dm.append(coords, Dm.list(Dm.list(overheat_temp - 50, 1)));
            Dm.append(coords, Dm.list(Dm.list(overheat_temp - 50, 0)));
        }
        else
        {
            Dm.append(coords, Dm.list(Dm.list(overheat_temp + 50, 1)));
            Dm.append(coords, Dm.list(Dm.list(overheat_temp + 50, 0)));
        }
        return coords;
    }

    ///Returns a string descriptor of a reactions themic_constant
    // tg: code/modules/reagents/chemistry/holder/ui_data.dm:68
    public virtual object determine_reaction_thermics(datum_chemical_reaction reaction = null)
    {
        float thermic = Dm.num(reaction.thermic_constant);
        if (Dm.truthy(Dm.bitand(reaction.reaction_flags, REACTION_HEAT_ARBITARY)))
            thermic *= 100; //Because arbitary is a lower scale
        // DM's switch takes the first range that holds the value, so 0 itself is "Weakly endothermic" (tg's order)
        if (thermic <= -1500)
            return "Overwhelmingly endothermic";
        if (thermic <= -1000)
            return "Extremely endothermic";
        if (thermic <= -500)
            return "Strongly endothermic";
        if (thermic <= -200)
            return "Moderately endothermic";
        if (thermic <= -50)
            return "Endothermic";
        if (thermic <= 0)
            return "Weakly endothermic";
        if (thermic <= 50)
            return "Weakly Exothermic";
        if (thermic <= 200)
            return "Exothermic";
        if (thermic <= 500)
            return "Moderately exothermic";
        if (thermic <= 1000)
            return "Strongly exothermic";
        if (thermic <= 1500)
            return "Extremely exothermic";
        return "Overwhelmingly exothermic";
    }

    // tg: code/modules/reagents/chemistry/holder/ui_data.dm:100
    public virtual object parse_addictions(datum_reagent reagent = null)
    {
        DmList addict_text = Dm.list();
        foreach (object entry in Dm.each(reagent.addiction_types))
        {
            float threshold = Dm.num(reagent.addiction_types[entry]);
            object addiction_name = ((datum_addiction)GLOB.addictions[entry]).name;
            if (threshold >= 0 && threshold <= 10)
                Dm.append(addict_text, Dm.text("Potent []", addiction_name));
            else if (threshold >= 10 && threshold <= 30)
                Dm.append(addict_text, Dm.text("Strong []", addiction_name));
            else if (threshold >= 30 && threshold <= 100)
                Dm.append(addict_text, Dm.text("[]", addiction_name));
            else if (threshold >= 100)
                Dm.append(addict_text, Dm.text("Weak []", addiction_name));
        }

        return addict_text;
    }

    // tg: code/modules/reagents/chemistry/holder/ui_data.dm:115
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["selectedBitflags"] = ui_tags_selected;

        data["currentReagents"] = Dm.list();
        foreach (datum_reagent target in Dm.each_cast<datum_reagent>(reagent_list))
            Dm.append((DmList)data["currentReagents"], target.type);
        data["beakerSync"] = ui_beaker_sync;
        data["linkedBeaker"] = my_atom.name; //To solidify the fact that the UI is linked to a beaker - not a machine.

        //First we check to see if reactions are synced with the beaker
        if (Dm.truthy(ui_beaker_sync))
        {
            if (Dm.truthy(reaction_list)) //But we don't want to null the previously displayed if there are none
            {
                //makes sure we're within bounds
                if (ui_reaction_index > reaction_list.len)
                    ui_reaction_index = reaction_list.len;
                ui_reaction_id = (reaction_list[(int)ui_reaction_index] as datum_equilibrium).reaction.type;
            }
        }

        //reagent lookup data
        if (Dm.truthy(ui_reagent_id))
        {
            datum_reagent reagent = glob_reagent_entry(ui_reagent_id);
            if (reagent == null)
            {
                to_chat(user, "Could not find reagent!");
                ui_reagent_id = null;
            }
            else
            {
                DmList mode_reagent = Dm.list(Dm.kv("name", reagent.name), Dm.kv("id", reagent.type), Dm.kv("desc", reagent.description), Dm.kv("reagentCol", reagent.color), Dm.kv("pH", reagent.ph), Dm.kv("pHCol", convert_ph_to_readable_color(reagent.ph)), Dm.kv("metaRate", reagent.metabolization_rate), Dm.kv("OD", reagent.overdose_threshold));
                data["reagent_mode_reagent"] = mode_reagent;
                mode_reagent["addictions"] = Dm.list();
                mode_reagent["addictions"] = parse_addictions(reagent);

                datum_reagent inverse_reagent = glob_reagent_entry(reagent.inverse_chem);
                if (inverse_reagent != null)
                    Dm.append(mode_reagent, Dm.list(Dm.kv("inverseReagent", inverse_reagent.name), Dm.kv("inverseId", inverse_reagent.type)));

                if (Dm.truthy(Dm.bitand(reagent.chemical_flags, REAGENT_DEAD_PROCESS)))
                    Dm.append(mode_reagent, Dm.list(Dm.kv("deadProcess", TRUE)));
            }
        }
        else
            data["reagent_mode_reagent"] = null;

        //reaction lookup data
        if (Dm.truthy(ui_reaction_id))
        {
            datum_chemical_reaction reaction = GLOB.chemical_reactions_list[ui_reaction_id] as datum_chemical_reaction;
            if (reaction == null)
            {
                to_chat(user, "Could not find reaction!");
                ui_reaction_id = null;
                return data;
            }
            //Required holder
            object container_name = null;
            if (reaction.required_container != null)
            {
                DmList names = splittext(Dm.text("[]", reaction.required_container), "/");
                container_name = Dm.text("[] []", names[names.len - 1], names[names.len]);
                container_name = replacetext(container_name, "_", " ");
            }

            //Next, find the product
            float has_product = TRUE;
            DmList mode_recipe;
            //If we have no product, use the typepath to create a name for it
            if (!Dm.truthy(length(reaction.results)))
            {
                has_product = FALSE;
                DmList names = splittext(Dm.text("[]", reaction.type), "/");
                object product_name = names[names.len];
                mode_recipe = Dm.list(Dm.kv("name", product_name), Dm.kv("id", reaction.type), Dm.kv("hasProduct", has_product), Dm.kv("reagentCol", COLOR_WHITE), Dm.kv("thermodynamics", generate_thermodynamic_profile(reaction)), Dm.kv("explosive", generate_explosive_profile(reaction)), Dm.kv("lowerpH", reaction.optimal_ph_min), Dm.kv("upperpH", reaction.optimal_ph_max), Dm.kv("thermics", determine_reaction_thermics(reaction)), Dm.kv("thermoUpper", reaction.rate_up_lim), Dm.kv("minPurity", reaction.purity_min), Dm.kv("inversePurity", "N/A"), Dm.kv("tempMin", reaction.required_temp), Dm.kv("explodeTemp", reaction.overheat_temp), Dm.kv("reqContainer", container_name), Dm.kv("subReactLen", 1), Dm.kv("subReactIndex", 1));
            }
            //If we do have a product then we find it
            else
            {
                //Find out if we have multiple reactions for the same product
                datum_reagent primary_reagent = glob_reagent_entry(reaction.results[1]); //We use the first product - though it might be worth changing this
                //If we're syncing from the beaker
                DmList sub_reactions = Dm.list();
                if (Dm.truthy(ui_beaker_sync) && Dm.truthy(reaction_list))
                {
                    foreach (object _ongoing_eq in Dm.each(reaction_list))
                    {
                        datum_equilibrium ongoing_eq = _ongoing_eq as datum_equilibrium;
                        object ongoing_r = ongoing_eq.reaction;
                        Dm.append(sub_reactions, ongoing_r);
                    }
                }
                else
                    sub_reactions = GLOB.chemical_reactions_list_product_index[primary_reagent.type] as DmList;
                object sub_reaction_length = length(sub_reactions);
                float i = 1;
                foreach (datum_chemical_reaction sub_reaction in Dm.each<datum_chemical_reaction>(sub_reactions))
                {
                    if (sub_reaction.type == reaction.type)
                    {
                        ui_reaction_index = i; //update our index
                        break;
                    }
                    i += 1;
                }
                mode_recipe = Dm.list(Dm.kv("name", primary_reagent.name), Dm.kv("id", reaction.type), Dm.kv("hasProduct", has_product), Dm.kv("reagentCol", primary_reagent.color), Dm.kv("thermodynamics", generate_thermodynamic_profile(reaction)), Dm.kv("explosive", generate_explosive_profile(reaction)), Dm.kv("lowerpH", reaction.optimal_ph_min), Dm.kv("upperpH", reaction.optimal_ph_max), Dm.kv("thermics", determine_reaction_thermics(reaction)), Dm.kv("thermoUpper", reaction.rate_up_lim), Dm.kv("minPurity", reaction.purity_min), Dm.kv("inversePurity", primary_reagent.inverse_chem_val), Dm.kv("tempMin", reaction.required_temp), Dm.kv("explodeTemp", reaction.overheat_temp), Dm.kv("reqContainer", container_name), Dm.kv("subReactLen", sub_reaction_length), Dm.kv("subReactIndex", ui_reaction_index));
            }
            data["reagent_mode_recipe"] = mode_recipe;

            //Results sweep
            object has_reagent_col = "default";
            foreach (Type reagent in Dm.each_cast<Type>(reaction.results))
            {
                if (Dm.truthy(has_reagent(reagent)))
                    has_reagent_col = "green";
                mode_recipe["products"] = Dm.add(mode_recipe["products"] ?? Dm.list(), Dm.list(Dm.list(Dm.kv("name", Dm.initial(reagent, (datum_reagent r) => r.name)), Dm.kv("id", reagent), Dm.kv("ratio", reaction.results[reagent]), Dm.kv("hasReagentCol", has_reagent_col))));
            }

            //Reactant sweep
            foreach (Type reagent in Dm.each_cast<Type>(reaction.required_reagents))
            {
                object color_r = "default"; //If the holder is missing the reagent, it's displayed in orange
                if (Dm.truthy(has_reagent(reagent)))
                    color_r = "green"; //It's green if it's present
                object tooltip = null;
                float tooltip_bool = FALSE;
                DmList sub_reactions = GLOB.chemical_reactions_list_product_index[reagent] as DmList;
                //Get sub reaction possibilities, but ignore ones that need a specific holder atom
                int sub_index = 0;
                foreach (datum_chemical_reaction sub_reaction in Dm.each_cast<datum_chemical_reaction>(sub_reactions))
                {
                    if (sub_reaction.required_container != null) //So we don't have slime reactions confusing things
                    {
                        sub_index++;
                        continue;
                    }
                    sub_index++;
                    break;
                }
                if (sub_index != 0)
                {
                    datum_chemical_reaction sub_reaction = sub_reactions[sub_index] as datum_chemical_reaction;
                    //Subreactions sweep (if any)
                    foreach (Type sub_reagent in Dm.each_cast<Type>(sub_reaction.required_reagents))
                    {
                        tooltip = Dm.add(tooltip, Dm.text("[]u []\n", sub_reaction.required_reagents[sub_reagent], Dm.initial(sub_reagent, (datum_reagent r) => r.name)));
                        tooltip_bool = TRUE;
                    }
                }
                mode_recipe["reactants"] = Dm.add(mode_recipe["reactants"] ?? Dm.list(), Dm.list(Dm.list(Dm.kv("name", Dm.initial(reagent, (datum_reagent r) => r.name)), Dm.kv("id", reagent), Dm.kv("ratio", reaction.required_reagents[reagent]), Dm.kv("color", color_r), Dm.kv("tooltipBool", tooltip_bool), Dm.kv("tooltip", tooltip))));
            }

            //Catalyst sweep
            foreach (Type reagent in Dm.each_cast<Type>(reaction.required_catalysts))
            {
                object color_r = "default";
                if (Dm.truthy(has_reagent(reagent)))
                    color_r = "green";
                object tooltip = null;
                float tooltip_bool = FALSE;
                DmList sub_reactions = GLOB.chemical_reactions_list_product_index[reagent] as DmList;
                if (Dm.truthy(length(sub_reactions)))
                {
                    datum_chemical_reaction sub_reaction = sub_reactions[1] as datum_chemical_reaction;
                    //Subreactions sweep (if any)
                    foreach (Type sub_reagent in Dm.each_cast<Type>(sub_reaction.required_reagents))
                    {
                        tooltip = Dm.add(tooltip, Dm.text("[]u []\n", sub_reaction.required_reagents[sub_reagent], Dm.initial(sub_reagent, (datum_reagent r) => r.name)));
                        tooltip_bool = TRUE;
                    }
                }
                mode_recipe["catalysts"] = Dm.add(mode_recipe["catalysts"] ?? Dm.list(), Dm.list(Dm.list(Dm.kv("name", Dm.initial(reagent, (datum_reagent r) => r.name)), Dm.kv("id", reagent), Dm.kv("ratio", reaction.required_catalysts[reagent]), Dm.kv("color", color_r), Dm.kv("tooltipBool", tooltip_bool), Dm.kv("tooltip", tooltip))));
            }
            mode_recipe["isColdRecipe"] = reaction.is_cold_recipe;
        }
        else
            data["reagent_mode_recipe"] = null;

        await DmTask.CompletedTask;
        return data;
    }

    // tg: code/modules/reagents/chemistry/holder/ui_data.dm:250
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();
        //Use GLOB list - saves processing
        data["master_reaction_list"] = GLOB.chemical_reactions_results_lookup_list;
        data["bitflags"] = Dm.list();
        DmList readable = REACTION_TAG_READABLE;
        foreach (object readable_flag in Dm.each(readable))
            ((DmList)data["bitflags"])[readable_flag] = readable[readable_flag];

        await DmTask.CompletedTask;
        return data;
    }

    /* Returns a reaction type by index from an input reagent type
    * i.e. the input reagent's associated reactions are found, and the index determines which one to return
    * If the index is out of range, it is set to 1
    */
    // tg: code/modules/reagents/chemistry/holder/ui_data.dm:264
    public virtual object get_reaction_from_indexed_possibilities(object path = null, object index = null)
    {
        if (Dm.truthy(index))
            ui_reaction_index = Dm.num(index);
        DmList sub_reactions = GLOB.chemical_reactions_list_product_index[path] as DmList;
        if (!Dm.truthy(length(sub_reactions)))
        {
            to_chat(usr, "There is no recipe associated with this product.");
            return FALSE;
        }
        if (ui_reaction_index > sub_reactions.len)
            ui_reaction_index = 1;
        datum_chemical_reaction reaction = sub_reactions[(int)ui_reaction_index] as datum_chemical_reaction;
        return reaction.type;
    }

    // tg: code/modules/reagents/chemistry/holder/ui_data.dm:276
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        switch (action)
        {
            case "find_reagent_reaction":
                ui_reaction_id = get_reaction_from_indexed_possibilities(text2path(@params["id"] as string));
                return TRUE;
            case "reagent_click":
                ui_reagent_id = text2path(@params["id"] as string);
                return TRUE;
            case "recipe_click":
                ui_reaction_id = text2path(@params["id"] as string);
                return TRUE;
            case "search_reagents":
            {
                object input_reagent = await tgui_input_list(usr, "Select reagent", "Reagent", GLOB.name2reagent);
                input_reagent = get_reagent_type_from_product_string(input_reagent); //from string to type
                datum_reagent reagent = glob_reagent_entry(input_reagent);
                if (reagent == null)
                {
                    to_chat(usr, "Could not find reagent!");
                    return FALSE;
                }
                ui_reagent_id = reagent.type;
                return TRUE;
            }
            case "search_recipe":
            {
                // DEVIATION(DV217): input("Enter the name of product reagent", "Input") as text|null is tgui's text prompt
                object input_reagent = await tgui_input_text(usr, "Enter the name of product reagent", "Input");
                input_reagent = get_reagent_type_from_product_string(input_reagent); //from string to type
                datum_reagent reagent = glob_reagent_entry(input_reagent);
                if (reagent == null)
                {
                    to_chat(usr, "Could not find product reagent!");
                    return null;
                }
                ui_reaction_id = get_reaction_from_indexed_possibilities(reagent.type);
                return TRUE;
            }
            case "increment_index":
                ui_reaction_index += 1;
                if (!Dm.truthy(ui_beaker_sync) || !Dm.truthy(reaction_list))
                    ui_reaction_id = get_reaction_from_indexed_possibilities(get_reagent_type_from_product_string(@params["id"]));
                return TRUE;
            case "reduce_index":
                if (ui_reaction_index == 1)
                    return null;
                ui_reaction_index -= 1;
                if (!Dm.truthy(ui_beaker_sync) || !Dm.truthy(reaction_list))
                    ui_reaction_id = get_reaction_from_indexed_possibilities(get_reagent_type_from_product_string(@params["id"]));
                return TRUE;
            case "beaker_sync":
                ui_beaker_sync = Dm.truthy(ui_beaker_sync) ? FALSE : TRUE;
                return TRUE;
            case "toggle_tag_brute":
                ui_tags_selected = Dm.bitxor(ui_tags_selected, REACTION_TAG_BRUTE);
                return TRUE;
            case "toggle_tag_burn":
                ui_tags_selected = Dm.bitxor(ui_tags_selected, REACTION_TAG_BURN);
                return TRUE;
            case "toggle_tag_toxin":
                ui_tags_selected = Dm.bitxor(ui_tags_selected, REACTION_TAG_TOXIN);
                return TRUE;
            case "toggle_tag_oxy":
                ui_tags_selected = Dm.bitxor(ui_tags_selected, REACTION_TAG_OXY);
                return TRUE;
            case "toggle_tag_healing":
                ui_tags_selected = Dm.bitxor(ui_tags_selected, REACTION_TAG_HEALING);
                return TRUE;
            case "toggle_tag_damaging":
                ui_tags_selected = Dm.bitxor(ui_tags_selected, REACTION_TAG_DAMAGING);
                return TRUE;
            case "toggle_tag_explosive":
                ui_tags_selected = Dm.bitxor(ui_tags_selected, REACTION_TAG_EXPLOSIVE);
                return TRUE;
            case "toggle_tag_other":
                ui_tags_selected = Dm.bitxor(ui_tags_selected, REACTION_TAG_OTHER);
                return TRUE;
            case "toggle_tag_easy":
                ui_tags_selected = Dm.bitxor(ui_tags_selected, REACTION_TAG_EASY);
                return TRUE;
            case "toggle_tag_moderate":
                ui_tags_selected = Dm.bitxor(ui_tags_selected, REACTION_TAG_MODERATE);
                return TRUE;
            case "toggle_tag_hard":
                ui_tags_selected = Dm.bitxor(ui_tags_selected, REACTION_TAG_HARD);
                return TRUE;
            case "toggle_tag_organ":
                ui_tags_selected = Dm.bitxor(ui_tags_selected, REACTION_TAG_ORGAN);
                return TRUE;
            case "toggle_tag_drink":
                ui_tags_selected = Dm.bitxor(ui_tags_selected, REACTION_TAG_DRINK);
                return TRUE;
            case "toggle_tag_food":
                ui_tags_selected = Dm.bitxor(ui_tags_selected, REACTION_TAG_FOOD);
                return TRUE;
            case "toggle_tag_dangerous":
                ui_tags_selected = Dm.bitxor(ui_tags_selected, REACTION_TAG_DANGEROUS);
                return TRUE;
            case "toggle_tag_slime":
                ui_tags_selected = Dm.bitxor(ui_tags_selected, REACTION_TAG_SLIME);
                return TRUE;
            case "toggle_tag_drug":
                ui_tags_selected = Dm.bitxor(ui_tags_selected, REACTION_TAG_DRUG);
                return TRUE;
            case "toggle_tag_unique":
                ui_tags_selected = Dm.bitxor(ui_tags_selected, REACTION_TAG_UNIQUE);
                return TRUE;
            case "toggle_tag_chemical":
                ui_tags_selected = Dm.bitxor(ui_tags_selected, REACTION_TAG_CHEMICAL);
                return TRUE;
            case "toggle_tag_plant":
                ui_tags_selected = Dm.bitxor(ui_tags_selected, REACTION_TAG_PLANT);
                return TRUE;
            case "toggle_tag_competitive":
                ui_tags_selected = Dm.bitxor(ui_tags_selected, REACTION_TAG_COMPETITIVE);
                return TRUE;
            case "toggle_tag_component":
                ui_tags_selected = Dm.bitxor(ui_tags_selected, REACTION_TAG_COMPONENT);
                return TRUE;
            case "toggle_tag_active":
                ui_tags_selected = Dm.bitxor(ui_tags_selected, REACTION_TAG_ACTIVE);
                return TRUE;

            case "update_ui":
                return TRUE;
        }
        return dot;
    }
}
