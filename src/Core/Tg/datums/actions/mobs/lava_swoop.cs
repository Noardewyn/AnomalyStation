// Translated from tgstation (snapshot 1d99bd7f): code/datums/actions/mobs/lava_swoop.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var blocks are generated.

// tg: code/datums/actions/mobs/lava_swoop.dm:4
public partial class datum_action_cooldown_mob_cooldown_lava_swoop : datum_action_cooldown_mob_cooldown
{
    // tg: code/datums/actions/mobs/lava_swoop.dm:1-2 (file-local defines)
    private const float SWOOP_HEIGHT = 270; //how high up swoops go, in pixels
    private const float SWOOP_DIRECTION_CHANGE_RANGE = 5; //the range our x has to be within to not change the direction we slam from

    // tg: code/datums/actions/mobs/lava_swoop.dm:15
    public override async DmTask<object> Grant(mob granted_to = null, params object[] __extra)
    {
        object dot = await base.Grant(granted_to, __extra);
        granted_to.add_traits(Dm.list(TRAIT_LAVA_IMMUNE, TRAIT_NOFIRE), ACTION_TRAIT);
        return dot;
    }

    // tg: code/datums/actions/mobs/lava_swoop.dm:19
    public override async DmTask Remove(mob removed_from = null)
    {
        await base.Remove(removed_from);
        removed_from.remove_traits(Dm.list(TRAIT_LAVA_IMMUNE, TRAIT_NOFIRE), ACTION_TRAIT);
    }

    // tg: code/datums/actions/mobs/lava_swoop.dm:23
    public override async DmTask<object> Activate(atom target_atom = null)
    {
        disable_cooldown_actions();
        await attack_sequence(target_atom);
        StartCooldown();
        enable_cooldown_actions();
        return TRUE;
    }

    // tg: code/datums/actions/mobs/lava_swoop.dm:30
    public virtual async DmTask attack_sequence(atom target = null)
    {
        if (Dm.truthy(enraged))
        {
            await swoop_attack(target, TRUE);
            return;
        }
        Waiting.Detach(lava_pools(target)); // INVOKE_ASYNC
        await swoop_attack(target);
    }

    // tg: code/datums/actions/mobs/lava_swoop.dm:37
    public virtual async DmTask swoop_attack(atom target = null, float? lava_arena = null)
    {
        lava_arena ??= FALSE;
        if (Dm.truthy(swooping) || target == null)
            return;
        // stop swooped target movement
        swooping = TRUE;
        owner.add_traits(Dm.list(TRAIT_GODMODE, TRAIT_UNDENSE), SWOOPING_TRAIT);
        owner.visible_message(span_boldwarning(I18n.T("[] swoops up high!", owner)));

        bool negative = false;
        float initial_x = owner.x;
        if (target.x < initial_x) //if the target's x is lower than ours, swoop to the left
            negative = true;
        else if (target.x > initial_x)
            negative = false;
        else if (target.x == initial_x) //if their x is the same, pick a direction
            negative = prob(50);
        obj_effect_temp_visual_dragon_flight flight_vis = Dm.@new<obj_effect_temp_visual_dragon_flight>(owner.loc, Dm.num(negative));

        negative = !negative; //invert it for the swoop down later

        object oldtransform = owner.transform;
        owner.alpha = 255;
        animate(owner, Dm.named("alpha", 204), Dm.named("transform", Dm.mul(Dm.matrix(), 0.9f)), Dm.named("time", 3), Dm.named("easing", BOUNCE_EASING));
        for (float i = 1; i <= 3; i++)
        {
            await sleep(0.1f * SECONDS);
            if (QDELETED(owner) || owner.stat == DEAD) //we got hit and died, rip us
            {
                qdel(flight_vis);
                if (owner.stat == DEAD)
                {
                    swooping = FALSE;
                    animate(owner, Dm.named("alpha", 255), Dm.named("transform", oldtransform), Dm.named("time", 0), Dm.named("flags", ANIMATION_END_NOW)); //reset immediately
                }
                return;
            }
        }
        animate(owner, Dm.named("alpha", 100), Dm.named("transform", Dm.mul(Dm.matrix(), 0.7f)), Dm.named("time", 7));
        owner.SEND_SIGNAL(COMSIG_SWOOP_INVULNERABILITY_STARTED, Dm.list(owner));

        owner.mouse_opacity = MOUSE_OPACITY_TRANSPARENT;
        // SLEEP_CHECK_DEATH(7, owner) (__DEFINES/mobs.dm:627), expanded inline
        await sleep(7);
        if (QDELETED(owner) || owner.stat == DEAD)
            return;

        turf target_turf = get_turf(target);
        while (!QDELETED(target) && owner.loc != target_turf && owner.z == target_turf.z)
        {
            await owner.forceMove(get_step(owner, get_dir(owner, target_turf)));
            // SLEEP_CHECK_DEATH(0.5, owner), expanded inline
            await sleep(0.5f);
            if (QDELETED(owner) || owner.stat == DEAD)
                return;
            target_turf = get_turf(target);
        }

        // Ash drake flies onto its target and rains fire down upon them
        float descentTime = 10;
        object lava_success = TRUE;
        if (Dm.truthy(lava_arena))
            lava_success = await this.lava_arena(target);

        //ensure swoop direction continuity.
        if (negative)
        {
            if (ISINRANGE(owner.x, initial_x + 1, initial_x + SWOOP_DIRECTION_CHANGE_RANGE))
                negative = false;
        }
        else
        {
            if (ISINRANGE(owner.x, initial_x - SWOOP_DIRECTION_CHANGE_RANGE, initial_x - 1))
                negative = true;
        }
        Dm.@new<obj_effect_temp_visual_dragon_flight_end>(owner.loc, Dm.num(negative));
        Dm.@new<obj_effect_temp_visual_dragon_swoop>(owner.loc);
        animate(owner, Dm.named("alpha", 255), Dm.named("transform", oldtransform), descentTime);
        // SLEEP_CHECK_DEATH(descentTime, owner), expanded inline
        await sleep(descentTime);
        if (QDELETED(owner) || owner.stat == DEAD)
            return;
        owner.mouse_opacity = Dm.num(Dm.initial(owner, (atom p) => p.mouse_opacity));
        playsound(owner.loc, Dm.resource("sound/effects/meteorimpact.ogg"), 200, TRUE);
        foreach (mob_living victim in Dm.each<mob_living>(Dm.sub(orange(1, owner), owner)))
        {
            await victim.adjust_brute_loss(75);
            if (!QDELETED(victim)) // Some mobs are deleted on death
            {
                float throw_dir = get_dir(owner, victim);
                if (victim.loc == owner.loc)
                    throw_dir = Dm.num(pick(GLOB.alldirs));
                atom throwtarget = get_edge_target_turf(owner, throw_dir) as atom;
                await victim.throw_at(throwtarget, 3);
                owner.visible_message(span_warning(I18n.T("[] is thrown clear of []!", victim, owner)));
            }
        }
        foreach (obj_vehicle_sealed_mecha mech in Dm.each<obj_vehicle_sealed_mecha>(orange(1, owner)))
            await mech.take_damage(75, BRUTE, MELEE, 1);

        foreach (mob observer in Dm.each<mob>(range(7, owner)))
            shake_camera(observer, 15, 1);

        owner.remove_traits(Dm.list(TRAIT_GODMODE, TRAIT_UNDENSE), SWOOPING_TRAIT);
        // SLEEP_CHECK_DEATH(1, owner), expanded inline
        await sleep(1);
        if (QDELETED(owner) || owner.stat == DEAD)
            return;
        swooping = FALSE;
        if (!Dm.truthy(lava_success))
            owner.SEND_SIGNAL(COMSIG_LAVA_ARENA_FAILED, Dm.list(owner));
    }

