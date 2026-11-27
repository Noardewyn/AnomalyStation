// Translated from tgstation (snapshot 1d99bd7f): code/modules/mining/equipment/grapple_gun.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The grapple gun: fired at a rock face (or anything that blocks the line) out of reach, its hook flies trailing a zip-line beam; when
// it hits, the user is pulled along the line after a second and a half, knocking aside whoever stands in the way. It works only at
// Lavaland's low pressure unless emagged. The hook's overlay shows when it is loaded.

// tg: code/modules/mining/equipment/grapple_gun.dm:3
public partial class obj_item_grapple_gun : obj_item
{
    // tg: code/modules/mining/equipment/grapple_gun.dm:1  #define DAMAGE_ON_IMPACT 20
    private const float DAMAGE_ON_IMPACT = 20;

    ///overlay when the hook is retracted
    // tg: code/modules/mining/equipment/grapple_gun.dm:13  var/static/mutable_appearance/hook_overlay (made where it is drawn: one appearance)
    private static mutable_appearance hook_overlay() => mutable_appearance(icon: Dm.resource("icons/obj/mining.dmi"), icon_state: "grapple_gun_hooked") as mutable_appearance;

    ///traits we apply to the user when being launched
    // tg: code/modules/mining/equipment/grapple_gun.dm:19  var/static/list/traits_on_zipline
    private static DmList traits_on_zipline() => Dm.list(TRAIT_IMMOBILIZED, TRAIT_MOVE_FLOATING, TRAIT_FORCED_STANDING);

