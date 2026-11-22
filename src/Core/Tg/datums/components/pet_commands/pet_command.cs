// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/pet_commands/pet_command.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C2).

/**
 * # Pet Command
 * Set some AI blackboard commands in response to receiving instructions
 * This is abstract and should be extended for actual behaviour
 */
// tg: code/datums/components/pet_commands/pet_command.dm:8
public partial class datum_pet_command : datum
{
    // tg: code/datums/components/pet_commands/pet_command.dm:34
    [DmParams("parent")]
    public override async DmTask New(params object[] __extra)
    {
        mob_living parent = Dm.arg<mob_living>(__extra, 0);
        await base.New(__extra);
        weak_parent = WEAKREF(parent) as datum_weakref;
        return;
    }

    /// Register a new guy we want to listen to
    // tg: code/datums/components/pet_commands/pet_command.dm:39
    public virtual async DmTask add_new_friend(mob_living tamer = null)
    {
        RegisterSignal(tamer, COMSIG_MOB_SAY, nameof(respond_to_command));
        RegisterSignal(tamer, COMSIG_MOB_AUTOMUTE_CHECK, nameof(waive_automute));
        RegisterSignal(tamer, COMSIG_MOB_CREATED_CALLOUT, nameof(respond_to_callout));
        if (Dm.truthy(requires_pointing))
            RegisterSignal(tamer, COMSIG_MOVABLE_POINTED, nameof(point_on_target));
    }

    /// Stop listening to a guy
    // tg: code/datums/components/pet_commands/pet_command.dm:47
    public virtual async DmTask remove_friend(mob_living unfriended = null)
    {
        UnregisterSignal(unfriended, Dm.list(COMSIG_MOB_SAY, COMSIG_MOB_AUTOMUTE_CHECK, COMSIG_MOB_CREATED_CALLOUT, COMSIG_MOVABLE_POINTED));
    }

    /// Stop the automute from triggering for commands (unless the spoken text is suspiciously longer than the command)
    // tg: code/datums/components/pet_commands/pet_command.dm:56
    [SignalHandler]
    public virtual object waive_automute(mob_living speaker = null, client client = null, object last_message = null, object mute_type = null)
    {
        if (Dm.eq(mute_type, MUTE_IC) && Dm.truthy(find_command_in_text(last_message, check_verbosity: TRUE)))
            return WAIVE_AUTOMUTE_CHECK;
        return NONE;
    }

    /// Respond to something that one of our friends has asked us to do
    // tg: code/datums/components/pet_commands/pet_command.dm:63
    [SignalHandler]
    public virtual void respond_to_command(mob_living speaker = null, DmList speech_args = null)
    {
        mob_living parent = weak_parent.resolve() as mob_living;
        if (parent == null)
            return;
        if (!Dm.truthy(can_see(parent, speaker, sense_radius))) // Basically the same rules as hearing
            return;

        object spoken_text = speech_args[SPEECH_MESSAGE];
        if (!Dm.truthy(find_command_in_text(spoken_text)))
            return;

        Waiting.Detach(try_activate_command(commander: speaker, radial_command: FALSE));
    }

    /// Respond to a callout
    // tg: code/datums/components/pet_commands/pet_command.dm:78
    [SignalHandler]
    public virtual void respond_to_callout(mob_living speaker = null, object callout = null, atom target = null)
    {
        if (callout_type == null || !ispath(callout, callout_type))
            return;

        mob_living parent = weak_parent.resolve() as mob_living;
        if (parent == null)
            return;

        if (!Dm.truthy(valid_callout_target(speaker, callout, target)))
        {
            bool found_new_target = false;
            foreach (atom new_target in Dm.each<atom>(range(2, target)))
            {
                if (Dm.truthy(valid_callout_target(speaker, callout, new_target)))
                {
                    target = new_target;
                    found_new_target = true;
                }
            }

            if (!found_new_target)
                return;
        }

        Waiting.Detach(callout_activate(speaker, parent, target));
    }

    // the two lines of respond_to_callout after its checks (tg calls try_activate_command in the signal handler; set_command_active sleeps nowhere there)
    private async DmTask callout_activate(mob_living speaker, mob_living parent, atom target)
    {
        if (Dm.truthy(await try_activate_command(commander: speaker, radial_command: FALSE)))
            await look_for_target(parent, target);
    }

