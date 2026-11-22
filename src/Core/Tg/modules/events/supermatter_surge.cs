// Translated from tgstation (snapshot 1d99bd7f): code/modules/events/supermatter_surge.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E1b (D327): the whole file, the supermatter surge (nitrogen keeps more of the crystal's energy, its heat and power output rise
// and the emitters' bullets carry more energy for four minutes or so) and Poly's revenge, but its admin option, the admin's choice of
// the surge class (/datum/event_admin_setup/input_number/surge_spiciness: the admins' Trigger Event panel, D275). The var blocks are
// generated (D111).

/**
 * Supermatter Surge
 *
 * An engineering challenge event where the properties of the SM changes to be in a 'surge' of power.
 * For the duration of the event a powerloss inhibition is added to nitrogen, causing the crystal to retain more of its internal energy.
 * Heat modifier is lowered to generate some heat but not a high temp burn.
 * Bullet energy from emitters is raised slightly to raise meV while turned on.
 */
// tg: code/modules/events/supermatter_surge.dm:26
public partial class datum_round_event_control_supermatter_surge : datum_round_event_control
{
    // tg: code/modules/events/supermatter_surge.dm:40
    public override object can_spawn_event(object players_amt = null, float? allow_magic = null)
    {
        object dot = base.can_spawn_event(players_amt, allow_magic);

        if (!Dm.truthy(SSjob.has_minimum_jobs(crew_threshold: 3, jobs: Dm.list(JOB_STATION_ENGINEER, JOB_ATMOSPHERIC_TECHNICIAN) /* JOB_GROUP_ENGINEERS */, head_jobs: Dm.list(JOB_CHIEF_ENGINEER))))
            return FALSE;
        return dot;
    }
}

// tg: code/modules/events/supermatter_surge.dm:46
public partial class datum_round_event_supermatter_surge : datum_round_event
{
    // tg: code/modules/events/supermatter_surge.dm:1-14  the file-local defines
    private const float SURGE_DURATION_MIN = 240 * 0.5f; // 240 EVENT_SECONDS
    private const float SURGE_DURATION_MAX = 270 * 0.5f; // 270 EVENT_SECONDS
    private const float SURGE_SEVERITY_RANDOM = 5;
    /// The amount of bullet energy we add for the duration of the SM surge
    private const float SURGE_BULLET_ENERGY_ADDITION = 5;
    /// The amount of powerloss inhibition (energy retention) we add for the duration of the SM surge
    private const float SURGE_BASE_POWERLOSS_INHIBITION = 0.55f;
    /// The powerloss inhibition scaling based on surge severity
    private const float SURGE_POWERLOSS_INHIBITION_MODIFIER = 0.175f;
    /// The power generation scaling based on surge severity
    private const float SURGE_POWER_GENERATION_MODIFIER = 0.075f;
    /// The heat modifier scaling based on surge severity
    private const float SURGE_HEAT_MODIFIER = 0.25f;

    // tg: code/modules/events/supermatter_surge.dm:68
    public override async DmTask<object> setup()
    {
        engine = GLOB.main_supermatter_engine;
        if (engine == null)
        {
            stack_trace("SM surge event failed to find a supermatter engine!", "code/modules/events/supermatter_surge.dm", 71);
            return null;
        }

        sm_gas = LAZYACCESS(engine.current_gas_behavior, typeof(datum_gas_nitrogen)) as datum_sm_gas_nitrogen;
        if (sm_gas == null)
        {
            stack_trace(Dm.text("SM surge event failed to find gas properties for [].", engine), "code/modules/events/supermatter_surge.dm", 76);
            return null;
        }

        if (surge_class == SURGE_SEVERITY_RANDOM)
        {
            float severity_weight = rand(1, 100);
            if (severity_weight >= 1 && severity_weight <= 14)
                surge_class = 1;
            else if (severity_weight >= 15 && severity_weight <= 34)
                surge_class = 2;
            else if (severity_weight >= 35 && severity_weight <= 69)
                surge_class = 3;
            else if (severity_weight >= 70 && severity_weight <= 100)
                surge_class = 4;
        }

        end_when = rand(SURGE_DURATION_MIN, SURGE_DURATION_MAX);
        return null;
    }

    // tg: code/modules/events/supermatter_surge.dm:93
    public override async DmTask announce__proc(object fake = null)
    {
        object class_to_announce = Dm.truthy(fake) ? pick(1, 2, 3, 4) : surge_class;
        priority_announce("The Crystal Integrity Monitoring System has detected unusual atmospheric properties in the supermatter chamber, energy output from the supermatter crystal has increased significantly. Engineering intervention is required to stabilize the engine.", Dm.text("Class [] Supermatter Surge Alert", class_to_announce), "sound/machines/engine_alert/engine_alert3.ogg");
    }

    // tg: code/modules/events/supermatter_surge.dm:97
    public override async DmTask<object> start()
    {
        engine.bullet_energy = surge_class + SURGE_BULLET_ENERGY_ADDITION;
        sm_gas.powerloss_inhibition = (surge_class * SURGE_POWERLOSS_INHIBITION_MODIFIER) + SURGE_BASE_POWERLOSS_INHIBITION;
        sm_gas.heat_power_generation = (surge_class * SURGE_POWER_GENERATION_MODIFIER) - 1;
        sm_gas.heat_modifier = (surge_class * SURGE_HEAT_MODIFIER) - 1;
        return null;
    }

    // tg: code/modules/events/supermatter_surge.dm:103
    public override async DmTask end()
    {
        engine.bullet_energy = Dm.initial(engine, (obj_machinery_power_supermatter_crystal p) => p.bullet_energy);
        sm_gas.powerloss_inhibition = Dm.initial(sm_gas, (datum_sm_gas p) => p.powerloss_inhibition);
        sm_gas.heat_power_generation = Dm.initial(sm_gas, (datum_sm_gas p) => p.heat_power_generation);
        sm_gas.heat_modifier = Dm.initial(sm_gas, (datum_sm_gas p) => p.heat_modifier);
        priority_announce("The supermatter surge has dissipated, crystal output readings have normalized.", "Anomaly Cleared");
        engine = null;
        sm_gas = null;
    }
}

// tg: code/modules/events/supermatter_surge.dm:123
public partial class datum_round_event_supermatter_surge_poly : datum_round_event_supermatter_surge
{
    // tg: code/modules/events/supermatter_surge.dm:128
    public override async DmTask announce__proc(object fake = null)
    {
        priority_announce("The Crystal Integrity Monitoring System has detected unusual parrot type resonance in the supermatter chamber, energy output from the supermatter crystal has increased significantly. Engineering intervention is required to stabilize the engine.", "Class P Supermatter Surge Alert", "sound/machines/engine_alert/engine_alert3.ogg");
    }
}
