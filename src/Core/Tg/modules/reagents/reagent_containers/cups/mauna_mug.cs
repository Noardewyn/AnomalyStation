// Translated from tgstation (snapshot 1d99bd7f): code/modules/reagents/reagent_containers/cups/mauna_mug.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps H3 (D333, the bar): the whole file: the mauna mug that heats its drink from its power cell, with its battery case and its glow.
// The cuffable element waits (restraints). The var block is generated (D111; its cell holds the cell object, Generator.Members.cs).

// tg: code/modules/reagents/reagent_containers/cups/mauna_mug.dm:1
public partial class obj_item_reagent_containers_cup_maunamug : obj_item_reagent_containers_cup
{
    // tg: code/modules/reagents/reagent_containers/cups/mauna_mug.dm:15
    [DmParams("mapload", "vol")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.Deferred("code/modules/reagents/reagent_containers/cups/mauna_mug.dm:17", "Initialize: AddElement(/datum/element/cuffable_item) (cuffing an item to a wrist: restraints, M10+)");
        cell = Dm.@new<obj_item_stock_parts_power_store_cell>(this);
        return dot;
    }

    // tg: code/modules/reagents/reagent_containers/cups/mauna_mug.dm:20
    public override object get_cell(atom_movable @interface = null, mob user = null)
    {
        return cell;
    }

    // tg: code/modules/reagents/reagent_containers/cups/mauna_mug.dm:23
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        Dm.append(ref dot, span_notice(I18n.T("The status display reads: Current temperature: <b>[]K</b> Current Charge:[].",
            ((datum_reagents)reagents).chem_temp, cell != null ? Dm.text("[]%", cell.charge / cell.maxcharge * 100) : I18n.T("No cell found"))));
        if (Dm.truthy(open))
            Dm.append(ref dot, span_notice(I18n.T("The battery case is open.")));
        if (cell != null && cell.charge > 0)
            Dm.append(ref dot, span_notice(I18n.T("<b>Ctrl+Click</b> to toggle the power.")));
        return dot;
    }

    // tg: code/modules/reagents/reagent_containers/cups/mauna_mug.dm:31
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        base.process(seconds_per_tick, __extra);
        float seconds = Dm.num(seconds_per_tick);
        if (Dm.truthy(on) && (cell == null || cell.charge <= 0)) //Check if we ran out of power
        {
            change_power_status(FALSE);
            return FALSE;
        }
        Waiting.NoSleep(cell.use(0.005f * STANDARD_CELL_RATE * seconds)); //Basic cell goes for like 200 seconds, bluespace for 8000
        var holder = (datum_reagents)reagents;
        if (!Dm.truthy(holder.total_volume))
            return FALSE;
        float max_temp = min(500 + (500 * (0.2f * Dm.num(cell.rating))), 1000); // 373 to 1000
        holder.adjust_thermal_energy(0.4f * cell.maxcharge * holder.total_volume * seconds, max_temp: max_temp); // 4 kelvin every tick on a basic cell. 160k on bluespace
        Waiting.NoSleep(holder.handle_reactions());
        update_appearance();
        if (holder.chem_temp >= max_temp)
        {
            change_power_status(FALSE);
            audible_message(span_notice(I18n.T("The Mauna Mug lets out a happy beep and turns off!")));
            playsound(this, Dm.resource("sound/machines/chime.ogg"), 50);
        }
        return null;
    }

    // tg: code/modules/reagents/reagent_containers/cups/mauna_mug.dm:48
    public override object Destroy(float? force = null)
    {
        if (cell != null)
            QDEL_NULL(ref cell);
        STOP_PROCESSING(SSobj, this);
        return base.Destroy(force);
    }

    // tg: code/modules/reagents/reagent_containers/cups/mauna_mug.dm:54
    public override async DmTask<object> item_ctrl_click(mob user = null)
    {
        await DmTask.CompletedTask;
        if (Dm.truthy(on))
            change_power_status(FALSE);
        else
        {
            if (cell == null || cell.charge <= 0)
                return FALSE; //No power, so don't turn on
            change_power_status(TRUE);
        }
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/modules/reagents/reagent_containers/cups/mauna_mug.dm:63
    public virtual void change_power_status(object status = null)
    {
        on = Dm.num(status);
        if (Dm.truthy(on))
            START_PROCESSING(SSobj, this);
        else
            STOP_PROCESSING(SSobj, this);
        update_appearance();
    }

    // tg: code/modules/reagents/reagent_containers/cups/mauna_mug.dm:71
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        object dot = await base.screwdriver_act(user, tool);
        open = Dm.truthy(open) ? FALSE : TRUE;
        to_chat(user, span_notice(Dm.truthy(open) ? I18n.T("You screw the battery case on [] open .", this) : I18n.T("You screw the battery case on [] closed .", this)));
        update_appearance();
        return dot;
    }

    // tg: code/modules/reagents/reagent_containers/cups/mauna_mug.dm:77
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is not obj_item_stock_parts_power_store_cell)
            return await base.item_interaction(user, tool, modifiers, __extra);
        if (!Dm.truthy(open))
        {
            to_chat(user, span_warning(I18n.T("The battery case must be open to insert a power cell!")));
            return ITEM_INTERACT_BLOCKING;
        }
        if (cell != null)
        {
            to_chat(user, span_warning(I18n.T("There is already a power cell inside!")));
            return ITEM_INTERACT_BLOCKING;
        }
        else if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
            return ITEM_INTERACT_BLOCKING;
        cell = (obj_item_stock_parts_power_store)tool;
        user.visible_message(span_notice(I18n.T("[] inserts a power cell into [].", user, this)), span_notice(I18n.T("You insert the power cell into [].", this)));
        update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/reagents/reagent_containers/cups/mauna_mug.dm:93
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        if (cell != null && Dm.truthy(open))
        {
            await user.put_in_hands(cell);
            cell = null;
            to_chat(user, span_notice(I18n.T("You remove the power cell from [].", this)));
            on = FALSE;
            update_appearance();
            return TRUE;
        }
        return await base.attack_hand(user, modifiers, __extra);
    }

    // tg: code/modules/reagents/reagent_containers/cups/mauna_mug.dm:103
    public override DmTask<object> update_icon_state(params object[] __extra)
    {
        if (Dm.truthy(open))
        {
            icon_state = Dm.text("[][]_bat", base_icon_state, cell != null ? null : "_no");
            return base.update_icon_state(__extra);
        }
        icon_state = Dm.text("[][]", base_icon_state, Dm.truthy(on) ? "_on" : null);
        return base.update_icon_state(__extra);
    }

    // tg: code/modules/reagents/reagent_containers/cups/mauna_mug.dm:110
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        var holder = (datum_reagents)reagents;
        if (!Dm.truthy(holder.total_volume) || holder.chem_temp < 400)
            return dot;

        float intensity = (holder.chem_temp - 400) * 1 / 600f; //Get the opacity of the incandescent overlay. Ranging from 400 to 1000
        var mug_glow = (mutable_appearance)mutable_appearance(icon, "maunamug_incand");
        mug_glow.alpha = 255 * intensity;
        Dm.append(ref dot, mug_glow);
        return dot;
    }
}
