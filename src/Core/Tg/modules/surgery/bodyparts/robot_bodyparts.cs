// Translated from tg code/modules/surgery/bodyparts/robot_bodyparts.dm (snapshot 1d99bd7f).
// Gaps package P1 (D298): the cybernetic limbs the augmentation operation attaches (their render keys, EMP effects, the chest's
// power cell and wiring and the head's flash eyes of a cyborg's assembly, the all-robotic body's resistances); whole file.
using System;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

public partial class obj_item_bodypart_arm_left_robot : obj_item_bodypart_arm_left
{
    // tg: code/modules/surgery/bodyparts/robot_bodyparts.dm:50
    public override object generate_icon_key()
    {
        object dot = base.generate_icon_key();
        if (Dm.eq(limb_id, BODYPART_ID_ROBOTIC))
            Dm.append(ref dot, Dm.truthy(should_draw_greyscale) ? icon_greyscale : icon_static);
        return dot;
    }
}

public partial class obj_item_bodypart_arm_right_robot : obj_item_bodypart_arm_right
{
    // tg: code/modules/surgery/bodyparts/robot_bodyparts.dm:92
    public override object generate_icon_key()
    {
        object dot = base.generate_icon_key();
        if (Dm.eq(limb_id, BODYPART_ID_ROBOTIC))
            Dm.append(ref dot, Dm.truthy(should_draw_greyscale) ? icon_greyscale : icon_static);
        return dot;
    }
}

public partial class obj_item_bodypart_leg_left_robot : obj_item_bodypart_leg_left
{
    // tg: code/modules/surgery/bodyparts/robot_bodyparts.dm:135
    public override object generate_icon_key()
    {
        object dot = base.generate_icon_key();
        if (Dm.eq(limb_id, BODYPART_ID_ROBOTIC))
            Dm.append(ref dot, Dm.truthy(should_draw_greyscale) ? icon_greyscale : icon_static);
        return dot;
    }

    // tg: code/modules/surgery/bodyparts/robot_bodyparts.dm:140
    public override async DmTask<object> emp_effect(object severity = null, object protection = null)
    {
        object dot = await base.emp_effect(severity, protection);
        if (!Dm.truthy(dot) || owner == null)
            return dot;
        if (prob(80 / Dm.num(severity)))
        {
            if (Dm.eq(owner.body_position, LYING_DOWN)) // So the message isn't duplicated. If they were stunned beforehand by something else, then the message not showing makes more sense anyways.
                to_chat(owner, span_danger(I18n.T("Your [] suddenly malfunctions!", plaintext_zone)));
            else
                to_chat(owner, span_danger(I18n.T("As your [] suddenly malfunctions, it causes you to fall to the ground!", plaintext_zone)));
            await owner.Knockdown(AUGGED_LEG_EMP_KNOCKDOWN_TIME);
        }
        await owner.adjust_staggered(STAGGERED_SLOWDOWN_LENGTH * 3 / Dm.num(severity));
        if (owner.client != null)
        {
            owner.client.move_delay = Math.Max(owner.client.move_delay, Dm.num(world.time));
            owner.client.move_delay += 0.6f * SECONDS / Dm.num(severity);
        }
        return dot;
    }
}

public partial class obj_item_bodypart_leg_right_robot : obj_item_bodypart_leg_right
{
    // tg: code/modules/surgery/bodyparts/robot_bodyparts.dm:192
    public override object generate_icon_key()
    {
        object dot = base.generate_icon_key();
        if (Dm.eq(limb_id, BODYPART_ID_ROBOTIC))
            Dm.append(ref dot, Dm.truthy(should_draw_greyscale) ? icon_greyscale : icon_static);
        return dot;
    }

    // tg: code/modules/surgery/bodyparts/robot_bodyparts.dm:197
    public override async DmTask<object> emp_effect(object severity = null, object protection = null)
    {
        object dot = await base.emp_effect(severity, protection);
        if (!Dm.truthy(dot) || owner == null)
            return dot;
        if (prob(80 / Dm.num(severity)))
        {
            if (Dm.eq(owner.body_position, LYING_DOWN)) // So the message isn't duplicated. If they were stunned beforehand by something else, then the message not showing makes more sense anyways.
                to_chat(owner, span_danger(I18n.T("Your [] suddenly malfunctions!", plaintext_zone)));
            else
                to_chat(owner, span_danger(I18n.T("As your [] suddenly malfunctions, it causes you to fall to the ground!", plaintext_zone)));
            await owner.Knockdown(AUGGED_LEG_EMP_KNOCKDOWN_TIME);
        }
        await owner.adjust_staggered(STAGGERED_SLOWDOWN_LENGTH * 3 / Dm.num(severity));
        if (owner.client != null)
        {
            owner.client.move_delay = Math.Max(owner.client.move_delay, Dm.num(world.time));
            owner.client.move_delay += 0.6f * SECONDS / Dm.num(severity);
        }
        return dot;
    }
}

