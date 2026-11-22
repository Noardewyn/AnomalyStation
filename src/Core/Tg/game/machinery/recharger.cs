// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/recharger.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7x slice B: the recharger, whole. The speech lines ("has finished recharging!", say(), M9) and the emissive overlay (DV12) are
// deferred leaves. The power pack branch is translated (M7x slice C), and the battle rifle recalibration branch (M7x slice D). The var
// block is generated (D111).

// tg: code/game/machinery/recharger.dm:1
public partial class obj_machinery_recharger : obj_machinery
{
    // tg: code/game/machinery/recharger.dm:15  var/static/list/allowed_devices (a type-level static, rules 10.6)
    public static ref object allowed_devices => ref TgState.Current.obj_machinery_recharger__allowed_devices;

    // tg: code/game/machinery/recharger.dm:24
    public override async DmTask<object> RefreshParts()
    {
        object dot = await base.RefreshParts();
        foreach (datum_stock_part_capacitor capacitor in Dm.each<datum_stock_part_capacitor>(component_parts))
            recharge_coeff = Dm.num(capacitor.tier);
        return dot;
    }

    // tg: code/game/machinery/recharger.dm:29
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (!in_range(user, this) && !issilicon(user) && !isobserver(user))
        {
            Dm.append(ref dot, span_warning(I18n.T("You're too far away to examine []'s contents and display!", this)));
            return dot;
        }

        if (charging != null)
        {
            Dm.append(ref dot, Dm.text("[]\n- [].", span_notice(I18n.T("\\The [] contains:", this)), span_notice(I18n.T("\\A [].", charging))));
        }

