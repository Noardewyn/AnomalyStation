// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/legionnaire.dm
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
 * # Legionnaire
 *
 * A towering skeleton, embodying the power of Legion.
 * As its health gets lower, the head does more damage.
 * Its attacks are as follows:
 * - Charges at the target after a telegraph, throwing them across the arena should it connect.
 * - Legionnaire's head detaches, attacking as its own entity.  Has abilities of its own later into the fight.  Once dead, regenerates after a brief period.  If the skill is used while the head is off, it will be killed.
 * - Leaves a pile of bones at your location.  Upon using this skill again, you'll swap locations with the bone pile.
 * - Spews a cloud of smoke from its maw, wherever said maw is.
 * A unique fight incorporating the head mechanic of legion into a whole new beast.  Combatants will need to make sure the tag-team of head and body don't lure them into a deadly trap.
 */
// tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/legionnaire.dm:19
public partial class mob_living_simple_animal_hostile_asteroid_elite_legionnaire : mob_living_simple_animal_hostile_asteroid_elite
{
    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/legionnaire.dm:1-4 (file-local #defines)
    private const float LEGIONNAIRE_CHARGE = 1;
    private const float HEAD_DETACH = 2;
    private const float BONFIRE_TELEPORT = 3;
    private const float SPEW_SMOKE = 4;

    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/legionnaire.dm:80
    public override async DmTask OpenFire(atom A = null)
    {
        if (client != null)
        {
            if (chosen_attack == LEGIONNAIRE_CHARGE)
                legionnaire_charge(target);
            else if (chosen_attack == HEAD_DETACH)
                await head_detach(target);
            else if (chosen_attack == BONFIRE_TELEPORT)
                await bonfire_teleport();
            else if (chosen_attack == SPEW_SMOKE)
                await spew_smoke();
            return;
        }
        float aiattack = rand(1, 4);
        if (aiattack == LEGIONNAIRE_CHARGE)
            legionnaire_charge(target);
        else if (aiattack == HEAD_DETACH)
            await head_detach(target);
        else if (aiattack == BONFIRE_TELEPORT)
            await bonfire_teleport();
        else if (aiattack == SPEW_SMOKE)
            await spew_smoke();
    }

    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/legionnaire.dm:103
    public override async DmTask<object> Move(atom newloc = null, object direct = null, float? glide_size_override = null, float? update_dir = null)
    {
        if (Dm.truthy(charging))
            return FALSE;
        return await base.Move(newloc, direct, glide_size_override, update_dir);
    }

    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/legionnaire.dm:108
    public override object MiddleClickOn(atom A = null, object @params = null)
    {
        object dot = base.MiddleClickOn(A, @params);
        if (myhead == null)
            return dot;
        turf T = get_turf(A);
        if (T != null)
        {
            myhead.LoseTarget();
            myhead.Goto(T, myhead.move_to_delay);
        }
        return dot;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/legionnaire.dm:117
    public virtual void legionnaire_charge(object target = null)
    {
        ranged_cooldown = world.time + 4.0f * SECONDS;
        charging = TRUE;
        object dir_to_target = get_dir(get_turf(this), get_turf(target as atom));
        turf T = get_step(get_turf(this), Dm.num(dir_to_target));
        for (float i = 1, i__end = 6; i <= i__end; i++)
        {
            Dm.@new<obj_effect_temp_visual_dragon_swoop_legionnaire>(T);
            T = get_step(T, Dm.num(dir_to_target));
        }
        playsound(this, Dm.resource("sound/effects/magic/demon_attack1.ogg"), 200, 1);
        visible_message(span_boldwarning(I18n.T("[] prepares to charge!", this)));
        _addtimer(Dm.@new<datum_callback>(this, nameof(legionnaire_charge_2), dir_to_target, 0), 0.4f * SECONDS, file: "code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/legionnaire.dm", line: 128);
    }

    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/legionnaire.dm:129
    public virtual async DmTask legionnaire_charge_2(object move_dir = null, object times_ran = null)
    {
        if (Dm.num(times_ran) >= 6)
        {
            charging = FALSE;
            return;
        }
        turf T = get_step(get_turf(this), Dm.num(move_dir));
        if (ismineralturf(T))
        {
            turf_closed_mineral M = T as turf_closed_mineral;
            await M.gets_drilled();
            T = (turf)turf.dm_identity(T); // TURF-IDENTITY: tg's T names the cell, the floor the rock left from here on
        }
        if (Dm.truthy(T.density))
        {
            charging = FALSE;
            return;
        }
        foreach (obj_structure_window W in Dm.each<obj_structure_window>(T.contents))
        {
            charging = FALSE;
            return;
        }
        foreach (obj_machinery_door D in Dm.each<obj_machinery_door>(T.contents))
        {
            if (Dm.truthy(D.density))
            {
                charging = FALSE;
                return;
            }
        }
        await forceMove(T);
        playsound(this, Dm.resource("sound/effects/bang.ogg"), 200, 1);
        DmList hit_things = Dm.list();
        object throwtarget = get_edge_target_turf(this, move_dir);
        foreach (mob_living trample_target in Dm.each<mob_living>(Dm.sub(Dm.sub(T.contents, hit_things), this)))
        {
            Dm.append(hit_things, trample_target);
            if (Dm.truthy(faction_check_atom(trample_target)))
                continue;
            visible_message(span_boldwarning(I18n.T("[] tramples and kicks []!", this, trample_target)));
            to_chat(trample_target, span_userdanger(I18n.T("[] tramples you and kicks you away!", this)));
            await trample_target.safe_throw_at(throwtarget as atom, 10, 1, this);
            await trample_target.Paralyze(20);
            await trample_target.adjust_brute_loss(melee_damage_upper);
        }
        _addtimer(Dm.@new<datum_callback>(this, nameof(legionnaire_charge_2), move_dir, (Dm.num(times_ran) + 1)), 0.7f, file: "code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/legionnaire.dm", line: 163);
    }

    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/legionnaire.dm:162
    public virtual async DmTask head_detach(object target = null)
    {
        ranged_cooldown = world.time + 1 * SECONDS;
        if (myhead != null)
        {
            await myhead.adjust_brute_loss(600);
            return;
        }
        if (Dm.truthy(has_head))
        {
            has_head = FALSE;
            icon_state = "legionnaire_headless";
            icon_living = "legionnaire_headless";
            icon_aggro = "legionnaire_headless";
            visible_message(span_boldwarning(I18n.T("[]'s head flies off!", this)));
            mob_living_simple_animal_hostile_asteroid_elite_legionnairehead newhead = Dm.@new<mob_living_simple_animal_hostile_asteroid_elite_legionnairehead>(loc);
            newhead.GiveTarget(target);
            // SET_FACTION_AND_ALLIES_FROM(newhead, src) (__DEFINES/mobfactions.dm:119), inline
            newhead.set_faction(get_faction());
            newhead.set_allies(LAZYLISTDUPLICATE(allies));
            myhead = newhead;
            myhead.body = this;
            if (Dm.num(health) < Dm.num(maxHealth) * 0.25f)
            {
                myhead.melee_damage_lower = 40;
                myhead.melee_damage_upper = 40;
            }
            else if (Dm.num(health) < Dm.num(maxHealth) * 0.5f)
            {
                myhead.melee_damage_lower = 30;
                myhead.melee_damage_upper = 30;
            }
        }
    }

    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/legionnaire.dm:185
    public virtual void onHeadDeath()
    {
        myhead = null;
        _addtimer(Dm.@new<datum_callback>(this, nameof(regain_head)), 5 * SECONDS, file: "code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/legionnaire.dm", line: 191);
    }

    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/legionnaire.dm:189
    public virtual void regain_head()
    {
        has_head = TRUE;
        if (stat == DEAD)
            return;
        icon_state = "legionnaire";
        icon_living = "legionnaire";
        icon_aggro = "legionnaire";
        visible_message(span_boldwarning(I18n.T("The top of []'s spine leaks a black liquid, forming into a skull!", this)));
    }

    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/legionnaire.dm:198
    public virtual async DmTask bonfire_teleport()
    {
        ranged_cooldown = world.time + 5;
        if (mypile == null)
        {
            obj_structure_legionnaire_bonfire newpile = Dm.@new<obj_structure_legionnaire_bonfire>(loc);
            mypile = newpile;
            mypile.myowner = this;
            playsound(get_turf(this), Dm.resource("sound/items/fulton/fultext_deploy.ogg"), 200, 1);
            visible_message(span_boldwarning(I18n.T("[] summons a bonfire on []!", this, get_turf(this))));
            return;
        }
        else
        {
            turf legionturf = get_turf(this);
            turf pileturf = get_turf(mypile);
            if (legionturf == pileturf)
            {
                await mypile.take_damage(100);
                mypile = null;
                return;
            }
            playsound(pileturf, Dm.resource("sound/items/fulton/fultext_deploy.ogg"), 200, 1);
            playsound(legionturf, Dm.resource("sound/items/fulton/fultext_deploy.ogg"), 200, 1);
            visible_message(span_boldwarning(I18n.T("[] melts down into a burning pile of bones!", this)));
            await forceMove(pileturf);
            visible_message(span_boldwarning(I18n.T("[] forms from the bonfire!", this)));
            await mypile.forceMove(legionturf);
        }
    }

    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/legionnaire.dm:221
    public virtual async DmTask spew_smoke()
    {
        ranged_cooldown = world.time + 4 * SECONDS;
        turf smoke_location = null;
        if (myhead != null)
            smoke_location = get_turf(myhead);
        else
            smoke_location = get_turf(this);
        if (myhead != null)
            myhead.visible_message(span_boldwarning(I18n.T("[] spews smoke from its maw!", myhead)));
        else if (!Dm.truthy(has_head))
            visible_message(span_boldwarning(I18n.T("[] spews smoke from the tip of their spine!", this)));
        else
            visible_message(span_boldwarning(I18n.T("[] spews smoke from its maw!", this)));
        await do_smoke(2, this, smoke_location);
    }
}

//The legionnaire's head.  Basically the same as any legion head, but we have to tell our creator when we die so they can generate another head.
// tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/legionnaire.dm:237
public partial class mob_living_simple_animal_hostile_asteroid_elite_legionnairehead : mob_living_simple_animal_hostile_asteroid_elite
{
    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/legionnaire.dm:262
    public override async DmTask<object> death(object gibbed = null, params object[] __extra)
    {
        object dot = await base.death(gibbed, __extra);
        if (body != null)
            body.onHeadDeath();
        return dot;
    }
}

//The legionnaire's bonfire, which can be swapped positions with.  Also sets flammable living beings on fire when they walk over it.
// tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/legionnaire.dm:268
public partial class obj_structure_legionnaire_bonfire : obj_structure
{
    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/legionnaire.dm:281
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        DmList loc_connections = TgState.Current.obj_structure_legionnaire_bonfire__Initialize__loc_connections ??= Dm.list(
            Dm.kv(COMSIG_ATOM_ENTERED, nameof(on_entered)));
        Waiting.NoSleep(AddElement(typeof(datum_element_connect_loc), loc_connections));
        return dot;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/legionnaire.dm:288
    [SignalHandler]
    public virtual void on_entered(datum source = null, atom_movable mover = null)
    {
        if (isobj(mover))
        {
            obj @object = mover as obj;
            Waiting.NoSleep(@object.fire_act(1000, 500));
        }
        if (isliving(mover))
        {
            mob_living fire_walker = mover as mob_living;
            Waiting.NoSleep(fire_walker.adjust_fire_stacks(5));
            fire_walker.ignite_mob();
        }
    }

    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/legionnaire.dm:298
    public override object Destroy(float? force = null)
    {
        if (myowner != null)
            myowner.mypile = null;
        return base.Destroy(force);
    }
}

//The visual effect which appears in front of legionnaire when he goes to charge.
// tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/legionnaire.dm:304
public partial class obj_effect_temp_visual_dragon_swoop_legionnaire : obj_effect_temp_visual_dragon_swoop
{
    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/legionnaire.dm:308
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        transform = Dm.mul(transform, 0.33f);
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/legionnaire.dm:286 (var/static/list/loc_connections)
    internal DmList obj_structure_legionnaire_bonfire__Initialize__loc_connections;
}
