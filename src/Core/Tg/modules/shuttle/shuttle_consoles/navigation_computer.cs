// Translated from tgstation (snapshot 1d99bd7f): code/modules/shuttle/shuttle_consoles/navigation_computer.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate N1c, D316: the infiltrator's navigation computer, which designates a custom landing spot for the shuttle
// through a camera eye; the landing zone's tiles are images in the user's client.images, drawn flat on their tiles by the 3D client,
// DV86). DEVIATION(DV349): the client's view size is left alone (setTo(view_range), resetToDefault). The var blocks are generated
// (D111).

// tg: code/modules/shuttle/shuttle_consoles/navigation_computer.dm:1
public partial class obj_machinery_computer_camera_advanced_shuttle_docker : obj_machinery_computer_camera_advanced
{
    // tg: code/modules/shuttle/shuttle_consoles/navigation_computer.dm:35
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.append(ref actions, Dm.@new<datum_action_innate_shuttledocker_rotate>(this));
        Dm.append(ref actions, Dm.@new<datum_action_innate_shuttledocker_place>(this));
        Waiting.NoSleep(AddElement(typeof(datum_element_nav_computer_icon), Dm.resource("icons/effects/nav_computer_indicators.dmi"), "computer", FALSE));
        set_init_ports();

        // SSshuttle?.: a world built without the shuttle subsystem (the host-built test and toy rooms), as the shuttle console's Initialize
        if (Dm.truthy(Waiting.NoSleep(connect_to_shuttle(mapload, SSshuttle?.get_containing_shuttle(this) as obj_docking_port_mobile))))
        {
            foreach (obj_docking_port_stationary port in Dm.each<obj_docking_port_stationary>(SSshuttle.stationary_docking_ports))
            {
                if (Dm.eq(port.shuttle_id, shuttleId))
                    add_jumpable_port(port.shuttle_id);
            }
        }

