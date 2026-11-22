// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/tables_racks.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M3 P4): the table's init, its passage (CanAllowThrough, on_exit when flipped), its smoothing (update_icon) and
// the init of the glass and fancy wood tables are translated, so that the tables of the map smooth and block as in tg;
// M4a P4b: placing an item on a table (base_item_interaction, table_place_act by the click's icon-x and icon-y, tray_act)
// and the hand on a flipped table; the reinforced table's welder (weakening and strengthening), its tool acts' shock check
// (attempt_electrocution: the M7 shocks slice) and the operating table's breath mask and tank (item_interaction, the mask's
// tether) and its placing; M8y slice A (D223): the rest of the operating table (its buckle, the patient it marks, the mask
// and tank tools, connecting the tank to the patient's internals; gaps P1: the operating computer's surgery data);
// flipping, slamming, construction, destruction, tools and examine are later milestones (the roots below with
// their milestones, and the generated live-partial stubs of docs/ai/specs/live-partial.txt's P4 section). Gaps ST5 (D333): Nar'Sie's
// touch (a wooden table; the glass table reddens, the wooden ones stay), telekinesis, the greyscale material table and the rolling
// table (it carries what is put on it and the mobs on top when pushed). /obj/structure/table is in the live base (wave 4); the var
// blocks are generated.

/*
 * Tables
 */
// tg: code/game/objects/structures/tables_racks.dm:15
[DmPath("/obj/structure/table")]
public partial class obj_structure_table : obj_structure
{
    // tg: code/game/objects/structures/tables_racks.dm:65
    [DmParams("mapload", "frame_used", "stack_used")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        obj_structure_table_frame frame_used = Dm.arg<obj_structure_table_frame>(__extra, 0);
        obj_item_stack stack_used = Dm.arg<obj_item_stack>(__extra, 1);
        object dot = null;
        dot = base.Initialize(mapload, Dm.forward(__extra, frame_used, stack_used));
        if (Dm.truthy(frame_used))
            apply_frame_properties(frame_used);
        if (Dm.truthy(stack_used))
            Waiting.NoSleep(apply_stack_properties(stack_used));

        before_flipped_matrix = transform as matrix;
        on_init_smoothed_vars = Dm.list(smoothing_groups, canSmoothWith);

        Waiting.NoSleep(AddElement(typeof(datum_element_connect_loc), Initialize__loc_connections));
        Dm.Deferred("code/game/objects/structures/tables_racks.dm:80", "Initialize: register_context(): screentip contexts (M10+)");

        if (Dm.truthy(can_flip))
            Dm.Deferred("code/game/objects/structures/tables_racks.dm:83", "Initialize: AddElement(/datum/element/contextual_screentip_bare_hands, rmb_text = \"Flip\"): screentips (M10+)");

        ADD_TRAIT(this, TRAIT_COMBAT_MODE_SKIP_INTERACTION, INNATE_TRAIT);

        if (Dm.truthy(can_flip) && Dm.truthy(is_flipped))
        {
            Waiting.NoSleep(flip_table(dir));
            return dot;
        }

        Waiting.NoSleep(make_climbable());
        Dm.Deferred("code/game/objects/structures/tables_racks.dm:96", "Initialize: AddElement(/datum/element/give_turf_traits, turf_traits): the traits a table gives its turf (slowdown, slipping, immersion; M4a)");
        // tg: code/game/objects/structures/tables_racks.dm:93
        Waiting.NoSleep(AddElement(typeof(datum_element_footstep_override), Dm.named("priority", STEP_SOUND_TABLE_PRIORITY)));
        Waiting.NoSleep(AddElement(typeof(datum_element_table_smash), Dm.named("gentle_push", slam_gently), Dm.named("after_smash_proccall", nameof(after_smash)))); // gaps L1
        return dot;
    }

    // tg: code/game/objects/structures/tables_racks.dm:76  var/static/list/loc_connections (a proc-level static, rules 10.6)
    private static ref DmList Initialize__loc_connections => ref TgState.Current.obj_structure_table__Initialize__loc_connections;

    // tg: code/game/objects/structures/tables_racks.dm:236
    public override async DmTask<object> CanAllowThrough(atom_movable mover = null, object border_dir = null)
    {
        object dot = null;
        dot = await base.CanAllowThrough(mover, border_dir);
        if (Dm.truthy(dot))
            return dot;

        if (!Dm.truthy(is_flipped))
            return FALSE;

        if (Dm.truthy(is_transparent)) //Glass table, jolly ranchers pass
        {
            if (mover is atom_movable && Dm.truthy(Dm.bitand(mover.pass_flags, PASSGLASS)))
                return TRUE;
        }
        if (isprojectile(mover))
        {
            // The M7 projectiles slice
            obj_projectile proj = mover as obj_projectile;
            //Lets through bullets shot from behind the cover of the table
            if (Dm.truthy(proj.movement_vector) && angle2dir_cardinal(Dm.num(proj.movement_vector.angle)) == dir)
                return TRUE;
            return FALSE;
        }
        if (Dm.eq(border_dir, dir))
            return FALSE;

        return TRUE;
    }

    // tg: code/game/objects/structures/tables_racks.dm:258
    public override async DmTask<object> update_icon(float? updates = null, params object[] __extra)
    {
        updates ??= ALL;
        object dot = null;
        dot = await base.update_icon(updates, __extra);
        if (Dm.truthy(Dm.bitand(updates, UPDATE_SMOOTHING)) && Dm.truthy(Dm.bitand(smoothing_flags, USES_SMOOTHING)))
        {
            QUEUE_SMOOTH(this);
            QUEUE_SMOOTH_NEIGHBORS(this);
        }
        return dot;
    }
}

// M3 contracts (phase B): the roots of this file's procs that no part declared yet, with the signatures of
// docs/ai/specs/generated/slots.tsv (DmTask exactly where suspends = yes), Dm.NotTranslated until M3 P4 translates
// them (docs/ai/specs/m3-ownership.md). Overrides are not declared, so the inherited code keeps running until then.

// tg: code/game/objects/structures/tables_racks.dm:97
public partial class obj_structure_table : obj_structure
{
    // tg: code/game/objects/structures/tables_racks.dm:97
    public virtual void apply_frame_properties(obj_structure_table_frame frame_used = null)
    {
        frame = frame_used.type;
        framestack = frame_used.framestack;
        framestackamount = frame_used.framestackamount;
    }

    // tg: code/game/objects/structures/tables_racks.dm:103
    public virtual async DmTask apply_stack_properties(obj_item_stack stack_used = null)
    {
        return;
    }

    // tg: code/game/objects/structures/tables_racks.dm:107
    public virtual async DmTask make_climbable()
    {
        await AddElement(typeof(datum_element_climb_walkable));
        await AddElement(typeof(datum_element_climbable));
        Dm.Deferred("code/game/objects/structures/tables_racks.dm:110", "make_climbable: AddElement(/datum/element/elevation, pixel_shift = 12): the raised position of what stands on it: the client draws a standing mob on the structure's top instead (D239, DV154)");
    }

    //proc that adds elements present in normal tables
    // tg: code/game/objects/structures/tables_racks.dm:113  (gaps L1)
    public virtual async DmTask unflip_table()
    {
        playsound(this, unflip_table_sound, 100);
        await make_climbable();
        // AddElement(/datum/element/give_turf_traits, turf_traits): the table's turf traits are a deferred leaf of Initialize (M4a)
        await AddElement(typeof(datum_element_footstep_override), Dm.named("priority", STEP_SOUND_TABLE_PRIORITY));
        //resets vars from table being flipped
        layer = TABLE_LAYER;
        smoothing_flags = Dm.bitor(smoothing_flags, SMOOTH_BITMASK);
        pass_flags_self = Dm.bitor(pass_flags_self, PASSTABLE);
        if (Dm.truthy(use_matrices_instead))
            animate(this, Dm.named("transform", before_flipped_matrix), Dm.named("time", 0));
        else
            icon = Dm.initial(this, nameof(icon));
        icon_state = Dm.initial(this, nameof(icon_state)) as string;
        smoothing_groups = on_init_smoothed_vars[1] as DmList;
        canSmoothWith = on_init_smoothed_vars[2] as DmList;
        update_appearance();
        is_flipped = FALSE;
        flip_thrown.Clear(); // DEVIATION(DV420)
    }

