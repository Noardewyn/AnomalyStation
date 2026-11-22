// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/datatypes/signal.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps CI1 (D339, D341): the whole file. The var block is generated (D111).

// tg: code/modules/wiremod/datatypes/signal.dm:1
public partial class datum_circuit_datatype_signal : datum_circuit_datatype
{
    // tg: code/modules/wiremod/datatypes/signal.dm:13
    public override object handle_manual_input(datum_port_input port = null, mob user = null, object user_input = null)
    {
        atom parent = port.connected_component;
        if (parent != null)
            parent.balloon_alert(user, I18n.T("triggered []", port.name));
        return COMPONENT_SIGNAL;
    }
}

// tg: code/modules/wiremod/datatypes/signal.dm:19
public partial class datum_circuit_datatype_signal_instant_signal : datum_circuit_datatype_signal
{
    // tg: code/modules/wiremod/datatypes/signal.dm:22
    public override object is_compatible(datum_port port = null)
    {
        return Dm.num(istype(port, typeof(datum_port_output)));
    }
}

// tg: code/modules/wiremod/datatypes/signal.dm:25
public partial class datum_circuit_datatype_signal_response_signal : datum_circuit_datatype_signal
{
    // tg: code/modules/wiremod/datatypes/signal.dm:28
    public override object is_compatible(datum_port port = null)
    {
        return Dm.num(istype(port, typeof(datum_port_input)));
    }
}
