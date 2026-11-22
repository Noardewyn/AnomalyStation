// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/deadchat_control.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file but the immovable rod's subtype (the rod stays inert) and the admins' removal from the variable editor's dropdown
// (the view variables panel, not ported: handle_vv_topic and async_handle_vv_topic are live-partial.txt rows, so the component is
// live since gaps E1a, with the ghosts' alert of notify_ghosts). The var blocks are generated.

/**
 * Deadchat Plays Things - The Componenting
 *
 * Allows deadchat to control stuff and things by typing commands into chat.
 * These commands will then trigger callbacks to execute procs!
 */
// tg: code/datums/components/deadchat_control.dm:7
public partial class datum_component_deadchat_control : datum_component
{
    // tg: code/datums/components/deadchat_control.dm:27
    [DmParams("_deadchat_mode", "_inputs", "_input_cooldown", "_on_removal")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object _deadchat_mode = Dm.arg(__extra, 0);
        DmList _inputs = Dm.arg(__extra, 1) as DmList;
        object _input_cooldown = Dm.arg(__extra, 2) ?? 12 * SECONDS;
        datum_callback _on_removal = Dm.arg(__extra, 3) as datum_callback;
        if (!isatom(parent))
            return COMPONENT_INCOMPATIBLE;
        RegisterSignal(parent, COMSIG_ATOM_ORBIT_BEGIN, nameof(orbit_begin));
        RegisterSignal(parent, COMSIG_ATOM_ORBIT_STOP, nameof(orbit_stop));
        RegisterSignal(parent, COMSIG_ATOM_EXAMINE, nameof(on_examine));
        deadchat_mode = _deadchat_mode;
        inputs = _inputs;
        input_cooldown = _input_cooldown;
        on_removal = _on_removal;
        if (Dm.truthy(Dm.bitand(deadchat_mode, DEMOCRACY_MODE)))
        {
            if (Dm.truthy(Dm.bitand(deadchat_mode, ANARCHY_MODE))) // Choose one, please.
                stack_trace(Dm.text("deadchat_control component added to [] with both democracy and anarchy modes enabled.", parent.type), "code/datums/components/deadchat_control.dm", 43);
            timerid = _addtimer(Dm.@new<datum_callback>(this, nameof(democracy_loop)), Dm.num(input_cooldown), Dm.bitor(TIMER_STOPPABLE, TIMER_LOOP), file: "code/datums/components/deadchat_control.dm", line: 45);
        }
        if (!ismob(parent) && !Dm.truthy(await SSpoints_of_interest.is_valid_poi(parent as atom)))
        {
            Waiting.NoSleep(SSpoints_of_interest.make_point_of_interest(parent as atom));
            generated_point_of_interest = TRUE;
        }

        string parent_name = Dm.text("[]", parent);
        if (ismob(parent))
        {
            var mob_parent = (mob)parent;
            parent_name = Dm.text("[]", mob_parent.real_name);
        }
        notify_ghosts(
            I18n.T("[] is now deadchat controllable!", parent_name),
            source: parent as atom,
            header: I18n.T("Ghost Possession!"));
        return null;
    }

    // tg: code/datums/components/deadchat_control.dm:59
    public override object Destroy(float? force = null)
    {
        if (on_removal != null)
            Waiting.NoSleep(on_removal.Invoke());
        inputs = null;
        orbiters = null;
        ckey_to_cooldown = null;
        if (Dm.truthy(generated_point_of_interest))
            SSpoints_of_interest.remove_point_of_interest(parent as atom);
        on_removal = null;
        return base.Destroy(force);
    }

