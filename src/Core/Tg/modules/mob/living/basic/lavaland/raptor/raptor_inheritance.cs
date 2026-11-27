// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/lavaland/raptor/raptor_inheritance.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;


// tg: code/modules/mob/living/basic/lavaland/raptor/raptor_inheritance.dm:1
public partial class datum_raptor_inheritance : datum
{
    // Owner being set will randomize the stats as it means we're spawned not via an egg
    // otherwise wait for set_parents call to inherit genetics
    // tg: code/modules/mob/living/basic/lavaland/raptor/raptor_inheritance.dm:23
    [DmParams("owner")]
    public override async DmTask New(params object[] __extra)
    {
        mob_living_basic_raptor owner = Dm.arg<mob_living_basic_raptor>(__extra, 0);
        await base.New(__extra);
        if (owner == null)
            return;

        attack_modifier = rand(RAPTOR_INHERIT_MIN_ATTACK * 0.5f, RAPTOR_INHERIT_MAX_ATTACK * 0.5f);
        health_modifier = rand(RAPTOR_INHERIT_MIN_HEALTH * 0.5f, RAPTOR_INHERIT_MAX_HEALTH * 0.5f);
        DmList traits_to_pick = GLOB.raptor_inherit_traits.Copy();
        float amount = rand(1, RAPTOR_TRAIT_INHERIT_AMOUNT);
        for (float i = 1; i <= amount; i++)
            Dm.append(ref personality_traits, pick_n_take(traits_to_pick));
        return;
    }

