// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/human/life.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: the human's Life and breath translated (M2 P2, scope.md 2e "human": human/life.dm): Life, breathe,
// check_breath, handle_environment, the core temperature setters, the body temperature getters and handle_heart; M4b slice 1
// (the air that moves): the pressure and thermal protection of clothing (calculate_affecting_pressure,
// get_thermal_protection, the protection flags, get_heat_protection, get_cold_protection). has_smoke_protection is smoke
// (reagents, M8).

// tg: code/modules/mob/living/carbon/human/life.dm:122
public partial class mob_living_carbon_human : mob_living_carbon
{
    //NOTE: Breathing happens once per FOUR TICKS, unless the last breath fails. In which case it happens once per ONE TICK! So oxyloss healing is done once per 4 ticks while oxyloss damage is applied once per tick!

    // bitflags for the percentual amount of protection a piece of clothing which covers the body part offers.
    // Used with human/proc/get_heat_protection() and human/proc/get_cold_protection()
    // The values here should add up to 1.
    // Hands and feet have 2.5%, arms and legs 7.5%, each of the torso parts has 15% and the head has 30%
    // tg: code/modules/mob/living/carbon/human/life.dm:7-17  #define THERMAL_PROTECTION_HEAD 0.3 ... (file-local, #undef at :297-307)
    private const float THERMAL_PROTECTION_HEAD = 0.3f;
    private const float THERMAL_PROTECTION_CHEST = 0.15f;
    private const float THERMAL_PROTECTION_GROIN = 0.15f;
    private const float THERMAL_PROTECTION_LEG_LEFT = 0.075f;
    private const float THERMAL_PROTECTION_LEG_RIGHT = 0.075f;
    private const float THERMAL_PROTECTION_FOOT_LEFT = 0.025f;
    private const float THERMAL_PROTECTION_FOOT_RIGHT = 0.025f;
    private const float THERMAL_PROTECTION_ARM_LEFT = 0.075f;
    private const float THERMAL_PROTECTION_ARM_RIGHT = 0.075f;
    private const float THERMAL_PROTECTION_HAND_LEFT = 0.025f;
    private const float THERMAL_PROTECTION_HAND_RIGHT = 0.025f;

    // tg: code/modules/mob/living/carbon/human/life.dm:19
    public override object Life(object seconds_per_tick = null, params object[] __extra)
    {
        seconds_per_tick ??= SSMOBS_DT();
        object dot = null;
        if (HAS_TRAIT(this, TRAIT_NO_TRANSFORM))
            return dot;

        dot = base.Life(seconds_per_tick, __extra);
        if (QDELETED(this))
            return FALSE;

        // Body temperature stability and damage
        Waiting.NoSleep(((datum_species)dna.species).handle_body_temperature(this, seconds_per_tick));
        if (HAS_TRAIT(this, TRAIT_STASIS))
        {
            foreach (datum_wound iter_wound in Dm.each_cast<datum_wound>(all_wounds))
                iter_wound.on_stasis(seconds_per_tick);
            return Dm.num(stat != DEAD);
        }

        if (stat == DEAD)
            return FALSE;

        // Handle active mutations
        foreach (datum_mutation mutation in Dm.each_cast<datum_mutation>(dna.mutations))
        {
            Waiting.NoSleep(mutation.on_life(seconds_per_tick));
        }

        // Heart attack stuff
        Waiting.NoSleep(handle_heart(seconds_per_tick));
        // Handles liver failure effects, if we lack a liver
        Waiting.NoSleep(handle_liver(seconds_per_tick));
        // Crit damage but specifically for people who don't get suffocate while in crit so they can actually die eventually
        if (HAS_TRAIT(this, TRAIT_NOBREATH) && (health < crit_threshold) && !HAS_TRAIT(this, TRAIT_NOCRITDAMAGE))
            Waiting.NoSleep(adjust_brute_loss(0.5f * Dm.num(seconds_per_tick)));
        return Dm.num(stat != DEAD);
    }

