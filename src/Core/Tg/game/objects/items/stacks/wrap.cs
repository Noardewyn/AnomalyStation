// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/stacks/wrap.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (D325: the disposal system's mail): wrapping paper (gift wrap for a package), the package wrapper (an item, a closet
// or a portable atmospherics machine wrapped into a parcel the mail sorting can route) and the cardboard tube left when a roll runs out.

/*
 * Wrapping Paper
 */

// tg: code/game/objects/items/stacks/wrap.dm:7
public partial class obj_item_stack_wrapping_paper : obj_item_stack
{
    // tg: code/game/objects/items/stacks/wrap.dm:25
    public object Initialize__d0(object mapload = null, params object[] __extra) // the earlier definition of two (rules 7.5); the later one is :37
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_CUSTOM_TAP_SOUND, INNATE_TRAIT);
        return dot;
    }

    // tg: code/game/objects/items/stacks/wrap.dm:29
    public override async DmTask<object> attack__proc(mob_living target_mob = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        object dot = await base.attack__proc(target_mob, user, modifiers, attack_modifiers, __extra);
        user.visible_message(
            span_warning(I18n.T("[] baps [] on the head with []!", user, target_mob, this)),
            span_warning(I18n.T("You bap [] on the head with []!", target_mob, this)));
        await target_mob.add_mood_event("roll", typeof(datum_mood_event_bapped));
        return dot;
    }

    // tg: code/game/objects/items/stacks/wrap.dm:37
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = Initialize__d0(mapload, __extra);
        if (!Dm.truthy(greyscale_colors))
        {
            //Generate random valid colors for paper and ribbon
            string generated_base_color = "#" + random_color();
            string generated_ribbon_color = "#" + random_color();
            // tg's rgb2hsv (__HELPERS/colors.dm) is rgb2num(rgb, COLORSPACE_HSV)
            DmList base_hsv = rgb2num(generated_base_color, COLORSPACE_HSV);
            DmList ribbon_hsv = rgb2num(generated_ribbon_color, COLORSPACE_HSV);

            //If colors are too dark, set to original colors
            if (Dm.num(base_hsv[3]) < 50)
                generated_base_color = COLOR_VIBRANT_LIME;
            if (Dm.num(ribbon_hsv[3]) < 50)
                generated_ribbon_color = COLOR_RED;

            //Set layers to these colors, base then ribbon
            Waiting.NoSleep(set_greyscale(colors: Dm.list(generated_base_color, generated_ribbon_color)));
        }
        return dot;
    }

    // tg: code/game/objects/items/stacks/wrap.dm:55
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        object new_base = await tgui_color_picker(user, "", "Select a base color", color);
        object new_ribbon = await tgui_color_picker(user, "", "Select a ribbon color", color);
        if (!Dm.truthy(new_base) || !Dm.truthy(new_ribbon))
            return CLICK_ACTION_BLOCKING;

        await set_greyscale(colors: Dm.list(new_base, new_ribbon));
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/game/objects/items/stacks/wrap.dm:71
    [DmParams("used", "transfer", "check")]
    public override async DmTask<object> use(object used = null, params object[] __extra)
    {
        object transfer = Dm.arg<object>(__extra, 0);
        turf T = get_turf(this);
        object dot = await base.use(used, __extra);
        if (QDELETED(this) && !Dm.truthy(transfer))
            Dm.@new<obj_item_c_tube>(T);
        return dot;
    }
}

/*
 * Package Wrap
 */

// tg: code/game/objects/items/stacks/wrap.dm:86
public partial class obj_item_stack_package_wrap : obj_item_stack
{
    // tg: code/game/objects/items/stacks/wrap.dm:99
    public override async DmTask<object> grind_results()
    {
        await DmTask.CompletedTask;
        return Dm.list(Dm.kv(typeof(datum_reagent_cellulose), 5));
    }

