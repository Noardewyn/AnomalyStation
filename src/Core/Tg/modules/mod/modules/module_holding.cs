// Translated from tgstation (snapshot 1d99bd7f): code/modules/mod/modules/module_holding.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps MO2 (D333): the file whole. The MOD storage module of holding: a storage module that opens a bag of holding's pocket once a
// bluespace anomaly core is slotted in (datums/components/anomalock_module.dm), and tears a bluespace rift when its core goes in, or
// it goes into a suit, inside another bag of holding (after the bag's own confirmation). The var block is generated (D111).

// tg: code/modules/mod/modules/module_holding.dm:4
public partial class obj_item_mod_module_storage_holding : obj_item_mod_module_storage
{
    // tg: code/modules/mod/modules/module_holding.dm:1 (file-local, #undef at :105)
    private const float HOLDING_MODULE_PREVENT_DUPLICATE_CHECK = 1;
    // tg: code/modules/mod/modules/module_holding.dm:2 (file-local, #undef at :106)
    private const float HOLDING_MODULE_CHECK_CONFIRMED = 2;

    // tg: code/modules/mod/modules/module_holding.dm:18
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        anomalock = Waiting.NoSleep(AddComponent(typeof(datum_component_anomaly_locked_module),
            Dm.list(typeof(obj_item_assembly_signaler_anomaly_bluespace)),
            prebuilt,
            core_removable,
            nameof(pre_core_inserted),
            nameof(on_core_inserted),
            nameof(on_core_removed))) as datum_component_anomaly_locked_module;
        RegisterSignal(this, COMSIG_MODULE_TRY_INSTALL, nameof(try_install));
        return dot;
    }

    // tg: code/modules/mod/modules/module_holding.dm:30
    public override object Destroy(float? force = null)
    {
        possible_bag_bombs.Cut();
        return base.Destroy(force);
    }

    // tg: code/modules/mod/modules/module_holding.dm:34
    public virtual object pre_core_inserted(mob user = null, obj_item core = null, DmList modifiers = null)
    {
        if (Dm.eq(possible_bag_bombs[core], HOLDING_MODULE_PREVENT_DUPLICATE_CHECK))
            return ITEM_INTERACT_FAILURE;
        datum_storage_bag_of_holding other_bag = null;
        foreach (atom nested_loc in Dm.each<atom>(get_nested_locs(this)))
        {
            if (istype(nested_loc.atom_storage, typeof(datum_storage_bag_of_holding)))
            {
                other_bag = nested_loc.atom_storage as datum_storage_bag_of_holding;
                break;
            }
        }
        if (other_bag != null && !Dm.truthy(possible_bag_bombs[core]))
        {
            Waiting.Detach(recursive_core_insertion(other_bag, user, core, modifiers));
            return ITEM_INTERACT_BLOCKING;
        }
        return null;
    }

    // tg: code/modules/mod/modules/module_holding.dm:46
    public virtual async DmTask recursive_core_insertion(datum_storage_bag_of_holding bag_storage = null, mob user = null, obj_item core = null, DmList modifiers = null)
    {
        possible_bag_bombs[core] = HOLDING_MODULE_PREVENT_DUPLICATE_CHECK;
        if (Dm.truthy(await bag_storage.confirm_recursive_insertion(core, user as mob_living)) && !QDELETED(core) && Dm.truthy(user.is_holding(core)) && Dm.truthy(await IsReachableBy(user)))
            await anomalock.insert_core(this, user as mob_living, core, modifiers);
        Dm.remove(ref possible_bag_bombs, core);
    }

    // tg: code/modules/mod/modules/module_holding.dm:52
    public virtual async DmTask on_core_inserted(obj_item core = null, mob user = null)
    {
        foreach (atom nested_loc in Dm.each<atom>(get_nested_locs(this)))
        {
            datum_storage_bag_of_holding boh = nested_loc.atom_storage as datum_storage_bag_of_holding;
            if (istype(boh, typeof(datum_storage_bag_of_holding)))
            {
                await boh.create_rift(core, user as mob_living);
                return;
            }
        }
        await core.moveToNullspace(); // Otherwise, the core would become part of the suit's inventory.
        storage_type = typeof(datum_storage_bag_of_holding);
        await create_storage(storage_type: typeof(datum_storage_bag_of_holding));
        atom_storage.set_locked(STORAGE_FULLY_LOCKED);
        ADD_TRAIT(this, TRAIT_BLUESPACE_INTERFERENCE, TRAIT_GENERIC);
    }

    // tg: code/modules/mod/modules/module_holding.dm:64
    public virtual void on_core_removed()
    {
        QDEL_NULL(ref atom_storage);
        storage_type = null;
        REMOVE_TRAIT(this, TRAIT_BLUESPACE_INTERFERENCE, TRAIT_GENERIC);
    }

    // tg: code/modules/mod/modules/module_holding.dm:69
    [SignalHandler]
    public virtual object try_install(object _source = null, obj_item_mod_control suit = null, mob user = null)
    {
        if (Dm.eq(possible_bag_bombs[suit], HOLDING_MODULE_PREVENT_DUPLICATE_CHECK))
            return MOD_ABORT_INSTALL;
        if (anomalock.core == null)
        {
            balloon_alert(user, I18n.T("no core!"));
            playsound(suit, Dm.resource("sound/machines/scanner/scanbuzz.ogg"), 25, TRUE, SILENCED_SOUND_EXTRARANGE);
            return MOD_ABORT_INSTALL;
        }
        datum_storage_bag_of_holding other_bag = null;
        foreach (atom nested_loc in Dm.each<atom>(get_nested_locs(suit)))
        {
            if (istype(nested_loc.atom_storage, typeof(datum_storage_bag_of_holding)))
            {
                other_bag = nested_loc.atom_storage as datum_storage_bag_of_holding;
                break;
            }
        }
        if (other_bag != null)
        {
            if (Dm.eq(possible_bag_bombs[suit], HOLDING_MODULE_CHECK_CONFIRMED))
                Waiting.NoSleep(other_bag.create_rift(this, user as mob_living));
            else
                Waiting.Detach(recursive_installation(other_bag, suit, user));
            return MOD_ABORT_INSTALL;
        }
        return null;
    }

    // tg: code/modules/mod/modules/module_holding.dm:89
    public virtual async DmTask recursive_installation(datum_storage_bag_of_holding bag_storage = null, obj_item_mod_control suit = null, mob user = null)
    {
        possible_bag_bombs[suit] = HOLDING_MODULE_PREVENT_DUPLICATE_CHECK;
        if (Dm.truthy(await bag_storage.confirm_recursive_insertion(this, user as mob_living)) && !QDELETED(suit) && Dm.truthy(user.is_holding(this)) && Dm.truthy(await suit.IsReachableBy(user)))
        {
            possible_bag_bombs[suit] = HOLDING_MODULE_CHECK_CONFIRMED;
            await suit.install(this, user);
        }
        Dm.remove(ref possible_bag_bombs, suit);
    }
}
