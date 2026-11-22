// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/projectile/special/temperature.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the freeze beam and the heat beam (M7x slice B), the cryo and pyro beams of cryokinesis and pyrokinesis (gaps P3,
// D327). The var blocks are generated (D111).

// tg: code/modules/projectiles/projectile/special/temperature.dm:1
public partial class obj_projectile_temp : obj_projectile
{
    // tg: code/modules/projectiles/projectile/special/temperature.dm:9
    public override object is_hostile_projectile()
    {
        return temperature != 0 ? TRUE : FALSE; // our damage is done by cooling or heating (casting to boolean here)
    }

    // tg: code/modules/projectiles/projectile/special/temperature.dm:12
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        blocked ??= 0;
        object dot = await base.on_hit(target, blocked, pierce_hit);
        if (iscarbon(target))
        {
            mob_living_carbon hit_mob = target as mob_living_carbon;
            float thermal_protection = 1 - Dm.num(hit_mob.get_insulation_protection(hit_mob.bodytemperature + temperature));

            // The new body temperature is adjusted by the bullet's effect temperature
            // Reduce the amount of the effect temperature change based on the amount of insulation the mob is wearing
            hit_mob.adjust_bodytemperature((thermal_protection * temperature) + temperature);
        }
        else if (isliving(target))
        {
            mob_living L = target as mob_living;
            // the new body temperature is adjusted by the bullet's effect temperature
            L.adjust_bodytemperature((1 - blocked.Value) * temperature);
        }

        if (isobj(target))
        {
            obj objectification = target as obj;

            if (objectification.reagents != null)
            {
                datum_reagents reagents = objectification.reagents as datum_reagents;
                if (reagents != null)
                    await reagents.expose_temperature(temperature);
            }
        }
        return dot;
    }
}

// tg: code/modules/projectiles/projectile/special/temperature.dm:39
public partial class obj_projectile_temp_hot : obj_projectile_temp
{
    // tg: code/modules/projectiles/projectile/special/temperature.dm:39
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        blocked ??= FALSE;
        object dot = await base.on_hit(target, blocked, pierce_hit);

        if (isliving(target))
        {
            mob_living living_target = target as mob_living;
            await living_target.adjust_wet_stacks(-10);
        }
        return dot;
    }
}

// tg: code/modules/projectiles/projectile/special/temperature.dm:46
public partial class obj_projectile_temp_cryo : obj_projectile_temp
{
    // tg: code/modules/projectiles/projectile/special/temperature.dm:51
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        blocked ??= 0;
        object dot = await base.on_hit(target, blocked, pierce_hit);

        if (isliving(target))
        {
            mob_living living_target = target as mob_living;
            await living_target.apply_status_effect(typeof(datum_status_effect_freezing_blast));
        }
        return dot;
    }

    // tg: code/modules/projectiles/projectile/special/temperature.dm:58
    public override async DmTask on_range()
    {
        turf T = get_turf(this);
        if (isopenturf(T))
        {
            turf_open O = T as turf_open;
            await O.freeze_turf();
        }
        await base.on_range();
    }
}

// tg: code/modules/projectiles/projectile/special/temperature.dm:65
public partial class obj_projectile_temp_pyro : obj_projectile_temp
{
    // tg: code/modules/projectiles/projectile/special/temperature.dm:71
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        object dot = await base.on_hit(target, blocked, pierce_hit);
        if (!Dm.truthy(dot))
            return dot;

        if (isobj(target))
        {
            obj objectification = target as obj;

            if (Dm.truthy(Dm.bitand(objectification.resistance_flags, ON_FIRE))) //Don't burn something already on fire
                return dot;

            await objectification.fire_act(temperature);

            return dot;
        }

        if (isliving(target))
        {
            mob_living living_target = target as mob_living;
            await living_target.adjust_fire_stacks(2);
            living_target.ignite_mob();
        }
        return dot;
    }

    // tg: code/modules/projectiles/projectile/special/temperature.dm:91
    public override async DmTask on_range()
    {
        turf location = get_turf(this);
        Dm.@new<obj_effect_hotspot>(location);
        location.hotspot_expose(700, 50, 1);
        await base.on_range();
    }
}
