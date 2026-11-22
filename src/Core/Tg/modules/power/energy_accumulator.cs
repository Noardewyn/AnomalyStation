// Translated from tgstation (snapshot 1d99bd7f): code/modules/power/energy_accumulator.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The M7 zaps slice: the whole file (should_have_node came with the owner's play test 2026-10-09: a cable's icon reads it). The
// var block is generated (D111).

/// Abstract type for generators that accumulate energy over time and slowly release it
/// eg. radiation collectors, tesla coils
// tg: code/modules/power/energy_accumulator.dm:19
public partial class obj_machinery_power_energy_accumulator : obj_machinery_power
{
    /// The coefficient of the proportionate power output.
    /// Doesn't change output total, lower numbers just increases the smoothing - taking longer to ramp up, and longer to drop away.
    /// 4% means an accumulator, when starting up for the first time:
    /// - emits   50% of what is being received after 40 seconds
    /// - emits   90% of what is being received after two minutes
    /// - emits   99% of what is being received after four minutes
    /// after having stored energy for at least 4-5 minutes, then dropping input to nothing:
    /// - emits   50% of what was previously being received after 40 seconds
    /// - emits   25% of what was previously being received after 79 seconds
    /// - emits   10% of what was previously being received after two minutes
    /// - emits    1% of what was previously being received after four minutes
    // tg: code/modules/power/energy_accumulator.dm:12
    private const float ACCUMULATOR_PROPORTIONAL_COEFFICIENT = 0.04f;
    /// The coefficient for the constant power output.
    // tg: code/modules/power/energy_accumulator.dm:14
    private const int ACCUMULATOR_CONSTANT_COEFFICIENT = 2000;

    // tg: code/modules/power/energy_accumulator.dm:28
    public virtual object get_stored_joules()
    {
        return stored_energy;
    }

    /**
     * Gets the energy the energy_accumulator would release within the given timespan time.
     * The power output is proportional to the energy, and has a constant power output added to it.
     * Args:
     * - time: The amount of time that is being processed, in seconds.
     * Returns: The amount of energy it would release in the timespan.
     */
    // tg: code/modules/power/energy_accumulator.dm:38
    public virtual object calculate_energy_output(float? time = null)
    {
        time ??= 0;
        // dE/dt = -[ACCUMULATOR_PROPORTIONAL_COEFFICIENT] * E - [ACCUMULATOR_CONSTANT_COEFFICIENT]
        return min(stored_energy, stored_energy - ((ACCUMULATOR_PROPORTIONAL_COEFFICIENT * stored_energy + ACCUMULATOR_CONSTANT_COEFFICIENT) * Dm.pow(NUM_E, (-ACCUMULATOR_PROPORTIONAL_COEFFICIENT * time.Value)) - ACCUMULATOR_CONSTANT_COEFFICIENT) / ACCUMULATOR_PROPORTIONAL_COEFFICIENT);
    }

    /**
     * Calculates the power needed to sustain the energy accumulator at its current energy.
     */
    // tg: code/modules/power/energy_accumulator.dm:45
    public virtual object calculate_sustainable_power()
    {
        return ACCUMULATOR_PROPORTIONAL_COEFFICIENT * stored_energy + ACCUMULATOR_CONSTANT_COEFFICIENT;
    }

    // tg: code/modules/power/energy_accumulator.dm:48
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        release_energy(Dm.num(calculate_energy_output(Dm.num(seconds_per_tick))));
        return null;
    }

    /**
     * Releases joules amount of its stored energy onto the powernet.
     * Args:
     * - joules: The amount of energy to release.
     * Returns: Whether it successfully released its energy or not.
     */
    // tg: code/modules/power/energy_accumulator.dm:57
    public virtual object release_energy(float? joules = null)
    {
        joules ??= 0;
        if (Dm.truthy(wants_powernet))
        {
            add_avail(joules);
            stored_energy -= joules.Value;
            processed_energy = joules.Value;
            return TRUE;
        }
        return FALSE;
    }

    // tg: code/modules/power/energy_accumulator.dm:65
    public override object should_have_node()
    {
        return Dm.truthy(wants_powernet) && Dm.truthy(anchored) ? TRUE : FALSE;
    }

    // tg: code/modules/power/energy_accumulator.dm:68
    public override async DmTask<object> set_anchored(object anchorvalue = null)
    {
        object dot = null;
        dot = await base.set_anchored(anchorvalue);
        if (dot == null)
            return dot; //no need to process if we didn't change anything.
        if (!Dm.truthy(wants_powernet))
            return dot;
        if (!Dm.truthy(anchorvalue))
        {
            disconnect_from_network();
            return dot;
        }
        connect_to_network();
        return dot;
    }
}
