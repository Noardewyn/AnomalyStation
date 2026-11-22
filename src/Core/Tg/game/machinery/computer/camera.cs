// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/computer/camera.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file but the shuttle's networks (M10 slice D, D268): the security camera console, its tgui window (CameraConsole) and
// its map view. The client draws the map view's turfs as a second 3D view from the active camera (the window's ByondUi), and the
// server's interest takes them as the player's second eye (docs/ai/specs/multiplayer.md section 7).

// tg: code/game/machinery/computer/camera.dm:3
public partial class obj_machinery_computer_security : obj_machinery_computer
{
    private const int DEFAULT_MAP_SIZE = 15; // tg: camera.dm:1

    // tg: code/game/machinery/computer/camera.dm:22
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // Map name has to start and end with an A-Z character,
        // and definitely NOT with a square bracket or even a number.
        // I wasted 6 hours on this. :agony:
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
        return dot;
    }

    // tg: code/game/machinery/computer/camera.dm:36
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref cam_screen);
        return base.Destroy(force);
    }

    // tg: code/game/machinery/computer/camera.dm:40  (silicons SI4, with the cameras' own)
    public override async DmTask<object> connect_to_shuttle(object mapload = null, obj_docking_port_mobile port = null, obj_docking_port_stationary dock = null)
    {
        foreach (object i in Dm.each(network.Copy()))
        {
            network.Remove(i);
            network.Add(Dm.text("[]_[]", port.shuttle_id, i));
        }
        return null;
    }

    // tg: code/game/machinery/computer/camera.dm:45
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        object dot = await base.ui_interact(user, ui);
        if (!Dm.truthy(user.client)) //prevents errors by trying to pass clients that don't exist.
            return dot;
        // Update UI
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;

        // Update the camera, showing static if necessary and updating data if the location has moved.
        update_active_camera_screen();

        if (!Dm.truthy(ui))
        {
            object user_ref = REF(user);
            bool is_living = isliving(user);
            // Ghosts shouldn't count towards concurrent users, which produces
            // an audible terminal_on click.
            if (is_living)
                concurrent_users.Add(user_ref);
            // Turn on the console
            if (concurrent_users.len == 1 && is_living)
            {
                playsound(this, "sound/machines/terminal/terminal_on.ogg", 25, FALSE);
                await use_energy(active_power_usage);
            }
            // Open UI
            ui = Dm.@new<datum_tgui>(user, this, "CameraConsole", name);
            await ui.open();
            // Register map objects
            cam_screen.display_to(user, ui.window);
        }
        return dot;
    }

    // tg: code/game/machinery/computer/camera.dm:72
    public override async DmTask<object> ui_status(mob user = null, datum_ui_state state = null)
    {
        object dot = await base.ui_status(user, state);
        if (Dm.eq(dot, UI_DISABLED))
            return UI_CLOSE;
        return dot;
    }

    // tg: code/game/machinery/computer/camera.dm:78
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["activeCamera"] = null;
        if (active_camera != null)
        {
            DmList camera = Dm.list();
            camera["name"] = active_camera.c_tag;
            camera["ref"] = REF(active_camera);
            camera["status"] = active_camera.camera_enabled;
            data["activeCamera"] = camera;
        }
        return data;
    }

    // tg: code/game/machinery/computer/camera.dm:89
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();
        data["network"] = network;
        data["mapRef"] = cam_screen.assigned_map;
        data["cameras"] = SScameras.get_available_cameras_data(network);
        return data;
    }

    // tg: code/game/machinery/computer/camera.dm:96
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        if (Dm.eq(action, "switch_camera"))
        {
            active_camera?.on_stop_watching(this);
            // locate(params["camera"]) in SScameras.cameras
            obj_machinery_camera selected_camera = Dm.locate_ref(@params?["camera"]) as obj_machinery_camera;
            if (selected_camera != null && !Dm.@in(selected_camera, SScameras.cameras))
                selected_camera = null;
            active_camera = selected_camera;
            cam_screen.set_display(active_camera);

            if (active_camera == null)
                return TRUE;

            active_camera.on_start_watching(this);
            update_active_camera_screen();

            return TRUE;
        }
        return dot;
    }

    // tg: code/game/machinery/computer/camera.dm:116
    public virtual void update_active_camera_screen()
    {
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
    }

    // tg: code/game/machinery/computer/camera.dm:148
    public override void ui_close(mob user = null)
    {
        base.ui_close(user);
        object user_ref = REF(user);
        bool is_living = isliving(user);
        // Living creature or not, we remove you anyway.
        concurrent_users.Remove(user_ref);
        // Unregister map objects
        cam_screen?.hide_from(user);
        // Turn off the console
        if (concurrent_users.len == 0 && is_living)
        {
            active_camera?.on_stop_watching(this);
            active_camera = null;
            cam_screen?.set_display(null);
            last_camera_turf = null;
            playsound(this, "sound/machines/terminal/terminal_off.ogg", 25, FALSE);
        }
    }
}

// tg: code/game/machinery/computer/camera.dm:164
public partial class atom_movable_screen_map_view_camera : atom_movable_screen_map_view
{
    private const int DEFAULT_MAP_SIZE = 15; // tg: camera.dm:1

    // tg: code/game/machinery/computer/camera.dm:168
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref cam_background);
        return base.Destroy(force);
    }

    // tg: code/game/machinery/computer/camera.dm:172
    public override void generate_view(object map_key = null)
    {
        base.generate_view(map_key);
        cam_background = Dm.@new<atom_movable_screen_background>();
        cam_background.del_on_map_removal = FALSE;
        cam_background.assigned_map = assigned_map;
    }

    // tg: code/game/machinery/computer/camera.dm:179
    public override object display_to_client(client show_to = null)
    {
        show_to.register_map_obj(cam_background);
        return base.display_to_client(show_to);
    }

    // tg: code/game/machinery/computer/camera.dm:183
    public virtual void show_camera(DmList visible_turfs = null, object size_x = null, object size_y = null)
    {
        vis_contents = visible_turfs;
        cam_background.icon_state = "clear";
        cam_background.fill_rect(1, 1, size_x, size_y);
    }

    // tg: code/game/machinery/computer/camera.dm:188
    public virtual void show_camera_static()
    {
        vis_contents.Cut();
        cam_background.icon_state = "scanline2";
        cam_background.fill_rect(1, 1, DEFAULT_MAP_SIZE, DEFAULT_MAP_SIZE);
    }
}
