// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/organs/_organ.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The M2 subset (M2 P2, scope.md 2e "organs": INITIALIZE_IMMEDIATE, Initialize, Destroy, the organ traits and status effects,
// on_death, on_life, the damage procs and thresholds, failure, availability and replaceability, regenerate_organs, get_all_slots);
// examine, eating, scans and the wardrobe with M8 and M9; gaps OG1 the decay of a loose organ (process) and its action slot check.

// tg: code/modules/surgery/organs/_organ.dm:1
[DmPath("/obj/item/organ")]
public partial class obj_item_organ : obj_item
{
    // tg: code/modules/surgery/organs/_organ.dm:2-6
    public obj_item_organ()
    {
        name = "organ";
        icon = Dm.resource("icons/obj/medical/organs/organs.dmi");
        abstract_type = typeof(obj_item_organ);
        w_class = WEIGHT_CLASS_SMALL;
        throwforce = 0;
    }

    // tg: code/modules/surgery/organs/_organ.dm:49  the object-creating defaults (rules 10.2)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        food_reagents = Dm.list(Dm.kv(typeof(datum_reagent_consumable_nutriment), 5));
    }

    /// The mob that owns this organ.
    // tg: code/modules/surgery/organs/_organ.dm:8
    public mob_living_carbon owner = null;
    /// Reference to the limb we're inside of
    // tg: code/modules/surgery/organs/_organ.dm:10
    public obj_item_bodypart bodypart_owner;
    /// The cached info about the blood this organ belongs to
    // tg: code/modules/surgery/organs/_organ.dm:12
    public DmList blood_dna_info; // not every organ spawns inside a person
    /// The body zone this organ is supposed to inhabit.
    // tg: code/modules/surgery/organs/_organ.dm:14
    public string zone = BODY_ZONE_CHEST;
    /**
     * The organ slot this organ is supposed to inhabit. This should be unique by type. (Lungs, Appendix, Stomach, etc)
     * Do NOT add slots with matching names to different zones - it will break the organs_slot list!
     */
    // tg: code/modules/surgery/organs/_organ.dm:19
    public object slot;
    /// Random flags that describe this organ
    // tg: code/modules/surgery/organs/_organ.dm:21
    public float organ_flags = Dm.bitor(ORGAN_ORGANIC, ORGAN_EDIBLE);
    /// Maximum damage the organ can take, ever.
    // tg: code/modules/surgery/organs/_organ.dm:23
    public float maxHealth = STANDARD_ORGAN_THRESHOLD;
    /**
     * Total damage this organ has sustained.
     * Should only ever be modified by apply_organ_damage!
     */
    // tg: code/modules/surgery/organs/_organ.dm:28  (float?: the null-use grep matches isnull(damage),
    // mob/living/basic/illusion/illusion.dm:79 and datums/elements/eyestab.dm:24)
    public float? damage = 0;
    /// Healing factor and decay factor function on % of maxhealth, and do not work by applying a static number per tick
    // tg: code/modules/surgery/organs/_organ.dm:30
    public float healing_factor = 0; //fraction of maxhealth healed per on_life(), set to 0 for generic organs
    // tg: code/modules/surgery/organs/_organ.dm:31
    public float decay_factor = 0; //same as above but when without a living owner, set to 0 for generic organs
    // tg: code/modules/surgery/organs/_organ.dm:32
    public float high_threshold = STANDARD_ORGAN_THRESHOLD * 0.45f; //when severe organ damage occurs
    // tg: code/modules/surgery/organs/_organ.dm:33
    public float low_threshold = STANDARD_ORGAN_THRESHOLD * 0.1f; //when minor organ damage occurs
    // tg: code/modules/surgery/organs/_organ.dm:34
    public object severe_cooldown; //cooldown for severe effects, used for synthetic organ emp effects.

    // Organ variables for determining what we alert the owner with when they pass/clear the damage thresholds
    // tg: code/modules/surgery/organs/_organ.dm:37
    public float prev_damage = 0;
    // tg: code/modules/surgery/organs/_organ.dm:38
    public object low_threshold_passed;
    // tg: code/modules/surgery/organs/_organ.dm:39
    public object high_threshold_passed;
    // tg: code/modules/surgery/organs/_organ.dm:40
    public object now_failing;
    // tg: code/modules/surgery/organs/_organ.dm:41
    public object now_fixed;
    // tg: code/modules/surgery/organs/_organ.dm:42
    public object high_threshold_cleared;
    // tg: code/modules/surgery/organs/_organ.dm:43
    public object low_threshold_cleared;

    /// When set to false, this can't be used in surgeries and such - Honestly a terrible variable.
    // tg: code/modules/surgery/organs/_organ.dm:46
    public float useable = TRUE;

    /// Food reagents if the organ is edible
    // tg: code/modules/surgery/organs/_organ.dm:49
    public DmList food_reagents;
    /// Foodtypes if the organ is edible
    // tg: code/modules/surgery/organs/_organ.dm:51
    public float foodtype_flags = Dm.bitor(Dm.bitor(RAW, MEAT), GORE);
    /// Overrides tastes if the organ is edible
    // tg: code/modules/surgery/organs/_organ.dm:53
    public object food_tastes;
    /// The size of the reagent container if the organ is edible
    // tg: code/modules/surgery/organs/_organ.dm:55
    public float reagent_vol = 10;

    /// Time this organ has failed for
    // tg: code/modules/surgery/organs/_organ.dm:58
    public float failure_time = 0;
    /// Do we affect the appearance of our mob. Used to save time in preference code
    // tg: code/modules/surgery/organs/_organ.dm:60
    public float visual = TRUE;
    /**
     * Traits that are given to the holder of the organ.
     * If you want an effect that changes this, don't add directly to this. Use the add_organ_trait() proc.
     */
    // tg: code/modules/surgery/organs/_organ.dm:65
    public DmList organ_traits;
    /// Status Effects that are given to the holder of the organ.
    // tg: code/modules/surgery/organs/_organ.dm:67
    public DmList organ_effects;
    /// String displayed when the organ has decayed.
    // tg: code/modules/surgery/organs/_organ.dm:69
    public string failing_desc = "has decayed for too long, and has turned a sickly color. It probably won't work without repairs.";
    /// Assoc list of alternate zones where this can organ be slotted to organ slot for that zone
    // tg: code/modules/surgery/organs/_organ.dm:71
    public DmList valid_zones = null;
    /// The cell line we can spawn on us
    // tg: code/modules/surgery/organs/_organ.dm:73
    public object cell_line = null;
    /// The minimum cells we can spawn
    // tg: code/modules/surgery/organs/_organ.dm:75
    public float cells_minimum = 0;
    /// The maximum cells we can spawn
    // tg: code/modules/surgery/organs/_organ.dm:77
    public float cells_maximum = 0;
}

