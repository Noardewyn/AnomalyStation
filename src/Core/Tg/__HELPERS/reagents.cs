// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/reagents.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: the type /datum/reagents (its [DmPath]: reagents.dm is the earliest .dme file that declares it, rules 2.3), which
// /atom/proc/expose_reagents names (wave 3, the atoms package), and get_chem_id (M8 slice A: the dev verb reagent takes a
// reagent's name), get_random_reagent_id (M8 slice C), METABOLIZE_FREE_CONSTANT (M8 slice B: the coagulant and the toxins),
// chem_recipes_do_conflict and create_foam (gaps F1: the reaction lists and the foam reactions), convert_ph_to_readable_color
// and get_reagent_type_from_product_string (gaps P5: the chemistry windows and the reaction lookup), reagent_paths_list_to_text (gaps H2: the ice
// cream vat's ingredients), CONVERT_PH_TO_COLOR (gaps RG3: the universal indicator).

// tg: code/__HELPERS/reagents.dm:79
[DmPath("/datum/reagents")]
public partial class datum_reagents : datum
{
    //Creates foam from the reagent. Metaltype is for metal foam, notification is what to show people in textbox
    // tg: code/__HELPERS/reagents.dm:79
    public virtual async DmTask create_foam(Type foamtype = null, object foam_volume = null, Type result_type = null, object notification = null, float? log = null, object lifetime = null, object slippery = null)
    {
        log ??= FALSE;
        turf location = get_turf(my_atom);

        datum_effect_system_fluid_spread_foam foam = Dm.@new(foamtype, location, null, foam_volume, my_atom, this, result_type) as datum_effect_system_fluid_spread_foam; // carry = src, result_type = result_type
        await foam.start(log, lifetime, slippery); // log = log, lifetime = lifetime, slippery = slippery

        await clear_reagents();
        if (!Dm.truthy(notification))
            return;
        foreach (mob M in Dm.each<mob>(viewers(5, location)))
            to_chat(M, notification);
    }
}

public partial class datum_reagent : datum
{
    ///Helper to ensure a constant is applied regardless of reagent metabolization rate to get 100% effect
    ///Use for parent reagent types when subtypes alter metabolization rates
    // tg: code/__HELPERS/reagents.dm:3  #define METABOLIZE_FREE_CONSTANT(value)((value * REAGENTS_METABOLISM) / metabolization_rate)  (it reads src's
    // metabolization_rate: an instance member, rules 11.1; M8 slice B)
    public float METABOLIZE_FREE_CONSTANT(float value) => (value * REAGENTS_METABOLISM) / metabolization_rate;
}

public static partial class GlobalProcs
{
    // tg: code/__HELPERS/reagents.dm:5
    public static object chem_recipes_do_conflict(datum_chemical_reaction r1 = null, datum_chemical_reaction r2 = null)
    {
        //We have to check to see if either is competitive so can ignore it (competitive reagents are supposed to conflict)
        if (Dm.truthy(Dm.bitand(r1.reaction_flags, REACTION_COMPETITIVE)) || Dm.truthy(Dm.bitand(r2.reaction_flags, REACTION_COMPETITIVE)))
            return FALSE;

        //do the non-list tests first, because they are cheaper
        if (r1.is_cold_recipe != r2.is_cold_recipe)
        {
            datum_chemical_reaction cold_one = Dm.truthy(r1.is_cold_recipe) ? r1 : r2;
            datum_chemical_reaction warm_one = Dm.truthy(r1.is_cold_recipe) ? r2 : r1;
            if (cold_one.required_temp < warm_one.required_temp)
                //the range of temperatures does not overlap, so there is no conflict
                return FALSE;
        }

        //find the reactions with the shorter and longer required_reagents list
        datum_chemical_reaction long_req;
        datum_chemical_reaction short_req;
        if (r1.required_reagents.len > r2.required_reagents.len)
        {
            long_req = r1;
            short_req = r2;
        }
        else if (r1.required_reagents.len < r2.required_reagents.len)
        {
            long_req = r2;
            short_req = r1;
        }
        else
        {
            //if they are the same length, sort instead by the length of the catalyst list
            //this is important if the required_reagents lists are the same
            if (r1.required_catalysts.len > r2.required_catalysts.len)
            {
                long_req = r1;
                short_req = r2;
            }
            else
            {
                long_req = r2;
                short_req = r1;
            }
        }

