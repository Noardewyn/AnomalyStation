// Translated from tgstation (snapshot 1d99bd7f): code/modules/photography/camera/camera.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file but the image itself (gaps J3, D298): the camera takes pictures with their description, spotted mobs and names, flash, film, zoom and printing, and prints a photo. The picture's image
// (camera_get_icon, a composite of the seen atoms' sprites) has no counterpart in the 3D client: a row. The holodisk record is a row too (the AI's camera net came with silicons SI4); the camera is a circuit shell. The var blocks are generated.

// tg: code/modules/photography/camera/camera.dm:2
public partial class obj_item_camera : obj_item
{
    // __DEFINES/camera.dm: APERTURE_TO_METERS
    private static float APERTURE_TO_METERS(float aperture) => (aperture * 2) - 1;

    // tg: code/modules/photography/camera/camera.dm:59
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        //we do this so if this camera is used as an internal component, the flash will still be visible
        if (Dm.truthy(flash_enabled))
        {
            atom_movable parent = loc as atom_movable;
            light_holder = this;
            while (!(parent == null || ismob(parent) || isturf(parent)))
            {
                light_holder = parent;
                parent = light_holder.loc as atom_movable;
            }
            // light_holder.AddComponentFrom(REF(src), /datum/component/overlay_lighting/camera, ...): the lighting module (D24): the flash is the holder's light_on
            Dm.Deferred("code/modules/photography/camera/camera.dm:69", "Initialize: /datum/component/overlay_lighting/camera, the flash's overlay light (the lighting module, D24)");
        }

