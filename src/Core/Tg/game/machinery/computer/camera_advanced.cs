// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/computer/camera_advanced.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (syndicate N1c, D316: the base of the infiltrator's navigation computer, and the syndicate's advanced camera console on its
// bridge): the console, its camera eye and its action buttons, the circuits' USB port with its two circuit components (the eye's
// position, the target intercept) and the action buttons a plugged circuit's equipment action components add. Not translated: the
// AI's and a cyborg's clicks (silicons), the abductor's console (its own type, inert); the human AI's console came with its job. The camera chunks a
// plain console's eye asks for come with the AI's freelook (silicons SI4): until then SScameras sees no turf, so the eye starts at a
// camera of its networks and sees past the cameras' reach. DEVIATION(DV349): the client's view size is left alone (supress,
// unsupress). The var blocks are generated (D111).

// tg: code/game/machinery/computer/camera_advanced.dm:1
public partial class obj_machinery_computer_camera_advanced : obj_machinery_computer
{
    // tg: code/game/machinery/computer/camera_advanced.dm:34
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        foreach (object i in Dm.each(networks.Copy()))
        {
            networks.Remove(i);
            Dm.append(ref networks, LOWER_TEXT(i));
        }
        if (Dm.truthy(lock_override))
        {
            if (Dm.truthy(Dm.bitand(lock_override, CAMERA_LOCK_STATION)))
                Dm.combine(ref z_lock, SSmapping.levels_by_trait(ZTRAIT_STATION));
            if (Dm.truthy(Dm.bitand(lock_override, CAMERA_LOCK_MINING)))
                Dm.combine(ref z_lock, SSmapping.levels_by_trait(ZTRAIT_MINING));
            if (Dm.truthy(Dm.bitand(lock_override, CAMERA_LOCK_CENTCOM)))
                Dm.combine(ref z_lock, SSmapping.levels_by_trait(ZTRAIT_CENTCOM));
        }

