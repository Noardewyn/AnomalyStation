// Translated from tgstation (snapshot 1d99bd7f): code/modules/reagents/chemistry/reagents/toxin_reagents.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: the [DmPath] of types that an M2 slot signature or var block names, in the earliest .dme file that declares them
// (rules 2.3; written by the M2 contracts step, docs/ai/specs/m2-ownership.md); M8 slice B (D182): the toxin base's on_mob_life and
// formaldehyde's; the acids (silicons SI1c, D326). The toxins' on_hydroponics_apply (gaps P2). The var blocks are generated (D111).
// Silicons SI2 (D326): Tirizene (staminatoxin), the hacked peacekeeper cyborg's.
// Gaps H3: the fake beer's glass style (it looks like a glass of beer). Gaps SP1: carpotoxin, harmless to a felinid's tongue.
// Gaps RG2 (D339): every other toxin (gibbium's GBS since gaps VI1). Left (not-translated.md section 225): the fish procs
// (used_on_fish, fishing), the spider serum's spider antagonist (D333) and the client's drawing of rotatium's turned plane masters.

// tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:4
[DmPath("/datum/reagent/toxin")]
public partial class datum_reagent_toxin : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:27
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        if (Dm.truthy(toxpwr) && mob.health > health_required)
        {
            if (Dm.truthy(await mob.adjust_tox_loss(METABOLIZE_FREE_CONSTANT(0.5f) * toxpwr * Dm.num(normalise_creation_purity()) * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), updating_health: FALSE, required_biotype: affected_biotype)))
                return UPDATE_MOB_HEALTH;
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:844  (M2 P2: handle_organs tests a dead body for it)
[DmPath("/datum/reagent/toxin/formaldehyde")]
public partial class datum_reagent_toxin_formaldehyde : datum_reagent_toxin
{
    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:859
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        var mob = (mob_living_carbon)affected_mob;
        var liver = mob.get_organ_slot(ORGAN_SLOT_LIVER) as obj_item_organ_liver;
        if (liver != null && HAS_TRAIT(liver, TRAIT_CORONER_METABOLISM)) //mmmm, the forbidden pickle juice
        {
            if (Dm.truthy(await mob.adjust_tox_loss(-1 * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), updating_health: FALSE, required_biotype: affected_biotype))) //it counteracts its own toxin damage.
                return UPDATE_MOB_HEALTH;
            return null;
        }
        else if (SPT_PROB(2.5f, seconds_per_tick))
        {
            await holder.add_reagent(typeof(datum_reagent_toxin_histamine), pick(5, 15));
            await holder.remove_reagent(typeof(datum_reagent_toxin_formaldehyde), 2.4f * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        }
        return await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
    }
}

// tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:953
[DmPath("/datum/reagent/toxin/bad_food")]
public partial class datum_reagent_toxin_bad_food : datum_reagent_toxin
{
}

// Gaps P2 (D298): the reagents' effects in a hydroponics tray (on_hydroponics_apply) and the botany chems' own procs.

// tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:24
public partial class datum_reagent_toxin : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:24
    public override async DmTask on_hydroponics_apply(obj_machinery_hydroponics mytray = null, mob user = null)
    {
        mytray.adjust_toxic(Dm.round(Dm.num(volume) * 2));
    }
}

// tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:76
public partial class datum_reagent_toxin_mutagen : datum_reagent_toxin
{
    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:76
    public override async DmTask on_hydroponics_apply(obj_machinery_hydroponics mytray = null, mob user = null)
    {
        await mytray.radioactive_exposure(modifier: Dm.num(volume) * 0.1f);
        mytray.myseed?.adjust_instability(Dm.round(Dm.num(volume) * 0.2f));
    }
}

// tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:490
public partial class datum_reagent_toxin_plantbgone : datum_reagent_toxin
{
    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:490
    public override async DmTask on_hydroponics_apply(obj_machinery_hydroponics mytray = null, mob user = null)
    {
        mytray.adjust_plant_health(-Dm.round(Dm.num(volume) * 10));
        mytray.adjust_toxic(Dm.round(Dm.num(volume) * 6));
        mytray.adjust_weedlevel(-rand(4, 8));
    }

    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:495
    public override async DmTask<object> expose_obj(obj exposed_obj = null, object reac_volume = null, float? methods = null, float? show_message = null)
    {
        object dot = await base.expose_obj(exposed_obj, reac_volume, methods, show_message);
        if (exposed_obj is obj_structure_alien_weeds) // alien weeds have low hp so just kill
            qdel(exposed_obj);
        if (exposed_obj is obj_structure_alien_resin_flower_bud flower)
        {
            datum_spacevine_mutation_toxicity toxicity = Dm.locate_type(typeof(datum_spacevine_mutation_toxicity), flower.mutations) as datum_spacevine_mutation_toxicity;
            if (toxicity != null)
                return dot;

            float flower_damage = rand(15, 25) * weed_damage_multiplier;
            await flower.take_damage(flower_damage, BRUTE, 0);
        }
        if (exposed_obj is obj_structure_glowshroom) //even a small amount is enough to kill it
            qdel(exposed_obj);
        if (exposed_obj is obj_structure_spacevine vine)
        {
            datum_spacevine_mutation_toxicity toxicity = Dm.locate_type(typeof(datum_spacevine_mutation_toxicity), vine.mutations) as datum_spacevine_mutation_toxicity;
            if (toxicity != null)
                return dot;

            if (prob(spacevine_kill_prob))
                qdel(vine);
        }
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:519
    public override async DmTask expose_mob(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra)
    {
        methods ??= TOUCH;
        await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);
        float damage = Dm.min(Dm.round(0.4f * Dm.num(reac_volume), 0.1f), 10);
        if (Dm.truthy(Dm.bitand(exposed_mob.mob_biotypes, MOB_PLANT)))
        {
            damage = damage * 10 * weed_damage_multiplier;
            if (Dm.truthy(await exposed_mob.adjust_tox_loss(damage, required_biotype: affected_biotype)))
                return;
        }

        if (!Dm.truthy(Dm.bitand(methods, VAPOR)) || !ishuman(exposed_mob))
            return;

        mob_living_carbon_human exposed_human = exposed_mob as mob_living_carbon_human;
        if (exposed_human.wear_mask == null)
            await exposed_human.adjust_tox_loss(damage, required_biotype: affected_biotype);
    }
}

// tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:544
public partial class datum_reagent_toxin_plantbgone_weedkiller : datum_reagent_toxin_plantbgone
{
    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:544
    public override async DmTask on_hydroponics_apply(obj_machinery_hydroponics mytray = null, mob user = null)
    {
        mytray.adjust_toxic(Dm.round(Dm.num(volume) * 0.5f));
        mytray.adjust_weedlevel(-rand(1, 2));
    }
}

// tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:567
public partial class datum_reagent_toxin_pestkiller : datum_reagent_toxin
{
    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:567
    public override async DmTask on_hydroponics_apply(obj_machinery_hydroponics mytray = null, mob user = null)
    {
        mytray.adjust_toxic(Dm.round(Dm.num(volume)));
        mytray.adjust_pestlevel(-rand(1, 2));
    }

    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:558
    public override async DmTask on_new(object data = null)
    {
        await base.on_new(data);
        await AddElement(typeof(datum_element_bugkiller_reagent)); // inert until its package: the reagent's bug-killing exposure
    }

    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:562
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon mob = affected_mob as mob_living_carbon;
        if (Dm.truthy(await mob.adjust_tox_loss(1 * Dm.num(toxpwr) * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), updating_health: FALSE, required_biotype: MOB_BUG)))
            return UPDATE_MOB_HEALTH;
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:580
public partial class datum_reagent_toxin_pestkiller_organic : datum_reagent_toxin_pestkiller
{
    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:580
    public override async DmTask on_hydroponics_apply(obj_machinery_hydroponics mytray = null, mob user = null)
    {
        mytray.adjust_toxic(Dm.round(Dm.num(volume) * 0.1f));
        mytray.adjust_pestlevel(-rand(1, 2));
    }
}

// tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:1282
public partial class datum_reagent_toxin_acid : datum_reagent_toxin
{
    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:1282
    public override async DmTask on_hydroponics_apply(obj_machinery_hydroponics mytray = null, mob user = null)
    {
        mytray.adjust_plant_health(-Dm.round(Dm.num(volume)));
        mytray.adjust_toxic(Dm.round(Dm.num(volume) * 1.5f));
        mytray.adjust_weedlevel(-rand(1, 2));
    }
    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:1287
    public override async DmTask expose_mob(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra)
    {
        methods ??= TOUCH;
        await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);
        mob_living_carbon exposed_carbon = exposed_mob as mob_living_carbon;
        if (!(exposed_carbon is mob_living_carbon))
            return;
        obj_item_organ_liver liver = exposed_carbon.get_organ_slot(ORGAN_SLOT_LIVER) as obj_item_organ_liver;
        if (liver != null && HAS_TRAIT(liver, TRAIT_HUMAN_AI_METABOLISM))
            return;
        reac_volume = Dm.round(Dm.num(reac_volume), 0.1f);
        if (Dm.truthy(Dm.bitand(methods, Dm.bitor(INGEST, INHALE))))
        {
            await exposed_carbon.adjust_brute_loss(min(6 * toxpwr, Dm.num(reac_volume) * toxpwr), required_bodytype: affected_bodytype);
            return;
        }
        if (Dm.truthy(Dm.bitand(methods, INJECT)))
        {
            await exposed_carbon.adjust_brute_loss(1.5f * min(6 * toxpwr, Dm.num(reac_volume) * toxpwr), required_bodytype: affected_bodytype);
            return;
        }
        await exposed_carbon.acid_act(acidpwr, reac_volume);
    }

    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:1303
    public override async DmTask<object> expose_obj(obj exposed_obj = null, object reac_volume = null, float? methods = null, float? show_message = null)
    {
        methods ??= TOUCH;
        show_message ??= TRUE;
        object dot = await base.expose_obj(exposed_obj, reac_volume, methods, show_message);
        if (ismob(exposed_obj.loc)) //handled in human acid_act()
            return dot;
        reac_volume = Dm.round(Dm.num(reac_volume), 0.1f);
        await exposed_obj.acid_act(acidpwr, reac_volume);
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:1310
    public override async DmTask<object> expose_turf(turf exposed_turf = null, object reac_volume = null)
    {
        object dot = await base.expose_turf(exposed_turf, reac_volume);
        if (!(exposed_turf is turf))
            return dot;
        reac_volume = Dm.round(Dm.num(reac_volume), 0.1f);
        await exposed_turf.acid_act(acidpwr, reac_volume);
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:1330
public partial class datum_reagent_toxin_acid_fluacid : datum_reagent_toxin_acid
{
    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:1330
    public override async DmTask on_hydroponics_apply(obj_machinery_hydroponics mytray = null, mob user = null)
    {
        mytray.adjust_plant_health(-Dm.round(Dm.num(volume) * 2));
        mytray.adjust_toxic(Dm.round(Dm.num(volume) * 3));
        mytray.adjust_weedlevel(-rand(1, 4));
    }
    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:1335
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        if (Dm.truthy(await mob.adjust_fire_loss(0.5f * ((Dm.num(current_cycle) - 1) / 15) * Dm.num(metabolization_ratio) * Dm.num(normalise_creation_purity()) * Dm.num(seconds_per_tick), updating_health: FALSE, required_bodytype: affected_bodytype)))
            return UPDATE_MOB_HEALTH;
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:1341
public partial class datum_reagent_toxin_acid_nitracid : datum_reagent_toxin_acid
{
    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:1352
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        if (Dm.truthy(await mob.adjust_fire_loss(0.5f * (Dm.num(volume) / 10) * Dm.num(metabolization_ratio) * Dm.num(normalise_creation_purity()) * Dm.num(seconds_per_tick), updating_health: FALSE, required_bodytype: affected_bodytype))) //here you go nervar
            return UPDATE_MOB_HEALTH;
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:1652
public partial class datum_reagent_toxin_acid_industrial_waste : datum_reagent_toxin_acid
{
    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:1650  #define CRITICAL_CAPACITY (#undef at :1747)
    private const float CRITICAL_CAPACITY = 45;

    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:1663
    public override async DmTask on_new(object data = null)
    {
        await base.on_new(data);
        if (holder.my_atom is obj_machinery_plumbing_disposer)
            RegisterSignal(holder, COMSIG_REAGENTS_HOLDER_UPDATED, nameof(pre_disposal));
    }

    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:1668
    public override async DmTask on_merge(DmList mix_data = null, object amount = null)
    {
        await base.on_merge(mix_data, amount);
        float merged_total = Dm.num(amount) + Dm.num(volume);
        if (merged_total >= CRITICAL_CAPACITY)
        {
            await spew_waste(round(Dm.num(volume) / (WASTE_REACTION_THRESHOLD * 2)), merged_total); //Sure as HELL can't store it.
            atom container = holder.my_atom as atom;
            float damage_mult = 1;
            if (ismachinery(container))
                damage_mult = 2;
            await container.take_damage(round(merged_total * damage_mult / WASTE_REACTION_THRESHOLD), BURN, BIO); //It's an unusual combination of damage type and flags, but we need to intentionally bypass beakers acid immunity.
        }
    }


    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:1680
    public override async DmTask burn(datum_reagents holder = null)
    {
        await base.burn(holder);
        await spew_waste(2); //Can't burn it...
    }

    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:1684
    public override async DmTask<object> on_spark_act(float? power_charge = null, float? spark_flags = null)
    {
        if (Dm.truthy(Dm.bitand(spark_flags, SPARK_ACT_ENCLOSED)) && !ismob(holder.my_atom))
            return null;
        await spew_waste(2); //Can't electrify it...
        return null;
    }

    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:1689
    public override async DmTask<object> expose_obj(obj exposed_obj = null, object reac_volume = null, float? methods = null, float? show_message = null)
    {
        if (Dm.lt(reac_volume, WASTE_REACTION_THRESHOLD))
            return null; // There's too little waste to do anything.
        if (exposed_obj is obj_effect_decal_cleanable_greenglow_waste)
        {
            obj_effect_decal_cleanable_greenglow_waste goo = exposed_obj as obj_effect_decal_cleanable_greenglow_waste;
            goo.visible_message(span_warning(I18n.T("The new waste reactivates []!", goo)));
            goo.pre_dissolve(FALSE);
        }
        return await base.expose_obj(exposed_obj, reac_volume, methods, show_message);
    }

    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:1698
    public override async DmTask<object> expose_turf(turf exposed_turf = null, object reac_volume = null)
    {
        obj_effect_decal_cleanable_greenglow_waste goo = exposed_turf.spawn_unique_cleanable(typeof(obj_effect_decal_cleanable_greenglow_waste)) as obj_effect_decal_cleanable_greenglow_waste; //Following similar logic to how ants spawn their cleanables.
        if (QDELETED(goo))
            return null;

        goo.decal_reagent = type;
        float rounded_volume = Dm.round(Dm.num(reac_volume), 1);
        goo.reagent_amount = rounded_volume;

        if (Dm.truthy(await goo.lazy_init_reagents()))
            ((datum_reagents)goo.reagents).maximum_volume = min(Dm.num(((datum_reagents)goo.reagents).maximum_volume) + rounded_volume, 300);
        if (Dm.truthy(((datum_reagents)goo.reagents).has_reagent(type, WASTE_REACTION_THRESHOLD)))
        {
            goo.pre_dissolve();
            return null; // Otherwise there's too little waste to do anything.
        }
        return await base.expose_turf(exposed_turf, reac_volume);
    }

    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:1714
    public virtual async DmTask pre_disposal()
    {
        atom disaster_zone = holder?.my_atom as atom;
        if (disaster_zone == null)
            return;
        if (prob(10))
            disaster_zone.balloon_alert_to_viewers(I18n.T("hissssssss!"));
        await spew_waste(5); //You can't just dump the industrial waste down the kitchen sink. High range to disincentivize using the chem disposaler.
    }

    /**
     * Pick a random turf in the spew range and split our total amount of waste there.
     */
    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:1726
    public virtual async DmTask spew_waste(object spew_range = null, object total_toxins = null)
    {
        spew_range ??= 1;
        if (!Dm.truthy(spew_range))
            return;
        if (holder.my_atom == null)
            return;
        atom atom_holder = holder.my_atom as atom;
        turf dropturf = get_turf(atom_holder);
        if (dropturf == null)
            return; //Check for at least an inital turf to start
        obj_effect_particle_effect_fluid_smoke_quick greenboy = Dm.@new<obj_effect_particle_effect_fluid_smoke_quick>(dropturf);
        greenboy.color = "#00ff00";
        DmList turfs = Dm.list();
        foreach (turf_open floors in Dm.each<turf_open>(oview(spew_range, dropturf)))
        {
            if (isgroundlessturf(floors))
                continue;
            Dm.append(turfs, floors);
        }

        if (!Dm.truthy(Dm.length(turfs)))
            return;
        dropturf = pick(turfs) as turf;

        await expose_turf(dropturf, Dm.num(volume) / 2);
        volume = Dm.round(Dm.num(volume) / 2, 0.01f);
    }
}

// tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:737 (silicons SI2: the hacked peacekeeper cyborg's hypospray)
public partial class datum_reagent_toxin_staminatoxin : datum_reagent_toxin
{
    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:747
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon mob = affected_mob as mob_living_carbon;
        if (Dm.truthy(await mob.adjust_stamina_loss(0.5f * Dm.num(data) * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), updating_stamina: FALSE)))
            dot = UPDATE_MOB_HEALTH;
        data = max(Dm.num(data) - 1, 3);
        return dot;
    }
}

// Gaps H3 (the bar): the fake beer's glass, which looks like a glass of beer.
// tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:666
public partial class datum_glass_style_drinking_glass_fakebeer : datum_glass_style_drinking_glass
{
    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:669
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        // Copy styles from the beer drinking glass datum
        Type copy_from = typeof(datum_glass_style_drinking_glass_beer);
        name = Dm.initial(copy_from, (datum_glass_style g) => g.name);
        desc = Dm.initial(copy_from, (datum_glass_style g) => g.desc);
        icon = Dm.initial(copy_from, (datum_glass_style g) => g.icon);
        icon_state = Dm.initial(copy_from, (datum_glass_style g) => g.icon_state);
    }
}

// Gaps RG2 (D339): the rest of the toxins. The fish procs (mutagen's and bone hurting juice's used_on_fish) wait for fishing;
// gibbium's GBS came with the diseases (gaps VI1); the spider serum's spider antagonist is a row (D333); fentanyl's mood is live
// since gaps MS1. Rotatium turns the game plane masters, which the client does not draw (a row, section 225).

// tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:44
public partial class datum_reagent_toxin_mutagen : datum_reagent_toxin
{
    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:56
    public override async DmTask expose_mob(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra)
    {
        methods ??= TOUCH;
        show_message ??= TRUE;
        touch_protection ??= 0;
        await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);
        if (!Dm.truthy(exposed_mob.can_mutate()))
            return; //No robots, AIs, aliens, Ians or other mobs should be affected by this.

        if (Dm.truthy(Dm.bitand(methods, Dm.bitor(Dm.bitor(Dm.bitor(PATCH, INGEST), INJECT), INHALE))) || (Dm.truthy(Dm.bitand(methods, Dm.bitor(VAPOR, TOUCH))) && prob(min(Dm.num(reac_volume), 100) * (1 - Dm.num(touch_protection)))))
        {
            mob_living_carbon exposed_carbon = (mob_living_carbon)exposed_mob;
            await exposed_carbon.random_mutate_unique_identity();
            await exposed_carbon.random_mutate_unique_features();
            if (prob(98))
                await exposed_carbon.easy_random_mutate(NEGATIVE + MINOR_NEGATIVE);
            else
                await exposed_carbon.easy_random_mutate(POSITIVE);
            await exposed_carbon.updateappearance();
            await exposed_carbon.domutcheck();
        }
    }

    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:71
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        if (Dm.truthy(await mob.adjust_tox_loss(0.25f * Dm.num(seconds_per_tick) * Dm.num(metabolization_ratio), required_biotype: affected_biotype)))
            return UPDATE_MOB_HEALTH;
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:91
public partial class datum_reagent_toxin_plasma : datum_reagent_toxin
{
    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:84  #define LIQUID_PLASMA_BP (50+T0C) (file-local, #undef at :188)
    private const float LIQUID_PLASMA_BP = 50 + T0C;
    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:85  #define LIQUID_PLASMA_IG (325+T0C) (file-local, #undef at :189)
    private const float LIQUID_PLASMA_IG = 325 + T0C;
    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:86  #define LIQUID_PLASMA_CHARGE_COEFF 2.7 (file-local, #undef at :190)
    private const float LIQUID_PLASMA_CHARGE_COEFF = 2.7f;
    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:87  #define LIQUID_PLASMA_VOLUME_POWER_CAP 7 (file-local, #undef at :191)
    private const int LIQUID_PLASMA_VOLUME_POWER_CAP = 7;

    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:106
    public override async DmTask on_new(object data = null)
    {
        await base.on_new(data);
        RegisterSignal(holder, COMSIG_REAGENTS_TEMP_CHANGE, nameof(on_temp_change));
    }

    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:110
    public override object Destroy(float? force = null)
    {
        UnregisterSignal(holder, COMSIG_REAGENTS_TEMP_CHANGE);
        return base.Destroy(force);
    }

    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:114
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        if (Dm.truthy(holder.has_reagent(typeof(datum_reagent_medicine_epinephrine))))
            await holder.remove_reagent(typeof(datum_reagent_medicine_epinephrine), 1 * k);
        await mob.adjustPlasma(10 * k);
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:120
    public override async DmTask on_mob_metabolize(mob_living affected_mob = null, params object[] __extra)
    {
        await base.on_mob_metabolize(affected_mob, __extra);
        if (HAS_TRAIT(affected_mob, TRAIT_PLASMA_LOVER_METABOLISM)) // sometimes mobs can temporarily metabolize plasma (e.g. plasma fixation disease symptom)
            toxpwr = 0;
    }

    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:125
    public override async DmTask on_mob_end_metabolize(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.on_mob_end_metabolize(affected_mob, metabolization_ratio);
        toxpwr = Dm.num(Dm.initial(this, (datum_reagent_toxin_plasma p) => p.toxpwr));
    }

    /// Handles plasma boiling.
    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:130
    [SignalHandler]
    public virtual void on_temp_change(datum_reagents _holder = null, object old_temp = null)
    {
        if (holder.chem_temp < LIQUID_PLASMA_BP)
            return;
        if (holder.my_atom == null)
            return;
        if (Dm.truthy(Dm.bitand(holder.flags, SEALED_CONTAINER)) && (holder.chem_temp < LIQUID_PLASMA_IG))
            return;
        atom A = holder.my_atom;
        A.atmos_spawn_air(Dm.text("[]=[];[]", GAS_PLASMA, volume, TURF_TEMPERATURE(holder.chem_temp)));
        Waiting.NoSleep(holder.del_reagent(type));
    }

    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:142
    public override async DmTask<object> expose_turf(turf exposed_turf = null, object reac_volume = null)
    {
        if (exposed_turf is not turf_open)
            return null;
        float temp = holder != null ? holder.chem_temp : T20C;
        if (temp >= LIQUID_PLASMA_BP)
            exposed_turf.atmos_spawn_air(Dm.text("[]=[];[]", GAS_PLASMA, reac_volume, TURF_TEMPERATURE(temp)));
        return await base.expose_turf(exposed_turf, reac_volume);
    }

    // Splashing people with plasma is stronger than fuel!
    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:152
    public override async DmTask expose_mob(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra)
    {
        methods ??= TOUCH;
        await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);
        if (Dm.truthy(Dm.bitand(methods, Dm.bitor(TOUCH, VAPOR))))
        {
            await exposed_mob.adjust_fire_stacks(Dm.num(reac_volume) / 5);
            return;
        }
    }

    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:158
    public override async DmTask<object> on_spark_act(float? power_charge = null, float? spark_flags = null)
    {
        // Tape up your plasma IEDs
        if (Dm.truthy(Dm.bitand(spark_flags, SPARK_ACT_WEAKEN_COMMON)) && !Dm.truthy(Dm.bitand(spark_flags, SPARK_ACT_ENCLOSED)))
        {
            if (holder.chem_temp < LIQUID_PLASMA_BP)
                return NONE;
            turf holder_turf = get_turf(holder);
            if (holder_turf != null)
                holder_turf.atmos_spawn_air(Dm.text("[]=[];[]", GAS_PLASMA, volume, TURF_TEMPERATURE(holder.chem_temp)));
            return SPARK_ACT_NON_DESTRUCTIVE;
        }

        // If we have any stabilizing agent in the mix, we need 0.2% of a standard cell value per mol of agent to be spent at once to blow
        // This should allow for some more creative traps to be made with plasma
        float agent_volume = Dm.num(holder.get_reagent_amount(typeof(datum_reagent_stabilizing_agent)));
        if (Dm.truthy(agent_volume) && Dm.num(power_charge) < agent_volume * 0.002f * STANDARD_CELL_CHARGE)
            return NONE;

        // Plasma explosions become stronger with higher current, and don't care about if they're enclosed or not
        float power_modifier = max(0, round(sqrt(Dm.num(power_charge) / STANDARD_CELL_CHARGE) * LIQUID_PLASMA_CHARGE_COEFF, 1) - 1);
        float strengthdiv = 5;
        if (Dm.truthy(Dm.bitand(spark_flags, SPARK_ACT_WEAKEN_COMMON)))
            strengthdiv *= 3; // Stronger than waterpot, weaker than methbombs
        float current_limit = round(Dm.num(volume) / LIQUID_PLASMA_VOLUME_POWER_CAP, 1);
        // High current can only get you so far before you get a sharp dropoff
        if (power_modifier > current_limit)
            power_modifier = round(current_limit + log(power_modifier - current_limit + 1), 1);
        await reagent_explode(holder, volume, modifier: power_modifier, strengthdiv: strengthdiv, clear_holder_reagents: FALSE, flame_factor: 1);
        return Dm.bitor(SPARK_ACT_DESTRUCTIVE, SPARK_ACT_CLEAR_ALL);
    }
}

// tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:192
public partial class datum_reagent_toxin_hot_ice : datum_reagent_toxin
{
    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:202
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        if (Dm.truthy(holder.has_reagent(typeof(datum_reagent_medicine_epinephrine))))
            await holder.remove_reagent(typeof(datum_reagent_medicine_epinephrine), 1 * k);
        await mob.adjustPlasma(10 * k);
        mob.adjust_bodytemperature(-3.5f * TEMPERATURE_DAMAGE_COEFFICIENT * k, Dm.num(mob.get_body_temp_normal()));
        if (ishuman(mob))
        {
            mob_living_carbon_human humi = (mob_living_carbon_human)mob;
            humi.adjust_coretemperature(-3.5f * Dm.num(metabolization_ratio) * TEMPERATURE_DAMAGE_COEFFICIENT * Dm.num(seconds_per_tick), Dm.num(mob.get_body_temp_normal()));
        }
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:212
    public override async DmTask on_mob_metabolize(mob_living affected_mob = null, params object[] __extra)
    {
        await base.on_mob_metabolize(affected_mob, __extra);
        if (HAS_TRAIT(affected_mob, TRAIT_PLASMA_LOVER_METABOLISM))
            toxpwr = 0;
    }

    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:217
    public override async DmTask on_mob_end_metabolize(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.on_mob_end_metabolize(affected_mob, metabolization_ratio);
        toxpwr = Dm.num(Dm.initial(this, (datum_reagent_toxin_hot_ice p) => p.toxpwr));
    }
}

// tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:222
public partial class datum_reagent_toxin_lexorin : datum_reagent_toxin
{
    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:233
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        if (!HAS_TRAIT(mob, TRAIT_NOBREATH))
        {
            float k = Dm.num(normalise_creation_purity()) * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
            await mob.adjust_oxy_loss(2.5f * k, FALSE, required_biotype: affected_biotype);
            mob.losebreath += 1 * k;
            dot = UPDATE_MOB_HEALTH;
            if (SPT_PROB(10, seconds_per_tick))
                await mob.emote("gasp");
        }
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:242
    public override async DmTask on_mob_metabolize(mob_living affected_mob = null, params object[] __extra)
    {
        await base.on_mob_metabolize(affected_mob, __extra);
        RegisterSignal(affected_mob, COMSIG_CARBON_ATTEMPT_BREATHE, nameof(block_breath));
    }

    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:246
    public override async DmTask on_mob_end_metabolize(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.on_mob_end_metabolize(affected_mob, metabolization_ratio);
        UnregisterSignal(affected_mob, COMSIG_CARBON_ATTEMPT_BREATHE); // tg passes PROC_REF(block_breath) as a third argument, which UnregisterSignal does not take
    }

    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:250
    [SignalHandler]
    public virtual object block_breath(mob_living source = null)
    {
        return COMSIG_CARBON_BLOCK_BREATH;
    }
}

// tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:255
public partial class datum_reagent_toxin_carnivorousblood : datum_reagent_toxin
{
    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:267
    public override async DmTask expose_mob(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra)
    {
        await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);
        if (Dm.truthy(Dm.bitand(methods, INHALE)))
            data = Dm.list(); // Being vaporized is generally undesirable for living creatures
    }

    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:272
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        if (!Dm.truthy(CAN_HAVE_BLOOD(mob)) || mob.blood_volume == 0)
        {
            await ((datum_reagents)mob.reagents).remove_reagent(typeof(datum_reagent_toxin_carnivorousblood), volume);
            return dot;
        }
        float multiplier = 1;
        if (mob.stat == DEAD)
            multiplier *= 2;
        if (Dm.@in(mob.dna.unique_enzymes, data))
            multiplier *= 3;
        mob.adjust_blood_volume(-2 * multiplier * Dm.num(seconds_per_tick));
        if (SPT_PROB(10, seconds_per_tick))
        {
            to_chat(mob, span_danger(I18n.T("Your blood is writhing in your veins!")));
            if (SPT_PROB(25, seconds_per_tick))
                await mob.emote("scream");
        }
        return UPDATE_MOB_HEALTH;
    }

    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:289
    public override async DmTask on_merge(DmList mix_data = null, object amount = null)
    {
        await base.on_merge(mix_data, amount);
        feed_dna_list(mix_data);
    }

    /// Given a list of DNA keys, adds new keys up to the limit of three distinct sequences.
    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:294
    public virtual void feed_dna_list(DmList adding_list = null)
    {
        foreach (object dna in Dm.each(adding_list))
        {
            DmList data_list = (DmList)data;
            if (data_list.Count >= 3)
                return;
            if (Dm.@in(dna, data_list))
                continue;
            Dm.append(ref data_list, dna);
            data = data_list;
        }
    }
}

// tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:319
public partial class datum_reagent_toxin_slimejelly : datum_reagent_toxin
{
    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:329
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        if (SPT_PROB(5, seconds_per_tick))
        {
            to_chat(mob, span_danger(I18n.T("Your insides are burning!")));
            if (Dm.truthy(await mob.adjust_tox_loss(rand(20, 60) * Dm.num(metabolization_ratio), updating_health: FALSE, required_biotype: affected_biotype)))
                return UPDATE_MOB_HEALTH;
        }
        else if (SPT_PROB(23, seconds_per_tick))
        {
            if (Dm.truthy(await mob.heal_bodypart_damage(5)))
                return UPDATE_MOB_HEALTH;
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:340 (gaps SP1: the felinid's tongue is immune to it)
public partial class datum_reagent_toxin_carpotoxin : datum_reagent_toxin
{
    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:350
    public override async DmTask on_mob_add(mob_living affected_mob = null, object amount = null)
    {
        await base.on_mob_add(affected_mob, amount);
        if (HAS_TRAIT(affected_mob, TRAIT_CARPOTOXIN_IMMUNE))
            toxpwr = 0;
    }
}

// tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:356
public partial class datum_reagent_toxin_zombiepowder : datum_reagent_toxin
{
    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:369
    public override async DmTask expose_mob(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra)
    {
        await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);
        if (!isliving(exposed_mob) || !Dm.truthy(Dm.bitand(methods, Dm.bitor(INGEST, INHALE))))
            return;

        if (!Dm.truthy(data)) // LAZYINITLIST(data)
            data = Dm.list();
        DmList data_list = (DmList)data;
        data_list["method"] = Dm.bitor(data_list["method"], methods);

        //the stomach handles INGEST via on_mob_metabolize() we only deal with INHALE
        //also means vapour works much faster which is realistic
        if (Dm.truthy(Dm.bitand(methods, INHALE)))
            await zombify(exposed_mob);
    }

    /**
     * Does the fake death & oxy loss on the mob
     *
     * Arguments
     * * mob/living/holder_mob - the mob we are zombifying
    */
    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:387
    public virtual async DmTask zombify(mob_living holder_mob = null)
    {
        await holder_mob.adjust_oxy_loss(0.25f, FALSE, required_biotype: affected_biotype);
        if (Dm.truthy(Dm.bitand((data as DmList)?["method"], Dm.bitor(INGEST, INHALE))) && holder_mob.stat != DEAD)
            await holder_mob.apply_status_effect(typeof(datum_status_effect_reagent_effect_fakedeath), type);
    }

    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:394
    public override async DmTask on_mob_metabolize(mob_living affected_mob = null, params object[] __extra)
    {
        await base.on_mob_metabolize(affected_mob, __extra);
        await zombify(affected_mob);
    }

    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:398
    public override async DmTask on_mob_end_metabolize(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.on_mob_end_metabolize(affected_mob, metabolization_ratio);
        affected_mob.remove_status_effect(typeof(datum_status_effect_reagent_effect_fakedeath));
    }

    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:402
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living)affected_mob;
        if (HAS_TRAIT(mob, TRAIT_FAKEDEATH) && HAS_TRAIT(mob, TRAIT_DEATHCOMA))
            return dot;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        float need_mob_update = 0;
        float sw__1 = current_cycle;
        if (Dm.in_to(sw__1, 2, 6))
        {
            await mob.adjust_confusion(0.5f * SECONDS * k);
            await mob.adjust_drowsiness(1 * SECONDS * k);
            await mob.adjust_slurring(3 * SECONDS * k);
        }
        else if (Dm.in_to(sw__1, 6, 9))
        {
            need_mob_update = Dm.num(await mob.adjust_stamina_loss(20 * k, updating_stamina: FALSE));
        }
        else if (Dm.in_to(sw__1, 10, INFINITY))
        {
            if (mob.stat != DEAD)
                await mob.apply_status_effect(typeof(datum_status_effect_reagent_effect_fakedeath), type);
        }
        if (Dm.truthy(need_mob_update))
            return UPDATE_MOB_HEALTH;
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:421
public partial class datum_reagent_toxin_ghoulpowder : datum_reagent_toxin
{
    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:433  (tg passes FALSE positionally to updating_health and
    // again by name: the named one wins, both FALSE)
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        if (Dm.truthy(await mob.adjust_oxy_loss(0.5f * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), updating_health: FALSE, required_biotype: affected_biotype)))
            return UPDATE_MOB_HEALTH;
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:439
public partial class datum_reagent_toxin_mindbreaker : datum_reagent_toxin
{
    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:453
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        // mindbreaker toxin assuages hallucinations in those plagued with it, mentally
        if (Dm.truthy(mob.has_trauma_type(typeof(datum_brain_trauma_mild_hallucinations))))
            mob.remove_status_effect(typeof(datum_status_effect_hallucination));

        // otherwise it creates hallucinations. truly a miracle medicine.
        else
            await mob.adjust_hallucinations(5 * SECONDS * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:464
public partial class datum_reagent_toxin_mindbreaker_fish : datum_reagent_toxin_mindbreaker
{
    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:467
    public override async DmTask on_new(object data = null)
    {
        await base.on_new(data);
        if (holder?.my_atom != null)
            RegisterSignals(holder.my_atom, Dm.list(COMSIG_ITEM_FRIED, COMSIG_ITEM_BARBEQUE_GRILLED), nameof(on_atom_cooked));
    }

    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:472
    [SignalHandler]
    public virtual void on_atom_cooked(datum source = null, object cooking_time = null)
    {
        Waiting.NoSleep(holder.del_reagent(type));
    }
}

// tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:585
public partial class datum_reagent_toxin_spore : datum_reagent_toxin
{
    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:595
    public override async DmTask expose_mob(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra)
    {
        mob_living spore_lung_victim = exposed_mob;
        await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);

        if (!Dm.truthy(Dm.bitand(methods, INHALE)))
            return;
        if (!Dm.truthy(Dm.bitand(spore_lung_victim.mob_biotypes, Dm.bitor(MOB_HUMANOID, MOB_BEAST))))
            return;

        if (prob(min(Dm.num(reac_volume) * 10, 80)))
        {
            to_chat(spore_lung_victim, span_danger(I18n.T(Dm.str(pick("You have a coughing fit!", "You hack and cough!", "Your lungs burn!")))));
            await spore_lung_victim.Stun(1 * SECONDS);
            await spore_lung_victim.emote("cough");
        }
    }

    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:608
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        mob.damageoverlaytemp = 60;
        mob.update_damage_hud();
        await mob.set_eye_blur_if_lower(3 * SECONDS * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:615
public partial class datum_reagent_toxin_spore_burning : datum_reagent_toxin
{
    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:624
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        await mob.adjust_fire_stacks(1 * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        mob.ignite_mob();
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:630
public partial class datum_reagent_toxin_chloralhydrate : datum_reagent_toxin
{
    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:643
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        float k = Dm.num(normalise_creation_purity()) * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        float sw__1 = current_cycle;
        if (Dm.in_to(sw__1, 2, 11))
        {
            await mob.adjust_confusion(0.67f * SECONDS * k);
            await mob.adjust_drowsiness(1.34f * SECONDS * k);
        }
        else if (Dm.in_to(sw__1, 11, 51))
        {
            await mob.Sleeping(13.34f * k);
        }
        else if (Dm.in_to(sw__1, 52, INFINITY))
        {
            await mob.Sleeping(13.34f * k);
            if (Dm.truthy(await mob.adjust_tox_loss(0.34f * (current_cycle - 51) * k, updating_health: FALSE, required_biotype: affected_biotype)))
                return UPDATE_MOB_HEALTH;
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:657
public partial class datum_reagent_toxin_fakebeer : datum_reagent_toxin
{
    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:678
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        float sw__1 = current_cycle;
        if (Dm.in_to(sw__1, 2, 51))
        {
            await mob.Sleeping(13.34f * k);
        }
        else if (Dm.in_to(sw__1, 52, INFINITY))
        {
            await mob.Sleeping(13.34f * k);
            if (Dm.truthy(await mob.adjust_tox_loss(0.34f * (current_cycle - 50) * k, updating_health: FALSE, required_biotype: affected_biotype)))
                return UPDATE_MOB_HEALTH;
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:720
public partial class datum_reagent_toxin_mutetoxin : datum_reagent_toxin
{
    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:732
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        // Gain approximately 12 seconds * creation purity seconds of silence every metabolism tick.
        await mob.set_silence_if_lower(3 * SECONDS * Dm.num(metabolization_ratio) * Dm.num(normalise_creation_purity()) * Dm.num(seconds_per_tick));
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:754
public partial class datum_reagent_toxin_polonium : datum_reagent_toxin
{
    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:764
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
        else
        {
            if (Dm.truthy(await mob.adjust_tox_loss(4 * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), updating_health: FALSE, required_biotype: affected_biotype)))
                return UPDATE_MOB_HEALTH;
        }
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:774
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
            chance: (min(Dm.num(reac_volume) * rad_power, CALCULATE_RAD_MAX_CHANCE(rad_power))));
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:787
    public override async DmTask expose_mob(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra)
    {
        await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);

        if (!Dm.truthy(SSradiation?.can_irradiate_basic(exposed_mob))) // rules 10.6: a harness world may lack SSradiation
            return;

        if (ishuman(exposed_mob) && Dm.truthy(SSradiation.wearing_rad_protected_clothing((mob_living_carbon_human)exposed_mob)))
            return;

        if (!Dm.truthy(Dm.bitand(methods, Dm.bitor(TOUCH, VAPOR))))
            return;

        radiation_pulse(
            source: exposed_mob,
            max_range: 0,
            threshold: RAD_VERY_LIGHT_INSULATION,
            chance: (min(Dm.num(reac_volume) * rad_power, CALCULATE_RAD_MAX_CHANCE(rad_power))));
    }
}

// tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:807
public partial class datum_reagent_toxin_histamine : datum_reagent_toxin
{
    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:817
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        if (SPT_PROB(30, seconds_per_tick))
        {
            float sw__1 = Dm.num(pick(1, 2, 3, 4));
            if (sw__1 == 1)
            {
                to_chat(mob, span_danger(I18n.T("You can barely see!")));
                await mob.set_eye_blur_if_lower(6 * SECONDS);
            }
            else if (sw__1 == 2)
            {
                await mob.emote("cough");
            }
            else if (sw__1 == 3)
            {
                await mob.emote("sneeze");
            }
            else if (sw__1 == 4)
            {
                if (prob(75))
                {
                    to_chat(mob, span_danger(I18n.T("You scratch at an itch.")));
                    if (Dm.truthy(await mob.adjust_brute_loss(4 * Dm.num(metabolization_ratio), updating_health: FALSE, required_bodytype: affected_bodytype)))
                        return UPDATE_MOB_HEALTH;
                }
            }
        }
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:834
    public override async DmTask<object> overdose_process(mob_living affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.overdose_process(affected_mob, seconds_per_tick, metabolization_ratio);
        float heal = 4 * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        float need_mob_update;
        need_mob_update = Dm.num(await affected_mob.adjust_oxy_loss(heal, updating_health: FALSE, required_biotype: affected_biotype));
        need_mob_update += Dm.num(await affected_mob.adjust_brute_loss(heal, updating_health: FALSE, required_bodytype: affected_bodytype));
        need_mob_update += Dm.num(await affected_mob.adjust_tox_loss(heal, updating_health: FALSE, required_biotype: affected_biotype));
        if (Dm.truthy(need_mob_update))
            return UPDATE_MOB_HEALTH;
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:871
public partial class datum_reagent_toxin_venom : datum_reagent_toxin
{
    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:881
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = null;
        var mob = (mob_living_carbon)affected_mob;
        float newsize = 1.1f * RESIZE_DEFAULT_SIZE;
        await mob.update_transform(newsize / current_size);
        current_size = newsize;
        toxpwr = 0.1f * Dm.num(volume);

        if (Dm.truthy(await mob.adjust_brute_loss(2 * (0.3f * Dm.num(volume)) * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), updating_health: FALSE, required_bodytype: affected_bodytype)))
            dot = UPDATE_MOB_HEALTH;

        // chance to either decay into histamine or go the normal route of toxin metabolization
        if (SPT_PROB(8, seconds_per_tick))
        {
            await holder.add_reagent(typeof(datum_reagent_toxin_histamine), pick(5, 10));
            await holder.remove_reagent(typeof(datum_reagent_toxin_venom), 1.1f);
        }
        else
        {
            object parent = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
            return Dm.truthy(parent) ? parent : dot;
        }
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:897
    public override async DmTask on_mob_end_metabolize(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.on_mob_end_metabolize(affected_mob, metabolization_ratio);
        await affected_mob.update_transform(RESIZE_DEFAULT_SIZE / current_size);
        current_size = RESIZE_DEFAULT_SIZE;
    }
}

// tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:903
public partial class datum_reagent_toxin_fentanyl : datum_reagent_toxin
{
    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:915
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(normalise_creation_purity()) * Dm.num(seconds_per_tick);
        float need_mob_update;
        need_mob_update = Dm.num(await mob.adjust_organ_loss(ORGAN_SLOT_BRAIN, 3 * k, 150));
        if (mob.toxloss <= 60)
            need_mob_update += Dm.num(await mob.adjust_tox_loss(1 * k, updating_health: FALSE, required_biotype: affected_biotype));
        if (current_cycle > 4)
            await mob.add_mood_event("smacked out", typeof(datum_mood_event_narcotic_heavy), name);
        if (current_cycle > 18)
            await mob.Sleeping(40 * k);
        if (Dm.truthy(need_mob_update))
            return UPDATE_MOB_HEALTH;
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:929
public partial class datum_reagent_toxin_cyanide : datum_reagent_toxin
{
    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:940
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        float need_mob_update = FALSE;
        if (SPT_PROB(2.5f, seconds_per_tick))
        {
            mob.losebreath += 1;
            need_mob_update = TRUE;
        }
        if (SPT_PROB(4, seconds_per_tick))
        {
            to_chat(mob, span_danger(I18n.T("You feel horrendously weak!")));
            await mob.Stun(40);
            need_mob_update += Dm.num(await mob.adjust_tox_loss(8 * Dm.num(normalise_creation_purity()) * Dm.num(metabolization_ratio), updating_health: FALSE, required_biotype: affected_biotype));
        }
        if (Dm.truthy(need_mob_update))
            return UPDATE_MOB_HEALTH;
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:964
public partial class datum_reagent_toxin_itching_powder : datum_reagent_toxin
{
    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:977
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        var mob = (mob_living_carbon)affected_mob;
        object scratched = FALSE;
        float scratch_damage = 0.25f * Dm.num(metabolization_ratio);
        obj_item_bodypart head = mob.get_bodypart(BODY_ZONE_HEAD) as obj_item_bodypart;
        if (!(head == null) && SPT_PROB(8, seconds_per_tick))
            scratched = await mob.itch(damage: scratch_damage, target_part: head);

        obj_item_bodypart leg = mob.get_bodypart(Dm.str(pick(BODY_ZONE_L_LEG, BODY_ZONE_R_LEG))) as obj_item_bodypart;
        if (!(leg == null) && SPT_PROB(8, seconds_per_tick))
        {
            object itched = await mob.itch(damage: scratch_damage, target_part: leg, silent: Dm.num(scratched));
            scratched = Dm.truthy(itched) ? itched : scratched;
        }

        obj_item_bodypart arm = mob.get_bodypart(Dm.str(pick(BODY_ZONE_L_ARM, BODY_ZONE_R_ARM))) as obj_item_bodypart;
        if (!(arm == null) && SPT_PROB(8, seconds_per_tick))
        {
            object itched = await mob.itch(damage: scratch_damage, target_part: arm, silent: Dm.num(scratched));
            scratched = Dm.truthy(itched) ? itched : scratched;
        }

        if (SPT_PROB(1.5f, seconds_per_tick))
        {
            await holder.add_reagent(typeof(datum_reagent_toxin_histamine), rand(1, 3));
            await holder.remove_reagent(typeof(datum_reagent_toxin_itching_powder), 1.2f);
            return null;
        }
        else
        {
            object parent = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
            return Dm.truthy(parent) ? parent : null;
        }
    }
}

// tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:1000
public partial class datum_reagent_toxin_initropidril : datum_reagent_toxin
{
    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:1009
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        if (!SPT_PROB(13, seconds_per_tick))
            return dot;
        float picked_option = rand(1, 3);
        float need_mob_update = 0;
        if (picked_option == 1)
        {
            await mob.Paralyze(60);
        }
        else if (picked_option == 2)
        {
            mob.losebreath += 10;
            await mob.adjust_oxy_loss(2 * rand(5, 25) * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), updating_health: FALSE, required_biotype: affected_biotype);
            need_mob_update = TRUE;
        }
        else if (picked_option == 3)
        {
            if (!Dm.truthy(mob.undergoing_cardiac_arrest()) && Dm.truthy(mob.can_heartattack()))
            {
                mob.set_heartattack(TRUE);
                if (!IS_UNCONSCIOUS_OR_CRIT(mob))
                    mob.visible_message(span_userdanger(I18n.T("[] clutches at [] chest as if [] heart stopped!", mob, mob.p_their(), mob.p_their())));
            }
            else
            {
                mob.losebreath += 10;
                need_mob_update = Dm.num(await mob.adjust_oxy_loss(rand(5, 25), updating_health: FALSE, required_biotype: affected_biotype));
            }
        }
        if (Dm.truthy(need_mob_update))
            return UPDATE_MOB_HEALTH;
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:1034
public partial class datum_reagent_toxin_pancuronium : datum_reagent_toxin
{
    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:1044
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        if (current_cycle > 10)
            await mob.Stun(80 * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        if (SPT_PROB(10, seconds_per_tick))
        {
            mob.losebreath += 4;
            return UPDATE_MOB_HEALTH;
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:1053
public partial class datum_reagent_toxin_sodium_thiopental : datum_reagent_toxin
{
    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:1063
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        if (current_cycle > 10)
            await mob.Sleeping(26.67f * k);
        if (Dm.truthy(await mob.adjust_stamina_loss(0.67f * k, updating_stamina: FALSE)))
            return UPDATE_MOB_HEALTH;
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:1071
public partial class datum_reagent_toxin_sulfonal : datum_reagent_toxin
{
    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:1083
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        if (current_cycle > 22)
            await mob.Sleeping(160 * Dm.num(normalise_creation_purity()) * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:1089
public partial class datum_reagent_toxin_amanitin : datum_reagent_toxin
{
    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:1099
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        delayed_toxin_damage += 6 * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:1103
    public override async DmTask on_mob_delete(mob_living affected_mob = null)
    {
        await base.on_mob_delete(affected_mob);
        affected_mob.log_message(Dm.text("has taken [] toxin damage from amanitin toxin", delayed_toxin_damage), LOG_ATTACK);
        await affected_mob.adjust_tox_loss(delayed_toxin_damage, required_biotype: affected_biotype);
    }
}

// tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:1109
public partial class datum_reagent_toxin_lipolicide : datum_reagent_toxin
{
    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:1123
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        if (mob.nutrition <= NUTRITION_LEVEL_STARVING)
        {
            if (Dm.truthy(await mob.adjust_tox_loss(1 * k, updating_health: FALSE, required_biotype: affected_biotype)))
                dot = UPDATE_MOB_HEALTH;
        }
        await mob.adjust_nutrition(-3 * Dm.num(normalise_creation_purity()) * k); // making the chef more valuable, one meme trap at a time
        mob.overeatduration = 0;
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:1132
public partial class datum_reagent_toxin_coniine : datum_reagent_toxin
{
    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:1140
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        if (mob.losebreath < 5)
        {
            mob.losebreath = min(mob.losebreath + 41.67f * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), 5);
            return UPDATE_MOB_HEALTH;
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:1147
public partial class datum_reagent_toxin_spewium : datum_reagent_toxin
{
    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:1156
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        if (current_cycle > 11 && SPT_PROB(min(31, current_cycle), seconds_per_tick))
        {
            // tg's first call is still in vomit's old argument order: vomit_flags 10 (MOB_VOMIT_STUN | MOB_VOMIT_BLOOD), prob(10) as
            // vomit_type (which a bloody vomit never reads), prob(50) as lost_nutrition, rand(0,4) as distance and TRUE as
            // purge_ratio. Kept, with the unread vomit_type left null.
            prob(10);
            await mob.vomit(10, null, prob(50) ? 1 : 0, rand(0, 4), TRUE);
            float constructed_flags = Dm.bitor(MOB_VOMIT_MESSAGE, MOB_VOMIT_HARM);
            if (prob(10))
                constructed_flags = Dm.bitor(constructed_flags, MOB_VOMIT_BLOOD);
            if (prob(50))
                constructed_flags = Dm.bitor(constructed_flags, MOB_VOMIT_STUN);
            await mob.vomit(vomit_flags: constructed_flags, distance: rand(0, 4));
            datum_reagents blood = (datum_reagents)mob.reagents;
            foreach (datum_reagent_toxin reagent in Dm.each<datum_reagent_toxin>(blood.reagent_list))
            {
                if (reagent != this)
                    await blood.remove_reagent(reagent.type, 0.5f * reagent.purge_multiplier * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
            }
        }
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:1170
    public override async DmTask<object> overdose_process(mob_living affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.overdose_process(affected_mob, seconds_per_tick, metabolization_ratio);
        if (current_cycle > 33 && SPT_PROB(7.5f, seconds_per_tick))
        {
            mob_living_carbon carbon = (mob_living_carbon)affected_mob;
            await carbon.spew_organ();
            await carbon.vomit(VOMIT_CATEGORY_BLOOD, lost_nutrition: 0, distance: 4);
            to_chat(affected_mob, span_userdanger(I18n.T("You feel something lumpy come up as you vomit.")));
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:1178
public partial class datum_reagent_toxin_curare : datum_reagent_toxin
{
    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:1186
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        if (current_cycle > 11)
            await mob.Paralyze(240 * k);
        if (Dm.truthy(await mob.adjust_oxy_loss(2 * k, updating_health: FALSE, required_biotype: affected_biotype)))
            return UPDATE_MOB_HEALTH;
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:1194
public partial class datum_reagent_toxin_heparin : datum_reagent_toxin
{
    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:1208
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        if (Dm.truthy(holder.has_reagent(typeof(datum_reagent_medicine_coagulant)))) //Directly purges coagulants from the system. Get rid of the heparin BEFORE attempting to use coagulants.
            await holder.remove_reagent(typeof(datum_reagent_medicine_coagulant), 5 * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        return await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
    }
}

// tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:1214
public partial class datum_reagent_toxin_rotatium : datum_reagent_toxin
{
    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:1227  (the plane masters turn on the server; the client draws
    // no plane master transform yet: a row, section 225)
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        if (mob.hud_used == null || (current_cycle < 20 || (current_cycle % 20) == 0))
            return dot;
        atom_movable_plane_master_controller pm_controller = mob.hud_used.plane_master_controllers[PLANE_MASTERS_GAME] as atom_movable_plane_master_controller;

        float rotation = min(round(current_cycle / 20), 89); // By this point the player is probably puking and quitting anyway
        foreach (atom_movable_screen_plane_master plane in Dm.each_cast<atom_movable_screen_plane_master>(pm_controller.get_planes()))
        {
            Dm.animate(plane, Dm.named("transform", matrix(rotation, MATRIX_ROTATE)), Dm.named("time", 5), Dm.named("easing", QUAD_EASING), Dm.named("loop", -1));
            Dm.animate(Dm.named("transform", matrix(-rotation, MATRIX_ROTATE)), Dm.named("time", 5), Dm.named("easing", QUAD_EASING));
        }
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:1238
    public override async DmTask on_mob_end_metabolize(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.on_mob_end_metabolize(affected_mob, metabolization_ratio);
        if (affected_mob?.hud_used != null)
        {
            atom_movable_plane_master_controller pm_controller = affected_mob.hud_used.plane_master_controllers[PLANE_MASTERS_GAME] as atom_movable_plane_master_controller;
            foreach (atom_movable_screen_plane_master plane in Dm.each_cast<atom_movable_screen_plane_master>(pm_controller.get_planes()))
                Dm.animate(plane, Dm.named("transform", matrix()), Dm.named("time", 5), Dm.named("easing", QUAD_EASING));
        }
    }
}

// tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:1246
public partial class datum_reagent_toxin_anacea : datum_reagent_toxin
{
    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:1257
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        float remove_amt = 5;
        if (Dm.truthy(holder.has_reagent(typeof(datum_reagent_medicine_calomel))) || Dm.truthy(holder.has_reagent(typeof(datum_reagent_medicine_pen_acid))))
            remove_amt = 0.5f;
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        datum_reagents blood = (datum_reagents)mob.reagents;
        foreach (datum_reagent_medicine reagent in Dm.each<datum_reagent_medicine>(blood.reagent_list))
            reagent.volume = Dm.num(reagent.volume) - 6.25f * remove_amt * reagent.purge_multiplier * Dm.num(normalise_creation_purity()) * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        await blood.update_total();
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:1358
public partial class datum_reagent_toxin_delayed : datum_reagent_toxin
{
    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:1365
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        if (current_cycle <= 31)
            return dot;
        if (holder != null)
            await holder.remove_reagent(type, REAGENTS_METABOLISM * mob.metabolism_efficiency * Dm.num(seconds_per_tick));
        if (Dm.truthy(await mob.adjust_tox_loss(0.5f * 5 * Dm.num(seconds_per_tick), updating_health: FALSE, required_biotype: affected_biotype)))
            dot = UPDATE_MOB_HEALTH;
        if (SPT_PROB(5, seconds_per_tick))
            await mob.Paralyze(20);
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:1391
public partial class datum_reagent_toxin_bonehurtingjuice : datum_reagent_toxin
{
    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:1404
    public override async DmTask on_mob_add(mob_living affected_mob = null, object amount = null)
    {
        await base.on_mob_add(affected_mob, amount);
        Waiting.Detach(affected_mob.say("oof ouch my bones", forced: typeof(datum_reagent_toxin_bonehurtingjuice))); // INVOKE_ASYNC
    }

    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:1408
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        if (Dm.truthy(await mob.adjust_stamina_loss(3.25f * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), updating_stamina: FALSE)))
            dot = UPDATE_MOB_HEALTH;
        if (!SPT_PROB(10, seconds_per_tick))
            return dot;
        float sw__1 = rand(1, 3);
        if (sw__1 == 1)
            await mob.say(pick("oof.", "ouch.", "my bones.", "oof ouch.", "oof ouch my bones."), forced: typeof(datum_reagent_toxin_bonehurtingjuice));
        else if (sw__1 == 2)
            mob.manual_emote(pick("oofs silently.", Dm.text("looks like [] bones hurt.", mob.p_their()), Dm.text("grimaces, as though [] bones hurt.", mob.p_their())));
        else if (sw__1 == 3)
            to_chat(mob, span_warning(I18n.T("Your bones hurt!")));
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:1422
    public override async DmTask<object> overdose_process(mob_living affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.overdose_process(affected_mob, seconds_per_tick, metabolization_ratio);
        if (SPT_PROB(2, seconds_per_tick) && iscarbon(affected_mob)) //big oof
        {
            mob_living_carbon carbon = (mob_living_carbon)affected_mob;
            object selected_part = pick(BODY_ZONE_L_ARM, BODY_ZONE_R_ARM, BODY_ZONE_L_LEG, BODY_ZONE_R_LEG); //God help you if the same limb gets picked twice quickly.
            obj_item_bodypart BP = carbon.get_bodypart(Dm.str(selected_part)) as obj_item_bodypart;
            if (BP != null)
            {
                playsound(affected_mob, SFX_DESECRATION, 50, TRUE, -1);
                affected_mob.visible_message(span_warning(I18n.T("[]'s bones hurt too much!!", affected_mob)), span_danger(I18n.T("Your bones hurt too much!!")));
                await affected_mob.say("OOF!!", forced: type);
                await affected_mob.apply_damage(20, BRUTE, BP, wound_bonus: rand(30, 130));
            }
            else //SUCH A LUST FOR REVENGE!!!
            {
                to_chat(affected_mob, span_warning(I18n.T("A phantom limb hurts!")));
                await affected_mob.say("Why are we still here, just to suffer?", forced: type);
            }
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:1443
public partial class datum_reagent_toxin_bungotoxin : datum_reagent_toxin
{
    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:1453
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        if (Dm.truthy(await mob.adjust_organ_loss(ORGAN_SLOT_HEART, 3 * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick))))
            dot = UPDATE_MOB_HEALTH;

        // If our mob's currently dizzy from anything else, we will also gain confusion
        float mob_dizziness = Dm.num(mob.get_timed_status_effect_duration(typeof(datum_status_effect_dizziness)));
        if (mob_dizziness > 0)
        {
            // Gain confusion equal to about half the duration of our current dizziness
            await mob.set_confusion(mob_dizziness / 2);
        }

        if (current_cycle >= 13 && SPT_PROB(4, seconds_per_tick))
        {
            object tox_message = pick("You feel your heart spasm in your chest.", "You feel faint.", "You feel you need to catch your breath.", "You feel a prickle of pain in your chest.");
            to_chat(mob, span_notice(I18n.T(Dm.str(tox_message))));
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:1469
public partial class datum_reagent_toxin_leadacetate : datum_reagent_toxin
{
    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:1478
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        float need_mob_update;
        need_mob_update = Dm.num(await mob.adjust_organ_loss(ORGAN_SLOT_EARS, 0.5f * k));
        need_mob_update += Dm.num(await mob.adjust_organ_loss(ORGAN_SLOT_BRAIN, 0.5f * k));
        if (Dm.truthy(need_mob_update))
            dot = UPDATE_MOB_HEALTH;
        if (SPT_PROB(0.5f, seconds_per_tick))
        {
            to_chat(mob, span_notice(I18n.T("Ah, what was that? You thought you heard something...")));
            await mob.adjust_confusion(5 * SECONDS);
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:1496
public partial class datum_reagent_toxin_viperspider : datum_reagent_toxin
{
    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:1502
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        await mob.adjust_hallucinations(5 * SECONDS * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:1507
public partial class datum_reagent_toxin_tetrodotoxin : datum_reagent_toxin
{
    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:1526
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        float mr = Dm.num(metabolization_ratio);
        float k = mr * Dm.num(seconds_per_tick);
        float need_mob_update = 0;
        if (HAS_TRAIT(mob, TRAIT_TETRODOTOXIN_HEALING))
        {
            toxpwr = 0;
            liver_tolerance_multiplier = 0;
            silent_toxin = TRUE;
            remove_paralysis(mob);
            need_mob_update += Dm.num(await mob.adjust_oxy_loss(-3.5f * k, updating_health: FALSE, required_biotype: affected_biotype));
            need_mob_update = Dm.num(await mob.adjust_tox_loss(-3.25f * k, updating_health: FALSE, required_biotype: affected_biotype));
            need_mob_update += Dm.num(await mob.adjust_brute_loss(-6 * k, updating_health: FALSE, required_bodytype: affected_bodytype));
            need_mob_update += Dm.num(await mob.adjust_fire_loss(-6.75f * k, updating_health: FALSE, required_bodytype: affected_bodytype));
            return Dm.truthy(need_mob_update) ? UPDATE_MOB_HEALTH : dot;
        }

        liver_tolerance_multiplier = Dm.num(Dm.initial(this, (datum_reagent_toxin_tetrodotoxin p) => p.liver_tolerance_multiplier));

        //be ready for a cocktail of symptoms, including:
        //numbness, nausea, vomit, breath loss, weakness, paralysis and nerve damage/impairment and eventually a heart attack if enough time passes.
        float sw__1 = current_cycle;
        if (Dm.in_to(sw__1, 7, 13))
        {
            if (SPT_PROB(20, seconds_per_tick))
                await mob.set_jitter_if_lower(5 * rand(2 * SECONDS, 3 * SECONDS) * mr);
            if (SPT_PROB(5, seconds_per_tick))
            {
                obj_item_organ_tongue tongue = mob.get_organ_slot(ORGAN_SLOT_TONGUE) as obj_item_organ_tongue;
                if (tongue != null)
                    to_chat(mob, span_warning(I18n.T("Your [] feels numb...", tongue.name)));
                await mob.set_slurring_if_lower(25 * SECONDS * mr);
            }
            mob.adjust_disgust(17.5f * k);
        }
        else if (Dm.in_to(sw__1, 13, 21))
        {
            silent_toxin = FALSE;
            toxpwr = 0.5f;
            need_mob_update = Dm.num(await mob.adjust_stamina_loss(12.5f * k, updating_stamina: FALSE));
            if (SPT_PROB(20, seconds_per_tick))
            {
                mob.losebreath += 5 * mr;
                need_mob_update = TRUE;
            }
            if (SPT_PROB(40, seconds_per_tick))
                await mob.set_jitter_if_lower(5 * rand(2 * SECONDS, 3 * SECONDS) * mr);
            mob.adjust_disgust(15 * k);
            await mob.set_slurring_if_lower(5 * SECONDS * k);
            await mob.adjust_stamina_loss(10 * k, updating_stamina: FALSE);
            if (SPT_PROB(4, seconds_per_tick))
            {
                paralyze_limb(mob);
                need_mob_update = TRUE;
            }
            if (SPT_PROB(10, seconds_per_tick))
                await mob.adjust_confusion(rand(6 * SECONDS, 8 * SECONDS));
        }
        else if (Dm.in_to(sw__1, 21, 29))
        {
            toxpwr = 1;
            need_mob_update = Dm.num(await mob.adjust_organ_loss(ORGAN_SLOT_BRAIN, 0.5f));
            need_mob_update = Dm.num(await mob.adjust_organ_loss(ORGAN_SLOT_LUNGS, 0.7f));
            if (SPT_PROB(40, seconds_per_tick))
            {
                mob.losebreath += 10 * mr;
                need_mob_update = TRUE;
            }
            mob.adjust_disgust(15 * k);
            await mob.set_slurring_if_lower(15 * SECONDS * k);
            if (SPT_PROB(5, seconds_per_tick))
                to_chat(mob, span_danger(I18n.T("You feel horribly weak.")));
            need_mob_update += Dm.num(await mob.adjust_stamina_loss(25 * k, updating_stamina: FALSE));
            if (SPT_PROB(8, seconds_per_tick))
            {
                paralyze_limb(mob);
                need_mob_update = TRUE;
            }
            if (SPT_PROB(10, seconds_per_tick))
                await mob.adjust_confusion(rand(6 * SECONDS, 8 * SECONDS));
        }
        else if (Dm.in_to(sw__1, 29, INFINITY))
        {
            toxpwr = 1.5f;
            need_mob_update = Dm.num(await mob.adjust_organ_loss(ORGAN_SLOT_BRAIN, 1, BRAIN_DAMAGE_DEATH));
            need_mob_update = Dm.num(await mob.adjust_organ_loss(ORGAN_SLOT_LUNGS, 1.4f));
            await mob.set_silence_if_lower(15 * SECONDS * k);
            need_mob_update += Dm.num(await mob.adjust_stamina_loss(25 * k, updating_stamina: FALSE));
            mob.adjust_disgust(10 * k);
            if (SPT_PROB(15, seconds_per_tick))
            {
                paralyze_limb(mob);
                need_mob_update = TRUE;
            }
            if (SPT_PROB(20, seconds_per_tick))
                await mob.adjust_confusion(rand(6 * SECONDS, 8 * SECONDS));
        }

        if (current_cycle > 38 && !Dm.truthy(Dm.length(traits_not_applied)) && SPT_PROB(5, seconds_per_tick) && !Dm.truthy(mob.undergoing_cardiac_arrest()))
        {
            mob.set_heartattack(TRUE);
            to_chat(mob, span_bolddanger(I18n.T("You feel a burning pain spread throughout your chest!")));
        }

        if (Dm.truthy(need_mob_update))
            return UPDATE_MOB_HEALTH;
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:1608
    public virtual void paralyze_limb(mob_living affected_mob = null)
    {
        if (!Dm.truthy(Dm.length(traits_not_applied)))
            return;
        object added_trait = pick_n_take(traits_not_applied);
        ADD_TRAIT(affected_mob, added_trait, REF(this));
    }

    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:1614
    public override async DmTask on_mob_add(mob_living affected_mob = null, object amount = null)
    {
        await base.on_mob_add(affected_mob, amount);
        if (HAS_TRAIT(affected_mob, TRAIT_TETRODOTOXIN_HEALING))
            liver_tolerance_multiplier = 0;
    }

    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:1619
    public override async DmTask on_mob_metabolize(mob_living affected_mob = null, params object[] __extra)
    {
        await base.on_mob_metabolize(affected_mob, __extra);
        RegisterSignal(affected_mob, COMSIG_CARBON_ATTEMPT_BREATHE, nameof(block_breath));
    }

    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:1623
    public override async DmTask on_mob_end_metabolize(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.on_mob_end_metabolize(affected_mob, metabolization_ratio);
        UnregisterSignal(affected_mob, COMSIG_CARBON_ATTEMPT_BREATHE); // tg passes PROC_REF(block_breath) as a third argument, which UnregisterSignal does not take
        remove_paralysis(affected_mob);
    }

    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:1628
    public virtual void remove_paralysis(mob_living affected_mob = null)
    {
        // the initial() proc doesn't work for lists.
        DmList initial_list = Dm.list(
            Dm.kv(TRAIT_PARALYSIS_L_ARM, BODY_ZONE_L_ARM),
            Dm.kv(TRAIT_PARALYSIS_R_ARM, BODY_ZONE_R_ARM),
            Dm.kv(TRAIT_PARALYSIS_L_LEG, BODY_ZONE_L_LEG),
            Dm.kv(TRAIT_PARALYSIS_R_LEG, BODY_ZONE_R_LEG));
        affected_mob.remove_traits(initial_list, REF(this));
        traits_not_applied = initial_list;
    }

    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:1639
    [SignalHandler]
    public virtual object block_breath(mob_living source = null)
    {
        if (current_cycle > 28 && !HAS_TRAIT(source, TRAIT_TETRODOTOXIN_HEALING))
            return COMSIG_CARBON_BLOCK_BREATH;
        return null;
    }
}

/// Gibs you (lol), after an easily curable disease because WERE COWARDS
// tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:1754
public partial class datum_reagent_toxin_gibbium : datum_reagent_toxin
{
    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:1766
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);

        if (current_cycle >= gib_cycle)
            await ((mob_living)affected_mob).ForceContractDisease(Dm.@new<datum_disease_gbs_no_transmission>()); // gaps VI1
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:1773
public partial class datum_reagent_toxin_spider_serum : datum_reagent_toxin
{
    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:1783
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;

        if (prob(10))
        {
            await Dm.new_async<mob_living_basic_spider_growing_spiderling>(get_turf(mob));
            await mob.vomit(VOMIT_CATEGORY_BLOOD, lost_nutrition: 20);
            to_chat(mob, span_warning(I18n.T("You feel tiny legs climbing up your throat.")));
        }

        if (current_cycle >= transformation_cycle)
        {
            if (mob.mind != null)
                Dm.Deferred("code/modules/reagents/chemistry/reagents/toxin_reagents.dm:1792", "spider_serum on_mob_life: affected_mob.mind?.add_antag_datum(/datum/antagonist/spider): the spider antagonist (antagonist-only, D333)");
            await mob.change_mob_type(typeof(mob_living_basic_spider_giant), delete_old_mob: TRUE);
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:1796
public partial class datum_reagent_toxin_berserker : datum_reagent_toxin
{
    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:1808
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        float need_mob_update = 0;
        need_mob_update += Dm.num(await mob.adjust_organ_loss(ORGAN_SLOT_BRAIN, 0.75f * k));
        await mob.AdjustAllImmobility(-5 * k);
        if (Dm.truthy(await mob.adjust_stamina_loss(-10 * k, updating_stamina: FALSE)))
            dot = UPDATE_MOB_HEALTH;
        if (Dm.truthy(need_mob_update))
            dot = UPDATE_MOB_HEALTH;
        if (SPT_PROB(5, seconds_per_tick))
            await mob.emote(pick("twitch", "drool"));
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:1820
    public override async DmTask on_mob_metabolize(mob_living affected_mob = null, params object[] __extra)
    {
        await base.on_mob_metabolize(affected_mob, __extra);
        await affected_mob.apply_status_effect(typeof(datum_status_effect_spasms));
        RegisterSignal(affected_mob, COMSIG_LIVING_ENTER_STAMCRIT, nameof(on_stamcrit));
    }

    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:1825
    public override async DmTask on_mob_end_metabolize(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.on_mob_end_metabolize(affected_mob, metabolization_ratio);
        affected_mob.remove_status_effect(typeof(datum_status_effect_spasms));
        UnregisterSignal(affected_mob, COMSIG_LIVING_ENTER_STAMCRIT);
    }

    // tg: code/modules/reagents/chemistry/reagents/toxin_reagents.dm:1830
    [SignalHandler]
    public virtual object on_stamcrit(mob_living affected_mob = null)
    {
        Waiting.NoSleep(affected_mob.set_stamina_loss(90, updating_stamina: TRUE));
        to_chat(affected_mob, span_message(I18n.T("This can't end like this... Blood rushes away from your head...")));
        volume -= volume;
        return STAMCRIT_CANCELLED;
    }
}
