// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/atmospherics.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7 slice T3 (D164): the whole file (the gas analyzer's window reads the gas mixture parser and the atmos handbooks).
// GLOBAL_LIST_EMPTY(reaction_handbook) and GLOBAL_LIST_EMPTY(gas_handbook) (:64-65) are the generated vars of GLOB.

public partial class datum_controller_global_vars
{
    // tg: code/__HELPERS/atmospherics.dm:64  GLOBAL_LIST_EMPTY(reaction_handbook)
    public DmList reaction_handbook;

    // tg: code/__HELPERS/atmospherics.dm:65  GLOBAL_LIST_EMPTY(gas_handbook)
    public DmList gas_handbook;

    // tg: code/__HELPERS/atmospherics.dm:64  GLOBAL_LIST_EMPTY(reaction_handbook): GLOBAL_MANAGED (__DEFINES/__globals.dm:5-9)
    public virtual void InitGlobalreaction_handbook()
    {
        reaction_handbook = Dm.list();
        Dm.append(ref gvars_datum_init_order, nameof(reaction_handbook));
    }

    // tg: code/__HELPERS/atmospherics.dm:65  GLOBAL_LIST_EMPTY(gas_handbook): GLOBAL_MANAGED (__DEFINES/__globals.dm:5-9)
    public virtual void InitGlobalgas_handbook()
    {
        gas_handbook = Dm.list();
        Dm.append(ref gvars_datum_init_order, nameof(gas_handbook));
    }
}

public static partial class GlobalProcs
{
    // tg: code/__HELPERS/atmospherics.dm:1
    public static object molar_cmp_less_than(object a = null, object b = null, object epsilon = null)
    {
        epsilon ??= MAXIMUM_ERROR_GAS_REMOVAL;
        return Dm.num(Dm.lt(a, Dm.add(b, epsilon)));
    }

    // tg: code/__HELPERS/atmospherics.dm:4
    public static object molar_cmp_greater_than(object a = null, object b = null, object epsilon = null)
    {
        epsilon ??= MAXIMUM_ERROR_GAS_REMOVAL;
        return Dm.num(Dm.gt(Dm.add(a, epsilon), b));
    }

    // tg: code/__HELPERS/atmospherics.dm:7
    public static object molar_cmp_equals(object a = null, object b = null, object epsilon = null)
    {
        epsilon ??= MAXIMUM_ERROR_GAS_REMOVAL;
        return Dm.num(Dm.gt(Dm.add(a, epsilon), b) && Dm.lt(Dm.sub(a, epsilon), b));
    }

