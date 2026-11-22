// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/projectile/bullets/junk.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

// Junk (Pipe Pistols and Pipeguns)

namespace Anomaly.Core.Tg;

// PARTIAL (gaps GU1, D333): the pipe guns' junk bullets (the extra damage against their biotypes; the shock bundle's electrocution).
// The phasic, hunter, ripper and reaper bullets, the burning oil and the embedding datums only set vars. Left (docs/ai/specs/not-translated.md
// section 215): the ball of ants (junk.dm:54, it waits for the ants reagent). The var blocks are generated (D111).

// tg: code/modules/projectiles/projectile/bullets/junk.dm:3
public partial class obj_projectile_bullet_junk : obj_projectile_bullet
{
    // tg: code/modules/projectiles/projectile/bullets/junk.dm:17
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        blocked ??= 0;
        object dot = await base.on_hit(target, blocked, pierce_hit);

        if (!isliving(target))
            return dot;
        mob_living living_target = (mob_living)target;

        float is_correct_biotype = Dm.num(Dm.bitand(living_target.mob_biotypes, extra_damage_mob_biotypes));
        if (Dm.truthy(extra_damage_mob_biotypes) && Dm.truthy(is_correct_biotype))
        {
            float multiplied_damage = Dm.truthy(extra_damage_multiplier) ? ((Dm.num(damage) * extra_damage_multiplier) - Dm.num(damage)) : 0;
            float finalized_damage = multiplied_damage + extra_damage_added_damage;
            if (Dm.truthy(finalized_damage))
                await living_target.apply_damage(finalized_damage, damagetype: extra_damage_type, def_zone: BODY_ZONE_CHEST, wound_bonus: wound_bonus);
        }
        return dot;
    }
}

// tg: code/modules/projectiles/projectile/bullets/junk.dm:63
public partial class obj_projectile_bullet_junk_shock : obj_projectile_bullet_junk
{
    // tg: code/modules/projectiles/projectile/bullets/junk.dm:72
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        blocked ??= 0;
        object dot = await base.on_hit(target, blocked, pierce_hit);
        if (isliving(target))
        {
            mob_living victim = (mob_living)target;
            await victim.electrocute_act(damage, this, siemens_coeff: 1, flags: SHOCK_NOSTUN);
        }
        return dot;
    }
}
