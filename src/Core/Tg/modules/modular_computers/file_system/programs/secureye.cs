// Translated from tgstation (snapshot 1d99bd7f): code/modules/modular_computers/file_system/programs/secureye.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate G3d, D316: the uplink's SyndEye disk; the spying programs' "Track Person", start_tracking and
// on_track_target, came with /datum/trackable and the AI's camera chunks, silicons SI4). Its NtosSecurEye window is CameraConsole's content, and its map view is the
// console's (the client's second 3D view from the active camera, M10 slice D, D268). The var blocks are generated (D111).

// tg: code/modules/modular_computers/file_system/programs/secureye.dm:3
public partial class datum_computer_file_program_secureye : datum_computer_file_program
{
    // tg: code/modules/modular_computers/file_system/programs/secureye.dm:72
    public override void on_install(datum_computer_file source = null, obj_item_modular_computer computer_installing = null, mob user = null)
    {
        base.on_install(source, computer_installing, user);
        // Map name has to start and end with an A-Z character,
        // and definitely NOT with a square bracket or even a number.
        string map_name = Dm.text("camera_console_[]_map", REF(this));
        // Convert networks to lowercase
        foreach (object i in Dm.each(network.Copy()))
        {
            network.Remove(i);
            network.Add(LOWER_TEXT(i));
        }
        // Initialize map objects
        cam_screen = Dm.@new<atom_movable_screen_map_view_camera>();
        cam_screen.generate_view(map_name);
    }

