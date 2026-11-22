// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/pronouns.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// SKELETON: holds the [DmPath] of a type that the wave-4 var blocks and signatures name (rules 2.3: the earliest .dme file that declares it), written by the W4.0 contracts; only the type is declared, and the file is translated with its module.
// PARTIAL (M4a P1): p_they, p_They and p_are of /datum, /mob and /atom translated (the examine base's tag line,
// game/atom/atom_examine.dm:31); M4b slice 3 (fire's messages): p_them and p_s of the same types; M5 slice 1: /datum's
// p_theyre and p_Theyre (the burning component); M5 slice 2: p_their of /datum, /mob and /atom (the stacks' messages); M5 slice 4: p_es of the same types (the girder wall messages); M6 slice 2: /datum's p_Their (the wound descriptions); M8z slice B: p_were of /datum, /mob and /atom (the shoelace prank). The human's overrides
// (get_visible_gender, identity M9) and the other pronouns come with their users.

// tg: code/__HELPERS/pronouns.dm:413
[DmPath("/mob/living/carbon/human")]
public partial class mob_living_carbon_human : mob_living_carbon
{
}

// M2 F1: the clothing base, translated in modules/clothing/clothing.cs
// tg: code/__HELPERS/pronouns.dm:467
[DmPath("/obj/item/clothing")]
public partial class obj_item_clothing : obj_item
{
}

//pronoun procs, for getting pronouns without using the text macros that only work in certain positions
//datums don't have gender, but most of their subtypes do!
// tg: code/__HELPERS/pronouns.dm:6
public partial class datum
{
    // tg: code/__HELPERS/pronouns.dm:6
    public virtual object p_they(object temp_gender = null)
    {
        return "it";
    }

    // tg: code/__HELPERS/pronouns.dm:9
    public virtual object p_They(object temp_gender = null)
    {
        return capitalize(p_they(temp_gender));
    }

    // tg: code/__HELPERS/pronouns.dm:33
    public virtual object p_are(object temp_gender = null)
    {
        return "is";
    }

    // tg: code/__HELPERS/pronouns.dm:36
    public virtual object p_were(object temp_gender = null)
    {
        return "was";
    }

    // tg: code/__HELPERS/pronouns.dm:48  (M5 slice 1: the burning component's examine line)
    public virtual object p_theyre(object temp_gender = null)
    {
        return Dm.add(Dm.add(p_they(temp_gender), "'"), copytext_char(p_are(temp_gender), 2));
    }

    // tg: code/__HELPERS/pronouns.dm:51
    public virtual object p_Theyre(object temp_gender = null)
    {
        return Dm.add(Dm.add(p_They(temp_gender), "'"), copytext_char(p_are(temp_gender), 2));
    }

    // tg: code/__HELPERS/pronouns.dm:12
    public virtual object p_their(object temp_gender = null)
    {
        return "its";
    }

    // tg: code/__HELPERS/pronouns.dm:15
    public virtual object p_Their(object temp_gender = null)
    {
        return capitalize(p_their(temp_gender));
    }

    // tg: code/__HELPERS/pronouns.dm:18
    public virtual object p_theirs(object temp_gender = null)
    {
        return "its";
    }

    // tg: code/__HELPERS/pronouns.dm:21
    public virtual object p_Theirs(object temp_gender = null)
    {
        return capitalize(p_theirs(temp_gender));
    }

    // tg: code/__HELPERS/pronouns.dm:24
    public virtual object p_them(object temp_gender = null, params object[] __extra)
    {
        return "it";
    }

    // tg: code/__HELPERS/pronouns.dm:27
    public virtual object p_Them(object temp_gender = null)
    {
        return capitalize(p_them(temp_gender));
    }

    // tg: code/__HELPERS/pronouns.dm:54
    public virtual object p_s(object temp_gender = null) //is this a descriptive proc name, or what?
    {
        return "s";
    }

    // tg: code/__HELPERS/pronouns.dm:57
    public virtual object p_es(object temp_gender = null)
    {
        return "es";
    }
}

// tg: code/__HELPERS/pronouns.dm:198
public partial class mob : atom_movable
{
    // tg: code/__HELPERS/pronouns.dm:290
    public override object p_themselves(object temp_gender = null)
    {
        if (!Dm.truthy(temp_gender))
            temp_gender = gender;
        if (Dm.eq(temp_gender, FEMALE))
            return "herself";
        if (Dm.eq(temp_gender, MALE))
            return "himself";
        if (Dm.eq(temp_gender, PLURAL))
            return "themselves";
        return "itself";
    }

