// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/rcd.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// E13 (the RCD family): the whole file, the RCD's destruction scan (its action button): a fading hologram of the wall or window
// that each remembered tile of the area had, a hologram (makeHologram, silicons SI4) that fades.

public static partial class GlobalProcs
{
    /// How many tiles within player radius does it perform a rcd scan in
    // tg: code/game/objects/effects/rcd.dm:2  #define RCD_DESTRUCTIVE_SCAN_RANGE 10 (file-local, rules 11.4)
    private const float RCD_DESTRUCTIVE_SCAN_RANGE = 10;

    /**
     * Global proc that generates RCD hologram in a range.
     *
     * Arguments:
     * * source - The atom the scans originate from
     * * scan_range - The range of turfs we grab from the source
     * * fade_time - The time for RCD holograms to fade
     */
    // tg: code/game/objects/effects/rcd.dm:12
    public static void rcd_scan(atom source = null, float? scan_range = null, float? fade_time = null)
    {
        scan_range ??= RCD_DESTRUCTIVE_SCAN_RANGE;
        fade_time ??= RCD_HOLOGRAM_FADE_TIME;
        playsound(source, Dm.resource("sound/items/tools/rcdscan.ogg"), 50, vary: TRUE, pressure_affected: FALSE);

        turf source_turf = get_turf(source);
        foreach (turf surrounding_turf in Dm.each_cast<turf>(RANGE_TURFS(scan_range, source_turf)))
        {
            object rcd_memory = surrounding_turf.rcd_memory;
            if (!Dm.truthy(rcd_memory))
                continue;

            bool skip_to_next_turf = false;

            foreach (atom content_of_turf in Dm.each_cast<atom>(surrounding_turf.contents))
            {
                if (Dm.truthy(content_of_turf.density))
                {
                    skip_to_next_turf = true;
                    break;
                }
            }

            if (skip_to_next_turf)
                continue;

            object hologram_icon = null;
            object sw__1 = rcd_memory;
            if (Dm.eq(sw__1, RCD_MEMORY_WALL))
                hologram_icon = GLOB.icon_holographic_wall;
            else if (Dm.eq(sw__1, RCD_MEMORY_WINDOWGRILLE))
                hologram_icon = GLOB.icon_holographic_window;

            obj_effect_rcd_hologram hologram = Dm.@new<obj_effect_rcd_hologram>(surrounding_turf);
            hologram.icon = hologram_icon;
            hologram.makeHologram();
            animate(hologram, Dm.named("alpha", 0), Dm.named("time", fade_time), Dm.named("easing", Dm.bitor(CIRCULAR_EASING, EASE_IN)));
        }
    }
}
