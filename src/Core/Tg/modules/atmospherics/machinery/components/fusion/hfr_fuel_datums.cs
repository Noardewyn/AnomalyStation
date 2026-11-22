// Translated from tgstation (snapshot 1d99bd7f): code/modules/atmospherics/machinery/components/fusion/hfr_fuel_datums.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Package E12b (docs/ai/specs/engineering.md): the whole file, the hypertorus fuel recipes and their global list. The seven recipes' vars
// are generated.

public partial class datum_controller_global_vars
{
    ///Global list of recipes for atmospheric machines to use
    // tg: code/modules/atmospherics/machinery/components/fusion/hfr_fuel_datums.dm:2  GLOBAL_LIST_INIT(hfr_fuels_list, hfr_fuels_create_list())
    public DmList hfr_fuels_list;

    // tg: code/modules/atmospherics/machinery/components/fusion/hfr_fuel_datums.dm:2  (GLOBAL_MANAGED(hfr_fuels_list, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalhfr_fuels_list()
    {
        hfr_fuels_list = hfr_fuels_create_list() as DmList;
        Dm.append(ref gvars_datum_init_order, "hfr_fuels_list");
    }
}

public static partial class GlobalProcs
{
    /*
     * Global proc to build the gas recipe global list
     */
    // tg: code/modules/atmospherics/machinery/components/fusion/hfr_fuel_datums.dm:7
    public static object hfr_fuels_create_list()
    {
        object dot = Dm.list();
        foreach (Type fuel_mix_path in Dm.each<Type>(subtypesof(typeof(datum_hfr_fuel))))
        {
            datum_hfr_fuel fuel_mix = Dm.@new(fuel_mix_path) as datum_hfr_fuel;

            ((DmList)dot)[fuel_mix.id] = fuel_mix;
        }
        return dot;
    }
}

// tg: code/modules/atmospherics/machinery/components/fusion/hfr_fuel_datums.dm:14
public partial class datum_hfr_fuel : datum
{
    // tg: code/modules/atmospherics/machinery/components/fusion/hfr_fuel_datums.dm:40
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        temperature_change_multiplier = min(temperature_change_multiplier, 1);
    }
}
