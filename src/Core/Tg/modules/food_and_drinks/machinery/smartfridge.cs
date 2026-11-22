// Translated from tgstation (snapshot 1d99bd7f): code/modules/food_and_drinks/machinery/smartfridge.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package H2, D333): the smart fridges (plants, drinks, food, slime extracts, petri dishes, organs, chemistry,
// virology, disks), the dehydrator and the drying rack, with the SmartVend window. The var blocks are generated (D111); the
// proc-level statics are TgState fields (rules 10.6).

// -------------------------
//  SmartFridge.  Much todo
// -------------------------
// tg: code/modules/food_and_drinks/machinery/smartfridge.dm:4
public partial class obj_machinery_smartfridge : obj_machinery
{
    // tg: code/modules/food_and_drinks/machinery/smartfridge.dm:38
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        create_reagents(100, NO_REACT);
        air_update_turf(TRUE, TRUE);
        register_context();
        if (Dm.truthy(mapload) && Dm.truthy(can_be_welded_down))
            welded_down = TRUE;

        if (islist(initial_contents))
        {
            foreach (object typekey in Dm.each(initial_contents))
            {
                object amount = initial_contents[typekey];
                if (amount == null)
                    amount = 1;
                for (float i = 1, i__end = Dm.num(amount); i <= i__end; i++)
                    Waiting.NoSleep(load(Dm.@new((Type)typekey, this) as obj_item));
            }
        }
        return dot;
    }

    // tg: code/modules/food_and_drinks/machinery/smartfridge.dm:54
    public override async DmTask<object> Move(atom newloc = null, object direct = null, float? glide_size_override = null, float? update_dir = null)
    {
        turf old_loc = loc as turf;
        object dot = await base.Move(newloc, direct, glide_size_override, update_dir);
        move_update_air(old_loc);
        return dot;
    }

    // tg: code/modules/food_and_drinks/machinery/smartfridge.dm:59
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(can_be_welded_down))
            return await base.welder_act(user, tool);
        if (Dm.truthy(welded_down))
        {
            if (!Dm.truthy(tool.tool_start_check(user, amount: 2)))
                return ITEM_INTERACT_BLOCKING;

            user.visible_message(
                span_notice(I18n.T("[] starts to cut \\the [] free from the floor.", user.name, this)),
                span_notice(I18n.T("You start to cut [] free from the floor...", this)),
                span_hear(I18n.T("You hear welding."))
            );

            if (!Dm.truthy(await tool.use_tool(this, user, delay: 100, volume: 100)))
                return ITEM_INTERACT_BLOCKING;

            welded_down = FALSE;
            to_chat(user, span_notice(I18n.T("You cut [] free from the floor.", this)));
            return ITEM_INTERACT_SUCCESS;
        }

        if (!Dm.truthy(anchored))
        {
            balloon_alert(user, I18n.T("wrench it first!"));
            return ITEM_INTERACT_BLOCKING;
        }

        if (!Dm.truthy(tool.tool_start_check(user, amount: 2)))
            return ITEM_INTERACT_BLOCKING;

        user.visible_message(
            span_notice(I18n.T("[] starts to weld \\the [] to the floor.", user.name, this)),
            span_notice(I18n.T("You start to weld [] to the floor...", this)),
            span_hear(I18n.T("You hear welding."))
        );

        if (!Dm.truthy(await tool.use_tool(this, user, delay: 100, volume: 100)))
            return ITEM_INTERACT_BLOCKING;

        welded_down = TRUE;
        to_chat(user, span_notice(I18n.T("You weld [] to the floor.", this)));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/food_and_drinks/machinery/smartfridge.dm:99
    public override async DmTask<object> welder_act_secondary(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
        {
            balloon_alert(user, I18n.T("no repair needed!"));
            return ITEM_INTERACT_BLOCKING;
        }

        if (!Dm.truthy(tool.tool_start_check(user, amount: 1)))
            return ITEM_INTERACT_BLOCKING;

        user.visible_message(
            span_notice(I18n.T("[] is repairing [].", user, this)),
            span_notice(I18n.T("You begin repairing []...", this)),
            span_hear(I18n.T("You hear welding."))
        );

        if (Dm.truthy(await tool.use_tool(this, user, delay: 40, volume: 50)))
        {
            if (!Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
                return ITEM_INTERACT_BLOCKING;
            to_chat(user, span_notice(I18n.T("You repair []", this)));
            atom_integrity = max_integrity;
            await set_machine_stat(Dm.bitand(machine_stat, Dm.bitnot(BROKEN)));
            await update_icon();
        }
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/food_and_drinks/machinery/smartfridge.dm:122
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        return default_deconstruction_screwdriver(user, tool);
    }

    // tg: code/modules/food_and_drinks/machinery/smartfridge.dm:125
    public override object can_be_unfasten_wrench(mob user = null, object silent = null)
    {
        if (Dm.truthy(welded_down))
        {
            balloon_alert(user, I18n.T("unweld first!"));
            return FAILED_UNFASTEN;
        }
        return base.can_be_unfasten_wrench(user, silent);
    }

    // tg: code/modules/food_and_drinks/machinery/smartfridge.dm:131
    public override async DmTask<object> set_anchored(object anchorvalue = null)
    {
        object dot = await base.set_anchored(anchorvalue);
        if (!Dm.truthy(anchored) && Dm.truthy(welded_down)) //make sure they're keep in sync in case it was forcibly unanchored by badmins or by a megafauna.
            welded_down = FALSE;
        recheck_atmos_passing();
        return dot;
    }

    // tg: code/modules/food_and_drinks/machinery/smartfridge.dm:137
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.eq(await default_unfasten_wrench(user, tool), SUCCESSFUL_UNFASTEN))
        {
            power_change();
            return ITEM_INTERACT_SUCCESS;
        }
        return null;
    }

    // tg: code/modules/food_and_drinks/machinery/smartfridge.dm:142
    public override object can_crowbar_deconstruct()
    {
        return (base.can_crowbar_deconstruct() is var and__1 && !Dm.truthy(and__1) ? and__1 : Dm.num(!Dm.truthy(welded_down)));
    }

    // tg: code/modules/food_and_drinks/machinery/smartfridge.dm:145
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        return await default_pry_open(user, tool, close_after_pry: TRUE, deconstruct_on_fail: TRUE);
    }

    // tg: code/modules/food_and_drinks/machinery/smartfridge.dm:148
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        if (held_item == null)
            return NONE;

        float tool_tip_set = FALSE;
        if (Dm.eq(held_item.tool_behaviour, TOOL_WELDER))
        {
            if (Dm.truthy(welded_down))
            {
                context[SCREENTIP_CONTEXT_LMB] = "Unweld";
                tool_tip_set = TRUE;
            }
            else if (!Dm.truthy(welded_down) && Dm.truthy(anchored) && Dm.truthy(can_be_welded_down))
            {
                context[SCREENTIP_CONTEXT_LMB] = "Weld down";
                tool_tip_set = TRUE;
            }
            if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
            {
                context[SCREENTIP_CONTEXT_RMB] = "Repair";
                tool_tip_set = TRUE;
            }
        }
        else if (Dm.eq(held_item.tool_behaviour, TOOL_SCREWDRIVER))
        {
            context[SCREENTIP_CONTEXT_LMB] = Dm.text("[] panel", Dm.truthy(panel_open) ? "close" : "open");
            tool_tip_set = TRUE;
        }
        else if (Dm.eq(held_item.tool_behaviour, TOOL_CROWBAR))
        {
            if (Dm.truthy(panel_open))
            {
                context[SCREENTIP_CONTEXT_LMB] = "Deconstruct";
                tool_tip_set = TRUE;
            }
        }
        else if (Dm.eq(held_item.tool_behaviour, TOOL_WRENCH))
        {
            context[SCREENTIP_CONTEXT_LMB] = Dm.text("[]nchor", Dm.truthy(anchored) ? "Una" : "A");
            tool_tip_set = TRUE;
        }

        return Dm.truthy(tool_tip_set) ? CONTEXTUAL_SCREENTIP_SET : NONE;
    }

    // tg: code/modules/food_and_drinks/machinery/smartfridge.dm:179
    public override async DmTask<object> RefreshParts()
    {
        object dot = await base.RefreshParts();
        foreach (datum_stock_part_matter_bin matter_bin in Dm.each<datum_stock_part_matter_bin>(component_parts))
            max_n_of_items = Dm.initial(this, (obj_machinery_smartfridge f) => f.max_n_of_items) * Dm.num(matter_bin.tier);
        return dot;
    }

    // tg: code/modules/food_and_drinks/machinery/smartfridge.dm:184
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);

        if (Dm.truthy(in_range(user, this)) || isobserver(user))
            Dm.append(ref dot, status_examine());

        Dm.append(ref dot, structure_examine());
        return dot;
    }

    // tg: code/modules/food_and_drinks/machinery/smartfridge.dm:192
    public override async DmTask on_set_machine_stat(object old_value = null)
    {
        await base.on_set_machine_stat(old_value);
        recheck_atmos_passing();
        if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
        {
            pass_flags_self = PASSMACHINE;
            return;
        }
        pass_flags_self = PASSCLOSEDTURF;
    }

    /// Returns details related to the fridge structure
    // tg: code/modules/food_and_drinks/machinery/smartfridge.dm:201
    public virtual object structure_examine()
    {
        object dot = Dm.list();

        if (Dm.truthy(welded_down))
            Dm.append(ref dot, span_info(I18n.T("It's moorings are firmly [] to the floor.", EXAMINE_HINT(I18n.T("welded")))));
        else if (Dm.truthy(can_be_welded_down))
            Dm.append(ref dot, span_info(I18n.T("It's moorings are loose and can be [] down.", EXAMINE_HINT(I18n.T("welded")))));

        if (Dm.truthy(anchored))
            Dm.append(ref dot, span_info(I18n.T("It is [] down on the floor.", EXAMINE_HINT(I18n.T("wrenched")))));
        else
            Dm.append(ref dot, span_info(I18n.T("It could be [] down.", EXAMINE_HINT(I18n.T("wrenched")))));
        return dot;
    }

    /// Returns details related to the fridge status
    // tg: code/modules/food_and_drinks/machinery/smartfridge.dm:215
    public virtual object status_examine()
    {
        object dot = Dm.list();

        Dm.append(ref dot, span_notice(I18n.T("The status display reads: This unit can hold a maximum of <b>[]</b> items.", max_n_of_items)));
        return dot;
    }

    // tg: code/modules/food_and_drinks/machinery/smartfridge.dm:220
    public override object update_appearance(float? updates = null)
    {
        updates ??= ALL;
        object dot = base.update_appearance(updates);

        Waiting.NoSleep(set_light((!Dm.truthy(Dm.bitand(machine_stat, BROKEN)) && Dm.truthy(powered__proc())) ? MINIMUM_USEFUL_LIGHT_RANGE : 0));
        return dot;
    }

    // tg: code/modules/food_and_drinks/machinery/smartfridge.dm:225
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("[]", base_icon_state);
        if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
            icon_state += "-broken";
        return await base.update_icon_state(__extra);
    }

    /// Returns the number of items visible in the fridge. Faster than subtracting 2 lists
    // tg: code/modules/food_and_drinks/machinery/smartfridge.dm:232
    public virtual object visible_items()
    {
        return contents.len - 1; // Exclude circuitboard
    }

    // tg: code/modules/food_and_drinks/machinery/smartfridge.dm:235
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);

        if (Dm.truthy(panel_open))
            Dm.append(ref dot, Dm.text("[]-panel", base_icon_state));

        object shown_contents_length = visible_items();
        if (Dm.truthy(visible_contents) && Dm.truthy(shown_contents_length))
        {
            string content_level = Dm.text("[]-[]", base_icon_state, contents_overlay_icon);
            object sw__1 = shown_contents_length;
            if (Dm.in_to(sw__1, 1, 25))
                content_level += "-1";
            else if (Dm.in_to(sw__1, 26, 50))
                content_level += "-2";
            else if (Dm.in_to(sw__1, 31, INFINITY))
                content_level += "-3";
            Dm.append(ref dot, mutable_appearance(icon, content_level));
        }

        Dm.append(ref dot, mutable_appearance(icon, Dm.text("[]-glass[]", base_icon_state, Dm.truthy(Dm.bitand(machine_stat, BROKEN)) ? "-broken" : "")));
        if (Dm.truthy(has_emissive) && Dm.truthy(powered__proc()) && !Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
        {
            Dm.append(ref dot, mutable_appearance(icon, Dm.text("[]-powered", base_icon_state)));
            // DEVIATION(DV12): . += emissive_appearance(icon, "[base_icon_state]-light-mask", src, alpha = src.alpha), the glow on the
            // emissive plane, is not created
        }
        return dot;
    }

    // tg: code/modules/food_and_drinks/machinery/smartfridge.dm:258
    public override object play_attack_sound(object damage_amount = null, string damage_type = null, object damage_flag = null, params object[] __extra)
    {
        damage_type ??= BRUTE;
        damage_flag ??= 0;
        string sw__1 = damage_type;
        if (sw__1 == BRUTE)
            playsound(this.loc, Dm.resource("sound/effects/glass/glasshit.ogg"), 75, TRUE);
        else if (sw__1 == BURN)
            playsound(this.loc, Dm.resource("sound/items/tools/welder.ogg"), 100, TRUE);
        return null;
    }

    // tg: code/modules/food_and_drinks/machinery/smartfridge.dm:265
    public override async DmTask<object> atom_break(object damage_flag = null)
    {
        playsound(this, SFX_SHATTER, 50, TRUE);
        return await base.atom_break(damage_flag);
    }

    // tg: code/modules/food_and_drinks/machinery/smartfridge.dm:269
    public virtual object can_load_item(obj_item loadable = null)
    {
        return Dm.num(!Dm.truthy(Dm.bitand(loadable.item_flags, ABSTRACT)) && !Dm.truthy(Dm.bitand(loadable.flags_1, HOLOGRAM_1)) && Dm.truthy(accept_check(loadable)));
    }

    // tg: code/modules/food_and_drinks/machinery/smartfridge.dm:272
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(user.combat_mode))
            return NONE;
        if (Dm.truthy(machine_stat))
        {
            if (Dm.truthy(Dm.bitand(machine_stat, NOPOWER)))
                to_chat(user, span_warning(I18n.T("\\The []'s magnetic door won't open without power!", this)));
            return ITEM_INTERACT_BLOCKING;
        }

        float loaded_count = Dm.num(visible_items());
        if (loaded_count >= max_n_of_items)
        {
            balloon_alert(user, I18n.T("no space!"));
            return ITEM_INTERACT_BLOCKING;
        }

        // Loading a single item
        if (Dm.truthy(can_load_item(tool)))
        {
            await load(tool, user);
            user.visible_message(
                span_notice(I18n.T("[] adds \\the [] to \\the [].", user, tool, this)),
                span_notice(I18n.T("You add \\the [] to \\the [].", tool, this))
            );
            SStgui.update_uis(this);
            if (Dm.truthy(visible_contents))
                update_appearance();
            return ITEM_INTERACT_SUCCESS;
        }

        // Loading from a bag (until fridge is full)
        if (tool is obj_item_storage_bag)
        {
            float loaded = 0;
            foreach (obj_item @object in Dm.each<obj_item>(tool.contents))
            {
                if (loaded_count >= max_n_of_items)
                    break;
                if (!Dm.truthy(can_load_item(@object)))
                    continue;
                await load(@object, user);
                loaded++;
                loaded_count++;
            }

            SStgui.update_uis(this);

            if (!Dm.truthy(loaded))
            {
                to_chat(user, span_warning(I18n.T("There is nothing in [] to put in []!", tool, this)));
                return ITEM_INTERACT_BLOCKING;
            }

            bool filled = loaded_count >= max_n_of_items;
            user.visible_message(
                span_notice(I18n.T("[] loads \\the [] with \\the [].", user, this, tool)),
                span_notice(I18n.T("You [] \\the [] with \\the [].", filled ? I18n.T("fill") : I18n.T("load"), this, tool))
            );
            if (Dm.truthy(Dm.length(tool.contents)))
                to_chat(user, span_warning(I18n.T("Some items are refused.")));
            if (Dm.truthy(visible_contents))
                update_appearance();
            return ITEM_INTERACT_SUCCESS;
        }

        to_chat(user, span_warning(I18n.T("\\The [] smartly refuses [].", this, tool)));
        return ITEM_INTERACT_BLOCKING;
    }

    // tg: code/modules/food_and_drinks/machinery/smartfridge.dm:335  accept_check's var/static/list/accepted_items (rules 10.6)
    private static DmList accept_check__accepted_items => TgState.Current.obj_machinery_smartfridge__accept_check__accepted_items ??= Dm.list(
        typeof(obj_item_food_grown),
        typeof(obj_item_seeds),
        typeof(obj_item_grown),
        typeof(obj_item_graft)
    );

    /**
     * Can this item be accepted by the smart fridge
     * Arguments
     * * [weapon][obj/item] - the item to accept
     */
    // tg: code/modules/food_and_drinks/machinery/smartfridge.dm:334
    public virtual object accept_check(obj_item weapon = null)
    {
        DmList accepted_items = accept_check__accepted_items;
        return is_type_in_list(weapon, accepted_items);
    }

    /**
     * Loads the item into the smart fridge
     * Arguments
     * * [weapon][obj/item] - the item to load. If the item is being held by a mo it will transfer it from hand else directly force move
     */
    // tg: code/modules/food_and_drinks/machinery/smartfridge.dm:348
    public virtual async DmTask<object> load(obj_item weapon = null, mob user = null)
    {
        if (ismob(weapon.loc))
        {
            mob owner = (mob)weapon.loc;
            if (!Dm.truthy(await owner.transferItemToLoc(weapon, this)))
            {
                to_chat(owner, span_warning(I18n.T("\\the [] is stuck to your hand, you cannot put it in \\the []!", weapon, this)));
                return FALSE;
            }
            return TRUE;
        }
        else
        {
            atom weapon_loc = (atom)weapon.loc;
            if (weapon_loc.atom_storage != null)
                return weapon_loc.atom_storage.attempt_remove(weapon, this, silent: TRUE);
            else
            {
                await weapon.forceMove(this);
                return TRUE;
            }
        }
    }

    // tg: code/modules/food_and_drinks/machinery/smartfridge.dm:362
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "SmartVend", name);
            ui.set_autoupdate(FALSE);
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/food_and_drinks/machinery/smartfridge.dm:369
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList dot = Dm.list();

        DmList listofitems = Dm.list();
        foreach (object item in Dm.each(this))
        {
            // We do not vend our own components.
            if (Dm.@in(item, component_parts))
                continue;

            atom_movable atom = (atom_movable)item;
            if (!QDELETED(atom))
            {
                string key = Dm.text("[]-[]", atom.type, atom.name);
                if (Dm.truthy(listofitems[key]))
                    Dm.inc_at(listofitems[key] as DmList, "amount");
                else
                {
                    listofitems[key] = Dm.list(
                        Dm.kv("path", key),
                        Dm.kv("name", full_capitalize(atom.name)),
                        Dm.kv("icon", atom.icon),
                        Dm.kv("icon_state", atom.icon_state),
                        Dm.kv("amount", 1)
                    );
                }
            }
        }
        dot["contents"] = sort_list(listofitems);
        dot["name"] = name;
        dot["isdryer"] = FALSE;
        return dot;
    }

    // tg: code/modules/food_and_drinks/machinery/smartfridge.dm:395
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null) // Update the UIs in case something inside is removed
    {
        object dot = await base.Exited(gone, direction);
        SStgui?.update_uis(this); // rules 10.6: a test harness world may lack SStgui (a fridge's contents leave at its destruction)
        return dot;
    }

    // tg: code/modules/food_and_drinks/machinery/smartfridge.dm:399
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot) || !Dm.truthy(await ui.user.can_perform_action(this, FORBID_TELEKINESIS_REACH)))
            return dot;

        mob living_mob = ui.user;

        object sw__1 = action;
        if (Dm.eq(sw__1, "Release"))
        {
            float? amount = text2num(@params["amount"]);
            if (amount == null || !isnum(amount))
                return TRUE;
            float dispensed_amount = 0;

            if (isAI(living_mob))
            {
                to_chat(living_mob, span_warning(I18n.T("[] does not respect your authority!", this)));
                return TRUE;
            }

            foreach (obj_item dispensed_item in Dm.each<obj_item>(contents))
            {
                if (amount <= 0)
                    break;
                string item_name = Dm.text("[]-[]", dispensed_item.type, replacetext(replacetext(dispensed_item.name, Dm.text("\\proper"), ""), Dm.text("\\improper"), ""));
                if (!Dm.eq(@params["path"], item_name))
                    continue;
                if (Dm.@in(dispensed_item, component_parts))
                    throw Dm.CRASH(Dm.text("Attempted removal of [] component_part from smartfridge via smartfridge interface.", dispensed_item), "code/modules/food_and_drinks/machinery/smartfridge.dm", 424);
                //dispense the item
                if (!Dm.truthy(await living_mob.put_in_hands(dispensed_item)))
                {
                    await dispensed_item.forceMove(drop_location() as atom);
                    adjust_item_drop_location(dispensed_item);
                }
                await use_energy(active_power_usage);
                dispensed_amount++;
                amount--;
            }
            if (Dm.truthy(dispensed_amount) && Dm.truthy(vend_sound))
                playsound(this, vend_sound, 50, TRUE, extrarange: -3);
            if (Dm.truthy(visible_contents))
                update_appearance();
            return TRUE;
        }

        return FALSE;
    }

    // tg: code/modules/food_and_drinks/machinery/smartfridge.dm:440
    public virtual void recheck_atmos_passing()
    {
        if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
            can_atmos_pass = ATMOS_PASS_YES;
        else
            can_atmos_pass = Dm.truthy(anchored) ? ATMOS_PASS_NO : ATMOS_PASS_YES;
        air_update_turf(TRUE, anchored);
    }
}

