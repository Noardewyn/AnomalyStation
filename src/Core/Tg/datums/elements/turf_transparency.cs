// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/turf_transparency.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only /datum/element/turf_z_transparency translated (D325, multi-z). The z pillars (request_z_pillar, /datum/z_pillar,
// /obj/effect/abstract/z_holder, :1-174) and the element's underlays are BYOND's drawing of the level below a transparent turf
// (vis_contents of the turfs below, the base turf's underlay, a closed turf's girder): presentation, rules 13. The 3D client draws
// every level of a stack at its height and leaves out the floor of a z-transparent turf (open space). The element's trait
// (TURF_Z_TRANSPARENT_TRAIT, istransparentturf) is what the server's code reads: sounds through open space (playsound).

// tg: code/datums/elements/turf_transparency.dm:175
public partial class datum_element_turf_z_transparency : datum_element
{
    ///This proc sets up the signals to handle updating viscontents when turfs above/below update. Handle plane and layer here too so that they don't cover other obs/turfs in Dream Maker
    // tg: code/datums/elements/turf_transparency.dm:179
    [DmParams("target", "mapload")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object mapload = Dm.arg(__extra, 0);
        await base.Attach(target, __extra);
        if (!isturf(target))
            return ELEMENT_INCOMPATIBLE;

        turf our_turf = target as turf;

        RegisterSignal(target, COMSIG_TURF_MULTIZ_DEL, nameof(on_multiz_turf_del));
        RegisterSignal(target, COMSIG_TURF_MULTIZ_NEW, nameof(on_multiz_turf_new));

        if (!Dm.truthy(mapload))
            update_multi_z(our_turf);
        return null;
    }

    // tg: code/datums/elements/turf_transparency.dm:192
    public override object Detach(datum source = null, params object[] __extra)
    {
        object dot = base.Detach(source, __extra);
        turf our_turf = source as turf;
        clear_multiz(our_turf);

        UnregisterSignal(our_turf, Dm.list(COMSIG_TURF_MULTIZ_NEW, COMSIG_TURF_MULTIZ_DEL));
        return dot;
    }

    ///Updates the viscontents or underlays below this tile.
    // tg: code/datums/elements/turf_transparency.dm:200
    public virtual object update_multi_z(turf our_turf = null)
    {
        // tg: :201-223, the z pillars' vis_contents of the turfs below (the base turf's underlay with none below) and a closed
        // turf's girder and plating underlays: presentation (rules 13), the 3D client draws the stack itself (D325)
        Dm.Deferred("code/datums/elements/turf_transparency.dm:201", "update_multi_z: the z pillars and underlays that draw the level below (presentation, rules 13: the 3D client draws the stack, D325)");
        return TRUE;
    }

    // tg: code/datums/elements/turf_transparency.dm:227
    public virtual void clear_multiz(turf our_turf = null)
    {
        // tg: :228-245, the same pictures taken away (presentation, rules 13, D325)
        Dm.Deferred("code/datums/elements/turf_transparency.dm:228", "clear_multiz: the z pillars and underlays that draw the level below (presentation, rules 13: the 3D client draws the stack, D325)");
    }

    // tg: code/datums/elements/turf_transparency.dm:247
    [SignalHandler]
    public virtual void on_multiz_turf_del(turf our_turf = null, turf below_turf = null, object dir = null)
    {
        if (!Dm.eq(dir, DOWN))
            return;

        update_multi_z(our_turf);
    }

    // tg: code/datums/elements/turf_transparency.dm:255
    [SignalHandler]
    public virtual void on_multiz_turf_new(turf our_turf = null, turf below_turf = null, object dir = null)
    {
        if (!Dm.eq(dir, DOWN))
            return;

        update_multi_z(our_turf);
    }
}
