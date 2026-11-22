// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/tools/inducer.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7 power helpers slice (D164): the whole file. The inducer and its subtypes (empty, orderable, science, syndicate, cyborg).
// The var block is generated (D111).
//
// GAP(types): tg's `var/obj/item/stock_parts/power_store/powerdevice` (:18) is generated as a Type (rules 3.2 V3: its default
// and the subtypes' overrides are typepaths, which tg replaces by the cell on Initialize); the cell the inducer holds is
// powerdevice_value (the same split as /obj/machinery/var/circuit).

// tg: code/game/objects/items/tools/inducer.dm:1
public partial class obj_item_inducer : obj_item
{
    /// GAP(types): tg's `powerdevice` (:18) once it holds the cell: the cell used in recharging cycles.
    public obj_item_stock_parts_power_store powerdevice_value;

    // tg: code/game/objects/items/tools/inducer.dm:23
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        if (ispath(powerdevice))
            powerdevice_value = Dm.@new(powerdevice, this) as obj_item_stock_parts_power_store;

        register_context();

        update_appearance(UPDATE_OVERLAYS);
        return dot;
    }

    // tg: code/game/objects/items/tools/inducer.dm:32
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref powerdevice_value);
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/tools/inducer.dm:36
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (gone == powerdevice_value)
            powerdevice_value = null;
        return dot;
    }

    // tg: code/game/objects/items/tools/inducer.dm:42
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        object dot = NONE;

        if (held_item == null)
        {
            if (Dm.truthy(opened) && !QDELETED(powerdevice_value))
            {
                context[SCREENTIP_CONTEXT_LMB] = "Remove Cell";
                dot = CONTEXTUAL_SCREENTIP_SET;
            }
            return dot;
        }

        if (Dm.truthy(opened))
        {
            if (istype(held_item, typeof(obj_item_stock_parts_power_store)) && QDELETED(powerdevice_value))
            {
                context[SCREENTIP_CONTEXT_LMB] = "Insert cell";
                return CONTEXTUAL_SCREENTIP_SET;
            }

            if (istype(held_item, typeof(obj_item_stack_sheet_mineral_plasma)) && !QDELETED(powerdevice_value))
            {
                context[SCREENTIP_CONTEXT_LMB] = "Charge cell";
                return CONTEXTUAL_SCREENTIP_SET;
            }
        }

        if (Dm.eq(held_item.tool_behaviour, TOOL_SCREWDRIVER))
        {
            context[SCREENTIP_CONTEXT_LMB] = Dm.text("[] Panel", Dm.truthy(opened) ? "Close" : "Open");
            return CONTEXTUAL_SCREENTIP_SET;
        }
        return dot;
    }

    // tg: code/game/objects/items/tools/inducer.dm:63
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);

        Dm.append(ref dot, examine_hints(user as mob_living));
        return dot;
    }

    /**
     * Gives description for this inducer
     * Arguments
     *
     * * mob/living/user - the mob we are returning the description to
     */
    // tg: code/game/objects/items/tools/inducer.dm:74  (PROTECTED_PROC and SHOULD_BE_PURE are lint macros, rules 7.9)
    public virtual DmList examine_hints(mob_living user = null)
    {
        DmList dot = Dm.list();

        obj_item_stock_parts_power_store our_cell = get_cell(this, user) as obj_item_stock_parts_power_store;
        if (!QDELETED(our_cell))
        {
            Dm.append(ref dot, span_notice(I18n.T("Its display shows: [].", display_energy(our_cell.charge))));
            if (Dm.truthy(opened))
            {
                Dm.append(ref dot, span_notice(I18n.T("The cell can be removed with an empty hand.")));
                Dm.append(ref dot, span_notice(I18n.T("Plasma sheets can be used to recharge the cell.")));
            }
        }
        else
            Dm.append(ref dot, span_warning(I18n.T("It's missing a power cell.")));

        Dm.append(ref dot, span_notice(I18n.T("Its battery compartment can be [] [].", EXAMINE_HINT(I18n.T("screwed")), Dm.truthy(opened) ? I18n.T("shut") : I18n.T("open"))));
        return dot;
    }

    // tg: code/game/objects/items/tools/inducer.dm:90
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (!Dm.truthy(opened))
            return dot;
        Dm.append(ref dot, Dm.text("inducer-[]", !QDELETED(powerdevice_value) ? "bat" : "nobat"));
        return dot;
    }

    // tg: code/game/objects/items/tools/inducer.dm:96
    public override object get_cell(atom_movable @interface = null, mob user = null)
    {
        return powerdevice_value;
    }

    // tg: code/game/objects/items/tools/inducer.dm:99
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (!QDELETED(powerdevice_value) && !Dm.truthy(Dm.bitand(dot, EMP_PROTECT_CONTENTS)))
            await powerdevice_value.emp_act(severity);
        return dot;
    }

    // tg: code/game/objects/items/tools/inducer.dm:104
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(await tool.use_tool(this, user, 0)))
            return NONE;

        opened = Dm.num(!Dm.truthy(opened));
        to_chat(user, span_notice(I18n.T("You [] the battery compartment.", Dm.truthy(opened) ? I18n.T("open") : I18n.T("close"))));
        update_appearance(UPDATE_OVERLAYS);

        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/tools/inducer.dm:115
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(user.combat_mode) || tool == null || Dm.truthy(Dm.bitand(tool.flags_1, HOLOGRAM_1)) || Dm.truthy(Dm.bitand(tool.item_flags, ABSTRACT)))
            return ITEM_INTERACT_SKIP_TO_ATTACK;

        if (istype(tool, typeof(obj_item_stock_parts_power_store)))
        {
            if (!Dm.truthy(opened))
            {
                balloon_alert(user, I18n.T("open first!"));
                return ITEM_INTERACT_FAILURE;
            }

            if (!QDELETED(powerdevice_value))
            {
                balloon_alert(user, I18n.T("cell already installed!"));
                return ITEM_INTERACT_FAILURE;
            }

            if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
            {
                balloon_alert(user, I18n.T("stuck in hand!"));
                return ITEM_INTERACT_FAILURE;
            }

            powerdevice_value = tool as obj_item_stock_parts_power_store;
            return ITEM_INTERACT_SUCCESS;
        }
        else if (istype(tool, typeof(obj_item_stack_sheet_mineral_plasma)) && !QDELETED(powerdevice_value))
        {
            if (!Dm.truthy(powerdevice_value.used_charge()))
            {
                balloon_alert(user, I18n.T("fully charged!"));
                return ITEM_INTERACT_FAILURE;
            }

            await ((obj_item_stack)tool).use(1);
            await powerdevice_value.give(1.5f * STANDARD_CELL_CHARGE);
            balloon_alert(user, I18n.T("cell recharged"));

            return ITEM_INTERACT_SUCCESS;
        }
        return NONE;
    }

    // tg: code/game/objects/items/tools/inducer.dm:150
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (HAS_TRAIT(interacting_with, TRAIT_COMBAT_MODE_SKIP_INTERACTION))
            return NONE;

        // the parameter is declared /atom/movable here (a turf is no movable, istype(interacting_with) is the declared type)
        if (Dm.truthy(user.combat_mode) || !istype(interacting_with, typeof(atom_movable)) || Dm.truthy(Dm.bitand(interacting_with.flags_1, HOLOGRAM_1)))
            return ITEM_INTERACT_SKIP_TO_ATTACK;

        //basic checks
        if (Dm.truthy(opened))
        {
            balloon_alert(user, I18n.T("close first!"));
            return ITEM_INTERACT_FAILURE;
        }

        if (Dm.truthy(recharging) || (!isturf(interacting_with) && user.loc == interacting_with))
            return ITEM_INTERACT_FAILURE;

        if (!ISADVANCEDTOOLUSER(user))
        {
            to_chat(user, span_warning(I18n.T("You don't have the dexterity to use []!", this)));
            return ITEM_INTERACT_FAILURE;
        }

        obj_item_stock_parts_power_store our_cell = get_cell(this, user) as obj_item_stock_parts_power_store;

        if (QDELETED(our_cell))
        {
            balloon_alert(user, I18n.T("no cell installed!"));
            return ITEM_INTERACT_FAILURE;
        }

        if (!Dm.truthy(our_cell.charge))
        {
            balloon_alert(user, I18n.T("no charge!"));
            return ITEM_INTERACT_FAILURE;
        }

        obj_item_stock_parts_power_store target_cell = ((atom_movable)interacting_with).get_cell(this, user) as obj_item_stock_parts_power_store;

        if (QDELETED(target_cell))
            return ITEM_INTERACT_FAILURE;

        if (!Dm.truthy(target_cell.used_charge()))
        {
            balloon_alert(user, I18n.T("fully charged!"));
            return ITEM_INTERACT_FAILURE;
        }

        //begin recharging
        recharging = TRUE;
        user.visible_message(span_notice(I18n.T("[] starts recharging [] with [].", user, interacting_with, this)), span_notice(I18n.T("You start recharging [] with [].", interacting_with, this)));

        float done_any = FALSE;
        while (Dm.truthy(target_cell.used_charge()))
        {
            if (!Dm.truthy(await do_after(user, 1 * SECONDS, target: user)))
                break;

            //transfer of charge
            float transferred = min(our_cell.charge, Dm.num(target_cell.used_charge()), target_cell.rating_base * Dm.num(target_cell.rating) * power_transfer_multiplier);
            if (transferred == 0)
                break;
            await our_cell.use(await target_cell.give(transferred));

            //update all appearances
            our_cell.update_appearance();
            target_cell.update_appearance();
            interacting_with.update_appearance();

            //sparks & update
            do_sparks(1, FALSE, interacting_with);
            done_any = TRUE;
        }

        recharging = FALSE;

        // Only show a message if we succeeded at least once
        if (Dm.truthy(done_any))
            user.visible_message(span_notice(I18n.T("[] recharges []!", user, interacting_with)), span_notice(I18n.T("You recharge []!", interacting_with)));

        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/tools/inducer.dm:219
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(opened) && !QDELETED(powerdevice_value))
        {
            user.visible_message(span_notice(I18n.T("[] removes [] from []!", user, powerdevice_value, this)), span_notice(I18n.T("You remove [].", powerdevice_value)));
            powerdevice_value.update_appearance();
            await user.put_in_hands(powerdevice_value);
            update_appearance(UPDATE_OVERLAYS);
        }
        return null;
    }
}

