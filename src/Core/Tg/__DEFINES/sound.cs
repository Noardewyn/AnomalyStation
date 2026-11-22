// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/sound.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M4b audio, slice 1): the function-like macro CALCULATE_SOUND_VOLUME_RATIO and GLOBAL_LIST_EMPTY(sfx_datum_by_key);
// the constant defines of the file (the channels, ranges, falloff, environments and the SFX keys) are generated into
// Defines.

public static partial class GlobalProcs
{
    /* Calculates the volume of a sound based on distance
     *
     * https://www.desmos.com/calculator/ing6lxgd0m Update this when changed please
     *
     * Arguments:
     * * volume: The initial volume of the sound being played
     * * distance: How far away the sound is in tiles from the source
     * * falloff_distance: Distance at which falloff begins. Sound is at peak volume (in regards to falloff) aslong as it is in this range.
     * * falloff_exponent: Rate of falloff for the audio. Higher means quicker drop to low volume. Should generally be over 1 to indicate a quick dive to 0 rather than a slow dive.
     * Returns: The max distance of a sound based on audible volume range
     */
    // tg: code/__DEFINES/sound.dm:32  #define CALCULATE_SOUND_VOLUME_RATIO(volume, distance, max_distance, falloff_distance, falloff_exponent)
    // ((max(distance - falloff_distance, 0) / (max(max_distance, distance) - falloff_distance)) ** (1 / falloff_exponent))
    // (volume is a parameter the expansion never reads)
    public static float CALCULATE_SOUND_VOLUME_RATIO(object volume, object distance, object max_distance, object falloff_distance, object falloff_exponent)
        => Dm.pow(Dm.div(max(Dm.num(distance) - Dm.num(falloff_distance), 0f), (Dm.num(max(max_distance, distance)) - Dm.num(falloff_distance))), Dm.div(1f, Dm.num(falloff_exponent)));
}

public partial class datum_controller_global_vars
{
    // tg: code/__DEFINES/sound.dm:164  GLOBAL_LIST_EMPTY(sfx_datum_by_key)
    public DmList sfx_datum_by_key;

    // tg: code/__DEFINES/sound.dm:164  GLOBAL_LIST_EMPTY(sfx_datum_by_key): GLOBAL_MANAGED (__DEFINES/__globals.dm:5-9)
    public virtual void InitGlobalsfx_datum_by_key()
    {
        sfx_datum_by_key = Dm.list();
        Dm.append(ref gvars_datum_init_order, nameof(sfx_datum_by_key));
    }
}
