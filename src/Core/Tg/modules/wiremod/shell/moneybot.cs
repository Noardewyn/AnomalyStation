// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/shell/moneybot.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var blocks are generated.

/**
 * # Money Bot
 *
 * Immobile (but not dense) shell that can receive and dispense money.
 */
// tg: code/modules/wiremod/shell/moneybot.dm:6
public partial class obj_structure_money_bot : obj_structure
{
    // tg: code/modules/wiremod/shell/moneybot.dm:18
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        disassembled ??= TRUE;
        if (Dm.truthy(stored_money))
            Dm.@new<obj_item_holochip>(drop_location(), stored_money);
    }

    // tg: code/modules/wiremod/shell/moneybot.dm:22
    public virtual void add_money(object to_add = null)
    {
        stored_money += Dm.num(to_add);
        SEND_SIGNAL(COMSIG_MONEYBOT_ADD_MONEY, Dm.list(this, to_add));
    }

    // tg: code/modules/wiremod/shell/moneybot.dm:26
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_shell), Dm.list(
            Dm.@new<obj_item_circuit_component_money_bot>(),
            Dm.@new<obj_item_circuit_component_money_dispenser>()
        ), SHELL_CAPACITY_LARGE));
        return dot;
    }

    // tg: code/modules/wiremod/shell/moneybot.dm:33
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(locked))
            return null;
        await set_anchored(Dm.not(anchored));
        tool.play_tool_sound(this);
        balloon_alert(user, Dm.truthy(anchored) ? I18n.T("secured") : I18n.T("unsecured"));
        return TRUE;
    }
}

// tg: code/modules/wiremod/shell/moneybot.dm:42
public partial class obj_item_circuit_component_money_dispenser : obj_item_circuit_component
{
    // tg: code/modules/wiremod/shell/moneybot.dm:64
    public override async DmTask populate_ports()
    {
        dispense_amount = add_input_port("Amount", PORT_TYPE_NUMBER) as datum_port_input;
        on_fail = add_output_port("On Failed", PORT_TYPE_SIGNAL) as datum_port_output;
    }

    // tg: code/modules/wiremod/shell/moneybot.dm:68
    public override object get_ui_notices()
    {
        DmList dot = base.get_ui_notices() as DmList;
        Dm.append(dot, create_ui_notice(Dm.text("Dispense Cooldown: []", DisplayTimeText(dispense_cd_length)), "orange", FA_ICON_STOPWATCH));
        Dm.append(dot, create_ui_notice(Dm.text("Dispense Limit: [] Holochips (per tile)", max_chips), "orange", FA_ICON_MONEY_BILL_TRANSFER));
        return dot;
    }

    // tg: code/modules/wiremod/shell/moneybot.dm:73
    public override async DmTask register_shell(atom_movable shell = null)
    {
        await base.register_shell(shell);
        if (istype(shell, typeof(obj_structure_money_bot)))
            attached_bot = shell as obj_structure_money_bot;
    }

    // tg: code/modules/wiremod/shell/moneybot.dm:78
    public override async DmTask unregister_shell(atom_movable shell = null)
    {
        attached_bot = null;
        await base.unregister_shell(shell);
    }

    // tg: code/modules/wiremod/shell/moneybot.dm:82
    public override void pre_input_received(datum_port_input port = null)
    {
        dispense_amount.set_value(floor(Dm.num(dispense_amount.value)));
    }

    // tg: code/modules/wiremod/shell/moneybot.dm:85
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        if (attached_bot == null)
            return null;

        if (!(dispense_cd <= world.time)) // COOLDOWN_FINISHED
        {
            on_fail.set_output(COMPONENT_SIGNAL);
            return null;
        }

        float to_dispense = clamp(Dm.num(dispense_amount.value), 0, attached_bot.stored_money);
        if (!Dm.truthy(to_dispense))
        {
            on_fail.set_output(COMPONENT_SIGNAL);
            return null;
        }

