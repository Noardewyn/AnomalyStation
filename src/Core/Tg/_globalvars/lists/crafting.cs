// Translated from tgstation (snapshot 1d99bd7f): code/_globalvars/lists/crafting.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5 slice 2 (materials and stacks): the whole file (init_crafting_recipes reads the food categories).

public partial class datum_controller_global_vars
{
    // tg: code/_globalvars/lists/crafting.dm:1  GLOBAL_LIST_INIT(crafting_category_food, list(...))
    public DmList crafting_category_food;

    // tg: code/_globalvars/lists/crafting.dm:1  (GLOBAL_MANAGED(crafting_category_food, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalcrafting_category_food()
    {
        crafting_category_food = Dm.list(
            CAT_FOOD,
            CAT_DRINK
        );
        Dm.append(ref gvars_datum_init_order, "crafting_category_food");
    }

    // tg: code/_globalvars/lists/crafting.dm:6  GLOBAL_LIST_INIT(crafting_category, list(...))
    public DmList crafting_category;

    // tg: code/_globalvars/lists/crafting.dm:6  (GLOBAL_MANAGED(crafting_category, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalcrafting_category()
    {
        crafting_category = Dm.list(
            CAT_WEAPON_RANGED,
            CAT_WEAPON_MELEE,
            CAT_WEAPON_AMMO,
            CAT_ROBOT,
            CAT_MISC,
            CAT_CLOTHING,
            CAT_CHEMISTRY,
            CAT_ATMOSPHERIC,
            CAT_STRUCTURE,
            CAT_TILES,
            CAT_WINDOWS,
            CAT_DOORS,
            CAT_FURNITURE,
            CAT_EQUIPMENT,
            CAT_CONTAINERS,
            CAT_ENTERTAINMENT,
            CAT_TOOLS,
            CAT_CULT,
            CAT_CONSTRUCTION
        );
        Dm.append(ref gvars_datum_init_order, "crafting_category");
    }
}
