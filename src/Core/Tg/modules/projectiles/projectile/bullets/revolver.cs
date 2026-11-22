// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/projectile/bullets/revolver.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps GU1, D333): the detective's special .38 rounds (the TRAC bullet that implants a tracker, the Hot Shot that sets
// a man alight, the Iceblox that chills him) and the gatfruit's pea bullet that injects its reagents. The other bullets only set vars.
// The var blocks are generated (D111).

// tg: code/modules/projectiles/projectile/bullets/revolver.dm:93
public partial class obj_projectile_bullet_c38_trac : obj_projectile_bullet_c38
{
    // tg: code/modules/projectiles/projectile/bullets/revolver.dm:98
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        blocked ??= 0;
        object dot = await base.on_hit(target, blocked, pierce_hit);
        mob_living_carbon M = target as mob_living_carbon;
        if (M == null)
            return dot;
        obj_item_implant_tracking_c38 imp = null;
        foreach (obj_item_implant_tracking_c38 TI in Dm.each<obj_item_implant_tracking_c38>(M.implants)) //checks if the target already contains a tracking implant
        {
            imp = TI;
            return dot;
        }
        if (imp == null)
        {
            imp = Dm.@new<obj_item_implant_tracking_c38>(M);
            await imp.implant(M);
        }
        return dot;
    }
}

// tg: code/modules/projectiles/projectile/bullets/revolver.dm:111
public partial class obj_projectile_bullet_c38_hotshot : obj_projectile_bullet_c38 //similar to incendiary bullets, but do not leave a flaming trail
{
    // tg: code/modules/projectiles/projectile/bullets/revolver.dm:116
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        blocked ??= 0;
        object dot = await base.on_hit(target, blocked, pierce_hit);
        if (iscarbon(target))
        {
            mob_living_carbon criminal_scum = (mob_living_carbon)target;
            await criminal_scum.adjust_fire_stacks(6);
            criminal_scum.ignite_mob();
        }
        return dot;
    }
}

// tg: code/modules/projectiles/projectile/bullets/revolver.dm:123
public partial class obj_projectile_bullet_c38_iceblox : obj_projectile_bullet_c38 //see /obj/projectile/temp for the original code
{
    // tg: code/modules/projectiles/projectile/bullets/revolver.dm:129
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        blocked ??= 0;
        object dot = await base.on_hit(target, blocked, pierce_hit);
        if (isliving(target))
        {
            mob_living criminal_scum = (mob_living)target;
            criminal_scum.adjust_bodytemperature(((100 - blocked.Value) / 100) * (temperature - criminal_scum.bodytemperature));
        }
        return dot;
    }
}

//gatfruit
// tg: code/modules/projectiles/projectile/bullets/revolver.dm:167
public partial class obj_projectile_bullet_pea : obj_projectile_bullet
{
    // tg: code/modules/projectiles/projectile/bullets/revolver.dm:175
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        create_reagents(100, NO_REACT); //same as the fruit itself, wont ever hit that much though i believe
        return dot;
    }

    // tg: code/modules/projectiles/projectile/bullets/revolver.dm:179
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        blocked ??= 0;
        object dot = null;
        mob_living_carbon carbon_target = target as mob_living_carbon; // the parameter is typed mob/living/carbon here (rules 3.3, narrowed)
        if (carbon_target != null && blocked.Value != 100)
        {
            if (Dm.truthy(carbon_target.has_status_effect(typeof(datum_status_effect_zombie)))) // https://www.youtube.com/watch?v=ssZoq1eUK-s
                await carbon_target.adjust_brute_loss(15);
            if (Dm.truthy(carbon_target.can_inject(target_zone: def_zone))) // Pass the hit zone to see if it can inject by whether it hit the head or the body.
            {
                await base.on_hit(target, blocked, pierce_hit);
                await ((datum_reagents)reagents).trans_to(carbon_target, ((datum_reagents)reagents).total_volume, methods: INJECT);
                return BULLET_ACT_HIT;
            }
            blocked = 100;
            carbon_target.visible_message(span_danger(I18n.T("\\The [] is deflected!", this)), span_userdanger(I18n.T("You are protected against \\the []!", this)));
        }
        dot = await base.on_hit(target, blocked, pierce_hit);
        if (Dm.truthy(Dm.bitand(((datum_reagents)reagents).flags, NO_REACT))) //first impact on a noncarbon
        {
            ((datum_reagents)reagents).flags = Dm.bitand(((datum_reagents)reagents).flags, ~NO_REACT);
            await ((datum_reagents)reagents).handle_reactions();
        }
        return dot;
    }
}