// tg: code/modules/surgery/organs/_organ.dm:83 (interleaved types, rules 1.9)
public partial class obj_item_organ : obj_item
{
    // Players can look at prefs before atoms SS init, and without this
    // they would not be able to see external organs, such as moth wings.
    // This is also necessary because assets SS is before atoms, and so
    // any nonhumans created in that time would experience the same effect.
    // The New() that INITIALIZE_IMMEDIATE(/obj/item/organ) defines (__DEFINES/subsystems.dm:96; inline, rules 11.1 (c), (d))
    // tg: code/modules/surgery/organs/_organ.dm:83
    [DmParams("loc")]
    public override async DmTask New(params object[] __extra)
    {
        object loc = Dm.arg(__extra, 0);
        await base.New(Dm.forward(__extra, loc));
        if (!Dm.truthy(Dm.bitand(flags_1, INITIALIZED_1)))
        {
            float previous_initialized_value = SSatoms.initialized;
            SSatoms.initialized = INITIALIZATION_INNEW_MAPLOAD;
            DmList args = Dm.args(Dm.forward(__extra, loc));
            args[1] = TRUE;
            SSatoms.InitAtom(this, FALSE, args);
            SSatoms.initialized = previous_initialized_value;
        }
    }

    // tg: code/modules/surgery/organs/_organ.dm:85
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        Dm.combine(ref organ_flags, ORGAN_VIRGIN);
        blood_dna_info = Dm.list(Dm.kv("Unknown DNA", get_blood_type(typeof(datum_blood_type_human_o_plus))));
        if (Dm.truthy(Dm.bitand(organ_flags, ORGAN_EDIBLE)))
        {
            Waiting.NoSleep(AddComponentFrom(
                SOURCE_EDIBLE_INNATE,
                typeof(datum_component_edible),
                Dm.named("initial_reagents", food_reagents),
                Dm.named("foodtypes", foodtype_flags),
                Dm.named("volume", reagent_vol),
                Dm.named("tastes", food_tastes),
                Dm.named("after_eat", Dm.@new<datum_callback>(this, nameof(OnEatFrom)))
            ));
            RegisterSignal(this, COMSIG_FOOD_ATTEMPT_EAT, nameof(block_nom));
        }

