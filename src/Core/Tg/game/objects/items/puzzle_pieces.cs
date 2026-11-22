// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/puzzle_pieces.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. Syndicate N1b (D316): the keycards and the puzzle doors with the keycard door (puzzle_pieces.dm:1-145: the syndicate
// base's laboratories, fridge and custodial closet, modules/mapfluff/centcom/nuke_ops.dm). Gaps OD1 (D333): the rest, the ruins'
// puzzles (the hologrid plate and its holobox, the light mechanism, the buttons, keycard pads and password and PIN panels, the shield
// and one-way gates, the pod door relay, the dotted board, crayon and note clues). The var blocks are generated (D111); three ids are
// hand-declared (a puzzle id is map text the converter's map sources do not read: the ruins are not loaded maps yet).

// tg: code/game/objects/items/puzzle_pieces.dm:6
public partial class obj_item_keycard : obj_item
{
    // tg: code/game/objects/items/puzzle_pieces.dm:19
    public override object get_save_vars()
    {
        object dot = base.get_save_vars();
        Dm.append(ref dot, nameof(puzzle_id));
        return dot;
    }
}

// tg: code/game/objects/items/puzzle_pieces.dm:39
public partial class obj_machinery_door_puzzle : obj_machinery_door
{
    // tg: code/game/objects/items/puzzle_pieces.dm:59
    public override object get_save_vars()
    {
        object dot = base.get_save_vars();
        Dm.append(ref dot, nameof(puzzle_id));
        return dot;
    }

    // tg: code/game/objects/items/puzzle_pieces.dm:73
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (puzzle_id != null && Dm.truthy(uses_queuelinks))
            SSqueuelinks.add_to_queue(this, puzzle_id);
        Waiting.NoSleep(AddElement(typeof(datum_element_empprotection), EMP_PROTECT_ALL));
        return dot;
    }

    // tg: code/game/objects/items/puzzle_pieces.dm:79
    public override void MatchedLinks(object id = null, DmList partners = null)
    {
        foreach (object partner in Dm.each(partners))
            RegisterSignal(partner as datum, COMSIG_PUZZLE_COMPLETED, nameof(try_signal));
    }

    // tg: code/game/objects/items/puzzle_pieces.dm:83
    [SignalHandler]
    public virtual void try_signal(datum source = null, object try_id = null)
    {
        puzzle_id = null; //honestly these cant be closed anyway and im not fucking around with door code anymore
        Waiting.Detach(try_puzzle_open(null));
    }

    // tg: code/game/objects/items/puzzle_pieces.dm:89
    public override object animation_length(object animation = null)
    {
        switch (Dm.str(animation))
        {
            case DOOR_OPENING_ANIMATION:
                return 1.0f * SECONDS;
        }
        return null;
    }

    // tg: code/game/objects/items/puzzle_pieces.dm:94
    public override object animation_segment_delay(object animation = null)
    {
        switch (Dm.str(animation))
        {
            case DOOR_OPENING_PASSABLE:
                return 0.8f * SECONDS;
            case DOOR_OPENING_FINISHED:
                return 1.0f * SECONDS;
        }
        return null;
    }

    // tg: code/game/objects/items/puzzle_pieces.dm:101
    public override object Bumped(atom_movable AM = null)
    {
        return !Dm.truthy(density) && Dm.truthy(base.Bumped(AM)) ? TRUE : FALSE;
    }

    // tg: code/game/objects/items/puzzle_pieces.dm:104
    public override object ex_act(object severity = null, object target = null, params object[] __extra)
    {
        return FALSE;
    }

    // tg: code/game/objects/items/puzzle_pieces.dm:107
    public override async DmTask<object> try_to_activate_door(mob user = null, float? access_bypass = null, float? bumped = null)
    {
        add_fingerprint(user);
        if (Dm.truthy(operating))
            return null;
        return null;
    }

    // tg: code/game/objects/items/puzzle_pieces.dm:112
    public virtual async DmTask<object> try_puzzle_open(object try_id = null)
    {
        if (Dm.truthy(puzzle_id) && !Dm.eq(puzzle_id, try_id))
            return FALSE;
        if (!Dm.truthy(density))
        {
            visible_message(span_warning(I18n.T("The door can't seem to be closed.")));
            return TRUE;
        }
        if (Dm.truthy(open_message))
            visible_message(span_notice(open_message));
        await open();
        return TRUE;
    }
}