    // tg: code/datums/components/deadchat_control.dm:69
    [SignalHandler]
    public virtual object deadchat_react(mob source = null, object message = null)
    {
        message = LOWER_TEXT(message);

        if (!Dm.truthy(inputs[message]))
            return null;

        if (Dm.truthy(Dm.bitand(deadchat_mode, ANARCHY_MODE)))
        {
            if (source == null || !Dm.truthy(source.ckey))
                return null;
            float cooldown = Dm.num(ckey_to_cooldown[source.ckey]) - Dm.num(world.time);
            if (cooldown > 0)
            {
                to_chat(source, span_warning(I18n.T("Your deadchat control inputs are still on cooldown for another [] second\\s.", CEILING(cooldown * 0.1f, 1))));
                return MOB_DEADSAY_SIGNAL_INTERCEPT;
            }
            ckey_to_cooldown[source.ckey] = Dm.num(world.time) + Dm.num(input_cooldown);
            _addtimer(Dm.@new<datum_callback>(this, nameof(end_cooldown), source.ckey), Dm.num(input_cooldown), file: "code/datums/components/deadchat_control.dm", line: 85);
            Waiting.NoSleep(((datum_callback)inputs[message]).Invoke());
            to_chat(source, span_notice(I18n.T("\"[]\" input accepted. You are now on cooldown for [] second\\s.", message, Dm.num(input_cooldown) * 0.1f)));
            return MOB_DEADSAY_SIGNAL_INTERCEPT;
        }

        if (Dm.truthy(Dm.bitand(deadchat_mode, DEMOCRACY_MODE)))
        {
            ckey_to_cooldown[source.ckey] = message;
            to_chat(source, span_notice(I18n.T("You have voted for \"[]\".", message)));
            return MOB_DEADSAY_SIGNAL_INTERCEPT;
        }
        return null;
    }

    // tg: code/datums/components/deadchat_control.dm:95
    public virtual void democracy_loop()
    {
        if (QDELETED(parent) || !Dm.truthy(Dm.bitand(deadchat_mode, DEMOCRACY_MODE)))
        {
            deltimer(timerid);
            return;
        }
        object result = count_democracy_votes();
        if (result != null)
        {
            Waiting.NoSleep(((datum_callback)inputs[result]).Invoke());
            if (!Dm.truthy(Dm.bitand(deadchat_mode, MUTE_DEMOCRACY_MESSAGES)))
            {
                string message = I18n.T("<span class='deadsay italics bold'>[] has done action []!<br>New vote started. It will end in [] second\\s.</span>", parent, result, Dm.num(input_cooldown) * 0.1f);
                foreach (object M in Dm.each(orbiters))
                    to_chat(M, message);
            }
        }
        else if (!Dm.truthy(Dm.bitand(deadchat_mode, MUTE_DEMOCRACY_MESSAGES)))
        {
            string message = I18n.T("<span class='deadsay italics bold'>No votes were cast this cycle.</span>");
            foreach (object M in Dm.each(orbiters))
                to_chat(M, message);
        }
    }

    // tg: code/datums/components/deadchat_control.dm:113
    public virtual object count_democracy_votes()
    {
        if (!Dm.truthy(Dm.length(ckey_to_cooldown)))
            return null;
        DmList votes = Dm.list();
        foreach (object command in Dm.each(inputs))
            votes[Dm.text("[]", command)] = 0;
        foreach (object vote in Dm.each(ckey_to_cooldown.Copy()))
        {
            object key = ckey_to_cooldown[vote];
            votes[key] = Dm.num(votes[key]) + 1;
            ckey_to_cooldown.Remove(vote);
        }

        // Solve which had most votes.
        float prev_value = 0;
        object result = null;
        foreach (object vote in Dm.each(votes))
        {
            if (Dm.num(votes[vote]) > prev_value)
            {
                prev_value = Dm.num(votes[vote]);
                result = vote;
            }
        }

        if (Dm.@in(result, inputs))
            return result;
        return null;
    }

    // tg: code/datums/components/deadchat_control.dm:134
    public override async DmTask<object> vv_edit_var(object var_name = null, object var_value = null)
    {
        object dot = await base.vv_edit_var(var_name, var_value);
        if (!Dm.truthy(dot))
            return dot;
        if (!Dm.eq(var_name, nameof(deadchat_mode)))
            return dot;
        ckey_to_cooldown = Dm.list();
        if (Dm.eq(var_value, DEMOCRACY_MODE))
            timerid = _addtimer(Dm.@new<datum_callback>(this, nameof(democracy_loop)), Dm.num(input_cooldown), Dm.bitor(TIMER_STOPPABLE, TIMER_LOOP), file: "code/datums/components/deadchat_control.dm", line: 141);
        else
            deltimer(timerid);
        return dot;
    }

    // tg: code/datums/components/deadchat_control.dm:145
    [SignalHandler]
    public virtual void orbit_begin(atom source = null, atom orbiter = null)
    {
        RegisterSignal(orbiter, COMSIG_MOB_DEADSAY, nameof(deadchat_react));
        RegisterSignal(orbiter, COMSIG_MOB_AUTOMUTE_CHECK, nameof(waive_automute));
        Dm.combine(ref orbiters, orbiter);
    }

