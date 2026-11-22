// Translated from tgstation (snapshot 1d99bd7f): code/modules/experisci/experiment/handlers/experiment_handler.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps RD3 (D333, the experiments): the whole file: the experiment handler, the component of every device that performs experiments
// (the experi-scanner, the destructive scanner, the operating computer): it links a techweb and one of its experiments, runs the
// selected experiment (or every one, always active) with what its device's signals hand it, and opens tg's ExperimentConfigure
// window. The var block is generated (D111).

/**
 * # Experiment Handler
 *
 * This is the component for interacting with experiments from a connected techweb. It is generic
 * and should be set-up to automatically work on any class it is attached to without outside code
 * (Excluding potential callbacks)
 */
// tg: code/modules/experisci/experiment/handlers/experiment_handler.dm:8
public partial class datum_component_experiment_handler : datum_component
{
    /**
     * Initializes a new instance of the experiment_handler component
     *
     * Arguments:
     * * allowed_experiments - The list of /datum/experiment types that can be performed with this component
     * * blacklisted_experiments - The list of /datum/experiment types that explicitly cannot be performed with this component
     * * config_mode - The define that determines how the experiment_handler should display the configuration UI
     * * disallowed_traits - Flags that control what experiment traits are blacklisted by this experiment handler
     * * config_flags - Flags that control the operational behaviour of the experiment handler, see experiment defines
     * * start_experiment_callback - When provided adds a UI button to use this callback to the start the experiment
     */
    // tg: code/modules/experisci/experiment/handlers/experiment_handler.dm:35
    [DmParams("allowed_experiments", "blacklisted_experiments", "config_mode", "disallowed_traits", "config_flags", "start_experiment_callback", "experiment_signals")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object allowed_experiments = Dm.arg(__extra, 0);
        object blacklisted_experiments = Dm.arg(__extra, 1);
        string config_mode = Dm.arg<string>(__extra, 2);
        object disallowed_traits = Dm.arg(__extra, 3);
        object config_flags = Dm.arg(__extra, 4);
        datum_callback start_experiment_callback = Dm.arg<datum_callback>(__extra, 5);
        DmList experiment_signals = Dm.arg<DmList>(__extra, 6);
        allowed_experiments ??= Dm.list();
        blacklisted_experiments ??= Dm.list();
        config_mode ??= EXPERIMENT_CONFIG_ATTACKSELF;
        object dot = await base.Initialize(Dm.forward(__extra, allowed_experiments, blacklisted_experiments, config_mode, disallowed_traits, config_flags, start_experiment_callback, experiment_signals));
        if (!ismovable(parent))
            return COMPONENT_INCOMPATIBLE;

        this.allowed_experiments = allowed_experiments as DmList;
        this.blacklisted_experiments = blacklisted_experiments as DmList;
        this.disallowed_traits = disallowed_traits;
        this.config_flags = config_flags;
        this.start_experiment_callback = start_experiment_callback;

        foreach (object signal in Dm.each(experiment_signals))
            RegisterSignal(parent, signal, experiment_signals[signal]);

        // Determine UI display mode
        switch (config_mode)
        {
            case EXPERIMENT_CONFIG_ATTACKSELF:
                RegisterSignal(parent, COMSIG_ITEM_ATTACK_SELF, nameof(configure_experiment));
                break;
            case EXPERIMENT_CONFIG_ALTCLICK:
                RegisterSignal(parent, COMSIG_CLICK_ALT, nameof(configure_experiment));
                break;
            case EXPERIMENT_CONFIG_CLICK:
                RegisterSignal(parent, COMSIG_ATOM_UI_INTERACT, nameof(configure_experiment_click));
                break;
            case EXPERIMENT_CONFIG_UI:
                RegisterSignal(parent, COMSIG_UI_ACT, nameof(ui_handle_experiment));
                break;
        }

        // Auto connect to the first visible techweb (useful for always active handlers)
        // Note this won't work at the moment for non-machines that have been included
        // on the map as the servers aren't initialized when the non-machines are initializing
        if (!Dm.truthy(Dm.bitand(config_flags, EXPERIMENT_CONFIG_NO_AUTOCONNECT)))
            CONNECT_TO_RND_SERVER_ROUNDSTART(ref linked_web, parent as atom);

        Dm.append(ref GLOB.experiment_handlers, this);
        return dot;
    }

