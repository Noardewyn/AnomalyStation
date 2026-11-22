// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/wall_mounted.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5 slice 7 (the wall-mounted frames, D148): the whole file. A wallframe item is mounted on the wall the user faces (a
// click on a closed turf, or a screwdriver in hand) and becomes its result_path (the fire alarm, the air alarm, the button,
// the extinguisher cabinet frames), or is wrenched apart into its sheets; the electronics base with its grind results (gaps P5:
// the grinder). The var blocks are generated.

// tg: code/game/objects/items/wall_mounted.dm:1
public partial class obj_item_wallframe : obj_item
{
    // tg: code/game/objects/items/wall_mounted.dm:16
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        register_context();
        register_item_context();
        return dot;
    }

    // tg: code/game/objects/items/wall_mounted.dm:21
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        object dot = NONE;
        if (Dm.eq(held_item?.tool_behaviour, TOOL_WRENCH))
        {
            context[SCREENTIP_CONTEXT_LMB] = "Deconstruct";
            return CONTEXTUAL_SCREENTIP_SET;
        }
        return dot;
    }

    // tg: code/game/objects/items/wall_mounted.dm:27
    public override object add_item_context(obj_item source = null, DmList context = null, atom target = null, mob_living user = null)
    {
        object dot = NONE;
        if (Dm.truthy(find_support_structure(target)))
        {
            context[SCREENTIP_CONTEXT_LMB] = "Mount";
            return CONTEXTUAL_SCREENTIP_SET;
        }
        return dot;
    }

    /**
     * Returns an structure to mount on from the atom passed
     * for e.g if its not an closed turf then return an structure on the turf to mount on
     * Arguments
     * * atom/structure - the atom or something in this atom we are trying to mount on
    */
    // tg: code/game/objects/items/wall_mounted.dm:33
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("It can be [] apart.", EXAMINE_HINT(I18n.T("wrenched")))));
        return dot;
    }

    // tg: code/game/objects/items/wall_mounted.dm:43
    public virtual object find_support_structure(atom structure = null)
    {
        return isclosedturf(structure) ? structure : null;
    }

    // tg: code/game/objects/items/wall_mounted.dm:48
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        atom support_structure = find_support_structure(interacting_with) as atom;
        if (support_structure == null)
            return NONE;
        if (!Dm.truthy(await try_build(support_structure, user)))
            return ITEM_INTERACT_FAILURE;

        playsound(loc, Dm.resource("sound/machines/click.ogg"), 75, TRUE); // tg: code/game/objects/items/wall_mounted.dm:55
        user.visible_message(span_notice(I18n.T("[] attaches [] to the wall.", user.name, this)),
            span_notice(I18n.T("You attach [] to the wall.", this)),
            span_hear(I18n.T("You hear clicking.")));

        object floor_to_support = get_dir(user, support_structure);
        obj hanging_object = Dm.@new(result_path as Type, get_turf(user)) as obj;
        await hanging_object.setDir(floor_to_support);
        if (Dm.truthy(pixel_shift))
        {
            object sw__1 = floor_to_support;
            if (Dm.eq(sw__1, NORTH))
                hanging_object.pixel_y = Dm.num(pixel_shift);
            else if (Dm.eq(sw__1, SOUTH))
                hanging_object.pixel_y = -Dm.num(pixel_shift);
            else if (Dm.eq(sw__1, EAST))
                hanging_object.pixel_x = Dm.num(pixel_shift);
            else if (Dm.eq(sw__1, WEST))
                hanging_object.pixel_x = -Dm.num(pixel_shift);
        }
        await hanging_object.find_and_mount_on_atom();
        after_attach(hanging_object);
        qdel(this);

        return ITEM_INTERACT_SUCCESS;
    }

    /**
     * Check if we can build on this support structure
     *
     * Arguments
     * * atom/support - the atom we are trying to mount on
     * * mob/user - the player attempting to do the mount
    */
    // tg: code/game/objects/items/wall_mounted.dm:86
    public virtual async DmTask<object> try_build(atom support = null, mob user = null)
    {
        if (Dm.num(get_dist(support, user)) > 1)
        {
            balloon_alert(user, I18n.T("you are too far!"));
            return FALSE;
        }
        object floor_to_support = get_dir(user, support);
        if (!Dm.@in(floor_to_support, GLOB.cardinals))
        {
            balloon_alert(user, I18n.T("stand in line with wall!"));
            return FALSE;
        }
        turf T = get_turf(user) as turf;
        if (!isfloorturf(T))
        {
            balloon_alert(user, I18n.T("cannot place here!"));
            return FALSE;
        }
        if (Dm.truthy(check_wall_item(T, floor_to_support, wall_external)))
        {
            balloon_alert(user, I18n.T("already something here!"));
            return FALSE;
        }

        return TRUE;
    }

    /**
     * Stuff to do after wallframe attached to support atom
     *
     * Arguments
     * * obj/attached_to - the object that has been created on the atom
    */
    // tg: code/game/objects/items/wall_mounted.dm:110
    public virtual void after_attach(obj attached_to = null)
    {
        // tg: code/game/objects/items/wall_mounted.dm:111
        transfer_fingerprints_to(attached_to);
    }

    // tg: code/game/objects/items/wall_mounted.dm:113
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        return await interact_with_atom(get_step(get_turf(user), user.dir) as atom, user);
    }

    // tg: code/game/objects/items/wall_mounted.dm:116
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        to_chat(user, span_notice(I18n.T("You dismantle [].", this)));
        await deconstruct(TRUE);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/wall_mounted.dm:121
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        atom drop = drop_location() as atom;
        foreach (datum_material mat in Dm.each<datum_material>(custom_materials))
            Dm.@new(mat.sheet_type, drop, round(Dm.num(custom_materials[mat]) / SHEET_MATERIAL_AMOUNT));
    }
}

