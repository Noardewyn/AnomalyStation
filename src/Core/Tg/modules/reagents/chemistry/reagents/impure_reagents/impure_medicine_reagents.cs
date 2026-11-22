// Translated from tgstation (snapshot 1d99bd7f): code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. Gaps P2 (D298): the on_hydroponics_apply procs (hydroponics). Gaps RG2 (D339): every other impure and inverse
// medicine: helgrasp's curse hands, nooartrium's return from the dead, the temporary traumas, the speech mutations, the dulling
// pigment, newtonium's gravity; sadness's sanity drain and the hug that cures it since gaps MS1. Left (not-translated.md section
// 225): the chat icon of super melatonin's snore. The var blocks are generated (D111).

// tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:794
public partial class datum_reagent_inverse_sal_acid : datum_reagent_inverse
{
    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:794
    public override async DmTask on_hydroponics_apply(obj_machinery_hydroponics mytray = null, mob user = null)
    {
        mytray.adjust_plant_health(Dm.round(Dm.num(volume) * 0.5f));
        mytray.myseed?.adjust_production(-Dm.round(Dm.num(volume) * 0.2f));
        mytray.myseed?.adjust_potency(Dm.round(Dm.num(volume) * 0.25f));
        mytray.myseed?.adjust_yield(Dm.round(Dm.num(volume) * 0.2f));
    }
}

////////////////////MEDICINES///////////////////////////

//Catch all failed reaction for medicines - supposed to be non punishing
//Random healing of the 4 main groups
// tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:29
public partial class datum_reagent_impurity_healing_medicine_failure : datum_reagent_impurity_healing
{
    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:37
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        float need_mob_update = 0;
        object picked = pick("brute", "burn", "tox", "oxy");
        if (Dm.eq(picked, "brute"))
            need_mob_update = Dm.num(await mob.adjust_brute_loss(-0.2f * k, updating_health: FALSE, required_bodytype: affected_bodytype));
        else if (Dm.eq(picked, "burn"))
            need_mob_update = Dm.num(await mob.adjust_fire_loss(-0.2f * k, updating_health: FALSE, required_bodytype: affected_bodytype));
        else if (Dm.eq(picked, "tox"))
            need_mob_update = Dm.num(await mob.adjust_tox_loss(-0.2f * k, updating_health: FALSE, required_biotype: affected_biotype));
        else if (Dm.eq(picked, "oxy"))
            need_mob_update = Dm.num(await mob.adjust_oxy_loss(-0.2f * k, updating_health: FALSE, required_biotype: affected_biotype));
        if (Dm.truthy(need_mob_update))
            return UPDATE_MOB_HEALTH;
        return dot;
    }
}

// C2 medications
// Helbital
//Inverse:
// tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:57
public partial class datum_reagent_inverse_helgrasp : datum_reagent_inverse
{
    //Warns you about the impenting hands
    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:68
    public override async DmTask on_mob_add(mob_living affected_mob = null, object amount = null)
    {
        await base.on_mob_add(affected_mob, amount);
        to_chat(affected_mob, span_hierophant(I18n.T("You hear laughter as malevolent hands apparate before you, eager to drag you down to hell...! Look out!")));
        playsound(affected_mob.loc, Dm.resource("sound/effects/chemistry/ahaha.ogg"), 80, TRUE, -1); //Very obvious tell so people can be ready
    }

    //Sends hands after you for your hubris (tg's comment at :73-85 explains the lag compensation)
    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:85
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        await spawn_hands(mob);
        float spt = Dm.num(seconds_per_tick);
        lag_remainder += spt - floor(spt);
        spt = floor(spt);
        if (lag_remainder >= 1)
        {
            spt += 1;
            lag_remainder -= 1;
        }
        float hands = 1;
        float time = 2 / spt;
        while (hands < spt) //we already made a hand now so start from 1
        {
            LAZYADD(ref timer_ids, _addtimer(Dm.@new<datum_callback>(this, nameof(spawn_hands), mob), (time * hands) * SECONDS, TIMER_STOPPABLE, file: "code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm", line: 96)); //keep track of all the timers we set up
            hands += time;
        }
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:99
    public virtual async DmTask spawn_hands(mob_living_carbon affected_mob = null)
    {
        if (affected_mob == null && iscarbon(holder.my_atom)) //Catch timer
            affected_mob = (mob_living_carbon)holder.my_atom;
        await fire_curse_hand(affected_mob);
    }

    //At the end, we clear up any loose hanging timers just in case and spawn any remaining lag_remaining hands all at once.
    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:105
    public override async DmTask on_mob_delete(mob_living affected_mob = null)
    {
        await base.on_mob_delete(affected_mob);
        float hands = 0;
        while (lag_remainder > hands)
        {
            await spawn_hands(affected_mob as mob_living_carbon);
            hands++;
        }
        foreach (object id in Dm.each(timer_ids)) // So that we can be certain that all timers are deleted at the end.
            deltimer(id);
        timer_ids?.Cut(); // a null timer_ids (no timer was set) is a runtime error in tg, at the proc's last line
    }
}

//libital
//Inverse:
//Simply reduces your alcohol tolerance, kinda simular to prohol
// tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:125
public partial class datum_reagent_inverse_libitoil : datum_reagent_inverse
{
    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:132
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        await mob.adjust_organ_loss(ORGAN_SLOT_LIVER, 0.05f * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:136
    public override async DmTask on_mob_add(mob_living affected_mob = null, object amount = null)
    {
        await base.on_mob_add(affected_mob, amount);
        mob_living_carbon consumer = affected_mob as mob_living_carbon;
        if (consumer == null)
            return;
        RegisterSignal(consumer, COMSIG_CARBON_GAIN_ORGAN, nameof(on_gained_organ));
        RegisterSignal(consumer, COMSIG_CARBON_LOSE_ORGAN, nameof(on_removed_organ));
        obj_item_organ_liver this_liver = consumer.get_organ_slot(ORGAN_SLOT_LIVER) as obj_item_organ_liver;
        this_liver.alcohol_tolerance *= 2;
    }

    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:146
    [SignalHandler]
    public virtual void on_gained_organ(mob prev_owner = null, obj_item_organ organ = null)
    {
        if (organ is not obj_item_organ_liver)
            return;
        obj_item_organ_liver this_liver = (obj_item_organ_liver)organ;
        this_liver.alcohol_tolerance *= 2;
    }

    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:153
    [SignalHandler]
    public virtual void on_removed_organ(mob prev_owner = null, obj_item_organ organ = null)
    {
        if (organ is not obj_item_organ_liver)
            return;
        obj_item_organ_liver this_liver = (obj_item_organ_liver)organ;
        this_liver.alcohol_tolerance /= 2;
    }

    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:160
    public override async DmTask on_mob_delete(mob_living affected_mob = null)
    {
        await base.on_mob_delete(affected_mob);
        mob_living_carbon consumer = affected_mob as mob_living_carbon;
        UnregisterSignal(consumer, COMSIG_CARBON_LOSE_ORGAN);
        UnregisterSignal(consumer, COMSIG_CARBON_GAIN_ORGAN);
        obj_item_organ_liver this_liver = consumer.get_organ_slot(ORGAN_SLOT_LIVER) as obj_item_organ_liver;
        if (this_liver == null)
            return;
        this_liver.alcohol_tolerance /= 2;
    }
}

//probital
// tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:173
public partial class datum_reagent_impurity_probital_failed : datum_reagent_impurity
{
    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:183
    public override async DmTask overdose_start(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.overdose_start(affected_mob, metabolization_ratio);
        metabolization_rate = 4 * REAGENTS_METABOLISM;
    }
}

// tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:188
public partial class datum_reagent_peptides_failed : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:193
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        if (Dm.truthy(await mob.adjust_organ_loss(ORGAN_SLOT_BRAIN, 0.125f * k, 170)))
            dot = UPDATE_MOB_HEALTH;
        await mob.adjust_nutrition(-2.5f * k);
        return dot;
    }
}

