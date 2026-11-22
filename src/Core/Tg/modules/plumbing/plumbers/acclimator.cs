// Translated from tgstation (snapshot 1d99bd7f): code/modules/plumbing/plumbers/acclimator.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Engineering E15 (plumbing): the whole file. The var block is generated (D111). handle_reactions reaches the holder's
// deferred reaction lookup (chemistry is not ported: reactions.cs), so the acclimator heats and cools without reacting.

// tg: code/modules/plumbing/plumbers/acclimator.dm:8
public partial class obj_machinery_plumbing_acclimator : obj_machinery_plumbing
{
    ///Same as a tier 3 chem heater
    // tg: code/modules/plumbing/plumbers/acclimator.dm:2  #define HEATER_COFFICIENT 0.3 (file-local, rules 11.4)
    private const float HEATER_COFFICIENT = 0.3f;
    ///Decimal point in rounding temperature
    // tg: code/modules/plumbing/plumbers/acclimator.dm:4  #define TEMP_ROUNDING 0.01 (file-local, rules 11.4)
    private const float TEMP_ROUNDING = 0.01f;
    ///Minimal allowed difference temperature range
    // tg: code/modules/plumbing/plumbers/acclimator.dm:6  #define TEMP_DIFF 0.5 (file-local, rules 11.4)
    private const float TEMP_DIFF = 0.5f;

    // tg: code/modules/plumbing/plumbers/acclimator.dm:23
    [DmParams("mapload", "layer")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object layer = Dm.arg(__extra, 0);
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_plumbing_acclimator), layer));
        RegisterSignal(reagents as datum, COMSIG_REAGENTS_HOLDER_UPDATED, nameof(clear));
        return dot;
    }

    // tg: code/modules/plumbing/plumbers/acclimator.dm:28
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);

        icon_state = base_icon_state as string;
        if (!Dm.truthy(is_operational) || !Dm.truthy(anchored))
            return dot;

        switch (acclimate_state)
        {
            case AC_FILLING:
                icon_state += "_fill";
                break;
            case AC_HEATING:
                icon_state += "_hot";
                break;
            case AC_COOLING:
                icon_state += "_cold";
                break;
            case AC_EMPTYING:
                icon_state += "_empty";
                break;
        }
        return dot;
    }

    // tg: code/modules/plumbing/plumbers/acclimator.dm:45
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        object dot = await base.wrench_act(user, tool);
        if (Dm.eq(dot, ITEM_INTERACT_SUCCESS))
        {
            acclimate_state = AC_FILLING;
            update_appearance(UPDATE_ICON_STATE);
        }
        return dot;
    }

    // tg: code/modules/plumbing/plumbers/acclimator.dm:51
    public override async DmTask<object> plunger_act(obj_item_plunger attacking_plunger = null, mob_living user = null, object reinforced = null)
    {
        object dot = await base.plunger_act(attacking_plunger, user, reinforced);
        if (Dm.truthy(dot))
        {
            acclimate_state = AC_FILLING;
            update_appearance(UPDATE_ICON_STATE);
        }
        return dot;
    }

    // tg: code/modules/plumbing/plumbers/acclimator.dm:57
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        datum_reagents holder = (datum_reagents)reagents;
        if (!Dm.truthy(is_operational) || !Dm.truthy(holder.total_volume) || Dm.eq(acclimate_state, AC_FILLING) || Dm.eq(acclimate_state, AC_EMPTYING))
            return null;

        float new_state = holder.chem_temp > Dm.num(target_temperature) ? AC_COOLING : AC_HEATING;
        if (acclimate_state != new_state)
        {
            acclimate_state = new_state;
            update_appearance(UPDATE_ICON_STATE);
        }

        float energy = (Dm.num(target_temperature) - holder.chem_temp) * HEATER_COFFICIENT * Dm.num(seconds_per_tick) * Dm.num(holder.heat_capacity());
        holder.adjust_thermal_energy(energy);
        Waiting.NoSleep(holder.handle_reactions());
        Waiting.NoSleep(use_energy(active_power_usage + abs(ROUND_UP(energy) / 120)));

        if (Dm.truthy(holder.is_reacting))
            return null;

        float temp = round(holder.chem_temp, TEMP_ROUNDING);
        if (temp >= Dm.num(target_temperature) - TEMP_DIFF && temp <= Dm.num(target_temperature) + TEMP_DIFF)
        {
            holder.set_temperature(target_temperature);
            acclimate_state = AC_EMPTYING;
            update_appearance(UPDATE_ICON_STATE);
        }
        return null;
    }

    // tg: code/modules/plumbing/plumbers/acclimator.dm:80
    [SignalHandler]
    public virtual void clear()
    {
        datum_reagents holder = (datum_reagents)reagents;
        if (Dm.eq(acclimate_state, AC_EMPTYING) && !Dm.truthy(holder.total_volume))
        {
            acclimate_state = AC_FILLING;
            Dm.combine(ref holder.flags, NO_REACT);
            update_appearance(UPDATE_ICON_STATE);
        }
    }

    // tg: code/modules/plumbing/plumbers/acclimator.dm:88
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "ChemAcclimator", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/plumbing/plumbers/acclimator.dm:94
    public override async DmTask<object> ui_data(mob user = null)
    {
        return Dm.list(
            Dm.kv("chem_temp", round(((datum_reagents)reagents).chem_temp, TEMP_ROUNDING)),
            Dm.kv("target_temperature", target_temperature),
            Dm.kv("max_volume", max_volume),
            Dm.kv("acclimate_state", acclimate_state));
    }

    // tg: code/modules/plumbing/plumbers/acclimator.dm:102
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        switch (action)
        {
            case "set_target_temperature":
            {
                float? value = text2num(@params["temperature"]);
                if (!Dm.truthy(value))
                    return FALSE;

                target_temperature = round(clamp(value.Value, 1, 1000), TEMP_ROUNDING);
                return TRUE;
            }

            case "change_volume":
            {
                float? value = text2num(@params["volume"]);
                if (!Dm.truthy(value))
                    return FALSE;

                max_volume = round(clamp(value.Value, 1, Dm.num(buffer)), CHEMICAL_VOLUME_ROUNDING);
                return TRUE;
            }
        }
        return dot;
    }
}