    //proc that removes elements present in now-flipped tables
    // tg: code/game/objects/structures/tables_racks.dm:133  (gaps L1)
    public virtual async DmTask flip_table(float? new_dir = null)
    {
        new_dir ??= SOUTH;
        playsound(this, flipped_table_sound, 100);
        RemoveElement(typeof(datum_element_climb_walkable));
        RemoveElement(typeof(datum_element_climbable));
        RemoveElement(typeof(datum_element_footstep_override), Dm.named("priority", STEP_SOUND_TABLE_PRIORITY));
        // RemoveElement(/datum/element/give_turf_traits, turf_traits), RemoveElement(/datum/element/elevation, pixel_shift = 12): neither
        // is added (deferred leaves of Initialize and make_climbable, DV154)

        //change icons
        layer = LOW_ITEM_LAYER;
        if (Dm.truthy(Dm.bitand(new_dir, EAST))) // Dirs need to be part of the 4 main cardinal directions so proc/CanAllowThrough isn't fucky wucky
            new_dir = EAST;
        else if (Dm.truthy(Dm.bitand(new_dir, WEST)))
            new_dir = WEST;
        dir = new_dir.Value;
        if (new_dir == SOUTH)
            layer = ABOVE_MOB_LAYER;

        turf throw_target = get_step(this, dir);
        if (throw_target != null)
        {
            foreach (atom_movable movable_entity in Dm.each<atom_movable>(((atom)loc).contents.Copy()))
            {
                if (Dm.truthy(is_able_to_throw(this, movable_entity)))
                {
                    flip_thrown.Add(movable_entity); // DEVIATION(DV420): see on_exit
                    Waiting.Detach(movable_entity.safe_throw_at(throw_target, range: 1, speed: 1, force: MOVE_FORCE_NORMAL, gentle: TRUE));
                }
            }
        }

        smoothing_flags = Dm.bitand(smoothing_flags, ~SMOOTH_BITMASK);
        smoothing_groups = null;
        canSmoothWith = null;
        pass_flags_self = Dm.bitand(pass_flags_self, ~PASSTABLE);

        if (Dm.truthy(use_matrices_instead))
        {
            icon_state = Dm.initial(this, nameof(icon_state)) as string;
            before_flipped_matrix = transform as matrix;
            matrix transform_matrix = Dm.matrix(1, 0, 0, 0, 0.350f, 9); // "flips" the table
            //there's probably a nicer way to do this but whatever. rotates the table according to the dir
            if (dir == EAST)
                transform_matrix.Turn(90);
            if (dir == SOUTH)
                transform_matrix.Turn(180);
            if (dir == WEST)
                transform_matrix.Turn(270);
            animate(this, Dm.named("transform", transform_matrix), Dm.named("time", 0));
        }
        else
        {
            icon = flipped_table_icon;
            icon_state = Dm.str(base_icon_state);
        }

        update_appearance();
        QUEUE_SMOOTH_NEIGHBORS(this);

        is_flipped = TRUE;
    }

    // tg: code/game/objects/structures/tables_racks.dm:206
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(is_flipped))
            Dm.append(ref dot, span_notice(I18n.T("It's been flipped on its side!")));
        Dm.append(ref dot, deconstruction_hints(user));
        return dot;
    }

    // tg: code/game/objects/structures/tables_racks.dm:212
    public virtual object deconstruction_hints(mob user = null)
    {
        return span_notice(I18n.T("The top is <b>screwed</b> on, but the main <b>bolts</b> are also visible."));
    }

    // DEVIATION(DV420): what the flip throws off the table. tg's throw takes its first step at once (throw_at's TT.tick()), before the
    // table counts as flipped, so its flipped top never stops it; the port's physics flight (DV416) crosses the edge a moment later, when
    // the table is already flipped, so the flipped top lets the things its own flip threw leave over it while they fly.
    private readonly HashSet<atom_movable> flip_thrown = new();

    // tg: code/game/objects/structures/tables_racks.dm:215
    [SignalHandler]
    public virtual object on_exit(datum source = null, atom_movable leaving = null, object direction = null)
    {
        if (!Dm.truthy(is_flipped))
            return null;

        if (flip_thrown.Remove(leaving) && leaving.throwing != null) // DEVIATION(DV420): see flip_thrown
            return null;

        if (Dm.truthy(Dm.bitand(leaving.movement_type, PHASING)))
            return null;

        if (leaving == this)
            return null;
        if (Dm.truthy(is_transparent)) //Glass table, jolly ranchers pass
        {
            if (leaving is atom_movable && Dm.truthy(Dm.bitand(leaving.pass_flags, PASSGLASS)))
                return null;
        }

        if (leaving is obj_projectile)
            return null;

        if (Dm.eq(direction, dir))
        {
            Waiting.NoSleep(leaving.Bump(this));
            return COMPONENT_ATOM_BLOCK_EXIT;
        }
        return null;
    }

    // tg: code/game/objects/structures/tables_racks.dm:269
    public override async DmTask<object> attack_paw(mob user = null, DmList modifiers = null)
    {
        return await attack_hand(user, modifiers);
    }

    // tg: code/game/objects/structures/tables_racks.dm:272
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(is_flipped))
            return null;
        return await base.attack_hand(user, modifiers, __extra);
    }

    // tg: code/game/objects/structures/tables_racks.dm:302  (gaps L1)
    public virtual object is_able_to_throw(obj_structure table = null, atom_movable movable_entity = null)
    {
        if (movable_entity == table) //Thing is not the table
            return FALSE;
        if (Dm.truthy(movable_entity.anchored)) //Thing isn't anchored
            return FALSE;
        if (!isliving(movable_entity) && !isobj(movable_entity)) //Thing isn't an obj or mob
            return FALSE;
        if (Dm.truthy(movable_entity.throwing) || Dm.truthy(Dm.bitand(movable_entity.movement_type, Dm.bitor(FLOATING, FLYING))) || HAS_TRAIT(movable_entity, TRAIT_IGNORE_ELEVATION)) //Thing isn't flying/floating
            return FALSE;

        return TRUE;
    }

    // tg: code/game/objects/structures/tables_racks.dm:276  (gaps L1: flipping the table with an empty hand's right click)
    // DEVIATION(DV503): the owner (2026-11-17): the right click no longer flips; the flip is the radial's Flip (RadialExtras), radial_flip below.
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        return await base.attack_hand_secondary(user, modifiers);
    }

    /// <summary>DV503: the radial's Flip, tg's flip of <c>attack_hand_secondary</c> (tables_racks.dm:276) after its parent's call.</summary>
    public async DmTask radial_flip(mob user)
    {
        if (user is not mob_living || !Dm.truthy(await user.can_interact_with(this)))
            return;

        if (!Dm.truthy(can_flip))
            return;

        string interaction_key = Dm.text("table_flip_[]", REF(this));
        if (!Dm.truthy(is_flipped))
        {
            if (!Dm.truthy(LAZYACCESS(user.do_afters, interaction_key))) // To avoid balloon alert spam
                user.balloon_alert_to_viewers(I18n.T("flipping table..."));
            if (Dm.truthy(await do_after(user, Dm.num(max_integrity) * 0.25f, this, interaction_key: interaction_key)))
                await flip_table(get_dir(user, this));
            return;
        }

        if (!Dm.truthy(LAZYACCESS(user.do_afters, interaction_key))) // To avoid balloon alert spam
            user.balloon_alert_to_viewers(I18n.T("flipping table upright..."));
        if (Dm.truthy(await do_after(user, Dm.num(max_integrity) * 0.25f, this, interaction_key: interaction_key)))
            await unflip_table();
    }

    // This extends base item interaction because tables default to blocking 99% of interactions
    // tg: code/game/objects/structures/tables_racks.dm:343
    public override async DmTask<object> base_item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null)
    {
        object dot = await base.base_item_interaction(user, tool, modifiers);
        if (Dm.truthy(dot))
            return dot;

        if (Dm.truthy(is_flipped))
            return dot;

        if (tool is obj_item_toy_cards_deck dealer_deck)
            dot = await deck_act(user, dealer_deck, modifiers, Dm.not(Dm.not(LAZYACCESS(modifiers, RIGHT_CLICK))));
        if (tool is obj_item_storage_bag_tray used_tray)
            dot = await tray_act(user, used_tray);

        // Continue to placing if we don't do anything else
        if (Dm.truthy(dot))
            return dot;

        if (!Dm.truthy(user.combat_mode) || Dm.truthy(Dm.bitand(tool.item_flags, NOBLUDGEON)))
            return await table_place_act(user, tool, modifiers);

        return NONE;
    }

    // tg: code/game/objects/structures/tables_racks.dm:365
    public virtual async DmTask<object> tray_act(mob_living user = null, obj_item_storage_bag_tray used_tray = null)
    {
        if (Dm.le(length(used_tray.contents), 0))
            return NONE; // If the tray IS empty, continue on (tray will be placed on the table like other items)

        foreach (obj_item thing in Dm.each<obj_item>(used_tray.contents))
            AfterPutItemOnTable(thing, user);
        await used_tray.atom_storage.remove_all(drop_location() as atom);
        user.visible_message(span_notice(I18n.T("[] empties [] on [].", user, used_tray, this)));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/tables_racks.dm:375
    public virtual async DmTask<object> deck_act(mob_living user = null, obj_item_toy_cards_deck dealer_deck = null, DmList modifiers = null, object flip = null)
    {
        Dm.NotTranslated("code/game/objects/structures/tables_racks.dm:375", "deck_act: dealing cards (M10+)");
        return default;
    }

    // Where putting things on tables is handled.
    // tg: code/game/objects/structures/tables_racks.dm:387
    public virtual async DmTask<object> table_place_act(mob_living user = null, obj_item tool = null, DmList modifiers = null)
    {
        if (Dm.truthy(Dm.bitand(tool.item_flags, ABSTRACT)))
            return NONE;

        float x_offset = 0;
        float y_offset = 0;
        // Items are centered by default, but we move them if click ICON_X and ICON_Y are available
        if (Dm.truthy(LAZYACCESS(modifiers, ICON_X)) && Dm.truthy(LAZYACCESS(modifiers, ICON_Y)))
        {
            // Clamp it so that the icon never moves more than 16 pixels in either direction (thus leaving the table turf)
            x_offset = Dm.num(clamp(Dm.num(text2num(LAZYACCESS(modifiers, ICON_X))) - 16, -(ICON_SIZE_X * 0.5f), ICON_SIZE_X * 0.5f));
            y_offset = Dm.num(clamp(Dm.num(text2num(LAZYACCESS(modifiers, ICON_Y))) - 16, -(ICON_SIZE_Y * 0.5f), ICON_SIZE_Y * 0.5f));
        }

        if (!Dm.truthy(await user.transfer_item_to_turf(tool, get_turf(this) as turf, x_offset, y_offset, silent: FALSE)))
            return ITEM_INTERACT_BLOCKING;
        AfterPutItemOnTable(tool, user);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/tables_racks.dm:404
    public virtual void AfterPutItemOnTable(obj_item thing = null, mob_living user = null)
    {
        return;
    }
}

// M5 slice 7 (furniture): taking a table apart (the screwdriver's and the wrench's secondary acts, atom_deconstruct), the
// glass table breaking under a mob and shattering, and the context tips of the tools.
// tg: code/game/objects/structures/tables_racks.dm:15
public partial class obj_structure_table : obj_structure
{
    // tg: code/game/objects/structures/tables_racks.dm:186
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        object dot = base.add_context(source, context, held_item, user);

        if (held_item == null)
            return NONE;

        if (istype(held_item, typeof(obj_item_toy_cards_deck)))
        {
            obj_item_toy_cards_deck dealer_deck = (obj_item_toy_cards_deck)held_item;
            if (HAS_TRAIT(dealer_deck, TRAIT_WIELDED))
            {
                context[SCREENTIP_CONTEXT_LMB] = "Deal card";
                context[SCREENTIP_CONTEXT_RMB] = "Deal card faceup";
                dot = CONTEXTUAL_SCREENTIP_SET;
            }
        }

        if (Dm.truthy(deconstruction_ready))
        {
            if (Dm.eq(held_item.tool_behaviour, TOOL_SCREWDRIVER))
            {
                context[SCREENTIP_CONTEXT_RMB] = "Disassemble";
                dot = CONTEXTUAL_SCREENTIP_SET;
            }
            if (Dm.eq(held_item.tool_behaviour, TOOL_WRENCH))
            {
                context[SCREENTIP_CONTEXT_RMB] = "Deconstruct";
                dot = CONTEXTUAL_SCREENTIP_SET;
            }
        }

        return Dm.truthy(dot) ? dot : NONE;
    }

    // tg: code/game/objects/structures/tables_racks.dm:324
    public override async DmTask<object> screwdriver_act_secondary(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(deconstruction_ready))
            return NONE;
        to_chat(user, span_notice(I18n.T("You start disassembling []...", this)));
        if (Dm.truthy(await tool.use_tool(this, user, 2 * SECONDS, volume: 50)))
            await deconstruct(TRUE);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/tables_racks.dm:332
    public override async DmTask<object> wrench_act_secondary(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(deconstruction_ready))
            return NONE;
        to_chat(user, span_notice(I18n.T("You start deconstructing []...", this)));
        if (Dm.truthy(await tool.use_tool(this, user, 4 * SECONDS, volume: 50)))
        {
            playsound(loc, Dm.resource("sound/items/deconstruct.ogg"), 50, TRUE); // tg: code/game/objects/structures/tables_racks.dm:337
            frame = null;
            await deconstruct(TRUE);
        }
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/tables_racks.dm:407
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        disassembled ??= TRUE;
        turf target_turf = get_turf(this) as turf;
        if (buildstack != null)
            Dm.@new(buildstack, target_turf, buildstackamount);
        else
        {
            foreach (datum_material mat in Dm.each<datum_material>(custom_materials))
                Dm.@new(mat.sheet_type, target_turf, FLOOR(Dm.num(custom_materials[mat]) / SHEET_MATERIAL_AMOUNT, 1));
        }

        if (frame != null)
            Dm.@new(frame, target_turf);
        else
            Dm.@new(framestack, get_turf(this), framestackamount);
    }
}

