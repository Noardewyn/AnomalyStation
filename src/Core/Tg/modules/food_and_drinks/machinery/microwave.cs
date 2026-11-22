// Translated from tgstation (snapshot 1d99bd7f): code/modules/food_and_drinks/machinery/microwave.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package H2, D333): the microwave, its cooking, charging and cell, the hell microwave and the wireless
// (engineering) microwave. The var block is generated (D111); the type-level statics (the radial pictures and options) are TgState
// fields (rules 10.6). The [DmPath] stays here, the earliest .dme file that declares the type (rules 2.3).

// tg: code/modules/food_and_drinks/machinery/microwave.dm:20
[DmPath("/obj/machinery/microwave")]
public partial class obj_machinery_microwave : obj_machinery
{
    // tg: code/modules/food_and_drinks/machinery/microwave.dm:18  #define TIER_1_CELL_CHARGE_RATE (0.25 * STANDARD_CELL_CHARGE) (a file-local define)
    private const float TIER_1_CELL_CHARGE_RATE = 0.25f * STANDARD_CELL_CHARGE;

    // tg: code/modules/food_and_drinks/machinery/microwave.dm:4-15  file-local #defines (#undef at :953-962)
    /// Values based on microwave success
    private const float MICROWAVE_NORMAL = 0;
    private const float MICROWAVE_MUCK = 1;
    private const float MICROWAVE_PRE = 2;
    /// Values for how broken the microwave is
    private const float NOT_BROKEN = 0;
    private const float KINDA_BROKEN = 1;
    private const float REALLY_BROKEN = 2;
    /// The max amount of dirtiness a microwave can be
    private const float MAX_MICROWAVE_DIRTINESS = 100;
    // tg: code/modules/food_and_drinks/machinery/microwave.dm:220  #define MICROWAVE_INGREDIENT_OVERLAY_SIZE 24 (#undef at :297)
    private const float MICROWAVE_INGREDIENT_OVERLAY_SIZE = 24;

    // tg: code/modules/food_and_drinks/machinery/microwave.dm:82-89  the type's var/static radial pictures and options (rules 10.6)
    private static object radial_examine => TgState.Current.obj_machinery_microwave__radial_examine ??= image(icon: Dm.resource("icons/hud/radial.dmi"), icon_state: "radial_examine");
    private static object radial_eject => TgState.Current.obj_machinery_microwave__radial_eject ??= image(icon: Dm.resource("icons/hud/radial.dmi"), icon_state: "radial_eject");
    private static object radial_cook => TgState.Current.obj_machinery_microwave__radial_cook ??= image(icon: Dm.resource("icons/hud/radial.dmi"), icon_state: "radial_cook");
    private static object radial_charge => TgState.Current.obj_machinery_microwave__radial_charge ??= image(icon: Dm.resource("icons/hud/radial.dmi"), icon_state: "radial_charge");
    // we show the button even if the proc will not work
    private static DmList radial_options => TgState.Current.obj_machinery_microwave__radial_options ??= Dm.list(Dm.kv("eject", radial_eject), Dm.kv("cook", radial_cook), Dm.kv("charge", radial_charge));
    private static DmList ai_radial_options => TgState.Current.obj_machinery_microwave__ai_radial_options ??= Dm.list(Dm.kv("eject", radial_eject), Dm.kv("cook", radial_cook), Dm.kv("charge", radial_charge), Dm.kv("examine", radial_examine));

