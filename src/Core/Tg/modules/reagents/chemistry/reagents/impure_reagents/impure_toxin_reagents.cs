// Translated from tgstation (snapshot 1d99bd7f): code/modules/reagents/chemistry/reagents/impure_reagents/impure_toxin_reagents.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps RG2, D339): the impure toxins (ipecacide, methanol, chloralax, rosenol). The var blocks are generated
// (D111).

//Reagents produced by metabolising/reacting fermichems suboptimally these specifically are for toxins
//Inverse = Splitting
//Invert = Whole conversion
//Failed = End reaction below purity_min

////////////////////TOXINS///////////////////////////

//Lipolicide - Impure Version
// tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_toxin_reagents.dm:10
public partial class datum_reagent_impurity_ipecacide : datum_reagent_impurity
{
    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_toxin_reagents.dm:15
    public override async DmTask on_mob_add(mob_living affected_mob = null, object amount = null)
    {
        mob_living owner = affected_mob;
        if (((mob_living_carbon)owner).disgust >= DISGUST_LEVEL_GROSS)
        {
            await base.on_mob_add(affected_mob, amount);
            return;
        }
        owner.adjust_disgust(50);
        await base.on_mob_add(affected_mob, amount);
    }
}

//Formaldehyde - Impure Version
// tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_toxin_reagents.dm:23
public partial class datum_reagent_impurity_methanol : datum_reagent_impurity
{
    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_toxin_reagents.dm:29
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        obj_item_organ_eyes eyes = mob.get_organ_slot(ORGAN_SLOT_EYES) as obj_item_organ_eyes;
        if (eyes != null && Dm.truthy(await eyes.apply_organ_damage(0.25f * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), required_organ_flag: affected_organ_flags)))
            return UPDATE_MOB_HEALTH;
        return dot;
    }
}

//Chloral Hydrate - Impure Version
// tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_toxin_reagents.dm:37
public partial class datum_reagent_impurity_chloralax : datum_reagent_impurity
{
    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_toxin_reagents.dm:43
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var owner = (mob_living_carbon)affected_mob;
        if (Dm.truthy(await owner.adjust_tox_loss(0.5f * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), updating_health: FALSE, required_biotype: affected_biotype)))
            return UPDATE_MOB_HEALTH;
        return dot;
    }
}

//Mindbreaker Toxin - Impure Version
// tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_toxin_reagents.dm:50
public partial class datum_reagent_impurity_rosenol : datum_reagent_impurity
{
    // tg: code/modules/reagents/chemistry/reagents/impure_reagents/impure_toxin_reagents.dm:57
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var owner = (mob_living_carbon)affected_mob;
        obj_item_organ_tongue tongue = owner.get_organ_slot(ORGAN_SLOT_TONGUE) as obj_item_organ_tongue;
        if (tongue == null)
            return dot;
        if (SPT_PROB(4.0f, seconds_per_tick))
        {
            owner.manual_emote(Dm.text("clicks with [] tongue.", owner.p_their()));
            await owner.say("Noice.", forced: typeof(datum_reagent_impurity_rosenol));
        }
        if (SPT_PROB(2.0f, seconds_per_tick))
            await owner.say(pick("Ah! That was a mistake!", "Horrible.", "Watch out everybody, the potato is really hot.", "When I was six I ate a bag of plums.", "And if there is one thing I can't stand it's tomatoes.", "And if there is one thing I love it's tomatoes.", "We had a captain who was so strict, you weren't allowed to breathe in their station.", "The unrobust ones just used to keel over and die, you'd hear them going down behind you."), forced: typeof(datum_reagent_impurity_rosenol));
        return dot;
    }
}
