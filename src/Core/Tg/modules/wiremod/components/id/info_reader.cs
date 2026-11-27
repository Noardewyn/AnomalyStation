// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/id/info_reader.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var block is generated.

// tg: code/modules/wiremod/components/id/info_reader.dm:1
public partial class obj_item_circuit_component_id_info_reader : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/id/info_reader.dm:23
    public override object get_ui_notices()
    {
        DmList dot = base.get_ui_notices() as DmList;
        Dm.append(dot, create_ui_notice(Dm.text("Maximum Range: [] tiles.", max_range), "orange", "info"));
        return dot;
    }

    // tg: code/modules/wiremod/components/id/info_reader.dm:27
    public override async DmTask populate_ports()
    {
        target = add_input_port("Target", PORT_TYPE_ATOM) as datum_port_input;
        name_port = add_output_port("Name", PORT_TYPE_STRING) as datum_port_output;
        rank_port = add_output_port("Rank", PORT_TYPE_STRING) as datum_port_output;
        age_port = add_output_port("Age", PORT_TYPE_NUMBER) as datum_port_output;
    }

    // tg: code/modules/wiremod/components/id/info_reader.dm:34
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        obj_item_card_id target_item = target.value as obj_item_card_id;
        turf current_turf = get_location() as turf;
        turf target_turf = get_turf(target_item);
        if (!(target_item is obj_item_card_id) || get_dist(current_turf, target_turf) > max_range || current_turf.z != target_turf.z)
        {
            name_port.set_output(null);
            rank_port.set_output(null);
            age_port.set_output(null);
            return null;
        }
        name_port.set_output(target_item.registered_name);
        rank_port.set_output(target_item.assignment);
        age_port.set_output(target_item.registered_age);
        return null;
    }
}
