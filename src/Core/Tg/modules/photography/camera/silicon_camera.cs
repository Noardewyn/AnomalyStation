// Translated from tgstation (snapshot 1d99bd7f): code/modules/photography/camera/silicon_camera.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (silicons SI2, D326): the cyborgs' and the AI's built-in photo cameras. The var block is generated (D111).

// tg: code/modules/photography/camera/silicon_camera.dm:1
public partial class obj_item_camera_siliconcam : obj_item_camera
{
    /// Checks if we can take a picture at this moment. Returns TRUE if we can, FALSE if we can't.
    // tg: code/modules/photography/camera/silicon_camera.dm:10
    public virtual object can_take_picture(mob_living_silicon clicker = null)
    {
        if (IS_UNCONSCIOUS_OR_CRIT(clicker) || Dm.truthy(clicker.incapacitated))
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/photography/camera/silicon_camera.dm:15
    public virtual void InterceptClickOn(mob_living_silicon clicker = null, object @params = null, atom clicked_on = null)
    {
        if (!Dm.truthy(can_take_picture(clicker)))
            return;
        clicker.face_atom(clicked_on);
        attempt_picture(clicked_on, clicker);
        toggle_camera_mode(clicker, sound: FALSE);
    }

    /// Toggles the camera mode on or off.
    /// If sound is TRUE, plays a sound effect and displays a message on successful toggle
    // tg: code/modules/photography/camera/silicon_camera.dm:24
    public virtual void toggle_camera_mode(mob user = null, float? sound = null)
    {
        sound ??= TRUE;
        if (user.click_intercept == this)
            user.click_intercept = null;

        else if (user.click_intercept == null)
            user.click_intercept = this;

        else
        {
            // Trying to turn on camera mode while you have another click intercept active, such as malf abilities
            if (Dm.truthy(sound))
            {
                balloon_alert(user, I18n.T("can't enable camera mode!"));
                playsound(user, Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 25, TRUE);
            }
            return;
        }

        if (Dm.truthy(sound))
        {
            playsound(user, Dm.resource("sound/items/tools/wirecutter.ogg"), 50, TRUE);
            balloon_alert(user, user.click_intercept == this ? I18n.T("camera mode activated") : I18n.T("camera mode deactivated"));
        }
    }

    // tg: code/modules/photography/camera/silicon_camera.dm:42
    public virtual async DmTask<object> selectpicture(mob user = null)
    {
        if (!Dm.truthy(length(stored)))
        {
            user.balloon_alert(user, I18n.T("no stored photos!"));
            return null;
        }
        DmList nametemp = Dm.list();
        DmList temp = Dm.list();
        foreach (datum_picture stored_photo in Dm.each_cast<datum_picture>(stored))
        {
            Dm.append(ref nametemp, stored_photo.picture_name);
            temp[stored_photo.picture_name] = stored_photo;
        }
        object find = await tgui_input_list(user, I18n.T("Select image"), I18n.T("Storage"), nametemp);
        if (find == null || temp[find] == null)
            return null;
        return temp[find];
    }

    // tg: code/modules/photography/camera/silicon_camera.dm:57
    public virtual async DmTask viewpictures(mob user = null)
    {
        datum_picture selection = await selectpicture(user) as datum_picture;
        if (selection is datum_picture)
        {
            obj_item_photo P = Dm.@new<obj_item_photo>(this, selection);
            P.show(user);
            to_chat(user, P.desc);
            qdel(P);
        }
    }
}

// tg: code/modules/photography/camera/silicon_camera.dm:65
public partial class obj_item_camera_siliconcam_ai_camera : obj_item_camera_siliconcam
{
    // tg: code/modules/photography/camera/silicon_camera.dm:69
    public override object can_take_picture(mob_living_silicon clicker = null)
    {
        if (Dm.truthy((clicker as mob_living_silicon_ai).control_disabled))
            return FALSE;
        return base.can_take_picture(clicker);
    }

