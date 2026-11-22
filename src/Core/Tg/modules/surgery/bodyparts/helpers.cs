// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/bodyparts/helpers.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: everything translated (M2 P2; M6 slice 4: the limb replacement procs and newBodyPart; gaps X5: the larva's and the adult
// alien's overrides) but get_hand_of_item (inventory, M4a), which stays a root with a Dm.NotTranslated body. The embedding procs
// (remove_embedded_object, remove_all_embedded_objects, has_embedded_objects): M6 slice 3b.

// tg: code/modules/surgery/bodyparts/helpers.dm:11
public partial class mob_living : mob
{
    /**
     * Returns a bodypart of the specified zone that this mob has
     *
     * * zone: the zone to get.
     * Defaults to chest, allowing for skilling zone nullchecks if you don't care what bodypart you get.
     * * include_stumps: whether or not to consider stumps as valid bodyparts to return.
     * Defaults to FALSE, meaning that if a limb is missing (is a stump), nothing will be returned.
     *
     * Returns a bodypart, or null.
     */
    // tg: code/modules/surgery/bodyparts/helpers.dm:11
    public virtual object get_bodypart(string zone = null, float? include_stumps = null)
    {
        zone ??= BODY_ZONE_CHEST;
        include_stumps ??= FALSE;
        return null;
    }
}

// tg: code/modules/surgery/bodyparts/helpers.dm:14 (interleaved types, rules 1.9)
public partial class mob_living_carbon : mob_living
{
    // tg: code/modules/surgery/bodyparts/helpers.dm:14
    public override object get_bodypart(string zone = null, float? include_stumps = null)
    {
        zone ??= BODY_ZONE_CHEST;
        include_stumps ??= FALSE;
        if (!Dm.truthy(include_stumps))
            return real_bodypart_cache[zone];

        foreach (obj_item_bodypart bodypart in Dm.each_cast<obj_item_bodypart>(bodyparts))
        {
            if (Dm.eq(bodypart.body_zone, zone))
                return bodypart;
        }
        return null;
    }
}

// tg: code/modules/surgery/bodyparts/helpers.dm:31 (interleaved types, rules 1.9)
public partial class mob_living : mob
{
    /**
     * Returns all bodyparts this mob has, optionally including stumps.
     *
     * include_stumps: whether or not to consider stumps as valid bodyparts to return.
     * Defaults to FALSE, meaning that if a limb is missing (is a stump), it won't be included in the returned list.
     *
     * Returns a list of bodyparts, which may be empty.
     */
    // tg: code/modules/surgery/bodyparts/helpers.dm:31
    public virtual object get_bodyparts(float? include_stumps = null)
    {
        include_stumps ??= FALSE;
        DmList parts = Dm.list();
        foreach (object zone in Dm.each(get_all_limbs()))
        {
            obj_item_bodypart bodypart = get_bodypart(zone as string, include_stumps) as obj_item_bodypart;
            if (bodypart != null)
                Dm.append(ref parts, bodypart);
        }
        return parts;
    }
}

// tg: code/modules/surgery/bodyparts/helpers.dm:39 (interleaved types, rules 1.9)
public partial class mob_living_carbon : mob_living
{
    // tg: code/modules/surgery/bodyparts/helpers.dm:39
    public override object get_bodyparts(float? include_stumps = null)
    {
        include_stumps ??= FALSE;
        if (Dm.truthy(include_stumps))
            return bodyparts.Copy();
        DmList parts = Dm.list();
        foreach ((object key, object limb) in Dm.pairs(real_bodypart_cache))
            Dm.append(ref parts, limb);
        return parts;
    }
}