        if (off_action != null)
            Dm.append(ref actions, Dm.@new(off_action, this));
        if (jump_action != null)
            Dm.append(ref actions, Dm.@new(jump_action, this));
        //Camera action button to move up a Z level
        if (move_up_action != null)
            Dm.append(ref actions, Dm.@new(move_up_action, this));
        //Camera action button to move down a Z level
        if (move_down_action != null)
            Dm.append(ref actions, Dm.@new(move_down_action, this));
        if (Dm.truthy(add_usb_port))
        {
            Waiting.NoSleep(AddComponent(typeof(datum_component_usb_port),
                typecacheof(Dm.list(
                    typeof(obj_item_circuit_component_advanced_camera),
                    typeof(obj_item_circuit_component_advanced_camera_intercept)
                ), only_root_path: TRUE),
                Dm.named("extra_registration_callback", nameof(register_usb_port)),
                Dm.named("extra_unregistration_callback", nameof(unregister_usb_port))
            ));
        }
        return dot;
    }

    // tg: code/game/machinery/computer/camera_advanced.dm:68
    public override object Destroy(float? force = null)
    {
        Waiting.NoSleep(unset_machine());
        QDEL_NULL(ref eyeobj);
        QDEL_LIST(actions);
        current_user = null;
        return base.Destroy(force);
    }

    // tg: code/game/machinery/computer/camera_advanced.dm:75
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (!Dm.truthy(Waiting.NoSleep(can_use(current_user))) || (issilicon(current_user) && !HAS_SILICON_ACCESS(current_user)))
        {
            Waiting.NoSleep(unset_machine());
            return PROCESS_KILL;
        }
        return null;
    }

    // tg: code/game/machinery/computer/camera_advanced.dm:80
    public override async DmTask<object> connect_to_shuttle(object mapload = null, obj_docking_port_mobile port = null, obj_docking_port_stationary dock = null)
    {
        foreach (object i in Dm.each(networks.Copy()))
        {
            networks.Remove(i);
            Dm.append(ref networks, Dm.text("[]_[]", port.shuttle_id, i));
        }
        return null;
    }

    /**
     * Initializes a camera eye.
     * Returns TRUE if initialization was successful.
     * Will return nothing if it runtimes.
     */
    // tg: code/game/machinery/computer/camera_advanced.dm:98
    public virtual async DmTask<object> CreateEye()
    {
        if (eyeobj != null)
            throw Dm.CRASH("Tried to make another eyeobj for some reason. Why?", "code/game/machinery/computer/camera_advanced.dm", 100);

        eyeobj = Dm.@new<mob_eye_camera_remote>(get_turf(this), this);
        return TRUE;
    }

    // tg: code/game/machinery/computer/camera_advanced.dm:105
    public virtual async DmTask GrantActions(mob_living user = null)
    {
        foreach (datum_action to_grant in Dm.each<datum_action>(actions))
            await to_grant.Grant(user);
    }

    // tg: code/game/machinery/computer/camera_advanced.dm:112
    public virtual async DmTask give_eye_control(mob user = null)
    {
        if (user?.client == null)
            return;

        current_user = user as mob_living;
        await eyeobj.assign_user(user as mob_living);
        await GrantActions(user as mob_living);

        // DEVIATION(DV349): if(should_supress_view_changes) user.client.view_size.supress(): the first-person view has no view size to hold
        begin_processing();
    }

    // tg: code/game/machinery/computer/camera_advanced.dm:124
    public override async DmTask remove_eye_control(mob_living user = null)
    {
        if (user?.client == null)
            return;

        foreach (datum_action actions_removed in Dm.each<datum_action>(actions))
            await actions_removed.Remove(user);

        await eyeobj.assign_user(null);
        current_user = null;

        // DEVIATION(DV349): user.client.view_size.unsupress(): the first-person view has no view size to hold

        playsound(this, "sound/machines/terminal/terminal_off.ogg", 25, FALSE);
    }

    // tg: code/game/machinery/computer/camera_advanced.dm:138
    public override async DmTask on_set_is_operational(object old_value = null)
    {
        if (!Dm.truthy(is_operational))
            await unset_machine();
    }

    // tg: code/game/machinery/computer/camera_advanced.dm:142
    public virtual async DmTask unset_machine()
    {
        if (!QDELETED(current_user))
            await remove_eye_control(current_user);
        end_processing();
    }

    // tg: code/game/machinery/computer/camera_advanced.dm:147
    public virtual async DmTask<object> can_use(mob_living user = null)
    {
        return await can_interact(user);
    }

    // tg: code/game/machinery/computer/camera_advanced.dm:155
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        if (!Dm.truthy(await can_use(user as mob_living)))
            return dot;
        if (user.client == null)
            return dot;
        if (!QDELETED(current_user))
        {
            to_chat(user, span_warning(I18n.T("The console is already in use!")));
            return dot;
        }

        if (eyeobj != null)
        {
            await give_eye_control(user);
            eyeobj.setLoc(eyeobj.loc);
            return dot;
        }
        /* We're attempting to initialize the eye past this point */

        if (!Dm.truthy(await CreateEye()))
        {
            to_chat(user, span_warning(I18n.T("\\The [] flashes a bunch of never-ending errors on the display. Something is really wrong.", this)));
            return dot;
        }

        if (Dm.truthy(_listen_lookup?[COMSIG_ADVANCED_CAMERA_EYE_CREATED]))
            _SendSignal(COMSIG_ADVANCED_CAMERA_EYE_CREATED, Dm.list(this, eyeobj));

        turf camera_location = null;
        turf myturf = get_turf(this);
        bool consider_zlock = Dm.length(z_lock) > 0;

        if (!Dm.truthy(eyeobj.use_visibility))
        {
            if (consider_zlock && !Dm.@in(myturf.z, z_lock))
                camera_location = locate(Dm.round(world.maxx * 0.5f), Dm.round(world.maxy * 0.5f), Dm.num(z_lock[1]));
            else
                camera_location = myturf;
        }
        else
        {
            if ((!consider_zlock || Dm.@in(myturf.z, z_lock)) && Dm.truthy(SScameras.is_visible_by_cameras(myturf)))
                camera_location = myturf;
            else
            {
                foreach (obj_machinery_camera C in Dm.each<obj_machinery_camera>(SScameras.cameras))
                {
                    if (!Dm.truthy(C.can_use()) || consider_zlock && !Dm.@in(C.z, z_lock))
                        continue;
                    object network_overlap = Dm.bitand(networks, C.network);
                    if (Dm.length(network_overlap) > 0)
                    {
                        camera_location = get_turf(C);
                        break;
                    }
                }
            }
        }

        if (camera_location != null)
        {
            await give_eye_control(user);
            eyeobj.setLoc(camera_location, TRUE);
        }
        else
            await unset_machine();
        return dot;
    }
}

