// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/displaycase.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps ST2 (D333): the whole file (the admins' variable editing, vv_edit_var, since gaps ST5): the display case with its
// showpiece drawn at 0.6 inside the glass, its burglar alarm (the area's alarm, the alarm handler's minute, the alert sound), opened
// by an ID with its access or a crowbar, the hover field's release, the kick, the glass broken and fixed, the welder's repair; the
// captain's case with the antique laser, the lab cage with Lamarr, the trophy case with its Trophycase window, the curator's key and
// the historian mode (the persistence that saves and restores the trophies is the owner's deferral: not-translated.md 204); the
// display case chassis that airlock electronics, a card reader or ten glass sheets finish; the vend-a-tray with its Vendatray window
// (the registration, the price, the purchase from the buyer's account, its vend flick, the multitool's recalibration, the emag) and the
// kitchen's. DEVIATION(DV500): the windows' picture of the showpiece is its icon and state, which the client draws (as DV496). The var
// blocks are generated (D111).

// tg: code/game/objects/structures/displaycase.dm:1
public partial class obj_structure_displaycase : obj_structure
{
    // tg: code/game/objects/structures/displaycase.dm:38
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (start_showpiece_type != null)
            showpiece = Dm.@new(start_showpiece_type, this) as obj_item;
        update_appearance();
        alarm_manager = Dm.@new<datum_alarm_handler>(this);
        return dot;
    }

    // tg: code/game/objects/structures/displaycase.dm:45  (gaps ST5)
    public override async DmTask<object> vv_edit_var(object vname = null, object vval = null)
    {
        object dot = await base.vv_edit_var(vname, vval);
        if (Dm.@in(vname, Dm.list(nameof(open), nameof(showpiece), nameof(custom_glass_overlay))))
            update_appearance();
        return dot;
    }

    // tg: code/game/objects/structures/displaycase.dm:50
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (gone == electronics)
            electronics = null;
        if (gone == showpiece)
        {
            showpiece = null;
            update_appearance();
        }
        return dot;
    }

    // tg: code/game/objects/structures/displaycase.dm:58
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref electronics);
        QDEL_NULL(ref showpiece);
        QDEL_NULL(ref alarm_manager);
        return base.Destroy(force);
    }

    // tg: code/game/objects/structures/displaycase.dm:64
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(alert))
            Dm.append(ref dot, span_notice(I18n.T("Hooked up with an anti-theft system.")));
        if (showpiece != null)
            Dm.append(ref dot, span_notice(I18n.T("There's \\a [] inside.", showpiece)));
        return dot;
    }

    ///Removes the showpiece from the displaycase
    // tg: code/game/objects/structures/displaycase.dm:72
    public virtual async DmTask dump()
    {
        if (QDELETED(showpiece))
            return;
        await showpiece.forceMove(drop_location() as atom);
    }

    // tg: code/game/objects/structures/displaycase.dm:77
    public override object play_attack_sound(object damage_amount = null, string damage_type = null, object damage_flag = null, params object[] __extra)
    {
        damage_type ??= BRUTE;
        object sw__1 = damage_type;
        if (Dm.eq(sw__1, BRUTE))
            playsound(this, Dm.resource("sound/effects/glass/glasshit.ogg"), 75, TRUE);
        else if (Dm.eq(sw__1, BURN))
            playsound(this, Dm.resource("sound/items/tools/welder.ogg"), 100, TRUE);
        return null;
    }

    // tg: code/game/objects/structures/displaycase.dm:84
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        disassembled ??= TRUE;
        await dump();
        if (!Dm.truthy(disassembled))
        {
            Dm.@new<obj_item_shard>(drop_location());
            trigger_alarm();
        }
    }

    // tg: code/game/objects/structures/displaycase.dm:90
    public override async DmTask<object> atom_break(object damage_flag = null)
    {
        object dot = await base.atom_break(damage_flag);
        if (!Dm.truthy(broken))
        {
            set_density(FALSE);
            broken = TRUE;
            Dm.@new<obj_item_shard>(drop_location());
            playsound(this, SFX_SHATTER, 70, TRUE);
            update_appearance();
            trigger_alarm();
        }
        return dot;
    }

    ///Anti-theft alarm triggered when broken.
    // tg: code/game/objects/structures/displaycase.dm:101
    public virtual void trigger_alarm()
    {
        if (!Dm.truthy(alert))
            return;
        area alarmed = get_area(this) as area;
        alarmed.burglaralert(this);

        alarm_manager.send_alarm(ALARM_BURGLAR);
        _addtimer(Dm.@new<datum_callback>(alarm_manager, nameof(datum_alarm_handler.clear_alarm), ALARM_BURGLAR), 1 * MINUTES, file: "code/game/objects/structures/displaycase.dm", line: 108);

        playsound(this, Dm.resource("sound/effects/alert.ogg"), 50, TRUE);
    }

    // tg: code/game/objects/structures/displaycase.dm:112
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (showpiece != null)
        {
            mutable_appearance showpiece_overlay = mutable_appearance(showpiece.icon, showpiece.icon_state) as mutable_appearance;
            showpiece_overlay.copy_overlays(showpiece);
            showpiece_overlay.transform = Dm.mul(showpiece_overlay.transform, 0.6f);
            Dm.append(ref dot, showpiece_overlay);
        }
        if (Dm.truthy(custom_glass_overlay))
            return dot;
        if (Dm.truthy(broken))
        {
            Dm.append(ref dot, Dm.text("[]_broken", Dm.initial(this, nameof(icon_state))));
            return dot;
        }
        if (!Dm.truthy(open))
        {
            Dm.append(ref dot, Dm.text("[]_closed", Dm.initial(this, nameof(icon_state))));
            return dot;
        }
        return dot;
    }

    // tg: code/game/objects/structures/displaycase.dm:128
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(tool.GetID()) && !Dm.truthy(broken))
        {
            if (!Dm.truthy(await allowed__proc(user)))
            {
                to_chat(user, span_alert(I18n.T("Access denied.")));
                return ITEM_INTERACT_BLOCKING;
            }

            to_chat(user, span_notice(Dm.truthy(open) ? I18n.T("You close [].", this) : I18n.T("You open [].", this)));
            toggle_lock(user);
            return ITEM_INTERACT_SUCCESS;
        }

        if (Dm.truthy(open) && showpiece == null)
        {
            await insert_showpiece(tool, user);
            return ITEM_INTERACT_SUCCESS; //cancel the attack chain, whether we successfully placed an item or not
        }

        if (Dm.truthy(glass_fix) && Dm.truthy(broken) && istype(tool, typeof(obj_item_stack_sheet_glass)))
        {
            obj_item_stack_sheet_glass glass_sheet = (obj_item_stack_sheet_glass)tool;
            if (Dm.lt(glass_sheet.get_amount(), 2))
            {
                to_chat(user, span_warning(I18n.T("You need two glass sheets to fix the case!")));
                return ITEM_INTERACT_BLOCKING;
            }

            to_chat(user, span_notice(I18n.T("You start fixing []...", this)));
            if (!Dm.truthy(await do_after(user, 2 * SECONDS, target: this)))
                return ITEM_INTERACT_BLOCKING;

            await glass_sheet.use(2);
            broken = FALSE;
            atom_integrity = max_integrity;
            update_appearance();
            return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }

    // tg: code/game/objects/structures/displaycase.dm:161
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(user.combat_mode) || Dm.truthy(broken))
            return ITEM_INTERACT_SKIP_TO_ATTACK;

        if (Dm.eq(atom_integrity, max_integrity))
        {
            to_chat(user, span_warning(I18n.T("[] is already in good condition!", this)));
            return ITEM_INTERACT_BLOCKING;
        }

        if (!Dm.truthy(tool.tool_start_check(user, amount: 1)))
            return ITEM_INTERACT_BLOCKING;

        to_chat(user, span_notice(I18n.T("You begin repairing []...", this)));
        if (!Dm.truthy(await tool.use_tool(this, user, 40, volume: 50)))
            return ITEM_INTERACT_BLOCKING;

        atom_integrity = max_integrity;
        update_appearance();
        to_chat(user, span_notice(I18n.T("You repair [].", this)));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/displaycase.dm:181
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(alert)) //Only applies to the lab cage and player made display cases
            return ITEM_INTERACT_SKIP_TO_ATTACK;

        if (Dm.truthy(broken))
        {
            if (showpiece != null)
            {
                to_chat(user, span_warning(I18n.T("Remove the displayed object first!")));
                return ITEM_INTERACT_BLOCKING;
            }

            to_chat(user, span_notice(I18n.T("You remove the destroyed case.")));
            qdel(this);
            return ITEM_INTERACT_SUCCESS;
        }

        to_chat(user, span_notice(Dm.truthy(open) ? I18n.T("You start to close []...", this) : I18n.T("You start to open []...", this)));
        if (!Dm.truthy(await tool.use_tool(this, user, 20)))
            return ITEM_INTERACT_BLOCKING;

        to_chat(user, span_notice(Dm.truthy(open) ? I18n.T("You close [].", this) : I18n.T("You open [].", this)));
        toggle_lock(user);
        return ITEM_INTERACT_SUCCESS;
    }

    ///Handles placing an item into the display case. Returns TRUE if the item failed to be placed inside the container, useful for descendants
    // tg: code/game/objects/structures/displaycase.dm:204
    public virtual async DmTask<object> insert_showpiece(obj_item new_showpiece = null, mob user = null)
    {
        if (showpiece_type != null && !istype(new_showpiece, showpiece_type.GetType()))
        {
            to_chat(user, span_notice(I18n.T("This doesn't belong in this kind of display.")));
            return TRUE;
        }
        if (Dm.truthy(await user.transferItemToLoc(new_showpiece, this)))
        {
            showpiece = new_showpiece;
            to_chat(user, span_notice(I18n.T("You put [] on display.", new_showpiece)));
            update_appearance();
        }
        return null;
    }

    ///Opens and closes the display case
    // tg: code/game/objects/structures/displaycase.dm:214
    public virtual void toggle_lock(mob user = null)
    {
        playsound(this, Dm.resource("sound/machines/click.ogg"), 20, TRUE);
        open = Dm.not(open);
        update_appearance();
    }

    // tg: code/game/objects/structures/displaycase.dm:219
    public override async DmTask<object> attack_paw(mob user = null, DmList modifiers = null)
    {
        return await attack_hand(user, modifiers);
    }

    // tg: code/game/objects/structures/displaycase.dm:222
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        mob_living living_user = (mob_living)user;
        living_user.changeNext_move(CLICK_CD_MELEE);
        if (showpiece != null && (Dm.truthy(broken) || Dm.truthy(open)))
        {
            to_chat(living_user, span_notice(I18n.T("You deactivate the hover field built into the case.")));
            log_combat(living_user, this, "deactivates the hover field of");
            await dump();
            add_fingerprint(living_user);
            return dot;
        }
        else
        {
            //prevents remote "kicks" with TK
            if (!Dm.truthy(await Adjacent(living_user)))
                return dot;
            if (!Dm.truthy(living_user.combat_mode))
            {
                if (!Dm.truthy(open) && !Dm.truthy(autoexamine_while_closed))
                    return dot;
                if (!Dm.truthy(living_user.is_blind()))
                    living_user.examinate(this);
                return dot;
            }
            living_user.visible_message(span_danger(I18n.T("[] kicks the display case.", living_user)), null, null, COMBAT_MESSAGE_RANGE);
            log_combat(living_user, this, "kicks");
            await living_user.do_attack_animation(this, ATTACK_EFFECT_KICK);
            await take_damage(2);
        }
        return dot;
    }
}

