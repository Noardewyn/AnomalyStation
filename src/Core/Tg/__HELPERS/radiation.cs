// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/radiation.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7 radiation slice: the whole file. The var block of /datum/radiation_pulse_information is generated (D111).

public static partial class GlobalProcs
{
    /// Sends out a pulse of radiation, eminating from the source.
    /// Radiation is performed by collecting all radiatables within the max range (0 means source only, 1 means adjacent, etc),
    /// then makes their way towards them. A number, starting at 1, is multiplied
    /// by the insulation amounts of whatever is in the way (for example, walls lowering it down).
    /// If this number hits equal or below the threshold, then the target can no longer be irradiated.
    /// If the number is above the threshold, then the chance is the chance that the target will be irradiated.
    /// As a consumer, this means that max_range going up usually means you want to lower the threshold too,
    /// as well as the other way around.
    /// If max_range is high, but threshold is too high, then it usually won't reach the source at the max range in time.
    /// If max_range is low, but threshold is too low, then it basically guarantees everyone nearby, even if there's walls
    /// and such in the way, can be irradiated.
    /// You can also pass in a minimum exposure time. If this is set, then this radiation pulse
    /// will not irradiate the source unless they have been around *any* radioactive source for that
    /// period of time.
    /// The chance to get irradiated diminishes over range, and from objects that block radiation.
    /// Assuming there is nothing in the way, the chance will determine what the chance is to get irradiated from half of max_range.
    /// Example: If chance is equal to 30%, and max_range is equal to 8,
    /// then the chance for a thing to get irradiated is 30% if they are 4 turfs away from the pulse source.
    // tg: code/__HELPERS/radiation.dm:25
    public static object radiation_pulse(atom source = null, object max_range = null, object threshold = null, object chance = null, object minimum_exposure_time = null)
    {
        chance ??= DEFAULT_RADIATION_CHANCE;
        minimum_exposure_time ??= 0;
        // rules 10.6: tg's Master always creates SSradiation; a harness world without the Master has none, and pulses nothing.
        if (SSradiation == null || !Dm.truthy(SSradiation.can_fire))
            return null;

        datum_radiation_pulse_information pulse_information = Dm.@new<datum_radiation_pulse_information>();
        pulse_information.source_ref = WEAKREF(source) as datum_weakref;
        pulse_information.max_range = max_range;
        pulse_information.threshold = threshold;
        pulse_information.chance = chance;
        pulse_information.minimum_exposure_time = minimum_exposure_time;
        pulse_information.turfs_to_process = RANGE_TURFS(max_range, source);

        Dm.append(ref SSradiation.processing, pulse_information);

        return TRUE;
    }

    // tg: code/__HELPERS/radiation.dm:55
    private const float MEDIUM_RADIATION_THRESHOLD_RANGE = 0.5f;
    // tg: code/__HELPERS/radiation.dm:56
    private const float EXTREME_RADIATION_CHANCE = 30;

    /// Gets the perceived "danger" of radiation pulse, given the threshold to the target.
    /// Returns a RADIATION_DANGER_* define, see [code/__DEFINES/radiation.dm]
    // tg: code/__HELPERS/radiation.dm:60
    public static object get_perceived_radiation_danger(datum_radiation_pulse_information pulse_information = null, object insulation_to_target = null)
    {
        if (Dm.num(insulation_to_target) > Dm.num(pulse_information.threshold))
        {
            // We could get irradiated! The only thing stopping us now is chance, so scale based on that.
            if (Dm.num(pulse_information.chance) >= EXTREME_RADIATION_CHANCE)
                return PERCEIVED_RADIATION_DANGER_EXTREME;
            else
                return PERCEIVED_RADIATION_DANGER_HIGH;
        }
        else
        {
            // We're out of the threshold from being irradiated, but by how much?
            if (Dm.num(Dm.div(insulation_to_target, pulse_information.threshold)) <= MEDIUM_RADIATION_THRESHOLD_RANGE)
                return PERCEIVED_RADIATION_DANGER_MEDIUM;
            else
                return PERCEIVED_RADIATION_DANGER_LOW;
        }
    }
}

// tg: code/__HELPERS/radiation.dm:76
public partial class atom
{
    /// A common proc used to send COMSIG_ATOM_PROPAGATE_RAD_PULSE to adjacent atoms
    /// Only used for uranium (false/tram)walls to spread their radiation pulses
    // tg: code/__HELPERS/radiation.dm:76
    public virtual void propagate_radiation_pulse()
    {
        foreach (atom atom in Dm.each<atom>(orange(1, this)))
        {
            if (Dm.truthy(atom._listen_lookup?[COMSIG_ATOM_PROPAGATE_RAD_PULSE]))
                atom._SendSignal(COMSIG_ATOM_PROPAGATE_RAD_PULSE, Dm.list(atom, this));
        }
    }
}
