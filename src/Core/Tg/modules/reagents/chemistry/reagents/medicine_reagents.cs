// Translated from tgstation (snapshot 1d99bd7f): code/modules/reagents/chemistry/reagents/medicine_reagents.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. M8 slice B (D182): the medicine base and the medicines of the core set: leporazine, spaceacillin, oxandrolone,
// the saline-glucose solution, omnizine, potassium iodide, pentetic acid, salicylic acid, salbutamol, morphine, oculine,
// inacusiate, atropine, epinephrine, mannitol, sanguirite (coagulant), and the mitogen metabolism factor that probital makes in the
// stomach; M8x slice A: cryoxadone and pyroxadone. Gaps RG2 (D339): every other medicine (strange reagent's revival, the
// stimulants, the trauma and mutation cures, flumpuline's eye stalks, earthsblood's drugginess); their moods since gaps MS1. The
// var blocks are generated (D111).
// Silicons SI2 (D326): the syndicate medical cyborg's restorative nanites.
// Gaps K1 (D327): the changeling adrenaline (Gene Stim) and the changeling haste.
// Gaps DE1 (D333): inaprovaline (Meditopia's banner gives it).

// tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:9
[DmPath("/datum/reagent/medicine")]
public partial class datum_reagent_medicine : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:13
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        // All medicine metabolizes out slower / stay longer if you have a better metabolism
        chemical_flags = Dm.bitor(chemical_flags, REAGENT_REVERSE_METABOLISM);
    }
}

// tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:18
public partial class datum_reagent_medicine_leporazine : datum_reagent_medicine
{
    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:26
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        float k = TEMPERATURE_DAMAGE_COEFFICIENT * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        float target_temp = Dm.num(mob.get_body_temp_normal(apply_change: FALSE));
        if (mob.bodytemperature > target_temp)
            mob.adjust_bodytemperature(-20 * k, target_temp);
        else if (mob.bodytemperature < (target_temp + 1))
            mob.adjust_bodytemperature(20 * k, 0, target_temp);
        if (mob is mob_living_carbon_human affected_human)
        {
            if (affected_human.coretemperature > target_temp)
                affected_human.adjust_coretemperature(-20 * k, target_temp);
            else if (affected_human.coretemperature < (target_temp + 1))
                affected_human.adjust_coretemperature(20 * k, 0, target_temp);
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:143  (M8x slice A; on_hydroponics_apply :177 waits for hydroponics)
public partial class datum_reagent_medicine_cryoxadone : datum_reagent_medicine
{
    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:154
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        metabolization_rate = REAGENTS_METABOLISM * (0.00001f * Dm.pow(mob.bodytemperature, 2) + 0.5f);
        if (mob.bodytemperature >= T0C)
            return dot;
        float power = -0.00003f * Dm.pow(mob.bodytemperature, 2) + 3;
        if (IS_UNCONSCIOUS(mob)) //Significantly more effective when unconscious
            power *= 2;
        float k = power * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        float need_mob_update;
        need_mob_update = Dm.num(await mob.adjust_oxy_loss(-1.5f * k, updating_health: FALSE, required_biotype: affected_biotype));
        need_mob_update += Dm.num(await mob.adjust_brute_loss(-0.5f * k, updating_health: FALSE, required_bodytype: affected_bodytype));
        need_mob_update += Dm.num(await mob.adjust_fire_loss(-0.5f * k, updating_health: FALSE, required_bodytype: affected_bodytype));
        need_mob_update += Dm.num(await mob.adjust_tox_loss(-0.5f * k, updating_health: FALSE, forced: TRUE, required_biotype: affected_biotype)); //heals TOXINLOVERs
        foreach (datum_wound iter_wound in Dm.each<datum_wound>(mob.all_wounds))
            await iter_wound.on_xadone(0.5f * k);
        obj_item_bodypart head = mob.get_bodypart(BODY_ZONE_HEAD) as obj_item_bodypart;
        if (head != null)
            REMOVE_TRAIT(head, TRAIT_DISFIGURED, TRAIT_GENERIC); //fixes common causes for disfiguration
        if (Dm.truthy(need_mob_update))
            return UPDATE_MOB_HEALTH;
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:181  (M8x slice A)
public partial class datum_reagent_medicine_pyroxadone : datum_reagent_medicine
{
    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:190
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        if (mob.bodytemperature <= BODYTEMP_HEAT_DAMAGE_LIMIT)
            return dot;
        float power = 0;
        float sw__1 = mob.bodytemperature;
        if (Dm.in_to(sw__1, BODYTEMP_HEAT_DAMAGE_LIMIT, 400))
            power = 2;
        else if (Dm.in_to(sw__1, 400, 460))
            power = 3;
        else
            power = 5;
        if (Dm.truthy(mob.on_fire))
            power *= 2;

        float k = power * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        float need_mob_update;
        need_mob_update = Dm.num(await mob.adjust_oxy_loss(-1 * k, updating_health: FALSE, required_biotype: affected_biotype));
        need_mob_update += Dm.num(await mob.adjust_brute_loss(-0.5f * k, updating_health: FALSE, required_bodytype: affected_bodytype));
        need_mob_update += Dm.num(await mob.adjust_fire_loss(-0.75f * k, updating_health: FALSE, required_bodytype: affected_bodytype));
        need_mob_update += Dm.num(await mob.adjust_tox_loss(-0.5f * k, updating_health: FALSE, forced: TRUE, required_biotype: affected_biotype));
        if (Dm.truthy(need_mob_update))
            dot = UPDATE_MOB_HEALTH;
        foreach (datum_wound iter_wound in Dm.each<datum_wound>(mob.all_wounds))
            await iter_wound.on_xadone(0.5f * k);
        obj_item_bodypart head = mob.get_bodypart(BODY_ZONE_HEAD) as obj_item_bodypart;
        if (head != null)
            REMOVE_TRAIT(head, TRAIT_DISFIGURED, TRAIT_GENERIC);
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:264  (only vars: its added trait, TRAIT_VIRUS_RESISTANCE)
public partial class datum_reagent_medicine_spaceacillin : datum_reagent_medicine
{
}

// tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:278
public partial class datum_reagent_medicine_oxandrolone : datum_reagent_medicine
{
    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:290
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick) * Dm.num(normalise_creation_purity());
        object need_mob_update;
        if (Dm.num(mob.get_fire_loss()) > 25)
            need_mob_update = await mob.adjust_fire_loss(-4 * k, updating_health: FALSE, required_bodytype: affected_bodytype); //Twice as effective as AIURI for severe burns
        else
            need_mob_update = await mob.adjust_fire_loss(-0.5f * k, updating_health: FALSE, required_bodytype: affected_bodytype); //But only a quarter as effective for more minor ones
        return Dm.truthy(need_mob_update) ? UPDATE_MOB_HEALTH : dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:300
    public override async DmTask<object> overdose_process(mob_living affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.overdose_process(affected_mob, seconds_per_tick, metabolization_ratio);
        if (Dm.truthy(affected_mob.get_fire_loss())) //It only makes existing burns worse
        {
            if (Dm.truthy(await affected_mob.adjust_fire_loss(4.5f * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), updating_health: FALSE, required_bodytype: affected_biotype))) // it's going to be healing either 4 or 0.5
                return UPDATE_MOB_HEALTH;
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:306
public partial class datum_reagent_medicine_salglu_solution : datum_reagent_medicine
{
    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:323
    public override async DmTask on_mob_add(mob_living affected_mob = null, object amount = null)
    {
        await base.on_mob_add(affected_mob, amount);
        if ((affected_mob.get_bloodtype() as datum_blood_type)?.restoration_chem != required_restoration_chem)
            return;

        if (Dm.truthy(affected_mob.has_status_effect(typeof(datum_status_effect_stacking_saline_glucose_dilution))))
            return;

        await affected_mob.apply_status_effect(typeof(datum_status_effect_stacking_saline_glucose_dilution));
    }

    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:333
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        float need_mob_update = FALSE;

        if (SPT_PROB(18, seconds_per_tick))
        {
            need_mob_update = Dm.num(await mob.adjust_brute_loss(-0.5f * Dm.num(metabolization_ratio), updating_health: FALSE, required_bodytype: affected_biotype));
            need_mob_update += Dm.num(await mob.adjust_fire_loss(-0.5f * Dm.num(metabolization_ratio), updating_health: FALSE, required_bodytype: affected_biotype));
        }

        // Regen is handled here, dilution is handled in [living/proc/get_blood_volume]
        var blood_type = mob.get_bloodtype() as datum_blood_type;
        if (blood_type?.restoration_chem == required_restoration_chem)
            mob.adjust_blood_volume(1 * extra_regen * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));

        return Dm.truthy(need_mob_update) ? UPDATE_MOB_HEALTH : dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:349
    public override async DmTask<object> overdose_process(mob_living affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.overdose_process(affected_mob, seconds_per_tick, metabolization_ratio);
        float need_mob_update = 0;
        if (SPT_PROB(1.5f, seconds_per_tick))
        {
            if (holder != null)
            {
                to_chat(affected_mob, span_warning(I18n.T("You feel salty.")));
                await holder.add_reagent(typeof(datum_reagent_consumable_salt), 1);
                await holder.remove_reagent(typeof(datum_reagent_medicine_salglu_solution), 0.5f);
            }
        }
        else if (SPT_PROB(1.5f, seconds_per_tick))
        {
            if (holder != null)
            {
                to_chat(affected_mob, span_warning(I18n.T("You feel sweet.")));
                await holder.add_reagent(typeof(datum_reagent_consumable_sugar), 1);
                await holder.remove_reagent(typeof(datum_reagent_medicine_salglu_solution), 0.5f);
            }
        }
        if (SPT_PROB(18, seconds_per_tick))
        {
            need_mob_update = Dm.num(await affected_mob.adjust_brute_loss(0.5f * Dm.num(metabolization_ratio), updating_health: FALSE, required_bodytype: affected_biotype));
            need_mob_update += Dm.num(await affected_mob.adjust_fire_loss(0.5f * Dm.num(metabolization_ratio), updating_health: FALSE, required_bodytype: affected_biotype));
        }
        return Dm.truthy(need_mob_update) ? UPDATE_MOB_HEALTH : dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:415
public partial class datum_reagent_medicine_omnizine : datum_reagent_medicine
{
    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:426
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        float heal = -2 * healing * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        float need_mob_update;
        need_mob_update = Dm.num(await mob.adjust_tox_loss(heal, updating_health: FALSE, required_biotype: affected_biotype));
        need_mob_update += Dm.num(await mob.adjust_oxy_loss(heal, updating_health: FALSE, required_biotype: affected_biotype));
        need_mob_update += Dm.num(await mob.adjust_brute_loss(heal, updating_health: FALSE, required_bodytype: affected_bodytype));
        need_mob_update += Dm.num(await mob.adjust_fire_loss(heal, updating_health: FALSE, required_bodytype: affected_bodytype));
        return Dm.truthy(need_mob_update) ? UPDATE_MOB_HEALTH : dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:437
    public override async DmTask<object> overdose_process(mob_living affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.overdose_process(affected_mob, seconds_per_tick, metabolization_ratio);
        float hurt = 3 * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        float need_mob_update;
        need_mob_update = Dm.num(await affected_mob.adjust_tox_loss(hurt, updating_health: FALSE, required_biotype: affected_biotype));
        need_mob_update += Dm.num(await affected_mob.adjust_oxy_loss(hurt, updating_health: FALSE, required_biotype: affected_biotype));
        need_mob_update += Dm.num(await affected_mob.adjust_brute_loss(hurt, updating_health: FALSE, required_bodytype: affected_bodytype));
        need_mob_update += Dm.num(await affected_mob.adjust_fire_loss(hurt, updating_health: FALSE, required_bodytype: affected_bodytype));
        return Dm.truthy(need_mob_update) ? UPDATE_MOB_HEALTH : dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:516
public partial class datum_reagent_medicine_potass_iodide : datum_reagent_medicine
{
    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:526
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        if (HAS_TRAIT(mob, TRAIT_IRRADIATED))
        {
            if (Dm.truthy(await mob.adjust_tox_loss(-0.25f * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), updating_health: FALSE, required_biotype: affected_biotype)))
                return UPDATE_MOB_HEALTH;
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:532
public partial class datum_reagent_medicine_pen_acid : datum_reagent_medicine
{
    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:544
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        if (Dm.truthy(await mob.adjust_tox_loss(-2 * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), updating_health: FALSE, required_biotype: affected_biotype)))
            dot = UPDATE_MOB_HEALTH;
        var blood = (datum_reagents)mob.reagents;
        foreach (datum_reagent reagent in Dm.each_cast<datum_reagent>(blood.reagent_list?.Copy()))
        {
            if (reagent != this)
                await blood.remove_reagent(reagent.type, 2 * reagent.purge_multiplier * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:552
public partial class datum_reagent_medicine_sal_acid : datum_reagent_medicine
{
    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:564
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick) * Dm.num(normalise_creation_purity());
        object need_mob_update;
        if (Dm.num(mob.get_brute_loss()) > 25)
            need_mob_update = await mob.adjust_brute_loss(-4 * k, updating_health: FALSE, required_bodytype: affected_bodytype);
        else
            need_mob_update = await mob.adjust_brute_loss(-0.5f * k, updating_health: FALSE, required_bodytype: affected_bodytype);
        return Dm.truthy(need_mob_update) ? UPDATE_MOB_HEALTH : dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:574
    public override async DmTask<object> overdose_process(mob_living affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.overdose_process(affected_mob, seconds_per_tick, metabolization_ratio);
        if (Dm.truthy(affected_mob.get_brute_loss())) //It only makes existing bruises worse
        {
            if (Dm.truthy(await affected_mob.adjust_brute_loss(4.5f * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), updating_health: FALSE, required_bodytype: BODYTYPE_ORGANIC))) // it's going to be healing either 4 or 0.5
                return UPDATE_MOB_HEALTH;
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:580
public partial class datum_reagent_medicine_salbutamol : datum_reagent_medicine
{
    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:591
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        object need_mob_update = await mob.adjust_oxy_loss(-6 * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), updating_health: FALSE, required_biotype: affected_biotype);
        mob.losebreath = max(0, mob.losebreath - (4 * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick)));
        return Dm.truthy(need_mob_update) ? UPDATE_MOB_HEALTH : dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:600  (gaps OD1: the inhalers' albuterol canisters)
public partial class datum_reagent_medicine_albuterol : datum_reagent_medicine
{
    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:619
    public override async DmTask on_mob_metabolize(mob_living affected_mob = null, params object[] __extra)
    {
        await base.on_mob_metabolize(affected_mob, __extra);

        if (!iscarbon(affected_mob))
            return;

        // has additional effects on asthma, but that's handled in the quirk

        RegisterSignal(affected_mob, COMSIG_CARBON_LOSE_ORGAN, nameof(holder_lost_organ));
        RegisterSignal(affected_mob, COMSIG_CARBON_GAIN_ORGAN, nameof(holder_gained_organ));
        mob_living_carbon carbon_mob = (mob_living_carbon)affected_mob;
        obj_item_organ_lungs holder_lungs = carbon_mob.get_organ_slot(ORGAN_SLOT_LUNGS) as obj_item_organ_lungs;
        holder_lungs?.adjust_received_pressure_mult(pressure_mult_increment);
    }

    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:633
    public override async DmTask on_mob_end_metabolize(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.on_mob_end_metabolize(affected_mob, metabolization_ratio);

        if (!iscarbon(affected_mob))
            return;

        UnregisterSignal(affected_mob, Dm.list(COMSIG_CARBON_LOSE_ORGAN, COMSIG_CARBON_GAIN_ORGAN));
        mob_living_carbon carbon_mob = (mob_living_carbon)affected_mob;
        obj_item_organ_lungs holder_lungs = carbon_mob.get_organ_slot(ORGAN_SLOT_LUNGS) as obj_item_organ_lungs;
        holder_lungs?.adjust_received_pressure_mult(-pressure_mult_increment);
    }

    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:644
    public override async DmTask<object> overdose_process(mob_living affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.overdose_process(affected_mob, seconds_per_tick, metabolization_ratio);

        if (!iscarbon(affected_mob))
            return dot;

        mob_living_carbon carbon_mob = (mob_living_carbon)affected_mob;
        if (SPT_PROB(25, seconds_per_tick))
            await carbon_mob.adjust_jitter_up_to(2 * SECONDS, 20 * SECONDS);
        if (SPT_PROB(35, seconds_per_tick))
        {
            if (prob(60))
            {
                carbon_mob.losebreath += 1;
                to_chat(affected_mob, span_danger(I18n.T("Your diaphram spasms and you find yourself unable to breathe!")));
            }
            else
            {
                await carbon_mob.breathe(seconds_per_tick);
                to_chat(affected_mob, span_danger(I18n.T("Your diaphram spasms and you unintentionally take a breath!")));
            }
        }

        if (current_cycle > secondary_overdose_effect_cycle_threshold)
        {
            if (SPT_PROB(30, seconds_per_tick))
                await carbon_mob.adjust_eye_blur_up_to(6 * SECONDS, 30 * SECONDS);
            if (Dm.num(carbon_mob.get_stamina_loss()) < maximum_od_stamina_damage)
                await carbon_mob.adjust_stamina_loss(seconds_per_tick);
        }
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:667
    [SignalHandler]
    public virtual void holder_lost_organ(datum source = null, obj_item_organ lost = null)
    {
        if (lost is obj_item_organ_lungs holder_lungs)
            holder_lungs.adjust_received_pressure_mult(-pressure_mult_increment);
    }

    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:674
    [SignalHandler]
    public virtual void holder_gained_organ(datum source = null, obj_item_organ gained = null)
    {
        if (gained is obj_item_organ_lungs holder_lungs)
            holder_lungs.adjust_received_pressure_mult(pressure_mult_increment);
    }
}

// tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:750
public partial class datum_reagent_medicine_morphine : datum_reagent_medicine
{
    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:763
    public override async DmTask on_mob_metabolize(mob_living affected_mob = null, params object[] __extra)
    {
        await base.on_mob_metabolize(affected_mob, __extra);
        affected_mob.add_movespeed_mod_immunities(type, typeof(datum_movespeed_modifier_damage_slowdown));
    }

    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:767
    public override async DmTask on_mob_end_metabolize(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.on_mob_end_metabolize(affected_mob, metabolization_ratio);
        affected_mob.remove_movespeed_mod_immunities(type, typeof(datum_movespeed_modifier_damage_slowdown));
    }

    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:771
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        float ratio = Dm.num(metabolization_ratio), spt = Dm.num(seconds_per_tick);
        if (current_cycle > 5)
            await mob.add_mood_event("numb", typeof(datum_mood_event_narcotic_medium), name);
        if (mob.disgust < DISGUST_LEVEL_VERYGROSS && SPT_PROB(50 * (2 - creation_purity), seconds_per_tick))
            mob.adjust_disgust(1.5f * ratio);

        // switch(current_cycle): the first matching case runs
        if (current_cycle == 16) //~3u
        {
            to_chat(mob, span_warning(I18n.T("You start to feel tired...")));
            await mob.adjust_eye_blur(2 * SECONDS * ratio * spt);
            if (SPT_PROB(66, seconds_per_tick))
                await mob.emote("yawn");
        }
        else if (current_cycle >= 24 && current_cycle <= 36) // 5u to 7.5u
        {
            if (SPT_PROB(66 * (2 - creation_purity), seconds_per_tick))
                await mob.adjust_drowsiness_up_to(2 * SECONDS * ratio, 12 * SECONDS);
        }
        else if (current_cycle >= 36 && current_cycle <= 48) // 7.5u to 10u
        {
            await mob.adjust_drowsiness_up_to(2 * SECONDS * ratio * spt, 12 * SECONDS);
        }
        else if (current_cycle >= 48) //10u onward
        {
            await mob.adjust_drowsiness_up_to(3 * SECONDS * ratio * spt, 20 * SECONDS);
            // doesn't scale from purity - at this point it tries to guarantee sleep
            if (SPT_PROB(30 * (48 - current_cycle), seconds_per_tick))
                await mob.Sleeping(4 * SECONDS * ratio);
        }
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:798
    public override async DmTask<object> overdose_process(mob_living affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.overdose_process(affected_mob, seconds_per_tick, metabolization_ratio);
        if (SPT_PROB(18, seconds_per_tick))
        {
            await affected_mob.drop_all_held_items();
            await affected_mob.set_dizzy_if_lower(4 * SECONDS);
            await affected_mob.set_jitter_if_lower(4 * SECONDS);
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:806
public partial class datum_reagent_medicine_oculine : datum_reagent_medicine
{
    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:821  (tg's override does not call its parent)
    public override async DmTask on_mob_add(mob_living affected_mob = null, object amount = null)
    {
        if (!iscarbon(affected_mob))
            return;
        RegisterSignal(affected_mob, COMSIG_CARBON_GAIN_ORGAN, nameof(on_gained_organ));
        RegisterSignal(affected_mob, COMSIG_CARBON_LOSE_ORGAN, nameof(on_removed_organ));
        if (affected_mob.get_organ_slot(ORGAN_SLOT_EYES) is not obj_item_organ_eyes eyes)
            return;
        improve_eyesight((mob_living_carbon)affected_mob, eyes);
    }

    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:832
    public virtual void improve_eyesight(mob_living_carbon affected_mob = null, obj_item_organ_eyes eyes = null)
    {
        delta_light = creation_purity * 10;
        eyes.lighting_cutoff = Dm.add(eyes.lighting_cutoff, delta_light);
        affected_mob.update_sight();
    }

    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:837
    public virtual void restore_eyesight(mob_living_carbon affected_mob = null, obj_item_organ_eyes eyes = null)
    {
        eyes.lighting_cutoff = Dm.sub(eyes.lighting_cutoff, delta_light);
        affected_mob.update_sight();
    }

    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:841
    [SignalHandler]
    public virtual void on_gained_organ(mob affected_mob = null, obj_item_organ organ = null)
    {
        if (organ is not obj_item_organ_eyes affected_eyes)
            return;
        improve_eyesight((mob_living_carbon)affected_mob, affected_eyes);
    }

    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:848
    [SignalHandler]
    public virtual void on_removed_organ(mob prev_affected_mob = null, obj_item_organ organ = null)
    {
        if (organ is not obj_item_organ_eyes eyes)
            return;
        restore_eyesight((mob_living_carbon)prev_affected_mob, eyes);
    }

    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:855
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        float normalized_purity = Dm.num(normalise_creation_purity());
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        await mob.adjust_temp_blindness(-2 * SECONDS * k * normalized_purity);
        await mob.adjust_eye_blur(-2 * SECONDS * k * normalized_purity);
        if (mob.get_organ_slot(ORGAN_SLOT_EYES) is obj_item_organ_eyes eyes)
        {
            // Healing eye damage will cure nearsightedness and blindness from ... eye damage
            if (Dm.truthy(await eyes.apply_organ_damage(-1 * k * Dm.num(normalise_creation_purity()), required_organ_flag: affected_organ_flags)))
                dot = UPDATE_MOB_HEALTH;
            // If our eyes are seriously damaged, we have a probability of causing eye blur while healing depending on purity
            if (Dm.num(eyes.damage) >= eyes.low_threshold && Dm.truthy(IS_ORGANIC_ORGAN(eyes)) && SPT_PROB(16 - min(normalized_purity * 6, 12), seconds_per_tick))
            {
                // While healing, gives some eye blur
                if (Dm.truthy(mob.is_blind_from(EYE_DAMAGE)))
                {
                    to_chat(mob, span_warning(I18n.T("Your vision slowly returns...")));
                    await mob.adjust_eye_blur(20 * SECONDS);
                }
                else if (Dm.truthy(mob.is_nearsighted_from(EYE_DAMAGE)))
                {
                    to_chat(mob, span_warning(I18n.T("The blackness in your peripheral vision begins to fade.")));
                    await mob.adjust_eye_blur(5 * SECONDS);
                }
            }
        }
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:875
    public override async DmTask on_mob_delete(mob_living affected_mob = null)
    {
        await base.on_mob_delete(affected_mob);
        if (affected_mob.get_organ_slot(ORGAN_SLOT_EYES) is not obj_item_organ_eyes eyes)
            return;
        restore_eyesight((mob_living_carbon)affected_mob, eyes);
    }

    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:882
    public override async DmTask<object> overdose_process(mob_living affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.overdose_process(affected_mob, seconds_per_tick, metabolization_ratio);
        if (Dm.truthy(await affected_mob.adjust_organ_loss(ORGAN_SLOT_EYES, 0.75f * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), required_organ_flag: affected_organ_flags)))
            dot = UPDATE_MOB_HEALTH;
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:945
public partial class datum_reagent_medicine_inacusiate : datum_reagent_medicine
{
    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:956
    public override async DmTask on_mob_add(mob_living affected_mob = null, object amount = null)
    {
        await base.on_mob_add(affected_mob, amount);
        if (creation_purity >= 1)
        {
            ADD_TRAIT(affected_mob, TRAIT_GOOD_HEARING, type);
            if (!HAS_TRAIT(affected_mob, TRAIT_DEAF))
                to_chat(affected_mob, span_nicegreen(I18n.T("You can feel your hearing drastically improve!")));
        }
    }

    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:963
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        if (mob.get_organ_slot(ORGAN_SLOT_EARS) is not obj_item_organ_ears ears)
            return dot;
        float multiplier = Dm.num(normalise_creation_purity()) * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        await ears.apply_organ_damage(-2 * multiplier);
        ears.adjust_temporary_deafness(-4 * multiplier);
        return UPDATE_MOB_HEALTH;
    }

    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:973
    public override async DmTask on_mob_delete(mob_living affected_mob = null)
    {
        await base.on_mob_delete(affected_mob);
        REMOVE_TRAIT(affected_mob, TRAIT_GOOD_HEARING, type);
        if (!HAS_TRAIT(affected_mob, TRAIT_DEAF))
            to_chat(affected_mob, span_notice(I18n.T("Your hearing returns to its normal acuity.")));
    }
}

// tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:979
public partial class datum_reagent_medicine_atropine : datum_reagent_medicine
{
    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:992
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        if (mob.health <= mob.crit_threshold)
        {
            float need_mob_update;
            need_mob_update = Dm.num(await mob.adjust_tox_loss(-4 * k, updating_health: FALSE, required_biotype: affected_biotype));
            need_mob_update += Dm.num(await mob.adjust_brute_loss(-4 * k, updating_health: FALSE, required_bodytype: affected_bodytype));
            need_mob_update += Dm.num(await mob.adjust_fire_loss(-4 * k, updating_health: FALSE, required_bodytype: affected_bodytype));
            need_mob_update += Dm.num(await mob.adjust_oxy_loss(-10 * k, updating_health: FALSE, required_biotype: affected_biotype));
            if (Dm.truthy(need_mob_update))
                dot = UPDATE_MOB_HEALTH;
        }
        mob.losebreath = 0;
        if (SPT_PROB(10, seconds_per_tick))
        {
            await mob.set_dizzy_if_lower(10 * SECONDS);
            await mob.set_jitter_if_lower(10 * SECONDS);
        }
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1007
    public override async DmTask<object> overdose_process(mob_living affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.overdose_process(affected_mob, seconds_per_tick, metabolization_ratio);
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        if (Dm.truthy(await affected_mob.adjust_tox_loss(1 * k, updating_health: FALSE, required_biotype: affected_biotype)))
            dot = UPDATE_MOB_HEALTH;
        await affected_mob.set_dizzy_if_lower(4 * SECONDS * k);
        await affected_mob.set_jitter_if_lower(4 * SECONDS * k);
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1014
public partial class datum_reagent_medicine_epinephrine : datum_reagent_medicine
{
    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1026
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        if (Dm.truthy(holder.has_reagent(typeof(datum_reagent_toxin_lexorin))))
        {
            if (SPT_PROB(10, seconds_per_tick))
                await holder.add_reagent(typeof(datum_reagent_toxin_histamine), 4);
            return dot;
        }

        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        float need_mob_update = 0;
        if (mob.health <= mob.crit_threshold)
        {
            float heal = -1 * k;
            need_mob_update = Dm.num(await mob.adjust_tox_loss(heal, updating_health: FALSE, required_biotype: affected_biotype));
            need_mob_update += Dm.num(await mob.adjust_brute_loss(heal, updating_health: FALSE, required_bodytype: affected_bodytype));
            need_mob_update += Dm.num(await mob.adjust_fire_loss(heal, updating_health: FALSE, required_bodytype: affected_bodytype));
            need_mob_update += Dm.num(await mob.adjust_oxy_loss(heal, updating_health: FALSE, required_biotype: affected_biotype));
        }
        mob.losebreath = max(0, mob.losebreath - (4 * k));
        need_mob_update += Dm.num(await mob.adjust_stamina_loss(-4 * k, updating_stamina: FALSE));
        if (SPT_PROB(10, seconds_per_tick))
        {
            await mob.AdjustAllImmobility(-20);
            need_mob_update = TRUE;
        }
        return Dm.truthy(need_mob_update) ? UPDATE_MOB_HEALTH : dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1048
    public override async DmTask metabolize_reagent(mob_living_carbon affected_mob = null, object seconds_per_tick = null, object metabolized_volume = null)
    {
        if (Dm.truthy(holder.has_reagent(typeof(datum_reagent_toxin_lexorin))))
        {
            // REM is intentional here
            await holder.remove_reagent(typeof(datum_reagent_toxin_lexorin), 1 * REM() * Dm.num(metabolized_volume) * Dm.num(seconds_per_tick));
            await holder.remove_reagent(typeof(datum_reagent_medicine_epinephrine), 0.5f * REM() * Dm.num(metabolized_volume) * Dm.num(seconds_per_tick));
        }
        await base.metabolize_reagent(affected_mob, seconds_per_tick, metabolized_volume);
    }

    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1055
    public override async DmTask<object> overdose_process(mob_living affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.overdose_process(affected_mob, seconds_per_tick, metabolization_ratio);
        if (SPT_PROB(18, seconds_per_tick))
        {
            float need_mob_update;
            need_mob_update = Dm.num(await affected_mob.adjust_stamina_loss(5 * Dm.num(metabolization_ratio), updating_stamina: FALSE));
            need_mob_update += Dm.num(await affected_mob.adjust_tox_loss(2 * Dm.num(metabolization_ratio), updating_health: FALSE, required_biotype: affected_biotype));
            affected_mob.losebreath++;
            if (Dm.truthy(need_mob_update))
                return UPDATE_MOB_HEALTH;
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1202
public partial class datum_reagent_medicine_mannitol : datum_reagent_medicine
{
    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1216
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        if (Dm.truthy(await mob.adjust_organ_loss(ORGAN_SLOT_BRAIN, -1 * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick) * Dm.num(normalise_creation_purity()), required_organ_flag: affected_organ_flags)))
            return UPDATE_MOB_HEALTH;
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1221
    public override async DmTask overdose_start(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.overdose_start(affected_mob, metabolization_ratio);
        to_chat(affected_mob, span_notice(I18n.T("You suddenly feel <span class='purple'>E N L I G H T E N E D!</span>")));
    }

    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1225
    public override async DmTask<object> overdose_process(mob_living affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.overdose_process(affected_mob, seconds_per_tick, metabolization_ratio);
        if (SPT_PROB(65, seconds_per_tick))
            return dot;
        object tips = null;
        if (SPT_PROB(50, seconds_per_tick))
            tips = world.file2list("strings/tips.txt");
        if (SPT_PROB(50, seconds_per_tick))
            tips = world.file2list("strings/sillytips.txt");
        else
            tips = world.file2list("strings/chemistrytips.txt");
        object message = pick(tips as DmList);
        send_tip_of_the_round(affected_mob, message, source: "Chemical-induced wisdom");
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1774  (probital's ingested remainder, cat2_medicine_reagents.dm:168)
public partial class datum_reagent_medicine_metafactor : datum_reagent_medicine
{
    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1785
    public override async DmTask overdose_start(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.overdose_start(affected_mob, metabolization_ratio);
        metabolization_rate = 2 * REAGENTS_METABOLISM;
    }

    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1789
    public override async DmTask<object> overdose_process(mob_living affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.overdose_process(affected_mob, seconds_per_tick, metabolization_ratio);
        if (SPT_PROB(13, seconds_per_tick))
            await ((mob_living_carbon)affected_mob).vomit(VOMIT_CATEGORY_KNOCKDOWN);
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1865
public partial class datum_reagent_medicine_coagulant : datum_reagent_medicine
{
    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1881
    public override async DmTask on_mob_metabolize(mob_living affected_mob = null, params object[] __extra)
    {
        await base.on_mob_metabolize(affected_mob, __extra);
        MODIFY_PHYSIOLOGY(affected_mob, PHYS_COEFF_BLEED, passive_bleed_modifier);
    }

    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1885
    public override async DmTask on_mob_end_metabolize(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.on_mob_end_metabolize(affected_mob, metabolization_ratio);
        if (Dm.truthy(was_working))
            to_chat(affected_mob, span_warning(I18n.T("The medicine thickening your blood loses its effect!")));
        MODIFY_PHYSIOLOGY(affected_mob, PHYS_COEFF_BLEED, 1 / passive_bleed_modifier);
    }

    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1891
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        if (Dm.truthy(await mob.coagulant_effect(METABOLIZE_FREE_CONSTANT(0.5f) * clot_rate * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick))))
        {
            if (!Dm.truthy(was_working))
            {
                to_chat(mob, span_green(I18n.T("You can feel your flowing blood start thickening!")));
                was_working = TRUE;
            }
        }
        else if (Dm.truthy(was_working))
        {
            was_working = FALSE;
        }
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1901
    public override async DmTask<object> overdose_process(mob_living affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.overdose_process(affected_mob, seconds_per_tick, metabolization_ratio);
        if (!Dm.truthy(CAN_HAVE_BLOOD(affected_mob)))
            return dot;

        if (SPT_PROB(7.5f, seconds_per_tick))
        {
            float ratio = Dm.num(metabolization_ratio);
            affected_mob.losebreath += rand(2, 4);
            await affected_mob.adjust_oxy_loss(METABOLIZE_FREE_CONSTANT(1) * rand(1, 3) * ratio, updating_health: FALSE, required_biotype: affected_biotype);
            if (prob(30))
            {
                to_chat(affected_mob, span_danger(I18n.T("You can feel your blood clotting up in your veins!")));
            }
            else if (prob(10))
            {
                to_chat(affected_mob, span_userdanger(I18n.T("You feel like your blood has stopped moving!")));
                await affected_mob.adjust_oxy_loss(METABOLIZE_FREE_CONSTANT(0.5f) * rand(3, 4) * ratio, updating_health: FALSE, required_biotype: affected_biotype);
            }

            if (prob(50))
            {
                var our_lungs = (obj_item_organ_lungs)affected_mob.get_organ_slot(ORGAN_SLOT_LUNGS);
                await our_lungs.apply_organ_damage(METABOLIZE_FREE_CONSTANT(0.5f) * ratio);
            }
            else
            {
                var our_heart = (obj_item_organ_heart)affected_mob.get_organ_slot(ORGAN_SLOT_HEART);
                await our_heart.apply_organ_damage(METABOLIZE_FREE_CONSTANT(0.5f) * ratio);
            }

            return UPDATE_MOB_HEALTH;
        }
        return dot;
    }
}

// Gaps P2 (D298): the reagents' effects in a hydroponics tray (on_hydroponics_apply).

// tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:51
public partial class datum_reagent_medicine_adminordrazine : datum_reagent_medicine
{
    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:51
    public override async DmTask on_hydroponics_apply(obj_machinery_hydroponics mytray = null, mob user = null)
    {
        mytray.adjust_waterlevel(Dm.round(Dm.num(volume)));
        mytray.adjust_plant_health(Dm.round(Dm.num(volume)));
        mytray.adjust_pestlevel(-rand(1, 5));
        mytray.adjust_weedlevel(-rand(1, 5));
        if (Dm.num(volume) < 3)
            return;
        float roll = rand(100);
        if (roll >= 66 && roll <= 100)
            await mytray.mutatespecie();
        else if (roll >= 33 && roll <= 65)
            await mytray.mutateweed();
        else if (roll >= 1 && roll <= 32)
            mytray.mutatepest();
        else
        {
            if (prob(20))
                mytray.visible_message(span_warning(I18n.T("Nothing happens...")));
        }
    }
}

// tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:177
public partial class datum_reagent_medicine_cryoxadone : datum_reagent_medicine
{
    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:177
    public override async DmTask on_hydroponics_apply(obj_machinery_hydroponics mytray = null, mob user = null)
    {
        mytray.adjust_plant_health(Dm.round(Dm.num(volume) * 3));
        mytray.adjust_toxic(-Dm.round(Dm.num(volume) * 3));
    }
}

// tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1099
public partial class datum_reagent_medicine_strange_reagent : datum_reagent_medicine
{
    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1099
    public override async DmTask on_hydroponics_apply(obj_machinery_hydroponics mytray = null, mob user = null)
    {
        mytray.spawnplant();
    }
}

// tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1458 (silicons SI2: the syndicate medical cyborg's hypospray)
public partial class datum_reagent_medicine_syndicate_nanites : datum_reagent_medicine //Used exclusively by Syndicate medical cyborgs
{
    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1467
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon mob = affected_mob as mob_living_carbon;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        float need_mob_update;
        need_mob_update = Dm.num(await mob.adjust_brute_loss(-2.5f * k, updating_health: FALSE)); //A ton of healing - this is a 50 telecrystal investment.
        need_mob_update += Dm.num(await mob.adjust_fire_loss(-2.5f * k, updating_health: FALSE));
        need_mob_update += Dm.num(await mob.adjust_oxy_loss(-7.5f * k, updating_health: FALSE));
        need_mob_update += Dm.num(await mob.adjust_tox_loss(-2.5f * k, updating_health: FALSE, forced: TRUE, required_biotype: affected_biotype));
        need_mob_update += Dm.num(await mob.adjust_organ_loss(ORGAN_SLOT_BRAIN, -7.5f * k));
        if (Dm.truthy(need_mob_update))
            return UPDATE_MOB_HEALTH;
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1478
    public override async DmTask<object> overdose_process(mob_living affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null) //wtb flavortext messages that hint that you're vomitting up robots
    {
        object dot = await base.overdose_process(affected_mob, seconds_per_tick, metabolization_ratio);
        if (SPT_PROB(13, seconds_per_tick))
        {
            await (affected_mob.reagents as datum_reagents).remove_reagent(GetType(), 5 * Dm.num(metabolization_rate)); // ~5 units at a rate of 0.4 but i wanted a nice number in code
            await (affected_mob as mob_living_carbon).vomit(vomit_flags: VOMIT_CATEGORY_DEFAULT, vomit_type: typeof(obj_effect_decal_cleanable_vomit_nanites), lost_nutrition: 20); // nanite safety protocols make your body expel them to prevent harmies
        }
        return dot;
    }
}

// Gaps K1 (D327): the changeling adrenaline (Gene Stim) and the changeling haste.
// Gaps DE1 (D333): inaprovaline (Meditopia's banner gives it).

//used for changeling's adrenaline power
// tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1579
public partial class datum_reagent_medicine_changelingadrenaline : datum_reagent_medicine
{
    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1586
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon metabolizer = (mob_living_carbon)affected_mob;
        await metabolizer.AdjustAllImmobility(-10 * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        if (Dm.truthy(await metabolizer.adjust_stamina_loss(-15 * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), updating_stamina: FALSE)))
            dot = UPDATE_MOB_HEALTH;
        await metabolizer.set_jitter_if_lower(10 * SECONDS * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        await metabolizer.set_dizzy_if_lower(10 * SECONDS * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1594
    public override async DmTask on_mob_metabolize(mob_living affected_mob = null, params object[] __extra)
    {
        await base.on_mob_metabolize(affected_mob, __extra);
        affected_mob.add_traits(Dm.list(TRAIT_SLEEPIMMUNE, TRAIT_BATON_RESISTANCE), type);
        affected_mob.add_movespeed_mod_immunities(type, typeof(datum_movespeed_modifier_damage_slowdown));
        RegisterSignal(affected_mob, COMSIG_LIVING_ENTER_STAMCRIT, nameof(on_stamcrit));
    }

    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1600
    public override async DmTask on_mob_end_metabolize(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.on_mob_end_metabolize(affected_mob, metabolization_ratio);
        affected_mob.remove_traits(Dm.list(TRAIT_SLEEPIMMUNE, TRAIT_BATON_RESISTANCE), type);
        affected_mob.remove_movespeed_mod_immunities(type, typeof(datum_movespeed_modifier_damage_slowdown));
        affected_mob.remove_status_effect(typeof(datum_status_effect_dizziness));
        affected_mob.remove_status_effect(typeof(datum_status_effect_jitter));
        UnregisterSignal(affected_mob, COMSIG_LIVING_ENTER_STAMCRIT);
    }

    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1608
    [SignalHandler]
    public virtual object on_stamcrit(mob_living affected_mob = null)
    {
        if (affected_mob != null)
            Waiting.NoSleep(affected_mob.set_stamina_loss(90, updating_stamina: TRUE));
        to_chat(affected_mob, span_changeling(I18n.T("Our gene-stim flares! We are invigorated, but its potency wanes.")));
        volume -= (min(Dm.num(volume), 1));
        return STAMCRIT_CANCELLED;
    }

    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1615
    public override async DmTask<object> overdose_process(mob_living affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.overdose_process(affected_mob, seconds_per_tick, metabolization_ratio);
        if (Dm.truthy(await affected_mob.adjust_tox_loss(0.5f * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), updating_health: FALSE)))
            return UPDATE_MOB_HEALTH;
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1620
public partial class datum_reagent_medicine_changelinghaste : datum_reagent_medicine
{
    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1628
    public override async DmTask on_mob_metabolize(mob_living affected_mob = null, params object[] __extra)
    {
        await base.on_mob_metabolize(affected_mob, __extra);
        await affected_mob.add_movespeed_modifier(typeof(datum_movespeed_modifier_reagent_changelinghaste));
    }

    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1632
    public override async DmTask on_mob_end_metabolize(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.on_mob_end_metabolize(affected_mob, metabolization_ratio);
        affected_mob.remove_movespeed_modifier(typeof(datum_movespeed_modifier_reagent_changelinghaste));
    }

    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1636
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon metabolizer = (mob_living_carbon)affected_mob;
        if (Dm.truthy(await metabolizer.adjust_tox_loss(4 * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), updating_health: FALSE)))
            return UPDATE_MOB_HEALTH;
        return dot;
    }
}

//Trek Chems, used primarily by medibots. Only heals a specific damage type, but is very efficient.
// tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1408
public partial class datum_reagent_medicine_inaprovaline : datum_reagent_medicine //is this used anywhere?
{
    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1415
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon mob = (mob_living_carbon)affected_mob;
        if (mob.losebreath >= 5)
        {
            mob.losebreath -= 2.5f * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
            return UPDATE_MOB_HEALTH;
        }
        return dot;
    }
}

// Gaps RG2 (D339): the rest of the file (the mood events since gaps MS1). Earthsblood's drugginess, strange reagent's revival, the
// stimulants, the trauma and mutation cures and the classic medicines.

// tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:41
public partial class datum_reagent_medicine_adminordrazine : datum_reagent_medicine
{
    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:70
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        await mob.heal_bodypart_damage(brute: 2.5f * k, burn: 2.5f * k, updating_health: FALSE, required_bodytype: affected_bodytype);
        await mob.adjust_tox_loss(-2.5f * k, updating_health: FALSE, forced: TRUE, required_biotype: affected_biotype);
        // Heal everything! That we want to. But really don't heal reagents. Otherwise we'll lose ... us.
        await mob.fully_heal(Dm.bitand(full_heal_flags, Dm.bitnot(HEAL_ALL_REAGENTS))); // there is no need to return UPDATE_MOB_HEALTH because this proc calls updatehealth()
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:84
public partial class datum_reagent_medicine_synaptizine : datum_reagent_medicine
{
    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:91
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        await mob.adjust_drowsiness(-5 * SECONDS * k);
        await mob.AdjustAllImmobility(-10 * k);

        if (Dm.truthy(holder.has_reagent(typeof(datum_reagent_toxin_mindbreaker))))
            await holder.remove_reagent(typeof(datum_reagent_toxin_mindbreaker), 2.5f * k);
        await mob.adjust_hallucinations(-10 * SECONDS * k);
        if (SPT_PROB(16, seconds_per_tick))
        {
            if (Dm.truthy(await mob.adjust_tox_loss(0.5f * Dm.num(metabolization_ratio), updating_health: FALSE, required_biotype: affected_biotype)))
                return UPDATE_MOB_HEALTH;
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:104
public partial class datum_reagent_medicine_synaphydramine : datum_reagent_medicine
{
    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:111
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        await mob.adjust_drowsiness(-5 * SECONDS * k);
        if (Dm.truthy(holder.has_reagent(typeof(datum_reagent_toxin_mindbreaker))))
            await holder.remove_reagent(typeof(datum_reagent_toxin_mindbreaker), 2.5f * k);
        if (Dm.truthy(holder.has_reagent(typeof(datum_reagent_toxin_histamine))))
            await holder.remove_reagent(typeof(datum_reagent_toxin_histamine), 2.5f * k);
        await mob.adjust_hallucinations(-10 * SECONDS * k);
        if (SPT_PROB(16, seconds_per_tick))
        {
            if (Dm.truthy(await mob.adjust_tox_loss(0.5f * Dm.num(metabolization_ratio), updating_health: FALSE, required_biotype: affected_biotype)))
                return UPDATE_MOB_HEALTH;
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:124
public partial class datum_reagent_medicine_sansufentanyl : datum_reagent_medicine
{
    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:131
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        await mob.adjust_confusion_up_to(1.5f * SECONDS * k, 5 * SECONDS);
        await mob.adjust_dizzy_up_to(3 * SECONDS * k, 12 * SECONDS);
        if (Dm.truthy(await mob.adjust_stamina_loss(0.5f * k, updating_stamina: FALSE)))
            dot = UPDATE_MOB_HEALTH;

        if (SPT_PROB(10, seconds_per_tick))
        {
            to_chat(mob, I18n.T("You feel confused and disoriented."));
            if (prob(30))
                Dm.output_to(mob, sound(Dm.resource("sound/items/weapons/flash_ring.ogg"))); // SEND_SOUND (DIRECT_OUTPUT, rules 12)
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:220
public partial class datum_reagent_medicine_rezadone : datum_reagent_medicine
{
    // Rezadone is almost never used in favor of cryoxadone. Hopefully this will change that. // No such luck so far // with clone damage gone, someone will find a better use for rezadone... right?
    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:232
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        if (Dm.truthy(await mob.heal_bodypart_damage(
            brute: 0.5f * k,
            burn: 0.5f * k,
            updating_health: FALSE,
            required_bodytype: affected_biotype)))
            dot = UPDATE_MOB_HEALTH;
        obj_item_bodypart head = mob.get_bodypart(BODY_ZONE_HEAD) as obj_item_bodypart;
        if (head != null)
            REMOVE_TRAIT(head, TRAIT_DISFIGURED, TRAIT_GENERIC);
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:245
    public override async DmTask<object> overdose_process(mob_living affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.overdose_process(affected_mob, seconds_per_tick, metabolization_ratio);
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        if (Dm.truthy(await affected_mob.adjust_tox_loss(0.5f * k, updating_health: FALSE, required_biotype: affected_biotype)))
            dot = UPDATE_MOB_HEALTH;
        await affected_mob.set_dizzy_if_lower(5 * SECONDS * k);
        await affected_mob.set_jitter_if_lower(5 * SECONDS * k);
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:252
    public override async DmTask expose_mob(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra)
    {
        methods ??= TOUCH;
        show_message ??= TRUE;
        touch_protection ??= 0;
        await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);
        if (!iscarbon(exposed_mob))
            return;

        float functional_react_volume = Dm.num(reac_volume) * (1 - Dm.num(touch_protection));

        mob_living_carbon patient = (mob_living_carbon)exposed_mob;
        if (functional_react_volume >= 5 && HAS_TRAIT_FROM(patient, TRAIT_HUSK, BURN) && Dm.num(patient.get_fire_loss()) < UNHUSK_DAMAGE_THRESHOLD) //One carp yields 12u rezadone.
        {
            await patient.cure_husk(BURN);
            patient.visible_message(span_nicegreen(I18n.T("[]'s body rapidly absorbs moisture from the environment, taking on a more healthy appearance.", patient)));
        }
    }
}

// tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:369
public partial class datum_reagent_medicine_mine_salve : datum_reagent_medicine
{
    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:378
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        float need_mob_update;
        need_mob_update = Dm.num(await mob.adjust_brute_loss(-0.3125f * k, FALSE, required_bodytype: affected_bodytype));
        need_mob_update += Dm.num(await mob.adjust_fire_loss(-0.3125f * k, FALSE, required_bodytype: affected_bodytype));
        if (Dm.truthy(need_mob_update))
            return UPDATE_MOB_HEALTH;
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:386
    public override async DmTask expose_mob(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra)
    {
        methods ??= TOUCH;
        show_message ??= TRUE;
        touch_protection ??= 0;
        await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);
        if (!iscarbon(exposed_mob) || (exposed_mob.stat == DEAD))
            return;

        if (Dm.truthy(Dm.bitand(methods, Dm.bitor(Dm.bitor(Dm.bitor(INGEST, VAPOR), INJECT), INHALE))))
        {
            float miner_cramps = 5 * (1 - Dm.num(touch_protection));
            if (Dm.truthy(miner_cramps))
            {
                await exposed_mob.adjust_nutrition(-miner_cramps);
                if (Dm.truthy(show_message))
                    to_chat(exposed_mob, span_warning(I18n.T("Your stomach feels empty and cramps!")));
            }
        }

        if (Dm.truthy(Dm.bitand(methods, Dm.bitor(PATCH, TOUCH))))
        {
            exposed_mob.add_surgery_speed_mod(type, 0.9f, min(Dm.num(reac_volume) * 1 * MINUTES, 5 * MINUTES));
            if (Dm.truthy(show_message))
                to_chat(exposed_mob, span_danger(I18n.T("You feel your injuries fade away to nothing!")));
        }
    }

    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:403
    public override async DmTask on_mob_metabolize(mob_living affected_mob = null, params object[] __extra)
    {
        await base.on_mob_metabolize(affected_mob, __extra);
        await affected_mob.apply_status_effect(typeof(datum_status_effect_grouped_screwy_hud_fake_healthy), type);
    }

    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:407
    public override async DmTask on_mob_end_metabolize(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.on_mob_end_metabolize(affected_mob, metabolization_ratio);
        affected_mob.remove_status_effect(typeof(datum_status_effect_grouped_screwy_hud_fake_healthy), type);
    }

    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:411
    public override async DmTask on_burn_wound_processing(datum_wound_burn_flesh burn_wound = null)
    {
        burn_wound.sanitization += 0.3f;
        burn_wound.flesh_healing += 0.5f;
    }
}

// tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:457
public partial class datum_reagent_medicine_calomel : datum_reagent_medicine
{
    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:467
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        datum_reagents blood = (datum_reagents)mob.reagents;
        foreach (datum_reagent target_reagent in Dm.each_cast<datum_reagent>(blood.reagent_list))
        {
            if (target_reagent is datum_reagent_medicine_calomel)
                continue;
            await blood.remove_reagent(target_reagent.type, 1.5f * target_reagent.purge_multiplier * k);
        }
        float toxin_amount = round(mob.health / 40, 0.1f);
        if (Dm.truthy(await mob.adjust_tox_loss(0.5f * toxin_amount * k, updating_health: FALSE, required_biotype: affected_biotype)))
            return UPDATE_MOB_HEALTH;
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:477
    public override async DmTask<object> overdose_process(mob_living affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.overdose_process(affected_mob, seconds_per_tick, metabolization_ratio);
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        await ((datum_reagents)affected_mob.reagents).remove_reagent(type, 1 * k);
        if (Dm.truthy(await affected_mob.adjust_tox_loss(1.25f * k, updating_health: FALSE, required_biotype: affected_biotype)))
            return UPDATE_MOB_HEALTH;
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:484
public partial class datum_reagent_medicine_ammoniated_mercury : datum_reagent_medicine
{
    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:498
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        datum_reagents blood = (datum_reagents)mob.reagents;
        float toxin_chem_amount = 0;
        foreach (datum_reagent_toxin target_reagent in Dm.each<datum_reagent_toxin>(blood.reagent_list))
        {
            toxin_chem_amount += 1;
            await blood.remove_reagent(target_reagent.type, 25 * target_reagent.purge_multiplier * k);
        }
        float toxin_amount = round(Dm.num(mob.get_brute_loss()) / 15, 0.1f) + round(Dm.num(mob.get_fire_loss()) / 30, 0.1f) - 3;
        if (Dm.truthy(await mob.adjust_tox_loss(5 * toxin_amount * k, updating_health: FALSE, required_biotype: affected_biotype)))
            dot = UPDATE_MOB_HEALTH;
        if (toxin_chem_amount == 0)
        {
            foreach (datum_reagent_medicine_ammoniated_mercury target_reagent in Dm.each<datum_reagent_medicine_ammoniated_mercury>(blood.reagent_list))
                await blood.remove_reagent(target_reagent.type, 5 * k);
        }
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:511
    public override async DmTask<object> overdose_process(mob_living affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.overdose_process(affected_mob, seconds_per_tick, metabolization_ratio);
        if (Dm.truthy(await affected_mob.adjust_tox_loss(15 * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), updating_health: FALSE, required_biotype: affected_biotype)))
            return UPDATE_MOB_HEALTH;
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:682
public partial class datum_reagent_medicine_ephedrine : datum_reagent_medicine
{
    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:696
    public override async DmTask on_mob_metabolize(mob_living affected_mob = null, params object[] __extra)
    {
        await base.on_mob_metabolize(affected_mob, __extra);
        await affected_mob.add_movespeed_modifier(typeof(datum_movespeed_modifier_reagent_ephedrine));
        float purity_movespeed_accounting = -0.375f * Dm.num(normalise_creation_purity());
        await affected_mob.add_or_update_variable_movespeed_modifier(typeof(datum_movespeed_modifier_reagent_ephedrine), TRUE, purity_movespeed_accounting);
    }

    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:702
    public override async DmTask on_mob_end_metabolize(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.on_mob_end_metabolize(affected_mob, metabolization_ratio);
        affected_mob.remove_movespeed_modifier(typeof(datum_movespeed_modifier_reagent_ephedrine));
    }

    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:706
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        obj_item active_held_item = mob.get_active_held_item() as obj_item;
        if (SPT_PROB(10 * (1.5f - creation_purity), seconds_per_tick) && iscarbon(mob) && Dm.num(active_held_item?.w_class) > WEIGHT_CLASS_SMALL)
        {
            if (active_held_item != null && Dm.truthy(await mob.dropItemToGround(active_held_item)))
            {
                to_chat(mob, span_notice(I18n.T("Your hands spaz out and you drop what you were holding!")));
                await mob.set_jitter_if_lower(20 * SECONDS);
            }
        }

        await mob.AdjustAllImmobility(-20 * k * Dm.num(normalise_creation_purity()));
        await mob.adjust_stamina_loss(-4 * k * Dm.num(normalise_creation_purity()), updating_stamina: FALSE);

        return UPDATE_MOB_HEALTH;
    }

    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:719
    public override async DmTask<object> overdose_process(mob_living affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.overdose_process(affected_mob, seconds_per_tick, metabolization_ratio);
        if (SPT_PROB(1 * (1 + (1 - Dm.num(normalise_creation_purity()))), seconds_per_tick) && iscarbon(affected_mob))
        {
            await affected_mob.apply_status_effect(typeof(datum_status_effect_heart_attack));
            to_chat(affected_mob, span_userdanger(I18n.T("You're pretty sure you just felt your heart stop for a second there..")));
            affected_mob.playsound_local(affected_mob, Dm.resource("sound/effects/singlebeat.ogg"), 100, 0);
        }

        if (SPT_PROB(3.5f * (1 + (1 - Dm.num(normalise_creation_purity()))), seconds_per_tick))
            to_chat(affected_mob, span_notice(I18n.T(Dm.str(pick("Your head pounds.", "You feel a tight pain in your chest.", "You find it hard to stay still.", "You feel your heart practically beating out of your chest.")))));

        if (SPT_PROB(18 * (1 + (1 - Dm.num(normalise_creation_purity()))), seconds_per_tick))
        {
            await affected_mob.adjust_tox_loss(1 * Dm.num(metabolization_ratio), updating_health: FALSE, required_biotype: affected_biotype);
            affected_mob.losebreath++;
            return UPDATE_MOB_HEALTH;
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:735
public partial class datum_reagent_medicine_diphenhydramine : datum_reagent_medicine
{
    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:743
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        if (SPT_PROB(5, seconds_per_tick))
            await mob.adjust_drowsiness(2 * SECONDS);
        await mob.adjust_jitter(-2 * SECONDS * k);
        await holder.remove_reagent(typeof(datum_reagent_toxin_histamine), 2 * k);
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:888
public partial class datum_reagent_medicine_oculine_flumpuline : datum_reagent_medicine_oculine
{
    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:900  var/static/list/eye_types (rules 10.6: a TgState field, below)
    public static ref DmList eye_types => ref TgState.Current.datum_reagent_medicine_oculine_flumpuline__eye_types;

    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:902
    public override void improve_eyesight(mob_living_carbon affected_mob = null, obj_item_organ_eyes eyes = null)
    {
        delta_light = 200; //2x better than pure oculine
        eyes.lighting_cutoff = Dm.add(eyes.lighting_cutoff, delta_light);
        affected_mob.update_sight();
    }

    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:907
    public override void restore_eyesight(mob_living_carbon affected_mob = null, obj_item_organ_eyes eyes = null)
    {
        eyes.lighting_cutoff = Dm.sub(eyes.lighting_cutoff, delta_light);
        affected_mob.update_sight();
    }

    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:911
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        obj_item_organ_eyes eyes = mob.get_organ_slot(ORGAN_SLOT_EYES) as obj_item_organ_eyes;
        // if no eyes or inorganic do nothing. we let already changed eyes go because funny
        if (eyes == null || !Dm.truthy(IS_ORGANIC_ORGAN(eyes)))
            return dot;

        if (!prob(2))
            return dot;

        await flump_eyes(mob, eyes);
        return dot;
    }

    // Overdose causes constant eye popping
    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:924
    public override async DmTask<object> overdose_process(mob_living affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.overdose_process(affected_mob, seconds_per_tick, metabolization_ratio);
        if (!prob(25))
            return dot;
        obj_item_organ_eyes eyes = affected_mob.get_organ_slot(ORGAN_SLOT_EYES) as obj_item_organ_eyes;

        await flump_eyes(affected_mob, eyes);
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:932
    public virtual async DmTask flump_eyes(mob affected_mob = null, obj eyes = null)
    {
        Type new_eyes_type = pick(eye_types) as Type;
        obj_item_organ_eyes new_eyes = await Dm.new_async(new_eyes_type, affected_mob) as obj_item_organ_eyes;
        await new_eyes.Insert(affected_mob as mob_living_carbon);
        playsound(affected_mob, Dm.resource("sound/effects/cartoon_sfx/cartoon_pop.ogg"), 50, TRUE);
        affected_mob.visible_message(span_danger(I18n.T("[]'s [] suddenly sprout stalks and turn into []!", affected_mob, eyes != null ? eyes : I18n.T("eye holes"), new_eyes)));
        spawn(-1, async () =>
        {
            await affected_mob.emote("scream");
            await sleep(5 * SECONDS);
            if (!QDELETED(eyes))
            {
                eyes.visible_message(span_danger(I18n.T("[] rapidly turn to dust.", eyes)));
                await eyes.dust();
            }
        });
    }
}

public sealed partial class TgState
{
    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:900  /datum/reagent/medicine/oculine/flumpuline/var/static/list/eye_types
    internal DmList datum_reagent_medicine_oculine_flumpuline__eye_types = Dm.list(typeof(obj_item_organ_eyes_snail), typeof(obj_item_organ_eyes_night_vision_mushroom));
}

// tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1066
public partial class datum_reagent_medicine_strange_reagent : datum_reagent_medicine
{
    // To override for subtypes.
    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1084
    public virtual object pre_rez_check(atom thing_to_rez = null)
    {
        return TRUE;
    }

    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1092
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        description = replacetext(description, "%MAXHEALTHRATIO%", Dm.text("[]%", max_revive_damage_ratio * 100)) as string;
        if (Dm.truthy(instant))
            description += " It appears to be pulsing with a warm pink light.";
    }

    /// Calculates the amount of reagent to at a bare minimum make the target not dead
    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1103
    public virtual object calculate_amount_needed_to_revive(mob_living benefactor = null)
    {
        float their_health = Dm.num(benefactor.getMaxHealth()) - (Dm.num(benefactor.get_brute_loss()) + Dm.num(benefactor.get_fire_loss()));
        if (their_health > 0)
            return 1;

        return round(-their_health / healing_per_reagent_unit, DAMAGE_PRECISION);
    }

    /// Calculates the amount of reagent that will be needed to both revive and full heal the target. Looks at healing_per_reagent_unit and excess_healing_ratio
    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1111
    public virtual object calculate_amount_needed_to_full_heal(mob_living benefactor = null)
    {
        float their_health = Dm.num(benefactor.get_brute_loss()) + Dm.num(benefactor.get_fire_loss());
        float max_health = Dm.num(benefactor.getMaxHealth());
        if (their_health >= max_health)
            return 1;

        float amount_needed_to_revive = Dm.num(calculate_amount_needed_to_revive(benefactor));
        float expected_amount_to_full_heal = round(max_health / healing_per_reagent_unit, DAMAGE_PRECISION) / excess_healing_ratio;
        return amount_needed_to_revive + expected_amount_to_full_heal;
    }

    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1121
    public override async DmTask expose_mob(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra)
    {
        methods ??= TOUCH;
        if (exposed_mob.stat != DEAD || !Dm.truthy(Dm.bitand(exposed_mob.mob_biotypes, MOB_ORGANIC)))
        {
            await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);
            return;
        }

        if (HAS_TRAIT(exposed_mob, TRAIT_SUICIDED)) //they are never coming back
        {
            exposed_mob.visible_message(span_warning(I18n.T("[]'s body does not react...", exposed_mob)));
            return;
        }

        if (iscarbon(exposed_mob) && !Dm.truthy(Dm.bitand(methods, Dm.bitor(INGEST, INHALE)))) //simplemobs can still be splashed
        {
            await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);
            return;
        }

        if (HAS_TRAIT(exposed_mob, TRAIT_HUSK))
        {
            exposed_mob.visible_message(span_warning(I18n.T("[]'s body lets off a puff of smoke...", exposed_mob)));
            return;
        }

        if ((Dm.num(exposed_mob.get_brute_loss()) + Dm.num(exposed_mob.get_fire_loss())) > (Dm.num(exposed_mob.getMaxHealth()) * max_revive_damage_ratio))
        {
            exposed_mob.visible_message(span_warning(I18n.T("[]'s body convulses violently, before falling still...", exposed_mob)));
            return;
        }

        float needed_to_revive = Dm.num(calculate_amount_needed_to_revive(exposed_mob));
        if (Dm.num(reac_volume) < needed_to_revive)
        {
            exposed_mob.visible_message(span_warning(I18n.T("[]'s body convulses a bit, and then falls still once more.", exposed_mob)));
            exposed_mob.do_jitter_animation(10);
            return;
        }

        if (!Dm.truthy(pre_rez_check(exposed_mob)))
        {
            exposed_mob.visible_message(span_warning(I18n.T("[]'s body twitches slightly.", exposed_mob)));
            exposed_mob.do_jitter_animation(1);
            return;
        }

        exposed_mob.visible_message(span_warning(I18n.T("[]'s body starts convulsing!", exposed_mob)));
        exposed_mob.notify_revival(I18n.T("Your body is being revived with Strange Reagent!"));
        exposed_mob.do_jitter_animation(10);

        // we factor in healing needed when determing if we do anything
        float healing = needed_to_revive * healing_per_reagent_unit;
        // but excessive healing is penalized, to reward doctors who use the perfect amount
        reac_volume = Dm.num(reac_volume) - needed_to_revive;
        healing += (Dm.num(reac_volume) * healing_per_reagent_unit) * excess_healing_ratio;

        // during unit tests, we want it to happen immediately
        if (Dm.truthy(instant))
        {
            await exposed_mob.do_strange_reagent_revival(healing);
        }
        else
        {
            // jitter immediately, after four seconds, and after eight seconds
            _addtimer(Dm.@new<datum_callback>(exposed_mob, nameof(mob_living.do_jitter_animation), 1 * SECONDS), 4 * SECONDS, file: "code/modules/reagents/chemistry/reagents/medicine_reagents.dm", line: 1166);
            _addtimer(Dm.@new<datum_callback>(exposed_mob, nameof(mob_living.do_strange_reagent_revival), healing), 7 * SECONDS, file: "code/modules/reagents/chemistry/reagents/medicine_reagents.dm", line: 1167);
            _addtimer(Dm.@new<datum_callback>(exposed_mob, nameof(mob_living.do_jitter_animation), 1 * SECONDS), 8 * SECONDS, file: "code/modules/reagents/chemistry/reagents/medicine_reagents.dm", line: 1168);
        }

        await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);
    }

    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1172
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        float damage_at_random = rand(0, 250) / 100; //0 to 2.5
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        float need_mob_update;
        need_mob_update = Dm.num(await mob.adjust_brute_loss(0.4f * damage_at_random * k, updating_health: FALSE, required_bodytype: affected_bodytype));
        need_mob_update += Dm.num(await mob.adjust_fire_loss(0.4f * damage_at_random * k, updating_health: FALSE, required_bodytype: affected_bodytype));
        if (Dm.truthy(need_mob_update))
            return UPDATE_MOB_HEALTH;
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1182
public partial class datum_reagent_medicine_strange_reagent_fishy_reagent : datum_reagent_medicine_strange_reagent
{
    // only revives fish.
    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1192
    public override object pre_rez_check(atom thing_to_rez = null)
    {
        if (ismob(thing_to_rez))
        {
            mob_living mob_to_rez = thing_to_rez as mob_living;
            if (Dm.truthy(Dm.bitand(mob_to_rez.mob_biotypes, MOB_AQUATIC)))
                return TRUE;
            return FALSE;
        }
        if (thing_to_rez is obj_item_fish) // isfish
            return TRUE;
        return FALSE;
    }
}

// tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1240
public partial class datum_reagent_medicine_neurine : datum_reagent_medicine
{
    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1253
    public override async DmTask on_mob_add(mob_living affected_mob = null, object amount = null)
    {
        await base.on_mob_add(affected_mob, amount);
        if (!iscarbon(affected_mob))
            return;
        mob_living_carbon affected_carbon = (mob_living_carbon)affected_mob;
        if (creation_purity >= 1)
            initial_bdamage = Dm.num(affected_carbon.get_organ_loss(ORGAN_SLOT_BRAIN));
    }

    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1261
    public override async DmTask on_mob_delete(mob_living affected_mob = null)
    {
        await base.on_mob_delete(affected_mob);
        if (!iscarbon(affected_mob))
            return;
        mob_living_carbon affected_carbon = (mob_living_carbon)affected_mob;
        if (initial_bdamage < Dm.num(affected_carbon.get_organ_loss(ORGAN_SLOT_BRAIN)))
            await affected_carbon.set_organ_loss(ORGAN_SLOT_BRAIN, initial_bdamage);
    }

    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1269
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        if (Dm.truthy(holder.has_reagent(typeof(datum_reagent_consumable_ethanol_neurotoxin))))
            await holder.remove_reagent(typeof(datum_reagent_consumable_ethanol_neurotoxin), 2.5f * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick) * Dm.num(normalise_creation_purity()));
        if (SPT_PROB(8 * Dm.num(normalise_creation_purity()), seconds_per_tick))
            mob.cure_trauma_type(resilience: TRAUMA_RESILIENCE_BASIC);
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1276
    public override async DmTask<object> on_mob_dead(mob_living_carbon affected_mob = null, object seconds_per_tick = null, params object[] __extra)
    {
        object metabolization_ratio = Dm.arg(__extra, 0);
        object dot = await base.on_mob_dead(affected_mob, seconds_per_tick, __extra);
        if (Dm.truthy(await affected_mob.adjust_organ_loss(ORGAN_SLOT_BRAIN, -0.5f * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick) * Dm.num(normalise_creation_purity()), required_organ_flag: affected_organ_flags)))
            return UPDATE_MOB_HEALTH;
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1282
public partial class datum_reagent_medicine_mutadone : datum_reagent_medicine
{
    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1290
    public override async DmTask on_mob_metabolize(mob_living affected_mob = null, params object[] __extra)
    {
        await base.on_mob_metabolize(affected_mob, __extra);
        if (!ishuman(affected_mob))
            return;
        mob_living_carbon_human human_mob = (mob_living_carbon_human)affected_mob;
        if (ismonkey(human_mob))
        {
            if (!HAS_TRAIT(human_mob, TRAIT_BORN_MONKEY))
            {
                //This is the only time mutadone should remove monkeyism
                await human_mob.dna.remove_mutation(typeof(datum_mutation_race), GLOB.standard_mutation_sources);
            }
        }
        else if (HAS_TRAIT(human_mob, TRAIT_BORN_MONKEY))
        {
            human_mob.monkeyize();
        }
    }

    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1303
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        mob.remove_status_effect(typeof(datum_status_effect_jitter));
        if (Dm.truthy(mob.has_dna()))
        {
            DmList mutations = mob.dna.mutations;
            if (Dm.truthy(LAZYLEN(mutations))) // Don't need to be doing this at all if there are no mutations
                await mob.dna.remove_mutation_group(Dm.sub(mutations, mob.dna.get_mutation(typeof(datum_mutation_race))) as DmList, GLOB.standard_mutation_sources);
            mob.dna.scrambled = FALSE;
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1313
public partial class datum_reagent_medicine_antihol : datum_reagent_medicine
{
    /// All status effects we remove on metabolize.
    /// Does not include drunk (despite what you may thing) as that's decresed gradually
    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1325  var/static/list/status_effects_to_clear (rules 10.6: a TgState field, below)
    public static ref DmList status_effects_to_clear => ref TgState.Current.datum_reagent_medicine_antihol__status_effects_to_clear;

    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1332
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        foreach (object effect in Dm.each(status_effects_to_clear))
            mob.remove_status_effect(effect);
        await ((datum_reagents)mob.reagents).remove_reagent(typeof(datum_reagent_consumable_ethanol), 4 * k * Dm.num(normalise_creation_purity()), include_subtypes: TRUE);
        if (Dm.truthy(await mob.adjust_tox_loss(-0.1f * k, updating_health: FALSE, required_biotype: affected_biotype)))
            dot = UPDATE_MOB_HEALTH;
        await mob.adjust_drunk_effect(-5 * k * Dm.num(normalise_creation_purity()));
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1341
    public override async DmTask expose_mob(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra)
    {
        methods ??= TOUCH;
        await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);
        if (!Dm.truthy(Dm.bitand(methods, Dm.bitor(Dm.bitor(TOUCH, VAPOR), PATCH))))
            return;

        exposed_mob.add_surgery_speed_mod(type, 1.1f, min(Dm.num(reac_volume) * 1 * MINUTES, 5 * MINUTES));
    }
}

public sealed partial class TgState
{
    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1325-1330  /datum/reagent/medicine/antihol/var/static/list/status_effects_to_clear
    internal DmList datum_reagent_medicine_antihol__status_effects_to_clear = Dm.list(
        typeof(datum_status_effect_confusion),
        typeof(datum_status_effect_dizziness),
        typeof(datum_status_effect_drowsiness),
        typeof(datum_status_effect_speech_slurring_drunk));
}

// tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1349
public partial class datum_reagent_medicine_stimulants : datum_reagent_medicine
{
    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1360
    public override async DmTask on_mob_metabolize(mob_living affected_mob = null, params object[] __extra)
    {
        await base.on_mob_metabolize(affected_mob, __extra);
        await affected_mob.add_movespeed_modifier(typeof(datum_movespeed_modifier_reagent_stimulants));
    }

    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1364
    public override async DmTask on_mob_end_metabolize(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.on_mob_end_metabolize(affected_mob, metabolization_ratio);
        affected_mob.remove_movespeed_modifier(typeof(datum_movespeed_modifier_reagent_stimulants));
    }

    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1368
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        float need_mob_update = 0;
        if (mob.health < 50 && mob.health > 0)
        {
            float heal = -1 * k;
            need_mob_update += Dm.num(await mob.adjust_oxy_loss(heal, updating_health: FALSE, required_biotype: affected_biotype));
            need_mob_update += Dm.num(await mob.adjust_tox_loss(heal, updating_health: FALSE, required_biotype: affected_biotype));
            need_mob_update += Dm.num(await mob.adjust_brute_loss(heal, updating_health: FALSE, required_bodytype: affected_bodytype));
            need_mob_update += Dm.num(await mob.adjust_fire_loss(heal, updating_health: FALSE, required_bodytype: affected_bodytype));
        }

        await mob.AdjustAllImmobility(-60 * k);
        need_mob_update += Dm.num(await mob.adjust_stamina_loss(-12 * k, updating_stamina: FALSE, required_biotype: affected_biotype));
        if (Dm.truthy(need_mob_update))
            return UPDATE_MOB_HEALTH;
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1383
    public override async DmTask<object> overdose_process(mob_living affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.overdose_process(affected_mob, seconds_per_tick, metabolization_ratio);
        if (SPT_PROB(18, seconds_per_tick))
        {
            await affected_mob.adjust_stamina_loss(2.5f * Dm.num(metabolization_ratio), updating_stamina: FALSE, required_biotype: affected_biotype);
            await affected_mob.adjust_tox_loss(1 * Dm.num(metabolization_ratio), updating_health: FALSE, required_biotype: affected_biotype);
            affected_mob.losebreath++;
            return UPDATE_MOB_HEALTH;
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1392
public partial class datum_reagent_medicine_insulin : datum_reagent_medicine
{
    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1400
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        await mob.AdjustSleeping(-2 * SECONDS * k);
        await holder.remove_reagent(typeof(datum_reagent_consumable_sugar), 3 * k);
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1422
public partial class datum_reagent_medicine_regen_jelly : datum_reagent_medicine
{
    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1431  (tg names its second parameter reac_volume, so the
    // methods argument stands in for the volume: kept)
    public override async DmTask expose_mob(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra)
    {
        await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);
        if (!ishuman(exposed_mob) || (Dm.num(methods) < 0.5f))
            return;

        mob_living_carbon_human exposed_human = (mob_living_carbon_human)exposed_mob;
        exposed_human.set_facial_haircolor(color, update: FALSE);
        exposed_human.set_haircolor(color, update: TRUE);
    }

    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1440
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        float heal = -healing * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        float need_mob_update;
        need_mob_update = Dm.num(await mob.adjust_brute_loss(heal, updating_health: FALSE, required_bodytype: affected_bodytype));
        need_mob_update += Dm.num(await mob.adjust_fire_loss(heal, updating_health: FALSE, required_bodytype: affected_bodytype));
        need_mob_update += Dm.num(await mob.adjust_oxy_loss(heal, updating_health: FALSE, required_biotype: affected_biotype));
        need_mob_update += Dm.num(await mob.adjust_tox_loss(heal, updating_health: FALSE, forced: TRUE, required_biotype: affected_biotype)); //heals TOXINLOVERs
        if (Dm.truthy(need_mob_update))
            return UPDATE_MOB_HEALTH;
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1485
public partial class datum_reagent_medicine_earthsblood : datum_reagent_medicine
{
    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1495
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        float need_mob_update;
        if (current_cycle < 25) //10u has to be processed before u get into THE FUN ZONE
        {
            need_mob_update = Dm.num(await mob.adjust_brute_loss(-0.5f * k, updating_health: FALSE, required_bodytype: affected_bodytype));
            need_mob_update += Dm.num(await mob.adjust_fire_loss(-0.5f * k, updating_health: FALSE, required_bodytype: affected_bodytype));
            need_mob_update += Dm.num(await mob.adjust_oxy_loss(-0.25f * k, updating_health: FALSE, required_biotype: affected_biotype));
            need_mob_update += Dm.num(await mob.adjust_tox_loss(-0.25f * k, updating_health: FALSE, required_biotype: affected_biotype));
            need_mob_update += Dm.num(await mob.adjust_stamina_loss(-1 * k, updating_stamina: FALSE, required_biotype: affected_biotype));
            need_mob_update += Dm.num(await mob.adjust_organ_loss(ORGAN_SLOT_BRAIN, 0.5f * k, 150, affected_organ_flags)); //This does, after all, come from ambrosia, and the most powerful ambrosia in existence, at that!
        }
        else
        {
            need_mob_update = Dm.num(await mob.adjust_brute_loss(-2.5f * k, updating_health: FALSE, required_bodytype: affected_bodytype)); //slow to start, but very quick healing once it gets going
            need_mob_update += Dm.num(await mob.adjust_fire_loss(-2.5f * k, updating_health: FALSE, required_bodytype: affected_bodytype));
            need_mob_update += Dm.num(await mob.adjust_oxy_loss(-1.5f * k, updating_health: FALSE, required_biotype: affected_biotype));
            need_mob_update += Dm.num(await mob.adjust_tox_loss(-1.5f * k, updating_health: FALSE, required_biotype: affected_biotype));
            need_mob_update += Dm.num(await mob.adjust_stamina_loss(-4 * k, updating_stamina: FALSE, required_biotype: affected_biotype));
            need_mob_update += Dm.num(await mob.adjust_organ_loss(ORGAN_SLOT_BRAIN, 1 * k, 150, affected_organ_flags));
            await mob.adjust_jitter_up_to(3 * SECONDS * k, 1 * MINUTES);
            if (SPT_PROB(5, seconds_per_tick))
                await mob.say(return_hippie_line(), forced: typeof(datum_reagent_medicine_earthsblood));
        }
        await mob.adjust_drugginess_up_to(10 * SECONDS * k, 15 * SECONDS * k);
        if (Dm.truthy(need_mob_update))
            return UPDATE_MOB_HEALTH;
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1519
    public override async DmTask<object> overdose_process(mob_living affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.overdose_process(affected_mob, seconds_per_tick, metabolization_ratio);
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        await affected_mob.adjust_hallucinations_up_to(5 * SECONDS * k, 120 * SECONDS);
        float need_mob_update = 0;
        if (current_cycle > 26)
        {
            need_mob_update = Dm.num(await affected_mob.adjust_tox_loss(2 * k, updating_health: FALSE, required_biotype: affected_biotype));
            if (current_cycle > 101) //podpeople get out reeeeeeeeeeeeeeeeeeeee
                need_mob_update += Dm.num(await affected_mob.adjust_tox_loss(3 * k, updating_health: FALSE, required_biotype: affected_biotype));
        }
        if (iscarbon(affected_mob))
        {
            mob_living_carbon hippie = (mob_living_carbon)affected_mob;
            await hippie.gain_trauma(typeof(datum_brain_trauma_severe_pacifism));
        }

        if (Dm.truthy(need_mob_update))
            return UPDATE_MOB_HEALTH;
        return dot;
    }

    /// Returns a hippie-esque string for the person affected by the reagent to say.
    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1535
    public virtual object return_hippie_line()
    {
        return pick(return_hippie_line__earthsblood_lines);
    }

    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1536  proc-level var/static/list/earthsblood_lines (rules 10.6)
    private static ref DmList return_hippie_line__earthsblood_lines => ref TgState.Current.datum_reagent_medicine_earthsblood__return_hippie_line__earthsblood_lines;
}

public sealed partial class TgState
{
    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1536-1543  /datum/reagent/medicine/earthsblood/proc/return_hippie_line var/static/list/earthsblood_lines
    internal DmList datum_reagent_medicine_earthsblood__return_hippie_line__earthsblood_lines = Dm.list(
        "Am I glad he's frozen in there and that we're out here, and that he's the sheriff and that we're frozen out here, and that we're in there, and I just remembered, we're out here. What I wanna know is: Where's the caveman?",
        "Do you believe in magic in a young girl's heart?",
        "It ain't me, it ain't me...",
        "Make love, not war!",
        "Stop, hey, what's that sound? Everybody look what's going down...",
        "Yeah, well, you know, that's just, like, uh, your opinion, man.");
}

// tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1548
public partial class datum_reagent_medicine_haloperidol : datum_reagent_medicine
{
    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1556
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        datum_reagents blood = (datum_reagents)mob.reagents;
        foreach (datum_reagent_drug reagent in Dm.each<datum_reagent_drug>(blood.reagent_list))
            await blood.remove_reagent(reagent.type, 2.5f * reagent.purge_multiplier * k);
        await mob.adjust_drowsiness(5 * SECONDS * k);

        if (Dm.num(mob.get_timed_status_effect_duration(typeof(datum_status_effect_jitter))) >= 6 * SECONDS)
            await mob.adjust_jitter(-7.5f * SECONDS * k);

        if (Dm.num(mob.get_timed_status_effect_duration(typeof(datum_status_effect_hallucination))) >= 10 * SECONDS)
            await mob.adjust_hallucinations(-12.5f * SECONDS * k);

        if (Dm.num(mob.get_stamina_loss()) >= 100)
            await blood.remove_reagent(type, 1.25f * metabolization_rate * k);

        float need_mob_update = FALSE;
        if (SPT_PROB(10, seconds_per_tick))
            need_mob_update += Dm.num(await mob.adjust_organ_loss(ORGAN_SLOT_BRAIN, 1, 50, affected_organ_flags));
        need_mob_update += Dm.num(await mob.adjust_stamina_loss(3.125f * k, updating_stamina: FALSE, required_biotype: affected_biotype));
        if (Dm.truthy(need_mob_update))
            return UPDATE_MOB_HEALTH;
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1651
public partial class datum_reagent_medicine_cordiolis_hepatico : datum_reagent_medicine
{
    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1658
    public override async DmTask on_mob_add(mob_living affected_mob = null, object amount = null)
    {
        await base.on_mob_add(affected_mob, amount);
        affected_mob.add_traits(Dm.list(TRAIT_STABLELIVER, TRAIT_STABLEHEART), type);
    }

    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1662
    public override async DmTask on_mob_end_metabolize(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.on_mob_end_metabolize(affected_mob, metabolization_ratio);
        affected_mob.remove_traits(Dm.list(TRAIT_STABLELIVER, TRAIT_STABLEHEART), type);
    }
}

// tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1667
public partial class datum_reagent_medicine_muscle_stimulant : datum_reagent_medicine
{
    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1673
    public override async DmTask on_mob_metabolize(mob_living affected_mob = null, params object[] __extra)
    {
        await base.on_mob_metabolize(affected_mob, __extra);
        affected_mob.add_movespeed_mod_immunities(type, typeof(datum_movespeed_modifier_damage_slowdown));
    }

    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1677
    public override async DmTask on_mob_end_metabolize(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.on_mob_end_metabolize(affected_mob, metabolization_ratio);
        affected_mob.remove_movespeed_mod_immunities(type, typeof(datum_movespeed_modifier_damage_slowdown));
    }
}

// tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1682
public partial class datum_reagent_medicine_modafinil : datum_reagent_medicine
{
    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1695
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var metabolizer = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        if (Dm.truthy(overdosed)) // We do not want any effects on OD
            return dot;
        overdose_threshold = overdose_threshold + 5 * ((rand(-10, 10) / 10) * k); // for extra fun
        await metabolizer.AdjustAllImmobility(-25 * k);
        await metabolizer.adjust_stamina_loss(-15 * k, updating_stamina: FALSE, required_biotype: affected_biotype);
        await metabolizer.set_jitter_if_lower(5 * SECONDS * k);
        metabolization_rate = 0.005f * REAGENTS_METABOLISM * rand(5, 20); // randomizes metabolism between 0.02 and 0.08 per second
        return UPDATE_MOB_HEALTH;
    }

    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1706
    public override async DmTask overdose_start(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.overdose_start(affected_mob, metabolization_ratio);
        to_chat(affected_mob, span_userdanger(I18n.T("You feel awfully out of breath and jittery!")));
        metabolization_rate = 0.025f * REAGENTS_METABOLISM; // sets metabolism to 0.005 per second on overdose
    }

    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1711
    public override async DmTask<object> overdose_process(mob_living affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.overdose_process(affected_mob, seconds_per_tick, metabolization_ratio);
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        overdose_progress++;
        float need_mob_update = 0;
        float sw__1 = overdose_progress;
        if (Dm.in_to(sw__1, 1, 40))
        {
            await affected_mob.adjust_jitter_up_to(40 * SECONDS * k, 20 * SECONDS);
            await affected_mob.adjust_stutter_up_to(40 * SECONDS * k, 20 * SECONDS);
            await affected_mob.set_dizzy_if_lower(200 * SECONDS * k);
            if (SPT_PROB(30, seconds_per_tick))
            {
                affected_mob.losebreath++;
                need_mob_update = TRUE;
            }
        }
        else if (Dm.in_to(sw__1, 41, 80))
        {
            need_mob_update = Dm.num(await affected_mob.adjust_oxy_loss(2 * k, updating_health: FALSE, required_biotype: affected_biotype));
            need_mob_update += Dm.num(await affected_mob.adjust_stamina_loss(2 * k, updating_stamina: FALSE, required_biotype: affected_biotype));
            await affected_mob.adjust_jitter_up_to(40 * SECONDS * k, 40 * SECONDS);
            await affected_mob.adjust_stutter_up_to(40 * SECONDS * k, 40 * SECONDS);
            await affected_mob.set_dizzy_if_lower(400 * SECONDS * k);
            if (SPT_PROB(30, seconds_per_tick))
            {
                affected_mob.losebreath++;
                need_mob_update = TRUE;
            }
            if (SPT_PROB(10, seconds_per_tick))
            {
                to_chat(affected_mob, span_userdanger(I18n.T("You have a sudden fit!")));
                await affected_mob.emote("moan");
                await affected_mob.Paralyze(20); // you should be in a bad spot at this point unless epipen has been used
            }
        }
        else if (sw__1 == 81)
        {
            to_chat(affected_mob, span_userdanger(I18n.T("You feel too exhausted to continue!"))); // at this point you will eventually die unless you get charcoal
            need_mob_update = Dm.num(await affected_mob.adjust_oxy_loss(2 * k, updating_health: FALSE, required_biotype: affected_biotype));
            need_mob_update += Dm.num(await affected_mob.adjust_stamina_loss(2 * k, updating_stamina: FALSE, required_biotype: affected_biotype));
        }
        else if (Dm.in_to(sw__1, 82, INFINITY))
        {
            REMOVE_TRAIT(affected_mob, TRAIT_SLEEPIMMUNE, type);
            await affected_mob.Sleeping(200 * SECONDS * k);
            need_mob_update += Dm.num(await affected_mob.adjust_oxy_loss(30 * k, updating_health: FALSE, required_biotype: affected_biotype));
            need_mob_update += Dm.num(await affected_mob.adjust_stamina_loss(30 * k, updating_stamina: FALSE, required_biotype: affected_biotype));
        }
        if (Dm.truthy(need_mob_update))
            return UPDATE_MOB_HEALTH;
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1749
public partial class datum_reagent_medicine_psicodine : datum_reagent_medicine
{
    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1759
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        await mob.adjust_jitter(-24 * SECONDS * k);
        await mob.adjust_dizzy(-24 * SECONDS * k);
        await mob.adjust_confusion(-12 * SECONDS * k);
        mob.disgust = max(mob.disgust - (12 * k), 0);
        if (mob.mob_mood != null && mob.mob_mood.sanity <= SANITY_NEUTRAL) // only take effect if in negative sanity and then...
            await mob.mob_mood.adjust_sanity(10 * k, maximum: SANITY_NEUTRAL); // set minimum to prevent unwanted spiking over neutral
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1768
    public override async DmTask<object> overdose_process(mob_living affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.overdose_process(affected_mob, seconds_per_tick, metabolization_ratio);
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        await affected_mob.adjust_hallucinations_up_to(20 * SECONDS * k, 120 * SECONDS);
        if (Dm.truthy(await affected_mob.adjust_tox_loss(2 * k, updating_health: FALSE, required_biotype: affected_biotype)))
            return UPDATE_MOB_HEALTH;
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1795
public partial class datum_reagent_medicine_silibinin : datum_reagent_medicine
{
    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1802
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        if (Dm.truthy(await mob.adjust_organ_loss(ORGAN_SLOT_LIVER, -0.67f * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), required_organ_flag: affected_organ_flags))) // Add a chance to cure liver trauma once implemented.
            return UPDATE_MOB_HEALTH;
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1808
public partial class datum_reagent_medicine_polypyr : datum_reagent_medicine
{
    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1817
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null) //I wanted a collection of small positive effects, this is as hard to obtain as coniine after all.
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        float need_mob_update;
        need_mob_update = Dm.num(await mob.adjust_organ_loss(ORGAN_SLOT_LUNGS, -0.5f * k, required_organ_flag: affected_organ_flags));
        need_mob_update += Dm.num(await mob.adjust_brute_loss(-0.7f * k, updating_health: FALSE, required_bodytype: affected_bodytype));
        if (Dm.truthy(need_mob_update))
            return UPDATE_MOB_HEALTH;
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1825
    public override async DmTask expose_mob(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra)
    {
        methods ??= TOUCH;
        await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);
        if (!Dm.truthy(Dm.bitand(methods, Dm.bitor(TOUCH, VAPOR))) || !ishuman(exposed_mob) || (Dm.num(reac_volume) < 0.5f))
            return;
        mob_living_carbon_human exposed_human = (mob_living_carbon_human)exposed_mob;
        exposed_human.set_facial_haircolor("#9922ff", update: FALSE);
        exposed_human.set_haircolor(color); //this will call update_body_parts()
    }

    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1832
    public override async DmTask<object> overdose_process(mob_living affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.overdose_process(affected_mob, seconds_per_tick, metabolization_ratio);
        if (Dm.truthy(await affected_mob.adjust_organ_loss(ORGAN_SLOT_LUNGS, 1 * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), required_organ_flag: affected_organ_flags)))
            return UPDATE_MOB_HEALTH;
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1838
public partial class datum_reagent_medicine_granibitaluri : datum_reagent_medicine
{
    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1847
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        float healamount = max(1.5f - round(0.01f * (Dm.num(mob.get_brute_loss()) + Dm.num(mob.get_fire_loss())), 0.2f), 0.5f); //base of 1.5 healing per cycle and loses 0.2 healing for every 10 combined brute/burn damage you have
        float need_mob_update;
        need_mob_update = Dm.num(await mob.adjust_brute_loss(-1 * healamount * k, updating_health: FALSE, required_bodytype: affected_bodytype));
        need_mob_update += Dm.num(await mob.adjust_fire_loss(-1 * healamount * k, updating_health: FALSE, required_bodytype: affected_bodytype));
        if (Dm.truthy(need_mob_update))
            return UPDATE_MOB_HEALTH;
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1856
    public override async DmTask<object> overdose_process(mob_living affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.overdose_process(affected_mob, seconds_per_tick, metabolization_ratio);
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        float need_mob_update;
        need_mob_update = Dm.num(await affected_mob.adjust_organ_loss(ORGAN_SLOT_LIVER, 0.2f * k, required_organ_flag: affected_organ_flags));
        need_mob_update += Dm.num(await affected_mob.adjust_tox_loss(0.2f * k, updating_health: FALSE, required_biotype: affected_biotype)); //Only really deadly if you eat over 100u
        if (Dm.truthy(need_mob_update))
            return UPDATE_MOB_HEALTH;
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1958
public partial class datum_reagent_medicine_ondansetron : datum_reagent_medicine
{
    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1966
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        if (SPT_PROB(8, seconds_per_tick))
            await mob.adjust_drowsiness(2 * SECONDS * Dm.num(metabolization_ratio));
        if (SPT_PROB(15, seconds_per_tick) && !Dm.truthy(mob.get_stamina_loss()))
        {
            if (Dm.truthy(await mob.adjust_stamina_loss(10 * Dm.num(metabolization_ratio), updating_stamina: FALSE)))
                dot = UPDATE_MOB_HEALTH;
        }
        mob.adjust_disgust(-10 * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1976
public partial class datum_reagent_medicine_naloxone : datum_reagent_medicine
{
    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1985  var/static/list/opiates_to_clear (rules 10.6: a TgState field, below)
    public static ref DmList opiates_to_clear => ref TgState.Current.datum_reagent_medicine_naloxone__opiates_to_clear;

    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1993
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        foreach (object opiate in Dm.each(opiates_to_clear))
            await holder.remove_reagent(opiate, 3 * k);

        if (Dm.truthy(mob.mob_mood?.get_mood_event("numb")))
        {
            mob.clear_mood_event("numb");
            await mob.add_mood_event("not numb", typeof(datum_mood_event_antinarcotic_medium));
        }

        if (Dm.truthy(mob.mob_mood?.get_mood_event("smacked out")))
        {
            mob.clear_mood_event("smacked out");
            await mob.add_mood_event("not smacked out", typeof(datum_mood_event_antinarcotic_heavy));
        }

        await mob.adjust_drowsiness(-5 * SECONDS * k);
        if (mob.losebreath >= 1)
        {
            mob.losebreath -= 1 * k;
            return UPDATE_MOB_HEALTH;
        }
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/reagents/chemistry/reagents/medicine_reagents.dm:1985-1991  /datum/reagent/medicine/naloxone/var/static/list/opiates_to_clear
    internal DmList datum_reagent_medicine_naloxone__opiates_to_clear = Dm.list(
        typeof(datum_reagent_medicine_morphine),
        typeof(datum_reagent_impedrezene),
        typeof(datum_reagent_toxin_fentanyl),
        typeof(datum_reagent_drug_krokodil),
        typeof(datum_reagent_inverse_krokodil));
}