    // tg: code/modules/mob/living/carbon/human/life.dm:52
    public override object calculate_affecting_pressure(object pressure = null)
    {
        bool chest_covered = !Dm.truthy(get_bodypart(BODY_ZONE_CHEST));
        bool head_covered = !Dm.truthy(get_bodypart(BODY_ZONE_HEAD));
        bool hands_covered = !Dm.truthy(get_bodypart(BODY_ZONE_L_ARM)) && !Dm.truthy(get_bodypart(BODY_ZONE_R_ARM));
        bool feet_covered = !Dm.truthy(get_bodypart(BODY_ZONE_L_LEG)) && !Dm.truthy(get_bodypart(BODY_ZONE_R_LEG));
        foreach (obj_item_clothing equipped in Dm.each<obj_item_clothing>(get_equipped_items(INCLUDE_ABSTRACT)))
        {
            if (!chest_covered && Dm.truthy(Dm.bitand(equipped.body_parts_covered, CHEST)) && Dm.truthy(Dm.bitand(equipped.clothing_flags, STOPSPRESSUREDAMAGE)))
                chest_covered = true;
            if (!head_covered && Dm.truthy(Dm.bitand(equipped.body_parts_covered, HEAD)) && Dm.truthy(Dm.bitand(equipped.clothing_flags, STOPSPRESSUREDAMAGE)))
                head_covered = true;
            // (equipped.body_parts_covered & HANDS|ARMS): & binds tighter than | in DM, so this is (covered & HANDS) | ARMS,
            // which is always true (tg's own precedence, kept)
            if (!hands_covered && Dm.truthy(Dm.bitor(Dm.bitand(equipped.body_parts_covered, HANDS), ARMS)) && Dm.truthy(Dm.bitand(equipped.clothing_flags, STOPSPRESSUREDAMAGE)))
                hands_covered = true;
            if (!feet_covered && Dm.truthy(Dm.bitor(Dm.bitand(equipped.body_parts_covered, FEET), LEGS)) && Dm.truthy(Dm.bitand(equipped.clothing_flags, STOPSPRESSUREDAMAGE)))
                feet_covered = true;
        }

        if (chest_covered && head_covered && hands_covered && feet_covered)
            return ONE_ATMOSPHERE;
        if (ismovable(loc))
        {
            /// If we're in a space with 0.5 content pressure protection, it averages the values, for example.
            atom_movable occupied_space = loc as atom_movable;
            return (occupied_space.contents_pressure_protection * ONE_ATMOSPHERE + (1 - occupied_space.contents_pressure_protection) * Dm.num(pressure));
        }
        return pressure;
    }

    // tg: code/modules/mob/living/carbon/human/life.dm:73
    public override async DmTask breathe(object seconds_per_tick = null)
    {
        if (!HAS_TRAIT(this, TRAIT_NOBREATH))
        {
            await base.breathe(seconds_per_tick);
            return;
        }
    }

    // tg: code/modules/mob/living/carbon/human/life.dm:77
    public override async DmTask<object> check_breath(datum_gas_mixture breath = null)
    {
        obj_item_organ_lungs human_lungs = get_organ_slot(ORGAN_SLOT_LUNGS) as obj_item_organ_lungs;
        if (human_lungs != null)
            return await human_lungs.check_breath(breath, this);

        float oxy_damage = SUFFOCATION_OXYLOSS + 1;
        if (stat == SOFT_CRIT || stat == HARD_CRIT)
            oxy_damage *= (HAS_TRAIT(this, TRAIT_NOCRITDAMAGE) ? 0 : SUFFOCATION_OXYLOSS_CRIT_MODIFIER);

        if (oxy_damage > 0)
            await apply_damage(oxy_damage, OXY);

        failed_last_breath = TRUE;
        object sw__1 = ((datum_species)dna?.species)?.get_breath_type();
        if (Dm.eq(sw__1, GAS_O2) || sw__1 == null) // null means use oxyloss alert by default
        {
            throw_alert(ALERT_NOT_ENOUGH_OXYGEN, typeof(atom_movable_screen_alert_not_enough_oxy));
        }
        else if (Dm.eq(sw__1, GAS_PLASMA))
        {
            throw_alert(ALERT_NOT_ENOUGH_PLASMA, typeof(atom_movable_screen_alert_not_enough_plas));
        }
        else if (Dm.eq(sw__1, GAS_CO2))
        {
            throw_alert(ALERT_NOT_ENOUGH_CO2, typeof(atom_movable_screen_alert_not_enough_co2));
        }
        else if (Dm.eq(sw__1, GAS_N2))
        {
            throw_alert(ALERT_NOT_ENOUGH_NITRO, typeof(atom_movable_screen_alert_not_enough_nitro));
        }
        else
            stack_trace(Dm.text("Unsupported breath type for species [] in check_breath()", ((datum_species)dna.species).name), "code/modules/mob/living/carbon/human/life.dm", 100);

        return FALSE;
    }