        foreach (obj_docking_port_stationary port in Dm.each<obj_docking_port_stationary>(SSshuttle?.stationary_docking_ports))
        {
            if (port == null)
                continue;
            if (Dm.truthy(jump_to_ports[port.shuttle_id]))
                Dm.combine(ref z_lock, port.z);
        }
        whitelist_turfs = typecacheof(whitelist_turfs) as DmList;
        return dot;
    }

    // tg: code/modules/shuttle/shuttle_consoles/navigation_computer.dm:54
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);
        if (Dm.truthy(my_port?.get_docked()))
        {
            my_port.delete_after = TRUE;
            my_port.shuttle_id = null;
            my_port.name = Dm.text("Old []", my_port.name);
            my_port = null;
        }
        else
            QDEL_NULL(ref my_port);
        return dot;
    }

    // tg: code/modules/shuttle/shuttle_consoles/navigation_computer.dm:64
    public override async DmTask<object> vv_edit_var(object var_name = null, object var_value = null)
    {
        object dot = await base.vv_edit_var(var_name, var_value);
        if (Dm.@in(var_name, Dm.list(nameof(view_range), nameof(x_offset), nameof(y_offset), nameof(see_hidden))))
            await refresh_eye();
        return dot;
    }

    /// Destroys the eyeobj of this console, safely refreshing it if the console is currently being used.
    // tg: code/modules/shuttle/shuttle_consoles/navigation_computer.dm:70
    public virtual async DmTask refresh_eye()
    {
        mob_living user = current_user;
        if (user != null)
            await remove_eye_control(user);
        QDEL_NULL(ref eyeobj);
        if (user != null)
            await attack_hand(user);
    }

    /// "Initializes" any default port ids we have, done so add_jumpable_port can be a proper setter
    // tg: code/modules/shuttle/shuttle_consoles/navigation_computer.dm:79
    public virtual void set_init_ports()
    {
        DmList init_ports = jump_to_ports.Copy();
        jump_to_ports = Dm.list(); //Reset it so we don't get dupes
        foreach (object port_id in Dm.each(init_ports))
            add_jumpable_port(port_id);
    }

    // tg: code/modules/shuttle/shuttle_consoles/navigation_computer.dm:85
    public virtual void add_jumpable_port(object port_id = null)
    {
        if (Dm.length(jump_to_ports) == 0)
            Dm.append(ref actions, Dm.@new<datum_action_innate_camera_jump_shuttle_docker>(this));
        jump_to_ports[port_id] = TRUE;
    }

    // tg: code/modules/shuttle/shuttle_consoles/navigation_computer.dm:90
    public virtual void remove_jumpable_port(object port_id = null)
    {
        jump_to_ports.Remove(port_id);
        if (Dm.length(jump_to_ports) == 0)
        {
            datum_action to_remove = Dm.locate<datum_action_innate_camera_jump_shuttle_docker>(actions);
            actions.Remove(to_remove);
            qdel(to_remove);
        }
    }

    // tg: code/modules/shuttle/shuttle_consoles/navigation_computer.dm:97
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(jammed))
        {
            to_chat(user, span_warning(I18n.T("The Syndicate is jamming the console!")));
            return null;
        }
        if (shuttle_port == null && SSshuttle.getShuttle(shuttleId) == null)
        {
            to_chat(user, span_warning(I18n.T("Warning: Shuttle connection severed!")));
            return null;
        }
        return await base.attack_hand(user, modifiers, __extra);
    }

    // tg: code/modules/shuttle/shuttle_consoles/navigation_computer.dm:106
    public override async DmTask<object> CreateEye()
    {
        shuttle_port = SSshuttle.getShuttle(shuttleId) as obj_docking_port_mobile;
        if (QDELETED(shuttle_port))
        {
            shuttle_port = null;
            return null;
        }

        eyeobj = Dm.@new<mob_eye_camera_remote_shuttle_docker>(null, this);
        var the_eye = eyeobj as mob_eye_camera_remote_shuttle_docker;
        await the_eye.setDir(shuttle_port.dir);
        turf origin = locate(shuttle_port.x + x_offset, shuttle_port.y + y_offset, shuttle_port.z);
        foreach (area shuttle_area in Dm.each<area>(shuttle_port.shuttle_areas))
        {
            foreach (DmList zlevel_turfs in Dm.each<DmList>(shuttle_area.get_zlevel_turf_lists()))
            {
                foreach (turf shuttle_turf in Dm.each<turf>(zlevel_turfs))
                {
                    if (shuttle_turf.z != origin.z)
                        continue;
                    image I = image(Dm.resource("icons/effects/alphacolors.dmi"), origin, "red");
                    float x_off = shuttle_turf.x - origin.x;
                    float y_off = shuttle_turf.y - origin.y;
                    I.loc = locate(origin.x + x_off, origin.y + y_off, origin.z); //we have to set this after creating the image because it might be null, and images created in nullspace are immutable.
                    I.layer = ABOVE_NORMAL_TURF_LAYER;
                    SET_PLANE(I, ABOVE_GAME_PLANE, shuttle_turf);
                    I.mouse_opacity = MOUSE_OPACITY_TRANSPARENT;
                    the_eye.placement_images[I] = Dm.list(x_off, y_off);
                }
            }
        }
        gatherNavComputerIcons();

        return TRUE;
    }

    // tg: code/modules/shuttle/shuttle_consoles/navigation_computer.dm:133
    public override async DmTask give_eye_control(mob user = null)
    {
        await base.give_eye_control(user);
        if (!QDELETED(user) && user.client != null)
        {
            var the_eye = eyeobj as mob_eye_camera_remote_shuttle_docker;
            DmList to_add = Dm.list();
            Dm.append(ref to_add, the_eye.placement_images);
            Dm.append(ref to_add, the_eye.placed_images);
            Dm.append(ref to_add, the_eye.extra_images);
            if (!Dm.truthy(see_hidden))
                Dm.append(ref to_add, SSshuttle.hidden_shuttle_turf_images);

            Dm.append(user.client.images, to_add);
            // DEVIATION(DV349): user.client.view_size.setTo(view_range): the first-person view has no view size to widen
        }
    }

    // tg: code/modules/shuttle/shuttle_consoles/navigation_computer.dm:147
    public override async DmTask remove_eye_control(mob_living user = null)
    {
        await base.remove_eye_control(user);
        if (!QDELETED(user) && user.client != null)
        {
            var the_eye = eyeobj as mob_eye_camera_remote_shuttle_docker;
            DmList to_remove = Dm.list();
            Dm.append(ref to_remove, the_eye.placement_images);
            Dm.append(ref to_remove, the_eye.placed_images);
            Dm.append(ref to_remove, the_eye.extra_images);
            if (!Dm.truthy(see_hidden))
                Dm.append(ref to_remove, SSshuttle.hidden_shuttle_turf_images);

            Dm.remove(user.client.images, to_remove);
            // DEVIATION(DV349): user.client.view_size.resetToDefault(): the first-person view has no view size to widen
        }
    }

    // tg: code/modules/shuttle/shuttle_consoles/navigation_computer.dm:161
    public virtual turf shuttle_turf_from_coords(DmList coords = null)
    {
        var the_eye = eyeobj as mob_eye_camera_remote_shuttle_docker;
        float shuttleDir = shuttle_port.dir;
        float curDir = the_eye.dir;
        DmList adjustedCoords = coords.Copy();

        // Rotate coords so  they match the current shuttle docking port's dir
        if (Dm.num(turn(curDir, -90)) == shuttleDir)
        {
            adjustedCoords[1] = Dm.num(coords[2]) + y_offset;
            adjustedCoords[2] = -(Dm.num(coords[1]) + x_offset);
        }
        else if (Dm.num(turn(curDir, 90)) == shuttleDir)
        {
            adjustedCoords[1] = -(Dm.num(coords[2]) + y_offset);
            adjustedCoords[2] = Dm.num(coords[1]) + x_offset;
        }
        else if (Dm.num(turn(curDir, 180)) == shuttleDir)
        {
            adjustedCoords[1] = -(Dm.num(coords[1]) + x_offset);
            adjustedCoords[2] = -(Dm.num(coords[2]) + y_offset);
        }
        else
        {
            adjustedCoords[1] = Dm.num(coords[1]) + x_offset;
            adjustedCoords[2] = Dm.num(coords[2]) + y_offset;
        }

        return locate(shuttle_port.x + Dm.num(adjustedCoords[1]), shuttle_port.y + Dm.num(adjustedCoords[2]), shuttle_port.z);
    }

    // tg: code/modules/shuttle/shuttle_consoles/navigation_computer.dm:183
    public virtual void gatherNavComputerIcons()
    {
        var the_eye = eyeobj as mob_eye_camera_remote_shuttle_docker;
        DmList placement_image_cache = the_eye.placement_images;
        DmList extra_image_cache = the_eye.extra_images;
        for (int i = 1; i <= placement_image_cache.len; i++)
        {
            var placement_image = placement_image_cache[i] as image;
            var coords = placement_image_cache[placement_image] as DmList;
            turf shuttle_turf = shuttle_turf_from_coords(coords);
            DmList images_to_add = Dm.list();
            foreach (atom atom in Dm.each<atom>(shuttle_turf?.contents))
            {
                if (Dm.truthy(atom._listen_lookup?[COMSIG_SHUTTLE_NAV_COMPUTER_IMAGE_REQUESTED]))
                    atom._SendSignal(COMSIG_SHUTTLE_NAV_COMPUTER_IMAGE_REQUESTED, Dm.list(atom, images_to_add));
            }
            for (int i2 = 1; i2 <= images_to_add.len; i2++)
            {
                var extra_image = images_to_add[i2] as image;
                extra_image.dir = Dm.num(turn(extra_image.dir, Dm.num(dir2angle(the_eye.dir)) - Dm.num(dir2angle(shuttle_port.dir))));
                extra_image.loc = locate(the_eye.x + Dm.num(coords[1]), the_eye.y + Dm.num(coords[2]), the_eye.z);
                extra_image.layer = ABOVE_NORMAL_TURF_LAYER;
                SET_PLANE(extra_image, ABOVE_GAME_PLANE, the_eye);
                extra_image.mouse_opacity = MOUSE_OPACITY_TRANSPARENT;
                extra_image_cache[extra_image] = coords.Copy();
            }
        }
    }

    // tg: code/modules/shuttle/shuttle_consoles/navigation_computer.dm:203
    public virtual async DmTask<object> placeLandingSpot()
    {
        if (designating_target_loc != null || current_user == null)
            return null;

        var the_eye = eyeobj as mob_eye_camera_remote_shuttle_docker;
        object landing_clear = checkLandingSpot();
        if (Dm.truthy(designate_time) && !Dm.eq(landing_clear, SHUTTLE_DOCKER_BLOCKED))
        {
            to_chat(current_user, span_warning(I18n.T("Targeting transit location, please wait []...", DisplayTimeText(designate_time))));
            designating_target_loc = the_eye.loc as turf;
            object wait_completed = await do_after(current_user, designate_time, designating_target_loc, timed_action_flags: IGNORE_HELD_ITEM,
                extra_checks: Dm.@new<datum_callback>(this, nameof(canDesignateTarget)));
            designating_target_loc = null;
            if (current_user == null)
                return null;
            if (!Dm.truthy(wait_completed))
            {
                to_chat(current_user, span_warning(I18n.T("Operation aborted.")));
                return null;
            }
            landing_clear = checkLandingSpot();
        }

        if (!Dm.eq(landing_clear, SHUTTLE_DOCKER_LANDING_CLEAR))
        {
            switch (Dm.num(landing_clear))
            {
                case SHUTTLE_DOCKER_BLOCKED:
                    to_chat(current_user, span_warning(I18n.T("Invalid transit location.")));
                    break;
                case SHUTTLE_DOCKER_BLOCKED_BY_HIDDEN_PORT:
                    to_chat(current_user, span_warning(I18n.T("Unknown object detected in landing zone. Please designate another location.")));
                    break;
            }
            return null;
        }

        ///Make one use port that deleted after fly off, to don't lose info that need on to properly fly off.
        if (Dm.truthy(my_port?.get_docked()))
        {
            my_port.unregister();
            my_port.delete_after = TRUE;
            my_port.shuttle_id = null;
            my_port.name = Dm.text("Old []", my_port.name);
            my_port = null;
        }

        if (my_port == null)
        {
            my_port = Dm.@new<obj_docking_port_stationary>();
            my_port.unregister();
            my_port.name = shuttlePortName;
            my_port.shuttle_id = shuttlePortId;
            my_port.height = shuttle_port.height;
            my_port.width = shuttle_port.width;
            my_port.dheight = shuttle_port.dheight;
            my_port.dwidth = shuttle_port.dwidth;
            my_port.hidden = shuttle_port.hidden;
            my_port.register(TRUE);
        }
        await my_port.setDir(the_eye.dir);
        await my_port.forceMove(locate(eyeobj.x - x_offset, eyeobj.y - y_offset, eyeobj.z));

        if (current_user.client != null)
            Dm.remove(current_user.client.images, the_eye.placed_images);

        the_eye.placed_images?.Cut(); // LAZYCLEARLIST

        foreach (image place_spots in Dm.each<image>(the_eye.placement_images))
        {
            image newI = image(Dm.resource("icons/effects/alphacolors.dmi"), the_eye.loc, "blue");
            newI.loc = place_spots.loc; //It is highly unlikely that any landing spot including a null tile will get this far, but better safe than sorry.
            newI.layer = NAVIGATION_EYE_LAYER;
            SET_PLANE_EXPLICIT(newI, ABOVE_GAME_PLANE, place_spots);
            newI.mouse_opacity = 0;
            Dm.append(ref the_eye.placed_images, newI);
        }

        if (current_user.client != null)
        {
            Dm.append(current_user.client.images, the_eye.placed_images);
            to_chat(current_user, span_notice(I18n.T("Transit location designated.")));
        }
        return TRUE;
    }

    // tg: code/modules/shuttle/shuttle_consoles/navigation_computer.dm:269
    public virtual object canDesignateTarget()
    {
        if (designating_target_loc == null || current_user == null || eyeobj.loc != designating_target_loc || Dm.truthy(Dm.bitand(machine_stat, NOPOWER | BROKEN)))
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/shuttle/shuttle_consoles/navigation_computer.dm:274
    public virtual async DmTask rotateLandingSpot()
    {
        var the_eye = eyeobj as mob_eye_camera_remote_shuttle_docker;
        DmList image_cache = Dm.add(the_eye.placement_images, the_eye.extra_images) as DmList;
        await the_eye.setDir(turn(the_eye.dir, -90));
        for (int i = 1; i <= image_cache.len; i++)
        {
            var pic = image_cache[i] as image;
            var coords = image_cache[pic] as DmList;
            object Tmp = coords[1];
            coords[1] = coords[2];
            coords[2] = -Dm.num(Tmp);
            pic.loc = locate(the_eye.x + Dm.num(coords[1]), the_eye.y + Dm.num(coords[2]), the_eye.z);
            pic.dir = Dm.num(turn(pic.dir, -90));
        }
        float Tmp2 = x_offset;
        x_offset = y_offset;
        y_offset = -Tmp2;
        checkLandingSpot();
    }

    // tg: code/modules/shuttle/shuttle_consoles/navigation_computer.dm:291
    public virtual object checkLandingSpot()
    {
        var the_eye = eyeobj as mob_eye_camera_remote_shuttle_docker;
        turf eyeturf = get_turf(the_eye);
        if (eyeturf == null)
            return SHUTTLE_DOCKER_BLOCKED;
        if (!Dm.truthy(eyeturf.z) || Dm.truthy(SSmapping.level_has_any_trait(eyeturf.z, locked_traits)))
            return SHUTTLE_DOCKER_BLOCKED;

        object dot = SHUTTLE_DOCKER_LANDING_CLEAR;
        DmList bounds = shuttle_port.return_coords(the_eye.x - x_offset, the_eye.y - y_offset, the_eye.dir);
        DmList overlappers = SSshuttle.get_dock_overlap(Dm.num(bounds[1]), Dm.num(bounds[2]), Dm.num(bounds[3]), Dm.num(bounds[4]), the_eye.z);
        DmList image_cache = the_eye.placement_images;
        for (int i = 1; i <= image_cache.len; i++)
        {
            var I = image_cache[i] as image;
            var coords = image_cache[I] as DmList;
            turf T = locate(eyeturf.x + Dm.num(coords[1]), eyeturf.y + Dm.num(coords[2]), eyeturf.z);
            I.loc = T;
            switch (Dm.num(checkLandingTurf(T, overlappers)))
            {
                case SHUTTLE_DOCKER_LANDING_CLEAR:
                    I.icon_state = "green";
                    break;
                case SHUTTLE_DOCKER_BLOCKED_BY_HIDDEN_PORT:
                    I.icon_state = "green";
                    if (Dm.eq(dot, SHUTTLE_DOCKER_LANDING_CLEAR))
                        dot = SHUTTLE_DOCKER_BLOCKED_BY_HIDDEN_PORT;
                    break;
                default:
                    I.icon_state = "red";
                    dot = SHUTTLE_DOCKER_BLOCKED;
                    break;
            }
        }
        DmList extra_image_cache = the_eye.extra_images;
        for (int i = 1; i <= extra_image_cache.len; i++)
        {
            var image = extra_image_cache[i] as image;
            var coords = extra_image_cache[image] as DmList;
            turf turf = locate(eyeturf.x + Dm.num(coords[1]), eyeturf.y + Dm.num(coords[2]), eyeturf.z);
            image.loc = turf;
        }
        return dot;
    }

    // tg: code/modules/shuttle/shuttle_consoles/navigation_computer.dm:325
    public virtual object checkLandingTurf(turf T = null, DmList overlappers = null)
    {
        // Too close to the map edge is never allowed
        if (T == null || T.x <= 10 || T.y <= 10 || T.x >= world.maxx - 10 || T.y >= world.maxy - 10)
            return SHUTTLE_DOCKER_BLOCKED;
        // If it's one of our shuttle areas assume it's ok to be there
        if (Dm.truthy(shuttle_port.shuttle_areas[T.loc]))
            return SHUTTLE_DOCKER_LANDING_CLEAR;
        object dot = SHUTTLE_DOCKER_LANDING_CLEAR;
        // See if the turf is hidden from us
        DmList hidden_turf_info = null;
        if (!Dm.truthy(see_hidden))
        {
            hidden_turf_info = SSshuttle.hidden_shuttle_turfs[T] as DmList;
            if (hidden_turf_info != null)
                dot = SHUTTLE_DOCKER_BLOCKED_BY_HIDDEN_PORT;
        }

        if (Dm.length(whitelist_turfs) > 0)
        {
            object turf_type = hidden_turf_info != null ? hidden_turf_info[2] : T.type;
            if (!Dm.truthy(is_type_in_typecache(turf_type, whitelist_turfs)))
                return SHUTTLE_DOCKER_BLOCKED;
        }

        // Checking for overlapping dock boundaries
        for (int i = 1; i <= overlappers.len; i++)
        {
            var port = overlappers[i] as obj_docking_port;
            if (port == my_port)
                continue;
            bool port_hidden = !Dm.truthy(see_hidden) && Dm.truthy(port.hidden);
            var overlap = overlappers[port] as DmList;
            var xs = overlap[1] as DmList;
            var ys = overlap[2] as DmList;
            if (Dm.truthy(xs[Dm.text("[]", T.x)]) && Dm.truthy(ys[Dm.text("[]", T.y)]))
            {
                if (port_hidden)
                    dot = SHUTTLE_DOCKER_BLOCKED_BY_HIDDEN_PORT;
                else
                    return SHUTTLE_DOCKER_BLOCKED;
            }
        }
        return dot;
    }

    // tg: code/modules/shuttle/shuttle_consoles/navigation_computer.dm:360
    public virtual void update_hidden_docking_ports(DmList remove_images = null, DmList add_images = null)
    {
        if (!Dm.truthy(see_hidden) && current_user?.client != null)
        {
            Dm.remove(current_user.client.images, remove_images);
            Dm.append(current_user.client.images, add_images);
        }
    }

    // tg: code/modules/shuttle/shuttle_consoles/navigation_computer.dm:365
    public override async DmTask<object> connect_to_shuttle(object mapload = null, obj_docking_port_mobile port = null, obj_docking_port_stationary dock = null)
    {
        if (!Dm.truthy(mapload))
            return FALSE;
        if (port != null)
        {
            shuttleId = Dm.str(port.shuttle_id);
            shuttlePortId = Dm.text("[]_custom", port.shuttle_id);
        }
        if (dock != null)
            add_jumpable_port(dock.shuttle_id);
        return TRUE;
    }
}

