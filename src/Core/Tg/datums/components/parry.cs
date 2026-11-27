// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/parry.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/// Add to a projectile to allow it to be parried by mobs with a certain trait (TRAIT_MINING_PARRYING by default)
// tg: code/datums/components/parry.dm:2
public partial class datum_component_parriable_projectile : datum_component
{
    // tg: code/datums/components/parry.dm:26
    [DmParams("parry_speed_mult", "parry_damage_mult", "boost_speed_mult", "boost_damage_mult", "parry_trait", "grace_period", "parry_callback")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object parry_speed_mult = Dm.arg(__extra, 0);
        object parry_damage_mult = Dm.arg(__extra, 1);
        object boost_speed_mult = Dm.arg(__extra, 2);
        object boost_damage_mult = Dm.arg(__extra, 3);
        object parry_trait = Dm.arg(__extra, 4);
        object grace_period = Dm.arg(__extra, 5);
        datum_callback parry_callback = Dm.arg<datum_callback>(__extra, 6);
        parry_speed_mult ??= 1.25f;
        parry_damage_mult ??= 1.15f;
        boost_speed_mult ??= 1.6f;
        boost_damage_mult ??= 1.5f;
        parry_trait ??= TRAIT_MINING_PARRYING;
        grace_period ??= 0.25f * SECONDS;
        if (!isprojectile(parent))
            return COMPONENT_INCOMPATIBLE;
        this.parry_speed_mult = parry_speed_mult;
        this.parry_damage_mult = parry_damage_mult;
        this.boost_speed_mult = boost_speed_mult;
        this.boost_damage_mult = boost_damage_mult;
        this.parry_trait = parry_trait;
        this.grace_period = grace_period;
        this.parry_callback = parry_callback;
        fire_time = world.time;
        return null;
    }

    // tg: code/datums/components/parry.dm:38
    public override object Destroy(float? force = null)
    {
        foreach (turf parry_turf in Dm.each_cast<turf>(parry_turfs))
            UnregisterSignal(parry_turf, COMSIG_CLICK);
        return base.Destroy(force);
    }