    // tg: code/game/objects/items/stacks/wrap.dm:102
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] begins wrapping []self in \\the []! It looks like [] trying to commit suicide!", user, user.p_them(), this, user.p_theyre())));
        if (Dm.truthy(await use(3)))
        {
            obj_item_delivery_big parcel = Dm.@new<obj_item_delivery_big>(get_turf(user.loc));
            parcel.base_icon_state = "deliverypackage5";
            await parcel.update_icon();
            await user.forceMove(parcel);
            parcel.add_fingerprint(user);
            return OXYLOSS;
        }
        else
        {
            balloon_alert(user, I18n.T("not enough paper!"));
            return SHAME;
        }
    }

    // tg: code/game/objects/items/stacks/wrap.dm:127
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!isobj(interacting_with))
            return NONE;
        if (Dm.truthy((interacting_with as obj).anchored))
            return NONE;

        if (isitem(interacting_with))
        {
            obj_item item = interacting_with as obj_item;
            if (!Dm.truthy(item.can_be_package_wrapped()))
            {
                if (HAS_TRAIT(interacting_with, TRAIT_COMBAT_MODE_SKIP_INTERACTION) && Dm.truthy(user.combat_mode)) // SHOULD_SKIP_INTERACTION (__DEFINES/tools.dm:62)
                    return NONE; // put it in the bag instead of yelling
                balloon_alert(user, I18n.T("can't be wrapped!"));
                return ITEM_INTERACT_BLOCKING;
            }
            if (Dm.truthy(user.is_holding(item)))
            {
                if (!Dm.truthy(await user.dropItemToGround(item)))
                    return ITEM_INTERACT_BLOCKING;
            }
            else if (!isturf(item.loc))
                return ITEM_INTERACT_BLOCKING;
            if (Dm.truthy(await use(1)))
            {
                obj_item_delivery_small parcel = Dm.@new<obj_item_delivery_small>(get_turf(item.loc));
                if (Dm.truthy(await user.Adjacent(item)))
                {
                    parcel.add_fingerprint(user);
                    item.add_fingerprint(user);
                    await user.put_in_hands(parcel);
                }
                await item.forceMove(parcel);
                float size = round(Dm.num(item.w_class));
                if (istype(item, typeof(obj_item_disk)))
                {
                    parcel.base_icon_state = "deliveryfloppy";
                    parcel.name = "floppy disk parcel";
                }
                else
                {
                    parcel.base_icon_state = Dm.text("deliverypackage[]", size);
                    parcel.name = Dm.text("[] parcel", weight_class_to_text(size));
                }
                parcel.update_weight_class(size);
                size = min(size, 5);
                await parcel.update_icon();
            }
            else
                return ITEM_INTERACT_BLOCKING;
        }
        else if (istype(interacting_with, typeof(obj_structure_closet)))
        {
            obj_structure_closet closet = interacting_with as obj_structure_closet;
            if (Dm.truthy(closet.opened))
            {
                balloon_alert(user, I18n.T("can't wrap while open!"));
                return ITEM_INTERACT_BLOCKING;
            }
            if (!Dm.truthy(closet.delivery_icon)) //no delivery icon means unwrappable closet (e.g. body bags)
            {
                balloon_alert(user, I18n.T("can't wrap!"));
                return ITEM_INTERACT_BLOCKING;
            }
            if (Dm.truthy(await use(3)))
            {
                obj_item_delivery_big parcel = Dm.@new<obj_item_delivery_big>(get_turf(closet.loc));
                mob being_pulled_by = closet.pulledby as mob;
                if (being_pulled_by != null)
                    await being_pulled_by.start_pulling(parcel);
                parcel.base_icon_state = closet.delivery_icon as string;
                await parcel.update_icon();
                parcel.drag_slowdown = closet.drag_slowdown;
                await closet.forceMove(parcel);
                parcel.add_fingerprint(user);
                closet.add_fingerprint(user);
            }
            else
            {
                balloon_alert(user, I18n.T("not enough paper!"));
                return ITEM_INTERACT_BLOCKING;
            }
        }
        else if (istype(interacting_with, typeof(obj_machinery_portable_atmospherics)))
        {
            obj_machinery_portable_atmospherics portable_atmospherics = interacting_with as obj_machinery_portable_atmospherics;
            if (Dm.truthy(portable_atmospherics.anchored))
            {
                balloon_alert(user, I18n.T("can't wrap while anchored!"));
                return ITEM_INTERACT_BLOCKING;
            }
            if (Dm.truthy(await use(3)))
            {
                obj_item_delivery_big parcel = Dm.@new<obj_item_delivery_big>(get_turf(portable_atmospherics.loc));
                parcel.base_icon_state = "deliverybox";
                await parcel.update_icon();
                parcel.drag_slowdown = portable_atmospherics.drag_slowdown;
                await portable_atmospherics.forceMove(parcel);
                parcel.add_fingerprint(user);
                portable_atmospherics.add_fingerprint(user);
            }
            else
            {
                balloon_alert(user, I18n.T("not enough paper!"));
                return ITEM_INTERACT_BLOCKING;
            }
        }
        else
        {
            balloon_alert(user, I18n.T("can't wrap!"));
            return ITEM_INTERACT_BLOCKING;
        }

        user.visible_message(span_notice(I18n.T("[] wraps [].", user, interacting_with)));
        user.log_message(Dm.text("has used [] on []", name, key_name(interacting_with)), LOG_ATTACK, color: "blue");
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/stacks/wrap.dm:211
    [DmParams("used", "transfer", "check")]
    public override async DmTask<object> use(object used = null, params object[] __extra)
    {
        object transfer = Dm.arg<object>(__extra, 0) ?? FALSE;
        turf T = get_turf(this);
        object dot = await base.use(used, __extra);
        if (QDELETED(this) && !Dm.truthy(transfer))
            Dm.@new<obj_item_c_tube>(T);
        return dot;
    }
}

