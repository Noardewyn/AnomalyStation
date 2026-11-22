// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/riding/riding.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C4).

/**
 * This is the riding component, which is applied to a movable atom by the [ridable element][/datum/element/ridable] when a mob is successfully buckled to said movable.
 */
// tg: code/datums/components/riding/riding.dm:13
public partial class datum_component_riding : datum_component
{
    private const string RIDING_SOURCE = "riding"; // tg: code/datums/components/riding/riding.dm:1
    private const string BEING_RIDDEN_SOURCE = "being_ridden"; // tg: code/datums/components/riding/riding.dm:2

    // tg: code/datums/components/riding/riding.dm:38
    [DmParams("riding_mob", "force", "buckle_mob_flags")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        mob_living riding_mob = Dm.arg(__extra, 0) as mob_living;
        object buckle_mob_flags = Dm.arg(__extra, 2) ?? NONE;
        if (!ismovable(parent))
            return COMPONENT_INCOMPATIBLE;

        riding_mob.updating_glide_size = FALSE;
        ride_check_flags = Dm.bitor(ride_check_flags, Dm.num(buckle_mob_flags));

        if (HAS_TRAIT(parent, TRAIT_SPEED_POTIONED))
            vehicle_move_delay = Dm.round(Dm.num(config.Get(typeof(datum_config_entry_number_movedelay_run_delay))) * 0.85f, 0.01f);
        return null;
    }

    // tg: code/datums/components/riding/riding.dm:49
    public override async DmTask RegisterWithParent()
    {
        await base.RegisterWithParent();
        RegisterSignal(parent, COMSIG_ATOM_DIR_CHANGE, nameof(vehicle_turned));
        RegisterSignal(parent, COMSIG_MOVABLE_UNBUCKLE, nameof(vehicle_mob_unbuckle));
        RegisterSignal(parent, COMSIG_MOVABLE_BUCKLE, nameof(vehicle_mob_buckle));
        RegisterSignal(parent, COMSIG_MOVABLE_MOVED, nameof(vehicle_moved));
        RegisterSignal(parent, COMSIG_MOVABLE_BUMP, nameof(vehicle_bump));
        RegisterSignal(parent, COMSIG_BUCKLED_CAN_Z_MOVE, nameof(riding_can_z_move));
        RegisterSignals(parent, GLOB.movement_type_addtrait_signals, nameof(on_movement_type_trait_gain));
        RegisterSignals(parent, GLOB.movement_type_removetrait_signals, nameof(on_movement_type_trait_loss));
        RegisterSignal(parent, COMSIG_SUPERMATTER_CONSUMED, nameof(on_entered_supermatter));
        if (other_unbuckle == CANNOT_FORCE_UNBUCKLE)
            RegisterSignal(parent, COMSIG_ATOM_ATTACK_HAND, nameof(block_unbuckle));
        else if (other_unbuckle == CAN_DISARM_UNBUCKLE)
        {
            RegisterSignal(parent, COMSIG_ATOM_ATTACK_HAND, nameof(block_unbuckle));
            if (isliving(parent))
                RegisterSignal(parent, COMSIG_LIVING_DISARM_HIT, nameof(parent_disarmed));
        }
    }

    /// This proc is called when a rider unbuckles, whether they chose to or not. If there's no more riders, this will be the riding component's death knell.
    // tg: code/datums/components/riding/riding.dm:68
    [SignalHandler]
    public virtual void vehicle_mob_unbuckle(datum source = null, mob_living rider = null, object force = null)
    {
        restore_rider_layer_and_offsets(rider);
        handle_unbuckle(rider);
        atom_movable movable_parent = parent as atom_movable;
        if (Dm.truthy(movable_parent.has_buckled_mobs()))
            update_parent_layer_and_offsets(movable_parent.dir, TRUE);
        else
        {
            restore_parent_layer_and_offsets();
            qdel(this);
        }
    }

    /// Called to do things to the rider when they get unbuckled
    // tg: code/datums/components/riding/riding.dm:80
    public virtual void handle_unbuckle(mob_living rider = null)
    {
        unequip_buckle_inhands(rider);
        rider.updating_glide_size = TRUE;
        UnregisterSignal(rider, COMSIG_LIVING_TRY_PULL);
        UnregisterSignal(rider, COMSIG_LIVING_DISARM_HIT);
        foreach (object trait in Dm.each(GLOB.movement_type_trait_to_flag))
        {
            if (HAS_TRAIT(parent, trait))
                REMOVE_TRAIT(rider, trait, REF(this));
        }
        rider.remove_traits(rider_traits, REF(this));
    }