// ----------------------------
//  Drying 'smartfridge'
// ----------------------------
// tg: code/modules/food_and_drinks/machinery/smartfridge.dm:450
public partial class obj_machinery_smartfridge_drying : obj_machinery_smartfridge
{
    // tg: code/modules/food_and_drinks/machinery/smartfridge.dm:469
    public override object Destroy(float? force = null)
    {
        current_user = null;
        return base.Destroy(force);
    }

    // tg: code/modules/food_and_drinks/machinery/smartfridge.dm:473
    public override object AllowDrop()
    {
        return TRUE; // Allow drying results to stay inside
    }

    // tg: code/modules/food_and_drinks/machinery/smartfridge.dm:476
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (Dm.truthy(visible_contents) && Dm.truthy(powered__proc()) && !Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
        {
            string suffix = Dm.truthy(drying) ? "on" : "off";
            Dm.append(ref dot, mutable_appearance(icon, Dm.text("[]-[]", base_icon_state, suffix)));
            // DEVIATION(DV12): . += emissive_appearance(icon, "[base_icon_state]-[suffix]", src, alpha = src.alpha) is not created
        }
        return dot;
    }

    // tg: code/modules/food_and_drinks/machinery/smartfridge.dm:483
    public override object visible_items()
    {
        return min(1, (contents.len - 1)); // Return one if has any, as there's only one icon for overlay
    }