    // tg: code/datums/actions/mobs/lava_swoop.dm:125
    public virtual async DmTask lava_pools(atom target = null, float? amount = null, float? delay = null)
    {
        amount ??= 30;
        delay ??= 0.8f;
        if (target == null)
            return;
        target.visible_message(span_boldwarning(I18n.T("Lava starts to pool up around you!")));

        while (amount > 0)
        {
            if (QDELETED(target))
                break;
            turf target_turf = get_turf(target);
            turf lava_turf = pick(RANGE_TURFS(1, target_turf)) as turf;
            obj_effect_temp_visual_lava_warning warn_effect = Dm.@new<obj_effect_temp_visual_lava_warning>(lava_turf, 60); // longer reset time for the lava
            warn_effect.owner = owner;
            amount--;
            // SLEEP_CHECK_DEATH(delay, owner), expanded inline
            await sleep(Dm.num(delay));
            if (QDELETED(owner) || owner.stat == DEAD)
                return;
        }
    }

    // tg: code/datums/actions/mobs/lava_swoop.dm:141
    public virtual async DmTask<object> lava_arena(atom target = null)
    {
        turf target_turf = get_turf(target);
        if (QDELETED(target) || !isliving(target) || target_turf.z != owner.z)
            return null;
        target.visible_message(span_boldwarning(I18n.T("[] encases you in an arena of fire!", owner)));
        float amount = 3;
        turf center = get_turf(owner);
        DmList walled = Dm.sub(RANGE_TURFS(3, center), RANGE_TURFS(2, center)) as DmList;
        DmList drakewalls = Dm.list();
        foreach (turf T in Dm.each<turf>(walled))
            Dm.append(ref drakewalls, Dm.@new<obj_effect_temp_visual_drakewall>(T)); // no people with lava immunity can just run away from the attack for free
        DmList indestructible_turfs = Dm.list();

        foreach (object turf_target__it in Dm.each(RANGE_TURFS(2, center)))
        {
            turf turf_target = (turf)turf_target__it;
            if (isindestructiblefloor(turf_target))
                continue;
            if (isindestructiblewall(turf_target))
            {
                Dm.append(ref indestructible_turfs, turf_target);
                continue;
            }
            if (ismineralturf(turf_target))
            {
                turf_closed_mineral mineral_turf = turf_target as turf_closed_mineral;
                await mineral_turf.gets_drilled(owner);
            }
        }

        // SLEEP_CHECK_DEATH(1 SECONDS, owner), expanded inline: give them a bit of time to realize what attack is actually happening
        await sleep(1 * SECONDS);
        if (QDELETED(owner) || owner.stat == DEAD)
            return null;

        DmList turfs = RANGE_TURFS(2, center);
        DmList mobs_with_clients = Dm.list();
        while (amount > 0)
        {
            DmList empty = indestructible_turfs.Copy(); // can't place safe turfs on turfs that weren't changed to be open
            bool any_attack = false;
            foreach (turf T in Dm.each<turf>(turfs))
            {
                foreach (mob_living L in Dm.each<mob_living>(T.contents))
                {
                    if (L.client != null)
                    {
                        Dm.append(ref empty, pick(Dm.sub(Dm.bitand(Dm.sub(RANGE_TURFS(2, L), RANGE_TURFS(1, L)), turfs), empty))); // picks a turf within 2 of the creature not outside or in the shield
                        any_attack = true;
                        Dm.combine(mobs_with_clients, L);
                    }
                }
                foreach (obj_vehicle_sealed_mecha M in Dm.each<obj_vehicle_sealed_mecha>(T.contents))
                {
                    Dm.append(ref empty, pick(Dm.sub(Dm.bitand(Dm.sub(RANGE_TURFS(2, M), RANGE_TURFS(1, M)), turfs), empty)));
                    any_attack = true;
                }
            }
            if (!any_attack) // nothing to attack in the arena, time for enraged attack if we still have a cliented target.
            {
                foreach (obj_effect_temp_visual_drakewall D in Dm.each<obj_effect_temp_visual_drakewall>(drakewalls))
                    qdel(D);
                foreach (object a in Dm.each(mobs_with_clients))
                {
                    mob_living L = a as mob_living;
                    if (!QDELETED(L) && L.client != null)
                        return FALSE;
                }
                return TRUE;
            }
            foreach (turf T in Dm.each<turf>(turfs))
            {
                if (!Dm.@in(T, empty))
                    Dm.@new<obj_effect_temp_visual_lava_warning>(T);
                else if (!isindestructiblewall(T))
                    Dm.@new<obj_effect_temp_visual_lava_safe>(T);
            }
            amount--;
            // SLEEP_CHECK_DEATH(2.4 SECONDS, owner), expanded inline
            await sleep(2.4f * SECONDS);
            if (QDELETED(owner) || owner.stat == DEAD)
                return null;
        }
        return TRUE; // attack finished completely
    }
}