//Lenturi
//inverse
// tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:202
public partial class datum_reagent_inverse_lentslurri : datum_reagent_inverse
{
    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:207
    public override async DmTask on_mob_metabolize(mob_living affected_mob = null, params object[] __extra)
    {
        await base.on_mob_metabolize(affected_mob, __extra);
        await affected_mob.add_movespeed_modifier(typeof(datum_movespeed_modifier_reagent_lenturi));
    }

    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:211
    public override async DmTask on_mob_end_metabolize(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.on_mob_end_metabolize(affected_mob, metabolization_ratio);
        affected_mob.remove_movespeed_modifier(typeof(datum_movespeed_modifier_reagent_lenturi));
    }
}

//Aiuri
//inverse
// tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:218
public partial class datum_reagent_inverse_aiuri : datum_reagent_inverse
{
    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:226
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        var owner = (mob_living_carbon)affected_mob;
        await owner.adjust_organ_loss(ORGAN_SLOT_EYES, 0.05f * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        await owner.adjust_eye_blur(amount_of_blur_applied * Dm.num(seconds_per_tick));
        await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        return TRUE;
    }
}

//Hercuri
//inverse
// tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:235
public partial class datum_reagent_inverse_hercuri : datum_reagent_inverse
{
    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:245
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        float heating = 0.5f * rand(5, 25) * creation_purity * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        datum_reagents mob_reagents = mob.reagents as datum_reagents;
        if (mob_reagents != null)
            await mob_reagents.expose_temperature(mob_reagents.chem_temp + heating, 1);
        mob.adjust_bodytemperature(heating * TEMPERATURE_DAMAGE_COEFFICIENT);
        if (!ishuman(mob))
            return dot;
        mob_living_carbon_human human = (mob_living_carbon_human)mob;
        human.adjust_coretemperature(heating * TEMPERATURE_DAMAGE_COEFFICIENT);
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:257
    public override async DmTask expose_mob(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra)
    {
        methods ??= VAPOR;
        await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);
        if (!Dm.truthy(Dm.bitand(methods, VAPOR)))
            return;

        exposed_mob.adjust_bodytemperature(Dm.num(reac_volume) * TEMPERATURE_DAMAGE_COEFFICIENT);
        await exposed_mob.adjust_fire_stacks(Dm.num(reac_volume) / 2);
    }

    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:265
    public override async DmTask<object> overdose_process(mob_living affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.overdose_process(affected_mob, seconds_per_tick, metabolization_ratio);
        if (Dm.truthy(await affected_mob.adjust_organ_loss(ORGAN_SLOT_LIVER, 1 * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), required_organ_flag: affected_organ_flags))) //Makes it so you can't abuse it with pyroxadone very easily (liver dies from 25u unless it's fully upgraded)
            dot = UPDATE_MOB_HEALTH;
        float heating = 5 * creation_purity * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick) * TEMPERATURE_DAMAGE_COEFFICIENT;
        affected_mob.adjust_bodytemperature(heating); //hot hot
        if (ishuman(affected_mob))
        {
            mob_living_carbon_human human = (mob_living_carbon_human)affected_mob;
            human.adjust_coretemperature(heating);
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:276
public partial class datum_reagent_inverse_healing_tirimol : datum_reagent_inverse_healing
{
    //Makes patients fall asleep, then boosts the purirty of their medicine reagents if they're asleep
    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:285
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        float sw__1 = current_cycle;
        if (Dm.in_to(sw__1, 2, 11)) //same delay as chloral hydrate
        {
            if (prob(50))
                await mob.emote("yawn");
        }
        else if (Dm.in_to(sw__1, 11, INFINITY))
        {
            await mob.Sleeping(40);
            dot = 1;
            if (Dm.truthy(mob.IsSleeping()))
            {
                foreach (datum_reagent reagent in Dm.each_cast<datum_reagent>(((datum_reagents)mob.reagents).reagent_list))
                {
                    if (Dm.@in(reagent, cached_reagent_list))
                        continue;
                    if (reagent is not datum_reagent_medicine)
                        continue;
                    reagent.creation_purity *= 1.25f;
                    Dm.append(ref cached_reagent_list, reagent);
                }
            }
            else if (!Dm.truthy(mob.IsSleeping()) && Dm.truthy(Dm.length(cached_reagent_list)))
            {
                foreach (datum_reagent reagent in Dm.each_cast<datum_reagent>(cached_reagent_list))
                {
                    if (reagent == null)
                        continue;
                    reagent.creation_purity *= 0.8f;
                }
                cached_reagent_list = Dm.list();
            }
        }
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:310
    public override async DmTask on_mob_delete(mob_living affected_mob = null)
    {
        await base.on_mob_delete(affected_mob);
        if (Dm.truthy(affected_mob.IsSleeping()))
            affected_mob.visible_message(span_notice(I18n.T("[] lets out a hearty snore!", affected_mob))); //small way of letting people know the supersnooze is ended (tg's icon2html of the mob leads the line: the chat icons, a row)
        foreach (datum_reagent reagent in Dm.each_cast<datum_reagent>(cached_reagent_list))
        {
            if (reagent == null)
                continue;
            reagent.creation_purity *= 0.8f;
        }
        cached_reagent_list = Dm.list();
    }
}

//convermol
//inverse
// tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:323
public partial class datum_reagent_inverse_healing_convermol : datum_reagent_inverse_healing
{
    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:337
    public override async DmTask on_mob_add(mob_living affected_mob = null, object amount = null)
    {
        await base.on_mob_add(affected_mob, amount);
        RegisterSignal(affected_mob, COMSIG_CARBON_GAIN_ORGAN, nameof(on_gained_organ));
        RegisterSignal(affected_mob, COMSIG_CARBON_LOSE_ORGAN, nameof(on_removed_organ));
        obj_item_organ_lungs lungs = affected_mob.get_organ_slot(ORGAN_SLOT_LUNGS) as obj_item_organ_lungs;
        if (lungs == null)
            return;
        apply_lung_levels(lungs);
    }

    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:346
    [SignalHandler]
    public virtual void on_gained_organ(mob prev_owner = null, obj_item_organ organ = null)
    {
        if (organ is not obj_item_organ_lungs)
            return;
        obj_item_organ_lungs lungs = (obj_item_organ_lungs)organ;
        apply_lung_levels(lungs);
    }

    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:353
    public virtual void apply_lung_levels(obj_item_organ_lungs lungs = null)
    {
        cached_heat_level_1 = lungs.heat_level_1_threshold;
        cached_heat_level_2 = lungs.heat_level_2_threshold;
        cached_heat_level_3 = lungs.heat_level_3_threshold;
        cached_cold_level_1 = lungs.cold_level_1_threshold;
        cached_cold_level_2 = lungs.cold_level_2_threshold;
        cached_cold_level_3 = lungs.cold_level_3_threshold;
        //Heat threshold is increased
        lungs.heat_level_1_threshold *= creation_purity * 1.5f;
        lungs.heat_level_2_threshold *= creation_purity * 1.5f;
        lungs.heat_level_3_threshold *= creation_purity * 1.5f;
        //Cold threshold is decreased
        lungs.cold_level_1_threshold *= creation_purity * 0.5f;
        lungs.cold_level_2_threshold *= creation_purity * 0.5f;
        lungs.cold_level_3_threshold *= creation_purity * 0.5f;
    }

    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:369
    [SignalHandler]
    public virtual void on_removed_organ(mob prev_owner = null, obj_item_organ organ = null)
    {
        if (organ is not obj_item_organ_lungs)
            return;
        obj_item_organ_lungs lungs = (obj_item_organ_lungs)organ;
        restore_lung_levels(lungs);
    }

    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:376
    public virtual void restore_lung_levels(obj_item_organ_lungs lungs = null)
    {
        lungs.heat_level_1_threshold = Dm.num(cached_heat_level_1);
        lungs.heat_level_2_threshold = Dm.num(cached_heat_level_2);
        lungs.heat_level_3_threshold = Dm.num(cached_heat_level_3);
        lungs.cold_level_1_threshold = Dm.num(cached_cold_level_1);
        lungs.cold_level_2_threshold = Dm.num(cached_cold_level_2);
        lungs.cold_level_3_threshold = Dm.num(cached_cold_level_3);
    }

    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:384
    public override async DmTask on_mob_delete(mob_living affected_mob = null)
    {
        await base.on_mob_delete(affected_mob);
        UnregisterSignal(affected_mob, COMSIG_CARBON_LOSE_ORGAN);
        UnregisterSignal(affected_mob, COMSIG_CARBON_GAIN_ORGAN);
        obj_item_organ_lungs lungs = affected_mob.get_organ_slot(ORGAN_SLOT_LUNGS) as obj_item_organ_lungs;
        if (lungs == null)
            return;
        restore_lung_levels(lungs);
    }
}

//seiver
//Inverse
//Allows the scanner to detect organ health to the nearest 1% (similar use to irl) and upgrates the scan to advanced
// tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:397
public partial class datum_reagent_inverse_technetium : datum_reagent_inverse
{
    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:405
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        time_until_next_poison -= Dm.num(seconds_per_tick) * (1 * SECONDS);
        if (time_until_next_poison <= 0)
        {
            time_until_next_poison = poison_interval;
            if (Dm.truthy(await mob.adjust_tox_loss(1.34f * creation_purity * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), updating_health: FALSE, required_biotype: affected_biotype)))
                return UPDATE_MOB_HEALTH;
        }
        return dot;
    }
}