// tg: code/game/objects/structures/displaycase.dm:248
public partial class obj_structure_displaycase_chassis : obj_structure
{
    // tg: code/game/objects/structures/displaycase.dm:260
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        register_context();
        return dot;
    }

    // tg: code/game/objects/structures/displaycase.dm:264
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        object dot = base.add_context(source, context, held_item, user);
        if (held_item == null)
            return dot;

        if (Dm.eq(held_item.tool_behaviour, TOOL_WRENCH))
        {
            context[SCREENTIP_CONTEXT_LMB] = "Deconstruct";
            return CONTEXTUAL_SCREENTIP_SET;
        }
        if (istype(held_item, typeof(obj_item_electronics_airlock)) && electronics == null)
        {
            context[SCREENTIP_CONTEXT_LMB] = "Add electronics";
            return CONTEXTUAL_SCREENTIP_SET;
        }
        if (istype(held_item, typeof(obj_item_stock_parts_card_reader)))
        {
            context[SCREENTIP_CONTEXT_LMB] = "Construct Vend-A-Tray";
            return CONTEXTUAL_SCREENTIP_SET;
        }
        if (istype(held_item, typeof(obj_item_stack_sheet_glass)))
        {
            context[SCREENTIP_CONTEXT_LMB] = "Finalize display case";
            return CONTEXTUAL_SCREENTIP_SET;
        }
        return dot;
    }

    // tg: code/game/objects/structures/displaycase.dm:283
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (electronics == null)
            Dm.append(ref dot, span_notice(I18n.T("You can attach [] to give it access restrictions.", EXAMINE_HINT(I18n.T("airlock electronics")))));
        Dm.append(ref dot, span_notice(I18n.T("[] can be finalized using [], or turned into a Vend-A-Tray using a [].", this, EXAMINE_HINT(I18n.T("10 glass sheets")), EXAMINE_HINT(I18n.T("card reader")))));
        return dot;
    }

    // tg: code/game/objects/structures/displaycase.dm:289
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        await base.wrench_act(user, tool);
        balloon_alert(user, I18n.T("disassembling..."));
        tool.play_tool_sound(this);
        if (Dm.truthy(await tool.use_tool(this, user, 3 * SECONDS)))
        {
            playsound(loc, Dm.resource("sound/items/deconstruct.ogg"), 50, TRUE);
            Dm.@new<obj_item_stack_sheet_mineral_wood>(drop_location(), 5);
            if (electronics != null)
            {
                await electronics.forceMove(drop_location() as atom);
                electronics = null;
            }
            qdel(this);
        }
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/displaycase.dm:302
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (istype(tool, typeof(obj_item_electronics_airlock)))
        {
            balloon_alert(user, I18n.T("installing electronics..."));
            if (!Dm.truthy(await do_after(user, 3 * SECONDS, target: this)) || !Dm.truthy(await user.transferItemToLoc(tool, this)))
                return ITEM_INTERACT_BLOCKING;

            electronics = (obj_item_electronics_airlock)tool;
            balloon_alert(user, I18n.T("electronics installed"));
            return ITEM_INTERACT_SUCCESS;
        }

        if (istype(tool, typeof(obj_item_stock_parts_card_reader)))
        {
            balloon_alert(user, I18n.T("adding []...", tool));
            if (!Dm.truthy(await do_after(user, 2 * SECONDS, target: this)))
                return ITEM_INTERACT_BLOCKING;

            qdel(tool);
            await make_final_result(display_type: typeof(obj_structure_displaycase_forsale));
            return ITEM_INTERACT_SUCCESS;
        }

        if (istype(tool, typeof(obj_item_stack_sheet_glass)))
        {
            obj_item_stack_sheet_glass glass_sheets = (obj_item_stack_sheet_glass)tool;
            if (Dm.lt(glass_sheets.get_amount(), 10))
            {
                balloon_alert(user, I18n.T("need 10 sheets!"));
                return ITEM_INTERACT_BLOCKING;
            }

            balloon_alert(user, I18n.T("adding glass..."));
            if (!Dm.truthy(await do_after(user, 2 * SECONDS, target: this)))
                return ITEM_INTERACT_BLOCKING;

            await glass_sheets.use(10);
            await make_final_result(display_type: typeof(obj_structure_displaycase_noalert));
            return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }

    ///Makes the final result of the chassis, then deletes itself.
    // tg: code/game/objects/structures/displaycase.dm:338
    public virtual async DmTask make_final_result(Type display_type = null)
    {
        obj_structure_displaycase display = Dm.@new(display_type, loc) as obj_structure_displaycase;
        if (electronics != null)
        {
            await electronics.forceMove(display);
            display.electronics = electronics;
            if (Dm.truthy(electronics.one_access))
                display.req_one_access = electronics.accesses;
            else
                display.req_access = electronics.accesses;
        }
        qdel(this);
    }
}

