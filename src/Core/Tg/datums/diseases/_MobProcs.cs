// Translated from tgstation (snapshot 1d99bd7f): code/datums/diseases/_MobProcs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps VI1, D341): catching a disease by contact (with the clothes' bio armour per body zone), by air (the mouth
// covers and internals) and by force, and the human's virus immunity.

// tg: code/datums/diseases/_MobProcs.dm:2
public partial class mob_living : mob
{
    // tg: code/datums/diseases/_MobProcs.dm:2
    public virtual object HasDisease(datum_disease D = null)
    {
        foreach (object thing in Dm.each(diseases))
        {
            datum_disease DD = (datum_disease)thing;
            if (Dm.truthy(D.IsSame(DD)))
                return TRUE;
        }
        return FALSE;
    }


    // tg: code/datums/diseases/_MobProcs.dm:10
    public virtual object CanContractDisease(datum_disease D = null)
    {
        if (stat == DEAD && !Dm.truthy(D.process_dead))
            return FALSE;

        if (Dm.@in(D.GetDiseaseID(), disease_resistances))
            return FALSE;

        if (Dm.truthy(HasDisease(D)))
            return FALSE;

        if (!Dm.truthy(Dm.bitand(D.infectable_biotypes, mob_biotypes)))
            return FALSE;

        if (!Dm.truthy(D.is_viable_mobtype(type)))
            return FALSE;

        return TRUE;
    }


    // tg: code/datums/diseases/_MobProcs.dm:29
    public virtual async DmTask<object> ContactContractDisease(datum_disease D = null, params object[] __extra)
    {
        if (!Dm.truthy(CanContractDisease(D)))
            return FALSE;
        await D.try_infect(this);
        return null;
    }

    /**
     * Handle being contracted a disease via airborne transmission
     *
     * * disease - the disease datum that's infecting us
     */
    // tg: code/datums/diseases/_MobProcs.dm:102
    public virtual async DmTask<object> contract_airborne_disease(datum_disease disease = null)
    {
        if (!Dm.truthy(can_be_spread_airborne_disease()))
            return FALSE;
        if (!prob(min((50 * disease.spreading_modifier - 1), 50)))
            return FALSE;
        if (!Dm.truthy(disease.has_required_infectious_organ(this as mob_living_carbon, ORGAN_SLOT_LUNGS)))
            return FALSE;
        return await ForceContractDisease(disease);
    }

    //Proc to use when you 100% want to try to infect someone (ignoreing protective clothing and such), as long as they aren't immune
    // tg: code/datums/diseases/_MobProcs.dm:112
    public virtual async DmTask<object> ForceContractDisease(datum_disease D = null, float? make_copy = null, float? del_on_fail = null)
    {
        make_copy ??= TRUE;
        del_on_fail ??= FALSE;
        if (!Dm.truthy(CanContractDisease(D)))
        {
            if (Dm.truthy(del_on_fail))
                qdel(D);
            return FALSE;
        }
        if (!Dm.truthy(await D.try_infect(this, make_copy)))
        {
            if (Dm.truthy(del_on_fail))
                qdel(D);
            return FALSE;
        }
        return TRUE;
    }

    /// Checks if this mob can currently spread air based diseases.
    /// Nondeterministic
    // tg: code/datums/diseases/_MobProcs.dm:136
    [ShouldCallParent]
    public virtual object can_spread_airborne_diseases()
    {
        if (HAS_TRAIT(this, TRAIT_NOBREATH))
            return FALSE;
        if (losebreath >= 1)
            return FALSE;
        // I don't know how you are spreading via air with no head but sure
        if (!Dm.truthy(get_bodypart(BODY_ZONE_HEAD)))
            return TRUE;
        // Check both hat and mask for bio protection
        // Anything above 50 individually is a shoe-in, and stacking two items at 25 is also a shoe-in
        obj_item_clothing hat = is_mouth_covered(ITEM_SLOT_HEAD) as obj_item_clothing;
        obj_item_clothing mask = is_mouth_covered(ITEM_SLOT_MASK) as obj_item_clothing;
        float total_prot = 2 * Dm.num(Dm.add(hat?.get_armor_rating(BIO), mask?.get_armor_rating(BIO)));
        if (prob(total_prot))
            return FALSE;

        return TRUE;
    }

    /// Checks if this mob can currently be infected by air based diseases
    /// Nondeterministic
    // tg: code/datums/diseases/_MobProcs.dm:163
    public virtual object can_be_spread_airborne_disease()
    {
        if (HAS_TRAIT(this, TRAIT_NOBREATH))
            return FALSE;
        if (losebreath >= 1)
            return FALSE;
        // Spaceacillin for infection resistance
        if (HAS_TRAIT(this, TRAIT_VIRUS_RESISTANCE) && !HAS_TRAIT(this, TRAIT_IMMUNODEFICIENCY) && prob(75))
            return FALSE;
        // Bio check for head AND mask
        // Meaning if we're masked up and wearing a dome, we are very likely never getting sick
        obj_item_clothing hat = is_mouth_covered(ITEM_SLOT_HEAD) as obj_item_clothing;
        obj_item_clothing mask = is_mouth_covered(ITEM_SLOT_MASK) as obj_item_clothing;
        float total_prot = Dm.num(Dm.add(hat?.get_armor_rating(BIO), mask?.get_armor_rating(BIO)));
        if (prob(total_prot))
            return FALSE;

        return TRUE;
    }
}