//Kind of a healing effect, Presumably you're using syrinver to purge so this helps that
// tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:415
public partial class datum_reagent_inverse_healing_syriniver : datum_reagent_inverse_healing
{
    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:423
    public override async DmTask on_mob_add(mob_living affected_mob = null, object amount = null)
    {
        if (!iscarbon(affected_mob))
        {
            await base.on_mob_add(affected_mob, amount);
            return;
        }
        mob_living_carbon affected_carbon = (mob_living_carbon)affected_mob;
        foreach (datum_reagent reagent in Dm.each_cast<datum_reagent>(((datum_reagents)affected_carbon.reagents).reagent_list))
        {
            if (Dm.@in(reagent, cached_reagent_list))
                continue;
            if (reagent is datum_reagent_medicine)
                continue;
            reagent.creation_purity *= 0.8f;
            Dm.append(ref cached_reagent_list, reagent);
        }
        await base.on_mob_add(affected_mob, amount);
    }

    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:436
    public override async DmTask on_mob_delete(mob_living affected_mob = null)
    {
        await base.on_mob_delete(affected_mob);
        if (!iscarbon(affected_mob))
            return;
        if (cached_reagent_list == null)
            return;
        foreach (datum_reagent reagent in Dm.each_cast<datum_reagent>(cached_reagent_list))
        {
            if (reagent == null)
                continue;
            reagent.creation_purity *= 1.25f;
        }
        cached_reagent_list = null;
    }
}

//Multiver
//Inverse
//Reaction product when between 0.2 and 0.35 purity.
// tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:452
public partial class datum_reagent_inverse_healing_monover : datum_reagent_inverse_healing
{
    //Heals toxins if it's the only thing present - kinda the oposite of multiver! Maybe that's why it's inverse!
    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:458
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        float need_mob_update;
        if (Dm.length(((datum_reagents)mob.reagents).reagent_list) > 1)
            need_mob_update = Dm.num(await mob.adjust_organ_loss(ORGAN_SLOT_LUNGS, 0.5f * Dm.num(seconds_per_tick), required_organ_flag: affected_organ_flags)); //Hey! It's everyone's favourite drawback from multiver!
        else
            need_mob_update = Dm.num(await mob.adjust_tox_loss(-1 * Dm.num(metabolization_ratio) * creation_purity * Dm.num(seconds_per_tick), updating_health: FALSE, required_biotype: affected_biotype));
        if (Dm.truthy(need_mob_update))
            return UPDATE_MOB_HEALTH;
        return dot;
    }
}

