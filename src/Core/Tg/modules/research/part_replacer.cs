// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/part_replacer.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package OD3, D333): the rapid part exchange device (RPED) that stores, sorts and applies machine parts,
// the bluespace RPED working at range with its beam (and its guard against reagent bombs and rigged cells), the tiered and
// cargo RPEDs with their parts, and the cyborgs' RPEDs. Its storage is `datums/storage/subtypes/others/rped.cs`; the exchange
// itself is `/obj/machinery/exchange_parts` and the frames' install procs. The var blocks are generated (D111).

// tg: code/modules/research/part_replacer.dm:2
public partial class obj_item_storage_part_replacer : obj_item_storage
{
    // tg: code/modules/research/part_replacer.dm:14
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        obj attacked_object = interacting_with as obj;
        if (Dm.truthy(user.combat_mode))
            return ITEM_INTERACT_SKIP_TO_ATTACK;

        //its very important to NOT block so frames can still interact with it
        if (!ismachinery(attacked_object) || istype(attacked_object, typeof(obj_machinery_computer)))
            return NONE;

        obj_machinery attacked_machinery = (obj_machinery)attacked_object;
        if (!Dm.truthy(LAZYLEN(attacked_machinery.component_parts)))
            return ITEM_INTERACT_FAILURE;

        return Dm.truthy(await attacked_machinery.exchange_parts(user, this)) ? ITEM_INTERACT_SUCCESS : ITEM_INTERACT_FAILURE;
    }

    ///Plays the sound & flick animation for RPED exhanging or installing parts.
    // tg: code/modules/research/part_replacer.dm:29
    public virtual void play_rped_effect()
    {
        playsound(this, Dm.resource("sound/items/tools/rped.ogg"), 40, TRUE);
        flick(Dm.text("[]_active", icon_state), this);
    }

    /**
     * Gets parts sorted in order of their tier
     * Arguments
     *
     * * ignore_stacks - should the final list contain stacks
     */
    // tg: code/modules/research/part_replacer.dm:39
    public virtual DmList get_sorted_parts(float? ignore_stacks = null)
    {
        ignore_stacks ??= FALSE;
        DmList part_list = Dm.list();
        //Assemble a list of current parts, then sort them by their rating!
        foreach (object o in Dm.each(contents))
        {
            if (o is not obj_item component_part)
                continue;
            //No need to put circuit boards in this list or stacks when exchanging parts
            if (istype(component_part, typeof(obj_item_circuitboard)) || (Dm.truthy(ignore_stacks) && istype(component_part, typeof(obj_item_stack))))
                continue;
            Dm.append(ref part_list, component_part);
        }
        //Sort the parts. This ensures that higher tier items are applied first.
        sortTim(part_list, "/proc/" + nameof(GlobalProcs.cmp_rped_sort));

        return part_list;
    }
}