    // tg: code/modules/food_and_drinks/machinery/smartfridge.dm:486
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList dot = await base.ui_data(user) as DmList;
        dot["isdryer"] = TRUE;
        dot["drying"] = drying;
        return dot;
    }

    // tg: code/modules/food_and_drinks/machinery/smartfridge.dm:491
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
        {
            update_appearance(); // This is to handle a case where the last item is taken out manually instead of through drying pop-out
            return dot;
        }

        mob user = ui.user;
        object sw__1 = action;
        if (Dm.eq(sw__1, "Dry"))
        {
            toggle_drying(FALSE, user);
            return TRUE;
        }
        return dot;
    }

    // tg: code/modules/food_and_drinks/machinery/smartfridge.dm:503
    public override object powered__proc(object chan = null, float? ignore_use_power = null)
    {
        return !Dm.truthy(anchored) ? FALSE : base.powered__proc(chan, ignore_use_power);
    }

    // tg: code/modules/food_and_drinks/machinery/smartfridge.dm:506
    public override object power_change()
    {
        object dot = base.power_change();
        if (!Dm.truthy(powered__proc()))
            toggle_drying(TRUE);
        return dot;
    }

    // tg: code/modules/food_and_drinks/machinery/smartfridge.dm:511
    public override async DmTask<object> load(obj_item dried_object = null, mob user = null) //For updating the filled overlay
    {
        object dot = await base.load(dried_object, user);
        if (!Dm.truthy(dot))
            return dot;
        update_appearance();
        if (Dm.truthy(drying) && user?.mind != null)
            current_user = WEAKREF(user.mind) as datum_weakref;
        return dot;
    }

    // tg: code/modules/food_and_drinks/machinery/smartfridge.dm:519
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (Dm.truthy(drying))
        {
            foreach (obj_item item_iterator in Dm.each<obj_item>(this))
            {
                if (!Dm.truthy(accept_check(item_iterator)))
                    continue;
                if (Dm.truthy(item_iterator._listen_lookup?[COMSIG_ITEM_FINISH_DRYING]))
                    item_iterator._SendSignal(COMSIG_ITEM_FINISH_DRYING, Dm.list(item_iterator, current_user, seconds_per_tick));
            }

            SStgui.update_uis(this);
            update_appearance();
            Waiting.NoSleep(use_energy(active_power_usage));
        }
        return null;
    }

    // tg: code/modules/food_and_drinks/machinery/smartfridge.dm:530
    public override object accept_check(obj_item O = null)
    {
        return Dm.num(HAS_TRAIT(O, TRAIT_DRYABLE) && !HAS_TRAIT(O, TRAIT_DRIED));
    }

    /**
     * Toggles drying on or off
     * Arguments
     * * forceoff - if TRUE will force the dryer off always
     */
    // tg: code/modules/food_and_drinks/machinery/smartfridge.dm:538
    public virtual void toggle_drying(object forceoff = null, mob user = null)
    {
        if (Dm.truthy(drying) || Dm.truthy(forceoff))
        {
            drying = FALSE;
            current_user = null;
            update_use_power(IDLE_POWER_USE);
        }
        else
        {
            drying = TRUE;
            if (user?.mind != null)
                current_user = WEAKREF(user.mind) as datum_weakref;
            update_use_power(ACTIVE_POWER_USE);
        }
        update_appearance();
    }

    // tg: code/modules/food_and_drinks/machinery/smartfridge.dm:550
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)))
            return dot;
        atmos_spawn_air(Dm.text("[]", TURF_TEMPERATURE(1000)));
        return dot;
    }
}