///Can bring a corpse back to life temporarily (if heart is intact)
///Makes wounds bleed more, if it brought someone back, they take additional brute and heart damage
///They can't die during this, but if they're past crit then take increasing stamina damage
///If they're past fullcrit, their movement is slowed by half
///If they OD, their heart explodes (if they were brought back from the dead)
// tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:474
public partial class datum_reagent_inverse_penthrite : datum_reagent_inverse
{
    /// List of trait buffs to give to the affected mob, and remove as needed.
    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:487  var/static/list/trait_buffs (rules 10.6: a TgState field, below)
    public static ref DmList trait_buffs => ref TgState.Current.datum_reagent_inverse_penthrite__trait_buffs;

    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:497
    public override async DmTask<object> on_mob_dead(mob_living_carbon affected_mob = null, object seconds_per_tick = null, params object[] __extra)
    {
        object dot = await base.on_mob_dead(affected_mob, seconds_per_tick, __extra);
        if (HAS_TRAIT(affected_mob, TRAIT_SUICIDED))
            return dot;
        obj_item_organ_heart heart = affected_mob.get_organ_slot(ORGAN_SLOT_HEART) as obj_item_organ_heart;
        if (heart == null || Dm.truthy(Dm.bitand(heart.organ_flags, ORGAN_FAILING)))
            return dot;
        metabolization_rate = 0.5f * REAGENTS_METABOLISM;
        affected_mob.add_traits(trait_buffs, type);
        if (!Dm.truthy(await affected_mob.revive(force_grab_ghost: TRUE)))
            return dot;

        await affected_mob.set_resting(FALSE, TRUE); // Please get up, no one wants a deaththrows juggernaught that lies on the floor all the time
        await affected_mob.SetAllImmobility(0 * SECONDS);
        back_from_the_dead = TRUE;
        await affected_mob.emote("gasp");
        affected_mob.playsound_local(affected_mob, Dm.resource("sound/effects/health/fastbeat.ogg"), 65);
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:515
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        if (!Dm.truthy(back_from_the_dead))
            return dot;
        //Following is for those brought back from the dead only
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        float creation_impurity = 1 - creation_purity;
        foreach (datum_wound iter_wound in Dm.each_cast<datum_wound>(mob.all_wounds))
            await iter_wound.adjust_blood_flow(4 * creation_impurity * k);
        float need_mob_update = 0;
        need_mob_update += Dm.num(await mob.adjust_brute_loss(20 * creation_impurity * k, required_bodytype: affected_bodytype));
        need_mob_update += Dm.num(await mob.adjust_organ_loss(ORGAN_SLOT_HEART, 4 * ((1 + creation_impurity) * k), required_organ_flag: affected_organ_flags));
        if (mob.health < mob.crit_threshold)
            await mob.add_movespeed_modifier(typeof(datum_movespeed_modifier_reagent_nooartrium));
        if (mob.health < mob.hardcrit_threshold)
            await mob.add_actionspeed_modifier(typeof(datum_actionspeed_modifier_nooartrium));
        obj_item_organ_heart heart = mob.get_organ_slot(ORGAN_SLOT_HEART) as obj_item_organ_heart;
        if ((heart == null) || Dm.truthy(Dm.bitand(heart.organ_flags, ORGAN_FAILING)))
            remove_buffs(mob);
        if (Dm.truthy(need_mob_update))
            return UPDATE_MOB_HEALTH;
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:536
    public override async DmTask on_mob_delete(mob_living affected_mob = null)
    {
        await base.on_mob_delete(affected_mob);
        mob_living_carbon carbon = (mob_living_carbon)affected_mob;
        remove_buffs(carbon);
        obj_item_organ_heart heart = carbon.get_organ_slot(ORGAN_SLOT_HEART) as obj_item_organ_heart;
        if (carbon.health < (5 * carbon.dead_threshold) || Dm.truthy(Dm.bitand(heart?.organ_flags, ORGAN_FAILING))) // Honestly commendable if you get -500
        {
            // tg passes the reagent as explosion_cause (a datum where the proc types an atom, read only by the logs): left null
            await explosion(carbon, light_impact_range: 1);
            carbon.visible_message(span_boldwarning(I18n.T("[]'s heart explodes!", carbon)));
            qdel(heart);
        }
    }

    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:545
    public override async DmTask overdose_start(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        if (!Dm.truthy(back_from_the_dead))
        {
            await base.overdose_start(affected_mob, metabolization_ratio);
            return;
        }

        obj_item_organ_heart heart = affected_mob.get_organ_slot(ORGAN_SLOT_HEART) as obj_item_organ_heart;
        if ((heart == null)) // No heart? No life!
        {
            REMOVE_TRAIT(affected_mob, TRAIT_NODEATH, type);
        }
        else
        {
            await explosion(affected_mob, light_impact_range: 1); // explosion_cause = src: see on_mob_delete
            affected_mob.visible_message(span_boldwarning(I18n.T("[]'s heart explodes!", affected_mob)));
            qdel(heart);
        }
        await base.overdose_start(affected_mob, metabolization_ratio);
    }

    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:558
    public virtual void remove_buffs(mob_living_carbon affected_mob = null)
    {
        affected_mob.remove_traits(trait_buffs, type);
        affected_mob.remove_movespeed_modifier(typeof(datum_movespeed_modifier_reagent_nooartrium));
        affected_mob.remove_actionspeed_modifier(typeof(datum_actionspeed_modifier_nooartrium));
    }
}

public sealed partial class TgState
{
    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:487-495  /datum/reagent/inverse/penthrite/var/static/list/trait_buffs
    internal DmList datum_reagent_inverse_penthrite__trait_buffs = Dm.list(
        TRAIT_NOCRITDAMAGE,
        TRAIT_NOCRITOVERLAY,
        TRAIT_NODEATH,
        TRAIT_NOHARDCRIT,
        TRAIT_NOSOFTCRIT,
        TRAIT_STABLEHEART,
        TRAIT_NO_OXYLOSS_PASSOUT);

    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:616  /datum/reagent/inverse/neurine/proc/on_mob_life var/static/list/traumalist
    internal DmList datum_reagent_inverse_neurine__on_mob_life__traumalist;

    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:868-878  /datum/reagent/inverse/atropine/proc/overdose_process var/static/list/possible_organs
    internal DmList datum_reagent_inverse_atropine__overdose_process__possible_organs = Dm.list(
        ORGAN_SLOT_HEART,
        ORGAN_SLOT_LIVER,
        ORGAN_SLOT_LUNGS,
        ORGAN_SLOT_STOMACH,
        ORGAN_SLOT_EYES,
        ORGAN_SLOT_EARS,
        ORGAN_SLOT_BRAIN,
        ORGAN_SLOT_APPENDIX,
        ORGAN_SLOT_TONGUE);
}

/*				Non c2 medicines 				*/

// tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:566
public partial class datum_reagent_impurity_mannitol : datum_reagent_impurity
{
    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:575
    public override async DmTask on_mob_add(mob_living affected_mob = null, object amount = null)
    {
        await base.on_mob_add(affected_mob, amount);
        if (!iscarbon(affected_mob))
            return;
        mob_living_carbon affected_carbon = (mob_living_carbon)affected_mob;
        if (affected_carbon.dna == null)
            return;
        DmList speech_options = Dm.list(
            typeof(datum_mutation_swedish),
            typeof(datum_mutation_unintelligible),
            typeof(datum_mutation_stoner),
            typeof(datum_mutation_medieval),
            typeof(datum_mutation_wacky),
            typeof(datum_mutation_piglatin),
            typeof(datum_mutation_nervousness),
            typeof(datum_mutation_mute));
        speech_options = shuffle(speech_options) as DmList;
        foreach (object option in Dm.each(speech_options))
        {
            if (Dm.truthy(affected_carbon.dna.get_mutation(option))) // tg passes MUTATION_SOURCE_MANNITOIL too, which get_mutation does not take
                continue;
            await affected_carbon.dna.add_mutation(option, MUTATION_SOURCE_MANNITOIL);
            speech_option = option;
            return;
        }
    }

    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:600
    public override async DmTask on_mob_delete(mob_living affected_mob = null)
    {
        await base.on_mob_delete(affected_mob);
        if (!iscarbon(affected_mob))
            return;
        mob_living_carbon carbon = (mob_living_carbon)affected_mob;
        if (carbon.dna != null)
            await carbon.dna.remove_mutation(speech_option, MUTATION_SOURCE_MANNITOIL);
    }
}

// tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:608
public partial class datum_reagent_inverse_neurine : datum_reagent_inverse
{
    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:616  proc-level var/static/list/traumalist (rules 10.6)
    private static ref DmList on_mob_life__traumalist => ref TgState.Current.datum_reagent_inverse_neurine__on_mob_life__traumalist;

    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:618
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        if (temp_trauma != null)
            return dot;
        if (!(SPT_PROB(creation_purity * 10, seconds_per_tick)))
            return dot;
        ref DmList traumalist = ref on_mob_life__traumalist;
        if (!Dm.truthy(traumalist))
        {
            traumalist = subtypesof(typeof(datum_brain_trauma));

            // Don't add these to the list because they're abstract category types
            DmList abstracttraumas = Dm.list(
                typeof(datum_brain_trauma_magic),
                typeof(datum_brain_trauma_mild),
                typeof(datum_brain_trauma_severe),
                typeof(datum_brain_trauma_special));

            // Don't give out these traumas or any of their descendants
            DmList forbiddentraumas = Dm.list(
                typeof(datum_brain_trauma_severe_split_personality), // Uses a ghost, I don't want to use a ghost for a temp thing
                typeof(datum_brain_trauma_special_imaginary_friend), // Same as above
                typeof(datum_brain_trauma_special_obsessed), // Obsessed sets the affected_mob as an antag - I presume this will lead to problems, so we'll remove it
                typeof(datum_brain_trauma_hypnosis), // Hypnosis, same reason as obsessed, plus a bug makes it remain even after the neruwhine purges and then turn into "nothing" on the med reading upon a second application
                typeof(datum_brain_trauma_severe_hypnotic_stupor), // These apply the above blacklisted trauma
                typeof(datum_brain_trauma_severe_hypnotic_trigger),
                typeof(datum_brain_trauma_special_honorbound), // Designed to be chaplain exclusive
                typeof(datum_brain_trauma_voided)); // Voidwalker exclusive and more of a magical status effect than a trauma

            // Do give out these traumas but not any of their subtypes, usually because the trauma replaces itself with a subtype
            DmList forbiddensubtypes = Dm.list(
                typeof(datum_brain_trauma_mild_phobia),
                typeof(datum_brain_trauma_severe_paralysis),
                typeof(datum_brain_trauma_special_psychotic_brawling));

            Dm.remove(ref traumalist, abstracttraumas);
            foreach (object forbidden in Dm.each(forbiddentraumas))
                Dm.remove(ref traumalist, Dm.typesof(forbidden));
            foreach (object forbidden in Dm.each(forbiddensubtypes))
                Dm.remove(ref traumalist, subtypesof(forbidden));
        }

