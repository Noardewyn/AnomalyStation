// Translated from tgstation (snapshot 1d99bd7f): code/modules/atmospherics/machinery/components/gas_recipe_machines/crystallizer.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Engineering E12a (D287): the whole file. The crystallizer draws the selected recipe's gases from its input pipe (airs[2]) into
// its internal mix, exchanges heat with the coolant on its other port (airs[1]), raises the progress and the quality while the
// mix is in the recipe's temperature window, and makes the products south of itself; its tools, pictures and Crystallizer window.
// Deferred leaves: the investigate logs and the admin message of a dangerous product (admin tooling, M10+); add_context is a
// live-partial stub (the screentips, D264). The var block is generated.

// tg: code/modules/atmospherics/machinery/components/gas_recipe_machines/crystallizer.dm:6
public partial class obj_machinery_atmospherics_components_binary_crystallizer : obj_machinery_atmospherics_components_binary
{
    // tg: code/modules/atmospherics/machinery/components/gas_recipe_machines/crystallizer.dm:1-4  (file-local, #undef at :333-336)
    private const float MIN_PROGRESS_AMOUNT = 3;
    private const float MIN_DEVIATION_RATE = 0.90f;
    private const float MAX_DEVIATION_RATE = 1.1f;
    private const float HIGH_CONDUCTIVITY_RATIO = 0.95f;