///Table on wheels
// tg: code/game/objects/structures/tables_racks.dm:448  (gaps ST5)
public partial class obj_structure_table_rolling : obj_structure_table
{
    // tg: code/game/objects/structures/tables_racks.dm:463
    [DmParams("mapload", "frame_used", "stack_used")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_noisy_movement)));
        return dot;
    }

    // tg: code/game/objects/structures/tables_racks.dm:467
    public override object Destroy(float? force = null)
    {
        foreach (object item in Dm.each<object>(attached_items))
            clear_item_reference(item as obj_item);
        LAZYNULL(ref attached_items); // safety
        return base.Destroy(force);
    }

    // tg: code/game/objects/structures/tables_racks.dm:473
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = NONE;
        if (tool is not obj_item_rolling_table_dock rable)
            return dot;

        if (rable.loaded != null)
        {
            to_chat(user, span_warning(I18n.T("You already have \\a [] docked!", rable.loaded)));
            return ITEM_INTERACT_FAILURE;
        }

        if (Dm.locate<mob_living>(loc.get_all_contents()) != null)
        {
            to_chat(user, span_warning(I18n.T("You can't collect \\the [] with that much on top!", this)));
            return ITEM_INTERACT_FAILURE;
        }

        rable.loaded = this;
        await forceMove(rable);
        user.visible_message(span_notice(I18n.T("[] collects \\the [].", user, this)), span_notice(I18n.T("You collect \\the [].", this)));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/tables_racks.dm:491
    public override void AfterPutItemOnTable(obj_item thing = null, mob_living user = null)
    {
        base.AfterPutItemOnTable(thing, user);
        LAZYADD(ref attached_items, thing);
        RegisterSignal(thing, COMSIG_MOVABLE_MOVED, nameof(on_item_moved));
    }

    /// Handles cases where any attached item moves, with or without the table. If we get picked up or anything, unregister the signal so we don't move with the table after removal from the surface.
    // tg: code/game/objects/structures/tables_racks.dm:497
    [SignalHandler]
    public virtual void on_item_moved(datum source = null, atom old_loc = null, object dir = null, object forced = null, DmList old_locs = null, object momentum_change = null)
    {
        atom thing = (atom)source; // let it runtime if it doesn't work because that is mad wack
        if (thing.loc == loc) // if we move with the table, move on
            return;

        clear_item_reference(thing as obj_item);
    }

    /// Handles movement of the table itself, as well as moving along any atoms we have on our surface.
    // tg: code/game/objects/structures/tables_racks.dm:507
    public override async DmTask<object> Moved(atom old_loc = null, object movement_dir = null, object forced = null, DmList old_locs = null, float? momentum_change = null)
    {
        object dot = await base.Moved(old_loc, movement_dir, forced, old_locs, momentum_change);

        if (loc == null) // aw hell naw
            return dot;

        foreach (mob_living living_mob in Dm.each<mob_living>(old_loc.contents)) //Kidnap everyone on top
            await living_mob.forceMove(loc);

        foreach (atom_movable attached_movable in Dm.each<atom_movable>(attached_items))
        {
            if (!Dm.truthy(await attached_movable.Move(loc))) // weird
                clear_item_reference(attached_movable as obj_item); // we check again in on_item_moved() just in case something's wacky tobaccy
        }
        return dot;
    }

    /// Removes the signal and the entrance from the list.
    // tg: code/game/objects/structures/tables_racks.dm:521
    public virtual void clear_item_reference(obj_item thing = null)
    {
        UnregisterSignal(thing, COMSIG_MOVABLE_MOVED);
        LAZYREMOVE(ref attached_items, thing);
    }
}

