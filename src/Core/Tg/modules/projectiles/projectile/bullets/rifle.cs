// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/projectile/bullets/rifle.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate G4b, D316: the rebar crossbow's rebar, its empty handle_drop; gaps GU1, D333: the metallic hydrogen bolt
// that pierces men and loses its aim with the distance, the healium bolt that hands its heals to the rod it leaves, the supermatter
// bolt that dusts what it hits). The other bullets only set vars. The var blocks are generated (D111).

// Rebar (Rebar Crossbow)
// tg: code/modules/projectiles/projectile/bullets/rifle.dm:66
public partial class obj_projectile_bullet_rebar : obj_projectile_bullet
{
    // tg: code/modules/projectiles/projectile/bullets/rifle.dm:90
    public virtual void handle_drop(datum source = null, obj_item_ammo_casing_rebar newcasing = null)
    {
    }
}

// tg: code/modules/projectiles/projectile/bullets/rifle.dm:139
public partial class obj_projectile_bullet_rebar_hydrogen : obj_projectile_bullet_rebar
{
    // tg: code/modules/projectiles/projectile/bullets/rifle.dm:158
    public override async DmTask<object> impact(atom target = null)
    {
        object dot = await base.impact(target);
        def_zone = (string)ran_zone(def_zone, clamp(205 - (7 * get_dist(get_turf(target), starting)), 5, 100));
        return dot;
    }

    // tg: code/modules/projectiles/projectile/bullets/rifle.dm:165
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        if (isAI(target))
            return BULLET_ACT_FORCE_PIERCE;
        return await base.on_hit(target, blocked, pierce_hit);
    }
}

// tg: code/modules/projectiles/projectile/bullets/rifle.dm:170
public partial class obj_projectile_bullet_rebar_healium : obj_projectile_bullet_rebar
{
    // tg: code/modules/projectiles/projectile/bullets/rifle.dm:185
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        RegisterSignals(this, Dm.list(COMSIG_PROJECTILE_ON_SPAWN_DROP, COMSIG_PROJECTILE_ON_SPAWN_EMBEDDED), nameof(on_spawn_embedded));
        return dot;
    }

    // tg: code/modules/projectiles/projectile/bullets/rifle.dm:189
    [SignalHandler]
    public virtual void on_spawn_embedded(datum source = null, obj_item_ammo_casing_rebar_healium rebar = null)
    {
        rebar.heals_left = heals_left;
    }
}

// tg: code/modules/projectiles/projectile/bullets/rifle.dm:193
public partial class obj_projectile_bullet_rebar_supermatter : obj_projectile_bullet_rebar
{
    // tg: code/modules/projectiles/projectile/bullets/rifle.dm:203
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        blocked ??= 0;
        object dot = await base.on_hit(target, blocked, pierce_hit);
        if (isliving(target))
        {
            mob_living victim = (mob_living)target;
            victim.investigate_log(Dm.text("has been dusted by [].", this), INVESTIGATE_DEATHS);
            dust_feedback(target);
            await victim.dust();
        }

        else if (!isturf(target) && !isliving(target))
        {
            dust_feedback(target);
            qdel(target);
        }

        return BULLET_ACT_HIT;
    }

    // tg: code/modules/projectiles/projectile/bullets/rifle.dm:217
    public virtual void dust_feedback(atom target = null)
    {
        playsound(get_turf(this), Dm.resource("sound/effects/supermatter.ogg"), 10, TRUE);
        visible_message(span_danger(I18n.T("[] is hit by [], turning [] to dust in a brilliant flash of light!", target, this, target.p_them())));
    }
}
