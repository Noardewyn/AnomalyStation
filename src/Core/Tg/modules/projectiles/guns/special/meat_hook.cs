// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/guns/special/meat_hook.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Syndicate G4b (D316): the meat hook, whole (the gun, the hook projectile and the datum that reels the victim in).

/// Meat Hook
// tg: code/modules/projectiles/guns/special/meat_hook.dm:6
public partial class obj_item_gun_magic_hook : obj_item_gun_magic
{
    // tg: code/modules/projectiles/guns/special/meat_hook.dm:23
    public override async DmTask shoot_with_empty_chamber(mob_living user = null)
    {
        balloon_alert(user, I18n.T("not ready yet!"));
    }

    // This isn't really a gun, so it shouldn't be checking for TRAIT_NOGUNS, a firing pin (pinless), or a trigger guard (guardless)
    // tg: code/modules/projectiles/guns/special/meat_hook.dm:26
    public override async DmTask<object> can_trigger_gun(mob_living user = null, object akimbo_usage = null)
    {
        if (Dm.truthy(akimbo_usage))
            return FALSE; //this would be kinda weird while shooting someone down.
        if (HAS_TRAIT(user, TRAIT_IMMOBILIZED))
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/projectiles/guns/special/meat_hook.dm:33
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        obj_item_bodypart_head removable = user.get_bodypart(BODY_ZONE_HEAD) as obj_item_bodypart_head;
        if (removable == null)
        {
            user.visible_message(span_suicide(I18n.T("[] stuffs the chain of the [] down the hole where their head should be! It looks like [] trying to commit suicide!", user, this, user.p_theyre())));
            return OXYLOSS;
        }

        playsound(get_turf(this), fire_sound, 50, TRUE, -1);
        user.visible_message(span_suicide(I18n.T("[] is using the [] on their [] head! It looks like [] trying to commit suicide!", user, this, user.p_their(), user.p_theyre())));
        playsound(get_turf(this), Dm.resource("sound/items/weapons/bladeslice.ogg"), 70);
        await removable.dismember(silent: FALSE);
        return BRUTELOSS;
    }

    // tg: code/modules/projectiles/guns/special/meat_hook.dm:45
    [SignalHandler]
    public override object on_mail_unwrap(atom source = null, mob user = null, obj_item_mail_traitor letter = null)
    {
        return NONE;
    }
}

// tg: code/modules/projectiles/guns/special/meat_hook.dm:56
public partial class obj_projectile_hook : obj_projectile
{
    // tg: code/modules/projectiles/guns/special/meat_hook.dm:1
    internal const string TRAIT_HOOKED = "hooked";
    //! How long we immobilize the firer after firing - we do cancel the immobilization early if nothing is hit.
    // tg: code/modules/projectiles/guns/special/meat_hook.dm:2
    private const float IMMOBILIZATION_TIMER = 0.25f * SECONDS;

    // tg: code/modules/projectiles/guns/special/meat_hook.dm:68
    public override async DmTask fire(object fire_angle = null, atom direct_target = null)
    {
        if (firer != null)
        {
            initial_chain = firer.Beam(this, icon_state: "chain", emissive: NONE) as datum_beam;
            ADD_TRAIT(firer, TRAIT_IMMOBILIZED, REF(this));
            _addtimer(TRAIT_CALLBACK_REMOVE(firer, TRAIT_IMMOBILIZED, REF(this)), IMMOBILIZATION_TIMER, file: "code/modules/projectiles/guns/special/meat_hook.dm", line: 72); // safety if we miss, if we get a hit we stay immobilized
        }
        await base.fire(fire_angle, direct_target);
    }

    // tg: code/modules/projectiles/guns/special/meat_hook.dm:75
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        object dot = await base.on_hit(target, blocked, pierce_hit);
        if (!ismovable(target))
            return dot;

        atom_movable victim = (atom_movable)target;
        if (Dm.truthy(victim.anchored) || HAS_TRAIT_FROM(victim, TRAIT_HOOKED, REF(firer)))
            return dot;

        victim.visible_message(span_danger(I18n.T("[] is snagged by []'s hook!", victim, firer)));

        datum_hook_and_move puller = Dm.@new<datum_hook_and_move>();
        await puller.begin_pulling(firer, victim, get_turf(firer));
        REMOVE_TRAIT(firer, TRAIT_IMMOBILIZED, REF(this));
        return dot;
    }

    // tg: code/modules/projectiles/guns/special/meat_hook.dm:91
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref initial_chain);
        return base.Destroy(force);
    }
}

