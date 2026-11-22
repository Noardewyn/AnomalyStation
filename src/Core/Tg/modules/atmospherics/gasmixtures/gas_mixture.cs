// Translated from tgstation (snapshot 1d99bd7f): code/modules/atmospherics/gasmixtures/gas_mixture.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only GLOB.meta_gas_info, the var block, New and the listmos procs a breath and a turf's air need translated (M2
// P2, scope.md 2e "atmosphere (minimal gas_mixture)": the asserts, garbage_collect, heat capacity, total_moles, pressure,
// temperature, volume, thermal_energy, archive, merge, set/adjust/convert, remove and its variants, equalize, copy and
// copy_from, get_breath_partial_pressure), with /datum/gas_mixture/turf. M4b slice 1 (D134, the air that moves): the
// LINDA sharing copy_from_ratio, share, temperature_share, compare and react (the reactions themselves came with fire and
// plasma, M4b slice 3, reactions.dm). M4b slice 2 (the machines): the pump transfers (gas_pressure_minimum_transfer,
// gas_pressure_calculate with its quadratic and Newton solvers, pump_gas_to, release_gas_to) and check_gases. The gas
// visuals' return_visuals came with M4b slice 3. Electrolysis (electrolyze, the M7 zaps slice: a tesla zap's). The string form
// (to_string, the map export of M10) stays not translated.

public partial class datum_controller_global_vars
{
    // tg: code/modules/atmospherics/gasmixtures/gas_mixture.dm:7  GLOBAL_LIST_INIT(meta_gas_info, meta_gas_list()) //see ATMOSPHERICS/gas_types.dm
    public DmList meta_gas_info;

    // tg: code/modules/atmospherics/gasmixtures/gas_mixture.dm:7  (GLOBAL_MANAGED(meta_gas_info, meta_gas_list()), __DEFINES/_globals.dm)
    public virtual void InitGlobalmeta_gas_info()
    {
        meta_gas_info = meta_gas_list() as DmList;
        Dm.append(ref gvars_datum_init_order, "meta_gas_info");
    }
}

// tg: code/modules/atmospherics/gasmixtures/gas_mixture.dm:9
[DmPath("/datum/gas_mixture")]
public partial class datum_gas_mixture : datum
{
    /// Associative list of moles for each gas. List key is /datum/gas/<gas_name>, value is amount in moles
    // tg: code/modules/atmospherics/gasmixtures/gas_mixture.dm:11  (a plain list at the snapshot; New assigns list())
    public DmList moles;
    /// Archived version of moles
    // tg: code/modules/atmospherics/gasmixtures/gas_mixture.dm:13
    public DmList moles_archive;
    /// Static list of gas meta data like heat capacity (initialized globally)
    // tg: code/modules/atmospherics/gasmixtures/gas_mixture.dm:15  var/static/list/gas_meta  (rules 10.6: a TgState field, below)
    public static ref DmList gas_meta => ref TgState.Current.datum_gas_mixture__gas_meta;
    /// The temperature of the gas mix in kelvin. Should never be lower then TCMB
    // tg: code/modules/atmospherics/gasmixtures/gas_mixture.dm:17
    public float temperature = TCMB;
    /// Used, like all archived variables, to ensure turf sharing is consistent inside a tick, no matter
    /// The order of operations
    // tg: code/modules/atmospherics/gasmixtures/gas_mixture.dm:20  var/tmp/temperature_archived
    public float temperature_archived = TCMB;
    /// Volume in liters (duh)
    // tg: code/modules/atmospherics/gasmixtures/gas_mixture.dm:22  (float?: the null-use grep matches isnull(volume),
    // gas_mixture.dm:36, New's parameter)
    public float? volume = CELL_VOLUME;
    /// The last tick this gas mixture shared on. A counter that turfs use to manage activity
    // tg: code/modules/atmospherics/gasmixtures/gas_mixture.dm:24
    public float last_share = 0;
    /// Tells us what reactions have happened in our gasmix. Assoc list of reaction - moles reacted pair.
    // tg: code/modules/atmospherics/gasmixtures/gas_mixture.dm:26
    public DmList reaction_results;
    /// Whether to call garbage_collect() on the sharer during shares, used for immutable mixtures
    // tg: code/modules/atmospherics/gasmixtures/gas_mixture.dm:28
    public float gc_share = FALSE;
    /// When this gas mixture was last touched by pipeline processing
    /// I am sorry
    // tg: code/modules/atmospherics/gasmixtures/gas_mixture.dm:31
    public float pipeline_cycle = -1;
}

public sealed partial class TgState
{
    // tg: code/modules/atmospherics/gasmixtures/gas_mixture.dm:15  var/static/list/gas_meta
    internal DmList datum_gas_mixture__gas_meta;
}

// tg: code/modules/atmospherics/gasmixtures/gas_mixture.dm:34 (interleaved types, rules 1.9)
public partial class datum_gas_mixture : datum
{
    // tg: code/modules/atmospherics/gasmixtures/gas_mixture.dm:33
    [DmParams("volume")]
    public override async DmTask New(params object[] __extra)
    {
        object volume = Dm.arg<object>(__extra, 0);
        moles = Dm.list();
        moles_archive = Dm.list();
        if (volume != null)
            this.volume = Dm.num(volume);
        if (Dm.num(this.volume) <= 0)
            stack_trace("Created a gas mixture with zero volume!", "code/modules/atmospherics/gasmixtures/gas_mixture.dm", 40);
        reaction_results = Dm.list();
    }
}

//listmos procs
//use the macros in performance intensive areas. for their definitions, refer to code/__DEFINES/atmospherics.dm

// tg: code/modules/atmospherics/gasmixtures/gas_mixture.dm:47
public partial class datum_gas_mixture : datum
{
    // tg: code/modules/atmospherics/gasmixtures/gas_mixture.dm:47
    public virtual void assert_gas(object gas_id = null)
    {
        Dm.append_at(moles, gas_id, 0);
        Dm.append_at(moles_archive, gas_id, 0);
    }

