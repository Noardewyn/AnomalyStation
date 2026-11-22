// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/reflector.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: the M7 supermatter slice (G5, D163): the reflectors (base, single, double, box and their anchored and mapping
// subtypes) that turn MetaStation's emitter beams onto the crystal, with their construction, rotation and their tgui window
// (Reflector), the cyborg's reach (attack_robot, silicons SI2). Left out: the integrated circuits' USB port and its circuit
// component (wiremod, after the core version). The var blocks are generated (D111).

// tg: code/game/objects/structures/reflector.dm:1
public partial class obj_structure_reflector : obj_structure
{
    // tg: code/game/objects/structures/reflector.dm:21
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        icon_state = "reflector_base";
        allowed_projectile_typecache = typecacheof(allowed_projectile_typecache) as DmList;
        if (Dm.truthy(deflector_icon_state))
        {
            deflector_overlay = mutable_appearance(icon, (string)deflector_icon_state) as mutable_appearance;
            // We offset our physical position DOWN, because TRANSFORM IS A FUCK
            deflector_overlay.pixel_y = -32;
            deflector_overlay.pixel_z = 32;
            add_overlay(deflector_overlay);
        }

        if (rotation_angle == -1)
            set_angle(dir2angle(dir));
        else
            set_angle(rotation_angle);

        if (Dm.truthy(admin))
            can_rotate = FALSE;

