// Translated from tgstation (snapshot 1d99bd7f): code/modules/food_and_drinks/machinery/grill.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package H2, D333): the barbeque grill. The var block is hand-written: tg's `var/obj/item/food/grilled_item`
// holds any edible item the grill takes (IS_EDIBLE, :210), which an /obj/item/food field would drop (rules 3.2, wider values).

// tg: code/modules/food_and_drinks/machinery/grill.dm:6
public partial class obj_machinery_grill : obj_machinery
{
    // tg: code/modules/food_and_drinks/machinery/grill.dm:2  #define GRILL_FUELUSAGE_IDLE 0.5 (file-local, #undef at :300)
    private const float GRILL_FUELUSAGE_IDLE = 0.5f;
    // tg: code/modules/food_and_drinks/machinery/grill.dm:4  #define GRILL_FUELUSAGE_ACTIVE 5 (file-local, #undef at :301)
    private const float GRILL_FUELUSAGE_ACTIVE = 5;

    ///The item we are trying to grill
    // tg: code/modules/food_and_drinks/machinery/grill.dm:20
    public obj_item grilled_item;

    // tg: code/modules/food_and_drinks/machinery/grill.dm:6 (the type's own var defaults: a hand-written var block, types-converter.md 3.2)
    public obj_machinery_grill()
    {
        name = "Barbeque grill";
        desc = "Just like the old days. Smokes items over a light heat";
        icon = Dm.resource("icons/obj/machines/kitchen.dmi");
        icon_state = "grill_open";
        density = TRUE;
        pass_flags_self = PASSMACHINE | LETPASSTHROW;
        processing_flags = START_PROCESSING_MANUALLY;
        use_power = NO_POWER_USE;
    }

