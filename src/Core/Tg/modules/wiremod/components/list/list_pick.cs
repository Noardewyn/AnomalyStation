// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/list/list_pick.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps CI1 (D339, D341): the whole file. The var block is generated (D111).

/**
 * # List pick component
 *
 * Allows user to select 1 entry from a list
 */
// tg: code/modules/wiremod/components/list/list_pick.dm:6
public partial class obj_item_circuit_component_list_pick : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/list/list_pick.dm:30
    public virtual void make_list_port()
    {
        input_list = add_input_port("List", PORT_TYPE_LIST(PORT_TYPE_STRING)) as datum_port_input;
    }

    // tg: code/modules/wiremod/components/list/list_pick.dm:33
    public override async DmTask populate_ports()
    {
        input_name = add_input_port("Input Name", PORT_TYPE_STRING) as datum_port_input;
        user = add_input_port("User", PORT_TYPE_USER) as datum_port_input;
        make_list_port();

        output = add_output_port("Picked Item", PORT_TYPE_STRING) as datum_port_output;
        failure = add_output_port("On Failure", PORT_TYPE_SIGNAL) as datum_port_output;
        success = add_output_port("On Success", PORT_TYPE_SIGNAL) as datum_port_output;
    }

    // tg: code/modules/wiremod/components/list/list_pick.dm:42
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        mob mob_user = user.value as mob;
        if (!ismob(mob_user) || HAS_TRAIT_FROM(parent, TRAIT_CIRCUIT_UI_OPEN, REF(mob_user)))
        {
            failure.set_output(COMPONENT_SIGNAL);
            return null;
        }
        Waiting.Detach(show_list(mob_user, input_name.value, input_list.value as DmList));
        return null;
    }

    /// Show a list of options to the user using standed TGUI input list
    // tg: code/modules/wiremod/components/list/list_pick.dm:50
    public virtual async DmTask show_list(mob user = null, object message = null, DmList showed_list = null)
    {
        if (!Dm.truthy(showed_list) || showed_list.len == 0)
        {
            failure.set_output(COMPONENT_SIGNAL);
            return;
        }
        if (!Dm.truthy(message))
            message = "circuit input";
        if (!Dm.truthy(await user.can_perform_action(parent.shell as atom, FORBID_TELEKINESIS_REACH | ALLOW_SILICON_REACH | ALLOW_RESTING)))
        {
            failure.set_output(COMPONENT_SIGNAL);
            return;
        }
        object user_ref = REF(user);
        ADD_TRAIT(parent, TRAIT_CIRCUIT_UI_OPEN, user_ref);
        object picked = await tgui_input_list(user, message: message, items: showed_list);
        REMOVE_TRAIT(parent, TRAIT_CIRCUIT_UI_OPEN, user_ref);
        if (QDELETED(this))
            return;
        if (!Dm.truthy(await user.can_perform_action(parent.shell as atom, FORBID_TELEKINESIS_REACH | ALLOW_SILICON_REACH | ALLOW_RESTING)))
        {
            failure.set_output(COMPONENT_SIGNAL);
            return;
        }
        choose_item(picked, showed_list);
    }

    // tg: code/modules/wiremod/components/list/list_pick.dm:70
    public virtual void choose_item(object choice = null, DmList choice_list = null)
    {
        if (Dm.truthy(choice))
        {
            output.set_output(choice);
            success.set_output(COMPONENT_SIGNAL);
        }
        else
            failure.set_output(COMPONENT_SIGNAL);
    }
}
