// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/projectile/beams.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The file whole but the alien shrink bolt (/obj/projectile/magic/shrink/alien, a var block under the inert magic bolts: the abductors'
// shrink ray and the wizard's magic, not ported, D333): /obj/projectile/beam/emitter (the M7 projectiles slice, D165: the emitter's shot;
// /obj/projectile/beam and /obj/projectile/beam/emitter/hitscan declare no procs), /obj/projectile/beam/laser (M7x slice B: the laser
// gun's bolt, which sets a carbon on fire; the disabler, the rapid, practice and hellfire beams declare no procs); gaps GU1 (D333): the
// accelerator laser growing with each tile it flies, the flare that marks its target, the pulse beams that blast walls and structures
// (the heavy one through two targets), and the emitter's lens beams (bioregen heals, incendiary sets on fire, psy saddens, magnetic
// pulls, blast explodes). The var blocks are generated (D111).

// tg: code/modules/projectiles/projectile/beams.dm:248
public partial class obj_projectile_beam_emitter : obj_projectile_beam
{
    // tg: code/modules/projectiles/projectile/beams.dm:257
    public override async DmTask<object> singularity_pull(atom singularity = null, object current_size = null)
    {
        return null; //don't want the emitters to miss
    }
}

// tg: code/modules/projectiles/projectile/beams.dm:19
public partial class obj_projectile_beam_laser : obj_projectile_beam
{
    // tg: code/modules/projectiles/projectile/beams.dm:98
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        blocked ??= 0;
        object dot = await base.on_hit(target, blocked, pierce_hit);
        if (iscarbon(target))
        {
            mob_living_carbon M = target as mob_living_carbon;
            M.ignite_mob();
        }
        else if (isturf(target))
        {
            impact_effect_type = typeof(obj_effect_temp_visual_impact_effect_red_laser_wall);
        }
        return dot;
    }
}

// tg: code/modules/projectiles/projectile/beams.dm:51
public partial class obj_projectile_beam_laser_accelerator : obj_projectile_beam_laser
{
    // tg: code/modules/projectiles/projectile/beams.dm:59
    public override async DmTask<object> reduce_range()
    {
        await base.reduce_range();
        damage = Dm.num(damage) + 7;
        transform = matrix();
        transform = Dm.mul(transform, min(1 + Dm.num(Dm.sub(maximum_range, range)) * size_per_tile, max_scale));
        return null;
    }
}

// tg: code/modules/projectiles/projectile/beams.dm:74
public partial class obj_projectile_beam_laser_flare : obj_projectile_beam_laser
{
    // tg: code/modules/projectiles/projectile/beams.dm:83
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        object dot = await base.on_hit(target, blocked, pierce_hit);
        if (!isliving(target))
            return dot;
        mob_living designated_target = target as mob_living;
        await designated_target.apply_status_effect(typeof(datum_status_effect_designated_target));
        return dot;
    }
}

// tg: code/modules/projectiles/projectile/beams.dm:214
public partial class obj_projectile_beam_pulse : obj_projectile_beam
{
    // tg: code/modules/projectiles/projectile/beams.dm:225
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        blocked ??= 0;
        object dot = await base.on_hit(target, blocked, pierce_hit);
        if (!QDELETED(target) && (isturf(target) || isstructure(target)))
        {
            // DEVIATION(DV577): the cell's own ceiling it hit takes the heavy blast (it has no atom: ShotGeometry.HitCeiling), not the floor under it
            if (dm_hit_ceiling && target is turf cell)
                CeilingSystem.Of(World.Current).ExAct((int)Dm.num(cell.x), (int)Dm.num(cell.y), (int)Dm.num(cell.z), EXPLODE_HEAVY, null);
            else if (isobj(target))
                Dm.append(ref SSexplosions.med_mov_atom, target);
            else
                Dm.append(ref SSexplosions.medturf, target);
        }
        return dot;
    }
}

// tg: code/modules/projectiles/projectile/beams.dm:236
public partial class obj_projectile_beam_pulse_heavy : obj_projectile_beam_pulse
{
    // tg: code/modules/projectiles/projectile/beams.dm:242
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        blocked ??= 0;
        if (pierce_hits <= 0)
            projectile_piercing = NONE;
        pierce_hits -= 1;
        return await base.on_hit(target, blocked, pierce_hit);
    }
}

// tg: code/modules/projectiles/projectile/beams.dm:296
public partial class obj_projectile_beam_emitter_hitscan_bioregen : obj_projectile_beam_emitter_hitscan
{
    // tg: code/modules/projectiles/projectile/beams.dm:309
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        object dot = await base.on_hit(target, blocked, pierce_hit);
        if (!iscarbon(target))
            return dot;
        mob_living_carbon healed_guy = target as mob_living_carbon;
        await healed_guy.heal_bodypart_damage(brute: healing_done, burn: healing_done, updating_health: FALSE);
        return dot;
    }
}

// tg: code/modules/projectiles/projectile/beams.dm:316
public partial class obj_projectile_beam_emitter_hitscan_incend : obj_projectile_beam_emitter_hitscan
{
    // tg: code/modules/projectiles/projectile/beams.dm:330
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        object dot = await base.on_hit(target, blocked, pierce_hit);
        if (!isliving(target))
            return dot;
        mob_living burnt_guy = target as mob_living;
        await burnt_guy.adjust_fire_stacks(5);
        burnt_guy.ignite_mob();
        return dot;
    }
}

// tg: code/modules/projectiles/projectile/beams.dm:338
public partial class obj_projectile_beam_emitter_hitscan_psy : obj_projectile_beam_emitter_hitscan
{
    // tg: code/modules/projectiles/projectile/beams.dm:351
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        object dot = await base.on_hit(target, blocked, pierce_hit);
        if (!isliving(target))
            return dot;
        mob_living siphoned = target as mob_living;
        if (siphoned.mob_mood == null)
            return dot; // tg reads mob_mood unguarded: a moodless mob's runtime ends on_hit here (kept, without the runtime, DV1)
        await siphoned.mob_mood.adjust_sanity(-25);
        to_chat(siphoned, span_warning(I18n.T("Suddenly, everything feels just a little bit worse!")));
        return dot;
    }
}

// tg: code/modules/projectiles/projectile/beams.dm:359
public partial class obj_projectile_beam_emitter_hitscan_magnetic : obj_projectile_beam_emitter_hitscan
{
    // tg: code/modules/projectiles/projectile/beams.dm:370
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        object dot = await base.on_hit(target, blocked, pierce_hit);
        turf turf_to_magnet = get_turf(target);
        await goonchem_vortex(turf_to_magnet, FALSE, 4);
        return dot;
    }
}

// tg: code/modules/projectiles/projectile/beams.dm:375
public partial class obj_projectile_beam_emitter_hitscan_blast : obj_projectile_beam_emitter_hitscan
{
    // tg: code/modules/projectiles/projectile/beams.dm:389
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        object dot = await base.on_hit(target, blocked, pierce_hit);
        turf turf_to_explode = get_turf(target);
        await explosion(turf_to_explode, 0, 1, 2);
        return dot;
    }
}