// tg: code/game/machinery/computer/camera_advanced.dm:85
public partial class obj_machinery_computer_camera_advanced_syndie : obj_machinery_computer_camera_advanced
{
    // tg: code/game/machinery/computer/camera_advanced.dm:90
    public override async DmTask<object> connect_to_shuttle(object mapload = null, obj_docking_port_mobile port = null, obj_docking_port_stationary dock = null)
    {
        return null; //For syndie nuke shuttle, to spy for station.
    }
}

// tg: code/game/machinery/computer/camera_advanced.dm:109
public partial class obj_machinery : obj
{
    // tg: code/game/machinery/computer/camera_advanced.dm:109
    public virtual async DmTask remove_eye_control(mob_living user = null)
    {
        throw Dm.CRASH(Dm.text("[] does not implement camera eye handling", type), "code/game/machinery/computer/camera_advanced.dm", 110);
    }
}

// tg: code/game/machinery/computer/camera_advanced.dm:212
public partial class datum_action_innate_camera_off : datum_action_innate
{
    // tg: code/game/machinery/computer/camera_advanced.dm:217
    public override async DmTask<object> Activate()
    {
        if (owner == null || !isliving(owner))
            return null;
        var remote_eye = owner.remote_control as mob_eye_camera_remote;
        var console = remote_eye.origin_ref.resolve() as obj_machinery_computer_camera_advanced;
        await console.remove_eye_control(owner as mob_living);
        return null;
    }
}

// tg: code/game/machinery/computer/camera_advanced.dm:224
public partial class datum_action_innate_camera_jump : datum_action_innate
{
    // tg: code/game/machinery/computer/camera_advanced.dm:229
    public override async DmTask<object> Activate()
    {
        if (owner == null || !isliving(owner))
            return null;
        var remote_eye = owner.remote_control as mob_eye_camera_remote;
        var origin = remote_eye.origin_ref.resolve() as obj_machinery_computer_camera_advanced;

        DmList cameras_by_tag = SScameras.get_available_camera_by_tag_list(origin.networks, origin.z_lock);

        playsound(origin, "sound/machines/terminal/terminal_prompt.ogg", 25, FALSE);
        object camera = await tgui_input_list(usr, "Camera to view", "Cameras", cameras_by_tag);
        if (camera == null)
            return null;

        // playsound(src, SFX_TERMINAL_TYPE, 25, FALSE): src is the action, a datum with no turf, so tg plays nothing

        var chosen_camera = cameras_by_tag[camera] as obj_machinery_camera;
        if (chosen_camera == null)
        {
            playsound(origin, "sound/machines/terminal/terminal_prompt_deny.ogg", 25, FALSE);
            return null;
        }

        playsound(origin, "sound/machines/terminal/terminal_prompt_confirm.ogg", 25, FALSE);
        remote_eye.setLoc(get_turf(chosen_camera));
        owner.overlay_fullscreen("flash", typeof(atom_movable_screen_fullscreen_flash_static));
        owner.clear_fullscreen("flash", 3); //Shorter flash than normal since it's an ~~advanced~~ console!
        return null;
    }
}

// tg: code/game/machinery/computer/camera_advanced.dm:254
public partial class datum_action_innate_camera_multiz_up : datum_action_innate
{
    // tg: code/game/machinery/computer/camera_advanced.dm:259
    public override async DmTask<object> Activate()
    {
        if (owner == null || !isliving(owner))
            return null;
        var remote_eye = owner.remote_control as mob_eye_camera_remote;
        if (Dm.truthy(await remote_eye.zMove(UP)))
            to_chat(owner, span_notice(I18n.T("You move upwards.")));
        else
            to_chat(owner, span_notice(I18n.T("You can't move upwards!")));
        return null;
    }
}

// tg: code/game/machinery/computer/camera_advanced.dm:268
public partial class datum_action_innate_camera_multiz_down : datum_action_innate
{
    // tg: code/game/machinery/computer/camera_advanced.dm:273
    public override async DmTask<object> Activate()
    {
        if (owner == null || !isliving(owner))
            return null;
        var remote_eye = owner.remote_control as mob_eye_camera_remote;
        if (Dm.truthy(await remote_eye.zMove(DOWN)))
            to_chat(owner, span_notice(I18n.T("You move downwards.")));
        else
            to_chat(owner, span_notice(I18n.T("You can't move downwards!")));
        return null;
    }
}

