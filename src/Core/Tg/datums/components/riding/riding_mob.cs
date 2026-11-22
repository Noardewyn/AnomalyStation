// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/riding/riding_mob.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The riding creature base and the animals' variants (gaps package C4), the charger holoparasite's (syndicate X2), the cyborg's (silicons SI2): the human, mulebot and ruin creatures' variants wait with their hosts (a row).

// For any mob that can be ridden
// tg: code/datums/components/riding/riding_mob.dm:3
public partial class datum_component_riding_creature : datum_component_riding
{
    // tg: code/datums/components/riding/riding_mob.dm:19
    [DmParams("riding_mob", "force", "ride_check_flags")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        mob_living riding_mob = Dm.arg(__extra, 0) as mob_living;
        if (!isliving(parent))
            return COMPONENT_INCOMPATIBLE;
        object dot = await base.Initialize(__extra);
        mob_living living_parent = parent as mob_living;
        Waiting.NoSleep(living_parent.stop_pulling()); // was only used on humans previously, may change some other behavior
        log_riding(living_parent, riding_mob);
        riding_mob.set_glide_size(living_parent.glide_size);
        update_parent_layer_and_offsets(living_parent.dir);

        if (Dm.truthy(can_use_abilities))
            setup_abilities(riding_mob);

        return dot;
    }

    // tg: code/datums/components/riding/riding_mob.dm:37
    public override object Destroy(float? force = null)
    {
        unequip_buckle_inhands(parent as atom_movable);
        parent.remove_traits(Dm.list(TRAIT_AI_PAUSED, TRAIT_AI_MOVEMENT_HALTED), REF(this));
        return base.Destroy(force);
    }

    // tg: code/datums/components/riding/riding_mob.dm:45
    public override async DmTask RegisterWithParent()
    {
        await base.RegisterWithParent();
        RegisterSignal(parent, COMSIG_MOB_EMOTE, nameof(check_emote));
        if (Dm.truthy(can_be_driven))
            RegisterSignal(parent, COMSIG_RIDDEN_DRIVER_MOVE, nameof(driver_move)); // this isn't needed on riding humans or cyborgs since the rider can't control them
    }

    // The same table in every animal variant of the file.
    protected static DmList creature_parent_offsets() => Dm.list(
        Dm.kv(TEXT_NORTH, Dm.list(0f, 0f, MOB_BELOW_PIGGYBACK_LAYER)),
        Dm.kv(TEXT_SOUTH, Dm.list(0f, 0f, MOB_ABOVE_PIGGYBACK_LAYER)),
        Dm.kv(TEXT_EAST, Dm.list(0f, 0f, MOB_BELOW_PIGGYBACK_LAYER)),
        Dm.kv(TEXT_WEST, Dm.list(0f, 0f, MOB_BELOW_PIGGYBACK_LAYER)));

    // tg: code/datums/components/riding/riding_mob.dm:233
    public override void handle_buckle(mob_living rider = null)
    {
        base.handle_buckle(rider);
        mob_living ridden = parent as mob_living;
        if (!Dm.truthy(require_minigame) || Dm.truthy(ridden.has_ally(rider)))
            return;
        // Only raptors set require_minigame (their minigame is not ported: a row).
        Dm.Deferred("code/datums/components/riding/riding_mob.dm:237", "handle_buckle: the riding minigame (riding_minigame.dm), raptors only");
    }

    /// Creatures need to be logged when being mounted
    // tg: code/datums/components/riding/riding_mob.dm:52
    public virtual void log_riding(mob_living living_parent = null, mob_living rider = null)
    {
        // The game log's two lines: admin tooling (a row).
    }