    /// This proc is called when a rider buckles, allowing for offsets to be set properly
    // tg: code/datums/components/riding/riding.dm:91
    [SignalHandler]
    public virtual void vehicle_mob_buckle(datum source = null, mob_living rider = null, object force = null)
    {
        atom_movable movable_parent = parent as atom_movable;
        update_parent_layer_and_offsets(movable_parent.dir, TRUE);
        handle_buckle(rider);
    }

    /// Called to do things to the rider when they get buckled
    // tg: code/datums/components/riding/riding.dm:98
    public virtual void handle_buckle(mob_living rider = null)
    {
        if (rider.pulling == parent)
            Waiting.NoSleep(rider.stop_pulling());
        RegisterSignal(rider, COMSIG_LIVING_TRY_PULL, nameof(on_rider_try_pull));
        if (other_unbuckle == CAN_DISARM_UNBUCKLE)
            RegisterSignal(rider, COMSIG_LIVING_DISARM_HIT, nameof(rider_disarmed));

        foreach (object trait in Dm.each(GLOB.movement_type_trait_to_flag))
        {
            if (HAS_TRAIT(parent, trait))
                ADD_TRAIT(rider, trait, REF(this));
        }
        rider.add_traits(rider_traits, REF(this));
    }

    /// This proc is called when the rider attempts to grab the thing they're riding, preventing them from doing so.
    // tg: code/datums/components/riding/riding.dm:111
    [SignalHandler]
    public virtual object on_rider_try_pull(mob_living rider_pulling = null, atom_movable target = null, object force = null)
    {
        if (target == parent)
        {
            mob_living ridden = parent as mob_living;
            ridden.balloon_alert(rider_pulling, "not while riding it!");
            return COMSIG_LIVING_CANCEL_PULL;
        }
        return null;
    }

    /// Some ridable atoms may want to only show on top of the rider in certain directions, like wheelchairs
    // tg: code/datums/components/riding/riding.dm:119
    [SignalHandler]
    public virtual void vehicle_moved(datum source = null, object oldloc = null, object dir = null, object forced = null)
    {
        atom_movable movable_parent = parent as atom_movable;
        if (dir == null)
            dir = movable_parent.dir;
        foreach (object m in Dm.each(movable_parent.buckled_mobs))
            ride_check(m as mob_living);
        if (QDELETED(this))
            return; // runtimed with piggy's without this, look into this more
        update_parent_layer_and_offsets(dir);
    }

    /// Turning is like moving
    // tg: code/datums/components/riding/riding.dm:132
    [SignalHandler]
    public virtual void vehicle_turned(datum source = null, object _old_dir = null, object new_dir = null)
    {
        vehicle_moved(source, null, new_dir);
    }

    /**
     * ride_check() is called when the rider is moved by a vehicle.
     * Return TRUE to allow the movement, FALSE to deny it.
     */
    // tg: code/datums/components/riding/riding.dm:144
    public virtual object ride_check(mob_living rider = null, object consequences = null)
    {
        return TRUE;
    }

    // tg: code/datums/components/riding/riding.dm:148-150  GET_X_OFFSET, GET_Y_OFFSET, GET_LAYER
    private static float GET_X_OFFSET(DmList offsets) => Dm.length(offsets) >= 1 ? Dm.num(offsets[1]) : 0;
    private static float GET_Y_OFFSET(DmList offsets) => Dm.length(offsets) >= 2 ? Dm.num(offsets[2]) : 0;
    private static float GET_LAYER(DmList offsets, float @default) => Dm.length(offsets) >= 3 ? Dm.num(offsets[3]) : @default;