// tg: code/game/objects/structures/tables_racks.dm:430  (gaps ST5)
public partial class obj_structure_table_greyscale : obj_structure_table
{
    // tg: code/game/objects/structures/tables_racks.dm:438
    public override async DmTask apply_stack_properties(obj_item_stack stack_used = null)
    {
        if (stack_used.material_type == null)
            return;
        await set_custom_materials(Dm.list(Dm.kv(stack_used.material_type, SHEET_MATERIAL_AMOUNT)));
    }

    // tg: code/game/objects/structures/tables_racks.dm:443
    public override async DmTask finalize_material_effects(DmList materials = null)
    {
        await base.finalize_material_effects(materials);
        object english_list = get_material_english_list(materials);
        desc = I18n.T("A square [] of [] on four legs. It can not move.", Dm.length(materials) > 1 ? I18n.T("amalgamation") : I18n.T("piece"), english_list);
    }
}

// Gaps ST5 (D333): Nar'Sie's touch and telekinesis on a table.
public partial class obj_structure_table : obj_structure
{
    // tg: code/game/objects/structures/tables_racks.dm:264
    public override async DmTask<object> narsie_act(params object[] __extra)
    {
        atom A = loc;
        qdel(this);
        Dm.@new<obj_structure_table_wood>(A);
        await DmTask.CompletedTask;
        return null;
    }

    // tg: code/game/objects/structures/tables_racks.dm:314
    public override async DmTask<object> attack_tk(mob user = null)
    {
        await DmTask.CompletedTask;
        return null;
    }
}

// Gaps ST5 (D333): Nar'Sie's touch on the glass and wooden tables.
public partial class obj_structure_table_glass : obj_structure_table
{
    // tg: code/game/objects/structures/tables_racks.dm:600
    public override async DmTask<object> narsie_act(params object[] __extra)
    {
        color = NARSIE_WINDOW_COLOUR;
        await DmTask.CompletedTask;
        return null;
    }
}

public partial class obj_structure_table_wood : obj_structure_table
{
    // tg: code/game/objects/structures/tables_racks.dm:647
    [DmParams("total_override")]
    public override async DmTask<object> narsie_act(params object[] __extra)
    {
        object total_override = __extra.Length > 0 ? __extra[0] : TRUE;
        if (!Dm.truthy(total_override))
            await base.narsie_act(__extra);
        return null;
    }
}

public partial class obj_structure_table_wood_poker : obj_structure_table_wood
{
    // tg: code/game/objects/structures/tables_racks.dm:662
    public override async DmTask<object> narsie_act(params object[] __extra)
    {
        return await base.narsie_act(FALSE);
    }
}

// tg: code/game/objects/structures/tables_racks.dm:554
public partial class obj_structure_table_glass : obj_structure_table
{
    // tg: code/game/objects/structures/tables_racks.dm:546
    [DmParams("mapload", "frame_used", "stack_used")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_connect_loc), Initialize__glass_loc_connections));
        Dm.Deferred("code/game/objects/structures/tables_racks.dm:552", "Initialize: AddElement(/datum/element/give_turf_traits, string_list(list(TRAIT_AI_AVOID_TURF))): the AI's pathing (M10+)");
        return dot;
    }

    // tg: code/game/objects/structures/tables_racks.dm:548  var/static/list/loc_connections (a proc-level static, rules 10.6)
    private static ref DmList Initialize__glass_loc_connections => ref TgState.Current.obj_structure_table_glass__Initialize__loc_connections;

    // tg: code/game/objects/structures/tables_racks.dm:554
    [SignalHandler]
    public virtual void on_entered(datum source = null, atom_movable AM = null)
    {
        if (!isliving(AM))
            return;
        // Don't break if they're just flying past
        if (Dm.truthy(AM.throwing))
            _addtimer(Dm.@new<datum_callback>(this, nameof(throw_check), AM), 0.5f * SECONDS, file: "code/game/objects/structures/tables_racks.dm", line: 561);
        else
            Waiting.NoSleep(check_break(AM as mob_living));
    }

    // tg: code/game/objects/structures/tables_racks.dm:565
    public virtual async DmTask throw_check(mob_living M = null)
    {
        if (M.loc == get_turf(this))
            await check_break(M);
    }

    // tg: code/game/objects/structures/tables_racks.dm:569
    public virtual async DmTask<object> check_break(mob_living M = null)
    {
        if (Dm.truthy(is_flipped))
            return FALSE;
        if (Dm.truthy(M.has_gravity()) && Dm.num(M.mob_size) > MOB_SIZE_SMALL && !Dm.truthy(Dm.bitand(M.movement_type, MOVETYPES_NOT_TOUCHING_GROUND)))
            await table_shatter(M);
        return null;
    }

    // tg: code/game/objects/structures/tables_racks.dm:575
    public virtual async DmTask table_shatter(mob_living victim = null)
    {
        visible_message(span_warning(I18n.T("[] breaks!", this)),
            span_danger(I18n.T("You hear breaking glass.")));

        playsound(loc, SFX_SHATTER, 50, TRUE); // tg: code/game/objects/structures/tables_racks.dm:579

        Dm.@new(frame, loc);

        obj_item_shard shard = Dm.@new(glass_shard_type, loc) as obj_item_shard;
        shard.throw_impact(victim);

        await victim.Paralyze(100);
        qdel(this);
    }

    // tg: code/game/objects/structures/tables_racks.dm:589
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        disassembled ??= TRUE;
        if (Dm.truthy(disassembled))
        {
            await base.atom_deconstruct(disassembled);
            return;
        }
        else
        {
            turf T = get_turf(this) as turf;
            playsound(T, SFX_SHATTER, 50, TRUE); // tg: code/game/objects/structures/tables_racks.dm:595

            Dm.@new(frame, loc);
            Dm.@new(glass_shard_type, loc);
        }
    }
}

// tg: code/game/objects/structures/tables_racks.dm:665
public partial class obj_structure_table_wood_fancy : obj_structure_table_wood
{
    // tg: code/game/objects/structures/tables_racks.dm:677
    [DmParams("mapload", "frame_used", "stack_used")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        // Needs to be set dynamically because table smooth sprites are 32x34,
        // which the editor treats as a two-tile-tall object. The sprites are that
        // size so that the north/south corners look nice - examine the detail on
        // the sprites in the editor to see why.
        return dot;
    }

    // tg: code/game/objects/structures/tables_racks.dm:684
    public override async DmTask apply_stack_properties(obj_item_stack stack_used = null)
    {
        buildstack = stack_used.type;
    }
}

//No specialties, Just a mapping object.
// tg: code/game/objects/structures/tables_racks.dm:651
public partial class obj_structure_table_wood_poker : obj_structure_table_wood
{
    // tg: code/game/objects/structures/tables_racks.dm:659
    public override async DmTask apply_stack_properties(obj_item_stack stack_used = null)
    {
        buildstack = stack_used.type;
    }
}

// tg: code/game/objects/structures/tables_racks.dm:744
public partial class obj_structure_table_reinforced : obj_structure_table
{
    // tg: code/game/objects/structures/tables_racks.dm:778
    public override object deconstruction_hints(mob user = null)
    {
        if (Dm.truthy(deconstruction_ready))
            return span_notice(I18n.T("The top cover has been <i>welded</i> loose and the main frame's <b>bolts</b> are exposed."));
        else
            return span_notice(I18n.T("The top cover is firmly <b>welded</b> on."));
    }