    /// Does this callout with this target trigger this command?
    // tg: code/datums/components/pet_commands/pet_command.dm:102
    public virtual object valid_callout_target(mob_living speaker = null, object callout = null, atom target = null)
    {
        return TRUE;
    }

    /**
     * Returns true if we find any of our spoken commands in the text.
     * if check_verbosity is true, skip the match if there spoken_text is way longer than the match
     */
    // tg: code/datums/components/pet_commands/pet_command.dm:109
    public virtual object find_command_in_text(object spoken_text = null, float? check_verbosity = null)
    {
        check_verbosity ??= FALSE;
        foreach (object command in Dm.each<object>(speech_commands))
        {
            if (!Dm.truthy(findtext(spoken_text, command)))
                continue;
            if (Dm.truthy(check_verbosity) && length(spoken_text) > length(command) + MAX_NAME_LEN)
                continue;
            return TRUE;
        }
        return FALSE;
    }

    // tg: code/datums/components/pet_commands/pet_command.dm:118
    public virtual object pet_able_to_respond()
    {
        mob_living parent = weak_parent.resolve() as mob_living;
        if (parent == null || parent.ai_controller == null)
            return FALSE;
        if (Dm.truthy(parent.incapacitated)) // Probably can't hear them if we're dead
            return FALSE;
        return TRUE;
    }

    /// Apply a command state if conditions are right, return command if successful
    // tg: code/datums/components/pet_commands/pet_command.dm:127
    public virtual async DmTask<object> try_activate_command(mob_living commander = null, float? radial_command = null)
    {
        if (!Dm.truthy(pet_able_to_respond()))
            return FALSE;
        mob_living parent = weak_parent.resolve() as mob_living;
        await set_command_active(parent, commander, radial_command);
        return TRUE;
    }

    // tg: code/datums/components/pet_commands/pet_command.dm:134
    public virtual object generate_emote_command(atom target = null)
    {
        mob_living living_pet = weak_parent?.resolve() as mob_living;
        return living_pet == null ? null : retrieve_command_text(living_pet, target);
    }

    // tg: code/datums/components/pet_commands/pet_command.dm:138
    public virtual object retrieve_command_text(atom living_pet = null, atom target = null)
    {
        return Dm.text("signals [] to spring into action!", living_pet);
    }

    /// Target the pointed atom for actions
    // tg: code/datums/components/pet_commands/pet_command.dm:142
    public virtual async DmTask<object> look_for_target(mob_living friend = null, atom potential_target = null)
    {
        mob_living parent = weak_parent.resolve() as mob_living;
        if (!Dm.truthy(pet_able_to_respond()))
            return FALSE;
        if (Dm.eq((parent.ai_controller as datum_ai_controller).blackboard[BB_CURRENT_PET_TARGET], potential_target)) // That's already our target
            return FALSE;
        if (!Dm.truthy(can_see(parent, potential_target, sense_radius)))
            return FALSE;

        await set_command_target(parent, potential_target);
        return TRUE;
    }

    /// Activate the command, extend to add visible messages and the like
    // tg: code/datums/components/pet_commands/pet_command.dm:155
    public virtual async DmTask set_command_active(mob_living parent = null, mob_living commander = null, float? radial_command = null)
    {
        radial_command ??= FALSE;
        datum_ai_controller controller = parent.ai_controller as datum_ai_controller;
        controller.clear_blackboard_key(BB_CURRENT_PET_TARGET);

        datum_pet_command previous = controller.blackboard[BB_ACTIVE_PET_COMMAND] as datum_pet_command;
        if (previous != null && previous != this)
            await previous.command_ended(controller);
        controller.set_blackboard_key(BB_ACTIVE_PET_COMMAND, this);
        await execute_action(controller); // Install the BT override subtree for this command
        if (Dm.truthy(command_feedback))
            parent.balloon_alert_to_viewers(Dm.text("[]", command_feedback)); // If we get a nicer runechat way to do this, refactor this
        if (!Dm.truthy(radial_command))
            return;
        if (!Dm.truthy(requires_pointing))
        {
            object manual_emote_text = generate_emote_command();
            commander.manual_emote(manual_emote_text);
            return;
        }
        RegisterSignal(commander, COMSIG_MOB_CLICKON, nameof(click_on_target));
        if (commander.client != null)
            commander.client.mouse_override_icon = Dm.resource("icons/effects/mouse_pointers/pet_paw.dmi");
        commander.update_mouse_pointer();
    }