    // this applies to humans and most creatures, but is replaced again for cyborgs
    // tg: code/datums/components/riding/riding_mob.dm:60
    public override object ride_check(mob_living rider = null, object consequences = null)
    {
        consequences ??= TRUE;
        bool dot = true;
        mob_living living_parent = parent as mob_living;
        if (!Dm.eq(living_parent.body_position, STANDING_UP)) // if we move while on the ground, the rider falls off
            dot = false;
        // for piggybacks and (redundant?) borg riding, check if the rider is stunned/restrained
        else if (Dm.truthy(Dm.bitand(ride_check_flags, RIDER_NEEDS_ARMS)) && (HAS_TRAIT(rider, TRAIT_RESTRAINED) || Dm.truthy(INCAPACITATED_IGNORING(rider, Dm.bitor(INCAPABLE_RESTRAINTS, INCAPABLE_GRAB)))))
            dot = false;
        // for fireman carries, check if the ridden is stunned/restrained
        else if (Dm.truthy(Dm.bitand(ride_check_flags, CARRIER_NEEDS_ARM)) && (HAS_TRAIT(living_parent, TRAIT_RESTRAINED) || Dm.truthy(INCAPACITATED_IGNORING(living_parent, Dm.bitor(INCAPABLE_RESTRAINTS, INCAPABLE_GRAB)))))
            dot = false;
        else if (Dm.truthy(Dm.bitand(ride_check_flags, JUST_FRIEND_RIDERS)) && !Dm.truthy(living_parent.has_ally(rider)))
            dot = false;

        if (dot || !Dm.truthy(consequences))
            return dot ? TRUE : FALSE;

        rider.visible_message(span_warning(Dm.text("[] falls off of []!", rider, living_parent)),
                        span_warning(Dm.text("You fall off of []!", living_parent)));
        Waiting.NoSleep(rider.Paralyze(1 * SECONDS));
        Waiting.NoSleep(rider.Knockdown(4 * SECONDS));
        Waiting.NoSleep(living_parent.unbuckle_mob(rider));
        return FALSE;
    }

    // tg: code/datums/components/riding/riding_mob.dm:84
    public override void vehicle_mob_buckle(datum source = null, mob_living rider = null, object force = null)
    {
        mob_living ridden = source as mob_living;
        // Ensure that the /mob/post_buckle_mob(mob/living/M) does not mess us up with layers
        // If we do not do this override we'll be stuck with the above proc (+0.1)-ing our rider's layer incorrectly
        rider.layer = Dm.num(initial(rider, "layer"));
        if (Dm.truthy(can_be_driven))
        {
            //let the rider drive if it's a pet etc.
            if (Dm.truthy(Dm.bitand(ai_behavior_while_ridden, RIDING_PAUSE_AI_PLANNING)))
                ADD_TRAIT(ridden, TRAIT_AI_PAUSED, REF(this));
            if (Dm.truthy(Dm.bitand(ai_behavior_while_ridden, RIDING_PAUSE_AI_MOVEMENT)))
                ADD_TRAIT(ridden, TRAIT_AI_MOVEMENT_HALTED, REF(this));
        }
        base.vehicle_mob_buckle(source, rider, force);
    }

    // tg: code/datums/components/riding/riding_mob.dm:96
    public override void vehicle_mob_unbuckle(datum source = null, mob_living former_rider = null, object force = null)
    {
        mob_living formerly_ridden = source as mob_living;
        remove_abilities(former_rider);
        if (Dm.length(formerly_ridden.buckled_mobs) == 0)
            formerly_ridden.remove_traits(Dm.list(TRAIT_AI_PAUSED, TRAIT_AI_MOVEMENT_HALTED), REF(this));
        // We gotta reset those layers at some point, don't we?
        former_rider.layer = MOB_LAYER;
        formerly_ridden.layer = MOB_LAYER;
        base.vehicle_mob_unbuckle(source, former_rider, force);
    }

    // tg: code/datums/components/riding/riding_mob.dm:108
    public override object Process_Spacemove(object direction = null, object continuous_move = null)
    {
        mob_living living_parent = parent as mob_living;
        return Dm.truthy(override_allow_spacemove) || Dm.truthy(Waiting.NoSleep(living_parent.Process_Spacemove(Dm.num(direction), Dm.num(continuous_move)))) ? TRUE : FALSE;
    }

    // tg: code/datums/components/riding/riding_mob.dm:112
    public override object driver_move(atom_movable movable_parent = null, mob_living user = null, object direction = null)
    {
        if (!(vehicle_move_cooldown <= world.time) || !Dm.truthy(Process_Spacemove(direction)))
            return COMPONENT_DRIVER_BLOCK_MOVE;
        if (!Dm.truthy(keycheck(user)))
        {
            if (ispath(keytype, typeof(obj_item)))
                to_chat(user, span_warning(Dm.text("You need a [] to ride []!", initial(keytype, "name"), movable_parent)));
            return COMPONENT_DRIVER_BLOCK_MOVE;
        }
        mob_living living_parent = parent as mob_living;
        Waiting.Detach(step(living_parent, Dm.num(direction)));
        float modified_move_delay = get_move_delay(living_parent, user, direction);
        // If we're moving diagonally, we need to adjust the move delay
        if (Dm.truthy(NSCOMPONENT(Dm.num(direction))) && Dm.truthy(EWCOMPONENT(Dm.num(direction))))
            modified_move_delay = FLOOR(modified_move_delay * Dm.sqrt(2), world.tick_lag);
        vehicle_move_cooldown = world.time + modified_move_delay;
        return base.driver_move(movable_parent, user, direction);
    }