public partial class obj_item_bodypart_chest_robot : obj_item_bodypart_chest
{
    // tg: code/modules/surgery/bodyparts/robot_bodyparts.dm:252
    public override object generate_icon_key()
    {
        object dot = base.generate_icon_key();
        // When we reskin cybernetic limbs, we solely change their icon, nothing else
        // So we need to include the relevant icon in the cache key
        if (Dm.eq(limb_id, BODYPART_ID_ROBOTIC))
            Dm.append(ref dot, Dm.truthy(should_draw_greyscale) ? icon_greyscale : icon_static);
        return dot;
    }

    // tg: code/modules/surgery/bodyparts/robot_bodyparts.dm:259
    public override async DmTask<object> emp_effect(object severity = null, object protection = null)
    {
        object dot = await base.emp_effect(severity, protection);
        if (!Dm.truthy(dot) || owner == null)
            return dot;
        float damage_percent_to_max = Dm.num(get_damage()) / max_damage;
        if (damage_percent_to_max >= robotic_emp_paralyze_damage_percent_threshold)
        {
            to_chat(owner, span_danger(I18n.T("Your []'s logic boards temporarily become unresponsive!", plaintext_zone)));
            await owner.Stun(AUGGED_CHEST_EMP_STUN_TIME / Dm.num(severity));
        }
        await owner.adjust_jitter(AUGGED_CHEST_EMP_SHAKE_TIME / Dm.num(severity));
        return dot;
    }

    // tg: code/modules/surgery/bodyparts/robot_bodyparts.dm:269
    public override object get_cell(atom_movable @interface = null, mob user = null)
    {
        return cell;
    }