    // tg: code/__HELPERS/pronouns.dm:211
    public override object p_their(object temp_gender = null)
    {
        if (!Dm.truthy(temp_gender))
            temp_gender = gender;
        object sw__1 = temp_gender;
        if (Dm.eq(sw__1, FEMALE))
            return "her";
        else if (Dm.eq(sw__1, MALE))
            return "his";
        else if (Dm.eq(sw__1, PLURAL))
            return "their";
        else
            return "its";
    }

    // tg: code/__HELPERS/pronouns.dm:198
    public override object p_they(object temp_gender = null)
    {
        if (!Dm.truthy(temp_gender))
            temp_gender = gender;
        object sw__1 = temp_gender;
        if (Dm.eq(sw__1, FEMALE))
            return "she";
        else if (Dm.eq(sw__1, MALE))
            return "he";
        else if (Dm.eq(sw__1, PLURAL))
            return "they";
        else
            return "it";
    }

    // tg: code/__HELPERS/pronouns.dm:257
    public override object p_are(object temp_gender = null)
    {
        if (!Dm.truthy(temp_gender))
            temp_gender = gender;
        if (Dm.eq(temp_gender, PLURAL))
            return "are";
        return "is";
    }

    // tg: code/__HELPERS/pronouns.dm:264
    public override object p_were(object temp_gender = null)
    {
        if (!Dm.truthy(temp_gender))
            temp_gender = gender;
        if (Dm.eq(temp_gender, PLURAL))
            return "were";
        return "was";
    }

    // tg: code/__HELPERS/pronouns.dm:224
    public override object p_theirs(object temp_gender = null)
    {
        if (!Dm.truthy(temp_gender))
            temp_gender = gender;
        object sw__1 = temp_gender;
        if (Dm.eq(sw__1, FEMALE))
            return "hers";
        else if (Dm.eq(sw__1, MALE))
            return "his";
        else if (Dm.eq(sw__1, PLURAL))
            return "theirs";
        else
            return "its";
    }

    // tg: code/__HELPERS/pronouns.dm:237
    [DmParams("capitalized", "temp_gender")]
    public override object p_them(object capitalized = null, params object[] __extra)
    {
        object temp_gender = Dm.arg(__extra, 0);
        if (!Dm.truthy(temp_gender))
            temp_gender = gender;
        object sw__1 = temp_gender;
        if (Dm.eq(sw__1, FEMALE))
            return "her";
        else if (Dm.eq(sw__1, MALE))
            return "him";
        else if (Dm.eq(sw__1, PLURAL))
            return "them";
        else
            return "it";
    }

    // tg: code/__HELPERS/pronouns.dm:278
    public override object p_s(object temp_gender = null)
    {
        if (!Dm.truthy(temp_gender))
            temp_gender = gender;
        if (!Dm.eq(temp_gender, PLURAL))
            return "s";
        return null;
    }

    // tg: code/__HELPERS/pronouns.dm:284
    public override object p_es(object temp_gender = null)
    {
        if (!Dm.truthy(temp_gender))
            temp_gender = gender;
        if (!Dm.eq(temp_gender, PLURAL))
            return "es";
        return null;
    }
}

//Pronoun procs for objects
// tg: code/__HELPERS/pronouns.dm:304
public partial class atom : datum
{
    // tg: code/__HELPERS/pronouns.dm:317
    public override object p_their(object temp_gender = null)
    {
        if (!Dm.truthy(temp_gender))
            temp_gender = gender;
        object sw__1 = temp_gender;
        if (Dm.eq(sw__1, FEMALE))
            return "her";
        else if (Dm.eq(sw__1, MALE))
            return "his";
        else if (Dm.eq(sw__1, PLURAL))
            return "their";
        else
            return "its";
    }

    // tg: code/__HELPERS/pronouns.dm:304
    public override object p_they(object temp_gender = null)
    {
        if (!Dm.truthy(temp_gender))
            temp_gender = gender;
        object sw__1 = temp_gender;
        if (Dm.eq(sw__1, FEMALE))
            return "she";
        else if (Dm.eq(sw__1, MALE))
            return "he";
        else if (Dm.eq(sw__1, PLURAL))
            return "they";
        else
            return "it";
    }

