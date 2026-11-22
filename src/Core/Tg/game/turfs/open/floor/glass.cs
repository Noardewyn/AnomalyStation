// Translated from tgstation (snapshot 1d99bd7f): code/game/turfs/open/floor/glass.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps G2 (D291): the whole file. A glass floor at the bottom of its stack glows with the starlight below it (tg's light of
// range 2); its z transparency since D325 (multi-z). Left out: the emissive blockers that let the space below glow through
// (DEVIATION DV12, lighting planes).

// tg: code/game/turfs/open/floor/glass.dm:1
public partial class turf_open_floor_glass : turf_open_floor
{
    // tg: code/game/turfs/open/floor/glass.dm:25
    public override object broken_states()
    {
        return Dm.list("glass-damaged1", "glass-damaged2", "glass-damaged3");
    }

    // tg: code/game/turfs/open/floor/glass.dm:28
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        icon_state = ""; //Prevent the normal icon from appearing behind the smooth overlays
        base.Initialize(mapload, __extra);
        return INITIALIZE_HINT_LATELOAD;
    }

    // tg: code/game/turfs/open/floor/glass.dm:33
    public override void LateInitialize(params object[] __extra)
    {
        ADD_TURF_TRANSPARENCY(this, INNATE_TRAIT);
        Waiting.NoSleep(setup_glow());
    }

    // tg: code/game/turfs/open/floor/glass.dm:37
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);
        QDEL_LIST(glow_stuff);
        UnregisterSignal(SSdcs, COMSIG_STARLIGHT_COLOR_CHANGED);
        return dot;
    }

    /// If this turf is at the bottom of the local rendering stack
    /// Then we're gonna make it emissive block so the space below glows
    // tg: code/game/turfs/open/floor/glass.dm:44
    public virtual async DmTask setup_glow()
    {
        // DEVIATION(DV367): tg's plane offsets are all 0 in the port, so the bottom of the stack is a turf with none below it
        if (GET_TURF_BELOW(this) != null) // We ain't the bottom brother
            return;
        // We assume no parallax means no space means no light
        if (Dm.truthy(SSmapping.level_trait(z, ZTRAIT_NOPARALLAX)))
            return;

        // DEVIATION(DV12): glow_stuff = partially_block_emissives(src, alpha_to_leave), the render steps that cut the emissive
        // blockers away, are lighting planes
        Dm.Deferred("code/game/turfs/open/floor/glass.dm:51", "setup_glow: partially_block_emissives (DEVIATION DV12)");
        if (!Dm.truthy(starlight_color))
            RegisterSignal(SSdcs, COMSIG_STARLIGHT_COLOR_CHANGED, nameof(starlight_changed));
        else
            UnregisterSignal(SSdcs, COMSIG_STARLIGHT_COLOR_CHANGED);
        await set_light(2, 1, Dm.truthy(starlight_color) ? starlight_color : GLOB.starlight_color, l_height: LIGHTING_HEIGHT_SPACE);
    }

    // tg: code/game/turfs/open/floor/glass.dm:58
    public virtual async DmTask starlight_changed(datum source = null, object old_star = null, object new_star = null)
    {
        if (Dm.eq(light_color, old_star))
            await set_light(l_color: new_star);
    }
}