// tg: code/modules/surgery/bodyparts/helpers.dm:54 (interleaved types, rules 1.9)
public partial class mob_living : mob
{
    /**
     * Returns all bodyparts this mob has, indexed by their body zone
     * Also includes stumps and nulls, so be sure to check for those if you use this proc.
     * (Note: nulls will be very rare - as 95% of missing limbs are represented as stumps - but not impossible due to some edge cases)
     *
     * Returns a list of bodyparts indexed by their body zone
     */
    // tg: code/modules/surgery/bodyparts/helpers.dm:54
    public virtual object get_bodyparts_by_zones()
    {
        DmList parts = Dm.list();
        foreach (object zone in Dm.each(get_all_limbs()))
            parts[zone] = get_bodypart(zone as string);
        return parts;
    }
}

// tg: code/modules/surgery/bodyparts/helpers.dm:61 (interleaved types, rules 1.9)
public partial class mob_living_carbon : mob_living
{
    /// Replaces a single limb and deletes the old one if there was one
    // tg: code/modules/surgery/bodyparts/helpers.dm:61
    public virtual async DmTask del_and_replace_bodypart(obj_item_bodypart new_limb = null, object special = null)
    {
        obj_item_bodypart old_limb = get_bodypart(new_limb.body_zone as string) as obj_item_bodypart;
        if (old_limb != null)
        {
            await old_limb.drop_limb(special: TRUE);
            qdel(old_limb);
        }
        await new_limb.try_attach_limb(this, special: special);
    }

    /// Replaces a single limb and returns the old one if there was one
    // tg: code/modules/surgery/bodyparts/helpers.dm:69
    public virtual async DmTask<object> return_and_replace_bodypart(obj_item_bodypart new_limb = null, object special = null)
    {
        obj_item_bodypart old_limb = get_bodypart(new_limb.body_zone as string) as obj_item_bodypart;
        if (old_limb != null)
        {
            await old_limb.drop_limb(special: special);
            await old_limb.moveToNullspace();
        }

        await new_limb.try_attach_limb(this, special: special);
        return old_limb; // can be null
    }

    /// Replaces the chosen limb(zone) to the original one
    // tg: code/modules/surgery/bodyparts/helpers.dm:79
    public virtual async DmTask reset_to_original_bodypart(object limb_zone = null)
    {
        if (!Dm.@in(limb_zone, GLOB.all_body_zones))
        {
            stack_trace(Dm.text("Invalid zone [] provided to reset_to_original_bodypart()", limb_zone), "code/modules/surgery/bodyparts/helpers.dm", 81);
            return;
        }

        // find old limb to del it first
        obj_item_bodypart old_limb = get_bodypart(limb_zone as string) as obj_item_bodypart;
        if (old_limb != null)
        {
            await old_limb.drop_limb(special: TRUE);
            qdel(old_limb);
        }

        // mаke and attach the original limb
        obj_item_bodypart original_limb = await newBodyPart(limb_zone) as obj_item_bodypart;
        await original_limb.try_attach_limb(this, TRUE);
        await original_limb.update_limb(is_creating: TRUE);
        await regenerate_icons();
    }

    // tg: code/modules/surgery/bodyparts/helpers.dm:96
    public override object has_hand_for_held_index(object i = null)
    {
        if (!Dm.truthy(i))
            return FALSE;
        obj_item_bodypart hand_instance = hand_bodyparts[i] as obj_item_bodypart;
        if (hand_instance != null && !Dm.truthy(hand_instance.bodypart_disabled))
            return hand_instance;
        return FALSE;
    }
}

// tg: code/modules/surgery/bodyparts/helpers.dm:106 (interleaved types, rules 1.9)
public partial class mob : atom_movable
{
    ///Get the bodypart for whatever hand we have active, Only relevant for carbons
    // tg: code/modules/surgery/bodyparts/helpers.dm:106
    public virtual object get_active_hand()
    {
        return FALSE;
    }
}

