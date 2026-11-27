// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/deployable_turret.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the manned machine gun turrets. A player buckles himself in (a drop of himself onto the gun, the radial's Buckle);
// his hands fill with the turret's controls, and a click through them turns the gun towards what he aims at and fires a burst of
// bullets at it from the gun's tile, with the gun's sound per shot and its overheat sound when he fires again too soon. The heavy
// machine gun fires three-shot bursts and folds up with a wrench into the folded heavy machine gun (game/machinery/deployable.dm).
// Holding the button keeps firing (the controls' autoclick, the client's MouseDown). Kept from tg: process() ends the gun's
// processing on its first run, since update_positioning() returns null for a manned gun, so the gun turns only when its gunner clicks
// and with each shot of a burst (that shot's own refresh). DEVIATION(DV349): the client's view size is left alone. The var blocks are
// generated.

/// DEPLOYABLE TURRET (FORMERLY MANNED TURRET)
//All of this file is five year old shitcode, and I'm too scared to touch more than I have to

// tg: code/game/objects/structures/deployable_turret.dm:4
public partial class obj_machinery_deployable_turret : obj_machinery
{
    // tg: code/game/objects/structures/deployable_turret.dm:42
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(always_anchored))
            Waiting.NoSleep(set_anchored(TRUE));
        return dot;
    }

    // tg: code/game/objects/structures/deployable_turret.dm:47
    public override object Destroy(float? force = null)
    {
        target = null;
        target_turf = null;
        return base.Destroy(force);
    }

    /// Undeploying, for when you want to move your big dakka around
    // tg: code/game/objects/structures/deployable_turret.dm:53
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        obj_item used_wrench = tool;
        if (!Dm.truthy(can_be_undeployed))
            return ITEM_INTERACT_SKIP_TO_ATTACK;
        if (!ishuman(user))
            return ITEM_INTERACT_SKIP_TO_ATTACK;
        used_wrench.play_tool_sound(user);
        user.balloon_alert(user, I18n.T("undeploying..."));
        if (!Dm.truthy(await do_after(user, undeploy_time, this)))
            return ITEM_INTERACT_BLOCKING;
        obj undeployed_object = Dm.@new(spawned_on_undeploy) as obj;
        //Keeps the health the same even if you redeploy the gun
        await undeployed_object.modify_max_integrity(max_integrity);
        if (!Dm.truthy(await user.put_in_hands(undeployed_object as obj_item)))
            await undeployed_object.forceMove(loc);
        qdel(this);
        return ITEM_INTERACT_SUCCESS;
    }

    //BUCKLE HOOKS

    // tg: code/game/objects/structures/deployable_turret.dm:72
    public override async DmTask<object> unbuckle_mob(mob_living buckled_mob = null, float? force = null, float? can_fall = null)
    {
        force ??= FALSE;
        can_fall ??= TRUE;
        playsound(this, Dm.resource("sound/vehicles/mecha/mechmove01.ogg"), 50, TRUE);
        foreach (obj_item I in Dm.each<obj_item>(buckled_mob.held_items))
        {
            if (istype(I, typeof(obj_item_gun_control)))
                qdel(I);
        }
        if (istype(buckled_mob, typeof(mob_living)))
        {
            buckled_mob.pixel_x = buckled_mob.base_pixel_x;
            buckled_mob.pixel_y = buckled_mob.base_pixel_y;
            // DEVIATION(DV349): tg's client.view_size.resetToDefault(): a first-person view has no top-down view size to widen
        }
        if (!Dm.truthy(always_anchored))
            await set_anchored(FALSE);
        object dot = await base.unbuckle_mob(buckled_mob, force, can_fall);
        STOP_PROCESSING(SSfastprocess, this);
        return dot;
    }

    // tg: code/game/objects/structures/deployable_turret.dm:87
    public override async DmTask<object> user_buckle_mob(mob_living M = null, mob user = null, float? check_loc = null)
    {
        check_loc ??= TRUE;
        if (Dm.truthy(user.incapacitated) || !istype(user, typeof(mob)))
            return null;
        await M.forceMove(get_turf(this));
        object dot = await base.user_buckle_mob(M, user, check_loc);
        if (!Dm.truthy(dot))
            return dot;
        foreach (object V in Dm.each(M.held_items))
        {
            obj_item I = V as obj_item;
            if (istype(I, typeof(obj_item)))
            {
                if (Dm.truthy(await M.dropItemToGround(I)))
                {
                    obj_item_gun_control TC = Dm.@new<obj_item_gun_control>(this);
                    await M.put_in_hands(TC);
                }
            }
            else //Entries in the list should only ever be items or null, so if it's not an item, we can assume it's an empty hand
            {
                obj_item_gun_control TC = Dm.@new<obj_item_gun_control>(this);
                await M.put_in_hands(TC);
            }
        }
        M.pixel_y = 14;
        layer = ABOVE_MOB_LAYER;
        await setDir(SOUTH);
        playsound(this, Dm.resource("sound/vehicles/mecha/mechmove01.ogg"), 50, TRUE);
        await set_anchored(TRUE);
        // DEVIATION(DV349): tg's client.view_size.setTo(view_range): a first-person view has no top-down view size to widen
        START_PROCESSING(SSfastprocess, this);
        return dot;
    }

    // tg: code/game/objects/structures/deployable_turret.dm:112
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (!Dm.truthy(update_positioning()))
            return PROCESS_KILL;
        return null;
    }

    // tg: code/game/objects/structures/deployable_turret.dm:116
    public virtual object update_positioning()
    {
        if (!Dm.truthy(LAZYLEN(buckled_mobs)))
            return FALSE;
        mob_living controller = buckled_mobs[1] as mob_living;
        if (!istype(controller, typeof(mob_living)))
            return FALSE;
        client controlling_client = controller.client;
        if (Dm.truthy(controlling_client))
        {
            DmList modifiers = params2list(controlling_client.mouseParams);
            atom target_atom = controlling_client.mouse_object_ref?.resolve() as atom;
            turf target_turf = get_turf(target_atom);
            if (istype(target_turf, typeof(turf))) //They're hovering over something in the map.
            {
                direction_track(controller, target_turf);
                calculated_projectile_vars = calculate_projectile_angle_and_pixel_offsets(controller, target_turf, modifiers) as DmList;
            }
        }
        return null;
    }

    // tg: code/game/objects/structures/deployable_turret.dm:131
    public virtual void direction_track(mob user = null, atom targeted = null)
    {
        if (Dm.truthy(user.incapacitated))
            return;
        Waiting.NoSleep(setDir(get_dir(this, targeted)));
        Waiting.NoSleep(user.setDir(dir));
        float sw__1 = Dm.num(dir);
        if (sw__1 == NORTH)
        {
            layer = BELOW_MOB_LAYER;
            user.pixel_x = 0;
            user.pixel_y = -14;
        }
        else if (sw__1 == NORTHEAST)
        {
            layer = BELOW_MOB_LAYER;
            user.pixel_x = -8;
            user.pixel_y = -4;
        }
        else if (sw__1 == EAST)
        {
            layer = ABOVE_MOB_LAYER;
            user.pixel_x = -14;
            user.pixel_y = 0;
        }
        else if (sw__1 == SOUTHEAST)
        {
            layer = BELOW_MOB_LAYER;
            user.pixel_x = -8;
            user.pixel_y = 4;
        }
        else if (sw__1 == SOUTH)
        {
            layer = ABOVE_MOB_LAYER;
            user.pixel_x = 0;
            user.pixel_y = 14;
        }
        else if (sw__1 == SOUTHWEST)
        {
            layer = BELOW_MOB_LAYER;
            user.pixel_x = 8;
            user.pixel_y = 4;
        }
        else if (sw__1 == WEST)
        {
            layer = ABOVE_MOB_LAYER;
            user.pixel_x = 14;
            user.pixel_y = 0;
        }
        else if (sw__1 == NORTHWEST)
        {
            layer = BELOW_MOB_LAYER;
            user.pixel_x = 8;
            user.pixel_y = -4;
        }
    }

    // tg: code/game/objects/structures/deployable_turret.dm:170
    public virtual async DmTask checkfire(atom targeted_atom = null, mob user = null)
    {
        await DmTask.CompletedTask;
        target = targeted_atom;
        if (target == user || Dm.truthy(user.incapacitated) || target == get_turf(this))
            return;
        if (Dm.num(world.time) < cooldown)
        {
            if (!Dm.truthy(warned) && Dm.num(world.time) > (cooldown - cooldown_duration + rate_of_fire * number_of_shots)) // To capture the window where one is done firing
            {
                warned = TRUE;
                playsound(this, overheatsound, 100, TRUE);
            }
            return;
        }
        else
        {
            cooldown = Dm.num(world.time) + cooldown_duration;
            warned = FALSE;
            volley(user);
        }
    }

    // tg: code/game/objects/structures/deployable_turret.dm:184
    public virtual void volley(mob user = null)
    {
        target_turf = get_turf(target);
        for (float i = 1, i__end = number_of_shots; i <= i__end; i++)
            _addtimer(Dm.@new<datum_callback>(this, nameof(fire_helper), user), i * rate_of_fire, file: "code/game/objects/structures/deployable_turret.dm", line: 187);
    }

    // tg: code/game/objects/structures/deployable_turret.dm:189
    public virtual async DmTask fire_helper(mob user = null)
    {
        if (Dm.truthy(user.incapacitated) || !Dm.@in(user, buckled_mobs))
            return;
        update_positioning(); //REFRESH MOUSE TRACKING!!
        turf targets_from = get_turf(this);
        if (QDELETED(target))
            target = target_turf;
        obj_projectile projectile_to_fire = Dm.@new(projectile_type, targets_from) as obj_projectile;
        playsound(this, firesound, 75, TRUE);
        await projectile_to_fire.aim_projectile(target, targets_from);
        projectile_to_fire.firer = user;
        projectile_to_fire.fired_from = this;
        await projectile_to_fire.fire();
    }
}