// tg: code/game/objects/items/stacks/wrap.dm:115
public partial class obj_item : obj
{
    //can the item be wrapped with package wrapper into a delivery package
    // tg: code/game/objects/items/stacks/wrap.dm:115  (no live ancestor declares it: the root slot)
    public virtual object can_be_package_wrapped()
    {
        return TRUE;
    }
}

// tg: code/game/objects/items/stacks/wrap.dm:118  (the earliest .dme file that declares the type, rules 2.3)
[DmPath("/obj/item/storage")]
public partial class obj_item_storage : obj_item
{
    public override object can_be_package_wrapped()
    {
        return FALSE;
    }
}

// tg: code/game/objects/items/stacks/wrap.dm:121
[DmPath("/obj/item/storage/box")]
public partial class obj_item_storage_box : obj_item_storage
{
    public override object can_be_package_wrapped()
    {
        return TRUE;
    }
}

// tg: code/game/objects/items/stacks/wrap.dm:124
public partial class obj_item_delivery : obj_item
{
    public override object can_be_package_wrapped()
    {
        return FALSE;
    }
}

// tg: code/game/objects/items/stacks/wrap.dm:226
public partial class obj_item_c_tube : obj_item
{
    // tg: code/game/objects/items/stacks/wrap.dm:238
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_CUSTOM_TAP_SOUND, INNATE_TRAIT);
        return dot;
    }

    // tg: code/game/objects/items/stacks/wrap.dm:242
    public override async DmTask<object> attack__proc(mob_living target_mob = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        object dot = await base.attack__proc(target_mob, user, modifiers, attack_modifiers, __extra);
        user.visible_message(
            span_warning(I18n.T("[] baps [] on the head with []!", user, target_mob, this)),
            span_warning(I18n.T("You bap [] on the head with []!", target_mob, this)));
        await target_mob.add_mood_event("roll", typeof(datum_mood_event_bapped));
        return dot;
    }
}
