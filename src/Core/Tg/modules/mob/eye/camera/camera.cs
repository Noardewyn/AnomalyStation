// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/eye/camera/camera.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate N1c, D316: the camera eye of the infiltrator's navigation computer). The camera chunks it asks the
// camera net for (what a camera eye sees and the static over the rest) come with the AI's freelook (silicons SI4): until then
// SScameras.update_eye_chunk is a deferred no-op, so an eye has no visible chunk. The var block is generated (D111).

/**
 * Eye mob used to look around a [camera network][/datum/cameranet]. \
 * As it moves, it makes requests to the network to update what the user can and cannot see.
 */
// tg: code/modules/mob/eye/camera/camera.dm:5
public partial class mob_eye_camera : mob_eye
{
    // tg: code/modules/mob/eye/camera/camera.dm:20
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.append(ref GLOB.camera_eyes, this);
        return dot;
    }

    // tg: code/modules/mob/eye/camera/camera.dm:24
    public override object Destroy(float? force = null)
    {
        clear_camera_chunks();
        Dm.remove(ref GLOB.camera_eyes, this);
        return base.Destroy(force);
    }

    /// Clears us from any visible camera chunks.
    // tg: code/modules/mob/eye/camera/camera.dm:30
    public virtual void clear_camera_chunks()
    {
        foreach (datum_camerachunk chunk in Dm.each<datum_camerachunk>(visibleCameraChunks))
            chunk.remove(this);
    }

    /**
     * Getter proc for getting the current user's client.
     *
     * The base version of this proc returns null.
     * Subtypes are expected to overload this proc and make it return something meaningful.
     */
    // tg: code/modules/mob/eye/camera/camera.dm:40
    public virtual client GetViewerClient()
    {
        return null;
    }

    /**
     * Use this when setting the camera eye's location directly. \
     * It will also attempt to update visible chunks.
     */
    // tg: code/modules/mob/eye/camera/camera.dm:50
    public virtual void setLoc(object destination = null, float? force_update = null)
    {
        force_update ??= FALSE;
        // SHOULD_NOT_SLEEP(TRUE)
        destination = get_turf(destination as atom);
        if (!Dm.truthy(force_update) && destination == get_turf(this))
            return;

        if (destination != null)
            Waiting.NoSleep(abstract_move(destination as atom));
        else
            Waiting.NoSleep(moveToNullspace());

        if (Dm.truthy(use_visibility))
            update_visibility();
    }

    /// Sends a visibility query to the cameranet.
    /// Can be used as a signal handler.
    // tg: code/modules/mob/eye/camera/camera.dm:67
    [SignalHandler]
    public virtual void update_visibility()
    {
        if (Dm.truthy(use_visibility))
            SScameras.update_eye_chunk(this);
    }

    // tg: code/modules/mob/eye/camera/camera.dm:75
    public override async DmTask<object> zMove(object dir = null, turf target = null, float? z_move_flags = null, params object[] __extra)
    {
        object dot = await base.zMove(dir, target, z_move_flags, __extra);
        if (Dm.truthy(dot))
            setLoc(loc, force_update: TRUE);
        return dot;
    }

    // tg: code/modules/mob/eye/camera/camera.dm:80
    public override async DmTask<object> Move(atom newloc = null, object direct = null, float? glide_size_override = null, float? update_dir = null)
    {
        // SHOULD_NOT_OVERRIDE(TRUE)
        return null;
    }
}