    // tg: code/datums/components/parry.dm:43
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_PROJECTILE_MOVE_PROCESS_STEP, nameof(on_moved));
        RegisterSignal(parent, COMSIG_MOVABLE_MOVED, nameof(before_move));
        RegisterSignal(parent, COMSIG_PROJECTILE_BEFORE_MOVE, nameof(before_move));
        RegisterSignal(parent, COMSIG_PROJECTILE_SELF_PREHIT, nameof(before_hit));
    }

    // tg: code/datums/components/parry.dm:49
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, Dm.list(COMSIG_PROJECTILE_MOVE_PROCESS_STEP, COMSIG_MOVABLE_MOVED, COMSIG_PROJECTILE_BEFORE_MOVE, COMSIG_PROJECTILE_SELF_PREHIT));
    }

    // tg: code/datums/components/parry.dm:52
    [SignalHandler]
    public virtual void before_move(obj_projectile source = null)
    {
        DmList turfs_to_remove = Dm.list();
        foreach (turf parry_turf in Dm.each_cast<turf>(parry_turfs))
        {
            if (Dm.num(parry_turfs[parry_turf]) < world.time)
                Dm.append(turfs_to_remove, parry_turf);
        }

        foreach (turf parry_turf in Dm.each_cast<turf>(turfs_to_remove))
        {
            Dm.remove(ref parry_turfs, parry_turf);
            UnregisterSignal(parry_turf, COMSIG_CLICK);
        }

        DmList parriers_to_remove = Dm.list();
        foreach (mob parrier in Dm.each_cast<mob>(parriers))
        {
            if (Dm.num(parriers[parrier]) < world.time)
                Dm.append(parriers_to_remove, parrier);
        }

        // tg removes the stale parriers from parriers_to_remove itself, so parriers keeps them: kept as tg has it
        foreach (mob parrier in Dm.each_cast<mob>(parriers_to_remove))
            Dm.remove(parriers_to_remove, parrier);
    }

    // tg: code/datums/components/parry.dm:72
    [SignalHandler]
    public virtual void on_moved(obj_projectile source = null)
    {
        if (!isturf(source.loc) || Dm.truthy(parry_turfs[source.loc]))
            return;
        parry_turfs[source.loc] = world.time + Dm.num(grace_period);
        RegisterSignal(source.loc, COMSIG_CLICK, nameof(on_turf_click));
    }

    // tg: code/datums/components/parry.dm:79
    [SignalHandler]
    public virtual void on_turf_click(turf source = null, atom location = null, object control = null, DmList @params = null, mob user = null)
    {
        if (!HAS_TRAIT(user, parry_trait))
            return;
        obj_projectile proj_parent = parent as obj_projectile;
        if (proj_parent.firer == user && (fire_time + Dm.num(grace_period) > world.time) && !Dm.truthy(parried))
        {
            Waiting.NoSleep(attempt_parry(proj_parent, user));
            return;
        }
        parriers[user] = world.time + Dm.num(grace_period);
    }

    // tg: code/datums/components/parry.dm:89
    [SignalHandler]
    public virtual object before_hit(obj_projectile source = null, mob_living user = null)
    {
        if (user == null || !Dm.truthy(parriers[user]) || Dm.truthy(parried))
            return null;

        Dm.remove(ref parriers, user);
        return Waiting.NoSleep(attempt_parry(source, user));
    }

    // tg: code/datums/components/parry.dm:98
    public virtual async DmTask<object> attempt_parry(obj_projectile source = null, mob user = null)
    {
        if (QDELETED(source) || Dm.truthy(source.deletion_queued))
            return NONE;

        if (Dm.truthy(Dm.bitand(!Dm.truthy(user._listen_lookup?[COMSIG_LIVING_PROJECTILE_PARRIED]) ? NONE : user._SendSignal(COMSIG_LIVING_PROJECTILE_PARRIED, Dm.list(user, source)), INTERCEPT_PARRY_EFFECTS)))
            return NONE;

        parried = TRUE;
        if (source.firer != user)
        {
            if (Dm.abs(Dm.num(source.angle) - Dm.num(dir2angle(user.dir))) < 15)
                source.set_angle(Dm.mod(Dm.num(source.angle) + 180, 360) + rand(-3, 3));
            else
                source.set_angle(Dm.num(dir2angle(user.dir)) + rand(-3, 3));
            user.visible_message(span_warning(I18n.T("[] expertly parries [] with [] bare hand!", user, source, user.p_their())), span_warning(I18n.T("You parry [] with your hand!", source)));
        }
        else
            user.visible_message(span_warning(I18n.T("[] boosts [] with [] bare hand!", user, source, user.p_their())), span_warning(I18n.T("You boost [] with your hand!", source)));
        source.firer = user;
        source.speed = Dm.num(source.speed) * Dm.num(source.firer == user ? boost_speed_mult : parry_speed_mult);
        source.damage = Dm.num(source.damage) * Dm.num(source.firer == user ? boost_damage_mult : parry_damage_mult);
        await source.add_atom_colour(COLOR_RED_LIGHT, TEMPORARY_COLOUR_PRIORITY);
        if (parry_callback != null)
            Waiting.NoSleep(parry_callback.Invoke(user));

        user.playsound_local(source.loc, Dm.resource("sound/effects/parry.ogg"), 50, TRUE);
        user.overlay_fullscreen("projectile_parry", typeof(atom_movable_screen_fullscreen_crit_projectile_parry), 2);
        _addtimer(Dm.@new<datum_callback>(user, nameof(mob.clear_fullscreen), "projectile_parry"), 0.25f * SECONDS, file: "code/datums/components/parry.dm", line: 123);
        return PROJECTILE_INTERRUPT_HIT_PHASE;
    }
}