    /** A simple rudimentary gasmix to information list converter. Can be used for UIs.
     * Args:
     * * gasmix: [/datum/gas_mixture]
     * * name: String used to name the list, optional.
     * Returns: A list parsed_gasmixes with the following structure:
     * - parsed_gasmixes    Value: Assoc List     Desc: The thing we return
     * -- Key: name         Value: String         Desc: Gasmix Name
     * -- Key: temperature  Value: Number         Desc: Temperature in kelvins
     * -- Key: volume       Value: Number         Desc: Volume in liters
     * -- Key: pressure     Value: Number         Desc: Pressure in kPa
     * -- Key: ref          Value: String         Desc: The reference for the instantiated gasmix.
     * -- Key: gases        Value: Numbered list  Desc: List of gasses in our gasmix
     * --- Key: 1           Value: String         Desc: gas id var from the gas
     * --- Key: 2           Value: String         Desc: Human readable gas name.
     * --- Key: 3           Value: Number         Desc: Mol amount of the gas.
     * -- Key: gases        Value: Numbered list  Desc: Assoc list of reactions that occur inside.
     * --- Key: 1           Value: String         Desc: reaction id var from the gas.
     * --- Key: 2           Value: String         Desc: Human readable reaction name.
     * --- Key: 3           Value: Number         Desc: The number associated with the reaction.
     * Returned list should always be filled with keys even if value are nulls.
     */
    // tg: code/__HELPERS/atmospherics.dm:31
    public static object gas_mixture_parser(datum_gas_mixture gasmix = null, object name = null)
    {
        DmList dot = null;
        dot = Dm.list(
            Dm.kv("gases", Dm.list()),
            Dm.kv("reactions", Dm.list()),
            Dm.kv("name", format_text(name)),
            Dm.kv("total_moles", null),
            Dm.kv("temperature", null),
            Dm.kv("volume", null),
            Dm.kv("pressure", null),
            Dm.kv("reference", null)
        );
        if (!Dm.truthy(gasmix))
            return dot;
        DmList cached_gas_id = (DmList)GLOB.meta_gas_info[META_GAS_ID]; // GAS_META[META_GAS_ID]
        DmList cached_gas_name = (DmList)GLOB.meta_gas_info[META_GAS_NAME]; // GAS_META[META_GAS_NAME]
        foreach ((object gas_path, object amount) in Dm.pairs(gasmix.moles))
        {
            Dm.append_at(dot, "gases", Dm.list(Dm.list(
                cached_gas_id[gas_path],
                cached_gas_name[gas_path],
                amount
            )));
        }
        foreach (object reaction_result in Dm.each(gasmix.reaction_results))
        {
            Dm.append_at(dot, "reactions", Dm.list(Dm.list(
                Dm.initial((Type)reaction_result, (datum_gas_reaction_standard p) => p.id),
                Dm.initial((Type)reaction_result, (datum_gas_reaction_standard p) => p.name),
                gasmix.reaction_results[reaction_result]
            )));
        }
        dot["total_moles"] = gasmix.total_moles();
        dot["temperature"] = gasmix.temperature;
        dot["volume"] = gasmix.volume;
        dot["pressure"] = gasmix.return_pressure();
        dot["reference"] = REF(gasmix);
        return dot;
    }

    /// Automatically populates gas_handbook and reaction_handbook. They are formatted lists containing information regarding gases and reactions they participate in.
    /// Structure can be found in TS form at AtmosHandbook.tsx
    // tg: code/__HELPERS/atmospherics.dm:69
    public static void atmos_handbooks_init()
    {
        if (Dm.truthy(length(GLOB.reaction_handbook)))
            GLOB.reaction_handbook = Dm.list();
        if (Dm.truthy(length(GLOB.gas_handbook)))
            GLOB.gas_handbook = Dm.list();

        /// Final product is a numbered list, this one is assoc just so we can generate the "reactions" entry easily.
        DmList momentary_gas_list = Dm.list();

        foreach (Type gas_path in Dm.each_cast<Type>(subtypesof(typeof(datum_gas))))
        {
            DmList gas_info = Dm.list();
            DmList meta_information = GLOB.meta_gas_info;
            if (!Dm.truthy(meta_information[META_GAS_ID]))
                continue;
            gas_info["id"] = ((DmList)meta_information[META_GAS_ID])[gas_path];
            gas_info["name"] = ((DmList)meta_information[META_GAS_NAME])[gas_path];
            gas_info["description"] = ((DmList)meta_information[META_GAS_DESC])[gas_path];
            gas_info["specific_heat"] = ((DmList)meta_information[META_GAS_SPECIFIC_HEAT])[gas_path];
            gas_info["export_value"] = Dm.truthy(Dm.bitand(Dm.initial(gas_path, (datum_gas p) => p.cargo_flags), GAS_EXPORTABLE)) ? Dm.initial(gas_path, (datum_gas p) => p.base_value) : 0;
            gas_info["reactions"] = Dm.list();
            momentary_gas_list[gas_path] = gas_info;
        }

        foreach (Type reaction_path in Dm.each_cast<Type>(valid_subtypesof(typeof(datum_gas_reaction))))
        {
            datum_gas_reaction reaction = Dm.@new(reaction_path) as datum_gas_reaction; // tg types the loop var /datum/gas_reaction/standard but the list holds every reaction, the electrolyzer's too
            DmList reaction_info = Dm.list();
            reaction_info["id"] = reaction.id;
            reaction_info["name"] = reaction.name;
            reaction_info["description"] = reaction.desc;
            reaction_info["factors"] = Dm.list();
            foreach (object factor in Dm.each(reaction.factor))
            {
                DmList factor_info = Dm.list();
                factor_info["desc"] = reaction.factor[factor];

                if (Dm.@in(factor, momentary_gas_list))
                {
                    DmList factor_gas = (DmList)momentary_gas_list[factor];
                    Dm.append_at((DmList)factor_gas, "reactions", Dm.list(Dm.kv(reaction.id, reaction.name)));
                    factor_info["factor_id"] = factor_gas["id"]; //Gas id
                    factor_info["factor_type"] = "gas";
                    factor_info["factor_name"] = factor_gas["name"]; //Common name
                }
                else
                {
                    factor_info["factor_name"] = factor;
                    factor_info["factor_type"] = "misc";
                    if (Dm.eq(factor, "Temperature") || Dm.eq(factor, "Pressure"))
                        factor_info["tooltip"] = Dm.text("Reaction is influenced by the [] of the place where the reaction is occurring.", LOWER_TEXT(factor));
                    else if (Dm.eq(factor, "Energy"))
                        factor_info["tooltip"] = "Energy released by the reaction. May or may not result in linear temperature change depending on a slew of other factors.";
                    else if (Dm.eq(factor, "Radiation"))
                        factor_info["tooltip"] = "This reaction emits hazardous radiation - take precautions.";
                    else if (Dm.eq(factor, "Location"))
                        factor_info["tooltip"] = "This reaction has special behaviour when occurring in specific locations.";
                    else if (Dm.eq(factor, "Hot Ice"))
                        factor_info["tooltip"] = "Hot ice are solidified stacks of plasma. Ignition of one will result in a raging fire.";
                }
                Dm.append((DmList)reaction_info["factors"], Dm.list(factor_info));
            }
            Dm.append(GLOB.reaction_handbook, Dm.list(reaction_info));
            qdel(reaction);
        }

        foreach (object gas_info_index in Dm.each(momentary_gas_list))
            Dm.append(GLOB.gas_handbook, Dm.list(momentary_gas_list[gas_info_index]));
    }

