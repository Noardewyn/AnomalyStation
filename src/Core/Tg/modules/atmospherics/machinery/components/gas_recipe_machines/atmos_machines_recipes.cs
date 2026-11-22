// Translated from tgstation (snapshot 1d99bd7f): code/modules/atmospherics/machinery/components/gas_recipe_machines/atmos_machines_recipes.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Engineering E12a (D287): the whole file, the crystallizer's recipe list. The recipes are data: their var blocks (the
// requirements, temperatures, energy and products) are generated (D111).

public partial class datum_controller_global_vars
{
    ///Global list of recipes for atmospheric machines to use
    // tg: code/modules/atmospherics/machinery/components/gas_recipe_machines/atmos_machines_recipes.dm:2  GLOBAL_LIST_INIT(gas_recipe_meta, gas_recipes_list())
    public DmList gas_recipe_meta;

    // tg: code/modules/atmospherics/machinery/components/gas_recipe_machines/atmos_machines_recipes.dm:2  (GLOBAL_MANAGED(gas_recipe_meta, gas_recipes_list()), __DEFINES/_globals.dm)
    public virtual void InitGlobalgas_recipe_meta()
    {
        gas_recipe_meta = gas_recipes_list() as DmList;
        Dm.append(ref gvars_datum_init_order, "gas_recipe_meta");
    }
}

public static partial class GlobalProcs
{
    /*
     * Global proc to build the gas recipe global list
     */
    // tg: code/modules/atmospherics/machinery/components/gas_recipe_machines/atmos_machines_recipes.dm:7
    public static object gas_recipes_list()
    {
        DmList dot = Dm.list();
        // the recipes are data-only subtypes (no procs): every one is created, as tg's gas types (types-converter.md 8, "Not wrapped")
        foreach (Type recipe_path in Dm.each_cast<Type>(subtypesof(typeof(datum_gas_recipe))))
        {
            datum_gas_recipe recipe = Dm.@new(recipe_path) as datum_gas_recipe;

            dot[recipe.id] = recipe;
        }
        return dot;
    }
}