    // tg: code/modules/experisci/experiment/handlers/experiment_handler.dm:75
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);
        Dm.remove(ref GLOB.experiment_handlers, this);
        return dot;
    }

    /**
     * Hooks on attack to try and run an experiment (When using a handheld handler)
     */
    // tg: code/modules/experisci/experiment/handlers/experiment_handler.dm:82
    [SignalHandler]
    public virtual object try_run_handheld_experiment(datum source = null, atom target = null, mob user = null, DmList modifiers = null)
    {
        if (!Dm.truthy(Waiting.NoSleep(should_run_handheld_experiment(source, target, user))))
            return null;
        Waiting.Detach(try_run_handheld_experiment_async(source, target, user));
        return COMPONENT_CANCEL_ATTACK_CHAIN;
    }

    /**
     * Provides feedback when an item isn't related to an experiment, and has fully passed the attack chain
     */
    // tg: code/modules/experisci/experiment/handlers/experiment_handler.dm:92
    [SignalHandler]
    public virtual void ignored_handheld_experiment_attempt(datum source = null, atom target = null, mob user = null, DmList modifiers = null)
    {
        if ((selected_experiment == null && !Dm.truthy(Dm.bitand(config_flags, EXPERIMENT_CONFIG_ALWAYS_ACTIVE))) || Dm.truthy(Dm.bitand(config_flags, EXPERIMENT_CONFIG_SILENT_FAIL)))
            return;
        playsound(user, Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 25);
        to_chat(user, span_notice(I18n.T("[] is not related to your currently selected experiment.", target)));
    }

    /**
     * Checks that an experiment can be run using the provided target, used for preventing the cancellation of the attack chain inappropriately
     */
    // tg: code/modules/experisci/experiment/handlers/experiment_handler.dm:102
    public virtual async DmTask<object> should_run_handheld_experiment(datum source = null, atom target = null, mob user = null, params object[] __extra)
    {
        // Check that there is actually an experiment selected
        if (selected_experiment == null && !Dm.truthy(Dm.bitand(config_flags, EXPERIMENT_CONFIG_ALWAYS_ACTIVE)))
            return null;
        if (!Dm.truthy(linked_web))
            return null;

        // Determine if this experiment is actionable with this target
        DmList arguments = Dm.list(this);
        DmList args = Dm.args(source, target, user, __extra);
        arguments = args.len > 1 ? Dm.add(arguments, args.Copy(2)) as DmList : arguments;
        if (Dm.truthy(Dm.bitand(config_flags, EXPERIMENT_CONFIG_ALWAYS_ACTIVE)))
        {
            foreach (datum_experiment experiment in Dm.each<datum_experiment>(linked_web.available_experiments))
            {
                if (Dm.truthy(await Dm.Call(experiment, nameof(datum_experiment.actionable), Dm.arglist(arguments))))
                    return TRUE;
            }
        }
        else
            return await Dm.Call(selected_experiment, nameof(datum_experiment.actionable), Dm.arglist(arguments));
        return null;
    }

    /**
     * This proc exists because Jared Fogle really likes async
     */
    // tg: code/modules/experisci/experiment/handlers/experiment_handler.dm:122
    public virtual async DmTask try_run_handheld_experiment_async(datum source = null, atom target = null, mob user = null)
    {
        if (selected_experiment == null && !Dm.truthy(Dm.bitand(config_flags, EXPERIMENT_CONFIG_ALWAYS_ACTIVE)))
        {
            if (!Dm.truthy(Dm.bitand(config_flags, EXPERIMENT_CONFIG_SILENT_FAIL)))
                to_chat(user, span_notice(I18n.T("You do not have an experiment selected!")));
            return;
        }
        if (!Dm.truthy(Dm.bitand(config_flags, EXPERIMENT_CONFIG_IMMEDIATE_ACTION)) && !Dm.truthy(await do_after(user, 1 * SECONDS, target: target)))
            return;
        if (Dm.truthy(await action_experiment(source, target)))
        {
            playsound(user, Dm.resource("sound/machines/ping.ogg"), 25);
            to_chat(user, span_notice(I18n.T("You scan [].", target)));
        }
        else if (!Dm.truthy(Dm.bitand(config_flags, EXPERIMENT_CONFIG_SILENT_FAIL)))
        {
            playsound(user, Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 25);
            to_chat(user, span_notice(I18n.T("[] is not related to your currently selected experiment.", target)));
        }
    }

    /**
     * Hooks on destructive scans to try and run an experiment (When using a handheld handler)
     */
    // tg: code/modules/experisci/experiment/handlers/experiment_handler.dm:139
    [SignalHandler]
    public virtual void try_run_destructive_experiment(datum source = null, DmList scanned_atoms = null)
    {
        atom_movable our_scanner = parent as atom_movable;
        if (selected_experiment == null)
        {
            if (!Dm.truthy(Dm.bitand(config_flags, EXPERIMENT_CONFIG_SILENT_FAIL)))
            {
                playsound(our_scanner, Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 25);
                to_chat(our_scanner, span_notice(I18n.T("No experiment selected!")));
            }
            return;
        }
        float successful_scan = 0;
        foreach (object scan_target in Dm.each(scanned_atoms))
        {
            if (Dm.truthy(Waiting.NoSleep(action_experiment(source, scan_target))))
                successful_scan = TRUE;
        }
        if (Dm.truthy(successful_scan))
        {
            playsound(our_scanner, Dm.resource("sound/machines/ping.ogg"), 25);
            to_chat(our_scanner, span_notice(I18n.T("The scan succeeds.")));
        }
        else if (!Dm.truthy(Dm.bitand(config_flags, EXPERIMENT_CONFIG_SILENT_FAIL)))
        {
            // tg passes the component, which has no turf, so tg's playsound returns at once (sound.dm:29): the buzz is not heard.
            playsound((datum)this as atom, Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 25);
            Waiting.NoSleep(our_scanner.say(I18n.T("The scan did not result in anything.")));
        }
    }

    /// Hooks on a successful autopsy experiment
    // tg: code/modules/experisci/experiment/handlers/experiment_handler.dm:159
    [SignalHandler]
    public virtual void try_run_autopsy_experiment(obj source = null, mob_living target = null)
    {
        if (Dm.truthy(Waiting.NoSleep(action_experiment(source, target))))
        {
            playsound(source, Dm.resource("sound/machines/ping.ogg"), 25);
            Waiting.NoSleep(source.say(I18n.T("New unique autopsy successfully catalogued.")));
        }
    }

    /**
     * Announces a message to this experiment handler
     *
     * Arguments:
     * * message - The message to announce
     */
    // tg: code/modules/experisci/experiment/handlers/experiment_handler.dm:172
    public virtual async DmTask announce_message(object message = null)
    {
        atom_movable experi_parent = parent as atom_movable;
        await experi_parent.say(message);
    }

    /**
     * Attempts to perform the selected experiment given some arguments
     */
    // tg: code/modules/experisci/experiment/handlers/experiment_handler.dm:179
    public virtual async DmTask<object> action_experiment(datum source = null, params object[] __extra)
    {
        // Check if an experiment is selected
        if (selected_experiment == null && !Dm.truthy(Dm.bitand(config_flags, EXPERIMENT_CONFIG_ALWAYS_ACTIVE)))
            return FALSE;

        // Get arguments for passing to the experiment[s]
        DmList arguments = Dm.list(this);
        DmList args = Dm.args(source, __extra);
        arguments = args.len > 1 ? Dm.add(arguments, args.Copy(2)) as DmList : arguments;

        // Check if this handler is configured to be always active, in which case we
        // attempt to action every experiment that is available to this handler.
        if (Dm.truthy(Dm.bitand(config_flags, EXPERIMENT_CONFIG_ALWAYS_ACTIVE)))
        {
            object any_success = null;
            foreach (datum_experiment experiment in Dm.each<datum_experiment>(linked_web.available_experiments))
            {
                // Because this checks any experiment, we have to ensure it is allowable to be selected with can_select_experiment(...)
                // this handles the handler's blacklist, whitelist, etc (potentially refactor this in the future if possible because this could be expensive)
                if (Dm.truthy(can_select_experiment(experiment)) && Dm.truthy(await Dm.Call(experiment, nameof(datum_experiment.actionable), Dm.arglist(arguments))) && Dm.truthy(await Dm.Call(experiment, nameof(datum_experiment.perform_experiment), Dm.arglist(arguments))))
                    any_success = TRUE;
            }
            return any_success;
        }
        else
        {
            // Returns true if the experiment was successfuly handled
            return (await Dm.Call(selected_experiment, nameof(datum_experiment.actionable), Dm.arglist(arguments)) is var and__1 && !Dm.truthy(and__1) ? and__1 : await Dm.Call(selected_experiment, nameof(datum_experiment.perform_experiment), Dm.arglist(arguments)));
        }
    }

    /**
     * Hook for handling UI interaction via signals
     */
    // tg: code/modules/experisci/experiment/handlers/experiment_handler.dm:205
    [SignalHandler]
    public virtual void ui_handle_experiment(datum source = null, mob user = null, object action = null)
    {
        object sw__1 = action;
        if (Dm.eq(sw__1, "open_experiments"))
            Waiting.DetachSync(() => configure_experiment(null, usr));
    }

    /**
     * Attempts to show the user the experiment configuration panel
     *
     * Arguments:
     * * user - The user to show the experiment configuration panel to
     */
    // tg: code/modules/experisci/experiment/handlers/experiment_handler.dm:217
    [SignalHandler]
    public virtual object configure_experiment(datum source = null, mob user = null)
    {
        Waiting.Detach(ui_interact(user));
        return CLICK_ACTION_SUCCESS;
    }

    /**
     * Attempts to show the user the experiment configuration panel
     *
     * Arguments:
     * * user - The user to show the experiment configuration panel to
     */
    // tg: code/modules/experisci/experiment/handlers/experiment_handler.dm:228
    [SignalHandler]
    public virtual void configure_experiment_click(datum source = null, mob user = null)
    {
        Waiting.Detach(ui_interact(user));
    }

    /**
     * Attempts to link this experiment_handler to a provided techweb
     *
     * This proc attempts to link the handler to a provided techweb, overriding the existing techweb if relevant
     *
     * Arguments:
     * * new_web - The new techweb to link to
     */
    // tg: code/modules/experisci/experiment/handlers/experiment_handler.dm:240
    public virtual void link_techweb(datum_techweb new_web = null)
    {
        if (new_web == linked_web)
            return;
        selected_experiment?.on_unselected(this);
        selected_experiment = null;
        linked_web = new_web;
    }

    /**
     * Unlinks this handler from the selected techweb
     */
    // tg: code/modules/experisci/experiment/handlers/experiment_handler.dm:250
    public virtual void unlink_techweb()
    {
        selected_experiment?.on_unselected(this);
        selected_experiment = null;
        linked_web = null;
    }

    /**
     * Attempts to link this experiment_handler to a provided experiment
     *
     * Arguments:
     * * experiment - The experiment to attempt to link to
     */
    // tg: code/modules/experisci/experiment/handlers/experiment_handler.dm:261
    public virtual void link_experiment(datum_experiment experiment = null)
    {
        if (Dm.truthy(can_select_experiment(experiment)))
        {
            unlink_experiment();
            selected_experiment = experiment;
            selected_experiment.on_selected(this);
        }
    }

    /**
     * Unlinks this handler from the selected experiment
     */
    // tg: code/modules/experisci/experiment/handlers/experiment_handler.dm:270
    public virtual void unlink_experiment()
    {
        selected_experiment?.on_unselected(this);
        selected_experiment = null;
    }

    /**
     * Checks if an experiment is valid to be selected by this handler
     *
     * Arguments:
     * * experiment - The experiment to check
     */
    // tg: code/modules/experisci/experiment/handlers/experiment_handler.dm:280
    public virtual object can_select_experiment(datum_experiment experiment = null)
    {
        // Check that this experiment is visible currently
        if (!Dm.@in(experiment, linked_web?.available_experiments))
            return FALSE;

        return is_compatible_experiment(experiment);
    }

    /**
     * Checks if an experiment could be selected by the handler if it were available in the linked web.
     * Basically, it skips the available_experiments check, unlike can_select_experiment.
     */
    // tg: code/modules/experisci/experiment/handlers/experiment_handler.dm:291
    public virtual object is_compatible_experiment(datum_experiment experiment = null)
    {
        // Check that this experiments has no disallowed traits
        if (Dm.truthy(Dm.bitand(experiment.traits, disallowed_traits)))
            return FALSE;

        // Check against the list of allowed experimentors
        if (Dm.truthy(length(experiment.allowed_experimentors)) && !Dm.truthy(is_type_in_list(parent, experiment.allowed_experimentors)))
            return FALSE;

        // Check that this experiment type isn't blacklisted
        if (Dm.truthy(is_type_in_list(experiment, blacklisted_experiments)))
            return FALSE;

        // Finally, check against the allowed experiment types
        return is_type_in_list(experiment, allowed_experiments);
    }

    // tg: code/modules/experisci/experiment/handlers/experiment_handler.dm:307
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            atom parent_atom = parent as atom;
            ui = Dm.@new<datum_tgui>(user, this, "ExperimentConfigure", I18n.T("[]Experiment Configuration", Dm.truthy(parent_atom) ? I18n.T("[] | ", parent_atom.name) : ""));
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/experisci/experiment/handlers/experiment_handler.dm:314
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList dot = null;
        dot = Dm.list(
            Dm.kv("always_active", Dm.bitand(config_flags, EXPERIMENT_CONFIG_ALWAYS_ACTIVE)),
            Dm.kv("has_start_callback", Dm.num(start_experiment_callback != null)));
        dot["techwebs"] = Dm.list();
        foreach (datum_techweb techwebs in Dm.each_cast<datum_techweb>(SSresearch.techwebs))
        {
            if (!Dm.truthy(length(techwebs.techweb_servers))) //no servers, we don't care
            {
                if (techwebs == linked_web) //disconnect if OUR techweb lost their servers.
                    unlink_techweb();
                continue;
            }
            if (!Dm.truthy(length(SSresearch.find_valid_servers(get_turf(parent), techwebs))))
                continue;
            DmList data = Dm.list(
                Dm.kv("web_id", techwebs.id),
                Dm.kv("web_org", techwebs.organization),
                Dm.kv("selected", Dm.num(techwebs == linked_web)),
                Dm.kv("ref", REF(techwebs)),
                Dm.kv("all_servers", techwebs.techweb_servers));
            Dm.append_at(dot, "techwebs", Dm.list(data));
        }
        dot["experiments"] = Dm.list();
        if (Dm.truthy(linked_web))
        {
            foreach (datum_experiment experiment in Dm.each_cast<datum_experiment>(linked_web.available_experiments))
            {
                if (!Dm.truthy(can_select_experiment(experiment)))
                    continue;
                Dm.append_at(dot, "experiments", Dm.list(Dm.add(Dm.list(Dm.kv("selected", Dm.num(selected_experiment == experiment))), await experiment.to_ui_data())));
            }
        }
        return dot;
    }

    // tg: code/modules/experisci/experiment/handlers/experiment_handler.dm:342
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        object sw__1 = action;
        if (Dm.eq(sw__1, "select_server"))
        {
            dot = TRUE;
            datum_techweb new_techweb = Dm.locate_ref(@params["ref"]) as datum_techweb;
            if (Dm.truthy(new_techweb))
            {
                link_techweb(new_techweb);
                return dot;
            }
        }
        else if (Dm.eq(sw__1, "clear_server"))
        {
            dot = TRUE;
            unlink_techweb();
        }
        else if (Dm.eq(sw__1, "select_experiment"))
        {
            dot = TRUE;
            // Don't allow selection for always actives (no concept of active)
            if (Dm.truthy(Dm.bitand(config_flags, EXPERIMENT_CONFIG_ALWAYS_ACTIVE)))
                return dot;
            datum_experiment experiment = Dm.locate_ref(@params["ref"]) as datum_experiment;
            if (Dm.truthy(experiment))
                link_experiment(experiment);
        }
        else if (Dm.eq(sw__1, "clear_experiment"))
        {
            dot = TRUE;
            unlink_experiment();
        }
        else if (Dm.eq(sw__1, "start_experiment_callback"))
            await start_experiment_callback.Invoke(selected_experiment);
        return dot;
    }
}