    /// Corrects the offsets and layers of the parent and all buckled mobs.
    // tg: code/datums/components/riding/riding.dm:153
    public virtual void update_parent_layer_and_offsets(object dir = null, object animate = null)
    {
        atom_movable seat = parent as atom_movable;
        if (!Dm.truthy(seat.has_buckled_mobs()))
            return;

        int passindex = 0;
        foreach (mob_living buckled_mob in Dm.each_cast<mob_living>(seat.buckled_mobs))
        {
            passindex++;
            update_rider_layer_and_offsets(dir, passindex, buckled_mob);
        }

        DmList offsets = get_parent_offsets_and_layers()?[Dm.text("[]", dir)] as DmList;
        float px = GET_X_OFFSET(offsets);
        float py = GET_Y_OFFSET(offsets);
        float layer = GET_LAYER(offsets, Dm.num(seat.layer));

        if (isliving(seat))
        {
            mob_living living_seat = seat as mob_living;
            living_seat.add_offsets(BEING_RIDDEN_SOURCE, x_add: px, y_add: py, animate: Dm.truthy(animate) ? TRUE : FALSE);
        }
        else
        {
            seat.pixel_x = px + seat.base_pixel_x;
            seat.pixel_y = py + seat.base_pixel_y;
        }
        seat.layer = layer;
    }

    /// Set the rider's offsets and layer
    // tg: code/datums/components/riding/riding.dm:177
    public virtual void update_rider_layer_and_offsets(object dir = null, object passindex = null, mob_living rider = null, object animate = null)
    {
        if (!Dm.eq(rider.dir, dir))
            Waiting.NoSleep(rider.setDir(dir));

        DmList diroffsets = get_rider_offsets_and_layers(passindex, rider)?[Dm.text("[]", dir)] as DmList;
        float x_offset = GET_X_OFFSET(diroffsets);
        float y_offset = GET_Y_OFFSET(diroffsets);
        float layer = GET_LAYER(diroffsets, Dm.num(rider.layer));

        atom_movable seat = parent as atom_movable;
        if (Dm.truthy(seat.buckle_lying) && Dm.eq(rider.body_position, LYING_DOWN))
            y_offset += (-1 * PIXEL_Y_OFFSET_LYING);

        rider.add_offsets(RIDING_SOURCE, x_add: x_offset, z_add: y_offset, animate: Dm.truthy(animate) ? TRUE : FALSE);
        rider.layer = layer;
    }

    // tg: code/datums/components/riding/riding.dm:199
    public virtual DmList get_rider_offsets_and_layers(object pass_index = null, mob offsetter = null) // list(dir = x, y, layer)
    {
        return Dm.list(
            Dm.kv(TEXT_NORTH, Dm.list(0f, 0f)),
            Dm.kv(TEXT_SOUTH, Dm.list(0f, 0f)),
            Dm.kv(TEXT_EAST, Dm.list(0f, 0f)),
            Dm.kv(TEXT_WEST, Dm.list(0f, 0f)));
    }

    // tg: code/datums/components/riding/riding.dm:209
    public virtual DmList get_parent_offsets_and_layers() // list(dir = x, y, layer)
    {
        return Dm.list(
            Dm.kv(TEXT_NORTH, Dm.list(0f, 0f, OBJ_LAYER)),
            Dm.kv(TEXT_SOUTH, Dm.list(0f, 0f, ABOVE_MOB_LAYER)),
            Dm.kv(TEXT_EAST, Dm.list(0f, 0f, ABOVE_MOB_LAYER)),
            Dm.kv(TEXT_WEST, Dm.list(0f, 0f, ABOVE_MOB_LAYER)));
    }

    /// This proc is used to see if we have the appropriate key to drive this atom, if such a key is needed. Returns FALSE if we don't have what we need to drive.
    // tg: code/datums/components/riding/riding.dm:219
    public virtual object keycheck(mob user = null)
    {
        if (keytype == null)
            return TRUE;
        if (isvehicle(parent))
        {
            obj_vehicle vehicle_parent = parent as obj_vehicle;
            return Dm.istype(vehicle_parent.inserted_key, keytype) ? TRUE : FALSE;
        }
        return user.is_holding_item_of_type(keytype);
    }

    // tg: code/datums/components/riding/riding.dm:228
    public virtual void restore_rider_layer_and_offsets(mob_living buckled_mob = null)
    {
        buckled_mob.remove_offsets(RIDING_SOURCE);
        buckled_mob.layer = Dm.num(initial(buckled_mob, "layer"));
        SET_PLANE_EXPLICIT(buckled_mob, Dm.num(initial(buckled_mob, "plane")), parent as atom);
        buckled_mob.client?.view_size?.resetToDefault();
    }

