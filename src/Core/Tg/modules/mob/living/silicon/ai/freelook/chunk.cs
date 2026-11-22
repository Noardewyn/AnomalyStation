// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/silicon/ai/freelook/chunk.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI4 (D326): the camera chunk, whole. Its static images go into the viewer's client.images as tg's do; the server sends them
// to the client as each chunk's unseen tiles (DV386), which the client draws as static. The var block is generated (D111).

/**
 * A 16x16 grid of the map with a list of turfs that can be seen, are visible and are dimmed. \
 * Allows Camera Eyes to stream these chunks and know what it can and cannot see.
 */
// tg: code/modules/mob/living/silicon/ai/freelook/chunk.dm:5
public partial class datum_camerachunk : datum
{
    // tg: code/modules/mob/living/silicon/ai/freelook/chunk.dm:38
    public virtual void add(mob_eye_camera eye = null)
    {
        Dm.append(ref eye.visibleCameraChunks, this);
        Dm.append(ref seenby, eye);
        force_update();
        client client = eye.GetViewerClient();
        if (client != null && Dm.truthy(eye.use_visibility))
            { DmList images = client.images; Dm.append(ref images, active_static_images); } // a list: changed in place
    }

    // tg: code/modules/mob/living/silicon/ai/freelook/chunk.dm:48
    public virtual void remove(mob_eye_camera eye = null)
    {
        Dm.remove(ref eye.visibleCameraChunks, this);
        Dm.remove(ref seenby, eye);
        client client = eye.GetViewerClient();
        if (client != null && Dm.truthy(eye.use_visibility) && (seenby.len == 0 || (isliving(client.mob) && !isAI(client.mob)))) // Bypass for non-AIs, to unsure static removal.
            { DmList images = client.images; Dm.remove(ref images, active_static_images); } // a list: changed in place
    }

    /**
     * Queues the chuck to be updated after a delay.
     *
     * * update_source - the atom that caused the update
     * * update_delay_buffer - the delay before the update is performed. Defaults to 0 (instant).
     */
    // tg: code/modules/mob/living/silicon/ai/freelook/chunk.dm:64
    public virtual void queue_update(atom update_source = null, float update_delay_buffer = 0)
    {
        if (Dm.truthy(length(seenby)))
        {
            _addtimer(Dm.@new<datum_callback>(this, nameof(update)), update_delay_buffer != 0 ? update_delay_buffer : 1, TIMER_UNIQUE, file: "code/modules/mob/living/silicon/ai/freelook/chunk.dm", line: 67);
            return;
        }
        reset_update();
        bool start_queue = !Dm.truthy(length(update_sources));
        string update_key = Dm.str(REF(update_source));
        if (update_source is obj_machinery_camera)
            update_sources[update_key] = Dm.num(update_sources[update_key]) + 1;
        else if (update_sources[update_key] == null)
            update_sources[update_key] = update_source.opacity;
        else if (!Dm.eq(update_sources[update_key], update_source.opacity))
        {
            Dm.remove(ref update_sources, update_key);
            _dequeue_update();
            return;
        }
        if (!start_queue)
            return;
        if (update_delay_buffer <= 0)
            _queue_update();
        else
            _addtimer(Dm.@new<datum_callback>(this, nameof(_queue_update)), update_delay_buffer, TIMER_UNIQUE, file: "code/modules/mob/living/silicon/ai/freelook/chunk.dm", line: 97);
    }

    // tg: code/modules/mob/living/silicon/ai/freelook/chunk.dm:100
    public virtual void _queue_update()
    {
        if (!Dm.truthy(length(update_sources)))
            return;
        SScameras.chunks_to_update[this] = TRUE;
    }

    // tg: code/modules/mob/living/silicon/ai/freelook/chunk.dm:107
    private void _dequeue_update()
    {
        if (Dm.truthy(length(update_sources)))
            return;
        Dm.remove(ref SScameras.chunks_to_update, this);
        Dm.remove(ref SScameras.current_run, this);
    }

    /**
     * Forces the chunk to update immediately
     *
     * * only_if_necessary - if TRUE, will not update the chunk unless it's been marked to update.
     */
    // tg: code/modules/mob/living/silicon/ai/freelook/chunk.dm:120
    public virtual void force_update(float? only_if_necessary = null)
    {
        only_if_necessary ??= TRUE;
        if (Dm.truthy(only_if_necessary) && !Dm.truthy(length(update_sources)))
            return;
        update();
    }

