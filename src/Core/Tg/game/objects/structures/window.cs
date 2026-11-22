// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/window.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M3 P4: /obj/structure/window is live. Its init, its face blocking (CanAllowThrough, on_exit by dir and ON_BORDER_1),
// the atmos and smoothing hooks and the small helpers are translated; M5 slice 1: the heat exposure (atmos_sensitive), the
// damage (take_damage, repair_damage, play_attack_sound with the glass hit sound), the breaking (atom_deconstruct,
// spawn_debris: the shards, the glass debris and a reinforced window's rods) and the hits that reach it (attack_hand's knock
// and bash, attack_paw, attack_generic, tool_act, attackby); M5 slice 5: construction and deconstruction (the welder's
// repair, the screwdriver, crowbar and wrench states of a window, the reinforced window's right-click sequence and its
// cooling bolts, the rotation element); M5x slice 1: the blocks_explosives element; E13: the RCD's rcd_vals and rcd_act. Gaps ST5
// (D333): Nar'Sie's touch (a cult window), telekinetic knocking, the hulk's reach check, the hardened shuttle window's tool blockers
// and the paper frame separator (opaque while whole, torn paper overlay when damaged, patched with paper, burnt by a hot item,
// broken into wood and natural paper). Examine and the other parity-tier interactions are generated live-partial stubs
// (docs/ai/specs/live-partial.txt's P4 section) or the roots below with their milestones. The var blocks are generated.

// tg: code/game/objects/structures/window.dm:1
public partial class obj_structure_window : obj_structure
{
    // tg: code/game/objects/structures/window.dm:445
    public override object get_dumping_location()
    {
        return null;
    }

    // tg: code/game/objects/structures/window.dm:47
    [DmParams("mapload", "direct")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object direct = Dm.arg(__extra, 0);
        object dot = null;
        Waiting.NoSleep(AddElement(typeof(datum_element_blocks_explosives)));
        dot = base.Initialize(mapload, Dm.forward(__extra, direct));
        if (Dm.truthy(direct))
            Waiting.NoSleep(setDir(direct));
        if (Dm.truthy(reinf) && Dm.truthy(anchored))
            state = RWINDOW_SECURE;

        if (!Dm.truthy(reinf) && Dm.truthy(anchored))
            state = WINDOW_SCREWED_TO_FRAME;

        air_update_turf(TRUE, TRUE);

        if (Dm.truthy(fulltile))
        {
            Waiting.NoSleep(setDir());
            Dm.mask(ref obj_flags, Dm.bitnot(BLOCKS_CONSTRUCTION_DIR));
            Dm.mask(ref obj_flags, Dm.bitnot(IGNORE_DENSITY));
            // DEVIATION(DV398): a full-tile window is a block up to the level above, so it holds what stands on the open space over it, as a wall does
            Dm.combine(ref obj_flags, BLOCK_Z_IN_DOWN);
            Waiting.NoSleep(AddElement(typeof(datum_element_can_barricade)));
        }

        //windows only block while reinforced and fulltile
        if (!Dm.truthy(reinf) || !Dm.truthy(fulltile))
            set_explosion_block(0);

        Dm.combine(ref flags_1, ALLOW_DARK_PAINTS_1);
        RegisterSignal(this, COMSIG_OBJ_PAINTED, nameof(on_painted));
        Waiting.NoSleep(AddElement(typeof(datum_element_atmos_sensitive), mapload));
        Waiting.NoSleep(AddElement(typeof(datum_element_simple_rotation), ROTATION_NEEDS_ROOM, Dm.named("post_rotation_proccall", nameof(post_rotation))));

        if (Dm.truthy(Dm.bitand(flags_1, ON_BORDER_1)))
            Waiting.NoSleep(AddElement(typeof(datum_element_connect_loc), Initialize__loc_connections));
        return dot;
    }

    // tg: code/game/objects/structures/window.dm:73  var/static/list/loc_connections (a proc-level static, rules 10.6)
    private static ref DmList Initialize__loc_connections => ref TgState.Current.obj_structure_window__Initialize__loc_connections;