    // tg: code/datums/components/riding/riding.dm:235
    public virtual void restore_parent_layer_and_offsets()
    {
        atom_movable seat = parent as atom_movable;
        if (isliving(seat))
        {
            mob_living living_seat = seat as mob_living;
            living_seat.remove_offsets(BEING_RIDDEN_SOURCE);
        }
        else
        {
            seat.pixel_x = seat.base_pixel_x;
            seat.pixel_y = seat.base_pixel_y;
        }
        seat.layer = Dm.num(initial(seat, "layer"));
    }

    // tg: code/datums/components/riding/riding.dm:246
    public virtual object turf_check(turf next = null, turf current = null)
    {
        if (allowed_turf_typecache != null && !Dm.truthy(allowed_turf_typecache[next.GetType()]))
            return allowed_turf_typecache[current.GetType()];
        else if (forbid_turf_typecache != null && Dm.truthy(forbid_turf_typecache[next.GetType()]))
            return !Dm.truthy(forbid_turf_typecache[current.GetType()]) ? TRUE : FALSE;
        return TRUE;
    }

    /// Every time the driver tries to move, this is called to see if they can actually drive and move the vehicle (via relaymove)
    // tg: code/datums/components/riding/riding.dm:254
    [SignalHandler]
    public virtual object driver_move(atom_movable movable_parent = null, mob_living user = null, object direction = null)
    {
        movable_parent.set_glide_size(DELAY_TO_GLIDE_SIZE(vehicle_move_delay));
        return null;
    }

    /// So we can check all occupants when we bump a door to see if anyone has access
    // tg: code/datums/components/riding/riding.dm:260
    [SignalHandler]
    public virtual void vehicle_bump(atom_movable movable_parent = null, obj_machinery_door possible_bumped_door = null)
    {
        if (possible_bumped_door == null || world.time - vehicle_last_bumped <= 0.3f * SECONDS)
            return;
        foreach (mob_living occupant in Dm.each<mob_living>(movable_parent.buckled_mobs))
        {
            vehicle_last_bumped = world.time;
            occupant.last_bumped = 0;
            possible_bumped_door.Bumped(occupant);
        }
    }

    // tg: code/datums/components/riding/riding.dm:268
    public virtual void Unbuckle(atom_movable M = null)
    {
        _addtimer(Dm.@new<datum_callback>(parent, nameof(atom_movable.unbuckle_mob), M), 0, TIMER_UNIQUE, file: "code/datums/components/riding/riding.dm", line: 269);
    }

    // tg: code/datums/components/riding/riding.dm:271
    public virtual object Process_Spacemove(object direction = null, object continuous_move = null)
    {
        atom_movable AM = parent as atom_movable;
        return Dm.truthy(override_allow_spacemove) || Dm.truthy(AM.has_gravity()) ? TRUE : FALSE;
    }

    /// currently replicated from ridable because we need this behavior here too, see if we can deal with that
    // tg: code/datums/components/riding/riding.dm:276
    public virtual object unequip_buckle_inhands(atom_movable user = null)
    {
        atom_movable AM = parent as atom_movable;
        foreach (obj_item_riding_offhand O in Dm.each<obj_item_riding_offhand>(user?.contents))
        {
            if (O.parent != AM)
                throw Dm.CRASH("RIDING OFFHAND ON WRONG MOB");
            if (Dm.truthy(O.selfdeleting))
                continue;
            else
                qdel(O);
        }
        return TRUE;
    }

    /// Extra checks before buckled.can_z_move can be called in mob/living/can_z_move()
    // tg: code/datums/components/riding/riding.dm:287
    [SignalHandler]
    public virtual object riding_can_z_move(atom_movable movable_parent = null, object direction = null, turf start = null, turf destination = null, object z_move_flags = null, mob_living rider = null)
    {
        return COMPONENT_RIDDEN_ALLOW_Z_MOVE;
    }

    /// Called when our vehicle gains a movement trait, so our riders can also have it
    // tg: code/datums/components/riding/riding.dm:292
    [SignalHandler]
    public virtual void on_movement_type_trait_gain(atom_movable source = null, object trait = null)
    {
        atom_movable movable_parent = parent as atom_movable;
        foreach (mob rider in Dm.each<mob>(movable_parent.buckled_mobs))
            ADD_TRAIT(rider, trait, REF(this));
    }

