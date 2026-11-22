// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/computer/teleporter.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps TP1 (D333's station order: the bridge's teleporter): the teleporter control console (:1-264): its power station link, the
// Teleporter window (the target, the regime, the calibration), the targets (the tracking beacons and the tracking implants in the
// teleporter regime, the linked stations in the gate regime) picked from a list, the calibration timed by the hub's matter bins,
// a disabled beacon dropping the lock. Deferred: the circuit's USB port and its teleporter control component (:266-326; the
// integrated circuits are not ported, not-translated.md). The var blocks are generated (D111); `id` is hand-written below (tg
// gives it a number's text, the converter typed it as a path).

// tg: code/game/machinery/computer/teleporter.dm:4
public partial class obj_machinery_computer_teleporter : obj_machinery_computer
{
    // tg: code/game/machinery/computer/teleporter.dm:1-2  #define REGIME_TELEPORTER "Teleporter", REGIME_GATE "Gate" (the file's own)
    private const string REGIME_TELEPORTER = "Teleporter";
    private const string REGIME_GATE = "Gate";

    /// The ID of this teleporter, used for linking to power stations
    public object id; // tg: code/game/machinery/computer/teleporter.dm:15

    // The type's defaults (tg: code/game/machinery/computer/teleporter.dm:5): with id declared above, the var block is hand-written
    // (types-converter.md 3.2, mode Hand), so the constructor the generator wrote is here.
    public obj_machinery_computer_teleporter()
    {
        name = "teleporter control console";
        desc = "Used to control a linked teleportation Hub and Station.";
        icon_state = "computer";
        icon_screen = "teleport";
        icon_keyboard = "teleport_key";
        light_color = "#6496FA";
        circuit = typeof(obj_item_circuitboard_computer_teleporter);
    }

    // tg: code/game/machinery/computer/teleporter.dm:23
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        id = Dm.text("[]", rand(1000, 9999));
        link_power_station();