// tg: code/game/objects/items/puzzle_pieces.dm:123
public partial class obj_machinery_door_puzzle_keycard : obj_machinery_door_puzzle
{
    // tg: code/game/objects/items/puzzle_pieces.dm:127
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is not obj_item_keycard keycard)
            return NONE;
        if (!Dm.truthy(await try_puzzle_open(keycard.puzzle_id)))
        {
            to_chat(user, span_notice(I18n.T("[] buzzes. This must not be the right key.", this)));
            return ITEM_INTERACT_BLOCKING;
        }
        return ITEM_INTERACT_SUCCESS;
    }
}

//Uses the pressure_plate settings for a pretty basic custom pattern that waits for a specific item to trigger. Easy enough to retool for mapping purposes or subtypes.
// tg: code/game/objects/items/puzzle_pieces.dm:161
public partial class obj_item_pressure_plate_hologrid : obj_item_pressure_plate
{
    // tg: code/game/objects/items/puzzle_pieces.dm:181
    public override object get_save_vars()
    {
        object dot = base.get_save_vars();
        Dm.append(ref dot, nameof(reward));
        return dot;
    }

    // tg: code/game/objects/items/puzzle_pieces.dm:184
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(undertile_pressureplate))
            // DEVIATION(DV529): tg passes tile_overlay = tile_overlay too (an /image has no ref() in the port, as the pressure plate's)
            Waiting.NoSleep(AddElement(typeof(datum_element_undertile), Dm.named("use_anchor", FALSE))); //we remove use_anchor here, so it ALWAYS stays anchored
        return dot;
    }

    // tg: code/game/objects/items/puzzle_pieces.dm:189
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(claimed))
            Dm.append(ref dot, span_notice(I18n.T("This one appears to be spent already.")));
        return dot;
    }

    // tg: code/game/objects/items/puzzle_pieces.dm:194
    public override DmTask trigger()
    {
        if (!Dm.truthy(claimed))
            Dm.@new(reward, loc);
        flick("lasergrid_a", this);
        icon_state = "lasergrid_full";
        claimed = TRUE;
        return DmTask.CompletedTask;
    }

    // tg: code/game/objects/items/puzzle_pieces.dm:201
    [SignalHandler]
    public override void on_entered(datum source = null, atom_movable AM = null)
    {
        base.on_entered(source, AM);
        if (Dm.truthy(trigger_item) && istype(AM, specific_item) && !Dm.truthy(claimed))
        {
            Waiting.NoSleep(AM.set_anchored(TRUE));
            flick("laserbox_burn", AM);
            Waiting.Detach(trigger());
            _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), 15 > GC_FILTER_QUEUE ? (object)WEAKREF(AM) : AM), 15, file: "code/game/objects/items/puzzle_pieces.dm", line: 207); // QDEL_IN(AM, 15)
        }
    }
}

//Light puzzle
// tg: code/game/objects/items/puzzle_pieces.dm:211
public partial class obj_structure_light_puzzle : obj_structure
{
    /// Banned combinations of the list in decimal
    // tg: code/game/objects/items/puzzle_pieces.dm:230  var/static/list/banned_combinations
    public static DmList banned_combinations => TgState.Current.obj_structure_light_puzzle__banned_combinations ??= Dm.list(-1, 47, 95, 203, 311, 325, 422, 473, 488, 500, 511);

    // tg: code/game/objects/items/puzzle_pieces.dm:234
    public override object get_save_vars()
    {
        object dot = base.get_save_vars();
        Dm.append(ref dot, Dm.list(nameof(queue_size), nameof(puzzle_id)));
        return dot;
    }