    /// Yeets the rider off, used for animals and cyborgs, redefined for humans who shove their piggyback rider off
    // tg: code/datums/components/riding/riding_mob.dm:129
    public virtual float get_move_delay(mob_living living_parent = null, mob_living user = null, object direction = null)
    {
        float modified_move_delay = Dm.truthy(uses_native_speed) ? Dm.num(living_parent.cached_multiplicative_slowdown) : vehicle_move_delay;
        if (HAS_TRAIT(user, TRAIT_ROUGHRIDER)) // YEEHAW!
            modified_move_delay *= get_roughrider_mult(user);
        return modified_move_delay;
    }

    /// If we're a cowboy, we can ride faster on a horse
    // tg: code/datums/components/riding/riding_mob.dm:135
    public virtual float get_roughrider_mult(mob_living user = null)
    {
        if (HAS_TRAIT(user, TRAIT_PRIMITIVE))
            return 0.8f;
        switch (user.mob_mood?.sanity_level)
        {
            case SANITY_LEVEL_GREAT:
                return 0.8f;
            case SANITY_LEVEL_NEUTRAL:
                return 0.9f;
            case SANITY_LEVEL_DISTURBED:
                return 1f;
            case SANITY_LEVEL_CRAZY:
                return 1.1f;
            case SANITY_LEVEL_INSANE:
                return 1.2f;
        }
        return 1f;
    }

    /// Yeets the rider off, used for animals and cyborgs, redefined for humans who shove their piggyback rider off
    // tg: code/datums/components/riding/riding_mob.dm:152
    public virtual void force_dismount(mob_living rider = null, object throw_range = null, object throw_speed = null, object gentle = null)
    {
        throw_range ??= 8f;
        throw_speed ??= 3f;
        gentle ??= FALSE;
        atom_movable movable_parent = parent as atom_movable;
        Waiting.NoSleep(movable_parent.unbuckle_mob(rider));
        Waiting.NoSleep(rider.Knockdown(3 * SECONDS));
        if (Dm.num(throw_range) == 0)
            return;
        if (!iscyborg(movable_parent) && !isanimal_or_basicmob(movable_parent))
            return;
        turf target = get_edge_target_turf(movable_parent, movable_parent.dir) as turf;
        rider.visible_message(span_warning(Dm.text("[] is thrown clear of []!", rider, movable_parent)),
        span_warning(Dm.text("You're thrown clear of []!", movable_parent)));
        Waiting.Detach(rider.throw_at(target, Dm.num(throw_range), Dm.num(throw_speed), movable_parent, gentle: Dm.num(gentle)));
    }

    /// If we're a cyborg or animal and we spin, we yeet whoever's on us off us
    // tg: code/datums/components/riding/riding_mob.dm:166
    [SignalHandler]
    public virtual void check_emote(mob_living user = null, datum_emote emote = null)
    {
        if ((!iscyborg(user) && !isanimal_or_basicmob(user)) || emote is not datum_emote_spin)
            return;
        foreach (mob yeet_mob in Dm.each<mob>(user.buckled_mobs))
            force_dismount(yeet_mob as mob_living, gentle: !Dm.truthy(user.combat_mode) ? TRUE : FALSE); // gentle on help, byeeee if not
    }

    /// If the ridden creature has abilities, and some var yet to be made is set to TRUE, the rider will be able to control those abilities
    // tg: code/datums/components/riding/riding_mob.dm:176
    public virtual void setup_abilities(mob_living rider = null)
    {
        if (!isliving(parent))
            return;
        mob_living ridden_creature = parent as mob_living;
        foreach (datum_action action in Dm.each_cast<datum_action>(ridden_creature.actions))
        {
            if (Dm.truthy(is_type_in_list(action, blacklist_abilities)))
                continue;
            if (!Dm.truthy(action.can_be_shared) && !Dm.truthy(is_type_in_list(action, override_unsharable_abilities)))
                continue;
            Waiting.NoSleep(action.GiveAction(rider));
        }
    }

