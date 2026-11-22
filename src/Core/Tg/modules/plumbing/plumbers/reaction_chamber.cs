// Translated from tgstation (snapshot 1d99bd7f): code/modules/plumbing/plumbers/reaction_chamber.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Engineering E15 (plumbing): the whole file. The var blocks are generated (D111). The chamber takes in its required reagents,
// holds, heats and cools them by tg's code; the reactions themselves wait for chemistry (the holder's handle_reactions reaches its
// deferred reaction lookup, reactions.cs), so a full chamber empties out what it took in.

///a reaction chamber for plumbing. pretty much everything can react, but this one keeps the reagents separated and only reacts under your given terms
// tg: code/modules/plumbing/plumbers/reaction_chamber.dm:6
public partial class obj_machinery_plumbing_reaction_chamber : obj_machinery_plumbing
{
    /// coefficient to convert temperature to joules. same lvl as acclimator
    // tg: code/modules/plumbing/plumbers/reaction_chamber.dm:4  #define HEATER_COEFFICIENT 0.05 (file-local, rules 11.4)
    private const float HEATER_COEFFICIENT = 0.05f;

    // tg: code/modules/plumbing/plumbers/reaction_chamber.dm:25
    [DmParams("mapload", "layer")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object layer = Dm.arg(__extra, 0);
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_plumbing_reaction_chamber), layer));
        return dot;
    }

    /// Handles stopping the emptying process when the chamber empties.
    // tg: code/modules/plumbing/plumbers/reaction_chamber.dm:30
    [SignalHandler]
    public virtual void on_reagent_change(datum_reagents_plumbing_reaction_chamber holder = null)
    {
        if (!Dm.truthy(holder.get_catalyst_excluded_volume())) //we were emptying, but now we aren't
        {
            emptying = FALSE;
            Dm.combine(ref holder.flags, NO_REACT);
            UnregisterSignal(reagents as datum, COMSIG_REAGENTS_HOLDER_UPDATED);
        }
    }

    // tg: code/modules/plumbing/plumbers/reaction_chamber.dm:38
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        datum_reagents holder = (datum_reagents)reagents;
        if (!Dm.truthy(is_operational) || !Dm.truthy(holder.total_volume))
            return null;

        if (!Dm.truthy(emptying) || Dm.truthy(holder.is_reacting))
        {
            //adjust temperature of final solution
            float energy = (Dm.num(target_temperature) - holder.chem_temp) * HEATER_COEFFICIENT * Dm.num(seconds_per_tick) * Dm.num(holder.heat_capacity());
            holder.adjust_thermal_energy(energy);
            Waiting.NoSleep(use_energy(active_power_usage + abs(ROUND_UP(energy) / 120)));

            //do other stuff with final solution
            Waiting.NoSleep(handle_reagents(seconds_per_tick));
        }
        return null;
    }

    /**
     * For subtypes that want to do additional reagent handling
     * Arguments
     *
     * * seconds_per_tick - passed down from process()
     */
    // tg: code/modules/plumbing/plumbers/reaction_chamber.dm:57
    public virtual async DmTask handle_reagents(object seconds_per_tick = null)
    {
        return;
    }

    // tg: code/modules/plumbing/plumbers/reaction_chamber.dm:62
    public override object power_change()
    {
        object dot = base.power_change();

        icon_state = Dm.initial(this, (atom p) => p.icon_state) + Dm.text("[]", !Dm.eq(use_power, NO_POWER_USE) ? "_on" : "");
        return dot;
    }

    // tg: code/modules/plumbing/plumbers/reaction_chamber.dm:67
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "ChemMixingChamber", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/plumbing/plumbers/reaction_chamber.dm:73
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList dot = Dm.list();

        DmList reagents_data = Dm.list();
        foreach (Type required_reagent in Dm.each_cast<Type>(required_reagents)) //make a list where the key is text, because that looks alot better in the ui than a typepath
        {
            DmList reagent_data = Dm.list();
            reagent_data["name"] = Dm.initial(required_reagent, (datum_reagent r) => r.name);
            reagent_data["volume"] = required_reagents[required_reagent];
            reagents_data = Dm.add(reagents_data, Dm.list(reagent_data)) as DmList;
        }

        DmList catalyst_data = Dm.list();
        foreach (Type required_catalyst in Dm.each_cast<Type>(catalysts))
        {
            DmList reagent_data = Dm.list();
            reagent_data["name"] = Dm.initial(required_catalyst, (datum_reagent r) => r.name);
            reagent_data["volume"] = catalysts[required_catalyst];
            catalyst_data = Dm.add(catalyst_data, Dm.list(reagent_data)) as DmList;
        }

        datum_reagents holder = (datum_reagents)reagents;
        dot["reagents"] = reagents_data;
        dot["catalysts"] = catalyst_data;
        dot["emptying"] = emptying;
        dot["temperature"] = round(holder.chem_temp, 0.1f);
        dot["targetTemp"] = target_temperature;
        dot["isReacting"] = holder.is_reacting;
        return dot;
    }

    // tg: code/modules/plumbing/plumbers/reaction_chamber.dm:97
    [DmParams("action", "params", "ui", "state")]
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return TRUE;

        switch (action)
        {
            case "add":
            {
                object selected_reagent = await tgui_input_list(ui.user, "Select reagent", "Reagent", GLOB.name2reagent);
                if (!Dm.truthy(selected_reagent))
                    return FALSE;
                if (QDELETED(ui) || ui.status != UI_INTERACTIVE)
                    return FALSE;

                object input_reagent = GLOB.name2reagent[selected_reagent];
                if (!Dm.truthy(input_reagent))
                    return FALSE;

                if (Dm.truthy(catalysts[input_reagent]))
                    return FALSE;

                float? input_amount = text2num(@params["amount"]);
                if (!Dm.truthy(input_amount))
                    return FALSE;
                input_amount = round(input_amount.Value, CHEMICAL_VOLUME_ROUNDING);

                if (!Dm.truthy(required_reagents[input_reagent]))
                {
                    required_reagents[input_reagent] = input_amount;
                    return TRUE;
                }
                break;
            }

            case "remove":
            {
                object reagent = get_chem_id(@params["chem"]);
                if (Dm.truthy(reagent))
                {
                    required_reagents.Remove(reagent);
                    return TRUE;
                }
                break;
            }

            case "temperature":
            {
                float? target = text2num(@params["target"]);
                if (target != null)
                {
                    target_temperature = clamp(target.Value, 0, 1000);
                    return TRUE;
                }
                break;
            }

            case "catalyst":
            {
                object reagent = get_chem_id(@params["chem"]);
                if (!Dm.truthy(reagent))
                    return FALSE;

                if (!Dm.truthy(catalysts[reagent]))
                {
                    catalysts[reagent] = required_reagents[reagent];
                    Dm.remove(ref required_reagents, reagent);
                    return TRUE;
                }
                break;
            }

            case "catremove":
            {
                object reagent = get_chem_id(@params["chem"]);
                if (!Dm.truthy(reagent))
                    return FALSE;

                if (Dm.truthy(catalysts[reagent]))
                {
                    required_reagents[reagent] = catalysts[reagent];
                    Dm.remove(ref catalysts, reagent);
                    return TRUE;
                }
                break;
            }
        }

        object result = handle_ui_act(action, @params, ui, state);
        if (result == null)
            result = FALSE;
        return result;
    }

    /// For custom handling of ui actions from inside a subtype
    // tg: code/modules/plumbing/plumbers/reaction_chamber.dm:164
    public virtual object handle_ui_act(object action = null, object @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        return null;
    }
}

