// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/utility/typecheck.dm
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
 * # Typecheck Component
 *
 * Checks the type of a value
 */
// tg: code/modules/wiremod/components/utility/typecheck.dm:9
public partial class obj_item_circuit_component_compare_typecheck : obj_item_circuit_component_compare
{
    // tg: code/modules/wiremod/components/utility/typecheck.dm:1-2  (#undef at :50-51)
    private const string COMP_TYPECHECK_MOB = "organism";
    private const string COMP_TYPECHECK_HUMAN = "humanoid";

    // tg: code/modules/wiremod/components/utility/typecheck.dm:20  var/static/component_options
    private static ref DmList populate_options__component_options => ref TgState.Current.obj_item_circuit_component_compare_typecheck__populate_options__component_options;

    // tg: code/modules/wiremod/components/utility/typecheck.dm:19
    public override void populate_options()
    {
        typecheck_options = add_option_port("Typecheck Options", populate_options__component_options) as datum_port_input_option;
    }

    // tg: code/modules/wiremod/components/utility/typecheck.dm:30
    public override async DmTask populate_custom_ports()
    {
        thing_to_check = add_input_port("Value", PORT_TYPE_ANY) as datum_port_input;
    }

    // tg: code/modules/wiremod/components/utility/typecheck.dm:33
    public override object do_comparisons()
    {
        object input_val = thing_to_check.value;
        switch (typecheck_options.value)
        {
            case PORT_TYPE_STRING:
                return Dm.num(istext(input_val));
            case PORT_TYPE_NUMBER:
                return Dm.num(isnum(input_val));
            case PORT_COMPOSITE_TYPE_LIST:
                return Dm.num(islist(input_val));
            case PORT_TYPE_ATOM:
                return Dm.num(isatom(input_val));
            case COMP_TYPECHECK_MOB:
                return Dm.num(ismob(input_val));
            case COMP_TYPECHECK_HUMAN:
                return Dm.num(ishuman(input_val));
        }
        return FALSE;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/wiremod/components/utility/typecheck.dm:20
    internal DmList obj_item_circuit_component_compare_typecheck__populate_options__component_options = Dm.list(
        PORT_TYPE_STRING,
        PORT_TYPE_NUMBER,
        PORT_COMPOSITE_TYPE_LIST,
        PORT_TYPE_ATOM,
        "organism",
        "humanoid");
}