    // tg: code/modules/surgery/bodyparts/robot_bodyparts.dm:272
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (gone == cell)
            cell = null;
        return dot;
    }

    // tg: code/modules/surgery/bodyparts/robot_bodyparts.dm:277
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref cell);
        UnregisterSignal(this, COMSIG_BODYPART_ATTACHED);
        return base.Destroy(force);
    }

    // tg: code/modules/surgery/bodyparts/robot_bodyparts.dm:282
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        RegisterSignal(this, COMSIG_BODYPART_ATTACHED, nameof(on_attached));
        RegisterSignal(this, COMSIG_BODYPART_REMOVED, nameof(on_detached));
        return dot;
    }

    // tg: code/modules/surgery/bodyparts/robot_bodyparts.dm:287
    [SignalHandler]
    public virtual void on_attached(obj_item_bodypart_chest_robot this_bodypart = null, mob_living_carbon_human new_owner = null)
    {
        RegisterSignals(new_owner, Dm.list(COMSIG_CARBON_POST_ATTACH_LIMB, COMSIG_CARBON_POST_REMOVE_LIMB), nameof(check_limbs));
    }

    // tg: code/modules/surgery/bodyparts/robot_bodyparts.dm:292
    [SignalHandler]
    public virtual void on_detached(obj_item_bodypart_chest_robot this_bodypart = null, mob_living_carbon_human old_owner = null)
    {
        UnregisterSignal(old_owner, Dm.list(COMSIG_CARBON_POST_ATTACH_LIMB, COMSIG_CARBON_POST_REMOVE_LIMB));
    }

    // tg: code/modules/surgery/bodyparts/robot_bodyparts.dm:297
    [SignalHandler]
    public virtual void check_limbs()
    {
        bool all_robotic = true;
        foreach (obj_item_bodypart part in Dm.each<obj_item_bodypart>(owner.get_bodyparts()))
            all_robotic = all_robotic && Dm.truthy(IS_ROBOTIC_LIMB(part));

        if (all_robotic)
            owner.add_traits(Dm.list(TRAIT_RESISTCOLD, TRAIT_RESISTHEAT, TRAIT_RESISTLOWPRESSURE, TRAIT_RESISTHIGHPRESSURE), AUGMENTATION_TRAIT);
        else
            owner.remove_traits(Dm.list(TRAIT_RESISTCOLD, TRAIT_RESISTHEAT, TRAIT_RESISTLOWPRESSURE, TRAIT_RESISTHIGHPRESSURE), AUGMENTATION_TRAIT);
    }

    // tg: code/modules/surgery/bodyparts/robot_bodyparts.dm:319
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is obj_item_stock_parts_power_store_cell)
        {
            if (cell != null)
            {
                to_chat(user, span_warning(I18n.T("A cell is already present in []!", this)));
                return ITEM_INTERACT_BLOCKING;
            }
            if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
                return ITEM_INTERACT_BLOCKING;
            cell = (obj_item_stock_parts_power_store)tool;
            to_chat(user, span_notice(I18n.T("You insert [] into [].", cell, this)));
            return ITEM_INTERACT_SUCCESS;
        }

        if (tool is obj_item_stack_cable_coil coil)
        {
            if (Dm.truthy(wired))
            {
                to_chat(user, span_warning(I18n.T("[] is already wired up!", this)));
                return ITEM_INTERACT_BLOCKING;
            }
            if (!Dm.truthy(await coil.use(1)))
            {
                to_chat(user, span_warning(I18n.T("You need one length of coil to wire it!")));
                return ITEM_INTERACT_BLOCKING;
            }
            wired = TRUE;
            to_chat(user, span_notice(I18n.T("You wire the cell inside of [].", this)));
            return ITEM_INTERACT_SUCCESS;
        }
        return NONE;
    }

    // tg: code/modules/surgery/bodyparts/robot_bodyparts.dm:343
    public override async DmTask<object> wirecutter_act(mob_living user = null, obj_item tool = null)
    {
        object dot = await base.wirecutter_act(user, tool);
        if (!Dm.truthy(wired))
            return dot;
        dot = TRUE;
        tool.play_tool_sound(this);
        to_chat(user, span_notice(I18n.T("You cut the wires out of [].", this)));
        Dm.@new<obj_item_stack_cable_coil>(drop_location(), 1);
        wired = FALSE;
        return dot;
    }

    // tg: code/modules/surgery/bodyparts/robot_bodyparts.dm:353
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        await base.screwdriver_act(user, tool);
        object dot = TRUE;
        if (cell == null)
        {
            to_chat(user, span_warning(I18n.T("There's no power cell installed in []!", this)));
            return dot;
        }
        tool.play_tool_sound(this);
        to_chat(user, span_notice(I18n.T("Remove [] from [].", cell, this)));
        await cell.forceMove(drop_location() as atom);
        return dot;
    }

    // tg: code/modules/surgery/bodyparts/robot_bodyparts.dm:363
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (cell != null)
            Dm.append(ref dot, I18n.T("It has a [] inserted.\n\n\t\t[]", cell, span_info(I18n.T("You can use a <b>screwdriver</b> to remove [].", cell))));
        else
            Dm.append(ref dot, span_info(I18n.T("It has an empty port for a <b>power cell</b>.")));
        if (Dm.truthy(wired))
            Dm.append(ref dot, I18n.T("Its all wired up[].\n", cell != null ? I18n.T(" and ready for usage") : "") + span_info(I18n.T("You can use <b>wirecutters</b> to remove the wiring.")));
        else
            Dm.append(ref dot, span_info(I18n.T("It has a couple spots that still need to be <b>wired</b>.")));
        return dot;
    }

    // tg: code/modules/surgery/bodyparts/robot_bodyparts.dm:376
    public override async DmTask drop_organs(mob user = null, object violent_removal = null)
    {
        atom drop_loc = drop_location() as atom;
        if (Dm.truthy(wired))
        {
            Dm.@new<obj_item_stack_cable_coil>(drop_loc, 1);
            wired = FALSE;
        }
        if (cell != null)
            await cell.forceMove(drop_loc);
        await base.drop_organs(user, violent_removal);
    }
}

public partial class obj_item_bodypart_head_robot : obj_item_bodypart_head
{
    // tg: code/modules/surgery/bodyparts/robot_bodyparts.dm:423
    public override object generate_icon_key()
    {
        object dot = base.generate_icon_key();
        if (Dm.eq(limb_id, BODYPART_ID_ROBOTIC))
            Dm.append(ref dot, Dm.truthy(should_draw_greyscale) ? icon_greyscale : icon_static);
        return dot;
    }