    // tg: code/modules/atmospherics/gasmixtures/gas_mixture.dm:52
    public virtual void assert_gases(params object[] __extra)
    {
        DmList cached_moles = moles;
        DmList cached_moles_archive = moles_archive;
        foreach (object gas_id in Dm.each(Dm.args(__extra)))
        {
            Dm.append_at(cached_moles, gas_id, 0);
            Dm.append_at(cached_moles_archive, gas_id, 0);
        }
    }

    // tg: code/modules/atmospherics/gasmixtures/gas_mixture.dm:61
    public virtual void add_gas(object gas_id = null)
    {
        moles[gas_id] = 0;
        moles_archive[gas_id] = 0;
    }

    // tg: code/modules/atmospherics/gasmixtures/gas_mixture.dm:66
    public virtual void add_gases(params object[] __extra)
    {
        DmList cached_moles = moles;
        DmList cached_moles_archive = moles_archive;
        foreach (object gas_id in Dm.each(Dm.args(__extra)))
        {
            cached_moles[gas_id] = 0;
            cached_moles_archive[gas_id] = 0;
        }
    }

    // tg: code/modules/atmospherics/gasmixtures/gas_mixture.dm:78
    public virtual void garbage_collect()
    {
        values_cut_under(moles, MOLAR_ACCURACY, TRUE);
        values_cut_under(moles_archive, MOLAR_ACCURACY, TRUE);
    }

    // tg: code/modules/atmospherics/gasmixtures/gas_mixture.dm:85
    public virtual object heat_capacity()
    {
        return values_dot(moles, gas_meta[META_GAS_SPECIFIC_HEAT]);
    }

    // tg: code/modules/atmospherics/gasmixtures/gas_mixture.dm:90
    public virtual object heat_capacity_archive()
    {
        return values_dot(moles_archive, gas_meta[META_GAS_SPECIFIC_HEAT]);
    }
}

// tg: code/modules/atmospherics/gasmixtures/gas_mixture.dm:94  (the type is declared by its first proc override)
[DmPath("/datum/gas_mixture/turf")]
public partial class datum_gas_mixture_turf : datum_gas_mixture
{
    /// Same as above except vacuums return HEAT_CAPACITY_VACUUM
    // tg: code/modules/atmospherics/gasmixtures/gas_mixture.dm:94
    public override object heat_capacity()
    {
        return (values_dot(moles, gas_meta[META_GAS_SPECIFIC_HEAT]) is var or__1 && Dm.truthy(or__1) ? or__1 : HEAT_CAPACITY_VACUUM);
    }

    /// Same as above except vacuums return HEAT_CAPACITY_VACUUM
    // Separate function to reduce branches in a hot function
    // tg: code/modules/atmospherics/gasmixtures/gas_mixture.dm:99
    public override object heat_capacity_archive()
    {
        return (values_dot(moles_archive, gas_meta[META_GAS_SPECIFIC_HEAT]) is var or__1 && Dm.truthy(or__1) ? or__1 : HEAT_CAPACITY_VACUUM);
    }
}

// tg: code/modules/atmospherics/gasmixtures/gas_mixture.dm:103 (interleaved types, rules 1.9)
public partial class datum_gas_mixture : datum
{
    // tg: code/modules/atmospherics/gasmixtures/gas_mixture.dm:103
    public virtual object total_moles()
    {
        return values_sum(moles);
    }

    // tg: code/modules/atmospherics/gasmixtures/gas_mixture.dm:109
    public virtual object has_gas(object gas_id = null, float? amount = null)
    {
        amount ??= 0;
        return Dm.num(Dm.num(amount) < Dm.num(moles[gas_id]));
    }

    // tg: code/modules/atmospherics/gasmixtures/gas_mixture.dm:113
    public virtual object return_pressure()
    {
        if (Dm.truthy(volume)) // to prevent division by zero
            return values_sum(moles) * R_IDEAL_GAS_EQUATION * temperature / Dm.num(volume);
        return 0;
    }

    // tg: code/modules/atmospherics/gasmixtures/gas_mixture.dm:119
    public virtual object return_temperature()
    {
        return temperature;
    }

    // tg: code/modules/atmospherics/gasmixtures/gas_mixture.dm:123
    public virtual object return_volume()
    {
        return max(0, Dm.num(volume));
    }

    // tg: code/modules/atmospherics/gasmixtures/gas_mixture.dm:127
    public virtual object return_visuals(turf z_context = null)
    {
        DmList output = GAS_OVERLAYS(moles, z_context);
        return output;
    }

    // tg: code/modules/atmospherics/gasmixtures/gas_mixture.dm:133
    public virtual object thermal_energy()
    {
        return THERMAL_ENERGY(this); //see code/__DEFINES/atmospherics.dm; use the define in performance critical areas
    }

    // tg: code/modules/atmospherics/gasmixtures/gas_mixture.dm:137
    public virtual void archive()
    {
        moles_archive = moles.Copy();
        temperature_archived = temperature;
    }

    // tg: code/modules/atmospherics/gasmixtures/gas_mixture.dm:142
    public virtual object merge(datum_gas_mixture giver = null)
    {
        if (giver == null)
            return FALSE;

        //heat transfer
        if (abs(temperature - giver.temperature) > MINIMUM_TEMPERATURE_DELTA_TO_CONSIDER)
        {
            float self_heat_capacity = Dm.num(heat_capacity());
            float giver_heat_capacity = Dm.num(giver.heat_capacity());
            float combined_heat_capacity = giver_heat_capacity + self_heat_capacity;
            if (Dm.truthy(combined_heat_capacity))
                temperature = (giver.temperature * giver_heat_capacity + temperature * self_heat_capacity) / combined_heat_capacity;
        }

        DmList cached_moles = moles; //accessing datum vars is slower than proc vars
        DmList cached_giver_moles = giver.moles;
        //gas transfer
        foreach ((object gas_id, object value) in Dm.pairs(cached_giver_moles))
            Dm.append_at(cached_moles, gas_id, value);

        if (Dm.truthy(_listen_lookup?[COMSIG_GASMIX_MERGED]))
            _SendSignal(COMSIG_GASMIX_MERGED, Dm.list(this));
        return TRUE;
    }

    // tg: code/modules/atmospherics/gasmixtures/gas_mixture.dm:164
    public virtual void set_gas(object gas_specie = null, object amount = null)
    {
        moles[gas_specie] = amount;
        garbage_collect();
    }

