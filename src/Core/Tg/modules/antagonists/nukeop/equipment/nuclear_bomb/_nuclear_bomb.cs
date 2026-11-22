// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/nukeop/equipment/nuclear_bomb/_nuclear_bomb.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign N1a (D316): the whole file but the leaves below (the minimap blip: the minimap, not ported; the lag switch:
// not declared). station_was_nuked is declared in game_modes.cs (M10 slice H).
// The var block is generated (D111).

public partial class datum_controller_global_vars
{
    /// The source of the last nuke that went off
    // tg: code/modules/antagonists/nukeop/equipment/nuclear_bomb/_nuclear_bomb.dm:4  GLOBAL_VAR(station_nuke_source)
    public object station_nuke_source;

    // tg: code/modules/antagonists/nukeop/equipment/nuclear_bomb/_nuclear_bomb.dm:4  (GLOBAL_MANAGED(station_nuke_source, null), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalstation_nuke_source()
    {
        station_nuke_source = null;
        Dm.append(ref gvars_datum_init_order, "station_nuke_source");
    }
}

// tg: code/modules/antagonists/nukeop/equipment/nuclear_bomb/_nuclear_bomb.dm:6
public partial class obj_machinery_nuclearbomb : obj_machinery
{
    private const float NUKE_RADIUS = 127;