// tg: code/modules/surgery/bodyparts/helpers.dm:109 (interleaved types, rules 1.9)
public partial class mob_living_carbon : mob_living
{
    // tg: code/modules/surgery/bodyparts/helpers.dm:109
    public override object get_active_hand()
    {
        string which_hand = BODY_ZONE_PRECISE_L_HAND;
        if (IS_RIGHT_INDEX(active_hand_index))
            which_hand = BODY_ZONE_PRECISE_R_HAND;
        return get_bodypart(check_zone(which_hand) as string);
    }
}

// tg: code/modules/surgery/bodyparts/helpers.dm:116 (interleaved types, rules 1.9)
public partial class mob : atom_movable
{
    /// Gets the inactive hand of the mob. Returns FALSE on non-carbons, otherwise returns the /obj/item/bodypart.
    // tg: code/modules/surgery/bodyparts/helpers.dm:116
    public virtual object get_inactive_hand()
    {
        return null;
    }
}

// tg: code/modules/surgery/bodyparts/helpers.dm:119 (interleaved types, rules 1.9)
public partial class mob_living_carbon : mob_living
{
    // tg: code/modules/surgery/bodyparts/helpers.dm:119
    public override object get_inactive_hand()
    {
        string which_hand = BODY_ZONE_PRECISE_R_HAND;
        if (IS_RIGHT_INDEX(active_hand_index))
            which_hand = BODY_ZONE_PRECISE_L_HAND;
        return get_bodypart(check_zone(which_hand) as string);
    }
}

// tg: code/modules/surgery/bodyparts/helpers.dm:125 (interleaved types, rules 1.9)
public partial class mob : atom_movable
{
    // tg: code/modules/surgery/bodyparts/helpers.dm:125
    public virtual object has_left_hand(float? check_disabled = null)
    {
        check_disabled ??= TRUE;
        return TRUE;
    }
}

// tg: code/modules/surgery/bodyparts/helpers.dm:129 (interleaved types, rules 1.9)
public partial class mob_living_carbon : mob_living
{
    // tg: code/modules/surgery/bodyparts/helpers.dm:129
    public override object has_left_hand(float? check_disabled = null)
    {
        check_disabled ??= TRUE;
        foreach (obj_item_bodypart hand_instance in Dm.each<obj_item_bodypart>(hand_bodyparts))
        {
            if (IS_RIGHT_INDEX(hand_instance.held_index) || (Dm.truthy(check_disabled) && Dm.truthy(hand_instance.bodypart_disabled)))
                continue;
            return TRUE;
        }
        return FALSE;
    }
}

// tg: code/modules/surgery/bodyparts/helpers.dm:141 (interleaved types, rules 1.9)
public partial class mob : atom_movable
{
    // tg: code/modules/surgery/bodyparts/helpers.dm:141
    public virtual object has_right_hand(float? check_disabled = null)
    {
        check_disabled ??= TRUE;
        return TRUE;
    }
}

// tg: code/modules/surgery/bodyparts/helpers.dm:145 (interleaved types, rules 1.9)
public partial class mob_living_carbon : mob_living
{
    // tg: code/modules/surgery/bodyparts/helpers.dm:145
    public override object has_right_hand(float? check_disabled = null)
    {
        check_disabled ??= TRUE;
        foreach (obj_item_bodypart hand_instance in Dm.each<obj_item_bodypart>(hand_bodyparts))
        {
            if (IS_LEFT_INDEX(hand_instance.held_index) || (Dm.truthy(check_disabled) && Dm.truthy(hand_instance.bodypart_disabled)))
                continue;
            return TRUE;
        }
        return FALSE;
    }

    /// Returns the bodypart holding the passed item
    // tg: code/modules/surgery/bodyparts/helpers.dm:157
    public virtual object get_hand_of_item(obj_item I = null)
    {
        Dm.NotTranslated("code/modules/surgery/bodyparts/helpers.dm:157", "get_hand_of_item: not in M2 (items in hands, M4a)");
        return default;
    }
}

