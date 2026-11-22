// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/tripwire.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// gaps ST5 (D333): the file whole. The stanchions (a crafting recipe) and the sturdy cable crafted from thirty cable: the cable tied
// to one anchored post, then fed to another in a straight line up to four tiles away (three seconds a tile) lays tg's heavy cable
// pieces (`tripwire_1` to `_3` facing outward from the middle) and ties both posts (`tripwire_post_tied`); a blocked tile in the way
// unties it all. The wire stops a running mob that crosses it (not one walking, lying, floating, flying or without gravity, nor a
// projectile or anything moving along it) and topples a mech (but the Clarke and a phasing Phazon) that strides across it, crushing
// what it falls on, hurting its pilot's head and righting it after a while. Wirecutters cut a wire or a post's whole line back into
// cable; a wrench (un)anchors an untied post; a sharp thing hits a wire ten times as hard. The var blocks are generated (D111).

// tg: code/game/objects/structures/tripwire.dm:1
public partial class obj_structure_tripwire : obj_structure
{
    // tg: code/game/objects/structures/tripwire.dm:223
    public override async DmTask<object> CanAllowThrough(atom_movable mover = null, object border_dir = null)
    {
        object dot = await base.CanAllowThrough(mover, border_dir);

        if (Dm.truthy(dot))
            return dot;

        if (isprojectile(mover))
            return TRUE;

        if (Dm.eq(mover.dir, dir) || Dm.eq(mover.dir, REVERSE_DIR(dir)))
            return TRUE;

        if (!Dm.truthy(mover.has_gravity()))
            return TRUE;

        if (Dm.truthy(Dm.bitand(mover.movement_type, MOVETYPES_NOT_TOUCHING_GROUND)))
            return TRUE;

        if (iscarbon(mover))
        {
            mob_living_carbon moving_carbon = (mob_living_carbon)mover;
            if (Dm.eq(moving_carbon.body_position, LYING_DOWN)) // Crawl under it. Bonus of allowing dragging dying people.
                return TRUE;

            if (Dm.eq(moving_carbon.move_intent, MOVE_INTENT_WALK))
                return TRUE;
        }

        if (ismecha(mover))
        {
            if (mover is obj_vehicle_sealed_mecha_clarke) // treads
                return FALSE;

            return TRUE;
        }

        return FALSE;
    }

    // tg: code/game/objects/structures/tripwire.dm:258
    public virtual async DmTask on_entered(datum source = null, atom_movable entered = null)
    {
        if (!ismecha(entered))
            return;

        obj_vehicle_sealed_mecha falling_down = (obj_vehicle_sealed_mecha)entered;
        if (Dm.truthy(falling_down.toppled)) // did you trip directly into a tripwire? We don't want this to chain, however amusing the image might be.
            return;

        if (Dm.truthy((falling_down as obj_vehicle_sealed_mecha_phazon)?.phasing))
            return;

        if (Dm.eq(falling_down.dir, dir) || Dm.eq(falling_down.dir, REVERSE_DIR(dir)))
            return;

        float damage_value = Dm.num(falling_down.max_integrity) / 5;

        float falling_angle;
        if (Dm.eq(falling_down.dir, WEST))
            falling_angle = 270;
        else if (Dm.eq(falling_down.dir, EAST))
            falling_angle = 90;
        else
            falling_angle = Dm.num(pick(90, 270));

        // For disambiguation, damage_value is the mech's health / 5. damage_value is the base for all the rest of the numbers.
        // damage_value is applied directly as damage to whoever it falls on. That person is also paralyzed for damage_value / 10 seconds.
        // The pilot of the falling mech receives damage_value / 5 as a head trauma.
        // The mech itself receives damage_value * 0.15 seconds of immobility(can still attack, but can't rotate). (damage_value / 10 * 1.5)
        falling_down.toppled = TRUE;
        await falling_down.forceMove(get_turf(this));
        object tripped = await falling_down.fall_and_crush(get_step(falling_down, falling_down.dir), damage_value, 15, paralyze_time: damage_value, rotation: falling_angle);
        _addtimer(Dm.@new<datum_callback>(falling_down, nameof(obj_vehicle_sealed_mecha.right_self), falling_angle), damage_value * 1.5f, file: "code/game/objects/structures/tripwire.dm", line: 289);
        if (!Dm.truthy(Dm.bitand(tripped, SUCCESSFULLY_FELL_OVER)))
        {
            falling_down.toppled = FALSE;
            return; // Don't know how it'd be avoided, but if you beat it, you beat it
        }

        DmList drivers = falling_down.return_drivers();

        visible_message(span_danger(I18n.T("[] topples over \\the []!", falling_down, this)),
            blind_message: span_danger(I18n.T("You hear a deafening CRASH!")),
            ignored_mobs: drivers);

        foreach (mob_living driver in Dm.each_cast<mob_living>(drivers)) // can any mechs have two drivers? No. Could they? yes.
        {
            float damage = Dm.num(await driver.apply_damage(damage_value / 3, BRUTE, BODY_ZONE_HEAD, Dm.num_or_null(driver.run_armor_check(BODY_ZONE_HEAD, MELEE)), wound_bonus: 30, wound_clothing: FALSE));
            string falling_string = null;
            if (damage == 0)
                falling_string = I18n.T("You rapidly fall forward but only lightly hit []'s controls. ", falling_down);
            else if (damage >= 0 && damage <= 10)
                falling_string = I18n.T("You rapidly fall forward and hit your head on []'s controls! ", falling_down);
            else if (damage >= 10 && damage <= INFINITY)
                falling_string = I18n.T("You rapidly fall forward and smash your head against []'s controls! ", falling_down);

            falling_string += I18n.T("When you recover, you [] begin righting [].", damage_value > 40 ? I18n.T("slowly") : I18n.T("quickly"), falling_down);
            to_chat(driver, span_danger(falling_string));
        }
    }
}