// The human AI's console, unpacked from its pod in the AI satellite's chamber (station_trait/human_ai.dm:171): a screwdriver packs it
// back into the pod
// tg: code/game/machinery/computer/camera_advanced.dm:282
public partial class obj_machinery_computer_camera_advanced_human_ai : obj_machinery_computer_camera_advanced
{
    // tg: code/game/machinery/computer/camera_advanced.dm:282
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        balloon_alert(user, I18n.T("repackaging..."));
        if (!Dm.truthy(await do_after(user, 5 * SECONDS, this)))
            return ITEM_INTERACT_BLOCKING;
        tool.play_tool_sound(this, 40);
        Dm.@new<obj_item_secure_camera_console_pod>(get_turf(this));
        qdel(this);
        return ITEM_INTERACT_SUCCESS;
    }
}

// The AI's and cyborgs' clicks (silicons SI2, D326)
// tg: code/game/machinery/computer/camera_advanced.dm:206
public partial class obj_machinery_computer_camera_advanced : obj_machinery_computer
{
    // tg: code/game/machinery/computer/camera_advanced.dm:206
    public override async DmTask<object> attack_robot(mob user = null, object modifiers = null)
    {
        return await attack_hand(user);
    }

    // tg: code/game/machinery/computer/camera_advanced.dm:209
    public override async DmTask<object> attack_ai(mob user = null)
    {
        return null; //AIs would need to disable their own camera procs to use the console safely. Bugs happen otherwise.
    }
}

/// Equipment action component support

// tg: code/game/machinery/computer/camera_advanced.dm:293
public partial class obj_machinery_computer_camera_advanced : obj_machinery_computer
{
    // tg: code/game/machinery/computer/camera_advanced.dm:293
    public virtual void register_usb_port(datum_component_usb_port port = null)
    {
        RegisterSignal(port, COMSIG_USB_PORT_REGISTER_PHYSICAL_OBJECT, nameof(on_port_register_object));
        RegisterSignal(port, COMSIG_USB_PORT_UNREGISTER_PHYSICAL_OBJECT, nameof(on_port_unregister_object));
        if (port.physical_object != null)
            on_port_register_object(port, port.physical_object);
    }

    // tg: code/game/machinery/computer/camera_advanced.dm:299
    [SignalHandler]
    public virtual void on_port_register_object(datum_component_usb_port source = null, atom_movable @object = null)
    {
        obj_item_integrated_circuit circuit = source.attached_circuit;
        if (@object == circuit)
            return;
        RegisterSignal(@object, COMSIG_CIRCUIT_ACTION_COMPONENT_REGISTERED, nameof(add_circuit_action));
        RegisterSignal(@object, COMSIG_CIRCUIT_ACTION_COMPONENT_UNREGISTERED, nameof(remove_circuit_action));
        foreach (obj_item_circuit_component_equipment_action action_comp in Dm.each<obj_item_circuit_component_equipment_action>(circuit.attached_components))
            add_circuit_action(null, action_comp);
    }

    // tg: code/game/machinery/computer/camera_advanced.dm:309
    [SignalHandler]
    public virtual void add_circuit_action(datum _source = null, obj_item_circuit_component_equipment_action action_comp = null)
    {
        datum_action_innate_circuit_equipment_action new_action = Dm.@new<datum_action_innate_circuit_equipment_action>(this, action_comp);
        LAZYADD(ref actions, new_action);
        if (current_user != null)
            Waiting.NoSleep(new_action.Grant(current_user));
    }

    // tg: code/game/machinery/computer/camera_advanced.dm:316
    [SignalHandler]
    public virtual void remove_circuit_action(datum _source = null, obj_item_circuit_component_equipment_action action_comp = null)
    {
        datum_action_innate_circuit_equipment_action action = action_comp.granted_to[REF(this)] as datum_action_innate_circuit_equipment_action;
        if (!(action is datum_action_innate_circuit_equipment_action))
            return;
        LAZYREMOVE(ref actions, action);
        QDEL_LIST_ASSOC_VAL(action_comp.granted_to);
    }

    // tg: code/game/machinery/computer/camera_advanced.dm:324
    [SignalHandler]
    public virtual void on_port_unregister_object(datum_component_usb_port source = null, atom_movable @object = null)
    {
        obj_item_integrated_circuit circuit = source.attached_circuit;
        foreach (obj_item_circuit_component_equipment_action action_comp in Dm.each<obj_item_circuit_component_equipment_action>(circuit.attached_components))
            remove_circuit_action(null, action_comp);
        UnregisterSignal(@object, Dm.list(COMSIG_CIRCUIT_ACTION_COMPONENT_REGISTERED, COMSIG_CIRCUIT_ACTION_COMPONENT_UNREGISTERED));
    }