        if (Dm.truthy(bodypart_overlay))
        {
            setup_bodypart_overlay();
        }

        if (Dm.truthy(cell_line) && Dm.truthy(Dm.bitand(organ_flags, ORGAN_ORGANIC)))
        {
            // AddElement(/datum/element/swabable, cell_line, cell_line_amount = rand(cells_minimum, cells_maximum)): cytology
            // swabs (M8); GAP(lead): /datum/element/swabable is not declared.
            Dm.Deferred("code/modules/surgery/organs/_organ.dm:106", "Initialize: AddElement(/datum/element/swabable, ...): a swabable organ (cytology, M8)");
        }

        START_PROCESSING(SSobj, this);
        return dot;
    }

    // tg: code/modules/surgery/organs/_organ.dm:109
    public override object Destroy(float? force = null)
    {
        if (bodypart_owner != null && owner == null && !QDELETED(bodypart_owner))
            Waiting.NoSleep(bodypart_remove(bodypart_owner));
        else if (owner != null && QDESTROYING(owner))
            // The mob is being deleted, don't update the mob
            Waiting.NoSleep(Remove(owner, special: TRUE));
        else if (owner != null)
            Waiting.NoSleep(Remove(owner));
        else
            STOP_PROCESSING(SSobj, this);
        return base.Destroy(force);
    }
}

// tg: code/modules/surgery/organs/_organ.dm:122
public partial class obj_item_organ : obj_item
{
    // tg: code/modules/surgery/organs/_organ.dm:122
    public virtual void add_organ_trait(object trait = null)
    {
        LAZYOR(ref organ_traits, trait);
        if (owner == null)
            return;
        ADD_TRAIT(owner, trait, REF(this));
    }

    // tg: code/modules/surgery/organs/_organ.dm:129
    public virtual void remove_organ_trait(object trait = null)
    {
        LAZYREMOVE(ref organ_traits, trait);
        if (owner == null)
            return;
        REMOVE_TRAIT(owner, trait, REF(this));
    }

    // tg: code/modules/surgery/organs/_organ.dm:136
    public virtual async DmTask add_organ_status(object status = null)
    {
        LAZYADD(ref organ_effects, status);
        if (owner == null)
            return;
        await owner.apply_status_effect(status as Type, type);
    }

    // tg: code/modules/surgery/organs/_organ.dm:143
    public virtual void remove_organ_status(object status = null)
    {
        LAZYREMOVE(ref organ_effects, status);
        if (owner == null)
            return;
        owner.remove_status_effect(status, type);
    }

    // tg: code/modules/surgery/organs/_organ.dm:149
    public virtual async DmTask on_find(mob_living finder = null)
    {
        return;
    }

    // tg: code/modules/surgery/organs/_organ.dm:152
    public override async DmTask<object> wash(object clean_types = null, params object[] __extra)
    {
        object dot = await base.wash(clean_types, __extra);
        if (!Dm.truthy(dot))
            return dot;
        // always add the original dna to the organ after it's washed
        if (!Dm.truthy(IS_ROBOTIC_ORGAN(this)) && Dm.truthy(Dm.bitand(clean_types, CLEAN_TYPE_BLOOD)))
            await add_blood_DNA(blood_dna_info);
        return dot;
    }

