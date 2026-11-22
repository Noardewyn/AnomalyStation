// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/training_machine.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// gaps ST4 (D333): the file whole. The AURUMILL training machine: a shooting target or a training toolbox attached (drawn on it
// through vis_contents) or a mob buckled on, its TrainingMachine window sets the speed and range and begins or ends the simulation
// (tg's beeps and lines), it then roams point to point within its range on SSfastprocess and hits the carbons next to it with the
// training toolbox; emagged, it pops out a syndicate toolbox that hurts, can't be stopped from its panel and shows the evil overlay;
// broken, it blows up lightly and throws off what it holds. The training toolbox counts the hits on a mob, a target or a dressed
// machine, beeps every nine, and its lap is reset with alt-click. The var block is generated (D111).

/**
 * Machine that runs around wildly so people can practice clickin on things
 *
 * Can have a mob buckled on or a obj/item/target attached. Movement controlled by SSFastProcess,
 * movespeed controlled by cooldown macros. Can attach obj/item/target, obj/item/training_toolbox, and can buckle mobs to this.
 */
// tg: code/game/objects/structures/training_machine.dm:15
public partial class obj_structure_training_machine : obj_structure
{
    // tg: code/game/objects/structures/training_machine.dm:1
    private const float MIN_RANGE = 1;
    // tg: code/game/objects/structures/training_machine.dm:2
    private const float MIN_SPEED = 1;
    // tg: code/game/objects/structures/training_machine.dm:3
    private const float MAX_RANGE = 7;
    // tg: code/game/objects/structures/training_machine.dm:4
    private const float MAX_SPEED = 10;
    // tg: code/game/objects/structures/training_machine.dm:6
    private const float MIN_ATTACK_DELAY = 10;
    // tg: code/game/objects/structures/training_machine.dm:7
    private const float MAX_ATTACK_DELAY = 15;

    /**
     * Called on qdel(), so we don't want a cool explosion to happen
     */
    // tg: code/game/objects/structures/training_machine.dm:44
    public override object Destroy(float? force = null)
    {
        Waiting.NoSleep(remove_attached_item());
        return base.Destroy(force);
    }

    /**
     * Called on a normal destruction, so we have a cool explosion and toss whatever's attached
     */
    // tg: code/game/objects/structures/training_machine.dm:51
    public override async DmTask atom_destruction(object damage_flag = null)
    {
        await remove_attached_item(throwing: TRUE);
        await explosion(this, light_impact_range: 1, flash_range: 2);
        await base.atom_destruction(damage_flag);
    }

    // tg: code/game/objects/structures/training_machine.dm:56
    public override object ui_state(mob user = null)
    {
        return GLOB.physical_state;
    }