    // tg: code/game/objects/structures/window.dm:90
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);

        if (Dm.eq(state, WINDOW_SCREWED_TO_FRAME))
            Dm.append(ref dot, span_notice(I18n.T("The window is <b>screwed</b> to the frame.")));
        else if (Dm.eq(state, WINDOW_IN_FRAME))
            Dm.append(ref dot, span_notice(I18n.T("The window is <i>unscrewed</i> but <b>pried</b> into the frame.")));
        else if (Dm.eq(state, WINDOW_OUT_OF_FRAME))
        {
            if (Dm.truthy(anchored))
                Dm.append(ref dot, span_notice(I18n.T("The window is <b>screwed</b> to the floor. It can be <b>pried</b> into a more secure position.")));
            else
                Dm.append(ref dot, span_notice(I18n.T("The window is <i>unscrewed</i> from the floor, and could be deconstructed by <b>wrenching</b>.")));
        }
        return dot;
    }

    // tg: code/game/objects/structures/window.dm:133
    public override async DmTask<object> CanAllowThrough(atom_movable mover = null, object border_dir = null)
    {
        object dot = null;
        dot = await base.CanAllowThrough(mover, border_dir);
        if (Dm.truthy(dot))
            return dot;

        if (Dm.truthy(fulltile))
            return FALSE;

        if (Dm.eq(border_dir, dir))
            return FALSE;

        if (mover is obj_structure_window)
        {
            obj_structure_window moved_window = mover as obj_structure_window;
            return valid_build_direction(loc as turf, moved_window.dir, is_fulltile: moved_window.fulltile);
        }

        if (mover is obj_structure_windoor_assembly || mover is obj_machinery_door_window)
            return valid_build_direction(loc as turf, mover.dir, is_fulltile: FALSE);

        return TRUE;
    }

    // tg: code/game/objects/structures/window.dm:153
    [SignalHandler]
    public virtual object on_exit(datum source = null, atom_movable leaving = null, object direction = null)
    {
        if (Dm.truthy(Dm.bitand(leaving.movement_type, PHASING)))
            return null;

        if (leaving == this)
            return null; // Let's not block ourselves.

        if (Dm.truthy(Dm.bitand(leaving.pass_flags, pass_flags_self)))
            return null;

        if (Dm.truthy(fulltile))
            return null;

        if (Dm.eq(direction, dir) && Dm.truthy(density))
        {
            Waiting.NoSleep(leaving.Bump(this));
            return COMPONENT_ATOM_BLOCK_EXIT;
        }
        return null;
    }

    // tg: code/game/objects/structures/window.dm:299
    public override async DmTask<object> set_anchored(object anchorvalue = null)
    {
        object dot = null;
        dot = await base.set_anchored(anchorvalue);
        air_update_turf(TRUE, Dm.num_or_null(anchorvalue));
        update_nearby_icons();
        return dot;
    }

    // tg: code/game/objects/structures/window.dm:304
    public virtual object check_state(object checked_state = null)
    {
        if (Dm.eq(state, checked_state))
            return TRUE;
        return null;
    }

    // tg: code/game/objects/structures/window.dm:308
    public virtual object check_anchored(object checked_anchored = null)
    {
        if (Dm.eq(anchored, checked_anchored))
            return TRUE;
        return null;
    }

    // tg: code/game/objects/structures/window.dm:312
    public virtual object check_state_and_anchored(object checked_state = null, object checked_anchored = null)
    {
        return (check_state(checked_state) is var and__1 && !Dm.truthy(and__1) ? and__1 : check_anchored(checked_anchored));
    }

    // tg: code/game/objects/structures/window.dm:185  (M5 slice 1: the knock and the bash)
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        if (!Dm.truthy(await can_be_reached(user)))
            return dot;
        user.changeNext_move(CLICK_CD_MELEE);

        if (!Dm.truthy(((mob_living)user).combat_mode))
        {
            user.visible_message(span_notice(I18n.T("[] knocks on [].", user, this)),
                span_notice(I18n.T("You knock on [].", this)));
            playsound(this, knock_sound, 50, TRUE);
        }
        else
        {
            user.visible_message(span_warning(I18n.T("[] bashes []!", user, this)),
                span_warning(I18n.T("You bash []!", this)));
            playsound(this, bash_sound, 100, TRUE);
        }
        return dot;
    }

    // tg: code/game/objects/structures/window.dm:202
    public override async DmTask<object> attack_paw(mob user = null, DmList modifiers = null)
    {
        return await attack_hand(user, modifiers);
    }

    // tg: code/game/objects/structures/window.dm:205  //used by attack_alien, attack_animal
    public override async DmTask<object> attack_generic(mob user = null, float? damage_amount = null, string damage_type = null, object damage_flag = null, float? sound_effect = null, float? armor_penetration = null)
    {
        if (!Dm.truthy(await can_be_reached(user)))
            return null;
        return await base.attack_generic(user, damage_amount, damage_type, damage_flag, sound_effect, armor_penetration);
    }

    // tg: code/game/objects/structures/window.dm:210
    public override async DmTask<object> tool_act(mob_living user = null, obj_item tool = null, DmList modifiers = null)
    {
        if (!Dm.truthy(await can_be_reached(user)))
            return ITEM_INTERACT_SKIP_TO_ATTACK; // Guess you get to hit it
        // tg: code/game/objects/structures/window.dm:213
        add_fingerprint(user);
        return await base.tool_act(user, tool, modifiers);
    }

    // tg: code/game/objects/structures/window.dm:216
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.ge(atom_integrity, max_integrity))
        {
            to_chat(user, span_warning(I18n.T("[] is already in good condition!", this)));
            return ITEM_INTERACT_SUCCESS;
        }
        if (!Dm.truthy(tool.tool_start_check(user, amount: 0)))
            return FALSE;
        to_chat(user, span_notice(I18n.T("You begin repairing []...", this)));
        if (Dm.truthy(await tool.use_tool(this, user, 4 * SECONDS, volume: 50)))
        {
            await repair_damage(max_integrity);
            to_chat(user, span_notice(I18n.T("You repair [].", this)));
        }
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/window.dm:228
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        object sw__1 = state;
        if (Dm.eq(sw__1, WINDOW_SCREWED_TO_FRAME))
        {
            to_chat(user, span_notice(I18n.T("You begin to unscrew the window from the frame...")));
            if (Dm.truthy(await tool.use_tool(this, user, decon_speed, volume: 75, extra_checks: Dm.@new<datum_callback>(this, nameof(check_state_and_anchored), state, anchored))))
            {
                state = WINDOW_IN_FRAME;
                to_chat(user, span_notice(I18n.T("You unfasten the window from the frame.")));
            }
        }
        else if (Dm.eq(sw__1, WINDOW_IN_FRAME))
        {
            to_chat(user, span_notice(I18n.T("You begin to screw the window to the frame...")));
            if (Dm.truthy(await tool.use_tool(this, user, decon_speed, volume: 75, extra_checks: Dm.@new<datum_callback>(this, nameof(check_state_and_anchored), state, anchored))))
            {
                state = WINDOW_SCREWED_TO_FRAME;
                to_chat(user, span_notice(I18n.T("You fasten the window to the frame.")));
            }
        }
        else if (Dm.eq(sw__1, WINDOW_OUT_OF_FRAME))
        {
            if (Dm.truthy(anchored))
            {
                to_chat(user, span_notice(I18n.T("You begin to unscrew the frame from the floor...")));
                if (Dm.truthy(await tool.use_tool(this, user, decon_speed, volume: 75, extra_checks: Dm.@new<datum_callback>(this, nameof(check_state_and_anchored), state, anchored))))
                {
                    await set_anchored(FALSE);
                    to_chat(user, span_notice(I18n.T("You unfasten the frame from the floor.")));
                }
            }
            else
            {
                to_chat(user, span_notice(I18n.T("You begin to screw the frame to the floor...")));
                if (Dm.truthy(await tool.use_tool(this, user, decon_speed, volume: 75, extra_checks: Dm.@new<datum_callback>(this, nameof(check_state_and_anchored), state, anchored))))
                {
                    await set_anchored(TRUE);
                    to_chat(user, span_notice(I18n.T("You fasten the frame to the floor.")));
                }
            }
        }
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/window.dm:254
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(anchored))
            return FALSE;
        if (Dm.truthy(reinf) && Dm.ge(state, RWINDOW_FRAME_BOLTED))
            return FALSE;

        to_chat(user, span_notice(I18n.T("You begin to disassemble []...", this)));
        if (!Dm.truthy(await tool.use_tool(this, user, decon_speed, volume: 75, extra_checks: Dm.@new<datum_callback>(this, nameof(check_state_and_anchored), state, anchored))))
            return ITEM_INTERACT_SUCCESS;
        obj_item_stack_sheet G = await Dm.new_async(glass_type, user.loc, glass_amount) as obj_item_stack_sheet;
        if (!QDELETED(G))
        {
            // tg: code/game/objects/structures/window.dm:265
            G.add_fingerprint(user);
        }
        playsound(this, Dm.resource("sound/items/deconstruct.ogg"), 50, TRUE);
        to_chat(user, span_notice(I18n.T("You successfully disassemble [].", this)));
        qdel(this);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/window.dm:271
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(anchored))
            return FALSE;

        object sw__1 = state;
        if (Dm.eq(sw__1, WINDOW_IN_FRAME))
        {
            to_chat(user, span_notice(I18n.T("You begin to lever the window out of the frame...")));
            if (Dm.truthy(await tool.use_tool(this, user, 10 * SECONDS, volume: 75, extra_checks: Dm.@new<datum_callback>(this, nameof(check_state_and_anchored), state, anchored))))
            {
                state = WINDOW_OUT_OF_FRAME;
                to_chat(user, span_notice(I18n.T("You pry the window out of the frame.")));
            }
        }
        else if (Dm.eq(sw__1, WINDOW_OUT_OF_FRAME))
        {
            to_chat(user, span_notice(I18n.T("You begin to lever the window back into the frame...")));
            if (Dm.truthy(await tool.use_tool(this, user, 5 * SECONDS, volume: 75, extra_checks: Dm.@new<datum_callback>(this, nameof(check_state_and_anchored), state, anchored))))
            {
                state = WINDOW_SCREWED_TO_FRAME;
                to_chat(user, span_notice(I18n.T("You pry the window back into the frame.")));
            }
        }
        else
            return FALSE;

        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/window.dm:291
    public override async DmTask<object> attackby(obj_item attacking_item = null, mob user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        if (!Dm.truthy(await can_be_reached(user)))
            return TRUE; //skip the afterattack

        // tg: code/game/objects/structures/window.dm:295
        add_fingerprint(user);
        return await base.attackby(attacking_item, user, modifiers, attack_modifiers);
    }

    // tg: code/game/objects/structures/window.dm:316
    public virtual async DmTask<object> can_be_reached(mob user = null)
    {
        if (Dm.truthy(fulltile))
            return TRUE;
        float checking_dir = get_dir(user, this);
        if (!Dm.truthy(Dm.bitand(checking_dir, dir)))
            return TRUE; // Only windows on the other side may be blocked by other things.
        checking_dir = REVERSE_DIR(checking_dir);
        foreach (obj blocker in Dm.each<obj>(loc))
        {
            if (!Dm.truthy(await blocker.CanPass(user, checking_dir)))
                return FALSE;
        }
        return TRUE;
    }

    // tg: code/game/objects/structures/window.dm:329
    public override async DmTask<object> take_damage(object damage_amount = null, string damage_type = null, object damage_flag = null, float? sound_effect = null, object attack_dir = null, float? armour_penetration = null)
    {
        object dot = await base.take_damage(damage_amount, damage_type, damage_flag, sound_effect, attack_dir, armour_penetration);
        if (Dm.truthy(dot)) //received damage
            update_nearby_icons();
        return dot;
    }

    // tg: code/game/objects/structures/window.dm:334
    public override async DmTask<object> repair_damage(object amount = null)
    {
        object dot = await base.repair_damage(amount);
        update_nearby_icons();
        return dot;
    }

    // tg: code/game/objects/structures/window.dm:338
    public override object play_attack_sound(object damage_amount = null, string damage_type = null, object damage_flag = null, params object[] __extra)
    {
        damage_type ??= BRUTE;
        object sw__1 = damage_type;
        if (Dm.eq(sw__1, BRUTE))
        {
            if (Dm.truthy(damage_amount))
                playsound(this, hit_sound, 75, TRUE);
            else
                playsound(this, Dm.resource("sound/items/weapons/tap.ogg"), 50, TRUE);
        }
        else if (Dm.eq(sw__1, BURN))
            playsound(this, Dm.resource("sound/items/tools/welder.ogg"), 100, TRUE);
        return null;
    }

    // tg: code/game/objects/structures/window.dm:349
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        disassembled ??= TRUE;
        if (!Dm.truthy(disassembled))
        {
            playsound(this, break_sound, 70, TRUE);
            foreach (obj_item_shard debris in Dm.each<obj_item_shard>(await spawn_debris(drop_location())))
            {
                // tg: code/game/objects/structures/window.dm:353
                transfer_fingerprints_to(debris);
            }
        }
        update_nearby_icons();
    }

    ///Spawns shard and debris decal based on the glass_material_datum, spawns rods if window is reinforned and number of shards/rods is determined by the window being fulltile or not.
    // tg: code/game/objects/structures/window.dm:357
    public virtual async DmTask<object> spawn_debris(object location = null)
    {
        datum_material glass_material_ref = await SSmaterials.get_material(glass_material_datum) as datum_material;
        Type shard_type = glass_material_ref.shard_type;
        Type debris_type = glass_material_ref.debris_type;
        DmList dropped_debris = Dm.list();
        if (shard_type != null)
        {
            Dm.append(ref dropped_debris, Dm.@new(shard_type, location));
            if (Dm.truthy(fulltile))
                Dm.append(ref dropped_debris, Dm.@new(shard_type, location));
        }
        if (debris_type != null)
            Dm.append(ref dropped_debris, Dm.@new(debris_type, location));
        if (Dm.truthy(reinf))
            Dm.append(ref dropped_debris, Dm.@new<obj_item_stack_rods>(location, (Dm.truthy(fulltile) ? 2 : 1)));
        return dropped_debris;
    }

    // tg: code/game/objects/structures/window.dm:372
    public virtual void post_rotation(mob user = null, object degrees = null)
    {
        air_update_turf(TRUE, FALSE);
    }

    // tg: code/game/objects/structures/window.dm:375
    [SignalHandler]
    public virtual void on_painted(obj_structure_window source = null, mob user = null, obj_item_toy_crayon_spraycan spraycan = null, object is_dark_color = null)
    {
        Dm.NotTranslated("code/game/objects/structures/window.dm:375", "on_painted: spray painting (M10+)");
    }

    // tg: code/game/objects/structures/window.dm:384
    public override async DmTask<object> wash(object clean_types = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.wash(clean_types, __extra);
        if (!Dm.truthy(Dm.bitand(clean_types, CLEAN_SCRUB)))
            return dot;
        object initial_opacity = Dm.initial(this, (obj_structure_window p) => p.opacity);
        if (!Dm.eq(opacity, initial_opacity))
        {
            await set_opacity(initial_opacity);
            Dm.combine(ref dot, Dm.bitor(COMPONENT_CLEANED, COMPONENT_CLEANED_GAIN_XP));
        }
        foreach (atom_movable cleanables in Dm.each_cast<atom_movable>(this))
        {
            if (cleanables == this)
                continue;
            object cleanable_washed = await cleanables.wash(clean_types);
            if (!Dm.truthy(cleanable_washed))
                continue;
            Dm.combine(ref dot, cleanable_washed);
            Dm.remove(vis_contents, cleanables);
        }
        return dot;
    }

    // tg: code/game/objects/structures/window.dm:401
    public override object Destroy(float? force = null)
    {
        set_density(FALSE);
        air_update_turf(TRUE, FALSE);
        update_nearby_icons();
        return base.Destroy(force);
    }

    // tg: code/game/objects/structures/window.dm:407
    public override async DmTask<object> Move(atom newloc = null, object direction = null, float? glide_size_override = null, float? update_dir = null)
    {
        object dot = null;
        turf T = loc as turf;
        dot = await base.Move(newloc, direction, glide_size_override, update_dir);
        if (Dm.truthy(anchored))
            move_update_air(T);
        return dot;
    }

    // tg: code/game/objects/structures/window.dm:413
    public override object can_atmos_pass__proc(turf target_turf = null, float? vertical = null)
    {
        vertical ??= FALSE;
        if (!Dm.truthy(anchored) || !Dm.truthy(density))
            return TRUE;
        return Dm.num(!(Dm.truthy(fulltile) || dir == get_dir(loc, target_turf)));
    }

    //This proc is used to update the icons of nearby windows.
    // tg: code/game/objects/structures/window.dm:419
    public virtual void update_nearby_icons()
    {
        update_appearance();
        if (Dm.truthy(Dm.bitand(smoothing_flags, USES_SMOOTHING)))
            QUEUE_SMOOTH_NEIGHBORS(this);
    }

    //merges adjacent full-tile windows into one
    // tg: code/game/objects/structures/window.dm:425
    [DmParams("updates")]
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        float? updates = Dm.arg<float?>(__extra, 0);
        updates ??= ALL;
        DmList dot = null;
        dot = await base.update_overlays(Dm.forward(__extra, updates)) as DmList;
        if (QDELETED(this) || !Dm.truthy(fulltile))
            return dot;

        if (Dm.truthy(Dm.bitand(updates, UPDATE_SMOOTHING)) && Dm.truthy(Dm.bitand(smoothing_flags, USES_SMOOTHING)))
            QUEUE_SMOOTH(this);

        float ratio = Dm.num(Dm.div(atom_integrity, max_integrity));
        ratio = ceil(ratio * 4) * 25;
        if (ratio > 75)
            return dot;
        Dm.append(ref dot, mutable_appearance(Dm.resource("icons/obj/structures.dmi"), Dm.text("damage[]", ratio), -(layer + 0.1f)));
        return dot;
    }

    // tg: code/game/objects/structures/window.dm:439
    public override object should_atmos_process(datum_gas_mixture air = null, object exposed_temperature = null)
    {
        return Dm.num(Dm.gt(exposed_temperature, T0C + heat_resistance));
    }

    // tg: code/game/objects/structures/window.dm:442
    public override async DmTask<object> atmos_expose(datum_gas_mixture air = null, object exposed_temperature = null)
    {
        await take_damage(round(Dm.num(air.return_volume()) / 100), BURN, 0, 0);
        return null;
    }

    // tg: code/game/objects/structures/window.dm:456
    public virtual void temporary_shatter(float? time_to_go = null, float? time_to_return = null, float? take_grill = null)
    {
        Dm.NotTranslated("code/game/objects/structures/window.dm:456", "temporary_shatter: the dramatic shattering of an antagonist ability (M10+)");
    }

    // tg: code/game/objects/structures/window.dm:482
    public override async DmTask<object> Moved(atom old_loc = null, object movement_dir = null, object forced = null, DmList old_locs = null, float? momentum_change = null)
    {
        object dot = null;
        dot = await base.Moved(old_loc, movement_dir, forced, old_locs, momentum_change);
        if (Dm.truthy(loc))
            update_nearby_icons();
        return dot;
    }

    // tg: code/game/objects/structures/window.dm:638
    public virtual void cool_bolts()
    {
        if (Dm.eq(state, RWINDOW_BOLTS_HEATED))
        {
            state = RWINDOW_SECURE;
            visible_message(span_notice(I18n.T("The bolts on \\the [] look like they've cooled off...", this)));
        }
    }
}