    /// Takes away the riding parent's abilities from the rider
    // tg: code/datums/components/riding/riding_mob.dm:190
    public virtual void remove_abilities(mob_living rider = null)
    {
        if (!isliving(parent))
            return;
        mob_living ridden_creature = parent as mob_living;
        foreach (datum_action action in Dm.each_cast<datum_action>(ridden_creature.actions))
        {
            if (action is datum_action_cooldown cooldown_action && rider.click_intercept == action)
                Waiting.NoSleep(cooldown_action.unset_click_ability(rider, refund_cooldown: TRUE));
            action.HideFrom(rider);
        }
    }

    // tg: code/datums/components/riding/riding_mob.dm:202
    public override object riding_can_z_move(atom_movable movable_parent = null, object direction = null, turf start = null, turf destination = null, object z_move_flags = null, mob_living rider = null)
    {
        if (!Dm.truthy(Dm.bitand(z_move_flags, ZMOVE_CAN_FLY_CHECKS)))
            return COMPONENT_RIDDEN_ALLOW_Z_MOVE;
        if (!Dm.truthy(can_be_driven))
        {
            if (Dm.truthy(Dm.bitand(z_move_flags, ZMOVE_FEEDBACK)))
                to_chat(rider, span_warning(Dm.text("[] cannot be driven around. Unbuckle from [] first.", movable_parent, movable_parent.p_them())));
            return COMPONENT_RIDDEN_STOP_Z_MOVE;
        }
        if (!Dm.truthy(ride_check(rider, FALSE)))
        {
            if (Dm.truthy(Dm.bitand(z_move_flags, ZMOVE_FEEDBACK)))
                to_chat(rider, span_warning(Dm.text("You're unable to ride [] right now!", movable_parent)));
            return COMPONENT_RIDDEN_STOP_Z_MOVE;
        }
        return COMPONENT_RIDDEN_ALLOW_Z_MOVE;
    }
}

//Now onto cyborg riding//
// tg: code/datums/components/riding/riding_mob.dm:332
public partial class datum_component_riding_creature_cyborg : datum_component_riding_creature
{
    // tg: code/datums/components/riding/riding_mob.dm:335
    public override object ride_check(mob_living rider = null, object consequences = null)
    {
        consequences ??= TRUE;
        mob_living_silicon_robot robot_parent = (mob_living_silicon_robot)parent;
        if (!iscarbon(rider))
            return TRUE;
        object dot = rider.usable_hands;
        if (!Dm.truthy(dot) && Dm.truthy(consequences))
        {
            Unbuckle(rider);
            to_chat(rider, span_warning(I18n.T("You can't grab onto [] with no hands!", robot_parent)));
        }
        return dot;
    }

    // tg: code/datums/components/riding/riding_mob.dm:345
    public override DmList get_rider_offsets_and_layers(object pass_index = null, mob offsetter = null)
    {
        mob_living_silicon_robot robot_parent = (mob_living_silicon_robot)parent;
        return robot_parent.model?.ride_offsets ?? Dm.list(
            Dm.kv(TEXT_NORTH, Dm.list(0f, 4f)),
            Dm.kv(TEXT_SOUTH, Dm.list(0f, 4f)),
            Dm.kv(TEXT_EAST, Dm.list(-6f, 3f)),
            Dm.kv(TEXT_WEST, Dm.list(6f, 3f))); // DEFAULT_ROBOT_RIDING_OFFSETS
    }

    // tg: code/datums/components/riding/riding_mob.dm:349
    public override DmList get_parent_offsets_and_layers()
    {
        return Dm.list(
            Dm.kv(TEXT_NORTH, Dm.list(0f, 0f, MOB_BELOW_PIGGYBACK_LAYER)),
            Dm.kv(TEXT_SOUTH, Dm.list(0f, 0f, MOB_ABOVE_PIGGYBACK_LAYER)),
            Dm.kv(TEXT_EAST, Dm.list(0f, 0f, MOB_ABOVE_PIGGYBACK_LAYER)),
            Dm.kv(TEXT_WEST, Dm.list(0f, 0f, MOB_ABOVE_PIGGYBACK_LAYER)));
    }
}

