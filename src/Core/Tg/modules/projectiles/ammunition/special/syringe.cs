// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/ammunition/special/syringe.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only the syringe gun's spring (M8 slice C, D190); the mech syringe gun's branch of ready_proj is not translated
// (mechs, after the core version); the DNA gun's spring (syndicate G4a, D316); the reagent gun's dart synthesiser (syndicate G4b,
// D316). The var blocks are generated.

// tg: code/modules/projectiles/ammunition/special/syringe.dm:1
public partial class obj_item_ammo_casing_syringegun : obj_item_ammo_casing
{
    // tg: code/modules/projectiles/ammunition/special/syringe.dm:9
    public override async DmTask ready_proj(atom target = null, mob_living user = null, object quiet = null, string zone_override = null, atom fired_from = null)
    {
        zone_override ??= "";
        if (loaded_projectile == null)
            return;

        if (loc is obj_item_gun_syringe syringegun)
        {
            if (!Dm.truthy(syringegun.syringes.len))
                return;

            obj_item_reagent_containers_syringe syringe = (obj_item_reagent_containers_syringe)syringegun.syringes[1];
            obj_projectile_bullet_dart dart = (obj_projectile_bullet_dart)loaded_projectile;
            dart.name = syringe.name;
            dart.inject_flags = syringe.inject_flags;
            dart.armour_penetration = syringe.armour_penetration;
            syringegun.syringes.Remove(syringe);
            if (Dm.truthy(syringegun.low_power))
            {
                await insert_syringe(syringe);
                await base.ready_proj(target, user, quiet, zone_override, fired_from);
                return;
            }

            await ((datum_reagents)syringe.reagents).trans_to(dart, ((datum_reagents)syringe.reagents).total_volume, transferred_by: user);
            qdel(syringe);
            await base.ready_proj(target, user, quiet, zone_override, fired_from);
            return;
        }

        if (!istype(loc, typeof(obj_item_mecha_parts_mecha_equipment_medical_syringe_gun)))
        {
            await base.ready_proj(target, user, quiet, zone_override, fired_from);
            return;
        }

        Dm.NotTranslated("code/modules/projectiles/ammunition/special/syringe.dm:35", "ready_proj: the mech syringe gun's dart (mechs, after the core version)");
    }

    // tg: code/modules/projectiles/ammunition/special/syringe.dm:45
    public virtual async DmTask insert_syringe(obj_item_reagent_containers_syringe syringe = null)
    {
        obj_projectile_bullet_dart_syringe dart = (obj_projectile_bullet_dart_syringe)loaded_projectile;
        dart.set_embed((syringe.get_embed() as datum_embedding)?.create_copy());
        ((datum_embedding)dart.get_embed()).embed_chance = 100; // Don't want to fail the shot here
        await syringe.forceMove(dart);
        dart.inner_syringe = syringe;
    }
}

// tg: code/modules/projectiles/ammunition/special/syringe.dm:52  (syndicate G4b, D316)
public partial class obj_item_ammo_casing_chemgun : obj_item_ammo_casing
{
    // tg: code/modules/projectiles/ammunition/special/syringe.dm:58
    public override async DmTask ready_proj(atom target = null, mob_living user = null, object quiet = null, string zone_override = null, atom fired_from = null)
    {
        zone_override ??= "";
        if (loaded_projectile == null)
            return;
        if (loc is obj_item_gun_chem CG)
        {
            if (CG.syringes_left <= 0)
                return;
            await ((datum_reagents)CG.reagents).trans_to(loaded_projectile, 15, transferred_by: user);
            loaded_projectile.name = "piercing chemical dart";
            CG.syringes_left--;
        }
        await base.ready_proj(target, user, quiet, zone_override, fired_from);
    }
}

// tg: code/modules/projectiles/ammunition/special/syringe.dm:70  (syndicate G4a, D316)
public partial class obj_item_ammo_casing_dnainjector : obj_item_ammo_casing
{
    // tg: code/modules/projectiles/ammunition/special/syringe.dm:76
    public override async DmTask ready_proj(atom target = null, mob_living user = null, object quiet = null, string zone_override = null, atom fired_from = null)
    {
        zone_override ??= "";
        if (loaded_projectile == null)
            return;
        if (loc is obj_item_gun_syringe_dna SG)
        {
            if (!Dm.truthy(SG.syringes.len))
                return;

            obj_item_dnainjector S = popleft(SG.syringes) as obj_item_dnainjector;
            obj_projectile_bullet_dnainjector D = loaded_projectile as obj_projectile_bullet_dnainjector;
            await S.forceMove(D);
            D.injector = S;
        }
        await base.ready_proj(target, user, quiet, zone_override, fired_from);
    }
}