        Waiting.NoSleep(AddComponent(typeof(datum_component_shell), Dm.list(Dm.@new<obj_item_circuit_component_camera>(), Dm.@new<obj_item_circuit_component_remotecam_polaroid>()), SHELL_CAPACITY_SMALL));
        // register_context(): the context tips (screentips, not now)
        return dot;
    }

    // tg: code/modules/photography/camera/camera.dm:74
    public override object Destroy(float? force = null)
    {
        light_holder = null;
        return base.Destroy(force);
    }

    // tg: code/modules/photography/camera/camera.dm:98
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        dot.Add(span_notice(I18n.T("It has [] photos left.", pictures_left)));
        dot.Add(span_notice(I18n.T("Alt-click to change its focusing, allowing you to set how big of an area it will capture.")));
        dot.Add(span_notice(I18n.T("Ctrl-click to change the printer between color and monochrome.")));
        dot.Add(span_notice(I18n.T("The present dimensions of the picture are []", EXAMINE_HINT(Dm.text("[]x[]", APERTURE_TO_METERS(picture_size_x), APERTURE_TO_METERS(picture_size_y))))));

        if (disk == null)
            dot.Add(span_notice(I18n.T("It has a slot for a holorecord disk.")));
        else
            dot.Add(span_notice(I18n.T("It has \\an [] inserted.", disk.name)));
        return dot;
    }

    // tg: code/modules/photography/camera/camera.dm:110
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (gone == disk)
            disk = null;
        return dot;
    }

    /**
     * Adjusts the zoom of this camera
     * Arguments
     *
     * * desired_x - the x zoom value to use
     * * desired_y - the y zoom value to use
     * * mob/user - the optional user who is taking the photo. Passing the mob will ask for input and ignore the above params
    */
    // tg: code/modules/photography/camera/camera.dm:123
    public virtual async DmTask<object> adjust_zoom(object desired_x = null, object desired_y = null, mob user = null)
    {
        desired_x ??= picture_size_x;
        desired_y ??= picture_size_y;

        if (user != null)
        {
            if (loc != user)
            {
                to_chat(user, span_warning(I18n.T("You must be holding the camera to continue!")));
                return FALSE;
            }
            desired_x = await tgui_input_number(user, I18n.T("Set camera half width Aperture"), I18n.T("Zoom"), picture_size_x, CAMERA_PICTURE_SIZE_HARD_LIMIT, 1);
            if (!Dm.truthy(desired_x) || QDELETED(user) || QDELETED(this) || !Dm.truthy(await user.can_perform_action(this, Dm.bitor(FORBID_TELEKINESIS_REACH, ALLOW_PAI))) || loc != user)
                return FALSE;
            desired_y = await tgui_input_number(user, I18n.T("Set camera half height Aperture"), I18n.T("Zoom"), picture_size_y, CAMERA_PICTURE_SIZE_HARD_LIMIT, 1);
            if (!Dm.truthy(desired_y) || QDELETED(user) || QDELETED(this) || !Dm.truthy(await user.can_perform_action(this, Dm.bitor(FORBID_TELEKINESIS_REACH, ALLOW_PAI))) || loc != user)
                return FALSE;
        }

        picture_size_x = clamp(Dm.num(desired_x), 1, CAMERA_PICTURE_SIZE_HARD_LIMIT);
        picture_size_y = clamp(Dm.num(desired_y), 1, CAMERA_PICTURE_SIZE_HARD_LIMIT);

        if (user != null)
            to_chat(user, span_notice(I18n.T("The dimensions of the picture will be []", EXAMINE_HINT(Dm.text("[]x[]", APERTURE_TO_METERS(picture_size_x), APERTURE_TO_METERS(picture_size_y))))));

        return TRUE;
    }

    /// Resets flash to be used again
    // tg: code/modules/photography/camera/camera.dm:146
    public virtual async DmTask cooldown__proc()
    {
        while (Dm.truthy(blending)) // UNTIL(!blending)
            await stoplag();
        icon_state = Dm.str(base_icon_state);
        on = TRUE;
    }

    /// Turns the light/flash off
    // tg: code/modules/photography/camera/camera.dm:154
    public virtual async DmTask flash_end()
    {
        await light_holder.set_light_on(FALSE);
    }

    /**
     * Turns the flash quickly on and off when picture is taken
     * Arguments
     *
     * * atom/target - the target we are trying to take a photo of
     * * mob/user - the optional user who is taking the photo
    */
    // tg: code/modules/photography/camera/camera.dm:166
    public virtual async DmTask on_flash(atom target = null, mob user = null)
    {
        on = FALSE;
        _addtimer(Dm.@new<datum_callback>(this, nameof(cooldown__proc)), cooldown, file: "code/modules/photography/camera/camera.dm", line: 171);
        icon_state = Dm.text("[]_off", base_icon_state);
        if (Dm.truthy(flash_enabled))
        {
            await light_holder.set_light_on(TRUE);
            _addtimer(Dm.@new<datum_callback>(this, nameof(flash_end)), FLASH_LIGHT_DURATION, Dm.bitor(TIMER_OVERRIDE, TIMER_UNIQUE), file: "code/modules/photography/camera/camera.dm", line: 175);
        }
    }

    /**
     * Steal souls from all mobs captured in the image
     * Arguments
     *
     * * list/victims - list of all mobs captured in the image
    */
    // tg: code/modules/photography/camera/camera.dm:183
    public virtual async DmTask steal_souls(DmList victims = null)
    {
    }

    /**
     * Attempts to take an image of the target and all its surrounding tiles
     * Returns TRUE if it successfully starts taking a picture.
     * Arguments
     *
     * * atom/target - the target we are trying to take a photo of
     * * mob/user - the optional user who is taking the photo
    */
    // tg: code/modules/photography/camera/camera.dm:196
    public virtual object attempt_picture(atom target = null, mob user = null)
    {
        if (!Dm.truthy(on))
        {
            if (user != null)
                user.balloon_alert(user, I18n.T("flash still charging!"));
            return FALSE;
        }

        if (Dm.truthy(blending))
        {
            if (user != null)
                user.balloon_alert(user, I18n.T("image still blending!"));
            return FALSE;
        }

        blending = TRUE;
        Waiting.Detach(capture_image(target, user));
        return TRUE;
    }

    /**
     * Renders an image of the target and all its surrounding tiles
     * Arguments passed from attempt_picture()
    */
    // tg: code/modules/photography/camera/camera.dm:217
    public virtual async DmTask capture_image(atom target = null, mob user = null)
    {
        //Checking if we can target
        turf target_turf = get_turf(target);
        object view_size = user?.client?.view ?? world.view;
        if (target_turf == null)
        {
            blending = FALSE;
            return;
        }
        if (isAI(user))
        {
            if (!SScameras.is_visible_by_cameras(target_turf))
            {
                blending = FALSE;
                return;
            }
        }
        else if (!Dm.@in(target_turf, view(view_size, user)))
        {
            blending = FALSE;
            return;
        }

        //These vars will be reused later on
        float size_x = picture_size_x - 1;
        float size_y = picture_size_y - 1;
        DmList viewlist = getviewsize(view_size) as DmList;
        float view_range = max(Dm.num(viewlist[1]), Dm.num(viewlist[2])) + max(size_x, size_y);
        atom viewer = get_turf(user?.client?.eye as atom ?? user);
        DmList seen = get_hear_turfs(view_range, viewer);
        if (!Dm.@in(target_turf, seen))
        {
            blending = FALSE;
            return;
        }

        //taking the actual picture
        await on_flash(target, user);
        DmList mobs_spotted = Dm.list();
        DmList dead_spotted = Dm.list();
        DmList turfs = Dm.list();
        DmList mobs = Dm.list();
        object blueprints = FALSE;
        float width = APERTURE_TO_METERS(picture_size_x);
        float height = APERTURE_TO_METERS(picture_size_y);
        ///list of human names taken on picture
        DmList names = Dm.list();
        // tg's turf reservation holds the clones camera_get_icon renders; with no image there are no clones (a row)
        foreach (turf seen_placeholder in Dm.each<turf>(CORNER_BLOCK_OFFSET(target_turf, width, height, -size_x, -size_y)))
        {
            if (seen_placeholder == null)
                continue;
            if (!Dm.@in(seen_placeholder, seen))
                continue;

            //Multi-z photography
            turf target_placeholder = seen_placeholder;
            while (target_placeholder != null)
            {
                turfs.Add(target_placeholder);
                foreach (mob mob_there in Dm.each<mob>(target_placeholder))
                    mobs.Add(mob_there);
                if (Dm.locate<obj_item_blueprints>(target_placeholder) != null)
                    blueprints = TRUE;

                if (isopenspaceturf(target_placeholder) || istype(target_placeholder, typeof(turf_open_floor_glass)))
                    target_placeholder = GET_TURF_BELOW(target_placeholder) as turf;
                else
                    break;
            }
        }

        // do this before picture is taken so we can reveal revenants for the photo
        await steal_souls(mobs);

        DmList desc_lines = Dm.list(I18n.T("This is a photo of an area of [] meters by [] meters.", width, height));
        foreach (mob seen_mob in Dm.each<mob>(mobs))
        {
            mobs_spotted.Add(seen_mob);
            if (seen_mob.stat == DEAD)
                dead_spotted.Add(seen_mob);
            object info = seen_mob.get_photo_description(this);
            if (info != null)
                desc_lines.Add(info);
        }

        object get_icon = await camera_get_icon(turfs, target_turf, null);
        foreach (mob_living_carbon_human person in Dm.each<mob_living_carbon_human>(mobs))
        {
            if (Dm.truthy(Dm.bitand(person.obscured_slots, HIDEFACE)))
                continue;
            names.Add(Dm.text("[]", person.name));
        }

        datum_picture picture = Dm.@new<datum_picture>(
            "picture",
            jointext(desc_lines, "<br>"),
            mobs_spotted,
            dead_spotted,
            names,
            get_icon,
            null,
            width * ICON_SIZE_X,
            height * ICON_SIZE_Y,
            blueprints,
            null,
            null,
            see_ghosts);
        await after_picture(user, picture);
        if (Dm.truthy(_listen_lookup?[COMSIG_CAMERA_IMAGE_CAPTURED]))
            _SendSignal(COMSIG_CAMERA_IMAGE_CAPTURED, Dm.list(this, target, user, picture));
        blending = FALSE;
    }

    /**
     * Action to take after the picture is taken
     *
     * Arguments
     *
     * * mob/user - the user who took the picture
     * * datum/picture/picture - the picture taken
    */
    // tg: code/modules/photography/camera/camera.dm:324
    public virtual async DmTask after_picture(mob user = null, datum_picture picture = null)
    {
        if (Dm.truthy(silent))
            user.playsound_local(get_turf(this), SFX_POLAROID, 35, TRUE);
        else
            playsound(loc, SFX_POLAROID, 75, TRUE, -3);

        if (Dm.truthy(print_picture_on_snap))
            await printpicture(user, picture);
    }

    /**
     * Print the picture tkane on film
     *
     * Arguments
     *
     * * mob/user - the user who took the picture
     * * datum/picture/picture - the picture taken
    */
    // tg: code/modules/photography/camera/camera.dm:343
    public virtual async DmTask printpicture(mob user = null, datum_picture picture = null)
    {
        obj_item_photo new_photo = null;
        if (user != null)
        {
            if (!Dm.truthy(pictures_left))
            {
                to_chat(user, span_warning(I18n.T("No film left.")));
                return;
            }

            new_photo = Dm.@new<obj_item_photo>(this, picture);

            to_chat(user, span_notice(I18n.T("[] photos left.", pictures_left)));

            bool name_customized = false;
            if (Dm.truthy(can_customise))
            {
                object customise = await tgui_alert(user, I18n.T("Do you want to customize the photo?"), I18n.T("Customization"), Dm.list("Yes", "No"));
                if (Dm.eq(customise, "Yes"))
                {
                    object name1 = await tgui_input_text(user, I18n.T("Set a name for this photo, or leave blank."), I18n.T("Name"), max_length: 32);
                    object desc1 = await tgui_input_text(user, I18n.T("Set a description to add to photo, or leave blank."), I18n.T("Description"), max_length: 128);
                    object caption = await tgui_input_text(user, I18n.T("Set a caption for this photo, or leave blank."), I18n.T("Caption"), max_length: 256);
                    if (Dm.truthy(name1))
                    {
                        picture.picture_name = Dm.str(name1);
                        name_customized = true;
                    }
                    if (Dm.truthy(desc1))
                        picture.picture_desc = Dm.text("[] - []", desc1, picture.picture_desc);
                    if (Dm.truthy(caption))
                        picture.caption = caption;
                }
            }
            if (!name_customized && Dm.truthy(default_picture_name))
                picture.picture_name = Dm.str(default_picture_name);
        }
        else if (isliving(loc))
        {
            mob_living holder = (mob_living)loc;

            if (!Dm.truthy(pictures_left))
            {
                to_chat(holder, span_warning(I18n.T("No film left.")));
                return;
            }

            new_photo = Dm.@new<obj_item_photo>(get_turf(this), picture);

            to_chat(holder, span_notice(I18n.T("[] photos left.", pictures_left)));
        }

        await new_photo.set_picture(picture, TRUE, TRUE);
        // if(CONFIG_GET(flag/picture_logging_camera)) picture.log_to_file(): the pictures' log (no image to log)

        pictures_left--;

        await user.put_in_hands(new_photo);
    }

    // tg: code/modules/photography/camera/camera.dm:392
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (disk == null)
            return null;
        playsound(this, Dm.resource("sound/machines/card_slide.ogg"), 50);
        await user.put_in_hands(disk);
        disk = null;
        return null;
    }

    // tg: code/modules/photography/camera/camera.dm:399
    public override async DmTask<object> attack__proc(mob_living target_mob = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        return null;
    }

    // tg: code/modules/photography/camera/camera.dm:402
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is obj_item_camera_film)
        {
            if (Dm.truthy(pictures_left))
            {
                balloon_alert(user, I18n.T("isn't empty!"));
                return ITEM_INTERACT_BLOCKING;
            }
            if (!Dm.truthy(await user.temporarilyRemoveItemFromInventory(tool)))
                return ITEM_INTERACT_BLOCKING;
            playsound(this, Dm.resource("sound/machines/click.ogg"), 50, TRUE);
            qdel(tool);
            pictures_left = pictures_max;
            return ITEM_INTERACT_SUCCESS;
        }

        if (tool is obj_item_disk_holodisk)
        {
            if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
            {
                balloon_alert(user, I18n.T("stuck in hand!"));
                return TRUE;
            }
            if (disk != null)
            {
                await user.put_in_hands(disk);
                balloon_alert(user, I18n.T("disks swapped!"));
            }
            else
                balloon_alert(user, I18n.T("disk inserted!"));
            playsound(this, Dm.resource("sound/machines/card_slide.ogg"), 50);
            disk = (obj_item_disk_holodisk)tool;
            return ITEM_INTERACT_SUCCESS;
        }
        return null;
    }

    // tg: code/modules/photography/camera/camera.dm:427
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (HAS_TRAIT(interacting_with, TRAIT_COMBAT_MODE_SKIP_INTERACTION))
            return NONE;
        return await ranged_interact_with_atom(interacting_with, user, modifiers);
    }

    // tg: code/modules/photography/camera/camera.dm:432
    public override async DmTask<object> ranged_interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (disk != null)
        {
            if (!ismob(interacting_with))
            {
                to_chat(user, span_warning(I18n.T("Invalid holodisk target.")));
                return ITEM_INTERACT_BLOCKING;
            }
            if (disk.record != null)
                QDEL_NULL(ref disk.record);

            disk.record = Dm.@new<datum_holorecord>();
            var recorded_mob = (mob)interacting_with;
            disk.record.caller_name = recorded_mob.name;
            disk.record.set_caller_image(recorded_mob);
        }

        attempt_picture(interacting_with, user);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/photography/camera/camera.dm:448
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        if (!Dm.truthy(await adjust_zoom(user: user)))
            return CLICK_ACTION_BLOCKING;
        if (Dm.truthy(silent)) // Don't out your silent cameras
            user.playsound_local(get_turf(this), Dm.resource("sound/machines/click.ogg"), 50, TRUE);
        else
            playsound(this, Dm.resource("sound/machines/click.ogg"), 50, TRUE);
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/modules/photography/camera/camera.dm:457
    public override async DmTask<object> item_ctrl_click(mob user = null)
    {
        print_monochrome = Dm.truthy(print_monochrome) ? FALSE : TRUE;
        user.balloon_alert(user, I18n.T("printing []", Dm.truthy(print_monochrome) ? "monochrome" : "in color"));
        if (Dm.truthy(silent)) // Don't out your silent cameras
            user.playsound_local(get_turf(this), Dm.resource("sound/machines/click.ogg"), 50, TRUE);
        else
            playsound(this, Dm.resource("sound/machines/click.ogg"), 50, TRUE);
        return CLICK_ACTION_SUCCESS;
    }
}