// tg: code/modules/surgery/bodyparts/helpers.dm:161 (interleaved types, rules 1.9)
public partial class mob_living : mob
{
    ///Returns a list of all limbs this mob should have.
    // tg: code/modules/surgery/bodyparts/helpers.dm:161
    public virtual object get_all_limbs()
    {
        return GLOB.all_body_zones.Copy();
    }
}

// tg: code/modules/surgery/bodyparts/helpers.dm:166 (interleaved types, rules 1.9)
public partial class mob_living_carbon : mob_living
{
    ///Returns a list of all limbs this mob should have.
    // tg: code/modules/surgery/bodyparts/helpers.dm:166
    public override object get_all_limbs()
    {
        object dot = null;
        // gets the "normal list", ie chest-head-legs-arms. order matters for human rendering!
        dot = (((datum_species)dna?.species)?.bodypart_overrides.Copy() is var or__1 && Dm.truthy(or__1) ? or__1 : base.get_all_limbs());
        // includes any additional adminbussed hands
        foreach (obj_item_bodypart hand in Dm.each<obj_item_bodypart>(hand_bodyparts))
            Dm.combine(ref dot, hand.body_zone);
        return dot;
    }

    ///Returns a list of all missing limbs this mob should have on them, but don't.
    // tg: code/modules/surgery/bodyparts/helpers.dm:174
    public virtual object get_missing_limbs()
    {
        DmList full = get_all_limbs() as DmList;
        foreach (object zone in Dm.each(full))
        {
            if (Dm.truthy(get_bodypart(zone as string)))
                Dm.remove(ref full, zone);
        }
        return full;
    }
}

// tg: code/modules/surgery/bodyparts/helpers.dm:188 (interleaved types, rules 1.9)
public partial class mob_living : mob
{
    // tg: code/modules/surgery/bodyparts/helpers.dm:188
    public virtual object get_disabled_limbs()
    {
        return Dm.list();
    }
}

// tg: code/modules/surgery/bodyparts/helpers.dm:191 (interleaved types, rules 1.9)
public partial class mob_living_carbon : mob_living
{
    // tg: code/modules/surgery/bodyparts/helpers.dm:191
    public override object get_disabled_limbs()
    {
        DmList full = get_all_limbs() as DmList;
        DmList disabled = Dm.list();
        foreach (object zone in Dm.each(full))
        {
            obj_item_bodypart affecting = get_bodypart(zone as string) as obj_item_bodypart;
            if (Dm.truthy(affecting?.bodypart_disabled))
                Dm.append(ref disabled, zone);
        }
        return disabled;
    }

    ///Remove a specific embedded item from the carbon mob
    // tg: code/modules/surgery/bodyparts/helpers.dm:210
    public virtual async DmTask remove_embedded_object(obj_item embedded = null)
    {
        if ((embedded.get_embed() as datum_embedding)?.owner != this)
            return;
        await ((datum_embedding)embedded.get_embed()).remove_embedding();
    }

    ///Remove all embedded objects from all limbs on the carbon mob
    // tg: code/modules/surgery/bodyparts/helpers.dm:216
    public virtual async DmTask remove_all_embedded_objects()
    {
        foreach (obj_item_bodypart bodypart in Dm.each_cast<obj_item_bodypart>(get_bodyparts(include_stumps: TRUE)))
        {
            foreach (obj_item embedded in Dm.each_cast<obj_item>(bodypart.embedded_objects))
                await remove_embedded_object(embedded);
        }
    }

    // tg: code/modules/surgery/bodyparts/helpers.dm:221
    public virtual object has_embedded_objects(float? include_harmless = null)
    {
        include_harmless ??= FALSE;
        foreach (obj_item_bodypart bodypart in Dm.each_cast<obj_item_bodypart>(get_bodyparts(include_stumps: TRUE)))
        {
            foreach (obj_item embedded in Dm.each_cast<obj_item>(bodypart.embedded_objects))
            {
                if (!Dm.truthy(include_harmless) && Dm.truthy(((datum_embedding)embedded.get_embed()).is_harmless(consider_stamina: TRUE)))
                    continue;
                return TRUE;
            }
        }
        return null;
    }

