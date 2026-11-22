// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/logging/atmos.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps A1 (D327): the whole file, the game log of a gas mixture.

public static partial class GlobalProcs
{
    /// Logs the contents of the gasmix to the game log, prefixed by text
    // tg: code/__HELPERS/logging/atmos.dm:2
    public static void log_atmos(object text = null, datum_gas_mixture gas_mixture = null)
    {
        string message = Dm.text("[]\"[]\"", text, print_gas_mixture(gas_mixture));
        //Cache commonly accessed information.
        DmList cached_moles = gas_mixture.moles;
        float heat_capacity = Dm.num(gas_mixture.heat_capacity());
        float temperature = Dm.num(gas_mixture.return_temperature());
        float thermal_energy = temperature * heat_capacity;
        float volume = Dm.num(gas_mixture.return_volume());
        float pressure = Dm.num(gas_mixture.return_pressure());
        float total_moles = Dm.num(gas_mixture.total_moles());
        var cached_specific_heat = (DmList)GLOB.meta_gas_info[META_GAS_SPECIFIC_HEAT]; // GAS_META[META_GAS_SPECIFIC_HEAT]
        var cached_gas_name = (DmList)GLOB.meta_gas_info[META_GAS_NAME]; // GAS_META[META_GAS_NAME]
        ///The total value of the gas mixture in credits.
        float total_value = 0;
        DmList specific_gas_data = Dm.list();

        //Gas specific information assigned to each gas.
        foreach (Type gas_path in Dm.each_cast<Type>(cached_moles))
        {
            float moles = Dm.num(cached_moles[gas_path]);
            float composition = moles / total_moles;
            float energy = temperature * moles * Dm.num(cached_specific_heat[gas_path]);
            float value = Dm.num(Dm.initial(gas_path, (datum_gas p) => p.base_value)) * moles;
            total_value += value;
            specific_gas_data[cached_gas_name[gas_path]] = Dm.list(
                Dm.kv("moles", moles),
                Dm.kv("composition", composition),
                Dm.kv("molar concentration", moles / volume),
                Dm.kv("partial pressure", composition * pressure),
                Dm.kv("energy", energy),
                Dm.kv("energy density", energy / volume),
                Dm.kv("value", value));
        }

        log_game(
            message,
            data: Dm.list(
                Dm.kv("total moles", total_moles),
                Dm.kv("volume", volume),
                Dm.kv("molar density", total_moles / volume),
                Dm.kv("temperature", temperature),
                Dm.kv("pressure", pressure),
                Dm.kv("heat capacity", heat_capacity),
                Dm.kv("energy", thermal_energy),
                Dm.kv("energy density", thermal_energy / volume),
                Dm.kv("value", total_value),
                Dm.kv("gases", specific_gas_data)));
    }
}