// tg: code/game/objects/structures/displaycase.dm:363
public partial class obj_structure_displaycase_trophy : obj_structure_displaycase
{
    // tg: code/game/objects/structures/displaycase.dm:378
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.append(ref GLOB.trophy_cases, this);
        return dot;
    }

    // tg: code/game/objects/structures/displaycase.dm:382
    public override object Destroy(float? force = null)
    {
        Dm.remove(ref GLOB.trophy_cases, this);
        return base.Destroy(force);
    }

    ///Creates a showpiece dummy to display, using persistent data
    // tg: code/game/objects/structures/displaycase.dm:387
    public virtual void set_up_trophy(datum_trophy_data chosen_trophy = null)
    {
        showpiece = Dm.@new<obj_item_showpiece_dummy>(this, text2path(Dm.str(chosen_trophy.path)));
        trophy_message = Dm.str(trim(chosen_trophy.message, MAX_PLAQUE_LEN));
        if (trophy_message == "")
            trophy_message = Dm.str(trim(showpiece.desc, MAX_PLAQUE_LEN));
        placer_key = Dm.str(trim(chosen_trophy.placer_key));
        holographic_showpiece = TRUE;
        update_appearance();
    }

    // tg: code/game/objects/structures/displaycase.dm:396
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!istype(tool, typeof(obj_item_key_displaycase)))
            return await base.item_interaction(user, tool, modifiers, __extra);
        toggle_historian_mode(user);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/displaycase.dm:402
    public override async DmTask dump()
    {
        if (showpiece != null)
        {
            if (Dm.truthy(holographic_showpiece))
            {
                visible_message(span_danger(I18n.T("[] fizzles and vanishes!", showpiece)));
                do_sparks(number: 1, cardinal_only: FALSE, source: this);
                QDEL_NULL(ref showpiece);
                holographic_showpiece = FALSE;
            }
            else
                await base.dump();
            placer_key = "";
            trophy_message = null;
        }
    }

    // tg: code/game/objects/structures/displaycase.dm:414
    public override async DmTask<object> insert_showpiece(obj_item new_showpiece = null, mob user = null)
    {
        if (Dm.truthy(await base.insert_showpiece(new_showpiece, user)))
            return TRUE;
        if (showpiece == new_showpiece)
            placer_key = user.ckey;
        return null;
    }

    ///Toggles the mode that shows the historian panel on the UI, enabling saving the looks and the trophy message of the current trophy
    // tg: code/game/objects/structures/displaycase.dm:421
    public virtual void toggle_historian_mode(mob user = null)
    {
        historian_mode = Dm.not(historian_mode);
        balloon_alert(user, Dm.truthy(historian_mode) ? I18n.T("enabled historian mode.") : I18n.T("disabled historian mode."));
        playsound(this, Dm.resource("sound/machines/beep/twobeep.ogg"), 10, vary: 50);
        SStgui.update_uis(this);
    }

    // tg: code/game/objects/structures/displaycase.dm:427
    public override void toggle_lock(mob user = null)
    {
        base.toggle_lock(user);
        SStgui.close_uis(this);
    }

    // tg: code/game/objects/structures/displaycase.dm:431
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["historian_mode"] = historian_mode;
        data["holographic_showpiece"] = holographic_showpiece;
        data["max_length"] = MAX_PLAQUE_LEN;
        data["has_showpiece"] = showpiece != null ? TRUE : FALSE;
        if (showpiece != null)
        {
            data["showpiece_name"] = capitalize(format_text(showpiece.name));
            data["showpiece_description"] = Dm.truthy(trophy_message) ? format_text(trophy_message) : null;
        }
        await DmTask.CompletedTask;
        return data;
    }

    // tg: code/game/objects/structures/displaycase.dm:442
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();
        if (showpiece != null)
        {
            // DEVIATION(DV500): tg's icon2base64(getFlatIcon(showpiece, no_anim=TRUE)), a PNG the server makes; the port sends the
            // showpiece's icon and state and the client's Trophycase draws tg's sprite (the server reads no .dmi, D104).
            data["showpiece_icon"] = Dm.list(Dm.kv("icon", Dm.text("[]", showpiece.icon)), Dm.kv("icon_state", showpiece.icon_state));
        }
        await DmTask.CompletedTask;
        return data;
    }

    // tg: code/game/objects/structures/displaycase.dm:448
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        switch (action)
        {
            case "insert_key":
            {
                if (Dm.truthy(historian_mode))
                    return dot;
                obj_item_key_displaycase trophy_key = usr.get_active_held_item() as obj_item_key_displaycase;
                if (trophy_key != null)
                {
                    toggle_historian_mode(usr);
                    return TRUE;
                }
                return dot;
            }
            case "change_message":
            {
                if (showpiece != null && !Dm.truthy(holographic_showpiece))
                {
                    object new_trophy_message = await tgui_input_text(usr, I18n.T("Let's make history!"), I18n.T("Trophy Message"), trophy_message, max_length: MAX_PLAQUE_LEN);
                    if (!Dm.truthy(new_trophy_message))
                        return dot;
                    trophy_message = Dm.str(new_trophy_message);
                    return TRUE;
                }
                break;
            }
            case "lock":
            {
                if (!Dm.truthy(historian_mode))
                    return dot;
                toggle_historian_mode(usr);
                return TRUE;
            }
        }
        return dot;
    }

    // tg: code/game/objects/structures/displaycase.dm:474
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        if (Dm.truthy(open))
            return null;
        if (isliving(usr))
        {
            mob_living living_usr = (mob_living)usr;
            if (Dm.truthy(living_usr.combat_mode))
                return null;
        }
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "Trophycase", name);
            ui.set_autoupdate(FALSE);
            await ui.open();
        }
        return null;
    }
}

