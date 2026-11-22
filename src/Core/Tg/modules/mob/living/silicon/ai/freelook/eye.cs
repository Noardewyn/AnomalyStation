// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/silicon/ai/freelook/eye.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI4 (D326): the AI's eye, whole: the eye mob, the AI detector's HUD, clicking to move the eye, the AI's eye movement with its
// acceleration, its core view and the eye's creation. The client looks from the eye as from any camera eye (DV382). The var blocks
// are generated (D111).

// tg: code/modules/mob/living/silicon/ai/freelook/eye.dm:3
public partial class mob_eye_camera_ai : mob_eye_camera
{
    // tg: code/modules/mob/living/silicon/ai/freelook/eye.dm:15
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        update_ai_detect_hud();
        return dot;
    }

    // tg: code/modules/mob/living/silicon/ai/freelook/eye.dm:19
    public override object Destroy(float? force = null)
    {
        if (ai != null)
        {
            Dm.remove(ref ai.all_eyes, this);
            ai = null;
        }
        if (Dm.truthy(ai_detector_visible))
        {
            datum_atom_hud_ai_detector hud = GLOB.huds[DATA_HUD_AI_DETECT] as datum_atom_hud_ai_detector;
            hud.remove_atom_from_hud(this);
            DmList L = hud_list?[AI_DETECT_HUD] as DmList;
            QDEL_LIST(L);
        }
        return base.Destroy(force);
    }

    /**
     * Returns a list of turfs visible to the client's viewsize. \
     * Note that this will return an empty list if the camera's loc is not a turf.
     */
    // tg: code/modules/mob/living/silicon/ai/freelook/eye.dm:34
    public virtual DmList get_visible_turfs()
    {
        if (!isturf(loc))
            return Dm.list();
        client C = GetViewerClient();
        DmList view = C != null ? getviewsize(C.view) : getviewsize(Dm.world.view);
        turf lowerleft = locate(max(1, x - (Dm.num(view[1]) - 1) / 2), max(1, y - (Dm.num(view[2]) - 1) / 2), z);
        turf upperright = locate(min(Dm.world.maxx, lowerleft.x + (Dm.num(view[1]) - 1)), min(Dm.world.maxy, lowerleft.y + (Dm.num(view[2]) - 1)), lowerleft.z);
        return block(lowerleft, upperright);
    }

    // tg: code/modules/mob/living/silicon/ai/freelook/eye.dm:47
    public virtual void update_ai_detect_hud()
    {
        datum_atom_hud_ai_detector hud = GLOB.huds[DATA_HUD_AI_DETECT] as datum_atom_hud_ai_detector;
        DmList old_images = hud_list?[AI_DETECT_HUD] as DmList ?? Dm.list();
        if (!Dm.truthy(ai_detector_visible))
        {
            hud.remove_atom_from_hud(this);
            QDEL_LIST(old_images);
            return;
        }

        if (!Dm.truthy(length(hud.hud_users_all_z_levels)))
            return; //no one is watching, do not bother updating anything

        hud.remove_atom_from_hud(this);

        DmList vis_contents_opaque = TgState.Current.mob_eye_camera_ai__vis_contents_opaque ??= Dm.list(); // var/static/list/vis_contents_opaque
        turf our_turf = get_turf(this);
        float our_z_offset = Dm.num(GET_TURF_PLANE_OFFSET(our_turf));
        string key = Dm.text("[]-[]", our_z_offset, ai_detector_color);
        obj_effect_overlay_ai_detect_hud hud_obj = vis_contents_opaque[key] as obj_effect_overlay_ai_detect_hud;
        if (hud_obj == null)
        {
            hud_obj = Dm.@new<obj_effect_overlay_ai_detect_hud>();
            hud_obj.plane = GET_NEW_PLANE(Dm.num(PLANE_TO_TRUE(hud_obj.plane)), our_z_offset); // SET_PLANE_W_SCALAR
            hud_obj.color = ai_detector_color;
            vis_contents_opaque[key] = hud_obj;
        }

        DmList new_images = Dm.list();
        DmList turfs = get_visible_turfs();
        foreach (turf seen_turf in Dm.each<turf>(turfs))
        {
            image img = (old_images.len > new_images.len) ? old_images[new_images.len + 1] as image : Dm.image(loc: seen_turf, layer: ABOVE_ALL_MOB_LAYER);
            Dm.append(ref img.vis_contents, hud_obj);
            SET_PLANE(img, GAME_PLANE, seen_turf);
            Dm.append(ref new_images, img);
        }
        for (float i = new_images.len + 1; i <= old_images.len; i++)
            qdel(old_images[i]);

        active_hud_list ??= Dm.list();
        active_hud_list[AI_DETECT_HUD] = new_images;
        hud.add_atom_to_hud(this);
    }

    // tg: code/modules/mob/living/silicon/ai/freelook/eye.dm:85
    public override void setLoc(object destination = null, float? force_update = null)
    {
        if (ai == null)
            return;
        if (!isturf(ai.loc))
            return;
        base.setLoc(destination, force_update);
        if (ai.client != null && !Dm.truthy(ai.multicam_on))
            Waiting.NoSleep(ai.client.set_eye(this));
        update_ai_detect_hud();
        if (ai.current is obj_machinery_holopad H)
        {
            if (!Waiting.NoSleep(H.move_hologram(ai, destination as turf)))
                H.clear_holo(ai);
        }
        if (Dm.truthy(ai.camera_light_on))
            ai.light_cameras();
        if (ai.master_multicam != null)
            ai.master_multicam.refresh_view();
    }

    // tg: code/modules/mob/living/silicon/ai/freelook/eye.dm:107
    public override void update_visibility()
    {
        if (ai != null)
            ai.camera_visibility(this);
        else
            base.update_visibility();
    }

    // tg: code/modules/mob/living/silicon/ai/freelook/eye.dm:113
    public override client GetViewerClient()
    {
        if (ai != null)
            return ai.client;
        return null;
    }

    // tg: code/modules/mob/living/silicon/ai/freelook/eye.dm:118
    public override async DmTask<object> examine(mob user = null, params object[] __extra) //Displays a silicon's laws to ghosts
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (ai != null && ai.laws != null && isobserver(user))
        {
            Dm.append(ref dot, Dm.text("<b>[] has the following laws:</b>", ai));
            foreach (object law in Dm.each(ai.laws.get_law_list(include_zeroth: TRUE)))
                Dm.append(ref dot, law);
        }
        return dot;
    }

    // tg: code/modules/mob/living/silicon/ai/freelook/eye.dm:125
    public override async DmTask on_changed_z_level(turf old_turf = null, turf new_turf = null, object same_z_layer = null, float? notify_contents = null)
    {
        await base.on_changed_z_level(old_turf, new_turf, same_z_layer, notify_contents);
        if (Dm.truthy(same_z_layer))
            return;
        update_ai_detect_hud();
        ai?.on_looking_z_level_change(old_turf, new_turf);
    }

    // tg: code/modules/mob/living/silicon/ai/freelook/eye.dm:221
    public override async DmTask<object> Hear(atom_movable speaker = null, object message_language = null, object raw_message = null, object radio_freq = null, object radio_freq_name = null, object radio_freq_color = null, DmList spans = null, DmList message_mods = null, float? message_range = null)
    {
        object dot = await base.Hear(speaker, message_language, raw_message, radio_freq, radio_freq_name, radio_freq_color, spans, message_mods, message_range);
        if (Dm.truthy(relay_speech) && speaker != null && ai != null && !Dm.truthy(radio_freq) && speaker != ai && SScameras.is_visible_by_cameras(speaker))
            await ai.relay_speech(speaker, message_language as datum_language, raw_message, radio_freq, spans, message_mods ?? Dm.list());
        return dot;
    }
}

