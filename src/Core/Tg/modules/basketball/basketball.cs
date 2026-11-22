// Translated from tgstation (snapshot 1d99bd7f): code/modules/basketball/basketball.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps ST2 (D333): the whole file (the basketball: dribbled with its bounce every few steps, passed from hand to hand, handed to a
// carbon by a click, thrown with combat mode on, shot with a right click after half a second's wind-up through the mobs, caught by a
// carbon it hits; the steal by a disarm by the players' stamina and facing, the fumble when knocked down, the spin's bounces; the
// scorer's three seconds before picking it up again). The var block is generated (D111).

// tg: code/modules/basketball/basketball.dm:4
public partial class obj_item_toy_basketball : obj_item_toy
{
    // tg: code/modules/basketball/basketball.dm:1-2 (the file's #defines, #undef'd at its end)
    private const float MIN_DISARM_CHANCE = 25;
    private const float MAX_DISARM_CHANCE = 75;

    // tg: code/modules/basketball/basketball.dm:25
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        RegisterSignal(this, COMSIG_ITEM_EQUIPPED, nameof(on_equip));
        RegisterSignal(this, COMSIG_ITEM_DROPPED, nameof(remove_ball_effects));
        RegisterSignal(this, COMSIG_MOVABLE_THROW_LANDED, nameof(after_throw_reset));
        RegisterSignal(this, COMSIG_MOVABLE_IMPACT, nameof(after_throw_reset));
        return dot;
    }

    // tg: code/modules/basketball/basketball.dm:33
    public virtual void reset_pickup_restriction()
    {
        pickup_restriction_ckeys = Dm.list();
        pickup_cooldown = 0; // COOLDOWN_RESET (__DEFINES/cooldowns.dm), inline (rules 11.1 (a))
    }

    // tg: code/modules/basketball/basketball.dm:37
    [SignalHandler]
    public virtual void on_equip(obj_item source = null, mob_living user = null, object slot = null)
    {
        wielder = user;

        // the equip signal is sent AFTER the object is put in hands
        // so we need to manually check if the held object is different
        foreach (obj_item_toy_basketball ball in Dm.each<obj_item_toy_basketball>(user.held_items))
        {
            if (ball != this)
                return; // multiple balls in different hands so no need to setup signals again
        }

        RegisterSignal(user, COMSIG_MOVABLE_MOVED, nameof(movement_effect));
        RegisterSignal(user, COMSIG_MOB_EMOTED("spin"), nameof(on_spin));
        RegisterSignal(user, COMSIG_LIVING_DISARM_HIT, nameof(on_equipped_mob_disarm));
        RegisterSignal(user, COMSIG_LIVING_STATUS_KNOCKDOWN, nameof(on_equipped_mob_knockdown));
    }

    // tg: code/modules/basketball/basketball.dm:53
    [SignalHandler]
    public virtual void remove_ball_effects()
    {
        // unlike on_equip, this signal is triggered after the ball is removed from hands
        // so we can just use is_holding_item_of_type() proc to check for multiple balls
        if (!Dm.truthy(wielder.is_holding_item_of_type(typeof(obj_item_toy_basketball))))
            UnregisterSignal(wielder, Dm.list(COMSIG_MOVABLE_MOVED, COMSIG_MOB_EMOTED("spin"), COMSIG_LIVING_DISARM_HIT, COMSIG_LIVING_STATUS_KNOCKDOWN));

        wielder = null;
    }

    /**
     * After a ball is thrown we need to reset the pass_flags since shooting lets you shoot through mobs
     * * source: Datum src from original signal call
    **/
    // tg: code/modules/basketball/basketball.dm:67
    [SignalHandler]
    public virtual void after_throw_reset() // don't need the args for the signal
    {
        pass_flags = Dm.num(Dm.initial(this, nameof(pass_flags)));
    }

    // tg: code/modules/basketball/basketball.dm:72
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(await user.can_perform_action(this, NEED_HANDS)))
            return null;

        if (Dm.@in(user.ckey, pickup_restriction_ckeys) && !(pickup_cooldown <= world.time)) // COOLDOWN_FINISHED, inline (rules 11.1 (a))
        {
            // COOLDOWN_TIMELEFT(src, pickup_cooldown): max(0, pickup_cooldown - world.time), inline
            user.balloon_alert(user, I18n.T("cant pickup for [] seconds!", max(0, pickup_cooldown - world.time) * 0.1f));
            return null;
        }

        reset_pickup_restriction();
        return await base.attack_hand(user, modifiers, __extra);
    }

    // tg: code/modules/basketball/basketball.dm:83
    [SignalHandler]
    public virtual void movement_effect(atom_movable source = null, atom old_loc = null, object dir = null, object forced = null)
    {
        if (steps > step_delay)
        {
            playsound(this, Dm.resource("sound/items/basketball_bounce.ogg"), 75, FALSE);
            steps = 0;
        }
        else
            steps++;
    }

    // tg: code/modules/basketball/basketball.dm:92
    [SignalHandler]
    public virtual void on_spin(mob_living user = null)
    {
        for (float i = 1; i <= 6; i++)
            _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, nameof(playsound), this, Dm.resource("sound/items/basketball_bounce.ogg"), 75, FALSE), 0.25f * SECONDS * i, file: "code/modules/basketball/basketball.dm", line: 96);
        _addtimer(Dm.@new<datum_callback>(user, nameof(mob_living.adjust_stamina_loss), STAMINA_COST_SPINNING), 1.5f * SECONDS, file: "code/modules/basketball/basketball.dm", line: 97);
    }

    /// Used to calculate our disarm chance based on stamina, direction, and spinning
    /// Note - monkeys use attack_paw() and never trigger this signal (so they always have 100% disarm)
    // tg: code/modules/basketball/basketball.dm:101
    [SignalHandler]
    public virtual void on_equipped_mob_disarm(mob_living baller = null, mob_living stealer = null, object zone = null, obj_item weapon = null)
    {
        // spinning gives you a lower disarm chance but it drains stamina
        float disarm_chance = HAS_TRAIT(baller, TRAIT_SPINNING) ? 35 : 50;
        // ballers stamina results in lower disarm, stealer stamina results in higher disarm
        disarm_chance += (Dm.num(baller.get_stamina_loss()) - Dm.num(stealer.get_stamina_loss())) / 2;
        // the lowest chance for disarm is 25% and the highest is 75%
        disarm_chance = clamp(disarm_chance, MIN_DISARM_CHANCE, MAX_DISARM_CHANCE);

        // getting disarmed or shoved while holding the ball drains stamina
        Waiting.NoSleep(baller.adjust_stamina_loss(STAMINA_COST_DISARMING));

        if (!prob(disarm_chance))
            return; // the disarm failed

        playsound(this, Dm.resource("sound/items/basketball_bounce.ogg"), 75, FALSE);
        object blocking_dir_bonus = check_target_facings(stealer, baller);

        if (Dm.eq(blocking_dir_bonus, FACING_EACHOTHER))
        {
            stealer.balloon_alert_to_viewers(I18n.T("steals the ball"));
            Waiting.Detach(stealer.put_in_hands(this)); // INVOKE_ASYNC(stealer, TYPE_PROC_REF(/mob, put_in_hands), src)
        }
        else if (Dm.eq(blocking_dir_bonus, FACING_INIT_FACING_TARGET_TARGET_FACING_PERPENDICULAR))
        {
            if (prob(50))
            {
                if (!Dm.truthy(Waiting.NoSleep(baller.dropItemToGround(this))))
                    return;
                stealer.balloon_alert_to_viewers(I18n.T("bats the ball"));
            }
            else
            {
                stealer.balloon_alert_to_viewers(I18n.T("steals the ball"));
                Waiting.Detach(stealer.put_in_hands(this));
            }
        }
        else if (Dm.eq(blocking_dir_bonus, FACING_SAME_DIR))
        {
            if (!Dm.truthy(Waiting.NoSleep(baller.dropItemToGround(this))))
                return;
            stealer.balloon_alert_to_viewers(I18n.T("bats the ball"));
        }
    }

    // tg: code/modules/basketball/basketball.dm:137
    [SignalHandler]
    public virtual void on_equipped_mob_knockdown(mob_living user = null, object amount = null)
    {
        if (user == null)
            return;

        // Healing knockdown or setting knockdown to zero or something? No fumble
        if (Dm.num(amount) <= 0)
            return;

        if (!Dm.truthy(Waiting.NoSleep(user.dropItemToGround(this))))
            return;

        user.balloon_alert_to_viewers(I18n.T("fumbles the ball"));
    }

    // tg: code/modules/basketball/basketball.dm:152
    public override async DmTask<object> attack__proc(mob_living target_mob = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        if (!iscarbon(target_mob) || Dm.truthy(user.combat_mode))
            return await base.attack__proc(target_mob, user, modifiers, attack_modifiers, __extra);

        playsound(this, Dm.resource("sound/items/basketball_bounce.ogg"), 75, FALSE);
        await target_mob.put_in_hands(this);
        return null;
    }

    // tg: code/modules/basketball/basketball.dm:159
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(await user.can_perform_action(this, Dm.bitor(NEED_HANDS, FORBID_TELEKINESIS_REACH))))
            return null;

        // no spamming
        if (last_use + use_delay > world.time)
            return null;

        // need a free hand and can't be spinning
        if (!Dm.truthy(await user.put_in_inactive_hand(this)) || HAS_TRAIT(user, TRAIT_SPINNING))
            return null;

        last_use = world.time;
        user.swap_hand(user.get_held_index_of_item(this));
        playsound(this, Dm.resource("sound/items/basketball_bounce.ogg"), 75, FALSE);
        return null;
    }

    // tg: code/modules/basketball/basketball.dm:175
    public override async DmTask<object> ranged_interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        return await interact_with_atom(interacting_with, user, modifiers);
    }

    // tg: code/modules/basketball/basketball.dm:178
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (Dm.truthy(user.combat_mode))
        {
            await user.throw_item(interacting_with);
            return ITEM_INTERACT_SUCCESS;
        }
        return NONE;
    }

    // tg: code/modules/basketball/basketball.dm:184
    public override async DmTask<object> ranged_interact_with_atom_secondary(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        return await interact_with_atom_secondary(interacting_with, user, modifiers);
    }

    // tg: code/modules/basketball/basketball.dm:187
    public override async DmTask<object> interact_with_atom_secondary(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        mob_living baller = user;
        if (istype(interacting_with, typeof(obj_structure_hoop)) && Dm.truthy(await baller.Adjacent(interacting_with)))
            return NONE; // Do hoop stuff

        await baller.adjust_stamina_loss(STAMINA_COST_SHOOTING);

        float dunk_dir = get_dir(baller, interacting_with);
        float dunk_pixel_y = Dm.truthy(Dm.bitand(dunk_dir, SOUTH)) ? -16 : 16;
        float dunk_pixel_x = Dm.truthy(Dm.bitand(dunk_dir, EAST)) ? 16 : (Dm.truthy(Dm.bitand(dunk_dir, WEST)) ? -16 : 0);

        animate(baller, Dm.named("pixel_x", dunk_pixel_x), Dm.named("pixel_y", dunk_pixel_y), Dm.named("time", 5), Dm.named("easing", Dm.bitor(BOUNCE_EASING, Dm.bitor(EASE_IN, EASE_OUT))));
        if (Dm.truthy(await do_after(baller, 0.5f * SECONDS)))
        {
            pass_flags = Dm.bitor(pass_flags, PASSMOB);
            await baller.throw_item(interacting_with);
            animate(baller, Dm.named("pixel_x", 0), Dm.named("pixel_y", 0), Dm.named("time", 3));
            return ITEM_INTERACT_SUCCESS;
        }

        animate(baller, Dm.named("pixel_x", 0), Dm.named("pixel_y", 0), Dm.named("time", 3));
        return ITEM_INTERACT_BLOCKING;
    }

    // tg: code/modules/basketball/basketball.dm:207
    public override object throw_impact(atom hit_atom = null, datum_thrownthing throwingdatum = null, params object[] __extra)
    {
        playsound(this, Dm.resource("sound/items/basketball_bounce.ogg"), 75, FALSE);

        if (hit_atom is not mob_living_carbon target)
            return base.throw_impact(hit_atom, throwingdatum, __extra);

        Waiting.NoSleep(target.put_in_hands(this));
        return null;
    }
}