    // tg: code/modules/surgery/organs/_organ.dm:160
    public virtual async DmTask on_death(object seconds_per_tick = null)
    {
        if (Dm.truthy(Dm.bitand(organ_flags, Dm.bitor(ORGAN_ROBOTIC, ORGAN_FROZEN))))
            return;

        if (owner != null)
        {
            if (owner.bodytemperature > T0C)
            {
                float air_temperature_factor = min((owner.bodytemperature - T0C) / 20, 1);
                await apply_organ_damage(decay_factor * maxHealth * Dm.num(seconds_per_tick) * air_temperature_factor);
            }
        }
        else
        {
            datum_gas_mixture exposed_air = await return_air() as datum_gas_mixture;
            if (exposed_air != null && exposed_air.temperature > T0C)
            {
                float air_temperature_factor = min((exposed_air.temperature - T0C) / 20, 1);
                await apply_organ_damage(decay_factor * maxHealth * Dm.num(seconds_per_tick) * air_temperature_factor);
            }
        }
    }

    // tg: code/modules/surgery/organs/_organ.dm:174
    [ShouldCallParent]
    public virtual async DmTask on_life(object seconds_per_tick = null)
    {
        if (Dm.truthy(Dm.bitand(organ_flags, ORGAN_FAILING)))
        {
            await handle_failing_organs(seconds_per_tick);
            return;
        }

        if (failure_time > 0)
            failure_time--;

        if (Dm.truthy(Dm.bitand(organ_flags, ORGAN_EMP))) //Synthetic organ has been emped, is now failing.
        {
            await apply_organ_damage(decay_factor * maxHealth * Dm.num(seconds_per_tick));
            return;
        }

        if (!Dm.truthy(damage)) // No sense healing if you're not even hurt bro
            return;

        if (Dm.truthy(IS_ROBOTIC_ORGAN(this))) // Robotic organs don't naturally heal
            return;

        ///Damage decrements by a percent of its maxhealth
        float healing_amount = healing_factor;
        ///Damage decrements again by a percent of its maxhealth, up to a total of 4 extra times depending on the owner's health
        healing_amount += (owner.satiety > 0) ? (4 * healing_factor * owner.satiety / MAX_SATIETY) : 0;
        await apply_organ_damage(-healing_amount * maxHealth * Dm.num(seconds_per_tick), damage); // pass curent damage incase we are over cap
    }

