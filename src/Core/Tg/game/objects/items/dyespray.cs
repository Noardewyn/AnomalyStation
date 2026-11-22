// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/dyespray.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps OD2 (D333): the file whole. The hair dye spray, used in hand or on a human: tgui's prompts pick the hair or the facial hair (or
// a dyeable external body part), a gradient pattern and a colour; after three seconds the gradient is drawn on the hair (or after
// four and a half the body part is dyed, or un-dyed), with the spray's sound. The var block is generated (D111).

// tg: code/game/objects/items/dyespray.dm:1
public partial class obj_item_dyespray : obj_item
{
    // tg: code/game/objects/items/dyespray.dm:8
    [DmParams("user")]
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        await dye(user, user);
        return null;
    }

    // tg: code/game/objects/items/dyespray.dm:11
    public override async DmTask<object> pre_attack(atom target = null, mob_living user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        await dye(target as mob, user);
        return await base.pre_attack(target, user, modifiers, attack_modifiers);
    }

    /**
     * Applies a gradient and a gradient color to a mob.
     *
     * Arguments:
     * * target - The mob who we will apply the gradient and gradient color to.
     */

    // tg: code/game/objects/items/dyespray.dm:22
    public virtual async DmTask dye(mob target = null, mob user = null)
    {
        if (!ishuman(target))
            return;
        mob_living_carbon_human human_target = target as mob_living_carbon_human;
        DmList dyables = Dm.list(I18n.T("Hair"), I18n.T("Facial Hair"));
        foreach (obj_item_organ organ in Dm.each_cast<obj_item_organ>(human_target.organs))
        {
            if (!(organ.bodypart_overlay is datum_bodypart_overlay_mutant))
                continue;
            datum_bodypart_overlay_mutant overlay = organ.bodypart_overlay as datum_bodypart_overlay_mutant;
            if (Dm.truthy(overlay.dyable) && Dm.truthy(overlay.sprite_datum.color_src))
            {
                Dm.append(ref dyables, Dm.list(I18n.T("External Body Parts")));
                break;
            }
        }
        obj_item_bodypart_head head = human_target.get_bodypart(BODY_ZONE_HEAD) as obj_item_bodypart_head;
        if (head == null || !Dm.truthy(Dm.bitand(head.head_flags, HEAD_HAIR)) || HAS_TRAIT(human_target, TRAIT_BALD))
            Dm.remove(ref dyables, I18n.T("Hair"));
        if (head == null || !Dm.truthy(Dm.bitand(head.head_flags, HEAD_FACIAL_HAIR)) || HAS_TRAIT(human_target, TRAIT_SHAVED))
            Dm.remove(ref dyables, I18n.T("Facial Hair"));
        if (!Dm.truthy(length(dyables)))
        {
            if (target != user)
                to_chat(user, span_warning(I18n.T("[] doesn't have anything that can be dyed.", human_target)));
            else
                to_chat(user, span_warning(I18n.T("You have nothing to dye.")));
            return;
        }
        object what_to_dye = await tgui_alert(user, I18n.T("What do you want to dye?"), I18n.T("Character Preference"), dyables);
        if (!Dm.truthy(what_to_dye) || !Dm.truthy(await user.can_perform_action(this, NEED_DEXTERITY)))
            return;

        if (Dm.eq(what_to_dye, I18n.T("External Body Parts")))
        {
            await dye_organ(target as mob_living_carbon_human, user);
            return;
        }

        DmList choices = Dm.eq(what_to_dye, I18n.T("Hair")) ? SSaccessories.hair_gradients_list : SSaccessories.facial_hair_gradients_list;
        object new_grad_style = await tgui_input_list(user, I18n.T("Choose a color pattern"), I18n.T("Character Preference"), choices);
        if (new_grad_style == null)
            return;
        if (!Dm.truthy(await user.can_perform_action(this, NEED_DEXTERITY)))
            return;

        string hair_key = Dm.eq(what_to_dye, I18n.T("Hair")) ? GRADIENT_HAIR_KEY : GRADIENT_FACIAL_HAIR_KEY;
        object new_grad_color = await tgui_color_picker(user, I18n.T("Choose a secondary hair color:"), I18n.T("Character Preference"), human_target.get_hair_gradient_color(hair_key));
        if (!Dm.truthy(new_grad_color) || !Dm.truthy(await user.can_perform_action(this, NEED_DEXTERITY)) || !Dm.truthy(await target.IsReachableBy(user)))
            return;

        to_chat(user, span_notice(I18n.T("You start applying the hair dye...")));
        if (!Dm.truthy(await do_after(user, 3 * SECONDS, target)))
            return;
        if (Dm.eq(what_to_dye, I18n.T("Hair")))
        {
            human_target.set_hair_gradient_style(new_grad_style, update: FALSE);
            human_target.set_hair_gradient_color(new_grad_color, update: TRUE);
        }
        else
        {
            human_target.set_facial_hair_gradient_style(new_grad_style, update: FALSE);
            human_target.set_facial_hair_gradient_color(new_grad_color, update: TRUE);
        }
        playsound(this, Dm.resource("sound/effects/spray.ogg"), 10, vary: TRUE);
    }

    // tg: code/game/objects/items/dyespray.dm:76
    public virtual async DmTask dye_organ(mob_living_carbon_human target = null, mob user = null)
    {
        DmList dyables = Dm.list();
        DmList choices = Dm.list();
        foreach (obj_item_organ organ in Dm.each_cast<obj_item_organ>(target.organs))
        {
            if (!(organ.bodypart_overlay is datum_bodypart_overlay_mutant))
                continue;
            datum_bodypart_overlay_mutant overlay__1 = organ.bodypart_overlay as datum_bodypart_overlay_mutant;
            if (Dm.truthy(overlay__1.dyable) && Dm.truthy(overlay__1.sprite_datum.color_src))
            {
                object choice_name = full_capitalize(organ.name);
                dyables[choice_name] = organ;
                Dm.append(ref choices, choice_name);
            }
        }
        if (!Dm.truthy(length(choices)))
            return;
        object what_to_dye = await tgui_alert(user, I18n.T("What do you want to dye?"), I18n.T("Character Preference"), choices);
        if (!Dm.truthy(what_to_dye) || !Dm.truthy(await user.can_perform_action(this, NEED_DEXTERITY)))
            return;

        obj_item_organ selected = dyables[what_to_dye] as obj_item_organ;
        if (QDELETED(selected) || !Dm.@in(selected, target.organs))
            return;

        datum_bodypart_overlay_mutant overlay = selected.bodypart_overlay as datum_bodypart_overlay_mutant;
        if (Dm.truthy(overlay.dye_color))
        {
            object remove_dye = await tgui_alert(user, I18n.T("Do you want to un-dye []?", selected), I18n.T("Character Preference"), Dm.list(I18n.T("Yes"), I18n.T("No")));
            if (remove_dye == null || !Dm.truthy(await user.can_perform_action(this, NEED_DEXTERITY)))
                return;
            if (QDELETED(selected) || !Dm.@in(selected, target.organs))
                return;
            if (Dm.eq(remove_dye, I18n.T("Yes")))
            {
                await overlay.set_dye_color(null, selected);
                return;
            }
        }

        object default_color = Dm.truthy(overlay.dye_color) ? overlay.dye_color : overlay.draw_color;
        object new_color = await tgui_color_picker(user, I18n.T("Choose a color for []:", selected), I18n.T("Character Preference"), default_color);
        if (new_color == null || Dm.eq(new_color, default_color) || !Dm.truthy(await user.can_perform_action(this, NEED_DEXTERITY)))
            return;
        if (QDELETED(selected) || !Dm.@in(selected, target.organs))
            return;
        if (!Dm.truthy(await do_after(user, 4.5f * SECONDS, target)))
            return;
        if (QDELETED(selected) || !Dm.@in(selected, target.organs))
            return;
        await overlay.set_dye_color(new_color, selected);
    }
}