// tg: code/game/objects/structures/window.dm:494
public partial class obj_structure_window_reinforced : obj_structure_window
{
    // tg: code/game/objects/structures/window.dm:529
    public override async DmTask<object> item_interaction_secondary(mob_living user = null, obj_item tool = null, DmList modifiers = null)
    {
        if (Dm.truthy(Dm.bitand(resistance_flags, INDESTRUCTIBLE)))
        {
            balloon_alert(user, I18n.T("too resilient!"));
            return ITEM_INTERACT_BLOCKING;
        }

        if (!Dm.truthy(tool.tool_behaviour))
            return NONE;
        // to have gotten to this point, any tool must be innapropriate for its step
        object sw__1 = state;
        if (Dm.eq(sw__1, RWINDOW_SECURE))
            to_chat(user, span_warning(I18n.T("The security screws need to be heated first!")));
        else if (Dm.eq(sw__1, RWINDOW_BOLTS_HEATED))
            to_chat(user, span_warning(I18n.T("The security screws need to be removed first!")));
        else if (Dm.eq(sw__1, RWINDOW_BOLTS_OUT))
            to_chat(user, span_warning(I18n.T("The gap needs to be pried first!")));
        else if (Dm.eq(sw__1, RWINDOW_POPPED))
            to_chat(user, span_warning(I18n.T("The bars need to be cut first!")));
        else if (Dm.eq(sw__1, RWINDOW_BARS_CUT))
            to_chat(user, span_warning(I18n.T("The bolts need to be loosened first!")));

        return ITEM_INTERACT_BLOCKING;
    }

