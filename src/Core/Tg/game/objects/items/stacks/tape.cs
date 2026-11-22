// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/stacks/tape.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8 slice D (surgical tape sets a gelled bone, bones.dm's tape): the sticky tape's procs and the surgical tape's; gaps SK1 (D333):
// the duct tape's repair of objects and robots on the right button (tape.dm:175), so the file is whole.

// tg: code/game/objects/items/stacks/tape.dm:1
public partial class obj_item_stack_medical_wrap_sticky_tape : obj_item_stack_medical_wrap
{
    // tg: code/game/objects/items/stacks/tape.dm:36
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_cellulose), 5));
    }

    // tg: code/game/objects/items/stacks/tape.dm:39
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        if (user.get_inactive_held_item() == this)
        {
            if (Dm.truthy(is_zero_amount(delete_if_zero: TRUE)))
                return null;
            playsound(user, Dm.resource("sound/items/duct_tape/duct_tape_rip.ogg"), 50, TRUE);
            if (!Dm.truthy(await do_after(user, 1 * SECONDS)))
                return null;
            object new_tape_gag = Dm.@new(tape_gag, this);
            await user.put_in_hands(new_tape_gag as obj_item);
            await use(1);
            to_chat(user, span_notice(I18n.T("You rip off a piece of tape.")));
            playsound(user, Dm.resource("sound/items/duct_tape/duct_tape_snap.ogg"), 50, TRUE);
            return TRUE;
        }
        return await base.attack_hand(user, modifiers, __extra);
    }

    // tg: code/game/objects/items/stacks/tape.dm:54
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("You could rip a piece off by using an empty hand.")));
        return dot;
    }

    // tg: code/game/objects/items/stacks/tape.dm:58
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!isitem(interacting_with))
            return NONE;
        obj_item target = interacting_with as obj_item;

        if (Dm.eq((target.get_embed() as datum)?.type, conferred_embed))
        {
            to_chat(user, span_warning(I18n.T("[] is already coated in []!", target, this)));
            return ITEM_INTERACT_BLOCKING;
        }

        user.visible_message(span_notice(I18n.T("[] begins wrapping [] with [].", user, target, this)), span_notice(I18n.T("You begin wrapping [] with [].", target, this)));
        playsound(user, Dm.resource("sound/items/duct_tape/duct_tape_rip.ogg"), 50, TRUE);

        if (!Dm.truthy(await do_after(user, 3 * SECONDS, target: target)))
            return ITEM_INTERACT_BLOCKING;

        playsound(user, Dm.resource("sound/items/duct_tape/duct_tape_snap.ogg"), 50, TRUE);
        await use(1);
        if (istype(target, typeof(obj_item_clothing_gloves_fingerless)))
        {
            var O = Dm.@new<obj_item_clothing_gloves_tackler_offbrand>();
            to_chat(user, span_notice(I18n.T("You turn [] into [] with [].", target, O, this)));
            QDEL_NULL(ref target);
            await user.put_in_hands(O);
            return ITEM_INTERACT_SUCCESS;
        }

        if (Dm.eq((target.get_embed() as datum)?.type, conferred_embed))
        {
            to_chat(user, span_warning(I18n.T("[] is already coated in []!", target, this)));
            return ITEM_INTERACT_BLOCKING;
        }

        target.set_embed(conferred_embed);
        to_chat(user, span_notice(I18n.T("You finish wrapping [] with [].", target, this)));
        target.name = I18n.T("[] []", prefix, target.name);

        if (target is obj_item_grenade sticky_bomb)
            sticky_bomb.sticky = TRUE;

        return ITEM_INTERACT_SUCCESS;
    }
}

// tg: code/game/objects/items/stacks/tape.dm:147
public partial class obj_item_stack_medical_wrap_sticky_tape_surgical : obj_item_stack_medical_wrap_sticky_tape
{
    // tg: code/game/objects/items/stacks/tape.dm:158
    public override object get_surgery_tool_overlay(object tray_extended = null)
    {
        return "tape" + (Dm.truthy(tray_extended) ? "" : "_out");
    }
}

// tg: code/game/objects/items/stacks/tape.dm:159
public partial class obj_item_stack_medical_wrap_sticky_tape_duct : obj_item_stack_medical_wrap_sticky_tape
{
    // tg: code/game/objects/items/stacks/tape.dm:175
    public override async DmTask<object> interact_with_atom_secondary(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (object_repair_value == 0)
            return NONE;

        if (issilicon(interacting_with))
        {
            var robotic_pal = interacting_with as mob_living_silicon;
            object robot_is_damaged = robotic_pal.get_brute_loss();

            if (!Dm.truthy(robot_is_damaged))
            {
                user.balloon_alert(user, I18n.T("[] is not damaged!", robotic_pal));
                return ITEM_INTERACT_BLOCKING;
            }

            user.visible_message(span_notice(I18n.T("[] begins repairing [] with [].", user, robotic_pal, this)), span_notice(I18n.T("You begin repairing [] with [].", robotic_pal, this)));
            playsound(user, Dm.resource("sound/items/duct_tape/duct_tape_rip.ogg"), 50, TRUE);

            if (!Dm.truthy(await do_after(user, 3 * SECONDS, target: robotic_pal)))
                return ITEM_INTERACT_BLOCKING;

            await robotic_pal.adjust_brute_loss(-object_repair_value);
            await use(1);
            to_chat(user, span_notice(I18n.T("You finish repairing [] with [].", interacting_with, this)));
            return ITEM_INTERACT_SUCCESS;
        }

        if (!isobj(interacting_with) || iseffect(interacting_with))
            return NONE;

        if (HAS_TRAIT(interacting_with, TRAIT_DUCT_TAPE_UNREPAIRABLE))
        {
            user.balloon_alert(user, I18n.T("cannot be repaired with duct tape!"));
            return ITEM_INTERACT_BLOCKING;
        }

        // tg types the target /obj/item but reaches any /obj here (rules 3.4: wider stored values)
        var object_to_repair = interacting_with as obj;
        bool object_is_damaged = Dm.num(object_to_repair.get_integrity()) < Dm.num(object_to_repair.max_integrity);

        if (!object_is_damaged)
        {
            user.balloon_alert(user, I18n.T("[] is not damaged!", object_to_repair));
            return ITEM_INTERACT_BLOCKING;
        }

        user.visible_message(span_notice(I18n.T("[] begins repairing [] with [].", user, object_to_repair, this)), span_notice(I18n.T("You begin repairing [] with [].", object_to_repair, this)));
        playsound(user, Dm.resource("sound/items/duct_tape/duct_tape_rip.ogg"), 50, TRUE);

        if (!Dm.truthy(await do_after(user, 3 * SECONDS, target: object_to_repair)))
            return ITEM_INTERACT_BLOCKING;

        if (object_to_repair is obj_item_clothing clothing_to_repair)
            await clothing_to_repair.repair();
        else
            await object_to_repair.repair_damage(object_repair_value);

        await use(1);
        to_chat(user, span_notice(I18n.T("You finish repairing [] with [].", interacting_with, this)));
        return ITEM_INTERACT_SUCCESS;
    }
}