    // tg: code/datums/components/deadchat_control.dm:153
    [SignalHandler]
    public virtual void orbit_stop(atom source = null, atom orbiter = null)
    {
        if (Dm.@in(orbiter, orbiters))
        {
            UnregisterSignal(orbiter, Dm.list(COMSIG_MOB_DEADSAY, COMSIG_MOB_AUTOMUTE_CHECK));
            Dm.remove(ref orbiters, orbiter);
        }
    }

    /**
     * Prevents messages used to control the parent from counting towards the automute threshold for repeated identical messages.
     *
     * Arguments:
     * - [speaker][/client]: The mob that is trying to speak.
     * - [client][/client]: The client that is trying to speak.
     * - message: The message that the speaker is trying to say.
     * - mute_type: Which type of mute the message counts towards.
     */
    // tg: code/datums/components/deadchat_control.dm:172
    [SignalHandler]
    public virtual object waive_automute(mob speaker = null, client client = null, object message = null, object mute_type = null)
    {
        if (Dm.eq(mute_type, MUTE_DEADCHAT) && Dm.truthy(inputs[LOWER_TEXT(message)]))
            return WAIVE_AUTOMUTE_CHECK;
        return NONE;
    }

    /// Informs any examiners to the inputs available as part of deadchat control, as well as the current operating mode and cooldowns.
    // tg: code/datums/components/deadchat_control.dm:201
    [SignalHandler]
    public virtual void on_examine(atom A = null, mob user = null, DmList examine_list = null)
    {
        if (!isobserver(user))
            return;

        Dm.append(ref examine_list, span_notice(I18n.T("[] currently under deadchat control using the [] ruleset!", A.p_Theyre(), Dm.truthy(Dm.bitand(deadchat_mode, DEMOCRACY_MODE)) ? "democracy" : "anarchy")));

        if (Dm.truthy(Dm.bitand(deadchat_mode, DEMOCRACY_MODE)))
            Dm.append(ref examine_list, span_notice(I18n.T("Type a command into chat to vote on an action. This happens once every [] second\\s.", Dm.num(input_cooldown) * 0.1f)));
        else if (Dm.truthy(Dm.bitand(deadchat_mode, ANARCHY_MODE)))
            Dm.append(ref examine_list, span_notice(I18n.T("Type a command into chat to perform. You may do this once every [] second\\s.", Dm.num(input_cooldown) * 0.1f)));

        string extended_examine = "<span class='notice'>Command list:";

        foreach (object possible_input in Dm.each(inputs))
            extended_examine += Dm.text(" []", possible_input);

        extended_examine += ".</span>";

        Dm.append(ref examine_list, extended_examine);
    }

    ///Removes the ghost from the ckey_to_cooldown list and lets them know they are free to submit a command for the parent again.
    // tg: code/datums/components/deadchat_control.dm:224
    public virtual void end_cooldown(object ghost_ckey = null)
    {
        Dm.remove(ref ckey_to_cooldown, ghost_ckey);
        mob ghost = get_mob_by_ckey(ghost_ckey) as mob;
        if (ghost == null || isliving(ghost))
            return;
        to_chat(ghost, Dm.text("[] <span class='nicegreen'>Your deadchat control inputs for [] are no longer on cooldown.</span>", FOLLOW_LINK(ghost, parent), parent));
    }
}

/**
 * Deadchat Moves Things
 *
 * A special variant of the deadchat_control component that comes pre-baked with all the hottest inputs for a spicy
 * singularity or vomit goose.
 */
// tg: code/datums/components/deadchat_control.dm:237
public partial class datum_component_deadchat_control_cardinal_movement : datum_component_deadchat_control
{
    // tg: code/datums/components/deadchat_control.dm:237
    [DmParams("_deadchat_mode", "_inputs", "_input_cooldown", "_on_removal")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        if (!ismovable(parent))
            return COMPONENT_INCOMPATIBLE;

        object dot = await base.Initialize(__extra);

        inputs["up"] = Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs._step), parent, NORTH);
        inputs["down"] = Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs._step), parent, SOUTH);
        inputs["left"] = Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs._step), parent, WEST);
        inputs["right"] = Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs._step), parent, EAST);
        return dot;
    }
}

public static partial class GlobalProcs
{
    // tg: code/modules/admin/verbs/SDQL2/SDQL_2_wrappers.dm:250  (the callback target of the movement commands)
    public static object _step(object @ref = null, object dir = null)
    {
        return Waiting.NoSleep(step(@ref as atom_movable, Dm.num(dir)));
    }
}