    //Helper for quickly creating a new limb - used by augment code in species.dm spec_attacked_by
    //
    // FUCK YOU AUGMENT CODE - With love, Kapu
    // tg: code/modules/surgery/bodyparts/helpers.dm:231
    public virtual async DmTask<object> newBodyPart(object zone = null)
    {
        object path = ((datum_species)dna.species).bodypart_overrides[zone];
        if (path == null)
            return null;
        obj_item_bodypart new_bodypart = await Dm.new_async(path as Type) as obj_item_bodypart;
        return new_bodypart;
    }

    /// Makes sure that the owner's bodytype flags match the flags of all of its parts and organs
    // tg: code/modules/surgery/bodyparts/helpers.dm:266
    public virtual void synchronize_bodytypes()
    {
        float all_overlay_flags = NONE;
        foreach (obj_item_bodypart limb in Dm.each_cast<obj_item_bodypart>(get_bodyparts(include_stumps: TRUE)))
        {
            foreach (obj_item_organ organ in Dm.each<obj_item_organ>(limb))
                Dm.combine(ref all_overlay_flags, organ.external_bodytypes);
            Dm.combine(ref all_overlay_flags, limb.bodytype);
        }

        bodytype = all_overlay_flags;
    }

    /// Makes sure that the owner's bodyshape flags match the flags of all of its parts and organs
    // tg: code/modules/surgery/bodyparts/helpers.dm:276
    public virtual void synchronize_bodyshapes()
    {
        float all_overlay_flags = NONE;
        foreach (obj_item_bodypart limb in Dm.each_cast<obj_item_bodypart>(get_bodyparts(include_stumps: TRUE)))
        {
            foreach (obj_item_organ organ in Dm.each<obj_item_organ>(limb))
                Dm.combine(ref all_overlay_flags, organ.external_bodyshapes);
            Dm.combine(ref all_overlay_flags, limb.bodyshape);
        }

        if (Dm.truthy(Dm.bitand(obscured_slots, HIDESNOUT)))
            Dm.mask(ref all_overlay_flags, Dm.bitnot(BODYSHAPE_SNOUTED));

        bodyshape = all_overlay_flags;
    }
}

// tg: code/modules/surgery/bodyparts/helpers.dm:288 (global procs; interleaved types, rules 1.9)
public static partial class GlobalProcs
{
    // tg: code/modules/surgery/bodyparts/helpers.dm:288
    public static object skintone2hex(object skin_tone = null)
    {
        object dot = null;
        dot = 0;
        object sw__1 = skin_tone;
        if (Dm.eq(sw__1, "caucasian1"))
            dot = "#ffe0d1";
        else if (Dm.eq(sw__1, "caucasian2"))
            dot = "#fcccb3";
        else if (Dm.eq(sw__1, "caucasian3"))
            dot = "#e8b59b";
        else if (Dm.eq(sw__1, "latino"))
            dot = "#d9ae96";
        else if (Dm.eq(sw__1, "mediterranean"))
            dot = "#c79b8b";
        else if (Dm.eq(sw__1, "asian1"))
            dot = "#ffdeb3";
        else if (Dm.eq(sw__1, "asian2"))
            dot = "#e3ba84";
        else if (Dm.eq(sw__1, "arab"))
            dot = "#c4915e";
        else if (Dm.eq(sw__1, "indian"))
            dot = "#b87840";
        else if (Dm.eq(sw__1, "mixed1"))
            dot = "#a57a66";
        else if (Dm.eq(sw__1, "mixed2"))
            dot = "#87563d";
        else if (Dm.eq(sw__1, "mixed3"))
            dot = "#725547";
        else if (Dm.eq(sw__1, "mixed4"))
            dot = "#866e63";
        else if (Dm.eq(sw__1, "african1"))
            dot = "#754523";
        else if (Dm.eq(sw__1, "african2"))
            dot = "#471c18";
        else if (Dm.eq(sw__1, "albino"))
            dot = "#fff4e6";
        else if (Dm.eq(sw__1, "orange"))
            dot = "#ffc905";
        else if (Dm.eq(sw__1, "green"))
            dot = "#a8e61d";
        return dot;
    }
}