// tg: code/game/objects/items/tools/inducer.dm:252
public partial class obj_item_inducer_cyborg : obj_item_inducer
{
    // tg: code/game/objects/items/tools/inducer.dm:257
    public override DmList examine_hints(mob_living user = null)
    {
        DmList dot = Dm.list();

        obj_item_stock_parts_power_store our_cell = get_cell(this, user) as obj_item_stock_parts_power_store;
        if (!QDELETED(our_cell))
        {
            Dm.append(ref dot, span_notice(I18n.T("Its display shows: [].", display_energy(our_cell.charge))));
            if (Dm.truthy(opened))
                Dm.append(ref dot, span_notice(I18n.T("Plasma sheets can be used to recharge the cell.")));
        }
        else
            Dm.append(ref dot, span_warning(I18n.T("It's missing a power cell.")));
        Dm.append(ref dot, span_notice(I18n.T("Its battery compartment can be [] [].", EXAMINE_HINT(I18n.T("screwed")), Dm.truthy(opened) ? I18n.T("shut") : I18n.T("open"))));
        return dot;
    }

    // tg: code/game/objects/items/tools/inducer.dm:270
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        return NONE;
    }

    // tg: code/game/objects/items/tools/inducer.dm:273
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (iscyborg(user) && iscyborg(interacting_with))
        {
            balloon_alert(user, I18n.T("can't charge this!"));
            return ITEM_INTERACT_FAILURE;
        }
        return await base.interact_with_atom(interacting_with, user, modifiers);
    }
}