    /// Called when our vehicle loses a movement trait, so our riders can also lose it
    // tg: code/datums/components/riding/riding.dm:299
    [SignalHandler]
    public virtual void on_movement_type_trait_loss(atom_movable source = null, object trait = null)
    {
        atom_movable movable_parent = parent as atom_movable;
        foreach (mob rider in Dm.each<mob>(movable_parent.buckled_mobs))
            REMOVE_TRAIT(rider, trait, REF(this));
    }

    /// Blocks unbuckling (when clicked on) from the parent
    // tg: code/datums/components/riding/riding.dm:306
    [SignalHandler]
    public virtual object block_unbuckle(atom_movable source = null, mob_living unbuckler = null)
    {
        if (!Dm.truthy(source.has_buckled_mobs()) || Dm.@in(unbuckler, source.buckled_mobs))
            return NONE;

        if (other_unbuckle == CANNOT_FORCE_UNBUCKLE)
            to_chat(unbuckler, span_warning(Dm.text("You can't dismount []'s rider[]!", source, Dm.length(source.buckled_mobs) == 1 ? "" : "s")));
        else if (other_unbuckle == CAN_DISARM_UNBUCKLE)
            parent_disarmed(source as mob_living, unbuckler);

        return COMPONENT_CANCEL_ATTACK_CHAIN;
    }

    /// Unbuckles a random rider from the parent when the parent is disarmed
    // tg: code/datums/components/riding/riding.dm:320
    [SignalHandler]
    public virtual void parent_disarmed(mob_living source = null, mob_living disarmer = null)
    {
        if (!Dm.truthy(source.has_buckled_mobs()) || Dm.@in(disarmer, source.buckled_mobs))
            return;

        mob_living throwing = pick(source.buckled_mobs) as mob_living;
        float disarm_chance = get_disarm_chance(source) + get_disarm_chance(disarmer);
        if (!prob(disarm_chance))
        {
            if (disarm_chance > 0)
            {
                string plural = Dm.length(source.buckled_mobs) == 1 ? "" : "s";
                source.visible_message(
                    span_warning(Dm.text("[] shoves [] around, trying to throw off [] rider[]!", disarmer, source, source.p_their(), plural)),
                    span_warning(Dm.text("[] shoves you around, trying to throw off your rider[]!", disarmer, plural)),
                    vision_distance: COMBAT_MESSAGE_RANGE
                );
                if (Dm.truthy(source.is_blind()))
                    to_chat(source, span_warning(Dm.text("Someone shoves you around, trying to throw off your rider[]!", plural)));
                if (!Dm.truthy(disarmer.is_blind()))
                {
                    if (disarm_chance >= 50)
                        to_chat(disarmer, span_warning(Dm.text("[] barely holds stable as you shove them around! Keep at it!", source)));
                    else if (disarm_chance >= 25)
                        to_chat(disarmer, span_warning(Dm.text("[] holds stable as you shove them around! Weakening [] stability would help...", source, source.p_their())));
                    else
                        to_chat(disarmer, span_warning(Dm.text("[] effortlessly holds stable as you shove them around! You'd need to weaken [] stability...!", source, source.p_their())));
                }
            }
            return;
        }

        source.visible_message(
            span_warning(Dm.text("As [] shoves [] around, [] is thrown from []!", disarmer, source, throwing, parent)),
            span_warning(Dm.text("As [] shoves you around, [] is thrown from you!", disarmer, throwing))
        );
        if (Dm.truthy(source.is_blind()))
            to_chat(source, span_warning(Dm.text("As someone shoves you around, you feel [] get thrown from you!", Dm.length(source.buckled_mobs) == 1 ? "your rider" : "one of your riders")));

        Waiting.NoSleep(source.unbuckle_mob(throwing, force: TRUE));
        Waiting.NoSleep(throwing.Knockdown(Dm.truthy(throwing.has_status_effect(typeof(datum_status_effect_staggered))) ? SHOVE_KNOCKDOWN_COLLATERAL : SHOVE_KNOCKDOWN_HUMAN));
    }

