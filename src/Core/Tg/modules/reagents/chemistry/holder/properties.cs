// Translated from tgstation (snapshot 1d99bd7f): code/modules/reagents/chemistry/holder/properties.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M4a P4a: get_reagent_amount, heat_capacity and set_temperature, the welder's fuel amount and add_reagent's
// temperature equalisation; M8 slice A the rest).

// tg: code/modules/reagents/chemistry/holder/properties.dm:12
public partial class datum_reagents : datum
{
    //============================VOLUME======================================
    /// Is this holder full or not
    // tg: code/modules/reagents/chemistry/holder/properties.dm:3
    public virtual object holder_full()
    {
        return total_volume >= maximum_volume ? TRUE : FALSE;
    }

    /**
     * Get the amount of this reagent or the sum of all its subtypes if specified
     * Arguments
     * * [reagent][datum/reagent] - the typepath of the reagent to look for
     * * type_check - see defines under reagents.dm file
     */
    // tg: code/modules/reagents/chemistry/holder/properties.dm:12
    public virtual object get_reagent_amount(object reagent = null, float? type_check = null)
    {
        type_check ??= REAGENT_STRICT_TYPE;
        if (!ispath(reagent))
        {
            stack_trace(Dm.text("invalid path passed to get_reagent_amount []", reagent), "code/modules/reagents/chemistry/holder/properties.dm", 14);
            return 0;
        }
        DmList cached_reagents = reagent_list;

        float total_amount = 0;
        foreach (datum_reagent cached_reagent in Dm.each_cast<datum_reagent>(cached_reagents))
        {
            object sw__1 = type_check;
            if (Dm.eq(sw__1, REAGENT_STRICT_TYPE))
            {
                if (!Dm.eq(cached_reagent.type, reagent))
                    continue;
            }
            else if (Dm.eq(sw__1, REAGENT_PARENT_TYPE)) //to simulate typesof() which returns the type and then child types
            {
                if (!Dm.eq(cached_reagent.type, reagent) && !Dm.eq(cached_reagent.type.BaseType, reagent))
                    continue;
            }
            else
            {
                if (!istype(cached_reagent, reagent as Type))
                    continue;
            }

            total_amount += Dm.num(cached_reagent.volume);

            //short cut to break when we have found our one exact type
            if (Dm.eq(type_check, REAGENT_STRICT_TYPE))
                break;
        }

        return total_amount;
    }

    //======================PH(clamped between 0->14)========================================
    /*
    * Adjusts the base pH of all of the reagents in a beaker
    *
    * - moves it towards acidic
    * + moves it towards basic
    * Arguments:
    * * value - How much to adjust the base pH by
    */
    // tg: code/modules/reagents/chemistry/holder/properties.dm:49
    public virtual void adjust_all_reagents_ph(object value = null)
    {
        foreach (datum_reagent reagent in Dm.each_cast<datum_reagent>(reagent_list))
            reagent.ph = clamp(reagent.ph + Dm.num(value), CHEMICAL_MIN_PH, CHEMICAL_MAX_PH);
    }

    /*
    * Adjusts the base pH of a specific type
    *
    * - moves it towards acidic
    * + moves it towards basic
    * Arguments:
    * * input_reagent - type path of the reagent
    * * value - How much to adjust the base pH by
    */
    // tg: code/modules/reagents/chemistry/holder/properties.dm:62
    public virtual object adjust_specific_reagent_ph(object input_reagent = null, object value = null)
    {
        datum_reagent reagent = has_reagent(input_reagent) as datum_reagent;
        if (reagent == null) //We can call this with missing reagents.
            return FALSE;
        reagent.ph = clamp(reagent.ph + Dm.num(value), CHEMICAL_MIN_PH, CHEMICAL_MAX_PH);
        return null;
    }

    //==========================TEMPERATURE======================================
    /// Returns the total heat capacity for all of the reagents currently in this holder.
    // tg: code/modules/reagents/chemistry/holder/properties.dm:71
    public virtual object heat_capacity()
    {
        object dot = null;
        dot = 0;
        DmList cached_reagents = reagent_list; //cache reagents
        foreach (datum_reagent reagent in Dm.each<datum_reagent>(cached_reagents))
            dot = Dm.num(dot) + reagent.specific_heat * Dm.num(reagent.volume);
        return dot;
    }

