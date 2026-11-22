// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/datatypes/boolean.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps CI1 (D339, D341): the whole file. The var block is generated (D111).

// tg: code/modules/wiremod/datatypes/boolean.dm:1
public partial class datum_circuit_datatype_boolean : datum_circuit_datatype
{
    // tg: code/modules/wiremod/datatypes/boolean.dm:6
    public override object can_receive_from_datatype(object datatype_to_check = null)
    {
        return TRUE;
    }

    // tg: code/modules/wiremod/datatypes/boolean.dm:9
    public override object convert_value(datum_port port = null, object value_to_convert = null, float? force = null)
    {
        return Dm.num(Dm.truthy(value_to_convert));
    }
}
