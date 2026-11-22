// Translated from tgstation (snapshot 1d99bd7f): code/datums/armor/_armor.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M4a P2, at the lead's assignment: run_armor_check reads the worn items' armour as tg has them): the armour cache
// (GLOB.armor_by_type), get_armor_by_type, and the datum's tag, Destroy, get_rating and get_rating_list. M5 slice 1 (atom
// integrity): the armour arithmetic (generate_new_with_modifiers, _multipliers, _specific, add_other_armor,
// subtract_other_armor, has_any_armor, the /datum/armor/immune overrides, armor_to_protection_class and _name). Not
// declared: the admin procs vv_edit_var :58, can_vv_mark :61, vv_get_dropdown :64 and CanProcCall :68 (admin variable
// editing and proc calls, M10). The var blocks are generated.

/// Assosciative list of type -> armor. Used to ensure we always hold a reference to default armor datums
// tg: code/datums/armor/_armor.dm:2
public partial class datum_controller_global_vars
{
    // tg: code/datums/armor/_armor.dm:2  GLOBAL_LIST_INIT(armor_by_type, generate_armor_type_cache())
    public DmList armor_by_type;

    // tg: code/datums/armor/_armor.dm:2  (GLOBAL_MANAGED(armor_by_type, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalarmor_by_type()
    {
        armor_by_type = generate_armor_type_cache() as DmList;
        Dm.append(ref gvars_datum_init_order, "armor_by_type");
    }
}

// tg: code/datums/armor/_armor.dm:4 (global procs)
public static partial class GlobalProcs
{
    // tg: code/datums/armor/_armor.dm:4
    public static object generate_armor_type_cache()
    {
        DmList armor_cache = Dm.list();
        foreach (object armor_type__it in Dm.each(subtypesof(typeof(datum_armor))))
        {
            datum_armor armor_type = Dm.@new((Type)armor_type__it) as datum_armor;
            armor_cache[armor_type.GetType()] = armor_type;
            armor_type.GenerateTag();
        }
        return armor_cache;
    }

    /**
     * Gets an armor type datum using the given type by formatting it into the expected datum tag
     */
    // tg: code/datums/armor/_armor.dm:15
    public static object get_armor_by_type(object armor_type = null)
    {
        object armor = Dm.locate_ref(replacetext(Dm.text("[]", armor_type), "/", "-"));
        if (Dm.truthy(armor))
            return armor;
        if (Dm.eq(armor_type, typeof(datum_armor)))
            throw Dm.CRASH("Attempted to get the base armor type, you probably meant to use /datum/armor/none", "code/datums/armor/_armor.dm", 20);
        throw Dm.CRASH(Dm.text("Attempted to get an armor type that did not exist! '[]'", armor_type), "code/datums/armor/_armor.dm", 21);
    }
}

/**
 * The armor datum holds information about different types of armor that an atom can have.
 * It also contains logic and helpers for calculating damage and effective damage
 */
// tg: code/datums/armor/_armor.dm:27
[DmPath("/datum/armor")]
public partial class datum_armor : datum
{
    // tg: code/datums/armor/_armor.dm:46
    public override object Destroy(float? force = null)
    {
        if (!Dm.truthy(force) && Dm.truthy(tag))
            return QDEL_HINT_LETMELIVE;

        // something really wants us gone
        datum_flags = Dm.num(Dm.bitand(datum_flags, Dm.bitnot(DF_USE_TAG)));
        tag = null;
        return base.Destroy(force);
    }

    // tg: code/datums/armor/_armor.dm:54
    public override void GenerateTag()
    {
        base.GenerateTag();
        tag = replacetext(Dm.text("[]", GetType()), "/", "-");
    }

    // tg: code/datums/armor/_armor.dm:141
    public virtual object get_rating(object rating = null)
    {
        // its not that I don't trust coders, it's just that I don't trust coders
        if (!Dm.@in(rating, ARMOR_LIST_ALL__local()))
            throw Dm.CRASH(Dm.text("Attempted to get a rating '[]' that doesn't exist", rating), "code/datums/armor/_armor.dm", 144);
        return Dm.get_var(this, Dm.str(rating));
    }

