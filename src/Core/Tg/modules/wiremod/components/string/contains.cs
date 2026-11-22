// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/string/contains.dm
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
 * # String Contains Component
 *
 * Checks if a string contains a word/letter
 */
// tg: code/modules/wiremod/components/string/contains.dm:6
public partial class obj_item_circuit_component_compare_contains : obj_item_circuit_component_compare
{
    // tg: code/modules/wiremod/components/string/contains.dm:14
    public override async DmTask populate_custom_ports()
    {
        needle = add_input_port("Needle", PORT_TYPE_STRING) as datum_port_input;
        haystack = add_input_port("Haystack", PORT_TYPE_STRING) as datum_port_input;
    }

    // tg: code/modules/wiremod/components/string/contains.dm:18
    public override object Destroy(float? force = null)
    {
        needle = null;
        haystack = null;
        return base.Destroy(force);
    }

    // tg: code/modules/wiremod/components/string/contains.dm:23
    public override object do_comparisons()
    {
        object to_find = needle.value;
        object to_search = haystack.value;

        if (!Dm.truthy(to_find) || !Dm.truthy(to_search))
            return null;

        return Dm.findtext(to_search, to_find);
    }
}