/*----------------------------------------------------*/

// tg: code/modules/mob/living/silicon/ai/freelook/eye.dm:134
public partial class atom : datum
{
    public virtual void move_camera_by_click()
    {
        if (!isAI(usr))
            return;
        mob_living_silicon_ai AI = usr as mob_living_silicon_ai;
        if (AI.eyeobj != null && (Dm.truthy(AI.multicam_on) || AI.client.eye == AI.eyeobj) && Dm.eq(AI.eyeobj.z, z))
        {
            AI.ai_tracking_tool.reset_tracking();
            if (isturf(loc) || isturf(this))
                AI.eyeobj.setLoc(this);
        }
    }
}

// tg: code/modules/mob/living/silicon/ai/freelook/eye.dm:145
public partial class mob_living_silicon_ai : mob_living_silicon
{
    private const float SPRINT_PER_TICK = 0.5f;
    private const float MAX_SPRINT = 50;
    private const float SPRINT_PER_STEP = 20;

    // tg: code/modules/mob/living/silicon/ai/freelook/eye.dm:149
    public virtual void AIMove(object direction = null)
    {
        float initial_sprint = Dm.initial(GetType(), (mob_living_silicon_ai a) => a.sprint);
        if (Dm.truthy(last_moved) && last_moved + 1 < Dm.world.timeofday)
        {
            float missed_sprint = max((Dm.world.timeofday + 1) - last_moved, 0) * SPRINT_PER_TICK;
            sprint = max(sprint - missed_sprint * 7, initial_sprint);
        }

        float step_count = 0;
        for (float i = 0; i < max(sprint, initial_sprint); i += SPRINT_PER_STEP)
        {
            step_count += 1;
            turf step = get_turf(get_step(eyeobj, Dm.num(direction)));
            if (step != null)
                eyeobj.setLoc(step);
        }

        eyeobj.glide_size = ICON_SIZE_ALL;
        last_moved = Dm.world.timeofday;

        if (Dm.truthy(acceleration))
            sprint = min(sprint + SPRINT_PER_TICK, MAX_SPRINT);
        else
            sprint = initial_sprint;

        ai_tracking_tool.reset_tracking();
    }

