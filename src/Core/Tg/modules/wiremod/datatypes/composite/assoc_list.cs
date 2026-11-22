// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/datatypes/composite/assoc_list.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps CI1 (D339, D341): the whole file. The var block is generated (D111).

// tg: code/modules/wiremod/datatypes/composite/assoc_list.dm:1
public partial class datum_circuit_composite_template_assoc_list : datum_circuit_composite_template
{
    // tg: code/modules/wiremod/datatypes/composite/assoc_list.dm:6
    public override object generate_name(DmList composite_datatypes = null)
    {
        if (Dm.eq(composite_datatypes[1], PORT_TYPE_STRING))
            return Dm.text("[] associative list", composite_datatypes[2]);
        else
            return Dm.text("[], [] assoc. list", composite_datatypes[1], composite_datatypes[2]);
    }
}

// tg: code/modules/wiremod/datatypes/composite/assoc_list.dm:12
public partial class datum_circuit_datatype_composite_instance_assoc_list : datum_circuit_datatype_composite_instance
{
    // tg: code/modules/wiremod/datatypes/composite/assoc_list.dm:16
    public override object convert_value_extensive(datum_port port = null, object value_to_convert = null, float? force = null)
    {
        datum_circuit_datatype key_handler = GLOB.circuit_datatypes[composite_datatypes[1]] as datum_circuit_datatype;
        datum_circuit_datatype value_handler = GLOB.circuit_datatypes[composite_datatypes[2]] as datum_circuit_datatype;

        DmList converted_list = Dm.list();
        DmList value_list = value_to_convert as DmList;
        foreach (object data in Dm.each(value_to_convert))
            converted_list[key_handler.convert_value(port, data)] = value_handler.convert_value(port, value_list[data]);
        return converted_list;
    }
}