///Bluespace RPED. Allows exchanging parts from a distance & through cameras
// tg: code/modules/research/part_replacer.dm:56
public partial class obj_item_storage_part_replacer_bluespace : obj_item_storage_part_replacer
{
    // tg: code/modules/research/part_replacer.dm:64
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        RegisterSignal(this, COMSIG_ATOM_ENTERED, nameof(on_part_entered));
        RegisterSignal(this, COMSIG_ATOM_EXITED, nameof(on_part_exited));
        return dot;
    }

    // tg: code/modules/research/part_replacer.dm:70
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        object dot = await base.interact_with_atom(interacting_with, user, modifiers);
        if (Dm.truthy(Dm.bitand(dot, ITEM_INTERACT_ANY_BLOCKER)))
            user.Beam(interacting_with, icon_state: "rped_upgrade", time: 0.5f * SECONDS);
        return dot;
    }

    // tg: code/modules/research/part_replacer.dm:75
    public override async DmTask<object> ranged_interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        return await interact_with_atom(interacting_with, user, modifiers);
    }

    // tg: code/modules/research/part_replacer.dm:78
    public override void play_rped_effect()
    {
        if (prob(1))
            playsound(this, Dm.resource("sound/items/pshoom/pshoom_2.ogg"), 40, TRUE);
        else
            playsound(this, Dm.resource("sound/items/pshoom/pshoom.ogg"), 40, TRUE);
        flick(Dm.text("[]_active", icon_state), this);
    }

    /**
     * Signal handler for when a part has been inserted into the BRPED.
     * If the inserted item is a rigged or corrupted cell, does some logging.
     * We clear existing reagents & stop new ones from being added to prevent remote spam bombing
     */
    // tg: code/modules/research/part_replacer.dm:90
    [SignalHandler]
    public virtual void on_part_entered(datum source = null, obj_item inserted_component = null)
    {
        if (istype(inserted_component, typeof(obj_item_stock_parts_power_store)))
        {
            obj_item_stock_parts_power_store inserted_cell = (obj_item_stock_parts_power_store)inserted_component;
            if (Dm.truthy(inserted_cell.corrupted))
            {
                mob user = usr as mob;
                message_admins(Dm.text("[] has inserted corrupted [] into [].", ADMIN_LOOKUPFLW(user), inserted_cell, this));
                user?.log_message(Dm.text("has inserted corrupted [] into [].", inserted_cell, this), LOG_GAME);
                user?.log_message(Dm.text("inserted corrupted [] into []", inserted_cell, this), LOG_ATTACK);
            }
            return;
        }

        datum_reagents target_holder = inserted_component?.reagents as datum_reagents;
        if (target_holder != null)
        {
            if (Dm.truthy(target_holder.total_volume))
            {
                Waiting.NoSleep(target_holder.force_stop_reacting());
                Waiting.NoSleep(target_holder.clear_reagents());
                to_chat(usr, span_notice(I18n.T("[] churns as [] has its reagents emptied into bluespace.", this, inserted_component)));
            }
            RegisterSignal(target_holder, COMSIG_REAGENTS_PRE_ADD_REAGENT, nameof(on_insered_component_reagent_pre_add));
        }
    }

    /// Hooks [COMSIG_REAGENTS_PRE_ADD_REAGENT] to block adding any form of reagent to component beakers inside the RPED
    // tg: code/modules/research/part_replacer.dm:110
    [SignalHandler]
    public virtual object on_insered_component_reagent_pre_add(datum source = null, object reagent = null, object amount = null, object reagtemp = null, object data = null, object no_react = null)
    {
        return COMPONENT_CANCEL_REAGENT_ADD;
    }

    /**
     * Signal handler for a part is removed from the BRPED.
     * Restores original reagents of the component part, if it has any.
     */
    // tg: code/modules/research/part_replacer.dm:119
    [SignalHandler]
    public virtual void on_part_exited(datum source = null, obj_item removed_component = null)
    {
        datum_reagents target_holder = removed_component?.reagents as datum_reagents;
        if (target_holder != null)
            UnregisterSignal(target_holder, COMSIG_REAGENTS_PRE_ADD_REAGENT);
    }
}

//RPED with tiered contents
// tg: code/modules/research/part_replacer.dm:127
public partial class obj_item_storage_part_replacer_bluespace_tier1 : obj_item_storage_part_replacer_bluespace
{
    // tg: code/modules/research/part_replacer.dm:127
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 10; i++)
        {
            Dm.@new<obj_item_stock_parts_capacitor>(this);
            Dm.@new<obj_item_stock_parts_scanning_module>(this);
            Dm.@new<obj_item_stock_parts_servo>(this);
            Dm.@new<obj_item_stock_parts_micro_laser>(this);
            Dm.@new<obj_item_stock_parts_matter_bin>(this);
            Dm.@new<obj_item_stock_parts_power_store_cell_high>(this);
            Dm.@new<obj_item_stock_parts_power_store_battery_high>(this);
        }
    }
}