/// Wooden version
// tg: code/modules/food_and_drinks/machinery/smartfridge.dm:557
public partial class obj_machinery_smartfridge_drying_rack : obj_machinery_smartfridge_drying
{
    // tg: code/modules/food_and_drinks/machinery/smartfridge.dm:569
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        //so we don't drop any of the parent smart fridge parts upon deconstruction
        clear_components();
        Waiting.NoSleep(AddElement(typeof(datum_element_tool_blocker), TOOL_WELDER));
        Waiting.NoSleep(AddElement(typeof(datum_element_tool_blocker), TOOL_SCREWDRIVER));
        return dot;
    }

    // tg: code/modules/food_and_drinks/machinery/smartfridge.dm:576
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        if (held_item == null)
            return NONE;

        float tool_tip_set = FALSE;
        if (Dm.eq(held_item.tool_behaviour, TOOL_CROWBAR))
        {
            context[SCREENTIP_CONTEXT_LMB] = "Deconstruct";
            tool_tip_set = TRUE;
        }
        else if (Dm.eq(held_item.tool_behaviour, TOOL_WRENCH))
        {
            context[SCREENTIP_CONTEXT_LMB] = Dm.text("[]chor", Dm.truthy(anchored) ? "Unan" : "An");
            tool_tip_set = TRUE;
        }

        return Dm.truthy(tool_tip_set) ? CONTEXTUAL_SCREENTIP_SET : NONE;
    }

    // tg: code/modules/food_and_drinks/machinery/smartfridge.dm:590
    public override object status_examine()
    {
        object dot = Dm.list();
        Dm.append(ref dot, span_notice(I18n.T("It looks like this unit can hold a maximum of <b>[]</b> items.", max_n_of_items)));
        return dot;
    }

    // tg: code/modules/food_and_drinks/machinery/smartfridge.dm:594
    public override object structure_examine()
    {
        object dot = base.structure_examine();
        Dm.append(ref dot, span_info(I18n.T("The whole rack can be [] apart.", EXAMINE_HINT(I18n.T("pried")))));
        return dot;
    }

    // tg: code/modules/food_and_drinks/machinery/smartfridge.dm:598
    public override async DmTask<object> exchange_parts(mob user = null, obj_item_storage_part_replacer replacer_tool = null)
    {
        return null;
    }

    // tg: code/modules/food_and_drinks/machinery/smartfridge.dm:601
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        Dm.@new<obj_item_stack_sheet_mineral_wood>(drop_location(), 10);
        return null;
    }

    // tg: code/modules/food_and_drinks/machinery/smartfridge.dm:604
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        return await default_deconstruction_crowbar(user, tool);
    }

    // tg: code/modules/food_and_drinks/machinery/smartfridge.dm:607
    public override object can_crowbar_deconstruct()
    {
        return TRUE;
    }

    // tg: code/modules/food_and_drinks/machinery/smartfridge.dm:610
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (Dm.truthy(drying))
            Dm.append(ref dot, Dm.text("[]-drying", base_icon_state));
        if (Dm.truthy(contents.len))
            Dm.append(ref dot, Dm.text("[]-filled", base_icon_state));
        return dot;
    }
}

