// Translated from tgstation (snapshot 1d99bd7f): code/modules/cargo/expressconsole.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P4d (D298): the whole file, the express supply console (its CargoExpress window): orders by supply pod to the cargo bay or the
// linked beacon, the beacon's printing, the bluespace pod disk and the emagged console's rain of pods. The console declares landingzone
// as object (an area's typepath, then the area, rules 3.2), so its var block is hand-written.

// tg: code/modules/cargo/expressconsole.dm:4
public partial class obj_machinery_computer_cargo_express : obj_machinery_computer_cargo
{
    // tg: code/modules/cargo/expressconsole.dm:1-2  (file-local, #undef at the end)
    private const float EXPRESS_EMAG_DISCOUNT = 0.72f;
    private const float BEACON_PRINT_COOLDOWN = 10 * SECONDS;

    // tg: code/modules/cargo/expressconsole.dm:17
    public object message;
    /// The linked supplypod beacon
    // tg: code/modules/cargo/expressconsole.dm:19
    public obj_item_supplypod_beacon beacon;
    /// Where we droppin boys
    // tg: code/modules/cargo/expressconsole.dm:21  object (rules 3.2): an area's typepath, then the area
    public object landingzone = typeof(area_station_cargo_storage);
    // tg: code/modules/cargo/expressconsole.dm:22
    public Type pod_type = typeof(obj_structure_closet_supplypod);
    /// If this console is locked and needs to be unlocked with an ID
    // tg: code/modules/cargo/expressconsole.dm:24
    public float locked = TRUE;
    /// Is the console in beacon mode? Exists to let beacon know when a pod may come in
    // tg: code/modules/cargo/expressconsole.dm:26
    public float using_beacon = FALSE;
    /// Number of beacons printed. Used to determine beacon names.
    // tg: code/modules/cargo/expressconsole.dm:28  var/static/printed_beacons (a type static, rules 10.6)
    private static float printed_beacons
    {
        get => TgState.Current.obj_machinery_computer_cargo_express__printed_beacons;
        set => TgState.Current.obj_machinery_computer_cargo_express__printed_beacons = value;
    }
    /// Cooldown to prevent beacon spam
    // tg: code/modules/cargo/expressconsole.dm:30
    public float beacon_print_cooldown = 0;

    // tg: code/modules/cargo/expressconsole.dm:5
    public obj_machinery_computer_cargo_express()
    {
        name = "express supply console";
        desc = "This console allows the user to purchase a package with 1/40th of the delivery time: made possible by Nanotrasen's new \"1500mm Orbital Railgun\".All sales are near instantaneous - please choose carefully";
        icon_state = "computer";
        icon_screen = "supply_express";
        circuit = typeof(obj_item_circuitboard_computer_cargo_express);
        blockade_warning = "Bluespace instability detected. Delivery impossible.";
        is_express = TRUE;
        interface_type = "CargoExpress";
    }

