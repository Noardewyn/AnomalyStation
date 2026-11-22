// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/buttons.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M4a P4b): the button's init, its device (the door control assemblies of doorcontrol.dm), its appearance, the
// press (interact, attempt_press with the access check, the press with an item in hand) and examine and screentips are
// translated, so that Meta's buttons open and close their pod doors and bolt their airlocks. M5 slice 7 (the wall-mounted
// frames, D148): the panel (on_set_panel_open, the screwdriver), the assembly and airlock electronics acts and their removal,
// the wrench into a button frame (/obj/item/wallframe/button: find_support_structure, interact_with_atom), on_deconstruction
// and dump_inventory_contents; the AI and the shuttles are later (live-partial stubs); the emag
// came with it (G1a, D316). Gaps ST2: the curtain button's setup_device.
//
// GAP(types): tg's `device` and `board` (buttons.dm:21-22, var/obj/item/assembly/device, var/obj/item/electronics/airlock/board)
// are generated as Type (types-converter.md 3.5, V3: tg assigns typepaths to vars named `device` elsewhere, and the
// button's subtree gives them only null), which an instance does not fit. The button keeps its instances in
// `device_instance` and `board_instance` below, read and written where tg reads and writes `device` and `board`; the
// generated Type fields stay null. Requested from the lead: type the two vars by V2 (their declared classes).

// tg: code/game/machinery/buttons.dm:1
public partial class obj_machinery_button : obj_machinery
{
    /// tg's `device` (buttons.dm:21): the assembly inside the button (see the GAP(types) above).
    public obj_item_assembly device_instance;

    /// tg's `board` (buttons.dm:22): the airlock electronics inside the button (see the GAP(types) above).
    public obj_item_electronics_airlock board_instance;

    /**
     * INITIALIZATION
     */

    // tg: code/game/machinery/buttons.dm:47
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        if (!Dm.truthy(mapload))
        {
            set_panel_open(TRUE);
            update_appearance();
        }

        check_access(null);

        if (Dm.truthy(length(req_access)) || Dm.truthy(length(req_one_access)))
        {
            board_instance = Dm.@new<obj_item_electronics_airlock>(this);
            if (Dm.truthy(length(req_access)))
                board_instance.accesses = req_access;
            else
            {
                board_instance.one_access = 1;
                board_instance.accesses = req_one_access;
            }
        }