        dispense_cd = world.time + dispense_cd_length; // COOLDOWN_START
        atom droploc = attached_bot.drop_location() as atom;
        float num_on_tile = 0;
        foreach (obj_item_holochip chip in Dm.each<obj_item_holochip>(droploc))
            num_on_tile++;
        // at this point, clearly no one's jumping for the cash. so let's stop dispensing
        if (num_on_tile > max_chips)
        {
            on_fail.set_output(COMPONENT_SIGNAL);
            return null;
        }

        attached_bot.add_money(-to_dispense);
        Dm.@new<obj_item_holochip>(droploc, to_dispense);
        return null;
    }
}

// tg: code/modules/wiremod/shell/moneybot.dm:112
public partial class obj_item_circuit_component_money_bot : obj_item_circuit_component
{
    // tg: code/modules/wiremod/shell/moneybot.dm:126
    public override async DmTask populate_ports()
    {
        total_money = add_output_port("Total Money", PORT_TYPE_NUMBER) as datum_port_output;
        money_input = add_output_port("Last Input Money", PORT_TYPE_NUMBER) as datum_port_output;
        entity = add_output_port("User", PORT_TYPE_USER) as datum_port_output;
        money_trigger = add_output_port("Money Input", PORT_TYPE_SIGNAL) as datum_port_output;
    }

    // tg: code/modules/wiremod/shell/moneybot.dm:132
    public override async DmTask register_shell(atom_movable shell = null)
    {
        await base.register_shell(shell);
        if (istype(shell, typeof(obj_structure_money_bot)))
        {
            attached_bot = shell as obj_structure_money_bot;
            total_money.set_output(attached_bot.stored_money);
            RegisterSignal(shell, COMSIG_ATOM_ATTACKBY, nameof(handle_money_insert));
            RegisterSignal(shell, COMSIG_MONEYBOT_ADD_MONEY, nameof(handle_money_update));
            RegisterSignal(parent, COMSIG_CIRCUIT_SET_LOCKED, nameof(on_set_locked));
            attached_bot.locked = parent.locked;
        }
    }

    // tg: code/modules/wiremod/shell/moneybot.dm:143
    public override async DmTask unregister_shell(atom_movable shell = null)
    {
        UnregisterSignal(shell, Dm.list(
            COMSIG_ATOM_ATTACKBY,
            COMSIG_MONEYBOT_ADD_MONEY
        ));
        total_money.set_output(null);
        if (attached_bot != null)
        {
            attached_bot.locked = FALSE;
            UnregisterSignal(parent, COMSIG_CIRCUIT_SET_LOCKED);
        }
        attached_bot = null;
        await base.unregister_shell(shell);
    }

    // tg: code/modules/wiremod/shell/moneybot.dm:156
    [SignalHandler]
    public virtual void handle_money_insert(atom source = null, obj_item item = null, mob_living attacker = null)
    {
        if (attached_bot == null || !iscash(item))
            return;

        object amount_to_insert = item.get_item_credit_value();
        if (!Dm.truthy(amount_to_insert))
        {
            balloon_alert(attacker, I18n.T("this has no value!"));
            return;
        }

        attached_bot.add_money(amount_to_insert);
        balloon_alert(attacker, I18n.T("inserted [] [].", amount_to_insert, MONEY_NAME));
        money_input.set_output(amount_to_insert);
        entity.set_output(attacker);
        money_trigger.set_output(COMPONENT_SIGNAL);
        qdel(item);
    }

    // tg: code/modules/wiremod/shell/moneybot.dm:173
    [SignalHandler]
    public virtual void handle_money_update(atom source = null)
    {
        if (attached_bot != null)
            total_money.set_output(attached_bot.stored_money);
    }

    /**
     * Locks the attached bot when the circuit is locked.
     *
     * Arguments:
     * * new_value - A boolean that determines if the circuit is locked or not.
     **/
    // tg: code/modules/wiremod/shell/moneybot.dm:184
    [SignalHandler]
    public virtual void on_set_locked(datum source = null, object new_value = null)
    {
        attached_bot.locked = Dm.num(new_value);
    }
}
