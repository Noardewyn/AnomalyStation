// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/devices/multitool.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7 slice T1 (D164): the multitool whole (its suicide_act: gaps DV2): the machine buffer
// (set_buffer and its signals), the APC locator with its arrow, examine. The syndicate campaign G1c (D316): the AI-detection
// multitool (/ai_detect, :153-325); silicons SI4 (D326): its static scan shows the camera chunks' unseen tiles. The abductor's and the cyborg's multitools are var-only subtypes.

// tg: code/game/objects/items/devices/multitool.dm:14
public partial class obj_item_multitool : obj_item
{
    // tg: code/game/objects/items/devices/multitool.dm:42
    public override object Destroy(float? force = null)
    {
        if (Dm.truthy(buffer))
            remove_buffer(buffer);
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/devices/multitool.dm:47
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("Its buffer []", Dm.truthy(buffer) ? I18n.T("contains [].", buffer) : I18n.T("is empty."))));
        return dot;
    }

    // tg: code/game/objects/items/devices/multitool.dm:51
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);

        if (Dm.truthy(dot) || !Dm.truthy(apc_scanner))
            return dot;

        await scan_apc(user);
        return dot;
    }

    // tg: code/game/objects/items/devices/multitool.dm:59
    public override async DmTask<object> attack_self_secondary(mob user = null, object modifiers = null)
    {
        object dot = await base.attack_self_secondary(user, modifiers);

        if (Dm.truthy(dot) || !Dm.truthy(apc_scanner))
            return dot;

        await scan_apc(user);
        return dot;
    }

    // tg: code/game/objects/items/devices/multitool.dm:67
    public virtual async DmTask scan_apc(mob user = null)
    {
        // COOLDOWN_FINISHED(src, next_apc_scan) (__DEFINES/cooldowns.dm:113), inline (rules 11.1 (a))
        if (!(next_apc_scan <= world.time))
            return;

        // COOLDOWN_START(src, next_apc_scan, 2 SECONDS) (__DEFINES/cooldowns.dm:111), inline
        next_apc_scan = world.time + 2 * SECONDS;

        area local_area = get_area(this) as area;
        obj_machinery_power_apc power_controller = local_area.apc;
        if (!Dm.truthy(power_controller))
        {
            user.balloon_alert(user, I18n.T("couldn't find apc!"));
            return;
        }

        float dist = get_dist(this, power_controller);
        float dir = get_dir(user, power_controller);
        object balloon_message = null;
        string arrow_color;

        if (dist == 0)
        {
            user.balloon_alert(user, I18n.T("found apc!"));
            return;
        }
        else if (dist >= 1 && dist <= 5)
            arrow_color = COLOR_GREEN;
        else if (dist >= 6 && dist <= 10)
            arrow_color = COLOR_YELLOW;
        else if (dist >= 11 && dist <= 15)
            arrow_color = COLOR_ORANGE;
        else
            arrow_color = COLOR_RED;

        user.balloon_alert(user, balloon_message);

        datum_hud user_hud = user.hud_used;
        if (!Dm.truthy(user_hud))
            return;

        atom_movable_screen_multitool_arrow arrow = await user_hud.add_screen_object(typeof(atom_movable_screen_multitool_arrow), HUD_MULTITOOL_ARROW, HUD_GROUP_INFO, update_screen: TRUE) as atom_movable_screen_multitool_arrow;
        arrow.color = arrow_color;
        arrow.transform = Dm.matrix(Dm.num(dir2angle(dir)), MATRIX_ROTATE);
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), 1.5f * SECONDS > GC_FILTER_QUEUE ? (object)WEAKREF(arrow) : arrow), 1.5f * SECONDS, file: "code/game/objects/items/devices/multitool.dm", line: 117); // QDEL_IN(arrow, 1.5 SECONDS), inline (rules 11.1 (f))
    }

    // tg: code/game/objects/items/devices/multitool.dm:121  (gaps DV2)
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] puts the [] to [] chest. It looks like [] trying to pulse [] heart off!", user, this, user.p_their(), user.p_theyre(), user.p_their())));
        return OXYLOSS;//there's a reason it wasn't recommended by doctors
    }

    /**
     * Sets the multitool internal object buffer
     *
     * Arguments:
     * * buffer - the new object to assign to the multitool's buffer
     */
    // tg: code/game/objects/items/devices/multitool.dm:131
    public virtual void set_buffer(datum buffer = null)
    {
        if (Dm.truthy(this.buffer))
        {
            UnregisterSignal(this.buffer, COMSIG_QDELETING);
            remove_buffer(this.buffer);
        }
        this.buffer = buffer;
        if (!QDELETED(buffer))
            RegisterSignal(buffer, COMSIG_QDELETING, nameof(remove_buffer));
    }

    /**
     * Called when the buffer's stored object is deleted
     *
     * This proc does not clear the buffer of the multitool, it is here to
     * handle the deletion of the object the buffer references
     */
    // tg: code/game/objects/items/devices/multitool.dm:145
    [SignalHandler]
    public virtual void remove_buffer(datum source = null)
    {
        if (Dm.truthy(this._listen_lookup?[COMSIG_MULTITOOL_REMOVE_BUFFER]))
            this._SendSignal(COMSIG_MULTITOOL_REMOVE_BUFFER, Dm.list(this, source));
        buffer = null;
    }
}

