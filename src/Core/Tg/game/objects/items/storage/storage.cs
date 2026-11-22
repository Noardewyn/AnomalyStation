// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/storage/storage.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4a P3: the storage item base (docs/ai/specs/m4a-ownership.md, P3's list). Later, as docs/ai/specs/live-partial.txt's
// "## P3 (M4a)" rows: canStrip and doStrip (stripping, M9), the fantasy bonuses (M10).
// M5 slice 8: used_in_craft. M5x slice 1: contents_explosion (explosions).

// tg: code/game/objects/items/storage/storage.dm:1
public partial class obj_item_storage : obj_item
{
    // tg: code/game/objects/items/storage/storage.dm:38  (M5x slice 1: explosions)
    public override async DmTask contents_explosion(object severity = null, object target = null)
    {
        switch (Dm.num(severity))
        {
            case EXPLODE_DEVASTATE:
                Dm.append(ref SSexplosions.high_mov_atom, contents);
                break;
            case EXPLODE_HEAVY:
                Dm.append(ref SSexplosions.med_mov_atom, contents);
                break;
            case EXPLODE_LIGHT:
                Dm.append(ref SSexplosions.low_mov_atom, contents);
                break;
        }
    }

    // tg: code/game/objects/items/storage/storage.dm:15
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);

        Waiting.NoSleep(create_storage(storage_type: storage_type));

        Waiting.NoSleep(PopulateContents());
        return dot;
    }

    // tg: code/game/objects/items/storage/storage.dm:22
    [DmParams("max_slots", "max_specific_storage", "max_total_storage", "canhold", "canthold", "storage_type")]
    public override async DmTask<object> create_storage(object max_slots = null, object max_specific_storage = null, object max_total_storage = null, DmList canhold = null, DmList canthold = null, Type storage_type = null, object rustle_sound = null, object remove_rustle_sound = null)
    {
        // If no type was passed in, default to what we already have
        storage_type ??= this.storage_type;
        return await base.create_storage(max_slots, max_specific_storage, max_total_storage, canhold, canthold, storage_type, rustle_sound, remove_rustle_sound);
    }

    ///Use this to populate the contents of the storage
    // tg: code/game/objects/items/storage/storage.dm:35
    public virtual async DmTask PopulateContents()
    {
        // PROTECTED_PROC(TRUE): a lint annotation (rules 7.9); the base populates nothing.
    }

    // tg: code/game/objects/items/storage/storage.dm:56
    public override object AllowDrop()
    {
        return FALSE;
    }

    ///Drops all contents of this storage on the turf of its parent
    // tg: code/game/objects/items/storage/storage.dm:60
    public virtual async DmTask emptyStorage()
    {
        await atom_storage.remove_all();
    }

    /// Returns a list of object types to be preloaded by our code
    /// I'll say it again, be very careful with this. We only need it for a few things
    /// Don't do anything stupid, please
    // tg: code/game/objects/items/storage/storage.dm:97
    public virtual object get_types_to_preload()
    {
        return null;
    }

    // tg: code/game/objects/items/storage/storage.dm:100
    public override async DmTask used_in_craft(atom result = null, datum_crafting_recipe current_recipe = null)
    {
        await base.used_in_craft(result, current_recipe);
        // If we consumed in crafting, we should dump contents out before qdeling them.
        if (!Dm.truthy(is_type_in_list(this, current_recipe.parts)))
            await emptyStorage();
    }

    /// Removes an item or puts it in mouth from the contents, if any
    // tg: code/game/objects/items/storage/storage.dm:107
    public virtual async DmTask quick_remove_item(obj_item grabbies = null, mob user = null, float? equip_to_mouth = null)
    {
        equip_to_mouth ??= FALSE;
        // locate(grabbies) in contents: grabbies is an instance, so locate gives it back when it is in the list.
        obj_item finger = (grabbies != null && Dm.@in(grabbies, contents)) ? grabbies : null;
        if (!Dm.truthy(finger))
            return;
        if (!Dm.truthy(equip_to_mouth))
        {
            if (Dm.truthy(await atom_storage.remove_single(user, finger, drop_location() as atom)))
                await user.put_in_hands(finger);
            return;
        }
        if (Dm.truthy(await user.equip_to_slot_if_possible(finger, ITEM_SLOT_MASK, qdel_on_fail: FALSE, disable_warning: TRUE)))
        {
            // DEVIATION(DV234): tg's finger.forceMove(user) here (:116) unequips the worn item again (/obj/item/doMove's IN_INVENTORY path, items.dm:1319): the cigarette would drop out of the mouth into the mob's contents
            return;
        }
        balloon_alert(user, I18n.T("mouth is covered!"));
    }
}
