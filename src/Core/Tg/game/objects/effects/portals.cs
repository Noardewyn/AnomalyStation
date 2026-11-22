// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/portals.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

public static partial class GlobalProcs
{
    // tg: code/game/objects/effects/portals.dm:1
    public static object create_portal_pair(turf source = null, turf destination = null, float _lifespan = 300, object accuracy = null, Type newtype = null)
    {
        newtype ??= typeof(obj_effect_portal);
        if (source == null || destination == null)
            return null;
        turf actual_destination = get_valid_teleport_turf(source, destination, accuracy ?? 0) as turf;
        obj_effect_portal P1 = Dm.@new(newtype, source, _lifespan, null, FALSE, null) as obj_effect_portal;
        obj_effect_portal P2 = Dm.@new(newtype, actual_destination, _lifespan, P1, TRUE, null) as obj_effect_portal;
        if (P1 == null || P2 == null)
            return null;
        playsound(P1, SFX_PORTAL_CREATED, 50, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
        playsound(P2, SFX_PORTAL_CREATED, 50, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
        P1.link_portal(P2);
        P1.hardlinked = TRUE;
        return Dm.list(P1, P2);
    }
}

// tg: code/game/objects/effects/portals.dm:15
public partial class obj_effect_portal : obj_effect
{
    // tg: code/game/objects/effects/portals.dm:61
    public override async DmTask<object> Move(atom newloc = null, object direct = null, float? glide_size_override = null, float? update_dir = null)
    {
        foreach (object T in Dm.each<object>(newloc?.contents))
        {
            if (T is obj_effect_portal)
                return FALSE;
        }
        return await base.Move(newloc, direct, glide_size_override, update_dir);
    }

    // Prevents portals spawned by jaunter/handtele from floating into space when relocated to an adjacent tile.
    // tg: code/game/objects/effects/portals.dm:68
    public override DmTask<object> newtonian_move(object inertia_angle = null, float? instant = null, float? start_delay = null, float? drift_force = null, object controlled_cap = null, float? force_loop = null) => DmTask<object>.FromResult(TRUE);

    // tg: code/game/objects/effects/portals.dm:71
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(await Adjacent(user)) || istype(tool, typeof(obj_item_hand_tele)))
            return await base.item_interaction(user, tool, modifiers, __extra);

        await teleport(user);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/effects/portals.dm:78
    public override async DmTask<object> CanAllowThrough(atom_movable mover = null, object border_dir = null)
    {
        object dot = await base.CanAllowThrough(mover, border_dir);
        if (HAS_TRAIT(mover, TRAIT_NO_TELEPORT) && !Dm.truthy(force_teleport))
            return TRUE;
        return dot;
    }

    // tg: code/game/objects/effects/portals.dm:83
    public override object Bumped(atom_movable bumped_atom = null)
    {
        Waiting.Detach(teleport(bumped_atom));
        return null;
    }

    // tg: code/game/objects/effects/portals.dm:86
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        if (Dm.truthy(await Adjacent(user)))
            await teleport(user);
        return dot;
    }

    // tg: code/game/objects/effects/portals.dm:93
    public override async DmTask<object> attack_robot(mob user = null, object modifiers = null)
    {
        if (Dm.truthy(await Adjacent(user)))
            await teleport(user);
        return null;
    }

    // tg: code/game/objects/effects/portals.dm:97
    [DmParams("mapload", "_lifespan", "_linked", "automatic_link", "hard_target_override")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        float _lifespan = Dm.num(Dm.arg(__extra, 0));
        obj_effect_portal _linked = Dm.arg(__extra, 1) as obj_effect_portal;
        object automatic_link = Dm.arg(__extra, 2);
        object hard_target_override = Dm.arg(__extra, 3);

        object dot = base.Initialize(mapload);
        Dm.append(ref GLOB.portals, this);
        if (_linked == null && Dm.truthy(automatic_link))
            throw Dm.CRASH("Somebody fucked up.", "code/game/objects/effects/portals.dm", 102);
        if (_lifespan > 0)
            _addtimer(Dm.@new<datum_callback>(this, nameof(expire)), _lifespan, TIMER_DELETE_ME, file: "code/game/objects/effects/portals.dm", line: 104);
        link_portal(_linked);
        hardlinked = Dm.truthy(automatic_link) ? 1 : 0;
        if (isturf(hard_target_override))
            hard_target = hard_target_override as turf;
        if (Dm.truthy(wibbles))
            apply_wibbly_filters(this);
        return dot;
    }

    // tg: code/game/objects/effects/portals.dm:112
    public virtual void expire()
    {
        playsound(loc, SFX_PORTAL_CLOSE, 50, FALSE, SHORT_RANGE_SOUND_EXTRARANGE);
        qdel(this);
    }

    // tg: code/game/objects/effects/portals.dm:116
    public override DmTask<object> singularity_pull(atom singularity = null, object current_size = null) => DmTask<object>.FromResult(null);

    // tg: code/game/objects/effects/portals.dm:119
    public override DmTask<object> singularity_act() => DmTask<object>.FromResult(null);

    // tg: code/game/objects/effects/portals.dm:122
    public virtual void link_portal(obj_effect_portal newlink = null) => linked = newlink;

    // tg: code/game/objects/effects/portals.dm:125
    public override object Destroy(float? force = null)
    {
        Dm.remove(GLOB.portals, this);
        if (Dm.truthy(hardlinked) && !QDELETED(linked))
            QDEL_NULL(ref linked);
        else
            linked = null;
        return base.Destroy(force);
    }

    // tg: code/game/objects/effects/portals.dm:132
    public override async DmTask<object> attack_ghost(mob_dead_observer user = null)
    {
        if (!Dm.truthy(await teleport(user, TRUE)))
            return await base.attack_ghost(user);
        return BULLET_ACT_FORCE_PIERCE;
    }