// Admin-only proof of concept for autoclicker automatics
// tg: code/game/objects/structures/deployable_turret.dm:203
public partial class obj_machinery_deployable_turret_ultimate : obj_machinery_deployable_turret
{
    // tg: code/game/objects/structures/deployable_turret.dm:207
    public override async DmTask checkfire(atom targeted_atom = null, mob user = null)
    {
        target = targeted_atom;
        if (target == user || target == get_turf(this))
            return;
        target_turf = get_turf(target);
        await fire_helper(user);
    }
}

// tg: code/game/objects/structures/deployable_turret.dm:229
public partial class obj_item_gun_control : obj_item
{
    // tg: code/game/objects/structures/deployable_turret.dm:238
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_NODROP, ABSTRACT_ITEM_TRAIT);
        turret = loc as obj_machinery_deployable_turret;
        if (!istype(turret, typeof(obj_machinery_deployable_turret)))
            return INITIALIZE_HINT_QDEL;
        return dot;
    }

    // tg: code/game/objects/structures/deployable_turret.dm:245
    public override object Destroy(float? force = null)
    {
        turret = null;
        return base.Destroy(force);
    }

    // tg: code/game/objects/structures/deployable_turret.dm:249
    public override object CanItemAutoclick(object @object = null, object location = null, object @params = null)
    {
        return TRUE;
    }

    // tg: code/game/objects/structures/deployable_turret.dm:252
    public override async DmTask<object> attack_atom(atom attacked_atom = null, mob_living user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        atom attacked_obj = attacked_atom; // tg types it obj/attacked_obj; any atom arrives, and /atom/attacked_by is the root
        user.changeNext_move(CLICK_CD_MELEE);
        return await attacked_obj.attacked_by(this, user, modifiers);
    }

    // tg: code/game/objects/structures/deployable_turret.dm:256
    public override async DmTask<object> attack__proc(mob_living target_mob = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        target_mob.lastattacker = user.real_name;
        target_mob.lastattackerckey = user.ckey;
        await target_mob.attacked_by(this, user, modifiers);
        add_fingerprint(user);
        return null;
    }

    // tg: code/game/objects/structures/deployable_turret.dm:262
    public override async DmTask<object> ranged_interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        obj_machinery_deployable_turret buckled_turret = user.buckled as obj_machinery_deployable_turret;
        if (!istype(buckled_turret, typeof(obj_machinery_deployable_turret)))
            return NONE;
        buckled_turret.calculated_projectile_vars = calculate_projectile_angle_and_pixel_offsets(user, interacting_with, modifiers) as DmList;
        buckled_turret.direction_track(user, interacting_with);
        await buckled_turret.checkfire(interacting_with, user);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/deployable_turret.dm:271
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        return await ranged_interact_with_atom(interacting_with, user, modifiers);
    }
}