    /// Environment handlers for species
    // tg: code/modules/mob/living/carbon/human/life.dm:105
    public override async DmTask handle_environment(datum_gas_mixture environment = null, object seconds_per_tick = null, params object[] __extra)
    {
        // If we are in a cryo bed do not process life functions
        if (istype(loc, typeof(obj_machinery_cryo_cell)))
            return;

        await ((datum_species)dna.species).handle_environment(this, environment, seconds_per_tick);
    }

    // tg: code/modules/mob/living/carbon/human/life.dm:122
    public virtual void adjust_coretemperature(object amount = null, float? min_temp = null, float? max_temp = null)
    {
        min_temp ??= 0;
        max_temp ??= INFINITY;
        set_coretemperature(clamp(coretemperature + Dm.num(amount), Dm.num(min_temp), Dm.num(max_temp)));
    }

    // tg: code/modules/mob/living/carbon/human/life.dm:125
    public virtual void set_coretemperature(object value = null)
    {
        if (Dm.truthy(_listen_lookup?[COMSIG_HUMAN_CORETEMP_CHANGE]))
            _SendSignal(COMSIG_HUMAN_CORETEMP_CHANGE, Dm.list(this, coretemperature, value));
        coretemperature = Dm.num(value);
    }

    /**
     * get_body_temperature Returns the body temperature with any modifications applied
     *
     * This applies the result from proc/get_body_temp_normal_change() against the bodytemp_normal
     * for the species and returns the result
     *
     * arguments:
     * * apply_change (optional) Default True This applies the changes to body temperature normal
     */
    // tg: code/modules/mob/living/carbon/human/life.dm:139
    public override object get_body_temp_normal(float? apply_change = null)
    {
        apply_change ??= TRUE;
        if (!Dm.truthy(apply_change))
            return ((datum_species)dna.species).bodytemp_normal;
        return Dm.add(((datum_species)dna.species).bodytemp_normal, get_body_temp_normal_change());
    }

    // tg: code/modules/mob/living/carbon/human/life.dm:144
    public override object get_body_temp_heat_damage_limit()
    {
        return ((datum_species)dna.species).bodytemp_heat_damage_limit;
    }

    // tg: code/modules/mob/living/carbon/human/life.dm:147
    public override object get_body_temp_cold_damage_limit()
    {
        return ((datum_species)dna.species).bodytemp_cold_damage_limit;
    }

    // tg: code/modules/mob/living/carbon/human/life.dm:149
    public virtual object get_thermal_protection()
    {
        float thermal_protection = 0; //Simple check to estimate how protected we are against multiple temperatures
        if (wear_suit != null)
        {
            if (Dm.truthy(Dm.bitand(wear_suit.heat_protection, CHEST)) && Dm.num(wear_suit.max_heat_protection_temperature) >= FIRE_SUIT_MAX_TEMP_PROTECT)
                thermal_protection += (Dm.num(wear_suit.max_heat_protection_temperature) * 0.7f);
        }
        if (head != null)
        {
            if (Dm.truthy(Dm.bitand(head.heat_protection, HEAD)) && Dm.num(head.max_heat_protection_temperature) >= FIRE_HELM_MAX_TEMP_PROTECT)
                thermal_protection += (Dm.num(head.max_heat_protection_temperature) * THERMAL_PROTECTION_HEAD);
        }
        thermal_protection = round(thermal_protection);
        return thermal_protection;
    }

    //END FIRE CODE

    //This proc returns a number made up of the flags for body parts which you are protected on. (such as HEAD, CHEST, GROIN, etc. See setup.dm for the full list)
    // tg: code/modules/mob/living/carbon/human/life.dm:163
    public virtual object get_heat_protection_flags(object temperature = null) //Temperature is the temperature you're being exposed to.
    {
        float thermal_protection_flags = 0;
        //Handle normal clothing
        if (head != null)
        {
            if (Dm.truthy(head.max_heat_protection_temperature) && Dm.num(head.max_heat_protection_temperature) >= Dm.num(temperature))
                thermal_protection_flags = Dm.bitor(thermal_protection_flags, Dm.num(head.heat_protection));
        }
        if (wear_suit != null)
        {
            if (Dm.truthy(wear_suit.max_heat_protection_temperature) && Dm.num(wear_suit.max_heat_protection_temperature) >= Dm.num(temperature))
                thermal_protection_flags = Dm.bitor(thermal_protection_flags, Dm.num(wear_suit.heat_protection));
        }
        if (w_uniform != null)
        {
            if (Dm.truthy(w_uniform.max_heat_protection_temperature) && Dm.num(w_uniform.max_heat_protection_temperature) >= Dm.num(temperature))
                thermal_protection_flags = Dm.bitor(thermal_protection_flags, Dm.num(w_uniform.heat_protection));
        }
        if (shoes != null)
        {
            if (Dm.truthy(shoes.max_heat_protection_temperature) && Dm.num(shoes.max_heat_protection_temperature) >= Dm.num(temperature))
                thermal_protection_flags = Dm.bitor(thermal_protection_flags, Dm.num(shoes.heat_protection));
        }
        if (gloves != null)
        {
            if (Dm.truthy(gloves.max_heat_protection_temperature) && Dm.num(gloves.max_heat_protection_temperature) >= Dm.num(temperature))
                thermal_protection_flags = Dm.bitor(thermal_protection_flags, Dm.num(gloves.heat_protection));
        }
        if (wear_mask != null)
        {
            if (Dm.truthy(wear_mask.max_heat_protection_temperature) && Dm.num(wear_mask.max_heat_protection_temperature) >= Dm.num(temperature))
                thermal_protection_flags = Dm.bitor(thermal_protection_flags, Dm.num(wear_mask.heat_protection));
        }

        return thermal_protection_flags;
    }

