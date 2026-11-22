// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/utility/timepiece.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps CI1 (D339, D341): the whole file. The var block is generated (D111).

/**
 * # Timepiece Component
 *
 * returns the current station time.
 */
// tg: code/modules/wiremod/components/utility/timepiece.dm:12
public partial class obj_item_circuit_component_timepiece : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/utility/timepiece.dm:1-5  (#undef at :65-69)
    private const string COMP_TIMEPIECE_TWENTYFOUR_HOUR = "24-Hour";
    private const string COMP_TIMEPIECE_TWELVE_HOUR = "12-Hour";
    private const string COMP_TIMEPIECE_SECONDS = "Seconds";
    private const string COMP_TIMEPIECE_MINUTES = "Minutes";
    private const string COMP_TIMEPIECE_HOURS = "Hours";

    // tg: code/modules/wiremod/components/utility/timepiece.dm:32  var/static/format_options
    private static ref DmList populate_options__format_options => ref TgState.Current.obj_item_circuit_component_timepiece__populate_options__format_options;
    // tg: code/modules/wiremod/components/utility/timepiece.dm:37  var/static/unit_options
    private static ref DmList populate_options__unit_options => ref TgState.Current.obj_item_circuit_component_timepiece__populate_options__unit_options;

    // tg: code/modules/wiremod/components/utility/timepiece.dm:27
    public override async DmTask populate_ports()
    {
        text_output = add_output_port("Time Format", PORT_TYPE_STRING) as datum_port_output;
        num_output = add_output_port("Unit of Time", PORT_TYPE_NUMBER) as datum_port_output;
    }

    // tg: code/modules/wiremod/components/utility/timepiece.dm:31
    public override void populate_options()
    {
        format = add_option_port("Time Format", populate_options__format_options) as datum_port_input_option;
        time_unit = add_option_port("Unit of Time", populate_options__unit_options) as datum_port_input_option;
    }

    // tg: code/modules/wiremod/components/utility/timepiece.dm:44
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        object time = null;

        switch (format.value)
        {
            case COMP_TIMEPIECE_TWENTYFOUR_HOUR:
                time = round_timestamp();
                break;
            case COMP_TIMEPIECE_TWELVE_HOUR:
                time = time_to_twelve_hour();
                break;
        }

        text_output.set_output(time);

        switch (time_unit.value)
        {
            case COMP_TIMEPIECE_HOURS:
                time = round_timestamp(format: "hh");
                break;
            case COMP_TIMEPIECE_MINUTES:
                time = round_timestamp(format: "mm");
                break;
            case COMP_TIMEPIECE_SECONDS:
                time = round_timestamp(format: "ss");
                break;
        }

        num_output.set_output(time);
        return null;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/wiremod/components/utility/timepiece.dm:32
    internal DmList obj_item_circuit_component_timepiece__populate_options__format_options = Dm.list(
        "24-Hour", // Station time is expressed in 24-h in the status tab. So this is the default.
        "12-Hour");
    // tg: code/modules/wiremod/components/utility/timepiece.dm:37
    internal DmList obj_item_circuit_component_timepiece__populate_options__unit_options = Dm.list(
        "Hours",
        "Minutes",
        "Seconds");
}