// ----------------------------
//  Bar drink smartfridge
// ----------------------------
// tg: code/modules/food_and_drinks/machinery/smartfridge.dm:620
public partial class obj_machinery_smartfridge_drinks : obj_machinery_smartfridge
{
    // tg: code/modules/food_and_drinks/machinery/smartfridge.dm:626
    public override object accept_check(obj_item weapon = null)
    {
        //not an item or valid container
        if (!is_reagent_container(weapon))
            return FALSE;

        //an bowl or something that has no reagents
        if (weapon is obj_item_reagent_containers_cup_bowl || !Dm.truthy(Dm.length(((datum_reagents)weapon.reagents)?.reagent_list)))
            return FALSE;

        //list of items acceptable
        return Dm.num(weapon is obj_item_reagent_containers_cup || weapon is obj_item_reagent_containers_condiment);
    }
}

// ----------------------------
//  Food smartfridge
// ----------------------------
// tg: code/modules/food_and_drinks/machinery/smartfridge.dm:641
public partial class obj_machinery_smartfridge_food : obj_machinery_smartfridge
{
    // tg: code/modules/food_and_drinks/machinery/smartfridge.dm:646
    public override object accept_check(obj_item weapon = null)
    {
        if (weapon.w_class >= WEIGHT_CLASS_BULKY)
            return FALSE;
        if (IS_EDIBLE(weapon))
            return TRUE;
        if (weapon is obj_item_reagent_containers_cup_bowl && Dm.num(((datum_reagents)weapon.reagents)?.total_volume) > 0)
            return TRUE;
        return FALSE;
    }
}

