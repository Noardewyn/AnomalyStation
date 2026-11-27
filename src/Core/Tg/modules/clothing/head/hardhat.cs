// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/head/hardhat.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: the hard hat's headlamp and the welding hard hat's visor, with its on_saboteur (the SC/FISHER's disruptor, gaps train 7).
// The atmos helmet's bodypart texture and emissive are not translated (M10). Gaps CL1 (D333): the carved pumpkin and the blumpkin
// (their lamp's Halloween screen colour and the dog's lit or unlit fashion; the glow is DV12). The var blocks are generated.

// tg: code/modules/clothing/head/hardhat.dm:9
public partial class obj_item_clothing_head_utility_hardhat : obj_item_clothing_head_utility
{
    // tg: code/modules/clothing/head/hardhat.dm:45
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_update_icon_updates_onmob)));
        return dot;
    }

    // tg: code/modules/clothing/head/hardhat.dm:49
    public virtual async DmTask toggle_helmet_light(mob_living user = null)
    {
        on = Dm.num(!Dm.truthy(on));
        if (Dm.truthy(on))
            await turn_on(user);
        else
            await turn_off(user);
        update_appearance();
    }

    // tg: code/modules/clothing/head/hardhat.dm:57
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = (string)Dm.text("hardhat[]_[]", on, hat_type);
        inhand_icon_state = icon_state;
        return await base.update_icon_state(__extra);
    }

    // tg: code/modules/clothing/head/hardhat.dm:67  (gaps train 7: the SC/FISHER, a crew gun since gaps GU1, reaches it)
    public override async DmTask<object> on_saboteur(datum source = null, object disrupt_duration = null)
    {
        object dot = await base.on_saboteur(source, disrupt_duration);
        if (Dm.truthy(on))
        {
            await toggle_helmet_light();
            return TRUE;
        }
        return dot;
    }

    // tg: code/modules/clothing/head/hardhat.dm:61
    public virtual async DmTask turn_on(mob user = null)
    {
        await set_light_on(TRUE);
    }

    // tg: code/modules/clothing/head/hardhat.dm:64
    public virtual async DmTask turn_off(mob user = null)
    {
        await set_light_on(FALSE);
    }

    // tg: code/modules/clothing/head/hardhat.dm:73
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        await toggle_helmet_light(user as mob_living);
        return null;
    }
}

// tg: code/modules/clothing/head/hardhat.dm:121
public partial class obj_item_clothing_head_utility_hardhat_welding : obj_item_clothing_head_utility_hardhat
{
    // tg: code/modules/clothing/head/hardhat.dm:139
    public override async DmTask<object> attack_self_secondary(mob user = null, object modifiers = null)
    {
        await adjust_visor(user as mob_living);
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/modules/clothing/head/hardhat.dm:143
    public override async DmTask<object> ui_action_click(mob user = null, object actiontype = null)
    {
        if (actiontype is datum_action_item_action_toggle_welding_screen)
        {
            await adjust_visor(user as mob_living);
            return null;
        }
        return await base.ui_action_click(user, actiontype);
    }

    // tg: code/modules/clothing/head/hardhat.dm:149
    public override async DmTask<object> adjust_visor(mob_living user = null)
    {
        object dot = await base.adjust_visor(user);
        if (Dm.truthy(dot))
            playsound(this, Dm.truthy(up) ? SFX_VISOR_UP : SFX_VISOR_DOWN, 50, TRUE);
        return dot;
    }

    // tg: code/modules/clothing/head/hardhat.dm:154
    public override object worn_overlays(mutable_appearance standing = null, float? isinhands = null, object icon_file = null, float? bodyshape = null)
    {
        isinhands ??= FALSE;
        bodyshape ??= NONE;
        DmList dot = base.worn_overlays(standing, isinhands, icon_file, bodyshape) as DmList;
        if (Dm.truthy(isinhands))
            return dot;
        if (!Dm.truthy(up))
            Dm.append(ref dot, mutable_appearance(Dm.resource("icons/mob/clothing/head/utility.dmi"), visor_state));
        return dot;
    }

    // tg: code/modules/clothing/head/hardhat.dm:162
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (!Dm.truthy(up))
            Dm.append(ref dot, visor_state);
        return dot;
    }
}