    // tg: code/modules/atmospherics/gasmixtures/gas_mixture.dm:168
    public virtual void set_temperature(object target_temp = null)
    {
        temperature = Dm.num(target_temp);
    }

    // tg: code/modules/atmospherics/gasmixtures/gas_mixture.dm:173
    public virtual void adjust_gas(object gas = null, object amount = null)
    {
        Dm.append_at(moles, gas, QUANTIZE(amount));
        garbage_collect();
    }

    // tg: code/modules/atmospherics/gasmixtures/gas_mixture.dm:179
    public virtual void adjust_multiple_gases(DmList gases_moles = null)
    {
        DmList cached_moles = moles;
        foreach ((object gas_id, object value) in Dm.pairs(gases_moles))
            Dm.append_at(cached_moles, gas_id, value);
        garbage_collect();
    }

    // tg: code/modules/atmospherics/gasmixtures/gas_mixture.dm:188
    public virtual void convert_gas(datum_gas reactant = null, datum_gas product = null, object conversion_amount = null)
    {
        DmList cached_moles = moles;
        assert_gases(reactant, product);
        Dm.remove_at(cached_moles, reactant, QUANTIZE(conversion_amount));
        Dm.append_at(cached_moles, product, QUANTIZE(conversion_amount));
        garbage_collect();
    }

    // tg: code/modules/atmospherics/gasmixtures/gas_mixture.dm:197
    public virtual object remove(object amount = null)
    {
        DmList cached_moles = moles;
        float total_moles = values_sum(cached_moles);
        amount = min(Dm.num(amount), total_moles); //Can not take more air than tile has!
        if (Dm.num(amount) <= 0)
            return null;
        float ratio = Dm.num(amount) / total_moles;

        datum_gas_mixture removed = (datum_gas_mixture)Dm.@new(type, volume);
        DmList cached_removed_moles = removed.moles; //accessing datum vars is slower than proc vars

        removed.temperature = temperature;
        foreach ((object id, object value) in Dm.pairs(cached_moles))
        {
            cached_removed_moles[id] = QUANTIZE(Dm.num(value) * ratio);
            Dm.remove_at(cached_moles, id, cached_removed_moles[id]);
        }

        garbage_collect();

        if (Dm.truthy(_listen_lookup?[COMSIG_GASMIX_REMOVED]))
            _SendSignal(COMSIG_GASMIX_REMOVED, Dm.list(this));
        return removed;
    }

    // tg: code/modules/atmospherics/gasmixtures/gas_mixture.dm:221
    public virtual object remove_ratio(object ratio = null)
    {
        if (Dm.num(ratio) <= 0)
        {
            datum_gas_mixture removed__1 = Dm.@new<datum_gas_mixture>(volume);
            return removed__1;
        }
        ratio = min(Dm.num(ratio), 1);

        DmList cached_moles = moles;
        datum_gas_mixture removed = (datum_gas_mixture)Dm.@new(type, volume);
        DmList cached_removed_moles = removed.moles; //accessing datum vars is slower than proc vars

        removed.temperature = temperature;
        foreach ((object id, object value) in Dm.pairs(cached_moles))
        {
            cached_removed_moles[id] = QUANTIZE(Dm.num(value) * Dm.num(ratio));
            Dm.remove_at(cached_moles, id, cached_removed_moles[id]);
        }

        garbage_collect();

        if (Dm.truthy(_listen_lookup?[COMSIG_GASMIX_REMOVED]))
            _SendSignal(COMSIG_GASMIX_REMOVED, Dm.list(this));
        return removed;
    }

    // tg: code/modules/atmospherics/gasmixtures/gas_mixture.dm:243
    public virtual object remove_specific(object gas_id = null, object amount = null)
    {
        DmList cached_moles = moles;
        amount = min(Dm.num(amount), Dm.num(cached_moles[gas_id]));
        if (Dm.num(amount) <= 0)
            return null;
        datum_gas_mixture removed = (datum_gas_mixture)Dm.@new(type);
        removed.temperature = temperature;
        removed.moles[gas_id] = amount;
        Dm.remove_at(cached_moles, gas_id, amount);

        // TODO(antropod): maybe use if (cached_moles[gas_id] < MOLAR_ACCURACY) cached_moles -= gas_id?
        garbage_collect();
        return removed;
    }

    // tg: code/modules/atmospherics/gasmixtures/gas_mixture.dm:257
    public virtual object remove_specific_ratio(object gas_id = null, object ratio = null)
    {
        if (Dm.num(ratio) <= 0)
            return null;
        ratio = min(Dm.num(ratio), 1);

        DmList cached_moles = moles;
        datum_gas_mixture removed = (datum_gas_mixture)Dm.@new(type);
        DmList cached_removed_moles = removed.moles; //accessing datum vars is slower than proc vars

        removed.temperature = temperature;
        cached_removed_moles[gas_id] = QUANTIZE(Dm.num(cached_moles[gas_id]) * Dm.num(ratio));
        Dm.remove_at(cached_moles, gas_id, cached_removed_moles[gas_id]);

        garbage_collect();

        return removed;
    }

    // tg: code/modules/atmospherics/gasmixtures/gas_mixture.dm:276
    public virtual object equalize(datum_gas_mixture other = null)
    {
        object dot = null;
        dot = FALSE;
        if (abs(Dm.num(return_temperature()) - Dm.num(other.return_temperature())) > MINIMUM_TEMPERATURE_DELTA_TO_SUSPEND)
        {
            dot = TRUE;
            float self_heat_cap = Dm.num(heat_capacity());
            float other_heat_cap = Dm.num(other.heat_capacity());
            float new_temp = (temperature * self_heat_cap + other.temperature * other_heat_cap) / (self_heat_cap + other_heat_cap);
            temperature = new_temp;
            other.temperature = new_temp;
        }

        float min_p_delta = 0.1f;
        float total_volume = Dm.num(volume) + Dm.num(other.volume);
        DmList cached_moles = moles;
        DmList cached_other_moles = other.moles;
        object gas_list = Dm.bitor(cached_moles, cached_other_moles);
        foreach (object gas_id in Dm.each(gas_list))
        {
            assert_gas(gas_id);
            other.assert_gas(gas_id);
            //math is under the assumption temperatures are equal
            if (abs(Dm.num(cached_moles[gas_id]) / Dm.num(volume) - Dm.num(cached_other_moles[gas_id]) / Dm.num(other.volume)) > min_p_delta / (R_IDEAL_GAS_EQUATION * temperature))
            {
                dot = TRUE;
                float total_moles = Dm.num(cached_moles[gas_id]) + Dm.num(cached_other_moles[gas_id]);
                cached_moles[gas_id] = total_moles * (Dm.num(volume) / total_volume);
                cached_other_moles[gas_id] = total_moles * (Dm.num(other.volume) / total_volume);
            }
        }
        garbage_collect();
        other.garbage_collect();
        return dot;
    }

