// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/admin/input_request.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var block is generated.

/**
 * # Input Request Component
 *
 * Requests an input from someone.
 */
// tg: code/modules/wiremod/components/admin/input_request.dm:10
public partial class obj_item_circuit_component_input_request : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/admin/input_request.dm:1
    private const string COMP_INPUT_STRING = "string";
    // tg: code/modules/wiremod/components/admin/input_request.dm:2
    private const string COMP_INPUT_NUMBER = "number";
    // tg: code/modules/wiremod/components/admin/input_request.dm:3
    private const string COMP_INPUT_LIST = "list";

    // tg: code/modules/wiremod/components/admin/input_request.dm:35  var/static/list/component_options
    private static ref DmList populate_options__component_options => ref TgState.Current.obj_item_circuit_component_input_request__populate_options__component_options;

    // tg: code/modules/wiremod/components/admin/input_request.dm:34
    public override void populate_options()
    {
        input_options = add_option_port("Option", populate_options__component_options) as datum_port_input_option;
    }

    // tg: code/modules/wiremod/components/admin/input_request.dm:43
    public override async DmTask populate_ports()
    {
        entity = add_input_port("Entity", PORT_TYPE_ATOM) as datum_port_input;
        input_response = add_output_port("Response", PORT_TYPE_ANY) as datum_port_output;
        input_triggered = add_output_port("Input Sent", PORT_TYPE_SIGNAL) as datum_port_output;
        input_failed = add_output_port("Input Failed", PORT_TYPE_SIGNAL) as datum_port_output;

        update_options();
    }

    // tg: code/modules/wiremod/components/admin/input_request.dm:51
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        mob player = entity.value as mob;
        if (!(player is mob))
            return null;

        Waiting.Detach(request_input_from_player(player));
        return null;
    }

    // tg: code/modules/wiremod/components/admin/input_request.dm:58
    public virtual async DmTask request_input_from_player(mob player = null)
    {
        object new_option = input_options.value;
        object sw__1 = new_option;
        if (Dm.eq(sw__1, COMP_INPUT_STRING))
        {
            object player_input = await tgui_input_text(player, "Input a value", "Input value", max_length: MAX_MESSAGE_LEN);
            if (player_input == null)
                return;
            input_response.set_output(player_input);
        }
        else if (Dm.eq(sw__1, COMP_INPUT_NUMBER))
        {
            object player_input = await tgui_input_number(player, "Input a value", "Input value");
            if (player_input == null)
                return;
            input_response.set_output(player_input);
        }
        else if (Dm.eq(sw__1, COMP_INPUT_LIST))
        {
            object data = parameter.value;
            if (!islist(data))
                return;
            object player_input = await tgui_input_list(player, "Input a value", "Input value", data as DmList);
            if (player_input == null)
                return;
            input_response.set_output(player_input);
        }
        input_triggered.set_output(COMPONENT_SIGNAL);
    }

    // tg: code/modules/wiremod/components/admin/input_request.dm:81
    public override void pre_input_received(datum_port_input port = null)
    {
        if (port == input_options)
            update_options(port);
    }

    // tg: code/modules/wiremod/components/admin/input_request.dm:85
    public virtual void update_options(datum_port_input port = null)
    {
        object new_option = input_options.value;
        if (parameter != null)
        {
            remove_input_port(parameter);
            parameter = null;
        }

        object sw__1 = new_option;
        if (Dm.eq(sw__1, COMP_INPUT_STRING))
            input_response.set_datatype(PORT_TYPE_STRING);
        else if (Dm.eq(sw__1, COMP_INPUT_NUMBER))
            input_response.set_datatype(PORT_TYPE_NUMBER);
        else if (Dm.eq(sw__1, COMP_INPUT_LIST))
        {
            parameter = add_input_port("Options List", PORT_TYPE_LIST(PORT_TYPE_ANY)) as datum_port_input;
            input_response.set_datatype(PORT_TYPE_STRING);
        }
    }
}

public sealed partial class TgState
{
    // tg: code/modules/wiremod/components/admin/input_request.dm:35
    internal DmList obj_item_circuit_component_input_request__populate_options__component_options = Dm.list(
        "string",
        "number",
        "list");
}