    // tg: code/game/objects/structures/window.dm:643
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(Dm.bitand(resistance_flags, INDESTRUCTIBLE)))
            return dot;
        if (Dm.eq(state, RWINDOW_SECURE))
            Dm.append(ref dot, span_notice(I18n.T("It's been screwed in with one way screws, you'd need to <b>heat them</b> to have any chance of backing them out.")));
        else if (Dm.eq(state, RWINDOW_BOLTS_HEATED))
            Dm.append(ref dot, span_notice(I18n.T("The screws are glowing white hot, and you'll likely be able to <b>unscrew them</b> now.")));
        else if (Dm.eq(state, RWINDOW_BOLTS_OUT))
            Dm.append(ref dot, span_notice(I18n.T("The screws have been removed, revealing a small gap you could fit a <b>prying tool</b> in.")));
        else if (Dm.eq(state, RWINDOW_POPPED))
            Dm.append(ref dot, span_notice(I18n.T("The main plate of the window has popped out of the frame, exposing some bars that look like they can be <b>cut</b>.")));
        else if (Dm.eq(state, RWINDOW_BARS_CUT))
            Dm.append(ref dot, span_notice(I18n.T("The main pane can be easily moved out of the way to reveal some <b>bolts</b> holding the frame in.")));
        return dot;
    }

    // tg: code/game/objects/structures/window.dm:555
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(anchored))
            return NONE;
        if (!Dm.eq(state, WINDOW_OUT_OF_FRAME))
            return NONE;
        to_chat(user, span_notice(I18n.T("You begin to lever the window back into the frame...")));
        if (!Dm.truthy(await tool.use_tool(this, user, 10 * SECONDS, volume: 75, extra_checks: Dm.@new<datum_callback>(this, nameof(check_state_and_anchored), state, anchored))))
            return ITEM_INTERACT_BLOCKING;

        state = RWINDOW_SECURE;
        to_chat(user, span_notice(I18n.T("You pry the window back into the frame.")));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/window.dm:568
    public override async DmTask<object> welder_act_secondary(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.eq(state, RWINDOW_SECURE))
            return NONE; // we got all that messaging for innapropriate tools, no skip to attack

        if (!Dm.truthy(tool.tool_start_check(user, heat_required: HIGH_TEMPERATURE_REQUIRED)))
            return ITEM_INTERACT_BLOCKING;

        user.visible_message(span_notice(I18n.T("[] holds \\the [] to the security screws on \\the []...", user, tool, this)),
            span_notice(I18n.T("You begin heating the security screws on \\the []...", this)));
        if (!Dm.truthy(await tool.use_tool(this, user, 15 * SECONDS, volume: 100)))
            return ITEM_INTERACT_BLOCKING;

        to_chat(user, span_notice(I18n.T("The security screws are glowing white hot and look ready to be removed.")));
        state = RWINDOW_BOLTS_HEATED;
        _addtimer(Dm.@new<datum_callback>(this, nameof(cool_bolts)), 30 * SECONDS, file: "code/game/objects/structures/window.dm", line: 582);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/window.dm:585
    public override async DmTask<object> screwdriver_act_secondary(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.eq(state, RWINDOW_BOLTS_HEATED))
            return NONE;

        user.visible_message(span_notice(I18n.T("[] digs into the heated security screws and starts removing them...", user)),
            span_notice(I18n.T("You dig into the heated screws hard and they start turning...")));
        if (!Dm.truthy(await tool.use_tool(this, user, 5 * SECONDS, volume: 50)))
            return ITEM_INTERACT_BLOCKING;

        state = RWINDOW_BOLTS_OUT;
        to_chat(user, span_notice(I18n.T("The screws come out, and a gap forms around the edge of the pane.")));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/window.dm:598
    public override async DmTask<object> crowbar_act_secondary(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.eq(state, RWINDOW_BOLTS_OUT))
            return NONE;

        user.visible_message(span_notice(I18n.T("[] wedges \\the [] into the gap in the frame and starts prying...", user, tool)),
            span_notice(I18n.T("You wedge \\the [] into the gap in the frame and start prying...", tool)));
        if (!Dm.truthy(await tool.use_tool(this, user, 4 * SECONDS, volume: 50)))
            return ITEM_INTERACT_BLOCKING;

        state = RWINDOW_POPPED;
        to_chat(user, span_notice(I18n.T("The panel pops out of the frame, exposing some thin metal bars that looks like they can be cut.")));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/window.dm:611
    public override async DmTask<object> wirecutter_act_secondary(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.eq(state, RWINDOW_POPPED))
            return NONE;

        user.visible_message(span_notice(I18n.T("[] starts cutting the exposed bars on \\the []...", user, this)),
            span_notice(I18n.T("You start cutting the exposed bars on \\the []", this)));
        if (!Dm.truthy(await tool.use_tool(this, user, 2 * SECONDS, volume: 50)))
            return ITEM_INTERACT_BLOCKING;

        state = RWINDOW_BARS_CUT;
        to_chat(user, span_notice(I18n.T("The panels falls out of the way exposing the frame bolts.")));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/window.dm:624
    public override async DmTask<object> wrench_act_secondary(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.eq(state, RWINDOW_BARS_CUT))
            return NONE;

        user.visible_message(span_notice(I18n.T("[] starts unfastening \\the [] from the frame...", user, this)),
            span_notice(I18n.T("You start unfastening the bolts from the frame...")));
        if (!Dm.truthy(await tool.use_tool(this, user, 4 * SECONDS, volume: 50)))
            return ITEM_INTERACT_BLOCKING;

        to_chat(user, span_notice(I18n.T("You unscrew the bolts from the frame and the window pops loose.")));
        state = WINDOW_OUT_OF_FRAME;
        await set_anchored(FALSE);
        return ITEM_INTERACT_SUCCESS;
    }
}

