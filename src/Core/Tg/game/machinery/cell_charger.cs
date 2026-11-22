// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/cell_charger.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Package E9 (docs/ai/specs/engineering.md): the whole file. The examine is translated (X1). The var block is
// generated.

// tg: code/game/machinery/cell_charger.dm:1
public partial class obj_machinery_cell_charger : obj_machinery
{
    // tg: code/game/machinery/cell_charger.dm:12
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);

        if (charging == null)
            return dot;

        if (!Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(BROKEN, NOPOWER))))
        {
            float newlevel = round(Dm.num(charging.percent()) * 4 / 100);
            Dm.append(ref dot, Dm.text("ccharger-o[]", newlevel));
        }
        Dm.append(ref dot, Dm.image(charging.icon, charging.icon_state));
        if (Dm.truthy(charging.grown_battery))
            Dm.append(ref dot, mutable_appearance(Dm.resource("icons/obj/machines/cell_charger.dmi"), "grown_wires"));
        Dm.append(ref dot, Dm.text("ccharger-[]-on", charging.connector_type));
        if ((charging.charge > 0.01f) && Dm.truthy(charging.charge_light_type))
            Dm.append(ref dot, mutable_appearance(Dm.resource("icons/obj/machines/cell_charger.dmi"), Dm.text("cell-[]-o[]", charging.charge_light_type, (Dm.num(charging.percent()) >= 99.5f) ? 2 : 1)));
        return dot;
    }

    // tg: code/game/machinery/cell_charger.dm:28
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, I18n.T("There's [] in the charger.", charging != null ? I18n.T("\\a []", charging) : I18n.T("no cell")));
        if (charging != null)
            Dm.append(ref dot, I18n.T("Current charge: []%.", round(Dm.num(charging.percent()), 1)));
        if (in_range(user, this) || isobserver(user))
            Dm.append(ref dot, span_notice(I18n.T("The status display reads: Charging power: <b>[]</b>.", display_power(charge_rate, convert: FALSE))));
        return dot;
    }

    // tg: code/game/machinery/cell_charger.dm:36
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        if (charging != null)
            return NONE;
        if (Dm.truthy(await default_unfasten_wrench(user, tool)))
            update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/cell_charger.dm:43
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        return charging != null ? NONE : default_deconstruction_screwdriver(user, tool);
    }

    // tg: code/game/machinery/cell_charger.dm:46
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        return await default_deconstruction_crowbar(user, tool);
    }

    // tg: code/game/machinery/cell_charger.dm:49
    public override object can_crowbar_deconstruct()
    {
        return Dm.truthy(base.can_crowbar_deconstruct()) && charging == null ? TRUE : FALSE;
    }

    // tg: code/game/machinery/cell_charger.dm:52
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!istype(tool, typeof(obj_item_stock_parts_power_store_cell)) || Dm.truthy(panel_open))
            return NONE;

        if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
        {
            to_chat(user, span_warning(I18n.T("[] is broken!", this)));
            return ITEM_INTERACT_BLOCKING;
        }
        if (!Dm.truthy(anchored))
        {
            to_chat(user, span_warning(I18n.T("[] isn't attached to the ground!", this)));
            return ITEM_INTERACT_BLOCKING;
        }
        if (charging != null)
        {
            to_chat(user, span_warning(I18n.T("There is already a cell in the charger!")));
            return ITEM_INTERACT_BLOCKING;
        }

        area charge_area = get_area(this) as area;
        if (!isarea(charge_area))
            return ITEM_INTERACT_BLOCKING;
        if (charge_area.power_equip == 0) // There's no APC in this area, don't try to cheat power!
        {
            to_chat(user, span_warning(I18n.T("[] blinks red as you try to insert the cell!", this)));
            return ITEM_INTERACT_BLOCKING;
        }
        if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
            return ITEM_INTERACT_BLOCKING;

        charging = tool as obj_item_stock_parts_power_store_cell;
        user.visible_message(
            span_notice(I18n.T("[] inserts a cell into [].", user, this)),
            span_notice(I18n.T("You insert a cell into [].", this)));
        update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/cell_charger.dm:83
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        if (charging != null)
            await charging.forceMove(drop_location() as atom);
        return null;
    }

    // tg: code/game/machinery/cell_charger.dm:86
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (gone == charging)
            charging = null;
        return dot;
    }

    // tg: code/game/machinery/cell_charger.dm:91
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref charging);
        return base.Destroy(force);
    }

    // tg: code/game/machinery/cell_charger.dm:95
    public virtual async DmTask<object> removecell(object new_loc = null)
    {
        object dot = charging;
        charging.update_appearance();
        await charging.forceMove(new_loc as atom);
        charging = null;
        update_appearance();
        return dot;
    }

    // tg: code/game/machinery/cell_charger.dm:102
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot) || charging == null)
            return dot;

        charging.add_fingerprint(user);
        user.visible_message(span_notice(I18n.T("[] removes [] from [].", user, charging, this)), span_notice(I18n.T("You remove [] from [].", charging, this)));
        await user.put_in_hands(await removecell(drop_location()) as obj_item);
        return dot;
    }

    // tg: code/game/machinery/cell_charger.dm:111
    public override async DmTask<object> attack_tk(mob user = null)
    {
        if (charging == null)
            return null;

        to_chat(user, span_notice(I18n.T("You telekinetically remove [] from [].", charging, this)));
        await removecell(drop_location());
        return COMPONENT_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/game/machinery/cell_charger.dm:119
    public override async DmTask<object> attack_ai(mob user = null)
    {
        return null;
    }

    // tg: code/game/machinery/cell_charger.dm:122
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);

        if (Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(BROKEN, NOPOWER))) || Dm.truthy(Dm.bitand(dot, EMP_PROTECT_CONTENTS)))
            return dot;

        if (charging != null)
            await charging.emp_act(severity);
        return dot;
    }

    // tg: code/game/machinery/cell_charger.dm:131
    public override async DmTask<object> RefreshParts()
    {
        object dot = await base.RefreshParts();
        charge_rate = 0.25f * STANDARD_CELL_RATE;
        foreach (datum_stock_part_capacitor capacitor in Dm.each<datum_stock_part_capacitor>(component_parts))
            charge_rate *= Dm.num(capacitor.tier);
        return dot;
    }

    // tg: code/game/machinery/cell_charger.dm:137
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (charging == null || Dm.num(charging.percent()) >= 100 || !Dm.truthy(anchored) || !Dm.truthy(is_operational))
            return null;

        float main_draw = charge_rate * Dm.num(seconds_per_tick);
        if (main_draw == 0)
            return null;

        //charge cell, account for heat loss from work done
        object charge_given = Waiting.NoSleep(charge_cell__proc(main_draw, charging, TRUE));
        if (Dm.truthy(charge_given))
            Waiting.NoSleep(use_energy((Dm.num(charge_given) + active_power_usage) * 0.01f));

        update_appearance();
        return null;
    }
}
