// Translated from tgstation (snapshot 1d99bd7f): code/modules/lighting/lighting_object.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7 lumcount. DEVIATION(DV85): no /atom/movable/lighting_object is ever created on the server (one per lit turf, ~25k
// movables on MetaStation, whose only job in tg is to draw the corners' light; the client draws the light, D24). The turf's
// mark dm_lighting_object stands for its lighting_object in every test tg makes of it (get_lumcount, check_lumcount_*,
// is_softly_lit, ChangeTurf), and dm_lighting_object_new and dm_lighting_object_del keep the data effects of the object's
// Initialize (:18-56) and Destroy (:58-67). Not translated: update (:69-123, the object's appearance from the corners'
// cache_*: docs/ai/specs/not-translated.md section 42). The type stays declared for the vars that name it.

// tg: code/modules/lighting/lighting_object.dm:1
[DmPath("/atom/movable/lighting_object")]
public partial class atom_movable_lighting_object : atom_movable
{
}

// tg: code/modules/lighting/lighting_object.dm:125 (interleaved types, rules 1.9; M5x slice 5)
public partial class atom_movable_lighting_object : atom_movable
{
    // tg: code/modules/lighting/lighting_object.dm:125
    public override async DmTask<object> singularity_act()
    {
        return null;
    }

    // tg: code/modules/lighting/lighting_object.dm:128
    public override async DmTask<object> singularity_pull(atom singularity = null, object current_size = null)
    {
        return null;
    }
}

// tg: code/modules/lighting/lighting_object.dm:18 (DEVIATION DV85: the object's data effects on its turf)
public partial class turf : atom
{
    /// DEVIATION(DV85): TRUE while tg would hold a lighting object for this turf (turf.dm:67 lighting_object)
    public float dm_lighting_object = FALSE;

    // new /atom/movable/lighting_object(null, src): the data effects of the object's Initialize
    // tg: code/modules/lighting/lighting_object.dm:18
    public void dm_lighting_object_new()
    {
        // tg: :41-43  a turf that already has a lighting object gets a new one (stack_trace); the mark stays set
        dm_lighting_object = TRUE;
        // Default to fullbright, so things can "see" if they use view() before we update
        luminosity = 1;

        // This path is really hot. this is faster
        // Really this should be a global var or something, but lets not think about that yes?
        foreach (turf_open_space space_tile in Dm.each<turf_open_space>(RANGE_TURFS(1, this)))
            Waiting.NoSleep(space_tile.enable_starlight());
        // tg: :54-55  needs_update = TRUE; SSlighting.objects_queue += src: the visual update (DV85)
    }

    // qdel(lighting_object, force = TRUE): the data effects of the object's Destroy
    // tg: code/modules/lighting/lighting_object.dm:58
    public void dm_lighting_object_del()
    {
        dm_lighting_object = FALSE;
        luminosity = 1;
    }
}