    // tg: code/modules/atmospherics/gasmixtures/gas_mixture.dm:305
    public virtual object copy()
    {
        DmList cached_moles = moles;
        datum_gas_mixture copy = (datum_gas_mixture)Dm.@new(type);
        DmList copy_cached_moles = copy.moles;
        DmList copy_cached_moles_archive = copy.moles_archive;

        copy.temperature = temperature;
        foreach ((object gas_id, object value) in Dm.pairs(cached_moles))
        {
            copy_cached_moles[gas_id] = value;
            copy_cached_moles_archive[gas_id] = 0;
        }

        return copy;
    }

    // tg: code/modules/atmospherics/gasmixtures/gas_mixture.dm:321
    public virtual object copy_from(datum_gas_mixture sample = null)
    {
        DmList cached_moles = moles; //accessing datum vars is slower than proc vars
        DmList cached_moles_archive = moles_archive;
        DmList sample_cached_moles = sample.moles;

        //remove all gases
        cached_moles.Cut();
        cached_moles_archive.Cut();

        temperature = sample.temperature;
        foreach ((object gas_id, object value) in Dm.pairs(sample_cached_moles))
        {
            cached_moles[gas_id] = value;
            cached_moles_archive[gas_id] = 0;
        }

        return TRUE;
    }

    // tg: code/modules/atmospherics/gasmixtures/gas_mixture.dm:339
    public virtual object copy_from_ratio(datum_gas_mixture sample = null, float? partial = null)
    {
        partial ??= 1;
        DmList cached_moles = moles; //accessing datum vars is slower than proc vars
        DmList sample_cached_moles = sample.moles;
        // Remove all gases (sample overrides our values anyways)
        cached_moles.Cut();
        temperature = sample.temperature;
        foreach ((object gas_id, object amount) in Dm.pairs(sample_cached_moles))
            cached_moles[gas_id] = Dm.num(amount) * Dm.num(partial);
        return TRUE;
    }

    /// Performs air sharing calculations between two gas_mixtures
    /// share() is communitive, which means A.share(B) needs to be the same as B.share(A)
    /// If we don't retain this, we will get negative moles. Don't do it
    /// Returns: amount of gas exchanged (+ if sharer received)
    // tg: code/modules/atmospherics/gasmixtures/gas_mixture.dm:353
    public virtual object share(datum_gas_mixture sharer = null, object our_coeff = null, object sharer_coeff = null)
    {
        DmList cached_moles = moles;
        DmList cached_moles_archive = moles_archive;
        DmList sharer_cached_moles = sharer.moles;
        DmList sharer_cached_moles_archive = sharer.moles_archive;

        DmList only_in_sharer = Dm.sub(sharer_cached_moles, cached_moles) as DmList;
        DmList only_in_cached = Dm.sub(cached_moles, sharer_cached_moles) as DmList;

        float temperature_delta = temperature_archived - sharer.temperature_archived;
        bool temp_delta_threshold = abs(temperature_delta) > MINIMUM_TEMPERATURE_DELTA_TO_CONSIDER;

        float old_self_heat_capacity = 0;
        float old_sharer_heat_capacity = 0;
        if (temp_delta_threshold)
        {
            old_self_heat_capacity = Dm.num(heat_capacity());
            old_sharer_heat_capacity = Dm.num(sharer.heat_capacity());
        }

        float heat_capacity_self_to_sharer = 0; //heat capacity of the moles transferred from us to the sharer
        float heat_capacity_sharer_to_self = 0; //heat capacity of the moles transferred from the sharer to us

        float moved_moles = 0;
        float abs_moved_moles = 0;

        //GAS TRANSFER

        //Prep
        foreach (object gas_id in Dm.each(only_in_sharer)) //create gases not in our cache
        {
            cached_moles[gas_id] = 0;
            cached_moles_archive[gas_id] = 0;
        }
        foreach (object gas_id in Dm.each(only_in_cached)) //create gases not in the sharing mix
        {
            sharer_cached_moles[gas_id] = 0;
            sharer_cached_moles_archive[gas_id] = 0;
        }

        DmList cached_specific_heat = gas_meta[META_GAS_SPECIFIC_HEAT] as DmList;
        foreach (object gas_id in Dm.each(cached_moles)) //transfer gases
        {
            float delta = QUANTIZE(Dm.num(cached_moles_archive[gas_id]) - Dm.num(sharer_cached_moles_archive[gas_id])); //the amount of gas that gets moved between the mixtures

            if (delta == 0)
                continue;

            // If we have more gas then they do, gas is moving from us to them
            // This means we want to scale it by our coeff. Vis versa for their case
            if (delta > 0)
                delta = delta * Dm.num(our_coeff);
            else
                delta = delta * Dm.num(sharer_coeff);

            if (temp_delta_threshold)
            {
                float gas_heat_capacity = delta * Dm.num(cached_specific_heat[gas_id]);
                if (delta > 0)
                    heat_capacity_self_to_sharer += gas_heat_capacity;
                else
                    heat_capacity_sharer_to_self -= gas_heat_capacity; //subtract here instead of adding the absolute value because we know that delta is negative.
            }

            cached_moles[gas_id] = Dm.num(cached_moles[gas_id]) - delta;
            sharer_cached_moles[gas_id] = Dm.num(sharer_cached_moles[gas_id]) + delta;
            moved_moles += delta;
            abs_moved_moles += abs(delta);
        }

        last_share = abs_moved_moles;

        //THERMAL ENERGY TRANSFER
        if (temp_delta_threshold)
        {
            float new_self_heat_capacity = old_self_heat_capacity + heat_capacity_sharer_to_self - heat_capacity_self_to_sharer;
            float new_sharer_heat_capacity = old_sharer_heat_capacity + heat_capacity_self_to_sharer - heat_capacity_sharer_to_self;

            //transfer of thermal energy (via changed heat capacity) between self and sharer
            if (new_self_heat_capacity > MINIMUM_HEAT_CAPACITY)
                temperature = (old_self_heat_capacity * temperature - heat_capacity_self_to_sharer * temperature_archived + heat_capacity_sharer_to_self * sharer.temperature_archived) / new_self_heat_capacity;

            if (new_sharer_heat_capacity > MINIMUM_HEAT_CAPACITY)
            {
                sharer.temperature = (old_sharer_heat_capacity * sharer.temperature - heat_capacity_sharer_to_self * sharer.temperature_archived + heat_capacity_self_to_sharer * temperature_archived) / new_sharer_heat_capacity;
                //thermal energy of the system (self and sharer) is unchanged

                if (abs(old_sharer_heat_capacity) > MINIMUM_HEAT_CAPACITY)
                {
                    if (abs(new_sharer_heat_capacity / old_sharer_heat_capacity - 1) < 0.1f) // <10% change in sharer heat capacity
                        temperature_share(sharer, OPEN_HEAT_TRANSFER_COEFFICIENT);
                }
            }
        }

        if (Dm.truthy(length(Dm.add(only_in_sharer, only_in_cached)))) //if all gases were present in both mixtures, we know that no gases are 0
        {
            // garbage_collect(only_in_cached) and sharer.garbage_collect(only_in_sharer): garbage_collect takes no parameter at the
            // snapshot, so DM drops the argument
            garbage_collect(); //any gases the sharer had, we are guaranteed to have. gases that it didn't have we are not.
            sharer.garbage_collect(); //the reverse is equally true
        }
        else if (Dm.truthy(Dm.initial(sharer, (datum_gas_mixture p) => p.gc_share)))
            sharer.garbage_collect();

        if (temperature_delta > MINIMUM_TEMPERATURE_TO_MOVE || abs(moved_moles) > MINIMUM_MOLES_DELTA_TO_MOVE)
        {
            float our_moles = values_sum(cached_moles);
            float their_moles = values_sum(sharer_cached_moles);
            return (temperature_archived * (our_moles + moved_moles) - sharer.temperature_archived * (their_moles - moved_moles)) * R_IDEAL_GAS_EQUATION / Dm.num(volume);
        }
        return null;
    }