// tg: code/modules/clothing/head/hardhat.dm:167
public partial class obj_item_clothing_head_utility_hardhat_welding_up : obj_item_clothing_head_utility_hardhat_welding
{
    // tg: code/modules/clothing/head/hardhat.dm:170
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        up = FALSE;
        visor_toggling();
        return dot;
    }
}

// tg: code/modules/clothing/head/hardhat.dm:192
public partial class obj_item_clothing_head_utility_hardhat_welding_white_up : obj_item_clothing_head_utility_hardhat_welding_white
{
    // tg: code/modules/clothing/head/hardhat.dm:195
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        up = FALSE;
        visor_toggling();
        return dot;
    }
}

// tg: code/modules/clothing/head/hardhat.dm:235
public partial class obj_item_clothing_head_utility_hardhat_pumpkinhead : obj_item_clothing_head_utility_hardhat
{
    // tg: code/modules/clothing/head/hardhat.dm:258
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_adjust_fishing_difficulty), 3));
        return dot;
    }

    // tg: code/modules/clothing/head/hardhat.dm:262
    public override async DmTask<object> set_light_on(object new_value = null)
    {
        object dot = await base.set_light_on(new_value);
        if (dot == null)
            return dot;
        if (Dm.truthy(new_value))
            await AddElement(typeof(datum_element_wearable_client_colour), typeof(datum_client_colour_halloween_helmet), ITEM_SLOT_HEAD, HELMET_TRAIT, Dm.named("forced", TRUE));
        else
            RemoveElement(typeof(datum_element_wearable_client_colour), typeof(datum_client_colour_halloween_helmet), ITEM_SLOT_HEAD, HELMET_TRAIT, Dm.named("forced", TRUE));
        await update_icon(UPDATE_OVERLAYS);
        return dot;
    }

    // tg: code/modules/clothing/head/hardhat.dm:272
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        // DEVIATION(DV12): if(light_on) . += emissive_appearance(icon, "carved_pumpkin-emissive", src, alpha = src.alpha): the
        // carved face's glow on the emissive plane is not created (the lamp itself lights the room)
        return dot;
    }

    // tg: code/modules/clothing/head/hardhat.dm:277
    public override object worn_overlays(mutable_appearance standing = null, float? isinhands = null, object icon_file = null, float? bodyshape = null)
    {
        bodyshape ??= NONE;
        object dot = base.worn_overlays(standing, isinhands, icon_file, bodyshape);
        // DEVIATION(DV12): if(light_on && !isinhands) . += emissive_appearance(icon_file, "carved_pumpkin-emissive", src, alpha =
        // src.alpha): the worn glow on the emissive plane is not created
        return dot;
    }

    // tg: code/modules/clothing/head/hardhat.dm:282
    public override async DmTask turn_on(mob user = null)
    {
        await base.turn_on(user);
        dog_fashion = typeof(datum_dog_fashion_head_pumpkin_lit);
    }

    // tg: code/modules/clothing/head/hardhat.dm:286
    public override async DmTask turn_off(mob user = null)
    {
        await base.turn_off(user);
        dog_fashion = typeof(datum_dog_fashion_head_pumpkin_unlit);
    }
}

// tg: code/modules/clothing/head/hardhat.dm:290
public partial class obj_item_clothing_head_utility_hardhat_pumpkinhead_blumpkin : obj_item_clothing_head_utility_hardhat_pumpkinhead
{
    // tg: code/modules/clothing/head/hardhat.dm:299
    public override async DmTask turn_on(mob user = null)
    {
        await base.turn_on(user);
        dog_fashion = typeof(datum_dog_fashion_head_blumpkin_lit);
    }

    // tg: code/modules/clothing/head/hardhat.dm:303
    public override async DmTask turn_off(mob user = null)
    {
        await base.turn_off(user);
        dog_fashion = typeof(datum_dog_fashion_head_blumpkin_unlit);
    }
}