    // tg: code/modules/antagonists/nukeop/equipment/nuclear_bomb/_nuclear_bomb.dm:59
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        countdown = Dm.@new<obj_effect_countdown_nuclearbomb>(this);
        core = Dm.@new<obj_item_nuke_core>(this);
        STOP_PROCESSING(SSobj, core);
        update_appearance();
        Waiting.NoSleep(SSpoints_of_interest.make_point_of_interest(this));
        previous_level = Dm.str(SSsecurity_level.get_current_level_as_text());
        update_minimap_blip();
        return dot;
    }

    // tg: code/modules/antagonists/nukeop/equipment/nuclear_bomb/_nuclear_bomb.dm:69
    public override object Destroy(float? force = null)
    {
        safety = FALSE;
        if (!Dm.truthy(exploding))
        {
            // If we're not exploding, set the alert level back to normal
            toggle_nuke_safety();
        }
        QDEL_NULL(ref countdown);
        QDEL_NULL(ref core);
        return base.Destroy(force);
    }

    // tg: code/modules/antagonists/nukeop/equipment/nuclear_bomb/_nuclear_bomb.dm:78
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        switch (deconstruction_state)
        {
            case NUKESTATE_UNSCREWED:
                dot.Add(span_notice(I18n.T("The front panel has been unscrewed and can be <b>pried open</b>.")));
                break;
            case NUKESTATE_PANEL_REMOVED:
                dot.Add(span_notice(I18n.T("The inner plate is exposed and can be cut with a <b>welding tool</b>.")));
                break;
            case NUKESTATE_WELDED:
                dot.Add(span_notice(I18n.T("The inner plate has been cut through and can be <b>pried off</b>.")));
                break;
            case NUKESTATE_CORE_EXPOSED:
                dot.Add(span_danger(I18n.T("The inner chamber is exposed, revealing [] to the outside!", core)));
                dot.Add(span_notice(I18n.T("The damaged inner plate covering the inner chamber can be replaced with some <b>iron</b>.")));
                break;
            case NUKESTATE_CORE_REMOVED:
                dot.Add(span_notice(I18n.T("The inner chamber is exposed, but is empty.")));
                break;
            case NUKESTATE_INTACT:
                dot.Add(span_notice(I18n.T("The front panel is secured.")));
                break;
        }

        switch (get_nuke_state())
        {
            case NUKE_OFF_LOCKED:
                dot.Add(span_notice(I18n.T("The device is awaiting activation codes.")));
                break;
            case NUKE_OFF_UNLOCKED:
                dot.Add(span_notice(I18n.T("The device is set and is ready for arming the detonation countdown.")));
                break;
            case NUKE_ON_TIMING:
                dot.Add(span_danger(I18n.T("There are [] seconds until detonation.", get_time_left())));
                break;
            case NUKE_ON_EXPLODING:
                dot.Add(span_bolddanger(I18n.T("It is in the process of exploding. Perhaps reviewing your affairs is in order.")));
                break;
        }
        return dot;
    }

    /// Checks if the disk inserted is a real nuke disk or not.
    // tg: code/modules/antagonists/nukeop/equipment/nuclear_bomb/_nuclear_bomb.dm:107
    public virtual bool disk_check(obj_item_disk_nuclear inserted_disk = null)
    {
        if (Dm.truthy(inserted_disk.fake))
        {
            Waiting.NoSleep(say(I18n.T("Authentication failure; disk not recognised.")));
            return false;
        }

        return true;
    }

    // tg: code/modules/antagonists/nukeop/equipment/nuclear_bomb/_nuclear_bomb.dm:114
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is obj_item_disk_nuclear disk)
        {
            if (!disk_check(disk))
                return ITEM_INTERACT_BLOCKING;

            if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
                return ITEM_INTERACT_BLOCKING;

            auth = disk;
            update_ui_mode();
            playsound(this, "sound/machines/terminal/terminal_insert_disc.ogg", 50, FALSE);
            add_fingerprint(user);
            return ITEM_INTERACT_SUCCESS;
        }

        switch (deconstruction_state)
        {
            case NUKESTATE_CORE_EXPOSED:
                if (tool is obj_item_nuke_core_container empty_box)
                {
                    to_chat(user, span_notice(I18n.T("You start loading the plutonium core into []...", empty_box)));
                    if (!Dm.truthy(await do_after(user, 5 * SECONDS, target: this, cog_icon: null)))
                        return ITEM_INTERACT_BLOCKING;

                    if (!Dm.truthy(await empty_box.load(core, user)))
                    {
                        to_chat(user, span_warning(I18n.T("You fail to load the plutonium core into []. [] has already been used!", empty_box, empty_box)));
                        return ITEM_INTERACT_BLOCKING;
                    }

                    to_chat(user, span_notice(I18n.T("You load the plutonium core into [].", empty_box)));
                    deconstruction_state = NUKESTATE_CORE_REMOVED;
                    update_appearance();
                    core = null;
                    return ITEM_INTERACT_SUCCESS;
                }

                if (tool is obj_item_stack_sheet_iron)
                {
                    if (!Dm.truthy(tool.tool_start_check(user, amount: 20)))
                        return ITEM_INTERACT_BLOCKING;

                    to_chat(user, span_notice(I18n.T("You begin repairing []'s inner metal plate...", this)));
                    if (!Dm.truthy(await tool.use_tool(this, user, 10 * SECONDS, amount: 20)))
                        return ITEM_INTERACT_BLOCKING;

                    to_chat(user, span_notice(I18n.T("You repair []'s inner metal plate. The radiation is contained.", this)));
                    deconstruction_state = NUKESTATE_PANEL_REMOVED;
                    STOP_PROCESSING(SSobj, core);
                    update_appearance();
                    return ITEM_INTERACT_SUCCESS;
                }
                break;

            case NUKESTATE_CORE_REMOVED:
                if ((tool as obj_item_nuke_core_container)?.core != null && tool is not obj_item_nuke_core_container_supermatter)
                {
                    var core_box = (obj_item_nuke_core_container)tool;
                    to_chat(user, span_notice(I18n.T("You pry open [] and begin placing [] into []'s inner chamber...", core_box, core_box.core, this)));
                    if (!Dm.truthy(await do_after(user, 15 * SECONDS, this)))
                        return ITEM_INTERACT_BLOCKING;

                    await core_box.core.forceMove(this);
                    core = core_box.core;
                    to_chat(user, span_notice(I18n.T("You place [] into []'s inner chamber.", core_box.core, this)));
                    deconstruction_state = NUKESTATE_CORE_EXPOSED;
                    update_appearance();
                    core_box.icon_state = Dm.initial(core_box, (obj_item_nuke_core_container c) => c.icon_state);
                    core_box.core = null;
                    return ITEM_INTERACT_SUCCESS;
                }

                if (tool is obj_item_nuke_core && tool is not obj_item_nuke_core_supermatter_sliver)
                {
                    to_chat(user, span_notice(I18n.T("You begin placing [] into []'s inner chamber...", tool, this)));
                    if (!Dm.truthy(await do_after(user, 6 * SECONDS, this)))
                        return ITEM_INTERACT_BLOCKING;

                    await tool.forceMove(this);
                    core = (obj_item_nuke_core)tool;
                    to_chat(user, span_notice(I18n.T("You place [] into []'s inner chamber.", tool, this)));
                    deconstruction_state = NUKESTATE_CORE_EXPOSED;
                    update_appearance();
                    return ITEM_INTERACT_SUCCESS;
                }
                break;
        }

        return NONE;
    }

    // tg: code/modules/antagonists/nukeop/equipment/nuclear_bomb/_nuclear_bomb.dm:190
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        switch (deconstruction_state)
        {
            case NUKESTATE_UNSCREWED:
                to_chat(user, span_notice(I18n.T("You start removing []'s front panel...", this)));
                if (!Dm.truthy(await tool.use_tool(this, user, 3 * SECONDS, volume: 100)))
                    return ITEM_INTERACT_BLOCKING;

                to_chat(user, span_notice(I18n.T("You remove []'s front panel.", this)));
                deconstruction_state = NUKESTATE_PANEL_REMOVED;
                update_appearance();
                return ITEM_INTERACT_SUCCESS;

            case NUKESTATE_WELDED:
                to_chat(user, span_notice(I18n.T("You start prying off []'s inner plate...", this)));
                if (!Dm.truthy(await tool.use_tool(this, user, 3 * SECONDS, volume: 100)))
                    return ITEM_INTERACT_BLOCKING;

                if (core != null)
                {
                    to_chat(user, span_notice(I18n.T("You pry off []'s inner plate. You can see the core's green glow!", this)));
                    START_PROCESSING(SSobj, core);
                    deconstruction_state = NUKESTATE_CORE_EXPOSED;
                }
                else
                {
                    to_chat(user, span_notice(I18n.T("You pry off []'s inner plate. The inner chamber is empty, save for some beer stains.", this)));
                    deconstruction_state = NUKESTATE_CORE_REMOVED;
                }
                update_appearance();
                Dm.@new<obj_item_stack_sheet_iron>(loc, 15);
                return ITEM_INTERACT_SUCCESS;

            case NUKESTATE_PANEL_REMOVED:
                to_chat(user, span_notice(I18n.T("You start levering []'s inner panel back into place...", this)));
                if (!Dm.truthy(await tool.use_tool(this, user, 3 * SECONDS, volume: 100)))
                    return ITEM_INTERACT_BLOCKING;

                to_chat(user, span_notice(I18n.T("You lever []'s inner panel back into place.", this)));
                deconstruction_state = NUKESTATE_UNSCREWED;
                update_appearance();
                return ITEM_INTERACT_SUCCESS;
        }

        return ITEM_INTERACT_SKIP_TO_ATTACK;
    }

    // tg: code/modules/antagonists/nukeop/equipment/nuclear_bomb/_nuclear_bomb.dm:230
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        if (tool is not obj_item_screwdriver_nuke)
            return ITEM_INTERACT_SKIP_TO_ATTACK;

        switch (deconstruction_state)
        {
            case NUKESTATE_INTACT:
                to_chat(user, span_notice(I18n.T("You start removing []'s front panel's screws...", this)));
                if (!Dm.truthy(await tool.use_tool(this, user, 6 * SECONDS, volume: 100)))
                    return ITEM_INTERACT_BLOCKING;

                deconstruction_state = NUKESTATE_UNSCREWED;
                to_chat(user, span_notice(I18n.T("You remove the screws from []'s front panel.", this)));
                update_appearance();
                return ITEM_INTERACT_SUCCESS;

            case NUKESTATE_UNSCREWED:
                to_chat(user, span_notice(I18n.T("You start screwing []'s front panel back in...", this)));
                if (!Dm.truthy(await tool.use_tool(this, user, 8 * SECONDS, volume: 100)))
                    return ITEM_INTERACT_BLOCKING;

                deconstruction_state = NUKESTATE_INTACT;
                to_chat(user, span_notice(I18n.T("You screw []'s front panel back into place.", this)));
                deconstruction_state = NUKESTATE_INTACT;
                update_appearance();
                return ITEM_INTERACT_SUCCESS;
        }

        return ITEM_INTERACT_SKIP_TO_ATTACK;
    }

    // tg: code/modules/antagonists/nukeop/equipment/nuclear_bomb/_nuclear_bomb.dm:260
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        if (deconstruction_state != NUKESTATE_PANEL_REMOVED)
            return ITEM_INTERACT_SKIP_TO_ATTACK;

        if (!Dm.truthy(tool.tool_start_check(user, amount: 1)))
            return ITEM_INTERACT_BLOCKING;

        to_chat(user, span_notice(I18n.T("You start cutting []'s inner plate...", this)));
        if (!Dm.truthy(await tool.use_tool(this, user, 8 * SECONDS, volume: 100)))
            return ITEM_INTERACT_BLOCKING;

        to_chat(user, span_notice(I18n.T("You cut []'s inner plate.", this)));
        deconstruction_state = NUKESTATE_WELDED;
        update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/antagonists/nukeop/equipment/nuclear_bomb/_nuclear_bomb.dm:277
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        if (deconstruction_state != NUKESTATE_CORE_EXPOSED)
            return await base.attack_hand_secondary(user, modifiers);
        to_chat(user, span_danger(I18n.T("You can't hold [] with your bare hands!", core)));
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/modules/antagonists/nukeop/equipment/nuclear_bomb/_nuclear_bomb.dm:283
    public override async DmTask<object> can_interact(mob user = null, float? require_adjacent_turf = null)
    {
        if (HAS_TRAIT(user, TRAIT_CAN_USE_NUKE))
            return TRUE;

        return await base.can_interact(user, require_adjacent_turf);
    }

    // tg: code/modules/antagonists/nukeop/equipment/nuclear_bomb/_nuclear_bomb.dm:289
    public override object ui_state(mob user = null)
    {
        if (HAS_TRAIT(user, TRAIT_CAN_USE_NUKE))
            return GLOB.physical_state;

        return base.ui_state(user);
    }

    /// Gets the current state of the nuke.
    // tg: code/modules/antagonists/nukeop/equipment/nuclear_bomb/_nuclear_bomb.dm:296
    public virtual int get_nuke_state()
    {
        if (Dm.truthy(exploding))
            return NUKE_ON_EXPLODING;
        if (Dm.truthy(timing))
            return NUKE_ON_TIMING;
        if (Dm.truthy(safety))
            return NUKE_OFF_LOCKED;
        else
            return NUKE_OFF_UNLOCKED;
    }

    // tg: code/modules/antagonists/nukeop/equipment/nuclear_bomb/_nuclear_bomb.dm:306
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (deconstruction_state != NUKESTATE_INTACT)
        {
            icon_state = "nuclearbomb_base";
            return await base.update_icon_state(__extra);
        }

        switch (get_nuke_state())
        {
            case NUKE_OFF_LOCKED:
            case NUKE_OFF_UNLOCKED:
                icon_state = "nuclearbomb_base";
                break;
            case NUKE_ON_TIMING:
                icon_state = "nuclearbomb_timing";
                break;
            case NUKE_ON_EXPLODING:
                icon_state = "nuclearbomb_exploding";
                break;
        }

        return await base.update_icon_state(__extra);
    }

    // tg: code/modules/antagonists/nukeop/equipment/nuclear_bomb/_nuclear_bomb.dm:321
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);

        if (!string.IsNullOrEmpty(lights))
            cut_overlay(lights);
        cut_overlay(interior);

        switch (deconstruction_state)
        {
            case NUKESTATE_UNSCREWED:
                interior = "panel-unscrewed";
                break;
            case NUKESTATE_PANEL_REMOVED:
                interior = "panel-removed";
                break;
            case NUKESTATE_WELDED:
                interior = "plate-welded";
                break;
            case NUKESTATE_CORE_EXPOSED:
                interior = "plate-removed";
                break;
            case NUKESTATE_CORE_REMOVED:
                interior = "core-removed";
                break;
            case NUKESTATE_INTACT:
                interior = null;
                break;
        }

        switch (get_nuke_state())
        {
            case NUKE_OFF_LOCKED:
                lights = null;
                break;
            case NUKE_OFF_UNLOCKED:
                lights = "lights-safety";
                break;
            case NUKE_ON_TIMING:
                lights = "lights-timing";
                break;
            case NUKE_ON_EXPLODING:
                lights = "lights-exploding";
                break;
        }

        add_overlay(lights);
        add_overlay(interior);
        return dot;
    }

    // tg: code/modules/antagonists/nukeop/equipment/nuclear_bomb/_nuclear_bomb.dm:355
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (!Dm.truthy(timing) || Dm.truthy(exploding))
            return null;

        if (Dm.num(detonation_timer) < world.time)
        {
            explode();
            return null;
        }

        float volume = Dm.num(get_time_left()) <= 20 ? 30 : 5;
        playsound(loc as atom, "sound/items/timer.ogg", volume, FALSE);
        return null;
    }

    /// Changes what mode the UI is depending on the state of the nuke.
    // tg: code/modules/antagonists/nukeop/equipment/nuclear_bomb/_nuclear_bomb.dm:367
    public virtual void update_ui_mode()
    {
        if (Dm.truthy(exploded))
        {
            ui_mode = NUKEUI_EXPLODED;
            return;
        }

        if (auth == null)
        {
            ui_mode = NUKEUI_AWAIT_DISK;
            return;
        }

        if (Dm.truthy(timing))
        {
            ui_mode = NUKEUI_TIMING;
            return;
        }

        if (!Dm.truthy(safety))
        {
            ui_mode = NUKEUI_AWAIT_ARM;
            return;
        }

        if (!Dm.truthy(yes_code))
        {
            ui_mode = NUKEUI_AWAIT_CODE;
            return;
        }

        ui_mode = NUKEUI_AWAIT_TIMER;
    }

    // tg: code/modules/antagonists/nukeop/equipment/nuclear_bomb/_nuclear_bomb.dm:390
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "NuclearBomb", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/antagonists/nukeop/equipment/nuclear_bomb/_nuclear_bomb.dm:396
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["disk_present"] = auth;

        bool hidden_code = ui_mode == NUKEUI_AWAIT_CODE && numeric_input != "ERROR";

        string current_code = "";
        if (hidden_code)
        {
            while (length(current_code) < length(numeric_input))
                current_code = current_code + "*";
        }
        else
            current_code = numeric_input;
        while (length(current_code) < 5)
            current_code = current_code + "-";

        string first_status = null;
        string second_status = null;
        switch (ui_mode)
        {
            case NUKEUI_AWAIT_DISK:
                first_status = "DEVICE LOCKED";
                if (Dm.truthy(timing))
                    second_status = Dm.text("TIME: []", get_time_left());
                else
                    second_status = "AWAIT DISK";
                break;
            case NUKEUI_AWAIT_CODE:
                first_status = "INPUT CODE";
                second_status = Dm.text("CODE: []", current_code);
                break;
            case NUKEUI_AWAIT_TIMER:
                first_status = "INPUT TIME";
                second_status = Dm.text("TIME: []", current_code);
                break;
            case NUKEUI_AWAIT_ARM:
                first_status = "DEVICE READY";
                second_status = Dm.text("TIME: []", get_time_left());
                break;
            case NUKEUI_TIMING:
                first_status = "DEVICE ARMED";
                second_status = Dm.text("TIME: []", get_time_left());
                break;
            case NUKEUI_EXPLODED:
                first_status = "DEVICE DEPLOYED";
                second_status = "THANK YOU";
                break;
        }

        data["status1"] = first_status;
        data["status2"] = second_status;
        data["anchored"] = anchored;

        return data;
    }

    // tg: code/modules/antagonists/nukeop/equipment/nuclear_bomb/_nuclear_bomb.dm:442
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        playsound(this, SFX_TERMINAL_TYPE, 20, FALSE);
        switch (Dm.str(action))
        {
            case "eject_disk":
                if (auth != null && auth.loc == this)
                {
                    playsound(this, "sound/machines/terminal/terminal_insert_disc.ogg", 50, FALSE);
                    playsound(this, "sound/machines/nuke/general_beep.ogg", 50, FALSE);
                    await auth.forceMove(get_turf(this) as atom);
                    auth = null;
                    dot = TRUE;
                }
                else
                {
                    var I = usr.is_holding_item_of_type(typeof(obj_item_disk_nuclear)) as obj_item_disk_nuclear;
                    if (I != null && disk_check(I) && Dm.truthy(await usr.transferItemToLoc(I, this)))
                    {
                        playsound(this, "sound/machines/terminal/terminal_insert_disc.ogg", 50, FALSE);
                        playsound(this, "sound/machines/nuke/general_beep.ogg", 50, FALSE);
                        auth = I;
                        dot = TRUE;
                    }
                }
                update_ui_mode();
                break;
            case "keypad":
                if (auth != null)
                {
                    string digit = Dm.str(@params["digit"]);
                    switch (digit)
                    {
                        case "C":
                            if (auth != null && ui_mode == NUKEUI_AWAIT_ARM)
                            {
                                toggle_nuke_safety();
                                yes_code = FALSE;
                                playsound(this, "sound/machines/nuke/confirm_beep.ogg", 50, FALSE);
                                update_ui_mode();
                            }
                            else
                                playsound(this, "sound/machines/nuke/general_beep.ogg", 50, FALSE);
                            numeric_input = "";
                            dot = TRUE;
                            break;
                        case "E":
                            switch (ui_mode)
                            {
                                case NUKEUI_AWAIT_CODE:
                                    if (numeric_input == r_code)
                                    {
                                        numeric_input = "";
                                        yes_code = TRUE;
                                        playsound(this, "sound/machines/nuke/general_beep.ogg", 50, FALSE);
                                        dot = TRUE;
                                    }
                                    else
                                    {
                                        playsound(this, "sound/machines/nuke/angry_beep.ogg", 50, FALSE);
                                        numeric_input = "ERROR";
                                    }
                                    break;
                                case NUKEUI_AWAIT_TIMER:
                                    object number_value = text2num(numeric_input);
                                    if (Dm.truthy(number_value))
                                    {
                                        timer_set = clamp(Dm.num(number_value), minimum_timer_set, maximum_timer_set);
                                        playsound(this, "sound/machines/nuke/general_beep.ogg", 50, FALSE);
                                        toggle_nuke_safety();
                                        dot = TRUE;
                                    }
                                    break;
                                default:
                                    playsound(this, "sound/machines/nuke/angry_beep.ogg", 50, FALSE);
                                    break;
                            }
                            update_ui_mode();
                            break;
                        case "0": case "1": case "2": case "3": case "4": case "5": case "6": case "7": case "8": case "9":
                            if (numeric_input != "ERROR")
                            {
                                numeric_input += digit;
                                if (length(numeric_input) > 5)
                                    numeric_input = "ERROR";
                                else
                                    playsound(this, "sound/machines/nuke/general_beep.ogg", 50, FALSE);
                                dot = TRUE;
                            }
                            break;
                    }
                }
                else
                    playsound(this, "sound/machines/nuke/angry_beep.ogg", 50, FALSE);
                break;
            case "arm":
                if (auth != null && Dm.truthy(yes_code) && !Dm.truthy(safety) && !Dm.truthy(exploded))
                {
                    playsound(this, "sound/machines/nuke/confirm_beep.ogg", 50, FALSE);
                    toggle_nuke_armed();
                    update_ui_mode();
                    dot = TRUE;
                }
                else
                    playsound(this, "sound/machines/nuke/angry_beep.ogg", 50, FALSE);
                break;
            case "anchor":
                if (auth != null && Dm.truthy(yes_code))
                {
                    playsound(this, "sound/machines/nuke/general_beep.ogg", 50, FALSE);
                    await set_anchor(usr);
                }
                else
                    playsound(this, "sound/machines/nuke/angry_beep.ogg", 50, FALSE);
                break;
        }
        return dot;
    }

    /// Anchors the nuke, duh. Can only be done if the disk is inside.
    // tg: code/modules/antagonists/nukeop/equipment/nuclear_bomb/_nuclear_bomb.dm:524
    public virtual async DmTask set_anchor(mob anchorer = null)
    {
        if (Dm.truthy(isinspace()) && !Dm.truthy(anchored))
        {
            if (anchorer != null)
                to_chat(anchorer, span_warning(I18n.T("There is nothing to anchor to!")));
            return;
        }

        await set_anchored(Dm.num(!Dm.truthy(anchored)));
    }

    /// Toggles the safety of the nuke.
    // tg: code/modules/antagonists/nukeop/equipment/nuclear_bomb/_nuclear_bomb.dm:533
    public virtual void toggle_nuke_safety()
    {
        safety = Dm.num(!Dm.truthy(safety));

        // We're safe now, so stop any ongoing timers
        if (Dm.truthy(safety))
        {
            if (Dm.truthy(timing))
            {
                timing = FALSE;
                disarm_nuke();
            }

            detonation_timer = null;
            countdown.stop();
        }
        update_appearance(UPDATE_OVERLAYS); //only the lights overlay are affected by safety
    }

    /// Arms the nuke, or disarms it if it's already active.
    // tg: code/modules/antagonists/nukeop/equipment/nuclear_bomb/_nuclear_bomb.dm:547
    public virtual void toggle_nuke_armed()
    {
        if (Dm.truthy(safety))
        {
            to_chat(usr, span_danger(I18n.T("The safety is still on.")));
            return;
        }

        timing = Dm.num(!Dm.truthy(timing));
        if (Dm.truthy(timing))
            arm_nuke(usr);
        else
            disarm_nuke(usr);
        update_minimap_blip();
    }

    /// Arms the nuke, making it active and triggering all pinpointers to start counting down (+delta alert)
    // tg: code/modules/antagonists/nukeop/equipment/nuclear_bomb/_nuclear_bomb.dm:560
    public virtual void arm_nuke(mob armer = null)
    {
        var our_turf = get_turf(this) as turf;
        message_admins(Dm.text("\\The [] was armed at [] by [].", this, ADMIN_VERBOSEJMP(our_turf), armer != null ? ADMIN_LOOKUPFLW(armer) : "an unknown user"));
        armer?.log_message(Dm.text("armed \\the [].", this), LOG_GAME);
        armer?.add_mob_memory(typeof(datum_memory_bomb_planted_nuke), Dm.named("antagonist", this));

        // the DM var takes the level's number here; the C# var is text (set_level takes either)
        previous_level = Dm.str(SSsecurity_level.get_current_level_as_text());
        detonation_timer = world.time + (timer_set * 10);
        foreach (obj_item_pinpointer_nuke_syndicate nuke_pointer in Dm.each<obj_item_pinpointer_nuke_syndicate>(GLOB.pinpointer_list))
            nuke_pointer.switch_mode_to(TRACK_INFILTRATOR);

        // SEND_GLOBAL_SIGNAL(COMSIG_GLOB_NUKE_DEVICE_ARMED, src) (__DEFINES/dcs/helpers.dm:7), expanded inline (rules 11.1 (e))
        if (Dm.truthy(SSdcs._listen_lookup?[COMSIG_GLOB_NUKE_DEVICE_ARMED]))
            SSdcs._SendSignal(COMSIG_GLOB_NUKE_DEVICE_ARMED, Dm.list(SSdcs, this));

        countdown.start();
        SSsecurity_level.set_level(SEC_LEVEL_DELTA);
        notify_ghosts(
            I18n.T("A nuclear device has been armed in []!", get_area_name(this)),
            source: this,
            header: I18n.T("Nuke Armed"));
        update_appearance();
    }

    /// Disarms the nuke, reverting all pinpointers and the security level
    // tg: code/modules/antagonists/nukeop/equipment/nuclear_bomb/_nuclear_bomb.dm:583
    public virtual void disarm_nuke(mob disarmer = null)
    {
        var our_turf = get_turf(this) as turf;
        message_admins(Dm.text("\\The [] at [] was disarmed by [].", this, ADMIN_VERBOSEJMP(our_turf), disarmer != null ? ADMIN_LOOKUPFLW(disarmer) : "an unknown user"));
        if (disarmer != null)
            disarmer.log_message(Dm.text("disarmed [].", this), LOG_GAME);

        detonation_timer = null;
        SSsecurity_level.set_level(previous_level);

        foreach (obj_item_pinpointer_nuke_syndicate nuke_pointer in Dm.each<obj_item_pinpointer_nuke_syndicate>(GLOB.pinpointer_list))
        {
            nuke_pointer.switch_mode_to(Dm.initial(nuke_pointer, (obj_item_pinpointer_nuke p) => p.mode));
            nuke_pointer.alert = FALSE;
        }

        countdown.stop();
        // SEND_GLOBAL_SIGNAL(COMSIG_GLOB_NUKE_DEVICE_DISARMED, src) (__DEFINES/dcs/helpers.dm:7), expanded inline (rules 11.1 (e))
        if (Dm.truthy(SSdcs._listen_lookup?[COMSIG_GLOB_NUKE_DEVICE_DISARMED]))
            SSdcs._SendSignal(COMSIG_GLOB_NUKE_DEVICE_DISARMED, Dm.list(SSdcs, this));
        update_appearance();
    }

    // tg: code/modules/antagonists/nukeop/equipment/nuclear_bomb/_nuclear_bomb.dm:600
    public virtual void update_minimap_blip()
    {
        if (!Dm.truthy(is_on_minimap))
            return;
        // add_minimap_blip(src, MINIMAP_BOMB_BLIP, "nuke_[timing ? "on" : "off"]", 'icons/ui_icons/minimap/map_blips_large.dmi', TRUE): the
        // minimap (modules/minimap) is not ported
    }

    /// If the nuke is active, gets how much time is left until it detonates, in seconds.
    /// If the nuke is not active, gets how much time the nuke is set for, in seconds.
    // tg: code/modules/antagonists/nukeop/equipment/nuclear_bomb/_nuclear_bomb.dm:608
    public virtual object get_time_left()
    {
        if (Dm.truthy(timing))
            return round(max(0, Dm.num(detonation_timer) - world.time) / 10, 1);
        else
            return timer_set;
    }

    // tg: code/modules/antagonists/nukeop/equipment/nuclear_bomb/_nuclear_bomb.dm:614
    public override async DmTask<object> blob_act(obj_structure_blob attacking_blob = null)
    {
        if (Dm.truthy(exploding))
            return null;
        qdel(this);
        return null;
    }

    // tg: code/modules/antagonists/nukeop/equipment/nuclear_bomb/_nuclear_bomb.dm:619
    public override async DmTask<object> zap_act(object power = null, object zap_flags = null)
    {
        object dot = await base.zap_act(power, zap_flags);
        if (Dm.truthy(Dm.bitand(zap_flags, ZAP_MACHINE_EXPLOSIVE)))
            qdel(this); //like the singulo, tesla deletes it. stops it from exploding over and over
        return dot;
    }

    /**
     * Begins the process of exploding the nuke.
     * [proc/explode] -> [proc/actually_explode] -> [proc/really_actually_explode])
     *
     * Goes through a few timers and plays a cinematic.
     */
    // tg: code/modules/antagonists/nukeop/equipment/nuclear_bomb/_nuclear_bomb.dm:632
    public virtual bool explode()
    {
        if (Dm.truthy(safety))
        {
            timing = FALSE;
            return false;
        }

        exploding = TRUE;
        yes_code = FALSE;
        safety = TRUE;
        update_appearance();
        sound_to_playing_players("sound/announcer/alarm/nuke_alarm.ogg", 70);

        // SEND_GLOBAL_SIGNAL(COMSIG_GLOB_NUKE_DEVICE_DETONATING, src) (__DEFINES/dcs/helpers.dm:7), expanded inline (rules 11.1 (e))
        if (Dm.truthy(SSdcs._listen_lookup?[COMSIG_GLOB_NUKE_DEVICE_DETONATING]))
            SSdcs._SendSignal(COMSIG_GLOB_NUKE_DEVICE_DETONATING, Dm.list(SSdcs, this));

        if (Dm.truthy(SSticker.HasRoundStarted()))
            SSticker.roundend_check_paused = TRUE;
        _addtimer(Dm.@new<datum_callback>(this, nameof(actually_explode)), 10 * SECONDS, file: "code/modules/antagonists/nukeop/equipment/nuclear_bomb/_nuclear_bomb.dm", line: 647);
        return true;
    }

    // tg: code/modules/antagonists/nukeop/equipment/nuclear_bomb/_nuclear_bomb.dm:650
    public virtual async DmTask<object> actually_explode()
    {
        if (core == null)
        {
            await play_cinematic(typeof(datum_cinematic_nuke_no_core), world);
            SSticker.roundend_check_paused = FALSE;
            return null;
        }

        object detonation_status = null;
        var bomb_location = get_turf(this) as turf;
        var nuke_area = get_area(bomb_location) as area;

        // The nuke was on the station zlevel
        if (bomb_location != null && Dm.truthy(is_station_level(bomb_location.z)))
        {
            // Nuke missed, it's in space
            if (nuke_area is area_space)
                detonation_status = DETONATION_NEAR_MISSED_STATION;

            // Nuke missed, it'stoo far from the station
            else if (bomb_location.x < (128 - NUKE_RADIUS)
                || bomb_location.x > (128 + NUKE_RADIUS)
                || bomb_location.y < (128 - NUKE_RADIUS)
                || bomb_location.y > (128 + NUKE_RADIUS))

                detonation_status = DETONATION_NEAR_MISSED_STATION;

            // Confirming good hits, the nuke hit the station
            else
            {
                // SSlag_switch.set_measure(DISABLE_NON_OBSJOBS, TRUE): the lag switch is not declared (its measures are off)
                detonation_status = DETONATION_HIT_STATION;
                GLOB.station_was_nuked = TRUE;
            }
        }

        // The nuke was on the syndicate base
        else if (Dm.truthy(bomb_location.onSyndieBase()))
            detonation_status = DETONATION_HIT_SYNDIE_BASE;

        // The nuke was somewhere wacky - deep space, mining z, centcom? Whatever
        else
            detonation_status = DETONATION_MISSED_STATION;

        // Now go play the cinematic
        GLOB.station_nuke_source = detonation_status;
        await really_actually_explode(detonation_status);
        SSticker.roundend_check_paused = FALSE;

        return detonation_status;
    }

    // tg: code/modules/antagonists/nukeop/equipment/nuclear_bomb/_nuclear_bomb.dm:695
    public virtual async DmTask<object> really_actually_explode(object detonation_status = null)
    {
        Type cinematic = get_cinematic_type(detonation_status);
        if (cinematic != null)
            await play_cinematic(cinematic, world);

        bool drop_level = true;
        switch (Dm.num(detonation_status))
        {
            case DETONATION_HIT_STATION:
                nuke_effects(SSmapping.levels_by_trait(ZTRAIT_STATION) as DmList);
                drop_level = false;
                break;

            case DETONATION_HIT_SYNDIE_BASE:
            {
                priority_announce(
                    "Long Range Scanners indicate that the nuclear device has detonated on a previously unknown base, we assume the base to be of Syndicate Origin. Good work crew.",
                    "Nuclear Operations Command");

                var syndicate_base = await SSmapping.lazy_load_template(LAZY_TEMPLATE_KEY_NUKIEBASE) as datum_turf_reservation;
                Waiting.Detach(gib_the_base(syndicate_base));
                break;
            }

            default:
                priority_announce(
                    "Long Range Scanners indicate that the nuclear device has detonated; however seismic activity on the station is minimal. We anticipate that the device has not detonated on the station itself.",
                    "Nuclear Operations Command");
                break;
        }

        if (drop_level)
            SSsecurity_level.set_level(SEC_LEVEL_RED);
        qdel(this);
        return TRUE;
    }

    // tg: code/modules/antagonists/nukeop/equipment/nuclear_bomb/_nuclear_bomb.dm:714  (the ASYNC block: everyone living on the base's turfs)
    private async DmTask gib_the_base(datum_turf_reservation syndicate_base)
    {
        foreach (turf turf in Dm.each<turf>(syndicate_base.reserved_turfs))
        {
            foreach (mob_living about_to_explode in Dm.each<mob_living>(turf.contents))
                await nuke_gib(about_to_explode, this);
            CHECK_TICK();
        }
    }

    /// Cause nuke effects to the passed z-levels.
    // tg: code/modules/antagonists/nukeop/equipment/nuclear_bomb/_nuclear_bomb.dm:733
    public virtual void nuke_effects(DmList affected_z_levels = null)
    {
        Waiting.Detach(callback_on_everyone_on_z(affected_z_levels, Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.nuke_gib)), this));
    }

    /// Gets what type of cinematic this nuke showcases depending on where we detonated.
    // tg: code/modules/antagonists/nukeop/equipment/nuclear_bomb/_nuclear_bomb.dm:737
    public virtual Type get_cinematic_type(object detonation_status = null)
    {
        if (detonation_status == null)
            return typeof(datum_cinematic_nuke_self_destruct_miss);

        return typeof(datum_cinematic_nuke_self_destruct);
    }
}

