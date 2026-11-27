// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/id/access_reader.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var block is generated.

// tg: code/modules/wiremod/components/id/access_reader.dm:1
public partial class obj_item_circuit_component_id_access_reader : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/id/access_reader.dm:17
    public override object get_ui_notices()
    {
        DmList dot = base.get_ui_notices() as DmList;
        Dm.append(dot, create_ui_notice(Dm.text("Maximum Range: [] tiles.", max_range), "orange", "info"));
        return dot;
    }

    // tg: code/modules/wiremod/components/id/access_reader.dm:21
    public override async DmTask populate_ports()
    {
        target = add_input_port("Target", PORT_TYPE_ATOM) as datum_port_input;
        access_port = add_output_port("Access", PORT_TYPE_LIST(PORT_TYPE_STRING)) as datum_port_output;
    }

    // tg: code/modules/wiremod/components/id/access_reader.dm:26
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        obj_item_card_id target_item = target.value as obj_item_card_id;
        turf current_turf = get_location() as turf;
        turf target_turf = get_turf(target_item);
        if (!(target_item is obj_item_card_id) || get_dist(current_turf, target_turf) > max_range || current_turf.z != target_turf.z)
        {
            access_port.set_output(null);
            return null;
        }
        access_port.set_output(target_item.GetAccess());
        return null;
    }
}
