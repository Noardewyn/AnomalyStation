// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/ammunition/ballistic/pistol.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate N2a, D316: the smartgun's .160 smart bullets, homing on the aimed target; the other pistol casings are var
// blocks). The var blocks are generated (D111).

// .160 Smart (Abielle smartgun)
// tg: code/modules/projectiles/ammunition/ballistic/pistol.dm:59
public partial class obj_item_ammo_casing_c160smart : obj_item_ammo_casing
{
    // tg: code/modules/projectiles/ammunition/ballistic/pistol.dm:68
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_caseless)));
        return dot;
    }

    // tg: code/modules/projectiles/ammunition/ballistic/pistol.dm:72
    public override async DmTask ready_proj(atom target = null, mob_living user = null, object quiet = null, string zone_override = null, atom fired_from = null)
    {
        await base.ready_proj(target, user, quiet, zone_override, fired_from);
        if (!isturf(target))
        {
            loaded_projectile.set_homing_target(target);
            obj_effect_temp_visual_smartgun_target.Mark(target);
        }
        else
        {
            // DEVIATION(DV96): the lock never picks the shooter: a 3D shot at the floor or a wall is often that near them
            mob_living aimbot_target = null;
            foreach (mob_living near in Dm.each<mob_living>(range(auto_lock_range, target)))
            {
                if (near != user)
                {
                    aimbot_target = near;
                    break;
                }
            }
            if (aimbot_target != null)
            {
                loaded_projectile.set_homing_target(aimbot_target);
                obj_effect_temp_visual_smartgun_target.Mark(aimbot_target);
            }
        }
    }
}

// tg: code/modules/projectiles/ammunition/ballistic/pistol.dm:83
public partial class obj_effect_temp_visual_smartgun_target : obj_effect_temp_visual
{
    /// <summary>
    /// tg's <c>new /obj/effect/temp_visual/smartgun_target(get_turf(target))</c>. DEVIATION(DV96, the owner 2026-11-21): on a mob it is made inside
    /// it and joins its vis_contents, as a gun's firing effect does, so the clients draw it on the mob the bullets home on, in front of it and
    /// turned to the viewer (tg draws it on that mob's turf, which in 3D hid it in or behind the mob); on anything else it is on the turf.
    /// </summary>
    public static void Mark(atom target)
    {
        if (target is mob_living living && living.gc_destroyed == null)
            Dm.@new<obj_effect_temp_visual_smartgun_target>(living);
        else
            Dm.@new<obj_effect_temp_visual_smartgun_target>(get_turf(target));
    }

    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (loc is atom_movable host)
            Dm.append(host.vis_contents, this);
        return dot;
    }
}