    // tg: code/modules/mob/living/carbon/human/life.dm:187
    public override object get_heat_protection(object temperature = null)
    {
        float thermal_protection_flags = Dm.num(get_heat_protection_flags(temperature));
        float thermal_protection = Dm.num(heat_protection);

        // Apply clothing items protection
        if (thermal_protection_flags != 0)
        {
            if (Dm.truthy(Dm.bitand(thermal_protection_flags, HEAD)))
                thermal_protection += THERMAL_PROTECTION_HEAD;
            if (Dm.truthy(Dm.bitand(thermal_protection_flags, CHEST)))
                thermal_protection += THERMAL_PROTECTION_CHEST;
            if (Dm.truthy(Dm.bitand(thermal_protection_flags, GROIN)))
                thermal_protection += THERMAL_PROTECTION_GROIN;
            if (Dm.truthy(Dm.bitand(thermal_protection_flags, LEG_LEFT)))
                thermal_protection += THERMAL_PROTECTION_LEG_LEFT;
            if (Dm.truthy(Dm.bitand(thermal_protection_flags, LEG_RIGHT)))
                thermal_protection += THERMAL_PROTECTION_LEG_RIGHT;
            if (Dm.truthy(Dm.bitand(thermal_protection_flags, FOOT_LEFT)))
                thermal_protection += THERMAL_PROTECTION_FOOT_LEFT;
            if (Dm.truthy(Dm.bitand(thermal_protection_flags, FOOT_RIGHT)))
                thermal_protection += THERMAL_PROTECTION_FOOT_RIGHT;
            if (Dm.truthy(Dm.bitand(thermal_protection_flags, ARM_LEFT)))
                thermal_protection += THERMAL_PROTECTION_ARM_LEFT;
            if (Dm.truthy(Dm.bitand(thermal_protection_flags, ARM_RIGHT)))
                thermal_protection += THERMAL_PROTECTION_ARM_RIGHT;
            if (Dm.truthy(Dm.bitand(thermal_protection_flags, HAND_LEFT)))
                thermal_protection += THERMAL_PROTECTION_HAND_LEFT;
            if (Dm.truthy(Dm.bitand(thermal_protection_flags, HAND_RIGHT)))
                thermal_protection += THERMAL_PROTECTION_HAND_RIGHT;
        }

        return min(1, round(thermal_protection, 0.001f));
    }