    // tg: code/game/objects/items/puzzle_pieces.dm:247
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        Waiting.NoSleep(AddElement(typeof(datum_element_blocks_explosives)));
        object dot = base.Initialize(mapload, __extra);
        float generated_board = -1;
        while (Dm.truthy(Dm.@in(generated_board, banned_combinations)))
            generated_board = rand(0, 510);
        for (int i = 0; i <= 8; i++)
        {
            float position = Dm.truthy(Dm.bitand(generated_board, Dm.shl(1, i))) ? 1 : 0;
            light_list[i + 1] = position;
        }
        Waiting.NoSleep(update_icon(UPDATE_OVERLAYS));
        if (puzzle_id != null)
            SSqueuelinks.add_to_queue(this, puzzle_id, queue_size);
        return dot;
    }

    // tg: code/game/objects/items/puzzle_pieces.dm:260
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        for (int i = 1; i <= 9; i++)
        {
            if (!Dm.truthy(light_list[i]))
                continue;
            mutable_appearance lit_image = mutable_appearance(Dm.resource("icons/obj/fluff/puzzle_small.dmi"), "light_lit") as mutable_appearance;
            // DEVIATION(DV12): var/mutable_appearance/emissive_image = emissive_appearance('icons/obj/fluff/puzzle_small.dmi', "light_lit", src), at the lit image's offsets
            Dm.Deferred("code/game/objects/items/puzzle_pieces.dm:266", "update_overlays: emissive_appearance of a lit light (DEVIATION DV12)");
            lit_image.pixel_w = 8 * ((Dm.truthy(i % 3) ? i % 3 : 3) - 1);
            lit_image.pixel_z = -8 * (ROUND_UP(i / 3f) - 1);
            Dm.append(ref dot, lit_image);
        }
        return dot;
    }

    // tg: code/game/objects/items/puzzle_pieces.dm:274
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(modifiers) || Dm.truthy(powered))
            return await base.attack_hand(user, modifiers, __extra);
        float light_clicked;
        float x_clicked = Dm.num(text2num(modifiers[ICON_X]));
        float y_clicked = Dm.num(text2num(modifiers[ICON_Y]));
        if (x_clicked <= 4 || x_clicked >= 29 || y_clicked <= 4 || y_clicked >= 29)
            return await base.attack_hand(user, modifiers, __extra);
        x_clicked = ROUND_UP((x_clicked - 4) / 8);
        y_clicked = (-(ROUND_UP((y_clicked - 4) / 8) - 4) - 1) * 3;
        light_clicked = x_clicked + y_clicked;
        await switch_light(light_clicked);
        playsound(this, Dm.resource("sound/machines/click.ogg"), 50, TRUE);
        return null;
    }

    // tg: code/game/objects/items/puzzle_pieces.dm:288
    public virtual async DmTask switch_light(float light)
    {
        DmList updating_lights = Dm.list();
        Dm.append(ref updating_lights, light);
        if (light % 3 != 0)
            Dm.append(ref updating_lights, light + 1);
        if (light % 3 != 1)
            Dm.append(ref updating_lights, light - 1);
        if (light + 3 <= 9)
            Dm.append(ref updating_lights, light + 3);
        if (light - 3 > 0)
            Dm.append(ref updating_lights, light - 3);
        foreach (object updating_light in Dm.each(updating_lights))
            light_list[updating_light] = Dm.truthy(light_list[updating_light]) ? 0 : 1;
        await update_icon(UPDATE_OVERLAYS);
        foreach (object checking_light in Dm.each(light_list))
        {
            if (!Dm.truthy(checking_light))
                return;
        }
        visible_message(span_boldnotice(I18n.T("[] becomes fully charged!", this)));
        powered = TRUE;
        SEND_SIGNAL(COMSIG_PUZZLE_COMPLETED, Dm.list(this));
        playsound(this, Dm.resource("sound/machines/synth/synth_yes.ogg"), 100, TRUE);
    }
}

//
// literally just buttons
//

// tg: code/game/objects/items/puzzle_pieces.dm:315
public partial class obj_machinery_puzzle : obj_machinery
{
    // tg: code/game/objects/items/puzzle_pieces.dm:329
    public override object get_save_vars()
    {
        object dot = base.get_save_vars();
        Dm.append(ref dot, Dm.list(nameof(queue_size), nameof(id)));
        return dot;
    }

    // tg: code/game/objects/items/puzzle_pieces.dm:332
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (id != null)
        {
            SSqueuelinks.add_to_queue(this, id, Dm.truthy(late_initialize_pop) ? 0 : queue_size);
            return Dm.truthy(late_initialize_pop) ? INITIALIZE_HINT_LATELOAD : dot;
        }
        return dot;
    }

    // tg: code/game/objects/items/puzzle_pieces.dm:338
    public override async DmTask post_machine_initialize()
    {
        await base.post_machine_initialize();
        if (Dm.truthy(late_initialize_pop) && Dm.truthy(id) && Dm.truthy(SSqueuelinks.queues[id]))
            SSqueuelinks.pop_link(id);
    }

    // tg: code/game/objects/items/puzzle_pieces.dm:343
    public virtual void on_puzzle_complete() //incase someone wants to make this do something else for some reason
    {
        SEND_SIGNAL(COMSIG_PUZZLE_COMPLETED, Dm.list(this));
    }

    // tg: code/game/objects/items/puzzle_pieces.dm:346
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("[][]", base_icon_state, used);
        return await base.update_icon_state(__extra);
    }
}