public static partial class GlobalProcs
{
    // tg: code/__HELPERS/atoms.dm:106  (M5 slice 7: a wallframe's mount)
    public static object check_wall_item(object floor_loc = null, object dir_toward_wall = null, object check_external = null)
    {
        check_external ??= 0;
        turf wall_loc = get_step(floor_loc as atom, Dm.num(dir_toward_wall)) as turf;
        foreach (obj checked_object in Dm.each<obj>(floor_loc))
        {
            if (Dm.truthy(is_type_in_typecache(checked_object, GLOB.WALLITEMS_INTERIOR)) && !Dm.truthy(check_external))
            {
                //Direction works sometimes
                if (Dm.eq(checked_object.dir, dir_toward_wall))
                    return TRUE;

                //Some stuff doesn't use dir properly, so we need to check pixel instead
                //That's exactly what get_turf_pixel() does
                if (get_turf_pixel(checked_object) == wall_loc)
                    return TRUE;
            }

            if (Dm.truthy(is_type_in_typecache(checked_object, GLOB.WALLITEMS_EXTERIOR)) && Dm.truthy(check_external))
            {
                if (Dm.eq(checked_object.dir, dir_toward_wall))
                    return TRUE;
            }
        }

        //Some stuff is placed directly on the wallturf (signs).
        //If we're only checking for external entities, we don't need to look though these.
        if (Dm.truthy(check_external))
            return FALSE;
        foreach (obj checked_object in Dm.each<obj>(wall_loc))
        {
            if (Dm.truthy(is_type_in_typecache(checked_object, GLOB.WALLITEMS_INTERIOR)))
            {
                if (checked_object.pixel_x == 0 && checked_object.pixel_y == 0)
                    return TRUE;
            }
        }
        return FALSE;
    }
}


// tg: code/game/objects/items/wall_mounted.dm:126
public partial class obj_item_electronics : obj_item
{
    // tg: code/game/objects/items/wall_mounted.dm:141  (gaps P5: what the grinder makes of the electronics)
    public override async DmTask<object> grind_results()
    {
        await DmTask.CompletedTask;
        return Dm.list(Dm.kv(typeof(datum_reagent_iron), 10), Dm.kv(typeof(datum_reagent_silicon), 10));
    }
}

// tg: code/__HELPERS/global_lists.dm:158  (M5 slice 7)
public partial class datum_controller_global_vars
{
    // tg: code/__HELPERS/global_lists.dm:158  GLOBAL_LIST_INIT(WALLITEMS_INTERIOR, typecacheof(list(...)))
    public DmList WALLITEMS_INTERIOR;

    // tg: code/__HELPERS/global_lists.dm:158
    public virtual void InitGlobalWALLITEMS_INTERIOR()
    {
        WALLITEMS_INTERIOR = typecacheof(Dm.list(
            typeof(obj_item_radio_intercom),
            typeof(obj_structure_secure_safe),
            typeof(obj_machinery_airalarm),
            typeof(obj_machinery_button),
            typeof(obj_machinery_computer_security_telescreen),
            typeof(obj_machinery_computer_security_telescreen_entertainment),
            typeof(obj_machinery_defibrillator_mount),
            typeof(obj_machinery_firealarm),
            typeof(obj_machinery_flasher),
            typeof(obj_machinery_keycard_auth_wall_mounted),
            typeof(obj_machinery_light_switch),
            typeof(obj_machinery_newscaster),
            typeof(obj_machinery_power_apc),
            typeof(obj_machinery_requests_console),
            typeof(obj_machinery_status_display),
            typeof(obj_machinery_ticket_machine),
            typeof(obj_machinery_turretid),
            typeof(obj_machinery_barsign),
            typeof(obj_structure_extinguisher_cabinet),
            typeof(obj_structure_fish_mount),
            typeof(obj_structure_fireaxecabinet),
            typeof(obj_structure_mirror),
            typeof(obj_structure_noticeboard),
            typeof(obj_structure_reagent_dispensers_wall),
            typeof(obj_structure_sign),
            typeof(obj_structure_sign_picture_frame),
            typeof(obj_structure_sign_poster_contraband_random),
            typeof(obj_structure_sign_poster_official_random),
            typeof(obj_structure_sign_poster_random),
            typeof(obj_structure_urinal)
            )) as DmList;
        Dm.append(ref gvars_datum_init_order, "WALLITEMS_INTERIOR");
    }

    // tg: code/__HELPERS/global_lists.dm:193  GLOBAL_LIST_INIT(WALLITEMS_EXTERIOR, typecacheof(list(...)))
    public DmList WALLITEMS_EXTERIOR;

    // tg: code/__HELPERS/global_lists.dm:193
    public virtual void InitGlobalWALLITEMS_EXTERIOR()
    {
        WALLITEMS_EXTERIOR = typecacheof(Dm.list(
            typeof(obj_machinery_camera),
            typeof(obj_machinery_light),
            typeof(obj_structure_light_construct),
            typeof(obj_structure_sink)
            )) as DmList;
        Dm.append(ref gvars_datum_init_order, "WALLITEMS_EXTERIOR");
    }
}
