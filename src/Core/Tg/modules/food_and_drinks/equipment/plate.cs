// Translated from tgstation (snapshot 1d99bd7f): code/modules/food_and_drinks/equipment/plate.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package H2, D333): the plates and the ceramic shard; the oven tray is a plate. The var blocks are generated (D111).

// tg: code/modules/food_and_drinks/equipment/plate.dm:1
public partial class obj_item_plate : obj_item
{
    // tg: code/modules/food_and_drinks/equipment/plate.dm:24
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        if (Dm.truthy(fragile))
            Waiting.NoSleep(AddElement(typeof(datum_element_can_shatter)));
        return dot;
    }

    // tg: code/modules/food_and_drinks/equipment/plate.dm:30
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!IS_EDIBLE(tool))
            return NONE;
        if (Dm.num(tool.w_class) > biggest_w_class)
        {
            balloon_alert(user, I18n.T("too big!"));
            return ITEM_INTERACT_BLOCKING;
        }
        if (Dm.length(contents) >= max_items)
        {
            balloon_alert(user, I18n.T("can't fit!"));
            return ITEM_INTERACT_BLOCKING;
        }
        //Center the icon where the user clicked.
        if (!Dm.truthy(LAZYACCESS(modifiers, ICON_X)) || !Dm.truthy(LAZYACCESS(modifiers, ICON_Y)))
            return ITEM_INTERACT_BLOCKING;
        if (!Dm.truthy(await user.transferItemToLoc(tool, this, silent: FALSE)))
            return ITEM_INTERACT_BLOCKING;
        tool.pixel_x = clamp(Dm.num(text2num(LAZYACCESS(modifiers, ICON_X))) - 16, -max_x_offset, max_x_offset);
        tool.pixel_y = min(Dm.num(text2num(LAZYACCESS(modifiers, ICON_Y))) + placement_offset, max_height_offset);
        to_chat(user, span_notice(I18n.T("You place [] on [].", tool, this)));
        AddToPlate(tool); // tg passes the user too, which AddToPlate does not take
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/food_and_drinks/equipment/plate.dm:50
    public override async DmTask<object> pre_attack(atom target = null, mob_living user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        if (!iscarbon(target))
            return null;
        if (!Dm.truthy(Dm.length(contents)))
            return null;
        obj_item object_to_eat = contents[1] as obj_item;
        await object_to_eat.melee_attack_chain(user, target);
        return TRUE; //No normal attack
    }

    // tg: code/modules/food_and_drinks/equipment/plate.dm:59
    public override object IsContainedAtomAccessible(atom contained = null, atom_movable user = null)
    {
        return TRUE;
    }

    ///This proc adds the food to viscontents and makes sure it can deregister if this changes.
    // tg: code/modules/food_and_drinks/equipment/plate.dm:63
    public virtual void AddToPlate(obj_item item_to_plate = null)
    {
        Dm.append(vis_contents, item_to_plate);
        item_to_plate.vis_flags = Dm.bitor(item_to_plate.vis_flags, VIS_INHERIT_PLANE);
        RegisterSignal(item_to_plate, COMSIG_MOVABLE_MOVED, nameof(ItemMoved));
        RegisterSignal(item_to_plate, COMSIG_QDELETING, nameof(ItemMoved));
        // We gotta offset ourselves via pixel_w/z, so we don't end up z fighting with the plane
        item_to_plate.pixel_w = item_to_plate.pixel_x;
        item_to_plate.pixel_z = item_to_plate.pixel_y;
        item_to_plate.pixel_x = 0;
        item_to_plate.pixel_y = 0;
        update_appearance();
        // If the incoming item is the same weight class as the plate, bump us up a class
        if (Dm.eq(item_to_plate.w_class, w_class))
            update_weight_class(Dm.num(w_class) + 1);
    }

    ///This proc cleans up any signals on the item when it is removed from a plate, and ensures it has the correct state again.
    // tg: code/modules/food_and_drinks/equipment/plate.dm:79
    public virtual void ItemRemovedFromPlate(obj_item removed_item = null)
    {
        removed_item.vis_flags = Dm.bitand(removed_item.vis_flags, Dm.bitnot(VIS_INHERIT_PLANE));
        Dm.remove(vis_contents, removed_item);
        UnregisterSignal(removed_item, Dm.list(COMSIG_MOVABLE_MOVED, COMSIG_QDELETING));
        // Reset item offsets
        removed_item.pixel_x = removed_item.pixel_w;
        removed_item.pixel_y = removed_item.pixel_z;
        removed_item.pixel_w = 0;
        removed_item.pixel_z = 0;
        // We need to ensure the weight class is accurate now that we've lost something
        // that may or may not have been of equal weight
        float new_w_class = Dm.num(Dm.initial(this, (obj_item i) => i.w_class));
        foreach (obj_item on_board in Dm.each<obj_item>(this))
        {
            if (Dm.eq(on_board.w_class, w_class))
            {
                new_w_class += 1;
                break;
            }
        }

        update_weight_class(new_w_class);
    }

    ///This proc is called by signals that remove the food from the plate.
    // tg: code/modules/food_and_drinks/equipment/plate.dm:99
    [SignalHandler]
    public virtual void ItemMoved(obj_item moved_item = null, atom OldLoc = null, object Dir = null, object Forced = null)
    {
        ItemRemovedFromPlate(moved_item);
    }
}

// tg: code/modules/food_and_drinks/equipment/plate.dm:128
public partial class obj_item_plate_shard : obj_item
{
    // tg: code/modules/food_and_drinks/equipment/plate.dm:141
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        Waiting.NoSleep(AddComponent(typeof(datum_component_caltrop), Dm.named("min_damage", force), Dm.named("paralyze_duration", 2 * SECONDS), Dm.named("soundfile", hitsound)));

        icon_state = Dm.text("[][]", base_icon_state, rand(1, variants));
        return dot;
    }
}
