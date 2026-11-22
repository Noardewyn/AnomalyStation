// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/ammunition/ballistic/revolver.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate N2a, D316: the .357 heartseeker of the operatives' revolver, homing on the aimed target; the gatfruit's pea
// with it; the other revolver casings are var blocks). The var blocks are generated (D111).

// tg: code/modules/projectiles/ammunition/ballistic/revolver.dm:22
public partial class obj_item_ammo_casing_c357_heartseeker : obj_item_ammo_casing_c357
{
    // tg: code/modules/projectiles/ammunition/ballistic/revolver.dm:26
    public override async DmTask ready_proj(atom target = null, mob_living user = null, object quiet = null, string zone_override = null, atom fired_from = null)
    {
        await base.ready_proj(target, user, quiet, zone_override, fired_from);
        if (!isturf(target))
            loaded_projectile.set_homing_target(target);
    }
}

//gatfruit
// tg: code/modules/projectiles/ammunition/ballistic/revolver.dm:92
public partial class obj_item_ammo_casing_pea : obj_item_ammo_casing
{
    // tg: code/modules/projectiles/ammunition/ballistic/revolver.dm:103
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        create_reagents(60, SEALED_CONTAINER);
        return dot;
    }

    // tg: code/modules/projectiles/ammunition/ballistic/revolver.dm:107
    public override async DmTask ready_proj(atom target = null, mob_living user = null, object quiet = null, string zone_override = null, atom fired_from = null)
    {
        await base.ready_proj(target, user, quiet, zone_override, fired_from);
        if (loaded_projectile == null)
            return;
        loaded_projectile.damage = damage;
    }

    // tg: code/modules/projectiles/ammunition/ballistic/revolver.dm:113
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        if (loaded_projectile == null)
            return dot;
        var peas = Dm.@new<obj_item_food_grown_peas>(user.drop_location());
        await user.put_in_hands(peas);
        to_chat(user, span_notice(I18n.T("You separate [] from [].", peas, this)));
        loaded_projectile = null;
        update_appearance();
        return dot;
    }
}
