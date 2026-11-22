// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/projectile/energy/thermal.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps GU1, D333): the thermal pistols' nanite shots. The molten one bursts a man chilled far below his species'
// cold limit in a small fiery explosion and warms him back to normal; the frozen one knocks down and burns a man heated far above
// his heat limit and cools him back. The var blocks are generated (D111).

// tg: code/modules/projectiles/projectile/energy/thermal.dm:1
public partial class obj_projectile_energy_inferno : obj_projectile_energy
{
    // tg: code/modules/projectiles/projectile/energy/thermal.dm:13
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        blocked ??= 0;
        object dot = await base.on_hit(target, blocked, pierce_hit);
        if (!ishuman(target))
            return dot;

        if (HAS_TRAIT(target, TRAIT_RESISTCOLD))
            return dot;

        mob_living_carbon cold_target = target as mob_living_carbon;
        float how_cold_is_target = cold_target.bodytemperature;
        float danger_zone = ((datum_species)cold_target.dna.species).bodytemp_cold_damage_limit - 150;
        if (how_cold_is_target < danger_zone)
        {
            await explosion(cold_target, devastation_range: -1, heavy_impact_range: -1, light_impact_range: 2, flame_range: 3); //maybe stand back a bit
            cold_target.bodytemperature = ((datum_species)cold_target.dna.species).bodytemp_normal; //avoids repeat explosions, maybe could be used to heat up again?
            playsound(cold_target, Dm.resource("sound/items/weapons/sear.ogg"), 30, TRUE, -1);
        }
        return dot;
    }
}

// tg: code/modules/projectiles/projectile/energy/thermal.dm:29
public partial class obj_projectile_energy_cryo : obj_projectile_energy
{
    // tg: code/modules/projectiles/projectile/energy/thermal.dm:41
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        blocked ??= 0;
        object dot = await base.on_hit(target, blocked, pierce_hit);
        if (!ishuman(target))
            return dot;

        if (HAS_TRAIT(target, TRAIT_RESISTHEAT))
            return dot;

        mob_living_carbon hot_target = target as mob_living_carbon;
        float how_hot_is_target = hot_target.bodytemperature;
        float danger_zone = ((datum_species)hot_target.dna.species).bodytemp_heat_damage_limit + 300;
        if (how_hot_is_target > danger_zone)
        {
            await hot_target.Knockdown(100);
            await hot_target.apply_damage(20, BURN);
            hot_target.bodytemperature = ((datum_species)hot_target.dna.species).bodytemp_normal; //avoids repeat knockdowns, maybe could be used to cool down again?
            playsound(hot_target, Dm.resource("sound/items/weapons/sonic_jackhammer.ogg"), 30, TRUE, -1);
        }
        return dot;
    }
}