// tg: code/datums/diseases/_MobProcs.dm:35
public partial class mob_living_carbon : mob_living
{
    // tg: code/datums/diseases/_MobProcs.dm:35
    [DmParams("disease", "target_zone")]
    public override async DmTask<object> ContactContractDisease(datum_disease disease = null, params object[] __extra)
    {
        object target_zone = Dm.arg(__extra, 0);
        if (!Dm.truthy(CanContractDisease(disease)))
            return FALSE;

        bool passed = true;

        float head_chance = 80;
        float body_chance = 100;
        float hands_chance = 35f / 2;
        float feet_chance = 15f / 2;

        if (prob(Dm.div(15, disease.spreading_modifier)))
            return null;

        if (satiety > 0 && prob(satiety / 2)) // positive satiety makes it harder to contract the disease.
            return null;

        if (!Dm.truthy(target_zone))
        {
            target_zone = pick_weight(Dm.list(
                Dm.kv(BODY_ZONE_HEAD, head_chance),
                Dm.kv(BODY_ZONE_CHEST, body_chance),
                Dm.kv(BODY_ZONE_R_ARM, hands_chance),
                Dm.kv(BODY_ZONE_L_ARM, hands_chance),
                Dm.kv(BODY_ZONE_R_LEG, feet_chance),
                Dm.kv(BODY_ZONE_L_LEG, feet_chance)
            ));
        }
        else
            target_zone = check_zone(target_zone);

        if (ishuman(this))
        {
            mob_living_carbon_human infecting_human = (mob_living_carbon_human)this;

            if (HAS_TRAIT(infecting_human, TRAIT_VIRUS_RESISTANCE) && !HAS_TRAIT(infecting_human, TRAIT_IMMUNODEFICIENCY) && prob(75))
                return null;

            if (Dm.eq(target_zone, BODY_ZONE_HEAD))
            {
                if (isobj(infecting_human.head))
                    passed = prob(100 - Dm.num(infecting_human.head.get_armor_rating(BIO)));
                if (passed && isobj(infecting_human.wear_mask))
                    passed = prob(100 - Dm.num(infecting_human.wear_mask.get_armor_rating(BIO)));
                if (passed && isobj(infecting_human.wear_neck))
                    passed = prob(100 - Dm.num(infecting_human.wear_neck.get_armor_rating(BIO)));
            }
            else if (Dm.eq(target_zone, BODY_ZONE_CHEST))
            {
                if (isobj(infecting_human.wear_suit))
                    passed = prob(100 - Dm.num(infecting_human.wear_suit.get_armor_rating(BIO)));
                if (passed && isobj(infecting_human.w_uniform))
                    passed = prob(100 - Dm.num(infecting_human.w_uniform.get_armor_rating(BIO)));
            }
            else if (Dm.eq(target_zone, BODY_ZONE_L_ARM) || Dm.eq(target_zone, BODY_ZONE_R_ARM))
            {
                if (isobj(infecting_human.wear_suit) && Dm.truthy(Dm.bitand(infecting_human.wear_suit.body_parts_covered, HANDS)))
                    passed = prob(100 - Dm.num(infecting_human.wear_suit.get_armor_rating(BIO)));
                if (passed && isobj(infecting_human.gloves))
                    passed = prob(100 - Dm.num(infecting_human.gloves.get_armor_rating(BIO)));
            }
            else if (Dm.eq(target_zone, BODY_ZONE_L_LEG) || Dm.eq(target_zone, BODY_ZONE_R_LEG))
            {
                if (isobj(infecting_human.wear_suit) && Dm.truthy(Dm.bitand(infecting_human.wear_suit.body_parts_covered, FEET)))
                    passed = prob(100 - Dm.num(infecting_human.wear_suit.get_armor_rating(BIO)));
                if (passed && isobj(infecting_human.shoes))
                    passed = prob(100 - Dm.num(infecting_human.shoes.get_armor_rating(BIO)));
            }
        }

        if (passed)
            await disease.try_infect(this);
        return null;
    }

    // tg: code/datums/diseases/_MobProcs.dm:155
    public override object can_spread_airborne_diseases()
    {
        if (@internal != null || external != null)
            return FALSE;

        return base.can_spread_airborne_diseases();
    }

    // tg: code/datums/diseases/_MobProcs.dm:181
    public override object can_be_spread_airborne_disease()
    {
        // Using an isolated air supply is also effective
        if ((@internal != null || external != null) && prob(75))
            return FALSE;

        return base.can_be_spread_airborne_disease();
    }
}

// tg: code/datums/diseases/_MobProcs.dm:124
public partial class mob_living_carbon_human : mob_living_carbon
{
    // tg: code/datums/diseases/_MobProcs.dm:124
    [DmParams("disease")]
    public override object CanContractDisease(datum_disease disease = null)
    {
        if (dna != null)
        {
            if (HAS_TRAIT(this, TRAIT_VIRUSIMMUNE) && !Dm.truthy(disease.bypasses_immunity))
                return FALSE;
        }
        if (Dm.truthy(disease.required_organ))
        {
            if (!Dm.truthy(disease.has_required_infectious_organ(this, disease.required_organ)))
                return FALSE;
        }

        return base.CanContractDisease(disease);
    }
}