    // tg: code/modules/mob/living/silicon/ai/freelook/chunk.dm:126
    public virtual void reset_update()
    {
        if (!Dm.truthy(currently_updating))
            return;
        processing_visible_turfs = Dm.list();
        processing_cameras = Dm.list();
        currently_updating = FALSE;
        Dm.remove(ref SScameras.current_run, this);
        _queue_update();
    }

    // tg: code/modules/mob/living/silicon/ai/freelook/chunk.dm:139
    public virtual bool yield_update()
    {
        if (Dm.truthy(SScameras.disable_camera_updates))
            return true;
        if (!Dm.truthy(currently_updating))
        {
            currently_updating = TRUE;
            processing_cameras = Dm.list();
            for (float z_level = Dm.num(lower_z); z_level <= Dm.num(upper_z); z_level++)
                processing_cameras[Dm.text("[]", z_level)] = (cameras[Dm.text("[]", z_level)] as DmList).Copy();
        }
        DmList updated_visible_turfs = this.processing_visible_turfs;
        for (float z_level = Dm.num(lower_z); z_level <= Dm.num(upper_z); z_level++)
        {
            DmList processing = processing_cameras[Dm.text("[]", z_level)] as DmList;
            while (Dm.truthy(length(processing)))
            {
                obj_machinery_camera current_camera = processing[length(processing)] as obj_machinery_camera;
                processing.len--;
                if (!Dm.truthy(current_camera?.can_use()))
                {
                    if (TICK_CHECK())
                        return false;
                    continue;
                }
                turf point = locate(Dm.num(x) + (CHUNK_SIZE / 2), Dm.num(y) + (CHUNK_SIZE / 2), z_level);
                if (get_dist(point, current_camera) > MAX_CAMERA_RANGE + (CHUNK_SIZE / 2))
                    continue;
                foreach (turf vis_turf in Dm.each<turf>(Dm.bitand(current_camera.can_see(), turfs)))
                    updated_visible_turfs[vis_turf] = vis_turf;
                if (TICK_CHECK())
                    return false;
            }
        }
        update_with_turfs(updated_visible_turfs);
        update_sources.Cut();
        reset_update();
        return true;
    }

    // tg: code/modules/mob/living/silicon/ai/freelook/chunk.dm:176
    public virtual void update()
    {
        if (Dm.truthy(SScameras.disable_camera_updates))
            return;
        update_sources.Cut();
        _dequeue_update();
        reset_update();
        DmList updated_visible_turfs = Dm.list();
        for (float z_level = Dm.num(lower_z); z_level <= Dm.num(upper_z); z_level++)
        {
            foreach (obj_machinery_camera current_camera in Dm.each<obj_machinery_camera>(cameras[Dm.text("[]", z_level)]))
            {
                if (!Dm.truthy(current_camera?.can_use()))
                    continue;
                turf point = locate(Dm.num(x) + (CHUNK_SIZE / 2), Dm.num(y) + (CHUNK_SIZE / 2), z_level);
                if (get_dist(point, current_camera) > MAX_CAMERA_RANGE + (CHUNK_SIZE / 2))
                    continue;
                foreach (turf vis_turf in Dm.each<turf>(Dm.bitand(current_camera.can_see(), turfs)))
                    updated_visible_turfs[vis_turf] = vis_turf;
            }
        }
        update_with_turfs(updated_visible_turfs);
    }

    // tg: code/modules/mob/living/silicon/ai/freelook/chunk.dm:202
    public virtual void update_with_turfs(DmList updated_visible_turfs = null)
    {
        DmList newly_visible_turfs = Dm.sub(updated_visible_turfs, visibleTurfs) as DmList;
        DmList newly_obscured_turfs = Dm.sub(visibleTurfs, updated_visible_turfs) as DmList;
        foreach (mob_eye_camera client_eye in Dm.each<mob_eye_camera>(seenby))
        {
            client client = client_eye.GetViewerClient();
            if (client == null)
                continue;
            { DmList images = client.images; Dm.remove(ref images, active_static_images); } // a list: changed in place
        }
        foreach (turf visible_turf in Dm.each<turf>(newly_visible_turfs))
        {
            image static_image = obscuredTurfs[visible_turf] as image;
            if (static_image == null)
                continue;
            Dm.remove(ref active_static_images, static_image);
            Dm.remove(ref obscuredTurfs, visible_turf);
        }
        foreach (turf obscured_turf in Dm.each<turf>(newly_obscured_turfs))
        {
            if (Dm.truthy(obscuredTurfs[obscured_turf]) || obscured_turf is turf_open_ai_visible)
                continue;
            image static_image = turfs[obscured_turf] as image;
            if (static_image == null)
            {
                stack_trace("somehow a camera chunk used a turf it didn't contain!!", "code/modules/mob/living/silicon/ai/freelook/chunk.dm", 227);
                break;
            }
            obscuredTurfs[obscured_turf] = static_image;
            Dm.append(ref active_static_images, static_image);
        }
        visibleTurfs = updated_visible_turfs;
        foreach (mob_eye_camera client_eye in Dm.each<mob_eye_camera>(seenby))
        {
            client client = client_eye.GetViewerClient();
            if (client == null)
                continue;
            { DmList images = client.images; Dm.append(ref images, active_static_images); } // a list: changed in place
        }
    }

