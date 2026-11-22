// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/datatypes/composite/list.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps CI1 (D339, D341): the whole file. The var block is generated (D111).

// tg: code/modules/wiremod/datatypes/composite/list.dm:1
public partial class datum_circuit_composite_template_list : datum_circuit_composite_template
{
    // tg: code/modules/wiremod/datatypes/composite/list.dm:6
    public override object generate_name(DmList composite_datatypes = null)
    {
        return Dm.text("[] []", composite_datatypes[1], datatype);
    }
}

// tg: code/modules/wiremod/datatypes/composite/list.dm:9
public partial class datum_circuit_datatype_composite_instance_list : datum_circuit_datatype_composite_instance
{
    // tg: code/modules/wiremod/datatypes/composite/list.dm:13
    public override object convert_value_extensive(datum_port port = null, object value_to_convert = null, float? force = null)
    {
        datum_circuit_datatype datatype_handler = GLOB.circuit_datatypes[composite_datatypes[1]] as datum_circuit_datatype;

        DmList converted_list = Dm.list();
        foreach (object data in Dm.each(value_to_convert))
            Dm.append(converted_list, Dm.list(datatype_handler.convert_value(port, data)));
        return converted_list;
    }
}
