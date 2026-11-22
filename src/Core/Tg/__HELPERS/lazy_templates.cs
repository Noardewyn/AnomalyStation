// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/lazy_templates.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign X1a (D316): the whole file.

public partial class datum_controller_global_vars
{
    // tg: code/__HELPERS/lazy_templates.dm:1  GLOBAL_LIST_INIT(lazy_templates, generate_lazy_template_map())
    public DmList lazy_templates;

    // tg: code/__HELPERS/lazy_templates.dm:1  (GLOBAL_MANAGED(lazy_templates, generate_lazy_template_map()), __DEFINES/__globals.dm:5)
    public virtual void InitGloballazy_templates()
    {
        lazy_templates = generate_lazy_template_map() as DmList;
        Dm.append(ref gvars_datum_init_order, "lazy_templates");
    }
}

public static partial class GlobalProcs
{
    /**
     * Iterates through all lazy template datums that exist and returns a list of them as an associative list of key -> instance.
     *
     * Screams if more than one key exists, loudly.
     */
    // tg: code/__HELPERS/lazy_templates.dm:8
    public static object generate_lazy_template_map()
    {
        DmList dot = Dm.list();
        foreach (Type template in Dm.each_cast<Type>(subtypesof(typeof(datum_lazy_template))))
        {
            object key = Dm.initial(template, (datum_lazy_template t) => t.key);
            if (!Dm.truthy(key)) // some subtypes like basketball and deathmatch have a base datum with no key
                continue;
            if (Dm.@in(key, dot))
                stack_trace(Dm.text("Found multiple lazy templates with the same key! '[]'", key), "code/__HELPERS/lazy_templates.dm", 15);
            dot[key] = Dm.@new(template);
        }
        return dot;
    }
}
