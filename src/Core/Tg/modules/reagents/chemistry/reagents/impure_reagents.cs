// Translated from tgstation (snapshot 1d99bd7f): code/modules/reagents/chemistry/reagents/impure_reagents.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps RG2, D339): the impurity (liver damage) and inverse (toxin damage) bases, eigenswap's swapped hands and
// cryogelidia's ice block (the freeze reagent effect). The var blocks are generated (D111).

//Reagents produced by metabolising/reacting fermichems suboptimally, i.e. inverse_chems or impure_chems
//Inverse = Splitting
//Invert = Whole conversion

//Causes slight liver damage, and that's it.
// tg: code/modules/reagents/chemistry/reagents/impure_reagents.dm:7
public partial class datum_reagent_impurity : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/impure_reagents.dm:17
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        obj_item_organ_liver liver = mob.get_organ_slot(ORGAN_SLOT_LIVER) as obj_item_organ_liver;
        float need_mob_update;

        if (liver != null) //Though, lets be safe
            need_mob_update = Dm.num(await mob.adjust_organ_loss(ORGAN_SLOT_LIVER, METABOLIZE_FREE_CONSTANT(0.5f) * liver_damage * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), required_organ_flag: affected_organ_flags));
        else
            need_mob_update = Dm.num(await mob.adjust_tox_loss(1 * liver_damage * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), updating_health: FALSE, required_biotype: affected_biotype)); //Incase of no liver!

        if (Dm.truthy(need_mob_update))
            return UPDATE_MOB_HEALTH;
        return dot;
    }
}

//Basically just so people don't forget to adjust metabolization_rate
// tg: code/modules/reagents/chemistry/reagents/impure_reagents.dm:32
public partial class datum_reagent_inverse : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/impure_reagents.dm:41
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;

        if (Dm.truthy(await mob.adjust_tox_loss(METABOLIZE_FREE_CONSTANT(0.5f) * tox_damage * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), updating_health: FALSE, required_biotype: affected_biotype)))
            return UPDATE_MOB_HEALTH;
        return dot;
    }
}

// Unique

// tg: code/modules/reagents/chemistry/reagents/impure_reagents.dm:64
public partial class datum_reagent_inverse_eigenswap : datum_reagent_inverse
{
    // tg: code/modules/reagents/chemistry/reagents/impure_reagents.dm:72  (tg names the second parameter metabolization_ratio,
    // though it is given seconds_per_tick; the proc reads neither)
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        if (!prob(creation_purity * 100))
            return dot;
        DmList cached_hand_items = mob.held_items;
        float index = 1;
        foreach (object thing in Dm.each(cached_hand_items))
        {
            index++;
            if (index > Dm.length(cached_hand_items)) //If we're past the end of the list, go back to start
                index = 1;
            if (!Dm.truthy(thing))
                continue;
            await mob.put_in_hand(thing as obj_item, index, forced: TRUE, ignore_anim: TRUE);
            playsound(mob, Dm.resource("sound/effects/phasein.ogg"), 20, TRUE);
        }
        return dot;
    }
}

/*
* Freezes the player in a block of ice, 1s = 1u
* Will be removed when the required reagent is removed too
* Does not work via INGEST method (pills, drinking)
* is processed on the dead.
*/

// tg: code/modules/reagents/chemistry/reagents/impure_reagents.dm:92
public partial class datum_reagent_inverse_cryostylane : datum_reagent_inverse
{
    // tg: code/modules/reagents/chemistry/reagents/impure_reagents.dm:100
    public override async DmTask expose_mob(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra)
    {
        mob_living human_thing = exposed_mob;
        await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);
        if (Dm.truthy(Dm.bitand(methods, INGEST)) || !ishuman(human_thing))
            return;

        if (HAS_TRAIT(human_thing, TRAIT_RESISTCOLD))
        {
            await holder.del_reagent(type);
            return;
        }

        await human_thing.apply_status_effect(typeof(datum_status_effect_reagent_effect_freeze), type);
    }

    // tg: code/modules/reagents/chemistry/reagents/impure_reagents.dm:111
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        metabolization_rate += 0.05f * REAGENTS_METABOLISM; //speed up our metabolism over time. Chop chop.
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/impure_reagents.dm:115
    public override async DmTask metabolize_reagent(mob_living_carbon affected_mob = null, object seconds_per_tick = null, object metabolized_volume = null)
    {
        if (current_cycle >= 60)
        {
            volume = 0; // remove it all if we're past 60 cycles
            await holder.update_total();
            return;
        }

        await base.metabolize_reagent(affected_mob, seconds_per_tick, metabolized_volume);
    }

    // tg: code/modules/reagents/chemistry/reagents/impure_reagents.dm:123
    public override async DmTask on_mob_end_metabolize(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.on_mob_end_metabolize(affected_mob, metabolization_ratio);
        affected_mob.remove_status_effect(typeof(datum_status_effect_reagent_effect_freeze));
    }
}
