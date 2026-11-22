// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/silicon/ai/multicam.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (silicons SI4, D326): the AI's multicamera mode. The AI looks at CentCom's camera room while its picture-in-picture
// windows (picture_in_picture.cs) show what its secondary eyes see; the 3D client draws each window as a floating second 3D view. The
// var blocks are generated (D111).

//Picture in picture

// tg: code/modules/mob/living/silicon/ai/multicam.dm:3
public partial class atom_movable_screen_movable_pic_in_pic_ai : atom_movable_screen_movable_pic_in_pic
{
    // tg: code/modules/mob/living/silicon/ai/multicam.dm:9
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        aiEye = Dm.@new<mob_eye_camera_ai_pic_in_pic>();
        aiEye.screen = this;
        return dot;
    }

    // tg: code/modules/mob/living/silicon/ai/multicam.dm:14
    public override object Destroy(float? force = null)
    {
        set_ai(null);
        QDEL_NULL(ref aiEye);
        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/silicon/ai/multicam.dm:19
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        await base.Click(location, control, @params);
        if (ai != null)
            ai.select_main_multicam_window(this);
        return null;
    }

    // tg: code/modules/mob/living/silicon/ai/multicam.dm:24
    public override void make_backgrounds()
    {
        base.make_backgrounds();
        highlighted_background = Dm.@new<mutable_appearance>();
        highlighted_background.icon = Dm.resource("icons/hud/pic_in_pic.dmi");
        highlighted_background.icon_state = "background_highlight";
        highlighted_background.layer = LOWER_FLOOR_LAYER;
        highlighted_background.appearance_flags = PIXEL_SCALE;
    }

    // tg: code/modules/mob/living/silicon/ai/multicam.dm:32
    public override void add_background()
    {
        if (width > 0 && height > 0)
        {
            matrix M = matrix();
            M.Scale(width + 0.5f, height + 0.5f);
            M.Translate((width - 1) / 2 * ICON_SIZE_X, (height - 1) / 2 * ICON_SIZE_Y);
            highlighted_background.transform = M;
            standard_background.transform = M;
            add_overlay(Dm.truthy(highlighted) ? highlighted_background : standard_background);
        }
    }

    // tg: code/modules/mob/living/silicon/ai/multicam.dm:41
    public override void set_view_size(float width = 0, float height = 0, float? do_refresh = null)
    {
        do_refresh ??= TRUE;
        aiEye.static_visibility_range = round(max(width, height) / 2) + 1;
        if (ai != null)
            ai.camera_visibility(aiEye);
        base.set_view_size(width, height, do_refresh);
    }

    // tg: code/modules/mob/living/silicon/ai/multicam.dm:47
    public override void set_view_center(atom target = null, float? do_refresh = null)
    {
        do_refresh ??= TRUE;
        base.set_view_center(target, do_refresh);
        aiEye.setLoc(get_turf(target));
    }

    // tg: code/modules/mob/living/silicon/ai/multicam.dm:51
    public override void refresh_view()
    {
        base.refresh_view();
        aiEye.setLoc(get_turf(center));
    }

    // tg: code/modules/mob/living/silicon/ai/multicam.dm:55
    public virtual void highlight()
    {
        if (Dm.truthy(highlighted))
            return;
        highlighted = TRUE;
        cut_overlay(standard_background);
        add_overlay(highlighted_background);
    }

    // tg: code/modules/mob/living/silicon/ai/multicam.dm:62
    public virtual void unhighlight()
    {
        if (!Dm.truthy(highlighted))
            return;
        highlighted = FALSE;
        cut_overlay(highlighted_background);
        add_overlay(standard_background);
    }

    // tg: code/modules/mob/living/silicon/ai/multicam.dm:69
    public virtual void set_ai(mob_living_silicon_ai new_ai = null)
    {
        if (ai != null)
        {
            Dm.remove(ref ai.multicam_screens, this);
            Dm.remove(ref ai.all_eyes, aiEye);
            if (ai.master_multicam == this)
                ai.master_multicam = null;
            if (Dm.truthy(ai.multicam_on))
                unshow_to(ai.client);
        }
        ai = new_ai;
        if (new_ai != null)
        {
            Dm.append(ref new_ai.multicam_screens, this);
            Dm.append(ref ai.all_eyes, aiEye);
            if (Dm.truthy(new_ai.multicam_on))
                show_to(new_ai.client);
        }
    }
}

//Turf, area, and landmark for the viewing room