        // tg: AddComponent(/datum/component/usb_port, typecacheof(list(/obj/item/circuit_component/teleporter_control_console), only_root_path = TRUE))
        // DEFERRED (not-translated.md): the integrated circuits' USB port is not ported.
        return dot;
    }

    // tg: code/game/machinery/computer/teleporter.dm:30
    public override object Destroy(float? force = null)
    {
        if (power_station != null)
        {
            power_station.teleporter_console = null;
            power_station = null;
        }
        return base.Destroy(force);
    }

    // tg: code/game/machinery/computer/teleporter.dm:36
    [SignalHandler]
    public virtual void check_for_disabled_beacon(datum target = null)
    {
        if (target == null)
            return;
        if (target.weak_reference != target_ref)
            return;
        turn_off();
        set_teleport_target(null);
    }

    // tg: code/game/machinery/computer/teleporter.dm:44
    public virtual obj_machinery_teleport_station link_power_station()
    {
        if (power_station != null)
            return null;
        foreach (object direction in Dm.each(GLOB.cardinals))
        {
            power_station = Dm.locate<obj_machinery_teleport_station>(get_step(this, Dm.num(direction)));
            if (power_station != null)
            {
                power_station.link_console_and_hub();
                break;
            }
        }
        return power_station;
    }

    // tg: code/game/machinery/computer/teleporter.dm:54
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        object dot = await base.ui_interact(user, ui);
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "Teleporter", name);
            await ui.open();
        }
        return dot;
    }

    // tg: code/game/machinery/computer/teleporter.dm:61
    public override async DmTask<object> ui_data(mob user = null)
    {
        atom target = null;
        if (target_ref != null)
            target = target_ref.resolve() as atom;
        if (target == null)
            target_ref = null;
        DmList data = Dm.list();
        data["power_station"] = power_station != null ? TRUE : FALSE;
        data["teleporter_hub"] = power_station?.teleporter_hub != null ? TRUE : FALSE;
        data["regime_set"] = regime_set;
        data["target"] = target == null ? I18n.T("None") : Dm.text("[] []", get_area(target), regime_set != REGIME_GATE ? "" : REGIME_TELEPORTER);
        data["calibrating"] = calibrating;

        if (Dm.truthy(power_station?.teleporter_hub?.calibrated) || (power_station?.teleporter_hub != null && power_station.teleporter_hub.accuracy >= 3))
            data["calibrated"] = TRUE;
        else
            data["calibrated"] = FALSE;

        return data;
    }

    // tg: code/game/machinery/computer/teleporter.dm:80
    public virtual void turn_off()
    {
        power_station.engaged = FALSE;
        power_station.teleporter_hub.update_appearance();
        power_station.teleporter_hub.calibrated = FALSE;
    }

    // tg: code/game/machinery/computer/teleporter.dm:85
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        if (!Dm.truthy(check_hub_connection()))
        {
            await say(I18n.T("Error: Unable to detect hub."));
            return dot;
        }
        if (Dm.truthy(calibrating))
        {
            await say(I18n.T("Error: Calibration in progress. Stand by."));
            return dot;
        }

        switch (Dm.str(action))
        {
            case "regimeset":
                turn_off();
                reset_regime();
                dot = TRUE;
                break;
            case "settarget":
                turn_off();
                await set_target(usr);
                dot = TRUE;
                break;
            case "calibrate":
                if (target_ref == null)
                {
                    await say(I18n.T("Error: No target set to calibrate to."));
                    return dot;
                }
                if (Dm.truthy(power_station.teleporter_hub.calibrated) || power_station.teleporter_hub.accuracy >= 3)
                {
                    await say(I18n.T("Hub is already calibrated!"));
                    return dot;
                }

                await say(I18n.T("Processing hub calibration to target..."));
                calibrating = TRUE;
                power_station.update_appearance();
                _addtimer(Dm.@new<datum_callback>(this, nameof(finish_calibration)), 5 * SECONDS * (3 - power_station.teleporter_hub.accuracy), file: "code/game/machinery/computer/teleporter.dm", line: 119); //Better parts mean faster calibration
                return TRUE;
        }
        return dot;
    }

    // tg: code/game/machinery/computer/teleporter.dm:122
    public virtual void set_teleport_target(object new_target = null)
    {
        datum old_target = null;
        var new_target_ref = WEAKREF(new_target as datum) as datum_weakref;
        if (target_ref == new_target_ref)
            return;
        if (target_ref != null)
            old_target = target_ref.resolve() as datum;
        SEND_SIGNAL(COMSIG_TELEPORTER_NEW_TARGET, Dm.list(this, new_target));
        target_ref = new_target_ref;
        if (old_target is obj_item_beacon)
            UnregisterSignal(old_target, COMSIG_BEACON_DISABLED);
        if (new_target is obj_item_beacon)
            RegisterSignal((datum)new_target, COMSIG_BEACON_DISABLED, nameof(check_for_disabled_beacon));
    }

    // tg: code/game/machinery/computer/teleporter.dm:136
    public virtual async DmTask finish_calibration()
    {
        calibrating = FALSE;
        if (Dm.truthy(check_hub_connection()))
        {
            power_station.teleporter_hub.calibrated = TRUE;
            await say(I18n.T("Calibration complete."));
        }
        else
            await say(I18n.T("Error: Unable to detect hub."));
        power_station?.update_appearance();
    }

    // tg: code/game/machinery/computer/teleporter.dm:145
    public virtual object check_hub_connection()
    {
        if (power_station == null)
            return FALSE;
        if (power_station.teleporter_hub == null)
            return FALSE;
        return TRUE;
    }

    // tg: code/game/machinery/computer/teleporter.dm:152
    public virtual void reset_regime()
    {
        set_teleport_target(null);
        if (regime_set == REGIME_TELEPORTER)
            regime_set = REGIME_GATE;
        else
            regime_set = REGIME_TELEPORTER;
    }

    /// Gets a list of targets to teleport to.
    /// List is an assoc list of descriptors to locations.
    // tg: code/game/machinery/computer/teleporter.dm:161
    public virtual DmList get_targets()
    {
        DmList targets = Dm.list();
        DmList area_index = Dm.list();

        if (regime_set == REGIME_TELEPORTER)
        {
            foreach (obj_item_beacon beacon in Dm.each<obj_item_beacon>(GLOB.teleportbeacons))
            {
                if (!Dm.truthy(is_eligible(beacon)))
                    continue;

                if (Dm.truthy(beacon.renamed))
                    targets[avoid_assoc_duplicate_keys(Dm.text("[] ([])", beacon.name, format_text(get_area(beacon))), area_index)] = beacon;
                else
                {
                    var area = get_area(beacon) as area;
                    targets[avoid_assoc_duplicate_keys(format_text(area.name), area_index)] = beacon;
                }
            }

            foreach (obj_item_implant_beacon tracking_beacon in Dm.each<obj_item_implant_beacon>(GLOB.tracked_implants))
            {
                if (tracking_beacon.imp_in == null || !isliving(tracking_beacon.loc))
                    continue;

                var implanted = (mob_living)tracking_beacon.loc;
                if (Dm.eq(implanted.stat, DEAD) && Dm.num(implanted.timeofdeath) + Dm.num(tracking_beacon.lifespan_postmortem) < world.time)
                    continue;

                if (Dm.truthy(is_eligible(tracking_beacon)))
                    targets[avoid_assoc_duplicate_keys(Dm.text("[] ([])", implanted.real_name, format_text(get_area(implanted))), area_index)] = tracking_beacon;
            }
        }
        else
        {
            foreach (obj_machinery_teleport_station station in Dm.each<obj_machinery_teleport_station>(power_station.linked_stations))
            {
                if (Dm.truthy(is_eligible(station)) && station.teleporter_hub != null)
                {
                    var area = get_area(station) as area;
                    targets[avoid_assoc_duplicate_keys(format_text(area.name), area_index)] = station;
                }
            }
        }

        return targets;
    }

    /// Given a target station, will power and link it.
    // tg: code/game/machinery/computer/teleporter.dm:194
    public virtual async DmTask lock_in_station(obj_machinery_teleport_station target_station = null)
    {
        if (!Dm.@in(power_station, target_station.linked_stations))
            Dm.append(ref target_station.linked_stations, power_station); // linked_stations |= power_station
        await target_station.set_machine_stat(Dm.bitand(target_station.machine_stat, Dm.bitnot(NOPOWER)));
        if (target_station.teleporter_hub != null)
        {
            await target_station.teleporter_hub.set_machine_stat(Dm.bitand(target_station.teleporter_hub.machine_stat, Dm.bitnot(NOPOWER)));
            target_station.teleporter_hub.update_appearance();
        }
        if (target_station.teleporter_console != null)
        {
            await target_station.teleporter_console.set_machine_stat(Dm.bitand(target_station.teleporter_console.machine_stat, Dm.bitnot(NOPOWER)));
            target_station.teleporter_console.update_appearance();
        }
    }

    // tg: code/game/machinery/computer/teleporter.dm:205
    public virtual async DmTask set_target(mob user = null)
    {
        DmList targets = get_targets();

        if (regime_set == REGIME_TELEPORTER)
        {
            object desc = await tgui_input_list(usr, I18n.T("Select a location to lock in"), I18n.T("Locking Computer"), (DmList)sort_list(targets));
            if (desc == null || !Dm.truthy(await user.can_perform_action(this, ALLOW_SILICON_REACH)))
                return;
            set_teleport_target(targets[desc]);
            user.log_message(Dm.text("set the teleporter target to [].]", targets[desc]), LOG_GAME);
        }
        else
        {
            if (!Dm.truthy(length(targets)))
            {
                to_chat(user, span_alert(I18n.T("No active connected stations located.")));
                return;
            }

            object desc = await tgui_input_list(usr, I18n.T("Select a station to lock in"), I18n.T("Locking Computer"), (DmList)sort_list(targets));
            if (desc == null || !Dm.truthy(await user.can_perform_action(this, ALLOW_SILICON_REACH)))
                return;
            var target_station = targets[desc] as obj_machinery_teleport_station;
            if (target_station == null || target_station.teleporter_hub == null)
                return;
            var target_station_turf = get_turf(target_station);
            user.log_message(Dm.text("set the teleporter target to [].", target_station_turf), LOG_GAME);
            set_teleport_target(target_station.teleporter_hub);
            await lock_in_station(target_station);
        }
    }

    // tg: code/game/machinery/computer/teleporter.dm:232
    public virtual object is_eligible(atom_movable AM = null)
    {
        var T = get_turf(AM) as turf;
        if (T == null)
            return FALSE;
        if (Dm.truthy(is_centcom_level(T.z)) || Dm.truthy(is_away_level(T.z)))
            return FALSE;
        if (!Dm.truthy(check_teleport_valid(AM, get_turf(this))))
            return FALSE;
        return TRUE;
    }
}