    // tg: code/modules/mob/living/basic/lavaland/raptor/raptor_inheritance.dm:34
    public virtual void set_parents(mob_living_basic_raptor mother = null, mob_living_basic_raptor father = null)
    {
        datum_raptor_inheritance mom_stats = mother.inherited_stats;
        datum_raptor_inheritance dad_stats = father.inherited_stats;

        parent_colors = Dm.bitor(Dm.bitor(Dm.bitor(mom_stats.parent_colors, dad_stats.parent_colors), (mother.raptor_color as datum).type), (father.raptor_color as datum).type) as DmList;

        DmList traits_to_pick = Dm.list();
        // + so shared traits have double the weight
        foreach (object raptor_trait in Dm.each(Dm.add(mom_stats.personality_traits, dad_stats.personality_traits)))
        {
            if (!Dm.truthy(traits_to_pick[raptor_trait]))
                traits_to_pick[raptor_trait] = 0;
            traits_to_pick[raptor_trait] = Dm.num(traits_to_pick[raptor_trait]) + 1;
        }

        float attack_mod = 0;
        float health_mod = 0;
        float speed_mod = 0;
        float ability_mod = 0;
        float growth_mod = 0;
        float amount_eaten = 0;

        foreach (object food_type in Dm.each(foods_eaten))
        {
            DmList stat_mods = foods_eaten[food_type] as DmList;
            amount_eaten += Dm.num(stat_mods["amount"]);
        }

        foreach (object food_type in Dm.each(foods_eaten))
        {
            DmList stat_mods = foods_eaten[food_type] as DmList;
            // First multiply stats themselves, then multiply to get a proportion of this food from amount_eaten
            float amount = Dm.num(Dm.pow(stat_mods["amount"], 2));
            // Eating other foods reduces the effects of a specific one
            attack_mod += Dm.num(stat_mods["attack"]) / amount_eaten * amount;
            health_mod += Dm.num(stat_mods["health"]) / amount_eaten * amount;
            speed_mod += Dm.num(stat_mods["speed"]) / amount_eaten * amount;
            ability_mod += Dm.num(stat_mods["ability"]) / amount_eaten * amount;
            growth_mod += Dm.num(stat_mods["growth"]) / amount_eaten * amount;
            DmList trait_list = stat_mods["traits"] as DmList;
            foreach (object raptor_trait in Dm.each(trait_list))
            {
                if (!Dm.truthy(traits_to_pick[raptor_trait]))
                    traits_to_pick[raptor_trait] = 0;
                traits_to_pick[raptor_trait] = Dm.num(traits_to_pick[raptor_trait]) + Dm.num(trait_list[raptor_trait]) * Dm.num(stat_mods["amount"]);
            }
        }

        // If we don't clamp these, RNG and drift can get wildly out of control and result in polar values
        // and this way we at most get twice the minimum/maximum value, so at least half the rand spread is within the clamp values
        attack_mod = clamp(attack_mod, RAPTOR_INHERIT_MIN_ATTACK, RAPTOR_INHERIT_MAX_ATTACK);
        health_mod = clamp(health_mod, RAPTOR_INHERIT_MIN_HEALTH, RAPTOR_INHERIT_MAX_HEALTH);
        speed_mod = clamp(speed_mod, RAPTOR_INHERIT_MIN_SPEED, RAPTOR_INHERIT_MAX_SPEED);
        ability_mod = clamp(ability_mod, RAPTOR_INHERIT_MIN_MODIFIER, RAPTOR_INHERIT_MAX_MODIFIER);
        growth_mod = clamp(growth_mod, RAPTOR_INHERIT_MIN_MODIFIER, RAPTOR_INHERIT_MAX_MODIFIER);

        float mom_attack = Dm.num(mom_stats.attack_modifier), dad_attack = Dm.num(dad_stats.attack_modifier);
        float mom_health = Dm.num(mom_stats.health_modifier), dad_health = Dm.num(dad_stats.health_modifier);
        float mom_speed = Dm.num(mom_stats.speed_modifier), dad_speed = Dm.num(dad_stats.speed_modifier);
        float mom_ability = Dm.num(mom_stats.ability_modifier), dad_ability = Dm.num(dad_stats.ability_modifier);
        float mom_growth = Dm.num(mom_stats.growth_modifier), dad_growth = Dm.num(dad_stats.growth_modifier);
        attack_modifier = rand((min(mom_attack, dad_attack) + min(0, attack_mod)) + RAPTOR_GENETIC_DRIFT * RAPTOR_INHERIT_MIN_ATTACK, (max(mom_attack, dad_attack) + max(0, attack_mod)) + RAPTOR_GENETIC_DRIFT * RAPTOR_INHERIT_MAX_ATTACK);
        health_modifier = rand((min(mom_health, dad_health) + min(0, health_mod)) + RAPTOR_GENETIC_DRIFT * RAPTOR_INHERIT_MIN_HEALTH, (max(mom_health, dad_health) + max(0, health_mod)) + RAPTOR_GENETIC_DRIFT * RAPTOR_INHERIT_MAX_HEALTH);
        speed_modifier = rand((min(mom_speed, dad_speed) + min(0, speed_mod)) + RAPTOR_GENETIC_DRIFT * RAPTOR_INHERIT_MIN_SPEED, (max(mom_speed, dad_speed) + max(0, speed_mod)) + RAPTOR_GENETIC_DRIFT * RAPTOR_INHERIT_MAX_SPEED);
        ability_modifier = rand((min(mom_ability, dad_ability) + min(0, ability_mod)) + RAPTOR_GENETIC_DRIFT * RAPTOR_INHERIT_MIN_MODIFIER, (max(mom_ability, dad_ability) + max(0, ability_mod)) + RAPTOR_GENETIC_DRIFT * RAPTOR_INHERIT_MAX_MODIFIER);
        growth_modifier = rand((min(mom_growth, dad_growth) + min(0, growth_mod)) + RAPTOR_GENETIC_DRIFT * RAPTOR_INHERIT_MIN_MODIFIER, (max(mom_growth, dad_growth) + max(0, growth_mod)) + RAPTOR_GENETIC_DRIFT * RAPTOR_INHERIT_MAX_MODIFIER);

        float picks = min(Dm.length(traits_to_pick), RAPTOR_TRAIT_INHERIT_AMOUNT);
        for (float i = 1; i <= picks; i++)
        {
            object chosen_trait = pick_weight(traits_to_pick);
            Dm.remove(ref traits_to_pick, chosen_trait);
            Dm.append(ref personality_traits, chosen_trait);
        }

        attack_modifier = clamp(Dm.num(attack_modifier), RAPTOR_INHERIT_MIN_ATTACK, RAPTOR_INHERIT_MAX_ATTACK);
        health_modifier = clamp(Dm.num(health_modifier), RAPTOR_INHERIT_MIN_HEALTH, RAPTOR_INHERIT_MAX_HEALTH);
        speed_modifier = clamp(Dm.num(speed_modifier), RAPTOR_INHERIT_MIN_SPEED, RAPTOR_INHERIT_MAX_SPEED);
        ability_modifier = clamp(Dm.num(ability_modifier), RAPTOR_INHERIT_MIN_MODIFIER, RAPTOR_INHERIT_MAX_MODIFIER);
        growth_modifier = clamp(Dm.num(growth_modifier), RAPTOR_INHERIT_MIN_MODIFIER, RAPTOR_INHERIT_MAX_MODIFIER);
    }
}