// tg: code/modules/mob/living/silicon/ai/multicam.dm:85
public partial class turf_open_ai_visible : turf_open
{
    // tg: code/modules/mob/living/silicon/ai/multicam.dm:94
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        RegisterSignal(SSmapping, COMSIG_PLANE_OFFSET_INCREASE, nameof(multiz_offset_increase));
        return dot;
    }

    // tg: code/modules/mob/living/silicon/ai/multicam.dm:98
    public virtual void multiz_offset_increase(datum source = null)
    {
        plane = GET_NEW_PLANE(Dm.num(Dm.initial(GetType(), (atom a) => a.plane)), SSmapping.max_plane_offset); // SET_PLANE_W_SCALAR
    }
}

// tg: code/modules/mob/living/silicon/ai/multicam.dm:112
public partial class datum_controller_global_vars
{
    // tg: code/modules/mob/living/silicon/ai/multicam.dm:112  GLOBAL_DATUM(ai_camera_room_landmark, /obj/effect/landmark/ai_multicam_room)
    public obj_effect_landmark_ai_multicam_room ai_camera_room_landmark;

    // tg: code/modules/mob/living/silicon/ai/multicam.dm:112  (GLOBAL_UNMANAGED(ai_camera_room_landmark), __DEFINES/__globals.dm:11)
    public virtual void InitGlobalai_camera_room_landmark()
    {
        return;
    }
}

// tg: code/modules/mob/living/silicon/ai/multicam.dm:114
public partial class obj_effect_landmark_ai_multicam_room : obj_effect_landmark
{
    // tg: code/modules/mob/living/silicon/ai/multicam.dm:119
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        qdel(GLOB.ai_camera_room_landmark);
        GLOB.ai_camera_room_landmark = this;
        return dot;
    }

    // tg: code/modules/mob/living/silicon/ai/multicam.dm:124
    public override object Destroy(float? force = null)
    {
        if (GLOB.ai_camera_room_landmark == this)
            GLOB.ai_camera_room_landmark = null;
        return base.Destroy(force);
    }
}

//Dummy camera eyes

// tg: code/modules/mob/living/silicon/ai/multicam.dm:131
public partial class mob_eye_camera_ai_pic_in_pic : mob_eye_camera_ai
{
    // tg: code/modules/mob/living/silicon/ai/multicam.dm:143
    public override client GetViewerClient()
    {
        if (screen?.ai != null)
            return screen.ai.client;
        return null;
    }

    // tg: code/modules/mob/living/silicon/ai/multicam.dm:147
    public override void update_visibility()
    {
        if (screen?.ai != null)
            screen.ai.camera_visibility(this);
        else
            base.update_visibility();
    }

    // tg: code/modules/mob/living/silicon/ai/multicam.dm:153
    public override void setLoc(object destination = null, float? force_update = null)
    {
        if (Dm.truthy(destination))
            Waiting.NoSleep(abstract_move(destination as atom));
        else
            Waiting.NoSleep(moveToNullspace());
        update_visibility();
        update_camera_telegraphing();
        update_ai_detect_hud();
    }

    // tg: code/modules/mob/living/silicon/ai/multicam.dm:162
    public override DmList get_visible_turfs()
    {
        //we do our own thing here
        return screen != null ? screen.get_visible_turfs() : Dm.list();
    }

    // tg: code/modules/mob/living/silicon/ai/multicam.dm:166
    public virtual void update_camera_telegraphing()
    {
        if (!Dm.truthy(telegraph_cameras))
            return;
        DmList add;
        DmList remove;
        DmList visible = Dm.list();
        foreach (datum_camerachunk chunk in Dm.each<datum_camerachunk>(visibleCameraChunks))
        {
            foreach (object z_key in Dm.each(chunk.cameras))
            {
                foreach (obj_machinery_camera camera in Dm.each<obj_machinery_camera>(chunk.cameras[z_key]))
                {
                    if (!Dm.truthy(camera.can_use()) || get_dist(camera, this) > telegraph_range)
                        continue;
                    Dm.combine(ref visible, camera);
                }
            }
        }

        add = Dm.sub(visible, cameras_telegraphed) as DmList;
        remove = Dm.sub(cameras_telegraphed, visible) as DmList;

        foreach (obj_machinery_camera C in Dm.each<obj_machinery_camera>(remove))
        {
            if (QDELETED(C))
                continue;
            Dm.remove(ref cameras_telegraphed, C);
            C.in_use_lights--;
            C.update_appearance();
        }
        foreach (obj_machinery_camera C in Dm.each<obj_machinery_camera>(add))
        {
            if (QDELETED(C))
                continue;
            Dm.combine(ref cameras_telegraphed, C);
            C.in_use_lights++;
            C.update_appearance();
        }
    }