    // tg: code/modules/surgery/organs/_organ.dm:200
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);

        Dm.append(ref dot, zones_tip());

        if (HAS_MIND_TRAIT(user, TRAIT_ENTRAILS_READER) || isobserver(user))
        {
            if (HAS_TRAIT(this, TRAIT_CLIENT_STARTING_ORGAN))
                Dm.append(ref dot, span_info(I18n.T("Lived in and homely. Proven to work. This should fetch a high price on the market.")));
        }

        if (Dm.truthy(Dm.bitand(organ_flags, ORGAN_FAILING)))
        {
            Dm.append(ref dot, span_warning(I18n.T("[] []", this, failing_desc)));
            return dot;
        }

        if (damage > high_threshold)
        {
            if (Dm.truthy(IS_ROBOTIC_ORGAN(this)))
            {
                Dm.append(ref dot, span_warning(I18n.T("[] seems to be malfunctioning.", this)));
                return dot;
            }
            Dm.append(ref dot, span_warning(I18n.T("[] is starting to look discolored.", this)));
        }
        return dot;
    }

    /// Returns a line to be displayed regarding valid insertion zones
    // tg: code/modules/surgery/organs/_organ.dm:220
    public virtual object zones_tip()
    {
        if (!Dm.truthy(valid_zones))
            return span_notice(I18n.T("It should be inserted in the [].", parse_zone(zone)));

        DmList fit_zones = Dm.list();
        foreach (object valid_zone in Dm.each(valid_zones))
            Dm.append(ref fit_zones, parse_zone(valid_zone));
        return span_notice(I18n.T("It should be inserted in the [].", english_list(fit_zones, and_text: " or ")));
    }

    // tg: code/modules/surgery/organs/_organ.dm:230
    public virtual void exit_wardrobe()
    {
        START_PROCESSING(SSobj, this);
        if (bodypart_overlay != null)
            (bodypart_overlay as datum_bodypart_overlay_mutant).imprint_on_next_insertion = TRUE;
    }

    // tg: code/modules/surgery/organs/_organ.dm:235
    public virtual async DmTask enter_wardrobe()
    {
        STOP_PROCESSING(SSobj, this);
    }

    // tg: code/modules/surgery/organs/_organ.dm:238 (gaps OG1: a loose organ decays in SSobj)
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        Waiting.NoSleep(on_death(seconds_per_tick)); //Kinda hate doing it like this, but I really don't want to call process directly.
        return null;
    }

    // tg: code/modules/surgery/organs/_organ.dm:245 (gaps OG1)
    [DmParams("slot", "user")]
    public override object item_action_slot_check(object slot = null, mob user = null, datum_action action = null)
    {
        return null; //so we don't grant the organ's action to mobs who pick up the organ.
    }

    // tg: code/modules/surgery/organs/_organ.dm:241
    public virtual void OnEatFrom(object eater = null, object feeder = null)
    {
        // You can't use it anymore after eating it
        Dm.combine(ref organ_flags, ORGAN_UNUSABLE);
    }

    // tg: code/modules/surgery/organs/_organ.dm:249
    public virtual async DmTask<object> apply_organ_damage(object damage_amount = null, object maximum = null, float? required_organ_flag = null)
    {
        maximum ??= maxHealth;
        required_organ_flag ??= NONE;
        object dot = null;
        if (!Dm.truthy(damage_amount)) //Micro-optimization.
            return FALSE;
        maximum = clamp(Dm.num(maximum), 0, maxHealth); // the logical max is, our max
        if (Dm.num(maximum) < Dm.num(damage))
            return FALSE;
        if (Dm.truthy(required_organ_flag) && !Dm.truthy(Dm.bitand(organ_flags, required_organ_flag)))
            return FALSE;
        damage = clamp(Dm.num(damage) + Dm.num(damage_amount), 0, Dm.num(maximum));
        // SEND_SIGNAL(src, COMSIG_ORGAN_ADJUST_DAMAGE, damage_amount, maximum, required_organ_flag) (rules 11.1 (e), a statement)
        if (Dm.truthy(this._listen_lookup?[COMSIG_ORGAN_ADJUST_DAMAGE]))
            this._SendSignal(COMSIG_ORGAN_ADJUST_DAMAGE, Dm.list(this, damage_amount, maximum, required_organ_flag));
        dot = (prev_damage - Dm.num(damage)); // return net damage
        object message = await check_damage_thresholds();
        prev_damage = Dm.num(damage);

        if (Dm.truthy(message) && owner != null && !IS_UNCONSCIOUS(owner))
            to_chat(owner, message);
        return dot;
    }

    // tg: code/modules/surgery/organs/_organ.dm:267
    public virtual async DmTask<object> set_organ_damage(object damage_amount = null, float? required_organ_flag = null)
    {
        required_organ_flag ??= NONE;
        return await apply_organ_damage(Dm.num(damage_amount) - Dm.num(damage), required_organ_flag: required_organ_flag);
    }

    // tg: code/modules/surgery/organs/_organ.dm:276
    [ShouldCallParent]
    public virtual async DmTask<object> check_damage_thresholds()
    {
        if (Dm.num(damage) == prev_damage)
            return null;
        float delta = Dm.num(damage) - prev_damage;
        object message = "";
        if (delta > 0)
        {
            if (Dm.num(damage) > low_threshold && prev_damage <= low_threshold)
            {
                await on_low_damage_received();
                message = low_threshold_passed;
            }
            if (Dm.num(damage) > high_threshold && prev_damage <= high_threshold)
            {
                await on_high_damage_received();
                message = high_threshold_passed;
            }
            if (Dm.num(damage) >= maxHealth)
            {
                Dm.combine(ref organ_flags, ORGAN_FAILING);
                await on_begin_failure();
                message = now_failing;
            }
            return message;
        }

        if (prev_damage == maxHealth)
        {
            Dm.mask(ref organ_flags, Dm.bitnot(ORGAN_FAILING));
            on_failure_recovery();
            message = now_fixed;
        }
        if (prev_damage > high_threshold && Dm.num(damage) <= high_threshold)
        {
            await on_high_damage_healed();
            message = high_threshold_cleared;
        }
        if (prev_damage > low_threshold && Dm.num(damage) <= low_threshold)
        {
            on_low_damage_healed();
            message = low_threshold_cleared;
        }
        return message;
    }

    // tg: code/modules/surgery/organs/_organ.dm:314
    public virtual async DmTask on_low_damage_received()
    {
        return;
    }

    // tg: code/modules/surgery/organs/_organ.dm:318
    public virtual void on_low_damage_healed()
    {
        return;
    }

    // tg: code/modules/surgery/organs/_organ.dm:322
    public virtual async DmTask on_high_damage_received()
    {
        return;
    }

    // tg: code/modules/surgery/organs/_organ.dm:326
    public virtual async DmTask on_high_damage_healed()
    {
        return;
    }

    // tg: code/modules/surgery/organs/_organ.dm:330
    public virtual async DmTask on_begin_failure()
    {
        return;
    }

    // tg: code/modules/surgery/organs/_organ.dm:334
    public virtual void on_failure_recovery()
    {
        return;
    }

    // tg: code/modules/surgery/organs/_organ.dm:404
    public virtual async DmTask handle_failing_organs(object seconds_per_tick = null)
    {
        if (owner.stat == DEAD)
            return;

        failure_time += Dm.num(seconds_per_tick);
        await organ_failure(seconds_per_tick);
    }

    // tg: code/modules/surgery/organs/_organ.dm:418
    public virtual async DmTask organ_failure(object seconds_per_tick = null)
    {
        return;
    }

    // tg: code/modules/surgery/organs/_organ.dm:432
    public virtual object get_availability(datum_species owner_species = null, mob_living owner_mob = null)
    {
        return TRUE;
    }

    // tg: code/modules/surgery/organs/_organ.dm:444
    public virtual object get_replaceability(object new_organ_type = null, object expected_organ_type = null, datum_species old_species = null, float? replace_current = null)
    {
        replace_current ??= TRUE;
        // the two organ "types" are typepaths, as every caller passes them (species/regenerate_organs: get_mutant_organ_type_for_slot)
        // we don't want to remove organs that are the same as the new one
        if (Dm.eq(type, new_organ_type))
            return FALSE;

        // we dont want to remove organs that were not from the old species (such as from freak surgery or prosthetics)
        if (Dm.truthy(replace_current) || Dm.eq(type, expected_organ_type))
            return TRUE;

        return FALSE;
    }

    // tg: code/modules/surgery/organs/_organ.dm:456
    [ShouldCallParent]
    public virtual async DmTask before_organ_replacement(obj_item_organ replacement = null)
    {
        // SEND_SIGNAL(src, COMSIG_ORGAN_BEING_REPLACED, replacement) (rules 11.1 (e), a statement)
        if (Dm.truthy(this._listen_lookup?[COMSIG_ORGAN_BEING_REPLACED]))
            this._SendSignal(COMSIG_ORGAN_BEING_REPLACED, Dm.list(this, replacement));

        // If we're being replace with an identical type we should take organ damage
        if (replacement.type == type)
            await replacement.set_organ_damage(damage);
    }

    /// Called by medical scanners to get a simple summary of how healthy the organ is. Returns an empty string if things are fine.
    // tg: code/modules/surgery/organs/_organ.dm:466
    public virtual object get_status_text(object scanpower = null, object add_tooltips = null, float? colored = null)
    {
        colored ??= TRUE;
        bool c = Dm.truthy(colored);
        if (Dm.num(scanpower) >= SCANPOWER_ADVANCED && Dm.truthy(Dm.bitand(organ_flags, ORGAN_HAZARDOUS)))
            return conditional_tooltip(Dm.text("[]Harmful Foreign Body[]", c ? "<font color='#cc3333'>" : "", c ? "</font>" : ""), I18n.T("Remove surgically."), add_tooltips);

        if (Dm.truthy(Dm.bitand(organ_flags, ORGAN_EMP)))
            return conditional_tooltip(Dm.text("[]EMP-Derived Failure[]", c ? "<font color='#cc3333'>" : "", c ? "</font>" : ""), I18n.T("Repair or replace surgically."), add_tooltips);

        string tech_text = "";
        if (Dm.truthy(owner.has_reagent(typeof(datum_reagent_inverse_technetium))))
            tech_text = I18n.T("[]% damaged", round((Dm.num(damage) / maxHealth) * 100, 1));

        if (Dm.truthy(Dm.bitand(organ_flags, ORGAN_FAILING)))
            return conditional_tooltip(Dm.text("[][][]", c ? "<font color='#cc3333'>" : "", tech_text != "" ? tech_text : I18n.T("Non-Functional"), c ? "</font>" : ""), I18n.T("Repair or replace surgically."), add_tooltips);

        if (Dm.num(damage) > high_threshold)
            return conditional_tooltip(Dm.text("[][][]", c ? "<font color='#ff9933'>" : "", tech_text != "" ? tech_text : I18n.T("Severely Damaged"), c ? "</font>" : ""), healing_factor != 0 ? I18n.T("Treat with rest or use specialty medication.") : I18n.T("Repair surgically or use specialty medication."), Dm.truthy(add_tooltips) && owner.stat != DEAD);

        if (Dm.num(damage) > low_threshold)
            return conditional_tooltip(Dm.text("[][][]", c ? "<font color='#ffcc33'>" : "", tech_text != "" ? tech_text : I18n.T("Mildly Damaged"), c ? "</font>" : ""), healing_factor != 0 ? I18n.T("Treat with rest.") : I18n.T("Use specialty medication."), Dm.truthy(add_tooltips) && owner.stat != DEAD);

        if (tech_text != "")
            return Dm.text("[][][]", c ? "<font color='#33cc33'>" : "", tech_text, c ? "</font>" : "");

        return "";
    }

    /// Determines if this organ is shown when a user has condensed scans enabled
    // tg: code/modules/surgery/organs/_organ.dm:492
    public virtual object show_on_condensed_scans()
    {
        // We don't need to show *most* damaged organs as they have no effects associated
        return Dm.bitand(organ_flags, Dm.bitor(Dm.bitor(ORGAN_PROMINENT, ORGAN_HAZARDOUS), Dm.bitor(ORGAN_FAILING, ORGAN_VITAL)));
    }

    /// Similar to get_status_text, but appends the text after the damage report, for additional status info
    // tg: code/modules/surgery/organs/_organ.dm:497
    public virtual object get_status_appendix(object scanpower = null, object add_tooltips = null)
    {
        return null;
    }

    // tg: code/modules/surgery/organs/_organ.dm:513
    public virtual object feel_for_damage(object self_aware = null)
    {
        if (Dm.truthy(Dm.bitand(organ_flags, ORGAN_EXTERNAL)))
            return "";
        if (Dm.num(damage) < low_threshold)
            return "";
        if (Dm.num(damage) < high_threshold)
            return span_warning(I18n.T("[] feels a bit off.", Dm.truthy(self_aware) ? Dm.text("[]", capitalize(slot)) : I18n.T("It")));
        return span_boldwarning(I18n.T("[] feels terrible!", Dm.truthy(self_aware) ? Dm.text("[]", capitalize(slot)) : I18n.T("It")));
    }

    // tg: code/modules/surgery/organs/_organ.dm:523
    public virtual async DmTask<object> replace_into(mob_living_carbon new_owner = null)
    {
        await Insert(new_owner, special: TRUE, movement_flags: DELETE_IF_REPLACED);
        return null;
    }

    // tg: code/modules/surgery/organs/_organ.dm:527
    [SignalHandler]
    public virtual object block_nom(datum source = null, mob_living_carbon eater = null, mob_living_carbon feeder = null)
    {
        if (!HAS_TRAIT(eater, TRAIT_READY_TO_OPERATE))
            return NONE;
        if (eater == feeder)
            to_chat(feeder, span_warning(I18n.T("You feel it unwise to eat [] while you're undergoing surgery.", source)));
        else
            to_chat(feeder, span_warning(I18n.T("The only thing you could think of doing with [] right now is feeding it to [], but that doesn't seem right.", source, eater)));
        return BLOCK_EAT_ATTEMPT;
    }
}