    // tg: code/game/objects/structures/tables_racks.dm:784
    public override async DmTask<object> welder_act_secondary(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(tool.tool_start_check(user, amount: 0)))
        {
            if (Dm.truthy(await attempt_electrocution(user)))
                return ITEM_INTERACT_BLOCKING;

            if (Dm.truthy(deconstruction_ready))
            {
                to_chat(user, span_notice(I18n.T("You start strengthening the reinforced table...")));
                if (Dm.truthy(await tool.use_tool(this, user, 50, volume: 50)))
                {
                    to_chat(user, span_notice(I18n.T("You strengthen the table.")));
                    deconstruction_ready = FALSE;
                    return ITEM_INTERACT_SUCCESS;
                }
            }
            else
            {
                to_chat(user, span_notice(I18n.T("You start weakening the reinforced table...")));
                if (Dm.truthy(await tool.use_tool(this, user, 50, volume: 50)))
                {
                    to_chat(user, span_notice(I18n.T("You weaken the table.")));
                    deconstruction_ready = TRUE;
                    return ITEM_INTERACT_SUCCESS;
                }
            }
        }
        return ITEM_INTERACT_BLOCKING;
    }

    // tg: code/game/objects/structures/tables_racks.dm:803
    public override async DmTask<object> item_interaction_secondary(mob_living user = null, obj_item tool = null, DmList modifiers = null)
    {
        if (Dm.eq(tool.tool_behaviour, TOOL_WELDER))
            return NONE;

        return await base.item_interaction_secondary(user, tool, modifiers);
    }

    // tg: code/game/objects/structures/tables_racks.dm:809
    public override async DmTask<object> screwdriver_act_secondary(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(deconstruction_ready) && Dm.truthy(await attempt_electrocution(user)))
            return ITEM_INTERACT_BLOCKING;
        return await base.screwdriver_act_secondary(user, tool);
    }

    // tg: code/game/objects/structures/tables_racks.dm:814
    public override async DmTask<object> wrench_act_secondary(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(deconstruction_ready) && Dm.truthy(await attempt_electrocution(user)))
            return ITEM_INTERACT_BLOCKING;
        return await base.wrench_act_secondary(user, tool);
    }

    /// Attempts to shock the user, given the table is hooked up and they're within range.
    /// Returns TRUE on successful electrocution, FALSE otherwise.
    // tg: code/game/objects/structures/tables_racks.dm:821
    public virtual async DmTask<object> attempt_electrocution(mob user = null)
    {
        if (!Dm.truthy(anchored)) // If for whatever reason it's not anchored, it can't be shocked either.
            return FALSE;
        if (!in_range(this, user)) // To prevent TK and mech users from getting shocked.
            return FALSE;

        turf our_turf = get_turf(this) as turf;
        if (Dm.truthy(our_turf.overfloor_placed)) // Can't have a floor in the way.
            return FALSE;

        obj_structure_cable cable_node = our_turf.get_cable_node() as obj_structure_cable;
        if (cable_node == null)
            return FALSE;
        if (!Dm.truthy(await electrocute_mob(user as mob_living_carbon, cable_node, this, 1, TRUE)))
            return FALSE;

        do_sparks(3, TRUE, this);
        return TRUE;
    }
}

// tg: code/game/objects/structures/tables_racks.dm:899
public partial class obj_structure_table_optable : obj_structure_table
{
    // tg: code/game/objects/structures/tables_racks.dm:921
    [DmParams("mapload", "frame_used", "stack_used")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        foreach (object direction in Dm.each(GLOB.alldirs))
        {
            computer = Dm.locate<obj_machinery_computer_operating>(get_step(this, Dm.num(direction)));
            if (Dm.truthy(computer))
            {
                computer.table = this;
                break;
            }
        }

        Waiting.NoSleep(AddElement(typeof(datum_element_connect_loc), optable_loc_connections));

        foreach (mob_living_carbon potential_patient in Dm.each<mob_living_carbon>(loc))
            mark_patient(null, potential_patient);
        return dot;
    }

    // tg: code/game/objects/structures/tables_racks.dm:928  var/static/list/loc_connections (a proc-level static, rules 10.6)
    private static ref DmList optable_loc_connections => ref TgState.Current.obj_structure_table_optable__Initialize__loc_connections;

    // tg: code/game/objects/structures/tables_racks.dm:939
    public override object Destroy(float? force = null)
    {
        if (Dm.truthy(computer) && computer.table == this)
            computer.table = null;
        patient = null;
        QDEL_NULL(ref air_tank);
        if (breath_mask?.loc == this)
            qdel(breath_mask);
        breath_mask = null;
        return base.Destroy(force);
    }

    // tg: code/game/objects/structures/tables_racks.dm:949
    public override void buckle_feedback(mob_living being_buckled = null, mob buckler = null)
    {
        if (HAS_TRAIT(being_buckled, TRAIT_RESTRAINED))
        {
            base.buckle_feedback(being_buckled, buckler);
            return;
        }

        if (being_buckled == buckler)
        {
            being_buckled.visible_message(
                span_notice(I18n.T("[] lays down on [].", buckler, this)),
                span_notice(I18n.T("You lay down on [].", this)),
                visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE
            );
        }
        else
        {
            being_buckled.visible_message(
                span_notice(I18n.T("[] lays [] down on [].", buckler, being_buckled, this)),
                span_notice(I18n.T("[] lays you down on [].", buckler, this)),
                visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE
            );
        }
    }

    // tg: code/game/objects/structures/tables_racks.dm:966
    [DmParams("being_unbuckled", "unbuckler")]
    public override void unbuckle_feedback(mob_living being_unbuckled = null, mob unbuckler = null)
    {
        if (HAS_TRAIT(being_unbuckled, TRAIT_RESTRAINED))
        {
            base.unbuckle_feedback(being_unbuckled, unbuckler);
            return;
        }

        if (being_unbuckled == unbuckler)
        {
            being_unbuckled.visible_message(
                span_notice(I18n.T("[] gets up from [].", unbuckler, this)),
                span_notice(I18n.T("You get up from [].", this)),
                visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE
            );
        }
        else
        {
            being_unbuckled.visible_message(
                span_notice(I18n.T("[] pulls [] up from [].", unbuckler, being_unbuckled, this)),
                span_notice(I18n.T("[] pulls you up from [].", unbuckler, this)),
                visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE
            );
        }
    }

    // tg: code/game/objects/structures/tables_racks.dm:983
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        object dot = base.add_context(source, context, held_item, user);
        if (held_item == null)
        {
            if (breath_mask?.loc == this)
            {
                context[SCREENTIP_CONTEXT_RMB] = "Take mask";
                dot = Dm.bitor(dot, CONTEXTUAL_SCREENTIP_SET);
            }
            return dot;
        }

        if (Dm.truthy(breath_mask) && breath_mask != held_item)
        {
            if (Dm.eq(held_item.tool_behaviour, TOOL_SCREWDRIVER))
            {
                context[SCREENTIP_CONTEXT_LMB] = "Detach mask";
                dot = Dm.bitor(dot, CONTEXTUAL_SCREENTIP_SET);
            }
        }
        else if (held_item is obj_item_clothing_mask_breath)
        {
            context[SCREENTIP_CONTEXT_LMB] = "Attach mask";
            dot = Dm.bitor(dot, CONTEXTUAL_SCREENTIP_SET);
        }

        if (Dm.truthy(air_tank))
        {
            if (Dm.eq(held_item.tool_behaviour, TOOL_WRENCH))
            {
                context[SCREENTIP_CONTEXT_LMB] = "Detach tank";
                dot = Dm.bitor(dot, CONTEXTUAL_SCREENTIP_SET);
            }
        }
        else if (held_item is obj_item_tank)
        {
            obj_item_tank as_tank = (obj_item_tank)held_item;
            if (Dm.truthy(as_tank.tank_holder_icon_state))
            {
                context[SCREENTIP_CONTEXT_LMB] = "Attach tank";
                dot = Dm.bitor(dot, CONTEXTUAL_SCREENTIP_SET);
            }
        }
        return dot;
    }

    // tg: code/game/objects/structures/tables_racks.dm:1009
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        await base.atom_deconstruct(disassembled);
        atom drop_loc = drop_location() as atom;
        if (!Dm.truthy(drop_loc))
            return;

        if (Dm.truthy(air_tank))
        {
            await air_tank.forceMove(drop_loc);
            air_tank = null;
        }

        if (!Dm.truthy(breath_mask))
            return;
        UnregisterSignal(breath_mask, Dm.list(COMSIG_MOVABLE_MOVED, COMSIG_ITEM_DROPPED));
        if (breath_mask.loc == this)
            await breath_mask.forceMove(drop_loc);
        else if (Dm.truthy(breath_mask.loc))
            UnregisterSignal(breath_mask.loc, COMSIG_MOVABLE_MOVED);
        breath_mask = null;
    }

    // tg: code/game/objects/structures/tables_racks.dm:1028
    public override async DmTask make_climbable()
    {
        await AddElement(typeof(datum_element_elevation), Dm.named("pixel_shift", 12));
    }

    // surgical tools cannot be placed on the op table while a patient is also on it
    // tg: code/game/objects/structures/tables_racks.dm:1032
    public override async DmTask<object> table_place_act(mob_living user = null, obj_item tool = null, DmList modifiers = null)
    {
        if (patient != null && Dm.truthy(Dm.bitand(tool.item_flags, SURGICAL_TOOL)))
        {
            await tool.melee_attack_chain(user, patient, modifiers);
            return ITEM_INTERACT_SUCCESS;
        }

        return await base.table_place_act(user, tool, modifiers);
    }