// tg: code/game/objects/structures/tripwire.dm:10
public partial class obj_structure_tripwire_post : obj_structure_tripwire
{
    // tg: code/game/objects/structures/tripwire.dm:19
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        DmList loc_connections = TgState.Current.obj_structure_tripwire_post__Initialize__loc_connections ??= Dm.list(
            Dm.kv(COMSIG_ATOM_ENTERED, nameof(on_entered)));
        Waiting.NoSleep(AddElement(typeof(datum_element_connect_loc), loc_connections));
        return dot;
    }

    // tg: code/game/objects/structures/tripwire.dm:26
    public override object Destroy(float? force = null)
    {
        clear_wire();
        return base.Destroy(force);
    }

    // tg: code/game/objects/structures/tripwire.dm:30
    public virtual void clear_wire()
    {
        if (opposing_post != null)
        {
            opposing_post.opposing_post = null;
            opposing_post.clear_wire();
            opposing_post = null;
        }
        foreach (object tripwire in Dm.each(tripwires))
        {
            UnregisterSignal(tripwire as datum, COMSIG_QDELETING);
            qdel(tripwire);
        }
        tripwires = Dm.list();
        update_appearance(UPDATE_ICON);
    }

    // tg: code/game/objects/structures/tripwire.dm:41
    public virtual void wire_qdeleted(datum source = null)
    {
        Dm.remove(ref tripwires, source);
        UnregisterSignal(source, COMSIG_QDELETING);
        clear_wire();
    }

    // tg: code/game/objects/structures/tripwire.dm:46
    public virtual void connect_to_post(obj_structure_tripwire_post connected_post = null, object new_direction = null)
    {
        dir = Dm.num(new_direction);
        opposing_post = connected_post;
        update_appearance(UPDATE_ICON);
    }

    // tg: code/game/objects/structures/tripwire.dm:51
    public override object update_appearance(float? updates = null)
    {
        object dot = base.update_appearance(updates);
        icon_state = Dm.text("tripwire_post[]", opposing_post != null ? "_tied" : "");
        return dot;
    }

    // tg: code/game/objects/structures/tripwire.dm:55
    public override async DmTask<object> wirecutter_act(mob_living user = null, obj_item tool = null)
    {
        if (opposing_post == null)
            return NONE;

        visible_message(span_notice(I18n.T("[] begins cutting through \\the []'s tripwire...", user, this)),
            span_notice(I18n.T("You begin cutting through \\the []'s tripwire...", this)),
            span_hear(I18n.T("You hear snipping.")));
        if (!Dm.truthy(await tool.use_tool(this, user, 5 * SECONDS)))
            return ITEM_INTERACT_BLOCKING;

        if (opposing_post == null)
            return ITEM_INTERACT_BLOCKING;

        visible_message(span_notice(I18n.T("[] cuts away \\the []'s tripwire.", user, this)),
            span_notice(I18n.T("You finish cutting \\the []'s tripwire.", this)),
            span_hear(I18n.T("You hear a final snip.")));
        Dm.@new<obj_item_stack_cable_coil>(drop_location(), 20);
        clear_wire();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/tripwire.dm:75
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is not obj_item_tripwire_cable)
            return NONE;

        if (!Dm.truthy(anchored) || opposing_post != null)
            return NONE;

        obj_item_tripwire_cable irrelevant_cable = (obj_item_tripwire_cable)tool;

        obj_structure_tripwire_post connecting_post = irrelevant_cable.connecting_post?.resolve() as obj_structure_tripwire_post;

        float are_we_tying = FALSE;
        if (connecting_post == null || !Dm.eq(connecting_post.z, z))
            are_we_tying = TRUE;

        float distance_between = Dm.num(get_dist(this, connecting_post));
        if (distance_between > 6)
            are_we_tying = TRUE;

        if (Dm.truthy(are_we_tying)) // if it's on a different z level, or further than would be reasonable, refuse to acknowledge the distance and mulligan.
        {
            to_chat(user, span_notice(I18n.T("You tie one end of \\the [] around \\the [].", irrelevant_cable, this)));
            irrelevant_cable.connecting_post = WEAKREF(this) as datum_weakref;
            return ITEM_INTERACT_SUCCESS;
        }

        if (connecting_post == this)
            return ITEM_INTERACT_BLOCKING;

        if (!Dm.truthy(connecting_post.anchored))
            return ITEM_INTERACT_BLOCKING;

        if (distance_between > 4)
        {
            to_chat(user, span_notice(I18n.T("The post on the other end is too far away!")));
            return ITEM_INTERACT_BLOCKING;
        }

        if (distance_between == 0)
        {
            to_chat(user, span_notice(I18n.T("No point in connecting them, they're already together.")));
            return ITEM_INTERACT_BLOCKING;
        }

        if (!Dm.eq(connecting_post.x, x) && !Dm.eq(connecting_post.y, y))
        {
            to_chat(user, span_notice(I18n.T("The wire will be much less effective at an angle.")));
            return ITEM_INTERACT_BLOCKING;
        }

        turf end_point_turf = get_turf(connecting_post);

        if (!Dm.truthy(await do_after(user, distance_between * 3 * SECONDS, this)))
            return ITEM_INTERACT_BLOCKING;

        if (!Dm.truthy(anchored) || opposing_post != null || !Dm.truthy(connecting_post?.anchored) || connecting_post.opposing_post != null || end_point_turf != get_turf(connecting_post))
            return ITEM_INTERACT_BLOCKING;

        float direction_to_extend = Dm.num(get_dir(this, connecting_post));
        turf iterating_turf = get_turf(this);
        for (float distance_extended = 1; distance_extended <= distance_between; distance_extended++)
        {
            iterating_turf = get_step(iterating_turf, direction_to_extend) as turf;
            if (iterating_turf == end_point_turf)
            {
                connect_to_post(connecting_post, direction_to_extend);
                connecting_post.connect_to_post(this, REVERSE_DIR(direction_to_extend));
                to_chat(user, span_notice(I18n.T("You successfully wire together the two posts.")));
                qdel(irrelevant_cable);
                return ITEM_INTERACT_SUCCESS;
            }

            if (Dm.truthy(await iterating_turf.is_blocked_turf(TRUE)))
            {
                clear_wire();
                to_chat(user, span_notice(I18n.T("You can't feed the cable through solid objects.")));
                return ITEM_INTERACT_BLOCKING;
            }

            obj_structure_tripwire_cable new_wire = Dm.@new<obj_structure_tripwire_cable>(iterating_turf);
            // if(1) we'd have already dropped out at connecting the two posts together, and wouldn't get here
            if (distance_between == 2)
                new_wire.icon_state = "tripwire_1"; // post - wire(1) - post
            else if (distance_between == 3)
                new_wire.icon_state = "tripwire_2"; // post - wire(2)(L) - wire(2)(R) - post
            else if (distance_between == 4)
                new_wire.icon_state = Dm.text("tripwire_[]", distance_extended == 2 ? "3" : "2"); // post - wire(2)(L) - wire(3) - wire(2)(R) - post

            new_wire.dir = (distance_extended > (distance_between / 2)) ? direction_to_extend : REVERSE_DIR(direction_to_extend); // Switch directions once we're halfway there


            RegisterSignal(new_wire, COMSIG_QDELETING, nameof(wire_qdeleted));
            Dm.append(ref tripwires, new_wire);
        }

        // HOW DID WE GET HERE WE SHOULD NEVER GET HERE BURN IT ALL
        clear_wire();
        return ITEM_INTERACT_BLOCKING;
    }

    // tg: code/game/objects/structures/tripwire.dm:161
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        if (opposing_post != null)
        {
            balloon_alert(user, I18n.T("unwire first!"));
            return ITEM_INTERACT_BLOCKING;
        }

        if (!Dm.truthy(await tool.use_tool(this, user, 0)))
            return ITEM_INTERACT_BLOCKING;

        balloon_alert(user, I18n.T("[]anchored", Dm.truthy(anchored) ? "un" : ""));
        await set_anchored(Dm.not(anchored));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/tripwire.dm:173
    public override async DmTask<object> Moved(atom old_loc = null, object movement_dir = null, object forced = null, DmList old_locs = null, float? momentum_change = null)
    {
        clear_wire();
        return await base.Moved(old_loc, movement_dir, forced, old_locs, momentum_change);
    }

    // tg: code/game/objects/structures/tripwire.dm:177
    public override async DmTask<object> CanAllowThrough(atom_movable mover = null, object border_dir = null)
    {
        if (opposing_post == null)
            return TRUE;

        return await base.CanAllowThrough(mover, border_dir);
    }

    // tg: code/game/objects/structures/tripwire.dm:183
    public override async DmTask on_entered(datum source = null, atom_movable entered = null)
    {
        if (opposing_post == null)
            return;

        await base.on_entered(source, entered);
    }
}

