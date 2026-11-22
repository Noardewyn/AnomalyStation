// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/projectile/bullets/dart_syringe.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8 slice C (D190): the whole file (the dart injecting its reagents where it hits unless deflected, the syringe dart
// dropping or embedding its syringe, the metal foam and piercing darts). The var blocks are generated.

// tg: code/modules/projectiles/projectile/bullets/dart_syringe.dm:1
public partial class obj_projectile_bullet_dart : obj_projectile_bullet
{
    // tg: code/modules/projectiles/projectile/bullets/dart_syringe.dm:9
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        create_reagents(50, NO_REACT);
        return dot;
    }

    // tg: code/modules/projectiles/projectile/bullets/dart_syringe.dm:13
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        blocked ??= 0;
        datum_reagents holder = (datum_reagents)reagents;
        if (iscarbon(target))
        {
            mob_living_carbon M = (mob_living_carbon)target;
            if (blocked != 100) // not completely blocked
            {
                if (Dm.truthy(M.can_inject(target_zone: def_zone, injection_flags: inject_flags))) // Pass the hit zone to see if it can inject by whether it hit the head or the body.
                {
                    await base.on_hit(target, blocked, pierce_hit);
                    await holder.trans_to(M, holder.total_volume, methods: INJECT);
                    return BULLET_ACT_HIT;
                }
                else
                {
                    blocked = 100;
                    target.visible_message(span_danger(I18n.T("\\The [] is deflected!", this)),
                                           span_userdanger(I18n.T("You are protected against \\the []!", this)));
                }
            }
        }

        await base.on_hit(target, blocked);
        holder.flags = Dm.bitand(holder.flags, Dm.bitnot(NO_REACT));
        await holder.handle_reactions();
        return BULLET_ACT_HIT;
    }
}

// tg: code/modules/projectiles/projectile/bullets/dart_syringe.dm:30
public partial class obj_projectile_bullet_dart_metalfoam : obj_projectile_bullet_dart
{
    // tg: code/modules/projectiles/projectile/bullets/dart_syringe.dm:30
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        datum_reagents holder = (datum_reagents)reagents;
        Waiting.NoSleep(holder.add_reagent(typeof(datum_reagent_aluminium), 15));
        Waiting.NoSleep(holder.add_reagent(typeof(datum_reagent_foaming_agent), 5));
        Waiting.NoSleep(holder.add_reagent(typeof(datum_reagent_toxin_acid_fluacid), 5));
        return dot;
    }
}

// tg: code/modules/projectiles/projectile/bullets/dart_syringe.dm:37
public partial class obj_projectile_bullet_dart_syringe : obj_projectile_bullet_dart
{
    // tg: code/modules/projectiles/projectile/bullets/dart_syringe.dm:43
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // This prevents the Ody from being used as a combat mech spamming RDX/Teslium syringes all over the place.
        // Other syringe guns are loaded manually with pre-filled syringes which will react chems themselves.
        // The traitor chem dartgun uses /obj/projectile/bullet/dart/piercing, so this does not impact it.
        datum_reagents holder = (datum_reagents)reagents;
        holder.flags = Dm.bitand(holder.flags, Dm.bitnot(NO_REACT));
        return dot;
    }

    // tg: code/modules/projectiles/projectile/bullets/dart_syringe.dm:50
    public override async DmTask on_range()
    {
        if (inner_syringe != null)
            await inner_syringe.forceMove((atom)drop_location());
        await base.on_range();
    }

    // tg: code/modules/projectiles/projectile/bullets/dart_syringe.dm:55
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        object dot = await base.on_hit(target, blocked, pierce_hit);
        if (inner_syringe != null)
            await inner_syringe.forceMove((atom)drop_location());
        return dot;
    }
}