    // tg: code/modules/surgery/bodyparts/robot_bodyparts.dm:429
    public override async DmTask<object> emp_effect(object severity = null, object protection = null)
    {
        object dot = await base.emp_effect(severity, protection);
        if (!Dm.truthy(dot) || owner == null)
            return dot;
        to_chat(owner, span_danger(I18n.T("Your [] hurts...", plaintext_zone)));
        // QDEL_IN(owner.add_client_colour(/datum/client_colour/malfunction, HEAD_TRAIT), AUGGED_HEAD_EMP_GLITCH_DURATION / severity), inline (rules 11.1 (f))
        object colour = owner.add_client_colour(typeof(datum_client_colour_malfunction), HEAD_TRAIT);
        float time = AUGGED_HEAD_EMP_GLITCH_DURATION / Dm.num(severity);
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), time > GC_FILTER_QUEUE ? (object)WEAKREF(colour as datum) : colour), time, file: "code/modules/surgery/bodyparts/robot_bodyparts.dm", line: 434);
        await owner.adjust_confusion(AUGGED_HEAD_EMP_GLITCH_DURATION / Dm.num(severity));
        return dot;
    }

    // tg: code/modules/surgery/bodyparts/robot_bodyparts.dm:437
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (gone == flash1)
            flash1 = null;
        if (gone == flash2)
            flash2 = null;
        return dot;
    }

    // tg: code/modules/surgery/bodyparts/robot_bodyparts.dm:444
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref flash1);
        QDEL_NULL(ref flash2);
        return base.Destroy(force);
    }

    // tg: code/modules/surgery/bodyparts/robot_bodyparts.dm:449
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (flash1 == null && flash2 == null)
            Dm.append(ref dot, span_info(I18n.T("It has two empty eye sockets for <b>flashes</b>.")));
        else
        {
            bool single_flash = false;
            if (flash1 == null || flash2 == null)
            {
                single_flash = true;
                Dm.append(ref dot, I18n.T("One of its eye sockets is currently occupied by a flash.\n\n\t\t\t[]", span_info(I18n.T("It has an empty eye socket for another <b>flash</b>."))));
            }
            else
                Dm.append(ref dot, I18n.T("It has two eye sockets occupied by flashes."));
            Dm.append(ref dot, span_notice(I18n.T("You can remove the seated flash[] with a <b>crowbar</b>.", single_flash ? "" : "es")));
        }
        return dot;
    }

    // tg: code/modules/surgery/bodyparts/robot_bodyparts.dm:463
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is not obj_item_assembly_flash_handheld flash)
            return NONE;

        if (flash1 != null && flash2 != null)
        {
            to_chat(user, span_warning(I18n.T("[] already has both flash-eyes present!", this)));
            return ITEM_INTERACT_BLOCKING;
        }

        if (Dm.truthy(flash.burnt_out))
        {
            to_chat(user, span_warning(I18n.T("You can't use a broken flash!")));
            return ITEM_INTERACT_BLOCKING;
        }

        if (!Dm.truthy(await user.transferItemToLoc(flash, this)))
            return ITEM_INTERACT_BLOCKING;

        if (flash1 != null)
            flash2 = flash;
        else
            flash1 = flash;
        to_chat(user, span_notice(I18n.T("You insert the flash into the eye socket.")));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/surgery/bodyparts/robot_bodyparts.dm:486
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        await base.crowbar_act(user, tool);
        if (flash1 != null || flash2 != null)
        {
            tool.play_tool_sound(this);
            to_chat(user, span_notice(I18n.T("You remove the flash from [].", this)));
            if (flash1 != null)
                await flash1.forceMove(drop_location() as atom);
            if (flash2 != null)
                await flash2.forceMove(drop_location() as atom);
        }
        else
            to_chat(user, span_warning(I18n.T("There is no flash to remove from [].", this)));
        return TRUE;
    }

    // tg: code/modules/surgery/bodyparts/robot_bodyparts.dm:497
    public override async DmTask drop_organs(mob user = null, object violent_removal = null)
    {
        atom drop_loc = drop_location() as atom;
        if (flash1 != null)
            await flash1.forceMove(drop_loc);
        if (flash2 != null)
            await flash2.forceMove(drop_loc);
        await base.drop_organs(user, violent_removal);
    }
}