// tg: code/datums/components/riding/riding_mob.dm:369
public partial class datum_component_riding_creature_cow : datum_component_riding_creature
{
    // tg: code/datums/components/riding/riding_mob.dm:371
    public override DmList get_rider_offsets_and_layers(object pass_index = null, mob offsetter = null)
    {
        return Dm.list(
            Dm.kv(TEXT_NORTH, Dm.list(0f, 8f)),
            Dm.kv(TEXT_SOUTH, Dm.list(0f, 8f)),
            Dm.kv(TEXT_EAST, Dm.list(-2f, 8f)),
            Dm.kv(TEXT_WEST, Dm.list(2f, 8f)));
    }

    // tg: code/datums/components/riding/riding_mob.dm:379
    public override DmList get_parent_offsets_and_layers()
    {
        return creature_parent_offsets();
    }
}

// tg: code/datums/components/riding/riding_mob.dm:387
public partial class datum_component_riding_creature_pig : datum_component_riding_creature
{
    // tg: code/datums/components/riding/riding_mob.dm:389
    public override DmList get_rider_offsets_and_layers(object pass_index = null, mob offsetter = null)
    {
        return Dm.list(
            Dm.kv(TEXT_NORTH, Dm.list(0f, 8f)),
            Dm.kv(TEXT_SOUTH, Dm.list(0f, 8f)),
            Dm.kv(TEXT_EAST, Dm.list(-2f, 8f)),
            Dm.kv(TEXT_WEST, Dm.list(2f, 8f)));
    }

    // tg: code/datums/components/riding/riding_mob.dm:397
    public override DmList get_parent_offsets_and_layers()
    {
        return creature_parent_offsets();
    }
}

// tg: code/datums/components/riding/riding_mob.dm:405
public partial class datum_component_riding_creature_pony : datum_component_riding_creature
{
    // tg: code/datums/components/riding/riding_mob.dm:410
    public override DmList get_rider_offsets_and_layers(object pass_index = null, mob offsetter = null)
    {
        return Dm.list(
            Dm.kv(TEXT_NORTH, Dm.list(0f, 9f)),
            Dm.kv(TEXT_SOUTH, Dm.list(0f, 9f)),
            Dm.kv(TEXT_EAST, Dm.list(-2f, 9f)),
            Dm.kv(TEXT_WEST, Dm.list(2f, 9f)));
    }

    // tg: code/datums/components/riding/riding_mob.dm:418
    public override DmList get_parent_offsets_and_layers()
    {
        return creature_parent_offsets();
    }

    // tg: code/datums/components/riding/riding_mob.dm:426
    public override object driver_move(atom_movable movable_parent = null, mob_living user = null, object direction = null)
    {
        object dot = base.driver_move(movable_parent, user, direction);

        if (Dm.eq(dot, COMPONENT_DRIVER_BLOCK_MOVE) || !(pony_trot_cooldown <= world.time))
            return dot;

        mob_living_carbon_human human_user = user as mob_living_carbon_human;

        if (human_user != null && Dm.truthy(is_clown_job(human_user.mind?.assigned_role)))
        {
            // there's a new sheriff in town
            playsound(movable_parent, Dm.resource("sound/mobs/non-humanoids/pony/clown_gallup.ogg"), 50);
            pony_trot_cooldown = world.time + 500 * MILLISECONDS;
        }
        return dot;
    }
}

// tg: code/datums/components/riding/riding_mob.dm:439
public partial class datum_component_riding_creature_bear : datum_component_riding_creature
{
    // tg: code/datums/components/riding/riding_mob.dm:442
    public override DmList get_rider_offsets_and_layers(object pass_index = null, mob offsetter = null)
    {
        return Dm.list(
            Dm.kv(TEXT_NORTH, Dm.list(1f, 8f)),
            Dm.kv(TEXT_SOUTH, Dm.list(1f, 8f)),
            Dm.kv(TEXT_EAST, Dm.list(-3f, 6f)),
            Dm.kv(TEXT_WEST, Dm.list(3f, 6f)));
    }

    // tg: code/datums/components/riding/riding_mob.dm:450
    public override DmList get_parent_offsets_and_layers()
    {
        return creature_parent_offsets();
    }
}

