// Translated from tgstation (snapshot 1d99bd7f): code/modules/cargo/exports/large_objects.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P4b (D327): the whole file: the crates' recycling note, the reagent tanks by how full they are, and the gas canisters by their
// exportable gases. The var blocks are generated.

// tg: code/modules/cargo/exports/large_objects.dm:1
public partial class datum_export_crate : datum_export
{
    // That's why a goddamn metal crate costs that much.
    // tg: code/modules/cargo/exports/large_objects.dm:14
    public override object total_printout(datum_export_report ex = null, object notes = null)
    {
        notes ??= TRUE;
        object dot = base.total_printout(ex, notes);
        if (Dm.truthy(dot) && Dm.truthy(notes))
            dot = Dm.add(dot, I18n.T(" Thanks for participating in Nanotrasen Crates Recycling Program."));
        return dot;
    }
}

// tg: code/modules/cargo/exports/large_objects.dm:48
public partial class datum_export_reagent_dispenser : datum_export
{
    // tg: code/modules/cargo/exports/large_objects.dm:54
    public override object get_base_cost(obj exported_item = null)
    {
        var dispenser = (obj_structure_reagent_dispensers)exported_item;
        var reagents = (datum_reagents)dispenser.reagents;
        return Dm.num(base.get_base_cost(exported_item)) + round(contents_cost * (Dm.num(reagents.total_volume) / Dm.num(reagents.maximum_volume)));
    }
}

/**
 * Gas canister exports.
 * I'm going to put a quick aside here as this has been a pain to balance for several years now, and I'd like to at least break how to keep gas exports tame.
 * So: Gas exports are dependent on the amount of gas in the canister. The amount of gas in the canister is dependent on the pressure and temperature of the canister.
 * Maximum pressure a canister can withstand is 9.2e13 kPa at a minimum of 2.7K which would contain a horrifying 4,098,150,709.4 moles.
 * We don't want players making that much credits so we limit the total amount earned to MAX_GAS_CREDITS
*/
// tg: code/modules/cargo/exports/large_objects.dm:123
public partial class datum_export_gas_canister : datum_export
{
    // tg: code/modules/cargo/exports/large_objects.dm:117  #define MAX_GAS_CREDITS 15000 (file-local, #undef at :150)
    private const float MAX_GAS_CREDITS = 15000;

    // tg: code/modules/cargo/exports/large_objects.dm:128
    public override object get_base_cost(obj exported_item = null)
    {
        var canister = (obj_machinery_portable_atmospherics_canister)exported_item;
        var canister_mix = Waiting.NoSleep(canister.return_air()) as datum_gas_mixture;
        if (!Dm.truthy(canister_mix.total_moles()))
            return 0;
        DmList cached_moles = canister_mix.moles;

        float worth = cost;
        foreach (Type gas in Dm.each_cast<Type>(GLOB.meta_gas_info[META_GAS_ID] as DmList))
        {
            if (!Dm.truthy(Dm.bitand(Dm.initial(gas, (datum_gas p) => p.cargo_flags), GAS_EXPORTABLE)))
                continue;
            canister_mix.assert_gas(gas);
            if (Dm.num(cached_moles[gas]) > 0)
            {
                worth += get_gas_value(gas, cached_moles[gas]);
                if (worth > MAX_GAS_CREDITS)
                {
                    worth = MAX_GAS_CREDITS;
                    break;
                }
            }
        }

        return worth;
    }

    // tg: code/modules/cargo/exports/large_objects.dm:147
    public virtual float get_gas_value(Type gasType = null, object moles = null)
    {
        return ROUND_UP(Dm.initial(gasType, (datum_gas p) => p.base_value) * Dm.num(moles));
    }
}
