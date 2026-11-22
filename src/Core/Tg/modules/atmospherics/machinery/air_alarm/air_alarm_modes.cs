// Translated from tgstation (snapshot 1d99bd7f): code/modules/atmospherics/machinery/air_alarm/air_alarm_modes.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4b slice 2 (the air alarms): the whole file. The var blocks (the modes' names and flags) are generated.

/// Keys are [/datum/air_alarm_mode] paths
/// Values are their respective instances.
public partial class datum_controller_global_vars
{
    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_modes.dm:3  GLOBAL_LIST_INIT(air_alarm_modes, init_air_alarm_modes())
    public DmList air_alarm_modes;

    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_modes.dm:3  (GLOBAL_MANAGED(air_alarm_modes, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalair_alarm_modes()
    {
        air_alarm_modes = init_air_alarm_modes() as DmList;
        Dm.append(ref gvars_datum_init_order, "air_alarm_modes");
    }
}

public static partial class GlobalProcs
{
    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_modes.dm:5
    public static object init_air_alarm_modes()
    {
        DmList ret = Dm.list();
        foreach (Type mode_path in Dm.each_cast<Type>(subtypesof(typeof(datum_air_alarm_mode))))
            ret[mode_path] = Dm.@new(mode_path);
        return ret;
    }
}

/// Various modes that an [/obj/machinery/airalarm] can assume.
// tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_modes.dm:12
public partial class datum_air_alarm_mode : datum
{
    /** The proc that runs when this air alarm mode is selected.
     *
     * Arguments:
     * * applied - which area will we apply this mode to.
     */
    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_modes.dm:27
    public virtual void apply(area applied = null)
    {
        return;
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_modes.dm:30
    public virtual void replace(area applied = null, object pressure = null)
    {
        return;
    }
}

/// The default.
// tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_modes.dm:34
public partial class datum_air_alarm_mode_filtering : datum_air_alarm_mode
{
    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_modes.dm:39
    public override void apply(area applied = null)
    {
        foreach (obj_machinery_atmospherics_components_unary_vent_pump vent in Dm.each_cast<obj_machinery_atmospherics_components_unary_vent_pump>(applied.air_vents))
        {
            vent.on = TRUE;
            vent.pressure_checks = ATMOS_EXTERNAL_BOUND;
            vent.external_pressure_bound = ONE_ATMOSPHERE;
            vent.pump_direction = ATMOS_DIRECTION_RELEASING;
            vent.update_appearance(UPDATE_ICON);
        }

        foreach (obj_machinery_atmospherics_components_unary_vent_scrubber scrubber in Dm.each_cast<obj_machinery_atmospherics_components_unary_vent_scrubber>(applied.air_scrubbers))
        {
            scrubber.on = TRUE;
            scrubber.filter_types = Dm.list(typeof(datum_gas_carbon_dioxide));
            scrubber.set_scrubbing(ATMOS_DIRECTION_SCRUBBING);
            scrubber.set_widenet(FALSE);
        }
    }
}

// tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_modes.dm:53
public partial class datum_air_alarm_mode_contaminated : datum_air_alarm_mode
{
    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_modes.dm:58
    public override void apply(area applied = null)
    {
        foreach (obj_machinery_atmospherics_components_unary_vent_pump vent in Dm.each_cast<obj_machinery_atmospherics_components_unary_vent_pump>(applied.air_vents))
        {
            vent.on = TRUE;
            vent.pressure_checks = ATMOS_EXTERNAL_BOUND;
            vent.external_pressure_bound = ONE_ATMOSPHERE;
            vent.pump_direction = ATMOS_DIRECTION_RELEASING;
            vent.update_appearance(UPDATE_ICON);
        }

        DmList filtered = subtypesof(typeof(datum_gas)) as DmList;
        Dm.remove(ref filtered, Dm.list(typeof(datum_gas_oxygen), typeof(datum_gas_nitrogen), typeof(datum_gas_pluoxium)));
        foreach (obj_machinery_atmospherics_components_unary_vent_scrubber scrubber in Dm.each_cast<obj_machinery_atmospherics_components_unary_vent_scrubber>(applied.air_scrubbers))
        {
            scrubber.on = TRUE;
            scrubber.filter_types = filtered.Copy();
            scrubber.set_scrubbing(ATMOS_DIRECTION_SCRUBBING);
            scrubber.set_widenet(TRUE);
        }
    }
}

// tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_modes.dm:74
public partial class datum_air_alarm_mode_draught : datum_air_alarm_mode
{
    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_modes.dm:79
    public override void apply(area applied = null)
    {
        foreach (obj_machinery_atmospherics_components_unary_vent_pump vent in Dm.each_cast<obj_machinery_atmospherics_components_unary_vent_pump>(applied.air_vents))
        {
            vent.on = TRUE;
            vent.pressure_checks = ATMOS_EXTERNAL_BOUND;
            vent.external_pressure_bound = ONE_ATMOSPHERE * 2;
            vent.pump_direction = ATMOS_DIRECTION_RELEASING;
            vent.update_appearance(UPDATE_ICON);
        }

        foreach (obj_machinery_atmospherics_components_unary_vent_scrubber scrubber in Dm.each_cast<obj_machinery_atmospherics_components_unary_vent_scrubber>(applied.air_scrubbers))
        {
            scrubber.on = TRUE;
            scrubber.set_widenet(FALSE);
            scrubber.set_scrubbing(ATMOS_DIRECTION_SIPHONING);
        }
    }
}

// tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_modes.dm:92
public partial class datum_air_alarm_mode_refill : datum_air_alarm_mode
{
    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_modes.dm:97
    public override void apply(area applied = null)
    {
        foreach (obj_machinery_atmospherics_components_unary_vent_pump vent in Dm.each_cast<obj_machinery_atmospherics_components_unary_vent_pump>(applied.air_vents))
        {
            vent.on = TRUE;
            vent.pressure_checks = ATMOS_EXTERNAL_BOUND;
            vent.external_pressure_bound = ONE_ATMOSPHERE * 3;
            vent.pump_direction = ATMOS_DIRECTION_RELEASING;
            vent.update_appearance(UPDATE_ICON);
        }

        foreach (obj_machinery_atmospherics_components_unary_vent_scrubber scrubber in Dm.each_cast<obj_machinery_atmospherics_components_unary_vent_scrubber>(applied.air_scrubbers))
        {
            scrubber.on = TRUE;

            scrubber.filter_types = Dm.list(typeof(datum_gas_carbon_dioxide));
            scrubber.set_widenet(FALSE);
            scrubber.set_scrubbing(ATMOS_DIRECTION_SCRUBBING);
        }
    }
}

// tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_modes.dm:112
public partial class datum_air_alarm_mode_cycle : datum_air_alarm_mode
{
    /// Same as [/datum/air_alarm_mode/siphon/apply]
    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_modes.dm:118
    public override void apply(area applied = null)
    {
        foreach (obj_machinery_atmospherics_components_unary_vent_pump vent in Dm.each_cast<obj_machinery_atmospherics_components_unary_vent_pump>(applied.air_vents))
        {
            vent.on = FALSE;
            vent.update_appearance(UPDATE_ICON);
        }

        foreach (obj_machinery_atmospherics_components_unary_vent_scrubber scrubber in Dm.each_cast<obj_machinery_atmospherics_components_unary_vent_scrubber>(applied.air_scrubbers))
        {
            scrubber.on = TRUE;
            scrubber.set_widenet(TRUE);
            scrubber.set_scrubbing(ATMOS_DIRECTION_SIPHONING);
        }
    }

    /// Special case for cycles. Cycles need to refill the air again after it's scrubbed out so this proc is called.
    /// Same as [/datum/air_alarm_mode/filtering/apply]
    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_modes.dm:130
    public override void replace(area applied = null, object pressure = null)
    {
        if (Dm.num(pressure) >= ONE_ATMOSPHERE * 0.05f)
            return;

        foreach (obj_machinery_atmospherics_components_unary_vent_pump vent in Dm.each_cast<obj_machinery_atmospherics_components_unary_vent_pump>(applied.air_vents))
        {
            vent.on = TRUE;
            vent.pressure_checks = ATMOS_EXTERNAL_BOUND;
            vent.external_pressure_bound = ONE_ATMOSPHERE;
            vent.pump_direction = ATMOS_DIRECTION_RELEASING;
            vent.update_appearance(UPDATE_ICON);
        }

        foreach (obj_machinery_atmospherics_components_unary_vent_scrubber scrubber in Dm.each_cast<obj_machinery_atmospherics_components_unary_vent_scrubber>(applied.air_scrubbers))
        {
            scrubber.on = TRUE;
            scrubber.filter_types = Dm.list(typeof(datum_gas_carbon_dioxide));
            scrubber.set_scrubbing(ATMOS_DIRECTION_SCRUBBING);
            scrubber.set_widenet(FALSE);
        }
    }
}

// tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_modes.dm:147
public partial class datum_air_alarm_mode_siphon : datum_air_alarm_mode
{
    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_modes.dm:152
    public override void apply(area applied = null)
    {
        foreach (obj_machinery_atmospherics_components_unary_vent_pump vent in Dm.each_cast<obj_machinery_atmospherics_components_unary_vent_pump>(applied.air_vents))
        {
            vent.on = FALSE;
            vent.update_appearance(UPDATE_ICON);
        }

        foreach (obj_machinery_atmospherics_components_unary_vent_scrubber scrubber in Dm.each_cast<obj_machinery_atmospherics_components_unary_vent_scrubber>(applied.air_scrubbers))
        {
            scrubber.on = TRUE;
            scrubber.set_widenet(FALSE);
            scrubber.set_scrubbing(ATMOS_DIRECTION_SIPHONING);
        }
    }
}

// tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_modes.dm:162
public partial class datum_air_alarm_mode_panic_siphon : datum_air_alarm_mode
{
    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_modes.dm:167
    public override void apply(area applied = null)
    {
        foreach (obj_machinery_atmospherics_components_unary_vent_pump vent in Dm.each_cast<obj_machinery_atmospherics_components_unary_vent_pump>(applied.air_vents))
        {
            vent.on = FALSE;
            vent.update_appearance(UPDATE_ICON);
        }

        foreach (obj_machinery_atmospherics_components_unary_vent_scrubber scrubber in Dm.each_cast<obj_machinery_atmospherics_components_unary_vent_scrubber>(applied.air_scrubbers))
        {
            scrubber.on = TRUE;
            scrubber.set_widenet(TRUE);
            scrubber.set_scrubbing(ATMOS_DIRECTION_SIPHONING);
        }
    }
}

// tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_modes.dm:177
public partial class datum_air_alarm_mode_off : datum_air_alarm_mode
{
    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_modes.dm:182
    public override void apply(area applied = null)
    {
        foreach (obj_machinery_atmospherics_components_unary_vent_pump vent in Dm.each_cast<obj_machinery_atmospherics_components_unary_vent_pump>(applied.air_vents))
        {
            vent.on = FALSE;
            vent.update_appearance(UPDATE_ICON);
        }

        foreach (obj_machinery_atmospherics_components_unary_vent_scrubber scrubber in Dm.each_cast<obj_machinery_atmospherics_components_unary_vent_scrubber>(applied.air_scrubbers))
        {
            scrubber.on = FALSE;
            scrubber.update_appearance(UPDATE_ICON);
        }
    }
}

// tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_modes.dm:191
public partial class datum_air_alarm_mode_flood : datum_air_alarm_mode
{
    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_modes.dm:197
    public override void apply(area applied = null)
    {
        foreach (obj_machinery_atmospherics_components_unary_vent_pump vent in Dm.each_cast<obj_machinery_atmospherics_components_unary_vent_pump>(applied.air_vents))
        {
            vent.on = TRUE;
            vent.pressure_checks = ATMOS_INTERNAL_BOUND;
            vent.internal_pressure_bound = 0;
            vent.pump_direction = ATMOS_DIRECTION_RELEASING;
            vent.update_appearance(UPDATE_ICON);
        }

        foreach (obj_machinery_atmospherics_components_unary_vent_scrubber scrubber in Dm.each_cast<obj_machinery_atmospherics_components_unary_vent_scrubber>(applied.air_scrubbers))
        {
            scrubber.on = FALSE;
            scrubber.update_appearance(UPDATE_ICON);
        }
    }
}

// tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_modes.dm:209
public partial class datum_air_alarm_mode_vent_siphon : datum_air_alarm_mode
{
    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_modes.dm:215
    public override void apply(area applied = null)
    {
        foreach (obj_machinery_atmospherics_components_unary_vent_pump vent in Dm.each_cast<obj_machinery_atmospherics_components_unary_vent_pump>(applied.air_vents))
        {
            vent.on = TRUE;
            vent.pressure_checks = NONE;
            vent.internal_pressure_bound = 0;
            vent.external_pressure_bound = 0;
            vent.pump_direction = ATMOS_DIRECTION_SIPHONING;
            vent.update_appearance(UPDATE_ICON);
        }

        foreach (obj_machinery_atmospherics_components_unary_vent_scrubber scrubber in Dm.each_cast<obj_machinery_atmospherics_components_unary_vent_scrubber>(applied.air_scrubbers))
        {
            scrubber.on = FALSE;
            scrubber.update_appearance(UPDATE_ICON);
        }
    }
}
