// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/atom/reagentscanner.dm
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
 * # Reagents Scanner
 *
 * Returns the reagentss of an atom
 */
// tg: code/modules/wiremod/components/atom/reagentscanner.dm:6
public partial class obj_item_circuit_component_reagentscanner : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/atom/reagentscanner.dm:20
    public override object get_ui_notices()
    {
        DmList dot = base.get_ui_notices() as DmList;
        Dm.append(dot, create_ui_notice(Dm.text("Maximum Range: [] tiles", max_range), "orange", "info"));
        Dm.append(dot, create_table_notices(Dm.list(
            "reagent",
            "volume"
            )));
        return dot;
    }

    // tg: code/modules/wiremod/components/atom/reagentscanner.dm:28
    public override async DmTask populate_ports()
    {
        input_port = add_input_port("Entity", PORT_TYPE_ATOM) as datum_port_input;
        result = add_output_port("Reagents", PORT_TYPE_TABLE) as datum_port_output;
    }

    // tg: code/modules/wiremod/components/atom/reagentscanner.dm:32
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        atom entity = input_port.value as atom;
        turf location = get_location() as turf;
        if (!(entity is atom) || !IN_GIVEN_RANGE(location, entity, Dm.num(max_range)))
        {
            result.set_output(null);
            return null;
        }
        DmList new_table = Dm.list();
        foreach (object reagent__it in Dm.each((entity.reagents as datum_reagents)?.reagent_list))
        {
            datum_reagent reagent = (datum_reagent)reagent__it;
            DmList entry = Dm.list();
            entry["reagent"] = reagent.name;
            entry["volume"] = reagent.volume;
            Dm.append(new_table, Dm.list(entry));
        }
        result.set_output(new_table);
        return null;
    }
}