    /// Generate a brand new armor datum with the modifiers given, if ARMOR_ALL is specified only that modifier is used
    // tg: code/datums/armor/_armor.dm:72
    public virtual object generate_new_with_modifiers(DmList modifiers = null)
    {
        datum_armor new_armor = Dm.@new<datum_armor>();

        DmList all_keys = ARMOR_LIST_ALL__local();
        if (Dm.@in(ARMOR_ALL, modifiers))
        {
            object modifier_all = modifiers[ARMOR_ALL];
            if (!Dm.truthy(modifier_all))
                return this;
            foreach (object mod in Dm.each(all_keys))
                Dm.set_var(new_armor, Dm.str(mod), Dm.add(Dm.get_var(this, Dm.str(mod)), modifier_all));
            return new_armor;
        }

        foreach (object modifier in Dm.each(modifiers))
        {
            if (Dm.@in(modifier, all_keys))
                Dm.set_var(new_armor, Dm.str(modifier), Dm.add(Dm.get_var(this, Dm.str(modifier)), modifiers[modifier]));
            else
                stack_trace(Dm.text("Attempt to call generate_new_with_modifiers with illegal modifier '[]'! Ignoring it", modifier), "code/datums/armor/_armor.dm", 88);
        }
        return new_armor;
    }

    /// Generate a brand new armor datum with the multiplier given, if ARMOR_ALL is specified only that modifer is used
    // tg: code/datums/armor/_armor.dm:95
    public virtual object generate_new_with_multipliers(DmList multipliers = null)
    {
        datum_armor new_armor = Dm.@new<datum_armor>();

        DmList all_keys = ARMOR_LIST_ALL__local();
        if (Dm.@in(ARMOR_ALL, multipliers))
        {
            object multiplier_all = multipliers[ARMOR_ALL];
            if (!Dm.truthy(multiplier_all))
                return this;
            foreach (object multiplier in Dm.each(all_keys))
                Dm.set_var(new_armor, Dm.str(multiplier), Dm.mul(Dm.get_var(this, Dm.str(multiplier)), multiplier_all));
            return new_armor;
        }

        foreach (object multiplier in Dm.each(multipliers))
        {
            if (Dm.@in(multiplier, all_keys))
                Dm.set_var(new_armor, Dm.str(multiplier), Dm.mul(Dm.get_var(this, Dm.str(multiplier)), multipliers[multiplier]));
            else
                stack_trace(Dm.text("Attempt to call generate_new_with_multipliers with illegal multiplier '[]'! Ignoring it", multiplier), "code/datums/armor/_armor.dm", 111);
        }
        return new_armor;
    }

    /// Generate a brand new armor datum with the values given, if a value is not present it carries over
    // tg: code/datums/armor/_armor.dm:118
    public virtual object generate_new_with_specific(DmList values = null)
    {
        datum_armor new_armor = Dm.@new<datum_armor>();

        DmList all_keys = ARMOR_LIST_ALL__local();
        if (Dm.@in(ARMOR_ALL, values))
        {
            object value_all = values[ARMOR_ALL];
            if (!Dm.truthy(value_all))
                return this;
            foreach (object mod in Dm.each(all_keys))
                Dm.set_var(new_armor, Dm.str(mod), value_all);
            return new_armor;
        }

        foreach (object armor_rating in Dm.each(all_keys))
        {
            if (Dm.@in(armor_rating, values))
                Dm.set_var(new_armor, Dm.str(armor_rating), values[armor_rating]);
            else
                Dm.set_var(new_armor, Dm.str(armor_rating), Dm.get_var(this, Dm.str(armor_rating)));
        }
        return new_armor;
    }

    /// Converts all the ratings of the armor into a list, optionally inverted
    // tg: code/datums/armor/_armor.dm:151
    public virtual object get_rating_list(float? inverse = null)
    {
        inverse ??= FALSE;
        DmList ratings = Dm.list();
        foreach (object rating in Dm.each(ARMOR_LIST_ALL__local()))
        {
            object value = Dm.get_var(this, Dm.str(rating));
            if (Dm.truthy(inverse))
                value = Dm.mul(value, -1);
            ratings[rating] = value;
        }
        return ratings;
    }

    /// Returns a new armor datum with the given armor added onto this one
    // tg: code/datums/armor/_armor.dm:167
    public virtual object add_other_armor(object other = null)
    {
        if (ispath(other))
            other = get_armor_by_type(other);
        return generate_new_with_modifiers(((datum_armor)other).get_rating_list() as DmList);
    }