/// Lightweight datum that just handles moving a target for the hook.
/// For the love of God, do not use this outside this file.
// tg: code/modules/projectiles/guns/special/meat_hook.dm:97
public partial class datum_hook_and_move : datum
{
    /// List of traits that prevent the user from moving. More restrictive than attempting to fire the hook by design.
    // tg: code/modules/projectiles/guns/special/meat_hook.dm:117
    public static DmList prevent_movement_traits => TgState.Current.datum_hook_and_move__prevent_movement_traits ??= Dm.list(
        TRAIT_IMMOBILIZED,
        TRAIT_UI_BLOCKED);

    // tg: code/modules/projectiles/guns/special/meat_hook.dm:122
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSfastprocess, this);
        QDEL_NULL(ref return_chain);
        return base.Destroy(force);
    }

    /// Uses fastprocessing to move our victim to the destination at a rather fast speed.
    // tg: code/modules/projectiles/guns/special/meat_hook.dm:128
    public virtual async DmTask begin_pulling(atom_movable firer = null, atom_movable victim = null, atom destination = null)
    {
        return_chain = firer.Beam(victim, icon_state: "chain", emissive: NONE) as datum_beam;

        firer_ref_string = REF(firer);
        ADD_TRAIT(victim, obj_projectile_hook.TRAIT_HOOKED, firer_ref_string);
        firer.add_traits(prevent_movement_traits, REF(this));
        if (isliving(victim))
        {
            mob_living fresh_meat = (mob_living)victim;
            await fresh_meat.Knockdown(knockdown_time);
        }

        destination_ref = WEAKREF(destination) as datum_weakref;
        victim_ref = WEAKREF(victim) as datum_weakref;
        firer_ref = WEAKREF(firer) as datum_weakref;

        START_PROCESSING(SSfastprocess, this);
    }

    /// Cancels processing and removes the trait from the victim.
    // tg: code/modules/projectiles/guns/special/meat_hook.dm:147
    public virtual void end_movement()
    {
        atom_movable firer = firer_ref?.resolve() as atom_movable;
        if (!QDELETED(firer))
            firer.remove_traits(prevent_movement_traits, REF(this));

        atom_movable victim = victim_ref?.resolve() as atom_movable;
        if (!QDELETED(victim))
            REMOVE_TRAIT(victim, obj_projectile_hook.TRAIT_HOOKED, firer_ref_string);

        qdel(this);
    }

    // tg: code/modules/projectiles/guns/special/meat_hook.dm:159
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        atom_movable victim = victim_ref?.resolve() as atom_movable;
        atom destination = destination_ref?.resolve() as atom;
        if (QDELETED(victim) || QDELETED(destination))
        {
            end_movement();
            return null;
        }

        float steps_to_take = round(steps_per_tick * (Dm.num(world.time) - last_movement));
        if (steps_to_take <= 0)
            return null;

        object movement_result = Waiting.NoSleep(attempt_movement(victim, destination));
        if (!Dm.truthy(movement_result) || (victim.loc == destination.loc)) // either we failed our movement or our mission is complete
            end_movement();
        return null;
    }

    /// Attempts to move the victim towards the destination. Returns TRUE if we do a successful movement, FALSE otherwise.
    /// second_attempt is a boolean to prevent infinite recursion.
    /// If this whole series of events wasn't reliant on SSfastprocess firing as fast as it does, it would have been more useful to make this a move loop datum. But, we need the speed.
    // tg: code/modules/projectiles/guns/special/meat_hook.dm:177
    public virtual async DmTask<object> attempt_movement(atom_movable subject = null, atom target = null, float? second_attempt = null)
    {
        second_attempt ??= FALSE;
        object actually_moved = FALSE;
        if (!Dm.truthy(second_attempt))
            actually_moved = await step_towards(subject, target);

        if (Dm.truthy(actually_moved))
            return TRUE;

        // alright now the code fucking sucks
        float subject_x = subject.x;
        float subject_y = subject.y;
        float target_x = target.x;
        float target_y = target.y;

        //If we're going x, step x
        if ((target_x > subject_x) && Dm.truthy(await step(subject, EAST)))
            actually_moved = TRUE;
        else if ((target_x < subject_x) && Dm.truthy(await step(subject, WEST)))
            actually_moved = TRUE;

        if (Dm.truthy(actually_moved))
            return TRUE;

        //If the x step failed, go y
        if ((target_y > subject_y) && Dm.truthy(await step(subject, NORTH)))
            actually_moved = TRUE;
        else if ((target_y < subject_y) && Dm.truthy(await step(subject, SOUTH)))
            actually_moved = TRUE;

        if (Dm.truthy(actually_moved))
            return TRUE;

        // if we fail twice, abort. otherwise queue up the second attempt.
        if (Dm.truthy(second_attempt))
            return FALSE;

        return await attempt_movement(subject, target, second_attempt: TRUE);
    }
}

public sealed partial class TgState
{
    internal DmList datum_hook_and_move__prevent_movement_traits;
}
