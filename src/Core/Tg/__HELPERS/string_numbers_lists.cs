// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/string_numbers_lists.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P2 (D298): the whole file (the bees' venom injection range).

public partial class datum_controller_global_vars
{
    // tg: code/__HELPERS/string_numbers_lists.dm:1  GLOBAL_LIST_EMPTY(string_numbers_lists)
    public DmList string_numbers_lists;

    // tg: code/__HELPERS/string_numbers_lists.dm:1  (GLOBAL_MANAGED(string_numbers_lists, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalstring_numbers_lists()
    {
        string_numbers_lists = Dm.list();
        Dm.append(ref gvars_datum_init_order, "string_numbers_lists");
    }
}

// tg: code/__HELPERS/string_numbers_lists.dm:6
public partial class datum
{
    /**
     * Caches lists of numeric values.
     */
    // tg: code/__HELPERS/string_numbers_lists.dm:6
    public virtual object string_numbers_list(DmList values = null)
    {
        //Just to to be extra-safe. If you try to shove in text or paths, you deserve the runtime errors.
        object sum = 0;
        foreach (object number in Dm.each(values))
            sum = Dm.add(sum, number);

        string string_id = values.Join("-");

        object dot = GLOB.string_numbers_lists[string_id];

        if (Dm.truthy(dot))
            return dot;

        return GLOB.string_numbers_lists[string_id] = values;
    }
}
