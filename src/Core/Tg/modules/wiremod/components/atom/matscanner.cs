// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/atom/matscanner.dm
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
 * # Material Scanner
 *
 * Returns the materials of an atom
 */
// tg: code/modules/wiremod/components/atom/matscanner.dm:6
public partial class obj_item_circuit_component_matscanner : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/atom/matscanner.dm:22
    public override object get_ui_notices()
    {
        DmList dot = base.get_ui_notices() as DmList;
        Dm.append(dot, create_ui_notice(Dm.text("Maximum Range: [] tiles", max_range), "orange", "info"));
        return dot;
    }

    // tg: code/modules/wiremod/components/atom/matscanner.dm:26
    public override async DmTask populate_ports()
    {
        input_port = add_input_port("Entity", PORT_TYPE_ATOM) as datum_port_input;
        break_down_alloys = add_input_port("Break Down Alloys", PORT_TYPE_NUMBER) as datum_port_input;
        result = add_output_port("Materials", PORT_TYPE_ASSOC_LIST(PORT_TYPE_STRING, PORT_TYPE_NUMBER)) as datum_port_output;
    }

    // tg: code/modules/wiremod/components/atom/matscanner.dm:31
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        atom entity = input_port.value as atom;
        turf location = get_location() as turf;
        if (!(entity is atom) || !IN_GIVEN_RANGE(location, entity, Dm.num(max_range)))
        {
            result.set_output(null);
            return null;
        }
        // tg tests the port itself, which is always set, so the alloys are always broken down; kept as tg has it
        DmList composition = Waiting.NoSleep(entity.get_material_composition(break_down_alloys != null ? NONE : MATCONTAINER_ACCEPT_ALLOYS)) as DmList;
        DmList composition_but_with_string_keys = Dm.list();
        foreach (object material__it in Dm.each(composition))
        {
            datum_material material = (datum_material)material__it;
            composition_but_with_string_keys[material.name] = composition[material];
        }
        result.set_output(composition_but_with_string_keys);
        return null;
    }
}