        setup_device(mapload);
        if (Dm.truthy(mapload))
            Waiting.NoSleep(find_and_mount_on_atom());
        register_context();
        return dot;
    }

    // tg: code/game/machinery/buttons.dm:68
    public override object Destroy(float? force = null)
    {
        // QDEL_NULL(device), QDEL_NULL(board) (__DEFINES/qdel.dm), inline (rules 11.1 (a))
        qdel(device_instance);
        device_instance = null;
        qdel(board_instance);
        board_instance = null;
        return base.Destroy(force);
    }

    // tg: code/game/machinery/buttons.dm:73
    public virtual void setup_device(object mapload = null)
    {
        if (Dm.truthy(mapload) && device_instance == null && device_type != null)
            device_instance = Dm.@new(device_type, this) as obj_item_assembly;
        if (Dm.truthy(id) && device_instance is obj_item_assembly_control control_device)
            control_device.id = id;
        initialized_button = TRUE;
    }

    // tg: code/game/machinery/buttons.dm:81
    public override DmTask<object> connect_to_shuttle(object mapload = null, obj_docking_port_mobile port = null, obj_docking_port_stationary dock = null)
    {
        if (Dm.truthy(id))
        {
            id = Dm.text("[]_[]", port.shuttle_id, id);
            setup_device();
        }
        return DmTask<object>.FromResult(default);
    }

    /**
     * APPEARANCE
     */

    // tg: code/game/machinery/buttons.dm:91
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("[][]", base_icon_state, skin);
        if (Dm.truthy(panel_open))
            icon_state += "-open";
        else if (Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(NOPOWER, BROKEN))))
            icon_state += "-nopower";
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/machinery/buttons.dm:99
    public override object update_appearance(float? updates = null)
    {
        updates ??= ALL;
        object dot = base.update_appearance(updates);

        if (Dm.truthy(panel_open) || Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(NOPOWER, BROKEN))))
            Waiting.NoSleep(set_light(0));
        else
            Waiting.NoSleep(set_light(Dm.initial(this, (obj_machinery_button p) => p.light_range), light_power, light_color));
        return dot;
    }

    // tg: code/game/machinery/buttons.dm:107
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);

        if (Dm.truthy(panel_open) && board_instance != null)
            Dm.append(ref dot, Dm.text("[]-overlay-board", base_icon_state));
        if (Dm.truthy(panel_open) && device_instance != null)
        {
            if (device_instance is obj_item_assembly_signaler)
                Dm.append(ref dot, Dm.text("[]-overlay-signaler", base_icon_state));
            else
                Dm.append(ref dot, Dm.text("[]-overlay-device", base_icon_state));
        }

        if (!Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(NOPOWER, BROKEN))) && !Dm.truthy(panel_open))
        {
            // DEVIATION(DV12): . += emissive_appearance(icon, "[base_icon_state]-light-mask", src, alpha = src.alpha)
            Dm.Deferred("code/game/machinery/buttons.dm:118", "update_overlays: emissive_appearance of the button's light mask (DEVIATION DV12)");
        }
        return dot;
    }

    /**
     * INTERACTION
     */

    // tg: code/game/machinery/buttons.dm:132
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(panel_open))
            return NONE;

        // isassembly(tool) (__DEFINES/is_helpers.dm), inline (rules 11.1 (a))
        if (tool is obj_item_assembly assembly)
            return await assembly_act(user, assembly);
        else if (tool is obj_item_electronics_airlock electronics)
            return await airlock_electronics_act(user, electronics);
        return null;
    }

    // tg: code/game/machinery/buttons.dm:141
    public virtual async DmTask<object> assembly_act(mob_living user = null, obj_item_assembly new_device = null)
    {
        if (device_instance != null)
        {
            to_chat(user, span_warning(I18n.T("The button already contains a device!")));
            return ITEM_INTERACT_BLOCKING;
        }
        if (!Dm.truthy(Dm.bitand(new_device.assembly_behavior, ASSEMBLY_FUNCTIONAL_OUTPUT)))
        {
            to_chat(user, span_warning(I18n.T("\\The [] won't really do anything meaningful inside of the button...", new_device)));
            return ITEM_INTERACT_BLOCKING;
        }
        if (!Dm.truthy(await user.transferItemToLoc(new_device, this, silent: FALSE)))
        {
            to_chat(user, span_warning(I18n.T("\\The [] is stuck to you!", new_device)));
            return ITEM_INTERACT_BLOCKING;
        }

        device_instance = new_device;
        if (Dm.truthy(new_device._listen_lookup?[COMSIG_ASSEMBLY_ADDED_TO_BUTTON]))
            new_device._SendSignal(COMSIG_ASSEMBLY_ADDED_TO_BUTTON, Dm.list(new_device, this, user));
        to_chat(user, span_notice(I18n.T("You add \\the [] to the button.", new_device)));

        update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/buttons.dm:159
    public virtual async DmTask<object> airlock_electronics_act(mob_living user = null, obj_item_electronics_airlock new_board = null)
    {
        if (board_instance != null)
        {
            to_chat(user, span_warning(I18n.T("The button already contains a board!")));
            return ITEM_INTERACT_BLOCKING;
        }
        if (!Dm.truthy(await user.transferItemToLoc(new_board, this, silent: FALSE)))
        {
            to_chat(user, span_warning(I18n.T("\\The [] is stuck to you!", new_board)));
            return ITEM_INTERACT_BLOCKING;
        }

        board_instance = new_board;
        if (Dm.truthy(board_instance.one_access))
            req_one_access = board_instance.accesses;
        else
            req_access = board_instance.accesses;
        to_chat(user, span_notice(I18n.T("You add \\the [] to the button.", new_board)));

        update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/buttons.dm:121  (M5 slice 7)
    public override void on_set_panel_open(object old_value = null)
    {
        if (Dm.truthy(panel_open)) // Only allow renaming while the panel is open
            Dm.combine(ref obj_flags, UNIQUE_RENAME);
        else
            obj_flags = Dm.bitand(obj_flags, Dm.bitnot(UNIQUE_RENAME));
    }

    // tg: code/game/machinery/buttons.dm:177  (M5 slice 7)
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(panel_open) || Dm.truthy(await allowed__proc(user)))
            return default_deconstruction_screwdriver(user, tool);

        balloon_alert(user, I18n.T("access denied"));
        flick_overlay_view(Dm.text("[]-overlay-error", base_icon_state), 1 * SECONDS);
        return ITEM_INTERACT_BLOCKING;
    }

    // tg: code/game/machinery/buttons.dm:185  (M5 slice 7)
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(panel_open))
        {
            balloon_alert(user, I18n.T("open button first!"));
            return ITEM_INTERACT_BLOCKING;
        }

        if (device_instance != null || board_instance != null)
        {
            balloon_alert(user, I18n.T("empty button first!"));
            return ITEM_INTERACT_BLOCKING;
        }

        to_chat(user, span_notice(I18n.T("You start unsecuring the button frame...")));
        if (Dm.truthy(await tool.use_tool(this, user, 40, volume: 50)))
        {
            to_chat(user, span_notice(I18n.T("You unsecure the button frame.")));
            playsound(loc, Dm.resource("sound/items/deconstruct.ogg"), 50, TRUE); // tg: code/game/machinery/buttons.dm:197
            await deconstruct(TRUE);
        }

        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/buttons.dm:202
    public override async DmTask<object> base_item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null)
    {
        object dot = await base.base_item_interaction(user, tool, modifiers);
        if (Dm.truthy(dot))
            return dot;
        // This is in here so it's called only after every other item interaction.
        if (!Dm.truthy(user.combat_mode) && !Dm.truthy(Dm.bitand(tool.item_flags, NOBLUDGEON)) && !Dm.truthy(panel_open))
            return Dm.truthy(await attempt_press(user)) ? ITEM_INTERACT_SUCCESS : ITEM_INTERACT_BLOCKING;
        return dot;
    }

    // tg: code/game/machinery/buttons.dm:211
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        await base.emag_act(user, emag_card, __extra);
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return null;
        req_access = Dm.list();
        req_one_access = Dm.list();
        playsound(this, SFX_SPARKS, 100, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
        obj_flags = Dm.bitor(obj_flags, EMAGGED);

        // The device inside can be emagged by swiping the button
        // returning TRUE will prevent feedback (so we can do our own)
        if (device_instance == null || !Dm.truthy(await device_instance.emag_act(user, emag_card)))
            balloon_alert(user, "access overridden");
        return TRUE;
    }

    // tg: code/game/machinery/buttons.dm:233
    public override async DmTask<object> interact(mob user = null, params object[] __extra)
    {
        object dot = await base.interact(user, __extra);
        if (Dm.truthy(dot))
            return dot;
        if (!Dm.truthy(initialized_button))
            setup_device();
        // tg: code/game/machinery/buttons.dm:238
        add_fingerprint(user);

        if (!Dm.truthy(panel_open))
        {
            await attempt_press(user);
            return null;
        }

        if (board_instance != null)
        {
            await remove_airlock_electronics(user);
            return null;
        }
        if (device_instance != null)
        {
            await remove_assembly(user);
            return null;
        }

        if (Dm.truthy(can_alter_skin))
        {
            if (skin == "")
            {
                skin = "-warning";
                to_chat(user, span_notice(I18n.T("You change the button frame's front panel to warning lines.")));
            }
            else
            {
                skin = "";
                to_chat(user, span_notice(I18n.T("You change the button frame's front panel to default.")));
            }
            update_appearance(UPDATE_ICON);
            balloon_alert(user, I18n.T("style swapped"));
        }
        return null;
    }

    // tg: code/game/machinery/buttons.dm:262
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        if (!Dm.truthy(initialized_button))
            setup_device();
        // tg: code/game/machinery/buttons.dm:265
        add_fingerprint(user);

        if (!Dm.truthy(panel_open))
            return SECONDARY_ATTACK_CALL_NORMAL;

        if (device_instance != null)
        {
            await remove_assembly(user);
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        }
        if (board_instance != null)
        {
            await remove_airlock_electronics(user);
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        }

        return await base.attack_hand_secondary(user, modifiers);
    }

    // tg: code/game/machinery/buttons.dm:279
    public virtual async DmTask remove_assembly(mob user = null)
    {
        if (Dm.truthy(device_instance._listen_lookup?[COMSIG_ASSEMBLY_REMOVED_FROM_BUTTON]))
            device_instance._SendSignal(COMSIG_ASSEMBLY_REMOVED_FROM_BUTTON, Dm.list(device_instance, this, user));
        await user.put_in_hands(device_instance);
        to_chat(user, span_notice(I18n.T("You remove \\the [] from the button frame.", device_instance)));
        device_instance = null;
        update_appearance(UPDATE_ICON);
    }

    // tg: code/game/machinery/buttons.dm:286
    public virtual async DmTask remove_airlock_electronics(mob user = null)
    {
        await user.put_in_hands(board_instance);
        to_chat(user, span_notice(I18n.T("You remove the board from the button frame.")));
        req_access = Dm.list();
        req_one_access = Dm.list();
        board_instance = null;
        update_appearance(UPDATE_ICON);
    }

    /**
     * DECONSTRUCTION
     */

    // tg: code/game/machinery/buttons.dm:319  (M5 slice 7)
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        obj_item_wallframe_button dropped_frame = Dm.@new<obj_item_wallframe_button>(drop_location());
        // tg: code/game/machinery/buttons.dm:321
        transfer_fingerprints_to(dropped_frame);
        _ = dropped_frame;
        return null;
    }

    // tg: code/game/machinery/buttons.dm:323  (M5 slice 7)
    public override async DmTask dump_inventory_contents(DmList subset = null)
    {
        await base.dump_inventory_contents(subset);
        device_instance = null;
        board_instance = null;
        req_access = Dm.list();
        req_one_access = Dm.list();
    }

    // tg: code/game/machinery/buttons.dm:294
    public virtual async DmTask<object> attempt_press(mob user = null)
    {
        if (Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(NOPOWER, BROKEN))))
            return FALSE;

        if (device_instance != null && device_instance.next_activate > world.time)
            return FALSE;

        if (!Dm.truthy(await allowed__proc(user)))
        {
            balloon_alert(user, I18n.T("access denied"));
            flick_overlay_view(Dm.text("[]-overlay-error", base_icon_state), 1 * SECONDS);
            return FALSE;
        }

        await use_energy(5 * JOULES);
        flick_overlay_view(Dm.text("[]-overlay-success", base_icon_state), 1 * SECONDS);

        if (device_instance != null)
            device_instance.pulsed(user);
        // SEND_GLOBAL_SIGNAL(COMSIG_GLOB_BUTTON_PRESSED, src) (__DEFINES/dcs/helpers.dm), inline (rules 11.1 (a))
        if (Dm.truthy(SSdcs._listen_lookup?[COMSIG_GLOB_BUTTON_PRESSED]))
            SSdcs._SendSignal(COMSIG_GLOB_BUTTON_PRESSED, Dm.list(SSdcs, this));
        return TRUE;
    }

    /**
     * INFORMATION
     */

    // tg: code/game/machinery/buttons.dm:335
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.examine(user, __extra);
        if (!Dm.truthy(panel_open))
            return dot;
        if (device_instance != null)
            Dm.append(ref dot, span_notice(I18n.T("There is \\a [] inside, which could be removed with an <b>empty hand</b>.", device_instance)));
        if (board_instance != null)
            Dm.append(ref dot, span_notice(I18n.T("There is \\a [] inside, which could be removed with an <b>empty hand</b>.", board_instance)));
        if (board_instance == null && device_instance == null)
            Dm.append(ref dot, span_notice(I18n.T("There is nothing currently installed in \\the [].", this)));
        return dot;
    }

    // tg: code/game/machinery/buttons.dm:346
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        if (Dm.truthy(panel_open))
        {
            if (held_item == null)
            {
                if (board_instance != null && device_instance != null)
                {
                    context[SCREENTIP_CONTEXT_LMB] = "Remove Board";
                    context[SCREENTIP_CONTEXT_RMB] = "Remove Device";
                    return CONTEXTUAL_SCREENTIP_SET;
                }
                else if (board_instance != null)
                {
                    context[SCREENTIP_CONTEXT_LMB] = "Remove Board";
                    return CONTEXTUAL_SCREENTIP_SET;
                }
                else if (device_instance != null)
                {
                    context[SCREENTIP_CONTEXT_LMB] = "Remove Device";
                    return CONTEXTUAL_SCREENTIP_SET;
                }
                else if (Dm.truthy(can_alter_skin))
                {
                    context[SCREENTIP_CONTEXT_LMB] = "Swap Style";
                    return CONTEXTUAL_SCREENTIP_SET;
                }
            }
            else if (held_item is obj_item_assembly) // isassembly(held_item), inline (rules 11.1 (a))
            {
                context[SCREENTIP_CONTEXT_LMB] = "Install Device";
                return CONTEXTUAL_SCREENTIP_SET;
            }
            else if (held_item is obj_item_electronics_airlock)
            {
                context[SCREENTIP_CONTEXT_LMB] = "Install Board";
                return CONTEXTUAL_SCREENTIP_SET;
            }
            else if (Dm.eq(held_item.tool_behaviour, TOOL_WRENCH))
            {
                context[SCREENTIP_CONTEXT_LMB] = "Deconstruct Button";
                return CONTEXTUAL_SCREENTIP_SET;
            }
            else if (Dm.eq(held_item.tool_behaviour, TOOL_SCREWDRIVER))
            {
                context[SCREENTIP_CONTEXT_LMB] = "Close Button";
                return CONTEXTUAL_SCREENTIP_SET;
            }
        }
        else
        {
            if (held_item == null)
            {
                context[SCREENTIP_CONTEXT_LMB] = "Press Button";
                return CONTEXTUAL_SCREENTIP_SET;
            }
            else if (Dm.eq(held_item.tool_behaviour, TOOL_SCREWDRIVER))
            {
                context[SCREENTIP_CONTEXT_LMB] = "Open Button";
                return CONTEXTUAL_SCREENTIP_SET;
            }
        }

        return NONE;
    }
}

