// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/radio.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M9 slice D: the radio).

// For information on what objects or departments use what frequencies,
// see __DEFINES/radio.dm. Mappers may also select additional frequencies for
// use in maps, such as in intercoms.

public static partial class GlobalProcs
{
    /// Ensure the frequency is within bounds of what it should be sending/receiving at
    // tg: code/__HELPERS/radio.dm:2
    public static float sanitize_frequency(object frequency = null, object free = null, object syndie = null)
    {
        free ??= FALSE;
        syndie ??= FALSE;
        float freq = Dm.round(Dm.num(frequency));
        float dot;
        if (Dm.truthy(free))
            dot = Dm.clamp(freq, MIN_FREE_FREQ, MAX_FREE_FREQ);
        else
            dot = Dm.clamp(freq, MIN_FREQ, MAX_FREQ);
        if (!Dm.truthy(Dm.mod(dot, 2))) // Ensure the last digit is an odd number
            dot += 1;
        if (dot == FREQ_SYNDICATE && !Dm.truthy(syndie)) // Prevents people from picking (or rounding up) into the syndie frequency
            dot = FREQ_COMMON;
        return dot;
    }

    /// Format frequency by moving the decimal.
    // tg: code/__HELPERS/radio.dm:15
    public static string format_frequency(object frequency = null)
    {
        float freq = Dm.num(text2num(frequency));
        return Dm.text("[].[]", Dm.round(freq / 10), Dm.mod(freq, 10));
    }

    ///Opposite of format, returns as a number
    // tg: code/__HELPERS/radio.dm:20
    public static float unformat_frequency(object frequency = null)
    {
        return Dm.num(text2num(frequency)) * 10;
    }

    ///returns a random unused frequency between MIN_FREE_FREQ & MAX_FREE_FREQ if free = TRUE, and MIN_FREQ & MAX_FREQ if FALSE
    // tg: code/__HELPERS/radio.dm:25
    public static float return_unused_frequency(object free = null)
    {
        free ??= FALSE;
        float start = Dm.truthy(free) ? MIN_FREE_FREQ : MIN_FREQ;
        float end = Dm.truthy(free) ? MAX_FREE_FREQ : MAX_FREQ;

        float freq_to_check = 0;
        do
        {
            freq_to_check = Dm.rand(start, end);
            if (!Dm.truthy(Dm.mod(freq_to_check, 2))) // Ensure the last digit is an odd number
                freq_to_check++;
        }
        while (freq_to_check == 0 || Dm.@in(Dm.text("[]", freq_to_check), GLOB.reserved_radio_frequencies));

        return freq_to_check;
    }
}