    ///Performs temperature sharing calculations (via conduction) between two gas_mixtures assuming only 1 boundary length
    ///Returns: new temperature of the sharer
    // tg: code/modules/atmospherics/gasmixtures/gas_mixture.dm:445
    public virtual object temperature_share(datum_gas_mixture sharer = null, object conduction_coefficient = null, object sharer_temperature = null, object sharer_heat_capacity = null)
    {
        //transfer of thermal energy (via conduction) between self and sharer
        if (sharer != null)
            sharer_temperature = sharer.temperature_archived;
        float temperature_delta = temperature_archived - Dm.num(sharer_temperature);
        if (abs(temperature_delta) > MINIMUM_TEMPERATURE_DELTA_TO_CONSIDER)
        {
            float self_heat_capacity = Dm.num(heat_capacity_archive());
            sharer_heat_capacity = Dm.truthy(sharer_heat_capacity) ? sharer_heat_capacity : sharer.heat_capacity_archive();

            if ((Dm.num(sharer_heat_capacity) > MINIMUM_HEAT_CAPACITY) && (self_heat_capacity > MINIMUM_HEAT_CAPACITY))
            {
                // coefficient applied first because some turfs have very big heat caps.
                float heat = CALCULATE_CONDUCTION_ENERGY(Dm.num(conduction_coefficient) * temperature_delta, Dm.num(sharer_heat_capacity), self_heat_capacity);

                temperature = max(temperature - heat / self_heat_capacity, TCMB);
                sharer_temperature = max(Dm.num(sharer_temperature) + heat / Dm.num(sharer_heat_capacity), TCMB);
                if (sharer != null)
                {
                    sharer.temperature = Dm.num(sharer_temperature);
                    if (Dm.truthy(Dm.initial(sharer, (datum_gas_mixture p) => p.gc_share)))
                        sharer.garbage_collect();
                }
            }
        }
        return sharer_temperature;
        //thermal energy of the system (self and sharer) is unchanged
    }

    ///Compares sample to self to see if within acceptable ranges that group processing may be enabled
    ///Takes the bool as a second arg to read to read archived values for moles and temperature
    ///Returns: a string indicating what check failed, or "" if check passes
    // tg: code/modules/atmospherics/gasmixtures/gas_mixture.dm:470
    public virtual object compare(datum_gas_mixture sample = null, object cmp_archive = null)
    {
        DmList cached_moles = Dm.truthy(cmp_archive) ? moles_archive : moles;
        DmList sample_cached_moles = Dm.truthy(cmp_archive) ? sample.moles_archive : sample.moles; //accessing datum vars is slower than proc vars
        float total_delta = abs(values_sum(moles) - values_sum(sample.moles));

        if (total_delta > MINIMUM_MOLES_DELTA_TO_MOVE) //difference in total moles no need for further iterration
            return "total_moles";

        foreach (object gas_id in Dm.each(Dm.bitor(cached_moles, sample_cached_moles))) // compare gases from either mixture
        {
            float gas_moles = Dm.num(cached_moles[gas_id]); // it can be null, but everything coerce to 0 after, so we save JMP and Tst
            float sample_moles = Dm.num(sample_cached_moles[gas_id]);
            float abs_delta = abs(gas_moles - sample_moles);
            if ((abs_delta > MINIMUM_MOLES_DELTA_TO_MOVE) && (abs_delta > gas_moles * MINIMUM_AIR_RATIO_TO_MOVE))
                return gas_id;
        }

        if (values_sum(cached_moles) > MINIMUM_MOLES_DELTA_TO_MOVE) //Don't consider temp if there's not enough mols
        {
            if (Dm.truthy(cmp_archive))
            {
                if (abs(temperature_archived - sample.temperature_archived) > MINIMUM_TEMPERATURE_DELTA_TO_SUSPEND)
                    return "temp";
            }
            else
            {
                if (abs(temperature - sample.temperature) > MINIMUM_TEMPERATURE_DELTA_TO_SUSPEND)
                    return "temp";
            }
        }

        return "";
    }

