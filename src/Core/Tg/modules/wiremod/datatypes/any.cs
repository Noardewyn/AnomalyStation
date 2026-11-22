// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/datatypes/any.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps CI1 (D339, D341): the whole file. The var block is generated (D111).

// tg: code/modules/wiremod/datatypes/any.dm:1
public partial class datum_circuit_datatype_any : datum_circuit_datatype
{
    // tg: code/modules/wiremod/datatypes/any.dm:6
    public override object can_receive_from_datatype(object datatype_to_check = null)
    {
        return TRUE;
    }

    // tg: code/modules/wiremod/datatypes/any.dm:9
    public override object handle_manual_input(datum_port_input port = null, mob user = null, object user_input = null)
    {
        object result = text2num(user_input);
        if (result == null)
            return user_input;
        return result;
    }
}