// tg: code/game/objects/structures/window.dm:667
public partial class obj_structure_window_plasma : obj_structure_window
{
    // tg: code/game/objects/structures/window.dm:688
    [DmParams("mapload", "direct")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        RemoveElement(typeof(datum_element_atmos_sensitive));
        return dot;
    }
}

// tg: code/game/objects/structures/window.dm:719
public partial class obj_structure_window_reinforced_plasma : obj_structure_window_reinforced
{
    // tg: code/game/objects/structures/window.dm:719
    public override object block_superconductivity()
    {
        return TRUE;
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/structures/window.dm:73  var/static/list/loc_connections = list(COMSIG_ATOM_EXIT = PROC_REF(on_exit))
    internal DmList obj_structure_window__Initialize__loc_connections = Dm.list(
        Dm.kv(COMSIG_ATOM_EXIT, nameof(obj_structure_window.on_exit)));
}

// tg: code/game/objects/structures/window.dm:126 (interleaved types, rules 1.9; M5x slice 5)
public partial class obj_structure_window : obj_structure
{
    // tg: code/game/objects/structures/window.dm:126
    public override async DmTask<object> singularity_pull(atom singularity = null, object current_size = null)
    {
        await base.singularity_pull(singularity, current_size);
        if (Dm.truthy(anchored) && Dm.num(current_size) >= STAGE_TWO)
            await set_anchored(FALSE);
        if (Dm.num(current_size) >= STAGE_FIVE)
            await deconstruct(FALSE);
        return null;
    }
}

// tg: code/game/objects/structures/window.dm:104 (interleaved types, rules 1.9; E13: the RCD)
public partial class obj_structure_window : obj_structure
{
    // tg: code/game/objects/structures/window.dm:104
    public override object rcd_vals(mob user = null, obj_item_construction_rcd the_rcd = null, params object[] __extra)
    {
        if (Dm.eq(the_rcd.mode, RCD_DECONSTRUCT))
            return Dm.list(Dm.kv("delay", 2 * SECONDS), Dm.kv("cost", 5));
        return FALSE;
    }