        // AddComponent(/datum/component/usb_port, typecacheof(list(/obj/item/circuit_component/reflector), only_root_path = TRUE))
        Dm.Deferred("code/game/objects/structures/reflector.dm:40", "Initialize: the integrated circuits' USB port and its reflector component (wiremod, after the core version)");
        return dot;
    }

    // tg: code/game/objects/structures/reflector.dm:42
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.examine(user, __extra);
        if (Dm.truthy(finished))
        {
            Dm.append(ref dot, I18n.T("It is set to [] degrees, and the rotation is [].", rotation_angle, Dm.truthy(can_rotate) ? "unlocked" : "locked"));
            if (!Dm.truthy(admin))
            {
                if (Dm.truthy(can_rotate))
                {
                    Dm.append(ref dot, span_notice(I18n.T("Use your <b>hand</b> to adjust its direction.")));
                    Dm.append(ref dot, span_notice(I18n.T("Use a <b>screwdriver</b> to lock the rotation.")));
                }
                else
                    Dm.append(ref dot, span_notice(I18n.T("Use <b>screwdriver</b> to unlock the rotation.")));
            }
        }
        return dot;
    }

    // tg: code/game/objects/structures/reflector.dm:53
    public virtual void set_angle(object new_angle = null)
    {
        if (Dm.truthy(can_rotate))
        {
            rotation_angle = Dm.num(new_angle);
            if (Dm.truthy(deflector_overlay))
            {
                cut_overlay(deflector_overlay);
                deflector_overlay.transform = turn(Dm.@new<matrix>(), Dm.num(new_angle));
                add_overlay(deflector_overlay);
            }
        }
    }

    // tg: code/game/objects/structures/reflector.dm:62
    public override async DmTask setDir(object newdir = null)
    {
        await base.setDir(NORTH);
    }

    // tg: code/game/objects/structures/reflector.dm:65
    public override async DmTask<object> bullet_act(obj_projectile proj = null, object def_zone = null, float? piercing_hit = null, float? blocked = null)
    {
        object pdir = proj.dir;
        object pangle = proj.angle;
        turf ploc = get_turf(proj);
        if (!Dm.truthy(finished) || !Dm.truthy(allowed_projectile_typecache[proj.type]) || !Dm.@in(proj.dir, GLOB.cardinals))
            return await base.bullet_act(proj, def_zone, piercing_hit, blocked);
        if (!Dm.eq(await auto_reflect(proj, pdir, ploc, pangle), BULLET_ACT_FORCE_PIERCE))
            return await base.bullet_act(proj, def_zone, piercing_hit, blocked);
        return BULLET_ACT_FORCE_PIERCE;
    }

    // tg: code/game/objects/structures/reflector.dm:75
    public virtual async DmTask<object> auto_reflect(obj_projectile proj = null, object pdir = null, turf ploc = null, object pangle = null)
    {
        proj.ignore_source_check = TRUE;
        proj.range = Dm.num(proj.maximum_range);
        proj.maximum_range = max(Dm.postdec(ref proj.maximum_range), 0);
        return BULLET_ACT_FORCE_PIERCE;
    }

    // tg: code/game/objects/structures/reflector.dm:81
    public override async DmTask<object> tool_act(mob_living user = null, obj_item tool = null, DmList modifiers = null)
    {
        if (Dm.truthy(admin) && Dm.truthy(tool.tool_behaviour))
            return ITEM_INTERACT_BLOCKING;
        return await base.tool_act(user, tool, modifiers);
    }

    // tg: code/game/objects/structures/reflector.dm:86
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        can_rotate = Dm.num(!Dm.truthy(can_rotate));
        to_chat(user, span_notice(I18n.T("You [] []'s rotation.", Dm.truthy(can_rotate) ? "unlock" : "lock", this)));
        tool.play_tool_sound(this);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/reflector.dm:92
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(anchored))
        {
            to_chat(user, span_warning(I18n.T("Unweld [] from the floor first!", this)));
            return ITEM_INTERACT_SUCCESS;
        }
        user.visible_message(span_notice(I18n.T("[] starts to dismantle [].", user, this)), span_notice(I18n.T("You start to dismantle []...", this)));
        if (!Dm.truthy(await tool.use_tool(this, user, 8 * SECONDS, volume: 50)))
            return ITEM_INTERACT_BLOCKING;
        to_chat(user, span_notice(I18n.T("You dismantle [].", this)));
        Dm.@new(framebuildstacktype, drop_location(), framebuildstackamount);
        if (Dm.truthy(buildstackamount))
            Dm.@new(buildstacktype, drop_location(), buildstackamount);
        qdel(this);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/reflector.dm:106
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(tool.tool_start_check(user, amount: 1)))
            return ITEM_INTERACT_BLOCKING;
        if (Dm.num(atom_integrity) < Dm.num(max_integrity))
        {
            user.visible_message(span_notice(I18n.T("[] starts to repair [].", user, this)),
                span_notice(I18n.T("You begin repairing []...", this)),
                span_hear(I18n.T("You hear welding.")));
            if (Dm.truthy(await tool.use_tool(this, user, 4 * SECONDS, volume: 40)))
            {
                atom_integrity = max_integrity;
                user.visible_message(span_notice(I18n.T("[] repairs [].", user, this)),
                    span_notice(I18n.T("You finish repairing [].", this)));
            }
        }
        else if (!Dm.truthy(anchored))
        {
            user.visible_message(span_notice(I18n.T("[] starts to weld [] to the floor.", user, this)),
                span_notice(I18n.T("You start to weld [] to the floor...", this)),
                span_hear(I18n.T("You hear welding.")));
            if (Dm.truthy(await tool.use_tool(this, user, 2 * SECONDS, volume: 50)))
            {
                await set_anchored(TRUE);
                to_chat(user, span_notice(I18n.T("You weld [] to the floor.", this)));
            }
        }
        else
        {
            user.visible_message(span_notice(I18n.T("[] starts to cut [] free from the floor.", user, this)),
                span_notice(I18n.T("You start to cut [] free from the floor...", this)),
                span_hear(I18n.T("You hear welding.")));
            if (Dm.truthy(await tool.use_tool(this, user, 2 * SECONDS, volume: 50)))
            {
                await set_anchored(FALSE);
                to_chat(user, span_notice(I18n.T("You cut [] free from the floor.", this)));
            }
        }

        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/reflector.dm:134
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(admin))
            return ITEM_INTERACT_BLOCKING;
        //Finishing the frame
        if (!(tool is obj_item_stack_sheet))
            return NONE;

        if (Dm.truthy(finished))
            return ITEM_INTERACT_BLOCKING;

        obj_item_stack_sheet using_stack = (obj_item_stack_sheet)tool;
        if (using_stack is obj_item_stack_sheet_glass)
        {
            if (!Dm.truthy(await using_stack.use(5)))
            {
                to_chat(user, span_warning(I18n.T("You need five sheets of glass to create a reflector!")));
                return ITEM_INTERACT_BLOCKING;
            }

            Dm.@new<obj_structure_reflector_single>(drop_location());
            qdel(this);
            return ITEM_INTERACT_SUCCESS;
        }

        if (using_stack is obj_item_stack_sheet_rglass)
        {
            if (!Dm.truthy(await using_stack.use(10)))
            {
                to_chat(user, span_warning(I18n.T("You need ten sheets of reinforced glass to create a double reflector!")));
                return ITEM_INTERACT_BLOCKING;
            }

            Dm.@new<obj_structure_reflector_double>(drop_location());
            qdel(this);
            return ITEM_INTERACT_SUCCESS;
        }

        if (using_stack is obj_item_stack_sheet_mineral_diamond)
        {
            if (!Dm.truthy(await using_stack.use(1)))
                return ITEM_INTERACT_BLOCKING;

            Dm.@new<obj_structure_reflector_box>(drop_location());
            qdel(this);
            return ITEM_INTERACT_SUCCESS;
        }
        return null;
    }

    // tg: code/game/objects/structures/reflector.dm:171
    public virtual async DmTask<object> rotate(mob user = null)
    {
        if (!Dm.truthy(can_rotate) || Dm.truthy(admin))
        {
            to_chat(user, span_warning(I18n.T("The rotation is locked!")));
            return FALSE;
        }
        object new_angle = await tgui_input_number(user, I18n.T("New angle for primary reflection face"), I18n.T("Reflector Angle"), rotation_angle, 360);
        if (new_angle == null || QDELETED(user) || QDELETED(this) || !Dm.truthy(await usr.can_perform_action(this, FORBID_TELEKINESIS_REACH)))
            return FALSE;
        set_angle(SIMPLIFY_DEGREES(Dm.num(new_angle)));
        return TRUE;
    }

    // tg: code/game/objects/structures/reflector.dm:258
    public override object ex_act(object severity = null, object target = null, params object[] __extra)
    {
        if (Dm.truthy(admin))
            return FALSE;
        return base.ex_act(severity, target, __extra);
    }

    // tg: code/game/objects/structures/reflector.dm:263
    public override async DmTask<object> singularity_act()
    {
        if (Dm.truthy(admin))
            return null;
        else
            return await base.singularity_act();
    }

    // tgui menu

    // tg: code/game/objects/structures/reflector.dm:298
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        if (!Dm.truthy(finished))
        {
            user.balloon_alert(user, I18n.T("nothing to rotate!"));
            return null;
        }
        if (!Dm.truthy(can_rotate))
        {
            user.balloon_alert(user, I18n.T("can't rotate!"));
            ui?.close();
            return null;
        }
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "Reflector");
            await ui.open();
        }
        return null;
    }

    // tg: code/game/objects/structures/reflector.dm:315
    public override object ui_state(mob user = null)
    {
        return GLOB.physical_state; //Prevents borgs from adjusting this at range
    }

    // tg: code/game/objects/structures/reflector.dm:318
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["rotation_angle"] = rotation_angle;
        data["reflector_name"] = name;

        return data;
    }

    // tg: code/game/objects/structures/reflector.dm:325
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = null;
        dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        if (Dm.eq(action, "rotate"))
        {
            if (!Dm.truthy(can_rotate) || Dm.truthy(admin))
                return FALSE;
            object new_angle = @params["rotation_angle"];
            if (new_angle != null)
                set_angle(SIMPLIFY_DEGREES(Dm.num(new_angle)));
            return TRUE;
        }
        else if (Dm.eq(action, "calculate"))
        {
            if (!Dm.truthy(can_rotate) || Dm.truthy(admin))
                return FALSE;
            object new_angle = Dm.add(rotation_angle, @params["rotation_angle"]);
            if (new_angle != null)
                set_angle(SIMPLIFY_DEGREES(Dm.num(new_angle)));
            return TRUE;
        }
        return dot;
    }
}

