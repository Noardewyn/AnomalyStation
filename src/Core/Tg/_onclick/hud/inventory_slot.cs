// Translated from tgstation (snapshot 1d99bd7f): code/_onclick/hud/inventory_slot.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Translated whole (M4a P2, D126): the inventory slot datums behind the HUD's slots. The var blocks are generated
// (types-converter.md 3.2); the slot datums of hud/human.dm are var-only subtypes.

// tg: code/_onclick/hud/inventory_slot.dm:1
public partial class datum_controller_global_vars
{
    // tg: code/_onclick/hud/inventory_slot.dm:1  GLOBAL_LIST_INIT(inventory_slot_datums, initialize_inventory_slots())
    public DmList inventory_slot_datums;

    // tg: code/_onclick/hud/inventory_slot.dm:1  (GLOBAL_MANAGED(inventory_slot_datums, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalinventory_slot_datums()
    {
        inventory_slot_datums = initialize_inventory_slots() as DmList;
        Dm.append(ref gvars_datum_init_order, "inventory_slot_datums");
    }
}

// tg: code/_onclick/hud/inventory_slot.dm:3  (the global procs, rules 7.8)
public static partial class GlobalProcs
{
    // tg: code/_onclick/hud/inventory_slot.dm:3
    public static object initialize_inventory_slots()
    {
        DmList slot_types = Dm.list();
        foreach (object slot_type in Dm.each(valid_subtypesof(typeof(datum_inventory_slot))))
            slot_types[slot_type] = Dm.@new((Type)slot_type);
        return slot_types;
    }
}

/// Inventory slot datum (singleton) which holds data about an inventory screen element
// tg: code/_onclick/hud/inventory_slot.dm:10
[DmPath("/datum/inventory_slot")]
public partial class datum_inventory_slot : datum
{
    // tg: code/_onclick/hud/inventory_slot.dm:29
    public virtual async DmTask<object> create_element(datum_hud hud = null)
    {
        atom_movable_screen_inventory inv_box = await hud.add_screen_object(screen_type, datum_hud.HUD_KEY_ITEM_SLOT(slot_id), screen_group, Dm.truthy(inherit_style) ? hud.ui_style : null, screen_loc) as atom_movable_screen_inventory;
        inv_box.name = name;
        inv_box.icon_state = icon_state;
        inv_box.icon_full = icon_full;
        inv_box.slot_id = slot_id;
        return inv_box;
    }

    /// Returns the item held in this slot by hud's mymob
    // tg: code/_onclick/hud/inventory_slot.dm:38
    public virtual object get_slot_item(mob owner = null, params object[] __extra)
    {
        return owner.get_item_by_slot(slot_id);
    }

    /// Returns a screen object for this slot for a particular hud datum
    // tg: code/_onclick/hud/inventory_slot.dm:42
    public virtual object get_screen_slot(datum_hud hud = null, params object[] __extra)
    {
        return hud.screen_objects[datum_hud.HUD_KEY_ITEM_SLOT(slot_id)];
    }

    /// Update inventory slot visuals
    // tg: code/_onclick/hud/inventory_slot.dm:46
    public virtual void update_inventory_slot(datum_hud hud = null, mob owner = null, params object[] __extra)
    {
        atom_movable_screen_inventory slot = get_screen_slot(hud) as atom_movable_screen_inventory;
        if (!Dm.truthy(slot))
            return;

        obj_item slot_item = get_slot_item(owner) as obj_item;
        update_slot_icon_contents(slot, slot_item);
    }

    /// Updates the slot icon and its contents
    // tg: code/_onclick/hud/inventory_slot.dm:55
    public virtual void update_slot_icon_contents(atom_movable_screen slot = null, obj_item slot_item = null)
    {
        slot.update_appearance(UPDATE_ICON);
        if (!Dm.truthy(slot_item))
        {
            slot.vis_contents.Cut();
            return;
        }

        if (Dm.@in(slot_item, slot.vis_contents))
            return;

        // Reset pixel offsets in order to make it fit the box
        slot_item.pixel_x = slot_item.base_pixel_x;
        slot_item.pixel_y = slot_item.base_pixel_y;
        Dm.append(slot.vis_contents, slot_item);
    }
}

// Not a real inventory slot, but this is used as a generic abstraction for hand slot UI behaviors
// tg: code/_onclick/hud/inventory_slot.dm:70
public partial class datum_inventory_slot_hands : datum_inventory_slot
{
    // tg: code/_onclick/hud/inventory_slot.dm:73
    public override async DmTask<object> create_element(datum_hud hud = null)
    {
        throw Dm.CRASH(Dm.text("[] attempted to call create_element on a behavior-only hands inventory slot datum!", hud), "code/_onclick/hud/inventory_slot.dm", 74);
    }

    // tg: code/_onclick/hud/inventory_slot.dm:76
    [DmParams("owner", "hand_index")]
    public override object get_slot_item(mob owner = null, params object[] __extra)
    {
        object hand_index = Dm.arg(__extra, 0) ?? 1;
        return owner.held_items[hand_index];
    }

    // tg: code/_onclick/hud/inventory_slot.dm:79
    [DmParams("hud", "hand_index")]
    public override object get_screen_slot(datum_hud hud = null, params object[] __extra)
    {
        object hand_index = Dm.arg(__extra, 0) ?? 1;
        return hud.screen_objects[datum_hud.HUD_KEY_HAND_SLOT(hand_index)];
    }

    // tg: code/_onclick/hud/inventory_slot.dm:82
    [DmParams("hud", "owner", "hand_index")]
    public override void update_inventory_slot(datum_hud hud = null, mob owner = null, params object[] __extra)
    {
        object hand_index = Dm.arg(__extra, 0);
        // If no index was passed, update all hand slots
        if (hand_index == null)
        {
            for (float i = 1, i__end = length(owner.held_items); i <= i__end; i++)
                update_inventory_slot(hud, owner, i);
            return;
        }

        atom_movable_screen slot = get_screen_slot(hud, hand_index) as atom_movable_screen; // a cyborg's module slot is no /atom/movable/screen/inventory (tg's var type is unchecked)
        if (!Dm.truthy(slot))
            return;

        obj_item slot_item = get_slot_item(owner, hand_index) as obj_item;
        update_slot_icon_contents(slot, slot_item);
    }
}