// tg: code/game/objects/items/puzzle_pieces.dm:351
public partial class obj_machinery_puzzle_button : obj_machinery_puzzle
{
    // tg: code/game/objects/items/puzzle_pieces.dm:357
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        if (Dm.truthy(used) && Dm.truthy(single_use))
            return dot;
        used = single_use;
        await update_icon_state();
        visible_message(span_notice(I18n.T("[] presses a button on [].", user, this)), span_notice(I18n.T("You press a button on [].", this)));
        playsound(this, Dm.resource("sound/machines/terminal/terminal_button07.ogg"), 45, TRUE);
        on_puzzle_complete();
        return dot;
    }
}

// tg: code/game/objects/items/puzzle_pieces.dm:371
public partial class obj_machinery_puzzle_keycardpad : obj_machinery_puzzle
{
    // tg: code/game/objects/items/puzzle_pieces.dm:377
    public override async DmTask<object> attackby(obj_item attacking_item = null, mob user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        object dot = await base.attackby(attacking_item, user, modifiers, attack_modifiers);
        if (attacking_item is not obj_item_keycard key || Dm.truthy(used))
            return dot;
        bool correct_card = Dm.eq(key.puzzle_id, id);
        balloon_alert_to_viewers(correct_card ? I18n.T("correct card swiped") : I18n.T("incorrect card swiped!"));
        playsound(this, Dm.resource("sound/machines/card_slide.ogg"), 45, TRUE);
        if (!correct_card)
            return dot;
        used = TRUE;
        await update_icon_state();
        playsound(this, Dm.resource("sound/machines/beep/beep.ogg"), 45, TRUE);
        on_puzzle_complete();
        return dot;
    }
}

// tg: code/game/objects/items/puzzle_pieces.dm:394
public partial class obj_machinery_puzzle_password : obj_machinery_puzzle
{
    // tg: code/game/objects/items/puzzle_pieces.dm:408
    public override object get_save_vars()
    {
        object dot = base.get_save_vars();
        Dm.append(ref dot, Dm.list(nameof(password), nameof(tgui_text), nameof(tgui_title), nameof(input_max_len_is_pass)));
        return dot;
    }

    // tg: code/game/objects/items/puzzle_pieces.dm:411
    public override async DmTask<object> interact(mob user = null, params object[] __extra)
    {
        if (Dm.truthy(used) && Dm.truthy(single_use))
            return null;
        if (!Dm.truthy(await user.can_perform_action(this, ALLOW_SILICON_REACH)) || !Dm.truthy(await user.can_interact_with(this)))
            return null;
        object pass_input = await tgui_input_text(user, tgui_text, tgui_title, max_length: Dm.truthy(input_max_len_is_pass) ? length(password) : MAX_NAME_LEN);
        if (pass_input == null || !Dm.truthy(await user.can_perform_action(this, ALLOW_SILICON_REACH)) || !Dm.truthy(await user.can_interact_with(this)))
            return null;
        bool correct = Dm.eq(pass_input, password);
        balloon_alert_to_viewers(correct ? I18n.T("correct password") : I18n.T("wrong password!"));
        if (!correct)
        {
            playsound(this, Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 45, TRUE);
            return null;
        }
        used = single_use;
        await update_icon_state();
        playsound(this, Dm.resource("sound/machines/terminal/terminal_button07.ogg"), 45, TRUE);
        on_puzzle_complete();
        return null;
    }
}

// tg: code/game/objects/items/puzzle_pieces.dm:433
public partial class obj_machinery_puzzle_password_pin : obj_machinery_puzzle_password
{
    // tg: code/game/objects/items/puzzle_pieces.dm:441
    public override object get_save_vars()
    {
        object dot = base.get_save_vars();
        Dm.append(ref dot, nameof(pin_length));
        return dot;
    }

