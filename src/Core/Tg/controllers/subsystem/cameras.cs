// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/cameras.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M10 slice D, D268: the cameras list and the consoles' camera lists; silicons SI4, D326: the camera chunks, what a
// camera eye sees through the cameras and the static over the rest). The var block is generated (D111).

// tg: code/controllers/subsystem/cameras.dm:2
public partial class datum_controller_subsystem_cameras : datum_controller_subsystem
{
    // The New() that SUBSYSTEM_DEF(cameras) defines (__DEFINES/MC.dm), with NEW_SS_GLOBAL(SScameras) (__DEFINES/MC.dm:29) expanded inline.
    // tg: code/controllers/subsystem/cameras.dm:2
    public override async DmTask New(params object[] __extra)
    {
        if (SScameras != this)
        {
            if (SScameras is datum_controller_subsystem_cameras)
            {
                Recover();
                qdel(SScameras);
            }
            SScameras = this;
        }
        PreInit();
    }

    // tg: code/controllers/subsystem/cameras.dm:28
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        update_offsets(SSmapping.max_plane_offset);
        RegisterSignal(SSmapping, COMSIG_PLANE_OFFSET_INCREASE, nameof(on_offset_growth));
        return SS_INIT_SUCCESS;
    }

    // MC_TICK_CHECK (__DEFINES/MC.dm:1), inline (rules 11.3)
    private bool MC_TICK_CHECK() => Dm.truthy((world.tick_usage > datum_controller_master.current_ticklimit || !Dm.eq(this.state, SS_RUNNING)) ? pause() : 0);

    // tg: code/controllers/subsystem/cameras.dm:33
    public override async DmTask fire(float? resumed = null, params object[] __extra)
    {
        if (!Dm.truthy(resumed))
        {
            this.current_run = chunks_to_update.Copy();
            chunks_to_update = Dm.list();
        }

        DmList current_run = this.current_run;
        while (Dm.truthy(current_run.len))
        {
            datum_camerachunk chunk = current_run[current_run.len] as datum_camerachunk;
            // DEVIATION(DV392): a finished chunk is popped only while it is still the last; its reset_update (chunk.dm:134) already took
            // it out, so tg's bare len-- dropped the next chunk unupdated, or crashed on the emptied list
            if (chunk.yield_update() && current_run.len > 0 && current_run[current_run.len] == chunk)
                current_run.len--;
            if (MC_TICK_CHECK())
                break;
        }
    }

    // tg: code/controllers/subsystem/cameras.dm:45
    public override object stat_entry(object msg = null)
    {
        msg = Dm.text("Cams: [] | Chunks: [] | Updating: []", length(cameras), length(chunks), length(chunks_to_update));
        return base.stat_entry(msg);
    }

    /// Updates the images for new plane offsets
    // tg: code/controllers/subsystem/cameras.dm:50
    public virtual void update_offsets(float new_offset = 0)
    {
        for (float i = length(obscured_images); i <= new_offset; i++)
        {
            image obscured = Dm.@new<image>(Dm.resource("icons/effects/cameravis.dmi"));
            obscured.plane = GET_NEW_PLANE(CAMERA_STATIC_PLANE, i); // SET_PLANE_W_SCALAR(obscured, CAMERA_STATIC_PLANE, i)
            obscured.appearance_flags = Dm.bitor(Dm.bitor(Dm.bitor(RESET_TRANSFORM, RESET_ALPHA), RESET_COLOR), KEEP_APART);
            obscured.@override = TRUE;
            Dm.append(ref obscured_images, obscured);
        }
    }

    // tg: code/controllers/subsystem/cameras.dm:58
    public virtual void on_offset_growth(datum source = null, object old_offset = null, object new_offset = null)
    {
        update_offsets(Dm.num(new_offset));
    }

    /// Checks if a chunk has been generated in x, y, z.
    // tg: code/controllers/subsystem/cameras.dm:63
    public virtual datum_camerachunk get_camera_chunk(float x, float y, float z)
    {
        x = GET_CHUNK_COORD(x);
        y = GET_CHUNK_COORD(y);
        if (Dm.num(GET_LOWEST_STACK_OFFSET(z)) != 0)
        {
            turf lowest = get_lowest_turf(locate(x, y, z)) as turf;
            return chunks[Dm.text("[],[],[]", x, y, lowest.z)] as datum_camerachunk;
        }

        return chunks[Dm.text("[],[],[]", x, y, z)] as datum_camerachunk;
    }

    // Returns the chunk in the x, y, z.
    // If there is no chunk, it creates a new chunk and returns that.
    // tg: code/controllers/subsystem/cameras.dm:74
    public virtual datum_camerachunk generate_chunk(float x, float y, float z)
    {
        x = GET_CHUNK_COORD(x);
        y = GET_CHUNK_COORD(y);
        turf lowest = get_lowest_turf(locate(x, y, z)) as turf;
        string key = Dm.text("[],[],[]", x, y, lowest.z);
        datum_camerachunk dot = chunks[key] as datum_camerachunk;
        if (dot == null)
        {
            dot = Dm.@new<datum_camerachunk>(x, y, lowest.z);
            chunks[key] = dot;
        }
        return dot;
    }

    /// Updates what the camera eye can see.
    /// It is recommended you use this when a camera eye moves or its location is set.
    // tg: code/controllers/subsystem/cameras.dm:86
    public virtual void update_eye_chunk(mob_eye_camera eye = null)
    {
        DmList visibleChunks = Dm.list();
        //Get the eye's turf in case its located in an object like a mecha
        turf eye_turf = get_turf(eye);
        if (eye.loc != null)
        {
            float static_range = Dm.num(eye.static_visibility_range);
            float x1 = max(1, eye_turf.x - static_range);
            float y1 = max(1, eye_turf.y - static_range);
            float x2 = min(Dm.world.maxx, eye_turf.x + static_range);
            float y2 = min(Dm.world.maxy, eye_turf.y + static_range);

            for (float x = x1; x <= x2; x += CHUNK_SIZE)
            {
                for (float y = y1; y <= y2; y += CHUNK_SIZE)
                    visibleChunks = Dm.bitor(visibleChunks, generate_chunk(x, y, eye_turf.z)) as DmList;
            }
        }

        DmList remove = Dm.sub(eye.visibleCameraChunks, visibleChunks) as DmList;
        DmList add = Dm.sub(visibleChunks, eye.visibleCameraChunks) as DmList;

        foreach (datum_camerachunk chunk in Dm.each<datum_camerachunk>(remove))
            chunk.remove(eye);

        foreach (datum_camerachunk chunk in Dm.each<datum_camerachunk>(add))
            chunk.add(eye);
    }

    /// Used in [/proc/major_chunk_change] - indicates the camera should be removed from the chunk list.
    private const float REMOVE_CAMERA = 0;
    /// Used in [/proc/major_chunk_change] - indicates the camera should be added to the chunk list.
    private const float ADD_CAMERA = 1;
    /// Used in [/proc/major_chunk_change] - indicates the chunk should be updated without adding/removing a camera.
    private const float IGNORE_CAMERA = 2;

    /// Updates the chunks that the turf is located in. Use this when obstacles are destroyed or when doors open.
    // tg: code/controllers/subsystem/cameras.dm:119
    public virtual void update_visibility(atom relevant_atom = null)
    {
        if (SSticker == null)
            return;
        major_chunk_change(relevant_atom, IGNORE_CAMERA);
    }

    /// Removes a camera from a chunk.
    // tg: code/controllers/subsystem/cameras.dm:125
    public virtual void remove_camera_from_chunk(obj_machinery_camera old_cam = null)
    {
        major_chunk_change(old_cam, REMOVE_CAMERA);
    }

    /// Add a camera to a chunk.
    // tg: code/controllers/subsystem/cameras.dm:129
    public virtual void add_camera_to_chunk(obj_machinery_camera new_cam = null)
    {
        if (Dm.truthy(new_cam.can_use()))
            major_chunk_change(new_cam, ADD_CAMERA);
    }

    /**
     * Used to update cameras that are movin, since most everything in the game can.
     * update_delay_buffer is passed to allow variable update delays. useful so one
     * camera'd guy moving doesn't absolutely spam updates to watching ais (laggin the server)
    */
    // tg: code/controllers/subsystem/cameras.dm:138
    public virtual void camera_moved(obj_machinery_camera updating_camera = null, turf old_turf = null, turf new_turf = null, float update_delay_buffer = 0)
    {
        if (old_turf == new_turf)
            return;

        DmList old_chunks = Dm.list();
        float range_difference = MAX_CAMERA_RANGE + 1;
        if (old_turf != null)
        {
            float x1 = max(1, old_turf.x - range_difference);
            float y1 = max(1, old_turf.y - range_difference);
            float x2 = min(Dm.world.maxx, old_turf.x + range_difference);
            float y2 = min(Dm.world.maxy, old_turf.y + range_difference);
            for (float x = x1; x <= x2; x += CHUNK_SIZE)
            {
                for (float y = y1; y <= y2; y += CHUNK_SIZE)
                {
                    datum_camerachunk chunk = get_camera_chunk(x, y, old_turf.z);
                    if (chunk == null)
                        continue;
                    Dm.append(ref old_chunks, chunk);
                    // if we've moved, what we can see will have changed so queue er up
                    chunk.queue_update(updating_camera, update_delay_buffer);
                }
            }
        }

        DmList new_chunks = Dm.list();
        if (new_turf != null && Dm.truthy(updating_camera.can_use()))
        {
            if (QDELETED(updating_camera))
                stack_trace("Tried to add a qdeleting camera to the net", "code/controllers/subsystem/cameras.dm", 162);
            else
            {
                float x1 = max(1, new_turf.x - range_difference);
                float y1 = max(1, new_turf.y - range_difference);
                float x2 = min(Dm.world.maxx, new_turf.x + range_difference);
                float y2 = min(Dm.world.maxy, new_turf.y + range_difference);
                for (float x = x1; x <= x2; x += CHUNK_SIZE)
                {
                    for (float y = y1; y <= y2; y += CHUNK_SIZE)
                    {
                        datum_camerachunk chunk = get_camera_chunk(x, y, new_turf.z);
                        if (chunk == null)
                            continue;
                        Dm.append(ref new_chunks, chunk);
                        // if we've moved, what we can see will have changed so queue er up
                        chunk.queue_update(updating_camera, update_delay_buffer);
                    }
                }
            }
        }

        // First, cut us from chunks who we can't see anymore
        foreach (datum_camerachunk lost in Dm.each<datum_camerachunk>(Dm.sub(old_chunks, new_chunks)))
        {
            DmList z_cameras = lost.cameras[Dm.text("[]", old_turf.z)] as DmList;
            Dm.remove(ref z_cameras, updating_camera);
        }

        // Then we add to turfs who we can newly see!
        foreach (datum_camerachunk found in Dm.each<datum_camerachunk>(Dm.sub(new_chunks, old_chunks)))
        {
            string key = Dm.text("[]", new_turf.z);
            found.cameras[key] = Dm.bitor(found.cameras[key], updating_camera);
        }
    }

    /**
     * Never access this proc directly!!!!
     * This will update the chunk and all the surrounding chunks.
     * It will also add the atom to the cameras list if you set the choice to 1.
     * Setting the choice to 0 will remove the camera from the chunks.
     * If you want to update the chunks around an object, without adding/removing a camera, use choice 2.
     * update_delay_buffer is passed all the way to queue_update() from portable camera updates on movement
     * to change the time between static updates.
     */
    // tg: code/controllers/subsystem/cameras.dm:192
    protected virtual void major_chunk_change(atom center_or_camera = null, float choice = IGNORE_CAMERA, float update_delay_buffer = 0)
    {
        if (QDELETED(center_or_camera) && choice == ADD_CAMERA)
            throw Dm.CRASH("Tried to add a qdeleting camera to the net", "code/controllers/subsystem/cameras.dm", 196);

        turf chunk_turf = get_turf(center_or_camera);
        if (chunk_turf == null)
            return;

        float range_difference = MAX_CAMERA_RANGE + 1;
        float x1 = max(1, chunk_turf.x - range_difference);
        float y1 = max(1, chunk_turf.y - range_difference);
        float x2 = min(Dm.world.maxx, chunk_turf.x + range_difference);
        float y2 = min(Dm.world.maxy, chunk_turf.y + range_difference);
        for (float x = x1; x <= x2; x += CHUNK_SIZE)
        {
            for (float y = y1; y <= y2; y += CHUNK_SIZE)
            {
                datum_camerachunk chunk = get_camera_chunk(x, y, chunk_turf.z);
                if (chunk == null)
                    continue;
                string key = Dm.text("[]", chunk_turf.z);
                if (choice == REMOVE_CAMERA)
                {
                    // Remove the camera.
                    DmList z_cameras = chunk.cameras[key] as DmList;
                    Dm.remove(ref z_cameras, center_or_camera);
                }
                if (choice == ADD_CAMERA)
                {
                    // You can't have the same camera in the list twice.
                    chunk.cameras[key] = Dm.bitor(chunk.cameras[key], center_or_camera);
                }
                chunk.queue_update(center_or_camera, update_delay_buffer);
            }
        }
    }

    /// A faster, turf only version of [/datum/controller/subsystem/cameras/proc/major_chunk_change]
    /// For use in sensitive code, be careful with it
    // tg: code/controllers/subsystem/cameras.dm:223
    public virtual void bare_major_chunk_change(turf changed = null)
    {
        float range_difference = MAX_CAMERA_RANGE + 1;
        float x1 = max(1, changed.x - range_difference);
        float y1 = max(1, changed.y - range_difference);
        float x2 = min(Dm.world.maxx, changed.x + range_difference);
        float y2 = min(Dm.world.maxy, changed.y + range_difference);
        for (float x = x1; x <= x2; x += CHUNK_SIZE)
        {
            for (float y = y1; y <= y2; y += CHUNK_SIZE)
            {
                datum_camerachunk chunk = get_camera_chunk(x, y, changed.z);
                chunk?.queue_update(changed, 0);
            }
        }
    }

    /// Will check if an atom is on a viewable turf.
    /// Returns TRUE if the atom is visible by any camera, FALSE otherwise.
    // tg: code/controllers/subsystem/cameras.dm:236
    public virtual bool is_visible_by_cameras(atom target = null)
    {
        return turf_visible_by_cameras(get_turf(target));
    }

    /// Checks if the passed turf is visible by any camera.
    /// Returns TRUE if the turf is visible by any camera, FALSE otherwise.
    // tg: code/controllers/subsystem/cameras.dm:241
    private bool turf_visible_by_cameras(turf position)
    {
        if (position == null)
            return false;
        datum_camerachunk chunk = generate_chunk(position.x, position.y, position.z);
        if (chunk == null)
            return false;
        chunk.force_update(only_if_necessary: TRUE); // Update NOW if necessary
        if (Dm.truthy(chunk.visibleTurfs[position]))
            return true;
        return false;
    }

    /// Gets the camera chunk the passed turf is in.
    /// Returns the chunk if it exists and is visible, null otherwise.
    // tg: code/controllers/subsystem/cameras.dm:254
    public virtual datum_camerachunk get_turf_camera_chunk(turf position = null)
    {
        datum_camerachunk chunk = generate_chunk(position.x, position.y, position.z);
        if (chunk == null)
            return null;
        chunk.force_update(only_if_necessary: TRUE); // Update NOW if necessary
        if (Dm.truthy(chunk.visibleTurfs[position]))
            return chunk;
        return null;
    }

    /// Returns list of available cameras, ready to use for UIs displaying list of them
    /// The format is: list("name" = "camera.c_tag", ref = REF(camera))
    // tg: code/controllers/subsystem/cameras.dm:267
    public virtual DmList get_available_cameras_data(DmList networks_available = null, DmList z_levels_available = null)
    {
        DmList available_cameras_data = Dm.list();
        foreach (obj_machinery_camera camera in Dm.each<obj_machinery_camera>(get_filtered_and_sorted_cameras(networks_available, z_levels_available)))
        {
            DmList entry = Dm.list();
            entry["name"] = camera.c_tag;
            entry["ref"] = REF(camera);
            available_cameras_data.Add(Dm.list(entry));
        }

        return available_cameras_data;
    }

    /**
     * get_available_camera_by_tag_list
     *
     * Builds a list of all available cameras that can be seen to networks_available and in z_levels_available.
     * Entries are stored in `c_tag[camera.can_use() ? null : " (Deactivated)"]` => `camera` format
     */
    // tg: code/controllers/subsystem/cameras.dm:287
    public virtual DmList get_available_camera_by_tag_list(DmList networks_available = null, DmList z_levels_available = null)
    {
        DmList available_cameras_by_tag = Dm.list();
        foreach (obj_machinery_camera camera in Dm.each<obj_machinery_camera>(get_filtered_and_sorted_cameras(networks_available, z_levels_available)))
            available_cameras_by_tag[Dm.text("[][]", camera.c_tag, Dm.truthy(camera.can_use()) ? null : " (Deactivated)")] = camera;

        return available_cameras_by_tag;
    }

    /// Returns list of all cameras that passed `is_camera_available` filter and sorted by `cmp_camera_ctag_asc`
    // tg: code/controllers/subsystem/cameras.dm:295
    private DmList get_filtered_and_sorted_cameras(DmList networks_available = null, DmList z_levels_available = null)
    {
        DmList filtered_cameras = Dm.list();
        foreach (obj_machinery_camera camera in Dm.each<obj_machinery_camera>(cameras))
        {
            if (!is_camera_available(camera, networks_available, z_levels_available))
                continue;

            filtered_cameras.Add(camera);
        }

        return sortTim(filtered_cameras, "/proc/" + nameof(GlobalProcs.cmp_camera_ctag_asc)) as DmList;
    }

    /// Checks if the `camera_to_check` meets the requirements of availability.
    // tg: code/controllers/subsystem/cameras.dm:308
    private bool is_camera_available(obj_machinery_camera camera_to_check, DmList networks_available, DmList z_levels_available)
    {
        if (!Dm.truthy(camera_to_check.c_tag))
            return false;

        if (Dm.truthy(LAZYLEN(z_levels_available)) && !Dm.@in(camera_to_check.z, z_levels_available))
            return false;

        return Dm.truthy(LAZYLEN(Dm.bitand(camera_to_check.network, networks_available)));
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/cameras.dm:2  SUBSYSTEM_DEF(cameras): GLOBAL_REAL(SScameras, /datum/controller/subsystem/cameras)
    internal datum_controller_subsystem_cameras SScameras;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_cameras SScameras => ref TgState.Current.SScameras;

    // tg: code/__DEFINES/cameranets.dm:7  #define GET_CHUNK_COORD(v) max((FLOOR((v), CHUNK_SIZE)), 1)
    public static float GET_CHUNK_COORD(float v) => max(FLOOR(v, CHUNK_SIZE), 1);
}