// tg: code/modules/research/part_replacer.dm:137
public partial class obj_item_storage_part_replacer_bluespace_tier2 : obj_item_storage_part_replacer_bluespace
{
    // tg: code/modules/research/part_replacer.dm:137
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 10; i++)
        {
            Dm.@new<obj_item_stock_parts_capacitor_adv>(this);
            Dm.@new<obj_item_stock_parts_scanning_module_adv>(this);
            Dm.@new<obj_item_stock_parts_servo_nano>(this);
            Dm.@new<obj_item_stock_parts_micro_laser_high>(this);
            Dm.@new<obj_item_stock_parts_matter_bin_adv>(this);
            Dm.@new<obj_item_stock_parts_power_store_cell_super>(this);
            Dm.@new<obj_item_stock_parts_power_store_battery_super>(this);
        }
    }
}

// tg: code/modules/research/part_replacer.dm:147
public partial class obj_item_storage_part_replacer_bluespace_tier3 : obj_item_storage_part_replacer_bluespace
{
    // tg: code/modules/research/part_replacer.dm:147
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 10; i++)
        {
            Dm.@new<obj_item_stock_parts_capacitor_super>(this);
            Dm.@new<obj_item_stock_parts_scanning_module_phasic>(this);
            Dm.@new<obj_item_stock_parts_servo_pico>(this);
            Dm.@new<obj_item_stock_parts_micro_laser_ultra>(this);
            Dm.@new<obj_item_stock_parts_matter_bin_super>(this);
            Dm.@new<obj_item_stock_parts_power_store_cell_hyper>(this);
            Dm.@new<obj_item_stock_parts_power_store_battery_hyper>(this);
        }
    }
}

// tg: code/modules/research/part_replacer.dm:157
public partial class obj_item_storage_part_replacer_bluespace_tier4 : obj_item_storage_part_replacer_bluespace
{
    // tg: code/modules/research/part_replacer.dm:157
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 10; i++)
        {
            Dm.@new<obj_item_stock_parts_capacitor_quadratic>(this);
            Dm.@new<obj_item_stock_parts_scanning_module_triphasic>(this);
            Dm.@new<obj_item_stock_parts_servo_femto>(this);
            Dm.@new<obj_item_stock_parts_micro_laser_quadultra>(this);
            Dm.@new<obj_item_stock_parts_matter_bin_bluespace>(this);
            Dm.@new<obj_item_stock_parts_power_store_cell_bluespace>(this);
            Dm.@new<obj_item_stock_parts_power_store_battery_bluespace>(this);
            Dm.@new<obj_item_stack_cable_coil_thirty>(this);
        }
    }
}

// tg: code/modules/research/part_replacer.dm:168
public partial class obj_item_storage_part_replacer_bluespace_AdminDebug : obj_item_storage_part_replacer_bluespace
{
    // tg: code/modules/research/part_replacer.dm:168
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 40; i++)
        {
            Dm.@new<obj_item_stock_parts_capacitor_quadratic>(this);
            Dm.@new<obj_item_stock_parts_scanning_module_triphasic>(this);
            Dm.@new<obj_item_stock_parts_servo_femto>(this);
            Dm.@new<obj_item_stock_parts_micro_laser_quadultra>(this);
            Dm.@new<obj_item_stock_parts_matter_bin_bluespace>(this);
            Dm.@new<obj_item_stock_parts_power_store_cell_bluespace>(this);
            Dm.@new<obj_item_stack_cable_coil_thirty>(this);
        }
    }
}

//used in a cargo crate
// tg: code/modules/research/part_replacer.dm:179
public partial class obj_item_storage_part_replacer_cargo : obj_item_storage_part_replacer
{
    // tg: code/modules/research/part_replacer.dm:179
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 10; i++)
        {
            Dm.@new<obj_item_stock_parts_capacitor>(this);
            Dm.@new<obj_item_stock_parts_scanning_module>(this);
            Dm.@new<obj_item_stock_parts_servo>(this);
            Dm.@new<obj_item_stock_parts_micro_laser>(this);
            Dm.@new<obj_item_stock_parts_matter_bin>(this);
        }
    }
}
