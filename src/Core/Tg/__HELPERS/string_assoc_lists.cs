// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/string_assoc_lists.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (W4.0 contracts: /atom/Initialize calls string_assoc_list for every atom with a hud_possible list, and
// tg's /mob sets one, mob_defines.dm:14, so every mob reaches it).

public partial class datum_controller_global_vars
{
    // tg: code/__HELPERS/string_assoc_lists.dm:1  GLOBAL_LIST_EMPTY(string_assoc_lists)
    public DmList string_assoc_lists;

    // tg: code/__HELPERS/string_assoc_lists.dm:1  (GLOBAL_MANAGED(string_assoc_lists, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalstring_assoc_lists()
    {
        string_assoc_lists = Dm.list();
        Dm.append(ref gvars_datum_init_order, "string_assoc_lists");
    }
}

// tg: code/__HELPERS/string_assoc_lists.dm:6
public partial class datum
{
    /**
     * Caches associative lists with non-numeric stringify-able index keys and stringify-able values (text/typepath -> text/path/number).
     */
    // tg: code/__HELPERS/string_assoc_lists.dm:6
    public virtual object string_assoc_list(DmList values = null)
    {
        object dot = null;
        // (object, rules 3.4: tg stores the joined text in the list var, :10)
        object string_id = Dm.list();
        foreach (object val in Dm.each(values))
            Dm.append(ref string_id, Dm.text("[]_[]", val, values[val]));
        string_id = ((DmList)string_id).Join("-");

        if (!Dm.truthy(length(GLOB.string_assoc_lists))) // because we might be accessing this super early in some cases, it might not be set up yet!
            GLOB.string_assoc_lists = Dm.list(); // so do that now.

        dot = GLOB.string_assoc_lists[string_id];

        if (Dm.truthy(dot))
            return dot;

        return GLOB.string_assoc_lists[string_id] = values;
    }
}
