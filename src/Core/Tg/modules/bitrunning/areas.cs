// Translated from tgstation (snapshot 1d99bd7f): code/modules/bitrunning/areas.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps BR1 (D339, D341): the whole file. The areas are var blocks (generated, D111); the virtual areas' hash list is is_area_virtual's
// (__DEFINES/is_helpers.dm:368: NT Pay refuses a transfer from a domain).

public partial class datum_controller_global_vars
{
    /// A hash lookup list of all the virtual area types
    // tg: code/modules/bitrunning/areas.dm:77  GLOBAL_LIST_INIT_TYPED(virtual_areas, /area, populate_virtual_areas())
    public DmList virtual_areas;

    // tg: code/modules/bitrunning/areas.dm:77  (GLOBAL_MANAGED(virtual_areas, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalvirtual_areas()
    {
        virtual_areas = populate_virtual_areas();
        Dm.append(ref gvars_datum_init_order, "virtual_areas");
    }
}

public static partial class GlobalProcs
{
    /// Constructs the list of virtual areas
    // tg: code/modules/bitrunning/areas.dm:80
    public static DmList populate_virtual_areas()
    {
        DmList virtual_areas = Dm.list();
        foreach (Type area_type in Dm.each<Type>(subtypesof(typeof(area))))
        {
            if (Dm.truthy(Dm.bitand(Dm.initial(area_type, (area p) => p.area_flags_mapping), VIRTUAL_AREA)))
                virtual_areas[area_type] = TRUE;
        }
        return virtual_areas;
    }
}
