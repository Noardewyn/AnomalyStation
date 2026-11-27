// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/action/equpiment_action.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var blocks are generated.

// tg: code/modules/wiremod/components/action/equpiment_action.dm:1
public partial class obj_item_circuit_component_equipment_action : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/action/equpiment_action.dm:20
    [DmParams("mapload", "default_icon")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object default_icon = Dm.arg(__extra, 0);
        object dot = base.Initialize(mapload, __extra);

        if (default_icon != null)
            icon_options.set_input(default_icon);

        button_name = add_input_port("Name", PORT_TYPE_STRING) as datum_port_input;

        user = add_output_port("User", PORT_TYPE_USER) as datum_port_output;
        signal = add_output_port("Signal", PORT_TYPE_SIGNAL) as datum_port_output;
        return dot;
    }

    // tg: code/modules/wiremod/components/action/equpiment_action.dm:32
    public override object Destroy(float? force = null)
    {
        QDEL_LIST_ASSOC_VAL(granted_to);
        return base.Destroy(force);
    }

    // tg: code/modules/wiremod/components/action/equpiment_action.dm:36  var/static/action_options
    private static ref DmList populate_options__action_options => ref TgState.Current.obj_item_circuit_component_equipment_action__populate_options__action_options;

    // tg: code/modules/wiremod/components/action/equpiment_action.dm:36
    public override void populate_options()
    {
        icon_options = add_option_port("Icon", populate_options__action_options) as datum_port_input_option;
    }

    // tg: code/modules/wiremod/components/action/equpiment_action.dm:76
    public override async DmTask register_shell(atom_movable shell = null)
    {
        await base.register_shell(shell);
        shell.SEND_SIGNAL(COMSIG_CIRCUIT_ACTION_COMPONENT_REGISTERED, Dm.list(shell, this));
    }

    // tg: code/modules/wiremod/components/action/equpiment_action.dm:80
    public override async DmTask unregister_shell(atom_movable shell = null)
    {
        await base.unregister_shell(shell);
        shell.SEND_SIGNAL(COMSIG_CIRCUIT_ACTION_COMPONENT_UNREGISTERED, Dm.list(shell, this));
    }

    // tg: code/modules/wiremod/components/action/equpiment_action.dm:84
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        if (Dm.truthy(length(granted_to)))
            update_actions();
        return null;
    }

    // tg: code/modules/wiremod/components/action/equpiment_action.dm:88
    public virtual void update_actions()
    {
        foreach (object target in Dm.each(granted_to))
        {
            datum_action granted_action = granted_to[target] as datum_action;
            granted_action.name = (string)(button_name.value is var or__1 && Dm.truthy(or__1) ? or__1 : "Action");
            granted_action.button_icon_state = Dm.text("bci_[]", replacetextEx(LOWER_TEXT(icon_options.value), " ", "_"));
        }
    }
}

// tg: code/modules/wiremod/components/action/equpiment_action.dm:94
public partial class datum_action_innate_circuit_equipment_action : datum_action_innate
{
    // tg: code/modules/wiremod/components/action/equpiment_action.dm:102
    [DmParams("target", "action_comp")]
    public override async DmTask New(params object[] __extra)
    {
        datum target = Dm.arg<datum>(__extra, 0);
        obj_item_circuit_component_equipment_action action_comp = Dm.arg<obj_item_circuit_component_equipment_action>(__extra, 1);
        await base.New(__extra);
        action_comp.granted_to[REF(target)] = this;
        this.action_comp = action_comp;
    }

    // tg: code/modules/wiremod/components/action/equpiment_action.dm:107
    public override object Destroy(float? force = null)
    {
        Dm.remove(action_comp.granted_to, REF(target));
        action_comp = null;

        return base.Destroy(force);
    }

    // tg: code/modules/wiremod/components/action/equpiment_action.dm:113
    public override async DmTask<object> Activate()
    {
        action_comp.user.set_output(owner);
        action_comp.signal.set_output(COMPONENT_SIGNAL);
        return null;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/wiremod/components/action/equpiment_action.dm:37
    internal DmList obj_item_circuit_component_equipment_action__populate_options__action_options = Dm.list(
        "Blank",

        "One",
        "Two",
        "Three",
        "Four",
        "Five",

        "Blood",
        "Bomb",
        "Brain",
        "Brain Damage",
        "Cross",
        "Electricity",
        "Exclamation",
        "Heart",
        "Id",
        "Info",
        "Injection",
        "Magnetism",
        "Minus",
        "Network",
        "Plus",
        "Power",
        "Question",
        "Radioactive",
        "Reaction",
        "Repair",
        "Say",
        "Scan",
        "Shield",
        "Skull",
        "Sleep",
        "Wireless");
}
