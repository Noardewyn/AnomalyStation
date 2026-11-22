// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/string/replace.dm
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
 * # String Replace Component
 *
 * Replace any instance of a word/letter in a string with another
 */
// tg: code/modules/wiremod/components/string/replace.dm:6
public partial class obj_item_circuit_component_compare_contains_replace : obj_item_circuit_component_compare_contains
{
    // tg: code/modules/wiremod/components/string/replace.dm:14
    public override async DmTask populate_custom_ports()
    {
        await base.populate_custom_ports();
        replacement = add_input_port("Replacement", PORT_TYPE_STRING) as datum_port_input;
    }

    // tg: code/modules/wiremod/components/string/replace.dm:18
    public override object do_comparisons()
    {
        object to_find = needle.value;
        object to_search = haystack.value;

        if (!Dm.truthy(to_find) || !Dm.truthy(to_search))
            return null;

        object to_replace = replacement.value;

        return Dm.replacetext(to_search, to_find, to_replace);
    }
}