    /// Called when this command is replaced by another command or otherwise deactivated. Extend to add cleanup logic.
    // tg: code/datums/components/pet_commands/pet_command.dm:177
    public virtual async DmTask command_ended(datum_ai_controller controller = null)
    {
        await controller.set_behavior_tree_override(SUBPLAN_ID_PET_COMMAND, null);
    }

    // tg: code/datums/components/pet_commands/pet_command.dm:181
    [SignalHandler]
    public virtual object click_on_target(mob_living source = null, atom target = null, DmList modifiers = null)
    {
        if (!Dm.truthy(can_see(source, target, 9)))
            return COMSIG_MOB_CANCEL_CLICKON;
        object manual_emote_text = generate_emote_command(target);
        Waiting.Detach(click_on_target_set(source, target, manual_emote_text));
        UnregisterSignal(source, COMSIG_MOB_CLICKON);
        if (source.client != null)
            source.client.mouse_override_icon = null; // source.client::mouse_override_icon
        source.update_mouse_pointer();
        return COMSIG_MOB_CANCEL_CLICKON;
    }

    // the emote of click_on_target after the target took (on_target_set may wait on the tree's plan: tg's signal handler never sleeps there, and neither does this)
    private async DmTask click_on_target_set(mob_living source, atom target, object manual_emote_text)
    {
        if (Dm.truthy(await on_target_set(source, target)) && manual_emote_text != null)
            source.manual_emote(manual_emote_text);
    }

    // tg: code/datums/components/pet_commands/pet_command.dm:193
    [SignalHandler]
    public virtual void point_on_target(mob_living friend = null, atom potential_target = null)
    {
        Waiting.Detach(on_target_set(friend, potential_target));
    }

    /// Store the target for the AI blackboard
    // tg: code/datums/components/pet_commands/pet_command.dm:198
    public virtual async DmTask<object> set_command_target(mob_living parent = null, atom target = null)
    {
        (parent.ai_controller as datum_ai_controller).set_blackboard_key(BB_CURRENT_PET_TARGET, target);
        return TRUE;
    }

    /// Provide information about how to display this command in a radial menu
    // tg: code/datums/components/pet_commands/pet_command.dm:203
    public virtual object provide_radial_data()
    {
        if (Dm.truthy(hidden))
            return null;
        datum_radial_menu_choice choice = Dm.@new<datum_radial_menu_choice>();
        choice.name = command_name;
        choice.image = Dm.@new<icon>(radial_icon, radial_icon_state);
        return Dm.list(Dm.kv(Dm.text("[]", command_name), choice));
    }

    /**
     * Execute an AI action on the provided controller, what we should actually do when this command is active.
     * This should basically always be called from a planning subtree which passes its own controller.
     * Return SUBTREE_RETURN_FINISH_PLANNING to pass that instruction on to the controller, or don't if you don't want that.
     */
    // tg: code/datums/components/pet_commands/pet_command.dm:216
    public virtual async DmTask<object> execute_action(datum_ai_controller controller = null)
    {
        throw Dm.CRASH("Pet command execute action not implemented.", "code/datums/components/pet_commands/pet_command.dm", 218);
    }

    /// Target the pointed atom for actions
    // tg: code/datums/components/pet_commands/pet_command.dm:221
    public virtual async DmTask<object> on_target_set(mob_living friend = null, atom potential_target = null)
    {
        mob_living parent = weak_parent.resolve() as mob_living;
        if (parent == null)
            return FALSE;

        await (parent.ai_controller as datum_ai_controller).cancel_current_plan();
        if (!Dm.truthy(await look_for_target(friend, potential_target)) || !Dm.truthy(await set_command_target(parent, potential_target)))
            return FALSE;
        parent.visible_message(span_warning(Dm.text("[] follows []'s gesture towards [] []!", parent, friend, potential_target, pointed_reaction)));
        return TRUE;
    }
}
