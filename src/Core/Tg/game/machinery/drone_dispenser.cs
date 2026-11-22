// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/drone_dispenser.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI7 (D326): the whole file. The var blocks are generated (D111).

// tg: code/game/machinery/drone_dispenser.dm:5
public partial class obj_machinery_drone_dispenser : obj_machinery
{
    private const string DRONE_PRODUCTION = "production";
    private const string DRONE_RECHARGING = "recharging";
    private const string DRONE_READY = "ready";

    // tg: code/game/machinery/drone_dispenser.dm:71
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        materials = Dm.@new<datum_material_container>(
            this,
            Dm.list(typeof(datum_material_iron), typeof(datum_material_glass)),
            SHEET_MATERIAL_AMOUNT * MAX_STACK_SIZE * 2,
            MATCONTAINER_EXAMINE,
            Dm.named("allowed_items", typeof(obj_item_stack)));
        Waiting.NoSleep(materials.insert_amount_mat(starting_amount, typeof(datum_material_iron)));
        Waiting.NoSleep(materials.insert_amount_mat(starting_amount, typeof(datum_material_glass)));
        materials.precise_insertion = TRUE;
        using_materials = Dm.list(Dm.kv(typeof(datum_material_iron), iron_cost), Dm.kv(typeof(datum_material_glass), glass_cost));
        // REGISTER_REQUIRED_MAP_ITEM(1, 1): the unit tests' map item check (UNIT_TESTS)
        return dot;
    }

    // tg: code/game/machinery/drone_dispenser.dm:86
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref materials);
        return base.Destroy(force);
    }

    // tg: code/game/machinery/drone_dispenser.dm:171
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList ?? Dm.list();
        string material_requirement_string = "It needs ";
        if (iron_cost > 0)
        {
            material_requirement_string += Dm.text("[] iron sheets ", iron_cost / SHEET_MATERIAL_AMOUNT);
            if (glass_cost > 0)
                material_requirement_string += "and ";
        }
        if (glass_cost > 0)
            material_requirement_string += Dm.text("[] glass sheets ", glass_cost / SHEET_MATERIAL_AMOUNT);
        if (iron_cost > 0 || glass_cost > 0)
        {
            material_requirement_string += "to produce one drone shell.";
            dot.Add(span_notice(I18n.T(material_requirement_string)));
        }
        if (mode == DRONE_RECHARGING && !Dm.truthy(machine_stat) && Dm.truthy(recharging_text))
            dot.Add(span_warning(I18n.T(Dm.str(recharging_text))));
        return dot;
    }

    // tg: code/game/machinery/drone_dispenser.dm:186
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(NOPOWER, BROKEN))) || !Dm.truthy(anchored))
            return null;

        if ((glass_cost != 0 || iron_cost != 0) && !Waiting.NoSleep(materials.has_materials(using_materials)))
            return null; // We require more minerals

        // We are currently in the middle of something
        if (Dm.num(timer) > world.time)
            return null;

        switch (mode)
        {
            case DRONE_READY:
                // If we have X drone shells already on our turf
                if (Dm.truthy(maximum_idle) && count_shells() >= maximum_idle)
                    return null; // then do nothing; check again next tick
                if (Dm.truthy(begin_create_message))
                    visible_message(span_notice(I18n.T("[] []", this, begin_create_message)));
                if (Dm.truthy(work_sound))
                    playsound(this, work_sound, 50, TRUE);
                mode = DRONE_PRODUCTION;
                timer = world.time + production_time;
                update_appearance();
                break;

            case DRONE_PRODUCTION:
                Waiting.NoSleep(materials.use_materials(using_materials));
                if (Dm.truthy(energy_used))
                    Waiting.NoSleep(use_energy(energy_used));

                foreach (Type spawnable_item in Dm.each_cast<Type>(dispense_type))
                {
                    var spawned_atom = (atom)Dm.@new(spawnable_item, loc);
                    spawned_atom.flags_1 = Dm.num(Dm.bitor(spawned_atom.flags_1, Dm.bitand(flags_1, ADMIN_SPAWNED_1)));
                }

                if (Dm.truthy(create_sound))
                    playsound(this, create_sound, 50, TRUE);
                if (Dm.truthy(end_create_message))
                    visible_message(span_notice(I18n.T("[] []", this, end_create_message)));

                mode = DRONE_RECHARGING;
                timer = world.time + cooldownTime;
                update_appearance();
                break;

            case DRONE_RECHARGING:
                if (Dm.truthy(recharge_sound))
                    playsound(this, recharge_sound, 50, TRUE);
                if (Dm.truthy(recharge_message))
                    visible_message(span_notice(I18n.T("[] []", this, recharge_message)));

                mode = DRONE_READY;
                update_appearance();
                break;
        }
        return null;
    }

    // tg: code/game/machinery/drone_dispenser.dm:237
    public virtual int count_shells()
    {
        int dot = 0;
        foreach (object actual_shell in Dm.each(loc.contents))
        {
            foreach (Type potential_item in Dm.each_cast<Type>(dispense_type))
            {
                if (istype(actual_shell, potential_item))
                    dot++;
            }
        }
        return dot;
    }

    // tg: code/game/machinery/drone_dispenser.dm:244
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(BROKEN, NOPOWER))))
            icon_state = Dm.str(icon_off);
        else if (mode == DRONE_RECHARGING)
            icon_state = Dm.str(icon_recharging);
        else if (mode == DRONE_PRODUCTION)
            icon_state = Dm.str(icon_creating);
        else
            icon_state = Dm.str(icon_on);
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/machinery/drone_dispenser.dm:257
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        await materials.retrieve_all();
        tool.play_tool_sound(this);
        to_chat(user, span_notice(I18n.T("You retrieve the materials from [].", this)));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/drone_dispenser.dm:263
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
        {
            to_chat(user, span_warning(I18n.T("[] doesn't need repairs.", this)));
            return ITEM_INTERACT_BLOCKING;
        }

        if (!Dm.truthy(tool.tool_start_check(user, amount: 1)))
            return ITEM_INTERACT_BLOCKING;

        user.visible_message(
            span_notice(I18n.T("[] begins patching up [] with [].", user, this, tool)),
            span_notice(I18n.T("You begin restoring the damage to []...", this)));

        if (!Dm.truthy(await tool.use_tool(this, user, 40, volume: 50)))
            return ITEM_INTERACT_BLOCKING;

        user.visible_message(
            span_notice(I18n.T("[] fixes []!", user, this)),
            span_notice(I18n.T("You restore [] to operation.", this)));

        await set_machine_stat(Dm.bitand(machine_stat, ~(int)BROKEN));
        atom_integrity = max_integrity;
        update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/drone_dispenser.dm:287
    public override async DmTask<object> atom_break(object damage_flag = null)
    {
        object dot = await base.atom_break(damage_flag);
        if (!Dm.truthy(dot))
            return dot;
        if (Dm.truthy(break_message))
            audible_message(span_warning(I18n.T("[] []", this, break_message)));
        if (Dm.truthy(break_sound))
            playsound(this, break_sound, 50, TRUE);
        return dot;
    }
}