    // tg: code/game/objects/items/puzzle_pieces.dm:444
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        for (int iteration = 1; iteration <= pin_length; iteration++)
            password += Dm.text("[]", rand(1, 9));

        DmList possible_colors = Dm.list(
            "white",
            "black",
            "red",
            "green",
            "blue",
            "yellow",
            COLOR_ORANGE, // orange is also not valid
            COLOR_BROWN, // brown is NOT a valid byond color
            "gray",
            "purple");
        for (int digit = 0; digit <= 9; digit++)
            digit_to_color[Dm.text("[]", digit)] = pick_n_take(possible_colors);
        return dot;
    }
}

//
// blockade
//

///blockades destroy themselves if they receive COMSIG_GLOB_PUZZLE_COMPLETED with their ID
// tg: code/game/objects/items/puzzle_pieces.dm:473
public partial class obj_structure_puzzle_blockade : obj_structure
{
    /// if we receive a puzzle signal with this id we get destroyed
    public object id; // tg: code/game/objects/items/puzzle_pieces.dm:483 (hand-declared: text)

    // tg: code/game/objects/items/puzzle_pieces.dm:473 (the generated ctor, dropped with the hand-declared id)
    public obj_structure_puzzle_blockade()
    {
        name = "shield gate";
        desc = "A wall of solid light, likely defending something important. Virtually indestructible, must be a way around, or to disable it.";
        icon = Dm.resource("icons/effects/effects.dmi");
        icon_state = "wave2";
        resistance_flags = 99;
        move_resist = 30000;
        opacity = 0;
        density = 1;
        anchored = 1;
    }

    // tg: code/game/objects/items/puzzle_pieces.dm:485
    public override object get_save_vars()
    {
        object dot = base.get_save_vars();
        Dm.append(ref dot, nameof(id));
        return dot;
    }

    // tg: code/game/objects/items/puzzle_pieces.dm:488
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (id != null)
            SSqueuelinks.add_to_queue(this, id);
        return dot;
    }

    // tg: code/game/objects/items/puzzle_pieces.dm:493
    public override void MatchedLinks(object id = null, DmList partners = null)
    {
        foreach (object partner in Dm.each(partners))
            RegisterSignal(partner as datum, COMSIG_PUZZLE_COMPLETED, nameof(try_signal));
    }

    // tg: code/game/objects/items/puzzle_pieces.dm:497
    [SignalHandler]
    public virtual void try_signal(datum source = null)
    {
        playsound(this, SFX_SPARKS, 100, vary: TRUE, extrarange: SHORT_RANGE_SOUND_EXTRARANGE);
        do_sparks(3, cardinal_only: FALSE, source: this);
        qdel(this);
    }
}

// tg: code/game/objects/items/puzzle_pieces.dm:504
public partial class obj_structure_puzzle_blockade_oneway : obj_structure_puzzle_blockade
{
    // tg: code/game/objects/items/puzzle_pieces.dm:513
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("[][]", base_icon_state, Dm.truthy(density) ? "" : "-off");
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/objects/items/puzzle_pieces.dm:517
    public override async DmTask<object> CanAllowThrough(atom_movable mover = null, object border_dir = null)
    {
        return Dm.truthy(await base.CanAllowThrough(mover, border_dir)) && (Dm.eq(REVERSE_DIR(Dm.num(border_dir)), dir) || get_turf(mover) == get_turf(this)) ? TRUE : FALSE;
    }

    // tg: code/game/objects/items/puzzle_pieces.dm:520
    public override object CanAStarPass(object to_dir = null, datum_can_pass_info pass_info = null)
    {
        return Dm.eq(REVERSE_DIR(Dm.num(to_dir)), dir) ? TRUE : FALSE;
    }

    // tg: code/game/objects/items/puzzle_pieces.dm:523
    [SignalHandler]
    public override void try_signal(datum source = null)
    {
        density = FALSE;
        update_appearance(UPDATE_ICON);
    }
}

// tg: code/game/objects/items/puzzle_pieces.dm:528
public partial class obj_effect_puzzle_poddoor_open : obj_effect
{
    /// door id
    public object id; // tg: code/game/objects/items/puzzle_pieces.dm:537 (hand-declared: a pod door's id, text or a number)