///Align the mob with the table when buckled.
    // tg: code/game/objects/structures/tables_racks.dm:1040
    [DmParams("buckled")]
    public override async DmTask post_buckle_mob(mob_living buckled = null)
    {
        buckled.add_offsets(type, z_add: 6);
        await buckled.AddComponentFrom(type, typeof(datum_component_free_operation));
    }

    ///Disalign the mob with the table when unbuckled.
    // tg: code/game/objects/structures/tables_racks.dm:1045
    [DmParams("buckled")]
    public override DmTask post_unbuckle_mob(mob_living buckled = null)
    {
        buckled.remove_offsets(type);
        buckled.RemoveComponentSource(type, typeof(datum_component_free_operation));
        return DmTask.CompletedTask;
    }

    /// Any mob that enters our tile will be marked as a potential patient. They will be turned into a patient if they lie down.
    // tg: code/game/objects/structures/tables_racks.dm:1050
    [SignalHandler]
    public virtual void mark_patient(datum source = null, mob_living potential_patient = null)
    {
        if (potential_patient is not mob_living)
            return;
        RegisterSignal(potential_patient, COMSIG_LIVING_SET_BODY_POSITION, nameof(recheck_patient), TRUE);
        recheck_patient(potential_patient as mob_living_carbon); // In case the mob is already lying down before they entered.
    }

    /// Unmark the potential patient.
    // tg: code/game/objects/structures/tables_racks.dm:1058
    [SignalHandler]
    public virtual void unmark_patient(datum source = null, mob_living potential_patient = null)
    {
        if (potential_patient is not mob_living)
            return;
        if (potential_patient == patient)
            recheck_patient(patient as mob_living_carbon); // Can just set patient to null, but doing the recheck lets us find a replacement patient.
        UnregisterSignal(potential_patient, COMSIG_LIVING_SET_BODY_POSITION);
    }

    /// Someone on our tile just lied down, got up, moved in, or moved out.
    /// potential_patient is the mob that had one of those four things change.
    /// The check is a bit broad so we can find a replacement patient.
    // tg: code/game/objects/structures/tables_racks.dm:1069
    [SignalHandler]
    // shortcut: a non-carbon living (the slot's type is tg's mob/living/carbon) arrives as null and is found by the replacement search below.
    public virtual void recheck_patient(mob_living_carbon potential_patient = null)
    {
        if (Dm.truthy(patient) && patient != potential_patient)
            return;

        if (potential_patient != null && IS_LYING_OR_CANNOT_LIE(potential_patient) && potential_patient.loc == loc)
        {
            Waiting.NoSleep(set_patient(potential_patient));
            return;
        }

        // Find another lying mob as a replacement.
        mob_living found_replacement = null;
        foreach (mob_living replacement_patient in Dm.each<mob_living>(loc))
        {
            if (!IS_LYING_OR_CANNOT_LIE(replacement_patient))
                continue;
            if (iscarbon(found_replacement))
                break;
            else if (isliving(found_replacement) && !iscarbon(replacement_patient))
                continue; // Prefer carbons over non-carbons.
            found_replacement = replacement_patient;
        }

        Waiting.NoSleep(set_patient(found_replacement));
    }

    /// Updates [var/patient] with out new patient, re-registers surgery related signals, updates UI data for operation computers and vital monitors if those connected.
    // tg: code/game/objects/structures/tables_racks.dm:1093
    public virtual async DmTask set_patient(mob_living new_patient = null)
    {
        if (Dm.truthy(patient))
        {
            UnregisterSignal(patient, Dm.list(
                SIGNAL_ADDTRAIT(TRAIT_READY_TO_OPERATE),
                SIGNAL_REMOVETRAIT(TRAIT_READY_TO_OPERATE),
                COMSIG_ATOM_BEING_OPERATED_ON,
                COMSIG_ATOM_SURGERY_FINISHED,
                COMSIG_LIVING_UPDATING_SURGERY_STATE
            ));

            mob_living_carbon breather_patient = patient as mob_living_carbon;
            if (iscarbon(breather_patient) && Dm.truthy(breather_patient.external) && breather_patient.external == air_tank)
                await breather_patient.close_externals();
        }

        if (Dm.truthy(this._listen_lookup?[COMSIG_OPERATING_TABLE_SET_PATIENT]))
            this._SendSignal(COMSIG_OPERATING_TABLE_SET_PATIENT, Dm.list(this, new_patient));
        patient = new_patient;
        update_appearance();
        if (computer != null)
            Waiting.NoSleep(computer.update_static_data_for_all_viewers());
        if (!Dm.truthy(patient))
            return;
        RegisterSignals(patient, Dm.list(
            SIGNAL_ADDTRAIT(TRAIT_READY_TO_OPERATE),
            SIGNAL_REMOVETRAIT(TRAIT_READY_TO_OPERATE),
            COMSIG_ATOM_SURGERY_FINISHED,
            COMSIG_LIVING_UPDATING_SURGERY_STATE
        ), nameof(on_surgery_change));
        RegisterSignal(patient, COMSIG_ATOM_BEING_OPERATED_ON, nameof(get_surgeries));
    }

    // tg: code/game/objects/structures/tables_racks.dm:1121
    [SignalHandler]
    public virtual void on_surgery_change(datum source = null)
    {
        update_appearance();
        computer?.update_static_data_batched();
    }

    // tg: code/game/objects/structures/tables_racks.dm:1126
    [SignalHandler]
    public virtual void get_surgeries(datum source = null, mob_living surgeon = null, DmList operations = null)
    {
        if (computer == null)
            return;

        Dm.combine(ref operations, computer.advanced_surgeries);
    }

    // tg: code/game/objects/structures/tables_racks.dm:1134
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is obj_item_clothing_mask_breath)
        {
            if (Dm.truthy(breath_mask) && breath_mask != tool)
            {
                balloon_alert(user, I18n.T("mask already attached!"));
                return ITEM_INTERACT_BLOCKING;
            }

            if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
                return ITEM_INTERACT_BLOCKING;

            if (breath_mask != tool)
            {
                breath_mask = (obj_item_clothing_mask_breath)tool;
                RegisterSignal(breath_mask, COMSIG_MOVABLE_MOVED, nameof(on_mask_moved));
            }

            balloon_alert(user, I18n.T("mask attached"));
            playsound(this, Dm.resource("sound/machines/click.ogg"), 50, TRUE); // tg: code/game/objects/structures/tables_racks.dm:1148
            update_appearance();
            return ITEM_INTERACT_SUCCESS;
        }

        if (tool is not obj_item_tank)
            return NONE;

        if (Dm.truthy(air_tank))
        {
            balloon_alert(user, I18n.T("tank already attached!"));
            return ITEM_INTERACT_BLOCKING;
        }

        obj_item_tank as_tank = (obj_item_tank)tool;
        if (!Dm.truthy(as_tank.tank_holder_icon_state))
        {
            balloon_alert(user, I18n.T("does not fit!"));
            return ITEM_INTERACT_BLOCKING;
        }

        if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
            return ITEM_INTERACT_BLOCKING;

        air_tank = as_tank;
        balloon_alert(user, I18n.T("tank attached"));
        playsound(this, Dm.resource("sound/machines/click.ogg"), 50, TRUE); // tg: code/game/objects/structures/tables_racks.dm:1169
        update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/tables_racks.dm:1173
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(breath_mask))
            return NONE;

        if (breath_mask.loc != this)
            return ITEM_INTERACT_BLOCKING;

        await breath_mask.forceMove(drop_location() as atom);
        tool.play_tool_sound(this, 50);
        balloon_alert(user, I18n.T("mask detached"));
        UnregisterSignal(breath_mask, Dm.list(COMSIG_MOVABLE_MOVED, COMSIG_ITEM_DROPPED));
        if (Dm.truthy(await breath_mask.IsReachableBy(user)))
            await user.put_in_hands(breath_mask);
        breath_mask = null;
        update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/tables_racks.dm:1190
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(air_tank))
            return NONE;
        balloon_alert(user, I18n.T("detaching the tank..."));
        if (!Dm.truthy(await tool.use_tool(this, user, 3 * SECONDS)))
            return ITEM_INTERACT_BLOCKING;
        await air_tank.forceMove(drop_location() as atom);
        tool.play_tool_sound(this, 50);
        balloon_alert(user, I18n.T("tank detached"));
        if (Dm.truthy(await air_tank.IsReachableBy(user)))
            await user.put_in_hands(air_tank);

        mob_living_carbon carbon_patient = patient as mob_living_carbon;
        if (iscarbon(carbon_patient) && Dm.truthy(carbon_patient?.external) && carbon_patient.external == air_tank)
            await carbon_patient.close_externals();
        air_tank = null;
        update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/tables_racks.dm:1209
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        object dot = await base.attack_hand_secondary(user, modifiers);
        if (Dm.eq(dot, SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN))
            return dot;

        if (Dm.truthy(await detach_mask(user as mob_living)))
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        return dot;
    }

    // tg: code/game/objects/structures/tables_racks.dm:1217
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(air_tank))
        {
            Dm.append(ref dot, span_notice(I18n.T("It has \\a [] secured to it with a couple of [].", air_tank, EXAMINE_HINT(I18n.T("bolts")))));
            if (Dm.truthy(patient))
                Dm.append(ref dot, span_info(I18n.T("You can connect []'s internals to \\the [] by dragging \\the [] onto them.", patient, air_tank, this)));
        }
        else
            Dm.append(ref dot, span_notice(I18n.T("It has an attachment slot for an air tank underneath.")));
        if (Dm.truthy(breath_mask))
        {
            Dm.append(ref dot, span_notice(I18n.T("It has \\a [] attached to its side, the tube secured with a single [].", breath_mask, EXAMINE_HINT(I18n.T("screw")))));
            if (breath_mask.loc == this)
                Dm.append(ref dot, span_info(I18n.T("You can detach the mask by right-clicking \\the [] with an empty hand.", this)));
        }
        else
            Dm.append(ref dot, span_notice(I18n.T("There's a port for a breathing mask tube on its side.")));
        return dot;
    }

    // tg: code/game/objects/structures/tables_racks.dm:1232
    public virtual async DmTask<object> detach_mask(mob_living user = null)
    {
        if (user is not mob_living || !Dm.truthy(await IsReachableBy(user)) || !Dm.truthy(await user.can_interact_with(this)))
            return FALSE;

        if (!Dm.truthy(breath_mask))
        {
            balloon_alert(user, I18n.T("no mask attached!"));
            return TRUE;
        }

        if (!Dm.truthy(await user.put_in_hands(breath_mask)))
        {
            balloon_alert(user, I18n.T("hands busy!"));
            return TRUE;
        }

        to_chat(user, span_notice(I18n.T("You pull out \\the [] from \\the [].", breath_mask, this)));
        update_appearance();
        return TRUE;
    }

    // tg: code/game/objects/structures/tables_racks.dm:1248
    public override async DmTask<object> mouse_drop_dragged(atom over = null, mob user = null, object src_location = null, object over_location = null, object @params = null)
    {
        if (over != patient || user is not mob_living || !Dm.truthy(await IsReachableBy(user)) || !Dm.truthy(await user.can_interact_with(this)))
            return null;

        if (!iscarbon(patient))
        {
            balloon_alert(user, I18n.T("no internals connector!"));
            return null;
        }

        if (!Dm.truthy(air_tank))
        {
            balloon_alert(user, I18n.T("no tank attached!"));
            return null;
        }

        mob_living_carbon carbon_patient = (mob_living_carbon)patient;
        object internals = carbon_patient.can_breathe_internals();
        if (!Dm.truthy(internals))
        {
            balloon_alert(user, I18n.T("no internals connector!"));
            return null;
        }

        user.visible_message(span_notice(I18n.T("[] begins connecting []'s [] to []'s [].", user, this, air_tank, patient, internals)), span_notice(I18n.T("You begin connecting []'s [] to []'s []...", this, air_tank, patient, internals)), ignored_mobs: patient);
        to_chat(patient, span_userdanger(I18n.T("[] begins connecting []'s [] to your []!", user, this, air_tank, internals)));

        if (!Dm.truthy(await do_after(user, 4 * SECONDS, patient)))
            return null;

        if (!Dm.truthy(air_tank) || patient != over || !Dm.truthy(carbon_patient.can_breathe_internals()))
            return null;

        await carbon_patient.open_internals(air_tank, is_external: TRUE);
        to_chat(user, span_notice(I18n.T("You connect []'s [] to []'s [].", this, air_tank, patient, internals)));
        to_chat(patient, span_userdanger(I18n.T("[] connects []'s [] to your []!", user, this, air_tank, internals)));
        return null;
    }

    // tg: code/game/objects/structures/tables_racks.dm:1280
    [SignalHandler]
    public virtual void on_mask_moved(datum source = null, atom oldloc = null, object direction = null)
    {
        if (oldloc != this)
            UnregisterSignal(oldloc, COMSIG_MOVABLE_MOVED);
        if (Dm.truthy(breath_mask.loc) && breath_mask.loc != this)
            RegisterSignal(breath_mask.loc, COMSIG_MOVABLE_MOVED, nameof(check_mask_range));
        check_mask_range();
    }

    // tg: code/game/objects/structures/tables_racks.dm:1288
    public override async DmTask<object> Moved(atom old_loc = null, object movement_dir = null, object forced = null, DmList old_locs = null, float? momentum_change = null)
    {
        object dot = await base.Moved(old_loc, movement_dir, forced, old_locs, momentum_change);
        if (Dm.truthy(breath_mask))
            check_mask_range();
        return dot;
    }

    // tg: code/game/objects/structures/tables_racks.dm:1293
    [SignalHandler]
    public virtual void check_mask_range()
    {
        // Check if the mask is inside of us, or if its being *directly held* by someone and not in their backpack
        if (breath_mask.loc == this || (isturf(breath_mask.loc?.loc) && in_range(breath_mask, this)))
            return;

        if (isliving(loc))
        {
            mob_living user = loc as mob_living;
            to_chat(user, span_warning(I18n.T("[]'s tube overextends and it comes out of your hands!", breath_mask)));
        }
        else
            visible_message(span_notice(I18n.T("[] snaps back into \\the [].", breath_mask, this)));
        snap_mask_back();
    }

    // tg: code/game/objects/structures/tables_racks.dm:1307
    [SignalHandler]
    public virtual void snap_mask_back()
    {
        if (ismob(breath_mask.loc))
        {
            mob as_mob = breath_mask.loc as mob;
            Waiting.NoSleep(as_mob.temporarilyRemoveItemFromInventory(breath_mask, force: TRUE));
        }
        Waiting.NoSleep(breath_mask.forceMove(this));
        update_appearance();
    }

    // tg: code/game/objects/structures/tables_racks.dm:1315
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (Dm.truthy(air_tank))
            Dm.append(ref dot, mutable_appearance(icon, air_tank.tank_holder_icon_state));
        if (breath_mask?.loc == this)
            Dm.append(ref dot, mutable_appearance(icon, Dm.text("mask_[]", breath_mask.icon_state)));
        if (!Dm.truthy(patient) || !HAS_TRAIT(patient, TRAIT_READY_TO_OPERATE))
            return dot;
        Dm.append(ref dot, mutable_appearance(icon, Dm.text("[]_[]linked", icon_state, Dm.truthy(computer) ? "" : "un")));
        // DEVIATION(DV12): if (computer) . += emissive_appearance(icon, "[icon_state]_linked", src, alpha = 175), the glow on the emissive plane, is not created
        return dot;
    }
}