    // tg: code/game/objects/structures/training_machine.dm:59
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "TrainingMachine", name);
            await ui.open();
        }
        return null;
    }

    /**
     * Send data to the UI
     *
     * Include's the machine's movement range, speed, and whether or not it's active
     */
    // tg: code/game/objects/structures/training_machine.dm:70
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["range"] = range;
        data["movespeed"] = move_speed;
        data["moving"] = moving;
        return data;
    }

    /**
     * Control the attached variables.
     *
     * Will not respond if moving and emagged, so once you set it to go it can't be stopped!
     */
    // tg: code/game/objects/structures/training_machine.dm:82
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        if (Dm.truthy(moving) && Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
        {
            visible_message(span_warning(I18n.T("The []'s control panel fizzles slightly.", this)));
            return dot;
        }
        switch (action as string)
        {
            case "toggle":
                await toggle();
                dot = TRUE;
                break;
            case "range":
            {
                object range_input = @params["range"];
                range = Dm.num(clamp(range_input, MIN_RANGE, MAX_RANGE));
                dot = TRUE;
                break;
            }
            case "movespeed":
            {
                object range_input = @params["movespeed"];
                move_speed = Dm.num(clamp(range_input, MIN_SPEED, MAX_SPEED));
                dot = TRUE;
                break;
            }
        }
        return dot;
    }

    // tg: code/game/objects/structures/training_machine.dm:102
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        await ui_interact(user);
        return null;
    }

    /**
     * Called when the machien is attacked by something
     *
     * Meant for attaching an item to the machine, should only be a training toolbox or target. If emagged, the
     * machine will gain an auto-attached syndicate toolbox, so in that case we shouldn't be able to swap it out
     */
    // tg: code/game/objects/structures/training_machine.dm:111
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(user.combat_mode))
            return NONE;

        if (!(tool is obj_item_training_toolbox) && !(tool is obj_item_target))
            return NONE;

        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
        {
            to_chat(user, span_warning(I18n.T("The toolbox is somehow stuck on! It won't budge!")));
            return ITEM_INTERACT_BLOCKING;
        }

        await attach_item(tool);
        to_chat(user, span_notice(I18n.T("You attach \\the [] to the training device.", attached_item)));
        playsound(this, SFX_RUSTLE, 50, TRUE);
        return ITEM_INTERACT_SUCCESS;
    }

    /**
     * Attach an item to the machine
     *
     * This proc technically works with any obj. Currently is only used for objects of type item/target and item/training_toolbox
     * Will make the attached item appear visually on the machine
     * Arguments
     * * target - The object to attach
     */
    // tg: code/game/objects/structures/training_machine.dm:135
    public virtual async DmTask attach_item(obj target = null)
    {
        await remove_attached_item();
        attached_item = target;
        await attached_item.forceMove(this);
        attached_item.vis_flags = Dm.num(Dm.bitor(attached_item.vis_flags, Dm.bitor(VIS_INHERIT_ID, VIS_INHERIT_PLANE)));
        Dm.append(vis_contents, attached_item);
        RegisterSignal(attached_item, COMSIG_QDELETING, nameof(on_attached_delete));
        handle_density();
    }

    /**
     * Called when the attached item is deleted.
     *
     * Cleans up behavior for when the attached item is deleted or removed.
     */
    // tg: code/game/objects/structures/training_machine.dm:149
    [SignalHandler]
    public virtual void on_attached_delete()
    {
        UnregisterSignal(attached_item, COMSIG_QDELETING);
        Dm.remove(vis_contents, attached_item);
        attached_item.vis_flags = Dm.num(Dm.bitand(attached_item.vis_flags, Dm.bitnot(Dm.bitor(VIS_INHERIT_ID, VIS_INHERIT_PLANE))));
        attached_item = null;
        handle_density();
    }

    /**
     * Remove the attached item from the machine
     *
     * Called when a user removes the item by hand or by swapping it out with another, when the machine breaks, or
     * when the machine is emagged.
     * Arguments
     * * user - The peson , if any, removing the attached item
     * * throwing - If we should make the item fly off the machine
     */
    // tg: code/game/objects/structures/training_machine.dm:166
    public virtual async DmTask remove_attached_item(mob user = null, float throwing = FALSE)
    {
        if (attached_item == null)
            return;
        if (attached_item is obj_item_storage_toolbox_syndicate)
        {
            UnregisterSignal(attached_item, COMSIG_QDELETING);
            qdel(attached_item);
        }
        else if (user != null)
            Waiting.Detach(user.put_in_hands(attached_item as obj_item));
        else
            await attached_item.forceMove(drop_location() as atom);
        if (Dm.truthy(throwing) && !QDELETED(attached_item)) //Fun little thing where we throw out the old attached item when emagged
        {
            //We do a QDELETED check here because we don't want to throw the syndi toolbox, if it exists
            atom destination = get_edge_target_turf(get_turf(this), pick(GLOB.alldirs)) as atom;
            await attached_item.throw_at(destination, 4, 1);
        }
        on_attached_delete();
    }

    // tg: code/game/objects/structures/training_machine.dm:182
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        if (Dm.truthy(has_buckled_mobs()))
        {
            await user_unbuckle_mob(buckled_mobs[1] as mob_living, user);
            return CLICK_ACTION_SUCCESS;
        }
        if (attached_item == null)
            return NONE;
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
        {
            to_chat(user, span_warning(I18n.T("The toolbox is somehow stuck on! It won't budge!")));
            return CLICK_ACTION_BLOCKING;
        }
        to_chat(user, span_notice(I18n.T("You remove \\the [] from the training device.", attached_item)));
        await remove_attached_item(user);
        playsound(this, SFX_RUSTLE, 50, TRUE);
        return CLICK_ACTION_SUCCESS;
    }

    /**
     * Toggle the machine's movement
     */
    // tg: code/game/objects/structures/training_machine.dm:199
    public virtual async DmTask toggle()
    {
        if (Dm.truthy(moving))
            await stop_moving();
        else
            await start_moving();
    }

    /**
     * Stop the machine's movement
     *
     * Will call STOP_PROCESSING, play a sound, and say an appropriate message
     * Arguments
     * * Message - the message the machine says when stopping
     */
    // tg: code/game/objects/structures/training_machine.dm:212
    public virtual async DmTask stop_moving(object message = null)
    {
        message ??= I18n.T("Ending training simulation.");
        moving = FALSE;
        starting_turf = null;
        await say(message);
        playsound(this, Dm.resource("sound/machines/synth/synth_no.ogg"), 50, FALSE);
        STOP_PROCESSING(SSfastprocess, this);
    }

    /**
     * Start the machine's movement
     *
     * Says a message, plays a sound, then starts processing
     */
    // tg: code/game/objects/structures/training_machine.dm:224
    public virtual async DmTask start_moving()
    {
        moving = TRUE;
        starting_turf = get_turf(this);
        await say(I18n.T("Beginning training simulation."));
        playsound(this, Dm.resource("sound/machines/beep/triple_beep.ogg"), 50, FALSE);
        START_PROCESSING(SSfastprocess, this);
    }

    /**
     * Main movement method for the machine
     *
     * Handles movement using SSFastProcess. Moves randomly, point-to-point, in an area centered around wherever it started.
     * Will only move if the move_cooldown cooldown macro is finished.
     * If it can't find a place to go, it will stop moving.
     */
    // tg: code/game/objects/structures/training_machine.dm:238
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (!(move_cooldown <= world.time)) // COOLDOWN_FINISHED(src, move_cooldown), inline (rules 11.1 (a))
            return null;
        turf current_turf = get_turf(this);
        if (!Dm.truthy(moving) || starting_turf == null || isspaceturf(current_turf))
        {
            Waiting.NoSleep(stop_moving());
            return null;
        }
        if (current_turf == target_position) //We've reached our target turf, now find a new one
            target_position = null;
        if (target_position == null)
        {
            target_position = Waiting.NoSleep(find_target_position()) as turf;
            if (target_position == null)
                Waiting.NoSleep(stop_moving(I18n.T("ERROR! Cannot calculate suitable movement path.")));
        }
        turf nextStep = get_step_towards(this, target_position) as turf;
        if (!Dm.truthy(Waiting.NoSleep(Move(nextStep, get_dir(this, nextStep)))))
            target_position = null; //We couldn't move towards the target turf, so find a new target turf
        Waiting.NoSleep(try_attack());
        move_cooldown = world.time + Math.Max(MAX_SPEED - move_speed, 1); // COOLDOWN_START(src, move_cooldown, ...), inline
        return null;
    }

    /**
     * Find a suitable turf to move towards
     */
    // tg: code/game/objects/structures/training_machine.dm:260
    public virtual async DmTask<object> find_target_position()
    {
        DmList turfs = Dm.list();
        foreach (turf potential_turf in Dm.each<turf>(view(range, starting_turf)))
        {
            if (Dm.truthy(await potential_turf.is_blocked_turf()) || potential_turf == target_position)
                continue;
            Dm.append(turfs, potential_turf);
        }
        if (length(turfs) == 0)
            return null;
        return pick(turfs);
    }

    /**
     * Try to attack a nearby mob
     *
     * Called whenever the machine moves, this will look for mobs adjacent to the machine to attack.
     * Will attack with either a training toolbox (if attached), or a much more deadly syndicate toolbox (if emagged).
     * A cooldown macro (attack_cooldown) ensures it doesn't attack too quickly
     */
    // tg: code/game/objects/structures/training_machine.dm:277
    public virtual async DmTask try_attack()
    {
        if (attached_item == null || attached_item is obj_item_target)
            return;
        if (!(attack_cooldown <= world.time)) // COOLDOWN_FINISHED(src, attack_cooldown), inline
            return;
        DmList targets = null;
        foreach (mob_living_carbon candidate in Dm.each<mob_living_carbon>(oview(1, get_turf(this)))) //Find adjacent target
        {
            if (!IS_UNCONSCIOUS_OR_CRIT(candidate) && Dm.truthy(await candidate.Adjacent(this)))
                LAZYADD(ref targets, candidate);
        }
        mob_living_carbon target = pick(targets) as mob_living_carbon;
        if (target == null)
            return;
        await do_attack_animation(target, null, attached_item as obj_item);
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            await target.apply_damage((attached_item as obj_item)?.force, BRUTE, BODY_ZONE_CHEST, attacking_item: attached_item);
        playsound(this, Dm.resource("sound/items/weapons/smash.ogg"), 15, TRUE);
        attack_cooldown = world.time + Dm.num(rand(MIN_ATTACK_DELAY, MAX_ATTACK_DELAY)); // COOLDOWN_START(src, attack_cooldown, ...), inline
    }

    /**
     * Make sure the machine can't be walked through if something is attached
     */
    // tg: code/game/objects/structures/training_machine.dm:298
    public virtual void handle_density()
    {
        if (length(buckled_mobs) > 0 || attached_item != null)
            set_density(TRUE);
        else
            set_density(FALSE);
    }

    // tg: code/game/objects/structures/training_machine.dm:304
    public override async DmTask<object> buckle_mob(mob_living M = null, float? force = null, float? check_loc = null, float? buckle_mob_flags = null)
    {
        object dot = await base.buckle_mob(M, force ?? FALSE, check_loc ?? TRUE, buckle_mob_flags);
        if (attached_item is obj_item_target)
            return FALSE;
        return dot;
    }

    // tg: code/game/objects/structures/training_machine.dm:309
    public override async DmTask post_buckle_mob(mob_living buckled_mob = null)
    {
        handle_density();
        await base.post_buckle_mob(buckled_mob);
    }

    // tg: code/game/objects/structures/training_machine.dm:313
    public override async DmTask post_unbuckle_mob(mob_living unbuckled_mob = null)
    {
        handle_density();
        await base.post_unbuckle_mob(unbuckled_mob);
    }

    /**
     * Emagging causes a deadly, unremovable syndicate toolbox to be attached to the machine
     */
    // tg: code/game/objects/structures/training_machine.dm:320
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        object dot = await base.emag_act(user, emag_card, __extra);
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return dot;
        obj_flags = Dm.bitor(obj_flags, EMAGGED);
        await remove_attached_item(throwing: TRUE); //Toss out the old attached item!
        await attach_item(Dm.@new<obj_item_storage_toolbox_syndicate>(this));
        to_chat(user, span_warning(I18n.T("You override the training machine's safety protocols, and activate its realistic combat feature. A toolbox pops out of a slot on the top.")));
        playsound(this, Dm.resource("sound/machines/click.ogg"), 50, TRUE);
        add_overlay("evil_trainer");
        return TRUE;
    }

    // tg: code/game/objects/structures/training_machine.dm:332
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList ?? Dm.list();
        object has_buckled_mob = has_buckled_mobs();
        if (Dm.truthy(has_buckled_mob))
            Dm.append(dot, span_notice(I18n.T("<b>Alt-Click to unbuckle \\the []</b>", buckled_mobs[1])));
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            Dm.append(dot, span_warning(I18n.T("It has a dangerous-looking toolbox attached to it, and the control panel is smoking sightly...")));
        else if (!Dm.truthy(has_buckled_mob) && attached_item != null) //Can't removed the syndicate toolbox!
            Dm.append(dot, span_notice(I18n.T("<b>Alt-Click to remove \\the []</b>", attached_item)));
        Dm.append(dot, span_notice(I18n.T("<b>Click to open control interface.</b>")));
        return dot;
    }
}