    // tg: code/game/objects/items/puzzle_pieces.dm:528 (the generated ctor, dropped with the hand-declared id)
    public obj_effect_puzzle_poddoor_open()
    {
        name = "puzzle-poddoor relay";
        desc = "Activates pod doors if activated with a puzzle signal.";
        icon = Dm.resource("icons/effects/mapping_helpers.dmi");
        icon_state = "";
        anchored = 1;
        invisibility = 100;
    }

    // tg: code/game/objects/items/puzzle_pieces.dm:539
    public override object get_save_vars()
    {
        object dot = base.get_save_vars();
        Dm.append(ref dot, Dm.list(nameof(queue_id), nameof(id)));
        return dot;
    }

    // tg: code/game/objects/items/puzzle_pieces.dm:542
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (id == null || queue_id == null)
        {
            log_mapping(Dm.text("[] id:[] has no id or door id and has been deleted", this, id));
            return INITIALIZE_HINT_QDEL;
        }

        SSqueuelinks.add_to_queue(this, queue_id);
        return dot;
    }

    // tg: code/game/objects/items/puzzle_pieces.dm:550
    public override void MatchedLinks(object id = null, DmList partners = null)
    {
        foreach (object partner in Dm.each(partners))
            RegisterSignal(partner as datum, COMSIG_PUZZLE_COMPLETED, nameof(try_signal));
    }

    // tg: code/game/objects/items/puzzle_pieces.dm:554
    [SignalHandler]
    public virtual void try_signal(datum source = null)
    {
        object openclose = null;
        foreach (obj_machinery_door_poddoor door in Dm.each<obj_machinery_door_poddoor>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_door_poddoor))))
        {
            if (!Dm.eq(door.id, id))
                continue;
            if (openclose == null)
                openclose = door.density;
            if (Dm.truthy(openclose))
                Waiting.Detach(door.open()); // INVOKE_ASYNC(door, TYPE_PROC_REF(/obj/machinery/door/poddoor, open))
            else
                Waiting.Detach(door.close()); // INVOKE_ASYNC(door, TYPE_PROC_REF(/obj/machinery/door/poddoor, close))
        }
    }
}

///A dotted board that can be used as clue for PIN puzzle machinery
// tg: code/game/objects/items/puzzle_pieces.dm:570
public partial class obj_effect_decal_puzzle_dots : obj_effect_decal
{
    // tg: code/game/objects/items/puzzle_pieces.dm:566-568
    private const int MAX_PUZZLE_DOTS_PER_ROW = 4;
    private const int PUZZLE_DOTS_VERTICAL_OFFSET = 7;
    private const int PUZZLE_DOTS_HORIZONTAL_OFFSET = 7;

    ///The id of the puzzle we're linked to.
    public object id; // tg: code/game/objects/items/puzzle_pieces.dm:579 (hand-declared: text)

    // tg: code/game/objects/items/puzzle_pieces.dm:570 (the generated ctor, dropped with the hand-declared id)
    public obj_effect_decal_puzzle_dots()
    {
        name = "dotted board";
        desc = "A board filled with colored dots. What could this mean?";
        icon = Dm.resource("icons/obj/fluff/puzzle_small.dmi");
        icon_state = "puzzle_dots";
        layer = 2.08f;
        plane = -4;
        resistance_flags = 83;
        flags_1 = 512;
    }

    // tg: code/game/objects/items/puzzle_pieces.dm:581
    public override object get_save_vars()
    {
        object dot = base.get_save_vars();
        Dm.append(ref dot, nameof(id));
        return dot;
    }

    // tg: code/game/objects/items/puzzle_pieces.dm:584
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(id))
            SSqueuelinks.add_to_queue(this, id);
        return dot;
    }

    // tg: code/game/objects/items/puzzle_pieces.dm:589
    public override void MatchedLinks(object id = null, DmList partners = null)
    {
        obj_machinery_puzzle_password_pin pad = Dm.locate<obj_machinery_puzzle_password_pin>(partners);
        DmList pass_digits = splittext(pad.password, "");
        float pass_len = length(pass_digits);
        float extra_rows = CEILING((pass_len / MAX_PUZZLE_DOTS_PER_ROW) - 1, 1);
        if (Dm.truthy(extra_rows))
        {
            pixel_y += round(extra_rows * (PUZZLE_DOTS_VERTICAL_OFFSET * 0.5f));
            for (int i = 1; i <= extra_rows; i++)
            {
                mutable_appearance row = mutable_appearance(icon, icon_state) as mutable_appearance;
                row.pixel_z = -i * PUZZLE_DOTS_VERTICAL_OFFSET;
                add_overlay(row);
            }
        }
        for (int i = 1; i <= pass_len; i++)
        {
            mutable_appearance colored_dot = mutable_appearance(icon, "puzzle_dot_single") as mutable_appearance;
            colored_dot.color = pad.digit_to_color[pass_digits[i]];
            colored_dot.pixel_w = PUZZLE_DOTS_HORIZONTAL_OFFSET * ((i - 1) % MAX_PUZZLE_DOTS_PER_ROW);
            colored_dot.pixel_z -= CEILING((i / (float)MAX_PUZZLE_DOTS_PER_ROW) - 1, 1) * PUZZLE_DOTS_VERTICAL_OFFSET;
            add_overlay(colored_dot);
        }
    }
}

