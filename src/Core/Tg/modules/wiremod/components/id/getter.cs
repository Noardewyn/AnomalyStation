// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/id/getter.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var block is generated.

// tg: code/modules/wiremod/components/id/getter.dm:1
public partial class obj_item_circuit_component_id_getter : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/id/getter.dm:20
    public override object get_ui_notices()
    {
        DmList dot = base.get_ui_notices() as DmList;
        Dm.append(dot, create_ui_notice(Dm.text("Maximum Range: [] tiles.", max_range), "orange", "info"));
        return dot;
    }

    // tg: code/modules/wiremod/components/id/getter.dm:24
    public override async DmTask populate_ports()
    {
        target = add_input_port("Target", PORT_TYPE_ATOM) as datum_port_input;
        prioritize_hands = add_input_port("Prioritize Hands", PORT_TYPE_BOOLEAN) as datum_port_input;
        id_port = add_output_port("ID", PORT_TYPE_ATOM) as datum_port_output;
    }

    // tg: code/modules/wiremod/components/id/getter.dm:29
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        mob_living target_mob = target.value as mob_living;
        turf current_turf = get_location() as turf;
        if (!(target_mob is mob_living) || get_dist(current_turf, target_mob) > max_range || current_turf.z != target_mob.z)
        {
            id_port.set_output(null);
            return null;
        }
        id_port.set_output(target_mob.get_idcard(prioritize_hands.value));
        return null;
    }
}
