// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/projectile/magic.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (gaps RD4, D333: the experimentor's fireball malfunction): the magic bolt's base, which fizzles on anyone (or any plant)
// that blocks magic, and the exploding fireball; the root of animate_atom_living (gaps ST5: a structure or a petrified statue made a
// living mimic or statue). The other magic bolts are the wizard's (antagonists, D333) and stay inert
// (not-translated.md 202). The var blocks are generated (D111).

// tg: code/modules/projectiles/projectile/magic.dm:1
public partial class obj_projectile_magic : obj_projectile
{
    // tg: code/modules/projectiles/projectile/magic.dm:13
    public override async DmTask<object> prehit_pierce(atom target = null)
    {
        object dot = await base.prehit_pierce(target);

        if (isliving(target))
        {
            var victim = (mob_living)target;
            if (Dm.truthy(victim.can_block_magic(antimagic_flags, antimagic_charge_cost)))
            {
                visible_message(span_warning(I18n.T("[] fizzles on contact with []!", this, victim)));
                return PROJECTILE_DELETE_WITHOUT_HITTING;
            }
        }

        if (target is obj_machinery_hydroponics) // even plants can block antimagic
        {
            var plant_tray = (obj_machinery_hydroponics)target;
            if (plant_tray.myseed == null)
                return dot;
            if (Dm.truthy(plant_tray.myseed.get_gene(typeof(datum_plant_gene_trait_anti_magic))))
            {
                visible_message(span_warning(I18n.T("[] fizzles on contact with []!", this, plant_tray)));
                return PROJECTILE_DELETE_WITHOUT_HITTING;
            }
        }
        return dot;
    }
}

/// Classic exploding fireball
// tg: code/modules/projectiles/projectile/magic.dm:492
public partial class obj_projectile_magic_fireball : obj_projectile_magic
{
    // tg: code/modules/projectiles/projectile/magic.dm:507
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        blocked ??= 0;
        object dot = await base.on_hit(target, blocked, pierce_hit);
        if (isliving(target))
        {
            var mob_target = (mob_living)target;
            // between this 10 burn, the 10 brute, the explosion brute, and the onfire burn,
            // you are at about 65 damage if you stop drop and roll immediately
            await mob_target.take_overall_damage(burn: 10);
        }

        turf target_turf = get_turf(target);

        await explosion(
            target_turf,
            devastation_range: -1,
            heavy_impact_range: exp_heavy,
            light_impact_range: exp_light,
            flame_range: exp_fire,
            flash_range: exp_flash,
            adminlog: FALSE,
            explosion_cause: this
        );
        return dot;
    }
}

// tg: code/modules/projectiles/projectile/magic.dm:186
public partial class atom : datum
{
    ///proc to animate the target into a living creature, should return the mob if possible
    // tg: code/modules/projectiles/projectile/magic.dm:187  (gaps ST5)
    public virtual async DmTask<object> animate_atom_living(mob_living owner = null)
    {
        await DmTask.CompletedTask;
        return null;
    }
}