    // tg: code/game/objects/structures/window.dm:109
    public override async DmTask<object> rcd_act(mob user = null, obj_item_construction_rcd the_rcd = null, DmList rcd_data = null, params object[] __extra)
    {
        if (Dm.eq(rcd_data[RCD_DESIGN_MODE], RCD_DECONSTRUCT))
        {
            qdel(this);
            return TRUE;
        }
        return FALSE;
    }
}

// tg: code/game/objects/structures/window.dm:524
public partial class obj_structure_window_reinforced : obj_structure_window
{
    // tg: code/game/objects/structures/window.dm:524
    public override object rcd_vals(mob user = null, obj_item_construction_rcd the_rcd = null, params object[] __extra)
    {
        if (Dm.eq(the_rcd.mode, RCD_DECONSTRUCT))
            return Dm.list(Dm.kv("delay", 3 * SECONDS), Dm.kv("cost", 15));
        return FALSE;
    }
}

// tg: code/game/objects/structures/window.dm:758
public partial class obj_structure_window_fulltile : obj_structure_window
{
    // tg: code/game/objects/structures/window.dm:758
    public override object rcd_vals(mob user = null, obj_item_construction_rcd the_rcd = null, params object[] __extra)
    {
        if (Dm.eq(the_rcd.mode, RCD_DECONSTRUCT))
            return Dm.list(Dm.kv("delay", 2.5f * SECONDS), Dm.kv("cost", 10));
        return FALSE;
    }
}

// tg: code/game/objects/structures/window.dm:819
public partial class obj_structure_window_reinforced_fulltile : obj_structure_window_reinforced
{
    // tg: code/game/objects/structures/window.dm:819
    public override object rcd_vals(mob user = null, obj_item_construction_rcd the_rcd = null, params object[] __extra)
    {
        if (Dm.eq(the_rcd.mode, RCD_DECONSTRUCT))
            return Dm.list(Dm.kv("mode", RCD_DECONSTRUCT), Dm.kv("delay", 4 * SECONDS), Dm.kv("cost", 20));
        return FALSE;
    }
}

// Gaps package C1 (the AI's pathfinding and movement).
public partial class obj_structure_window : obj_structure
{
    // tg: code/game/objects/structures/window.dm:448
    public override object CanAStarPass(object to_dir = null, datum_can_pass_info pass_info = null)
    {
        if (!Dm.truthy(density))
            return TRUE;
        if (Dm.truthy(fulltile) || Dm.eq(dir, to_dir))
            return FALSE;

        return TRUE;
    }
}

// Gaps ST5 (D333): the parity-tier interactions of the window and the shuttle windows, and the paper frame separator.
public partial class obj_structure_window : obj_structure
{
    // tg: code/game/objects/structures/window.dm:115
    public override async DmTask<object> narsie_act(params object[] __extra)
    {
        atom place = loc;
        await add_atom_colour(NARSIE_WINDOW_COLOUR, FIXED_COLOUR_PRIORITY);
        if (Dm.truthy(fulltile))
        {
            qdel(this);
            Dm.@new<obj_structure_window_cult_fulltile>(place);
        }
        else
        {
            qdel(this);
            Dm.@new<obj_structure_window_cult>(place);
        }
        return null;
    }

