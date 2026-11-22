// Translated from tgstation (snapshot 1d99bd7f): code/datums/diseases/advance/symptoms/fire.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps VI1, D341): spontaneous combustion and alkali perspiration. The var blocks are generated (D111). The flames
// are the living's fire (its overlay and sounds).

// tg: code/datums/diseases/advance/symptoms/fire.dm:10
public partial class datum_symptom_fire : datum_symptom
{
    // tg: code/datums/diseases/advance/symptoms/fire.dm:32
    [DmParams("A")]
    public override object Start(datum_disease_advance A = null)
    {
        object dot = null;
        dot = base.Start(A);
        if (!Dm.truthy(dot))
            return dot;
        if (Dm.num(A.totalStageSpeed()) >= 4)
            power = 1.5f;
        if (Dm.num(A.totalStageSpeed()) >= 8)
            power = 2;
        if (Dm.num(A.totalStealth()) >= 4)
            suppress_warning = TRUE;
        if (Dm.num(A.totalTransmittable()) >= 8) //burning skin spreads the virus through smoke
            infective = TRUE;
        return dot;
    }

    // tg: code/datums/diseases/advance/symptoms/fire.dm:45
    [DmParams("A")]
    public override async DmTask<object> Activate(datum_disease_advance A = null)
    {
        object dot = null;
        dot = await base.Activate(A);
        if (!Dm.truthy(dot))
            return dot;
        mob_living living_mob = A.affected_mob;
        if (Dm.in_to(A.stage, 1, 2))
            return dot;
        else if (A.stage == 3)
        {
            if (prob(base_message_chance) && !Dm.truthy(suppress_warning))
                warn_mob(living_mob);
        }
        else
        {
            bool advanced_stage = A.stage > 4;
            await living_mob.adjust_fire_stacks((advanced_stage ? 3 : 1) * Dm.num(power));
            await living_mob.take_overall_damage(burn: ((advanced_stage ? 5 : 3) * Dm.num(power)), required_bodytype: BODYTYPE_ORGANIC);
            living_mob.ignite_mob(silent: TRUE);
            if (Dm.truthy(living_mob.on_fire)) //check to make sure they actually caught on fire, or if it was prevented cause they were wet.
            {
                living_mob.visible_message(span_warning(I18n.T("[] catches fire!", living_mob)), ignored_mobs: living_mob);
                to_chat(living_mob, span_userdanger((advanced_stage ? I18n.T("Your skin erupts into an inferno!") : I18n.T("Your skin bursts into flames!"))));
                await living_mob.emote("scream");
            }
            else if (!Dm.truthy(suppress_warning))
                warn_mob(living_mob);
            if (Dm.truthy(infective))
                await A.airborne_spread(advanced_stage ? 4 : 2);
        }
        return dot;
    }

    // tg: code/datums/diseases/advance/symptoms/fire.dm:71
    public virtual void warn_mob(mob_living living_mob = null)
    {
        if (prob(33.33f))
            living_mob.show_message(span_hear(I18n.T("You hear a crackling noise.")), type: MSG_AUDIBLE);
        else
        {
            if (HAS_TRAIT(living_mob, TRAIT_ANOSMIA)) //Anosmia quirk holder can't smell anything.
                to_chat(living_mob, span_warning(I18n.T("You feel hot.")));
            else
                to_chat(living_mob, span_warning(I18n.T(Dm.str(pick(Dm.list("You feel hot.", "You smell smoke."))))));
        }
    }
}

/*
Alkali perspiration
	Hidden.
	Lowers resistance.
	Decreases stage speed.
	Decreases transmissibility.
	Fatal Level.

Bonus
	Ignites infected mob.
	Explodes mob on contact with water.
*/
// tg: code/datums/diseases/advance/symptoms/fire.dm:92
public partial class datum_symptom_alkali : datum_symptom
{
    // tg: code/datums/diseases/advance/symptoms/fire.dm:115
    [DmParams("A")]
    public override object Start(datum_disease_advance A = null)
    {
        object dot = null;
        dot = base.Start(A);
        if (!Dm.truthy(dot))
            return dot;
        if (Dm.num(A.totalResistance()) >= 9) //intense but sporadic effect
        {
            power = 2;
            symptom_delay = 95;
        }
        if (Dm.num(A.totalStageSpeed()) >= 8) //serious boom when wet
            explosion_power = 2;
        if (Dm.num(A.totalTransmittable()) >= 8) //extra chemicals
            chems = TRUE;
        return dot;
    }

    // tg: code/datums/diseases/advance/symptoms/fire.dm:127
    [DmParams("A")]
    public override async DmTask<object> Activate(datum_disease_advance A = null)
    {
        object dot = null;
        dot = await base.Activate(A);
        if (!Dm.truthy(dot))
            return dot;
        mob_living M = A.affected_mob;
        if (A.stage == 3)
        {
            if (prob(base_message_chance))
                to_chat(M, span_warning(I18n.T(Dm.str(pick(Dm.list("Your veins boil.", "You feel hot.", "You smell meat cooking."))))));
        }
        else if (A.stage == 4)
        {
            if (M.fire_stacks < 0)
            {
                M.visible_message(span_warning(I18n.T("[]'s sweat sizzles and pops on contact with water!", M)));
                await explosion(M, devastation_range: -1, heavy_impact_range: (-1 + explosion_power), light_impact_range: (2 * explosion_power), explosion_cause: this);
            }
            await Alkali_fire_stage_4(M, A);
            M.ignite_mob();
            to_chat(M, span_userdanger(I18n.T("Your sweat bursts into flames!")));
            await M.emote("scream");
        }
        else if (A.stage == 5)
        {
            if (M.fire_stacks < 0)
            {
                M.visible_message(span_warning(I18n.T("[]'s sweat sizzles and pops on contact with water!", M)));
                await explosion(M, devastation_range: -1, heavy_impact_range: (-1 + explosion_power), light_impact_range: (2 * explosion_power), explosion_cause: this);
            }
            await Alkali_fire_stage_5(M, A);
            M.ignite_mob();
            to_chat(M, span_userdanger(I18n.T("Your skin erupts into an inferno!")));
            await M.emote("scream");
        }
        return dot;
    }

    // tg: code/datums/diseases/advance/symptoms/fire.dm:153
    public virtual async DmTask<object> Alkali_fire_stage_4(mob_living M = null, datum_disease_advance A = null)
    {
        float get_stacks = 6 * Dm.num(power);
        await M.adjust_fire_stacks(get_stacks);
        await M.take_overall_damage(burn: get_stacks / 2, required_bodytype: BODYTYPE_ORGANIC);
        if (Dm.truthy(chems))
            await ((datum_reagents)M.reagents).add_reagent(typeof(datum_reagent_clf3), 2 * Dm.num(power));
        return 1;
    }

    // tg: code/datums/diseases/advance/symptoms/fire.dm:161
    public virtual async DmTask<object> Alkali_fire_stage_5(mob_living M = null, datum_disease_advance A = null)
    {
        float get_stacks = 8 * Dm.num(power);
        await M.adjust_fire_stacks(get_stacks);
        await M.take_overall_damage(burn: get_stacks, required_bodytype: BODYTYPE_ORGANIC);
        if (Dm.truthy(chems))
            await ((datum_reagents)M.reagents).add_reagent_list(Dm.list(Dm.kv(typeof(datum_reagent_napalm), 4 * Dm.num(power)), Dm.kv(typeof(datum_reagent_clf3), 4 * Dm.num(power))));
        return 1;
    }
}