        if (Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(NOPOWER, BROKEN))))
            return dot;
        bool status_display_message_shown = false;
        if (Dm.truthy(using_power))
        {
            status_display_message_shown = true;
            Dm.append(ref dot, span_notice(I18n.T("The status display reads:")));
            Dm.append(ref dot, span_notice(I18n.T("- Recharging efficiency: <b>[]%</b>.", recharge_coeff * 100)));
        }

        if (charging == null)
            return dot;
        if (!status_display_message_shown)
            Dm.append(ref dot, span_notice(I18n.T("The status display reads:")));

        obj_item_stock_parts_power_store charging_cell = charging.get_cell() as obj_item_stock_parts_power_store;
        if (charging_cell != null)
        {
            Dm.append(ref dot, span_notice(I18n.T("- \\The []'s cell is at <b>[]%</b>.", charging, charging_cell.percent())));
            return dot;
        }
        if (istype(charging, typeof(obj_item_ammo_box_magazine_recharge)))
        {
            obj_item_ammo_box_magazine_recharge power_pack = charging as obj_item_ammo_box_magazine_recharge;
            Dm.append(ref dot, span_notice(I18n.T("- \\The []'s cell is at <b>[]%</b>.", charging, 100 * power_pack.stored_ammo.Count / power_pack.max_ammo))); // PERCENT(x)
            return dot;
        }
        if (istype(charging, typeof(obj_item_gun_ballistic_automatic_battle_rifle)))
        {
            obj_item_gun_ballistic_automatic_battle_rifle recalibrating_gun = charging as obj_item_gun_ballistic_automatic_battle_rifle;
            Dm.append(ref dot, span_notice(I18n.T("- \\The []'s system degradation is at stage [] of []</b>.", charging, recalibrating_gun.degradation_stage, recalibrating_gun.degradation_stage_max)));
            Dm.append(ref dot, span_notice(I18n.T("- \\The []'s degradation buffer is at <b>[]%</b>.", charging, 100 * recalibrating_gun.shots_before_degradation / recalibrating_gun.max_shots_before_degradation))); // PERCENT(x)
            return dot;
        }
        Dm.append(ref dot, span_notice(I18n.T("- \\The [] is not reporting a power level.", charging)));
        return dot;
    }

    // tg: code/game/machinery/recharger.dm:67
    public override async DmTask<object> Entered(atom_movable arrived = null, atom old_loc = null, DmList old_locs = null)
    {
        allowed_devices ??= typecacheof(Dm.list(typeof(obj_item_gun_energy), typeof(obj_item_melee_baton_security), typeof(obj_item_ammo_box_magazine_recharge), typeof(obj_item_modular_computer), typeof(obj_item_gun_ballistic_automatic_battle_rifle)));
        if (Dm.truthy(is_type_in_typecache(arrived, allowed_devices)))
        {
            charging = arrived as obj_item;
            START_PROCESSING(SSmachines, this);
            update_use_power(ACTIVE_POWER_USE);
            using_power = TRUE;
            update_appearance();
        }
        return await base.Entered(arrived, old_loc, old_locs);
    }

    // tg: code/game/machinery/recharger.dm:76
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        if (gone == charging)
        {
            if (!QDELING(charging))
                charging.update_appearance();
            charging = null;
            update_use_power(IDLE_POWER_USE);
            using_power = FALSE;
            update_appearance();
        }
        return await base.Exited(gone, direction);
    }

    // tg: code/game/machinery/recharger.dm:86
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        allowed_devices ??= typecacheof(Dm.list(typeof(obj_item_gun_energy), typeof(obj_item_melee_baton_security), typeof(obj_item_ammo_box_magazine_recharge), typeof(obj_item_modular_computer), typeof(obj_item_gun_ballistic_automatic_battle_rifle)));
        if (!Dm.truthy(is_type_in_typecache(tool, allowed_devices)))
            return NONE;

        if (!Dm.truthy(anchored))
        {
            to_chat(user, span_notice(I18n.T("[] isn't connected to anything!", this)));
            return ITEM_INTERACT_BLOCKING;
        }
        if (charging != null || Dm.truthy(panel_open))
            return ITEM_INTERACT_BLOCKING;

        area our_area = get_area(this) as area; //Check to make sure user's not in space doing it, and that the area got proper power.
        if (!isarea(our_area) || our_area.power_equip == 0)
        {
            to_chat(user, span_notice(I18n.T("[] blinks red as you try to insert [].", this, tool)));
            return ITEM_INTERACT_BLOCKING;
        }

        if (istype(tool, typeof(obj_item_gun_energy)))
        {
            obj_item_gun_energy energy_gun = tool as obj_item_gun_energy;
            if (!Dm.truthy(energy_gun.can_charge))
            {
                to_chat(user, span_notice(I18n.T("Your gun has no external power connector.")));
                return ITEM_INTERACT_BLOCKING;
            }
        }
        await user.transferItemToLoc(tool, this);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/recharger.dm:109
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        if (charging != null)
        {
            to_chat(user, span_notice(I18n.T("Remove the charging item first!")));
            return ITEM_INTERACT_BLOCKING;
        }
        await set_anchored(!Dm.truthy(anchored) ? TRUE : FALSE);
        power_change();
        to_chat(user, span_notice(I18n.T("You [] [].", Dm.truthy(anchored) ? "attached" : "detached", this)));
        tool.play_tool_sound(this);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/recharger.dm:119
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        return (!Dm.truthy(anchored) || charging != null) ? ITEM_INTERACT_BLOCKING : default_deconstruction_screwdriver(user, tool);
    }

    // tg: code/game/machinery/recharger.dm:122
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        return await default_deconstruction_crowbar(user, tool);
    }

    // tg: code/game/machinery/recharger.dm:125
    public override object can_crowbar_deconstruct()
    {
        return Dm.truthy(base.can_crowbar_deconstruct()) && Dm.truthy(anchored) && charging == null ? TRUE : FALSE;
    }

    // tg: code/game/machinery/recharger.dm:128
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;

        add_fingerprint(user);
        if (charging == null || Dm.truthy(await user.put_in_hands(charging)))
            return null;
        await charging.forceMove(drop_location() as atom);
        return null;
    }

    // tg: code/game/machinery/recharger.dm:138
    public override async DmTask<object> attack_tk(mob user = null)
    {
        if (charging == null)
            return null;
        await charging.forceMove(drop_location() as atom);
        return COMPONENT_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/game/machinery/recharger.dm:144
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(NOPOWER, BROKEN))) || !Dm.truthy(anchored))
            return PROCESS_KILL;

        using_power = FALSE;
        if (charging == null)
            return PROCESS_KILL;
        obj_item_stock_parts_power_store charging_cell = charging.get_cell() as obj_item_stock_parts_power_store;
        if (charging_cell != null)
        {
            if (charging_cell.charge < charging_cell.maxcharge)
            {
                Waiting.NoSleep(charge_cell__proc(charging_cell.chargerate * recharge_coeff * Dm.num(seconds_per_tick), charging_cell));
                if (charging_cell.charge >= charging_cell.maxcharge) //Inserted thing is at max charge/ammo, notify those around us
                {
                    playsound(this, Dm.resource("sound/machines/ping.ogg"), 30, TRUE);
                    Waiting.NoSleep(say(I18n.T("[] has finished recharging!", charging)));
                    // Modular computers (mainly PDAs) can remain on and drain their cell while charging.
                    // Unless we stop, the computer will constantly spam the finished recharging message.
                    if (istype(charging, typeof(obj_item_modular_computer)))
                    {
                        update_appearance();
                        return PROCESS_KILL;
                    }
                }
                else
                {
                    using_power = TRUE;
                }
            }
            update_appearance();
            return null;
        }

        if (istype(charging, typeof(obj_item_ammo_box_magazine_recharge))) //if you add any more snowflake ones, make sure to update the examine messages too.
        {
            obj_item_ammo_box_magazine_recharge power_pack = charging as obj_item_ammo_box_magazine_recharge;
            for (float charge_iterations = 1; charge_iterations <= recharge_coeff; charge_iterations++)
            {
                if (power_pack.stored_ammo.Count < power_pack.max_ammo)
                {
                    power_pack.stored_ammo.Add(Dm.@new(power_pack.ammo_type, power_pack));
                    Waiting.NoSleep(use_energy(active_power_usage * Dm.num(seconds_per_tick)));
                    if (power_pack.stored_ammo.Count >= power_pack.max_ammo)
                    {
                        playsound(this, Dm.resource("sound/machines/ping.ogg"), 30, TRUE);
                        Waiting.NoSleep(say(I18n.T("[] has finished recharging!", charging)));
                    }
                    else
                    {
                        using_power = TRUE;
                    }
                }
            }
            update_appearance();
            return null;
        }

        if (istype(charging, typeof(obj_item_gun_ballistic_automatic_battle_rifle)))
        {
            obj_item_gun_ballistic_automatic_battle_rifle recalibrating_gun = charging as obj_item_gun_ballistic_automatic_battle_rifle;

            if (recalibrating_gun.degradation_stage != 0)
            {
                recalibrating_gun.attempt_recalibration(FALSE);
                Waiting.NoSleep(use_energy(active_power_usage * recharge_coeff * Dm.num(seconds_per_tick)));
                using_power = TRUE;
            }

            else if (recalibrating_gun.shots_before_degradation < recalibrating_gun.max_shots_before_degradation)
            {
                recalibrating_gun.attempt_recalibration(TRUE, recharge_coeff);
                Waiting.NoSleep(use_energy(active_power_usage * recharge_coeff * Dm.num(seconds_per_tick)));
                if (recalibrating_gun.shots_before_degradation == recalibrating_gun.max_shots_before_degradation)
                {
                    playsound(this, Dm.resource("sound/machines/ping.ogg"), 30, TRUE);
                    Waiting.NoSleep(say(I18n.T("[] has finished recalibrating!", charging)));
                }
                else
                {
                    using_power = TRUE;
                }
            }
            update_appearance();
            return null;
        }
        return null;
    }

    // tg: code/game/machinery/recharger.dm:201
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (Dm.truthy(Dm.bitand(dot, EMP_PROTECT_CONTENTS)))
            return dot;
        if (Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(NOPOWER, BROKEN))) || !Dm.truthy(anchored))
            return dot;
        if (istype(charging, typeof(obj_item_gun_energy)))
        {
            obj_item_gun_energy energy_gun = charging as obj_item_gun_energy;
            if (energy_gun.cell != null)
                await energy_gun.cell.emp_act(severity);
        }
        else if (istype(charging, typeof(obj_item_melee_baton_security)))
        {
            obj_item_melee_baton_security batong = charging as obj_item_melee_baton_security;
            if (batong.cell != null)
                batong.cell.charge = 0;
        }
        return dot;
    }

    // tg: code/game/machinery/recharger.dm:217
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(NOPOWER, BROKEN))) || !Dm.truthy(anchored))
            return dot;
        if (Dm.truthy(panel_open))
        {
            Dm.append(ref dot, mutable_appearance(icon, Dm.text("[]-open", base_icon_state), alpha: this.alpha));
            return dot;
        }

        string icon_to_use = Dm.text("[]-[]", base_icon_state, charging == null ? "empty" : (Dm.truthy(using_power) ? "charging" : "full"));
        Dm.append(ref dot, mutable_appearance(icon, icon_to_use, alpha: this.alpha));
        // DEVIATION(DV12): . += emissive_appearance(icon, icon_to_use, src, alpha = src.alpha), the glow on the emissive plane, is not created
        // (lighting planes and emissives are not ported).
        return dot;
    }
}

// The static lists of the recharger (rules 10.6: type-level statics live in TgState).
public sealed partial class TgState
{
    // tg: code/game/machinery/recharger.dm:15  var/static/list/allowed_devices = typecacheof(...), built at first use
    internal object obj_machinery_recharger__allowed_devices;
}