    // tg: code/game/objects/structures/window.dm:172
    public override async DmTask<object> attack_tk(mob user = null)
    {
        user.changeNext_move(CLICK_CD_MELEE);
        user.visible_message(span_notice(I18n.T("Something knocks on [].", this)));
        add_fingerprint(user);
        playsound(this, knock_sound, 50, TRUE);
        await DmTask.CompletedTask;
        return COMPONENT_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/game/objects/structures/window.dm:180
    public override async DmTask<object> attack_hulk(mob_living_carbon_human user = null, params object[] __extra)
    {
        if (!Dm.truthy(await can_be_reached(user)))
            return null;
        return await base.attack_hulk(user, __extra);
    }
}

// tg: code/game/objects/structures/window.dm:851
public partial class obj_structure_window_reinforced_shuttle : obj_structure_window_reinforced
{
    // tg: code/game/objects/structures/window.dm:881
    public override async DmTask<object> narsie_act(params object[] __extra)
    {
        await add_atom_colour("#3C3434", FIXED_COLOUR_PRIORITY);
        return null;
    }
}

// tg: code/game/objects/structures/window.dm:891
public partial class obj_structure_window_reinforced_shuttle_indestructible : obj_structure_window_reinforced_shuttle
{
    // tg: code/game/objects/structures/window.dm:896
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_tool_blocker), TOOL_WELDER, TOOL_ACT_PRIMARY));
        Waiting.NoSleep(AddElement(typeof(datum_element_tool_blocker), TOOL_SCREWDRIVER, TOOL_ACT_PRIMARY));
        Waiting.NoSleep(AddElement(typeof(datum_element_tool_blocker), TOOL_WRENCH, TOOL_ACT_PRIMARY));
        Waiting.NoSleep(AddElement(typeof(datum_element_tool_blocker), TOOL_CROWBAR, TOOL_ACT_PRIMARY));
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/structures/window.dm:1037  var/static/mutable_appearance/torn
    internal mutable_appearance obj_structure_window_paperframe__torn;
    // tg: code/game/objects/structures/window.dm:1038  var/static/mutable_appearance/paper
    internal mutable_appearance obj_structure_window_paperframe__paper;
}