    // tg: code/modules/modular_computers/file_system/programs/secureye.dm:85
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref cam_screen);
        QDEL_NULL(ref internal_tracker);
        last_camera_turf = null;
        return base.Destroy(force);
    }

    // tg: code/modules/modular_computers/file_system/programs/secureye.dm:91
    public override object kill_program(mob user = null)
    {
        if (user != null)
            ui_close(user);
        return base.kill_program(user);
    }

    // tg: code/modules/modular_computers/file_system/programs/secureye.dm:96
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        // Update the camera, showing static if necessary and updating data if the location has moved.
        update_active_camera_screen();

        object user_ref = REF(user);
        bool is_living = isliving(user);
        // Ghosts shouldn't count towards concurrent users, which produces
        // an audible terminal_on click.
        if (is_living)
            concurrent_users.Add(user_ref);
        // Register map objects
        cam_screen.display_to(user, ui.window);
        return null;
    }

    // tg: code/modules/modular_computers/file_system/programs/secureye.dm:109
    public override async DmTask<object> ui_status(mob user = null, datum_ui_state state = null)
    {
        object dot = await base.ui_status(user, state);
        if (Dm.eq(dot, UI_DISABLED))
            return UI_CLOSE;
        return dot;
    }

    // tg: code/modules/modular_computers/file_system/programs/secureye.dm:115
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["activeCamera"] = null;
        if (camera_ref?.resolve() is obj_machinery_camera active_camera)
        {
            DmList camera = Dm.list();
            camera["name"] = active_camera.c_tag;
            camera["ref"] = REF(active_camera);
            camera["status"] = active_camera.camera_enabled;
            data["activeCamera"] = camera;
        }
        return data;
    }

    // tg: code/modules/modular_computers/file_system/programs/secureye.dm:127
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();
        data["network"] = network;
        data["mapRef"] = cam_screen.assigned_map;
        data["can_spy"] = Dm.truthy(spying) ? TRUE : FALSE;
        data["cameras"] = SScameras.get_available_cameras_data(network);
        return data;
    }

    // tg: code/modules/modular_computers/file_system/programs/secureye.dm:135
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        if (Dm.eq(action, "switch_camera"))
        {
            obj_machinery_camera active_camera = camera_ref?.resolve() as obj_machinery_camera;
            if (!Dm.truthy(spying) && active_camera != null)
                active_camera.on_stop_watching(this);

            if (!Dm.truthy(spying))
                playsound(computer, SFX_TERMINAL_TYPE, 25, FALSE);

            // locate(params["camera"]) in SScameras.cameras
            obj_machinery_camera selected_camera = Dm.locate_ref(@params?["camera"]) as obj_machinery_camera;
            if (selected_camera != null && !Dm.@in(selected_camera, SScameras.cameras))
                selected_camera = null;
            if (selected_camera != null)
                camera_ref = WEAKREF(selected_camera) as datum_weakref;
            else
            {
                camera_ref = null;
                return TRUE;
            }
            if (!Dm.truthy(spying))
                selected_camera.on_start_watching(this);
            if (internal_tracker != null)
                internal_tracker.reset_tracking();

            update_active_camera_screen();
            return TRUE;
        }

        if (Dm.eq(action, "start_tracking"))
        {
            if (internal_tracker == null)
            {
                internal_tracker = Dm.@new<datum_trackable>(this);
                RegisterSignal(internal_tracker, COMSIG_TRACKABLE_TRACKING_TARGET, nameof(on_track_target));
            }
            await internal_tracker.track_input(usr as mob_living);
            return TRUE;
        }
        return dot;
    }

    // tg: code/modules/modular_computers/file_system/programs/secureye.dm:169
    public virtual void on_track_target(datum_trackable source = null, mob_living target = null)
    {
        turf target_turf = get_turf(target);
        datum_camerachunk target_camerachunk = SScameras.get_turf_camera_chunk(target_turf);
        if (target_camerachunk == null)
            throw Dm.CRASH(Dm.text("[] was able to track [] through /datum/trackable, but was not on a visible turf to cameras.", this, target), "code/modules/modular_computers/file_system/programs/secureye.dm", 174);
        foreach (obj_machinery_camera cameras in Dm.each_cast<obj_machinery_camera>(target_camerachunk.cameras[Dm.text("[]", target.z)]))
        {
            // We need to find a particular camera that can see this turf
            if (!Dm.truthy(length(Dm.bitand(cameras.can_see(), Dm.list(target_turf)))))
                continue;
            datum_weakref new_camera = WEAKREF(cameras) as datum_weakref;
            if (camera_ref == new_camera)
                return;
            camera_ref = new_camera;
            update_active_camera_screen();
            return;
        }
    }

    // tg: code/modules/modular_computers/file_system/programs/secureye.dm:186
    public override void ui_close(mob user = null)
    {
        base.ui_close(user);
        //don't track anyone while we're shutting off.
        if (internal_tracker != null)
            internal_tracker.reset_tracking();
        object user_ref = REF(user);
        bool is_living = isliving(user);
        // Living creature or not, we remove you anyway.
        concurrent_users.Remove(user_ref);
        // Unregister map objects
        cam_screen?.hide_from(user);
        // Turn off the console
        if (concurrent_users.len == 0 && is_living)
        {
            obj_machinery_camera active_camera = camera_ref?.resolve() as obj_machinery_camera;
            if (!Dm.truthy(spying) && active_camera != null)
                active_camera.on_stop_watching(this);
            camera_ref = null;
            last_camera_turf = null;
            if (!Dm.truthy(spying))
                playsound(computer, "sound/machines/terminal/terminal_off.ogg", 25, FALSE);
        }
    }

    // tg: code/modules/modular_computers/file_system/programs/secureye.dm:207
    public virtual void update_active_camera_screen()
    {
        obj_machinery_camera active_camera = camera_ref?.resolve() as obj_machinery_camera;
        // Show static if can't use the camera
        if (!Dm.truthy(active_camera?.can_use()))
        {
            cam_screen.show_camera_static();
            return;
        }

        DmList visible_turfs = Dm.list();

        // Get the camera's turf to correctly gather what's visible from its turf, in case it's located in a moving object (borgs / mechs)
        turf new_cam_turf = get_turf(active_camera) as turf;

        // If we're not forcing an update for some reason and the cameras are in the same location,
        // we don't need to update anything.
        // Most security cameras will end here as they're not moving.
        if (last_camera_turf == new_cam_turf)
            return;

        // Cameras that get here are moving, and are likely attached to some moving atom such as cyborgs.
        last_camera_turf = new_cam_turf;

        //Here we gather what's visible from the camera's POV based on its view_range and xray modifier if present
        DmList visible_things = Dm.truthy(active_camera.isXRay(ignore_malf_upgrades: TRUE)) ? range(active_camera.view_range, new_cam_turf) : view(active_camera.view_range, new_cam_turf);

        foreach (turf visible_turf in Dm.each<turf>(visible_things))
            visible_turfs.Add(visible_turf);

        //Get coordinates for a rectangle area that contains the turfs we see so we can then clear away the static in the resulting rectangle area
        DmList bbox = get_bbox_of_atoms(visible_turfs);
        float size_x = Dm.num(bbox[3]) - Dm.num(bbox[1]) + 1;
        float size_y = Dm.num(bbox[4]) - Dm.num(bbox[2]) + 1;

        cam_screen.show_camera(visible_turfs, size_x, size_y);
        cam_screen.set_display(active_camera);
    }
}