    // tg: code/game/objects/effects/portals.dm:137
    public override async DmTask<object> projectile_hit(obj_projectile hitting_projectile = null, object def_zone = null, float? piercing_hit = null, object blocked = null)
    {
        if (Dm.truthy(await teleport(hitting_projectile, TRUE)))
            return BULLET_ACT_FORCE_PIERCE;
        return await base.projectile_hit(hitting_projectile, def_zone, piercing_hit, blocked);
    }

    // tg: code/game/objects/effects/portals.dm:142
    public virtual async DmTask<object> teleport(atom_movable moving = null, object force = null)
    {
        bool forced = Dm.truthy(force);
        if (!forced && (moving == null || iseffect(moving) || (ismecha(moving) && !Dm.truthy(mech_sized)) || (!isobj(moving) && !ismob(moving)))) //Things that shouldn't teleport.
            return null;
        turf real_target = get_link_target_turf();
        if (real_target == null)
            return FALSE;

        if (!forced && !ismecha(moving) && !isprojectile(moving) && Dm.truthy(moving.anchored) && !Dm.truthy(allow_anchored))
            return null;
        bool no_effect = false;
        if (Dm.eq(last_effect, world.time) || Dm.truthy(sparkless))
            no_effect = true;
        else
            last_effect = world.time;
        turf start_turf = get_turf(moving);
        if (Dm.truthy(await do_teleport(moving, real_target, innate_accuracy_penalty, no_effects: no_effect ? TRUE : FALSE, channel: teleport_channel, forced: force_teleport)))
        {
            if (moving is obj_projectile proj)
                proj.ignore_source_check = TRUE;
            Dm.@new<obj_effect_temp_visual_portal_animation>(start_turf, this, moving);
            playsound(start_turf, SFX_PORTAL_ENTER, 50, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
            playsound(real_target, SFX_PORTAL_ENTER, 50, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
            return TRUE;
        }
        return FALSE;
    }

    // tg: code/game/objects/effects/portals.dm:169
    public virtual turf get_link_target_turf()
    {
        turf real_target;
        if (linked == null || QDELETED(linked))
        {
            if (Dm.truthy(hardlinked))
                qdel(this);
            if (hard_target == null || QDELETED(hard_target))
            {
                hard_target = null;
                return null;
            }
            real_target = hard_target;
            linked = null;
        }
        else
            real_target = get_turf(linked);
        return real_target;
    }
}

// tg: code/game/objects/effects/portals.dm:185
public partial class obj_effect_portal_permanent : obj_effect_portal
{
    // tg: code/game/objects/effects/portals.dm:192
    public virtual void set_linked()
    {
        if (!Dm.truthy(id))
            return;
        foreach (obj_effect_portal_permanent P in Dm.each<obj_effect_portal_permanent>(GLOB.portals))
        {
            if (P == this)
                continue;
            if (Dm.eq(P.id, id))
            {
                P.linked = this;
                linked = P;
                break;
            }
        }
    }

    // tg: code/game/objects/effects/portals.dm:200
    public override async DmTask<object> teleport(atom_movable moving = null, object force = null)
    {
        set_linked(); // update portal links
        return await base.teleport(moving, force);
    }
}

// tg: code/game/objects/effects/portals.dm:204
public partial class obj_effect_portal_permanent_one_way : obj_effect_portal_permanent // doesn't have a return portal, can have multiple exits, /obj/effect/landmark/portal_exit to mark them
{
    // tg: code/game/objects/effects/portals.dm:208
    public override void set_linked()
    {
        if (!Dm.truthy(id))
            return;
        DmList possible_turfs = Dm.list();
        foreach (obj_effect_landmark_portal_exit PE in Dm.each<obj_effect_landmark_portal_exit>(GLOB.landmarks_list))
        {
            if (Dm.eq(PE.id, id))
            {
                turf T = get_turf(PE);
                if (T != null)
                    Dm.combine(ref possible_turfs, T);
            }
        }
        if (possible_turfs.Count != 0)
            hard_target = pick(possible_turfs) as turf;
    }
}

// tg: code/game/objects/effects/portals.dm:220
public partial class obj_effect_portal_permanent_one_way_one_use : obj_effect_portal_permanent_one_way
{
    // tg: code/game/objects/effects/portals.dm:224
    public override async DmTask<object> teleport(atom_movable moving = null, object force = null)
    {
        object dot = await base.teleport(moving, force);
        if (Dm.truthy(dot) && !isdead(moving))
            expire();
        return dot;
    }
}

/**
 * Animation used for transitioning atoms which are teleporting somewhere via a portal
 *
 * To use, pass it the atom doing the teleporting and the atom that is being teleported in init.
 */
// tg: code/game/objects/effects/portals.dm:234
public partial class obj_effect_temp_visual_portal_animation : obj_effect_temp_visual
{
    // tg: code/game/objects/effects/portals.dm:237
    [DmParams("mapload", "portal", "teleporting")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        atom portal = Dm.arg(__extra, 0) as atom;
        atom_movable teleporting = Dm.arg(__extra, 1) as atom_movable;
        object dot = base.Initialize(mapload);
        if (portal == null || teleporting == null)
            return dot;

        appearance = teleporting.appearance;
        dir = teleporting.dir;
        layer = Dm.num(portal.layer) + 0.01f;
        alpha = teleporting.alpha;
        animate(this, Dm.named("pixel_x", Dm.num(portal.x) * 32 - Dm.num(x) * 32), Dm.named("pixel_y", Dm.num(portal.y) * 32 - Dm.num(y) * 32), Dm.named("alpha", 0), Dm.named("time", duration));
        return dot;
    }
}