    /**
     * Adjusts the thermal energy of the reagents in this holder by an amount.
     *
     * Arguments:
     * - delta_energy: The amount to change the thermal energy by.
     * - min_temp: The minimum temperature that can be reached.
     * - max_temp: The maximum temperature that can be reached.
     */
    // tg: code/modules/reagents/chemistry/holder/properties.dm:84
    public virtual void adjust_thermal_energy(object delta_energy = null, float? min_temp = null, float? max_temp = null)
    {
        min_temp ??= 2.7f;
        max_temp ??= 1000;
        object heat_capacity = this.heat_capacity();
        if (!Dm.truthy(heat_capacity))
            return; // no div/0 please
        set_temperature(clamp(chem_temp + Dm.num(Dm.div(delta_energy, heat_capacity)), min_temp.Value, max_temp.Value));
    }

    /** Sets the temperature of this reagent container to a new value.
     *
     * Handles setter signals.
     *
     * Arguments:
     * - _temperature: The new temperature value.
     */
    // tg: code/modules/reagents/chemistry/holder/properties.dm:97
    public virtual object set_temperature(object _temperature = null)
    {
        object dot = null;
        if (Dm.eq(_temperature, chem_temp))
            return dot;

        dot = chem_temp;
        chem_temp = clamp(Dm.num(_temperature), 0, CHEMICAL_MAXIMUM_TEMPERATURE);
        if (Dm.truthy(_listen_lookup?[COMSIG_REAGENTS_TEMP_CHANGE]))
            _SendSignal(COMSIG_REAGENTS_TEMP_CHANGE, Dm.list(this, _temperature, dot));
        return dot;
    }

    //==============================PURITY==========================================
    /**
     * Get the purity of this reagent
     * Arguments
     * * [reagent][datum/reagent] - the typepath of the specific reagent to get purity of
     */
    // tg: code/modules/reagents/chemistry/holder/properties.dm:111
    public virtual object get_reagent_purity(object reagent = null)
    {
        if (!ispath(reagent))
        {
            stack_trace(Dm.text("invalid reagent typepath passed to get_reagent_purity []", reagent), "code/modules/reagents/chemistry/holder/properties.dm", 113);
            return 0;
        }

        DmList cached_reagents = reagent_list;
        foreach (datum_reagent cached_reagent in Dm.each_cast<datum_reagent>(cached_reagents))
        {
            if (Dm.eq(cached_reagent.type, reagent))
                return round(cached_reagent.purity, 0.01f);
        }

        return 0;
    }

    /**
     * Get the average purity of all reagents (or all subtypes of provided typepath)
     * Arguments
     * * [parent_type][datum/reagent] - the typepath of specific reagents to look for
     */
    // tg: code/modules/reagents/chemistry/holder/properties.dm:128
    public virtual object get_average_purity(object parent_type = null)
    {
        if (parent_type != null && !ispath(parent_type))
        {
            stack_trace(Dm.text("illegal path passed to get_average_purity []", parent_type), "code/modules/reagents/chemistry/holder/properties.dm", 130);
            return FALSE;
        }

        object total_amount = null;
        object weighted_purity = null;
        DmList cached_reagents = reagent_list;
        foreach (datum_reagent reagent in Dm.each_cast<datum_reagent>(cached_reagents))
        {
            if (parent_type != null && !istype(reagent, parent_type as Type))
                continue;
            total_amount = Dm.num(total_amount) + Dm.num(reagent.volume);
            weighted_purity = Dm.num(weighted_purity) + Dm.num(reagent.volume) * reagent.purity;
        }

        return Dm.div(weighted_purity, total_amount);
    }

    /**
     * Directly set the purity of all contained reagents to a new value
     * Arguments
     * * new_purity - the new purity value
     */
    // tg: code/modules/reagents/chemistry/holder/properties.dm:149
    public virtual void set_all_reagents_purity(float? new_purity = null)
    {
        new_purity ??= 0;
        DmList cached_reagents = reagent_list;
        foreach (datum_reagent cached_reagent in Dm.each_cast<datum_reagent>(cached_reagents))
            cached_reagent.purity = max(0, new_purity.Value);
    }

    //================================TASTE===================================================
    /**
     * Returns what this holder's reagents taste like
     *
     * Arguments:
     * * mob/living/taster - who is doing the tasting. Some mobs can pick up specific flavours.
     * * minimum_percent - the lower the minimum percent, the more sensitive the message is.
     */
    // tg: code/modules/reagents/chemistry/holder/properties.dm:163
    public virtual object generate_taste_message(mob_living taster = null, object minimum_percent = null)
    {
        return generate_reagents_taste_message(reagent_list, taster, minimum_percent);
    }
}
