// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/wall_smasher.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var block is generated.

/**
 * # Wall Smasher
 * An element you put on mobs to let their attacks break walls
 * If put in the hands of a player this can cause a lot of problems, be careful
 */
// tg: code/datums/elements/wall_smasher.dm:6
public partial class datum_element_wall_smasher : datum_element
{
    // tg: code/datums/elements/wall_smasher.dm:12
    [DmParams("target", "strength_flag")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        float? strength_flag = Dm.arg<float?>(__extra, 0);
        strength_flag ??= ENVIRONMENT_SMASH_WALLS;
        object dot = await base.Attach(target, Dm.forward(__extra, strength_flag));
        if (Dm.eq(dot, ELEMENT_INCOMPATIBLE))
            return ELEMENT_INCOMPATIBLE;
        if (!isliving(target))
            return ELEMENT_INCOMPATIBLE;

        this.strength_flag = strength_flag;
        RegisterSignal(target, COMSIG_LIVING_UNARMED_ATTACK, nameof(on_unarm_attack)); // Players
        RegisterSignal(target, COMSIG_HOSTILE_PRE_ATTACKINGTARGET, nameof(on_pre_attackingtarget)); // AI

        if (isanimal_or_basicmob(target))
        {
            // tg declares the var a simple animal; a basic mob has a same-named var, set by name
            Dm.set_var(target, "environment_smash", strength_flag);
        }
        return dot;
    }

    // tg: code/datums/elements/wall_smasher.dm:27
    public override object Detach(datum source = null, params object[] __extra)
    {
        UnregisterSignal(source, Dm.list(COMSIG_LIVING_UNARMED_ATTACK, COMSIG_HOSTILE_PRE_ATTACKINGTARGET));
        if (isanimal_or_basicmob(source))
        {
            // initial(animal_target.environment_smash): the type's default, read by name on a basic mob too
            Dm.set_var(source, "environment_smash", Dm.initial(source, "environment_smash"));
        }

        return base.Detach(source, __extra);
    }

    // tg: code/datums/elements/wall_smasher.dm:35
    [SignalHandler]
    public virtual object on_unarm_attack(mob_living puncher = null, atom target = null, object proximity = null, object modifiers = null)
    {
        return Waiting.NoSleep(try_smashing(puncher, target));
    }

    // tg: code/datums/elements/wall_smasher.dm:39
    [SignalHandler]
    public virtual object on_pre_attackingtarget(mob_living puncher = null, atom target = null)
    {
        return Waiting.NoSleep(try_smashing(puncher, target));
    }

    // tg: code/datums/elements/wall_smasher.dm:43
    public virtual async DmTask<object> try_smashing(mob_living puncher = null, atom target = null)
    {
        if (!isturf(target))
            return NONE;
        if (isfloorturf(target))
            return NONE;
        if (isindestructiblewall(target))
            return NONE;

        puncher.changeNext_move(CLICK_CD_MELEE);
        await puncher.do_attack_animation(target);

        if (ismineralturf(target))
        {
            turf_closed_mineral mineral_wall = target as turf_closed_mineral;
            await mineral_wall.gets_drilled(puncher);
            return COMPONENT_HOSTILE_NO_ATTACK;
        }

        if (!iswallturf(target)) // In case you're some kind of non-wall non-mineral closed turf yet to be invented
            return COMPONENT_HOSTILE_NO_ATTACK;

        turf_closed_wall wall_turf = target as turf_closed_wall;

        if (wall_turf is turf_closed_wall_r_wall && !Dm.eq(strength_flag, ENVIRONMENT_SMASH_RWALLS))
        {
            playsound(wall_turf, Dm.resource("sound/effects/bang.ogg"), 50, vary: TRUE);
            wall_turf.balloon_alert(puncher, "too tough!");
            return COMPONENT_HOSTILE_NO_ATTACK;
        }

        await wall_turf.dismantle_wall(devastated: TRUE);
        playsound(wall_turf, Dm.resource("sound/effects/meteorimpact.ogg"), 100, vary: TRUE);
        return COMPONENT_HOSTILE_NO_ATTACK;
    }
}