    // tg: code/modules/food_and_drinks/machinery/microwave.dm:91
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        register_context();
        set_wires(Dm.@new<datum_wires_microwave>(this));
        create_reagents(100);
        soundloop = Dm.@new<datum_looping_sound_microwave>(this, FALSE);
        update_appearance(UPDATE_ICON);
        return dot;
    }

    // tg: code/modules/food_and_drinks/machinery/microwave.dm:99
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        if (Dm.@in(gone, ingredients))
        {
            Dm.remove(ref ingredients, gone);
            if (!QDELING(gone) && Dm.truthy(ingredients.len) && isitem(gone))
            {
                obj_item itemized_ingredient = (obj_item)gone;
                if (!Dm.truthy(Dm.bitand(itemized_ingredient.item_flags, NO_PIXEL_RANDOM_DROP)))
                {
                    itemized_ingredient.pixel_x = itemized_ingredient.base_pixel_x + rand(-6, 6);
                    itemized_ingredient.pixel_y = itemized_ingredient.base_pixel_y + rand(-5, 6);
                }
            }
        }
        return await base.Exited(gone, direction);
    }

    // tg: code/modules/food_and_drinks/machinery/microwave.dm:109
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        await eject();
        return await base.on_deconstruction(disassembled);
    }

    // tg: code/modules/food_and_drinks/machinery/microwave.dm:113
    public override object Destroy(float? force = null)
    {
        QDEL_LIST(ingredients);
        QDEL_NULL(ref soundloop);
        remove_shared_particles(typeof(particles_smoke));
        if (cell != null)
            QDEL_NULL(ref cell);
        return base.Destroy(force);
    }

    // tg: code/modules/food_and_drinks/machinery/microwave.dm:121
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        object dot = base.add_context(source, context, held_item, user);
        if (Dm.truthy(cell_powered))
        {
            if (cell != null)
                context[SCREENTIP_CONTEXT_CTRL_LMB] = "Remove cell";
            else if (held_item != null && held_item is obj_item_stock_parts_power_store_cell)
                context[SCREENTIP_CONTEXT_CTRL_LMB] = "Insert cell";
        }

        if (Dm.eq(held_item?.tool_behaviour, TOOL_WRENCH))
        {
            context[SCREENTIP_CONTEXT_LMB] = Dm.text("[]", Dm.truthy(anchored) ? "Unsecure" : "Install/Secure");
            return CONTEXTUAL_SCREENTIP_SET;
        }

        if (held_item?.atom_storage != null)
        {
            context[SCREENTIP_CONTEXT_RMB] = "Dump contents";
            return CONTEXTUAL_SCREENTIP_SET;
        }

        if (broken > NOT_BROKEN)
        {
            if (broken == REALLY_BROKEN && Dm.eq(held_item?.tool_behaviour, TOOL_WIRECUTTER))
            {
                context[SCREENTIP_CONTEXT_LMB] = "Repair";
                return CONTEXTUAL_SCREENTIP_SET;
            }
            else if (broken == KINDA_BROKEN && Dm.eq(held_item?.tool_behaviour, TOOL_WELDER))
            {
                context[SCREENTIP_CONTEXT_LMB] = "Repair";
                return CONTEXTUAL_SCREENTIP_SET;
            }
        }

        context[SCREENTIP_CONTEXT_LMB] = "Show menu";

        if (Dm.truthy(vampire_charging_capable))
            context[SCREENTIP_CONTEXT_ALT_LMB] = Dm.text("Change to []", Dm.truthy(vampire_charging_enabled) ? "cook" : "charge");

        if (Dm.length(ingredients) != 0)
            context[SCREENTIP_CONTEXT_RMB] = Dm.text("Start []", Dm.truthy(vampire_charging_enabled) ? "charging" : "cooking");

        return CONTEXTUAL_SCREENTIP_SET;
    }

    // tg: code/modules/food_and_drinks/machinery/microwave.dm:156
    public override async DmTask<object> RefreshParts()
    {
        object dot = await base.RefreshParts();
        efficiency = 0;
        vampire_charging_capable = FALSE;
        foreach (datum_stock_part_micro_laser micro_laser in Dm.each<datum_stock_part_micro_laser>(component_parts))
            efficiency += Dm.num(micro_laser.tier);
        foreach (datum_stock_part_matter_bin matter_bin in Dm.each<datum_stock_part_matter_bin>(component_parts))
        {
            max_n_of_items = 10 * Dm.num(matter_bin.tier);
            break;
        }
        foreach (datum_stock_part_capacitor capacitor in Dm.each<datum_stock_part_capacitor>(component_parts))
        {
            if (Dm.num(capacitor.tier) >= 2)
            {
                vampire_charging_capable = TRUE;
                visible_message(span_notice(I18n.T("The [] light on \\the [] flickers to life.", EXAMINE_HINT(I18n.T("Charge Ready")), this)));
                break;
            }
        }
        return dot;
    }

    // tg: code/modules/food_and_drinks/machinery/microwave.dm:171
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(vampire_charging_capable))
        {
            Dm.append(ref dot, span_info(I18n.T("This model features Wave™: a Nanotrasen exclusive. Our latest and greatest, Wave™ allows your PDA to be charged wirelessly through microwave frequencies! You can Wave-charge your device by placing it inside and selecting the charge mode.")));
            Dm.append(ref dot, span_info(I18n.T("Because nothing says 'future' like charging your PDA while overcooking your leftovers. Nanotrasen Wave™ - Multitasking, redefined.")));
        }

        if (Dm.truthy(cell_powered))
            Dm.append(ref dot, span_notice(I18n.T("This model is wireless, powered by portable cells. []", cell == null ? I18n.T("The cell slot is empty.") : I18n.T("[] to remove the power cell.", EXAMINE_HINT(I18n.T("Ctrl-click"))))));

        if (!Dm.truthy(operating))
        {
            if (!Dm.truthy(operating) && Dm.truthy(vampire_charging_capable))
                Dm.append(ref dot, span_notice(I18n.T("[] to change default mode.", EXAMINE_HINT(I18n.T("Alt-click")))));

            Dm.append(ref dot, span_notice(I18n.T("[] to start [] cycle.", EXAMINE_HINT(I18n.T("Right-click")), Dm.truthy(vampire_charging_enabled) ? I18n.T("charging") : I18n.T("cooking"))));
        }

        if (!in_range(user, this) && !issilicon(user) && !isobserver(user))
        {
            Dm.append(ref dot, span_warning(I18n.T("You're too far away to examine []'s contents and display!", this)));
            return dot;
        }
        if (Dm.truthy(operating))
        {
            Dm.append(ref dot, span_notice(I18n.T("\\The [] is operating.", this)));
            return dot;
        }

        if (Dm.truthy(Dm.length(ingredients)))
        {
            if (issilicon(user))
                Dm.append(ref dot, span_notice(I18n.T("\\The [] camera shows:", this)));
            else
                Dm.append(ref dot, span_notice(I18n.T("\\The [] contains:", this)));
            DmList items_counts = Dm.list();
            foreach (object i in Dm.each(ingredients))
            {
                if (isstack(i))
                {
                    obj_item_stack item_stack = (obj_item_stack)i;
                    items_counts[item_stack.name] = Dm.add(items_counts[item_stack.name], item_stack.amount);
                }
                else
                {
                    atom_movable single_item = (atom_movable)i;
                    items_counts[single_item.name] = Dm.add(items_counts[single_item.name], 1);
                }
            }
            foreach (object item in Dm.each(items_counts))
                Dm.append(ref dot, span_notice(I18n.T("- []x [].", items_counts[item], item)));
        }
        else
            Dm.append(ref dot, span_notice(I18n.T("\\The [] is empty.", this)));

        if (!Dm.truthy(Dm.bitand(machine_stat, NOPOWER | BROKEN)))
        {
            Dm.append(ref dot, Dm.text("[]\n", span_notice(I18n.T("The status display reads:"))) +
                Dm.text("[]\n", span_notice(I18n.T("- Mode: <b>[]</b>.", Dm.truthy(vampire_charging_enabled) ? I18n.T("Charge") : I18n.T("Cook")))) +
                Dm.text("[]\n", span_notice(I18n.T("- Capacity: <b>[]</b> items.", max_n_of_items))) +
                span_notice(I18n.T("- Power: <b>[]W</b>.", efficiency * TIER_1_CELL_CHARGE_RATE)));

            if (Dm.truthy(cell_powered))
            {
                if (cell == null)
                    Dm.append(ref dot, span_notice(I18n.T("- Charge: <b>[]</b>.", I18n.T("INSERT CELL"))));
                else
                    Dm.append(ref dot, span_notice(I18n.T("- Charge: <b>[]</b>.", Dm.text("[]%", round(Dm.num(cell.percent()))))));
            }
        }
        return dot;
    }

    // tg: code/modules/food_and_drinks/machinery/microwave.dm:222
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);

        // All of these will use a full icon state instead
        if (Dm.truthy(panel_open) || dirty >= MAX_MICROWAVE_DIRTINESS || Dm.truthy(broken) || Dm.truthy(dirty_anim_playing))
            return dot;

        float ingredient_count = 0;

        foreach (atom_movable ingredient in Dm.each_cast<atom_movable>(ingredients))
        {
            image ingredient_overlay = image(ingredient, this);

            DmList icon_dimensions = get_icon_dimensions(ingredient.icon);
            ingredient_overlay.transform = ((matrix)ingredient_overlay.transform).Scale(
                MICROWAVE_INGREDIENT_OVERLAY_SIZE / Dm.num(icon_dimensions["width"]),
                MICROWAVE_INGREDIENT_OVERLAY_SIZE / Dm.num(icon_dimensions["height"])
            );

            ingredient_overlay.pixel_w = Dm.num(ingredient_shifts_x[Dm.num(Dm.mod(ingredient_count, ingredient_shifts_x.len)) + 1]);
            ingredient_overlay.pixel_z = Dm.num(ingredient_shifts_y[Dm.num(Dm.mod(ingredient_count, ingredient_shifts_y.len)) + 1]);
            ingredient_overlay.layer = FLOAT_LAYER;
            ingredient_overlay.plane = FLOAT_PLANE;
            ingredient_overlay.blend_mode = BLEND_INSET_OVERLAY;

            ingredient_count += 1;

            Dm.append(ref dot, ingredient_overlay);
        }

        string border_icon_state;
        string door_icon_state;

        if (Dm.truthy(open))
        {
            door_icon_state = Dm.text("[]door_open", base_icon_state);
            border_icon_state = Dm.text("[]mwo", base_icon_state);
        }
        else if (Dm.truthy(operating))
        {
            if (Dm.truthy(vampire_charging_enabled))
                door_icon_state = Dm.text("[]door_charge", base_icon_state);
            else
                door_icon_state = Dm.text("[]door_on", base_icon_state);
            border_icon_state = Dm.text("[]mw1", base_icon_state);
        }
        else
        {
            door_icon_state = Dm.text("[]door_off", base_icon_state);
            border_icon_state = Dm.text("[]mw", base_icon_state);
        }

        Dm.append(ref dot, mutable_appearance(
            icon,
            door_icon_state
        ));

        Dm.append(ref dot, border_icon_state);

        if (!Dm.truthy(open))
            Dm.append(ref dot, Dm.text("[]door_handle", base_icon_state));

        // DEVIATION(DV12): if(!(machine_stat & NOPOWER) || cell_powered) . += emissive_appearance(icon, "emissive_[border_icon_state]", src, alpha = src.alpha),
        // the glow on the emissive plane, is not created

        if (Dm.truthy(cell_powered) && cell != null)
        {
            // (each cell overlay's emissive_appearance copy is not created either, DV12)
            object sw__1 = cell.percent();
            if (Dm.in_to(sw__1, 75, 100))
                Dm.append(ref dot, mutable_appearance(icon, Dm.text("[]cell_100", base_icon_state)));
            else if (Dm.in_to(sw__1, 50, 75))
                Dm.append(ref dot, mutable_appearance(icon, Dm.text("[]cell_75", base_icon_state)));
            else if (Dm.in_to(sw__1, 25, 50))
                Dm.append(ref dot, mutable_appearance(icon, Dm.text("[]cell_25", base_icon_state)));
            else
                Dm.append(ref dot, mutable_appearance(icon, Dm.text("[]cell_0", base_icon_state)));
        }

        return dot;
    }

    // tg: code/modules/food_and_drinks/machinery/microwave.dm:299
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (Dm.truthy(broken))
            icon_state = Dm.text("[]mwb", base_icon_state);
        else if (Dm.truthy(dirty_anim_playing))
            icon_state = Dm.text("[]mwbloody1", base_icon_state);
        else if (dirty >= MAX_MICROWAVE_DIRTINESS)
            icon_state = Dm.truthy(open) ? Dm.text("[]mwbloodyo", base_icon_state) : Dm.text("[]mwbloody", base_icon_state);
        else if (Dm.truthy(operating))
            icon_state = Dm.text("[]back_on", base_icon_state);
        else if (Dm.truthy(open))
            icon_state = Dm.text("[]back_open", base_icon_state);
        else if (Dm.truthy(panel_open))
            icon_state = Dm.text("[]mw-o", base_icon_state);
        else
            icon_state = Dm.text("[]back_off", base_icon_state);

        return await base.update_icon_state(__extra);
    }

    // tg: code/modules/food_and_drinks/machinery/microwave.dm:317
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(await default_unfasten_wrench(user, tool)))
            update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/food_and_drinks/machinery/microwave.dm:322
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        return await default_deconstruction_crowbar(user, tool);
    }

    // tg: code/modules/food_and_drinks/machinery/microwave.dm:325
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        return default_deconstruction_screwdriver(user, tool);
    }

    // tg: code/modules/food_and_drinks/machinery/microwave.dm:328
    public override async DmTask<object> wirecutter_act(mob_living user = null, obj_item tool = null)
    {
        if (broken != REALLY_BROKEN)
            return NONE;

        user.visible_message(
            span_notice(I18n.T("[] starts to fix part of [].", user, this)),
            span_notice(I18n.T("You start to fix part of []...", this))
        );

        if (!Dm.truthy(await tool.use_tool(this, user, 2 * SECONDS, volume: 50)))
            return ITEM_INTERACT_BLOCKING;

        user.visible_message(
            span_notice(I18n.T("[] fixes part of [].", user, this)),
            span_notice(I18n.T("You fix part of [].", this))
        );
        broken = KINDA_BROKEN; // Fix it a bit
        update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/food_and_drinks/machinery/microwave.dm:348
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        if (broken != KINDA_BROKEN)
            return NONE;

        user.visible_message(
            span_notice(I18n.T("[] starts to fix part of [].", user, this)),
            span_notice(I18n.T("You start to fix part of []...", this))
        );

        if (!Dm.truthy(await tool.use_tool(this, user, 2 * SECONDS, amount: 1, volume: 50)))
            return ITEM_INTERACT_BLOCKING;

        user.visible_message(
            span_notice(I18n.T("[] fixes [].", user, this)),
            span_notice(I18n.T("You fix [].", this))
        );
        broken = NOT_BROKEN;
        update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/food_and_drinks/machinery/microwave.dm:368
    public override async DmTask<object> tool_act(mob_living user = null, obj_item tool = null, DmList modifiers = null)
    {
        if (!Dm.truthy(tool.tool_behaviour))
            return await base.tool_act(user, tool, modifiers);
        if (Dm.truthy(operating))
            return ITEM_INTERACT_SKIP_TO_ATTACK; // Don't use tools if we're operating
        if (dirty >= MAX_MICROWAVE_DIRTINESS)
            return ITEM_INTERACT_SKIP_TO_ATTACK; // Don't insert items if we're dirty
        if (Dm.truthy(panel_open) && Dm.truthy(is_wire_tool(tool)))
        {
            await wires.interact(user);
            return ITEM_INTERACT_SUCCESS;
        }
        return await base.tool_act(user, tool, modifiers);
    }

    // tg: code/modules/food_and_drinks/machinery/microwave.dm:380
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item item = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(operating))
            return NONE;

        if (Dm.truthy(Dm.bitand(item.item_flags, ABSTRACT)))
            return NONE;

        if (dirty >= MAX_MICROWAVE_DIRTINESS) // The microwave is all dirty so can't be used!
        {
            if (IS_EDIBLE(item))
            {
                balloon_alert(user, I18n.T("it's too dirty!"));
                return ITEM_INTERACT_BLOCKING;
            }
            return NONE;
        }

        if (broken > NOT_BROKEN)
        {
            if (IS_EDIBLE(item))
            {
                balloon_alert(user, I18n.T("it's broken!"));
                return ITEM_INTERACT_BLOCKING;
            }
            return NONE;
        }

        if (item is obj_item_stock_parts_power_store_cell && Dm.truthy(cell_powered))
        {
            float swapped = FALSE;
            if (cell != null)
            {
                await cell.forceMove(drop_location() as atom);
                if (!HAS_SILICON_ACCESS(user) && Dm.truthy(await Adjacent(user)))
                    await user.put_in_hands(cell);
                cell = null;
                swapped = TRUE;
            }
            if (!Dm.truthy(await user.transferItemToLoc(item, this)))
            {
                update_appearance();
                return ITEM_INTERACT_BLOCKING;
            }
            cell = item as obj_item_stock_parts_power_store;
            balloon_alert(user, I18n.T("[] cell", Dm.truthy(swapped) ? I18n.T("swapped") : I18n.T("inserted")));
            update_appearance();
            return ITEM_INTERACT_SUCCESS;
        }

        if (!Dm.truthy(anchored))
        {
            if (IS_EDIBLE(item))
            {
                balloon_alert(user, I18n.T("not secured!"));
                return ITEM_INTERACT_BLOCKING;
            }
            return NONE;
        }

        if (Dm.truthy(vampire_charging_capable) && item is obj_item_modular_computer && ingredients.len > 0)
        {
            balloon_alert(user, I18n.T("max 1 device!"));
            return ITEM_INTERACT_BLOCKING;
        }

        if (item.w_class <= WEIGHT_CLASS_NORMAL && !Dm.truthy(user.combat_mode) && item.atom_storage == null)
        {
            if (ingredients.len >= max_n_of_items)
            {
                balloon_alert(user, I18n.T("it's full!"));
                return ITEM_INTERACT_BLOCKING;
            }
            if (!Dm.truthy(await user.transferItemToLoc(item, this)))
            {
                balloon_alert(user, I18n.T("it's stuck to your hand!"));
                return ITEM_INTERACT_BLOCKING;
            }

            Dm.append(ref ingredients, item);
            open__proc(autoclose: 0.6f * SECONDS);
            user.visible_message(span_notice(I18n.T("[] adds \\a [] to \\the [].", user, item, this)), span_notice(I18n.T("You add [] to \\the [].", item, this)));
            update_appearance();
            return ITEM_INTERACT_SUCCESS;
        }
        return null;
    }

    // tg: code/modules/food_and_drinks/machinery/microwave.dm:439
    public override async DmTask<object> item_interaction_secondary(mob_living user = null, obj_item tool = null, DmList modifiers = null)
    {
        if (tool.atom_storage == null)
            return null;
        await handle_dumping(user, tool);
        return ITEM_INTERACT_BLOCKING;
    }

    // tg: code/modules/food_and_drinks/machinery/microwave.dm:445
    public virtual async DmTask handle_dumping(mob_living user = null, obj_item tool = null)
    {
        if (tool.atom_storage == null)
            return;

        float loaded = 0;
        if (tool is not obj_item_storage_bag_tray)
        {
            // Non-tray dumping requires a do_after
            to_chat(user, span_notice(I18n.T("You start dumping out the contents of [] into []...", tool, this)));
            if (!Dm.truthy(await do_after(user, 2 * SECONDS, target: tool)))
                return;
        }

        foreach (obj tray_item in Dm.each<obj>(tool.contents))
        {
            if (!IS_EDIBLE(tray_item))
                continue;
            if (ingredients.len >= max_n_of_items)
            {
                balloon_alert(user, I18n.T("it's full!"));
                return;
            }
            if (Dm.truthy(tool.atom_storage.attempt_remove(tray_item as obj_item, this)))
            {
                loaded++;
                Dm.append(ref ingredients, tray_item);
            }
        }

        if (Dm.truthy(loaded))
        {
            open__proc(autoclose: 0.6f * SECONDS);
            to_chat(user, span_notice(I18n.T("You insert [] items into \\the [].", loaded, this)));
            update_appearance();
        }
    }

    // tg: code/modules/food_and_drinks/machinery/microwave.dm:471
    public override async DmTask<object> mouse_drop_receive(atom tool = null, mob user = null, object @params = null)
    {
        if (tool is not obj_item tool__item || tool__item.atom_storage == null)
            return null;
        await handle_dumping(user as mob_living, tool__item);
        return null;
    }

    // tg: code/modules/food_and_drinks/machinery/microwave.dm:476
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        if (Dm.truthy(await user.can_perform_action(this, ALLOW_SILICON_REACH)))
        {
            if (!Dm.truthy(Dm.length(ingredients)))
            {
                balloon_alert(user, I18n.T("it's empty!"));
                return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
            }

            await start_cycle(user);
        }

        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/modules/food_and_drinks/machinery/microwave.dm:486
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        if (!Dm.truthy(vampire_charging_capable))
            return NONE;

        vampire_charging_enabled = Dm.num(!Dm.truthy(vampire_charging_enabled));
        balloon_alert(user, I18n.T("set to []", Dm.truthy(vampire_charging_enabled) ? I18n.T("charge") : I18n.T("cook")));
        playsound(this, Dm.resource("sound/machines/beep/twobeep_high.ogg"), 50, FALSE);
        if (HAS_SILICON_ACCESS(user))
            visible_message(span_notice(I18n.T("[] sets \\the [] to [].", user, this, Dm.truthy(vampire_charging_enabled) ? I18n.T("charge") : I18n.T("cook"))), blind_message: span_notice(I18n.T("You hear \\the [] make an informative beep!", this)));
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/modules/food_and_drinks/machinery/microwave.dm:497
    public override async DmTask<object> click_ctrl(mob user = null)
    {
        if (!Dm.truthy(anchored))
            return NONE;

        if (Dm.truthy(cell_powered) && cell != null)
        {
            await user.put_in_hands(cell);
            balloon_alert(user, I18n.T("removed cell"));
            cell = null;
            update_appearance();
            return CLICK_ACTION_SUCCESS;
        }

        return CLICK_ACTION_BLOCKING;
    }

    // tg: code/modules/food_and_drinks/machinery/microwave.dm:510
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        object dot = await base.ui_interact(user, ui, __extra);

        if (!Dm.truthy(anchored))
        {
            balloon_alert(user, I18n.T("not secured!"));
            return dot;
        }
        if (Dm.truthy(operating) || Dm.truthy(panel_open) || !Dm.truthy(await user.can_perform_action(this, ALLOW_SILICON_REACH)))
            return dot;
        if (HAS_AI_ACCESS(user) && Dm.truthy(Dm.bitand(machine_stat, NOPOWER)))
            return dot;

        if (!Dm.truthy(Dm.length(ingredients)))
        {
            if (HAS_AI_ACCESS(user))
                user.examinate(this);
            else
                balloon_alert(user, I18n.T("it's empty!"));
            return dot;
        }

        object choice = await show_radial_menu(user, this, HAS_AI_ACCESS(user) ? ai_radial_options : radial_options, require_near: !HAS_SILICON_ACCESS(user) ? TRUE : FALSE);

        // post choice verification
        if (Dm.truthy(operating) || Dm.truthy(panel_open) || (!Dm.truthy(vampire_charging_capable) && !Dm.truthy(anchored)) || !Dm.truthy(await user.can_perform_action(this, ALLOW_SILICON_REACH)))
            return dot;
        if (HAS_AI_ACCESS(user) && Dm.truthy(Dm.bitand(machine_stat, NOPOWER)))
            return dot;

        object sw__1 = choice;
        if (Dm.eq(sw__1, "eject"))
            await eject();
        else if (Dm.eq(sw__1, "cook"))
        {
            vampire_charging_enabled = FALSE;
            await start_cycle(user);
        }
        else if (Dm.eq(sw__1, "charge"))
        {
            vampire_charging_enabled = TRUE;
            await start_cycle(user);
        }
        else if (Dm.eq(sw__1, "examine"))
            user.examinate(this);
        return dot;
    }

    // tg: code/modules/food_and_drinks/machinery/microwave.dm:548
    public override async DmTask<object> wash(object clean_types = null, params object[] __extra)
    {
        object dot = await base.wash(clean_types, __extra);
        if (Dm.truthy(operating) || !Dm.truthy(Dm.bitand(clean_types, CLEAN_SCRUB)))
            return dot;

        dirty = 0;
        update_appearance();
        Dm.combine(ref dot, COMPONENT_CLEANED | COMPONENT_CLEANED_GAIN_XP);
        return dot;
    }

    // tg: code/modules/food_and_drinks/machinery/microwave.dm:557
    public virtual async DmTask eject()
    {
        atom drop_loc = drop_location() as atom;
        foreach (obj_item item_ingredient in Dm.each_cast<obj_item>(ingredients))
        {
            await item_ingredient.forceMove(drop_loc);
            await item_ingredient.dropped(); //Mob holders can be on the ground if we don't do this
        }
        open__proc(autoclose: 1.4f * SECONDS);
    }

    // tg: code/modules/food_and_drinks/machinery/microwave.dm:564
    public virtual async DmTask start_cycle(mob user = null)
    {
        if (Dm.truthy(wire_mode_swap))
        {
            spark();
            if (Dm.truthy(vampire_charging_enabled))
                await cook(user);
            else
                await charge(user);
        }
        else if (Dm.truthy(vampire_charging_enabled))
            await charge(user);
        else
            await cook(user);
    }

    /**
     * Begins the process of cooking the included ingredients.
     *
     * * cooker - The mob that initiated the cook cycle, can be null if no apparent mob triggered it (such as via emp)
     */
    // tg: code/modules/food_and_drinks/machinery/microwave.dm:582
    public virtual async DmTask cook(mob cooker = null)
    {
        if (Dm.truthy(Dm.bitand(machine_stat, NOPOWER | BROKEN)))
            return;

        if (Dm.truthy(operating) || broken > 0 || Dm.truthy(panel_open) || !Dm.truthy(anchored) || dirty >= MAX_MICROWAVE_DIRTINESS)
            return;

        if (Dm.truthy(wire_disabled))
        {
            audible_message(I18n.T("[] buzzes.", this));
            playsound(this, Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 50, FALSE);
            return;
        }

        if (Dm.truthy(cell_powered) && Dm.num(cell?.charge) < TIER_1_CELL_CHARGE_RATE * efficiency)
        {
            playsound(this, Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 50, FALSE);
            balloon_alert(cooker, I18n.T("no power draw!"));
            return;
        }

        if (cooker != null && HAS_TRAIT(cooker, TRAIT_CURSED) && prob(7))
        {
            await muck();
            return;
        }

        if (prob(max(Dm.num(Dm.div(5, efficiency)) - 5, dirty * 5))) //a clean unupgraded microwave has no risk of failure
        {
            await muck();
            return;
        }

        // How many items are we cooking that aren't already food items
        float non_food_ingedients = Dm.length(ingredients);
        foreach (atom_movable potential_fooditem in Dm.each_cast<atom_movable>(ingredients))
        {
            if (IS_EDIBLE(potential_fooditem))
                non_food_ingedients--;
            if (potential_fooditem is obj_item_modular_computer && prob(75))
            {
                pda_failure = TRUE;
                notify_ghosts(
                    I18n.T("[] has overheated their PDA!", cooker.real_name),
                    source: this,
                    notify_flags: NOTIFY_CATEGORY_NOFLASH,
                    header: "Hunger Games: Catching Fire"
                );
            }
        }

        // If we're cooking non-food items we can fail randomly
        // (tg takes the length of a number, which is always 0 (BYOND ref length), so this branch never runs)
        if (Dm.truthy(Dm.length(non_food_ingedients)) && prob(min(dirty * 5, 100)))
        {
            await start_can_fail(cooker);
            return;
        }

        await start(cooker);
    }

    // tg: code/modules/food_and_drinks/machinery/microwave.dm:628
    public virtual async DmTask wzhzhzh()
    {
        if (Dm.truthy(cell_powered) && cell != null)
        {
            if (!Dm.truthy(await cell.use(TIER_1_CELL_CHARGE_RATE * efficiency)))
            {
                playsound(this, Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 50, FALSE);
                return;
            }
        }

        visible_message(span_notice(I18n.T("\\The [] turns on.", this)), null, span_hear(I18n.T("You hear a microwave humming.")));
        operating = TRUE;
        await set_light(l_range: 1.5f, l_power: 1.2f, l_on: TRUE);
        soundloop.start();
        update_appearance();
    }

    // tg: code/modules/food_and_drinks/machinery/microwave.dm:640
    public virtual void spark()
    {
        visible_message(span_warning(I18n.T("Sparks fly around []!", this)));
        do_sparks(2, TRUE, this);
    }

    /**
     * The start of the cook loop
     *
     * * cooker - The mob that initiated the cook cycle, can be null if no apparent mob triggered it (such as via emp)
     */
    // tg: code/modules/food_and_drinks/machinery/microwave.dm:649
    public virtual async DmTask start(mob cooker = null)
    {
        await wzhzhzh();
        await cook_loop(type: MICROWAVE_NORMAL, cycles: 10, cooker: cooker);
    }

    /**
     * The start of the cook loop, but can fail (result in a splat / dirty microwave)
     *
     * * cooker - The mob that initiated the cook cycle, can be null if no apparent mob triggered it (such as via emp)
     */
    // tg: code/modules/food_and_drinks/machinery/microwave.dm:658
    public virtual async DmTask start_can_fail(mob cooker = null)
    {
        await wzhzhzh();
        await cook_loop(type: MICROWAVE_PRE, cycles: 4, cooker: cooker);
    }

    // tg: code/modules/food_and_drinks/machinery/microwave.dm:662
    public virtual async DmTask muck()
    {
        await wzhzhzh();
        playsound(loc, Dm.resource("sound/effects/splat.ogg"), 50, TRUE);
        dirty_anim_playing = TRUE;
        update_appearance();
        await cook_loop(type: MICROWAVE_MUCK, cycles: 4);
    }

    /**
     * The actual cook loop started via [proc/start] or [proc/start_can_fail]
     *
     * * type - the type of cooking, determined via how this iteration of cook_loop is called, and determines the result
     * * time - how many loops are left, base case for recursion
     * * wait - deciseconds between loops
     * * cooker - The mob that initiated the cook cycle, can be null if no apparent mob triggered it (such as via emp)
     */
    // tg: code/modules/food_and_drinks/machinery/microwave.dm:677
    public virtual async DmTask cook_loop(object type = null, object cycles = null, object wait = null, mob cooker = null) // standard wait is 10
    {
        wait ??= max(12 - 2 * efficiency, 2);
        if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)) && Dm.eq(type, MICROWAVE_PRE))
        {
            await pre_fail();
            return;
        }

        if (Dm.num(cycles) <= 0 || !Dm.truthy(Dm.length(ingredients)))
        {
            object sw__1 = type;
            if (Dm.eq(sw__1, MICROWAVE_NORMAL))
                await loop_finish(cooker);
            else if (Dm.eq(sw__1, MICROWAVE_MUCK))
                await muck_finish();
            else if (Dm.eq(sw__1, MICROWAVE_PRE))
                await pre_success(cooker);
            return;
        }

        if (Dm.eq(cycles, 1)) //Only needs to try to shock mobs once, towards the end of the loop
        {
            float successful_shock = FALSE;
            DmList microwave_contents = Dm.list();
            Dm.append(ref microwave_contents, get_all_contents()); //Mobs are often hid inside of mob holders, which could be fried and made into a burger...
            foreach (mob_living victim in Dm.each<mob_living>(microwave_contents))
            {
                if (Dm.truthy(await victim.electrocute_act(shock_damage: 100, source: this, siemens_coeff: 1, flags: SHOCK_NOGLOVES)))
                {
                    successful_shock = TRUE;
                    if (victim.stat == DEAD) //This is mostly so humans that have the can_be_held element don't get gibbed from one microwave run alone, but mice become burnt messes
                    {
                        await victim.gib();
                        await muck();
                    }
                }
            }
            if (Dm.truthy(successful_shock)) //We only want to give feedback once, regardless of how many mobs got shocked
            {
                DmList cant_smell = Dm.list();
                foreach (mob smeller in Dm.each<mob>(get_hearers_in_view(DEFAULT_MESSAGE_RANGE, this)))
                {
                    if (HAS_TRAIT(smeller, TRAIT_ANOSMIA))
                        Dm.append(ref cant_smell, smeller);
                }
                visible_message(span_danger(I18n.T("You smell a burnt smell coming from []!", this)), ignored_mobs: cant_smell);
                add_shared_particles(typeof(particles_smoke));
                _addtimer(Dm.@new<datum_callback>(this, nameof(atom_movable.remove_shared_particles), typeof(particles_smoke)), 10 * SECONDS, file: "code/modules/food_and_drinks/machinery/microwave.dm", line: 709);
                Shake(duration: 1 * SECONDS);
            }
        }

        cycles = Dm.num(cycles) - 1;
        await use_energy(active_power_usage);
        _addtimer(Dm.@new<datum_callback>(this, nameof(cook_loop), type, cycles, wait, cooker), Dm.num(wait), file: "code/modules/food_and_drinks/machinery/microwave.dm", line: 714);
    }

    // tg: code/modules/food_and_drinks/machinery/microwave.dm:716  (the earlier of the two definitions in this type; the later one at
    // :882 calls it through ..(), rules 2.5)
    public object power_change__d0()
    {
        object dot = base.power_change();
        if (Dm.truthy(cell_powered))
            return dot;

        if (Dm.truthy(Dm.bitand(machine_stat, NOPOWER)) && Dm.truthy(operating))
        {
            Waiting.NoSleep(pre_fail());
            Waiting.NoSleep(eject());
        }
        return dot;
    }

    /**
     * Called when the cook_loop is done successfully, no dirty mess or whatever
     *
     * * cooker - The mob that initiated the cook cycle, can be null if no apparent mob triggered it (such as via emp)
     */
    // tg: code/modules/food_and_drinks/machinery/microwave.dm:730
    public virtual async DmTask loop_finish(mob cooker = null)
    {
        operating = FALSE;
        if (Dm.truthy(pda_failure))
        {
            spark();
            pda_failure = FALSE; // in case they repair it after this, reset
            broken = REALLY_BROKEN;
            await explosion(this, heavy_impact_range: 1, light_impact_range: 2, flame_range: 1);
        }

        bool cursed_chef = cooker != null && HAS_TRAIT(cooker, TRAIT_CURSED);
        float metal_amount = 0;
        foreach (obj_item cooked_item in Dm.each<obj_item>(ingredients))
        {
            object sigreturn = cooked_item.microwave_act(this, cooker, randomize_pixel_offset: ingredients.len);
            if (Dm.truthy(Dm.bitand(sigreturn, COMPONENT_MICROWAVE_SUCCESS)))
            {
                if (isstack(cooked_item))
                {
                    obj_item_stack cooked_stack = (obj_item_stack)cooked_item;
                    dirty += Dm.num(cooked_stack.amount);
                }
                else
                    dirty++;
            }

            metal_amount += Dm.num(cooked_item.custom_materials?[await SSmaterials.get_material(typeof(datum_material_iron))] is var or__1 && Dm.truthy(or__1) ? or__1 : 0);
        }

        if (cursed_chef && (Dm.truthy(metal_amount) || prob(5))) // If we're unlucky and have metal, we're guaranteed to explode
        {
            spark();
            broken = REALLY_BROKEN;
            await explosion(this, light_impact_range: 2, flame_range: 1);
        }

        if (Dm.truthy(metal_amount))
        {
            spark();
            broken = REALLY_BROKEN;
            if (prob(max(metal_amount / 2, 33)))
                await explosion(this, heavy_impact_range: 1, light_impact_range: 2);
        }

        await after_finish_loop();
    }

    // tg: code/modules/food_and_drinks/machinery/microwave.dm:764
    public virtual async DmTask pre_fail()
    {
        broken = REALLY_BROKEN;
        operating = FALSE;
        spark();
        await after_finish_loop();
    }

    // tg: code/modules/food_and_drinks/machinery/microwave.dm:770
    public virtual async DmTask pre_success(mob cooker = null)
    {
        await cook_loop(type: MICROWAVE_NORMAL, cycles: 10, cooker: cooker);
    }

    // tg: code/modules/food_and_drinks/machinery/microwave.dm:773
    public virtual async DmTask muck_finish()
    {
        visible_message(span_warning(I18n.T("\\The [] gets covered in muck!", this)));

        dirty = MAX_MICROWAVE_DIRTINESS;
        dirty_anim_playing = FALSE;
        operating = FALSE;

        await after_finish_loop();
    }

    // tg: code/modules/food_and_drinks/machinery/microwave.dm:782
    public virtual async DmTask after_finish_loop()
    {
        await set_light(l_on: FALSE);
        soundloop.stop();
        await eject();
        open__proc(autoclose: 2 * SECONDS);
    }

    // tg: code/modules/food_and_drinks/machinery/microwave.dm:788
    public virtual void open__proc(float? autoclose = null)
    {
        autoclose ??= 2 * SECONDS;
        open = TRUE;
        playsound(loc, Dm.resource("sound/machines/click.ogg"), 15, TRUE, -3);
        update_appearance();
        _addtimer(Dm.@new<datum_callback>(this, nameof(close)), autoclose, file: "code/modules/food_and_drinks/machinery/microwave.dm", line: 792);
    }

    // tg: code/modules/food_and_drinks/machinery/microwave.dm:794
    public virtual void close()
    {
        open = FALSE;
        update_appearance();
    }

    /**
     * The start of the charge loop
     *
     * * cooker - The mob that initiated the cook cycle, can be null if no apparent mob triggered it (such as via emp)
     */
    // tg: code/modules/food_and_drinks/machinery/microwave.dm:803
    public virtual async DmTask vampire(mob cooker = null)
    {
        obj_item_modular_computer vampire_pda = LAZYACCESS(ingredients, 1) as obj_item_modular_computer;
        if (vampire_pda == null)
        {
            playsound(this, Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 50, FALSE);
            await after_finish_loop();
            return;
        }

        vampire_cell = vampire_pda.internal_cell as obj_item_stock_parts_power_store;
        if (vampire_cell == null)
        {
            playsound(this, Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 50, FALSE);
            await after_finish_loop();
            return;
        }

        await wzhzhzh();
        float vampire_charge_amount = vampire_cell.maxcharge - vampire_cell.charge;
        await charge_loop(vampire_charge_amount, cooker: cooker);
    }

    // tg: code/modules/food_and_drinks/machinery/microwave.dm:820
    public virtual async DmTask charge(mob cooker = null)
    {
        if (!Dm.truthy(vampire_charging_capable))
        {
            balloon_alert(cooker, I18n.T("needs upgrade!"));
            playsound(this, Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 50, FALSE);
            return;
        }

        if (Dm.truthy(operating) || broken > 0 || Dm.truthy(panel_open) || dirty >= MAX_MICROWAVE_DIRTINESS)
            return;

        if (Dm.truthy(wire_disabled))
        {
            audible_message(I18n.T("[] buzzes.", this));
            playsound(this, Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 50, FALSE);
            return;
        }

        // We should only be charging PDAs
        foreach (atom_movable potential_item in Dm.each_cast<atom_movable>(ingredients))
        {
            if (potential_item is not obj_item_modular_computer)
            {
                balloon_alert(cooker, I18n.T("pda only!"));
                playsound(this, Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 50, FALSE);
                await eject();
                return;
            }
        }

        await vampire(cooker);
    }

    /**
     * The actual cook loop started via [proc/start] or [proc/start_can_fail]
     *
     * * type - the type of charging, determined via how this iteration of cook_loop is called, and determines the result
     * * time - how many loops are left, base case for recursion
     * * wait - deciseconds between loops
     * * cooker - The mob that initiated the cook cycle, can be null if no apparent mob triggered it (such as via emp)
     */
    // tg: code/modules/food_and_drinks/machinery/microwave.dm:852
    public virtual async DmTask charge_loop(object vampire_charge_amount = null, object wait = null, mob cooker = null) // standard wait is 10
    {
        wait ??= max(12 - 2 * efficiency, 2);
        if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
        {
            await pre_fail();
            return;
        }

        if (Dm.truthy(cell_powered) && (cell == null || !Dm.truthy(cell.charge)))
        {
            playsound(this, Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 50, FALSE);
            balloon_alert(cooker, I18n.T("no power draw!"));
            return;
        }

        if (!Dm.truthy(vampire_charge_amount) || !Dm.truthy(Dm.length(ingredients)) || Dm.num(vampire_charge_amount) < 25)
        {
            vampire_cell = null;
            await charge_loop_finish(cooker);
            return;
        }

        float charge_rate = vampire_cell.chargerate * (1 + ((efficiency - 1) * 0.25f));
        if (charge_rate > Dm.num(vampire_charge_amount))
            charge_rate = Dm.num(vampire_charge_amount);

        if (Dm.truthy(cell_powered) && !Dm.truthy(await cell.use(charge_rate)))
            await charge_loop_finish(cooker);

        await use_energy(charge_rate * (0.5f - efficiency * 0.12f)); //Some of the power gets lost as heat.
        await charge_cell__proc(charge_rate * (0.5f + efficiency * 0.12f), vampire_cell); //Cell gets charged, which further uses power.


        vampire_charge_amount = vampire_cell.maxcharge - vampire_cell.charge;

        _addtimer(Dm.@new<datum_callback>(this, nameof(charge_loop), vampire_charge_amount, wait, cooker), Dm.num(wait), file: "code/modules/food_and_drinks/machinery/microwave.dm", line: 880);
    }

    // tg: code/modules/food_and_drinks/machinery/microwave.dm:882  (the later definition: its ..() is the earlier one at :716, rules 2.5)
    public override object power_change()
    {
        object dot = power_change__d0();
        if (Dm.truthy(Dm.bitand(machine_stat, NOPOWER)) && Dm.truthy(operating))
        {
            Waiting.NoSleep(pre_fail());
            Waiting.NoSleep(eject());
        }
        return dot;
    }

    /**
     * Called when the charge_loop is done successfully, no dirty mess or whatever
     *
     * * cooker - The mob that initiated the cook cycle, can be null if no apparent mob triggered it (such as via emp)
     */
    // tg: code/modules/food_and_drinks/machinery/microwave.dm:893
    public virtual async DmTask charge_loop_finish(mob cooker = null)
    {
        operating = FALSE;
        bool cursed_chef = cooker != null && HAS_TRAIT(cooker, TRAIT_CURSED);
        if (cursed_chef && prob(5))
        {
            spark();
            broken = REALLY_BROKEN;
            await explosion(this, light_impact_range: 2, flame_range: 1);
        }

        // playsound(src, 'sound/machines/chime.ogg', 50, FALSE)
        await after_finish_loop();
    }
}

