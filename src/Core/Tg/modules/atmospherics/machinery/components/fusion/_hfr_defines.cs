// Translated from tgstation (snapshot 1d99bd7f): code/modules/atmospherics/machinery/components/fusion/_hfr_defines.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;

namespace Anomaly.Core.Tg;

// Package E12b (docs/ai/specs/engineering.md): the hypertorus fusion reactor's defines, the whole file. The file never #undefs them and the
// .dme includes it outside code/__DEFINES, so the defines converter does not emit them: they are public members of GlobalProcs (rules 11.4).

public static partial class GlobalProcs
{
    ///Speed of light, in m/s
    // tg: code/modules/atmospherics/machinery/components/fusion/_hfr_defines.dm:2
    public const float LIGHT_SPEED = 299792458;
    ///Calculation between the plank constant and the lambda of the lightwave
    // tg: code/modules/atmospherics/machinery/components/fusion/_hfr_defines.dm:4
    public const float PLANCK_LIGHT_CONSTANT = 2e-16f;
    ///Radius of the h2 calculated based on the amount of number of atom in a mole (and some addition for balancing issues)
    // tg: code/modules/atmospherics/machinery/components/fusion/_hfr_defines.dm:6
    public const float CALCULATED_H2RADIUS = 120e-4f;
    ///Radius of the trit calculated based on the amount of number of atom in a mole (and some addition for balancing issues)
    // tg: code/modules/atmospherics/machinery/components/fusion/_hfr_defines.dm:8
    public const float CALCULATED_TRITRADIUS = 230e-3f;
    ///Power conduction in the void, used to calculate the efficiency of the reaction
    // tg: code/modules/atmospherics/machinery/components/fusion/_hfr_defines.dm:10
    public const float VOID_CONDUCTION = 1e-2f;
    ///Mole count required (tritium/hydrogen) to start a fusion reaction
    // tg: code/modules/atmospherics/machinery/components/fusion/_hfr_defines.dm:12
    public const int FUSION_MOLE_THRESHOLD = 25;
    ///Used to reduce the gas_power to a more useful amount
    // tg: code/modules/atmospherics/machinery/components/fusion/_hfr_defines.dm:14
    public const float INSTABILITY_GAS_POWER_FACTOR = 0.003f;
    ///Used to calculate the toroidal_size for the instability
    // tg: code/modules/atmospherics/machinery/components/fusion/_hfr_defines.dm:16
    public const int TOROID_VOLUME_BREAKEVEN = 1000;
    ///Constant used when calculating the chance of emitting a radioactive particle
    // tg: code/modules/atmospherics/machinery/components/fusion/_hfr_defines.dm:18
    public const float PARTICLE_CHANCE_CONSTANT = -20000000;
    ///Conduction of heat inside the fusion reactor
    // tg: code/modules/atmospherics/machinery/components/fusion/_hfr_defines.dm:20
    public const float METALLIC_VOID_CONDUCTIVITY = 0.38f;
    ///Conduction of heat near the external cooling loop
    // tg: code/modules/atmospherics/machinery/components/fusion/_hfr_defines.dm:22
    public const float HIGH_EFFICIENCY_CONDUCTIVITY = 0.975f;
    ///Sets the minimum amount of power the machine uses
    // tg: code/modules/atmospherics/machinery/components/fusion/_hfr_defines.dm:24
    public const int MIN_POWER_USAGE = 50 * KILO * WATTS;
    ///Sets the multiplier for the damage
    // tg: code/modules/atmospherics/machinery/components/fusion/_hfr_defines.dm:26
    public const float DAMAGE_CAP_MULTIPLIER = 0.005f;
    ///Sets the range of the hallucinations
    // tg: code/modules/atmospherics/machinery/components/fusion/_hfr_defines.dm:28  #define HALLUCINATION_HFR(P) (min(7, round(abs(P) ** 0.25)))
    public static float HALLUCINATION_HFR(float P) => min(7, round(Dm.pow(abs(P), 0.25f)));
    ///Chance in percentage points per fusion level of iron accumulation when operating at unsafe levels
    // tg: code/modules/atmospherics/machinery/components/fusion/_hfr_defines.dm:30
    public const int IRON_CHANCE_PER_FUSION_LEVEL = 17;
    ///Amount of iron accumulated per second whenever we fail our saving throw, using the chance above
    // tg: code/modules/atmospherics/machinery/components/fusion/_hfr_defines.dm:32
    public const float IRON_ACCUMULATED_PER_SECOND = 0.005f;
    ///Maximum amount of iron that can be healed per second. Calculated to mostly keep up with fusion level 5.
    // tg: code/modules/atmospherics/machinery/components/fusion/_hfr_defines.dm:34
    public const float IRON_OXYGEN_HEAL_PER_SECOND = IRON_ACCUMULATED_PER_SECOND * (100 - IRON_CHANCE_PER_FUSION_LEVEL) / 100;
    ///Amount of oxygen in moles required to fully remove 100% iron content. Currently about 2409mol. Calculated to consume at most 10mol/s.
    // tg: code/modules/atmospherics/machinery/components/fusion/_hfr_defines.dm:36
    public const float OXYGEN_MOLES_CONSUMED_PER_IRON_HEAL = 10 / IRON_OXYGEN_HEAL_PER_SECOND;