// tg: code/modules/shuttle/shuttle_consoles/navigation_computer.dm:375
public partial class mob_eye_camera_remote_shuttle_docker : mob_eye_camera_remote
{
    // tg: code/modules/shuttle/shuttle_consoles/navigation_computer.dm:386
    public override void setLoc(object destination = null, float? force_update = null)
    {
        base.setLoc(destination, force_update);
        var console = origin_ref?.resolve() as obj_machinery_computer_camera_advanced_shuttle_docker;
        console.checkLandingSpot();
    }

    // tg: code/modules/shuttle/shuttle_consoles/navigation_computer.dm:391
    public override object allow_z_transition(datum_space_level from = null, datum_space_level into = null)
    {
        object dot = base.allow_z_transition(from, into);
        if (Dm.truthy(dot))
            return dot;
        var console = origin_ref?.resolve() as obj_machinery_computer_camera_advanced_shuttle_docker;
        if (!Dm.truthy(console.zlink_range))
            return null;
        obj_docking_port_mobile shuttle = console.shuttle_port;
        if (shuttle == null)
            return null;
        DmList zlevel_stack = SSmapping.get_connected_levels(shuttle.z);
        DmList zlevels_in_range = Dm.list();
        foreach (object level in Dm.each(zlevel_stack))
            Dm.combine(ref zlevels_in_range, get_linked_z_levels_in_range(level, console.zlink_range));
        return zlevels_in_range[into];
    }
}