        //if short one has a container requirement and it's not the same as the long one's, there's no conflict
        if (short_req.required_container != null && short_req.required_container != long_req.required_container)
            return FALSE;

        //if short one has a more extreme temperature requirement, there's no conflict
        if (short_req.is_cold_recipe == long_req.is_cold_recipe)
        {
            if (Dm.truthy(short_req.is_cold_recipe) && short_req.required_temp < long_req.required_temp)
                return FALSE;
            if (!Dm.truthy(short_req.is_cold_recipe) && short_req.required_temp > long_req.required_temp)
                return FALSE;
        }

        //check if the shorter reaction list is a subset of the longer one
        DmList reagent_overlap = Dm.bitand((object)long_req.required_reagents, short_req.required_reagents) as DmList;
        if (reagent_overlap.len != short_req.required_reagents.len)
            //there is at least one reagent in the short list that is not in the long list, so there is no conflict
            return FALSE;

        //check to see if the shorter reaction's catalyst list is also a subset of the longer reaction's catalyst list
        //if the longer reaction's catalyst list is a subset of the shorter ones, that is fine
        //if the reaction lists are the same, the short reaction will have the shorter required_catalysts list, so it will register as a conflict
        DmList catalyst_overlap = Dm.bitand((object)long_req.required_catalysts, short_req.required_catalysts) as DmList;
        if (catalyst_overlap.len != short_req.required_catalysts.len)
            //there is at least one unique catalyst for the short reaction, so there is no conflict
            return FALSE;

        //if the list of reagents&catalysts is exactly the same, don't bother comparing the amounts, just call it an overlap
        if (reagent_overlap.len == long_req.required_reagents.len && catalyst_overlap.len == long_req.required_catalysts.len)
            return TRUE;

        foreach (object ingredient in Dm.each(reagent_overlap))
        {
            //if longer reaction requires lower reagent quantity, there's no conflict
            if (Dm.num(reagent_overlap[ingredient]) < Dm.num(short_req.required_reagents[ingredient]))
                return FALSE;
        }
        foreach (object catalyst in Dm.each(catalyst_overlap))
        {
            //if longer reaction requires lower catalys quantity, there's no conflict
            if (Dm.num(catalyst_overlap[catalyst]) < Dm.num(short_req.required_catalysts[catalyst]))
                return FALSE;
        }

        //if we got this far, the longer reaction will be impossible to create if the shorter one is earlier in GLOB.chemical_reactions_list_reactant_index, and will require the reagents to be added in a particular order otherwise
        return TRUE;
    }

    // tg: code/__HELPERS/reagents.dm:177 (M8 slice C: the maintenance pills)
    public static object get_random_reagent_id(float? randomization_flags = null, DmList blacklist = null, DmList whitelist = null)
    {
        randomization_flags ??= REAGENT_SPAWN_RANDOM_PRODUCERS;
        blacklist ??= Dm.list();
        whitelist ??= subtypesof(typeof(datum_reagent)) as DmList;
        DmList reagent_list_to_process = Dm.list();
        foreach (Type reagent_path in Dm.each_cast<Type>(Dm.sub(whitelist, blacklist)))
        {
            if (Dm.truthy(Dm.bitand(Dm.initial(reagent_path, (datum_reagent p) => p.randomized_spawns), randomization_flags)))
                reagent_list_to_process.Add(reagent_path);
        }
        return pick(reagent_list_to_process);
    }

    // tg: code/__HELPERS/reagents.dm:185
    public static object get_chem_id(object chem_name = null)
    {
        // for(var/X in GLOB.chemical_reagents_list): GAP(lead): the list is not built (the holder's add_reagent GAP); its keys
        // are valid_subtypesof(/datum/reagent) in that order and each entry's name is its type's initial name.
        foreach (Type X in Dm.each_cast<Type>(valid_subtypesof(typeof(datum_reagent))))
        {
            if (ckey(chem_name) == ckey(LOWER_TEXT(Dm.initial(X, (datum_reagent R) => R.name))))
                return X;
        }
        return null;
    }
}