    //If integrity percent remaining is less than these values, the monitor sets off the relevant alarm.
    // tg: code/modules/atmospherics/machinery/components/fusion/_hfr_defines.dm:39-42
    public const int HYPERTORUS_MELTING_PERCENT = 5;
    public const int HYPERTORUS_EMERGENCY_PERCENT = 25;
    public const int HYPERTORUS_DANGER_PERCENT = 50;
    public const int HYPERTORUS_WARNING_PERCENT = 100;

    // tg: code/modules/atmospherics/machinery/components/fusion/_hfr_defines.dm:44
    public const int WARNING_TIME_DELAY = 60;
    ///to prevent accent sounds from layering
    // tg: code/modules/atmospherics/machinery/components/fusion/_hfr_defines.dm:46
    public const int HYPERTORUS_ACCENT_SOUND_MIN_COOLDOWN = 3 * SECONDS;

    // tg: code/modules/atmospherics/machinery/components/fusion/_hfr_defines.dm:48
    public const int HYPERTORUS_COUNTDOWN_TIME = 30 * SECONDS;

    //
    // Damage source: Too much mass in the fusion mix at high fusion levels
    //
    /// Start taking overfull damage at this power level
    // tg: code/modules/atmospherics/machinery/components/fusion/_hfr_defines.dm:57
    public const int HYPERTORUS_OVERFULL_MIN_POWER_LEVEL = 6;
    /// Take 0 damage beneath this much fusion mass at 1 degree Kelvin
    // tg: code/modules/atmospherics/machinery/components/fusion/_hfr_defines.dm:59
    public const int HYPERTORUS_OVERFULL_MAX_SAFE_COLD_FUSION_MOLES = 2700;
    /// Take 0 damage beneath this much fusion mass at FUSION_TEMPERATURE_MAX degrees Kelvin
    // tg: code/modules/atmospherics/machinery/components/fusion/_hfr_defines.dm:61
    public const int HYPERTORUS_OVERFULL_MAX_SAFE_HOT_FUSION_MOLES = 1800;
    /// Every 200 moles, 1 point of damage per second
    // tg: code/modules/atmospherics/machinery/components/fusion/_hfr_defines.dm:64  (DM's 1/200 is a float division)
    public const float HYPERTORUS_OVERFULL_MOLAR_SLOPE = 1f / 200;
    // Derive the temperature slope from the molar slope
    // tg: code/modules/atmospherics/machinery/components/fusion/_hfr_defines.dm:72
    public const float HYPERTORUS_OVERFULL_TEMPERATURE_SLOPE = HYPERTORUS_OVERFULL_MOLAR_SLOPE * (HYPERTORUS_OVERFULL_MAX_SAFE_COLD_FUSION_MOLES - HYPERTORUS_OVERFULL_MAX_SAFE_HOT_FUSION_MOLES) / (FUSION_MAXIMUM_TEMPERATURE - 1);
    // Derive the constant to set damage = 0 at our desired thresholds above
    // tg: code/modules/atmospherics/machinery/components/fusion/_hfr_defines.dm:74
    public const float HYPERTORUS_OVERFULL_CONSTANT = -(HYPERTORUS_OVERFULL_MOLAR_SLOPE * HYPERTORUS_OVERFULL_MAX_SAFE_HOT_FUSION_MOLES + HYPERTORUS_OVERFULL_TEMPERATURE_SLOPE * FUSION_MAXIMUM_TEMPERATURE);