///Chemistry version of reaction chamber that allows for acid and base buffers to be used while reacting
// tg: code/modules/plumbing/plumbers/reaction_chamber.dm:170
public partial class obj_machinery_plumbing_reaction_chamber_chem : obj_machinery_plumbing_reaction_chamber
{
    // tg: code/modules/plumbing/plumbers/reaction_chamber.dm:183
    [DmParams("mapload", "layer")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        datum_component_plumbing_buffered acidic_input = Waiting.NoSleep(AddComponent(typeof(datum_component_plumbing_buffered_acidic_input))) as datum_component_plumbing_buffered;
        datum_component_plumbing_buffered basic_input = Waiting.NoSleep(AddComponent(typeof(datum_component_plumbing_buffered_alkaline_input))) as datum_component_plumbing_buffered;

        acidic_beaker = acidic_input.recipient_reagents_holder() as datum_reagents;
        alkaline_beaker = basic_input.recipient_reagents_holder() as datum_reagents;
        return dot;
    }

    // tg: code/modules/plumbing/plumbers/reaction_chamber.dm:192
    public override async DmTask handle_reagents(object seconds_per_tick = null)
    {
        datum_reagents holder = (datum_reagents)reagents;
        if (holder.ph < acidic_limit || holder.ph > alkaline_limit)
        {
            //nothing to react with
            float num_of_reagents = length(holder.reagent_list);
            if (!Dm.truthy(num_of_reagents))
                return;


            //.figure out which buffer to transfer to restore balance
            //if solution is getting too basic(high ph) add some acid to lower its value
            //else if solution is getting too acidic(low ph) add some base to increase its value
            datum_reagents buffer = holder.ph > alkaline_limit ? acidic_beaker : alkaline_beaker;
            if (!Dm.truthy(buffer.total_volume))
                return;

            //transfer buffer and handle reactions
            float ph_change = max((holder.ph > alkaline_limit ? (holder.ph - alkaline_limit) : (acidic_limit - holder.ph)), 0.25f);
            float buffer_amount = ((ph_change * holder.total_volume) / (BUFFER_IONIZING_STRENGTH * num_of_reagents)) * Dm.num(seconds_per_tick);
            if (!Dm.truthy(await buffer.trans_to(holder, buffer_amount)))
                return;

            //some power for accurate ph balancing
            await use_energy(active_power_usage * 0.03f * buffer_amount);
        }
    }

    // tg: code/modules/plumbing/plumbers/reaction_chamber.dm:216
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "ChemReactionChamber", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/plumbing/plumbers/reaction_chamber.dm:222
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList dot = await base.ui_data(user) as DmList;
        dot["ph"] = round(((datum_reagents)reagents).ph, 0.01f);
        dot["reagentAcidic"] = acidic_limit;
        dot["reagentAlkaline"] = alkaline_limit;
        return dot;
    }

    // tg: code/modules/plumbing/plumbers/reaction_chamber.dm:228
    public override object handle_ui_act(object action = null, object @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = TRUE;

        DmList p = @params as DmList;
        switch (action)
        {
            case "acidic":
                acidic_limit = clamp(round(Dm.num(text2num(p["target"]))), CHEMICAL_MIN_PH, alkaline_limit - 1);
                break;
            case "alkaline":
                alkaline_limit = clamp(round(Dm.num(text2num(p["target"]))), acidic_limit + 1, CHEMICAL_MAX_PH);
                break;
            default:
                return FALSE;
        }
        return dot;
    }
}