    /// Unbuckles a rider from the parent when the rider is disarmed
    // tg: code/datums/components/riding/riding.dm:360
    [SignalHandler]
    public virtual void rider_disarmed(mob_living rider = null, mob_living disarmer = null)
    {
        float disarm_chance = get_disarm_chance(parent as mob_living) + get_disarm_chance(rider);
        if (!prob(disarm_chance))
        {
            if (disarm_chance > 0)
            {
                string how = disarm_chance >= 50 ? "barely" : (disarm_chance >= 25 ? "" : "effortlessly");
                rider.visible_message(
                    span_warning(Dm.text("[] shoves [] around, trying to throw [] off []!", disarmer, rider, rider.p_them(), parent)),
                    span_warning(Dm.text("As [] shoves you around, you [] manage to hold on to []!", disarmer, how, parent)),
                    vision_distance: COMBAT_MESSAGE_RANGE
                );
                if (Dm.truthy(rider.is_blind()))
                    to_chat(rider, span_warning(Dm.text("As someone shoves you around, you [] manage to hold on to []!", how, parent)));
                if (!Dm.truthy(disarmer.is_blind()))
                {
                    if (disarm_chance >= 50)
                        to_chat(disarmer, span_warning(Dm.text("[] barely holds on to [] as you shove them around! Keep at it!", rider, parent)));
                    else if (disarm_chance >= 25)
                        to_chat(disarmer, span_warning(Dm.text("[] holds on to [] as you shove them around! Weakening [] [] would help...", rider, parent, rider.p_their(), pick("balance", "grip", "hold"))));
                    else
                        to_chat(disarmer, span_warning(Dm.text("[] effortlessly holds on to [] as you shove them around! You'd need to weaken [] [] ...!", rider, parent, rider.p_their(), pick("balance", "grip", "hold"))));
                }
            }
            return;
        }

        rider.visible_message(
            span_warning(Dm.text("As [] shoves [] around, [] lose[] [] [] and fall[] off []!", disarmer, rider, rider.p_they(), rider.p_s(), rider.p_their(), pick("balance", "grip", "hold"), rider.p_s(), parent)),
            span_warning(Dm.text("As [] shoves you around, you lose your [] and fall off []!", disarmer, pick("balance", "grip", "hold"), parent))
        );
        if (Dm.truthy(rider.is_blind()))
            to_chat(rider, span_warning(Dm.text("As someone shoves you around, you lose your [] and fall off []!", pick("balance", "grip", "hold"), parent)));
        atom_movable riding = parent as atom_movable;
        Waiting.NoSleep(riding.unbuckle_mob(rider, force: TRUE));
        if (Dm.truthy(rider.has_status_effect(typeof(datum_status_effect_staggered))))
            Waiting.NoSleep(rider.Knockdown(2 * SECONDS));
    }

    // tg: code/datums/components/riding/riding.dm:398
    public virtual float get_disarm_chance(mob_living disarmed = null)
    {
        if (!isliving(disarmed))
            return 0;
        float staminaloss = Dm.round(Dm.num(disarmed.get_stamina_loss()), 0.1f);
        float healthpercent = Dm.round(1 - (Dm.num(disarmed.health) / Dm.num(disarmed.maxHealth)), 0.01f);

        float chance = 0;
        chance += Dm.truthy(disarmed.has_status_effect(typeof(datum_status_effect_staggered))) ? 25 : 0;
        chance += Dm.truthy(INCAPACITATED_IGNORING(disarmed, INCAPABLE_STASIS)) ? 75 : 0;
        chance += staminaloss >= 10 ? (staminaloss * 0.5f) : 0;
        chance += healthpercent >= 0.25f ? ((Dm.num(disarmed.health) / Dm.num(disarmed.maxHealth)) * 50) : 0;
        chance -= Dm.max(0, Dm.num(disarmed.mob_size) - MOB_SIZE_HUMAN) * 12.5f;
        return chance;
    }

    /// Run the Bump the rider would have hit the crystal with
    // tg: code/datums/components/riding/riding.dm:415
    [SignalHandler]
    public virtual void on_entered_supermatter(atom_movable ridden = null, atom_movable supermatter = null)
    {
        foreach (mob passenger in Dm.each_cast<mob>(ridden.buckled_mobs))
            Waiting.Detach(passenger.Bump(supermatter));
    }
}