    ///Performs various reactions such as combustion and fabrication
    ///Returns: 1 if any reaction took place; 0 otherwise
    // tg: code/modules/atmospherics/gasmixtures/gas_mixture.dm:497
    public virtual async DmTask<object> react(datum holder = null)
    {
        object dot = NO_REACTION;
        DmList cached_moles = moles;
        if (!Dm.truthy(length(cached_moles)))
            return dot;

        DmList pre_formation = Dm.list();
        DmList mid_formation = Dm.list();
        DmList post_formation = Dm.list();
        DmList fires = Dm.list();
        DmList gas_reactions = SSair.gas_reactions;
        foreach (object gas_id in Dm.each(cached_moles))
        {
            DmList reaction_set = gas_reactions[gas_id] as DmList;
            if (!Dm.truthy(reaction_set))
                continue;
            Dm.append(ref pre_formation, reaction_set[1]);
            Dm.append(ref mid_formation, reaction_set[2]);
            Dm.append(ref post_formation, reaction_set[3]);
            Dm.append(ref fires, reaction_set[4]);
        }

        DmList reactions = Dm.add(Dm.add(Dm.add(pre_formation, mid_formation), post_formation), fires) as DmList;

        if (!Dm.truthy(length(reactions)))
            return dot;

        //Fuck you
        if (Dm.num(cached_moles[typeof(datum_gas_hypernoblium)]) >= REACTION_OPPRESSION_THRESHOLD && temperature > REACTION_OPPRESSION_MIN_TEMP)
            return STOP_REACTIONS;

        reaction_results = Dm.list();
        //It might be worth looking into updating these after each reaction, but that makes us care more about order of operations, so be careful
        float temp = temperature;
        foreach (datum_gas_reaction_standard reaction in Dm.each_cast<datum_gas_reaction_standard>(reactions))
        {
            DmList reqs = reaction.requirements;
            if ((Dm.truthy(reqs["MIN_TEMP"]) && temp < Dm.num(reqs["MIN_TEMP"])) || (Dm.truthy(reqs["MAX_TEMP"]) && temp > Dm.num(reqs["MAX_TEMP"])))
                continue;

            bool unmet = false;
            foreach ((object id, object value) in Dm.pairs(reqs))
            {
                if (Dm.eq(id, "MIN_TEMP") || Dm.eq(id, "MAX_TEMP"))
                    continue;
                if (Dm.num(cached_moles[id]) < Dm.num(value))
                {
                    unmet = true; // continue reaction_loop
                    break;
                }
            }
            if (unmet)
                continue;

            //at this point, all requirements for the reaction are satisfied. we can now react()
            dot = Dm.bitor(dot, await reaction.react(this, holder));
        }

        if (Dm.truthy(dot)) //If we changed the mix to any degree
        {
            garbage_collect();
            if (Dm.truthy(_listen_lookup?[COMSIG_GASMIX_REACTED]))
                _SendSignal(COMSIG_GASMIX_REACTED, Dm.list(this));
        }
        return dot;
    }

    // tg: code/modules/atmospherics/gasmixtures/gas_mixture.dm:562
    public virtual object get_breath_partial_pressure(object gas_mole_count = null)
    {
        return (Dm.num(gas_mole_count) * R_IDEAL_GAS_EQUATION * temperature) / BREATH_VOLUME;
    }

    // tg: code/modules/atmospherics/gasmixtures/gas_mixture.dm:572
    public virtual object gas_pressure_minimum_transfer(datum_gas_mixture output_air = null)
    {
        float resulting_energy = Dm.num(output_air.thermal_energy()) + (MOLAR_ACCURACY / Dm.num(total_moles()) * Dm.num(thermal_energy()));
        float resulting_capacity = Dm.num(output_air.heat_capacity()) + (MOLAR_ACCURACY / Dm.num(total_moles()) * Dm.num(heat_capacity()));
        return (Dm.num(output_air.total_moles()) + MOLAR_ACCURACY) * R_IDEAL_GAS_EQUATION * (resulting_energy / resulting_capacity) / Dm.num(output_air.volume);
    }