// tg: code/datums/actions/mobs/lava_swoop.dm:213
public partial class obj_effect_temp_visual_dragon_flight : obj_effect_temp_visual
{
    // tg: code/datums/actions/mobs/lava_swoop.dm:1 (file-local define)
    private const float SWOOP_HEIGHT = 270;

    // tg: code/datums/actions/mobs/lava_swoop.dm:221
    [DmParams("mapload", "negative")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object negative = Dm.arg(__extra, 0);
        object dot = base.Initialize(mapload, __extra);
        Waiting.Detach(flight(negative)); // INVOKE_ASYNC
        return dot;
    }

    // tg: code/datums/actions/mobs/lava_swoop.dm:225
    public virtual async DmTask flight(object negative = null)
    {
        if (Dm.truthy(negative))
            animate(this, Dm.named("pixel_x", -SWOOP_HEIGHT * 0.1f), Dm.named("pixel_z", SWOOP_HEIGHT * 0.15f), Dm.named("time", 3), Dm.named("easing", BOUNCE_EASING));
        else
            animate(this, Dm.named("pixel_x", SWOOP_HEIGHT * 0.1f), Dm.named("pixel_z", SWOOP_HEIGHT * 0.15f), Dm.named("time", 3), Dm.named("easing", BOUNCE_EASING));
        await sleep(0.3f * SECONDS);
        icon_state = "dragon_swoop";
        if (Dm.truthy(negative))
            animate(this, Dm.named("pixel_x", -SWOOP_HEIGHT), Dm.named("pixel_z", SWOOP_HEIGHT), Dm.named("time", 7));
        else
            animate(this, Dm.named("pixel_x", SWOOP_HEIGHT), Dm.named("pixel_z", SWOOP_HEIGHT), Dm.named("time", 7));
    }
}

// tg: code/datums/actions/mobs/lava_swoop.dm:237
public partial class obj_effect_temp_visual_dragon_flight_end : obj_effect_temp_visual_dragon_flight
{
    // tg: code/datums/actions/mobs/lava_swoop.dm:1 (file-local define)
    private const float SWOOP_HEIGHT = 270;

    // tg: code/datums/actions/mobs/lava_swoop.dm:242
    public override async DmTask flight(object negative = null)
    {
        if (Dm.truthy(negative))
        {
            pixel_x = -SWOOP_HEIGHT;
            animate(this, Dm.named("pixel_x", -16), Dm.named("pixel_z", 0), Dm.named("time", 5));
        }
        else
            animate(this, Dm.named("pixel_x", -16), Dm.named("pixel_z", 0), Dm.named("time", 5));
    }
}