    // tg: code/modules/mob/living/silicon/ai/multicam.dm:197
    public virtual void disable_camera_telegraphing()
    {
        telegraph_cameras = FALSE;
        foreach (obj_machinery_camera C in Dm.each<obj_machinery_camera>(cameras_telegraphed))
        {
            if (QDELETED(C))
                continue;
            C.in_use_lights--;
            C.update_appearance();
        }
        cameras_telegraphed.Cut();
    }

    // tg: code/modules/mob/living/silicon/ai/multicam.dm:207
    public override object Destroy(float? force = null)
    {
        disable_camera_telegraphing();
        return base.Destroy(force);
    }
}

//AI procs

// tg: code/modules/mob/living/silicon/ai/multicam.dm:213
public partial class mob_living_silicon_ai : mob_living_silicon
{
    // tg: code/modules/mob/living/silicon/ai/multicam.dm:213
    public virtual atom_movable_screen_movable_pic_in_pic_ai drop_new_multicam(float? silent = null)
    {
        silent ??= FALSE;
        if (!Dm.truthy(config.Get(typeof(datum_config_entry_flag_allow_ai_multicam)))) /* CONFIG_GET(flag/allow_ai_multicam) */
        {
            if (!Dm.truthy(silent))
                to_chat(this, span_warning(I18n.T("This action is currently disabled. Contact an administrator to enable this feature.")));
            return null;
        }
        if (eyeobj == null)
            return null;
        if (multicam_screens.len >= max_multicams)
        {
            if (!Dm.truthy(silent))
                to_chat(this, span_warning(I18n.T("Cannot place more than [] multicamera windows.", max_multicams)));
            return null;
        }
        atom_movable_screen_movable_pic_in_pic_ai C = Dm.@new<atom_movable_screen_movable_pic_in_pic_ai>();
        C.set_view_size(3, 3, FALSE);
        C.set_view_center(get_turf(eyeobj));
        C.set_ai(this);
        C.aiEye.name = Dm.text("[] (Secondary AI Eye)", name);
        if (!Dm.truthy(silent))
            to_chat(this, span_notice(I18n.T("Added new multicamera window.")));
        return C;
    }

    // tg: code/modules/mob/living/silicon/ai/multicam.dm:233
    public virtual void toggle_multicam()
    {
        if (!Dm.truthy(config.Get(typeof(datum_config_entry_flag_allow_ai_multicam)))) /* CONFIG_GET(flag/allow_ai_multicam) */
        {
            to_chat(this, span_warning(I18n.T("This action is currently disabled. Contact an administrator to enable this feature.")));
            return;
        }
        if (Dm.truthy(multicam_on))
            end_multicam();
        else
            start_multicam();
    }

    // tg: code/modules/mob/living/silicon/ai/multicam.dm:242
    public virtual void start_multicam()
    {
        if (Dm.truthy(multicam_on) || Dm.truthy(aiRestorePowerRoutine) || !isturf(loc))
            return;
        if (GLOB.ai_camera_room_landmark == null)
        {
            to_chat(this, span_warning(I18n.T("This function is not available at this time.")));
            return;
        }
        multicam_on = TRUE;
        refresh_multicam();
        to_chat(this, span_notice(I18n.T("Multiple-camera viewing mode activated.")));
    }

    // tg: code/modules/mob/living/silicon/ai/multicam.dm:252
    public virtual void refresh_multicam()
    {
        Waiting.NoSleep(reset_perspective(GLOB.ai_camera_room_landmark));
        if (client != null)
        {
            foreach (atom_movable_screen_movable_pic_in_pic P in Dm.each<atom_movable_screen_movable_pic_in_pic>(multicam_screens))
                P.show_to(client);
        }
    }

    // tg: code/modules/mob/living/silicon/ai/multicam.dm:259
    public virtual void end_multicam()
    {
        if (!Dm.truthy(multicam_on))
            return;
        multicam_on = FALSE;
        select_main_multicam_window(null);
        if (client != null)
        {
            foreach (atom_movable_screen_movable_pic_in_pic P in Dm.each<atom_movable_screen_movable_pic_in_pic>(multicam_screens))
                P.unshow_to(client);
        }
        Waiting.NoSleep(reset_perspective());
        to_chat(this, span_notice(I18n.T("Multiple-camera viewing mode deactivated.")));
    }

    // tg: code/modules/mob/living/silicon/ai/multicam.dm:272
    public virtual void select_main_multicam_window(atom_movable_screen_movable_pic_in_pic_ai P = null)
    {
        if (master_multicam == P)
            return;

        if (master_multicam != null)
        {
            master_multicam.set_view_center(get_turf(eyeobj), FALSE);
            master_multicam.unhighlight();
            master_multicam = null;
        }

        if (P != null)
        {
            P.highlight();
            eyeobj.setLoc(get_turf(P.center));
            P.set_view_center(eyeobj);
            master_multicam = P;
        }
    }
}