    /**
     * DEVIATION(DV423): AIMove for the eye's free flight (LocomotionSeam.AiEyes.cs): tg's sprint, glide and tracking lines, minus the
     * setLoc steps; returns how many tiles tg would step the eye this move, which the flight takes as its speed's multiple.
     */
    // tg: code/modules/mob/living/silicon/ai/freelook/eye.dm:149
    public virtual float AIMove_capsule()
    {
        float initial_sprint = Dm.initial(GetType(), (mob_living_silicon_ai a) => a.sprint);
        if (Dm.truthy(last_moved) && last_moved + 1 < Dm.world.timeofday)
        {
            float missed_sprint = max((Dm.world.timeofday + 1) - last_moved, 0) * SPRINT_PER_TICK;
            sprint = max(sprint - missed_sprint * 7, initial_sprint);
        }

        float step_count = 0;
        for (float i = 0; i < max(sprint, initial_sprint); i += SPRINT_PER_STEP)
            step_count += 1;

        eyeobj.glide_size = ICON_SIZE_ALL;
        last_moved = Dm.world.timeofday;

        if (Dm.truthy(acceleration))
            sprint = min(sprint + SPRINT_PER_TICK, MAX_SPRINT);
        else
            sprint = initial_sprint;

        ai_tracking_tool.reset_tracking();
        return step_count;
    }

    // tg: code/modules/mob/living/silicon/ai/freelook/eye.dm:178
    public virtual async DmTask view_core()
    {
        if (current is obj_machinery_holopad H)
            H.clear_holo(this);
        else
            current = null;
        ai_tracking_tool?.reset_tracking();

        if (isturf(loc) && (QDELETED(eyeobj) || eyeobj.loc == null))
        {
            to_chat(this, "ERROR: Eyeobj not found. Creating new eye...");
            stack_trace(Dm.text("AI eye object wasn't found! Location: [] / Eyeobj: [] / QDELETED: [] / Eye loc: []", loc, eyeobj, QDELETED(eyeobj) ? TRUE : FALSE, eyeobj?.loc), "code/modules/mob/living/silicon/ai/freelook/eye.dm", 188);
            QDEL_NULL(ref eyeobj);
            create_eye();
        }

        eyeobj?.setLoc(loc);
    }

    // tg: code/modules/mob/living/silicon/ai/freelook/eye.dm:194
    public virtual void create_eye()
    {
        if (eyeobj != null)
            return;
        eyeobj = Dm.@new<mob_eye_camera_ai>();
        Dm.append(ref all_eyes, eyeobj);
        eyeobj.ai = this;
        eyeobj.name = Dm.text("[] (AI Eye)", name);
        eyeobj.setLoc(loc, TRUE);
        set_eyeobj_visible(TRUE);
    }

    // tg: code/modules/mob/living/silicon/ai/freelook/eye.dm:204
    public virtual void set_eyeobj_visible(float? state = null)
    {
        state ??= TRUE;
        if (eyeobj == null)
            return;
        eyeobj.mouse_opacity = Dm.truthy(state) ? MOUSE_OPACITY_ICON : Dm.initial(eyeobj.GetType(), (atom a) => a.mouse_opacity);
        if (Dm.truthy(state))
            eyeobj.SetInvisibility(INVISIBILITY_OBSERVER, id: type);
        else
            eyeobj.RemoveInvisibility(type);
    }

    // tg: code/modules/mob/living/silicon/ai/freelook/eye.dm:213  GAME_VERB(/mob/living/silicon/ai, toggle_acceleration, "Toggle Camera Acceleration", "AI Commands"): the verb proc
    public virtual void toggle_acceleration(params object[] __extra)
    {
        Waiting.Detach(__gvb_toggle_acceleration(Dm.list()));
    }

    // tg: code/modules/mob/living/silicon/ai/freelook/eye.dm:213  the body
    public virtual async DmTask __gvb_toggle_acceleration(DmList structured_args = null)
    {
        if (Dm.truthy(incapacitated))
            return;
        acceleration = !Dm.truthy(acceleration) ? TRUE : FALSE;
        to_chat(usr, Dm.text("Camera acceleration has been toggled [].", Dm.truthy(acceleration) ? "on" : "off"));
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mob/living/silicon/ai/freelook/eye.dm:58  var/static/list/vis_contents_opaque
    internal DmList mob_eye_camera_ai__vis_contents_opaque;
}