    // tg: code/modules/atmospherics/machinery/components/gas_recipe_machines/crystallizer.dm:38
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        @internal = Dm.@new<datum_gas_mixture>();
        register_context();
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/gas_recipe_machines/crystallizer.dm:43
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        turf local_turf = get_turf(loc) as turf;
        if (Dm.truthy(@internal.total_moles()))
            await local_turf.assume_air(@internal);
        return await base.on_deconstruction(disassembled);
    }

    // tg: code/modules/atmospherics/machinery/components/gas_recipe_machines/crystallizer.dm:61
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        return Dm.truthy(on) ? NONE : default_deconstruction_screwdriver(user, tool);
    }

    // tg: code/modules/atmospherics/machinery/components/gas_recipe_machines/crystallizer.dm:64
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        return await default_change_direction_wrench(user, tool);
    }

    // tg: code/modules/atmospherics/machinery/components/gas_recipe_machines/crystallizer.dm:67
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        return await crowbar_deconstruction_act(user, tool, Dm.num(@internal.return_pressure()));
    }

    // tg: code/modules/atmospherics/machinery/components/gas_recipe_machines/crystallizer.dm:70
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        mutable_appearance pipe_appearance1 = mutable_appearance(Dm.resource("icons/obj/pipes_n_cables/pipe_underlays.dmi"), Dm.text("intact_[]_[]", dir, piping_layer), layer: GAS_SCRUBBER_LAYER) as mutable_appearance;
        pipe_appearance1.color = COLOR_LIME;
        mutable_appearance pipe_appearance2 = mutable_appearance(Dm.resource("icons/obj/pipes_n_cables/pipe_underlays.dmi"), Dm.text("intact_[]_[]", REVERSE_DIR(dir), piping_layer), layer: GAS_SCRUBBER_LAYER) as mutable_appearance;
        pipe_appearance2.color = COLOR_MOSTLY_PURE_RED;
        Dm.append(ref dot, pipe_appearance1);
        Dm.append(ref dot, pipe_appearance2);
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/gas_recipe_machines/crystallizer.dm:79
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        if (Dm.truthy(panel_open))
            icon_state = Dm.text("[]-open", base_icon_state);
        else if (Dm.truthy(on) && Dm.truthy(is_operational))
            icon_state = Dm.text("[]-on", base_icon_state);
        else
            icon_state = Dm.text("[]-off", base_icon_state);
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/gas_recipe_machines/crystallizer.dm:88
    public override async DmTask<object> click_ctrl(mob user = null)
    {
        if (!Dm.truthy(is_operational))
            return CLICK_ACTION_BLOCKING;
        if (Dm.truthy(panel_open))
        {
            balloon_alert(user, I18n.T("close panel!"));
            return CLICK_ACTION_BLOCKING;
        }
        set_on(Dm.truthy(on) ? FALSE : TRUE);
        balloon_alert(user, I18n.T("turned []", Dm.truthy(on) ? "on" : "off"));
        investigate_log(Dm.text("was turned [] by []", Dm.truthy(on) ? "on" : "off", key_name(user)), INVESTIGATE_ATMOS);
        return CLICK_ACTION_SUCCESS;
    }

    ///Checks if the reaction temperature is inside the range of temperature + a little deviation
    // tg: code/modules/atmospherics/machinery/components/gas_recipe_machines/crystallizer.dm:100
    public virtual object check_temp_requirements()
    {
        if (@internal.temperature >= selected_recipe.min_temp * MIN_DEVIATION_RATE && @internal.temperature <= selected_recipe.max_temp * MAX_DEVIATION_RATE)
            return TRUE;
        return FALSE;
    }

    ///Injects the gases from the input inside the internal gasmix, the amount is dependent on the gas_input var
    // tg: code/modules/atmospherics/machinery/components/gas_recipe_machines/crystallizer.dm:106
    public virtual void inject_gases()
    {
        datum_gas_mixture contents = airs[2] as datum_gas_mixture;
        foreach (object gas_type in Dm.each(selected_recipe.requirements))
        {
            if (!Dm.truthy(contents.moles[gas_type]))
                continue;
            if (Dm.num(@internal.moles[gas_type]) >= Dm.num(selected_recipe.requirements[gas_type]) * 2)
                continue;
            @internal.merge(contents.remove_specific(gas_type, Dm.num(contents.moles[gas_type]) * gas_input) as datum_gas_mixture);
        }
    }

    ///Checks if the gases required are all inside
    // tg: code/modules/atmospherics/machinery/components/gas_recipe_machines/crystallizer.dm:116
    public virtual object internal_check()
    {
        float gas_check = 0;
        foreach (object gas_type in Dm.each(selected_recipe.requirements))
        {
            if (!Dm.truthy(@internal.moles[gas_type]))
                return FALSE;
            if (Dm.num(@internal.moles[gas_type]) >= Dm.num(selected_recipe.requirements[gas_type]))
                gas_check++;
        }
        if (gas_check == selected_recipe.requirements.len)
            return TRUE;
        return FALSE;
    }

    ///Calculation for the heat of the various gas mixes and controls the quality of the item
    // tg: code/modules/atmospherics/machinery/components/gas_recipe_machines/crystallizer.dm:128
    public virtual void heat_calculations()
    {
        float progress_amount_to_quality = MIN_PROGRESS_AMOUNT * 4.5f / (round(log(10, total_recipe_moles * 0.1f), 0.01f));
        if ((@internal.temperature >= (selected_recipe.min_temp * MIN_DEVIATION_RATE) && @internal.temperature <= selected_recipe.min_temp) ||
            (@internal.temperature >= selected_recipe.max_temp && @internal.temperature <= (selected_recipe.max_temp * MAX_DEVIATION_RATE)))
            quality_loss = min(quality_loss + progress_amount_to_quality, 100);

        float median_temperature = (selected_recipe.max_temp + selected_recipe.min_temp) / 2;
        if (@internal.temperature >= (median_temperature * MIN_DEVIATION_RATE) && @internal.temperature <= (median_temperature * MAX_DEVIATION_RATE))
            quality_loss = max(quality_loss - progress_amount_to_quality, -85);

        @internal.temperature = max(@internal.temperature + (selected_recipe.energy_release / Dm.num(@internal.heat_capacity())), TCMB);
        update_parents();
    }

    ///Conduction between the internal gasmix and the moderating (cooling/heating) gasmix.
    // tg: code/modules/atmospherics/machinery/components/gas_recipe_machines/crystallizer.dm:142
    public virtual void heat_conduction()
    {
        datum_gas_mixture cooling_port = airs[1] as datum_gas_mixture;
        if (Dm.num(cooling_port.total_moles()) > MINIMUM_MOLE_COUNT)
        {
            if (Dm.num(@internal.total_moles()) > 0)
            {
                float coolant_temperature_delta = cooling_port.temperature - @internal.temperature;
                float cooling_heat_capacity = Dm.num(cooling_port.heat_capacity());
                float internal_heat_capacity = Dm.num(@internal.heat_capacity());
                float cooling_heat_amount = HIGH_CONDUCTIVITY_RATIO * coolant_temperature_delta * (cooling_heat_capacity * internal_heat_capacity / (cooling_heat_capacity + internal_heat_capacity));
                cooling_port.temperature = max(cooling_port.temperature - cooling_heat_amount / cooling_heat_capacity, TCMB);
                @internal.temperature = max(@internal.temperature + cooling_heat_amount / internal_heat_capacity, TCMB);
                update_parents();
            }
        }
    }

    ///Calculate the total moles needed for the recipe
    // tg: code/modules/atmospherics/machinery/components/gas_recipe_machines/crystallizer.dm:155
    public virtual void moles_calculations()
    {
        float amounts = 0;
        foreach (object gas_type in Dm.each(selected_recipe.requirements))
            amounts += Dm.num(selected_recipe.requirements[gas_type]);
        total_recipe_moles = amounts;
    }

    ///Removes the gases from the internal gasmix when the recipe is changed
    // tg: code/modules/atmospherics/machinery/components/gas_recipe_machines/crystallizer.dm:162
    public virtual void dump_gases()
    {
        datum_gas_mixture remove = @internal.remove(@internal.total_moles()) as datum_gas_mixture;
        ((datum_gas_mixture)airs[2]).merge(remove);
        @internal.garbage_collect();
    }

    // tg: code/modules/atmospherics/machinery/components/gas_recipe_machines/crystallizer.dm:167
    public override object process_atmos(object seconds_per_tick = null)
    {
        if (!Dm.truthy(on) || !Dm.truthy(is_operational) || selected_recipe == null)
            return null;

        inject_gases();

        if (!Dm.truthy(@internal.total_moles()))
            return null;

        heat_conduction();

        if (Dm.truthy(internal_check()))
        {
            if (Dm.truthy(check_temp_requirements()))
            {
                heat_calculations();
                progress_bar = min(progress_bar + (MIN_PROGRESS_AMOUNT * 5 / (round(log(10, total_recipe_moles * 0.1f), 0.01f))), 100);
            }
            else
            {
                quality_loss = min(quality_loss + 0.5f, 100);
                progress_bar = max(progress_bar - 1, 0);
            }
        }
        if (progress_bar != 100)
        {
            update_parents();
            return null;
        }
        progress_bar = 0;

        foreach (object gas_type in Dm.each(selected_recipe.requirements))
        {
            float required_gas_moles = Dm.num(selected_recipe.requirements[gas_type]);
            float amount_consumed = required_gas_moles + (required_gas_moles * (quality_loss * 0.01f));
            if (Dm.num(@internal.moles[gas_type]) < amount_consumed)
                quality_loss = min(quality_loss + 10, 100);
            @internal.remove_specific(gas_type, amount_consumed);
        }

        float total_quality = clamp(50 - quality_loss, 0, 100);
        string quality_control = null;
        // DM's ranges are inclusive: a fractional quality between two of them (94.5) matches none and stays null, as in tg
        if (Dm.in_to(total_quality, 100, 100))
            quality_control = "Masterwork";
        else if (Dm.in_to(total_quality, 95, 99))
            quality_control = "Supreme";
        else if (Dm.in_to(total_quality, 75, 94))
            quality_control = "Good";
        else if (Dm.in_to(total_quality, 65, 74))
            quality_control = "Decent";
        else if (Dm.in_to(total_quality, 55, 64))
            quality_control = "Average";
        else if (Dm.in_to(total_quality, 35, 54))
            quality_control = "Ok";
        else if (Dm.in_to(total_quality, 15, 34))
            quality_control = "Poor";
        else if (Dm.in_to(total_quality, 5, 14))
            quality_control = "Ugly";
        else if (Dm.in_to(total_quality, 1, 4))
            quality_control = "Cracked";
        else if (total_quality == 0)
            quality_control = "Oh God why";

        foreach (Type path in Dm.each_cast<Type>(selected_recipe.products))
        {
            float amount_produced = Dm.num(selected_recipe.products[path]);
            for (float i = 1; i <= amount_produced; i++)
            {
                obj creation = Dm.@new(path, get_step(this, SOUTH)) as obj;
                creation.name = Dm.text("[] []", quality_control, creation.name);
                if (Dm.truthy(selected_recipe.dangerous))
                {
                    investigate_log(Dm.text("[] has been created in the crystallizer.", creation.name), INVESTIGATE_ENGINE);
                    message_admins(Dm.text("[] has been created in the crystallizer [].", creation.name, ADMIN_JMP(this)));
                }
            }
        }

        quality_loss = 0;
        update_parents();
        return null;
    }

    // tg: code/modules/atmospherics/machinery/components/gas_recipe_machines/crystallizer.dm:234
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "Crystallizer", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/atmospherics/machinery/components/gas_recipe_machines/crystallizer.dm:240
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();
        data["selected_recipes"] = Dm.list(Dm.list(Dm.kv("name", "Nothing"), Dm.kv("id", "")));
        foreach (object path in Dm.each(GLOB.gas_recipe_meta))
        {
            datum_gas_recipe recipe = GLOB.gas_recipe_meta[path] as datum_gas_recipe;
            if (!Dm.eq(recipe.machine_type, "Crystallizer"))
                continue;
            data["selected_recipes"] = Dm.add(data["selected_recipes"], Dm.list(Dm.list(Dm.kv("name", recipe.name), Dm.kv("id", recipe.id))));
        }
        return data;
    }

    // tg: code/modules/atmospherics/machinery/components/gas_recipe_machines/crystallizer.dm:250
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["on"] = on;

        if (selected_recipe != null)
            data["selected"] = selected_recipe.id;
        else
            data["selected"] = "";

        DmList internal_gas_data = Dm.list();
        DmList cached_gas_name = (DmList)GLOB.meta_gas_info[META_GAS_NAME]; // GAS_META[META_GAS_NAME]
        DmList cached_gas_id = (DmList)GLOB.meta_gas_info[META_GAS_ID]; // GAS_META[META_GAS_ID]
        if (Dm.truthy(@internal.total_moles()))
        {
            foreach ((object gasid, object amount) in Dm.pairs(@internal.moles))
            {
                internal_gas_data.Add(Dm.list(Dm.list(
                    Dm.kv("name", cached_gas_name[gasid]),
                    Dm.kv("id", cached_gas_id[gasid]),
                    Dm.kv("amount", round(Dm.num(amount), 0.01f)))));
            }
        }
        else
        {
            foreach (object gasid in Dm.each(@internal.moles))
            {
                internal_gas_data.Add(Dm.list(Dm.list(
                    Dm.kv("name", cached_gas_name[gasid]),
                    Dm.kv("id", cached_gas_id[gasid]),
                    Dm.kv("amount", 0))));
            }
        }
        data["internal_gas_data"] = internal_gas_data;

        DmList requirements;
        if (selected_recipe == null)
            requirements = Dm.list("Select a recipe to see the requirements");
        else
        {
            requirements = Dm.list(Dm.text("To create [] you will need:", selected_recipe.name));
            foreach (object gas_type in Dm.each(selected_recipe.requirements))
            {
                object amount_consumed = selected_recipe.requirements[gas_type];
                Dm.append(ref requirements, Dm.text("-[] moles of []", amount_consumed, Dm.initial(gas_type as Type, (datum_gas g) => g.name)));
            }
            Dm.append(ref requirements, Dm.text("In a temperature range between [] K and [] K", selected_recipe.min_temp, selected_recipe.max_temp));
            Dm.append(ref requirements, Dm.text("The crystallization reaction will be []", selected_recipe.energy_release != 0 ? (selected_recipe.energy_release > 0 ? "exothermic" : "endothermic") : "thermally neutral"));
        }
        data["requirements"] = requirements.Join("\n");

        float temperature;
        if (Dm.truthy(@internal.total_moles()))
            temperature = @internal.temperature;
        else
            temperature = 0;
        data["internal_temperature"] = temperature;
        data["progress_bar"] = progress_bar;
        data["gas_input"] = gas_input;
        return data;
    }

    // tg: code/modules/atmospherics/machinery/components/gas_recipe_machines/crystallizer.dm:301
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        switch (action)
        {
            case "power":
                set_on(Dm.truthy(on) ? FALSE : TRUE);
                investigate_log(Dm.text("was turned [] by []", Dm.truthy(on) ? "on" : "off", key_name(usr)), INVESTIGATE_ATMOS);
                dot = TRUE;
                break;
            case "recipe":
            {
                selected_recipe = null;
                object recipe_name = "nothing";
                datum_gas_recipe recipe = GLOB.gas_recipe_meta[@params["mode"]] as datum_gas_recipe;
                if (Dm.truthy(@internal.total_moles()))
                    dump_gases();
                quality_loss = 0;
                progress_bar = 0;
                if (recipe != null && !Dm.eq(recipe.id, ""))
                {
                    selected_recipe = recipe;
                    recipe_name = recipe.name;
                    update_parents(); //prevent the machine from stopping because of the recipe change and the pipenet not updating
                    moles_calculations();
                }
                investigate_log(Dm.text("was set to recipe [] by []", Dm.truthy(recipe_name) ? recipe_name : "null", key_name(usr)), INVESTIGATE_ATMOS);
                dot = TRUE;
                break;
            }
            case "gas_input":
            {
                object _gas_input = @params["gas_input"];
                gas_input = Dm.num(Dm.clamp(_gas_input, 0, 250));
                break;
            }
        }
        await update_icon();
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/gas_recipe_machines/crystallizer.dm:330
    public override void update_layer()
    {
        return;
    }
}