    //
    // Heal source: Small enough mass in the fusion mix
    //
    /// Start healing when fusion mass is below this threshold
    // tg: code/modules/atmospherics/machinery/components/fusion/_hfr_defines.dm:82
    public const int HYPERTORUS_SUBCRITICAL_MOLES = 1200;
    /// Heal one point per second per this many moles under the threshold
    // tg: code/modules/atmospherics/machinery/components/fusion/_hfr_defines.dm:84
    public const int HYPERTORUS_SUBCRITICAL_SCALE = 400;

    //
    // Heal source: Cold enough coolant
    //
    /// Heal up to this many points of damage per second at 1 degree kelvin
    // tg: code/modules/atmospherics/machinery/components/fusion/_hfr_defines.dm:92
    public const float HYPERTORUS_COLD_COOLANT_MAX_RESTORE = 2.5f;
    /// Start healing below this temperature
    // tg: code/modules/atmospherics/machinery/components/fusion/_hfr_defines.dm:94  (10 ** 5)
    public const int HYPERTORUS_COLD_COOLANT_THRESHOLD = 100000;
    // tg: code/modules/atmospherics/machinery/components/fusion/_hfr_defines.dm:96  #define HYPERTORUS_COLD_COOLANT_SCALE (HYPERTORUS_COLD_COOLANT_MAX_RESTORE / log(10, HYPERTORUS_COLD_COOLANT_THRESHOLD))
    public static float HYPERTORUS_COLD_COOLANT_SCALE => HYPERTORUS_COLD_COOLANT_MAX_RESTORE / Dm.log(10, HYPERTORUS_COLD_COOLANT_THRESHOLD);

    //
    // Damage source: Iron content
    //
    /// Start taking damage over this threshold, up to a maximum of (1 - HYPERTORUS_MAX_SAFE_IRON) per tick at 100% iron
    // tg: code/modules/atmospherics/machinery/components/fusion/_hfr_defines.dm:104
    public const float HYPERTORUS_MAX_SAFE_IRON = 0.35f;

    //
    // Damage source: Extreme levels of mass in fusion mix at any power level
    //
    /// Start taking damage over this threshold
    // tg: code/modules/atmospherics/machinery/components/fusion/_hfr_defines.dm:113
    public const int HYPERTORUS_HYPERCRITICAL_MOLES = 10000;
    /// Take this much damage per mole over the threshold per second
    // tg: code/modules/atmospherics/machinery/components/fusion/_hfr_defines.dm:115
    public const float HYPERTORUS_HYPERCRITICAL_SCALE = 0.002f;
    /// Take at most this much damage per second
    // tg: code/modules/atmospherics/machinery/components/fusion/_hfr_defines.dm:117
    public const int HYPERTORUS_HYPERCRITICAL_MAX_DAMAGE = 20;

