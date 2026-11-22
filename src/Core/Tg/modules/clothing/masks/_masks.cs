// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/masks/_masks.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// /obj/item/clothing/mask whole: equipped, dropped, get_emote_sound, worn_overlays, separate_worn_overlays and
// update_clothes_damaged_state (M4a clothing: masks are put on, taken off and drawn on the wearer); the voice box toggle
// (attack_self), the visor (visor_toggling, update_icon_state) and consume_filter (M4c slice 1: the breath mask's
// internals). The mask subtypes of this file are translated with their mechanics.

// tg: code/modules/clothing/masks/_masks.dm:1
public partial class obj_item_clothing_mask : obj_item_clothing
{
    // tg: code/modules/clothing/masks/_masks.dm:23
    [DmParams("equipper", "slot")]
    public override async DmTask equipped__proc(mob equipper = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(equipper, slot, initial);
        if (!Dm.truthy(Dm.bitand(slot, ITEM_SLOT_MASK)))
            return;
        foreach (object key in Dm.each(emote_sounds))
            RegisterSignal(equipper, COMSIG_MOB_EMOTE_SOUND(key), nameof(get_emote_sound));
    }

    // tg: code/modules/clothing/masks/_masks.dm:30
    [DmParams("dropper")]
    public override async DmTask<object> dropped(mob dropper = null, float? silent = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.dropped(dropper, silent, __extra);
        foreach (object key in Dm.each(emote_sounds))
            UnregisterSignal(dropper, COMSIG_MOB_EMOTE_SOUND(key));
        return dot;
    }

    // tg: code/modules/clothing/masks/_masks.dm:35
    [SignalHandler]
    public virtual void get_emote_sound(mob_living source = null, object key = null, DmList sounds = null)
    {
        object sound_override = get_emote_sound_from_list(emote_sounds[key], source);
        sounds[sound_override] = EMOTE_SOUND_MASK;
    }

    // tg: code/modules/clothing/masks/_masks.dm:46
    public override object worn_overlays(mutable_appearance standing = null, float? isinhands = null, object icon_file = null, float? bodyshape = null)
    {
        isinhands ??= FALSE;
        bodyshape ??= NONE;
        DmList dot = null;
        dot = base.worn_overlays(standing, isinhands, icon_file, bodyshape) as DmList;
        if (Dm.truthy(isinhands) || !Dm.truthy(Dm.bitand(body_parts_covered, HEAD)))
            return dot;
        if (Dm.truthy(damaged_clothes))
            Dm.append(ref dot, mutable_appearance(Dm.resource("icons/effects/item_damage.dmi"), "damagedmask"));
        return dot;
    }

    // tg: code/modules/clothing/masks/_masks.dm:53
    public override async DmTask<object> separate_worn_overlays(mutable_appearance standing = null, mutable_appearance draw_target = null, float? isinhands = null, object icon_file = null, float? bodyshape = null)
    {
        bodyshape ??= NONE;
        DmList dot = null;
        dot = await base.separate_worn_overlays(standing, draw_target, isinhands, icon_file, bodyshape) as DmList;
        if (Dm.truthy(isinhands) || !Dm.truthy(Dm.bitand(body_parts_covered, HEAD)))
            return dot;
        object blood_overlay = await get_blood_overlay("mask", bodyshape);
        if (Dm.truthy(blood_overlay))
            Dm.append(ref dot, blood_overlay);
        return dot;
    }

    // tg: code/modules/clothing/masks/_masks.dm:61
    public override async DmTask update_clothes_damaged_state(float? damaged_state = null)
    {
        damaged_state ??= CLOTHING_DAMAGED;
        await base.update_clothes_damaged_state(damaged_state);
        if (ismob(loc))
        {
            mob M = loc as mob;
            await M.update_worn_mask();
        }
    }

    // tg: code/modules/clothing/masks/_masks.dm:40
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(clothing_flags, VOICEBOX_TOGGLABLE)))
        {
            clothing_flags = Dm.bitxor(clothing_flags, VOICEBOX_DISABLED);
            bool status = !Dm.truthy(Dm.bitand(clothing_flags, VOICEBOX_DISABLED));
            to_chat(user, span_notice(I18n.T("You turn the voice box in [] [].", this, status ? I18n.T("on") : I18n.T("off"))));
        }
        return null;
    }

    //Proc that moves gas/breath masks out of the way, disabling them and allowing pill/food consumption
    // tg: code/modules/clothing/masks/_masks.dm:68
    [DmParams("user")]
    public override void visor_toggling(params object[] __extra)
    {
        base.visor_toggling(__extra);
        if (Dm.truthy(up))
        {
            if (Dm.truthy(adjusted_flags))
                slot_flags = Dm.num(adjusted_flags);
        }
        else
            slot_flags = Dm.initial(this, (obj_item_clothing_mask p) => p.slot_flags);
    }

    // tg: code/modules/clothing/masks/_masks.dm:76
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = null;
        dot = await base.update_icon_state(__extra);
        icon_state = Dm.text("[][]", base_icon_state is var or__1 && Dm.truthy(or__1) ? or__1 : Dm.initial(this, (obj_item_clothing_mask p) => p.post_init_icon_state) is var or__2 && Dm.truthy(or__2) ? or__2 : Dm.initial(this, (obj_item_clothing_mask p) => p.icon_state), Dm.truthy(up) ? "_up" : "");
        return dot;
    }

    /**
     * Proc called in lungs.dm to act if wearing a mask with filters, used to reduce the filters durability, return a changed gas mixture depending on the filter status
     * Arguments:
     * * breath - the gas mixture of the breather
     */
    // tg: code/modules/clothing/masks/_masks.dm:85
    public virtual object consume_filter(datum_gas_mixture breath = null)
    {
        return breath;
    }
}