// tg: code/modules/surgery/bodyparts/helpers.dm:137 (interleaved types, rules 1.9; gaps X5)
public partial class mob_living_carbon_alien_larva : mob_living_carbon_alien
{
    // tg: code/modules/surgery/bodyparts/helpers.dm:137
    public override object has_left_hand(float? check_disabled = null)
    {
        check_disabled ??= TRUE;
        return TRUE;
    }

    // tg: code/modules/surgery/bodyparts/helpers.dm:153
    public override object has_right_hand(float? check_disabled = null)
    {
        check_disabled ??= TRUE;
        return TRUE;
    }

    ///Returns a list of all limbs this mob should have.
    // tg: code/modules/surgery/bodyparts/helpers.dm:181
    public override object get_missing_limbs()
    {
        DmList full = Dm.list(BODY_ZONE_HEAD, BODY_ZONE_CHEST);
        foreach (object zone in Dm.each(full))
        {
            if (Dm.truthy(get_bodypart(zone as string)))
                Dm.remove(ref full, zone);
        }
        return full;
    }

    // tg: code/modules/surgery/bodyparts/helpers.dm:200
    public override object get_disabled_limbs()
    {
        DmList full = Dm.list(BODY_ZONE_HEAD, BODY_ZONE_CHEST);
        DmList disabled = Dm.list();
        foreach (object zone in Dm.each(full))
        {
            obj_item_bodypart affecting = get_bodypart(zone as string) as obj_item_bodypart;
            if (Dm.truthy(affecting?.bodypart_disabled))
                Dm.append(ref disabled, zone);
        }
        return disabled;
    }

    // tg: code/modules/surgery/bodyparts/helpers.dm:238
    public override async DmTask<object> newBodyPart(object zone = null)
    {
        obj_item_bodypart new_bodypart = null;
        switch (Dm.str(zone))
        {
            case BODY_ZONE_HEAD:
                new_bodypart = Dm.@new<obj_item_bodypart_head_larva>();
                break;
            case BODY_ZONE_CHEST:
                new_bodypart = Dm.@new<obj_item_bodypart_chest_larva>();
                break;
        }
        return new_bodypart;
    }
}

// tg: code/modules/surgery/bodyparts/helpers.dm:247 (interleaved types, rules 1.9; gaps X5)
public partial class mob_living_carbon_alien_adult : mob_living_carbon_alien
{
    // tg: code/modules/surgery/bodyparts/helpers.dm:247
    public override async DmTask<object> newBodyPart(object zone = null)
    {
        obj_item_bodypart new_bodypart = null;
        switch (Dm.str(zone))
        {
            case BODY_ZONE_L_ARM:
                new_bodypart = Dm.@new<obj_item_bodypart_arm_left_alien>();
                break;
            case BODY_ZONE_R_ARM:
                new_bodypart = Dm.@new<obj_item_bodypart_arm_right_alien>();
                break;
            case BODY_ZONE_HEAD:
                new_bodypart = Dm.@new<obj_item_bodypart_head_alien>();
                break;
            case BODY_ZONE_L_LEG:
                new_bodypart = Dm.@new<obj_item_bodypart_leg_left_alien>();
                break;
            case BODY_ZONE_R_LEG:
                new_bodypart = Dm.@new<obj_item_bodypart_leg_right_alien>();
                break;
            case BODY_ZONE_CHEST:
                new_bodypart = Dm.@new<obj_item_bodypart_chest_alien>();
                break;
        }
        if (new_bodypart != null)
            await new_bodypart.update_limb(is_creating: TRUE);
        return null;
    }
}
