// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/codeword_hearing.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Syndicate package A2 (D316): the whole file (a traitor hears the code words highlighted: tg's span classes "blue" and "red", which
// the client's chat styles draw). The var block is generated but source (hand-declared here).

/**
 * Component that allows for highlighting of words or phrases in chat based on regular expressions.
 *
 * Hooks into the parent's COMSIG_MOVABLE_HEAR signal to wrap every regex match in the message
 * between <span class=''></span> tags with the provided span class. This modifies the output that
 * is sent to the parent's chat window.
 *
 * Removal of this component should be done by calling [GetComponents(/datum/component/codeword_hearing)]
 * on the parent and then iterating through all components calling [delete_if_from_source(source)].
 */
// tg: code/datums/components/codeword_hearing.dm:12
public partial class datum_component_codeword_hearing : datum_component
{
    /// The source of this component. Used to identify the source in delete_if_from_source since this component is COMPONENT_DUPE_ALLOWED.
    // tg: code/datums/components/codeword_hearing.dm:19  (rules 3.2: untyped in tg; the types tool read a typepath into it, tg
    // stores the antagonist datum that added it)
    public object source;

    // tg: code/datums/components/codeword_hearing.dm:12  (the var overrides; the generated var block drops its constructor once a var
    // is hand-declared)
    public datum_component_codeword_hearing()
    {
        dupe_mode = COMPONENT_DUPE_ALLOWED;
    }

    // tg: code/datums/components/codeword_hearing.dm:21
    [DmParams("codeword_regex", "highlight_span_class", "component_source")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        regex codeword_regex = Dm.arg(__extra, 0) as regex;
        object highlight_span_class = Dm.arg(__extra, 1);
        object component_source = Dm.arg(__extra, 2);
        if (!ismovable(parent))
            return COMPONENT_INCOMPATIBLE;

        replace_regex = codeword_regex;
        span_class = highlight_span_class;
        source = component_source;
        return await base.Initialize(__extra);
    }

    // tg: code/datums/components/codeword_hearing.dm:30
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_MOVABLE_HEAR, nameof(handle_hearing));
    }

    // tg: code/datums/components/codeword_hearing.dm:33
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, COMSIG_MOVABLE_HEAR);
    }

    /// Callback for COMSIG_MOVABLE_HEAR which highlights syndicate code phrases in chat.
    // tg: code/datums/components/codeword_hearing.dm:37
    [SignalHandler]
    public virtual void handle_hearing(datum source = null, DmList hearing_args = null)
    {
        mob_living owner = parent as mob_living;
        if (owner == null)
            return;

        // don't skip codewords when owner speaks
        if (HAS_TRAIT(owner, TRAIT_DEAF) || !Dm.truthy(owner.has_language(hearing_args[HEARING_LANGUAGE])))
            return;

        object message = hearing_args[HEARING_RAW_MESSAGE];
        message = replace_regex.Replace(message, Dm.text("<span class='[]'>$1</span>", span_class));
        hearing_args[HEARING_RAW_MESSAGE] = message;
    }

    /// Since a parent can have multiple of these components on them simultaneously, this allows a datum to delete components from a specific source.
    // tg: code/datums/components/codeword_hearing.dm:52
    public virtual object delete_if_from_source(object component_source = null)
    {
        if (source == component_source)
        {
            qdel(this);
            return TRUE;
        }

        return FALSE;
    }
}