// tg: code/game/objects/items/puzzle_pieces.dm:611
public partial class obj_effect_decal_cleanable_crayon_puzzle : obj_effect_decal_cleanable_crayon
{
    // tg: code/game/objects/items/puzzle_pieces.dm:618
    public override object get_save_vars()
    {
        object dot = base.get_save_vars();
        Dm.append(ref dot, nameof(puzzle_id));
        return dot;
    }

    // tg: code/game/objects/items/puzzle_pieces.dm:621
    [DmParams("mapload", "main", "type", "e_name", "graf_rot", "alt_icon")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        name = "number";
        if (Dm.truthy(puzzle_id))
            SSqueuelinks.add_to_queue(this, puzzle_id);
        return dot;
    }

    // tg: code/game/objects/items/puzzle_pieces.dm:627 (tg returns TRUE or FALSE, which no caller reads; the base proc returns nothing)
    public override void MatchedLinks(object id = null, DmList partners = null)
    {
        obj_machinery_puzzle_password pad = Dm.locate<obj_machinery_puzzle_password>(partners);
        DmList pass_character = splittext(pad.password, "");
        object chosen_character = icon_state;
        if (!Dm.truthy(findtext(chosen_character, GLOB.is_alphanumeric)))
        {
            qdel(this);
            return;
        }
        icon_state = Dm.str(pick(pass_character));
        if (!Dm.truthy(text2num(icon_state)))
        {
            name = "letter";
            desc = "A letter vandalizing the station.";
        }
    }
}

// tg: code/game/objects/items/puzzle_pieces.dm:640
public partial class obj_effect_decal_cleanable_crayon_puzzle_pin : obj_effect_decal_cleanable_crayon_puzzle
{
    // tg: code/game/objects/items/puzzle_pieces.dm:643
    public override void MatchedLinks(object id = null, DmList partners = null)
    {
        base.MatchedLinks(id, partners);
        obj_machinery_puzzle_password_pin pad = Dm.locate<obj_machinery_puzzle_password_pin>(partners);
        Waiting.NoSleep(add_atom_colour(pad.digit_to_color[icon_state], FIXED_COLOUR_PRIORITY));
    }
}

// tg: code/game/objects/items/puzzle_pieces.dm:648
public partial class obj_item_paper_fluff_scrambled_pass : obj_item_paper_fluff
{
    // tg: code/game/objects/items/puzzle_pieces.dm:654
    public override object get_save_vars()
    {
        object dot = base.get_save_vars();
        Dm.append(ref dot, nameof(puzzle_id));
        return dot;
    }

    // tg: code/game/objects/items/puzzle_pieces.dm:657
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(mapload) && Dm.truthy(puzzle_id))
            SSqueuelinks.add_to_queue(this, puzzle_id);
        return dot;
    }

    // tg: code/game/objects/items/puzzle_pieces.dm:662
    public override void MatchedLinks(object id = null, DmList partners = null)
    {
        obj_machinery_puzzle_password pad = Dm.locate<obj_machinery_puzzle_password>(partners);
        string scrambled_text = "";
        DmList pass_characters = splittext(pad.password, "");
        float count = rand(200, 300);
        for (int i = 1; i <= count; i++)
            scrambled_text += Dm.str(pick(pass_characters));
        add_raw_text(scrambled_text);
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/items/puzzle_pieces.dm:230  /obj/structure/light_puzzle/var/static/list/banned_combinations
    internal DmList obj_structure_light_puzzle__banned_combinations;
}