// tg: code/modules/surgery/organs/_organ.dm:346
public partial class mob_living_carbon : mob_living
{
    // tg: code/modules/surgery/organs/_organ.dm:346
    public virtual async DmTask regenerate_organs(float? remove_hazardous = null)
    {
        remove_hazardous ??= FALSE;
        // Delegate to species if possible.
        if (Dm.truthy(dna?.species))
        {
            foreach (obj_item_organ organ in Dm.each_cast<obj_item_organ>(organs))
            {
                if (Dm.truthy(Dm.bitand(organ.organ_flags, ORGAN_EMP)))
                    Dm.mask(ref organ.organ_flags, Dm.bitnot(ORGAN_EMP));
                if (Dm.truthy(remove_hazardous) && Dm.truthy(Dm.bitand(organ.organ_flags, ORGAN_HAZARDOUS)))
                {
                    qdel(organ);
                    continue;
                }
                // Species regenerate organs doesn't ALWAYS handle healing the organs because it's dumb
                await organ.set_organ_damage(0);
            }

            await ((datum_species)dna.species).regenerate_organs(this, replace_current: FALSE);
            set_heartattack(FALSE);

            // Ears have aditional var "deaf", need to update it too
            obj_item_organ_ears ears__1 = get_organ_slot(ORGAN_SLOT_EARS) as obj_item_organ_ears;
            ears__1.adjust_temporary_deafness(-INFINITY);

            return;
        }

        // Default organ fixing handling (gaps X5: the aliens have no species)
        // May result in kinda cursed stuff for mobs which don't need these organs
        obj_item_organ_lungs lungs = get_organ_slot(ORGAN_SLOT_LUNGS) as obj_item_organ_lungs;
        if (lungs == null)
        {
            lungs = Dm.@new<obj_item_organ_lungs>();
            await lungs.Insert(this);
        }
        await lungs.set_organ_damage(0);
        lungs.received_pressure_mult = Dm.initial(lungs, (obj_item_organ_lungs p) => p.received_pressure_mult);

        obj_item_organ_heart heart = get_organ_slot(ORGAN_SLOT_HEART) as obj_item_organ_heart;
        if (heart != null)
            set_heartattack(FALSE);
        else
        {
            heart = Dm.@new<obj_item_organ_heart>();
            await heart.Insert(this);
        }
        await heart.set_organ_damage(0);

        obj_item_organ_tongue tongue = get_organ_slot(ORGAN_SLOT_TONGUE) as obj_item_organ_tongue;
        if (tongue == null)
        {
            tongue = Dm.@new<obj_item_organ_tongue>();
            await tongue.Insert(this);
        }
        await tongue.set_organ_damage(0);

        obj_item_organ_eyes eyes = get_organ_slot(ORGAN_SLOT_EYES) as obj_item_organ_eyes;
        if (eyes == null)
        {
            eyes = Dm.@new<obj_item_organ_eyes>();
            await eyes.Insert(this);
        }
        await eyes.set_organ_damage(0);

        obj_item_organ_ears ears = get_organ_slot(ORGAN_SLOT_EARS) as obj_item_organ_ears;
        if (ears == null)
        {
            ears = Dm.@new<obj_item_organ_ears>();
            await ears.Insert(this);
        }
        await ears.set_organ_damage(0);
        ears.adjust_temporary_deafness(-INFINITY);
    }
}

