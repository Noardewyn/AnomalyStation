// Translated from tgstation (snapshot 1d99bd7f): code/modules/reagents/chemistry/reagents/other_reagents.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: the [DmPath] of types that an M2 slot signature or var block names, in the earliest .dme file that declares
// them (rules 2.3; written by the M2 contracts step, docs/ai/specs/m2-ownership.md: blood, iron, love, only the type
// declared), and water's exposure procs (M4b, the fire extinguisher: expose_turf, expose_obj, expose_mob with the file-local
// wet-stack factors), and welding fuel's expose_mob and expose_turf (the fire extender's chem); M8 slice A: water's and
// fuel's on_mob_life (drinking them); M8 slice B (D182): determination, the wounds' adrenaline (iron has no procs of its own; blood's
// on_new and its blood_reagent element are blood transfer, M8 slice C; translated there). Water's on_hydroponics_apply, fuel's on_spark_act and the rest of the file are
// later (rows in docs/ai/specs/live-partial.txt); syndicate N3 (D316): space lube's exposure (the clown operatives' super lube flower).
// The var blocks are generated (D111).
// Silicons SI2 (D326): the peacekeeper cyborg's Synthpax and dizzying and tiring solutions, and the drying agent.
// Gaps RG1 (D339): saltwater's exposure and the wounds' on_saltwater (with the drinks' and the food's wound hooks they make the
// flesh wounds live).

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1
[DmPath("/datum/reagent/blood")]
public partial class datum_reagent_blood : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:25 (M8 slice C, D192)
    public override async DmTask on_new(object data = null)
    {
        await base.on_new(data);
        // If we were artificially created without blood data, we still want to have the blood_reagent element for exposure effects
        if (LAZYACCESS(data as DmList, BLOOD_DATA_TYPE) == null)
        {
            RegisterSignal(this, COMSIG_REAGENT_GROWN_IN_PLANT, nameof(grown_in_plant));
            await AddElement(typeof(datum_element_blood_reagent), null, get_blood_type(typeof(datum_blood_type_universal)));
            return;
        }

        if (((DmList)data)[BLOOD_DATA_TYPE] is not datum_blood_type blood_type)
            return;

        object blood_color = await blood_type.get_color();
        if (Dm.eq(blood_color, BLOOD_COLOR_RED)) // If the blood is default red, just use the darker red color for the reagent.
            color = Dm.initial(this, (datum_reagent_blood p) => p.color);
    }

    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:41 (M8 slice C)
    public override object get_taste_description(mob_living taster = null)
    {
        if (taster == null)
            return base.get_taste_description(taster);
        if (!HAS_TRAIT(taster, TRAIT_DETECTIVES_TASTE))
            return base.get_taste_description(taster);
        object blood_type = (data as DmList)?[BLOOD_DATA_TYPE];
        if (!Dm.truthy(blood_type))
            return base.get_taste_description(taster);
        return Dm.list(Dm.kv(Dm.text("[] type blood", blood_type), 1));
    }

    /// All blood grown in plants defaults to O-
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:52 (M8 slice C)
    [SignalHandler]
    public virtual void grown_in_plant(datum source = null, obj_item_seeds our_seeds = null, obj_item our_plant = null)
    {
        DmList d = data as DmList;
        LAZYSET(ref d, BLOOD_DATA_TYPE, get_blood_type(typeof(datum_blood_type_human_o_minus)));
        data = d;
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1209
[DmPath("/datum/reagent/iron")]
public partial class datum_reagent_iron : datum_reagent
{
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:3125 (M8 slice B: a new wound's second wind, _wounds.dm:481)
public partial class datum_reagent_determination : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:3137
    public override async DmTask on_mob_end_metabolize(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.on_mob_end_metabolize(affected_mob, metabolization_ratio);
        if (Dm.truthy(significant))
        {
            float stam_crash = 0;
            foreach (datum_wound W in Dm.each_cast<datum_wound>(((mob_living_carbon)affected_mob).all_wounds))
                stam_crash += (Dm.num(W.severity) + 1) * 3; // spike of 3 stam damage per wound severity (moderate = 6, severe = 9, critical = 12) when the determination wears off if it was a combat rush
            await affected_mob.adjust_stamina_loss(stam_crash);
        }
        affected_mob.remove_status_effect(typeof(datum_status_effect_determined));
    }

    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:3147
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        if (!Dm.truthy(significant) && Dm.num(volume) >= WOUND_DETERMINATION_SEVERE)
        {
            significant = TRUE;
            await mob.apply_status_effect(typeof(datum_status_effect_determined)); // in addition to the slight healing, limping cooldowns are divided by 4 during the combat high
        }

        volume = min(Dm.num(volume), WOUND_DETERMINATION_MAX);

        foreach (datum_wound W in Dm.each_cast<datum_wound>(mob.all_wounds?.Copy()))
        {
            obj_item_bodypart wounded_part = W.limb;
            if (wounded_part != null)
                await wounded_part.heal_damage(0.167f * k, 0.25f * k);
            if (Dm.truthy(await mob.adjust_stamina_loss(-0.67f * k, updating_stamina: FALSE))) // the more wounds, the more stamina regen
                return UPDATE_MOB_HEALTH;
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:3390
[DmPath("/datum/reagent/love")]
public partial class datum_reagent_love : datum_reagent
{
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:114  (the vars are generated, D111; M4b: the extinguisher)
[DmPath("/datum/reagent/water")]
public partial class datum_reagent_water : datum_reagent
{
    /// How many wet stacks you get per units of water when it's applied by touch.
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:189  #define WATER_TO_WET_STACKS_FACTOR_TOUCH 0.5 (file-local, #undef at :226)
    private const float WATER_TO_WET_STACKS_FACTOR_TOUCH = 0.5f;
    /// How many wet stacks you get per unit of water when it's applied by vapor. Much less effective than by touch, of course.
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:191  #define WATER_TO_WET_STACKS_FACTOR_VAPOR 0.1 (file-local, #undef at :227)
    private const float WATER_TO_WET_STACKS_FACTOR_VAPOR = 0.1f;

    /*
     * Water reaction to turf
     */
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:138
    public override async DmTask<object> expose_turf(turf exposed_turf = null, object reac_volume = null)
    {
        object dot = await base.expose_turf(exposed_turf, reac_volume);

        if (exposed_turf is not turf_open open_turf)
            return dot;

        foreach (mob_living_basic_slime exposed_slime in Dm.each<mob_living_basic_slime>(open_turf))
            await exposed_slime.apply_water();

        float cool_temp = cooling_temperature;

        obj_effect_hotspot hotspot = Dm.locate<obj_effect_hotspot>(open_turf);
        if (hotspot != null && !isspaceturf(open_turf)) // the water evaporates in an endothermic reaction
        {
            if (open_turf.air != null)
            {
                datum_gas_mixture air = open_turf.air;
                air.temperature = min(max(min(air.temperature - (cool_temp * 1000), air.temperature / cool_temp), T0C), air.temperature); // the outer min temperature check is for weird phenomena like freon combustion
                open_turf.temperature = clamp(min(open_turf.temperature - (cool_temp * 1000), open_turf.temperature / cool_temp), T20C, open_turf.temperature); // turfs normally don't go below T20C so I'll just clamp it to that in case of weird phenomena.
                await air.react(this);
                qdel(hotspot);
            }
        }

        if (isgroundlessturf(open_turf) || isnoslipturf(open_turf))
            return dot;

        if (Dm.num(reac_volume) >= 5)
            await open_turf.MakeSlippery(TURF_WET_WATER, 10 * SECONDS, min(Dm.num(reac_volume) * 1.5f * SECONDS, 60 * SECONDS));
        return dot;
    }

    /*
     * Water reaction to an object
     */
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:168
    public override async DmTask<object> expose_obj(obj exposed_obj = null, object reac_volume = null, float? methods = null, float? show_message = null)
    {
        methods ??= TOUCH;
        show_message ??= TRUE;
        object dot = await base.expose_obj(exposed_obj, reac_volume, methods, show_message);
        // DEVIATION(DV27): the interaction guard (D132) on a type whose own extinguish or wash is not translated (a lighter,
        // a cigarette, a flare, an organ): it is not put out or washed (its parent's proc does not run in its place).
        if (!InertSeam.MissingOverride(exposed_obj, "extinguish"))
            await exposed_obj.extinguish();
        if (!InertSeam.MissingOverride(exposed_obj, "wash"))
            await exposed_obj.wash(CLEAN_TYPE_ACID);
        // Monkey cube
        if (istype(exposed_obj, typeof(obj_item_food_monkeycube)))
        {
            obj_item_food_monkeycube cube = exposed_obj as obj_item_food_monkeycube;
            await cube.Expand();
        }

        // Dehydrated carp
        else if (istype(exposed_obj, typeof(obj_item_toy_plush_carpplushie_dehy_carp)))
        {
            obj_item_toy_plush_carpplushie_dehy_carp dehy = exposed_obj as obj_item_toy_plush_carpplushie_dehy_carp;
            dehy.Swell(); // Makes a carp
        }

        else if (istype(exposed_obj, typeof(obj_item_stack_sheet_hairlesshide)))
        {
            // new /obj/item/stack/sheet/wethide(get_turf(HH), HH.amount); qdel(HH): tanning hides (leather crafting, M5)
            Dm.NotTranslated("code/modules/reagents/chemistry/reagents/other_reagents.dm:184", "expose_obj: a hairless hide made wet (leather crafting, M5)");
            return null;
        }
        return dot;
    }

    /**
     * Water reaction to a mob
     */
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:197
    public override async DmTask expose_mob(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra)
    {
        methods ??= TOUCH;
        await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);
        if (Dm.truthy(Dm.bitand(methods, TOUCH)))
        {
            exposed_mob.extinguish_mob(); // extinguish removes all fire stacks
            await exposed_mob.adjust_wet_stacks(Dm.num(reac_volume) * WATER_TO_WET_STACKS_FACTOR_TOUCH); // Water makes you wet, at a 50% water-to-wet-stacks ratio. Which, in turn, gives you some mild protection from being set on fire!
        }

        if (Dm.truthy(Dm.bitand(methods, VAPOR)))
        {
            await exposed_mob.adjust_wet_stacks(Dm.num(reac_volume) * WATER_TO_WET_STACKS_FACTOR_VAPOR); // Spraying someone with water with the hope to put them out is just simply too funny to me not to add it.

            if (!HAS_TRAIT(exposed_mob, TRAIT_WATER_HATER) || HAS_TRAIT(exposed_mob, TRAIT_WATER_ADAPTATION))
                return;

            await exposed_mob.incapacitate(1); // startles the felinid, canceling any do_after
            await exposed_mob.add_mood_event("watersprayed", typeof(datum_mood_event_watersprayed));
        }

        if (Dm.truthy(Dm.bitand(methods, Dm.bitor(TOUCH, VAPOR)))) // wakey wakey eggs and bakey
        {
            await exposed_mob.adjust_dizzy(-2 * SECONDS);
            await exposed_mob.adjust_confusion(-2 * SECONDS);
            await exposed_mob.adjust_drowsiness(-4 * SECONDS);
            await exposed_mob.adjust_jitter(-4 * SECONDS);
            await exposed_mob.AdjustSleeping(-15 * SECONDS);
            await exposed_mob.AdjustUnconscious(-8 * SECONDS);
            float drunkness_restored = HAS_TRAIT(exposed_mob, TRAIT_WATER_ADAPTATION) ? -0.5f : -0.25f;
            await exposed_mob.adjust_drunk_effect(drunkness_restored);
        }

        if (Dm.truthy(Dm.bitand(methods, INGEST)) && HAS_TRAIT(exposed_mob, TRAIT_WATER_ADAPTATION) && Dm.num(reac_volume) >= 4)
            await exposed_mob.adjust_wet_stacks(0.15f * Dm.num(reac_volume));
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:229 (M8 slice A: drinking water)
public partial class datum_reagent_water : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:229
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon affected_carbon = (mob_living_carbon)affected_mob;

        obj_item_organ_liver liver = affected_carbon.get_organ_slot(ORGAN_SLOT_LIVER) as obj_item_organ_liver;
        if (Dm.truthy(liver?.damage) && !Dm.truthy(IS_ROBOTIC_ORGAN(liver)) && !Dm.truthy(Dm.bitand(liver.organ_flags, ORGAN_FAILING)))
        {
            float healing_bonus = liver.healing_factor * liver.maxHealth;
            await liver.apply_organ_damage(-0.5f * healing_bonus * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        }

        bool water_adaptation = HAS_TRAIT(affected_carbon, TRAIT_WATER_ADAPTATION);
        float blood_restored = water_adaptation ? 0.3f : 0.1f;
        affected_carbon.adjust_blood_volume(0.5f * blood_restored * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick)); // water is good for you!
        float drunkness_restored = water_adaptation ? -0.5f : -0.25f;
        await affected_carbon.adjust_drunk_effect(0.5f * drunkness_restored * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick)); // and even sobers you up slowly!!
        if (water_adaptation)
        {
            float need_mob_update = FALSE;
            need_mob_update = Dm.num(await affected_carbon.adjust_tox_loss(-0.125f * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), updating_health: FALSE, required_biotype: affected_biotype));
            need_mob_update += Dm.num(await affected_carbon.adjust_fire_loss(-0.125f * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), updating_health: FALSE, required_bodytype: affected_bodytype));
            need_mob_update += Dm.num(await affected_carbon.adjust_brute_loss(-0.125f * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), updating_health: FALSE, required_bodytype: affected_bodytype));
            return Dm.truthy(need_mob_update) ? UPDATE_MOB_HEALTH : dot;
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1379  (the vars are generated, D111; M4b: the fire
// extender, /obj/item/extinguisher/anti, sprays it)
[DmPath("/datum/reagent/fuel")]
public partial class datum_reagent_fuel : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1398
    public override async DmTask expose_mob(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra) //Splashing people with welding fuel to make them easy to ignite!
    {
        methods ??= TOUCH;
        await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);
        if (!Dm.truthy(Dm.bitand(methods, Dm.bitor(VAPOR, TOUCH))))
            return;

        await exposed_mob.adjust_fire_stacks(Dm.num(reac_volume) / 10);
    }

    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1413
    public override async DmTask<object> expose_turf(turf exposed_turf = null, object reac_volume = null)
    {
        object dot = await base.expose_turf(exposed_turf, reac_volume);
        if (!istype(exposed_turf, typeof(turf)) || isspaceturf(exposed_turf))
            return dot;

        if (Dm.num(reac_volume) < 5)
            return dot;

        obj_effect_decal_cleanable_fuel_pool pool = exposed_turf.spawn_unique_cleanable(typeof(obj_effect_decal_cleanable_fuel_pool)) as obj_effect_decal_cleanable_fuel_pool;
        if (pool != null)
            pool.burn_amount = max(min(round(Dm.num(reac_volume) / 5), 10), 1);
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1405 (M8 slice A: drinking welding fuel)
public partial class datum_reagent_fuel : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1405
    public override async DmTask<object> on_mob_life(object victim = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(victim, seconds_per_tick, metabolization_ratio);
        mob_living_carbon victim_carbon = (mob_living_carbon)victim;
        obj_item_organ_liver liver = victim_carbon.get_organ_slot(ORGAN_SLOT_LIVER) as obj_item_organ_liver;
        if (liver != null && HAS_TRAIT(liver, TRAIT_HUMAN_AI_METABOLISM))
            return dot;
        if (Dm.truthy(await victim_carbon.adjust_tox_loss(0.5f * Dm.num(seconds_per_tick), updating_health: FALSE, required_biotype: affected_biotype)))
            return UPDATE_MOB_HEALTH;
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1686  (the vars are generated, D111; M8 slice C: tg's
// reagent_mob_expose test gasses a mob with it)
[DmPath("/datum/reagent/nitrous_oxide")]
public partial class datum_reagent_nitrous_oxide : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1697
    public override async DmTask<object> expose_turf(turf exposed_turf = null, object reac_volume = null)
    {
        object dot = await base.expose_turf(exposed_turf, reac_volume);
        if (exposed_turf is turf_open open_turf)
            open_turf.atmos_spawn_air(Dm.text("[]=[];[]", GAS_N2O, Dm.num(reac_volume) / 20, TURF_TEMPERATURE(holder != null ? holder.chem_temp : T20C)));
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1702
    public override async DmTask expose_mob(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra)
    {
        methods ??= TOUCH;
        show_message ??= TRUE;
        touch_protection ??= 0;
        await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);
        if (Dm.truthy(Dm.bitand(methods, Dm.bitor(VAPOR, INHALE))))
        {
            // apply 2 seconds of drowsiness per unit applied, with a min duration of 4 seconds
            float drowsiness_to_apply = max(round(Dm.num(reac_volume), 1) * 2 * SECONDS * (1 - touch_protection.Value), 4 * SECONDS);
            await exposed_mob.adjust_drowsiness(drowsiness_to_apply);
        }
        if (Dm.truthy(Dm.bitand(methods, INHALE)))
        {
            await exposed_mob.adjust_organ_loss(ORGAN_SLOT_BRAIN, 0.25f * Dm.num(reac_volume), required_organ_flag: affected_organ_flags);
            await exposed_mob.adjust_hallucinations(10 * SECONDS * Dm.num(reac_volume));
        }
    }

    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1712
    public override async DmTask on_mob_metabolize(mob_living affected_mob = null, params object[] __extra)
    {
        await base.on_mob_metabolize(affected_mob, __extra);
        if (!HAS_TRAIT(affected_mob, TRAIT_COAGULATING)) //IF the mob does not have a coagulant in them, we add the blood mess trait to make the bleed quicker
            ADD_TRAIT(affected_mob, TRAIT_BLOOD_FOUNTAIN, type);
    }

    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1717
    public override async DmTask on_mob_end_metabolize(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.on_mob_end_metabolize(affected_mob, metabolization_ratio);
        REMOVE_TRAIT(affected_mob, TRAIT_BLOOD_FOUNTAIN, type);
    }

    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1721
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        await mob.adjust_drowsiness(1.34f * SECONDS * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));

        if (!HAS_TRAIT(mob, TRAIT_BLOOD_FOUNTAIN) && !HAS_TRAIT(mob, TRAIT_COAGULATING)) //So long as they do not have a coagulant, if they did not have the bloody mess trait, they do now
            ADD_TRAIT(mob, TRAIT_BLOOD_FOUNTAIN, type);

        else if (HAS_TRAIT(mob, TRAIT_COAGULATING)) //if we find they now have a coagulant, we remove the trait
            REMOVE_TRAIT(mob, TRAIT_BLOOD_FOUNTAIN, type);

        if (SPT_PROB(10, seconds_per_tick))
        {
            mob.losebreath += 2;
            await mob.adjust_confusion_up_to(2 * SECONDS, 5 * SECONDS);
        }
        return dot;
    }
}

// Gaps P2 (D298): the reagents' effects in a hydroponics tray (on_hydroponics_apply) and the botany chems' own procs.

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:22
public partial class datum_reagent_blood : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:22
    public override async DmTask on_hydroponics_apply(obj_machinery_hydroponics mytray = null, mob user = null)
    {
        mytray.adjust_pestlevel(rand(2, 3));
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:250
public partial class datum_reagent_water : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:250
    public override async DmTask on_hydroponics_apply(obj_machinery_hydroponics mytray = null, mob user = null)
    {
        mytray.adjust_waterlevel(Dm.round(Dm.num(volume)));
        //You don't belong in this world, monster!
        await ((datum_reagents)mytray.reagents).remove_reagent(type, volume);
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:343
public partial class datum_reagent_water_holywater : datum_reagent_water
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:343
    public override async DmTask on_hydroponics_apply(obj_machinery_hydroponics mytray = null, mob user = null)
    {
        mytray.adjust_waterlevel(Dm.round(Dm.num(volume)));
        mytray.adjust_plant_health(Dm.round(Dm.num(volume) * 0.1f));
        mytray.myseed?.adjust_instability(Dm.round(Dm.num(volume) * 0.15f));
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1107
public partial class datum_reagent_chlorine : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1107
    public override async DmTask on_hydroponics_apply(obj_machinery_hydroponics mytray = null, mob user = null)
    {
        mytray.adjust_plant_health(-Dm.round(Dm.num(volume)));
        mytray.adjust_toxic(Dm.round(Dm.num(volume) * 1.5f));
        mytray.adjust_waterlevel(-Dm.round(Dm.num(volume) * 0.5f));
        mytray.adjust_weedlevel(-rand(1, 3));
        // White Phosphorous + water -> phosphoric acid. That's not a good thing really.
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1130
public partial class datum_reagent_fluorine : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1130
    public override async DmTask on_hydroponics_apply(obj_machinery_hydroponics mytray = null, mob user = null)
    {
        mytray.adjust_plant_health(-Dm.round(Dm.num(volume) * 2));
        mytray.adjust_toxic(Dm.round(Dm.num(volume) * 2.5f));
        mytray.adjust_waterlevel(-Dm.round(Dm.num(volume) * 0.5f));
        mytray.adjust_weedlevel(-rand(1, 4));
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1160
public partial class datum_reagent_phosphorus : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1160
    public override async DmTask on_hydroponics_apply(obj_machinery_hydroponics mytray = null, mob user = null)
    {
        mytray.adjust_plant_health(-Dm.round(Dm.num(volume) * 0.75f));
        mytray.adjust_waterlevel(-Dm.round(Dm.num(volume) * 0.5f));
        mytray.adjust_weedlevel(-rand(1, 2));
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1316
public partial class datum_reagent_uranium : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1316
    public override async DmTask on_hydroponics_apply(obj_machinery_hydroponics mytray = null, mob user = null)
    {
        await mytray.radioactive_exposure(modifier: Dm.num(volume) * 0.1f);
        mytray.myseed?.adjust_instability(Dm.round(Dm.num(volume) * 0.1f));
        mytray.adjust_toxic(Dm.round(0.5f * Dm.num(volume) * tox_damage));
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1645
public partial class datum_reagent_ammonia : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1645
    public override async DmTask on_hydroponics_apply(obj_machinery_hydroponics mytray = null, mob user = null)
    {
        // Ammonia is bad ass.
        mytray.adjust_plant_health(Dm.round(Dm.num(volume) * 0.12f));
        obj_item_seeds myseed = mytray.myseed;
        if (myseed != null && prob(10))
        {
            myseed.adjust_yield(1);
            myseed.adjust_instability(1);
        }
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1664
public partial class datum_reagent_diethylamine : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1664
    public override async DmTask on_hydroponics_apply(obj_machinery_hydroponics mytray = null, mob user = null)
    {
        mytray.adjust_plant_health(Dm.round(Dm.num(volume)));
        mytray.adjust_pestlevel(-rand(1, 2));
        obj_item_seeds myseed = mytray.myseed;
        if (myseed != null)
        {
            myseed.adjust_yield(Dm.round(Dm.num(volume)));
            myseed.adjust_instability(-Dm.round(Dm.num(volume)));
        }
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1884
public partial class datum_reagent_plantnutriment : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1893
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon mob = affected_mob as mob_living_carbon;

        if (SPT_PROB(tox_prob, seconds_per_tick))
        {
            float tox_modifier = Dm.truthy(Dm.bitand(mob.mob_biotypes, MOB_PLANT)) ? -1 : 1;
            if (Dm.truthy(await mob.adjust_tox_loss(tox_modifier * Dm.num(metabolization_ratio), updating_health: FALSE, required_biotype: affected_biotype)))
                return UPDATE_MOB_HEALTH;
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1909
public partial class datum_reagent_plantnutriment_eznutriment : datum_reagent_plantnutriment
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1909
    public override async DmTask on_hydroponics_apply(obj_machinery_hydroponics mytray = null, mob user = null)
    {
        obj_item_seeds myseed = mytray.myseed;
        if (myseed != null)
        {
            myseed.adjust_instability(0.2f);
            myseed.adjust_potency(Dm.round(Dm.num(volume) * 0.3f));
            myseed.adjust_yield(Dm.round(Dm.num(volume) * 0.1f));
        }
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1924
public partial class datum_reagent_plantnutriment_left4zednutriment : datum_reagent_plantnutriment
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1924
    public override async DmTask on_hydroponics_apply(obj_machinery_hydroponics mytray = null, mob user = null)
    {
        mytray.adjust_plant_health(Dm.round(Dm.num(volume) * 0.1f));
        mytray.myseed?.adjust_instability(Dm.round(Dm.num(volume) * 0.2f));
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1936
public partial class datum_reagent_plantnutriment_robustharvestnutriment : datum_reagent_plantnutriment
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1936
    public override async DmTask on_hydroponics_apply(obj_machinery_hydroponics mytray = null, mob user = null)
    {
        obj_item_seeds myseed = mytray.myseed;
        if (myseed != null)
        {
            myseed.adjust_instability(-0.25f);
            myseed.adjust_potency(Dm.round(Dm.num(volume) * 0.1f));
            myseed.adjust_yield(Dm.round(Dm.num(volume) * 0.2f));
        }
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1951
public partial class datum_reagent_plantnutriment_endurogrow : datum_reagent_plantnutriment
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1951
    public override async DmTask on_hydroponics_apply(obj_machinery_hydroponics mytray = null, mob user = null)
    {
        obj_item_seeds myseed = mytray.myseed;
        if (myseed != null)
        {
            myseed.adjust_potency(-Dm.round(Dm.num(volume) * 0.1f));
            myseed.adjust_yield(-Dm.round(Dm.num(volume) * 0.075f));
            myseed.adjust_endurance(Dm.round(Dm.num(volume) * 0.35f));
        }
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1966
public partial class datum_reagent_plantnutriment_liquidearthquake : datum_reagent_plantnutriment
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1966
    public override async DmTask on_hydroponics_apply(obj_machinery_hydroponics mytray = null, mob user = null)
    {
        obj_item_seeds myseed = mytray.myseed;
        if (myseed != null)
        {
            myseed.adjust_weed_rate(Dm.round(Dm.num(volume) * 0.1f));
            myseed.adjust_weed_chance(Dm.round(Dm.num(volume) * 0.3f));
            myseed.adjust_production(-Dm.round(Dm.num(volume) * 0.075f));
        }
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:2320
public partial class datum_reagent_ash : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:2320
    public override async DmTask on_hydroponics_apply(obj_machinery_hydroponics mytray = null, mob user = null)
    {
        mytray.adjust_plant_health(Dm.round(Dm.num(volume)));
        mytray.adjust_weedlevel(-1);
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:2556
public partial class datum_reagent_saltpetre : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:2556
    public override async DmTask on_hydroponics_apply(obj_machinery_hydroponics mytray = null, mob user = null)
    {
        mytray.adjust_plant_health(Dm.round(Dm.num(volume) * 0.18f));
        mytray.myseed?.adjust_production(-Dm.round(Dm.num(volume) / 10) - (prob(Dm.num(volume) % 10) ? 1 : 0));
        mytray.myseed?.adjust_potency(Dm.round(Dm.num(volume)));
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:3382
public partial class datum_reagent_brimdust : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:3382
    public override async DmTask on_hydroponics_apply(obj_machinery_hydroponics mytray = null, mob user = null)
    {
        mytray.adjust_weedlevel(-1);
        mytray.adjust_pestlevel(-1);
        mytray.adjust_plant_health(Dm.round(Dm.num(volume)));
        mytray.myseed?.adjust_potency(Dm.round(Dm.num(volume) * 0.5f));
    }
}

// The syndicate campaign G1c (D316): the mulligan serum (a new random look for a human).
// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:927
public partial class datum_reagent_mulligan : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:936
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        if (affected_mob is not mob_living_carbon_human affected_human)
            return dot;
        to_chat(affected_human, span_boldwarning(I18n.T("You grit your teeth in pain as your body rapidly mutates!")));
        affected_human.visible_message(Dm.text("<b>[]</b> suddenly transforms!", affected_human));
        await randomize_human_normie(affected_human);
        return dot;
    }
}

///Used for clownery
// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:547
public partial class datum_reagent_lube : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:556
    public override async DmTask<object> expose_turf(turf exposed_turf = null, object reac_volume = null)
    {
        object dot = await base.expose_turf(exposed_turf, reac_volume);
        if (isopenturf(exposed_turf) && Dm.num(reac_volume) >= 1)
            await ((turf_open)exposed_turf).MakeSlippery(lube_kind, 15 * SECONDS, min(Dm.num(reac_volume) * 2 * SECONDS, 12 * SECONDS));
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:561
    public override async DmTask<object> expose_obj(obj exposed_obj = null, object reac_volume = null, float? methods = null, float? show_message = null)
    {
        object dot = await base.expose_obj(exposed_obj, reac_volume, methods, show_message);
        if (isitem(exposed_obj) && Dm.num(reac_volume) >= 1)
        {
            await exposed_obj.AddComponent(
                typeof(datum_component_slippery_item),
                Dm.named("fall_chance", Dm.eq(lube_kind, TURF_WET_SUPERLUBE) ? 80 : 50),
                Dm.named("fall_catch_chance", Dm.eq(lube_kind, TURF_WET_SUPERLUBE) ? 10 : 30),
                Dm.named("examine_msg", span_info(I18n.T("It's coated in a slippery substance!"))),
                Dm.named("wash_flags", CLEAN_TYPE_HARD_DECAL),
                Dm.named("duration", min(Dm.num(reac_volume) * 3 * SECONDS, 30 * SECONDS)));
        }
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:573
    public override async DmTask<object> used_on_fish(obj_item_fish fish = null)
    {
        await DmTask.CompletedTask;
        ADD_TRAIT(fish, TRAIT_FISH_FED_LUBE, type); //required for the lubefish mutation
        _addtimer(TRAIT_CALLBACK_REMOVE(fish, TRAIT_FISH_FED_LUBE, type), fish.feeding_frequency, TIMER_UNIQUE | TIMER_OVERRIDE, file: "code/modules/reagents/chemistry/reagents/other_reagents.dm", line: 575);
        return TRUE;
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:2865 (silicons SI2: the peacekeeper cyborg's hypospray)
public partial class datum_reagent_pax_peaceborg : datum_reagent_pax
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:2874
    public override async DmTask on_mob_metabolize(mob_living affected_mob = null, params object[] __extra)
    {
        await base.on_mob_metabolize(affected_mob, __extra);
        if (!HAS_TRAIT(affected_mob, TRAIT_SYNTHPAX_IMMUNE))
            ADD_TRAIT(affected_mob, TRAIT_PACIFISM, METABOLIZATION_TRAIT(GetType()));
        RegisterSignal(affected_mob, COMSIG_MOB_AFTER_APPLY_DAMAGE, nameof(on_metabolizer_damaged));
    }

    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:2880
    public override async DmTask on_mob_end_metabolize(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.on_mob_end_metabolize(affected_mob, metabolization_ratio);
        UnregisterSignal(affected_mob, COMSIG_MOB_AFTER_APPLY_DAMAGE);
        REMOVE_TRAIT(affected_mob, TRAIT_PACIFISM, METABOLIZATION_TRAIT(GetType()));
    }

    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:2885
    [SignalHandler]
    public virtual void on_metabolizer_damaged(mob_living source = null, object amount = null)
    {
        Waiting.NoSleep(source.adjust_timed_status_effect(Dm.num(amount) * 1 * SECONDS, typeof(datum_status_effect_synthpax_immunity), max_duration: 5 * SECONDS));
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:2889
public partial class datum_reagent_peaceborg_confuse : datum_reagent_peaceborg
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:2897
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon mob = affected_mob as mob_living_carbon;
        await mob.adjust_confusion_up_to(1 * SECONDS * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), 5 * SECONDS);
        await mob.adjust_dizzy_up_to(2 * SECONDS * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), 12 * SECONDS);

        if (SPT_PROB(10, seconds_per_tick))
            to_chat(mob, I18n.T("You feel confused and disoriented."));
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:2905
public partial class datum_reagent_peaceborg_tire : datum_reagent_peaceborg
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:2913
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon mob = affected_mob as mob_living_carbon;
        float healthcomp = 100 - Dm.num(mob.health); //DOES NOT ACCOUNT FOR ADMINBUS THINGS THAT MAKE YOU HAVE MORE THAN 200/210 HEALTH, OR SOMETHING OTHER THAN A HUMAN PROCESSING THIS.
        dot = FALSE;
        if (Dm.num(mob.get_stamina_loss()) < (45 - healthcomp)) //At 50 health you would have 200 - 150 health meaning 50 compensation. 60 - 50 = 10, so would only do 10-19 stamina.)
        {
            if (Dm.truthy(await mob.adjust_stamina_loss(3.34f * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), updating_stamina: FALSE)))
                dot = UPDATE_MOB_HEALTH;
        }
        if (SPT_PROB(16, seconds_per_tick))
            to_chat(mob, I18n.T("You should sit down and take a rest..."));
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:2570 (silicons SI2: the janitor cyborg's drying spray)
public partial class datum_reagent_drying_agent : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:2579
    public override async DmTask<object> expose_turf(turf exposed_turf = null, object reac_volume = null)
    {
        object dot = await base.expose_turf(exposed_turf, reac_volume);
        if (exposed_turf is not turf_open open_turf)
            return dot;
        // We want one spray of this stuff (5u) to take out a wet floor. Feels better that way
        open_turf.MakeDry(ALL, TRUE, Dm.num(reac_volume) * 10 * SECONDS);
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:2586
    public override async DmTask<object> expose_obj(obj exposed_obj = null, object reac_volume = null, float? methods = null, float? show_message = null)
    {
        methods ??= TOUCH;
        show_message ??= TRUE;
        object dot = await base.expose_obj(exposed_obj, reac_volume, methods, show_message);
        if (exposed_obj.GetType() != typeof(obj_item_clothing_shoes_galoshes))
            return dot;
        turf t_loc = get_turf(exposed_obj);
        qdel(exposed_obj);
        Dm.@new<obj_item_clothing_shoes_galoshes_dry>(t_loc);
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:266
public partial class datum_reagent_water_salt : datum_reagent_water
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:286
    public override async DmTask expose_mob(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra)
    {
        await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);
        if (!iscarbon(exposed_mob))
            return;
        mob_living_carbon carbies = (mob_living_carbon)exposed_mob;
        if (!Dm.truthy(Dm.bitand(methods, Dm.bitor(Dm.bitor(PATCH, TOUCH), VAPOR))))
            return;
        foreach (datum_wound iter_wound in Dm.each<datum_wound>(carbies.all_wounds))
            await iter_wound.on_saltwater(reac_volume, carbies);
    }
}

// Mixed salt with water! All the help of salt with none of the irritation. Plus increased volume.
// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:296 (interleaved types, rules 1.9)
public partial class datum_wound : datum
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:296
    public virtual async DmTask on_saltwater(object reac_volume = null, mob_living_carbon carbies = null)
    {
        return;
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:299
public partial class datum_wound_pierce_bleed : datum_wound_pierce
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:299
    public override async DmTask on_saltwater(object reac_volume = null, mob_living_carbon carbies = null)
    {
        await adjust_blood_flow(-0.06f * Dm.num(reac_volume), Dm.num(initial_flow) * 0.6f);
        to_chat(carbies, span_notice(I18n.T("The salt water splashes over [], soaking up the blood.", LOWER_TEXT(this))));
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:303
public partial class datum_wound_slash_flesh : datum_wound_slash
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:303
    public override async DmTask on_saltwater(object reac_volume = null, mob_living_carbon carbies = null)
    {
        await adjust_blood_flow(-0.1f * Dm.num(reac_volume), Dm.num(initial_flow) * 0.5f);
        to_chat(carbies, span_notice(I18n.T("The salt water splashes over [], soaking up the blood.", LOWER_TEXT(this))));
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:307
public partial class datum_wound_burn_flesh : datum_wound_burn
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:307
    [DmParams("reac_volume")]
    public override async DmTask on_saltwater(object reac_volume = null, mob_living_carbon carbies = null)
    {
        // Similar but better stats from normal salt.
        sanitization += VALUE_PER(0.6f, 30) * Dm.num(reac_volume);
        infection = Dm.num(infection) - max(VALUE_PER(0.5f, 30) * Dm.num(reac_volume), 0);
        infection_rate += VALUE_PER(0.07f, 30) * Dm.num(reac_volume);
        to_chat(victim, span_notice(I18n.T("The salt water splashes over [], soaking up the... miscellaneous fluids. It feels somewhat better afterwards.", LOWER_TEXT(this))));
        return;
    }
}

// Gaps RG3 (D339): the rest of the file. The diseases' reagents (the vaccines, the transformation toxins, the spores, Agent-S,
// Tranquility) came with VI1's diseases (wired in gaps train 12), romerol (the zombies) and the concrete (its walls and floors are not ported) stay with
// their rows in not-translated.md section 226; the moods are MS1's (live since gaps train 11). Space lube and growth serum fed to a
// fish came with the fish.

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:80 (gaps VI1's diseases, wired in gaps train 12)
public partial class datum_reagent_vaccine : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:86
    public override async DmTask expose_mob(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra)
    {
        methods ??= TOUCH;
        await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);
        if (!islist(data) || !Dm.truthy(Dm.bitand(methods, Dm.bitor(INGEST, INJECT))))
            return;

        foreach (object thing in Dm.each(exposed_mob.diseases))
        {
            datum_disease infection = (datum_disease)thing;
            if (Dm.@in(infection.GetDiseaseID(), data))
                await infection.cure(add_resistance: TRUE);
        }
        LAZYOR(ref exposed_mob.disease_resistances, data);
    }

    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:98
    public override async DmTask on_merge(DmList mix_data = null, object amount = null)
    {
        await base.on_merge(mix_data, amount);
        if (Dm.truthy(mix_data))
            data = Dm.bitor(data, mix_data);
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:103
public partial class datum_reagent_vaccine_fungal_tb : datum_reagent_vaccine
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:106
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        object data = __extra.Length > 0 ? __extra[0] : null;
        DmList cached_data;
        if (!Dm.truthy(data))
            cached_data = Dm.list();
        else
            cached_data = (DmList)data;
        Dm.combine(ref cached_data, Dm.text("[]", typeof(datum_disease_tuberculosis)));
        this.data = cached_data;
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:255
public partial class datum_reagent_water_mineral : datum_reagent_water
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:260
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        await mob.adjust_tox_loss(-0.05f * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), updating_health: FALSE);
        return UPDATE_MOB_HEALTH;
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:315
public partial class datum_reagent_water_holywater : datum_reagent_water
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:333
    public override async DmTask on_new(object data = null)
    {
        // Tracks the total amount of deciseconds that the reagent has been metab'd for, for the purpose of deconversion
        if (data == null)
            data = Dm.list(Dm.kv("deciseconds_metabolized", 0));
        else if (((DmList)data)["deciseconds_metabolized"] == null)
            ((DmList)data)["deciseconds_metabolized"] = 0;

        await base.on_new(data);
    }

    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:348
    public override async DmTask on_mob_add(mob_living affected_mob = null, object amount = null)
    {
        await base.on_mob_add(affected_mob, amount);
        if (Dm.truthy(IS_CULTIST(affected_mob)))
            to_chat(affected_mob, span_userdanger(I18n.T("A vile holiness begins to spread its shining tendrils through your mind, purging the Geometer of Blood's influence!")));
    }

    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:353
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        DmList my_data = (DmList)data;
        // Microdosing holy water is less effective than just gulping it down
        Dm.append_at(my_data, "deciseconds_metabolized", Dm.num(seconds_per_tick) * 1 * SECONDS * Dm.num(metabolization_ratio));

        await mob.adjust_jitter_up_to(2 * SECONDS * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), 20 * SECONDS);
        float need_mob_update = FALSE;

        if (spell_clear_cd <= world.time) // COOLDOWN_FINISHED(src, spell_clear_cd), inline (rules 11.1 (a))
        {
            if (Dm.truthy(IS_CULTIST(mob)))
            {
                foreach (datum_action_innate_cult_blood_magic BM in Dm.each<datum_action_innate_cult_blood_magic>(mob.actions))
                {
                    float removed_any = FALSE;
                    foreach (datum_action_innate_cult_blood_spell BS in Dm.each<datum_action_innate_cult_blood_spell>(BM.spells))
                    {
                        removed_any = TRUE;
                        qdel(BS);
                    }
                    if (Dm.truthy(removed_any))
                    {
                        to_chat(mob, span_cult_large(I18n.T("Your blood rites falter as holy water scours your body!")));
                        spell_clear_cd = world.time + 3 * SECONDS; // COOLDOWN_START, inline
                    }
                }
            }

            // IS_HERETIC(affected_mob) (__DEFINES/antagonists.dm:293), expanded inline (rules 11.1)
            if (Dm.truthy(mob.mind?.has_antag_datum(typeof(datum_antagonist_heretic))) || HAS_TRAIT(mob, TRAIT_ACT_AS_HERETIC))
            {
                // tg :376-383: the heretic's researched spells lose charges (heretic_datum.get_researched_knowledge(),
                // spell.remove_charges). GAP(D333): the heretic is not ported (no mind holds its antag datum), so this never runs.
                Dm.Deferred("code/modules/reagents/chemistry/reagents/other_reagents.dm:376", "holy water on_mob_life: the heretic's spells drained (the heretic, D333)");
            }
        }

        if (Dm.num(my_data["deciseconds_metabolized"]) >= (25 * SECONDS)) // 10 units
        {
            await mob.adjust_stutter_up_to(2 * SECONDS * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), 20 * SECONDS);
            await mob.set_dizzy_if_lower(10 * SECONDS);
            if (Dm.truthy(IS_CULTIST(mob)) && SPT_PROB(10, seconds_per_tick))
            {
                await mob.say(pick("Av'te Nar'Sie", "Pa'lid Mors", "INO INO ORA ANA", "SAT ANA!", "Daim'niodeis Arc'iai Le'eones", "R'ge Na'sie", "Diabo us Vo'iscum", "Eld' Mon Nobis"), forced: "holy water");
                if (prob(10))
                {
                    mob.visible_message(span_danger(I18n.T("[] starts having a seizure!", mob)), span_userdanger(I18n.T("You have a seizure!")));
                    await mob.Unconscious(12 * SECONDS);
                    to_chat(mob, span_cult_large(Dm.text("[].", pick(I18n.T("Your blood is your bond - you are nothing without it"), I18n.T("Do not forget your place"),
                        I18n.T("All that power, and you still fail?"), I18n.T("If you cannot scour this poison, I shall scour your meager life!")))));
                }
            }
            else if (HAS_TRAIT(mob, TRAIT_EVIL) && SPT_PROB(25, seconds_per_tick)) //Congratulations, your committment to evil has now made holy water a deadly poison to you!
            {
                if (!Dm.truthy(IS_CULTIST(mob)) || (mob.mind as datum_mind)?.holy_role != HOLY_ROLE_PRIEST)
                {
                    await mob.emote("scream");
                    need_mob_update += Dm.num(await mob.adjust_fire_loss(1.5f * Dm.num(metabolization_ratio), updating_health: FALSE));
                }
            }
        }

        if (Dm.num(my_data["deciseconds_metabolized"]) >= (1 * MINUTES)) // 24 units
        {
            if (Dm.truthy(IS_CULTIST(mob)))
            {
                await ((datum_mind)mob.mind).remove_antag_datum(typeof(datum_antagonist_cult));
                await mob.Unconscious(10 * SECONDS);
            }
            else if (HAS_TRAIT(mob, TRAIT_EVIL)) //At this much holy water, you're probably going to fucking melt. good luck
            {
                if (!Dm.truthy(IS_CULTIST(mob)) || (mob.mind as datum_mind)?.holy_role != HOLY_ROLE_PRIEST)
                    need_mob_update += Dm.num(await mob.adjust_fire_loss(5 * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), updating_health: FALSE));
            }
            mob.remove_status_effect(typeof(datum_status_effect_jitter));
            mob.remove_status_effect(typeof(datum_status_effect_speech_stutter));
            foreach (datum_status_effect_eldritch_painting eldritch_curses in Dm.each<datum_status_effect_eldritch_painting>(mob.status_effects))
                qdel(eldritch_curses);
            if (holder != null)
                await holder.remove_reagent(type, volume); // maybe this is a little too perfect and a max() cap on the statuses would be better??
        }
        if (Dm.truthy(need_mob_update))
            return UPDATE_MOB_HEALTH;
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:412
    public override async DmTask<object> expose_turf(turf exposed_turf = null, object reac_volume = null)
    {
        object dot = await base.expose_turf(exposed_turf, reac_volume);
        if (!istype(exposed_turf, typeof(turf)))
            return dot;
        if (Dm.num(reac_volume) >= 10)
        {
            foreach (obj_effect_rune R in Dm.each<obj_effect_rune>(exposed_turf))
                qdel(R);
        }
        await exposed_turf.bless_turf();
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:429
public partial class datum_reagent_hydrogen_peroxide : datum_reagent
{
    /*
     * Water reaction to turf
     */
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:452
    public override async DmTask<object> expose_turf(turf exposed_turf = null, object reac_volume = null)
    {
        object dot = await base.expose_turf(exposed_turf, reac_volume);
        if (Dm.num(reac_volume) < 1.5f)
            return dot;
        if (!isplatingturf(exposed_turf) && exposed_turf.type != typeof(turf_closed_wall))
            return dot;
        if (!HAS_TRAIT(exposed_turf, TRAIT_RUSTY))
            await exposed_turf.AddElement(typeof(datum_element_rust));
        return dot;
    }

    /*
     * Water reaction to a mob
     */
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:464
    public override async DmTask expose_mob(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra) //Exposing people to h2o2 can burn them !
    {
        methods ??= TOUCH;
        show_message ??= TRUE;
        touch_protection ??= 0;
        await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);
        if (!Dm.truthy(Dm.bitand(methods, Dm.bitor(VAPOR, TOUCH))))
            return;

        float damage_to_inflict = 2 * max(1 - touch_protection.Value, 0);

        if (Dm.truthy(damage_to_inflict))
            await exposed_mob.adjust_fire_loss(damage_to_inflict);
    }
}

//if you somehow managed to extract this from someone, dont splash it on yourself and have a smoke
// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:474
public partial class datum_reagent_fuel_unholywater : datum_reagent_fuel
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:485
    public override async DmTask on_mob_metabolize(mob_living affected_mob = null, params object[] __extra)
    {
        await base.on_mob_metabolize(affected_mob, __extra);
        if (Dm.truthy(IS_CULTIST(affected_mob)))
            ADD_TRAIT(affected_mob, TRAIT_COAGULATING, type);
    }

    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:490
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);

        float need_mob_update = FALSE;
        if (Dm.truthy(IS_CULTIST(mob)))
        {
            await mob.adjust_drowsiness(-2 * SECONDS * k);
            await mob.AdjustAllImmobility(-8 * k);
            need_mob_update += Dm.num(await mob.adjust_stamina_loss(-2 * k, updating_stamina: FALSE));
            need_mob_update += Dm.num(await mob.adjust_tox_loss(-0.4f * k, updating_health: FALSE));
            need_mob_update += Dm.num(await mob.adjust_oxy_loss(-0.4f * k, updating_health: FALSE));
            need_mob_update += Dm.num(await mob.adjust_brute_loss(-0.4f * k, updating_health: FALSE));
            need_mob_update += Dm.num(await mob.adjust_fire_loss(-0.4f * k, updating_health: FALSE));
            mob.adjust_blood_volume(0.6f * k, maximum: BLOOD_VOLUME_NORMAL);
            await mob.coagulant_effect(0.4f * k);
        }
        else // Will deal about 90 damage when 50 units are thrown
        {
            need_mob_update += Dm.num(await mob.adjust_organ_loss(ORGAN_SLOT_BRAIN, 0.6f * k, 150));
            need_mob_update += Dm.num(await mob.adjust_tox_loss(0.2f * k, updating_health: FALSE));
            need_mob_update += Dm.num(await mob.adjust_fire_loss(0.2f * k, updating_health: FALSE));
            need_mob_update += Dm.num(await mob.adjust_oxy_loss(0.2f * k, updating_health: FALSE));
            need_mob_update += Dm.num(await mob.adjust_brute_loss(0.2f * k, updating_health: FALSE));
        }
        if (Dm.truthy(need_mob_update))
            return UPDATE_MOB_HEALTH;
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:514
    public override async DmTask on_mob_end_metabolize(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.on_mob_end_metabolize(affected_mob, metabolization_ratio);
        REMOVE_TRAIT(affected_mob, TRAIT_COAGULATING, type); //We don't cult check here because potentially our imbiber may no longer be a cultist for whatever reason! It doesn't purge holy water, after all!
    }
}

//if someone has this in their system they've really pissed off an eldrich god
// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:518
public partial class datum_reagent_hellwater : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:526
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        await mob.set_fire_stacks(min(Dm.num(mob.fire_stacks) + (1.5f * Dm.num(seconds_per_tick)), 5));
        mob.ignite_mob(); //Only problem with igniting people is currently the commonly available fire suits make you immune to being on fire
        float need_mob_update;
        need_mob_update = Dm.num(await mob.adjust_tox_loss(0.5f * Dm.num(seconds_per_tick), updating_health: FALSE));
        need_mob_update += Dm.num(await mob.adjust_fire_loss(0.5f * Dm.num(seconds_per_tick), updating_health: FALSE)); //Hence the other damages... ain't I a bastard?
        await mob.adjust_organ_loss(ORGAN_SLOT_BRAIN, 2.5f * Dm.num(seconds_per_tick), 150);
        if (holder != null)
            await holder.remove_reagent(type, 0.5f * Dm.num(seconds_per_tick));
        if (Dm.truthy(need_mob_update))
            return UPDATE_MOB_HEALTH;
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:586
public partial class datum_reagent_spraytan : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:600
    public override async DmTask expose_mob(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra)
    {
        methods ??= TOUCH;
        show_message ??= TRUE;
        touch_protection ??= 0;
        await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);
        if (ishuman(exposed_mob))
        {
            if (Dm.truthy(Dm.bitand(methods, Dm.bitor(PATCH, VAPOR))) && touch_protection < 1)
            {
                var exposed_human = (mob_living_carbon_human)exposed_mob;
                if (HAS_TRAIT(exposed_human, TRAIT_USES_SKINTONES))
                {
                    string sw__1 = exposed_human.skin_tone;
                    if (sw__1 == "african1")
                        exposed_human.skin_tone = "african2";
                    else if (sw__1 == "indian")
                        exposed_human.skin_tone = "mixed2";
                    else if (sw__1 == "arab")
                        exposed_human.skin_tone = "indian";
                    else if (sw__1 == "asian2")
                        exposed_human.skin_tone = "arab";
                    else if (sw__1 == "asian1")
                        exposed_human.skin_tone = "asian2";
                    else if (sw__1 == "mediterranean")
                        exposed_human.skin_tone = "mixed1";
                    else if (sw__1 == "latino")
                        exposed_human.skin_tone = "mediterranean";
                    else if (sw__1 == "caucasian3")
                        exposed_human.skin_tone = "mediterranean";
                    else if (sw__1 == "caucasian2")
                        exposed_human.skin_tone = Dm.str(pick("caucasian3", "latino"));
                    else if (sw__1 == "caucasian1")
                        exposed_human.skin_tone = "caucasian2";
                    else if (sw__1 == "albino")
                        exposed_human.skin_tone = "caucasian1";
                    else if (sw__1 == "mixed1")
                        exposed_human.skin_tone = "mixed2";
                    else if (sw__1 == "mixed2")
                        exposed_human.skin_tone = "mixed3";
                    else if (sw__1 == "mixed3")
                        exposed_human.skin_tone = "african1";
                    else if (sw__1 == "mixed4")
                        exposed_human.skin_tone = "mixed3";
                }
                //take current alien color and darken it slightly
                else if (HAS_TRAIT(exposed_human, TRAIT_MUTANT_COLORS) && !HAS_TRAIT(exposed_human, TRAIT_FIXED_MUTANT_COLORS))
                {
                    DmList existing_color = rgb2num(Dm.str(exposed_human.dna.features[FEATURE_MUTANT_COLOR]));
                    DmList darkened_color = Dm.list();
                    // Reduces each part of the color by 16
                    foreach (object channel in Dm.each(existing_color))
                        Dm.append(ref darkened_color, max(Dm.num(channel) - 17, 0));

                    string new_color = rgb(Dm.num(darkened_color[1]), Dm.num(darkened_color[2]), Dm.num(darkened_color[3]));
                    DmList new_hsv = rgb2hsv(new_color);
                    // Can't get too dark now
                    if (Dm.num(new_hsv[3]) >= 50)
                        exposed_human.dna.features[FEATURE_MUTANT_COLOR] = new_color;
                }
                await exposed_human.update_body(TRUE); // is_creating = TRUE
            }

            if (Dm.truthy(Dm.bitand(methods, INGEST)) && Dm.truthy(show_message))
                to_chat(exposed_mob, span_notice(I18n.T("That tasted horrible.")));
        }
    }

    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:655
    public override async DmTask<object> overdose_process(mob_living affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.overdose_process(affected_mob, seconds_per_tick, metabolization_ratio);
        metabolization_rate = REAGENTS_METABOLISM;

        if (ishuman(affected_mob))
        {
            var affected_human = (mob_living_carbon_human)affected_mob;
            var head = affected_human.get_bodypart(BODY_ZONE_HEAD) as obj_item_bodypart_head;
            if (head != null)
                head.head_flags = Dm.num(Dm.bitor(head.head_flags, HEAD_HAIR)); //No hair? No problem!
            if (!HAS_TRAIT(affected_human, TRAIT_SHAVED))
                affected_human.set_facial_hairstyle("Shaved", update: FALSE);
            affected_human.set_facial_haircolor(COLOR_BLACK, update: FALSE);
            if (!HAS_TRAIT(affected_human, TRAIT_BALD))
                affected_human.set_hairstyle("Spiky", update: FALSE);
            affected_human.set_haircolor(COLOR_BLACK, update: FALSE);
            if (HAS_TRAIT(affected_human, TRAIT_USES_SKINTONES))
                affected_human.skin_tone = "orange";
            else if (HAS_TRAIT(affected_human, TRAIT_MUTANT_COLORS) && !HAS_TRAIT(affected_human, TRAIT_FIXED_MUTANT_COLORS)) //Aliens with custom colors simply get turned orange
                affected_human.dna.features[FEATURE_MUTANT_COLOR] = "#ff8800";
            await affected_human.update_body(TRUE); // is_creating = TRUE
            if (SPT_PROB(3.5f, seconds_per_tick))
            {
                if (affected_human.w_uniform != null)
                    affected_mob.visible_message(pick(I18n.T("<b>[]</b>'s collar pops up without warning.</span>", affected_mob), I18n.T("<b>[]</b> flexes [] arms.", affected_mob, affected_mob.p_their())));
                else
                    affected_mob.visible_message(I18n.T("<b>[]</b> flexes [] arms.", affected_mob, affected_mob.p_their()));
            }
        }
        if (SPT_PROB(5, seconds_per_tick))
            await affected_mob.say(pick("Shit was SO cash.", "You are everything bad in the world.", "What sports do you play, other than 'jack off to naked drawn Japanese people?'", "Don???t be a stranger. Just hit me with your best shot.", "My name is John and I hate every single one of you."), forced: typeof(datum_reagent_spraytan));
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:694
public partial class datum_reagent_mutationtoxin : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:681  #define MUT_MSG_IMMEDIATE 1 (file-local, #undef at :913)
    private const int MUT_MSG_IMMEDIATE = 1;
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:682  #define MUT_MSG_EXTENDED 2 (file-local, #undef at :914)
    private const int MUT_MSG_EXTENDED = 2;
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:683  #define MUT_MSG_ABOUT2TURN 3 (file-local, #undef at :915)
    private const int MUT_MSG_ABOUT2TURN = 3;
    /// the current_cycle threshold / iterations needed before one can transform
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:686  #define CYCLES_TO_TURN 20 (file-local, #undef at :917)
    protected const int CYCLES_TO_TURN = 20;
    /// the cycle at which 'immediate' mutation text begins displaying
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:688  #define CYCLES_MSG_IMMEDIATE 6 (file-local, #undef at :918)
    private const int CYCLES_MSG_IMMEDIATE = 6;
    /// the cycle at which 'extended' mutation text begins displaying
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:690  #define CYCLES_MSG_EXTENDED 16 (file-local, #undef at :919)
    private const int CYCLES_MSG_EXTENDED = 16;

    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:708
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        if (!ishuman(mob))
            return dot;
        mob_living_carbon affected_human = mob;
        if (!Dm.truthy(affected_human.dna?.species) || !Dm.truthy(Dm.bitand(affected_human.mob_biotypes, affected_biotype)))
            return dot;

        if (SPT_PROB(5, seconds_per_tick))
        {
            DmList pick_ur_fav = Dm.list();
            float filter = NONE;
            if (current_cycle <= CYCLES_MSG_IMMEDIATE)
                filter = MUT_MSG_IMMEDIATE;
            else if (current_cycle <= CYCLES_MSG_EXTENDED)
                filter = MUT_MSG_EXTENDED;
            else
                filter = MUT_MSG_ABOUT2TURN;

            foreach (object i in Dm.each(mutationtexts))
            {
                if (Dm.eq(mutationtexts[i], filter))
                    Dm.append(ref pick_ur_fav, i);
            }
            to_chat(affected_human, span_warning(I18n.T(Dm.str(pick(pick_ur_fav)))));
        }

        if (current_cycle >= CYCLES_TO_TURN)
        {
            Type species_type = race;
            // GAP(D338): the species not ported yet (moth, ethereal, pod, jelly, golem, abductor, skeleton, zombie, spirit, plasmaman, the
            // cerulean) are inert; tg turns the drinker into one, the port leaves the drinker as they are until the species is ported
            // (not-translated.md section 226). The human, felinid, lizard, ashwalker, fly, android and shadow toxins transform.
            if (InertSeam.IsInertType(species_type))
                return dot;
            affected_human.set_species(species_type);
            await holder.del_reagent(type);
            to_chat(affected_human, span_warning(I18n.T("You've become \\a []!", LOWER_TEXT(Dm.initial(species_type, (datum_species s) => s.name)))));
            return dot;
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:808
public partial class datum_reagent_mutationtoxin_jelly : datum_reagent_mutationtoxin
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:817
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        var mob = (mob_living_carbon)affected_mob;
        if (!ishuman(mob))
            return await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon affected_human = mob;
        if (Dm.truthy(Dm.bitand(affected_human.mob_biotypes, MOB_SLIME)))
        {
            Type species_type = (Type)pick(subtypesof(race));
            // GAP(D338, XB1-2): the jellypeople are not ported (their species are inert); see the parent's GAP.
            if (InertSeam.IsInertType(species_type))
                return null;
            affected_human.set_species(species_type);
            await holder.del_reagent(type);
            to_chat(affected_human, span_warning(I18n.T("Your jelly shifts and morphs, turning you into another subspecies!")));
            return UPDATE_MOB_HEALTH;
        }
        if (current_cycle < CYCLES_TO_TURN) //overwrite since we want subtypes of jelly
            return await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        Type species_type__1 = (Type)pick(subtypesof(race));
        // GAP(D338, XB1-2): the jellypeople are not ported (their species are inert); see the parent's GAP.
        if (InertSeam.IsInertType(species_type__1))
            return null;
        affected_human.set_species(species_type__1);
        await holder.del_reagent(type);
        to_chat(affected_human, span_warning(I18n.T("You've become \\a []!", Dm.initial(species_type__1, (datum_species s) => s.name))));
        return UPDATE_MOB_HEALTH;
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:974
public partial class datum_reagent_serotrotium : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:984
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        if (SPT_PROB(3.5f, seconds_per_tick))
            await mob.emote(pick("twitch", "drool", "moan", "gasp"));
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:989
public partial class datum_reagent_oxygen : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:999
    public override async DmTask<object> expose_turf(turf exposed_turf = null, object reac_volume = null)
    {
        object dot = await base.expose_turf(exposed_turf, reac_volume);
        if (istype(exposed_turf, typeof(turf_open)))
            exposed_turf.atmos_spawn_air(Dm.text("[]=[];[]", GAS_O2, Dm.num(reac_volume) / 20, TURF_TEMPERATURE(holder != null ? holder.chem_temp : T20C)));
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1005
public partial class datum_reagent_copper : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1014
    public override async DmTask<object> expose_obj(obj exposed_obj = null, object reac_volume = null, float? methods = null, float? show_message = null)
    {
        methods ??= TOUCH;
        show_message ??= TRUE;
        object dot = await base.expose_obj(exposed_obj, reac_volume, methods, show_message);
        if (!istype(exposed_obj, typeof(obj_item_stack_sheet_iron)))
            return dot;

        var metal = (obj_item_stack_sheet_iron)exposed_obj;
        reac_volume = min(Dm.num(reac_volume), Dm.num(metal.amount));
        Dm.@new<obj_item_stack_sheet_bronze>(get_turf(metal), reac_volume);
        await metal.use(reac_volume);
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1024
public partial class datum_reagent_nitrogen : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1032
    public override async DmTask<object> expose_turf(turf exposed_turf = null, object reac_volume = null)
    {
        if (istype(exposed_turf, typeof(turf_open)))
            exposed_turf.atmos_spawn_air(Dm.text("[]=[];[]", GAS_N2, Dm.num(reac_volume) / 20, TURF_TEMPERATURE(holder != null ? holder.chem_temp : T20C)));
        return await base.expose_turf(exposed_turf, reac_volume);
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1054
public partial class datum_reagent_mercury : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1062
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        // tg bug kept (D86): HAS_TRAIT(src, ...) reads the reagent's traits, not the mob's, so an immobilized mob still steps
        if (!HAS_TRAIT(this, TRAIT_IMMOBILIZED) && isturf(mob.loc) && !isgroundlessturf(mob.loc))
            await step(mob, Dm.num(pick(GLOB.cardinals)));
        if (SPT_PROB(3.5f, seconds_per_tick))
            await mob.emote(pick("twitch", "drool", "moan"));
        if (Dm.truthy(await mob.adjust_organ_loss(ORGAN_SLOT_BRAIN, 0.5f * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick))))
            return UPDATE_MOB_HEALTH;
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1080
public partial class datum_reagent_carbon : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1089
    public override async DmTask<object> expose_turf(turf exposed_turf = null, object reac_volume = null)
    {
        object dot = await base.expose_turf(exposed_turf, reac_volume);
        if (isspaceturf(exposed_turf))
            return dot;

        exposed_turf.spawn_unique_cleanable(typeof(obj_effect_decal_cleanable_dirt));
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1096
public partial class datum_reagent_chlorine : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1115
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        if (Dm.truthy(await mob.take_bodypart_damage(0.25f * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), 0)))
            return UPDATE_MOB_HEALTH;
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1120
public partial class datum_reagent_fluorine : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1136
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        if (Dm.truthy(await mob.adjust_tox_loss(0.25f * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), updating_health: FALSE)))
            return UPDATE_MOB_HEALTH;
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1165
public partial class datum_reagent_lithium : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1174
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        if (!HAS_TRAIT(mob, TRAIT_IMMOBILIZED) && isturf(mob.loc) && !isgroundlessturf(mob.loc))
            await step(mob, Dm.num(pick(GLOB.cardinals)));
        if (SPT_PROB(2.5f, seconds_per_tick))
            await mob.emote(pick("twitch", "drool", "moan"));
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1190
public partial class datum_reagent_space_cleaner_sterilizine : datum_reagent_space_cleaner
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1199
    public override async DmTask expose_mob(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra)
    {
        methods ??= TOUCH;
        await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);
        if (!Dm.truthy(Dm.bitand(methods, Dm.bitor(Dm.bitor(TOUCH, VAPOR), PATCH))))
            return;

        exposed_mob.add_surgery_speed_mod(type, 0.8f, min(Dm.num(reac_volume) * 1 * MINUTES, 5 * MINUTES));
    }

    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1206
    public override async DmTask on_burn_wound_processing(datum_wound_burn_flesh burn_wound = null)
    {
        burn_wound.sanitization += 0.9f;
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1237
public partial class datum_reagent_uranium : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1252
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        // rules 10.6: tg's Master always creates SSradiation; a harness world without it irradiates nothing.
        if (!HAS_TRAIT(mob, TRAIT_IRRADIATED) && Dm.truthy(SSradiation?.can_irradiate_basic(mob)))
        {
            float chance = min(Dm.num(volume) / (20 - rad_power * 5), rad_power);
            if (SPT_PROB(chance, seconds_per_tick)) // ignore rad protection calculations bc it's inside of us
                await mob.AddComponent(typeof(datum_component_irradiated));
        }

        if (Dm.truthy(await mob.adjust_tox_loss(tox_damage * Dm.num(seconds_per_tick) * metabolization_rate, updating_health: FALSE)))
            return UPDATE_MOB_HEALTH;
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1262
    public override async DmTask<object> expose_obj(obj exposed_obj = null, object reac_volume = null, float? methods = null, float? show_message = null)
    {
        methods ??= TOUCH;
        show_message ??= TRUE;
        object dot = await base.expose_obj(exposed_obj, reac_volume, methods, show_message);

        if (!Dm.truthy(SSradiation?.can_irradiate_basic(exposed_obj))) // rules 10.6: a harness world may lack SSradiation
            return dot;

        radiation_pulse(
            source: exposed_obj,
            max_range: 0,
            threshold: RAD_VERY_LIGHT_INSULATION,
            chance: (min(Dm.num(reac_volume) * rad_power, CALCULATE_RAD_MAX_CHANCE(rad_power)))
        );
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1275
    public override async DmTask expose_mob(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra)
    {
        show_message ??= TRUE;
        touch_protection ??= 0;
        await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);

        if (!Dm.truthy(SSradiation?.can_irradiate_basic(exposed_mob))) // rules 10.6: a harness world may lack SSradiation
            return;

        if (ishuman(exposed_mob) && Dm.truthy(SSradiation.wearing_rad_protected_clothing((mob_living_carbon_human)exposed_mob)))
            return;

        if (!Dm.truthy(Dm.bitand(methods, Dm.bitor(TOUCH, VAPOR))))
            return;

        // tg bug kept (D86): min(..., 0, 100) is never above 0, so the exposure is never refused
        float exposure_probability = min(100 - (touch_protection.Value * 100), 0, 100);
        if (Dm.truthy(exposure_probability) && !prob(exposure_probability))
            return;

        radiation_pulse(
            source: exposed_mob,
            max_range: 0,
            threshold: RAD_VERY_LIGHT_INSULATION,
            chance: (min(Dm.num(reac_volume) * rad_power, CALCULATE_RAD_MAX_CHANCE(rad_power)))
        );
    }

    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1299
    public override async DmTask<object> expose_turf(turf exposed_turf = null, object reac_volume = null)
    {
        object dot = await base.expose_turf(exposed_turf, reac_volume);
        if ((Dm.num(reac_volume) < 3) || isspaceturf(exposed_turf))
            return dot;

        var glow = exposed_turf.spawn_unique_cleanable(typeof(obj_effect_decal_cleanable_greenglow)) as obj_effect_decal_cleanable_greenglow;
        if (QDELETED(glow))
            return dot;

        glow.decal_reagent = type;
        float rounded_volume = round(Dm.num(reac_volume), 1);
        glow.reagent_amount = rounded_volume;
        if (Dm.truthy(await glow.lazy_init_reagents())) // Decal already has reagents inited, add instead
        {
            var glow_reagents = (datum_reagents)glow.reagents;
            glow_reagents.maximum_volume = min(glow_reagents.maximum_volume + rounded_volume, 300); // Increase reagent holder volume up to a max of 300
            await glow_reagents.add_reagent(type, rounded_volume);
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1333
public partial class datum_reagent_bluespace : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1343
    public override async DmTask expose_mob(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra)
    {
        methods ??= TOUCH;
        show_message ??= TRUE;
        touch_protection ??= 0;
        await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);
        if (!Dm.truthy(Dm.bitand(methods, Dm.bitor(VAPOR, TOUCH))))
            return;

        await do_teleport(exposed_mob, get_turf(exposed_mob), (Dm.num(reac_volume) / 5), asoundin: Dm.resource("sound/effects/phasein.ogg"), channel: TELEPORT_CHANNEL_BLUESPACE); //4 tiles per crystal
    }

    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1350
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        if (current_cycle > 10 && SPT_PROB(7.5f, seconds_per_tick))
        {
            to_chat(mob, span_warning(I18n.T("You feel unstable...")));
            await mob.set_jitter_if_lower(2 * SECONDS);
            current_cycle = 1;
            _addtimer(Dm.@new<datum_callback>(mob, nameof(mob_living.bluespace_shuffle)), 3 * SECONDS, file: "code/modules/reagents/chemistry/reagents/other_reagents.dm", line: 1356);
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1358 (interleaved types, rules 1.9)
public partial class mob_living : mob
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1358
    public virtual async DmTask bluespace_shuffle()
    {
        await do_teleport(this, get_turf(this), 5, asoundin: Dm.resource("sound/effects/phasein.ogg"), channel: TELEPORT_CHANNEL_BLUESPACE);
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1379 (gaps RG3: welding fuel lit by a spark)
public partial class datum_reagent_fuel : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1425
    public override async DmTask<object> on_spark_act(float? power_charge = null, float? spark_flags = null)
    {
        power_charge ??= 0;
        spark_flags ??= NONE;
        // Doesn't go boom in open air unless we pass a REALLY high current or if we're hot enough
        if (!Dm.truthy(Dm.bitand(spark_flags, SPARK_ACT_ENCLOSED)) && power_charge < STANDARD_BATTERY_VALUE && holder.chem_temp < 474)
        {
            turf our_turf = get_turf(holder.my_atom);
            our_turf?.hotspot_expose(holder.chem_temp * 10, volume);
            return NONE;
        }

        float strengthdiv = 10;
        if (Dm.truthy(Dm.bitand(spark_flags, SPARK_ACT_WEAKEN_COMMON)))
            strengthdiv *= 3; // Noticeably weaker than waterpot, at least put some effort in, cmon

        await reagent_explode(holder, volume, strengthdiv: strengthdiv, clear_holder_reagents: FALSE, flame_factor: 1);
        return Dm.bitor(SPARK_ACT_DESTRUCTIVE, SPARK_ACT_CLEAR_ALL);
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1439
public partial class datum_reagent_space_cleaner : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1451
    public override async DmTask<object> expose_obj(obj exposed_obj = null, object reac_volume = null, float? methods = null, float? show_message = null)
    {
        methods ??= TOUCH;
        show_message ??= TRUE;
        object dot = await base.expose_obj(exposed_obj, reac_volume, methods, show_message);
        if (exposed_obj != null)
            await exposed_obj.wash(clean_types);
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1455
    public override async DmTask<object> expose_turf(turf exposed_turf = null, object reac_volume = null)
    {
        object dot = await base.expose_turf(exposed_turf, reac_volume);
        if (Dm.num(reac_volume) < 1)
            return dot;

        await exposed_turf.wash(clean_types, TRUE);

        foreach (mob_living_basic_slime exposed_slime in Dm.each<mob_living_basic_slime>(exposed_turf))
            await exposed_slime.adjust_tox_loss(rand(5, 10));
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1465
    public override async DmTask expose_mob(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra)
    {
        methods ??= TOUCH;
        show_message ??= TRUE;
        touch_protection ??= 0;
        await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);
        if (!Dm.truthy(Dm.bitand(methods, Dm.bitor(VAPOR, TOUCH))))
            return;

        await exposed_mob.wash(clean_types);
    }

    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1472
    public override async DmTask on_burn_wound_processing(datum_wound_burn_flesh burn_wound = null)
    {
        burn_wound.sanitization += 0.3f;
        if (prob(5))
        {
            to_chat(burn_wound.victim, span_notice(I18n.T("Your [] stings and burns from [] covering it! It <i>does</i> look pretty clean though.", burn_wound, this)));
            await burn_wound.victim.apply_damage(0.5f, TOX);
            await burn_wound.victim.apply_damage(0.5f, BURN, burn_wound.limb, wound_bonus: CANT_WOUND);
        }
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1479
public partial class datum_reagent_space_cleaner_ez_clean : datum_reagent_space_cleaner
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1489
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        float heal = 1.1f * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        float need_mob_update;
        need_mob_update = Dm.num(await mob.adjust_brute_loss(heal, updating_health: FALSE));
        need_mob_update += Dm.num(await mob.adjust_fire_loss(heal, updating_health: FALSE));
        need_mob_update += Dm.num(await mob.adjust_tox_loss(heal, updating_health: FALSE));
        if (Dm.truthy(need_mob_update))
            return UPDATE_MOB_HEALTH;
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1499
    public override async DmTask expose_mob(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra)
    {
        methods ??= TOUCH;
        show_message ??= TRUE;
        touch_protection ??= 0;
        await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);
        if (!Dm.truthy(Dm.bitand(methods, Dm.bitor(TOUCH, VAPOR))) || issilicon(exposed_mob))
            return;

        float damage_to_inflict = 1.5f * max(1 - touch_protection.Value, 0);

        if (Dm.truthy(damage_to_inflict))
        {
            await exposed_mob.adjust_brute_loss(damage_to_inflict);
            await exposed_mob.adjust_fire_loss(damage_to_inflict);
        }
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1510
public partial class datum_reagent_cryptobiolin : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1520
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        await mob.set_dizzy_if_lower(2 * SECONDS);

        // Cryptobiolin adjusts the mob's confusion down to 20 seconds if it's higher,
        // or up to 1 second if it's lower, but will do nothing if it's in between
        float confusion_left = Dm.num(mob.get_timed_status_effect_duration(typeof(datum_status_effect_confusion)));
        if (confusion_left < 1 * SECONDS)
            await mob.set_confusion(1 * SECONDS);

        else if (confusion_left > 20 * SECONDS)
            await mob.set_confusion(20 * SECONDS);
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1533
public partial class datum_reagent_impedrezene : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1543
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        await mob.adjust_jitter(-5 * SECONDS * Dm.num(seconds_per_tick));
        if (SPT_PROB(55, seconds_per_tick))
        {
            await mob.adjust_organ_loss(ORGAN_SLOT_BRAIN, 2);
            dot = TRUE;
        }
        if (SPT_PROB(30, seconds_per_tick))
            await mob.adjust_drowsiness(6 * SECONDS);
        if (SPT_PROB(5, seconds_per_tick))
            await mob.emote("drool");
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1672
public partial class datum_reagent_carbondioxide : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1681
    public override async DmTask<object> expose_turf(turf exposed_turf = null, object reac_volume = null)
    {
        if (istype(exposed_turf, typeof(turf_open)))
            exposed_turf.atmos_spawn_air(Dm.text("[]=[];[]", GAS_CO2, Dm.num(reac_volume) / 20, TURF_TEMPERATURE(holder != null ? holder.chem_temp : T20C)));
        return await base.expose_turf(exposed_turf, reac_volume);
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1990
public partial class datum_reagent_stable_plasma : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:2000
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        await mob.adjustPlasma(5 * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:2013
public partial class datum_reagent_carpet : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:2022
    public override async DmTask<object> expose_turf(turf exposed_turf = null, object reac_volume = null)
    {
        if (isopenturf(exposed_turf) && Dm.truthy(Dm.bitand(exposed_turf.turf_flags, IS_SOLID)) && !istype(exposed_turf, typeof(turf_open_floor_carpet)))
            await exposed_turf.place_on_top(carpet_type, flags: CHANGETURF_INHERIT_AIR);
        return await base.expose_turf(exposed_turf, reac_volume);
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:2090
public partial class datum_reagent_carpet_royal : datum_reagent_carpet
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:2094
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        var liver = mob.get_organ_slot(ORGAN_SLOT_LIVER) as obj_item_organ_liver;
        if (liver != null)
        {
            // Heads of staff and the captain have a "royal metabolism"
            if (HAS_TRAIT(liver, TRAIT_ROYAL_METABOLISM))
            {
                if (SPT_PROB(5, seconds_per_tick))
                    to_chat(mob, I18n.T("You feel like royalty."));
                if (SPT_PROB(2.5f, seconds_per_tick))
                    await mob.say(pick("Peasants..", "This carpet is worth more than your contracts!", "I could fire you at any time..."), forced: "royal carpet");
            }

            // The quartermaster, as a semi-head, has a "pretender royal" metabolism
            else if (HAS_TRAIT(liver, TRAIT_PRETENDER_ROYAL_METABOLISM))
            {
                if (SPT_PROB(8, seconds_per_tick))
                    to_chat(mob, I18n.T("You feel like an impostor..."));
            }
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:2280
public partial class datum_reagent_acetone_oxide : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:2288
    public override async DmTask expose_mob(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra) //Splashing people kills people!
    {
        methods ??= TOUCH;
        show_message ??= TRUE;
        touch_protection ??= 0;
        await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);
        if (!Dm.truthy(Dm.bitand(methods, TOUCH)))
            return;

        float damage_to_inflict = 2 * max(1 - touch_protection.Value, 0);

        if (Dm.truthy(damage_to_inflict))
            await exposed_mob.adjust_fire_loss(damage_to_inflict);

        await exposed_mob.adjust_fire_stacks((Dm.num(reac_volume) / 10));
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:2332
public partial class datum_reagent_colorful_reagent : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:2350
    public override async DmTask New(params object[] __extra)
    {
        color_callback = Dm.@new<datum_callback>(this, nameof(UpdateColor));
        SSticker?.OnRoundstart(color_callback);
        await base.New(__extra);
    }

    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:2355
    public override object Destroy(float? force = null)
    {
        if (SSticker != null)
            LAZYREMOVE(ref SSticker.round_end_events, color_callback); //Prevents harddels during roundstart
        color_callback = null; //Fly free little callback
        return base.Destroy(force);
    }

    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:2360
    public virtual void UpdateColor()
    {
        color_callback = null;
        color = Dm.str(pick(random_color_list));
    }

    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:2364
    public override async DmTask expose_mob(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra)
    {
        await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);
        object picked_color = pick(random_color_list);
        object color_filter = color_transition_filter(picked_color, SATURATION_OVERRIDE);
        if (Dm.truthy(can_color_clothing) && Dm.truthy(Dm.bitand(methods, Dm.bitor(Dm.bitor(TOUCH, VAPOR), INHALE))))
        {
            float include_flags = Dm.num(Dm.bitor(INCLUDE_HELD, INCLUDE_ACCESSORIES));
            if (Dm.truthy(Dm.bitand(methods, Dm.bitor(VAPOR, INHALE))) || Dm.num(touch_protection) >= 1)
                include_flags = Dm.num(Dm.bitor(include_flags, INCLUDE_POCKETS));
            // Not as anyting because this can produce nulls with the flags we passed
            foreach (obj_item to_color in Dm.each<obj_item>(exposed_mob.get_equipped_items(include_flags)))
                await to_color.add_atom_colour(color_filter, WASHABLE_COLOUR_PRIORITY);
        }

        if (ishuman(exposed_mob))
        {
            var exposed_human = (mob_living_carbon_human)exposed_mob;
            exposed_human.set_facial_haircolor(picked_color, update: FALSE);
            exposed_human.set_haircolor(picked_color);
        }

        if (!Dm.truthy(can_color_mobs))
            return;

        if (!iscarbon(exposed_mob))
        {
            await exposed_mob.add_atom_colour(color_filter, WASHABLE_COLOUR_PRIORITY);
            return;
        }

        var exposed_carbon = (mob_living_carbon)exposed_mob;

        foreach (obj_item_organ organ in Dm.each_cast<obj_item_organ>(exposed_carbon.organs))
            await organ.add_atom_colour(color_filter, WASHABLE_COLOUR_PRIORITY);

        foreach (obj_item_bodypart part in Dm.each_cast<obj_item_bodypart>(exposed_carbon.get_bodyparts()))
            await part.add_atom_colour(color_filter, WASHABLE_COLOUR_PRIORITY);
    }

    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:2396
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living)affected_mob;

        if (!iscarbon(mob))
        {
            if (Dm.truthy(can_color_mobs))
                await mob.add_atom_colour(color_transition_filter(pick(random_color_list), SATURATION_OVERRIDE), WASHABLE_COLOUR_PRIORITY);
            return dot;
        }

        if (!Dm.truthy(can_color_organs))
            return dot;

        var carbon_mob = (mob_living_carbon)mob;
        float color_priority = WASHABLE_COLOUR_PRIORITY;
        if (current_cycle >= 30) // Seeps deep into your tissues
            color_priority = FIXED_COLOUR_PRIORITY;

        foreach (obj_item_organ organ in Dm.each_cast<obj_item_organ>(carbon_mob.organs))
            await organ.add_atom_colour(color_transition_filter(pick(random_color_list), SATURATION_OVERRIDE), color_priority);
        return dot;
    }

    /// Colors anything it touches a random color.
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:2416
    public override async DmTask<object> expose_atom(atom exposed_atom = null, object reac_volume = null, float? methods = null)
    {
        object dot = await base.expose_atom(exposed_atom, reac_volume, methods);
        if (!isliving(exposed_atom))
            await exposed_atom.add_atom_colour(color_transition_filter(pick(random_color_list), SATURATION_OVERRIDE), WASHABLE_COLOUR_PRIORITY);
        return dot;
    }
}

/////////////////////////Colorful Powder////////////////////////////
//For colouring in /proc/mix_color_from_reagents
// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1738
public partial class datum_reagent_colorful_reagent_powder : datum_reagent_colorful_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1746
    public override async DmTask New(params object[] __extra)
    {
        if (Dm.eq(colorname, "none"))
            description = "A rather mundane-looking powder. It doesn't look like it'd color much of anything...";
        else if (Dm.eq(colorname, "invisible"))
            description = "An invisible powder. Unfortunately, since it's invisible, it doesn't look like it'd color much of anything...";
        else
            description = Dm.text("\\An [] powder, used for coloring things [].", colorname, colorname);
        await base.New(__extra);
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:2421
public partial class datum_reagent_hair_dye : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:2431
    public override async DmTask New(params object[] __extra)
    {
        SSticker?.OnRoundstart(Dm.@new<datum_callback>(this, nameof(UpdateColor)));
        await base.New(__extra);
    }

    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:2435
    public virtual void UpdateColor()
    {
        color = Dm.str(pick(potential_colors));
    }

    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:2438
    public override async DmTask expose_mob(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra)
    {
        methods ??= TOUCH;
        show_message ??= TRUE;
        touch_protection ??= 0;
        await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);
        if (!Dm.truthy(Dm.bitand(methods, Dm.bitor(Dm.bitor(TOUCH, VAPOR), INHALE))) || !ishuman(exposed_mob))
            return;

        // tg bug kept (D86): min(..., 0, 100) is never above 0, so the dye is never refused
        float exposure_probability = min(100 - (touch_protection.Value * 100), 0, 100);
        if (Dm.truthy(exposure_probability) && !prob(exposure_probability))
            return;

        var exposed_human = (mob_living_carbon_human)exposed_mob;
        exposed_human.set_facial_haircolor(pick(potential_colors), update: FALSE);
        exposed_human.set_haircolor(pick(potential_colors)); //this will call update_body_parts()
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:2451
public partial class datum_reagent_barbers_aid : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:2460
    public override async DmTask expose_mob(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra)
    {
        methods ??= TOUCH;
        show_message ??= TRUE;
        touch_protection ??= 0;
        await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);
        if (!Dm.truthy(Dm.bitand(methods, Dm.bitor(TOUCH, VAPOR))) || !ishuman(exposed_mob) || (HAS_TRAIT(exposed_mob, TRAIT_BALD) && HAS_TRAIT(exposed_mob, TRAIT_SHAVED)))
            return;

        float exposure_probability = min(100 - (touch_protection.Value * 100), 0, 100);
        if (Dm.truthy(exposure_probability) && !prob(exposure_probability))
            return;

        var exposed_human = (mob_living_carbon_human)exposed_mob;
        if (!HAS_TRAIT(exposed_human, TRAIT_SHAVED))
        {
            object picked_beard = pick(SSaccessories.facial_hairstyles_list);
            exposed_human.set_facial_hairstyle(picked_beard, update: FALSE);
        }
        if (!HAS_TRAIT(exposed_human, TRAIT_BALD))
        {
            object picked_hair = pick(SSaccessories.hairstyles_list);
            exposed_human.set_hairstyle(picked_hair, update: TRUE);
        }
        to_chat(exposed_human, span_notice(I18n.T("Hair starts sprouting from your [].", HAS_TRAIT(exposed_human, TRAIT_BALD) ? I18n.T("face") : I18n.T("scalp"))));
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:2478
public partial class datum_reagent_concentrated_barbers_aid : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:2487
    public override async DmTask expose_mob(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra)
    {
        methods ??= TOUCH;
        show_message ??= TRUE;
        touch_protection ??= 0;
        await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);
        if (!Dm.truthy(Dm.bitand(methods, Dm.bitor(TOUCH, VAPOR))) || !ishuman(exposed_mob) || (HAS_TRAIT(exposed_mob, TRAIT_BALD) && HAS_TRAIT(exposed_mob, TRAIT_SHAVED)))
            return;

        float exposure_probability = min(100 - (touch_protection.Value * 100), 0, 100);
        if (Dm.truthy(exposure_probability) && !prob(exposure_probability))
            return;

        var exposed_human = (mob_living_carbon_human)exposed_mob;
        if (!HAS_TRAIT(exposed_human, TRAIT_SHAVED))
            exposed_human.set_facial_hairstyle("Beard (Very Long)", update: FALSE);
        if (!HAS_TRAIT(exposed_human, TRAIT_BALD))
            exposed_human.set_hairstyle("Very Long Hair", update: TRUE);
        to_chat(exposed_human, span_notice(I18n.T("Your[] hair starts growing at an incredible speed!", HAS_TRAIT(exposed_human, TRAIT_BALD) ? I18n.T(" facial") : "")));
    }

    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:2503
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        if (current_cycle > 21 / creation_purity)
        {
            if (!ishuman(mob))
                return dot;
            var human_mob = (mob_living_carbon_human)mob;
            // the quirks are owner-deferred (D333): no mob has the bald quirk, so remove_quirk never runs
            if (creation_purity == 1 && Dm.truthy(human_mob.has_quirk(typeof(datum_quirk_item_quirk_bald))))
                human_mob.remove_quirk(typeof(datum_quirk_item_quirk_bald));
            var head = human_mob.get_bodypart(BODY_ZONE_HEAD) as obj_item_bodypart_head;
            if (head == null || Dm.truthy(Dm.bitand(head.head_flags, HEAD_HAIR)))
                return dot;
            head.head_flags = Dm.num(Dm.bitor(head.head_flags, HEAD_HAIR));
            if (HAS_TRAIT(mob, TRAIT_BALD))
                to_chat(mob, span_warning(I18n.T("You feel your scalp mutate, but you are still hopelessly bald.")));
            else
            {
                to_chat(mob, span_notice(I18n.T("Your scalp mutates, a full head of hair sprouting from it.")));
                await human_mob.update_body_parts();
            }
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:2521
public partial class datum_reagent_baldium : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:2532
    public override async DmTask expose_mob(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra)
    {
        methods ??= TOUCH;
        show_message ??= TRUE;
        touch_protection ??= 0;
        await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);
        if (!Dm.truthy(Dm.bitand(methods, Dm.bitor(TOUCH, VAPOR))) || !ishuman(exposed_mob))
            return;

        float exposure_probability = min(100 - (touch_protection.Value * 100), 0, 100);
        if (Dm.truthy(exposure_probability) && !prob(exposure_probability))
            return;

        var exposed_human = (mob_living_carbon_human)exposed_mob;
        to_chat(exposed_human, span_danger(I18n.T("Your hair is falling out in clumps!")));
        exposed_human.set_facial_hairstyle("Shaved", update: FALSE);
        exposed_human.set_hairstyle("Bald", update: TRUE);
    }
}

// Bee chemicals

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:2663
public partial class datum_reagent_royal_bee_jelly : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:2672
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        if (SPT_PROB(1, seconds_per_tick))
            await mob.say(pick("Bzzz...", "BZZ BZZ", "Bzzzzzzzzzzz..."), forced: "royal bee jelly");
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:2708
public partial class datum_reagent_magillitis : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:2715
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        if ((ishuman(mob)) && current_cycle > 10)
        {
            var new_gorilla = await mob.gorillize() as mob_living_basic_gorilla;
            await new_gorilla.AddComponent(typeof(datum_component_regenerator), Dm.named("regeneration_delay", 12 * SECONDS), Dm.named("brute_per_second", 1.5f), Dm.named("outline_colour", COLOR_PALE_GREEN));
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:2721
public partial class datum_reagent_growthserum : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:2730
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        float newsize = current_size;
        // switch(volume): the first matching range runs
        float sw__1 = Dm.num(volume);
        if (sw__1 >= 0 && sw__1 <= 19)
            newsize = 1.25f * RESIZE_DEFAULT_SIZE;
        else if (sw__1 >= 20 && sw__1 <= 49)
            newsize = 1.5f * RESIZE_DEFAULT_SIZE;
        else if (sw__1 >= 50 && sw__1 <= 99)
            newsize = 2 * RESIZE_DEFAULT_SIZE;
        else if (sw__1 >= 100 && sw__1 <= 199)
            newsize = 2.5f * RESIZE_DEFAULT_SIZE;
        else if (sw__1 >= 200)
            newsize = 3.5f * RESIZE_DEFAULT_SIZE;

        await mob.update_transform(newsize / current_size);
        current_size = newsize;
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:2748
    public override async DmTask on_mob_end_metabolize(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.on_mob_end_metabolize(affected_mob, metabolization_ratio);
        await affected_mob.update_transform(RESIZE_DEFAULT_SIZE / current_size);
        current_size = RESIZE_DEFAULT_SIZE;
    }

    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:2753
    public override async DmTask<object> used_on_fish(obj_item_fish fish = null)
    {
        await DmTask.CompletedTask;
        ADD_TRAIT(fish, TRAIT_FISH_QUICK_GROWTH, type);
        _addtimer(TRAIT_CALLBACK_REMOVE(fish, TRAIT_FISH_QUICK_GROWTH, type), fish.feeding_frequency * 0.8f, TIMER_UNIQUE | TIMER_OVERRIDE, file: "code/modules/reagents/chemistry/reagents/other_reagents.dm", line: 2755);
        return TRUE;
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:2767
public partial class datum_reagent_glitter : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:2776
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        if (SPT_PROB(25, seconds_per_tick))
        {
            await mob.emote("cough");
            await expose_turf(get_turf(mob), 0);
        }
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:2782
    public override async DmTask<object> expose_turf(turf exposed_turf = null, object reac_volume = null)
    {
        object dot = await base.expose_turf(exposed_turf, reac_volume);

        if (!istype(exposed_turf, typeof(turf)))
            return dot;
        exposed_turf.spawn_glitter(((DmList)data)["colors"] as DmList);
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:2789
    public override async DmTask on_new(object data = null)
    {
        await base.on_new(data);

        if (Dm.truthy(((DmList)this.data)["colors"]))
            color = Dm.str(pick_weight(((DmList)this.data)["colors"] as DmList));
        else
            color = COLOR_WHITE;
    }

    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:2797
    public override async DmTask on_merge(DmList mix_data = null, object amount = null)
    {
        await base.on_merge(mix_data, amount);

        float prop_current = (Dm.num(volume) - Dm.num(amount)) / (Dm.num(volume));

        DmList my_data = (DmList)data;
        if (mix_data != null)
            my_data["colors"] = blend_weighted_lists(mix_data["colors"] as DmList, my_data["colors"] as DmList, prop_current);

        if (Dm.truthy(my_data["colors"]))
            color = Dm.str(pick_weight(my_data["colors"] as DmList));
        else
            color = COLOR_WHITE;
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:2810
public partial class datum_reagent_glitter_random : datum_reagent_glitter
{
    // The weighted list of random color choices that can be chosen upon spawning
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:2812  var/static/list/possible_colors (rules 10.6)
    public static DmList possible_colors => TgState.Current.datum_reagent_glitter_random__possible_colors ??= Dm.list(
        Dm.kv(COLOR_WHITE, 25),
        Dm.kv("#ff8080", 25),
        Dm.kv("#4040ff", 25),
        Dm.kv("#ff5555", 8),
        Dm.kv("#55ff55", 9),
        Dm.kv("#5555ff", 8)
    );

    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:2822
    public override async DmTask on_new(object data = null)
    {
        ((DmList)this.data)["colors"] = possible_colors;
        await base.on_new(data);
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:2826
public partial class datum_reagent_confetti : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:2833
    public override async DmTask<object> expose_turf(turf exposed_turf = null, object reac_volume = null)
    {
        object dot = await base.expose_turf(exposed_turf, reac_volume);
        if (!istype(exposed_turf, typeof(turf)))
            return dot;
        exposed_turf.spawn_unique_cleanable(typeof(obj_effect_decal_cleanable_confetti));
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:2850
public partial class datum_reagent_bz_metabolites : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:2859
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var target = (mob_living_carbon)affected_mob;
        await target.adjust_hallucinations(12.5f * SECONDS * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        var changeling = IS_CHANGELING(target) as datum_antagonist_changeling;
        changeling?.adjust_chemicals(-5 * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        return dot;
    }
}

/// Improvised reagent that induces vomiting. Created by dipping a dead mouse in welder fluid.
// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:2943
public partial class datum_reagent_yuck : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:2961  #define YUCK_PUKE_CYCLES 3 (file-local, #undef at :2985)
    private const int YUCK_PUKE_CYCLES = 3; // every X cycle is a puke
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:2962  #define YUCK_PUKES_TO_STUN 3 (file-local, #undef at :2986)
    private const int YUCK_PUKES_TO_STUN = 3; // hit this amount of pukes in a row to start stunning

    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:2959
    public override async DmTask on_mob_add(mob_living affected_mob = null, object amount = null)
    {
        if (HAS_TRAIT(affected_mob, TRAIT_NOHUNGER)) //they can't puke
            await holder.del_reagent(type);
        await base.on_mob_add(affected_mob, amount);
    }

    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:2966
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        if (!Dm.truthy(yuck_cycle))
        {
            if (SPT_PROB(4, seconds_per_tick))
            {
                object dread = pick(I18n.T("Something is moving in your stomach..."),
                    I18n.T("A wet growl echoes from your stomach..."),
                    I18n.T("For a moment you feel like your surroundings are moving, but it's your stomach..."));
                to_chat(mob, span_userdanger(Dm.text("[]", dread)));
                yuck_cycle = current_cycle;
            }
        }
        else
        {
            float yuck_cycles = current_cycle - yuck_cycle;
            if (Dm.num(Dm.mod(yuck_cycles, YUCK_PUKE_CYCLES)) == 0)
            {
                if (yuck_cycles >= YUCK_PUKE_CYCLES * YUCK_PUKES_TO_STUN)
                {
                    if (holder != null)
                        await holder.remove_reagent(type, 5);
                }
                float passable_flags = Dm.num(Dm.bitor(MOB_VOMIT_MESSAGE, MOB_VOMIT_HARM));
                if (yuck_cycles >= (YUCK_PUKE_CYCLES * YUCK_PUKES_TO_STUN))
                    passable_flags = Dm.num(Dm.bitor(passable_flags, MOB_VOMIT_STUN));
                await mob.vomit(vomit_flags: passable_flags, lost_nutrition: rand(14, 26));
            }
        }
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:2989
    public override async DmTask on_mob_end_metabolize(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.on_mob_end_metabolize(affected_mob, metabolization_ratio);
        yuck_cycle = 0; // reset vomiting
    }

    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:2993
    public override async DmTask expose_mob(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra)
    {
        await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);
        object expose_volume = reac_volume;
        if (!Dm.truthy(Dm.bitand(methods, INGEST)) || !iscarbon(exposed_mob))
            return;

        var mob_reagents = (datum_reagents)exposed_mob.reagents;
        await mob_reagents.remove_reagent(type, expose_volume);
        await mob_reagents.add_reagent(typeof(datum_reagent_fuel), Dm.num(expose_volume) * 0.75f);
        await mob_reagents.add_reagent(typeof(datum_reagent_water), Dm.num(expose_volume) * 0.25f);
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:3036
public partial class datum_reagent_metalgen : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:3044
    public override async DmTask<object> expose_obj(obj exposed_obj = null, object reac_volume = null, float? methods = null, float? show_message = null)
    {
        methods ??= TOUCH;
        show_message ??= TRUE;
        object dot = await base.expose_obj(exposed_obj, reac_volume, methods, show_message);
        // Don't morph holographic or abstract objects
        if (!Dm.truthy(Dm.bitand(exposed_obj.flags_1, HOLOGRAM_1)) && exposed_obj.invisibility < INVISIBILITY_ABSTRACT)
            await metal_morph(exposed_obj);
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:3050
    public override async DmTask<object> expose_turf(turf exposed_turf = null, object reac_volume = null)
    {
        object dot = await base.expose_turf(exposed_turf, reac_volume);
        // Or non-real turfs
        if (isclosedturf(exposed_turf) || isopenturf(exposed_turf))
            await metal_morph(exposed_turf);
        return dot;
    }

    ///turn an object into a special material
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:3057
    public virtual async DmTask metal_morph(atom target = null)
    {
        object metal_ref = ((DmList)data)["material"];
        if (!Dm.truthy(metal_ref))
            return;

        await metal_transmute(target, metal_ref);
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:3065 (a global proc, rules 7.8)
public static partial class GlobalProcs
{
    /// Change the material type of an object or turf, but with visuals and effects related to the material
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:3065
    public static async DmTask metal_transmute(atom target = null, object metal_type = null, object default_material_amount = null, object applied_material_flags = null)
    {
        default_material_amount ??= 100;
        applied_material_flags ??= MATERIAL_METALGEN;
        if (Dm.truthy(is_type_in_typecache(target, GLOB.blacklisted_metalgen_types))) //some stuff can lead to exploits if transmuted
            return;

        float metal_amount = 0;
        DmList materials_to_transmute = await target.get_material_composition() as DmList;
        foreach (object metal_key in Dm.each(materials_to_transmute)) //list with what they're made of
            metal_amount += Dm.num(materials_to_transmute[metal_key]);

        if (!Dm.truthy(metal_amount))
            metal_amount = Dm.num(default_material_amount); //some stuff doesn't have materials at all. To still give them properties, we give them a material. Basically doesn't exist

        // Delete existing materials first before changing the material flags
        if (Dm.truthy(length(target.custom_materials)))
            await target.set_custom_materials();
        DmList metal_dat = Dm.list(Dm.kv(metal_type, metal_amount));
        target.material_flags = Dm.num(applied_material_flags);
        if (Dm.truthy(target.has_material_slots()))
        {
            DmList new_slots = target.get_material_slots() as DmList;
            foreach (object slot_type in Dm.each(new_slots))
                new_slots[slot_type] = metal_type;
            // Safe to call and doesn't do anything as no materials are currently present on the target
            await target.set_material_slots(new_slots);
        }
        await target.set_custom_materials(metal_dat);
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:3090
public partial class datum_reagent_gravitum : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:3103
    public override async DmTask<object> expose_obj(obj exposed_obj = null, object reac_volume = null, float? methods = null, float? show_message = null)
    {
        methods ??= TOUCH;
        show_message ??= TRUE;
        object dot = await base.expose_obj(exposed_obj, reac_volume, methods, show_message);
        await exposed_obj.AddElement(typeof(datum_element_forced_gravity), 0);
        _addtimer(Dm.@new<datum_callback>(exposed_obj, nameof(datum._RemoveElement), Dm.list(typeof(datum_element_forced_gravity), 0)), Dm.num(volume) * time_multiplier, Dm.num(TIMER_UNIQUE | TIMER_OVERRIDE), file: "code/modules/reagents/chemistry/reagents/other_reagents.dm", line: 3106);
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:3108
    public override async DmTask on_mob_metabolize(mob_living affected_mob = null, params object[] __extra)
    {
        await base.on_mob_metabolize(affected_mob, __extra);
        await affected_mob.AddElement(typeof(datum_element_forced_gravity), 0); //0 is the gravity, and in this case weightless
    }

    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:3112
    public override async DmTask on_mob_end_metabolize(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.on_mob_end_metabolize(affected_mob, metabolization_ratio);
        affected_mob.RemoveElement(typeof(datum_element_forced_gravity), 0);
    }
}

// who knows.
// maybe nar'sie is considered to be too "mainstream" of a god to worship in the heretic community.
// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:3167
public partial class datum_reagent_eldritch : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:3179
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var drinker = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        float common = 0.4f * k;
        float need_mob_update = FALSE;
        // IS_HERETIC_OR_MONSTER(drinker) (__DEFINES/antagonists.dm:300, with IS_HERETIC :293 and IS_LUNATIC :298), expanded inline (rules 11.1)
        if (Dm.truthy(drinker.mind?.has_antag_datum(typeof(datum_antagonist_heretic))) || HAS_TRAIT(drinker, TRAIT_ACT_AS_HERETIC) || HAS_TRAIT(drinker, TRAIT_HERETIC_SUMMON) || Dm.truthy(drinker.mind?.has_antag_datum(typeof(datum_antagonist_lunatic))))
        {
            await drinker.adjust_drowsiness(-2 * k);
            await drinker.AdjustAllImmobility(-8 * k);
            need_mob_update += Dm.num(await drinker.adjust_stamina_loss(-2 * k, updating_stamina: FALSE));
            need_mob_update += Dm.num(await drinker.adjust_tox_loss(-common, updating_health: FALSE, forced: TRUE));
            need_mob_update += Dm.num(await drinker.adjust_oxy_loss(-common, updating_health: FALSE));
            need_mob_update += Dm.num(await drinker.adjust_brute_loss(-common, updating_health: FALSE));
            need_mob_update += Dm.num(await drinker.adjust_fire_loss(-common, updating_health: FALSE));
            drinker.adjust_blood_volume(0.6f * k, maximum: BLOOD_VOLUME_NORMAL);
            // Slowly regulates your body temp
            drinker.adjust_bodytemperature((Dm.num(drinker.get_body_temp_normal()) - drinker.bodytemperature) / 5);
            foreach (datum_reagent reagent in Dm.each_cast<datum_reagent>(((datum_reagents)drinker.reagents).reagent_list))
            {
                if (reagent != this)
                    await ((datum_reagents)drinker.reagents).remove_reagent(reagent.type, 0.8f * reagent.purge_multiplier * k);
            }
        }
        else
        {
            need_mob_update = Dm.num(await drinker.adjust_organ_loss(ORGAN_SLOT_BRAIN, 0.6f * k, 150));
            need_mob_update += Dm.num(await drinker.adjust_tox_loss(common, updating_health: FALSE));
            need_mob_update += Dm.num(await drinker.adjust_fire_loss(common, updating_health: FALSE));
            need_mob_update += Dm.num(await drinker.adjust_oxy_loss(common, updating_health: FALSE));
            need_mob_update += Dm.num(await drinker.adjust_brute_loss(common, updating_health: FALSE));
        }
        if (Dm.truthy(need_mob_update))
            return UPDATE_MOB_HEALTH;
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:3206
    public override async DmTask<object> expose_turf(turf exposed_turf = null, object reac_volume = null)
    {
        object dot = await base.expose_turf(exposed_turf, reac_volume);
        if ((Dm.num(reac_volume) >= 1.5f || isplatingturf(exposed_turf)))
            await exposed_turf.rust_heretic_act(RUST_RESISTANCE_TITANIUM);
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:3211
public partial class datum_reagent_universal_indicator : datum_reagent
{
    //Colours things by their pH
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:3220
    public override async DmTask<object> expose_atom(atom exposed_atom = null, object reac_volume = null, float? methods = null)
    {
        object dot = await base.expose_atom(exposed_atom, reac_volume, methods);
        if (exposed_atom.reagents != null)
        {
            object color = null;
            CONVERT_PH_TO_COLOR(((datum_reagents)exposed_atom.reagents).ph, ref color);
            await exposed_atom.add_atom_colour(color, WASHABLE_COLOUR_PRIORITY);
        }
        return dot;
    }
}

// [Original ants concept by Keelin on Goon]
// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:3228
public partial class datum_reagent_ants : datum_reagent
{
    /// List of possible common statements to scream when eating ants
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:3243  var/static/list/ant_screams (rules 10.6)
    public static DmList ant_screams => TgState.Current.datum_reagent_ants__ant_screams ??= Dm.list(
        "THEY'RE UNDER MY SKIN!!",
        "GET THEM OUT OF ME!!",
        "HOLY HELL THEY BURN!!",
        "MY GOD THEY'RE INSIDE ME!!",
        "GET THEM OUT!!"
    );

    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:3260
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var victim = (mob_living_carbon)affected_mob;
        await victim.adjust_brute_loss(0.2f * max(0.1f, round((Dm.num(ant_ticks) * Dm.num(ant_damage)), 0.1f)) * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick)); //Scales with time. Roughly 32 brute with 100u.
        ant_ticks = Dm.num(ant_ticks) + 1;
        if (Dm.num(ant_ticks) < 5) // Makes ant food a little more appetizing, since you won't be screaming as much.
            return dot;
        if (SPT_PROB(5, seconds_per_tick))
        {
            if (SPT_PROB(5, seconds_per_tick)) //Super rare statement
                await victim.say("AUGH NO NOT THE ANTS! NOT THE ANTS! AAAAUUGH THEY'RE IN MY EYES! MY EYES! AUUGH!!", forced: type);
            else
                await victim.say(pick(ant_screams), forced: type);
        }
        if (SPT_PROB(15, seconds_per_tick))
            await victim.emote("scream");
        if (SPT_PROB(2, seconds_per_tick)) // Stuns, but purges ants.
            await victim.vomit(VOMIT_CATEGORY_DEFAULT, lost_nutrition: rand(5, 10), purge_ratio: 1);
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:3276
    public override async DmTask on_mob_end_metabolize(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.on_mob_end_metabolize(affected_mob, metabolization_ratio);
        ant_ticks = 0;
        to_chat(affected_mob, span_notice(I18n.T("You feel like the last of \\the [] are out of your system.", this)));
    }

    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:3281
    public override async DmTask expose_mob(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra)
    {
        methods ??= TOUCH;
        show_message ??= TRUE;
        touch_protection ??= 0;
        await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);
        if (!iscarbon(exposed_mob))
            return;
        if (Dm.truthy(Dm.bitand(methods, INGEST)))
            await exposed_mob.check_allergic_reaction(BUGS, chance: Dm.num(reac_volume) * 10, histamine_add: min(10, Dm.num(reac_volume)));
        if (!Dm.truthy(Dm.bitand(methods, Dm.bitor(Dm.bitor(PATCH, TOUCH), VAPOR))))
            return;

        amount_left = round(Dm.num(reac_volume), 0.1f) * (1 - touch_protection.Value);
        if (Dm.truthy(amount_left))
            await exposed_mob.apply_status_effect(status_effect, amount_left);
    }

    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:3294
    public override async DmTask<object> expose_obj(obj exposed_obj = null, object reac_volume = null, float? methods = null, float? show_message = null)
    {
        methods ??= TOUCH;
        show_message ??= TRUE;
        object dot = await base.expose_obj(exposed_obj, reac_volume, methods, show_message);
        var my_turf = exposed_obj.loc as turf_open; // No dumping ants on an object in a storage slot
        if (!istype(my_turf, typeof(turf_open))) //Are we actually in an open turf?
            return dot;
        DmList accepted_types = expose_obj__accepted_types;
        if (!Dm.truthy(accepted_types[exposed_obj.type])) // Bypasses pipes, vents, and cables to let people create ant mounds on top easily.
            return dot;
        await expose_turf(my_turf, reac_volume);
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:3299  expose_obj's var/static/list/accepted_types (rules 10.6)
    private static DmList expose_obj__accepted_types => TgState.Current.datum_reagent_ants__expose_obj__accepted_types ??= typecacheof(Dm.list(typeof(obj_machinery_atmospherics), typeof(obj_structure_cable), typeof(obj_structure_disposalpipe))) as DmList;

    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:3304
    public override async DmTask<object> expose_turf(turf exposed_turf = null, object reac_volume = null)
    {
        object dot = await base.expose_turf(exposed_turf, reac_volume);
        if (!istype(exposed_turf, typeof(turf))) // Is the turf valid
            return dot;
        if ((Dm.num(reac_volume) <= 10)) // Makes sure people don't duplicate ants.
            return dot;

        var pests = exposed_turf.spawn_unique_cleanable(ants_decal) as obj_effect_decal_cleanable_ants;
        if (QDELETED(pests))
            return dot;

        pests.decal_reagent = type;
        float rounded_volume = round(Dm.num(reac_volume), 1);
        pests.reagent_amount = rounded_volume;
        if (Dm.truthy(await pests.lazy_init_reagents())) // Decal already has reagents inited, add instead
        {
            var pests_reagents = (datum_reagents)pests.reagents;
            pests_reagents.maximum_volume = min(pests_reagents.maximum_volume + rounded_volume, 300); // Increase reagent holder volume up to a max of 300
            await pests_reagents.add_reagent(type, rounded_volume);
        }
        await pests.update_ant_damage();
        return dot;
    }
}

//This is intended to a be a scarce reagent to gate certain drugs and toxins with. Do not put in a synthesizer. Renewable sources of this reagent should be inefficient.
// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:3338
public partial class datum_reagent_lead : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:3345
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        // tg bug kept (D86): tg's on_mob_life(mob/living/carbon/victim, metabolization_ratio) names its second argument
        // metabolization_ratio, but the holder passes seconds_per_tick there; the damage reads neither.
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var victim = (mob_living_carbon)affected_mob;
        if (Dm.truthy(await victim.adjust_organ_loss(ORGAN_SLOT_BRAIN, 0.5f)))
            return UPDATE_MOB_HEALTH;
        return dot;
    }
}

//The main feedstock for kronkaine production, also a shitty stamina healer.
// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:3351
public partial class datum_reagent_kronkus_extract : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:3361
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var kronkus_enjoyer = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        float need_mob_update;
        need_mob_update = Dm.num(await kronkus_enjoyer.adjust_organ_loss(ORGAN_SLOT_HEART, 0.2f * k, required_organ_flag: affected_organ_flags));
        need_mob_update += Dm.num(await kronkus_enjoyer.adjust_stamina_loss(-6 * k, updating_stamina: FALSE));
        if (Dm.truthy(need_mob_update))
            return UPDATE_MOB_HEALTH;
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:3369
public partial class datum_reagent_brimdust : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:3377
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        if (Dm.truthy(await mob.adjust_fire_loss(Dm.truthy(Dm.bitand(mob.mob_biotypes, MOB_PLANT)) ? -1 : 1 * Dm.num(seconds_per_tick), updating_health: FALSE)))
            return UPDATE_MOB_HEALTH;
        return dot;
    }
}

// I made this food....with love.
// Reagent added to food by chef's with a chef's kiss. Makes people happy.
// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:3390
public partial class datum_reagent_love : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:3398
    public override async DmTask expose_mob(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra)
    {
        await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);
        // A syringe is not grandma's cooking
        if (Dm.truthy(Dm.bitand(methods, Dm.bitnot(INGEST))))
            await ((datum_reagents)exposed_mob.reagents).del_reagent(type);
    }

    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:3404
    public override async DmTask on_mob_metabolize(mob_living affected_mob = null, params object[] __extra)
    {
        await base.on_mob_metabolize(affected_mob, __extra);
        await affected_mob.add_mood_event(name, typeof(datum_mood_event_love_reagent));
    }

    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:3408
    public override async DmTask on_mob_delete(mob_living affected_mob = null)
    {
        await base.on_mob_delete(affected_mob);
        // When we exit the system we'll leave the moodlet based on the amount we had
        float duration_of_moodlet = current_cycle * 20 * SECONDS;
        affected_mob.clear_mood_event(name);
        await affected_mob.add_mood_event(name, typeof(datum_mood_event_love_reagent), duration_of_moodlet);
    }

    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:3415
    public override async DmTask<object> overdose_process(mob_living affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.overdose_process(affected_mob, seconds_per_tick, metabolization_ratio);
        var carbon_metabolizer = affected_mob as mob_living_carbon;
        if (!istype(carbon_metabolizer, typeof(mob_living_carbon)) || !Dm.truthy(carbon_metabolizer.can_heartattack()) || Dm.truthy(carbon_metabolizer.undergoing_cardiac_arrest()))
        {
            await ((datum_reagents)affected_mob.reagents).del_reagent(type);
            return dot;
        }

        if (SPT_PROB(10, seconds_per_tick))
            carbon_metabolizer.set_heartattack(TRUE);
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:3425
public partial class datum_reagent_hauntium : datum_reagent
{
    //gives 20 seconds of haunting effect for every unit of it that touches an item
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:3437
    public override async DmTask<object> expose_obj(obj exposed_obj = null, object reac_volume = null, float? methods = null, float? show_message = null)
    {
        methods ??= TOUCH;
        show_message ??= TRUE;
        object dot = await base.expose_obj(exposed_obj, reac_volume, methods, show_message);
        if (!isitem(exposed_obj))
            return dot;
        var exposed_item = (obj_item)exposed_obj;
        if (HAS_TRAIT_FROM(exposed_item, TRAIT_HAUNTED, HAUNTIUM_REAGENT_TRAIT))
            return dot;
        exposed_item.make_haunted(HAUNTIUM_REAGENT_TRAIT, "#f8f8ff");
        _addtimer(Dm.@new<datum_callback>(exposed_item, nameof(obj_item.remove_haunted), HAUNTIUM_REAGENT_TRAIT), Dm.num(reac_volume) * 20 * SECONDS, file: "code/modules/reagents/chemistry/reagents/other_reagents.dm", line: 3445);
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:3447
    public override async DmTask on_mob_metabolize(mob_living affected_mob = null, params object[] __extra)
    {
        await base.on_mob_metabolize(affected_mob, __extra);
        to_chat(affected_mob, span_userdanger(I18n.T("You feel an evil presence inside you!")));
        if (Dm.truthy(Dm.bitand(affected_mob.mob_biotypes, MOB_UNDEAD)) || HAS_MIND_TRAIT(affected_mob, TRAIT_MORBID))
            await affected_mob.add_mood_event("morbid_hauntium", typeof(datum_mood_event_morbid_hauntium), name); //8 minutes of slight mood buff if undead or morbid
        else
            await affected_mob.add_mood_event("hauntium_spirits", typeof(datum_mood_event_hauntium_spirits), name); //8 minutes of mood debuff
    }

    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:3455
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        if (Dm.truthy(Dm.bitand(mob.mob_biotypes, MOB_UNDEAD)) || HAS_MIND_TRAIT(mob, TRAIT_MORBID)) //if morbid or undead,acts like an addiction-less drug
        {
            float common = -3.34f * SECONDS * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
            mob.remove_status_effect(typeof(datum_status_effect_jitter));
            await mob.AdjustStun(common);
            await mob.AdjustKnockdown(common);
            await mob.AdjustUnconscious(common);
            await mob.AdjustParalyzed(common);
            await mob.AdjustImmobilized(common);
        }
        else
        {
            if (Dm.truthy(await mob.adjust_organ_loss(ORGAN_SLOT_HEART, 1.34f * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick)))) //1 heart damage per tick
                dot = UPDATE_MOB_HEALTH;
            if (SPT_PROB(10, seconds_per_tick))
                await mob.emote(pick("twitch", "choke", "shiver", "gag"));
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:3476
public partial class datum_reagent_luminescent_fluid : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:3495
    public override async DmTask on_mob_metabolize(mob_living affected_mob = null, params object[] __extra)
    {
        await base.on_mob_metabolize(affected_mob, __extra);
        if (Dm.num(volume) > 20) // Even if you don't have eyes, your eyeholes still glow :)
        {
            glowing = Dm.@new<obj_item_flashlight_eyelight_glow>(affected_mob);
            glowing.set_light_color(color);
            await glowing.set_light_on(TRUE);
            added_light = TRUE;
        }

        if (!ishuman(affected_mob))
            return;

        var affected_human = (mob_living_carbon_human)affected_mob;
        await affected_human.add_eye_color(color, EYE_COLOR_LUMINESCENT_PRIORITY);
        RegisterSignal(affected_human, COMSIG_CARBON_GAIN_ORGAN, nameof(on_organ_added));
        RegisterSignal(affected_human, COMSIG_CARBON_LOSE_ORGAN, nameof(on_organ_removed));
        var eyes = affected_human.get_organ_slot(ORGAN_SLOT_EYES) as obj_item_organ_eyes;
        if (eyes != null && !Dm.truthy(IS_ROBOTIC_ORGAN(eyes)))
            ADD_TRAIT(affected_human, TRAIT_LUMINESCENT_EYES, REF(this));
    }

    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:3514
    public override async DmTask on_mob_end_metabolize(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.on_mob_end_metabolize(affected_mob, metabolization_ratio);
        QDEL_NULL(ref glowing);
        if (!ishuman(affected_mob))
            return;

        var affected_human = (mob_living_carbon_human)affected_mob;
        await affected_human.remove_eye_color(EYE_COLOR_LUMINESCENT_PRIORITY);
        var eyes = affected_human.get_organ_slot(ORGAN_SLOT_EYES) as obj_item_organ_eyes;
        if (eyes != null && !Dm.truthy(IS_ROBOTIC_ORGAN(eyes)) && !Dm.truthy(overdosed))
            REMOVE_TRAIT(affected_human, TRAIT_LUMINESCENT_EYES, REF(this));
    }

    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:3526
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living)affected_mob;

        if (glowing == null && !Dm.truthy(added_light) && Dm.num(volume) > 20)
        {
            glowing = Dm.@new<obj_item_flashlight_eyelight_glow>(mob);
            glowing.set_light_color(color);
            await glowing.set_light_on(TRUE);
            added_light = TRUE;
        }

        if (SPT_PROB(8, seconds_per_tick))
        {
            if (Dm.truthy(await mob.adjust_tox_loss(3.34f * Dm.num(metabolization_ratio), updating_health: FALSE)))
                return UPDATE_MOB_HEALTH;
        }
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:3539
    [SignalHandler]
    public virtual void on_organ_added(mob_living source = null, obj_item_organ_eyes new_eyes = null)
    {
        if (istype(new_eyes, typeof(obj_item_organ_eyes)) && !Dm.truthy(IS_ROBOTIC_ORGAN(new_eyes)))
            ADD_TRAIT(source, TRAIT_LUMINESCENT_EYES, REF(this));
    }

    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:3545
    [SignalHandler]
    public virtual void on_organ_removed(mob_living source = null, obj_item_organ_eyes old_eyes = null)
    {
        if (istype(old_eyes, typeof(obj_item_organ_eyes)) && !Dm.truthy(IS_ROBOTIC_ORGAN(old_eyes)) && !Dm.truthy(overdosed))
            REMOVE_TRAIT(source, TRAIT_LUMINESCENT_EYES, REF(this));
    }

    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:3551
    public override async DmTask overdose_start(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.overdose_start(affected_mob, metabolization_ratio);
        if (!ishuman(affected_mob))
            return;
        var affected_human = (mob_living_carbon_human)affected_mob;
        var eyes = affected_human.get_organ_slot(ORGAN_SLOT_EYES) as obj_item_organ_eyes;
        if (eyes != null && !Dm.truthy(IS_ROBOTIC_ORGAN(eyes)))
        {
            eyes.eye_color_left = color;
            eyes.eye_color_right = color;
            await affected_human.update_eyes();
        }
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:3562
public partial class datum_reagent_luminescent_fluid_red : datum_reagent_luminescent_fluid
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:3568
    public override async DmTask overdose_start(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.overdose_start(affected_mob, metabolization_ratio);
        if (!ishuman(affected_mob))
            return;
        ADD_TRAIT(affected_mob, TRAIT_UNNATURAL_RED_GLOWY_EYES, OVERDOSE_TRAIT);
    }
}

/// Reagent that polymorphs you
// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:3595
public partial class datum_reagent_polyjuice : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:3607
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;

        if (current_cycle > polymorph_cycle)
            // GAP(M10+): /mob/living/proc/wabbajack (living.dm:1489, the random transformation the wizard's staff shares) is not
            // translated; not-translated.md section 226.
            Dm.Deferred("code/modules/reagents/chemistry/reagents/other_reagents.dm:3611", "polyjuice on_mob_life: affected_mob.wabbajack() (the random transformation, M10+)");
        return dot;
    }
}

/// Turns you into STONE
// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:3614
public partial class datum_reagent_metalgen_gorgium : datum_reagent_metalgen
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:3630
    public override async DmTask expose_mob(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra)
    {
        await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);

        if (Dm.num(reac_volume) < min_volume_to_pretrify)
            return;

        await exposed_mob.Stun(4 * SECONDS);
        await exposed_mob.petrify(Dm.num(reac_volume) * reagent_to_time_conversion);
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:3639
public partial class datum_reagent_cement : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:3650
    public override async DmTask expose_mob(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra)
    {
        await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);
        if (!Dm.truthy(Dm.bitand(methods, INGEST)))
            return;
        await exposed_mob.add_mood_event("cement", typeof(datum_mood_event_cement));
    }

    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:3656
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        float rate = Dm.num(potency) * Dm.num(seconds_per_tick) * Dm.num(metabolization_ratio);
        float need_mob_update = 0;

        if (prob(min(current_cycle / 2, 5)))
            need_mob_update += Dm.num(await mob.adjust_tox_loss(rate));

        if (prob(min(current_cycle / 4, 10)))
            need_mob_update += Dm.num(await mob.adjust_organ_loss(ORGAN_SLOT_BRAIN, rate));

        if (Dm.truthy(need_mob_update))
            return UPDATE_MOB_HEALTH;
        return dot;
    }
}

// Gaps train 12 (D339, D341): the reagents that give VI1's diseases (the transformation toxins, the fungal spores, Agent-S), wired
// at the merge of RG3's file with VI1's diseases.

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:945
public partial class datum_reagent_aslimetoxin : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:954
    public override async DmTask expose_mob(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra)
    {
        methods ??= TOUCH;
        show_message ??= TRUE;
        touch_protection ??= 0;
        await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);
        if (Dm.truthy(Dm.bitand(methods, Dm.bitor(Dm.bitor(PATCH, INGEST), Dm.bitor(INJECT, INHALE)))) || (Dm.truthy(Dm.bitand(methods, Dm.bitor(VAPOR, TOUCH))) && prob(Math.Min(Dm.num(reac_volume), 100) * (1 - Dm.num(touch_protection)))))
            await exposed_mob.ForceContractDisease(Dm.@new<datum_disease_transformation_slime>(), FALSE, TRUE);
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:959
public partial class datum_reagent_gluttonytoxin : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:966
    public override async DmTask expose_mob(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra)
    {
        methods ??= TOUCH;
        show_message ??= TRUE;
        touch_protection ??= 0;
        await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);
        if (Dm.num(reac_volume) <= 1)//This prevents microdosing from infecting masses of people
            return;

        if (Dm.truthy(Dm.bitand(methods, Dm.bitor(Dm.bitor(PATCH, INGEST), Dm.bitor(INJECT, INHALE)))) || (Dm.truthy(Dm.bitand(methods, Dm.bitor(VAPOR, TOUCH))) && prob(Math.Min(Dm.num(reac_volume), 100) * (1 - Dm.num(touch_protection)))))
            await exposed_mob.ForceContractDisease(Dm.@new<datum_disease_transformation_morph>(), FALSE, TRUE);
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1554
public partial class datum_reagent_cyborg_mutation_nanomachines : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1562
    public override async DmTask expose_mob(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra)
    {
        methods ??= TOUCH;
        show_message ??= TRUE;
        touch_protection ??= 0;
        await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);
        obj_item_organ_liver liver = exposed_mob.get_organ_slot(ORGAN_SLOT_LIVER) as obj_item_organ_liver;
        if (liver != null && HAS_TRAIT(liver, TRAIT_HUMAN_AI_METABOLISM))
            return;
        if (Dm.truthy(Dm.bitand(methods, Dm.bitor(Dm.bitor(PATCH, INGEST), Dm.bitor(INJECT, INHALE)))) || (Dm.truthy(Dm.bitand(methods, Dm.bitor(VAPOR, TOUCH))) && prob(Math.Min(Dm.num(reac_volume), 100) * (1 - Dm.num(touch_protection)))))
            await exposed_mob.ForceContractDisease(Dm.@new<datum_disease_transformation_robot>(), FALSE, TRUE);
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1570
public partial class datum_reagent_xenomicrobes : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1578
    public override async DmTask expose_mob(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra)
    {
        methods ??= TOUCH;
        show_message ??= TRUE;
        touch_protection ??= 0;
        await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);
        if (Dm.truthy(Dm.bitand(methods, Dm.bitor(Dm.bitor(PATCH, INGEST), Dm.bitor(INJECT, INHALE)))) || (Dm.truthy(Dm.bitand(methods, Dm.bitor(VAPOR, TOUCH))) && prob(Math.Min(Dm.num(reac_volume), 100) * (1 - Dm.num(touch_protection)))))
            await exposed_mob.ForceContractDisease(Dm.@new<datum_disease_transformation_xeno>(), FALSE, TRUE);
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1583
public partial class datum_reagent_fungalspores : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1591
    public override async DmTask expose_mob(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra)
    {
        methods ??= TOUCH;
        show_message ??= TRUE;
        touch_protection ??= 0;
        await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);
        if (Dm.truthy(Dm.bitand(methods, Dm.bitor(Dm.bitor(PATCH, INGEST), Dm.bitor(INJECT, INHALE)))) || (Dm.truthy(Dm.bitand(methods, Dm.bitor(VAPOR, TOUCH))) && prob(Math.Min(Dm.num(reac_volume), 100) * (1 - Dm.num(touch_protection)))))
            await exposed_mob.ForceContractDisease(Dm.@new<datum_disease_tuberculosis>(), FALSE, TRUE);
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1596
public partial class datum_reagent_snail : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:1604
    public override async DmTask expose_mob(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra)
    {
        methods ??= TOUCH;
        show_message ??= TRUE;
        touch_protection ??= 0;
        await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);
        if (Dm.truthy(Dm.bitand(methods, Dm.bitor(Dm.bitor(PATCH, INGEST), Dm.bitor(INJECT, INHALE)))) || (Dm.truthy(Dm.bitand(methods, Dm.bitor(VAPOR, TOUCH))) && prob(Math.Min(Dm.num(reac_volume), 100) * (1 - Dm.num(touch_protection)))))
            await exposed_mob.ForceContractDisease(Dm.@new<datum_disease_gastrolosis>(), FALSE, TRUE);
    }
}

// tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:2923
public partial class datum_reagent_gondola_mutation_toxin : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:2931
    public override async DmTask expose_mob(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra)
    {
        methods ??= TOUCH;
        show_message ??= TRUE;
        touch_protection ??= 0;
        await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);
        if (Dm.truthy(Dm.bitand(methods, Dm.bitor(Dm.bitor(PATCH, INGEST), Dm.bitor(INJECT, INHALE)))) || (Dm.truthy(Dm.bitand(methods, Dm.bitor(VAPOR, TOUCH))) && prob(Math.Min(Dm.num(reac_volume), 100) * (1 - Dm.num(touch_protection)))))
            await exposed_mob.ForceContractDisease((datum_disease)Dm.@new(gondola_disease), FALSE, TRUE);
    }
}


public sealed partial class TgState
{
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:2812  /datum/reagent/glitter/random's var/static/list/possible_colors
    internal DmList datum_reagent_glitter_random__possible_colors;
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:3243  /datum/reagent/ants's var/static/list/ant_screams
    internal DmList datum_reagent_ants__ant_screams;
    // tg: code/modules/reagents/chemistry/reagents/other_reagents.dm:3299  /datum/reagent/ants/expose_obj's var/static/list/accepted_types
    internal DmList datum_reagent_ants__expose_obj__accepted_types;
}
