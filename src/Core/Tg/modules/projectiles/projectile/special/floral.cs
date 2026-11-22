// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/projectile/special/floral.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P2 (D298): the whole file (the floral somatoray's beams; on a tray they act through its bullet_act).

// tg: code/modules/projectiles/projectile/special/floral.dm:1
public partial class obj_projectile_energy_flora : obj_projectile_energy
{
    // tg: code/modules/projectiles/projectile/special/floral.dm:6
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        if (!isliving(target))
            return await base.on_hit(target, blocked, pierce_hit);

        mob_living hit_plant = target as mob_living;
        if (!Dm.truthy(Dm.bitand(hit_plant.mob_biotypes, MOB_PLANT)))
        {
            hit_plant.show_message(span_notice(I18n.T("The radiation beam dissipates harmlessly through your body.")));
            return BULLET_ACT_BLOCK;
        }

        object dot = await base.on_hit(target, blocked, pierce_hit);
        if (Dm.eq(dot, BULLET_ACT_HIT) && Dm.num(blocked) < 100)
            await on_hit_plant_effect(hit_plant);

        return dot;
    }

    /// Called when we hit a mob with plant biotype
    // tg: code/modules/projectiles/projectile/special/floral.dm:22
    public virtual DmTask on_hit_plant_effect(mob_living hit_plant = null)
    {
        return DmTask.CompletedTask;
    }
}

// tg: code/modules/projectiles/projectile/special/floral.dm:25
public partial class obj_projectile_energy_flora_mut : obj_projectile_energy_flora
{
    // tg: code/modules/projectiles/projectile/special/floral.dm:29
    public override async DmTask on_hit_plant_effect(mob_living hit_plant = null)
    {
        if (prob(85))
        {
            await hit_plant.adjust_fire_loss(rand(5, 15));
            hit_plant.show_message(span_userdanger(I18n.T("The radiation beam singes you!")));
            return;
        }

        await hit_plant.adjust_tox_loss(rand(3, 6));
        await hit_plant.Paralyze(10 * SECONDS);
        hit_plant.visible_message(
            span_warning(I18n.T("[] writhes in pain as [] vacuoles boil.", hit_plant, hit_plant.p_their())),
            span_userdanger(I18n.T("You writhe in pain as your vacuoles boil!")),
            span_hear(I18n.T("You hear the crunching of leaves.")));
        if (iscarbon(hit_plant) && Dm.truthy(hit_plant.has_dna()))
        {
            mob_living_carbon carbon_plant = hit_plant as mob_living_carbon;
            if (prob(80))
                await carbon_plant.easy_random_mutate(NEGATIVE + MINOR_NEGATIVE);
            else
                await carbon_plant.easy_random_mutate(POSITIVE);
            await carbon_plant.random_mutate_unique_identity();
            await carbon_plant.random_mutate_unique_features();
            await carbon_plant.domutcheck();
        }
    }
}

// tg: code/modules/projectiles/projectile/special/floral.dm:52
public partial class obj_projectile_energy_flora_yield : obj_projectile_energy_flora
{
    // tg: code/modules/projectiles/projectile/special/floral.dm:56
    public override async DmTask on_hit_plant_effect(mob_living hit_plant = null)
    {
        await hit_plant.set_nutrition(Dm.min(Dm.num(hit_plant.nutrition) + 30, NUTRITION_LEVEL_FULL));
    }
}

// tg: code/modules/projectiles/projectile/special/floral.dm:59
public partial class obj_projectile_energy_flora_evolution : obj_projectile_energy_flora
{
    // tg: code/modules/projectiles/projectile/special/floral.dm:63
    public override async DmTask on_hit_plant_effect(mob_living hit_plant = null)
    {
        hit_plant.show_message(span_notice(I18n.T("The radiation beam leaves you feeling disoriented!")));
        await hit_plant.set_dizzy_if_lower(30 * SECONDS);
        await hit_plant.emote("flip");
        await hit_plant.emote("spin");
    }
}
