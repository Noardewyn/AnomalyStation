// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/organs/internal/stomach/stomach_golem.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps OG1, D339, D341): the golem's silicate grinder, which works in any carbon without the golem species (it eats only
// the golem food the species' minerals make), and the statued status of an empty tank. The var blocks are generated (D111).

// tg: code/modules/surgery/organs/internal/stomach/stomach_golem.dm:1
public partial class obj_item_organ_stomach_golem : obj_item_organ_stomach
{
    // tg: code/modules/surgery/organs/internal/stomach/stomach_golem.dm:14
    public override async DmTask<object> on_mob_insert(mob_living_carbon organ_owner = null, float? special = null, object movement_flags = null)
    {
        object dot = await base.on_mob_insert(organ_owner, special, movement_flags);
        RegisterSignal(owner, COMSIG_CARBON_ATTEMPT_EAT, nameof(try_eating));
        return dot;
    }

    // tg: code/modules/surgery/organs/internal/stomach/stomach_golem.dm:18
    public override async DmTask on_mob_remove(object organ_owner = null, float? special = null, object movement_flags = null)
    {
        await base.on_mob_remove(organ_owner, special, movement_flags);
        mob_living_carbon organ_owner__carbon = organ_owner as mob_living_carbon;
        UnregisterSignal(organ_owner__carbon, COMSIG_CARBON_ATTEMPT_EAT);
        organ_owner__carbon.remove_movespeed_modifier(typeof(datum_movespeed_modifier_golem_hunger));
        organ_owner__carbon.remove_status_effect(typeof(datum_status_effect_golem_statued));
    }

    /// Reject food, rocks only
    // tg: code/modules/surgery/organs/internal/stomach/stomach_golem.dm:25
    [SignalHandler]
    public virtual object try_eating(mob_living_carbon source = null, atom eating = null)
    {
        if (istype(eating, typeof(obj_item_food_golem_food)))
            return null;
        source.balloon_alert(source, I18n.T("minerals only!"));
        return BLOCK_EAT_ATTEMPT;
    }

    /// Golem stomach cannot process nutriment except from minerals
    // tg: code/modules/surgery/organs/internal/stomach/stomach_golem.dm:33
    [DmParams("delta_time")]
    public override async DmTask on_life(object seconds_per_tick = null)
    {
        foreach (datum_reagent_consumable food in Dm.each_cast<datum_reagent_consumable>((reagents as datum_reagents)?.reagent_list))
        {
            if (istype(food, typeof(datum_reagent_consumable_nutriment_mineral)))
                continue;
            food.nutriment_factor = 0;
        }
        await base.on_life(seconds_per_tick);
    }

    /// Slow down based on how full you are
    // tg: code/modules/surgery/organs/internal/stomach/stomach_golem.dm:41
    [DmParams("human", "delta_time")]
    public override async DmTask handle_hunger(mob_living_carbon_human human = null, object seconds_per_tick = null)
    {
        // the effects are all negative, so just don't run them if you have the trait
        await base.handle_hunger(human, seconds_per_tick);
        if (HAS_TRAIT(human, TRAIT_NOHUNGER))
            return;
        float hunger = (NUTRITION_LEVEL_HUNGRY - Dm.num(human.nutrition)) / NUTRITION_LEVEL_HUNGRY; // starving = 1, satisfied = 0
        if (hunger > 0)
        {
            float slowdown = LERP(min_hunger_slowdown, max_hunger_slowdown, hunger);
            await human.add_or_update_variable_movespeed_modifier(typeof(datum_movespeed_modifier_golem_hunger), multiplicative_slowdown: slowdown);
        }
        else
            human.remove_movespeed_modifier(typeof(datum_movespeed_modifier_golem_hunger));

        if (hunger >= 1)
            await human.apply_status_effect(typeof(datum_status_effect_golem_statued));
        else
            human.remove_status_effect(typeof(datum_status_effect_golem_statued));
    }
}

/// Uh oh, you can't move, yell for help
// tg: code/modules/surgery/organs/internal/stomach/stomach_golem.dm:59
public partial class datum_status_effect_golem_statued : datum_status_effect
{
    // tg: code/modules/surgery/organs/internal/stomach/stomach_golem.dm:69
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        object dot = await base.on_apply(__extra);
        if (!Dm.truthy(dot))
            return FALSE;
        owner.visible_message(span_warning(I18n.T("[] siezes up and becomes as rigid as a statue!", owner)), span_warning(I18n.T("Your limbs fall still. You no longer have enough energy to move!")));
        owner.add_traits(Dm.list(TRAIT_IMMOBILIZED, TRAIT_FORCED_STANDING, TRAIT_HANDS_BLOCKED, TRAIT_INCAPACITATED), TRAIT_STATUS_EFFECT(id));
        return TRUE;
    }

    // tg: code/modules/surgery/organs/internal/stomach/stomach_golem.dm:77
    public override object get_examine_text(mob examiner = null)
    {
        return span_warning(I18n.T("[] [] as still as a statue!", owner.p_They(), owner.p_are()));
    }

    // tg: code/modules/surgery/organs/internal/stomach/stomach_golem.dm:80
    public override async DmTask<object> on_remove()
    {
        owner.visible_message(span_notice(I18n.T("[] slowly stirs back into motion!", owner)), span_notice(I18n.T("You have gathered enough strength to move your body once more.")));
        owner.remove_traits(Dm.list(TRAIT_IMMOBILIZED, TRAIT_FORCED_STANDING, TRAIT_HANDS_BLOCKED, TRAIT_INCAPACITATED), TRAIT_STATUS_EFFECT(id));
        return await base.on_remove();
    }
}