    // tg: code/modules/mob/living/silicon/ai/freelook/chunk.dm:244
    [DmParams("x", "y", "lower_z")]
    public override async DmTask New(params object[] __extra)
    {
        float new_x = Dm.num(Dm.arg(__extra, 0));
        float new_y = Dm.num(Dm.arg(__extra, 1));
        float new_lower_z = Dm.num(Dm.arg(__extra, 2));
        new_x = GET_CHUNK_COORD(new_x);
        new_y = GET_CHUNK_COORD(new_y);
        this.x = new_x;
        this.y = new_y;
        this.lower_z = new_lower_z;
        turf upper_turf = get_highest_turf(locate(new_x, new_y, new_lower_z)) as turf;
        this.upper_z = upper_turf.z;
        DmList cameras = this.cameras;
        DmList turfs = this.turfs;
        DmList visibleTurfs = this.visibleTurfs;
        DmList obscuredTurfs = this.obscuredTurfs;
        DmList active_static_images = this.active_static_images;
        float lower_x = new_x;
        float lower_y = new_y;
        float upper_x = min(lower_x + CHUNK_SIZE - 1, Dm.world.maxx);
        float upper_y = min(lower_y + CHUNK_SIZE - 1, Dm.world.maxy);
        DmList stack = SSmapping.get_connected_levels(new_lower_z);
        for (float z_level = new_lower_z; z_level <= Dm.num(upper_z); z_level++)
        {
            cameras[Dm.text("[]", z_level)] = Dm.list();
            image mirror_from = SScameras.obscured_images[Dm.num(GET_Z_PLANE_OFFSET(z_level)) + 1] as image;
            turf chunk_corner = locate(new_x, new_y, z_level);
            foreach (turf lad in Dm.each<turf>(CORNER_BLOCK(chunk_corner, CHUNK_SIZE, CHUNK_SIZE))) //we use CHUNK_SIZE for width and height here as it handles subtracting 1 from those two parameters by itself
            {
                image our_image = Dm.@new<image>(mirror_from);
                our_image.loc = lad;
                turfs[lad] = our_image;
            }
        }
        foreach (obj_machinery_camera camera in Dm.each<obj_machinery_camera>(SScameras.cameras))
        {
            turf camera_loc = get_turf(camera);
            if (camera_loc.x + MAX_CAMERA_RANGE < lower_x || camera_loc.x - MAX_CAMERA_RANGE > upper_x)
                continue;
            if (camera_loc.y + MAX_CAMERA_RANGE < lower_y || camera_loc.y - MAX_CAMERA_RANGE > upper_y)
                continue;
            // tg compares the cached stack lists (z_level_to_stack); a toy room's level gets a fresh list per call (mapping.cs's GAP),
            // so the stacks are compared by their levels
            if (!same_stack(stack, SSmapping.get_connected_levels(camera_loc.z)))
                continue;
            if (!Dm.truthy(camera.can_use()))
                continue;
            DmList z_cameras = cameras[Dm.text("[]", camera_loc.z)] as DmList;
            Dm.append(ref z_cameras, camera);
            foreach (turf vis_turf in Dm.each<turf>(Dm.bitand(camera.can_see(), turfs)))
                visibleTurfs[vis_turf] = vis_turf;
        }
        foreach (turf obscured_turf in Dm.each<turf>(Dm.sub(turfs, visibleTurfs)))
        {
            image new_static = turfs[obscured_turf] as image;
            Dm.append(ref active_static_images, new_static);
            obscuredTurfs[obscured_turf] = new_static;
        }
    }

    private static bool same_stack(DmList a, DmList b)
    {
        if (a == b)
            return true;
        if (a == null || b == null || a.Count != b.Count)
            return false;
        for (int i = 1; i <= a.Count; i++)
        {
            if (!Dm.eq(a[(float)i], b[(float)i]))
                return false;
        }
        return true;
    }
}