public static partial class GlobalProcs
{
    ///Converts the pH into a tgui readable color - i.e. white and black text is readable over it. This is NOT the colourwheel for pHes however.
    // tg: code/__HELPERS/reagents.dm:92
    public static object convert_ph_to_readable_color(object pH = null)
    {
        float ph = Dm.num(pH);
        if (ph <= 1)
            return "red";
        if (ph <= 2)
            return "orange";
        if (ph <= 3)
            return "average";
        if (ph <= 4)
            return "yellow";
        if (ph <= 5)
            return "olive";
        if (ph <= 6)
            return "good";
        if (ph <= 8)
            return "green";
        if (ph <= 9.5f)
            return "teal";
        if (ph <= 11)
            return "blue";
        if (ph <= 12.5f)
            return "violet";
        return "purple";
    }

    ///Returns a list of chemical_reaction datums that have the input STRING as a product
    // tg: code/__HELPERS/reagents.dm:153
    public static object get_reagent_type_from_product_string(object @string = null)
    {
        object input_reagent = replacetext(LOWER_TEXT(@string), " ", ""); //95% of the time, the reagent id is a lowercase/no spaces version of the name
        if (input_reagent == null)
            return null;

        DmList shortcuts = Dm.list(Dm.kv("meth", typeof(datum_reagent_drug_methamphetamine)));
        if (Dm.truthy(shortcuts[input_reagent]))
            input_reagent = shortcuts[input_reagent];
        else
            input_reagent = get_chem_id(input_reagent);
        return input_reagent;
    }
}

// tg: code/__HELPERS/reagents.dm:191 (gaps H2)
public static partial class GlobalProcs
{
    // tg: code/__HELPERS/reagents.dm:191
    public static string reagent_paths_list_to_text(DmList reagents = null, object addendum = null)
    {
        DmList temp = Dm.list();
        foreach (Type R in Dm.each<Type>(reagents))
            Dm.combine(ref temp, Dm.initial(R, (datum_reagent r) => r.name));
        if (Dm.truthy(addendum))
            Dm.append(ref temp, addendum);
        return Dm.jointext(temp, ", ");
    }
}

// tg: code/__HELPERS/reagents.dm:118 (gaps RG3, D339: the universal indicator)
public static partial class GlobalProcs
{
    ///Converts pH to universal indicator colours. This is the colorwheel for pHes
    // tg: code/__HELPERS/reagents.dm:118  #define CONVERT_PH_TO_COLOR(pH, color) (it assigns its second argument: by ref, rules 11.1)
    public static void CONVERT_PH_TO_COLOR(float pH, ref object color)
    {
        // switch(pH): the first matching range runs
        if (pH >= 14)
            color = "#462c83";
        else if (pH >= 13 && pH <= 14)
            color = "#63459b";
        else if (pH >= 12 && pH <= 13)
            color = "#5a51a2";
        else if (pH >= 11 && pH <= 12)
            color = "#3853a4";
        else if (pH >= 10 && pH <= 11)
            color = "#3f93cf";
        else if (pH >= 9 && pH <= 10)
            color = "#0bb9b7";
        else if (pH >= 8 && pH <= 9)
            color = "#23b36e";
        else if (pH >= 7 && pH <= 8)
            color = "#3aa651";
        else if (pH >= 6 && pH <= 7)
            color = "#4cb849";
        else if (pH >= 5 && pH <= 6)
            color = "#b5d335";
        else if (pH >= 4 && pH <= 5)
            color = "#f7ec1e";
        else if (pH >= 3 && pH <= 4)
            color = "#fbc314";
        else if (pH >= 2 && pH <= 3)
            color = "#f26724";
        else if (pH >= 1 && pH <= 2)
            color = "#ef1d26";
        else if (pH <= 1)
            color = "#c6040c";
    }
}