    // tg: code/game/machinery/computer/camera_advanced.dm:331
    public virtual void unregister_usb_port(datum_component_usb_port port = null)
    {
        if (port.physical_object != null)
            on_port_unregister_object(port, port.physical_object);
        UnregisterSignal(port, Dm.list(COMSIG_USB_PORT_REGISTER_PHYSICAL_OBJECT, COMSIG_USB_PORT_UNREGISTER_PHYSICAL_OBJECT));
    }
}

/// Advanced camera component

// tg: code/game/machinery/computer/camera_advanced.dm:338
public partial class obj_item_circuit_component_advanced_camera : obj_item_circuit_component
{
    // tg: code/game/machinery/computer/camera_advanced.dm:348
    public override async DmTask populate_ports()
    {
        eye_x = add_output_port("X", PORT_TYPE_NUMBER) as datum_port_output;
        eye_y = add_output_port("Y", PORT_TYPE_NUMBER) as datum_port_output;
        eye_z = add_output_port("Z", PORT_TYPE_NUMBER) as datum_port_output;
    }

    // tg: code/game/machinery/computer/camera_advanced.dm:353
    public override void register_usb_parent(atom_movable shell = null)
    {
        base.register_usb_parent(shell);
        if (istype(shell, typeof(obj_machinery_computer_camera_advanced)))
        {
            attached_console = shell as obj_machinery_computer_camera_advanced;
            if (attached_console.eyeobj != null)
                register_eyeobj(attached_console.eyeobj);
            else
                RegisterSignal(attached_console, COMSIG_ADVANCED_CAMERA_EYE_CREATED, nameof(on_parent_eye_created));
        }
    }

    // tg: code/game/machinery/computer/camera_advanced.dm:362
    [SignalHandler]
    public virtual void on_parent_eye_created(datum _source = null, mob_eye_camera_remote eyeobj = null)
    {
        UnregisterSignal(attached_console, COMSIG_ADVANCED_CAMERA_EYE_CREATED);
        register_eyeobj(eyeobj);
    }

    // tg: code/game/machinery/computer/camera_advanced.dm:367
    public virtual void register_eyeobj(mob_eye_camera_remote eyeobj = null)
    {
        RegisterSignal(eyeobj, COMSIG_MOVABLE_MOVED, nameof(on_eyeobj_moved));
    }

    // tg: code/game/machinery/computer/camera_advanced.dm:370
    public override object unregister_usb_parent(atom_movable shell = null)
    {
        if (istype(shell, typeof(obj_machinery_computer_camera_advanced)))
        {
            UnregisterSignal(attached_console, COMSIG_ADVANCED_CAMERA_EYE_CREATED);
            if (attached_console.eyeobj != null)
                UnregisterSignal(attached_console.eyeobj, COMSIG_MOVABLE_MOVED);
            attached_console = null;
        }
        return base.unregister_usb_parent(shell);
    }

    // tg: code/game/machinery/computer/camera_advanced.dm:378
    [SignalHandler]
    public virtual void on_eyeobj_moved(atom_movable source = null)
    {
        turf eye_turf = get_turf(source);
        if (eye_turf == null)
            return;
        if (!SScameras.is_visible_by_cameras(eye_turf))
            return;
        eye_x.set_output(source.x);
        eye_y.set_output(source.y);
        eye_z.set_output(source.z);
    }
}

/// Advanced camera target intercept component

// tg: code/game/machinery/computer/camera_advanced.dm:391
public partial class obj_item_circuit_component_advanced_camera_intercept : obj_item_circuit_component
{
    // tg: code/game/machinery/computer/camera_advanced.dm:408
    public override async DmTask populate_ports()
    {
        await base.populate_ports();
        enabled = add_input_port("Enabled", PORT_TYPE_NUMBER) as datum_port_input;

        target_x = add_output_port("X", PORT_TYPE_NUMBER) as datum_port_output;
        target_y = add_output_port("Y", PORT_TYPE_NUMBER) as datum_port_output;
        target_z = add_output_port("Z", PORT_TYPE_NUMBER) as datum_port_output;

        target_port = add_output_port("Target", PORT_TYPE_ATOM) as datum_port_output;

        primary_click = add_output_port("Primary", PORT_TYPE_SIGNAL) as datum_port_output;
        secondary_click = add_output_port("Secondary", PORT_TYPE_SIGNAL) as datum_port_output;
    }

