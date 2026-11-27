// Translated from tgstation (snapshot 1d99bd7f): code/modules/lighting/light_middleman.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The MetaStation leftovers. DEVIATION(DV688): tg's middleman intercepts its parent's overlay light (a render target over the light's
// mask, set as the light's render source) so that its owner can animate the light's picture: fire_flicker_middleman loops the light's
// alpha between 235 and 255 to flicker a flame (a lighter, a welder, a flare, the life candle). The client draws its own lights (D24)
// and has no light masks, so the middleman keeps tg's life (made, overriding, stopped, deleted) and its update signal, which is raised
// when it starts overriding (tg raises it each time an overlay light is applied, which here the client's light always is), and the
// flicker marks the parent's light (dm_light_flicker), which the light state carries to the client (protocol version 99). The
// intercept objects, their render targets and vis_contents are not made.

public partial class atom : datum
{
    /// <summary>
    /// DV688: the atom's light flickers as a flame (tg's fire_flicker_middleman on its middleman's light), until the middleman stops
    /// overriding it. Sent with the light (protocol version 99).
    /// </summary>
    public bool dm_light_flicker;
}

// tg: code/modules/lighting/light_middleman.dm:4
public partial class datum_light_middleman : datum
{
    // tg: code/modules/lighting/light_middleman.dm:20
    [DmParams("parent", "unique_string")]
    public override async DmTask New(params object[] __extra)
    {
        atom parent = Dm.arg(__extra, 0) as atom;
        await base.New();
        if (parent == null || !(parent.light_system == OVERLAY_LIGHT || parent.light_system == OVERLAY_LIGHT_DIRECTIONAL || parent.light_system == OVERLAY_LIGHT_BEAM)) // IS_OVERLAY_LIGHT_SYSTEM, inline
            stack_trace(Dm.text("Attempted to create a light middleman with a parent [] that does not use overlay lighting! This will not work.", parent?.type), "code/modules/lighting/light_middleman.dm", 23);
        if (isturf(parent))
            stack_trace("Warning, becuase overlay lights are basically never used on turfs, since they don't move, vis contents replacement has not yet been implemented for them (see changeturf for why this is needed)!", "code/modules/lighting/light_middleman.dm", 25);
        this.parent = parent as atom_movable;
        // tg's primary_intercept and cone_intercept with their render targets: not made (DV688)
    }

    // tg: code/modules/lighting/light_middleman.dm:37
    public override object Destroy(float? force = null)
    {
        stop_overriding_light();
        parent = null;
        light_holder = null;
        return base.Destroy(force);
    }

    // tg: code/modules/lighting/light_middleman.dm:44
    public virtual void being_overriding_light(object unique_string = null)
    {
        if (Dm.truthy(overriding))
            return;
        overriding = TRUE;
        // tg registers COMSIG_ATOM_OVERLAY_LIGHT_APPLIED and _REMOVED and sets the parent's light render source; the client's light is
        // always applied (DV688), so its owners hear the update now, as tg's light_applied tells them
        if (Dm.truthy(_listen_lookup?[COMSIG_LIGHT_MIDDLEMAN_UPDATED]))
            _SendSignal(COMSIG_LIGHT_MIDDLEMAN_UPDATED, Dm.list(this));
    }

    // tg: code/modules/lighting/light_middleman.dm:54
    public virtual void stop_overriding_light()
    {
        if (!Dm.truthy(overriding))
            return;
        overriding = FALSE;
        if (parent != null)
            parent.dm_light_flicker = false; // the light is its own again (parent.set_light_render_source(""))
    }
}

public static partial class GlobalProcs
{
    // Procs for reuse on multiple types
    // tg: code/modules/lighting/light_middleman.dm:125
    public static void fire_flicker_middleman(datum_light_middleman middleman = null)
    {
        // DV688: tg's 17 random flickers of the light's alpha (235 to 255, about a fifth of a second each) are the client's flame flicker
        if (middleman?.parent != null)
            middleman.parent.dm_light_flicker = true;
    }
}