public static partial class GlobalProcs
{
    /**
     * Helper proc that handles gibbing someone who has been nuked.
     */
    // tg: code/modules/antagonists/nukeop/equipment/nuclear_bomb/_nuclear_bomb.dm:748
    public static async DmTask<object> nuke_gib(mob_living gibbed = null, atom source = null)
    {
        if (HAS_TRAIT(gibbed, TRAIT_NUKEIMMUNE))
            return FALSE;

        if (gibbed.loc is obj_structure_closet_secure_closet_freezer freezer)
        {
            if (!Dm.truthy(freezer.jones))
            {
                to_chat(gibbed, span_bolddanger(I18n.T("You hold onto [] as [] goes off. Luckily, as [] is lead-lined, you survive.", freezer, source, freezer)));
                freezer.jones = TRUE;
                return FALSE;
            }
        }

        if (gibbed.stat == DEAD)
            return FALSE;

        to_chat(gibbed, span_userdanger(I18n.T("You are shredded to atoms by []!", source)));
        gibbed.investigate_log("has been gibbed by a nuclear blast.", INVESTIGATE_DEATHS);
        await gibbed.gib(DROP_ALL_REMAINS);
        return TRUE;
    }

    /**
     * Invokes a callback on every living mob on the provided z level.
     */
    // tg: code/modules/antagonists/nukeop/equipment/nuclear_bomb/_nuclear_bomb.dm:771
    public static async DmTask callback_on_everyone_on_z(DmList z_levels = null, datum_callback to_do = null, atom optional_source = null)
    {
        if (z_levels == null)
        {
            stack_trace(Dm.text("callback_on_everyone_on_z called []", z_levels != null ? "with an invalid z-level list" : "without any z-levels"),
                file: "code/modules/antagonists/nukeop/equipment/nuclear_bomb/_nuclear_bomb.dm", line: 773);
            return;
        }

        foreach (mob_living victim in Dm.each<mob_living>(GLOB.mob_living_list.Copy()))
        {
            if (QDELETED(victim) || victim.loc == null)
                continue;

            var target_turf = get_turf(victim) as turf;
            if (target_turf != null && !Dm.@in(target_turf.z, z_levels))
                continue;

            await to_do.Invoke(victim, optional_source);
        }
    }
}
