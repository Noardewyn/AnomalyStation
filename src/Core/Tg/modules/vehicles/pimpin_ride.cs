// Translated from tgstation (snapshot 1d99bd7f): code/modules/vehicles/pimpin_ride.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps H4 (D333, the janitor): the whole file: the janicart (ridden with the janitor's key, its hooked trash bag that takes what is
// put in it, its upgrade slot) and its upgrades, the floor buffer (the cleaning element) and the vacuum (the vacuum component). The
// var blocks are generated (D111).

/**
 * # Janicart
 */
// tg: code/modules/vehicles/pimpin_ride.dm:4
public partial class obj_vehicle_ridden_janicart : obj_vehicle_ridden
{
    // tg: code/modules/vehicles/pimpin_ride.dm:15
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        register_context();
        update_appearance();
        Waiting.NoSleep(AddElement(typeof(datum_element_ridable), typeof(datum_component_riding_vehicle_janicart)));
        Dm.append(ref GLOB.janitor_devices, this);
        if (installed_upgrade != null)
            Waiting.NoSleep(installed_upgrade.install(this));
        return dot;
    }

    // tg: code/modules/vehicles/pimpin_ride.dm:24
    public override object Destroy(float? force = null)
    {
        Dm.remove(ref GLOB.janitor_devices, this);
        if (trash_bag != null)
            QDEL_NULL(ref trash_bag);
        if (installed_upgrade != null)
            QDEL_NULL(ref installed_upgrade);
        return base.Destroy(force);
    }

    // tg: code/modules/vehicles/pimpin_ride.dm:32
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (installed_upgrade != null)
            Dm.append(ref dot, I18n.T("It has been upgraded with [], which can be removed with a screwdriver.", installed_upgrade));
        return dot;
    }

    // tg: code/modules/vehicles/pimpin_ride.dm:37
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.item_interaction(user, tool, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        if (tool is obj_item_storage_bag_trash)
        {
            if (trash_bag != null)
            {
                to_chat(user, span_warning(I18n.T("[] already has a trashbag hooked!", this)));
                return ITEM_INTERACT_BLOCKING;
            }
            if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
                return ITEM_INTERACT_BLOCKING;

            to_chat(user, span_notice(I18n.T("You hook the trashbag onto [].", this)));
            trash_bag = (obj_item_storage_bag_trash)tool;
            RegisterSignal(trash_bag, COMSIG_QDELETING, nameof(bag_deleted));
            SEND_SIGNAL(COMSIG_VACUUM_BAG_ATTACH, Dm.list(this, tool));
            update_appearance();
            return ITEM_INTERACT_SUCCESS;
        }

        if (tool is obj_item_janicart_upgrade new_upgrade)
        {
            if (installed_upgrade != null)
            {
                to_chat(user, span_warning(I18n.T("[] already has an upgrade installed! Use a screwdriver to remove it.", this)));
                return ITEM_INTERACT_BLOCKING;
            }
            await new_upgrade.forceMove(this);
            await new_upgrade.install(this);
            installed_upgrade = new_upgrade;
            to_chat(user, span_notice(I18n.T("You upgrade [] with [].", this, new_upgrade)));
            update_appearance();
            return ITEM_INTERACT_SUCCESS;
        }

        if (trash_bag != null && (!is_key(tool) || is_key(inserted_key))) // don't put a key in the trash when we need it
        {
            trash_bag.atom_storage.attempt_insert(tool, user);
            return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }

    // tg: code/modules/vehicles/pimpin_ride.dm:73
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        if (installed_upgrade == null)
            return ITEM_INTERACT_BLOCKING;
        installed_upgrade.uninstall(this);
        await installed_upgrade.forceMove(get_turf(user));
        await user.put_in_hands(installed_upgrade);
        to_chat(user, span_notice(I18n.T("You remove [] from []", installed_upgrade, this)));
        installed_upgrade = null;
        update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/vehicles/pimpin_ride.dm:84
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        if (trash_bag != null)
        {
            if (trash_bag is obj_item_storage_bag_trash_bluespace)
                Dm.append(ref dot, "cart_bluespace_garbage");
            else
                Dm.append(ref dot, "cart_garbage");
        }
        if (installed_upgrade != null)
        {
            // tg: var/mutable_appearance/overlay = new(SSgreyscale.GetColoredIconByType(...)); overlay.icon_state = "janicart_upgrade"
            var overlay = (mutable_appearance)mutable_appearance(SSgreyscale.GetColoredIconByType(installed_upgrade.overlay_greyscale_config, installed_upgrade.greyscale_colors), "janicart_upgrade");
            Dm.append(ref dot, overlay);
        }
        return dot;
    }

    // tg: code/modules/vehicles/pimpin_ride.dm:96
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        // right click removes bag without unbuckling when possible
        object dot = Dm.truthy(LAZYACCESS(modifiers, RIGHT_CLICK)) && Dm.truthy(await try_remove_bag(user)) ? TRUE : null;
        if (!Dm.truthy(dot))
            dot = await base.attack_hand(user, modifiers, __extra);
        if (!Dm.truthy(dot))
            await try_remove_bag(user);
        return dot;
    }

    // tg: code/modules/vehicles/pimpin_ride.dm:102
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        object dot = base.add_context(source, context, held_item, user);

        if (held_item == null)
        {
            if (occupant_amount() > 0)
            {
                context[SCREENTIP_CONTEXT_LMB] = "Dismount";
                context[SCREENTIP_CONTEXT_RMB] = "Dismount";
                if (trash_bag != null)
                    context[SCREENTIP_CONTEXT_RMB] = "Remove trash bag";
                if (is_key(inserted_key) && Dm.truthy(occupants.Find(user)))
                    context[SCREENTIP_CONTEXT_ALT_LMB] = "Remove key";
                return CONTEXTUAL_SCREENTIP_SET;
            }
            else if (trash_bag != null)
            {
                context[SCREENTIP_CONTEXT_LMB] = "Remove trash bag";
                context[SCREENTIP_CONTEXT_RMB] = "Remove trash bag";
                return CONTEXTUAL_SCREENTIP_SET;
            }
        }

        if (held_item is obj_item_storage_bag_trash && trash_bag == null)
        {
            context[SCREENTIP_CONTEXT_LMB] = "Add trash bag";
            context[SCREENTIP_CONTEXT_RMB] = "Add trash bag";
            return CONTEXTUAL_SCREENTIP_SET;
        }

        if (held_item is obj_item_janicart_upgrade && installed_upgrade == null)
        {
            context[SCREENTIP_CONTEXT_LMB] = "Install upgrade";
            return CONTEXTUAL_SCREENTIP_SET;
        }

        if (held_item is obj_item_screwdriver && installed_upgrade != null)
        {
            context[SCREENTIP_CONTEXT_LMB] = "Remove upgrade";
            return CONTEXTUAL_SCREENTIP_SET;
        }

        if (is_key(held_item) && !is_key(inserted_key))
        {
            context[SCREENTIP_CONTEXT_LMB] = "Insert key";
            context[SCREENTIP_CONTEXT_RMB] = "Insert key";
            return CONTEXTUAL_SCREENTIP_SET;
        }
        else if (trash_bag != null)
        {
            context[SCREENTIP_CONTEXT_LMB] = "Insert into trash bag";
            context[SCREENTIP_CONTEXT_RMB] = "Insert into trash bag";
            return CONTEXTUAL_SCREENTIP_SET;
        }
        return dot;
    }

    /**
     * Called if the attached bag is being qdeleted, ensures appearance is maintained properly
     */
    // tg: code/modules/vehicles/pimpin_ride.dm:144
    [SignalHandler]
    public virtual void bag_deleted(datum source = null)
    {
        Waiting.Detach(try_remove_bag()); // INVOKE_ASYNC
    }

    /**
     * Attempts to remove the attached trash bag, returns true if bag was removed
     *
     * Arguments:
     * * remover - The (optional) mob attempting to remove the bag
     */
    // tg: code/modules/vehicles/pimpin_ride.dm:154
    public virtual async DmTask<object> try_remove_bag(mob remover = null)
    {
        if (trash_bag == null)
            return FALSE;
        if (remover != null)
        {
            await trash_bag.forceMove(get_turf(remover));
            await remover.put_in_hands(trash_bag);
        }
        UnregisterSignal(trash_bag, COMSIG_QDELETING);
        trash_bag = null;
        SEND_SIGNAL(COMSIG_VACUUM_BAG_DETACH, Dm.list(this));
        update_appearance();
        return TRUE;
    }
}