    // tg: code/modules/atmospherics/gasmixtures/gas_mixture.dm:584
    public virtual object gas_pressure_calculate(datum_gas_mixture output_air = null, object target_pressure = null, float? ignore_temperature = null)
    {
        ignore_temperature ??= FALSE;
        // So we don't need to iterate the gaslist multiple times.
        float our_moles = Dm.num(total_moles());
        float output_moles = Dm.num(output_air.total_moles());
        float output_pressure = Dm.num(output_air.return_pressure());

        if (our_moles <= 0 || temperature <= 0)
            return FALSE;

        float pressure_delta = 0;
        if (output_air.temperature <= 0 || output_moles <= 0)
        {
            ignore_temperature = TRUE;
            pressure_delta = Dm.num(target_pressure);
        }
        else
            pressure_delta = Dm.num(target_pressure) - output_pressure;

        if (pressure_delta < 0.01f || Dm.num(gas_pressure_minimum_transfer(output_air)) > Dm.num(target_pressure))
            return FALSE;

        if (Dm.truthy(ignore_temperature))
            return (pressure_delta * Dm.num(output_air.volume)) / (temperature * R_IDEAL_GAS_EQUATION);

        // Lower and upper bound for the moles we must transfer to reach the pressure. The answer is bound to be here somewhere.
        float pv = Dm.num(target_pressure) * Dm.num(output_air.volume);
        /// The PV/R part in the equation we will use later. Counted early because pv/(r*t) might not be equal to pv/r/t, messing our lower and upper limit.
        float pvr = pv / R_IDEAL_GAS_EQUATION;
        // These works by assuming our gas has extremely high heat capacity
        // and the resultant gasmix will hit either the highest or lowest temperature possible.

        /// This is the true lower limit, but numbers still can get lower than this due to floats.
        float lower_limit = max((pvr / max(temperature, output_air.temperature)) - output_moles, 0);
        float upper_limit = (pvr / min(temperature, output_air.temperature)) - output_moles; // In theory this should never go below zero, the pressure_delta check above should account for this.

        lower_limit = max(lower_limit - ATMOS_PRESSURE_ERROR_TOLERANCE, 0);
        upper_limit += ATMOS_PRESSURE_ERROR_TOLERANCE;

        /*
         * We have PV=nRT as a nice formula, we can rearrange it into nT = PV/R
         * But now both n and T can change, since any incoming moles also change our temperature.
         * So we need to unify both our n and T, somehow.
         *
         * We can rewrite T as (our old thermal energy + incoming thermal energy) divided by (our old heat capacity + incoming heat capacity)
         * T = (W1 + n/N2 * W2) / (C1 + n/N2 * C2). C being heat capacity, W being work, N being total moles.
         *
         * In total we now have our equation be: (N1 + n) * (W1 + n/N2 * W2) / (C1 + n/N2 * C2) = PV/R
         * Now you can rearrange this and find out that it's a quadratic equation and pretty much solvable with the formula. Will be a bit messy though.
         *
         * W2/N2n^2 +
         * (N1*W2/N2)n + W1n - ((PV/R)*C2/N2)n +
         * (-(PV/R)*C1) + N1W1 = 0
         *
         * We will represent each of these terms with A, B, and C. A for the n^2 part, B for the n^1 part, and C for the n^0 part.
         * We then put this into the famous (-b +/- sqrt(b^2-4ac)) / 2a formula.
         *
         * Oh, and one more thing. By "our" we mean the gasmix in the argument. We are the incoming one here. We are number 2, target is number 1.
         * If all this counting fucks up, we revert first to Newton's approximation, then the old simple formula.
         */

        // Our thermal energy and moles
        float w2 = Dm.num(thermal_energy());
        float n2 = our_moles;
        float c2 = Dm.num(heat_capacity());

        // Target thermal energy and moles
        float w1 = Dm.num(output_air.thermal_energy());
        float n1 = output_moles;
        float c1 = Dm.num(output_air.heat_capacity());

        /// x^2 in the quadratic
        float a_value = w2 / n2;
        /// x^1 in the quadratic
        float b_value = ((n1 * w2) / n2) + w1 - (pvr * c2 / n2);
        /// x^0 in the quadratic
        float c_value = (-1 * pvr * c1) + n1 * w1;

        object dot = gas_pressure_quadratic(a_value, b_value, c_value, lower_limit, upper_limit);
        if (Dm.truthy(dot))
            return dot;
        dot = gas_pressure_approximate(a_value, b_value, c_value, lower_limit, upper_limit);
        if (Dm.truthy(dot))
            return dot;
        // Inaccurate and will probably explode but whatever.
        return (pressure_delta * Dm.num(output_air.volume)) / (temperature * R_IDEAL_GAS_EQUATION);
    }

    /// Actually tries to solve the quadratic equation.
    /// Do mind that the numbers can get very big and might hit BYOND's single point float limit.
    // tg: code/modules/atmospherics/gasmixtures/gas_mixture.dm:670
    public virtual object gas_pressure_quadratic(object a = null, object b = null, object c = null, object lower_limit = null, object upper_limit = null)
    {
        object solution = null;
        if (IS_FINITE(a) && IS_FINITE(b) && IS_FINITE(c))
        {
            solution = max(SolveQuadratic(a, b, c));
            if (Dm.gt(solution, lower_limit) && Dm.lt(solution, upper_limit)) //SolveQuadratic can return empty lists so be careful here
                return solution;
        }
        stack_trace(Dm.text("Failed to solve pressure quadratic equation. A: []. B: []. C:[]. Current value = []. Expected lower limit: []. Expected upper limit: [].", a, b, c, solution, lower_limit, upper_limit), "code/modules/atmospherics/gasmixtures/gas_mixture.dm", 676);
        return FALSE;
    }

    /// Approximation of the quadratic equation using Newton-Raphson's Method.
    /// We use the slope of an approximate value to get closer to the root of a given equation.
    // tg: code/modules/atmospherics/gasmixtures/gas_mixture.dm:681
    public virtual object gas_pressure_approximate(object a = null, object b = null, object c = null, object lower_limit = null, object upper_limit = null)
    {
        object solution = null;
        if (IS_FINITE(a) && IS_FINITE(b) && IS_FINITE(c))
        {
            float fa = Dm.num(a), fb = Dm.num(b), fc = Dm.num(c);
            // We start at the extrema of the equation, added by a number.
            // This way we will hopefully always converge on the positive root, while starting at a reasonable number.
            float sol = (-fb / (2 * fa)) + 200;
            solution = sol;
            for (float iteration = 1; iteration <= ATMOS_PRESSURE_APPROXIMATION_ITERATIONS; iteration++)
            {
                float diff = (fa * Dm.pow(sol, 2) + fb * sol + fc) / (2 * fa * sol + fb); // f(sol) / f'(sol)
                sol -= diff; // xn+1 = xn - f(sol) / f'(sol)
                solution = sol;
                if (abs(diff) < MOLAR_ACCURACY && (sol > Dm.num(lower_limit)) && (sol < Dm.num(upper_limit)))
                    return solution;
            }
        }
        stack_trace(Dm.text("Newton's Approximation for pressure failed after [] iterations. A: []. B: []. C:[]. Current value: []. Expected lower limit: []. Expected upper limit: [].", ATMOS_PRESSURE_APPROXIMATION_ITERATIONS, a, b, c, solution, lower_limit, upper_limit), "code/modules/atmospherics/gasmixtures/gas_mixture.dm", 692);
        return FALSE;
    }