        traumalist = shuffle(traumalist) as DmList;
        obj_item_organ_brain brain = mob.get_organ_slot(ORGAN_SLOT_BRAIN) as obj_item_organ_brain;
        foreach (object trauma in Dm.each(traumalist))
        {
            temp_trauma = await brain.brain_gain_trauma(trauma, TRAUMA_RESILIENCE_MAGIC) as datum_brain_trauma;
            if (temp_trauma != null)
                return dot;
        }
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:668
    public override async DmTask on_mob_delete(mob_living affected_mob = null)
    {
        await base.on_mob_delete(affected_mob);
        if (temp_trauma == null)
            return;
        if (temp_trauma is datum_brain_trauma_special_imaginary_friend) //Good friends stay by you, no matter what
            return;
        qdel(temp_trauma);
        temp_trauma = null;
    }
}

// tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:678
public partial class datum_reagent_inverse_corazargh : datum_reagent_inverse
{
    ///Give the victim the manual heart beating component.
    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:685  (tg calls the parent twice when
    // the component is added: kept)
    public override async DmTask on_mob_metabolize(mob_living affected_mob = null, params object[] __extra)
    {
        await base.on_mob_metabolize(affected_mob, __extra);
        if (!iscarbon(affected_mob))
            return;
        mob_living_carbon carbon_mob = (mob_living_carbon)affected_mob;
        obj_item_organ_heart affected_heart = carbon_mob.get_organ_slot(ORGAN_SLOT_HEART) as obj_item_organ_heart;
        if ((affected_heart == null))
            return;
        await carbon_mob.AddComponent(typeof(datum_component_manual_heart));
        await base.on_mob_metabolize(affected_mob, __extra);
    }

    ///We're done - remove the curse
    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:701
    public override async DmTask on_mob_end_metabolize(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        qdel(affected_mob.GetComponent(typeof(datum_component_manual_heart)));
        await base.on_mob_end_metabolize(affected_mob, metabolization_ratio);
    }
}

// tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:706
public partial class datum_reagent_inverse_antihol : datum_reagent_inverse
{
    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:715
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var C = (mob_living_carbon)affected_mob;
        foreach (datum_reagent_consumable_ethanol alcohol in Dm.each<datum_reagent_consumable_ethanol>(((datum_reagents)C.reagents).reagent_list))
            alcohol.boozepwr += 8 * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:721
public partial class datum_reagent_inverse_oculine : datum_reagent_inverse
{
    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:731  (with a headache tg calls the
    // parent a second time: kept)
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        if (Dm.truthy(headache))
            return await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        if (SPT_PROB(100 * creation_purity, seconds_per_tick))
        {
            await mob.become_blind(IMPURE_OCULINE);
            to_chat(mob, span_danger(I18n.T("You suddenly develop a pounding headache as your vision fluxuates.")));
            headache = TRUE;
        }
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:741
    public override async DmTask on_mob_end_metabolize(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.on_mob_end_metabolize(affected_mob, metabolization_ratio);
        affected_mob.cure_blind(IMPURE_OCULINE);
        if (Dm.truthy(headache))
            to_chat(affected_mob, span_notice(I18n.T("Your headache clears up!")));
    }
}

// tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:748
public partial class datum_reagent_impurity_inacusiate : datum_reagent_impurity
{
    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:759
    public override async DmTask on_mob_metabolize(mob_living affected_mob = null, params object[] __extra)
    {
        await base.on_mob_metabolize(affected_mob, __extra);
        random_span = pick("clown", "small", "big", "hypnophrase", "alien", "cult", "alert", "danger", "emote", "yell", "brass", "sans", "papyrus", "robot", "his_grace", "phobia");
        RegisterSignal(affected_mob, COMSIG_MOVABLE_HEAR, nameof(owner_hear));
        to_chat(affected_mob, span_warning(!HAS_TRAIT(affected_mob, TRAIT_DEAF) ? I18n.T("Your hearing seems to be a bit off!") : I18n.T("Your hearing seems to be a bit off - wait, that's normal.")));
    }

    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:765
    public override async DmTask on_mob_end_metabolize(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.on_mob_end_metabolize(affected_mob, metabolization_ratio);
        UnregisterSignal(affected_mob, COMSIG_MOVABLE_HEAR);
        to_chat(affected_mob, span_notice(!HAS_TRAIT(affected_mob, TRAIT_DEAF) ? I18n.T("You start hearing things normally again.") : I18n.T("You start hearing things normally again - no, wait, no you don't.")));
    }

    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:770
    [SignalHandler]
    public virtual void owner_hear(mob_living owner = null, DmList hearing_args = null)
    {
        // don't skip messages that the owner says or can't understand (since they still make sounds)
        if (HAS_TRAIT(owner, TRAIT_DEAF))
            return;
        // not technically hearing
        atom_movable speaker = hearing_args[HEARING_SPEAKER] as atom_movable;
        if (!(speaker == null) && HAS_TRAIT(speaker, TRAIT_SIGN_LANG))
            return;

        DmList spans = hearing_args[HEARING_SPANS] as DmList;
        object copied_spans = spans.Copy();
        Dm.combine(ref copied_spans, random_span);
        hearing_args[HEARING_SPANS] = copied_spans;
    }
}

// tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:801
public partial class datum_reagent_inverse_oxandrolone : datum_reagent_inverse
{
    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:809
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        object high_message = pick("You feel unstoppable.", "Giving it EVERYTHING!!", "You feel ready for anything.", "You feel like doing a thousand jumping jacks!");
        if (SPT_PROB(2, seconds_per_tick))
            to_chat(mob, span_notice(I18n.T(Dm.str(high_message))));
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:815
    public override async DmTask<object> overdose_process(mob_living affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.overdose_process(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon carbon = (mob_living_carbon)affected_mob;
        if (SPT_PROB(25, seconds_per_tick))
        {
            carbon.adjust_bodytemperature(15 * TEMPERATURE_DAMAGE_COEFFICIENT * Dm.num(metabolization_ratio));
            await carbon.set_jitter_if_lower(3 * SECONDS);
            await carbon.adjust_stamina_loss(2.5f * Dm.num(metabolization_ratio));
        }
        else if (SPT_PROB(5, seconds_per_tick))
        {
            await carbon.vomit(VOMIT_CATEGORY_BLOOD, lost_nutrition: 0, distance: 3);
            await carbon.Paralyze(3 * SECONDS);
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:836
public partial class datum_reagent_inverse_pen_acid : datum_reagent_inverse
{
    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:844
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        await holder.remove_reagent(typeof(datum_reagent_medicine_c2_seiver), 40 * k);
        await holder.remove_reagent(typeof(datum_reagent_medicine_potass_iodide), 40 * k);
        await holder.remove_reagent(typeof(datum_reagent_medicine_c2_multiver), 40 * k);

        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        if (HAS_TRAIT(mob, TRAIT_IRRADIATED))
        {
            await mob.set_jitter_if_lower(10 * SECONDS);
            mob.adjust_disgust(2.4f * k);
            if (SPT_PROB(2.5f, seconds_per_tick))
            {
                to_chat(mob, span_warning(I18n.T("A horrible ache spreads in your insides!")));
                await mob.adjust_confusion_up_to(10 * SECONDS, 15 * SECONDS);
            }
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:858
public partial class datum_reagent_inverse_atropine : datum_reagent_inverse
{
    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:866
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        float need_mob_update;
        need_mob_update = Dm.num(await mob.adjust_organ_loss(ORGAN_SLOT_STOMACH, -1 * k));
        need_mob_update += Dm.num(await mob.adjust_organ_loss(ORGAN_SLOT_HEART, -1 * k));
        if (Dm.num(mob.get_tox_loss()) <= 25)
            need_mob_update = Dm.num(await mob.adjust_tox_loss(-0.5f, updating_health: FALSE, required_biotype: affected_biotype));
        if (Dm.truthy(need_mob_update))
            return UPDATE_MOB_HEALTH;
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:876
    public override async DmTask<object> overdose_process(mob_living affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.overdose_process(affected_mob, seconds_per_tick, metabolization_ratio);
        await affected_mob.adjust_organ_loss(pick(overdose_process__possible_organs), 2 * Dm.num(seconds_per_tick));
        await ((datum_reagents)affected_mob.reagents).remove_reagent(type, 1 * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:876  proc-level var/static/list/possible_organs (rules 10.6)
    private static ref DmList overdose_process__possible_organs => ref TgState.Current.datum_reagent_inverse_atropine__overdose_process__possible_organs;
}

// tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:893
public partial class datum_reagent_inverse_ammoniated_mercury : datum_reagent_inverse
{
    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:899
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        if (SPT_PROB(7.5f, seconds_per_tick))
        {
            await mob.emote("scream");
            await mob.say(pick("AAAAAAAHHHHH!!", "OOOOH NOOOOOO!!", "GGGUUUUHHHHH!!", "AIIIIIEEEEEE!!", "HAHAHAHAHAAAAAA!!", "OORRRGGGHHH!!", "AAAAAAAJJJJJJJJJ!!"), forced: type);
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:906
public partial class datum_reagent_inverse_rezadone : datum_reagent_inverse
{
    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:913
    public override async DmTask on_mob_metabolize(mob_living affected_mob = null, params object[] __extra)
    {
        await base.on_mob_metabolize(affected_mob, __extra);
        await ((mob_living_carbon)affected_mob).gain_trauma(typeof(datum_brain_trauma_mild_phobia_fish), TRAUMA_RESILIENCE_ABSOLUTE);
    }

    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:917
    public override async DmTask on_mob_end_metabolize(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.on_mob_end_metabolize(affected_mob, metabolization_ratio);
        ((mob_living_carbon)affected_mob).cure_trauma_type(typeof(datum_brain_trauma_mild_phobia_fish), resilience: TRAUMA_RESILIENCE_ABSOLUTE);
    }
}

// tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:932
public partial class datum_reagent_inverse_krokodil : datum_reagent_inverse
{
    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:942
    public override async DmTask expose_mob(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra)
    {
        methods ??= TOUCH;
        await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);
        if (!Dm.truthy(Dm.bitand(methods, Dm.bitor(Dm.bitor(TOUCH, VAPOR), PATCH))))
            return;

        exposed_mob.add_surgery_speed_mod(type, 0.7f, min(Dm.num(reac_volume) * 1 * MINUTES, 5 * MINUTES));
    }

    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:949
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        await mob.add_mood_event("smacked out", typeof(datum_mood_event_narcotic_heavy));
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:953
    public override async DmTask<object> overdose_process(mob_living affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.overdose_process(affected_mob, seconds_per_tick, metabolization_ratio);
        await affected_mob.set_jitter_if_lower(10 * SECONDS);
        await affected_mob.set_dizzy_if_lower(5 * SECONDS);
        if (SPT_PROB(10, seconds_per_tick))
            await affected_mob.emote("drool");
        if (SPT_PROB(5, seconds_per_tick))
        {
            to_chat(affected_mob, span_warning(I18n.T("You briefly lose control of your legs!")));
            await affected_mob.Knockdown(5 * SECONDS);
        }
        if (SPT_PROB(5, seconds_per_tick))
        {
            to_chat(affected_mob, span_warning(I18n.T("The muscles in your arms give out!")));
            await affected_mob.drop_all_held_items();
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:967
public partial class datum_reagent_inverse_bath_salts : datum_reagent_inverse
{
    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:976
    public override async DmTask on_mob_metabolize(mob_living affected_mob = null, params object[] __extra)
    {
        await base.on_mob_metabolize(affected_mob, __extra);
        if (HAS_TRAIT(affected_mob, TRAIT_SIMIAN))
        {
            await ((mob_living_carbon)affected_mob).gain_trauma(typeof(datum_brain_trauma_special_primal_instincts), TRAUMA_RESILIENCE_ABSOLUTE);
            affected_mob.add_traits(Dm.list(TRAIT_STUNIMMUNE, TRAIT_SLEEPIMMUNE, TRAIT_ANALGESIA, TRAIT_STIMULATED), type);
            if (jungle_arts != null)
                return;
            jungle_arts = await Dm.new_async<datum_martial_art_jungle_arts>(this);
            jungle_arts.locked_to_use = TRUE;
            await jungle_arts.teach(affected_mob);
        }
    }

    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:987
    public override async DmTask on_mob_end_metabolize(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.on_mob_end_metabolize(affected_mob, metabolization_ratio);
        QDEL_NULL(ref jungle_arts);
        affected_mob.remove_traits(Dm.list(TRAIT_STUNIMMUNE, TRAIT_SLEEPIMMUNE, TRAIT_ANALGESIA, TRAIT_STIMULATED), type);
        await affected_mob.Sleeping(30 * SECONDS);
        if (HAS_TRAIT(affected_mob, TRAIT_SIMIAN))
            ((mob_living_carbon)affected_mob).cure_trauma_type(typeof(datum_brain_trauma_special_primal_instincts), resilience: TRAUMA_RESILIENCE_ABSOLUTE);
    }

    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:996
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        if (Dm.truthy(holder.has_reagent(typeof(datum_reagent_drug_bath_salts))))
        {
            await holder.remove_reagent(type, volume);
            return dot;
        }

        if (HAS_TRAIT(mob, TRAIT_SIMIAN))
        {
            float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
            float need_mob_update;
            need_mob_update = Dm.num(await mob.adjust_organ_loss(ORGAN_SLOT_BRAIN, 5 * k, required_organ_flag: affected_organ_flags));

            if (Dm.truthy(holder.has_reagent(typeof(datum_reagent_consumable_monkey_energy))))
                need_mob_update += Dm.num(await mob.adjust_organ_loss(ORGAN_SLOT_BRAIN, 5 * k, required_organ_flag: affected_organ_flags));

            if (Dm.truthy(need_mob_update))
                dot = UPDATE_MOB_HEALTH;
            return dot;
        }

        if (SPT_PROB(10, seconds_per_tick))
            await mob.emote(pick("screech", "scratch", "jump", "look"));

        QDEL_NULL(ref jungle_arts);
        mob.remove_traits(Dm.list(TRAIT_STUNIMMUNE, TRAIT_SLEEPIMMUNE, TRAIT_ANALGESIA, TRAIT_STIMULATED), type);
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:1020
public partial class datum_reagent_inverse_aranesp : datum_reagent_inverse
{
    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:1029
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        if (Dm.truthy(overdosed))
            return dot;

        float mr = Dm.num(metabolization_ratio);
        float k = mr * Dm.num(seconds_per_tick);
        await mob.coagulant_effect(0.08f * k);
        mob.adjust_blood_volume(1.6f * k, maximum: BLOOD_VOLUME_NORMAL);
        await mob.adjust_organ_loss(ORGAN_SLOT_HEART, 0.16f * k);

        float sw__1 = current_cycle;
        if (sw__1 == 10)
        {
            to_chat(mob, span_warning(I18n.T("You feel a migraine coming on...")));
            await mob.adjust_eye_blur(0.16f * SECONDS * k);
        }
        else if (Dm.in_to(sw__1, 15, 30))
        {
            if (SPT_PROB(5, seconds_per_tick))
            {
                to_chat(mob, span_warning(I18n.T("Your head aches as your vision blurs.")));
                await mob.adjust_eye_blur(4 * SECONDS * mr);
            }
            if (SPT_PROB(5, seconds_per_tick))
            {
                to_chat(mob, span_warning(I18n.T("Your face contorts as a sudden pain forms in your head.")));
                await mob.Stun(1 * SECONDS);
            }
        }
        else if (Dm.in_to(sw__1, 31, 45))
        {
            if (SPT_PROB(5, seconds_per_tick))
            {
                to_chat(mob, span_warning(I18n.T("Intense pressure forms in your head, you can barely see!")));
                await mob.adjust_eye_blur(8 * SECONDS * mr);
                await mob.adjust_confusion_up_to(5 * SECONDS, 20 * SECONDS);
                await mob.adjust_hallucinations(10 * SECONDS);
            }
            if (SPT_PROB(5, seconds_per_tick))
            {
                to_chat(mob, span_warning(I18n.T("You lose focus and stare ahead.")));
                await mob.Stun(3 * SECONDS);
                await mob.emote(pick("stare", "drool", "moan", "look"));
            }
            if (SPT_PROB(5, seconds_per_tick))
            {
                to_chat(mob, span_warning(I18n.T("Your inhaling becomes more stressed, it's getting harder to breathe!")));
                await holder.add_reagent(typeof(datum_reagent_toxin_histamine), 3.2f * mr);
            }
        }
        else if (Dm.in_to(sw__1, 46, INFINITY))
        {
            if (SPT_PROB(5, seconds_per_tick))
            {
                to_chat(mob, span_warning(I18n.T("It feels like your head is going to implode!")));
                await mob.adjust_eye_blur(8 * SECONDS * mr);
                await mob.adjust_confusion_up_to(10 * SECONDS, 20 * SECONDS);
                await mob.adjust_hallucinations(30 * SECONDS);
            }
            if (SPT_PROB(5, seconds_per_tick))
            {
                to_chat(mob, span_warning(I18n.T("You can't bring yourself to focus at all!")));
                await mob.Stun(5 * SECONDS);
                await mob.emote(pick("stare", "drool", "tremble", "shake"));
            }
            if (SPT_PROB(5, seconds_per_tick))
            {
                to_chat(mob, span_warning(I18n.T("Your breathing becomes weak and raspy, you can barely stay conscious!")));
                await holder.add_reagent(typeof(datum_reagent_toxin_histamine), 4.8f * mr);
                mob.losebreath += 3;
            }
        }
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:1080
    public override async DmTask<object> overdose_process(mob_living affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.overdose_process(affected_mob, seconds_per_tick, metabolization_ratio);
        float mr = Dm.num(metabolization_ratio);
        float need_mob_update = 0;
        if (SPT_PROB(10, seconds_per_tick))
        {
            to_chat(affected_mob, span_warning(I18n.T("It feels like your head is going to implode!")));
            await affected_mob.adjust_eye_blur(8 * SECONDS * mr);
            await affected_mob.adjust_confusion_up_to(10 * SECONDS, 20 * SECONDS);
            await affected_mob.adjust_hallucinations(30 * SECONDS);
        }
        if (SPT_PROB(10, seconds_per_tick))
        {
            to_chat(affected_mob, span_warning(I18n.T("You can't bring yourself to focus at all!")));
            await affected_mob.Stun(50);
            await affected_mob.emote(pick("stare", "drool", "tremble", "shake"));
        }
        if (SPT_PROB(10, seconds_per_tick))
        {
            to_chat(affected_mob, span_warning(I18n.T("Your breathing becomes weak and raspy, you can barely stay conscious!")));
            await holder.add_reagent(typeof(datum_reagent_toxin_histamine), 4.8f * mr);
            affected_mob.losebreath += 3;
            need_mob_update = TRUE;
        }

        if (Dm.truthy(need_mob_update))
            return UPDATE_MOB_HEALTH;
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:1102
public partial class datum_reagent_inverse_happiness : datum_reagent_inverse
{
    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:1110
    public override async DmTask on_mob_metabolize(mob_living affected_mob = null, params object[] __extra)
    {
        await base.on_mob_metabolize(affected_mob, __extra);
        await affected_mob.add_mood_event("sadness_inverse", typeof(datum_mood_event_sadness_inverse));
        if (Dm.in_to(Dm.num(volume), 1, INFINITY)) // prevents microdosing from repeating this line
            await affected_mob.say("What?? No... NO...", forced: type);
    }

    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:1117
    public override async DmTask on_mob_end_metabolize(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.on_mob_end_metabolize(affected_mob, metabolization_ratio);
        affected_mob.clear_mood_event("sadness_inverse");
    }

    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:1121
    public override async DmTask expose_mob(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra)
    {
        methods ??= TOUCH;
        await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);
        if (Dm.truthy(Dm.bitand(methods, Dm.bitor(TOUCH, VAPOR))))
        {
            await exposed_mob.emote("cry");
            return;
        }
    }

    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:1127
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        if (Dm.truthy(mob.mob_mood?.has_mood_of_category("friendly_hug")) && !HAS_TRAIT(mob, TRAIT_BADTOUCH))
        {
            await holder.remove_reagent(type, volume);
            return dot;
        }

        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        await holder.remove_reagent(typeof(datum_reagent_drug_happiness), 10 * k);
        await holder.remove_reagent(typeof(datum_reagent_medicine_psicodine), 10 * k);

        if (mob.mob_mood == null)
            return dot; // tg reads mob_mood unguarded: a moodless carbon's runtime ends on_mob_life here (kept, without the runtime, DV1)
        await mob.mob_mood.adjust_sanity(-13 * k, minimum: SANITY_INSANE);
        if (mob.mob_mood != null && mob.mob_mood.sanity < (SANITY_CRAZY))
        {
            await mob.adjust_drowsiness_up_to(5 * SECONDS, 30 * SECONDS);
            if (SPT_PROB(25, seconds_per_tick))
                await mob.emote(pick("cry", "frown", "pout", "whimper", "sigh"));
            if (SPT_PROB(3, seconds_per_tick))
                await mob.say(pick("Why are we still here? Just to suffer?", "To live is to suffer!", "Life is suffering. It is the nature of existence!", "Each day we wake up is another day closer to death.", "Sometimes I can hear my bones straining under the weight of all the lives I'm not living.", "There are no beautiful surfaces without a terrible depth."), forced: type);
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:1145
public partial class datum_reagent_inverse_baldium : datum_reagent_inverse
{
    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:1152
    public override async DmTask on_mob_metabolize(mob_living affected_mob = null, params object[] __extra)
    {
        await base.on_mob_metabolize(affected_mob, __extra);
        affected_mob.add_shared_particles(typeof(particles_smoke_steam_mald));
        affected_mob.manual_emote("inhales sharply.");
        to_chat(affected_mob, span_warning(I18n.T("You can't help but find everything more irritating.")));
    }

    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:1158
    public override async DmTask on_mob_end_metabolize(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.on_mob_end_metabolize(affected_mob, metabolization_ratio);
        await affected_mob.remove_atom_colour(TEMPORARY_COLOUR_PRIORITY, "#fe0000");
        affected_mob.remove_shared_particles(typeof(particles_smoke_steam_mald));
        affected_mob.manual_emote("exhales sharply.");
        to_chat(affected_mob, span_warning(I18n.T("You feel an intense burning sensation as your anger subsides!")));
    }

    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:1165
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        delayed_burn_damage += (Dm.num(seconds_per_tick) * 1);
        if (Dm.truthy(holder.has_reagent(typeof(datum_reagent_consumable_salt))))
        {
            float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
            await holder.remove_reagent(typeof(datum_reagent_inverse_baldium), 1.5f * k);
            await holder.remove_reagent(typeof(datum_reagent_consumable_salt), 0.5f * k);
        }

        float sw__1 = current_cycle;
        if (sw__1 == 5)
        {
            await mob.add_atom_colour("#fe0000", TEMPORARY_COLOUR_PRIORITY);
            if (!HAS_TRAIT(mob, TRAIT_PACIFISM))
            {
                turf angery_blast = get_turf(mob);
                await goonchem_vortex(angery_blast, 1, 4);
                to_chat(mob, span_warning(I18n.T("You can't control yourself as you yell out ANGRILY!!")));
                await mob.emote("scream");
                await mob.say(pick("RAGE!! UNLEASH THE RAGE!!", "I'M SO ANGRY!!", "WHY I OUGHTA...", "AAARRRGGHH!!"), forced: type);
            }
            else
            {
                to_chat(mob, span_warning(I18n.T("You calmly yet firmly state your discontent.")));
                await mob.say(pick("Aaahhhh...", "Rage, woo yeah...", "I'm slightly upset..."), forced: type);
            }
        }
        else if (Dm.in_to(sw__1, 6, INFINITY))
        {
            if (SPT_PROB(10, seconds_per_tick))
                mob.manual_emote(pick("breathes rapidly!", "huffs, and puffs...", "stares MENACINGLY!", "sighs AGGRESSIVELY!", "sheds a tear ANGRILY!"));
        }
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:1189
    public override async DmTask on_mob_delete(mob_living affected_mob = null)
    {
        await base.on_mob_delete(affected_mob);
        affected_mob.log_message(Dm.text("has taken [] burn damage from maldium's aftereffects", delayed_burn_damage), LOG_ATTACK);
        await affected_mob.adjust_fire_loss(delayed_burn_damage, updating_health: TRUE, required_bodytype: affected_bodytype);
    }
}

// tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:1195
public partial class datum_reagent_inverse_colorful_reagent : datum_reagent_inverse
{
    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:1212
    public override async DmTask New(params object[] __extra)
    {
        color_callback = Dm.@new<datum_callback>(this, nameof(UpdateColor));
        SSticker.OnRoundstart(color_callback);
        await base.New(__extra);
    }

    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:1217
    public override object Destroy(float? force = null)
    {
        LAZYREMOVE(ref SSticker.round_end_events, color_callback);
        color_callback = null;
        return base.Destroy(force);
    }

    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:1222
    public override async DmTask overdose_start(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.overdose_start(affected_mob, metabolization_ratio);
        metabolization_rate = 0.1f * REAGENTS_METABOLISM;
    }

    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:1226
    public override async DmTask on_mob_metabolize(mob_living affected_mob = null, params object[] __extra)
    {
        await base.on_mob_metabolize(affected_mob, __extra);
        await ((mob_living_carbon)affected_mob).gain_trauma(typeof(datum_brain_trauma_mild_color_blindness), TRAUMA_RESILIENCE_ABSOLUTE);
    }

    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:1230
    public override async DmTask on_mob_end_metabolize(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.on_mob_end_metabolize(affected_mob, metabolization_ratio);
        ((mob_living_carbon)affected_mob).cure_trauma_type(typeof(datum_brain_trauma_mild_color_blindness), resilience: TRAUMA_RESILIENCE_ABSOLUTE);
    }

    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:1234
    public virtual void UpdateColor()
    {
        color_callback = null;
        color = Dm.str(pick(random_color_list));
    }

    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:1238
    public override async DmTask expose_mob(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra)
    {
        await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);
        object picked_color = pick(random_color_list);
        object color_filter = color_transition_filter(picked_color, SATURATION_OVERRIDE);
        if (Dm.truthy(can_color_clothing) && Dm.truthy(Dm.bitand(methods, Dm.bitor(Dm.bitor(TOUCH, VAPOR), INHALE))))
        {
            float include_flags = Dm.bitor(Dm.bitor(Dm.bitor(INCLUDE_HELD, INCLUDE_ACCESSORIES), INCLUDE_PROSTHETICS), INCLUDE_ABSTRACT);
            if (Dm.truthy(Dm.bitand(methods, Dm.bitor(VAPOR, INHALE))) || Dm.num(touch_protection) >= 1)
                include_flags = Dm.bitor(include_flags, INCLUDE_POCKETS);
            foreach (obj_item to_color in Dm.each<obj_item>(exposed_mob.get_equipped_items(include_flags)))
                await to_color.add_atom_colour(color_filter, WASHABLE_COLOUR_PRIORITY);
        }

        if (ishuman(exposed_mob))
        {
            mob_living_carbon_human exposed_human = (mob_living_carbon_human)exposed_mob;
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

        mob_living_carbon exposed_carbon = (mob_living_carbon)exposed_mob;

        foreach (obj_item_organ organ in Dm.each_cast<obj_item_organ>(exposed_carbon.organs))
            await organ.add_atom_colour(color_filter, WASHABLE_COLOUR_PRIORITY);

        foreach (obj_item_bodypart part in Dm.each_cast<obj_item_bodypart>(exposed_carbon.get_bodyparts()))
            await part.add_atom_colour(color_filter, WASHABLE_COLOUR_PRIORITY);
    }

    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:1269
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = affected_mob as mob_living;

        if (!iscarbon(mob))
        {
            if (Dm.truthy(can_color_mobs))
                await mob.add_atom_colour(color_transition_filter(pick(random_color_list), SATURATION_OVERRIDE), WASHABLE_COLOUR_PRIORITY);
            return dot;
        }

        if (!Dm.truthy(can_color_organs))
            return dot;

        mob_living_carbon carbon_mob = (mob_living_carbon)mob;
        float color_priority = WASHABLE_COLOUR_PRIORITY;
        if (current_cycle >= 30)
            color_priority = FIXED_COLOUR_PRIORITY;

        foreach (obj_item_organ organ in Dm.each_cast<obj_item_organ>(carbon_mob.organs))
            await organ.add_atom_colour(color_transition_filter(pick(random_color_list), SATURATION_OVERRIDE), color_priority);
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:1288
    public override async DmTask<object> expose_atom(atom exposed_atom = null, object reac_volume = null, float? methods = null)
    {
        object dot = await base.expose_atom(exposed_atom, reac_volume, methods);
        if (!isliving(exposed_atom))
            await exposed_atom.add_atom_colour(color_transition_filter(pick(random_color_list), SATURATION_OVERRIDE), WASHABLE_COLOUR_PRIORITY);
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:1294
public partial class datum_reagent_inverse_gravitum : datum_reagent_inverse
{
    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:1302
    public override async DmTask on_mob_metabolize(mob_living affected_mob = null, params object[] __extra)
    {
        await base.on_mob_metabolize(affected_mob, __extra);
        await affected_mob.AddElement(typeof(datum_element_forced_gravity), Dm.named("gravity", 5), Dm.named("ignore_turf_gravity", TRUE), Dm.named("can_override", FALSE));
    }

    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:1306
    public override async DmTask on_mob_end_metabolize(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.on_mob_end_metabolize(affected_mob, metabolization_ratio);
        affected_mob.RemoveElement(typeof(datum_element_forced_gravity), Dm.named("gravity", 5), Dm.named("ignore_turf_gravity", TRUE), Dm.named("can_override", FALSE));
    }

    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:1310
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;

        float sw__1 = current_cycle;
        if (sw__1 == 10)
        {
            foreach (obj_item_bodypart_leg leg in Dm.each<obj_item_bodypart_leg>(mob.get_bodyparts()))
                await mob.cause_wound_of_type_and_severity(WOUND_BLUNT, leg, WOUND_SEVERITY_MODERATE);
            to_chat(mob, span_warning(I18n.T("Your legs start to cave in to your overwhelming gravity!")));
        }
        else if (sw__1 == 20)
        {
            foreach (obj_item_bodypart_leg leg in Dm.each<obj_item_bodypart_leg>(mob.get_bodyparts()))
                await mob.cause_wound_of_type_and_severity(WOUND_BLUNT, leg, WOUND_SEVERITY_SEVERE);
            to_chat(mob, span_warning(I18n.T("Your bones fragment horribly as the gravity pounds on you!")));
        }
        else if (sw__1 == 30)
        {
            foreach (obj_item_bodypart_leg leg in Dm.each<obj_item_bodypart_leg>(mob.get_bodyparts()))
                await mob.cause_wound_of_type_and_severity(WOUND_BLUNT, leg, WOUND_SEVERITY_CRITICAL);
            to_chat(mob, span_warning(I18n.T("The gravity of this situation makes your bones snap like popsicle sticks!")));
        }
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_medicine_reagents.dm:1329
    public override async DmTask overdose_start(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.overdose_start(affected_mob, metabolization_ratio);
        await affected_mob.AddElement(typeof(datum_element_squish), 120 * SECONDS);
        mob_living_carbon carbon = (mob_living_carbon)affected_mob;
        foreach (obj_item_bodypart_leg leg in Dm.each<obj_item_bodypart_leg>(carbon.get_bodyparts()))
            await carbon.cause_wound_of_type_and_severity(WOUND_SLASH, leg, WOUND_SEVERITY_SEVERE);
    }
}