    // tg: code/game/machinery/computer/camera_advanced.dm:421
    public override void register_usb_parent(atom_movable shell = null)
    {
        base.register_usb_parent(shell);
        if (istype(shell, typeof(obj_machinery_computer_camera_advanced)))
        {
            attached_console = shell as obj_machinery_computer_camera_advanced;
            if (attached_console.eyeobj != null)
                register_eyeobj(attached_console.eyeobj);
            else
                RegisterSignal(attached_console, COMSIG_ADVANCED_CAMERA_EYE_CREATED, nameof(on_parent_eye_created));
        }
    }

    // tg: code/game/machinery/computer/camera_advanced.dm:430
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        if (port != enabled)
            return null;
        if (Dm.truthy(enabled.value))
        {
            if (attached_console.current_user != null)
                attached_console.current_user.click_intercept = this;
        }
        else
        {
            if (attached_console.current_user != null)
                attached_console.current_user.click_intercept = null;
        }
        return null;
    }

    // tg: code/game/machinery/computer/camera_advanced.dm:438
    [SignalHandler]
    public virtual void on_parent_eye_created(datum _source = null, mob_eye_camera_remote eyeobj = null)
    {
        UnregisterSignal(attached_console, COMSIG_ADVANCED_CAMERA_EYE_CREATED);
        register_eyeobj(eyeobj);
    }

    // tg: code/game/machinery/computer/camera_advanced.dm:443
    public virtual void register_eyeobj(mob_eye_camera_remote eyeobj = null)
    {
        RegisterSignal(eyeobj, COMSIG_REMOTE_CAMERA_ASSIGN_USER, nameof(on_parent_assign_user));
        if (Dm.truthy(enabled.value))
        {
            if (attached_console.current_user != null)
                attached_console.current_user.click_intercept = this;
        }
    }

    // tg: code/game/machinery/computer/camera_advanced.dm:448
    public override object unregister_usb_parent(atom_movable shell = null)
    {
        if (istype(shell, typeof(obj_machinery_computer_camera_advanced)))
        {
            if (attached_console.current_user != null)
                attached_console.current_user.click_intercept = null;
            if (attached_console.eyeobj != null)
                UnregisterSignal(attached_console.eyeobj, COMSIG_REMOTE_CAMERA_ASSIGN_USER);
            UnregisterSignal(attached_console, COMSIG_ADVANCED_CAMERA_EYE_CREATED);
            attached_console = null;
        }
        return base.unregister_usb_parent(shell);
    }

    // tg: code/game/machinery/computer/camera_advanced.dm:457
    [SignalHandler]
    public virtual void on_parent_assign_user(datum _source = null, mob_living new_user = null, mob_living old_user = null)
    {
        if (old_user != null)
            old_user.click_intercept = null;
        if (Dm.truthy(enabled.value))
        {
            if (new_user != null)
                new_user.click_intercept = this;
        }
    }

    // tg: code/game/machinery/computer/camera_advanced.dm:463
    public virtual object InterceptClickOn(mob user = null, object @params = null, atom target = null)
    {
        DmList modifiers = params2list(@params);
        if (Dm.truthy(LAZYACCESS(modifiers, SHIFT_CLICK)))
            return null;
        turf target_turf = get_turf(target);
        if (target_turf == null)
            return null;
        if (!SScameras.is_visible_by_cameras(target_turf))
            return null;
        if (Dm.truthy(TIMER_COOLDOWN_RUNNING(parent.shell, COOLDOWN_CIRCUIT_TARGET_INTERCEPT)))
            return null;
        target_x.set_output(target.x);
        target_y.set_output(target.y);
        target_z.set_output(target.z);

        target_port.set_output(target);

        if (Dm.truthy(LAZYACCESS(modifiers, RIGHT_CLICK)))
            secondary_click.set_output(COMPONENT_SIGNAL);
        else
            primary_click.set_output(COMPONENT_SIGNAL);
        if (parent.shell != null)
        {
            // TIMER_COOLDOWN_START (__DEFINES/cooldowns.dm:77, it expands addtimer: inline, rules 11.1 (f))
            atom_movable cd_source = parent.shell;
            LAZYSET(ref cd_source.cooldowns, COOLDOWN_CIRCUIT_TARGET_INTERCEPT, _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.end_cooldown), cd_source, COOLDOWN_CIRCUIT_TARGET_INTERCEPT), 1 * SECONDS, file: "code/game/machinery/computer/camera_advanced.dm", line: 485));
        }
        return TRUE;
    }
}