    /// Returns an assoc list of the gas handbook and the reaction handbook.
    /// For UIs, simply do data += return_atmos_handbooks() to use.
    // tg: code/__HELPERS/atmospherics.dm:129
    public static object return_atmos_handbooks()
    {
        return Dm.list(Dm.kv("gasInfo", GLOB.gas_handbook), Dm.kv("reactionInfo", GLOB.reaction_handbook), Dm.kv("moneySymbol", MONEY_SYMBOL), Dm.kv("moneyName", MONEY_NAME));
    }

    // tg: code/__HELPERS/atmospherics.dm:132
    public static object extract_id_tags(object objects = null)
    {
        DmList tags = Dm.list();

        foreach (obj @object in Dm.each<obj>(objects))
            Dm.append(tags, @object.id_tag);

        return tags;
    }

    // tg: code/__HELPERS/atmospherics.dm:140
    public static object find_by_id_tag(object objects = null, object id_tag = null)
    {
        foreach (obj @object in Dm.each<obj>(objects))
        {
            if (Dm.eq(@object.id_tag, id_tag))
                return @object;
        }

        return null;
    }

    // tg: code/__HELPERS/atmospherics.dm:147
    public static object print_gas_mixture(datum_gas_mixture gas_mixture = null)
    {
        string message = Dm.text("TEMPERATURE: []K, QUANTITY: [] mols, VOLUME: []L; ", gas_mixture.temperature, gas_mixture.total_moles(), gas_mixture.volume);
        DmList cached_gas_id = (DmList)GLOB.meta_gas_info[META_GAS_ID]; // GAS_META[META_GAS_ID]
        foreach ((object gas_id, object amount) in Dm.pairs(gas_mixture.moles))
            message += Dm.text("[]=[] mols;", cached_gas_id[gas_id], amount);
        return message;
    }
}
