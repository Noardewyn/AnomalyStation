// Translated from tgstation (snapshot 1d99bd7f): code/datums/actions/items/toggles.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4c slice 3: the toggles the M4c items use: the generic "Toggle [item]" (the magboots), the space suit's thermal
// regulator, and the jetpack's two buttons (the jetpack itself is M4c slice 6). M7 slice T3: the night vision toggle (the night
// mesons). Gaps P1: the HUD implant's toggle (:80). The other organ and wearable HUD toggles (:89-156) wait for their items. The var-only
// toggles are generated.

// tg: code/datums/actions/items/toggles.dm:1
public partial class datum_action_item_action_toggle : datum_action_item_action
{
    // tg: code/datums/actions/items/toggles.dm:3
    [DmParams("Target")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        obj_item item_target = target as obj_item;
        name = I18n.T("Toggle []", item_target.name);
    }
}

// tg: code/datums/actions/items/toggles.dm:41
public partial class datum_action_item_action_toggle_spacesuit : datum_action_item_action
{
    // tg: code/datums/actions/items/toggles.dm:46
    public override void apply_button_icon(atom_movable_screen_movable_action_button button = null, float? force = null)
    {
        obj_item_clothing_suit_space suit = target as obj_item_clothing_suit_space;
        if (suit is obj_item_clothing_suit_space)
            button_icon_state = Dm.text("thermal_[]", Dm.truthy(suit.thermal_on) ? "on" : "off");

        base.apply_button_icon(button, force);
    }
}

// tg: code/datums/actions/items/toggles.dm:71
public partial class datum_action_item_action_jetpack_stabilization : datum_action_item_action
{
    // tg: code/datums/actions/items/toggles.dm:74
    public override async DmTask<object> IsAvailable(float? feedback = null)
    {
        feedback ??= FALSE;
        obj_item_tank_jetpack linked_jetpack = target as obj_item_tank_jetpack;
        if (!(linked_jetpack is obj_item_tank_jetpack) || !Dm.truthy(linked_jetpack.on))
            return FALSE;
        return await base.IsAvailable(feedback);
    }
}

// tg: code/datums/actions/items/toggles.dm:115
public partial class datum_action_item_action_toggle_wearable_hud : datum_action_item_action
{
    // tg: code/datums/actions/items/toggles.dm:118
    public override async DmTask<object> do_effect(object trigger_flags = null)
    {
        obj_item_clothing_glasses_hud hud_display = (obj_item_clothing_glasses_hud)target;
        hud_display.toggle_hud_display(owner as mob_living_carbon);
        return TRUE;
    }
}

// tg: code/datums/actions/items/toggles.dm:123
public partial class datum_action_item_action_toggle_nv : datum_action_item_action
{
    // tg: code/datums/actions/items/toggles.dm:128
    [DmParams("target")]
    public override async DmTask New(params object[] __extra)
    {
        obj_item_clothing_glasses target = Dm.arg<obj_item_clothing_glasses>(__extra, 0);
        await base.New(Dm.forward(__extra, target));
        await target.AddElement(typeof(datum_element_update_icon_updates_onmob));
    }

    // tg: code/datums/actions/items/toggles.dm:132
    public override async DmTask<object> do_effect(object trigger_flags = null)
    {
        if (!istype(target, typeof(obj_item_clothing_glasses)))
            return await base.do_effect(trigger_flags);
        obj_item_clothing_glasses goggles = (obj_item_clothing_glasses)target;
        mob holder = goggles.loc as mob;
        if (!(holder is mob) || !Dm.eq(holder.get_slot_by_item(goggles), ITEM_SLOT_EYES))
            holder = null;
        if (Dm.truthy(stored_cutoffs))
        {
            goggles.color_cutoffs = stored_cutoffs as DmList;
            goggles.flash_protect = FLASH_PROTECTION_SENSITIVE;
            stored_cutoffs = null;
            if (Dm.truthy(stored_colour))
                goggles.change_glass_color(stored_colour);
            playsound(goggles, Dm.resource("sound/items/night_vision_on.ogg"), 30, TRUE, -3);
        }
        else
        {
            stored_cutoffs = goggles.color_cutoffs;
            stored_colour = goggles.glass_colour_type;
            goggles.color_cutoffs = Dm.list();
            goggles.flash_protect = FLASH_PROTECTION_NONE;
            if (Dm.truthy(stored_colour))
                goggles.change_glass_color(null);
            playsound(goggles, Dm.resource("sound/machines/click.ogg"), 30, TRUE, -3);
        }
        holder?.update_sight();
        GhostNightVision.Sync(this);
        goggles.update_appearance();
        return TRUE;
    }
}


// Gaps package P1: the HUD implant's toggle (the eye implants of augments_eyes.dm).
// tg: code/datums/actions/items/toggles.dm:80
public partial class datum_action_item_action_organ_action_toggle_hud : datum_action_item_action_organ_action
{
    // tg: code/datums/actions/items/toggles.dm:84
    public override async DmTask<object> do_effect(object trigger_flags = null)
    {
        obj_item_organ_cyberimp_eyes_hud hud_implant = target as obj_item_organ_cyberimp_eyes_hud;
        await hud_implant.toggle_hud(owner as mob_living_carbon_human);
        return TRUE;
    }
}