// tg: code/game/objects/structures/displaycase.dm:491
public partial class obj_item_showpiece_dummy : obj_item
{
    // tg: code/game/objects/structures/displaycase.dm:494
    [DmParams("mapload", "path")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object path = Dm.arg(__extra, 0);
        object dot = base.Initialize(mapload);
        Type item_path = path as Type;
        // Made without a path (only the create-and-destroy sweeps do; a trophy case always passes one), tg reads initial() of
        // null, which the shim raises (Dm.initial); the dummy keeps its own name and look instead.
        if (item_path == null)
            return dot;
        name = Dm.str(Dm.initial(item_path, (obj_item p) => p.name));
        desc = Dm.str(Dm.initial(item_path, (obj_item p) => p.desc));
        icon = Dm.initial(item_path, (obj_item p) => p.icon);
        icon_state = Dm.str(Dm.initial(item_path, (obj_item p) => p.icon_state));
        return dot;
    }
}

// tg: code/game/objects/structures/displaycase.dm:502
public partial class obj_structure_displaycase_forsale : obj_structure_displaycase
{
    // tg: code/game/objects/structures/displaycase.dm:520
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("[][]", Dm.initial(this, nameof(icon_state)), Dm.truthy(broken) ? "_broken" : (Dm.truthy(open) ? "_open" : (showpiece == null ? "_empty" : null)));
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/objects/structures/displaycase.dm:524
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (!Dm.truthy(broken) && !Dm.truthy(open))
            Dm.append(ref dot, Dm.text("[]_overlay", Dm.initial(this, nameof(icon_state))));
        return dot;
    }

    // tg: code/game/objects/structures/displaycase.dm:529
    public override async DmTask<object> insert_showpiece(obj_item new_showpiece = null, mob user = null)
    {
        if (Dm.truthy(await base.insert_showpiece(new_showpiece, user)))
            return TRUE;
        await update_static_data_for_all_viewers();
        return null;
    }

    // tg: code/game/objects/structures/displaycase.dm:534
    public override async DmTask dump()
    {
        await base.dump();
        await update_static_data_for_all_viewers();
    }

    // tg: code/game/objects/structures/displaycase.dm:538
    public override void toggle_lock(mob user = null)
    {
        base.toggle_lock(user);
        SStgui.update_uis(this);
    }

    // tg: code/game/objects/structures/displaycase.dm:542
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "Vendatray", name);
            ui.set_autoupdate(FALSE);
            await ui.open();
        }
        return null;
    }

    // tg: code/game/objects/structures/displaycase.dm:549
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["owner_name"] = payments_acc != null ? payments_acc.account_holder : null;
        data["product_name"] = showpiece != null ? capitalize(format_text(showpiece.name)) : null;
        data["registered"] = payments_acc != null ? TRUE : FALSE;
        data["product_cost"] = sale_price;
        data["tray_open"] = open;
        await DmTask.CompletedTask;
        return data;
    }

    // tg: code/game/objects/structures/displaycase.dm:558
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();
        // DEVIATION(DV500): tg's icon2base64(getFlatIcon(showpiece, no_anim=TRUE)); the port sends the showpiece's icon and state and
        // the client's Vendatray draws tg's sprite (the server reads no .dmi, D104).
        data["product_icon"] = showpiece != null ? Dm.list(Dm.kv("icon", Dm.text("[]", showpiece.icon)), Dm.kv("icon_state", showpiece.icon_state)) : null;
        await DmTask.CompletedTask;
        return data;
    }

    // tg: code/game/objects/structures/displaycase.dm:563
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        obj_item_card_id potential_acc = null;
        if (isliving(usr))
        {
            mob_living L = (mob_living)usr;
            potential_acc = L.get_idcard(hand_first: TRUE) as obj_item_card_id;
        }
        switch (action)
        {
            case "Buy":
            {
                if (showpiece == null)
                {
                    to_chat(usr, span_notice(I18n.T("There's nothing for sale.")));
                    return TRUE;
                }
                if (Dm.truthy(broken))
                {
                    to_chat(usr, span_notice(I18n.T("[] appears to be broken.", this)));
                    return TRUE;
                }
                if (payments_acc == null)
                {
                    to_chat(usr, span_notice(I18n.T("[] hasn't been registered yet.", this)));
                    return TRUE;
                }
                if (!Dm.truthy(await usr.can_perform_action(this, FORBID_TELEKINESIS_REACH)))
                    return TRUE;
                if (potential_acc == null)
                {
                    to_chat(usr, span_notice(I18n.T("No ID card detected.")));
                    return dot;
                }
                datum_bank_account account = potential_acc.registered_account;
                if (account == null)
                {
                    to_chat(usr, span_notice(I18n.T("[] has no account registered!", potential_acc)));
                    return dot;
                }
                if (!account.has_money(sale_price))
                {
                    to_chat(usr, span_notice(I18n.T("You do not possess the funds to purchase this.")));
                    return TRUE;
                }
                else
                {
                    account.adjust_money(-sale_price, Dm.text("Display Case: []", capitalize(showpiece.name)));
                    if (payments_acc != null)
                        payments_acc.adjust_money(sale_price, Dm.text("Display Case: []", capitalize(showpiece.name)));
                    await usr.put_in_hands(showpiece);
                    to_chat(usr, span_notice(I18n.T("You purchase [] for [] [].", showpiece, sale_price, MONEY_NAME)));
                    playsound(this, Dm.resource("sound/effects/cashregister.ogg"), 40, TRUE);
                    flick(Dm.text("[]_vend", Dm.initial(this, nameof(icon_state))), this);
                    showpiece = null;
                    update_appearance();
                    await update_static_data_for_all_viewers();
                    return TRUE;
                }
            }
            case "Open":
            {
                if (payments_acc == null)
                {
                    to_chat(usr, span_notice(I18n.T("[] hasn't been registered yet.", this)));
                    return TRUE;
                }
                if (potential_acc == null || potential_acc.registered_account == null)
                    return dot;
                if (!Dm.truthy(check_access(potential_acc)))
                {
                    playsound(this, Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 50, TRUE);
                    return dot;
                }
                toggle_lock();
                break;
            }
            case "Register":
            {
                if (payments_acc != null)
                    return dot;
                if (potential_acc == null || potential_acc.registered_account == null)
                    return dot;
                if (!Dm.truthy(check_access(potential_acc)))
                {
                    playsound(this, Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 50, TRUE);
                    return dot;
                }
                payments_acc = potential_acc.registered_account;
                playsound(this, Dm.resource("sound/machines/click.ogg"), 20, TRUE);
                break;
            }
            case "Adjust":
            {
                if (!Dm.truthy(check_access(potential_acc)) || potential_acc.registered_account != payments_acc)
                {
                    playsound(this, Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 50, TRUE);
                    return dot;
                }

                object new_price_input = await tgui_input_number(usr, I18n.T("Sale price for this vend-a-tray"), I18n.T("New Price"), 10, 1000);
                if (!Dm.truthy(new_price_input) || QDELETED(usr) || QDELETED(this))
                    return dot;
                if (payments_acc != potential_acc.registered_account)
                {
                    to_chat(usr, span_warning(I18n.T("[] rejects your new price.", this)));
                    return dot;
                }
                if (!Dm.truthy(await usr.can_perform_action(this, FORBID_TELEKINESIS_REACH)))
                {
                    to_chat(usr, span_warning(I18n.T("You need to get closer!")));
                    return dot;
                }
                sale_price = Dm.num(new_price_input);
                to_chat(usr, span_notice(I18n.T("The cost is now set to [].", sale_price)));
                SStgui.update_uis(this);
                return TRUE;
            }
        }
        dot = TRUE;
        return dot;
    }

    // tg: code/game/objects/structures/displaycase.dm:646
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (isidcard(tool))
        {
            //Card Registration
            obj_item_card_id potential_acc = (obj_item_card_id)tool;
            if (potential_acc.registered_account == null)
            {
                to_chat(user, span_warning(I18n.T("This ID card has no account registered!")));
                return ITEM_INTERACT_BLOCKING;
            }

            if (payments_acc != potential_acc.registered_account)
                return ITEM_INTERACT_BLOCKING;

            toggle_lock();
            return ITEM_INTERACT_SUCCESS;
        }

        if (istype(tool, typeof(obj_item_modular_computer)))
            return ITEM_INTERACT_BLOCKING;

        SStgui.update_uis(this);
        // DEVIATION(DV501): tg returns ITEM_INTERACT_SUCCESS here and never calls the parent, so nothing can be put on a vend-a-tray
        // (its insert_showpiece at :529 is unreachable: the item_interaction refactor dropped the old attackby's `..()`); the port
        // lets the parent put the item on display when the tray is open and empty, and otherwise answers as tg.
        if (Dm.truthy(open) && showpiece == null)
            return await base.item_interaction(user, tool, modifiers, __extra);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/displaycase.dm:666
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        object dot = await base.multitool_act(user, tool);
        if (Dm.num(atom_integrity) <= integrity_failure * Dm.num(max_integrity))
        {
            to_chat(user, span_notice(I18n.T("You start recalibrating []'s hover field...", this)));
            if (Dm.truthy(await do_after(user, 2 * SECONDS, target: this)))
            {
                broken = FALSE;
                atom_integrity = max_integrity;
                update_appearance();
            }
            return TRUE;
        }
        return dot;
    }

    // tg: code/game/objects/structures/displaycase.dm:676
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        object dot = await base.wrench_act(user, tool);
        if (Dm.truthy(open) && !Dm.truthy(user.combat_mode))
        {
            if (Dm.truthy(anchored))
                to_chat(user, span_notice(I18n.T("You start unsecuring []...", this)));
            else
                to_chat(user, span_notice(I18n.T("You start securing []...", this)));
            if (Dm.truthy(await tool.use_tool(this, user, 16, volume: 50)))
            {
                if (QDELETED(tool))
                    return dot;
                if (Dm.truthy(anchored))
                    to_chat(user, span_notice(I18n.T("You unsecure [].", this)));
                else
                    to_chat(user, span_notice(I18n.T("You secure [].", this)));
                await set_anchored(!Dm.truthy(anchored) ? TRUE : FALSE);
                return TRUE;
            }
        }
        else if (!Dm.truthy(open) && !Dm.truthy(user.combat_mode))
        {
            to_chat(user, span_notice(I18n.T("[] must be open to move it.", this)));
            return dot;
        }
        return dot;
    }

    // tg: code/game/objects/structures/displaycase.dm:696
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        await base.emag_act(user, emag_card, __extra);
        payments_acc = null;
        req_access = Dm.list();
        balloon_alert(user, I18n.T("account owner reset"));
        to_chat(user, span_warning(I18n.T("[]'s card reader fizzles and smokes.", this)));
        return TRUE;
    }

    // tg: code/game/objects/structures/displaycase.dm:704
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (showpiece != null && !Dm.truthy(open))
            Dm.append(ref dot, span_notice(I18n.T("[] is for sale for [] [].", showpiece, sale_price, MONEY_NAME)));
        if (Dm.truthy(broken))
            Dm.append(ref dot, span_notice(I18n.T("[] is sparking and the hover field generator seems to be overloaded. Use a multitool to fix it.", this)));
        return dot;
    }

    // tg: code/game/objects/structures/displaycase.dm:711
    public override async DmTask<object> atom_break(object damage_flag = null)
    {
        object dot = await base.atom_break(damage_flag);
        if (!Dm.truthy(broken))
        {
            broken = TRUE;
            playsound(this, SFX_SHATTER, 70, TRUE);
            update_appearance();
            trigger_alarm(); //In case it's given an alarm anyway.
        }
        return dot;
    }
}