// tg: code/modules/shuttle/shuttle_consoles/navigation_computer.dm:407
public partial class datum_action_innate_shuttledocker_rotate : datum_action_innate
{
    // tg: code/modules/shuttle/shuttle_consoles/navigation_computer.dm:412
    public override async DmTask<object> Activate()
    {
        if (QDELETED(owner) || !isliving(owner))
            return null;
        var remote_eye = owner.remote_control as mob_eye_camera_remote;
        var origin = remote_eye.origin_ref.resolve() as obj_machinery_computer_camera_advanced_shuttle_docker;
        await origin.rotateLandingSpot();
        return null;
    }
}

// tg: code/modules/shuttle/shuttle_consoles/navigation_computer.dm:419
public partial class datum_action_innate_shuttledocker_place : datum_action_innate
{
    // tg: code/modules/shuttle/shuttle_consoles/navigation_computer.dm:424
    public override async DmTask<object> Activate()
    {
        if (QDELETED(owner) || !isliving(owner))
            return null;
        var remote_eye = owner.remote_control as mob_eye_camera_remote;
        var origin = remote_eye.origin_ref.resolve() as obj_machinery_computer_camera_advanced_shuttle_docker;
        await origin.placeLandingSpot(); // tg passes owner, which the proc does not take
        return null;
    }
}

