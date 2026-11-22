// Translated from tgstation (snapshot 1d99bd7f): code/game/turfs/open/space/space_EXPENSIVE.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/game/turfs/open/space/space_EXPENSIVE.dm:13
public partial class turf_open_space : turf_open
{
    /**
     * Space Initialize
     *
     * Doesn't call parent, see [/atom/proc/Initialize].
     * When adding new stuff to /atom/Initialize, /turf/Initialize, etc
     * don't just add it here unless space actually needs it.
     *
     * There is a lot of work that is intentionally not done because it is not currently used.
     * This includes stuff like smoothing, blocking camera visibility, etc.
     * If you are facing some odd bug with specifically space, check if it's something that was
     * intentionally ommitted from this implementation.
     */
    // tg: code/game/turfs/open/space/space_EXPENSIVE.dm:13  (SHOULD_CALL_PARENT(FALSE) is dropped, rules 7.9)
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        air = space_gas;

        if (Dm.truthy(FALSE)) // PERFORM_ALL_TESTS(focus_only/multiple_space_initialization) is FALSE in production (rules 1.7)
        {
            if (Dm.truthy(Dm.bitand(flags_1, INITIALIZED_1)))
                stack_trace(Dm.text("Warning: []([]) initialized multiple times!", this, type), "code/game/turfs/open/space/space_EXPENSIVE.dm", 19);
        }
        Dm.combine(ref flags_1, INITIALIZED_1);

        // We make the assumption that the space plane will never be blacklisted, as an optimization
        if (Dm.truthy(SSmapping.max_plane_offset))
            plane = Dm.num(Dm.sub(PLANE_SPACE, Dm.mul(PLANE_RANGE, SSmapping.z_level_to_plane_offset[z])));

        area our_area = loc as area;
        if (!Dm.truthy(our_area.area_has_base_lighting) && Dm.truthy(space_lit)) //Only provide your own lighting if the area doesn't for you
        {
            // Intentionally not add_overlay for performance reasons.
            // add_overlay does a bunch of generic stuff, like creating a new list for overlays,
            // queueing compile, cloning appearance, etc etc etc that is not necessary here.
            // overlays += GLOB.starlight_overlays[GET_TURF_PLANE_OFFSET(src) + 1]: GLOB.starlight_overlays
            // (modules/lighting/static_lighting_area.dm:27) is lighting, which is not ported (D24, rules 13).
            Dm.Deferred("code/game/turfs/open/space/space_EXPENSIVE.dm:31", "Initialize: overlays += GLOB.starlight_overlays[...]: the starlight overlay is lighting, not ported (D24, rules 13)");
        }

        if (!Dm.truthy(mapload))
        {
            if (Dm.truthy(requires_activation))
                SSair.add_to_active(this, TRUE);

            if (Dm.truthy(SSmapping.max_plane_offset))
            {
                turf T = GET_TURF_ABOVE(this) as turf;
                if (Dm.truthy(T))
                    T.multiz_turf_new(this, DOWN);
                T = GET_TURF_BELOW(this) as turf;
                if (Dm.truthy(T))
                    T.multiz_turf_new(this, UP);
            }
        }

        return INITIALIZE_HINT_NORMAL;
    }
}