/**
 * Device that simply counts the number of times you've hit a mob or target with. Looks like a toolbox but isn't.
 *
 * Also has a 'Lap' function for keeping track of hits made at a certain point. Also, looks kinda like his grace for laughs and pranks.
 */
// tg: code/game/objects/structures/training_machine.dm:348
public partial class obj_item_training_toolbox : obj_item
{
    // tg: code/game/objects/structures/training_machine.dm:5
    private const float HITS_TO_KILL = 9;

    // tg: code/game/objects/structures/training_machine.dm:367
    public override async DmTask<object> pre_attack(atom target = null, mob_living user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        object dot = await base.pre_attack(target, user, modifiers, attack_modifiers);
        if (Dm.truthy(dot))
            return dot;
        if (target == user || !Dm.truthy(user.combat_mode))
            return dot;
        if (!Dm.truthy(check_hit(target)))
            return dot;
        user.changeNext_move(CLICK_CD_MELEE);
        await user.do_attack_animation(target);
        return TRUE;
    }

    /**
     * Check if we should increment the hit counter
     *
     * Increments the 'hit' counter if the target we're attacking is a mob, target, or training machine with an attached item.
     * Will beep every 9 hits, as 9 hits usually signifies a KO with a normal toolbox
     * Arguments
     * * target - the atom we're hitting
     */
    // tg: code/game/objects/structures/training_machine.dm:387
    public virtual object check_hit(atom target = null)
    {
        bool target_is_machine = target is obj_structure_training_machine;
        if (!ismob(target) && !(target is obj_item_target) && !target_is_machine)
            return FALSE;
        if (target_is_machine)
        {
            obj_structure_training_machine trainer = (obj_structure_training_machine)target;
            if (trainer.attached_item == null)
                return FALSE;
        }
        total_hits++;
        lap_hits++;
        playsound(this, Dm.resource("sound/items/weapons/smash.ogg"), 50, FALSE);
        if (lap_hits % HITS_TO_KILL == 0)
            playsound(this, Dm.resource("sound/machines/beep/twobeep.ogg"), 25, FALSE);
        return TRUE;
    }