// tg: code/game/objects/structures/window.dm:1012
public partial class obj_structure_window_paperframe : obj_structure_window
{
    // tg: code/game/objects/structures/window.dm:1037
    public static mutable_appearance torn => TgState.Current.obj_structure_window_paperframe__torn ??=
        (mutable_appearance)mutable_appearance(Dm.resource("icons/obj/smooth_structures/structure_variations.dmi"), icon_state: "paper-torn", layer: ABOVE_OBJ_LAYER - 0.1f);
    // tg: code/game/objects/structures/window.dm:1038
    public static mutable_appearance paper => TgState.Current.obj_structure_window_paperframe__paper ??=
        (mutable_appearance)mutable_appearance(Dm.resource("icons/obj/smooth_structures/structure_variations.dmi"), icon_state: "paper-whole", layer: ABOVE_OBJ_LAYER - 0.1f);

    // tg: code/game/objects/structures/window.dm:1040
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        update_appearance();
        return dot;
    }

    // tg: code/game/objects/structures/window.dm:1044
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.num(atom_integrity) < Dm.num(max_integrity))
            Dm.append(ref dot, span_info(I18n.T("It looks a bit damaged, you may be able to fix it with some <b>paper</b>.")));
        return dot;
    }

    // tg: code/game/objects/structures/window.dm:1049
    public override async DmTask<object> spawn_debris(object location = null)
    {
        DmList dot = Dm.list(Dm.@new<obj_item_stack_sheet_mineral_wood>(location));
        float count = rand(1, 4);
        for (int i = 1; i <= count; i++)
            Dm.append(ref dot, Dm.@new<obj_item_paper_natural>(location));
        await DmTask.CompletedTask;
        return dot;
    }

    // tg: code/game/objects/structures/window.dm:1054
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers);
        if (Dm.truthy(dot))
            return dot;
        if (Dm.truthy((user as mob_living)?.combat_mode))
        {
            await take_damage(4, BRUTE, MELEE, 0);
            if (!QDELETED(this))
                update_appearance();
        }
        return dot;
    }

    // tg: code/game/objects/structures/window.dm:1063
    public override object update_appearance(float? updates = null)
    {
        object dot = base.update_appearance(updates);
        Waiting.NoSleep(set_opacity(Dm.num(Dm.num(atom_integrity) >= Dm.num(max_integrity))));
        return dot;
    }

    // tg: code/game/objects/structures/window.dm:1067
    public override async DmTask<object> update_icon(float? updates = null, params object[] __extra)
    {
        updates ??= ALL;
        object dot = await base.update_icon(updates, __extra);
        if (Dm.truthy(Dm.bitand(updates, UPDATE_SMOOTHING)) && Dm.truthy(Dm.bitand(smoothing_flags, USES_SMOOTHING)))
            QUEUE_SMOOTH(this);
        return dot;
    }

    // tg: code/game/objects/structures/window.dm:1072
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        Dm.append(ref dot, Dm.num(atom_integrity) < Dm.num(max_integrity) ? torn : paper);
        return dot;
    }

    // tg: code/game/objects/structures/window.dm:1076
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.num(tool.get_temperature()) >= FIRE_MINIMUM_TEMPERATURE_TO_EXIST)
        {
            await fire_act(tool.get_temperature());
            return ITEM_INTERACT_SUCCESS;
        }

        if (Dm.truthy(user.combat_mode))
            return NONE;

        if (!istype(tool, typeof(obj_item_paper)) || Dm.eq(atom_integrity, max_integrity))
            return NONE;

        user.visible_message(span_notice(I18n.T("[] starts to patch the holes in \\the [].", user, this)));
        if (!Dm.truthy(await do_after(user, 2 * SECONDS, target: this)))
            return ITEM_INTERACT_BLOCKING;

        atom_integrity = Dm.min(Dm.num(atom_integrity) + 4, max_integrity);
        qdel(tool);
        user.visible_message(span_notice(I18n.T("[] patches some of the holes in \\the [].", user, this)));
        if (Dm.eq(atom_integrity, max_integrity))
            update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }
}