// tg: code/modules/surgery/organs/_organ.dm:538 (global procs)
public static partial class GlobalProcs
{
    // tg: code/modules/surgery/organs/_organ.dm:538
    public static object get_all_slots()
    {
        if (!Dm.truthy(get_all_slots__all_organ_slots.len))
        {
            foreach (Type an_organ in Dm.each_cast<Type>(subtypesof(typeof(obj_item_organ))))
            {
                if (!Dm.truthy(Dm.initial(an_organ, (obj_item_organ p) => p.slot)))
                    continue;
                Dm.combine(ref get_all_slots__all_organ_slots, Dm.initial(an_organ, (obj_item_organ p) => p.slot));
            }
        }

        return get_all_slots__all_organ_slots;
    }
}

public static partial class GlobalProcs
{
    // tg: code/modules/surgery/organs/_organ.dm:539  var/static/list/all_organ_slots (a proc-level static, rules 10.6)
    private static ref DmList get_all_slots__all_organ_slots => ref TgState.Current.GlobalProcs__get_all_slots__all_organ_slots;
}

public sealed partial class TgState
{
    // tg: code/modules/surgery/organs/_organ.dm:539  var/static/list/all_organ_slots = list()
    internal DmList GlobalProcs__get_all_slots__all_organ_slots = Dm.list();
}
