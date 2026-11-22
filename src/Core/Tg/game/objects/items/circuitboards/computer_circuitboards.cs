// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/circuitboards/computer_circuitboards.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7 (the computer frames, D171): the computer board's base. Its one proc (examine) is translated, so the boards that only set
// vars turn live with it and give the machine they build its default parts (the board in component_parts, circuitboard.cs);
// the boards with procs of their own (the syndicate shuttle's Destroy) stay inert. The var blocks are generated. The engineering
// package E6 (D287): the atmospheric alert board's examine and its multitool mode switch. Gaps P4d (D298): the supply consoles'
// boards (their receiver spectrum, emag and the console they configure). Gaps RD2 (D333): the R&D console's board.

// tg: code/game/objects/items/circuitboards/computer_circuitboards.dm:2
public partial class obj_item_circuitboard_computer : obj_item_circuitboard
{
    // tg: code/game/objects/items/circuitboards/computer_circuitboards.dm:6
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(GetComponent(typeof(datum_component_gps))))
            Dm.append(ref dot, span_info(I18n.T("there's a small, blinking light!")));
        return dot;
    }
}

// tg: code/game/objects/items/circuitboards/computer_circuitboards.dm:33
public partial class obj_item_circuitboard_computer_atmos_alert : obj_item_circuitboard_computer
{
    // tg: code/game/objects/items/circuitboards/computer_circuitboards.dm:43
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_info(I18n.T("The board is configured to [].", Dm.truthy(station_only) ? I18n.T("track all station and mining alarms") : I18n.T("track alarms on the same z-level"))));
        Dm.append(ref dot, span_notice(I18n.T("The board mode can be changed with a [].", EXAMINE_HINT(I18n.T("multitool")))));
        return dot;
    }

    // tg: code/game/objects/items/circuitboards/computer_circuitboards.dm:48
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        station_only = Dm.truthy(station_only) ? FALSE : TRUE;
        balloon_alert(user, I18n.T("tracking set to []", Dm.truthy(station_only) ? I18n.T("station") : I18n.T("z-level")));
        return TRUE;
    }
}

// The tram port (2026-11-12): the tram controls' board.
// tg: code/game/objects/items/circuitboards/computer_circuitboards.dm:391
public partial class obj_item_circuitboard_computer_tram_controls : obj_item_circuitboard_computer
{
    // tg: code/game/objects/items/circuitboards/computer_circuitboards.dm:397
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_info(I18n.T("The board is configured for tram ID [] and a [] installation.", specific_transport_id, install_type)));
        Dm.append(ref dot, span_notice(I18n.T("The tram ID can be changed with a []. The installation mode can be changed with a [].", EXAMINE_HINT("multitool"), EXAMINE_HINT("screwdriver"))));
        return dot;
    }

    // tg: code/game/objects/items/circuitboards/computer_circuitboards.dm:402
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        object selected_install_type = await tgui_input_list(user, I18n.T("Window mounted or standalone?"), I18n.T("Off the rails"), Dm.list(NORMAL_WINDOW, SPLIT_WINDOW, STANDALONE));
        if (selected_install_type == null)
            return NONE;
        install_type = Dm.text("[]", selected_install_type);
        to_chat(user, span_notice(I18n.T("[] is now aligned in installation mode [].", this, install_type)));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/circuitboards/computer_circuitboards.dm:410
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        object selected_transport_id = await tgui_input_list(user, I18n.T("Which tram?"), I18n.T("Off the rails"), SStransport.debug_tram_list);
        if (selected_transport_id == null)
            return NONE;
        specific_transport_id = Dm.text("[]", selected_transport_id);
        to_chat(user, span_notice(I18n.T("[] is now programmed to control [].", this, specific_transport_id)));
        return ITEM_INTERACT_SUCCESS;
    }
}

// The syndicate shuttle's board (syndicate N1b, D316: the infiltrator's console; the war declaration reads its challenge time)
public partial class datum_controller_global_vars
{
    // tg: code/_globalvars/lists/objects.dm:17  GLOBAL_LIST_EMPTY(syndicate_shuttle_boards)
    public DmList syndicate_shuttle_boards;

    // tg: code/_globalvars/lists/objects.dm:17  (GLOBAL_MANAGED(syndicate_shuttle_boards, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalsyndicate_shuttle_boards()
    {
        syndicate_shuttle_boards = Dm.list();
        Dm.append(ref gvars_datum_init_order, "syndicate_shuttle_boards");
    }
}

// tg: code/game/objects/items/circuitboards/computer_circuitboards.dm:356
public partial class obj_item_circuitboard_computer_syndicate_shuttle : obj_item_circuitboard_computer
{
    // tg: code/game/objects/items/circuitboards/computer_circuitboards.dm:364
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.append(ref GLOB.syndicate_shuttle_boards, this);
        return dot;
    }

    // tg: code/game/objects/items/circuitboards/computer_circuitboards.dm:368
    public override object Destroy(float? force = null)
    {
        Dm.remove(ref GLOB.syndicate_shuttle_boards, this);
        return base.Destroy(force);
    }
}

// tg: code/game/objects/items/circuitboards/computer_circuitboards.dm:613  (gaps P4d)
public partial class obj_item_circuitboard_computer_cargo : obj_item_circuitboard_computer
{
    // tg: code/game/objects/items/circuitboards/computer_circuitboards.dm:619
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        object dot = await base.multitool_act(user, tool);
        if (!Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
        {
            contraband = Dm.truthy(contraband) ? FALSE : TRUE;
            to_chat(user, span_notice(I18n.T("Receiver spectrum set to [].", Dm.truthy(contraband) ? I18n.T("Broad") : I18n.T("Standard"))));
        }
        else
            to_chat(user, span_alert(I18n.T("The spectrum chip is unresponsive.")));
        return dot;
    }