/*
 * Racks
 */
// M5 slice 7 (furniture): the rack (placing items on it, the kick, its sounds, the wrench's secondary act into rack parts)
// and the rack parts (wrenched into a sheet, assembled into a rack in hand). Generated live-partial stubs: the rack's
// examine (M9). The var blocks are generated.
// tg: code/game/objects/structures/tables_racks.dm:1330
public partial class obj_structure_rack : obj_structure
{
    // tg: code/game/objects/structures/tables_racks.dm:1349
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_climbable)));
        Dm.Deferred("code/game/objects/structures/tables_racks.dm:1352", "Initialize: AddElement(/datum/element/elevation, pixel_shift = 12): the raised position of what stands on it: the client draws a standing mob on the structure's top instead (D239, DV154)");
        register_context();
        ADD_TRAIT(this, TRAIT_COMBAT_MODE_SKIP_INTERACTION, INNATE_TRAIT);
        return dot;
    }

    // tg: code/game/objects/structures/tables_racks.dm:1356
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        if (held_item == null)
            return NONE;

        if (Dm.eq(held_item.tool_behaviour, TOOL_WRENCH))
        {
            context[SCREENTIP_CONTEXT_RMB] = "Deconstruct";
            return CONTEXTUAL_SCREENTIP_SET;
        }

        return NONE;
    }

    // tg: code/game/objects/structures/tables_racks.dm:1366
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("It's held together by a couple of <b>bolts</b>.")));
        return dot;
    }

    // tg: code/game/objects/structures/tables_racks.dm:1370
    public override async DmTask<object> CanAllowThrough(atom_movable mover = null, object border_dir = null)
    {
        object dot = await base.CanAllowThrough(mover, border_dir);
        if (Dm.truthy(dot))
            return dot;
        if (mover is atom_movable && Dm.truthy(Dm.bitand(mover.pass_flags, PASSTABLE)))
            return TRUE;
        return dot;
    }

    // tg: code/game/objects/structures/tables_racks.dm:1377
    public override async DmTask<object> wrench_act_secondary(mob_living user = null, obj_item tool = null)
    {
        tool.play_tool_sound(this);
        await deconstruct(TRUE);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/tables_racks.dm:1382
    public override async DmTask<object> base_item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null)
    {
        object dot = await base.base_item_interaction(user, tool, modifiers);
        if (Dm.truthy(dot))
            return dot;
        if (Dm.truthy(Dm.bitand(tool.item_flags, ABSTRACT)) || (Dm.truthy(user.combat_mode) && !Dm.truthy(Dm.bitand(tool.item_flags, NOBLUDGEON))))
            return NONE;
        if (Dm.truthy(await user.transfer_item_to_turf(tool, get_turf(this) as turf, silent: FALSE)))
            return ITEM_INTERACT_SUCCESS;
        return ITEM_INTERACT_BLOCKING;
    }

    // tg: code/game/objects/structures/tables_racks.dm:1392
    public override async DmTask<object> attack_paw(mob user = null, DmList modifiers = null)
    {
        await attack_hand(user, modifiers);
        return null;
    }

    // tg: code/game/objects/structures/tables_racks.dm:1395
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        mob_living living_user = user as mob_living;
        if (!Dm.truthy(living_user.combat_mode) || Dm.eq(living_user.body_position, LYING_DOWN) || Dm.num(living_user.usable_legs) < 2)
            return dot;
        living_user.changeNext_move(CLICK_CD_MELEE);
        await living_user.do_attack_animation(this, ATTACK_EFFECT_KICK);
        living_user.visible_message(span_danger(I18n.T("[] kicks [].", living_user, this)), null, null, COMBAT_MESSAGE_RANGE);
        await take_damage(rand(4, 8), BRUTE, MELEE, 1);
        return dot;
    }

    // tg: code/game/objects/structures/tables_racks.dm:1405
    public override object play_attack_sound(object damage_amount = null, string damage_type = null, object damage_flag = null, params object[] __extra)
    {
        damage_type ??= BRUTE;
        object sw__1 = damage_type;
        if (Dm.eq(sw__1, BRUTE))
        {
            if (Dm.truthy(damage_amount))
                playsound(loc, Dm.resource("sound/items/dodgeball.ogg"), 80, TRUE); // tg: code/game/objects/structures/tables_racks.dm:1409
            else
                playsound(loc, Dm.resource("sound/items/weapons/tap.ogg"), 50, TRUE); // tg: code/game/objects/structures/tables_racks.dm:1411
        }
        else if (Dm.eq(sw__1, BURN))
            playsound(loc, Dm.resource("sound/items/tools/welder.ogg"), 40, TRUE); // tg: code/game/objects/structures/tables_racks.dm:1413
        return null;
    }

    /*
     * Rack destruction
     */
    // tg: code/game/objects/structures/tables_racks.dm:1419
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        disassembled ??= TRUE;
        set_density(FALSE);
        obj_item_rack_parts newparts = Dm.@new<obj_item_rack_parts>(loc);
        // tg: code/game/objects/structures/tables_racks.dm:1422
        transfer_fingerprints_to(newparts);
        _ = newparts;
    }
}