// tg: code/modules/shuttle/shuttle_consoles/navigation_computer.dm:431
public partial class datum_action_innate_camera_jump_shuttle_docker : datum_action_innate_camera_jump
{
    // tg: code/modules/shuttle/shuttle_consoles/navigation_computer.dm:435
    public override async DmTask<object> Activate()
    {
        if (QDELETED(owner) || !isliving(owner))
            return null;
        var remote_eye = owner.remote_control as mob_eye_camera_remote;
        var console = remote_eye.origin_ref.resolve() as obj_machinery_computer_camera_advanced_shuttle_docker;

        playsound(console, "sound/machines/terminal/terminal_prompt_deny.ogg", 25, FALSE);

        DmList L = Dm.list();
        foreach (object V in Dm.each(SSshuttle.stationary_docking_ports))
        {
            if (V == null)
            {
                stack_trace("SSshuttle.stationary_docking_ports have null entry!", "code/modules/shuttle/shuttle_consoles/navigation_computer.dm", 446);
                continue;
            }
            var S = V as obj_docking_port_stationary;
            if (Dm.truthy(console.z_lock.len) && !Dm.@in(S.z, console.z_lock))
                continue;
            if (Dm.truthy(console.jump_to_ports[S.shuttle_id]))
                L[Dm.text("([])[]", L.len, S.name)] = S;
        }

        foreach (object V in Dm.each(SSshuttle.beacon_list))
        {
            if (V == null)
            {
                stack_trace("SSshuttle.beacon_list have null entry!", "code/modules/shuttle/shuttle_consoles/navigation_computer.dm", 456);
                continue;
            }
            var nav_beacon = V as obj_machinery_spaceship_navigation_beacon;
            if (!Dm.truthy(nav_beacon.z) || Dm.truthy(SSmapping.level_has_any_trait(nav_beacon.z, console.locked_traits)))
                break;
            if (!Dm.truthy(nav_beacon.locked))
                L[Dm.text("([]) [] located: [] [] []", L.len, nav_beacon.name, nav_beacon.x, nav_beacon.y, nav_beacon.z)] = nav_beacon;
            else
                L[Dm.text("([]) [] locked", L.len, nav_beacon.name)] = null;
        }

        playsound(console, "sound/machines/terminal/terminal_prompt.ogg", 25, FALSE);
        object selected = await tgui_input_list(usr, "Choose location to jump to", "Locations", sort_list(L) as DmList);
        if (selected == null)
        {
            playsound(console, "sound/machines/terminal/terminal_prompt_deny.ogg", 25, FALSE);
            return null;
        }
        if (QDELETED(this) || QDELETED(owner) || !isliving(owner))
            return null;
        // playsound(src, SFX_TERMINAL_TYPE, 25, FALSE): src is the action, a datum with no turf, so tg plays nothing
        turf T = get_turf(L[selected] as atom);
        if (T == null)
            return null;
        playsound(console, "sound/machines/terminal/terminal_prompt_confirm.ogg", 25, FALSE);
        remote_eye.setLoc(T);
        to_chat(owner, span_notice(I18n.T("Jumped to [].", selected)));
        owner.overlay_fullscreen("flash", typeof(atom_movable_screen_fullscreen_flash_static));
        owner.clear_fullscreen("flash", 3);
        return null;
    }
}