    // tg: code/__HELPERS/pronouns.dm:363
    public override object p_are(object temp_gender = null)
    {
        if (!Dm.truthy(temp_gender))
            temp_gender = gender;
        if (Dm.eq(temp_gender, PLURAL))
            return "are";
        return "is";
    }

    // tg: code/__HELPERS/pronouns.dm:370
    public override object p_were(object temp_gender = null)
    {
        if (!Dm.truthy(temp_gender))
            temp_gender = gender;
        if (Dm.eq(temp_gender, PLURAL))
            return "were";
        return "was";
    }

    // tg: code/__HELPERS/pronouns.dm:343
    [DmParams("capitalized", "temp_gender")]
    public override object p_them(object capitalized = null, params object[] __extra)
    {
        object temp_gender = Dm.arg(__extra, 0);
        if (!Dm.truthy(temp_gender))
            temp_gender = gender;
        object sw__1 = temp_gender;
        if (Dm.eq(sw__1, FEMALE))
            return "her";
        else if (Dm.eq(sw__1, MALE))
            return "him";
        else if (Dm.eq(sw__1, PLURAL))
            return "them";
        else
            return "it";
    }

    // tg: code/__HELPERS/pronouns.dm:384
    public override object p_s(object temp_gender = null)
    {
        if (!Dm.truthy(temp_gender))
            temp_gender = gender;
        if (!Dm.eq(temp_gender, PLURAL))
            return "s";
        return null;
    }

    // tg: code/__HELPERS/pronouns.dm:390
    public override object p_es(object temp_gender = null)
    {
        if (!Dm.truthy(temp_gender))
            temp_gender = gender;
        if (!Dm.eq(temp_gender, PLURAL))
            return "es";
        return null;
    }
}

// tg: code/__HELPERS/pronouns.dm:30 (interleaved types, rules 1.9; M5x slice 4: the grenade's examine, "it looks like they've
// already been used")
public partial class datum
{
    // tg: code/__HELPERS/pronouns.dm:30
    public virtual object p_have(object temp_gender = null)
    {
        return "has";
    }

    // tg: code/__HELPERS/pronouns.dm:42
    public virtual object p_theyve(object temp_gender = null)
    {
        return Dm.add(Dm.add(p_they(temp_gender), "'"), copytext_char(p_have(temp_gender), 3));
    }

    // tg: code/__HELPERS/pronouns.dm:45  (X3: the fish's examine)
    public virtual object p_Theyve(object temp_gender = null)
    {
        return Dm.add(Dm.add(p_They(temp_gender), "'"), copytext_char(p_have(temp_gender), 3));
    }
}

// tg: code/__HELPERS/pronouns.dm:250 (interleaved types, rules 1.9)
public partial class mob : atom_movable
{
    // tg: code/__HELPERS/pronouns.dm:250
    public override object p_have(object temp_gender = null)
    {
        if (!Dm.truthy(temp_gender))
            temp_gender = gender;
        if (Dm.eq(temp_gender, PLURAL))
            return "have";
        return "has";
    }
}

// tg: code/__HELPERS/pronouns.dm:356 (interleaved types, rules 1.9)
public partial class atom : datum
{
    // tg: code/__HELPERS/pronouns.dm:356
    public override object p_have(object temp_gender = null)
    {
        if (!Dm.truthy(temp_gender))
            temp_gender = gender;
        if (Dm.eq(temp_gender, PLURAL))
            return "have";
        return "has";
    }
}


public static partial class GlobalProcs
{
    // M7x slice D: the pellet cloud's hit messages (the throw_item message keeps its local copy in living_item_handling.cs)
    // tg: code/__HELPERS/pronouns.dm:63
    public static string plural_s(object pluralize = null)
    {
        string sw__1 = copytext_char(pluralize, -2);
        if (sw__1 == "ss" || sw__1 == "sh" || sw__1 == "ch")
            return "es";
        string sw__2 = copytext_char(pluralize, -1);
        if (sw__2 == "s" || sw__2 == "x" || sw__2 == "z")
            return "es";
        return "s";
    }
}

// tg: code/__HELPERS/pronouns.dm:39 (interleaved types, rules 1.9; M8 slice A: the failing liver's examine line)
public partial class datum
{
    // tg: code/__HELPERS/pronouns.dm:39
    public virtual object p_do(object temp_gender = null)
    {
        return "does";
    }
}

