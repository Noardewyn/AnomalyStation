// Translated from tgstation (snapshot 1d99bd7f): code/datums/actions/mobs/create_legion_turrets.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var blocks are generated.

// tg: code/datums/actions/mobs/create_legion_turrets.dm:1
public partial class datum_action_cooldown_mob_cooldown_create_legion_turrets : datum_action_cooldown_mob_cooldown
{
    // tg: code/datums/actions/mobs/create_legion_turrets.dm:12
    public override async DmTask<object> Activate(atom target_atom = null)
    {
        disable_cooldown_actions();
        await create(target_atom);
        StartCooldown();
        enable_cooldown_actions();
        return TRUE;
    }

    /// Creates new legion turrets around the owner between the minimum and maximum
    // tg: code/datums/actions/mobs/create_legion_turrets.dm:20
    public virtual async DmTask create(atom target = null)
    {
        playsound(owner, Dm.resource("sound/effects/magic/RATTLEMEBONES.ogg"), 100, TRUE);
        DmList possible_locations = Dm.list();
        foreach (turf checked_turf in Dm.each<turf>(oview(owner, 4))) //Only place the turrets on open turfs
        {
            if (Dm.truthy(await checked_turf.is_blocked_turf()))
                continue;
            Dm.append(ref possible_locations, checked_turf);
        }
        float count = min(rand(minimum_turrets, maximum_turrets), Dm.length(possible_locations));
        for (int i = 1; i <= count; i++) //Makes sure aren't spawning in nullspace.
        {
            object chosen = pick_n_take(possible_locations);
            Dm.@new<obj_structure_legionturret>(chosen);
        }
    }
}

/// A basic turret that shoots at nearby mobs. Intended to be used for the legion megafauna.
// tg: code/datums/actions/mobs/create_legion_turrets.dm:32
public partial class obj_structure_legionturret : obj_structure
{
    // tg: code/datums/actions/mobs/create_legion_turrets.dm:57
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        _addtimer(Dm.@new<datum_callback>(this, nameof(set_up_shot)), initial_firing_time, file: "code/datums/actions/mobs/create_legion_turrets.dm", line: 59);
        ADD_TRAIT(this, TRAIT_NO_FLOATING_ANIM, INNATE_TRAIT);
        return dot;
    }

    /// Handles an extremely basic AI
    // tg: code/datums/actions/mobs/create_legion_turrets.dm:63
    public virtual void set_up_shot()
    {
        foreach (mob_living possible_target in Dm.each<mob_living>(oview(9, this)))
        {
            if (IS_UNCONSCIOUS(possible_target))
                continue;
            if (Dm.truthy(faction_check_atom(possible_target)))
                continue;
            fire(possible_target);
            return;
        }
        fire(get_edge_target_turf(this, pick(GLOB.cardinals)) as atom);
    }

    /// Called when attacking a target. Shoots a projectile at the turf underneath the target.
    // tg: code/datums/actions/mobs/create_legion_turrets.dm:74
    public virtual void fire(atom target = null)
    {
        turf target_turf = get_turf(target);
        turf our_turf = get_turf(this);
        if (target_turf == null || our_turf == null)
            return;
        //Now we generate the tracer.
        object angle = get_angle(our_turf, target_turf);
        // DEVIATION(DV623): tg passes the icon file and the state in each other's places (beam.dm:600 takes icon_state first), so its
        // tracer draws nothing; here they go where Beam reads them, so the tracer shows.
        our_turf.Beam(target_turf, "blood_light", Dm.resource("icons/effects/beam.dmi"), time: shot_delay);
        playsound(this, Dm.resource("sound/machines/airlock/airlockopen.ogg"), 100, TRUE);
        _addtimer(Dm.@new<datum_callback>(this, nameof(fire_beam), angle), shot_delay, file: "code/datums/actions/mobs/create_legion_turrets.dm", line: 83);
    }

    /// Called shot_delay after the turret shot the tracer. Shoots a projectile into the same direction.
    // tg: code/datums/actions/mobs/create_legion_turrets.dm:86
    public virtual async DmTask fire_beam(object angle = null)
    {
        obj_projectile ouchie = Dm.@new(projectile_type, loc) as obj_projectile;
        ouchie.firer = this;
        await ouchie.fire(angle);
        playsound(this, Dm.resource("sound/effects/bin/bin_close.ogg"), 100, TRUE);
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), 0.5f * SECONDS > GC_FILTER_QUEUE ? WEAKREF(this) : this), 0.5f * SECONDS, file: "code/datums/actions/mobs/create_legion_turrets.dm", line: 91); // QDEL_IN(src, 0.5 SECONDS)
    }
}
