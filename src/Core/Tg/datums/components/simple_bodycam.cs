// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/simple_bodycam.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate N2c, D316: the operatives' bodycams the overwatch agent watches). The var block is generated (D111).

/// Simple component to integrate a bodycam into a mob
// tg: code/datums/components/simple_bodycam.dm:2
public partial class datum_component_simple_bodycam : datum_component
{
    // tg: code/datums/components/simple_bodycam.dm:9
    [DmParams("camera_name", "c_tag", "network", "emp_proof", "camera_update_time")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object camera_name = Dm.arg(__extra, 0) ?? "bodycam";
        object c_tag = Dm.arg(__extra, 1) ?? capitalize(camera_name);
        object network = Dm.arg(__extra, 2) ?? "ss13";
        object emp_proof = Dm.arg(__extra, 3) ?? FALSE;
        object camera_update_time = Dm.arg(__extra, 4) ?? 0.5f * SECONDS;
        if (!isliving(parent))
            return COMPONENT_INCOMPATIBLE;

        this.camera_update_time = Dm.num(camera_update_time);

        bodycam = Dm.@new<obj_machinery_camera>(parent);
        bodycam.network = Dm.list(network);
        bodycam.name = camera_name as string;
        bodycam.c_tag = c_tag as string;
        if (Dm.truthy(emp_proof))
            await bodycam.AddElement(typeof(datum_element_empprotection), EMP_PROTECT_ALL);

        RegisterSignal(parent, COMSIG_MOVABLE_MOVED, nameof(update_cam));
        RegisterSignal(parent, COMSIG_ATOM_DIR_CHANGE, nameof(rotate_cam));
        RegisterSignals(bodycam, Dm.list(COMSIG_QDELETING, COMSIG_MOVABLE_MOVED), nameof(camera_gone));

        do_update_cam(null);
        return null;
    }

    // tg: code/datums/components/simple_bodycam.dm:34
    public override object Destroy(float? force = null)
    {
        if (QDELETED(bodycam))
            bodycam = null;
        else
            QDEL_NULL(ref bodycam);
        return base.Destroy(force);
    }

    // tg: code/datums/components/simple_bodycam.dm:41
    [DmParams("new_bodycam", "camera_name", "c_tag", "network", "emp_proof", "camera_update_time")]
    public override async DmTask<object> CheckDupeComponent(datum_component C = null, params object[] __extra)
    {
        object network = Dm.arg(__extra, 2) ?? "ss13";
        // Dupes are only allowed if we don't have a camera on that network already
        return Dm.@in(network, bodycam.network) ? TRUE : FALSE;
    }

    // tg: code/datums/components/simple_bodycam.dm:52
    [SignalHandler]
    public virtual void update_cam(datum source = null, atom old_loc = null, params object[] __extra)
    {
        do_update_cam(old_loc);
    }

    // tg: code/datums/components/simple_bodycam.dm:56
    public virtual void do_update_cam(atom old_loc = null)
    {
        if (!Dm.truthy(bodycam?.can_use()))
            return;
        SScameras.camera_moved(bodycam, get_turf(old_loc), get_turf(bodycam), camera_update_time);
    }

    // tg: code/datums/components/simple_bodycam.dm:61
    [SignalHandler]
    public virtual void rotate_cam(datum source = null, object old_dir = null, object new_dir = null)
    {
        // I don't actually think cameras care about dir but just in case
        Waiting.NoSleep(bodycam.setDir(new_dir));
    }

    // tg: code/datums/components/simple_bodycam.dm:66
    [SignalHandler]
    public virtual void camera_gone(datum source = null, params object[] __extra)
    {
        if (!QDELETED(this))
            qdel(this);
    }
}
