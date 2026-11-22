// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/projectile/special/gravity.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps GU1, D333): the gravity gun's repulsion, attraction and chaos bolts, which throw every loose thing around
// where they hit and ripple the floor there. The var blocks are generated (D111).

// tg: code/modules/projectiles/projectile/special/gravity.dm:1
public partial class obj_projectile_gravityrepulse : obj_projectile
{
    // tg: code/modules/projectiles/projectile/special/gravity.dm:13
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        obj_item_ammo_casing_energy_gravity_repulse C = loc as obj_item_ammo_casing_energy_gravity_repulse;
        if (C is obj_item_ammo_casing_energy_gravity_repulse) //Hard-coded maximum power so servers can't be crashed by trying to throw the entire Z level's items
            power = Dm.num_or_null(min(C.gun?.power, 15));
        return dot;
    }

    // tg: code/modules/projectiles/projectile/special/gravity.dm:19
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        blocked ??= 0;
        object dot = await base.on_hit(target, blocked, pierce_hit);
        T = get_turf(this);
        foreach (atom_movable A in Dm.each<atom_movable>(Builtins.range(T, power)))
        {
            if (A == this || (Dm.truthy(firer) && A == firer) || Dm.truthy(A.anchored) || Dm.truthy(thrown_items[A]))
                continue;
            if (ismob(A)) //because (ismob(A) && A:mob_negates_gravity()) is a recipe for bugs.
            {
                mob M = A as mob;
                if (Dm.truthy(M.mob_negates_gravity()))
                    continue;
            }
            object throwtarget = get_edge_target_turf(this, get_dir(this, get_step_away(A, this)));
            await A.safe_throw_at(throwtarget as atom, Dm.num(power) + 1, 1, force: MOVE_FORCE_EXTREMELY_STRONG);
            thrown_items[A] = A;
        }
        foreach (turf F in Dm.each<turf>(RANGE_TURFS(power, T)))
            Dm.@new<obj_effect_temp_visual_gravpush>(F);
        return dot;
    }
}

// tg: code/modules/projectiles/projectile/special/gravity.dm:35
public partial class obj_projectile_gravityattract : obj_projectile
{
    // tg: code/modules/projectiles/projectile/special/gravity.dm:47
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        obj_item_ammo_casing_energy_gravity_attract C = loc as obj_item_ammo_casing_energy_gravity_attract;
        if (C is obj_item_ammo_casing_energy_gravity_attract) //Hard-coded maximum power so servers can't be crashed by trying to throw the entire Z level's items
            power = Dm.num_or_null(min(C.gun?.power, 15));
        return dot;
    }

    // tg: code/modules/projectiles/projectile/special/gravity.dm:53
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        blocked ??= 0;
        object dot = await base.on_hit(target, blocked, pierce_hit);
        T = get_turf(this);
        foreach (atom_movable A in Dm.each<atom_movable>(Builtins.range(T, power)))
        {
            if (A == this || (Dm.truthy(firer) && A == firer) || Dm.truthy(A.anchored) || Dm.truthy(thrown_items[A]))
                continue;
            if (ismob(A))
            {
                mob M = A as mob;
                if (Dm.truthy(M.mob_negates_gravity()))
                    continue;
            }
            await A.safe_throw_at(T, Dm.num(power) + 1, 1, force: MOVE_FORCE_EXTREMELY_STRONG);
            thrown_items[A] = A;
        }
        foreach (turf F in Dm.each<turf>(RANGE_TURFS(power, T)))
            Dm.@new<obj_effect_temp_visual_gravpush>(F);
        return dot;
    }
}

// tg: code/modules/projectiles/projectile/special/gravity.dm:68
public partial class obj_projectile_gravitychaos : obj_projectile
{
    // tg: code/modules/projectiles/projectile/special/gravity.dm:80
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        obj_item_ammo_casing_energy_gravity_chaos C = loc as obj_item_ammo_casing_energy_gravity_chaos;
        if (C is obj_item_ammo_casing_energy_gravity_chaos) //Hard-coded maximum power so servers can't be crashed by trying to throw the entire Z level's items
            power = Dm.num_or_null(min(C.gun?.power, 15));
        return dot;
    }

    // tg: code/modules/projectiles/projectile/special/gravity.dm:86
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        blocked ??= 0;
        object dot = await base.on_hit(target, blocked, pierce_hit);
        T = get_turf(this);
        foreach (atom_movable A in Dm.each<atom_movable>(Builtins.range(T, power)))
        {
            if (A == this || (Dm.truthy(firer) && A == firer) || Dm.truthy(A.anchored) || Dm.truthy(thrown_items[A]))
                continue;
            if (ismob(A))
            {
                mob M = A as mob;
                if (Dm.truthy(M.mob_negates_gravity()))
                    continue;
            }
            await A.safe_throw_at(get_edge_target_turf(A, pick(GLOB.cardinals)) as atom, Dm.num(power) + 1, 1, force: MOVE_FORCE_EXTREMELY_STRONG);
            thrown_items[A] = A;
        }
        foreach (turf Z in Dm.each<turf>(RANGE_TURFS(power, T)))
            Dm.@new<obj_effect_temp_visual_gravpush>(Z);
        return dot;
    }
}