    // tg: code/game/objects/structures/training_machine.dm:402
    public override object throw_impact(atom hit_atom = null, datum_thrownthing throwingdatum = null, params object[] __extra)
    {
        object dot = base.throw_impact(hit_atom, throwingdatum, __extra);
        if (!Dm.truthy(dot))
            check_hit(hit_atom);
        return dot;
    }

    // tg: code/game/objects/structures/training_machine.dm:407
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        to_chat(user, span_notice(I18n.T("You push the 'Lap' button on the toolbox's display.")));
        lap_hits = Dm.num(Dm.initial(this, nameof(lap_hits)));
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/game/objects/structures/training_machine.dm:412
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList ?? Dm.list();
        if (!in_range(this, user) && !isobserver(user))
        {
            Dm.append(dot, span_notice(I18n.T("You can see a display on the back. You'll need to get closer to read it, though.")));
            return dot;
        }
        Dm.append(dot, span_notice(I18n.T("A display on the back reads:")));
        Dm.append(dot, span_notice(I18n.T("Total Hits: <b>[]</b>", total_hits)));
        if (lap_hits != total_hits)
            Dm.append(dot, span_notice(I18n.T("Current Lap: <b>[]</b>", lap_hits)));
        Dm.append(dot, span_notice(I18n.T("<b>Alt-Click to 'Lap' the hit counter.</b>")));
        return dot;
    }
}
