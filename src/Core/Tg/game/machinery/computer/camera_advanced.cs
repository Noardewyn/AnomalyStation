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
// bridge): the console, its camera eye and its action buttons. Not translated: the circuits' USB port (wiremod: the console's
// add_usb_port component and its callbacks are live-partial stubs, docs/ai/specs/live-partial.txt), the AI's and a cyborg's clicks
// (silicons), the abductor's and the human AI's consoles and the circuit components (their own types, inert). The camera chunks a
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
            // AddComponent(/datum/component/usb_port, typecacheof(advanced_camera, advanced_camera_intercept), register_usb_port, unregister_usb_port)
            Dm.Deferred("code/game/machinery/computer/camera_advanced.dm:57", "Initialize: the circuits' USB port (wiremod, not ported)");
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