    /// Returns a new armor datum with the given armor removed from this one
    // tg: code/datums/armor/_armor.dm:176
    public virtual object subtract_other_armor(object other = null)
    {
        if (ispath(other))
            other = get_armor_by_type(other);
        return generate_new_with_modifiers(((datum_armor)other).get_rating_list(inverse: TRUE) as DmList);
    }

    /// Checks if any of the armor values are non-zero, so this technically also counts negative armor!
    // tg: code/datums/armor/_armor.dm:185
    public virtual object has_any_armor()
    {
        foreach (object rating in Dm.each(ARMOR_LIST_ALL__local()))
        {
            if (Dm.truthy(Dm.get_var(this, Dm.str(rating))))
                return TRUE;
        }
        return FALSE;
    }

    // tg: code/__DEFINES/combat.dm:51  #define ARMOR_LIST_ALL list(ACID, BIO, BOMB, BULLET, CONSUME, ENERGY, FIRE, LASER, MELEE,
    // WOUND). GAP(lead): no Defines member yet: a local copy, built per use (D31).
    internal static DmList ARMOR_LIST_ALL__local() => Dm.list(ACID, BIO, BOMB, BULLET, CONSUME, ENERGY, FIRE, LASER, MELEE, WOUND);
}

/// A version of armor that cannot be modified and will always return itself when attempted to be modified
// tg: code/datums/armor/_armor.dm:43
public partial class datum_armor_immune : datum_armor
{
    // tg: code/datums/armor/_armor.dm:91
    public override object generate_new_with_modifiers(DmList modifiers = null)
    {
        return this;
    }

    // tg: code/datums/armor/_armor.dm:114
    public override object generate_new_with_multipliers(DmList multipliers = null)
    {
        return this;
    }

    // tg: code/datums/armor/_armor.dm:137
    public override object generate_new_with_specific(DmList values = null)
    {
        return this;
    }

    // tg: code/datums/armor/_armor.dm:147
    public override object get_rating(object rating = null)
    {
        return 100;
    }

    // tg: code/datums/armor/_armor.dm:160
    public override object get_rating_list(float? inverse = null)
    {
        DmList ratings = base.get_rating_list(inverse) as DmList; // get all ratings
        foreach (object rating in Dm.each(ratings))
            ratings[rating] = 100; // and set them to 100
        return ratings;
    }

    // tg: code/datums/armor/_armor.dm:172
    public override object add_other_armor(object other = null)
    {
        return this;
    }

    // tg: code/datums/armor/_armor.dm:181
    public override object subtract_other_armor(object other = null)
    {
        return this;
    }

    // tg: code/datums/armor/_armor.dm:191
    public override object has_any_armor()
    {
        return TRUE;
    }
}

// tg: code/datums/armor/_armor.dm:200 (global procs)
public static partial class GlobalProcs
{
    /**
     * Rounds armor_value down to the nearest 10, divides it by 10 and then converts it to Roman numerals.
     *
     * Arguments:
     * * armor_value - Number we're converting
     */
    // tg: code/datums/armor/_armor.dm:200
    public static object armor_to_protection_class(object armor_value = null)
    {
        object dot = null;
        if (Dm.num(armor_value) < 0)
            dot = "-";
        dot = Dm.add(dot, Dm.text("\\Roman[]", round(abs(Dm.num(armor_value)), 10) / 10));
        return dot;
    }

    /**
     * Returns the client readable name of an armor type
     *
     * Arguments:
     * * armor_type - The type to convert
     */
    // tg: code/datums/armor/_armor.dm:212
    public static object armor_to_protection_name(object armor_type = null)
    {
        object sw__1 = armor_type;
        if (Dm.eq(sw__1, ACID))
            return "ACID";
        if (Dm.eq(sw__1, BIO))
            return "BIOHAZARD";
        if (Dm.eq(sw__1, BOMB))
            return "EXPLOSIVE";
        if (Dm.eq(sw__1, BULLET))
            return "BULLET";
        if (Dm.eq(sw__1, CONSUME))
            return "CONSUMING";
        if (Dm.eq(sw__1, ENERGY))
            return "ENERGY";
        if (Dm.eq(sw__1, FIRE))
            return "FIRE";
        if (Dm.eq(sw__1, LASER))
            return "LASER";
        if (Dm.eq(sw__1, MELEE))
            return "MELEE";
        if (Dm.eq(sw__1, WOUND))
            return "WOUNDING";
        throw Dm.CRASH(Dm.text("Unknown armor type '[]'", armor_type), "code/datums/armor/_armor.dm", 234);
    }
}