    // tg: code/modules/mining/equipment/grapple_gun.dm:33
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        zipline_sound = Dm.@new<datum_looping_sound_zipline>(this);
        update_appearance();
        return dot;
    }

    // tg: code/modules/mining/equipment/grapple_gun.dm:38
    public override async DmTask<object> ranged_interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        atom target = interacting_with;
        if (isgroundlessturf(target))
            return NONE;
        if (target == user || !Dm.truthy(hooked))
            return NONE;

        if (!Dm.truthy(lavaland_equipment_pressure_check(get_turf(user))) && !Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
        {
            user.balloon_alert(user, "gun mechanism won't work here!");
            return ITEM_INTERACT_BLOCKING;
        }
        if (get_dist(user, target) > 9)
        {
            user.balloon_alert(user, "too far away!");
            return ITEM_INTERACT_BLOCKING;
        }

        turf attacked_atom = get_turf(target);
        if (attacked_atom == null)
            return ITEM_INTERACT_BLOCKING;

        var turf_list = (DmList)Dm.sub(get_line(user, attacked_atom), get_turf(this));
        foreach (turf singular_turf in Dm.each<turf>(turf_list))
        {
            if (ischasm(singular_turf))
                continue;
            if (!Dm.truthy(await singular_turf.is_blocked_turf()))
                continue;
            attacked_atom = singular_turf;
            break;
        }

        if (Dm.truthy(await attacked_atom.IsReachableBy(user)))
            return ITEM_INTERACT_BLOCKING;

        var bullet = await fire_projectile(typeof(obj_projectile_grapple_hook), attacked_atom, Dm.resource("sound/items/weapons/zipline_fire.ogg")) as atom;
        zipline = user.Beam(bullet, icon_state: "zipline_hook", maxdistance: 9, layer: BELOW_MOB_LAYER) as datum_beam;
        hooked = FALSE;
        RegisterSignal(bullet, COMSIG_PROJECTILE_SELF_ON_HIT, nameof(on_grapple_hit));
        RegisterSignal(bullet, COMSIG_PREQDELETED, nameof(on_grapple_fail));
        zipliner = WEAKREF(user) as datum_weakref;
        update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/mining/equipment/grapple_gun.dm:76
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        await base.emag_act(user, emag_card, __extra);
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return FALSE;
        balloon_alert(user, "pressure settings overloaded");
        obj_flags = Dm.bitor(obj_flags, EMAGGED);
        return TRUE;
    }

    // tg: code/modules/mining/equipment/grapple_gun.dm:84
    [SignalHandler]
    public virtual void on_grapple_hit(datum source = null, atom_movable firer = null, atom target = null, object Angle = null)
    {
        UnregisterSignal(source, Dm.list(COMSIG_PROJECTILE_SELF_ON_HIT, COMSIG_PREQDELETED));
        QDEL_NULL(ref zipline);
        var user = zipliner?.resolve() as mob_living;
        if (user == null || target == null)
        {
            cancel_hook();
            return;
        }

        zipline = user.Beam(target, icon_state: "zipline_hook", maxdistance: 9, layer: BELOW_MOB_LAYER) as datum_beam;
        RegisterSignal(user, COMSIG_MOVABLE_MOVED, nameof(determine_distance));
        RegisterSignal(user, COMSIG_MOVABLE_PRE_THROW, nameof(apply_throw_traits));
        grapple_timer_id = _addtimer(Dm.@new<datum_callback>(this, nameof(launch_user), target), 1.5f * SECONDS, TIMER_STOPPABLE, file: "code/modules/mining/equipment/grapple_gun.dm", line: 96);
    }

    // tg: code/modules/mining/equipment/grapple_gun.dm:99
    [SignalHandler]
    public virtual void on_grapple_fail(datum source = null)
    {
        cancel_hook();
    }

    // tg: code/modules/mining/equipment/grapple_gun.dm:103
    [SignalHandler]
    public virtual void determine_distance(atom_movable source = null)
    {
        if (zipline == null)
            return;
        atom target = zipline.target;
        if (target == null)
            return;
        if (get_dist(source, target) > zipline.max_distance)
            cancel_hook();
    }

    // tg: code/modules/mining/equipment/grapple_gun.dm:114
    [SignalHandler]
    public virtual void apply_throw_traits(mob_living source = null, DmList arguements = null)
    {
        var target_atom = arguements[1] as atom;
        if (target_atom == null)
            return;
        float dir_to_turn = Dm.num(get_angle(source, target_atom));
        if (dir_to_turn > 175 && dir_to_turn < 190)
            dir_to_turn = 0;
        source.add_traits(traits_on_zipline(), LEAPING_TRAIT);
        initial_matrix = source.transform as matrix;
        animate(source, Dm.named("transform", Dm.@new<matrix>().Turn(dir_to_turn)), Dm.named("time", 0.1f * SECONDS));
    }

    // tg: code/modules/mining/equipment/grapple_gun.dm:126
    public virtual async DmTask launch_user(atom target_atom = null)
    {
        var my_user = zipliner?.resolve() as mob_living;
        if (my_user == null || target_atom == null || my_user.buckled != null)
        {
            cancel_hook();
            return;
        }
        zipline_sound.start();
        Dm.@new<obj_effect_temp_visual_mook_dust>(drop_location());
        RegisterSignal(my_user, COMSIG_MOVABLE_IMPACT, nameof(strike_target));
        await my_user.throw_at(target: target_atom, range: 9, speed: 1, spin: FALSE, gentle: TRUE, callback: Dm.@new<datum_callback>(this, nameof(post_land)));
    }

    // tg: code/modules/mining/equipment/grapple_gun.dm:136
    [SignalHandler]
    public virtual void strike_target(mob_living source = null, mob_living victim = null, datum_thrownthing throwingdatum = null)
    {
        if (!istype(victim, typeof(mob_living)))
            return;

        Waiting.NoSleep(victim.apply_damage(DAMAGE_ON_IMPACT));
        playsound(victim, Dm.resource("sound/effects/hit_kick.ogg"), 50);
        var target_turf = get_ranged_target_turf(victim, source.dir, 3) as turf;
        if (target_turf == null)
            return;
        Waiting.Detach(victim.throw_at(target: target_turf, speed: 1, spin: TRUE, range: 3));
    }

    // tg: code/modules/mining/equipment/grapple_gun.dm:150
    public virtual void post_land()
    {
        var my_user = zipliner?.resolve() as mob_living;
        if (my_user != null)
        {
            my_user.transform = initial_matrix;
            my_user.remove_traits(traits_on_zipline(), LEAPING_TRAIT);
        }
        Dm.@new<obj_effect_temp_visual_mook_dust>(drop_location());
        cancel_hook();
    }

    // tg: code/modules/mining/equipment/grapple_gun.dm:158
    public virtual void cancel_hook()
    {
        var my_zipliner = zipliner?.resolve() as atom;
        if (my_zipliner != null)
            UnregisterSignal(my_zipliner, Dm.list(COMSIG_MOVABLE_IMPACT, COMSIG_MOVABLE_MOVED, COMSIG_MOVABLE_PRE_THROW));
        QDEL_NULL(ref zipline);
        zipliner = null;
        if (Dm.truthy(grapple_timer_id))
            deltimer(grapple_timer_id);
        grapple_timer_id = null;
        hooked = TRUE;
        zipline_sound.stop();
        initial_matrix = null;
        update_appearance();
    }

    // tg: code/modules/mining/equipment/grapple_gun.dm:172
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        var dot = (DmList)await base.update_overlays(__extra);
        if (Dm.truthy(hooked))
            Dm.append(ref dot, hook_overlay());
        return dot;
    }
}