/*
 * Rack Parts
 */
// tg: code/game/objects/structures/tables_racks.dm:1429
public partial class obj_item_rack_parts : obj_item
{
    // tg: code/game/objects/structures/tables_racks.dm:1440
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        register_context();
        return dot;
    }

    // tg: code/game/objects/structures/tables_racks.dm:1444
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        if (held_item == null)
            return NONE;

        if (held_item == this)
        {
            context[SCREENTIP_CONTEXT_LMB] = "Construct Rack";
            return CONTEXTUAL_SCREENTIP_SET;
        }

        if (Dm.eq(held_item.tool_behaviour, TOOL_WRENCH))
        {
            context[SCREENTIP_CONTEXT_LMB] = "Deconstruct";
            return CONTEXTUAL_SCREENTIP_SET;
        }

        return NONE;
    }

    // tg: code/game/objects/structures/tables_racks.dm:1459
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        tool.play_tool_sound(this);
        await deconstruct(TRUE);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/tables_racks.dm:1464
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        disassembled ??= TRUE;
        Dm.@new<obj_item_stack_sheet_iron>(drop_location());
    }

    // tg: code/game/objects/structures/tables_racks.dm:1467
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(building))
            return null;
        building = TRUE;
        to_chat(user, span_notice(I18n.T("You start constructing a rack...")));
        if (Dm.truthy(await do_after(user, 5 * SECONDS, target: user)))
        {
            if (!Dm.truthy(await user.temporarilyRemoveItemFromInventory(this)))
                return null;
            obj_structure_rack R = Dm.@new<obj_structure_rack>(get_turf(this));
            user.visible_message(span_notice(I18n.T("[] assembles \\a [].", user, R)), span_notice(I18n.T("You assemble \\a [].", R)));
            // tg: code/game/objects/structures/tables_racks.dm:1476
            R.add_fingerprint(user);
            qdel(this);
        }
        building = FALSE;
        return null;
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/structures/tables_racks.dm:76  var/static/list/loc_connections = list(COMSIG_ATOM_EXIT = PROC_REF(on_exit))
    internal DmList obj_structure_table__Initialize__loc_connections = Dm.list(
        Dm.kv(COMSIG_ATOM_EXIT, nameof(obj_structure_table.on_exit)));

    // tg: code/game/objects/structures/tables_racks.dm:548  var/static/list/loc_connections = list(COMSIG_ATOM_ENTERED = PROC_REF(on_entered))
    internal DmList obj_structure_table_glass__Initialize__loc_connections = Dm.list(
        Dm.kv(COMSIG_ATOM_ENTERED, nameof(obj_structure_table_glass.on_entered)));

    // tg: code/game/objects/structures/tables_racks.dm:928  var/static/list/loc_connections = list(COMSIG_ATOM_ENTERED = PROC_REF(mark_patient), COMSIG_ATOM_AFTER_SUCCESSFUL_INITIALIZED_ON = PROC_REF(mark_patient), COMSIG_ATOM_EXITED = PROC_REF(unmark_patient))
    internal DmList obj_structure_table_optable__Initialize__loc_connections = Dm.list(
        Dm.kv(COMSIG_ATOM_ENTERED, nameof(obj_structure_table_optable.mark_patient)),
        Dm.kv(COMSIG_ATOM_AFTER_SUCCESSFUL_INITIALIZED_ON, nameof(obj_structure_table_optable.mark_patient)),
        Dm.kv(COMSIG_ATOM_EXITED, nameof(obj_structure_table_optable.unmark_patient)));
}

// tg: code/game/objects/structures/tables_racks.dm:420 (interleaved types, rules 1.9; E13: the RCD)
public partial class obj_structure_table : obj_structure
{
    // tg: code/game/objects/structures/tables_racks.dm:420
    public override object rcd_vals(mob user = null, obj_item_construction_rcd the_rcd = null, params object[] __extra)
    {
        if (Dm.eq(the_rcd.mode, RCD_DECONSTRUCT))
            return Dm.list(Dm.kv("delay", 2.4f * SECONDS), Dm.kv("cost", 16));
        return FALSE;
    }

    // tg: code/game/objects/structures/tables_racks.dm:425
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

// Gaps package C1 (the AI's pathfinding and movement).
public partial class obj_structure_table : obj_structure
{
    // tg: code/game/objects/structures/tables_racks.dm:317
    public override object CanAStarPass(object to_dir = null, datum_can_pass_info pass_info = null)
    {
        if (!Dm.truthy(density))
            return TRUE;
        if (Dm.truthy(Dm.bitand(pass_info.pass_flags, PASSTABLE)))
            return TRUE;
        return FALSE;
    }
}

// tg: code/game/objects/structures/tables_racks.dm:619  (gaps L1: the wood table may break under a slam)
public partial class obj_structure_table_wood : obj_structure_table
{
    // tg: code/game/objects/structures/tables_racks.dm:633
    public override async DmTask after_smash(mob_living smashed_onto = null)
    {
        if (QDELETED(this) || QDELETED(smashed_onto) || prob(66))
            return;
        visible_message(
            span_warning(I18n.T("[] smashes into bits!", this)),
            blind_message: span_hear(I18n.T("You hear the loud cracking of wood being split.")));

        playsound(this, Dm.resource("sound/effects/wounds/crack2.ogg"), 50, TRUE);
        await smashed_onto.Knockdown(10 * SECONDS);
        await smashed_onto.Paralyze(2 * SECONDS);
        await smashed_onto.apply_damage(20, BRUTE);
        await deconstruct(FALSE);
    }
}

// tg: code/game/objects/structures/tables_racks.dm:842  (gaps L1: its only proc, so the bronze table is live)
public partial class obj_structure_table_bronze : obj_structure_table
{
    // tg: code/game/objects/structures/tables_racks.dm:852
    public override async DmTask after_smash(mob_living smashed_onto = null)
    {
        playsound(this, Dm.resource("sound/effects/magic/clockwork/fellowship_armory.ogg"), 50, TRUE);
    }
}
