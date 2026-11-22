// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/datatypes/option.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps CI1 (D339, D341): the whole file. The var block is generated (D111).

// tg: code/modules/wiremod/datatypes/option.dm:1
public partial class datum_port_input_option : datum_port_input
{
    // tg: code/modules/wiremod/datatypes/option.dm:4
    [DmParams("to_connect", "name", "datatype", "order", "trigger", "default", "possible_options")]
    public override async DmTask New(params object[] __extra)
    {
        DmList possible_options = Dm.arg<DmList>(__extra, 6);
        await base.New(__extra);
        this.possible_options = possible_options;
        if (Dm.truthy(length(possible_options)))
            set_value(possible_options[1]);
    }
}

// tg: code/modules/wiremod/datatypes/option.dm:12
public partial class datum_circuit_datatype_option : datum_circuit_datatype
{
    // tg: code/modules/wiremod/datatypes/option.dm:18
    [DmParams("gained_port")]
    public override object is_compatible(datum_port port = null)
    {
        datum_port gained_port = port;
        return Dm.num(istype(gained_port, typeof(datum_port_input_option)));
    }

    // tg: code/modules/wiremod/datatypes/option.dm:21
    public override object convert_value(datum_port port = null, object value_to_convert = null, float? force = null)
    {
        datum_port_input_option option_port = port as datum_port_input_option;
        if (Dm.truthy(force))
            return value_to_convert;

        if (!Dm.truthy(length(option_port.possible_options)))
            return null;

        if (Dm.@in(value_to_convert, option_port.possible_options))
            return value_to_convert;
        return option_port.possible_options[1];
    }

    // tg: code/modules/wiremod/datatypes/option.dm:32
    public override object datatype_ui_data(datum_port port = null)
    {
        return (port as datum_port_input_option).possible_options;
    }
}
