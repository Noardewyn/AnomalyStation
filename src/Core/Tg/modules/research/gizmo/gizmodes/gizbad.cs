// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/gizmo/gizmodes/gizbad.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the cursed gizmo's bad modes: the ominous hum, the explosions (one with fire), the robot spider dispenser, the
// knife, shard and grenade throwers aimed at someone in view, the radiation pulse and the bone breaker. The robot spider explodes
// when it dies and dies from an EMP. The var blocks are generated (D111).

// tg: code/modules/research/gizmo/gizmodes/gizbad.dm:22
public partial class datum_gizpulse_explode : datum_gizpulse
{
    // tg: code/modules/research/gizmo/gizmodes/gizbad.dm:28
    public override async DmTask activate(atom_movable holder = null, datum_gizmodes master = null, datum_gizmo_interface @interface = null)
    {
        await explosion(holder, range_heavy, range_medium, range_light, range_flame);
    }
}

// tg: code/modules/research/gizmo/gizmodes/gizbad.dm:39
public partial class mob_living_basic_spider_robot : mob_living_basic_spider
{
    // tg: code/modules/research/gizmo/gizmodes/gizbad.dm:54
    public override async DmTask<object> death(object gibbed = null, params object[] __extra)
    {
        object dot = await base.death(gibbed, __extra);

        await explosion(this, 0, 0, 2);
        if (prob(80))
            qdel(this);
        return dot;
    }

    // tg: code/modules/research/gizmo/gizmodes/gizbad.dm:61
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);

        await death(); //very sensitive spider robot antennae makes it die fast to emp
        return dot;
    }
}

// tg: code/modules/research/gizmo/gizmodes/gizbad.dm:66
public partial class datum_gizpulse_thrower : datum_gizpulse
{
    // tg: code/modules/research/gizmo/gizmodes/gizbad.dm:76
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        throwing_path = pick_weight(throwables);
    }

    // tg: code/modules/research/gizmo/gizmodes/gizbad.dm:80
    public override async DmTask activate(atom_movable holder = null, datum_gizmodes master = null, datum_gizmo_interface @interface = null)
    {
        obj_item item = Dm.@new(throwing_path as Type, get_turf(holder)) as obj_item;

        DmList targets = Dm.list();
        foreach (mob_living victims in Dm.each<mob_living>(oview(5, holder)))
            Dm.append(ref targets, victims);

        if (!Dm.truthy(targets.len))
            Dm.append(ref targets, get_edge_target_turf(holder, GLOB.alldirs)); // tg passes the list of directions as the direction
        await item.throw_at(pick(targets) as atom, 20, 3);
        await modify(item);
    }

    /// Do some extra modifications if need be
    // tg: code/modules/research/gizmo/gizmodes/gizbad.dm:93
    public virtual async DmTask modify(obj_item item = null)
    {
        await DmTask.CompletedTask;
        return;
    }
}

// tg: code/modules/research/gizmo/gizmodes/gizbad.dm:96
public partial class datum_gizpulse_ominous : datum_gizpulse
{
    // tg: code/modules/research/gizmo/gizmodes/gizbad.dm:96
    public override async DmTask activate(atom_movable holder = null, datum_gizmodes master = null, datum_gizmo_interface @interface = null)
    {
        await DmTask.CompletedTask;
        holder.audible_message(span_hear(I18n.T("You hear an ominous hum.")));
    }
}

// tg: code/modules/research/gizmo/gizmodes/gizbad.dm:99
public partial class datum_gizpulse_thrower_grenade : datum_gizpulse_thrower
{
    // tg: code/modules/research/gizmo/gizmodes/gizbad.dm:111
    public override async DmTask modify(obj_item item = null)
    {
        obj_item_grenade regret = item as obj_item_grenade;
        await regret.arm_grenade();
    }
}

// tg: code/modules/research/gizmo/gizmodes/gizbad.dm:115
public partial class datum_gizpulse_bone_breaker : datum_gizpulse
{
    // tg: code/modules/research/gizmo/gizmodes/gizbad.dm:115
    public override async DmTask activate(atom_movable holder = null, datum_gizmodes master = null, datum_gizmo_interface @interface = null)
    {
        DmList victims = Dm.list();
        foreach (mob_living loser in Dm.each<mob_living>(orange(1, holder)))
            Dm.append(ref victims, loser);

        if (!Dm.truthy(victims.len))
            return;

        mob_living victim = pick(victims) as mob_living;
        await holder.forceMove(get_turf(victim));
        playsound(victim, "sound/effects/wounds/crack2.ogg", 70, TRUE);

        await victim.apply_damage(60, BRUTE, wound_bonus: 100, sharpness: NONE);
        await victim.Stun(2 * SECONDS);
        await victim.Knockdown(5 * SECONDS);

        await victim.emote("scream");
    }
}