//TYPES OF REFLECTORS, SINGLE, DOUBLE, BOX

//SINGLE

// tg: code/game/objects/structures/reflector.dm:185
public partial class obj_structure_reflector_single : obj_structure_reflector
{
    // tg: code/game/objects/structures/reflector.dm:201
    public override async DmTask<object> auto_reflect(obj_projectile proj = null, object pdir = null, turf ploc = null, object pangle = null)
    {
        float incidence = GET_ANGLE_OF_INCIDENCE(rotation_angle, (Dm.num(proj.angle) + 180));
        if (abs(incidence) > 90 && abs(incidence) < 270)
            return FALSE;
        float new_angle = SIMPLIFY_DEGREES(rotation_angle + incidence);
        await proj.set_angle_centered(loc, new_angle);
        return await base.auto_reflect(proj, pdir, ploc, pangle);
    }
}

//DOUBLE

// tg: code/game/objects/structures/reflector.dm:211
public partial class obj_structure_reflector_double : obj_structure_reflector
{
    // tg: code/game/objects/structures/reflector.dm:228
    public override async DmTask<object> auto_reflect(obj_projectile proj = null, object pdir = null, turf ploc = null, object pangle = null)
    {
        float incidence = GET_ANGLE_OF_INCIDENCE(rotation_angle, (Dm.num(proj.angle) + 180));
        float new_angle = SIMPLIFY_DEGREES(rotation_angle + incidence);
        await proj.forceMove(loc);
        await proj.set_angle_centered(loc, new_angle);
        return await base.auto_reflect(proj, pdir, ploc, pangle);
    }
}

//BOX

// tg: code/game/objects/structures/reflector.dm:237
public partial class obj_structure_reflector_box : obj_structure_reflector
{
    // tg: code/game/objects/structures/reflector.dm:254
    public override async DmTask<object> auto_reflect(obj_projectile proj = null, object pdir = null, turf ploc = null, object pangle = null)
    {
        await proj.set_angle_centered(loc, rotation_angle);
        return await base.auto_reflect(proj, pdir, ploc, pangle);
    }
}

// tg: code/game/objects/structures/reflector.dm:345
public partial class obj_structure_reflector_wrenched : obj_structure_reflector
{
    // tg: code/game/objects/structures/reflector.dm:347
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);

        Waiting.NoSleep(set_anchored(TRUE));
        return dot;
    }
}

// The AI's and cyborgs' clicks (silicons SI2, D326)
// tg: code/game/objects/structures/reflector.dm:311
public partial class obj_structure_reflector : obj_structure
{
    // tg: code/game/objects/structures/reflector.dm:311
    public override async DmTask<object> attack_robot(mob user = null, object modifiers = null)
    {
        await ui_interact(user);
        return null;
    }
}