    // tg: code/modules/food_and_drinks/machinery/grill.dm:15
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        custom_materials = Dm.list(Dm.kv(typeof(datum_material_iron), SHEET_MATERIAL_AMOUNT * 8), Dm.kv(typeof(datum_material_glass), SMALL_MATERIAL_AMOUNT * 0.5f)); // tg: code/modules/food_and_drinks/machinery/grill.dm:15
    }

    // tg: code/modules/food_and_drinks/machinery/grill.dm:26
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        create_reagents(30, NO_REACT);
        grill_loop = Dm.@new<datum_looping_sound_grill>(this, FALSE);
        register_context();
        return dot;
    }

    // tg: code/modules/food_and_drinks/machinery/grill.dm:32
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref grilled_item);
        QDEL_NULL(ref grill_loop);
        return base.Destroy(force);
    }

    // tg: code/modules/food_and_drinks/machinery/grill.dm:37
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        if (!QDELETED(grilled_item))
            await grilled_item.forceMove(drop_location() as atom);

        Dm.@new<obj_item_assembly_igniter>(loc);
        Dm.@new<obj_item_stack_sheet_iron>(loc, 5);
        Dm.@new<obj_item_stack_rods>(loc, 5);

        if (grill_fuel > 0)
            await do_smoke(1, this, loc, smoke_type: typeof(datum_effect_system_fluid_spread_smoke_bad));
        return null;
    }

    // tg: code/modules/food_and_drinks/machinery/grill.dm:48
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        object dot = NONE;
        if (held_item == null || Dm.truthy(Dm.bitand(held_item.item_flags, ABSTRACT)) || Dm.truthy(Dm.bitand(held_item.flags_1, HOLOGRAM_1)) || Dm.truthy(Dm.bitand(held_item.resistance_flags, INDESTRUCTIBLE)))
            return dot;

        if (held_item is obj_item_stack_sheet_mineral_coal || held_item is obj_item_stack_sheet_mineral_wood)
        {
            context[SCREENTIP_CONTEXT_LMB] = "Add fuel";
            return CONTEXTUAL_SCREENTIP_SET;
        }
        else if (is_reagent_container(held_item) && Dm.truthy(held_item.is_open_container()) && Dm.truthy(((datum_reagents)held_item.reagents).total_volume))
        {
            context[SCREENTIP_CONTEXT_LMB] = "Add fuel";
            return CONTEXTUAL_SCREENTIP_SET;
        }
        else if (IS_EDIBLE(held_item) && !HAS_TRAIT(held_item, TRAIT_NODROP))
        {
            context[SCREENTIP_CONTEXT_LMB] = "Add item";
            return CONTEXTUAL_SCREENTIP_SET;
        }
        else if (Dm.eq(held_item.tool_behaviour, TOOL_WRENCH))
        {
            context[SCREENTIP_CONTEXT_LMB] = Dm.text("[]chor", Dm.truthy(anchored) ? "Unan" : "An");
            return CONTEXTUAL_SCREENTIP_SET;
        }
        else if (!Dm.truthy(anchored) && Dm.eq(held_item.tool_behaviour, TOOL_CROWBAR))
        {
            context[SCREENTIP_CONTEXT_LMB] = "Deconstruct";
            return CONTEXTUAL_SCREENTIP_SET;
        }
        return dot;
    }

    // tg: code/modules/food_and_drinks/machinery/grill.dm:69
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);

        Dm.append(ref dot, span_notice(I18n.T("Add fuel via wood/coal stacks or any open container having a good fuel source")));
        Dm.append(ref dot, span_notice(I18n.T("Monkey energy > Oil > Welding fuel > Ethanol. Others cause bad effects")));
        Dm.append(ref dot, span_notice(I18n.T("Place any food item on top via hand to start grilling")));

        if (!Dm.truthy(anchored))
            Dm.append(ref dot, span_notice(I18n.T("It can be [] apart.", EXAMINE_HINT(I18n.T("pried")))));
        if (Dm.truthy(anchored))
            Dm.append(ref dot, span_notice(I18n.T("Its [] in place.", EXAMINE_HINT(I18n.T("anchored")))));
        else
            Dm.append(ref dot, span_warning(I18n.T("It needs to be [] to work.", EXAMINE_HINT(I18n.T("anchored")))));
        return dot;
    }

    // tg: code/modules/food_and_drinks/machinery/grill.dm:83
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (!QDELETED(grilled_item))
        {
            icon_state = "grill";
            return await base.update_icon_state(__extra);
        }
        if (grill_fuel > 0)
        {
            icon_state = "grill_on";
            return await base.update_icon_state(__extra);
        }
        icon_state = "grill_open";
        return await base.update_icon_state(__extra);
    }

    // tg: code/modules/food_and_drinks/machinery/grill.dm:93
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (gone == grilled_item)
        {
            grill_time = 0;
            grill_loop.stop();
            grilled_item = null;
        }
        return dot;
    }

    // tg: code/modules/food_and_drinks/machinery/grill.dm:100
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        if (!QDELETED(grilled_item))
        {
            balloon_alert(user, I18n.T("item removed"));
            await grilled_item.forceMove(drop_location() as atom);
            update_appearance(UPDATE_ICON_STATE);
            return TRUE;
        }

        return await base.attack_hand(user, modifiers, __extra);
    }

    // tg: code/modules/food_and_drinks/machinery/grill.dm:109
    public override async DmTask<object> attack_ai(mob user = null)
    {
        return null; //the ai can't physically flip the lid for the grill
    }

    /// Makes grill fuel from a unit of stack
    // tg: code/modules/food_and_drinks/machinery/grill.dm:114
    [DmPrivate]
    public virtual async DmTask burn_stack()
    {
        //compute boost from wood or coal
        float boost = 0;
        foreach (obj_item stack in Dm.each<obj_item>(contents))
        {
            boost = 5 * (GRILL_FUELUSAGE_IDLE + GRILL_FUELUSAGE_ACTIVE);
            if (stack is obj_item_stack_sheet_mineral_coal)
                boost *= 2;
            if (Dm.truthy(await stack.use(1)))
                grill_fuel += boost;
        }
        update_appearance(UPDATE_ICON_STATE);
    }

    // tg: code/modules/food_and_drinks/machinery/grill.dm:127
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item weapon = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(user.combat_mode) || Dm.truthy(Dm.bitand(weapon.item_flags, ABSTRACT)) || Dm.truthy(Dm.bitand(weapon.flags_1, HOLOGRAM_1)) || Dm.truthy(Dm.bitand(weapon.resistance_flags, INDESTRUCTIBLE)))
            return NONE;

        if (weapon is obj_item_stack_sheet_mineral_coal || weapon is obj_item_stack_sheet_mineral_wood)
        {
            if (!QDELETED(grilled_item))
                return NONE;
            if (!Dm.truthy(anchored))
            {
                balloon_alert(user, I18n.T("anchor it first!"));
                return ITEM_INTERACT_BLOCKING;
            }

            //required for amount subtypes
            Type target_type;
            if (weapon is obj_item_stack_sheet_mineral_coal)
                target_type = typeof(obj_item_stack_sheet_mineral_coal);
            else
                target_type = typeof(obj_item_stack_sheet_mineral_wood);

            //transfer or merge stacks if we have enough space
            float merged = FALSE;
            obj_item_stack target = weapon as obj_item_stack;
            foreach (obj_item_stack stored in Dm.each<obj_item_stack>(contents))
            {
                if (!istype(stored, target_type))
                    continue;
                if (stored.amount == MAX_STACK_SIZE)
                {
                    balloon_alert(user, I18n.T("no space!"));
                    return ITEM_INTERACT_BLOCKING;
                }
                await target.merge(stored);
                merged = TRUE;
                break;
            }
            if (!Dm.truthy(merged))
                await weapon.forceMove(this);

            to_chat(user, span_notice(I18n.T("You add [] to the fuel stack.", this)));
            if (!Dm.truthy(grill_fuel))
            {
                await burn_stack();
                begin_processing();
            }
            return ITEM_INTERACT_SUCCESS;
        }

        if (is_reagent_container(weapon) && Dm.truthy(weapon.is_open_container()))
        {
            obj_item_reagent_containers container = (obj_item_reagent_containers)weapon;
            if (!QDELETED(grilled_item))
                return NONE;
            if (!Dm.truthy(anchored))
            {
                balloon_alert(user, I18n.T("anchor it first!"));
                return ITEM_INTERACT_BLOCKING;
            }

            object transfered_amount = await ((datum_reagents)weapon.reagents).trans_to(this, container.amount_per_transfer_from_this);
            if (Dm.truthy(transfered_amount))
            {
                //reagents & their effects on fuel
                DmList fuel_map = item_interaction__fuel_map;

                //compute extra fuel to be obtained from everything transfered
                float boost;
                float additional_fuel = 0;
                foreach (datum_reagent stored in Dm.each_cast<datum_reagent>(((datum_reagents)reagents).reagent_list))
                {
                    boost = Dm.num(fuel_map[stored.type]);
                    if (!Dm.truthy(boost)) //anything we don't recognize as fuel has inverse effects
                        boost = -1;
                    boost = boost * Dm.num(stored.volume) * (GRILL_FUELUSAGE_IDLE + GRILL_FUELUSAGE_ACTIVE);
                    additional_fuel += boost;
                }

                //add to fuel source
                await ((datum_reagents)reagents).clear_reagents();
                grill_fuel += additional_fuel;
                if (grill_fuel <= 0) //can happen if you put water or something
                    grill_fuel = 0;
                else
                    begin_processing();
                update_appearance(UPDATE_ICON_STATE);

                //feedback
                to_chat(user, span_notice(I18n.T("You transfer []u to the fuel source.", transfered_amount)));
                return ITEM_INTERACT_SUCCESS;
            }

            balloon_alert(user, I18n.T("no fuel transfered!"));
            return ITEM_INTERACT_BLOCKING;
        }

        if (IS_EDIBLE(weapon))
        {
            //sanity checks
            if (!Dm.truthy(anchored))
            {
                balloon_alert(user, I18n.T("anchor first!"));
                return ITEM_INTERACT_BLOCKING;
            }
            if (HAS_TRAIT(weapon, TRAIT_NODROP))
                return await base.item_interaction(user, weapon, modifiers, __extra);
            if (!QDELETED(grilled_item))
            {
                balloon_alert(user, I18n.T("remove item first!"));
                return ITEM_INTERACT_BLOCKING;
            }
            if (grill_fuel <= 0)
            {
                balloon_alert(user, I18n.T("no fuel!"));
                return ITEM_INTERACT_BLOCKING;
            }
            if (!Dm.truthy(await user.transferItemToLoc(weapon, this)))
            {
                balloon_alert(user, I18n.T("[] is stuck in your hand!", weapon));
                return ITEM_INTERACT_BLOCKING;
            }

            //add the item on the grill
            grill_time = 0;
            grilled_item = weapon;
            datum_component_sizzle sizzle = grilled_item.GetComponent(typeof(datum_component_sizzle)) as datum_component_sizzle;
            if (sizzle != null)
                grill_time = Dm.num(sizzle.time_elapsed());
            to_chat(user, span_notice(I18n.T("You put the [] on [].", grilled_item, this)));
            update_appearance(UPDATE_ICON_STATE);
            grill_loop.start();
            return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }

    // tg: code/modules/food_and_drinks/machinery/grill.dm:177  item_interaction's var/static/list/fuel_map (rules 10.6)
    private static DmList item_interaction__fuel_map => TgState.Current.obj_machinery_grill__item_interaction__fuel_map ??= Dm.list(
        Dm.kv(typeof(datum_reagent_consumable_monkey_energy), 4),
        Dm.kv(typeof(datum_reagent_fuel_oil), 3),
        Dm.kv(typeof(datum_reagent_fuel), 2),
        Dm.kv(typeof(datum_reagent_consumable_ethanol), 1)
    );

    // tg: code/modules/food_and_drinks/machinery/grill.dm:240
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        object dot = ITEM_INTERACT_BLOCKING;
        if (Dm.eq(await default_unfasten_wrench(user, tool), SUCCESSFUL_UNFASTEN))
            return ITEM_INTERACT_SUCCESS;
        return dot;
    }

    // tg: code/modules/food_and_drinks/machinery/grill.dm:245
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(anchored))
        {
            balloon_alert(user, I18n.T("unanchor first!"));
            return ITEM_INTERACT_BLOCKING;
        }

        return await default_deconstruction_crowbar(user, tool);
    }

    // tg: code/modules/food_and_drinks/machinery/grill.dm:252
    public override object can_crowbar_deconstruct()
    {
        return Dm.num(!Dm.truthy(anchored));
    }

    // tg: code/modules/food_and_drinks/machinery/grill.dm:255
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (!Dm.truthy(anchored))
            return PROCESS_KILL;

        float fuel_usage = GRILL_FUELUSAGE_IDLE * Dm.num(seconds_per_tick);
        if (grill_fuel < fuel_usage)
        {
            grill_fuel = 0;
            Waiting.NoSleep(burn_stack());
            if (grill_fuel < fuel_usage) //could not make any fuel
                return PROCESS_KILL;
        }

        //use fuel, create smoke puffs for immersion
        grill_fuel -= fuel_usage;
        if (SPT_PROB(0.5f, seconds_per_tick))
            Waiting.NoSleep(do_smoke(1, this, loc, smoke_type: typeof(datum_effect_system_fluid_spread_smoke_bad)));

        fuel_usage = GRILL_FUELUSAGE_ACTIVE * Dm.num(seconds_per_tick);
        if (!QDELETED(grilled_item))
        {
            //check to see if we need to burn more fuel
            if (grill_fuel < fuel_usage)
            {
                Waiting.NoSleep(burn_stack());
                if (grill_fuel < fuel_usage) //could not make any fuel
                    return null;
            }

            //grill the item
            float last_grill_time = grill_time;
            grill_time += Dm.num(seconds_per_tick) * 10; //convert to deciseconds
            Waiting.NoSleep(((datum_reagents)grilled_item.reagents).add_reagent(typeof(datum_reagent_consumable_char), 0.5f * Dm.num(seconds_per_tick)));
            Waiting.NoSleep(grilled_item.AddComponent(typeof(datum_component_sizzle), grill_time));

            //check to see if we have grilled our item to perfection
            float time_limit = 20 * SECONDS;
            datum_component_grillable custom_grilling = grilled_item.GetComponent(typeof(datum_component_grillable)) as datum_component_grillable;
            if (custom_grilling != null)
                time_limit = custom_grilling.required_cook_time;
            if (grill_time >= time_limit)
            {
                grilled_item.RemoveElement(typeof(datum_element_grilled_item), last_grill_time);
                Waiting.NoSleep(grilled_item.AddElement(typeof(datum_element_grilled_item), grill_time));
            }

            //use fuel
            grill_fuel -= fuel_usage;
        }
        return null;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/food_and_drinks/machinery/grill.dm:177  /obj/machinery/grill/item_interaction's var/static/list/fuel_map
    internal DmList obj_machinery_grill__item_interaction__fuel_map;
}