    //See proc/get_heat_protection_flags(temperature) for the description of this proc.
    // tg: code/modules/mob/living/carbon/human/life.dm:219
    public virtual object get_cold_protection_flags(object temperature = null)
    {
        float thermal_protection_flags = 0;
        //Handle normal clothing

        if (head != null)
        {
            if (Dm.truthy(head.min_cold_protection_temperature) && Dm.num(head.min_cold_protection_temperature) <= Dm.num(temperature))
                thermal_protection_flags = Dm.bitor(thermal_protection_flags, Dm.num(head.cold_protection));
        }
        if (wear_suit != null)
        {
            if (Dm.truthy(wear_suit.min_cold_protection_temperature) && Dm.num(wear_suit.min_cold_protection_temperature) <= Dm.num(temperature))
                thermal_protection_flags = Dm.bitor(thermal_protection_flags, Dm.num(wear_suit.cold_protection));
        }
        if (w_uniform != null)
        {
            if (Dm.truthy(w_uniform.min_cold_protection_temperature) && Dm.num(w_uniform.min_cold_protection_temperature) <= Dm.num(temperature))
                thermal_protection_flags = Dm.bitor(thermal_protection_flags, Dm.num(w_uniform.cold_protection));
        }
        if (shoes != null)
        {
            if (Dm.truthy(shoes.min_cold_protection_temperature) && Dm.num(shoes.min_cold_protection_temperature) <= Dm.num(temperature))
                thermal_protection_flags = Dm.bitor(thermal_protection_flags, Dm.num(shoes.cold_protection));
        }
        if (gloves != null)
        {
            if (Dm.truthy(gloves.min_cold_protection_temperature) && Dm.num(gloves.min_cold_protection_temperature) <= Dm.num(temperature))
                thermal_protection_flags = Dm.bitor(thermal_protection_flags, Dm.num(gloves.cold_protection));
        }
        if (wear_mask != null)
        {
            if (Dm.truthy(wear_mask.min_cold_protection_temperature) && Dm.num(wear_mask.min_cold_protection_temperature) <= Dm.num(temperature))
                thermal_protection_flags = Dm.bitor(thermal_protection_flags, Dm.num(wear_mask.cold_protection));
        }

        return thermal_protection_flags;
    }

    // tg: code/modules/mob/living/carbon/human/life.dm:244
    public override object get_cold_protection(object temperature = null)
    {
        // There is an occasional bug where the temperature is miscalculated in areas with small amounts of gas.
        // This is necessary to ensure that does not affect this calculation.
        // Space's temperature is 2.7K and most suits that are intended to protect against any cold, protect down to 2.0K.
        temperature = max(Dm.num(temperature), 2.7f);
        float thermal_protection_flags = Dm.num(get_cold_protection_flags(temperature));
        float thermal_protection = Dm.num(cold_protection);

        // Apply clothing items protection
        if (thermal_protection_flags != 0)
        {
            if (Dm.truthy(Dm.bitand(thermal_protection_flags, HEAD)))
                thermal_protection += THERMAL_PROTECTION_HEAD;
            if (Dm.truthy(Dm.bitand(thermal_protection_flags, CHEST)))
                thermal_protection += THERMAL_PROTECTION_CHEST;
            if (Dm.truthy(Dm.bitand(thermal_protection_flags, GROIN)))
                thermal_protection += THERMAL_PROTECTION_GROIN;
            if (Dm.truthy(Dm.bitand(thermal_protection_flags, LEG_LEFT)))
                thermal_protection += THERMAL_PROTECTION_LEG_LEFT;
            if (Dm.truthy(Dm.bitand(thermal_protection_flags, LEG_RIGHT)))
                thermal_protection += THERMAL_PROTECTION_LEG_RIGHT;
            if (Dm.truthy(Dm.bitand(thermal_protection_flags, FOOT_LEFT)))
                thermal_protection += THERMAL_PROTECTION_FOOT_LEFT;
            if (Dm.truthy(Dm.bitand(thermal_protection_flags, FOOT_RIGHT)))
                thermal_protection += THERMAL_PROTECTION_FOOT_RIGHT;
            if (Dm.truthy(Dm.bitand(thermal_protection_flags, ARM_LEFT)))
                thermal_protection += THERMAL_PROTECTION_ARM_LEFT;
            if (Dm.truthy(Dm.bitand(thermal_protection_flags, ARM_RIGHT)))
                thermal_protection += THERMAL_PROTECTION_ARM_RIGHT;
            if (Dm.truthy(Dm.bitand(thermal_protection_flags, HAND_LEFT)))
                thermal_protection += THERMAL_PROTECTION_HAND_LEFT;
            if (Dm.truthy(Dm.bitand(thermal_protection_flags, HAND_RIGHT)))
                thermal_protection += THERMAL_PROTECTION_HAND_RIGHT;
        }

        return min(1, round(thermal_protection, 0.001f));
    }

    // tg: code/modules/mob/living/carbon/human/life.dm:285
    public virtual async DmTask handle_heart(object seconds_per_tick = null)
    {
        bool we_breath = !HAS_TRAIT_FROM(this, TRAIT_NOBREATH, SPECIES_TRAIT);

        if (!Dm.truthy(undergoing_cardiac_arrest()))
            return;

        if (we_breath)
        {
            await adjust_oxy_loss(4 * Dm.num(seconds_per_tick));
            await Unconscious(80);
        }
        // Tissues die without blood circulation
        await adjust_brute_loss(1 * Dm.num(seconds_per_tick));
    }
}