/// Type of microwave that automatically turns it self on erratically. Probably don't use this outside of the holodeck program "Microwave Paradise".
/// You could also live your life with a microwave that will continously run in the background of everything while also not having any power draw. I think the former makes more sense.
// tg: code/modules/food_and_drinks/machinery/microwave.dm:906
public partial class obj_machinery_microwave_hell : obj_machinery_microwave
{
    // tg: code/modules/food_and_drinks/machinery/microwave.dm:912
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        //We want there to be some chance of them getting a working microwave (eventually).
        if (prob(95))
            //The microwave should turn off asynchronously from any other microwaves that initialize at the same time. Keep in mind this will not turn off, since there is nothing to call the proc that ends this microwave's looping
            _addtimer(Dm.@new<datum_callback>(this, nameof(wzhzhzh)), rand(0.5f * SECONDS, 3 * SECONDS), file: "code/modules/food_and_drinks/machinery/microwave.dm", line: 917);
        return dot;
    }
}

// tg: code/modules/food_and_drinks/machinery/microwave.dm:919
public partial class obj_machinery_microwave_engineering : obj_machinery_microwave
{
    // tg: code/modules/food_and_drinks/machinery/microwave.dm:943
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(mapload))
            cell = Dm.@new<obj_item_stock_parts_power_store_cell_upgraded_plus>();
        update_appearance();
        return dot;
    }
}

// tg: code/modules/food_and_drinks/machinery/microwave.dm:949
public partial class obj_machinery_microwave_engineering_cell_included : obj_machinery_microwave_engineering
{
    // tg: code/modules/food_and_drinks/machinery/microwave.dm:949
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        cell = Dm.@new<obj_item_stock_parts_power_store_cell_upgraded_plus>();
        update_appearance();
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/food_and_drinks/machinery/microwave.dm:82-89  /obj/machinery/microwave's var/static radial pictures and options
    internal object obj_machinery_microwave__radial_examine;
    internal object obj_machinery_microwave__radial_eject;
    internal object obj_machinery_microwave__radial_cook;
    internal object obj_machinery_microwave__radial_charge;
    internal DmList obj_machinery_microwave__radial_options;
    internal DmList obj_machinery_microwave__ai_radial_options;
}