// tg: code/game/objects/items/devices/multitool.dm:109
public partial class atom_movable_screen_multitool_arrow : atom_movable_screen
{
    // tg: code/game/objects/items/devices/multitool.dm:115
    public override object Destroy(float? force = null)
    {
        datum_hud our_hud = hud;
        object dot = base.Destroy(force);
        if (!QDELETED(our_hud))
            Waiting.Detach(our_hud.show_hud(our_hud.hud_version));
        return dot;
    }
}

// Syndicate device disguised as a multitool; it will turn red when an AI camera is nearby.
// tg: code/game/objects/items/devices/multitool.dm:153
public partial class obj_item_multitool_ai_detect : obj_item_multitool
{
    // tg: code/game/objects/items/devices/multitool.dm:1-3  #define PROXIMITY_NONE "", PROXIMITY_ON_SCREEN "_red", PROXIMITY_NEAR "_yellow" (file-local)
    private const string PROXIMITY_NONE = "";
    private const string PROXIMITY_ON_SCREEN = "_red";
    private const string PROXIMITY_NEAR = "_yellow";

    // tg: code/game/objects/items/devices/multitool.dm:175
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (!Dm.truthy(hud_on))
            return dot;
        Dm.append(dot, span_notice(I18n.T("You can right-click to scan for nearby unseen spots. They will be shown for exactly 8 seconds due to battery limitations.")));
        switch (detect_state)
        {
            case PROXIMITY_NONE:
                Dm.append(dot, span_green(I18n.T("No AI should be currently looking at you. Keep on your clandestine activities.")));
                break;
            case PROXIMITY_NEAR:
                Dm.append(dot, span_warning(I18n.T("An AI is getting uncomfortably close. Maybe time to drop what youre doing.")));
                break;
            case PROXIMITY_ON_SCREEN:
                Dm.append(dot, span_danger(I18n.T("An AI is (probably) looking at you. You should probably hide this.")));
                break;
        }
        return dot;
    }

    // tg: code/game/objects/items/devices/multitool.dm:188
    public override object Destroy(float? force = null)
    {
        if (Dm.truthy(hud_on) && loc is mob holder)
            remove_hud(holder);
        cleanup_static();
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/devices/multitool.dm:194
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        toggle_hud(user);
        return dot;
    }

    // tg: code/game/objects/items/devices/multitool.dm:200
    public override async DmTask<object> attack_self_secondary(mob user = null, object modifiers = null)
    {
        object dot = await base.attack_self_secondary(user, modifiers);
        if (Dm.truthy(dot))
            return dot;
        scan_unseen(user);
        return dot;
    }

    // tg: code/game/objects/items/devices/multitool.dm:206
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        if (Dm.truthy(hud_on))
            show_hud(user);
    }

    // tg: code/game/objects/items/devices/multitool.dm:211
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = await base.dropped(user, silent, __extra);
        if (Dm.truthy(hud_on))
            remove_hud(user);
        cleanup_static();
        return dot;
    }

    // tg: code/game/objects/items/devices/multitool.dm:217
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.text("[][]", Dm.initial(this, (atom p) => p.icon_state), detect_state);
        return dot;
    }

    // tg: code/game/objects/items/devices/multitool.dm:221
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        string old_detect_state = detect_state;
        multitool_detect();
        if (detect_state != old_detect_state)
            update_appearance();
        return null;
    }

    // tg: code/game/objects/items/devices/multitool.dm:227
    public virtual void toggle_hud(mob user = null)
    {
        hud_on = Dm.truthy(hud_on) ? FALSE : TRUE;
        if (user != null)
            to_chat(user, span_notice(I18n.T("You toggle the ai detection feature on [] [].", this, Dm.truthy(hud_on) ? "on" : "off")));
        if (Dm.truthy(hud_on))
        {
            START_PROCESSING(SSfastprocess, this);
            show_hud(user);
        }
        else
        {
            STOP_PROCESSING(SSfastprocess, this);
            detect_state = PROXIMITY_NONE;
            update_appearance(UPDATE_ICON);
            remove_hud(user);
        }
    }

    // tg: code/game/objects/items/devices/multitool.dm:240
    public virtual void show_hud(mob user = null)
    {
        datum_atom_hud hud = GLOB.huds[DATA_HUD_AI_DETECT] as datum_atom_hud;
        hud.show_to(user);
    }

    // tg: code/game/objects/items/devices/multitool.dm:244
    public virtual void remove_hud(mob user = null)
    {
        datum_atom_hud hud = GLOB.huds[DATA_HUD_AI_DETECT] as datum_atom_hud;
        hud.hide_from(user);
    }

    // tg: code/game/objects/items/devices/multitool.dm:248
    public virtual void multitool_detect()
    {
        turf our_turf = get_turf(this);
        detect_state = PROXIMITY_NONE;

        foreach (mob_eye_camera_ai AI_eye in Dm.each<mob_eye_camera_ai>(GLOB.camera_eyes))
        {
            if (!Dm.truthy(AI_eye.ai_detector_visible))
                continue;

            turf ai_turf = get_turf(AI_eye);
            float distance = Dm.num(get_dist(our_turf, ai_turf));

            if (distance == -1) //get_dist() returns -1 for distances greater than 127 (and for errors, so assume -1 is just max range)
            {
                if (ai_turf == our_turf)
                {
                    detect_state = PROXIMITY_ON_SCREEN;
                    break;
                }
                continue;
            }

            if (distance < rangealert) //ai should be able to see us
            {
                detect_state = PROXIMITY_ON_SCREEN;
                break;
            }
            if (distance < rangewarning) //ai can't see us but is close
                detect_state = PROXIMITY_NEAR;
        }
    }

    // tg: code/game/objects/items/devices/multitool.dm:271
    public virtual void scan_unseen(mob user = null)
    {
        if (user?.client == null) // the monkey incident of 2564
            return;
        if (static_scan_cd > Dm.world.time) // COOLDOWN_FINISHED, inline
        {
            balloon_alert(user, "recharging!");
            return;
        }
        cleanup_static();
        turf our_turf = get_turf(this);
        DmList chunks = surrounding_chunks(our_turf);

        if (hud_obj == null)
        {
            hud_obj = Dm.@new<obj_effect_overlay_ai_detect_hud_camera_unseen>();
            // SET_PLANE_W_SCALAR(hud_obj, PLANE_TO_TRUE(hud_obj.plane), GET_TURF_PLANE_OFFSET(our_turf)): one z level's planes, no offset
        }

        DmList new_images = Dm.list();
        foreach (datum_camerachunk chunk in Dm.each<datum_camerachunk>(chunks))
        {
            foreach (turf seen_turf in Dm.each<turf>(chunk.obscuredTurfs))
            {
                image img = image(loc: seen_turf, layer: ABOVE_ALL_MOB_LAYER);
                img.vis_contents.Add(hud_obj);
                SET_PLANE(img, GAME_PLANE, seen_turf);
                Dm.append(new_images, img);
            }
        }
        Dm.combine(user.client.images, new_images);
        static_viewer = WEAKREF(user.client) as datum_weakref;
        balloon_alert(user, "nearby unseen spots shown");
        static_disappear_timer = _addtimer(Dm.@new<datum_callback>(this, nameof(cleanup_static)), 8 * SECONDS, TIMER_STOPPABLE, file: "code/game/objects/items/devices/multitool.dm", line: 295);
        static_scan_cd = Dm.world.time + 4 * SECONDS; // COOLDOWN_START, inline
    }

    // copied from camera chunks but we are doing a really big edge case here though
    // tg: code/game/objects/items/devices/multitool.dm:299
    public virtual DmList surrounding_chunks(turf epicenter = null)
    {
        DmList dot = Dm.list();
        float static_range = Dm.initial(typeof(mob_eye_camera_ai), (mob_eye_camera p) => p.static_visibility_range);
        float x1 = Math.Max(1, epicenter.x - static_range);
        float y1 = Math.Max(1, epicenter.y - static_range);
        float x2 = Math.Min(Dm.world.maxx, epicenter.x + static_range);
        float y2 = Math.Min(Dm.world.maxy, epicenter.y + static_range);

        for (float x = x1; x <= x2; x += CHUNK_SIZE)
        {
            for (float y = y1; y <= y2; y += CHUNK_SIZE)
            {
                datum_camerachunk chunk = SScameras.generate_chunk(x, y, epicenter.z);
                // removing cameras in build mode didnt affect it and i guess it needs an AI eye to update so we have to do this manually
                // unless we only want to see static in a jank manner only if an eye updates it
                chunk?.force_update(only_if_necessary: FALSE); // UPDATE THE FUCK NOW
                Dm.combine(ref dot, chunk);
            }
        }
        return dot;
    }

    // tg: code/game/objects/items/devices/multitool.dm:315
    public virtual void cleanup_static()
    {
        if (hud_obj == null) //we never did anything
            return;
        client viewer = static_viewer?.resolve() as client;
        if (viewer != null)
            Dm.remove(viewer.images, static_images);
        static_images.Cut();
        qdel(hud_obj); // QDEL_NULL
        hud_obj = null;
        viewer = null;
        deltimer(static_disappear_timer);
        static_disappear_timer = null;
    }
}