// tg: code/game/objects/structures/tripwire.dm:189
public partial class obj_structure_tripwire_cable : obj_structure_tripwire
{
    // tg: code/game/objects/structures/tripwire.dm:194
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        RegisterSignal(get_turf(this), COMSIG_ATOM_ENTERED, nameof(on_entered)); // No need for connect_loc, we won't be moving.
        return dot;
    }

    // tg: code/game/objects/structures/tripwire.dm:198
    public override async DmTask<object> attackby(obj_item attacking_item = null, mob user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        if (Dm.truthy(attacking_item.sharpness))
        {
            // MODIFY_ATTACK_FORCE_MULTIPLIER(attack_modifiers, 10) (__DEFINES/combat.dm:427)
            if (!islist(attack_modifiers)) { attack_modifiers = Dm.list(); }
            if (!Dm.@in(FORCE_MULTIPLIER, attack_modifiers)) { attack_modifiers[FORCE_MULTIPLIER] = 1; }
            attack_modifiers[FORCE_MULTIPLIER] = Dm.mul(attack_modifiers[FORCE_MULTIPLIER], 10);
        }
        return await base.attackby(attacking_item, user, modifiers, attack_modifiers);
    }

    // tg: code/game/objects/structures/tripwire.dm:203
    public override async DmTask<object> wirecutter_act(mob_living user = null, obj_item tool = null)
    {
        visible_message(span_notice(I18n.T("[] begins cutting through \\the []...", user, this)),
            span_notice(I18n.T("You begin cutting through \\the []...", this)),
            span_hear(I18n.T("You hear snipping.")));

        if (!Dm.truthy(await tool.use_tool(this, user, 5 * SECONDS)))
            return ITEM_INTERACT_BLOCKING;

        visible_message(span_notice(I18n.T("[] cuts away \\the [].", user, this)),
            span_notice(I18n.T("You finish cutting \\the [].", this)),
            span_hear(I18n.T("You hear a final snip.")));
        Dm.@new<obj_item_stack_cable_coil>(drop_location(), 20);
        qdel(this);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/tripwire.dm:218
    public override async DmTask<object> Moved(atom old_loc = null, object movement_dir = null, object forced = null, DmList old_locs = null, float? momentum_change = null)
    {
        object dot = await base.Moved(old_loc, movement_dir, forced, old_locs, momentum_change);
        if (!QDELING(this))
            qdel(this);
        return dot;
    }
}

// tg: code/game/objects/structures/tripwire.dm:314
public partial class obj_item_tripwire_cable : obj_item
{
    // tg: code/game/objects/structures/tripwire.dm:326
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(connecting_post.resolve()))
            return null;
        to_chat(user, span_notice(I18n.T("You recoil the length of cable until it's all back together again.")));
        connecting_post = null;
        await DmTask.CompletedTask;
        return null;
    }

    // tg: code/game/objects/structures/tripwire.dm:332
    public override async DmTask<object> wirecutter_act(mob_living user = null, obj_item tool = null)
    {
        to_chat(user, span_notice(I18n.T("You cut off the cable's excess bulk.")));
        Dm.@new<obj_item_stack_cable_coil>(drop_location(), 15);
        qdel(this);
        await DmTask.CompletedTask;
        return ITEM_INTERACT_SUCCESS;
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/structures/tripwire.dm:21  /obj/structure/tripwire/post/Initialize's var/static/list/loc_connections
    internal DmList obj_structure_tripwire_post__Initialize__loc_connections;
}
