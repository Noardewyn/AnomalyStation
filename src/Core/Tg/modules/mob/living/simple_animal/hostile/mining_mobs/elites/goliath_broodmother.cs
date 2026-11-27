// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/goliath_broodmother.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (the attack actions are var-only). The var blocks are generated.

/**
 * # Goliath Broodmother
 *
 * A stronger, faster variation of the goliath.  Has the ability to spawn baby goliaths, which it can later detonate at will.
 * When its health is below half, tendrils will spawn randomly around it.  When it is below a quarter of health, this effect is doubled.
 * Its attacks are as follows:
 * - Spawns a 3x3/plus shape of tentacles on the target location
 * - Spawns 2 baby goliaths on its tile, up to a max of 8.  Children blow up when they die.
 * - The broodmother lets out a noise, and is able to move faster for 6.5 seconds.
 * - Summons your children around you.
 * The broodmother is a fight revolving around stage control, as the activator has to manage the baby goliaths and the broodmother herself, along with all the tendrils.
 */
// tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/goliath_broodmother.dm:19
public partial class mob_living_simple_animal_hostile_asteroid_elite_broodmother : mob_living_simple_animal_hostile_asteroid_elite
{
    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/goliath_broodmother.dm:1-4 (file-local #defines)
    private const float TENTACLE_PATCH = 1;
    private const float SPAWN_CHILDREN = 2;
    private const float RAGE = 3;
    private const float CALL_CHILDREN = 4;

    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/goliath_broodmother.dm:56
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_TENTACLE_IMMUNE, INNATE_TRAIT);
        return dot;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/goliath_broodmother.dm:84
    public override async DmTask OpenFire(atom A = null)
    {
        if (client != null)
        {
            if (chosen_attack == TENTACLE_PATCH)
                tentacle_patch(target);
            else if (chosen_attack == SPAWN_CHILDREN)
                await spawn_children();
            else if (chosen_attack == RAGE)
                await rage();
            else if (chosen_attack == CALL_CHILDREN)
                await call_children();
            return;
        }
        float aiattack = rand(1, 4);
        if (aiattack == TENTACLE_PATCH)
            tentacle_patch(target);
        else if (aiattack == SPAWN_CHILDREN)
            await spawn_children();
        else if (aiattack == RAGE)
            await rage();
        else if (aiattack == CALL_CHILDREN)
            await call_children();
    }

    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/goliath_broodmother.dm:107
    public override object Life(object seconds_per_tick = null, params object[] __extra)
    {
        seconds_per_tick ??= SSMOBS_DT();
        object dot = base.Life(seconds_per_tick, __extra);
        if (!Dm.truthy(dot)) //Checks if they are dead as a rock.
            return dot;
        if (Dm.num(health) >= Dm.num(maxHealth) * 0.5f || !(random_tentacle <= world.time)) // COOLDOWN_FINISHED, inline
            return dot;
        random_tentacle = world.time + 3 * SECONDS; // COOLDOWN_START, inline
        float tentacle_amount = 5;
        if (Dm.num(health) < Dm.num(maxHealth) * 0.25f)
            tentacle_amount = 10;
        DmList possible_turfs = RANGE_TURFS(5, get_turf(this));
        for (float i = 1, i__end = tentacle_amount; i <= i__end; i++)
        {
            turf innsmouth = pick_n_take(possible_turfs) as turf;
            Dm.@new<obj_effect_goliath_tentacle_broodmother>(innsmouth, this);
        }
        return dot;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/goliath_broodmother.dm:122
    public virtual void tentacle_patch(object target = null)
    {
        ranged_cooldown = world.time + 15;
        object tturf = get_turf(target as atom);
        if (!isturf(tturf))
            return;
        visible_message(span_warning(I18n.T("[] digs its tentacles under []!", this, target)));
        Dm.@new<obj_effect_goliath_tentacle_broodmother_patch>(tturf, this);
    }

    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/goliath_broodmother.dm:130
    public virtual async DmTask spawn_children(object target = null)
    {
        ranged_cooldown = world.time + 40;
        visible_message(span_boldwarning(I18n.T("The ground churns behind []!", this)));
        for (float i = 1, i__end = 2; i <= i__end; i++)
        {
            if (children_list.len >= 8)
                return;
            mob_living_simple_animal_hostile_asteroid_elite_broodmother_child new_child = Dm.@new<mob_living_simple_animal_hostile_asteroid_elite_broodmother_child>(loc);
            new_child.GiveTarget(target);
            // SET_FACTION_AND_ALLIES_FROM(new_child, src) (__DEFINES/mobfactions.dm:119), inline
            new_child.set_faction(get_faction());
            new_child.set_allies(LAZYLISTDUPLICATE(allies));
            visible_message(span_boldwarning(I18n.T("[] appears below []!", new_child, this)));
            register_child(new_child);
        }
        await DmTask.CompletedTask;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/goliath_broodmother.dm:142
    public virtual void register_child(atom child = null)
    {
        Dm.append(children_list, child);
        RegisterSignals(child, Dm.list(COMSIG_QDELETING, COMSIG_LIVING_DEATH), nameof(remove_child));
    }

    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/goliath_broodmother.dm:146
    [SignalHandler]
    public virtual void remove_child(atom source = null)
    {
        Dm.remove(children_list, source);
        UnregisterSignal(source, Dm.list(
            COMSIG_QDELETING,
            COMSIG_LIVING_DEATH));
    }

    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/goliath_broodmother.dm:157
    public virtual async DmTask rage()
    {
        ranged_cooldown = world.time + 100;
        playsound(this, Dm.resource("sound/misc/insane_low_laugh.ogg"), 200, 1);
        visible_message(span_warning(I18n.T("[] starts picking up speed!", this)));
        color = COLOR_RED;
        await set_varspeed(0);
        move_to_delay = 3;
        _addtimer(Dm.@new<datum_callback>(this, nameof(reset_rage)), 6.5f * SECONDS, file: "code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/goliath_broodmother.dm", line: 167);
    }

    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/goliath_broodmother.dm:166
    public virtual async DmTask reset_rage()
    {
        color = COLOR_WHITE;
        await set_varspeed(2);
        move_to_delay = 5;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/goliath_broodmother.dm:171
    public virtual async DmTask call_children()
    {
        ranged_cooldown = world.time + 60;
        visible_message(span_warning(I18n.T("The ground shakes near []!", this)));
        DmList directions = Dm.add(GLOB.cardinals.Copy(), GLOB.diagonals.Copy()) as DmList;
        foreach (mob child in Dm.each<mob>(children_list))
        {
            object spawndir = pick_n_take(directions);
            turf T = get_step(this, Dm.num(spawndir));
            if (T != null)
            {
                await child.forceMove(T);
                playsound(this, Dm.resource("sound/effects/bamf.ogg"), 100, 1);
            }
        }
    }
}

//The goliath's children.  Pretty weak, simple mobs which are able to put a single tentacle under their target when at range.
// tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/goliath_broodmother.dm:183
public partial class mob_living_simple_animal_hostile_asteroid_elite_broodmother_child : mob_living_simple_animal_hostile_asteroid_elite
{
    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/goliath_broodmother.dm:209
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_TENTACLE_IMMUNE, INNATE_TRAIT);
        return dot;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/goliath_broodmother.dm:213
    public override async DmTask OpenFire(atom target = null)
    {
        ranged_cooldown = world.time + 40;
        object tturf = get_turf(target);
        if (!isturf(tturf))
            return;
        if (Dm.num(get_dist(this, target)) <= 7) //Screen range check, so it can't attack people off-screen
        {
            visible_message(span_warning(I18n.T("[] digs one of its tentacles under []!", this, target)));
            Dm.@new<obj_effect_goliath_tentacle_broodmother>(tturf, this);
        }
        await DmTask.CompletedTask;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/goliath_broodmother.dm:222
    public override async DmTask<object> death(object gibbed = null, params object[] __extra)
    {
        object dot = await base.death(gibbed, __extra);
        visible_message(span_warning(I18n.T("[] explodes!", this)));
        await explosion(this, flame_range: 3, adminlog: FALSE);
        await gib(DROP_ALL_REMAINS);
        return dot;
    }
}

// tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/goliath_broodmother.dm:233
public partial class obj_effect_goliath_tentacle_broodmother_patch : obj_effect_goliath_tentacle_broodmother
{
    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/goliath_broodmother.dm:233
    [DmParams("mapload", "goliath")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.Detach(createpatch());
        return dot;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/goliath_broodmother.dm:237
    public virtual async DmTask createpatch()
    {
        object tentacle_locs = spiral_range_turfs(1, get_turf(this));
        foreach (object T in Dm.each(tentacle_locs))
            Dm.@new<obj_effect_goliath_tentacle_broodmother>(T, owner);
        DmList directions = GLOB.cardinals.Copy();
        foreach (object i in Dm.each(directions))
        {
            turf T = get_step(get_turf(this), Dm.num(i));
            T = get_step(T, Dm.num(i));
            Dm.@new<obj_effect_goliath_tentacle_broodmother>(T, owner);
        }
        await DmTask.CompletedTask;
    }
}