    // tg: code/modules/photography/camera/silicon_camera.dm:74
    public override object balloon_alert(mob viewer = null, object text = null)
    {
        if (isAI(loc))
        {
            // redirects balloon alerts on us to balloon alerts on our ai eye
            mob_living_silicon_ai ai = loc as mob_living_silicon_ai;
            return ai.eyeobj.balloon_alert(viewer, text);
        }

        return base.balloon_alert(viewer, text);
    }

    // tg: code/modules/photography/camera/silicon_camera.dm:82
    public override async DmTask after_picture(mob user = null, datum_picture picture = null)
    {
        float number = length(stored);
        picture.picture_name = Dm.text("Image [] (taken by [])", number, loc.name);
        stored[picture] = TRUE;
        balloon_alert(user, I18n.T("image recorded"));
        user.playsound_local(get_turf(user), SFX_POLAROID, 50, TRUE, -3);
    }
}

// tg: code/modules/photography/camera/silicon_camera.dm:89
public partial class obj_item_camera_siliconcam_robot_camera : obj_item_camera_siliconcam
{
    // tg: code/modules/photography/camera/silicon_camera.dm:93
    public override object can_take_picture(mob_living_silicon clicker = null)
    {
        if (Dm.truthy((clicker as mob_living_silicon_robot).lockcharge))
            return FALSE;
        return base.can_take_picture(clicker);
    }

    // tg: code/modules/photography/camera/silicon_camera.dm:98
    public override async DmTask after_picture(mob user = null, datum_picture picture = null)
    {
        if (user is mob_living_silicon_robot robot_user && robot_user.connected_ai is mob_living_silicon_ai)
        {
            float number = robot_user.connected_ai.aicamera.stored.len;
            picture.picture_name = Dm.text("Image [] (taken by [])", number, loc.name);
            robot_user.connected_ai.aicamera.stored[picture] = TRUE;
            balloon_alert(user, I18n.T("image recorded and uploaded"));
        }
        else
        {
            float number = stored.len;
            picture.picture_name = Dm.text("Image [] (taken by [])", number, loc.name);
            stored[picture] = TRUE;
            balloon_alert(user, I18n.T("image recorded and saved locally"));
        }
        playsound(this, SFX_POLAROID, 75, TRUE, -3);
    }

    // tg: code/modules/photography/camera/silicon_camera.dm:111
    public override async DmTask<object> selectpicture(mob user = null)
    {
        if (user is mob_living_silicon_robot robot_user && robot_user.connected_ai != null)
        {
            robot_user.picturesync();
            return await robot_user.connected_ai.aicamera.selectpicture(user);
        }
        return await base.selectpicture(user);
    }

    // tg: code/modules/photography/camera/silicon_camera.dm:117
    public virtual async DmTask borgprint(mob_living_silicon_robot user = null)
    {
        if (user is not mob_living_silicon_robot || user.toner < printcost)
        {
            balloon_alert(user, I18n.T("not enough toner!"));
            return;
        }
        datum_picture selection = await selectpicture(user) as datum_picture;
        if (selection is not datum_picture)
        {
            balloon_alert(user, I18n.T("invalid image!"));
            return;
        }
        obj_item_photo printed = Dm.@new<obj_item_photo>(user.drop_location(), selection);
        printed.pixel_x = printed.base_pixel_x + rand(-10, 10);
        printed.pixel_y = printed.base_pixel_y + rand(-10, 10);
        user.toner -= printcost; //All fun allowed.
        user.visible_message(span_notice(I18n.T("[] spits out a photograph from a narrow slot on its chassis.", user.name)), span_notice(I18n.T("You print a photograph.")));
        balloon_alert(user, I18n.T("photograph printed"));
        playsound(this, Dm.resource("sound/items/taperecorder/taperecorder_print.ogg"), 50, TRUE, -3);
    }
}