    // tg: code/modules/cargo/expressconsole.dm:13
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        req_access = Dm.list(ACCESS_CARGO);
    }

    // tg: code/modules/cargo/expressconsole.dm:32
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        landingzone = GLOB.areas_by_type[landingzone];
        if (landingzone == null)
        {
            // WARNING(MSG) (__HELPERS/logging/_logging.dm:2), expanded inline (rules 11.1 (d), (f): src, usr, __FILE__, __LINE__)
            warning(Dm.text("[] in [] at line [] src: [] usr: [].", Dm.text("[] couldnt find a Quartermaster/Storage (aka cargobay) area on the station, and as such it has set the supplypod landingzone to the area it resides in.", this), "code/modules/cargo/expressconsole.dm", 36, this, usr));
            landingzone = get_area(this);
        }
        return dot;
    }

    // tg: code/modules/cargo/expressconsole.dm:39
    public override object Destroy(float? force = null)
    {
        if (beacon != null)
            beacon.unlink_console();
        return base.Destroy(force);
    }

    // tg: code/modules/cargo/expressconsole.dm:44
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(tool.GetID()) && Dm.truthy(await allowed__proc(user)))
        {
            locked = Dm.truthy(locked) ? FALSE : TRUE;
            to_chat(user, span_notice(I18n.T("You [] the interface.", Dm.truthy(locked) ? I18n.T("lock") : I18n.T("unlock"))));
            return ITEM_INTERACT_SUCCESS;
        }

        if (tool is obj_item_disk_cargo_bluespace_pod)
        {
            if (pod_type == typeof(obj_structure_closet_supplypod_bluespacepod))
            {
                balloon_alert(user, I18n.T("already upgraded!"));
                return ITEM_INTERACT_FAILURE;
            }
            if (!Dm.truthy(await user.temporarilyRemoveItemFromInventory(tool)))
                return ITEM_INTERACT_FAILURE;
            pod_type = typeof(obj_structure_closet_supplypod_bluespacepod); // doesnt affect our circuit board, making reversal possible
            to_chat(user, span_notice(I18n.T("You insert the disk into [], allowing for advanced supply delivery vehicles.", this)));
            await tool.forceMove(this);
            return ITEM_INTERACT_SUCCESS;
        }

        if (tool is obj_item_supplypod_beacon tool_beacon)
        {
            if (tool_beacon.express_console != this)
            {
                tool_beacon.link_console(this, user);
                return ITEM_INTERACT_SUCCESS;
            }

            to_chat(user, span_alert(I18n.T("[] is already linked to [].", this, tool_beacon)));
            return ITEM_INTERACT_FAILURE;
        }

        return NONE;
    }

    // tg: code/modules/cargo/expressconsole.dm:72
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return FALSE;
        if (user != null)
        {
            if (emag_card != null)
                user.visible_message(span_warning(I18n.T("[] swipes [] through []!", user, emag_card, this)));
            to_chat(user, span_notice(I18n.T("You change the routing protocols, allowing the Supply Pod to land anywhere on the station.")));
        }
        obj_flags = Dm.bitor(obj_flags, EMAGGED);
        contraband = TRUE;
        // This also sets this on the circuit board
        if (circuit_value is obj_item_circuitboard_computer_cargo board)
        {
            board.obj_flags = Dm.bitor(board.obj_flags, EMAGGED);
            board.contraband = TRUE;
        }
        return TRUE;
    }

    // tg: code/modules/cargo/expressconsole.dm:87
    public override async DmTask<object> ui_data(mob user = null)
    {
        bool canBeacon = beacon != null && (isturf(beacon.loc) || ismob(beacon.loc)); //is the beacon in a valid location?
        DmList data = Dm.list();
        datum_bank_account account = SSeconomy.get_dep_account(cargo_account);
        if (account != null)
            data["points"] = account.account_balance;
        data["locked"] = locked; //swipe an ID to unlock
        data["siliconUser"] = HAS_SILICON_ACCESS(user) ? TRUE : FALSE;
        data["beaconzone"] = beacon != null ? Dm.text("[]", get_area(beacon)) : ""; //where is the beacon located? outputs in the tgui
        data["using_beacon"] = using_beacon; //is the mode set to deliver to the beacon or the cargobay?
        data["canBeacon"] = !Dm.truthy(using_beacon) || canBeacon ? TRUE : FALSE; //is the mode set to beacon delivery, and is the beacon in a valid location?
        bool cooldown_finished = world.time >= beacon_print_cooldown; // COOLDOWN_FINISHED
        data["canBuyBeacon"] = cooldown_finished && account.account_balance >= BEACON_COST ? TRUE : FALSE;
        data["beaconError"] = Dm.truthy(using_beacon) && !canBeacon ? "(BEACON ERROR)" : ""; //changes button text to include an error alert if necessary
        data["hasBeacon"] = beacon != null ? TRUE : FALSE; //is there a linked beacon?
        data["beaconName"] = beacon != null ? beacon.name : I18n.T("No Beacon Found");
        data["printMsg"] = cooldown_finished ? I18n.T("Print Beacon for [] []", BEACON_COST, MONEY_NAME) : I18n.T("Print Beacon for [] [] ([])", BEACON_COST, MONEY_NAME, beacon_print_cooldown - world.time); //buttontext for printing beacons
        message = I18n.T("Sales are near-instantaneous - please choose carefully.");
        if (Dm.truthy(SSshuttle.supply_blocked))
            message = blockade_warning;
        if (Dm.truthy(using_beacon) && beacon == null)
            message = I18n.T("BEACON ERROR: BEACON MISSING"); //beacon was destroyed
        else if (Dm.truthy(using_beacon) && !canBeacon)
            message = I18n.T("BEACON ERROR: MUST BE EXPOSED"); //beacon's loc/user's loc must be a turf
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            message = I18n.T("(&!#@ERROR: R0UTING_#PRO7O&OL MALF(*CT#ON. $UG%ESTE@ ACT#0N: !^/PULS3-%E)ET CIR*)ITB%ARD.");
        data["message"] = message;
        return data;
    }

    // tg: code/modules/cargo/expressconsole.dm:115
    public override float get_discount() => Dm.truthy(Dm.bitand(obj_flags, EMAGGED)) ? EXPRESS_EMAG_DISCOUNT : 1;

    // tg: code/modules/cargo/expressconsole.dm:118
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        mob user = ui.user;
        switch (Dm.str(action))
        {
            case "LZCargo":
                using_beacon = FALSE;
                if (beacon != null)
                    beacon.update_status(SP_UNREADY); //ready light on beacon will turn off
                break;
            case "LZBeacon":
                using_beacon = TRUE;
                if (beacon != null)
                    beacon.update_status(SP_READY); //turns on the beacon's ready light
                break;
            case "printBeacon":
            {
                datum_bank_account account = SSeconomy.get_dep_account(cargo_account);
                if (account == null || !Dm.truthy(account.adjust_money(-BEACON_COST)))
                    return dot;

                // a ~ten second cooldown for printing beacons to prevent spam
                beacon_print_cooldown = world.time + BEACON_PRINT_COOLDOWN; // COOLDOWN_START
                var new_beacon = Dm.@new<obj_item_supplypod_beacon>(drop_location());
                new_beacon.link_console(this, user as mob_living); //rather than in beacon's Initialize(), we can assign the computer to the beacon by reusing this proc)
                printed_beacons++; //printed_beacons starts at 0, so the first one out will be called beacon # 1
                beacon.name = I18n.T("Supply Pod Beacon #[]", printed_beacons);
                break;
            }
            case "add": //Generate Supply Order first
            {
                if (Dm.truthy(TIMER_COOLDOWN_RUNNING(this, COOLDOWN_EXPRESSPOD_CONSOLE)))
                {
                    await say(I18n.T("Railgun recalibrating. Stand by."));
                    return dot;
                }
                object id = @params["id"];
                id = (object)text2path(Dm.str(id)) ?? id;
                if (SSshuttle.supply_packs[id] is not datum_supply_pack pack)
                    throw Dm.CRASH(Dm.text("Unknown supply pack id given by express order console ui. ID: []", @params["id"]), "code/modules/cargo/expressconsole.dm", 146);

                if (Dm.truthy(Dm.bitand(pack.order_flags, ORDER_GOODY)) && !Dm.truthy(self_paid) && !Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
                {
                    playsound(this, Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 50, FALSE);
                    await say(I18n.T("ERROR: Small crates may only be purchased by private accounts."));
                    return dot;
                }

                object name = "*None Provided*";
                object rank = "*None Provided*";
                object ckey = user.ckey;
                if (user is mob_living_carbon_human H)
                {
                    name = H.get_authentification_name();
                    rank = H.get_assignment(hand_first: TRUE);
                }
                else if (HAS_SILICON_ACCESS(user))
                {
                    name = user.real_name;
                    rank = "Silicon";
                }
                object reason = "";
                var order = Dm.@new<datum_supply_order>(pack, name, rank, ckey, reason);
                datum_bank_account account = SSeconomy.get_dep_account(cargo_account);
                if (account == null && order.pack.get_cost() > 0)
                    return dot;

                if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
                    landingzone = GLOB.areas_by_type[pick(GLOB.the_station_areas)];

                DmList empty_turfs = null;
                if (beacon == null || !Dm.truthy(using_beacon) || Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
                {
                    empty_turfs = Dm.list();
                    foreach (turf_open_floor open_turf in Dm.each<turf_open_floor>(((area)landingzone).get_turfs_from_all_zlevels()))
                    {
                        if (!Dm.truthy(await open_turf.is_blocked_turf()))
                            Dm.append(ref empty_turfs, open_turf);
                    }

                    if (!Dm.truthy(length(empty_turfs)))
                        return dot;
                }

                if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
                {
                    if (account.account_balance < order.pack.get_cost() * -get_discount())
                        return dot;

                    // TIMER_COOLDOWN_START(src, COOLDOWN_EXPRESSPOD_CONSOLE, 10 SECONDS) (__DEFINES/cooldowns.dm:77, inline, rules 11.1 (f))
                    LAZYSET(ref cooldowns, COOLDOWN_EXPRESSPOD_CONSOLE, _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.end_cooldown), this, COOLDOWN_EXPRESSPOD_CONSOLE), 10 * SECONDS, file: "code/modules/cargo/expressconsole.dm", line: 195));
                    order.generateRequisition(get_turf(this));
                    for (int i = 1; i <= MAX_EMAG_ROCKETS; i++)
                    {
                        if (!Dm.truthy(account.adjust_money(order.pack.get_cost() * -get_discount())))
                            break;

                        var emag_turf = (turf)pick(empty_turfs);
                        Dm.remove(ref empty_turfs, emag_turf);
                        if (pack.special_pod != null)
                            Dm.@new<obj_effect_pod_landingzone>(emag_turf, pack.special_pod, order);
                        else
                            Dm.@new<obj_effect_pod_landingzone>(emag_turf, pod_type, order);
                    }

                    update_appearance();
                    return TRUE;
                }

                turf landing_turf;
                if (beacon != null && Dm.truthy(using_beacon))
                {
                    landing_turf = get_turf(beacon);
                    beacon.update_status(SP_LAUNCH);
                }
                else
                    landing_turf = (turf)pick(empty_turfs);

                if (!Dm.truthy(account.adjust_money(-order.pack.get_cost() * get_discount())))
                    return dot;

                // TIMER_COOLDOWN_START(src, COOLDOWN_EXPRESSPOD_CONSOLE, 5 SECONDS) (__DEFINES/cooldowns.dm:77, inline, rules 11.1 (f))
                LAZYSET(ref cooldowns, COOLDOWN_EXPRESSPOD_CONSOLE, _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.end_cooldown), this, COOLDOWN_EXPRESSPOD_CONSOLE), 5 * SECONDS, file: "code/modules/cargo/expressconsole.dm", line: 221));
                if (pack.special_pod != null)
                    Dm.@new<obj_effect_pod_landingzone>(landing_turf, pack.special_pod, order);
                else
                    Dm.@new<obj_effect_pod_landingzone>(landing_turf, pod_type, order);

                update_appearance();
                return TRUE;
            }
        }
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/cargo/expressconsole.dm:28  var/static/printed_beacons
    internal float obj_machinery_computer_cargo_express__printed_beacons;
}