// tg: code/datums/components/riding/riding_mob.dm:458
public partial class datum_component_riding_creature_carp : datum_component_riding_creature
{
    // tg: code/datums/components/riding/riding_mob.dm:461
    public override DmList get_rider_offsets_and_layers(object pass_index = null, mob offsetter = null)
    {
        return Dm.list(
            Dm.kv(TEXT_NORTH, Dm.list(0f, 13f)),
            Dm.kv(TEXT_SOUTH, Dm.list(0f, 15f)),
            Dm.kv(TEXT_EAST, Dm.list(-2f, 12f)),
            Dm.kv(TEXT_WEST, Dm.list(2f, 12f)));
    }

    // tg: code/datums/components/riding/riding_mob.dm:469
    public override DmList get_parent_offsets_and_layers()
    {
        return creature_parent_offsets();
    }
}

// tg: code/datums/components/riding/riding_mob.dm:477
public partial class datum_component_riding_creature_megacarp : datum_component_riding_creature
{
    // tg: code/datums/components/riding/riding_mob.dm:480
    public override DmList get_rider_offsets_and_layers(object pass_index = null, mob offsetter = null)
    {
        return Dm.list(
            Dm.kv(TEXT_NORTH, Dm.list(1f, 8f)),
            Dm.kv(TEXT_SOUTH, Dm.list(1f, 8f)),
            Dm.kv(TEXT_EAST, Dm.list(-3f, 6f)),
            Dm.kv(TEXT_WEST, Dm.list(3f, 6f)));
    }

    // tg: code/datums/components/riding/riding_mob.dm:488
    public override DmList get_parent_offsets_and_layers()
    {
        return creature_parent_offsets();
    }
}

// tg: code/datums/components/riding/riding_mob.dm:789
public partial class datum_component_riding_creature_spider : datum_component_riding_creature
{
    // tg: code/datums/components/riding/riding_mob.dm:793
    public override DmList get_rider_offsets_and_layers(object pass_index = null, mob offsetter = null)
    {
        return Dm.list(
            Dm.kv(TEXT_NORTH, Dm.list(0f, 10f)),
            Dm.kv(TEXT_SOUTH, Dm.list(0f, 10f)),
            Dm.kv(TEXT_EAST, Dm.list(-5f, 10f)),
            Dm.kv(TEXT_WEST, Dm.list(5f, 10f)));
    }

    // tg: code/datums/components/riding/riding_mob.dm:801
    public override DmList get_parent_offsets_and_layers()
    {
        return creature_parent_offsets();
    }
}

// tg: code/datums/components/riding/riding_mob.dm:602  (syndicate X2: the charger holoparasite carries its summoner)
public partial class datum_component_riding_creature_guardian : datum_component_riding_creature
{
    // tg: code/datums/components/riding/riding_mob.dm:605
    public override DmList get_rider_offsets_and_layers(object pass_index = null, mob offsetter = null)
    {
        return Dm.list(
            Dm.kv(TEXT_NORTH, Dm.list(0f, 4f)),
            Dm.kv(TEXT_SOUTH, Dm.list(0f, 4f)),
            Dm.kv(TEXT_EAST, Dm.list(-6f, 3f)),
            Dm.kv(TEXT_WEST, Dm.list(6f, 3f)));
    }

    // tg: code/datums/components/riding/riding_mob.dm:613
    public override DmList get_parent_offsets_and_layers()
    {
        return Dm.list(
            Dm.kv(TEXT_NORTH, Dm.list(0f, 0f, MOB_BELOW_PIGGYBACK_LAYER)),
            Dm.kv(TEXT_SOUTH, Dm.list(0f, 0f, MOB_ABOVE_PIGGYBACK_LAYER)),
            Dm.kv(TEXT_EAST, Dm.list(0f, 0f, MOB_ABOVE_PIGGYBACK_LAYER)),
            Dm.kv(TEXT_WEST, Dm.list(0f, 0f, MOB_ABOVE_PIGGYBACK_LAYER)));
    }

    // tg: code/datums/components/riding/riding_mob.dm:621
    public override object ride_check(mob_living rider = null, object consequences = null)
    {
        consequences ??= TRUE;
        mob_living_basic_guardian charger = parent as mob_living_basic_guardian;
        if (charger == null)
            return base.ride_check(rider, consequences);
        return Dm.num(charger.summoner == rider);
    }
}