// tg: code/__HELPERS/pronouns.dm:271 (interleaved types, rules 1.9)
public partial class mob : atom_movable
{
    // tg: code/__HELPERS/pronouns.dm:271
    public override object p_do(object temp_gender = null)
    {
        if (!Dm.truthy(temp_gender))
            temp_gender = gender;
        if (Dm.eq(temp_gender, PLURAL))
            return "do";
        return "does";
    }
}

// tg: code/__HELPERS/pronouns.dm:377 (interleaved types, rules 1.9)
public partial class atom : datum
{
    // tg: code/__HELPERS/pronouns.dm:377
    public override object p_do(object temp_gender = null)
    {
        if (!Dm.truthy(temp_gender))
            temp_gender = gender;
        if (Dm.eq(temp_gender, PLURAL))
            return "do";
        return "does";
    }
}

public partial class datum
{
    // tg: code/__HELPERS/pronouns.dm:60
    public virtual object p_themselves(object temp_gender = null)
    {
        return "itself";
    }
}

public partial class atom : datum
{
    // tg: code/__HELPERS/pronouns.dm:396
    public override object p_themselves(object temp_gender = null)
    {
        temp_gender ??= gender;
        switch (temp_gender)
        {
            case FEMALE:
                return "herself";
            case MALE:
                return "himself";
            case PLURAL:
                return "themselves";
            default:
                return "itself";
        }
    }
}

// tg: code/__HELPERS/pronouns.dm:81 (interleaved types, rules 1.9; gaps package C5: the noticable organs' examine text)
public partial class datum
{
    /// A proc to replace pronouns in a string with the appropriate pronouns for a target atom.
    /// Uses associative list access from a __DEFINE list, since associative access is slightly
    /// faster
    // tg: code/__HELPERS/pronouns.dm:81
    public virtual object REPLACE_PRONOUNS(object target_string = null, atom targeted_atom = null, object targeted_gender = null)
    {
        /// If someone specifies targeted_gender we choose that,
        /// otherwise we go off the gender of our object
        object gender;
        if (Dm.truthy(targeted_gender))
        {
            if (!Dm.istext(targeted_gender) || !Dm.@in(targeted_gender, Dm.list(MALE, FEMALE, PLURAL, NEUTER)))
            {
                stack_trace("REPLACE_PRONOUNS called with improper parameters.", "code/__HELPERS/pronouns.dm", 87);
                return null;
            }
            gender = targeted_gender;
        }
        else
            gender = targeted_atom.get_visible_gender();
        ///The pronouns are ordered by their length to avoid %PRONOUN_Theyve being translated to "Heve" instead of "He's", for example
        regex pronoun_regex = Dm.regex("%PRONOUN(_(theirs|Theirs|theyve|Theyve|theyre|Theyre|their|Their|they|They|them|Them|have|were|are|do|es|s))");
        while (Dm.truthy(pronoun_regex.Find(target_string)))
        {
            // GET_TARGET_PRONOUN (:1): call(target, ALL_PRONOUNS[pronoun])(gender), each key "%PRONOUN_x" is the proc p_x (__DEFINES/pronouns.dm)
            string pronoun_proc = "p_" + (pronoun_regex.match as string)["%PRONOUN_".Length..];
            target_string = pronoun_regex.Replace(target_string, Waiting.NoSleep(Dm.Call(targeted_atom, pronoun_proc, gender)));
        }
        return target_string;
    }
}

// tg: code/__HELPERS/pronouns.dm:410 (interleaved types, rules 1.9)
public partial class atom : datum
{
    // tg: code/__HELPERS/pronouns.dm:410
    public virtual object get_visible_gender()
    {
        return gender;
    }
}

// tg: code/__HELPERS/pronouns.dm:413 (interleaved types, rules 1.9)
public partial class mob_living_carbon_human : mob_living_carbon
{
    // tg: code/__HELPERS/pronouns.dm:413
    public override object get_visible_gender()
    {
        if (HAS_TRAIT(this, TRAIT_UNKNOWN_APPEARANCE))
            return PLURAL;
        // If both face and uniform are hidden, can't assess gender
        if (Dm.eq(Dm.bitand(obscured_slots, Dm.bitor(HIDEFACE, HIDEJUMPSUIT)), Dm.bitor(HIDEFACE, HIDEJUMPSUIT)))
            return PLURAL;
        return gender;
    }
}