    /// Pumps gas from src to output_air. Amount depends on target_pressure
    // tg: code/modules/atmospherics/gasmixtures/gas_mixture.dm:696
    public virtual object pump_gas_to(datum_gas_mixture output_air = null, object target_pressure = null, object specific_gas = null, datum_gas_mixture output_pipenet_air = null)
    {
        datum_gas_mixture input_air = Dm.truthy(specific_gas) ? remove_specific_ratio(specific_gas, 1) as datum_gas_mixture : this;
        float temperature_delta = abs(input_air.temperature - output_air.temperature);
        datum_gas_mixture removed;

        float transfer_moles_output = Dm.num(input_air.gas_pressure_calculate(output_air, target_pressure, temperature_delta <= 5 ? TRUE : FALSE));
        float transfer_moles_pipenet = Dm.truthy(output_pipenet_air?.volume) ? Dm.num(input_air.gas_pressure_calculate(output_pipenet_air, target_pressure, temperature_delta <= 5 ? TRUE : FALSE)) : 0;
        float transfer_moles = max(transfer_moles_output, transfer_moles_pipenet);

        if (Dm.truthy(specific_gas))
        {
            removed = input_air.remove_specific(specific_gas, transfer_moles) as datum_gas_mixture;
            merge(input_air); // Merge the remaining gas back to the input node
        }
        else
            removed = input_air.remove(transfer_moles) as datum_gas_mixture;

        if (removed == null)
            return FALSE;

        output_air.merge(removed);
        return removed;
    }

    /// Releases gas from src to output air. This means that it can not transfer air to gas mixture with higher pressure.
    // tg: code/modules/atmospherics/gasmixtures/gas_mixture.dm:718
    public virtual object release_gas_to(datum_gas_mixture output_air = null, object target_pressure = null, float? rate = null, datum_gas_mixture output_pipenet_air = null)
    {
        rate ??= 1;
        float output_starting_pressure = Dm.num(output_air.return_pressure());
        float input_starting_pressure = Dm.num(return_pressure());

        //Need at least 10 KPa difference to overcome friction in the mechanism
        if (output_starting_pressure >= min(Dm.num(target_pressure), input_starting_pressure - 10))
            return FALSE;
        //Can not have a pressure delta that would cause output_pressure > input_pressure
        target_pressure = output_starting_pressure + min(Dm.num(target_pressure) - output_starting_pressure, (input_starting_pressure - output_starting_pressure) / 2);
        float temperature_delta = abs(temperature - output_air.temperature);

        float transfer_moles_output = Dm.num(gas_pressure_calculate(output_air, target_pressure, temperature_delta <= 5 ? TRUE : FALSE));
        float transfer_moles_pipenet = Dm.truthy(output_pipenet_air?.volume) ? Dm.num(gas_pressure_calculate(output_pipenet_air, target_pressure, temperature_delta <= 5 ? TRUE : FALSE)) : 0;
        float transfer_moles = max(transfer_moles_output, transfer_moles_pipenet);

        //Actually transfer the gas
        datum_gas_mixture removed = remove(transfer_moles * rate.Value) as datum_gas_mixture;

        if (removed == null)
            return FALSE;

        output_air.merge(removed);
        return TRUE;
    }

    // tg: code/modules/atmospherics/gasmixtures/gas_mixture.dm:748
    public virtual void electrolyze(float? working_power = null, object electrolyzer_args = null)
    {
        working_power ??= 0;
        electrolyzer_args ??= Dm.list();
        foreach (object reaction in Dm.each(GLOB.electrolyzer_reactions))
        {
            datum_gas_reaction_electrolyzer current_reaction = GLOB.electrolyzer_reactions[reaction] as datum_gas_reaction_electrolyzer;

            if (!Dm.truthy(current_reaction.reaction_check(air_mixture: this, electrolyzer_args: electrolyzer_args as DmList)))
                continue;

            current_reaction.react(air_mixture: this, working_power: working_power, electrolyzer_args: electrolyzer_args as DmList);
        }

        garbage_collect();
    }

    // tg: code/modules/atmospherics/gasmixtures/gas_mixture.dm:761
    public virtual object to_string()
    {
        Dm.NotTranslated("code/modules/atmospherics/gasmixtures/gas_mixture.dm:761", "to_string: not in M2 (M4b share/react/pumps)");
        return default;
    }

    /**
     * A simple helper proc that checks if the contents of a list of gases are within acceptable terms.
     *
     * Arguments:
     * * acceptable_gas_bounds: An associated list of gas types and acceptable boundaries in moles. e.g. /datum/gas/oxygen = list(16, 30)
     * * * if the assoc list is null, then it'll be considered a safe gas and won't return FALSE.
     * * extraneous_gas_limit: If a gas not in gases is found, this is the limit above which the proc will return FALSE.
     *
     * Returns TRUE if the list of gases is acceptable, FALSE otherwise.
     */
    // tg: code/modules/atmospherics/gasmixtures/gas_mixture.dm:790
    [ShouldBePure]
    public virtual object check_gases(DmList acceptable_gas_bounds = null, float? extraneous_gas_limit = null)
    {
        extraneous_gas_limit ??= 0.1f;
        DmList gases_to_check = acceptable_gas_bounds.Copy(); // thank you spaceman
        DmList cached_moles = moles;
        foreach ((object id, object gas_moles) in Dm.pairs(cached_moles))
        {
            if (!Dm.@in(id, gases_to_check))
            {
                if (Dm.num(gas_moles) > extraneous_gas_limit)
                    return FALSE;
                continue;
            }
            DmList boundaries = gases_to_check[id] as DmList;
            // ISINRANGE(gas_moles, boundaries[1], boundaries[2]) (__DEFINES/maths.dm:89), expanded inline (rules 11.1 (e))
            if (boundaries != null && !(Dm.num(boundaries[1]) <= Dm.num(gas_moles) && Dm.num(gas_moles) <= Dm.num(boundaries[2])))
                return FALSE;
            Dm.remove(ref gases_to_check, id);
        }
        ///Check that gases absent from the turf have a lower boundary of zero or none at all, otherwise return FALSE
        foreach (object id in Dm.each(gases_to_check))
        {
            DmList boundaries = gases_to_check[id] as DmList;
            if (boundaries != null && Dm.num(boundaries[1]) > 0)
                return FALSE;
        }
        return TRUE;
    }
}
