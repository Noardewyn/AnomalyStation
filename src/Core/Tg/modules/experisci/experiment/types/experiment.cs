// Translated from tgstation (snapshot 1d99bd7f): code/modules/experisci/experiment/types/experiment.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (silicons SI3, D326: New and the reward's text; gaps RD3, D333: the rest): the experiment's base, which its handler
// checks (actionable), performs (perform_experiment: its actions, then the techweb's completion when it is complete) and shows in
// its window (to_ui_data, also read by the R&D console's and the operating computer's windows). The var block is generated (D111).

/**
 * # Experiment
 *
 * This is the base datum for experiments, storing the base definition.
 *
 * This class should be subclassed for producing actual experiments. The
 * proc stubs should be implemented in whole.
 */
// tg: code/modules/experisci/experiment/types/experiment.dm:9
public partial class datum_experiment : datum
{
    /**
     * Performs any necessary initialization of tags and other variables
     */
    // tg: code/modules/experisci/experiment/types/experiment.dm:34
    [DmParams("techweb")]
    public override async DmTask New(params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(traits, EXPERIMENT_TRAIT_DESTRUCTIVE)))
            exp_tag = Dm.text("Destructive []", exp_tag);
    }

    /**
     * Checks if the experiment is complete
     *
     * This proc should be overridden such that it returns TRUE/FALSE to
     * state if the experiment is complete or not.
     */
    // tg: code/modules/experisci/experiment/types/experiment.dm:44
    public virtual object is_complete()
    {
        return null;
    }

    /**
     * Gets the current progress towards the goal of the experiment
     *
     * This proc should be overridden such that the return value is a
     * list of lists, wherein each inner list represents a stage. Stages have
     * types, see _DEFINES/experisci.dm. Each stage should be constructed using
     * one of several available macros in that file.
     */
    // tg: code/modules/experisci/experiment/types/experiment.dm:55
    public virtual async DmTask<object> check_progress()
    {
        DmList dot = null;
        dot = Dm.list();
        return dot;
    }

    /**
     * Gets if the experiment is actionable provided some arguments
     *
     * This proc should be overridden such that the return value is a
     * boolean value representing if the experiment could be actioned with
     * the provided arguments.
     */
    // tg: code/modules/experisci/experiment/types/experiment.dm:65
    public virtual async DmTask<object> actionable(params object[] __extra)
    {
        return Dm.not(is_complete());
    }

    ///Called when the experiment is selected by an experiment handler, for specific signals and the such.
    // tg: code/modules/experisci/experiment/types/experiment.dm:69
    public virtual void on_selected(datum_component_experiment_handler experiment_handler = null)
    {
        return;
    }

    ///Called when the opposite happens.
    // tg: code/modules/experisci/experiment/types/experiment.dm:73
    public virtual void on_unselected(datum_component_experiment_handler experiment_handler = null)
    {
        return;
    }

    /**
     * Proc that tries to perform the experiment, and then checks if its completed.
     */
    // tg: code/modules/experisci/experiment/types/experiment.dm:79
    public virtual async DmTask<object> perform_experiment(datum_component_experiment_handler experiment_handler = null, params object[] __extra)
    {
        DmList args = Dm.args(experiment_handler, __extra);
        object action_successful = await Dm.Call(this, nameof(perform_experiment_actions), Dm.arglist(args));
        // tg passes the experiment datum, which has no turf, so tg's playsound returns at once (sound.dm:29): no scan sound plays here.
        playsound((datum)this as atom, SFX_INDUSTRIAL_SCAN, 20, TRUE, -2, TRUE, FALSE);
        if (Dm.truthy(is_complete()))
            await finish_experiment(experiment_handler);
        return action_successful;
    }

    /**
     * Attempts to perform the experiment provided some arguments
     *
     * This proc should be overridden such that the experiment will be actioned
     * with some defined arguments
     */
    // tg: code/modules/experisci/experiment/types/experiment.dm:92
    public virtual async DmTask<object> perform_experiment_actions(datum_component_experiment_handler experiment_handler = null, params object[] __extra)
    {
        return null;
    }

    /**
     * Called when you complete an experiment, makes sure the techwebs knows the experiment was finished, and tells everyone it happend, yay!
     */
    // tg: code/modules/experisci/experiment/types/experiment.dm:98
    public virtual async DmTask finish_experiment(datum_component_experiment_handler experiment_handler = null, datum_techweb linked_web_override = null)
    {
        completed = TRUE;
        if (!Dm.truthy(experiment_handler) && !Dm.truthy(linked_web_override))
            throw Dm.CRASH("finish_experiment() called without either experiment_handler or linked_web_override being set", "code/modules/experisci/experiment/types/experiment.dm", 101);
        experiment_handler?.selected_experiment = null;
        datum_techweb linked_web = (linked_web_override is var or__1 && Dm.truthy(or__1) ? or__1 : experiment_handler.linked_web);
        object announcetext = linked_web.complete_experiment(this);
        await announce_message_to_all(announcetext, linked_web);
    }

    /**
     * Announces a message to all experiment handlers
     *
     * Arguments:
     * * message - The message to announce
     * * linked_web - the linked techweb we want to target. Prevent experiment handlers not linked to said techweb from receiving the message
     */
    // tg: code/modules/experisci/experiment/types/experiment.dm:114
    public virtual async DmTask announce_message_to_all(object message = null, datum_techweb linked_web = null)
    {
        foreach (datum_component_experiment_handler experi_handler in Dm.each_cast<datum_component_experiment_handler>(GLOB.experiment_handlers))
        {
            if (experi_handler.linked_web != linked_web)
                continue;
            if (!Dm.truthy(Dm.bitand(experi_handler.config_flags, EXPERIMENT_CONFIG_ALWAYS_ANNOUNCE)) && !Dm.truthy(experi_handler.is_compatible_experiment(this)))
                continue;
            atom_movable experi_parent = experi_handler.parent as atom_movable;
            await experi_parent.say(message);
        }
    }

    // tg: code/modules/experisci/experiment/types/experiment.dm:123
    public virtual object get_points_reward_text()
    {
        DmList english_list_keys = Dm.list();
        foreach (object points_type in Dm.each(points_reward))
            Dm.append(ref english_list_keys, Dm.text("[] []", points_reward[points_type], points_type));
        return Dm.text("[] points", english_list(english_list_keys));
    }

    // tg: code/modules/experisci/experiment/types/experiment.dm:129
    public virtual async DmTask<object> to_ui_data()
    {
        return Dm.list(
            Dm.kv("name", name),
            Dm.kv("description", description),
            Dm.kv("tag", exp_tag),
            Dm.kv("progress", await check_progress()),
            Dm.kv("completed", completed),
            Dm.kv("performance_hint", performance_hint),
            Dm.kv("ref", REF(this)));
    }
}