/**
 * MAPPING PRESETS
 */

// tg: code/game/machinery/buttons.dm:390
public partial class obj_machinery_button_door : obj_machinery_button
{
    // tg: code/game/machinery/buttons.dm:401
    public override void setup_device(object mapload = null)
    {
        if (Dm.truthy(mapload))
            device_instance = Dm.truthy(normaldoorcontrol) ? Dm.@new<obj_item_assembly_control_airlock>(this) : Dm.@new<obj_item_assembly_control_blast_door>(this);

        if (device_instance is obj_item_assembly_control_airlock airlock_device)
            airlock_device.specialfunctions = specialfunctions;
        else if (device_instance is obj_item_assembly_control_blast_door control_device)
            control_device.sync_doors = sync_doors;
        base.setup_device(mapload);
    }
}

// tg: code/game/machinery/buttons.dm:491  (gaps ST2: the theatre's curtain button)
public partial class obj_machinery_button_curtain : obj_machinery_button
{
    // tg: code/game/machinery/buttons.dm:499
    public override void setup_device(object mapload = null)
    {
        base.setup_device(mapload);
        obj_item_assembly_control curtain = device_instance as obj_item_assembly_control;
        if (curtain != null)
            curtain.sync_doors = sync_doors;
    }
}

// tg: code/game/machinery/buttons.dm:517
public partial class obj_item_wallframe_button : obj_item_wallframe
{
    // tg: code/game/machinery/buttons.dm:525  (M5 slice 7)
    public override object find_support_structure(atom structure = null)
    {
        return structure is obj_structure_table ? structure : base.find_support_structure(structure);
    }

    // tg: code/game/machinery/buttons.dm:528  (M5 slice 7)
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (interacting_with is obj_structure_table)
            return Dm.truthy(user.combat_mode) ? await base.interact_with_atom(interacting_with, user, modifiers) : NONE;
        return await base.interact_with_atom(interacting_with, user, modifiers);
    }
}

// The AI's and cyborgs' clicks (silicons SI2, D326)
// tg: code/game/machinery/buttons.dm:226
public partial class obj_machinery_button : obj_machinery
{
    // tg: code/game/machinery/buttons.dm:226
    public override async DmTask<object> attack_ai(mob user = null)
    {
        if (!Dm.truthy(silicon_access_disabled) && !Dm.truthy(panel_open))
            return await attempt_press(user);
        return null;
    }

    // tg: code/game/machinery/buttons.dm:230
    public override async DmTask<object> attack_robot(mob user = null, object modifiers = null)
    {
        return await attack_ai(user);
    }
}