    // If the moderator goes hypercritical, it cracks and starts to spill
    // If our pressure is weak, it can still spill, just weakly and infrequently
    // Even a small amount is still extremely hazardous with fusion temperatures
    // tg: code/modules/atmospherics/machinery/components/fusion/_hfr_defines.dm:122-123
    public const float HYPERTORUS_WEAK_SPILL_RATE = 0.0005f;
    public const int HYPERTORUS_WEAK_SPILL_CHANCE = 1;
    /// Start spilling superhot moderator gas when over this pressure threshold
    // tg: code/modules/atmospherics/machinery/components/fusion/_hfr_defines.dm:125
    public const int HYPERTORUS_MEDIUM_SPILL_PRESSURE = 10000;
    /// How much we should spill initially
    // tg: code/modules/atmospherics/machinery/components/fusion/_hfr_defines.dm:127
    public const float HYPERTORUS_MEDIUM_SPILL_INITIAL = 0.25f;
    /// How much of the moderator mix we should spill per second until mended
    // tg: code/modules/atmospherics/machinery/components/fusion/_hfr_defines.dm:129
    public const float HYPERTORUS_MEDIUM_SPILL_RATE = 0.01f;
    /// If the moderator gas goes over this threshold, REALLY spill it
    // tg: code/modules/atmospherics/machinery/components/fusion/_hfr_defines.dm:131
    public const int HYPERTORUS_STRONG_SPILL_PRESSURE = 12000;
    /// How much we should spill initially
    // tg: code/modules/atmospherics/machinery/components/fusion/_hfr_defines.dm:133
    public const float HYPERTORUS_STRONG_SPILL_INITIAL = 0.75f;
    /// How much of the moderator mix we should spill per second until mended
    // tg: code/modules/atmospherics/machinery/components/fusion/_hfr_defines.dm:135
    public const float HYPERTORUS_STRONG_SPILL_RATE = 0.05f;

    //
    // Explosion flags for use in fuel recipes
    //
    // tg: code/modules/atmospherics/machinery/components/fusion/_hfr_defines.dm:141-150
    public const int HYPERTORUS_FLAG_BASE_EXPLOSION = 1 << 0;
    public const int HYPERTORUS_FLAG_MEDIUM_EXPLOSION = 1 << 1;
    public const int HYPERTORUS_FLAG_DEVASTATING_EXPLOSION = 1 << 2;
    public const int HYPERTORUS_FLAG_RADIATION_PULSE = 1 << 3;
    public const int HYPERTORUS_FLAG_EMP = 1 << 4;
    public const int HYPERTORUS_FLAG_MINIMUM_SPREAD = 1 << 5;
    public const int HYPERTORUS_FLAG_MEDIUM_SPREAD = 1 << 6;
    public const int HYPERTORUS_FLAG_BIG_SPREAD = 1 << 7;
    public const int HYPERTORUS_FLAG_MASSIVE_SPREAD = 1 << 8;
    public const int HYPERTORUS_FLAG_CRITICAL_MELTDOWN = 1 << 9;

    ///High power damage
    // tg: code/modules/atmospherics/machinery/components/fusion/_hfr_defines.dm:153
    public const int HYPERTORUS_FLAG_HIGH_POWER_DAMAGE = 1 << 0;
    ///High fuel mix mole
    // tg: code/modules/atmospherics/machinery/components/fusion/_hfr_defines.dm:155
    public const int HYPERTORUS_FLAG_HIGH_FUEL_MIX_MOLE = 1 << 1;
    ///iron content damage
    // tg: code/modules/atmospherics/machinery/components/fusion/_hfr_defines.dm:157
    public const int HYPERTORUS_FLAG_IRON_CONTENT_DAMAGE = 1 << 2;
    ///Iron content increasing
    // tg: code/modules/atmospherics/machinery/components/fusion/_hfr_defines.dm:159
    public const int HYPERTORUS_FLAG_IRON_CONTENT_INCREASE = 1 << 3;
    ///Emped hypertorus
    // tg: code/modules/atmospherics/machinery/components/fusion/_hfr_defines.dm:161
    public const int HYPERTORUS_FLAG_EMPED = 1 << 4;
}