// -------------------------------------
// Xenobiology Slime-Extract Smartfridge
// -------------------------------------
// tg: code/modules/food_and_drinks/machinery/smartfridge.dm:658
public partial class obj_machinery_smartfridge_extract : obj_machinery_smartfridge
{
    // tg: code/modules/food_and_drinks/machinery/smartfridge.dm:664
    public override object accept_check(obj_item weapon = null)
    {
        return Dm.num(weapon is obj_item_slime_extract || weapon is obj_item_slime_scanner);
    }
}

// -------------------------------------
// Cytology Petri Dish Smartfridge
// -------------------------------------
// tg: code/modules/food_and_drinks/machinery/smartfridge.dm:673
public partial class obj_machinery_smartfridge_petri : obj_machinery_smartfridge
{
    // tg: code/modules/food_and_drinks/machinery/smartfridge.dm:679
    public override object accept_check(obj_item weapon = null)
    {
        return Dm.num(weapon is obj_item_petri_dish);
    }
}

// -------------------------
// Organ Surgery Smartfridge
// -------------------------
// tg: code/modules/food_and_drinks/machinery/smartfridge.dm:688
public partial class obj_machinery_smartfridge_organ : obj_machinery_smartfridge
{
    // tg: code/modules/food_and_drinks/machinery/smartfridge.dm:697
    public override object accept_check(obj_item O = null)
    {
        return Dm.num(isorgan(O) || isbodypart(O));
    }