/**
 * # Janicart Upgrade
 *
 * Functional upgrades that can be installed into a janicart.
 *
 */
// tg: code/modules/vehicles/pimpin_ride.dm:178
public partial class obj_item_janicart_upgrade : obj_item
{
    /**
     * Called when upgrade is installed into a janicart
     *
     * Arguments:
     * * installee - The cart the upgrade is being installed into
     */
    // tg: code/modules/vehicles/pimpin_ride.dm:194
    public virtual async DmTask<object> install(obj_vehicle_ridden_janicart installee = null)
    {
        await DmTask.CompletedTask;
        return FALSE;
    }

    /**
     * Called when upgrade is uninstalled from a janicart
     *
     * Arguments:
     * * installee - The cart the upgrade is being removed from
     */
    // tg: code/modules/vehicles/pimpin_ride.dm:203
    public virtual object uninstall(obj_vehicle_ridden_janicart installee = null)
    {
        return FALSE;
    }
}

// tg: code/modules/vehicles/pimpin_ride.dm:206
public partial class obj_item_janicart_upgrade_buffer : obj_item_janicart_upgrade
{
    // tg: code/modules/vehicles/pimpin_ride.dm:214
    public override async DmTask<object> install(obj_vehicle_ridden_janicart installee = null)
    {
        await installee.AddElement(typeof(datum_element_cleaning));
        return null;
    }

    // tg: code/modules/vehicles/pimpin_ride.dm:217
    public override object uninstall(obj_vehicle_ridden_janicart installee = null)
    {
        installee.RemoveElement(typeof(datum_element_cleaning));
        return null;
    }
}

// tg: code/modules/vehicles/pimpin_ride.dm:220
public partial class obj_item_janicart_upgrade_vacuum : obj_item_janicart_upgrade
{
    // tg: code/modules/vehicles/pimpin_ride.dm:228
    public override async DmTask<object> install(obj_vehicle_ridden_janicart installee = null)
    {
        return await installee.AddComponent(typeof(datum_component_vacuum), installee.trash_bag);
    }

    // tg: code/modules/vehicles/pimpin_ride.dm:231
    public override object uninstall(obj_vehicle_ridden_janicart installee = null)
    {
        qdel(installee.GetComponent(typeof(datum_component_vacuum)));
        return null;
    }
}