    // tg: code/game/objects/items/circuitboards/computer_circuitboards.dm:627
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return FALSE;

        contraband = TRUE;
        obj_flags = Dm.bitor(obj_flags, EMAGGED);
        to_chat(user, span_notice(I18n.T("You adjust []'s routing and receiver spectrum, unlocking special supplies and contraband.", this)));
        return TRUE;
    }

    // tg: code/game/objects/items/circuitboards/computer_circuitboards.dm:636
    public override void configure_machine(obj_machinery machine = null)
    {
        if (machine is not obj_machinery_computer_cargo cargo_machine)
            throw Dm.CRASH(Dm.text("Cargo board attempted to configure incorrect machine type: [] ([])", machine, machine?.type), "code/game/objects/items/circuitboards/computer_circuitboards.dm", 638);

        cargo_machine.contraband = contraband;
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            cargo_machine.obj_flags = Dm.bitor(cargo_machine.obj_flags, EMAGGED);
        else
            cargo_machine.obj_flags = Dm.bitand(cargo_machine.obj_flags, ~EMAGGED);
    }
}

// tg: code/game/objects/items/circuitboards/computer_circuitboards.dm:646  (gaps P4d)
public partial class obj_item_circuitboard_computer_cargo_express : obj_item_circuitboard_computer_cargo
{
    // tg: code/game/objects/items/circuitboards/computer_circuitboards.dm:650
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return FALSE;

        contraband = TRUE;
        obj_flags = Dm.bitor(obj_flags, EMAGGED);
        to_chat(user, span_notice(I18n.T("You change the routing protocols, allowing the Drop Pod to land anywhere on the station.")));
        return TRUE;
    }

    // tg: code/game/objects/items/circuitboards/computer_circuitboards.dm:659  (no ..(): it replaces the cargo board's)
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
        {
            contraband = Dm.truthy(contraband) ? FALSE : TRUE;
            to_chat(user, span_notice(I18n.T("Receiver spectrum set to [].", Dm.truthy(contraband) ? I18n.T("Broad") : I18n.T("Standard"))));
            return TRUE;
        }
        else
        {
            to_chat(user, span_notice(I18n.T("You reset the destination-routing protocols and receiver spectrum to factory defaults.")));
            contraband = FALSE;
            obj_flags = Dm.bitand(obj_flags, ~EMAGGED);
            return TRUE;
        }
    }
}


// gaps RD2 (D333): the R&D console's board: the lock research access toggles, the node announcements its multitool silences, the emag
// that unlocks it and announces every node on the common channel.
// tg: code/game/objects/items/circuitboards/computer_circuitboards.dm:470
public partial class obj_item_circuitboard_computer_rdconsole : obj_item_circuitboard_computer
{
    // tg: code/game/objects/items/circuitboards/computer_circuitboards.dm:484
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_info(I18n.T("The board is configured to [] researched nodes on radio.", Dm.truthy(silence_announcements) ? "silence" : "announce")));
        Dm.append(ref dot, span_notice(I18n.T("The board mode can be changed with a [].", EXAMINE_HINT("multitool"))));
        Dm.append(ref dot, span_notice(I18n.T("The board is [], and can be [] with an ID that has research access.", Dm.truthy(locked) ? "locked" : "unlocked", Dm.truthy(locked) ? "unlocked" : "locked")));
        return dot;
    }

    // tg: code/game/objects/items/circuitboards/computer_circuitboards.dm:490
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        object dot = await base.multitool_act(user, tool);
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
        {
            balloon_alert(user, "board mode is broken!");
            return dot;
        }
        silence_announcements = Dm.truthy(silence_announcements) ? FALSE : TRUE;
        balloon_alert(user, Dm.text("announcements []", Dm.truthy(silence_announcements) ? "enabled" : "disabled"));
        return dot;
    }

    // tg: code/game/objects/items/circuitboards/computer_circuitboards.dm:498
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        await DmTask.CompletedTask;
        if (Dm.truthy(locked))
        {
            locked = FALSE;
            to_chat(user, span_notice(I18n.T("You magnetically trigger the locking mechanism, causing it to unlock.")));
        }

        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return FALSE;

        Dm.combine(ref obj_flags, EMAGGED);
        silence_announcements = FALSE;
        to_chat(user, span_notice(I18n.T("You overload the node announcement chip, forcing every node to be announced on the common channel.")));
        return TRUE;
    }

    // tg: code/game/objects/items/circuitboards/computer_circuitboards.dm:511
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        await DmTask.CompletedTask;
        if (Dm.truthy(user.combat_mode) || !isidcard(tool))
            return NONE;
        if (!Dm.truthy(check_access(tool)))
        {
            balloon_alert(user, "no access!");
            return ITEM_INTERACT_BLOCKING;
        }
        locked = Dm.truthy(locked) ? FALSE : TRUE;
        balloon_alert(user, Dm.truthy(locked) ? "locked" : "unlocked");
        user.visible_message(
            span_notice(I18n.T("\\The [] unlock[] \\the [] with \\the [].", user, user.p_s(), this, tool)),
            span_notice(I18n.T("You unlock \\the [] with \\the [].", this, tool)),
            span_hear(I18n.T("You hear a soft beep.")));
        return ITEM_INTERACT_SUCCESS;
    }
}