    // tg: code/modules/food_and_drinks/machinery/smartfridge.dm:700
    public override async DmTask<object> load(obj_item item = null, mob user = null)
    {
        object dot = await base.load(item, user);
        if (!Dm.truthy(dot)) //if the item loads, clear can_decompose
            return dot;

        if (isorgan(item))
        {
            obj_item_organ organ = (obj_item_organ)item;
            Dm.combine(ref organ.organ_flags, ORGAN_FROZEN);
        }

        if (isbodypart(item))
        {
            obj_item_bodypart bodypart = (obj_item_bodypart)item;
            foreach (obj_item_organ stored in Dm.each<obj_item_organ>(bodypart.contents))
                Dm.combine(ref stored.organ_flags, ORGAN_FROZEN);
        }
        return dot;
    }

    // tg: code/modules/food_and_drinks/machinery/smartfridge.dm:714
    public override async DmTask<object> RefreshParts()
    {
        object dot = await base.RefreshParts();
        foreach (datum_stock_part_matter_bin matter_bin in Dm.each<datum_stock_part_matter_bin>(component_parts))
        {
            max_n_of_items = 20 * Dm.num(matter_bin.tier);
            repair_rate = max(0, STANDARD_ORGAN_HEALING * (Dm.num(matter_bin.tier) - 1) * 0.5f);
        }
        return dot;
    }

    // tg: code/modules/food_and_drinks/machinery/smartfridge.dm:720
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        foreach (obj_item_organ target_organ in Dm.each<obj_item_organ>(contents))
        {
            if (!Dm.truthy(target_organ.damage))
                continue;

            Waiting.NoSleep(target_organ.apply_organ_damage(-repair_rate * target_organ.maxHealth * Dm.num(seconds_per_tick), required_organ_flag: ORGAN_ORGANIC));
        }
        return null;
    }

    // tg: code/modules/food_and_drinks/machinery/smartfridge.dm:727
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);

        if (isorgan(gone))
        {
            obj_item_organ O = (obj_item_organ)gone;
            Dm.mask(ref O.organ_flags, Dm.bitnot(ORGAN_FROZEN));
        }

        if (isbodypart(gone))
        {
            obj_item_bodypart bodypart = (obj_item_bodypart)gone;
            foreach (obj_item_organ stored in Dm.each<obj_item_organ>(bodypart.contents))
                Dm.mask(ref stored.organ_flags, Dm.bitnot(ORGAN_FROZEN));
        }
        return dot;
    }
}

// -----------------------------
// Chemistry Medical Smartfridge
// -----------------------------
// tg: code/modules/food_and_drinks/machinery/smartfridge.dm:742
public partial class obj_machinery_smartfridge_chemistry : obj_machinery_smartfridge
{
    // tg: code/modules/food_and_drinks/machinery/smartfridge.dm:771  accept_check's var/static/list/chemfridge_typecache (rules 10.6)
    private static DmList accept_check__chemfridge_typecache => TgState.Current.obj_machinery_smartfridge_chemistry__accept_check__chemfridge_typecache ??= typecacheof(Dm.list(
        typeof(obj_item_reagent_containers_syringe),
        typeof(obj_item_reagent_containers_cup_tube),
        typeof(obj_item_reagent_containers_cup_bottle),
        typeof(obj_item_reagent_containers_cup_beaker),
        typeof(obj_item_reagent_containers_spray),
        typeof(obj_item_reagent_containers_medigel),
        typeof(obj_item_reagent_containers_chem_pack)
    )) as DmList;

    // tg: code/modules/food_and_drinks/machinery/smartfridge.dm:748
    public override object accept_check(obj_item weapon = null)
    {
        // not an item or reagent container
        if (!is_reagent_container(weapon))
            return FALSE;

        // empty pill prank ok
        if (weapon is obj_item_reagent_containers_applicator)
            return TRUE;

        //check each pill in the pill bottle
        if (weapon is obj_item_storage_pill_bottle)
        {
            if (Dm.truthy(weapon.contents.len))
            {
                foreach (obj_item target_item in Dm.each<obj_item>(weapon))
                {
                    if (!Dm.truthy(accept_check(target_item)))
                        return FALSE;
                }
                return TRUE;
            }
            return FALSE;
        }

        // other empty containers not accepted
        if (!Dm.truthy(Dm.length(((datum_reagents)weapon.reagents)?.reagent_list)))
            return FALSE;

        // the long list of other containers that can be accepted
        DmList chemfridge_typecache = accept_check__chemfridge_typecache;
        return is_type_in_typecache(weapon, chemfridge_typecache);
    }
}

// ----------------------------
// Disk """fridge"""
// ----------------------------
// tg: code/modules/food_and_drinks/machinery/smartfridge.dm:813
public partial class obj_machinery_smartfridge_disks : obj_machinery_smartfridge
{
    // tg: code/modules/food_and_drinks/machinery/smartfridge.dm:825
    public override object accept_check(obj_item weapon = null)
    {
        return Dm.num(weapon is obj_item_disk);
    }
}

public sealed partial class TgState
{
    // tg: code/modules/food_and_drinks/machinery/smartfridge.dm:335  /obj/machinery/smartfridge/accept_check's var/static/list/accepted_items
    internal DmList obj_machinery_smartfridge__accept_check__accepted_items;
    // tg: code/modules/food_and_drinks/machinery/smartfridge.dm:771  /obj/machinery/smartfridge/chemistry/accept_check's var/static/list/chemfridge_typecache
    internal DmList obj_machinery_smartfridge_chemistry__accept_check__chemfridge_typecache;
}
